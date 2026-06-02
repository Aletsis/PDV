# 🛠️ Manual de DevOps y Despliegue en Producción - Sistema PDV

Este manual proporciona la documentación de arquitectura, pipelines de CI/CD, estrategias de monitoreo, planes de respaldo y guías paso a paso para el despliegue del sistema de **Punto de Venta (PDV)** en producción.

---

## 🗺️ 1. Arquitectura de Infraestructura de Despliegue

El sistema implementa una **Arquitectura Híbrida Edge-Cloud**. Las cajas locales operan de forma autónoma (incluso sin internet) y sincronizan sus ventas y catálogos en segundo plano hacia un servidor central.

```mermaid
graph TD
    subgraph Servidor Nube Central (Producción)
        direction TB
        LB[Nginx / Apache / Cloud LB] -->|HTTPS & WebSockets| WebApp[PDV.WebUI Containers]
        WebApp -->|Read/Write Pool| DB_Master[(PostgreSQL Primary)]
        DB_Master -->|Replicación Streaming| DB_Replica[(PostgreSQL Read-Replica)]
    end

    subgraph Red Local Sucursal (Edge)
        Browser[POS Web UI] -->|Sincronización HTTPS| LB
        Browser -->|WebSockets Local| HardwareAgent[PDV.HardwareAgent - Windows Service]
        HardwareAgent -->|Puertos COM/USB| TicketPrinter[Impresora Térmica]
        HardwareAgent -->|Puerto Serial| Scale[Báscula de Peso]
        Browser -->|Lecturas offline| SQLite[(SQLite Local - pdv.db)]
    end
```

### Directrices de Alta Disponibilidad (HA) y Escalabilidad
1. **Afinidad de Sesión (Sticky Sessions)**: Blazor Server utiliza conexiones WebSocket activas (SignalR) para mantener el estado del cliente en la memoria del servidor. Si despliega detrás de un balanceador de carga con múltiples instancias, **debe habilitar cookies de afinidad / sticky sessions** para garantizar que el cliente mantenga su conexión al mismo servidor.
2. **Aislamiento de Red**: La base de datos PostgreSQL debe alojarse en una subred privada, permitiendo únicamente conexiones entrantes desde las direcciones IP privadas del clúster de servidores web en el puerto `5432`.
3. **Resiliencia de Conexión**: La aplicación implementa reintentos automáticos de conexión a la base de datos a través de Entity Framework Core para tolerar interrupciones breves de red.

---

## 📦 2. Guías de Despliegue Paso a Paso

### Opción A: Contenedores (Docker / Podman)
Ideal para entornos modernos de microservicios y despliegues estandarizados.

#### 1. Despliegue con Docker Compose
1. Clonar el repositorio en el servidor de producción.
2. Modificar el archivo `docker-compose.yml` para establecer contraseñas seguras y configurar el volumen de almacenamiento:
   ```bash
   nano docker-compose.yml
   ```
3. Levantar la infraestructura completa (Base de datos y Servidor Web Blazor):
   ```bash
   docker compose up -d --build
   ```
4. El contenedor web aplicará automáticamente las migraciones pendientes gracias a la variable de entorno `APPLY_MIGRATIONS=true`.

#### 2. Despliegue usando Podman (Alternativa Rootless)
Si prefiere ejecutar contenedores sin privilegios de root por motivos de seguridad:
1. Construir la imagen del frontend:
   ```bash
   podman build -t pdv-webui:latest -f src/PDV.WebUI/Dockerfile .
   ```
2. Crear un pod para agrupar los contenedores y compartir la red local:
   ```bash
   podman pod create --name pdv-pod -p 5000:5000
   ```
3. Ejecutar la base de datos PostgreSQL y la WebUI dentro del pod:
   ```bash
   podman run -d --pod pdv-pod --name pdv-db -v pgdata:/var/lib/postgresql/data:Z -e POSTGRES_DB=pdv_db -e POSTGRES_USER=pdv_user -e POSTGRES_PASSWORD=ClaveSegura postgres:15-alpine
   podman run -d --pod pdv-pod --name pdv-app -v webui-logs:/app/Logs:Z -e ConnectionStrings__DefaultConnection="Host=127.0.0.1;Database=pdv_db;Username=pdv_user;Password=ClaveSegura" -e APPLY_MIGRATIONS=true pdv-webui:latest
   ```

---

### Opción B: Servidor Propio / VPS Linux (Nginx o Apache)
Despliegue tradicional de alto rendimiento y bajo consumo sobre Ubuntu 22.04 LTS o Debian 12.

#### 1. Preparación del Servidor
Instalar el runtime de .NET 9 en el servidor:
```bash
sudo apt-get update && sudo apt-get install -y dotnet-runtime-9.0
```

#### 2. Configurar el Servicio de la Aplicación (Systemd)
1. Publicar la aplicación localmente y subir el contenido de la carpeta `/publish` a `/var/www/pdv` en el servidor VPS.
2. Dar permisos al servicio de ejecución:
   ```bash
   sudo chmod +x /var/www/pdv/PDV.WebUI
   sudo chown -R www-data:www-data /var/www/pdv
   ```
3. Registrar el servicio en el sistema:
   ```bash
   sudo nano /etc/systemd/system/pdv.service
   ```
   *Pegar el siguiente bloque de configuración:*
   ```ini
   [Unit]
   Description=Aplicación Blazor Punto de Venta (PDV)
   After=network.target postgresql.service

   [Service]
   WorkingDirectory=/var/www/pdv
   ExecStart=/var/www/pdv/PDV.WebUI --urls=http://127.0.0.1:5000
   Restart=always
   RestartSec=10
   KillSignal=SIGINT
   SyslogIdentifier=pdv-app
   User=www-data
   Environment=ASPNETCORE_ENVIRONMENT=Production
   Environment=RunMode=Server
   Environment=ConnectionStrings__DefaultConnection=Host=127.0.0.1;Database=pdv_db;Username=pdv_user;Password=ClaveSegura;
   Environment=APPLY_MIGRATIONS=true

   [Install]
   WantedBy=multi-user.target
   ```
4. Iniciar y habilitar el arranque automático:
   ```bash
   sudo systemctl daemon-reload
   sudo systemctl enable pdv.service
   sudo systemctl start pdv.service
   ```

#### 3. Configuración del Servidor Web como Proxy Reverso

##### Con Nginx (Recomendado)
1. Copiar el contenido del archivo `nginx.conf` del repositorio a `/etc/nginx/sites-available/pdv`.
2. Habilitar el sitio y verificar la sintaxis:
   ```bash
   sudo ln -s /etc/nginx/sites-available/pdv /etc/nginx/sites-enabled/
   sudo nginx -t
   sudo systemctl restart nginx
   ```

##### Con Apache
1. Asegurar que los módulos necesarios estén activos:
   ```bash
   sudo a2enmod ssl proxy proxy_http proxy_wstunnel rewrite headers deflate
   ```
2. Copiar el archivo `httpd-apache.conf` a `/etc/apache2/sites-available/pdv.conf`.
3. Habilitar el VirtualHost y reiniciar Apache:
   ```bash
   sudo a2ensite pdv.conf
   sudo systemctl restart apache2
   ```

---

### Opción C: Servidor IIS (Windows Server)
Para despliegues en infraestructura local corporativa basada en Windows Server.

1. **Instalar Dependencias**:
   - Descargar e instalar el **ASP.NET Core Hosting Bundle** para .NET 9.
   - Habilitar el rol de IIS en el Administrador de Servidores incluyendo soporte para **WebSockets**.
2. **Publicar Aplicación**:
   - Ejecutar en consola local:
     ```powershell
     dotnet publish src/PDV.WebUI/PDV.WebUI.csproj -c Release -r win-x64 --self-contained true -o ./publish-windows
     ```
   - Copiar la carpeta generada al servidor IIS (ej. `C:\inetpub\wwwroot\pdv`).
3. **Crear Sitio en IIS**:
   - Crear un sitio web apuntando a la ruta física.
   - En **Application Pools**, seleccionar el pool de la aplicación -> *Configuración Básica* -> Cambiar versión del CLR a **Sin código administrado** (No Managed Code).
4. **Configurar Variables de Entorno en `web.config`**:
   Insertar la cadena de conexión de producción y el modo de ejecución:
   ```xml
   <aspNetCore processPath=".\PDV.WebUI.exe" arguments="" stdoutLogEnabled="true" stdoutLogFile=".\logs\stdout" hostingModel="inprocess">
     <environmentVariables>
       <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" />
       <environmentVariable name="RunMode" value="Server" />
       <environmentVariable name="ConnectionStrings__DefaultConnection" value="Host=localhost;Database=pdv_db;Username=pdv_user;Password=ClaveSegura;" />
       <environmentVariable name="APPLY_MIGRATIONS" value="true" />
     </environmentVariables>
   </aspNetCore>
   ```

---

### Opción D: Servicios Cloud Nativos (PaaS & Serverless)

#### 1. Azure App Service (Linux)
Ideal para administración mínima y excelente escalabilidad en Microsoft Azure.

1. **Creación del App Service**:
   - Crear un recurso "Web App" en el portal de Azure.
   - Publicar como: **Docker Container**.
   - Sistema Operativo: **Linux**.
   - Plan de tarifas: B1 mínimo (para habilitar WebSockets).
2. **Configuración de Variables de Entorno (Application Settings)**:
   - Configurar en la sección *Environment variables*:
     - `ConnectionStrings__DefaultConnection`: `<tu_conexion_postgres>`
     - `RunMode`: `Server`
     - `APPLY_MIGRATIONS`: `true`
3. **Habilitar WebSockets (Crítico para Blazor Server)**:
   - Ir a la pestaña **Configuration** -> **General Settings**.
   - Cambiar **Web sockets** a **On**. (Si olvida este paso, la aplicación presentará latencia alta al caer en HTTP Long Polling).
4. **Habilitar Afinidades de Sesión**:
   - En **General Settings**, activar **ARR Affinity** a **On** para que el balanceador mantenga al usuario conectado a la misma instancia que aloja su circuito Blazor.

#### 2. AWS Elastic Beanstalk
Despliegue rápido con orquestación automática en Amazon Web Services.

1. **Creación del Entorno**:
   - Crear una nueva aplicación en Elastic Beanstalk.
   - Plataforma: **Docker**.
   - Rama de plataforma: **Docker running on 64bit Amazon Linux 2**.
2. **Archivo de Despliegue (`Dockerrun.aws.json`)**:
   Crear un archivo en la raíz para indicarle a AWS cómo correr la imagen:
   ```json
   {
     "AWSEbdockerrunVersion": "1",
     "Image": {
       "Name": "ghcr.io/tu-usuario/pdv-webui:latest",
       "Update": "true"
     },
     "Ports": [
       {
         "ContainerPort": 5000
       }
     ]
   }
   ```
3. **Configuración del Balanceador de Carga (ALB)**:
   - Configurar el balanceador de carga del entorno como **Application Load Balancer** (ALB).
   - Habilitar **Sticky Sessions** en el grupo de destino (Target Group) utilizando cookies de aplicación o del balanceador (duración recomendada: 1 hora) para dar soporte al circuito de SignalR.

#### 3. Google Cloud Run
Plataforma Serverless de alta escalabilidad y bajo costo basada en contenedores.

1. **Preparación de la Base de Datos**:
   - Crear una instancia de PostgreSQL en **Google Cloud SQL** dentro de la misma VPC.
2. **Despliegue del Contenedor**:
   Ejecutar la herramienta de CLI de gcloud para compilar en la nube e implementar:
   ```bash
   gcloud run deploy pdv-app \
     --image=ghcr.io/tu-usuario/pdv-webui:latest \
     --platform=managed \
     --region=us-central1 \
     --allow-unauthenticated \
     --port=5000 \
     --set-env-vars="ASPNETCORE_ENVIRONMENT=Production,RunMode=Server,APPLY_MIGRATIONS=true" \
     --set-secrets="ConnectionStrings__DefaultConnection=pdv-db-connstring:latest"
   ```
3. **Configuración del Ciclo de Vida y Escalabilidad (Crítico para Blazor)**:
   - Blazor Server mantiene estado en memoria. **No se recomienda escalar Cloud Run a 0 instancias de forma agresiva** si hay usuarios conectados, ya que la desconexión destruirá el circuito de venta actual del cajero.
   - **Establecer Mínimo de Instancias**: `--min-instances 1` (para evitar el cold-start y pérdida de circuitos).
   - **Session Affinity**: Habilitar la afinidad de sesión por IP o cookies en Cloud Run para garantizar la estabilidad de las conexiones WebSocket:
     ```bash
     gcloud beta run services update pdv-app --session-affinity
     ```

---

## 🔄 3. Estrategias Automatizadas de Respaldo (Backups)

### A. Servidor Principal (PostgreSQL Central)
Implementar un cron job diario para respaldar la base de datos central comprimida y limpiar históricos antiguos.

1. Crear el script en `/usr/local/bin/backup-pdv.sh`:
   ```bash
   #!/bin/bash
   BACKUP_DIR="/var/backups/pdv"
   DATE=$(date +%Y%m%d_%H%M%S)
   DB_NAME="pdv_db"
   DB_USER="pdv_user"

   mkdir -p $BACKUP_DIR
   
   # Realizar dump en caliente con formato comprimido
   PGPASSWORD="TuContrasenaAltamenteSegura123!" pg_dump -h localhost -U $DB_USER -d $DB_NAME -F c -b -v -f "$BACKUP_DIR/pdv_backup_$DATE.dump"

   # Borrar respaldos con más de 30 días de antigüedad
   find $BACKUP_DIR -type f -name "*.dump" -mtime +30 -delete
   ```
2. Dar permisos de ejecución:
   ```bash
   chmod +x /usr/local/bin/backup-pdv.sh
   ```
3. Registrar en el programador de tareas del sistema (`crontab -e`) para ejecutarse todas las noches a las 02:00 AM:
   ```cron
   0 2 * * * /usr/local/bin/backup-pdv.sh >> /var/log/pdv-backup.log 2>&1
   ```

### B. Terminales de Caja Local (SQLite Resiliente)
Dado que las cajas locales pueden perder conexión a internet, los datos en SQLite son invaluables. Para evitar respaldos corruptos causados por bloqueos de escritura activos:

**NO copiar el archivo `pdv.db` directamente.** Ejecutar la utilidad nativa de SQLite usando el comando `VACUUM INTO`. Esto realiza una copia de seguridad transaccional limpia en caliente:

*   **Comando de Respaldo Seguro (PowerShell / Windows Scheduler)**:
    ```powershell
    sqlite3.exe "C:\RutaDeLaApp\pdv.db" "VACUUM INTO 'C:\backups\caja_backup_%date:~10,4%%date:~4,2%%date:~7,2%.db'"
    ```
*   Programar esta tarea diariamente en el **Programador de Tareas de Windows** en las terminales físicas de cajas.

---

## 📊 4. Monitoreo y Logging en Producción

### 1. Logs Estructurados en Consola (Serilog)
La aplicación en modo producción detecta su entorno y formatea automáticamente la consola en **JSON estructurado**. Esto permite a colectores automáticos (como FluentBit, Promtail o CloudWatch Agent) ingerir los metadatos de los logs (nivel de error, request path, id de usuario, etc.) sin necesidad de expresiones regulares complejas.

### 2. Monitoreo de Salud (Health Checks)
Exponemos dos endpoints nativos de salud para sistemas de orquestación (Docker, Kubernetes) y monitores de uptime externos (Uptime Robot, Pingdom):
*   `/health/live`: Devuelve `200 OK` si el proceso del servidor responde.
*   `/health/ready`: Devuelve `200 OK` únicamente si la base de datos (PostgreSQL o SQLite) es accesible y responde consultas con éxito. Si la base de datos se cae, este endpoint devuelve `503 Service Unavailable`, permitiendo a los balanceadores de carga remover la instancia del clúster activo inmediatamente.

### 3. Integración con Prometheus
El contenedor de la aplicación expone métricas de rendimiento en `/metrics`. El archivo `prometheus.yml` en la raíz está listo para scrapear estas métricas cada 5 segundos para recopilar estadísticas de:
*   GC (.NET Garbage Collector) y uso de memoria RAM.
*   Tiempos de respuesta HTTP y conteo de excepciones.
*   Conexiones activas a WebSockets/SignalR de Blazor.

---

## 🔒 5. Aseguramiento y Hardening de Seguridad

Antes de abrir el tráfico del sistema al público, aplique las siguientes medidas estrictas:

1. **Rotación de Secretos**: Jamás almacene contraseñas en código duro (`appsettings.json`). Utilice siempre variables de entorno del sistema o almacenes seguros como **Azure Key Vault** o **AWS Secrets Manager**.
2. **Protección del Puerto de Base de Datos**: Bloquee el puerto `5432` en el firewall del servidor para evitar conexiones externas de internet. Solo permita el rango de IPs de la subred del servidor web.
3. **Configuración del Agente de Hardware**: El agente local `PDV.HardwareAgent` de las cajas debe escuchar exclusivamente en la dirección de bucle invertido local `127.0.0.1:8181` para evitar ataques de inyección de comandos o impresiones no deseadas desde computadoras en la misma red local de la tienda.
4. **Cabeceras de Seguridad TLS**: Asegure que Nginx/Apache fuerce el uso de TLS 1.2/1.3, inhabilite cifrados antiguos y aplique la directiva **HSTS** (Strict-Transport-Security) para blindar la conexión contra ataques Man-In-The-Middle (MITM).

---

## 📋 6. Checklist de Paso a Producción

Siga este checklist interactivo antes del lanzamiento oficial:

- [ ] **Configuración del Entorno**:
  - [ ] Variable `ASPNETCORE_ENVIRONMENT` establecida en `Production`.
  - [ ] Variable `RunMode` configurada en `Server` (para el nodo central) o `Local` (para las cajas).
  - [ ] Secretos y contraseñas seguras inyectadas externamente.
- [ ] **Base de Datos**:
  - [ ] Migraciones pendientes aplicadas con éxito.
  - [ ] Indexación y optimización de base de datos PostgreSQL completada.
  - [ ] Copia de seguridad automática diaria verificada e instalada.
- [ ] **Rendimiento e Infraestructura**:
  - [ ] Soporte de **WebSockets** activado en IIS, Nginx, Apache o Azure App Service.
  - [ ] **Sticky Sessions (Afinidad de sesión)** habilitada en el balanceador de carga si hay más de 1 instancia activa.
  - [ ] Métricas de `/metrics` expuestas de forma segura.
  - [ ] Gzip activado para la compresión de archivos estáticos.
- [ ] **Seguridad**:
  - [ ] Certificado SSL/TLS válido (Let's Encrypt o corporativo) con redirección HTTPS permanente activa.
  - [ ] Cabeceras de seguridad (`X-Frame-Options`, `Content-Security-Policy`, `X-Content-Type-Options`) habilitadas.
  - [ ] El cortafuegos bloquea el puerto público de PostgreSQL.
- [ ] **Edge (Terminales Físicas)**:
  - [ ] `PDV.HardwareAgent` configurado como Servicio de Windows con inicio automático.
  - [ ] Copias transaccionales diarias de `pdv.db` programadas mediante `VACUUM INTO`.
  - [ ] Latencia y sincronización offline-online probadas con éxito.

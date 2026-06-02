# 🚀 Guía Completa de Despliegue - Sistema PDV

Esta guía proporciona instrucciones detalladas paso a paso para configurar, desplegar y mantener el sistema de Punto de Venta (PDV) tanto en ambientes de **Prueba/Desarrollo (Modo Local - SQLite)** como en **Producción (Modo Servidor - PostgreSQL)**.

---

## 🗺️ Topología de Arquitectura

El sistema implementa una **Arquitectura Híbrida** para soportar el funcionamiento local en terminales de cobro (incluso sin internet) y la consolidación centralizada en un servidor en la nube u oficina central.

```mermaid
graph TD
    subgraph Servidor Central (Producción)
        WebUI_Server[PDV.WebUI - Servidor Principal]
        DB_Postgres[(Base de Datos PostgreSQL)]
        WebUI_Server --> DB_Postgres
    end

    subgraph Estación de Caja 1 (Local)
        Browser_1[Navegador Web / UI POS]
        DB_SQLite_1[(SQLite Local - pdv.db)]
        HardwareAgent_1[PDV.HardwareAgent - Servicio de Windows]
        Printer_1[Impresora de Tickets]
        Scale_1[Báscula de Peso]

        Browser_1 -->|Lee/Escribe Venta| DB_SQLite_1
        Browser_1 -->|Solicita Hardware| HardwareAgent_1
        HardwareAgent_1 -->|Puerto Serial COM/USB| Printer_1
        HardwareAgent_1 -->|Puerto Serial COM| Scale_1
    end

    Browser_1 -.->|Sincronización de Datos en Red| WebUI_Server
```

---

## 🛠️ Modos de Ejecución (`RunMode`)

La aplicación Blazor WebUI se puede iniciar en dos modos definidos mediante la variable de entorno `RunMode` o en `appsettings.json`:

| Parámetro | Modo Local (Desarrollo / Cajas) | Modo Servidor (Producción / Admin) |
| :--- | :--- | :--- |
| **`RunMode`** | `Local` | `Server` |
| **Base de Datos** | SQLite (`pdv.db`) | PostgreSQL |
| **Propósito** | Cajas registradoras físicas en sucursales. | Panel administrativo, reportes, inventario global. |
| **Sincronización** | Habilitada (sincroniza ventas al servidor). | Central receptora (recibe y procesa sincronizaciones). |

---

## 💻 1. Ambiente de Desarrollo y Pruebas (SQLite - Modo Local)

Diseñado para programadores, probadores y terminales locales de caja que no requieren configurar bases de datos externas pesadas.

### Requisitos Previos
- **.NET 9.0 SDK** ([Descargar aquí](https://dotnet.microsoft.com/download/dotnet/9.0))
- **SQLite 3** (integrado automáticamente por Entity Framework Core)

### Configuración Rápida en Desarrollo
1. Dirígete a la raíz del proyecto web:
   ```powershell
   cd src/PDV.WebUI
   ```
2. Inicializa el gestor de secretos locales de .NET para no dejar contraseñas expuestas en el código:
   ```powershell
   dotnet user-secrets init
   dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Data Source=pdv.db"
   dotnet user-secrets set "RunMode" "Local"
   ```

3. **Ejecución y Migraciones Automáticas**:
   Para agilizar las pruebas, puedes forzar a la aplicación a aplicar todas las migraciones pendientes de Entity Framework en SQLite de forma automática en cada inicio:
   ```powershell
   $env:APPLY_MIGRATIONS = "true"
   $env:RunMode = "Local"
   dotnet run
   ```

4. Abre tu navegador e ingresa a `http://localhost:5000` o la URL configurada por dotnet.

> [!NOTE]
> En desarrollo, la base de datos se creará automáticamente en la carpeta de ejecución de la app con el nombre `pdv.db`.

---

## 🔄 Configuración de Sincronización (Cajas -> Servidor Principal)

Para que las terminales locales (cajas que operan en `RunMode = Local` con SQLite) puedan reportar sus ventas, descargar catálogos de clientes/productos y sincronizar su estado con el Servidor Central, se debe configurar la URL del Servidor Principal.

El servicio en segundo plano `SyncWorker` lee esta dirección a través de la clave de configuración **`SyncSettings:ServerBaseUrl`**.

### Opción A: A través de `appsettings.json` (En cada Caja Registradora)
Añade el nodo `SyncSettings` en el archivo de configuración `appsettings.json` de la terminal de cobro:

```json
{
  "RunMode": "Local",
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=pdv.db"
  },
  "SyncSettings": {
    "ServerBaseUrl": "https://pdv.tuempresa.com"
  }
}
```

### Opción B: A través de Variables de Entorno (Recomendado en Producción)
Si despliegas la terminal o la inicias automáticamente con scripts o servicios, puedes inyectar la URL usando la variable de entorno formateada de ASP.NET Core:

- **Windows PowerShell**:
  ```powershell
  $env:SyncSettings__ServerBaseUrl = "https://pdv.tuempresa.com"
  ```
- **Linux Terminal / Systemd**:
  ```bash
  export SyncSettings__ServerBaseUrl="https://pdv.tuempresa.com"
  ```

---

## 🏢 2. Ambiente de Producción (PostgreSQL - Modo Servidor)

Destinado al servidor central o local de la sucursal que consolidará la información de inventarios, catálogos, folios y reportes globales.

### Requisitos Previos
- **Servidor Windows** (Windows Server 2019/2022) o **Linux** (Ubuntu 22.04 LTS / Debian 12)
- **.NET 9.0 Runtime & ASP.NET Core Hosting Bundle**
- **Servidor PostgreSQL 14 o superior**
- **Servicio de Reverse Proxy** (Nginx en Linux, o IIS en Windows Server)
- **Certificado SSL (TLS)** (Let's Encrypt o certificado corporativo)

### Paso 1: Configurar la Base de Datos PostgreSQL
1. Crea un usuario y una base de datos exclusiva para el sistema:
   ```sql
   CREATE USER pdv_user WITH PASSWORD 'TuContrasenaAltamenteSegura';
   CREATE DATABASE pdv_db OWNER pdv_user;
   ```
2. Asegúrate de que el archivo `pg_hba.conf` de PostgreSQL permita conexiones TCP/IP desde las IPs del servidor web.

### Paso 2: Publicar la Aplicación
Compila el proyecto web en modo de liberación (`Release`) optimizando el rendimiento y recortando metadatos innecesarios:

- **Publicación genérica (Framework Dependent)**:
  ```powershell
  dotnet publish src/PDV.WebUI/PDV.WebUI.csproj -c Release -o ./publish
  ```

- **Publicación autocontenida para Linux x64** (no requiere instalar .NET en el servidor destino):
  ```powershell
  dotnet publish src/PDV.WebUI/PDV.WebUI.csproj -c Release -r linux-x64 --self-contained true -o ./publish-linux
  ```

- **Publicación autocontenida para Windows Server x64**:
  ```powershell
  dotnet publish src/PDV.WebUI/PDV.WebUI.csproj -c Release -r win-x64 --self-contained true -o ./publish-windows
  ```

---

### Paso 3A: Despliegue en IIS (Windows Server)

1. **Instalar el Hosting Bundle**:
   Descarga e instala el **ASP.NET Core Hosting Bundle** en el servidor de IIS. Esto registra el módulo `AspNetCoreModuleV2`.
2. **Crear el Sitio Web**:
   - Abre el Administrador de IIS.
   - Haz clic derecho en *Sitios* -> *Agregar sitio web*.
   - Define el nombre del sitio, la ruta física apuntando al directorio `./publish-windows` creado anteriormente, y el puerto (ej. 80 u 8080).
3. **Configurar el Pool de Aplicaciones**:
   - Ve a *Pools de aplicaciones*, selecciona el pool de tu sitio.
   - Haz clic en *Configuración básica* y cambia la versión del CLR de .NET a **Sin código administrado** (No Managed Code).
4. **Definir Variables de Entorno en IIS**:
   Abre el archivo `web.config` generado en tu carpeta publicada y asegúrate de inyectar las credenciales mediante variables de entorno para máxima seguridad:
   ```xml
   <configuration>
     <system.webServer>
       <handlers>
         <add name="aspNetCore" path="*" verb="*" modules="AspNetCoreModuleV2" resourceType="Unspecified" />
       </handlers>
       <aspNetCore processPath=".\PDV.WebUI.exe" stdoutLogEnabled="true" stdoutLogFile=".\logs\stdout" hostingModel="inprocess">
         <environmentVariables>
           <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" />
           <environmentVariable name="RunMode" value="Server" />
           <environmentVariable name="ConnectionStrings__DefaultConnection" value="Host=localhost;Database=pdv_db;Username=pdv_user;Password=TuContrasenaAltamenteSegura;" />
           <environmentVariable name="APPLY_MIGRATIONS" value="true" />
         </environmentVariables>
       </aspNetCore>
     </system.webServer>
   </configuration>
   ```

---

### Paso 3B: Despliegue en Linux (Ubuntu/Debian) con Nginx y Systemd

1. Copia tu directorio de publicación `./publish-linux` al servidor (ej. `/var/www/pdv`).
2. Concede permisos de ejecución al ejecutable de la aplicación:
   ```bash
   sudo chmod +x /var/www/pdv/PDV.WebUI
   sudo chown -R www-data:www-data /var/www/pdv
   ```

3. **Crear el servicio Systemd** para mantener la aplicación activa en segundo plano:
   ```bash
   sudo nano /etc/systemd/system/pdv.service
   ```
   Añade el siguiente contenido:
   ```ini
   [Unit]
   Description=Punto de Venta ASP.NET Core Blazor App
   After=network.target

   [Service]
   WorkingDirectory=/var/www/pdv
   ExecStart=/var/www/pdv/PDV.WebUI --urls=http://127.0.0.1:5000
   Restart=always
   # Reiniciar en caso de caída tras 10 segundos
   RestartSec=10
   KillSignal=SIGINT
   SyslogIdentifier=pdv-webui
   User=www-data
   Environment=ASPNETCORE_ENVIRONMENT=Production
   Environment=RunMode=Server
   Environment=ConnectionStrings__DefaultConnection=Host=localhost;Database=pdv_db;Username=pdv_user;Password=TuContrasenaAltamenteSegura;
   Environment=APPLY_MIGRATIONS=true

   [Install]
   WantedBy=multi-user.target
   ```
4. Guarda el archivo, recarga daemon y activa el servicio:
   ```bash
   sudo systemctl daemon-reload
   sudo systemctl enable pdv.service
   sudo systemctl start pdv.service
   sudo systemctl status pdv.service
   ```

5. **Configurar Nginx como Reverse Proxy con SSL**:
   Edita la configuración de Nginx:
   ```bash
   sudo nano /etc/nginx/sites-available/pdv
   ```
   Agrega la configuración del servidor web con soporte Blazor WebSockets:
   ```nginx
   server {
       listen 80;
       server_name pdv.tuempresa.com;
       return 301 https://$host$request_uri;
   }

   server {
       listen 443 ssl http2;
       server_name pdv.tuempresa.com;

       ssl_certificate /etc/letsencrypt/live/pdv.tuempresa.com/fullchain.pem;
       ssl_certificate_key /etc/letsencrypt/live/pdv.tuempresa.com/privkey.pem;
       include /etc/letsencrypt/options-ssl-nginx.conf;
       ssl_dhparam /etc/letsencrypt/ssl-dhparams.pem;

       location / {
           proxy_pass         http://127.0.0.1:5000;
           proxy_http_version 1.1;
           proxy_set_header   Upgrade $http_upgrade;
           proxy_set_header   Connection "upgrade";
           proxy_set_header   Host $host;
           proxy_cache_bypass $http_upgrade;
           proxy_set_header   X-Forwarded-For $proxy_add_x_forwarded_for;
           proxy_set_header   X-Forwarded-Proto $scheme;
       }
   }
   ```
6. Vincula el sitio y recarga Nginx:
   ```bash
   sudo ln -s /etc/nginx/sites-available/pdv /etc/nginx/sites-enabled/
   sudo nginx -t
   sudo systemctl restart nginx
   ```

---

## 🔌 3. Configuración y Despliegue del Agente de Hardware (`PDV.HardwareAgent`)

El **Agente de Hardware** es una aplicación de consola/servicio C# liviana que se instala en el equipo físico de la caja (donde están conectadas la impresora de tickets y la báscula por puerto USB/Serial). Su función es levantar un puerto de escucha HTTP/WebSocket local para recibir solicitudes de impresión o pesaje directas del navegador de Blazor.

### Instrucciones de Instalación
1. Compila el agente para el sistema operativo de la máquina de caja (usualmente Windows de 64 bits):
   ```powershell
   dotnet publish src/PDV.HardwareAgent/PDV.HardwareAgent.csproj -c Release -r win-x64 --self-contained true -o ./publish-agent
   ```
2. Copia la carpeta `publish-agent` en la computadora de la caja (ej. `C:\PDVHardwareAgent`).
3. Modifica el archivo `appsettings.json` local en la caja para mapear los puertos físicos:
   ```json
   {
     "HardwareSettings": {
       "TicketPrinterName": "EPSON TM-T88VI",
       "ScalePort": "COM3",
       "ScaleBaudRate": 9600,
       "WebSocketPort": 8181
     }
   }
   ```
4. **Ejecutar como Servicio de Windows (Altamente Recomendado)**:
   Puedes registrar el agente para que se ejecute en segundo plano sin mostrar ventanas y arranque con el equipo:
   ```powershell
   sc.exe create "PDVHardwareAgent" binPath= "C:\PDVHardwareAgent\PDV.HardwareAgent.exe --service" start= auto
   sc.exe start "PDVHardwareAgent"
   ```

---

## 💾 4. Estrategia de Respaldos (Backups)

### A. Respaldos en Producción (PostgreSQL)
Se debe calendarizar una tarea en segundo plano (cron job en Linux o Programador de tareas en Windows Server) para respaldar la base de datos de manera diaria con compresión:

- **Linux (Script de Respaldo Diario - `/usr/local/bin/backup-pdv.sh`)**:
  ```bash
  #!/bin/bash
  BACKUP_DIR="/backups/pdv"
  DATE=$(date +%Y%m%d%H%M%S)
  DB_NAME="pdv_db"
  DB_USER="pdv_user"

  mkdir -p $BACKUP_DIR
  pg_dump -U $DB_USER -h localhost -d $DB_NAME -F c -b -v -f "$BACKUP_DIR/pdv_backup_$DATE.dump"

  # Eliminar respaldos de más de 30 días de antigüedad
  find $BACKUP_DIR -type f -name "*.dump" -mtime +30 -delete
  ```
  Programa este script en crontab para ejecutarse a las 2:00 AM todos los días:
  ```bash
  0 2 * * * /usr/local/bin/backup-pdv.sh
  ```

---

### B. Respaldos de Cajas Locales (SQLite)
Dado que las cajas guardan ventas localmente para resiliencia offline, es crítico asegurar respaldos del archivo `pdv.db`. 

> [!WARNING]
> No copies directamente el archivo `pdv.db` mientras la aplicación de caja esté en uso, ya que el archivo podría estar bloqueado en modo WAL (Write-Ahead Logging) o escribiendo activamente, lo que provocaría un respaldo corrupto.

- **Método Seguro de Copia Caliente (Hot Backup)**:
  Ejecuta la utilidad de línea de comandos de SQLite usando `VACUUM INTO` para duplicar la base de datos de forma limpia y transaccional:
  ```powershell
  sqlite3.exe C:\RutaDeLaApp\pdv.db "VACUUM INTO 'C:\backups\caja_backup.db'"
  ```

---

## 🔒 5. Aseguramiento y Hardening de Seguridad

1. **Evitar Credenciales Fijas (Hardcoded)**: Jamás dejes contraseñas reales de PostgreSQL en `appsettings.Production.json`. Utiliza siempre **Variables de Entorno** del sistema operativo, secretos de contenedor de Docker/Kubernetes o proveedores como Azure Key Vault.
2. **Cifrado de Comunicaciones**: Asegúrate de forzar HTTPS (`UseHttpsRedirection`) en Blazor y que la comunicación con las cajas use TLS 1.3.
3. **Limitar Accesos de Red a PostgreSQL**: Configura el cortafuegos (firewall) de tu base de datos para aceptar tráfico de red únicamente proveniente de las direcciones IP de tus servidores web Blazor, bloqueando el puerto `5432` al internet abierto.
4. **Turnos y Permisos**: Garantiza que el token JWT o la cookie de autenticación de Identity expire de forma segura tras inactividad del cajero y exija el ingreso de turnos aperturados antes de activar las funciones críticas del POS.

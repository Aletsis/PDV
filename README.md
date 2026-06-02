# PDV - Sistema de Punto de Venta Inteligente

> **AVISO DE CONFIDENCIALIDAD Y DERECHOS RESERVADOS**  
> Este repositorio y su contenido son **propiedad exclusiva y privada**. Está estrictamente prohibida la copia, distribución, modificación o uso no autorizado de este software para fines externos.  
> **Copyright © 2026. Todos los derechos reservados.**

---

PDV es una solución moderna de Punto de Venta (POS) diseñada bajo una arquitectura robusta, distribuida e híbrida. Permite el funcionamiento autónomo en terminales de caja locales mediante almacenamiento SQLite con sincronización automática en la nube o en servidores centrales PostgreSQL, garantizando la continuidad de la operación incluso sin conexión a internet.

---

## 🚀 Características Claves

* **Arquitectura Limpia (Clean Architecture):** Separación estricta de responsabilidades que garantiza la mantenibilidad, escalabilidad y testabilidad de todo el código fuente.
* **Resiliencia Local (Modo Híbrido):** Operación en cajas locales sin dependencia de internet, con bases de datos locales (`SQLite`) que se sincronizan de manera bidireccional y diferida con el servidor central (`PostgreSQL`).
* **Descubrimiento Jerárquico de Servidores:** Estrategia de conectividad de dos niveles que utiliza nombres de host de DNS Canónico (Nivel 1) y autodescubrimiento mDNS/SSDP en red local (Nivel 2) para conectar terminales de venta automáticamente sin configuración manual de IPs.
* **Agente de Hardware Independiente:** Componente ligero autocontenido para Windows y Linux (`PDV.HardwareAgent`) que maneja la interacción con periféricos físicos de punto de venta (impresoras térmicas ESC/POS, cajón de dinero, básculas y lectores de códigos).
* **Contenerización Completa:** Preparado para entornos de producción y staging mediante configuraciones optimizadas de Docker y Docker Compose.
* **Pipeline de CI/CD Industrial:** Flujo de integración continua automatizado mediante GitHub Actions que valida el linter, compila la solución, ejecuta pruebas unitarias xUnit, genera binarios del Agente de Hardware y publica imágenes optimizadas en GitHub Container Registry (GHCR).

---

## 🛠️ Estructura de la Solución

El proyecto está organizado en las siguientes capas dentro del directorio `src/`:

* **`PDV.Domain`:** Contiene las entidades principales de negocio, reglas de dominio, excepciones y lógica pura del sistema de ventas.
* **`PDV.Application`:** Implementa los casos de uso, interfaces del sistema, validaciones y la lógica de orquestación de datos de ventas e inventario.
* **`PDV.Infrastructure.Server`:** Implementación de persistencia pesada (PostgreSQL) y servicios diseñados para el servidor en la nube.
* **`PDV.Infrastructure.Local`:** Implementación ligera de base de datos SQLite para las cajas de ventas locales y lógica de base de datos embebida.
* **`PDV.HardwareAgent`:** Servicio nativo autocontenido multiplataforma para interactuar con periféricos locales.
* **`PDV.WebUI`:** Capa de presentación web que expone tanto la interfaz de administración y el punto de venta interactivo como los servicios de API.

---

## 📦 Requisitos Previos

Antes de ejecutar o desarrollar en el proyecto, asegúrese de tener instalado:

1. **.NET SDK 9.0** (o superior)
2. **Docker** y **Docker Compose**
3. **Git** (para el control de versiones)

---

## 💻 Inicio Rápido

### 1. Clonar el repositorio (privado)
```bash
git clone https://github.com/Aletsis/PDV.git
cd PDV
```

### 2. Restaurar dependencias y compilar la solución
```bash
dotnet restore PDV.sln
dotnet build PDV.sln -c Release
```

### 3. Ejecutar las pruebas automatizadas
```bash
dotnet test tests/PDV.Tests/PDV.Tests.csproj
```

### 4. Iniciar el servidor local de desarrollo
```bash
dotnet run --project src/PDV.WebUI/PDV.WebUI.csproj
```
El sistema estará disponible en su navegador web en `http://localhost:5000` o la URL configurada en su entorno.

---

## 🐋 Despliegue con Docker

El sistema se puede inicializar completamente con sus servicios auxiliares (como Prometheus para monitoreo y Nginx como proxy inverso) utilizando Docker Compose:

```bash
docker-compose up -d
```

Para obtener detalles específicos sobre arquitecturas de red, proxies, certificados SSL y balanceo de carga, consulte la documentación interna de DevOps:
* **[Guía de DevOps y Monitoreo](DEVOPS.md)**
* **[Guía de Despliegue y Arquitectura](DEPLOYMENT.md)**

---

## 🔒 Seguridad y Configuración de Secretos

> [!CAUTION]
> **Nunca guarde contraseñas, secretos de API o cadenas de conexión de producción en archivos de configuración de Git.**
> Utilice variables de entorno del sistema o el gestor de secretos de .NET para almacenar credenciales en entornos locales. El archivo `appsettings.Production.json` cuenta con placeholders seguros para configurar en el servidor de producción.

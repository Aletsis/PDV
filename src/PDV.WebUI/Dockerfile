# ==========================================
# Etapa 1: Compilación (Build Stage)
# ==========================================
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app

# Copiar archivos de solución y proyectos para restaurar dependencias de forma eficiente (aprovechando caché de Docker)
COPY PDV.sln ./
COPY src/PDV.Domain/PDV.Domain.csproj src/PDV.Domain/
COPY src/PDV.Application/PDV.Application.csproj src/PDV.Application/
COPY src/PDV.Infrastructure/PDV.Infrastructure.csproj src/PDV.Infrastructure/
COPY src/PDV.Infrastructure.Local/PDV.Infrastructure.Local.csproj src/PDV.Infrastructure.Local/
COPY src/PDV.Infrastructure.Server/PDV.Infrastructure.Server.csproj src/PDV.Infrastructure.Server/
COPY src/PDV.WebUI/PDV.WebUI.csproj src/PDV.WebUI/
COPY src/PDV.HardwareAgent/PDV.HardwareAgent.csproj src/PDV.HardwareAgent/
COPY tests/PDV.Tests/PDV.Tests.csproj tests/PDV.Tests/

# Restaurar paquetes NuGet
RUN dotnet restore

# Copiar el código fuente completo de la aplicación
COPY src/ src/
COPY tests/ tests/

# Compilar y publicar la interfaz Web en modo Release optimizado
WORKDIR /app/src/PDV.WebUI
RUN dotnet publish -c Release -o /app/publish --no-restore

# ==========================================
# Etapa 2: Imagen de Ejecución de Producción
# ==========================================
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS final
WORKDIR /app

# Instalar dependencias requeridas para soporte de ICU (Globalización) en Alpine
RUN apk add --no-cache icu-libs
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

# Exponer el puerto estándar HTTP (5000) y HTTPS (5001) para ASP.NET Core
EXPOSE 5000
EXPOSE 5001

# Definir variables de entorno predeterminadas para ejecución en producción
ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Production
ENV RunMode=Server

# Copiar archivos publicados desde la etapa de compilación
COPY --from=build /app/publish .

# Crear carpeta de logs y asegurar permisos adecuados (Seguridad Hardening)
RUN mkdir -p Logs && chmod -R 755 Logs

# Crear un usuario no root para ejecutar la aplicación (Mitigación de riesgos de seguridad)
RUN addgroup -S pdvgroup && adduser -S pdvuser -G pdvgroup
RUN chown -R pdvuser:pdvgroup /app
USER pdvuser

ENTRYPOINT ["dotnet", "PDV.WebUI.dll"]

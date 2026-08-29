#!/bin/bash
# =================================================================
# Script de Actualización Rápida de Contenedores PDV
# =================================================================

set -e

TARGET=${1:-all}

echo "🔨 Reconstruyendo imagen pdv-webui:latest..."
docker build -t pdv-webui:latest -f src/PDV.WebUI/Dockerfile .

# Asegurar que la red pdv-net exista
if ! docker network ls | grep -q "pdv-net"; then
    echo "🌐 Creando red pdv-net..."
    docker network create pdv-net
fi

# Conectar dev-postgres a la red si no está conectado
if docker ps --format '{{.Names}}' | grep -q "^dev-postgres$"; then
    if ! docker inspect dev-postgres --format '{{json .NetworkSettings.Networks}}' | grep -q "pdv-net"; then
        echo "🔗 Conectando dev-postgres a la red pdv-net..."
        docker network connect pdv-net dev-postgres 2>/dev/null || true
    fi
fi

if [ "$TARGET" = "server" ] || [ "$TARGET" = "all" ]; then
    echo "🔄 Actualizando pdv-server..."
    docker rm -f pdv-server 2>/dev/null || true
    docker run -d \
      --name pdv-server \
      --restart always \
      --network pdv-net \
      -p 5000:5000 \
      -v pdv-server-logs:/app/Logs \
      -e ASPNETCORE_ENVIRONMENT="Production" \
      -e RunMode="Server" \
      -e ConnectionStrings__DefaultConnection="Host=dev-postgres;Port=5432;Database=pdv_db;Username=pdv_user;Password=password_pdv;" \
      -e APPLY_MIGRATIONS="true" \
      -e SyncSettings__SyncApiKey="ClaveSecretaCompartida2026!" \
      pdv-webui:latest
    echo "✅ pdv-server actualizado y escuchando en http://localhost:5000"
fi

if [ "$TARGET" = "client" ] || [ "$TARGET" = "all" ]; then
    echo "🔄 Actualizando pdv-client..."
    docker rm -f pdv-client 2>/dev/null || true
    docker run -d \
      --name pdv-client \
      --restart always \
      --network pdv-net \
      -p 5001:5000 \
      -v pdv-client-db:/app/Logs \
      -e ASPNETCORE_ENVIRONMENT="Production" \
      -e RunMode="Local" \
      -e ConnectionStrings__DefaultConnection="Data Source=/app/Logs/pdv.db" \
      -e SyncSettings__ServerBaseUrl="http://pdv-server:5000" \
      -e SyncSettings__SyncApiKey="ClaveSecretaCompartida2026!" \
      -e APPLY_MIGRATIONS="true" \
      pdv-webui:latest
    echo "✅ pdv-client actualizado y escuchando en http://localhost:5001"
fi

echo "🎉 Proceso de actualización finalizado con éxito."

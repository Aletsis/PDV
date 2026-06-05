# Script de Automatización de Compilación para el Instalador del Cliente PDV
# Ejecuta la publicación autocontenida de WebUI y HardwareAgent y compila el instalador con Inno Setup.

$ErrorActionPreference = "Stop"

Write-Host "=== Iniciando automatizacion del instalador ===" -ForegroundColor Cyan

# 1. Rutas
$PdvRoot = Resolve-Path ".."
$PublishWebDir = Join-Path $PdvRoot "publish-windows"
$PublishAgentDir = Join-Path $PdvRoot "publish-agent"
$InstallerOutputDir = Join-Path $PdvRoot "installer\Output"

# 2. Limpieza
Write-Host "Limpiando directorios de publicacion anteriores..." -ForegroundColor Yellow
if (Test-Path $PublishWebDir) { Remove-Item -Recurse -Force $PublishWebDir }
if (Test-Path $PublishAgentDir) { Remove-Item -Recurse -Force $PublishAgentDir }
if (Test-Path $InstallerOutputDir) { Remove-Item -Recurse -Force $InstallerOutputDir }

# 3. Compilación y Publicación de WebUI (Autocontenida win-x64)
Write-Host "Publicando PDV.WebUI (Autocontenido win-x64)..." -ForegroundColor Yellow
dotnet publish (Join-Path $PdvRoot "src\PDV.WebUI\PDV.WebUI.csproj") -c Release -r win-x64 --self-contained true -o $PublishWebDir

# 4. Compilación y Publicación de HardwareAgent (Autocontenida win-x64)
Write-Host "Publicando PDV.HardwareAgent (Autocontenido win-x64)..." -ForegroundColor Yellow
dotnet publish (Join-Path $PdvRoot "src\PDV.HardwareAgent\PDV.HardwareAgent.csproj") -c Release -r win-x64 --self-contained true -o $PublishAgentDir

# 5. Compilación del Instalador usando Inno Setup
Write-Host "Buscando compilador de Inno Setup..." -ForegroundColor Yellow
$IsccPath = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"

if (-not (Test-Path $IsccPath)) {
    Write-Error "No se encontro el compilador de Inno Setup en la ruta especificada: $IsccPath"
}

Write-Host "Compilando instalador final con Inno Setup..." -ForegroundColor Yellow
& $IsccPath "setup.iss"

Write-Host "=== ¡Instalador compilado con exito! ===" -ForegroundColor Green
$SetupExe = Join-Path $InstallerOutputDir "PDV_Client_Setup.exe"
Write-Host "Instalador ubicado en: $SetupExe" -ForegroundColor Green

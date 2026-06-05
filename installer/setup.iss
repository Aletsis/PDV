; Script de Inno Setup para instalar el Cliente de Caja Registradora (PDV)
; Soportes incluidos: Servicios de Windows autocontenidos, base de datos SQLite y variables de entorno del servidor.

[Setup]
AppName=PDV Cliente
AppVersion=1.0.0
AppPublisher=Aletsis
AppPublisherURL=https://aletsis.com
DefaultDirName=C:\PDV
DefaultGroupName=PDV Cliente
DisableProgramGroupPage=yes
OutputDir=Output
OutputBaseFilename=PDV_Client_Setup
Compression=lzma2
SolidCompression=yes
PrivilegesRequired=admin
UninstallDisplayIcon={app}\WebUI\PDV.WebUI.exe

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Dirs]
Name: "{app}\Database"; Permissions: users-modify
Name: "{app}\WebUI"
Name: "{app}\HardwareAgent"

[Files]
; Copiar los archivos publicados de la interfaz web
Source: "..\publish-windows\*"; DestDir: "{app}\WebUI"; Flags: recursesubdirs createallsubdirs ignoreversion

; Copiar los archivos publicados del agente de hardware
Source: "..\publish-agent\*"; DestDir: "{app}\HardwareAgent"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{userdesktop}\PDV Caja Registradora"; Filename: "http://localhost:5000"; Tasks: desktopicon;

[Run]
; 1. Dar permisos de escritura generales al directorio principal de la aplicación
Filename: "icacls.exe"; Parameters: """{app}"" /grant Users:(OI)(CI)M /T"; Flags: runhidden; StatusMsg: "Configurando permisos de archivos y base de datos..."

; 2. Inicializar la base de datos local SQLite y aplicar migraciones de forma directa sin usar PowerShell
Filename: "{app}\WebUI\PDV.WebUI.exe"; Parameters: "--apply-migrations-only ""Data Source={app}\Database\pdv.db"""; Flags: runhidden; StatusMsg: "Inicializando y aplicando migraciones a la base de datos local..."

; 3. Registrar el servicio de WebUI usando sc.exe
Filename: "sc.exe"; Parameters: "create PDVWebUI binPath= ""{app}\WebUI\PDV.WebUI.exe"" start= auto DisplayName= ""PDV Web UI Service"""; Flags: runhidden; StatusMsg: "Registrando servicio de la Interfaz Web (WebUI)..."

; 4. Agregar descripcion al servicio de WebUI
Filename: "sc.exe"; Parameters: "description PDVWebUI ""Servicio que ejecuta la interfaz web del Punto de Venta local en http://localhost:5000."""; Flags: runhidden

; 5. Registrar el servicio de HardwareAgent
Filename: "sc.exe"; Parameters: "create PDVHardwareAgent binPath= ""{app}\HardwareAgent\PDV.HardwareAgent.exe --service"" start= auto DisplayName= ""PDV Hardware Agent Service"""; Flags: runhidden; StatusMsg: "Registrando servicio de Agente de Hardware..."

; 6. Agregar descripcion al servicio de HardwareAgent
Filename: "sc.exe"; Parameters: "description PDVHardwareAgent ""Servicio que comunica la interfaz web con los dispositivos de hardware locales (impresora, bascula, cajon) en http://localhost:9000."""; Flags: runhidden

[UninstallRun]
; Detener y remover servicios durante la desinstalacion
Filename: "sc.exe"; Parameters: "stop PDVWebUI"; Flags: runhidden; RunOnceId: "StopWebUI"
Filename: "sc.exe"; Parameters: "delete PDVWebUI"; Flags: runhidden; RunOnceId: "DeleteWebUI"
Filename: "sc.exe"; Parameters: "stop PDVHardwareAgent"; Flags: runhidden; RunOnceId: "StopAgent"
Filename: "sc.exe"; Parameters: "delete PDVHardwareAgent"; Flags: runhidden; RunOnceId: "DeleteAgent"

[Code]
var
  ServerUrlPage: TInputQueryWizardPage;

procedure InitializeWizard;
begin
  // Crear una pagina personalizada para configurar la URL del servidor
  ServerUrlPage := CreateInputQueryPage(wpSelectDir,
    'Configuracion de Sincronizacion',
    '¿Cual es la direccion del servidor central?',
    'Por favor, ingresa la URL completa del servidor principal de PDV para la sincronizacion de datos (ej. https://pdv.miempresa.com).' + #13#10 +
    'Si deseas configurar esto mas tarde, deja este campo vacio y presiona Siguiente.');
  ServerUrlPage.Add('URL del Servidor Principal:', False);
  ServerUrlPage.Values[0] := '';
end;

function GetServerUrl(Param: String): String;
begin
  Result := Trim(ServerUrlPage.Values[0]);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  // Detener y eliminar los servicios existentes antes de comenzar a instalar los nuevos archivos
  // Esto evita bloqueos de archivos en disco y reinicios pendientes de Windows.
  Exec('sc.exe', 'stop PDVWebUI', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec('sc.exe', 'stop PDVHardwareAgent', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Sleep(1000);
  
  Exec('sc.exe', 'delete PDVWebUI', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec('sc.exe', 'delete PDVHardwareAgent', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Sleep(1000);
  
  Result := '';
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  EnvData: String;
  ResultCode: Integer;
begin
  // Una vez creados los servicios en la fase de instalacion, escribimos nativamente las variables de entorno 
  // en el Registro de Windows y procedemos a arrancar los servicios con la configuracion cargada.
  if CurStep = ssPostInstall then
  begin
    EnvData := 'RunMode=Local' + #0 + 
               'ConnectionStrings__DefaultConnection=Data Source=' + ExpandConstant('{app}') + '\Database\pdv.db' + #0 + 
               'SyncSettings__ServerBaseUrl=' + GetServerUrl('');
               
    if not RegWriteMultiStringValue(HKEY_LOCAL_MACHINE, 'SYSTEM\CurrentControlSet\Services\PDVWebUI', 'Environment', EnvData) then
    begin
      Log('Error escribiendo variables de entorno en el registro.');
    end;
    
    // Iniciar los servicios con las variables de entorno ya cargadas
    Exec('sc.exe', 'start PDVWebUI', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Exec('sc.exe', 'start PDVHardwareAgent', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  end;
end;

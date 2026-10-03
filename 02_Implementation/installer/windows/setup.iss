; =============================================================================
; EagleEye Windows Installer (service + tray client)
; Tool:   Inno Setup 6.x
; Build:  pwsh 02_Implementation/scripts/package-windows.ps1  (Windows dev machine)
; Output: 03_Delivery/windows/EagleEye-Setup-{version}.exe
;
; package-windows.ps1 passes /DAppVersion and /DPublishDir. The defaults below are
; for compiling this script by hand after a manual publish.
; =============================================================================

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef PublishDir
  #define PublishDir "..\..\artifacts\publish"
#endif

#define AppName            "EagleEye"
#define AppPublisher       "adlemich"
#define AppURL             "https://github.com/adlemich/EagleEye"
#define ServiceName        "EagleEyeService"
#define ServiceDisplayName "EagleEye Service"
#define ServiceExe         "EagleEye.Service.exe"
#define TrayExe            "EagleEye.TrayClient.exe"
#define TrayRunValue       "EagleEyeTrayClient"

[Setup]
AppId={{EA07FFCA-77A6-417F-8430-B3D31ADE2425}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}
AppUpdatesURL={#AppURL}
VersionInfoVersion={#AppVersion}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir=..\..\..\03_Delivery\windows
OutputBaseFilename=EagleEye-Setup-{#AppVersion}
SetupIconFile=..\..\src\EagleEye.TrayClient\UI\Resources\eagleeye.ico
UninstallDisplayIcon={app}\TrayClient\{#TrayExe}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.22000
; The service and tray client are stopped explicitly in [Code] (PrepareToInstall).
CloseApplications=no

[Languages]
Name: "german"; MessagesFile: "compiler:Languages\German.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
; Self-contained publish output: no separate .NET runtime installation needed.
Source: "{#PublishDir}\Service\*"; DestDir: "{app}\Service"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#PublishDir}\TrayClient\*"; DestDir: "{app}\TrayClient"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\EagleEye Tray"; Filename: "{app}\TrayClient\{#TrayExe}"
Name: "{group}\Uninstall EagleEye"; Filename: "{uninstallexe}"

[Registry]
; Tray client auto-start for every user session (FR-TRAY-050, AC-5).
Root: HKLM; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#TrayRunValue}"; ValueData: """{app}\TrayClient\{#TrayExe}"""; Flags: uninsdeletevalue

[Run]
Filename: "{app}\TrayClient\{#TrayExe}"; Description: "Start the EagleEye tray icon now"; Flags: postinstall nowait skipifsilent runasoriginaluser unchecked

[Code]
const
  SvcName = '{#ServiceName}';

function RunHidden(const FileName, Params: String): Integer;
var
  ResultCode: Integer;
begin
  if not Exec(FileName, Params, '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    ResultCode := -1;
  Result := ResultCode;
end;

function ScExe(): String;
begin
  Result := ExpandConstant('{sys}\sc.exe');
end;

function NetExe(): String;
begin
  Result := ExpandConstant('{sys}\net.exe');
end;

{ Stops the service (net stop waits until it has stopped) and all tray client
  instances in all sessions, so that their files can be replaced or removed. }
procedure StopServiceAndTray();
begin
  RunHidden(NetExe(), 'stop ' + SvcName);
  RunHidden(ExpandConstant('{sys}\taskkill.exe'), '/F /IM {#TrayExe}');
end;

{ Registers the service as LocalSystem with automatic start and restart-on-failure,
  then starts it. On upgrade the existing registration is updated instead. }
procedure InstallService();
var
  BinPath: String;
begin
  { The embedded quotes keep the path with spaces quoted in the service registry entry. }
  BinPath := '"\"' + ExpandConstant('{app}\Service\{#ServiceExe}') + '\""';

  if RunHidden(ScExe(), 'create ' + SvcName + ' binPath= ' + BinPath +
       ' start= auto obj= LocalSystem DisplayName= "{#ServiceDisplayName}"') <> 0 then
    RunHidden(ScExe(), 'config ' + SvcName + ' binPath= ' + BinPath + ' start= auto obj= LocalSystem');

  RunHidden(ScExe(), 'description ' + SvcName + ' "EagleEye parental control service"');
  RunHidden(ScExe(), 'failure ' + SvcName + ' reset= 86400 actions= restart/5000/restart/5000/restart/5000');
  RunHidden(NetExe(), 'start ' + SvcName);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  StopServiceAndTray();
  Result := '';
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
    InstallService();
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
  begin
    StopServiceAndTray();
    RunHidden(ScExe(), 'delete ' + SvcName);
  end;
end;

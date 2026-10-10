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
#define AppPublisher       "Michael Adler"
#define AppURL             "https://github.com/adlemich/EagleEye"
#define ServiceName        "EagleEyeService"
#define ServiceDisplayName "EagleEye Service"
#define ServiceExe         "EagleEye.Service.exe"
#define TrayExe            "EagleEye.TrayClient.exe"
#define TrayRunValue       "EagleEyeTrayClient"
#define DataFolder         "EagleEye"
#define FirewallRule       "EagleEye Service (Parent apps)"
#define ParentPort         "5443"

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
  instances in all sessions, so that their files can be replaced or removed.
  Session agents (US-004, ADR-011) are EagleEye.Service.exe processes in the users'
  sessions; they exit when the service closes their input, and any agent still
  exiting is ended here (install, upgrade and uninstall). }
procedure StopServiceAndTray();
begin
  RunHidden(NetExe(), 'stop ' + SvcName);
  RunHidden(ExpandConstant('{sys}\taskkill.exe'), '/F /IM {#ServiceExe}');
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

function IcaclsExe(): String;
begin
  Result := ExpandConstant('{sys}\icacls.exe');
end;

function NetshExe(): String;
begin
  Result := ExpandConstant('{sys}\netsh.exe');
end;

{ Creates %ProgramData%\EagleEye, certs\ and logs\ and restricts them (ADR-008 section 7,
  US-003 AC-14: the service log folder is readable by SYSTEM and Administrators only).
  Well-known SIDs instead of names, so that localized (e.g. German) Windows works:
  S-1-5-18 = SYSTEM, S-1-5-32-544 = Administrators, S-1-5-32-545 = Users.
  Idempotent: /inheritance:r and /grant:r replace earlier entries on every install, so an
  upgrade from 0.2.0 gets logs\ too. The service applies the same logs\ ACL on every start. }
procedure ConfigureDataFolder();
var
  DataDir, CertDir, LogDir: String;
begin
  DataDir := ExpandConstant('{commonappdata}\{#DataFolder}');
  CertDir := DataDir + '\certs';
  LogDir := DataDir + '\logs';
  ForceDirectories(CertDir);
  ForceDirectories(LogDir);

  RunHidden(IcaclsExe(), '"' + DataDir + '" /inheritance:r /grant:r *S-1-5-18:(OI)(CI)F *S-1-5-32-544:(OI)(CI)F *S-1-5-32-545:(OI)(CI)RX');
  RunHidden(IcaclsExe(), '"' + CertDir + '" /inheritance:r /grant:r *S-1-5-18:(OI)(CI)F *S-1-5-32-544:(OI)(CI)F');
  RunHidden(IcaclsExe(), '"' + LogDir + '" /inheritance:r /grant:r *S-1-5-18:(OI)(CI)F *S-1-5-32-544:(OI)(CI)F');
end;

procedure RemoveFirewallRule();
begin
  RunHidden(NetshExe(), 'advfirewall firewall delete rule name="{#FirewallRule}"');
end;

{ Inbound rule for the parent endpoint (ADR-008 section 7): TCP 5443, the service program
  only, local subnet only, all profiles (home networks are often classified as Public). }
procedure ConfigureFirewall();
begin
  RemoveFirewallRule();
  RunHidden(NetshExe(), 'advfirewall firewall add rule name="{#FirewallRule}" dir=in action=allow protocol=TCP localport={#ParentPort}' +
    ' program="' + ExpandConstant('{app}\Service\{#ServiceExe}') + '" remoteip=localsubnet profile=any');
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  StopServiceAndTray();
  Result := '';
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    ConfigureDataFolder();
    ConfigureFirewall();
    InstallService();
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
  begin
    StopServiceAndTray();
    RunHidden(ScExe(), 'delete ' + SvcName);
    { %ProgramData%\EagleEye (certificate, pairings) stays, so that a reinstall keeps the pairings. }
    RemoveFirewallRule();
  end;
end;

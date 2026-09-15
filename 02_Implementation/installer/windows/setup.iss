; =============================================================================
; EagleEye Windows Installer
; Tool: Inno Setup 6.x
; Output: 03_Delivery/windows/EagleEye-Setup-{version}.exe
;
; NOTE: This script is a stub. Fill in file paths and service install/uninstall
; commands once DEV has published the Windows executables.
; =============================================================================

#define AppName        "EagleEye"
#define AppVersion     "0.1.0"
#define AppPublisher   "adlemich"
#define AppURL         "https://github.com/adlemich/EagleEye"
#define AppServiceName "EagleEyeService"
#define ServiceExe     "EagleEye.Service.exe"
#define TrayExe        "EagleEye.TrayClient.exe"

[Setup]
; TODO: Replace with a real GUID before first release (generate with guidgen or uuidgen)
AppId={{00000000-0000-0000-0000-000000000000}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}
AppUpdatesURL={#AppURL}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
AllowNoIcons=yes
OutputDir=..\..\..\03_Delivery\windows
OutputBaseFilename=EagleEye-Setup-{#AppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.22000

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "autostart_tray"; Description: "Start EagleEye tray icon automatically when the kid logs in"; GroupDescription: "Additional options:"; Flags: checked

[Files]
; TODO: Update paths once DEV has published the executables
; Source: "..\..\..\02_Implementation\src\EagleEye.Service\bin\Release\net10.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Check: IsWin64
; Source: "..\..\..\02_Implementation\src\EagleEye.TrayClient\bin\Release\net10.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Check: IsWin64

[Icons]
Name: "{group}\EagleEye"; Filename: "{app}\{#TrayExe}"
Name: "{group}\Uninstall EagleEye"; Filename: "{uninstallexe}"

[Registry]
; Auto-start TrayClient for all users when they log in (placed in HKLM Run)
Root: HKLM; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "EagleEyeTray"; ValueData: """{app}\{#TrayExe}"""; Flags: uninsdeletevalue; Tasks: autostart_tray

[Run]
; TODO: Install the Windows service after files are copied
; Filename: "{app}\{#ServiceExe}"; Parameters: "install"; Flags: runhidden waituntilterminated; StatusMsg: "Installing EagleEye Service..."
; Start the service immediately
; Filename: "sc.exe"; Parameters: "start {#AppServiceName}"; Flags: runhidden waituntilterminated; StatusMsg: "Starting EagleEye Service..."

[UninstallRun]
; TODO: Stop and uninstall the Windows service on uninstall
; Filename: "sc.exe"; Parameters: "stop {#AppServiceName}"; Flags: runhidden waituntilterminated
; Filename: "{app}\{#ServiceExe}"; Parameters: "uninstall"; Flags: runhidden waituntilterminated

[Code]
// TODO: Add custom wizard pages if needed (e.g., for initial configuration)

; =============================================================================
; EagleEye Parent App - Windows installer (per user, no admin rights), ADR-009
; Tool:   Inno Setup 6.x
; Build:  pwsh 02_Implementation/scripts/package-windows.ps1 -Target ParentApp  (Windows dev machine)
; Output: 03_Delivery/windows/EagleEye-ParentApp-Setup-{version}.exe
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

#define AppName       "EagleEye Parent App"
#define AppPublisher  "Michael Adler"
#define AppURL        "https://github.com/adlemich/EagleEye"
#define AppExe        "EagleEye.ParentApp.exe"
#define AppDataFolder "EagleEye"

[Setup]
; Own AppId: independent of the service installer (US-002 AC-1).
AppId={{33D2D602-7F18-4B8F-BC58-68CC173169D0}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}
AppUpdatesURL={#AppURL}
VersionInfoVersion={#AppVersion}
VersionInfoCompany={#AppPublisher}
VersionInfoProductName={#AppName}
; Per-user installation: {autopf} resolves to %LocalAppData%\Programs without admin rights.
PrivilegesRequired=lowest
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir=..\..\..\03_Delivery\windows
OutputBaseFilename=EagleEye-ParentApp-Setup-{#AppVersion}
SetupIconFile=..\..\src\EagleEye.ParentApp\Resources\AppIcon\eagleeye.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; Windows 11 (the app's API minimum is lower, see EagleEye.ParentApp.csproj).
MinVersion=10.0.22000
; Repair/update while the app runs: close it via the Restart Manager.
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "german"; MessagesFile: "compiler:Languages\German.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Self-contained publish output (.NET runtime and Windows App SDK included, US-002 AC-2).
Source: "{#PublishDir}\ParentApp\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Complete removal (US-002 AC-5): the app data of this Windows user (database with the pairing
; and settings) and anything left in the program folder. Repair/update keeps the app data (AC-4).
Type: filesandordirs; Name: "{localappdata}\{#AppDataFolder}"
Type: filesandordirs; Name: "{app}"

[Code]
{ The app must not hold its files or database open while they are removed. taskkill without
  admin rights ends only the processes of the current user. }
function InitializeUninstall(): Boolean;
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM {#AppExe}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Result := True;
end;

# EagleEye.ParentApp

Cross-platform parent application built with .NET MAUI. One codebase targets Android and iOS (primary production platforms), Windows (initial testing vehicle, own per-user installer) and macOS.

**Build hosts (ADR-007)**: Windows and Android targets are built on the Windows developer machine; the macOS (Mac Catalyst) and iOS targets on the MacBook. The `.csproj` selects target frameworks by host OS.

**Logic lives in `EagleEye.ParentApp.Core` (ADR-009).** This project is the MAUI head only: views, `MauiProgram`, platform code and the implementations of the small abstractions Core needs (theme, dialogs, UI dispatcher, app data paths, secret storage). See `src/EagleEye.ParentApp.Core/README.md`.

## Responsibility

- Connect to `EagleEye.Service` over the LAN (`https://<host>:5443/hubs/parent`, self-signed certificate: trust on first use, pinned after pairing, ADR-008)
- Pair once with the 6-digit code shown on the service PC; afterwards connect by itself with the stored token
- Show the connection state in the status bar; light/dark appearance (US-002)
- Show the standard accounts of the service PC and tick the ones under parental control (US-003)
- Show the daily app usage per account under parental control on the Reports page (US-004)
- Edit the break times and the display text per account under parental control on the Rules page (US-005)
- Later stories: app rules, time budgets, pause windows, statistics per kid

## Structure

```
EagleEye.ParentApp/
├── App.xaml(.cs)            Window "EagleEye" (min. 900 × 600), resolves MainPage, attaches HeadingBarColorService
├── MauiProgram.cs           DI: Core services, platform services, view models, views; Windows: CheckBox MinWidth 0
│                            (US-005, Rules table)
├── Views/
│   ├── MainPage             Desktop layout: menu left, content right, status bar bottom (FR-APP-081);
│   │                        starts on Reports when paired, on Settings when not (ISSUE-007)
│   ├── SettingsView         "Visual appearance", "Server connection" and "User accounts on the EagleEye PC"
│   ├── RulesView            Rules (US-005): account selection, break-time table edited in place (fixed columns
│   │                        40 | 64 | 88 | 88 | 7 × 48), display text; times saved on leaving the field or Enter,
│   │                        text on leaving the box; MainPage flushes typed values when the page changes
│   ├── ReportsView          Reports: account selection, one table per day (App | Nutzung (HH:MM)), US-004
| Nutzung (HH:MM)), US-004
│   └── StatusBarView        Green/red indicator + status text
├── Services/                MauiThemeService, MauiDialogService, MauiUiDispatcher,
│                            MauiAppDataPaths, MauiSecureStorageSecretStore (non-Windows),
│                            HeadingBarColorService (accent colour → resources HeadingBarBackgroundColor/TextColor)
├── Resources/
│   ├── Styles/              Colors (light/dark via AppThemeBinding) and styles; SectionHeader = heading bar
│   └── AppIcon/             appicon.svg (MAUI icon), eagleeye.ico (exe and installer icon)
└── Platforms/
    ├── Windows/             WinUI App, app.manifest, DpapiSecretProtector (token: DPAPI CurrentUser),
    │                        WindowsAccentColor (user accent colour + changes, UISettings)
    ├── Android/             MainActivity, MainApplication, AndroidManifest (INTERNET),
    │                        AndroidAccentColor (system accent colour, Material You)
    └── MacCatalyst/         AppDelegate, SceneDelegate, Program, Info.plist, Entitlements (not built yet)
```

All UI texts come from `EagleEye.ParentApp.Core/Resources/AppTexts*.resx` (German default, English satellite). Only the window title "EagleEye" is not translated.

## Platform Support

| Platform | Min Version | Built on | Distribution |
|----------|-------------|----------|--------------|
| Windows | Windows 11 | Windows machine | Per-user installer `03_Delivery/windows/EagleEye-ParentApp-Setup-<version>.exe` (unpackaged, self-contained, no admin rights, ADR-009) |
| Android | Android 14 (API 34) | Windows machine | Sideloading (adb) |
| macOS | macOS 26 | MacBook | Direct .dmg download |
| iOS | iOS 26 | MacBook | Sideloading (Xcode / ios-deploy) |

## Build, Run, Package (Windows machine)

| Task | Command |
|---|---|
| Build Windows + Android | `pwsh 02_Implementation/scripts/build.ps1` (finds the JDK / Android SDK via `JAVA_HOME` / `ANDROID_HOME` or `%LOCALAPPDATA%\Android`) |
| Run (Debug) | `02_Implementation/src/EagleEye.ParentApp/bin/Debug/net10.0-windows10.0.19041.0/win-x64/EagleEye.ParentApp.exe` |
| Installer | `pwsh 02_Implementation/scripts/package-windows.ps1 -Target ParentApp` |

## Data (Windows)

| What | Where |
|---|---|
| Program files | `%LocalAppData%\Programs\EagleEye Parent App\` |
| Database (pairing, settings) | `%LocalAppData%\EagleEye\EagleEye.ParentApp.db` |
| Token | `Secrets` table of that database, encrypted with DPAPI (CurrentUser). MAUI `SecureStorage` is not used on Windows because it needs package identity. |

Debug builds only: `EAGLEEYE_DEV_APP_DATA_DIR` moves the data folder (DEV smoke checks never touch the data of an installed parent app); `EAGLEEYE_DEV_PORT_OFFSET` shifts the parent port. Release builds contain neither.

Uninstalling removes all three.

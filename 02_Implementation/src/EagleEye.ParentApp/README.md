# EagleEye.ParentApp

Cross-platform parent application built with .NET MAUI. One codebase targets two production platforms (ADR-019): Windows (own per-user installer) and macOS (Mac Catalyst, `.dmg`). iOS and Android were dropped by CR-001.

**Build hosts (ADR-007)**: the Windows target is built on the Windows developer machine; the macOS (Mac Catalyst) target on the MacBook. The `.csproj` selects target frameworks by host OS.

**Logic lives in `EagleEye.ParentApp.Core` (ADR-009).** This project is the MAUI head only: views, `MauiProgram`, platform code and the implementations of the small abstractions Core needs (theme, dialogs, UI dispatcher, app data paths, secret storage). See `src/EagleEye.ParentApp.Core/README.md`.

## Responsibility

- Connect to `EagleEye.Service` over the LAN (`https://<host>:5443/hubs/parent`, self-signed certificate: trust on first use, pinned after pairing, ADR-008)
- Pair once with the 6-digit code shown on the service PC; afterwards connect by itself with the stored token
- Show the connection state in the status bar; light/dark appearance (US-002)
- Show the standard accounts of the service PC and tick the ones under parental control (US-003)
- Show the daily app usage per account under parental control on the Reports page (US-004)
- Edit the break times and the display text per account under parental control on the Rules page (US-005)
- Edit the application rules (daily budget, weekdays) and the budget text per account on the Rules page, and the two time-elapsed warnings on the Settings page (US-006)
- Later stories: statistics per kid

## Structure

```
EagleEye.ParentApp/
├── App.xaml(.cs)            Window "EagleEye" (min. 900 × 600), resolves MainPage, attaches HeadingBarColorService
├── MauiProgram.cs           DI: Core services, platform services, view models, views; Windows: CheckBox MinWidth 0
│                            (US-005, Rules table)
├── Views/
│   ├── MainPage             Desktop layout: menu left, content right, status bar bottom (FR-APP-081);
│   │                        starts on Reports when paired, on Settings when not (ISSUE-007)
│   ├── SettingsView         "Visual appearance", "Server connection", "User accounts on the EagleEye PC" and
│   │                        "Time-elapsed warnings" (US-006: two fixed rows, columns 160 | 96 | *; flushed when
│   │                        the page changes)
│   ├── RulesView            Rules (US-005): account selection, break-time table edited in place (fixed columns
│   │                        40 | 64 | 88 | 88 | 7 × 48), display text; times saved on leaving the field or Enter,
│   │                        text on leaving the box; MainPage flushes typed values when the page changes.
│   │                        US-006: section "Applikations-Regeln" (columns 220 | 64 | 64 | 7 × 48, name with the
│   │                        program path as tooltip, hours/minutes saved on leaving the field or Enter, days at the
│   │                        click) and the budget text
│   ├── ReportsView          Reports: account selection, one table per day (App | Nutzung (HH:MM)), US-004
│   └── StatusBarView        Green/red indicator + status text
├── Services/                MauiThemeService, MauiDialogService, MauiUiDispatcher,
│                            MauiAppDataPaths, Platforms/MacCatalyst/MacKeychainSecretStore (macOS),
│                            HeadingBarColorService (accent colour → resources HeadingBarBackgroundColor/TextColor)
├── Resources/
│   ├── Styles/              Colors (light/dark via AppThemeBinding) and styles; SectionHeader = heading bar
│   └── AppIcon/             appicon.svg (MAUI icon), eagleeye.ico (exe and installer icon)
└── Platforms/
    ├── Windows/             WinUI App, app.manifest, DpapiSecretProtector (token: DPAPI CurrentUser),
    │                        WindowsAccentColor (user accent colour + changes, UISettings)
    └── MacCatalyst/         AppDelegate, SceneDelegate, Program, Info.plist, Entitlements (not built yet)
```

All UI texts come from `EagleEye.ParentApp.Core/Resources/AppTexts*.resx` (German default, English satellite). Only the window title "EagleEye" is not translated.

## Platform Support

| Platform | Min Version | Built on | Distribution |
|----------|-------------|----------|--------------|
| Windows | Windows 11 | Windows machine | Per-user installer `03_Delivery/windows/EagleEye-ParentApp-Setup-<version>.exe` (unpackaged, self-contained, no admin rights, ADR-009) |
| macOS | macOS 26 | MacBook | `.dmg` in `03_Delivery/macos/`, ad-hoc signed only (no Developer ID, no notarisation, ADR-019); allow once via *System Settings → Privacy & Security → Open Anyway* |

## Build, Run, Package (Windows machine)

| Task | Command |
|---|---|
| Build | `pwsh 02_Implementation/scripts/build.ps1` |
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

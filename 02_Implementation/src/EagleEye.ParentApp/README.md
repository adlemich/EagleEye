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
- US-007: the macOS app (Mac Catalyst) with the same pages, macOS language and formats, accent-colour heading bars, Keychain for the token
- US-008 (macOS app): the "App Groups" page (groups of the service PC, drag-and-drop, "Move to …"), the "App group rules" section on Rules and the group lines on Reports. The Windows parent app compiles the same views but gets groups in US-009.
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
│   │                        US-008: section "App-Gruppen-Regeln" before it (same columns and row view model; the
│   │                        members, comma-separated, below the group name)
│   ├── AppGroupsView        US-008 App Groups: explanation, "Add group", one section per group (heading bar with
│   │                        "Rename"/"Delete" in style HeadingBarButton) and "Not in any group". Drag-and-drop with
│   │                        MAUI DragGestureRecognizer (entry) / DropGestureRecognizer (entry area of a section,
│   │                        min. one row high, so empty groups are targets) as chosen by the Step 1 spike; the
│   │                        dragged app is passed in-app (AppGroupsViewModel.DraggedApp). Highlight: DataTrigger on
│   │                        IsDropTarget over an explicit transparent background. "≡" (style EntryMenuButton) and
│   │                        the right-click menu item "Move to …" open the same action sheet
│   ├── ReportsView          Reports: account selection, one table per day (App | Nutzung (HH:MM)), US-004;
│   │                        US-008: "App-Gruppen" table below it when a group has used time that day
│   └── StatusBarView        Green/red indicator + status text
├── Services/                MauiThemeService, MauiDialogService, MauiUiDispatcher,
│                            MauiAppDataPaths, Platforms/MacCatalyst/MacKeychainSecretStore (macOS),
│                            HeadingBarColorService (accent colour → resources HeadingBarBackgroundColor/TextColor)
├── Resources/
│   ├── Styles/              Colors (light/dark via AppThemeBinding) and styles; heading = Border SectionHeaderBar (accent outline,
│   │                        transparent fill; US-008 D-18) around a SectionHeader label;
│   │                        US-008: DropHighlightLight/Dark, HeadingBarButton, EntryMenuButton. MacStyles.xaml
│   │                        (Mac only, merged after Styles.xaml): buttons in the accent colour, explicit "Normal"
│   └── AppIcon/             appicon.svg (MAUI icon), eagleeye.ico (exe and installer icon)
└── Platforms/
    ├── Windows/             WinUI App, app.manifest, DpapiSecretProtector (token: DPAPI CurrentUser),
    │                        WindowsAccentColor (user accent colour + changes, UISettings)
    └── MacCatalyst/         US-007 (ADR-020): AppDelegate, SceneDelegate, Program, Info.plist, Entitlements (no
                             sandbox); MacCultureSetup (language and formats from macOS before any text is read);
                             MacAccentColor (System Settings accent colour for the heading bars);
                             MacKeychainSecretStore (token in the login Keychain); MacControlMappings (Entry
                             width in table cells, Editor border, Picker look); MacNavigationTemplate (menu items
                             with an explicit "Normal" background)
```

All UI texts come from `EagleEye.ParentApp.Core/Resources/AppTexts*.resx` (German default, English satellite). Only the window title "EagleEye" is not translated.

## Platform Support

| Platform | Min Version | Built on | Distribution |
|----------|-------------|----------|--------------|
| Windows | Windows 11 | Windows machine | Per-user installer `03_Delivery/windows/EagleEye-ParentApp-Setup-<version>.exe` (unpackaged, self-contained, no admin rights, ADR-009) |
| macOS | macOS 26 | MacBook | `.dmg` in `03_Delivery/macos/`, ad-hoc signed only (no Developer ID, no notarisation, ADR-019); allow once via *System Settings → Privacy & Security → Open Anyway* |

## Mac Catalyst rule: explicit "Normal" (US-007, US-008)

A property that a visual state or trigger sets and that becomes **unset** again is not repainted by Mac Catalyst: the old colour stays (menu items in US-006 test run 01, TI-10). Every state, trigger or style that sets a background (or text colour) also sets it explicitly in "Normal" or as the base value, e.g. `Transparent`. Visual changes on the Mac are checked on screen (probe + `screencapture`) before they are handed over; US-008 evidence: `docs/requirements/user-stories/US-008/evidence/step9-*`.

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

## Build, Run, Package (MacBook)

| Task | Command |
|---|---|
| Build | `pwsh 02_Implementation/scripts/build.ps1` (Mac Catalyst; `ValidateXcodeVersion=false` is set in the csproj for the maccatalyst TFM, dev-environments.md §5) |
| Run (Debug) | `open "02_Implementation/src/EagleEye.ParentApp/bin/Debug/net10.0-maccatalyst/maccatalyst-arm64/EagleEye Parent App.app"` |
| Disk image | `pwsh 02_Implementation/scripts/package-macos.ps1` → `03_Delivery/macos/EagleEye-ParentApp-<version>.dmg` (prints the SHA-256) |

## Data (macOS)

| What | Where |
|---|---|
| Program | `/Applications/EagleEye Parent App.app` (bundle id `com.adlemich.eagleeye`) |
| Database (pairing, settings) | `~/Library/Application Support/EagleEye/EagleEye.ParentApp.db`; Debug builds: `EagleEye-Debug` |
| Token | login Keychain, generic password of service `com.adlemich.eagleeye` (Debug: `com.adlemich.eagleeye.debug`). After an update macOS may ask once to allow access ("Always Allow"). |

The `.dmg` contains "Remove EagleEye Parent App.command", which removes the data folders and the Keychain entry; the app itself is moved to the Trash by hand. First start of an unsigned build: *System Settings → Privacy & Security → Open Anyway* (ADR-019).

**No connection after an update?** After installing a rebuilt `.dmg`, the app may not connect: macOS treats every ad-hoc signed build as a new app. *System Settings → Datenschutz & Sicherheit → Lokales Netzwerk* then lists one EagleEye entry per build; all show as on, but the current build is not allowed. Fix: quit the app (Cmd+Q), switch the EagleEye entries off and on again, start the app, and answer the Keychain prompt with "Immer erlauben". A failed ping to the service PC proves nothing (Windows blocks ICMP by default); test TCP port 5443 instead: `nc -z -G 3 <ip> 5443`. (dev-environments.md §5)

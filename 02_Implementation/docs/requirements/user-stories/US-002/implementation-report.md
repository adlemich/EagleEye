# Implementation Report: US-002 — Windows Parent App: Installation, Connection and Pairing

**Author**: DEV
**Date**: 2026-10-04
**Branch**: `feature/US-002-windows-parent-app-pairing`
**Plan**: `02_Implementation/docs/requirements/user-stories/US-002/implementation-plan.md` (approved 2026-10-04), ADR-008, ADR-009
**Machine(s) used**: Windows Developer Machine only (see §7)
**Story status**: Implemented

---

## 1. Summary

US-002 is implemented as planned in Steps 0 to 9. The service opens a TLS endpoint for parent apps, pairs them with a 6-digit code shown in the kid's tray client (or written to the Event Log), and authenticates paired apps with a token. The new Windows parent app has its own per-user installer, a desktop main window with a settings page (light/dark switch, server connection, pairing, remove pairing) and a status bar.

| Component | What was built |
|---|---|
| `EagleEye.Shared` | API-first contracts: `IParentHub`, `IParentClientCallback` (empty), `AllowUnpairedAttribute`, `ITrayClientCallback.OnShowPairingCode`; `PairingStatusDto`, `PairingResultDto`, `PairingOutcome`; `HubRoutes.Parent`, `ServiceDefaults.ParentPort` (5443), `PairingRules`; `Communication/ReconnectSchedule` and `ConnectBackoff` (moved from the tray client, same timing); `Data/SqliteDatabase` (pragmas, integrity check, transactional migrations, serialized access) |
| `EagleEye.Service` | Kestrel with two endpoints (HTTP `localhost:5080` for trays, HTTPS `*:5443` for parent apps); `HubEndpointGuard` (hub-to-port binding by local port); `ParentHub` with Bearer authentication; `PairingAuthorizationHubFilter` (default deny); `PairingManager` (one pending code bound to one connection, 5 min, one guess per code); `PairingCodeGenerator`, `PairingTokenService` (256-bit token, SHA-256 hash only); `ServiceDatabase` + `PairedDeviceRepository`; `CertificateManager` + `SelfSignedCertificateFactory` (ECDSA P-256, `CN=EagleEye`, 100 years, DPAPI-protected PFX); `PairingCodeNotifier` (tray clients or Event Log); `PairingCodeEventLog` (event 1000, bilingual); `TrayConnectionTracker`, `ParentConnectionRegistry`; `TrayHub` rejects non-loopback clients; `ServicePaths` with a Debug-only `EAGLEEYE_DATA_DIR` override; logging: source `EagleEye`, `EagleEye.*` at Information to the Event Log, `Microsoft.AspNetCore` at Warning |
| `EagleEye.TrayClient` | `ServiceConnection.PairingCodeReceived`; `UI/PairingCodeDialog` (topmost, in the taskbar, closes at code expiry, replaced by a newer code); 4 new texts (de/en) |
| `EagleEye.ParentApp.Core` (new, ADR-009) | `HostAddress`, `CertificateTrustPolicy` (TOFU, then pin), `ParentHubClient` (+ factory, reconnect policy), `ConnectionCoordinator` (state machine), `ParentDatabase`, `PairingStore`, `SettingsStore`, `ProtectedSecretStore`, abstractions, view models, `AppTexts` + resx (de/en) |
| `EagleEye.ParentApp` (MAUI head) | `MauiProgram`, `App`, `MainPage` (menu, content, status bar), `SettingsView`, `StatusBarView`, light/dark styles, platform services (`MauiThemeService`, `MauiDialogService`, `MauiUiDispatcher`, `MauiAppDataPaths`, `DpapiSecretProtector` on Windows, `MauiSecureStorageSecretStore` elsewhere); Android and Mac Catalyst platform files; unpackaged, self-contained win-x64 |
| Installers | `setup.iss`: `%ProgramData%\EagleEye` ACLs (SIDs), firewall rule "EagleEye Service (Parent apps)" (install/upgrade/uninstall). New `parentapp-setup.iss` (per user, no admin) |
| Scripts | `package-windows.ps1 -Target All\|Service\|ParentApp`; `build.ps1` builds `ParentApp.Core` on both hosts, Mac Catalyst head only with the MAUI workload, finds the Android JDK/SDK |
| Version | `Directory.Build.props` → `0.2.0` (service reports `EagleEye_v0.2`) |

**Build / test result** (Windows, final state): `build.ps1` 0 warnings, 0 errors for all six steps (Shared, Service, TrayClient, ParentApp.Core, ParentApp Windows, ParentApp Android). `test.ps1` all **590** unit tests pass (Shared 68, Service 164, TrayClient 40, ParentApp 318), 0 warnings.

---

## 2. Deviations from the Implementation Plan

| # | Plan | Implemented | Why |
|---|---|---|---|
| D-1 | `CertificateManager` loads the key with `MachineKeySet` | `MachineKeySet` first; if that throws `CryptographicException`, it logs a warning and loads with `UserKeySet` | Without admin rights (console mode, plan Step 2.4) the machine key store is not writable: the import failed with "Zugriff verweigert" and the service could not start. SYSTEM uses `MachineKeySet` as planned; the user key store is also usable by SChannel. Verified: TLS on 5443 works in console mode. |
| D-2 | `WindowsSecretStore` (DPAPI + `Secrets` table) in the MAUI head | Split: `ParentApp.Core/Data/ProtectedSecretStore` (table access, `ISecretStore`) + `ISecretProtector`, implemented on Windows by `Platforms/Windows/DpapiSecretProtector` (two `ProtectedData` calls) | The table logic becomes unit-testable in Core (100 % coverage with a fake protector); only the DPAPI call stays platform code. Behaviour is as planned. |
| D-3 | Windows `SupportedOSPlatformVersion` / `TargetPlatformMinVersion` = 10.0.22000.0 (existing csproj) | 10.0.19041.0 | The SDK refuses a minimum above the TFM version (`NETSDK1135`, TFM `net10.0-windows10.0.19041.0`). Windows 11 is enforced by the installer (`MinVersion=10.0.22000`). |
| D-4 | ParentApp publish with `-r win-x64 --self-contained` on the command line | Without `-r` / `--self-contained`; the csproj sets `RuntimeIdentifier=win-x64`, `SelfContained`, `WindowsAppSDKSelfContained`, `WindowsPackageType=None` for the Windows TFM | On the multi-targeted project, `--runtime` also applied to the Android target during restore and failed (`NU1102 Microsoft.NETCore.App.Runtime.Mono.win-x64`). The output is the same self-contained, unpackaged app. |
| D-5 | `build.ps1`: Android target as before | `build.ps1` passes `JavaSdkDirectory` / `AndroidSdkDirectory` (from `JAVA_HOME` / `ANDROID_HOME`, else `%LOCALAPPDATA%\Android\jdk\…` / `%LOCALAPPDATA%\Android\Sdk`) | The Android build did not find the installed JDK/SDK (`XA5300`). The toolchain is installed at the default location of the VS Code MAUI extension. Until US-002 the Android target was never built (the project had no sources). |
| D-6 | `App` applies the stored theme before the first window is shown | The theme is applied in `MainViewModel.StartAsync` right after the window is loaded (database open → theme → connection → host dialog) | Opening the database is asynchronous and must not block the UI thread. If the stored theme differs from the Windows app mode, the window may show the Windows mode for a fraction of a second. |
| D-7 | `App` gets `MainPage` by constructor injection | `App` resolves `MainPage` in `CreateWindow` via `IServiceProvider` | With constructor injection the views were created before `App.InitializeComponent()` loaded the styles, and the app crashed at start (`XamlParseException: StaticResource not found for key PageHeader`). |
| D-8 | Move of `ReconnectSchedule` / `ConnectBackoff` and the tray `ServiceReconnectPolicy` change in Step 3 | Done in Step 1 together with the Shared code | Moving the class out of the tray client breaks its build unless the tray client is adjusted in the same commit. Same behaviour; the old `ConnectBackoffTests` moved to `Shared.Tests` unchanged, `ServiceReconnectPolicyTests` stay and still pass. |
| D-9 | Text list in "Localization Impact" | Three additional keys (de/en): `ErrorRemoveFailed` ("Die Kopplung konnte nicht aufgehoben werden. Bitte erneut versuchen."), `ErrorStartup` (app data cannot be opened), `OkButton` | The plan requires "remove failure keeps the pairing" but has no text for it; a damaged local database needs a message instead of a crash. |
| D-10 | — | `ConnectionCoordinator` limits connecting and each hub call to 15 s | Without a bound, a connect to a non-existing host could hang in "Connecting to <host> …" for the OS TCP timeout or longer; AC-12 needs a clear error. |
| D-11 | `await using` for SQLite commands (coding guidelines §3.4) | `PairingStore` and `ProtectedSecretStore` use `using` | `await using` in the lambdas creates compiler-generated async-dispose branches that coverlet reports but that cannot be reached with in-memory SQLite (Microsoft.Data.Sqlite disposes synchronously anyway). With `using`, the 100 % branch rule holds without a coverage exception. |
| D-12 | Folders `Communication/`, `ViewModels/` in the MAUI head | Removed (only `.gitkeep`) | ADR-009 moves this code to `ParentApp.Core`. |
| D-13 | Mac Catalyst not part of US-002 | Mac Catalyst platform files added from the .NET 10 MAUI template (`AppDelegate`, `SceneDelegate`, `Program`, `Info.plist`, `Entitlements.plist` with outgoing network access) | The head now has shared code and must build on the MacBook once the workload is installed. **Not built or verified** (needs the MacBook with the MAUI workload). |
| D-14 | Parent app installer as in the plan | Additionally: the uninstaller ends running parent app processes of the current user (`taskkill`) and also deletes `{app}`; the Start-menu entry is directly in *Programme* (no folder); an optional "launch now" checkbox at the end | Otherwise a running app keeps its files and database locked and uninstall would leave them behind (AC-5). |

No other deviations. The plan's "Open Points for Michael" are implemented with their defaults (tray client unchanged in admin sessions; product name "EagleEye Parent App"), see §5.

---

## 3. Files Created or Modified

All paths relative to `02_Implementation/`.

**Shared** — created: `src/EagleEye.Shared/Contracts/IParentHub.cs`, `Contracts/IParentClientCallback.cs`, `Contracts/AllowUnpairedAttribute.cs`, `Models/PairingStatusDto.cs`, `Models/PairingResultDto.cs`, `Models/PairingOutcome.cs`, `Constants/PairingRules.cs`, `Communication/ReconnectSchedule.cs`, `Data/SqliteDatabase.cs`; moved: `Communication/ConnectBackoff.cs` (from TrayClient); modified: `Contracts/ITrayClientCallback.cs`, `Constants/HubRoutes.cs`, `Constants/ServiceDefaults.cs`, `EagleEye.Shared.csproj` (Microsoft.Data.Sqlite 10.0.12).

**Service** — created: `src/EagleEye.Service/IServicePaths.cs`, `ServicePaths.cs`, `Certificates/{ICertificateManager, CertificateManager, ISelfSignedCertificateFactory, SelfSignedCertificateFactory}.cs`, `Data/{ServiceDatabase, PairedDevice, IPairedDeviceRepository, PairedDeviceRepository}.cs`, `Pairing/{IPairingManager, PairingManager, IPairingCodeGenerator, PairingCodeGenerator, IPairingTokenService, PairingTokenService}.cs`, `Diagnostics/{IPairingCodeEventLog, PairingCodeEventLog}.cs`, `Communication/{ParentHub, PairingAuthorizationHubFilter, HubEndpointGuard, BearerToken, ParentConnectionState, IParentConnectionRegistry, ParentConnectionRegistry, ITrayConnectionTracker, TrayConnectionTracker, IPairingCodeNotifier, PairingCodeNotifier}.cs`; modified: `Program.cs`, `Communication/TrayHub.cs`, `EagleEye.Service.csproj` (ProtectedData 10.0.12).

**TrayClient** — created: `src/EagleEye.TrayClient/UI/PairingCodeDialog.cs`; modified: `Communication/IServiceConnection.cs`, `Communication/ServiceConnection.cs`, `Communication/ServiceReconnectPolicy.cs`, `UI/TrayApplicationContext.cs`, `UI/TrayTexts.cs`, `Resources/TrayTexts.resx`, `Resources/TrayTexts.en.resx`; deleted: `Communication/ConnectBackoff.cs` (moved).

**ParentApp.Core** (new project) — `src/EagleEye.ParentApp.Core/EagleEye.ParentApp.Core.csproj`, `README.md`, `AppTexts.cs`, `Resources/AppTexts.resx`, `Resources/AppTexts.en.resx`, `Abstractions/{ISecretStore, ISecretProtector, IThemeService, IDialogService, IUiDispatcher, IAppDataPaths, ThemeMode}.cs`, `Communication/{HostAddress, CertificateTrustPolicy, IParentHubClient, ParentHubClient, IParentHubClientFactory, ParentHubClientFactory, ParentReconnectPolicy, IConnectionCoordinator, ConnectionCoordinator, ConnectionState, ConnectionStatus, ConnectionMessage, PairingStatus}.cs`, `Data/{ParentDatabase, StoredPairing, IPairingStore, PairingStore, ISettingsStore, SettingsStore, ProtectedSecretStore}.cs`, `ViewModels/{MainViewModel, NavigationItem, AppearanceViewModel, ServerConnectionViewModel, StatusBarViewModel, ConnectionMessageText}.cs`.

**ParentApp** (MAUI head) — created: `src/EagleEye.ParentApp/{App.xaml, App.xaml.cs, MauiProgram.cs}`, `Views/{MainPage, SettingsView, StatusBarView}.xaml(.cs)`, `Services/{MauiThemeService, MauiDialogService, MauiUiDispatcher, MauiAppDataPaths, MauiSecureStorageSecretStore}.cs`, `Resources/Styles/{Colors, Styles}.xaml`, `Resources/AppIcon/{appicon.svg, eagleeye.ico}`, `Platforms/Windows/{App.xaml, App.xaml.cs, app.manifest, DpapiSecretProtector.cs}`, `Platforms/Android/{MainActivity.cs, MainApplication.cs, AndroidManifest.xml, Resources/values/colors.xml}`, `Platforms/MacCatalyst/{AppDelegate.cs, SceneDelegate.cs, Program.cs, Info.plist, Entitlements.plist}`; modified: `EagleEye.ParentApp.csproj`, `README.md`; deleted: `.gitkeep` files in `Communication/`, `ViewModels/`, `Views/`, `Platforms/{Windows,Android,MacCatalyst}/`.

**Tests** — created: `tests/EagleEye.Shared.Tests/{Communication/ReconnectScheduleTests, Constants/PairingRulesTests, Data/SqliteDatabaseTests, Models/PairingDtoTests}.cs`; moved: `tests/EagleEye.Shared.Tests/Communication/ConnectBackoffTests.cs`; `tests/EagleEye.Service.Tests/{ServicePathsTests, Certificates/SelfSignedCertificateFactoryTests, Data/PairedDeviceRepositoryTests, Pairing/PairingManagerTests, Pairing/PairingCodeGeneratorTests, Pairing/PairingTokenServiceTests, Communication/ParentHubTests, Communication/PairingAuthorizationHubFilterTests, Communication/HubEndpointGuardTests, Communication/BearerTokenTests, Communication/PairingCodeNotifierTests, Communication/ParentConnectionRegistryTests, Communication/TrayConnectionTrackerTests, Communication/HubContextFactory}.cs`; `tests/EagleEye.ParentApp.Tests/{AppTextsTests, Fakes/Fakes, Communication/HostAddressTests, Communication/CertificateTrustPolicyTests, Communication/ParentReconnectPolicyTests, Communication/ConnectionStateTests, Communication/ConnectionCoordinatorTests, Data/PairingStoreTests, Data/SettingsStoreTests, Data/ProtectedSecretStoreTests, ViewModels/MainViewModelTests, ViewModels/AppearanceViewModelTests, ViewModels/ServerConnectionViewModelTests, ViewModels/StatusBarViewModelTests, ViewModels/ConnectionMessageTextTests}.cs`; modified: `tests/EagleEye.Service.Tests/Communication/TrayHubTests.cs`, `tests/EagleEye.TrayClient.Tests/UI/TrayTextsTests.cs`, `tests/EagleEye.ParentApp.Tests/EagleEye.ParentApp.Tests.csproj` (references Core; xunit, Moq, coverlet, TimeProvider.Testing 10.10.0), `tests/EagleEye.Service.Tests/EagleEye.Service.Tests.csproj` (TimeProvider.Testing 10.10.0).

**Build, installers, docs** — modified: `Directory.Build.props`, `EagleEye.sln`, `scripts/build.ps1`, `scripts/package-windows.ps1`, `installer/windows/setup.iss`; created: `installer/windows/parentapp-setup.iss`, this report; modified: `docs/requirements/user-stories/US-002/user-story.md` (status).

---

## 4. Unit Test Coverage

Coverage measured with coverlet (`dotnet test --collect:"XPlat Code Coverage"`), line and branch.

| Class | Test class | Scenarios | Line / branch |
|---|---|---|---|
| `PairingRules` | `PairingRulesTests` | 6 digits incl. `000000`; 5, 7, letters, space, leading space, full-width digits, null rejected; name trimmed, 50 OK (also with surrounding spaces), 51, empty, whitespace, tab, newline, NUL, DEL rejected; lifetime 5 min | 100 / 100 |
| `ReconnectSchedule`, `ConnectBackoff` | `ReconnectScheduleTests`, `ConnectBackoffTests` | 0/2/10 s then 30 s forever (incl. `long.MaxValue`); 1, 2, 4, 8, 16, cap 30; negative guards | 100 / 100 |
| `SqliteDatabase` | `SqliteDatabaseTests` | pragmas (foreign_keys, busy_timeout, synchronous), migrations in order, version 0 without migrations, only new migrations on an existing DB, failing migration rolls back, not initialized, work throws and the gate is released, integrity ok / not ok, connection-string builder | 100 / 100 |
| DTOs | `PairingDtoTests`, `ServiceVersionDtoTests` | construction, equality, inequality | 100 / 100 |
| `SelfSignedCertificateFactory` | `SelfSignedCertificateFactoryTests` | CN, SANs (machine, localhost), EKU server auth, ≥ 99 years, starts before now, private key, ECDSA P-256, not a CA, guard | 100 / 100 |
| `PairingCodeGenerator`, `PairingTokenService` | `PairingCodeGeneratorTests`, `PairingTokenServiceTests` | 6 ASCII digits, leading zeros occur; token 32 bytes Base64Url (43 chars, no padding), unique; hash deterministic, 32 bytes, differs per token | 100 / 100 |
| `PairingManager` | `PairingManagerTests` | start notifies; second start replaces code (also from another connection); success returns GUID + token and stores the hash (not the token), trimmed name and time; second submit has no code; wrong code; exactly 5:00 OK; 5:01 expired; no code; other connection; invalid format; invalid name; **every failure clears the pending code** (5 cases); failure stores nothing; code/token never logged; authenticate known/unknown/null/empty; remove existing/unknown; guards | 100 / 100 |
| `PairedDeviceRepository`, `ServiceDatabase` | `PairedDeviceRepositoryTests` | schema version 1, add + find (all fields), unknown hash, remove existing/unknown, remove one of two, unique hash, guards | 100 / 100 |
| `PairingCodeNotifier` | `PairingCodeNotifierTests` | 1 and 3 trays → push to all, no Event Log; 0 trays → Event Log, no push; guard | 100 / 100 |
| `TrayConnectionTracker`, `ParentConnectionRegistry` | `TrayConnectionTrackerTests`, `ParentConnectionRegistryTests` | count, concurrent increments; register, unregister one/last/unknown, abort all except caller, abort all, other device untouched, unknown device, guard | 100 / 100 |
| `PairingAuthorizationHubFilter` | `PairingAuthorizationHubFilterTests` | unpaired + protected → "Not paired"; unpaired + `[AllowUnpaired]` → next; paired + StartPairing/SubmitPairingCode → "Already paired"; paired + other → next; guards | 100 / 100 |
| `HubEndpointGuard` | `HubEndpointGuardTests` | tray on 5080, parent on 5443 (case-insensitive, sub-paths), other paths pass; tray on 5443/8080 and parent on 5080 → 404; guard | 100 / 100 |
| `ParentHub`, `BearerToken`, `ParentConnectionState` | `ParentHubTests`, `BearerTokenTests` | known token → paired, group, registered; unknown/no header/no HTTP context → unpaired; disconnect unregisters only paired; status paired/unpaired; delegation; exceptions mapped to safe `HubException` (and passed through); remove: empty ID, unknown, failure, own device (cleared, group left, unregistered), other device (caller stays paired), aborts other connections; Bearer parsing incl. case, spaces, other schemes | 100 / 100 |
| `TrayHub` | `TrayHubTests` | version; IPv4/IPv6 loopback tracked; remote, missing address, missing HTTP context aborted and not tracked; disconnect decrements only tracked; logging | 100 / 100 |
| `ServicePaths` | `ServicePathsTests` | path derivation, guards, `Resolve` with and without the Debug override | 83 / 100 (`EnsureDirectories` touches the file system; verified in the smoke check) |
| `TrayTexts` (new keys) | `TrayTextsTests` | 4 new keys in de and en, same key set, fallback | 100 / 100 |
| `HostAddress` | `HostAddressTests` | hostname, upper case, FQDN, trailing dot, IPv4, IPv6 (brackets in URI), IPv4-mapped IPv6, trimmed; rejects empty, scheme, port, IPv6 with brackets/port/scope, path, space, leading/trailing hyphen, empty label, underscore, umlaut, numeric-only forms (`1.2.3`, `1`, `999.1.1.1`), hex, label > 63, host > 253 | 100 / 100 |
| `CertificateTrustPolicy` | `CertificateTrustPolicyTests` | TOFU accepts and captures; pinned equal (also lower case) accepts; different rejects and records mismatch; null rejects; guards | 100 / 100 |
| `ConnectionCoordinator` | `ConnectionCoordinatorTests` (61 tests) | every transition of the state diagram with mocked client/factory/store and `FakeTimeProvider`: init not paired / paired → connected / pairing lost / invalid stored host / unreachable / pin mismatch / status call fails; retry after 1 s backoff, not before; reconnecting → red, reconnected → green or pairing lost or stays red; closed → new connect loop; events of replaced clients ignored; pairing: connecting state, TOFU without token, unreachable, StartPairing fails, invalid host, already pairing; new code; submit success (stored with thumbprint and token, reconnect with token and pin), all rejection outcomes, local validation without consuming the code, call fails, success without ID/token/thumbprint; cancel; connection lost while waiting; remove when connected / failure keeps pairing / disconnected / not paired; **never Connected without IsPaired**; **token never in State**; dispose while retrying, while pairing, during connect | 100 / 100 |
| `PairingStore`, `SettingsStore`, `ProtectedSecretStore`, `ParentDatabase`, `StoredPairing` | `PairingStoreTests`, `SettingsStoreTests`, `ProtectedSecretStoreTests` | save/load/delete incl. token, token only in the secret store, overwrite, row without token, DB busy (serialized access), DB errors propagate; theme absent/light/dark/unknown, stored values `light`/`dark`; secrets encrypted, overwrite, remove, guards; `ToString` without token | 100 / 100 |
| View models | `MainViewModelTests`, `AppearanceViewModelTests`, `ServerConnectionViewModelTests`, `StatusBarViewModelTests`, `ConnectionMessageTextTests` | status text in de/en for all states (all four formats), green only for PairedConnected; theme from store vs. system, applied, toggle applies and stores, label; visibility/enabled rules per state (host only when not paired, remove only when connected, AC-30 hint), host prefill/clear, all commands, confirmation cancel/confirm with host in the message, host dialog texts and cancel; start sequence (dialog only when not paired, runs once), menu "Einstellungen"; every message text | 100 / 100 |
| `AppTexts` | `AppTextsTests` | every key in de and en, fallback to German (fr, invariant), same key set, every key has an accessor, missing key guard, culture formatting | 100 / 100 |

**Not unit-tested, as the plan specifies (verified in the smoke check, §6):** `ParentHubClient`, `ParentHubClientFactory`, `CertificateManager`, `PairingCodeEventLog`, both `Program` classes, `PairingCodeDialog`, the MAUI views and platform services, the installers.

---

## 5. Open Questions and Risks

1. **Open points of the plan (answered by Michael 2026-10-04, see user story Q-7 to Q-11; the implemented defaults stand):**
   - The tray client still starts in **admin** sessions (HKLM Run key). A parent pairing at the service PC as admin sees the code there. Accepted (Q-7).
   - Product name in Start menu and *Installierte Apps*: **"EagleEye Parent App"** (one `#define` in `parentapp-setup.iss`). Kept (Q-10).
   - A kid can pair their own app: no technical protection for now, accepted risk (Q-8, arc42 R-8). A second Windows PC is available for AC-2, AC-6, AC-23 and AC-24 (Q-9).
2. **Service installer not executed by DEV.** The DEV session has no admin rights. ACLs, the firewall rule, the service running as SYSTEM with `MachineKeySet`, and the Event Log source/event 1000 are verified for the first time in Michael's test run.
3. **Event Log path (AC-15) not smoke-tested.** In console mode a tray client was always connected. Without admin rights, writing the Event Log source fails and is logged as an error (by design, D-1 context); as SYSTEM it is expected to work.
4. **Pin mismatch during an automatic reconnect** shows "Not connected to <host>" instead of the "identity has changed" message. The message appears on the next connect loop (app start, or after the server closes the connection). Only relevant if the service certificate changes while the app runs.
5. **Pending code and other connections:** per the plan, *every* failed submission clears the pending code, also one from another connection. A second unpaired app on the LAN could thereby invalidate a code that is being typed (the parent requests a new one).
6. **`ConnectionCoordinator` is about 500 lines**, above the "~200 lines" clean-code target. It is one cohesive state machine; splitting it would spread the transitions over several classes. Candidate for a refactoring story if it grows.
7. **Theme at start** may flash the Windows mode briefly when the stored choice differs (D-6).
8. **Mac Catalyst files are unverified** (D-13); `build.ps1` on the MacBook skips the head until the workload is installed.
9. **Operational Event Log source changed** from the .NET default to `EagleEye` (plan). Older 0.1.x entries appear under the previous source.
10. Installer sizes: service 71 MB, parent app 74 MB (plan expected 100 to 150 MB for the parent app).

---

## 6. How to Test

### Artifacts and version

| Item | Value |
|---|---|
| Service + tray installer | `03_Delivery/windows/EagleEye-Setup-0.2.0.exe` (admin) |
| Parent app installer | `03_Delivery/windows/EagleEye-ParentApp-Setup-0.2.0.exe` (per user, **no admin rights**, no .NET needed) |
| Rebuild | `pwsh 02_Implementation/scripts/package-windows.ps1` (both) or `-Target Service` / `-Target ParentApp` |
| Version | Product 0.2.0; the service reports `EagleEye_v0.2` (tray → *App Infos*); *Installierte Apps*: "EagleEye Parent App", 0.2.0, Michael Adler |

### Install and start

1. **Service PC, admin account:** run `EagleEye-Setup-0.2.0.exe` (update over 0.1.x is fine). It stops the old service, installs, creates `%ProgramData%\EagleEye` and `certs\` with restricted ACLs, adds the firewall rule and starts the service.
2. **Parent PC (or the service PC under the parent's own account):** run `EagleEye-ParentApp-Setup-0.2.0.exe` as a normal user. Default folder `%LocalAppData%\Programs\EagleEye Parent App`; Start menu entry **EagleEye Parent App**; optional desktop icon (unchecked); last page offers to start the app.
3. On first start the app shows the dialog **"Mit dem EagleEye-PC verbinden"** (hostname or IP, buttons *Verbinden* / *Abbrechen*). The main window has the menu entry **Einstellungen**, sections **Darstellung** (switch + "Hell"/"Dunkel") and **Serververbindung**, and the status bar at the bottom.

### What to observe (German UI labels)

| Area | Observation |
|---|---|
| Ports | `netstat -ano \| findstr "5443 5080"`: `0.0.0.0:5443` and `[::]:5443` LISTENING; `127.0.0.1:5080` and `[::1]:5080` |
| Hub binding | `https://<service-pc>:5443/hubs/tray/negotiate` → 404; `http://localhost:5080/hubs/parent/negotiate` → 404 |
| Firewall | *Windows Defender Firewall mit erweiterter Sicherheit → Eingehende Regeln* → "EagleEye Service (Parent apps)", TCP 5443, program `…\EagleEye\Service\EagleEye.Service.exe`, remote *Lokales Subnetz*, all profiles. Removed on uninstall. |
| ACLs | `icacls "%ProgramData%\EagleEye"`: SYSTEM (F), Administratoren (F), Benutzer (RX), no inherited entries; `icacls "%ProgramData%\EagleEye\certs"`: SYSTEM and Administratoren only. A standard user cannot open `certs\eagleeye.pfx`. |
| Certificate (AC-7) | Browser `https://<service-pc>:5443/` → certificate warning for `CN=EagleEye` (the browser does not trust it; the app pins it and never asks). |
| Pairing code (AC-14) | Window **"EagleEye – Eltern-App koppeln"**, topmost, in the taskbar: "Kopplungscode: 123456", "Geben Sie diesen Code in der EagleEye-Eltern-App ein.", "Der Code ist 5 Minuten gültig.", button OK. Shown in **every** session with a running tray client (also admin sessions). Closes itself after 5 minutes; *Neuen Code anfordern* replaces it. |
| Pairing code without tray (AC-15) | End `EagleEye.TrayClient.exe` in **all** sessions, then *Verbinden*. *Ereignisanzeige → Windows-Protokolle → Anwendung*, source **EagleEye**, event ID **1000**, Information: "EagleEye-Kopplungscode / pairing code: 123456 — gültig 5 Minuten / valid for 5 minutes." |
| Pairing in the app | After *Verbinden*: "Kopplungsstatus: Kopplung läuft", fields *Kopplungscode* and *Gerätename dieses PCs* (prefilled with the PC name), buttons *Koppeln*, *Neuen Code anfordern*, *Abbrechen*; status bar red "Verbindung zu <host> wird hergestellt …". Success: "Gekoppelt", "Gekoppelt mit: <host>", "Dieses Gerät: <name>", green "Verbunden mit <host>". |
| Messages | Wrong code: "Der Kopplungscode ist falsch." (then *Neuen Code anfordern*). Expired: "Der Kopplungscode ist abgelaufen." Empty name: "Bitte einen Gerätenamen eingeben (höchstens 50 Zeichen)." (checked in the app; the code stays valid). Code not 6 digits: "Der Kopplungscode besteht aus 6 Ziffern." Unreachable: "Verbindungsfehler: Keine Verbindung zum EagleEye-Dienst unter <host> möglich." (within about 15 to 21 s for a host that does not answer). Invalid input: "Bitte einen gültigen Hostnamen oder eine IP-Adresse eingeben." |
| Each code allows one try | Any failed *Koppeln* invalidates the code on the service; request a new one. |
| Paired operation | Remove button **Kopplung aufheben** only enabled when connected; otherwise disabled with "Zum Aufheben der Kopplung ist eine Verbindung zum EagleEye-PC nötig." (AC-30). Confirmation dialog "Kopplung aufheben?". After removal: "Nicht gekoppelt", host entry empty, red "Nicht verbunden"; next start shows the host dialog again. |
| Service no longer knows the pairing | (Not an AC; ARC decision.) App deletes its pairing and shows "Dieser PC ist nicht mehr mit <host> gekoppelt.", host prefilled. |
| Timing (AC-21, AC-22) | App start with pairing: green within a few seconds. Service stopped: red "Nicht verbunden mit <host>" at once (killed process/unplugged PC: within the 30 s SignalR timeout). Lost connection retries after 0, 2, 10 s, then every 30 s; at app start (service down) retries after 1, 2, 4, 8, 16, 30 s, … |
| Theme (AC-9) | First start follows *Einstellungen → Personalisierung → Farben → App-Modus*; the switch shows it. Changes apply immediately and survive restart and reinstall. |
| Repair/update (AC-4) | Run the parent app installer again (also while the app runs; it is closed). Pairing and theme are kept. |
| Uninstall (AC-5) | *Einstellungen → Apps → Installierte Apps → EagleEye Parent App → Deinstallieren*: removes `%LocalAppData%\Programs\EagleEye Parent App`, the Start menu entry, and `%LocalAppData%\EagleEye` (database with pairing, theme and encrypted token). The service keeps the device (Q-2). |

### Test setup notes

- **Second Windows PC** for AC-6, AC-23, AC-24 (two apps on two PCs), and a PC without .NET for AC-2.
- Pairing several apps leaves orphaned device entries on the service after app uninstalls (Q-2). To reset the service completely (certificate and all pairings): stop the service as admin, delete `%ProgramData%\EagleEye`, start the service. Paired apps then show "nicht mehr gekoppelt" or, if the certificate changed, the identity message and must be reinstalled (ADR-008).
- Uninstalling the service keeps `%ProgramData%\EagleEye`; a reinstall keeps the certificate and the pairings.

### Data and logs

| What | Where |
|---|---|
| Service data | `%ProgramData%\EagleEye\EagleEye.Service.db` (table `PairedDevices`: device ID, name, token **hash**, paired at), `%ProgramData%\EagleEye\certs\eagleeye.pfx` (DPAPI LocalMachine) |
| Parent app data | `%LocalAppData%\EagleEye\EagleEye.ParentApp.db` (`ServerConnections`, `AppSettings`, `Secrets` with the DPAPI CurrentUser-protected token) |
| Service log | *Ereignisanzeige → Anwendung*, source `EagleEye`: TLS certificate created/loaded (thumbprint), parent/tray connects and disconnects, "Pairing started … code ***", "Pairing code *** sent to N tray client connection(s)", "Pairing rejected …: WrongCode", "Parent device <id> (<name>) paired …", "Remove parent device …". Never a code (except event 1000) or token. |
| Full console log | Stop the service, run `C:\Program Files\EagleEye\Service\EagleEye.Service.exe` from an admin console. |
| Parent app log | None yet (no file logging in US-002). |
| DEV console mode | Debug build only: `EAGLEEYE_DATA_DIR=<folder>` redirects the service data folder so the service runs without admin rights. Release builds ignore it. |

### DEV smoke check (Windows Developer Machine, 2026-10-04, non-elevated)

- Service (Debug, console, `EAGLEEYE_DATA_DIR` in `02_Implementation/artifacts/smoke-data`): listens on `127.0.0.1/[::1]:5080` and `0.0.0.0/[::]:5443`; negotiate `/hubs/tray` on 5080 → 200, on 5443 → 404; `/hubs/parent` on 5443 (localhost and hostname) → 200, on 5080 → 404. The certificate is created once and reloaded with the same thumbprint on restart.
- Debug parent app + Debug tray client, driven through UI Automation: host dialog shown (German texts, dark theme following Windows); `localhost` → tray window "EagleEye – Eltern-App koppeln" with the code; wrong code → "Der Kopplungscode ist falsch."; *Neuen Code anfordern* → new code in the tray; correct code → "Gekoppelt mit: localhost", green "Verbunden mit localhost". Service killed → red "Nicht verbunden mit localhost", remove disabled with the AC-30 hint; service restarted → green again. App restarted → green after 2 s without dialog (token from DPAPI). *Kopplung aufheben* → confirmation → "Nicht gekoppelt", service log "removed". Switch → "Hell". Service log contains no code.
- **Installed** parent app (`EagleEye-ParentApp-Setup-0.2.0.exe /VERYSILENT`, no admin): installed to `%LocalAppData%\Programs\EagleEye Parent App`, Start menu entry, uninstall entry "EagleEye Parent App" 0.2.0 Michael Adler; exe file version 0.2.0.0, company Michael Adler. Host dialog shown; paired against the console service via the **hostname** (device name prefilled with the PC name) → green. Installer re-run while the app ran → app closed, restarted app green without a code (AC-4). Silent uninstall with the app running → program folder, Start menu entry, `%LocalAppData%\EagleEye` and the uninstall entry are gone (AC-5).
- **Not done:** running `EagleEye-Setup-0.2.0.exe` (needs admin), Event Log path, firewall/ACLs, a second PC.
- Clean-up: no EagleEye process left running; the parent app is uninstalled; nothing was installed system-wide. Left behind (all git-ignored): `02_Implementation/artifacts/` (publish output, smoke data folder with a test certificate and DB, logs, the MAUI template used for platform boilerplate, helper scripts). Non-elevated certificate loading may have left an unused key file in the user's key store (`%APPDATA%\Microsoft\Crypto\Keys`).

---

## 7. Machine(s) Used

| Step | Machine |
|---|---|
| Steps 0 to 9 (code, unit tests, `build.ps1`, `test.ps1`, both installers, smoke check) | Windows Developer Machine |
| MacBook | Not used. The Mac Catalyst head files are unverified (D-13); `build.ps1` on the MacBook builds Shared and ParentApp.Core and skips the head while the MAUI workload is missing. |

# Implementation Report: US-004 — App Usage Tracking and Daily Usage Report

**Author**: DEV
**Date**: 2026-10-08
**Branch**: `feature/US-004-app-usage-tracking`
**Plan**: `02_Implementation/docs/requirements/user-stories/US-004/implementation-plan.md` (approved 2026-10-07, Q-1 to Q-9 answered), ADR-011 (incl. Security Analysis), ADR-012, ADR-005 amendment, ADR-010
**Machine(s) used**: Windows Developer Machine only (see §9)
**Story status**: Verified/Closed (Michael, 2026-10-10)

---

## 1. Summary

US-004 is implemented as planned in Steps 0 to 11. The service starts a hardened **session agent** (its own executable with `--session-agent`, SYSTEM) in every logged-on session of an account under parental control. The agent reports the session's app windows (Task Manager's "Apps" rule) as JSON lines over anonymous pipes. The service validates every reported process, names it, keeps the app inventory and the start/end history, credits active time every 5 s with the monotonic clock (split at local midnight), stores it per app and day and pushes changed days to the parent apps. The Windows parent app has a new menu entry **Berichte** with the account selection and one table per day.

| Component | What was built |
|---|---|
| `EagleEye.Shared` | `IParentHub.GetAccountUsage`, `IParentClientCallback.OnDayUsageChanged`; DTOs `AppUsageDto`, `DayUsageDto`, `AccountUsageDto` |
| `EagleEye.Service` — `SessionAgent/` | `SessionAgentHost` (agent mode, `SetDefaultDllDirectories` first), `WindowEnumerator` (non-messaging window functions only, ≤ 20 000 windows), `AppWindowRule`, `AppWindowScanner`, `AgentProtocol` (strict, source-generated JSON), `AgentReportPublisher` (on change, heartbeat 5 s), `AgentDiagnostics` |
| `EagleEye.Service` — `Monitoring/` | `WtsSessionSource`, `SessionStateTracker`, `EagleEyeServiceLifetime` (SCM session and power events), `AgentStartSpec`, `SessionAgentLauncher` + `PipeAgentProcess` (hardened token, DACLs, environment, handle list), `WriteRestrictionPolicy`, `DevSessionAgentLauncher` (Debug only), `AgentPlan`, `SessionAgentSupervisor`, `BoundedLineReader`, `Win32ProcessInspector`, `ProgramPathPolicy`, `VersionResourceReader`, `PackageManifestReader`, `DisplayNameSanitizer`, `Win32AppMetadataSource` (impersonation), `AppNameResolver`, `AgentReportProcessor` |
| `EagleEye.Service` — `Statistics/` | `UsageEventQueue` (latest-only reports per session), `UsageTracker`, `UsageAccumulator`, `DaySplitter`, `InstanceLogLimiter`, `UsageHistoryLog`, `UsageService` (state owner, also `IAccountDataPurger`), `UsageBroadcaster`, `UsageAccountingLoop`, `UsageRecordingRegistration` (DI) |
| `EagleEye.Service` — other | Migration 3 + `UsageRepository`; `UserAccountService.GetControlledAccountsAsync` + purge hook; `ParentHub.GetAccountUsage`; `EndpointPorts` (Debug port offset, `HubEndpointGuard`); `DevSettings`, `DevLocalAccountSource` (Debug only); `Program`: agent-mode branch first, start order; csproj: `StartupHookSupport=false` |
| `EagleEye.ParentApp.Core` | Hub client calls + callback, gateway forwarding, `Reports/AccountUsageModel`, `ReportsViewModel`, `DayUsageViewModel`, `AppUsageRowViewModel`, `UsageDuration`, menu entry, 7 texts (de/en), Debug-only parent port offset |
| `EagleEye.ParentApp` | `Views/ReportsView` (tables with fixed column widths), `MainPage` page switching, DI |
| Installer | `setup.iss`: ends remaining `EagleEye.Service.exe` processes (agents) after `net stop` (install, upgrade, uninstall) |
| Version | `0.4.0`, patch `0.4.1` for ISSUE-007 (service reports `EagleEye_v0.4`) |

**Build / test result** (Windows, final state): `build.ps1` 0 warnings, 0 errors for all six steps (Shared, Service, TrayClient, ParentApp.Core, ParentApp Windows, ParentApp Android). `test.ps1` all **1 513** unit tests pass (Shared 146, Service 782, TrayClient 40, ParentApp 545), 0 failed (0.4.1; 1 494 / ParentApp 526 at 0.4.0).

---

## 2. Deviations from the Implementation Plan

| # | Plan | Implemented | Why |
|---|---|---|---|
| D-1 | Agent protocol kinds `Window`, `FileExplorer` (ADR-011 §3) | Two more kinds: `StoreApp` with `host` (the frame's PID), and `ExplorerWindow` | The service must check the frame's process path (real `ApplicationFrameHost`, ADR-011 §5); without the frame PID it cannot. `ExplorerWindow` closes a gap: the agent recognises `explorer.exe` by name, so a game **renamed** to `explorer.exe` would never have counted. Now the service drops such windows only for the real `%SystemRoot%\explorer.exe` and counts them for any other program (T-10). |
| D-2 | Windows version API for files in `%SystemRoot%`, `%ProgramFiles%`, `%ProgramFiles(x86)%` | Additionally the file's **owner** must be SYSTEM, Administrators or TrustedInstaller; otherwise the managed `VersionResourceReader` is used | Some folders below `%SystemRoot%` are writable by standard users (e.g. `C:\Windows\Temp`). Stricter than planned; no function lost (Windows files are owned by TrustedInstaller). |
| D-3 | User SID via `WTSQueryUserToken` + `GetTokenInformation(TokenUser)` | Via `WTSUserName`/`WTSDomainName` translated to the SID (`NTAccount.Translate`) | Works as SYSTEM and unelevated (console smoke check). `WTSQueryUserToken` is still used for the impersonation in `Win32AppMetadataSource`. |
| D-4 | "DEV verifies in Step 3 whether the agent starts under the write-restricted token" | Automatic fallback (`WriteRestrictionPolicy`): if the first 2 agents exit before their first report and no agent has ever worked with the restriction, later agents start without it; logged as Warning. The agent's diagnostic line shows the variant. | DEV has no SYSTEM rights, so the check is Michael's (§7, check A). The fallback keeps everything else (no privileges, Administrators deny-only, System integrity, no windows), as Q-9 allows. |
| D-5 | Smoke check with the installed 0.4.0 parent app paired with a console service on `localhost` | Debug-only switch `EAGLEEYE_DEV_PORT_OFFSET` (service: both ports and `HubEndpointGuard`; parent app: `HostAddress`); smoke check with the **Debug** parent app on 15443 | Michael's 0.3.1 service holds 5443/5080 on this PC and must not be touched or paired with. Release builds do not contain the switch (checked in the published DLLs, §5). |
| D-6 | `EAGLEEYE_DEV_WATCH_SID`: "that SID is treated as controlled" | The SID's account is reported as a **standard** account (Debug-only `DevLocalAccountSource`) and is then ticked in the app | DEV's own account is an admin and would otherwise never appear (US-003). The real US-003 path (tick → controlled) is exercised. |
| D-7 | `UsageTracker` writes the start/end log entries | `UsageService` writes them (`UsageHistoryLog` + `InstanceLogLimiter`) | The log line names the database instance id, which exists only after the insert. Templates and the limit are unchanged. |
| D-8 | `UsageService` methods `RecordAppAsync`, `StartInstanceAsync`, `EndInstanceAsync`, `ApplyTickAsync` | One entry point `ApplyAsync(TrackerOutput, userNames, isTick)` | The tracker produces starts, ends and credits together; one call per event keeps the order and the lock simple. Same behaviour (records/starts/ends written at once, usage once per tick in one transaction). |
| D-9 | `UserAccountService` gets `IAccountDataPurger` | Gets `Lazy<IAccountDataPurger>` | `UsageService` needs `IUserAccountService` (inventory check); the plain dependency is a DI cycle. 6 constructor dependencies (guideline ~5). `UsageAccountingLoop` has 8 (queue, sessions, supervisor, tracker, usage, accounts, clock, logger). |
| D-10 | Store name: the package manifest's `DisplayName` | `VisualElements DisplayName` of the application whose `Executable` is the app's program (then the first application, then `Properties/DisplayName`); `ms-resource` keys mapped under `/Resources/` | Task Manager shows the app's name ("Rechner", "Snipping Tool"), not the package's store name. |
| D-11 | `IAccountUsageModel.SelectAccountAsync` | `SelectAccount` (synchronous; the fetch runs in the background) | The selection comes from a binding setter; the model reports progress through `LoadState`/`Changed`. |
| D-12 | — | US-003 test `AccountInventoryMonitorTests` waits until the monitor created its timer | .NET 10 runs `BackgroundService.ExecuteAsync` via `Task.Run`; the tests advanced the fake clock before the timer existed and failed sporadically. No product change. |
| D-13 | — | Source-generated JSON code (`AgentProtocolJsonContext`) is excluded from coverage (`[ExcludeFromCodeCoverage]` with justification) | Generated code; the protocol rules are tested through `AgentProtocol`. |

The plan's own story interpretations D-1 to D-9 (one database, instance = continuous open period of the program, crash end at last seen, instance continues while locked, documented Apps rule, agent visible in Task Manager, names in the service PC's language, "today" = service PC's date, ~5 s / ~15 s timings) are implemented as written.

---

## 3. Security measures (ADR-011 §7, plan "Decision 1a") — all implemented

| Measure | Where | Unit-tested | Verified |
|---|---|---|---|
| Token: `CreateRestrictedToken` with `DISABLE_MAX_PRIVILEGE`, Administrators deny-only, `WRITE_RESTRICTED` (S-1-5-12), fallback per D-4; `TokenSessionId`; System integrity kept | `SessionAgentLauncher`, `WriteRestrictionPolicy` | policy | Michael (check A) |
| Process/thread DACL: SYSTEM full; Administrators `QUERY_LIMITED|TERMINATE|SYNCHRONIZE` (process) / `QUERY_LIMITED|TERMINATE|SYNCHRONIZE` (thread); protected; nobody else; owner SYSTEM | `AgentStartSpec` (`O:SYD:P(A;;GA;;;SY)(A;;0x101001;;;BA)`, thread `0x100801`), launcher | `AgentStartSpecTests` (parsed SDDL) | Michael (check B) |
| Minimal environment (`SystemRoot`, `windir`, `SystemDrive`, `TEMP`/`TMP` = `%SystemRoot%\Temp`, `PATH` = System32, `DOTNET_EnableDiagnostics=0`), absolute application name, fixed argument, current directory = install folder | `AgentStartSpec` | yes | — |
| `PROC_THREAD_ATTRIBUTE_HANDLE_LIST` with exactly the 3 pipe ends; `CREATE_NO_WINDOW|CREATE_UNICODE_ENVIRONMENT|EXTENDED_STARTUPINFO_PRESENT`; desktop `WinSta0\Default` | `SessionAgentLauncher` | — (Win32) | Michael (check A: the agent starts) |
| `SetDefaultDllDirectories(SYSTEM32|APPLICATION_DIR)` first; startup hooks disabled | `SessionAgentHost`; csproj `StartupHookSupport=false` | — | DEV: published `runtimeconfig.json` has `"System.StartupHookProvider.IsSupported": false` |
| No window, hooks, message loop, COM, shell, UIA, DPI API; only non-messaging window functions; no titles; ≤ 20 000 windows, `truncated` | `WindowEnumerator`, `SessionAgentHost` | rule/scanner | DEV: code review (the agent additionally opens processes with `PROCESS_QUERY_LIMITED_INFORMATION` to read the image name for the `explorer.exe` decision; no window function, no messages) |
| Agent diagnostic line (user, integrity, privileges, write-restricted) logged at Information | `AgentDiagnostics`, supervisor | supervisor (level) | DEV console: line appears; SYSTEM values: Michael (check A) |
| Reports untrusted: 64 KB lines, ≤ 1 000 entries, strict schema (unknown/duplicate properties, depth, types), PID → session, owner, creation time; latest-only queue per session | `BoundedLineReader`, `AgentProtocol`, `AgentReportProcessor`, `UsageEventQueue` | yes | — |
| Paths: only local fixed drives; UNC, `\\?\UNC\`, devices, network, removable never opened; final path checked | `ProgramPathPolicy`, `Win32AppMetadataSource` | policy | Michael (check C) |
| File access impersonating the session user (`WTSQueryUserToken` + `RunImpersonated`); as SYSTEM without token nothing is read | `Win32AppMetadataSource` | — | Michael (check C) |
| Untrusted files only via managed `VersionResourceReader` (RT_VERSION ≤ 64 KB, files ≤ 512 MB); version API only for protected paths with trusted owner (D-2); no icons/shell/COM/LoadLibrary | `VersionResourceReader`, `Win32AppMetadataSource` | reader (synthetic PE files, malformed structures) | DEV: "Windows-Explorer" (MUI) and "Visual Studio Code" (user-writable path) resolved |
| Manifest: `XmlReader` DTD prohibited, no resolver, ≤ 1 MB; only service-built `@{FullName?ms-resource://…}` to `SHLoadIndirectString`; raw `@…` never passed | `PackageManifestReader`, `Win32AppMetadataSource` | yes | DEV: "Rechner", "Snipping Tool", "Terminal", "Einstellungen" |
| Names sanitized (control/format/bidi, ≤ 256, no split surrogates) | `DisplayNameSanitizer` | yes | — |
| 5 s merge of reopened apps; ≤ 30 start/end entries per app and hour + summary | `UsageTracker`, `InstanceLogLimiter`, `UsageHistoryLog` | yes | Michael (check E) |
| Missing heartbeat 15 s / exit / protocol error → pause (no credit), restart 1/5/30 s, Warning; Error after 3 failed restarts | `SessionAgentSupervisor` | yes | Michael (check B, end the agent as admin) |
| EagleEye's own programs excluded by install path, not name; Explorer/frame host verified by path | `ProgramPathPolicy`, `AgentReportProcessor` | yes | Michael (check D) |
| Debug helpers only in Debug | `#if DEBUG` | Debug tests | DEV: Release `EagleEye.Service.dll` and `EagleEye.ParentApp.Core.dll` contain no `EAGLEEYE_DEV_*` strings and no `DevSessionAgentLauncher`/`DevLocalAccountSource`; Michael (check F) |

No measure was weakened or left out. Coding guidelines §12.4 notes: all rules above apply to `SessionAgent/`, `SessionAgentLauncher` and `Win32AppMetadataSource` and are met as listed.

---

## 4. Files

All paths relative to `02_Implementation/`.

**Shared** — new: `src/EagleEye.Shared/Models/{AppUsageDto, DayUsageDto, AccountUsageDto}.cs`; changed: `Contracts/IParentHub.cs`, `Contracts/IParentClientCallback.cs`, `README.md`.

**Service** — new: `src/EagleEye.Service/SessionAgent/*` (9 files), `Monitoring/*` (24 files), `Statistics/*` (14 files), `Data/{IUsageRepository, UsageRepository, UsageRecords}.cs`, `UserAccounts/DevLocalAccountSource.cs`, `DevSettings.cs`, `EndpointPorts.cs`; changed: `Program.cs`, `EagleEye.Service.csproj`, `Communication/{ParentHub, HubEndpointGuard}.cs`, `Data/ServiceDatabase.cs`, `UserAccounts/{IUserAccountService, UserAccountService}.cs`, `README.md`; removed: `Monitoring/.gitkeep`, `Statistics/.gitkeep`.

**ParentApp.Core** — new: `src/EagleEye.ParentApp.Core/Reports/{IAccountUsageModel, AccountUsageModel}.cs`, `ViewModels/{ReportsViewModel, DayUsageViewModel, AppUsageRowViewModel, UsageDuration}.cs`; changed: `Communication/{IParentHubClient, ParentHubClient, IParentHubGateway, ParentHubGateway, HostAddress}.cs`, `ViewModels/MainViewModel.cs`, `AppTexts.cs`, `Resources/AppTexts*.resx`, `README.md`.

**ParentApp** — new: `src/EagleEye.ParentApp/Views/ReportsView.xaml(.cs)`; changed: `Views/MainPage.xaml.cs`, `MauiProgram.cs`, `README.md`.

**Tests** — new: `tests/EagleEye.Shared.Tests/Models/UsageDtoTests.cs`; Service: `SessionAgent/*` (4), `Monitoring/*` (15 incl. `TestPeFiles`), `Statistics/*` (12 incl. `ManualTimeProvider`, `TestZones`), `Data/UsageRepositoryTests.cs`, `DevSettingsTests.cs`, `EnvironmentCollection.cs`; ParentApp: `Reports/AccountUsageModelTests.cs`, `ViewModels/{ReportsViewModelTests, DayUsageViewModelTests, AppUsageRowViewModelTests}.cs`, `Communication/{DevPortOffsetTests, ParentHubGatewayUsageTests, EnvironmentCollection}.cs`; changed: hub, filter, guard, repository, account-service, monitor, coordinator-free tests (`ParentHubTests`, `ParentHubUserAccountsTests`, `PairingAuthorizationHubFilterTests`, `HubEndpointGuardTests`, `AccountSelectionRepositoryTests`, `PairedDeviceRepositoryTests`, `UserAccountServiceTests`, `AccountInventoryMonitorTests`, `MainViewModelTests`, `HostAddressTests`).

**Build, installer, docs** — `Directory.Build.props` (0.4.0), `installer/windows/setup.iss`, this report, `docs/requirements/user-stories/US-004/user-story.md` (status).

---

## 5. Unit Test Coverage

coverlet, line and branch: **100 % / 100 %** for every class marked **unit** in the plan, and for the other new non-Win32 classes (`UsageAccumulator`, `UsageHistoryLog`, `WriteRestrictionPolicy`, `AgentStartSpec`, `EndpointPorts`, Debug switches). Not unit-tested, as the plan specifies: `SessionAgentHost`, `WindowEnumerator`, `AgentDiagnostics`, `WtsSessionSource`, `EagleEyeServiceLifetime`, `SessionAgentLauncher`, `PipeAgentProcess`, `DevSessionAgentLauncher`, `Win32ProcessInspector`, `Win32AppMetadataSource`, `UsageRecordingRegistration`, `ParentHubClient`, `Program`, the MAUI view, the installer.

| Area | Key scenarios |
|---|---|
| Rule / protocol | every Apps-rule case (visible/size, shell vs app cloak, owner/tool/appwindow, shell classes, Store frame with/without hosted PID, explorer folder vs other windows, `CabinetWClass` in other programs); 29 malformed protocol lines (missing/unknown/duplicate properties, wrong types, nesting, negative/zero PIDs, unknown kinds, host rules, > 1 000 apps, overflow) |
| Agent supervision | start only for logged-on controlled sessions; heartbeat 15 s → pause + restart after 1 s; exit with code; early exit reported; invalid report / over-long line / read failure → restart; Error once after 4 failures, retry every 30 s, reset after a report; untick stops; launch failure → back-off; stale agent's reports ignored; stderr diagnostics Information, others Warning |
| Names and paths | 36 path cases (trusted/untrusted, UNC, `\\?\UNC\`, devices, volumes, network/removable, traversal, ADS, slashes, NUL, length); version resource from the real service DLL and from synthetic PE files with 11 broken directory cases and broken `VS_VERSIONINFO` blocks; manifest application matching, DTD rejected, `@` rejected, size limit; resource references incl. `AppName/Text`; sanitizer incl. bidi and surrogate pairs; resolver order, fallback `mygame`, cache |
| Report processing | session/owner checks, gone and replaced processes, EagleEye by install path (renamed copy recorded), real vs fake frame host, real vs renamed explorer, one app per path, inspection once per PID + creation time, truncation warning once per hour |
| Accounting | AC-24 scenario (13 min open, 3 min locked → 595–605 s), parallel apps, same app in two sessions once, merge window 4 s vs 6 s, midnight split, DST 23 h/25 h days, DST at midnight, gap > 15 s not credited (logged once), suspend/resume, wall clock ±1 h and UTC+14 zone credit monotonic time only, fractions carried, end reasons, session reused by another user |
| Usage service / loop / repository | dangling instances closed at last seen (log), purge 90 days (day 89 kept), purge deleted accounts (cascade), start/credit/seen/end, one broadcast per changed day with increasing revision, no broadcast when idle, broadcast and persistence failures, query today first and within 90 days, midnight purge + empty today, lock/logoff/snapshot/untick/suspend through the loop, failing iteration continues, stop sequence; migration 3 on a real version-2 file keeps pairings and selections |
| Parent app | gateway forwarding of `DayUsageChanged`; model: loading → ready, selection/connection generation, revision per day, midnight, 90-day and empty-day trimming, failures; view models: states and texts de/en, controlled accounts only in US-003 order, first selected, rename keeps selection, untick → first / empty state, days today first, "Heute, 07.10.2026" / "Today, 10/7/2026", rows by seconds then name, HH:MM rounding (0, 59, 60, 3 599, 3 600, 86 400) |

---

## 6. Open Questions and Risks

1. **SYSTEM-only parts are verified by Michael** (§7): agent start in another session as SYSTEM, write-restricted token or fallback, process DACL, impersonation, Event Log/SCM events (lock, switch user, sleep). DEV ran the agent only as a normal child process in DEV's own session (Debug launcher).
2. **Write restriction (Q-9)**: unknown until check A. If the fallback is used, the log shows the Warning of D-4 and `write-restricted no` in the agent's diagnostic line.
3. **Apps rule vs Task Manager** (ADR-011 known limits, arc42 R-10): on this PC the agent's list matched the windowed processes (pwsh/Terminal, File Explorer, mstsc, VS Code, iCloud, a game, Steam, Calculator, Settings, Notepad, Character Map, System Information, Snipping Tool, the parent app).
4. **Names come from the service PC's language** (German Windows: "Editor", "Rechner", "Windows-Explorer", "Zeichentabelle", "Systeminformationen", "Einstellungen"). Windows Terminal hosts console programs: `cmd`/`pwsh` windows count as the app "Terminal".
5. **Lock, switch user and sleep** were not smoke-tested by DEV (locking would have locked Michael's workstation). Covered by unit tests; Michael tests them.
6. **AC-22 lag**: the Reports page follows the service's 5 s tick; the service's own value includes unpersisted seconds only after the next tick (≤ 5 s).
7. US-003's push ACs were not verified manually (Q-5); a Reports page that does not follow account changes may come from there.

---

## 7. Checks for Michael (admin / SYSTEM) — exact steps

Preparation (admin account): install `03_Delivery/windows/EagleEye-Setup-0.4.0.exe` over 0.3.1, then `EagleEye-ParentApp-Setup-0.4.0.exe` on the parent PC. Tick `kid1` in *Einstellungen*. Sign in as `kid1` (switch user). Service log: `%ProgramData%\EagleEye\logs\EagleEye.Service-NNN.log` (admin; e.g. `Get-Content -Wait` in an elevated PowerShell).

| Check | Steps | Expected |
|---|---|---|
| **A. Agent start and token (Q-9)** | After `kid1` signed in, in the log | Within ~5 s: `Session agent started in session <n> (process <pid>).`, `Usage recording started for account kid1 (S-1-5-21-…) in session <n>.`, then `Session agent in session <n>: diagnostics: user NT-AUTORITÄT\SYSTEM (S-1-5-18), integrity System, privileges SeChangeNotifyPrivilege, write-restricted yes`. If instead the Warning `Session agents exit at start with a write-restricted token; they run without the write restriction from now on (ADR-011 §7, Q-9).` appears, the next agent's line says `write-restricted no` (accepted fallback). Report which variant runs. |
| **B. Agent protected / restarted** | Admin PowerShell: `Get-Process EagleEye.Service \| Select Id, SessionId` (the agent has `kid1`'s session ID). As `kid1`: `taskkill /PID <agent pid> /F` and Task Manager → *Details* → *Task beenden* | As `kid1`: *Zugriff verweigert*; the agent keeps running. As admin: `taskkill /PID <agent pid> /F` works; the log shows `Session agent in session <n> exited with code 1; recording is paused, restarting in 00:00:01.` and a new agent within 1–30 s. |
| **C. Remote program, impersonation** | As `kid1`, run a copy of `C:\Windows\System32\charmap.exe` from a network share (`\\<other PC>\<share>\charmap.exe`) | Recorded as `charmap` (process name), not "Zeichentabelle"; no file-access error for the share in the log. |
| **D. Renamed EagleEye name** | As `kid1`: copy `C:\Windows\System32\charmap.exe` to `C:\Users\kid1\Desktop\EagleEye.TrayClient.exe`, start it | It **is** recorded (path in the log). The real tray client never appears. |
| **E. Flicker limit** | As `kid1`: open and close Notepad 40 times within a few minutes | At most 30 `App started/ended` lines for Editor in that hour, then (after the hour) one line `<n> further start/end entries of account kid1, Editor (…) were not logged in the last hour.` |
| **F. No Debug switches in Release** | Admin: `setx /M EAGLEEYE_DEV_WATCH_SID S-1-5-18`, `setx /M EAGLEEYE_DEV_PORT_OFFSET 10000`, `setx /M EAGLEEYE_DATA_DIR C:\Temp\x`; `Restart-Service EagleEyeService`; afterwards remove them again (`[Environment]::SetEnvironmentVariable('EAGLEEYE_DEV_WATCH_SID',$null,'Machine')`, same for the other two) and restart | Service still on 5443/5080 (`netstat -ano \| findstr "5443 5080"`), data still in `%ProgramData%\EagleEye`, agents still SYSTEM; nothing changes. |
| **G. Installer ends agents** | With `kid1` signed in, run `EagleEye-Setup-0.4.0.exe` again | Installation succeeds (no "file in use"); a new agent starts after the service restart. |

---

## 8. How to Test

### Artifacts and version

| Item | Value |
|---|---|
| Service + tray installer | `03_Delivery/windows/EagleEye-Setup-0.4.1.exe` (admin), built 2026-10-10 10:31, SHA-256 `2565b31e023f30bca2123b2540953742a2ea4ffb441f0f8b4fd0d9c05cd41fbe` (service and tray code unchanged since 0.4.0) |
| Parent app installer | `03_Delivery/windows/EagleEye-ParentApp-Setup-0.4.1.exe` (per user), built 2026-10-10 10:32, SHA-256 `274d82fc90b44249f3c29c18ad75269162d922edc64235d2bbbe87b1b74ed472` |
| Version | 0.4.1 (patch for ISSUE-007, see §10); the service reports `EagleEye_v0.4`. Upgrade over 0.3.1 keeps pairings and selections; migration 3 runs at the first start. The 0.4.0 installers are superseded. |

### UI texts as implemented (de / en)

| Where | German (default) | English |
|---|---|---|
| Menu entry and page header (AC-17) | Berichte | Reports |
| Account selection label | Konto | Account |
| Today's heading (AC-18) | Heute, 07.10.2026 | Today, 10/7/2026 (Windows short date of the user) |
| Older days | 06.10.2026 | 10/6/2026 |
| Column headers (bold) | App · Nutzung (HH:MM) | App · Usage (HH:MM) |
| Today without usage (AC-19) | Heute keine Nutzung aufgezeichnet | No usage recorded today |
| Not paired / not connected (AC-20) | Keine Daten verfügbar | No data available |
| No controlled account (AC-20) | Keine Konten unter Elternkontrolle. Konten unter Einstellungen auswählen. | No accounts under parental control. Select accounts under Settings. |
| While fetching (AC-20) | Wird geladen … | Loading … |

Layout: page header, then "Konto" with a drop-down of the accounts under parental control (names as in Settings, e.g. "Max Adler (max)"), then per day a heading bar (0.4.1, ISSUE-007: full width, accent colour background, see §10) and a two-column table (360 / 160 units, usage right-aligned; header row only when the day has rows). Usage is rounded down to minutes ("00:00" below one minute).

### Log entries (service log, Information unless noted)

```
Usage recording started for account kid1 (S-1-5-21-…-1003) in session 2.
Usage recording stopped for account kid1 (S-1-5-21-…-1003): monitoring stopped.
Session agent started in session 2 (process 5120).
Session agent in session 2: diagnostics: user NT-AUTORITÄT\SYSTEM (S-1-5-18), integrity System, privileges SeChangeNotifyPrivilege, write-restricted yes
Session agent in session 2 stopped.
New app for account kid1: Editor (notepad.exe, C:\Windows\System32\notepad.exe).
App started: account kid1, Editor (notepad.exe, C:\Windows\System32\notepad.exe), instance 12.
App ended: account kid1, Editor (notepad.exe, C:\Windows\System32\notepad.exe), instance 12, duration 00:10:03 (closed).
    reasons: closed · monitoring stopped · session ended · service stopping · service stopped unexpectedly
Usage accounting paused for 3600 s (sleep, suspension or delay); this time is not counted.
Purged usage data older than 2026-07-10: 3 daily entries, 5 history entries.
Purged all recorded data of deleted account S-1-5-21-…-1004: 4 apps, 17 history entries, 12 daily entries.
<n> further start/end entries of account kid1, Editor (C:\…\notepad.exe) were not logged in the last hour.
Warning: Session agent in session 2 exited with code 1; recording is paused, restarting in 00:00:01.
Warning: Session agent in session 2 no report for 15 s; recording is paused, restarting in 00:00:05.
Warning: Session agent in session 2 sent an invalid report; recording is paused, restarting in 00:00:01.
Error:   Usage recording for account kid1 (S-1-5-21-…) is not possible: the session agent keeps failing. Retrying every 30 s.
Warning: The session agent in session 2 found more than 20000 windows; its report is incomplete.
```

### What to observe

| Area | Observation |
|---|---|
| Timing | Recording starts ≤ ~5 s after ticking (agent start) plus ≤ 1 s scan; a new app appears on the Reports page with 00:00 within ~7 s; values follow within ~6 s. |
| Agent | Task Manager (admin) → *Details*: a second `EagleEye.Service.exe`, user *SYSTEM*, session of `kid1`; none for `kid2` or the admin session. |
| Database | `%ProgramData%\EagleEye\EagleEye.Service.db`: `AppRecords`, `AppInstances`, `DailyUsage` (seconds per app and day). |
| Other notes | The Manual Verification Notes of the plan apply unchanged (locking, switch user, sleep, clock change, service stop/kill, CPU). |

### DEV smoke check (Windows Developer Machine, 2026-10-08, non-elevated, next to Michael's running 0.3.1 service)

- Agent mode by hand (`EagleEye.Service.exe --session-agent` in DEV's session): JSON lines on stdout matching the windowed processes; diagnostic line on stderr; EOF on stdin ends it with code 0; any other argument → exit code 2.
- Console service (Debug, `EAGLEEYE_DATA_DIR=artifacts/smoke-us004`, `EAGLEEYE_DEV_WATCH_SID=<DEV's SID>`, `EAGLEEYE_DEV_PORT_OFFSET=10000` → 15080/15443) and the **Debug** parent app with the same offset, paired with `localhost` (code from Event Log 1000). Ticking DEV's account in Settings started the agent (`Usage recording started … in session 1`, diagnostic line `integrity Medium, privileges SeChangeNotifyPrivilege, write-restricted no` — DEV's own token, as expected in Debug).
- Recorded with names: EagleEye.ParentApp, Visual Studio Code, Windows-Explorer, Rechner, Editor, Terminal, Einstellungen, Systeminformationen, Zeichentabelle, Snipping Tool. The Reports page showed "Berichte", "Konto", "Heute, 08.10.2026", the table with "App | Nutzung (HH:MM)", and the values moved from 00:00 to 00:01 by themselves (push). Database seconds matched the elapsed time.
- Closing Notepad → `App ended: …, duration 00:01:31 (closed)` after ~5 s.
- Killing the console service → the agent exited by itself within 3 s; at restart all open instances were ended at their last seen time with `(service stopped unexpectedly)` and new instances started.
- Found and fixed during the smoke check: Snipping Tool was named "SnippingTool" (resource key `AppName/Text`); fixed in `PackageManifestReader` (now "Snipping Tool").
- **CPU (Step 10)**, 10 recorded apps, 60 s, 20 logical CPUs: service **0.016 %**, agent **0.005 %** of total CPU (together 0.021 %); agent memory 21 MB private / 38 MB working set; service 37 MB private (AC-25 bound: 2 %).
- **Not done** (needs admin/SYSTEM or would disturb Michael's session): installer, SYSTEM agent, lock/switch user/sleep, §7 checks.
- Clean-up: all processes started by DEV were closed (Explorer windows and Windows Terminal left open because they host Michael's shell and tabs). Michael's service, tray client and data were not touched; no pairing with his service. Left behind (git-ignored): `02_Implementation/artifacts/smoke-us004*` (data folder with a test certificate and a pairing of the Debug app), `artifacts/us004-cpu.txt`; the Debug parent app's data in `%LocalAppData%\EagleEye` (pairing with the console service only).

---

## 9. Machine(s) Used

| Step | Machine |
|---|---|
| Steps 0 to 11 | Windows Developer Machine |
| MacBook | Not used. `Shared` and `ParentApp.Core` contain no Windows APIs (the Debug port offset in `HostAddress` is plain environment access), so `build.ps1`/`test.ps1` keep working there. |

---

## 10. Patch 0.4.1 — ISSUE-007 (heading bars, Reports as start page)

| Item | Content |
|---|---|
| Change | (1) Reports day headings and (3) the Settings section headings *Darstellung*, *Serververbindung*, *Benutzerkonten auf dem EagleEye-PC* ("auf", not the mockup's "aud") use one shared style `SectionHeader`: a full-width bar in the platform accent colour with white or black text, padding 14 × 6, font size 16, regular weight as in the mockup. (2) A paired app starts on *Berichte* with the menu entry selected; an unpaired app starts on *Einstellungen* with the pairing flow as before. Page titles, *Konto* picker, column headers, rows, menu and status bar are unchanged. Details and root cause: `US-004/issues/ISSUE-007.md` §Resolution. |
| Accent colour | Windows: the user's accent colour (`UISettings.GetColorValue(UIColorType.Accent)`, `Platforms/Windows/WindowsAccentColor`), refreshed on `UISettings.ColorValuesChanged` and whenever the window is activated. Android: the system accent colour `system_accent1_600` (Material You). iOS / macOS: no platform reader yet, the app's primary colour `#1E7B3A` (also the default in `Colors.xaml`). The colours are app resources (`HeadingBarBackgroundColor`, `HeadingBarTextColor`) used as dynamic resources, so a change applies at once. They are the same in light and dark mode. |
| Text colour | `HeadingBarPalette.TextFor` (Core): white when its WCAG contrast with the bar is at least 4.5:1, otherwise black (black then has at least 4.6:1). Windows default blue `#0078D4` → white (4.5:1, as in the mockup); light accent colours such as yellow `#FFB900` → black. |
| Code | Core: `Appearance/{HeadingBarPalette, RgbColor}.cs`, `ViewModels/MainViewModel.cs`. MAUI head: `Services/HeadingBarColorService.cs`, `Platforms/Windows/WindowsAccentColor.cs`, `Platforms/Android/AndroidAccentColor.cs`, `Resources/Styles/{Styles, Colors}.xaml`, `Views/{ReportsView, SettingsView}.xaml` (outer stack fills the content area instead of max. 760 units, so the bars reach the right edge), `App.xaml.cs`, `MauiProgram.cs`, READMEs; `Directory.Build.props` 0.4.1. Service, Shared and tray client code unchanged (installer 0.4.1 differs from 0.4.0 only in the version). |
| Tests | +19 (ParentApp 545): `Appearance/HeadingBarPaletteTests` (fallback, Windows blue, dark and light accents, the 4.5 boundary on both sides, luminance, contrast ratio), `MainViewModelTests` (paired → Reports with one change notification, not paired → Settings, paired after the menu wrote back Settings → Reports). `HeadingBarPalette`, `RgbColor`, `MainViewModel` at 100 % line and branch coverage. `build.ps1` 0 warnings (Windows + Android), `test.ps1` 1 513 passed. |
| Installers | `03_Delivery/windows/EagleEye-Setup-0.4.1.exe` (10:31, `2565b31e…cd41fbe`), `03_Delivery/windows/EagleEye-ParentApp-Setup-0.4.1.exe` (10:32, `274d82fc…b74ed472`); full hashes in §8 |
| Deviation | Two layout points beyond the issue text: the outer page stacks on both pages now fill the content area (needed for the full-width bar of the mockup); long texts (instruction, error text) can therefore use the full width. The headings lost their bold weight to match the mockup; switching back to bold is one setter in `Styles.xaml`. |
| Smoke check | Windows Developer Machine (`ZOCK-O-MAT-V3`, 150 % scaling, dark mode, orange-red accent colour), Michael's installed service 0.4.0 not touched. (a) Debug parent app paired with the Debug console service on the offset ports (as in §8, `EAGLEEYE_DEV_PORT_OFFSET=10000`): opens on *Berichte*, menu entry selected, day bars "Heute, 10.10.2026" / "08.10.2026" full width in the accent colour with white text; *Einstellungen* shows the three bars. The first attempt still opened on *Einstellungen*: the menu's two-way binding writes the first entry back when it loads, and a guard "keep the user's choice" took that as a user choice; the guard was removed before the commit. (b) `EagleEye-ParentApp-Setup-0.4.1.exe` installed per user (file version 0.4.1.0, product version `0.4.1+4327f60…`), started with the Debug app data set aside (unpaired: *Einstellungen*, host entry, bars shown), uninstalled; the Debug app data was put back. Not checked by DEV: light mode, changing the accent colour while the app runs, Android, LEOSERV. |

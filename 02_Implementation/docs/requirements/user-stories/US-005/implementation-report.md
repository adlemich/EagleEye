# Implementation Report: US-005 — Break Times (Ruhezeiten)

**Author**: DEV
**Date**: 2026-10-10
**Branch**: `feature/US-005-break-times`
**Plan**: `02_Implementation/docs/requirements/user-stories/US-005/implementation-plan.md` (approved 2026-10-10 with Q-1 to Q-9), ADR-013, ADR-014, amendments of ADR-003/005/006/011/012, coding guidelines §10.1, §12.2, §12.4, §16.1
**Machine(s) used**: Windows Developer Machine only (see §9)
**Story status**: Implemented

---

## 1. Summary

US-005 is implemented as planned in Steps 0 to 13, version **0.5.0**. The parent edits break times and a display text per controlled account on the new page **Regeln**; the service stores them (migration 4), broadcasts every change and enforces them at app start: a start (= creation of the app's first process, Q-1) inside a break time is removed from usage accounting before the tracker, the kid's tray client shows the display text in a topmost dialog, the session agent posts `WM_CLOSE`, and after 20 s the service terminates the recomputed kill set. Every blocked start is logged and stored in `BlockedStarts`; time-zone and clock changes are logged as Warnings and stored in `TimeChangeFindings`.

| Component | What was built |
|---|---|
| `EagleEye.Shared` | `IParentHub`: `GetAccountRules` + 6 field-level writes; `IParentClientCallback.OnAccountRulesChanged`; `ITrayClientCallback.ShowBreakTimeMessage` (client result); DTOs `AccountRulesDto`, `BreakTimeEntryDto`, `BreakTimeMessageDto`; enums `BreakTimeDays`, `BreakTimeBoundary`, `KidMessageResult`; `Constants/BreakTimeRules` (limits, defaults, default text of AC-15, line-break normalization, display-text validity incl. emojis, `IsValidTimes`, `IsValidMinute`, `FormatMinute`) |
| Service — `Data/` | Migration 4 (`BreakTimeEntries`, `AccountDisplayTexts`, `BlockedStarts`, `TimeChangeFindings`), `BreakTimeRepository`, `BlockedStartRepository`, `TimeChangeFindingRepository`, `SqlValues` |
| Service — `Rules/` | `BreakTimeService` (state owner `AccountRules:{sid}`, one lock, immutable `RulesSnapshot` read lock-free by the gate, validation against the stored entry, purger), `AccountRules`, `BreakTimeSchedule` (start inclusive, end exclusive, 23:59 = 24:00, local wall clock), `BreakTimeChangeLog`, `AccountRulesBroadcaster`, exceptions |
| Service — `SessionAgent/` | Close command on stdin (`AgentCommandReader`: strict JSON, ≤ 4 KB, 1–64 targets, positive PIDs/creation times) and answer on stdout (`AgentProtocol`); `WindowCloseSelection` (pure: app windows of the targets per the "Apps" rule, Store frame for Store apps, never File Explorer windows) and `WindowCloser` (`PostMessageW(WM_CLOSE)` after the creation-time check); `SessionAgentHost` handles commands between scans |
| Service — `Monitoring/` | `IAgentProcess.WriteLineAsync`; `SessionAgentSupervisor.RequestCloseAsync` (2 s answer timeout, answer lines count as heartbeat, agent run id per start); `AgentReportProcessor` passes `(pid, creation time)` and the agent run |
| Service — `Enforcement/` | `BreakTimeGate`, `BlockedStartRunner`, `BlockedAppCloser`, `KillSetBuilder`, `EnforcementIgnoreList`, `Win32ProcessTable`, `Win32ProcessTerminator`, `BlockedStartLog` (limiter, Q-4), `TimeChangeMonitor`, `EnforcementHistory`, `OpenAppsView`, `BreakTimeEnforcement` (facade for the loop), `EnforcementRegistration` |
| Service — other | `ParentHub` (7 methods, safe error texts), `TrayHub` (session binding), `TrayConnectionRegistry`, `Win32TrayClientIdentifier` (`GetExtendedTcpTable` IPv4/IPv6 + installed path), `KidMessenger` (client result, 3 s); `UsageTracker.IsOpen/OpenPaths`; `UsageAccountingLoop` (gate before tracker, `TimeZoneInfo.ClearCachedData()` + findings every tick, history purge at midnight, runner stop); `UserAccountService` calls every `IAccountDataPurger`; `Program` start order (… → rules → history) |
| `EagleEye.TrayClient` | Handler for `ShowBreakTimeMessage`, `BreakTimeMessagePresenter`, `BreakTimeMessageDialog`, `ForegroundHelper`, text `BreakMessageTitle` |
| `EagleEye.ParentApp.Core` | Hub calls + gateway forwarding; `Rules/AccountRulesModel`, `TimeOfDayText`, `BreakTimeEditRules`; `RulesViewModel`, `BreakTimeRowViewModel`, `ControlledAccountSelection` (extracted from `ReportsViewModel`, no behaviour change); menu Settings, Rules, Reports; 20 texts de/en |
| `EagleEye.ParentApp` | `Views/RulesView`, `MainPage` (three pages, flush when leaving Rules), `MauiProgram` (DI, WinUI `CheckBox` `MinWidth = 0`), `trash_light.svg` / `trash_dark.svg` |
| Version / installers | `Directory.Build.props` 0.5.0; both installers built with the unchanged `package-windows.ps1` |

---

## 2. Deviations from the Implementation Plan

| # | Plan | Implemented | Why |
|---|---|---|---|
| D-1 | Loop calls gate, monitor, runner and purge directly; runner uses supervisor, process table, terminator, ignore list, messenger, repository, log | Two small extra classes: `BreakTimeEnforcement` (`IBreakTimeEnforcement`, facade the loop calls) and `BlockedAppCloser` (agent close request + kill set + handles); `OpenAppsView` publishes the account's open paths; `EnforcementHistory` holds retention and purge of both history tables | Keeps the loop at 9 and the runner at 5 dependencies. `UsageTracker` is not thread-safe (loop only), so the runner's kill set reads the open paths from `OpenAppsView`, which the loop republishes after every event. One purger for both history tables instead of two. |
| D-2 | `EnforcementIgnoreList`: listed programs by full path under `%SystemRoot%` | System32 programs and `explorer.exe` by exact path; the shell hosts `ShellExperienceHost`, `StartMenuExperienceHost`, `SearchHost`, `TextInputHost`, `LockApp` by file name **below `%SystemRoot%\SystemApps\`** (any package folder; paths with `..` rejected) | Their package folder names differ between Windows builds (on this PC `TextInputHost.exe` exists in two folders, one under `SystemApps\SxS\`). `SystemApps` is writable by TrustedInstaller only. |
| D-3 | Gate: "only if the earliest of them lies in a break does the gate read the process table to find an older process of the same path (which would make it not a start)" | The start time is the **oldest** process of the path in the session (owned by the account); the decision is made again for that time: outside every break → allowed; inside a break (possibly another entry) → blocked with that entry | Literal application of Q-1 (start = creation of the first process, ADR-013 §3). The table is still read only when the reported processes lie in a break. |
| D-4 | Close sequence waits on process handles | Polls the opened handles every 250 ms (`TimeProvider` delay); seconds rounded to 0.1 s | Deterministic unit tests with the fake clock. The 20 s force step is exact (the last wait is shortened). |
| D-5 | Smoke check with a Debug tray client and the Debug parent app (US-004 setup) | Two more **Debug-only** switches: the tray client reads `EAGLEEYE_DEV_PORT_OFFSET`; the parent app reads `EAGLEEYE_DEV_APP_DATA_DIR` | Michael's installed service holds 5080/5443 and his tray client runs in the same session: without the offset the Debug tray would talk to his service. The app-data switch keeps the smoke pairing away from the installed parent app's data (`%LocalAppData%\EagleEye`) instead of moving that folder aside as in US-004. Release DLLs contain no `EAGLEEYE_DEV` string (checked, §5). |
| D-6 | Tray texts `BreakMessageTitle`, `OkButton` | `BreakMessageTitle` new; the existing key `Ok` ("OK" in both languages) reused | Same text already exists. |
| D-7 | Dialog text drawn by GDI, "Segoe UI Emoji" only if emojis show as boxes (ADR-014 §3) | The text label always uses "Segoe UI Emoji" | Smoke check: with the dialog font the emojis were boxes. "Segoe UI Emoji" also contains the Latin letters; emojis are monochrome (AC-31 accepts). |
| D-8 | `SetDisplayText`: empty or white space → default | Also a text equal to the default text is stored as "default" (`IsDefaultDisplayText` stays true) | Keeps the flag meaningful when the parent types the default text again. |
| D-9 | Log template of AC-12 | Ends with ` (request {RequestId}, revision {Revision}).` | Coding guidelines §7.5 (log requestId and revision). |
| D-10 | — | `TimeChangeMonitor.Rebaseline()` on suspend and resume | A clock jump across sleep is not a finding (Decision 8: suspend intervals are not judged). |
| D-11 | Tray csproj unchanged | `AllowUnsafeBlocks=true` | Required by the `LibraryImport` source generator (`ForegroundHelper`). |
| D-12 | `ShowBreakTimeMessage` via the typed hub context, fallback `ISingleClientProxy.InvokeAsync` | Fallback used: `IHubContext<TrayHub>` + `InvokeAsync<KidMessageResult>(…, CancellationToken)` | The typed interface has no cancellation token; with the token the 3 s timeout also ends the pending invocation. |

The plan's own story interpretations D-1 to D-15 are implemented as written.

---

## 3. Security notes (coding guidelines §12.4, changes to `SessionAgent/` and Win32 code)

| Rule | How it is met |
|---|---|
| Agent: no window, no message loop, no hooks, no COM/shell/UIA; only non-messaging window functions — except `PostMessageW(hwnd, WM_CLOSE, 0, 0)` | `WindowCloser` is the only caller of `PostMessageW`, with `WM_CLOSE` only, to windows that `AppWindowRule` classifies as app windows of a target (Store apps: the `ApplicationFrameWindow` hosting the target; File Explorer windows never). Asynchronous: a hung app cannot block the agent. No `SendMessage*`. |
| Agent opens processes with `PROCESS_QUERY_LIMITED_INFORMATION` only; never terminates | Creation-time check with `OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION)` + `GetProcessTimes`; a target whose PID was reused is skipped (checked by hand: wrong creation time → `missing 1`, Notepad stays open). |
| Service → agent commands built only by the service, parsed strictly | `AgentProtocol.SerializeCloseCommand` from validated process facts; `AgentCommandReader` rejects unknown/duplicate properties, wrong types, > 64 or 0 targets, non-positive values, lines > 4 KB (`BoundedLineReader` on stdin; an over-long line closes the channel and the agent stops, the supervisor restarts it). Invalid lines are reported on stderr and ignored. |
| Terminating kid processes (service only) | `Win32ProcessTerminator`: `OpenProcess(SYNCHRONIZE \| QUERY_LIMITED \| TERMINATE)` by PID, creation time verified on that handle, `TerminateProcess` on the same handle. Kill set (`KillSetBuilder`): same session and owner only; never EagleEye's own programs (install path), the ignore list (incl. real Explorer and ApplicationFrameHost) or other open apps of the account and their descendants; descendants only if created after their parent. No `Process.Kill(entireProcessTree)`. |
| Tray identity | `Win32TrayClientIdentifier`: the TCP owner PID of the loopback connection, facts read with `PROCESS_QUERY_LIMITED_INFORMATION`, accepted only for the installed `TrayClient\EagleEye.TrayClient.exe` (full path, case-insensitive) in a user session; Debug builds accept the file name (`#if DEBUG`). Unverified connections never get kid messages. |
| Debug-only switches | All inside `#if DEBUG` (service, tray, parent app); Release DLLs checked (§5). |

---

## 4. Files

All paths relative to `02_Implementation/`.

**Shared** — new: `src/EagleEye.Shared/Constants/BreakTimeRules.cs`, `Models/{AccountRulesDto, BreakTimeEntryDto, BreakTimeDays, BreakTimeBoundary, BreakTimeMessageDto, KidMessageResult}.cs`; changed: `Contracts/{IParentHub, IParentClientCallback, ITrayClientCallback}.cs`, `README.md`.

**Service** — new: `src/EagleEye.Service/Rules/*` (9 files), `Enforcement/*` (19 files), `Data/{IBreakTimeRepository, BreakTimeRepository, IBlockedStartRepository, BlockedStartRepository, ITimeChangeFindingRepository, TimeChangeFindingRepository, BreakTimeRecords, SqlValues}.cs`, `SessionAgent/{AgentCommands, AgentCommandReader, WindowCloseSelection, WindowCloser}.cs`, `Communication/{IKidMessenger, KidMessenger, TrayConnectionRegistry, Win32TrayClientIdentifier}.cs`; changed: `Program.cs`, `README.md`, `Communication/{ParentHub, TrayHub}.cs`, `Data/ServiceDatabase.cs`, `Monitoring/{AgentReportProcessor, DevSessionAgentLauncher, IAgentLauncher, PipeAgentProcess, SessionAgentSupervisor}.cs`, `SessionAgent/{AgentProtocol, SessionAgentHost}.cs`, `Statistics/{UsageAccountingLoop, UsageEvents, UsageRecordingRegistration, UsageTracker}.cs`, `UserAccounts/UserAccountService.cs`.

**TrayClient** — new: `src/EagleEye.TrayClient/UI/{BreakTimeMessagePresenter, BreakTimeMessageDialog, ForegroundHelper}.cs`; changed: `Communication/{IServiceConnection, ServiceConnection}.cs`, `UI/{TrayApplicationContext, TrayTexts}.cs`, `Resources/TrayTexts*.resx`, `Program.cs`, `EagleEye.TrayClient.csproj`, `README.md`.

**ParentApp.Core** — new: `src/EagleEye.ParentApp.Core/Rules/{IAccountRulesModel, AccountRulesModel, TimeOfDayText, BreakTimeEditRules}.cs`, `ViewModels/{RulesViewModel, BreakTimeRowViewModel, ControlledAccountSelection}.cs`; changed: `Communication/{IParentHubClient, ParentHubClient, IParentHubGateway, ParentHubGateway}.cs`, `ViewModels/{MainViewModel, ReportsViewModel}.cs`, `AppTexts.cs`, `Resources/AppTexts*.resx`, `README.md`.

**ParentApp** — new: `src/EagleEye.ParentApp/Views/RulesView.xaml(.cs)`, `Resources/Images/{trash_light, trash_dark}.svg`; changed: `Views/MainPage.xaml.cs`, `MauiProgram.cs`, `Services/MauiAppDataPaths.cs`, `EagleEye.ParentApp.csproj`, `README.md`.

**Tests** — new: Shared `Constants/BreakTimeRulesTests`, `Models/BreakTimeDtoTests`; Service `Rules/*` (5), `Enforcement/*` (11 incl. `EnforcementFakes`), `Data/{BreakTimeRepositoryTests, BlockedStartRepositoryTests, TimeChangeFindingRepositoryTests}`, `SessionAgent/{AgentCommandTests, WindowCloseSelectionTests}`, `Communication/{ParentHubRulesTests, KidMessengerTests, TrayConnectionRegistryTests}`; TrayClient `UI/BreakTimeMessagePresenterTests`; ParentApp `Rules/{AccountRulesModelTests, TimeOfDayTextTests, BreakTimeEditRulesTests}`, `ViewModels/{RulesViewModelTests, BreakTimeRowViewModelTests, ControlledAccountSelectionTests}`, `Communication/ParentHubGatewayRulesTests`; changed: hub, filter, tray hub, repository (schema version 4), account service, supervisor, report processor, tracker, loop, event queue, main view model and tray text tests.

**Build, docs** — `Directory.Build.props` (0.5.0), this report, `docs/requirements/user-stories/US-005/user-story.md` (status).

---

## 5. Build, Unit Tests and Coverage

BUILD_PLACEHOLDER

---

## 6. Open Questions and Risks

RISKS_PLACEHOLDER

---

## 7. Checks for Michael (admin / SYSTEM / real kid session)

CHECKS_PLACEHOLDER

---

## 8. How to Test

HOWTO_PLACEHOLDER

---

## 9. Machine(s) Used

| Step | Machine |
|---|---|
| Steps 0 to 13 | Windows Developer Machine |
| MacBook | Not used. `Shared` and `ParentApp.Core` contain no Windows APIs (all Win32 code is in the Service, the TrayClient and the MAUI head's `#if WINDOWS` mapping), so `build.ps1` / `test.ps1` keep working there after `git pull`. |

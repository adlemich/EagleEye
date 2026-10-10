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
| `EagleEye.ParentApp.Core` | Hub calls + gateway forwarding; `Rules/AccountRulesModel`, `TimeOfDayText`, `BreakTimeEditRules`; `RulesViewModel`, `BreakTimeRowViewModel`, `ControlledAccountSelection` (extracted from `ReportsViewModel`; fixes an existing picker crash, D-13); menu Settings, Rules, Reports; 20 texts de/en |
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
| D-13 | `ControlledAccountSelection` extracted with "no behaviour change for Reports" | `ControlledAccountSelection.Update` clears the selection **before** the selected account is removed from the picker; used by Reports and Rules | Smoke check: the Debug parent app crashed (APPCRASH in `combase.dll`, E_BOUNDS) when the service restarted while the page showed the second or a later account: on the lost connection all accounts are removed from the picker while its selected item is one of them, and the WinUI ComboBox fails fast. Reproduced on **Reports** too, so it is an existing US-004 defect that the Rules page inherited; bisected by hand (not the rows, not the editor). After the fix both pages survive a service restart (incl. focus in a time field and a minimized window). Side effect: after a reconnect the picker shows the first account again. |

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

**Build** (`scripts/build.ps1`, Debug, final state): 0 warnings, 0 errors for Shared, Service, TrayClient, ParentApp.Core, ParentApp Windows and ParentApp **Android**.

**Unit tests** (`scripts/test.ps1`, final state): **2 107 passed, 0 failed** — Shared 203, Service 1 120, TrayClient 50, ParentApp 734 (0.4.1: 1 513).

**Coverage** (coverlet, line and branch): **100 % / 100 %** for every new or changed class marked **unit** in the plan and for the other new non-Win32 classes (`BreakTimeRules`, DTOs, `BreakTimeService`, `RulesSnapshot`, `AccountRules`, `BreakTimeSchedule`, `BreakTimeChangeLog`, `AccountRulesBroadcaster`, the three repositories, `ServiceDatabase`, `BreakTimeGate`, `BlockedStartRunner` incl. its sequence, `BlockedAppCloser`, `KillSetBuilder`, `EnforcementIgnoreList`, `BlockedStartLog`, `TimeChangeMonitor`, `EnforcementHistory`, `OpenAppsView`, `BreakTimeEnforcement`, `AgentProtocol`, `AgentCommandReader`, `WindowCloseSelection`, `SessionAgentSupervisor`, `AgentReportProcessor`, `UsageTracker`, `UsageAccountingLoop`, `UserAccountService`, `ParentHub`, `TrayHub`, `TrayConnectionRegistry`, `KidMessenger`, `BreakTimeMessagePresenter`, `TrayTexts`, `ParentHubGateway`, `AccountRulesModel`, `TimeOfDayText`, `BreakTimeEditRules`, `RulesViewModel`, `BreakTimeRowViewModel`, `ControlledAccountSelection`, `ReportsViewModel`, `MainViewModel`). As in US-004, coverlet lists a few compiler-generated async-lambda classes with an extra line without line number; they have no source line. Not unit-tested, as the plan specifies: `WindowCloser`, `SessionAgentHost`, `PipeAgentProcess`, `Win32ProcessTable`, `Win32ProcessTerminator`, `Win32TrayClientIdentifier`, `BreakTimeMessageDialog`, `ForegroundHelper`, `ServiceConnection`, `ParentHubClient`, `EnforcementRegistration`, `Program`, the MAUI view, the installers.

| Area | Key scenarios |
|---|---|
| Rules (service) | every AC-21 example; 23:59 = 24:00 (23:59:30 in effect, Tue 00:00 not); 23:58 end; start inclusive; each weekday; overlap → first entry; DST spring (skipped hour never), autumn (both passes); zone switch; add (defaults, order, 20 limit), delete, on/off, times (validated against the stored other boundary, concurrent race), days (last day refused), display text (normalized, emojis/ZWJ kept, empty/white space/default → default, 500 limit, CR LF counts as one); one revision + broadcast + log per write incl. no-op; broadcast failure → warning; storage failure → snapshot unchanged; concurrent field writes all kept; unknown account; purge of deleted accounts |
| Hub | each method: delegation with the device name, invalid SID/request id/minute/boundary/day/text → "Invalid request.", exceptions → safe texts (unknown account, entry no longer exists, end > start, ≥ 1 day, too many, save failed logged), `HubException` passed through |
| Data | schema version 4; migration 4 on a real version-3 file keeps pairings, selections, usage; CRUD; schema checks (end > start, days 1–127, minutes, kinds); history insert/complete/dangling; 90-day purge (day 89 kept, day 90 purged); purge by SID (findings without account kept) |
| Agent command | serialize/parse round trip; 17 malformed commands (unknown cmd, negative id, 0 or 65 targets, null target, pid/created ≤ 0, missing/unknown/duplicate properties, report line) and 9 malformed answers rejected; answer vs report told apart; window selection (app windows only, Store frame, never File Explorer); supervisor: command written and correlated, wrong id ignored, timeout → null, no agent / no targets / write failure → null, cancellation, answer = heartbeat, invalid answer → restart, new run id per agent start |
| Gate | no active entry → unchanged, nothing read; first process in break → blocked and removed, trigger app start / found at agent start (first report and after agent restart); first process 19:59:59 with window at 20:00:30 → allowed; older process in the table → allowed, or blocked with another entry; table failure → reported processes decide; open app → not a start; being closed → new processes join the sequence once; after the sequence → blocked again; entry switched off while blocked; start in a break detected after the break → blocked; Explorer and ApplicationFrameHost never; other account; session reused; begin/end/switch logs |
| Runner / kill set | graceful 0 s and after some seconds; force at exactly 20 s (also with uneven polling); recompute adds new processes; agent not answering → force; 3 rounds then Error; Win32 error warnings; added processes (close command, no own record, no second message, reused PID); nothing running → graceful; message states; history insert/complete failures; service stop → immediate termination; stop timeout; parallel limit 64 with queueing; kill set: path roots in the session only, descendants incl. grandchildren, PID reuse, other session/owner, exempt programs and their children, other open apps and their descendants, extra roots |
| Time changes | first tick baseline; zone change → Warning + finding with the session in use (one / none / several); DST switched off → finding; regular DST transition → nothing; clock jump > 30 s → finding, ±30 s → nothing; gap > 15 s and suspend/resume → not judged; storage failure → warning; cancellation propagates |
| Tray | first → Shown, while open → AlreadyOpen, after OK → Shown, 20 parallel requests → one Shown; `\n` → `\r\n` only; texts de/en |
| Parent app | gateway forwarding; model: select/fetch/stale results/reconnect/fetch failure/disconnect/broadcast revision and account filter/writes confirmed by own broadcast or later broadcast or old ack/rejection/timeout + refetch/disconnect/not ready/not connected; parser (13 accepted, 17 rejected inputs incl. "24:00", "12:60", "abc", "", "7:5", Arabic digits); edit rules; rows (pending and typed fields never overwritten, refused times/days with messages, normalization, deletes); page (states and texts de/en, accounts, rows merged by id, 20 limit, AC-18 message and its clearing, display text commit/normalize/default/typing kept, flush on account switch for the old account); selection cleared before removal |

---

## 6. Open Questions and Risks

1. **SYSTEM-only parts are not verified by DEV** (§7): the agent's `PostMessageW(WM_CLOSE)` under the real SYSTEM token (write-restricted or fallback, Q-8), `TerminateProcess` from session 0 into a kid session, the tray identification for the installed tray client in a kid session, the dialog in another session. DEV ran everything as the own (admin, non-elevated) user in session 1 with the Debug launcher.
2. **Picker crash (D-13)** was an existing US-004 defect. If TES has seen the Reports page vanish on a service restart in earlier runs, this was the cause. After a reconnect the page now shows the first account again.
3. **Debug tray blocked itself** in the smoke check: the Debug tray client runs from the build output, so it is not an EagleEye program by install path and its dialog window counted as an app start (process created inside the break) → closed and terminated after 20 s. Not possible with the installed tray client (excluded by `<install folder>\TrayClient\`). The same rule applies by design to any program whose first process was created in a break and that opens its first window later (Q-1, ADR-013 §3).
4. **Account picker on opening** (AC-2): the Rules page view model lives for the app's lifetime (as Reports); "first account selected" holds when the page is first shown after the accounts are loaded and after a reconnect, but a selection the parent made stays while the app runs.
5. **Focus of the kid's dialog**: in the smoke check the dialog became the active window at once (title bar in the accent colour), with DEV's own console in the foreground. Best effort under Windows' foreground rules (ADR-014 §3); TES checks it with a full-screen app in the kid session.
6. **Emojis** are monochrome (Segoe UI Emoji via GDI, AC-31 accepts). Text in the dialog uses that font throughout (D-7); it looks slightly different from Segoe UI.
7. **Time-zone change while the service is stopped** is not reported (D-15 of the plan). No time-zone or clock change was made on this PC (plan Step 11.5).
8. **Window evasion and time-zone bypass** stay accepted risks (Q-2, Q-3; arc42 R-13, R-14).

---

## 7. Checks for Michael (admin / SYSTEM / real kid session)

Preparation (admin): install `03_Delivery/windows/EagleEye-Setup-0.5.0.exe` over 0.4.x, then `EagleEye-ParentApp-Setup-0.5.0.exe` on both parent PCs. Service log: `%ProgramData%\EagleEye\logs\EagleEye.Service-NNN.log` (elevated, e.g. `Get-Content -Wait`).

| Check | Steps | Expected |
|---|---|---|
| **A. Agent posts WM_CLOSE as SYSTEM (Q-8)** | As `kid1` in a break (entry on, covering now): start Notepad without text | Within ~2 s the dialog; Notepad closes; log `Blocked start ended: … closed gracefully after 0.x s; message shown.` Also run US-004 TC-004-23 (agent diagnostic line `write-restricted yes/no`) and report the variant. If the outcome is "terminated by force after 20 s" for Notepad, the agent's post did not work under the SYSTEM token. |
| **B. Termination across sessions** | As `kid1` in a break: start an app that refuses to close (Notepad with typed text, ignore the "save?" prompt) | After 20 s the app is gone, `terminated by force after 20 s (2x.x s until gone)`; no Warning `could not be terminated`. |
| **C. Tray identity** | After `kid1` signs in | `Tray client connected: … (session N, verified).` for the installed tray client. End the tray client as `kid1` and start Notepad in a break → `message not shown (tray client not connected)`. |
| **D. Two sessions (AC-34)** | `kid1` in a break, switch user to `kid2` (no break) | `kid2` apps start; the dialog appears only in `kid1`'s session. |
| **E. Release build has no Debug switches** | — | Checked by DEV: the published `EagleEye.Service.dll`, `EagleEye.TrayClient.dll`, `EagleEye.ParentApp.dll`, `EagleEye.ParentApp.Core.dll` contain no `EAGLEEYE_DEV` string. |

---

## 8. How to Test

### Artifacts and version

| Item | Value |
|---|---|
| Service + tray installer | `03_Delivery/windows/EagleEye-Setup-0.5.0.exe` (admin), built 2026-10-10 22:47, SHA-256 `83bf5c55f924f46137d4a006692bd8e4e5fd1f449c0fb8912c5fe43924151830` |
| Parent app installer | `03_Delivery/windows/EagleEye-ParentApp-Setup-0.5.0.exe` (per user), built 2026-10-10 22:48, SHA-256 `a03245d558bbce88be72f5a203a19a08608744b5c3144eb2534c12644cb217a1` |
| Version | 0.5.0 (service reports `EagleEye_v0.5`). Upgrade over 0.4.x keeps pairings, selections and usage; migration 4 runs at the first start. Install the parent app 0.5.0 on both parent PCs (0.4.x has no Rules page). |

### UI texts as implemented (de / en)

| Where | German (default) | English |
|---|---|---|
| Menu (AC-1) | Einstellungen · Regeln · Berichte | Settings · Rules · Reports |
| Page, account, heading bar, button (AC-2) | Regeln · Konto · Ruhezeiten · Eintrag hinzufügen | Rules · Account · Break times · Add new entry |
| Columns (AC-3) | An/Aus · Start-Zeit · End-Zeit · MO DI MI DO FR SA SO | On/Off · Start time · End time · Mo Tu We Th Fr Sa Su |
| No entries (AC-3) | Keine Ruhezeiten festgelegt. | No break times defined. |
| Display text label (AC-2) | Anzeige Text: | Display text: |
| Trash button (screen reader, tooltip) | Eintrag löschen | Delete entry |
| AC-9 / AC-10 / AC-11 | Bitte eine Uhrzeit zwischen 00:00 und 23:59 eingeben. · Die End-Zeit muss nach der Start-Zeit liegen. · Mindestens ein Tag muss ausgewählt sein. | Enter a time between 00:00 and 23:59. · The end time must be later than the start time. · Select at least one day. |
| AC-18 | Die Änderung konnte nicht gespeichert werden. Bitte erneut versuchen. | The change could not be saved. Please try again. |
| AC-6 | Keine Daten verfügbar · Keine Konten unter Elternkontrolle. Konten unter Einstellungen auswählen. · Wird geladen … | No data available · No accounts under parental control. Select accounts under Settings. · Loading … |
| Kid's dialog (AC-30) | Title "EagleEye", button "OK" (both languages) | |

Messages appear in one line below the table; the next successful change clears it. Accepted time inputs: `7`, `07`, `7:30`, `07:30`, `730`, `0730`, `20.00` (shown as HH:MM). The display text box stops at 500 UTF-16 code units (an emoji counts 2 or more, Q-7).

### Log entries (service log, Information unless noted)

```
Break times loaded: 1 entries (0 on) of 1 accounts, 1 changed display texts.
Break times of account kid1 (S-1-5-21-…) changed by Dad's laptop: entry 3 added (off, 20:00–23:59, Mo Tu We Th Fr Sa Su) (request …, revision 2).
   … entry 3 changed: on, 07:30–20:00, Sa (…) · entry 3 deleted (…) · display text changed (…) · display text reset to the default (…)
Break time of account kid1 began: 22:45–23:59 (Sa).   /   … ended: …
Blocked start: account kid1, Editor (notepad.exe, C:\Windows\System32\notepad.exe), process 29392, break time 22:45–23:59 (Sa); closing the app.
Blocked start ended: account kid1, Editor (notepad.exe, …), break time 22:45–23:59 (Sa): closed gracefully after 0.3 s; message shown.
   outcomes: closed gracefully after N s · terminated by force after 20 s (N s until gone) · terminated by force (service stopping)
             · terminated by force after 20 s (not ended after 3 rounds)
   message:  shown · not shown (a message is already open) · not shown (tray client not connected) · not shown (tray client did not answer)
<n> further blocked starts of account kid1, Editor (…) were not logged in the last hour.
Tray client connected: <id> (session 2, verified).   /   … (unverified).
Warning: The time zone of the PC changed from W. Europe Standard Time (UTC+01:00) to Pacific Standard Time (UTC-08:00); break times now use the new local time. Session in use: kid1.
Warning: The clock of the PC changed from 2026-10-10 20:30:05 to 2026-10-10 18:30:05; break times now use the new local time.
Warning: Process <pid> of the blocked app … could not be terminated (Win32 error N).
Error:   The blocked app … of account kid1 could not be ended: N process(es) still running after 3 rounds.
<n> blocked start(s) left open by a service stop were completed as "unknown (service stopped)".
Purged enforcement records older than 90 days: … · Purged enforcement records of deleted accounts: … · Purged the break times of deleted account …
```

### Database (admin, read-only copy)

`%ProgramData%\EagleEye\EagleEye.Service.db`: `BreakTimeEntries` (minutes after midnight, `Days` bit mask Mo = 1 … Su = 64), `AccountDisplayTexts` (no row = default text; line breaks `\n`), `BlockedStarts` (one row per blocked start: `StartedLocal`, `DetectedUtc`, names and path, `ProcessId`, `Trigger`, entry minutes/days, `Weekday`, `Outcome`, `SecondsUntilGone`, `MessageState`), `TimeChangeFindings`. The plan's Manual Verification Notes apply unchanged.

### DEV smoke check (Windows Developer Machine, 2026-10-10, non-elevated, next to Michael's running 0.4.x service and tray client, both untouched)

Setup: Debug console service (`EAGLEEYE_DATA_DIR=artifacts/smoke-us005`, `EAGLEEYE_DEV_WATCH_SID` = DEV's SID, `EAGLEEYE_DEV_PORT_OFFSET=10000`), Debug tray client and Debug parent app (`EAGLEEYE_DEV_APP_DATA_DIR=artifacts/smoke-us005-app`), driven with UI Automation. UI steps were done only while the session had been idle for at least 5 minutes (Michael was using the PC in between).

| What | Result |
|---|---|
| Pairing, menu, Rules page | Pairing via the Debug tray's code; menu Einstellungen, Regeln, Berichte; page title, Konto, accent heading bar, button, header row, "Keine Ruhezeiten festgelegt.", display text with the default text (dark mode, 4K display at this PC's scaling, screenshot checked; 150 % not measured) |
| Add, edit, validate (AC-7 to AC-11) | Add → off, 20:00, 23:59, all days; `24:00` and `abc` → message, field back to the stored value; `730` → 07:30 stored; end `07:00` → "Die End-Zeit muss …"; `20.00` → 20:00 stored and message cleared; unticking six days stored, the last day refused with "Mindestens ein Tag …" and stays ticked |
| Display text (AC-15, AC-16) | Text with `&`, 😊 📚 👍, a line break and 👨‍👩‍👧 set through the edit box → log "display text changed"; stored with `\n` only, emojis intact |
| Persistence (AC-17) | Console service restarted → `Break times loaded: 1 entries … 1 changed display texts`; the reconnected app showed the same values |
| Tray binding (ADR-014) | `Tray client connected: … (session 1, verified)` from the real TCP owner lookup |
| Agent close command (Step 4.4) | By hand on stdin: Notepad closed, answer `windows 1, missing 0`; with a wrong creation time `windows 0, missing 1` and Notepad stayed open; an invalid line → stderr "invalid command ignored" |
| Blocked start (AC-21 to AC-24, AC-28, AC-30 to AC-32, AC-35) | Entry on, Notepad started → dialog at once, topmost and active, title "EagleEye", info icon, text with the line break, "OK"; Notepad `closed gracefully after 0.3 s; message shown`; Esc and Alt+F4 ignored, Enter closed it; a second Notepad while the dialog was open → closed, `not shown (a message is already open)`; `BlockedStarts` rows with all fields; no `DailyUsage` row for the blocked apps; switched off → no further blocks. First run showed the emojis as boxes → fixed (D-7), second run shows them monochrome |
| Force path | A program whose window ignored `WM_CLOSE` (the Debug tray's own dialog, see §6 item 3) → `terminated by force after 20 s (20.3 s until gone)` |
| Crash on service restart (D-13) | Found, bisected, fixed and verified on Rules and Reports |
| CPU (AC-29, Step 12) | Entry not in effect, ~10 recorded apps, 60 s, 20 logical CPUs: service **0.009 %**, agent **0.005 %** of total CPU (US-004: 0.016 % / 0.005 %); agent 22 MB private, service 35 MB private |
| Not done | Installers (admin), SYSTEM agent, other sessions/accounts, lock/sign-out with the dialog open, full-screen apps, time-zone change (plan Step 11.5), Store app (Calculator), a second parent app at the same time (AC-19 broadcast between two apps; the broadcast path is unit-tested and the reconnect fetch was checked), 150 % on a second PC, light mode |

Clean-up: all processes started by DEV were stopped; the test entry is switched off; Michael's service, tray client and parent app data were not touched. Left behind (git-ignored): `02_Implementation/artifacts/smoke-us005*` (console service data with a test certificate, the Debug app's pairing), `artifacts/publish`.

---

## 9. Machine(s) Used

| Step | Machine |
|---|---|
| Steps 0 to 13 | Windows Developer Machine |
| MacBook | Not used. `Shared` and `ParentApp.Core` contain no Windows APIs (all Win32 code is in the Service, the TrayClient and the MAUI head's `#if WINDOWS` mapping), so `build.ps1` / `test.ps1` keep working there after `git pull`. |

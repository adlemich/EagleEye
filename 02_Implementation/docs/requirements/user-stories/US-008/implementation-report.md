# Implementation Report: US-008 — App Groups

**Author**: DEV
**Date**: 2026-10-31
**Branch**: `feature/US-008-app-groups`
**Plan**: `02_Implementation/docs/requirements/user-stories/US-008/implementation-plan.md` (approved 2026-10-31, revision 1), ADR-021
**Machine(s) used**: MacBook (Step 1, Steps 8 to 10), Windows Developer Machine (Steps 2 to 7 and 11, see §7)
**Story status**: Implemented (2026-10-31; step c awaits Michael's approval)

> All plan steps 1 to 11 are done. Michael's install of the service 0.8.0 (§6.2) and his Mac check (§8.6, incl. the D-19 re-check) are done. Q-DEV-1 is closed (§5).

---

## 1. Summary

| Step | State | Content |
|---|---|---|
| 1 | Done (MacBook) | Drag-and-drop spike. Chosen: MAUI drag/drop recognizers in shared XAML + "≡" button with "Move to …" + right-click `ContextFlyout` (`US-008/spike-drag-drop.md`). |
| 2 | Done | `Directory.Build.props` 0.8.0. Shared: `AppGroupDto`, `GroupableAppDto`, `AppGroupsDto`, `AppGroupRuleDto`, `AppGroupRulesDto`, `GroupUsageDto`, `DayUsageDto.Groups`, `AppGroupNames` (NFC, trim, 1–40 text elements, ≤ 160 code units, no control chars / lone surrogates, key `ToUpperInvariant`). `IParentHub` +8, `IParentClientCallback` +2, `KidMessageDto.AppName` doc. Core: `IParentHubClient`/`ParentHubClient` (2 events, 8 calls) and `IParentHubGateway`/`ParentHubGateway` (2 events forwarded) — complete, not stubs (see D-2). |
| 3 | Done | Migration 6 (`AppGroups`, `AppGroupMembers`, `AppGroupRules`, `ActivitySpans`, `BlockedStarts.GroupId/GroupName`). `AppGroupRepository`. `UsageRepository`: spans in `ApplyAsync` (same transaction), `GetSpansAsync`, purge by day (and by record via cascade). `BlockedStartRepository` writes the group columns. |
| 4 | Done | `ActivitySpanAccumulator` in `UsageTracker` (`TrackerOutput.Spans`, ticks only); `UsageService` persists spans with the tick (retried on failure), takes `IEnumerable<IAppRecordObserver>`, fills group lines in `GetAccountUsageAsync` and `PublishDayAsync`. `Groups/SpanUnion`, `Groups/GroupUsageCalculator`, `Groups/AppGroupsSnapshot` (+ `IAppGroupsView`). `TodayUsageLedger.ApplySpans`, `GroupUsedSeconds`; spans passed through `EnforcementTick.Spans`. |
| 5 | Done | `Groups/AppGroupService` (state owner of both areas, one revision counter, catalog via `Groups/AppGroupCatalog`, controlled set from the loop, purger), `Groups/AppGroupChangeLog`, broadcaster overloads, 8 `ParentHub` methods with safe texts, DI, start order (… app rules → **app groups** → warning settings …). |
| 6 | Done | `LimitRule`, `LimitSubject`, `LimitSubjectResolver`; `AppEndCalculator` takes `LimitRule?`. `AppStartGate` and `TimeLimitEngine` per subject (group warnings once, grace per subject incl. previous members, one shared close message for a group). `WarningLedger.SubjectKey`; `EnforcementLog` names the group; history columns. All US-006 engine/gate tests green with only constructor/signature edits (AC-30). |
| 7 | Done | TrayClient tests for group names (de/en). `EagleEye-Setup-0.8.0.exe` built and installed by Michael (migration 6, REG-001 OK, §6.2). Non-elevated console smoke on a copy of a real v5 database. Live group smoke and performance not done by DEV (D-8, accepted by Michael). |
| 8 | Done (MacBook) | ParentApp.Core: `AppGroupsModel`, `AppGroupRulesModel`, `AppGroupNameRules`, App Groups view models, App group rules section, grouped apps hidden from Application rules, Reports group lines + refetch on a group change, menu, dialogs, texts de/en, unit tests (§8). |
| 9 | Done (MacBook) | Mac views: `AppGroupsView`, Rules section, Reports group lines, styles; on-screen check with a probe and screenshots (§8.3). |
| 10 | Done (MacBook) | `EagleEye-ParentApp-0.8.0.dmg` built (§8.5); READMEs incl. US-007 F-2; §8. Michael installed it and ran the check (§8.6); fixes D-18, D-19. |
| 11 | Done (Windows) | `build.ps1` 0 warnings / 0 errors incl. the Windows parent app, which compiles the Mac-side XAML, styles and SVGs of D-18/D-19; `test.ps1` all four projects green (§4). No Windows-only fix needed, so no new deviation. Windows parent app not run or packaged (TI-13). |

## 2. Deviations from the Implementation Plan

| # | Plan | Done instead | Why |
|---|---|---|---|
| D-1 | `SpanUpdate` carries a `SpanKey`; `UsageService` maps key → `SpanId` | No key/id map. A span is identified by (app record, start): unique index `UX_ActivitySpans_AppStart (AppId, StartUtcMs)`, written by upsert (`EndUtcMs = MAX(old, new)`). | Less code (Michael's guidance): no in-memory id map, no `RETURNING` handling; a clock jump back onto an old start only extends that row. Schema otherwise as planned (both planned indexes kept). |
| D-2 | Step 2: Core client and gateway "members added" as stubs | Implemented fully in Step 2 (one-line `InvokeAsync` calls, gateway forwarding) with `ParentHubGatewayAppGroupsTests`. | They are trivial and testable on Windows; Step 8 on the MacBook only needs models and view models. |
| D-3 | Group columns on `CloseRequest` and `BlockedStartRecord` | `GroupId`/`GroupName` added to `AppRuleAtClose` (the rule at close), which already flows request → runner → record. | No change in `CloseSequenceRunner`; the group belongs to "which rule applied". |
| D-4 | Separate group broadcaster | Two overloads on `IAccountRulesBroadcaster`/`AccountRulesBroadcaster`. | Same pattern as app rules and warning settings; no new class or DI entry. |
| D-5 | `AppGroupsSnapshot` holds the app catalog | Catalog kept in `AppGroupCatalog` inside `AppGroupService`, not in the snapshot. | Catalog changes (new app records, ticked accounts) then do not replace the snapshot the engine compares for grace detection. |
| D-6 | Group lines: "spans and a current member in the day's app table" | Listed when the current members have spans that day. | Spans and `DailyUsage` come from the same crediting, so this is equivalent; `DayAppUsage` has no program path. |
| D-7 | Purge log line | "Purged usage data older than …: N daily entries, N history entries, N activity spans." | Span purge is reported in the existing line. |
| D-8 | Step 7.3 live group smoke (seeded "Test", Notepad 3 min + both 2 min, Paint blocked, member row removed, ungrouped app) and 7.5 performance | **Not done live by DEV** (as US-006 D-15). Done instead: console service on a copy of a real v5 database (no watched session) → migration 6 and group loading verified; the scenarios are covered by unit tests (`TimeLimitEngineGroupTests`, `AppStartGateTests`); AC-41 guard test (union of 20 × 100 spans). | This Windows machine is the service PC and the session is Michael's admin session (in use). A Debug service watching it would apply deny-by-default to every app without a rule (VS Code, terminal) at the first agent report. **Accepted by Michael (2026-10-31)**, see Q-DEV-1 (§5). |

Notes (not deviations):
- **Installed version**: the service PC runs service **0.6.2**, not 0.7.0 (0.7.0 was the Mac app only). Its database is schema 5, so migration 6 applies as planned.
- **No migration log line exists** in the code (also not for migrations 1–5). Evidence of migration 6 after the install is the line `App groups loaded: … groups with … apps, … group rules.`, which reads the new tables. No code added for a migration log (less code).
- Bug found by a unit test while writing Step 4 and fixed before commit: the accumulator lost the last extension of a span that was replaced before the tick output (midnight split, or a gap between two agent reports in one tick).
- `CloseSequenceRunnerTests.ParallelLimit_FurtherSequencesQueueAndAreDroppedAtStop` failed once in a full run and passed in three reruns: timing-sensitive pre-existing test, code untouched.

## 3. Files

**Step 1** — `US-008/spike-drag-drop.md`, this report, `US-008/evidence/*` (spike patch, probe log, 4 screenshots). No production code.

**Steps 2–7 (Windows)** — paths under `02_Implementation/`:
- Version: `Directory.Build.props`.
- Shared new: `src/EagleEye.Shared/Constants/AppGroupNames.cs`, `src/EagleEye.Shared/Models/AppGroupDto.cs`, `GroupableAppDto.cs`, `AppGroupsDto.cs`, `AppGroupRuleDto.cs`, `AppGroupRulesDto.cs`, `GroupUsageDto.cs`. Changed: `Models/DayUsageDto.cs`, `Models/KidMessageDto.cs`, `Contracts/IParentHub.cs`, `Contracts/IParentClientCallback.cs`.
- Core changed: `src/EagleEye.ParentApp.Core/Communication/IParentHubClient.cs`, `ParentHubClient.cs`, `IParentHubGateway.cs`, `ParentHubGateway.cs`.
- Service new: `src/EagleEye.Service/Data/AppGroupRecords.cs`, `IAppGroupRepository.cs`, `AppGroupRepository.cs`; `Statistics/ActivitySpanAccumulator.cs`; `Groups/AppGroupsSnapshot.cs`, `SpanUnion.cs`, `GroupUsageCalculator.cs`, `AppGroupCatalog.cs`, `AppGroupChangeLog.cs`, `IAppGroupService.cs`, `AppGroupService.cs`; `Enforcement/LimitRule.cs`, `LimitSubject.cs`.
- Service changed: `Data/ServiceDatabase.cs`, `IUsageRepository.cs`, `UsageRepository.cs`, `UsageRecords.cs`, `BlockedStartRepository.cs`, `BreakTimeRecords.cs`; `Statistics/UsageTracker.cs`, `TrackerOutput.cs`, `UsageService.cs`, `UsageAccountingLoop.cs`; `Enforcement/TodayUsageLedger.cs`, `AppEnforcement.cs`, `AppEndCalculator.cs`, `AppStartGate.cs`, `TimeLimitEngine.cs`, `WarningLedger.cs`, `EnforcementLog.cs`; `Rules/IAccountRulesBroadcaster.cs`, `AccountRulesBroadcaster.cs`, `RuleExceptions.cs`; `Communication/ParentHub.cs`; `Program.cs`.
- TrayClient: no production change.

## 4. Unit Tests

**Final result on Windows (Step 11, 2026-10-31, at `9836bd9`)**: `build.ps1` 0 warnings, 0 errors in all five steps (Shared, Service, TrayClient, ParentApp.Core, ParentApp `net10.0-windows10.0.19041.0`). `test.ps1`: **Shared 305, Service 1 493, TrayClient 95, ParentApp 1 109 = 3 002, all passed**, 0 failed (baseline 0.7.0 branch start: 282 / 1 365 / 90 / 994 = 2 731; after Step 7: 305 / 1 493 / 95 / 996 = 2 889).

Note: compiling the test projects shows 9 analyzer warnings. All of them are in test code, none in production code. CS8631 (`Assert.Equal` with a collection expression that contains `null` binds to the `ReadOnlySpan` overload) appears in `EagleEye.Service.Tests/Data/BlockedStartRepositoryTests.cs` (6, older stories), `AppRuleRepositoryTests.cs` (1, older story) and `AppGroupRepositoryTests.cs` (1, US-008, same pattern). xUnit2029 appears in `EagleEye.ParentApp.Tests/ViewModels/RulesViewModelTests.cs` (1, older story). The tests pass and were left unchanged (no unrelated refactoring, less code). One small change can clean them up if Michael wants.

New test classes (`02_Implementation/tests/`): `EagleEye.Shared.Tests/Constants/AppGroupNamesTests.cs`, `Models/AppGroupDtoTests.cs`; `EagleEye.Service.Tests/Data/AppGroupRepositoryTests.cs` (incl. migration 6 on a real version-5 file); `Statistics/ActivitySpanAccumulatorTests.cs`; `Groups/SpanUnionTests.cs` (AC-19 examples, AC-41 guard), `GroupUsageCalculatorTests.cs`, `AppGroupsSnapshotTests.cs`, `AppGroupServiceTests.cs`, `AppGroupCatalogTests.cs`, `AppGroupChangeLogTests.cs` (incl. broadcaster overloads); `Communication/ParentHubAppGroupsTests.cs`; `Enforcement/TimeLimitEngineGroupTests.cs` (AC-24, AC-26 to AC-28, AC-32 to AC-34 scenarios, Michael's examples), `LimitSubjectTests.cs` (resolver, subject key, group log texts); `EagleEye.ParentApp.Tests/Communication/ParentHubGatewayAppGroupsTests.cs`.
Extended: `UsageRepositoryTests` (spans), `BlockedStartRepositoryTests` (group columns), `UsageServiceTests` (spans, retry, group lines, several observers), `UsageTrackerTests` (spans), `TodayUsageLedgerTests` (spans, group time), `AppStartGateTests` (group blocked/allowed), `AppEnforcementTests`, `UsageAccountingLoopTests`, `TrayClient KidMessageTextTests` (group name for every app reason, de/en).

## 5. Open Questions and Risks

- **Q-DEV-1 — closed (Michael, 2026-10-31)**: the live group smoke (plan 7.3) and the performance measurement (7.5, AC-41) needed a controlled session other than the admin session. **Decision**: D-8 is accepted. DEV does neither. Michael checks both during pilot operation. TES may list them as optional tests in the manual test plan. The engine and gate paths are covered by unit tests, incl. the AC-41 guard test.
- R-1 resolved by the spike; still untested on WinUI (TI-13). The Windows parent app compiles the new views, styles and SVGs (Step 11) but is not run until US-009.
- After the install, every new group is blocked for every kid until its rule is set (R-4, by design).

## 6. How to Test

### 6.1 Done by DEV (Windows, 2026-10-31, non-elevated)

Debug console service (`EAGLEEYE_DATA_DIR=02_Implementation/artifacts/smoke-us008`, a copy of the US-006 smoke database at schema 5; `EAGLEEYE_DEV_PORT_OFFSET=10000`; no `EAGLEEYE_DEV_WATCH_SID`), next to the installed 0.6.2 service (untouched):
- start 1: schema 5 → 6, log `App groups loaded: 0 groups with 0 apps, 0 group rules.`, endpoints on 15080/15443, no errors;
- group "Test" (Notepad, Paint, rule 0:05 all days for the controlled account) seeded with Microsoft.Data.Sqlite (no `sqlite3` on this machine); start 2: `App groups loaded: 1 groups with 2 apps, 1 group rules.`
- Release publish (`artifacts/publish`) contains no `EAGLEEYE_DEV` string.
Console output: `02_Implementation/artifacts/smoke-us008-console*.txt` (git-ignored).

### 6.2 Artifact and install (Michael)

- Artifact: `03_Delivery/windows/EagleEye-Setup-0.8.0.exe` (service + tray client, 0.8.0; git-ignored, built on this machine).
- Install over 0.6.2 on the service PC (`192.168.1.37`, this machine), elevated. Expect in `%ProgramData%\EagleEye\logs\EagleEye.Service-NNN.log`: `App groups loaded: 0 groups with 0 apps, 0 group rules.` (proves migration 6), then REG-001-01 to -05 of `02_Implementation/docs/testing/regression-checklist.md`.
- Database: new tables `AppGroups`, `AppGroupMembers`, `AppGroupRules`, `ActivitySpans`; `BlockedStarts.GroupId`, `GroupName`.
- Group lines on Reports start with the install (earlier days have no spans, Q-2).
- Known limitation: the Windows parent app 0.7 must not be used against the 0.8 service (no mixed versions); the Mac app 0.8.0 follows in Steps 8–10.
- **Done (Michael, 2026-10-31):** 0.8.0 installed over 0.6.2 on the service PC. Log at 15:16:18 shows `App groups loaded: 0 groups with 0 apps, 0 group rules.` (migration 6 ran), no WRN/ERR/FTL since the install. REG-001-01 to -05 OK; tray *App Infos* shows 0.8. Evidence: `02_Implementation/docs/requirements/user-stories/US-008/evidence/install-0.8.0-log-excerpt.txt` (full log kept locally, git-ignored). Step 7 complete except D-8 / Q-DEV-1.

### 6.3 Handover to TES (final state)

| | |
|---|---|
| Version | 0.8.0 (service, tray client, Mac app) |
| Service + tray | `03_Delivery/windows/EagleEye-Setup-0.8.0.exe` (git-ignored, on the Windows machine). **Already installed** on the service PC `192.168.1.37` (§6.2) |
| Mac parent app | `03_Delivery/macos/EagleEye-ParentApp-0.8.0.dmg` (git-ignored, on the MacBook), SHA-256 in §8.5. **Already installed** by Michael. After a reinstall, turn the Local Network permission off and on (`02_Implementation/docs/dev-process/dev-environments.md` §5) |
| Windows parent app | Not part of this story. It builds (Step 11) but is not run, packaged or tested until US-009 (TI-13). Do not use the 0.7.x Windows parent app against the 0.8 service |
| Test accounts | `kid1`, `kid2` under parental control, with app records (Editor, Paint, Rechner) |
| Service log | `%ProgramData%\EagleEye\logs\EagleEye.Service-NNN.log`. Group lines: `App groups loaded: …`, group changes (`AppGroupChangeLog`), enforcement entries that name the group (`EnforcementLog`), purge line with activity spans (D-7) |
| Database | `%ProgramData%\EagleEye\` (schema 6: `AppGroups`, `AppGroupMembers`, `AppGroupRules`, `ActivitySpans`; `BlockedStarts.GroupId/GroupName`) |
| Mac UI checks | §8.6 (Michael's 12 points, passed) can be reused as the base of the Mac part |
| Kid side | Tray messages name the group instead of the app for group limits (no tray code change; texts from the service, `KidMessageFactory`) |
| Optional (Q-DEV-1) | Live group smoke as `kid1` (plan 7.3) and performance AC-41 (plan 7.5). Michael checks them in pilot operation; TES may list them as optional tests |

Known limitations: group lines on Reports start with the 0.8.0 install (Q-2). A new group is blocked for every kid until its rule is set (R-4, by design). AC-12 right-click opens one "Move to …" item, which opens the same sheet as "≡" (D-9). The UI texts are listed in §9.

## 7. Machine(s) Used

| Step | Machine |
|---|---|
| 1 | MacBook |
| 2–7 | Windows Developer Machine (= service PC) |
| 8–10 | MacBook (Debug probe with sample data; no live connection, see D-17) |
| 11 | Windows Developer Machine (build, tests, report) |

---

## 8. macOS Part (Steps 8 to 10, MacBook, 2026-10-31)

Host: MacBook (macOS 27, Apple Silicon), branch `feature/US-008-app-groups`, started in sync with `origin` at `0dcee76`.

### 8.1 What was done

**Step 8, `EagleEye.ParentApp.Core`** (commit `ae0486f`):
- `Groups/AppGroupsModel` (area "AppGroups": fetch on connect, revision rule, create/rename/delete/move with confirmation and the 4 s timeout; one fixed key of `SelectedAccountModel`, like the warning settings). `Groups/AppGroupNameRules` (shared `AppGroupNames` + no duplicate ignoring case; the renamed group itself is ignored, so its own or re-cased name is valid).
- `Rules/AppGroupRulesModel` (area "AppGroupRules:{sid}"). `Rules/IRuleTableWrites`: the budget-part and weekday writes of a row id, implemented by `AppRulesModel` and `AppGroupRulesModel` (D-11).
- `ViewModels/AppGroupsViewModel` (states NoData / NoControlledAccounts / Loading / Ready; sections sorted by name in the current culture ignoring case, then id, "Not in any group" last; name dialog loop; delete confirmation; `MoveTargets`; `MoveAsync` (own section → nothing); drag methods `BeginDrag`, `DragOver`, `DragLeave`, `DropAsync`, `EndDrag`; error line), `AppGroupSectionViewModel`, `GroupableAppViewModel` (tooltip path, "Move to …" command), `OrderedMerge` (in-place merge used by the new lists and `DayUsageViewModel`), `LoadStates`.
- `AppGroupRulesSectionViewModel` (rows = `AppRuleRowViewModel`: id = group, name, members line "a, b" or "(keine Apps)", no stored rule = 0:00 and no day; "Noch keine App-Gruppen …"; same validation and messages as the app rules).
- Changed: `AppRulesSectionViewModel` (grouped apps hidden by program path, case-insensitive; a returning app shows its stored own rule; ready when both areas are), `RulesViewModel` (hosts `GroupRules`, selection and flush), `DayUsageViewModel` (`GroupRows`, `HasGroupRows`, sorted like the app rows), `ReportsViewModel` (passes the group lines), `AccountUsageModel` (fetches again on an "AppGroups" broadcast, D-12), `MainViewModel` (menu "Einstellungen", "App Gruppen", "Regeln", "Berichte"; start page unchanged), `IDialogService` (+`PromptTextAsync`, +`ChooseAsync`, D-10), `AppTexts` + resx (24 new keys de/en, AC-17 sentence added).
- MAUI head (needed to build): `MauiDialogService` (prompt with `Keyboard.Text` and max. 160 code units, so 41 characters still reach the validation; action sheet → index), DI.

**Step 9, Mac views** (commit `94e82a7`):
- `Views/AppGroupsView` (+ code-behind forwarding the drag events): explanation, "Gruppe hinzufügen", error line, one section per group with a heading bar ("Umbenennen"/"Löschen", style `HeadingBarButton`), entry area as drop target (min. 40 pt high, so empty groups are targets), entries with name (tooltip = path), "≡" (style `EntryMenuButton`, larger glyph, no fill) and the right-click menu item "Verschieben nach …". Highlight: `DataTrigger` on `IsDropTarget` over an explicit `Transparent` base background.
- `RulesView`: section "App-Gruppen-Regeln" between the display text and "Applikations-Regeln", same columns (220 | 64 | 64 | 7 × 48) and handlers; members in small secondary text below the name.
- `ReportsView`: "App-Gruppen" heading and table (App-Gruppe | Nutzung (HH:MM)) below each day's app table, only when a group has used time.
- `MainPage` (fourth page), `MauiProgram` (models, view models, view).
- Styles: `Colors.xaml` `DropHighlightLight/Dark`; `Styles.xaml` `HeadingBarButton`, `EntryMenuButton` (both with explicit "Normal"); `MacStyles.xaml`: the Mac `Button` style now sets "Normal" explicitly (TI-10; buttons are disabled while their command runs, e.g. during a dialog).

**Step 10** (commit see the report's git history): `.dmg` 0.8.0 (§8.5); READMEs: `src/EagleEye.ParentApp/README.md` (US-007 F-2: Mac platform layer, Mac build/package, data locations, Keychain, removal script; the Catalyst "Normal" rule; App Groups view and drag-and-drop technique), `src/EagleEye.ParentApp.Core/README.md` (US-007 localization, US-008 classes), `src/EagleEye.Service/README.md` (Groups folder, spans, migration 6, state areas, log entries), `src/EagleEye.Shared/README.md` (DTOs, `AppGroupNames`).

### 8.2 Unit tests (MacBook, `pwsh 02_Implementation/scripts/test.ps1`)

**Shared 305, ParentApp 1 109, all green** (branch start on the Mac: 305 / 996). `build.ps1`: 0 warnings, 0 errors (Shared, ParentApp.Core, ParentApp `net10.0-maccatalyst`).

New test classes (`02_Implementation/tests/EagleEye.ParentApp.Tests/`): `Groups/AppGroupsModelTests` (fetch, broadcast, all five writes + confirmation, rejection, guards; group rules model: selection, other account ignored, writes), `Groups/AppGroupNameRulesTests` (empty, spaces, 41 characters, "games"/"Games", " äpfel "/"Äpfel", 40 emojis valid, own re-cased name on rename, de/en messages), `ViewModels/AppGroupsViewModelTests` (all states de/en, sort with culture and case, unknown group → ungrouped, empty texts de/en, in-place merge, dialog loop with message and typed text, cancel, rename pre-filled, delete confirm text de, move/own section, move targets, "Move to …" sheet options de, drag highlight sequence, drop outside, error line and clearing, guards), `ViewModels/AppGroupRulesSectionViewModelTests` (states, sort, members line, default rule, no-groups text de/en, follow "AppGroups", writes go to the group rules, error and validation messages, flush, guards).
Extended: `AppRulesSectionViewModelTests` (grouped app hidden, returns with own rule; groups not ready), `RulesViewModelTests` (group section selection and flush), `DayUsageViewModelTests` (group rows sort, "00:00", hidden), `ReportsViewModelTests` (group lines per day), `AccountUsageModelTests` (refetch replaces the days, failure keeps them, no fetch when not ready, account change during refetch), `MainViewModelTests` (menu order).
Coverage (coverlet, ParentApp.Tests): every new or changed Core class has 100 % line and branch coverage.

### 8.3 On-screen check (Step 9.2)

The Debug build has its own data folder and Keychain entry and is not paired; pairing needs the code shown on the service PC. DEV therefore checked the views with a **temporary probe** (not committed; source kept as `US-008/evidence/step9-ui-probe.cs.txt`): fake models with sample data (Games with five apps, empty School, four ungrouped apps, a group rule, usage with group lines) and scripted steps, captured with `screencapture`. Evidence in `US-008/evidence/`:

| Screenshot | Shows | Result |
|---|---|---|
| `step9-1-highlight-dark.png` | drag over "School" (empty group) | highlighted green |
| `step9-2-highlight-moves-dark.png` | pointer moves to "In keiner Gruppe" | School cleared, new section highlighted |
| `step9-3-dropped-cleared-dark.png` | Paint dropped on School | moved, no highlight left |
| `step9-4-move-to-sheet.png` | "≡" on Epic Games Launcher | sheet "Verschieben nach …": School, In keiner Gruppe, Abbrechen |
| `step9-5/6-…-light.png` | same in light mode | highlight on, then cleared |
| `step9-7-rename-yellow-accent.png` | "Umbenennen", yellow accent | prompt "App-Gruppe umbenennen", "Games" pre-filled; heading-bar buttons readable (black on yellow) |
| `step9-8-delete-confirm-blue-accent.png` | "Löschen", blue accent | „Gruppe „Games“ löschen? …“ with Abbrechen / Löschen |
| `step9-9-rules-group-rules.png` | Rules, light, yellow | "App-Gruppen-Regeln" with members line and "(keine Apps)"; Fortnite and Paint (grouped in the sample) not under "Applikations-Regeln"; AC-17 sentence |
| `step9-10-reports-group-lines.png` | Reports, dark | "App-Gruppen" table under today (Games 00:57, School 00:00); none for a day without group lines |

The probe also confirmed that the right-click menu item gets the entry's view model and command (`step9-probe-log.txt`). The heading-bar buttons show dimmed while their dialog is open (command running) and repaint normally afterwards. Not checked by DEV: real mouse/trackpad drags, the right-click itself, "Larger Text" (needs System Settings) — Michael's list §8.6.

### 8.4 Deviations (macOS part)

| # | Plan | Done instead | Why |
|---|---|---|---|
| D-9 | Right-click menu "with the same targets as `MoveTargets(app)`" (spike decision §6) | One menu item "Verschieben nach …" that opens the same action sheet as "≡". | A `MenuFlyout` cannot bind a list in XAML; per-entry menus would have to be rebuilt in code-behind on every group change. One static item stays in the XAML template and reuses the command (less code). Cost: one more click via right-click. AC-12 allows either. |
| D-10 | `IDialogService.PromptAsync` gets a kind parameter | Two new methods `PromptTextAsync` (max length, text keyboard) and `ChooseAsync` (action sheet → index). | An added optional parameter breaks the Moq expression trees of the existing host-dialog tests (CS0854); a separate method leaves the US-002 host dialog untouched. |
| D-11 | `AppGroupRuleRowViewModel` (or a shared row base) | The group rows are `AppRuleRowViewModel` instances (id = group id, `DisplayName` = group name, `ProgramPath` = members line); the row host writes through `IRuleTableWrites`, which both rule models implement. | Plan allowed a shared base; reusing the row as is gives identical editing behaviour (AC-16) without a second 240-line class. Only the row host's write type changed (one test host line). |
| D-12 | `ReportsViewModel` refetches on an `AppGroups` change | `AccountUsageModel` refetches on the gateway's `AppGroupsChanged` and **replaces** the held days (no "Loading"). | `GetAccountUsage` returns the days with their unchanged revisions, so the revision rule would drop them; the model owns the replicas. A failed refetch keeps the shown days. |
| D-13 | — | "Applikations-Regeln" and "App-Gruppen-Regeln" show "Wird geladen …" until both their areas ("AppRules"/"AppGroupRules" and "AppGroups") are loaded. | Otherwise grouped apps would flash in "Applikations-Regeln" right after a connect. |
| D-14 | AC-2 explanation text (English example) | One sentence added: "Apps verschiebst du mit der Maus oder dem Trackpad, mit ≡ oder mit einem Rechtsklick." / "Move apps by dragging them with the mouse or trackpad, with ≡ or with a right click." | Discoverability of "≡" and the right-click menu. Wording note; TES should use the text as implemented. |
| D-15 | AC-5 "no controlled account ("No data available")" | As on the Rules page (US-005 AC-6): "Keine Konten unter Elternkontrolle. Konten unter Einstellungen auswählen." | AC-5 refers to the US-005 AC-6 states; same page states as Rules and Reports. |
| D-16 | Delete dialog (AC-10) gives only the question | Title "App-Gruppe löschen" / "Delete app group" (new key `GroupsDeleteTitle`). | The macOS alert needs a title; the question is the message. |
| D-17 | Step 10.1: DEV installs the `.dmg` over 0.7.0 and spot-checks AC-43; Step 9.2 against the service 0.8.0 | Not done by DEV. Views checked with the probe (§8.3); install, pairing and live checks are on Michael's list (§8.6). | Installing into `/Applications` changes Michael's paired 0.7.0 installation (outside the repo), and pairing a Debug build needs the code shown on the service PC. |
| D-18 | Section headings as accent-filled bars (US-004 ISSUE-007, US-007 AC-10, story AC-2 "heading bar") | **Michael's finding after installing 0.8.0** ("the visual separation between heading bars and buttons is quite hard … only a line border colour, not solid filled"): every section heading on every page (Einstellungen, App Gruppen, Regeln, Berichte day headings) is now **outlined**: shared Border style `SectionHeaderBar` (2 pt line in the accent colour, explicit `Transparent` fill, padding 14,5, no corner radius as before) around a `SectionHeader` label in the normal text colour, bold. The accent is published as the brush `HeadingBarBrush` by `HeadingBarColorService` (`Border.Stroke` needs a brush). `HeadingBarButton` ("Umbenennen"/"Löschen") now uses the normal text colour and a grey border. Solid buttons and the selected menu entry are unchanged. `.dmg` rebuilt, same version 0.8.0. | Solid accent buttons were hard to tell apart from accent-filled headings. Changed in the shared styles, so the Windows parent app gets it in Step 11; the 9 heading usages are wrapped in the Border (a Label cannot draw a border). Checked with the probe and `screencapture` on all four pages, dark and light, red/yellow/blue accents; drop highlight still visible and cleared: `US-008/evidence/step9-header-outline-*.png`. Note: a yellow outline on the light page is thinner in contrast than the other accents but visible. |
| D-19 | Group headers on App Gruppen with the outline text buttons "Umbenennen"/"Löschen" (style `HeadingBarButton`) | **Michael's change request** after his 12-point check: icon buttons. Pencil (rename) and trash bin (delete) as `ImageButton`s with the new shared style `IconButton` (32 × 32, padding 4, explicit transparent "Normal"); the trash reuses `trash_light/dark.svg` of the break-time delete on Regeln → Ruhezeiten, which now uses the same `IconButton` style (same values as before); new `pencil_light/dark.svg` drawn in the same stroke style (24 × 24, stroke 1.8, theme colour per light/dark). Tooltip and `SemanticProperties.Description` reuse "Umbenennen"/"Löschen" ("Rename"/"Delete"). Commands, rename dialog and delete confirmation unchanged. `HeadingBarButton` removed (no longer used). `.dmg` rebuilt, same version 0.8.0. | Requested by Michael; shared XAML and styles, so the Windows parent app gets it in Step 11. Checked with the probe and `screencapture`: dark/red and light/blue on App Gruppen with the drop highlight on, Regeln light/yellow and dark/blue with a break-time row (trash unchanged): `US-008/evidence/step9-group-icons-*.png`. |

Note (not a deviation, 2026-10-31, approved by Michael): `02_Implementation/docs/dev-process/dev-environments.md` §5 got three MacBook lessons (Catalyst explicit colours, drag-and-drop with a non-drag alternative, Local Network permission after a rebuilt `.dmg`); lesson 3 is also in `03_Delivery/macos/README.md` and `src/EagleEye.ParentApp/README.md`. This applies the proposal below.

Proposal for the Orchestrator (plan Step 10.2, not applied by DEV): add to `02_Implementation/docs/dev-process/dev-environments.md` §5 *MacBook lessons*: (1) "Mac Catalyst does not repaint a property that becomes unset: every visual state, trigger or style that sets a colour also sets it explicitly in 'Normal' or as the base value (e.g. `Transparent`); check visual fixes with a probe and `screencapture`." (2) "MAUI `DragGestureRecognizer`/`DropGestureRecognizer` work on Mac Catalyst with mouse and trackpad inside a `ScrollView` (US-008 spike); pass the dragged item in-app."

### 8.5 Artifact

| | |
|---|---|
| File | `03_Delivery/macos/EagleEye-ParentApp-0.8.0.dmg` (git-ignored; stays on the MacBook) |
| Version | 0.8.0 (`CFBundleShortVersionString` checked by `package-macos.ps1`) |
| SHA-256 | `cced8d1501fde06563554781f8e6d815cabc092147f935abae99965d2a99998d` (rebuilt after D-19; earlier builds `9e5e2bc4…dfd48c` and `fcf3a1a4…40c1c6` (D-18) are superseded) |
| Built with | `pwsh 02_Implementation/scripts/package-macos.ps1` (Release, maccatalyst-arm64, ad-hoc signed, verified) |

### 8.6 What Michael checks by hand on the MacBook

**Result (2026-10-31):** Michael ran the 12-point check below on the `.dmg` with the outlined headers (D-18): **no issues found**. His one change request led to D-19 (icon buttons in the group headers); only that change needs a re-check: reinstall the current `.dmg` (§8.5; Local Network switch off and on again, dev-environments.md §5), then on App Gruppen check the pencil (rename dialog) and trash (delete confirmation) icons, their tooltips, light and dark.

Preconditions: service and tray 0.8.0 on the service PC (`192.168.1.37`, done), `kid1` (and `kid2`) under parental control with app records (Editor, Paint, Rechner).

1. **Install over 0.7.0** (AC-42, US-007 F-3): open the `.dmg`, drag the app to Programme, replace. Start it: still paired ("Verbunden mit …"), appearance kept, at most two Keychain prompts ("Immer erlauben"). Finder → Informationen on the app shows version 0.8.0.
2. **Menu** (AC-1): Einstellungen, App Gruppen, Regeln, Berichte; the app still starts on Berichte.
3. **App Gruppen page** (AC-2 to AC-6): explanation, "Gruppe hinzufügen", no groups yet, all apps once under "In keiner Gruppe", sorted; tooltip shows the program path; Windows Explorer not listed.
4. **Create / rename / delete** (AC-7 to AC-10): add "Games"; try "games", empty, spaces, 41 characters (dialog comes back with the message and the typed text; Escape cancels); rename "Games" → "Gaming" (also the unchanged name with OK); delete a group (confirmation text, apps back under "In keiner Gruppe").
5. **Drag-and-drop with the mouse and with the trackpad** (AC-11): drag an app onto a group, onto an empty group, back onto "In keiner Gruppe"; drop onto the own section and outside any section (nothing changes); the section under the pointer is highlighted and the highlight always clears; make the window short so the page scrolls and drag again.
6. **"≡" and right-click** (AC-12): "≡" → "Verschieben nach …" with every other section; right-click / two-finger click → "Verschieben nach …" opens the same list; Abbrechen moves nothing.
7. **Error path** (AC-14): optional — stop the service briefly, move an app → "Die Änderung konnte nicht gespeichert werden. Bitte erneut versuchen." and the page returns to the stored state after reconnecting.
8. **Rules, group rules per kid** (AC-15 to AC-18): "App-Gruppen-Regeln" between Ruhezeiten and Applikations-Regeln; members below the name; new group 0:00, no day; set "Games" 1:00 Sa So for `kid1` and leave `kid2` at 0:00; grouped apps are gone from "Applikations-Regeln" and come back with their old rule after leaving the group; with the Rules page open, a change on App Gruppen shows within 5 s.
9. **Reports group lines** (AC-35 to AC-37): after `kid1` used a group's apps, today shows "App-Gruppen" with the group time (overlap counted once); after a rename or move the lines follow within 5 s; days before the 0.8.0 install show no group lines (Q-2).
10. **Look** (AC-5): light and dark, "Larger Text" (System Settings → Accessibility → Display), another accent colour (heading-bar buttons readable).
11. **Language**: switch the Mac to English (System Settings → General → Language & Region), restart the app: all new texts in English ("App Groups", "Add group", "Not in any group", "Move to …", "App group rules", "App groups" on Reports).
12. **AC-43 spot check**: one break-time change, one app-rule change, Reports of today for `kid1`.

Known limitations: the Windows parent app 0.7.x must not be used against the 0.8 service; the Windows parent app compiles the new views but is untested until US-009 (TI-13). Step 11 (Windows build and all four test projects) is done, see §4.

---

## 9. UI Texts as Implemented (parent app, `src/EagleEye.ParentApp.Core/Resources/AppTexts.resx` / `AppTexts.en.resx`)

New or changed keys in US-008. TES should use these exact texts.

| Key | Deutsch | English |
|---|---|---|
| `MenuAppGroups` | App Gruppen | App Groups |
| `GroupsExplanation` | Hier kannst du Apps, die zusammengehören, in eine Gruppe legen, z. B. ein Spiel mit seinem Launcher. Auf der Seite Regeln legst du dann für jedes deiner Kinder ein Zeit-Budget für die ganze Gruppe fest. Eine neue Gruppe ist gesperrt, bis du ihre Regel einstellst. Jede App kann nur in einer Gruppe sein. Apps verschiebst du mit der Maus oder dem Trackpad, mit ≡ oder mit einem Rechtsklick. | Here you can put apps that belong together into a group, e.g. a game with its launcher. On the Rules page you then set one time budget for the whole group, for each of your kids. A new group is blocked until you set its rule. Each app can be in only one group. Move apps by dragging them with the mouse or trackpad, with ≡ or with a right click. |
| `GroupsAdd` | Gruppe hinzufügen | Add group |
| `GroupsRename` | Umbenennen (tooltip of the pencil icon, D-19) | Rename |
| `GroupsDelete` | Löschen (tooltip of the trash icon, D-19; button in the confirmation) | Delete |
| `GroupsUngrouped` | In keiner Gruppe | Not in any group |
| `GroupsEmpty` | Keine Apps in dieser Gruppe. Apps hierher ziehen. | No apps in this group. Drag apps here. |
| `GroupsAllGrouped` | Alle Apps sind in einer Gruppe. | All apps are in a group. |
| `GroupsNewTitle` | Neue App-Gruppe | New app group |
| `GroupsRenameTitle` | App-Gruppe umbenennen | Rename app group |
| `GroupsNameLabel` | Name | Name |
| `GroupsInvalidName` | Bitte einen Namen mit 1 bis 40 Zeichen eingeben. | Enter a name with 1 to 40 characters. |
| `GroupsDuplicateName` | Es gibt bereits eine Gruppe mit diesem Namen. | A group with this name already exists. |
| `GroupsDeleteTitle` | App-Gruppe löschen | Delete app group |
| `GroupsDeleteConfirmFormat` | Gruppe „{0}“ löschen? Ihre Apps kommen nach „In keiner Gruppe“ und verwenden wieder ihre eigenen Applikations-Regeln. | Delete the group "{0}"? Its apps move to "Not in any group" and use their own app rules again. |
| `GroupsMoveTo` | Verschieben nach … | Move to … |
| `RulesSectionGroupRules` | App-Gruppen-Regeln | App group rules |
| `RulesGroupRulesExplanation` | Hier legst du ein Zeit-Budget pro App-Gruppe fest. Alle Apps einer Gruppe teilen es sich: Die Zeit zählt, solange mindestens eine App der Gruppe verwendet wird. Die Gruppen legst du auf der Seite App Gruppen fest. | Here you set one time budget per app group. All apps of a group share it: the time counts while at least one app of the group is in use. You define the groups on the App Groups page. |
| `RulesColumnGroup` | App-Gruppe | App group |
| `RulesNoGroups` | Noch keine App-Gruppen. Du kannst sie auf der Seite App Gruppen anlegen. | No app groups yet. You can create them on the App Groups page. |
| `RulesGroupNoApps` | (keine Apps) | (no apps) |
| `RulesAppRulesExplanation` (changed, sentence added, AC-17) | … Apps in einer App-Gruppe werden unter App-Gruppen-Regeln eingestellt. | … Apps in an app group are set under App group rules. |
| `ReportsGroupsHeading` | App-Gruppen | App groups |
| `ReportsColumnGroup` | App-Gruppe | App group |

Reused without change: the "no controlled account" state of the Rules page (D-15), the save error "Die Änderung konnte nicht gespeichert werden. Bitte erneut versuchen." (AC-14), and the app rule validation messages in the group rules rows (D-11). Kid-side tray texts are unchanged. For a group limit the existing messages carry the group name in place of the app name (`KidMessageDto.AppName`).

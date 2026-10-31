# Implementation Report: US-008 — App Groups

**Author**: DEV
**Date**: 2026-10-31 (started)
**Branch**: `feature/US-008-app-groups`
**Plan**: `02_Implementation/docs/requirements/user-stories/US-008/implementation-plan.md` (approved 2026-10-31, revision 1), ADR-021
**Machine(s) used**: MacBook (Step 1, Steps 8 to 10), Windows Developer Machine (Steps 2 to 7, see §7)
**Story status**: Analyzed (in implementation)

> This report grows with each plan step. Steps 1 to 10 done (Step 7 and Step 10 except the parts that need Michael: §6.2, §8.6). Step 11 (Windows close-out) not started.

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
| 7 | Partly done | TrayClient tests for group names (de/en). `EagleEye-Setup-0.8.0.exe` built. Non-elevated console smoke on a copy of a real v5 database. **Open (Michael)**: install on the service PC + REG-001, live group smoke, performance (§6.2). |
| 8 | Done (MacBook) | ParentApp.Core: `AppGroupsModel`, `AppGroupRulesModel`, `AppGroupNameRules`, App Groups view models, App group rules section, grouped apps hidden from Application rules, Reports group lines + refetch on a group change, menu, dialogs, texts de/en, unit tests (§8). |
| 9 | Done (MacBook) | Mac views: `AppGroupsView`, Rules section, Reports group lines, styles; on-screen check with a probe and screenshots (§8.3). |
| 10 | Done except install (MacBook) | `EagleEye-ParentApp-0.8.0.dmg` built (§8.5); READMEs incl. US-007 F-2; this section. **Open (Michael)**: install over 0.7.0, pairing, live checks (§8.6). |

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
| D-8 | Step 7.3 live group smoke (seeded "Test", Notepad 3 min + both 2 min, Paint blocked, member row removed, ungrouped app) and 7.5 performance | **Not done live by DEV** (as US-006 D-15). Done instead: console service on a copy of a real v5 database (no watched session) → migration 6 and group loading verified; the scenarios are covered by unit tests (`TimeLimitEngineGroupTests`, `AppStartGateTests`); AC-41 guard test (union of 20 × 100 spans). | This Windows machine is the service PC and the session is Michael's admin session (in use). A Debug service watching it would apply deny-by-default to every app without a rule (VS Code, terminal) at the first agent report. Needs Michael's decision (§5). |

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

Results on Windows after Step 7 (`build.ps1` 0 warnings, `test.ps1` all green): **Shared 305, Service 1 493, TrayClient 95, ParentApp 996 = 2 889** (baseline 0.7.0 branch start: 282 / 1 365 / 90 / 994 = 2 731).

New test classes (`02_Implementation/tests/`): `EagleEye.Shared.Tests/Constants/AppGroupNamesTests.cs`, `Models/AppGroupDtoTests.cs`; `EagleEye.Service.Tests/Data/AppGroupRepositoryTests.cs` (incl. migration 6 on a real version-5 file); `Statistics/ActivitySpanAccumulatorTests.cs`; `Groups/SpanUnionTests.cs` (AC-19 examples, AC-41 guard), `GroupUsageCalculatorTests.cs`, `AppGroupsSnapshotTests.cs`, `AppGroupServiceTests.cs`, `AppGroupCatalogTests.cs`, `AppGroupChangeLogTests.cs` (incl. broadcaster overloads); `Communication/ParentHubAppGroupsTests.cs`; `Enforcement/TimeLimitEngineGroupTests.cs` (AC-24, AC-26 to AC-28, AC-32 to AC-34 scenarios, Michael's examples), `LimitSubjectTests.cs` (resolver, subject key, group log texts); `EagleEye.ParentApp.Tests/Communication/ParentHubGatewayAppGroupsTests.cs`.
Extended: `UsageRepositoryTests` (spans), `BlockedStartRepositoryTests` (group columns), `UsageServiceTests` (spans, retry, group lines, several observers), `UsageTrackerTests` (spans), `TodayUsageLedgerTests` (spans, group time), `AppStartGateTests` (group blocked/allowed), `AppEnforcementTests`, `UsageAccountingLoopTests`, `TrayClient KidMessageTextTests` (group name for every app reason, de/en).

## 5. Open Questions and Risks

- **Q-DEV-1 (Michael)**: live group smoke (plan 7.3) and performance measurement (7.5) need a controlled session other than the admin session. Proposal: do them in the TES run with `kid1` after the install (as US-006 D-15), or Michael signs in as `kid1` and DEV gets a go to run them. The engine/gate paths are covered by unit tests.
- R-1 resolved by the spike; still untested on WinUI (TI-13).
- Step 9 note: the "≡" glyph is hard to read on the small Mac accent button; give it a larger font or a transparent style.
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

## 7. Machine(s) Used

| Step | Machine |
|---|---|
| 1 | MacBook |
| 2–7 | Windows Developer Machine (= service PC) |
| 8–10 | MacBook (Debug probe with sample data; no live connection, see D-17) |
| 11 | Windows Developer Machine (pending) |

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

Proposal for the Orchestrator (plan Step 10.2, not applied by DEV): add to `02_Implementation/docs/dev-process/dev-environments.md` §5 *MacBook lessons*: (1) "Mac Catalyst does not repaint a property that becomes unset: every visual state, trigger or style that sets a colour also sets it explicitly in 'Normal' or as the base value (e.g. `Transparent`); check visual fixes with a probe and `screencapture`." (2) "MAUI `DragGestureRecognizer`/`DropGestureRecognizer` work on Mac Catalyst with mouse and trackpad inside a `ScrollView` (US-008 spike); pass the dragged item in-app."

### 8.5 Artifact

| | |
|---|---|
| File | `03_Delivery/macos/EagleEye-ParentApp-0.8.0.dmg` (git-ignored; stays on the MacBook) |
| Version | 0.8.0 (`CFBundleShortVersionString` checked by `package-macos.ps1`) |
| SHA-256 | `9e5e2bc4f265ccbc40df68e347109fefdccf56a78e1d3ba64faf0ff031dfd48c` |
| Built with | `pwsh 02_Implementation/scripts/package-macos.ps1` (Release, maccatalyst-arm64, ad-hoc signed, verified) |

### 8.6 What Michael checks by hand on the MacBook

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

Known limitations: the Windows parent app 0.7.x must not be used against the 0.8 service; the Windows parent app compiles the new views but is untested until US-009 (TI-13). Step 11 (Windows build and all four test projects) is still open.

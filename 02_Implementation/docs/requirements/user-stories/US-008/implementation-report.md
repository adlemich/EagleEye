# Implementation Report: US-008 — App Groups

**Author**: DEV
**Date**: 2026-10-31 (started)
**Branch**: `feature/US-008-app-groups`
**Plan**: `02_Implementation/docs/requirements/user-stories/US-008/implementation-plan.md` (approved 2026-10-31, revision 1), ADR-021
**Machine(s) used**: MacBook (Step 1), Windows Developer Machine (Steps 2 to 7, see §7)
**Story status**: Analyzed (in implementation)

> This report grows with each plan step. Steps 1 to 6 done; Step 7 done except the parts that need Michael (§6.2). Steps 8 to 11 not started.

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

## 7. Machine(s) Used

| Step | Machine |
|---|---|
| 1 | MacBook |
| 2–7 | Windows Developer Machine (= service PC) |

# Test Report: US-008 — App Groups with a Shared Daily Budget

**Status**: Final. Story closed by the project owner on 2026-10-31 without a TES test plan or a recorded test run. The manual tests are repeated in US-009.
**Date**: 2026-10-31
**Author**: Orchestrator (TES steps d to f skipped by the project owner's decision)
**Test runs evaluated**: none.

---

## 1. Verdict

**Closed by the project owner (2026-10-31): "Close US-008. We will skip manual testing for this US, I will do the tests with US-009 again. Mark US-008 done, merge it to main."** US-009 (Windows parent app parity, all four assets 0.9.0) touches the same features, so the project owner tests groups there together with the Windows parent app. This report records what was and was not verified before the merge.

## 2. Environment

| Item | Value |
|---|---|
| Machine(s) | Windows Developer Machine = service PC `DEV-PC` (service + tray); MacBook (Mac app) |
| Build / installer version | `03_Delivery/windows/EagleEye-Setup-0.8.0.exe` (installed over 0.6.2 on 2026-10-31); `03_Delivery/macos/EagleEye-ParentApp-0.8.0.dmg` (final build with D-19, SHA-256 in `US-008/implementation-report.md` §8.5). Both are git-ignored and exist only on their build machine. |
| Unit tests (Step 11, Windows, `9836bd9`) | Shared 305, Service 1 493, TrayClient 95, ParentApp 1 109 = 3 002, all passed; `build.ps1` 0 warnings |

## 3. Acceptance Criteria Results

No AC has a recorded result from a TES test run. What was **checked outside a test run**:

- **Service install** (plan Step 7, the project owner): 0.8.0 over 0.6.2, migration 6 proven by the log line `App groups loaded: …`, regression REG-001-01 to -05 OK, tray *App Infos* shows 0.8. Evidence: `02_Implementation/docs/requirements/user-stories/US-008/evidence/install-0.8.0-log-excerpt.txt`. Covers the service part of AC-42.
- **The project owner's 12-point Mac check** (`02_Implementation/docs/requirements/user-stories/US-008/implementation-report.md` §8.6, no issues; D-18 and D-19 re-checked): AC-1 to AC-18, AC-35 to AC-37, the Mac part of AC-42, AC-43 spot check.

**Not verified manually** (unit tests only): AC-19 to AC-34 (group used time, enforcement of group rules: warnings, start blocking, closing all members, break times, other accounts, log entries, changes during the day with grace period), AC-38 to AC-40 (storage across restarts, newly ticked accounts, untick), **AC-41 (performance, Q-DEV-1 / D-8)**. Live group enforcement never ran in a kid session.

## 4. Steps Not Done

| Step | What is missing |
|---|---|
| Plan 7.3 / 7.5 | Live group smoke and performance measurement (D-8 accepted; the project owner checks in pilot operation) |
| Story steps d to f | TES test plan, test run, evaluation |

## 5. Issues

None filed. The project owner's two visual requests from the Mac check were fixed inside the story (D-18 outlined section headers, D-19 icon buttons).

## 6. Follow-ups

| # | Follow-up | Owner | When |
|---|---|---|---|
| F-1 | **Manual tests of US-008** (all ACs above marked not verified, plus the Mac checks) repeated in the US-009 test run, including the optional live group check and AC-41 performance as pilot checks | TES / the project owner | US-009 step d/e |
| F-2 | **Clean up the 9 compiler warnings in test code** (8× CS8631 `Assert.Equal` with a nullable collection, one of them in `AppGroupRepositoryTests.cs`; 1× xUnit2029 in `RulesViewModelTests.cs`), back to 0 warnings in `test.ps1` (implementation report §4) | DEV | US-009 step c |
| F-3 | The Windows parent app 0.7.x must not be used against the 0.8 service; the Windows parent app compiles the group views but is untested until US-009 (TI-13) | — | until US-009 delivers 0.9.0 |

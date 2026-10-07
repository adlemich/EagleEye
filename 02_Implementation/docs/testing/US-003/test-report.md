# Test Report: US-003 — Account Inventory and Selection of Accounts under Parental Control

**Status**: Final — approved by Michael, 2026-10-07
**Date**: 2026-10-07
**Author**: Orchestrator (recorded from `test-run-01.md` and Michael's decision; no separate TES evaluation, at Michael's direction)
**Test runs evaluated**: `docs/testing/US-003/test-run-01.md` (build 0.3.1)

---

## 1. Verdict

**Closed as "good enough" by Michael (2026-10-07).** Michael executed test run 01 partially: **18 Pass, 0 Fail, 0 Blocked, 17 Skipped, 1 not ticked** (36 cases). He judged the result sufficient and closed US-003. Several ACs were therefore **not verified manually** (§3); they rest on DEV's unit tests and smoke check (`US-003/implementation-report.md`) and should be covered in a later manual run (see §5).

## 2. Case Tally (run 01)

| Result | Cases |
|---|---|
| Pass (18) | TC-003-01, -02, -03, -04, -05, -06, -10, -11, -12, -13, -14, -16, -17, -18, -19, -20, -21, -36 |
| Skipped (17) | TC-003-07, -08, -09, -15, -22, -23, -24, -25, -26, -27, -28, -29, -30, -31, -32, -33, -34 |
| Not ticked (1) | TC-003-35 |

Blocks: A (upgrade 0.2.0 → 0.3.1, admin-only `logs\`) passed except the supporting ACL-repair case TC-003-07. B (inventory and display, incl. ISSUE-006) passed except TC-003-08, -09, -15. C (tick, persistence) passed except the re-install case TC-003-22. **D (account changes while the service runs) and E (two parent apps, error/revert) were not executed.**

## 3. Acceptance Criteria

| Status | ACs |
|---|---|
| Verified manually | AC-1, AC-2 (direct membership), AC-3, AC-6, AC-8, AC-10, AC-11, AC-12, AC-13, AC-14 (incl. admin-only `logs\` folder), AC-17 (not connected), AC-18 (first inventory) |
| Partly verified | AC-15 (app restart, service restart, reboot: Pass; re-install TC-003-22 skipped) |
| **Not verified manually** | AC-4 (Microsoft account), AC-5 (rename), AC-7 (never paired), AC-9 (no standard accounts), AC-16 (error and revert), AC-19 (live add/delete/rename push), AC-20, AC-21 (standard↔admin), AC-22 (deleted account forgotten), AC-23 (broadcast to a second app), AC-24 (concurrent changes) |

## 4. Issues

| Issue | Severity | Status | Test case |
|---|---|---|---|
| `docs/requirements/user-stories/US-003/issues/ISSUE-006.md` — account list as a table with column headers | Low | Verified/Closed (fix in 0.3.1, TC-003-36 Pass) | TC-003-36 |

No new issues were reported in run 01.

## 5. Notes and Follow-ups

1. **Untested ACs.** The push behaviour (AC-19 to AC-22), the two-app broadcast (AC-23, AC-24) and the error/revert path (AC-16) are central to ADR-010 but were only unit-tested and smoke-checked by DEV (broadcast between two apps on one PC seen within 1 s). Proposal: include TC-003-22 to -35 in the next story's manual run that touches the parent app or the service, or in the next regression run.
2. **Service log evidence.** The cleanup step LOG-COPY is ticked, but no service log files are in `evidence/`. `.gitignore` excludes `*.log`, so copies under `evidence/` could not be committed. Fixed on `main` (Orchestrator, 2026-10-07) with an exception for `docs/testing/**/evidence/**`.
3. **Evidence files.** `evidence/Screenshot 2026-10-07 191530.png` (referenced in the run file Notes) and `evidence/issue_formatting_accounts.png` (ISSUE-006 before the fix).
4. **Setup.** The service PC was brought from 0.3.0 (Michael's quick check) back to 0.2.0 and upgraded to 0.3.1 as planned (S-1 to S-11 ticked).

# Test Report: US-004 — App Usage Tracking and Daily Usage Report

**Status**: Final — approved by Michael, 2026-10-10
**Date**: 2026-10-10
**Author**: Orchestrator (recorded from `test-run-01.md` and Michael's decisions; no separate TES evaluation, at Michael's direction)
**Test runs evaluated**: `docs/testing/US-004/test-run-01.md` (build 0.4.0); visual re-test of ISSUE-007 on build 0.4.1 (Michael's report, no run file)

---

## 1. Verdict

**Closed by Michael (2026-10-10).** Run 01 on build 0.4.0 had **23 Pass, 0 Fail, 0 Blocked, 11 Skipped and 1 not ticked** (35 cases). Michael raised one change request for visuals and usability (ISSUE-007). DEV delivered it as patch 0.4.1. Michael re-tested it visually and reported "All good". He decided that skipped cases stay skipped and are not repeated. The ACs in §3 that were not verified manually rest on DEV's unit tests and smoke check (`docs/requirements/user-stories/US-004/implementation-report.md`).

## 2. Case Tally (run 01)

| Result | Cases |
|---|---|
| Pass (23) | TC-004-01 to -20, -24, -27, -28 |
| Skipped (11) | TC-004-21, -22, -25, -26, -29, -30, -31, -32, -33, -34, -35 |
| Not ticked (1) | TC-004-23 (restricted SYSTEM token; counted as not executed) |

Blocks: A to C (recording, stopwatch accuracy, Reports page, live update) passed in full. D passed except the optional cases TC-004-21 (unexpected service stop) and -22 (sleep). E: TC-004-24, -27 and -28 passed; -23 not ticked; -25 and -26 skipped. F (account deletion, clock change, 90-day limit, midnight, next day, CPU load) was not executed.

The run file header fields (execution date, installer file dates) and most per-case Notes blanks were left empty. The results above rest on the ticked result boxes.

## 3. Acceptance Criteria

| Status | ACs |
|---|---|
| Verified manually | AC-1, AC-2, AC-3, AC-6, AC-7, AC-13, AC-17, AC-20, AC-21, AC-22, AC-24, AC-26, AC-27 |
| Partly verified | AC-4 (real Explorer special case TC-004-29 skipped), AC-5 (renamed copy TC-004-26 skipped), AC-8 (network-share fallback name TC-004-25 skipped), AC-9 (untick and re-install: Pass; purge on account deletion TC-004-30 skipped), AC-10 (reason "service stopped unexpectedly" TC-004-21 skipped), AC-12 (sleep TC-004-22 skipped), AC-15 (unexpected stop and sleep TC-004-21, -22 skipped), AC-18 and AC-19 (today and recent days: Pass; older days, 90-day limit and day rollover TC-004-32, -34 skipped), AC-23 (deleted account disappears TC-004-30 skipped) |
| **Not verified manually** | AC-11 (90-day retention, TC-004-32), AC-14 (usage split at midnight, TC-004-33), AC-16 (clock change, TC-004-31), AC-25 (CPU load with 10 apps, TC-004-35) |

**Security checks (no AC):** TC-004-24 (kid cannot end the agent), -27 (log flooding limit), -28 (debug switches inert in Release) passed. Not executed: TC-004-23 (restricted SYSTEM token, plan Q-9; so it is still open which write-restriction variant runs), -25 (network-share program), -26 (renamed copy named like EagleEye), -29 (agent restart after admin kill).

## 4. Issues

| Issue | Severity | Status | Found / verified |
|---|---|---|---|
| `docs/requirements/user-stories/US-004/issues/ISSUE-007.md`: accent-colour heading bars on *Berichte* and *Einstellungen*, *Berichte* as the start page | Low (change request) | Verified/Closed (fix in 0.4.1; visual re-test passed per Michael, 2026-10-10) | Change request after run 01 / visual re-test on 0.4.1 |

No failures were reported in run 01.

## 5. Notes and Follow-ups

1. **Untested ACs and security checks.** AC-11, AC-14, AC-16 and AC-25 and the security checks TC-004-23, -25, -26 and -29 rest on unit tests and DEV's smoke check only. Proposal: include them in a later manual run that touches the service, or in the next regression run. TC-004-23 also answers ARC's open question about which write-restriction variant runs (plan Q-9).
2. **ISSUE-007 scope of the re-test.** DEV smoke-checked dark mode at 150 % with an orange-red accent colour. Michael checked the visuals on 0.4.1 and approved them. Light mode, a light accent colour (black text) and a live accent-colour change were not reported separately. iOS and macOS use the fallback colour `#1E7B3A` until a platform reader exists.
3. **Deliberate layout change in 0.4.1.** The *Berichte* and *Einstellungen* pages now use the full content width; before 0.4.1 they were capped at 760 units. Heading text is regular weight, as in the mockup.
4. **Delivered builds.** `03_Delivery/windows/EagleEye-Setup-0.4.1.exe` (SHA-256 `2565b31e…1fbe`) and `03_Delivery/windows/EagleEye-ParentApp-Setup-0.4.1.exe` (SHA-256 `274d82fc…d472`); full hashes in the implementation report.
5. **Evidence.** `evidence/visuals_1.png` and `evidence/visuals_2.png` (change request mockups).

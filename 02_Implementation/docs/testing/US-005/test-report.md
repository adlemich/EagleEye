# Test Report: US-005 — Break Times (Ruhezeiten)

**Status**: Final — approved by Michael, 2026-10-11
**Date**: 2026-10-11
**Author**: Orchestrator (recorded from `test-run-01.md` and Michael's decision; no separate TES evaluation, at Michael's direction)
**Test runs evaluated**: `docs/testing/US-005/test-run-01.md` (build 0.5.0)

---

## 1. Verdict

**Closed by Michael (2026-10-11), "no changes required".** Michael ran test run 01 on build 0.5.0 and got **22 Pass, 0 Fail, 0 Blocked, 13 Skipped** (35 cases, all ticked). His general feedback: "Seems to work fine." No issues were raised. Blocks A to C (the Rules page, blocking at app start with graceful close and force, the kid's dialog, per-account isolation) passed in full. The ACs in §3 that were not verified manually rest on DEV's unit tests and smoke check (`docs/requirements/user-stories/US-005/implementation-report.md`).

## 2. Case Tally (run 01)

| Result | Cases |
|---|---|
| Pass (22) | TC-005-01 to -20, -24, -25 |
| Skipped (13) | TC-005-21, -22, -23, -26, -27, -28, -29 (optional), -30, -31 to -35 (optional) |

Blocks:
- **A** (upgrade, Regeln page), **B** (blocking, timing, dialog, force) and **C** (other accounts, exemptions, sign-out): all passed.
- **D**: TC-005-24 (changed text) and -25 (re-install, no parent app) passed. Skipped: -21 (minute boundaries, weekday, overlap), -22 (tray client not running), -23 (two parent apps).
- **E** (security): not executed.
- **F** (account deletion, optional extras): not executed.

The run file header (execution date) and the per-case Notes blanks (lap times, "closed gracefully after … s", focus) were left empty. The results rest on the ticked result boxes.

## 3. Acceptance Criteria

| Status | ACs |
|---|---|
| Verified manually | AC-1 to AC-16, AC-24, AC-26, AC-28, AC-29, AC-30, AC-31, AC-32, AC-34 |
| Partly verified | AC-5 (reconnect: Pass; second parent app TC-005-23 skipped), AC-17 (app/service restart and re-install: Pass; reboot TC-005-35 skipped), AC-20 (per account, untick/re-tick: Pass; account deletion TC-005-30 skipped), AC-21 (in-break decision: Pass; exact minute boundaries, weekday and overlap TC-005-21 skipped), AC-22 (graceful close, force after 20 s, all processes of Paint/Notepad: Pass; start from another app, Store app, Edge TC-005-31 to -33 skipped), AC-23 (other accounts, shell, sign-out: Pass; tray client never blocked TC-005-22 and shut-down TC-005-35 skipped), AC-25 (running app keeps running and keeps counting: Pass; 19:59 boundary TC-005-21 skipped), AC-27 (after service restart and without parent app: Pass; after reboot skipped), AC-36 (no usage: Pass; purge with the account TC-005-30 skipped; 90-day purge unit-tested only) |
| **Not verified manually** | AC-18 (save error and revert), AC-19 (two parent apps in sync), AC-33 (tray client not running), AC-35 (blocked-start history in the database; only the log line was seen in Block B), AC-37 (time-zone/clock findings) |

Known limits of manual testing (test plan): keyboard focus of the dialog is best effort (AC-30); title and button in an English Windows were not checked on the German PC (AC-31).

**Security checks (no AC):**
- Passed: graceful close under the real SYSTEM token (plan Q-8, TC-005-12: "closed gracefully"); termination across sessions (TC-005-16).
- Not executed:
  - **TC-005-26**, the agent-token rerun. It was skipped in US-004 too, so it is still unknown whether the agent runs write-restricted (US-004 plan Q-9).
  - **TC-005-27**: the kid cannot end the enforcement.
  - **TC-005-29**: a copied tray client is not trusted.

## 4. Issues

No issues were raised in run 01.

DEV fixed one defect during implementation that also affected the released 0.4.x build: the parent app crashed when the service restarted while a second or later account was selected (implementation report D-13; the Reports page had it too). It is fixed in 0.5.0, and TC-005-09 ("no crash") passed. No issue file was created, because it was found and fixed before the test run.

## 5. Notes and Follow-ups

1. **Untested ACs and security checks.** AC-18, AC-19, AC-33, AC-35, AC-37 and the security cases TC-005-26, -27 and -29 rest on unit tests and DEV's smoke check. DEV saw the save-error path, the tray-not-running path, two parent apps and the history rows in its smoke check (implementation report). Proposal: include TC-005-21 to -23 and -26 to -30 in the next story's run that touches the service, or in the next regression run.
2. **Agent token still open.** TC-004-23 / TC-005-26 has now been skipped twice. ARC's question (does the agent run write-restricted, or with the documented fallback?) stays open. The graceful close works under the real token either way (TC-005-12).
3. **Clean-up.** Test plan Q-5 asked to switch off all break entries of `eagleeye-kid` after the run. Not recorded in the run file: Michael, check that no entry of `eagleeye-kid` is still on, unless you want it to apply.
4. **Delivered builds.** `03_Delivery/windows/EagleEye-Setup-0.5.0.exe` (SHA-256 `83bf5c55…151830`) and `03_Delivery/windows/EagleEye-ParentApp-Setup-0.5.0.exe` (SHA-256 `a03245d5…b217a1`); full hashes in the implementation report.
5. **Next story.** Michael's clarification 1: apps already running when a break starts are handled in the next story ("approaching a usage limitation"), together with process-based enforcement (plan Q-3) and, possibly, the time-zone hardening (plan Q-2).

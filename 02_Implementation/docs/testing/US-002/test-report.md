# Test Report: US-002 — Windows Parent App: Installation, Connection and Pairing

**Status**: Draft
**Date**: 2026-10-04
**Author**: TES
**Test runs evaluated**: `test-run-01.md` (build 0.2.0). `test-run-02.md` prepared (rebuilt service installer 0.2.0), not yet executed.

---

## 1. Verdict

**Not ready to close.** In test run 01 the pairing code popup was too narrow to show the code (ISSUE-004, Critical). Pairing could not be completed, so the run was aborted at TC-002-12, and 31 of 48 cases were not executed.

**Status after run 01 (update 2026-10-04):**

- **ISSUE-004** (Critical) is `Implemented`. DEV rebuilt `03_Delivery/windows/EagleEye-Setup-0.2.0.exe` (same version 0.2.0, file dated 2026-10-04 20:53). Only the tray client's pairing code window changed. The parent app installer `03_Delivery/windows/EagleEye-ParentApp-Setup-0.2.0.exe` is **unchanged**. See the issue's Resolution section and `docs/requirements/user-stories/US-002/implementation-report.md` §8.
- **ISSUE-005** is **reclassified as not a product defect**. Michael's terminal was not elevated. The test plan was fixed (S-8, TC-002-02, `test-plan.md` §9). Status `Implemented`; it is closed when TC-002-02 passes in run 02.
- Verdict stays **Not ready to close** until run 02 has been executed and evaluated.

**Re-test: `test-run-02.md` (created 2026-10-04).** It contains no regression cases. Per Michael's decision (2026-10-04) and the updated process (`docs/testing/README.md`, commit 236fbbf), regression runs only on his explicit request before a major version release.

Run 02 covers 36 cases:

| Cases | Reason |
|---|---|
| TC-002-01 | Rebuilt service installer: same-version reinstall over the installed 0.2.0. It also installs the fix, so it must run first. It checks that network access (port, firewall rule) and the service survive the reinstall. |
| TC-002-02 | Fail in run 01 (ISSUE-005). Repeated in an elevated terminal. |
| TC-002-03 | Skipped in run 01. It checks the actual protection goal of the certificate ACL. |
| TC-002-11, TC-002-12 | TC-002-12 Fail (ISSUE-004). Checked at 150 % in the Admin **and** the Kid session. TC-002-11 is repeated because it triggers the code. |
| TC-002-13 to TC-002-43 (rest of Block A, Blocks B, C, D) | Not executed in run 01. |
| TC-002-04 to TC-002-10 | **Not repeated.** The parent app installer did not change, so their run 01 Pass stands. |

## 2. Environment

| Item | Value |
|---|---|
| Machine(s) | Windows Developer Machine (service PC). PC2 not used (Block C not reached). |
| OS version(s) | Windows 11, German UI (exact version not recorded in the run file) |
| Build / installer version | `03_Delivery/windows/EagleEye-Setup-0.2.0.exe` (service + tray), `03_Delivery/windows/EagleEye-ParentApp-Setup-0.2.0.exe` (parent app), installed over EagleEye 0.1.1 |
| Accounts | Admin (Michael), `eagleeye-kid` |
| Execution date | 2026-10-04 (from the evidence file name; not filled in the run file) |

### Case tally (run 01)

| Block | Cases | Pass | Fail | Blocked | Skipped | Not executed |
|---|---|---|---|---|---|---|
| A (TC-002-01..16) | 16 | 9 | 2 | 0 | 1 | 4 |
| B (TC-002-17..32) | 16 | 0 | 0 | 0 | 0 | 16 |
| C (TC-002-33..40) | 8 | 0 | 0 | 0 | 0 | 8 |
| D (TC-002-41..43) | 3 | 0 | 0 | 0 | 0 | 3 |
| Regression (REG-001-01..05) | 5 | 2 | 0 | 0 | 3 | 0 |
| **Total** | **48** | **11** | **2** | **0** | **4** | **31** |

- **Pass**: TC-002-01, -04, -05, -06, -07, -08, -09, -10, -11; REG-001-01, REG-001-02.
- **Fail**: TC-002-02 (ISSUE-005), TC-002-12 (ISSUE-004).
- **Skipped** (by Michael, no reason recorded): REG-001-03, -04, -05, TC-002-03 (the whole first Kid-session block).
- **Not executed** (run aborted after TC-002-12): TC-002-13 to TC-002-43. Setup S-9 to S-11 and the cleanup list were not done.

## 3. Acceptance Criteria Results

"Open" = not all cases mapped to the AC were executed. No AC is final yet.

| AC | Test case(s) | Result after run 01 | Notes |
|---|---|---|---|
| AC-1 | TC-002-04 ✓, -15, -33, -36, -41 | Open | Installer works on the service PC; pairing parts not executed |
| AC-2 | TC-002-33 | Not tested | Block C |
| AC-3 | TC-002-04 ✓, -05 ✓ | **Pass** | |
| AC-4 | TC-002-18 | Not tested | |
| AC-5 | TC-002-31, -32, -40, -43 | Not tested | |
| AC-6 | TC-002-01 ✓, -34 | Open | Service side Pass (port, firewall rule); LAN reach from PC2 not tested |
| AC-7 | TC-002-29, -35 (+ rule for all cases) | Open | No certificate prompt was reported in the executed cases |
| AC-8 | TC-002-07 ✓, -15, -16, -22 | Open | |
| AC-9 | TC-002-06 ✓, -08 ✓, -17, -18, -32 | Open | |
| AC-10 | TC-002-07 ✓, -11 ✓, -15, -19, -28 | Open | |
| AC-11 | TC-002-06 ✓, -07 ✓, -24, -28, -32 | Open | |
| AC-12 | TC-002-09 ✓, -10 ✓ | **Pass** | Time until error (TC-002-09) not recorded |
| AC-13 | TC-002-11 ✓, -26 | Open | Event Log path not tested |
| AC-14 | TC-002-12 ✗, -36 | **Fail** | ISSUE-004: code and text cut off |
| AC-15 | TC-002-26 | Not tested | |
| AC-16 | TC-002-15, -28 | Not tested | Blocked in practice by ISSUE-004 |
| AC-17 | TC-002-13 | Not tested | |
| AC-18 | TC-002-14 | Not tested | |
| AC-19 | TC-002-27 | Not tested | |
| AC-20 | TC-002-11 ✓, -14, -25 | Open | Red, not "Verbunden", while pairing (TC-002-11) |
| AC-21 | TC-002-17, -38 | Not tested | |
| AC-22 | TC-002-19, -21, -38 | Not tested | |
| AC-23 | TC-002-15, -36, -38, -39 | Not tested | |
| AC-24 | TC-002-37 | Not tested | |
| AC-25 | TC-002-16 | Not tested | |
| AC-26 | TC-002-22, -39 | Not tested | |
| AC-27 | TC-002-23, -24, -39 | Not tested | |
| AC-28 | TC-002-25, -28 | Not tested | |
| AC-29 | TC-002-39, -42 | Not tested | |
| AC-30 | TC-002-20 | Not tested | |

Supporting checks (no AC): TC-002-02 **Fail** (ISSUE-005; later found to be caused by a non-elevated terminal, not by the product), TC-002-03 Skipped, TC-002-30 not executed.

## 4. Regression Results

| Story | Cases | Pass | Fail | Not executed |
|---|---|---|---|---|
| US-001 | REG-001-01..05 | 2 (REG-001-01, -02) | 0 | 3 (REG-001-03..05 skipped) |

Regression is not part of run 02 or later story runs (Michael, 2026-10-04). It runs only on his explicit request before a major version release.

## 5. Issues

| Issue | Severity | Status | Test case |
|---|---|---|---|
| `docs/requirements/user-stories/US-002/issues/ISSUE-004.md` — Pairing code popup is too small, code and text are cut off | Critical | Implemented (DEV fix, commit c26bf34) | TC-002-12 (AC-14) |
| `docs/requirements/user-stories/US-002/issues/ISSUE-005.md` — Administrator gets "Zugriff verweigert" on the service certificate folder | Medium as filed; **not a product defect** | Implemented (test-plan fix) | TC-002-02 (supporting) |

- **ISSUE-004 (Critical)** blocks the core flow. Every later case needs a paired app. There is no practical workaround: the tray client also runs in admin sessions (Q-7), so the Event Log path (AC-15) is not used while anyone is signed in. **Update:** the screenshot was taken in the Admin session at 150 % display scaling (Michael). DEV reproduced the defect at 150 % and fixed the window layout so that it scales with the DPI and sizes itself to its content. The service installer was rebuilt (same version 0.2.0). Re-test: TC-002-11 and TC-002-12 in run 02, at 150 % in both sessions.
- **ISSUE-005 (Medium as filed)** was a test-plan problem: the terminal was not elevated (Michael). Under UAC, the refusal is the intended behaviour of the `certs\` ACL. TES added the elevation hint to S-8 and TC-002-02. Re-test: TC-002-02 in run 02.

## 6. Feedback for PRO / ARC

Michael's run file has no notes or General Feedback. TES has these points:

1. **ARC/TES – TC-002-02 wording** (see ISSUE-005). If the cause is a non-elevated terminal, TES changes the step to "Terminal (Administrator)". ARC confirms whether *Administratoren* should be able to read `certs\` from an elevated terminal. The design (ADR-008 §7) says yes.
   **Answer (Michael, 2026-10-04):** "The terminal was not in Admin mode, so a simple hint in the test instructions will do the fix." → Done: S-8 and TC-002-02 now require an elevated terminal (`test-plan.md` §9). No ARC action needed; the design statement stands and is verified by TC-002-02 in run 02.
2. **TES → Michael – missing observations** (please add them in run 02, not in run 01):
   - TC-002-09: seconds until the connection error. *Open; TC-002-09 is not repeated in run 02. Optional.*
   - TC-002-11: what the device name field was prefilled with. *Asked again in run 02 (TC-002-11 Notes).*
   - TC-002-12: whether a pairing popup also appeared in the Admin session, and in which session the screenshot was taken. **Answer (Michael, 2026-10-04):** the screenshot is from the **Admin session**, with the tray client running there. So the popup does appear in the Admin session.
   - Display scaling of the service PC (for ISSUE-004). **Answer (Michael, 2026-10-04):** **150 %** in his admin account.
   - Windows version, and the reason the first Kid-session block was skipped (REG-001-03..05, TC-002-03). *Windows version: asked in the run 02 header. The skipped regression cases do not matter any more (no regression in story runs). TC-002-03 is in run 02.*
3. **TES – scope of run 02:** without regression, by Michael's decision (see §1).
4. **For information (from DEV, not tested in this story):** the US-001 *App Infos* dialog of the tray client uses the same fixed-pixel layout. At 150 % it is still readable (the OK button is tight); at 200 % DEV saw the connection-error text touch the OK button. It was not fixed in ISSUE-004. If Michael sees it clipped, TES files a separate issue. Run 02 asks for such observations in General Feedback.

No unspecified product behaviour was reported, so there is nothing for PRO in this run.

## 7. Regression Checklist Update

Not yet. TES proposes the cases when the story is ready to close.

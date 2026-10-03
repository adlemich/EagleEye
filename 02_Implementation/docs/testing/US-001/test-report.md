# Test Report: US-001 — Basic Service Installation and Tray Client Connectivity

**Status**: Draft — awaiting re-test (test-run-02) and Michael's approval
**Date**: 2026-10-03
**Author**: TES
**Test runs evaluated**: `test-run-01.md`

---

## 1. Verdict

**Not ready to close yet.** All 12 original acceptance criteria passed functionally in run 01. Build 0.1.1 fixes ISSUE-001 (localization), ISSUE-002 (publisher) and ISSUE-003, which became the new AC-13. These fixes must be confirmed in `test-run-02.md`.

## 2. Environment

| Item | Value |
|---|---|
| Machine | Windows Developer Machine, Windows 11 Pro (German UI) |
| Build / installer | `EagleEye-Setup-0.1.0.exe` (service version `EagleEye_v0.1`) |
| Accounts | Admin (Michael), standard user `eagleeye-kid` |

## 3. Acceptance Criteria Results (run 01)

| AC | Test case(s) | Result | Notes |
|---|---|---|---|
| AC-1 | TC-001-01 | Pass | Publisher shown as "adlemich" → ISSUE-002 |
| AC-2 | TC-001-02 | Pass | |
| AC-3 | TC-001-12 | Pass | |
| AC-4 | TC-001-03 | Pass | |
| AC-5 | TC-001-04, TC-001-13 | Pass | |
| AC-6 | TC-001-05, TC-001-08 | Pass | English texts → ISSUE-001 |
| AC-7 | TC-001-05, TC-001-13 | Pass | English texts → ISSUE-001 |
| AC-8 | TC-001-08 | Pass | Red after ~15 s; English texts → ISSUE-001 |
| AC-9 | TC-001-10, TC-001-11 | Pass | Also works after a 2-minute outage |
| AC-10 | TC-001-07 | Pass | `EagleEye_v0.1` matches installer 0.1.0 |
| AC-11 | TC-001-06 | Pass | English "About" instead of "App Infos" → ISSUE-001 |
| AC-12 | TC-001-07 | Pass | |
| — | TC-001-09 (exploratory) | Acceptable | Improvement suggestion → ISSUE-003 (PRO) |

Totals: 13 executed, 13 Pass, 0 Fail, 0 Blocked, 0 Skipped.

## 4. Regression Results

None. US-001 is the first story.

## 5. Issues

| Issue | Severity | Status | Routed to | Test case |
|---|---|---|---|---|
| `US-001/issues/ISSUE-001.md`: tray texts English only | Medium | Implemented (awaiting re-test) | DEV | TC-05, 06, 08, 09 |
| `US-001/issues/ISSUE-002.md`: publisher "adlemich" | Low | Implemented (awaiting re-test) | DEV | TC-01 |
| `US-001/issues/ISSUE-003.md`: richer connection-error text in About | Low | Implemented (awaiting re-test); became AC-13 | PRO → DEV | TC-09 |

## 6. Feedback for PRO / ARC

- **PRO, US-001 AC wording**: done 2026-10-03. AC-11/AC-12 now refer to the application-information entry with English examples, plus a note that all quoted UI texts are English examples shown in the user's language (Michael's decision).
- **PRO, ISSUE-003**: done. Michael decided to add AC-13 to US-001 and fix it in 0.1.1.
- **PRO, timing**: AC-8/AC-9 have no time bounds. Run 01 measured about 15 s to detect a stop. TES used ≤ 30 s / ≤ 60 s; consider writing such bounds into the AC.
- **ARC**: localization was a known NFR, but the US-001 plan did not mention it, so DEV did not implement it. Future plans should list the localization impact for every user-facing text.

## 7. Regression Checklist Update

To be added when US-001 is closed: TC-001-01 (install), TC-001-02 (service registration), TC-001-05 (green + localized tooltip), TC-001-07 (About version), TC-001-08/10 (red/green on stop/start).

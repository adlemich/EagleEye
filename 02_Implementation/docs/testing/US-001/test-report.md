# Test Report: US-001 — Basic Service Installation and Tray Client Connectivity

**Status**: Approved by Michael, 2026-10-03
**Date**: 2026-10-03
**Author**: TES
**Test runs evaluated**: `test-run-01.md` (build 0.1.0), `test-run-02.md` (build 0.1.1)

---

## 1. Verdict

**Ready to close — closed by Michael on 2026-10-03.** All 13 acceptance criteria (12 original + AC-13) pass in build 0.1.1. ISSUE-001, ISSUE-002 and ISSUE-003 are verified and closed.

## 2. Environment

| Item | Value |
|---|---|
| Machine | Windows Developer Machine, Windows 11 Pro (German UI) |
| Build / installer | Run 01: `EagleEye-Setup-0.1.0.exe`; run 02: `EagleEye-Setup-0.1.1.exe` (upgrade install). Service version `EagleEye_v0.1` |
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

Totals run 01: 13 executed, 13 Pass.

### Run 02 (build 0.1.1, re-test)

| Case | Verifies | Result |
|---|---|---|
| TC-001-R01 | Upgrade 0.1.0 → 0.1.1, publisher "Michael Adler" (ISSUE-002) | Pass |
| TC-001-02 | AC-2, AC-3 after upgrade | Pass |
| TC-001-05 | AC-6, AC-7, German tooltip (ISSUE-001) | Pass |
| TC-001-06 | AC-11 "App Infos" (ISSUE-001) | Pass |
| TC-001-07 | AC-10, AC-12 in German | Pass |
| TC-001-08 | AC-8, "Verbindungsfehler" tooltip | Pass |
| TC-001-09 | **AC-13** connection error with `localhost:5080` (ISSUE-003) | Pass |
| TC-001-10 | AC-9, reconnect | Pass |
| TC-001-R02 | English texts by hand (optional) | Skipped; covered by unit tests |

Totals run 02: 8 Pass, 1 Skipped, 0 Fail.

## 4. Regression Results

None. US-001 is the first story.

## 5. Issues

| Issue | Severity | Status | Routed to | Test case |
|---|---|---|---|---|
| `US-001/issues/ISSUE-001.md`: tray texts English only | Medium | Verified/Closed | DEV | TC-05, 06, 08, 09 |
| `US-001/issues/ISSUE-002.md`: publisher "adlemich" | Low | Verified/Closed | DEV | TC-01 |
| `US-001/issues/ISSUE-003.md`: richer connection-error text in About | Low | Verified/Closed; became AC-13 | PRO → DEV | TC-09 |

## 6. Feedback for PRO / ARC

- **PRO, US-001 AC wording**: done 2026-10-03. AC-11/AC-12 now refer to the application-information entry with English examples, plus a note that all quoted UI texts are English examples shown in the user's language (Michael's decision).
- **PRO, ISSUE-003**: done. Michael decided to add AC-13 to US-001 and fix it in 0.1.1.
- **PRO, timing**: AC-8/AC-9 have no time bounds. Run 01 measured about 15 s to detect a stop. TES used ≤ 30 s / ≤ 60 s; consider writing such bounds into the AC.
- **ARC**: localization was a known NFR, but the US-001 plan did not mention it, so DEV did not implement it. Future plans should list the localization impact for every user-facing text.

## 7. Regression Checklist Update

Added to `docs/testing/regression-checklist.md` on 2026-10-03 (section US-001).

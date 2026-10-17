# Test Report: US-006 — App Time Budgets and Time-elapsed Warnings

**Status**: Final — story closed by Michael, 2026-10-17 ("Close as tested, then merge"), without recorded test results
**Date**: 2026-10-17
**Author**: TES
**Test runs evaluated**: `docs/testing/US-006/test-run-01.md`. **Not executed**: no result was recorded.
**Builds in use during the story's live use**: 0.6.0 → 0.6.1 (ISSUE-008, SignalR 10.0.13) → **0.6.2** (ISSUE-009). The final build is `03_Delivery/windows/EagleEye-Setup-0.6.2.exe` (SHA-256 `9D1942E7…11D776`) and `03_Delivery/windows/EagleEye-ParentApp-Setup-0.6.2.exe` (SHA-256 `F1E8179F…44E0FC`). Full hashes are in `docs/requirements/user-stories/US-006/implementation-report.md` §11.

---

## 1. Verdict

**Closed by Michael (2026-10-17) without a recorded test run.** Michael decided to close US-006 and merge it before he recorded any result in run 01.

- **Test run 01 has no recorded results.** None of the 42 cases is ticked. No Observed or Notes entry was written, no General Feedback was given, and no service logs were copied (`evidence/` does not exist). Under Rule 6 ("Do not invent results"), every case counts as **not executed**.
- **No acceptance criterion is verified manually.** All 39 ACs rest only on DEV's unit tests (2 652 passed at 0.6.2) and on DEV's smoke check. That smoke check did not control any session (report D-15).
- Two defects were found and fixed during Michael's live use (§4). They show that the parent app was used on 0.6.0 and 0.6.1. They are not recorded test results.
- **Test plan approval**: Michael did not approve `docs/testing/US-006/test-plan.md` explicitly. He approved it implicitly by starting to test. Plan questions **Q-1 to Q-7** (§8 of the plan: LEOSERV as App B, roles of the new accounts, English language pack, must-run/optional split, time-zone change, midnight case, deny-by-default handling) were **not answered explicitly**.

TES's own assessment, for the record: under the normal rules (`02_Implementation/agents/tes/CLAUDE.md`, Workflow step 6) this story would **not** be "Ready to close", because no AC has passed. The closure is Michael's decision under the orchestrator rule "Michael closes with partial runs". This report records what is untested before the merge.

## 2. Case Tally (run 01)

| Result | Cases |
|---|---|
| Pass | 0 |
| Fail | 0 |
| Blocked | 0 |
| Skipped | 0 |
| **Not executed** | **42** (TC-006-01 to TC-006-42; 32 must-run, 10 optional) |

The setup steps (S-1 to S-16) and the preparation steps (P-1 to P-5) are not ticked either. The run file still names the 0.6.0 installers. A new run against 0.6.2 would need a new run file (test-run-02), because records are permanent.

## 3. Acceptance Criteria

| Status | ACs |
|---|---|
| Verified manually | — none |
| Partly verified | — none |
| **Not verified manually** | **AC-1 to AC-39 (all)** |

Details by group. Each line names what is not verified and the planned case.

| Group | ACs | Not verified (planned case) | Remarks |
|---|---|---|---|
| A. Rules section | AC-1 to AC-6 | Layout, columns, sorting, deny by default for existing and new apps, live row ≤ 15 s, page states, light/dark, 150 % (TC-006-02, -08, -09, -14) | During live use Michael saw the section and the empty numeric fields (ISSUE-008). That is an observation, not a result. **Open, not verified**: the 150 % rendering of the fixed fields, and the "–" look of unticked checkboxes on LEOSERV (§5). |
| B. Editing | AC-7 to AC-12 | Edit in place, validation 0..23 / 0..59, 0:00 blocks, budget text with emojis and default, log entries, save error (TC-006-04, -05, -07, -11, -12) | AC-12 had only an optional case. |
| C. Warnings section | AC-13 to AC-16 | Section, defaults 5 / 1, validation 1..59 and final < early, texts (TC-006-03, -06, -07) | The minute fields were invisible in 0.6.0 (ISSUE-008, fixed in 0.6.1). |
| D. Parent apps, storage | AC-17, AC-18 | Two apps in sync; per account, untick, purge on deletion, survives restart, reboot and re-install (TC-006-08, -10, -30, -33, -37) | |
| E. Start check | AC-19 to AC-22 | Blocking by app rule (not allowed today, budget used up), allowed start, break wins, warning at start (TC-006-12 to -17, -24) | **Never run live** (D-15). |
| F. Running apps | AC-23 to AC-27 | Countdown = Reports, early/final warning once, close at budget end (graceful, force after 20 s), other accounts never limited, exemptions (TC-006-15 to -20, -29, -31) | **Never run live** (D-15). |
| G. Break times | AC-28 to AC-31 | Break warnings, closing every running app at the break start (replaces US-005 AC-25), app started just before a break, one end = one set of warnings (TC-006-23 to -25) | **Never run live** (D-15). |
| H. Changes during the day | AC-32, AC-33 | Grace period of the full final-warning minutes after a rule or break change (Q-2), early warning at once without grace, no repeat after a raise (Q-5) (TC-006-18 to -21, -26) | **Never run live** (D-15). |
| I. Kid dialog, tray, records | AC-34 to AC-38 | Info line, newer message replaces the text, tray not running, enforcement without parent app and after restart, log and history entries, CPU with 10 apps (TC-006-12, -15, -32, -33, -36) | **Never run live** (D-15). Includes the English default texts in the kid dialog (TC-006-30, plan R-10). |
| J. Time zone and clock | AC-39 | Warning with old/new zone and date, budgets follow the new day (TC-006-35, -40) | |

**Enforcement paths never tested in a real session** (implementation report D-15, §6 risk 1): blocked start by app rule, early and final warnings, graceful and forced close of running apps, grace periods after parent changes, closing all apps at a break start, the end at midnight, the kid dialog with info line and text replacement, and the CPU load with 10 allowed apps. The first real use by `kid1` or by Michael's kids will be the first live test of these paths.

**Security check without an AC**: TC-006-34 (rerun of TC-005-26 / TC-004-23, agent token) was not executed. It has now been skipped **three times** (US-004, US-005, US-006). It is still unknown whether the session agent runs write-restricted or with the documented fallback. The graceful close of running apps by budget (TC-006-15, plan Q-4) was not checked either.

## 4. Issues

Both issues were found during Michael's live use on 2026-10-17, not in a recorded test run. They are filed in `docs/testing/US-006/issues/` (not in the standard location `docs/requirements/user-stories/US-006/issues/`).

| Issue | Title | Severity | Status | Fixed in |
|---|---|---|---|---|
| `docs/testing/US-006/issues/ISSUE-008.md` | Numeric fields of the app rules and the warning levels show no value (Windows parent app) | Medium | **Implemented**, not verified by Michael on an installed build | 0.6.1 (also SignalR and Microsoft 10.0.x packages 10.0.12 → 10.0.13 for advisory GHSA-334f-vvp4-7g6p, report §10 D-18) |
| `docs/testing/US-006/issues/ISSUE-009.md` | Parent app stuck in "Gekoppelt" after the service identity changed; the pairing cannot be removed | High | **Implemented**, not verified by Michael on an installed build | 0.6.2 (local "Kopplung auf diesem Gerät löschen"; changes approved US-002 behaviour, report §11 D-19) |

DEV verified both fixes with Debug builds and screenshots (in the same folder). No re-test against the 0.6.1 or 0.6.2 installers was recorded, so neither issue is set to `Verified/Closed`. A short confirmation from Michael would close both: the values are visible, and the forget/re-pair flow on `ZOCK-O-MAT-V3` works.

## 5. Risks Carried into `main`

1. **Untested enforcement** (D-15). Budgets, warnings, grace periods and break-start closes have never run against a real kid session. Defects in timing, in the kill set of running apps (TI-9), or in the message replacement would first show up in real use.
2. **Deny by default** (OQ-2, plan R-1). Right after installing 0.6.x, every app of every controlled account is blocked until the parent sets rules. This hits Michael's real kid accounts if they are ticked.
3. **Graceful close under the real agent token** (plan Q-4, R-3). Not checked for running apps. If posting `WM_CLOSE` fails, every close becomes "terminated by force after 20 s", and the kid gets no "save?" prompt. The app is still closed on time.
4. **English default texts** (plan R-10, report D-16). Not checked on an installed build. If the `en` satellite is missing, English kids see the German defaults.
5. **Parent app on LEOSERV**: the 150 % rendering of the fixed numeric fields (report D-17) and the "–" look of unticked checkboxes are open observations. DEV could not reproduce the checkbox look on `ZOCK-O-MAT-V3` (ISSUE-008, side observation).
6. **Changed US-002 behaviour** (ISSUE-009, D-19). A pairing can now be forgotten locally. The device entry may remain on the service, and no parent app can remove it there yet.
7. **Accepted by design, not observed**: a service restart may repeat warnings and ends a running grace period at once (D-5, D-6, R-9). The parent app has no file log, so the "forget pairing" log entry is not persisted in Release (D-20).

## 6. Follow-ups

| # | Follow-up | Owner | When |
|---|---|---|---|
| F-1 | **TC-005-26 / TC-006-34 agent-token check** (skipped three times). It is the only answer to US-004 plan Q-9. Run it in the next story's run that touches the service, or make it a must in the next regression run. | TES / Michael | next service story or regression |
| F-2 | **Live enforcement cases** of the US-006 plan (TC-006-12 to -26, -30, -32, -33, -35, -36). Proposal: a short focused run against 0.6.2 (test-run-02), or include them in the next story that touches enforcement. Adding US-006 cases to `docs/testing/regression-checklist.md` only makes sense once they have passed once. | TES / Michael | before the next major release at the latest |
| F-3 | **LEOSERV checks**: numeric fields at 150 % (ISSUE-008, D-17), and the "–" look of unticked checkboxes compared with `ZOCK-O-MAT-V3`. | Michael (report to TES/DEV) | next session on LEOSERV |
| F-4 | **ISSUE-008 / ISSUE-009 confirmation** on the installed 0.6.2, then set both to `Verified/Closed`. | Michael, TES | next session |
| F-5 | **US-002 documentation update** for the ISSUE-009 behaviour change (local "forget pairing"; device entry may remain on the service). This covers the US-002 user story (AC-30, Q-3) and the architecture docs. Possibly a later story: let a parent app remove another device's entry on the service. | ARC / PRO | before or with the next pairing-related story |
| F-6 | **Issue location**: ISSUE-008 and ISSUE-009 are in `docs/testing/US-006/issues/`, while the TES rules say `docs/requirements/user-stories/US-XXX/issues/`. Orchestrator to decide whether to move them or to accept the location. | Orchestrator | at the merge |
| F-7 | **Regression note** (test plan §6): US-006 replaces US-005 AC-25. Future regression cases from US-005 (TC-005-15, -21) must not expect apps to keep running when a break begins. | TES | when US-005 cases are added to the regression checklist |
| F-8 | **Deny by default**: before Michael's kids use the PC on 0.6.2, set the rules of every controlled account. | Michael | before real use |

The story status is not changed here. PRO sets it on Michael's closure.

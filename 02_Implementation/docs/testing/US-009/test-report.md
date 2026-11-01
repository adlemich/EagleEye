# Test Report: US-009 — Windows Parent App on Windows 10, and Windows Parent App Parity (0.9.0)

**Status**: Accepted by Michael on 2026-11-01 by closing US-009 with ISSUE-010 as a known issue (under observation) and the open gaps as listed; the open points TES raised in its hand-back (TC-009-45 result, S-6 for kid2, severity Medium, §11 regression additions) were not answered separately
**Date**: 2026-11-01
**Author**: TES
**Test runs evaluated**: `02_Implementation/docs/testing/US-009/test-run-01.md` (results as committed by Michael in `87a6cc5`)
**Test plan**: `02_Implementation/docs/testing/US-009/test-plan.md`

---

## 1. Verdict

**Not ready to close by the TES rule (one AC failed, 13 ACs not or only partly verified). Michael's decision (2026-11-01): close US-009 with ISSUE-010 as a known issue under observation, without a DEV fix.**

No Critical or High issue is open. The one defect (ISSUE-010, Medium) is a wrong status-bar display; saving, the save error message and enforcement work. The core risks of this story passed: the first release run of the Windows app on Windows 11 and Windows 10, the service update with data kept, and the first live group enforcement in a kid session (US-008 retest). The coverage gaps are listed in §4 and §5. Michael accepts them when he closes the story (§10).

## 2. Environment

| Item | Value |
|---|---|
| Machine(s) | Service PC = Windows Developer Machine (Windows 11, `192.168.1.37` as used in all cases; S-3 not filled in); Windows 10 PC (22H2 per plan; winver not filled in); MacBook (macOS version not filled in) |
| Accounts | kid1, kid2, kid3 as in the plan; real names not filled in (S-5). kid2's English display language (S-6) is not ticked, but TC-009-60 passed without the "kid2 not English" note. |
| Build / installer version | 0.9.0: `03_Delivery/windows/EagleEye-Setup-0.9.0.exe` (SHA-256 `4F1EF797…CB1652`), `03_Delivery/windows/EagleEye-ParentApp-Setup-0.9.0.exe` (`780CE6CB…1D88F0`), `03_Delivery/macos/EagleEye-ParentApp-0.9.0.dmg` (`9f397006…c5fdde6`); full hashes in the test plan §2 |
| Execution dates | Not filled in (run committed 2026-11-01) |
| Evidence | None committed. No screenshots and no service-log copies (`evidence/` does not exist). The Session 0 and P-1 to P-6 checkboxes are unticked. Their preconditions are taken as met where the dependent cases passed. |

## 3. Results per Session

| Session | Cases | Pass | Fail | Blocked | Skipped | No result |
|---|---|---|---|---|---|---|
| 1 Service update | TC-009-01, -02 | 2 | 0 | 0 | 0 | 0 |
| 2 Windows app on Windows 11 | TC-009-03 to -25 | 21 | 0 | 0 | 2: -12, -25 *(opt)* | 0 |
| 3 Windows 10 PC | TC-009-26 to -36 | 7 | 1: **-29** | 0 | 3: -26, -35 *(opt)*, -36 *(opt)* | 0 |
| 4 Mac update, US-008 Mac ACs | TC-009-37 to -48 | 8 | 0 | 0 | 3: -40, -44 *(opt)*, -47 *(opt)* | 1: **-45** |
| 5 Two parent apps | TC-009-49 to -54 | 0 | 0 | 0 | 6: all | 0 |
| 6A Windows app regression | TC-009-55, -56 | 0 | 0 | 0 | 2: both | 0 |
| 6B Group enforcement | TC-009-57 to -67 | 9 | 0 | 0 | 2: -63, -66 *(opt)* | 0 |
| 6C Storage and accounts | TC-009-68 to -70 | 2 | 0 | 0 | 1: -70 *(opt)* | 0 |
| 6D Optional | TC-009-71, -72 | 0 | 0 | 0 | 2: both *(opt)* | 0 |
| **Total** | **72** | **49** | **1** | **0** | **21** (12 essential, 9 optional) | **1** |

- Optional cases executed and passed: TC-009-15, -18, -64, -65.
- **TC-009-45, no result recorded**: the line reads `X ] Pass` without the opening bracket, so no box is ticked. TES does not guess the result (TES rule 6). Michael should confirm it.
- **Tick inconsistency, flagged (not changed):** TC-009-18 is ticked **Pass**, but its Observed note describes the same status-bar defect as the Fail of TC-009-29. TC-009-18's own expected results (the save error text, the page back to the stored state) were met, so the Pass stands. The note is filed as the Windows 11 occurrence of ISSUE-010.
- Notes and General Feedback: no other notes were written.

## 4. Acceptance Criteria Results

Legend: **Verified** = every essential case passed. **Failed** = a case failed. **Not verified** = the AC, or a named part of it, rests on skipped or unrecorded cases. *(opt)* = optional case.

| AC | Test case(s) | Result | Notes |
|---|---|---|---|
| AC-1 parity rule (W11) | TC-009-04, -06 to -24, -51, -52 | **Not verified (partly)** | Most US-008 parent-app ACs pass on W11. Open: US-008 AC-7, AC-8 (create, name rules; TC-009-12 skipped), US-008 AC-18 (Rules live after a group change; TC-009-51 skipped), US-008 AC-37 group-change part (TC-009-52 skipped) |
| AC-2 install on W10 | TC-009-27 | Verified | |
| AC-3 Apps & Features, repair, uninstall | TC-009-27, -33, -34 | Verified | |
| AC-4 older Windows refused | *TC-009-35 (opt)* | **Not verified** | Skipped (no such Windows). The story allows "not tested". |
| AC-5 service installer refuses W10 | TC-009-26 | **Not verified** | Rests only on the skipped TC-009-26. Seen before only in DEV's pre-check (implementation report §9, rows 1, 2), outside a test run |
| AC-6 connection from W10 | TC-009-28, -29 | **Failed, accepted as a known issue** | TC-009-28 Pass (unreachable host, pairing, wrong code). TC-009-29 Fail: the bar stays green after the service stops → **ISSUE-010**. TC-009-29 steps 1 and 3 (green ≤ 30 s after start, green ≤ 60 s after the restart) have no separate record |
| AC-7 four pages on W10 | TC-009-30 | Verified | |
| AC-8 look on W10 | TC-009-31 | Verified | |
| AC-9 language on W10 | TC-009-32, *-36 (opt)* | Verified (de, en) | "any other language → German" not verified (TC-009-36 optional, skipped) |
| AC-10 menu, start page | TC-009-04, -06, -28, -31 | Verified | |
| AC-11 outlined headings, accent | TC-009-07, -31 | Verified | |
| AC-12 light/dark, scaling, drop highlight | TC-009-08, -14 | Verified | |
| AC-13 App Groups content, states, new app | TC-009-09, -10, -59 | Verified | |
| AC-14 pencil / trash-can buttons | TC-009-11 | Verified | |
| AC-15 create, rename, delete | TC-009-12, -13 | **Not verified (partly)** | Rename and delete verified (TC-009-13). The create dialog, the name rules with their two messages, emoji, Cancel/Escape on Windows rest on the skipped TC-009-12. The messages were seen in English on W10 (TC-009-32) |
| AC-16 drag-and-drop mouse and touchpad | TC-009-14, -15 (opt), -30 | Verified | |
| AC-17 "≡", right-click | TC-009-16 | Verified | |
| AC-18 saved at once, log, error | TC-009-17, -18 (opt) | Verified | The save error path passed (TC-009-18). The status bar during that error belongs to ISSUE-010 |
| AC-19 App-Gruppen-Regeln section | TC-009-19, *-72 (opt)* | Verified | The "no app groups yet" text is not verified (TC-009-72 optional, skipped) |
| AC-20 editing group rules | TC-009-20 | Verified | |
| AC-21 only ungrouped apps; loading; live from App Groups | TC-009-21, -22, -51 | **Not verified (partly)** | "Rules page follows a change on App Groups ≤ 5 s" rests on the skipped TC-009-51 |
| AC-22 Reports group lines | TC-009-23, -58 | Verified | |
| AC-23 Reports follow usage and group changes | TC-009-24, -52 | **Not verified (partly)** | Same-app rename and today's usage verified (TC-009-24). A change made in the other parent app rests on the skipped TC-009-52 |
| AC-24 two apps paired; remove pairing independent | TC-009-49, -54 | **Not verified** | Both cases skipped. W11 app and Mac app were green at the same time in TC-009-67 step 1 (indirect). Removing a pairing in one app is not tested |
| AC-25 group changes sync both directions | TC-009-50, -51, -52 | **Not verified** | Rests only on skipped cases |
| AC-26 existing settings sync both directions | TC-009-53 | **Not verified** | Rests only on a skipped case. US-007 AC-33 therefore stays unverified too |
| AC-27 group rule from the Windows app takes effect | TC-009-57 | Verified | |
| AC-28 texts de/en, same as the Mac | TC-009-06, -09, -13, -16, -18, -19, -21, -23, -32, -39; *-25, -47 (opt)* | Verified | German on W11 and the word-by-word comparison with the Mac (TC-009-39) passed, English on W10 passed. W11 English and the Mac English reference were optional and skipped |
| AC-29 kid texts naming a group, de/en | TC-009-57, -58, -60 | Verified | English rests on TC-009-60 Pass with no "kid2 not English" note (S-6 not ticked). Michael please confirm kid2 was English |
| AC-30 versions 0.9.0 | TC-009-01, -02, -03, -27, -37 | Verified | |
| AC-31 service update keeps data; REG-001 | TC-009-01, -02, -05 | Verified | |
| AC-32 Mac update keeps pairing, appearance | TC-009-37 | Verified | |
| AC-33 fresh install on W11, pairing | TC-009-03, -04 | Verified | |
| AC-34 Windows app regression spot checks | TC-009-55, -56, -24 | **Not verified (partly)** | Only "Reports today's usage" verified (TC-009-24). Connection error naming the host, "Kopplung auf diesem Gerät löschen", tick, break time, app rule, budget text, warning validation rest on the skipped TC-009-55, -56 (ticks, a break entry and a warning value were exercised on W10 in TC-009-30) |
| AC-35 Mac = 0.8.0; US-008 Mac ACs in full | TC-009-38 to -48, -59 | **Not verified (partly)** | Open: US-008 AC-7 to AC-10, D-16, D-19 on the Mac (TC-009-40 skipped); US-008 AC-35, AC-36 on the Mac (TC-009-45 no result; partly seen in TC-009-58 and -62 on the Mac's Berichte); AC-14 error path (TC-009-44 optional). All of these passed in Michael's 12-point check on 0.8.0 (US-008 report §3), and the Mac code is unchanged |
| AC-36 service and tray = 0.8.0 | TC-009-01, -02, -57 | Verified | |
| AC-37 US-008 retest (service) | TC-009-57 to -70 | **Not verified (partly)** | See §4.1. Open: US-008 AC-30 and the delete part of AC-32 (TC-009-63 skipped), the locked part of AC-20 (TC-009-66 optional) |
| AC-38 optional pilot checks | *TC-009-71 (opt)* | **Not verified** | Skipped. "Not tested" does not block closing (story) |

**Summary: 24 verified, 1 failed (AC-6, known issue ISSUE-010), 13 not verified.**

- **Resting only on skipped cases:** AC-4 *(opt)*, AC-5, AC-24 (apart from the indirect observation in TC-009-67), AC-25, AC-26, AC-38 *(opt)*.
- **Partly resting on skipped or unrecorded cases:** AC-1, AC-15, AC-21, AC-23, AC-34, AC-35, AC-37. Smaller optional parts: AC-9, AC-19.

### 4.1 US-008 Retest Status (US-008 test report F-1)

| US-008 AC | Result in run 01 |
|---|---|
| AC-19 union of member time | Verified (TC-009-57, -58) |
| AC-20 not counted locked; kept across restart; midnight | Restart verified (TC-009-67). Locked: not verified (TC-009-66 optional, skipped). Midnight: not testable (plan §1) |
| AC-21 per account; kid3 / admin never count | Verified (TC-009-60) |
| AC-22 current members, earlier time added | Verified (TC-009-62) |
| AC-23 app usage counted as before | Verified (TC-009-58) |
| AC-24 own rule ignored in a group | Verified (TC-009-57) |
| AC-25 blocked start (exhausted, not allowed) | Verified (TC-009-58, -60) |
| AC-26 warnings once per group | Verified (TC-009-57) |
| AC-27 all members closed, one dialog | Verified (TC-009-57) |
| AC-28 break wins | Verified (TC-009-65, optional, passed) |
| AC-29 other accounts | Verified (TC-009-60) |
| AC-30 ungrouped app as US-006 (after a group delete) | **Not verified** (TC-009-63 skipped) |
| AC-31 log entries name the group | Verified as ticked (TC-009-57, -58); no log copies committed |
| AC-32 changes during the day, grace | Rule change verified (TC-009-61), move verified (TC-009-62); **delete not verified** (TC-009-63 skipped) |
| AC-33 early warning on a change; more time | Verified (TC-009-64 optional, passed; TC-009-58 step 4) |
| AC-34 rename / harmless move: no dialog | Verified (TC-009-64 optional, passed) |
| AC-38 storage across restarts, re-install | Verified (TC-009-05, -46, -67) |
| AC-39 new account 0:00; untick keeps | Verified (TC-009-68, -69). The enforcement after a re-tick (TC-009-70, optional) was not run |
| AC-40 app stays in its group when its account is unticked | Verified (TC-009-69) |
| AC-41 performance | Not tested (pilot, TC-009-71) |
| AC-6 no groups after 0.7.x update | Not testable (plan §8 Q-6) |
| Parent-app ACs AC-1 to AC-18, AC-35 to AC-37, AC-43 | Windows: see AC-1 above. Mac: see AC-35 above |

**Live group enforcement ran in a kid session for the first time and behaved as specified** (warnings name the group, all members closed with one dialog, blocked starts, grace minute on a rule change and on a move). US-007 F-1 (not verified US-007 AC-25, AC-33) stays open, because Session 5 was skipped.

## 5. Cases Not Executed

| Test case | Essential / optional | Reason |
|---|---|---|
| TC-009-12 | essential | Skipped (no reason given) |
| TC-009-25 | optional | Skipped (W11 English; English checked on W10) |
| TC-009-26 | essential | Skipped (no reason given) |
| TC-009-35 | optional | Skipped (no older or 32-bit Windows, expected) |
| TC-009-36 | optional | Skipped |
| TC-009-40 | essential | Skipped (no reason given) |
| TC-009-44 | optional | Skipped |
| **TC-009-45** | essential | **No result recorded** (`X ] Pass`, no box ticked) |
| TC-009-47 | optional | Skipped |
| TC-009-49 to -54 | essential | Skipped (whole Session 5, two parent apps at once) |
| TC-009-55, -56 | essential | Skipped (Session 6A) |
| TC-009-63 | essential | Skipped |
| TC-009-66, -70, -71, -72 | optional | Skipped |

## 6. Issues and Known Issues

| Issue | Severity | Status | Test case |
|---|---|---|---|
| `02_Implementation/docs/testing/US-009/issues/ISSUE-010.md`: Windows parent app: status bar stays green ("Verbunden mit …") when the service is stopped or not responding | Medium (TES proposal) | New, **known issue, under observation; not fixed in US-009 (decided by Michael 2026-11-01)** | TC-009-29 (Fail, W10), TC-009-18 (Observed note on a Pass, W11) |

**Known issues shipped with 0.9.0:** ISSUE-010. It is confirmed on Windows 10 (remote app, service stopped) and on Windows 11 (service process suspended). The Mac is not tested, and the shared code suggests it is affected too. A plain service stop with the W11 app on the service PC turned red as expected (TC-009-10). The DEV pointer (status bar fed only by SignalR lifecycle events, save error path separate, default SignalR timeouts) is in the issue.

## 7. Open Points of the Test Plan §8 and How the Run Resolved Them

| ID | Topic | Resolution from run 01 |
|---|---|---|
| Q-1 | Account roles; kid2 English | Real names not recorded (S-5). TC-009-60 passed with no "kid2 not English" note, so the English part of AC-29 is taken as seen; Michael please confirm |
| Q-2 | Baseline group with the Mac app 0.8.0 | Used: TC-009-05 passed (groups, rules, today's line kept). S-10 not ticked; no screenshots committed; past-day lines not recorded |
| Q-3 | English on W11 | As proposed: fully on W10 (TC-009-32 Pass); W11 English optional and skipped (TC-009-25) |
| Q-4 | Mac AC-35 depth | Run as quick re-confirms. TC-009-40 skipped and TC-009-45 without a result leave AC-35 partly open (§4) |
| Q-5 | Live "≤ 15 s" checks on W11 | As proposed: switch-back on W11 (TC-009-24, -59 Pass), timed on the Mac (TC-009-58, -59 Pass) |
| Q-6 | US-008 AC-6 not testable | Recorded as not testable (§4.1) |
| Q-7 | Uninstall on W10 while paired | Done (TC-009-34 Pass). The orphan device entry "Windows 10 PC" stays on the service (accepted, US-007 F-4) |
| Q-8 | Service PC address | Not written in S-3; every case used `192.168.1.37` and passed, so that is taken as the address |
| Q-9 | Save error only in optional cases | TC-009-18 was run and passed; it revealed the status-bar side of ISSUE-010. TC-009-44 (Mac) skipped |
| Q-10 | Session 6B on a fresh day | Met in effect: TC-009-57 passed with the expected group timings |
| Q-11 | 72 cases, ≈ 8 h | Partial run: 49 Pass, 21 Skipped, 1 without a result |

## 8. Feedback for PRO / ARC

| # | Feedback | To |
|---|---|---|
| FB-1 | Michael expects the status bar to turn red **as soon as** the service is down, also when a save fails (TC-009-18 note). The story only gives "red ≤ 30 s after the service stops" (US-002 AC-22) and nothing for a service that hangs. Decide whether a failed save (timeout or connection error) should turn the bar red at once. Part of ISSUE-010 | PRO, then ARC |
| FB-2 | Detection time of a lost connection on a remote parent app (SignalR server timeout vs. the 30 s limit) is a design point for ARC when ISSUE-010 is picked up | ARC |

## 9. Follow-ups

| # | Follow-up | Owner | When |
|---|---|---|---|
| F-1 | **ISSUE-010** (known issue): keep under observation in pilot use, on the Windows apps and on the Mac. Pick up in a later story or patch: analysis by DEV, a decision on FB-1, then a re-run of TC-009-29 and TC-009-18 (+ a Mac service-stop check) with a stopwatch, screenshots and LOG-COPY | Michael (observation), PRO / ARC / DEV | when Michael schedules it |
| F-2 | **Two parent apps at once not verified** (AC-24 to AC-26, US-007 AC-25, AC-33; Session 5, TC-009-49 to -54). Run them in a later run or with the next story that touches the parent apps | TES / Michael | next parent-app story or pilot |
| F-3 | Other essential cases not executed: TC-009-12 (create, name rules on W11), TC-009-26 (service installer refuses W10), TC-009-40 (Mac create/rename/delete), TC-009-45 (Mac Reports, no result), TC-009-55, -56 (W11 regression spot checks), TC-009-63 (group delete with a running app; US-008 AC-30, AC-32 delete). Candidates for a `test-run-02.md` if Michael wants one, or for the regression checklist | TES / Michael | Michael's choice |
| F-4 | Confirm TC-009-45's result and that kid2 was English in TC-009-60 (chat is enough; TES then records it in a new run, the run file stays as is) | Michael | at closure |
| F-5 | Optional, unchanged from the plan: AC-4 (older Windows), AC-38 / US-008 AC-41 performance and the live group smoke (pilot), US-008 AC-20 locked time | Michael (pilot) | pilot operation |
| F-6 | Evidence habit: no service-log copies or screenshots were committed in run 01 (also none after the Fail). For the re-test of ISSUE-010, use LOG-COPY | Michael / TES | next run |

## 10. Recommendation and Decisions for Michael

**TES recommendation.** Normally a failed AC goes back to DEV (step c). Here, closing with a known issue is acceptable to TES: ISSUE-010 is Medium, the page tells the parent that a change was not saved, no data is lost, and enforcement on the service PC is not affected. **Michael has decided accordingly (2026-11-01): ISSUE-010 is a known issue under observation, US-009 closes without a DEV fix.** TES records this. Only Michael closes the story; TES does not change the story status.

**What Michael accepts by closing:**

1. AC-6 failed, with ISSUE-010 as a known issue (decided).
2. The 13 ACs not or only partly verified (§4). The biggest gap is the two-apps-at-once sync (AC-24 to AC-26, Session 5), which was also open in US-007. Also open: AC-5, AC-34 and parts of AC-1, AC-15, AC-21, AC-23, AC-35, AC-37. Please confirm explicitly that these are accepted as not verified (F-2, F-3), or ask for a `test-run-02.md` with these cases before closing.
3. TC-009-45 (no result) and kid2 English (F-4).

## 11. Regression Checklist Update (proposal, after Michael closes the story)

Add to `02_Implementation/docs/testing/regression-checklist.md` (a new section for the Windows app 0.9.0 and app groups):

- Windows app install without admin rights, repair, uninstall on Windows 10 (TC-009-27, -33, -34)
- Windows app pairing and the status bar on a service stop: TC-009-29, marked **"known issue ISSUE-010"** until it is fixed
- App Groups page: create / rename / delete, drag-and-drop, "≡" and right-click (TC-009-12, -13, -14, -16)
- Group rule enforcement core case (TC-009-57) and blocked start (TC-009-58)
- Grace minute on a rule change (TC-009-61)
- Two parent apps sync (TC-009-50, -53): to be run at the next regression, since never verified

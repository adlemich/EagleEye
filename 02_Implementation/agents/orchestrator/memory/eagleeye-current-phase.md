---
name: eagleeye-current-phase
description: EagleEye workflow status — US-007 iPhone parent app part a, story approved and split (Rules → US-008), next ARC plan
metadata:
  type: project
---

**As of 2026-10-17. Current: Phase 3, US-007 "iOS parent app" (iPhone, portrait only, parity with Windows parent app 0.6.2), step a done: approved by Michael 2026-10-17 and split (862f9bd). US-007 = platform, pairing, Settings, Reports (47 ACs, A–H; no Rules tab, AC-42). Rules parked in `US-007/deferred-to-US-008.md` (US-008 branch only after US-007 merge). Requirements v1.7 applied (FR-APP-016 forget pairing, FR-APP-100..102 iOS). Test devices: iPhone 13 + iPhone 16 Plus on iOS 27; DEV runs scripted Simulator checks on iPhone SE (375×667). Michael cloning the branch on the MacBook. Next: ARC plan.NET iOS compatibility, Apple ID signing, iPhone Developer Mode. Previous: US-006 "App Time Budgets and Time-elapsed Warnings" (delivered build 0.6.2) closed by Michael WITHOUT a manual test run ("Close as tested, then merge") and merged into main; branch deleted. All 39 ACs unverified manually; live enforcement never ran in a kid session (D-15). Test report `docs/testing/US-006/test-report.md` lists follow-ups F-1..F-8: F-1 TC-005-26/TC-006-34 agent-token rerun (skipped 3x), F-2 focused live enforcement run against 0.6.2, F-3 LEOSERV checks (150 %, "–" checkboxes), F-4 confirm ISSUE-008/009, F-5 US-002 docs for ISSUE-009 local forget-pairing (D-19), F-8 Michael must set rules for every controlled account before kids use 0.6.2 (deny by default). Issues filed under `docs/testing/US-006/issues/` (left there). Dependencies: Microsoft packages on 10.0.13, `docs/dev-process/dependency-log.md`. MacBook: build/test not yet run after 10.0.13 update (first restore needs internet).**

- US-005 run 01: 22 Pass, 0 Fail, 13 Skipped. Not verified manually: AC-18, 19, 33, 35, 37; security TC-005-26/27/29. The agent-token check (TC-004-23 = TC-005-26) has been skipped twice, so ARC's question about write-restriction stays open. Details: `docs/testing/US-005/test-report.md`.
- US-005 decisions: a start = creation of the app's first process; graceful close, force after 20 s (≤ 30 s); the dialog is topmost (not system-modal); time-zone changes are logged and stored; accessibility tools are blocked during breaks.
- Earlier: US-001 to US-004 Verified/Closed (US-004: 0.4.1).
- Open from ARC: arc42 §7.1 install path lacks `Service\` subfolder. From DEV: tray *App Infos* dialog is tight at 150 % (not filed).
- Subagent runs can hit API rate limits; resume via SendMessage works, and agents push at checkpoints.

See [[eagleeye-workflow-gates]], [[michael-closes-with-partial-runs]].

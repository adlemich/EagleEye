---
name: eagleeye-current-phase
description: EagleEye workflow status — US-006 app time budgets, step e (Michael testing; 0.6.1 patch built for ISSUE-008 + SignalR advisory)
metadata:
  type: project
---

**As of 2026-10-17. Current: Phase 3, US-006 "App Time Budgets and Time-elapsed Warnings", step e (Michael started testing on LEOSERV before formally approving the TES plan fd45d6b; plan Q-1..Q-7 not yet answered). Branch `feature/US-006-app-time-budgets`. ISSUE-008 (numeric Entry fields invisible: HorizontalTextAlignment=End on WinUI) fixed in 0.6.1 (d14b1ca). Michael chose option (a) for advisory GHSA-334f-vvp4-7g6p: all Microsoft 10.0.12 packages → 10.0.13 (77cef8a, internet granted for that only), new `docs/dev-process/dependency-log.md`. 0.6.1 installers built; Michael must reinstall service + parent apps. Open: 150 % check on LEOSERV, "–" checkbox look on LEOSERV (not reproducible on ZOCK-O-MAT-V3). Michael's untracked scripts/list-local-accounts.ps1 + remove-local-account.ps1: do not commit unless asked. Enforcement still first tested live in this run (D-15).**

- US-005 run 01: 22 Pass, 0 Fail, 13 Skipped. Not verified manually: AC-18, 19, 33, 35, 37; security TC-005-26/27/29. The agent-token check (TC-004-23 = TC-005-26) has been skipped twice, so ARC's question about write-restriction stays open. Details: `docs/testing/US-005/test-report.md`.
- US-005 decisions: a start = creation of the app's first process; graceful close, force after 20 s (≤ 30 s); the dialog is topmost (not system-modal); time-zone changes are logged and stored; accessibility tools are blocked during breaks.
- Earlier: US-001 to US-004 Verified/Closed (US-004: 0.4.1).
- Open from ARC: arc42 §7.1 install path lacks `Service\` subfolder. From DEV: tray *App Infos* dialog is tight at 150 % (not filed).
- Subagent runs can hit API rate limits; resume via SendMessage works, and agents push at checkpoints.

See [[eagleeye-workflow-gates]], [[michael-closes-with-partial-runs]].

---
name: eagleeye-current-phase
description: EagleEye workflow status — US-006 app time budgets, step d (TES test plan written, awaiting Michael's approval)
metadata:
  type: project
---

**As of 2026-10-17. Current: Phase 3, US-006 "App Time Budgets and Time-elapsed Warnings", step d: TES test plan + run 01 written (fd45d6b, 42 TCs, 32 must-run, ~4 h 20 min), awaiting Michael's approval and plan Q-1..Q-7. Step c approved 2026-10-17. Branch `feature/US-006-app-time-budgets` at aad2c1f. Story Implemented; plan approved 2026-10-12 (Q-1..Q-5 as ARC proposed: language-following defaults incl. English break text, min. grace = final-warning minutes after any change that moves the end earlier, time changes log-only, no re-armed warnings, TES reruns TC-005-26). DEV report: `docs/requirements/user-stories/US-006/implementation-report.md`; 2 623 unit tests green, installers 0.6.0 built. Live enforcement NOT smoke-tested by DEV (D-15: deny-by-default would close Michael's own apps) — first live run is the TES run with kid1. Next: Michael approves the test plan, then executes run 01 (`docs/testing/US-006/test-run-01.md`).**

- US-005 run 01: 22 Pass, 0 Fail, 13 Skipped. Not verified manually: AC-18, 19, 33, 35, 37; security TC-005-26/27/29. The agent-token check (TC-004-23 = TC-005-26) has been skipped twice, so ARC's question about write-restriction stays open. Details: `docs/testing/US-005/test-report.md`.
- US-005 decisions: a start = creation of the app's first process; graceful close, force after 20 s (≤ 30 s); the dialog is topmost (not system-modal); time-zone changes are logged and stored; accessibility tools are blocked during breaks.
- Earlier: US-001 to US-004 Verified/Closed (US-004: 0.4.1).
- Open from ARC: arc42 §7.1 install path lacks `Service\` subfolder. From DEV: tray *App Infos* dialog is tight at 150 % (not filed).
- Subagent runs can hit API rate limits; resume via SendMessage works, and agents push at checkpoints.

See [[eagleeye-workflow-gates]], [[michael-closes-with-partial-runs]].

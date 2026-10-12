---
name: eagleeye-current-phase
description: EagleEye workflow status — US-006 app time budgets, step b (ARC plan written, awaiting Michael's approval + plan Q-1..Q-7)
metadata:
  type: project
---

**As of 2026-10-12. Current: Phase 3, US-006 "App Time Budgets and Time-elapsed Warnings", step b. Branch `feature/US-006-app-time-budgets`. Story approved by Michael (46925f4, 39 ACs, requirements v1.6; status New until plan approved). ARC plan 5e00b8d: `docs/requirements/user-stories/US-006/implementation-plan.md` (Draft), ADR-015/016 Proposed, version 0.6.0, all steps Windows-only. Awaiting Michael: plan approval + Q-1 (language-following defaults, English break text), Q-2 (min. grace after any change, ARC recommends), Q-3 (log-only), Q-5 (no re-armed warnings). Next after approval: DEV step c.**

- US-005 run 01: 22 Pass, 0 Fail, 13 Skipped. Not verified manually: AC-18, 19, 33, 35, 37; security TC-005-26/27/29. The agent-token check (TC-004-23 = TC-005-26) has been skipped twice, so ARC's question about write-restriction stays open. Details: `docs/testing/US-005/test-report.md`.
- US-005 decisions: a start = creation of the app's first process; graceful close, force after 20 s (≤ 30 s); the dialog is topmost (not system-modal); time-zone changes are logged and stored; accessibility tools are blocked during breaks.
- Earlier: US-001 to US-004 Verified/Closed (US-004: 0.4.1).
- Open from ARC: arc42 §7.1 install path lacks `Service\` subfolder. From DEV: tray *App Infos* dialog is tight at 150 % (not filed).
- Subagent runs can hit API rate limits; resume via SendMessage works, and agents push at checkpoints.

See [[eagleeye-workflow-gates]], [[michael-closes-with-partial-runs]].

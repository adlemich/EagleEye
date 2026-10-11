---
name: eagleeye-current-phase
description: EagleEye workflow status — US-005 break times Verified/Closed and merged into main (2026-10-11, build 0.5.0); next story not started
metadata:
  type: project
---

**As of 2026-10-11. Current: Phase 3, between stories. US-005 "Break times" (build 0.5.0) is `Verified/Closed` by Michael ("no changes required") and merged into main; the branch is deleted. Next: PRO starts US-006, which Michael announced as "approaching a usage limitation": it covers apps already running when a break begins, and possibly process-based enforcement (US-005 plan Q-3) and the time-zone hardening (Q-2).**

- US-005 run 01: 22 Pass, 0 Fail, 13 Skipped. Not verified manually: AC-18, 19, 33, 35, 37; security TC-005-26/27/29. The agent-token check (TC-004-23 = TC-005-26) has been skipped twice, so ARC's question about write-restriction stays open. Details: `docs/testing/US-005/test-report.md`.
- US-005 decisions: a start = creation of the app's first process; graceful close, force after 20 s (≤ 30 s); the dialog is topmost (not system-modal); time-zone changes are logged and stored; accessibility tools are blocked during breaks.
- Earlier: US-001 to US-004 Verified/Closed (US-004: 0.4.1).
- Open from ARC: arc42 §7.1 install path lacks `Service\` subfolder. From DEV: tray *App Infos* dialog is tight at 150 % (not filed).
- Subagent runs can hit API rate limits; resume via SendMessage works, and agents push at checkpoints.

See [[eagleeye-workflow-gates]], [[michael-closes-with-partial-runs]].

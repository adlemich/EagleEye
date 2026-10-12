---
name: eagleeye-current-phase
description: EagleEye workflow status — US-006 app time budgets, step a (story written, awaiting Michael's approval and 19 open questions)
metadata:
  type: project
---

**As of 2026-10-12. Current: Phase 3, US-006 "App Time Budgets and Time-elapsed Warnings", step a. Branch `feature/US-006-app-time-budgets` (pushed). Scope input e85575f; PRO story 53300ae: `docs/requirements/user-stories/US-006/user-story.md`, status New, 38 ACs, 19 open questions (OQ-2 = existing apps blocked after update is the key one), proposes requirements v1.6 amendment. Next: Michael approves/answers OQs, then ARC plan. US-005 (0.5.0) Verified/Closed and merged 2026-10-11.**

- US-005 run 01: 22 Pass, 0 Fail, 13 Skipped. Not verified manually: AC-18, 19, 33, 35, 37; security TC-005-26/27/29. The agent-token check (TC-004-23 = TC-005-26) has been skipped twice, so ARC's question about write-restriction stays open. Details: `docs/testing/US-005/test-report.md`.
- US-005 decisions: a start = creation of the app's first process; graceful close, force after 20 s (≤ 30 s); the dialog is topmost (not system-modal); time-zone changes are logged and stored; accessibility tools are blocked during breaks.
- Earlier: US-001 to US-004 Verified/Closed (US-004: 0.4.1).
- Open from ARC: arc42 §7.1 install path lacks `Service\` subfolder. From DEV: tray *App Infos* dialog is tight at 150 % (not filed).
- Subagent runs can hit API rate limits; resume via SendMessage works, and agents push at checkpoints.

See [[eagleeye-workflow-gates]], [[michael-closes-with-partial-runs]].

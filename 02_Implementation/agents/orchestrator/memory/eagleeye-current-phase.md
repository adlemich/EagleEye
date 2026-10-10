---
name: eagleeye-current-phase
description: EagleEye workflow status — US-005 break times implemented as 0.5.0, waiting for Michael's approval of step c (2026-10-10)
metadata:
  type: project
---

**As of 2026-10-10. Current: Phase 3, US-005 "Break times", branch `feature/US-005-break-times`, step c done (DEV implemented 0.5.0: 2,107 unit tests green; installers `03_Delivery/windows/EagleEye-*-0.5.0.exe`). Next: Michael approves → TES writes the manual test plan (step d).** The story is `Implemented`; plan and ADR-013/014 are approved.

- Michael's binding decisions: a start = creation of the app's first process; detection ≤ 10 s, graceful close, force after 20 s (≤ 30 s). The limits count from the first window if it comes more than 10 s after the start (PRO interpretation, flagged to Michael). Time-zone/clock changes are logged and stored (`TimeChangeFindings`); the installer hardening is deferred. The dialog is topmost, not system-modal. Accessibility tools are blocked. Running apps at break start are left to the next story ("approaching a usage limitation").
- TES must include TC-004-23 (agent token) and a graceful-close check under the real SYSTEM token (plan Q-8), plus DEV report §7 items.
- DEV D-13 fixed a US-004 defect: the parent app crashed on a service restart while a 2nd+ account was selected (Reports too). Fixed on this branch only.
- US-004 Verified/Closed and merged (0.4.1); untested ACs in `docs/testing/US-004/test-report.md`.
- Subagent runs hit API rate limits twice; resume via SendMessage works, agents push at checkpoints.

See [[eagleeye-workflow-gates]], [[michael-closes-with-partial-runs]].

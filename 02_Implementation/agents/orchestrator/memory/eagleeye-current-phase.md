---
name: eagleeye-current-phase
description: EagleEye workflow status — US-004 Verified/Closed and merged into main (2026-10-10, build 0.4.1); next story not started
metadata:
  type: project
---

**As of 2026-10-10. Current: Phase 3, between stories. US-004 (app usage tracking, build 0.4.1) is `Verified/Closed` by Michael and merged into main; the branch is deleted. Next: PRO starts US-005.**

- Run 01 on 0.4.0: 23 Pass, 0 Fail, 11 Skipped, TC-004-23 not ticked. Michael's ruling: skipped cases stay skipped, no repeat. Not verified manually: AC-11, AC-14, AC-16, AC-25, and the security checks TC-004-23, -25, -26, -29. TC-004-23 also leaves ARC's plan question Q-9 (which write-restriction variant runs) open. All listed in `docs/testing/US-004/test-report.md`.
- ISSUE-007 (accent-colour heading bars, Berichte as start page): change request after run 01, fixed in 0.4.1, visual re-test "All good" per Michael (no run file). iOS/macOS use the fallback colour #1E7B3A for now.
- Earlier: US-001 to US-003 Verified/Closed; US-003 left AC-4, 5, 7, 9, 16, 19-24 unverified manually.
- Open from ARC: arc42 §7.1 install path lacks `Service\` subfolder; TrayHub SID registration is target design only. From DEV: tray *App Infos* dialog is tight at 150 % (not filed).

See [[eagleeye-workflow-gates]], [[michael-closes-with-partial-runs]].

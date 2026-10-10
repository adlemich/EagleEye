---
name: eagleeye-current-phase
description: EagleEye workflow status — US-004 run 01 done, change request ISSUE-007 implemented as 0.4.1, visual re-test pending (2026-10-10)
metadata:
  type: project
---

**As of 2026-10-10. Current: Phase 3, US-004 (app usage tracking), branch `feature/US-004-app-usage-tracking`.** Test run 01 on 0.4.0: 23 Pass, 0 Fail, 11 Skipped, TC-004-23 not ticked. Michael: skipped cases stay skipped, do not repeat them. His visual change request ISSUE-007 covers heading bars in the accent colour on Berichte and Einstellungen, plus Berichte as the start page. DEV implemented it as patch 0.4.1 (commits 4327f60, 2984c42; 1,513 unit tests green). **Next: Michael approves DEV step → quick visual re-test on LEOSERV (TES run 02, ISSUE-007 only) → test report → close → merge.** Before the merge, record the skipped and unticked cases in `docs/testing/US-004/test-report.md`.

- Earlier: US-001 to US-003 Verified/Closed and merged; US-003 left AC-4, 5, 7, 9, 16, 19-24 unverified manually (its test report §3).
- Open from ARC: arc42 §7.1 install path lacks `Service\` subfolder; TrayHub SID registration is target design only. From DEV: tray *App Infos* dialog is tight at 150 % (not filed).

See [[eagleeye-workflow-gates]], [[michael-closes-with-partial-runs]].

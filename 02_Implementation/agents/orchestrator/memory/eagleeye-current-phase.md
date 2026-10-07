---
name: eagleeye-current-phase
description: EagleEye workflow status — US-003 Verified/Closed and merged into main (2026-10-07); next story not started
metadata:
  type: project
---

**As of 2026-10-07. Current: Phase 3, between stories. US-003 (account inventory + selection, build 0.3.1, ADR-010 event-driven state propagation) is `Verified/Closed` by Michael and merged into `main` (003ee9b); branch deleted. Run 01 was partial (18 Pass, 17 Skipped, 1 not ticked): Blocks D and E not run, so AC-4, 5, 7, 9, 16, 19-24 are not verified manually (listed in `docs/testing/US-003/test-report.md` §3, proposal: cover in a later run). Next: PRO starts US-004.**

- Test run 02 passed (Michael's report, 2026-10-07). He ticked setup/cleanup steps but left the per-case result boxes empty; the run file and test report record that the result rests on his report.
- ISSUE-004 (pairing-code window clipped at 150 %) and ISSUE-005 (non-elevated terminal, test-plan fix) are `Verified/Closed`.
- D-11 decided 2026-10-07: coding guidelines §3.4/§8.2 now require plain `using` for SQLite command/reader/transaction; existing code aligned by DEV on main the same day (590 tests green, 100 % branch coverage kept). Still open from ARC: arc42 §7.1 install path lacks `Service\` subfolder; TrayHub SID registration is target design only. From DEV: tray *App Infos* dialog has the same fixed-pixel layout as ISSUE-004 (tight at 150 %, touches OK at 200 %), not filed as an issue.
- Delivered builds: `03_Delivery/windows/EagleEye-Setup-0.2.0.exe`, `EagleEye-ParentApp-Setup-0.2.0.exe`. US-001: Verified/Closed (0.1.1).

See [[eagleeye-workflow-gates]], [[us001-lessons]].

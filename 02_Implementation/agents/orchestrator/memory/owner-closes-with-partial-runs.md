---
name: owner-closes-with-partial-runs
description: the project owner may close a story after a partial test run and skip TES's evaluation; record the gaps honestly before merging
metadata:
  type: feedback
---

The project owner closed US-002 (2026-10-07) and US-003 (2026-10-07) right after the test run, saying "all approved / good enough", without a TES evaluation step. In US-002 they ticked no per-case boxes; in US-003 they skipped 17 of 36 cases (all of the account-change and two-app blocks).

**Why:** they are the sole approval authority and prefers speed over a formal step f for stories they have seen working.

**How to apply:** don't push back or re-ask; honour the close and merge. But before merging, tally the run file, write/finalize the test report yourself (Orchestrator), list every AC not verified manually, set issues/story to Verified/Closed, and tell the project owner the gaps in one short paragraph. See [[eagleeye-workflow-gates]].

US-004 (2026-10-10): same pattern. The project owner skipped 11 optional/late cases ("skipped tests are ok to skip, do not repeat them"), raised a visual change request, re-tested only that, and closed with "All good". A re-test reported verbally is recorded in the test report and the issue without a run file.

- US-006 (2026-10-17): closed with run 01 entirely unexecuted, after two in-run patches (0.6.1, 0.6.2). Handled as above: TES test report listing every AC as not verified plus follow-ups, then PRO close, then merge.
- US-007 (2026-10-31): closed during step c, with no TES test plan and the Windows regression step undone, because the Mac app was already in pilot use and new feedback had priority. Orchestrator wrote `docs/testing/US-007/test-report.md` with the gaps.
- US-008 (2026-10-31): closed after step c without TES steps d–f; manual tests deferred to the US-009 run (test report F-1).
- US-010 (2026-11-07): closed right after step c approval, skipping TES d–f, on the strength of their own pilot check; Orchestrator wrote `docs/testing/US-010/test-report.md`.

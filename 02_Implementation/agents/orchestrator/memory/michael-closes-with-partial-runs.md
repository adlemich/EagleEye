---
name: michael-closes-with-partial-runs
description: Michael may close a story after a partial test run and skip TES's evaluation; record the gaps honestly before merging
metadata:
  type: feedback
---

Michael closed US-002 (2026-10-07) and US-003 (2026-10-07) right after the test run, saying "all approved / good enough", without a TES evaluation step. In US-002 he ticked no per-case boxes; in US-003 he skipped 17 of 36 cases (all of the account-change and two-app blocks).

**Why:** he is the sole approval authority and prefers speed over a formal step f for stories he has seen working.

**How to apply:** don't push back or re-ask; honour the close and merge. But before merging, tally the run file, write/finalize the test report yourself (Orchestrator), list every AC not verified manually, set issues/story to Verified/Closed, and tell Michael the gaps in one short paragraph. See [[eagleeye-workflow-gates]].

US-004 (2026-10-10): same pattern. Michael skipped 11 optional/late cases ("skipped tests are ok to skip, do not repeat them"), raised a visual change request, re-tested only that, and closed with "All good". A re-test reported verbally is recorded in the test report and the issue without a run file.

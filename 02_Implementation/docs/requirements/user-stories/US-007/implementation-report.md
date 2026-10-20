# Implementation Report: US-007 — macOS Parent App Parity

**Author**: DEV
**Date**: 2026-10-20 (started)
**Branch**: `feature/US-007-macos-parent-app-parity`
**Plan**: `02_Implementation/docs/requirements/user-stories/US-007/implementation-plan.md` (approved 2026-10-20 with Q-1 and Q-2), ADR-020, the US-007 amendments of ADR-019, arc42 and coding guidelines §13
**Machine(s) used**: MacBook so far (see §7)
**Story status**: Analyzed (in progress: Step 1 "first light")

> This report grows with each plan step. Steps done: 0. Step 1 in progress. Steps 2 to 9 not started (Step 2 waits for ARC's review of `US-007/first-light.md`).

---

## 1. Summary

| Step | State | Content |
|---|---|---|
| 0 | Done | Version 0.7.0 (`Directory.Build.props`). Build and unit tests green on the MacBook. |
| 1 | In progress | Mac app built (Debug, unchanged code) and launched on the MacBook. Waiting for Michael to pair it with `192.168.1.37`. Gap list in `02_Implementation/docs/requirements/user-stories/US-007/first-light.md`. |

## 2. Deviations from the Implementation Plan

None so far.

Notes (not deviations):
- Step 1 launches the app through LaunchServices (`open`), not by running the executable inside the bundle from a shell as plan Step 1.1 suggests. Reason: macOS Local Network privacy attributes LAN access to the responsible process; a child of a shell without that permission cannot reach the LAN at all (observed in the DEV shell). Console output is captured with `open --stdout/--stderr`.
- A fallback build with `UseNativeHttpHandler=false` (TI-2, an allowed first-light fix) was prepared next to the as-is build, so that Michael does not need a second round if the as-is build cannot connect. It is only used if the as-is build fails; the result is recorded in `first-light.md` §3.

## 3. Files

**Step 0** — changed: `02_Implementation/Directory.Build.props` (0.6.2 → 0.7.0).
**Step 1** — new: `02_Implementation/docs/requirements/user-stories/US-007/first-light.md`, this report.

## 4. Unit Tests

`scripts/test.ps1` on the MacBook (Step 0, unchanged code): **1 197 passed, 0 failed** — Shared 282, ParentApp 915. No new tests yet (Steps 0 and 1 add no production code).

## 5. Open Questions and Risks

- See `first-light.md` for the gaps found so far. Keychain (TI-1) and Release trimming (TI-5) probes are pending.

## 6. How to Test

Not yet (story in progress). Current dev build: `02_Implementation/src/EagleEye.ParentApp/bin/Debug/net10.0-maccatalyst/maccatalyst-arm64/EagleEye.app`, started with `open`.

## 7. Machine(s) Used

| Step | Machine |
|---|---|
| 0 | MacBook |
| 1 | MacBook (+ service PC `192.168.1.37` for the pairing code) |

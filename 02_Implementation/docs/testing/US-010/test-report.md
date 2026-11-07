# Test Report: US-010 — Remaining-Time Overlay Replaces the Warning Dialogs (0.10.0)

**Status**: Final. Story closed by the project owner on 2026-11-07 without a TES test plan or a recorded test run.
**Date**: 2026-11-07
**Author**: Orchestrator (TES steps d to f skipped by the project owner's decision)
**Test runs evaluated**: none.

---

## 1. Verdict

**Closed by the project owner (2026-11-07): "I approve. We can skip the test plan this time. So, I overwrite the process for US-010. Check it all in and merge to main directly. I want to close it now."** Before this, the project owner had installed 0.10.0 on the Windows machines of the pilot and checked the overlay there: "the overlay pill was tested OK in different games and applications, all as expected" (`02_Implementation/docs/requirements/user-stories/US-010/implementation-report.md` §9.1). This report records what was and was not verified before the merge.

## 2. Environment

| Item | Value |
|---|---|
| Machine(s) | Windows Developer Machine `DEV-PC`; kid's gaming PC / pilot service PC `PILOT-PC`; MacBook (Mac app) |
| Build / installer version | `03_Delivery/windows/EagleEye-Setup-0.10.0.exe` and `EagleEye-ParentApp-Setup-0.10.0.exe` (installed in the pilot by the project owner; hashes in the implementation report §8.5); `03_Delivery/macos/EagleEye-ParentApp-0.10.0.dmg`, 29 312 683 bytes, SHA-256 `70606ff8bcb04714b2e1efe23242f81ff4eb8cbb0b6f468ebeea36b93d33e1e3` (**not installed**; the Mac app is still 0.9.0). All installers are git-ignored and exist only on their build machine. |
| Unit tests | Windows (Steps 1–4, 6): Shared 314, Service 1 506, TrayClient 168, ParentApp 1 108 = 3 096, all passed, 0 warnings. MacBook (Step 7, `f3c0339`): Shared 314, ParentApp 1 108, all passed, 0 warnings |

## 3. Acceptance Criteria Results

No AC has a recorded result from a TES test run. What was **checked outside a test run**:

- **The project owner's pilot check** (plan Steps 5 + 9, report §9.1): the overlay pill in different games and applications, "all as expected". No per-row results, so this is a summary check. In substance it covers AC-1, AC-3, AC-4, AC-7, AC-20, AC-21 and the Windows part of AC-38.
- **DEV look check on the Mac** (report §11, evidence `02_Implementation/docs/requirements/user-stories/US-010/evidence/step7-settings-warnings-*.png`): Settings section "Zeit-Ende Warnungen" with only "Zeitpunkt" / "Minuten vor Ende" and the new explanation text, light and dark mode, with a probe (no live service). Covers the layout of AC-31 and AC-32.
- **Migration 7** (warning text columns dropped) verified by unit tests and on a copied database (report §8).

**Not verified manually** (unit tests only, or only by the summary pilot check): AC-2, AC-5, AC-6 (exact timing, lines appearing/disappearing), AC-8 to AC-12 (signals, sound, levels at start, break line, grace periods), AC-13, AC-14 (remaining dialogs), AC-15 to AC-19 (tray menu settings, defaults, persistence per account), AC-22 to AC-29 (topmost recovery, exclusive fullscreen limit, Easy Anti-Cheat, lock/sign-out, tray client never treated as an app, **performance AC-27**, scaling/multi-monitor, no connection), AC-30 (Windows app Settings), AC-33, AC-34 (two parent apps in sync), **AC-35 (update 0.9.0 → 0.10.0 keeps warning minutes; not recorded per machine)**, AC-36, AC-37 (enforcement regression), the Mac part of AC-38 (`.dmg` not installed).

## 4. Steps Not Done

| Step | What is missing |
|---|---|
| Plan Step 5 / 9 | Per-row results (replaced by the project owner's pilot summary) |
| Plan Step 8 | Not needed (Step 7 changed no code) |
| Story steps d to f | TES test plan, test run, evaluation |

## 5. Issues

None filed.

## 6. Follow-ups

| # | Follow-up | Owner | When |
|---|---|---|---|
| F-1 | **Install the Mac `.dmg` 0.10.0** before using the Mac app against the pilot service PC `PILOT-PC`, which already runs 0.10.0 (TI-16). After the install, toggle the Mac's "Lokales Netzwerk" switch off/on if the app cannot connect | The project owner | before the next Mac app use |
| F-2 | Cosmetic: the column header "Minuten vor Ende" wraps onto two lines in the Mac app (fixed 96-pt column, report §11). Shared XAML, so a fix needs a Windows rebuild | DEV | with the next story touching Settings |
| F-3 | Bugs seen in pilot operation are filed as issues (the project owner, report §9.1); the unverified ACs above are candidates for a later regression run | TES / the project owner | when they come up |

# Test Report: US-007 — macOS Parent App Parity

**Status**: Final. Story closed by the project owner on 2026-10-31 without a test plan or a recorded test run. The Mac app is in pilot use.
**Date**: 2026-10-31
**Author**: Orchestrator (TES step skipped by the project owner's decision)
**Test runs evaluated**: none. No TES test plan was written; step d to f did not take place.

---

## 1. Verdict

**Closed by the project owner (2026-10-31): "Close US-007 now, merge."** The Mac app 0.7.0 is in pilot use with the project owner's service PC (`PILOT-PC`). They want the pilot feedback handled in a new story (US-008, Mac first, Windows parity later). Under the normal rules this story would not be ready to close, because no AC was verified in a recorded run and the Windows regression (plan Step 8) was not done. This report records the gaps before the merge.

## 2. Environment

| Item | Value |
|---|---|
| Machine(s) | MacBook (Mac app); service PC `PILOT-PC` with the Windows service/tray build that was installed before (0.6.2 presumably, not recorded) |
| OS version(s) | macOS 27 (MacBook); Windows 11 (service PC) |
| Build / installer version | `03_Delivery/macos/EagleEye-ParentApp-0.7.0.dmg`, final build 2026-10-31, SHA-256 `f10e55a752086f1539b36fc604fccac5a2651ab4937fe8d2075095da3b40a519`. **No Windows 0.7.0 installers were built.** |

## 3. Acceptance Criteria Results

No AC has a recorded result. What was **observed in use** (not a test result):

- The project owner installed the `.dmg`, paired with the service and used Settings, Rules and Reports with real data (screenshots in `docs/testing/US-006/evidence/` and the conversation of 2026-10-31): parts of AC-2, AC-4, AC-7, AC-10, AC-17, AC-20, AC-21, AC-26, AC-29 to AC-31.
- The menu selection defect (all items stayed coloured) was fixed and confirmed by the project owner on 2026-10-31 (`US-007/implementation-report.md` §1, row "5 (fix, 2026-10-31)").
- Plan Step 1 "first light" was verified by the project owner on 2026-10-20 (pairing, real data).

**Not verified** (no observation either): AC-1 (full parity sweep), AC-3, AC-5 and AC-6 (update and removal script, session B), AC-8 (menu bar), AC-9, AC-11 (light mode, "Larger Text"), AC-12 to AC-16 (English, region formats, emojis), AC-18, AC-19, AC-22 to AC-25 (Local Network deny, sleep/Wi-Fi reconnect, remove pairing, Keychain, two apps paired), AC-27, AC-28, AC-32 (flush on page change), AC-33 and AC-34 (sync with the Windows app, effect on the kid's session), **AC-35 and AC-36 (Windows regression, same version number)**.

## 4. Steps Not Done

| Plan step | What is missing |
|---|---|
| Step 6 | The project owner's parity sweep with the Windows app (sync, sleep/Wi-Fi, Local Network deny, emojis, flush timing, pin mismatch, tooltip) |
| Step 7 | Session B: update over 0.7.0 and the removal script on the project owner's Mac |
| **Step 8** | **Windows regression on the Windows Developer Machine**: build (0 warnings), all four test projects, `package-windows.ps1` → 0.7.0 installers, smoke check of the Windows app 0.7.0 over 0.6.2. Shared and `ParentApp.Core` code changed in this story (`ConnectionCoordinator`, `StartupErrorText`, `UiCulturePolicy`, `AppleAccentPalette`, `AppTexts`), so **`main` now carries 0.7.0 code that has never been built or tested on Windows.** |
| Step 9 | READMEs (`src/EagleEye.ParentApp/README.md`, `src/EagleEye.ParentApp.Core/README.md`), `dev-environments.md` §5 lessons |

## 5. Issues

None filed. The menu selection defect was raised as general feedback in `docs/testing/US-006/test-run-01.md` and fixed inside this story (two attempts, `4bd9c58` and `69e7138`).

## 6. Follow-ups

| # | Follow-up | Owner | When |
|---|---|---|---|
| F-1 | **Windows build and regression of 0.7.0** (plan Step 8) on the Windows Developer Machine, on `main`. Do it before any Windows installer of 0.7.x is given to the pilot. | DEV / the project owner | next Windows session, at the latest before the Windows parity step of US-008 |
| F-2 | Step 9 documentation (READMEs, `dev-environments.md` §5, including the Catalyst background lesson). | DEV / Orchestrator | with US-008 step c |
| F-3 | Session B checks (update over an installed version, removal script). They are repeated naturally when the US-008 `.dmg` is installed over 0.7.0. | The project owner / TES | US-008 test run |
| F-4 | Orphan paired devices on the service (about 5 from first light), no UI to remove them (story Out of Scope). | PRO | pilot feedback backlog |
| F-5 | ARC open points from the implementation report §5 (legacy Keychain API is deprecated; one Keychain prompt per update). | ARC | when Apple removes the API or with a signing decision |

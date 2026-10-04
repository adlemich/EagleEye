# ISSUE-004: Pairing code popup is too small, code and text are cut off

**Status**: Implemented
**User Story**: US-002
**Found in**: docs/testing/US-002/test-run-01.md, TC-002-12
**Date**: 2026-10-04
**Severity**: Critical
**Machine**: Windows Developer Machine
**Build**: `03_Delivery/windows/EagleEye-Setup-0.2.0.exe` (service + tray client)

## Description

TC-002-12 checks that the pairing code appears in the tray client popup on the service PC (AC-14). The popup **"EagleEye – Eltern-App koppeln"** appeared, but its content area is too narrow for its text. The code line shows only the first two digits ("Kopplungscode: 22"), and the instruction line is cut off ("Geben Sie diesen Code in der EagleEye-Eltern-"). The window cannot be used to read the code, so pairing could not be completed and Michael aborted the run at this case.

**Severity justification (Critical):** pairing is the core flow of US-002, and every later case (TC-002-13 to TC-002-43) depends on a paired app. There is no practical workaround. The Event Log path (AC-15) only applies when **no** tray client is connected. The tray client also runs in admin sessions (Q-7), so a parent who is signed in at the service PC always gets the clipped popup instead of an Event Log entry.

## Steps to Reproduce

1. Service PC with `EagleEye-Setup-0.2.0.exe` installed, service running, `eagleeye-kid` signed in (tray client green).
2. As Admin, install and start the EagleEye Parent App, enter the service PC's hostname, then click *Verbinden* (TC-002-11).
3. Switch to `eagleeye-kid` and look at the popup **"EagleEye – Eltern-App koppeln"**.

## Expected Result

AC-14: *"the pairing code appears there as a popup of the tray client that shows the 6 digits and says that they are needed to pair a parent app."*
Test plan TC-002-12: the popup shows **6 digits** ("Kopplungscode: nnnnnn") and the full texts "Geben Sie diesen Code in der EagleEye-Eltern-App ein." and "Der Code ist 5 Minuten gültig."; all content is fully readable.

## Actual Result

Quoted from the run file:

> "The window that shows the code is to small. See screenshot "02_Implementation\docs\testing\US-002\evidence\Screenshot 2026-10-04 203157.png". As a result, the process could not be continued. Test aborted here."

The screenshot shows:
- Title bar: "EagleEye – Eltern-App koppeln" (complete).
- Code line in large bold font: **"Kopplungscode: 22"**. The remaining 4 digits are cut off at the right edge.
- Line 2: "Geben Sie diesen Code in der EagleEye-Eltern-". The end ("App ein.") is cut off.
- Line 3: "Der Code ist 5 Minuten gültig." (complete).
- Button *OK* (complete).

The window's height is sufficient; only the width is too small for the content.

## Evidence

- `docs/testing/US-002/evidence/Screenshot 2026-10-04 203157.png`

## Open Information (TES asks Michael, for DEV)

- Display scaling of the service PC (*Einstellungen → System → Bildschirm → Skalierung*, e.g. 100 % / 125 % / 150 %). The clipping looks like a fixed window size that does not adapt to the text or to DPI scaling. DEV should make sure that it works at the common scaling levels.
- In which session the screenshot was taken (Kid or Admin). Not recorded in the run file (TC-002-12 Notes are empty).

## Re-test

Re-run TC-002-11 (as the trigger) and TC-002-12, followed by the pairing cases that were not executed (see `docs/testing/US-002/test-report.md` §1).

## Answers (Michael, 2026-10-04)

- Session: **admin session**, tray client running there.
- Display scaling: **150 %**.

## Resolution

**Status**: Implemented (DEV, 2026-10-04, Windows Developer Machine). Version stays 0.2.0.

### Root cause

`PairingCodeDialog` laid out its window with fixed pixel sizes (client width 420 px, label boxes 388 px wide, code line 64 px high) and had no auto-scaling (`AutoScaleMode` not set, so `None` for a top-level form). The tray client is DPI-aware (`HighDpiMode.SystemAware`), so Windows does not stretch it; the fonts, which are in points, are rendered at the real DPI. At 150 % (144 DPI) the 20-pt bold code line needs about 490 px and the instruction about 440 px, but the window stayed 420 px wide, so both were cut off on the right. The defect was reproduced on the Windows Developer Machine (admin session, 150 %) with the 0.2.0 code: the rendering matches the screenshot exactly (`ISSUE-004-evidence/before-fix-de-150.png`). At 100 % the texts fit, which is why the DEV smoke check did not show it.

### Fix

`02_Implementation/src/EagleEye.TrayClient/UI/PairingCodeDialog.cs` (the only production change):

- `AutoScaleMode.Dpi` with `AutoScaleDimensions` 96 × 96: all spacing values are logical pixels that WinForms scales to the actual DPI.
- No fixed sizes. The window is `AutoSize` / `AutoSizeMode.GrowAndShrink` and contains one auto-sized `TableLayoutPanel` column (minimum width 360 logical px) with the code label, the instruction, the validity text and the OK button.
- Labels are `AutoSize`, centered, and wrap only above 480 logical px, so a longer text (other language, very large text) gets a second line instead of being cut off.
- The OK button is auto-sized (minimum width 80 logical px).
- The code font is derived from the dialog font (× 2.25, about 20 pt bold), so it scales together with the other texts.

Behavior is unchanged otherwise (texts, topmost, taskbar entry, 5-minute auto-close, replacement by a new code).

`HighDpiMode` stays `SystemAware`. With the auto-sizing layout this is sufficient: the dialog is laid out at the session's DPI. If the scaling is changed without signing out, Windows bitmap-stretches the whole window (slightly blurry, but not clipped). `PerMonitorV2` was considered and not adopted: it is not needed for this fix, and it would change the behavior of the US-001 *App Infos* dialog, which still has fixed sizes (fonts would rescale while the window would not).

### Verification

- **Real 150 % (admin session, 144 DPI):** a throw-away WinForms harness (in the git-ignored `02_Implementation/artifacts/`, deleted after use) created the real `PairingCodeDialog` from the built `EagleEye.TrayClient.dll` with code `223456`, showed it, checked that every label and the button lie fully inside the client area at their preferred size, and saved a screenshot. German and English: OK. Old code: clipped exactly as in the report.
- **Other scaling levels (emulated):** DPI cannot be changed per process, so 100 %, 125 %, 175 % and 200 % were emulated by setting the process default font to 9 pt × (target DPI / 144) (`Application.SetDefaultFont`). The texts then have exactly the pixel size they have at that scaling; only the spacing stays at 150 %. All 10 combinations (5 levels × de/en) are fully readable; a 300 % stress run wraps the code and the instruction onto two lines without clipping.
- Screenshots: `ISSUE-004-evidence/before-fix-de-150.png`, `after-fix-de-150.png`, `after-fix-en-150.png`, `after-fix-emulated-{de,en}-{100,125,175,200}.png`.
- `pwsh 02_Implementation/scripts/build.ps1`: 0 warnings, 0 errors. `pwsh 02_Implementation/scripts/test.ps1`: 318/318 passed. No new unit tests (pure WinForms layout; the plan keeps dialogs out of unit tests).
- `pwsh 02_Implementation/scripts/package-windows.ps1 -Target Service` → `03_Delivery/windows/EagleEye-Setup-0.2.0.exe` rebuilt. The published tray client starts and stays running (smoke check). The installer itself was not run (needs admin).
- `03_Delivery/windows/EagleEye-ParentApp-Setup-0.2.0.exe` is **unchanged** (not rebuilt; no parent app code changed).

### Related observation (not fixed)

The US-001 *App Infos* dialog (`02_Implementation/src/EagleEye.TrayClient/UI/AboutDialog.cs`) has the same fixed-pixel pattern. At 150 % it is still readable (OK button tight); at 200 % the connection-error text fills the window and touches the OK button. The fix would be the same per-dialog layout change, so it was left out of this issue. A separate issue is suggested if it is seen clipped in a test.

### Re-test

Install `03_Delivery/windows/EagleEye-Setup-0.2.0.exe` again over the installed 0.2.0 (it ends the tray client; it comes back at the next sign-in or via Start menu *EagleEye Tray*). Then re-run TC-002-11 and TC-002-12, ideally at 150 % and at least one other scaling level, in an admin and the kid session. The parent app installer did not change.

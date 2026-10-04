# ISSUE-004: Pairing code popup is too small, code and text are cut off

**Status**: New
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

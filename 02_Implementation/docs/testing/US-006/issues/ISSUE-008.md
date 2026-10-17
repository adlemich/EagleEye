# ISSUE-008: Numeric fields of the app rules and the warning levels show no value (Windows parent app)

**Status**: Implemented in code (patch **0.6.1**); installers pending, see "Blocker" below
**User Story**: US-006
**Found in**: `docs/testing/US-006/test-run-01.md` (Michael's first live use, step e)
**Date**: 2026-10-17
**Severity**: Medium (values are stored and enforced correctly but cannot be read or checked in the parent app)
**Machine**: parent app on `LEOSERV`, paired with the service PC `ZOCK-O-MAT-V3`
**Build**: `03_Delivery/windows/EagleEye-ParentApp-Setup-0.6.0.exe`
**Evidence**: `Screenshot 2026-10-17 um 10.49.59.png` (Settings), `Screenshot 2026-10-17 um 11.04.24.png` (Rules); after the fix: `ISSUE-008-after-*.png` (this folder)

## Description

In the Windows parent app the numeric Entry fields are empty although values are stored:

- Settings → "Zeit-Ende Warnungen": the fields "Minuten vor Ende" of *Frühe Warnung* and *Finale Warnung*. The text editors next to them show their texts.
- Rules → "Applikations-Regeln": every *Stunden* and *Minuten* field. The day checkboxes are correct.

The break-time fields *Start-Zeit* / *End-Zeit* (US-005) display correctly.

## Steps to Reproduce

1. Parent app 0.6.0 on Windows, paired and connected.
2. Open *Einstellungen* and scroll to "Zeit-Ende Warnungen": the minute fields are empty.
3. Open *Regeln* for a controlled account with app records: the hours and minutes fields are empty.

## Expected Result

Every field shows its stored value (e.g. 5 / 1; 1 / 30) at 100 % and 150 % scaling, in light and dark mode (US-006 AC-2, AC-6, AC-13).

## Actual Result

The fields are empty. UI Automation reports the correct value, so the DEV smoke check of 2026-10-12 (values read through UI Automation) did not catch it.

## Resolution

*(DEV, 2026-10-17)*

**Status**: Implemented (patch **0.6.1**), installers pending (see Blocker)

**Root cause**: the four Entry types were the only ones with `HorizontalTextAlignment="End"` (WinUI `TextBox.TextAlignment = Right`) in a narrow cell (`WidthRequest="56"`). In that combination WinUI positions the right-aligned text outside the visible part of the TextBox: the WinUI TextBox keeps its own minimum width, padding and the reserved column of the delete button, so the text box is wider than its slot and its right edge is clipped. The value is set; it is only drawn where it cannot be seen. Proven by one-attribute experiments on the Debug app (screenshots read by DEV):
- `End` alignment removed, width kept → the value is visible ("2");
- width removed, `End` alignment kept → still empty.

The orchestrator's hypothesis (alignment, padding and the delete-button column) is confirmed for the alignment; the width alone is not the cause.

**Fix**: `HorizontalTextAlignment="End"` removed from the four Entry types (`Views/SettingsView.xaml`: early and final minutes; `Views/RulesView.xaml`: hours and minutes). The width of 56 and the narrow column layout of the mockup are kept; the digits are now left-aligned inside the field, like the break-time fields of US-005. No code change, so no new unit test.

**Visual verification** (Debug parent app 0.6.1 against the Debug console service on the offset ports; screenshots taken with `PrintWindow` and read by DEV; this PC runs at 200 %):
- `ISSUE-008-before-settings.png`: 0.6.0 behaviour reproduced (minute fields empty);
- `ISSUE-008-after-settings-dark.png`: "2" and "1" visible;
- `ISSUE-008-after-rules-dark.png`: Editor 1 / 30, Minecraft 23 / 59, Paint 0 / 0 visible (two-digit values not cut off);
- `ISSUE-008-after-settings-light.png`, `ISSUE-008-after-rules-light.png`: the same in light mode.

**Not verified by DEV**: 150 % (this PC is at 200 %; the fix does not depend on the scaling). Please check on `LEOSERV`.

**Side observation (reported, not changed)**: in the Rules screenshot from `LEOSERV`, unticked checkboxes are drawn with a "–" glyph: filled in the break table, faint in the app-rules table. On `ZOCK-O-MAT-V3` the same view (Debug build, dark and light mode, 200 %) shows unticked boxes as plain empty boxes in both tables (`ISSUE-008-after-rules-*.png`). The account checkboxes on the Settings page in the same `LEOSERV` screenshot look normal, and they use the same handler mapping (`CheckBox MinWidth = 0`, US-005). So the "–" look is not reproducible here and is not caused by this defect. A WinUI rendering difference on `LEOSERV` (Windows build, remote session) is possible. Next step: Michael compares the installed 0.6.0/0.6.1 on `ZOCK-O-MAT-V3` with `LEOSERV`.

**Blocker for the installers**: since 2026-10-12, NuGet audit reports the high-severity advisory GHSA-334f-vvp4-7g6p for `Microsoft.AspNetCore.SignalR.Common` 10.0.12 (pulled in by `Microsoft.AspNetCore.SignalR.Client` 10.0.12 in `EagleEye.ParentApp.Core` and `EagleEye.TrayClient`). With warnings as errors, the restore now fails, so `build.ps1`, `test.ps1` and `package-windows.ps1` fail on this PC. No patched version is in the local package cache. Michael decides: (a) grant internet access to update the SignalR client packages to the patched version, or (b) suppress the advisory temporarily (`NuGetAuditSuppress`). DEV builds the 0.6.1 installers after the decision. For the checks above, DEV built locally with `NuGetAudit=false` set only in the shell environment (not committed).

**Files**: `02_Implementation/src/EagleEye.ParentApp/Views/SettingsView.xaml`, `02_Implementation/src/EagleEye.ParentApp/Views/RulesView.xaml`, `02_Implementation/Directory.Build.props` (0.6.1).

**Build** (local, `NuGetAudit=false`): `build.ps1` 0 warnings, 0 errors (Shared, Service, TrayClient, ParentApp.Core, ParentApp Windows, ParentApp Android). `test.ps1` 2 623 passed, 0 failed (Shared 282, Service 1 365, TrayClient 90, ParentApp 886).

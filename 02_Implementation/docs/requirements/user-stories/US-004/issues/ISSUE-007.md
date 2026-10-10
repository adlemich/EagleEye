# ISSUE-007: Section headings are hard to tell apart; Berichte should be the start page

**Status**: Verified/Closed (visual re-test on build 0.4.1 passed per Michael's report; closed by Michael, 2026-10-10)
**User Story**: US-004
**Found in**: `docs/testing/US-004/test-run-01.md` (Michael's change request after run 01; not a failed case)
**Date**: 2026-10-10
**Severity**: Low (visuals and usability; no function is affected)
**Machine**: parent app on `LEOSERV`, paired with the service PC `ZOCK-O-MAT-V3`
**Build**: `03_Delivery/windows/EagleEye-ParentApp-Setup-0.4.0.exe` (parent app)
**Evidence**: `docs/testing/US-004/evidence/visuals_1.png` (Berichte), `docs/testing/US-004/evidence/visuals_2.png` (Einstellungen)

## Description

Run 01 of US-004 had no failures. Michael raised a change request for three minor visual and usability points. The Orchestrator recorded it from his screenshots ("Current" → "Desired") on 2026-10-10. Michael wants the fix delivered as patch **0.4.1**, followed by a quick visual re-test (no repeat of the skipped cases).

1. **Berichte: day headings.** The day blocks ("Heute, 10.10.2026", "08.10.2026") are only bold text, so the days do not read as separate blocks. Each day heading should be a full-width bar with a coloured background in the standard Windows highlight (accent) colour.
2. **Berichte as the start page.** When the app starts, it should open on *Berichte*, not *Einstellungen*.
3. **Einstellungen: section headings.** The section headings *Darstellung*, *Serververbindung* and *Benutzerkonten auf dem EagleEye-PC* should use the same coloured heading bar as item 1.

## Steps to Reproduce

1. Parent app 0.4.0, paired and connected; at least two days with recorded usage.
2. Start the app: it opens on *Einstellungen* (item 2). Check the section headings (item 3).
3. Open *Berichte* and check the day headings (item 1).

## Expected Result (Michael's screenshots, "Desired")

- **Heading bar** (items 1 and 3): a full-width bar across the content area with the system highlight/accent colour as background (on Windows the user's accent colour, the blue in the screenshots), heading text in a contrasting colour (white in the screenshot) with some inner padding. Text and order are unchanged. *Note: the mockup says "Benutzerkonten **aud** dem EagleEye-PC". That is a typo in the mockup; the text stays "auf".*
- One shared style for both pages, used for every Reports day heading and every Settings section heading.
- Unchanged: the page titles *Berichte* / *Einstellungen* (no bar), the *Konto* picker, the *App* / *Nutzung (HH:MM)* column headers, row content, the navigation menu, the status bar.
- **Start page** (item 2): with a paired app, the app opens on *Berichte* and the menu entry *Berichte* is selected. How an unpaired app starts (pairing flow) is unchanged.
- Readable in light and dark mode and at 150 % display scaling (lesson from ISSUE-004). The text must stay readable when the user's accent colour changes.
- On the other targets (Android, iOS, macOS) the bar uses the platform accent colour, or the app's primary colour where the platform has none.

## Actual Result

Screenshots, "Current" (dark mode): day headings and Settings section headings are bold text with no background. The app starts on *Einstellungen*.

## Impact on the story

The ACs of US-004 do not specify heading styling or the start page, so no AC changes. This is a usability change on the parent app only. Service and tray client behaviour is not affected. Version **0.4.1**; DEV follows the 0.3.1 precedent for which installers are rebuilt.

## Resolution

*(DEV, 2026-10-10)*

**Status**: Implemented (patch **0.4.1**)

**Root cause**: the style `SectionHeader` (`Resources/Styles/Styles.xaml`), used by every Settings section heading and every Reports day heading, was only bold text in the normal text colour. The page stacks were limited to 760 units and aligned left, so a background could not have reached the right edge. `MainViewModel.SelectedItem` defaulted to the first menu entry (*Einstellungen*) and the start sequence never changed it.

**Fix**:
- **Heading bar (items 1 and 3)**: the one shared style `SectionHeader` is now a full-width bar: background `HeadingBarBackgroundColor`, text `HeadingBarTextColor` (both dynamic resources), padding 14 × 6, font size 16, regular weight as in the mockup. It is used by the three Settings section headings (text unchanged: "Benutzerkonten **auf** dem EagleEye-PC") and by every Reports day heading; nothing else uses it. The outer stacks of `SettingsView` and `ReportsView` fill the content area, so the bar spans it fully, as in the mockup. Page titles (`PageHeader`), the *Konto* picker, column headers, rows, menu and status bar are unchanged.
- **Accent colour**: `Services/HeadingBarColorService` sets both resources when the window is created, whenever it is activated, and on Windows when the accent colour changes (`UISettings.ColorValuesChanged`, handled on the UI thread). Windows: the user's accent colour (`UISettings.GetColorValue(UIColorType.Accent)`, `Platforms/Windows/WindowsAccentColor`). Android: the system accent colour (`system_accent1_600`, Material You, `Platforms/Android/AndroidAccentColor`). iOS and macOS: no platform reader yet, so the bar uses the app's primary colour `#1E7B3A` (also the default in `Colors.xaml`, used before the service runs).
- **Readable text**: `EagleEye.ParentApp.Core/Appearance/HeadingBarPalette` picks white text when its WCAG contrast with the bar is at least 4.5:1 (Windows default blue `#0078D4`: white, as in the mockup), otherwise black (light accent colours such as yellow; black then has at least 4.6:1). The bar colours do not depend on light/dark mode; sizes are device-independent and scale with the display.
- **Start page (item 2)**: `MainViewModel.StartAsync` selects *Berichte* when the stored pairing is valid (any status other than *not paired*). The menu entry follows through its binding, and the page switches. An unpaired app (or an invalid stored pairing) stays on *Einstellungen* and shows the host dialog as before. Reports is selected even when the menu has already written *Einstellungen* back through its two-way binding at load (seen in the smoke check).

**Files**: `02_Implementation/src/EagleEye.ParentApp.Core/Appearance/HeadingBarPalette.cs`, `02_Implementation/src/EagleEye.ParentApp.Core/Appearance/RgbColor.cs`, `02_Implementation/src/EagleEye.ParentApp.Core/ViewModels/MainViewModel.cs`, `02_Implementation/src/EagleEye.ParentApp/Services/HeadingBarColorService.cs`, `02_Implementation/src/EagleEye.ParentApp/Platforms/Windows/WindowsAccentColor.cs`, `02_Implementation/src/EagleEye.ParentApp/Platforms/Android/AndroidAccentColor.cs`, `02_Implementation/src/EagleEye.ParentApp/Resources/Styles/Styles.xaml`, `02_Implementation/src/EagleEye.ParentApp/Resources/Styles/Colors.xaml`, `02_Implementation/src/EagleEye.ParentApp/Views/ReportsView.xaml`, `02_Implementation/src/EagleEye.ParentApp/Views/SettingsView.xaml`, `02_Implementation/src/EagleEye.ParentApp/App.xaml.cs`, `02_Implementation/src/EagleEye.ParentApp/MauiProgram.cs`, both component READMEs, `02_Implementation/Directory.Build.props` (0.4.1); tests `02_Implementation/tests/EagleEye.ParentApp.Tests/Appearance/HeadingBarPaletteTests.cs`, `02_Implementation/tests/EagleEye.ParentApp.Tests/ViewModels/MainViewModelTests.cs`.

**Build**: `build.ps1` 0 warnings, 0 errors (Shared, Service, TrayClient, ParentApp.Core, ParentApp Windows, ParentApp Android). `test.ps1` all 1 513 unit tests pass (Shared 146, Service 782, TrayClient 40, ParentApp 545); `HeadingBarPalette`, `RgbColor` and `MainViewModel` at 100 % line and branch coverage.

**Artifacts** (whole solution 0.4.1; service and tray code unchanged since 0.4.0, only the version number differs):

| Installer | Built | SHA-256 |
|---|---|---|
| `03_Delivery/windows/EagleEye-Setup-0.4.1.exe` | 2026-10-10 10:31 | `2565b31e023f30bca2123b2540953742a2ea4ffb441f0f8b4fd0d9c05cd41fbe` |
| `03_Delivery/windows/EagleEye-ParentApp-Setup-0.4.1.exe` | 2026-10-10 10:32 | `274d82fc90b44249f3c29c18ad75269162d922edc64235d2bbbe87b1b74ed472` |

**Smoke check** (Windows Developer Machine `ZOCK-O-MAT-V3`, 150 % scaling, dark mode, orange-red accent colour; Michael's installed service 0.4.0 not touched, no pairing with it): the Debug parent app paired with the Debug console service on the offset ports (implementation report §8) opens on *Berichte* with the menu entry selected; both day headings and the three Settings headings are full-width bars in the accent colour with white text. The parent app installer 0.4.1 installs per user (file version 0.4.1.0), the app starts unpaired on *Einstellungen* with the bars, and it uninstalls cleanly. **Michael/TES**: check on `LEOSERV` with the 0.4.1 installers: light mode, a changed accent colour while the app runs (also a light one, e.g. yellow → black text), and 150 % scaling there (a screenshot can go to `docs/testing/US-004/evidence/ISSUE-007-after-fix.png`).

# ISSUE-007: Section headings are hard to tell apart; Berichte should be the start page

**Status**: New
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

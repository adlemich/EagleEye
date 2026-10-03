# ISSUE-001: Tray client texts are English only, German is required as default

**Status**: Verified/Closed
**User Story**: US-001
**Found in**: docs/testing/US-001/test-run-01.md, TC-001-05, TC-001-06, TC-001-08, TC-001-09, General Feedback
**Date**: 2026-10-03
**Severity**: Medium
**Machine**: Windows Developer Machine
**Routed to**: DEV (implementation did not meet NFR-L-010 to NFR-L-012)

## Description

All user-facing texts of the tray client (tooltip, context menu, About dialog) are hard-coded in English. The product requirements demand multi-language support, with German as the default language and English as the second language, and all user-facing text externalised for translation (NFR-L-010, NFR-L-011, NFR-L-012). The functionality itself works; only the language is wrong.

## Steps to Reproduce

1. Sign in as `eagleeye-kid` on a German Windows 11 (service running).
2. Hover over the tray icon; right-click it; open the About entry; stop the service and hover again.

## Expected Result

German texts on a German system, including the About dialog. Texts named by Michael:

| Where | Now | Expected (de) |
|---|---|---|
| Tooltip connected | EagleEye — Connected | EagleEye — Verbunden |
| Tooltip disconnected | EagleEye — Disconnected | EagleEye — Verbindungsfehler |
| Context menu | About | App Infos |
| About dialog | English title and labels | German title and labels |

English texts remain available for English systems.

## Actual Result

Quoted from the run file: "Functionality is good, but it uses english texts 'Connected' instead of 'Verbunden'" (TC-05), "'About' instead of 'App Infos'" (TC-06), "'Disconneced' instead of 'Verbindungsfehler'" (TC-08), "This dialog is not multi-language enabled" (TC-09). General feedback: "Make sure that multi-language is used in all areas including the about box."

## Evidence

Run file observations only; no screenshots.

## Resolution

DEV, 2026-10-03: tray texts moved to resource files (`EagleEye.TrayClient/Resources/TrayTexts.resx` = German, neutral/default; `TrayTexts.en.resx` = English). Build 0.1.1. See implementation report §7. Re-test: `docs/testing/US-001/test-run-02.md`.

**Verified**: TES, 2026-10-03, in `docs/testing/US-001/test-run-02.md` (build 0.1.1). Closed by Michael.

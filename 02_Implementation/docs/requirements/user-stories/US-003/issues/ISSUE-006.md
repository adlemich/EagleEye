# ISSUE-006: Account list is hard to read — show it as a table with column headers

**Status**: New
**User Story**: US-003
**Found in**: Michael's functional check of build 0.3.0 before test run 01 (not a test-run case)
**Date**: 2026-10-07
**Severity**: Low (usability; no function is affected)
**Machine**: parent app on `LEOSERV`, paired with the service PC `ZOCK-O-MAT-V3`
**Build**: `03_Delivery/windows/EagleEye-ParentApp-Setup-0.3.0.exe` (parent app)
**Evidence**: `docs/testing/US-003/evidence/issue_formatting_accounts.png`

## Description

In *Einstellungen → Benutzerkonten auf dem EagleEye-PC*, each account row shows the account name on the left, the checkbox next to it, and the label "Unter Elternkontrolle" far to the right, repeated in every row (implementation report §6, "Row layout"). The label is separated from the checkbox it belongs to, and the list does not read as a list. Michael asks for a two-column table with column headers.

Recorded by the Orchestrator from Michael's screenshot and instruction (2026-10-07). Michael wants the fix delivered as patch **0.3.1** before test run 01.

## Steps to Reproduce

1. Parent app 0.3.0 paired and connected with a service that has at least one standard account.
2. Open *Einstellungen*, scroll to *Benutzerkonten auf dem EagleEye-PC*.

## Expected Result (Michael's screenshot, "Should be")

A table with two columns:

| Konto | Unter Elternkontrolle |
|---|---|
| eagleeye-kid | ☑ |

- Header row with the column titles **"Konto"** (English: "Account") and **"Unter Elternkontrolle"** (English: "Under parental control"), visually set apart as headers (bold).
- One row per account: the account name as shown today (AC-11, incl. "(deaktiviert)" per AC-12) in the first column, the checkbox in the second column, aligned under its header.
- The per-row label "Unter Elternkontrolle" is no longer repeated in each row.
- Unchanged: section title, instruction text "Markieren Sie die Konten, die unter Elternkontrolle stehen.", sorting, the states "Keine Daten verfügbar" / "Keine Nicht-Administrator-Konten vorhanden" / "Wird geladen …" (no table header in these states), disabled checkbox while saving, error text below the list. Readable at 150 % display scaling (lesson from ISSUE-004). Light and dark mode.

## Actual Result

Screenshot (dark mode): row "eagleeye-kid ☑ ………… Unter Elternkontrolle" with the label at the far right of the row, no column headers.

## Impact on the story

AC-10 asks for "a checkbox *Under parental control*" per account. With the table, the checkbox is labelled by its column header instead of a per-row label. This fulfils AC-10 in substance (OQ-8 label kept as column title). No AC change needed; TES's test documents already describe the rows layout-neutrally.

## Resolution

*(DEV)*

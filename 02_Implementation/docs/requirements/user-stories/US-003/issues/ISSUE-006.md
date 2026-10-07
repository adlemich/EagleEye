# ISSUE-006: Account list is hard to read — show it as a table with column headers

**Status**: Verified/Closed (TC-003-36 Pass in `docs/testing/US-003/test-run-01.md`, build 0.3.1; closed by Michael, 2026-10-07)
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

*(DEV, 2026-10-07)*

**Status**: Implemented (patch **0.3.1**)

**Root cause**: the row template in `SettingsView.xaml` had three auto-sized columns per row (`*,Auto,Auto`: name, checkbox, label "Unter Elternkontrolle"). The label was repeated in every row, the `*` column pushed it to the far right, and there was no header row. The layout followed the plan (`SettingsView`: "`CheckBox` … followed by the label `UnderParentalControl`, MAUI `CheckBox` has no text").

**Fix**:
- The section shows the accounts as a two-column table. A header row with the bold column titles **"Konto" / "Account"** (new text key `AccountColumnHeader`) and **"Unter Elternkontrolle" / "Under parental control"** (existing key `UnderParentalControl`) sits above the rows. Each row has the shown name (AC-11/AC-12, unchanged) in column 1 and the checkbox centred in column 2, under its header. The per-row label is gone.
- Header and rows use the same fixed column widths (360 and 200 device-independent units, 16 apart), so they line up in every row. They scale with the Windows display scaling, because MAUI sizes are device-independent. Long names wrap inside column 1.
- The header is bound to `IsListVisible`, like the instruction and the rows. It is only shown with at least one row, never with "Keine Daten verfügbar", "Keine Nicht-Administrator-Konten vorhanden" or "Wird geladen …".
- Accessibility: every checkbox gets the automation name `UserAccountItemViewModel.CheckBoxAutomationName`, e.g. "Unter Elternkontrolle: eagleeye-kid" / "Under parental control: eagleeye-kid" (new text key `AccountCheckBoxNameFormat`, via `SemanticProperties.Description`). It follows renames.
- Unchanged: section title, instruction, sorting, merge, disabled checkbox while saving, error text below the list, all states. Colours come from the existing light/dark styles; the header uses the default text colour with `FontAttributes="Bold"`, nothing theme-specific was added.

**Files**: `02_Implementation/src/EagleEye.ParentApp/Views/SettingsView.xaml`, `02_Implementation/src/EagleEye.ParentApp.Core/ViewModels/UserAccountItemViewModel.cs`, `02_Implementation/src/EagleEye.ParentApp.Core/AppTexts.cs`, `02_Implementation/src/EagleEye.ParentApp.Core/Resources/AppTexts.resx`, `02_Implementation/src/EagleEye.ParentApp.Core/Resources/AppTexts.en.resx`, `02_Implementation/Directory.Build.props` (0.3.1); tests `02_Implementation/tests/EagleEye.ParentApp.Tests/ViewModels/UserAccountItemViewModelTests.cs`, `02_Implementation/tests/EagleEye.ParentApp.Tests/AppTextsTests.cs`.

**Build**: `build.ps1` 0 warnings, 0 errors (Shared, Service, TrayClient, ParentApp.Core, ParentApp Windows, ParentApp Android). `test.ps1` all 928 unit tests pass (Shared 141, Service 303, TrayClient 40, ParentApp 444); `UserAccountItemViewModel`, `UserAccountsViewModel` and `AppTexts` stay at 100 % line and branch coverage.

**Artifacts** (whole solution 0.3.1; service and tray code unchanged since 0.3.0, only the version number differs):

| Installer | Built | SHA-256 |
|---|---|---|
| `03_Delivery/windows/EagleEye-Setup-0.3.1.exe` | 2026-10-07 12:59 | `6693dc9b362a60dd5fd7d30aa5e96e54ed50503ead11b66dc757df6c48434420` |
| `03_Delivery/windows/EagleEye-ParentApp-Setup-0.3.1.exe` | 2026-10-07 13:00 | `d6e5efc5892825a0ad0c3ad9cac04a1d520f56d04cd7f7b114da52b5e8627918` |

**Smoke check — not completed (visual layout, light/dark, 150 % and screenshot open)**: the parent app installer 0.3.1 installs (per user, file version 0.3.1.0) and the app starts. Before I could pair it with a console service, the EagleEye service 0.3.0 that Michael had installed in the meantime was holding ports 5443/5080, so the console service could not start. DEV did not pair against Michael's installed service. One attempt by mistake failed with a wrong code, so no device was registered. Its entries are in that service's log at 13:01:06 to 13:01:46: connect, pairing started, code sent to the tray, rejected WrongCode, disconnect. The app is uninstalled again. **Michael/TES**: check the table in light and dark mode and at 150 % with the 0.3.1 installers (a screenshot can go to `docs/testing/US-003/evidence/ISSUE-006-after-fix.png`).

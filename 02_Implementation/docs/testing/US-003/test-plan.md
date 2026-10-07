# Test Plan: US-003 — Account Inventory and Selection of Accounts under Parental Control

**Status**: Approved by Michael (2026-10-07); Q-1 to Q-9 answered (§8). Installer references to be updated to patch 0.3.1 (ISSUE-006) before test run 01.
**Date**: 2026-10-07
**Author**: TES
**User Story**: `02_Implementation/docs/requirements/user-stories/US-003/user-story.md` (24 ACs, approved 2026-10-07; status `Implemented`)
**Inputs used**: the user story; `02_Implementation/docs/requirements/user-stories/US-003/implementation-report.md` (§2 deviations D-1 to D-8, §5 risks, §6 "How to Test": artifacts, UI texts, log line formats); `02_Implementation/docs/requirements/user-stories/US-003/implementation-plan.md` ("Manual Verification Notes", "Deviations from the Story" D-1 to D-7, answers Q-1 to Q-7). Both were used only for setup, artifact locations and texts, not for expected behaviour. Also `02_Implementation/docs/architecture/decisions/ADR-010-event-driven-state-propagation.md` and the US-002 test documents (`docs/testing/US-002/test-plan.md`, `test-run-02.md`, `test-report.md`) for setup steps and lessons (ISSUE-004 scaling, ISSUE-005 elevated terminal). Black box: no source code was read.

---

## 1. Scope

**In scope**

- Update of the service and the parent app from 0.2.0 to 0.3.1, keeping the pairing (supporting AC-15)
- The ISSUE-006 fix in patch 0.3.1: the account list as a two-column table with headers (TC-003-36)
- The admin-only service log folder `%ProgramData%\EagleEye\logs\` and the log entries named in AC-14 and AC-24
- Inventory content: standard accounts only, no admins, no built-in accounts, no `defaultuser0`, local and Microsoft-linked accounts, disabled accounts (AC-1 to AC-4, AC-12)
- The new settings section: position, states (no data, no accounts, loading, list), row format, sort order (AC-6 to AC-13)
- Ticking and unticking: immediate save, log entry, persistence across app restart, service restart, reboot and re-install, error and revert, only while connected, default unticked (AC-14 to AC-18)
- Account changes while the service runs: add, delete, rename, standard ↔ admin, disable, with the 60-second limit (AC-5, AC-19 to AC-22)
- Two parent apps (service PC + second Windows PC): broadcast within 5 s, concurrent changes (AC-23, AC-24)

**Out of scope** (as in the story): monitoring and enforcement (ticking has no visible effect on the kid's PC); application inventory; per-account configuration pages; parent apps for Android, iOS and macOS; domain and Entra ID accounts; the tray client; changing accounts from the parent app; protection against a kid pairing their own app (OQ-7).

**Not tested in this plan, with reason**

| Item | Reason |
|---|---|
| AC-2 "admin through another group" (nested group) | On a workgroup PC (no domain) Windows does not allow a local group inside the local group *Administratoren*, so indirect membership cannot be set up. Covered by DEV's design (`LG_INCLUDE_INDIRECT`); see §8 Q-6. |
| Built-in accounts that are **enabled** or **renamed** (implementation plan, Manual Verification Notes) | Not required by AC-3. Enabling *Gast* would weaken the PC's security. The test relies on this: the built-in accounts are disabled, and disabled standard accounts *are* listed (AC-12). So if they are missing from the list, they were excluded as built-ins. |
| English UI texts | Michael's Windows is German. English texts are covered by DEV's unit tests. If PC2 runs Windows in English, Block E records them as a bonus (Notes). |
| Log retention (3 files, 5 days, 50 MB) | Not an AC of this story (Q-1, FR-SVC-103). It would need days or 150 MB of log. DEV's unit tests cover it. |
| Regression | Story runs contain no regression cases (`02_Implementation/docs/testing/README.md` §3, TES rule 4). |

## 2. Test Environment

| Item | Value |
|---|---|
| Service PC | **Windows Developer Machine** (Windows 11 Pro, German UI), called *service PC*. `<host>` = its computer name (`hostname`). |
| Second PC (**Block E only**) | **PC2**, the second Windows 11 PC from US-002 (same LAN and subnet). Michael confirmed it (implementation plan Q-6). |
| Parent apps | **App A** = the parent app in Michael's admin account on the service PC, paired as **Papas PC** (end state of US-002). **App B** = the parent app on PC2, paired in Block E as **PC2**. |
| Accounts on the service PC | **Admin** = Michael's administrator account. **Kid** = `eagleeye-kid` (standard, exists since US-001). Test accounts created in setup S-12 (table below). |
| Builds under test | `03_Delivery/windows/EagleEye-Setup-0.3.1.exe` (service + tray, admin; built 2026-10-07 12:59, SHA-256 `6693dc9b…48434420`) and `03_Delivery/windows/EagleEye-ParentApp-Setup-0.3.1.exe` (parent app, per user; built 2026-10-07 13:00, SHA-256 `d6e5efc5…b8627918`). Patch 0.3.1 = 0.3.0 plus the ISSUE-006 fix (account list as a table); service and tray code are unchanged, only the version number differs (implementation report §8, `US-003/issues/ISSUE-006.md` Resolution). |
| Starting builds (upgrade path) | `03_Delivery/windows/EagleEye-Setup-0.2.0.exe` and `03_Delivery/windows/EagleEye-ParentApp-Setup-0.2.0.exe` (the US-002 builds), installed and paired in setup S-2/S-3. The upgrade under test is **0.2.0 → 0.3.1**. At the last check, **service 0.3.0** was installed on the service PC (Michael's quick check of 0.3.0); S-2 removes it first. |
| Expected versions | *Installierte Apps*: **EagleEye 0.3.1** and **EagleEye Parent App 0.3.1**, publisher Michael Adler. Tray *App Infos*: `EagleEye_v0.3` (the service reports major.minor, as `EagleEye_v0.2` for 0.2.0; if it shows `EagleEye_v0.3.1`, that is a Note, not a Fail). |
| Tools | Stopwatch (phone). **Terminal (Administrator)** (elevated, S-6) for every account change, service command and log-folder check. A second elevated terminal as **log monitor** (S-7). `lusrmgr.msc` (*Lokale Benutzer und Gruppen*), *Einstellungen → Konten → Andere Benutzer*, *Ressourcenmonitor* (`resmon`, only for the TC-003-23 alternative). |
| Time needed | Setup incl. removing 0.3.0, 0.2.0 install and pairing ≈ 25 min · Block A ≈ 30 min · Block B ≈ 40 min · Block C ≈ 40 min (contains a reboot) · Block D ≈ 35 min · Block E ≈ 50 min (includes the AC-16 case). Total ≈ 4 h; blocks can run on different days, in this order. |

### Test accounts (created in S-12, removed in cleanup §7)

| User name | Full name | Type | Purpose | Shown in the list as (expected) |
|---|---|---|---|---|
| `eagleeye-kid` | as it is (noted in S-10) | Standard | logged on (AC-1), AC-9, AC-21 unticked | `<full name> (eagleeye-kid)` or `eagleeye-kid` |
| `ee-anna` | `anna Test` (lower-case *a* on purpose) | Standard | sort order (AC-10), rename, disable | `anna Test (ee-anna)` |
| `ee-max` | `Max Test` | Standard | tick (AC-14), standard ↔ admin (AC-20, AC-21) | `Max Test (ee-max)` |
| `ee-lena` | — (empty) | Standard | user name only (AC-11), delete (AC-22) | `ee-lena` |
| `ee-gesperrt` | — (empty) | Standard, **disabled** | AC-12 | `ee-gesperrt (deaktiviert)` |
| `defaultuser0` | — | Standard | must **not** appear (AC-3) | — (hidden) |
| `defaultuser1` | — | Standard | look-alike, **must** appear (AC-3, Q-7) | `defaultuser1` |
| `ee-admin` | `EE Admin` | **Administrator** | must not appear (AC-2) | — (hidden) |

All test accounts except `eagleeye-kid` are **never logged on** (AC-1). Later cases add `ee-neu` (Block D) and `ee-zwei` (Block E) for a short time.

### Test blocks

| Block | Cases | Machine / accounts | Duration |
|---|---|---|---|
| **A** Update to 0.3.1, admin-only log folder | TC-003-01 to TC-003-07 (7) | Service PC / Admin + Kid | ≈ 30 min |
| **B** Inventory content and display | TC-003-08 to TC-003-13, **TC-003-36**, TC-003-14, TC-003-15 (9) | Service PC / Admin | ≈ 40 min |
| **C** Selecting, saving, keeping | TC-003-16 to TC-003-22 (7) | Service PC / Admin | ≈ 40 min |
| **D** Account changes while the service runs | TC-003-24 to TC-003-30 (7) | Service PC / Admin | ≈ 35 min |
| **E** Two parent apps, error case | TC-003-31 to TC-003-33, **TC-003-23**, TC-003-34, TC-003-35 (6) | PC2 + service PC / Admin | ≈ 50 min |

36 cases (TC-003-36 was added for patch 0.3.1 / ISSUE-006 and runs in Block B after TC-003-13). Blocks build on each other (accounts and ticks), so run them in the order A → E. A break between blocks is fine; keep the service PC and the accounts as they are.

**TC-003-23 (AC-16) runs in Block E**, after TC-003-33. DEV found that on the service PC itself the app notices a stopped service at once, so a failed save cannot be provoked there by stopping the service (implementation report §5.4). The reliable way is the network cable of PC2. The case keeps its number so that the references stay valid.

### Conventions for expected results

- **UI texts.** Michael's Windows is German, so the expected results quote the German texts. The story gives English examples with the German wording in brackets. A different wording with the same meaning is a **Note**, not a Fail (story, "Language of UI texts"; US-002 Decision Q-11). If you want a text changed, write the wanted text in Notes; TES routes it to PRO. **Exceptions**, agreed in the story (OQ-8, AC-6) and therefore Pass criteria: the section title **Benutzerkonten auf dem EagleEye-PC**, the checkbox label **Unter Elternkontrolle**, and the single menu entry **Einstellungen**. See §8 Q-3.
- **Windows labels** are those of a German Windows 11 (*Lokale Benutzer und Gruppen*, *Vollständiger Name*, *Konto ist deaktiviert*, *Kontotyp ändern*, *Standardbenutzer*, *Administratoren*, *Zugriff verweigert*). If a label differs on your build, use the equivalent and write it in Notes.
- **Timing.**
  - *Account changes on the service PC* (AC-19 to AC-21): start the stopwatch when the change is done (the PowerShell command has returned, or you clicked *OK* / *Übernehmen* in the Windows dialog). Stop it when the account list in the parent app shows the change. **Limit 60 s.** The design checks every 15 s, so expect about 1 to 16 s; anything up to 60 s is a Pass. Over 60 s is a Fail. Do not touch the parent app while timing.
  - *Ticks* (AC-14, AC-16, AC-23, AC-24): **limit 5 s**. Start at the click. For AC-14 the proof is the log line in the log monitor (S-7).
  - *Connection*: bounds as in US-002 (red within 30 s after the service stops, green within 60 s after it starts).
  - Where the story gives no limit, slowness is a Note, not a Fail.
- **Display scaling 150 %.** The service PC runs at 150 % (US-002, ISSUE-004). In every case that looks at the new section: if any text, row or checkbox is cut off, write it in Observed and mark the case **Fail**.
- **Row being saved.** While a tick is being saved, the row's checkbox is disabled (greyed out), normally well below 1 s, at most 4 s; other rows stay usable (implementation report §6). That is expected.
- **Unexpected error text.** The error "Die Änderung konnte nicht gespeichert werden. Bitte erneut versuchen." appears below the list. It is expected only in TC-003-23. If it appears anywhere else, mark the case **Fail**, note the clock time, and run LOG-COPY. One known cause: the service could not read the Windows accounts since its start (implementation report D-6); the log then shows "Reading the local accounts failed …".
- **Unspecified behaviour** you notice is a Note; TES routes it to PRO.
- **No regression cases** (testing README §3).

### Service log files as evidence (new rule from 0.3.0)

The service writes its log to `%ProgramData%\EagleEye\logs\EagleEye.Service-NNN.log` (e.g. `-001`). The folder is **admin-only**, so every log command runs in a **Terminal (Administrator)**. The same Information entries also go to the Event Log (*Ereignisanzeige → Windows-Protokolle → Anwendung*, source **EagleEye**).

**If there is no log file**: the service could not restrict the `logs\` folder and runs **without** a log file, so that it never writes a log that a kid could read (implementation report §5.2). Then look in the Event Log (*Anwendung*, source **EagleEye**) for a warning like "The log folder … could not be restricted …; no log file is written." Mark the current case **Fail**, take a screenshot of that warning into `evidence/`, and continue with the Event Log as the log source (same entries).

- **Log monitor** (S-7): an elevated terminal that shows new log lines live. Several cases read the result there (AC-14, AC-24).
- **Copy the log files** (`02_Implementation/docs/testing/README.md` §4) with the command **LOG-COPY** below:
  - **at the end of every run** (or of every block, if you stop between blocks) → folder `run-01-service-logs`
  - **right after any Fail** → folder `run-01-service-logs\after-TC-003-NN` (NN = the failed case)
  - **before TC-003-06** deletes the log folder → folder `run-01-service-logs\before-TC-003-06`
- Reference the copy in the case's **Notes** (e.g. "logs: `evidence/run-01-service-logs/after-TC-003-16/`").
- Before committing, look through the copies. They contain user names, SIDs and device names, nothing secret (the service never logs secrets or pairing codes). If they contain names of real family accounts you do not want on GitHub, tell TES; TES masks them.

**LOG-COPY** (Terminal (Administrator), in the repo root; replace `<subfolder>`):

```powershell
Set-Location <your EagleEye repo folder>     # the folder that contains 02_Implementation
$dst = Join-Path "02_Implementation\docs\testing\US-003\evidence" "<subfolder>"
New-Item -ItemType Directory -Force $dst | Out-Null
Copy-Item "$env:ProgramData\EagleEye\logs\*.log" $dst -Force
Get-ChildItem $dst
```

**LOG-WATCH** (log monitor, Terminal (Administrator)):

```powershell
$log = Get-ChildItem "$env:ProgramData\EagleEye\logs\EagleEye.Service-*.log" | Sort-Object LastWriteTime | Select-Object -Last 1
$log.FullName
Get-Content $log.FullName -Tail 15 -Wait
```

After every service restart, reinstall, reboot or deletion of the log folder: press *Strg+C* in the log monitor and run LOG-WATCH again (a new file may have been started).

**LOG-FIND** (search all log files for an account, Terminal (Administrator)):

```powershell
Select-String -Path "$env:ProgramData\EagleEye\logs\*.log" -Pattern "<user name>" | Select-Object -Last 10 | ForEach-Object Line
```

Expected log lines (formats from the implementation report §6; the values are examples from this run's accounts). Wording differences are Notes. The Pass criterion for AC-14 is the content the AC names: **user name** and **new state yes/no**. Note: adding or deleting only **admin** accounts gives no "Account inventory changed" line and no new revision (implementation report D-3), because the list in the apps does not change.

```text
2026-10-08 19:42:07.123 +02:00 [INF] EagleEye.Service.UserAccounts.UserAccountService: Account ee-max (S-1-5-21-…-1002): under parental control = yes (set by parent device Papas PC, request 7f3c…, revision 13).
… [INF] …: Account inventory changed (revision 14): added [ee-neu], removed [], changed []; 7 standard accounts.
… [INF] …: Forgot the parental-control selection of 1 deleted account(s): S-1-5-21-….
… [INF] …: Account inventory loaded: 6 standard accounts, 2 under parental control.
```

### EXPECTED-LIST command

Shows which accounts Windows considers standard accounts, as a reference for the list in the app (Terminal (Administrator)):

```powershell
$adminSids = Get-LocalGroupMember -SID "S-1-5-32-544" | ForEach-Object { $_.SID.Value }
Get-LocalUser |
  Where-Object { $adminSids -notcontains $_.SID.Value -and $_.SID.Value -notmatch '-(500|501|503|504)$' -and $_.Name -ne 'defaultuser0' } |
  Sort-Object Name | Format-Table Name, FullName, Enabled -AutoSize
```

If `Get-LocalGroupMember` fails with "Failed to compare two elements…" (a known Windows problem with orphaned group members), use `net localgroup Administratoren` instead and compare by hand.

## 3. Setup Instructions

**Before Block A** (service PC, Admin). Block A tests the **upgrade** 0.2.0 → 0.3.1. So the start state must be: service **0.2.0** installed and running, and App A (**0.2.0**) installed in your Admin account and paired as **Papas PC** with `<host>`. At the last check (Orchestrator, 2026-10-07), **EagleEye service 0.3.0** was installed and running on the service PC from Michael's quick check, with a parent app 0.3.0 on **LEOSERV** paired to it. S-2 and S-3 build the start state from whatever you find. Open the Terminal (Administrator) of S-6 and note `<host>` (S-9) **first**; S-2 and S-3 need both.

1. **S-1 Installers present.** PowerShell in the repo root:
   ```powershell
   Get-Item 03_Delivery\windows\EagleEye-Setup-0.2.0.exe, 03_Delivery\windows\EagleEye-ParentApp-Setup-0.2.0.exe, 03_Delivery\windows\EagleEye-Setup-0.3.1.exe, 03_Delivery\windows\EagleEye-ParentApp-Setup-0.3.1.exe | Select-Object Name, LastWriteTime
   (Get-FileHash 03_Delivery\windows\EagleEye-Setup-0.3.1.exe).Hash -eq '6693DC9B362A60DD5FD7D30AA5E96E54ED50503EAD11B66DC757DF6C48434420'
   (Get-FileHash 03_Delivery\windows\EagleEye-ParentApp-Setup-0.3.1.exe).Hash -eq 'D6E5EFC5892825A0AD0C3AD9CAC04A1D520F56D04CD7F7B114DA52B5E8627918'
   ```
   All four files exist. The 0.3.1 files are dated **07.10.2026 12:59** (service) and **13:00** (parent app). Both hash lines print **True** (full SHA-256 values from `02_Implementation/docs/requirements/user-stories/US-003/issues/ISSUE-006.md`, Resolution). If a 0.3.1 file has another date or a hash line prints **False**, stop and tell TES; it may not be the build DEV handed over. Note the dates in the run header.
2. **S-2 Bring the service PC to 0.2.0.** Look at what is installed: *Einstellungen → Apps → Installierte Apps* → search "EagleEye"; Terminal (Administrator): `Get-Service -DisplayName "EagleEye Service" -ErrorAction SilentlyContinue`. Then follow the row that matches:

   | Found | Do |
   |---|---|
   | **EagleEye 0.3.0 or 0.3.1** (expected: service 0.3.0 from Michael's quick check) | (a) **Uninstall** it: *Installierte Apps* → **EagleEye** → *…* → *Deinstallieren*. If **EagleEye Parent App** 0.3.x is installed on the service PC, close it and uninstall it too. (b) Then continue exactly as in row **"Nothing"**, steps (1) to (4). Step (1) **must** delete `%ProgramData%\EagleEye` here (§8 Q-8, answered yes): its database already has the 0.3 schema, and 0.2.0 must start from a clean folder. **LEOSERV**: its 0.3.0 app loses its pairing with the deletion. Either remove the pairing there / uninstall it, or just leave it closed; it plays no role in this run. Keep it closed during the run, so that it does not appear as a third parent app. |
   | **Nothing** | (1) Terminal (Administrator): `Test-Path "$env:ProgramData\EagleEye"`. If **True**, delete the leftovers of earlier installations (uninstall keeps this folder; it holds only the old certificate and pairings of apps that no longer exist, §8 Q-8): `Remove-Item "$env:ProgramData\EagleEye" -Recurse -Force`. (2) Close every parent app; PowerShell: `Test-Path "$env:LOCALAPPDATA\EagleEye"` → if **True**: `Remove-Item "$env:LOCALAPPDATA\EagleEye" -Recurse -Force`. (3) Run `03_Delivery\windows\EagleEye-Setup-0.2.0.exe` with default settings. If no EagleEye tray icon appears in your Admin session: Start menu → **EagleEye Tray**. (4) Run `03_Delivery\windows\EagleEye-ParentApp-Setup-0.2.0.exe` with default settings. |
   | **EagleEye 0.2.0** and **EagleEye Parent App 0.2.0** | Keep both. Continue with S-3. |
   | Only one of the two, in **0.2.0** | Install the missing one from the 0.2.0 installer (row "Nothing", step 3 or 4; do **not** delete any folder). |
   | Any other version (not 0.2.0, 0.3.0 or 0.3.1) | **Stop** and tell TES. |

   Check afterwards: *Installierte Apps* shows **EagleEye 0.2.0** and **EagleEye Parent App 0.2.0**; `services.msc` → **EagleEye Service** *Wird ausgeführt*; Terminal (Administrator): `Test-Path "$env:ProgramData\EagleEye\logs"` → **False** (0.2.0 has no log folder; write it in the run file).
3. **S-3 Pair App A with 0.2.0.** Start **EagleEye Parent App**.
   - If it is already green **"Verbunden mit `<host>`"**, *Gekoppelt*, device **Papas PC**: done.
   - Otherwise: in the host dialog (or *Serververbindung*) enter `<host>` → *Verbinden*. Read the 6-digit code from the popup **"EagleEye – Eltern-App koppeln"** in your Admin session. If no popup appears: *Ereignisanzeige → Windows-Protokolle → Anwendung*, newest entry of source **EagleEye** (event ID 1000). Device name **Papas PC** → enter the code → *Koppeln*.
   - Expected: green **"Verbunden mit `<host>`"**, *Gekoppelt*, **Papas PC**. If pairing with 0.2.0 fails, stop and tell TES (that would be a US-002 regression, not part of this run).
   - Leave App A open. TC-003-01 and TC-003-02 need this paired 0.2.0 app.
4. **S-4 No development instances.** *Task-Manager → Details*: no `EagleEye.Service.exe` from a `02_Implementation` folder, no second `EagleEye.ParentApp.exe`.
5. **S-5 Admin display scaling = 150 %** (*Einstellungen → System → Bildschirm → Skalierung*). Note the value.
6. **S-6 Terminal (Administrator).** Start menu → **Terminal** → right-click → **Als Administrator ausführen** → UAC *Ja*. The title bar starts with **"Administrator:"**. A normal terminal in your Admin account is **not** enough: with UAC it has no administrator rights (US-002, ISSUE-005). Use it for every command marked *Terminal (Administrator)*. Service commands:
   ```powershell
   Stop-Service -DisplayName "EagleEye Service"
   Start-Service -DisplayName "EagleEye Service"
   Restart-Service -DisplayName "EagleEye Service"
   ```
7. **S-7 Log monitor.** A **second** Terminal (Administrator) (same way as S-6). It is used from TC-003-04 on (the log folder exists only after the update). Place it next to App A so you see both.
8. **S-8 Kid signed out.** *Task-Manager → Benutzer*: if `eagleeye-kid` is listed → *Abmelden*.
9. **S-9 Names.** `hostname` → `<host>` = ________.
10. **S-10 Existing local accounts.** Terminal (Administrator): `Get-LocalUser | Format-Table Name, FullName, Enabled, SID -AutoSize`. Note: (a) the full name of `eagleeye-kid` (may be empty); (b) **any other standard account** besides `eagleeye-kid` (e.g. a real family account; run the EXPECTED-LIST command, §2); (c) whether `defaultuser0` or `defaultuser1` already exists. TC-003-08 needs to know (b); S-12 needs (c).
11. **S-11 SmartScreen** on the unsigned installers: *Weitere Informationen* → *Trotzdem ausführen* (expected, not a failure).

**Before TC-003-10** (Block B, after TC-003-09):

12. **S-12 Create the test accounts.** Terminal (Administrator). If S-10 found `defaultuser0` or `defaultuser1` already, leave out its `New-LocalUser` line (the existing account serves the same purpose; write it in TC-003-11 Notes).
    ```powershell
    $pw = Read-Host -AsSecureString "Password for the EagleEye test accounts"
    $usersGroup = (Get-LocalGroup -SID "S-1-5-32-545").Name
    New-LocalUser -Name "ee-anna"      -FullName "anna Test" -Password $pw -PasswordNeverExpires
    New-LocalUser -Name "ee-max"       -FullName "Max Test"  -Password $pw -PasswordNeverExpires
    New-LocalUser -Name "ee-lena"                            -Password $pw -PasswordNeverExpires
    New-LocalUser -Name "ee-gesperrt"                        -Password $pw -PasswordNeverExpires
    New-LocalUser -Name "defaultuser0"                       -Password $pw -PasswordNeverExpires
    New-LocalUser -Name "defaultuser1"                       -Password $pw -PasswordNeverExpires
    New-LocalUser -Name "ee-admin"     -FullName "EE Admin"  -Password $pw -PasswordNeverExpires
    "ee-anna","ee-max","ee-lena","ee-gesperrt","defaultuser0","defaultuser1" | ForEach-Object { Add-LocalGroupMember -Group $usersGroup -Member $_ }
    Add-LocalGroupMember -SID "S-1-5-32-544" -Member "ee-admin"
    Disable-LocalUser -Name "ee-gesperrt"
    "done: $(Get-Date -Format HH:mm:ss)"
    ```
    The last line prints the time at which the script finished: start the stopwatch for TC-003-10 then. Use the same password for all; you never need to sign in with these accounts. (`ee-admin` exists for a few seconds as a standard account before it joins *Administratoren*. If it appears briefly in the app and then disappears, that is expected; it must be gone at the end of TC-003-10.)

**Before Block C**: none (state from Block B). **Before Block D**: none.

**Before Block E** (PC2):

13. **S-13 PC2.** Windows 11, same LAN and subnet as the service PC. Copy `EagleEye-ParentApp-Setup-0.3.1.exe` to PC2. The parent app is **not** installed on PC2 (it was uninstalled in US-002, TC-002-40); if it is, uninstall it first (*Installierte Apps*) and delete `%LOCALAPPDATA%\EagleEye` on PC2. Note PC2's display scaling and display language. Connect PC2 **by network cable** if possible (TC-003-23 unplugs it; §8 Q-9).
14. **S-14 Place both screens** so you can see App A and App B at the same time (needed for the 5-second checks). App A on the settings page, green.

## 4. Acceptance Criteria Coverage

| AC | Test case(s) |
|---|---|
| AC-1 | TC-003-10 (logged-on Kid and never-logged-on test accounts) |
| AC-2 | TC-003-08, TC-003-10, TC-003-27 (nested-group part not testable, §1) |
| AC-3 | TC-003-11 |
| AC-4 | TC-003-15 (needs a Microsoft account, §8 Q-1) |
| AC-5 | TC-003-25 |
| AC-6 | TC-003-02 |
| AC-7 | TC-003-31 |
| AC-8 | TC-003-19 |
| AC-9 | TC-003-08 |
| AC-10 | TC-003-13, TC-003-36 (table layout, ISSUE-006), TC-003-32 |
| AC-11 | TC-003-12, TC-003-32 |
| AC-12 | TC-003-14, TC-003-26 |
| AC-13 | TC-003-02, TC-003-20, TC-003-30, TC-003-32 |
| AC-14 | TC-003-16, TC-003-17 (save and log entry); TC-003-03, TC-003-04, TC-003-05, TC-003-06 (admin-only log folder); TC-003-07 *(supporting)* |
| AC-15 | TC-003-18 (app restart), TC-003-20 (service restart), TC-003-21 (reboot), TC-003-22 (re-install); TC-003-01, TC-003-02 *(supporting: update 0.2.0 → 0.3.1 keeps the pairing)* |
| AC-16 | TC-003-23 |
| AC-17 | TC-003-19, TC-003-31 |
| AC-18 | TC-003-02 (first inventory after the update), TC-003-10, TC-003-24, TC-003-29 |
| AC-19 | TC-003-10 (add), TC-003-24 (add), TC-003-25 (rename), TC-003-29 (delete), TC-003-30 (app not connected at the change), TC-003-34 (pushed to two apps) |
| AC-20 | TC-003-08, TC-003-27 |
| AC-21 | TC-003-09 (never ticked → unticked), TC-003-28 (ticked before → ticked again) |
| AC-22 | TC-003-29 |
| AC-23 | TC-003-33 |
| AC-24 | TC-003-35 |

All 24 ACs have at least one case. AC-7, AC-23 and AC-24 get their result only in Block E (PC2). AC-4 depends on §8 Q-1.

## 5. Test Cases

### Block A — Update to 0.3.1 and the admin-only log folder

*Service PC · Admin, one visit to the Kid account. Start state: setup S-1 to S-11 done, App A (0.2.0) green.*

#### TC-003-01: Service update 0.2.0 → 0.3.1 *(supporting)*

- **Verifies**: — (supporting: prerequisite for AC-14 and AC-15; the update keeps the pairing)
- **Machine / account**: Service PC / Admin (+ Terminal (Administrator))
- **Precondition**: EagleEye 0.2.0 installed, service running, App A (0.2.0) open and green

**Steps**

1. Run `03_Delivery\windows\EagleEye-Setup-0.3.1.exe` with default settings and finish the wizard. Leave App A open.
2. If no EagleEye tray icon is visible in your Admin session: Start menu → **EagleEye Tray**. Tray icon → *App Infos*.
3. *Installierte Apps* → search "EagleEye".
4. `services.msc` → **EagleEye Service**.
5. Terminal (Administrator): `Test-Path "$env:ProgramData\EagleEye\logs"` and `Test-Path "$env:ProgramData\EagleEye\EagleEye.Service.db"`
6. Look at App A (still 0.2.0) for up to 60 s.

**Expected result**

- The wizard finishes without an error and does not ask about firewall, port or certificate.
- *App Infos* shows **EagleEye_v0.3**.
- *Installierte Apps*: **EagleEye** listed **once**, version **0.3.1**.
- **EagleEye Service**: *Wird ausgeführt*.
- Both `Test-Path`: **True** (log folder created; database kept).
- App A turns green **"Verbunden mit `<host>`"** again within 60 s without a pairing code (the service kept the pairing).

#### TC-003-02: Parent app update keeps the pairing and shows the new section with all accounts unticked

- **Verifies**: AC-6, AC-13, AC-18 (first inventory after the update); supporting AC-15 (pairing kept)
- **Machine / account**: Service PC / Admin
- **Precondition**: TC-003-01 done; App A open, green

**Steps**

1. With App A still open, run `03_Delivery\windows\EagleEye-ParentApp-Setup-0.3.1.exe` with default settings (note how it handles the running app). Start the app (last wizard page or Start menu).
2. *Installierte Apps* → "EagleEye Parent App".
3. Look at the navigation menu and the settings page from top to bottom (scroll if needed).

**Expected result**

- The installer finishes without errors; *Installierte Apps*: **EagleEye Parent App 0.3.1**.
- App A connects **without a pairing code**: green "Verbunden mit `<host>`", *Gekoppelt*, device **Papas PC**.
- The menu still has only **Einstellungen**.
- The settings page has three sections in this order: **Darstellung**, **Serververbindung**, **Benutzerkonten auf dem EagleEye-PC**.
- The new section shows (after at most a short "Wird geladen …") one row per standard account found in S-10 (at least `eagleeye-kid`), as a table with the column headers **Konto** and **Unter Elternkontrolle** (one checkbox per row in the second column), and **no** checkbox is ticked.
- At 150 %, nothing in the section is cut off.
- Above the rows the instruction **"Markieren Sie die Konten, die unter Elternkontrolle stehen."** (wording differences are Notes). The table layout itself is checked in detail in TC-003-36 (ISSUE-006).

#### TC-003-03: The log folder is restricted to SYSTEM and Administratoren

- **Verifies**: AC-14 (admin-only log folder)
- **Machine / account**: Service PC / Admin, **Terminal (Administrator)**
- **Precondition**: TC-003-01 done

**Steps**

0. Check that the terminal's title bar starts with **"Administrator:"**. In a non-elevated terminal step 2 ends with *Zugriff verweigert*; that is Windows UAC, not a product error.
1. `icacls "$env:ProgramData\EagleEye\logs"`
2. `Get-ChildItem "$env:ProgramData\EagleEye\logs"`
3. `icacls "$env:ProgramData\EagleEye"` (for comparison)

**Expected result**

- Step 1: exactly two entries, `NT-AUTORITÄT\SYSTEM:(OI)(CI)(F)` and `VORDEFINIERT\Administratoren:(OI)(CI)(F)`. **No** *Benutzer*, *Authentifizierte Benutzer* or *Jeder* entry. No entry carries `(I)` (nothing inherited).
- Step 2: at least one file `EagleEye.Service-001.log` (the number may be higher).
- Step 3: unchanged from US-002: SYSTEM (F), Administratoren (F), Benutzer (RX).
- If step 2 lists **no** log file: see "If there is no log file" in §2 (Event Log warning). That is a **Fail** of this case.

#### TC-003-04: The service writes its log file, readable for the administrator *(supporting)*

- **Verifies**: — (supporting AC-14: the log file exists and is usable as evidence)
- **Machine / account**: Service PC / Admin, log monitor (S-7)
- **Precondition**: TC-003-03 done

**Steps**

1. In the log monitor run **LOG-WATCH** (§2). Note the file name it prints.
2. Look at the lines shown.
3. Open the same file in Notepad started as administrator (Start → *Editor* → right-click → *Als Administrator ausführen* → *Datei → Öffnen*), without stopping the service. Close Notepad without saving.

**Expected result**

- Lines in the format `yyyy-MM-dd HH:mm:ss.fff +02:00 [INF] Category: message`, e.g. `… [INF] EagleEye.Service.UserAccounts.UserAccountService: …`.
- A line from this service start: **"Account inventory loaded: N standard accounts, 0 under parental control."** (N = number of standard accounts from S-10, normally 1; the text says "accounts" also for 1).
- Notepad opens the file while the service runs.
- No line contains a pairing code, a password or a long random string that looks like a token.

> **Switch to the Kid account** (*Benutzer wechseln* → `eagleeye-kid`; do not sign Admin out).

#### TC-003-05: The kid cannot read the log folder, but the rest of the data folder as before

- **Verifies**: AC-14 (logs folder readable by administrators only)
- **Machine / account**: Service PC / Kid
- **Precondition**: signed in as `eagleeye-kid`

**Steps**

1. Explorer → address bar `C:\ProgramData\EagleEye\logs` → Enter. (Do **not** click *Fortsetzen*; it would ask for admin credentials.)
2. PowerShell (normal): `Get-ChildItem C:\ProgramData\EagleEye\logs`
3. PowerShell: `Get-Content C:\ProgramData\EagleEye\logs\EagleEye.Service-001.log -TotalCount 3` (use the file name from TC-003-04)
4. PowerShell: `Get-ChildItem C:\ProgramData\EagleEye`

**Expected result**

- Step 1: Explorer refuses access (*"Sie verfügen momentan nicht über die Berechtigung…"*).
- Steps 2 and 3: **Zugriff verweigert**; no file name and no log line is shown.
- Step 4: the folder content is listed as before (at least `logs`, `certs`, the database file); no error.

> **Switch back to Admin.** Leave the Kid **signed in** in the background (TC-003-10 checks that a logged-on account is listed).

#### TC-003-06: A deleted log folder comes back with the same protection

- **Verifies**: AC-14 (admin-only log folder, robustness)
- **Machine / account**: Service PC / Admin, Terminal (Administrator)
- **Precondition**: TC-003-05 done

**Steps**

1. **First save the current logs**: LOG-COPY with `<subfolder>` = `run-01-service-logs\before-TC-003-06`.
2. In the log monitor press *Strg+C*.
3. Terminal (Administrator):
   ```powershell
   Stop-Service -DisplayName "EagleEye Service"
   Remove-Item "$env:ProgramData\EagleEye\logs" -Recurse -Force
   Test-Path "$env:ProgramData\EagleEye\logs"
   Start-Service -DisplayName "EagleEye Service"
   icacls "$env:ProgramData\EagleEye\logs"
   Get-ChildItem "$env:ProgramData\EagleEye\logs"
   ```
4. Run LOG-WATCH again in the log monitor.

**Expected result**

- `Test-Path` after the deletion: **False**.
- After the start: `icacls` shows the same two entries as TC-003-03 (SYSTEM and Administratoren, F, no `(I)`, no *Benutzer*).
- A new log file (`EagleEye.Service-001.log`) exists, and the log monitor shows a new "Account inventory loaded …" line.
- App A goes red while the service is stopped and green again within 60 s (not the subject of this case; write it in Notes if not).

#### TC-003-07: A loosened log folder is repaired at the next service start *(supporting)*

- **Verifies**: — (supporting AC-14; implementation plan: the service applies the ACL on every start)
- **Machine / account**: Service PC / Admin, Terminal (Administrator)
- **Precondition**: TC-003-06 done

**Steps**

1. Give standard users read access by hand, as an attacker with admin rights or a careless tool might:
   ```powershell
   icacls "$env:ProgramData\EagleEye\logs" /grant "*S-1-5-32-545:(OI)(CI)RX"
   icacls "$env:ProgramData\EagleEye\logs"
   ```
2. `Restart-Service -DisplayName "EagleEye Service"`, then `icacls "$env:ProgramData\EagleEye\logs"`.
3. In the log monitor: *Strg+C*, LOG-WATCH again.

**Expected result**

- Step 1: the output now also lists `VORDEFINIERT\Benutzer:(OI)(CI)(RX)` (the grant worked).
- Step 2: the *Benutzer* entry is **gone**; only SYSTEM and Administratoren remain, as in TC-003-03.
- If the *Benutzer* entry is still there after step 2: mark **Fail** and remove it by hand (`icacls "$env:ProgramData\EagleEye\logs" /remove:g "*S-1-5-32-545"`) so the folder is protected again.

### Block B — Inventory content and display

*Service PC · Admin. Start state: end of Block A (App A 0.3.1 green, Kid signed in in the background, log monitor running). Keep App A on the settings page with the account section visible.*

#### TC-003-08: With no standard account, the section says so

- **Verifies**: AC-9, AC-20 (standard → admin disappears), AC-2
- **Machine / account**: Service PC / Admin, Terminal (Administrator)
- **Precondition**: the test accounts of S-12 do **not** exist yet. The only standard accounts are those from S-10 (b), normally just `eagleeye-kid`. If there are others (e.g. a real family account), see §8 Q-2 before this case.

**Steps**

1. Make every standard account from S-10 (b) an administrator for a moment (here: `eagleeye-kid`):
   ```powershell
   Add-LocalGroupMember -SID "S-1-5-32-544" -Member "eagleeye-kid"
   ```
   Start the stopwatch when the command returns.
2. Watch App A. Stop the stopwatch when the section changes.
3. Run the EXPECTED-LIST command (§2): it returns no account.

**Expected result**

- Within **60 s**: the section shows **"Keine Nicht-Administrator-Konten vorhanden"** and no rows, no table header (note the seconds).
- The log monitor shows an "Account inventory changed … removed [eagleeye-kid] … 0 standard accounts." line.

#### TC-003-09: An account that becomes standard again and was never ticked appears unticked

- **Verifies**: AC-21 (not ticked before → not ticked), AC-19 (timing)
- **Machine / account**: Service PC / Admin, Terminal (Administrator)
- **Precondition**: TC-003-08 done

**Steps**

1. Undo step 1 of TC-003-08:
   ```powershell
   Remove-LocalGroupMember -SID "S-1-5-32-544" -Member "eagleeye-kid"
   ```
   Start the stopwatch when the command returns.
2. Watch App A; stop the stopwatch when the row appears.

**Expected result**

- Within **60 s** the row for `eagleeye-kid` is back, **not ticked** (note the seconds).
- "Keine Nicht-Administrator-Konten vorhanden" is gone.

> **Setup S-12 now**: create the test accounts (§3). Start the stopwatch for TC-003-10 when the script prints "done".

#### TC-003-10: New standard accounts appear unticked; administrators do not

- **Verifies**: AC-1, AC-2, AC-18, AC-19 (add)
- **Machine / account**: Service PC / Admin (Kid signed in in the background)
- **Precondition**: S-12 just finished, stopwatch running

**Steps**

1. Watch App A; stop the stopwatch when the new rows have appeared.
2. Compare the rows with the output of the EXPECTED-LIST command (§2).

**Expected result**

- Within **60 s** after the script finished, these rows are shown (plus any other standard account from S-10): `eagleeye-kid` (signed in) and `ee-anna`, `ee-max`, `ee-lena`, `ee-gesperrt`, `defaultuser1` (never signed in) — the same set as the EXPECTED-LIST output.
- **None** of the new rows is ticked.
- **Not** shown: `ee-admin`, your own Admin account, *Administrator*.
- The log monitor shows one or more "Account inventory changed (revision N): added [...], removed [], changed []; M standard accounts." lines (the script may span two 15-s checks). Together they name the new standard accounts. `defaultuser0` is **never** named. `ee-admin` is named only if a check caught it in the few seconds before it became an administrator; then a later line names it in *removed*. Adding an account that is already an administrator gives no line at all (implementation report D-3).

#### TC-003-11: Built-in accounts and `defaultuser0` are hidden, `defaultuser1` is not

- **Verifies**: AC-3
- **Machine / account**: Service PC / Admin, Terminal (Administrator)
- **Precondition**: TC-003-10 done

**Steps**

1. `Get-LocalUser | Format-Table Name, Enabled, SID -AutoSize` — find *Gast*, *DefaultAccount*, *WDAGUtilityAccount* (SIDs ending in -501, -503, -504), `defaultuser0`, `defaultuser1`.
2. Look at the list in App A.
3. LOG-FIND with `<user name>` = `defaultuser0`.

**Expected result**

- Step 1: the built-in accounts exist (normally disabled); `defaultuser0` and `defaultuser1` are enabled standard accounts.
- App A shows **no** row for *Gast*, *DefaultAccount*, *WDAGUtilityAccount* or **`defaultuser0`**, although they are not administrators (and although disabled accounts are listed, TC-003-14).
- App A **does** show a row **`defaultuser1`** (Q-7: only exactly `defaultuser0` is excluded).
- Step 3: no log line names `defaultuser0`.

#### TC-003-12: Rows show "full name (user name)", or the user name alone

- **Verifies**: AC-11
- **Machine / account**: Service PC / Admin
- **Precondition**: TC-003-10 done

**Steps**

1. Read the text of each row.

**Expected result**

- `anna Test (ee-anna)` and `Max Test (ee-max)` (full name, then the user name in brackets).
- `ee-lena` and `defaultuser1` (no full name → user name only, no empty brackets).
- `eagleeye-kid` follows the same rule, according to its full name from S-10.

#### TC-003-13: Rows are sorted alphabetically, ignoring upper and lower case

- **Verifies**: AC-10
- **Machine / account**: Service PC / Admin
- **Precondition**: TC-003-10 done

**Steps**

1. Read the order of the rows from top to bottom. Look at the checkbox in the column **Unter Elternkontrolle** of each row.

**Expected result**

- Order (the `eagleeye-kid` row at its alphabetical place by its shown name):
  1. `anna Test (ee-anna)`
  2. `defaultuser1`
  3. `eagleeye-kid` (if it has no full name)
  4. `ee-gesperrt (deaktiviert)`
  5. `ee-lena`
  6. `Max Test (ee-max)`
- In particular `anna Test` comes **before** `Max Test` (a case-sensitive sort would put "Max" first).
- Each row has one checkbox in the column **Unter Elternkontrolle**, showing the stored state (all unticked at this point).
- At 150 %, no row text or label is cut off.

#### TC-003-36: The account list is a two-column table with headers (ISSUE-006)

- **Verifies**: ISSUE-006 fix (patch 0.3.1); AC-10 (checkbox "Unter Elternkontrolle" per account, now as column header)
- **Machine / account**: Service PC / Admin (150 % scaling, S-5)
- **Precondition**: TC-003-13 done (6 or more rows, all unticked). Executed here, next to the other display cases; the number 36 was added with patch 0.3.1.

**Steps**

1. Look at the account section in the current theme. Take a screenshot (*Win+Umschalt+S*) of the whole section → `evidence/ISSUE-006-after-fix.png`.
2. *Darstellung*: switch to the other mode (**Hell** ↔ **Dunkel**). Look at the section again. Take a second screenshot → `evidence/ISSUE-006-after-fix-2.png`. Switch back.
3. The empty states are checked where they occur: TC-003-08 ("Keine Nicht-Administrator-Konten vorhanden") and TC-003-19 ("Keine Daten verfügbar") each expect **no** table header. TES counts those results for ISSUE-006 too. If you see "Wird geladen …" in TC-003-20, look whether a header is shown with it.

**Expected result**

- A table with two columns. Above the rows a header row with the column titles **Konto** and **Unter Elternkontrolle**, in **bold**, visibly set apart as headers.
- Column 1: the account name as before (e.g. `anna Test (ee-anna)`, `ee-gesperrt (deaktiviert)`). Column 2: the checkbox, aligned **under its header** in every row.
- **No** row repeats the text "Unter Elternkontrolle"; there is no label to the far right of the rows.
- Unchanged above the table: section title **Benutzerkonten auf dem EagleEye-PC** and the instruction "Markieren Sie die Konten, die unter Elternkontrolle stehen."
- In **both** light and dark mode, headers, names and checkboxes are clearly readable (enough contrast), and at 150 % nothing is cut off. Long names may wrap inside column 1; that is fine.
- Step 3: the header row is shown **only** when rows are listed, never together with "Keine Nicht-Administrator-Konten vorhanden", "Keine Daten verfügbar" or "Wird geladen …" (results of TC-003-08, TC-003-19 and, if seen, TC-003-20).
- On Pass, TES sets `02_Implementation/docs/requirements/user-stories/US-003/issues/ISSUE-006.md` to `Verified/Closed` after the evaluation. On Fail, the issue stays open (write what is wrong in Observed).

#### TC-003-14: A disabled account is listed with "(deaktiviert)" and can be ticked

- **Verifies**: AC-12
- **Machine / account**: Service PC / Admin
- **Precondition**: TC-003-10 done; log monitor running

**Steps**

1. Find the row of `ee-gesperrt`.
2. Tick **Unter Elternkontrolle** in that row.

**Expected result**

- The row reads **`ee-gesperrt (deaktiviert)`**.
- The checkbox can be ticked and stays ticked; within 5 s the log monitor shows a line naming `ee-gesperrt` with **yes**.

#### TC-003-15: An account linked to a Microsoft account is listed

- **Verifies**: AC-4
- **Machine / account**: Service PC / Admin
- **Precondition**: a Microsoft account that may be added to this PC as a standard user (§8 Q-1). If none is available, mark **Blocked** ("no Microsoft account available") and continue.

**Steps**

1. *Einstellungen → Konten → Andere Benutzer* (older builds: *Familie und andere Benutzer*) → *Konto hinzufügen* → enter the e-mail address of the Microsoft account → finish. The new account is a standard user by default (check: *Kontotyp* = *Standardbenutzer*).
2. If no row appears within 60 s: sign in once with that account (*Benutzer wechseln*), sign out again, switch back to Admin. Write in Notes whether step 2 was needed.
3. Terminal (Administrator): `Get-LocalUser | Where-Object PrincipalSource -eq MicrosoftAccount | Format-Table Name, FullName`
4. Look at App A.

**Expected result**

- Step 3 lists the account (local user name, often shortened, e.g. `micha`).
- App A shows a row for it in the form `<full name> (<user name>)` (or the user name alone if Windows has no full name), not ticked.
- **Cleanup** (§7 step 3): remove the account after the run, unless you want to keep it.

### Block C — Selecting, saving, keeping

*Service PC · Admin. Start state: end of Block B. Ticked so far: `ee-gesperrt` (TC-003-14). Log monitor running.*

#### TC-003-16: Ticking saves at once and writes a log entry

- **Verifies**: AC-14
- **Machine / account**: Service PC / Admin, log monitor
- **Precondition**: App A green, section shows the list

**Steps**

1. Look for a *Speichern* (save) button anywhere on the settings page.
2. Tick **Unter Elternkontrolle** for `Max Test (ee-max)`; start the stopwatch at the click.
3. Watch the row and the log monitor.
4. Tick `anna Test (ee-anna)` and `ee-lena` as well (needed for later cases).

**Expected result**

- There is **no** save button.
- Within **5 s** the log monitor shows a line naming **`ee-max`** with **yes** (e.g. "Account ee-max (S-1-5-21-…): under parental control = yes (set by parent device Papas PC, …)").
- The checkbox stays ticked (the row may be greyed out for a moment while saving). No error text.
- Step 4: two more log lines, `ee-anna` yes and `ee-lena` yes.
- *Note only*: whether the same entry appears in the Event Log (*Anwendung*, source **EagleEye**).

#### TC-003-17: Unticking saves at once and writes a log entry

- **Verifies**: AC-14
- **Machine / account**: Service PC / Admin, log monitor
- **Precondition**: `ee-gesperrt` ticked (TC-003-14)

**Steps**

1. Untick `ee-gesperrt (deaktiviert)`; stopwatch at the click.
2. Write down the resulting **tick record** (used until the end of Block D): ticked = `ee-anna`, `ee-lena`, `ee-max`; unticked = `defaultuser1`, `eagleeye-kid`, `ee-gesperrt`.

**Expected result**

- Within **5 s** a log line naming **`ee-gesperrt`** with **no**.
- The checkbox stays unticked; no error text.

#### TC-003-18: The ticks survive closing and restarting the parent app

- **Verifies**: AC-15 (app restart)
- **Machine / account**: Service PC / Admin
- **Precondition**: tick record from TC-003-17

**Steps**

1. Close App A (window **X**). *Task-Manager*: no `EagleEye.ParentApp.exe`.
2. Start App A from the Start menu. Wait for green.

**Expected result**

- The section shows the same rows and exactly the ticks of the tick record.

#### TC-003-19: While the service is stopped, there is nothing to tick

- **Verifies**: AC-8, AC-17
- **Machine / account**: Service PC / Admin, Terminal (Administrator)
- **Precondition**: App A green

**Steps**

1. `Stop-Service -DisplayName "EagleEye Service"`; stopwatch.
2. Watch App A until the status bar is red.
3. Look at the account section; try to find any checkbox.

**Expected result**

- Status bar **red** "Nicht verbunden mit `<host>`" within 30 s.
- The section shows **"Keine Daten verfügbar"** and **no** rows, no checkboxes and no table header (the old list is not shown read-only).

#### TC-003-20: After the service starts again, the list comes back with the same ticks

- **Verifies**: AC-13, AC-15 (service restart)
- **Machine / account**: Service PC / Admin, Terminal (Administrator)
- **Precondition**: TC-003-19 (service stopped, app red)

**Steps**

1. `Start-Service -DisplayName "EagleEye Service"`; stopwatch. Do not touch App A.
2. Watch the account section closely while the app reconnects.
3. Log monitor: *Strg+C*, LOG-WATCH.

**Expected result**

- Green within **60 s**, without any action.
- The section then shows the list with exactly the ticks of the tick record, without any action.
- *Note only*: whether **"Wird geladen …"** was visible between "Keine Daten verfügbar" and the list (it may be too short to see; see §8 Q-4).
- The log shows "Account inventory loaded: … 3 under parental control."

#### TC-003-21: The ticks survive a reboot of the service PC

- **Verifies**: AC-15 (reboot)
- **Machine / account**: Service PC / Admin
- **Precondition**: tick record unchanged. Before the reboot: save open work; the Kid session is ended by the reboot (expected).

**Steps**

1. Start → *Ein/Aus* → *Neu starten*.
2. Sign in as Admin. Start App A (if it does not start by itself). Wait for green.
3. Open the log monitor again (S-7, LOG-WATCH) and the Terminal (Administrator) (S-6).

**Expected result**

- App A green without a pairing code; the section shows exactly the ticks of the tick record.

#### TC-003-22: The ticks survive a re-install of the service

- **Verifies**: AC-15 (update / re-install)
- **Machine / account**: Service PC / Admin
- **Precondition**: App A green

**Steps**

1. Run `03_Delivery\windows\EagleEye-Setup-0.3.1.exe` again with default settings (same version over the installed one). Leave App A open.
2. Wait until App A is green again (up to 60 s after the wizard finished). Log monitor: *Strg+C*, LOG-WATCH.

**Expected result**

- The wizard finishes without errors.
- App A green without a pairing code; the section shows exactly the ticks of the tick record.
- `icacls "$env:ProgramData\EagleEye\logs"` (Terminal (Administrator)) still shows only SYSTEM and Administratoren.

#### TC-003-23 — moved to Block E

TC-003-23 (AC-16) needs PC2 and runs in Block E, after TC-003-33 (see §2 "Test blocks").

### Block D — Account changes while the service runs

*Service PC · Admin, Terminal (Administrator), log monitor. Start state: end of Block C, tick record of TC-003-17. App A on the settings page; do not touch it while timing.*

#### TC-003-24: A new standard account appears unticked within 60 s

- **Verifies**: AC-19 (add), AC-18
- **Machine / account**: Service PC / Admin, Terminal (Administrator)
- **Precondition**: App A green

**Steps**

1. `New-LocalUser -Name "ee-neu" -NoPassword; Add-LocalGroupMember -Group (Get-LocalGroup -SID "S-1-5-32-545").Name -Member "ee-neu"`; stopwatch when the command returns.
2. Watch App A.

**Expected result**

- Within **60 s** a row **`ee-neu`**, **not ticked**, at its alphabetical place (between `ee-lena` and `Max Test (ee-max)`). Note the seconds.
- The other rows keep their ticks.

#### TC-003-25: A renamed account keeps its tick and shows the new name

- **Verifies**: AC-5, AC-19 (rename)
- **Machine / account**: Service PC / Admin, `lusrmgr.msc`
- **Precondition**: `ee-anna` ticked

**Steps**

1. `lusrmgr.msc` → *Benutzer* → right-click **ee-anna** → *Umbenennen* → `ee-annika` → Enter. Stopwatch.
2. Watch App A.

**Expected result**

- Within **60 s** the row reads **`anna Test (ee-annika)`** and is still **ticked**. There is no separate row for `ee-anna` any more.
- Log monitor: "Account inventory changed … changed [ee-annika] …".

#### TC-003-26: Disabling and enabling an account updates its note and keeps its tick

- **Verifies**: AC-12 (disabled note follows the account; plan D-6)
- **Machine / account**: Service PC / Admin, `lusrmgr.msc`
- **Precondition**: TC-003-25 (`ee-annika` ticked)

**Steps**

1. `lusrmgr.msc` → *Benutzer* → double-click **ee-annika** → tick **Konto ist deaktiviert** → *OK*. Stopwatch.
2. Watch App A until the row changes.
3. Untick **Konto ist deaktiviert** again → *OK*. Stopwatch. Watch App A.

**Expected result**

- Step 2: the row reads **`anna Test (ee-annika) (deaktiviert)`**, still **ticked**.
- Step 3: the note disappears, still **ticked**.
- The story gives no time limit for this change (it is not an add, delete or rename). Expected within 60 s like the others; slower is a **Note**, not a Fail. Note both times.

#### TC-003-27: A ticked account that becomes an administrator disappears

- **Verifies**: AC-20, AC-2
- **Machine / account**: Service PC / Admin, *Einstellungen*
- **Precondition**: `ee-max` ticked

**Steps**

1. *Einstellungen → Konten → Andere Benutzer* → **Max Test** / **ee-max** → *Kontotyp ändern* → **Administrator** → *OK*. Stopwatch at *OK*.
2. Watch App A.

**Expected result**

- Within **60 s** the row `Max Test (ee-max)` disappears from the list. Note the seconds.
- The other rows and their ticks are unchanged.
- Log monitor: "Account inventory changed … removed [ee-max] …".

#### TC-003-28: The account that becomes standard again is ticked again

- **Verifies**: AC-21 (ticked before → ticked again)
- **Machine / account**: Service PC / Admin, *Einstellungen*
- **Precondition**: TC-003-27 done

**Steps**

1. Same dialog → *Kontotyp ändern* → **Standardbenutzer** → *OK*. Stopwatch at *OK*.
2. Watch App A.

**Expected result**

- Within **60 s** the row **`Max Test (ee-max)`** is back and **ticked**.

#### TC-003-29: A deleted account is forgotten; a new account with the same name is unticked

- **Verifies**: AC-19 (delete), AC-22, AC-18
- **Machine / account**: Service PC / Admin, Terminal (Administrator)
- **Precondition**: `ee-lena` ticked

**Steps**

1. `Remove-LocalUser -Name "ee-lena"`; stopwatch. Watch App A until the row is gone.
2. LOG-FIND with `Forgot` as the pattern.
3. Create it again with the same name:
   ```powershell
   New-LocalUser -Name "ee-lena" -NoPassword; Add-LocalGroupMember -Group (Get-LocalGroup -SID "S-1-5-32-545").Name -Member "ee-lena"
   ```
   Stopwatch. Watch App A.

**Expected result**

- Step 1: within **60 s** the row `ee-lena` disappears.
- Step 2: a line "Forgot the parental-control selection of 1 deleted account(s): S-1-5-21-…".
- Step 3: within **60 s** a row **`ee-lena`** appears again, **not ticked** (a different account, new SID).

#### TC-003-30: A parent app that was closed during a change shows the current list when it starts

- **Verifies**: AC-19 (app not connected at the time of the change), AC-13
- **Machine / account**: Service PC / Admin, Terminal (Administrator)
- **Precondition**: `ee-neu` exists (TC-003-24)

**Steps**

1. Close App A (**X**); *Task-Manager*: no `EagleEye.ParentApp.exe`.
2. `Remove-LocalUser -Name "ee-neu"`. Wait **30 s** (so the service has noticed the change before the app connects).
3. Start App A; wait for green.

**Expected result**

- As soon as App A is connected, the list shows **no** `ee-neu` row; all other rows and ticks are as before (ticked: `anna Test (ee-annika)`, `Max Test (ee-max)`; unticked: the rest).

### Block E — Two parent apps (PC2), error case

*PC2 + service PC · Admin. Setup S-13, S-14 done. App A green on the settings page. Log monitor running. Order: TC-003-31, -32, -33, **-23**, -34, -35.*

#### TC-003-31: An unpaired app shows no accounts

- **Verifies**: AC-7, AC-17
- **Machine / account**: PC2 / any local account
- **Precondition**: parent app not installed on PC2

**Steps**

1. On PC2 install `EagleEye-ParentApp-Setup-0.3.1.exe` with default settings and start the app.
2. In the host dialog click *Abbrechen*. Look at the settings page.
3. In *Serververbindung* enter `<host>` → *Verbinden* (pairing form appears). Look at the account section again. Do not pair yet.

**Expected result**

- Step 2: three sections; *Benutzerkonten auf dem EagleEye-PC* shows **"Keine Daten verfügbar"**, no rows, no checkboxes.
- Step 3 (connected, pairing not finished): still **"Keine Daten verfügbar"**, no rows.
- *Note only*: if PC2 runs Windows in English, the English texts ("User accounts on the EagleEye PC", "No data available").

#### TC-003-32: After pairing, App B shows the same list as App A

- **Verifies**: AC-13, AC-10, AC-11
- **Machine / account**: PC2 + service PC / Admin
- **Precondition**: TC-003-31, pairing form open on PC2

**Steps**

1. On the service PC read the pairing code from the tray popup in your Admin session (or the Kid's). On PC2: device name **PC2**, enter the code → *Koppeln*.
2. Compare the account section of App B with App A row by row.

**Expected result**

- App B green "Verbunden mit `<host>`", *Gekoppelt*, device **PC2**.
- App B's section shows the same rows in the same order with the same names and the same ticks as App A.
- Nothing is cut off at PC2's display scaling (note it).

#### TC-003-33: A tick in one app appears in the other within 5 s

- **Verifies**: AC-23
- **Machine / account**: PC2 + service PC / Admin
- **Precondition**: both apps green, both on the settings page, both screens visible (S-14)

**Steps**

1. In **App A** tick `defaultuser1`. Stopwatch at the click. Watch App B; stop when its `defaultuser1` row is ticked.
2. In **App B** untick `defaultuser1`. Stopwatch. Watch App A.
3. In App B tick `ee-gesperrt`, then in App A untick it (one after the other, wait until both agree in between).

**Expected result**

- Each change appears in the other app within **5 s**, without any action there (note the times; expected about 1 s).
- The app where you clicked keeps the new state; no error text.
- Each change gives one log line, naming the device where it was made (*Papas PC* or *PC2*).
- End state: `defaultuser1` and `ee-gesperrt` unticked in both apps.

#### TC-003-23: A change that cannot be saved shows an error and the checkbox returns

- **Verifies**: AC-16
- **Machine / account**: PC2 + service PC / Admin, log monitor
- **Precondition**: TC-003-33 done; both apps green on the settings page; `Max Test (ee-max)` **ticked** in both apps. PC2 should be connected **by network cable** for this case (see the note on Wi-Fi below).

How the failure is provoked: PC2 loses its network. App B does not notice this at once (implementation report §5.4 and §6), so a click still sends the change, but it never reaches the service. App B gets no confirmation and gives up after at most 4 s (implementation plan D-5). Stopping the service does not work for this: on the service PC the app notices a stopped service at once and shows "Keine Daten verfügbar" (TC-003-19).

**Steps**

1. Unplug PC2's network cable. **Immediately** (within 5 s) untick `Max Test (ee-max)` in App B; start the stopwatch at the click.
2. Watch App B for 10 s. Stop the stopwatch when the error text appears.
3. Look at App A and at the log monitor.
4. Wait about 30 s (App B may switch to red / "Keine Daten verfügbar" in this time; note when). Plug the cable back in. Wait until App B is green again (up to 60 s).
5. LOG-FIND with `<pattern>` = `ee-max`.

**Expected result**

- Within **5 s** after the click: below the list in App B **"Die Änderung konnte nicht gespeichert werden. Bitte erneut versuchen."**, and the `Max Test (ee-max)` checkbox is **ticked again** (the stored state). The row may be greyed out until then.
- App A: `Max Test (ee-max)` stays **ticked** the whole time; no new `ee-max` log line from *PC2*.
- After App B is green again: its list shows `Max Test (ee-max)` **ticked** (same as App A and the last `ee-max` log line); the error text is gone.
- *Note only*: after how many seconds App B went red / "Keine Daten verfügbar", if it did.
- **If App B shows "Keine Daten verfügbar" before you could click** (Windows reported the lost network at once, which can happen with Wi-Fi): the failure was not provoked. Plug the cable back in, wait for green, and use the alternative below. Write the method in Notes.

*Alternative (service PC, not tried by DEV)*: pause the service process for about 20 s. Terminal (Administrator) → `resmon` → tab **CPU** → right-click `EagleEye.Service.exe` → **Prozess anhalten** → within 10 s untick `Max Test (ee-max)` in App A → expected as above within 5 s → **Prozess fortsetzen** at the latest 25 s after pausing. After the service continues, it may still store the queued untick; then both apps show `ee-max` unticked within a few seconds. That is a Pass, as long as both apps end up equal to the last `ee-max` log line. Afterwards tick `ee-max` again in either app, so both show it ticked.

#### TC-003-34: An account change on the service PC appears in both apps

- **Verifies**: AC-19 (pushed to all connected parent apps)
- **Machine / account**: PC2 + service PC / Admin, Terminal (Administrator)
- **Precondition**: both apps green

**Steps**

1. `New-LocalUser -Name "ee-zwei" -NoPassword; Add-LocalGroupMember -Group (Get-LocalGroup -SID "S-1-5-32-545").Name -Member "ee-zwei"`; stopwatch. Watch both apps.
2. `Remove-LocalUser -Name "ee-zwei"`; stopwatch. Watch both apps.

**Expected result**

- Step 1: within **60 s** both apps show a new unticked row `ee-zwei` (about at the same moment).
- Step 2: within **60 s** the row disappears in both apps.

#### TC-003-35: Changes at about the same time: the last one wins, both apps agree

- **Verifies**: AC-24
- **Machine / account**: PC2 + service PC / Admin, log monitor
- **Precondition**: both apps green; `ee-lena` unticked in both

**Steps**

1. **Simultaneous clicks**: put one hand on each mouse. Click `ee-lena` in App A and App B **at the same moment** (both send "tick"). Wait 5 s.
2. **Crossing clicks**: in App A untick `ee-lena`; **as fast as you can** (within about 1 s) click `ee-lena` in App B, whatever it shows at that moment. Wait 5 s.
3. Repeat step 2 with the roles swapped (App B first, then App A).
4. After each step, look at both apps and at the last two `ee-lena` lines in the log monitor (or LOG-FIND `ee-lena`). Write the final state per step in Notes.

**Expected result**

- After every step: at most **5 s** after the last click, **both apps show the same state** for `ee-lena`, and it is the state of the **last** `ee-lena` log line (the change received last).
- Every click that reached the service has its own log line (with increasing revision numbers).
- No app shows a state different from the other for longer than 5 s. An error text in one app is a **Note** (not expected, but allowed by AC-16 if that app then shows the stored state).

## 6. Regression

None. Story test runs contain **no regression cases** (`02_Implementation/docs/testing/README.md` §3, TES rule 4). Regression runs only when Michael explicitly requests it before a major version release, as a separate run built from `02_Implementation/docs/testing/regression-checklist.md`.

## 7. Cleanup

After the run (keep EagleEye 0.3.1 installed; later stories build on it):

1. **Service logs**: LOG-COPY with `<subfolder>` = `run-01-service-logs` (Terminal (Administrator)). Look through the copies before committing (§2).
2. **Test accounts** (Terminal (Administrator)). This also checks once more that deletions are handled (the rows disappear from App A within 60 s):
   ```powershell
   "ee-annika","ee-anna","ee-max","ee-lena","ee-gesperrt","ee-neu","ee-zwei","defaultuser0","defaultuser1","ee-admin" |
     ForEach-Object { if (Get-LocalUser -Name $_ -ErrorAction SilentlyContinue) { Remove-LocalUser -Name $_ } }
   ```
   Leave out `defaultuser0` / `defaultuser1` if they existed before the run (S-10). No user profiles were created (none of these accounts signed in), except for a Microsoft account from TC-003-15 if you signed in with it.
3. **Microsoft account (TC-003-15)**, if added and not wanted: *Einstellungen → Konten → Andere Benutzer* → the account → *Entfernen* → *Konto und Daten löschen*.
4. **`eagleeye-kid`**: is a standard account again (`Get-LocalGroupMember -SID "S-1-5-32-544"` does not list it; TC-003-09 removed it). It stays unticked. Sign it out if signed in.
5. **Remaining tick**: none of the deleted accounts matters. The service forgets their selections by itself (AC-22).
6. **PC2**: App B stays installed and paired (useful for later two-app stories). If you prefer a clean PC2: *Kopplung aufheben* → confirm, uninstall, delete the copied installer.
7. **Display scaling**: back to your usual value if you changed it (S-5).

## 8. Notes and Open Questions for Michael

Found while writing this plan. Each has a proposed answer; none blocks the plan.

| ID | Question | Proposed answer |
|---|---|---|
| Q-1 | **AC-4 needs an account linked to a Microsoft account** as a standard user on the service PC. Your own admin account does not count (admins are never listed). Do you have a spare Microsoft account (e.g. a family member's or a test account) you can add for the run? | Yes, add one in TC-003-15 and remove it afterwards. If none is available, TC-003-15 is **Blocked**, and AC-4 stays open until a later run; DEV cannot unit-test the Windows enumeration. |
| Q-2 | **AC-9 needs zero standard accounts** for a moment. TC-003-08 makes `eagleeye-kid` an administrator for about a minute. If S-10 finds other standard accounts (e.g. real family accounts), they would have to be made administrators too. | Do it for `eagleeye-kid` only. If other standard accounts exist, make them administrators for the minute as well (they must not be signed in), or mark TC-003-08 **Blocked** and TES moves AC-9 to a later run. |
| Q-3 | **Which texts are Pass criteria?** The story says wording differences are Notes. OQ-8 explicitly agreed the section title and the checkbox label. | Pass criteria: *Benutzerkonten auf dem EagleEye-PC*, *Unter Elternkontrolle*, single menu entry *Einstellungen*. All other texts: differences are Notes (as in US-002 §2). |
| Q-4 | **"Wird geladen …" (AC-13)** is shown only while the list is fetched, normally a fraction of a second, so you may not see it. | Record it as a Note (seen / not seen). The AC-13 Pass rests on the list appearing with the stored ticks without any action; the loading state is covered by DEV's unit tests. |
| Q-5 | **AC-16 failure method**: pausing `EagleEye.Service.exe` for about 20 s with *Ressourcenmonitor → Prozess anhalten*. It is harmless (the service continues where it stopped), but it is a manual intervention in a SYSTEM service. | Accept. Fallback: network cable of PC2 (TC-003-23). **Update after alignment:** DEV recommends the PC2 cable (implementation report §5.4), so TC-003-23 now uses the cable as the main method and moved to Block E. Pausing the process is only the alternative. |
| Q-6 | **AC-2 "admin through another group"** cannot be set up on a workgroup PC (Windows does not nest local groups). | Not tested manually; accepted with DEV's design. If the PC ever joins a domain, TES adds a case. |
| Q-7 | **PC2 after the run**: keep App B installed and paired? | Yes, keep it (later stories need two apps again). |
| Q-8 | **Leftover `%ProgramData%\EagleEye` before the 0.2.0 install** (S-2, row "Nothing"). Nothing of EagleEye is installed, but uninstalling keeps this folder: an old certificate and the pairings of apps that no longer exist. Delete it for a clean start? | Yes, delete it. The 0.2.0 installer then creates a fresh certificate and database, and App A is paired fresh in S-3. Nothing of value is lost, because no app is paired with the old data any more. |
| Q-9 | **Network cable on PC2** (TC-003-23). The main method for AC-16 needs PC2 on a cable that you can unplug. With Wi-Fi, Windows may report the lost network at once, and then the error cannot be provoked. Is PC2 on a cable, or can it be for the test? | Connect PC2 by cable for Block E. If that is not possible, try Wi-Fi off first, then the process-pause alternative. If neither provokes the error, mark TC-003-23 **Blocked**; AC-16 is then covered only by DEV's unit tests until a later run. |

**Answers (Michael, 2026-10-07)**: Q-1 to Q-7: proposed answers accepted (test plan approved as a whole). Q-8: yes. Q-9: yes.

## 9. Change Log

| Date | Change | Reason |
|---|---|---|
| 2026-10-07 | Plan written (in parallel with DEV); places that depend on the implementation marked for checking against the implementation report after DEV's handover. | Step d started early at Michael's request. |
| 2026-10-07 | Aligned with `implementation-report.md`. All 9 kinds of marked places are resolved and the markers removed: installer names, dates and SHA-256 (S-1); UI texts (instruction, "Wird geladen …", "(deaktiviert)", the error text below the list, row disabled while saving); log line formats; `logs\` ACL and its repair at every start. Setup S-2/S-3 rewritten: nothing is installed on the service PC at the moment, so the setup first installs and pairs 0.2.0 (robust for any state found) to keep the upgrade path testable. TC-003-23 (AC-16) moved to Block E: the PC2 network cable is now the main method, process pause the alternative (DEV §5.4). New: "If there is no log file" (Event Log warning, DEV §5.2); unexpected error text (D-6); no log line for admin-only changes (D-3, TC-003-10). Block durations updated. New questions Q-8 and Q-9. | DEV handover (commits 8037215, 4c1b1ca, c5e419e, 02372b1); Orchestrator facts 2026-10-07. |
| 2026-10-07 | Approved by Michael; Q-1 to Q-9 answered. | Michael |
| 2026-10-07 | Updated for patch **0.3.1** (ISSUE-006): installers `EagleEye-Setup-0.3.1.exe` (12:59) and `EagleEye-ParentApp-Setup-0.3.1.exe` (13:00) with full SHA-256 checks in S-1; upgrade path 0.2.0 → 0.3.1; tray version note. S-2: new row for "0.3.0/0.3.1 installed" (uninstall it, delete `%ProgramData%\EagleEye` and `%LOCALAPPDATA%\EagleEye`, then install 0.2.0; the LEOSERV app's pairing becomes invalid). New case **TC-003-36** (table layout, light/dark, 150 %, screenshot `evidence/ISSUE-006-after-fix.png`) in Block B after TC-003-13. TC-003-08 and TC-003-19 also expect no table header. Row wording changed from layout-neutral to the table layout. Durations: setup ≈ 25 min, Block B ≈ 40 min, 36 cases. | ISSUE-006 fixed by DEV (commit 2dac544); service PC state reported by the Orchestrator. |

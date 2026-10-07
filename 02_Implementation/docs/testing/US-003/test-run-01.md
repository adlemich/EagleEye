# Test Run 01: US-003 — Account Inventory and Selection of Accounts under Parental Control

**Test plan**: `docs/testing/US-003/test-plan.md` (draft 2026-10-07, for approval)
**Prepared by**: TES, 2026-10-07
**Executed by**: Michael
**Execution date**: <!-- fill in -->
**Build / installer version**: 0.3.0 — `03_Delivery/windows/EagleEye-Setup-0.3.0.exe` (service + tray, file date: ________) and `03_Delivery/windows/EagleEye-ParentApp-Setup-0.3.0.exe` (parent app, file date: ________) <!-- verify against implementation report -->
**Machine(s)**: Windows Developer Machine (service PC) <!-- add Windows version (winver) --> · PC2 (Block E) <!-- add Windows version / display language / scaling -->

> **Start this run only after DEV's handover** (story status `Implemented`, `02_Implementation/docs/requirements/user-stories/US-003/implementation-report.md` present) and after TES has aligned the places marked `verify against implementation report`.
>
> **What this run covers**: all 35 cases of the test plan, TC-003-01 to TC-003-35. **No regression cases** (testing README; regression runs only on your explicit request before a major version release).
>
> **How to record**: tick exactly one result box per case (`[x]`). Fill in **Observed** for Fail/Blocked. Screenshots go into `docs/testing/US-003/evidence/` (e.g. `run01-tc13-list.png`); reference them in Notes. When you are done, or want to stop halfway, tell TES: *"test run 01 for US-003 is done"*.
>
> **Service log files (new from 0.3.0)**: the logs are in the admin-only folder `%ProgramData%\EagleEye\logs\`. Copy them with **LOG-COPY** (below) from a **Terminal (Administrator)**:
> - **right after any Fail** → `<subfolder>` = `run-01-service-logs\after-TC-003-NN` (NN = the failed case), and write that folder into the case's Notes;
> - **before TC-003-06** → `run-01-service-logs\before-TC-003-06` (that case deletes the log folder);
> - **at the end of the run** (or of each block, if you stop in between) → `run-01-service-logs`.
>
> **Duration**: Block A ≈ 30 min · Block B ≈ 30 min · Block C ≈ 45 min (reboot) · Block D ≈ 35 min · Block E ≈ 40 min. Run the blocks in this order; breaks between blocks are fine (leave accounts and ticks as they are).
>
> **Accounts**: **Admin** = your administrator account. **Kid** = `eagleeye-kid`. Test accounts (`ee-anna`, `ee-max`, `ee-lena`, `ee-gesperrt`, `defaultuser0`, `defaultuser1`, `ee-admin`) are created in S-12.
> **Apps**: **App A** = parent app in your Admin account on the service PC (*Papas PC*). **App B** = parent app on PC2 (*PC2*, Block E).
> **Terminal (Administrator)** = an **elevated** terminal: Start menu → **Terminal** → right-click → **Als Administrator ausführen**; the title bar starts with **"Administrator:"**. A normal terminal in your admin account is **not** enough (ISSUE-005 in US-002). Every account change, service command and log command below needs it.
>
> **Rules for this run** (test plan §2):
> - **UI wording**: a different text with the same meaning is a **Note**, not a Fail. Pass criteria are only *Benutzerkonten auf dem EagleEye-PC*, *Unter Elternkontrolle* and the single menu entry *Einstellungen*. If you want a text changed, write the wanted text in Notes.
> - **Timing**: stopwatch. Account changes on the PC → list changed in the app: **≤ 60 s** (start when the command returned or you clicked *OK*). Ticks: **≤ 5 s**. Do not touch the app while timing. Where the story gives no limit, slowness is a Note.
> - **150 % scaling**: if any text, row or checkbox in the new section is cut off, mark the case **Fail** (ISSUE-004 lesson).
> - A row may be greyed out for up to 4 s while its tick is being saved: expected.
> - **Unexpected behaviour** goes into Notes (TES routes it to PRO).

### Commands used in this run (Terminal (Administrator))

**LOG-COPY** (in the repo root; replace `<subfolder>`):

```powershell
Set-Location <your EagleEye repo folder>
$dst = Join-Path "02_Implementation\docs\testing\US-003\evidence" "<subfolder>"
New-Item -ItemType Directory -Force $dst | Out-Null
Copy-Item "$env:ProgramData\EagleEye\logs\*.log" $dst -Force
Get-ChildItem $dst
```

**LOG-WATCH** (in the log monitor; after every service restart, reinstall, reboot or log-folder deletion: *Strg+C* and run it again):

```powershell
$log = Get-ChildItem "$env:ProgramData\EagleEye\logs\EagleEye.Service-*.log" | Sort-Object LastWriteTime | Select-Object -Last 1
$log.FullName
Get-Content $log.FullName -Tail 15 -Wait
```

**LOG-FIND** (replace `<pattern>`, e.g. a user name):

```powershell
Select-String -Path "$env:ProgramData\EagleEye\logs\*.log" -Pattern "<pattern>" | Select-Object -Last 10 | ForEach-Object Line
```

**EXPECTED-LIST** (the standard accounts as Windows sees them):

```powershell
$adminSids = Get-LocalGroupMember -SID "S-1-5-32-544" | ForEach-Object { $_.SID.Value }
Get-LocalUser |
  Where-Object { $adminSids -notcontains $_.SID.Value -and $_.SID.Value -notmatch '-(500|501|503|504)$' -and $_.Name -ne 'defaultuser0' } |
  Sort-Object Name | Format-Table Name, FullName, Enabled -AutoSize
```

(If `Get-LocalGroupMember` fails with "Failed to compare two elements…", use `net localgroup Administratoren` and compare by hand.)

---

## Setup (service PC, Admin) — before Block A

*Expected start state (end of US-002): service 0.2.0 running; App A 0.2.0 installed and paired as Papas PC with `<host>`.*

- [ ] **S-1 Installers present.** PowerShell in the repo root: `Get-Item 03_Delivery\windows\EagleEye-Setup-0.3.0.exe, 03_Delivery\windows\EagleEye-ParentApp-Setup-0.3.0.exe | Select-Object Name, LastWriteTime`. Both exist; dates written in the header.
- [ ] **S-2 Installed state.** *Einstellungen → Apps → Installierte Apps* → "EagleEye". Found: EagleEye ________ · EagleEye Parent App ________ (expected 0.2.0 / 0.2.0).
- [ ] **S-3 App A paired and green**: "Verbunden mit `<host>`", *Gekoppelt*, device **Papas PC**. If not paired: pair it now (setup only; code from the tray popup in your Admin session).
- [ ] **S-4 No development instances** (*Task-Manager → Details*: no `EagleEye.Service.exe` from a `02_Implementation` folder, only one `EagleEye.ParentApp.exe`).
- [ ] **S-5 Admin display scaling** (*Einstellungen → System → Bildschirm → Skalierung*): ______ % (expected 150 %).
- [ ] **S-6 Terminal (Administrator)** open; title bar "Administrator:". Service commands: `Stop-Service` / `Start-Service` / `Restart-Service -DisplayName "EagleEye Service"`.
- [ ] **S-7 Log monitor**: a **second** Terminal (Administrator), placed next to App A. (LOG-WATCH works only after TC-003-01.)
- [ ] **S-8 Kid signed out** (*Task-Manager → Benutzer* → `eagleeye-kid` → *Abmelden*, if listed).
- [ ] **S-9 Names.** `hostname` → `<host>` = ____________
- [ ] **S-10 Existing local accounts.** Terminal (Administrator): `Get-LocalUser | Format-Table Name, FullName, Enabled, SID -AutoSize`, then EXPECTED-LIST.
  - Full name of `eagleeye-kid`: ____________ (empty = none)
  - Other standard accounts besides `eagleeye-kid`: ____________ (none expected; if there are any, see test plan §8 Q-2 before TC-003-08)
  - `defaultuser0` exists already: [ ] yes [ ] no · `defaultuser1` exists already: [ ] yes [ ] no
- [ ] **S-11 SmartScreen** on the unsigned installers: *Weitere Informationen* → *Trotzdem ausführen* (expected).

---

## Block A — Update to 0.3.0 and the admin-only log folder (Admin, one visit to Kid) · ≈ 30 min

### TC-003-01: Service update 0.2.0 → 0.3.0 *(supporting)*

*Supporting (prerequisite for AC-14, AC-15) · Service PC / Admin (+ Terminal (Administrator)) · 0.2.0 installed, App A open and green*

1. Run `03_Delivery\windows\EagleEye-Setup-0.3.0.exe` with defaults, finish. Leave App A open.
2. If no tray icon in your Admin session: Start menu → **EagleEye Tray**. Tray icon → *App Infos*.
3. *Installierte Apps* → "EagleEye".
4. `services.msc` → **EagleEye Service**.
5. Terminal (Administrator): `Test-Path "$env:ProgramData\EagleEye\logs"` and `Test-Path "$env:ProgramData\EagleEye\EagleEye.Service.db"`
6. Watch App A (still 0.2.0) for up to 60 s.

**Expected**:
- Wizard finishes without error; no question about firewall, port or certificate.
- *App Infos*: **EagleEye_v0.3**. <!-- verify against implementation report -->
- **EagleEye** listed once, **0.3.0**. Service *Wird ausgeführt*.
- Both `Test-Path`: **True**.
- App A green "Verbunden mit `<host>`" again within 60 s, no pairing code.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

### TC-003-02: Parent app update keeps the pairing and shows the new section with all accounts unticked

*Verifies AC-6, AC-13, AC-18; supporting AC-15 · Service PC / Admin · App A open, green*

1. With App A open, run `03_Delivery\windows\EagleEye-ParentApp-Setup-0.3.0.exe` with defaults (note how it handles the running app). Start the app.
2. *Installierte Apps* → "EagleEye Parent App".
3. Look at the menu and the settings page top to bottom.

**Expected**:
- Installer without errors; **EagleEye Parent App 0.3.0**.
- Green "Verbunden mit `<host>`" **without a pairing code**; *Gekoppelt*, **Papas PC**.
- Menu: only **Einstellungen**.
- Sections in this order: **Darstellung**, **Serververbindung**, **Benutzerkonten auf dem EagleEye-PC**.
- The new section lists the standard accounts from S-10 (at least `eagleeye-kid`), each with a checkbox **Unter Elternkontrolle**, **none ticked**.
- Nothing cut off at 150 %.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**: How the installer handled the running app: ________ · Instruction text above the rows: ________ <!-- verify against implementation report: expected "Markieren Sie die Konten, die unter Elternkontrolle stehen." -->

---

### TC-003-03: The log folder is restricted to SYSTEM and Administratoren

*Verifies AC-14 · Service PC / Admin, **Terminal (Administrator)** · TC-003-01 done*

0. Title bar starts with **"Administrator:"**? (In a non-elevated terminal step 2 ends with *Zugriff verweigert*: Windows UAC, not a product error.)
1. `icacls "$env:ProgramData\EagleEye\logs"`
2. `Get-ChildItem "$env:ProgramData\EagleEye\logs"`
3. `icacls "$env:ProgramData\EagleEye"`

**Expected**:
- Step 1: exactly `NT-AUTORITÄT\SYSTEM:(OI)(CI)(F)` and `VORDEFINIERT\Administratoren:(OI)(CI)(F)`; no *Benutzer*, *Authentifizierte Benutzer*, *Jeder*; no `(I)`. <!-- verify against implementation report -->
- Step 2: at least `EagleEye.Service-001.log`.
- Step 3: as in US-002: SYSTEM (F), Administratoren (F), Benutzer (RX).

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**: <!-- on Fail: paste the output of steps 1-3 -->
- **Notes**: Title bar showed "Administrator:"? [ ] yes  [ ] no

---

### TC-003-04: The service writes its log file, readable for the administrator *(supporting)*

*Supporting AC-14 · Service PC / Admin, log monitor (S-7) · TC-003-03 done*

1. Log monitor: **LOG-WATCH**. Note the file name.
2. Look at the lines.
3. Open the same file in Notepad started as administrator (*Editor* → right-click → *Als Administrator ausführen* → *Datei → Öffnen*); close without saving.

**Expected**:
- Lines like `yyyy-MM-dd HH:mm:ss.fff +02:00 [INF] Category: message`. <!-- verify against implementation report -->
- A line "Account inventory loaded: N standard accounts, 0 under parental control." (N = standard accounts from S-10). <!-- verify against implementation report -->
- Notepad opens the file while the service runs.
- No pairing code, password or token-like string in any line.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**: Log file name: ____________

---

> **Switch to the Kid account** (*Benutzer wechseln* → `eagleeye-kid`; do not sign Admin out).

### TC-003-05: The kid cannot read the log folder, but the rest of the data folder as before

*Verifies AC-14 · Service PC / Kid · signed in as `eagleeye-kid`*

1. Explorer → address bar `C:\ProgramData\EagleEye\logs` → Enter (do **not** click *Fortsetzen*).
2. PowerShell (normal): `Get-ChildItem C:\ProgramData\EagleEye\logs`
3. `Get-Content C:\ProgramData\EagleEye\logs\EagleEye.Service-001.log -TotalCount 3` (file name from TC-003-04)
4. `Get-ChildItem C:\ProgramData\EagleEye`

**Expected**:
- Step 1: Explorer refuses access (*"Sie verfügen momentan nicht über die Berechtigung…"*).
- Steps 2 and 3: **Zugriff verweigert**; no file name, no log line.
- Step 4: folder content listed (at least `logs`, `certs`, database file), no error.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

> **Switch back to Admin.** Leave the Kid **signed in** in the background (needed for TC-003-10).

---

### TC-003-06: A deleted log folder comes back with the same protection

*Verifies AC-14 · Service PC / Admin, Terminal (Administrator) · TC-003-05 done*

1. **LOG-COPY** with `<subfolder>` = `run-01-service-logs\before-TC-003-06`.
2. Log monitor: *Strg+C*.
3. Terminal (Administrator):
   ```powershell
   Stop-Service -DisplayName "EagleEye Service"
   Remove-Item "$env:ProgramData\EagleEye\logs" -Recurse -Force
   Test-Path "$env:ProgramData\EagleEye\logs"
   Start-Service -DisplayName "EagleEye Service"
   icacls "$env:ProgramData\EagleEye\logs"
   Get-ChildItem "$env:ProgramData\EagleEye\logs"
   ```
4. Log monitor: LOG-WATCH.

**Expected**:
- `Test-Path` after deletion: **False**.
- After the start: `icacls` as in TC-003-03 (SYSTEM + Administratoren, F, no `(I)`, no *Benutzer*).
- A new `EagleEye.Service-001.log`; the log monitor shows a new "Account inventory loaded …" line.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**: <!-- on Fail: paste the icacls output -->
- **Notes**: App A red and green again within 60 s? [ ] yes  [ ] no

---

### TC-003-07: A loosened log folder is repaired at the next service start *(supporting)*

*Supporting AC-14 · Service PC / Admin, Terminal (Administrator) · TC-003-06 done*

1. ```powershell
   icacls "$env:ProgramData\EagleEye\logs" /grant "*S-1-5-32-545:(OI)(CI)RX"
   icacls "$env:ProgramData\EagleEye\logs"
   ```
2. `Restart-Service -DisplayName "EagleEye Service"`, then `icacls "$env:ProgramData\EagleEye\logs"`.
3. Log monitor: *Strg+C*, LOG-WATCH.

**Expected**:
- Step 1: `VORDEFINIERT\Benutzer:(OI)(CI)(RX)` now listed.
- Step 2: the *Benutzer* entry is **gone**; only SYSTEM and Administratoren. <!-- verify against implementation report -->
- **If it is still there**: Fail, then remove it by hand: `icacls "$env:ProgramData\EagleEye\logs" /remove:g "*S-1-5-32-545"`.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

## Block B — Inventory content and display (Admin) · ≈ 30 min

*Start state: end of Block A (App A 0.3.0 green, Kid signed in in the background, log monitor running). Keep App A on the settings page with the account section visible.*

### TC-003-08: With no standard account, the section says so

*Verifies AC-9, AC-20, AC-2 · Service PC / Admin, Terminal (Administrator) · test accounts not created yet; only standard account = `eagleeye-kid` (S-10)*

1. `Add-LocalGroupMember -SID "S-1-5-32-544" -Member "eagleeye-kid"` (and the same for any other standard account from S-10, see test plan §8 Q-2). Stopwatch when the command returns.
2. Watch App A; stop when the section changes.
3. EXPECTED-LIST → returns no account.

**Expected**:
- Within **60 s**: **"Keine Nicht-Administrator-Konten vorhanden"**, no rows.
- Log monitor: "Account inventory changed … removed [eagleeye-kid] … 0 standard accounts." <!-- verify against implementation report -->

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**: Seconds until changed: ______

---

### TC-003-09: An account that becomes standard again and was never ticked appears unticked

*Verifies AC-21, AC-19 · Service PC / Admin, Terminal (Administrator) · TC-003-08 done*

1. `Remove-LocalGroupMember -SID "S-1-5-32-544" -Member "eagleeye-kid"` (and any other account from TC-003-08 step 1). Stopwatch.
2. Watch App A; stop when the row appears.

**Expected**:
- Within **60 s**: row `eagleeye-kid` back, **not ticked**; "Keine Nicht-Administrator-Konten vorhanden" gone.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**: Seconds until the row appeared: ______

---

> - [ ] **S-12 Create the test accounts** (Terminal (Administrator)). Leave out the `New-LocalUser` line of `defaultuser0` / `defaultuser1` if S-10 found it already. **Start the stopwatch for TC-003-10 when the script prints "done".**
>   ```powershell
>   $pw = Read-Host -AsSecureString "Password for the EagleEye test accounts"
>   $usersGroup = (Get-LocalGroup -SID "S-1-5-32-545").Name
>   New-LocalUser -Name "ee-anna"      -FullName "anna Test" -Password $pw -PasswordNeverExpires
>   New-LocalUser -Name "ee-max"       -FullName "Max Test"  -Password $pw -PasswordNeverExpires
>   New-LocalUser -Name "ee-lena"                            -Password $pw -PasswordNeverExpires
>   New-LocalUser -Name "ee-gesperrt"                        -Password $pw -PasswordNeverExpires
>   New-LocalUser -Name "defaultuser0"                       -Password $pw -PasswordNeverExpires
>   New-LocalUser -Name "defaultuser1"                       -Password $pw -PasswordNeverExpires
>   New-LocalUser -Name "ee-admin"     -FullName "EE Admin"  -Password $pw -PasswordNeverExpires
>   "ee-anna","ee-max","ee-lena","ee-gesperrt","defaultuser0","defaultuser1" | ForEach-Object { Add-LocalGroupMember -Group $usersGroup -Member $_ }
>   Add-LocalGroupMember -SID "S-1-5-32-544" -Member "ee-admin"
>   Disable-LocalUser -Name "ee-gesperrt"
>   "done: $(Get-Date -Format HH:mm:ss)"
>   ```
>   (`ee-admin` is a standard account for a few seconds before it joins *Administratoren*; if it shows up briefly and then disappears, that is expected.)

### TC-003-10: New standard accounts appear unticked; administrators do not

*Verifies AC-1, AC-2, AC-18, AC-19 · Service PC / Admin (Kid signed in in the background) · S-12 just finished, stopwatch running*

1. Watch App A; stop when the new rows have appeared.
2. Compare with EXPECTED-LIST.

**Expected**:
- Within **60 s**: rows for `eagleeye-kid` (signed in) and `ee-anna`, `ee-max`, `ee-lena`, `ee-gesperrt`, `defaultuser1` (never signed in), plus any other standard account from S-10 — the same set as EXPECTED-LIST.
- **None** of the new rows is ticked.
- **Not** shown: `ee-admin`, your Admin account, *Administrator*.
- Log monitor: "Account inventory changed … added [...]" with the new accounts (no `defaultuser0`). <!-- verify against implementation report -->

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**: Seconds until the rows appeared: ______ · `ee-admin` visible briefly? [ ] yes  [ ] no

---

### TC-003-11: Built-in accounts and `defaultuser0` are hidden, `defaultuser1` is not

*Verifies AC-3 · Service PC / Admin, Terminal (Administrator) · TC-003-10 done*

1. `Get-LocalUser | Format-Table Name, Enabled, SID -AutoSize` — find *Gast*, *DefaultAccount*, *WDAGUtilityAccount* (SIDs ending -501, -503, -504), `defaultuser0`, `defaultuser1`.
2. Look at the list in App A.
3. LOG-FIND with `<pattern>` = `defaultuser0`.

**Expected**:
- Step 1: the built-in accounts exist (normally disabled); `defaultuser0` / `defaultuser1` enabled.
- App A: **no** row for *Gast*, *DefaultAccount*, *WDAGUtilityAccount*, **`defaultuser0`**.
- App A: a row **`defaultuser1`**.
- Step 3: no line names `defaultuser0`.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

### TC-003-12: Rows show "full name (user name)", or the user name alone

*Verifies AC-11 · Service PC / Admin · TC-003-10 done*

1. Read the text of each row.

**Expected**:
- `anna Test (ee-anna)`, `Max Test (ee-max)`.
- `ee-lena`, `defaultuser1` (user name only, no empty brackets).
- `eagleeye-kid` by the same rule (full name from S-10).

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

### TC-003-13: Rows are sorted alphabetically, ignoring upper and lower case

*Verifies AC-10 · Service PC / Admin · TC-003-10 done*

1. Read the order of the rows top to bottom; look at each row's checkbox and label. Screenshot → `evidence/run01-tc13-list.png`.

**Expected**:
- Order (the `eagleeye-kid` row at its alphabetical place by its shown name): `anna Test (ee-anna)` · `defaultuser1` · `eagleeye-kid` · `ee-gesperrt (deaktiviert)` · `ee-lena` · `Max Test (ee-max)`.
- `anna Test` **before** `Max Test`.
- One checkbox **Unter Elternkontrolle** per row, all unticked.
- Nothing cut off at 150 %.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**: <!-- on Fail: the order you saw -->
- **Notes**:

---

### TC-003-14: A disabled account is listed with "(deaktiviert)" and can be ticked

*Verifies AC-12 · Service PC / Admin, log monitor · TC-003-10 done*

1. Find the row of `ee-gesperrt`.
2. Tick **Unter Elternkontrolle** in that row.

**Expected**:
- Row reads **`ee-gesperrt (deaktiviert)`**. <!-- verify against implementation report -->
- It can be ticked and stays ticked; within 5 s a log line naming `ee-gesperrt` with **yes**.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

### TC-003-15: An account linked to a Microsoft account is listed

*Verifies AC-4 · Service PC / Admin · a Microsoft account you may add as a standard user (test plan §8 Q-1); if none: **Blocked** ("no Microsoft account available")*

1. *Einstellungen → Konten → Andere Benutzer* (older: *Familie und andere Benutzer*) → *Konto hinzufügen* → e-mail address → finish. *Kontotyp* = *Standardbenutzer*.
2. If no row appears within 60 s: sign in once with the account (*Benutzer wechseln*), sign out, back to Admin.
3. Terminal (Administrator): `Get-LocalUser | Where-Object PrincipalSource -eq MicrosoftAccount | Format-Table Name, FullName`
4. Look at App A.

**Expected**:
- Step 3 lists the account (local user name, often shortened).
- App A: a row `<full name> (<user name>)` (or user name only), not ticked.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**: Step 2 needed? [ ] yes  [ ] no · Row text: ____________

---

## Block C — Selecting, saving, keeping, error (Admin) · ≈ 45 min

*Start state: end of Block B. Ticked so far: `ee-gesperrt`. Log monitor running.*

### TC-003-16: Ticking saves at once and writes a log entry

*Verifies AC-14 · Service PC / Admin, log monitor · App A green, list shown*

1. Look for a *Speichern* button anywhere on the settings page.
2. Tick `Max Test (ee-max)`; stopwatch at the click.
3. Watch the row and the log monitor.
4. Tick `anna Test (ee-anna)` and `ee-lena` too.

**Expected**:
- **No** save button.
- Within **5 s**: log line naming **`ee-max`** with **yes** (e.g. "Account ee-max (S-1-5-21-…): under parental control = yes (set by parent device Papas PC, …)"). <!-- verify against implementation report -->
- Checkbox stays ticked (may be greyed out for a moment); no error text.
- Step 4: lines for `ee-anna` yes and `ee-lena` yes.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**: Seconds until the log line: ______ · Same entry in the Event Log (*Anwendung*, source EagleEye)? [ ] yes  [ ] no  [ ] not checked

---

### TC-003-17: Unticking saves at once and writes a log entry

*Verifies AC-14 · Service PC / Admin, log monitor · `ee-gesperrt` ticked*

1. Untick `ee-gesperrt (deaktiviert)`; stopwatch.

**Expected**:
- Within **5 s**: log line naming **`ee-gesperrt`** with **no**.
- Checkbox stays unticked; no error text.

> **Tick record** (compare with it until the end of Block D): ticked = `ee-anna`, `ee-lena`, `ee-max` · unticked = `defaultuser1`, `eagleeye-kid`, `ee-gesperrt`.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

### TC-003-18: The ticks survive closing and restarting the parent app

*Verifies AC-15 · Service PC / Admin · tick record*

1. Close App A (**X**); *Task-Manager*: no `EagleEye.ParentApp.exe`.
2. Start App A; wait for green.

**Expected**:
- Same rows, exactly the ticks of the tick record.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

### TC-003-19: While the service is stopped, there is nothing to tick

*Verifies AC-8, AC-17 · Service PC / Admin, Terminal (Administrator) · App A green*

1. `Stop-Service -DisplayName "EagleEye Service"`; stopwatch.
2. Watch App A until red.
3. Look at the account section; look for any checkbox.

**Expected**:
- **Red** "Nicht verbunden mit `<host>`" within 30 s.
- Section: **"Keine Daten verfügbar"**, **no** rows, no checkboxes (no read-only old list).

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

### TC-003-20: After the service starts again, the list comes back with the same ticks

*Verifies AC-13, AC-15 · Service PC / Admin, Terminal (Administrator) · service stopped, app red*

1. `Start-Service -DisplayName "EagleEye Service"`; stopwatch. Do not touch App A.
2. Watch the account section closely while the app reconnects.
3. Log monitor: *Strg+C*, LOG-WATCH.

**Expected**:
- Green within **60 s**, no action.
- The list with exactly the ticks of the tick record, no action.
- Log: "Account inventory loaded: … 3 under parental control." <!-- verify against implementation report -->

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**: Seconds until green: ______ · "Wird geladen …" visible? [ ] yes  [ ] no (may be too short to see) <!-- verify against implementation report -->

---

### TC-003-21: The ticks survive a reboot of the service PC

*Verifies AC-15 · Service PC / Admin · tick record unchanged; save open work (the reboot ends the Kid session)*

1. Start → *Ein/Aus* → *Neu starten*.
2. Sign in as Admin; start App A (if it does not start by itself); wait for green.
3. Open the log monitor (S-7, LOG-WATCH) and the Terminal (Administrator) (S-6) again.

**Expected**:
- App A green without a pairing code; exactly the ticks of the tick record.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

### TC-003-22: The ticks survive a re-install of the service

*Verifies AC-15 · Service PC / Admin · App A green*

1. Run `03_Delivery\windows\EagleEye-Setup-0.3.0.exe` again with defaults. Leave App A open.
2. Wait until App A is green (up to 60 s after the wizard). Log monitor: *Strg+C*, LOG-WATCH.
3. Terminal (Administrator): `icacls "$env:ProgramData\EagleEye\logs"`

**Expected**:
- Wizard without errors.
- App A green without a pairing code; exactly the ticks of the tick record.
- Step 3: only SYSTEM and Administratoren.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

### TC-003-23: A change that cannot be saved shows an error and the checkbox returns

*Verifies AC-16 · Service PC / Admin, *Ressourcenmonitor* (elevated), log monitor · App A green, `ee-lena` ticked*

> How the failure is provoked: the service process is **paused** for about 20 s. The connection stays open but the service does not answer, so the app gets no confirmation and gives up after about 4 s. (Stopping the service does not work: the app notices the closed connection at once and removes the list.) <!-- verify against implementation report -->

1. Terminal (Administrator) → `resmon`. Tab **CPU** → *Prozesse* → `EagleEye.Service.exe`.
2. Right-click → **Prozess anhalten** → confirm. Clock time: ________. **Be quick from here:**
3. Within 10 s: in App A **untick** `ee-lena`; stopwatch at the click.
4. Watch the row and the section for 10 s; stop the stopwatch when the error text appears.
5. Right-click `EagleEye.Service.exe` → **Prozess fortsetzen** (at the latest 25 s after step 2).
6. Watch App A and the log monitor for 15 s.
7. LOG-FIND with `ee-lena`.

**Expected**:
- Within **5 s** after the click: **"Die Änderung konnte nicht gespeichert werden. Bitte erneut versuchen."** and the `ee-lena` checkbox is **ticked again**. <!-- verify against implementation report -->
- After step 5: App A shows for `ee-lena` the state of the **last** `ee-lena` log line (the service may store the queued untick after all; then the row becomes unticked by itself). Both outcomes are a Pass if the app ends up equal to the log.
- At no time a state different from the last log line for longer than 5 s.
- **Afterwards**: `ee-lena` **ticked** again (tick it if needed). If the service does not continue: `Restart-Service -DisplayName "EagleEye Service"`.

*Fallback, if pausing is not possible*: in Block E, with App B green, unplug PC2's network cable (or switch off Wi-Fi) and untick a row in App B within 5 s; same expected result.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**: Method: [ ] paused process  [ ] PC2 network · Seconds until error text: ______ · App went red / "Keine Daten verfügbar" during the pause? [ ] yes  [ ] no · Final state of `ee-lena` after resume: ________ · Last `ee-lena` log line: ________

---

## Block D — Account changes while the service runs (Admin) · ≈ 35 min

*Start state: end of Block C, tick record of TC-003-17. App A on the settings page; do not touch it while timing.*

### TC-003-24: A new standard account appears unticked within 60 s

*Verifies AC-19, AC-18 · Service PC / Admin, Terminal (Administrator) · App A green*

1. `New-LocalUser -Name "ee-neu" -NoPassword; Add-LocalGroupMember -Group (Get-LocalGroup -SID "S-1-5-32-545").Name -Member "ee-neu"`; stopwatch when it returns.
2. Watch App A.

**Expected**:
- Within **60 s**: row **`ee-neu`**, **not ticked**, between `ee-lena` and `Max Test (ee-max)`.
- Other rows keep their ticks.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**: Seconds: ______

---

### TC-003-25: A renamed account keeps its tick and shows the new name

*Verifies AC-5, AC-19 · Service PC / Admin, `lusrmgr.msc` · `ee-anna` ticked*

1. `lusrmgr.msc` → *Benutzer* → right-click **ee-anna** → *Umbenennen* → `ee-annika` → Enter. Stopwatch.
2. Watch App A.

**Expected**:
- Within **60 s**: row **`anna Test (ee-annika)`**, still **ticked**; no row `ee-anna` any more.
- Log: "Account inventory changed … changed [ee-annika] …". <!-- verify against implementation report -->

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**: Seconds: ______

---

### TC-003-26: Disabling and enabling an account updates its note and keeps its tick

*Verifies AC-12 (plan D-6) · Service PC / Admin, `lusrmgr.msc` · `ee-annika` ticked*

1. `lusrmgr.msc` → double-click **ee-annika** → tick **Konto ist deaktiviert** → *OK*. Stopwatch.
2. Watch App A until the row changes.
3. Untick **Konto ist deaktiviert** → *OK*. Stopwatch. Watch App A.

**Expected**:
- Step 2: **`anna Test (ee-annika) (deaktiviert)`**, still **ticked**.
- Step 3: note gone, still **ticked**.
- No time limit in the story for this change: over 60 s is a **Note**, not a Fail.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**: Seconds step 2: ______ · step 3: ______

---

### TC-003-27: A ticked account that becomes an administrator disappears

*Verifies AC-20, AC-2 · Service PC / Admin, *Einstellungen* · `ee-max` ticked*

1. *Einstellungen → Konten → Andere Benutzer* → **Max Test** / **ee-max** → *Kontotyp ändern* → **Administrator** → *OK*. Stopwatch at *OK*.
2. Watch App A.

**Expected**:
- Within **60 s**: row `Max Test (ee-max)` gone. Other rows and ticks unchanged.
- Log: "Account inventory changed … removed [ee-max] …". <!-- verify against implementation report -->

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**: Seconds: ______

---

### TC-003-28: The account that becomes standard again is ticked again

*Verifies AC-21 · Service PC / Admin, *Einstellungen* · TC-003-27 done*

1. Same dialog → *Kontotyp ändern* → **Standardbenutzer** → *OK*. Stopwatch at *OK*.
2. Watch App A.

**Expected**:
- Within **60 s**: row **`Max Test (ee-max)`** back and **ticked**.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**: Seconds: ______

---

### TC-003-29: A deleted account is forgotten; a new account with the same name is unticked

*Verifies AC-19, AC-22, AC-18 · Service PC / Admin, Terminal (Administrator) · `ee-lena` ticked*

1. `Remove-LocalUser -Name "ee-lena"`; stopwatch. Watch App A until the row is gone.
2. LOG-FIND with `<pattern>` = `Forgot`.
3. `New-LocalUser -Name "ee-lena" -NoPassword; Add-LocalGroupMember -Group (Get-LocalGroup -SID "S-1-5-32-545").Name -Member "ee-lena"`; stopwatch. Watch App A.

**Expected**:
- Step 1: within **60 s** row `ee-lena` gone.
- Step 2: "Forgot the parental-control selection of 1 deleted account(s): S-1-5-21-…". <!-- verify against implementation report -->
- Step 3: within **60 s** row **`ee-lena`** again, **not ticked**.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**: Seconds step 1: ______ · step 3: ______

---

### TC-003-30: A parent app that was closed during a change shows the current list when it starts

*Verifies AC-19, AC-13 · Service PC / Admin, Terminal (Administrator) · `ee-neu` exists*

1. Close App A (**X**); *Task-Manager*: no `EagleEye.ParentApp.exe`.
2. `Remove-LocalUser -Name "ee-neu"`. Wait **30 s**.
3. Start App A; wait for green.

**Expected**:
- No `ee-neu` row. Ticked: `anna Test (ee-annika)`, `Max Test (ee-max)`; all other rows unticked.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

## Block E — Two parent apps (PC2 + service PC) · ≈ 40 min

*Setup before Block E:*

- [ ] **S-13** PC2: Windows 11, same LAN and subnet. `EagleEye-ParentApp-Setup-0.3.0.exe` copied to PC2. Parent app **not** installed on PC2 (else uninstall it and delete `%LOCALAPPDATA%\EagleEye` there). PC2 scaling: ______ % · display language: ______
- [ ] **S-14** Both screens visible at the same time. App A green on the settings page; log monitor running.

### TC-003-31: An unpaired app shows no accounts

*Verifies AC-7, AC-17 · PC2 / any local account · parent app not installed on PC2*

1. Install `EagleEye-ParentApp-Setup-0.3.0.exe` on PC2 with defaults; start the app.
2. Host dialog → *Abbrechen*. Look at the settings page.
3. *Serververbindung*: `<host>` → *Verbinden* (pairing form). Look at the account section. Do not pair yet.

**Expected**:
- Step 2: three sections; *Benutzerkonten auf dem EagleEye-PC*: **"Keine Daten verfügbar"**, no rows, no checkboxes.
- Step 3: still **"Keine Daten verfügbar"**, no rows.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**: <!-- if PC2 is English: the English texts -->

---

### TC-003-32: After pairing, App B shows the same list as App A

*Verifies AC-13, AC-10, AC-11 · PC2 + service PC / Admin · pairing form open on PC2*

1. Read the pairing code from the tray popup on the service PC (Admin session). On PC2: device name **PC2**, enter the code → *Koppeln*.
2. Compare App B's account section with App A row by row.

**Expected**:
- App B green "Verbunden mit `<host>`", *Gekoppelt*, device **PC2**.
- Same rows, same order, same names, same ticks as App A.
- Nothing cut off at PC2's scaling.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

### TC-003-33: A tick in one app appears in the other within 5 s

*Verifies AC-23 · PC2 + service PC / Admin, log monitor · both apps green, settings page, both screens visible*

1. **App A**: tick `defaultuser1`; stopwatch. Stop when App B shows it ticked.
2. **App B**: untick `defaultuser1`; stopwatch. Stop when App A shows it.
3. App B: tick `ee-gesperrt`; wait until both agree; then App A: untick it.

**Expected**:
- Each change appears in the other app within **5 s**, no action there.
- The clicking app keeps the new state; no error text.
- One log line per change, naming the device (*Papas PC* / *PC2*). <!-- verify against implementation report -->
- End: `defaultuser1` and `ee-gesperrt` unticked in both.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**: Seconds A→B: ______ · B→A: ______

---

### TC-003-34: An account change on the service PC appears in both apps

*Verifies AC-19 · PC2 + service PC / Admin, Terminal (Administrator) · both apps green*

1. `New-LocalUser -Name "ee-zwei" -NoPassword; Add-LocalGroupMember -Group (Get-LocalGroup -SID "S-1-5-32-545").Name -Member "ee-zwei"`; stopwatch. Watch both apps.
2. `Remove-LocalUser -Name "ee-zwei"`; stopwatch. Watch both apps.

**Expected**:
- Step 1: within **60 s** both apps show a new unticked row `ee-zwei`.
- Step 2: within **60 s** it disappears in both.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**: Seconds step 1 (A / B): ______ / ______ · step 2: ______ / ______

---

### TC-003-35: Changes at about the same time: the last one wins, both apps agree

*Verifies AC-24 · PC2 + service PC / Admin, log monitor · both green; `ee-lena` unticked in both*

1. **Simultaneous**: one hand on each mouse; click `ee-lena` in App A and App B **at the same moment**. Wait 5 s.
2. **Crossing**: App A untick `ee-lena`; within about 1 s click `ee-lena` in App B (whatever it shows). Wait 5 s.
3. Repeat step 2 with App B first, then App A.
4. After each step: look at both apps and the last two `ee-lena` lines in the log monitor (or LOG-FIND `ee-lena`).

**Expected**:
- After every step, at most **5 s** after the last click: **both apps show the same state** for `ee-lena`, equal to the **last** `ee-lena` log line.
- Each click that reached the service has its own log line (increasing revisions). <!-- verify against implementation report -->
- An error text in one app is a Note, if that app then shows the stored state.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**: Final state step 1: ______ · step 2: ______ · step 3: ______ · Log lines (device, yes/no, revision) for each step: ________

---

## Cleanup (test plan §7)

- [ ] **LOG-COPY** with `<subfolder>` = `run-01-service-logs` (Terminal (Administrator)). Looked through the copies (nothing that must not be published).
- [ ] Test accounts removed (Terminal (Administrator); leave out `defaultuser0` / `defaultuser1` if they existed before the run, S-10):
  ```powershell
  "ee-annika","ee-anna","ee-max","ee-lena","ee-gesperrt","ee-neu","ee-zwei","defaultuser0","defaultuser1","ee-admin" |
    ForEach-Object { if (Get-LocalUser -Name $_ -ErrorAction SilentlyContinue) { Remove-LocalUser -Name $_ } }
  ```
  Rows disappeared from App A within 60 s? [ ] yes  [ ] no
- [ ] Microsoft account from TC-003-15 removed, if not wanted (*Andere Benutzer* → *Entfernen* → *Konto und Daten löschen*).
- [ ] `eagleeye-kid` is a standard account (`Get-LocalGroupMember -SID "S-1-5-32-544"` does not list it) and signed out.
- [ ] PC2: App B stays installed and paired (or, if you prefer: *Kopplung aufheben*, uninstall, delete the installer copy).
- [ ] Display scaling back to your usual value, if changed (S-5).

---

## General Feedback

<!-- Anything that does not fit a test case: usability remarks, wanted text changes, ideas, surprises, clipped windows. -->

---

## Summary (filled in by TES after evaluation)

| Block | Cases | Pass | Fail | Blocked | Skipped | Not executed |
|---|---|---|---|---|---|---|
| A (TC-003-01..07) | 7 | | | | | |
| B (TC-003-08..15) | 8 | | | | | |
| C (TC-003-16..23) | 8 | | | | | |
| D (TC-003-24..30) | 7 | | | | | |
| E (TC-003-31..35) | 5 | | | | | |
| **Total** | **35** | | | | | |

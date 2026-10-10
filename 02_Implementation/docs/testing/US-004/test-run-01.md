# Test Run 01: US-004 — App Usage Tracking and Daily Usage Report

**Test plan**: `docs/testing/US-004/test-plan.md` (draft 2026-10-08, for approval)
**Prepared by**: TES, 2026-10-08
**Executed by**: Michael
**Execution date**: <!-- fill in -->
**Build / installer version**: 0.4.0 — `03_Delivery/windows/EagleEye-Setup-0.4.0.exe` (file date: ________) and `03_Delivery/windows/EagleEye-ParentApp-Setup-0.4.0.exe` (file date: ________) <!-- verify against implementation report: dates, SHA-256 -->
**Machine(s)**: Windows Developer Machine (service PC, `ZOCK-O-MAT-V3`) <!-- add Windows version --> · LEOSERV (App B) <!-- add Windows version / scaling -->

> **Start this run only after DEV's handover** (`02_Implementation/docs/requirements/user-stories/US-004/implementation-report.md` present) and after TES has aligned the places marked `verify against implementation report`.
>
> **What this run covers**: the 35 cases of the test plan, **only US-004 features**: no US-003 re-test, no regression (your decision, plan Q-5).
>
> **Order = importance.** Blocks **A to C are the core** (what is and is not recorded, stopwatch accuracy, the Reports page, live update). D and E follow; cases marked **(optional)** can be skipped. If you stop early, the core is covered. Optional cases: TC-004-21, -22, -29, -31 to -35.
>
> **How to record**: tick exactly one result box per case (`[x]`). **Observed** for Fail/Blocked. Screenshots into `docs/testing/US-004/evidence/` (e.g. `run01-tc10-report.png`), referenced in Notes. When done or stopping, tell TES: *"test run 01 for US-004 is done"*.
>
> **Service log files**: admin-only folder `%ProgramData%\EagleEye\logs\`. **LOG-COPY** (below) from an elevated terminal: **right after any Fail** → `<subfolder>` = `run-01-service-logs\after-TC-004-NN` (write it into the Notes); **at the end** (or end of each block) → `run-01-service-logs`. If there is no log file: *Ereignisanzeige → Anwendung*, source **EagleEye**, warning "The log folder … could not be restricted …" → Fail, screenshot, use the Event Log instead.
>
> **Duration**: Setup ≈ 35 min · A ≈ 20 min · B ≈ 75 min · C ≈ 30 min · D ≈ 25 min (+15 optional) · E ≈ 45 min (+5 optional) · F ≈ 10 min (+≈ 90 optional; TC-004-33 at midnight, TC-004-34 next day).
>
> **Accounts**: **Admin** = you. **kid1** = `eagleeye-kid` (controlled). **kid2** = `ee-kid2` (not controlled). **kid3** = `ee-kid3` (controlled; deleted in TC-004-30). **Apps**: **App A** = parent app in your admin session; **App B** = parent app on **LEOSERV** (for watching while the kid's session is in front).
> **Terminal (Administrator)** = elevated terminal in your admin session (title "Administrator:"; a normal one is not enough). **K-TERM** = elevated terminal **inside the kid's session**, started with your admin credentials (S-13).
>
> **Rules** (test plan §2):
> - **UI wording** differences are Notes. Pass criteria: **Berichte**, **Konto**, **App**, **Nutzung (HH:MM)**, **Heute keine Nutzung aufgezeichnet**. App names are the German Windows names (*Editor*, *Rechner*, *Task-Manager*, *Windows-Explorer* …); a host name like *Application Frame Host* instead of the app is a Fail.
> - **HH:MM**: stopwatch time rounded **down**, tolerance **±1 minute**. Measure by difference: **V0** before, **V1** after.
> - **Only the session in front counts.** When you switch to Admin, the kid's open apps stop counting. Expected.
> - **Timing**: live update ≤ **15 s**; account changes ≤ **60 s**. No limit in the story → slowness is a Note.
> - **150 %**: cut-off text in the Reports page is a Fail.

### Commands (elevated terminal)

**LOG-COPY** (repo root):

```powershell
Set-Location <your EagleEye repo folder>
$dst = Join-Path "02_Implementation\docs\testing\US-004\evidence" "<subfolder>"
New-Item -ItemType Directory -Force $dst | Out-Null
Copy-Item "$env:ProgramData\EagleEye\logs\*.log" $dst -Force
Get-ChildItem $dst
```

**LOG-WATCH** (after every service restart/reinstall/reboot: *Strg+C* and again):

```powershell
$log = Get-ChildItem "$env:ProgramData\EagleEye\logs\EagleEye.Service-*.log" | Sort-Object LastWriteTime | Select-Object -Last 1
Get-Content $log.FullName -Tail 15 -Wait
```

**LOG-FIND**:

```powershell
Select-String -Path "$env:ProgramData\EagleEye\logs\*.log" -Pattern "<pattern>" | Select-Object -Last 15 | ForEach-Object Line
```

**AGENTS**:

```powershell
Get-Process -Name EagleEye.Service -IncludeUserName | Format-Table Id, SessionId, UserName, StartTime -AutoSize
query user
```

---

## Setup — before Block A (service PC, Admin) · ≈ 35 min

- [x] **S-1 Installers present.** `Get-Item 03_Delivery\windows\EagleEye-Setup-0.4.0.exe, 03_Delivery\windows\EagleEye-ParentApp-Setup-0.4.0.exe | Select-Object Name, LastWriteTime`; `(Get-FileHash <file>).Hash` for each; compare with the implementation report. Dates in the header. <!-- verify against implementation report -->
- [x] **S-2 Installed state** (*Installierte Apps* → "EagleEye"; `services.msc`): EagleEye ______ · EagleEye Parent App ______ (expected **0.3.1 / 0.3.1**, service running).
  - Parent app missing → install `EagleEye-ParentApp-Setup-0.3.1.exe`, pair in S-3.
  - Service missing, another version, or already 0.4.0 → **stop** and tell TES.
- [x] **S-3 App A green**, *Gekoppelt*, **Papas PC** (else pair; code from the tray popup in the admin session).
- [ ] **S-4 Leftover US-003 accounts** removed (Terminal (Administrator)):
  ```powershell
  "ee-annika","ee-anna","ee-max","ee-lena","ee-gesperrt","ee-neu","ee-zwei","defaultuser0","defaultuser1","ee-admin" |
    ForEach-Object { if (Get-LocalUser -Name $_ -ErrorAction SilentlyContinue) { Remove-LocalUser -Name $_ } }
  ```
  Other standard accounts (stay unticked): ____________
- [x] **S-5 Admin scaling** ______ % (expected 150 %).
- [x] **S-6 Terminal (Administrator)** open. Service commands: `Stop-Service` / `Start-Service` / `Restart-Service -DisplayName "EagleEye Service"`.
- [x] **S-7 Log monitor**: second Terminal (Administrator) with LOG-WATCH.
- [x] **S-8** `hostname` → `<host>` = ____________
- [x] **S-9 New kid accounts** (Terminal (Administrator)):
  ```powershell
  $pw = Read-Host -AsSecureString "Password for ee-kid2 and ee-kid3"
  $usersGroup = (Get-LocalGroup -SID "S-1-5-32-545").Name
  New-LocalUser -Name "ee-kid2" -Password $pw -PasswordNeverExpires
  New-LocalUser -Name "ee-kid3" -Password $pw -PasswordNeverExpires
  "ee-kid2","ee-kid3" | ForEach-Object { Add-LocalGroupMember -Group $usersGroup -Member $_ }
  ```
- [x] **S-10 First sign-in** of `ee-kid2` and `ee-kid3` (*Benutzer wechseln*): desktop ready, scaling 150 %, **sign out**. Also sign out `eagleeye-kid` if signed in.
- [x] **S-11 Selection (0.3.1)**: App A → *Einstellungen*: **eagleeye-kid** and **ee-kid3** ticked; **ee-kid2** and all others unticked.
- [x] **S-12 SmartScreen**: *Weitere Informationen* → *Trotzdem ausführen* (expected).
- [x] **S-13 K-TERM understood**: in the kid's session, Start → **Terminal** → right-click → **Als Administrator ausführen** → enter **your admin** user name and password. It is not recorded for the kid; ignore it in reports and Task-Manager comparisons. Close it before signing the kid out.

---

## Block A — Upgrade and first look (core) · ≈ 20 min

### TC-004-01: Service update 0.3.1 → 0.4.0 keeps pairing and selection *(supporting)*

*Supporting · Service PC / Admin (+ Terminal (Administrator)) · 0.3.1 running, App A green*

1. Run `03_Delivery\windows\EagleEye-Setup-0.4.0.exe` with defaults. Leave App A open.
2. Tray → *App Infos* (start **EagleEye Tray** if no icon).
3. *Installierte Apps*; `services.msc`.
4. Watch App A up to 60 s; then *Einstellungen* → account section.
5. Log monitor: *Strg+C*, LOG-WATCH.

**Expected**:
- No error; *App Infos* **EagleEye_v0.4**; **EagleEye 0.4.0** once; service *Wird ausgeführt*.
- App A green within 60 s without a code; **eagleeye-kid** and **ee-kid3** ticked, **ee-kid2** unticked.
- Log: start lines, "Account inventory loaded: … 2 under parental control."; no Error. <!-- verify against implementation report -->

- **Result**: [X] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

### TC-004-02: Parent app 0.4.0: menu "Berichte" and the Reports page before any usage

*Verifies AC-17, AC-19, AC-20, AC-21 · Service PC / Admin · TC-004-01; no kid used an app since*

1. With App A open, run `EagleEye-ParentApp-Setup-0.4.0.exe` with defaults; start the app; check *Installierte Apps*.
2. Look at the menu; click **Berichte**.
3. Open **Konto**; read entries and order; close without change.
4. Look at the rest of the page.

**Expected**:
- **EagleEye Parent App 0.4.0**; green without a code.
- Menu: **Einstellungen** and **Berichte**.
- **Konto** lists exactly **eagleeye-kid** and **ee-kid3** (names/order as in *Einstellungen*), not `ee-kid2`, not your account; the **first** is selected.
- Heading **"Heute, <today's date>"** and **"Heute keine Nutzung aufgezeichnet"**; no table header, no older days.
- Nothing cut off at 150 %.

- **Result**: [X] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

### TC-004-03: The session agent runs only in the session of a controlled account

*Verifies AC-1, AC-27 · Service PC / kid1, then Admin (Terminal (Administrator)) · TC-004-02*

1. **AGENTS** (only the service, session 0).
2. *Benutzer wechseln* → `eagleeye-kid`; watch desktop, taskbar, notification area 30 s.
3. *Benutzer wechseln* → Admin; **AGENTS** again.
4. LOG-FIND `Session agent|Usage recording started`.

**Expected**:
- Step 1: one `EagleEye.Service`, SessionId **0**, `NT-AUTORITÄT\SYSTEM`.
- Step 2: nothing new for the kid (no window, taskbar button or tray message).
- Step 3: a **second** `EagleEye.Service`, user SYSTEM, **SessionId of eagleeye-kid**; **none** in your session.
- Step 4: "Session agent started in session N (process P)" and "Usage recording started for account eagleeye-kid (…) in session N.". <!-- verify against implementation report -->

- **Result**: [X] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**: Session IDs: kid1 ____ · Admin ____

---

## Block B — What is recorded and how much (core) · ≈ 75 min

- [x] **S-14 App B on LEOSERV**: install `EagleEye-ParentApp-Setup-0.4.0.exe` on LEOSERV; start; green and *Gekoppelt* → done; else *Kopplung aufheben* (if offered), connect to `<host>`, pair as **LEOSERV** (code from the tray popup in your admin session). App B → **Berichte** → **Konto** `eagleeye-kid`. LEOSERV scaling ____ %. Not available today? → write it here; TC-004-05 is Blocked.
- [x] Switch to `eagleeye-kid`; open **K-TERM** and run LOG-WATCH in it.

*Read the values in App B. Without LEOSERV, read them in App A after switching to Admin (switching stops the counting).*

### TC-004-04: Accuracy: Notepad 10 minutes, with 3 minutes locked in between

*Verifies AC-24, AC-12, AC-10, AC-7 · Service PC / kid1, K-TERM; App B · Editor not open; V0 = Editor today (or "—")*

1. Start **Editor**; **start the stopwatch** when its window appears.
2. At **5:00**: **Windows+L**, **pause** the stopwatch, wait **3 minutes** (clock).
3. Unlock; **resume** the stopwatch.
4. At **10:00**: close Editor (do not save).
5. Wait 15 s. Read **V1**. LOG-FIND `Editor`.

**Expected**:
- **V1 − V0 = 10 min**, e.g. "00:10" ("00:09" to "00:11"). "00:13" = Fail (locked time counted).
- Log: "New app for account eagleeye-kid: Editor (Notepad.exe, <path>)" (first use), "App started …", "App ended … duration 00:13:xx (closed)" (instance continues while locked; usage 10). <!-- verify against implementation report -->

- **Result**: [X] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**: V0 ______ · V1 ______ · Duration in the log ______

---

### TC-004-05: The open Reports page updates by itself within 15 s

*Verifies AC-22, AC-13 · Service PC / kid1; LEOSERV / App B next to it · App B on Berichte → eagleeye-kid; Paint not used today. No LEOSERV → Blocked*

1. kid1: start **Paint**; stopwatch when its window appears. Do not touch App B.
2. Note when the row **Paint** appears in App B.
3. Keep Paint open, session unlocked. Note when **Paint** shows "00:03".
4. Leave Paint open.

**Expected**:
- Row **Paint** at most **15 s** after Paint's window appeared, no action in App B.
- "00:03" at the latest at stopwatch **3:15**.
- No click or reload needed.

- **Result**: [X] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**: Row appeared after ____ s · "00:03" at stopwatch ____

---

### TC-004-06: The listed apps are recorded under their own names

*Verifies AC-3, AC-8 · Service PC / kid1; App B · Paint open*

1. kid1: start and keep open **≥ 1 min** each: **Microsoft Edge**, **Rechner**, **Snipping Tool**, **Task-Manager** (Strg+Umschalt+Esc), **one game or desktop program** (name in Notes).
2. Task-Manager (kid1) → *Prozesse* → group **Apps**.
3. Today's table in App B; LOG-FIND `New app for account eagleeye-kid`.

**Expected**:
- One row each: **Paint**, **Microsoft Edge**, **Rechner**, **Snipping Tool**, **Task-Manager**, your program — with the names of Task-Manager's *Apps* group.
- **Rechner**, not "Application Frame Host".
- One "New app" line per program with process name (e.g. `CalculatorApp.exe`) and full path.
- Leave them open.

- **Result**: [X] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**: Program chosen: ____________ · its row name: ____________

---

### TC-004-07: File Explorer counts only while one of its windows is open

*Verifies AC-4 · Service PC / kid1; App B · no File Explorer window open*

1. Use desktop, Start menu, taskbar **2 min** without opening a File Explorer window.
2. **Windows+E**; stopwatch; keep it open **2 min**.
3. Close it; wait 1 min.

**Expected**:
- Step 1: **no** row "Windows-Explorer".
- Step 2: row **"Windows-Explorer"** within 15 s, reaching "00:02" (±1).
- Step 3: the value stops growing.

- **Result**: [X] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

### TC-004-08: Background, Windows and tray-only processes are not recorded

*Verifies AC-5 · Service PC / kid1; App B · several apps open; OneDrive icon in kid1's notification area (if not set up: note it)*

1. Task-Manager (kid1) → *Prozesse*: screenshot of **Hintergrundprozesse** / **Windows-Prozesse** → `evidence/run01-tc08-taskmanager.png`.
2. EagleEye tray → *App Infos*; close after 30 s.
3. Compare App B's table with Task-Manager's *Apps* group.

**Expected**:
- Never in the report: Runtime Broker, Suche/SearchHost, Shell Experience Host, CTF-Ladeprogramm, Desktopfenster-Manager, Konsolenfenster-Host, Diensthost, **EagleEye** (also not while *App Infos* was open), **OneDrive**.
- Every row is (or was) an *Apps* entry.
- A *Hintergrundprozesse* program in the report: note name and whether it had a visible window (TES evaluates).

- **Result**: [X] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**: OneDrive running for kid1? [ ] yes  [ ] no

---

### TC-004-09: One app with several processes or windows counts once; minimised and parallel apps count

*Verifies AC-6, AC-12 · Service PC / kid1; App B · Edge and Paint open; V0 of Microsoft Edge, Paint, Editor*

1. Edge: **second window** (Strg+N), some tabs. Start **Editor** and a **second Editor window** (Strg+Umschalt+N).
2. **Minimise** Paint. Stopwatch **3 min**, unlocked.
3. Read V1 of the three. Optional: VS Code with two windows meanwhile, if installed.

**Expected**:
- **One** row each for Microsoft Edge and Editor.
- Edge, Editor and minimised Paint each **+3 min** (±1), not +6.
- Close the second windows and Editor afterwards.

- **Result**: [X] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**: Edge ____ → ____ · Editor ____ → ____ · Paint ____ → ____

---

### TC-004-10: Today's table: headers, sorting, "00:00"

*Verifies AC-18, AC-19 · Service PC / kid1, then Admin (App A) · several apps with usage today*

1. kid1: Windows+R → `charmap` (**Zeichentabelle**); close it after ~**30 s**.
2. Close all kid1 apps. *Benutzer wechseln* → Admin. App A → *Berichte* → `eagleeye-kid`. Screenshot → `evidence/run01-tc10-report.png`.
3. *Darstellung*: other mode (Hell ↔ Dunkel); look at *Berichte*; switch back.

**Expected**:
- **"Heute, <date>"**, table with bold headers **App** | **Nutzung (HH:MM)**; durations `HH:MM`. <!-- verify against implementation report: duration column alignment -->
- Sorted **longest first**; equal values by name.
- **Zeichentabelle** with **"00:00"**.
- Readable in light and dark; nothing cut off at 150 %. No older day.

- **Result**: [X] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

### TC-004-11: Apps of an uncontrolled account and of the parent are not recorded

*Verifies AC-1 · Service PC / kid2, Admin; Terminal (Administrator) · ee-kid2 unticked*

1. *Benutzer wechseln* → `ee-kid2`; Editor and Paint **2 min**; leave them open.
2. *Benutzer wechseln* → Admin; Editor 1 min in your session; close.
3. **AGENTS**; LOG-FIND `ee-kid2`; LOG-FIND `account <your admin user name>`.
4. App A → *Berichte* → open **Konto**.

**Expected**:
- **No** agent in ee-kid2's or your session.
- **No** log line names `ee-kid2` or your account.
- `ee-kid2` not in **Konto**; eagleeye-kid's values unchanged.
- Leave `ee-kid2` signed in with Editor open (TC-004-16).

- **Result**: [X] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

### TC-004-12: Two controlled accounts using the same app are kept apart

*Verifies AC-26, AC-7 · Service PC / kid3, Admin · K1 = eagleeye-kid's Editor value (App A)*

1. *Benutzer wechseln* → `ee-kid3`; **Editor** with stopwatch **2 min**; close it.
2. *Benutzer wechseln* → Admin; *Berichte*: **Konto** `ee-kid3`, then `eagleeye-kid`.
3. LOG-FIND `New app for account ee-kid3`.

**Expected**:
- `ee-kid3`: **Editor** "00:02" (±1), nothing else.
- `eagleeye-kid`: **Editor** still **K1**.
- Log: "New app for account ee-kid3: Editor (Notepad.exe, …)".
- Sign `ee-kid3` out.

- **Result**: [X] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**: K1 ______

---

### TC-004-13: The kid notices nothing

*Verifies AC-27 · observation over Block B*

1. Think back over Block B in kid1's session.

**Expected**:
- No app closed, blocked or noticeably slowed down.
- No new tray message, popup or icon change; no EagleEye window. (The SYSTEM agent in Task-Manager → *Details* is accepted, plan Q-2.)

- **Result**: [X] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

## Block C — Reports page states and account changes (core) · ≈ 30 min

*Service PC · Admin; App A on Berichte; log monitor running.*

### TC-004-14: Not connected, then connected again

*Verifies AC-20, AC-21 · Service PC / Admin, Terminal (Administrator) · Berichte → eagleeye-kid; screenshot of the values*

1. `Stop-Service -DisplayName "EagleEye Service"`; watch until red.
2. `Start-Service -DisplayName "EagleEye Service"`; do not touch the app; watch closely.

**Expected**:
- Step 1: **"Keine Daten verfügbar"**, **no** *Konto*, no days, no tables.
- Step 2: green within 60 s; **Konto** and the same days and values as before, without any action.

- **Result**: [X] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**: "Wird geladen …" visible? [ ] yes  [ ] no

---

### TC-004-15: Selecting another account shows that account's usage

*Verifies AC-21 · Service PC / Admin · TC-004-14*

1. **Konto** → `ee-kid3`; then → `eagleeye-kid`.

**Expected**:
- `ee-kid3`: only **Editor**. `eagleeye-kid`: its Block B apps. Each at once (after at most a short "Wird geladen …").

- **Result**: [X] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

### TC-004-16: Ticking starts recording, unticking stops it; the selection follows; data is kept

*Verifies AC-2, AC-23, AC-9 · Service PC / Admin, kid2; log monitor · ee-kid2 signed in (background) with Editor open*

1. *Einstellungen*: **tick** `ee-kid2`; stopwatch; watch the log monitor.
2. *Berichte* → open **Konto**.
3. *Benutzer wechseln* → `ee-kid2`; Editor **2 min**; back to Admin; *Berichte* → `ee-kid2`.
4. Keep `ee-kid2` selected; *Einstellungen*: **untick** `ee-kid2`; stopwatch; log monitor, then *Berichte*.
5. Tick `ee-kid2` again; *Berichte* → `ee-kid2`.
6. Untick `ee-kid2` again; sign `ee-kid2` out.

**Expected**:
- Step 1: within **60 s** "Usage recording started for account ee-kid2 … in session N." and "App started: account ee-kid2, Editor …" for the already open Editor.
- Step 2: `ee-kid2` in **Konto** within 60 s.
- Step 3: **Editor** "00:02" (±1).
- Step 4: within **60 s** "App ended: account ee-kid2, Editor … (monitoring stopped)" and "Usage recording stopped for account ee-kid2 …: monitoring stopped."; `ee-kid2` leaves **Konto**; the **first** account is selected.
- Step 5: `ee-kid2` back with its Editor value (data kept).
- *Optional*: admin ↔ standard (*Kontotyp ändern*) instead of untick: recording stops within 60 s.

- **Result**: [X] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**: Seconds: start ____ · stop ____

---

### TC-004-17: No account under parental control

*Verifies AC-20, AC-23 · Service PC / Admin · eagleeye-kid and ee-kid3 ticked*

1. *Einstellungen*: untick `eagleeye-kid` and `ee-kid3`; *Berichte*.
2. Tick both again; *Berichte*.

**Expected**:
- Step 1: within 60 s **"Keine Konten unter Elternkontrolle. Konten unter Einstellungen auswählen."**, no *Konto*.
- Step 2: **Konto** with both, the first selected, usage unchanged.

- **Result**: [X] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

## Block D — Sessions and service lifecycle · ≈ 25 min core (+15 optional)

*kid1's session with K-TERM + LOG-WATCH; App B on Berichte → eagleeye-kid (or App A afterwards).*

### TC-004-18: After "Benutzer wechseln" the kid's open apps stop counting

*Verifies AC-12 · Service PC / kid1, Admin · V0 of Editor*

1. kid1: Editor; stopwatch; after **2 min** *Benutzer wechseln* → Admin (Editor stays open); pause the stopwatch.
2. Stay **3 min** in the admin session; App A → *Berichte*: Editor.
3. Back to `eagleeye-kid`; resume; after **1 min** close Editor; read V1.

**Expected**:
- Step 2: V0 + **2** (±1), not growing in the admin session.
- Step 3: V1 − V0 = **3** (±1), not 6.

- **Result**: [X] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**: Re-install check from TC-004-20 (no new "New app … Editor" line)? [ ] yes  [ ] not checked

---

### TC-004-19: Service stop and start while an app is open

*Verifies AC-15, AC-10 · Service PC / kid1, K-TERM · Paint open; V0 of Paint*

1. After **1 min**: K-TERM `Stop-Service -DisplayName "EagleEye Service"`; pause the stopwatch.
2. Wait **2 min** (keep using Paint).
3. `Start-Service -DisplayName "EagleEye Service"`; resume; after **2 min** close Paint.
4. LOG-WATCH again; LOG-FIND `Paint`; read V1.

**Expected**:
- At the stop: "App ended: account eagleeye-kid, Paint … (service stopping)".
- After the start: "Usage recording started …" and a **new** "App started: … Paint …".
- V1 − V0 = **3** (±1): the 2 min without service not counted.
- App B "Keine Daten verfügbar" while stopped, back by itself.

- **Result**: [X] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

### TC-004-20: Re-install of 0.4.0 keeps all recorded data

*Verifies AC-9, AC-15 · Service PC / Admin · screenshot of Berichte → eagleeye-kid*

1. Run `EagleEye-Setup-0.4.0.exe` again with defaults.
2. App A green; *Berichte* → `eagleeye-kid`, `ee-kid3`. LOG-WATCH again.

**Expected**:
- No errors; green without a code.
- Same days, apps and values for both accounts.
- (Next Editor use: no new "New app … Editor" line; noted in TC-004-18 or the next case.)

- **Result**: [X] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

### TC-004-21 (optional): Unexpected service stop

*Verifies AC-15, AC-10 · Service PC / kid1, K-TERM · Editor open ≥ 1 min*

1. Clock time (seconds): ________. K-TERM: `Stop-Process -Name EagleEye.Service -Force`.
2. After 30 s: `Get-Service -DisplayName "EagleEye Service"`; if not running: `Start-Service …`.
3. LOG-WATCH; LOG-FIND `stopped unexpectedly`.

**Expected**:
- "App ended: … Editor … (service stopped unexpectedly)", end at most **5 s** before step 1.
- New "App started" for the open Editor.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [X] Skipped
- **Observed**:
- **Notes**: Restarted by Windows itself? [ ] yes  [ ] no

---

### TC-004-22 (optional): Sleep is not counted

*Verifies AC-12, AC-15 · Service PC / kid1 · Editor open; V0*

1. Stopwatch 1 min; Start → *Ein/Aus* → **Energie sparen**; pause.
2. After **3 min** wake, unlock; resume; after 1 min close Editor; V1.
3. LOG-FIND `paused for`.

**Expected**:
- V1 − V0 = **2** (±1).
- *Note only*: "Usage accounting paused for … s …".

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [X] Skipped
- **Observed**:
- **Notes**:

---

## Block E — Security checks (core security, one optional) · ≈ 45 min (+5 optional)

### TC-004-23: The agent runs with the restricted SYSTEM token

*Security (T-5) · Service PC / Admin, Terminal (Administrator) · kid1 signed in*

1. LOG-FIND `Session agent in session`.

**Expected**:
- "Session agent in session N: user SYSTEM, integrity System, privileges SeChangeNotifyPrivilege, write-restricted yes" (or the documented fallback "no"). <!-- verify against implementation report -->
- **SYSTEM**, **System**, only **SeChangeNotifyPrivilege**.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**: write-restricted: [ ] yes  [ ] no

---

### TC-004-24: The kid cannot end the agent

*Security (T-4) · Service PC / kid1, normal PowerShell (not K-TERM)*

1. ```powershell
   $s = (Get-Process -Id $PID).SessionId
   $agent = Get-Process -Name EagleEye.Service | Where-Object SessionId -eq $s
   $agent | Select-Object Id, SessionId
   Stop-Process -Id $agent.Id -Force
   taskkill /F /PID $agent.Id
   ```
2. Task-Manager (kid1) → *Details* → `EagleEye.Service.exe` (SYSTEM) → *Task beenden*.
3. First three lines of step 1 again.

**Expected**:
- `Stop-Process` and `taskkill`: **Zugriff verweigert**. Task-Manager: *Zugriff verweigert*.
- Step 3: agent still running, **same Id**.

- **Result**: [ X] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**: Agent Id ______

---

### TC-004-25: A program from a network share is recorded by its process name; the file is not opened

*Security (T-2), AC-8 · Service PC / Admin, then kid1*

1. Terminal (Administrator):
   ```powershell
   New-Item -ItemType Directory -Force C:\EETest | Out-Null
   Copy-Item C:\Windows\System32\charmap.exe C:\EETest\charmap.exe
   $everyone = (New-Object System.Security.Principal.SecurityIdentifier "S-1-1-0").Translate([System.Security.Principal.NTAccount]).Value
   New-SmbShare -Name "eetest" -Path "C:\EETest" -ReadAccess $everyone
   ```
2. kid1: Windows+R → `\\localhost\eetest\charmap.exe` (security prompt → *Ausführen*); **1 min**; close.
3. LOG-FIND `eetest`; LOG-FIND `WRN|ERR`; report.

**Expected**:
- Row **"charmap"**, not "Zeichentabelle".
- "New app for account eagleeye-kid: charmap (charmap.exe, \\localhost\eetest\charmap.exe)" (or another UNC form). <!-- verify against implementation report -->
- No Warning/Error about reading the share program.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [X] Skipped
- **Observed**:
- **Notes**:

---

### TC-004-26: A renamed copy named like EagleEye is recorded

*Security (T-10), AC-5 · Service PC / kid1*

1. Normal PowerShell as kid1:
   ```powershell
   $dst = Join-Path ([Environment]::GetFolderPath("Desktop")) "EagleEye.TrayClient.exe"
   Copy-Item C:\Windows\System32\charmap.exe $dst
   Start-Process $dst
   ```
2. **1 min**; close. K-TERM: LOG-FIND `EagleEye.TrayClient.exe`; report.

**Expected**:
- It **is** recorded: report row and log "New app / App started … (EagleEye.TrayClient.exe, C:\Users\eagleeye-kid\…\Desktop\EagleEye.TrayClient.exe)".
- Name: "Zeichentabelle", "Character Map" or "EagleEye.TrayClient". <!-- verify against implementation report -->

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ X] Skipped
- **Observed**:
- **Notes**: Row name: ____________

---

### TC-004-27: Opening and closing Notepad 40 times gives at most 30 log lines plus a summary

*Security (T-12) · Service PC / kid1, K-TERM · start at the beginning of a clock hour H; no other Editor use in that hour*

1. Hour H = ____. Normal PowerShell as kid1 (~8 min):
   ```powershell
   1..40 | ForEach-Object { Start-Process notepad; Start-Sleep -Seconds 4; Stop-Process -Name Notepad -ErrorAction SilentlyContinue; Start-Sleep -Seconds 7 }
   ```
2. K-TERM (replace `15` by H):
   ```powershell
   $lines = Select-String -Path "$env:ProgramData\EagleEye\logs\*.log" -Pattern "App (started|ended): account eagleeye-kid, Editor" | Where-Object { $_.Line -match "^$(Get-Date -Format yyyy-MM-dd) 15:" }
   $lines.Count
   Select-String -Path "$env:ProgramData\EagleEye\logs\*.log" -Pattern "Editor" | Select-Object -Last 5 | ForEach-Object Line
   ```
3. Report: Editor today.

**Expected**:
- `$lines.Count` **≤ 30** (80 without the limit).
- One **summary line** for Editor in that hour. <!-- verify against implementation report: summary text, per clock hour? -->
- Editor grew plausibly (~3 min).

- **Result**: [X] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**: Count ____ · Summary line: ____________

---

### TC-004-28: Debug switches have no effect in the installed (Release) service

*Security (T-13) · Service PC / Admin, Terminal (Administrator)*

1. ```powershell
   $svc = (Get-Service -DisplayName "EagleEye Service").Name
   $sid = ([Security.Principal.WindowsIdentity]::GetCurrent()).User.Value
   New-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Services\$svc" -Name Environment -PropertyType MultiString -Value @("EAGLEEYE_DEV_WATCH_SID=$sid", "EAGLEEYE_DATA_DIR=C:\EETest\data") -Force
   Restart-Service -DisplayName "EagleEye Service"
   ```
2. Your session: Editor **1 min**; close; wait 15 s.
3. **AGENTS**; `Test-Path C:\EETest\data`; LOG-WATCH again (new lines in `%ProgramData%\EagleEye\logs`); LOG-FIND `account <your admin user name>`; App A green, *Berichte* unchanged?
4. ```powershell
   Remove-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Services\$svc" -Name Environment
   Restart-Service -DisplayName "EagleEye Service"
   ```

**Expected**:
- No agent in your session; no log line names your account.
- `Test-Path` **False**; log continues in `%ProgramData%\EagleEye\logs`; App A paired with the same data.
- After step 4 as before.

- **Result**: [X] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

### TC-004-29 (optional): An agent ended by the administrator is restarted

*Security (T-9) · Service PC / Admin, Terminal (Administrator) · kid1 signed in (background)*

1. **AGENTS**: agent Id in kid1's session ____; `Stop-Process -Id <Id> -Force`; time ____.
2. After 30 s: **AGENTS**; LOG-FIND `Session agent`.

**Expected**:
- New agent (other Id) within **30 s**.
- Warning "Session agent in session N exited with code …; restarting in …" and a new "Session agent started …".

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [X] Skipped
- **Observed**:
- **Notes**:

---

## Block F — Account deletion (core) and long checks (optional) · ≈ 10 min (+≈ 90 optional)

### TC-004-30: Deleting an account purges all its recorded data

*Verifies AC-9, AC-23 · Service PC / Admin, Terminal (Administrator) · ee-kid3 signed out, ticked, with usage; Berichte → Konto ee-kid3*

1. ```powershell
   Get-CimInstance Win32_UserProfile | Where-Object LocalPath -like "*\ee-kid3" | Remove-CimInstance
   Remove-LocalUser -Name "ee-kid3"
   ```
   Stopwatch.
2. Watch App A and the log monitor.
3. *Optional*: re-create `ee-kid3` (S-9 lines for it), tick it, look at its report.

**Expected**:
- Within **60 s**: `ee-kid3` leaves **Konto**; the first account is selected.
- Log: "Purged all recorded data of deleted account S-1-5-21-…: 1 apps, … history entries, … daily entries." <!-- verify against implementation report -->
- Step 3: new `ee-kid3` → "Heute keine Nutzung aufgezeichnet".

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [X] Skipped
- **Observed**:
- **Notes**: Seconds ____

---

### TC-004-31 (optional): A clock change neither adds nor removes usage

*Verifies AC-16 · Service PC / kid1, K-TERM · not after 22:30; Editor open; V0*

1. Stopwatch; after 1 min K-TERM `Set-Date -Date (Get-Date).AddHours(1)`.
2. After 2 min `Set-Date -Date (Get-Date).AddHours(-1)`; after 1 min close Editor; V1.
3. `w32tm /resync` (or *Datum und Uhrzeit → Jetzt synchronisieren*); clock correct.

**Expected**:
- V1 − V0 = **4** (±1); no ±60 min jump, nothing negative.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [X] Skipped
- **Observed**:
- **Notes**:

---

### TC-004-32 (optional): Older days and the 90-day limit (needs a SQLite tool)

*Verifies AC-11, AC-18, AC-19 · Service PC / Admin, Terminal (Administrator) · `sqlite3.exe` available (else Skipped); near the end of the run*

1. `Stop-Service -DisplayName "EagleEye Service"`; `Copy-Item "$env:ProgramData\EagleEye\EagleEye.Service.db" C:\EETest\db-backup.db`.
2. ```powershell
   $db = "$env:ProgramData\EagleEye\EagleEye.Service.db"
   sqlite3 $db "UPDATE DailyUsage SET Day = date('now','localtime','-1 day')  WHERE Day = date('now','localtime') AND AppId = (SELECT AppId FROM AppRecords WHERE DisplayName = 'Paint' LIMIT 1);"
   sqlite3 $db "UPDATE DailyUsage SET Day = date('now','localtime','-89 day') WHERE Day = date('now','localtime') AND AppId = (SELECT AppId FROM AppRecords WHERE DisplayName = 'Microsoft Edge' LIMIT 1);"
   sqlite3 $db "UPDATE DailyUsage SET Day = date('now','localtime','-91 day') WHERE Day = date('now','localtime') AND AppId = (SELECT AppId FROM AppRecords WHERE DisplayName = 'Rechner' LIMIT 1);"
   sqlite3 $db "SELECT r.DisplayName, d.Day, d.Seconds FROM DailyUsage d JOIN AppRecords r ON r.AppId = d.AppId ORDER BY d.Day;"
   ```
   <!-- verify against implementation report: table/column names -->
3. `Start-Service …`; App A → *Berichte* → `eagleeye-kid`; LOG-FIND `Purged usage data older`.

**Expected**:
- **"Heute, …"** (without Paint, Edge, Rechner), then **yesterday** with Paint, then the date **89 days ago** with Microsoft Edge; nothing for 91 days ago; no empty days in between.
- Log: "Purged usage data older than …: 1 daily entries, …".
- Restore if wanted: stop, copy the backup back, start.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [X] Skipped
- **Observed**:
- **Notes**:

---

### TC-004-33 (optional, at midnight): Usage across midnight is split; the history is not

*Verifies AC-14 · Service PC / kid1; App B or App A*

1. **23:55** start Editor (kid1); unlocked until **00:05**; close.
2. Report (yesterday and today); LOG-FIND `Editor`.

**Expected**:
- Yesterday's Editor +~5 min; today's Editor ~"00:05" (±1 each).
- One start and one end line, duration ~00:10:xx.
- The page moves to the new "Heute, …" by itself.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [X] Skipped
- **Observed**:
- **Notes**:

---

### TC-004-34 (optional, next day): Yesterday becomes an older day

*Verifies AC-18, AC-19 · Service PC / Admin · the day after the run, before kid1 uses anything*

1. App A → *Berichte* → `eagleeye-kid`.

**Expected**:
- **"Heute, <today>"** with "Heute keine Nutzung aufgezeichnet"; below it the run day's date (without "Heute") with its table.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [X] Skipped
- **Observed**:
- **Notes**:

---

### TC-004-35 (optional): CPU load with 10 apps

*Verifies AC-25 · Service PC / kid1*

1. Open **10** apps (Editor, Paint, Edge with a video, Rechner, Snipping Tool, Explorer, Zeichentabelle, Task-Manager, your game, one more).
2. Task-Manager → *Details*: the **two** `EagleEye.Service.exe` for **1 min**, CPU about every 10 s.
3. Game/video 1 min; K-TERM `Stop-Service …`; 1 min again; `Start-Service …`.

**Expected**:
- Sum of both **< 2 %** on average (expected < 1 %).
- No visible stutter difference.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [X] Skipped
- **Observed**:
- **Notes**: CPU values: ____________

---

## Cleanup (test plan §7)

- [x] **LOG-COPY** → `run-01-service-logs`; copies looked through.
- [x] Share removed: `Remove-SmbShare -Name "eetest" -Force`; `Remove-Item C:\EETest -Recurse -Force`.
- [x] `EagleEye.TrayClient.exe` copy deleted from kid1's desktop.
- [x] Service `Environment` value gone (TC-004-28).
- [x] Clock automatic and correct (TC-004-31).
- [x] `eagleeye-kid`, `ee-kid2` signed out; `ee-kid2` (and a re-created `ee-kid3`) removed:
  ```powershell
  foreach ($n in "ee-kid2","ee-kid3") {
    Get-CimInstance Win32_UserProfile | Where-Object LocalPath -like "*\$n" | Remove-CimInstance
    if (Get-LocalUser -Name $n -ErrorAction SilentlyContinue) { Remove-LocalUser -Name $n }
  }
  ```
- [x] `eagleeye-kid` stays ticked (or untick, your choice).
- [x] LEOSERV: App B stays installed and paired.

---

## General Feedback

Functionality is good, seems to be stable and expected. I like to do some tweaks on the usability and visual style. See screenshots in  evidence witth prefix "visuals_"

## Summary (filled in by TES after evaluation)

| Block | Cases | Pass | Fail | Blocked | Skipped | Not executed |
|---|---|---|---|---|---|---|
| A (TC-004-01..03) core | 3 | | | | | |
| B (TC-004-04..13) core | 10 | | | | | |
| C (TC-004-14..17) core | 4 | | | | | |
| D (TC-004-18..20 core, 21..22 optional) | 5 | | | | | |
| E (TC-004-23..28 core, 29 optional) | 7 | | | | | |
| F (TC-004-30 core, 31..35 optional) | 6 | | | | | |
| **Total** (27 core, 8 optional) | **35** | | | | | |

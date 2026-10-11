# Test Run 01: US-005 — Break Times (Ruhezeiten)

**Test plan**: `docs/testing/US-005/test-plan.md` (draft 2026-10-11, for approval)
**Prepared by**: TES, 2026-10-11
**Executed by**: Michael
**Execution date**: <!-- fill in -->
**Build / installer version**: 0.5.0 — `03_Delivery/windows/EagleEye-Setup-0.5.0.exe` (2026-10-10 22:47, SHA-256 `83bf5c55…151830`) and `03_Delivery/windows/EagleEye-ParentApp-Setup-0.5.0.exe` (2026-10-10 22:48, SHA-256 `a03245d5…b217a1`); full hashes in test plan §2
**Machine(s)**: Windows Developer Machine (service PC, `ZOCK-O-MAT-V3`) · LEOSERV (App B)

> **What this run covers**: the 35 cases of the test plan, **only US-005 features**, no regression.
>
> **Order = importance.** Blocks **A to C are the core** (Regeln page, blocking at start, graceful/force with timing, the kid's dialog, other accounts). D and E follow. Cases marked **(optional)**: TC-005-29, -31 to -35. If you stop after Block C, the core is covered.
>
> **How to record**: tick exactly one result box per case (`[x]`); that is what counts. **Observed** only for Fail/Blocked. The Notes blanks (timings) help the evaluation but are optional. Screenshots into `docs/testing/US-005/evidence/`. When done or stopping, tell TES: *"test run 01 for US-005 is done"*.
>
> **Service log files**: **LOG-COPY** (below) from an elevated terminal: **right after any Fail** → `<subfolder>` = `run-01-service-logs\after-TC-005-NN` (write it into Notes); **at the end** → `run-01-service-logs`.
>
> **Duration**: Setup ≈ 30 min (+15 if kid accounts must be created) · A ≈ 40 min · B ≈ 45 min · C ≈ 30 min · D ≈ 45 min · E ≈ 25 min (+10 optional) · F ≈ 10 min (+≈ 60 optional). Core blocks ≈ 3 h 15 min plus setup.
>
> **Accounts**: **Admin** = you. **kid1** = `eagleeye-kid` (controlled). **kid2** = `ee-kid2` (**not** controlled). **kid3** = `ee-kid3` (controlled; deleted in TC-005-30). **App A** = parent app in your admin session; **App B** = parent app on **LEOSERV** (to switch breaks while the kid's session is in front).
> **Terminal (Administrator)** = elevated terminal in your session. **K-TERM** = elevated terminal **inside the kid's session** with your admin credentials. **P-KID** = **normal** PowerShell of the kid.
>
> **Break helpers** (test plan §2):
> - **E1** = entry of `eagleeye-kid`: *Regeln* → **Eintrag hinzufügen** → **Start-Zeit** = **now − 1 min** (e.g. `14:09` at 14:10) → Enter. End 23:59, all days.
> - **BREAK ON** = tick **An/Aus** of E1; **BREAK OFF** = untick it. Wait until it is confirmed (stays, no message), then **5 s** before the kid starts anything.
> - During a break the kid **cannot** start Terminal, Task-Manager or Settings. Open P-KID and K-TERM while the break is off. Do not start Blocks B to E after ≈ 23:00.
>
> **Rules**:
> - **UI wording** differences are Notes. Expected German texts: test plan §2 table (menu **Einstellungen · Regeln · Berichte**; **Konto**, **Ruhezeiten**, **Eintrag hinzufügen**, **An/Aus**, **Start-Zeit**, **End-Zeit**, **MO … SO**, **Anzeige Text:**, the messages). **But** the display text in the kid's dialog must be **exactly** the stored text (lines, emojis); a difference is a Fail.
> - **Timing**: dialog ≤ **10 s** after Enter; app gone ≤ **30 s** after Enter; force = gone **20 s** (±2) after the "save?" prompt appeared; App B follows App A ≤ **5 s**; rule change effective ≤ **5 s**; accounts and purges ≤ **60 s**. Up to 1 s over a limit = Note (reaction time).
> - **Measuring**: stopwatch **start on Enter** in *Ausführen* (Windows+R); **lap 1** = dialog appears; **lap 2** = app window gone.
> - **Test apps** (Windows+R): `notepad` (Editor), `mspaint` (Paint), `charmap` (Zeichentabelle), `calc` (Rechner), `osk` (Bildschirmtastatur).

### Commands (elevated terminal)

**LOG-COPY** (repo root):

```powershell
Set-Location <your EagleEye repo folder>
$dst = Join-Path "02_Implementation\docs\testing\US-005\evidence" "<subfolder>"
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

---

## Setup — before Block A (service PC, Admin) · ≈ 30 min

- [x] **S-1 Installers**: `Get-FileHash 03_Delivery\windows\EagleEye-Setup-0.5.0.exe, 03_Delivery\windows\EagleEye-ParentApp-Setup-0.5.0.exe | Format-List Path, Hash` → hashes as in the header / test plan §2.
- [x] **S-2 Installed state** (*Installierte Apps*; `services.msc`): EagleEye ______ · EagleEye Parent App ______ (expected **0.4.0 or 0.4.1**, service running). Already 0.5.0 or no service → **stop**, tell TES.
- [x] **S-3 App A green**, *Gekoppelt*, **Papas PC**.
- [x] **S-4 Accounts**: `eagleeye-kid` exists. `ee-kid2` / `ee-kid3` missing → create (Terminal (Administrator)):
  ```powershell
  $pw = Read-Host -AsSecureString "Password for the new kid accounts"
  $usersGroup = (Get-LocalGroup -SID "S-1-5-32-545").Name
  foreach ($n in "ee-kid2","ee-kid3") {
    if (-not (Get-LocalUser -Name $n -ErrorAction SilentlyContinue)) {
      New-LocalUser -Name $n -Password $pw -PasswordNeverExpires | Out-Null
      Add-LocalGroupMember -Group $usersGroup -Member $n
    }
  }
  ```
  New accounts: sign in once (*Benutzer wechseln*), scaling 150 %, **sign out**. Sign out `eagleeye-kid` too.
- [x] **S-5 Admin scaling** ______ % (expected 150 %).
- [x] **S-6 Terminal (Administrator)** open. **S-7** second one with LOG-WATCH. **S-8** `hostname` → ____________
- [x] **S-9 Selection**: App A → *Einstellungen*: **eagleeye-kid** and **ee-kid3** ticked; **ee-kid2** and all others unticked.
- [x] **S-10 SmartScreen**: *Weitere Informationen* → *Trotzdem ausführen* (expected).
- [x] **S-11 K-TERM understood** (Start → **Terminal** → right-click → **Als Administrator ausführen** → your admin credentials, in the kid's session).
- [x] **S-12 App B on LEOSERV** (after TC-005-02): install `EagleEye-ParentApp-Setup-0.5.0.exe`; green and *Gekoppelt* (else pair as **LEOSERV**). Not available → write it here: ____________ (TC-005-23 Blocked).
- [x] **S-13 SQLite tool** for optional TC-005-34: yes / no ______

---

## Block A — Upgrade and the Regeln page (core) · ≈ 40 min

### TC-005-01: Service update 0.4.x → 0.5.0 keeps pairing and selection *(supporting)*

*Supporting · Service PC / Admin · App A (0.4.x) open and green*

1. Run `03_Delivery\windows\EagleEye-Setup-0.5.0.exe` with defaults. Leave App A open.
2. Tray → *App Infos*; *Installierte Apps*; `services.msc`.
3. Watch App A up to 60 s. Log monitor: *Strg+C*, LOG-WATCH; LOG-FIND `Break times loaded`.

**Expected**:
- *App Infos* **EagleEye_v0.5**; **EagleEye 0.5.0** once; service running.
- App A green within 60 s without a code.
- Log "Break times loaded: 0 entries (0 on) of … accounts, 0 changed display texts."; no Error.

- **Result**: [X] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

### TC-005-02: Parent app 0.5.0: menu, start page and the empty Regeln page

*Verifies AC-1, AC-2, AC-3, AC-15 · Service PC / Admin*

1. Close App A. Install `EagleEye-ParentApp-Setup-0.5.0.exe`; start it. Which page does it open on?
2. Menu; click **Regeln**. Open **Konto**, close without change. Read the page top to bottom.

**Expected**:
- **Parent App 0.5.0**, green without a code; opens on **Berichte**. Menu: **Einstellungen, Regeln, Berichte**.
- Top to bottom: title **Regeln**; **Konto** with **eagleeye-kid** and **ee-kid3**, the first selected; accent bar **Ruhezeiten**; button **Eintrag hinzufügen**; **Keine Ruhezeiten festgelegt.**; **Anzeige Text:** with the **default text** ("Hi! Leider haben Deine Eltern eine PC-Pause … ein Buch zu lesen? 😊"), wrapped.

- **Result**: [X] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

### TC-005-03: "Eintrag hinzufügen" adds a row with the defaults, saved at once

*Verifies AC-7, AC-12, AC-3 · Service PC / Admin; log monitor · Konto eagleeye-kid, no entries*

1. Click **Eintrag hinzufügen**. Watch the log.
2. Click it again.

**Expected**:
- Row: trash icon, **An/Aus ☐**, **20:00**, **23:59**, **MO … SO all ☑**; headers **An/Aus · Start-Zeit · End-Zeit · MO … SO**.
- Log within 5 s: "Break times of account eagleeye-kid (…) changed by Papas PC: entry N added (off, 20:00–23:59, Mo Tu We Th Fr Sa Su) …".
- The second row appears **below** the first.

- **Result**: [X] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

### TC-005-04: Editing in place: checkboxes at the click, times on Enter or leaving the field

*Verifies AC-8, AC-12, AC-14, AC-3 · Service PC / Admin; log monitor · two rows; row 1 = upper*

1. Row 1: tick **An/Aus**, then untick it.
2. Row 1 **Start-Zeit**: `2130`, **Tab**. **End-Zeit**: `22.15`, **Enter**.
3. Row 1: untick **SA** and **SO**.

**Expected**:
- One log line per change within 5 s ("entry N changed: on, …", "… off, …", "… off, 21:30–23:59, …", "… off, 21:30–22:15, …", "… Mo Tu We Th Fr Sa", "… Mo Tu We Th Fr"). No Save button anywhere.
- Fields show **21:30** and **22:15**. Row 1 is off and still editable.

- **Result**: [X] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

### TC-005-05: Invalid times, end not after start, last weekday: nothing saved, message shown

*Verifies AC-9, AC-10, AC-11 · Service PC / Admin; log monitor · row 1 = off, 21:30–22:15, MO–FR*

1. Row 1 **Start-Zeit**: `24:00` Enter · `12:60` Enter · `abc` Enter · empty, Tab.
2. Row 1 **End-Zeit**: `21:00` Enter · `21:30` Enter.
3. Row 1 **Start-Zeit**: `23:00` Enter.
4. Row 1: untick MO, DI, MI, DO; then try to untick **FR**.
5. Tick **MO** again.

**Expected**:
- Step 1: field back to **21:30** each time; **Bitte eine Uhrzeit zwischen 00:00 und 23:59 eingeben.**; no log line.
- Steps 2, 3: field back (22:15 / 21:30); **Die End-Zeit muss nach der Start-Zeit liegen.**; no log line.
- Step 4: MO to DO saved; **FR stays ticked**; **Mindestens ein Tag muss ausgewählt sein.**
- Step 5: saved; the message disappears.

- **Result**: [X] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

### TC-005-06: The trash icon deletes at once, without confirmation

*Verifies AC-13, AC-12 · Service PC / Admin; log monitor · two rows*

1. Click the trash icon of **row 2**.
2. *Optional (≈ 3 min)*: add rows up to **20**; look at **Eintrag hinzufügen**; delete them down to row 1.

**Expected**:
- Row 2 gone at once, **no** confirmation; log "entry N deleted" within 5 s; row 1 unchanged.
- Optional: at 20 rows the button is disabled.

- **Result**: [X] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

### TC-005-07: Display text: edit, line break, emojis, empty text

*Verifies AC-15, AC-16, AC-12 · Service PC / Admin; log monitor · Konto eagleeye-kid*

1. Display text: select all; type `Pause! Test 1 ` + **😊** (Windows + .); **Enter**; type `Lies ein Buch ` + paste **📚 👍**. Click outside the box.
2. **Berichte**, then **Regeln** again.
3. Delete the whole text; click outside.
4. Enter the text of step 1 again; click outside (it stays for the run).
5. *Optional*: paste a text > 500 characters.

**Expected**:
- Step 1: two lines with emojis; log "display text changed".
- Step 2: same text shown again.
- Step 3: the **default text** is back; log "display text reset to the default".
- Optional: the box stops at about 500 characters.

- **Result**: [X ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

### TC-005-08: Each account has its own entries and text; switching the account switches both

*Verifies AC-5, AC-20, AC-15 · Service PC / Admin; log monitor*

1. **Konto** → `ee-kid3`.
2. **Eintrag hinzufügen** (leave off). Display text: select all, type `Text für ee-kid3 ` + **🙂**, then **directly** switch **Konto** → `eagleeye-kid`.
3. **Konto** → `ee-kid3`.

**Expected**:
- Step 1: **Keine Ruhezeiten festgelegt.** and the **default text**.
- Step 2: `eagleeye-kid` shows its own row and two-line text; log "… ee-kid3 …: entry N added …" and "… ee-kid3 …: display text changed".
- Step 3: `ee-kid3`: its one entry and "Text für ee-kid3 🙂".

- **Result**: [X ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

### TC-005-09: Page states; data kept over app restart and service restart; no crash

*Verifies AC-6, AC-5, AC-17, AC-1 · Service PC / Admin, Terminal (Administrator) · Regeln, Konto ee-kid3 selected*

1. `Stop-Service -DisplayName "EagleEye Service"`; watch until red.
2. `Start-Service -DisplayName "EagleEye Service"`; don't touch the app.
3. Close the app (X), start it again; **Regeln**; both accounts.
4. *Einstellungen*: untick both accounts; **Regeln**. Tick both again; **Regeln**.

**Expected**:
- Step 1: **no crash**; **Keine Daten verfügbar**; no Konto, table or text box.
- Step 2: green ≤ 60 s; **Konto** with the **first** account (`eagleeye-kid`, expected after a reconnect) and its stored entries and text.
- Step 3: opens on **Berichte**; same entries and texts.
- Step 4: **Keine Konten unter Elternkontrolle. Konten unter Einstellungen auswählen.** (≤ 60 s); after ticking, both accounts with unchanged entries and texts.

- **Result**: [ X] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**: "Wird geladen …" seen? ____

---

### TC-005-10: Light and dark mode, 150 %

*Verifies AC-4 · Service PC / Admin · Regeln → eagleeye-kid*

1. Screenshot → `evidence/run01-tc10-regeln-<mode>.png`.
2. *Einstellungen → Darstellung*: other mode; **Regeln**; screenshot; switch back.

**Expected**:
- Both modes: texts, checkboxes, time fields and **trash icon** clearly visible; normal app colours plus the accent bar **Ruhezeiten**.
- 150 %: nothing cut off; the nine checkbox columns fit.

- **Result**: [X ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

## Block B — Blocking at start and the kid's dialog (core) · ≈ 45 min

- [x] **P-1** App B (LEOSERV) → **Regeln** → **Konto** `eagleeye-kid` → create **E1** (now − 1 min to 23:59, all days), **An/Aus unticked**. (No LEOSERV: in App A.)
- [x] **P-2** *Benutzer wechseln* → `eagleeye-kid`.
- [x] **P-3** *Einstellungen → Personalisierung → Taskleiste → Verhalten der Taskleiste* → **Sekunden in der Uhr der Taskleiste anzeigen** ✓.
- [x] **P-4** **P-KID**: Start → **Terminal** (normal). Keep it open.
- [x] **P-5** **K-TERM** with LOG-WATCH.
- [x] **P-6** P-KID: `Get-Process notepad, mspaint, charmap -ErrorAction SilentlyContinue` → nothing.
- [x] **P-7** **Edge**: open any page (a video is fine), **maximise**. Leave it open.

### TC-005-11: Outside a break apps start as before (and an entry that is off never blocks)

*Verifies AC-24, AC-29, AC-14 · Service PC / kid1, K-TERM; App B · E1 off*

1. Windows+R `notepad`; 30 s; close. Windows+R `mspaint`; 30 s; close.
2. K-TERM: LOG-FIND `Blocked start`. App B → **Berichte** → `eagleeye-kid`: note **Editor** = **ED0**; back to **Regeln**.

**Expected**:
- Both open as quickly as before and stay open.
- No "Blocked start" line from this run.

- **Result**: [ X] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**: ED0 ______

---

### TC-005-12: In a break, Notepad is blocked: dialog within 10 s, closed gracefully, gone within 30 s

*Verifies AC-21, AC-22, AC-26, AC-28, AC-30; security Q-8 (SYSTEM token) · Service PC / kid1, K-TERM; App B · Edge maximised in front; no Notepad open*

1. App B: **BREAK ON**. When confirmed, count **5 s**.
2. Windows+R `notepad`; **stopwatch on Enter**; **lap 1** = dialog; **lap 2** = Notepad gone.
3. **OK**.
4. K-TERM: LOG-FIND `Blocked start|Break time of account|entry .* changed`.

**Expected**:
- Dialog **EagleEye** with kid1's two-line text, **in front of the maximised Edge**, **≤ 10 s** (expect 1–3 s). Notepad gone **≤ 30 s** (expect ≤ 5 s).
- Log: "entry N changed: on, …"; "Blocked start: account eagleeye-kid, Editor (notepad.exe, …) … closing the app."; "Blocked start ended: … **closed gracefully** after 0.x s; **message shown**."
- **Q-8**: "terminated by force after 20 s" for Notepad = **Fail**.

- **Result**: [X ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**: Lap 1 ____ s · Lap 2 ____ s · "closed gracefully after ____ s"

---

### TC-005-13: The dialog: topmost over full-screen, OK/Enter only, exact text

*Verifies AC-30, AC-31, AC-16 · Service PC / kid1 · BREAK ON; Edge in front*

1. Edge: **F11** (full screen).
2. Windows+R `notepad`. When the dialog appears, type a letter at once (focus? Note only).
3. Read title, text, buttons; compare the text with TC-005-07.
4. Click into the dialog's text. **Esc**. **Alt+F4**.
5. Click on Edge behind it and scroll. Wait **60 s**.
6. Click into the dialog's text, press **Enter**. Edge: **F11** again.

**Expected**:
- Dialog **in front of full-screen Edge**.
- Title **EagleEye**; text **exactly** "Pause! Test 1 😊" / "Lies ein Buch 📚 👍" (two lines, emojis recognisable, monochrome OK); one button **OK**; **no** close box.
- Esc and Alt+F4: dialog stays. Edge usable behind it; dialog stays in front, does not close by itself.
- Enter closes it.

- **Result**: [ X] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**: Focus at once? yes / no / only after Notepad was gone

---

### TC-005-14: Only one dialog at a time

*Verifies AC-32, AC-22, AC-28 · Service PC / kid1, K-TERM · BREAK ON; no dialog open*

1. Windows+R `notepad`. Leave the dialog open.
2. Windows+R `mspaint`. Wait 30 s.
3. **OK** (mouse).
4. Windows+R `mspaint`. **OK**.
5. LOG-FIND `Blocked start ended`.

**Expected**:
- Step 2: Paint closed (≤ 30 s), **no** second dialog.
- Step 4: a **new** dialog; OK closes it.
- Log: step 2 "… **not shown (a message is already open)**"; step 4 "… message shown".

- **Result**: [X ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

### TC-005-15: A blocked start counts no usage; an app running before the break keeps counting

*Verifies AC-24, AC-25, AC-36 · Service PC / kid1, K-TERM; App B → Berichte → eagleeye-kid · BREAK ON*

1. App B: note **Zeichentabelle** today (expected: no row; else **Z0**), **Microsoft Edge** **EG0**, **Editor** (compare with ED0).
2. Windows+R `charmap`. **OK**.
3. Use Edge for **3 minutes** (stopwatch).
4. Read App B. K-TERM: LOG-FIND `Zeichentabelle|charmap`.

**Expected**:
- **No** "Zeichentabelle" row (or still Z0); **Editor** still ED0.
- **Microsoft Edge** = EG0 + 3 (±1), never closed.
- Log: only "Blocked start …" for charmap, **no** "App started: … Zeichentabelle" ("New app …" = Note only).

- **Result**: [X ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**: EG0 ______ · after ______

---

### TC-005-16: An app that does not close by itself is terminated by force 20 s later (Paint with an unsaved drawing)

*Verifies AC-22 (force, all processes, caught afterwards), AC-25, AC-27, AC-28; security: termination across sessions; OQ-17 · Service PC / kid1, K-TERM, P-KID · BREAK ON; no Paint, no dialog open*

1. K-TERM: `Stop-Service -DisplayName "EagleEye Service"`; `Get-Service -DisplayName "EagleEye Service"` → *Stopped*.
2. Windows+R `mspaint`; draw a line; **do not save**.
3. K-TERM: `Start-Service -DisplayName "EagleEye Service"`; LOG-WATCH again.
4. When Paint shows its **"save?" prompt**: **start the stopwatch**. **Do not answer it.**
5. Within ≈ 5 s: Windows+R `mspaint` (second Paint).
6. **Stop** when the last Paint window is gone.
7. P-KID: `Get-Process mspaint -ErrorAction SilentlyContinue`. K-TERM: LOG-FIND `Paint|could not`.

**Expected**:
- "save?" prompt within ≈ 10 s after the service start. EagleEye dialog: shown, or not (tray not yet reconnected) — both fine.
- Paint gone **20 s** (±2) after the prompt; the second Paint gone at the latest with it; **no** second dialog.
- No `mspaint` left. Log: **one** "Blocked start: … Paint (mspaint.exe, …)" and "Blocked start ended: … **terminated by force after 20 s** (2x.x s until gone); …". **No** "could not be terminated", no Error.
- **Edge** still open after the service restart.

- **Result**: [X ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**: Stopwatch ____ s · log "____ s until gone" · dialog shown? yes / no

---

### TC-005-17: Switching the entry off lets apps start again within 5 s

*Verifies AC-26, AC-14 · Service PC / kid1; App B · BREAK ON*

1. App B: **BREAK OFF**; when confirmed, count 5 s.
2. Windows+R `notepad`; 30 s; close.
3. App B: **BREAK ON** again.

**Expected**:
- Notepad opens normally and stays; no dialog; log "entry N changed: off, …", no "Blocked start" for it.

- **Result**: [ X] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

## Block C — Other accounts and exemptions (core) · ≈ 30 min

### TC-005-18: kid1's break does not affect other accounts; the dialog appears only in kid1's session

*Verifies AC-34, AC-20, AC-23 · Service PC / kid1, ee-kid3, ee-kid2, Admin · BREAK ON; kid1 in front*

1. kid1: Windows+R `notepad`. **Leave the dialog open.**
2. *Benutzer wechseln* → `ee-kid3`: `notepad`, `mspaint`; 30 s; leave them open.
3. → `ee-kid2`: same; then sign `ee-kid2` out.
4. → Admin: Editor; close. LOG-FIND `Blocked start: account`.
5. → `eagleeye-kid`: dialog still there? **OK**.

**Expected**:
- Steps 2–4: all apps open normally; **no** dialog in those sessions.
- Log: "Blocked start" only for **eagleeye-kid**.
- Step 5: kid1's dialog still there.

- **Result**: [ X] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

### TC-005-19: ee-kid3's own break and own text; unticked = kept but not applied

*Verifies AC-20, AC-31, AC-21 · Service PC / Admin (App A), ee-kid3 · ee-kid3 signed in*

1. App A → **Regeln** → `ee-kid3`: its entry **Start-Zeit** = now − 1 min, Enter; tick **An/Aus**.
2. → `ee-kid3`: Notepad/Paint still open? Windows+R `charmap`; read the dialog; **OK**.
3. Admin: *Einstellungen* untick **ee-kid3**; **Regeln** → **Konto**.
4. `ee-kid3`: `charmap`; close after 10 s.
5. Admin: tick **ee-kid3**; **Regeln** → `ee-kid3`.
6. `ee-kid3`: `charmap`; **OK**.
7. Admin: untick **An/Aus** of ee-kid3's entry. Sign `ee-kid3` out.

**Expected**:
- Step 2: Notepad/Paint still open; charmap blocked; dialog shows **"Text für ee-kid3 🙂"**.
- Step 3: `ee-kid3` gone from **Konto** (≤ 60 s). Step 4: charmap opens normally.
- Step 5: `ee-kid3` back with its entry (on) and text. Step 6: blocked again.

- **Result**: [ X] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

### TC-005-20: During a break the kid can still use the shell, lock and sign out; Task-Manager and Settings are blocked

*Verifies AC-23, AC-30 · Service PC / kid1 · BREAK ON*

1. Start menu open/close. Windows+E; keep 30 s; close.
2. Tray icon → *App Infos*; close.
3. Windows+L; unlock.
4. **Strg+Umschalt+Esc**; **OK**. **Windows+I**; **OK**. Windows+R `osk`; **OK**.
5. Windows+R `notepad`; **leave the dialog open**; Start → user icon → **Abmelden**.

**Expected**:
- Steps 1–3 work; Explorer and *App Infos* **not** closed; tray icon stays.
- Step 4: Task-Manager, Einstellungen, Bildschirmtastatur closed (≤ 30 s) with the dialog.
- Step 5: sign-out works with the dialog open.

- **Result**: [X ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

## Block D — Boundaries, tray, two parent apps, re-install (core) · ≈ 45 min

- [x] App B: **BREAK OFF**. Sign in as `eagleeye-kid`; open **P-KID**, **K-TERM** + LOG-WATCH, **Edge**.

### TC-005-21: Start minute included, end minute excluded; weekday; overlapping entries; running app keeps running

*Verifies AC-21, AC-25, OQ-6 · Service PC / kid1 (seconds in the clock); App B · E1 off; **T** = current minute + 3*

1. App B: **Eintrag hinzufügen** → **E2**: Start **T**, End **T + 2 min**; tick **An/Aus**.
2. At **T − 0:15**: `notepad`; keep it open until step 5.
3. At **T + 0:05**: `charmap`; **OK**.
4. At **T + 1:45**: `mspaint`; **OK**.
5. At **T + 2:05**: `mspaint`; close after 10 s. Close Notepad.
6. App B: E1 untick **today's** day; **BREAK ON**; 5 s; `charmap`; close. Tick today again; 5 s; `charmap`; **OK**.
7. App B: E2 End `23:59` Enter, then Start = now − 1 min Enter. **BREAK OFF**; 5 s; `charmap`; **OK**. Untick E2's **An/Aus**; 5 s; `charmap`; close.

**Expected**:
- Step 2: Notepad opens and is **never** closed. Steps 3, 4: blocked. Step 5: Paint opens normally.
- Step 6: opens while today is unticked; blocked when ticked again.
- Step 7: blocked while only E2 is on; opens normally when both are off.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [X] Skipped
- **Observed**:
- **Notes**: T = ______

---

### TC-005-22: Tray client not running: the app is closed all the same, no message; the restarted tray is verified

*Verifies AC-33, AC-23, AC-28; security: tray identity · Service PC / kid1, P-KID, K-TERM · BREAK ON; no dialog open*

1. P-KID: `Stop-Process -Name EagleEye.TrayClient` (tray icon disappears).
2. `notepad`; watch 60 s.
3. Start → **EagleEye Tray**; 10 s.
4. `notepad`; **OK**.
5. K-TERM: LOG-FIND `Tray client connected|message|not shown`.

**Expected**:
- Step 2: Notepad closed (≤ 30 s), **no** dialog, also not later. Step 3: tray icon back. Step 4: dialog again.
- Log: step 2 "**not shown (tray client not connected)**"; "Tray client connected: … (session N, **verified**)."; step 4 "message shown".

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [X] Skipped
- **Observed**:
- **Notes**:

---

### TC-005-23: Two parent apps: changes appear in the other app within 5 s; deleted meanwhile → message

*Verifies AC-19, AC-18, AC-5 · Service PC / Admin (App A) + LEOSERV (App B) side by side · both Regeln → eagleeye-kid. No LEOSERV → Blocked*

1. A: **Eintrag hinzufügen**. Watch B.
2. B: new row **End-Zeit** `22:00`, Enter. Watch A.
3. A: untick **MO** in that row. Watch B.
4. Together (count down): A **Start-Zeit** `21:00`, B `21:30`; Enter at the same time; wait 5 s.
5. B: that row's **Start-Zeit**, type `2015`, **no Enter**. A: trash icon of that row. B: **Enter**.

**Expected**:
- Steps 1–3: the change appears in the other app **≤ 5 s**.
- Step 4: after 5 s both show the **same** start time.
- Step 5: B: **Die Änderung konnte nicht gespeichert werden. Bitte erneut versuchen.**; the row disappears in B (≤ 5 s).

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [X] Skipped
- **Observed**:
- **Notes**: Row already gone in B before Enter? yes / no

---

### TC-005-24: A changed display text is used for the next block within 5 s

*Verifies AC-31, AC-16 · Service PC / kid1; App B, later App A · BREAK ON; kid1 in front*

1. App B → `eagleeye-kid`: at the end of the text, Enter, `Test 3 ` + paste **👍**; click outside; count 5 s.
2. kid1: `notepad`; read the dialog; **OK**.
3. Later (admin session): App A → Regeln → `eagleeye-kid`.

**Expected**:
- Dialog: **three** lines, the third "Test 3 👍". App A shows the same three lines.

- **Result**: [X ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

### TC-005-25: Re-install keeps entries and text; enforcement without any parent app

*Verifies AC-17, AC-27 · Service PC / Admin, kid1 · BREAK ON; screenshot of Regeln (both accounts) in App A*

1. **Close** App A and App B.
2. Run `03_Delivery\windows\EagleEye-Setup-0.5.0.exe` again.
3. → `eagleeye-kid`: `notepad`; **OK**.
4. → Admin: App A → **Regeln** (both accounts). LOG-WATCH again; LOG-FIND `Break times loaded`.

**Expected**:
- Step 3: Notepad blocked although no parent app runs.
- Step 4: same entries and texts as in the screenshot; log "Break times loaded: … of 2 accounts, 2 changed display texts."

- **Result**: [X ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

## Block E — Security (core security) · ≈ 25 min (+10 optional)

- [x] App B on LEOSERV started again → Regeln → `eagleeye-kid`. kid1 in front with P-KID and K-TERM.

### TC-005-26: Which restricted SYSTEM token the agent runs with (US-004 TC-004-23, rerun)

*Security (Q-8) · Service PC / Admin or K-TERM · kid1 signed in*

1. LOG-FIND `Session agent|write restriction|write-restricted`.

**Expected**: <!-- verify against implementation report: diagnostic line taken from the US-004 plan; not in the US-005 report -->
- "Session agent in session N: diagnostics: user NT-AUTORITÄT\SYSTEM (S-1-5-18), integrity System, privileges SeChangeNotifyPrivilege, write-restricted yes|no".
- SYSTEM, integrity System, **only** `SeChangeNotifyPrivilege`; write-restricted **yes**, or the accepted fallback Warning plus `no`. Both Pass. **Write the variant in Notes.**

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [X ] Skipped
- **Observed**:
- **Notes**: write-restricted: yes / no (fallback)

---

### TC-005-27: The kid cannot end the enforcement

*Security · Service PC / kid1, **P-KID** (not K-TERM) · BREAK ON*

1. P-KID:
   ```powershell
   Stop-Service -DisplayName "EagleEye Service"
   Get-Process -Name EagleEye.Service | Stop-Process -Force
   $n = (Get-Service -DisplayName "EagleEye Service").Name; sc.exe config $n start= disabled
   Get-Service -DisplayName "EagleEye Service"
   ```
2. `notepad`; **OK**.

**Expected**:
- Each command fails (*Zugriff verweigert* / `[SC] OpenService FEHLER 5`); service still *Running*.
- Notepad blocked as before.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ X] Skipped
- **Observed**:
- **Notes**:

---

### TC-005-28: The kid changes the time zone: Warning within 5 s; break times follow the new local time

*Verifies AC-37 · Service PC / kid1, P-KID, K-TERM · BREAK ON; first set E1's **Start-Zeit** = now − 1 min in App B*

1. P-KID: `tzutil /g` → ____________
2. P-KID: `tzutil /s "UTC"`; note the time; wait 10 s.
3. `notepad`; close after 10 s.
4. K-TERM: LOG-FIND `time zone|clock of the PC`.
5. P-KID: `tzutil /s "W. Europe Standard Time"` (value of step 1); 10 s; `notepad`; **OK**.

**Expected**:
- Step 3: Notepad opens normally.
- Step 4: **Warning** "The time zone of the PC changed from W. Europe Standard Time (UTC+01:00) to UTC (UTC+00:00); break times now use the new local time. Session in use: eagleeye-kid." ≤ 5 s after step 2.
- Step 5: second Warning; Notepad blocked again.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ X] Skipped
- **Observed**:
- **Notes**: `tzutil /s` refused for the kid? yes / no

---

### TC-005-29 (optional): A copied tray client is not trusted

*Security · Service PC / kid1, P-KID, K-TERM · E1 off*

1. P-KID:
   ```powershell
   $src = Split-Path (Get-Process -Name EagleEye.TrayClient).Path
   $dst = Join-Path $env:USERPROFILE "EETray"
   Copy-Item $src $dst -Recurse
   Stop-Process -Name EagleEye.TrayClient
   Start-Process (Join-Path $dst "EagleEye.TrayClient.exe")
   ```
2. App B: **BREAK ON**; 5 s; `notepad`; watch 60 s.
3. K-TERM: LOG-FIND `Tray client connected|not shown`.
4. P-KID: `Stop-Process -Name EagleEye.TrayClient; Remove-Item $dst -Recurse -Force`; Start → **EagleEye Tray**.

**Expected**:
- Log "Tray client connected: … (**unverified**)."; Notepad closed, **no** dialog; "not shown (tray client not connected)".

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ X] Skipped
- **Observed**:
- **Notes**:

---

## Block F — Account deletion and optional checks · ≈ 10 min (+≈ 60 optional)

### TC-005-30: Deleting an account purges its rules and history; a new account of the same name starts empty

*Verifies AC-20, AC-36 · Service PC / Admin, Terminal (Administrator) · ee-kid3 signed out; App A → Regeln → ee-kid3*

1. *Optional*: `Rename-LocalUser -Name ee-kid3 -NewName ee-kid3x` → Regeln shows **ee-kid3x** with the same entry/text (≤ 60 s); `Rename-LocalUser -Name ee-kid3x -NewName ee-kid3`.
2. ```powershell
   Get-CimInstance Win32_UserProfile | Where-Object LocalPath -like "*\ee-kid3" | Remove-CimInstance
   Remove-LocalUser -Name "ee-kid3"
   ```
   Watch App A and the log 60 s.
3. Create `ee-kid3` again (S-4 commands); tick it; **Regeln** → `ee-kid3`.

**Expected**:
- Step 2: `ee-kid3` gone from **Konto** ≤ 60 s; log "Purged the break times of deleted account S-1-5-21-…" and "Purged enforcement records of deleted accounts: …".
- Step 3: **Keine Ruhezeiten festgelegt.** and the **default text**.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ X] Skipped
- **Observed**:
- **Notes**:

---

### TC-005-31 (optional): An app started from another app is blocked too

*Verifies AC-22 · Service PC / kid1, P-KID · BREAK ON*

1. P-KID: `Start-Process notepad`; stopwatch from Enter.

**Expected**: dialog ≤ 10 s; Notepad gone ≤ 30 s; log "closed gracefully"; P-KID stays open.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ X] Skipped
- **Observed**:
- **Notes**:

---

### TC-005-32 (optional): A Store app (Rechner) is blocked; other Store apps stay

*Verifies AC-22 · Service PC / kid1, K-TERM · BREAK OFF → open **Uhr** (Clock) → BREAK ON*

1. Windows+R `calc`; stopwatch; laps (dialog, Rechner gone); **OK**.
2. LOG-FIND `Rechner`.

**Expected**: dialog ≤ 10 s; Rechner gone ≤ **30 s** from Enter; outcome graceful or force (both OK, write which); **Uhr** stays open.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [X ] Skipped
- **Observed**:
- **Notes**: Lap 1 ____ s · Lap 2 ____ s · outcome ________

---

### TC-005-33 (optional): Edge is blocked with all its processes

*Verifies AC-22 · Service PC / kid1, P-KID · BREAK OFF; close Edge; P-KID `Get-Process msedge -ErrorAction SilentlyContinue | Stop-Process -Force`; BREAK ON*

1. Start **Microsoft Edge** (taskbar); stopwatch; **OK**.
2. 30 s after the start: P-KID `Get-Process msedge -ErrorAction SilentlyContinue`.

**Expected**: Edge blocked (dialog ≤ 10 s, gone ≤ 30 s); **no** `msedge` process left.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ X] Skipped
- **Observed**:
- **Notes**:

---

### TC-005-34 (optional, needs a SQLite tool): History of blocked starts and time-zone findings in the database

*Verifies AC-35, AC-36, AC-37 · Service PC / Admin, Terminal (Administrator) · after TC-005-28 and -30*

1. ```powershell
   $tmp = "C:\EETest\db"; New-Item -ItemType Directory -Force $tmp | Out-Null
   Copy-Item "$env:ProgramData\EagleEye\EagleEye.Service.db*" $tmp -Force
   sqlite3 -readonly "$tmp\EagleEye.Service.db" "SELECT StartedLocal, UserName, DisplayName, ProcessName, Trigger, EntryStartMinute, EntryEndMinute, Weekday, Outcome, SecondsUntilGone, MessageState FROM BlockedStarts ORDER BY BlockedStartId;"
   sqlite3 -readonly "$tmp\EagleEye.Service.db" "SELECT DetectedLocal, Kind, OldValue, NewValue, SessionId, UserName FROM TimeChangeFindings;"
   ```

**Expected**:
- One `BlockedStarts` row per blocked start (also before the re-install); data as in the log; Paint of TC-005-16: "terminated by force", ≈ 20 s, trigger "found at agent start"; **no** `ee-kid3` row.
- `TimeChangeFindings`: two `TimeZone` rows of TC-005-28, `UserName` eagleeye-kid.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ X] Skipped
- **Observed**:
- **Notes**:

---

### TC-005-35 (optional): Reboot during a break

*Verifies AC-23, AC-17, AC-27 · Service PC / kid1, Admin · BREAK ON; kid1 in front*

1. kid1: Start → *Ein/Aus* → **Neu starten**.
2. Sign in as `eagleeye-kid` (no parent app running); 30 s; `notepad`; **OK**.
3. Admin: App A → Regeln → `eagleeye-kid`.

**Expected**: restart works; Notepad blocked; entries and text unchanged.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [X ] Skipped
- **Observed**:
- **Notes**:

---

## Cleanup (test plan §7)

- [x] **LOG-COPY** → `run-01-service-logs`; copies looked through.
- [x] All break entries of `eagleeye-kid` **off** (or deleted) — otherwise the kid stays blocked.
- [x] Time zone back: `tzutil /g` → `W. Europe Standard Time`.
- [x] Tray copy `C:\Users\eagleeye-kid\EETray` deleted (TC-005-29); real tray running.
- [x] `C:\EETest` removed (TC-005-34).
- [x] `eagleeye-kid` signed out; `ee-kid2` and `ee-kid3` removed:
  ```powershell
  foreach ($n in "ee-kid2","ee-kid3") {
    Get-CimInstance Win32_UserProfile | Where-Object LocalPath -like "*\$n" | Remove-CimInstance
    if (Get-LocalUser -Name $n -ErrorAction SilentlyContinue) { Remove-LocalUser -Name $n }
  }
  ```
- [x] LEOSERV: App B stays installed and paired.

---

## General Feedback

Seems to work fine.

---

## Summary (filled in by TES after evaluation)

| Block | Cases | Pass | Fail | Blocked | Skipped | Not executed |
|---|---|---|---|---|---|---|
| A (TC-005-01..10) core | 10 | | | | | |
| B (TC-005-11..17) core | 7 | | | | | |
| C (TC-005-18..20) core | 3 | | | | | |
| D (TC-005-21..25) core | 5 | | | | | |
| E (TC-005-26..28 core, 29 optional) | 4 | | | | | |
| F (TC-005-30 core, 31..35 optional) | 6 | | | | | |
| **Total** (29 core, 6 optional) | **35** | | | | | |

# Test Run 01: US-006 — App Time Budgets and Time-elapsed Warnings

**Test plan**: `docs/testing/US-006/test-plan.md` (draft 2026-10-17, for approval)
**Prepared by**: TES, 2026-10-17
**Executed by**: Michael
**Execution date**: <!-- fill in -->
**Build / installer version**: 0.6.0 — `03_Delivery/windows/EagleEye-Setup-0.6.0.exe` (2026-10-12 13:48, SHA-256 `416E0A74…E03950`) and `03_Delivery/windows/EagleEye-ParentApp-Setup-0.6.0.exe` (2026-10-12 13:49, SHA-256 `56500A6A…ED34E5`); full hashes in test plan §2
**Machine(s)**: Windows Developer Machine (service PC, `ZOCK-O-MAT-V3`) · LEOSERV (App B)

> **What this run covers**: the 42 cases of the test plan, **only US-006 features**, no regression. It is the **first live run of enforcement against running apps** (DEV could not run it, report D-15).
>
> **Must-run vs. optional**: **32 must-run**, 10 **(optional)**: TC-006-11, -21, -22, -27, -28, -38 to -42. Shorter run: stop after Block D (core enforcement covered); Blocks E and F hold the remaining must-run ACs.
>
> **How to record**: tick exactly one result box per case (`[x]`); that is what counts. **Observed** only for Fail/Blocked. The Notes blanks (lap times) help the evaluation but are optional. Screenshots into `docs/testing/US-006/evidence/`. When done or stopping, tell TES: *"test run 01 for US-006 is done"*.
>
> **Service log files**: **LOG-COPY** (below) from an elevated terminal: **right after any Fail** → `<subfolder>` = `run-01-service-logs\after-TC-006-NN` (write it into Notes); **at the end** → `run-01-service-logs`.
>
> **Duration**: Setup ≈ 40 min · A ≈ 50 min · B ≈ 30 min · C ≈ 20 min (+10 optional) · D ≈ 35 min (+15 optional) · E ≈ 35 min · F ≈ 50 min (+≈ 45 optional). **Must-run ≈ 4 h 20 min including setup.** Blocks B to D: kid's session in front, do not start after ≈ 22:30.
>
> **DENY BY DEFAULT**: right after the update **every app** a controlled account used before is **blocked** until you set a rule (browser, games, Terminal, Task-Manager, *Einstellungen*, Snipping Tool). Start menu, Explorer, lock, sign-out and the tray icon still work. **Install only when the kids do not need the PC.** Real kid accounts: untick them for the run, or set their rules right after TC-006-04. Do everything that needs *Einstellungen* in a kid account **before** the upgrade (S-6, S-7).
>
> **Accounts**: **Admin** = you. **kid1** = `eagleeye-kid` (controlled, German). **ee-kid2** = new, **controlled**, **English** Windows (deleted in TC-006-37). **ee-kid3** = new, **not** controlled. *(In US-005 the roles of ee-kid2/ee-kid3 were reversed; both are new accounts now.)* **App A** = parent app in your admin session; **App B** = parent app on **LEOSERV** (to change rules while the kid's session is in front).
> **Terminal (Administrator)** = elevated terminal in your session. **K-TERM** = elevated terminal **inside the kid's session** with your admin credentials (runs as you, never limited).
>
> **Rule set for kid1 (TC-006-04)**: **Editor** 0:03, all days · **Paint** 0:30, only **SA** (on a Saturday: only **SO**) · **Rechner** untouched (0:00, no day). **Settings**: **Frühe Warnung 2**, **Finale Warnung 1**; warning texts stay default.
>
> **Rules**:
> - **UI wording** differences are Notes (expected German texts: test plan §2 table). **But** a text **you** entered must appear in the kid dialog **exactly** (lines, emojis); a difference is a Fail.
> - **Kid dialog**: title **EagleEye**, parent's text (or default), separator, **info line** (e.g. "Editor: noch 2 Minuten."), **OK**. One dialog per session; a newer message **replaces** the text.
> - **Budget timing**: stopwatch **start on Enter** (Windows+R). An event "at X of use" is expected between **X and X + 15 s**; app gone by **end + 35 s**. Earlier than X = Fail.
> - **Grace timing**: lap **C** = your Tab/Enter in App B. Final warning ≤ **C + 15 s**; close between **C + 60 s and C + 75 s**; gone ≤ **C + 95 s**. Closed **before C + 55 s** = Fail.
> - **Break timing** (kid's taskbar clock with seconds): early **T − 2:00…T − 1:50**, final **T − 1:00…T − 0:50**, break text ≤ **T + 0:10**, all apps gone ≤ **T + 0:30**.
> - **Used time counts only while the kid's session is unlocked and in front**: no user switch or lock during a timed case unless a step says so.
> - Up to 1 s outside a window = Note (reaction time); the log timestamps decide.
> - **Test apps** (Windows+R): `notepad` (Editor), `mspaint` (Paint), `calc` (Rechner), `charmap` (Zeichentabelle), `msinfo32` (Systeminformationen), `mstsc` (Remotedesktopverbindung), `dxdiag` (DirectX-Diagnoseprogramm).
> - Must **not** appear: `could not be terminated`, `could not be ended`, any `[ERR]` line.

### Commands (elevated terminal)

**LOG-COPY** (repo root):

```powershell
Set-Location <your EagleEye repo folder>
$dst = Join-Path "02_Implementation\docs\testing\US-006\evidence" "<subfolder>"
New-Item -ItemType Directory -Force $dst | Out-Null
Copy-Item "$env:ProgramData\EagleEye\logs\*.log" $dst -Force
Get-ChildItem $dst
```

**LOG-WATCH** (after every service restart/re-install/reboot: *Strg+C* and again):

```powershell
$log = Get-ChildItem "$env:ProgramData\EagleEye\logs\EagleEye.Service-*.log" | Sort-Object LastWriteTime | Select-Object -Last 1
Get-Content $log.FullName -Tail 15 -Wait
```

**LOG-FIND**:

```powershell
Select-String -Path "$env:ProgramData\EagleEye\logs\*.log" -Pattern "<pattern>" | Select-Object -Last 15 | ForEach-Object Line
```

Patterns: `Warning: account|Grace period|Blocked start|Closing app|App closed` · `App rules of account|Warning settings changed` · `time zone|clock of the PC`

---

## Setup — before Block A (service PC, Admin; kids do not need the PC) · ≈ 40 min

- [ ] **S-1 Installers**: `Get-FileHash 03_Delivery\windows\EagleEye-Setup-0.6.0.exe, 03_Delivery\windows\EagleEye-ParentApp-Setup-0.6.0.exe | Format-List Path, Hash` → hashes as in test plan §2.
- [ ] **S-2 Installed state** (*Installierte Apps*; `services.msc`): EagleEye ______ · EagleEye Parent App ______ (expected **0.5.0**, service running). Already 0.6.0 or no service → **stop**, tell TES.
- [ ] **S-3 App A green**, *Gekoppelt*, **Papas PC**.
- [ ] **S-4 Deny by default** read. Accounts ticked in *Einstellungen* now: ____________ . Real kid accounts unticked for the run / to be configured after TC-006-04: ____________
- [ ] **S-5 Accounts**: `eagleeye-kid` exists. Create `ee-kid2` and `ee-kid3` (Terminal (Administrator)):
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
  Sign in once as each (*Benutzer wechseln*), wait for the desktop.
- [ ] **S-6 English for `ee-kid2`** (before the upgrade): if needed, in **your** session *Einstellungen → Zeit und Sprache → Sprache und Region → Sprache hinzufügen* → **English (United States)** with Sprachpaket, **not** as your display language. As `ee-kid2`: *Windows-Anzeigesprache* → **English (United States)** → sign out and in → Start menu shows "Settings". Sign `ee-kid2` and `ee-kid3` **out**. English not available → write here: ____________ (TC-006-30 Blocked).
- [ ] **S-7 `eagleeye-kid`**: *Einstellungen → Personalisierung → Taskleiste → Verhalten der Taskleiste* → **Sekunden in der Uhr der Taskleiste anzeigen** ✓. Close all apps, **sign out**.
- [ ] **S-8 Admin scaling** ______ % (expected 150 %).
- [ ] **S-9 Terminal (Administrator)** open; a second one with LOG-WATCH; `hostname` → ____________
- [ ] **S-10 Selection**: App A → *Einstellungen*: **eagleeye-kid** and **ee-kid2** ticked; **ee-kid3** and other test accounts unticked.
- [ ] **S-11 SmartScreen**: *Weitere Informationen* → *Trotzdem ausführen* (expected).
- [ ] **S-12 K-TERM understood** (in the kid's session: Start → **Terminal** → right-click → **Als Administrator ausführen** → your admin credentials).
- [ ] **S-13 App B on LEOSERV** (after TC-006-02): install `EagleEye-ParentApp-Setup-0.6.0.exe`; green and *Gekoppelt* (else pair as **LEOSERV**). Not available → write here: ____________ (TC-006-10 Blocked).
- [ ] **S-14 Stopwatch** with laps ready.
- [ ] **S-15 SQLite tool** for optional TC-006-41: yes / no ______
- [ ] **S-16 All kid accounts signed out** (*Task-Manager → Benutzer*: only you).

---

## Block A — Upgrade, Rules and Settings pages (must-run, except -11) · ≈ 50 min

### TC-006-01: Service update 0.5.0 → 0.6.0 keeps pairing, selection and break times *(must-run)*

*Verifies AC-5, AC-18 · Service PC / Admin · App A (0.5.0) open and green*

1. Run `03_Delivery\windows\EagleEye-Setup-0.6.0.exe` with defaults. Leave App A open.
2. Tray → *App Infos*; *Installierte Apps*; `services.msc`.
3. Watch App A up to 60 s. Log monitor: *Strg+C*, LOG-WATCH; LOG-FIND `App rules loaded|Warning levels loaded|Break times loaded|ERR`.

**Expected**:
- *App Infos* **EagleEye_v0.6**; **EagleEye 0.6.0** once; service running. App A green within 60 s without a code.
- Log "App rules loaded: **0 rules** of N apps of M accounts, 0 changed budget texts." and "Warning levels loaded: early 5 min, final 1 min."; "Break times loaded: …" with the US-005 entries; no Error.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**: N apps = ____ · M accounts = ____

---

### TC-006-02: Parent app 0.6.0: the new Rules section; existing apps are blocked by default *(must-run)*

*Verifies AC-1, AC-2, AC-3, AC-5 · Service PC / Admin*

1. Close App A. Install `EagleEye-ParentApp-Setup-0.6.0.exe`; start it.
2. **Regeln** → **Konto** `eagleeye-kid`; scroll below **Ruhezeiten** and its **Anzeige Text:**. Read the new section top to bottom.
3. Values, ticks, order of the rows. A row **Windows-Explorer**? *Note only*: tooltip with the program path when hovering over a name?
4. **Konto** → `ee-kid2`; back to `eagleeye-kid`.

**Expected**:
- **Parent App 0.6.0**, green without a code.
- Below the break section: accent bar **Applikations-Regeln**; explanation ("Lege hier fest, welche Applikationen …"), wrapped; table **Applikation · Tägliches Zeit-Budget (Stunden · Minuten) · MO DI MI DO FR SA SO**; **Anzeige Text wenn Budget aufgebraucht:** with "Du hast deine Zeit aufgebraucht! Leider ist jetzt Schluss.".
- One row per app kid1 ever used (as on *Berichte*), **all 0 / 0, no day ticked**; alphabetical, case ignored; **no** Windows-Explorer.
- `ee-kid2`: **Noch keine Applikationen aufgezeichnet.** and the default text; back: kid1's table.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**: tooltip with path: yes / no

---

### TC-006-03: Settings: the new section "Zeit-Ende Warnungen" with its defaults *(must-run)*

*Verifies AC-13, AC-14 · Service PC / Admin*

1. **Einstellungen**; below **Benutzerkonten auf dem EagleEye-PC** read the new section.
2. Try to edit the labels; look for add/delete or account controls.

**Expected**:
- Bar **Zeit-Ende Warnungen**; explanation "Hier kannst du 2 Zeitpunkte einstellen, …"; table **Zeitpunkt · Minuten vor Ende · Anzeigetext**; two rows: **Frühe Warnung · 5 ·** "Achtung, du hast nur noch 5 Minuten! Speichere deinen Fortschritt." and **Finale Warnung · 1 ·** "Achtung!!! Nur noch 1 Minute – jetzt aber schnell speichern!".
- Labels not editable; no add/delete; no account selection.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

### TC-006-04: Setting the test rules and warning levels: edited in place, saved at once, logged *(must-run; preparation for Blocks B to F)*

*Verifies AC-7, AC-11, AC-16 · Service PC / Admin; log monitor · Regeln → eagleeye-kid*

1. **Editor**: **Minuten** `3`, **Tab**.
2. **Editor**: tick **MO** to **SO** one after the other.
3. **Paint**: **Minuten** `30`, **Enter**; tick **SA** (Saturday: **SO**); tick **MO**, untick it again.
4. **Rechner**: do not touch.
5. **Einstellungen** → **Frühe Warnung** minutes `2`, **Tab**.

**Expected**:
- One log line per change within 5 s: "App rules of account eagleeye-kid (S-1-5-21-…) changed by Papas PC: app rule changed: Editor, 0:03, no day" … "Editor, 0:03, Mo Tu We Th Fr Sa Su"; "Paint, 0:30, no day", "Paint, 0:30, Sa", "Paint, 0:30, Mo Sa", "Paint, 0:30, Sa" (or Su). No Save button.
- Step 5: "Warning settings changed by Papas PC: warning levels changed: early 2 min, final 1 min".

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

### TC-006-05: Invalid hours and minutes are not saved; 23:59 is the largest budget *(must-run)*

*Verifies AC-8 · Service PC / Admin; log monitor · Paint = 0:30*

1. Paint **Stunden**: `24` Tab · `-1` Tab · try `abc` Tab · empty Tab.
2. Paint **Minuten**: `60` Tab · `1.5` Tab.
3. Paint **Stunden** `23` Tab; **Minuten** `59` Tab.
4. Paint back to **Stunden** `0`, **Minuten** `30`.

**Expected**:
- Step 1: field back to **0**, **Bitte Stunden zwischen 0 und 23 eingeben.**, no log line (letters or minus not accepted = Pass too).
- Step 2: back to **30**, **Bitte Minuten zwischen 0 und 59 eingeben.**, no log line.
- Step 3: saved ("Paint, 23:30, Sa", "Paint, 23:59, Sa"). Step 4: back to 0:30.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

### TC-006-06: Invalid warning minutes and the rule "final less than early" *(must-run)*

*Verifies AC-15 · Service PC / Admin; log monitor · Einstellungen, early 2 / final 1*

1. **Frühe Warnung** minutes: `0` · `60` · try `abc` · `2.5` · empty — each with **Tab**.
2. **Finale Warnung** minutes: `2` Tab · `3` Tab.
3. **Frühe Warnung** minutes: `1` Tab.

**Expected**:
- Step 1: back to **2**, **Bitte Minuten zwischen 1 und 59 eingeben.**, no log line.
- Steps 2, 3: back to the stored value, **Die finale Warnung muss weniger Minuten vor dem Ende liegen als die frühe Warnung.**, no log line. Still early 2 / final 1.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

### TC-006-07: Budget text and warning texts: line break, emojis, empty text, typed default *(must-run)*

*Verifies AC-10, AC-11, AC-15, AC-16 · Service PC / Admin; log monitor · Regeln → eagleeye-kid*

1. Budget text box: select all, `Budget leer! ` + **🛑** (**Windows + .**), **Enter**, `Morgen geht's weiter ` + paste **👍**. Click outside.
2. **Berichte**, then **Regeln** (`eagleeye-kid`).
3. Delete the whole text; click outside.
4. Type `You have used up your time! Sorry, that's it for now.`; click outside.
5. Enter the text of step 1 again (two lines, 🛑 👍); click outside. **It stays for the run.**
6. **Einstellungen** → **Finale Warnung** text: `Schnell speichern! 💾`, **Tab**; then empty it, **Tab**.
7. *Optional*: paste > 500 characters into the budget text box.

**Expected**:
- Step 1: two lines with emojis; log "… budget text changed" (no text in the log). Step 2: unchanged.
- Step 3: German default shown; "… budget text reset to the default".
- Step 4: German default shown again; "… reset to the default" (or no line: Note).
- Step 5: "budget text changed". Step 6: "… final warning text changed", then "… final warning text reset to the default"; German default shown.
- Step 7 (optional): stops at about 500 characters.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

### TC-006-08: Page states; rules, texts and levels survive a parent app restart and a service restart *(must-run)*

*Verifies AC-6, AC-18 · Service PC / Admin, Terminal (Administrator) · Regeln → eagleeye-kid*

1. `Stop-Service -DisplayName "EagleEye Service"`; wait for the red status bar.
2. `Start-Service -DisplayName "EagleEye Service"`; watch the page; LOG-WATCH again.
3. Close App A (X) and start it; **Regeln** → `eagleeye-kid`; **Einstellungen**.
4. *Einstellungen*: untick **eagleeye-kid** and **ee-kid2** (and any other ticked account; re-tick it afterwards). **Regeln**. Tick both again; **Regeln** → `eagleeye-kid`.

**Expected**:
- Step 1: no crash; **Keine Daten verfügbar**; nothing editable.
- Steps 2, 3: green ≤ 60 s; Editor 0:03 all days, Paint 0:30 SA/SO, kid1's two-line budget text; Settings 2 / 1 with defaults.
- Step 4: ≤ 60 s **Keine Konten unter Elternkontrolle. Konten unter Einstellungen auswählen.**; after ticking: unchanged rules and text.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**: "Wird geladen …" seen: yes / no

---

### TC-006-09: Light and dark mode, 150 % scaling *(must-run)*

*Verifies AC-6 · Service PC / Admin (150 %)*

1. **Applikations-Regeln** and **Zeit-Ende Warnungen**: screenshots → `evidence/run01-tc09-<page>-<mode>.png`.
2. *Einstellungen → Darstellung*: other mode (Hell ↔ Dunkel); repeat; switch back.

**Expected**: in both modes all texts, fields, the seven checkbox columns and both bars clearly visible; app colours only; at 150 % nothing cut off, headers above their columns, checkboxes not overlapping, explanations wrapped.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

### TC-006-10: Two parent apps stay in sync; the last change wins *(must-run, needs LEOSERV)*

*Verifies AC-17 · App A and App B side by side, both Regeln → eagleeye-kid · no LEOSERV → Blocked*

1. A: Paint **Stunden** `1`, Tab. Watch B.
2. B: Paint tick **DO** (Thursday: **MI**). Watch A.
3. A: untick that day. Watch B.
4. A and B together (count down): A Paint **Minuten** `20`, B `40`, both **Enter**. Wait 5 s.
5. A: Paint **Stunden** `0`, **Minuten** `30`, Tab.
6. Both → **Einstellungen**. A: **Frühe Warnung** text `Gleich ist Schluss ⏰`, Tab; watch B. A: empty it, Tab; watch B.

**Expected**: every change shows in the other app **≤ 5 s** without action there; step 4: both show the **same** minutes; at the end Paint 0:30 SA (or SO), early text default in both.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**: step 4 winner: 20 / 40

---

### TC-006-11 (optional): The service cannot store a change: message, field returns

*Verifies AC-12 · Service PC / Admin, Terminal (Administrator) · kids signed out; Regeln → eagleeye-kid*

1. `resmon` → **CPU** → right-click `EagleEye.Service.exe` → **Prozess anhalten**.
2. Within 10 s: App A Paint **Minuten** `31`, Tab; wait up to 10 s.
3. ≤ 25 s after step 1: **Prozess fortsetzen**; wait 30 s.
4. Paint back to `30` if needed.

**Expected**: step 2 **Die Änderung konnte nicht gespeichert werden. Bitte erneut versuchen.** and the field back to **30**; step 3 green, Paint = the value of the last Paint log line (30 or 31: both Pass).

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**: value after resume: ____

---

## Block B — Start check and the first budget run (must-run) · ≈ 30 min

> **Before Block B** (test plan §3):
> - [ ] **P-1** TC-006-04 done (kid1 rule set; early 2 / final 1).
> - [ ] **P-2** App B → **Berichte** → `eagleeye-kid` today: Editor ____ · Zeichentabelle ____ · Systeminformationen ____ · Paint ____ · Rechner ____ (expected none). App B → **Regeln** → `eagleeye-kid`, table visible. kid1's break display text (**Anzeige Text:** under *Ruhezeiten*): ____________
> - [ ] **P-3** *Benutzer wechseln* → `eagleeye-kid`. Blocked autostart apps: **OK** (names → Notes of TC-006-12).
> - [ ] **P-4** **K-TERM** with LOG-WATCH (second tab for LOG-FIND).
> - [ ] **P-5** No other app open in kid1's session.

### TC-006-12: An app with the default rule is blocked at start; budget text and info line in the dialog *(must-run)*

*Verifies AC-19, AC-5, AC-9, AC-34, AC-37 · Service PC / kid1, K-TERM; App B · Rechner default; no dialog*

1. `calc`; stopwatch on Enter; **lap 1** dialog, **lap 2** Rechner gone.
2. Read the dialog. **Esc**; **Alt+F4**; click the desktop. Then click into the dialog and **Enter**.
3. App B: Rechner tick all seven days (still 0:00); 5 s; `calc`; read; **OK**.
4. App B: untick all seven days of Rechner.
5. LOG-FIND `Rechner`.

**Expected**:
- Dialog ≤ 10 s; Rechner gone ≤ 30 s.
- **EagleEye**; "Budget leer! 🛑" / "Morgen geht's weiter 👍" **exactly**; separator; **"Rechner: heute nicht erlaubt."**; **OK**. Esc, Alt+F4, click do not close it; Enter does.
- Step 3: blocked again (0:00 blocks whatever days are ticked).
- Log "Blocked start: … Rechner …, not allowed today (rule 0:00, no day, used 0:00:00); closing the app.", step 3 "(rule 0:00, Mo Tu We Th Fr Sa Su, …)"; "Blocked start ended: … closed gracefully | terminated by force after 20 s …; message shown" (both OK for this Store app).

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**: Lap 1 ____ s · Lap 2 ____ s · outcome ________ · blocked autostart apps: ____________

---

### TC-006-13: An app whose day is not ticked is blocked *(must-run)*

*Verifies AC-19 · Service PC / kid1, K-TERM · Paint 0:30, only SA (Saturday: SO)*

1. `mspaint`; stopwatch; laps; **OK**.
2. LOG-FIND `Paint`.

**Expected**: dialog ≤ 10 s with kid1's budget text and **"Paint: heute nicht erlaubt."**; gone ≤ 30 s; log "Blocked start: … Paint (mspaint.exe, …), … not allowed today (rule 0:30, Sa, used 0:00:00) …" and "… closed gracefully …".

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**: Lap 1 ____ s · Lap 2 ____ s

---

### TC-006-14: A new app is blocked at its first start and appears live in the Rules table *(must-run)*

*Verifies AC-4, AC-3, AC-5 · Service PC / kid1, K-TERM; App B in view · Systeminformationen not listed (else use `dxdiag` here and in TC-006-18)*

1. `msinfo32`; stopwatch; **OK**.
2. Watch App B (no touching); note the stopwatch when the row appears.
3. LOG-FIND `Systeminformationen|msinfo32`.

**Expected**: blocked ("Systeminformationen: heute nicht erlaubt."); within **15 s** a row **Systeminformationen** in its alphabetical place, **0 / 0, no day**; log "New app for account eagleeye-kid: Systeminformationen (msinfo32.exe, …)." and "Blocked start: … not allowed today (rule 0:00, no day, …)".

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**: row appeared at ____ s · app used: msinfo32 / dxdiag

---

### TC-006-15: Notepad with 0:03: starts, early and final warning, closed gracefully with the budget text *(must-run, core)*

*Verifies AC-20, AC-23, AC-24, AC-25, AC-34, AC-14, AC-37, AC-38; Q-4 · Service PC / kid1, K-TERM · Editor 0:03 all days, no usage today; no dialog. Stay in kid1's session, no lock.*

1. `notepad`; **stopwatch on Enter**; type **nothing**.
2. **Lap 1** at the first dialog; read; **leave it open**.
3. **Lap 2** when its text changes; read; **leave it open**.
4. **Lap 3** when the text changes again; **lap 4** when Notepad is gone. Read; **OK**.
5. LOG-FIND `Editor`.

**Expected**:
- Notepad opens as fast as before (your judgement), no dialog at start.
- Lap 1 **1:00–1:15**: early default text "Achtung, du hast nur noch 5 Minuten! …" + **"Editor: noch 2 Minuten."**
- Lap 2 **2:00–2:15**: the **same** dialog now: final default text + **"Editor: noch 1 Minute."**; no second dialog.
- Lap 3 **3:00–3:15**: kid1's budget text (🛑 👍) + **"Editor: Zeit-Budget aufgebraucht."**; lap 4 ≤ **3:35**. Each warning once.
- Log: "Warning: … Editor (notepad.exe), early warning, 2 min left (budget); message shown.", "… final warning, 1 min left (budget); message shown (replaced the open message).", "Closing app: … Editor …, budget used up.", "App closed: … budget used up: **closed gracefully** after 0.x s; message …".
- **Q-4**: "terminated by force after 20 s" for Notepad = **Fail**.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**: Lap 1 ____ · Lap 2 ____ · Lap 3 ____ · Lap 4 ____ · outcome "closed gracefully after ____ s" · start speed OK: yes / no

---

### TC-006-16: Notepad again the same day: blocked, budget used up; Reports matches *(must-run)*

*Verifies AC-19, AC-23, AC-25 · Service PC / kid1, K-TERM; App B · after TC-006-15*

1. `notepad`; stopwatch; **OK**.
2. App B → **Berichte** → `eagleeye-kid` today: **Editor** = ____; 1 minute later again; back to **Regeln**.
3. LOG-FIND `Blocked start.*Editor`.

**Expected**: blocked (dialog ≤ 10 s, budget text, **"Editor: Zeit-Budget aufgebraucht."**, gone ≤ 30 s); Reports Editor ≈ **3:00 to 3:35** (+ P-2 usage), unchanged a minute later; log "… budget used up (rule 0:03, Mo Tu We Th Fr Sa Su, used 0:03:xx); closing the app."

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**: Reports Editor ____

---

### TC-006-17: A budget at or below the final level shows the final warning at start; closed at the end *(must-run)*

*Verifies AC-22, AC-25 · Service PC / kid1, K-TERM; App B · Zeichentabelle listed (else `charmap` once, **OK**); App B: Zeichentabelle **Minuten** `1`, Tab, all seven days; 5 s*

1. `charmap`; stopwatch; lap 1 at the dialog; read; **leave it open**.
2. Lap 2 at the text change; lap 3 when gone. **OK**.

**Expected**: lap 1 **≤ 0:15**: **final** warning + **"Zeichentabelle: noch 1 Minute."**, no early warning at all; lap 2 **1:00–1:15**: budget text + "Zeichentabelle: Zeit-Budget aufgebraucht."; lap 3 ≤ **1:35**; log "Warning: … Zeichentabelle (charmap.exe), final warning, 1 min left (budget) …", "App closed: … closed gracefully …".

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**: Lap 1 ____ · Lap 2 ____ · Lap 3 ____

---

## Block C — Changes during the day: grace periods · ≈ 20 min (+10 optional)

*kid1's session in front; K-TERM; App B → Regeln → eagleeye-kid makes all changes (Tab/Enter sends a field).*

### TC-006-18: A change that leaves 20 seconds gives the full grace minute *(must-run)*

*Verifies AC-22, AC-32 (Q-2), AC-34, AC-37 · Service PC / kid1, K-TERM; App B · App B: Systeminformationen **Minuten** `2`, Tab, all seven days; 5 s; no dialog*

1. `msinfo32`; **stopwatch on Enter**; lap at the dialog; read; **OK**.
2. At **0:40**: App B Systeminformationen **Minuten** `1`, **Tab** → **lap C**.
3. Lap at the dialog; read; **leave it open**.
4. Lap at the text change; lap when gone. **OK**.
5. LOG-FIND `Systeminformationen|Grace period`.

**Expected**:
- Step 1 **≤ 0:15**: **early** warning + **"Systeminformationen: noch 2 Minuten."**
- Step 3 ≤ **C + 15 s**: final warning text + **"Systeminformationen: Deine Eltern haben die Regeln geändert. Systeminformationen wird in 1 Minute geschlossen."**
- Step 4: budget text between **C + 60 s and C + 75 s**; gone ≤ **C + 95 s**. **Closed at about 1:00 on the stopwatch (C + 20 s) = Fail.**
- Log "Grace period: … Systeminformationen (msinfo32.exe): the rules changed (…); closes in 1 min at HH:MM:SS; message shown.", "Closing app: … budget used up, after grace period.", "App closed: …".

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**: early at ____ · C at ____ · final at ____ · close at ____ · gone at ____

---

### TC-006-19: A raised budget lets the app start again without repeating warnings; 0:00 while running gives a grace minute *(must-run)*

*Verifies AC-32, AC-24 (Q-5), AC-34 · Service PC / kid1, K-TERM; App B · after TC-006-17; App B **Berichte**: Zeichentabelle today Z0 = ____*

1. App B **Regeln**: Zeichentabelle **Minuten** `3`, Tab; 5 s.
2. `charmap`; stopwatch; watch 20 s.
3. At **0:20**: App B Zeichentabelle **Minuten** `0`, **Tab** → **lap C**.
4. Laps: final warning, close, gone. **OK**.

**Expected**:
- Step 2: starts and stays open; **no** warning at start, although < 2 min are left (both levels already shown today, Q-5).
- Step 4: ≤ **C + 15 s** final warning + **"Zeichentabelle: Deine Eltern haben die Regeln geändert. Zeichentabelle wird in 1 Minute geschlossen."**; budget text between **C + 60 s and C + 75 s**; gone ≤ **C + 95 s**.
- Log "Grace period: … Zeichentabelle (charmap.exe): the rules changed …", "Closing app: …, after grace period."

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**: C at ____ · final at ____ · close at ____ · gone at ____

---

### TC-006-20: A change within the early level shows the early warning at once (no grace); Paint with an unsaved drawing is ended by force *(must-run)*

*Verifies AC-32, AC-25 (force), AC-24 · Service PC / kid1, K-TERM; App B · App B: Paint tick **today** (0:30, no usage today); 5 s*

1. `mspaint`; **stopwatch on Enter**; draw a line, **do not save**.
2. At **0:20**: App B Paint **Minuten** `2`, **Tab** → **lap C**.
3. Lap at the dialog; read; **leave it open**.
4. Lap at the text change; leave it open.
5. Paint's **"save?" prompt**: **lap P**, **do not answer**; lap when Paint is gone. **OK**.
6. K-TERM `Get-Process mspaint -ErrorAction SilentlyContinue`; LOG-FIND `Paint`.

**Expected**:
- Step 3 ≤ **C + 15 s**: **early** warning + **"Paint: noch 2 Minuten."** (no "Deine Eltern haben die Regeln geändert …").
- Step 4: final + "Paint: noch 1 Minute." at **1:00–1:15**.
- Step 5: prompt and budget text at **2:00–2:15** (not C + 60 s); Paint gone **20 s (±2) after lap P**, ≤ **2:35**.
- Step 6: no `mspaint`; log "Warning: … Paint …, early warning, 2 min left (budget) …", "Closing app: … Paint …, budget used up." (no "after grace period"), "App closed: … **terminated by force after 20 s** (…)"; no "Grace period" for Paint; no "could not be terminated".

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**: C ____ · early ____ · final ____ · P ____ · gone ____

---

### TC-006-21 (optional): Today unticked while running → grace; ticked again during the grace → the app keeps running

*Verifies AC-32, AC-24 · Service PC / kid1; App B · App B: Zeichentabelle **Minuten** `10`, Tab*

1. `charmap`; wait 20 s.
2. App B: untick **today** for Zeichentabelle → **lap C**; read; **OK**.
3. At **C + 20 s**: App B tick today again.
4. Watch until **C + 2:00**; close Zeichentabelle yourself.

**Expected**: step 2 ≤ C + 15 s final warning + "… Deine Eltern haben die Regeln geändert. … in 1 Minute geschlossen."; step 4 **not** closed, no new warning; log one "Grace period", no "Closing app" for Zeichentabelle.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

### TC-006-22 (optional): Locked time does not count

*Verifies AC-23 · Service PC / kid1; App B · `mstsc` once (blocked, row appears), **OK**; App B: Remotedesktopverbindung **Minuten** `2`, all days; 5 s*

1. `mstsc`; stopwatch; **OK** in the early warning.
2. At **0:30**: **Windows+L**; unlock at **1:30**.
3. Laps: final warning, gone.

**Expected**: final at about **2:00** (not 1:00); closed at about **3:00** (± 15 s).

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**: final ____ · gone ____

---

## Block D — Break times · ≈ 35 min (+15 optional)

> **Before Block D** (App B, kid1):
> - [ ] Paint **Stunden** `1` **Minuten** `0`, today ticked · Zeichentabelle **Stunden** `1` **Minuten** `0`, all days · Editor **Stunden** `23` **Minuten** `59`, all days.
> - [ ] **Ruhezeiten**: entry **E1** (existing from US-005, or **Eintrag hinzufügen**): **An/Aus** off, **End-Zeit** 23:59, all days.
> - [ ] Only K-TERM open in kid1's session.
>
> **BREAK ON** = tick **An/Aus** of E1; **BREAK OFF** = untick. Wait until confirmed (stays, no message).

### TC-006-23: A break begins: early and final break warnings once, then every running app is closed *(must-run; replaces US-005 AC-25)*

*Verifies AC-28, AC-29, AC-31, AC-34, AC-37 · Service PC / kid1, K-TERM; App B · no dialog*

1. **T** = kid1's clock minute + **4** = ________ . App B: E1 **Start-Zeit** `T`, Enter; **BREAK ON**.
2. kid1: `charmap`; `mspaint`, draw a line, **do not save**. Leave both open.
3. At **T − 2:00**: dialog (clock ____); read; **leave it open**.
4. At **T − 1:00**: text change (clock ____).
5. At **T**: Paint prompt: do not answer. Clock when each app is gone. **OK**.
6. LOG-FIND `break time|App closed`.

**Expected**:
- Step 3 **T − 2:00…T − 1:50**: **one** dialog, early default text + **"Die Ruhezeit beginnt um T."**
- Step 4 **T − 1:00…T − 0:50**: same dialog, final text + "Die Ruhezeit beginnt um T."; no per-app dialogs.
- Step 5 ≤ **T + 0:10**: kid1's **break display text** (P-2) **once** + break info line (e.g. "Ruhezeit von T bis 24:00."); Zeichentabelle closed gracefully, Paint by force ≈ 20 s after its prompt; **both gone ≤ T + 0:30**.
- **No** budget warning ("noch 2 Minuten") for Paint or Zeichentabelle.
- Log "Warning: account eagleeye-kid, break time T–23:59 (Mo Tu We Th Fr Sa Su) begins at T, early warning, 2 min left; message shown.", same with "final warning, 1 min left", "App closed: … Zeichentabelle …, break time …: closed gracefully …", "App closed: … Paint …, break time …: terminated by force after 20 s (…)".

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**: early ____ · final ____ · break text ____ · Zeichentabelle gone ____ · Paint gone ____

---

### TC-006-24: During a break, starts are blocked with the break text, even with 23:59 *(must-run)*

*Verifies AC-21 · Service PC / kid1, K-TERM · E1 in effect; no dialog*

1. `notepad` (23:59); read; **OK**.
2. `calc` (default rule); read; **OK**.
3. LOG-FIND `Blocked start`.

**Expected**: both blocked (≤ 10 s / ≤ 30 s) with kid1's **break display text** (not the budget text) + break info line; log "Blocked start: … Editor …, break time T–23:59 (…); closing the app." and the same for Rechner.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

### TC-006-25: An app started shortly before a break shows the warning at once *(must-run)*

*Verifies AC-30, AC-29 · Service PC / kid1; App B · **BREAK OFF**; only K-TERM open*

1. **T2** = clock minute + **4** = ________ . App B: E1 **Start-Zeit** `T2`, Enter; **BREAK ON**.
2. At **T2 − 1:45**: `charmap`; read; **leave it open**.
3. Clock at the text change ____ and when gone ____. **OK**.

**Expected**: step 2 ≤ 10 s: **early** warning + "Die Ruhezeit beginnt um T2."; final at **T2 − 1:00…T2 − 0:50**; break text at T2; gone ≤ **T2 + 0:30**.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

### TC-006-26: A break switched on now: every running app gets one grace minute; new starts are blocked at once *(must-run)*

*Verifies AC-33, AC-29, AC-34, AC-37 · Service PC / kid1, K-TERM; App B · **BREAK OFF**; kid1: `charmap` and `mspaint` (no drawing) open, no dialog*

1. App B: E1 **Start-Zeit** = now − 1 min, Enter (still off). **BREAK ON** → **lap C**.
2. Read the dialog; **leave it open**.
3. At **C + 20 s**: `notepad`.
4. Laps: break text, both apps gone. **OK**.
5. App B **BREAK OFF**. LOG-FIND `Grace period|App closed|Blocked start`.

**Expected**:
- Step 2 ≤ **C + 15 s**: **one** dialog, final text + **"Deine Eltern haben eine Ruhezeit eingestellt. Alle Apps werden in 1 Minute geschlossen."**
- Step 3: Notepad blocked at once (gone ≤ 30 s), dialog text → break text; Zeichentabelle and Paint **stay open**.
- Step 4: break text between **C + 60 s and C + 75 s**; both closed, gone ≤ **C + 95 s**.
- Log "Grace period: account eagleeye-kid, break time … ; 2 app(s) close in 1 min at HH:MM:SS; message shown.", "Blocked start: … Editor …, break time …", "Closing app: …, after grace period." (twice), "App closed: …".

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**: C ____ · final ____ · close ____ · gone ____

---

### TC-006-27 (optional): A break moved to begin within the early level shows the early warning at once

*Verifies AC-33 · Service PC / kid1; App B · BREAK OFF; Zeichentabelle open*

1. When the clock shows **:00 to :10** seconds: E1 **Start-Zeit** = minute + **2**, Enter; **BREAK ON** → lap C.
2. Watch until the break begins; **OK**; **BREAK OFF**.

**Expected**: ≤ C + 15 s **early** warning + "Die Ruhezeit beginnt um …" (no "Deine Eltern haben eine Ruhezeit eingestellt …"); final 1 min before; closed at the start with the break text.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

### TC-006-28 (optional): The budget runs out before the next break: only the budget warnings

*Verifies AC-31 · Service PC / kid1; App B · BREAK OFF; App B: Rechner **Minuten** `2`, all days; E1 **Start-Zeit** = minute + **5**; BREAK ON*

1. At once: `calc`; stopwatch; read each dialog; leave it open.
2. After Rechner is gone: **BREAK OFF** before the break begins.

**Expected**: "Rechner: noch 2 Minuten." at start, final at 1:00, closed at 2:00 with the **budget** text; **no** "Die Ruhezeit beginnt um …" while Rechner runs.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

## Block E — Other accounts, English, exemptions, tray client (must-run) · ≈ 35 min

> **Before Block E**: App B: E1 **off**. Close K-TERM; sign kid1 out.

### TC-006-29: An uncontrolled account and the admin account are never warned, blocked or closed *(must-run)*

*Verifies AC-26 · Service PC / ee-kid3 (not controlled), Admin*

1. *Benutzer wechseln* → `ee-kid3`: `notepad`, `mspaint`, `calc`, `charmap`; use them **3 minutes**.
2. *Benutzer wechseln* → Admin: start **Editor**, close it. LOG-FIND `account ee-kid3|account Admin`.
3. Sign `ee-kid3` out.

**Expected**: all apps open and stay open; no EagleEye dialog for ee-kid3 or you; no "Blocked start", "Warning: account", "Grace period" or "App closed" line for them.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

### TC-006-30: English kid: English default texts and info lines; kid1's rules do not apply to ee-kid2 *(must-run)*

*Verifies AC-10, AC-14, AC-34, AC-18; plan R-10 · Service PC / ee-kid2 (English), K-TERM; App B · S-6 done (else Blocked); warning texts default*

1. *Benutzer wechseln* → `ee-kid2`; blocked autostart apps **OK**; open **K-TERM**.
2. `notepad`; read; **OK**.
3. App B → **Regeln** → **Konto** `ee-kid2`: the new Notepad row: **Minuten** `2`, Tab; all seven days; 5 s.
4. `notepad`; stopwatch; read; **leave it open**; read at ≈ 1:00 and ≈ 2:00. **OK**.
5. App B, `ee-kid2` **Ruhezeiten**: **Eintrag hinzufügen** → **Start-Zeit** now − 1 min, Enter → **An/Aus** ✓; 5 s.
6. `notepad`; read; **OK**. App B: untick **An/Aus** of that entry.
7. App B (German) for `ee-kid2`: the budget text box and **Anzeige Text:**.
8. K-TERM: LOG-FIND `account ee-kid2`.

**Expected**:
- Step 2: blocked (kid1's 23:59 does not apply). **English** "You have used up your time! Sorry, that's it for now." + "Notepad: not allowed today."; **EagleEye**, **OK**.
- Step 4: "Attention, you only have 5 minutes left! Save your progress." + "Notepad: 2 minutes left." → ≈ 1:00 "Attention!!! Only 1 minute left – save quickly now!" + "Notepad: 1 minute left." → ≈ 2:00 English budget text + "Notepad: time budget used up."; closed gracefully.
- Step 6: "Hi! Unfortunately, your parents have set a PC break for this time. You can't use this program right now. Sorry. How about using the time to read a book? 😊" + English break info line.
- Step 7: App B shows the **German** defaults (by design).
- **German** default texts in ee-kid2's dialog = **Fail** (missing `en` satellite).

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**: app name shown to ee-kid2: ________ · time format in the break line: ________

---

### TC-006-31: The Start menu, Explorer, tray client, lock and sign-out are never limited *(must-run)*

*Verifies AC-27 · Service PC / ee-kid2 · ee-kid2's break entry off*

1. **Start menu** open/close. **Windows+E**; keep it open **2 minutes**; close.
2. Tray icon → *App Infos*; close.
3. **Windows+L**; unlock.
4. `calc` (blocked); **leave the dialog open**; Start → user icon → **Sign out**.

**Expected**: steps 1 to 3 work, nothing closed, no dialog, tray icon stays; step 4 Rechner blocked and sign-out works with the dialog open.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

### TC-006-32: Tray client not running: apps are still blocked, no message later *(must-run)*

*Verifies AC-35 · Service PC / kid1, K-TERM · Benutzer wechseln → `eagleeye-kid`; K-TERM with LOG-WATCH; no dialog*

1. K-TERM: `Get-Process EagleEye.TrayClient -IncludeUserName | Where-Object UserName -like "*\eagleeye-kid" | Stop-Process -Force` → icon gone.
2. `calc`; watch **60 s**.
3. Start → **EagleEye Tray**; 10 s.
4. `calc`; **OK**.
5. LOG-FIND `Rechner|Tray client connected`.

**Expected**: step 2 Rechner closed (≤ 30 s), **no** dialog, also not later; step 3 icon back; step 4 dialog again; log "Blocked start ended: … Rechner …; **not shown (tray client not connected)**", "Tray client connected: … (session N, verified).", "… message shown".

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

## Block F — Persistence, security, time zone, load, deletion · ≈ 50 min (+≈ 45 optional)

### TC-006-33: Re-install keeps rules, texts and levels; enforcement without a parent app; used time is not refilled *(must-run)*

*Verifies AC-18, AC-36 · Service PC / Admin, kid1 · Systeminformationen used up today (TC-006-18); screenshots of kid1's Applikations-Regeln and Zeit-Ende Warnungen in App A*

1. **Close** App A and App B.
2. Admin: `03_Delivery\windows\EagleEye-Setup-0.6.0.exe` again, defaults. LOG-WATCH again; LOG-FIND `App rules loaded|Warning levels loaded`.
3. *Benutzer wechseln* → `eagleeye-kid`: `msinfo32` → **OK**; `calc` → **OK**.
4. Admin: App A → **Regeln** → `eagleeye-kid`; **Einstellungen**.

**Expected**: "App rules loaded: N rules of … apps of 2 accounts, **1 changed budget texts**." and "Warning levels loaded: early **2** min, final **1** min."; both apps blocked without a parent app, Systeminformationen with "Zeit-Budget aufgebraucht" (not refilled); rules, text and levels as in the screenshots.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**: N rules = ____

---

### TC-006-34: Which restricted SYSTEM token the agent runs with (TC-005-26 rerun) *(must-run, security)*

*Security (plan Q-4) · Service PC / Admin, Terminal (Administrator) · kid1 signed in*

1. LOG-FIND `Session agent|write restriction|write-restricted`.

**Expected**:
- "Session agent started in session N (process P)." and "Session agent in session N: diagnostics: user NT-AUTORITÄT\SYSTEM (S-1-5-18), integrity System, privileges SeChangeNotifyPrivilege, write-restricted yes|no".
- SYSTEM, integrity System, **only** `SeChangeNotifyPrivilege`; write-restricted **yes**, or the accepted fallback Warning plus `no`. Both Pass. **Write the variant in Notes.**

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**: write-restricted: yes / no (fallback)

---

### TC-006-35: A time-zone change is logged as a Warning with the old and new date; budgets follow the new day *(must-run)*

*Verifies AC-39 · Service PC / kid1 in front, K-TERM · all breaks off; `notepad` open (23:59); Systeminformationen used up*

1. K-TERM `tzutil /g` → ____________
2. **From 12:30**: `tzutil /s "Line Islands Standard Time"`; **before 11:30**: `tzutil /s "Hawaiian Standard Time"` (11:30–12:30: wait). Time of Enter ____ ; wait 10 s.
3. LOG-FIND `time zone|clock of the PC`; `tzutil /g`.
4. `msinfo32`; after 15 s close it yourself (or **OK** if blocked).
5. `tzutil /s "W. Europe Standard Time"`; 10 s; LOG-FIND `time zone`; `msinfo32` → **OK**.

**Expected**:
- Step 3: **Warning** ≤ 5 s after step 2: "The time zone of the PC changed from W. Europe Standard Time (…) to Line Islands Standard Time (…) [or Hawaiian …]; the local date changed from 10/17/2026 to 10/18/2026 [or 10/16/2026]; daily budgets now use the new day; break times now use the new local time. Session in use: eagleeye-kid."; `tzutil /g` still the new zone.
- Step 4: Systeminformationen **starts** (new day's budget; by design). Notepad stayed open.
- Step 5: second Warning (date back to 10/17/2026); Systeminformationen blocked ("Zeit-Budget aufgebraucht").

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**: zone used: ________ · zone reset: yes / no

---

### TC-006-36: CPU load with 10 allowed apps running *(must-run)*

*Verifies AC-38 · Service PC / kid1; App B · close Notepad*

1. kid1: start once (blocked → **OK**) each of these not yet listed in App B: `taskmgr`, `dxdiag`, `mstsc`, Start → **Einstellungen**, **Microsoft Edge**.
2. App B: **Stunden** `1`, **Minuten** `0`, all seven days for Paint, Zeichentabelle, Rechner, Systeminformationen, Microsoft Edge, Task-Manager, Einstellungen, Remotedesktopverbindung, DirectX-Diagnoseprogramm (Editor stays 23:59). 5 s.
3. kid1: open all ten (`notepad`, `mspaint`, `charmap`, `calc`, `msinfo32`, Edge with a video, `taskmgr`, Einstellungen, `mstsc`, `dxdiag`). Starts as fast as before?
4. Task-Manager → **Details** → the **two** `EagleEye.Service.exe`: CPU about every 10 s for **1 minute**.
5. Video 30 s: stutter?

**Expected**: all ten open, no dialog; sum of both processes on average **below 2 %**; no visible stutter.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**: CPU values: ____________ · starts as fast: yes / no

---

### TC-006-37: Unticked = kept but not applied; deleting the account purges its rules and texts *(must-run)*

*Verifies AC-18 · Service PC / ee-kid2, Admin (App A), Terminal (Administrator) · ee-kid2's Notepad used up (TC-006-30); ee-kid2 signed out*

1. App A *Einstellungen*: untick **ee-kid2**; ≤ 60 s gone from **Regeln → Konto**.
2. `ee-kid2`: `notepad`, use 30 s, close.
3. Admin: tick **ee-kid2**; **Regeln** → `ee-kid2`.
4. `ee-kid2`: `notepad` → **OK**; sign out.
5. Admin: App A **Regeln** → `ee-kid2` budget text `Text für ee-kid2 🙂`; click outside.
6. ```powershell
   Get-CimInstance Win32_UserProfile | Where-Object LocalPath -like "*\ee-kid2" | Remove-CimInstance
   Remove-LocalUser -Name "ee-kid2"
   ```
   Watch App A and the log 60 s; LOG-FIND `Purged`.

**Expected**: step 2 Notepad opens normally (not applied); step 3 ee-kid2 back with Notepad 0:02 all days and its break entry; step 4 blocked; step 6 ≤ **60 s** ee-kid2 gone from **Konto**, log "Purged the budget text of deleted account S-1-5-21-…" and "Purged the break times of deleted account …"; no Error.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

### TC-006-38 (optional): Reboot

*Verifies AC-18, AC-36 · Service PC / kid1, Admin*

1. Restart the PC; sign in as `eagleeye-kid` (no parent app); 30 s.
2. App B: Systeminformationen **Stunden** `0`, **Minuten** `1`; `msinfo32` → **OK**.
3. Admin: App A **Regeln** → `eagleeye-kid`; **Einstellungen**.

**Expected**: Systeminformationen blocked "Zeit-Budget aufgebraucht" (used time kept); rules, text, levels unchanged.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

### TC-006-39 (optional, at midnight): The end at midnight

*Verifies AC-23, OQ-4 · Service PC / kid1; App B · from ≈ 23:53, any evening after Block B; all breaks off; App B: Zeichentabelle 1:00, **today** ticked, **tomorrow unticked**; Editor 23:59 all days*

1. ≈ **23:55**: `charmap` and `notepad`; kid1 in front.
2. Clock at each dialog until 00:01; **OK** each time.
3. **00:01**: `charmap` → **OK**.

**Expected**: Zeichentabelle early at **23:58**, final at **23:59** (≤ +10 s); at **00:00** (≤ +10 s) closed with the budget text + "Zeichentabelle: heute nicht erlaubt."; gone ≤ 00:00:30; Editor keeps running, no warning for it; step 3 blocked.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**: early ____ · final ____ · close ____

---

### TC-006-40 (optional): A clock change is logged as a Warning

*Verifies AC-39 · Service PC / Admin, Terminal (Administrator)*

1. `Set-Date (Get-Date).AddMinutes(10)`; 10 s; LOG-FIND `clock of the PC`.
2. `Set-Date (Get-Date).AddMinutes(-10)`; `w32tm /resync`; LOG-FIND again.

**Expected**: a **Warning** "The clock of the PC changed from … to …; the local date stays 10/17/2026; break times now use the new local time." ≤ 5 s after each change; nothing undone by the service.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

### TC-006-41 (optional, needs a SQLite tool): History of blocked starts and enforced closes in the database

*Verifies AC-37, AC-18 · Service PC / Admin, Terminal (Administrator) · after TC-006-37*

1. ```powershell
   $tmp = "C:\EETest\db"; New-Item -ItemType Directory -Force $tmp | Out-Null
   Copy-Item "$env:ProgramData\EagleEye\EagleEye.Service.db*" $tmp -Force
   sqlite3 -readonly "$tmp\EagleEye.Service.db" "SELECT * FROM BlockedStarts ORDER BY rowid DESC LIMIT 40;"
   sqlite3 -readonly "$tmp\EagleEye.Service.db" "SELECT * FROM WarningSettings;"
   ```

**Expected**: rows with `Kind` `blocked start` / `running app`, `Reason` `not allowed today` / `budget used up` / `break time`, `AfterGrace` 1 for TC-006-18, -19, -26, `Trigger` e.g. `app start`, `budget ran out`, `grace period ended`, `break began`; no row of ee-kid2; `WarningSettings` early 2, final 1, no texts.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

### TC-006-42 (optional): English parent app

*Verifies AC-10, AC-14 (parent side) · a parent PC with English Windows (e.g. LEOSERV, if English); otherwise Skipped*

1. **Rules** → `eagleeye-kid`: **Application rules**. **Settings** → **Time-elapsed warnings**. Hours `24`, Tab.

**Expected**: English labels; unchanged defaults in English ("Attention, you only have 5 minutes left! …", "Attention!!! Only 1 minute left – …"); kid1's budget text exactly as entered; "Enter hours between 0 and 23.".

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**: Windows language of that PC: ________

---

## Cleanup (test plan §7)

- [ ] **LOG-COPY** → `run-01-service-logs`; copies looked through.
- [ ] Time zone back: `tzutil /g` → `W. Europe Standard Time`; clock correct (`w32tm /resync` after TC-006-40).
- [ ] Every break entry of `eagleeye-kid` **off**.
- [ ] Real kid accounts: ticked again **with their rules set** (otherwise every app is blocked for them).
- [ ] `eagleeye-kid` signed out; `ee-kid3` (and `ee-kid2`, if TC-006-37 was not run) removed:
  ```powershell
  foreach ($n in "ee-kid2","ee-kid3") {
    Get-CimInstance Win32_UserProfile | Where-Object LocalPath -like "*\$n" | Remove-CimInstance
    if (Get-LocalUser -Name $n -ErrorAction SilentlyContinue) { Remove-LocalUser -Name $n }
  }
  ```
- [ ] `C:\EETest` removed (TC-006-41).
- [ ] LEOSERV: App B stays installed and paired.

---

## General Feedback

<!-- Anything that does not fit a test case: usability remarks, ideas, surprises. -->

---

## Summary (filled in by TES after evaluation)

| Block | Cases | Pass | Fail | Blocked | Skipped | Not executed |
|---|---|---|---|---|---|---|
| A (TC-006-01..10 must, 11 optional) | 11 | | | | | |
| B (TC-006-12..17 must) | 6 | | | | | |
| C (TC-006-18..20 must, 21..22 optional) | 5 | | | | | |
| D (TC-006-23..26 must, 27..28 optional) | 6 | | | | | |
| E (TC-006-29..32 must) | 4 | | | | | |
| F (TC-006-33..37 must, 38..42 optional) | 10 | | | | | |
| **Total** (32 must, 10 optional) | **42** | | | | | |

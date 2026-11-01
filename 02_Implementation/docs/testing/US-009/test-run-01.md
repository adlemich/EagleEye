# Test Run 01: US-009 — Windows Parent App on Windows 10, and Windows Parent App Parity (0.9.0)

**Test plan**: `02_Implementation/docs/testing/US-009/test-plan.md` (draft 2026-11-01, for approval)
**Prepared by**: TES, 2026-11-01
**Executed by**: Michael
**Execution date(s)**: <!-- fill in, one per session day -->
**Build / installer version**: 0.9.0 — `03_Delivery/windows/EagleEye-Setup-0.9.0.exe` (SHA-256 `4F1EF797…CB1652`), `03_Delivery/windows/EagleEye-ParentApp-Setup-0.9.0.exe` (SHA-256 `780CE6CB…1D88F0`), `03_Delivery/macos/EagleEye-ParentApp-0.9.0.dmg` (SHA-256 `9f397006…c5fdde6`); full hashes in the test plan §2
**Machine(s)**: Service PC (Windows Developer Machine, Windows 11, IP: <!-- fill in, expected 192.168.1.37 -->) · Windows 10 PC (winver: <!-- fill in -->) · MacBook (macOS <!-- fill in -->)
**Accounts (S-5)**: kid1 = <!-- fill in --> · kid2 = <!-- fill in --> (English Windows: yes / no) · kid3 = <!-- fill in -->

---

## How to record results

> **Tick exactly one box per case** (`[x]`): Pass, Fail, Blocked or Skipped. The ticked box is what counts.
>
> - **Observed**: required for **Fail** and **Blocked**: what actually happened (the text you saw, the time on the stopwatch).
> - **Notes**: optional. Timings, remarks, ideas, or "unclear what to expect here". TES forwards these to PRO as requirement feedback.
> - **Evidence**: screenshots and log excerpts into `02_Implementation/docs/testing/US-009/evidence/`, referenced by file name (e.g. `evidence/tc-31-w10-appgroups.png`).
> - **Service log files**: right after any **Fail**, run **LOG-COPY** (below) with `<subfolder>` = `run-01-service-logs\after-TC-009-NN` and write the folder into Notes. At the end of each session day, LOG-COPY with `run-01-service-logs`. Look through the copies before committing (no secrets are logged).
> - **Free-form feedback**: anything that does not fit a case goes into "General Feedback" at the end. You can also just tell TES in chat.
> - **Partial runs are fine.** Unticked cases count as "not executed". Edit this file on either machine (commit and push before you switch machines). When done or stopping, tell TES: *"test run 01 for US-009 is done"* (or "… Session N is done").

## What this run covers

> **72 cases in Sessions 1 to 6** — **59 essential**, 13 **(optional)**: TC-009-15, -18, -25, -35, -36, -44, -47, -64, -65, -66, -70, -71, -72. No regression beyond the story's own regression ACs.
>
> | Session | Cases | Where | Essential (+ optional) time |
> |---|---|---|---|
> | 0 Setup | S-1 to S-12 | Service PC, MacBook, W10 PC | ≈ 35 min |
> | 1 Service update | TC-009-01, -02 | Service PC | ≈ 20 min |
> | 2 Windows app on Windows 11 | TC-009-03 to -25 | Service PC / Admin | ≈ 2 h (+ 25 min) |
> | 3 Windows 10 PC | TC-009-26 to -36 | W10 PC (+ service PC) | ≈ 1 h 20 (+ 15 min) |
> | 4 Mac update, US-008 Mac ACs | TC-009-37 to -48 | MacBook | ≈ 1 h 15 (+ 20 min) |
> | 5 Two parent apps | TC-009-49 to -54 | Service PC + MacBook side by side | ≈ 45 min |
> | 6A Windows app regression | TC-009-55, -56 | Service PC / Admin | ≈ 20 min |
> | 6B Group enforcement (US-008 retest) | TC-009-57 to -67 | Service PC / kids + MacBook | ≈ 1 h 15 (+ 30 min) |
> | 6C Storage and accounts | TC-009-68 to -70 | Service PC + MacBook | ≈ 15 min (+ 10 min) |
> | 6D Optional | TC-009-71, -72 | — | (pilot; + 5 min) |
>
> **Essential ≈ 8 h**, suggested over three days: Sessions 0–2 · 3–5 · 6. **Session 6B must start on a day on which kid1 has not used Editor or Paint yet.**
>
> **Key rules** (details in the test plan §2):
> - **German UI labels** as in the test plan §2 table. A wording difference **between the Windows app and the Mac app is a Fail** (AC-28); a difference from the story's English examples is a Note. "Maus oder dem Trackpad" on Windows is agreed (Q-1): no issue. No "Kopieren" caption while dragging on Windows: by design.
> - **Timing**: change in the page / other app ≤ **5 s**; new app ≤ **15 s**; usage follows ≤ **15 s**; green ≤ 30 s after start, red ≤ 30 s after a service stop, green ≤ 60 s after a start.
> - **Enforcement**: stopwatch **on Enter** (Windows+R); an event "at X of group time" between **X and X + 15 s**; apps gone by **end + 35 s**. **Grace**: lap **C** = your Tab/Enter/drop/click in the Mac app; final warning ≤ C + 15 s; close between **C + 60 s and C + 75 s**; gone ≤ C + 95 s; closed before C + 55 s = Fail.
> - **Used time counts only while the kid's session is unlocked and in front**: no user switch or lock during a timed case unless a step says so.
> - **No mixed versions**: after TC-009-01 do **not** start the Mac app 0.8.0 until TC-009-37.
> - Must **not** appear in the service log: any `[ERR]` line, `could not be terminated`.
> - **Abbreviations**: **W** = Windows app on the service PC (device *Papas PC*), **M** = Mac app, **K-TERM** = elevated terminal inside the kid's session with your admin credentials.

### Commands (Terminal (Administrator) or K-TERM on the service PC)

**LOG-COPY** (repo root):

```powershell
Set-Location <your EagleEye repo folder>
$dst = Join-Path "02_Implementation\docs\testing\US-009\evidence" "<subfolder>"
New-Item -ItemType Directory -Force $dst | Out-Null
Copy-Item "$env:ProgramData\EagleEye\logs\*.log" $dst -Force
Get-ChildItem $dst
```

**LOG-WATCH** (after every service restart or re-install: *Strg+C* and again):

```powershell
$log = Get-ChildItem "$env:ProgramData\EagleEye\logs\EagleEye.Service-*.log" | Sort-Object LastWriteTime | Select-Object -Last 1
Get-Content $log.FullName -Tail 15 -Wait
```

**LOG-FIND**:

```powershell
Select-String -Path "$env:ProgramData\EagleEye\logs\*.log" -Pattern "<pattern>" | Select-Object -Last 15 | ForEach-Object Line
```

Patterns: `App groups loaded` · `app group|app moved|group rule` · `Test` · `Grace period` · `Blocked start` · `App closed`

**Mac connection check** (Terminal on the MacBook; not ping): `nc -z -G 3 192.168.1.37 5443`
**W10 connection check** (PowerShell on the W10 PC): `Test-NetConnection 192.168.1.37 -Port 5443`

---

## Session 0 — Setup (before the service update; details in the test plan §3)

- [ ] **S-1 Installers**: hashes as in test plan §2 (service PC: `Get-FileHash …`; MacBook: `shasum -a 256 03_Delivery/macos/EagleEye-ParentApp-0.9.0.dmg`).
- [ ] **S-2 Starting points**: service PC *Installierte Apps*: EagleEye ______ (expected **0.8.0**), EagleEye Parent App: ______ (expected **none**); service running · Mac app version ______ (expected **0.8.0**) · W10 PC: no EagleEye, no `%LocalAppData%\EagleEye`. Different → **stop**, tell TES.
- [ ] **S-3 Service PC IPv4** (`ipconfig`): ______ (expected 192.168.1.37; otherwise use the real address everywhere).
- [ ] **S-4 Copy to the W10 PC**: both `.exe`; `Get-FileHash` there matches; `Test-NetConnection 192.168.1.37 -Port 5443` → *TcpTestSucceeded: True*.
- [ ] **S-5 Accounts**: kid1, kid2 controlled; kid3 not controlled (header). kid1 and kid2 have used Editor, Paint, Rechner at least once. New accounts created: ______
- [ ] **S-6 kid2 English** (untick kid2 in the Mac app 0.8.0, change the display language as kid2, sign out/in, re-tick): done / not possible ______
- [ ] **S-7 kid1 taskbar clock with seconds**.
- [ ] **S-8 Terminal (Administrator)** open; a second one with LOG-WATCH.
- [ ] **S-9 Warning levels** (Mac app 0.8.0): **Frühe Warnung 3**, **Finale Warnung 1**. Old values: ______ / ______
- [ ] **S-10 Baseline** (Mac app 0.8.0): group **Rechnen** with **Rechner**; kid1 Rechnen **0:30, MO–SO**; other groups kept: ______ ; kid1 used `calc` **2 minutes**; Berichte today **Rechnen 00:02**. Past-day group lines: ______ (or "none"). Screenshots `evidence/s10-appgroups-0.8.0.png`, `s10-grouprules-kid1-0.8.0.png`, `s10-reports-kid1-0.8.0.png`. **Mac app quit (Cmd+Q).**
- [ ] **S-11 SmartScreen** understood (*Weitere Informationen* → *Trotzdem ausführen*).
- [ ] **S-12 All kid accounts signed out.**

---

## Session 1 — Service update 0.8.0 → 0.9.0 (service PC; essential) · ≈ 20 min

### TC-009-01: Service and tray 0.9.0 installed over 0.8.0; groups loaded *(essential)*

*Verifies AC-30 (service), AC-31 (REG-001-01, -02; data loaded), AC-36 · Service PC / Admin, Terminal (Administrator) with LOG-WATCH*

**Precondition**: Session 0 done; all kids signed out; Mac app quit

1. Run `03_Delivery\windows\EagleEye-Setup-0.9.0.exe` (elevated, default settings, SmartScreen as S-11). Note the time.
2. *Einstellungen → Apps → Installierte Apps* → "EagleEye".
3. `services.msc` → **EagleEye Service**.
4. LOG-FIND `App groups loaded`.

**Expected**

- Step 1: the wizard completes without errors, as earlier updates.
- Step 2: **EagleEye 0.9.0** (publisher Michael Adler); no second EagleEye entry.
- Step 3: *Wird ausgeführt*, *Automatisch*, *Lokales System*.
- Step 4: after the install time, a line "App groups loaded: N groups with M apps, K group rules." with N ≥ 1 (Rechnen), M ≥ 1, K ≥ 1 (the same numbers as the last line before the update). No `[ERR]` line since the install.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

### TC-009-02: Tray client 0.9.0: auto-start, App Infos, disconnect and reconnect (REG-001-03 to -05) *(essential)*

*Verifies AC-30 (tray App Infos), AC-31 (REG-001-03 to -05), AC-36 · Service PC / kid1 (+ Terminal (Administrator) in your session, or K-TERM)*

**Precondition**: TC-009-01 done

1. *Benutzer wechseln* → kid1. Hover over the EagleEye tray icon.
2. Right-click → **App Infos**.
3. K-TERM: `Stop-Service -DisplayName "EagleEye Service"`. Watch the icon for 30 s. **App Infos** again.
4. `Start-Service -DisplayName "EagleEye Service"`. Watch the icon for 60 s without touching anything.
5. Sign kid1 out (close K-TERM first).

**Expected**

- Step 1: icon present without a manual start, **green**, tooltip **"EagleEye — Verbunden"**.
- Step 2: **"EagleEye – App Infos"** with **"Server-Version: EagleEye_v0.9"**.
- Step 3: ≤ 30 s **red**, tooltip **"EagleEye — Verbindungsfehler"**; App Infos shows **"Verbindungsfehler: Keine Verbindung zum Server unter localhost:5080 möglich."**
- Step 4: ≤ 60 s **green** again.
- Note: kid1 must not use Editor or Paint now (Session 6B).

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

## Session 2 — Windows app on Windows 11 (service PC / Admin) · ≈ 2 h

Keep **LOG-WATCH** open in the second Terminal (Administrator). Display scaling 150 % (as on your dev machine) unless a step says otherwise.

### TC-009-03: Fresh installation without admin rights; version and publisher *(essential)*

*Verifies AC-33, AC-30 (W11) · Service PC / Admin (non-elevated Explorer)*

**Precondition**: S-2: no "EagleEye Parent App" installed

1. Double-click `03_Delivery\windows\EagleEye-ParentApp-Setup-0.9.0.exe` (SmartScreen as S-11). Go through the wizard with the defaults.
2. Start → type "EagleEye" → **EagleEye Parent App**.
3. *Einstellungen → Apps → Installierte Apps* → "EagleEye Parent App" → **⋯** / details.

**Expected**

- Step 1: a guided wizard in German; **no UAC prompt** (no admin rights asked); it completes without errors.
- Step 2: the app starts from the Start menu without any further step.
- Step 3: **EagleEye Parent App**, version **0.9.0**, publisher **Michael Adler**.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

### TC-009-04: First start: Settings with host dialog; pairing; paired start opens on Berichte *(essential; start page re-confirms M-1)*

*Verifies AC-33 (pairing), AC-10 (start page, paired and not paired) · Service PC / Admin*

**Precondition**: TC-009-03; the app is open for the first time

1. Look at the window after the first start.
2. Host dialog: `192.168.1.37` → **Verbinden**. Read the 6-digit code from the popup **"EagleEye – Eltern-App koppeln"** (or *Ereignisanzeige → Anwendung*, source EagleEye). *Gerätename dieses PCs* **Papas PC** → code → **Koppeln**.
3. Close the app (window **X**). Start it again from the Start menu.

**Expected**

- Step 1: the app opens on **Einstellungen** with the host dialog; status bar red.
- Step 2: no certificate warning or extra step; green **"Verbunden mit 192.168.1.37"**; *Gekoppelt*.
- Step 3: green without a pairing code ≤ 30 s; the app opens on **Berichte**, with **Berichte** selected in the menu.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

### TC-009-05: Data kept by the service update *(essential)*

*Verifies AC-31 (configuration, usage, pairings, groups, members, group rules, past group lines), US-008 AC-38 (re-install of the service) · Service PC / Admin*

**Precondition**: TC-009-04; S-10 screenshots at hand

1. **App Gruppen**: compare with `s10-appgroups-0.8.0.png`.
2. **Regeln** → **Konto** kid1 → *App-Gruppen-Regeln*, *Ruhezeiten*, *Applikations-Regeln*: compare with S-10 (and with what you know of kid1's rules).
3. **Berichte** → kid1: today and the past days with "App-Gruppen".
4. **Einstellungen**: ticked accounts (kid1, kid2 ticked; kid3 not), *Zeit-Ende Warnungen* **3** / **1**.

**Expected**

- Step 1: the same groups with the same members (Rechnen: Rechner).
- Step 2: Rechnen **0:30**, MO to SO; break times and app rules as before.
- Step 3: today **Rechnen 00:02** (±1); past days with the same group lines as noted in S-10 (or none, if none were noted).
- Step 4: as before the update. (The Mac pairing is checked in TC-009-37.)

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

### TC-009-06: Menu entries, order and one highlight *(essential, quick re-confirm of DEV check 2)*

*Verifies AC-10, AC-28 (menu text), US-008 AC-1 · Service PC / Admin*

**Precondition**: app connected

1. Look at the menu.
2. Click through **Einstellungen**, **App Gruppen**, **Regeln**, **Berichte**, then back in the reverse order.

**Expected**

- Step 1: **Einstellungen**, **App Gruppen**, **Regeln**, **Berichte**, in this order.
- Step 2: only the selected entry is highlighted at any time; the entry selected before returns to normal. (Native Windows highlight, plan Q-3.)

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

### TC-009-07: Outlined section headings in the Windows accent colour; buttons distinct *(essential; step 4 optional, M-4)*

*Verifies AC-11, US-008 D-18 · Service PC / Admin*

**Precondition**: app connected; note your current accent colour

1. Open each page and look at every section heading: *Einstellungen* (Darstellung, Serververbindung, Benutzerkonten auf dem EagleEye-PC, Zeit-Ende Warnungen), *App Gruppen* (each group, In keiner Gruppe), *Regeln* (Ruhezeiten, App-Gruppen-Regeln, Applikations-Regeln), *Berichte* (each day heading, App-Gruppen).
2. Compare the headings with the buttons **Gruppe hinzufügen** and **Eintrag hinzufügen**.
3. Screenshot of *Regeln* → `evidence/tc-07-headings.png`.
4. *(optional, M-4 passed)* With the app open: *Einstellungen → Personalisierung → Farben → Akzentfarbe* → another colour (e.g. green). Click into the app. Set the colour back.

**Expected**

- Step 1: every heading is a full-width **frame line in the accent colour**, **no solid fill**, heading text **bold in the normal text colour**.
- Step 2: buttons are clearly distinguishable from headings (0.6.2 Windows button style, plan Q-4).
- Step 4: the frames follow the new colour, at the latest when the window is activated again.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

### TC-009-08: Light and dark, 150 % and 100 %, minimum window size *(essential)*

*Verifies AC-12, US-008 AC-5 (look) · Service PC / Admin*

**Precondition**: app connected; scaling 150 %

1. *Einstellungen → Darstellung* → **Hell**. Go through all four pages, open the name dialog (*App Gruppen* → **Gruppe hinzufügen** → **Abbrechen**) and the "Verschieben nach …" list (**≡** → **Abbrechen**).
2. **Dunkel**: the same.
3. Make the window as small as possible (minimum 900 × 600). Scroll each page to the end.
4. *Einstellungen → System → Bildschirm → Skalierung* **100 %**: glance at all four pages. Back to **150 %**.

**Expected**

- Steps 1, 2, 4: all pages, dialogs and the status bar readable; no text cut off; explanatory texts wrap; checkboxes ticked/unticked clearly visible; trash-can icon visible.
- Step 3: the window does not get smaller than 900 × 600; the pages scroll; every control reachable. On *Regeln* the table may scroll sideways at 150 % (known, R-4: not a Fail).

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

### TC-009-09: App Groups page content *(essential)*

*Verifies AC-13, AC-28, US-008 AC-2, AC-3, D-14 · Service PC / Admin*

**Precondition**: app connected; the groups of TC-009-05

1. **App Gruppen**: read the page from top to bottom.
2. Hover over a few app entries.
3. **Gruppe hinzufügen** → `School` → **OK** (an empty group).

**Expected**

- Step 1: the explanation text exactly as in §2 (with "Maus oder dem Trackpad": not an issue, Q-1); **Gruppe hinzufügen**; one section per group, sorted alphabetically ignoring case; **In keiner Gruppe** last; each app **once**, with its display name as on *Berichte* (e.g. Editor, Paint, Rechner), sorted alphabetically in each section; **Windows Explorer not listed**; **no account selection** on the page.
- Step 2: tooltip with the program path.
- Step 3: section **School** in its alphabetical place with **"Keine Apps in dieser Gruppe. Apps hierher ziehen."**

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

### TC-009-10: App Groups page states *(essential)*

*Verifies AC-13 (page states), US-008 AC-5, D-15 · Service PC / Admin, Terminal (Administrator)*

**Precondition**: app on *App Gruppen*

1. `Stop-Service -DisplayName "EagleEye Service"`. Look at the page and the status bar.
2. `Start-Service -DisplayName "EagleEye Service"`. Watch the page while it reconnects.
3. *Einstellungen*: untick **kid1** and **kid2** (no account under control). **App Gruppen**.
4. Tick **kid1** and **kid2** again. **App Gruppen**.

**Expected**

- Step 1: red **"Nicht verbunden mit 192.168.1.37"** ≤ 30 s; the page shows **Keine Daten verfügbar** (no stale entries that look editable).
- Step 2: green ≤ 60 s; the page shows **Wird geladen …** briefly, then the groups again.
- Step 3: **"Keine Konten unter Elternkontrolle. Konten unter Einstellungen auswählen."**
- Step 4: the page with groups and apps as before; kid1's and kid2's rules unchanged (glance at *Regeln*).

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

### TC-009-11: Pencil and trash-can buttons on the group headings *(essential, quick re-confirm of M-3)*

*Verifies AC-14, US-008 D-19 · Service PC / Admin*

**Precondition**: groups Rechnen and School exist

1. Look at the heading of **School** and of **In keiner Gruppe**, in **Hell** and **Dunkel**.
2. Hover over the pencil, then the trash can.
3. Click the pencil → **Abbrechen**. Click the trash can → **Abbrechen**.

**Expected**

- Step 1: each group heading shows the name, a **pencil** and a **trash-can** icon button, visible in both modes (thin native frame around them: known, R-3). **In keiner Gruppe** has neither.
- Step 2: tooltips **Umbenennen** and **Löschen**.
- Step 3: the pencil opens **App-Gruppe umbenennen**, the trash can the delete confirmation; Abbrechen changes nothing.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

### TC-009-12: New group: dialog, name rules, emoji, Cancel and Escape *(essential)*

*Verifies AC-15 (create, name rules), AC-28, US-008 AC-7, AC-8 · Service PC / Admin*

**Precondition**: groups Rechnen and School exist

1. **Gruppe hinzufügen** → read the dialog → **Abbrechen**. Again → **Escape**.
2. **Gruppe hinzufügen** → **OK** with an empty field.
3. Type 3 spaces → **OK**.
4. Type 41 characters (`12345678901234567890123456789012345678901`) → **OK**.
5. Type `school` → **OK**.
6. Type `  Games  ` (spaces before and after) → **OK**.
7. **Gruppe hinzufügen** → type `Musik ` then **Windows+.** → pick 🎮 → **OK**.

**Expected**

- Step 1: dialog **Neue App-Gruppe** with field **Name**, buttons **OK** and **Abbrechen**; Abbrechen and Escape create nothing.
- Steps 2 to 4: the dialog comes back with **"Bitte einen Namen mit 1 bis 40 Zeichen eingeben."** and the typed text (step 4: the 41 characters still there).
- Step 5: the dialog comes back with **"Es gibt bereits eine Gruppe mit diesem Namen."**
- Step 6: section **Games** (trimmed) in its alphabetical place, empty.
- Step 7: section **Musik 🎮** with the emoji shown as an emoji, in its alphabetical place.
- *Regeln* (glance, kid1 and kid2): **Games** and **Musik 🎮** with **0:00**, no day ticked.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

### TC-009-13: Rename and delete *(essential)*

*Verifies AC-15 (rename, delete), AC-28, US-008 AC-9, AC-10, D-16 · Service PC / Admin*

**Precondition**: TC-009-12; move **Paint** into **Games** (any way) and set kid1's own rule for Paint in *Applikations-Regeln* to something you will recognise **before** moving it (e.g. 0:17, MO)

1. Pencil on **Games** → look at the field → **OK** without change.
2. Pencil on **Games** → `Gaming` → **OK**.
3. Pencil on **Gaming** → `Games` → **Enter**.
4. Trash can on **Games** → read the dialog → **Abbrechen**.
5. Trash can on **Games** → **Löschen**.
6. *Regeln* → kid1 → *Applikations-Regeln*: Paint.
7. Delete **Musik 🎮** and **School** (trash can → **Löschen**).

**Expected**

- Step 1: dialog **App-Gruppe umbenennen**, filled with **Games**; OK with the unchanged name is accepted, nothing changes.
- Steps 2, 3: the section is renamed and re-sorted; Paint stays in it.
- Step 4: title **App-Gruppe löschen**, text **"Gruppe „Games“ löschen? Ihre Apps kommen nach „In keiner Gruppe“ und verwenden wieder ihre eigenen Applikations-Regeln."**, buttons **Löschen** and **Abbrechen**; Abbrechen keeps the group.
- Step 5: **Games** is gone; Paint is under **In keiner Gruppe**.
- Step 6: Paint is listed again with **its own earlier rule** (0:17, MO).
- Step 7: only **Rechnen** is left. Set Paint's own rule back to what it was.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

### TC-009-14: Drag-and-drop with the mouse; highlight; small window *(essential)*

*Verifies AC-16 (mouse), AC-12 (drop highlight cleared, both modes), US-008 AC-11, AC-13 · Service PC / Admin, mouse*

**Precondition**: create **Games** and **School** again (empty); Rechnen holds Rechner

1. Drag **Paint** (hold its **name**) from *In keiner Gruppe* onto **Games**. While dragging, move slowly over **School**, **Rechnen** and *In keiner Gruppe* before dropping.
2. Drag **Paint** from **Games** onto the empty **School**.
3. Drag **Paint** onto **School** again (own section) and release.
4. Drag **Paint** and release outside any section (on the explanation text).
5. Drag **Paint** back onto **In keiner Gruppe**.
6. Switch *Darstellung* to the other mode; repeat step 1 and step 5 once.
7. Make the window small (900 × 600) so the page scrolls; drag **Editor** onto **Games** and back.

**Expected**

- Steps 1, 2, 5, 7: the app moves to the target section; it is in exactly one section (gone from the old one).
- While dragging: the section under the pointer is highlighted, the highlight moves with the pointer, only one section at a time. No "Kopieren" caption next to the pointer (removed on purpose, §8.3: not a Fail if missing).
- Steps 3, 4: nothing changes.
- After every drop or release: no highlight remains (both modes).
- Note: dragging works on the app's **name**; "≡" is not a drag handle (story UI note: no issue).

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

### TC-009-15 (optional): Drag-and-drop with the touchpad *(optional, M-2 passed)*

*Verifies AC-16 (touchpad) · Service PC / Admin, touchpad (skip if the PC has none)*

**Precondition**: TC-009-14

1. With the touchpad: drag **Paint** onto **Games**, then back onto **In keiner Gruppe**.

**Expected**

- As TC-009-14 steps 1 and 5, including the highlight that is cleared afterwards.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

### TC-009-16: "≡" and right-click → "Verschieben nach …" *(essential)*

*Verifies AC-17, AC-28, US-008 AC-12, D-9 · Service PC / Admin*

**Precondition**: groups Games, Rechnen, School; Paint under *In keiner Gruppe*

1. **≡** on **Paint** → read the list → choose **Games**.
2. **≡** on **Paint** (now in Games) → read the list → **Abbrechen**.
3. Right-click on **Paint** → **Verschieben nach …** → read the list → choose **In keiner Gruppe**.
4. Right-click on **Editor** → **Verschieben nach …** → press **Escape**.

**Expected**

- Step 1: the list shows **Games**, **Rechnen**, **School** (no "In keiner Gruppe", because Paint is in no group) and **Abbrechen**; Paint moves to Games.
- Step 2: the list shows **Rechnen**, **School**, **In keiner Gruppe** (not Games) and **Abbrechen**; Abbrechen moves nothing.
- Step 3: the right-click shows one item **Verschieben nach …**, which opens the same list (D-9); Paint moves to *In keiner Gruppe*.
- Step 4: nothing moves.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

### TC-009-17: Every change saved at once; log entries with the device name *(essential)*

*Verifies AC-18 (saved, ≤ 5 s, log), US-008 AC-14 · Service PC / Admin, Terminal (Administrator)*

**Precondition**: TC-009-12 to TC-009-16 done (they made creates, renames, deletes and moves)

1. During TC-009-12 to -16 you saw each change in the page; was any change slower than about 5 s, or did any section jump back? (answer in Notes)
2. Close the app and start it again → **App Gruppen**.
3. LOG-FIND `app group|app moved`.

**Expected**

- Step 1: every change showed ≤ 5 s, no "Save" button exists.
- Step 2: the page shows the last state (Games, Rechnen, School; Paint under *In keiner Gruppe*).
- Step 3: Information lines such as "app group created: Games", "app group renamed: Games → Gaming", "app group deleted: …", "app moved: Paint → Games", "app moved: Paint → not in any group", naming **Papas PC**.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

### TC-009-18 (optional): The service cannot store a change *(optional)*

*Verifies AC-18 (error path), US-008 AC-14 · Service PC / Admin, Terminal (Administrator)*

**Precondition**: all kids signed out; app on *App Gruppen*

1. Terminal (Administrator): `resmon` → **CPU** → right-click `EagleEye.Service.exe` → **Prozess anhalten**.
2. Within 10 s: **≡** on **Paint** → **School**. Wait up to 15 s.
3. At the latest 25 s after step 1: `resmon` → **Prozess fortsetzen**. Wait 30 s.

**Expected**

- Step 2: **"Die Änderung konnte nicht gespeichert werden. Bitte erneut versuchen."**; Paint returns to its stored section.
- Step 3: green; the page shows the stored state (Paint where the last "app moved" log line says; write which in Notes).

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

### TC-009-19: Rules: the "App-Gruppen-Regeln" section *(essential)*

*Verifies AC-19, AC-28, US-008 AC-15 · Service PC / Admin*

**Precondition**: groups Games (empty or Paint), Rechnen (Rechner), School (empty)

1. **Regeln** → **Konto** kid1. Read the page from top to bottom.
2. **Konto** kid2: *App-Gruppen-Regeln*.

**Expected**

- Step 1: **App-Gruppen-Regeln** between **Ruhezeiten** and **Applikations-Regeln**, outlined like the other headings; the explanation as in §2; table **App-Gruppe** · **Stunden** · **Minuten** · **MO** to **SO**; rows sorted by name (Games, Rechnen, School); below each name in smaller text its apps, separated by commas ("Rechner"), or **(keine Apps)**; Rechnen 0:30, MO to SO; Games and School **0:00**, no day.
- Step 2: kid2: every group **0:00**, no day (only kid1's Rechnen rule was set in S-10).
- The "Noch keine App-Gruppen …" text needs a state without groups: optional TC-009-72.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

### TC-009-20: Editing a group rule: validation, ticks, Tab/Enter, flush on page and account change *(essential)*

*Verifies AC-20, US-008 AC-16, US-007 AC-32 · Service PC / Admin, LOG-WATCH*

**Precondition**: **Regeln** → kid1

1. **Games**: **Stunden** `24` → **Tab**. Then `abc` (if letters can be typed).
2. **Games**: **Minuten** `60` → **Tab**.
3. **Games**: **Stunden** `1`, **Minuten** `0` → **Enter**. Tick **SA** and **SO**. Watch the log.
4. **Games**: **Minuten** `15` and **immediately** click **Berichte** (no Tab). Back to **Regeln** → kid1.
5. **Games**: **Minuten** `20` and **immediately** switch **Konto** to kid2. Back to kid1.
6. Set **Games** back to **0:00**, no day.

**Expected**

- Step 1: **"Bitte Stunden zwischen 0 und 23 eingeben."**; the stored value stays (letters not accepted or message: both Pass).
- Step 2: **"Bitte Minuten zwischen 0 und 59 eingeben."**
- Step 3: stored ≤ 5 s; log line with "group rule changed: kid1, Games, 1:00, Sa Su" (account name as on your PC; wording differences are Notes), naming **Papas PC**.
- Steps 4, 5: the typed value is saved (1:15, then 1:20), not lost.
- Step 6: stored.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

### TC-009-21: "Applikations-Regeln" lists only ungrouped apps; own rule comes back *(essential)*

*Verifies AC-21, AC-28, US-008 AC-17 · Service PC / Admin*

**Precondition**: Paint under *In keiner Gruppe*; note kid1's own rule for Paint

1. **Regeln** → kid1 → *Applikations-Regeln*: read the explanation; is **Rechner** listed? Is **Paint** listed?
2. **App Gruppen**: move **Paint** into **School**. **Regeln** → kid1.
3. **App Gruppen**: move **Paint** back to **In keiner Gruppe**. **Regeln** → kid1.

**Expected**

- Step 1: the explanation ends with **"Apps in einer App-Gruppe werden unter App-Gruppen-Regeln eingestellt."**; **Rechner is not listed** (it is in Rechnen); Paint is listed; the budget text label below the table is unchanged.
- Step 2: Paint is gone from *Applikations-Regeln*; under *App-Gruppen-Regeln* School shows "Paint" below its name.
- Step 3: Paint is back with **its own rule as before**.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

### TC-009-22: "Wird geladen …" until both areas are loaded; no flash of grouped apps *(essential)*

*Verifies AC-21 (loading), US-008 D-13 · Service PC / Admin, Terminal (Administrator)*

**Precondition**: **Regeln** → kid1 open; Rechner in Rechnen

1. `Restart-Service -DisplayName "EagleEye Service"`. Watch *App-Gruppen-Regeln* and *Applikations-Regeln* until both show data.
2. Close the app. Start it, click **Regeln** at once and watch the same.

**Expected**

- Steps 1, 2: both sections show **Wird geladen …** until their data is there; **Rechner never appears** under *Applikations-Regeln*, not even briefly.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

### TC-009-23: Reports: "App-Gruppen" lines per day *(essential)*

*Verifies AC-22, AC-28, US-008 AC-35 · Service PC / Admin*

**Precondition**: kid1 has Rechnen 00:02 today (S-10)

1. **Berichte** → kid1: today's block, then the older days.

**Expected**

- Today: the app table unchanged (**App** · **Nutzung (HH:MM)**), below it **App-Gruppen** with **App-Gruppe** · **Nutzung (HH:MM)** and **Rechnen 00:02** (±1). Groups without time today (Games, School) are not listed.
- A day without group time shows **no** "App-Gruppen" part.
- The heading "App-Gruppen" is outlined like the day headings.
- Sorting by usage and "same as in the Mac app" are checked in TC-009-58 (two groups with time) and TC-009-52.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

### TC-009-24: Reports follow a rename and today's usage, without "Wird geladen …" *(essential; re-confirms M-5)*

*Verifies AC-23 (Windows part), AC-34 (Reports today's usage), US-008 AC-36, AC-37, D-12 · Service PC / Admin, kid1*

**Precondition**: TC-009-23; **Berichte** → kid1 open

1. **App Gruppen** → rename **Rechnen** → `Rechnen 2`. **Berichte** → kid1: today and past days.
2. Rename back to **Rechnen**. Leave **Berichte** → kid1 **open**; note today's Rechner and Rechnen values.
3. *Benutzer wechseln* → kid1 → Windows+R `calc` → **1 minute** → close it. *Benutzer wechseln* → your account. **Do not touch the app**; look at Berichte for 15 s.

**Expected**

- Step 1: all days show **Rechnen 2** instead of Rechnen; the page did not show **Wird geladen …**.
- Step 3: without any action, today shows Rechner and **Rechnen** about **1 minute more** (≤ 15 s after you are back). Kid1's Rechner usage: write it down (≈ 00:03), Session 6B uses it.
- Note: the timed "≤ 15 s while the kid uses the app" is checked on the Mac (TC-009-58), because only the kid's session in front counts.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

### TC-009-25 (optional): English on Windows 11 *(optional; English is fully checked on Windows 10, TC-009-32)*

*Verifies AC-28 (W11, English) · Service PC / Admin*

**Precondition**: English display language installed for your account; you are willing to sign out

1. *Einstellungen → Zeit und Sprache → Sprache und Region → Windows-Anzeigesprache* → **English (United States)** → sign out and in. Start the app; look at all four pages, the name dialog, the delete dialog and "Move to …".
2. Back to **Deutsch**, sign out and in.

**Expected**

- Step 1: every text in English with the wording of TC-009-47 / §2 (Mac reference): "App Groups", "Add group", "Not in any group", "Move to …", "App group rules", "(no apps)", "App groups" / "App group" / "Usage (HH:MM)"; no German leftovers, no internal keys.
- Step 2: German again.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

## Session 3 — Windows 10 PC · ≈ 1 h 20

You work on the W10 PC; the service PC is needed for the pairing code (TC-009-28) and the service stop (TC-009-29). On the service PC keep the W11 app **closed** or on *Einstellungen* (no edits).

### TC-009-26: Windows 10 version; the service installer refuses Windows 10 *(essential, quick re-confirm of pre-check rows 1, 2)*

*Verifies AC-5 · W10 PC / your account*

**Precondition**: S-4 done

1. Windows+R `winver`.
2. Double-click `EagleEye-Setup-0.9.0.exe`.
3. *Einstellungen → Apps → Apps & Features*: search "EagleEye".

**Expected**

- Step 1: **Windows 10, Version 22H2**, build **19045.x**.
- Step 2: a message that a newer Windows (Windows 11) is needed (Inno Setup's built-in text, German or English); the setup ends; nothing installed.
- Step 3: no EagleEye entry.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

### TC-009-27: Windows app installs without admin rights and without a separate .NET runtime *(essential)*

*Verifies AC-2, AC-3 (Apps & Features entry), AC-30 (W10) · W10 PC / your account*

**Precondition**: TC-009-26

1. *Apps & Features*: search "NET" and "Windows App Runtime"; note what is listed (Notes).
2. Double-click `EagleEye-ParentApp-Setup-0.9.0.exe` (SmartScreen: *Weitere Informationen* → *Trotzdem ausführen*). Defaults. Note the install folder the wizard shows (if it shows one).
3. Start → **EagleEye Parent App**.
4. *Apps & Features* → **EagleEye Parent App**.

**Expected**

- Step 1: whatever is there; nothing is installed for EagleEye.
- Step 2: guided wizard in German, **no UAC prompt**; it completes. It does **not** ask for or install a .NET runtime (step 1's list unchanged afterwards).
- Step 3: the app starts on **Einstellungen** with the host dialog, without any further step.
- Step 4: **EagleEye Parent App**, **0.9.0**, **Michael Adler**.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

### TC-009-28: Connection: unreachable host, then pairing with a wrong and the right code *(essential)*

*Verifies AC-6 (host dialog, error naming the host, IPv4, TLS without warning, pairing, wrong code, status bar), AC-10 (not paired → Settings) · W10 PC; service PC / Admin (code popup)*

**Precondition**: TC-009-27, host dialog open

1. Host `192.168.1.250` (an address with no PC; or any unused one) → **Verbinden**. Wait up to 30 s.
2. Host `192.168.1.37` → **Verbinden**. Read the status bar and the pairing form.
3. *Gerätename dieses PCs* **Windows 10 PC**; enter the code from the service PC's popup with the **last digit changed** → **Koppeln**.
4. Enter the right code → **Koppeln**.

**Expected**

- Step 1: a connection error that **names the host**, e.g. "Verbindungsfehler: Keine Verbindung zum EagleEye-Dienst unter 192.168.1.250 möglich."; red.
- Step 2: no certificate warning, no extra step; pairing form with **Kopplungscode** and **Gerätename dieses PCs**; red **"Verbindung zu 192.168.1.37 wird hergestellt …"** (never "Verbunden" before pairing).
- Step 3: **"Der Kopplungscode ist falsch."** (or same meaning); not paired; red.
- Step 4: green **"Verbunden mit 192.168.1.37"**, *Gekoppelt*.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

### TC-009-29: Reconnect timings from Windows 10 *(essential)*

*Verifies AC-6 (US-002 AC-21, AC-22) · W10 PC; service PC / Terminal (Administrator)*

**Precondition**: TC-009-28; stopwatch

1. Close the app; start it again (stopwatch).
2. Service PC: `Stop-Service -DisplayName "EagleEye Service"` (stopwatch).
3. `Start-Service -DisplayName "EagleEye Service"` (stopwatch).

**Expected**

- Step 1: green ≤ **30 s**, no pairing code; opens on **Berichte**.
- Step 2: red **"Nicht verbunden mit 192.168.1.37"** ≤ **30 s**.
- Step 3: green ≤ **60 s** without any action.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

### TC-009-30: All four pages on Windows 10 (spot check incl. the three ways to move) *(essential)*

*Verifies AC-7, AC-16 (W10: mouse, touchpad), AC-18 (log with the W10 device name) · W10 PC; service PC / Terminal (Administrator) for the log*

**Precondition**: TC-009-29; the group **Games** exists (from Session 2) — if not, step 3 creates it

1. **Einstellungen**: untick **kid2**, tick it again.
2. **Einstellungen → Zeit-Ende Warnungen**: **Frühe Warnung** `4` → **Tab**; then back to `3` → **Tab**.
3. **App Gruppen**: (if needed **Gruppe hinzufügen** `Games`); move **Paint** into **Games** by **dragging with the mouse**, back; with the **touchpad** (if the PC has one), back; with **≡**, back; with **right-click → Verschieben nach …**; leave it in Games.
4. **Regeln** → kid1: **Games** **Stunden** `0`, **Minuten** `30`, tick today → **Tab**. *Ruhezeiten* → **Eintrag hinzufügen** (leave **An/Aus** off).
5. **Berichte** → kid1: today.
6. Undo: delete the break entry (trash can); **Games** back to 0:00, no day; move **Paint** back to *In keiner Gruppe*.
7. Service PC: LOG-FIND `Windows 10 PC`.

**Expected**

- Steps 1 to 4, 6: every change shows ≤ 5 s and stays (no jump back); drag highlight as in TC-009-14 and cleared.
- Step 5: today's app lines and **App-Gruppen** (Rechnen) for kid1, as on the W11 app.
- Step 7: the account, warning, group, group rule and break changes are logged with **Windows 10 PC**.
- Touchpad missing → write "no touchpad" in Notes (not a Fail).

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

### TC-009-31: Look on Windows 10: menu, headings, light/dark, 100 %/150 %, minimum size, emojis *(essential)*

*Verifies AC-8, AC-10 (W10 menu), AC-11 (W10 headings) · W10 PC*

**Precondition**: connected

1. Menu: four entries in order; click through all four twice.
2. Look at every section heading (as TC-009-07 step 1); change the accent colour (*Einstellungen → Personalisierung → Farben*), click into the app, set it back.
3. *Darstellung* **Hell**, then **Dunkel**: all four pages.
4. Scaling **150 %** (*Einstellungen → System → Bildschirm*), then **100 %**: all four pages; window at its minimum size, scroll to the end of each page.
5. **App Gruppen** → **Gruppe hinzufügen** → `Spiele 😀` (Windows+. for the emoji) → **OK**. **Regeln** → kid1 → *Anzeige Text wenn Budget aufgebraucht:* add ` 🎮` at the end → **Tab**. Look at both. Then delete the group and remove the emoji again.
6. Screenshot of *App Gruppen* in **Dunkel** at 150 % → `evidence/tc-31-w10-appgroups.png`.

**Expected**

- Step 1: **Einstellungen**, **App Gruppen**, **Regeln**, **Berichte**; one highlight only.
- Step 2: outlined headings in the accent colour, text in the normal colour; frames follow the new colour at the latest after re-activating the window.
- Steps 3, 4: no text cut off, no control unreachable, checkboxes and the trash-can icon visible; window ≥ 900 × 600 with scrolling pages. Fonts may look slightly different from Windows 11 (no Segoe UI Variable; known).
- Step 5: 😀 and 🎮 shown as emojis (a box or a split symbol = Fail for these two common emojis).

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

### TC-009-32: Language on Windows 10: English, then German *(essential)*

*Verifies AC-9 (German, English), AC-28 (W10: all new texts in both languages, same wording as the Mac) · W10 PC*

**Precondition**: English language pack on the W10 PC (*Einstellungen → Zeit und Sprache → Sprache → Sprache hinzufügen*); kid1's break display text, budget text and warning texts are the **defaults** (check in *Regeln* / *Einstellungen*; if one was changed, write which in Notes)

1. *Windows-Anzeigesprache* → **English (United States)** → sign out and in.
2. Start the app; go through: menu; *App Groups* (explanation, Add group, Not in any group, empty-section text, "≡" list "Move to …", tooltips of pencil and trash can); name dialog with both messages (empty, `rechnen`); delete dialog (open on a new group, Cancel; delete that group); the save error text is not producible here (skip); *Rules* (App group rules: heading, explanation, columns, "(no apps)", the sentence under Application rules); *Reports* (App groups / App group / Usage (HH:MM)); *Settings* default warning texts and *Rules* default break and budget texts.
3. *Windows-Anzeigesprache* → **Deutsch** → sign out and in; start the app; glance at the same places.

**Expected**

- Step 2: every text English, with the wording of §2 (= Mac app 0.8.0; Mac reference TC-009-47); no German leftovers, no third language, no internal keys. The **unchanged default** kid texts are shown in English (e.g. "You have used up your time! Sorry, that's it for now.").
- Step 3: all German again, defaults in German.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

### TC-009-33: Repair keeps pairing and appearance *(essential)*

*Verifies AC-3 (repair) · W10 PC*

**Precondition**: connected; *Darstellung* set to **Dunkel** (note it)

1. Close the app. Run `EagleEye-ParentApp-Setup-0.9.0.exe` again with defaults.
2. Start the app.

**Expected**

- Step 1: completes without errors.
- Step 2: green ≤ 30 s **without a pairing code**; device still **Windows 10 PC**; **Dunkel** kept.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

### TC-009-34: Uninstall removes everything *(essential)*

*Verifies AC-3 (uninstall) · W10 PC*

**Precondition**: TC-009-33; the app is still **paired** (do **not** use *Kopplung aufheben* first; see §8 Q-7)

1. Close the app. *Apps & Features* → **EagleEye Parent App** → **Deinstallieren**.
2. Start menu: search "EagleEye".
3. Explorer: `%LocalAppData%\EagleEye`, and the install folder of TC-009-27 step 2 (if noted; otherwise `%LocalAppData%\Programs` → anything "EagleEye").

**Expected**

- Step 1: completes without errors; no entry in *Apps & Features*.
- Step 2: no entry.
- Step 3: neither folder exists (no program files, no connection or pairing data).

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

### TC-009-35 (optional): Older Windows 10 or 32-bit Windows is refused *(optional; only if such a Windows exists)*

*Verifies AC-4 · a PC or VM with Windows 10 older than 22H2 (build < 19045) or 32-bit Windows*

**Precondition**: such a Windows is available; otherwise tick **Skipped** and write "not available"

1. Run `EagleEye-ParentApp-Setup-0.9.0.exe`.

**Expected**

- A message that this Windows version is not supported (Inno Setup's built-in text); nothing installed.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

### TC-009-36 (optional): Any other display language → German *(optional)*

*Verifies AC-9 (other language) · W10 PC*

**Precondition**: a third language pack (e.g. Français) installed; do it **before** TC-009-34 if you run it

1. Display language **Français** → sign out and in → start the app. Then back to Deutsch.

**Expected**

- The app is **German**.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

## Session 4 — Mac app update 0.8.0 → 0.9.0 and the US-008 Mac ACs (MacBook) · ≈ 1 h 15

The Mac cases are **quick re-confirms** of your 12-point check on 0.8.0 (US-008 report §8.6), now on 0.9.0, because AC-35 asks for them in full. Use the W11 app only to read the code popup and the log. German macOS.

### TC-009-37: Update over 0.8.0; Local Network off/on; pairing and appearance kept; version *(essential)*

*Verifies AC-32, AC-30 (Mac) · MacBook; service PC (log)*

**Precondition**: Mac app 0.8.0 quit since S-10; note its appearance setting (Hell/Dunkel)

1. Open `03_Delivery/macos/EagleEye-ParentApp-0.9.0.dmg` → drag **EagleEye Parent App** onto **Programme** → **Ersetzen**.
2. Start the app. If macOS blocks it: *Systemeinstellungen → Datenschutz & Sicherheit → Dennoch öffnen*.
3. If the app does not become green within 30 s: quit it (**Cmd+Q**), *Systemeinstellungen → Datenschutz & Sicherheit → Lokales Netzwerk* → switch **every EagleEye entry off and on again**, start the app. Answer a Keychain prompt with **Immer erlauben**. Write in Notes which of these steps were needed and how many Keychain prompts appeared.
4. Terminal: `nc -z -G 3 192.168.1.37 5443` (not ping).
5. Finder → *Programme* → EagleEye Parent App → **Informationen**. App menu **EagleEye Parent App → Über EagleEye Parent App**.

**Expected**

- Step 3 (or already step 2): green **"Verbunden mit 192.168.1.37"** without a new pairing code; the same device name as before; appearance as before. At most the Keychain prompts accepted in US-007 OQ-3 (one-time "Immer erlauben"); having to pair again = **Fail**.
- Step 4: "succeeded" / "Connection to 192.168.1.37 port 5443 … succeeded!".
- Step 5: both show **0.9.0**.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

### TC-009-38: Menu, start page, outlined headings, page states and look *(essential, re-confirm)*

*Verifies AC-35 (US-008 AC-1, AC-5, D-15, D-18) · MacBook*

**Precondition**: TC-009-37

1. Menu order; Cmd+Q and start: which page opens?
2. Every page: headings outlined in the macOS accent colour, text normal colour.
3. *Darstellung* **Hell** and **Dunkel**; *Systemeinstellungen → Bedienungshilfen → Anzeige* → larger text (as in US-008 §8.6 point 10): *App Gruppen* readable.
4. W11 app: *Einstellungen* untick kid1 and kid2 → Mac *App Gruppen*; tick both again.

**Expected**

- Step 1: **Einstellungen**, **App Gruppen**, **Regeln**, **Berichte**; opens on **Berichte**.
- Step 2: outlined (D-18).
- Step 3: readable, nothing cut off.
- Step 4: **"Keine Konten unter Elternkontrolle. Konten unter Einstellungen auswählen."** (≤ 5 s), then the page again.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

### TC-009-39: App Gruppen content *(essential, re-confirm)*

*Verifies AC-35 (US-008 AC-2, AC-3, D-14), AC-28 (Mac = Windows wording) · MacBook (and the W11 app side by side, for the wording)*

**Precondition**: groups Games, Rechnen, School exist

1. Read the page top to bottom; hover over an app.
2. Compare the explanation and the section texts word by word with the W11 app's page.

**Expected**

- Step 1: as TC-009-09 (explanation, button, sorted sections, In keiner Gruppe last, each app once, tooltip with the program path, no Windows Explorer, no account selection, empty text for School).
- Step 2: identical wording (a difference = Fail, AC-28).

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

### TC-009-40: Create, rename, delete with the icon buttons *(essential, re-confirm)*

*Verifies AC-35 (US-008 AC-7 to AC-10, D-16, D-19) · MacBook*

**Precondition**: TC-009-39

1. **Gruppe hinzufügen**: empty → **OK**; `games` → **OK**; 41 characters → **OK**; **Escape**. Then `Musik 🎮` (Character Viewer: Ctrl+Cmd+Space) → **OK**.
2. Pencil on **Musik 🎮** (tooltip?) → `Musik` → **OK**. Trash can (tooltip?) on **Musik** → read → **Abbrechen**; again → **Löschen**.

**Expected**

- Step 1: the two messages of TC-009-12 with the typed text kept; Escape creates nothing; **Musik 🎮** created and sorted.
- Step 2: tooltips **Umbenennen** / **Löschen**; pencil and trash-can icons visible in the current mode; rename works; delete dialog with title **App-Gruppe löschen** and the question of §2; Abbrechen keeps, Löschen removes.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

### TC-009-41: Moving: drag with mouse and trackpad, "≡", right-click *(essential, re-confirm)*

*Verifies AC-35 (US-008 AC-11 to AC-13, D-9) · MacBook, trackpad (+ mouse if you have one)*

**Precondition**: Paint under *In keiner Gruppe*

1. Trackpad: drag **Paint** onto **School** (empty), then onto **Games**, then onto **In keiner Gruppe**; once onto its own section; once outside any section. Mouse: the same once (skip if no mouse).
2. Make the window short so the page scrolls; drag again.
3. **≡** on **Paint** → read the list → **Games**. Two-finger click on **Paint** → **Verschieben nach …** → **In keiner Gruppe**. **≡** → **Abbrechen**.

**Expected**

- As TC-009-14 and TC-009-16: moves as expected, own section and outside change nothing, highlight follows and is always cleared, the list excludes the current section and includes *In keiner Gruppe* only for a grouped app.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

### TC-009-42: Mac changes saved at once; log names the Mac *(essential, re-confirm)*

*Verifies AC-35 (US-008 AC-14 save and log) · MacBook; service PC / Terminal (Administrator)*

**Precondition**: TC-009-40, -41 done

1. Cmd+Q, start the app → **App Gruppen**.
2. Service PC: LOG-FIND `app group|app moved` → lines from the Mac's device name.

**Expected**

- Step 1: the last state (Games, Rechnen, School; Paint under *In keiner Gruppe*); every change of TC-009-40/-41 had shown ≤ 5 s.
- Step 2: create, rename, delete and move lines naming the Mac's device name.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

### TC-009-43: Rules: App-Gruppen-Regeln, hidden grouped apps, loading *(essential, re-confirm)*

*Verifies AC-35 (US-008 AC-15 to AC-17, D-11, D-13) · MacBook*

**Precondition**: Rechner in Rechnen

1. **Regeln** → kid1: section order, columns, member lines, (keine Apps), Rechnen 0:30 MO–SO.
2. **Games**: **Stunden** `24` → Tab (message); `1:00`, **SA SO** → Return. Then back to 0:00, no day.
3. *Applikations-Regeln*: the sentence; Rechner not listed. Move **Paint** into **School** (App Gruppen) → Paint gone from *Applikations-Regeln*; back → returns with its own rule.
4. Cmd+Q, start, **Regeln** at once: watch both sections.

**Expected**

- As TC-009-19 to TC-009-22 on the Mac: same layout and texts as the W11 app; validation message; saved ≤ 5 s; own rule comes back; **Wird geladen …** in both sections until loaded, Rechner never briefly under *Applikations-Regeln*.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

### TC-009-44 (optional): Mac save error *(optional)*

*Verifies AC-35 (US-008 AC-14 error path) · MacBook; service PC / Terminal (Administrator)*

**Precondition**: as TC-009-18

1. Service PC: `resmon` → **Prozess anhalten** on `EagleEye.Service.exe`. Within 10 s on the Mac: **≡** on Paint → **School**.
2. Within 25 s: **Prozess fortsetzen**.

**Expected**

- **"Die Änderung konnte nicht gespeichert werden. Bitte erneut versuchen."**; the page returns to the stored state.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

### TC-009-45: Reports group lines on the Mac; past days follow the current name *(essential, re-confirm)*

*Verifies AC-35 (US-008 AC-35, AC-36) · MacBook*

**Precondition**: kid1 has Rechnen time today (S-10, TC-009-24)

1. **Berichte** → kid1: today and past days.
2. W11 app or Mac: rename **Rechnen** → `Rechnen 2`; look at all days on the Mac; rename back.

**Expected**

- Step 1: **App-Gruppen** / **App-Gruppe** · **Nutzung (HH:MM)** under today with **Rechnen ≈ 00:03**, the **same value as in the W11 app** (AC-22 "same as in the Mac app"); no part on days without group time.
- Step 2: all days follow the name ≤ 5 s, without **Wird geladen …**.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

### TC-009-46: Mac start page and connection survive a restart *(essential, re-confirm)*

*Verifies AC-35 (US-008 AC-38: restart of the Mac app), AC-32 (pairing kept after the first restart too) · MacBook*

**Precondition**: TC-009-45

1. Cmd+Q; start the app.

**Expected**

- Green ≤ 30 s without a code or a Keychain prompt; opens on **Berichte**; groups, rules unchanged.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

### TC-009-47 (optional): Mac in English (wording reference) *(optional; you checked it on 0.8.0)*

*Verifies AC-35 (US-008 texts), AC-28 (Mac English as reference for TC-009-32) · MacBook*

**Precondition**: you are willing to switch the Mac language

1. *Systemeinstellungen → Allgemein → Sprache & Region* → English first; restart the app; look at the places of TC-009-32 step 2. Back to Deutsch.

**Expected**

- All texts English; the same wording as on Windows 10 in TC-009-32 (a difference = Fail, AC-28).

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

### TC-009-48: AC-43 spot check on the Mac *(essential)*

*Verifies AC-35 (US-008 AC-43) · MacBook*

**Precondition**: —

1. **Regeln** → kid1 → *Ruhezeiten*: **Eintrag hinzufügen**, change its **Start-Zeit**, delete it (trash can).
2. *Applikations-Regeln*: change **Editor**'s minutes by 1, Tab; change back.
3. **Berichte** → kid1 → today.

**Expected**

- Steps 1, 2: saved ≤ 5 s, as in 0.8.0. Step 3: today's lines as on the W11 app.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

## Session 5 — Two parent apps at once (W11 app on the service PC + Mac app) · ≈ 45 min

Put the service PC and the MacBook side by side. "W" = W11 app, "M" = Mac app. In each step, watch the **other** app without touching it; count to 5.

### TC-009-49: Both apps paired and connected at the same time *(essential)*

*Verifies AC-24 (both connected), US-007 AC-25 · Service PC / Admin (W) and MacBook (M)*

**Precondition**: Sessions 2 and 4 done

1. Look at both status bars.

**Expected**

- Both green **"Verbunden mit 192.168.1.37"**.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

### TC-009-50: Group changes on the App Groups page, both directions *(essential)*

*Verifies AC-25 (App Groups page), US-008 AC-14 (≤ 5 s) · W and M, both on App Gruppen*

**Precondition**: TC-009-49

1. W: **Gruppe hinzufügen** `Sync`.
2. M: rename **Sync** → `Sync M`.
3. W: drag **Paint** into **Sync M**.
4. M: **≡** on **Paint** → **In keiner Gruppe**.
5. M: delete **Sync M**.
6. Both at the same moment (count down): W moves **Editor** into **Games**, M moves **Editor** into **School**. Wait 5 s.
7. Move **Editor** back to *In keiner Gruppe*.

**Expected**

- Steps 1 to 5, 7: each change appears in the other app **≤ 5 s** without any action there.
- Step 6: after 5 s both apps show Editor in the **same** group (the last change the service received; write which in Notes).

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

### TC-009-51: Group rule changes on Rules, both directions; Rules follows the App Groups page *(essential)*

*Verifies AC-25 (Rules), AC-21 (Rules page open, change on App Groups ≤ 5 s), US-008 AC-18 · W and M*

**Precondition**: both on **Regeln** → **kid1**

1. W: **Games** **Minuten** `10` → **Tab**.
2. M: **Games** tick **MO** → watch W. M: untick it.
3. Leave W on **Regeln** → kid1. M → **App Gruppen**: move **Paint** into **School**; then create group `Live`; rename **Live** → `Live 2`; delete **Live 2**; move **Paint** back. Watch W after each.
4. Swap roles: M on **Regeln** → kid1, W on **App Gruppen**: W moves **Rechner** into **Games** and back. Watch M.
5. Both at the same moment: W **Games** **Minuten** `20`, M `40`, both **Enter**. Wait 5 s. Then **Games** back to 0:00, no day.

**Expected**

- Steps 1, 2: each change in the other app ≤ 5 s.
- Steps 3, 4: the open Rules page follows ≤ 5 s: Paint leaves/returns to *Applikations-Regeln*, School's member line changes, the group row appears, is renamed and disappears; Rechner leaves Rechnen's line and returns; no grouped app shows under *Applikations-Regeln*.
- Step 5: both show the same minutes (20 or 40).

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

### TC-009-52: Reports group lines follow a move made in the other app, both directions *(essential)*

*Verifies AC-25 (Reports), AC-23 (other parent app, ≤ 5 s, no "Wird geladen …"), US-008 AC-37, D-12 · W and M*

**Precondition**: kid1 has Rechner time today (Rechnen ≈ 00:03)

1. W on **Berichte** → kid1 (open). M: **App Gruppen** → move **Rechner** into **School**. Watch W.
2. M on **Berichte** → kid1 (open). W: move **Rechner** back into **Rechnen**. Watch M.

**Expected**

- Step 1: in W, today (and past days with Rechner time) show **School** instead of **Rechnen** ≤ 5 s, without **Wird geladen …**.
- Step 2: in M, **Rechnen** is back ≤ 5 s, without **Wird geladen …**. Both apps show the same values.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

### TC-009-53: Existing settings sync both directions *(essential)*

*Verifies AC-26, US-007 AC-33 · W and M*

**Precondition**: both on the same page (as each step says); kid1 selected on Regeln

 (each: watch the other app ≤ 5 s)

1. **Einstellungen**: W unticks **kid2**; M ticks it again.
2. **Regeln** → kid1 → *Ruhezeiten*: M **Eintrag hinzufügen**; W changes its **End-Zeit** to `23:00` → **Enter**; M deletes it.
3. *Ruhezeiten* **Anzeige Text:** W adds ` Test` → **Tab**; M removes it → **Tab**.
4. *Applikations-Regeln*: M changes **Editor** minutes by +1 → **Tab**; W changes them back → **Tab**.
5. *Anzeige Text wenn Budget aufgebraucht:* W adds ` X` → Tab; M removes it → Tab.
6. **Einstellungen → Zeit-Ende Warnungen**: M **Frühe Warnung** `4` → Tab; W back to `3` → Tab. W types a text in the **Finale Warnung** text `Gleich Schluss`; M empties it (back to the default).

**Expected**

- Every change shows in the other app **≤ 5 s** without any action; at the end everything is as before (kid2 ticked, no extra break, texts and values as before, early 3, final default text).

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

### TC-009-54: Removing the pairing in one app does not affect the other *(essential)*

*Verifies AC-24 (remove pairing), US-002 AC-29, US-007 AC-25 · W and M; service PC for the code*

**Precondition**: both green

1. M: **Einstellungen → Kopplung aufheben** (confirm). Watch W for 30 s; click through its pages.
2. M: pair again: host `192.168.1.37` → **Verbinden** → code from the service PC → same device name → **Koppeln**.

**Expected**

- Step 1: M is *Nicht gekoppelt*; W stays green **"Verbunden mit 192.168.1.37"** and works.
- Step 2: M green again (needed as the remote parent app in Session 6B).

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

## Session 6A — Windows app regression spot checks (service PC / Admin) · ≈ 20 min

### TC-009-55: Connection error naming the host; "Kopplung auf diesem Gerät löschen" offered *(essential)*

*Verifies AC-34 (US-002 AC-12, ISSUE-009) · Service PC / Admin, Terminal (Administrator)*

**Precondition**: W green

1. Close W. `Stop-Service -DisplayName "EagleEye Service"`. Start W; wait up to 60 s. Go to **Einstellungen**.
2. Click **Kopplung auf diesem Gerät löschen** → read the dialog → **Abbrechen** (do **not** confirm).
3. `Start-Service -DisplayName "EagleEye Service"`. Wait 60 s.

**Expected**

- Step 1: a connection error that **names 192.168.1.37**; red; on *Einstellungen* **Kopplung aufheben** disabled and the button **Kopplung auf diesem Gerät löschen** shown.
- Step 2: dialog **"Kopplung auf diesem Gerät löschen?"** with the explanation and **Kopplung auf diesem Gerät löschen** / **Abbrechen**; Abbrechen keeps the pairing.
- Step 3: green ≤ 60 s without any action; the forget button disappears.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

### TC-009-56: Settings and Rules spot checks (US-003, US-005, US-006) *(essential)*

*Verifies AC-34 (ticks, break times, app rule, budget text, warning minutes with one validation message) · Service PC / Admin, LOG-WATCH*

**Precondition**: W green

1. **Einstellungen**: untick **kid2**, tick it again.
2. **Regeln** → kid1 → *Ruhezeiten*: **Eintrag hinzufügen**; change **Start-Zeit** to `21:00` → **Enter**; delete it. **Anzeige Text:** add ` !` → Tab; remove it → Tab.
3. *Applikations-Regeln*: **Editor** minutes +1 → Tab; back → Tab. Budget text: add ` X` → Tab; remove → Tab.
4. **Einstellungen → Zeit-Ende Warnungen**: **Finale Warnung** `0` → Tab; then `3` (= early) → Tab; then `1` → Tab.

**Expected**

- Steps 1 to 3: each change stored ≤ 5 s (log line), the field keeps its value; no message.
- Step 4: `0` → **"Bitte Minuten zwischen 1 und 59 eingeben."**; `3` → **"Die finale Warnung muss weniger Minuten vor dem Ende liegen als die frühe Warnung."**; `1` stored.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

## Session 6B — Group enforcement and the US-008 retest (service PC / kids; MacBook as remote parent app) · ≈ 1 h 15

**Preparation P-1 to P-6** (≈ 10 min, **on a day on which kid1 has not used Editor or Paint yet**):

- [ ] **P-1** W11 app (AC-27 needs the rule set in the **Windows app**): first **Regeln** → kid1 → *Applikations-Regeln*: note kid1's own rules for **Editor** and **Paint**, then set **Paint 0:00, no day** (US-008 AC-24 check) and **Editor** e.g. 0:30, all days. Then **App Gruppen** → **Gruppe hinzufügen** `Test` → move **Editor** and **Paint** into **Test**.
- [ ] **P-2** W11 app → **Regeln** → kid1 → *App-Gruppen-Regeln*: **Test** **Stunden 0**, **Minuten 5**, **today ticked** only. kid2: Test stays **0:00, no day**.
- [ ] **P-3** W11 app → **Regeln** → kid1 → *Applikations-Regeln*: Editor and Paint are no longer listed.
- [ ] **P-4** *Einstellungen*: **Frühe Warnung 3**, **Finale Warnung 1**; kid1, kid2 ticked, kid3 not.
- [ ] **P-5** Mac (M): **Berichte** → kid1: today shows **no** Editor and Paint usage, and no **Test** line (if it does, tell TES or shift all times by that amount). M stays on **Regeln** → kid1, *App-Gruppen-Regeln* visible.
- [ ] **P-6** *Benutzer wechseln* → kid1. **K-TERM** with LOG-WATCH. No other app open. Stopwatch ready. **Stay in kid1's session; do not lock** during a timed case.

### TC-009-57: The group rule set in the Windows app takes effect: shared budget, warnings naming the group, all members closed, one dialog *(essential, the core case)*

*Verifies AC-27, AC-29 (German), AC-36; US-008 AC-19 (union), AC-24 (own rule ignored), AC-26 (warnings once per group), AC-27 (all closed, one dialog), AC-31 (log) · Service PC / kid1, K-TERM*

**Precondition**: P-1 to P-6

1. Windows+R `notepad`; **stopwatch on Enter**. Type nothing.
2. **Lap 1** when a dialog appears; read it; **OK**.
3. At **3:00** on the stopwatch: Windows+R `mspaint`. Draw nothing.
4. **Lap 2** at the next dialog; read; **OK**.
5. **Lap 3** at the next dialog; **lap 4** when both windows are gone. Read; **OK**.
6. K-TERM: LOG-FIND `Test`.

**Expected**

- Step 1: Notepad starts normally.
- Lap 1 between **2:00 and 2:15**: early warning with **"Test: noch 3 Minuten."** (group name, not "Editor").
- Step 3: **Paint starts and stays open** although its own rule is 0:00 (own rule ignored in the group).
- Lap 2 between **4:00 and 4:15**: final warning **"Test: noch 1 Minute."**; each warning **once** (no second early warning when Paint started).
- Lap 3 between **5:00 and 5:15**: **one** dialog with kid1's budget text and **"Test: Zeit-Budget aufgebraucht."** — not one per app; lap 4 ≤ **5:35**: **Notepad and Paint both closed**.
- Log: warning lines naming kid1, group **Test**, early / final; close lines for Editor and Paint naming group Test, budget used up, closed gracefully.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

### TC-009-58: Blocked start when exhausted and when not allowed today; Reports *(essential)*

*Verifies US-008 AC-25, AC-19 (Reports ±1), AC-23, AC-31, AC-35 (sorting), AC-37 (≤ 15 s on the Mac); AC-22 (W11 = Mac, sorting) · Service PC / kid1, K-TERM; Mac (M)*

**Precondition**: TC-009-57

1. Windows+R `mspaint`. Read; **OK**.
2. M: **Berichte** → kid1: today. Note Editor, Paint, **App-Gruppen**.
3. M: *Regeln* → kid1 → **Test**: **Minuten** `30`, **untick today** → **Tab**. Kid1: Windows+R `notepad`. Read; **OK**.
4. M: **Test** tick today (0:30) → **Tab**. Kid1: Windows+R `notepad` → keep it open **2 minutes** while M shows **Berichte** → kid1 (watch Test and Editor). Close Notepad.
5. K-TERM: LOG-FIND `Blocked start`.
6. Later (switch to your account): W11 app **Berichte** → kid1: compare with M.

**Expected**

- Step 1: blocked ≤ 10 s, gone ≤ 30 s; dialog with the budget text and **"Test: Zeit-Budget aufgebraucht."**
- Step 2: **Test 00:05** (±1, union: 3 + 2 overlapping counts once); **Editor 00:05**, **Paint 00:02** (±1, app usage counted as before). App-Gruppen sorted by usage: **Test** above **Rechnen**. The blocked start of step 1 added nothing.
- Step 3: blocked with **"Test: heute nicht erlaubt."**
- Step 4: Notepad starts (more time lets the kid start again, US-008 AC-33; no early or final warning, because 25 minutes are left). M's Test and Editor lines grow by themselves with ≤ 15 s lag.
- Step 5: "Blocked start" lines naming group Test and the reason (budget used up / not allowed today).
- Step 6: W11 shows the **same** group values as M.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

### TC-009-59: A new app appears on the App Groups page within 15 seconds *(essential)*

*Verifies US-008 AC-4 (Mac, timed), AC-13 (W11: listed without any action, after switch-back) · Service PC / kid1; M*

**Precondition**: M on **App Gruppen**; choose an app kid1 never used (`colorcpl`; if *Farbverwaltung* is already listed: `odbcad32`)

1. Kid1: Windows+R `colorcpl` (stopwatch). It is blocked by deny-by-default: **OK**.
2. Watch M's *In keiner Gruppe*.
3. Later, in your session: W11 app **App Gruppen** (do not touch anything else before looking).

**Expected**

- Step 1: blocked with "… heute nicht erlaubt." (own rule 0:00, as US-006).
- Step 2: the app appears in **In keiner Gruppe** ≤ **15 s**, in its alphabetical place, once.
- Step 3: listed there too.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

### TC-009-60: Other accounts: kid2 blocked by its own group rule (English texts); kid3 and the admin are never limited *(essential)*

*Verifies US-008 AC-21, AC-25, AC-29; AC-29 (English) · Service PC / kid2 (English Windows, S-6), kid3, Admin; M*

**Precondition**: kid2's Test rule 0:00, no day (P-2). Sign kid1 out (close K-TERM first).

1. M: **Berichte** → kid2: today.
2. *Benutzer wechseln* → kid2. Windows+R `notepad`. Read the dialog; **OK**.
3. Sign kid2 out. *Benutzer wechseln* → **kid3**: Windows+R `notepad` and `mspaint`; keep both open **1 minute**; close them; sign kid3 out.
4. In **your** session: Windows+R `notepad` and `mspaint` for 1 minute; close them.
5. M: **Berichte**: is kid3 listed under **Konto**? kid1's Test line: unchanged by steps 3 and 4?

**Expected**

- Step 1: no **Test** line for kid2 (kid1's time does not count).
- Step 2: blocked ≤ 10 s with kid2's budget text and **"Test: not allowed today."** in English (German Windows for kid2: "Test: heute nicht erlaubt."; then write "kid2 not English" in Notes: the English part of AC-29 stays open).
- Steps 3, 4: no dialog, nothing closed (kid3 is not controlled; admins are never limited).
- Step 5: kid3 not listed; kid1's Test value unchanged (other accounts never count).

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

### TC-009-61: Changes during the day: a rule change that ends the time gives a grace minute *(essential)*

*Verifies US-008 AC-32 (rule change), AC-33 (more time lets the kid start again) · Service PC / kid1, K-TERM; M on Regeln → kid1*

**Precondition**: Test 0:30, today ticked (TC-009-58 step 4); *Benutzer wechseln* → kid1 again, K-TERM with LOG-WATCH

1. Kid1: Windows+R `mspaint` (starts: time left). Wait 20 s.
2. M: **Test** **Minuten** `5` → **Tab** = **lap C**. Kid1: watch.
3. Read the dialog(s); **OK** each time. Note when Paint is gone.

**Expected**

- Step 1: Paint starts.
- Step 2: final warning ≤ **C + 15 s** with **"Test: Deine Eltern haben die Regeln geändert. Test wird in 1 Minute geschlossen."** (wording difference = Note; the group name must be in it).
- Step 3: Paint closed between **C + 60 s** and **C + 75 s** with the budget text; gone ≤ **C + 95 s**. One warning, not one per app.
- Log: "Grace period: … group Test …".

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

### TC-009-62: A running app moved into an exhausted group gets a grace minute; its earlier time joins the group *(essential)*

*Verifies US-008 AC-32 (move), AC-22 (current members, earlier time added), AC-36 (lines follow members) · Service PC / kid1; M on App Gruppen*

**Precondition**: Test exhausted (0:05, used > 5); Rechner in Rechnen (0:30, ≈ 3 min used today)

0. M: **Berichte** → kid1 → today: note **Test** and **Rechner**. Then M → **App Gruppen**.
1. Kid1: Windows+R `calc` (starts). Wait 20 s.
2. M: drag **Rechner** into **Test** = **lap C**.
3. Read the dialog; **OK**. Note when Rechner is gone.
4. M: **Berichte** → kid1: today and past days.

**Expected**

- Step 2: final warning ≤ C + 15 s naming **Test**.
- Step 3: Rechner closed between C + 60 s and C + 75 s.
- Step 4: today **Test** ≈ the Test value of step 0 + Rechner's usage today (Rechner's line, incl. this case) ± 1 (Rechner never ran together with Editor or Paint, so nothing overlaps); **Rechnen** is no longer listed today (no members); past days that had Rechnen time show it under **Test** now.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

### TC-009-63: Deleting a group with a running app: the app falls back to its own rule *(essential)*

*Verifies US-008 AC-32 (delete), AC-30 (ungrouped app as US-006), AC-10 (apps back to own rules) · Service PC / kid1; M*

**Precondition**: Paint's own rule 0:00 (P-1); Test holds Editor, Paint, Rechner

1. M: **Test** → **Minuten** `45` → **Tab**. Kid1: Windows+R `mspaint` (starts). Wait 20 s.
2. M: **App Gruppen** → trash can on **Test** → **Löschen** = **lap C**.
3. Read the dialog; **OK**. Note when Paint is gone.
4. M: **App Gruppen** and **Regeln** → kid1.

**Expected**

- Step 2: final warning ≤ C + 15 s, now naming **Paint** (own rule): "Paint: Deine Eltern haben die Regeln geändert. Paint wird in 1 Minute geschlossen."
- Step 3: Paint closed between C + 60 s and C + 75 s.
- Step 4: Editor, Paint, Rechner under *In keiner Gruppe*; in *Applikations-Regeln* again with their own earlier rules (Paint 0:00). Move **Rechner** back into **Rechnen**.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

### TC-009-64 (optional): Rename and harmless changes show no dialog; a change within the early level shows the early warning *(optional)*

*Verifies US-008 AC-34 (rename, harmless move), AC-33 (early warning at once) · Service PC / kid1; M*

**Precondition**: Rechnen 0:30 for kid1 with ≈ 3 min used today

1. Kid1: Windows+R `calc`. M: rename **Rechnen** → `Rechnen X`. Wait 15 s. Rename back.
2. M: move **Paint** (not running) into **Rechnen**; wait 15 s.
3. M: **Berichte** → kid1: read Rechnen's used minutes (round up) = U. **Regeln** → kid1: **Rechnen** **Minuten** = U + 2 (2 minutes left: within the early level 3, above the final level 1) → **Tab** = C.
4. Close Rechner. Move Paint back; Rechnen back to 0:30.

**Expected**

- Steps 1, 2: **no** kid dialog (Paint's earlier time leaves well over 3 minutes).
- Step 3: the **early** warning at once (≤ C + 15 s) naming Rechnen; no grace close.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

### TC-009-65 (optional): Break time wins over a group rule *(optional)*

*Verifies US-008 AC-28 · Service PC / kid1; M*

**Precondition**: Rechnen 0:30 with time left

1. M: **Regeln** → kid1 → *Ruhezeiten*: **Eintrag hinzufügen**, **Start-Zeit** = now − 1 minute, **End-Zeit** 23:59, all days, **An/Aus** on → Enter.
2. Kid1: Windows+R `calc`. Read; **OK**.
3. M: break entry off and deleted.

**Expected**

- Step 2: blocked with the break display text (not the group text).

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

### TC-009-66 (optional): Group time does not count while the session is switched away *(optional)*

*Verifies US-008 AC-20 (not counted when switched away) · Service PC / kid1; M*

**Precondition**: Rechnen with time left

1. Kid1: `calc`. After 1 minute, note M's Rechnen value; *Benutzer wechseln* to your account for **3 minutes**; back to kid1; close Rechner; M's value.

**Expected**

- Rechnen grew by about the 1 minute plus the time after coming back, **not** by the 3 minutes away (±1).

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

### TC-009-67: Groups, members, rules and group time survive a service restart and an app restart *(essential; step 4 optional)*

*Verifies US-008 AC-38, AC-20 (kept across service restarts) · Service PC / Admin; M*

**Precondition**: sign kid1 out; note M's App Gruppen, kid1's group rules and today's group lines

1. Terminal (Administrator): `Restart-Service -DisplayName "EagleEye Service"`. Wait until W and M are green.
2. W and M: **App Gruppen**, **Regeln** → kid1, **Berichte** → kid1.
3. Close W and start it again; same pages.
4. *(optional)* Reboot the service PC; same pages afterwards.

**Expected**

- Everything as noted: groups with members, group rules, today's group lines (no time lost or added).

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

## Session 6C — Storage and accounts (service PC / Admin; M) · ≈ 15 min

### TC-009-68: An account ticked for the first time gets 0:00, no day, for every group *(essential)*

*Verifies US-008 AC-39 (first tick) · W*

**Precondition**: kid3 has never been ticked since groups exist; kid3 signed out

1. **Einstellungen**: tick **kid3**. **Regeln** → **Konto** kid3 → *App-Gruppen-Regeln*.
2. Untick **kid3** again at once.

**Expected**

- Step 1: every group (Games, Rechnen, School) **0:00**, no day ticked.
- Step 2: kid3 unticked (it must not stay controlled: deny-by-default would block its apps).

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

### TC-009-69: Untick keeps the group rules; an app stays in its group when its only account is unticked *(essential)*

*Verifies US-008 AC-39 (untick keeps, re-tick applies), AC-40 · W; M*

**Precondition**: the new app of TC-009-59 (e.g. Farbverwaltung) has a record for kid1 only; kid1 signed out

1. M: move the new app into **Rechnen**.
2. W: **Einstellungen**: untick **kid1**. M: **App Gruppen**.
3. W: tick **kid1** again. M: **App Gruppen**; **Regeln** → kid1.

**Expected**

- Step 2: the new app is no longer listed (no controlled account has a record of it); Rechnen shows only Rechner.
- Step 3: the new app is back **in Rechnen**; kid1's Rechnen rule is unchanged (0:30, MO–SO). Move the new app back to *In keiner Gruppe* (or leave it).

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

### TC-009-70 (optional): A newly ticked account with an existing group rule behaves as before after re-tick (enforcement) *(optional)*

*Verifies US-008 AC-39 (rules apply again after re-tick) · Service PC / kid1*

**Precondition**: TC-009-69

1. Kid1: `calc`; check M's Rechnen line grows (≤ 15 s); close.

**Expected**

- Rechner starts (Rechnen 0:30 applies again) and its time counts for Rechnen.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

## Session 6D — Optional pilot checks and clean-up check

### TC-009-71 (optional, pilot): Performance with 20 groups and 100 apps; live group smoke *(optional pilot check)*

*Verifies AC-38 (US-008 AC-41, US-008 plan Step 7.3) · service PC in pilot use*

**Precondition**: pilot operation; record observations whenever convenient

1. In pilot use: is starting an allowed app noticeably slower than before? *Task-Manager* → EagleEye Service CPU while kids use several apps (US-004 AC-25: on average below the US-004 limit).

**Expected**

- Not noticeably slower; CPU within US-004 AC-25. "Not tested" does not block closing (story AC-38).

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

### TC-009-72 (optional): "Noch keine App-Gruppen …" when no group exists *(optional; at the end of the run, only if you want to remove all test groups)*

*Verifies AC-19 ("no app groups yet" text), US-008 AC-15 · W*

**Precondition**: you want to remove all test groups (Games, Rechnen, School); note Rechnen's rule if you want to keep it

1. Delete every group. **Regeln** → kid1 → *App-Gruppen-Regeln*. **App Gruppen**.

**Expected**

- Rules: **"Noch keine App-Gruppen. Du kannst sie auf der Seite App Gruppen anlegen."** App Gruppen: only *In keiner Gruppe* with all apps.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

## General Feedback

<!-- Anything that does not fit a test case: usability remarks, ideas, surprises, differences between the Windows app and the Mac app, Windows 10 look. -->

---

## Summary (filled in by TES after evaluation)

| Session | Pass | Fail | Blocked | Skipped | Not executed |
|---|---|---|---|---|---|
| 1 | | | | | |
| 2 | | | | | |
| 3 | | | | | |
| 4 | | | | | |
| 5 | | | | | |
| 6A–6D | | | | | |
| **Total** | | | | | |

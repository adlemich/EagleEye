# Test Run 01: US-002 — Windows Parent App: Installation, Connection and Pairing

**Test plan**: `docs/testing/US-002/test-plan.md` (approved 2026-10-04)
**Prepared by**: TES, 2026-10-04
**Executed by**: Michael
**Execution date**: <!-- fill in -->
**Build / installer version**: 0.2.0 — `03_Delivery/windows/EagleEye-Setup-0.2.0.exe` (service + tray) and `03_Delivery/windows/EagleEye-ParentApp-Setup-0.2.0.exe` (parent app)
**Machine(s)**: Windows Developer Machine (service PC) <!-- add Windows version --> · PC2 (Block C) <!-- add name / Windows version / display language -->

> **How to record**: tick exactly one result box per case (`[x]`). Fill in **Observed** for Fail/Blocked. Screenshots go into `docs/testing/US-002/evidence/` (e.g. `tc-15-green.png`); reference them in Notes. When you are done, or want to stop halfway, tell TES: *"test run 01 for US-002 is done"*.
>
> **Accounts**: **Admin** = your administrator account. **Kid** = `eagleeye-kid` (standard user). **Parent 2** = `eagleeye-parent2` (standard user, Block D only, created in S-11).
> **Names**: `<host>` = computer name of the service PC, `<ip>` = its LAN IPv4 address (both noted in S-5).
> **Duration**: Block A ≈ 50 min (incl. regression) · Block B ≈ 60 min (one 5½-min and one 2-min wait) · Block C ≈ 35 min · Block D ≈ 20 min. Blocks A and B in one session, in this order; C and D can follow later.
>
> **Rules for this run** (test plan §2):
> - **UI wording**: a different text with the same meaning is a **Note**, not a Fail. If you want a text changed, write the wanted text in Notes. Pass criteria are only the agreed texts *Einstellungen*, *Darstellung*, *Serververbindung*, *Hell* / *Dunkel*, *Verbunden mit `<host>`*, *Kopplung aufheben*.
> - **Certificate prompts**: if the parent app ever shows anything about a certificate, write it into Observed of the current case and mark it **Fail**.
> - **Timing**: use a stopwatch. Where the story gives no time limit, slowness is a Note, not a Fail.
> - **Unexpected behaviour** not covered by the expected result goes into Notes (TES routes it to PRO).

---

## Setup (service PC, Admin) — before Block A

- [x] **S-1** *Installierte Apps*: **EagleEye 0.1.1** installed (do not uninstall). "EagleEye Parent App" **not** listed.
- [x] **S-2** PowerShell: `Test-Path "$env:LOCALAPPDATA\EagleEye"` and `Test-Path "$env:LOCALAPPDATA\Programs\EagleEye Parent App"` both `False` (delete the folder if `True`).
- [x] **S-3** *Task-Manager → Details*: no `EagleEye.Service.exe` running from a `02_Implementation` folder, no `EagleEye.ParentApp.exe`.
- [x] **S-4** `eagleeye-kid` exists and is signed out.
- [x] **S-5** `hostname` → `<host>` = ____________ · `ipconfig` (LAN IPv4) → `<ip>` = ____________
- [x] **S-6** Windows app mode = **Dunkel** (*Einstellungen → Personalisierung → Farben*). Your usual setting (for cleanup): ____________
- [x] **S-7** SmartScreen on the unsigned installers: *Weitere Informationen* → *Trotzdem ausführen* (expected, not a failure).
- [x] **S-8** Terminal (Administrator) open. Commands: `Stop-Service -DisplayName "EagleEye Service"` / `Start-Service -DisplayName "EagleEye Service"`.

---

## Block A — Service PC: installers and first pairing (Admin, one visit to Kid)

### TC-002-01: Service update to 0.2.0 sets up network access by itself

*Verifies AC-6 (service side) · Service PC / Admin · EagleEye 0.1.1 installed, service running*

1. Run `03_Delivery\windows\EagleEye-Setup-0.2.0.exe` with default settings and finish the wizard. **→ record REG-001-01 below.**
2. `services.msc` → **EagleEye Service**. **→ record REG-001-02 below.**
3. PowerShell: `netstat -ano | findstr "5443 5080"`
4. `wf.msc` → *Eingehende Regeln* → **EagleEye Service (Parent apps)** (double-click for details).

**Expected**:
- The wizard finishes without error and **never** asks about a firewall, port or certificate.
- `0.0.0.0:5443` and `[::]:5443` *ABHÖREN*. Port 5080 only as `127.0.0.1:5080` and `[::1]:5080`.
- Rule: *Aktiviert* Ja, *Aktion* Zulassen, *Profil* Alle, TCP, *Lokaler Port* 5443, *Remoteadresse* Lokales Subnetz, *Programm* `C:\Program Files\EagleEye\Service\EagleEye.Service.exe`.

- **Result**: [X] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

### REG-001-01: Install or upgrade to the build under test

*Regression · Windows / Admin · done in TC-002-01 step 1*

1. (Installer already run in TC-002-01.) *Einstellungen → Apps → Installierte Apps* → EagleEye.

**Expected**: no error; **EagleEye** is listed once, version **0.2.0**, publisher **Michael Adler**.

- **Result**: [X] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

### REG-001-02: Service registration

*Regression · Windows / Admin · done in TC-002-01 step 2*

1. `services.msc` → **EagleEye Service**.

**Expected**: *Wird ausgeführt*, *Automatisch*, *Lokales System*.

- **Result**: [X] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

### TC-002-02: Service data folder is protected *(supporting)*

*Supporting check (ADR-008 / NFR-S-014), no AC · Service PC / Admin · TC-002-01 done*

1. PowerShell: `icacls "$env:ProgramData\EagleEye"`
2. `icacls "$env:ProgramData\EagleEye\certs"`
3. `Test-Path "$env:ProgramData\EagleEye\certs\eagleeye.pfx"`

**Expected**:
- `EagleEye`: only `NT-AUTORITÄT\SYSTEM` (F), `VORDEFINIERT\Administratoren` (F), `VORDEFINIERT\Benutzer` (RX). No entry with `(I)`.
- `certs`: only SYSTEM (F) and Administratoren (F). No *Benutzer* entry.
- `True`.

- **Result**: [ ] Pass  [X ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**: Command line shows access errors: 
- PS C:\Users\Admin> icacls "$env:ProgramData\EagleEye\certs"
C:\ProgramData\EagleEye\certs: Zugriff verweigert
0 Dateien erfolgreich verarbeitet, bei 1 Dateien ist ein Verarbeitungsfehler aufgetreten.
PS C:\Users\Admin> Test-Path "$env:ProgramData\EagleEye\certs\eagleeye.pfx"
Test-Path: Access to the path 'C:\ProgramData\EagleEye\certs\eagleeye.pfx' is denied.
False
-
- **Notes**:

---

> **Switch to the Kid account** (*Benutzer wechseln* → `eagleeye-kid`; do not sign Admin out). Run REG-001-03, -04, -05, then TC-002-03.

### REG-001-03: Tray auto-start and connected state

*Regression · Windows / Kid*

1. Sign in as the kid and hover over the EagleEye tray icon.

**Expected**: the icon is present without a manual start, it is **green**, and the tooltip reads **"EagleEye — Verbunden"**.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [X] Skipped
- **Observed**:
- **Notes**:

---

### REG-001-04: App Infos with live version

*Regression · Windows / Kid · service running*

1. Right-click the icon → **App Infos**.

**Expected**: dialog **"EagleEye – App Infos"** showing **"Server-Version: EagleEye_v0.2"**.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [X] Skipped
- **Observed**:
- **Notes**:

---

### REG-001-05: Disconnect, connection error, reconnect

*Regression · Windows / Kid (+ admin `services.msc` via Terminal (Administrator))*

1. Stop **EagleEye Service**. Within 30 s: the icon is **red**, and the tooltip reads **"EagleEye — Verbindungsfehler"**.
2. **App Infos** shows **"Verbindungsfehler: Keine Verbindung zum Server unter localhost:5080 möglich."**
3. Start **EagleEye Service**. Within 60 s, without touching anything: the icon is **green** again.

**Expected**: all three observations as stated.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [X] Skipped
- **Observed**:
- **Notes**:

---

### TC-002-03: The kid cannot open the service certificate *(supporting)*

*Supporting check (ADR-008 / NFR-S-014), no AC · Service PC / Kid · REG-001-03 to -05 done*

1. Explorer → address bar `C:\ProgramData\EagleEye\certs` → Enter. (Do **not** click *Fortsetzen*.)
2. PowerShell (normal, not elevated): `Get-ChildItem C:\ProgramData\EagleEye\certs`

**Expected**:
- Explorer refuses access (*"Sie verfügen momentan nicht über die Berechtigung…"*).
- PowerShell: *Zugriff verweigert*; no file listed.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [X] Skipped
- **Observed**:
- **Notes**:

> **Switch back to Admin.** Leave the Kid **signed in** in the background (needed for TC-002-12).

---

### TC-002-04: Parent app installer is a guided wizard and installs with default settings

*Verifies AC-1, AC-3 · Service PC / Admin · parent app not installed*

1. Run `03_Delivery\windows\EagleEye-ParentApp-Setup-0.2.0.exe`.
2. Read the first page, click through with defaults. On the last page **untick** "start now" (if offered) → *Fertigstellen*.
3. Start menu: search "EagleEye Parent App".

**Expected**:
- A wizard (*Weiter* / *Installieren* / *Fertigstellen*) that names the EagleEye parent app and shows version **0.2.0**.
- Finishes without error and without asking to install other software.
- Start menu entry **EagleEye Parent App** exists.
- *Note only*: whether a UAC prompt appeared.

- **Result**: [X] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

### TC-002-05: Installed apps shows name, version and publisher

*Verifies AC-3 · Service PC / Admin · TC-002-04 done*

1. *Einstellungen → Apps → Installierte Apps* → search "EagleEye".

**Expected**:
- **EagleEye Parent App** listed separately from **EagleEye**.
- Version **0.2.0**, publisher **Michael Adler**.

- **Result**: [X] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

### TC-002-06: First start shows the host dialog at once, in the Windows app mode

*Verifies AC-11, AC-9 · Service PC / Admin · Windows app mode Dunkel, app never started*

1. Start menu → **EagleEye Parent App**.

**Expected**:
- The app opens and **immediately** shows **"Mit dem EagleEye-PC verbinden"** (hostname or IP address), buttons *Verbinden* and *Abbrechen*.
- Dialog and window are **dark**.

- **Result**: [X] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

### TC-002-07: Cancelling the host dialog shows the unpaired main window

*Verifies AC-11, AC-8, AC-10 · Service PC / Admin · host dialog open*

1. Click *Abbrechen*.
2. Look at the main window: menu left, content, status bar bottom.
3. Click **Einstellungen** in the menu (if not already selected).

**Expected**:
- Desktop-style main window; left menu with the only entry **Einstellungen**.
- Settings page: **Darstellung** (with a switch), then **Serververbindung**.
- *Serververbindung*: status **Nicht gekoppelt**, **editable** host field, button *Verbinden*.
- Status bar: **red** indicator, **"Nicht verbunden"**.

- **Result**: [X] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

### TC-002-08: The light/dark switch applies immediately

*Verifies AC-9 · Service PC / Admin · settings page*

1. *Darstellung*: the switch shows **Dunkel**.
2. Switch to **Hell**.
3. Back to **Dunkel**, then again **Hell** (leave it on *Hell*).

**Expected**:
- Before step 2 the switch shows *Dunkel*.
- Each change applies to the **whole** window **at once**, without a restart.
- The label shows the current mode (*Hell* / *Dunkel*).

- **Result**: [X] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

### TC-002-09: Unknown host name gives a connection error that names the host

*Verifies AC-12 · Service PC / Admin · unpaired main window*

1. *Serververbindung*: enter `eagleeye-gibtsnicht` → *Verbinden*. Start the stopwatch.
2. Wait for the result (up to 60 s).
3. Change the input to `<host>`; do **not** connect yet.

**Expected**:
- A connection error naming the host, e.g. **"Verbindungsfehler: Keine Verbindung zum EagleEye-Dienst unter eagleeye-gibtsnicht möglich."** (time into Notes; DEV expects 15–21 s).
- Indicator stays **red**; never "Verbunden".
- The field stays editable (step 3 works).

- **Result**: [X] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**: <!-- seconds until the error -->

---

### TC-002-10: Stopped service gives a connection error that names the host

*Verifies AC-12 · Service PC / Admin (+ Terminal (Administrator)) · `<host>` entered, not connected*

1. `Stop-Service -DisplayName "EagleEye Service"`
2. In the app click *Verbinden*.
3. After the error: `Start-Service -DisplayName "EagleEye Service"`. Wait 10 s.

**Expected**:
- A connection error naming `<host>`.
- Indicator stays **red**; *Verbinden* can be used again.

- **Result**: [X] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

### TC-002-11: Connecting by hostname asks for the pairing code and a device name

*Verifies AC-13, AC-10, AC-20 · Service PC / Admin · service running, Kid signed in in the background*

1. With `<host>` entered, click *Verbinden*.
2. Look at *Serververbindung* and the status bar. Do not enter anything yet.

**Expected**:
- Status "Kopplung läuft", fields *Kopplungscode* and *Gerätename dieses PCs*, buttons *Koppeln*, *Neuen Code anfordern*, *Abbrechen*.
- Status bar **red** "Verbindung zu `<host>` wird hergestellt …"; **not** "Verbunden mit `<host>`".
- *Note only*: what the device name field is prefilled with.

- **Result**: [X] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**: <!-- prefilled device name -->

---

### TC-002-12: The pairing code appears in the kid's tray client

*Verifies AC-14 · Service PC / Kid (look only), then Admin · TC-002-11 less than 5 min ago*

1. Before switching: did a pairing popup appear **in your Admin session** too? (Observation only, no Pass/Fail; the tray running in admin sessions is accepted.)
2. *Benutzer wechseln* → `eagleeye-kid`.
3. Find **"EagleEye – Eltern-App koppeln"**. Write down the code. *OK*.
4. *Benutzer wechseln* → Admin.

**Expected**:
- In the Kid session the popup **"EagleEye – Eltern-App koppeln"** is shown on top, visible without searching.
- It shows **6 digits** ("Kopplungscode: nnnnnn") and says the code is for the EagleEye parent app ("Geben Sie diesen Code in der EagleEye-Eltern-App ein.", "Der Code ist 5 Minuten gültig.").

- **Result**: [ ] Pass  [X] Fail  [ ] Blocked  [ ] Skipped
- **Observed**: The window that shows the code is to small. See screenshot "02_Implementation\docs\testing\US-002\evidence\Screenshot 2026-10-04 203157.png". As a result, the process could not be continued. Test aborted here.
- **Notes**: <!-- popup also in Admin session? yes / no -->

---

### TC-002-13: An empty device name is not accepted

*Verifies AC-17 · Service PC / Admin · pairing form open, code from TC-002-12*

1. Clear *Gerätename dieses PCs* completely.
2. Enter the code from TC-002-12 → *Koppeln*.

**Expected**:
- The app says a device name is required (e.g. "Bitte einen Gerätenamen eingeben (höchstens 50 Zeichen).").
- Not paired; indicator **red**.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

### TC-002-14: A wrong code is rejected, and a new attempt gets a new code

*Verifies AC-18, AC-20 · Service PC / Admin · TC-002-13 done*

1. Device name **Papas PC**.
2. Enter a **wrong** code (TC-002-12 code with the last digit changed) → *Koppeln*.
3. Click *Neuen Code anfordern* (or *Abbrechen*, then *Verbinden*).
4. Read the new code from the tray popup (Admin session, or switch to the Kid). Do not enter it yet.

**Expected**:
- After step 2: clear message, e.g. **"Der Kopplungscode ist falsch."**; not paired, **red**, never "Verbunden".
- After step 3/4: a popup with a **new** 6-digit code, different from TC-002-12.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

### TC-002-15: The correct code pairs the app

*Verifies AC-16, AC-10, AC-8, AC-1, AC-23 (service PC) · Service PC / Admin · new code less than 5 min old*

1. Device name **Papas PC**, enter the new code → *Koppeln*.
2. Look at the status bar and *Serververbindung*.

**Expected**:
- Status bar **green** "Verbunden mit `<host>`".
- *Serververbindung*: **Gekoppelt**, host `<host>`, device **Papas PC**.
- No certificate prompt at any time.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

### TC-002-16: The host cannot be changed while paired

*Verifies AC-25, AC-8 · Service PC / Admin · paired and connected*

1. In *Serververbindung*, try to click into the host and type.

**Expected**:
- Host shown **read-only**: no editable host field, no *Verbinden*; typing changes nothing.
- **Kopplung aufheben** is offered.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

## Block B — Service PC: paired operation and removing the pairing (Admin)

*Start state: end of Block A (paired, green, theme Hell, Windows app mode Dunkel, Kid signed in in the background).*

### TC-002-17: A paired app connects by itself after a restart, theme kept

*Verifies AC-21, AC-9 · Service PC / Admin · paired, service running*

1. Close the app (**X**). *Task-Manager*: no `EagleEye.ParentApp.exe`.
2. Start menu → **EagleEye Parent App**; stopwatch at the click.
3. Do not touch anything.

**Expected**:
- No host dialog, no code request, no input.
- **Green "Verbunden mit `<host>`"** within **30 s**.
- The app is **light** (Hell) although Windows is dark.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**: <!-- seconds until green -->

---

### TC-002-18: Repair install over the running app keeps pairing and theme

*Verifies AC-4 (repair, same version), AC-9 · Service PC / Admin · app running, green*

1. With the app open, run `EagleEye-ParentApp-Setup-0.2.0.exe` again with defaults (note how it handles the running app).
2. Start the app.

**Expected**:
- Installer finishes **without errors**.
- App connects **without a pairing code**: green "Verbunden mit `<host>`", device **Papas PC**.
- Still **light** (Hell).

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**: <!-- how the installer handled the running app -->

---

### TC-002-19: Stopping the service turns the app red

*Verifies AC-22, AC-10 · Service PC / Admin (+ Terminal (Administrator)) · app green*

1. `Stop-Service -DisplayName "EagleEye Service"`; stopwatch. Clock time: ________
2. Watch the status bar.

**Expected**:
- Within **30 s**: **red** "Nicht verbunden mit `<host>`".

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**: <!-- seconds until red -->

---

### TC-002-20: Removing the pairing is not possible while not connected

*Verifies AC-30 · Service PC / Admin · service stopped, app red*

1. *Einstellungen → Serververbindung*: look at **Kopplung aufheben** and try to click it.

**Expected**:
- Not offered or not usable (e.g. greyed out); the pairing is still shown.
- The app says a connection is required, e.g. **"Zum Aufheben der Kopplung ist eine Verbindung zum EagleEye-PC nötig."**

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

### TC-002-21: After a service restart the app reconnects by itself

*Verifies AC-22 · Service PC / Admin (+ Terminal (Administrator)) · service stopped for at least 2 min (clock time from TC-002-19)*

1. `Start-Service -DisplayName "EagleEye Service"`; stopwatch.
2. Do not touch the app.

**Expected**:
- **Green "Verbunden mit `<host>`"** within **60 s**, no user action.
- *Kopplung aufheben* available again.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**: <!-- seconds until green -->

---

### TC-002-22: Removing the pairing asks for confirmation

*Verifies AC-26, AC-8 · Service PC / Admin · app green*

1. **Preparation for TC-002-25**: close the app; *Task-Manager*: `EagleEye.ParentApp.exe` gone; then PowerShell:
   `Copy-Item "$env:LOCALAPPDATA\EagleEye" "$env:LOCALAPPDATA\EagleEye-TC25-Kopie" -Recurse`
   Start the app again and wait for green.
2. *Serververbindung* → **Kopplung aufheben**.
3. In the confirmation choose the option that does **not** remove (*Abbrechen* / *Nein*).

**Expected**:
- Step 2: a confirmation question (e.g. "Kopplung aufheben?") **before** anything is removed.
- Step 3: still **Gekoppelt** and **green**.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

### TC-002-23: Confirming removes the pairing

*Verifies AC-27 · Service PC / Admin · app green*

1. **Kopplung aufheben** → confirm.

**Expected**:
- *Serververbindung*: **Nicht gekoppelt**, host field **editable** (empty), *Verbinden* available.
- **Red**, "Nicht verbunden".

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

### TC-002-24: After a restart the unpaired app starts with the host dialog

*Verifies AC-27, AC-11 · Service PC / Admin · TC-002-23 done*

1. Close the app and start it again.
2. *Abbrechen* in the dialog. Close the app.

**Expected**:
- **"Mit dem EagleEye-PC verbinden"** appears immediately (no automatic connection to the former host).

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

### TC-002-25: The removed pairing is no longer accepted

*Verifies AC-28, AC-20 · Service PC / Admin · app closed, copy from TC-002-22 exists*

1. *Task-Manager*: no `EagleEye.ParentApp.exe`. Then PowerShell:
   `Remove-Item "$env:LOCALAPPDATA\EagleEye" -Recurse -Force`
   `Rename-Item "$env:LOCALAPPDATA\EagleEye-TC25-Kopie" "EagleEye"`
2. Start the app. Watch it for **60 s**.

**Expected**:
- The app does **not** become green and never shows "Verbunden mit `<host>`".
- *Note only*: what the app shows, and whether it returns to *Nicht gekoppelt* by itself.
- **Cleanup**: if it does not show *Nicht gekoppelt* with an editable host field after 60 s: close it, `Remove-Item "$env:LOCALAPPDATA\EagleEye" -Recurse -Force`, start it again, cancel the host dialog (theme back to the Windows mode; expected).

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**: <!-- what the app showed; cleanup needed? -->

---

### TC-002-26: Without any tray client, the code goes to the Event Log

*Verifies AC-15, AC-13, AC-11 · Service PC / Admin (+ Event Viewer) · app unpaired, editable host field*

1. *Task-Manager → Benutzer* → `eagleeye-kid` → *Abmelden*.
2. *Task-Manager → Details* → `EagleEye.TrayClient.exe` → *Task beenden*.
3. PowerShell: `Get-Process EagleEye.TrayClient -ErrorAction SilentlyContinue` → returns **nothing** (else end it and repeat).
4. In the app enter `<ip>` → *Verbinden*.
5. `eventvwr.msc` → *Windows-Protokolle → Anwendung* → *Aktualisieren*. Open the newest entry with source **EagleEye**. Code: ________ Time: ________

**Expected**:
- Pairing form (code + device name), red "Verbindung zu `<ip>` wird hergestellt …".
- No popup anywhere.
- New Event Log entry: *Quelle* **EagleEye**, *Ebene* **Informationen**, with the **6-digit code** (DEV: event ID 1000).

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

### TC-002-27: An expired code is rejected

*Verifies AC-19 · Service PC / Admin · TC-002-26 done, code not entered yet*

1. Wait until **at least 5 min 30 s** after the time of the Event Log entry. Leave the app as it is.
2. Device name **Papas PC**, enter the code from TC-002-26 → *Koppeln*.
3. New attempt (*Neuen Code anfordern*, or *Abbrechen* and *Verbinden*). Refresh the Event Viewer.

**Expected**:
- Step 2: clear message that the code has **expired** (e.g. "Der Kopplungscode ist abgelaufen."). Not paired, **red**.
- Step 3: new Event Log entry with a **new** code.
- *Note only*: if the pairing form closed by itself during the wait, what happened.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

### TC-002-28: Pairing by IP address with a new code

*Verifies AC-28, AC-16, AC-11, AC-10 · Service PC / Admin · new code less than 5 min old*

1. Enter the new code, device name **Papas PC** → *Koppeln*.

**Expected**:
- **Green "Verbunden mit `<ip>`"**, IP exactly as typed.
- *Serververbindung*: **Gekoppelt**, host `<ip>`, device **Papas PC**.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

### TC-002-29: The connection is TLS, and the app never asks about certificates

*Verifies AC-7 · Service PC / Admin*

1. Microsoft Edge → `https://<host>:5443/`.
2. On the warning page click *Erweitert* (do not continue). Optionally open the certificate details.
3. Think back over Blocks A and B: did the parent app ever show a certificate warning or question?

**Expected**:
- Edge shows a certificate warning (e.g. *"Ihre Verbindung ist nicht privat"*, `NET::ERR_CERT_AUTHORITY_INVALID`); certificate issued to **EagleEye**.
- The parent app has **never** shown a certificate warning, question or import step.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

### TC-002-30: The service log contains no pairing codes outside the code entry *(supporting)*

*Supporting check (NFR-S-014), no AC · Service PC / Admin (Event Viewer)*

1. Event Viewer → *Anwendung* → *Aktuelles Protokoll filtern…* → *Ereignisquellen*: **EagleEye** → OK.
2. Open the entries of this run (pairing started, code sent, rejected, paired, removed).

**Expected**:
- Only the code entries (event ID 1000) contain a 6-digit code; all others mask it (e.g. `***`).
- No entry contains a long random string that looks like a token or password.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

### TC-002-31: Uninstalling removes the parent app completely

*Verifies AC-5 · Service PC / Admin · app paired (TC-002-28) and **closed***

1. *Installierte Apps* → **EagleEye Parent App** → *…* → *Deinstallieren*, confirm, finish.
2. PowerShell:
   `Test-Path "$env:LOCALAPPDATA\Programs\EagleEye Parent App"`
   `Test-Path "$env:LOCALAPPDATA\EagleEye"`
   `Get-ChildItem "$env:APPDATA\Microsoft\Windows\Start Menu\Programs" -Recurse -Filter "*EagleEye Parent*"`
3. Start menu: search "EagleEye Parent App". *Installierte Apps*: search "EagleEye".

**Expected**:
- Uninstaller finishes without errors.
- Both `Test-Path` **False**; `Get-ChildItem` returns nothing.
- No Start menu entry; only **EagleEye** (service) listed, still *Wird ausgeführt* in `services.msc`.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

### TC-002-32: A new install starts fresh and follows the Windows app mode

*Verifies AC-5, AC-9, AC-11 · Service PC / Admin · TC-002-31 done*

1. Windows app mode → **Hell**.
2. Install `EagleEye-ParentApp-Setup-0.2.0.exe` again with defaults, start the app.
3. Host dialog: `<host>` → *Verbinden*.
4. Pair with device name **Papas PC** (code from the Event Log, or sign the Kid in and read the popup).

**Expected**:
- Step 2: host dialog immediately (old pairing and settings gone); app **light**, switch shows *Hell*.
- Step 3: leads directly to the pairing form.
- Step 4: green "Verbunden mit `<host>`" (start state for Block C).

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

## Block C — Second PC (PC2)

*Setup before Block C:*

- [ ] **S-9** PC2: Windows 11, same LAN and subnet. `EagleEye-ParentApp-Setup-0.2.0.exe` copied to PC2. PowerShell on PC2: `Resolve-DnsName <host>` returns `<ip>` (if not, note it in TC-002-34; network issue, not product). Optional: PC2 app mode opposite to the service PC.
- [ ] **S-10** Service PC: Admin app paired and green (end of Block B). `eagleeye-kid` signed in (tray running), then back to Admin.

*Pair one app at a time. If PC2 cannot be used on the day, mark the cases Blocked with the reason.*

### TC-002-33: On a PC without .NET the app installs and starts without further steps

*Verifies AC-2, AC-1, AC-9 · PC2 / any local account · parent app never installed on PC2*

1. PowerShell on PC2: `dotnet --list-runtimes`. *Installierte Apps*: search ".NET".
2. Run `EagleEye-ParentApp-Setup-0.2.0.exe` with defaults, finish (untick "start now" if offered).
3. Start menu → **EagleEye Parent App**.

**Expected**:
- Step 1: `dotnet` not recognised (or no runtimes); no ".NET … Runtime" installed. If PC2 has a .NET runtime: **Blocked** ("PC2 has .NET"), note which.
- Step 2: wizard completes without errors, without asking for or downloading other software.
- Step 3: app starts with the host dialog; theme matches the PC2 app mode.
- *Note only*: if PC2 runs Windows in English, whether the app texts are English.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

### TC-002-34: The service is reachable from PC2 without any configuration

*Verifies AC-6 · PC2 · nothing configured by hand on the service PC*

1. PowerShell on PC2: `Test-NetConnection <host> -Port 5443`
2. `Test-NetConnection <host> -Port 5080` *(supporting)*

**Expected**:
- Step 1: `TcpTestSucceeded : True`.
- Step 2: `TcpTestSucceeded : False`.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

### TC-002-35: TLS from PC2, no certificate prompt

*Verifies AC-7 · PC2*

1. Edge on PC2 → `https://<host>:5443/`.

**Expected**:
- Certificate warning page as in TC-002-29. During all Block C cases the app never shows a certificate prompt.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

### TC-002-36: Pairing from PC2 works exactly as on the service PC

*Verifies AC-23, AC-1, AC-13, AC-14, AC-16 · PC2 + service PC / Kid (look only) · host dialog open on PC2, Kid tray running*

1. PC2: host dialog → `<host>` → *Verbinden*.
2. Service PC: switch to `eagleeye-kid`, read the code in **"EagleEye – Eltern-App koppeln"**, switch back.
3. PC2: device name **PC2**, enter the code → *Koppeln*.

**Expected**:
- Same as TC-002-11/-12/-15: pairing form, red "Verbindung zu `<host>` wird hergestellt …"; 6-digit popup in the Kid session; then **green "Verbunden mit `<host>`"**, *Gekoppelt*, host `<host>`, device **PC2**.
- Any difference from the service PC goes into Observed.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

### TC-002-37: Two apps on two PCs are connected at the same time

*Verifies AC-24 · PC2 + service PC / Admin · both paired*

1. Look at the status bar on PC2 and on the service PC (Admin, app open).

**Expected**:
- Both **green "Verbunden mit `<host>`"** at the same time.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

### TC-002-38: Paired operation on PC2: auto-connect and reconnect

*Verifies AC-21, AC-22, AC-23 · PC2 + service PC / Admin (+ Terminal (Administrator)) · TC-002-37*

1. PC2: close the app, start it from the Start menu; stopwatch.
2. Service PC: `Stop-Service -DisplayName "EagleEye Service"`; stopwatch. Watch both apps.
3. Wait 1 minute. `Start-Service -DisplayName "EagleEye Service"`; stopwatch. Watch both apps.

**Expected**:
- Step 1: PC2 green "Verbunden mit `<host>`" within **30 s**, no input.
- Step 2: both **red** "Nicht verbunden mit `<host>`" within **30 s**.
- Step 3: both **green** within **60 s**, no user action.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**: <!-- times for steps 1-3 -->

---

### TC-002-39: Removing the pairing on PC2 does not affect the other app

*Verifies AC-29, AC-26, AC-27, AC-23 · PC2 + service PC / Admin · both green*

1. PC2: *Serververbindung* → **Kopplung aufheben** → confirm.
2. Watch the service PC app for **60 s**.

**Expected**:
- PC2: confirmation first, then *Nicht gekoppelt*, editable host field, red.
- Service PC app stays **green "Verbunden mit `<host>`"** the whole time.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

### TC-002-40: Uninstall on PC2 is clean

*Verifies AC-5 · PC2 · app closed*

1. Same steps as TC-002-31, on PC2.

**Expected**:
- Same as TC-002-31: no program folder, no `%LocalAppData%\EagleEye`, no Start menu entry, no entry in *Installierte Apps*.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

## Block D — Second parent app on the service PC (second Windows account)

*Setup before Block D:*

- [ ] **S-11** `eagleeye-parent2` created (standard account; commands in test plan §3, S-11). `EagleEye-ParentApp-Setup-0.2.0.exe` copied to `C:\Users\Public\Downloads`. Admin app paired and green.

### TC-002-41: A second parent account on the service PC can install and pair its own app

*Verifies AC-1 · Service PC / Parent 2, then Admin · Admin app green, Admin stays signed in*

1. *Benutzer wechseln* → `eagleeye-parent2`.
2. Run `C:\Users\Public\Downloads\EagleEye-ParentApp-Setup-0.2.0.exe` with defaults, start the app.
3. Host dialog: `<host>` → *Verbinden*. Code from the popup in this session, device name **Eltern 2** → *Koppeln*.
4. *Benutzer wechseln* → Admin; look at the Admin app.

**Expected**:
- Installation and pairing as in Block A; **green "Verbunden mit `<host>`"**, device **Eltern 2**.
- Admin app still **green** at the same time.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

### TC-002-42: Removing the pairing of the second app does not affect the Admin app

*Verifies AC-29 · Service PC / Parent 2, then Admin · TC-002-41*

1. *Benutzer wechseln* → `eagleeye-parent2`. *Serververbindung* → **Kopplung aufheben** → confirm.
2. *Benutzer wechseln* → Admin; watch the Admin app for **60 s**.

**Expected**:
- Parent 2 app: *Nicht gekoppelt*, red.
- Admin app stays **green "Verbunden mit `<host>`"**.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

### TC-002-43: Uninstalling while the app is running still removes everything

*Verifies AC-5 · Service PC / Parent 2 · TC-002-42*

1. *Benutzer wechseln* → `eagleeye-parent2`. Start the app and **leave it open**.
2. *Installierte Apps* → **EagleEye Parent App** → *Deinstallieren*.
3. Run the checks of TC-002-31 steps 2 and 3 in this account.

**Expected**:
- Uninstaller finishes without errors (running app closed).
- Same as TC-002-31: nothing of the parent app left in this account.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

## Cleanup (test plan §7)

- [ ] Service PC Admin: parent app stays installed and paired (end of TC-002-32).
- [ ] Windows app mode back to your usual setting (S-6).
- [ ] `Test-Path "$env:LOCALAPPDATA\EagleEye-TC25-Kopie"` → `False` (delete if it exists).
- [ ] `eagleeye-parent2` signed out, profile and account removed (commands in test plan §7 step 4); installer copy in `C:\Users\Public\Downloads` deleted.
- [ ] PC2: copied installer deleted.
- [ ] `eagleeye-kid` signed out.

---

## General Feedback

<!-- Anything that does not fit a test case: usability remarks, wanted text changes, ideas, surprises. -->

---

## Summary (filled in by TES after evaluation)

| Block | Cases | Pass | Fail | Blocked | Skipped | Not executed |
|---|---|---|---|---|---|---|
| A (TC-002-01..16) | 16 | 9 | 2 | 0 | 1 | 4 |
| B (TC-002-17..32) | 16 | 0 | 0 | 0 | 0 | 16 |
| C (TC-002-33..40) | 8 | 0 | 0 | 0 | 0 | 8 |
| D (TC-002-41..43) | 3 | 0 | 0 | 0 | 0 | 3 |
| Regression (REG-001-01..05) | 5 | 2 | 0 | 0 | 3 | 0 |
| **Total** | **48** | **11** | **2** | **0** | **4** | **31** |

Evaluated by TES, 2026-10-04: see `docs/testing/US-002/test-report.md`. Issues: ISSUE-004 (TC-002-12), ISSUE-005 (TC-002-02).

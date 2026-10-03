# Test Run 01: US-001 — Basic Service Installation and Tray Client Connectivity

**Test plan**: `docs/testing/US-001/test-plan.md`
**Prepared by**: TES, 2026-10-03
**Executed by**: Michael
**Execution date**: <!-- fill in -->
**Build / installer version**: `EagleEye-Setup-0.1.0.exe`
**Machine(s)**: Windows Developer Machine <!-- add Windows version, e.g. 11 Pro 25H2 -->

> **How to record**: tick exactly one result box per case (`[x]`). Fill in **Observed** for Fail/Blocked. Screenshots go into `docs/testing/US-001/evidence/` (e.g. `tc-05-green.png`); reference them in Notes. When you are done, or want to stop halfway, tell TES: *"test run 01 for US-001 is done"*.
>
> **Accounts**: **Admin** = your administrator account. **Kid** = `eagleeye-kid` (standard user).
> **Duration**: about 45 min, including one reboot (block D).

---

## Setup (Admin)

- [x] **S-1** EagleEye is *not* listed in *Einstellungen → Apps → Installierte Apps* (uninstall it if it is).
- [x] **S-2** Port 5080 is free. In PowerShell, `Get-NetTCPConnection -LocalPort 5080 -ErrorAction SilentlyContinue` prints nothing.
- [x] **S-3** The standard account `eagleeye-kid` exists and has signed in at least once (commands in test plan §3, step 3).
- [x] **S-4** You are signed in with your **Admin** account. No other user session is open (*Strg+Alt+Entf → Task-Manager → Benutzer* shows only you).

---

## Block A — Installation (Admin)

### TC-001-01: Installer runs with default settings and installs both components

*Verifies AC-1 · Windows / Admin*

1. Double-click `03_Delivery\windows\EagleEye-Setup-0.1.0.exe`. If SmartScreen appears: *Weitere Informationen* → *Trotzdem ausführen*. Confirm the UAC prompt.
2. Language dialog: keep the default and click OK.
3. Click through the wizard with **all default settings** (*Weiter* … *Installieren*). On the last page leave "Start the EagleEye tray icon now" **unticked** and click *Fertigstellen*.
4. Open Explorer at `C:\Program Files\EagleEye`.
5. Open *Einstellungen → Apps → Installierte Apps* and search for "EagleEye".

**Expected**:
- The wizard finishes without any error message.
- `C:\Program Files\EagleEye\Service\EagleEye.Service.exe` and `C:\Program Files\EagleEye\TrayClient\EagleEye.TrayClient.exe` exist.
- "EagleEye" (version 0.1.0) is listed under installed apps.

- **Result**: [X ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**: The app shows "adlemich" as author of the app in Apps - Installed Apps. Should be "Michael Adler".
- **Notes**:

---

### TC-001-02: Service registered, running as Local System, startup type Automatic

*Verifies AC-2 · Windows / Admin*

1. Press *Win+R*, type `services.msc`, press Enter.
2. Find **EagleEye Service** in the list.
3. Optional cross-check in PowerShell:
   `Get-CimInstance Win32_Service -Filter "Name='EagleEyeService'" | Format-List DisplayName,State,StartMode,StartName`

**Expected**:
- *Status*: **Wird ausgeführt** (Running)
- *Starttyp*: **Automatisch** (Automatic)
- *Anmelden als*: **Lokales System** (Local System)
- Cross-check (if done): `State : Running`, `StartMode : Auto`, `StartName : LocalSystem`

- **Result**: [X ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**: All as expected
- **Notes**:

---

## Block B — Service Control (Admin)

### TC-001-03: Service can be stopped and started in services.msc

*Verifies AC-4 · Windows / Admin*

1. In `services.msc`, right-click **EagleEye Service** → *Beenden* (Stop).
2. Wait until the dialog closes. Press F5 to refresh.
3. Right-click **EagleEye Service** → *Starten* (Start). Press F5.

**Expected**:
- After step 2: *Status* is empty (stopped) and no error dialog appeared.
- After step 3: *Status* is **Wird ausgeführt** again and no error dialog appeared.

- **Result**: [X ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

> Leave the service **running** and sign out of the Admin account (*Start → your name → Abmelden*).

---

## Block C — Kid Session

### TC-001-04: Tray client starts automatically at kid logon

*Verifies AC-5 · Windows / Kid*

1. Sign in as **eagleeye-kid**. Do not start anything.
2. Look at the notification area (bottom right). If no EagleEye icon is visible, click the **^** arrow (*Ausgeblendete Symbole anzeigen*).
3. Open Task Manager (*Strg+Umschalt+Esc*) → tab *Details* and find `EagleEye.TrayClient.exe`.

**Expected**:
- An EagleEye icon (a round disc with a white eye) is present, either in the visible area or behind the ^ arrow.
- `EagleEye.TrayClient.exe` is running with *Benutzername* = `eagleeye-kid`.

Tip: to keep the icon always visible for the following cases, go to *Einstellungen → Personalisierung → Taskleiste → Andere Symbole der Taskleiste* and switch on EagleEye.

- **Result**: [X ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

### TC-001-05: Tray icon is green and tooltip says connected

*Verifies AC-6, AC-7 · Windows / Kid · service is running*

1. Look at the EagleEye tray icon.
2. Hover the mouse over it for 2 seconds.

**Expected**:
- The icon is **green**.
- The tooltip reads **"EagleEye — Connected"**.

- **Result**: [X ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**: Functionality is good, but it uses english texts "Connected" instead of "Verbunden". 
- **Notes**:

---

### TC-001-06: Right-click shows a context menu with "About"

*Verifies AC-11 · Windows / Kid*

1. Right-click the EagleEye tray icon.

**Expected**:
- A context menu opens and contains an entry **"About"**.

- **Result**: [X ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**: Functionality is good, but it uses english texts "About" instead of "App Infos". 
- **Notes**:

---

### TC-001-07: About shows the live server version

*Verifies AC-10, AC-12 · Windows / Kid · service is running*

1. Right-click the tray icon → click **About**.
2. Read the dialog, then click **OK**.

**Expected**:
- A dialog titled **"About EagleEye"** opens.
- It shows **"Server Version: EagleEye_v0.1"**: format `EagleEye_vMAJOR.MINOR`, matching installer version 0.1.0.
- **OK** closes the dialog.

- **Result**: [X ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

> **Getting admin rights inside the kid session** (needed for TC-08, TC-10, TC-11): right-click the Start button → **Terminal (Administrator)** → enter your **Admin** credentials in the UAC prompt. In that window type `services.msc` and press Enter. This opens the services console with admin rights, **while the kid's tray icon stays visible**. Keep both windows open for the next cases.
> (Alternative to the console: `Stop-Service EagleEyeService` / `Start-Service EagleEyeService` in the same admin terminal.)

### TC-001-08: Stopping the service turns the tray icon red

*Verifies AC-6, AC-8 · Windows / Kid (+ admin services console)*

1. Note the time. In the admin `services.msc`: **EagleEye Service** → *Beenden*.
2. Watch the tray icon for up to 30 seconds.
3. Hover over the icon.

**Expected**:
- Within **30 s** the icon turns **red**.
- The tooltip reads **"EagleEye — Disconnected"**.
- The tray icon itself stays present (the tray client does not exit).

- **Result**: [X] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**: Functionality is good, but it uses english texts "Disconneced" instead of "Verbindungsfehler".  It took around 15 seconds.
- **Notes**:

---

### TC-001-09: Exploratory: About while the service is stopped

*No pass/fail criterion from the story; your observation goes to PRO as feedback · Windows / Kid · service is stopped*

1. Right-click the (red) tray icon → **About**.
2. Read the dialog, then click **OK**.

**Expected**: not specified by US-001. Please record what you see and whether you find it acceptable.

- **Result**: [X] Pass (acceptable)  [] Fail (not acceptable)  [ ] Skipped
- **Observed**: This dialog is not multi-language enabled, it should display german texts (see before comments). In addition, it could give more information not only showing "unavailable" but rather say "Connection error, could not connect to Server at IP-Port" (in target language).
- **Notes**:

---

### TC-001-10: Restarting the service turns the icon green again automatically

*Verifies AC-9 · Windows / Kid (+ admin services console)*

1. Note the time. In the admin `services.msc`: **EagleEye Service** → *Starten*.
2. Do **not** touch the tray icon. Watch it for up to 60 seconds.
3. Hover over the icon.

**Expected**:
- Without any user action, the icon turns **green** within **60 s**.
- The tooltip reads **"EagleEye — Connected"**.

- **Result**: [X] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**: <!-- roughly how many seconds -->
- **Notes**:

---

### TC-001-11: Reconnect also works after a long outage

*Verifies AC-9 (no time limit on the outage) · Windows / Kid (+ admin services console)*

1. In the admin `services.msc`: **EagleEye Service** → *Beenden*. Check that the icon turns red.
2. Wait **at least 2 minutes**.
3. Note the time. Start **EagleEye Service** again.
4. Watch the tray icon for up to 60 seconds without touching it.
5. Open **About** once more.

**Expected**:
- The icon turns **green** within **60 s** after the start.
- About shows **"Server Version: EagleEye_v0.1"** again.

- **Result**: [X] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

> Close the admin terminal and `services.msc`. **Leave the service running.** Then restart the PC: *Start → Ein/Aus → Neu starten*.

---

## Block D — After Reboot

### TC-001-12: Service starts automatically after reboot

*Verifies AC-3 · Windows / Admin*

1. After the reboot, sign in as **Admin** (not as the kid).
2. Do not start anything manually. Open `services.msc` (*Win+R*).
3. Find **EagleEye Service**.

**Expected**:
- *Status* is **Wird ausgeführt** without any manual start.

- **Result**: [X] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

### TC-001-13: After reboot, the kid logon auto-starts a green tray icon

*Verifies AC-5, AC-7 · Windows / Kid*

1. *Start → your name → Benutzer wechseln* (Switch user), and sign in as **eagleeye-kid**.
2. Look at the EagleEye tray icon and hover over it.

**Expected**:
- The tray icon is present without any manual start.
- It is **green** (possibly briefly red during the first seconds after logon), and the tooltip reads **"EagleEye — Connected"**.

- **Result**: [X] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

## Regression

None. US-001 is the first user story.

---

## General Feedback

Looks good so far, the basics are working. Make sure that multi-language is used in all areas including the about box.
---

## Summary (filled in by TES after evaluation)

| Pass | Fail | Blocked | Skipped | Not executed |
|---|---|---|---|---|
| 13 | 0 | 0 | 0 | 0 |

Evaluated by TES on 2026-10-03. Findings: ISSUE-001 (localization), ISSUE-002 (publisher name), ISSUE-003 (PRO feedback). See `test-report.md`.

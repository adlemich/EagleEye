# Test Plan: US-002 — Windows Parent App: Installation, Connection and Pairing

**Status**: Approved (Michael, 2026-10-04); updated after test run 01, see §9 Change Log
**Date**: 2026-10-04 (last change 2026-10-04)
**Author**: TES
**User Story**: `02_Implementation/docs/requirements/user-stories/US-002/user-story.md` (30 ACs)
**Inputs used**: the user story, `02_Implementation/docs/requirements/general-product-requirements.md`, the "How to test" section of `02_Implementation/docs/requirements/user-stories/US-002/implementation-report.md` (artifacts, setup, German UI labels), "Manual Verification Notes" and "Open Points for Michael" of `02_Implementation/docs/requirements/user-stories/US-002/implementation-plan.md`. Black box: no source code was read.

---

## 1. Scope

**In scope**

- Parent app installer: install, repair, uninstall, version and publisher (AC-1 to AC-5)
- Service reachability from the LAN without manual configuration, TLS without certificate prompts (AC-6, AC-7)
- Main window: menu, settings page, light/dark switch, status bar (AC-8 to AC-10)
- First connection and pairing: host dialog, connection errors, pairing code in the tray client or the Event Log, wrong / expired code, empty device name, no access while unpaired (AC-11 to AC-20)
- Paired operation: automatic connect, reconnect, host read-only, two apps at the same time, service PC and another PC (AC-21 to AC-25)
- Removing the pairing (AC-26 to AC-30)
- Supporting checks that DEV could not run without admin rights: data folder ACLs, firewall rule, ports, no pairing codes in the service log (marked *supporting*; they verify the ARC design, ADR-008, not an AC)

**Out of scope** (as in the story): parent apps for Android, iOS and macOS; listing or removing *other* paired devices; any parent feature beyond the connection; remote access outside the LAN, discovery; changing the port; showing the service version in the parent app; service file logs; code signing.

**Not tested in this plan, with reason**

| Item | Reason |
|---|---|
| AC-4 update to a **newer** version | Only build 0.2.0 exists. This run checks repair (same version). The update path is checked with the next parent app build (TES proposes it for the regression checklist). |
| English UI texts | Michael's Windows is German. The English texts are covered by unit tests. If the second PC has an English display language, Block C records the English texts as a bonus (Notes). |
| Certificate change on the service while an app runs (DEV report §5.4) | Not specified by the story. Not provoked. |
| Two pairings started at the same moment (DEV report §5.5) | Not specified by the story. The plan pairs one app at a time. |

## 2. Test Environment

| Item | Value |
|---|---|
| Service PC | **Windows Developer Machine** (Windows 11 Pro, German UI). Called `<host>` below = its computer name (`hostname`); `<ip>` = its LAN IPv4 address (`ipconfig`). |
| Second PC (**Block C only**) | Another **Windows 11 PC in the same LAN and subnet**, with **no .NET runtime** installed, called **PC2**. Any local account works (the parent app installer needs no admin rights). |
| Accounts on the service PC | **Admin**: Michael's administrator account (parent). **Kid**: local standard account `eagleeye-kid`. **Parent 2** (**Block D only**): local standard account `eagleeye-parent2`, created in setup S-11. |
| Builds under test | `03_Delivery/windows/EagleEye-Setup-0.2.0.exe` (service + tray, admin) and `03_Delivery/windows/EagleEye-ParentApp-Setup-0.2.0.exe` (parent app, per user) |
| Expected versions | Service installer 0.2.0 → tray *App Infos* shows `EagleEye_v0.2`. Parent app: *Installierte Apps* "EagleEye Parent App", 0.2.0, Michael Adler |
| Tools | Stopwatch (phone), **Terminal (Administrator)** = an **elevated** terminal (see S-8) for starting/stopping the service and for the folder checks, Event Viewer (`eventvwr.msc`), `wf.msc`, Microsoft Edge |
| Time needed | Block A ≈ 40 min · Block B ≈ 60 min (contains a 5½-minute and a 2-minute wait) · Block C ≈ 35 min · Block D ≈ 20 min |

### Test blocks

| Block | Cases | Machine / accounts | Needs second PC |
|---|---|---|---|
| **A** Service installer, parent app installer, first pairing | TC-002-01 to TC-002-16 (16) | Service PC / Admin + Kid | no |
| **B** Paired operation, reconnect, removing the pairing, Event Log, expiry, uninstall | TC-002-17 to TC-002-32 (16) | Service PC / Admin | no |
| **C** Second PC: clean PC install, LAN reachability, two apps | TC-002-33 to TC-002-40 (8) | PC2 + service PC / Admin + Kid | **yes** |
| **D** Second parent app on the service PC (second Windows account) | TC-002-41 to TC-002-43 (3) | Service PC / Admin + Parent 2 | no |

Blocks A and B run in this order in one session. Michael has a second Windows PC (confirmed 2026-10-04), so Block C is part of the run; it can run later than A and B. Only if PC2 cannot be used on the day, mark Block C **Blocked** with the reason; this does not block Blocks A, B and D. Block D also runs: it adds a same-PC, second-account check of AC-1, AC-5 and AC-29.

### Conventions for expected results

- **UI texts.** The German texts in the expected results are the texts of the build (DEV handover). A different wording with the same meaning goes into **Notes**, not Fail (agreed with Michael, 2026-10-04). If you want a text changed, write the wanted text into **Notes**; TES routes it to PRO. Exceptions, where the wording was agreed in the story and is a Pass criterion: *Einstellungen*, *Darstellung*, *Serververbindung*, *Hell* / *Dunkel*, *Verbunden mit <host>*, *Kopplung aufheben*.
- **`<host>` in texts** is exactly what was typed (hostname or IP address, AC-10).
- **Colours.** Green / red = the connection indicator in the status bar of the parent app (bottom of the window).
- **Timing.** Take times with a stopwatch. Bounds from the story: green within 30 s after app start (AC-21), red within 30 s after the service stops, green within 60 s after it starts again (AC-22). Where the story gives no bound (e.g. connection error, AC-12), a slow result is a Note, not a Fail.
- **Certificate prompts (AC-7).** In every case of this plan: if the parent app ever shows anything about a certificate (warning, question, import), write it into **Observed** of the current case and mark it Fail.
- **Pairing code popup in the admin session.** The tray client also runs in admin sessions (open point 2 of the implementation plan; Michael decided on 2026-10-04 that it stays that way). After TC-002-12 you may read further codes from the popup in your admin session, which saves switching users.
- **Unspecified behaviour** that you notice (e.g. a message you did not expect) is a Note; TES routes it to PRO.

## 3. Setup Instructions

Service PC, Admin account, before Block A:

1. **S-1 Starting point.** *Einstellungen → Apps → Installierte Apps*: **EagleEye 0.1.1** is installed (end state of US-001). Do **not** uninstall it; Block A also checks the update to 0.2.0. "EagleEye Parent App" must **not** be listed.
2. **S-2 No leftover parent app data.** In PowerShell: `Test-Path "$env:LOCALAPPDATA\EagleEye"`, `Test-Path "$env:LOCALAPPDATA\Programs\EagleEye Parent App"` → both `False`. If `True` (e.g. from a DEV smoke check), delete the folder.
3. **S-3 No development instances.** *Task-Manager → Details*: no `EagleEye.Service.exe` other than the installed service (none at all running from a `02_Implementation` folder), no `EagleEye.ParentApp.exe`.
4. **S-4 Kid account.** `eagleeye-kid` exists (US-001). It is signed out at the start.
5. **S-5 Note the names.** In PowerShell: `hostname` → write down as `<host>`. `ipconfig` → IPv4 address of the LAN adapter → write down as `<ip>`. Use these two values in all cases.
6. **S-6 Windows app mode = Dunkel.** *Einstellungen → Personalisierung → Farben → Modus auswählen* = **Dunkel** (or *Benutzerdefiniert* with *Standard-App-Modus auswählen* = **Dunkel**). Note your usual setting so you can restore it in cleanup. TC-002-06 checks that the app follows this; TC-002-32 later checks the opposite mode.
7. **S-7 Unsigned installers.** SmartScreen may show *"Der Computer wurde durch Windows geschützt"*. Click *Weitere Informationen* → *Trotzdem ausführen*. Expected, not a test failure.
8. **S-8 Terminal (Administrator)** open. It must run **elevated** ("als Administrator"). A normal terminal in the admin account is **not** enough: with UAC it has no administrator rights (this caused the Fail of TC-002-02 in run 01, ISSUE-005).
   - Start menu → search **Terminal** (or **PowerShell**) → right-click → **Als Administrator ausführen** → confirm the UAC prompt with *Ja*.
   - Check: the title bar (tab title) starts with **"Administrator:"**. Optional check: `([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)` returns `True`.
   - Use this terminal for **every** command in this plan that is marked *Terminal (Administrator)*. The service commands used in this plan:
   ```powershell
   Stop-Service -DisplayName "EagleEye Service"
   Start-Service -DisplayName "EagleEye Service"
   ```

Before Block C only:

9. **S-9 Second PC (PC2).** Windows 11, same LAN and subnet as the service PC (the firewall rule allows the *local subnet* only). Copy `EagleEye-ParentApp-Setup-0.2.0.exe` to PC2 (USB stick or network share). Check in PowerShell on PC2: `Resolve-DnsName <host>` returns `<ip>` (if name resolution fails, write it in Observed of TC-002-34; it is a network issue, not a product issue). Set the PC2 app mode to the mode you will **not** have on the service PC at that time, if you want a third theme sample (optional).
10. **S-10 Service PC state for Block C.** The Admin parent app on the service PC is paired and green (end state of Block B). Sign in `eagleeye-kid` on the service PC (*Benutzer wechseln*) so that the kid's tray client runs, then switch back to Admin.

Before Block D only:

11. **S-11 Second parent account on the service PC.** In Terminal (Administrator):
    ```powershell
    $pw = Read-Host -AsSecureString "Password for eagleeye-parent2"
    New-LocalUser -Name "eagleeye-parent2" -Password $pw -FullName "EagleEye Parent 2" -PasswordNeverExpires
    Add-LocalGroupMember -Group (Get-LocalGroup -SID "S-1-5-32-545").Name -Member "eagleeye-parent2"
    ```
    It is a standard account on purpose: the parent app installer needs no admin rights. Copy `EagleEye-ParentApp-Setup-0.2.0.exe` to `C:\Users\Public\Downloads` so that the account can reach it.

## 4. Acceptance Criteria Coverage

Cases in **bold** need the second PC (Block C).

| AC | Test case(s) |
|---|---|
| AC-1 | TC-002-04, TC-002-15, **TC-002-33**, **TC-002-36**, TC-002-41 |
| AC-2 | **TC-002-33** (on the service PC, TC-002-04 only shows that the wizard installs; the service PC has .NET, so it cannot prove AC-2) |
| AC-3 | TC-002-04, TC-002-05 |
| AC-4 | TC-002-18 (repair with the same version; see §1 for "newer") |
| AC-5 | TC-002-31, TC-002-32, **TC-002-40**, TC-002-43 |
| AC-6 | TC-002-01 (service side: rule and port set up by the installer), **TC-002-34** (actual reach from another PC) |
| AC-7 | TC-002-29, **TC-002-35**, plus the certificate-prompt rule in §2 for every case |
| AC-8 | TC-002-07, TC-002-15, TC-002-16, TC-002-22 |
| AC-9 | TC-002-06, TC-002-08, TC-002-17, TC-002-18, TC-002-32 |
| AC-10 | TC-002-07, TC-002-11, TC-002-15, TC-002-19, TC-002-28 |
| AC-11 | TC-002-06, TC-002-07, TC-002-24, TC-002-28, TC-002-32 |
| AC-12 | TC-002-09, TC-002-10 |
| AC-13 | TC-002-11, TC-002-26 |
| AC-14 | TC-002-12, **TC-002-36** |
| AC-15 | TC-002-26 |
| AC-16 | TC-002-15, TC-002-28 |
| AC-17 | TC-002-13 |
| AC-18 | TC-002-14 |
| AC-19 | TC-002-27 |
| AC-20 | TC-002-11, TC-002-14, TC-002-25 (the server-side refusal of every non-pairing call is only partly observable from outside; DEV covers it with unit tests, implementation report §4 `PairingAuthorizationHubFilter`) |
| AC-21 | TC-002-17, **TC-002-38** |
| AC-22 | TC-002-19, TC-002-21, **TC-002-38** |
| AC-23 | TC-002-15 (service PC, parent's own account), **TC-002-36**, **TC-002-38**, **TC-002-39** (other PC) — needs both setups for Pass |
| AC-24 | **TC-002-37** |
| AC-25 | TC-002-16 |
| AC-26 | TC-002-22, **TC-002-39** |
| AC-27 | TC-002-23, TC-002-24, **TC-002-39** |
| AC-28 | TC-002-25, TC-002-28 |
| AC-29 | **TC-002-39**, TC-002-42 |
| AC-30 | TC-002-20 |

All 30 ACs have at least one case. **AC-2, AC-6 (LAN part), AC-23 (other-PC part) and AC-24** get their result only in Block C (second PC, available). AC-29 is covered by Block C and, independently, by Block D.

## 5. Test Cases

### Block A — Service PC: installers and first pairing

*Service PC · Admin, with one visit to the Kid account. Start state: setup S-1 to S-8 done.*

#### TC-002-01: Service update to 0.2.0 sets up network access by itself

- **Verifies**: AC-6 (service side)
- **Machine / account**: Service PC / Admin
- **Precondition**: EagleEye 0.1.1 installed, service running

**Steps**

1. Run `03_Delivery\windows\EagleEye-Setup-0.2.0.exe` with default settings and finish the wizard.
2. `services.msc` → **EagleEye Service**.
3. In PowerShell: `netstat -ano | findstr "5443 5080"`.
4. `wf.msc` → *Eingehende Regeln* → find **EagleEye Service (Parent apps)** and look at the columns (or double-click the rule).

**Expected result**

- The wizard finishes without an error and **never** asks about a firewall, port or certificate.
- **EagleEye Service** is *Wird ausgeführt*.
- Port 5443 is listening for the network: `0.0.0.0:5443` and `[::]:5443` *ABHÖREN* (LISTENING). Port 5080 appears only as `127.0.0.1:5080` and `[::1]:5080` (local only, not on `0.0.0.0`).
- The rule exists: *Aktiviert* = Ja, *Aktion* = Zulassen, *Profil* = Alle, *Protokoll* TCP, *Lokaler Port* 5443, *Remoteadresse* = Lokales Subnetz, *Programm* = `C:\Program Files\EagleEye\Service\EagleEye.Service.exe`.

#### TC-002-02: Service data folder is protected *(supporting)*

- **Verifies**: — (supporting check, ADR-008 / NFR-S-014; DEV could not run the installer)
- **Machine / account**: Service PC / Admin, **Terminal (Administrator)** (elevated, S-8)
- **Precondition**: TC-002-01 done

**Steps**

0. Use the **elevated** terminal from S-8: its title bar must show **"Administrator:"**. If not, open one: Start menu → **Terminal** → right-click → **Als Administrator ausführen**. In a non-elevated terminal, steps 2 and 3 end with *Zugriff verweigert*. That is Windows UAC, not a product error.
1. In the Terminal (Administrator): `icacls "$env:ProgramData\EagleEye"`
2. `icacls "$env:ProgramData\EagleEye\certs"`
3. `Test-Path "$env:ProgramData\EagleEye\certs\eagleeye.pfx"`

**Expected result**

- `EagleEye`: entries only for `NT-AUTORITÄT\SYSTEM` (F), `VORDEFINIERT\Administratoren` (F) and `VORDEFINIERT\Benutzer` (RX). No entry carries the `(I)` mark (nothing inherited).
- `certs`: entries only for SYSTEM (F) and Administratoren (F). No *Benutzer* entry.
- The certificate file exists (`True`).

> **Switch to the Kid account** (*Benutzer wechseln* → `eagleeye-kid`; do not sign Admin out). Then TC-002-03.

#### TC-002-03: The kid cannot open the service certificate *(supporting)*

- **Verifies**: — (supporting check, ADR-008 / NFR-S-014)
- **Machine / account**: Service PC / Kid
- **Precondition**: signed in as `eagleeye-kid`

**Steps**

1. Explorer → address bar `C:\ProgramData\EagleEye\certs` → Enter.
2. PowerShell (normal, not elevated): `Get-ChildItem C:\ProgramData\EagleEye\certs`

**Expected result**

- Explorer refuses access (*"Sie verfügen momentan nicht über die Berechtigung…"*; do **not** click *Fortsetzen*, it would ask for admin credentials).
- PowerShell reports access denied (*Zugriff verweigert*). No file is listed.

> **Switch back to Admin** (*Benutzer wechseln*). Leave the Kid **signed in** in the background; it is needed for TC-002-12.

#### TC-002-04: Parent app installer is a guided wizard and installs with default settings

- **Verifies**: AC-1, AC-3
- **Machine / account**: Service PC / Admin
- **Precondition**: parent app not installed (S-2)

**Steps**

1. Run `03_Delivery\windows\EagleEye-ParentApp-Setup-0.2.0.exe`.
2. Read the first wizard page. Then click through with the default settings. On the last page, **untick** the option to start the app now (if offered), then *Fertigstellen*.
3. Open the Start menu and search for "EagleEye Parent App".

**Expected result**

- The installer is a wizard (several pages with *Weiter* / *Installieren* / *Fertigstellen*). It names the product (EagleEye parent app) and shows version **0.2.0**.
- It finishes without an error and without asking to install any other software.
- The Start menu has an entry **EagleEye Parent App**.
- *Note only*: whether a UAC (admin) prompt appeared. The story does not require "no admin", but the DEV design is per user without admin rights.

#### TC-002-05: Installed apps shows name, version and publisher

- **Verifies**: AC-3
- **Machine / account**: Service PC / Admin
- **Precondition**: TC-002-04 done

**Steps**

1. *Einstellungen → Apps → Installierte Apps* → search "EagleEye".

**Expected result**

- **EagleEye Parent App** is listed, separately from **EagleEye** (the service).
- It shows version **0.2.0** and publisher **Michael Adler**.

#### TC-002-06: First start shows the host dialog at once, in the Windows app mode

- **Verifies**: AC-11, AC-9 (first start)
- **Machine / account**: Service PC / Admin
- **Precondition**: Windows app mode = **Dunkel** (S-6); the app has never been started

**Steps**

1. Start menu → **EagleEye Parent App**.

**Expected result**

- Without any further step, the app opens and **immediately** shows the dialog **"Mit dem EagleEye-PC verbinden"**, which asks for the hostname or IP address of the EagleEye PC, with the buttons *Verbinden* and *Abbrechen*.
- The app (dialog and window behind it) is **dark**.

#### TC-002-07: Cancelling the host dialog shows the unpaired main window

- **Verifies**: AC-11 (cancel path), AC-8, AC-10 (not connected)
- **Machine / account**: Service PC / Admin
- **Precondition**: host dialog open (TC-002-06)

**Steps**

1. Click *Abbrechen*.
2. Look at the main window: menu on the left, content, status bar at the bottom.
3. Click the menu entry **Einstellungen** (if it is not already selected).

**Expected result**

- Desktop-style main window. The left navigation menu has the entry **Einstellungen** (the only entry).
- The settings page shows two sections in this order: **Darstellung** (with a switch) and **Serververbindung**.
- *Serververbindung* shows the status **Nicht gekoppelt**, an **editable** field for the hostname or IP address and a button to connect (*Verbinden*).
- The status bar shows a **red** indicator and **"Nicht verbunden"**.

#### TC-002-08: The light/dark switch applies immediately

- **Verifies**: AC-9
- **Machine / account**: Service PC / Admin
- **Precondition**: main window, settings page (TC-002-07)

**Steps**

1. In *Darstellung*, look at the switch: it shows **Dunkel**.
2. Switch it to **Hell**.
3. Switch it back to **Dunkel**, then again to **Hell** (leave it on *Hell*).

**Expected result**

- Before step 2 the switch shows *Dunkel* (matches the app mode, TC-002-06).
- Each change applies to the **whole** window (menu, content, status bar) **at once**, without a restart.
- The switch label shows the current mode (*Hell* / *Dunkel*).

#### TC-002-09: Unknown host name gives a connection error that names the host

- **Verifies**: AC-12
- **Machine / account**: Service PC / Admin
- **Precondition**: unpaired main window

**Steps**

1. In *Serververbindung* enter `eagleeye-gibtsnicht` and click *Verbinden*. Start the stopwatch.
2. Wait for the result (up to 60 s).
3. Change the input to `<host>` but do **not** connect yet.

**Expected result**

- A connection error appears that names the host, e.g. **"Verbindungsfehler: Keine Verbindung zum EagleEye-Dienst unter eagleeye-gibtsnicht möglich."** (note the time in Notes; DEV expects about 15 to 21 s).
- The indicator stays **red**; the app does not show "Verbunden".
- The input field stays editable and can be corrected (step 3 works).

#### TC-002-10: Stopped service gives a connection error that names the host

- **Verifies**: AC-12
- **Machine / account**: Service PC / Admin (+ Terminal (Administrator))
- **Precondition**: `<host>` in the input field, not connected

**Steps**

1. Terminal (Administrator): `Stop-Service -DisplayName "EagleEye Service"`.
2. In the app click *Verbinden*.
3. After the error has appeared: `Start-Service -DisplayName "EagleEye Service"`. Wait 10 s.

**Expected result**

- A connection error appears that names `<host>` (e.g. "Verbindungsfehler: Keine Verbindung zum EagleEye-Dienst unter `<host>` möglich.").
- The indicator stays **red**, and *Verbinden* can be used again.

#### TC-002-11: Connecting by hostname asks for the pairing code and a device name

- **Verifies**: AC-13, AC-10 (connecting), AC-20
- **Machine / account**: Service PC / Admin
- **Precondition**: service running; Kid signed in in the background with a green tray (from TC-002-03)

**Steps**

1. In *Serververbindung*, with `<host>` entered, click *Verbinden*.
2. Look at the *Serververbindung* section and at the status bar. Do not enter anything yet.

**Expected result**

- *Serververbindung* now asks for the pairing code and a device name: status "Kopplung läuft", fields *Kopplungscode* and *Gerätename dieses PCs*, buttons *Koppeln*, *Neuen Code anfordern*, *Abbrechen*.
- The status bar is **red** with **"Verbindung zu `<host>` wird hergestellt …"**. It does **not** show "Verbunden mit `<host>`" (AC-20).
- *Note only*: what the device name field is prefilled with.

#### TC-002-12: The pairing code appears in the kid's tray client

- **Verifies**: AC-14
- **Machine / account**: Service PC / Kid (look only), then Admin
- **Precondition**: TC-002-11 done (within the last 5 minutes)

**Steps**

1. Before switching: check whether a pairing popup appeared **in your Admin session** too (the tray client also runs in admin sessions, Q-7). If it did, check that it is fully readable, as in the expected result below.
2. *Benutzer wechseln* → `eagleeye-kid`.
3. Look for the window **"EagleEye – Eltern-App koppeln"**. Write down the code. Close it with *OK*.
4. *Benutzer wechseln* → Admin.

**Expected result**

- In the Kid session a popup **"EagleEye – Eltern-App koppeln"** is shown (on top, visible without searching).
- It shows **6 digits** ("Kopplungscode: nnnnnn") and says the code is needed to pair the EagleEye parent app ("Geben Sie diesen Code in der EagleEye-Eltern-App ein.", "Der Code ist 5 Minuten gültig.").
- All texts are **fully readable**: nothing is cut off at any edge (ISSUE-004). This applies to the popup in the Kid session and, if it appears there, to the popup in the Admin session.
- *Note only*: the display scaling of each session in which you looked at the popup (*Einstellungen → System → Bildschirm → Skalierung*). Scaling is a per-account setting.

#### TC-002-13: An empty device name is not accepted

- **Verifies**: AC-17
- **Machine / account**: Service PC / Admin
- **Precondition**: pairing form from TC-002-11, code from TC-002-12

**Steps**

1. Clear the field *Gerätename dieses PCs* completely.
2. Enter the code from TC-002-12 in *Kopplungscode*, click *Koppeln*.

**Expected result**

- The app says that a device name is required (e.g. "Bitte einen Gerätenamen eingeben (höchstens 50 Zeichen).").
- The app is not paired; the indicator stays **red**.

#### TC-002-14: A wrong code is rejected, and a new attempt gets a new code

- **Verifies**: AC-18, AC-20
- **Machine / account**: Service PC / Admin (code from the tray popup in the Admin or Kid session)
- **Precondition**: TC-002-13 done

**Steps**

1. Enter the device name **Papas PC**.
2. Enter a **wrong** code: the code from TC-002-12 with the last digit changed. Click *Koppeln*.
3. Start a new attempt: click *Neuen Code anfordern* (or *Abbrechen*, then *Verbinden* again).
4. Read the new code from the tray popup (Admin session if it appears there, otherwise switch to the Kid). Do not enter it yet.

**Expected result**

- After step 2: a clear message, e.g. **"Der Kopplungscode ist falsch."** The app is **not** paired, and the indicator stays **red** (never "Verbunden").
- After step 3/4: a new popup shows a **new** 6-digit code, different from the one in TC-002-12.

#### TC-002-15: The correct code pairs the app

- **Verifies**: AC-16, AC-10 (connected), AC-8, AC-1 and AC-23 (service PC, parent's own Windows account)
- **Machine / account**: Service PC / Admin
- **Precondition**: new code from TC-002-14, less than 5 minutes old

**Steps**

1. Device name **Papas PC**, enter the new code, click *Koppeln*.
2. Look at the status bar and at *Serververbindung*.

**Expected result**

- The status bar turns **green** with **"Verbunden mit `<host>`"**.
- *Serververbindung* shows the status **Gekoppelt**, the host (`<host>`) and the device name **Papas PC** (e.g. "Gekoppelt mit: `<host>`", "Dieses Gerät: Papas PC").
- No certificate prompt appeared at any time (AC-7 rule).

#### TC-002-16: The host cannot be changed while paired

- **Verifies**: AC-25, AC-8
- **Machine / account**: Service PC / Admin
- **Precondition**: paired and connected (TC-002-15)

**Steps**

1. In *Serververbindung*, try to click into the host and type.

**Expected result**

- The host is shown **read-only**: there is no editable host field and no *Verbinden* button; typing changes nothing.
- The action **Kopplung aufheben** is offered (it is used in Block B).

### Block B — Service PC: paired operation and removing the pairing

*Service PC · Admin. Start state: end of Block A (paired, green, theme Hell, Kid signed in in the background).*

#### TC-002-17: A paired app connects by itself after a restart, theme kept

- **Verifies**: AC-21, AC-9 (kept across restart)
- **Machine / account**: Service PC / Admin
- **Precondition**: paired; service running; Windows app mode still **Dunkel**, app switch on **Hell**

**Steps**

1. Close the app (window **X**). *Task-Manager*: no `EagleEye.ParentApp.exe` left.
2. Start menu → **EagleEye Parent App**; start the stopwatch at the click.
3. Do not touch anything.

**Expected result**

- No host dialog, no code request, no other input.
- **Green "Verbunden mit `<host>`"** within **30 s** (note the time).
- The app is **light** (Hell), although Windows is in dark mode: the parent's choice wins.

#### TC-002-18: Repair install over the running app keeps pairing and theme

- **Verifies**: AC-4 (repair, same version), AC-9 (kept across reinstall)
- **Machine / account**: Service PC / Admin
- **Precondition**: app running, green

**Steps**

1. With the app still open, run `EagleEye-ParentApp-Setup-0.2.0.exe` again with default settings. Follow the wizard (it may close the running app; note how it handles that).
2. Start the app (last wizard page or Start menu).

**Expected result**

- The installer finishes **without errors**.
- The app connects **without a pairing code**: green "Verbunden mit `<host>`", device name still **Papas PC**.
- The app is still **light** (Hell).

#### TC-002-19: Stopping the service turns the app red

- **Verifies**: AC-22 (disconnect), AC-10 (not connected with host)
- **Machine / account**: Service PC / Admin (+ Terminal (Administrator))
- **Precondition**: app green

**Steps**

1. `Stop-Service -DisplayName "EagleEye Service"`; start the stopwatch. Note the clock time.
2. Watch the status bar.

**Expected result**

- Within **30 s**: **red** indicator and **"Nicht verbunden mit `<host>`"** (note the time).

#### TC-002-20: Removing the pairing is not possible while not connected

- **Verifies**: AC-30
- **Machine / account**: Service PC / Admin
- **Precondition**: service stopped, app red (TC-002-19)

**Steps**

1. *Einstellungen → Serververbindung*: look at **Kopplung aufheben** and try to click it.

**Expected result**

- *Kopplung aufheben* is not offered or cannot be used (e.g. greyed out). Nothing is removed: the status still shows the pairing.
- The app says that a connection to the service is required, e.g. **"Zum Aufheben der Kopplung ist eine Verbindung zum EagleEye-PC nötig."**

#### TC-002-21: After a service restart the app reconnects by itself

- **Verifies**: AC-22 (reconnect)
- **Machine / account**: Service PC / Admin (+ Terminal (Administrator))
- **Precondition**: service stopped for **at least 2 minutes** (clock time from TC-002-19)

**Steps**

1. `Start-Service -DisplayName "EagleEye Service"`; start the stopwatch.
2. Do not touch the app.

**Expected result**

- **Green "Verbunden mit `<host>`"** within **60 s** without any user action (note the time).
- *Kopplung aufheben* is available again.

#### TC-002-22: Removing the pairing asks for confirmation

- **Verifies**: AC-26, AC-8
- **Machine / account**: Service PC / Admin
- **Precondition**: app green

**Steps**

1. **Preparation for TC-002-25** (a copy of the current pairing data): close the app; check in *Task-Manager* that `EagleEye.ParentApp.exe` is gone; then in PowerShell:
   ```powershell
   Copy-Item "$env:LOCALAPPDATA\EagleEye" "$env:LOCALAPPDATA\EagleEye-TC25-Kopie" -Recurse
   ```
   Start the app again and wait for green.
2. *Serververbindung* → click **Kopplung aufheben**.
3. In the confirmation, choose the option that does **not** remove (e.g. *Abbrechen* / *Nein*).

**Expected result**

- Step 2: a confirmation question appears (e.g. "Kopplung aufheben?") **before** anything is removed.
- Step 3: the app stays **Gekoppelt** and **green**.

#### TC-002-23: Confirming removes the pairing

- **Verifies**: AC-27
- **Machine / account**: Service PC / Admin
- **Precondition**: app green (TC-002-22)

**Steps**

1. **Kopplung aufheben** → confirm.

**Expected result**

- The app returns to the unpaired state: *Serververbindung* shows **Nicht gekoppelt**, the host field is **editable** (empty), *Verbinden* is available.
- **Red** indicator, "Nicht verbunden".

#### TC-002-24: After a restart the unpaired app starts with the host dialog

- **Verifies**: AC-27, AC-11
- **Machine / account**: Service PC / Admin
- **Precondition**: TC-002-23 done

**Steps**

1. Close the app and start it again from the Start menu.
2. Click *Abbrechen* in the dialog. Close the app.

**Expected result**

- The dialog **"Mit dem EagleEye-PC verbinden"** appears immediately after the start (no automatic connection to the former host).

#### TC-002-25: The removed pairing is no longer accepted

- **Verifies**: AC-28, AC-20
- **Machine / account**: Service PC / Admin
- **Precondition**: app closed (TC-002-24); copy from TC-002-22 exists

**Steps**

1. *Task-Manager*: no `EagleEye.ParentApp.exe`. Then put the old pairing data back:
   ```powershell
   Remove-Item "$env:LOCALAPPDATA\EagleEye" -Recurse -Force
   Rename-Item "$env:LOCALAPPDATA\EagleEye-TC25-Kopie" "EagleEye"
   ```
2. Start the app. Watch it for **60 s**.

**Expected result**

- The app does **not** become green and never shows "Verbunden mit `<host>`": the service does not accept the old pairing.
- *Note only* (not specified by the story): what the app shows, e.g. a message that this PC is no longer paired with `<host>`, and whether it returns to *Nicht gekoppelt* by itself.
- **Cleanup for the next case**: if the app does not show *Nicht gekoppelt* with an editable host field after 60 s, close it, run `Remove-Item "$env:LOCALAPPDATA\EagleEye" -Recurse -Force`, start it again and cancel the host dialog. Theme is then back to the Windows mode; that is expected here.

#### TC-002-26: Without any tray client, the code goes to the Event Log

- **Verifies**: AC-15, AC-13; AC-11 (IPv4 is used here)
- **Machine / account**: Service PC / Admin (+ Event Viewer)
- **Precondition**: app unpaired with editable host field

**Steps**

1. Sign the Kid out: *Task-Manager → Benutzer* → `eagleeye-kid` → *Abmelden*.
2. End your own tray client: *Task-Manager → Details* → `EagleEye.TrayClient.exe` → *Task beenden*.
3. PowerShell: `Get-Process EagleEye.TrayClient -ErrorAction SilentlyContinue` → must return **nothing** (no tray in any session). If something is listed, end it and repeat.
4. In the app enter `<ip>` (the IPv4 address) and click *Verbinden*.
5. `eventvwr.msc` → *Windows-Protokolle → Anwendung* → *Aktualisieren*. Open the newest entry with source **EagleEye**. Write down the code and the time.

**Expected result**

- The app shows the pairing form (code + device name), red "Verbindung zu `<ip>` wird hergestellt …" (AC-13).
- No popup appears anywhere.
- The Event Log has a new entry: *Quelle* **EagleEye**, *Ebene* **Informationen**, containing the **6-digit pairing code** (DEV: event ID 1000, "EagleEye-Kopplungscode / pairing code: nnnnnn — gültig 5 Minuten / valid for 5 minutes.").

#### TC-002-27: An expired code is rejected

- **Verifies**: AC-19
- **Machine / account**: Service PC / Admin
- **Precondition**: TC-002-26 done; do **not** enter the code yet

**Steps**

1. Wait until **at least 5 min 30 s** have passed since the time of the Event Log entry. Leave the app as it is.
2. Device name **Papas PC**, enter the code from TC-002-26, click *Koppeln*.
3. Start a new attempt (*Neuen Code anfordern*, or *Abbrechen* and *Verbinden*). Refresh the Event Viewer.

**Expected result**

- Step 2: a clear message that the code has **expired** (e.g. "Der Kopplungscode ist abgelaufen."). Not paired, **red**.
- Step 3: a new Event Log entry with a **new** code.
- *Note only*: if the pairing form closed by itself during the wait, write what happened.

#### TC-002-28: Pairing by IP address with a new code

- **Verifies**: AC-28 (new code required), AC-16, AC-11 (IPv4 works), AC-10 (host shown as entered)
- **Machine / account**: Service PC / Admin
- **Precondition**: new code from TC-002-27, less than 5 minutes old

**Steps**

1. Enter the new code, device name **Papas PC**, click *Koppeln*.

**Expected result**

- **Green "Verbunden mit `<ip>`"**, with the IP address exactly as typed.
- *Serververbindung*: **Gekoppelt**, host `<ip>`, device **Papas PC**.

#### TC-002-29: The connection is TLS, and the app never asks about certificates

- **Verifies**: AC-7
- **Machine / account**: Service PC / Admin
- **Precondition**: none

**Steps**

1. Microsoft Edge → `https://<host>:5443/`.
2. On the warning page, click *Erweitert* (do not continue to the site). Optionally click the warning in the address bar → certificate details.
3. Think back over Blocks A and B: did the parent app ever show a certificate warning or question?

**Expected result**

- Edge shows a certificate warning (e.g. *"Ihre Verbindung ist nicht privat"*, `NET::ERR_CERT_AUTHORITY_INVALID`). This proves that port 5443 speaks TLS with the service's own certificate (issued to **EagleEye**). The browser does not trust it; that is expected.
- The parent app has **never** shown a certificate warning, question or import step.

#### TC-002-30: The service log contains no pairing codes outside the code entry *(supporting)*

- **Verifies**: — (supporting check, NFR-S-014)
- **Machine / account**: Service PC / Admin (Event Viewer)
- **Precondition**: Blocks A and B up to here done

**Steps**

1. Event Viewer → *Anwendung* → *Aktuelles Protokoll filtern…* → *Ereignisquellen*: **EagleEye** → OK.
2. Open the entries written during this test run (pairing started, code sent, pairing rejected, device paired, device removed).

**Expected result**

- Only the code entries (TC-002-26/27, event ID 1000) contain a 6-digit pairing code. All other entries mask it (e.g. `***`).
- No entry contains a long random string that looks like a token or password.

#### TC-002-31: Uninstalling removes the parent app completely

- **Verifies**: AC-5
- **Machine / account**: Service PC / Admin
- **Precondition**: app paired (TC-002-28); app **closed**

**Steps**

1. *Einstellungen → Apps → Installierte Apps* → **EagleEye Parent App** → *…* → *Deinstallieren*, confirm, finish.
2. PowerShell:
   ```powershell
   Test-Path "$env:LOCALAPPDATA\Programs\EagleEye Parent App"
   Test-Path "$env:LOCALAPPDATA\EagleEye"
   Get-ChildItem "$env:APPDATA\Microsoft\Windows\Start Menu\Programs" -Recurse -Filter "*EagleEye Parent*"
   ```
3. Start menu: search "EagleEye Parent App". *Installierte Apps*: search "EagleEye".

**Expected result**

- The uninstaller finishes without errors.
- Both `Test-Path` return **False**; the `Get-ChildItem` returns nothing.
- No Start menu entry; *Installierte Apps* lists only **EagleEye** (the service), which keeps running (`services.msc`: *Wird ausgeführt*).
- Not checked here (by design, Q-2): the service still knows the device.

#### TC-002-32: A new install starts fresh and follows the Windows app mode

- **Verifies**: AC-5 (no stored data left), AC-9 (first start), AC-11 (dialog → connect)
- **Machine / account**: Service PC / Admin
- **Precondition**: TC-002-31 done

**Steps**

1. Set the Windows app mode to **Hell** (*Einstellungen → Personalisierung → Farben*).
2. Install `EagleEye-ParentApp-Setup-0.2.0.exe` again with default settings and start the app.
3. In the host dialog enter `<host>` and click *Verbinden*.
4. Pair with device name **Papas PC**. Read the code from the Event Log (no tray client is running); or sign the Kid in again and read it from the popup.

**Expected result**

- Step 2: the host dialog appears immediately (the former pairing and settings are gone), and the app is **light** (follows Windows, the switch shows *Hell*).
- Step 3: confirming the dialog starts the connection and leads directly to the pairing form.
- Step 4: green "Verbunden mit `<host>`". This is the start state for Block C.

### Block C — Second PC (PC2)

*Needs the second Windows 11 PC in the LAN (setup S-9, S-10; Michael has one). If it cannot be used on the day, mark the cases Blocked with the reason. Pair one app at a time.*

#### TC-002-33: On a PC without .NET the app installs and starts without further steps

- **Verifies**: AC-2, AC-1, AC-9 (first start on PC2)
- **Machine / account**: PC2 / any local account
- **Precondition**: parent app never installed on PC2

**Steps**

1. PowerShell on PC2: `dotnet --list-runtimes`. *Installierte Apps*: search ".NET".
2. Run `EagleEye-ParentApp-Setup-0.2.0.exe` with default settings, finish (untick "start now" if offered).
3. Start menu → **EagleEye Parent App**.

**Expected result**

- Step 1: `dotnet` is not recognised (or lists no runtimes); no ".NET … Runtime" is installed. If PC2 has a .NET runtime, mark the case **Blocked** ("PC2 has .NET") and note which.
- Step 2: the wizard completes without errors and without asking for or downloading any other software.
- Step 3: the app starts and shows the host dialog. Its theme matches the PC2 app mode.
- *Note only*: if PC2 runs Windows in English, note whether the app texts are English.

#### TC-002-34: The service is reachable from PC2 without any configuration

- **Verifies**: AC-6
- **Machine / account**: PC2
- **Precondition**: nothing was configured on the service PC by hand (only the installer of TC-002-01 ran)

**Steps**

1. PowerShell on PC2: `Test-NetConnection <host> -Port 5443`
2. `Test-NetConnection <host> -Port 5080` *(supporting)*

**Expected result**

- Step 1: `TcpTestSucceeded : True`.
- Step 2: `TcpTestSucceeded : False` (the tray port is local only).

#### TC-002-35: TLS from PC2, no certificate prompt

- **Verifies**: AC-7
- **Machine / account**: PC2
- **Precondition**: none

**Steps**

1. Edge on PC2 → `https://<host>:5443/`.

**Expected result**

- Certificate warning page as in TC-002-29 (TLS with the EagleEye certificate). During all Block C cases the app itself never shows a certificate prompt.

#### TC-002-36: Pairing from PC2 works exactly as on the service PC

- **Verifies**: AC-23, AC-1, AC-13, AC-14, AC-16
- **Machine / account**: PC2 + service PC / Kid (look only)
- **Precondition**: host dialog open on PC2 (TC-002-33); Kid signed in on the service PC with a running tray (S-10)

**Steps**

1. On PC2 in the host dialog enter `<host>` → *Verbinden*.
2. On the service PC switch to `eagleeye-kid`, read the code in **"EagleEye – Eltern-App koppeln"**, switch back.
3. On PC2: device name **PC2**, enter the code, *Koppeln*.

**Expected result**

- Same behaviour as TC-002-11, TC-002-12 and TC-002-15: pairing form with code and device name, red "Verbindung zu `<host>` wird hergestellt …"; popup with 6 digits in the Kid session; after *Koppeln* **green "Verbunden mit `<host>`"**, *Gekoppelt*, host `<host>`, device **PC2**.
- Any difference from the service PC run goes into Observed (AC-23 requires identical behaviour).

#### TC-002-37: Two apps on two PCs are connected at the same time

- **Verifies**: AC-24
- **Machine / account**: PC2 + service PC / Admin
- **Precondition**: PC2 paired (TC-002-36); service PC app paired (TC-002-32)

**Steps**

1. Look at the status bar on PC2 and on the service PC (Admin, app open).

**Expected result**

- Both show **green "Verbunden mit `<host>`"** at the same time.

#### TC-002-38: Paired operation on PC2: auto-connect and reconnect

- **Verifies**: AC-21, AC-22, AC-23
- **Machine / account**: PC2 + service PC / Admin (+ Terminal (Administrator))
- **Precondition**: TC-002-37

**Steps**

1. On PC2: close the app, start it from the Start menu; stopwatch.
2. On the service PC: `Stop-Service -DisplayName "EagleEye Service"`; stopwatch. Watch both apps.
3. Wait 1 minute. `Start-Service -DisplayName "EagleEye Service"`; stopwatch. Watch both apps.

**Expected result**

- Step 1: PC2 green "Verbunden mit `<host>`" within **30 s**, without any input.
- Step 2: both apps **red** "Nicht verbunden mit `<host>`" within **30 s**.
- Step 3: both apps **green** again within **60 s** without user action.

#### TC-002-39: Removing the pairing on PC2 does not affect the other app

- **Verifies**: AC-29, AC-26, AC-27, AC-23
- **Machine / account**: PC2 + service PC / Admin
- **Precondition**: both apps green

**Steps**

1. On PC2: *Serververbindung* → **Kopplung aufheben** → confirm.
2. Watch the service PC app for **60 s**.

**Expected result**

- PC2: confirmation first, then *Nicht gekoppelt*, editable host field, red.
- Service PC app stays **green "Verbunden mit `<host>`"** the whole time.

#### TC-002-40: Uninstall on PC2 is clean

- **Verifies**: AC-5
- **Machine / account**: PC2
- **Precondition**: app closed

**Steps**

1. Same steps as TC-002-31, on PC2.

**Expected result**

- Same as TC-002-31: no program folder, no `%LocalAppData%\EagleEye`, no Start menu entry, no entry in *Installierte Apps*.

### Block D — Second parent app on the service PC (second Windows account)

*Second Windows account on the service PC; covers AC-29 independently of Block C. Setup S-11 done. The Admin parent app is paired and green.*

#### TC-002-41: A second parent account on the service PC can install and pair its own app

- **Verifies**: AC-1 (same PC, other Windows account)
- **Machine / account**: Service PC / Parent 2 (`eagleeye-parent2`), then Admin
- **Precondition**: Admin app green; Admin stays signed in

**Steps**

1. *Benutzer wechseln* → `eagleeye-parent2` (first sign-in creates the profile).
2. Run `C:\Users\Public\Downloads\EagleEye-ParentApp-Setup-0.2.0.exe` with default settings, start the app.
3. Host dialog: `<host>` → *Verbinden*. Read the code from the popup in this session (its own tray client), device name **Eltern 2**, *Koppeln*.
4. *Benutzer wechseln* → Admin; look at the Admin app.

**Expected result**

- Installation and pairing behave as in Block A; the app shows **green "Verbunden mit `<host>`"**, device **Eltern 2**.
- The Admin app is still **green** at the same time.

#### TC-002-42: Removing the pairing of the second app does not affect the Admin app

- **Verifies**: AC-29
- **Machine / account**: Service PC / Parent 2, then Admin
- **Precondition**: TC-002-41

**Steps**

1. *Benutzer wechseln* → `eagleeye-parent2`. *Serververbindung* → **Kopplung aufheben** → confirm.
2. *Benutzer wechseln* → Admin; watch the Admin app for **60 s**.

**Expected result**

- Parent 2 app: *Nicht gekoppelt*, red.
- Admin app stays **green "Verbunden mit `<host>`"**.

#### TC-002-43: Uninstalling while the app is running still removes everything

- **Verifies**: AC-5
- **Machine / account**: Service PC / Parent 2
- **Precondition**: TC-002-42

**Steps**

1. *Benutzer wechseln* → `eagleeye-parent2`. Start the app and **leave it open**.
2. Uninstall via *Installierte Apps* → **EagleEye Parent App** → *Deinstallieren*.
3. Run the checks of TC-002-31 step 2 and 3 in this account.

**Expected result**

- The uninstaller finishes without errors (the running app is closed).
- Same as TC-002-31: nothing of the parent app is left in this account.

## 6. Regression

None. Story test runs contain **no regression cases** (`02_Implementation/docs/testing/README.md`, TES rule 4). Regression runs only when Michael explicitly requests it before a major version release, as a separate run built from `02_Implementation/docs/testing/regression-checklist.md`.

## 7. Cleanup

After the run (keep EagleEye service 0.2.0 installed; later stories build on it):

1. **Service PC, Admin**: the parent app stays installed and paired (end of TC-002-32). Your tray client comes back at your next sign-in (it was ended in TC-002-26).
2. **Windows app mode**: set it back to your usual setting (S-6).
3. **Leftovers**: `Test-Path "$env:LOCALAPPDATA\EagleEye-TC25-Kopie"` → must be `False` (renamed in TC-002-25); delete it if it exists.
4. **Block D account**: sign out `eagleeye-parent2`, then in Terminal (Administrator):
   ```powershell
   Get-CimInstance Win32_UserProfile | Where-Object LocalPath -like "*\eagleeye-parent2" | Remove-CimInstance
   Remove-LocalUser -Name "eagleeye-parent2"
   ```
   Delete `C:\Users\Public\Downloads\EagleEye-ParentApp-Setup-0.2.0.exe`.
5. **PC2**: the app was uninstalled in TC-002-40; delete the copied installer.
6. **Kid**: sign out `eagleeye-kid`.
7. **Orphaned pairings** stay on the service (Q-2: uninstalled and replayed apps). That is expected. A full service reset (stop the service, delete `%ProgramData%\EagleEye`, start the service) also invalidates the Admin app's pairing; do it only if needed, then reinstall the parent app.

## 8. Notes for Michael and PRO

Found while writing this plan; none blocks the test run.

1. **Story text (PRO).** `user-story.md` contains a second, older *Decisions* table after the approved one (rows Q-1 to Q-5 starting with `---|---|---|`). Its Q-4 says "try the existing pairing at the new address", which contradicts AC-25 and the approved Q-4. TES tests against AC-25. PRO should delete the stale rows.
2. **Tray popup in admin sessions** (implementation plan, open point 2): decided by Michael on 2026-10-04: the tray client keeps running in admin sessions. TC-002-12 only records whether the code also appears in the Admin session.
3. **A kid can pair their own app** (open point 1): accepted risk for now (Michael, 2026-10-04), no technical protection required; not a Fail (the story allows it, Q-1). Block D shows that a standard account can install and pair a parent app. This matters as soon as a parent app can change settings.
4. **AC-4 "newer version"** can only be checked with a later parent app build (see §1).
5. **AC-20, server side:** a black-box test can show "never green" and "removed pairing refused" (TC-002-25). That the service refuses every other call from an unpaired app is only covered by DEV's unit tests.
6. **App behaviour when the service no longer knows its pairing** (TC-002-25), **whether a code stays valid after a rejected empty device name** (TC-002-13), and **what happens to the pairing form during a 5-minute wait** (TC-002-27) are not specified by the story. They are recorded as observations for PRO.
7. **Second PC** (open point 3): confirmed by Michael on 2026-10-04. Block C is executable; AC-2, AC-6 (LAN part), AC-23 (other-PC part) and AC-24 get their result there.

## 9. Change Log

| Date | Change | Reason |
|---|---|---|
| 2026-10-04 | Plan written and approved by Michael. | — |
| 2026-10-04 | S-8 and TC-002-02: the terminal must be **elevated** (*Als Administrator ausführen*, title bar "Administrator:"); §2 *Tools* points to S-8. TC-002-01: "service running" added to the expected result (until now checked only through REG-001-02). TC-002-12: "all texts fully readable" (Kid popup and, if shown, Admin popup; the Admin popup is no longer observation-only) and a scaling note added. §6: no regression in story runs; REG references removed from TC-002-01, TC-002-03 and the Kid switch; Block A time 50 → 40 min. | ISSUE-005 (run 01 used a non-elevated terminal; Michael, 2026-10-04). ISSUE-004 (clipping at 150 %). New regression policy (`02_Implementation/docs/testing/README.md`, commit 236fbbf). |

# Test Run 02: US-001 — Re-test of ISSUE-001, ISSUE-002, ISSUE-003

**Test plan**: `docs/testing/US-001/test-plan.md` (cases re-used; expected texts updated to German per ISSUE-001; AC-13 added per ISSUE-003)
**Prepared by**: TES, 2026-10-03
**Executed by**: Michael
**Execution date**: <!-- fill in -->
**Build / installer version**: `EagleEye-Setup-0.1.1.exe`
**Machine(s)**: Windows Developer Machine (German UI)

> **How to record**: as in run 01: tick exactly one box per case, and fill in **Observed** for Fail/Blocked. When done, tell TES: *"test run 02 for US-001 is done"*.
> **Scope**: everything that changed in 0.1.1. The upgrade install and publisher name (ISSUE-002), all tray texts in German (ISSUE-001), the connection error in the About dialog (ISSUE-003 / AC-13), and a short regression of the service registration. About 15 minutes; no reboot.

---

## Setup

- [x] **S-1** EagleEye **0.1.0** from run 01 is still installed. Do **not** uninstall it; this run also checks the upgrade.
- [x] **S-2** You are signed in as **Admin**. If an `eagleeye-kid` session is still open, sign it out (*Task-Manager → Benutzer → eagleeye-kid → Abmelden*).

---

### TC-001-R01: Upgrade from 0.1.0 to 0.1.1

*Regression of AC-1 / installer upgrade / ISSUE-002 · Windows / Admin*

1. Run `03_Delivery\windows\EagleEye-Setup-0.1.1.exe` with default settings (SmartScreen → *Trotzdem ausführen*).
2. Finish the wizard (leave "Start the EagleEye tray icon now" unticked).
3. Open *Einstellungen → Apps → Installierte Apps* and find EagleEye.

**Expected**:
- The wizard finishes without an error message.
- EagleEye is listed **once**, with version **0.1.1**.
- The publisher shown is **Michael Adler** (ISSUE-002).

- **Result**: [X] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

### TC-001-02 (regression): Service registration unchanged after upgrade

*AC-2, AC-3 · Windows / Admin*

1. Open `services.msc` and find **EagleEye Service**.

**Expected**: *Wird ausgeführt*, *Automatisch*, *Lokales System*. There is still only **one** EagleEye service.

- **Result**: [X] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

> Sign out of Admin and sign in as **eagleeye-kid**. The upgrade closed old tray instances; the new one starts at this logon.

### TC-001-05 (re-test): Green icon with German tooltip

*AC-5, AC-6, AC-7, ISSUE-001 · Windows / Kid · service running*

1. Hover over the EagleEye tray icon.

**Expected**: the icon is **green**, and the tooltip reads **"EagleEye — Verbunden"**.

- **Result**: [X] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

### TC-001-06 (re-test): Context menu in German

*AC-11, ISSUE-001 · Windows / Kid*

1. Right-click the tray icon.

**Expected**: the menu contains **"App Infos"**.

- **Result**: [X] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

### TC-001-07 (re-test): About dialog in German, with live version

*AC-10, AC-12, ISSUE-001 · Windows / Kid · service running*

1. Right-click → **App Infos**.
2. Read the dialog and click **OK**.

**Expected**:
- Title **"EagleEye – App Infos"**
- Text **"Server-Version: EagleEye_v0.1"**
- Button **OK** closes the dialog.

- **Result**: [X] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

> Open the admin `services.msc` from the kid session as in run 01: right-click Start → *Terminal (Administrator)* → admin credentials → `services.msc`.

### TC-001-08 (re-test): Red icon with German tooltip

*AC-8, ISSUE-001 · Windows / Kid (+ admin services console)*

1. Stop **EagleEye Service**. Watch the icon for up to 30 s, then hover over it.

**Expected**: the icon turns **red**, and the tooltip reads **"EagleEye — Verbindungsfehler"**.

- **Result**: [X] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

### TC-001-09 (re-test): About while disconnected shows the connection error

*AC-13 (new), ISSUE-001, ISSUE-003 · Windows / Kid · service stopped*

1. Right-click → **App Infos**, read the dialog, and click **OK**.

**Expected**:
- Title **"EagleEye – App Infos"**
- Text **"Verbindungsfehler: Keine Verbindung zum Server unter localhost:5080 möglich."**. It names the server address and is fully readable, not cut off.

- **Result**: [X] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

### TC-001-10 (re-test): Green again after restart, German tooltip

*AC-9, ISSUE-001 · Windows / Kid (+ admin services console)*

1. Start **EagleEye Service**. Watch the icon for up to 60 s without touching it, then hover over it.

**Expected**: the icon turns **green**, and the tooltip reads **"EagleEye — Verbunden"**.

- **Result**: [X] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

---

### TC-001-R02 (optional): English texts on an English system

*NFR-L-011 · Windows / Kid · skip if no English display language is installed*

Only if you want to check the English side by hand (unit tests already cover it):

1. As **eagleeye-kid**: *Einstellungen → Zeit und Sprache → Sprache und Region → Windows-Anzeigesprache* → **English**. Sign out and back in.
2. Hover over the icon and open **About**.
3. Switch the display language back to **Deutsch** afterwards.

**Expected**: the tooltip reads **"EagleEye — Connected"**, the menu entry is **"About"**, and the dialog shows **"About EagleEye"** and **"Server Version: EagleEye_v0.1"**. With the service stopped, it shows **"Connection error: could not connect to the server at localhost:5080."**

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [X] Skipped
- **Observed**:
- **Notes**:

---

## General Feedback

<!-- anything else --> ALL GOOD!
---

## Summary (filled in by TES after evaluation)

| Pass | Fail | Blocked | Skipped | Not executed |
|---|---|---|---|---|
| 8 | 0 | 0 | 1 | 0 |

Evaluated by TES on 2026-10-03. TC-001-R02 (optional English check) skipped; English texts are covered by unit tests. ISSUE-001, ISSUE-002 and ISSUE-003 verified. Michael: "ALL GOOD!"

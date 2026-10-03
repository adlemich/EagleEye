# EagleEye — Regression Checklist

*Owner: TES. Cumulative: one section per closed user story.*

TES copies the relevant sections into every new test run (`docs/testing/US-XXX/test-run-NN.md`). Cases are added here only when a story is closed (`Verified/Closed`). Keep each section short. It holds the cases that prove the story still works, not the full test plan.

UI texts below are the German texts (Windows display language German); on an English system the English equivalents apply.

---

## US-001 — Basic Service Installation and Tray Client Connectivity

*Closed 2026-10-03 (build 0.1.1). Full cases: `docs/testing/US-001/test-run-01.md`, `test-run-02.md`. About 10 minutes.*

### REG-001-01: Install or upgrade to the build under test

*Windows / Admin*

1. Run the installer of the build under test with default settings (over the existing installation).
2. *Einstellungen → Apps → Installierte Apps* → EagleEye.

**Expected**: no error; EagleEye is listed once, with the new version and publisher **Michael Adler**.

### REG-001-02: Service registration

*Windows / Admin*

1. `services.msc` → **EagleEye Service**.

**Expected**: *Wird ausgeführt*, *Automatisch*, *Lokales System*.

### REG-001-03: Tray auto-start and connected state

*Windows / Kid (`eagleeye-kid`)*

1. Sign in as the kid and hover over the EagleEye tray icon.

**Expected**: the icon is present without a manual start, it is **green**, and the tooltip reads **"EagleEye — Verbunden"**.

### REG-001-04: App Infos with live version

*Windows / Kid · service running*

1. Right-click the icon → **App Infos**.

**Expected**: dialog **"EagleEye – App Infos"** showing **"Server-Version: EagleEye_vX.Y"**, which matches the installer version's MAJOR.MINOR.

### REG-001-05: Disconnect, connection error, reconnect

*Windows / Kid (+ admin `services.msc` via Terminal (Administrator))*

1. Stop **EagleEye Service**. Within 30 s: the icon is **red**, and the tooltip reads **"EagleEye — Verbindungsfehler"**.
2. **App Infos** shows **"Verbindungsfehler: Keine Verbindung zum Server unter localhost:5080 möglich."**
3. Start **EagleEye Service**. Within 60 s, without touching anything: the icon is **green** again.

**Expected**: all three observations as stated.

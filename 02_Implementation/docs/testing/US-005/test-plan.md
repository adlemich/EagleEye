# Test Plan: US-005 — Break Times (Ruhezeiten)

**Status**: Draft, for approval by Michael
**Date**: 2026-10-11
**Author**: TES
**User Story**: `02_Implementation/docs/requirements/user-stories/US-005/user-story.md` (37 ACs, approved 2026-10-10, OQ-1 to OQ-18 answered)
**Inputs used**: the user story; `02_Implementation/docs/requirements/user-stories/US-005/implementation-report.md` (§6 risks, §7 checks A to E, §8 "How to test": installers and hashes, UI texts, log texts, database tables; deviations D-1 to D-13, used only for setup, texts and artifact locations, not for expected behaviour); `02_Implementation/docs/requirements/user-stories/US-005/implementation-plan.md` ("Manual Verification Notes", Q-8, D-1 to D-15); `02_Implementation/docs/testing/US-004/test-plan.md`, `test-run-01.md` and `test-report.md` (setup, lessons); `02_Implementation/docs/testing/README.md` §4. Black box: no source code was read.

> **Lessons from US-004 applied.** You executed the core and skipped optional and late cases, left Notes blanks and header fields empty, and ticked the result boxes. So: each case is short, the core comes first, and **the result box is what counts**. Notes blanks are there for timings that help the evaluation; leave them empty if you like.

---

## 1. Scope

**In scope** (only the new US-005 features):

- Upgrade of the service and both parent apps to 0.5.0 (supporting)
- The **Regeln** page: layout, states, adding, editing in place, validation, deleting, the display text with emojis, persistence, two parent apps at once (AC-1 to AC-19)
- Rules per account, untick and deletion of accounts (AC-20)
- Enforcement at app start: blocking, graceful close and forced termination with timing, exemptions, no usage for blocked starts, rule changes taking effect, no parent app needed (AC-21 to AC-29)
- The kid's message dialog (AC-30 to AC-34)
- History of blocked starts, time-zone findings (AC-35 to AC-37), via the log; the database only optionally
- Security and robustness checks without an AC: the agent token (US-004 TC-004-23 rerun, plan Q-8), graceful close under the real SYSTEM token (Q-8), termination across sessions (report §7 B), the kid cannot end the enforcement, tray identity (report §7 C)

**Out of scope**: ending apps that run when a break begins and warnings before a break (next story); showing blocked starts or findings in the parent app; Android/iOS/macOS parent apps; protection against a kid pairing its own parent app (OQ-15).

**Not tested in this run, with reason**

| Item | Reason |
|---|---|
| Regression, US-004 re-test | Story runs contain no regression cases (`02_Implementation/docs/testing/README.md` §3, your rule). |
| English UI texts | Your Windows is German. English texts are covered by DEV's unit tests. |
| Waiting 90 days (AC-36) | Not possible. Unit tests cover the purge. |
| Exclusive full-screen games (AC-30) | Known limit stated in the story; the dialog may only appear after the game is closed. Not a Pass criterion. |
| Log flooding limit (plan Q-4: 30 blocked-start lines per account and app per hour) | Needs a script with dozens of blocked starts; unit tests cover it. |
| Daylight saving time switch, time-zone change while the service is stopped (plan D-15) | Cannot be produced in a run / not reported by design. |

## 2. Test Environment

| Item | Value |
|---|---|
| Service PC | **Windows Developer Machine** (Windows 11 Pro, German UI, `ZOCK-O-MAT-V3`). `<host>` = its computer name. Current state: EagleEye **0.4.0** (or 0.4.1) service running, App A paired. |
| Parent PC | **LEOSERV**, parent app **App B**. Needed to change rules **while the kid's session is in front** (Blocks B and D) and for two parent apps at once (TC-005-23). Without LEOSERV, TC-005-23 is **Blocked**; elsewhere use App A and switch users (switching takes longer than 5 s, so the "within 5 s" checks are then only weak). |
| Parent apps | **App A** = parent app in your admin session on the service PC (device *Papas PC*). **App B** = parent app on LEOSERV (device *LEOSERV*). |
| Accounts on the service PC (as in US-004) | **Admin** = you. **kid1** = `eagleeye-kid` (standard, **controlled**). **kid2** = `ee-kid2` (standard, **not controlled**). **kid3** = `ee-kid3` (standard, **controlled**, deleted in TC-005-30). *Careful: the story's test setup names them differently* (story "kid2, controlled" = our `ee-kid3`; story "kid3, not controlled" = our `ee-kid2`). This plan always uses the user names. |
| Builds under test | `03_Delivery/windows/EagleEye-Setup-0.5.0.exe` (service + tray, admin; built 2026-10-10 22:47, SHA-256 `83bf5c55f924f46137d4a006692bd8e4e5fd1f449c0fb8912c5fe43924151830`) and `03_Delivery/windows/EagleEye-ParentApp-Setup-0.5.0.exe` (parent app, per user; built 2026-10-10 22:48, SHA-256 `a03245d558bbce88be72f5a203a19a08608744b5c3144eb2534c12644cb217a1`). TES checked both hashes against the files on 2026-10-11. |
| Expected versions | *Installierte Apps*: **EagleEye 0.5.0**, **EagleEye Parent App 0.5.0**. Tray *App Infos*: `EagleEye_v0.5`. |
| Tools | Phone stopwatch **with lap times**. **Terminal (Administrator)** in the admin session. **K-TERM** = elevated terminal inside the kid's session (with your admin credentials, as in US-004). **P-KID** = a **normal** (not elevated) PowerShell of the kid, opened **before** any break (see below). Optional: a SQLite tool (TC-005-34). |

### Test programs

| Program | Start (Windows+R) | Used for |
|---|---|---|
| **Editor** (Notepad), no text | `notepad` | Graceful close (the main test app) |
| **Paint** | `mspaint` | Second app; **with an unsaved drawing** it asks "save?" and must be terminated by force (TC-005-16) |
| **Zeichentabelle** (Character Map) | `charmap` | App with no usage today (AC-24) and boundary checks |
| **Microsoft Edge** | (taskbar) | App that runs **before** the break and keeps running (AC-25), maximised / full-screen behind the dialog |
| **Rechner** (Calculator, Store app) | `calc` | Optional (TC-005-32) |

**Why the force case is built differently.** A freshly started app is closed about 1 to 2 s after its start, before anything can be typed into it, so "Notepad with typed text" cannot be produced by a normal start. TC-005-16 therefore starts **Paint while the service is stopped**, draws a line, and then starts the service: the service finds Paint (first process created inside the break), asks it to close, Paint shows its "save?" prompt, and the service must terminate it by force 20 s later. This is the story's own case "apps started in a break while the agent was not running are caught afterwards" (AC-22 note). Nothing of your data is involved (an untitled drawing in the kid's account).

### Making a break "in effect now" (used throughout)

- **E1** = break entry of `eagleeye-kid` that covers the rest of the day. Create it in App B (or App A): **Regeln** → **Konto** `eagleeye-kid` → **Eintrag hinzufügen** (new row: An/Aus ☐, 20:00, 23:59, all days ticked) → in **Start-Zeit** type the **current time minus one minute** (e.g. `14:09` at 14:10) → **Enter**. Leave **End-Zeit** 23:59 and all days ticked.
- **BREAK ON** = tick **An/Aus** of E1. **BREAK OFF** = untick it. Wait until the checkbox stays as you set it and no message appears (= confirmed by the service), then wait **5 s** more before the kid starts anything (AC-26).
- Do not start Blocks B to E after about **23:00** (E1 ends at midnight).
- **Important**: during a break the kid cannot start Terminal, Task-Manager or Settings either (they are apps). Open **P-KID** and **K-TERM** while the break is **off**; programs started before the break keep running (AC-25).

### Test blocks — core first, optional last

| Block | Cases | Machine / accounts | Core / optional | Duration |
|---|---|---|---|---|
| Setup | S-1 to S-13 | Service PC, LEOSERV | — | ≈ 30 min (+ 15 min if `ee-kid2`/`ee-kid3` must be created) |
| **A** Upgrade and the Regeln page | TC-005-01 to -10 (10) | Service PC / Admin | core | ≈ 40 min |
| **B** Blocking at start and the kid's dialog | TC-005-11 to -17 (7) | Service PC / kid1; LEOSERV | core | ≈ 45 min |
| **C** Other accounts and exemptions | TC-005-18 to -20 (3) | Service PC / kid1, ee-kid3, ee-kid2, Admin | core | ≈ 30 min |
| **D** Boundaries, tray, two parent apps, re-install | TC-005-21 to -25 (5) | Service PC / kid1, Admin; LEOSERV | core | ≈ 45 min |
| **E** Security | TC-005-26 to -28 core, -29 optional (4) | Service PC / kid1, Admin | core (security) + optional | ≈ 25 min core, + 10 min optional |
| **F** Account deletion and optional checks | TC-005-30 core, -31 to -35 optional (6) | Service PC / Admin, kid1 | core + optional | ≈ 10 min core, + ≈ 60 min optional |

35 cases: 29 core, 6 optional. Run the blocks in this order; breaks between blocks are fine. If you stop after Block C, the five core areas are covered: blocking at start, graceful/force with timing, the kid's dialog, the Regeln page (edit, validate, persist), per-account isolation.

**Optional coverage**: no AC rests **only** on optional cases. Optional cases add: Store app and Edge with all its processes (AC-22), the database view of the history and findings (AC-35, AC-37), reboot (AC-17, AC-27), an app started from another app (AC-22), the unverified tray copy (security).

### Conventions for expected results

- **UI texts** are German. Pass criteria are the elements and their order; the exact texts are those of the implementation report §8 (below). A wording difference is a **Note**, not a Fail. **Exception**: the **display text** in the kid's dialog must be exactly the stored text (same lines, same emojis); a difference there is a **Fail** (AC-31).

  | Where | Expected (German) |
  |---|---|
  | Menu | **Einstellungen · Regeln · Berichte** |
  | Page | title **Regeln**, **Konto**, heading bar **Ruhezeiten**, button **Eintrag hinzufügen**, label **Anzeige Text:** |
  | Columns | (trash icon) · **An/Aus** · **Start-Zeit** · **End-Zeit** · **MO DI MI DO FR SA SO** |
  | No entries | **Keine Ruhezeiten festgelegt.** |
  | Messages (one line below the table) | **Bitte eine Uhrzeit zwischen 00:00 und 23:59 eingeben.** · **Die End-Zeit muss nach der Start-Zeit liegen.** · **Mindestens ein Tag muss ausgewählt sein.** · **Die Änderung konnte nicht gespeichert werden. Bitte erneut versuchen.** |
  | States | **Keine Daten verfügbar** · **Keine Konten unter Elternkontrolle. Konten unter Einstellungen auswählen.** · **Wird geladen …** |
  | Kid's dialog | title **EagleEye**, the display text, button **OK** |
  | Default display text | "Hi! Leider haben Deine Eltern eine PC-Pause für diese Uhrzeit eingestellt. Du kannst dieses Programm jetzt nicht verwenden. Tut mir leid. Wie wäre es wenn Du die Zeit nutzt, um ein Buch zu lesen? 😊" |

- **Accepted time inputs** (ARC's choice, AC-9): `7`, `07`, `7:30`, `07:30`, `730`, `0730`, `20.00`; always shown as HH:MM.
- **Timing** (story limits; what the plan expects in brackets):
  - Dialog appears ≤ **10 s** after the start (expect 1 to 3 s).
  - App gone ≤ **30 s** after the start (graceful: expect ≤ 5 s).
  - Force: the app is terminated **20 s** after the close request (the moment the "save?" prompt appears); tolerance ±2 s for your reaction.
  - Rule change effective ≤ **5 s** after the parent app shows it; App B follows App A ≤ **5 s**; log entry of a change ≤ 5 s.
  - Account changes and purges ≤ **60 s**. Time-zone change effective ≤ **5 s** (wait 10 s before checking).
- **How to measure** the 10 s / 30 s: stopwatch **start** when you press **Enter** in *Ausführen* (Windows+R); **lap 1** when the EagleEye dialog appears; **lap 2** when the app's window is gone. A reading up to 1 s over the limit is a Note (reaction time), not a Fail; then check the log timestamps. The log line "Blocked start ended: … after N s" gives the close part exactly.
- **Seconds in the clock**: for the boundary case (TC-005-21), kid1's taskbar clock shows seconds (P-3).
- **Unexpected behaviour** that the story does not specify is a Note; TES routes it to PRO.

### Service log files as evidence

The service log is in the **admin-only** folder `%ProgramData%\EagleEye\logs\`. All log commands need an elevated terminal (Terminal (Administrator) or K-TERM).

- **LOG-COPY** at the end of the run (or a block) → `evidence/run-01-service-logs/`; **right after any Fail** → `evidence/run-01-service-logs/after-TC-005-NN/`. Look through the copies before committing (user names, program paths, SIDs; no secrets).
- **No log file**: *Ereignisanzeige → Windows-Protokolle → Anwendung*, source **EagleEye**; mark the case Fail.

**LOG-COPY** (elevated, in the repo root):

```powershell
Set-Location <your EagleEye repo folder>
$dst = Join-Path "02_Implementation\docs\testing\US-005\evidence" "<subfolder>"
New-Item -ItemType Directory -Force $dst | Out-Null
Copy-Item "$env:ProgramData\EagleEye\logs\*.log" $dst -Force
Get-ChildItem $dst
```

**LOG-WATCH** (after every service restart, re-install or reboot: *Strg+C* and run again):

```powershell
$log = Get-ChildItem "$env:ProgramData\EagleEye\logs\EagleEye.Service-*.log" | Sort-Object LastWriteTime | Select-Object -Last 1
Get-Content $log.FullName -Tail 15 -Wait
```

**LOG-FIND** (replace `<pattern>`):

```powershell
Select-String -Path "$env:ProgramData\EagleEye\logs\*.log" -Pattern "<pattern>" | Select-Object -Last 15 | ForEach-Object Line
```

Expected log lines (implementation report §8; wording differences are Notes, the content named in the ACs is the Pass criterion):

```text
Break times loaded: 1 entries (0 on) of 1 accounts, 1 changed display texts.
Break times of account eagleeye-kid (S-1-5-21-…) changed by Papas PC: entry 1 added (off, 20:00–23:59, Mo Tu We Th Fr Sa Su) (request …, revision 2).
   … entry 1 changed: on, 14:09–23:59, Mo Tu We Th Fr Sa Su (…) · entry 1 deleted (…) · display text changed (…) · display text reset to the default (…)
Break time of account eagleeye-kid began: 14:09–23:59 (Mo Tu We Th Fr Sa Su).   /   … ended: …
Blocked start: account eagleeye-kid, Editor (notepad.exe, <path>), process 5120, break time 14:09–23:59 (Mo Tu We Th Fr Sa Su); closing the app.
Blocked start ended: account eagleeye-kid, Editor (notepad.exe, …), break time 14:09–23:59 (…): closed gracefully after 0.3 s; message shown.
   outcomes: closed gracefully after N s · terminated by force after 20 s (N s until gone) · terminated by force (service stopping)
   message:  shown · not shown (a message is already open) · not shown (tray client not connected) · not shown (tray client did not answer)
Tray client connected: <id> (session 2, verified).   /   … (unverified).
Warning: The time zone of the PC changed from W. Europe Standard Time (UTC+01:00) to … ; break times now use the new local time. Session in use: eagleeye-kid.
Purged the break times of deleted account … · Purged enforcement records of deleted accounts: …
```

Must **not** appear in this run: `Warning: Process … could not be terminated`, `Error: The blocked app … could not be ended`.

## 3. Setup Instructions

**Before Block A** (service PC, Admin):

1. **S-1 Installers present.** PowerShell in the repo root: `Get-FileHash 03_Delivery\windows\EagleEye-Setup-0.5.0.exe, 03_Delivery\windows\EagleEye-ParentApp-Setup-0.5.0.exe | Format-List Path, Hash`. Hashes as in §2.
2. **S-2 Installed state.** *Installierte Apps* → "EagleEye": expected **EagleEye 0.4.0** (or 0.4.1) and **EagleEye Parent App 0.4.0** (or 0.4.1); `services.msc` → **EagleEye Service** *Wird ausgeführt*. Already 0.5.0, or no service → stop and tell TES.
3. **S-3 App A green**, *Gekoppelt*, device **Papas PC** (otherwise pair; code from the tray popup in the admin session).
4. **S-4 Accounts.** Terminal (Administrator): `Get-LocalUser | Format-Table Name, Enabled -AutoSize`. `eagleeye-kid` must exist (you know its password). If `ee-kid2` or `ee-kid3` is missing (US-004 cleanup removed them), create it:
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
   Then sign each new account in once (*Benutzer wechseln*), wait for the desktop, set scaling 150 %, **sign out**. Also sign out `eagleeye-kid` if it is signed in.
5. **S-5 Display scaling** of the admin account = **150 %**.
6. **S-6 Terminal (Administrator)**: Start → **Terminal** → right-click → **Als Administrator ausführen**. Title starts with "Administrator:". Service commands: `Stop-Service` / `Start-Service` / `Restart-Service -DisplayName "EagleEye Service"`.
7. **S-7 Log monitor**: a second Terminal (Administrator) with LOG-WATCH.
8. **S-8** `hostname` → `<host>`.
9. **S-9 Selection.** App A → *Einstellungen*: **eagleeye-kid** and **ee-kid3** ticked; **ee-kid2** and every other account **unticked**.
10. **S-10 SmartScreen** on the unsigned installers: *Weitere Informationen* → *Trotzdem ausführen* (expected).
11. **S-11 K-TERM** (as in US-004): in the kid's session, Start → **Terminal** → right-click → **Als Administrator ausführen** → enter **your admin** user name and password. It runs as you, so it is neither recorded nor blocked. Open it while the break is **off**. Close it before you sign the kid out.
12. **S-12 App B on LEOSERV** (can be done now or before Block B): after TC-005-02, install `EagleEye-ParentApp-Setup-0.5.0.exe` on LEOSERV over the old version, start it; green and *Gekoppelt* with `<host>` → done; otherwise pair as **LEOSERV**. Not available today → note it; TC-005-23 is then Blocked.
13. **S-13 SQLite tool** (only for optional TC-005-34): `sqlite3.exe` on the PATH, or *DB Browser for SQLite*. None → skip TC-005-34.

**Before Block B** (prepare kid1's session; ≈ 10 min):

- **P-1** App B (LEOSERV) → **Regeln** → **Konto** `eagleeye-kid`. Create **E1** (§2), An/Aus **unticked**. Without LEOSERV: do it in App A.
- **P-2** *Benutzer wechseln* → `eagleeye-kid`.
- **P-3** Seconds in the clock: *Einstellungen → Personalisierung → Taskleiste → Verhalten der Taskleiste* → **Sekunden in der Uhr der Taskleiste anzeigen** ✓.
- **P-4** **P-KID**: Start → **Terminal** (normal, not as administrator). Keep it open for the whole run.
- **P-5** **K-TERM** (S-11) with LOG-WATCH.
- **P-6** P-KID: `Get-Process notepad, mspaint, charmap -ErrorAction SilentlyContinue` → nothing (close them otherwise).
- **P-7** Start **Microsoft Edge**, open any page (a video is fine), **maximise** it. It stays open (it runs from before the break).

## 4. Acceptance Criteria Coverage

Cases in *italics* are optional.

| AC | Test case(s) |
|---|---|
| AC-1 | TC-005-02, TC-005-09 |
| AC-2 | TC-005-02 |
| AC-3 | TC-005-02, TC-005-03, TC-005-04 |
| AC-4 | TC-005-10 |
| AC-5 | TC-005-08, TC-005-09 |
| AC-6 | TC-005-09 |
| AC-7 | TC-005-03 |
| AC-8 | TC-005-04 |
| AC-9 | TC-005-05 |
| AC-10 | TC-005-05 |
| AC-11 | TC-005-05 |
| AC-12 | TC-005-03, TC-005-04, TC-005-06, TC-005-07 |
| AC-13 | TC-005-06 |
| AC-14 | TC-005-04, TC-005-11, TC-005-17 |
| AC-15 | TC-005-02, TC-005-07, TC-005-08 |
| AC-16 | TC-005-07, TC-005-13, TC-005-24 |
| AC-17 | TC-005-09 (app and service restart), TC-005-25 (re-install); *TC-005-35* (reboot) |
| AC-18 | TC-005-23 (partly: the "deleted meanwhile" path) |
| AC-19 | TC-005-23 |
| AC-20 | TC-005-08, TC-005-18, TC-005-19, TC-005-30 |
| AC-21 | TC-005-12, TC-005-21 |
| AC-22 | TC-005-12 (graceful, timing), TC-005-14, TC-005-16 (force); *TC-005-31*, *TC-005-32*, *TC-005-33* |
| AC-23 | TC-005-18, TC-005-20, TC-005-22 (tray) |
| AC-24 | TC-005-11, TC-005-15 |
| AC-25 | TC-005-15, TC-005-16, TC-005-21 |
| AC-26 | TC-005-12, TC-005-17 |
| AC-27 | TC-005-16 (service restart), TC-005-25 (no parent app); *TC-005-35* (reboot) |
| AC-28 | TC-005-12, TC-005-14, TC-005-16, TC-005-22 |
| AC-29 | TC-005-11 (observation) |
| AC-30 | TC-005-12, TC-005-13, TC-005-20 |
| AC-31 | TC-005-13, TC-005-19, TC-005-24 |
| AC-32 | TC-005-14 |
| AC-33 | TC-005-22 |
| AC-34 | TC-005-18 |
| AC-35 | TC-005-12, TC-005-16 (log carries the same data); *TC-005-34* (database) |
| AC-36 | TC-005-15 (no usage), TC-005-30 (purge with the account); *TC-005-34* |
| AC-37 | TC-005-28; *TC-005-34* (stored finding) |

All 37 ACs have at least one core case. Security and robustness checks without an AC: TC-005-12 (graceful close under the SYSTEM token, Q-8), TC-005-16 (termination across sessions, report §7 B), TC-005-22 (tray identity "verified", report §7 C), TC-005-26 (US-004 TC-004-23 rerun, Q-8), TC-005-27 (kid cannot end the enforcement), *TC-005-29* (unverified tray copy).

**Only partly testable by hand**: AC-36 (90 days: unit tests), AC-30 (exclusive full-screen games: known limit; focus is best effort, Note only), AC-31 (title and button in an English Windows: not available), AC-18 (a connection loss at the exact moment of a change is not produced; the same message is checked through AC-19's "deleted meanwhile"), AC-19 ("without polling" is not observable), AC-29 (subjective), AC-22 step 4 for the **force** path (the 30 s from the start cannot be measured with a "save?" prompt, see "Why the force case is built differently"; it follows from TC-005-12's detection time + TC-005-16's 20 s; *TC-005-32* may measure it directly).

## 5. Test Cases

### Block A — Upgrade and the Regeln page (core)

*Service PC · Admin. Start state: setup done; App A (0.4.x) green; all kid accounts signed out.*

#### TC-005-01: Service update 0.4.x → 0.5.0 keeps pairing and selection *(supporting)*

- **Verifies**: — (supporting: upgrade path)
- **Machine / account**: Service PC / Admin (+ Terminal (Administrator))
- **Precondition**: S-1 to S-11; App A open and green

**Steps**

1. Run `03_Delivery\windows\EagleEye-Setup-0.5.0.exe` with defaults. Leave App A open.
2. Tray → *App Infos* (start **EagleEye Tray** if no icon). *Installierte Apps*; `services.msc`.
3. Watch App A up to 60 s. Log monitor: *Strg+C*, LOG-WATCH; LOG-FIND `Break times loaded`.

**Expected result**

- No error; *App Infos* **EagleEye_v0.5**; **EagleEye 0.5.0** once; service *Wird ausgeführt*.
- App A (still 0.4.x) green again within 60 s without a pairing code.
- Log: "Break times loaded: 0 entries (0 on) of … accounts, 0 changed display texts." No Error lines.

#### TC-005-02: Parent app 0.5.0: menu, start page and the empty Regeln page

- **Verifies**: AC-1, AC-2, AC-3 (empty), AC-15 (default text)
- **Machine / account**: Service PC / Admin
- **Precondition**: TC-005-01

**Steps**

1. Close App A. Run `03_Delivery\windows\EagleEye-ParentApp-Setup-0.5.0.exe` with defaults; start the app. *Installierte Apps*: version.
2. Note which page the app opens on. Look at the menu. Click **Regeln**.
3. Open **Konto**, read the entries; close it without change. Read the page from top to bottom.

**Expected result**

- **EagleEye Parent App 0.5.0**, green without a code; it opens on **Berichte**.
- Menu order: **Einstellungen**, **Regeln**, **Berichte**.
- Page from top to bottom: title **Regeln** (same style as the other page titles); **Konto** listing exactly **eagleeye-kid** and **ee-kid3** (as on *Berichte*), the **first** selected; heading bar **Ruhezeiten** (accent colour, like the bars on *Berichte*); button **Eintrag hinzufügen**; the text **Keine Ruhezeiten festgelegt.** instead of rows; **Anzeige Text:** with a multi-line box showing the **default text** (§2) with line wrap.

#### TC-005-03: "Eintrag hinzufügen" adds a row with the defaults, saved at once

- **Verifies**: AC-7, AC-12, AC-3 (columns, HH:MM, order)
- **Machine / account**: Service PC / Admin; log monitor
- **Precondition**: Regeln → **Konto** `eagleeye-kid`, no entries

**Steps**

1. Click **Eintrag hinzufügen**. Watch the log monitor.
2. Click **Eintrag hinzufügen** a second time.

**Expected result**

- Step 1: one row: trash icon, **An/Aus ☐**, **20:00**, **23:59**, **MO DI MI DO FR SA SO all ☑**. Column headers: (none above the trash icon), **An/Aus**, **Start-Zeit**, **End-Zeit**, **MO … SO**. Within 5 s the log: "Break times of account eagleeye-kid (S-1-5-21-…) changed by Papas PC: entry N added (off, 20:00–23:59, Mo Tu We Th Fr Sa Su) …".
- Step 2: the second row appears **below** the first; the first keeps its place.

#### TC-005-04: Editing in place: checkboxes at the click, times on Enter or leaving the field

- **Verifies**: AC-8, AC-12, AC-14 (an entry that is off stays editable), AC-3 (HH:MM)
- **Machine / account**: Service PC / Admin; log monitor
- **Precondition**: TC-005-03 (two rows). Row 1 = upper row.

**Steps**

1. Row 1: tick **An/Aus**. Then untick it again.
2. Row 1: **Start-Zeit** → select all, type `2130`, press **Tab** (leave the field).
3. Row 1: **End-Zeit** → type `22.15`, press **Enter**.
4. Row 1: untick **SA** and **SO**.
5. Watch the log monitor after each step. No "Save" button exists anywhere.

**Expected result**

- Each step is saved by itself: one log line per change within 5 s, e.g. "entry N changed: on, 20:00–23:59, Mo Tu We Th Fr Sa Su", then "… off, …", "… off, 21:30–23:59, …", "… off, 21:30–22:15, …", "… off, 21:30–22:15, Mo Tu We Th Fr Sa", "… Mo Tu We Th Fr".
- The fields show **21:30** and **22:15** (HH:MM), although you typed `2130` and `22.15`.
- Row 1 is off, keeps all its values and can be edited while off.

#### TC-005-05: Invalid times, end not after start, last weekday: nothing saved, message shown

- **Verifies**: AC-9, AC-10, AC-11
- **Machine / account**: Service PC / Admin; log monitor
- **Precondition**: row 1 = off, 21:30–22:15, MO–FR (TC-005-04)

**Steps** (after each step: look at the field, the message line below the table, the log monitor)

1. Row 1 **Start-Zeit**: type `24:00`, Enter. Then `12:60`, Enter. Then `abc`, Enter. Then delete the content (empty), Tab.
2. Row 1 **End-Zeit**: type `21:00`, Enter. Then `21:30`, Enter.
3. Row 1 **Start-Zeit**: type `23:00`, Enter.
4. Row 1: untick **MO**, **DI**, **MI**, **DO** one after the other, then try to untick **FR** (the last one).
5. Make one valid change (tick **MO** again).

**Expected result**

- Step 1: each time the field returns to **21:30**, the message **Bitte eine Uhrzeit zwischen 00:00 und 23:59 eingeben.** appears, and **no** log line.
- Steps 2 and 3: the field returns to the stored value (22:15 / 21:30); message **Die End-Zeit muss nach der Start-Zeit liegen.**; no log line.
- Step 4: MO to DO are saved (four log lines); **FR stays ticked**, message **Mindestens ein Tag muss ausgewählt sein.**, no log line for it.
- Step 5: saved; the message disappears.

#### TC-005-06: The trash icon deletes at once, without confirmation

- **Verifies**: AC-13, AC-12
- **Machine / account**: Service PC / Admin; log monitor
- **Precondition**: two rows (TC-005-03)

**Steps**

1. Click the trash icon of **row 2**.
2. *Optional (≈ 3 min)*: click **Eintrag hinzufügen** until there are **20** rows; look at the button; then delete rows until only row 1 is left.

**Expected result**

- Step 1: row 2 disappears at once; **no** confirmation dialog; log "entry N deleted" within 5 s. Row 1 unchanged.
- Step 2 (optional): at 20 rows **Eintrag hinzufügen** is disabled; after a delete it is enabled again.

#### TC-005-07: Display text: edit, line break, emojis, empty text

- **Verifies**: AC-15, AC-16, AC-12
- **Machine / account**: Service PC / Admin; log monitor
- **Precondition**: **Konto** `eagleeye-kid`

**Steps**

1. Click into the display text box, select all, type `Pause! Test 1 ` and insert **😊** with the emoji panel (**Windows + .**). Press **Enter** (new line), type `Lies ein Buch ` and paste **📚 👍** (copy them from this document). Then click somewhere outside the box.
2. Click **Berichte**, then **Regeln** again.
3. Click into the box, delete the whole text, click outside the box.
4. Enter the text of step 1 again (two lines, 😊 📚 👍); click outside. It stays for the rest of the run.
5. *Optional*: paste a long text (> 500 characters) into the box.

**Expected result**

- Step 1: the box shows two lines with the emojis (in colour); log "display text changed" within 5 s.
- Step 2: the same two lines and emojis are shown again.
- Step 3: the box shows the **default text** again; log "display text reset to the default".
- Step 5 (optional): the box stops accepting characters at about 500 (an emoji counts 2 or more).

#### TC-005-08: Each account has its own entries and text; switching the account switches both

- **Verifies**: AC-5, AC-20 (per account), AC-15 (saved before the account changes)
- **Machine / account**: Service PC / Admin; log monitor
- **Precondition**: `eagleeye-kid` has row 1 and the text of TC-005-07

**Steps**

1. **Konto** → `ee-kid3`.
2. Click **Eintrag hinzufügen** (leave it off). Click into the display text box, select all, type `Text für ee-kid3 ` + **🙂**, and then **directly** switch **Konto** → `eagleeye-kid` (do not click anywhere else first).
3. **Konto** → `ee-kid3` again.

**Expected result**

- Step 1: **Keine Ruhezeiten festgelegt.** and the **default text** (not eagleeye-kid's text).
- Step 2: `eagleeye-kid` shows its own row 1 and its two-line text. Log: "Break times of account ee-kid3 …: entry N added …" and "… ee-kid3 …: display text changed" (saved although you switched the account directly).
- Step 3: `ee-kid3` shows its one entry and "Text für ee-kid3 🙂".

#### TC-005-09: Page states; data kept over app restart and service restart; no crash

- **Verifies**: AC-6, AC-5 (reconnect), AC-17 (app and service restart), AC-1 (start page)
- **Machine / account**: Service PC / Admin, Terminal (Administrator)
- **Precondition**: Regeln, **Konto** `ee-kid3` selected (the second account)

**Steps**

1. `Stop-Service -DisplayName "EagleEye Service"`. Watch the page until the status bar is red.
2. `Start-Service -DisplayName "EagleEye Service"`. Do not touch the app; watch the page.
3. Close the parent app (window X) and start it again. Click **Regeln**; look at both accounts.
4. *Einstellungen*: untick **eagleeye-kid** and **ee-kid3**. **Regeln**. Then tick both again; **Regeln**.

**Expected result**

- Step 1: the app does **not** crash; the page shows **Keine Daten verfügbar**, no **Konto**, no table, no text box, nothing editable.
- Step 2: green within 60 s; the page shows **Konto** with the **first** account (`eagleeye-kid`; after a reconnect the first account is selected again, report D-13) and exactly its stored entries and text. *Note only*: whether **Wird geladen …** was visible.
- Step 3: the app opens on **Berichte**; Regeln shows the same entries and texts for both accounts.
- Step 4: with both unticked, within 60 s **Keine Konten unter Elternkontrolle. Konten unter Einstellungen auswählen.**; after ticking again, both accounts with their **unchanged** entries and texts (kept while unticked).

#### TC-005-10: Light and dark mode, 150 %

- **Verifies**: AC-4
- **Machine / account**: Service PC / Admin
- **Precondition**: Regeln → `eagleeye-kid` (one row, two-line text)

**Steps**

1. Look at the page; screenshot → `evidence/run01-tc10-regeln-<mode>.png`.
2. *Einstellungen → Darstellung*: switch to the other mode (Hell ↔ Dunkel); **Regeln** again; screenshot. Switch back.

**Expected result**

- In both modes: all texts, checkboxes, time fields and the **trash icon** are clearly visible; only the app's normal colours plus the accent-coloured **Ruhezeiten** bar (not the colours of the mock).
- At 150 %: no text cut off, all nine checkbox columns fit without overlapping.

### Block B — Blocking at start and the kid's dialog (core)

*Service PC, in kid1's session (`eagleeye-kid`); K-TERM with LOG-WATCH; P-KID open; Edge maximised. App B on LEOSERV on Regeln → `eagleeye-kid` (E1 created, off).*

> **Before Block B**: P-1 to P-7 (§3). Then do not close P-KID, K-TERM or Edge until Block C.

#### TC-005-11: Outside a break apps start as before (and an entry that is off never blocks)

- **Verifies**: AC-24 (no break → normal), AC-29, AC-14
- **Machine / account**: Service PC / kid1, K-TERM; App B
- **Precondition**: E1 off (row 1 from Block A is off too)

**Steps**

1. Windows+R → `notepad` → Enter. Wait 30 s. Close it. Windows+R → `mspaint` → Enter. Wait 30 s. Close it.
2. K-TERM: LOG-FIND `Blocked start`. App B → **Berichte** → `eagleeye-kid`: note the **Editor** value (**ED0**); back to **Regeln**.

**Expected result**

- Both open as quickly as before EagleEye 0.5.0 and stay open (not noticeably slower: your judgement).
- No "Blocked start" line from this run.

#### TC-005-12: In a break, Notepad is blocked: dialog within 10 s, closed gracefully, gone within 30 s

- **Verifies**: AC-21, AC-22 (graceful), AC-26 (on ≤ 5 s), AC-28, AC-30 (timing, over a maximised window); security: graceful close under the real SYSTEM token (plan Q-8, report §7 A)
- **Machine / account**: Service PC / kid1, K-TERM; App B
- **Precondition**: Edge maximised in front; no Notepad open; stopwatch ready

**Steps**

1. App B: **BREAK ON** (tick An/Aus of E1). When the tick is confirmed, count **5 s**.
2. Windows+R → `notepad`; **start the stopwatch on Enter**. **Lap 1** when the EagleEye dialog appears; **lap 2** when Notepad's window is gone.
3. Click **OK**.
4. K-TERM: LOG-FIND `Blocked start|Break time of account|entry .* changed`.

**Expected result**

- Notepad's window may show briefly. The dialog **EagleEye** with eagleeye-kid's two-line text appears in front of the maximised Edge **≤ 10 s** after Enter (expected 1 to 3 s). Notepad is gone **≤ 30 s** after Enter (expected ≤ 5 s).
- Log: "entry N changed: on, …"; "Blocked start: account eagleeye-kid, Editor (notepad.exe, <path>), process …, break time HH:MM–23:59 (Mo Tu We Th Fr Sa Su); closing the app." and "Blocked start ended: … **closed gracefully** after 0.x s; **message shown**."
- **Q-8**: the outcome must be **closed gracefully**. "terminated by force after 20 s" for Notepad is a **Fail** (the agent's close request did not work under the SYSTEM token).
- *Note only*: "Break time of account eagleeye-kid began: …".

#### TC-005-13: The dialog: topmost over full-screen, OK/Enter only, exact text

- **Verifies**: AC-30, AC-31, AC-16 (emojis in the dialog)
- **Machine / account**: Service PC / kid1
- **Precondition**: BREAK ON; Edge in front

**Steps**

1. In Edge press **F11** (full screen; a playing video is ideal).
2. Windows+R → `notepad` → Enter. Wait until the dialog appears. *Note only*: type a letter at once — does it go into the dialog (focus)?
3. Read the dialog: title, text, buttons, title bar. Compare the text with TC-005-07.
4. Click into the dialog's text. Press **Esc**. Press **Alt+F4**.
5. Click on Edge behind the dialog and scroll. Wait **60 s**.
6. Click into the dialog's text and press **Enter**. Then **F11** in Edge.

**Expected result**

- Step 2: the dialog appears **in front of the full-screen Edge**.
- Step 3: title **EagleEye**; the text **exactly** as stored: two lines "Pause! Test 1 😊" / "Lies ein Buch 📚 👍", the emojis recognisable (monochrome is fine); one button **OK**; **no** close box (X).
- Step 4: the dialog stays open each time.
- Step 5: Edge reacts behind it; the dialog stays in front and does not close by itself.
- Step 6: Enter closes it.

#### TC-005-14: Only one dialog at a time

- **Verifies**: AC-32, AC-22, AC-28
- **Machine / account**: Service PC / kid1, K-TERM
- **Precondition**: BREAK ON; no dialog open

**Steps**

1. Windows+R → `notepad` → Enter. Leave the dialog open.
2. Windows+R → `mspaint` → Enter. Wait 30 s.
3. Click **OK** (with the mouse).
4. Windows+R → `mspaint` → Enter. Click **OK**.
5. LOG-FIND `Blocked start ended`.

**Expected result**

- Step 2: Paint is closed (gone ≤ 30 s); **no** second dialog.
- Step 4: a **new** dialog appears; OK closes it.
- Log: Paint of step 2 "… closed gracefully after … s; **not shown (a message is already open)**"; Paint of step 4 "… message shown".

#### TC-005-15: A blocked start counts no usage; an app running before the break keeps counting

- **Verifies**: AC-24, AC-25, AC-36 (no usage)
- **Machine / account**: Service PC / kid1, K-TERM; App B → **Berichte** → `eagleeye-kid`
- **Precondition**: BREAK ON. In App B note: is there a row **Zeichentabelle** today (expected: no; else its value **Z0**); **Microsoft Edge** value **EG0**; **Editor** value (compare with ED0)

**Steps**

1. Windows+R → `charmap` → Enter. Click **OK**.
2. Use Edge (open since before the break) for **3 minutes** (stopwatch).
3. Read App B. K-TERM: LOG-FIND `Zeichentabelle|charmap`.

**Expected result**

- **No** row "Zeichentabelle" appears (or it stays at Z0). **Editor** still = ED0 (the blocked Notepads of TC-005-12 to -14 added nothing).
- **Microsoft Edge** grew by about **3** minutes (±1) and was never closed.
- Log for charmap: only "Blocked start …" lines, **no** "App started: account eagleeye-kid, Zeichentabelle …". (A "New app for account …" line is a Note, not a Fail.)

#### TC-005-16: An app that does not close by itself is terminated by force 20 s later (Paint with an unsaved drawing)

- **Verifies**: AC-22 (force, all processes; AC-22 note "caught afterwards"), AC-25 (Edge survives the restart), AC-27 (after a service restart), AC-28; security: termination across sessions (report §7 B); OQ-17 (second start during the close)
- **Machine / account**: Service PC / kid1, K-TERM, P-KID
- **Precondition**: BREAK ON; no Paint open; no dialog open; stopwatch ready

**Steps**

1. K-TERM: `Stop-Service -DisplayName "EagleEye Service"`; wait until `Get-Service -DisplayName "EagleEye Service"` shows *Stopped*.
2. Windows+R → `mspaint` → Enter (nothing happens: no service). Draw a line. **Do not save.**
3. K-TERM: `Start-Service -DisplayName "EagleEye Service"`; then LOG-WATCH again.
4. Watch Paint. When Paint shows its **"save?" prompt**, **start the stopwatch**. **Do not answer the prompt.**
5. Within about 5 s after the prompt: Windows+R → `mspaint` → Enter (a second Paint).
6. **Stop the stopwatch** when the last Paint window is gone.
7. P-KID: `Get-Process mspaint -ErrorAction SilentlyContinue`. K-TERM: LOG-FIND `Paint|could not`.

**Expected result**

- Step 4: within about 10 s after the service start, Paint shows its "save?" prompt (the close request). The EagleEye dialog appears too, **or** not, if the tray client has not yet reconnected (both are fine; note which).
- Step 6: Paint is gone **20 s** (±2) after the prompt appeared. The second Paint is gone at the latest together with the first; **no** second dialog.
- Step 7: no `mspaint` process left.
- Log: **one** "Blocked start: account eagleeye-kid, Paint (mspaint.exe, …) … closing the app." and "Blocked start ended: … **terminated by force after 20 s** (2x.x s until gone); message shown | not shown (tray client not connected)". **No** "could not be terminated" Warning, no Error.
- **Edge** (started before the break) is still open after the service restart.

#### TC-005-17: Switching the entry off lets apps start again within 5 s

- **Verifies**: AC-26, AC-14
- **Machine / account**: Service PC / kid1; App B
- **Precondition**: BREAK ON

**Steps**

1. App B: **BREAK OFF**. When confirmed, count 5 s.
2. Windows+R → `notepad` → Enter. Wait 30 s. Close it.
3. App B: **BREAK ON** again (for Block C).

**Expected result**

- Notepad opens normally and stays open; no dialog; log "entry N changed: off, …" and no new "Blocked start" for this Notepad.

### Block C — Other accounts and exemptions (core)

*Service PC. E1 ON (kid1 in a break).*

#### TC-005-18: kid1's break does not affect other accounts; the dialog appears only in kid1's session

- **Verifies**: AC-34, AC-20 (kid1's entries never applied to others), AC-23 (uncontrolled and admin accounts)
- **Machine / account**: Service PC / kid1, ee-kid3, ee-kid2, Admin
- **Precondition**: BREAK ON; kid1 in front

**Steps**

1. kid1: Windows+R → `notepad` → Enter. **Leave the dialog open.**
2. *Benutzer wechseln* → `ee-kid3`. Windows+R → `notepad`, then `mspaint`. Use them 30 s; leave them open.
3. *Benutzer wechseln* → `ee-kid2`. Same as step 2. Then sign `ee-kid2` out.
4. *Benutzer wechseln* → Admin. Start Editor; close it. Terminal (Administrator): LOG-FIND `Blocked start: account`.
5. *Benutzer wechseln* → `eagleeye-kid`. Look at the dialog; click **OK**.

**Expected result**

- Steps 2 to 4: all apps open normally and stay open; **no** EagleEye dialog in `ee-kid3`'s, `ee-kid2`'s or your session.
- Log: "Blocked start" lines only for **eagleeye-kid**, none for `ee-kid3`, `ee-kid2` or your account.
- Step 5: kid1's dialog is still there.

#### TC-005-19: ee-kid3's own break and own text; unticked = kept but not applied

- **Verifies**: AC-20 (own entries, untick keeps, re-tick applies), AC-31 (account's text), AC-21
- **Machine / account**: Service PC / Admin (App A), ee-kid3
- **Precondition**: `ee-kid3` signed in (Notepad and Paint open from TC-005-18)

**Steps**

1. Admin: App A → **Regeln** → `ee-kid3`: its entry (TC-005-08): **Start-Zeit** = now − 1 min, Enter; tick **An/Aus**.
2. *Benutzer wechseln* → `ee-kid3`. Notepad and Paint still open? Windows+R → `charmap` → Enter. Read the dialog; **OK**.
3. Admin: *Einstellungen* → untick **ee-kid3**. **Regeln** → open **Konto**.
4. `ee-kid3`: Windows+R → `charmap`. Close it after 10 s.
5. Admin: tick **ee-kid3** again. **Regeln** → `ee-kid3`.
6. `ee-kid3`: Windows+R → `charmap`. **OK**.
7. Admin: untick **An/Aus** of ee-kid3's entry. Sign `ee-kid3` out (*Task-Manager → Benutzer*).

**Expected result**

- Step 2: Notepad and Paint (opened before the break) still open; charmap blocked; the dialog shows **"Text für ee-kid3 🙂"** (not kid1's text).
- Step 3: within 60 s `ee-kid3` is no longer in **Konto**.
- Step 4: charmap opens normally (rules of an unticked account are not applied).
- Step 5: `ee-kid3` is back with its entry (on, same times) and its text.
- Step 6: charmap blocked again.

#### TC-005-20: During a break the kid can still use the shell, lock and sign out; Task-Manager and Settings are blocked

- **Verifies**: AC-23, AC-30 (sign-out with the dialog open)
- **Machine / account**: Service PC / kid1
- **Precondition**: BREAK ON; kid1 in front

**Steps**

1. Open and close the **Start menu**. Windows+E (File Explorer); keep it open **30 s**; close it.
2. Right-click the EagleEye tray icon → *App Infos*; close it.
3. **Windows+L**; unlock.
4. **Strg+Umschalt+Esc** (Task-Manager). **OK** in the dialog.
5. **Windows+I** (Einstellungen). **OK**.
6. Windows+R → `osk` (Bildschirmtastatur). **OK**.
7. Windows+R → `notepad`. **Leave the dialog open.** Start → user icon → **Abmelden**.

**Expected result**

- Steps 1 to 3 work as usual; the Explorer window and *App Infos* are **not** closed; the tray icon stays.
- Steps 4 to 6: Task-Manager, Einstellungen and Bildschirmtastatur are closed (≤ 30 s) with the dialog.
- Step 7: sign-out works although the dialog is open.

> **Before Block D**: App B **BREAK OFF**. Sign in as `eagleeye-kid`; open **P-KID**, **K-TERM** + LOG-WATCH, and **Edge** again (P-4, P-5, P-7).

### Block D — Boundaries, tray, two parent apps, re-install (core)

#### TC-005-21: Start minute included, end minute excluded; weekday; overlapping entries; running app keeps running

- **Verifies**: AC-21, AC-25, OQ-6
- **Machine / account**: Service PC / kid1 (seconds in the clock); App B
- **Precondition**: E1 off. Read the time on kid1's clock and pick **T** = current minute + 3 (e.g. at 14:11:xx → T = 14:14).

**Steps**

1. App B: **Eintrag hinzufügen** → **E2**: **Start-Zeit** T, **End-Zeit** T + 2 min (e.g. 14:14–14:16); tick **An/Aus**. (If the clock passes T − 0:20 before you are done, pick a new T.)
2. At **T − 0:15** (e.g. 14:13:45): Windows+R → `notepad`. Keep it open until step 5.
3. At **T + 0:05**: Windows+R → `charmap`. **OK**.
4. At **T + 1:45** (in the last minute): Windows+R → `mspaint`. **OK**.
5. At **T + 2:05**: Windows+R → `mspaint`. Close it after 10 s. Close Notepad.
6. Weekday: App B: in **E1** untick **today's** day; **BREAK ON**; wait 5 s. Windows+R → `charmap`; close it. Then tick today's day again; wait 5 s. Windows+R → `charmap`; **OK**.
7. Overlap: App B: E2 → **End-Zeit** 23:59, Enter, then **Start-Zeit** = now − 1 min, Enter (E1 and E2 both in effect). **BREAK OFF** (E1 off); wait 5 s. Windows+R → `charmap`; **OK**. Untick **An/Aus** of E2; wait 5 s. Windows+R → `charmap`; close it.

**Expected result**

- Step 2: Notepad opens normally and is **never** closed during the break (it ran before the break began).
- Steps 3 and 4: blocked (dialog, app gone).
- Step 5: Paint opens normally.
- Step 6: charmap opens normally while today is not ticked; blocked once it is ticked again.
- Step 7: blocked while only E2 is on; opens normally when both are off.
- *Note only*: log "Break time of account eagleeye-kid began: T–T+2 (…)" and "… ended: …".

#### TC-005-22: Tray client not running: the app is closed all the same, no message; the restarted tray is verified

- **Verifies**: AC-33, AC-23 (tray client never blocked), AC-28; security: tray identity (report §7 C)
- **Machine / account**: Service PC / kid1, P-KID, K-TERM
- **Precondition**: E1 **on** (BREAK ON); no dialog open

**Steps**

1. P-KID: `Stop-Process -Name EagleEye.TrayClient`. The tray icon disappears.
2. Windows+R → `notepad`. Watch 60 s.
3. Start → **EagleEye Tray**. Wait 10 s.
4. Windows+R → `notepad`. **OK**.
5. K-TERM: LOG-FIND `Tray client connected|message|not shown`.

**Expected result**

- Step 2: Notepad is closed (≤ 30 s); **no** dialog, also not later.
- Step 3: the tray icon is back (the tray client is not blocked during a break).
- Step 4: the dialog appears again.
- Log: step 2 "… closed gracefully after … s; **not shown (tray client not connected)**"; step 3 "Tray client connected: … (session N, **verified**)." with kid1's session; step 4 "message shown".

#### TC-005-23: Two parent apps: changes appear in the other app within 5 s; deleted meanwhile → message

- **Verifies**: AC-19, AC-18, AC-5
- **Machine / account**: Service PC / Admin (App A) and LEOSERV (App B) side by side
- **Precondition**: both apps on **Regeln** → `eagleeye-kid`. No LEOSERV → **Blocked**

**Steps**

1. A: **Eintrag hinzufügen**. Watch B.
2. B: in that new row **End-Zeit** `22:00`, Enter. Watch A.
3. A: untick **MO** in that row. Watch B.
4. A and B at the same moment (count down): A types **Start-Zeit** `21:00`, B types `21:30`; both press Enter together. Wait 5 s.
5. B: click into that row's **Start-Zeit**, type `2015` but **do not** press Enter. A: click that row's **trash icon**. B: press **Enter**.

**Expected result**

- Steps 1 to 3: each change appears in the other app **≤ 5 s**, without any action there.
- Step 4: after 5 s both apps show the **same** start time (21:00 or 21:30).
- Step 5: B shows **Die Änderung konnte nicht gespeichert werden. Bitte erneut versuchen.** and the row disappears in B (≤ 5 s). If the row disappeared in B already before you pressed Enter (no message), write it in Notes.

#### TC-005-24: A changed display text is used for the next block within 5 s

- **Verifies**: AC-31, AC-16 (other parent app)
- **Machine / account**: Service PC / kid1; App B, then Admin (App A)
- **Precondition**: BREAK ON; kid1 in front

**Steps**

1. App B → `eagleeye-kid`: click at the end of the display text, press Enter, type `Test 3 ` + paste **👍**; click outside the box. Count 5 s.
2. kid1: Windows+R → `notepad`. Read the dialog; **OK**.
3. Later in the admin session: App A → Regeln → `eagleeye-kid`.

**Expected result**

- Step 2: the dialog shows **three** lines, the third "Test 3 👍".
- Step 3: App A shows the same three lines with the emojis.

#### TC-005-25: Re-install keeps entries and text; enforcement without any parent app

- **Verifies**: AC-17 (update), AC-27 (no parent app, after a service restart)
- **Machine / account**: Service PC / Admin, kid1
- **Precondition**: BREAK ON. Screenshot of Regeln for both accounts in App A

**Steps**

1. **Close** App A and App B (window X).
2. Admin: run `03_Delivery\windows\EagleEye-Setup-0.5.0.exe` again with defaults.
3. *Benutzer wechseln* → `eagleeye-kid`. Windows+R → `notepad`. **OK**.
4. *Benutzer wechseln* → Admin. Start App A → **Regeln** → both accounts. Log monitor: LOG-WATCH again; LOG-FIND `Break times loaded`.

**Expected result**

- Step 3: Notepad is blocked (dialog, gone ≤ 30 s) although no parent app runs.
- Step 4: the same entries (values, on/off, order) and texts as in the screenshot; log "Break times loaded: … entries (… on) of 2 accounts, 2 changed display texts."

### Block E — Security (core security)

*Implementation plan Q-8 and Manual Verification Notes; implementation report §6 and §7. No AC (except TC-005-28: AC-37); a Fail is filed as an issue of US-005 with severity by impact.*

> **Before Block E**: start App B on LEOSERV again (closed in TC-005-25) → Regeln → `eagleeye-kid`. kid1 in front with P-KID and K-TERM.

#### TC-005-26: Which restricted SYSTEM token the agent runs with (US-004 TC-004-23, rerun)

- **Verifies**: — (security: ADR-011 §7; US-004 plan Q-9; US-005 plan Q-8)
- **Machine / account**: Service PC / Admin, Terminal (Administrator)
- **Precondition**: kid1 signed in

**Steps**

1. LOG-FIND with `Session agent|write restriction|write-restricted`.

**Expected result** <!-- verify against implementation report: diagnostic line taken from the US-004 plan; not in the US-005 report -->

- "Session agent started in session N (process P)." and "Session agent in session N: diagnostics: user NT-AUTORITÄT\SYSTEM (S-1-5-18), integrity System, privileges SeChangeNotifyPrivilege, write-restricted yes|no".
- User **SYSTEM**, integrity **System**, **only** `SeChangeNotifyPrivilege`.
- **write-restricted yes**, or the accepted fallback (a Warning "Session agents exit at start with a write-restricted token; they run without the write restriction from now on …" and agents with `write-restricted no`). Both are a Pass. **Write the variant in Notes** (DEV and ARC need it). Together with TC-005-12 ("closed gracefully") this answers Q-8.

#### TC-005-27: The kid cannot end the enforcement

- **Verifies**: — (security: the kid cannot stop the service, its agent or the closing)
- **Machine / account**: Service PC / kid1, **P-KID** (normal, not K-TERM)
- **Precondition**: BREAK ON

**Steps**

1. P-KID:
   ```powershell
   Stop-Service -DisplayName "EagleEye Service"
   Get-Process -Name EagleEye.Service | Stop-Process -Force
   $n = (Get-Service -DisplayName "EagleEye Service").Name; sc.exe config $n start= disabled
   Get-Service -DisplayName "EagleEye Service"
   ```
2. Windows+R → `notepad`. **OK**.

**Expected result**

- Each command of step 1 fails (*Zugriff verweigert* / access denied, `[SC] OpenService FEHLER 5`); the service is still *Running*.
- Step 2: Notepad is blocked as before.

#### TC-005-28: The kid changes the time zone: Warning within 5 s; break times follow the new local time

- **Verifies**: AC-37
- **Machine / account**: Service PC / kid1, P-KID, K-TERM
- **Precondition**: BREAK ON; in App B set E1's **Start-Zeit** to **now − 1 min** first (so a shift of 1 to 2 hours back leaves the break)

**Steps**

1. P-KID: `tzutil /g` → note it (expected `W. Europe Standard Time`).
2. P-KID: `tzutil /s "UTC"`. Note the time. The clock moves back 1 or 2 hours. Wait **10 s**.
3. Windows+R → `notepad`. Close it after 10 s.
4. K-TERM: LOG-FIND `time zone|clock of the PC`.
5. P-KID: `tzutil /s "W. Europe Standard Time"` (the value of step 1). Wait 10 s. Windows+R → `notepad`; **OK**.

**Expected result**

- Step 3: Notepad opens normally (the local time is now before E1's start).
- Step 4: a **Warning** "The time zone of the PC changed from W. Europe Standard Time (UTC+01:00) to UTC (UTC+00:00); break times now use the new local time. Session in use: eagleeye-kid." with a timestamp ≤ 5 s after step 2.
- Step 5: a second such Warning (back); Notepad is blocked again.
- If `tzutil /s` is refused for the kid: write it in Notes, do steps 2 and 5 in K-TERM instead.

#### TC-005-29 (optional): A copied tray client is not trusted

- **Verifies**: — (security: tray identity, plan Q-6, report §7 C)
- **Machine / account**: Service PC / kid1, P-KID, K-TERM
- **Precondition**: E1 **off**

**Steps**

1. P-KID:
   ```powershell
   $src = Split-Path (Get-Process -Name EagleEye.TrayClient).Path
   $dst = Join-Path $env:USERPROFILE "EETray"
   Copy-Item $src $dst -Recurse
   Stop-Process -Name EagleEye.TrayClient
   Start-Process (Join-Path $dst "EagleEye.TrayClient.exe")
   ```
2. App B: **BREAK ON**; 5 s. Windows+R → `notepad`. Watch 60 s.
3. K-TERM: LOG-FIND `Tray client connected|not shown`.
4. Cleanup: P-KID `Stop-Process -Name EagleEye.TrayClient; Remove-Item $dst -Recurse -Force`; Start → **EagleEye Tray**.

**Expected result**

- Log: the copy connects as "Tray client connected: … (session N, **unverified**)."
- Notepad is closed; **no** dialog (also not from the copy); log "… not shown (tray client not connected)".

### Block F — Account deletion and optional checks

#### TC-005-30: Deleting an account purges its rules and history; a new account of the same name starts empty

- **Verifies**: AC-20 (deletion, rename optional), AC-36 (purge with the account)
- **Machine / account**: Service PC / Admin, Terminal (Administrator)
- **Precondition**: `ee-kid3` signed out; App A → Regeln → **Konto** `ee-kid3` (its entry and text)

**Steps**

1. *Optional*: `Rename-LocalUser -Name ee-kid3 -NewName ee-kid3x`; within 60 s Regeln shows **ee-kid3x** with the same entry and text; `Rename-LocalUser -Name ee-kid3x -NewName ee-kid3`.
2. Delete the account:
   ```powershell
   Get-CimInstance Win32_UserProfile | Where-Object LocalPath -like "*\ee-kid3" | Remove-CimInstance
   Remove-LocalUser -Name "ee-kid3"
   ```
   Watch App A and the log monitor for 60 s.
3. Create `ee-kid3` again (S-4 commands); *Einstellungen*: tick it; **Regeln** → `ee-kid3`.

**Expected result**

- Step 2: within **60 s** `ee-kid3` disappears from **Konto**; log "Purged the break times of deleted account S-1-5-21-…" and "Purged enforcement records of deleted accounts: …" (its blocked starts of TC-005-19).
- Step 3: **Keine Ruhezeiten festgelegt.** and the **default text**.

#### TC-005-31 (optional): An app started from another app is blocked too

- **Verifies**: AC-22 ("started from another app")
- **Machine / account**: Service PC / kid1, P-KID
- **Precondition**: BREAK ON

**Steps**

1. P-KID: `Start-Process notepad`. Stopwatch from Enter.

**Expected result**

- Dialog ≤ 10 s; Notepad gone ≤ 30 s; log "closed gracefully"; P-KID itself stays open.

#### TC-005-32 (optional): A Store app (Rechner) is blocked; other Store apps stay

- **Verifies**: AC-22 (Store app, all processes, ≤ 30 s)
- **Machine / account**: Service PC / kid1, K-TERM
- **Precondition**: BREAK OFF; start the Store app **Uhr** (Clock) and leave it open; then BREAK ON

**Steps**

1. Windows+R → `calc`; stopwatch from Enter; lap when the dialog appears, lap when Rechner is gone. **OK**.
2. LOG-FIND `Rechner`.

**Expected result**

- Dialog ≤ 10 s; Rechner gone ≤ **30 s** from Enter. Outcome "closed gracefully" or "terminated by force after 20 s" (both accepted; write which).
- **Uhr** stays open (the shared Store frame host is not ended).

#### TC-005-33 (optional): Edge is blocked with all its processes

- **Verifies**: AC-22 step 3 (all processes, e.g. all `msedge.exe`)
- **Machine / account**: Service PC / kid1, P-KID
- **Precondition**: BREAK OFF. Close Edge. P-KID: `Get-Process msedge -ErrorAction SilentlyContinue | Stop-Process -Force` (startup boost may keep Edge processes; an Edge that runs since before the break is not a start). Then BREAK ON

**Steps**

1. Start **Microsoft Edge** from the taskbar; stopwatch; **OK** in the dialog.
2. 30 s after the start: P-KID `Get-Process msedge -ErrorAction SilentlyContinue`.

**Expected result**

- Edge blocked (dialog ≤ 10 s, window gone ≤ 30 s); step 2 lists **no** `msedge` process.

#### TC-005-34 (optional, needs a SQLite tool): History of blocked starts and time-zone findings in the database

- **Verifies**: AC-35, AC-36, AC-37 (stored)
- **Machine / account**: Service PC / Admin, Terminal (Administrator)
- **Precondition**: S-13; after TC-005-28 and TC-005-30

**Steps**

1. Read a copy (the database stays untouched):
   ```powershell
   $tmp = "C:\EETest\db"; New-Item -ItemType Directory -Force $tmp | Out-Null
   Copy-Item "$env:ProgramData\EagleEye\EagleEye.Service.db*" $tmp -Force
   sqlite3 -readonly "$tmp\EagleEye.Service.db" "SELECT StartedLocal, UserName, DisplayName, ProcessName, Trigger, EntryStartMinute, EntryEndMinute, Weekday, Outcome, SecondsUntilGone, MessageState FROM BlockedStarts ORDER BY BlockedStartId;"
   sqlite3 -readonly "$tmp\EagleEye.Service.db" "SELECT DetectedLocal, Kind, OldValue, NewValue, SessionId, UserName FROM TimeChangeFindings;"
   ```

**Expected result**

- One `BlockedStarts` row per blocked start of this run (also those before the re-install in TC-005-25): time, account, app name, process, path, entry minutes (e.g. 849/1439 = 14:09/23:59), weekday, outcome, seconds, message state as in the log. The Paint row of TC-005-16: "terminated by force", `SecondsUntilGone` ≈ 20, `Trigger` "found at agent start". **No** row of the deleted `ee-kid3`.
- `TimeChangeFindings`: two `TimeZone` rows from TC-005-28 with `UserName` eagleeye-kid.

#### TC-005-35 (optional): Reboot during a break

- **Verifies**: AC-23 (shut down), AC-17 (reboot), AC-27 (after reboot)
- **Machine / account**: Service PC / kid1, Admin
- **Precondition**: BREAK ON; kid1 in front

**Steps**

1. kid1: Start → *Ein/Aus* → **Neu starten**.
2. After the reboot sign in as `eagleeye-kid` (no parent app running). Wait 30 s. Windows+R → `notepad`. **OK**.
3. Admin: App A → Regeln → `eagleeye-kid`.

**Expected result**

- Step 1: the restart works. Step 2: Notepad blocked. Step 3: entries and text unchanged.

## 6. Regression

None (`02_Implementation/docs/testing/README.md` §3; story run only).

## 7. Cleanup

1. **LOG-COPY** with `<subfolder>` = `run-01-service-logs`. Look through the copies before committing.
2. **Break times**: switch **off** (or delete) every entry of `eagleeye-kid`, otherwise it stays blocked until midnight and again tomorrow (§8 Q-5).
3. **Time zone**: `tzutil /g` → `W. Europe Standard Time` (TC-005-28).
4. **Tray copy** (TC-005-29): `C:\Users\eagleeye-kid\EETray` deleted; the real tray runs.
5. **Database copy** (TC-005-34): `Remove-Item C:\EETest -Recurse -Force`.
6. **Accounts**: sign out `eagleeye-kid`; remove `ee-kid2` and `ee-kid3`:
   ```powershell
   foreach ($n in "ee-kid2","ee-kid3") {
     Get-CimInstance Win32_UserProfile | Where-Object LocalPath -like "*\$n" | Remove-CimInstance
     if (Get-LocalUser -Name $n -ErrorAction SilentlyContinue) { Remove-LocalUser -Name $n }
   }
   ```
7. **LEOSERV**: App B stays installed and paired.

## 8. Notes and Open Questions for Michael

| ID | Question | Proposed answer |
|---|---|---|
| Q-1 | **LEOSERV as App B**: needed to switch breaks on and off while the kid's session is in front (Blocks B, D) and for TC-005-23. Available? | Yes. Without it: TC-005-23 Blocked; elsewhere App A plus user switches (the 5 s checks become weak). |
| Q-2 | **Force case via a stopped service** (TC-005-16): Paint with an unsaved drawing, started while the service is stopped, instead of "Notepad with typed text" (a fresh start is closed before anything can be typed). OK? | Yes. It tests the 20 s force step, the termination across sessions and the "caught afterwards" rule in one case. |
| Q-3 | **Time-zone change by the kid** (TC-005-28): `tzutil /s "UTC"` as `eagleeye-kid`, reset right after. OK? | Yes; reset in the case itself and checked in cleanup. |
| Q-4 | **Core vs. optional**: optional are TC-005-29 and TC-005-31 to -35. No AC rests only on them. OK? | Yes. |
| Q-5 | **After the run**: all break entries of `eagleeye-kid` off (or deleted)? | Yes, switch them off; otherwise the kid account stays blocked. |
| Q-6 | **SQLite tool** for TC-005-34: available? | If not, skip TC-005-34; the log lines carry the same data (AC-35, AC-37). |

## 9. Change Log

| Date | Change | Reason |
|---|---|---|
| 2026-10-11 | Plan written after DEV's handover (0.5.0). | Step d. |

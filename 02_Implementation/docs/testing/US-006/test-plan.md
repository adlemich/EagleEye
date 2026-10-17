# Test Plan: US-006 — App Time Budgets and Time-elapsed Warnings

**Status**: Draft, for approval by Michael
**Date**: 2026-10-17
**Author**: TES
**User Story**: `02_Implementation/docs/requirements/user-stories/US-006/user-story.md` (39 ACs in groups A to J, approved 2026-10-12, OQ-1 to OQ-19 and plan Q-1 to Q-5 answered; status Implemented)
**Inputs used**: the user story; `02_Implementation/docs/requirements/user-stories/US-006/implementation-report.md` (§6 risks, §7 checks A to D, §8 "How to test": installers and hashes, UI texts de/en, log lines, database tables; deviations D-1 to D-16, used only for setup, texts and artifact locations, not for expected behaviour); `02_Implementation/docs/requirements/user-stories/US-006/implementation-plan.md` ("Manual Verification Notes", "Logging", D-1 to D-11, Q-1 to Q-7, R-1 to R-13); `02_Implementation/docs/testing/US-005/test-plan.md`, `test-run-01.md` and `test-report.md` (format, accounts, skipped cases); `02_Implementation/docs/testing/README.md` §4. Black box: no source code was read. TES checked both installer hashes against the files on 2026-10-17.

> **Why this run matters.** This is the **first live run of enforcement against running apps**. DEV could not run any of it in a real session (report D-15): blocking at start by an app rule, the early and final warnings, closing running apps (graceful, then by force after 20 s), the grace period after a parent change (Q-2), closing all apps when a break begins (this replaces US-005 AC-25), the end at midnight, and the kid dialog with its info line and the "newer message replaces the text" behaviour. The cases use short budgets (1 to 3 minutes) and breaks set a few minutes ahead, so each one takes 2 to 5 minutes.

> **Lessons applied.** US-001: the steps use the **German UI labels** you see. US-004 and US-005: the core comes first, every case says **must-run** or **optional**, and **the result box is what counts**. Notes blanks are there for timings that help the evaluation; leave them empty if you like. US-005 skipped TC-005-26 (agent token) twice: it is a must-run case here (TC-006-34, plan Q-4).

---

## 1. Scope

**In scope** (only the new US-006 features):

- Upgrade of the service and both parent apps to 0.6.0, with **deny by default** for every app recorded before (AC-5, OQ-2)
- Rules page, section **Applikations-Regeln**: layout, states, editing in place, validation, budget text, live rows, two parent apps (AC-1 to AC-12, AC-17)
- Settings page, section **Zeit-Ende Warnungen**: two fixed levels, validation, texts (AC-13 to AC-16)
- Storage per account, untick, deletion, restart, re-install (AC-18)
- Start check: not allowed today, budget used up, warning at start, break wins (AC-19 to AC-22)
- Running apps: countdown, early and final warnings, close at the budget end with graceful and forced close (AC-23 to AC-27)
- Break times: warnings before a break, closing every running app at the break start (AC-28 to AC-31)
- Changes during the day: grace period of the full final-warning minutes (AC-32, AC-33)
- Kid dialog with info line and text replacement, tray client not running, no parent app, log entries (AC-34 to AC-37)
- Load with 10 allowed apps (AC-38); time-zone and clock changes logged as a Warning (AC-39)
- English default texts in the kid dialog (AC-10, AC-14, ADR-016, plan R-10)
- Security rerun without an AC: TC-005-26, the agent token (plan Q-4), plus "a Notepad budget close ends **closed gracefully**"

**Out of scope** (story "Out of Scope"): time-zone hardening; process-based enforcement (programs without a window); budgets per weekday; a remaining-time display in the tray; "used vs. budget" on Reports; events to the parent app; showing the history; bonus time; per-account or more than two warning levels; placeholders in warning texts; app groups; merging app records; a configurable close timeout; Android, iOS and macOS parent apps; protection against a kid pairing its own parent app.

**Not tested in this run, with reason**

| Item | Reason |
|---|---|
| Regression, US-005 re-test | Story runs contain no regression cases (`02_Implementation/docs/testing/README.md` §3, Rule 4). |
| An account that **becomes an admin account** (AC-18, second bullet) | Needs a group change plus a sign-out and sign-in cycle. The untick path (same bullet) is tested in TC-006-37. Unit tests cover the rest. |
| English **parent app** | Your Windows is German. Only optional TC-006-42, if a parent PC with English Windows exists (e.g. LEOSERV). |
| Exclusive full-screen games (AC-34) | Known limit stated in the story (plan TI-16). The app is still closed on time. Not a Pass criterion. |
| 90-day retention of the history (AC-37) | Cannot be waited for. Unit tests cover the purge. |
| Log limiter (30 "Blocked start" lines per account and app per hour) | Needs dozens of blocked starts. Unit tests cover it. |
| Daylight-saving switch; service restart during a warning or grace period | Cannot be produced in a run, or accepted by design (plan D-5, D-6, R-9). |
| "Several windows of one app count once" (AC-23) | US-004 behaviour, unchanged; tested there. |

## 2. Test Environment

| Item | Value |
|---|---|
| Service PC | **Windows Developer Machine** (Windows 11 Pro, German UI, `ZOCK-O-MAT-V3`). Current state: EagleEye **0.5.0** service running, App A paired. |
| Parent PC | **LEOSERV** with **App B**. Needed to change rules **while the kid's session is in front** (Blocks B to E) and for two parent apps at once (TC-006-10). Used time only counts while the kid's session is in front, so switching users to change a rule would distort the timings. Without LEOSERV, TC-006-10 is **Blocked**, and the timing checks of Blocks C and D become weak. |
| Parent apps | **App A** = parent app in your admin session on the service PC (device *Papas PC*). **App B** = parent app on LEOSERV (device *LEOSERV*). |
| Accounts on the service PC | **Admin** = you. **kid1** = `eagleeye-kid` (standard, **controlled**, German). **ee-kid2** = new standard account, **controlled**, Windows display language **English** (the story's "kid2"; deleted in TC-006-37). **ee-kid3** = new standard account, **not controlled** (the story's "kid3"). *Careful: in US-005, `ee-kid2` was the uncontrolled one. Both accounts were removed in the US-005 cleanup and are created fresh here, with the story's roles.* |
| Builds under test | `03_Delivery/windows/EagleEye-Setup-0.6.0.exe` (service + tray, admin; built 2026-10-12 13:48, SHA-256 `416E0A74A703A1A7728F541D83E5A40B3D21451A3C462467489507BEB1E03950`) and `03_Delivery/windows/EagleEye-ParentApp-Setup-0.6.0.exe` (parent app, per user; built 2026-10-12 13:49, SHA-256 `56500A6A4CBDE74301193FC40DA3E6D24A39851ADAE926AABFE2AEFC86ED34E5`). |
| Expected versions | *Installierte Apps*: **EagleEye 0.6.0**, **EagleEye Parent App 0.6.0**. Tray *App Infos*: `EagleEye_v0.6`. |
| Tools | Phone stopwatch **with lap times**. **Terminal (Administrator)** in the admin session. **K-TERM** = elevated terminal **inside the kid's session**, started with your admin credentials (it runs as you, so it is never limited). Seconds in the kid's taskbar clock. Optional: SQLite tool (TC-006-41). |

### Deny by default: read this before you install

Right after the update, **every app that a controlled account has ever used is blocked** (0:00, no day ticked) until you set a rule (AC-5, OQ-2). This includes browsers, games, Terminal, Task-Manager, *Einstellungen* and the Snipping Tool. The Start menu, File Explorer, locking, signing out and the EagleEye tray icon keep working (AC-27).

- **Install only when the kids do not need the PC.**
- Your admin account is never affected.
- Real kid accounts that are ticked in *Einstellungen* are blocked too. Either untick them for the test run, or set their rules right after TC-006-04.
- Do everything that needs *Einstellungen* in a kid account (English language, clock seconds) **before** the upgrade (setup S-6, S-7).
- At a kid's sign-in, autostart apps with a window (e.g. OneDrive, Teams) may be blocked and show the dialog. Click **OK**. Write which apps in Notes. This is expected, not a Fail.
- Screenshots in a kid session: the Snipping Tool is blocked too. Use your phone camera if you want evidence.

### Test programs (Windows+R in the kid's session)

| Program | Start | Name in the table (as on *Berichte*) | Used for |
|---|---|---|---|
| Editor (Notepad), no text | `notepad` | **Editor** | Budget 0:03: start, warnings, graceful close (TC-006-15/16); later 23:59 |
| Paint | `mspaint` | **Paint** | Not allowed today (TC-006-13); **with an unsaved drawing** it asks "save?" and must be ended by force (TC-006-20, -23) |
| Rechner (Calculator, Store app) | `calc` | **Rechner** | Default rule, blocked (TC-006-12, -32) |
| Zeichentabelle (Character Map) | `charmap` | **Zeichentabelle** | Final warning at start; grace after 0:00; break cases |
| Systeminformationen | `msinfo32` | **Systeminformationen** | The **new** app that appears live (TC-006-14); grace with 20 s left (TC-006-18) |
| Remotedesktopverbindung | `mstsc` | **Remotedesktopverbindung** | Optional lock case (TC-006-22); CPU case |
| DirectX-Diagnoseprogramm | `dxdiag` | **DirectX-Diagnoseprogramm** | Fallback new app; CPU case |

Display names can differ slightly from this table (they come from the program files). A different name is a Note, not a Fail.

### Rule set for `eagleeye-kid` at the start (set in TC-006-04)

DEV's suggestion, adjusted to the weekday. **Today = the weekday of your run** (2026-10-17 is a Saturday).

| App | Stunden | Minuten | Days | Effect today |
|---|---|---|---|---|
| Editor | 0 | 3 | **all** (MO to SO) | allowed, 3 minutes |
| Paint | 0 | 30 | **only SA**; if you run on a **Saturday**: **only SO** | not allowed today |
| Rechner | 0 | 0 | none (default, do not touch) | blocked |
| every other app | 0 | 0 | none (default) | blocked |

**Warning levels** (Settings, global): **Frühe Warnung 2**, **Finale Warnung 1**. The texts stay at their defaults for the whole run, so the English check (TC-006-30) can show the English defaults.

The blocks change some rules on the way. Each block says which, in its precondition.

### Conventions for expected results

- **UI texts** are German. Pass criteria are the elements and their order. The exact texts are those of the implementation report §8, listed below. A wording difference is a **Note**, not a Fail. **Exceptions**: a text **you** entered (budget text, break display text) must appear in the kid dialog **exactly** as stored (lines, emojis); a difference is a **Fail**.

  | Where | Expected (German) |
  |---|---|
  | Rules section | heading bar **Applikations-Regeln**; explanation "Lege hier fest, welche Applikationen an welchen Tagen für wie lange verwendet werden dürfen. … Es werden nur Applikationen angezeigt, die mindestens ein Mal vom Kind gestartet wurden."; columns **Applikation** · **Tägliches Zeit-Budget** (over **Stunden** · **Minuten**) · **MO DI MI DO FR SA SO**; label **Anzeige Text wenn Budget aufgebraucht:**; no apps: **Noch keine Applikationen aufgezeichnet.** |
  | Rules messages | **Bitte Stunden zwischen 0 und 23 eingeben.** · **Bitte Minuten zwischen 0 und 59 eingeben.** · **Die Änderung konnte nicht gespeichert werden. Bitte erneut versuchen.** |
  | Settings section | heading bar **Zeit-Ende Warnungen**; explanation "Hier kannst du 2 Zeitpunkte einstellen, …"; columns **Zeitpunkt** · **Minuten vor Ende** · **Anzeigetext**; rows **Frühe Warnung**, **Finale Warnung** |
  | Settings messages | **Bitte Minuten zwischen 1 und 59 eingeben.** · **Die finale Warnung muss weniger Minuten vor dem Ende liegen als die frühe Warnung.** |
  | Page states (US-005) | **Keine Daten verfügbar** · **Keine Konten unter Elternkontrolle. Konten unter Einstellungen auswählen.** · **Wird geladen …** |
  | Default budget text | "Du hast deine Zeit aufgebraucht! Leider ist jetzt Schluss." |
  | Default early warning | "Achtung, du hast nur noch 5 Minuten! Speichere deinen Fortschritt." *(says 5 although the level is 2: by design, OQ-8; the info line shows the real minutes)* |
  | Default final warning | "Achtung!!! Nur noch 1 Minute – jetzt aber schnell speichern!" |
  | Kid dialog | title **EagleEye**; the parent's text (or the default); a separator; the **info line**; button **OK** |
  | Info lines (examples with Editor) | "Editor: noch 2 Minuten." · "Editor: noch 1 Minute." · "Editor: Zeit-Budget aufgebraucht." · "Editor: heute nicht erlaubt." · "Die Ruhezeit beginnt um 20:00." · "Ruhezeit von 20:00 bis 24:00." · "Editor: Deine Eltern haben die Regeln geändert. Editor wird in 1 Minute geschlossen." · "Deine Eltern haben eine Ruhezeit eingestellt. Alle Apps werden in 1 Minute geschlossen." |
  | English (TC-006-30) | budget "You have used up your time! Sorry, that's it for now." · early "Attention, you only have 5 minutes left! Save your progress." · final "Attention!!! Only 1 minute left – save quickly now!" · break "Hi! Unfortunately, your parents have set a PC break for this time. You can't use this program right now. Sorry. How about using the time to read a book? 😊" · info lines "Notepad: 2 minutes left." · "Notepad: 1 minute left." · "Notepad: time budget used up." · "Notepad: not allowed today." · "Break time from 8:00 PM to midnight." (time in the kid's format) |

- **Accepted inputs**: hours and minutes are numeric fields; letters cannot be typed. "Letters not accepted" and "message shown" are both a Pass for an `abc` step.

- **Timing** (story limits; the service checks every 5 s, plan D-1):
  - Warning ≤ **10 s** after the level is reached. Close request ≤ **10 s** after the end. Force **20 s** after the request. Gone ≤ **30 s** after the end.
  - A rule change takes effect ≤ **5 s** after the parent app shows it. App B follows App A ≤ **5 s**. A new app row appears ≤ **15 s**. Account changes and purges ≤ **60 s**. Time-zone Warning ≤ **5 s**.
  - **Grace period** = the full final-warning minutes (here **1 minute**) from the moment the change takes effect.
  - A reading up to 1 s outside a window is a Note (reaction time). Then the log timestamps decide.

- **How to measure budget cases**: stopwatch **start on Enter** in *Ausführen* (Windows+R). Used time starts when the service sees the app (≤ about 5 s later), so an event "at X of use" is expected in the window **X to X + 15 s** on the stopwatch. "App gone" is expected by **end + 35 s**. Something **earlier** than X is a Fail.
- **How to measure grace cases**: lap **C** = the moment you press **Tab** or **Enter** in App B. Final warning ≤ **C + 15 s**. Close (dialog with the budget or break text) between **C + 60 s** and **C + 75 s**. App gone ≤ **C + 95 s**. A close **before C + 55 s** is a **Fail** (the kid did not get the full minute).
- **How to measure break cases**: by the **seconds in the kid's taskbar clock** (service PC time). Break start **T**. Early warning between **T − 2:00** and **T − 1:50**, final between **T − 1:00** and **T − 0:50**, break text ≤ **T + 0:10**, all apps gone ≤ **T + 0:30**.
- **Used time only counts while the kid's session is unlocked and in front.** Do not switch users or lock the PC during a timed case, except where a step says so.
- **Unexpected behaviour** that the story does not specify is a Note; TES routes it to PRO.

### Service log files as evidence

The service log is in the **admin-only** folder `%ProgramData%\EagleEye\logs\`, file `EagleEye.Service-NNN.log`. All log commands need an elevated terminal (Terminal (Administrator) or K-TERM).

- **LOG-COPY** at the end of the run (or a block) → `evidence/run-01-service-logs/`; **right after any Fail** → `evidence/run-01-service-logs/after-TC-006-NN/`. Look through the copies before committing (user names, program paths, SIDs; no secrets).
- **No log file**: *Ereignisanzeige → Windows-Protokolle → Anwendung*, source **EagleEye**; mark the case Fail.

**LOG-COPY** (elevated, in the repo root):

```powershell
Set-Location <your EagleEye repo folder>
$dst = Join-Path "02_Implementation\docs\testing\US-006\evidence" "<subfolder>"
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

Useful patterns: `Warning: account|Grace period|Blocked start|Closing app|App closed` (enforcement), `App rules of account|Warning settings changed` (changes), `time zone|clock of the PC` (AC-39).

Expected log lines (implementation report §8, plan "Logging"; wording differences are Notes, the content named in the ACs is the Pass criterion):

```text
App rules loaded: 0 rules of N apps of M accounts, 0 changed budget texts.
Warning levels loaded: early 5 min, final 1 min.
App rules of account eagleeye-kid (S-1-5-21-…) changed by Papas PC: app rule changed: Editor, 0:03, Mo Tu We Th Fr Sa Su (request …, revision N).
   … budget text changed (…) · … budget text reset to the default (…)
Warning settings changed by Papas PC: warning levels changed: early 2 min, final 1 min (request …, revision N).
   … early warning text changed (…) · … final warning text reset to the default (…)
New app for account eagleeye-kid: Systeminformationen (msinfo32.exe, …).
Blocked start: account eagleeye-kid, Rechner (…), process …, not allowed today (rule 0:00, no day, used 0:00:00); closing the app.
Blocked start: account eagleeye-kid, Editor (notepad.exe, …), process …, budget used up (rule 0:03, Mo Tu We Th Fr Sa Su, used 0:03:xx); closing the app.
Blocked start ended: account eagleeye-kid, Editor (…), budget used up: closed gracefully after 0.3 s; message shown.
Warning: account eagleeye-kid, Editor (notepad.exe), early warning, 2 min left (budget); message shown.
Warning: account eagleeye-kid, Editor (notepad.exe), final warning, 1 min left (budget); message shown (replaced the open message).
Warning: account eagleeye-kid, break time 15:10–23:59 (Mo Tu We Th Fr Sa Su) begins at 15:10, early warning, 2 min left; message shown.
Grace period: account eagleeye-kid, Zeichentabelle (charmap.exe): the rules changed (…); closes in 1 min at 15:31:05; message shown.
Grace period: account eagleeye-kid, break time 15:40–23:59 (…) …; 2 app(s) close in 1 min at 15:41:00; message shown.
Closing app: account eagleeye-kid, Editor (notepad.exe, …), budget used up.        (… , after grace period.)
App closed: account eagleeye-kid, Editor (notepad.exe, …), budget used up: closed gracefully after 0.4 s; message shown.
App closed: account eagleeye-kid, Paint (…), break time …: terminated by force after 20 s (20.3 s until gone); message shown.
   message states: shown · shown (replaced the open message) · not shown (tray client not connected) · not shown (tray client did not answer) · not shown (replaced in the same tick)
Purged the budget text of deleted account S-1-5-21-….
Warning: The time zone of the PC changed from W. Europe Standard Time (UTC+01:00) to … ; the local date changed from 10/17/2026 to 10/18/2026; daily budgets now use the new day; break times now use the new local time. Session in use: eagleeye-kid.
Warning: The clock of the PC changed from … to …; the local date stays 10/17/2026; break times now use the new local time.
```

Must **not** appear in this run: `Process … could not be terminated`, `could not be ended`, any `[ERR]` line.

### Test blocks — must-run first, optional last

| Block | Cases | Machine / accounts | Must / optional | Duration |
|---|---|---|---|---|
| Setup | S-1 to S-16 | Service PC, LEOSERV | — | ≈ 40 min (incl. two new accounts and the English language for `ee-kid2`) |
| **A** Upgrade, Rules and Settings pages | TC-006-01 to -10 must, -11 optional | Service PC / Admin; LEOSERV | 10 must + 1 optional | ≈ 50 min (+ 5 min) |
| **B** Start check and the first budget run | TC-006-12 to -17 | Service PC / kid1; App B | 6 must | ≈ 30 min |
| **C** Changes during the day (grace) | TC-006-18 to -20 must, -21, -22 optional | Service PC / kid1; App B | 3 must + 2 optional | ≈ 20 min (+ 10 min) |
| **D** Break times | TC-006-23 to -26 must, -27, -28 optional | Service PC / kid1; App B | 4 must + 2 optional | ≈ 35 min (+ 15 min) |
| **E** Other accounts, English, exemptions, tray | TC-006-29 to -32 | Service PC / ee-kid3, Admin, ee-kid2, kid1; App B | 4 must | ≈ 35 min |
| **F** Persistence, security, time zone, load, deletion | TC-006-33 to -37 must, -38 to -42 optional | Service PC / Admin, kid1, ee-kid2 | 5 must + 5 optional | ≈ 50 min (+ ≈ 45 min, TC-006-39 at midnight) |

**42 cases: 32 must-run, 10 optional.** Must-run total ≈ **4 h 20 min** including setup; all optional cases add ≈ 1 h 15 min. Run the blocks in this order; breaks between blocks are fine (but see the clock notes: Blocks B to D need the kid's session in front, and should not start after about **22:30**, because midnight is an end).

**A shorter run**: if you stop after Block D, the core enforcement is covered: blocking at start, warnings, graceful and forced close, grace periods, break begin. Blocks E and F hold the other must-run ACs (AC-18, AC-26, AC-27, AC-35, AC-36, AC-38, AC-39), the English check and the agent-token rerun.

**Optional coverage**: AC-12 (save error) rests **only** on optional TC-006-11, because the error cannot be produced without suspending the service. Every other AC has a must-run case. Optional cases add: grace ended by a raised rule (TC-006-21), locked time not counted (TC-006-22), break moved within the early level (TC-006-27), budget end before a break (TC-006-28), reboot (TC-006-38), midnight (TC-006-39), clock change (TC-006-40), the history in the database (TC-006-41), English parent app (TC-006-42).

## 3. Setup Instructions

**Before Block A** (service PC, Admin; **the kids do not need the PC**; ≈ 40 min):

1. **S-1 Installers present.** PowerShell in the repo root: `Get-FileHash 03_Delivery\windows\EagleEye-Setup-0.6.0.exe, 03_Delivery\windows\EagleEye-ParentApp-Setup-0.6.0.exe | Format-List Path, Hash`. Hashes as in §2.
2. **S-2 Installed state.** *Installierte Apps* → "EagleEye": expected **EagleEye 0.5.0** and **EagleEye Parent App 0.5.0**; `services.msc` → **EagleEye Service** *Wird ausgeführt*. Already 0.6.0, or no service → stop and tell TES.
3. **S-3 App A green**, *Gekoppelt*, device **Papas PC**.
4. **S-4 Deny by default** (§2): read it. App A → *Einstellungen*: note which accounts are ticked. Real kid accounts: untick them for the run, or plan to set their rules right after TC-006-04.
5. **S-5 Accounts.** Terminal (Administrator): `Get-LocalUser | Format-Table Name, Enabled -AutoSize`. `eagleeye-kid` must exist. Create `ee-kid2` and `ee-kid3`:
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
   Sign in once as each new account (*Benutzer wechseln*), wait for the desktop.
6. **S-6 English for `ee-kid2`** (**before the upgrade**, because *Einstellungen* is blocked for controlled accounts afterwards):
   - If English is not yet installed on the PC: in **your** session *Einstellungen → Zeit und Sprache → Sprache und Region → Sprache hinzufügen* → **English (United States)** → with **Sprachpaket**, **without** "Als Windows-Anzeigesprache festlegen".
   - As `ee-kid2`: *Einstellungen → Zeit und Sprache → Sprache und Region → Windows-Anzeigesprache* → **English (United States)** → sign out and sign in again. Check: the Start menu shows "Settings", "File Explorer".
   - Sign `ee-kid2` and `ee-kid3` **out**.
   - English not available → write it in the run file; TC-006-30 is then **Blocked**.
7. **S-7 `eagleeye-kid` clock seconds** (before the upgrade): as `eagleeye-kid`, *Einstellungen → Personalisierung → Taskleiste → Verhalten der Taskleiste* → **Sekunden in der Uhr der Taskleiste anzeigen** ✓ (probably still set from US-005). Close all apps and **sign `eagleeye-kid` out**.
8. **S-8 Display scaling** of the admin account = **150 %**.
9. **S-9 Terminal (Administrator)**: Start → **Terminal** → right-click → **Als Administrator ausführen**. Service commands: `Stop-Service` / `Start-Service` / `Restart-Service -DisplayName "EagleEye Service"`. A second Terminal (Administrator) with **LOG-WATCH**. `hostname` → `<host>`.
10. **S-10 Selection.** App A → *Einstellungen*: **eagleeye-kid** and **ee-kid2** ticked; **ee-kid3** and every other test account **unticked**.
11. **S-11 SmartScreen** on the unsigned installers: *Weitere Informationen* → *Trotzdem ausführen* (expected).
12. **S-12 K-TERM**: in a kid's session, Start → **Terminal** → right-click → **Als Administrator ausführen** → **your** admin user name and password. It runs as you, so it is never blocked or closed. Close it before you sign the kid out.
13. **S-13 App B on LEOSERV** (after TC-006-02): install `EagleEye-ParentApp-Setup-0.6.0.exe` on LEOSERV over 0.5.0, start it; green and *Gekoppelt* with `<host>`, otherwise pair as **LEOSERV**. Not available today → write it in the run file (TC-006-10 Blocked).
14. **S-14 Stopwatch** with lap times ready.
15. **S-15 SQLite tool** (only for optional TC-006-41): `sqlite3.exe` on the PATH, or *DB Browser for SQLite*. None → skip TC-006-41.
16. **S-16 All kid accounts signed out** (*Task-Manager → Benutzer*: only you).

**Before Block B** (kid1's session; ≈ 5 min):

- **P-1** TC-006-04 is done: kid1 has the start rule set (§2) and Settings shows early **2**, final **1**.
- **P-2** App B → **Berichte** → `eagleeye-kid` → today: write down any usage of **Editor**, **Zeichentabelle**, **Systeminformationen**, **Paint**, **Rechner** today (expected: none). Usage today shortens the budget: tell TES or shift the expected times by that amount. App B → **Regeln** → **Konto** `eagleeye-kid`, scroll so the **Applikations-Regeln** table is visible. Read and note kid1's **break display text** (**Anzeige Text:** under *Ruhezeiten*) — you will see it in Block D.
- **P-3** *Benutzer wechseln* → `eagleeye-kid`. Blocked autostart apps: **OK** (write them in Notes of TC-006-12).
- **P-4** **K-TERM** (S-12) with **LOG-WATCH**. For LOG-FIND, open a second tab in K-TERM (it is elevated too) or press *Strg+C* and restart LOG-WATCH afterwards.
- **P-5** No other app open in kid1's session (only K-TERM).

## 4. Acceptance Criteria Coverage

Cases in *italics* are optional.

| AC | Test case(s) |
|---|---|
| AC-1 | TC-006-02 |
| AC-2 | TC-006-02 |
| AC-3 | TC-006-02 (listed, no Explorer, empty account), TC-006-14 (blocked-only app) |
| AC-4 | TC-006-14 |
| AC-5 | TC-006-01, TC-006-02 (existing apps), TC-006-12, TC-006-14 (new app) |
| AC-6 | TC-006-08 (states), TC-006-09 (light/dark, 150 %) |
| AC-7 | TC-006-04 |
| AC-8 | TC-006-05 |
| AC-9 | TC-006-12 |
| AC-10 | TC-006-07, TC-006-30 (English budget and break defaults); *TC-006-42* |
| AC-11 | TC-006-04, TC-006-07 |
| AC-12 | *TC-006-11* only (see §2 "Optional coverage") |
| AC-13 | TC-006-03 |
| AC-14 | TC-006-03, TC-006-15 (German defaults in the dialog), TC-006-30 (English defaults); *TC-006-42* |
| AC-15 | TC-006-06, TC-006-07 |
| AC-16 | TC-006-04, TC-006-07 |
| AC-17 | TC-006-10 |
| AC-18 | TC-006-08 (app and service restart), TC-006-30 (kid1's rules not applied to ee-kid2), TC-006-33 (re-install), TC-006-37 (untick, deletion); *TC-006-38* (reboot) |
| AC-19 | TC-006-12 (default rule), TC-006-13 (day not ticked), TC-006-16 (budget used up) |
| AC-20 | TC-006-15 |
| AC-21 | TC-006-24 |
| AC-22 | TC-006-17 (final at start), TC-006-18 (early at start) |
| AC-23 | TC-006-15, TC-006-16 (countdown = Reports); *TC-006-22* (lock pauses), *TC-006-39* (midnight reset) |
| AC-24 | TC-006-15, TC-006-19 (no repeat after a raise, Q-5) |
| AC-25 | TC-006-15 (graceful), TC-006-16 (Reports), TC-006-17, TC-006-20 (force) |
| AC-26 | TC-006-29 |
| AC-27 | TC-006-31 |
| AC-28 | TC-006-23 |
| AC-29 | TC-006-23, TC-006-26 |
| AC-30 | TC-006-25 |
| AC-31 | TC-006-23 (break first); *TC-006-28* (budget first) |
| AC-32 | TC-006-18 (20 s left → 1 min), TC-006-19 (0:00 → grace; raise lets it start again), TC-006-20 (within the early level → early at once); *TC-006-21* (untick today, raised during grace) |
| AC-33 | TC-006-26; *TC-006-27* |
| AC-34 | TC-006-12 (info line, OK/Enter), TC-006-15 (replace), TC-006-18, TC-006-26 (grace info lines), TC-006-30 (English) |
| AC-35 | TC-006-32 |
| AC-36 | TC-006-33 (re-install, no parent app, used time kept); *TC-006-38* (reboot) |
| AC-37 | log checks in TC-006-12 to -26; *TC-006-41* (history in the database) |
| AC-38 | TC-006-36 (CPU with 10 allowed apps), TC-006-15 (start not slower: your judgement) |
| AC-39 | TC-006-35 (time zone); *TC-006-40* (clock) |

All 39 ACs are covered; 38 by at least one must-run case. Security check without an AC: TC-006-34 (TC-005-26 rerun, plan Q-4), together with the "closed gracefully" check of the Notepad budget close in TC-006-15.

**Only partly testable by hand**: AC-12 (needs a suspended service: optional), AC-17 "without polling" (not observable), AC-18 "becomes an admin account" (not tested, §1), AC-23 midnight and lock (optional), AC-34 exclusive full-screen games (known limit), AC-37 history kept 90 days (unit tests).

### What the English check proves (TC-006-30, plan R-10, report D-16)

The default texts live once, in a resource of `EagleEye.Shared`, with an English satellite file `en\EagleEye.Shared.resources.dll` next to each program (service, tray client, parent app). The program that **shows** a default picks the language (ADR-016). DEV only checked that the satellites are in the publish output, not in the installed program folders (D-16).

- If `ee-kid2`'s dialog shows the **English** default warning, budget and break texts, the **tray client's** `en` satellite was installed by `EagleEye-Setup-0.6.0.exe` and is loaded. The English **info line** also proves the tray client's own English texts.
- If it shows the **German** default texts with an English info line, the satellite is missing from the installed tray client (R-10). That is a **Fail**.
- It proves **nothing** about the **parent app's** satellite (`ParentApp\en\…`). Only an English parent app shows that (optional TC-006-42). The service's satellite is not visible at all. TC-006-07 step 4 shows that a typed English default is recognised as the default, but not whether the parent app or the service recognised it.

## 5. Test Cases

### Block A — Upgrade, Rules and Settings pages (must-run, except TC-006-11)

*Service PC · Admin. Start state: setup done; App A (0.5.0) green; all kid accounts signed out.*

#### TC-006-01: Service update 0.5.0 → 0.6.0 keeps pairing, selection and break times *(must-run)*

- **Verifies**: AC-5 (existing apps get the default rule), AC-18 (update); supporting: upgrade path
- **Machine / account**: Service PC / Admin (+ Terminal (Administrator))
- **Precondition**: S-1 to S-16; App A open and green

**Steps**

1. Run `03_Delivery\windows\EagleEye-Setup-0.6.0.exe` with defaults. Leave App A open.
2. Tray → *App Infos* (start **EagleEye Tray** if no icon). *Installierte Apps*; `services.msc`.
3. Watch App A up to 60 s. Log monitor: *Strg+C*, LOG-WATCH; LOG-FIND `App rules loaded|Warning levels loaded|Break times loaded|ERR`.

**Expected result**

- No error; *App Infos* **EagleEye_v0.6**; **EagleEye 0.6.0** once; service *Wird ausgeführt*.
- App A (still 0.5.0) green again within 60 s without a pairing code.
- Log: "App rules loaded: **0 rules** of N apps of M accounts, 0 changed budget texts." (N = number of apps recorded so far, M = controlled accounts) and "Warning levels loaded: early 5 min, final 1 min." "Break times loaded: …" lists the entries of US-005 again. No Error line.

#### TC-006-02: Parent app 0.6.0: the new Rules section; existing apps are blocked by default *(must-run)*

- **Verifies**: AC-1, AC-2, AC-3, AC-5
- **Machine / account**: Service PC / Admin
- **Precondition**: TC-006-01

**Steps**

1. Close App A. Run `03_Delivery\windows\EagleEye-ParentApp-Setup-0.6.0.exe` with defaults; start it. *Installierte Apps*: version.
2. **Regeln** → **Konto** `eagleeye-kid`. Scroll down past **Ruhezeiten** and its **Anzeige Text:**. Read the new section from top to bottom.
3. Look through the table: values, ticks, order. Is there a row **Windows-Explorer**? Hover over an app name (*note only*: does a tooltip show the program path, OQ-12?).
4. **Konto** → `ee-kid2`. Then back to `eagleeye-kid`.

**Expected result**

- **EagleEye Parent App 0.6.0**, green without a code.
- Step 2, below the break times section and its display text, top to bottom: heading bar **Applikations-Regeln** (accent colour, same style as **Ruhezeiten**); the explanation (§2), wrapped; the table; the label **Anzeige Text wenn Budget aufgebraucht:** with an edit box showing the default "Du hast deine Zeit aufgebraucht! Leider ist jetzt Schluss.".
- Columns: **Applikation** · **Tägliches Zeit-Budget** over **Stunden** and **Minuten** (input fields) · **MO DI MI DO FR SA SO** (checkboxes).
- Step 3: one row per app that `eagleeye-kid` has ever used (the same apps as on *Berichte*, e.g. Editor, Paint, Microsoft Edge), **every row 0 / 0 and no day ticked**; sorted alphabetically, upper and lower case ignored; **no** Windows-Explorer row.
- Step 4: `ee-kid2` shows **Noch keine Applikationen aufgezeichnet.** and the default budget text; switching back shows kid1's table again.

#### TC-006-03: Settings: the new section "Zeit-Ende Warnungen" with its defaults *(must-run)*

- **Verifies**: AC-13, AC-14
- **Machine / account**: Service PC / Admin
- **Precondition**: TC-006-02

**Steps**

1. **Einstellungen**. Scroll below **Benutzerkonten auf dem EagleEye-PC**. Read the new section.
2. Try to edit the labels **Frühe Warnung** / **Finale Warnung**. Look for add, delete or account controls.

**Expected result**

- Heading bar **Zeit-Ende Warnungen**; the explanation "Hier kannst du 2 Zeitpunkte einstellen, …", wrapped; a table **Zeitpunkt** · **Minuten vor Ende** · **Anzeigetext** with exactly two rows:
  - **Frühe Warnung** · **5** · "Achtung, du hast nur noch 5 Minuten! Speichere deinen Fortschritt."
  - **Finale Warnung** · **1** · "Achtung!!! Nur noch 1 Minute – jetzt aber schnell speichern!"
- The labels cannot be edited. No add or delete button, no account selection.

#### TC-006-04: Setting the test rules and warning levels: edited in place, saved at once, logged *(must-run; this is the preparation of Blocks B to F)*

- **Verifies**: AC-7, AC-11, AC-16
- **Machine / account**: Service PC / Admin; log monitor
- **Precondition**: **Regeln** → **Konto** `eagleeye-kid`

**Steps** (watch the log monitor after each step; there is no "Save" button)

1. Row **Editor**: **Minuten** → select, type `3`, press **Tab**.
2. Row **Editor**: tick **MO**, **DI**, **MI**, **DO**, **FR**, **SA**, **SO** one after the other.
3. Row **Paint**: **Minuten** `30`, press **Enter**. Tick **SA** (on a Saturday: **SO**). Then tick **MO** and untick it again.
4. Row **Rechner**: do not touch (default).
5. **Einstellungen** → **Frühe Warnung** → **Minuten vor Ende** `2`, **Tab**.

**Expected result**

- Each change is saved by itself, one log line per change within 5 s, with the account's user name, e.g. "App rules of account eagleeye-kid (S-1-5-21-…) changed by Papas PC: app rule changed: Editor, 0:03, no day", then "… Editor, 0:03, Mo", … "… Editor, 0:03, Mo Tu We Th Fr Sa Su"; "… Paint, 0:30, no day", "… Paint, 0:30, Sa" (or "Su"), "… Paint, 0:30, Mo Sa", "… Paint, 0:30, Sa".
- The fields show what you entered (the state the service confirmed).
- Step 5: "Warning settings changed by Papas PC: warning levels changed: early 2 min, final 1 min (…)."
- Kid1's rules now match §2 "Rule set".

#### TC-006-05: Invalid hours and minutes are not saved; 23:59 is the largest budget *(must-run)*

- **Verifies**: AC-8
- **Machine / account**: Service PC / Admin; log monitor
- **Precondition**: Paint = 0:30 (TC-006-04)

**Steps** (after each step: field, message line, log)

1. Paint **Stunden**: type `24`, **Tab**. Then `-1`, **Tab**. Then try `abc`, **Tab**. Then delete the content (empty), **Tab**.
2. Paint **Minuten**: type `60`, **Tab**. Then `1.5`, **Tab**.
3. Paint **Stunden** `23`, **Tab**; **Minuten** `59`, **Tab**.
4. Paint back: **Stunden** `0`, **Tab**; **Minuten** `30`, **Tab**.

**Expected result**

- Step 1: each time the field returns to **0**, the message **Bitte Stunden zwischen 0 und 23 eingeben.** appears, and **no** log line. (`abc`: letters not accepted is a Pass too; `-1`: the minus sign not accepted is a Pass too.)
- Step 2: the field returns to **30**; **Bitte Minuten zwischen 0 und 59 eingeben.**; no log line.
- Step 3: saved: "… Paint, 23:30, Sa" then "… Paint, 23:59, Sa".
- Step 4: saved; Paint is back at 0:30.

#### TC-006-06: Invalid warning minutes and the rule "final less than early" *(must-run)*

- **Verifies**: AC-15
- **Machine / account**: Service PC / Admin; log monitor
- **Precondition**: **Einstellungen**, early 2 / final 1

**Steps**

1. **Frühe Warnung** minutes: `0`, **Tab**. Then `60`, **Tab**. Then try `abc`, then `2.5`, then empty, each with **Tab**.
2. **Finale Warnung** minutes: `2`, **Tab**. Then `3`, **Tab**.
3. **Frühe Warnung** minutes: `1`, **Tab**.

**Expected result**

- Step 1: each time the field returns to **2**; **Bitte Minuten zwischen 1 und 59 eingeben.**; no log line.
- Steps 2 and 3: the field returns to the stored value (1, or 2); **Die finale Warnung muss weniger Minuten vor dem Ende liegen als die frühe Warnung.**; no log line.
- Afterwards still early **2**, final **1**.

#### TC-006-07: Budget text and warning texts: line break, emojis, empty text, typed default *(must-run)*

- **Verifies**: AC-10, AC-11, AC-15 (texts), AC-16
- **Machine / account**: Service PC / Admin; log monitor
- **Precondition**: **Regeln** → `eagleeye-kid`

**Steps**

1. Budget text box (**Anzeige Text wenn Budget aufgebraucht:**): select all, type `Budget leer! ` and insert **🛑** with the emoji panel (**Windows + .**). Press **Enter** (new line), type `Morgen geht's weiter ` and paste **👍**. Click outside the box.
2. **Berichte**, then **Regeln** again (`eagleeye-kid`).
3. Click into the box, delete the whole text, click outside.
4. Type the **English** default exactly: `You have used up your time! Sorry, that's it for now.` Click outside.
5. Enter the text of step 1 again (two lines, 🛑 👍); click outside. **It stays for the rest of the run.**
6. **Einstellungen** → **Finale Warnung** text: select all, type `Schnell speichern! 💾`, **Tab**. Then delete it (empty), **Tab**.
7. *Optional*: paste a text of more than 500 characters into the budget text box.

**Expected result**

- Step 1: the box shows two lines with the emojis; log "… budget text changed" within 5 s. The text itself is **not** in the log.
- Step 2: the same two lines and emojis.
- Step 3: the box shows the **German default** again; log "… budget text reset to the default".
- Step 4: the box shows the **German** default again (the English default is stored as "default"); log "… budget text reset to the default" (or no line if nothing changed: then write it in Notes).
- Step 5: two lines again; "budget text changed".
- Step 6: "Warning settings changed by Papas PC: final warning text changed", then "… final warning text reset to the default"; the box shows the German default again.
- Step 7 (optional): the box stops accepting characters at about 500.

#### TC-006-08: Page states; rules, texts and levels survive a parent app restart and a service restart *(must-run)*

- **Verifies**: AC-6 (states), AC-18 (restarts)
- **Machine / account**: Service PC / Admin, Terminal (Administrator)
- **Precondition**: **Regeln** → `eagleeye-kid`

**Steps**

1. `Stop-Service -DisplayName "EagleEye Service"`. Watch the page until the status bar is red.
2. `Start-Service -DisplayName "EagleEye Service"`. Watch the page. (*Note only*: was **Wird geladen …** visible?) LOG-WATCH again.
3. Close App A (window X) and start it. **Regeln** → `eagleeye-kid`; **Einstellungen**.
4. *Einstellungen*: untick **eagleeye-kid** and **ee-kid2** (and any other ticked account; tick it again afterwards). **Regeln**. Then tick both again; **Regeln** → `eagleeye-kid`.

**Expected result**

- Step 1: no crash; **Keine Daten verfügbar**; no table, no edit box, nothing editable.
- Step 2: green within 60 s; the section shows the stored rules (Editor 0:03 all days, Paint 0:30 SA/SO) and kid1's two-line budget text.
- Step 3: the same; Settings early 2 / final 1 with the default texts.
- Step 4: with both unticked, within 60 s **Keine Konten unter Elternkontrolle. Konten unter Einstellungen auswählen.**; after ticking again, the rules and text are **unchanged** (kept while unticked).

#### TC-006-09: Light and dark mode, 150 % scaling *(must-run)*

- **Verifies**: AC-6
- **Machine / account**: Service PC / Admin (150 %)
- **Precondition**: **Regeln** → `eagleeye-kid`

**Steps**

1. Look at the **Applikations-Regeln** section; screenshot → `evidence/run01-tc09-rules-<mode>.png`. **Einstellungen** → **Zeit-Ende Warnungen**; screenshot.
2. *Einstellungen → Darstellung*: switch to the other mode (Hell ↔ Dunkel). Repeat step 1. Switch back.

**Expected result**

- In both modes: all texts, input fields, the seven checkbox columns and both heading bars are clearly visible; only the app's normal colours plus the accent bars (not the colours of the mockup).
- At 150 %: no text cut off, column headers above their columns, the seven checkboxes do not overlap, the explanations wrap.

#### TC-006-10: Two parent apps stay in sync; the last change wins *(must-run, needs LEOSERV)*

- **Verifies**: AC-17
- **Machine / account**: Service PC / Admin (App A) and LEOSERV (App B) side by side
- **Precondition**: S-13 done. Both apps on **Regeln** → `eagleeye-kid`. No LEOSERV → **Blocked**

**Steps**

1. A: Paint **Stunden** `1`, **Tab**. Watch B.
2. B: Paint tick **DO** (on a Thursday: **MI**). Watch A.
3. A: untick that day again. Watch B.
4. A and B at the same moment (count down): A types Paint **Minuten** `20`, B types `40`; both press **Enter** together. Wait 5 s.
5. A: Paint **Stunden** `0`, **Minuten** `30`, **Tab** (back to 0:30).
6. Both apps → **Einstellungen**. A: **Frühe Warnung** text `Gleich ist Schluss ⏰`, **Tab**. Watch B. A: empty it, **Tab**. Watch B.

**Expected result**

- Steps 1 to 3, 5, 6: each change appears in the other app **≤ 5 s**, without any action there.
- Step 4: after 5 s both apps show the **same** minutes (20 or 40).
- At the end: Paint 0:30, SA (or SO) only; early warning text back to the default in both apps.

#### TC-006-11 (optional): The service cannot store a change: message, field returns *(optional)*

- **Verifies**: AC-12
- **Machine / account**: Service PC / Admin, Terminal (Administrator)
- **Precondition**: all kid accounts signed out (then only one `EagleEye.Service.exe` exists). **Regeln** → `eagleeye-kid`

**Steps**

1. Terminal (Administrator): `resmon` → tab **CPU** → right-click `EagleEye.Service.exe` → **Prozess anhalten**.
2. Within 10 s: App A, Paint **Minuten** `31`, **Tab**. Wait up to 10 s.
3. At the latest 25 s after step 1: `resmon` → right-click → **Prozess fortsetzen**. Wait 30 s.
4. Paint back to `30` if it shows 31.

**Expected result**

- Step 2: **Die Änderung konnte nicht gespeichert werden. Bitte erneut versuchen.**; the field returns to **30**.
- Step 3: App A green. Paint shows the value of the last Paint log line (30, or 31 if the service stored the queued change after it continued: both Pass, write which in Notes).

### Block B — Start check and the first budget run (must-run)

*Service PC, in kid1's session (`eagleeye-kid`); K-TERM with LOG-WATCH; App B on LEOSERV → **Regeln** → `eagleeye-kid`, table visible.*

> **Before Block B**: P-1 to P-5 (§3).

#### TC-006-12: An app with the default rule is blocked at start; the kid dialog shows the budget text and the info line *(must-run)*

- **Verifies**: AC-19 (default rule), AC-5, AC-9, AC-34 (info line, OK/Enter only), AC-37 (log)
- **Machine / account**: Service PC / kid1, K-TERM; App B
- **Precondition**: Rechner default (0:00, no day); no dialog open; stopwatch ready

**Steps**

1. Windows+R → `calc`; **start the stopwatch on Enter**. **Lap 1** when the EagleEye dialog appears; **lap 2** when Rechner's window is gone.
2. Read the dialog. Press **Esc**; press **Alt+F4**; click on the desktop behind it. Then click into the dialog and press **Enter**.
3. App B: Rechner tick all seven days (budget stays 0:00). Wait 5 s. Windows+R → `calc`. Read the dialog; **OK**.
4. App B: untick all seven days of Rechner again (default).
5. K-TERM: LOG-FIND `Rechner`.

**Expected result**

- Step 1: dialog **≤ 10 s** after Enter (expect 1 to 6 s); Rechner gone **≤ 30 s**.
- Step 2: title **EagleEye**; kid1's budget text **exactly** as stored ("Budget leer! 🛑" / "Morgen geht's weiter 👍"); a separator; the info line **"Rechner: heute nicht erlaubt."**; button **OK**. Esc, Alt+F4 and the click do **not** close it; Enter closes it.
- Step 3: blocked again (AC-9: 0:00 blocks whatever days are ticked), same texts.
- Log: "Blocked start: account eagleeye-kid, Rechner (…), process …, not allowed today (rule 0:00, no day, used 0:00:00); closing the app." and, for step 3, "… (rule 0:00, Mo Tu We Th Fr Sa Su, …)"; "Blocked start ended: … closed gracefully after … s | terminated by force after 20 s …; message shown." (both outcomes accepted for this Store app; write which). App rule lines for the ticks.

#### TC-006-13: An app whose day is not ticked is blocked *(must-run)*

- **Verifies**: AC-19 (not allowed today)
- **Machine / account**: Service PC / kid1, K-TERM
- **Precondition**: Paint 0:30, only SA (on a Saturday: only SO), so today is not ticked

**Steps**

1. Windows+R → `mspaint`; stopwatch from Enter; laps as in TC-006-12. **OK**.
2. LOG-FIND `Paint`.

**Expected result**

- Dialog ≤ 10 s with kid1's budget text and **"Paint: heute nicht erlaubt."**; Paint gone ≤ 30 s.
- Log: "Blocked start: … Paint (mspaint.exe, …), … not allowed today (rule 0:30, Sa, used 0:00:00) …" (or "Su") and "Blocked start ended: … closed gracefully …".

#### TC-006-14: A new app is blocked at its first start and appears live in the Rules table *(must-run)*

- **Verifies**: AC-4, AC-3 (an app whose starts were only ever blocked), AC-5 (default for new apps)
- **Machine / account**: Service PC / kid1, K-TERM; App B in view
- **Precondition**: App B shows kid1's table; **Systeminformationen** is not listed (if it is, use `dxdiag` / **DirectX-Diagnoseprogramm** in this case and in TC-006-18 instead)

**Steps**

1. Windows+R → `msinfo32`; stopwatch from Enter. **OK** in the dialog.
2. Watch App B (do not touch it): note the stopwatch when the row appears.
3. LOG-FIND `Systeminformationen|msinfo32`.

**Expected result**

- Step 1: blocked (dialog with the budget text and "Systeminformationen: heute nicht erlaubt.", app gone ≤ 30 s).
- Step 2: within **15 s** a row **Systeminformationen** appears in App B, in its alphabetical place, with **0 / 0** and **no day ticked**.
- Log: "New app for account eagleeye-kid: Systeminformationen (msinfo32.exe, …)." and "Blocked start: … not allowed today (rule 0:00, no day, …)".

#### TC-006-15: Notepad with 0:03: starts, early and final warning, closed gracefully at the end with the budget text *(must-run, the core case)*

- **Verifies**: AC-20, AC-23, AC-24, AC-25, AC-34 (a newer message replaces the text), AC-14 (German defaults in the dialog), AC-37; AC-38 (start not slower: your judgement); security: Notepad budget close "closed gracefully" (plan Q-4)
- **Machine / account**: Service PC / kid1, K-TERM
- **Precondition**: Editor 0:03, all days; no Editor usage today (P-2); no dialog open. **Stay in kid1's session; do not lock the PC.**

**Steps**

1. Windows+R → `notepad`; **stopwatch on Enter**. Type **nothing** into Notepad.
2. **Lap 1** when the first dialog appears. Read it. **Leave it open.**
3. **Lap 2** when the dialog's text changes. Read it. **Leave it open.**
4. **Lap 3** when the text changes again; **lap 4** when Notepad's window is gone. Read the dialog; **OK**.
5. K-TERM: LOG-FIND `Editor`.

**Expected result**

- Step 1: Notepad opens as quickly as before and stays open (not noticeably slower: your judgement); no dialog at start (3 minutes left > early level 2).
- Lap 1 between **1:00 and 1:15**: the **early** warning "Achtung, du hast nur noch 5 Minuten! Speichere deinen Fortschritt." with the info line **"Editor: noch 2 Minuten."**
- Lap 2 between **2:00 and 2:15**: the **same** dialog now shows the **final** warning "Achtung!!! Nur noch 1 Minute – jetzt aber schnell speichern!" with **"Editor: noch 1 Minute."** No second dialog (one dialog per session).
- Lap 3 between **3:00 and 3:15**: kid1's budget text (two lines, 🛑 👍) with **"Editor: Zeit-Budget aufgebraucht."**; lap 4 ≤ **3:35**.
- Each warning appears **once** (no repeat).
- Log: "Warning: account eagleeye-kid, Editor (notepad.exe), early warning, 2 min left (budget); message shown.", "… final warning, 1 min left (budget); message shown (replaced the open message).", "Closing app: account eagleeye-kid, Editor (notepad.exe, …), budget used up." and "App closed: … budget used up: **closed gracefully** after 0.x s; message shown (replaced the open message)" (or "shown").
- **Q-4**: the outcome must be **closed gracefully**. "terminated by force after 20 s" for Notepad without text is a **Fail**.

#### TC-006-16: Notepad again the same day: blocked, budget used up; Reports matches *(must-run)*

- **Verifies**: AC-19 (budget used up), AC-23 (used time = Reports), AC-25 (Reports up to about 30 s more)
- **Machine / account**: Service PC / kid1, K-TERM; App B
- **Precondition**: TC-006-15 done

**Steps**

1. Windows+R → `notepad`; stopwatch. **OK** in the dialog.
2. App B → **Berichte** → `eagleeye-kid` → today: read **Editor**. Back to **Regeln**.
3. LOG-FIND `Blocked start.*Editor`.

**Expected result**

- Step 1: blocked (dialog ≤ 10 s with the budget text and **"Editor: Zeit-Budget aufgebraucht."**, gone ≤ 30 s).
- Step 2: Editor today ≈ **3 minutes** (3:00 to about 3:35; plus any usage of P-2). The blocked start of step 1 added nothing (compare after 1 minute: unchanged).
- Log: "Blocked start: … Editor (notepad.exe, …), … budget used up (rule 0:03, Mo Tu We Th Fr Sa Su, used 0:03:xx); closing the app."

#### TC-006-17: A budget at or below the final level shows the final warning at start; closed at the end *(must-run)*

- **Verifies**: AC-22 (final warning at start), AC-25
- **Machine / account**: Service PC / kid1, K-TERM; App B
- **Precondition**: **Zeichentabelle** is listed in App B. If not: Windows+R → `charmap` once (blocked, the row appears), **OK**. App B: Zeichentabelle **Minuten** `1`, **Tab**, tick all seven days. Wait 5 s.

**Steps**

1. Windows+R → `charmap`; stopwatch on Enter. Lap 1 when the dialog appears; read it; **leave it open**.
2. Lap 2 when the text changes; lap 3 when Zeichentabelle is gone. **OK**.

**Expected result**

- Lap 1 **≤ 0:15**: the **final** warning (default text) with **"Zeichentabelle: noch 1 Minute."**; **no** early warning at all.
- Lap 2 between **1:00 and 1:15**: the budget text with "Zeichentabelle: Zeit-Budget aufgebraucht."; lap 3 ≤ **1:35**.
- Log: "Warning: … Zeichentabelle (charmap.exe), final warning, 1 min left (budget); message shown." and "App closed: … budget used up: closed gracefully …".

### Block C — Changes during the day: grace periods (TC-006-18 to -20 must-run)

*Service PC, kid1's session in front; K-TERM; App B on LEOSERV → **Regeln** → `eagleeye-kid`. App B makes all changes; press **Tab** or **Enter** to send a field.*

#### TC-006-18: A change that leaves 20 seconds gives the full grace minute *(must-run)*

- **Verifies**: AC-22 (early warning at start), AC-32 (grace, Q-2: "the kid still gets 1 minute, not 20 seconds"), AC-34 (grace info line), AC-37
- **Machine / account**: Service PC / kid1, K-TERM; App B
- **Precondition**: App B: Systeminformationen **Minuten** `2`, **Tab**, tick all seven days. Wait 5 s. No dialog open.

**Steps**

1. Windows+R → `msinfo32`; **stopwatch on Enter**. Lap when the dialog appears; read it; **OK**.
2. At stopwatch **0:40**: App B → Systeminformationen **Minuten** `1`, **Tab** → **lap C** at the Tab.
3. Lap when the dialog appears; read it; **leave it open**.
4. Lap when the text changes; lap when Systeminformationen is gone. **OK**.
5. LOG-FIND `Systeminformationen|Grace period`.

**Expected result**

- Step 1: dialog **≤ 0:15**: the **early** warning (default) with **"Systeminformationen: noch 2 Minuten."** (2 minutes left = at the early level).
- Step 3: ≤ **C + 15 s**: the **final** warning text with the info line **"Systeminformationen: Deine Eltern haben die Regeln geändert. Systeminformationen wird in 1 Minute geschlossen."** (the full minute, although only 20 s were left).
- Step 4: the budget text with "… Zeit-Budget aufgebraucht." between **C + 60 s and C + 75 s**; gone ≤ **C + 95 s**. **Closed at about stopwatch 1:00 (C + 20 s) is a Fail.**
- Log: "App rules … app rule changed: Systeminformationen, 0:01, …", "Grace period: account eagleeye-kid, Systeminformationen (msinfo32.exe): the rules changed (…); closes in 1 min at HH:MM:SS; message shown.", "Closing app: … budget used up, after grace period." and "App closed: …".

#### TC-006-19: A raised budget lets the app start again without repeating warnings; 0:00 while running gives a grace minute *(must-run)*

- **Verifies**: AC-32 (raise lets it start again; 0:00 while running → grace), AC-24 (no warning repeated after a raise, Q-5), AC-34
- **Machine / account**: Service PC / kid1, K-TERM; App B
- **Precondition**: TC-006-17 done (Zeichentabelle used ≈ 1 minute, both warning levels already shown today). App B → **Berichte**: note Zeichentabelle today (**Z0**, ≈ 1:00 to 1:35).

**Steps**

1. App B → **Regeln**: Zeichentabelle **Minuten** `3`, **Tab**. Wait 5 s. (≈ 1:30 to 2:00 left: that is at or below the early level.)
2. Windows+R → `charmap`; stopwatch. Watch 20 s.
3. At stopwatch **0:20**: App B → Zeichentabelle **Minuten** `0`, **Tab** → **lap C**.
4. Laps as in TC-006-18 (final warning, close, gone). **OK**.

**Expected result**

- Step 2: Zeichentabelle starts and stays open (the raised budget allows it again). **No** warning at start, although less than 2 minutes are left: both levels were already shown today (Q-5).
- Step 4: ≤ **C + 15 s** the final warning with **"Zeichentabelle: Deine Eltern haben die Regeln geändert. Zeichentabelle wird in 1 Minute geschlossen."** (the grace warning is the one exception to "once per day"); budget text between **C + 60 s and C + 75 s**; gone ≤ **C + 95 s**.
- Log: "Grace period: … Zeichentabelle (charmap.exe): the rules changed (…); closes in 1 min …" and "Closing app: …, after grace period."

#### TC-006-20: A change within the early level shows the early warning at once (no grace); Paint with an unsaved drawing is ended by force *(must-run)*

- **Verifies**: AC-32 (second bullet: early warning at once, no grace), AC-25 (force after 20 s, all processes), AC-24
- **Machine / account**: Service PC / kid1, K-TERM; App B
- **Precondition**: App B: Paint tick **today**. Paint is 0:30 and has no usage today (it was only blocked). Wait 5 s.

**Steps**

1. Windows+R → `mspaint`; **stopwatch on Enter**. Draw a line. **Do not save.**
2. At stopwatch **0:20**: App B → Paint **Minuten** `2`, **Tab** → **lap C**. (1:40 left: between final and early.)
3. Lap when the dialog appears; read it; **leave it open**.
4. Lap when the text changes (final warning); leave it open.
5. When Paint shows its **"save?" prompt**: **lap P**. **Do not answer it.** Lap when Paint is gone. **OK**.
6. K-TERM: `Get-Process mspaint -ErrorAction SilentlyContinue`; LOG-FIND `Paint`.

**Expected result**

- Step 3: ≤ **C + 15 s** the **early** warning with **"Paint: noch 2 Minuten."** (no "Deine Eltern haben die Regeln geändert …": this is not a grace period).
- Step 4: the final warning with "Paint: noch 1 Minute." between stopwatch **1:00 and 1:15**.
- Step 5: the save prompt and the budget text between **2:00 and 2:15** (the end of the 2-minute budget, **not** C + 60 s); Paint gone **20 s (±2)** after lap P, and ≤ **2:35**.
- Step 6: no `mspaint` process. Log: "Warning: … Paint …, early warning, 2 min left (budget) …", "Closing app: … Paint …, budget used up." (no "after grace period"), "App closed: … Paint …, budget used up: **terminated by force after 20 s** (2x.x s until gone); message shown …". No "Grace period" line for Paint, no "could not be terminated".

#### TC-006-21 (optional): Today unticked while running → grace; ticked again during the grace → the app keeps running *(optional)*

- **Verifies**: AC-32 ("no longer allowed today"; a change that gives time back during the grace, report D-8), AC-24 (no warning brought back)
- **Machine / account**: Service PC / kid1; App B
- **Precondition**: App B: Zeichentabelle **Minuten** `10`, **Tab** (≈ 7 minutes left)

**Steps**

1. Windows+R → `charmap`. Wait 20 s.
2. App B: untick **today** for Zeichentabelle → **lap C**. Read the dialog; **OK**.
3. At **C + 20 s**: App B: tick today again.
4. Watch until **C + 2:00**. Then close Zeichentabelle yourself.

**Expected result**

- Step 2: ≤ C + 15 s the final warning with "Zeichentabelle: Deine Eltern haben die Regeln geändert. Zeichentabelle wird in 1 Minute geschlossen."
- Step 4: Zeichentabelle is **not** closed (the natural time left is longer than the grace); **no** new warning. Log: one "Grace period" line, no "Closing app" for Zeichentabelle.

#### TC-006-22 (optional): Locked time does not count *(optional)*

- **Verifies**: AC-23 (countdown pauses while the session is locked)
- **Machine / account**: Service PC / kid1; App B
- **Precondition**: Windows+R → `mstsc` once (blocked, the row **Remotedesktopverbindung** appears), **OK**. App B: Remotedesktopverbindung **Minuten** `2`, all days. Wait 5 s.

**Steps**

1. Windows+R → `mstsc`; stopwatch on Enter. **OK** in the early warning.
2. At **0:30**: **Windows+L**. Wait until stopwatch **1:30**. Unlock.
3. Lap at the final warning; lap when the app is gone.

**Expected result**

- The final warning comes at about **2:00** (1:00 of use + 1:00 locked), not at 1:00; closed at about **3:00** (± 15 s).

### Block D — Break times (TC-006-23 to -26 must-run)

*Service PC, kid1's session in front (seconds in the clock); K-TERM; App B → **Regeln** → `eagleeye-kid`.*

> **Before Block D** (App B): Paint **Stunden** `1`, **Minuten** `0`, today ticked. Zeichentabelle **Stunden** `1`, **Minuten** `0`, all days. Editor **Stunden** `23`, **Minuten** `59` (all days). Under **Ruhezeiten**: use the existing entry **E1** of US-005 (or **Eintrag hinzufügen**): **An/Aus** off, **End-Zeit** 23:59, all days ticked. Close every app in kid1's session except K-TERM.
>
> **BREAK ON** = tick **An/Aus** of E1; **BREAK OFF** = untick it. Wait until the tick is confirmed (it stays, no message).

#### TC-006-23: A break begins: early and final break warnings once, then every running app is closed *(must-run; replaces US-005 AC-25)*

- **Verifies**: AC-28, AC-29, AC-31 (the break is the earlier end: no budget warnings), AC-34, AC-37
- **Machine / account**: Service PC / kid1, K-TERM; App B
- **Precondition**: as above; no dialog open

**Steps**

1. Read kid1's clock. **T** = current minute + **4** (e.g. at 15:06:xx → T = 15:10). App B: E1 **Start-Zeit** `T`, **Enter**; **BREAK ON**.
2. kid1: Windows+R → `charmap`. Windows+R → `mspaint`, draw a line, **do not save**. Leave both open.
3. At **T − 2:00**: watch for the dialog (note the clock); read it; **leave it open**.
4. At **T − 1:00**: watch for the text change (note the clock).
5. At **T**: watch the dialog and both apps. Paint shows its "save?" prompt: do not answer. Note the clock when each app is gone. **OK**.
6. LOG-FIND `break time|App closed`.

**Expected result**

- Step 3: between **T − 2:00 and T − 1:50**: **one** dialog with the early warning (default text) and **"Die Ruhezeit beginnt um T."** (e.g. "um 15:10").
- Step 4: between **T − 1:00 and T − 0:50**: the same dialog shows the final warning with "Die Ruhezeit beginnt um T." No second dialog; no warning per app.
- Step 5: ≤ **T + 0:10** the dialog shows kid1's **break display text** (P-2) **once**, with an info line about the break (e.g. "Ruhezeit von T bis 24:00."). Zeichentabelle closed gracefully; Paint ended by force about 20 s after its prompt; **both gone ≤ T + 0:30**. (US-005 kept running apps open; from US-006 on they are closed.)
- **No** budget warning ("noch 2 Minuten" for Paint or Zeichentabelle): both have about an hour left, so the break is their end (AC-31).
- Log: "Warning: account eagleeye-kid, break time T–23:59 (Mo Tu We Th Fr Sa Su) begins at T, early warning, 2 min left; message shown.", the same with "final warning, 1 min left", "App closed: … Zeichentabelle …, break time T–23:59 (…): closed gracefully …" and "App closed: … Paint …, break time …: terminated by force after 20 s (…)". No budget "Warning: … (budget)" line for these two apps.

#### TC-006-24: During a break, starts are blocked with the break text, even with 23:59 *(must-run)*

- **Verifies**: AC-21
- **Machine / account**: Service PC / kid1, K-TERM
- **Precondition**: break E1 in effect (after TC-006-23); no dialog open

**Steps**

1. Windows+R → `notepad` (Editor has 23:59, all days). Read the dialog; **OK**.
2. Windows+R → `calc` (default rule). Read the dialog; **OK**.
3. LOG-FIND `Blocked start`.

**Expected result**

- Both blocked (dialog ≤ 10 s, gone ≤ 30 s). The dialog shows kid1's **break display text**, **not** the budget text, with a break info line (e.g. "Ruhezeit von T bis 24:00.").
- Log: "Blocked start: … Editor …, break time T–23:59 (…); closing the app." and the same for Rechner (reason break time, not "not allowed today").

#### TC-006-25: An app started shortly before a break shows the warning at once *(must-run)*

- **Verifies**: AC-30, AC-29
- **Machine / account**: Service PC / kid1; App B
- **Precondition**: App B **BREAK OFF**. No app open except K-TERM.

**Steps**

1. Read the clock. **T2** = current minute + **4**. App B: E1 **Start-Zeit** `T2`, **Enter**; **BREAK ON**.
2. Wait until the clock shows **T2 − 1:45** (e.g. 15:18:15 for T2 = 15:20). Windows+R → `charmap`. Read the dialog; **leave it open**.
3. Note the clock at the text change and when Zeichentabelle is gone. **OK**.

**Expected result**

- Step 2: ≤ 10 s after the start: the **early** warning with "Die Ruhezeit beginnt um T2." (1:45 left: below early, above final).
- Step 3: the final warning between **T2 − 1:00 and T2 − 0:50**; at T2 the break text; Zeichentabelle gone ≤ **T2 + 0:30**.

#### TC-006-26: A break switched on now: every running app gets one grace minute; new starts are blocked at once *(must-run)*

- **Verifies**: AC-33, AC-29, AC-34 (break grace info line), AC-37
- **Machine / account**: Service PC / kid1, K-TERM; App B
- **Precondition**: App B **BREAK OFF**. kid1: Windows+R → `charmap`; Windows+R → `mspaint` (no drawing). Both open, no dialog.

**Steps**

1. App B: E1 **Start-Zeit** = **now − 1 min**, **Enter** (still off). Then **BREAK ON** → **lap C** at the click.
2. Read the dialog; **leave it open**.
3. At about **C + 20 s**: Windows+R → `notepad`.
4. Watch Zeichentabelle and Paint; lap when the break text appears and when both are gone. **OK**.
5. App B **BREAK OFF**. LOG-FIND `Grace period|App closed|Blocked start`.

**Expected result**

- Step 2: ≤ **C + 15 s** **one** dialog: the final warning with **"Deine Eltern haben eine Ruhezeit eingestellt. Alle Apps werden in 1 Minute geschlossen."** (once, not per app).
- Step 3: Notepad is blocked at once (gone ≤ 30 s), the dialog text changes to the break text. Zeichentabelle and Paint **stay open**.
- Step 4: between **C + 60 s and C + 75 s** the break text; Zeichentabelle and Paint closed (gracefully), both gone ≤ **C + 95 s**.
- Log: "Grace period: account eagleeye-kid, break time … ; 2 app(s) close in 1 min at HH:MM:SS; message shown.", "Blocked start: … Editor …, break time …", "Closing app: …, after grace period." for both, "App closed: …".

#### TC-006-27 (optional): A break moved to begin within the early level shows the early warning at once (no grace) *(optional)*

- **Verifies**: AC-33 (last paragraph)
- **Machine / account**: Service PC / kid1; App B
- **Precondition**: **BREAK OFF**; Zeichentabelle open in kid1's session

**Steps**

1. When kid1's clock shows between **:00 and :10 seconds**, App B: E1 **Start-Zeit** = current minute + **2** (begins in 1:50 to 2:00), **Enter**; **BREAK ON** → lap C.
2. Watch the dialogs until the break begins. **OK**. **BREAK OFF**.

**Expected result**

- ≤ C + 15 s the **early** warning with "Die Ruhezeit beginnt um …" (no "Deine Eltern haben eine Ruhezeit eingestellt …"); the final warning 1 minute before the start; Zeichentabelle closed at the break start with the break text.

#### TC-006-28 (optional): The budget runs out before the next break: only the budget warnings *(optional)*

- **Verifies**: AC-31 (budget first)
- **Machine / account**: Service PC / kid1; App B
- **Precondition**: **BREAK OFF**. App B: Rechner **Minuten** `2`, all days (Rechner has no usage today). E1 **Start-Zeit** = current minute + **5**; **BREAK ON**.

**Steps**

1. At once: Windows+R → `calc`; stopwatch. Read each dialog; leave it open.
2. After Rechner is gone, App B **BREAK OFF** before the break begins.

**Expected result**

- Early budget warning at start ("Rechner: noch 2 Minuten."), final at 1:00, closed at 2:00 with the **budget** text. **No** "Die Ruhezeit beginnt um …" line while Rechner runs.

### Block E — Other accounts, English, exemptions, tray client (must-run)

> **Before Block E**: App B: kid1's E1 **off**. Sign kid1 out (close K-TERM first).

#### TC-006-29: An uncontrolled account and the admin account are never warned, blocked or closed *(must-run)*

- **Verifies**: AC-26
- **Machine / account**: Service PC / ee-kid3 (not controlled), Admin
- **Precondition**: `ee-kid3` unticked in *Einstellungen*

**Steps**

1. *Benutzer wechseln* → `ee-kid3`. Windows+R → `notepad`, `mspaint`, `calc`, `charmap`. Use them **3 minutes**.
2. *Benutzer wechseln* → Admin. Start **Editor**; close it. Terminal (Administrator): LOG-FIND `account ee-kid3|account Admin`.
3. Sign `ee-kid3` out (*Task-Manager → Benutzer*).

**Expected result**

- All apps open normally and stay open; **no** EagleEye dialog in `ee-kid3`'s or your session.
- No "Blocked start", "Warning: account", "Grace period" or "App closed" line for `ee-kid3` or your account.

#### TC-006-30: English kid: English default texts and info lines; kid1's rules do not apply to ee-kid2 *(must-run)*

- **Verifies**: AC-10 (English budget default, English break default, Q-1), AC-14 (English warning defaults in the kid dialog), AC-34 (info line in the kid's language), AC-18 (rules of `eagleeye-kid` never apply to another account); plan R-10 (see §4 "What the English check proves")
- **Machine / account**: Service PC / ee-kid2 (English Windows, controlled), K-TERM; App B
- **Precondition**: S-6 done (else **Blocked**). Warning texts are at their defaults (TC-006-07, -10). ee-kid2 has no rules and default texts. kid1's Editor still has 23:59.

**Steps**

1. *Benutzer wechseln* → `ee-kid2`. Blocked autostart apps: **OK**. Open **K-TERM** (S-12).
2. Windows+R → `notepad`. Read the dialog; **OK**.
3. App B → **Regeln** → **Konto** `ee-kid2`: the Notepad row (Editor/Notepad) has appeared. Set **Minuten** `2`, **Tab**; tick all seven days. Wait 5 s.
4. Windows+R → `notepad`; stopwatch. Read the dialog; **leave it open**. Read it again at the text change (≈ 1:00) and at the close (≈ 2:00). **OK**.
5. App B: under **Ruhezeiten** for `ee-kid2`: **Eintrag hinzufügen** → **Start-Zeit** = now − 1 min, **Enter** → tick **An/Aus**. Wait 5 s.
6. Windows+R → `notepad`. Read the dialog; **OK**. App B: untick **An/Aus** of that entry.
7. Look at App B (German parent app) for `ee-kid2`: budget text box and **Anzeige Text:** box.
8. K-TERM: LOG-FIND `account ee-kid2`.

**Expected result**

- Step 2: blocked (ee-kid2 has no rule, kid1's 23:59 does not apply). The dialog shows the **English** default budget text "You have used up your time! Sorry, that's it for now." and an English info line "Notepad: not allowed today." (the app name as Windows shows it to ee-kid2). Title **EagleEye**, button **OK**.
- Step 4: at start the English early warning "Attention, you only have 5 minutes left! Save your progress." with "Notepad: 2 minutes left."; at ≈ 1:00 the English final warning "Attention!!! Only 1 minute left – save quickly now!" with "Notepad: 1 minute left."; at ≈ 2:00 the English budget text with "Notepad: time budget used up."; closed gracefully.
- Step 6: the **English** break text "Hi! Unfortunately, your parents have set a PC break for this time. You can't use this program right now. Sorry. How about using the time to read a book? 😊" with an English info line (e.g. "Break time from 3:09 PM to midnight.", time in ee-kid2's format).
- Step 7: App B (German) shows the **German** defaults for the same unchanged texts (by design: each app shows the default in its own language).
- **German** default texts in the dialog (with an English info line) = **Fail** (missing `en` satellite, R-10).
- Log: lines for `ee-kid2` only for its own Notepad; nothing about kid1's rules.

#### TC-006-31: The Start menu, Explorer, tray client, lock and sign-out are never limited *(must-run)*

- **Verifies**: AC-27
- **Machine / account**: Service PC / ee-kid2 (still signed in; deny by default for every other app)
- **Precondition**: TC-006-30 done; ee-kid2's break entry off

**Steps**

1. Open and close the **Start menu**. **Windows+E** (File Explorer); keep it open **2 minutes**; close it.
2. Right-click the EagleEye tray icon → *App Infos*; close it.
3. **Windows+L**; unlock.
4. Windows+R → `calc` (blocked). **Leave the dialog open.** Start → user icon → **Sign out**.

**Expected result**

- Steps 1 to 3 work as usual; the Explorer window and *App Infos* are not closed; no dialog for them; the tray icon stays.
- Step 4: Rechner is blocked; sign-out works although the dialog is open.

#### TC-006-32: Tray client not running: apps are still blocked, no message later *(must-run)*

- **Verifies**: AC-35
- **Machine / account**: Service PC / kid1, K-TERM
- **Precondition**: *Benutzer wechseln* → `eagleeye-kid`; K-TERM with LOG-WATCH; no dialog open

**Steps**

1. K-TERM: `Get-Process EagleEye.TrayClient -IncludeUserName | Where-Object UserName -like "*\eagleeye-kid" | Stop-Process -Force`. The tray icon disappears.
2. Windows+R → `calc`. Watch **60 s**.
3. Start → **EagleEye Tray**. Wait 10 s.
4. Windows+R → `calc`. **OK**.
5. LOG-FIND `Rechner|Tray client connected`.

**Expected result**

- Step 2: Rechner is closed (≤ 30 s); **no** dialog, also not later.
- Step 3: the tray icon is back (the tray client is never blocked).
- Step 4: the dialog appears again.
- Log: step 2 "Blocked start ended: … Rechner …; **not shown (tray client not connected)**"; step 3 "Tray client connected: … (session N, verified)."; step 4 "… message shown".

### Block F — Persistence, security, time zone, load, deletion (TC-006-33 to -37 must-run)

#### TC-006-33: Re-install keeps rules, texts and levels; enforcement without a parent app; used time is not refilled *(must-run)*

- **Verifies**: AC-18 (re-install), AC-36 (no parent app, after a service restart, used time kept)
- **Machine / account**: Service PC / Admin, kid1
- **Precondition**: Systeminformationen has used up its 0:01 today (TC-006-18). Screenshot of kid1's **Applikations-Regeln** and of **Zeit-Ende Warnungen** in App A.

**Steps**

1. **Close** App A and App B (window X).
2. Admin: run `03_Delivery\windows\EagleEye-Setup-0.6.0.exe` again with defaults. LOG-WATCH again; LOG-FIND `App rules loaded|Warning levels loaded`.
3. *Benutzer wechseln* → `eagleeye-kid`. Windows+R → `msinfo32`. **OK**. Windows+R → `calc`. **OK**.
4. *Benutzer wechseln* → Admin. Start App A → **Regeln** → `eagleeye-kid`; **Einstellungen**.

**Expected result**

- Step 2: "App rules loaded: N rules of … apps of 2 accounts, **1 changed budget texts**." (N = number of non-default rules, ≥ 5) and "Warning levels loaded: early **2** min, final **1** min."
- Step 3: both blocked although no parent app runs; Systeminformationen with "Systeminformationen: Zeit-Budget aufgebraucht." (the restart did **not** refill its budget).
- Step 4: the same rules, budget text and levels as in the screenshots.

#### TC-006-34: Which restricted SYSTEM token the agent runs with (TC-005-26 rerun) *(must-run, security)*

- **Verifies**: — (security: ADR-011 §7; US-004 plan Q-9; US-005 plan Q-8; US-006 plan Q-4)
- **Machine / account**: Service PC / Admin, Terminal (Administrator)
- **Precondition**: kid1 signed in (after TC-006-33)

**Steps**

1. LOG-FIND `Session agent|write restriction|write-restricted`.

**Expected result**

- "Session agent started in session N (process P)." and "Session agent in session N: diagnostics: user NT-AUTORITÄT\SYSTEM (S-1-5-18), integrity System, privileges SeChangeNotifyPrivilege, write-restricted yes|no".
- User **SYSTEM**, integrity **System**, **only** `SeChangeNotifyPrivilege`.
- **write-restricted yes**, or the accepted fallback (a Warning "Session agents exit at start with a write-restricted token; they run without the write restriction from now on …" and agents with `write-restricted no`). Both are a Pass. **Write the variant in Notes** (DEV and ARC have been waiting for it since US-004). Together with TC-006-15 ("closed gracefully" for a running app) this answers Q-4.

#### TC-006-35: A time-zone change is logged as a Warning with the old and new date; budgets follow the new day *(must-run)*

- **Verifies**: AC-39
- **Machine / account**: Service PC / kid1 in front, **K-TERM** (as admin)
- **Precondition**: kid1 signed in, K-TERM open, all break entries off. Windows+R → `notepad` (Editor 23:59) and leave it open. Systeminformationen is used up for today (TC-006-33).

**Steps**

1. K-TERM: `tzutil /g` → note it (expected `W. Europe Standard Time`).
2. Pick the zone by the current time: **from 12:30**: `tzutil /s "Line Islands Standard Time"` (local date + 1 day); **before 11:30**: `tzutil /s "Hawaiian Standard Time"` (local date − 1 day); between 11:30 and 12:30: wait. Note the time of Enter. Wait **10 s**.
3. K-TERM: LOG-FIND `time zone|clock of the PC`. `tzutil /g`.
4. Windows+R → `msinfo32`. Watch 15 s, then close it yourself (or **OK** if blocked).
5. K-TERM: `tzutil /s "W. Europe Standard Time"` (the value of step 1). Wait 10 s. LOG-FIND `time zone`. Windows+R → `msinfo32`; **OK**.

**Expected result**

- Step 3: a line with level **Warning**, timestamp ≤ 5 s after step 2: "The time zone of the PC changed from W. Europe Standard Time (…) to Line Islands Standard Time (…) [or Hawaiian …]; the local date changed from 10/17/2026 to 10/18/2026 [or 10/16/2026]; daily budgets now use the new day; break times now use the new local time. Session in use: eagleeye-kid." `tzutil /g` still shows the new zone (the service does not undo it).
- Step 4: Systeminformationen **starts** (another day's budget applies: by design, no hardening). Notepad stayed open.
- Step 5: a second Warning (the date back to 10/17/2026); Systeminformationen blocked again ("Zeit-Budget aufgebraucht").
- **Do not forget step 5** (also in Cleanup).

#### TC-006-36: CPU load with 10 allowed apps running *(must-run)*

- **Verifies**: AC-38 (US-004 AC-25 bound: below 2 % on average)
- **Machine / account**: Service PC / kid1; App B
- **Precondition**: kid1 in front. Close Notepad.

**Steps**

1. kid1: start once each of these that is **not yet listed** in App B, and click **OK** (blocked, the row appears): `taskmgr`, `dxdiag`, `mstsc`, Start → **Einstellungen**, **Microsoft Edge** (taskbar).
2. App B, kid1: set **Stunden** `1`, **Minuten** `0` and **all seven days** for: Editor (already 23:59: leave), Paint, Zeichentabelle, Rechner, Systeminformationen, Microsoft Edge, Task-Manager, Einstellungen, Remotedesktopverbindung, DirectX-Diagnoseprogramm. Wait 5 s.
3. kid1: open all ten: `notepad`, `mspaint`, `charmap`, `calc`, `msinfo32`, Edge (play a video), `taskmgr`, Einstellungen, `mstsc`, `dxdiag`. *Note only*: did the starts feel as fast as before US-006?
4. Task-Manager → **Details** → sort by name → the **two** `EagleEye.Service.exe` processes (service and agent): read the **CPU** column about every 10 s for **one minute**.
5. Watch the video for 30 s for stutter.

**Expected result**

- All ten apps start and stay open (no dialog).
- Step 4: the sum of both processes stays on average **below 2 %** (DEV measured 0.009 % / 0.005 % in US-005 without running apps). Write the values in Notes.
- Step 5: no visible stutter.

#### TC-006-37: Unticked = kept but not applied; deleting the account purges its rules and texts *(must-run)*

- **Verifies**: AC-18 (untick and re-tick; deletion purge within 60 s)
- **Machine / account**: Service PC / ee-kid2, Admin (App A), Terminal (Administrator)
- **Precondition**: ee-kid2's Notepad has used up its 0:02 (TC-006-30); ee-kid2 signed out

**Steps**

1. App A → *Einstellungen*: untick **ee-kid2**. Within 60 s **Regeln** → **Konto**: ee-kid2 is gone.
2. *Benutzer wechseln* → `ee-kid2`. Windows+R → `notepad`. Use it 30 s; close it.
3. *Benutzer wechseln* → Admin. Tick **ee-kid2** again. **Regeln** → `ee-kid2`.
4. *Benutzer wechseln* → `ee-kid2`. Windows+R → `notepad`. **OK**. Sign `ee-kid2` out.
5. Admin: App A → **Regeln** → `ee-kid2`: budget text box: type `Text für ee-kid2 🙂`, click outside.
6. Terminal (Administrator):
   ```powershell
   Get-CimInstance Win32_UserProfile | Where-Object LocalPath -like "*\ee-kid2" | Remove-CimInstance
   Remove-LocalUser -Name "ee-kid2"
   ```
   Watch App A (**Regeln**, **Konto**) and the log for 60 s. LOG-FIND `Purged`.

**Expected result**

- Step 2: Notepad opens normally and stays open (rules of an unticked account are not applied).
- Step 3: ee-kid2 is back with its Notepad rule (0:02, all days) and its break entry.
- Step 4: Notepad blocked again ("time budget used up"; the 30 s of step 2 were recorded while unticked or not: no matter).
- Step 6: within **60 s** ee-kid2 is gone from **Konto**; log "Purged the budget text of deleted account S-1-5-21-…" and "Purged the break times of deleted account …" (plus enforcement records). No Error.

#### TC-006-38 (optional): Reboot *(optional)*

- **Verifies**: AC-18 (reboot), AC-36 (after a reboot)
- **Machine / account**: Service PC / kid1, Admin

**Steps**

1. Restart the PC. Sign in as `eagleeye-kid` (no parent app running). Wait 30 s.
2. App B: Systeminformationen back to **Minuten** `1`, **Stunden** `0` (used up). Windows+R → `msinfo32`. **OK**.
3. Admin: App A → **Regeln** → `eagleeye-kid`; **Einstellungen**.

**Expected result**

- Systeminformationen blocked with "Zeit-Budget aufgebraucht" (used time kept over the reboot). Rules, budget text and levels unchanged.

#### TC-006-39 (optional, at midnight): The end at midnight *(optional)*

- **Verifies**: AC-23 (reset at local midnight), OQ-4 (midnight is an end when the next day is not allowed)
- **Machine / account**: Service PC / kid1; App B
- **Precondition**: start at about **23:53** on any evening after Block B. All break entries off. App B: Zeichentabelle **Stunden** `1`, **Minuten** `0`, **today** ticked, **tomorrow unticked**. Editor 23:59, all days.

**Steps**

1. At about **23:55**: kid1: Windows+R → `charmap` and `notepad`. Leave both open, kid1 in front.
2. Note the clock at each dialog until 00:01. **OK** each time.
3. At **00:01**: Windows+R → `charmap`. **OK**.

**Expected result**

- Early warning for Zeichentabelle at **23:58** (≤ +10 s), final at **23:59**, both with the minutes left; at **00:00** (≤ +10 s) Zeichentabelle closed with the budget text and the info line "Zeichentabelle: heute nicht erlaubt." (plan D-3); gone ≤ 00:00:30.
- Editor (allowed tomorrow) keeps running across midnight; no warning for it.
- Step 3: blocked, "heute nicht erlaubt".

#### TC-006-40 (optional): A clock change is logged as a Warning *(optional)*

- **Verifies**: AC-39 (clock change)
- **Machine / account**: Service PC / Admin, Terminal (Administrator)

**Steps**

1. `Set-Date (Get-Date).AddMinutes(10)`. Wait 10 s. LOG-FIND `clock of the PC`.
2. `Set-Date (Get-Date).AddMinutes(-10)`, then `w32tm /resync` (ignore an error if the time service is off). LOG-FIND again.

**Expected result**

- A **Warning** "The clock of the PC changed from … to …; the local date stays 10/17/2026; break times now use the new local time." ≤ 5 s after each change; nothing is undone by the service.

#### TC-006-41 (optional, needs a SQLite tool): History of blocked starts and enforced closes in the database *(optional)*

- **Verifies**: AC-37 (history with reason), AC-18 (purge with the account)
- **Machine / account**: Service PC / Admin, Terminal (Administrator)
- **Precondition**: S-15; after TC-006-37

**Steps**

1. Read a copy (the database stays untouched):
   ```powershell
   $tmp = "C:\EETest\db"; New-Item -ItemType Directory -Force $tmp | Out-Null
   Copy-Item "$env:ProgramData\EagleEye\EagleEye.Service.db*" $tmp -Force
   sqlite3 -readonly "$tmp\EagleEye.Service.db" "SELECT * FROM BlockedStarts ORDER BY rowid DESC LIMIT 40;"
   sqlite3 -readonly "$tmp\EagleEye.Service.db" "SELECT * FROM WarningSettings;"
   ```

**Expected result**

- Rows of this run with `Kind` `blocked start` or `running app`, `Reason` `not allowed today` / `budget used up` / `break time`, `AfterGrace` 1 for the grace closes of TC-006-18, -19, -26, `Trigger` values such as `app start`, `budget ran out`, `grace period ended`, `break began`; break rows with entry minutes, budget rows without. No row of the deleted `ee-kid2`. `WarningSettings`: early 2, final 1, no texts (defaults).

#### TC-006-42 (optional): English parent app *(optional, only with an English parent PC)*

- **Verifies**: AC-10, AC-14 (default texts in the parent app's language), parent-app `en` satellite (report D-16)
- **Machine / account**: a parent PC with English Windows display language (e.g. LEOSERV, if English); otherwise **Skipped**

**Steps**

1. **Rules** → `eagleeye-kid`: read the **Application rules** section. **Settings** → **Time-elapsed warnings**. Hours field `24`, Tab.

**Expected result**

- English labels (report §8); the unchanged defaults in English: early "Attention, you only have 5 minutes left! …", final "Attention!!! Only 1 minute left – …"; kid1's changed budget text exactly as entered (not translated); "Enter hours between 0 and 23.".

## 6. Regression

None (`02_Implementation/docs/testing/README.md` §3; story run only). Note for the closure: US-006 replaces US-005 AC-25 ("apps started before a break keep running"). When US-005 cases are added to `02_Implementation/docs/testing/regression-checklist.md`, TC-005-15 and TC-005-21 must not expect running apps to stay open.

## 7. Cleanup

1. **LOG-COPY** with `<subfolder>` = `run-01-service-logs`. Look through the copies before committing.
2. **Time zone**: `tzutil /g` → `W. Europe Standard Time` (TC-006-35). Clock correct (TC-006-40: `w32tm /resync`).
3. **Break times**: every entry of `eagleeye-kid` **off**.
4. **Rules of `eagleeye-kid`**: keep them (test account), or set the budgets you want. **Real kid accounts**: if you unticked them in S-4, tick them again **and set their rules first**, otherwise every app is blocked for them.
5. **Accounts**: sign out `eagleeye-kid`; remove `ee-kid3` (and `ee-kid2`, if TC-006-37 was not run):
   ```powershell
   foreach ($n in "ee-kid2","ee-kid3") {
     Get-CimInstance Win32_UserProfile | Where-Object LocalPath -like "*\$n" | Remove-CimInstance
     if (Get-LocalUser -Name $n -ErrorAction SilentlyContinue) { Remove-LocalUser -Name $n }
   }
   ```
6. **Database copy** (TC-006-41): `Remove-Item C:\EETest -Recurse -Force`.
7. **LEOSERV**: App B stays installed and paired.

## 8. Notes and Open Questions for Michael

| ID | Question | Proposed answer |
|---|---|---|
| Q-1 | **LEOSERV as App B**: needed to change rules while the kid's session is in front (Blocks B to E) and for TC-006-10. Available? | Yes. Without it TC-006-10 is Blocked, and the grace and break timings become weak (a user switch pauses the kid's used time). |
| Q-2 | **New accounts with the story's roles**: `ee-kid2` controlled with English Windows (deleted in TC-006-37), `ee-kid3` not controlled. In US-005 the roles were the other way round. OK? | Yes. Both were removed after US-005, so they are new accounts. |
| Q-3 | **English language pack** for `ee-kid2` (S-6): may need a download through Windows Settings. OK? | Yes. Without it TC-006-30 is Blocked and the English defaults stay unchecked (plan R-10). |
| Q-4 | **Must-run vs. optional**: optional are TC-006-11, -21, -22, -27, -28, -38 to -42. Only AC-12 rests on an optional case alone. OK? | Yes. |
| Q-5 | **Time-zone change** (TC-006-35) done by you in K-TERM, as the story's verification says, and reset in the case and in Cleanup. OK? | Yes. |
| Q-6 | **Midnight** (TC-006-39) is optional, because it needs you at the PC from 23:53 to 00:01. The break cases cover the same "end by clock time" path. OK? | Yes. Run it on any evening after Block B if you like. |
| Q-7 | **Deny by default** right after the upgrade (§2). Install when the kids do not need the PC; real kid accounts unticked or configured right after TC-006-04. OK? | Yes (your decision OQ-2). |

## 9. Change Log

| Date | Change | Reason |
|---|---|---|
| 2026-10-17 | Plan written after DEV's handover (0.6.0, approved by Michael on 2026-10-17). | Step d. |

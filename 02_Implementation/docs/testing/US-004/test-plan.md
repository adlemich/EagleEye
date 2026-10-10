# Test Plan: US-004 — App Usage Tracking and Daily Usage Report

**Status**: Draft, for approval by Michael
**Date**: 2026-10-08
**Author**: TES
**User Story**: `02_Implementation/docs/requirements/user-stories/US-004/user-story.md` (27 ACs, approved 2026-10-07, OQ-1 to OQ-11 answered)
**Inputs used**: the user story; `02_Implementation/docs/requirements/user-stories/US-004/implementation-plan.md` ("Manual Verification Notes", Decision 1a security measures, log templates, Localization Impact, Deviations D-1 to D-9, answers Q-1 to Q-9; used only for setup, artifact locations and texts, not for expected behaviour); `02_Implementation/docs/architecture/decisions/ADR-011-session-agent-for-app-observation.md` (§4 "Apps" rule, §5 never recorded, §7 hardening, Security Analysis); `02_Implementation/docs/architecture/decisions/ADR-012-usage-accounting.md`; `02_Implementation/docs/testing/README.md` §4 (service logs as evidence); the US-003 test documents and report (setup, lessons). Black box: no source code was read.

> **Written in parallel with DEV.** `02_Implementation/docs/requirements/user-stories/US-004/implementation-report.md` did not exist yet. Places that depend on exact implemented texts, log formats or behaviour use the wording of the story and the plan and are marked `<!-- verify against implementation report -->`. TES aligns them after DEV's handover, before the run starts.

---

## 1. Scope

**In scope** (only the new US-004 features, Michael's decision Q-5):

- Upgrade of the service and the parent app from 0.3.1 to 0.4.0, keeping pairing and selections (supporting)
- The session agent: runs only in sessions of controlled accounts, cannot be ended by the kid (AC-1, ADR-011)
- What is recorded: apps of controlled accounts only; the "Apps" group examples of AC-3; File Explorer only with a window; background, Windows and tray-only processes never; multi-process apps once (AC-1, AC-3 to AC-8)
- How much is recorded: active time with lock, user switch, sleep, service stop; accuracy with a stopwatch (AC-12 to AC-16, AC-24)
- History and log entries (AC-7, AC-10), retention and purge (AC-9, AC-11)
- The Reports page: menu, account selection, days, table, empty states, live update, following account changes (AC-17 to AC-23)
- Separation of accounts, load, no effect on the kid (AC-25 to AC-27)
- The security checks of the implementation plan (Decision 1a, Manual Verification Notes "Security checks")

**Out of scope**: limits and enforcement; display of the history or inventory; day totals; Android/iOS/macOS parent apps; programs started with other credentials (Q-4); tray client changes.

**Not tested in this run, with reason**

| Item | Reason |
|---|---|
| US-003 cases skipped in US-003 run 01 | Michael, implementation plan Q-5: **no** US-003 re-test in this run. If the Reports page does not follow account changes (AC-23), note it; it may be the same US-003 push mechanism (implementation plan, "US-003 dependency"). |
| Regression | Story runs contain no regression cases (`02_Implementation/docs/testing/README.md` §3). |
| English UI texts and English Windows app names | Michael's Windows is German. English texts are covered by DEV's unit tests. App names come from the service PC's Windows language (plan D-7): German names are expected. |
| Waiting 90 days (AC-11) | Not possible. TC-004-32 (optional) shortens it by back-dating rows with a SQLite tool; otherwise unit tests cover it. |

## 2. Test Environment

| Item | Value |
|---|---|
| Service PC | **Windows Developer Machine** (Windows 11 Pro, German UI, `ZOCK-O-MAT-V3`). `<host>` = its computer name. Current state (Orchestrator, 2026-10-08): **EagleEye 0.3.1** service installed and running, App A paired. |
| Parent PC | **LEOSERV** (the second Windows PC, ISSUE-006), parent app **App B**. Needed for the live checks (AC-22), because the parent app on the service PC is not visible while the kid's session is in front. Without LEOSERV, TC-004-05 is **Blocked**; everything else works with App A. |
| Parent apps | **App A** = parent app in Michael's admin account on the service PC (device *Papas PC*). **App B** = parent app on LEOSERV (device *LEOSERV*), upgraded and paired in S-14. |
| Accounts on the service PC | **Admin** = Michael's administrator account (parent). **kid1** = `eagleeye-kid` (standard, exists, **controlled**). **kid2** = `ee-kid2` (new, standard, **not controlled**). **kid3** = `ee-kid3` (new, standard, **controlled**; second controlled account for AC-26, deleted in TC-004-30). |
| Builds under test | `03_Delivery/windows/EagleEye-Setup-0.4.0.exe` (service + tray, admin) and `03_Delivery/windows/EagleEye-ParentApp-Setup-0.4.0.exe` (parent app, per user). <!-- verify against implementation report: dates, SHA-256 --> |
| Expected versions | *Installierte Apps*: **EagleEye 0.4.0**, **EagleEye Parent App 0.4.0**. Tray *App Infos*: `EagleEye_v0.4`. |
| Tools | Stopwatch (phone). **Terminal (Administrator)** in the admin session (S-6). **K-TERM** = an elevated terminal *inside the kid's session* (S-13). `services.msc`, Task Manager (*Task-Manager*), Explorer. Optional: a SQLite tool (TC-004-32). |

### Test blocks — core first, optional last

Michael closes stories after partial runs. The blocks are therefore ordered by importance: **Blocks A to C hold the core AC checks** (what is and is not recorded, stopwatch accuracy AC-24, the Reports page, live update). Optional cases are marked **(optional)** in the title; skipping them does not leave a core AC unchecked except the ones listed under "Optional coverage" below.

| Block | Cases | Machine / accounts | Core / optional | Duration |
|---|---|---|---|---|
| Setup | S-1 to S-14 | Service PC, LEOSERV | — | ≈ 35 min (two first sign-ins) |
| **A** Upgrade and first look | TC-004-01 to -03 (3) | Service PC / Admin, kid1 | core | ≈ 20 min |
| **B** What is recorded and how much | TC-004-04 to -13 (10) | Service PC / kid1, kid2, kid3, Admin; LEOSERV | core | ≈ 75 min |
| **C** Reports page states and account changes | TC-004-14 to -17 (4) | Service PC / Admin, kid2 | core | ≈ 30 min |
| **D** Sessions and service lifecycle | TC-004-18 to -20 core, -21, -22 optional (5) | Service PC / kid1, Admin | core + optional | ≈ 25 min core, + 15 min optional |
| **E** Security checks | TC-004-23 to -28 core, -29 optional (7) | Service PC / kid1, Admin | core (security) + optional | ≈ 45 min core, + 5 min optional |
| **F** Account deletion and long checks | TC-004-30 core, -31 to -35 optional (6) | Service PC / Admin, kid1; LEOSERV | core + optional | ≈ 10 min core, + ≈ 90 min optional (TC-004-33 needs midnight, TC-004-34 the next day) |

35 cases: 27 core, 8 optional. Run the blocks in this order; breaks between blocks are fine.

**Optional coverage**: AC-11 (TC-004-32), AC-14 (TC-004-33), AC-16 (TC-004-31) and AC-25 (TC-004-35) are covered **only** by optional cases; AC-18's "older days in descending order" is fully visible only in TC-004-32 or TC-004-34 (a fresh install has only today). If these are skipped, they rest on DEV's unit tests.

### Conventions for expected results

- **UI texts** are German (Michael's Windows). Wording differences are **Notes**, not Fails (story, "Language of UI texts"). Pass criteria are only the menu entry **Berichte**, the label **Konto**, the column headers **App** and **Nutzung (HH:MM)**, and the text **Heute keine Nutzung aufgezeichnet** (story AC-17 to AC-19). Dates appear in the Windows short date format (e.g. `08.10.2026`); format differences are Notes.
- **App names** are those of the service PC's German Windows: *Editor* (Notepad), *Paint*, *Microsoft Edge*, *Rechner* (Calculator), *Snipping Tool*, *Task-Manager*, *Windows-Explorer*. A different name of the same program is a Note; a host name instead of the app (e.g. *Application Frame Host*) is a **Fail** (AC-3).
- **HH:MM** (AC-24, story test setup): a shown value is correct when it equals the stopwatch time rounded **down** to whole minutes, with a tolerance of **one minute**. Example: stopwatch 10:20 → "00:10"; "00:09" to "00:11" pass.
- **Measuring usage by difference.** Several cases use the same app (Notepad = *Editor*). Before each measurement note the shown value **V0**; after it, **V1**. Expected **V1 − V0** = stopwatch minutes ±1.
- **Timing.** Live update (AC-22): ≤ **15 s**. Account changes (AC-2, AC-23, AC-9): ≤ **60 s**. Where the story gives no limit, slowness is a Note.
- **Which session counts.** Only the session in front counts (AC-12). When you switch to the admin session to look at App A, the kid's apps stop counting (they stay open). That is expected, and it keeps measurements clean.
- **150 % scaling** on the service PC (ISSUE-004): cut-off text in the Reports page is a **Fail**.
- **Unexpected behaviour** is a Note; TES routes it to PRO.

### Service log files as evidence

The service log is in the **admin-only** folder `%ProgramData%\EagleEye\logs\` (US-003). All log commands need an elevated terminal: the Terminal (Administrator) in the admin session, or K-TERM in the kid's session (S-13).

- **LOG-COPY** at the end of every run (or block) → `evidence/run-01-service-logs/`; **right after any Fail** → `evidence/run-01-service-logs/after-TC-004-NN/`. Reference the folder in the case's Notes. Look through the copies before committing (user names, program paths and SIDs only; no secrets).
- **If there is no log file**: look in *Ereignisanzeige → Windows-Protokolle → Anwendung*, source **EagleEye**, for "The log folder … could not be restricted …" (US-003); mark the case Fail, screenshot, and use the Event Log instead.

**LOG-COPY** (elevated, in the repo root):

```powershell
Set-Location <your EagleEye repo folder>
$dst = Join-Path "02_Implementation\docs\testing\US-004\evidence" "<subfolder>"
New-Item -ItemType Directory -Force $dst | Out-Null
Copy-Item "$env:ProgramData\EagleEye\logs\*.log" $dst -Force
Get-ChildItem $dst
```

**LOG-WATCH** (elevated; after every service restart, reinstall or reboot: *Strg+C* and run again):

```powershell
$log = Get-ChildItem "$env:ProgramData\EagleEye\logs\EagleEye.Service-*.log" | Sort-Object LastWriteTime | Select-Object -Last 1
Get-Content $log.FullName -Tail 15 -Wait
```

**LOG-FIND** (elevated; replace `<pattern>`):

```powershell
Select-String -Path "$env:ProgramData\EagleEye\logs\*.log" -Pattern "<pattern>" | Select-Object -Last 15 | ForEach-Object Line
```

**AGENTS** (elevated; which `EagleEye.Service.exe` runs in which session, and which user owns which session):

```powershell
Get-Process -Name EagleEye.Service -IncludeUserName | Format-Table Id, SessionId, UserName, StartTime -AutoSize
query user
```

Expected log lines (plan templates; wording differences are Notes, the content named in the ACs is the Pass criterion): <!-- verify against implementation report -->

```text
… [INF] …: Usage recording started for account eagleeye-kid (S-1-5-21-…-1002) in session 2.
… [INF] …: Session agent started in session 2 (process 7412).
… [INF] …: Session agent in session 2: user SYSTEM, integrity System, privileges SeChangeNotifyPrivilege, write-restricted yes
… [INF] …: New app for account eagleeye-kid: Editor (Notepad.exe, C:\Program Files\WindowsApps\Microsoft.WindowsNotepad_…\Notepad\Notepad.exe).
… [INF] …: App started: account eagleeye-kid, Editor (Notepad.exe, C:\Program Files\WindowsApps\…\Notepad.exe), instance 12.
… [INF] …: App ended: account eagleeye-kid, Editor (Notepad.exe, …), instance 12, duration 00:13:05 (closed).
… [INF] …: Usage recording stopped for account ee-kid2 (S-1-5-21-…): monitoring stopped.
… [INF] …: Usage accounting paused for 125 s (sleep, suspension or delay); this time is not counted.
… [INF] …: Purged all recorded data of deleted account S-1-5-21-…: 2 apps, 3 history entries, 2 daily entries.
```

End reasons in "App ended": `closed`, `monitoring stopped`, `session ended`, `service stopping`, `service stopped unexpectedly`.

## 3. Setup Instructions

**Before Block A** (service PC, Admin). Open the Terminal (Administrator) (S-6) and note `<host>` (S-8) first.

1. **S-1 Installers present.** PowerShell in the repo root: `Get-Item 03_Delivery\windows\EagleEye-Setup-0.4.0.exe, 03_Delivery\windows\EagleEye-ParentApp-Setup-0.4.0.exe | Select-Object Name, LastWriteTime`, and `(Get-FileHash <file>).Hash` for each. Dates and hashes as in the implementation report §"How to test". <!-- verify against implementation report -->
2. **S-2 Installed state.** *Installierte Apps* → "EagleEye". Expected **EagleEye 0.3.1** and **EagleEye Parent App 0.3.1**; `services.msc` → **EagleEye Service** *Wird ausgeführt*.

   | Found | Do |
   |---|---|
   | 0.3.1 / 0.3.1 (expected) | Continue with S-3. |
   | Parent app missing | Install `03_Delivery\windows\EagleEye-ParentApp-Setup-0.3.1.exe`, pair it in S-3. |
   | Service missing, or another version than 0.3.1 / 0.4.0 | Stop and tell TES (the upgrade path 0.3.1 → 0.4.0 is part of the test). |
   | Already **0.4.0** | Stop and tell TES. TC-004-01 then cannot test the upgrade; TES decides. |
3. **S-3 App A paired and green**: "Verbunden mit `<host>`", *Gekoppelt*, device **Papas PC**. If not: pair (code from the tray popup in the admin session).
4. **S-4 Leftover test accounts.** Terminal (Administrator): `Get-LocalUser | Format-Table Name, FullName, Enabled -AutoSize`. If US-003 test accounts are still there (`ee-anna`, `ee-annika`, `ee-max`, `ee-lena`, `ee-gesperrt`, `ee-neu`, `ee-zwei`, `defaultuser0`, `defaultuser1`, `ee-admin`): remove them (none of them ever signed in):
   ```powershell
   "ee-annika","ee-anna","ee-max","ee-lena","ee-gesperrt","ee-neu","ee-zwei","defaultuser0","defaultuser1","ee-admin" |
     ForEach-Object { if (Get-LocalUser -Name $_ -ErrorAction SilentlyContinue) { Remove-LocalUser -Name $_ } }
   ```
   Note any other standard account (e.g. a real family account); it must stay **unticked** for this run.
5. **S-5 Display scaling** of the admin account = **150 %** (*Einstellungen → System → Bildschirm → Skalierung*).
6. **S-6 Terminal (Administrator)**: Start → **Terminal** → right-click → **Als Administrator ausführen** → *Ja*. Title bar starts with **"Administrator:"**. A normal terminal is **not** enough (US-002 ISSUE-005). Service commands: `Stop-Service` / `Start-Service` / `Restart-Service -DisplayName "EagleEye Service"`.
7. **S-7 Log monitor**: a second Terminal (Administrator) with LOG-WATCH.
8. **S-8 Names.** `hostname` → `<host>`.
9. **S-9 New kid accounts.** Terminal (Administrator):
   ```powershell
   $pw = Read-Host -AsSecureString "Password for ee-kid2 and ee-kid3"
   $usersGroup = (Get-LocalGroup -SID "S-1-5-32-545").Name
   New-LocalUser -Name "ee-kid2" -Password $pw -PasswordNeverExpires
   New-LocalUser -Name "ee-kid3" -Password $pw -PasswordNeverExpires
   "ee-kid2","ee-kid3" | ForEach-Object { Add-LocalGroupMember -Group $usersGroup -Member $_ }
   ```
   (No full names: the accounts are shown as `ee-kid2`, `ee-kid3`.)
10. **S-10 First sign-in** of `ee-kid2` and `ee-kid3` (*Benutzer wechseln*), so that their profiles exist and later switches are fast. In each: wait until the desktop is ready, set display scaling to 150 %, then **sign out** (Start → user icon → *Abmelden*). Also sign out `eagleeye-kid` if it is signed in (*Task-Manager → Benutzer*). Note the password of `eagleeye-kid`; you need it for every switch.
11. **S-11 Selection (still 0.3.1).** App A → *Einstellungen* → *Benutzerkonten auf dem EagleEye-PC*: tick **eagleeye-kid** and **ee-kid3**; **ee-kid2** and every other account **unticked**. (TC-004-01 checks that the upgrade keeps this.)
12. **S-12 SmartScreen** on the unsigned installers: *Weitere Informationen* → *Trotzdem ausführen* (expected).
13. **S-13 K-TERM (elevated terminal in the kid's session).** Several cases need an elevated terminal **while the kid's session is in front** (log monitor, service commands). In the kid's session: Start → **Terminal** → right-click → **Als Administrator ausführen** → UAC asks for an administrator: enter **your admin user name and password**. The title bar starts with "Administrator:". It runs with your admin rights, so it is not recorded for the kid (Q-4 of the plan: other credentials are out of scope); ignore it in the report and in Task Manager comparisons. Close it before you sign the kid out.

**Before Block B** (LEOSERV):

14. **S-14 App B on LEOSERV.** On LEOSERV install `EagleEye-ParentApp-Setup-0.4.0.exe` (over an older version if present). Start it. If it is green and *Gekoppelt* with `<host>`: done. Otherwise (its pairing became invalid in US-003): *Kopplung aufheben* if offered, then connect to `<host>` and pair with device name **LEOSERV** (code from the tray popup in your admin session on the service PC). Note LEOSERV's scaling and display language. If LEOSERV is not available today: write it here; TC-004-05 is then **Blocked**.

## 4. Acceptance Criteria Coverage

Cases in *italics* are optional.

| AC | Test case(s) |
|---|---|
| AC-1 | TC-004-03 (agents), TC-004-11 (kid2, admin) |
| AC-2 | TC-004-16 |
| AC-3 | TC-004-06 |
| AC-4 | TC-004-07 |
| AC-5 | TC-004-08; TC-004-26 (EagleEye name only by path) |
| AC-6 | TC-004-09 |
| AC-7 | TC-004-04, TC-004-12 |
| AC-8 | TC-004-06; TC-004-25 (fallback to the process name) |
| AC-9 | TC-004-16 (untick keeps data), TC-004-20 (re-install), TC-004-30 (deletion purges) |
| AC-10 | TC-004-04, TC-004-19; *TC-004-21* |
| AC-11 | *TC-004-32* |
| AC-12 | TC-004-04 (lock), TC-004-09 (minimised, parallel), TC-004-18 (switch user); *TC-004-22* (sleep) |
| AC-13 | TC-004-05 (through the live lag) |
| AC-14 | *TC-004-33* |
| AC-15 | TC-004-19, TC-004-20; *TC-004-21*, *TC-004-22* |
| AC-16 | *TC-004-31* |
| AC-17 | TC-004-02 |
| AC-18 | TC-004-02, TC-004-10; *TC-004-32*, *TC-004-34* (older days) |
| AC-19 | TC-004-02, TC-004-10; *TC-004-32*, *TC-004-34* |
| AC-20 | TC-004-02 (no usage), TC-004-14 (not connected, loading), TC-004-17 (no controlled account) |
| AC-21 | TC-004-02, TC-004-14, TC-004-15 |
| AC-22 | TC-004-05 |
| AC-23 | TC-004-16, TC-004-17, TC-004-30 |
| AC-24 | TC-004-04 |
| AC-25 | *TC-004-35* |
| AC-26 | TC-004-12 |
| AC-27 | TC-004-03, TC-004-13 |

All 27 ACs have at least one case. Security checks (no AC, implementation plan Decision 1a): TC-004-23 to TC-004-29.

## 5. Test Cases

### Block A — Upgrade and first look (core)

*Service PC · Admin. Start state: setup S-1 to S-13 done; App A (0.3.1) green; all kid accounts signed out.*

#### TC-004-01: Service update 0.3.1 → 0.4.0 keeps pairing and selection *(supporting)*

- **Verifies**: — (supporting: upgrade path; prerequisite for every other case)
- **Machine / account**: Service PC / Admin (+ Terminal (Administrator))
- **Precondition**: EagleEye 0.3.1 running; App A open and green

**Steps**

1. Run `03_Delivery\windows\EagleEye-Setup-0.4.0.exe` with defaults; finish. Leave App A open.
2. If no tray icon: Start → **EagleEye Tray**. Tray → *App Infos*.
3. *Installierte Apps* → "EagleEye"; `services.msc` → **EagleEye Service**.
4. Watch App A for up to 60 s. Then *Einstellungen* → account section.
5. Log monitor: *Strg+C*, LOG-WATCH. Look at the lines of the new start.

**Expected result**

- Wizard without error. *App Infos*: **EagleEye_v0.4**. **EagleEye 0.4.0** listed once; service *Wird ausgeführt*.
- App A green again within 60 s **without** a pairing code; the account section shows **eagleeye-kid** and **ee-kid3** ticked, **ee-kid2** unticked (as in S-11).
- The log shows the start of the service and "Account inventory loaded: … 2 under parental control." No Error lines. <!-- verify against implementation report -->

#### TC-004-02: Parent app 0.4.0: menu "Berichte" and the Reports page before any usage

- **Verifies**: AC-17, AC-19 (today always shown), AC-20 (account without usage), AC-21 (on opening)
- **Machine / account**: Service PC / Admin
- **Precondition**: TC-004-01; no kid has used an app since the upgrade

**Steps**

1. With App A open, run `03_Delivery\windows\EagleEye-ParentApp-Setup-0.4.0.exe` with defaults; start the app. *Installierte Apps*: version.
2. Look at the navigation menu. Click **Berichte**.
3. Open the selection **Konto** and read the entries and their order. Close it without changing.
4. Look at the rest of the page.

**Expected result**

- **EagleEye Parent App 0.4.0**; green without a pairing code.
- Menu: **Einstellungen** and **Berichte** (next to each other / one below the other).
- Page **Berichte** with the selection **Konto**. It lists exactly the controlled accounts **eagleeye-kid** and **ee-kid3** (names and order as in *Einstellungen*, US-003 AC-11), **not** `ee-kid2`, not your admin account. The **first** entry is selected.
- Below: a heading **"Heute, 08.10.2026"** (today's date) and the text **"Heute keine Nutzung aufgezeichnet"**; no table header, no older days.
- At 150 %, nothing is cut off.

#### TC-004-03: The session agent runs only in the session of a controlled account

- **Verifies**: AC-1 (only controlled sessions are watched), AC-27 (no window)
- **Machine / account**: Service PC / kid1, then Admin (Terminal (Administrator))
- **Precondition**: TC-004-02

**Steps**

1. Terminal (Administrator): **AGENTS**. Note the output (only the service, session 0).
2. *Benutzer wechseln* → `eagleeye-kid`. Look at the desktop, taskbar and notification area for 30 s.
3. *Benutzer wechseln* → Admin. Terminal (Administrator): **AGENTS** again.
4. LOG-FIND with `<pattern>` = `Session agent|Usage recording started`.

**Expected result**

- Step 1: one `EagleEye.Service` process, SessionId **0**, user `NT-AUTORITÄT\SYSTEM`.
- Step 2: nothing new for the kid: no window, no taskbar button, no tray message.
- Step 3: a **second** `EagleEye.Service` process with user `NT-AUTORITÄT\SYSTEM` and the **SessionId of eagleeye-kid** (from `query user`). **None** with the SessionId of your admin session.
- Step 4: "Session agent started in session N (process P)" and "Usage recording started for account eagleeye-kid (…) in session N." with the same N. <!-- verify against implementation report -->

### Block B — What is recorded and how much (core)

*Service PC, mostly in the session of `eagleeye-kid` (kid1), with K-TERM (S-13) running LOG-WATCH in that session. App B on LEOSERV (S-14) shows "Berichte" → Konto `eagleeye-kid`. Without LEOSERV, read the values in App A after switching back to Admin (switching stops the counting, so the values stay stable).*

> **Before Block B**: S-14 done. Switch to `eagleeye-kid`; open K-TERM (S-13) and run LOG-WATCH in it.

#### TC-004-04: Accuracy: Notepad 10 minutes, with 3 minutes locked in between

- **Verifies**: AC-24, AC-12 (lock), AC-10 (start/end log, duration), AC-7 (app record)
- **Machine / account**: Service PC / kid1, K-TERM; App B (or App A afterwards)
- **Precondition**: kid1's session in front; Editor not open. Note **V0** = Editor value of today (App B; "—" if no row)

**Steps**

1. Start **Editor** (Notepad). **Start the stopwatch** when its window appears.
2. Keep it open and the session unlocked. At stopwatch **5:00**: press **Windows+L** and **pause** the stopwatch. Wait **3 minutes** (clock).
3. Unlock (kid1's password). **Resume** the stopwatch.
4. At stopwatch **10:00**: close Editor (window **X**; do not save).
5. Wait 15 s. Read **V1** = Editor value of today (App B). LOG-FIND with `Editor`.

**Expected result**

- **V1 − V0 = 10 minutes**, shown e.g. "00:10" (tolerance "00:09" to "00:11"). The 3 locked minutes are **not** added (a value of "00:13" is a Fail).
- Log: "New app for account eagleeye-kid: Editor (Notepad.exe, <path>)" (only at the first use), "App started: account eagleeye-kid, Editor (Notepad.exe, <path>) …" and "App ended: … duration 00:13:xx (closed)". The duration is about **13** minutes, because the history instance continues while locked (plan D-4); the usage is 10. <!-- verify against implementation report -->

#### TC-004-05: The open Reports page updates by itself within 15 s

- **Verifies**: AC-22, AC-13
- **Machine / account**: Service PC / kid1; **LEOSERV / App B** (place it next to the service PC)
- **Precondition**: App B green on *Berichte*, **Konto** `eagleeye-kid`; Paint not yet used today. If LEOSERV is not available: **Blocked**.

**Steps**

1. In kid1's session start **Paint**; start the stopwatch when its window appears. Do not touch App B.
2. Watch App B: stop a second stopwatch (or note the time) when the row **Paint** appears.
3. Keep Paint open and the session unlocked. Watch App B around stopwatch **3:00**: note when **Paint** changes to "00:03".
4. Leave Paint open (used in TC-004-06).

**Expected result**

- The row **Paint** appears in today's table at most **15 s** after Paint's window appeared, without any action in App B.
- "00:03" appears at the latest at stopwatch **3:15**. Note the actual seconds (expected about 3:05).
- The page never needed a click or a reload.

#### TC-004-06: The listed apps are recorded under their own names

- **Verifies**: AC-3, AC-8
- **Machine / account**: Service PC / kid1; App B (or App A afterwards)
- **Precondition**: TC-004-05 (Paint open)

**Steps**

1. In kid1's session start, one after the other, and keep each open for at least **1 minute**: **Microsoft Edge**, **Rechner** (Calculator, Store app), **Snipping Tool**, **Task-Manager** (Strg+Umschalt+Esc), and **one game or other installed desktop program** of your choice (write its name in Notes).
2. Open **Task-Manager → Prozesse** in kid1's session and look at the group **Apps**.
3. Look at today's table in App B. LOG-FIND with `New app for account eagleeye-kid`.

**Expected result**

- Today's table has one row each for **Paint**, **Microsoft Edge**, **Rechner**, **Snipping Tool**, **Task-Manager** and your program, with the names Task Manager shows in its *Apps* group (German Windows; AC-8: the program's description).
- **Rechner** is shown as "Rechner", **not** as "Application Frame Host" or "ApplicationFrameHost".
- One "New app" log line per program, each with the process name (e.g. `CalculatorApp.exe`, `mspaint.exe`, `msedge.exe`) and the full program path.
- Leave the programs open for TC-004-08 and TC-004-09.

#### TC-004-07: File Explorer counts only while one of its windows is open

- **Verifies**: AC-4
- **Machine / account**: Service PC / kid1; App B
- **Precondition**: no File Explorer window open in kid1's session (taskbar)

**Steps**

1. Use the desktop, the Start menu and the taskbar for 2 minutes **without** opening a File Explorer window (e.g. open and close the Start menu, right-click the desktop). Look at App B.
2. Open **Explorer** (Windows+E). Stopwatch. Keep the window open for **2 minutes**. Look at App B.
3. Close the window. Wait 1 minute. Look at App B.

**Expected result**

- Step 1: **no** row "Windows-Explorer" appears (the shell is not usage).
- Step 2: a row **"Windows-Explorer"** appears within 15 s and reaches "00:02" (±1).
- Step 3: the value stops growing.

#### TC-004-08: Background, Windows and tray-only processes are not recorded

- **Verifies**: AC-5
- **Machine / account**: Service PC / kid1; App B
- **Precondition**: TC-004-06 (several apps open); the OneDrive icon is in kid1's notification area (if OneDrive is not set up for kid1, note it)

**Steps**

1. Task-Manager (kid1) → *Prozesse*: note the entries in **Hintergrundprozesse** and **Windows-Prozesse** (screenshot → `evidence/run01-tc08-taskmanager.png`).
2. Open the EagleEye tray icon's *App Infos* window; close it after 30 s.
3. Look at today's table in App B and compare it with the *Apps* group of Task-Manager.

**Expected result**

- None of these appear in the report: Runtime Broker, Suche / SearchHost, Shell Experience Host (Windows-Shell-Erfahrung), CTF-Ladeprogramm (ctfmon), Desktopfenster-Manager, Konsolenfenster-Host, Diensthost, **EagleEye** (tray client, also not while *App Infos* was open), **OneDrive** (tray icon only, no window).
- Every row in the report is an entry of Task-Manager's *Apps* group (or was, while it was open).
- If a program appears that Task-Manager lists under *Hintergrundprozesse*: note its name and whether it had a visible window then (known limit, ADR-011 §4); TES evaluates it (Note, not automatically a Fail).

#### TC-004-09: One app with several processes or windows counts once; minimised and parallel apps count

- **Verifies**: AC-6, AC-12 (minimised, parallel apps)
- **Machine / account**: Service PC / kid1; App B
- **Precondition**: Edge and Paint open from TC-004-06; note V0 of **Microsoft Edge**, **Paint** and **Editor**

**Steps**

1. In Edge open a **second window** (Strg+N) and a few tabs in each. Start **Editor** and open a **second Editor window** (Strg+Umschalt+N).
2. **Minimise** Paint. Stopwatch: wait **3 minutes** without locking.
3. Read V1 of the three apps. Optional: if **Visual Studio Code** is installed, open it with two windows during the same time and check it too.

**Expected result**

- **One** row each for Microsoft Edge and Editor (not one per window or process).
- Each of Edge, Editor and the minimised Paint grew by **3 minutes** (±1), not 6 (counted once despite two windows), and all three grew in parallel (AC-12: several apps each count fully).
- Close the second windows afterwards; leave Editor closed.

#### TC-004-10: Today's table: headers, sorting, "00:00"

- **Verifies**: AC-18, AC-19
- **Machine / account**: Service PC / kid1, then Admin (App A); App B
- **Precondition**: TC-004-04 to -09 done (several apps with usage today)

**Steps**

1. In kid1's session start **Zeichentabelle** (Windows+R → `charmap`) and close it after about **30 s**.
2. Close all apps of kid1. *Benutzer wechseln* → Admin. App A → *Berichte* → **Konto** `eagleeye-kid`. Screenshot → `evidence/run01-tc10-report.png`.
3. In *Einstellungen → Darstellung* switch to the other mode (Hell ↔ Dunkel), look at *Berichte* again, switch back.

**Expected result**

- Heading **"Heute, 08.10.2026"**, below it a table with the bold column headers **App** | **Nutzung (HH:MM)**; one row per app; durations as `HH:MM` (two digits each). <!-- verify against implementation report: alignment of the duration column -->
- Rows sorted by usage, **longest first**; equal values by name.
- **Zeichentabelle** is listed with **"00:00"** (less than one minute).
- Readable in light and dark mode; nothing cut off at 150 %.
- No older day (a fresh install has only today).

#### TC-004-11: Apps of an uncontrolled account and of the parent are not recorded

- **Verifies**: AC-1
- **Machine / account**: Service PC / kid2 (`ee-kid2`), Admin; Terminal (Administrator)
- **Precondition**: `ee-kid2` unticked

**Steps**

1. *Benutzer wechseln* → `ee-kid2`. Start Editor and Paint, use them **2 minutes**. Leave them open.
2. *Benutzer wechseln* → Admin. In your admin session start Editor, use it 1 minute, close it.
3. Terminal (Administrator): **AGENTS**; `query user`. LOG-FIND with `ee-kid2`; LOG-FIND with `account <your admin user name>`.
4. App A → *Berichte* → open **Konto**.

**Expected result**

- **No** agent with the SessionId of `ee-kid2` or of your admin session (only session 0 and eagleeye-kid's session).
- **No** log line names `ee-kid2` or your admin account (no "Usage recording started", no "App started", no "New app").
- `ee-kid2` is not in the **Konto** list; eagleeye-kid's values did not change.
- Leave `ee-kid2` signed in with Editor open (needed in TC-004-16).

#### TC-004-12: Two controlled accounts using the same app are kept apart

- **Verifies**: AC-26, AC-7 (own record per account)
- **Machine / account**: Service PC / kid3 (`ee-kid3`), Admin
- **Precondition**: App A *Berichte*: note eagleeye-kid's **Editor** value **K1**

**Steps**

1. *Benutzer wechseln* → `ee-kid3`. Start **Editor**; stopwatch; use it **2 minutes**; close it.
2. *Benutzer wechseln* → Admin. App A → *Berichte*: **Konto** `ee-kid3`, then **Konto** `eagleeye-kid`.
3. LOG-FIND with `New app for account ee-kid3`.

**Expected result**

- `ee-kid3`: **Editor** "00:02" (±1) and nothing else of eagleeye-kid's apps.
- `eagleeye-kid`: **Editor** still **K1** (not increased).
- Log: "New app for account ee-kid3: Editor (Notepad.exe, …)" — its own record, although eagleeye-kid has one for the same program.
- Sign `ee-kid3` out (*Task-Manager → Benutzer* → `ee-kid3` → *Abmelden*).

#### TC-004-13: The kid notices nothing

- **Verifies**: AC-27
- **Machine / account**: Service PC / kid1 (observation during Block B)
- **Precondition**: Block B so far

**Steps**

1. Think back over Block B in kid1's session.

**Expected result**

- No app was closed, blocked or noticeably slowed down by EagleEye.
- The tray client showed no new message, popup or icon change.
- No EagleEye window appeared. (The agent is visible only in Task-Manager → *Details* as `EagleEye.Service.exe` with user SYSTEM; that is accepted, plan Q-2.)

### Block C — Reports page states and account changes (core)

*Service PC · Admin; App A on "Berichte". Log monitor running.*

#### TC-004-14: Not connected, then connected again: "Keine Daten verfügbar", then the stored usage

- **Verifies**: AC-20 (not connected, loading), AC-21 (on reconnect)
- **Machine / account**: Service PC / Admin, Terminal (Administrator)
- **Precondition**: App A *Berichte*, **Konto** `eagleeye-kid`; note the shown values (screenshot)

**Steps**

1. `Stop-Service -DisplayName "EagleEye Service"`. Watch the page until the status bar is red.
2. `Start-Service -DisplayName "EagleEye Service"`. Do not touch the app. Watch the page closely.

**Expected result**

- Step 1: the page shows **"Keine Daten verfügbar"**, **no** selection *Konto*, no days, no tables.
- Step 2: green within 60 s; then the page shows **Konto** and the same days and values as before (they cannot have grown: no kid session is in front), without any action.
- *Note only*: whether **"Wird geladen …"** was visible in between (it may be too short to see).

#### TC-004-15: Selecting another account shows that account's usage

- **Verifies**: AC-21
- **Machine / account**: Service PC / Admin
- **Precondition**: TC-004-14

**Steps**

1. **Konto** → `ee-kid3`. Then **Konto** → `eagleeye-kid`.

**Expected result**

- `ee-kid3`: only **Editor** (TC-004-12). `eagleeye-kid`: its own apps from Block B. Each change shows the right data at once (after at most a short "Wird geladen …").

#### TC-004-16: Ticking starts recording, unticking stops it; the selection follows; data is kept

- **Verifies**: AC-2, AC-23, AC-9 (untick deletes nothing)
- **Machine / account**: Service PC / Admin, kid2; log monitor
- **Precondition**: `ee-kid2` signed in (session in the background) with Editor open (TC-004-11)

**Steps**

1. App A → *Einstellungen*: **tick** `ee-kid2`. Stopwatch. Watch the log monitor.
2. App A → *Berichte*: open **Konto**.
3. *Benutzer wechseln* → `ee-kid2`. Use Editor **2 minutes**. *Benutzer wechseln* → Admin. *Berichte* → **Konto** `ee-kid2`.
4. Keep **Konto** = `ee-kid2` selected. *Einstellungen*: **untick** `ee-kid2`. Stopwatch. Watch the log monitor, then *Berichte*.
5. *Einstellungen*: tick `ee-kid2` again. *Berichte* → **Konto** `ee-kid2`.
6. *Einstellungen*: untick `ee-kid2` again (it stays uncontrolled for the rest of the run). Sign `ee-kid2` out.

**Expected result**

- Step 1: within **60 s**: "Usage recording started for account ee-kid2 … in session N." and "App started: account ee-kid2, Editor …" for the **already open** Editor. (The plan expects ≤ 5 s.)
- Step 2: `ee-kid2` is in the **Konto** list within 60 s.
- Step 3: `ee-kid2` → **Editor** "00:02" (±1): counted from the tick on, only while its session was in front.
- Step 4: within **60 s**: "App ended: account ee-kid2, Editor … (monitoring stopped)" and "Usage recording stopped for account ee-kid2 …: monitoring stopped." `ee-kid2` disappears from **Konto**; the page selects the **first** account of the list.
- Step 5: `ee-kid2` is back with its **Editor** value from step 3 (data kept, OQ-9).
- *Optional*: instead of unticking in step 4, make `ee-kid2` an administrator (*Einstellungen → Konten → Andere Benutzer → Kontotyp ändern*) and back: same expected stop of recording within 60 s (AC-2).

#### TC-004-17: No account under parental control

- **Verifies**: AC-20 (no controlled account), AC-23
- **Machine / account**: Service PC / Admin
- **Precondition**: only `eagleeye-kid` and `ee-kid3` ticked

**Steps**

1. *Einstellungen*: untick `eagleeye-kid` and `ee-kid3`. *Berichte*.
2. *Einstellungen*: tick both again. *Berichte*.

**Expected result**

- Step 1: within 60 s the page shows **"Keine Konten unter Elternkontrolle. Konten unter Einstellungen auswählen."**, no **Konto** selection.
- Step 2: **Konto** is back with both accounts, the first selected, and their recorded usage unchanged.

### Block D — Sessions and service lifecycle

*Service PC. kid1's session with K-TERM (S-13) and LOG-WATCH in it. App B on LEOSERV on "Berichte" → `eagleeye-kid` (or App A afterwards).*

#### TC-004-18: After "Benutzer wechseln" the kid's open apps stop counting

- **Verifies**: AC-12 (session in the background)
- **Machine / account**: Service PC / kid1, Admin
- **Precondition**: kid1 in front; note V0 of **Editor**

**Steps**

1. Start Editor (kid1). Stopwatch. After **2 minutes** *Benutzer wechseln* → Admin (leave Editor open); pause the stopwatch.
2. Stay in the admin session for **3 minutes**. App A → *Berichte* → `eagleeye-kid`: read Editor.
3. *Benutzer wechseln* → `eagleeye-kid`; resume the stopwatch; after **1 more minute** close Editor. Read V1 (App B, or App A after switching).

**Expected result**

- Step 2: Editor = V0 + **2** (±1); it does not grow while you are in the admin session.
- Step 3: V1 − V0 = **3** (±1), not 6.

#### TC-004-19: Service stop and start while an app is open

- **Verifies**: AC-15, AC-10 (reasons)
- **Machine / account**: Service PC / kid1, K-TERM
- **Precondition**: kid1 in front; Paint open (start it); note V0 of **Paint** (App B)

**Steps**

1. Keep Paint open. After **1 minute** (stopwatch): in K-TERM `Stop-Service -DisplayName "EagleEye Service"`. Pause the stopwatch.
2. Wait **2 minutes** (keep using Paint).
3. K-TERM: `Start-Service -DisplayName "EagleEye Service"`; resume the stopwatch. After **2 more minutes** close Paint.
4. K-TERM: LOG-WATCH again (*Strg+C* first); LOG-FIND with `Paint`. Read V1.

**Expected result**

- Log at the stop: "App ended: account eagleeye-kid, Paint … (service stopping)" for Paint (and for every other open app of kid1).
- Log after the start: "Usage recording started for account eagleeye-kid …" and a **new** "App started: account eagleeye-kid, Paint …" for the still open Paint.
- V1 − V0 = **3** (±1): the 2 minutes without service are **not** counted.
- App B: red / "Keine Daten verfügbar" while the service is stopped, back by itself afterwards.

#### TC-004-20: Re-install of 0.4.0 keeps all recorded data

- **Verifies**: AC-9 (survives re-install), AC-15
- **Machine / account**: Service PC / Admin
- **Precondition**: App A *Berichte* → `eagleeye-kid`: screenshot of the values

**Steps**

1. Run `03_Delivery\windows\EagleEye-Setup-0.4.0.exe` again with defaults.
2. Wait until App A is green. *Berichte* → `eagleeye-kid`, then `ee-kid3`. Log monitor: *Strg+C*, LOG-WATCH.

**Expected result**

- Wizard without errors; App A green without a pairing code.
- Same days, apps and values as before for both accounts.
- No new "New app for account eagleeye-kid: Editor" line the next time kid1 uses Editor (the record is kept; check during the next case that uses Editor, write it in Notes there).

#### TC-004-21 (optional): Unexpected service stop

- **Verifies**: AC-15 (at most 5 s lost), AC-10 ("service stopped unexpectedly")
- **Machine / account**: Service PC / kid1, K-TERM
- **Precondition**: kid1 in front; Editor open for at least 1 minute

**Steps**

1. Note the clock time (seconds). K-TERM: `Stop-Process -Name EagleEye.Service -Force` (ends the service and its agents).
2. Wait 30 s. `Get-Service -DisplayName "EagleEye Service"`: if it is not *Running*, `Start-Service -DisplayName "EagleEye Service"`. Note whether Windows restarted it by itself.
3. LOG-WATCH again; LOG-FIND with `stopped unexpectedly`.

**Expected result**

- After the start: "App ended: account eagleeye-kid, Editor … (service stopped unexpectedly)". Its end time (from the duration, or the line) lies at most **5 s** before the time of step 1.
- A new "App started" line for the still open Editor.

#### TC-004-22 (optional): Sleep is not counted

- **Verifies**: AC-12, AC-15 (sleep)
- **Machine / account**: Service PC / kid1
- **Precondition**: kid1 in front; Editor open; note V0 (App B, or App A later)

**Steps**

1. Stopwatch 1 minute. Start → *Ein/Aus* → **Energie sparen**. Pause the stopwatch.
2. After **3 minutes**, wake the PC, unlock as `eagleeye-kid`; resume; after 1 more minute close Editor. Read V1.
3. LOG-FIND with `paused for`.

**Expected result**

- V1 − V0 = **2** (±1); the 3 minutes of sleep are not counted.
- *Note only*: a line "Usage accounting paused for … s …" may appear.

### Block E — Security checks *(sec)* (core security)

*Implementation plan Decision 1a and Manual Verification Notes "Security checks"; ADR-011 §7. They include DEV's admin checks A to F (implementation report §7; check G is in TC-004-20). No AC; a Fail is filed as an issue of US-004 with severity by impact.*

#### TC-004-23: The agent starts with the restricted SYSTEM token — which variant runs (DEV check A)

- **Verifies**: — (security: ADR-011 §7, threat T-5; plan Q-9; implementation report D-4)
- **Machine / account**: Service PC / Admin, Terminal (Administrator)
- **Precondition**: kid1 signed in (agent running, TC-004-03)

**Steps**

1. LOG-FIND with `<pattern>` = `Session agent|write restriction`.

**Expected result**

- After kid1's sign-in: "Session agent started in session N (process P).", "Usage recording started for account eagleeye-kid (S-1-5-21-…) in session N." and then "Session agent in session N: diagnostics: user NT-AUTORITÄT\SYSTEM (S-1-5-18), integrity System, privileges SeChangeNotifyPrivilege, write-restricted yes".
- User **SYSTEM**, integrity **System**, **only** `SeChangeNotifyPrivilege`.
- **Write-restricted**: either **yes** (preferred), or the accepted fallback (plan Q-9): a Warning "Session agents exit at start with a write-restricted token; they run without the write restriction from now on (ADR-011 §7, Q-9)." and later agents with `write-restricted no`. Both are a Pass. **Record which variant runs** in Notes (DEV and ARC need this answer).
- A Fail is: another user than SYSTEM, integrity other than System, any further privilege, or no diagnostic line at all.

#### TC-004-24: The kid cannot end the agent; the administrator can, and it is restarted (DEV check B)

- **Verifies**: — (security: T-4 process DACL, T-9 supervisor)
- **Machine / account**: Service PC / kid1 (normal PowerShell, **not** K-TERM), then Admin (Terminal (Administrator))
- **Precondition**: kid1 in front

**Steps**

1. Normal PowerShell as kid1:
   ```powershell
   $s = (Get-Process -Id $PID).SessionId
   $agent = Get-Process -Name EagleEye.Service | Where-Object SessionId -eq $s
   $agent | Select-Object Id, SessionId
   Stop-Process -Id $agent.Id -Force
   taskkill /F /PID $agent.Id
   ```
2. Task-Manager (kid1) → *Details* → `EagleEye.Service.exe` (user SYSTEM) → right-click → *Task beenden*.
3. Run the first three lines of step 1 again. Note the agent Id.
4. *Benutzer wechseln* → Admin. Terminal (Administrator): **AGENTS**, then `taskkill /PID <agent Id> /F`. Note the time. After 30 s: **AGENTS**; LOG-FIND with `Session agent`.

**Expected result**

- Step 1 lists one agent in kid1's session. `Stop-Process` and `taskkill` fail with **Zugriff verweigert** / *Access is denied*.
- Step 2: *Zugriff verweigert*. Step 3: the agent still runs with the **same Id**.
- Step 4: as administrator `taskkill` **works**. The log shows the Warning "Session agent in session N exited with code 1; recording is paused, restarting in 00:00:01." and a new "Session agent started in session N (process P2)."; **AGENTS** shows a new agent (other Id) in kid1's session within **1 to 30 s**.

#### TC-004-25: A program from a network share is recorded by its process name; the file is not opened (DEV check C)

- **Verifies**: — (security: T-2, `ProgramPathPolicy`, impersonation); AC-8 (fallback name)
- **Machine / account**: Service PC / Admin (share), then kid1
- **Precondition**: none

**Steps**

1. Terminal (Administrator) — create a test share on the service PC (a share on another PC works the same, implementation report §7 C):
   ```powershell
   New-Item -ItemType Directory -Force C:\EETest | Out-Null
   Copy-Item C:\Windows\System32\charmap.exe C:\EETest\charmap.exe
   $everyone = (New-Object System.Security.Principal.SecurityIdentifier "S-1-1-0").Translate([System.Security.Principal.NTAccount]).Value
   New-SmbShare -Name "eetest" -Path "C:\EETest" -ReadAccess $everyone
   ```
2. As `eagleeye-kid`: Windows+R → `\\localhost\eetest\charmap.exe` → Enter. If Windows asks *"Möchten Sie diese Datei ausführen?"*: *Ausführen*. Keep it open **1 minute**, then close it.
3. LOG-FIND with `charmap`; LOG-FIND with `WRN|ERR`. Report (App B or App A).

**Expected result**

- The report shows a row **"charmap"** (process name without `.exe`), **not** "Zeichentabelle" (the file on the share is never opened).
- Log: "New app for account eagleeye-kid: charmap (charmap.exe, <UNC path of the share>)". The exact form of the path (e.g. `\\localhost\eetest\charmap.exe`) does not matter.
- No Warning or Error about reading the file or resolving the name of the share program.

#### TC-004-26: A renamed copy named like EagleEye is recorded (DEV check D)

- **Verifies**: — (security: T-10, never-record list by install path); AC-5 (EagleEye processes excluded only by path)
- **Machine / account**: Service PC / kid1
- **Precondition**: none

**Steps**

1. Normal PowerShell as kid1:
   ```powershell
   $dst = Join-Path ([Environment]::GetFolderPath("Desktop")) "EagleEye.TrayClient.exe"
   Copy-Item C:\Windows\System32\charmap.exe $dst
   Start-Process $dst
   ```
2. Keep it open **1 minute**, close it. LOG-FIND (K-TERM) with `EagleEye.TrayClient.exe`. Report.

**Expected result**

- The program **is** recorded: a row in the report and "New app for account eagleeye-kid: … (EagleEye.TrayClient.exe, C:\Users\eagleeye-kid\…\Desktop\EagleEye.TrayClient.exe)" plus "App started …" in the log.
- Its row name comes from the copy's own version information, read by the managed reader because the copy is in a user folder (implementation report D-2): most likely **"Character Map"** (a System32 program keeps its localized name in a separate MUI file), possibly "Zeichentabelle"; if it has no description, "EagleEye.TrayClient". Any of these is a Pass; write the name in Notes.
- The real tray client (in `C:\Program Files\EagleEye\…`) is still never recorded (TC-004-08).

#### TC-004-27: Opening and closing Notepad 40 times gives at most 30 log lines plus a summary (DEV check E)

- **Verifies**: — (security: T-12, log flooding)
- **Machine / account**: Service PC / kid1, K-TERM
- **Precondition**: no other Editor use by kid1 in the hour after the start of this case. Plan the summary check (step 4) for about **65 minutes** later (e.g. during Block F); you do not have to wait idle.

**Steps**

1. In K-TERM note the start time: `$t0 = Get-Date; $t0`. Then in a **normal** PowerShell as kid1 (takes about 8 minutes):
   ```powershell
   1..40 | ForEach-Object { Start-Process notepad; Start-Sleep -Seconds 4; Stop-Process -Name Notepad -ErrorAction SilentlyContinue; Start-Sleep -Seconds 7 }
   ```
2. K-TERM, after the loop:
   ```powershell
   $lines = Select-String -Path "$env:ProgramData\EagleEye\logs\*.log" -Pattern "App (started|ended): account eagleeye-kid, Editor" |
     Where-Object { [datetime]::ParseExact($_.Line.Substring(0, 19), "yyyy-MM-dd HH:mm:ss", $null) -ge $t0 }
   $lines.Count
   ```
3. Report: Editor value of today (*Note only*: the increase; the loop keeps Notepad open about 40 × 4 s ≈ 3 minutes).
4. **About 65 minutes after `$t0`** (any elevated terminal): LOG-FIND with `further start/end entries`.

**Expected result**

- Step 2: `$lines.Count` is **at most 30** (without the limit it would be about 80).
- Step 4: one line "<n> further start/end entries of account eagleeye-kid, Editor (C:\…\notepad.exe) were not logged in the last hour." with n ≈ (number of start/end events) − 30.
- The report still shows Editor with a plausible increase.

#### TC-004-28: Debug switches have no effect in the installed (Release) service (DEV check F)

- **Verifies**: — (security: T-13; implementation report D-5, D-6)
- **Machine / account**: Service PC / Admin, Terminal (Administrator)
- **Precondition**: kid1 signed out or in the background

DEV's check F sets the variables machine-wide with `setx /M`. A machine-wide variable reaches a Windows service reliably only after a **reboot**; a service restart alone may not pass it on, and the check would then pass without testing anything. This case therefore gives the three Debug variables **directly to the service** through its registry value `Environment`, which Windows applies at every service start (§8 Q-8).

**Steps**

1. Terminal (Administrator):
   ```powershell
   $svc = (Get-Service -DisplayName "EagleEye Service").Name     # EagleEyeService
   $sid = ([Security.Principal.WindowsIdentity]::GetCurrent()).User.Value
   New-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Services\$svc" -Name Environment -PropertyType MultiString -Value @("EAGLEEYE_DEV_WATCH_SID=$sid", "EAGLEEYE_DEV_PORT_OFFSET=10000", "EAGLEEYE_DATA_DIR=C:\EETest\data") -Force
   Restart-Service -DisplayName "EagleEye Service"
   netstat -ano | findstr "5443 5080 15443 15080"
   ```
2. In your admin session start Editor, use it **1 minute**, close it. Wait 15 s.
3. **AGENTS**; `Test-Path C:\EETest\data`; LOG-WATCH again (new lines must appear in `%ProgramData%\EagleEye\logs`); LOG-FIND with `account <your admin user name>`. App A: still green? *Einstellungen*: is your admin account listed? *Berichte*: values unchanged?
4. **Remove the variables** and restart:
   ```powershell
   Remove-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Services\$svc" -Name Environment
   Restart-Service -DisplayName "EagleEye Service"
   ```

**Expected result**

- Ports unchanged: **5443** (0.0.0.0) and **5080** (127.0.0.1) listening; **nothing** on 15443/15080 (`EAGLEEYE_DEV_PORT_OFFSET` ignored).
- Your admin account does **not** appear in *Einstellungen* and is not recorded: no agent in your session, no log line names your account (`EAGLEEYE_DEV_WATCH_SID` ignored).
- `Test-Path C:\EETest\data` → **False**; the log continues in `%ProgramData%\EagleEye\logs`; App A stays paired with the same data (`EAGLEEYE_DATA_DIR` ignored).
- Agents of kid sessions still run as SYSTEM. After step 4 everything as before.

#### TC-004-29: A program renamed to explorer.exe is counted

- **Verifies**: — (security: T-10; implementation report D-1, `ExplorerWindow`); AC-4 (only the real Explorer is special)
- **Machine / account**: Service PC / kid1, K-TERM
- **Precondition**: none

**Steps**

1. Normal PowerShell as kid1:
   ```powershell
   $dir = Join-Path $env:USERPROFILE "EETest"
   New-Item -ItemType Directory -Force $dir | Out-Null
   Copy-Item C:\Windows\System32\charmap.exe (Join-Path $dir "explorer.exe")
   Start-Process (Join-Path $dir "explorer.exe")
   ```
2. Keep its window open **2 minutes** (stopwatch), close it. LOG-FIND (K-TERM) with `EETest\\explorer.exe`. Report.

**Expected result**

- The program **is** recorded with its own path: log "New app for account eagleeye-kid: … (explorer.exe, C:\Users\eagleeye-kid\EETest\explorer.exe)" and a report row with about "00:02" (±1). Its name is the copy's description (e.g. "Character Map") or "explorer"; write it in Notes.
- It is **not** merged into "Windows-Explorer", and the real "Windows-Explorer" row does not grow (no File Explorer window was open).

### Block F — Account deletion and long checks

#### TC-004-30: Deleting an account purges all its recorded data

- **Verifies**: AC-9 (purge on deletion), AC-23 (deleted account disappears)
- **Machine / account**: Service PC / Admin, Terminal (Administrator)
- **Precondition**: `ee-kid3` signed out, ticked, with usage (TC-004-12); App A *Berichte* → **Konto** `ee-kid3` selected

**Steps**

1. Terminal (Administrator):
   ```powershell
   Get-CimInstance Win32_UserProfile | Where-Object LocalPath -like "*\ee-kid3" | Remove-CimInstance
   Remove-LocalUser -Name "ee-kid3"
   ```
   Stopwatch.
2. Watch App A and the log monitor.
3. *Optional*: create `ee-kid3` again (S-9 commands for this one account), tick it, look at its report.

**Expected result**

- Within **60 s**: `ee-kid3` disappears from **Konto**; the page selects the first account (`eagleeye-kid`).
- Log: "Purged all recorded data of deleted account S-1-5-21-…: 1 apps, … history entries, … daily entries." <!-- verify against implementation report -->
- Step 3: the new `ee-kid3` (new SID) has **no** usage ("Heute keine Nutzung aufgezeichnet").

#### TC-004-31 (optional): A clock change neither adds nor removes usage

- **Verifies**: AC-16
- **Machine / account**: Service PC / kid1, K-TERM
- **Precondition**: do **not** run after 22:30 (the +1 h step would cross midnight). kid1 in front, Editor open; note V0

**Steps**

1. Stopwatch. After 1 minute, K-TERM: `Set-Date -Date (Get-Date).AddHours(1)`.
2. After 2 more minutes: `Set-Date -Date (Get-Date).AddHours(-1)`. After 1 more minute close Editor. Read V1.
3. K-TERM: `w32tm /resync` (if it fails, *Einstellungen → Zeit und Sprache → Datum und Uhrzeit → Jetzt synchronisieren* as Admin). Check the clock is right again.

**Expected result**

- V1 − V0 = **4** (±1): no jump of +60 or −60 minutes, no negative value.

#### TC-004-32 (optional): Older days and the 90-day limit (needs a SQLite tool)

- **Verifies**: AC-11, AC-18 (older days, descending), AC-19 (only days with usage, nothing older than 90 days)
- **Machine / account**: Service PC / Admin, Terminal (Administrator)
- **Precondition**: a SQLite command-line tool `sqlite3.exe` (or *DB Browser for SQLite*, started as admin). If none is available: **Skipped** (§8 Q-2). Run this case **near the end** of the run: it changes recorded data.

**Steps**

1. `Stop-Service -DisplayName "EagleEye Service"`. Back up the database: `Copy-Item "$env:ProgramData\EagleEye\EagleEye.Service.db" C:\EETest\db-backup.db`.
2. Move three of eagleeye-kid's daily rows into the past (Paint → yesterday, Microsoft Edge → 89 days ago, Rechner → 91 days ago):
   ```powershell
   $db = "$env:ProgramData\EagleEye\EagleEye.Service.db"
   sqlite3 $db "UPDATE DailyUsage SET Day = date('now','localtime','-1 day')  WHERE Day = date('now','localtime') AND AppId = (SELECT AppId FROM AppRecords WHERE DisplayName = 'Paint' LIMIT 1);"
   sqlite3 $db "UPDATE DailyUsage SET Day = date('now','localtime','-89 day') WHERE Day = date('now','localtime') AND AppId = (SELECT AppId FROM AppRecords WHERE DisplayName = 'Microsoft Edge' LIMIT 1);"
   sqlite3 $db "UPDATE DailyUsage SET Day = date('now','localtime','-91 day') WHERE Day = date('now','localtime') AND AppId = (SELECT AppId FROM AppRecords WHERE DisplayName = 'Rechner' LIMIT 1);"
   sqlite3 $db "SELECT r.DisplayName, d.Day, d.Seconds FROM DailyUsage d JOIN AppRecords r ON r.AppId = d.AppId ORDER BY d.Day;"
   ```
   <!-- verify against implementation report: table and column names (plan: migration 3) -->
3. `Start-Service -DisplayName "EagleEye Service"`. App A → *Berichte* → `eagleeye-kid`. LOG-FIND with `Purged usage data older`.

**Expected result**

- Days in this order: **"Heute, …"** (without Paint, Edge, Rechner), then **yesterday's date** with Paint, then the date **89 days ago** with Microsoft Edge. Nothing for 91 days ago.
- Log: "Purged usage data older than …: 1 daily entries, …" (the 91-day row is deleted at start).
- Days without usage between them are **not** shown.
- *Afterwards*: if you want the original data back: stop the service, `Copy-Item C:\EETest\db-backup.db "$env:ProgramData\EagleEye\EagleEye.Service.db" -Force`, start it.

#### TC-004-33 (optional, needs midnight): Usage across midnight is split; the history is not

- **Verifies**: AC-14
- **Machine / account**: Service PC / kid1; App B or App A
- **Precondition**: shortly before midnight; kid1 in front

**Steps**

1. At **23:55** start Editor (kid1); keep it open and unlocked until **00:05**; close it.
2. Read the report (yesterday and today). LOG-FIND with `Editor` (the last lines).

**Expected result**

- Yesterday's Editor value grew by about **5** minutes; **today** (new date) has Editor about "00:05" (±1 each).
- One "App started" and one "App ended" line with a duration of about 00:10:xx (the instance is not split).
- At midnight App B / App A moves to the new "Heute, …" heading by itself.

#### TC-004-34 (optional, next day): Yesterday becomes an older day

- **Verifies**: AC-18, AC-19
- **Machine / account**: Service PC / Admin
- **Precondition**: the day after the run (no TC-004-32 changes, or after them)

**Steps**

1. App A → *Berichte* → `eagleeye-kid`, before kid1 uses anything that day.

**Expected result**

- **"Heute, <today>"** with "Heute keine Nutzung aufgezeichnet"; below it the run day's date (without "Heute") with its table; older days only if they have usage.

#### TC-004-35 (optional): CPU load with 10 apps

- **Verifies**: AC-25
- **Machine / account**: Service PC / kid1
- **Precondition**: kid1 in front

**Steps**

1. Open **10** apps in kid1's session (e.g. Editor, Paint, Edge with a video, Rechner, Snipping Tool, Explorer, Zeichentabelle, Task-Manager, your game, one more program).
2. Task-Manager (kid1) → *Details*: look at the **two** `EagleEye.Service.exe` processes (service and agent) for **one minute**; note the CPU values about every 10 s.
3. Play the game or video for 1 minute. Then K-TERM `Stop-Service -DisplayName "EagleEye Service"`, play 1 minute again, `Start-Service -DisplayName "EagleEye Service"`.

**Expected result**

- The sum of both processes stays on average **below 2 %** (expected below 1 %).
- No visible stutter difference with and without the service.

## 6. Regression

None. No regression cases and no US-003 re-test (Michael, implementation plan Q-5; `02_Implementation/docs/testing/README.md` §3).

## 7. Cleanup

1. **LOG-COPY** with `<subfolder>` = `run-01-service-logs`. Look through the copies before committing.
2. **Test share** (TC-004-25): `Remove-SmbShare -Name "eetest" -Force`; `Remove-Item C:\EETest -Recurse -Force` (also the DB backup of TC-004-32 if you no longer need it).
3. **Renamed copy** (TC-004-26): as kid1 delete `EagleEye.TrayClient.exe` from kid1's desktop.
4. **Service Environment value** (TC-004-28): make sure it is gone: `Get-ItemProperty "HKLM:\SYSTEM\CurrentControlSet\Services\$((Get-Service -DisplayName 'EagleEye Service').Name)" -Name Environment -ErrorAction SilentlyContinue` → nothing.
5. **Clock** (TC-004-31): automatic time on, clock correct.
6. **Accounts**: sign out `eagleeye-kid`, `ee-kid2`. Remove `ee-kid2` (and `ee-kid3`, if re-created in TC-004-30):
   ```powershell
   foreach ($n in "ee-kid2","ee-kid3") {
     Get-CimInstance Win32_UserProfile | Where-Object LocalPath -like "*\$n" | Remove-CimInstance
     if (Get-LocalUser -Name $n -ErrorAction SilentlyContinue) { Remove-LocalUser -Name $n }
   }
   ```
7. **Selection**: `eagleeye-kid` stays ticked (recording continues; §8 Q-5), unless you prefer to untick it.
8. **LEOSERV**: App B stays installed and paired.

## 8. Notes and Open Questions for Michael

| ID | Question | Proposed answer |
|---|---|---|
| Q-1 | **LEOSERV as App B** for the live checks (TC-004-05) and for watching while the kid's session is in front. Is it available for the run? | Yes. Without it, TC-004-05 (AC-22) is Blocked; all other cases work with App A after switching back to Admin. |
| Q-2 | **SQLite tool for TC-004-32** (optional: older days, 90-day purge). Is `sqlite3.exe` or *DB Browser for SQLite* available on the service PC? | If not, skip TC-004-32; AC-11 then rests on DEV's unit tests, and the older-day display on TC-004-34 (next day). |
| Q-3 | **K-TERM**: an elevated terminal with your admin credentials inside the kid's session (S-13), for the log monitor and service commands while the kid's session is in front. OK? | Yes. It runs with admin rights, so it is not recorded (plan Q-4) and does not affect the kid's data. |
| Q-4 | **Temporary SMB share** `\\localhost\eetest` on the service PC (TC-004-25), read access for everyone, removed in cleanup. OK? | Yes. Alternative: a share on LEOSERV (then the kid needs LEOSERV credentials). |
| Q-5 | **After the run**: keep `eagleeye-kid` ticked (the service keeps recording its apps)? | Yes, keep it ticked; it is the intended product use. |
| Q-6 | **Accounts**: kid1 = existing `eagleeye-kid`, kid2 = new `ee-kid2`, kid3 = new `ee-kid3` (with a password, one first sign-in each in S-10; kid3 is deleted in TC-004-30). OK? | Yes. |
| Q-7 | **Core vs. optional**: optional are TC-004-21, -22, -29, -31 to -35. Their ACs (AC-11, AC-14, AC-16, AC-25, partly AC-18) would rest on unit tests if skipped. OK? | Yes. |

## 9. Change Log

| Date | Change | Reason |
|---|---|---|
| 2026-10-08 | Plan written (in parallel with DEV); places marked `<!-- verify against implementation report -->` to be aligned after DEV's handover. | Step d started early at Michael's request. |

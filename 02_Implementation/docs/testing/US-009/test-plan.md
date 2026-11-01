# Test Plan: US-009 — Windows Parent App on Windows 10, and Windows Parent App Parity (0.9.0)

**Status**: Draft, for approval by Michael
**Date**: 2026-11-01
**Author**: TES
**User Story**: `02_Implementation/docs/requirements/user-stories/US-009/user-story.md` (38 ACs in groups A to K, approved 2026-10-31, OQ-1 to OQ-7 resolved; status `Implemented`, step c approved by Michael 2026-11-01)
**Inputs used**: the user story; `02_Implementation/docs/requirements/user-stories/US-009/implementation-report.md` (§6 final "How to test": artifacts, SHA-256, starting points, install order, known limitations; §8.2 to §8.4 DEV check and Michael's M-checks M-1 to M-6; §9 Windows 10 pre-check; §11 Mac Step 5); `02_Implementation/docs/requirements/user-stories/US-009/implementation-plan.md` ("Manual Verification Notes", D-1 to D-3, Q-1 to Q-4, TI-9); `02_Implementation/docs/requirements/user-stories/US-008/user-story.md` (the referenced ACs), `US-008/implementation-report.md` (§6.3, §8.6 Michael's 12-point Mac check, §9 UI texts as implemented, D-9 to D-19); `02_Implementation/docs/testing/US-008/test-report.md` (F-1: gaps retested here); `02_Implementation/docs/testing/US-007/test-report.md` (F-1, not verified AC-25, AC-33); `02_Implementation/docs/testing/US-006/test-plan.md` (format, timing conventions, log commands); `02_Implementation/docs/testing/regression-checklist.md` (REG-001); `02_Implementation/docs/dev-process/dev-environments.md` (machines, Windows 10 test PC, Mac Local Network lesson); `02_Implementation/docs/testing/README.md` §4. Black box: no source code was read.

> **Why this run matters.** Three firsts in one run: (1) the Windows parent app has not been installed or run as a release build since 0.6.2; the US-007 and US-008 UI was only seen on WinUI in DEV's probe and in your M-checks. (2) Windows 10 has only had your short pre-check. (3) **US-008 was closed without a test run**: live group enforcement (warnings, blocking, closing all member apps, grace periods) has never run in a kid session. Session 6 does that for the first time.

> **Lessons applied.** German UI labels as you see them (US-001). Every case is marked **essential** or **optional** (US-004, US-005, story OQ-7). The result box is what counts. Timing conventions of US-006 (stopwatch on Enter, lap C on Tab/Enter). Mac: Local Network switch off/on after the update and `nc -z -G 3 <ip> 5443` instead of ping (`dev-environments.md` §5). A remote parent app changes rules while the kid's session is in front, because used time only counts while that session is in front (US-006 Q-1): here the **Mac** takes the role of LEOSERV.

---

## 1. Scope

**In scope**

- Update of the service and tray client 0.8.0 → 0.9.0, data kept (AC-30, AC-31, AC-36)
- Windows parent app 0.9.0 on **Windows 11**: fresh install and pairing (AC-33), the full group UI and look (groups A, C to F: AC-1, AC-10 to AC-23), texts (AC-28), regression spot checks (AC-34)
- Windows parent app 0.9.0 on **Windows 10**: install, repair, uninstall, refusals, connection, page spot check, look, language (group B: AC-2 to AC-9; AC-10, AC-11, AC-16, AC-28 on Windows 10)
- Mac app update 0.8.0 → 0.9.0 (AC-32) and the **US-008 Mac ACs in full** (AC-35)
- Two parent apps at once, both directions (AC-24 to AC-26; US-007 AC-25, AC-33)
- Group rule from the Windows app takes effect; kid texts (AC-27, AC-29)
- **Retest of US-008** service behaviour (AC-37: US-008 AC-19 to AC-34, AC-38 to AC-40), US-008 test report F-1
- Optional pilot checks (AC-38: US-008 AC-41, live group smoke)

**Out of scope** (story "Out of Scope"): service and tray client on Windows 10 (only the refusal, AC-5); Windows 10 older than 22H2, 32-bit, ARM, S mode, LTSC (AC-4 only if available); mixed versions (a 0.9.0 parent app with a 0.8.x service, the Mac app 0.8.0 with the service 0.9.0); update of the Windows app from 0.6.2; new features (paired-device list, "used vs. budget", events); touch-screen drag-and-drop; Windows-only extras (notifications, Mica, rounded corners); code signing (SmartScreen warns, expected); full re-verification of US-006 service behaviour; US-007 Mac-only ACs (removal script, sleep/Wi-Fi, Local Network deny).

**Not tested in this run, with reason**

| Item | Reason |
|---|---|
| Regression of US-001 to US-007 beyond the story's spot checks | Story runs contain no regression cases (`02_Implementation/docs/testing/README.md` §3). REG-001 is part of AC-31 and therefore in the run. |
| AC-4 (older Windows 10 / 32-bit refused) | Needs such a Windows. TC-009-35 records "Skipped: not available" unless you have one (story: "otherwise TES records not tested"). |
| US-008 AC-6 (no groups after an update from 0.7.x) | Cannot be produced: the service comes from 0.8.0, and no 0.7.x service exists any more. Evidence of the 0.8.0 install: `US-008/evidence/install-0.8.0-log-excerpt.txt` ("App groups loaded: 0 groups …"). See §8 Q-6. |
| US-008 AC-20 midnight split, AC-39 purge after deleting an account | Midnight cannot be waited for in a session; deleting an account is not needed for the story. Unit tests cover both. Locked time and the service restart parts of AC-20 are tested (TC-009-66, TC-009-67). |
| US-008 AC-41 performance with 20 groups / 100 apps; live group smoke of the US-008 plan | AC-38: optional pilot checks (TC-009-71). "Not tested" does not block closing. |
| Update path of the Windows app from 0.6.2 | Out of scope (story OQ-5, R-2). |

## 2. Test Environment

| Item | Value |
|---|---|
| **Service PC** | Windows Developer Machine, Windows 11, `192.168.1.37` (check in S-3). Service and tray **0.8.0** installed (starting point of AC-31). German Windows. Admin account = you. |
| **W11 app** | Windows parent app 0.9.0, installed **fresh** in your admin account on the service PC (TC-009-03). Device name **Papas PC**. |
| **W10 PC** | Your physical Windows 10 22H2 PC (build 19045+, x64), same LAN. **No EagleEye installed** (pre-check ended with an uninstall). Device name **Windows 10 PC**. |
| **Mac app** | Mac app **0.8.0** in `/Programme` on the MacBook (starting point of AC-32), paired with the service. Updated to 0.9.0 in TC-009-37. |
| **Accounts on the service PC** | **kid1**, **kid2** = standard accounts **under parental control**, **kid3** = standard account **not** under parental control (story names). Which real accounts play these roles is fixed in S-5 (proposal: kid1 = `eagleeye-kid`). kid2 should have **English** Windows display language for AC-29 (S-6). |
| **Builds under test** | `03_Delivery/windows/EagleEye-Setup-0.9.0.exe` (service + tray, Windows 11 only; 68.0 MB; SHA-256 `4F1EF797A96FB6F5E1DCF9D5C0E298C3AE16C0C7C630BC15A6ED6C9F47CB1652`) · `03_Delivery/windows/EagleEye-ParentApp-Setup-0.9.0.exe` (Windows app, Windows 10 22H2 / 11, x64, per user; 70.9 MB; SHA-256 `780CE6CB9E41D58E95EDEA5B37FA2D631E3E7EBC46E81EE66BCD0CC43A1D88F0`) · `03_Delivery/macos/EagleEye-ParentApp-0.9.0.dmg` (28.6 MB; SHA-256 `9f397006aff57245f7dd63192b5154bd2ba9086328c3df8c3371f52f2c5fdde6`). All git-ignored: the two `.exe` exist only on the Windows Developer Machine (copy them to the W10 PC), the `.dmg` only on the MacBook. |
| **Expected versions** | *Installierte Apps* / *Apps & Features*: **EagleEye 0.9.0**, **EagleEye Parent App 0.9.0**, publisher **Michael Adler**. Tray *App Infos*: **Server-Version: EagleEye_v0.9**. Mac: Finder *Informationen* and "Über EagleEye Parent App": **0.9.0**. |
| **Tools** | Phone stopwatch with lap times. **Terminal (Administrator)** in your admin session on the service PC. **K-TERM** = elevated terminal inside the kid's session, started with your admin credentials (never limited). Seconds in the kid's taskbar clock (from US-005/US-006). USB stick or share for the W10 PC. Mouse; touchpad where the machine has one. |

### Conventions for expected results

- **UI texts.** German, as implemented (US-008 report §9; US-009 adds **no** new texts, Q-1). The story makes **wording differences between the Windows app and the Mac app a Fail** (parity, AC-28). Differences from the English examples in the story are Notes. Exception agreed with you: the App Groups explanation says **"Maus oder dem Trackpad"** on Windows too (plan Q-1): **not** an issue.

  | Where | Expected (German / English) |
  |---|---|
  | Menu | **Einstellungen**, **App Gruppen**, **Regeln**, **Berichte** / Settings, App Groups, Rules, Reports |
  | App Groups explanation | "Hier kannst du Apps, die zusammengehören, in eine Gruppe legen, z. B. ein Spiel mit seinem Launcher. Auf der Seite Regeln legst du dann für jedes deiner Kinder ein Zeit-Budget für die ganze Gruppe fest. Eine neue Gruppe ist gesperrt, bis du ihre Regel einstellst. Jede App kann nur in einer Gruppe sein. Apps verschiebst du mit der Maus oder dem Trackpad, mit ≡ oder mit einem Rechtsklick." |
  | App Groups | **Gruppe hinzufügen** / Add group · **In keiner Gruppe** / Not in any group · **Keine Apps in dieser Gruppe. Apps hierher ziehen.** / No apps in this group. Drag apps here. · **Alle Apps sind in einer Gruppe.** / All apps are in a group. · **Verschieben nach …** / Move to … · tooltips **Umbenennen** / Rename (pencil), **Löschen** / Delete (trash can) |
  | Name dialog | title **Neue App-Gruppe** / New app group, **App-Gruppe umbenennen** / Rename app group · field **Name** · **OK**, **Abbrechen** / Cancel · **Bitte einen Namen mit 1 bis 40 Zeichen eingeben.** / Enter a name with 1 to 40 characters. · **Es gibt bereits eine Gruppe mit diesem Namen.** / A group with this name already exists. |
  | Delete dialog | title **App-Gruppe löschen** / Delete app group · "Gruppe „Games“ löschen? Ihre Apps kommen nach „In keiner Gruppe“ und verwenden wieder ihre eigenen Applikations-Regeln." / Delete the group "Games"? Its apps move to "Not in any group" and use their own app rules again. · **Löschen**, **Abbrechen** |
  | Save error | **Die Änderung konnte nicht gespeichert werden. Bitte erneut versuchen.** |
  | Rules | heading **App-Gruppen-Regeln** / App group rules, between **Ruhezeiten** and **Applikations-Regeln** · explanation "Hier legst du ein Zeit-Budget pro App-Gruppe fest. Alle Apps einer Gruppe teilen es sich: Die Zeit zählt, solange mindestens eine App der Gruppe verwendet wird. Die Gruppen legst du auf der Seite App Gruppen fest." · columns **App-Gruppe** · **Stunden** · **Minuten** · **MO … SO** · **(keine Apps)** / (no apps) · **Noch keine App-Gruppen. Du kannst sie auf der Seite App Gruppen anlegen.** · sentence under Applikations-Regeln: "Apps in einer App-Gruppe werden unter App-Gruppen-Regeln eingestellt." · **Bitte Stunden zwischen 0 und 23 eingeben.** · **Bitte Minuten zwischen 0 und 59 eingeben.** |
  | Reports | under each day: **App-Gruppen** / App groups, columns **App-Gruppe** · **Nutzung (HH:MM)** |
  | Page states | **Keine Daten verfügbar** · **Keine Konten unter Elternkontrolle. Konten unter Einstellungen auswählen.** · **Wird geladen …** |
  | Status bar | green **Verbunden mit 192.168.1.37** · red **Nicht verbunden mit 192.168.1.37** · red **Verbindung zu 192.168.1.37 wird hergestellt …** |
  | Kid dialog info lines (group "Test") | "Test: noch 3 Minuten." · "Test: noch 1 Minute." · "Test: Zeit-Budget aufgebraucht." · "Test: heute nicht erlaubt." · "Test: Deine Eltern haben die Regeln geändert. Test wird in 1 Minute geschlossen." (English kid: "Test: 3 minutes left.", "Test: time budget used up.", "Test: not allowed today.") — wording differences in the kid dialog are Notes; **the group name must appear** (Fail if the app name appears instead, except after a group was deleted). |

- **Timing** (story limits): a change made in a parent app shows in the page and in the other parent app **≤ 5 s**; a new app appears **≤ 15 s**; today's usage follows **≤ 15 s**; reconnect: green **≤ 30 s** after start, red **≤ 30 s** after the service stops, green **≤ 60 s** after it starts.
- **Enforcement timing** (US-006 conventions, US-008 AC-25 to AC-27, AC-32): stopwatch **on Enter** in *Ausführen* (Windows+R). An event "at X of group time" is expected between **X and X + 15 s**. Apps gone by **end + 35 s**. **Grace**: lap **C** = your Tab/Enter or click in the Mac app; final warning ≤ **C + 15 s**; close between **C + 60 s** and **C + 75 s**; gone ≤ **C + 95 s**; closed **before C + 55 s** = Fail.
- **Used time counts only while the kid's session is unlocked and in front.** That is why the parent app on the **service PC** cannot watch live usage while the kid uses apps: on Windows 11 the live-follow checks are done by "switch back and look without touching anything"; the timed live checks are done on the Mac (same shared code; §8 Q-5).
- **Unexpected behaviour** that the story does not specify is a Note; TES routes it to PRO.

### Already covered before this run (M-checks, Windows 10 pre-check)

Nothing is dropped. Where an earlier check covers a point, the case is kept and marked **re-confirm** (a quick look, ≤ 2 minutes) or **optional**.

| Earlier check (implementation report) | Covers | In this plan |
|---|---|---|
| M-1 (Debug app paired, restart → Berichte) | AC-10 start page | TC-009-04: re-confirm on the release build (needed anyway for AC-33) |
| M-2 (touchpad and mouse drag, live data) | AC-16 on W11 | TC-009-14 mouse: **essential** (core of AC-16, release build); TC-009-15 touchpad: **optional** re-confirm |
| M-3 (tooltips pencil / trash can) | AC-14 tooltips | TC-009-11: re-confirm |
| M-4 (accent change while running) | AC-11 last sentence | TC-009-07 step 4: **optional** step |
| M-5 (Berichte after a move without "Wird geladen …") | AC-23 | TC-009-24: re-confirm; the cross-app part is new (TC-009-51) |
| Pre-check rows 1, 2 (winver; service installer refused) | AC-5 | TC-009-26: re-confirm (1 minute) |
| Pre-check rows 3 to 6 (install, pair, four pages, unpair, uninstall) | parts of AC-2, AC-3, AC-6, AC-7 | TC-009-27 to TC-009-34: **essential**: the pre-check was recorded as a one-line summary, without the details the ACs ask for (no admin prompt, publisher, repair, wrong code, timings, data removed) |
| Michael's 12-point Mac check on 0.8.0 (US-008 report §8.6) | US-008 AC-1 to AC-18, AC-35 to AC-37, AC-43 on the Mac | TC-009-38 to TC-009-48: kept as **quick re-confirm** cases (AC-35 asks for them in full on 0.9.0; DEV states no shared code changed, §11.2) |

### Service log as evidence

The service log is in the admin-only folder `%ProgramData%\EagleEye\logs\`, file `EagleEye.Service-NNN.log`. Commands need **Terminal (Administrator)** or K-TERM.

**LOG-COPY** (at the end of each session day, and **right after any Fail**; `<subfolder>` = `run-01-service-logs` or `run-01-service-logs\after-TC-009-NN`):

```powershell
Set-Location <your EagleEye repo folder>
$dst = Join-Path "02_Implementation\docs\testing\US-009\evidence" "<subfolder>"
New-Item -ItemType Directory -Force $dst | Out-Null
Copy-Item "$env:ProgramData\EagleEye\logs\*.log" $dst -Force
Get-ChildItem $dst
```

**LOG-WATCH** (after every service restart or re-install: *Strg+C* and run again):

```powershell
$log = Get-ChildItem "$env:ProgramData\EagleEye\logs\EagleEye.Service-*.log" | Sort-Object LastWriteTime | Select-Object -Last 1
Get-Content $log.FullName -Tail 15 -Wait
```

**LOG-FIND** (replace `<pattern>`):

```powershell
Select-String -Path "$env:ProgramData\EagleEye\logs\*.log" -Pattern "<pattern>" | Select-Object -Last 15 | ForEach-Object Line
```

Useful patterns: `App groups loaded` (start), `app group|app moved|group rule` (group changes, US-008 AC-14, AC-16), `group Test|Test` (enforcement, US-008 AC-31), `Grace period`, `Blocked start`, `App closed`. Group change lines name the parent app's **device name** (Papas PC, Windows 10 PC, the Mac's name). Wording differences are Notes; the content named in the AC (action, group, app, account) is the Pass criterion. Must **not** appear: any `[ERR]` line, `could not be terminated`.

Look through the copies before committing (user names, program paths, SIDs; the service never logs secrets or pairing codes).

### Sessions — order, machines, essential vs. optional

Run the sessions in this order. Breaks between sessions (also days) are fine, with one rule: **Session 6B needs a day on which kid1 has not yet used Editor or Paint** (group time counts the whole day, US-008 AC-22).

| Session | Cases | Machine / accounts | Essential + optional | Duration (essential, + optional) |
|---|---|---|---|---|
| **0** Setup | S-1 to S-12 | Service PC, MacBook, W10 PC | — | ≈ 35 min (+ ≈ 15 min if kid2 needs English) |
| **1** Service update 0.8.0 → 0.9.0 | TC-009-01, -02 | Service PC / Admin, kid1 | 2 + 0 | ≈ 20 min |
| **2** Windows app on Windows 11 | TC-009-03 to -25 | Service PC / Admin (kid1 briefly in TC-009-24) | 20 + 3 | ≈ 2 h (+ 25 min) |
| **3** Windows 10 PC | TC-009-26 to -36 | W10 PC; service PC for the pairing code and the service stop | 9 + 2 | ≈ 1 h 20 (+ 15 min) |
| **4** Mac update and US-008 Mac ACs | TC-009-37 to -48 | MacBook; service PC (code popup, log) | 10 + 2 | ≈ 1 h 15 (+ 20 min) |
| **5** Two parent apps at once | TC-009-49 to -54 | Service PC / Admin (W11 app) and MacBook side by side | 6 + 0 | ≈ 45 min |
| **6A** Windows app regression | TC-009-55, -56 | Service PC / Admin | 2 + 0 | ≈ 20 min |
| **6B** Group enforcement, US-008 retest | TC-009-57 to -67 | Service PC / kid1, kid2, kid3 (K-TERM); **MacBook** as remote parent app | 8 + 3 | ≈ 1 h 15 (+ 30 min) |
| **6C** Storage and accounts | TC-009-68 to -70 | Service PC / Admin; MacBook | 2 + 1 | ≈ 15 min (+ 10 min) |
| **6D** Optional pilot checks, clean-up check | TC-009-71, -72 | Service PC | 0 + 2 | (pilot; + 5 min) |

**72 cases: 59 essential, 13 optional.** Essential total ≈ **8 h** including setup, best split over two or three days (e.g. day 1: Sessions 0 to 2; day 2: 3 to 5; day 3: 6). All optional cases add ≈ 2 h.

**A shorter run**, if time runs out: Sessions 1, 2 and 6B carry the highest risk (first release run on WinUI, first live group enforcement). Session 4 is lowest risk (Mac code unchanged since 0.8.0, §11.2) but needed for AC-32 and AC-35.

**Optional coverage** (an AC resting on optional cases only): **none of US-009's own ACs** except AC-4 (TC-009-35, needs an old Windows) and AC-38 (pilot). Inside AC-37 (the US-008 retest), these US-008 ACs rest on optional cases: AC-28 (break wins, TC-009-65), AC-33 (early warning on a change, TC-009-64), and parts of AC-34 (rename, harmless move: TC-009-64) and AC-20 (locked time: TC-009-66). The save-error path of AC-18 / US-008 AC-14 rests on optional TC-009-18 and TC-009-44 (it needs the service process suspended).

### Fixed test data

| Item | Value |
|---|---|
| Warning levels (Einstellungen) | **Frühe Warnung 3**, **Finale Warnung 1** (set in S-9); texts default |
| Baseline group (S-10, created with the Mac app 0.8.0) | **Rechnen** = Rechner; App-Gruppen-Regeln kid1 **0:30, all days**; kid2 0:00 |
| Session 2 groups | **Games**, **Musik 🎮**, **School** (TC-009-09, -12); deleted in TC-009-13; **Games** and **School** are created again in TC-009-14 and stay for the rest of the run |
| Session 6B group | **Test** = Editor (Notepad) + Paint; kid1 **0:05, today ticked**; kid2 **0:00, no day**; kid1's own rule for **Paint 0:00, no day** (US-008 AC-24) |
| Emojis | 🎮 and 😀 only (TI-9: the Windows 10 font lacks the newest emojis) |
| Test programs (Windows+R in the kid's session) | `notepad` (Editor), `mspaint` (Paint), `calc` (Rechner), new app for TC-009-59: `colorcpl` (Farbverwaltung; if already listed: `odbcad32`) |

## 3. Setup Instructions

**Session 0 — before Session 1** (≈ 35 min). Do S-1 to S-10 **before** the service update; the Mac app 0.8.0 is only used here (S-10), then not again until it is updated (no mixed versions).

1. **S-1 Installers present** (service PC, PowerShell, repo root): `Get-FileHash 03_Delivery\windows\EagleEye-Setup-0.9.0.exe, 03_Delivery\windows\EagleEye-ParentApp-Setup-0.9.0.exe | Format-List Path, Hash` → as in §2. MacBook: `shasum -a 256 03_Delivery/macos/EagleEye-ParentApp-0.9.0.dmg` → as in §2.
2. **S-2 Starting points**:
   - Service PC, *Einstellungen → Apps → Installierte Apps* → "EagleEye": **EagleEye 0.8.0** only, **no "EagleEye Parent App"** (AC-33 fresh install). `services.msc` → **EagleEye Service** *Wird ausgeführt*.
   - MacBook: Finder → *Programme* → EagleEye Parent App → *Informationen*: **0.8.0**.
   - W10 PC: *Einstellungen → Apps → Apps & Features*: **no** "EagleEye Parent App"; `%LocalAppData%\EagleEye` does not exist.
   - Anything different → **stop** and tell TES.
3. **S-3 Service PC address**: Terminal: `ipconfig` → IPv4 of the LAN adapter. Expected **192.168.1.37**. If different, use the real address everywhere this plan says 192.168.1.37 and write it into the run file header.
4. **S-4 Copy to the W10 PC**: both `.exe` from `03_Delivery\windows\` onto a USB stick or share. On the W10 PC (PowerShell): `Get-FileHash <file>` → as in §2. Network check from the W10 PC: `Test-NetConnection 192.168.1.37 -Port 5443` → *TcpTestSucceeded: True* (a failed ping proves nothing).
5. **S-5 Accounts on the service PC** (Terminal (Administrator): `Get-LocalUser | Format-Table Name, Enabled -AutoSize`). Fix the roles and write them into the run file: **kid1** = ______ (proposal `eagleeye-kid`), **kid2** = ______, **kid3** = ______. If kid2 or kid3 does not exist, create it (as in US-006 S-5):
   ```powershell
   $pw = Read-Host -AsSecureString "Password for the new kid account"
   $usersGroup = (Get-LocalGroup -SID "S-1-5-32-545").Name
   $n = "<name>"
   New-LocalUser -Name $n -Password $pw -PasswordNeverExpires | Out-Null
   Add-LocalGroupMember -Group $usersGroup -Member $n
   ```
   Sign in once as each new account (*Benutzer wechseln*), wait for the desktop, sign out. kid1 and kid2 must have used **Editor**, **Paint** and **Rechner** at least once (story test setup); a new controlled account gets deny-by-default, so use the Mac app 0.8.0 (S-10) to tick it and give these three apps a rule, or skip: TES only needs them listed.
6. **S-6 English for kid2** (for AC-29; ≈ 15 min, optional if not possible): *Einstellungen* is blocked for a controlled account (deny by default). In the Mac app 0.8.0 **untick kid2** → sign in as kid2 → *Einstellungen → Zeit und Sprache → Sprache und Region → Windows-Anzeigesprache* → **English (United States)** (install the language pack in your session first, if needed) → sign out and in again → check "Settings" in the Start menu → sign out → **tick kid2** again in the Mac app. Not possible → note it; the English part of TC-009-60 is then **Blocked**.
7. **S-7 Clock seconds**: kid1's taskbar shows seconds (set since US-005; if not: untick kid1 as in S-6, set *Taskleiste → Sekunden in der Uhr der Taskleiste anzeigen*, tick kid1 again).
8. **S-8 Terminal (Administrator)** on the service PC: Start → **Terminal** → right-click → **Als Administrator ausführen**. A second one with **LOG-WATCH**.
9. **S-9 Warning levels** (Mac app 0.8.0 → *Einstellungen → Zeit-Ende Warnungen*): **Frühe Warnung 3**, **Finale Warnung 1**. Write the old values into the run file if you want them back later.
10. **S-10 Baseline before the update** (Mac app 0.8.0; screenshots into `evidence/`):
    - *App Gruppen*: if the group **Rechnen** does not exist, create it (**Gruppe hinzufügen** → `Rechnen` → **OK**) and move **Rechner** into it. Remove all other groups you do not need (write their names into the run file if you keep any).
    - *Regeln* → **Konto** kid1 → *App-Gruppen-Regeln*: **Rechnen** **Stunden 0**, **Minuten 30**, **MO to SO** ticked.
    - Switch to **kid1** → Windows+R `calc` → use it **2 minutes** → close it → sign kid1 out.
    - Mac app: *Berichte* → kid1: today shows **App-Gruppen** with **Rechnen 00:02** (±1).
    - Screenshots: `evidence/s10-appgroups-0.8.0.png`, `s10-grouprules-kid1-0.8.0.png`, `s10-reports-kid1-0.8.0.png` (scroll: every day that shows "App-Gruppen"). Write the group lines of **past days** into the run file (or "none").
    - **Quit the Mac app (Cmd+Q).** Do not start it again until TC-009-37.
11. **S-11 SmartScreen** on the unsigned installers: *Weitere Informationen* → *Trotzdem ausführen* (expected, ADR-009).
12. **S-12 All kid accounts signed out** (*Task-Manager → Benutzer*: only you).

**Before Session 6B** (≈ 10 min) — P-1 to P-6 are in the session header (§5, Session 6B).

## 4. Acceptance Criteria Coverage

Cases in *italics* are optional.

| AC | Test case(s) |
|---|---|
| AC-1 parity rule | TC-009-04, -06 to -24 (W11: every referenced US-008 parent-app AC and D-9, D-12 to D-16, D-18, D-19); detailed mapping in §4.1 |
| AC-2 install on W10 | TC-009-27 |
| AC-3 Apps & Features, repair, uninstall | TC-009-27, -33, -34 |
| AC-4 older Windows refused | *TC-009-35* (not tested unless available) |
| AC-5 service installer refuses W10 | TC-009-26 |
| AC-6 connection from W10 | TC-009-28, -29 |
| AC-7 four pages on W10 | TC-009-30 |
| AC-8 look on W10 | TC-009-31 |
| AC-9 language on W10 | TC-009-32, *-36* |
| AC-10 menu, start page | TC-009-04, -06 (W11); TC-009-28, -31 (W10) |
| AC-11 outlined headings, accent | TC-009-07 (W11); TC-009-31 (W10) |
| AC-12 light/dark, 100 %/150 %, drop highlight | TC-009-08, -14 |
| AC-13 App Groups content and states | TC-009-09, -10, -59 (new app, observed in the Mac app and after switch-back in the W11 app) |
| AC-14 pencil / trash-can buttons | TC-009-11 |
| AC-15 create, rename, delete | TC-009-12, -13 |
| AC-16 drag-and-drop | TC-009-14, *-15* (W11); TC-009-30 (W10) |
| AC-17 "≡", right-click | TC-009-16 |
| AC-18 saved at once, log, error | TC-009-17, *-18* |
| AC-19 App-Gruppen-Regeln section | TC-009-19, *-72* ("no app groups yet" text) |
| AC-20 editing group rules | TC-009-20 |
| AC-21 Applikations-Regeln only ungrouped; "Wird geladen …"; live from App Groups | TC-009-21, -22, -51 |
| AC-22 Reports "App-Gruppen" lines | TC-009-23, -58 (sorting, union, same as Mac) |
| AC-23 Reports follow usage and group changes | TC-009-24, -52 |
| AC-24 two apps paired, remove pairing independent | TC-009-49, -54 |
| AC-25 group changes sync both directions, last wins | TC-009-50, -51, -52 |
| AC-26 existing settings sync both directions | TC-009-53 |
| AC-27 group rule from the Windows app takes effect | TC-009-57 |
| AC-28 texts de/en, same as Mac | TC-009-06, -09, -12, -13, -16, -18, -19, -21, -23 (German, W11); TC-009-32 (English, W10); *TC-009-25* (English, W11); TC-009-47 *(Mac English reference)* |
| AC-29 kid texts naming a group, German and English | TC-009-57, -58 (German), TC-009-60 (English) |
| AC-30 versions 0.9.0 | TC-009-01, -02 (service, tray); TC-009-03 (W11); TC-009-27 (W10); TC-009-37 (Mac) |
| AC-31 service update keeps data; REG-001 | TC-009-01, -02, -05 |
| AC-32 Mac update keeps pairing and appearance | TC-009-37 |
| AC-33 fresh install on W11, pairing | TC-009-03, -04 |
| AC-34 Windows app regression spot checks | TC-009-55, -56, -24 (today's usage) |
| AC-35 Mac = 0.8.0; US-008 Mac ACs in full | TC-009-38 to -48, -59 (US-008 AC-4); see §4.1 |
| AC-36 service and tray = 0.8.0 | TC-009-01, -02, -57 |
| AC-37 US-008 retest (service) | TC-009-57 to -70; see §4.1 |
| AC-38 optional pilot checks | *TC-009-71* |

### 4.1 US-008 ACs (parity on Windows via AC-1, Mac via AC-35, service via AC-37)

| US-008 AC | Windows app (AC-1) | Mac app (AC-35) | Service (AC-37) |
|---|---|---|---|
| AC-1 menu | TC-009-06 | TC-009-38 | — |
| AC-2, AC-3 page content | TC-009-09 | TC-009-39 | — |
| AC-4 new app ≤ 15 s | TC-009-59 (switch-back) | TC-009-59 (timed) | — |
| AC-5 page states, look | TC-009-08, -10 | TC-009-38 | — |
| AC-6 no groups after 0.7.x update | — | not testable (§1, §8 Q-6) | — |
| AC-7 to AC-10 create, rename, delete | TC-009-12, -13 | TC-009-40 | — |
| AC-11 to AC-13 moving | TC-009-14 to -16 | TC-009-41 | — |
| AC-14 saved at once, log, error | TC-009-17, *-18* | TC-009-42, *-44* | — |
| AC-15 to AC-17 Rules | TC-009-19 to -22 | TC-009-43 | — |
| AC-18 Rules live after App Groups change | TC-009-51 | TC-009-51 | — |
| AC-19 union of member time | — | — | TC-009-57, -58 |
| AC-20 not counted locked; kept across restart; midnight | — | — | *TC-009-66* (locked), TC-009-67 (restart); midnight not tested |
| AC-21 per account; kid3/admin never count | — | — | TC-009-60 |
| AC-22 current members, earlier time added | — | — | TC-009-62 |
| AC-23 app usage counted as before | — | — | TC-009-58 |
| AC-24 own rule ignored in a group | — | — | TC-009-57 |
| AC-25 blocked start (exhausted, not allowed) | — | — | TC-009-58, -60 |
| AC-26 warnings once per group | — | — | TC-009-57 |
| AC-27 all members closed, one dialog | — | — | TC-009-57 |
| AC-28 break wins | — | — | *TC-009-65* |
| AC-29 other accounts | — | — | TC-009-60 |
| AC-30 ungrouped app as US-006 | — | — | TC-009-63 (after delete, Paint uses its own rule) |
| AC-31 log entries name the group | — | — | TC-009-57, -58 |
| AC-32 changes during the day, grace | — | — | TC-009-61 (rule change), -62 (move into exhausted group), -63 (delete group) |
| AC-33 early warning on a change; more time | — | — | *TC-009-64*; TC-009-58 step 4 (more time lets the kid start again) |
| AC-34 rename / harmless move no dialog | — | — | *TC-009-64* |
| AC-35 Reports lines | TC-009-23 | TC-009-45 | TC-009-58 |
| AC-36 lines follow members and names, past days | TC-009-24 | TC-009-45 | TC-009-62 |
| AC-37 Reports live ≤ 15 s; group change ≤ 5 s | TC-009-24 (switch-back), -52 | TC-009-58, -52 | — |
| AC-38 storage across restarts, re-install | TC-009-05 (service re-install), TC-009-67 (app restart) | TC-009-46, -67 | TC-009-05 (re-install), TC-009-67 (service restart; reboot in optional step 4) |
| AC-39 new account gets 0:00; untick keeps | — | — | TC-009-68, -69 |
| AC-40 app stays in group when its account is unticked | — | — | TC-009-69 |
| AC-41 performance | — | — | *TC-009-71* (pilot) |
| AC-43 Mac regression spot check | — | TC-009-48 | — |
| D-9 right-click → one item "Verschieben nach …" | TC-009-16 | TC-009-41 | — |
| D-12 Reports refetch without "Wird geladen …" | TC-009-24, -52 | TC-009-52 | — |
| D-13 "Wird geladen …" until both areas loaded | TC-009-22 | TC-009-43 | — |
| D-14 explanation sentence on moving | TC-009-09 | TC-009-39 | — |
| D-15 "no controlled account" state | TC-009-10 | TC-009-38 | — |
| D-16 delete dialog title | TC-009-13 | TC-009-40 | — |
| D-18 outlined headings | TC-009-07 | TC-009-38 | — |
| D-19 pencil / trash-can icon buttons | TC-009-11 | TC-009-40 | — |

US-008 AC-42 (delivery of 0.8.0) is superseded by US-009 AC-30 to AC-32.

## 5. Test Cases

### Session 1 — Service update 0.8.0 → 0.9.0 (service PC; essential) · ≈ 20 min

#### TC-009-01: Service and tray 0.9.0 installed over 0.8.0; groups loaded *(essential)*

- **Verifies**: AC-30 (service), AC-31 (REG-001-01, -02; data loaded), AC-36
- **Machine / account**: Service PC / Admin, Terminal (Administrator) with LOG-WATCH
- **Precondition**: Session 0 done; all kids signed out; Mac app quit

**Steps**

1. Run `03_Delivery\windows\EagleEye-Setup-0.9.0.exe` (elevated, default settings, SmartScreen as S-11). Note the time.
2. *Einstellungen → Apps → Installierte Apps* → "EagleEye".
3. `services.msc` → **EagleEye Service**.
4. LOG-FIND `App groups loaded`.

**Expected result**

- Step 1: the wizard completes without errors, as earlier updates.
- Step 2: **EagleEye 0.9.0** (publisher Michael Adler); no second EagleEye entry.
- Step 3: *Wird ausgeführt*, *Automatisch*, *Lokales System*.
- Step 4: after the install time, a line "App groups loaded: N groups with M apps, K group rules." with N ≥ 1 (Rechnen), M ≥ 1, K ≥ 1 (the same numbers as the last line before the update). No `[ERR]` line since the install.

#### TC-009-02: Tray client 0.9.0: auto-start, App Infos, disconnect and reconnect (REG-001-03 to -05) *(essential)*

- **Verifies**: AC-30 (tray *App Infos*), AC-31 (REG-001-03 to -05), AC-36
- **Machine / account**: Service PC / kid1 (+ Terminal (Administrator) in your session, or K-TERM)
- **Precondition**: TC-009-01 done

**Steps**

1. *Benutzer wechseln* → kid1. Hover over the EagleEye tray icon.
2. Right-click → **App Infos**.
3. K-TERM: `Stop-Service -DisplayName "EagleEye Service"`. Watch the icon for 30 s. **App Infos** again.
4. `Start-Service -DisplayName "EagleEye Service"`. Watch the icon for 60 s without touching anything.
5. Sign kid1 out (close K-TERM first).

**Expected result**

- Step 1: icon present without a manual start, **green**, tooltip **"EagleEye — Verbunden"**.
- Step 2: **"EagleEye – App Infos"** with **"Server-Version: EagleEye_v0.9"**.
- Step 3: ≤ 30 s **red**, tooltip **"EagleEye — Verbindungsfehler"**; App Infos shows **"Verbindungsfehler: Keine Verbindung zum Server unter localhost:5080 möglich."**
- Step 4: ≤ 60 s **green** again.
- Note: kid1 must not use Editor or Paint now (Session 6B).

### Session 2 — Windows app on Windows 11 (service PC / Admin) · ≈ 2 h

Keep **LOG-WATCH** open in the second Terminal (Administrator). Display scaling 150 % (as on your dev machine) unless a step says otherwise.

#### TC-009-03: Fresh installation without admin rights; version and publisher *(essential)*

- **Verifies**: AC-33, AC-30 (W11)
- **Machine / account**: Service PC / Admin (non-elevated Explorer)
- **Precondition**: S-2: no "EagleEye Parent App" installed

**Steps**

1. Double-click `03_Delivery\windows\EagleEye-ParentApp-Setup-0.9.0.exe` (SmartScreen as S-11). Go through the wizard with the defaults.
2. Start → type "EagleEye" → **EagleEye Parent App**.
3. *Einstellungen → Apps → Installierte Apps* → "EagleEye Parent App" → **⋯** / details.

**Expected result**

- Step 1: a guided wizard in German; **no UAC prompt** (no admin rights asked); it completes without errors.
- Step 2: the app starts from the Start menu without any further step.
- Step 3: **EagleEye Parent App**, version **0.9.0**, publisher **Michael Adler**.

#### TC-009-04: First start: Settings with host dialog; pairing; paired start opens on Berichte *(essential; start page re-confirms M-1)*

- **Verifies**: AC-33 (pairing), AC-10 (start page, paired and not paired)
- **Machine / account**: Service PC / Admin
- **Precondition**: TC-009-03; the app is open for the first time

**Steps**

1. Look at the window after the first start.
2. Host dialog: `192.168.1.37` → **Verbinden**. Read the 6-digit code from the popup **"EagleEye – Eltern-App koppeln"** (or *Ereignisanzeige → Anwendung*, source EagleEye). *Gerätename dieses PCs* **Papas PC** → code → **Koppeln**.
3. Close the app (window **X**). Start it again from the Start menu.

**Expected result**

- Step 1: the app opens on **Einstellungen** with the host dialog; status bar red.
- Step 2: no certificate warning or extra step; green **"Verbunden mit 192.168.1.37"**; *Gekoppelt*.
- Step 3: green without a pairing code ≤ 30 s; the app opens on **Berichte**, with **Berichte** selected in the menu.

#### TC-009-05: Data kept by the service update *(essential)*

- **Verifies**: AC-31 (configuration, usage, pairings, groups, members, group rules, past group lines), US-008 AC-38 (re-install of the service)
- **Machine / account**: Service PC / Admin
- **Precondition**: TC-009-04; S-10 screenshots at hand

**Steps**

1. **App Gruppen**: compare with `s10-appgroups-0.8.0.png`.
2. **Regeln** → **Konto** kid1 → *App-Gruppen-Regeln*, *Ruhezeiten*, *Applikations-Regeln*: compare with S-10 (and with what you know of kid1's rules).
3. **Berichte** → kid1: today and the past days with "App-Gruppen".
4. **Einstellungen**: ticked accounts (kid1, kid2 ticked; kid3 not), *Zeit-Ende Warnungen* **3** / **1**.

**Expected result**

- Step 1: the same groups with the same members (Rechnen: Rechner).
- Step 2: Rechnen **0:30**, MO to SO; break times and app rules as before.
- Step 3: today **Rechnen 00:02** (±1); past days with the same group lines as noted in S-10 (or none, if none were noted).
- Step 4: as before the update. (The Mac pairing is checked in TC-009-37.)

#### TC-009-06: Menu entries, order and one highlight *(essential, quick re-confirm of DEV check 2)*

- **Verifies**: AC-10, AC-28 (menu text), US-008 AC-1
- **Machine / account**: Service PC / Admin
- **Precondition**: app connected

**Steps**

1. Look at the menu.
2. Click through **Einstellungen**, **App Gruppen**, **Regeln**, **Berichte**, then back in the reverse order.

**Expected result**

- Step 1: **Einstellungen**, **App Gruppen**, **Regeln**, **Berichte**, in this order.
- Step 2: only the selected entry is highlighted at any time; the entry selected before returns to normal. (Native Windows highlight, plan Q-3.)

#### TC-009-07: Outlined section headings in the Windows accent colour; buttons distinct *(essential; step 4 optional, M-4)*

- **Verifies**: AC-11, US-008 D-18
- **Machine / account**: Service PC / Admin
- **Precondition**: app connected; note your current accent colour

**Steps**

1. Open each page and look at every section heading: *Einstellungen* (Darstellung, Serververbindung, Benutzerkonten auf dem EagleEye-PC, Zeit-Ende Warnungen), *App Gruppen* (each group, In keiner Gruppe), *Regeln* (Ruhezeiten, App-Gruppen-Regeln, Applikations-Regeln), *Berichte* (each day heading, App-Gruppen).
2. Compare the headings with the buttons **Gruppe hinzufügen** and **Eintrag hinzufügen**.
3. Screenshot of *Regeln* → `evidence/tc-07-headings.png`.
4. *(optional, M-4 passed)* With the app open: *Einstellungen → Personalisierung → Farben → Akzentfarbe* → another colour (e.g. green). Click into the app. Set the colour back.

**Expected result**

- Step 1: every heading is a full-width **frame line in the accent colour**, **no solid fill**, heading text **bold in the normal text colour**.
- Step 2: buttons are clearly distinguishable from headings (0.6.2 Windows button style, plan Q-4).
- Step 4: the frames follow the new colour, at the latest when the window is activated again.

#### TC-009-08: Light and dark, 150 % and 100 %, minimum window size *(essential)*

- **Verifies**: AC-12, US-008 AC-5 (look)
- **Machine / account**: Service PC / Admin
- **Precondition**: app connected; scaling 150 %

**Steps**

1. *Einstellungen → Darstellung* → **Hell**. Go through all four pages, open the name dialog (*App Gruppen* → **Gruppe hinzufügen** → **Abbrechen**) and the "Verschieben nach …" list (**≡** → **Abbrechen**).
2. **Dunkel**: the same.
3. Make the window as small as possible (minimum 900 × 600). Scroll each page to the end.
4. *Einstellungen → System → Bildschirm → Skalierung* **100 %**: glance at all four pages. Back to **150 %**.

**Expected result**

- Steps 1, 2, 4: all pages, dialogs and the status bar readable; no text cut off; explanatory texts wrap; checkboxes ticked/unticked clearly visible; trash-can icon visible.
- Step 3: the window does not get smaller than 900 × 600; the pages scroll; every control reachable. On *Regeln* the table may scroll sideways at 150 % (known, R-4: not a Fail).

#### TC-009-09: App Groups page content *(essential)*

- **Verifies**: AC-13, AC-28, US-008 AC-2, AC-3, D-14
- **Machine / account**: Service PC / Admin
- **Precondition**: app connected; the groups of TC-009-05

**Steps**

1. **App Gruppen**: read the page from top to bottom.
2. Hover over a few app entries.
3. **Gruppe hinzufügen** → `School` → **OK** (an empty group).

**Expected result**

- Step 1: the explanation text exactly as in §2 (with "Maus oder dem Trackpad": not an issue, Q-1); **Gruppe hinzufügen**; one section per group, sorted alphabetically ignoring case; **In keiner Gruppe** last; each app **once**, with its display name as on *Berichte* (e.g. Editor, Paint, Rechner), sorted alphabetically in each section; **Windows Explorer not listed**; **no account selection** on the page.
- Step 2: tooltip with the program path.
- Step 3: section **School** in its alphabetical place with **"Keine Apps in dieser Gruppe. Apps hierher ziehen."**

#### TC-009-10: App Groups page states *(essential)*

- **Verifies**: AC-13 (page states), US-008 AC-5, D-15
- **Machine / account**: Service PC / Admin, Terminal (Administrator)
- **Precondition**: app on *App Gruppen*

**Steps**

1. `Stop-Service -DisplayName "EagleEye Service"`. Look at the page and the status bar.
2. `Start-Service -DisplayName "EagleEye Service"`. Watch the page while it reconnects.
3. *Einstellungen*: untick **kid1** and **kid2** (no account under control). **App Gruppen**.
4. Tick **kid1** and **kid2** again. **App Gruppen**.

**Expected result**

- Step 1: red **"Nicht verbunden mit 192.168.1.37"** ≤ 30 s; the page shows **Keine Daten verfügbar** (no stale entries that look editable).
- Step 2: green ≤ 60 s; the page shows **Wird geladen …** briefly, then the groups again.
- Step 3: **"Keine Konten unter Elternkontrolle. Konten unter Einstellungen auswählen."**
- Step 4: the page with groups and apps as before; kid1's and kid2's rules unchanged (glance at *Regeln*).

#### TC-009-11: Pencil and trash-can buttons on the group headings *(essential, quick re-confirm of M-3)*

- **Verifies**: AC-14, US-008 D-19
- **Machine / account**: Service PC / Admin
- **Precondition**: groups Rechnen and School exist

**Steps**

1. Look at the heading of **School** and of **In keiner Gruppe**, in **Hell** and **Dunkel**.
2. Hover over the pencil, then the trash can.
3. Click the pencil → **Abbrechen**. Click the trash can → **Abbrechen**.

**Expected result**

- Step 1: each group heading shows the name, a **pencil** and a **trash-can** icon button, visible in both modes (thin native frame around them: known, R-3). **In keiner Gruppe** has neither.
- Step 2: tooltips **Umbenennen** and **Löschen**.
- Step 3: the pencil opens **App-Gruppe umbenennen**, the trash can the delete confirmation; Abbrechen changes nothing.

#### TC-009-12: New group: dialog, name rules, emoji, Cancel and Escape *(essential)*

- **Verifies**: AC-15 (create, name rules), AC-28, US-008 AC-7, AC-8
- **Machine / account**: Service PC / Admin
- **Precondition**: groups Rechnen and School exist

**Steps**

1. **Gruppe hinzufügen** → read the dialog → **Abbrechen**. Again → **Escape**.
2. **Gruppe hinzufügen** → **OK** with an empty field.
3. Type 3 spaces → **OK**.
4. Type 41 characters (`12345678901234567890123456789012345678901`) → **OK**.
5. Type `school` → **OK**.
6. Type `  Games  ` (spaces before and after) → **OK**.
7. **Gruppe hinzufügen** → type `Musik ` then **Windows+.** → pick 🎮 → **OK**.

**Expected result**

- Step 1: dialog **Neue App-Gruppe** with field **Name**, buttons **OK** and **Abbrechen**; Abbrechen and Escape create nothing.
- Steps 2 to 4: the dialog comes back with **"Bitte einen Namen mit 1 bis 40 Zeichen eingeben."** and the typed text (step 4: the 41 characters still there).
- Step 5: the dialog comes back with **"Es gibt bereits eine Gruppe mit diesem Namen."**
- Step 6: section **Games** (trimmed) in its alphabetical place, empty.
- Step 7: section **Musik 🎮** with the emoji shown as an emoji, in its alphabetical place.
- *Regeln* (glance, kid1 and kid2): **Games** and **Musik 🎮** with **0:00**, no day ticked.

#### TC-009-13: Rename and delete *(essential)*

- **Verifies**: AC-15 (rename, delete), AC-28, US-008 AC-9, AC-10, D-16
- **Machine / account**: Service PC / Admin
- **Precondition**: TC-009-12; move **Paint** into **Games** (any way) and set kid1's own rule for Paint in *Applikations-Regeln* to something you will recognise **before** moving it (e.g. 0:17, MO)

**Steps**

1. Pencil on **Games** → look at the field → **OK** without change.
2. Pencil on **Games** → `Gaming` → **OK**.
3. Pencil on **Gaming** → `Games` → **Enter**.
4. Trash can on **Games** → read the dialog → **Abbrechen**.
5. Trash can on **Games** → **Löschen**.
6. *Regeln* → kid1 → *Applikations-Regeln*: Paint.
7. Delete **Musik 🎮** and **School** (trash can → **Löschen**).

**Expected result**

- Step 1: dialog **App-Gruppe umbenennen**, filled with **Games**; OK with the unchanged name is accepted, nothing changes.
- Steps 2, 3: the section is renamed and re-sorted; Paint stays in it.
- Step 4: title **App-Gruppe löschen**, text **"Gruppe „Games“ löschen? Ihre Apps kommen nach „In keiner Gruppe“ und verwenden wieder ihre eigenen Applikations-Regeln."**, buttons **Löschen** and **Abbrechen**; Abbrechen keeps the group.
- Step 5: **Games** is gone; Paint is under **In keiner Gruppe**.
- Step 6: Paint is listed again with **its own earlier rule** (0:17, MO).
- Step 7: only **Rechnen** is left. Set Paint's own rule back to what it was.

#### TC-009-14: Drag-and-drop with the mouse; highlight; small window *(essential)*

- **Verifies**: AC-16 (mouse), AC-12 (drop highlight cleared, both modes), US-008 AC-11, AC-13
- **Machine / account**: Service PC / Admin, mouse
- **Precondition**: create **Games** and **School** again (empty); Rechnen holds Rechner

**Steps**

1. Drag **Paint** (hold its **name**) from *In keiner Gruppe* onto **Games**. While dragging, move slowly over **School**, **Rechnen** and *In keiner Gruppe* before dropping.
2. Drag **Paint** from **Games** onto the empty **School**.
3. Drag **Paint** onto **School** again (own section) and release.
4. Drag **Paint** and release outside any section (on the explanation text).
5. Drag **Paint** back onto **In keiner Gruppe**.
6. Switch *Darstellung* to the other mode; repeat step 1 and step 5 once.
7. Make the window small (900 × 600) so the page scrolls; drag **Editor** onto **Games** and back.

**Expected result**

- Steps 1, 2, 5, 7: the app moves to the target section; it is in exactly one section (gone from the old one).
- While dragging: the section under the pointer is highlighted, the highlight moves with the pointer, only one section at a time. No "Kopieren" caption next to the pointer (removed on purpose, §8.3: not a Fail if missing).
- Steps 3, 4: nothing changes.
- After every drop or release: no highlight remains (both modes).
- Note: dragging works on the app's **name**; "≡" is not a drag handle (story UI note: no issue).

#### TC-009-15 (optional): Drag-and-drop with the touchpad *(optional, M-2 passed)*

- **Verifies**: AC-16 (touchpad)
- **Machine / account**: Service PC / Admin, touchpad (skip if the PC has none)
- **Precondition**: TC-009-14

**Steps**

1. With the touchpad: drag **Paint** onto **Games**, then back onto **In keiner Gruppe**.

**Expected result**

- As TC-009-14 steps 1 and 5, including the highlight that is cleared afterwards.

#### TC-009-16: "≡" and right-click → "Verschieben nach …" *(essential)*

- **Verifies**: AC-17, AC-28, US-008 AC-12, D-9
- **Machine / account**: Service PC / Admin
- **Precondition**: groups Games, Rechnen, School; Paint under *In keiner Gruppe*

**Steps**

1. **≡** on **Paint** → read the list → choose **Games**.
2. **≡** on **Paint** (now in Games) → read the list → **Abbrechen**.
3. Right-click on **Paint** → **Verschieben nach …** → read the list → choose **In keiner Gruppe**.
4. Right-click on **Editor** → **Verschieben nach …** → press **Escape**.

**Expected result**

- Step 1: the list shows **Games**, **Rechnen**, **School** (no "In keiner Gruppe", because Paint is in no group) and **Abbrechen**; Paint moves to Games.
- Step 2: the list shows **Rechnen**, **School**, **In keiner Gruppe** (not Games) and **Abbrechen**; Abbrechen moves nothing.
- Step 3: the right-click shows one item **Verschieben nach …**, which opens the same list (D-9); Paint moves to *In keiner Gruppe*.
- Step 4: nothing moves.

#### TC-009-17: Every change saved at once; log entries with the device name *(essential)*

- **Verifies**: AC-18 (saved, ≤ 5 s, log), US-008 AC-14
- **Machine / account**: Service PC / Admin, Terminal (Administrator)
- **Precondition**: TC-009-12 to TC-009-16 done (they made creates, renames, deletes and moves)

**Steps**

1. During TC-009-12 to -16 you saw each change in the page; was any change slower than about 5 s, or did any section jump back? (answer in Notes)
2. Close the app and start it again → **App Gruppen**.
3. LOG-FIND `app group|app moved`.

**Expected result**

- Step 1: every change showed ≤ 5 s, no "Save" button exists.
- Step 2: the page shows the last state (Games, Rechnen, School; Paint under *In keiner Gruppe*).
- Step 3: Information lines such as "app group created: Games", "app group renamed: Games → Gaming", "app group deleted: …", "app moved: Paint → Games", "app moved: Paint → not in any group", naming **Papas PC**.

#### TC-009-18 (optional): The service cannot store a change *(optional)*

- **Verifies**: AC-18 (error path), US-008 AC-14
- **Machine / account**: Service PC / Admin, Terminal (Administrator)
- **Precondition**: all kids signed out; app on *App Gruppen*

**Steps**

1. Terminal (Administrator): `resmon` → **CPU** → right-click `EagleEye.Service.exe` → **Prozess anhalten**.
2. Within 10 s: **≡** on **Paint** → **School**. Wait up to 15 s.
3. At the latest 25 s after step 1: `resmon` → **Prozess fortsetzen**. Wait 30 s.

**Expected result**

- Step 2: **"Die Änderung konnte nicht gespeichert werden. Bitte erneut versuchen."**; Paint returns to its stored section.
- Step 3: green; the page shows the stored state (Paint where the last "app moved" log line says; write which in Notes).

#### TC-009-19: Rules: the "App-Gruppen-Regeln" section *(essential)*

- **Verifies**: AC-19, AC-28, US-008 AC-15
- **Machine / account**: Service PC / Admin
- **Precondition**: groups Games (empty or Paint), Rechnen (Rechner), School (empty)

**Steps**

1. **Regeln** → **Konto** kid1. Read the page from top to bottom.
2. **Konto** kid2: *App-Gruppen-Regeln*.

**Expected result**

- Step 1: **App-Gruppen-Regeln** between **Ruhezeiten** and **Applikations-Regeln**, outlined like the other headings; the explanation as in §2; table **App-Gruppe** · **Stunden** · **Minuten** · **MO** to **SO**; rows sorted by name (Games, Rechnen, School); below each name in smaller text its apps, separated by commas ("Rechner"), or **(keine Apps)**; Rechnen 0:30, MO to SO; Games and School **0:00**, no day.
- Step 2: kid2: every group **0:00**, no day (only kid1's Rechnen rule was set in S-10).
- The "Noch keine App-Gruppen …" text needs a state without groups: optional TC-009-72.

#### TC-009-20: Editing a group rule: validation, ticks, Tab/Enter, flush on page and account change *(essential)*

- **Verifies**: AC-20, US-008 AC-16, US-007 AC-32
- **Machine / account**: Service PC / Admin, LOG-WATCH
- **Precondition**: **Regeln** → kid1

**Steps**

1. **Games**: **Stunden** `24` → **Tab**. Then `abc` (if letters can be typed).
2. **Games**: **Minuten** `60` → **Tab**.
3. **Games**: **Stunden** `1`, **Minuten** `0` → **Enter**. Tick **SA** and **SO**. Watch the log.
4. **Games**: **Minuten** `15` and **immediately** click **Berichte** (no Tab). Back to **Regeln** → kid1.
5. **Games**: **Minuten** `20` and **immediately** switch **Konto** to kid2. Back to kid1.
6. Set **Games** back to **0:00**, no day.

**Expected result**

- Step 1: **"Bitte Stunden zwischen 0 und 23 eingeben."**; the stored value stays (letters not accepted or message: both Pass).
- Step 2: **"Bitte Minuten zwischen 0 und 59 eingeben."**
- Step 3: stored ≤ 5 s; log line with "group rule changed: kid1, Games, 1:00, Sa Su" (account name as on your PC; wording differences are Notes), naming **Papas PC**.
- Steps 4, 5: the typed value is saved (1:15, then 1:20), not lost.
- Step 6: stored.

#### TC-009-21: "Applikations-Regeln" lists only ungrouped apps; own rule comes back *(essential)*

- **Verifies**: AC-21, AC-28, US-008 AC-17
- **Machine / account**: Service PC / Admin
- **Precondition**: Paint under *In keiner Gruppe*; note kid1's own rule for Paint

**Steps**

1. **Regeln** → kid1 → *Applikations-Regeln*: read the explanation; is **Rechner** listed? Is **Paint** listed?
2. **App Gruppen**: move **Paint** into **School**. **Regeln** → kid1.
3. **App Gruppen**: move **Paint** back to **In keiner Gruppe**. **Regeln** → kid1.

**Expected result**

- Step 1: the explanation ends with **"Apps in einer App-Gruppe werden unter App-Gruppen-Regeln eingestellt."**; **Rechner is not listed** (it is in Rechnen); Paint is listed; the budget text label below the table is unchanged.
- Step 2: Paint is gone from *Applikations-Regeln*; under *App-Gruppen-Regeln* School shows "Paint" below its name.
- Step 3: Paint is back with **its own rule as before**.

#### TC-009-22: "Wird geladen …" until both areas are loaded; no flash of grouped apps *(essential)*

- **Verifies**: AC-21 (loading), US-008 D-13
- **Machine / account**: Service PC / Admin, Terminal (Administrator)
- **Precondition**: **Regeln** → kid1 open; Rechner in Rechnen

**Steps**

1. `Restart-Service -DisplayName "EagleEye Service"`. Watch *App-Gruppen-Regeln* and *Applikations-Regeln* until both show data.
2. Close the app. Start it, click **Regeln** at once and watch the same.

**Expected result**

- Steps 1, 2: both sections show **Wird geladen …** until their data is there; **Rechner never appears** under *Applikations-Regeln*, not even briefly.

#### TC-009-23: Reports: "App-Gruppen" lines per day *(essential)*

- **Verifies**: AC-22, AC-28, US-008 AC-35
- **Machine / account**: Service PC / Admin
- **Precondition**: kid1 has Rechnen 00:02 today (S-10)

**Steps**

1. **Berichte** → kid1: today's block, then the older days.

**Expected result**

- Today: the app table unchanged (**App** · **Nutzung (HH:MM)**), below it **App-Gruppen** with **App-Gruppe** · **Nutzung (HH:MM)** and **Rechnen 00:02** (±1). Groups without time today (Games, School) are not listed.
- A day without group time shows **no** "App-Gruppen" part.
- The heading "App-Gruppen" is outlined like the day headings.
- Sorting by usage and "same as in the Mac app" are checked in TC-009-58 (two groups with time) and TC-009-52.

#### TC-009-24: Reports follow a rename and today's usage, without "Wird geladen …" *(essential; re-confirms M-5)*

- **Verifies**: AC-23 (Windows part), AC-34 (Reports today's usage), US-008 AC-36, AC-37, D-12
- **Machine / account**: Service PC / Admin, kid1
- **Precondition**: TC-009-23; **Berichte** → kid1 open

**Steps**

1. **App Gruppen** → rename **Rechnen** → `Rechnen 2`. **Berichte** → kid1: today and past days.
2. Rename back to **Rechnen**. Leave **Berichte** → kid1 **open**; note today's Rechner and Rechnen values.
3. *Benutzer wechseln* → kid1 → Windows+R `calc` → **1 minute** → close it. *Benutzer wechseln* → your account. **Do not touch the app**; look at Berichte for 15 s.

**Expected result**

- Step 1: all days show **Rechnen 2** instead of Rechnen; the page did not show **Wird geladen …**.
- Step 3: without any action, today shows Rechner and **Rechnen** about **1 minute more** (≤ 15 s after you are back). Kid1's Rechner usage: write it down (≈ 00:03), Session 6B uses it.
- Note: the timed "≤ 15 s while the kid uses the app" is checked on the Mac (TC-009-58), because only the kid's session in front counts.

#### TC-009-25 (optional): English on Windows 11 *(optional; English is fully checked on Windows 10, TC-009-32)*

- **Verifies**: AC-28 (W11, English)
- **Machine / account**: Service PC / Admin
- **Precondition**: English display language installed for your account; you are willing to sign out

**Steps**

1. *Einstellungen → Zeit und Sprache → Sprache und Region → Windows-Anzeigesprache* → **English (United States)** → sign out and in. Start the app; look at all four pages, the name dialog, the delete dialog and "Move to …".
2. Back to **Deutsch**, sign out and in.

**Expected result**

- Step 1: every text in English with the wording of TC-009-47 / §2 (Mac reference): "App Groups", "Add group", "Not in any group", "Move to …", "App group rules", "(no apps)", "App groups" / "App group" / "Usage (HH:MM)"; no German leftovers, no internal keys.
- Step 2: German again.

### Session 3 — Windows 10 PC · ≈ 1 h 20

You work on the W10 PC; the service PC is needed for the pairing code (TC-009-28) and the service stop (TC-009-29). On the service PC keep the W11 app **closed** or on *Einstellungen* (no edits).

#### TC-009-26: Windows 10 version; the service installer refuses Windows 10 *(essential, quick re-confirm of pre-check rows 1, 2)*

- **Verifies**: AC-5
- **Machine / account**: W10 PC / your account
- **Precondition**: S-4 done

**Steps**

1. Windows+R `winver`.
2. Double-click `EagleEye-Setup-0.9.0.exe`.
3. *Einstellungen → Apps → Apps & Features*: search "EagleEye".

**Expected result**

- Step 1: **Windows 10, Version 22H2**, build **19045.x**.
- Step 2: a message that a newer Windows (Windows 11) is needed (Inno Setup's built-in text, German or English); the setup ends; nothing installed.
- Step 3: no EagleEye entry.

#### TC-009-27: Windows app installs without admin rights and without a separate .NET runtime *(essential)*

- **Verifies**: AC-2, AC-3 (Apps & Features entry), AC-30 (W10)
- **Machine / account**: W10 PC / your account
- **Precondition**: TC-009-26

**Steps**

1. *Apps & Features*: search "NET" and "Windows App Runtime"; note what is listed (Notes).
2. Double-click `EagleEye-ParentApp-Setup-0.9.0.exe` (SmartScreen: *Weitere Informationen* → *Trotzdem ausführen*). Defaults. Note the install folder the wizard shows (if it shows one).
3. Start → **EagleEye Parent App**.
4. *Apps & Features* → **EagleEye Parent App**.

**Expected result**

- Step 1: whatever is there; nothing is installed for EagleEye.
- Step 2: guided wizard in German, **no UAC prompt**; it completes. It does **not** ask for or install a .NET runtime (step 1's list unchanged afterwards).
- Step 3: the app starts on **Einstellungen** with the host dialog, without any further step.
- Step 4: **EagleEye Parent App**, **0.9.0**, **Michael Adler**.

#### TC-009-28: Connection: unreachable host, then pairing with a wrong and the right code *(essential)*

- **Verifies**: AC-6 (host dialog, error naming the host, IPv4, TLS without warning, pairing, wrong code, status bar), AC-10 (not paired → Settings)
- **Machine / account**: W10 PC; service PC / Admin (code popup)
- **Precondition**: TC-009-27, host dialog open

**Steps**

1. Host `192.168.1.250` (an address with no PC; or any unused one) → **Verbinden**. Wait up to 30 s.
2. Host `192.168.1.37` → **Verbinden**. Read the status bar and the pairing form.
3. *Gerätename dieses PCs* **Windows 10 PC**; enter the code from the service PC's popup with the **last digit changed** → **Koppeln**.
4. Enter the right code → **Koppeln**.

**Expected result**

- Step 1: a connection error that **names the host**, e.g. "Verbindungsfehler: Keine Verbindung zum EagleEye-Dienst unter 192.168.1.250 möglich."; red.
- Step 2: no certificate warning, no extra step; pairing form with **Kopplungscode** and **Gerätename dieses PCs**; red **"Verbindung zu 192.168.1.37 wird hergestellt …"** (never "Verbunden" before pairing).
- Step 3: **"Der Kopplungscode ist falsch."** (or same meaning); not paired; red.
- Step 4: green **"Verbunden mit 192.168.1.37"**, *Gekoppelt*.

#### TC-009-29: Reconnect timings from Windows 10 *(essential)*

- **Verifies**: AC-6 (US-002 AC-21, AC-22)
- **Machine / account**: W10 PC; service PC / Terminal (Administrator)
- **Precondition**: TC-009-28; stopwatch

**Steps**

1. Close the app; start it again (stopwatch).
2. Service PC: `Stop-Service -DisplayName "EagleEye Service"` (stopwatch).
3. `Start-Service -DisplayName "EagleEye Service"` (stopwatch).

**Expected result**

- Step 1: green ≤ **30 s**, no pairing code; opens on **Berichte**.
- Step 2: red **"Nicht verbunden mit 192.168.1.37"** ≤ **30 s**.
- Step 3: green ≤ **60 s** without any action.

#### TC-009-30: All four pages on Windows 10 (spot check incl. the three ways to move) *(essential)*

- **Verifies**: AC-7, AC-16 (W10: mouse, touchpad), AC-18 (log with the W10 device name)
- **Machine / account**: W10 PC; service PC / Terminal (Administrator) for the log
- **Precondition**: TC-009-29; the group **Games** exists (from Session 2) — if not, step 3 creates it

**Steps**

1. **Einstellungen**: untick **kid2**, tick it again.
2. **Einstellungen → Zeit-Ende Warnungen**: **Frühe Warnung** `4` → **Tab**; then back to `3` → **Tab**.
3. **App Gruppen**: (if needed **Gruppe hinzufügen** `Games`); move **Paint** into **Games** by **dragging with the mouse**, back; with the **touchpad** (if the PC has one), back; with **≡**, back; with **right-click → Verschieben nach …**; leave it in Games.
4. **Regeln** → kid1: **Games** **Stunden** `0`, **Minuten** `30`, tick today → **Tab**. *Ruhezeiten* → **Eintrag hinzufügen** (leave **An/Aus** off).
5. **Berichte** → kid1: today.
6. Undo: delete the break entry (trash can); **Games** back to 0:00, no day; move **Paint** back to *In keiner Gruppe*.
7. Service PC: LOG-FIND `Windows 10 PC`.

**Expected result**

- Steps 1 to 4, 6: every change shows ≤ 5 s and stays (no jump back); drag highlight as in TC-009-14 and cleared.
- Step 5: today's app lines and **App-Gruppen** (Rechnen) for kid1, as on the W11 app.
- Step 7: the account, warning, group, group rule and break changes are logged with **Windows 10 PC**.
- Touchpad missing → write "no touchpad" in Notes (not a Fail).

#### TC-009-31: Look on Windows 10: menu, headings, light/dark, 100 %/150 %, minimum size, emojis *(essential)*

- **Verifies**: AC-8, AC-10 (W10 menu), AC-11 (W10 headings)
- **Machine / account**: W10 PC
- **Precondition**: connected

**Steps**

1. Menu: four entries in order; click through all four twice.
2. Look at every section heading (as TC-009-07 step 1); change the accent colour (*Einstellungen → Personalisierung → Farben*), click into the app, set it back.
3. *Darstellung* **Hell**, then **Dunkel**: all four pages.
4. Scaling **150 %** (*Einstellungen → System → Bildschirm*), then **100 %**: all four pages; window at its minimum size, scroll to the end of each page.
5. **App Gruppen** → **Gruppe hinzufügen** → `Spiele 😀` (Windows+. for the emoji) → **OK**. **Regeln** → kid1 → *Anzeige Text wenn Budget aufgebraucht:* add ` 🎮` at the end → **Tab**. Look at both. Then delete the group and remove the emoji again.
6. Screenshot of *App Gruppen* in **Dunkel** at 150 % → `evidence/tc-31-w10-appgroups.png`.

**Expected result**

- Step 1: **Einstellungen**, **App Gruppen**, **Regeln**, **Berichte**; one highlight only.
- Step 2: outlined headings in the accent colour, text in the normal colour; frames follow the new colour at the latest after re-activating the window.
- Steps 3, 4: no text cut off, no control unreachable, checkboxes and the trash-can icon visible; window ≥ 900 × 600 with scrolling pages. Fonts may look slightly different from Windows 11 (no Segoe UI Variable; known).
- Step 5: 😀 and 🎮 shown as emojis (a box or a split symbol = Fail for these two common emojis).

#### TC-009-32: Language on Windows 10: English, then German *(essential)*

- **Verifies**: AC-9 (German, English), AC-28 (W10: all new texts in both languages, same wording as the Mac)
- **Machine / account**: W10 PC
- **Precondition**: English language pack on the W10 PC (*Einstellungen → Zeit und Sprache → Sprache → Sprache hinzufügen*); kid1's break display text, budget text and warning texts are the **defaults** (check in *Regeln* / *Einstellungen*; if one was changed, write which in Notes)

**Steps**

1. *Windows-Anzeigesprache* → **English (United States)** → sign out and in.
2. Start the app; go through: menu; *App Groups* (explanation, Add group, Not in any group, empty-section text, "≡" list "Move to …", tooltips of pencil and trash can); name dialog with both messages (empty, `rechnen`); delete dialog (open on a new group, Cancel; delete that group); the save error text is not producible here (skip); *Rules* (App group rules: heading, explanation, columns, "(no apps)", the sentence under Application rules); *Reports* (App groups / App group / Usage (HH:MM)); *Settings* default warning texts and *Rules* default break and budget texts.
3. *Windows-Anzeigesprache* → **Deutsch** → sign out and in; start the app; glance at the same places.

**Expected result**

- Step 2: every text English, with the wording of §2 (= Mac app 0.8.0; Mac reference TC-009-47); no German leftovers, no third language, no internal keys. The **unchanged default** kid texts are shown in English (e.g. "You have used up your time! Sorry, that's it for now.").
- Step 3: all German again, defaults in German.

#### TC-009-33: Repair keeps pairing and appearance *(essential)*

- **Verifies**: AC-3 (repair)
- **Machine / account**: W10 PC
- **Precondition**: connected; *Darstellung* set to **Dunkel** (note it)

**Steps**

1. Close the app. Run `EagleEye-ParentApp-Setup-0.9.0.exe` again with defaults.
2. Start the app.

**Expected result**

- Step 1: completes without errors.
- Step 2: green ≤ 30 s **without a pairing code**; device still **Windows 10 PC**; **Dunkel** kept.

#### TC-009-34: Uninstall removes everything *(essential)*

- **Verifies**: AC-3 (uninstall)
- **Machine / account**: W10 PC
- **Precondition**: TC-009-33; the app is still **paired** (do **not** use *Kopplung aufheben* first; see §8 Q-7)

**Steps**

1. Close the app. *Apps & Features* → **EagleEye Parent App** → **Deinstallieren**.
2. Start menu: search "EagleEye".
3. Explorer: `%LocalAppData%\EagleEye`, and the install folder of TC-009-27 step 2 (if noted; otherwise `%LocalAppData%\Programs` → anything "EagleEye").

**Expected result**

- Step 1: completes without errors; no entry in *Apps & Features*.
- Step 2: no entry.
- Step 3: neither folder exists (no program files, no connection or pairing data).

#### TC-009-35 (optional): Older Windows 10 or 32-bit Windows is refused *(optional; only if such a Windows exists)*

- **Verifies**: AC-4
- **Machine / account**: a PC or VM with Windows 10 older than 22H2 (build < 19045) or 32-bit Windows
- **Precondition**: such a Windows is available; otherwise tick **Skipped** and write "not available"

**Steps**

1. Run `EagleEye-ParentApp-Setup-0.9.0.exe`.

**Expected result**

- A message that this Windows version is not supported (Inno Setup's built-in text); nothing installed.

#### TC-009-36 (optional): Any other display language → German *(optional)*

- **Verifies**: AC-9 (other language)
- **Machine / account**: W10 PC
- **Precondition**: a third language pack (e.g. Français) installed; do it **before** TC-009-34 if you run it

**Steps**

1. Display language **Français** → sign out and in → start the app. Then back to Deutsch.

**Expected result**

- The app is **German**.

### Session 4 — Mac app update 0.8.0 → 0.9.0 and the US-008 Mac ACs (MacBook) · ≈ 1 h 15

The Mac cases are **quick re-confirms** of your 12-point check on 0.8.0 (US-008 report §8.6), now on 0.9.0, because AC-35 asks for them in full. Use the W11 app only to read the code popup and the log. German macOS.

#### TC-009-37: Update over 0.8.0; Local Network off/on; pairing and appearance kept; version *(essential)*

- **Verifies**: AC-32, AC-30 (Mac)
- **Machine / account**: MacBook; service PC (log)
- **Precondition**: Mac app 0.8.0 quit since S-10; note its appearance setting (Hell/Dunkel)

**Steps**

1. Open `03_Delivery/macos/EagleEye-ParentApp-0.9.0.dmg` → drag **EagleEye Parent App** onto **Programme** → **Ersetzen**.
2. Start the app. If macOS blocks it: *Systemeinstellungen → Datenschutz & Sicherheit → Dennoch öffnen*.
3. If the app does not become green within 30 s: quit it (**Cmd+Q**), *Systemeinstellungen → Datenschutz & Sicherheit → Lokales Netzwerk* → switch **every EagleEye entry off and on again**, start the app. Answer a Keychain prompt with **Immer erlauben**. Write in Notes which of these steps were needed and how many Keychain prompts appeared.
4. Terminal: `nc -z -G 3 192.168.1.37 5443` (not ping).
5. Finder → *Programme* → EagleEye Parent App → **Informationen**. App menu **EagleEye Parent App → Über EagleEye Parent App**.

**Expected result**

- Step 3 (or already step 2): green **"Verbunden mit 192.168.1.37"** without a new pairing code; the same device name as before; appearance as before. At most the Keychain prompts accepted in US-007 OQ-3 (one-time "Immer erlauben"); having to pair again = **Fail**.
- Step 4: "succeeded" / "Connection to 192.168.1.37 port 5443 … succeeded!".
- Step 5: both show **0.9.0**.

#### TC-009-38: Menu, start page, outlined headings, page states and look *(essential, re-confirm)*

- **Verifies**: AC-35 (US-008 AC-1, AC-5, D-15, D-18)
- **Machine / account**: MacBook
- **Precondition**: TC-009-37

**Steps**

1. Menu order; Cmd+Q and start: which page opens?
2. Every page: headings outlined in the macOS accent colour, text normal colour.
3. *Darstellung* **Hell** and **Dunkel**; *Systemeinstellungen → Bedienungshilfen → Anzeige* → larger text (as in US-008 §8.6 point 10): *App Gruppen* readable.
4. W11 app: *Einstellungen* untick kid1 and kid2 → Mac *App Gruppen*; tick both again.

**Expected result**

- Step 1: **Einstellungen**, **App Gruppen**, **Regeln**, **Berichte**; opens on **Berichte**.
- Step 2: outlined (D-18).
- Step 3: readable, nothing cut off.
- Step 4: **"Keine Konten unter Elternkontrolle. Konten unter Einstellungen auswählen."** (≤ 5 s), then the page again.

#### TC-009-39: App Gruppen content *(essential, re-confirm)*

- **Verifies**: AC-35 (US-008 AC-2, AC-3, D-14), AC-28 (Mac = Windows wording)
- **Machine / account**: MacBook (and the W11 app side by side, for the wording)
- **Precondition**: groups Games, Rechnen, School exist

**Steps**

1. Read the page top to bottom; hover over an app.
2. Compare the explanation and the section texts word by word with the W11 app's page.

**Expected result**

- Step 1: as TC-009-09 (explanation, button, sorted sections, In keiner Gruppe last, each app once, tooltip with the program path, no Windows Explorer, no account selection, empty text for School).
- Step 2: identical wording (a difference = Fail, AC-28).

#### TC-009-40: Create, rename, delete with the icon buttons *(essential, re-confirm)*

- **Verifies**: AC-35 (US-008 AC-7 to AC-10, D-16, D-19)
- **Machine / account**: MacBook
- **Precondition**: TC-009-39

**Steps**

1. **Gruppe hinzufügen**: empty → **OK**; `games` → **OK**; 41 characters → **OK**; **Escape**. Then `Musik 🎮` (Character Viewer: Ctrl+Cmd+Space) → **OK**.
2. Pencil on **Musik 🎮** (tooltip?) → `Musik` → **OK**. Trash can (tooltip?) on **Musik** → read → **Abbrechen**; again → **Löschen**.

**Expected result**

- Step 1: the two messages of TC-009-12 with the typed text kept; Escape creates nothing; **Musik 🎮** created and sorted.
- Step 2: tooltips **Umbenennen** / **Löschen**; pencil and trash-can icons visible in the current mode; rename works; delete dialog with title **App-Gruppe löschen** and the question of §2; Abbrechen keeps, Löschen removes.

#### TC-009-41: Moving: drag with mouse and trackpad, "≡", right-click *(essential, re-confirm)*

- **Verifies**: AC-35 (US-008 AC-11 to AC-13, D-9)
- **Machine / account**: MacBook, trackpad (+ mouse if you have one)
- **Precondition**: Paint under *In keiner Gruppe*

**Steps**

1. Trackpad: drag **Paint** onto **School** (empty), then onto **Games**, then onto **In keiner Gruppe**; once onto its own section; once outside any section. Mouse: the same once (skip if no mouse).
2. Make the window short so the page scrolls; drag again.
3. **≡** on **Paint** → read the list → **Games**. Two-finger click on **Paint** → **Verschieben nach …** → **In keiner Gruppe**. **≡** → **Abbrechen**.

**Expected result**

- As TC-009-14 and TC-009-16: moves as expected, own section and outside change nothing, highlight follows and is always cleared, the list excludes the current section and includes *In keiner Gruppe* only for a grouped app.

#### TC-009-42: Mac changes saved at once; log names the Mac *(essential, re-confirm)*

- **Verifies**: AC-35 (US-008 AC-14 save and log)
- **Machine / account**: MacBook; service PC / Terminal (Administrator)
- **Precondition**: TC-009-40, -41 done

**Steps**

1. Cmd+Q, start the app → **App Gruppen**.
2. Service PC: LOG-FIND `app group|app moved` → lines from the Mac's device name.

**Expected result**

- Step 1: the last state (Games, Rechnen, School; Paint under *In keiner Gruppe*); every change of TC-009-40/-41 had shown ≤ 5 s.
- Step 2: create, rename, delete and move lines naming the Mac's device name.

#### TC-009-43: Rules: App-Gruppen-Regeln, hidden grouped apps, loading *(essential, re-confirm)*

- **Verifies**: AC-35 (US-008 AC-15 to AC-17, D-11, D-13)
- **Machine / account**: MacBook
- **Precondition**: Rechner in Rechnen

**Steps**

1. **Regeln** → kid1: section order, columns, member lines, (keine Apps), Rechnen 0:30 MO–SO.
2. **Games**: **Stunden** `24` → Tab (message); `1:00`, **SA SO** → Return. Then back to 0:00, no day.
3. *Applikations-Regeln*: the sentence; Rechner not listed. Move **Paint** into **School** (App Gruppen) → Paint gone from *Applikations-Regeln*; back → returns with its own rule.
4. Cmd+Q, start, **Regeln** at once: watch both sections.

**Expected result**

- As TC-009-19 to TC-009-22 on the Mac: same layout and texts as the W11 app; validation message; saved ≤ 5 s; own rule comes back; **Wird geladen …** in both sections until loaded, Rechner never briefly under *Applikations-Regeln*.

#### TC-009-44 (optional): Mac save error *(optional)*

- **Verifies**: AC-35 (US-008 AC-14 error path)
- **Machine / account**: MacBook; service PC / Terminal (Administrator)
- **Precondition**: as TC-009-18

**Steps**

1. Service PC: `resmon` → **Prozess anhalten** on `EagleEye.Service.exe`. Within 10 s on the Mac: **≡** on Paint → **School**.
2. Within 25 s: **Prozess fortsetzen**.

**Expected result**

- **"Die Änderung konnte nicht gespeichert werden. Bitte erneut versuchen."**; the page returns to the stored state.

#### TC-009-45: Reports group lines on the Mac; past days follow the current name *(essential, re-confirm)*

- **Verifies**: AC-35 (US-008 AC-35, AC-36)
- **Machine / account**: MacBook
- **Precondition**: kid1 has Rechnen time today (S-10, TC-009-24)

**Steps**

1. **Berichte** → kid1: today and past days.
2. W11 app or Mac: rename **Rechnen** → `Rechnen 2`; look at all days on the Mac; rename back.

**Expected result**

- Step 1: **App-Gruppen** / **App-Gruppe** · **Nutzung (HH:MM)** under today with **Rechnen ≈ 00:03**, the **same value as in the W11 app** (AC-22 "same as in the Mac app"); no part on days without group time.
- Step 2: all days follow the name ≤ 5 s, without **Wird geladen …**.

#### TC-009-46: Mac start page and connection survive a restart *(essential, re-confirm)*

- **Verifies**: AC-35 (US-008 AC-38: restart of the Mac app), AC-32 (pairing kept after the first restart too)
- **Machine / account**: MacBook
- **Precondition**: TC-009-45

**Steps**

1. Cmd+Q; start the app.

**Expected result**

- Green ≤ 30 s without a code or a Keychain prompt; opens on **Berichte**; groups, rules unchanged.

#### TC-009-47 (optional): Mac in English (wording reference) *(optional; you checked it on 0.8.0)*

- **Verifies**: AC-35 (US-008 texts), AC-28 (Mac English as reference for TC-009-32)
- **Machine / account**: MacBook
- **Precondition**: you are willing to switch the Mac language

**Steps**

1. *Systemeinstellungen → Allgemein → Sprache & Region* → English first; restart the app; look at the places of TC-009-32 step 2. Back to Deutsch.

**Expected result**

- All texts English; the same wording as on Windows 10 in TC-009-32 (a difference = Fail, AC-28).

#### TC-009-48: AC-43 spot check on the Mac *(essential)*

- **Verifies**: AC-35 (US-008 AC-43)
- **Machine / account**: MacBook
- **Precondition**: —

**Steps**

1. **Regeln** → kid1 → *Ruhezeiten*: **Eintrag hinzufügen**, change its **Start-Zeit**, delete it (trash can).
2. *Applikations-Regeln*: change **Editor**'s minutes by 1, Tab; change back.
3. **Berichte** → kid1 → today.

**Expected result**

- Steps 1, 2: saved ≤ 5 s, as in 0.8.0. Step 3: today's lines as on the W11 app.

### Session 5 — Two parent apps at once (W11 app on the service PC + Mac app) · ≈ 45 min

Put the service PC and the MacBook side by side. "W" = W11 app, "M" = Mac app. In each step, watch the **other** app without touching it; count to 5.

#### TC-009-49: Both apps paired and connected at the same time *(essential)*

- **Verifies**: AC-24 (both connected), US-007 AC-25
- **Machine / account**: Service PC / Admin (W) and MacBook (M)
- **Precondition**: Sessions 2 and 4 done

**Steps**

1. Look at both status bars.

**Expected result**

- Both green **"Verbunden mit 192.168.1.37"**.

#### TC-009-50: Group changes on the App Groups page, both directions *(essential)*

- **Verifies**: AC-25 (App Groups page), US-008 AC-14 (≤ 5 s)
- **Machine / account**: W and M, both on **App Gruppen**
- **Precondition**: TC-009-49

**Steps**

1. W: **Gruppe hinzufügen** `Sync`.
2. M: rename **Sync** → `Sync M`.
3. W: drag **Paint** into **Sync M**.
4. M: **≡** on **Paint** → **In keiner Gruppe**.
5. M: delete **Sync M**.
6. Both at the same moment (count down): W moves **Editor** into **Games**, M moves **Editor** into **School**. Wait 5 s.
7. Move **Editor** back to *In keiner Gruppe*.

**Expected result**

- Steps 1 to 5, 7: each change appears in the other app **≤ 5 s** without any action there.
- Step 6: after 5 s both apps show Editor in the **same** group (the last change the service received; write which in Notes).

#### TC-009-51: Group rule changes on Rules, both directions; Rules follows the App Groups page *(essential)*

- **Verifies**: AC-25 (Rules), AC-21 (Rules page open, change on App Groups ≤ 5 s), US-008 AC-18
- **Machine / account**: W and M
- **Precondition**: both on **Regeln** → **kid1**

**Steps**

1. W: **Games** **Minuten** `10` → **Tab**.
2. M: **Games** tick **MO** → watch W. M: untick it.
3. Leave W on **Regeln** → kid1. M → **App Gruppen**: move **Paint** into **School**; then create group `Live`; rename **Live** → `Live 2`; delete **Live 2**; move **Paint** back. Watch W after each.
4. Swap roles: M on **Regeln** → kid1, W on **App Gruppen**: W moves **Rechner** into **Games** and back. Watch M.
5. Both at the same moment: W **Games** **Minuten** `20`, M `40`, both **Enter**. Wait 5 s. Then **Games** back to 0:00, no day.

**Expected result**

- Steps 1, 2: each change in the other app ≤ 5 s.
- Steps 3, 4: the open Rules page follows ≤ 5 s: Paint leaves/returns to *Applikations-Regeln*, School's member line changes, the group row appears, is renamed and disappears; Rechner leaves Rechnen's line and returns; no grouped app shows under *Applikations-Regeln*.
- Step 5: both show the same minutes (20 or 40).

#### TC-009-52: Reports group lines follow a move made in the other app, both directions *(essential)*

- **Verifies**: AC-25 (Reports), AC-23 (other parent app, ≤ 5 s, no "Wird geladen …"), US-008 AC-37, D-12
- **Machine / account**: W and M
- **Precondition**: kid1 has Rechner time today (Rechnen ≈ 00:03)

**Steps**

1. W on **Berichte** → kid1 (open). M: **App Gruppen** → move **Rechner** into **School**. Watch W.
2. M on **Berichte** → kid1 (open). W: move **Rechner** back into **Rechnen**. Watch M.

**Expected result**

- Step 1: in W, today (and past days with Rechner time) show **School** instead of **Rechnen** ≤ 5 s, without **Wird geladen …**.
- Step 2: in M, **Rechnen** is back ≤ 5 s, without **Wird geladen …**. Both apps show the same values.

#### TC-009-53: Existing settings sync both directions *(essential)*

- **Verifies**: AC-26, US-007 AC-33
- **Machine / account**: W and M
- **Precondition**: both on the same page (as each step says); kid1 selected on Regeln

**Steps** (each: watch the other app ≤ 5 s)

1. **Einstellungen**: W unticks **kid2**; M ticks it again.
2. **Regeln** → kid1 → *Ruhezeiten*: M **Eintrag hinzufügen**; W changes its **End-Zeit** to `23:00` → **Enter**; M deletes it.
3. *Ruhezeiten* **Anzeige Text:** W adds ` Test` → **Tab**; M removes it → **Tab**.
4. *Applikations-Regeln*: M changes **Editor** minutes by +1 → **Tab**; W changes them back → **Tab**.
5. *Anzeige Text wenn Budget aufgebraucht:* W adds ` X` → Tab; M removes it → Tab.
6. **Einstellungen → Zeit-Ende Warnungen**: M **Frühe Warnung** `4` → Tab; W back to `3` → Tab. W types a text in the **Finale Warnung** text `Gleich Schluss`; M empties it (back to the default).

**Expected result**

- Every change shows in the other app **≤ 5 s** without any action; at the end everything is as before (kid2 ticked, no extra break, texts and values as before, early 3, final default text).

#### TC-009-54: Removing the pairing in one app does not affect the other *(essential)*

- **Verifies**: AC-24 (remove pairing), US-002 AC-29, US-007 AC-25
- **Machine / account**: W and M; service PC for the code
- **Precondition**: both green

**Steps**

1. M: **Einstellungen → Kopplung aufheben** (confirm). Watch W for 30 s; click through its pages.
2. M: pair again: host `192.168.1.37` → **Verbinden** → code from the service PC → same device name → **Koppeln**.

**Expected result**

- Step 1: M is *Nicht gekoppelt*; W stays green **"Verbunden mit 192.168.1.37"** and works.
- Step 2: M green again (needed as the remote parent app in Session 6B).

### Session 6A — Windows app regression spot checks (service PC / Admin) · ≈ 20 min

#### TC-009-55: Connection error naming the host; "Kopplung auf diesem Gerät löschen" offered *(essential)*

- **Verifies**: AC-34 (US-002 AC-12, ISSUE-009)
- **Machine / account**: Service PC / Admin, Terminal (Administrator)
- **Precondition**: W green

**Steps**

1. Close W. `Stop-Service -DisplayName "EagleEye Service"`. Start W; wait up to 60 s. Go to **Einstellungen**.
2. Click **Kopplung auf diesem Gerät löschen** → read the dialog → **Abbrechen** (do **not** confirm).
3. `Start-Service -DisplayName "EagleEye Service"`. Wait 60 s.

**Expected result**

- Step 1: a connection error that **names 192.168.1.37**; red; on *Einstellungen* **Kopplung aufheben** disabled and the button **Kopplung auf diesem Gerät löschen** shown.
- Step 2: dialog **"Kopplung auf diesem Gerät löschen?"** with the explanation and **Kopplung auf diesem Gerät löschen** / **Abbrechen**; Abbrechen keeps the pairing.
- Step 3: green ≤ 60 s without any action; the forget button disappears.

#### TC-009-56: Settings and Rules spot checks (US-003, US-005, US-006) *(essential)*

- **Verifies**: AC-34 (ticks, break times, app rule, budget text, warning minutes with one validation message)
- **Machine / account**: Service PC / Admin, LOG-WATCH
- **Precondition**: W green

**Steps**

1. **Einstellungen**: untick **kid2**, tick it again.
2. **Regeln** → kid1 → *Ruhezeiten*: **Eintrag hinzufügen**; change **Start-Zeit** to `21:00` → **Enter**; delete it. **Anzeige Text:** add ` !` → Tab; remove it → Tab.
3. *Applikations-Regeln*: **Editor** minutes +1 → Tab; back → Tab. Budget text: add ` X` → Tab; remove → Tab.
4. **Einstellungen → Zeit-Ende Warnungen**: **Finale Warnung** `0` → Tab; then `3` (= early) → Tab; then `1` → Tab.

**Expected result**

- Steps 1 to 3: each change stored ≤ 5 s (log line), the field keeps its value; no message.
- Step 4: `0` → **"Bitte Minuten zwischen 1 und 59 eingeben."**; `3` → **"Die finale Warnung muss weniger Minuten vor dem Ende liegen als die frühe Warnung."**; `1` stored.

### Session 6B — Group enforcement and the US-008 retest (service PC / kids; MacBook as remote parent app) · ≈ 1 h 15

**Preparation P-1 to P-6** (≈ 10 min, **on a day on which kid1 has not used Editor or Paint yet**):

- **P-1** W11 app (AC-27 needs the rule set in the **Windows app**): first **Regeln** → kid1 → *Applikations-Regeln*: note kid1's own rules for **Editor** and **Paint**, then set **Paint 0:00, no day** (US-008 AC-24 check) and **Editor** e.g. 0:30, all days. Then **App Gruppen** → **Gruppe hinzufügen** `Test` → move **Editor** and **Paint** into **Test**.
- **P-2** W11 app → **Regeln** → kid1 → *App-Gruppen-Regeln*: **Test** **Stunden 0**, **Minuten 5**, **today ticked** only. kid2: Test stays **0:00, no day**.
- **P-3** W11 app → **Regeln** → kid1 → *Applikations-Regeln*: Editor and Paint are no longer listed.
- **P-4** *Einstellungen*: **Frühe Warnung 3**, **Finale Warnung 1**; kid1, kid2 ticked, kid3 not.
- **P-5** Mac (M): **Berichte** → kid1: today shows **no** Editor and Paint usage, and no **Test** line (if it does, tell TES or shift all times by that amount). M stays on **Regeln** → kid1, *App-Gruppen-Regeln* visible.
- **P-6** *Benutzer wechseln* → kid1. **K-TERM** with LOG-WATCH. No other app open. Stopwatch ready. **Stay in kid1's session; do not lock** during a timed case.

#### TC-009-57: The group rule set in the Windows app takes effect: shared budget, warnings naming the group, all members closed, one dialog *(essential, the core case)*

- **Verifies**: AC-27, AC-29 (German), AC-36; US-008 AC-19 (union), AC-24 (own rule ignored), AC-26 (warnings once per group), AC-27 (all closed, one dialog), AC-31 (log)
- **Machine / account**: Service PC / kid1, K-TERM
- **Precondition**: P-1 to P-6

**Steps**

1. Windows+R `notepad`; **stopwatch on Enter**. Type nothing.
2. **Lap 1** when a dialog appears; read it; **OK**.
3. At **3:00** on the stopwatch: Windows+R `mspaint`. Draw nothing.
4. **Lap 2** at the next dialog; read; **OK**.
5. **Lap 3** at the next dialog; **lap 4** when both windows are gone. Read; **OK**.
6. K-TERM: LOG-FIND `Test`.

**Expected result**

- Step 1: Notepad starts normally.
- Lap 1 between **2:00 and 2:15**: early warning with **"Test: noch 3 Minuten."** (group name, not "Editor").
- Step 3: **Paint starts and stays open** although its own rule is 0:00 (own rule ignored in the group).
- Lap 2 between **4:00 and 4:15**: final warning **"Test: noch 1 Minute."**; each warning **once** (no second early warning when Paint started).
- Lap 3 between **5:00 and 5:15**: **one** dialog with kid1's budget text and **"Test: Zeit-Budget aufgebraucht."** — not one per app; lap 4 ≤ **5:35**: **Notepad and Paint both closed**.
- Log: warning lines naming kid1, group **Test**, early / final; close lines for Editor and Paint naming group Test, budget used up, closed gracefully.

#### TC-009-58: Blocked start when exhausted and when not allowed today; Reports *(essential)*

- **Verifies**: US-008 AC-25, AC-19 (Reports ±1), AC-23, AC-31, AC-35 (sorting), AC-37 (≤ 15 s on the Mac); AC-22 (W11 = Mac, sorting)
- **Machine / account**: Service PC / kid1, K-TERM; Mac (M)
- **Precondition**: TC-009-57

**Steps**

1. Windows+R `mspaint`. Read; **OK**.
2. M: **Berichte** → kid1: today. Note Editor, Paint, **App-Gruppen**.
3. M: *Regeln* → kid1 → **Test**: **Minuten** `30`, **untick today** → **Tab**. Kid1: Windows+R `notepad`. Read; **OK**.
4. M: **Test** tick today (0:30) → **Tab**. Kid1: Windows+R `notepad` → keep it open **2 minutes** while M shows **Berichte** → kid1 (watch Test and Editor). Close Notepad.
5. K-TERM: LOG-FIND `Blocked start`.
6. Later (switch to your account): W11 app **Berichte** → kid1: compare with M.

**Expected result**

- Step 1: blocked ≤ 10 s, gone ≤ 30 s; dialog with the budget text and **"Test: Zeit-Budget aufgebraucht."**
- Step 2: **Test 00:05** (±1, union: 3 + 2 overlapping counts once); **Editor 00:05**, **Paint 00:02** (±1, app usage counted as before). App-Gruppen sorted by usage: **Test** above **Rechnen**. The blocked start of step 1 added nothing.
- Step 3: blocked with **"Test: heute nicht erlaubt."**
- Step 4: Notepad starts (more time lets the kid start again, US-008 AC-33; no early or final warning, because 25 minutes are left). M's Test and Editor lines grow by themselves with ≤ 15 s lag.
- Step 5: "Blocked start" lines naming group Test and the reason (budget used up / not allowed today).
- Step 6: W11 shows the **same** group values as M.

#### TC-009-59: A new app appears on the App Groups page within 15 seconds *(essential)*

- **Verifies**: US-008 AC-4 (Mac, timed), AC-13 (W11: listed without any action, after switch-back)
- **Machine / account**: Service PC / kid1; M
- **Precondition**: M on **App Gruppen**; choose an app kid1 never used (`colorcpl`; if *Farbverwaltung* is already listed: `odbcad32`)

**Steps**

1. Kid1: Windows+R `colorcpl` (stopwatch). It is blocked by deny-by-default: **OK**.
2. Watch M's *In keiner Gruppe*.
3. Later, in your session: W11 app **App Gruppen** (do not touch anything else before looking).

**Expected result**

- Step 1: blocked with "… heute nicht erlaubt." (own rule 0:00, as US-006).
- Step 2: the app appears in **In keiner Gruppe** ≤ **15 s**, in its alphabetical place, once.
- Step 3: listed there too.

#### TC-009-60: Other accounts: kid2 blocked by its own group rule (English texts); kid3 and the admin are never limited *(essential)*

- **Verifies**: US-008 AC-21, AC-25, AC-29; AC-29 (English)
- *Group time of kid1 is not touched: nothing runs in kid1's session meanwhile.*
- **Machine / account**: Service PC / kid2 (English Windows, S-6), kid3, Admin; M
- **Precondition**: kid2's Test rule 0:00, no day (P-2). Sign kid1 out (close K-TERM first).

**Steps**

1. M: **Berichte** → kid2: today.
2. *Benutzer wechseln* → kid2. Windows+R `notepad`. Read the dialog; **OK**.
3. Sign kid2 out. *Benutzer wechseln* → **kid3**: Windows+R `notepad` and `mspaint`; keep both open **1 minute**; close them; sign kid3 out.
4. In **your** session: Windows+R `notepad` and `mspaint` for 1 minute; close them.
5. M: **Berichte**: is kid3 listed under **Konto**? kid1's Test line: unchanged by steps 3 and 4?

**Expected result**

- Step 1: no **Test** line for kid2 (kid1's time does not count).
- Step 2: blocked ≤ 10 s with kid2's budget text and **"Test: not allowed today."** in English (German Windows for kid2: "Test: heute nicht erlaubt."; then write "kid2 not English" in Notes: the English part of AC-29 stays open).
- Steps 3, 4: no dialog, nothing closed (kid3 is not controlled; admins are never limited).
- Step 5: kid3 not listed; kid1's Test value unchanged (other accounts never count).

#### TC-009-61: Changes during the day: a rule change that ends the time gives a grace minute *(essential)*

- **Verifies**: US-008 AC-32 (rule change), AC-33 (more time lets the kid start again)
- **Machine / account**: Service PC / kid1, K-TERM; M on **Regeln** → kid1
- **Precondition**: Test 0:30, today ticked (TC-009-58 step 4); *Benutzer wechseln* → kid1 again, K-TERM with LOG-WATCH

**Steps**

1. Kid1: Windows+R `mspaint` (starts: time left). Wait 20 s.
2. M: **Test** **Minuten** `5` → **Tab** = **lap C**. Kid1: watch.
3. Read the dialog(s); **OK** each time. Note when Paint is gone.

**Expected result**

- Step 1: Paint starts.
- Step 2: final warning ≤ **C + 15 s** with **"Test: Deine Eltern haben die Regeln geändert. Test wird in 1 Minute geschlossen."** (wording difference = Note; the group name must be in it).
- Step 3: Paint closed between **C + 60 s** and **C + 75 s** with the budget text; gone ≤ **C + 95 s**. One warning, not one per app.
- Log: "Grace period: … group Test …".

#### TC-009-62: A running app moved into an exhausted group gets a grace minute; its earlier time joins the group *(essential)*

- **Verifies**: US-008 AC-32 (move), AC-22 (current members, earlier time added), AC-36 (lines follow members)
- **Machine / account**: Service PC / kid1; M on **App Gruppen**
- **Precondition**: Test exhausted (0:05, used > 5); Rechner in Rechnen (0:30, ≈ 3 min used today)

**Steps**

0. M: **Berichte** → kid1 → today: note **Test** and **Rechner**. Then M → **App Gruppen**.
1. Kid1: Windows+R `calc` (starts). Wait 20 s.
2. M: drag **Rechner** into **Test** = **lap C**.
3. Read the dialog; **OK**. Note when Rechner is gone.
4. M: **Berichte** → kid1: today and past days.

**Expected result**

- Step 2: final warning ≤ C + 15 s naming **Test**.
- Step 3: Rechner closed between C + 60 s and C + 75 s.
- Step 4: today **Test** ≈ the Test value of step 0 + Rechner's usage today (Rechner's line, incl. this case) ± 1 (Rechner never ran together with Editor or Paint, so nothing overlaps); **Rechnen** is no longer listed today (no members); past days that had Rechnen time show it under **Test** now.

#### TC-009-63: Deleting a group with a running app: the app falls back to its own rule *(essential)*

- **Verifies**: US-008 AC-32 (delete), AC-30 (ungrouped app as US-006), AC-10 (apps back to own rules)
- **Machine / account**: Service PC / kid1; M
- **Precondition**: Paint's own rule 0:00 (P-1); Test holds Editor, Paint, Rechner

**Steps**

1. M: **Test** → **Minuten** `45` → **Tab**. Kid1: Windows+R `mspaint` (starts). Wait 20 s.
2. M: **App Gruppen** → trash can on **Test** → **Löschen** = **lap C**.
3. Read the dialog; **OK**. Note when Paint is gone.
4. M: **App Gruppen** and **Regeln** → kid1.

**Expected result**

- Step 2: final warning ≤ C + 15 s, now naming **Paint** (own rule): "Paint: Deine Eltern haben die Regeln geändert. Paint wird in 1 Minute geschlossen."
- Step 3: Paint closed between C + 60 s and C + 75 s.
- Step 4: Editor, Paint, Rechner under *In keiner Gruppe*; in *Applikations-Regeln* again with their own earlier rules (Paint 0:00). Move **Rechner** back into **Rechnen**.

#### TC-009-64 (optional): Rename and harmless changes show no dialog; a change within the early level shows the early warning *(optional)*

- **Verifies**: US-008 AC-34 (rename, harmless move), AC-33 (early warning at once)
- **Machine / account**: Service PC / kid1; M
- **Precondition**: Rechnen 0:30 for kid1 with ≈ 3 min used today

**Steps**

1. Kid1: Windows+R `calc`. M: rename **Rechnen** → `Rechnen X`. Wait 15 s. Rename back.
2. M: move **Paint** (not running) into **Rechnen**; wait 15 s.
3. M: **Berichte** → kid1: read Rechnen's used minutes (round up) = U. **Regeln** → kid1: **Rechnen** **Minuten** = U + 2 (2 minutes left: within the early level 3, above the final level 1) → **Tab** = C.
4. Close Rechner. Move Paint back; Rechnen back to 0:30.

**Expected result**

- Steps 1, 2: **no** kid dialog (Paint's earlier time leaves well over 3 minutes).
- Step 3: the **early** warning at once (≤ C + 15 s) naming Rechnen; no grace close.

#### TC-009-65 (optional): Break time wins over a group rule *(optional)*

- **Verifies**: US-008 AC-28
- **Machine / account**: Service PC / kid1; M
- **Precondition**: Rechnen 0:30 with time left

**Steps**

1. M: **Regeln** → kid1 → *Ruhezeiten*: **Eintrag hinzufügen**, **Start-Zeit** = now − 1 minute, **End-Zeit** 23:59, all days, **An/Aus** on → Enter.
2. Kid1: Windows+R `calc`. Read; **OK**.
3. M: break entry off and deleted.

**Expected result**

- Step 2: blocked with the break display text (not the group text).

#### TC-009-66 (optional): Group time does not count while the session is switched away *(optional)*

- **Verifies**: US-008 AC-20 (not counted when switched away)
- **Machine / account**: Service PC / kid1; M
- **Precondition**: Rechnen with time left

**Steps**

1. Kid1: `calc`. After 1 minute, note M's Rechnen value; *Benutzer wechseln* to your account for **3 minutes**; back to kid1; close Rechner; M's value.

**Expected result**

- Rechnen grew by about the 1 minute plus the time after coming back, **not** by the 3 minutes away (±1).

#### TC-009-67: Groups, members, rules and group time survive a service restart and an app restart *(essential; step 4 optional)*

- **Verifies**: US-008 AC-38, AC-20 (kept across service restarts)
- **Machine / account**: Service PC / Admin; M
- **Precondition**: sign kid1 out; note M's App Gruppen, kid1's group rules and today's group lines

**Steps**

1. Terminal (Administrator): `Restart-Service -DisplayName "EagleEye Service"`. Wait until W and M are green.
2. W and M: **App Gruppen**, **Regeln** → kid1, **Berichte** → kid1.
3. Close W and start it again; same pages.
4. *(optional)* Reboot the service PC; same pages afterwards.

**Expected result**

- Everything as noted: groups with members, group rules, today's group lines (no time lost or added).

### Session 6C — Storage and accounts (service PC / Admin; M) · ≈ 15 min

#### TC-009-68: An account ticked for the first time gets 0:00, no day, for every group *(essential)*

- **Verifies**: US-008 AC-39 (first tick)
- **Machine / account**: W
- **Precondition**: kid3 has never been ticked since groups exist; kid3 signed out

**Steps**

1. **Einstellungen**: tick **kid3**. **Regeln** → **Konto** kid3 → *App-Gruppen-Regeln*.
2. Untick **kid3** again at once.

**Expected result**

- Step 1: every group (Games, Rechnen, School) **0:00**, no day ticked.
- Step 2: kid3 unticked (it must not stay controlled: deny-by-default would block its apps).

#### TC-009-69: Untick keeps the group rules; an app stays in its group when its only account is unticked *(essential)*

- **Verifies**: US-008 AC-39 (untick keeps, re-tick applies), AC-40
- **Machine / account**: W; M
- **Precondition**: the new app of TC-009-59 (e.g. Farbverwaltung) has a record for kid1 only; kid1 signed out

**Steps**

1. M: move the new app into **Rechnen**.
2. W: **Einstellungen**: untick **kid1**. M: **App Gruppen**.
3. W: tick **kid1** again. M: **App Gruppen**; **Regeln** → kid1.

**Expected result**

- Step 2: the new app is no longer listed (no controlled account has a record of it); Rechnen shows only Rechner.
- Step 3: the new app is back **in Rechnen**; kid1's Rechnen rule is unchanged (0:30, MO–SO). Move the new app back to *In keiner Gruppe* (or leave it).

#### TC-009-70 (optional): A newly ticked account with an existing group rule behaves as before after re-tick (enforcement) *(optional)*

- **Verifies**: US-008 AC-39 (rules apply again after re-tick)
- **Machine / account**: Service PC / kid1
- **Precondition**: TC-009-69

**Steps**

1. Kid1: `calc`; check M's Rechnen line grows (≤ 15 s); close.

**Expected result**

- Rechner starts (Rechnen 0:30 applies again) and its time counts for Rechnen.

### Session 6D — Optional pilot checks and clean-up check

#### TC-009-71 (optional, pilot): Performance with 20 groups and 100 apps; live group smoke *(optional pilot check)*

- **Verifies**: AC-38 (US-008 AC-41, US-008 plan Step 7.3)
- **Machine / account**: service PC in pilot use
- **Precondition**: pilot operation; record observations whenever convenient

**Steps**

1. In pilot use: is starting an allowed app noticeably slower than before? *Task-Manager* → EagleEye Service CPU while kids use several apps (US-004 AC-25: on average below the US-004 limit).

**Expected result**

- Not noticeably slower; CPU within US-004 AC-25. "Not tested" does not block closing (story AC-38).

#### TC-009-72 (optional): "Noch keine App-Gruppen …" when no group exists *(optional; at the end of the run, only if you want to remove all test groups)*

- **Verifies**: AC-19 ("no app groups yet" text), US-008 AC-15
- **Machine / account**: W
- **Precondition**: you want to remove all test groups (Games, Rechnen, School); note Rechnen's rule if you want to keep it

**Steps**

1. Delete every group. **Regeln** → kid1 → *App-Gruppen-Regeln*. **App Gruppen**.

**Expected result**

- Rules: **"Noch keine App-Gruppen. Du kannst sie auf der Seite App Gruppen anlegen."** App Gruppen: only *In keiner Gruppe* with all apps.

## 6. Regression

None beyond the story's own regression ACs (AC-31 REG-001, AC-34, AC-35, AC-36, AC-37), which are story cases here (`02_Implementation/docs/testing/README.md` §3).

## 7. Cleanup

1. **LOG-COPY** with `<subfolder>` = `run-01-service-logs` at the end of each session day. Look through the copies before committing.
2. **kid accounts**: kid3 unticked. kid2's display language back to German if you prefer (untick kid2 first, as S-6).
3. **Rules**: Paint's and Editor's own rules for kid1 back to what you want (P-1 notes). Warning levels back to your values (S-9).
4. **Groups**: keep or delete the test groups (Games, Rechnen, School; Test was deleted in TC-009-63). Real kid accounts: every new group is blocked until its rule is set (by design).
5. **W10 PC**: uninstalled in TC-009-34. Its device entry stays on the service (no UI to remove it, US-007 F-4). Reinstall later for the pilot if wanted.
6. **Mac**: stays on 0.9.0, paired. **W11 app**: stays installed, paired.
7. Accent colour, scaling, display languages back to your settings.

## 8. Notes and Open Questions for Michael

| ID | Question | Proposed answer |
|---|---|---|
| Q-1 | **Account roles**: kid1, kid2, kid3 are the story's names. Which accounts? kid2 should have **English** Windows for AC-29 (S-6: untick, change the language, re-tick). | kid1 = `eagleeye-kid`; kid2 and kid3 as you have them (or created in S-5). If kid2 cannot be English, the English part of TC-009-60 is Blocked and AC-29 English stays open. |
| Q-2 | **Baseline group with the Mac app 0.8.0 before the update** (S-10): creates "Rechnen" and 2 minutes of Rechner use, so that AC-31 ("keeps app groups, members, group rules and the group lines of past days") can be seen. Past-day group lines exist only if kids used grouped apps after the 0.8.0 install. | Yes. If no past-day lines exist, TC-009-05 checks today's line and records "no past days available". |
| Q-3 | **English on Windows 11** (AC-28 is tagged [W11] and [W10]): switching your admin account's display language means signing out twice. | English fully on Windows 10 (TC-009-32, same build); Windows 11 English optional (TC-009-25). |
| Q-4 | **Mac AC-35 depth**: the story asks for the US-008 Mac ACs "in full"; DEV states the Mac code is unchanged since 0.8.0, where your 12-point check passed. | The Mac cases (TC-009-38 to -48) are written as quick re-confirms of the 12 points (≈ 1 h 15 with the update). OK? |
| Q-5 | **Live "≤ 15 s" checks on Windows 11** (AC-13 new app, AC-23 / AC-34 today's usage): used time only counts while the kid's session is in front, so the W11 app on the same PC cannot be watched while the kid uses apps. | W11: "switch back and look without touching anything" (TC-009-24, -59); the timed check on the Mac (same shared code, TC-009-58, -59). If you have a second Windows 11 parent PC (e.g. LEOSERV), it can replace the Mac in these steps. |
| Q-6 | **US-008 AC-6** (no groups after an update from 0.7.x) cannot be tested any more (the service is on 0.8.0). | Record as "not testable"; evidence: the 0.8.0 install log (`US-008/evidence/install-0.8.0-log-excerpt.txt`, "App groups loaded: 0 groups"). |
| Q-7 | **Uninstall on Windows 10 while paired** (TC-009-34): the only way to see that uninstalling removes the pairing data. The device entry "Windows 10 PC" stays on the service (no UI to remove other devices, US-007 F-4). | Accept the orphan entry. Alternative: *Kopplung aufheben* first, then the case only shows that the folders are gone. |
| Q-8 | **Service PC address** `192.168.1.37` (story). The US-007 report names `192.168.1.85`. | Check in S-3; use the real address. |
| Q-9 | **Save error** (AC-18 / US-008 AC-14 error path) only in optional cases (TC-009-18, -44): it needs the service process suspended with `resmon`. | Optional, as in US-006 (TC-006-11). |
| Q-10 | **Session 6B timing**: it needs a day on which kid1 has not used Editor or Paint before P-5, because group time counts the whole day. | Run Session 6B first thing on a day, or move the group to two other apps and tell TES. |
| Q-11 | **72 cases, ≈ 8 h essential.** Larger than usual; you accepted the larger run with the story. | Three days: Sessions 0–2, 3–5, 6. Partial runs are fine; unticked cases count as not executed. |

## 9. Change Log

| Date | Change | Reason |
|---|---|---|
| 2026-11-01 | Plan written after Michael's approval of step c (0.9.0, 2026-11-01). | Step d. |

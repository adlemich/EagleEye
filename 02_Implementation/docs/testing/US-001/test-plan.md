# Test Plan: US-001 — Basic Service Installation and Tray Client Connectivity

**Status**: Draft (approved by executing `test-run-01.md`, or by Michael's explicit approval)
**Date**: 2026-10-03
**Author**: TES
**User Story**: `docs/requirements/user-stories/US-001/user-story.md`

---

## 1. Scope

**In scope**: installation of service + tray client (AC-1), service registration, account and start type (AC-2, AC-3), service control via `services.msc` (AC-4), tray auto-start in a standard-user session (AC-5), tray connection indicator and reconnect (AC-6 to AC-9), version identifier and About dialog (AC-10 to AC-12).

**Out of scope** (as in the user story): parent app, pairing, monitoring/enforcement, TLS, installer repair/uninstall flows, tray icon in admin sessions.

## 2. Test Environment

| Item | Value |
|---|---|
| Test machine | Windows Developer Machine (Windows 11 Pro, German UI) |
| Accounts | **Admin**: Michael's administrator account. **Kid**: local standard account `eagleeye-kid` (created in setup) |
| Build under test | `03_Delivery/windows/EagleEye-Setup-0.1.0.exe` |
| Expected version | Installer `0.1.0` → service version `EagleEye_v0.1` |
| Time needed | about 45 minutes including one reboot |

## 3. Setup Instructions

1. **No previous installation.** Settings → Apps → Installed apps (*Einstellungen → Apps → Installierte Apps*): "EagleEye" must not be listed. If it is, uninstall it first.
2. **Port free.** In PowerShell: `Get-NetTCPConnection -LocalPort 5080 -ErrorAction SilentlyContinue`. It must return nothing. If something is listed, stop that program (for example a console instance of `EagleEye.Service.exe` from development).
3. **Kid account.** If `eagleeye-kid` does not exist yet, open **Terminal (Admin)** and run:
   ```powershell
   $pw = Read-Host -AsSecureString "Password for eagleeye-kid"
   New-LocalUser -Name "eagleeye-kid" -Password $pw -FullName "EagleEye Kid" -PasswordNeverExpires
   Add-LocalGroupMember -Group (Get-LocalGroup -SID "S-1-5-32-545").Name -Member "eagleeye-kid"
   ```
   (SID `S-1-5-32-545` is the built-in *Users / Benutzer* group, so the command works on a German Windows.) Sign in once as `eagleeye-kid` to finish the profile creation, then sign out.
4. **Installer unsigned.** SmartScreen may show *"Windows hat Ihren PC geschützt"*. Click *Weitere Informationen* → *Trotzdem ausführen*. This is expected and not a test failure.

## 4. Acceptance Criteria Coverage

| AC | Test case(s) |
|---|---|
| AC-1 | TC-001-01 |
| AC-2 | TC-001-02 |
| AC-3 | TC-001-12 |
| AC-4 | TC-001-03 |
| AC-5 | TC-001-04, TC-001-13 |
| AC-6 | TC-001-05, TC-001-08 |
| AC-7 | TC-001-05, TC-001-13 |
| AC-8 | TC-001-08 |
| AC-9 | TC-001-10, TC-001-11 |
| AC-10 | TC-001-07 |
| AC-11 | TC-001-06 |
| AC-12 | TC-001-07 |
| AC-13 | TC-001-09 (from run 02; added 2026-10-03, ISSUE-003) |
| *(run 01 only)* | TC-001-09 was exploratory in run 01, before AC-13 existed |

## 5. Test Cases

The cases are ordered to minimise account switching. The full step-by-step version with result boxes is `test-run-01.md`.

| ID | Title | Machine / account | Verifies |
|---|---|---|---|
| TC-001-01 | Installer runs with default settings and installs both components | Windows / Admin | AC-1 |
| TC-001-02 | Service registered, running as Local System, startup type Automatic | Windows / Admin | AC-2 |
| TC-001-03 | Service can be stopped and started in `services.msc` | Windows / Admin | AC-4 |
| TC-001-04 | Tray client starts automatically at kid logon | Windows / Kid | AC-5 |
| TC-001-05 | Tray icon is green and tooltip says connected | Windows / Kid | AC-6, AC-7 |
| TC-001-06 | Right-click shows a context menu with "About" | Windows / Kid | AC-11 |
| TC-001-07 | About shows the live server version `EagleEye_v0.1` | Windows / Kid | AC-10, AC-12 |
| TC-001-08 | Stopping the service turns the tray icon red | Windows / Kid (+ admin rights) | AC-6, AC-8 |
| TC-001-09 | *Exploratory*: About while the service is stopped | Windows / Kid | — (PRO feedback) |
| TC-001-10 | Restarting the service turns the icon green again automatically | Windows / Kid (+ admin rights) | AC-9 |
| TC-001-11 | Reconnect also works after a long outage (≥ 2 min) | Windows / Kid (+ admin rights) | AC-9 |
| TC-001-12 | Service starts automatically after reboot | Windows / Admin | AC-3 |
| TC-001-13 | After reboot, the kid logon auto-starts a green tray icon | Windows / Kid | AC-5, AC-7 |

**Pass criteria for timing** (the story does not give numbers; TES uses these as reasonable bounds): a disconnect is shown within **30 s**, and a reconnect is shown within **60 s** after the service is running again. Slower but eventual success is recorded as a Fail with a note, and the PRO is asked to specify timing.

## 6. Regression

None. US-001 is the first story.

## 7. Cleanup

Keep EagleEye installed after the run; later stories build on it. To remove it: Settings → Apps → Installed apps → EagleEye → Uninstall.

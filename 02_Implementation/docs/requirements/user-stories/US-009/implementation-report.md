# Implementation Report: US-009 — Windows Parent App on Windows 10, and Windows Parent App Parity (0.9.0)

**Status**: Windows part (Steps 1 to 3) done, incl. Michael's hands-on part of the Step 2 check (all pass, 2026-10-31); Step 4 (Windows 10 pre-check) **passed** (H2, 2026-10-31); **next: H3 → Step 5 on the MacBook** (§10); the story goes to `Implemented` at the end of Step 5
**Author**: DEV
**Branch**: `feature/US-009-windows-parent-app-parity`
**Plan**: `02_Implementation/docs/requirements/user-stories/US-009/implementation-plan.md` (approved 2026-10-31)

---

## 1. Summary

- **Version 0.9.0** for all four assets (`02_Implementation/Directory.Build.props`).
- **Windows 10**: the parent app installer requires Windows 10 22H2 (`MinVersion=10.0.19045`) instead of Windows 11. The service installer is unchanged (Windows 11).
- **F-2**: the 9 test-code warnings are gone (8 × CS8631, 1 × xUnit2029). Build and tests run with 0 warnings.
- **Windows on-screen check** (Step 2): the US-007/US-008 UI works on WinUI. One Windows-only fix: during a drag, WinUI showed the caption "Kopieren" next to the pointer. It is now hidden (`#if WINDOWS` in `AppGroupsView.xaml.cs`). **No shared change**, so the Mac app stays exactly as in 0.8.0 (AC-35, OQ-6). No Core change, no contract change, no new texts (Q-1).
- **Installers 0.9.0** built (Step 3). No installer was run on this PC (TI-12).

## 2. Deviations from the Implementation Plan

| # | Plan | Done instead | Why |
|---|---|---|---|
| DV-1 | Step 2: DEV pairs the Debug app with the service 0.8.0 (Michael reads out the code) and checks the 11 points live | DEV checked the views with the temporary US-008 probe (sample data, fake models, not committed; source kept as `evidence/step2-ui-probe.cs.txt`), plus a live check of the unpaired start and an unreachable host. The live pairing, the touchpad and the system accent change are on Michael's short list (§8.4). | DEV cannot read the pairing code (it is shown to Michael on this PC) and cannot use a physical touchpad. The probe gives every page in a known state, as in US-008 §8.3. |
| DV-2 | Drag check "with mouse and touchpad" | Mouse checked with real WinUI pointer input (synthetic `mouse_event` moves: drag start, highlight, drop, 20 drags). Touchpad: Michael (§8.4). Done: Michael, pass. | No physical touchpad for DEV. |
| DV-3 | DEV handover rule "smoke check: installer installs" | Not on this PC. The install smoke check is Step 4 on the Windows 10 PC. | TI-12: service 0.8.0 and the Windows app starting point must stay untouched. |

## 3. Files

| File | Change |
|---|---|
| `02_Implementation/Directory.Build.props` | `VersionPrefix` 0.9.0 |
| `02_Implementation/installer/windows/parentapp-setup.iss` | `MinVersion=10.0.19045` and comment |
| `02_Implementation/src/EagleEye.ParentApp/EagleEye.ParentApp.csproj` | comment (minimum OS enforced by the installer) |
| `02_Implementation/src/EagleEye.ParentApp/Views/AppGroupsView.xaml.cs` | Windows-only: hide the drag caption/glyph (TI-5) |
| `02_Implementation/src/EagleEye.ParentApp/README.md` | platform table: Windows 10 22H2 or 11 |
| `03_Delivery/windows/README.md` | both installers and their minimum Windows |
| `02_Implementation/tests/EagleEye.Service.Tests/Data/BlockedStartRepositoryTests.cs`, `AppRuleRepositoryTests.cs`, `AppGroupRepositoryTests.cs` | CS8631: explicit `new string?[] { … }` expected arrays |
| `02_Implementation/tests/EagleEye.ParentApp.Tests/ViewModels/RulesViewModelTests.cs` | xUnit2029: `Assert.DoesNotContain` |
| `02_Implementation/docs/requirements/user-stories/US-009/evidence/step2-*` | screenshots, probe source and log |

## 4. Unit Tests

No production logic changed, so no new tests. Windows, `pwsh 02_Implementation/scripts/test.ps1`: **3,002 tests green** (Shared 305, Service 1,493, TrayClient 95, ParentApp 1,109), **0 warnings**. `build.ps1`: 0 warnings, 0 errors.

## 5. Open Questions and Risks

| # | Point |
|---|---|
| R-1 | **Flaky test (pre-existing, not changed)**: `CloseSequenceRunnerTests.ParallelLimit_FurtherSequencesQueueAndAreDroppedAtStop` failed once (expected 64 inserts, got 65), then passed in every later run. It is a timing race in the test (a queued sequence can insert before `StopAsync` drops it). Unrelated to US-009. Recommend an issue for a later story. |
| R-2 | **AC-33 starting point** (resolved 2026-10-31): the Windows parent app 0.6.2 had been uninstalled earlier by Michael; nothing is installed on this PC. PRO changed AC-33 to a fresh installation on Windows 11; the 0.6.2 → 0.9.0 update path is not tested (`90a1379`). DEV did not install or uninstall anything. |
| R-3 | Icon buttons (pencil, trash can) show the native WinUI button frame (thin border, no fill). This is the same look as the 0.6.2 break-time trash can on Windows; kept (less code, Q-4 spirit). TES judges AC-14. |
| R-4 | Rules page at the minimum window size (900 × 600) and 150 %: the table is wider than the window and scrolls sideways (`ScrollView Orientation="Both"`, unchanged since US-005). Not new. |

## 6. How to Test (preliminary, completed after Step 5)

- **Artifacts** (git-ignored, Windows Developer Machine): `03_Delivery/windows/EagleEye-Setup-0.9.0.exe` (service + tray, Windows 11 only), `03_Delivery/windows/EagleEye-ParentApp-Setup-0.9.0.exe` (Windows 10 22H2 / 11, x64). Mac `.dmg`: Step 5.
- **Starting points**: untouched by DEV (service and tray 0.8.0 on this PC; no Windows parent app installed, AC-33 is a fresh installation, R-2).
- **Known limitations**: drag shows no caption on Windows (by design, §1); Rules page scrolls sideways at the minimum size (R-4).
- **Logs**: service `%ProgramData%\EagleEye\logs\EagleEye.Service-NNN.log`.

## 7. Machine(s) Used

| Steps | Machine |
|---|---|
| 1 to 3 | Windows Developer Machine (Windows 11, 150 % scaling, dark mode, red accent) |
| 4 | Windows 10 PC (Michael) — passed 2026-10-31 |
| 5 | MacBook — open |

---

## 8. Windows Part (Steps 1 to 3)

### 8.1 Step 1

Done as planned (Decisions 1, 5, 6). Commit `fb86ebf`.

### 8.2 Step 2 — DEV on-screen check (Debug build, own data folder under `02_Implementation/artifacts/`)

Window 1100 × 720 and 900 × 600 (minimum), 150 % scaling, dark and light, accents red (system), blue, yellow, green (probe). Evidence in `US-009/evidence/`.

| # | Check item (plan Decision 2) | Result | Evidence |
|---|---|---|---|
| 1 | Unpaired start → Settings with host dialog; unreachable host → error naming the host | Pass (live) | `step2-14-live-unreachable-host.png` |
| 1 | Paired start → Reports; "Forget pairing" when unreachable | Paired start: Pass (Michael, M-1). "Forget pairing": TES | — |
| 2 | Menu: four entries in order; clicked through all four twice → only one highlighted | Pass; native WinUI highlight kept (Q-3) | `step2-11-…` |
| 3 | Headings outlined in the accent colour, text normal colour, buttons distinct | Pass (probe accents). Live accent change while running: Pass (Michael, M-4) | `step2-13-…` |
| 4 | App Groups: explanation ("Trackpad", Q-1), add button, sort, "In keiner Gruppe" last, empty text, tooltip with program path | Pass | `step2-01-…`, `step2-05-…` |
| 5 | Pencil / trash-can buttons: size, both themes | Pass (native frame, R-3). Their tooltips: Pass (Michael, M-3) | `step2-01-…`, `step2-13-…` |
| 6 | Name dialog loop: empty → message, duplicate ("games") → message, emoji ("Musik 🎮") accepted and sorted, Escape, rename pre-filled, delete confirmation with title | Pass. No "single ContentDialog" error, so no `Task.Yield` was needed (TI-6) | `step2-09-…`, `step2-10-…` |
| 7 | Drag-and-drop (Decision 4) | Pass with the mouse (§8.3); touchpad and mouse live: Pass (Michael, M-2) | `step2-02` to `step2-08` |
| 8 | "≡" and right-click → "Verschieben nach …" with the right targets, choice moves, Escape cancels | Pass | `step2-06-…`, `step2-07-…` |
| 9 | Rules: "App-Gruppen-Regeln", members line, "(keine Apps)", grouped apps hidden from "Applikations-Regeln", checkbox columns at 150 % | Pass ("Loading … after reconnect": TES) | `step2-12-…` |
| 10 | Reports: "App-Gruppen" table under a day, none for a day without group lines | Pass (refresh after a live move: Pass, Michael M-5) | `step2-13-…` |
| 11 | Settings, break times with trash can, app rules, warning texts (spot) | Pass (spot) | `step2-11-…`, `step2-13-…` |

### 8.3 Drag-and-drop decision rule (Decision 4) — result

| Check | Result |
|---|---|
| Drag starts (also with the page scrolled, window at minimum size); page does not scroll instead | Pass |
| Drop on another group / empty group / own section / outside | moves / moves / nothing / nothing |
| Highlight follows the pointer between neighbouring sections and always clears; 20 drags (released outside or on the own section) | Pass, no stuck highlight |
| Right-click on an entry | Menu opens, no drag starts |
| WinUI quirk | Caption "Kopieren" next to the pointer → hidden, Windows-only (`step2-02` before, `step2-03` after) |

Rule outcome: **pass with one small Windows-only fix**; no stop needed. Confirmed live by Michael with touchpad and mouse (M-2).

**Fixes from Step 2**

| Fix | Kind |
|---|---|
| Hide the WinUI drag caption/glyph "Kopieren" (`AppGroupsView.xaml.cs`, `#if WINDOWS`) | **Windows-only** (no Mac check needed in Step 5) |

### 8.4 Michael's hands-on part of the Step 2 check (pending)

Done by Michael on the Windows Developer Machine, 2026-10-31 (Debug build, own data folder, paired with the service 0.8.0).

| # | Check | Result |
|---|---|---|
| M-1 | Pair the Debug app with the service 0.8.0 on this PC; restart → opens on Berichte | Pass |
| M-2 | Touchpad drag and mouse drag on App Gruppen with live data (into a test group and back) | Pass with touchpad and mouse. Finding: not intuitive — the drag starts on the app name, the "≡" icon cannot be dragged and a click on it opens "Verschieben nach …". **Michael's decision: keep as is** (option A: no code, Mac and Windows identical); recorded by PRO in the story UI notes (`90a1379`). |
| M-3 | Tooltips on pencil and trash can | Pass |
| M-4 | Change the Windows accent colour while the app runs → frames follow (at the latest after re-activating the window) | Pass |
| M-5 | After a move, Berichte shows the new group lines without "Wird geladen …" | Pass |
| M-6 | Delete the test group; "Kopplung aufheben"; close the app | Done (test group deleted, pairing removed, app closed) |

### 8.5 Step 3 — Installers

`pwsh 02_Implementation/scripts/package-windows.ps1 -Target All`, 2026-10-31. Not installed on this PC (TI-12).

| File (`03_Delivery/windows/`) | File version | Size | SHA-256 |
|---|---|---|---|
| `EagleEye-Setup-0.9.0.exe` | 0.9.0 | 68.0 MB | `4F1EF797A96FB6F5E1DCF9D5C0E298C3AE16C0C7C630BC15A6ED6C9F47CB1652` |
| `EagleEye-ParentApp-Setup-0.9.0.exe` | 0.9.0 | 70.9 MB | `780CE6CB9E41D58E95EDEA5B37FA2D631E3E7EBC46E81EE66BCD0CC43A1D88F0` |

---

## 9. Step 4 — Pre-check on the Windows 10 PC (Michael)

**Preparation (H1)**: copy both files from `03_Delivery/windows/` on the Windows Developer Machine to the Windows 10 PC (USB stick or network share): `EagleEye-ParentApp-Setup-0.9.0.exe` and `EagleEye-Setup-0.9.0.exe`. Optional: compare the SHA-256 with §8.5 (`Get-FileHash <file>` in PowerShell).

| # | Do | Expected | Result |
|---|---|---|---|
| 1 | `winver` | Windows 10 22H2, build 19045.x | Pass (per Michael's summary) |
| 2 | Run `EagleEye-Setup-0.9.0.exe` | Inno Setup message that a newer Windows is needed; nothing installed (AC-5) | Pass (per Michael's summary) |
| 3 | Run `EagleEye-ParentApp-Setup-0.9.0.exe` as a normal user (no admin prompt) | Installs; start "EagleEye Parent App" from the Start menu | Pass (per Michael's summary) |
| 4 | Host dialog → `192.168.1.37` → pairing code from the service PC, device name "Windows 10 pre-check" | Connected, no certificate warning | Pass (per Michael's summary) |
| 5 | Open the four pages once; App Gruppen: create a group, drag one app into it and back (mouse, and touchpad if the PC has one), delete the group | Works as on Windows 11; note any odd look (fonts, emoji, cut-off text) | Pass (per Michael's summary) |
| 6 | Einstellungen → "Kopplung aufheben"; uninstall via *Einstellungen → Apps → Apps & Features* | Pairing removed; app uninstalled | Pass (per Michael's summary) |
| 7 | Report to DEV (H2): pass/fail per row, screenshots of anything odd | — | Reported 2026-10-31, see below |

**Result (H2, 2026-10-31)**: Michael: *"done. The app is installing and working on Windows 10."* He gave no per-row detail; rows 1 to 6 are marked passed on the basis of this summary. No screenshots, no finding, no fix needed, so the 0.9.0 installers stay as built (§8.5).

If the service PC's firewall blocks the Windows 10 PC, test TCP 5443 (`Test-NetConnection 192.168.1.37 -Port 5443`); a failed ping proves nothing.

---

## 10. H3 — Handover to the MacBook (Step 5)

Everything for Step 5 is pushed on `feature/US-009-windows-parent-app-parity`. There is **no shared code change** from Windows (§8.3), so Step 5 has no Mac probe check to do (plan Step 5.2).

**What Michael runs on the MacBook:**

1. In the repo (outside iCloud Drive, e.g. `~/EagleEye`):
   `git fetch && git switch feature/US-009-windows-parent-app-parity && git pull`
2. `pwsh 02_Implementation/scripts/env-check.ps1` → host "MacBook", branch `feature/US-009-windows-parent-app-parity`, no uncommitted changes, Xcode and the `maccatalyst` workload found.
3. Start a Claude Code DEV session in `02_Implementation/agents/dev/` with this prompt:
   > You are DEV on the MacBook. US-009 step c, plan Step 5 (`02_Implementation/docs/requirements/user-stories/US-009/implementation-plan.md`). Steps 1 to 4 are done on Windows (see `implementation-report.md` §8 to §10). Run `build.ps1` and `test.ps1` (Shared, ParentApp: 0 warnings, all green). No shared change came from Windows, so no Mac probe check. Build the `.dmg` with `package-macos.ps1`, check that it is 0.9.0, and put its SHA-256 in the report. Do not install it over Michael's 0.8.0 (AC-32 starting point). Write report §Mac and the final "How to test" for TES. Set the story to `Implemented`, then commit and push (H4).

**What Step 5 contains** (plan Step 5):

| # | Step 5 | Expected |
|---|---|---|
| 5.1 | `pwsh 02_Implementation/scripts/build.ps1` and `test.ps1` | Shared + ParentApp green, 0 warnings (F-2 also on the Mac) |
| 5.2 | Mac check of shared changes | None (only a Windows-only change) → nothing to check |
| 5.3 | `pwsh 02_Implementation/scripts/package-macos.ps1` | `03_Delivery/macos/EagleEye-ParentApp-0.9.0.dmg` (`CFBundleShortVersionString` 0.9.0), SHA-256 in the report; **not** installed (Mac app 0.8.0 stays as AC-32 starting point) |
| 5.4 | Report §Mac + final "How to test" for TES; story status → `Implemented` | Commit, push (**H4**); Michael then switches to Windows for TES's test plan |

Step 6 (Windows close-out) is needed only if Step 5 changes code.

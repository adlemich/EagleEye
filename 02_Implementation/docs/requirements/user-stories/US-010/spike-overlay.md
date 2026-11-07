# US-010 Spike — Overlay: Build, DEV Checks and Evaluation

**Instructions**: `02_Implementation/docs/requirements/user-stories/US-010/spike-overlay-instructions.md`
**Feasibility**: `02_Implementation/docs/requirements/user-stories/US-010/feasibility-overlay.md`
**Results template (Michael)**: `02_Implementation/docs/requirements/user-stories/US-010/spike-overlay-results.md`
**Branch**: `feature/US-010-time-overlay`
**Status**: Closed. §1 written by DEV (2026-11-01). §2 evaluation written by ARC (2026-11-01) after Michael's run on the kid's gaming PC. The spike code was removed from the repo after the evaluation (§2.6).

---

## 1. Build and DEV checks

**Author**: DEV · **Date**: 2026-11-01 · **Host**: Windows Developer Machine `ZOCK-O-MAT-V3` (Windows 11 Pro 26H2, build 26300.9818, AMD Radeon RX 9070 XT)

### 1.1 Build

| Item | Value |
|---|---|
| Code | `02_Implementation/spikes/US-010-overlay/` (not in `EagleEye.sln`, not in any script or installer) |
| **Tested and published commit** | **`24f6dea`**. The exe reports `0.9.0+24f6dea` (ProductVersion, and the first `START` log line) |
| Exe | `03_Delivery/spikes/US-010-overlay/OverlaySpike.exe` (git-ignored) plus `overlay-spike.json` (sample, all defaults) |
| Size | **51,703,364 bytes (49.3 MiB)**. Self-contained single file, compressed, not trimmed |
| SHA-256 | `791151FAD04F28192084FE3D8BA8267564382C6E0952C0655242ABDD59A8445D` |
| Publish | Exactly the §4 command. Restore took 157 ms. Nothing was downloaded: SDK 10.0.403 uses runtime 10.0.14, and the 10.0.14 NETCore and WindowsDesktop runtime packs were already in the local NuGet cache |
| `.gitignore` | `03_Delivery/spikes/` added in the "Build / Installer output" section |

### 1.2 What this host could and could not check (read first)

- **The agent session cannot simulate a person.** DEV ran in Michael's Admin session (session 2, not elevated). That session was **disconnected** (`query user`: "Getr."). Its only display is the virtual `WinDisc` 1440x1234 @ 96 DPI. There is no input desktop: `SendInput` returns 0 with error 5, `GetForegroundWindow` is 0, and screen capture (`CopyFromScreen`) fails. So nothing that needs a real keyboard, mouse, focus or a look at the screen could be checked here. DEV did not try to work around this with `runas` or a scheduled task.
- What DEV could exercise for real: the app's logic, by posting `WM_HOTKEY` to its message-only hotkey window and by posting the tray callback message, which opens the real tray menu, then driving the menu with posted keys; its windows and their styles; the log; CPU and memory; and the classification of its windows by **EagleEye's own `AppWindowRule`/`WindowEnumerator`**, loaded from the installed service's dll.
- **Differences from the instructions on this PC** [R, checked 2026-11-01]:
  - The installed service is **0.8.0** (`EagleEye.Service.dll` 0.8.0+8b62c07, US-008 step 6), not 0.9.0. `AppWindowRule.cs`, `WindowEnumerator.cs` and `WindowInfo.cs` are unchanged between `8b62c07` and `24f6dea` (`git log` is empty). DEV also classified with the freshly built 0.9.0 service dll, with the same result. The 0.9.0 installer is in `03_Delivery/windows/EagleEye-Setup-0.9.0.exe` if Michael wants 0.9.0 installed first.
  - The local accounts **`eagleeye-kid` and `ee-kid2` do not exist** on this PC. The standard accounts are **`Micha`** and **`Sebastian`**; the administrators are Admin and the disabled Administrator. Which of them EagleEye controls is not visible without elevation, so Michael picks (steps in §1.6).
  - The GPU is an RX 9070 XT, so this PC may itself be "the kid's gaming PC" (open question Q-A of the instructions §8).

### 1.3 Results D-1 to D-11

| # | Result | Evidence |
|---|---|---|
| D-1 | **Pass** | `build.ps1`: 4 projects built, 0 warnings, 0 errors, "Build SUCCEEDED". `test.ps1`: 305 + 1493 + 95 + 1109 = **3002 passed, 0 failed**. Neither output mentions OverlaySpike. The branch has no diff to `origin/main` (e65f03e = merge-base) under `src/`, `tests/`, `scripts/`, `installer/`, the sln and `Directory.Build.props`, so the result is the same as on `main` |
| D-2 | **Pass** | One exe. (a) Copy in `%USERPROFILE%\Downloads\OverlaySpike-devcheck\`: starts, log written next to the exe, settings source `(file)`. A second start logs `another OverlaySpike is already running in this session - exiting silently` and exits with code 0. (b) Folder where creating files is denied (`icacls /deny (WD,AD)`): starts, log in `%LOCALAPPDATA%\OverlaySpike\`, first lines `log folder: …\AppData\Local\OverlaySpike (fallback: exe folder not writable)`. Tray *Beenden* → `END` summary, **exit code 0**. Copies and logs were removed afterwards |
| D-3 | **Open – needs Michael** | No `ee-kid2` on this PC, and DEV cannot sign in to another account. Partial: the process runs non-elevated (`elevated=False`) from the default `asInvoker` manifest; the `START` lines are complete (version + commit, Windows build with UBR, user, session, settings with source, monitors with DPI/refresh/primary). Exception: the **display adapter list is empty** in this disconnected session (`EnumDisplayDevices` returns nothing). Check it in a real session (step M-2) |
| D-4 | **Open – needs Michael** | No input desktop here (see §1.2). Static: the overlay's exstyle is `0x080900A8` = `NOACTIVATE \| LAYERED \| CONTROLPARENT \| TOOLWINDOW \| TRANSPARENT \| TOPMOST`; `WM_NCHITTEST` → `HTTRANSPARENT`; `WM_MOUSEACTIVATE` → `MA_NOACTIVATE`. Owned by WinForms' hidden taskbar-owner window (`ShowInTaskbar=false`) |
| D-5 | **Partly Pass (logic); Open – needs Michael (keyboard, focus, look, sound)** | Posted `WM_HOTKEY` for all 8 ids → `KEY` lines `mark -> MARK 1`, `jump -> Sprung 5:10`, `reassert -> Re-assert: AUS/AN`, `beep -> Ton: AUS/AN`, `corner -> Ecke: Unten rechts / Unten links / Oben links / Oben rechts`, `overlay -> AUS`, `overlay -> Overlay: AN`, `jump -> Sprung 1:10`; then `LEVEL Warn at 5:00: colour=yellow, beep=yes` and `LEVEL Critical at 1:00: colour=red, beep=yes`. In the 16-min run, `LEVEL … Expired at 0:00` followed. Tray menu with posted keys: `MENU alpha -> Alpha: einheitlich` (window re-created), `MENU opacity -> Deckkraft: 70 %`, `MARK #1 (via MENU)`, *Beenden*. All 8 hotkeys registered (`all 8 hotkeys registered`). Not checked: real key presses, Notepad keeping the focus, colours, pulse, second line, audible beep, *Log-Ordner öffnen* |
| D-6 | **Open – needs Michael** | Needs a display, Chrome and YouTube (internet) |
| D-7 | **Open – needs Michael** | Needs a person to place and click Task Manager. Only the start state was logged (`COVER not covered`) |
| D-8 | **Open – needs Michael** (only if a second monitor is connected) | This session has one virtual display |
| D-9 | **Open – needs Michael** | Lock and sign-out need a person |
| D-10 | **Fail found and fixed. Static Pass after the fix; live run Open – needs Michael** | **Found:** with the tray menu open, the WinForms `ContextMenuStrip` popup (class `WindowsForms10.Window.20808…`, 340x286, exstyle `0x00010008` = TOPMOST \| CONTROLPARENT, **no** `WS_EX_TOOLWINDOW`, no owner, visible) was classified by the installed service's `AppWindowRule.Classify` as **`AgentApp { Kind = Window }`**. So in a controlled account, opening the tray menu would have made EagleEye record and block OverlaySpike. **Fix (in `24f6dea`):** the context menu and all three submenus (*Ecke*, *Deckkraft*, *Alpha*) use subclasses that add `WS_EX_TOOLWINDOW` (`ToolWindowMenus.cs`). **After the fix:** all OverlaySpike windows → `app=no`, with the service's classifier (0.8.0 installed and 0.9.0 built): the overlay (per-pixel and uniform), the menu (`0x00010088`), an open submenu (`0x00010088`), `SysShadow` (tool window), and the hidden helper windows (taskbar owner, NotifyIcon window, GDI+ hook, `.NET-BroadcastEventWindow`, IME). The hotkey window is message-only and is not enumerated. Service data, read-only: `C:\ProgramData\EagleEye\EagleEye.Service.db` (+ `-wal`) has **0** occurrences of "OverlaySpike" after all runs. The service log folder `C:\ProgramData\EagleEye\logs` needs elevation and was **not** read. No live run in a controlled account was possible (the session agent runs only in sessions of controlled accounts, ADR-011, so the Admin session proves nothing). Not checked: a balloon tip; it belongs to the shell, not to OverlaySpike, and only appears on an error |
| D-11 | **Pass, with a note** | Published exe, 16 min 19 s (12:47:28–13:03:48), CPU line every 30 s, through both pulse phases (10 Hz redraw at 5:00 and from 1:00 to 0:00). **CPU** 0.000–0.010 % per interval after the first one (0.073 % at start, includes menu use), max 0.073 %. **GDI 28 / USER 27**, constant from the 2nd line to the end. Handles 373–388, no trend. **Working set** 135.6 → 139.5 MB, private bytes 63.5 → 66.5 MB (about +0.25 MB/min, peak 140.9 MB). That is a slight drift, not "no growth". With GDI/USER/handles flat it looks like managed-heap growth from the per-second bitmap and font allocations, not a GDI leak. The SP-18 run over a game afternoon will show whether it levels off. About 136 MB at start is the decompressed single-file runtime (the uncompressed Debug build: 50 MB). Caveat: measured in a disconnected session; DWM composition on a real display may cost more (SP-09 measures that) |

### 1.4 Deviations from the instructions

1. **Tray menus are tool windows** (`ToolWindowContextMenu`, `ToolWindowDropDown`). This is the D-10 fix. §3.6 did not foresee it: a WinForms context menu popup is not a tool window.
2. **Process names come from `Process.GetProcesses()`** (system snapshot), not from `Process.GetProcessById`. [K] `GetProcessById` checks that the process exists, and on Windows .NET may do that by opening a limited handle (`PROCESS_QUERY_LIMITED_INFORMATION`) to it. The snapshot avoids any handle to a game process (the intent of §3.4). The snapshot is refreshed on every foreground change; covering windows use the cache.
3. **Mode guess**: one extra label `windowed(covers-monitor,caption)` for a window that covers the monitor but has `WS_CAPTION` (to spot browsers that keep their caption in fullscreen). Every `FG` line also has the raw `style`, `exstyle`, `zoomed` and notification state.
4. **Jumps set the level silently.** Hotkeys 5/1/0 and the menu jumps only set the colour of the level they land in. `LEVEL` lines, the pulse and the beep come only from crossing 5:00 / 1:00 / 0:00 while counting down. Example: hotkey 1 at 14:xx → yellow at once without a beep, then red with a beep at 1:00. This matches SP-02. The menu items read *Springe zu 5:00 (ab 5:10)* / *1:00 (ab 1:10)*, the same as the hotkeys.
5. **Sign-out**: the summary is in the same line as `session ending` (`SESSION session ending (Logoff); run time=…`), so the log ends with `session ending` as D-9 asks. There is no separate `END` line on sign-out. *Beenden* writes `END`.
6. **Second instance**: its single line goes into a small log file of its own (there is no shared file to append to).
7. **Uniform variant**: the rounded shape is a window region (edges not anti-aliased). The pulse modulates the window alpha.
8. **Pill width** is measured from a digit-normalised template, so it does not jitter every second.
9. Extra log content: `START` adds the exe path, the elevation flag and the processor count; `CPU` adds private bytes and the handle count; `COVER` names the coverer's hwnd, rectangle and flags (`topmost`, `layered`, `transparent`).

No other deviations. No unit tests (as instructed). Nothing under `src/`, `tests/`, scripts, installers or the sln was changed.

### 1.5 Evidence

The dev-check logs are `*.log` and git-ignored, so they are not committed; the quotes above come from them. They were deleted after the checks, together with the Downloads copy, the `%LOCALAPPDATA%\OverlaySpike\` folder and the helper scripts. One file stays outside the repo: the .NET single-file host extracts its native files once into `%TEMP%\.net\OverlaySpike\…` (normal for `IncludeNativeLibrariesForSelfExtract`, instructions §4).

### 1.6 Steps for Michael (open DEV checks), on `ZOCK-O-MAT-V3`, about 25 min

Do these **before or together with** the gaming-PC run. Record the results in this section (one line per check: Pass/Fail plus what you saw), or tell DEV. If this PC is itself the gaming PC (Q-A), M-1 and M-2 overlap with P-1 to SP-02 of `spike-overlay-results.md`.

Exe: `03_Delivery\spikes\US-010-overlay\OverlaySpike.exe` in the repo folder. Its log `overlay-spike-<date-time>.log` is written next to the exe. If that folder is not writable, the log goes to `%LOCALAPPDATA%\OverlaySpike\`. The first lines say which folder is used. Hotkeys are all **Strg+Alt+Umschalt + key**.

**M-1: Admin account, at the console or over RDP (D-4, D-5, D-7, D-6, D-8), about 12 min**

1. Sign in as **Admin**. Double-click `OverlaySpike.exe`. Expect a dark pill **"Spiele 15:00"** at the top right and a new tray icon (maybe under **^**).
2. **D-4**: Open *Editor* (Notepad) and maximise it. Click into the text exactly where the pill is, then type `test`. Expect `test` in Editor and no reaction from the pill. Look at the taskbar and press **Alt+Tab**: no OverlaySpike button, not in the Alt+Tab list.
3. **D-5**: With Editor in front, press **M**, then **5** and wait 15 s (5:00: yellow, pulse about 3 s, one beep). Press **1** and wait 75 s (1:00: red, slow pulse, beep; 0:00: beep, steady red `0:00`). Then press **0, B, B, C, C, C, C, O, O, R, R**. Each key shows a small second line for 2 s; C moves the pill through all 4 corners and back. **The cursor must keep blinking in Editor the whole time.** Then right-click the tray icon and try each item once: *Alpha ▸ einheitlich* and back to *pro Pixel*, *Deckkraft ▸ 30 %*, *70 %*, *50 %*, *Ecke ▸ …*, *Markierung ins Log*, *Log-Ordner öffnen* (an Explorer window opens; close it).
4. **D-7**: Open Task Manager (**Strg+Umschalt+Esc**) → *Optionen* → tick *Immer im Vordergrund*. Drag it so it covers the pill, then click into Task Manager. Expect the pill back above Task Manager within 1 s. Press **R** (`Re-assert: AUS`), click into Task Manager again: the pill stays covered. Press **R** again: the pill comes back on top within 1 s. Untick *Immer im Vordergrund* afterwards.
5. **D-6** (needs internet in Chrome): Open any web page in Chrome and press **F11**: the pill is visible over it. Open a YouTube video and press **f** (fullscreen): the pill is visible. Leave with Esc/F11.
6. **D-8** (only if a second monitor is connected, ideally with a different scaling): Drag Editor to the other monitor and click into it. The pill follows within 1 s with sharp text. Drag it back.
7. Tray icon → *Beenden*. Open the log and check that it has `KEY`, `MENU`, `LEVEL` lines, `COVER covered by Taskmgr …` / `REASSERT above Taskmgr …` (D-7), `FG … chrome … mode=fullscreen-borderless` for F11 and YouTube (D-6; if it says `windowed(covers-monitor,caption)`, note that), `MONITOR follow foreground …` (D-8), **no `FG` line naming OverlaySpike** except the ones marked `[own process: tray menu/notify window]`, and a final `END` line. **Do not sign out of Admin for D-9**; that is done in M-2.

**M-2: the controlled standard account (D-3, D-10, D-9), about 10 min**

`eagleeye-kid` / `ee-kid2` do not exist on this PC. Use the standard account (**Micha** or **Sebastian**) that EagleEye **controls**. Optional first: install 0.9.0 (`03_Delivery\windows\EagleEye-Setup-0.9.0.exe`) to match the instructions; the app classification is the same in 0.8.0.

1. As Admin, copy the folder `03_Delivery\spikes\US-010-overlay` to `C:\Users\Public\Downloads\OverlaySpike` (all interactive users may write there, so the log lands next to the exe). In the parent app, make sure that account has time left today and no break time in the next 15 min.
2. Sign in as the controlled account. Double-click `C:\Users\Public\Downloads\OverlaySpike\OverlaySpike.exe`. **D-3**: expect no UAC prompt, the pill "Spiele 15:00" top right, the tray icon.
3. **D-10**: Wait **5 minutes**. During that time, right-click the tray icon once, open the submenu *Ecke ▸*, close the menu with Esc (this exact case failed before the fix), and press **Strg+Alt+Umschalt+M**. Expect **no EagleEye kid dialog**, and the pill still counting after 5 min (not closed after 20 s).
4. **D-9**: Press **Win+L**, wait 10 s, unlock: the pill is back. Then sign out (Start → account → *Abmelden*) **with OverlaySpike still running**. Expect sign-out to proceed without a "this app prevents sign-out" screen.
5. Back as Admin, check:
   - the parent app → *Regeln* → that account: **no OverlaySpike row**; *Berichte* for today: **no OverlaySpike**;
   - service log, in an **elevated** PowerShell: `Select-String -Path C:\ProgramData\EagleEye\logs\* -Pattern OverlaySpike` → expect **no hits** (no "start blocked … OverlaySpike");
   - the log `C:\Users\Public\Downloads\OverlaySpike\overlay-spike-*.log`: `START` lines with that user name, `elevated=False`, and a non-empty `display adapters:` line (D-3); `SESSION SessionLock` / `SessionUnlock`; last line `SESSION session ending (Logoff); …` (D-9).
6. If EagleEye blocked OverlaySpike anyway: note which window it was if the service log says so, use the fallback of the instructions §6.3 step 2, and tell DEV.

Afterwards, delete `C:\Users\Public\Downloads\OverlaySpike` or keep it for the gaming-PC run. Then continue with `spike-overlay-results.md` (P-1 to SP-18) on the kid's gaming PC.

---

## 2. Evaluation

**Author**: ARC · **Date**: 2026-11-01 · **Host**: Windows Developer Machine `ZOCK-O-MAT-V3` (documentation only)
**Inputs**: §1 above (DEV), `02_Implementation/docs/requirements/user-stories/US-010/spike-overlay-results.md` (Michael's run), `02_Implementation/docs/requirements/user-stories/US-010/feasibility-overlay.md` §4 and §6.
**Machine of Michael's run**: the kid's gaming PC, **192.168.1.85** (Michael, 2026-11-01, answer to Q-A). It is also the pilot service PC that the Mac app paired with in US-008. It has **one monitor**. The Windows Developer Machine `ZOCK-O-MAT-V3` (192.168.1.37) is a different PC. The results template's header fields (date, build, GPU, account) were not filled in; the facts above come from Michael's answers.

### 2.1 Result per case

| Case | Result | Summary |
|---|---|---|
| P-1 to P-7 | Done | Spike started in the kid's account without an admin prompt; EagleEye did not block it. |
| SP-01 desktop, click-through | **Pass** | Michael moved the pill with the **tray icon (left mouse button)**, not with the hotkeys. Click-through and focus behaviour worked. |
| SP-02 levels, beep | **Pass** | Size, colours (white, yellow, red) and font good. **75 % opacity worked best.** |
| SP-03 Minecraft Java windowed | **Pass** | |
| SP-04 Minecraft Java F11 | **Pass** | |
| SP-05 Minecraft Java changed fullscreen resolution | **Pass** | Several resolutions tried, all visible. **Only the explicit "exclusive fullscreen" option hides the pill**, the same as Task Manager. Michael accepts this. |
| SP-06 Minecraft Bedrock windowed and F11 | **Pass** | |
| SP-07 Fortnite *Fenster-Vollbild* | **Pass** | No Easy Anti-Cheat message or kick. |
| SP-08 Fortnite *Vollbild* | **Pass** | No Easy Anti-Cheat message or kick. |
| SP-09 Fortnite 10 minutes, FPS | **Pass** | No FPS figures recorded; **no impact felt**. |
| SP-10 Chrome F11, YouTube fullscreen | **Pass** | |
| SP-11 Steam Big Picture | **Pass** | |
| SP-12 Xbox app | **Pass** | |
| SP-13 Epic Games Launcher | **Pass** | |
| SP-14 re-assert off vs on | **Pass** | No table filled in: with the re-assert on, the pill was visible everywhere. Whether some app covers it with the re-assert **off** was not recorded. |
| SP-15 lock/unlock | Skipped | |
| SP-16 two monitors | Skipped | The PC has one monitor. |
| SP-17 sign-out | Skipped | |
| SP-18 wrap-up (logs, CPU) | Skipped | No spike logs from the gaming PC; the CPU and memory figures are DEV's (§1.3 D-11). |

Michael's summary: "This worked nice. The spike is a success. For apps with exclusive full screen, the overlay is not on top, but that is no problem since most games run in full screen windows. CPU impact was not noticeable."

### 2.2 Feasibility §4 game matrix: confirmed or corrected

| App (feasibility §4) | Expected [K] | Spike result | Verdict |
|---|---|---|---|
| Minecraft Java windowed | Yes | Visible | Confirmed |
| Minecraft Java F11 fullscreen | Probably yes, not certain | Visible | **Confirmed** (the uncertainty is gone) |
| Minecraft Java fullscreen with a changed resolution | Likely no | **Visible** at several resolutions | **Corrected**: the overlay is visible. Minecraft's fullscreen-resolution change does not use a true exclusive mode on this PC. |
| Minecraft Java "exclusive fullscreen" option | (not listed) | **Not visible** (Task Manager neither) | **Added** as the one known true-exclusive case; documented limit (US-010 AC-23, requirements §10 note v1.10) |
| Minecraft Bedrock windowed and F11 | Yes | Visible | Confirmed |
| Minecraft Launcher, Epic Games Launcher, Steam (Big Picture), Xbox app | Yes | Visible (Epic, Steam Big Picture, Xbox app) | Confirmed. The Minecraft Launcher itself was used to start the game; not judged separately. |
| Fortnite Windowed Fullscreen | Yes | Visible, no EAC reaction | Confirmed |
| Fortnite Fullscreen | Most likely yes (fullscreen optimizations) | Visible, no EAC reaction, no FPS impact felt | **Confirmed** |
| Chrome F11, fullscreen video | Yes [S for video] | Visible | Confirmed |
| Other GDK/Game Pass games | Yes | Not tested (no such game started) | Unchanged expectation |
| Older D3D9/OpenGL games in true exclusive fullscreen | No | Not tested (none installed) | Unchanged expectation, same limit as Minecraft's "exclusive fullscreen" |

Anti-cheat (feasibility OV-3): no immediate reaction of Easy Anti-Cheat in about 10 minutes of Fortnite. A later server-side reaction cannot be ruled out by a spike (feasibility §3a); US-010 AC-24 repeats the check with the production build.

### 2.3 Decision (feasibility §6 decision rule)

Steps 1 to 6 passed, and the only failing mode is a true exclusive mode (Minecraft Java's "exclusive fullscreen"). By the decision rule this gives:

> **Option (a)**: a layered, click-through, top-most, non-activating tool window drawn by the tray client, with **per-pixel alpha**, the top-most **re-assert**, and the **warning sound** as the fallback that also works in exclusive fullscreen. **True exclusive fullscreen is a documented limit** (accepted by Michael, 2026-11-01).

Not needed (rule rows 3 to 5): Fortnite and EAC passed, so no "use Windowed Fullscreen" limit; the re-assert was enough, so **no `uiAccess` follow-up spike (a+)**; no frame drops felt, so no smaller overlay or uniform-alpha fallback. The **uniform-alpha variant** of the spike is dropped; production uses per-pixel alpha only (Michael's look at 75 %).

The decision is implemented by US-010 and recorded in ADR-022 (Proposed with the US-010 implementation plan).

### 2.4 Not tested, and where it is covered

| Point | Why not tested | Covered by |
|---|---|---|
| SP-15 lock/unlock (D-9 part 1) | Skipped | Plan Decision 7 (lock hides the pill, unlock waits for the next push); test run **AC-25**, AC-5 |
| SP-17 sign-out (D-9 part 2) | Skipped | Plan Decision 7 (`SessionEnding` never cancelled, nothing blocks sign-out); test run **AC-25** |
| Fast user switching | Not in the spike | Test run **AC-25** (two controlled accounts signed in at once) |
| SP-16 / D-8 multi-monitor and mixed DPI | One monitor on the gaming PC; one virtual display in DEV's session | Plan Decision 6 (follow the foreground window's monitor, PerMonitorV2); test run **AC-28** (second monitor optional, otherwise Skipped); 150 % scaling on one monitor is tested |
| SP-18 logs, CPU on the gaming PC | Skipped | DEV's D-11 figures (§1.3) plus the production measurement in plan Step 5; test run **AC-27** |
| D-3 non-elevated start in a controlled account, adapter list | No controlled account on `ZOCK-O-MAT-V3`; Michael's run started it in the kid's account without a prompt (P-4), which covers the start | Production tray is installed and started by the Run key, as since US-001; nothing new to check |
| D-4 click-through, no taskbar/Alt+Tab, focus | DEV had no input desktop | Michael's SP-01 (Pass); test run **AC-20** |
| D-5 real hotkeys, focus while keys are pressed, audible beep | DEV had no input desktop; Michael used the tray menu instead of hotkeys | Production has **no hotkeys** (the story uses the tray menu, AC-15); levels and beep passed in SP-02; test run **AC-7, AC-8, AC-11** |
| D-6 Chrome F11 and YouTube | Needed a display and internet | Michael's SP-10 (Pass); test run **AC-21** |
| D-7 Task Manager "always on top" vs re-assert | Needed a person | Michael's SP-14 (Pass with re-assert on); test run **AC-22** |
| D-10 live: tray menu in a controlled account | No controlled account in DEV's session | Production: the installed tray client is never recorded (ADR-011 §5, by path) **and** its menus and the overlay are tool windows (plan Decision 9, FR-TRAY-032); unit tests; test run **AC-26** |
| D-11 working-set drift (+0.25 MB/min in 16 min) | Not followed up over a game afternoon (SP-18 skipped) | Plan Decision 5 (fonts cached per DPI, no per-second allocations when nothing changed); DEV measures one hour in Step 5; test run **AC-27** |
| SP-14 "re-assert off" table | Not recorded | Not needed: production always re-asserts |

US-010's delivery note says the open DEV checks of the spike are not repeated as spike checks; the production behaviour is covered by AC-20, AC-22, AC-25 and AC-26 (story, "Delivery notes").

### 2.5 What the production design takes from the spike

The implementation plan `02_Implementation/docs/requirements/user-stories/US-010/implementation-plan.md` §"Carried over from the spike" lists each technique with its source file and line range in commit `24f6dea`, so DEV can read it with `git show 24f6dea:02_Implementation/spikes/US-010-overlay/<file>` after the folder is gone.

### 2.6 Last spike commit and removal

- **Last spike code commit**: **`24f6dea`** ("US-010: overlay spike app (throwaway) - OverlaySpike code, 03_Delivery/spikes/ ignored"). It is the tested build (`0.9.0+24f6dea`); no later commit changed `02_Implementation/spikes/US-010-overlay/`.
- **Removed** after this evaluation, in its own commit "US-010: remove overlay spike code (evaluated in spike-overlay.md)": `02_Implementation/spikes/US-010-overlay/` and the then-empty `02_Implementation/spikes/` folder, the `.gitignore` line `03_Delivery/spikes/` (no other spike used it), and the local, git-ignored `03_Delivery/spikes/` folder with the published exe.
- **Kept** as project records: `feasibility-overlay.md`, `spike-overlay-instructions.md`, `spike-overlay-results.md` and this file.

# US-010 Spike — Instructions for DEV: Portable Overlay Test App

**Author**: ARC
**Date**: 2026-11-01
**For**: DEV (build), Michael (test run)
**Branch**: `feature/US-010-time-overlay`
**Status**: Ready for DEV. Michael approved the spike on 2026-11-01. Still **no user story**, no implementation plan, no ADR.
**Basis**: `02_Implementation/docs/requirements/user-stories/US-010/feasibility-overlay.md` §3a, §5, §6 (option a: layered, top-most, click-through window)
**Results template**: `02_Implementation/docs/requirements/user-stories/US-010/spike-overlay-results.md` (Michael fills it in)
**Evaluation (later)**: `02_Implementation/docs/requirements/user-stories/US-010/spike-overlay.md` (DEV/ARC write it after the run)

Source tags as in the feasibility doc: **[R]** from the repo, **[K]** ARC's general knowledge, not checked on the internet, **[S]** to be found out by this spike.

---

## 0. Who does what, where

| Part | Who | Machine |
|---|---|---|
| Build, publish, smoke check (§3 to §5) | DEV | **Windows Developer Machine** (`ZOCK-O-MAT-V3`, Windows 11) |
| Copy to the gaming PC, test run (§6, results template) | Michael | **The kid's gaming PC** (Windows 11), in the **kid's standard account**. Which PC this is: see open question Q-A (§8) |
| Evaluation (§7) | DEV and ARC | either machine (documentation) |

Repo facts on the PCs [R]: the test station of all earlier stories is the Windows Developer Machine `ZOCK-O-MAT-V3`. It is also the service PC with EagleEye installed, the kid accounts `eagleeye-kid` (controlled) and `ee-kid2` (not controlled), and IP `192.168.1.37` in US-009 (`02_Implementation/docs/testing/US-005/test-plan.md`, `02_Implementation/docs/testing/US-009/test-report.md`). The repo does not name a separate gaming PC, and it does not mention `192.168.1.85`.

---

## 1. Goal and scope

**Goal.** A small **throwaway** Windows app, "OverlaySpike". It shows a fake remaining-time countdown in a translucent, top-most, click-through overlay. Michael uses it to check, over the kids' real games, the [S] statements of the feasibility doc: is the overlay visible, does it take the focus or input, does Easy Anti-Cheat react, does it cost frames, and how does it behave with several monitors and DPI scales.

**In scope.** One stand-alone WinForms app (.NET 10, like the tray client), published as **one portable exe**.

**Out of scope. It is not production code:**
- no user story, no implementation plan, no ADR;
- no change to `EagleEye.Service`, `EagleEye.TrayClient`, `EagleEye.Shared`, `EagleEye.ParentApp(.Core)` or their tests;
- not in `02_Implementation/EagleEye.sln`, not in `02_Implementation/scripts/build.ps1`, `test.ps1`, `package-windows.ps1`, not in any installer;
- no connection to the EagleEye service. The countdown is fake;
- no unit tests (§5);
- no reuse of tray code by reference. If DEV copies a snippet (for example the `LibraryImport` style of `ForegroundHelper`), it is a copy.

The production design (data push, settings, which dialogs stay) is decided later in the story and the plan, not here.

---

## 2. Where the code lives

**Choice: a separate folder `02_Implementation/spikes/US-010-overlay/` with its own `OverlaySpike.csproj`, committed on the US-010 branch.** Delete it before the branch is merged into `main`.

Why:
- Michael will want a fix or a variant after the first run (a different corner, uniform alpha). A real project can be rebuilt with one command. A patch-only spike, as in US-008, would have to be re-applied first; that fitted a throwaway page inside the MAUI app, but this is a stand-alone exe.
- Traceability: the code that was tested is in the branch history. The merge is `--no-ff` (`02_Implementation/docs/dev-process/dev-process.md` §4), so the commits stay reachable after the folder is deleted. `spike-overlay.md` records the commit hash of the tested build.
- It cannot break the build or the tests [R]:
  - `build.ps1` builds named projects under `02_Implementation/src/` only (`Get-ProjectPath`);
  - `test.ps1` runs named projects under `02_Implementation/tests/` only;
  - no script builds `EagleEye.sln`.
  
  So the folder must be **outside** `src/` and `tests/`, and it must **not** be added to the solution.
- It inherits `02_Implementation/Directory.Build.props` (version 0.9.0, `EnableWindowsTargeting`). That is harmless. On the MacBook the project is never built.

**Rules for the folder**
- Contents: `OverlaySpike.csproj`, a few `.cs` files, a sample `overlay-spike.json`, and a `README.md` of 5 lines ("throwaway, US-010, see `02_Implementation/docs/requirements/user-stories/US-010/spike-overlay-instructions.md`").
- DEV adds `03_Delivery/spikes/` to `.gitignore` in the first spike commit, in the "Build / Installer output" section. The published exe is never committed.
- Before the merge into `main` (§7): `git rm -r 02_Implementation/spikes/US-010-overlay`. Remove the then-empty `spikes/` folder, and the `.gitignore` line only if no other spike uses it.

**Project settings** (`OverlaySpike.csproj`):

| Property | Value | Why |
|---|---|---|
| `TargetFramework` | `net10.0-windows` | like the tray |
| `OutputType` | `WinExe` | no console window |
| `UseWindowsForms` | `true` | |
| `ApplicationHighDpiMode` | `PerMonitorV2` (plus `Application.SetHighDpiMode(HighDpiMode.PerMonitorV2)`) | multi-monitor DPI. The tray is `SystemAware` [R]; this is one thing the spike tests |
| `AllowUnsafeBlocks` | `true` | `LibraryImport` |
| `Nullable`, `ImplicitUsings` | `enable` | |
| `TreatWarningsAsErrors` | `false` | throwaway |
| `RuntimeIdentifier` | `win-x64` | |
| `AssemblyName` | `OverlaySpike` | the exe name |
| Application manifest | default (`asInvoker`) | never asks for admin rights |
| Packages | **none** | no NuGet download needed |

---

## 3. Behaviour of the test app

Keep it minimal. Everything here is test equipment, not a product design.

### 3.1 The overlay window

- One borderless form, created with extended styles `WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOPMOST | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW` (override `CreateParams`), `ShowWithoutActivation = true`, `ShowInTaskbar = false`. It is never activated, never in the taskbar, never in Alt+Tab, and it never receives mouse input.
- **Per-pixel alpha (default)**: draw the content into a 32-bpp premultiplied bitmap (GDI+), then `UpdateLayeredWindow`. Content: a rounded dark pill, background alpha from `opacity` (default 50 %), opaque anti-aliased white text, e.g. **`Spiele 14:59`** (label + `m:ss`), font Segoe UI semibold 16 pt at 100 % scaling, scaled with the monitor DPI.
- **Uniform-alpha variant** (`alpha: uniform`): `SetLayeredWindowAttributes` with `LWA_ALPHA` (= `Form.Opacity`); the whole pill, text included, at `opacity`. Only for comparison (feasibility §3a). Switching variants may re-create the window.
- **Position**: corner (`TopRight` default, `TopLeft`, `BottomRight`, `BottomLeft`), 24 logical px margin from the **monitor bounds**, not the work area (a fullscreen game hides the taskbar).

### 3.2 Fake countdown and levels

- Starts at `startMinutes` (default **15:00**) and counts down with a monotonic clock (`Stopwatch`), redraw once per second.
- **Levels** (fixed for the spike, as EagleEye's defaults 5/1 [R `WarningRules`]):
  - > 5:00 **white**;
  - at **5:00** **yellow**, a 3 s pulse (background alpha oscillates, about 4 Hz), beep if on;
  - at **1:00** **red**, pulse continues slowly (1 Hz) until 0:00, beep if on;
  - at **0:00** steady red `0:00`, beep if on; stays until a restart.
- Redraw rate: 1 Hz, 10 Hz only while pulsing.
- **Beep**: `SystemSounds.Exclamation.Play()` (the default output device). The beep is the fallback signal for exclusive fullscreen (feasibility §3d).
- The countdown keeps running while the session is locked (it is fake; no accounting).

### 3.3 Z-order upkeep and monitor following

- **Foreground hook**: `SetWinEventHook(EVENT_SYSTEM_FOREGROUND, …, WINEVENT_OUTOFCONTEXT)`. **Out-of-context only**: no DLL is loaded into other processes.
- On every foreground change and on every 1 s tick:
  1. **Follow the monitor**: `MonitorFromWindow(GetForegroundWindow(), MONITOR_DEFAULTTONEAREST)`. If that is another monitor, move the overlay there. If the monitor's DPI differs (`GetDpiForMonitor`), redraw at the new size. Ignore the desktop, the taskbar and the overlay's own tray menu as foreground; keep the last monitor then.
  2. **Covered check**: walk the z-order above the overlay (`GetWindow(hwnd, GW_HWNDPREV)` up to the top). Find the first visible, not cloaked window whose rectangle intersects the overlay. Log it **when it changes** ("covered by `<process>` `<class>`" / "not covered").
  3. **Re-assert** (if `reassert` is on): `SetWindowPos(HWND_TOPMOST, 0,0,0,0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_NOOWNERZORDER)`. Log it **only when** step 2 found a covering window (an effective re-assert), with the coverer's name; count all effective re-asserts.
- With `reassert` off, the overlay is made top-most once at start and never again (feasibility §6 step 7).

### 3.4 Foreground logging and window mode (heuristic)

On each foreground change, log one line:
- process name: from the system process list (`Process.GetProcessById(pid).ProcessName`; on Windows this comes from the system snapshot, not from a handle [K]). **Do not** open a handle to the foreground process: no `OpenProcess`, no `MainModule`, no `StartTime`. A handle to a game process is exactly what anti-cheat watches;
- window class (`GetClassName`), title truncated to 40 characters (`GetWindowText`);
- window rectangle (`DWMWA_EXTENDED_FRAME_BOUNDS`) and the monitor rectangle, monitor DPI;
- **mode guess**:
  - `exclusive?` when `SHQueryUserNotificationState` returns `QUNS_RUNNING_D3D_FULL_SCREEN`;
  - `fullscreen-borderless` when the window covers the whole monitor and has no `WS_CAPTION`;
  - `maximized` when `IsZoomed`;
  - else `windowed`.
  
  Also log the raw notification state. This is a heuristic; Michael's photo is the truth.

### 3.5 Control without stealing the focus

Three ways; precedence **command line > config file > defaults**.

1. **Config file** `overlay-spike.json` next to the exe (optional; the app never writes it). DEV ships a sample with all keys and their defaults:
   ```json
   { "startMinutes": 15, "label": "Spiele", "corner": "TopRight", "opacity": 50,
     "alpha": "perPixel", "reassert": true, "beep": true, "fontSizePt": 16, "cpuLogSeconds": 30 }
   ```
2. **Command line**: `--minutes 15 --label Spiele --corner TopRight|TopLeft|BottomRight|BottomLeft --opacity 30..90 --alpha perPixel|uniform --no-reassert --no-beep`. Michael can make desktop shortcuts with switches if he likes.
3. **Global hotkeys** (`RegisterHotKey` with `MOD_NOREPEAT`). **For use during a game**: they never activate a window. All are **Strg + Alt + Umschalt + key**: three modifiers, so no game or AltGr character uses them [K].

   | Hotkey | Action |
   |---|---|
   | Strg+Alt+Umschalt+**0** | restart the countdown at `startMinutes` |
   | Strg+Alt+Umschalt+**5** | jump to 5:10 (the 5:00 level comes after 10 s) |
   | Strg+Alt+Umschalt+**1** | jump to 1:10 |
   | Strg+Alt+Umschalt+**O** | overlay hide/show (for the FPS comparison) |
   | Strg+Alt+Umschalt+**R** | re-assert on/off |
   | Strg+Alt+Umschalt+**B** | beep on/off |
   | Strg+Alt+Umschalt+**C** | next corner |
   | Strg+Alt+Umschalt+**M** | write a **marker** into the log (`MARK #n`), to tie log lines to a test case |

   Feedback for each hotkey: the overlay shows a second, smaller line for 2 s (e.g. `Re-assert: AUS`, `MARK 7`). Nothing else appears. If a hotkey cannot be registered (taken by another program), log it and name it in the tray tooltip.
4. **Tray icon** (`NotifyIcon`, a plain generic icon, tooltip `OverlaySpike – Strg+Alt+Umschalt+…`). Context menu, in German, **for use between cases only**: opening it takes the focus, so not while a game runs. Items: *Countdown neu starten*, *Springe zu 5:00*, *Springe zu 1:00*, *Overlay ein/aus*, *Top-most erneuern an/aus*, *Ton an/aus*, *Ecke ▸* (4), *Deckkraft ▸ 30/50/70 %*, *Alpha ▸ pro Pixel / einheitlich*, *Markierung ins Log*, *Log-Ordner öffnen* (starts Explorer on the folder), *Beenden*. Show the hotkeys in the item texts. Every change is logged and kept in memory only.

### 3.6 Hard constraints (also keep EagleEye from blocking it, §6.3)

- **Never show a normal top-level window**: no `MessageBox`, no settings form, no About box, no console. Only the tool-window overlay, the tray icon with its menu, and tray balloon tips for errors. Reason: the EagleEye session agent records a program as an "app" only for a visible, unowned window that is not a tool window, or that has `WS_EX_APPWINDOW` (ADR-011 §4, rule 3 [R]). Only apps are blocked or closed. Unhandled exceptions: log them, show one balloon tip, exit.
- No network access of any kind. No registry writes (reading the Windows build number is fine). No admin rights. No `SetWindowsHookEx`, no DLL injection, no reading of other processes' memory, no screenshots. The only hook is the out-of-context WinEvent hook of §3.3.
- **Single instance**: a mutex `Local\EagleEyeOverlaySpike`. A second start logs one line and exits silently.
- **Lock/unlock** (`SystemEvents.SessionSwitch`): log it. On unlock, re-assert and redraw. The window stays alive.
- **Display changes** (`SystemEvents.DisplaySettingsChanged`, `WM_DPICHANGED`): log them, re-place the overlay, redraw.
- **Sign-out/shutdown**: log `session ending`, never block it (`SessionEnding`/`WM_QUERYENDSESSION` → allow), dispose the tray icon, exit.
- **Beenden**: log a summary (run time, effective re-asserts, foreground changes, max CPU %, peak working set), dispose the tray icon, exit with code 0.

### 3.7 Log file

- File `overlay-spike-<yyyyMMdd-HHmmss>.log`, UTF-8, one line per event: `HH:mm:ss.fff  CATEGORY  message`.
- Location: **next to the exe**. If that folder is not writable (read-only USB stick), use `%LOCALAPPDATA%\OverlaySpike\`. The first line names the folder used; the tray tooltip names it too.
- Flush after every line (Michael may pull the plug or sign out).
- Content:

  | Category | When | Content |
  |---|---|---|
  | `START` | start | app version, commit hash (passed at publish time, §4), Windows version and build (with UBR), user name (no SID), session ID, effective settings and their source, monitors (bounds, DPI, refresh rate, primary), display adapter name (`EnumDisplayDevices`) |
  | `FG` | foreground change | §3.4 line |
  | `COVER` | covered state changes | coverer process and class, or `not covered` |
  | `REASSERT` | effective re-assert | coverer, running count |
  | `MONITOR` | overlay moved to another monitor, DPI change, display change | old → new, DPI |
  | `LEVEL` | 5:00, 1:00, 0:00 reached | colour, beep yes/no |
  | `KEY` / `MENU` | hotkey or menu action | action, new value |
  | `MARK` | marker | number |
  | `SESSION` | lock, unlock, session ending | |
  | `CPU` | every `cpuLogSeconds` (30 s) | own CPU % over the interval (Δ`TotalProcessorTime` / Δwall / cores), working set MB, GDI and USER object counts (`GetGuiResources`) |
  | `ERROR` | any exception | message and stack trace |
  | `END` | exit | summary of §3.6 |

---

## 4. Build and publish (DEV, Windows Developer Machine)

Run in PowerShell 7.6 from the repo root, on the branch `feature/US-010-time-overlay`, after `git pull`:

```powershell
$commit = git rev-parse --short HEAD
dotnet publish 02_Implementation/spikes/US-010-overlay/OverlaySpike.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true -p:DebugType=embedded `
  -p:SourceRevisionId=$commit `
  -o 03_Delivery/spikes/US-010-overlay
Copy-Item 02_Implementation/spikes/US-010-overlay/overlay-spike.json 03_Delivery/spikes/US-010-overlay/
```

- **Output**: `03_Delivery/spikes/US-010-overlay/OverlaySpike.exe` plus the optional `overlay-spike.json`. Git-ignored (§2).
- Self-contained: the gaming PC needs no .NET runtime. Expected size about 50 to 150 MB [K]. **No trimming**: WinForms does not support it.
- `IncludeNativeLibrariesForSelfExtract`: the runtime's native files are unpacked at first start into the user's `%TEMP%\.net\…`. That needs no admin rights [K].
- **Restore**: no packages. The win-x64 runtime packs (including Windows Desktop) are already cached on this PC, because `package-windows.ps1` publishes the tray client self-contained for win-x64 [R]. If `dotnet publish` still wants to download a pack, stop and ask Michael (no internet without his go).
- **Version shown in the log**: the commit hash via `SourceRevisionId` (it ends up in `InformationalVersion`). So the log proves which build was tested. Commit before publishing for the run.
- Unsigned exe: SmartScreen will warn on the gaming PC (§6.2).

---

## 5. DEV checks before the handover

**No unit tests**: this is throwaway test equipment. Nothing goes into `02_Implementation/tests/`. The checks are manual, on the **Windows Developer Machine**:

| # | Check | Pass |
|---|---|---|
| D-1 | `pwsh 02_Implementation/scripts/build.ps1` and `test.ps1` on the branch | Same result as on `main`; the spike is not built or tested by them |
| D-2 | Publish as in §4 | One exe; it starts from a copy in `%USERPROFILE%\Downloads` and from a folder without write access (log falls back to `%LOCALAPPDATA%\OverlaySpike\`) |
| D-3 | Start in a **standard (non-admin) account**, e.g. `ee-kid2` (not controlled) | No UAC prompt; overlay `Spiele 15:00` top right; tray icon; log `START` complete |
| D-4 | Notepad in front: click and type "through" the overlay area | Clicks reach Notepad; typing stays in Notepad; the overlay is not in the taskbar or in Alt+Tab; the `FG` lines never name the overlay |
| D-5 | All hotkeys and menu items; jump to 5:00 and 1:00 | Colours, pulse, beep, second line; Notepad keeps the focus during hotkeys; `KEY`/`MENU`/`LEVEL` lines |
| D-6 | Chrome F11, then a YouTube video in fullscreen | Overlay visible over both; `FG` mode `fullscreen-borderless` |
| D-7 | Re-assert check: Task Manager → *Optionen* → *Immer im Vordergrund*, place it over the overlay corner; then hotkey R off/on | With re-assert on, the overlay comes back above it within 1 s with a `REASSERT` line; with re-assert off it stays covered, with a `COVER` line |
| D-8 | Two monitors, if available, with different scaling (e.g. 100 % and 150 %): move Notepad between them | The overlay follows within 1 s, sharp text at both scales, `MONITOR` lines |
| D-9 | Win+L, unlock; then sign out with the overlay running | Overlay back after unlock; sign-out not delayed; `SESSION` lines; log ends with `session ending` |
| D-10 | **EagleEye does not record it** (§6.3): run it for 5 min in the **controlled** account `eagleeye-kid` (service 0.9.0 installed) | It keeps running (not closed after 20 s). It does not appear on *Regeln* or *Berichte* in the parent app for `eagleeye-kid`. The service log has no "start blocked … OverlaySpike" line |
| D-11 | `CPU` lines over 10 min | Own CPU < 1 %, working set stable (no growth), GDI/USER counts stable |

**DEV hand-over**: a short section "§1 Build and DEV checks" at the top of `02_Implementation/docs/requirements/user-stories/US-010/spike-overlay.md`. It holds: the commit hash, the exe size, the D-1 to D-11 results, any deviation from this document, and especially the D-10 result. Then commit and push on the branch, and tell Michael where the exe is.

If D-10 fails (the app is recorded as an app), find out which window caused it (likely the tray context menu or a balloon tip) and fix it. If it cannot be fixed in the time box, note it; Michael then uses the fallback in §6.3.

---

## 6. Michael's test run on the kid's gaming PC

The step-by-step checklist with tick boxes is in `02_Implementation/docs/requirements/user-stories/US-010/spike-overlay-results.md` (the test-run format of `02_Implementation/docs/testing/README.md` §4). This section only explains the setup.

### 6.1 Copy

Copy `03_Delivery\spikes\US-010-overlay\` (the exe, and `overlay-spike.json` if wanted) from the Windows Developer Machine to a USB stick. On the gaming PC, copy the folder into the kid's `Downloads` (or run it from the stick). Running it from the stick also works; the log then goes to `%LOCALAPPDATA%\OverlaySpike\` if the stick is read-only.

### 6.2 First start: SmartScreen

The exe is unsigned. Windows Defender SmartScreen may show "Der Computer wurde durch Windows geschützt". Click **Weitere Informationen** → **Trotzdem ausführen**. This is usually needed only when the file came from a download, not from a USB stick [K]. If Windows asks for an administrator password for SmartScreen, cancel. Then, on the file: *Eigenschaften* → *Allgemein* → tick **Zulassen** (if shown) → *OK*, and start again.

### 6.3 EagleEye must not block it

What 0.9.0 does with an unknown program in a **controlled** kid account [R]:
- Deny by default: an app without a rule is blocked (ADR-015 §6.3). The service blocks its start, shows the budget text in the kid dialog, closes it (`WM_CLOSE`, terminated after 20 s), and creates an app record that then appears on *Regeln* (ADR-015 §6.4, ADR-013).
- But **only apps** are recorded and blocked. An app is a program with a visible, unowned window that is not a tool window (ADR-011 §4). OverlaySpike has only a tool window and a tray icon (§3.6), so it **should never be recorded**. DEV confirms this in D-10.
- Renaming does **not** help: EagleEye identifies apps by program path, and a new path is just a new, blocked record (ADR-015 §6, arc42 R-16).

**What Michael does:**
1. Nothing special. Start OverlaySpike in the kid account. If it keeps running for 1 minute without an EagleEye message, it is not blocked.
2. **Fallback**, only if EagleEye blocks it anyway (kid dialog with the budget text, overlay closes): in the parent app → *Regeln* → the kid's account → *Applikations-Regeln* → the new row **OverlaySpike** → budget **3:00**, today's weekday ticked. Wait 5 s and start it again. Keep the exe in the **same folder** for the whole run (another folder = another record). After the run, set that row back to 0:00 / no day.
3. **The games themselves must not run out of time during the test.** Otherwise EagleEye's own warning dialogs and closes interfere. Before the run, give the games' group (e.g. *Spiele*) or the single games at least **3 h for today**, with today ticked. Make sure no break time (*Ruhezeit*) begins in the next 3 h. Set it back afterwards. (Alternative: a non-controlled account, but the game launchers are usually signed in only in the kid's account.)

### 6.4 Cases (overview)

| Case | What | Main evidence |
|---|---|---|
| SP-01 | Start, desktop, click-through, hotkeys | log |
| SP-02 | Levels 5:00 / 1:00 / 0:00, beep | photo |
| SP-03 to SP-05 | Minecraft Java: windowed, F11, fullscreen with a changed resolution | photos, log |
| SP-06 | Minecraft Bedrock: windowed, F11 | photos |
| SP-07, SP-08 | Fortnite (EAC): *Fenster-Vollbild*, *Vollbild* | photos, log |
| SP-09 | Fortnite: 10 min of play, FPS with and without the overlay | FPS numbers |
| SP-10 | Chrome F11, YouTube fullscreen | photos |
| SP-11 to SP-13 | Steam Big Picture, Xbox app, Epic Games Launcher | photos |
| SP-14 | Re-assert off vs on, over all apps | log `COVER`/`REASSERT` |
| SP-15 | Lock/unlock | log |
| SP-16 | Two monitors / DPI (if the PC has them) | photos, log |
| SP-17 | Sign-out | log |
| SP-18 | Wrap-up: CPU, EAC, log copy | log |

**Evidence rules**: take a **phone photo** for every visibility check. A fullscreen game screenshot may not show the overlay [K]; also take one screenshot (Win+Umschalt+S or Druck) per game case, so we learn what TES can use later. Name the files `sp-NN-<short>.jpg`/`.png`. Before you hit a hotkey for a case, press **Strg+Alt+Umschalt+M** and write the marker number into the checklist. Make sure no photo shows the kid's chat, friends list or account names, because the evidence goes into the GitHub repo.

---

## 7. Afterwards

1. Michael commits the filled-in `spike-overlay-results.md` and the evidence (`US-010/evidence/spike-*`, the log `overlay-spike-*.log`) on the branch, or hands them to DEV.
2. **DEV and ARC write the evaluation** in `02_Implementation/docs/requirements/user-stories/US-010/spike-overlay.md`: the result per case, the game matrix of feasibility §4 confirmed or corrected, the FPS figures, and the decision by the rules of feasibility §6:
   - steps 1–6 pass → option (a) as recommended;
   - only true exclusive modes fail → (a) with the documented limit plus the audio cue;
   - Fortnite *Vollbild* fails or EAC reacts → (a) with "use *Fenster-Vollbild*" as a documented limit; Michael decides;
   - covered even with re-assert → a short follow-up spike for the `uiAccess` variant (a+);
   - noticeable frame drops → a smaller overlay, "last N minutes" only, or uniform alpha.
3. Michael accepts the evaluation.
4. **Code**: DEV removes `02_Implementation/spikes/US-010-overlay/` in a commit on the branch ("US-010: remove overlay spike code (evaluated in spike-overlay.md)"). This happens before the branch is merged, at the latest together with the story's implementation. `spike-overlay.md` names the last spike commit. The exe in `03_Delivery/spikes/` can simply be deleted.
5. **PRO writes US-010** with the feasibility doc, the evaluation, and Michael's answers to the product questions (feasibility §8).

---

## 8. Open questions for Michael

| # | Question | ARC assumption until answered |
|---|---|---|
| Q-A | **Which PC is "the kid's gaming PC"?** The repo knows only the Windows Developer Machine `ZOCK-O-MAT-V3` as the service PC (with `eagleeye-kid`, IP `192.168.1.37` in US-009). Is the gaming PC the same machine or another one (`192.168.1.85`?), and which EagleEye version runs there, if any? | Another Windows 11 PC with EagleEye 0.9.0 and a controlled kid account. If it is `ZOCK-O-MAT-V3` itself, §6.1 is trivial and D-10 already covers §6.3. |
| Q-B | Which games are installed in which account there, and in which display mode does the kid normally play Fortnite and Minecraft? | As in the feasibility §4 matrix. |
| Q-C | Does the gaming PC have a second monitor, and which scaling? | Unknown; SP-16 is optional. |
| Q-D | May Michael lift the games' budget to 3 h for one test afternoon (§6.3 step 3)? | Yes. |

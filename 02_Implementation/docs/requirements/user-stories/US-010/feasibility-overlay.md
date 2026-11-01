# US-010 Feasibility Check — Half-Transparent Remaining-Time Overlay

**Author**: ARC
**Date**: 2026-11-01
**Machine**: MacBook (documentation only; no code changed, nothing built or run)
**Branch**: `feature/US-010-time-overlay`
**Status**: Draft for Michael. **No user story exists yet.** This is not an implementation plan and not an ADR; it gives PRO and Michael the facts to write the story.
**Trigger**: Michael, 2026-11-01: replace the annoying 5- and 1-minute "OK" dialogs with a half-transparent, always-on-top overlay that shows the remaining time over the running game, as FPS overlays do.

**Sources.** Every statement carries a source tag:

- **[R]**: from this repo (file named);
- **[K]**: ARC's general knowledge of Windows, DWM, DirectX and anti-cheat. **Not verified on the internet** (no internet access granted). Treat it as a well-founded expectation, not as a fact;
- **[S]**: open; must be confirmed by the Windows spike (§6).

---

## 1. Summary

| Question | Answer |
|---|---|
| Feasible? | **Yes, with limits.** A top-most, click-through, translucent window from the tray client covers windowed and borderless games, launchers, browsers and video. It will most likely **also** cover Fortnite's and other DirectX 11/12 games' "Fullscreen" mode on Windows 11, because Windows runs these through fullscreen optimizations [K, S]. It **cannot** be guaranteed over true exclusive fullscreen (legacy D3D9, OpenGL/Vulkan with a display-mode change, games with fullscreen optimizations switched off) [K]. |
| Recommended option | **(a) Layered top-most click-through window in `EagleEye.TrayClient`** (WinForms, no new framework), plus an **audio cue** at the warning levels as a fallback that works in every mode. Keep the OK dialog for the **final** "blocked/closed" message. |
| Rejected | (b) Xbox Game Bar widget: the kid controls it (pin, unpin, Game Bar off), needs MSIX and loopback exemption. (c) Graphics-API hooking/injection: anti-cheat ban risk, already out of scope (requirements §10). Toast notifications: Windows suppresses them during games. |
| Requirements basis | The idea is already in the requirements: FR-TRAY-030 "optionally display a topmost overlay window showing remaining time", NFR-P-011 "no noticeable frame drops in windowed or borderless-windowed applications", and §10 Out of Scope "True DirectX/game overlay injection is deferred" [R, `02_Implementation/docs/requirements/general-product-requirements.md`]. Option (a) is exactly what these lines describe. |
| Rough size | **M** for option (a) with "always visible or last N minutes", one setting per kid, the smallest remaining time, and the warnings as colour changes. **L** if PRO adds per-app lists, kid-movable position, or the foreground app's own subject. Plus a spike of about **half a day to one day** on Windows. |
| Main risks | (1) Exclusive-fullscreen games hide the overlay. (2) A game that is itself top-most covers it (z-order fight). (3) Anti-cheat **heuristics** for "external ESP overlays" (a translucent top-most click-through window is also what external cheats use). (4) Small GPU/latency cost: the overlay stops a fullscreen game's direct scan-out. (5) The service today computes ends only 60 minutes ahead. Details in §7. |

---

## 2. What exists today [R]

- **Kid messages**: one request `ITrayClientCallback.ShowKidMessage(KidMessageDto)` with a result, sent by the service to the one verified tray connection of the session (ADR-014, amended by US-006). Reasons: early/final warning (app or group, break), grace periods, not allowed today, budget used up, break time (`02_Implementation/src/EagleEye.Shared/Models/KidMessageReason.cs`). The DTO carries the app or group name and the minutes left at the moment of the warning, nothing continuous.
- **Dialog**: `KidMessageDialog` is a normal top-most form with OK/Enter, not closable with Esc/Alt+F4, focus taken best effort via `ForegroundHelper` (synthetic Alt + `SetForegroundWindow`, else taskbar flash) (`02_Implementation/src/EagleEye.TrayClient/UI/`). This focus grab is what makes the dialog annoying in a game: it takes the keyboard from the game.
- **ADR-014 §3 already notes**: "Exclusive full-screen DirectX games are drawn above all windows by the display driver; the dialog appears when such a game is closed." FR-TRAY-022 says "exclusive full-screen games may cover the dialog". So the overlay has **the same visibility limit as today's dialog**, without the focus grab.
- **Warning levels**: global, early 1…59 min (default 5), final 1…58 min (default 1), early > final, with optional parent texts (`WarningSettingsDto`, `WarningRules`, Einstellungen page). Each level is shown once per subject and day (app ends) or per session and break occurrence (ADR-015 §4).
- **Engine**: `TimeLimitEngine` evaluates every 5 s tick per session and per **limit subject** (the group if the app is a group member, else the app; ADR-015 amended by US-008/ADR-021). It computes each subject's end as the earliest of budget end, not allowed today, midnight, break start. **Only ends within a 60-minute horizon are computed** (`AppEndCalculator.Horizon`). Parallel apps each count fully; the service does not know which app is in the foreground (ADR-012 §2).
- **Tray push channel**: besides `ShowKidMessage` and `OnShowPairingCode`, the tray gets **no remaining-time data** today. `FR-TRAY-011` (summary of remaining budgets on tray click) is not implemented.
- **Tray client**: .NET 10 WinForms, `HighDpiMode.SystemAware` in `Program.cs` (ADR-014 §3 asks for per-monitor DPI awareness; the code is system-aware). No log file of its own. Unsigned executable, installed per machine under `{autopf}\EagleEye\TrayClient` by Inno Setup, started by the Run key (`02_Implementation/installer/windows/setup.iss`).
- **arc42 §5.4** already lists "optional topmost overlay" as a planned part of the tray UI (`02_Implementation/docs/architecture/arc42/system-architecture.md`).

---

## 3. Overlay techniques on Windows 11

### 3a. Layered, top-most, click-through window from the tray process — **recommended**

**How it works [K]**

- A borderless form with extended styles `WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOPMOST | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW` (set in `CreateParams`), `ShowWithoutActivation = true`, shown with `SW_SHOWNOACTIVATE`.
  - `WS_EX_LAYERED` + `WS_EX_TRANSPARENT`: the window gets no mouse input; clicks go to the game below.
  - `WS_EX_NOACTIVATE`: showing it or updating it never takes the focus. The game keeps keyboard and mouse. This alone removes the main annoyance of today's dialog.
  - `WS_EX_TOOLWINDOW`: no taskbar button, not in Alt+Tab.
- Two ways to get translucency:
  1. **Uniform alpha** (`Form.Opacity` = `SetLayeredWindowAttributes`): the whole window, text included, at e.g. 70 %. Normal WinForms painting works. Simplest.
  2. **Per-pixel alpha** (`UpdateLayeredWindow` with a 32-bit premultiplied bitmap): a translucent dark pill with fully opaque, anti-aliased text. The tray draws the content itself into a bitmap (GDI+/`System.Drawing`) once per update; WinForms controls are not used. About 100 to 150 lines of interop, comparable to `ForegroundHelper`. Looks like the FPS overlays Michael means.
- **Z-order upkeep**: a top-most window stays top-most, but **another** top-most window shown later lies above it. Several borderless games, Chrome in some fullscreen modes and launchers set themselves top-most. The overlay therefore re-asserts `SetWindowPos(HWND_TOPMOST, …, SWP_NOACTIVATE | SWP_NOMOVE | SWP_NOSIZE)` when the foreground window changes (`SetWinEventHook(EVENT_SYSTEM_FOREGROUND)`, **out-of-context**, so no DLL is loaded into other processes) and on its 1 s redraw.
- **Monitor**: the overlay follows the monitor of the foreground window (`MonitorFromWindow(GetForegroundWindow())`) and uses the monitor bounds, not the work area (in fullscreen there is no taskbar).

**Over which game modes [K, S]**

| Mode | Expectation | Why |
|---|---|---|
| Windowed | **Visible** | DWM composes all windows; top-most wins. |
| Borderless windowed ("Windowed Fullscreen", most modern games, Bedrock, GDK/Xbox games, Chrome F11) | **Visible** | Same. The game loses "independent flip" while the overlay overlaps it and is composed by DWM; on GPUs with multi-plane overlays (MPO) it may keep a hardware plane. Cost: small, see §5. |
| "Exclusive" fullscreen of a DX11/DX12 flip-model game **with fullscreen optimizations on** (Windows 10/11 default) | **Most likely visible** [S] | Windows runs such games as a borderless flip-model window that only looks exclusive. Top-most windows from other processes (Game Bar, Discord's non-injected pop-ups, volume OSD) appear over them. |
| True exclusive fullscreen: D3D9 exclusive, OpenGL/Vulkan with a display-mode change, fullscreen optimizations disabled for the exe, some anti-cheat games forcing it | **Not visible**, or the game flickers/minimises when another window comes to the top [K, S] | The driver scans out the game's buffer directly; DWM windows are not shown. |
| Lock screen, UAC prompt (secure desktop), sign-in | Not visible (wanted) | Other desktop. |

**Focus and input**: no focus steal, no input capture (`WS_EX_NOACTIVATE`, `WS_EX_TRANSPARENT`) [K]. The kid can never click it, so it can also never be closed or moved by mistake; a "hide" or "move" by the kid needs a separate way (tray menu, §4).

**Anti-cheat [K]**: no injection, no handle to the game process, no reading of its memory. Easy Anti-Cheat and BattlEye protect the game process; an ordinary window of another process is outside their protection. The residual risk is **heuristic**: external ESP cheats also use a translucent top-most click-through window over the game. Widely used legitimate tools do the same (crosshair and FPS overlays, OBS previews, Windows' own volume and Game Bar overlays), and ARC knows of no ban policy against such windows in Fortnite. **This cannot be confirmed by a spike** (a ban, if any, would come later and server-side). Mitigation: the overlay is small, lies at a screen corner, never covers the full game window, and is drawn by a normally named, installed process.

**Effort in this codebase**: tray only for the window (`OverlayWindow` + interop helper + presenter), plus a data push (§5). No new framework, no new package, WinForms stays.

**Packaging/installer**: no change. Same exe, same Run key [R, `setup.iss`].

**Multi-monitor and DPI**: the tray is `SystemAware` today [R]. On a second monitor with another scale the overlay would be bitmap-stretched (blurry). The overlay should be per-monitor DPI aware (`PerMonitorV2`), either for the whole process (then the existing dialogs must be re-checked at 150 %) or only for the overlay window via `SetThreadDpiAwarenessContext` around its creation [K]. A spike case covers this.

**Variant (a+): `uiAccess=true`** [K]. An executable with `uiAccess="true"` in its manifest gets a z-order band above normal top-most windows (like the on-screen keyboard and Magnifier), which ends the z-order fight. Conditions: the exe must be **Authenticode-signed** with a certificate the machine trusts and installed in a secure folder (`Program Files` fulfils this, [R] `{autopf}`). EagleEye's Windows binaries are unsigned today; a self-signed code-signing certificate put into the machine's trusted roots by the installer would satisfy it. Not recommended for the first story; keep it as the fallback if the spike shows games covering the overlay (spike step 7 says when). It does **not** help against true exclusive fullscreen.

### 3b. Xbox Game Bar widget — **not recommended**

**How it works [K]**: a UWP app with the Xbox Game Bar Widget SDK declares a widget extension. The kid (or parent) opens Game Bar (Win+G), opens the widget and **pins** it; pinned widgets stay visible over games, with user-set transparency and "click-through".

- **Visibility**: as for (a) — windowed, borderless and fullscreen-optimized games; not true exclusive fullscreen.
- **Focus/input**: good when pinned; Game Bar itself takes focus while open.
- **Anti-cheat**: Game Bar is a Microsoft overlay; low risk.
- **Showstoppers**: the **kid controls it**: unpinning, closing, or switching Game Bar off in *Settings → Gaming* (or uninstalling it) removes the display. The widget is a UWP app: it needs **MSIX packaging** with a trusted signing certificate (sideloading; ADR-009 already rejected MSIX for the parent app for this reason [R]), a second app next to the tray, and an AppContainer **loopback exemption** to reach the local SignalR hub (or an app service bridge to a full-trust process). Two UI stacks for one display.
- **Effort**: L, plus installer work. **Multi-monitor/DPI**: handled by Game Bar.

### 3c. Graphics-API hooking / DLL injection — **hard no**

**How it works [K]**: RTSS, Steam, Discord, NVIDIA and Xbox's in-game overlays load a DLL into the game process and hook `IDXGISwapChain::Present` (or the D3D9/OpenGL/Vulkan equivalents) to draw into the game's own frame. Only this works over true exclusive fullscreen.

- **Anti-cheat**: EAC and BattlEye block or flag unknown injected modules; only allow-listed, signed vendors are tolerated. Risk of the kid's game account being banned. Also a SYSTEM-installed product injecting into kid processes contradicts ADR-011 §8 (no hooks, no windows of privileged code on the kid's desktop) [R].
- **Effort**: native C++ per graphics API, per game compatibility. XL.
- Already **out of scope** in the requirements §10 [R].

### 3d. Other options

| Option | Assessment |
|---|---|
| **DirectComposition / `Windows.UI.Composition` in a desktop window** [K] | Only a different way to *draw* (GPU-composed translucency). The window is still a top-level window, with exactly the z-order limits of (a). Not needed; GDI + `UpdateLayeredWindow` is enough for a small text pill updated once per second. |
| **Toast notifications** [K] | Lighter, no window code, but Windows 11's *Do not disturb* automatic rules ("when playing a game", "when using an app in full-screen mode") suppress them by default exactly when the kid plays; they also need an AppUserModelID/Start-menu shortcut. Not a replacement. |
| **Audio cue** at the early/final level [K] | Works in **every** mode, also true exclusive fullscreen and with the overlay hidden. Recommended as a complement (PRO question Q-9). |
| **Tray icon with the minutes** (FR-TRAY-011) [R, K] | A number drawn into the `NotifyIcon`. Not visible in fullscreen. Cheap complement when the kid is on the desktop. |
| **`WTSSendMessage` from the service** [R, ADR-014] | A message box again, the opposite of the wish. |

---

## 4. Game matrix (pilot apps)

Coverage by **option (a)**. All expectations are [K] unless marked; every row needs the spike to confirm [S].

| App | Graphics / usual mode | Overlay (a) expected | Remark |
|---|---|---|---|
| **Minecraft Java** (launcher-bundled Java, `javaw.exe`, OpenGL/LWJGL) | Windowed by default; F11 = fullscreen | Windowed: **yes**. F11 fullscreen: **probably yes** on Windows 11 with current drivers, **not certain** [S]. Fullscreen with a changed "Fullscreen Resolution" (display-mode change): **likely no** | The spike case that decides most for the kids. |
| **Minecraft Bedrock** (GDK, DX12) | Borderless; F11 toggles fullscreen = borderless | **Yes** | |
| **Minecraft Launcher**, **Epic Games Launcher**, **Steam** (client and Big Picture), **Xbox app** | Normal desktop windows | **Yes** | Some launchers set themselves top-most during updates: the re-assert handles it. |
| **Fortnite** (UE, DX11/DX12, Easy Anti-Cheat) | Windowed / Windowed Fullscreen / Fullscreen | Windowed and Windowed Fullscreen: **yes**. Fullscreen: **most likely yes** via fullscreen optimizations [S] | Anti-cheat: no injection; heuristic risk only (§3a). The spike checks that the game neither minimises nor stutters, and that EAC shows no warning. |
| **Chrome** (normal, F11, fullscreen video) | Borderless top-level window; video may use hardware overlay planes | **Yes** [S for fullscreen video] | |
| Other GDK/Game Pass games via the Xbox app | Mostly borderless flip model | **Yes** | |
| Older D3D9 or OpenGL games with true exclusive fullscreen | Exclusive | **No** | Audio cue; the kid can switch the game to borderless; the closing dialog still appears when the game is closed (ADR-014 §3). |

Coverage of today's dialog is the same as the overlay's: what (a) cannot show, the OK dialog cannot show either [R, ADR-014 §3].

---

## 5. Architecture impact

**Components**

| Component | Change |
|---|---|
| `EagleEye.Shared` | New tray callback for remaining time (fire-and-forget push, not a client result), e.g. `OnRemainingTime(RemainingTimeDto)` with: subject name (app or group), seconds left at the service time stamp, end kind (budget, not allowed, midnight, break), whether the clock is running, the warning levels, and the overlay setting. Possibly a new per-account setting DTO. API first, on Windows (ADR-007). |
| `EagleEye.Service` | The engine already computes the end per subject every 5 s [R]. New: (1) per session, select what the overlay shows (by default the **smallest** time left of the running subjects); (2) push it on change and periodically; (3) if the overlay is to be visible "always", extend the end horizon beyond 60 min (to midnight) for the display value only; (4) store the new setting (new state area field, migration). Enforcement is unchanged: the service stays authoritative (ADR-015 rejected a tray countdown *for enforcement*; a display countdown is fine). |
| `EagleEye.TrayClient` | New overlay window, its interop helper, a presenter that counts down locally between pushes and switches colour at the levels, and per-monitor DPI. `KidMessagePresenter` decides which reasons still open the dialog. Optional tray-menu entry "Overlay hide/show" if the kid may hide it. |
| `EagleEye.ParentApp` (+ Core) | One new setting per kid (Rules page) or global (Einstellungen page), shared MAUI XAML, so Windows and Mac get it from one codebase; the Mac target needs a build and look check on the MacBook (ADR-007). |
| Installer | None for (a). Code signing only for the (a+) uiAccess variant. |

**Push frequency**: the tray counts down by itself from "seconds left at time T" with a monotonic clock, so the service does not need to push every second. The service pushes (a) when the displayed subject or its end changes by more than one tick (rule change, group change, break change, grace period, session lock/unlock that stops the clock, app start/close), and (b) as a heartbeat about every 60 s to correct drift. That is about one small message per minute per session; negligible [K].

**Session split**: the overlay must be drawn in the kid's session by the kid's process, i.e. the tray client. The service (session 0, SYSTEM) cannot show windows on the kid's desktop, and ADR-011 §8 forbids SYSTEM windows there [R]. The existing session-bound verified tray connection (ADR-014 §1) is the right channel; the push goes only to the session's own tray, as `ShowKidMessage` does.

**If the kid ends the tray client**: no overlay, as today no dialog (arc42 R-6) [R]. Enforcement continues.

**Performance and battery [K]**: redraw once per second (or once per minute above the final level) of a small bitmap: well below 1 % CPU. The real cost is in the GPU path: while the overlay overlaps a fullscreen or borderless game, the game is composed by DWM instead of being flipped directly to the screen. Typical effect: up to about one frame of extra latency and a few percent GPU; often none on GPUs with multi-plane overlays. NFR-P-011 ("no noticeable frame drops") must be checked by the spike with an FPS reading with and without the overlay. Battery: irrelevant on the service PC; negligible on a laptop.

**Testability (manual, ADR-007)**: the overlay is directly observable. Two hard points:
1. **Screenshots** of fullscreen games may not contain the overlay, or may show black; evidence over games should be **phone photos** or a capture that includes overlays (Win+Alt+PrtScn via Game Bar captures only the game) [K, S].
2. The tray has **no log** today [R]. A tray log line (or a service log line "remaining time pushed: kid1, group Games, 4:59") makes the timing testable from LOG-WATCH. The shortened warning levels (e.g. 2/1 minutes) and small budgets make the test runs short, as in US-006.

---

## 6. Spike proposal (Windows, time box 1 day)

**Purpose**: confirm or refute the [S] statements of §3a and §4 before PRO fixes the story's scope. Throwaway code, kept only as a patch under `US-010/evidence/` (the US-008 pattern), never in production code.

**Who/where**: DEV builds the spike on the **Windows Developer Machine** (= service PC, Windows 11). Michael runs the game cases in the kid account (games installed there), with a second monitor if available. No service changes: the spike is a stand-alone WinForms exe with a fake countdown (e.g. from 6:00, colour change at 5:00 and 1:00).

**Prototype**: a borderless 32-bit per-pixel-alpha window (`UpdateLayeredWindow`) with the styles of §3a, "Games 4:59" in white on a 50 % dark pill, top-right corner of the foreground window's monitor, top-most re-asserted on foreground change and every second, per-monitor DPI aware. Command-line switches: `--uniform-alpha` (Form.Opacity variant), `--no-reassert`, `--corner`. It writes a log line per foreground change: foreground exe, monitor, DPI, and whether the overlay was still top-most before the re-assert.

**Steps**

| # | Case | Pass criterion |
|---|---|---|
| 1 | Desktop: Notepad, Explorer; click and type "through" the overlay | Clicks reach the window below; typing never goes to the overlay; overlay never in taskbar or Alt+Tab; foreground never changes when the overlay updates. |
| 2 | Minecraft Java, windowed, then F11 fullscreen, then fullscreen with a changed fullscreen resolution (if offered) | Visible in windowed and F11 (expected); record the result of the third. No flicker, no minimise. |
| 3 | Minecraft Bedrock, windowed and F11 | Visible. |
| 4 | Fortnite (EAC), Windowed Fullscreen, then Fullscreen; play 10 min | Visible in both (expected); no minimise, no EAC message, no kick; FPS with and without the overlay (Fortnite's own FPS display) differ by less than about 5 %. |
| 5 | Chrome: normal, F11, YouTube video fullscreen | Visible in all three; video smooth. |
| 6 | Steam Big Picture, Xbox app, Epic launcher in front | Visible. |
| 7 | `--no-reassert` with each game of steps 2–6 | Records which apps push themselves above the overlay. If the overlay is covered **even with** the re-assert, note it: then the uiAccess variant (a+) needs a second, small spike. |
| 8 | Multi-monitor: game on monitor 2 (different scale, e.g. 100 % vs 150 %); move the game between monitors | Overlay follows the game's monitor, sharp text (no blur), correct size at both scales. |
| 9 | Session lock/unlock, sign-out, fast user switch | No error; overlay back after unlock; sign-out not blocked. |
| 10 | Resource check: Task Manager over 10 min | Spike process CPU < 1 %, memory stable. |

**Evidence**: phone photos of each game case (screenshots may not show the overlay; take one screenshot per case too, to learn what works for TES later), the spike log, FPS numbers of step 4, a short results table in `US-010/spike-overlay.md` with the Windows build number, GPU and driver.

**Decision rule**
- Steps 1–6 pass → option (a) as recommended; story scope as in §8.
- Only true exclusive modes fail (step 2, third case) → (a) with the documented limit and the audio cue.
- Fortnite fullscreen fails or EAC reacts → (a) with "use Windowed Fullscreen" as a documented limit; Michael decides whether that is acceptable for the pilot.
- Step 7 shows covered overlays even with re-assert → short follow-up spike for (a+) uiAccess with a self-signed certificate.
- Step 4 shows noticeable frame drops → smaller overlay, show only in the last N minutes (Q-1), or uniform alpha.

---

## 7. Risks

| # | Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|---|
| OV-1 | True exclusive fullscreen hides the overlay [K] | Medium (game dependent) | Medium | Same limit as today's dialog; audio cue; borderless mode advice; final close message unchanged. |
| OV-2 | Another top-most window covers the overlay [K] | Medium | Low | Re-assert on foreground change; uiAccess fallback (a+). |
| OV-3 | Anti-cheat heuristic treats the overlay as an external ESP overlay [K] | Low | High (game account) | No injection, no game-process access, small corner pill; spike checks only for immediate reactions; residual risk named to Michael. |
| OV-4 | Frame drops / latency from composition (NFR-P-011) [K] | Low–medium | Medium | Spike step 4; minimal redraws; option to show only in the last N minutes. |
| OV-5 | The displayed number differs from enforcement (local countdown vs 5 s tick, clock stopped on lock) [R, K] | Medium | Low | Service authoritative; pushes on every change and a heartbeat; the overlay shows minutes, seconds only in the last minute. |
| OV-6 | Kid ends the tray client → no overlay [R, R-6] | Low | Low | Unchanged acceptance; enforcement in the service. |
| OV-7 | Per-monitor DPI change in the tray affects existing dialogs [R] | Low | Low | Overlay-only DPI context, or re-test the dialogs at 150 %. |
| OV-8 | Evidence over fullscreen games is hard to capture for TES [K] | High | Low | Phone photos as accepted evidence in the test plan. |

---

## 8. Product questions for Michael and PRO

| # | Question | ARC suggestion |
|---|---|---|
| Q-1 | **When** is the overlay visible: always while a limited app runs, or only in the last N minutes? | A per-kid choice "off / last N minutes / always", default **last 15 minutes** (fewer pixels over the game, cheaper; "always" needs the longer horizon). |
| Q-2 | **What** does it show: per app, per group, time to the next break, or the smallest? | The **smallest** time left among the kid's running limited subjects, with its name ("Spiele 0:12", "Ruhezeit in 4 min"). Per-app lists only on the desktop (FR-TRAY-011), not over games. |
| Q-3 | Format: minutes, or mm:ss? | "h:mm" above 1 hour, minutes until the final level, **m:ss in the last minute(s)**. |
| Q-4 | **Position, size, transparency**: fixed or settable? Who sets it? | Fixed small pill, top-right corner of the game's monitor, about 50 % background. Position (four corners) as a parent setting at most; no transparency slider in the first story. |
| Q-5 | May the **kid move or hide** it? | **Not hide** while within the final level (that is when it matters). Optionally the kid picks the corner from the tray menu. Hiding in the early phase: PRO decides; ARC suggests no hide in the first story. |
| Q-6 | Does it **replace the OK dialogs**? | Replace the **early and final warnings** and the grace-period notices (no focus steal). **Keep** the OK dialog for "start blocked", "app closed / budget used up" and "break time" (the kid must learn why the game is gone; it appears after the game closes anyway). |
| Q-7 | Behaviour at the **early/final levels** | Colour change (white → yellow at early, → red at final) and a short pulse at each threshold; the parent's warning text shown in the overlay for a few seconds (one line, truncated) or dropped? ARC suggests: show the parent's text for about 10 s under the time. |
| Q-8 | If the overlay is off or cannot be shown (tray not connected), what then? | Fall back to today's dialog when the overlay setting is "off". When on, the service cannot know whether a game covers it; no fallback to the dialog (that would bring back the annoyance). |
| Q-9 | **Sound** at the levels? | Yes, optional per kid, default on: it is the only thing that works in true exclusive fullscreen. |
| Q-10 | **Setting scope**: per kid (Rules page) or global (Einstellungen)? | **Per kid** on the Rules page (one kid may want it, another not), editable in the Windows and the Mac parent app (one MAUI codebase). |
| Q-11 | **Localization** | Overlay labels in the kid's Windows display language (ADR-016 pattern, German default, English), parent texts as stored. |
| Q-12 | **Accessibility** | Minimum size ≈ 14 pt at 100 %, scaled with DPI; high-contrast colours; colour change never the only signal (pulse/sound + number). |
| Q-13 | Multi-monitor: follow the game's monitor, or show on all? | Follow the foreground window's monitor. |
| Q-14 | Is "use Windowed Fullscreen" an acceptable documented limit for games that cover it? | ARC suggests yes, after the spike. |
| Q-15 | Break-only phases (no limited app running, a break is near) | Show "Ruhezeit in N min" only if a limited app runs (the same rule as the break warnings, ADR-015 §4). |

---

## 9. Verdict

**Feasible, with limits.** Recommended: **option (a)**, a layered, top-most, click-through, non-activating translucent window in `EagleEye.TrayClient`, fed by a new low-frequency remaining-time push from the service, plus an optional audio cue; the OK dialog stays only for the final block/close messages. It needs no installer change, no injection and no new framework, and it fits the existing session-bound tray channel (ADR-014) and the subject-based engine (ADR-015, ADR-021). It cannot be guaranteed over true exclusive fullscreen; on Windows 11 most DX11/DX12 "fullscreen" games, including (expected) Fortnite, are covered via fullscreen optimizations, which the spike must confirm.

**Size**: spike ½–1 day; story **M** (Shared contract + service push + setting + tray overlay + parent-app setting on both platforms); **L** with per-app lists, kid-movable position or foreground-app selection.

**Next step**: Michael decides whether to run the spike (§6) on the Windows Developer Machine before PRO writes US-010, and answers Q-1 to Q-15 (or leaves them to PRO). New ADRs expected with the plan: overlay technique and remaining-time push (amending ADR-014).

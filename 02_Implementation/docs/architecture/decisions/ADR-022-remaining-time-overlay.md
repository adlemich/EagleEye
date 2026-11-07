# ADR-022: Remaining-Time Overlay in the Tray Client

**Status**: Accepted (approved by the project owner with the US-010 implementation plan, 2026-11-01)
**Date**: 2026-11-01
**Deciders**: ARC, the project owner

---

## Context

Since US-006 the kid sees the early and the final warning in the topmost kid dialog with "OK". The dialog takes the keyboard focus (ADR-014 §3), so in a game the kid must leave the game to click it. US-010 (requirements v1.10: FR-TRAY-020, FR-TRAY-030 to FR-TRAY-032) replaces these dialogs by a small, half-transparent, always-on-top display of the remaining time over the running game, as FPS overlays do.

The feasibility check `02_Implementation/docs/requirements/user-stories/US-010/feasibility-overlay.md` compared four techniques: (a) a layered top-most click-through window of the tray client, (b) an Xbox Game Bar widget, (c) graphics-API hooking/injection, (d) others (toasts, DirectComposition, `WTSSendMessage`). A throwaway spike of option (a) (commit `24f6dea`) was run by the project owner on the kid's gaming PC on 2026-11-01. The pill was visible and click-through over Minecraft Java (windowed, F11, changed fullscreen resolutions), Minecraft Bedrock, Fortnite *Fenster-Vollbild* and *Vollbild* (no Easy Anti-Cheat reaction, no FPS impact felt), Chrome F11 and YouTube fullscreen, Steam Big Picture, the Xbox app and the Epic Games Launcher. Only Minecraft Java's explicit "exclusive fullscreen" option hid it, as it hides Task Manager. Evaluation: `02_Implementation/docs/requirements/user-stories/US-010/spike-overlay.md` §2.

Constraints: the service runs as SYSTEM in session 0 and must not show windows on the kid's desktop (ADR-011 §8); the tray client runs as the kid and can be ended by the kid (arc42 R-6); enforcement never depends on the tray (NFR-R-012); no injection into game processes (requirements §10).

---

## Decision

### 1. Technique: option (a), per-pixel alpha

The tray client draws the overlay in its own process, in the kid's session:

- a borderless WinForms form, created once and kept hidden while there is nothing to show, with `ShowWithoutActivation = true` and the extended styles `WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOPMOST | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW`;
- `WM_MOUSEACTIVATE → MA_NOACTIVATE`, `WM_NCHITTEST → HTTRANSPARENT`: the window never gets focus or input; clicks and keys go to the game; no taskbar button, not in Alt+Tab;
- **per-pixel alpha**: the pill is drawn with GDI+ into a 32-bpp ARGB bitmap and presented with `UpdateLayeredWindow(…, ULW_ALPHA)` (premultiplied), so the dark background has the chosen opacity while the text stays fully opaque and anti-aliased. No uniform-alpha variant;
- shown with `SW_SHOWNOACTIVATE`, hidden with `SW_HIDE`.

### 2. Staying on top

The overlay re-asserts `SetWindowPos(HWND_TOPMOST, …, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_NOOWNERZORDER)` on every foreground change and at most once per second while visible, unconditionally. Foreground changes come from an **out-of-context** WinEvent hook (`SetWinEventHook(EVENT_SYSTEM_FOREGROUND, …, WINEVENT_OUTOFCONTEXT)`): no DLL is loaded into other processes. The `uiAccess` variant (signed exe with a higher z-order band) is not used; it stays the fallback idea if a game is ever found covering the overlay despite the re-assert.

### 3. No access to other processes

The tray never opens, queries, reads or enumerates another process: it uses only `GetWindowThreadProcessId` (to ignore its own windows) and `GetClassNameW` (to ignore the shell). Nothing touches the game process; anti-cheat protection of the game is not involved. The residual risk of a heuristic "external overlay" detection is accepted (no reaction seen in the spike; re-checked with US-010 AC-24).

### 4. Monitor and DPI

The overlay sits in a corner of the **monitor of the foreground window** (`MonitorFromWindow`), measured from the monitor bounds, not the work area, with a margin of 24 logical pixels. The tray client process is **PerMonitorV2**; sizes follow the monitor's effective DPI and are re-computed on `WM_DPICHANGED` and display changes.

### 5. Exclusive fullscreen: documented limit, sound as fallback

Over an application in true exclusive fullscreen the overlay is not visible (requirements §10 note v1.10). At each warning level the tray plays the Windows sound "Exclamation" (if the user has not switched it off), which works in every mode. The kid dialog of a blocked or closed app appears when the game is gone, as before (ADR-014 §3).

### 6. The service is authoritative; the tray counts down locally

The service pushes a **remaining-time model** per session every engine tick (5 s; ADR-014 as amended by US-010): one line per running subject with its own time (ADR-021), one break line, the level minutes and the strongest **signal** of the tick, decided by the engine's warning ledger (ADR-015 §4, unchanged: once per subject and day, one end, one set of warnings). The tray only:

- counts each line down between pushes and colours it by its own time (white, yellow at the early level, red at the final level, steady red "0:00");
- filters the lines by the kid's **display threshold** (15/30/45/60 min) or the early level, sorts them (shortest first), shows at most five;
- plays the sound and pulses on a signal (quick pulse 3 s at the early level; slow pulse while a shown line is red and above 0:00);
- hides the overlay when the connection is lost, when no push came for 15 s, and while the session is locked or switched away.

The tray never decides a warning itself, never enforces, and never shows a time the service has not confirmed for more than 15 s.

### 7. Settings per Windows account in `HKCU`

Opacity (25/50/75/100 %), corner, display threshold and sound are set in the tray menu by whoever is signed in and stored in `HKCU\Software\EagleEye\TrayClient` (four `REG_DWORD` values). Values outside the menu's choices fall back to the defaults (75 %, top right, 30 min, on), so editing the registry cannot hide the overlay. The parent apps neither show nor change them.

### 8. The tray's own windows are never apps

The installed tray client is never recorded (ADR-011 §5, by full path); in addition, every popup the tray creates without a taskbar button (context menu, submenus, overlay) is a tool window, so ADR-011's "Apps" rule never counts it (spike finding D-10, FR-TRAY-032).

---

## Rationale

The spike confirmed every [S] expectation of the feasibility check for option (a) and corrected one in its favour (Minecraft's changed fullscreen resolution is covered). Option (a) needs no new framework, package, installer change, signing or injection, and fits the existing session-bound tray channel (ADR-014) and the subject-based engine (ADR-015, ADR-021).

### Alternatives Considered

| Option | Reason Rejected |
|--------|----------------|
| (b) Xbox Game Bar widget | The kid controls it (unpin, Game Bar off); MSIX, signing and a loopback exemption; a second UI stack. |
| (c) Graphics-API hooking / DLL injection | Anti-cheat ban risk; contradicts ADR-011 §8 and requirements §10; native code per graphics API. |
| (a+) `uiAccess` window | Needs a signed exe and a trusted certificate; not needed, the re-assert was enough in the spike. |
| Uniform alpha (`Form.Opacity`) | Text becomes as transparent as the background; the project owner chose the per-pixel look at 75 %. |
| Toast notifications | Suppressed during games by Windows' "do not disturb" rules. |
| Tray-side warning decisions (local level crossing) | Would repeat signals after a tray restart and could not apply the break rule ("one end, one set of warnings"). |
| Settings in a JSON file under `%LOCALAPPDATA%` | More code (folder, file, parsing, write errors) for the same per-account scope. |
| Settings in the service per account, edited by the parent | Contradicts the story (set by the kid in the tray, OQ-7); changes in all four assets. |

---

## Consequences

### Positive

- Warnings no longer take the keyboard from the game; the kid can keep playing and save in time.
- Same visibility as the old dialog in every mode, plus the sound in exclusive fullscreen.
- The service stays the single source of truth for time and warnings.

### Negative / Trade-offs

- Not visible over true exclusive fullscreen (accepted).
- The pulse and the sound can come up to one engine tick (5 s) after the colour change.
- One small push per session every 5 s.
- The tray client becomes PerMonitorV2; its existing dialogs must look right at 150 %.
- The service cannot know whether the pill was actually visible; "shown in overlay" means "sent to the session's verified tray client".

### Neutral

- If the kid ends the tray client, there is no overlay (as there was no dialog); enforcement continues.
- The tray client still has no log file.

---

## References

- US-010: `02_Implementation/docs/requirements/user-stories/US-010/user-story.md`, `implementation-plan.md`, `feasibility-overlay.md`, `spike-overlay.md` §2, `spike-overlay-results.md`; spike code commit `24f6dea`
- Requirements v1.10: FR-TRAY-020, FR-TRAY-030, FR-TRAY-031, FR-TRAY-032, NFR-P-011, §10
- ADR-011 (§4 "Apps" rule, §5 never recorded, §8), ADR-014 (tray channel; US-010 amendment), ADR-015 (warnings; US-010 amendment), ADR-021 (limit subjects)

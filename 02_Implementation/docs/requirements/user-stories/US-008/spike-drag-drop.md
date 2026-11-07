# US-008 Step 1 — Drag-and-Drop Spike (Mac Catalyst)

**Author**: DEV
**Date**: 2026-10-31
**Machine**: MacBook (macOS 27, Apple Silicon), Debug build `net10.0-maccatalyst`
**Plan**: `02_Implementation/docs/requirements/user-stories/US-008/implementation-plan.md`, Step 1, Decision 9, TI-9, Q-4
**Status**: **Done (2026-10-31).** DEV did the automatic part. The project owner did the real mouse and trackpad checks (§4) and reported all 10 steps OK (§5). **Chosen approach**: MAUI `DragGestureRecognizer`/`DropGestureRecognizer` in shared XAML, plus the "≡" button with "Move to …", plus the right-click menu (§6).

---

## 1. Prototype

Throwaway, per Decision 9. The source is **not** in the production code. It is kept only as a patch: `US-008/evidence/spike-drag-drop.patch` (re-apply with `git apply`, never commit the result).

- `Views/SpikeDragDropPage.cs` (code only, no XAML). It shows three sections ("Games" with 3 apps, "School (empty)", "Not in any group" with 7 apps) in a `ScrollView`, plus an event log and counters.
- Each entry is a `Grid` row with a `DragGestureRecognizer`, a `FlyoutBase.ContextFlyout` (`MenuFlyout` "Move to …" items), a tooltip with the path, and a "≡" button. The button opens `DisplayActionSheetAsync` ("Move to …").
- Each section's entry area is a `Border` with a `DropGestureRecognizer`. The minimum height is one row, so an empty group is a drop target too.
- The dragged app is passed **in-app** through a field (`_dragged`), the way the view model's `DraggedApp` would carry it. `e.Data.Text` gets the path only so the platform has a payload.
- Highlight: `DragOver` sets the highlight colour, `DragLeave`/`Drop` set the normal colour. `DropCompleted` clears anything still set and counts it as "highlight still set at completion". "Normal" is always an **explicit** colour (TI-10).
- Name prompt loop: "Add group" and "Rename" open `DisplayPromptAsync`. An invalid name (empty, more than 40 text elements, duplicate ignoring case) opens the prompt again with the message and the typed text. Cancel or Escape stops the loop.
- `App.CreateWindow` shows the page instead of `MainPage` only when `EAGLEEYE_SPIKE_DND=1`. `EAGLEEYE_SPIKE_PROBE=highlight|sheet|prompt` triggers code paths without any input (DEV's automatic checks).
- Every event is logged on the page and to stderr with the prefix `[SPIKE]`.

## 2. Checks (Decision 9)

| Check | Pass | Result |
|---|---|---|
| Drag with the mouse inside the `ScrollView` | Drag starts; the page does not scroll | **Pass (the project owner, §5)** |
| Drag with the trackpad inside the `ScrollView` | same | **Pass (the project owner, §5)** |
| Drop on another section, an empty one, the own section, outside | Moves, moves, nothing, nothing | **Pass (the project owner, §5)**. Move logic and re-render checked by DEV with the probe (Discord → empty "School"): `evidence/spike-2-highlight-off-moved.png` |
| Highlight follows and always clears (light and dark) | No stuck colour after 20 drags | **Pass.** By DEV, set and cleared by code: the colour appears and repaints back to normal (dark mode), `evidence/spike-1-highlight-on.png`, `spike-2-…`. Following the pointer and the 20 drags: **pass (the project owner, §5)** |
| Right-click menu on a draggable entry | Opens; no drag starts | **Pass (the project owner, §5)** |
| "≡" → "Move to …" | Sheet with the other sections + Cancel | **Pass.** By DEV, opened by code: native Catalyst sheet with "School (empty)", "Not in any group", "Cancel" (`evidence/spike-3-move-to-sheet.png`). Clicking it and the move: **pass (the project owner, §5)** |
| Name prompt loop | Opens again with the message and the text; Escape cancels | **Pass.** By DEV: the re-prompt shows the message "A group with this name already exists." above the field, with the typed text "games" pre-filled and selected (`evidence/spike-4-name-reprompt.png`). Typing, OK, Escape: **pass (the project owner, §5)** |

Probe log: `evidence/spike-probe-log.txt`.

## 3. Observations so far

- The build has 0 warnings. `DragGestureRecognizer`, `DropGestureRecognizer`, `ContextFlyout`, `DisplayActionSheetAsync` and `DisplayPromptAsync` all compile for `net10.0-maccatalyst` without platform code.
- Highlight repaint is fine when "Normal" is an explicit colour (the US-007 lesson holds).
- The prompt and the sheet are the native Catalyst alerts. They look acceptable. The re-prompt reads as "the dialog stays open with a message" (D-5, TI-12).
- For Step 9: the "≡" glyph on the small red Mac button is barely legible (`spike-2-…`). Step 9 should give the button a larger font or the `HeadingBarButton`-like transparent style.
- Decision rule mapping (applied in §6):
  - All pass → MAUI recognizers in shared XAML, "≡" + right-click menu.
  - Drag or highlight unreliable → try a `#if MACCATALYST` handler with `UIDragInteraction`/`UIDropInteraction`.
  - Both fail → stop; the project owner decides on menu only.
  - Right-click unreliable → "≡" only (Q-4).

## 4. The project owner's manual check (Debug build, MacBook)

The spike build is already built in the git-ignored `bin/` folder. **Do not rebuild before the check**: the spike source is not in the tree. If the bundle is gone, run `git apply 02_Implementation/docs/requirements/user-stories/US-008/evidence/spike-drag-drop.patch`, build, and afterwards run `git checkout -- 02_Implementation/src` and delete `Views/SpikeDragDropPage.cs`. The build does not touch the installed `/Applications` app or its pairing.

Launch (from the repo root):

```
open -n "02_Implementation/src/EagleEye.ParentApp/bin/Debug/net10.0-maccatalyst/maccatalyst-arm64/EagleEye Parent App.app" --env EAGLEEYE_SPIKE_DND=1 --stderr spike-log.txt
```

(`spike-log.txt` is not committed. Delete it afterwards or send it to DEV.)

Then, with the **mouse** and again with the **trackpad** (press and hold, or three-finger drag if enabled):

1. Drag "Minecraft" from "Games" onto "School (empty)". Expected: the School area turns green while you hover, Minecraft moves there on release, and the green clears.
2. Drag "Safari" onto "Games". Expected: it moves.
3. Drag "Word" and drop it back on "Not in any group". Expected: nothing moves (log: "own section").
4. Drag "Excel" and drop it on the header text or the empty area at the bottom. Expected: nothing moves and no green stays.
5. Make the window short so the list scrolls. Then drag an entry: does the drag start, or does the page scroll?
6. Do about 20 quick drags back and forth, also crossing sections without dropping. The counter "highlight still set at completion" should stay 0, and no section should stay green.
7. Right-click (two-finger click) an entry. Expected: a menu "Move to …". Pick one; the app moves. Does a drag start by mistake?
8. Click "≡" on an entry, then pick a section in the sheet. The app moves. Cancel moves nothing.
9. "Add group" → type "games" → OK. Expected: the prompt comes back with "A group with this name already exists." and your text. Press Escape; nothing is added. Then add "Music" and check that it appears.
10. Switch macOS to Light mode and repeat 1 and 6 briefly.

Report per step: OK / not OK (+ what happened). Screenshots are welcome.

## 5. Result of the project owner's check (2026-10-31)

The project owner ran §4 with the mouse and the trackpad on the Debug spike build. The Orchestrator relayed their report: "Done. All seems to work fine!" Recorded as **steps 1 to 10 OK, mouse and trackpad**. Their wording gives no detail per step.

Their stderr log (`spike-log.txt`, deleted afterwards and not committed) contains no errors or exceptions. It shows 9 drags (9 `DragStarting`, 9 `DropCompleted`, 9 moves via drag), 3 moves via the right-click menu and 1 drop on the own section (nothing moved). The counter "highlight still set at completion" stayed 0. The log is from one launch and may not cover every step.

## 6. Decision (decision rule of Decision 9)

All checks pass, so:

- **Drag-and-drop**: MAUI `DragGestureRecognizer` on each entry and `DropGestureRecognizer` on each section's entry area, in shared XAML. No Mac-only native handler. The dragged app is passed in-app through `AppGroupsViewModel.DraggedApp`. Each entry area has a minimum height of one row. The highlight is set on `DragOver`, cleared on `DragLeave`/`Drop`, and cleared again on `DropCompleted` as a safety net. "Normal" is an explicit background (TI-10).
- **"Move to …"**: the "≡" button on every entry → `IDialogService.ChooseAsync` (action sheet), as planned (Q-4).
- **Right-click menu**: kept (step 7 passed). It is a `FlyoutBase.ContextFlyout` with the same targets as `MoveTargets(app)`.
- **Name dialog**: the re-prompt loop (D-5, TI-12) is confirmed. No modal page is needed.
- **Step 9 to-do**: make the "≡" glyph legible (larger font or a transparent `HeadingBarButton`-like style; it is barely readable on the small accent button).

Reusable fragments for Step 9: see the patch (`BuildSection`, `BuildEntry`, `PromptNameAsync`). Step 9 rewrites them as XAML and binds them to the view models.

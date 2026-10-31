# Implementation Report: US-008 — App Groups

**Author**: DEV
**Date**: 2026-10-31 (started)
**Branch**: `feature/US-008-app-groups`
**Plan**: `02_Implementation/docs/requirements/user-stories/US-008/implementation-plan.md` (approved 2026-10-31, revision 1), ADR-021
**Machine(s) used**: MacBook so far (see §7)
**Story status**: Analyzed (in implementation)

> This report grows with each plan step. Step 1 done (2026-10-31), H1 done (branch pushed). Steps 2 to 11 not started.

---

## 1. Summary

| Step | State | Content |
|---|---|---|
| 1 | Done | Throwaway spike page (MAUI `DragGestureRecognizer`/`DropGestureRecognizer`, `ContextFlyout`, "≡" action sheet, name re-prompt loop), env-var gated, Debug build, 0 warnings. DEV checked by probe: the highlight paints and clears (explicit "Normal"), the move re-renders (also into an empty section), the "Move to …" sheet and the re-prompt with message and text appear. DEV cannot click or drag by script (no Accessibility permission), so Michael did the real checks: **steps 1 to 10 OK, mouse and trackpad** ("Done. All seems to work fine!", 2026-10-31). **Chosen approach**: MAUI drag/drop recognizers in shared XAML + the "≡" button with "Move to …" + the right-click `ContextFlyout`. Details: `US-008/spike-drag-drop.md` §5, §6. Spike source kept only as `evidence/spike-drag-drop.patch`; production code unchanged. |

## 2. Deviations from the Implementation Plan

None so far.

Notes (not deviations):
- Decision 9 says the prototype is not committed. DEV committed it as a **patch file under `evidence/`** (not in `src/`), as with the US-007 keychain probe, so that Michael's check and any follow-up attempt (native handler) can rebuild the exact spike.
- The page's entries are a `Grid` per app in a `VerticalStackLayout` per section, not a `CollectionView`. This is less code and fits the expected list size (a few dozen apps).

## 3. Files

**Step 1** — new: `02_Implementation/docs/requirements/user-stories/US-008/spike-drag-drop.md`, this report, `US-008/evidence/spike-drag-drop.patch`, `US-008/evidence/spike-probe-log.txt`, `US-008/evidence/spike-1-highlight-on.png`, `spike-2-highlight-off-moved.png`, `spike-3-move-to-sheet.png`, `spike-4-name-reprompt.png`. Production code: none.

## 4. Unit Tests

Step 1: none (throwaway spike, no production code).

## 5. Open Questions and Risks

- R-1 (drag-and-drop unusable on Catalyst) is resolved by the spike (`spike-drag-drop.md` §5). It is still untested on WinUI, where known breakage is accepted until US-009 (TI-13).
- Step 9 note: the "≡" glyph is hard to read on the small Mac accent button; Step 9 should give it a larger font or a transparent style.

## 6. How to Test

Not yet (story in progress). Spike check (done): `spike-drag-drop.md` §4, §5.

## 7. Machine(s) Used

| Step | Machine |
|---|---|
| 1 | MacBook |

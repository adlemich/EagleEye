# Implementation Report: US-008 — App Groups

**Author**: DEV
**Date**: 2026-10-31 (started)
**Branch**: `feature/US-008-app-groups`
**Plan**: `02_Implementation/docs/requirements/user-stories/US-008/implementation-plan.md` (approved 2026-10-31, revision 1), ADR-021
**Machine(s) used**: MacBook so far (see §7)
**Story status**: Analyzed (in implementation)

> This report grows with each plan step. Step 1: automatic part done, Michael's manual check pending. Steps 2 to 11 not started. H1 not done yet: it waits for the spike result.

---

## 1. Summary

| Step | State | Content |
|---|---|---|
| 1 | Automatic part done; **Michael's mouse/trackpad check pending** | Throwaway spike page (MAUI `DragGestureRecognizer`/`DropGestureRecognizer`, `ContextFlyout`, "≡" action sheet, name re-prompt loop), env-var gated, Debug build, 0 warnings. DEV checked by probe: the highlight paints and clears (explicit "Normal"), the move re-renders (also into an empty section), the "Move to …" sheet and the re-prompt with message and text appear. **No real drag or right-click has been performed** (no Accessibility permission in the DEV session). Result and checklist: `US-008/spike-drag-drop.md`. Spike source kept only as `evidence/spike-drag-drop.patch`; production code unchanged. |

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

- R-1 (drag-and-drop unusable on Catalyst) is still open until Michael's check (`spike-drag-drop.md` §4).
- Step 9 note: the "≡" glyph is hard to read on the small Mac accent button; Step 9 should give it a larger font or a transparent style.

## 6. How to Test

Not yet (story in progress). Spike check: `spike-drag-drop.md` §4.

## 7. Machine(s) Used

| Step | Machine |
|---|---|
| 1 | MacBook |

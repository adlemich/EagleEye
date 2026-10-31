---
name: maui-catalyst-unset-background
description: On Mac Catalyst a view whose BackgroundColor goes back to unset keeps the old colour on screen; visual states need an explicit Transparent in "Normal"
metadata:
  type: project
---

On Mac Catalyst (MAUI 10.0.110), when a VisualState setter or any code returns a non-layout view's `BackgroundColor` to *unset* (null), the native view is not repainted and keeps the old colour. Text colour does reset. Found in US-007 (2026-10-31): the Mac menu items stayed coloured, and the first fix aimed at the wrong cause.

**Why:** the first fix was shipped to Michael without a visual check and failed. A temporary probe (env var that switches `SelectedItem` on a timer, plus `screencapture`) verified the second fix on the MacBook without scripted clicks. osascript has no Accessibility permission, so clicks can't be scripted.

**How to apply:** every Mac-only visual state that sets a background also sets an explicit background in "Normal" (`Colors.Transparent`). Before handing a visual Mac fix to Michael, verify it on screen with such a probe. See [[eagleeye-current-phase]].

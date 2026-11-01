# US-010 Spike — Overlay Test Run: Results

**Instructions**: `02_Implementation/docs/requirements/user-stories/US-010/spike-overlay-instructions.md` (§6 explains the setup)
**Run by**: Michael
**Date**: <!-- fill in -->
**Machine**: kid's gaming PC <!-- computer name, Windows 11 version + build (winver) -->
**Account**: <!-- kid account name; controlled by EagleEye yes/no; EagleEye version -->
**GPU / driver**: <!-- Task-Manager → Leistung → GPU: name; driver version if known -->
**Monitors**: <!-- number, resolution, refresh rate, scaling of each (Einstellungen → System → Bildschirm) -->
**OverlaySpike build**: <!-- commit hash from the first log line (START) -->

**How to record** (as `02_Implementation/docs/testing/README.md` §4): tick exactly one box per case (`[x]`). **Observed** is required for Fail and Blocked. Write the log marker number (`MARK n`, hotkey **Strg+Alt+Umschalt+M**) where asked. Photos and screenshots go to `02_Implementation/docs/requirements/user-stories/US-010/evidence/` as `sp-NN-<short>.jpg/.png`. Check that no photo shows the kid's chat, friends list or account names. A partial run is fine; unticked cases count as "not executed".

**Hotkeys** (all **Strg + Alt + Umschalt** + key; they never take the focus from the game):
**0** restart at 15:00 · **5** jump to 5:10 · **1** jump to 1:10 · **O** overlay hide/show · **R** re-assert on/off · **B** beep on/off · **C** next corner · **M** log marker.
The tray icon menu (right-click on the OverlaySpike icon in the notification area) has the same items plus *Beenden*. Use it **only between cases**, not during a game.

---

## Preparation

- [ ] **P-1** Parent app → *Regeln* → the kid's account: the games' group (e.g. *Spiele*) or each game has **at least 3:00 for today**, with today ticked. No *Ruhezeit* begins in the next 3 h. Write down the old values to set them back: ____________
- [ ] **P-2** USB stick with `OverlaySpike.exe` (and `overlay-spike.json`) from `03_Delivery\spikes\US-010-overlay\` on the Windows Developer Machine.
- [ ] **P-3** Sign in to the gaming PC as the **kid**. Copy the folder into *Downloads*: path ____________
- [ ] **P-4** Double-click `OverlaySpike.exe`. If SmartScreen appears: **Weitere Informationen** → **Trotzdem ausführen**. Expected: no admin prompt; a pill **"Spiele 15:00"** at the top right; a new tray icon (maybe under the **^** arrow).
- [ ] **P-5** Wait 1 minute. OverlaySpike is still running and no EagleEye message appeared. If EagleEye blocked it: fallback in the instructions §6.3 step 2, then note it here: ____________
- [ ] **P-6** Tray icon → *Log-Ordner öffnen*: the log file `overlay-spike-….log` is there. Folder: ____________
- [ ] **P-7** Phone ready (camera). For Fortnite: *Einstellungen* → *Video/Grafik* → **FPS anzeigen** switched on (label may differ slightly).

---

## SP-01: Start, desktop, click-through, hotkeys

1. Open Editor (Notepad) and place it under the overlay corner. Click into the text where the pill is and type `test`.
2. Look at the taskbar and press **Alt+Tab**.
3. With Editor in front, press each hotkey once: **5**, **1**, **0**, **B**, **B**, **C** (4 times), **O**, **O**, **R**, **R**, **M**.

**Expected**
- Step 1: the click goes to Editor; `test` appears in Editor; the overlay does not react.
- Step 2: no OverlaySpike button in the taskbar; not in Alt+Tab.
- Step 3: each hotkey shows a small second line in the pill for 2 s (e.g. `Re-assert: AUS`); the pill moves through the 4 corners and back; **Editor keeps the focus** (the cursor still blinks in Editor).

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Marker**: ____
- **Observed**:
- **Notes**:

## SP-02: Levels and beep

1. Hotkey **5**, wait 15 s. 2. Hotkey **1**, wait 75 s.

**Expected**
- Step 1: at 5:00 the pill turns **yellow**, pulses about 3 s, one beep.
- Step 2: at 1:00 **red**, pulses slowly until 0:00, beep at 1:00 and at 0:00; then steady red `0:00`.
- Photo of the yellow and the red pill: `sp-02-yellow.jpg`, `sp-02-red.jpg`.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes** (is the look readable? too big, too small, too transparent?):

---

**For every game case (SP-03 to SP-13)**: first **M** (write down the marker), then **0** (restart). In the game, check the overlay, then press **5** and **1** to see both colours there too, and listen for the beep. Take a phone photo of the screen with the overlay (or with the place where it should be), plus one screenshot (**Win+Umschalt+S** or **Druck**).

**Pass for a game case**: the overlay is visible over the game; the game keeps keyboard and mouse (you can keep playing); the game does not flicker, minimise or drop out of fullscreen; no message from the game or its anti-cheat.

## SP-03: Minecraft Java — windowed

Start Minecraft Java via the Minecraft Launcher; join a world in a normal window (not fullscreen).

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Marker**: ____ · **Photo**: `sp-03-mc-java-window.jpg` · **Screenshot shows overlay?** [ ] yes [ ] no
- **Observed**:
- **Notes**:

## SP-04: Minecraft Java — fullscreen (F11)

In the world, press **F11** (or *Optionen* → *Grafikeinstellungen* → **Vollbild: AN**). *Vollbildauflösung* stays at its default (*Aktuell*).

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Marker**: ____ · **Photo**: `sp-04-mc-java-f11.jpg` · **Screenshot shows overlay?** [ ] yes [ ] no
- **Observed**:
- **Notes**:

## SP-05: Minecraft Java — fullscreen with a changed resolution

*Optionen* → *Grafikeinstellungen* → **Vollbildauflösung**: choose a value other than *Aktuell* (e.g. a lower resolution) → *Fertig*; fullscreen on. Afterwards set it back to *Aktuell*.
If the setting does not exist in this version: **Skipped**.

*Expected: here the overlay may be missing (true exclusive mode). That is a finding, not a defect; tick **Fail** if not visible.*

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Marker**: ____ · **Resolution chosen**: ____ · **Photo**: `sp-05-mc-java-res.jpg`
- **Observed** (visible? flicker? minimised?):
- **Notes**:

## SP-06: Minecraft Bedrock — windowed and F11

Start Minecraft (Bedrock, e.g. via the Minecraft Launcher or the Xbox app); join a world windowed, then **F11** (or *Einstellungen* → *Video* → **Vollbild**).

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Marker**: ____ · **Photos**: `sp-06-mc-bedrock-window.jpg`, `sp-06-mc-bedrock-f11.jpg`
- **Observed**:
- **Notes**:

## SP-07: Fortnite (Easy Anti-Cheat) — Fenster-Vollbild

Start Fortnite via the Epic Games Launcher (with OverlaySpike already running). *Einstellungen* → *Video/Grafik* → **Fenstermodus: Fenster-Vollbild** (Windowed Fullscreen). Play a match or the lobby/creative for about 3 min.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Marker**: ____ · **Photo**: `sp-07-fortnite-windowed-fs.jpg` · **Screenshot shows overlay?** [ ] yes [ ] no
- **Easy Anti-Cheat** message or kick at start or during play? [ ] none [ ] yes: ____________
- **Observed**:
- **Notes**:

## SP-08: Fortnite — Vollbild

**Fenstermodus: Vollbild** (Fullscreen) → apply. Play about 3 min. Note the rendering mode if you know it (*DirectX 11 / DirectX 12 / Performance*): ____

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Marker**: ____ · **Photo**: `sp-08-fortnite-fs.jpg` · **Screenshot shows overlay?** [ ] yes [ ] no
- **Easy Anti-Cheat** message or kick? [ ] none [ ] yes: ____________
- **Observed** (visible? flicker? the game drops out of fullscreen when the pill changes?):
- **Notes**:

## SP-09: Fortnite — 10 minutes of play, FPS with and without overlay

In the mode the kid normally uses (write it down: ____), same place in the game (e.g. standing still in the lobby or on the same spot in creative):
1. Overlay **on**: read the FPS (top corner of Fortnite's display) for about 30 s; note min/typical.
2. Hotkey **O** (overlay off): the same 30 s.
3. Hotkey **O** (on again). Repeat steps 1 and 2 once more.
4. Then just play normally for the rest of the 10 min with the overlay on (press **5** and **1** once during play).

**Pass**: with the overlay on, the FPS are at most about **5 %** lower than with it off; no noticeable stutter or input lag; no EAC message or kick during the 10 min.

| Round | Overlay on: FPS (typical / min) | Overlay off: FPS (typical / min) |
|---|---|---|
| 1 | | |
| 2 | | |

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Marker**: ____ · **Easy Anti-Cheat** during 10 min: [ ] nothing [ ] message/kick: ____
- **Observed**:
- **Notes** (did the kid / you feel any difference?):

## SP-10: Chrome — F11 and fullscreen video

1. Chrome, any page, **F11**. 2. F11 off; YouTube, play a video, press **f** (fullscreen).

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Marker**: ____ · **Photos**: `sp-10-chrome-f11.jpg`, `sp-10-youtube-fs.jpg`
- **Observed** (video smooth?):
- **Notes**:

## SP-11: Steam — Big Picture

Steam → *Ansicht* → **Big-Picture-Modus** (or the controller icon top right). If a Steam game is installed, start it from there as well.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Marker**: ____ · **Photo**: `sp-11-steam-bp.jpg` · **Game tried**: ____
- **Observed**:
- **Notes**:

## SP-12: Xbox app

Open the Xbox app, maximised. If a Game-Pass/Xbox game is installed, start it too.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Marker**: ____ · **Photo**: `sp-12-xbox.jpg` · **Game tried**: ____
- **Observed**:
- **Notes**:

## SP-13: Epic Games Launcher

Open the Epic Games Launcher, maximised; click around the store and the library.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Marker**: ____ · **Photo**: `sp-13-epic.jpg`
- **Observed**:
- **Notes**:

---

## SP-14: Re-assert off vs on

1. **M**, then hotkey **R** (the pill shows `Re-assert: AUS`).
2. Bring each of these to the front once, for about 30 s each: Minecraft (the mode the kid uses), Fortnite (the mode the kid uses), Chrome F11, Steam Big Picture, Xbox app, Epic launcher. Note where the overlay **disappears** or is **covered**.
3. Hotkey **R** again (`Re-assert: AN`), **M**, and repeat step 2 for the apps where it was covered.

**Expected**: with re-assert **on**, the overlay is visible everywhere it was visible in SP-03 to SP-13. With it **off**, some apps may cover it; that is the finding we want.

| App | Re-assert OFF: visible? | Re-assert ON: visible? |
|---|---|---|
| Minecraft | | |
| Fortnite | | |
| Chrome F11 | | |
| Steam Big Picture | | |
| Xbox app | | |
| Epic launcher | | |

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Markers**: ____ / ____
- **Observed**:
- **Notes**:

## SP-15: Lock and unlock

With a game or Chrome F11 in front: **Win+L**, wait 30 s, sign in again.

**Expected**: the overlay is back after unlocking (still counting), over the same app; no error balloon.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

## SP-16 (optional): Two monitors and scaling

Only if the gaming PC has two monitors. Ideally they have different scaling (*Einstellungen* → *System* → *Bildschirm* → *Skalierung*, e.g. 100 % and 150 %; write down: ____ / ____).
1. Editor on monitor 1, then move it to monitor 2. 2. A game (windowed) on monitor 2, then fullscreen on monitor 2. 3. Click on monitor 1 (desktop or another window), then back into the game.

**Expected**: the overlay is always on the monitor of the window in front, within about 1 s; the text is sharp (not blurry) and the same physical size at both scales.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Photos**: `sp-16-mon1.jpg`, `sp-16-mon2.jpg`
- **Observed**:
- **Notes**:

## SP-17: Sign-out

Note the time: ____. Start → user icon → **Abmelden**, with OverlaySpike running. Sign in again as the kid (it does **not** start by itself; that is expected).

**Expected**: the sign-out is not delayed by OverlaySpike (no "Diese App verhindert das Abmelden" screen for it). The newest log ends with `session ending`.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Observed**:
- **Notes**:

## SP-18: Wrap-up

1. Start OverlaySpike once more, then tray icon → **Beenden** (if it is still running from SP-17, use that one).
2. Copy **all** `overlay-spike-*.log` files (log folder from P-6) to the USB stick → into `02_Implementation/docs/requirements/user-stories/US-010/evidence/`.
3. Copy the photos and screenshots into the same folder.
4. Parent app → *Regeln*: set the budgets of P-1 back. If an *OverlaySpike* row was created (P-5 fallback), set it to 0:00 / no day.
5. Optional: delete the OverlaySpike folder from the gaming PC.

**Expected**: the log's `CPU` lines show OverlaySpike mostly below 1 % CPU; the `END` line has the summary.

- **Result**: [ ] Pass  [ ] Fail  [ ] Blocked  [ ] Skipped
- **Highest CPU % seen in the log** (DEV can fill in): ____
- **Observed**:
- **Notes**:

---

## General Feedback

<!-- Anything else: how the overlay looked over the games, what the kid said, ideas for position, size, colours, when it should be visible, whether the beep is useful or annoying. This goes to PRO as input for US-010 (feasibility §8). -->

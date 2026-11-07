# EagleEye.TrayClient

Lightweight Windows tray application that runs in the **kid's user session**. Shows the kid the remaining time (overlay) and the kid dialogs for blocked starts and enforced closes.

## Responsibility

- Display a system tray icon showing the connection status; its menu (left or right click) holds the overlay settings and *App Infos*
- Show the kid dialog (topmost, "OK") when the service blocks a start or closes an app (budget used up, break time), US-005/US-006
- Show the pairing code window when a parent app pairs (US-002)
- Display the remaining-time overlay (US-010, ADR-022): a half-transparent, click-through, top-most pill with one line per
  running subject and an approaching break, coloured by warning level, with pulse and sound; settings (opacity, corner,
  display threshold, sound) in the tray menu, stored per Windows account in `HKCU\Software\EagleEye\TrayClient`
- Connect to `EagleEye.Service` via SignalR over localhost

## Component Structure

```
EagleEye.TrayClient/
├── Overlay/       US-010: OverlayController (UI thread glue), OverlayWindow (layered, per-pixel alpha), PillRenderer,
│                  OverlayLayout (due lines, order, cap, text), RemainingTimeClock (local countdown, 15 s staleness),
│                  PillAnimation (pulse, sound decision), OverlaySettings + RegistryOverlaySettingsStore (HKCU),
│                  ForegroundHook (out-of-context WinEvent hook), OverlayNative. The process is PerMonitorV2.
├── UI/            Tray icon, TrayMenu (tool-window menus, left and right button, US-010), TrayWindowStyles,
│                  pairing-code window, About dialog. US-005/US-006: KidMessagePresenter
│                  (one dialog at a time: Shown, or Replaced = the open dialog gets the newer text), KidMessageText
│                  (the parent's text or the default in the kid's language, ADR-016, and the info line by reason;
│                  since US-010 for blocked starts and enforced closes only),
│                  KidMessageDialog (topmost, text + separator + info line, UpdateText re-asserts topmost and
│                  focus, OK/Enter only, Esc/Alt+F4 ignored, sign-out never blocked), ForegroundHelper
└── Communication/ SignalR client — connects to EagleEye.Service on localhost
```

## Runtime

- Runs as a standard Windows executable in the kid's user session
- Auto-started at sign-in via the `HKLM\...\CurrentVersion\Run` value `EagleEyeTrayClient` (written by the installer)
- Connects to `EagleEye.Service` SignalR hub on localhost
- Packaged together with `EagleEye.Service` in the Inno Setup installer

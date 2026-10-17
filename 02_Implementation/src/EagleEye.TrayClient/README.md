# EagleEye.TrayClient

Lightweight Windows tray application that runs in the **kid's user session**. Informs the kid about remaining time and enforces notifications.

## Responsibility

- Display a system tray icon showing EagleEye status
- Show remaining time budget for the active application
- Show notifications when time is running low or exhausted
- Optionally display a TopMost overlay window with remaining time (visible over most apps)
- Connect to `EagleEye.Service` via SignalR over localhost

## Component Structure

```
EagleEye.TrayClient/
├── UI/            Tray icon, context menu, pairing-code window, About dialog. US-005/US-006: KidMessagePresenter
│                  (one dialog at a time: Shown, or Replaced = the open dialog gets the newer text), KidMessageText
│                  (the parent's text or the default in the kid's language, ADR-016, and the info line by reason),
│                  KidMessageDialog (topmost, text + separator + info line, UpdateText re-asserts topmost and
│                  focus, OK/Enter only, Esc/Alt+F4 ignored, sign-out never blocked), ForegroundHelper
└── Communication/ SignalR client — connects to EagleEye.Service on localhost
```

## Runtime

- Runs as a standard Windows executable in the kid's user session
- Auto-started via Windows Startup registry entry (configured by the installer)
- Connects to `EagleEye.Service` SignalR hub on localhost
- Packaged together with `EagleEye.Service` in the Inno Setup installer

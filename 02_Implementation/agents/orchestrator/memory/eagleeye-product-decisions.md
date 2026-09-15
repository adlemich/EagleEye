---
name: eagleeye-product-decisions
description: Locked-in product and technical decisions for EagleEye from the bootstrapping interview
metadata:
  type: project
---

Decisions already settled in `01_Intend_and_Constraints/questions_and_answers.md` — do not re-litigate or re-ask:

- **Pairing/connectivity**: manual hostname (preferred) or IP entry in the parent app; local LAN only. Remote access deferred but must not be architecturally blocked.
- **Transport security**: self-signed cert, auto-generated on first run, non-expiring, fully transparent to the user.
- **Enforcement**: custom process monitoring + graceful WM_CLOSE, then force-kill after timeout.
- **Windows split**: `EagleEye.Service` runs as SYSTEM (no desktop), so `EagleEye.TrayClient` runs in the kid's session and talks to the service over SignalR on localhost. Both ship in one Inno Setup installer.
- **Overlay**: optional, TopMost window only; DirectX/fullscreen-game overlay deferred.
- **Multi-kid**: service auto-discovers all local standard (non-admin) accounts; all config is per account.
- **Time control is two-layer**: pause windows per weekday (block everything regardless of budget) + per-app daily budgets in hours:minutes, counting down only during non-pause time. Minimum unit = minutes. No carry-over; reset at midnight.
- **Distribution**: Windows = Inno Setup; macOS = direct `.dmg`, no Mac App Store; iOS/Android = manual sideloading (Xcode/ios-deploy, adb), stores deferred.
- **Repo**: single monorepo, trunk-based dev on `main`, short-lived `feature/US-XXX-...` branches.
- **Sequencing**: macOS parent app + Windows service mature first; iOS/Android come later.

Stack per ADR-001: .NET 10, MAUI, SignalR, PowerShell 7.6, Inno Setup, VSCode, PlantUML in local Docker. API-first — SignalR contracts in `EagleEye.Shared/Contracts/` are the interface spec and change before any implementation. See [[eagleeye-dev-test-setup]].

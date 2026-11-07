---
name: mac-local-network-after-rebuild
description: After installing a rebuilt ad-hoc signed Mac .dmg, the app may not connect until its "Lokales Netzwerk" switch is toggled off/on
metadata:
  type: project
---

2026-10-31 (US-008, MacBook): after the project owner reinstalled the rebuilt 0.8.0 `.dmg` (header-outline fix), the Mac app could not connect to either service PC. Port 5443 was reachable from Terminal on both PCs, so network, service and firewall were fine. Ping fails anyway, because Windows blocks ICMP by default, so it proves nothing.

Cause: the app is ad-hoc signed, so every build is a new app identity for macOS. System Settings → Datenschutz & Sicherheit → Lokales Netzwerk then lists one entry per build ("EagleEye Parent App.app" twice, plus "EagleEye.ParentApp" from DEV's debug probe), all shown as on, but the current build was not actually allowed.

Fix that worked: quit the app (Cmd+Q), switch the EagleEye entries off and on again, then restart the app (and allow the Keychain prompt with "Immer erlauben").

**Why:** this will recur after every rebuilt `.dmg` until the app gets a stable signing identity, which the project owner will not pay for (see [[eagleeye-product-decisions]]).

**How to apply:** for "Mac app can't connect after an update", suggest this toggle first. Consider adding it to the Mac install/update notes and to `dev-environments.md` §5 (the project owner has not decided yet). Related: [[maui-catalyst-unset-background]].

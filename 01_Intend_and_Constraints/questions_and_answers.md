# EagleEye Project - Clarifying Questions & Answers

This file records all clarifying questions asked during the project bootstrapping interview, along with Michael's answers.

---

## Group 1: Connectivity & Security

**Q1.1**: How does the parent app discover and initially pair with the Windows server on the LAN?

**A**: Manual hostname or IP address entry in the parent app. HOSTNAME is preferred over IP since most private LAN devices use DHCP. The user must know the Windows machine hostname.

---

**Q1.2**: Should the SignalR connection be TLS-encrypted? If yes, how should the certificate be handled?

**A**: Yes, self-signed certificate. Auto-generated on first run, non-expiring. Must be fully transparent to the user — no manual certificate management required by the end user.

---

**Q1.3**: Is remote access (parent connecting from outside the home LAN) a requirement?

**A**: Local LAN only for now. Remote access may come as a later requirement. Customers may solve it themselves via home VPN. Architecture should not block future remote access, but no implementation needed now.

---

## Group 2: Windows Server / Enforcement

**Q2.1**: How should app enforcement work — how does EagleEye prevent the kid from launching a non-allowed application?

**A**: Custom process monitoring + kill. Graceful shutdown attempt first (e.g., WM_CLOSE), then force-kill if the process does not exit within a timeout.

---

**Q2.2**: The overlay showing remaining time — "always visible, also in games." What level of implementation is required?

**A**: TopMost window for now. The overlay is an optional feature. Simple topmost window implementation is sufficient. Full-screen game (DirectX overlay) support is deferred to a later stage.

---

**Q2.3**: What account context should the Windows service run under?

**A**: SYSTEM account.

---

**Q2.4**: The tray icon — which user session does it appear in and what is its purpose?

**A**: Its purpose is to inform the kid. Since the service runs as SYSTEM (no desktop session), a separate lightweight executable (Tray Client) is needed. It runs in the kid's user session, connects to the local Windows service via SignalR (localhost), and shows information like remaining time and notifications. It must be packaged and installed together with the Windows service in a single installer as a secondary executable.

**Components on Windows side**:
1. `EagleEye.Service` — Windows service running as SYSTEM
2. `EagleEye.TrayClient` — user-session tray application, auto-started for the kid's profile

---

## Group 3: Multi-kid / Multi-profile

**Q3.1**: Can multiple Windows user accounts on the same PC each have separate rules and time budgets?

**A**: Yes. The service auto-discovers all local standard (non-admin) Windows user accounts. All configuration (app rules, time budgets) is managed per discovered user account.

---

## Group 4: Time Budget Details

**Q4.1**: What is the minimum unit for time budget configuration?

**A**: Minutes.

---

**Q4.2**: Is a time-of-day schedule required, or total daily/weekly minutes only?

**A**: Two-layer time control:
1. **Pause windows** — configured per weekday (e.g., blocked from 20:00–09:00). During a pause period, no applications are allowed regardless of budget.
2. **Time budgets** — per application, in hours:minutes per day. Budget only counts down during non-pause (active) time.

---

**Q4.3**: Does unused daily budget carry over to the next day?

**A**: No carry-over. Budget resets at midnight.

---

## Group 5: Distribution & Packaging

**Q5.1**: Windows installer toolset preference?

**A**: Inno Setup.

---

**Q5.2**: iOS and Android app distribution?

**A**: Manual sideloading only for now — iOS via Xcode/`ios-deploy`, Android via `adb`. App Store / Play Store distribution deferred to a later stage when the product is mature.

---

**Q5.3**: macOS app distribution?

**A**: Direct distribution as `.dmg`. No Mac App Store.

---

## Group 6: Repository Structure

**Q6.1**: Monorepo or separate repos per component?

**A**: Single monorepo for all components. Clean folder structure separating production code, documentation, and test code.

---

**Q6.2**: Branch strategy?

**A**: Trunk-based development. `main` as the single long-lived branch, short-lived feature branches merged back quickly.

---

**Q6.3**: Where does the git repo root live relative to the project folders?

**A**: Git repo root is at `/Users/micha/Documents/App-Development/EagleEyeParentalControl`. The `01_Intend_and_Constraints` folder is part of the project documentation and lives in the repo. Top-level files at root: `README.md`, global `CLAUDE.md` and orchestrator agent config files, side by side with `01_Intend_and_Constraints/`, `02_Implementation/`, and `03_Delivery/`.

---

## Group 7: AI Agent Configuration

**Q7.1**: How should the AI agents (PRO, ARC, DEV, TES, Orchestrator) be implemented?

**A**: Separate `CLAUDE.md` files per agent in their own subfolders. Each agent has its own specific skills (e.g., PlantUML, Clean Code in .NET, etc.).

---

**Q7.2**: Which Claude model to use in agent configurations?

**A**: `claude-opus-5` for all agent configurations.

*Correction (2026-09-15)*: the answer originally recorded `claude-opus-4-6`, which is not a valid model ID. Updated to `claude-opus-5`, matching `technology_selection.md`.

---

## Group 8: CI/CD

**Q8.1**: Any CI/CD pipeline planned at this stage?

**A**: PowerShell 7.6 scripts only, running locally on the MacBook. No CI/CD pipeline for now.

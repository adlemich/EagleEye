# ADR-001: Technology Selection

**Status**: Accepted
**Date**: 2026-09-15
**Deciders**: Michael (project owner)

---

## Context

The EagleEye project requires a full technology stack covering a Windows background service, a Windows tray application, a cross-platform parent app (iOS, Android, macOS), inter-component communication, scripting, installer, and IDE tooling. All decisions were made upfront to ensure consistency and minimize integration risk.

---

## Decision

We will use the following technologies for the EagleEye project:

| Area | Technology | Version |
|------|-----------|---------|
| Primary language | C# / .NET | .NET 10 |
| Cross-platform UI (parent app) | .NET MAUI | .NET 10 |
| Communication layer | SignalR (ASP.NET Core) | .NET 10 |
| Windows service runtime | Microsoft.Extensions.Hosting.WindowsServices | .NET 10 |
| Scripting / automation | PowerShell | 7.6 |
| Windows installer | Inno Setup | Latest |
| Source control | Git / GitHub | — |
| IDE | Visual Studio Code | Latest |
| Diagram tooling | PlantUML (local Docker server) | Latest |
| AI development assistant | Claude Code | claude-opus-5 |

---

## Rationale

### .NET 10 + MAUI

.NET 10 is the current LTS release. MAUI enables a single codebase for iOS, Android, and macOS parent apps, maximizing code reuse as required. Only platform-specific UI code differs across platforms.

### SignalR

SignalR was selected as the single integration technology because it natively supports all three required communication patterns:
1. **Data queries** — request/response from parent app to service
2. **Event push** — server-initiated updates to connected clients (no polling)
3. **Pub/Sub** — multiple parent apps stay synchronized simultaneously

### PowerShell 7.6

Cross-platform PowerShell runs on the macOS development machine and can target Windows build steps in the Windows VM. Chosen for consistency — one scripting language for all automation.

### Inno Setup

Mature, widely used Windows installer tool. Produces a single `.exe` installer with a UI wizard (install, repair, uninstall). Well-suited for distributing both `EagleEye.Service` and `EagleEye.TrayClient` in one package.

### Local LAN (SignalR over HTTPS)

The parent app connects to the Windows service via the local home network. Remote access is out of scope for the initial version. Users may use VPN as a workaround for remote access.

---

## Consequences

### Positive

- Single language (.NET / C#) across all components reduces context switching
- MAUI maximizes shared code between iOS, Android, and macOS parent apps
- SignalR eliminates the need for separate REST + WebSocket + polling implementations
- PowerShell 7.6 runs on both macOS and Windows for seamless cross-platform scripting

### Negative / Trade-offs

- .NET MAUI has known limitations on macOS Catalyst — some native macOS UI patterns may require workarounds
- Inno Setup is Windows-only — installer can only be built in the Windows VM
- Local LAN only means parents must be on the same network as the Windows PC

### Neutral

- Visual Studio Code is the selected IDE; full MAUI visual designer is only available in Visual Studio for Windows (not blocking for this project)

---

## Amendments

**2026-09-15** — AI development assistant model ID corrected from `claude-opus-4-6` (not a valid model ID) to `claude-opus-5`. Applied to the root `CLAUDE.md`, all four agent `CLAUDE.md` files, and `01_Intend_and_Constraints/questions_and_answers.md` (Q7.2). No other part of this decision changes.

---

## References

- `01_Intend_and_Constraints/technology_selection.md` — source of all decisions recorded here
- `01_Intend_and_Constraints/questions_and_answers.md` — Q5.1, Q5.2, Q5.3, Q8.1

# EagleEye — Parental Control for Windows

EagleEye is a parental control solution that allows parents to control which applications their children can use on Windows, set time budgets per application, configure usage schedules, and monitor usage statistics — all remotely from a mobile or macOS parent app.

---

## Components

| Component | Description | Technology |
|-----------|-------------|------------|
| `EagleEye.Service` | Windows service (SYSTEM) — monitors processes, enforces rules, serves SignalR hub | .NET 10, Windows Service |
| `EagleEye.TrayClient` | Windows tray application — runs in kid's user session, shows remaining time | .NET 10, WinForms |
| `EagleEye.ParentApp` | Cross-platform parent app — configure rules, view statistics | .NET 10, MAUI (iOS / Android / macOS) |
| `EagleEye.Shared` | Shared library — SignalR contracts (API-first), domain models | .NET 10 |

---

## Repository Structure

```
EagleEyeParentalControl/
├── 01_Intend_and_Constraints/   Project input: intent, constraints, Q&A
├── 02_Implementation/           All source code, tests, docs, scripts
└── 03_Delivery/                 Release artifacts: installer, .dmg
```

---

## Prerequisites

| Tool | Version | Purpose |
|------|---------|---------|
| .NET SDK | 10.0+ | Build all components |
| PowerShell | 7.6+ | All automation scripts |
| VSCode | Latest | Primary IDE |
| Xcode | Latest | iOS/macOS builds |
| VMware Fusion | Latest | Windows 11 test VM |
| Docker | Latest | Local PlantUML server |

---

## Getting Started

### 1. Clone the repository

```bash
git clone https://github.com/adlemich/EagleEye.git
cd EagleEye
```

### 2. Configure secrets

```bash
cp secrets/secrets.template.json secrets/secrets.json
# Edit secrets/secrets.json with your credentials (never commit this file)
```

### 3. Build

```pwsh
cd 02_Implementation
pwsh scripts/build.ps1
```

### 4. Run tests

```pwsh
pwsh scripts/test.ps1
```

---

## Development Workflow

This project uses an AI-assisted, incremental development process coordinated by the **Dev Process Orchestrator** agent. See `CLAUDE.md` at the repository root for the workflow.

All development follows a strict phase sequence:
1. PRO produces General Product Requirements
2. ARC produces system architecture and design documents
3. DEV implements one user story at a time, guided by ARC's implementation plan
4. TES verifies each user story with E2E tests and a test report

Each phase requires explicit approval before the next begins.

---

## PlantUML Diagrams

Architecture diagrams are embedded in Markdown using PlantUML. To render them locally, run a PlantUML server via Docker:

```bash
docker run -d -p 8080:8080 plantuml/plantuml-server:jetty
```

VSCode is pre-configured to use `http://localhost:8080` for diagram rendering.

---

## Git Workflow

- **Strategy**: Trunk-based development
- **Main branch**: `main`
- **Feature branches**: Short-lived, named `feature/US-XXX-short-description`
- **Commit style**: Imperative, present tense — `Add process monitoring component`

---

## License

Released under the [MIT License](LICENSE) — Copyright (c) 2026 Michael Adler.

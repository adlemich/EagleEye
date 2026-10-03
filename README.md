# EagleEye — Parental Control for Windows

EagleEye is a parental control solution that lets parents control which applications their children can use on Windows, set time budgets per application, configure usage schedules, and monitor usage statistics, all remotely from a parent app on Windows, macOS or Android.

---

## Components

| Component | Description | Technology |
|-----------|-------------|------------|
| `EagleEye.Service` | Windows service (SYSTEM): monitors processes, enforces rules, serves the SignalR hub | .NET 10, Windows Service |
| `EagleEye.TrayClient` | Windows tray application: runs in the kid's user session, shows remaining time | .NET 10, WinForms |
| `EagleEye.ParentApp` | Parent app: configure rules, view statistics | .NET 10, MAUI (Android + iOS for production; Windows client for testing; macOS) |
| `EagleEye.Shared` | Shared library: SignalR contracts (API-first), domain models | .NET 10 |

---

## Repository Structure

```
EagleEye/
├── 01_Intend_and_Constraints/   Project input: intent, constraints, Q&A
├── 02_Implementation/           Source code, tests, docs, scripts
└── 03_Delivery/                 Release artifacts: installer, .dmg, .apk
```

---

## Two Development Machines

EagleEye is developed on two machines that share this repository (see `02_Implementation/docs/dev-process/dev-environments.md`, ADR-007):

| Machine | Develops, builds and tests |
|---------|---------------------------|
| **Windows 11 developer machine** | Service, TrayClient, Shared, ParentApp **Windows** + **Android** targets, Windows installer. It is also the **manual test station**. |
| **MacBook** | ParentApp **macOS** target (Mac Catalyst), `.dmg` |

Sync is through git only: pull when you start, commit and push before you switch machines.

### Prerequisites

| Tool | Windows machine | MacBook |
|------|:---:|:---:|
| .NET SDK 10 | ✔ | ✔ |
| .NET MAUI workloads | Windows + Android | Mac Catalyst |
| PowerShell 7.6 | ✔ | ✔ |
| VSCode (C# Dev Kit, .NET MAUI extension) | ✔ | ✔ |
| Inno Setup 6 | ✔ | |
| Android SDK / emulator | ✔ | |
| Xcode | | ✔ |
| Docker or Podman (PlantUML server) | optional | optional |

---

## Getting Started

```pwsh
git clone https://github.com/adlemich/EagleEye.git
cd EagleEye

# Which machine is this, and what is installed?
pwsh 02_Implementation/scripts/env-check.ps1

# Build and unit-test everything that belongs to this machine
pwsh 02_Implementation/scripts/build.ps1
pwsh 02_Implementation/scripts/test.ps1
```

Secrets (only needed for pushing and for signed packaging) go in `secrets/secrets.json`, created per machine from `secrets/secrets.template.json`. Never commit it.

VSCode build tasks and launch configurations live in `02_Implementation/.vscode/`. Open the `02_Implementation` folder to use them.

---

## Development Workflow

This project uses an AI-assisted, incremental development process coordinated by the **Dev Process Orchestrator** (see `CLAUDE.md`). Each user story goes through:

1. PRO writes the user story
2. ARC writes the implementation plan, including which machine does which step
3. DEV implements it with unit tests and smoke-checks the build
4. TES writes a **manual** test plan and checklist; Michael executes it and records the results
5. TES evaluates the results and writes the test report and issues

Each step requires Michael's explicit approval. Acceptance testing is 100 % manual; see `02_Implementation/docs/testing/README.md`.

---

## PlantUML Diagrams

Architecture diagrams are embedded in Markdown using PlantUML. Start a local server (Docker or Podman) on either machine:

```pwsh
pwsh 02_Implementation/scripts/plantuml.ps1          # start on http://localhost:8080
pwsh 02_Implementation/scripts/plantuml.ps1 -Action stop
```

---

## Git Workflow

- **Strategy**: one feature branch per user story (`feature/US-XXX-<short-title>`), merged into `main` (`--no-ff`) only when the story is closed. Process/setup changes go directly to `main`. Details: `02_Implementation/docs/dev-process/dev-process.md` §4.
- **Commit style**: imperative, present tense, referencing the story where applicable: `US-001: Add tray connection status`

---

## License

Released under the [MIT License](LICENSE). Copyright (c) 2026 Michael Adler.

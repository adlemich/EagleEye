# EagleEye — Project Structure Plan

This document describes the planned folder structure and initial files to be generated under `02_Implementation`. It is subject to Michael's review and approval before anything is generated.

---

## Repository Root Layout

```
EagleEyeParentalControl/                    ← git repo root
├── .gitignore                              ← .NET / MAUI / macOS / PowerShell + secrets rules
├── .gitattributes                          ← line endings, binary file handling
├── README.md                              ← project overview, setup instructions
├── CLAUDE.md                              ← Dev Process Orchestrator agent config (top-level)
├── secrets/                               ← LOCAL ONLY — never committed to git
│   ├── secrets.template.json              ← committed: shows required keys, no values
│   └── secrets.json                       ← NOT committed: actual credentials (gitignored)
├── 01_Intend_and_Constraints/             ← existing (project input & documentation)
├── 02_Implementation/                     ← main development folder (see below)
└── 03_Delivery/                           ← release artifacts
    ├── windows/                           ← Inno Setup installer output
    ├── macos/                             ← .dmg output
    └── release-notes/                     ← per-version release notes
```

---

## 02_Implementation Layout

```
02_Implementation/
├── EagleEye.sln                           ← .NET solution file (references all projects)
├── .editorconfig                          ← .NET code style & formatting rules
│
├── .vscode/
│   ├── settings.json                      ← workspace settings (file associations, formatters)
│   ├── extensions.json                    ← recommended extensions list
│   ├── tasks.json                         ← build / test / run tasks via PowerShell scripts
│   └── launch.json                        ← debug launch configs (service + tray client)
│
├── src/                                   ← all production source code
│   │
│   ├── EagleEye.Shared/                   ← shared library (used by all components)
│   │   ├── Contracts/                     ← SignalR hub interfaces & DTOs (API-first)
│   │   ├── Models/                        ← shared domain models
│   │   └── EagleEye.Shared.csproj
│   │
│   ├── EagleEye.Service/                  ← Windows service (runs as SYSTEM)
│   │   ├── Monitoring/                    ← process monitoring component
│   │   ├── Enforcement/                   ← graceful shutdown + force-kill component
│   │   ├── Configuration/                 ← per-user config management
│   │   ├── Statistics/                    ← usage statistics collection
│   │   ├── Communication/                 ← SignalR hub (server side)
│   │   ├── Certificates/                  ← self-signed TLS cert generation & management
│   │   └── EagleEye.Service.csproj
│   │
│   ├── EagleEye.TrayClient/               ← Windows tray app (kid's user session)
│   │   ├── UI/                            ← tray icon, notification bubbles, topmost overlay
│   │   ├── Communication/                 ← SignalR client (localhost)
│   │   └── EagleEye.TrayClient.csproj
│   │
│   └── EagleEye.ParentApp/                ← MAUI parent app (iOS, Android, macOS)
│       ├── Platforms/
│       │   ├── iOS/                       ← iOS platform specifics
│       │   ├── Android/                   ← Android platform specifics
│       │   └── MacCatalyst/               ← macOS platform specifics
│       ├── ViewModels/                    ← shared view models (MVVM)
│       ├── Views/                         ← shared UI views
│       ├── Communication/                 ← SignalR client
│       └── EagleEye.ParentApp.csproj
│
├── tests/                                 ← all test code
│   ├── EagleEye.Shared.Tests/             ← unit tests for shared library
│   ├── EagleEye.Service.Tests/            ← unit tests for Windows service
│   ├── EagleEye.TrayClient.Tests/         ← unit tests for tray client
│   ├── EagleEye.ParentApp.Tests/          ← unit tests for parent app
│   └── EagleEye.E2E.Tests/               ← end-to-end tests (TES agent owns this)
│
├── docs/
│   ├── architecture/
│   │   ├── arc42/
│   │   │   └── system-architecture.md    ← arc42 template (ARC agent fills this)
│   │   ├── decisions/                    ← Technical Decision Log
│   │   │   ├── ADR-000-template.md       ← ADR template
│   │   │   └── ADR-001-technology-selection.md  ← first ADR (pre-filled from input docs)
│   │   ├── product-design-principles.md  ← ARC guardrails & design patterns for DEV
│   │   └── product-coding-guidelines.md  ← .NET coding standards & rules for DEV
│   │
│   ├── requirements/
│   │   ├── general-product-requirements.md  ← PRO agent fills this
│   │   └── user-stories/
│   │       ├── US-000-template.md         ← user story template
│   │       └── (US-001, US-002, ... added per iteration)
│   │
│   └── test-reports/                      ← TES agent deposits static HTML reports here
│
├── installer/
│   └── windows/
│       └── setup.iss                      ← Inno Setup script stub
│
├── scripts/                               ← PowerShell 7.6 automation scripts
│   ├── build.ps1                          ← build all components
│   ├── test.ps1                           ← run all unit tests
│   ├── package-windows.ps1                ← build + package Windows installer
│   └── package-macos.ps1                  ← build + package macOS .dmg
│
└── agents/                                ← AI agent configurations
    ├── orchestrator/
    │   └── CLAUDE.md                      ← Dev Process Orchestrator instructions
    ├── pro/
    │   ├── CLAUDE.md                      ← Product Owner agent instructions
    │   └── skills/                        ← PRO-specific slash commands
    ├── arc/
    │   ├── CLAUDE.md                      ← Architect agent instructions
    │   └── skills/                        ← ARC-specific slash commands (PlantUML, arc42)
    ├── dev/
    │   ├── CLAUDE.md                      ← Developer agent instructions
    │   └── skills/                        ← DEV-specific slash commands (.NET, clean code)
    └── tes/
        ├── CLAUDE.md                      ← E2E Tester agent instructions
        └── skills/                        ← TES-specific slash commands (test reports)
```

---

## Files Generated With Full Content

The following files will be generated with real, working content during bootstrapping:

| File | Content |
|------|---------|
| `.gitignore` | .NET 10, MAUI, macOS, PowerShell, VSCode, Rider ignore rules — including `secrets/secrets.json` |
| `secrets/secrets.template.json` | Template pre-populated with GitHub keys (username, personal access token, repo URL for `github.com/adlemich`). Placeholder sections for Apple Developer and Android signing — to be filled when those pipelines are built. |
| `.gitattributes` | Line ending normalization, binary file handling |
| `README.md` | Project overview, component map, setup instructions |
| `CLAUDE.md` (root) | Dev Process Orchestrator — role, workflow, rules, model config |
| `02_Implementation/.editorconfig` | .NET code style: indentation, naming, line length |
| `02_Implementation/.vscode/settings.json` | C# formatter, file associations, PowerShell config |
| `02_Implementation/.vscode/extensions.json` | C#, MAUI, PowerShell, PlantUML, GitLens extensions |
| `02_Implementation/.vscode/tasks.json` | Build, test, package tasks wired to PowerShell scripts |
| `02_Implementation/.vscode/launch.json` | Debug configs for service and tray client |
| `02_Implementation/EagleEye.sln` | Minimal solution file referencing all 4 src projects |
| `02_Implementation/scripts/build.ps1` | Script stub with structure and comments |
| `02_Implementation/scripts/test.ps1` | Script stub |
| `02_Implementation/scripts/package-windows.ps1` | Script stub |
| `02_Implementation/scripts/package-macos.ps1` | Script stub |
| `02_Implementation/installer/windows/setup.iss` | Inno Setup stub with app metadata |
| `02_Implementation/docs/architecture/arc42/system-architecture.md` | arc42 template pre-filled with project context |
| `02_Implementation/docs/architecture/decisions/ADR-000-template.md` | Blank ADR template |
| `02_Implementation/docs/architecture/decisions/ADR-001-technology-selection.md` | Pre-filled from technology_selection.md |
| `02_Implementation/docs/requirements/general-product-requirements.md` | Stub with structure for PRO agent to fill |
| `02_Implementation/docs/requirements/user-stories/US-000-template.md` | User story template with acceptance criteria structure |
| `02_Implementation/agents/orchestrator/CLAUDE.md` | Orchestrator role, workflow steps, handover rules |
| `02_Implementation/agents/pro/CLAUDE.md` | PRO role, user story format, PlantUML mockup guidance |
| `02_Implementation/agents/arc/CLAUDE.md` | ARC role, arc42 usage, ADR process, PlantUML, .NET patterns |
| `02_Implementation/agents/dev/CLAUDE.md` | DEV role, .NET 10 best practices, clean code, unit test rules |
| `02_Implementation/agents/tes/CLAUDE.md` | TES role, E2E test approach, bug writing, test report format |

---

## Files Generated As Stubs (folder placeholders)

Source code folders under `src/` and `tests/` will be created with minimal `.csproj` stubs. No production or test code is generated — that is the responsibility of the DEV agent during user story implementation. Each component folder gets a `README.md` stub describing its role.

`03_Delivery/` subfolders get a `README.md` placeholder only.

Agent `skills/` subfolders are created empty (`.gitkeep`) — skills are added by each agent as the project progresses.

---

## Orchestrator Workflow — Mandatory Phase Sequence

The Orchestrator CLAUDE.md will enforce the following strict phase sequence. **No phase may begin before the previous one is approved by Michael.**

### Phase 0 — Project Bootstrap (this activity)
- Input documents in `01_Intend_and_Constraints` are complete.
- Project structure under `02_Implementation` has been generated.
- **Gate**: Michael approves the generated structure before Phase 1 starts.

### Phase 1 — PRO: General Product Requirements
The Orchestrator instructs PRO to produce:
- `docs/requirements/general-product-requirements.md` — detailed functional requirements covering all components, personas, use cases, and acceptance criteria derived from `01_Intend_and_Constraints`.

**Gate**: Michael reviews and approves the General Product Requirements. No ARC work starts until approved.

### Phase 2 — ARC: Foundation Architecture
The Orchestrator instructs ARC to produce (in order):
1. `docs/architecture/arc42/system-architecture.md` — full arc42 system architecture based on General Product Requirements
2. `docs/architecture/decisions/ADR-001-...` and further ADRs — all technology and design decisions
3. `docs/architecture/product-design-principles.md` — design principles and guardrails for DEV
4. `docs/architecture/product-coding-guidelines.md` — .NET coding guidelines and patterns DEV must follow

**Gate**: Michael reviews and approves all ARC documents. No DEV work (no code) starts until approved.

### Phase 3+ — Iterative User Story Implementation
Only after Phases 1 and 2 are approved does the Orchestrator begin the iterative loop:

```
PRO writes User Story → Michael approves →
ARC writes Implementation Plan → Michael approves →
DEV implements + unit tests → Michael approves →
TES writes E2E tests + test report → Michael approves →
(repeat for next user story)
```

**The Orchestrator must block DEV from writing any production code until Phase 2 is fully approved.**

---

## Key Design Decisions in This Structure

1. **API-first**: `EagleEye.Shared/Contracts/` is the single source of truth for the SignalR interface between the Windows service and all clients. All components depend on this, not on each other.

2. **Separation of concerns**: Each functional area in `EagleEye.Service` is its own subfolder/component (Monitoring, Enforcement, Configuration, Statistics, Communication, Certificates). This maps to the component architecture requirement.

3. **Agents are co-located with implementation**: The `agents/` folder lives inside `02_Implementation` so each agent has direct access to the code, docs, and tests it needs to work on.

4. **Single CLAUDE.md at root = Orchestrator**: The root-level `CLAUDE.md` is the Dev Process Orchestrator. Michael interacts with this agent to drive the overall workflow. The other agents are invoked from their own subfolders.

5. **Docs are first-class**: `docs/` lives alongside `src/` and `tests/` — not as an afterthought. Architecture, decisions, user stories, and test reports all have a defined home.

6. **Scripts are the CI**: PowerShell scripts are the only automation mechanism. VSCode tasks wrap them for convenience.

7. **Secrets management**: All credentials live in `secrets/secrets.json` at repo root — gitignored, never committed. A `secrets/secrets.template.json` is committed showing the required keys with empty/placeholder values. PowerShell scripts load secrets from `secrets/secrets.json` at runtime. The template covers: GitHub credentials, Apple Developer account, Android signing keystore, and any future credentials. If `secrets.json` is missing, scripts fail with a clear error pointing to the template.

---

## Open Questions Before Generation

None at this point — all required information has been gathered. Ready for approval.

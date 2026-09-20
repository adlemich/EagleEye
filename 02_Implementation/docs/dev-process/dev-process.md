# EagleEye — Development Process

*Status: Active*
*Audience: DEV agent, TES agent*
*Owner: Orchestrator*

---

## 1. Overview

EagleEye follows a gate-driven, agent-based development process. No code is written without approved requirements and an approved implementation plan. Every artifact passes through Michael's review before downstream work begins.

This document defines the rules, workflows, and conventions the DEV agent must follow when implementing user stories.

---

## 2. Agent Roles and Handoffs

| Agent | Responsibility | Output |
|-------|---------------|--------|
| **PRO** (Product Owner) | Requirements, user stories | `docs/requirements/` |
| **ARC** (Architect) | Architecture, ADRs, implementation plans | `docs/architecture/`, `docs/requirements/user-stories/US-XXX/implementation-plan.md` |
| **DEV** (Developer) | Production code, unit tests | `src/`, `tests/` |
| **TES** (E2E Tester) | E2E tests, test reports, bug reports | `tests/`, `docs/test-reports/` |

Agents communicate exclusively through files under `02_Implementation/docs/`. There is no direct agent-to-agent communication. Each handoff requires Michael's explicit approval.

---

## 3. User Story Lifecycle

Each user story progresses through these stages:

```
PRO writes User Story (US-XXX)         -> Michael approves
ARC writes Implementation Plan (US-XXX) -> Michael approves
DEV implements + unit tests             -> Michael approves
TES writes E2E tests + test report      -> Michael approves
```

**Status values**: `New` -> `Analyzed` -> `Implemented` -> `Verified/Closed`

### 3.1 DEV Entry Criteria

Before DEV writes any code for a user story, ALL of the following must be true:

1. Phase 2 (Foundation Architecture) is fully approved by Michael.
2. The user story (`US-XXX/user-story.md`) is approved by Michael.
3. The implementation plan (`US-XXX/implementation-plan.md`) is approved by Michael.

**ABSOLUTE RULE: DEV must not write any production code until the implementation plan is approved.**

### 3.2 DEV Exit Criteria

Before DEV presents work as complete:

1. All code specified in the implementation plan is written (or justified deviations are documented).
2. Every production class has corresponding unit tests with 100% branch coverage.
3. All unit tests pass (`pwsh scripts/test.ps1`).
4. Code compiles without warnings (`pwsh scripts/build.ps1`).
5. No secrets are hardcoded anywhere in the code.
6. An implementation report is written at `US-XXX/implementation-report.md`, documenting what was built, any deviations from ARC's plan with reasoning, and open questions.
7. The user story status is updated to `Implemented`.

---

## 4. Git Workflow

### 4.1 Branching Strategy

- **Trunk-based development** on `main`.
- Short-lived feature branches per user story: `feature/US-XXX-short-description`.
- Feature branches are merged back to `main` promptly after approval.

### 4.2 Commit Conventions

- Commit messages must be clear and reference the user story: `US-XXX: <what changed and why>`.
- No commits containing secrets, credentials, or tokens.
- No commented-out code in committed files.

### 4.3 Branch Lifecycle

1. DEV creates `feature/US-XXX-...` from `main` before starting implementation.
2. DEV commits incrementally as implementation progresses.
3. After Michael approves the implementation, the branch is merged to `main`.
4. The feature branch is deleted after merge.

---

## 5. Implementation Rules

### 5.1 Follow the Plan — or Justify Deviations

DEV follows ARC's implementation plan by default. The plan specifies:

- Which files to create or modify
- The order of implementation steps
- Interface contracts and class responsibilities

DEV may deviate from the plan when existing code or concrete technical knowledge yields a better solution. Every deviation must be documented with reasoning in the implementation report (`US-XXX/implementation-report.md`). Deviations must be grounded in concrete facts (e.g., existing code already provides the abstraction, a library API differs from what the plan assumed), not subjective preference.

### 5.2 API-First

If the user story requires SignalR interface changes:

1. Update `EagleEye.Shared/Contracts/` **first**.
2. Then implement the server-side hub methods.
3. Then implement the client-side consumption.

The shared contracts are the single source of truth for the API.

### 5.3 One Story at a Time

DEV fully completes the current user story before starting the next. No parallel story work. No "while I'm here" changes to unrelated code.

### 5.4 No Unsolicited Changes

- Do not refactor code outside the scope of the current user story.
- Do not add features not specified in the implementation plan.
- Do not "improve" existing code that is not part of the current story.
- Do not add speculative abstractions for hypothetical future requirements.

---

## 6. Project Structure

### 6.1 Solution Components

| Component | Purpose | Runs as |
|-----------|---------|---------|
| `EagleEye.Service` | Windows service — process monitoring, app enforcement, SignalR hub | SYSTEM account |
| `EagleEye.TrayClient` | Windows tray app — remaining time display, notifications | Kid's user session |
| `EagleEye.ParentApp` | MAUI parent app — remote configuration and statistics | iOS, Android, macOS |
| `EagleEye.Shared` | Shared library — SignalR API contracts, domain models | Referenced by all |

### 6.2 Dependency Rule

Components may only depend on `EagleEye.Shared`. No direct `ProjectReference` between top-level components:

```
EagleEye.Service    -> EagleEye.Shared  (OK)
EagleEye.TrayClient -> EagleEye.Shared  (OK)
EagleEye.ParentApp  -> EagleEye.Shared  (OK)
EagleEye.Service    -> EagleEye.ParentApp (FORBIDDEN)
```

### 6.3 File Placement

- New classes go in the correct component subfolder (e.g., `EagleEye.Service/Monitoring/`).
- Shared types (DTOs, hub interfaces) go in `EagleEye.Shared/Contracts/` or `EagleEye.Shared/Models/`.
- Namespace must match folder path: `EagleEye.Service.Monitoring`, `EagleEye.Shared.Contracts`.
- One class per file. File name matches class name.

---

## 7. Technology Stack

| Area | Technology | Version |
|------|-----------|---------|
| Language | C# | 13 (.NET 10) |
| Runtime | .NET | 10 (LTS) |
| Windows service | `Microsoft.Extensions.Hosting.WindowsServices` | .NET 10 |
| Cross-platform UI | .NET MAUI | .NET 10 |
| Communication | SignalR (`Microsoft.AspNetCore.SignalR`) | .NET 10 |
| Unit testing | xUnit + Moq | Latest |
| Build/automation | PowerShell | 7.6 |
| Windows installer | Inno Setup | Latest |

---

## 8. Build and Test

### 8.1 Scripts

All automation is via PowerShell 7.6 scripts in `02_Implementation/scripts/`:

| Script | Purpose |
|--------|---------|
| `build.ps1` | Build all projects |
| `test.ps1` | Run all unit tests |
| `package-windows.ps1` | Package Windows installer |
| `package-macos.ps1` | Package macOS app |

### 8.2 Development Environment

- **Development machine**: MacBook (macOS 26+), VSCode.
- **Windows testing**: Windows 11 x64 VM under VMware Fusion.
- **macOS parent app**: runs on the host Mac, connects to the service in the VM over LAN.
- **No CI/CD pipeline**. All builds and tests run locally.

### 8.3 Verification Before Handoff

Before presenting work as complete, DEV must:

1. Run `pwsh scripts/build.ps1` — zero warnings, zero errors.
2. Run `pwsh scripts/test.ps1` — all tests pass.
3. Verify no secrets are present in any source file.

---

## 9. Unit Test Standards

### 9.1 Structure

- Test project mirrors production project: `EagleEye.Service` -> `EagleEye.Service.Tests`.
- Test class mirrors production class: `ProcessMonitor` -> `ProcessMonitorTests`.
- 100% branch coverage for all production classes.

### 9.2 Naming

- Test method pattern: `MethodName_Scenario_ExpectedResult`
- Example: `EnforceRules_WhenAppNotAllowed_KillsProcess`

### 9.3 Rules

- One `[Fact]` or `[Theory]` per logical scenario.
- **Arrange / Act / Assert** structure; one assertion per test where possible.
- Use Moq for all external dependencies and interfaces.
- No test depends on another test's state.
- Unit tests must run without external services, files, network, or Windows-specific APIs (mock those).
- Tests must be deterministic and fast.

---

## 10. Clean Code Standards

- Classes: single responsibility; maximum ~200 lines.
- Methods: maximum ~30 lines; single level of abstraction per method.
- No magic numbers or strings — use constants or configuration.
- All public APIs have XML documentation comments (`/// <summary>`).
- Async/await throughout for all I/O and long-running operations.
- Nullable reference types enabled (`<Nullable>enable</Nullable>`) — no `!` suppression without a justifying comment.
- No commented-out code committed to git.

---

## 11. Security

- **No secrets in code.** Never hardcode credentials, tokens, keys, or passwords. Load from configuration or dependency injection only.
- Credentials live in `secrets/secrets.json` (git-ignored). Never log, commit, display, or echo their contents.
- Self-signed TLS certificates are auto-generated on first run. No manual certificate management for end users.

---

## 12. Definition of Done (DoD)

A user story may only be set to `Verified/Closed` when **all** of the following criteria are met:

### 12.1 Documentation

- Implementation documentation exists that describes how the user story was implemented (design decisions, component interactions, notable trade-offs).
- All existing documentation (system architecture, ADRs, coding guidelines, API contracts) is updated to reflect the implementation. No document may contradict the current state of the code.
- The user story file (`US-XXX/user-story.md`) is up to date with final acceptance criteria outcomes.

### 12.2 Testing

- E2E test cases have been created by TES and added to the test suites.
- All unit tests pass (`pwsh scripts/test.ps1`).
- All E2E tests pass and a test report has been generated in `docs/test-reports/`.
- Test results prove correct implementation of every acceptance criterion in the user story.

### 12.3 Human Acceptance

- Michael has performed a manual test of the implemented feature on the target environment (Windows 11 VM + macOS parent app where applicable).
- Michael has explicitly approved the user story as complete.

**No agent may close a user story. Only Michael can.**

---

## 13. Issue and Bug Tracking

Issues discovered during development or testing are tracked as markdown files:

- **Issue status values**: `New` -> `Analyzed` -> `Implemented` -> `Verified/Closed`
- Issues reference the originating user story.
- DEV fixes issues related to the current story before marking it `Implemented`.
- Issues outside the current story scope are logged but not fixed — they become input for future stories.

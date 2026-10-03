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
| **DEV** (Developer) | Production code, unit tests, build scripts | `src/`, `tests/`, `scripts/` |
| **TES** (Manual Test Lead) | Manual test plans and checklists, result evaluation, test reports, issues | `docs/testing/`, `docs/requirements/user-stories/US-XXX/issues/` |

Agents communicate exclusively through files under `02_Implementation/docs/`. There is no direct agent-to-agent communication. Each handoff requires Michael's explicit approval.

Development happens on two machines (Windows Developer Machine and MacBook). Which machine does what is defined in `02_Implementation/docs/dev-process/dev-environments.md` (ADR-007).

---

## 3. User Story Lifecycle

Each user story progresses through these stages:

```
PRO writes User Story (US-XXX)                    -> Michael approves
ARC writes Implementation Plan (US-XXX)           -> Michael approves
DEV implements + unit tests + smoke check         -> Michael approves
TES writes manual test plan                       -> Michael approves
Michael executes test run(s), records results
TES evaluates, writes test report + issues        -> Michael approves, closes story
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
3. All unit tests pass (`pwsh scripts/test.ps1`) on every machine that built part of the story.
4. Code compiles without warnings (`pwsh scripts/build.ps1`) on every machine that built part of the story.
5. No secrets are hardcoded anywhere in the code.
6. The testable artifact (installer, app build) is produced and smoke-checked on its target machine.
7. An implementation report is written at `US-XXX/implementation-report.md`, documenting what was built, any deviations from ARC's plan with reasoning, open questions, the machines used, and a **"How to test"** section for TES.
8. All work is committed and pushed on the story's feature branch (§4).
9. The user story status is updated to `Implemented`.

---

## 4. Git Workflow: One Feature Branch per User Story

*Binding for PRO, ARC, DEV and TES since 2026-10-03 (Michael's decision). US-001 predates this rule and was done on `main`.*

### 4.1 The Rule

- **Every user story is developed on its own feature branch**, named after the story:
  `feature/US-XXX-<short-title>`: the story ID plus a short kebab-case title, e.g. `feature/US-002-app-allow-list`.
- **All artifacts of the story** are committed on that branch, from PRO to TES: user story, implementation plan, ADRs and architecture updates, code, unit tests, scripts, implementation report, test plan, test runs, test report, issues.
- **`main` only receives a story once it is complete**, i.e. when Michael has set it to `Verified/Closed`. Nothing of an open story is committed to `main`.
- **One story at a time**: at most one open feature branch.
- The branch is pushed to `origin`, so that the Windows machine and the MacBook work on the same branch.

### 4.2 Branch Lifecycle

| Step | Who | Git actions |
|---|---|---|
| 1. Start story | **PRO**, before writing `user-story.md` | `git switch main` → `git pull` → `git switch -c feature/US-XXX-<short-title>` → (write and commit the story) → `git push -u origin feature/US-XXX-<short-title>` |
| 2. Work on story | **ARC, DEV, TES** (and PRO for story changes), on either machine | `git fetch` → `git switch feature/US-XXX-<short-title>` → `git pull` → work → commit → `git push` |
| 3. Switch machines | whoever is working | Commit and push on the branch; on the other machine `git switch` to the branch and `git pull` |
| 4. Story closed | Michael sets `Verified/Closed` (TES records it on the branch) | — |
| 5. Merge | **Orchestrator**, only with Michael's explicit go | `git switch main` → `git pull` → `git merge --no-ff feature/US-XXX-<short-title> -m "Merge US-XXX: <title>"` → `git push` → delete the branch locally (`git branch -d …`) and on `origin` (`git push origin --delete …`) |

### 4.3 Rules for Every Agent

1. **Check the branch before any story work.** If the current branch is not the story's feature branch, stop and switch (or ask Michael). Never commit story work to `main`.
2. **If `main` changes while a story branch is open** (process or setup changes), bring it in with `git merge main` on the feature branch. **Do not rebase**: the branch is shared between two machines.
3. **Work that does not belong to a user story** (process, setup or documentation fixes requested by Michael) is committed directly to `main` by the Orchestrator.
4. **Merge conflicts** are resolved on the feature branch, never on `main`.

### 4.4 Commit Conventions

- Commit messages must be clear and reference the user story: `US-XXX: <what changed and why>`.
- No commits containing secrets, credentials, or tokens.
- No commented-out code in committed files.

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
| `EagleEye.ParentApp` | MAUI parent app — remote configuration and statistics | Windows, Android, macOS (iOS later) |
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
| Language | C# | 14 (.NET 10) |
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
| `env-check.ps1` | Show host machine, git sync state, toolchain |
| `build.ps1` | Build the projects that belong to the current host |
| `test.ps1` | Run the unit tests that belong to the current host |
| `package-windows.ps1` | Package Windows installer (Windows machine only) |
| `package-macos.ps1` | Package macOS app (MacBook only) |
| `plantuml.ps1` | Start/stop the local PlantUML server |

### 8.2 Development Environment

Two machines, one repository. See `02_Implementation/docs/dev-process/dev-environments.md`.

- **Windows Developer Machine** (Windows 11): Service, TrayClient, Shared, ParentApp Windows + Android targets, installer. Also the manual test station.
- **MacBook** (macOS 26+): ParentApp Mac Catalyst target, `.dmg`.
- **No CI/CD pipeline**. All builds and tests run locally.

### 8.3 Verification Before Handoff

Before presenting work as complete, DEV must, on each machine involved:

1. Run `pwsh scripts/build.ps1` — zero warnings, zero errors.
2. Run `pwsh scripts/test.ps1` — all tests pass.
3. Verify no secrets are present in any source file.
4. Produce the testable artifact and smoke-check it (it installs/starts).

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

- All unit tests pass (`pwsh scripts/test.ps1`) on every machine involved.
- TES has written a test plan (`docs/testing/US-XXX/test-plan.md`) approved by Michael, covering every acceptance criterion.
- Michael has executed the manual test run(s) (`docs/testing/US-XXX/test-run-NN.md`) including the regression checklist.
- TES's test report (`docs/testing/US-XXX/test-report.md`) shows every AC passed and no open Critical/High issues.

### 12.3 Human Acceptance

- Michael has approved the test report.
- Michael has explicitly approved the user story as complete.

**No agent may close a user story. Only Michael can.**

---

## 13. Issue and Bug Tracking

Issues discovered during development or testing are tracked as markdown files in `docs/requirements/user-stories/US-XXX/issues/` (format: `agents/tes/CLAUDE.md`):

- **Issue status values**: `New` -> `Analyzed` -> `Implemented` -> `Verified/Closed`
- Issues reference the originating user story.
- DEV fixes issues related to the current story before marking it `Implemented`.
- Issues outside the current story scope are logged but not fixed — they become input for future stories.

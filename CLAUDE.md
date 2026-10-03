# EagleEye — Dev Process Orchestrator

> **Agent sessions read this first.** Claude Code loads this file in *every* session inside the repo, including PRO, ARC, DEV and TES sessions started in `02_Implementation/agents/<role>/`.
>
> - The sections **"Rules for All Agents"** and **"Where You Run"** apply to **every** session.
> - Everything under **"Orchestrator Role"** applies **only** when the session was started at the repo root. If your working directory is `02_Implementation/agents/<role>/`, you are that agent: follow its `CLAUDE.md` and ignore the Orchestrator Role section.

---

## Project Context

EagleEye is a parental control solution consisting of:

| Component | Description | Built on |
|-----------|-------------|----------|
| `EagleEye.Service` | Windows service (SYSTEM): process monitoring, app enforcement, SignalR hub | Windows machine |
| `EagleEye.TrayClient` | Windows tray app (kid's session): remaining time display, notifications | Windows machine |
| `EagleEye.ParentApp` | MAUI parent app, one codebase. **Android + iOS** = production platforms; **Windows** client (copy-deployed exe) = initial testing vehicle; **macOS** desktop | Windows: Windows + Android targets · MacBook: macOS + iOS targets |
| `EagleEye.Shared` | Shared library: SignalR API contracts, domain models (API-first) | both |

**Technology**: .NET 10, MAUI, SignalR, PowerShell 7.6, Inno Setup, VSCode
**Repository**: `https://github.com/adlemich/EagleEye.git`, branch `main` (credentials in `secrets/secrets.json`, one copy per machine)

---

## Where You Run (all agents)

Development happens on **two machines sharing this git repo**. The full rules are in `02_Implementation/docs/dev-process/dev-environments.md`; read it at the start of any session that builds, tests or changes code.

| Host | Detect via | Responsible for |
|------|-----------|-----------------|
| **Windows Developer Machine** | `Platform: win32` / `$IsWindows` | Service, TrayClient, Shared, ParentApp **Windows + Android** targets, Windows installer, **all manual testing** |
| **MacBook** | `Platform: darwin` / `$IsMacOS` | ParentApp **macOS** target (Mac Catalyst), **iOS** target, `.dmg` packaging |

1. **Know your host before acting.** Check the platform in your environment info, or run `pwsh 02_Implementation/scripts/env-check.ps1`. State the host when you report what you are about to do.
2. **Do not attempt the other machine's work.** If a task needs the other machine, say so. Make sure the work is committed, and tell Michael exactly what to run there.
3. **Sync through git only.** Check sync state with `origin/main` at session start. Before Michael switches machines, everything must be committed (and pushed if he asks).
4. Documentation work (requirements, architecture, plans, test plans) may be done on either machine.

---

## Rules for All Agents

1. **Scope.** Never read or write files outside the git repository root (`git rev-parse --show-toplevel`). Never hard-code absolute paths. Use repo-relative paths in all docs, scripts and code.
2. **Secrets stay local.** Credentials live in `secrets/secrets.json` (git-ignored, one per machine). Never log, commit, display or echo them.
3. **No internet access** unless Michael explicitly grants it for a specific task.
4. **Log everything.** All outputs, decisions, issues and resolutions are stored under `02_Implementation/docs/`.
5. **Reference artifacts by repo-relative path.** Never say "the document". Say `02_Implementation/docs/...`.
6. **Never skip a gate.** Each step waits for Michael's explicit approval (see the workflow below).
7. **Testing is manual.** Acceptance and E2E testing are executed by Michael, guided by TES (`02_Implementation/docs/testing/README.md`). Nobody writes automated E2E tests. DEV still writes automated unit tests.
8. **One feature branch per user story.** Every user story is developed on its own branch `feature/US-XXX-<short-title>`, from PRO's first commit to TES's last. All story artifacts (story, plan, code, tests, test runs, issues) are committed there, never on `main`. The branch is merged into `main` (`--no-ff`) only after Michael has closed the story and given the go to merge. Before any story work, check that you are on the story's branch. Full rules: `02_Implementation/docs/dev-process/dev-process.md` §4. Work that does not belong to a story goes directly to `main` (Orchestrator only).

---

## Orchestrator Role

*Applies only to sessions started at the repo root.*

You are the **Dev Process Orchestrator** for EagleEye. You guide Michael through the development process step by step, coordinate the agent team, and enforce workflow gates so that quality and alignment are confirmed before any work proceeds.

### Model

Use model: `claude-opus-5`

### Agent Team

| Agent | CLAUDE.md | Responsibility |
|-------|-----------|----------------|
| PRO — Product Owner | `02_Implementation/agents/pro/CLAUDE.md` | Requirements, user stories |
| ARC — Architect | `02_Implementation/agents/arc/CLAUDE.md` | Architecture, ADRs, implementation plans |
| DEV — Developer | `02_Implementation/agents/dev/CLAUDE.md` | Production code, unit tests, build scripts |
| TES — Manual Test Lead | `02_Implementation/agents/tes/CLAUDE.md` | Manual test plans, checklists, result evaluation, test reports, issues |

To invoke an agent: open a new Claude Code session in that agent's subfolder, on the machine that matches the work (see "Where You Run").

### Workflow Phases

- **Phase 0 (Bootstrap)**: complete.
- **Phase 1 (PRO: General Product Requirements)**: complete, approved.
- **Phase 2 (ARC: Foundation Architecture)**: complete, approved. Output: `02_Implementation/docs/architecture/` (arc42, ADRs, coding guidelines).
  **ABSOLUTE RULE: DEV must not write production code for a story until its implementation plan is approved.**
- **Phase 3+ (iterative user story implementation)**: active.

For each user story:

```
0. PRO creates the branch feature/US-XXX-<short-title> from main
a. PRO writes user story (US-XXX)                         → Michael approves
b. ARC writes implementation plan (US-XXX)                → Michael approves
   (the plan states which machine(s) each step runs on)
c. DEV implements + unit tests, smoke-checks the build    → Michael approves
d. TES writes the manual test plan                        → Michael approves
e. Michael executes the test run(s), records results
f. TES evaluates, writes test report + issues             → Michael approves
   (issues loop back to c; unclear requirements go to PRO via ARC)
g. Michael closes the story
h. Orchestrator merges the feature branch into main       → only on Michael's go
   (git merge --no-ff, push, delete the branch). Repeat with the next story.
```

Steps a to g all happen on the story's feature branch.

User story and issue status values: `New` → `Analyzed` → `Implemented` → `Verified/Closed`. Only Michael closes a story.

### Orchestrator Rules

1. **State your position.** Always tell Michael the current phase, the current step, the host you are on, the current git branch, and the next action.
2. **Route work to the right machine.** When the next step belongs to the other machine, say so explicitly, together with the handoff instructions.
3. **Support manual testing.** When a story reaches step d, make sure TES produces a test plan and a test-run checklist, and remind Michael how to record results (`02_Implementation/docs/testing/README.md` §4).

### Starting a Session

Greet Michael, then state the host machine, the git sync state, the current phase and step, and the next action. Ask what he wants to do next.

### Memory

Persistent orchestrator memory lives in the project, not in `~/.claude`:

- Index: `02_Implementation/agents/orchestrator/memory/MEMORY.md`, one pointer line per memory.
- Content: one fact per file in `02_Implementation/agents/orchestrator/memory/`.

Read the index at the start of every session and open the memories relevant to the task. Record new durable facts (decisions, constraints, Michael's feedback, phase status) as new files there plus a pointer line in the index. When a file becomes stale, update or delete it rather than adding a duplicate.

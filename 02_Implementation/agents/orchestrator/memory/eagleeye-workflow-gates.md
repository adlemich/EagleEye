---
name: eagleeye-workflow-gates
description: How the EagleEye agent workflow, gates and agent invocation actually work (manual testing since 2026-10-03)
metadata:
  type: project
---

Five agents: Orchestrator (root `CLAUDE.md`, this role), PRO, ARC, DEV, TES. Each has its own `CLAUDE.md` under `02_Implementation/agents/<name>/`. An agent is invoked by opening a NEW Claude Code session in that agent's subfolder, on the machine that matches the work. Agents hand off only through markdown files under `02_Implementation/docs/`.

Claude Code also loads the root `CLAUDE.md` in agent sessions. Its header therefore says that "Rules for All Agents" and "Where You Run" apply everywhere, while "Orchestrator Role" applies only to root sessions.

Per-story loop since 2026-10-03: PRO story → ARC plan (with machine assignment) → DEV code + unit tests + smoke check → TES manual test plan → Michael executes the test run (ticks results in `docs/testing/US-XXX/test-run-NN.md`) → TES test report + issues → Michael closes. Every arrow is an explicit approval from Michael. Only Michael closes stories. Each story runs on its own branch `feature/US-XXX-<short-title>` (PRO creates it; ARC, DEV and TES commit there); the Orchestrator merges it into `main` with `--no-ff` only after Michael closed the story and gave the go (Michael, 2026-10-03; US-001 predates the rule). Rules: `dev-process.md` §4.

**Why:** the whole point of the setup is that Michael reviews before work compounds on unreviewed work.

**How to apply:** always state the host machine, the current phase and step, and the next action. Reference artifacts by repo-relative path. Never skip a gate. Log every decision under `02_Implementation/docs/`. Status vocabulary: New → Analyzed → Implemented → Verified/Closed. See [[eagleeye-current-phase]], [[eagleeye-dev-test-setup]].

---
name: eagleeye-workflow-gates
description: How the EagleEye agent workflow, gates and agent invocation actually work
metadata:
  type: project
---

Five agents: Orchestrator (root `CLAUDE.md`, this role), PRO, ARC, DEV, TES — each with its own `CLAUDE.md` under `02_Implementation/agents/<name>/`. An agent is invoked by opening a NEW Claude Code session in that agent's subfolder, not by spawning a subagent from here. Agents hand work off only through markdown files under `02_Implementation/docs/`.

Gate sequence: Phase 1 PRO requirements → Phase 2 ARC (arc42 architecture, ADRs, design principles, coding guidelines) → Phase 3+ per-story loop: PRO user story → ARC implementation plan → DEV code + unit tests → TES E2E tests + HTML test report + issues. Each arrow is an explicit approval from Michael. Absolute rule: DEV writes no production code until Phase 2 is fully approved.

**Why:** the whole point of the setup is that Michael reviews before work compounds on unreviewed work.

**How to apply:** always state the current phase and next action, reference artifacts by full path, never skip a gate, and log every decision/issue/resolution under `02_Implementation/docs/`. Status vocabulary for stories and issues: New → Analyzed → Implemented → Verified/Closed. See [[eagleeye-current-phase]].

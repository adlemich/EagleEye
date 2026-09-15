---
name: memory-lives-in-project
description: Michael requires orchestrator memory to be stored inside the project folder, not in ~/.claude
metadata:
  type: feedback
---

Persistent memory for this project must live inside the project folder structure, at `02_Implementation/agents/orchestrator/memory/` with its index at `.../memory/MEMORY.md`. The default harness location (`~/.claude/projects/.../memory/`) only holds a pointer file that redirects here.

**Why:** Michael asked for it explicitly on 2026-09-15, and the project's own CLAUDE.md rule 6 forbids reading or writing anything outside `/Users/micha/Documents/App-Development/EagleEyeParentalControl`. Keeping memory in-project also means it is versioned with the repo and readable by the PRO/ARC/DEV/TES agent sessions, which each run in their own subfolder.

**How to apply:** Write every new memory file into the project memory folder and add its pointer line to the project `MEMORY.md`. Never create memory content under `~/.claude`.

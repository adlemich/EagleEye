# EagleEye — Orchestrator Memory Index

One line per memory. Content lives in the linked files, never here.

- [Michael — project owner](michael-project-owner.md) — professional engineer, reviewer and sole approval authority; works on a Windows machine and a MacBook.
- [Memory lives in the project](memory-lives-in-project.md) — memory files belong in this folder, never under ~/.claude.
- [Current phase](eagleeye-current-phase.md) — 2026-10-31: US-009 step c: Steps 1–3 + Michael check done; Step 4 Win10 PC pending, then H3 MacBook Step 5.
- [US-001 lessons](us001-lessons.md) — localization in every plan, ACs quote English examples, German UI labels in test manuals, publisher "Michael Adler".
- [Workflow and gates](eagleeye-workflow-gates.md) — five agents, file-based handoffs, approval gates, manual test loop, one feature branch per story merged on close.
- [Dev and test setup](eagleeye-dev-test-setup.md) — Windows: service/tray/Windows parent app + manual testing; MacBook: Mac app only (repo outside iCloud); ADR-007.
- [Locked product decisions](eagleeye-product-decisions.md) — settled answers on connectivity, enforcement, time model, packaging; parent app = Windows + Mac only since 2026-10-20; don't re-ask.
- [Git setup](eagleeye-git-setup.md) — repo on GitHub, MIT licensed; Windows pushes via Git Credential Manager (no secrets.json there); PAT pushes need username `x-access-token`; MacBook: plain push via Keychain works.
- [Secrets handling](eagleeye-secrets-handling.md) — credentials in secrets/secrets.json (one per machine), never logged or committed.
- [Michael closes with partial runs](michael-closes-with-partial-runs.md) — honour quick closes, but record untested ACs in the test report before merging.
- [Catalyst unset background](maui-catalyst-unset-background.md) — Mac Catalyst keeps the old colour when a background goes back to unset; verify visual Mac fixes with a probe + screencapture first.
- [Mac Local Network after rebuild](mac-local-network-after-rebuild.md) — rebuilt ad-hoc .dmg can't connect until the "Lokales Netzwerk" switch is toggled off/on; ping proves nothing.

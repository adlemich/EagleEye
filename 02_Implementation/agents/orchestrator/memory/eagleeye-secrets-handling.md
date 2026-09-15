---
name: eagleeye-secrets-handling
description: Where EagleEye credentials live and the rule for handling them
metadata:
  type: reference
---

GitHub credentials for `https://github.com/adlemich/EagleEye.git` live in `secrets/secrets.json` at the repo root, created from `secrets/secrets.template.json`. The file is git-ignored.

Hard rule from the project CLAUDE.md: never log, commit, display or echo its contents — not in chat, not in scripts, not in log files. Use it only by pointing tooling at the path.

---
name: eagleeye-git-setup
description: EagleEye git repo state and the auth quirk needed to push to GitHub
metadata:
  type: project
---

The repo was initialized and pushed on 2026-09-15. `/Users/micha/Documents/App-Development/EagleEyeParentalControl` is the git root, branch `main` tracks `origin/main` at `https://github.com/adlemich/EagleEye.git`. History: Michael's `Initial commit` (LICENSE only) → the bootstrap commit with all 81 project files. Licensed MIT; `LICENSE` is GitHub's MIT template and `README.md` states it.

**Auth quirk — this cost a failed push:** the credential in `secrets/secrets.json` is a GitHub **fine-grained** PAT. Pushing with `username: adlemich` (the value stored in the file) fails with *"Invalid username or token. Password authentication is not supported for Git operations."* Pushing with username `x-access-token` and the PAT as password succeeds.

**How to apply:** for any HTTPS push/fetch needing auth, feed git `username=x-access-token` and `password=<personalAccessToken>` via a `credential.helper` script that reads the JSON — never echo, log or inline the token. Also note `git ls-remote` succeeds anonymously on this repo, so it does NOT prove the token works; only a push does. See [[eagleeye-secrets-handling]].

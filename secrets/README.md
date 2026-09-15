# Secrets

This folder stores local credentials. **Only `secrets.template.json` is committed to git.**

## Setup

```bash
cp secrets/secrets.template.json secrets/secrets.json
```

Then edit `secrets/secrets.json` and fill in your values.

## Rules

- `secrets.json` is gitignored and must **never** be committed.
- Never log, print, or display secrets in scripts or agent output.
- PowerShell scripts load secrets from `secrets.json` at runtime and fail fast if the file is missing.
- If you need to share a new required secret with other developers, add the key (with an empty value and a `_comment`) to `secrets.template.json` and commit that.

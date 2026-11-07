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
- No build, test or packaging script needs `secrets.json`. `env-check.ps1` only reports whether it is present. It holds the GitHub credentials for pushing on a machine without another git credential helper.
- If you need to share a new required secret with other developers, add the key (with an empty value and a `_comment`) to `secrets.template.json` and commit that.

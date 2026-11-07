---
name: owner-no-personal-details
description: No personal or home-network details in any tracked file; neutral "the project owner"; placeholders DEV-PC/PILOT-PC/PARENT-PC
metadata:
  type: feedback
---

The project owner asked (2026-11-07) that no file in the repo names them or reveals their home setup: no real names, email, IP addresses, host names, personal Windows account names, family surname in test data, or such details in screenshots. Write "the project owner" (they/them). Use the placeholders `DEV-PC`, `PILOT-PC`, `PARENT-PC`, `UserA`/`UserB` (dev-environments.md §1) and example addresses from 192.0.2.0/24; test data uses "Mustermann" names. Kept as product facts: publisher/copyright "Michael Adler" in installers, Info.plist, Directory.Build.props, LICENSE, README.

**Why:** the repo is on GitHub; the owner does not want personal or home-network information in it.

**How to apply:** check every doc, report, test record, code comment and screenshot before committing (CLAUDE.md rule 9). Redact screenshots (OCR + overpaint worked with a Swift Vision script; small screenshots need upscaling before OCR). Git history still contains the old texts unless the owner decides to rewrite it. See [[project-owner]].

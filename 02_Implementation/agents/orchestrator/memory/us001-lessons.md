---
name: us001-lessons
description: Lessons from US-001 that every future story must apply — localization in every plan, German UI labels in test manuals, Michael's naming preferences
metadata:
  type: feedback
---

Lessons from US-001 (2026-10-03):

- **Localization is never implicit.** NFR-L-010..012 (German default, English second, all texts externalized) applied to US-001, but the plan did not mention it, so DEV shipped English-only texts (ISSUE-001). **How to apply:** every ARC implementation plan lists the localization impact of each user-facing text; DEV puts texts in resx (German neutral, `.en` satellite), as in `EagleEye.TrayClient/Resources/TrayTexts*.resx`.
- **Quoted UI texts in ACs are English examples** (Michael's decision). The actual text follows the user's language. PRO writes ACs this way.
- **Michael's Windows UI is German.** Test manuals give German UI labels (services.msc *Wird ausgeführt / Automatisch / Lokales System*, *Einstellungen → Apps → Installierte Apps*, etc.). He liked step-by-step manuals with one result block per case.
- **Publisher / company name is "Michael Adler"**, not the GitHub handle "adlemich".
- **Michael's German wording preferences**: "App Infos" (not "Über"), "Verbunden", "Verbindungsfehler".
- **The DEV session is not elevated** on the Windows machine, so DEV cannot run the installer. Manual test run 01 must start with install, registration and auto-start.

**Why:** these all caused rework or issues in US-001.

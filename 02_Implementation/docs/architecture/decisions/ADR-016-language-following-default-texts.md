# ADR-016: Default Kid Texts Are Rendered in the Language of the Displaying App

**Status**: Accepted (approved by Michael with the US-006 implementation plan, 2026-10-12; §5 reflects his answer to Q-1)
**Date**: 2026-10-12
**Deciders**: ARC, Michael

---

## Context

The parent sets texts for the kid: the break display text (US-005), the budget text and the two warning texts (US-006). The parent's own texts are never translated. Their **defaults** exist in German and English (US-006 AC-10, AC-14, OQ-8): "supply an English version if the app is installed in English". The two apps that show such a text can run in different languages. The parent app follows the parent's Windows display language. The tray client follows the kid's Windows display language. The service has no UI language: it runs as SYSTEM, and its log is English (arc42 §8.13).

In US-005, the break display text default was a German constant in `EagleEye.Shared` (`BreakTimeRules.DefaultDisplayText`). It was sent to parent apps as text. That cannot follow two languages at once.

---

## Decision

1. **The service stores only the parent's own texts.** A default text is stored as **"no text"** (no row, or `NULL`). DTOs carry the parent's text or `null` (new fields), or a flag `IsDefault…` (existing US-005 field).
2. **Default texts live once, in `EagleEye.Shared`**, as a resource file with German as the neutral language and an English satellite (`Resources/DefaultTexts.resx`, `DefaultTexts.en.resx`). The accessor `DefaultTexts.Get(DefaultTextKind, CultureInfo)` serves the kinds `BreakDisplayText`, `BudgetText`, `EarlyWarningText` and `FinalWarningText`.
3. **The app that shows a text renders its default in its own UI language** (`CultureInfo.CurrentUICulture`):
   - the parent app shows the default in the edit box in the parent's language;
   - the tray client shows the default in the kid dialog in the kid's language. The service sends only the message reason and the parent's text or `null` (`KidMessageDto`, ADR-014 amendment).
4. **A text equal to the default of any supported language is stored as the default.** This applies to empty text, white space only, or exactly the German or the English default after line-break normalization. The text then keeps following the language. Editing the default by even one character makes it the parent's own text, shown exactly as entered (AC-10).
5. **Break display text (US-005)**: the mechanism also applies to it (Michael, plan Q-1). The German default is unchanged. The English default replaces US-005 OQ-14 ("German in every language"): "Hi! Unfortunately, your parents have set a PC break for this time. You can't use this program right now. Sorry. How about using the time to read a book? 😊" (US-006 AC-10, FR-APP-052).
6. The service log never contains the texts (US-005 AC-12, US-006 AC-11). History records do not store them either.

---

## Rationale

Rendering at the edge is the only way two apps with different languages can both show "the default in my language". Keeping the strings in Shared gives one source for both apps and avoids two copies of the same German sentence drifting apart. Shared already builds on both machines and stays free of Windows APIs, so the MacBook is not affected.

### Alternatives Considered

| Option | Reason Rejected |
|---|---|
| The service stores the default in one language, chosen at install (installer language) | The service has no reliable install language (the service installer is per machine, the parent app installer per user). A parent with English Windows and a kid with German Windows would see the same text, so one of them would get the wrong language. |
| The parent app writes its localized default to the service on first use | The first parent app wins. Two parent apps in different languages would fight. The kid's language is never considered. |
| Copies of the defaults in the TrayClient and ParentApp.Core resources | Two copies of every default. They drift apart (the US-001 lesson on localization). |
| A per-account language setting | Not requested. It adds UI and storage. |

---

## Consequences

### Positive

- Every user sees defaults in their own language, while the parent's texts stay exactly as entered.
- One place for the default texts. Adding a language is one satellite file.
- The service stays language-neutral.

### Negative / Trade-offs

- The parent and the kid can see different wording for the same unchanged default. This is by design (plan Q-1).
- Shared gets its first resource file (satellite assembly `en/EagleEye.Shared.resources.dll`). Every installer must ship it. The Windows installers copy the publish output, which includes satellite folders. TES checks the English default in the tray client.

### Neutral

- The US-005 field `AccountRulesDto.DisplayText` keeps carrying the German default for compatibility. New clients use `IsDefaultDisplayText` and `DefaultTexts`.

---

## References

- US-006: `02_Implementation/docs/requirements/user-stories/US-006/user-story.md` (AC-10, AC-14, AC-34, OQ-8) and `implementation-plan.md` (Q-1)
- US-005 OQ-14 (German-only break default), US-001 lessons on localization
- Requirements NFR-L-010 to NFR-L-012, FR-APP-052 to FR-APP-054
- ADR-014, ADR-015, arc42 §8.13

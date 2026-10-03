# ISSUE-003: About dialog could explain the connection error (PRO feedback)

**Status**: Implemented
**User Story**: US-001
**Found in**: docs/testing/US-001/test-run-01.md, TC-001-09 (exploratory)
**Date**: 2026-10-03
**Severity**: Low — **PRO feedback**, not a defect (behaviour unspecified by US-001)
**Machine**: Windows Developer Machine
**Routed to**: PRO (AC-13 added) → DEV (implemented in 0.1.1, at Michael's direction)

## Description

When the service is stopped, the About dialog shows "Server Version: unavailable". Michael found this acceptable (TC-09 = Pass) but suggests more information.

## Suggestion (Michael)

> "It could give more information not only showing 'unavailable' but rather say 'Connection error, could not connect to Server at IP-Port' (in target language)."

## Proposed Handling

PRO decides whether to add an acceptance criterion, either to a follow-up story or as a refinement in the next tray-client story. Example wording for an AC: *"When the service is unreachable, the About dialog states that the connection failed and shows the address the tray client tried to reach (e.g. `localhost:5080`), in the user's language."*

Language-only aspects of this dialog are handled in ISSUE-001.

## Resolution

- **PRO** (2026-10-03, Michael's decision): added AC-13 to `US-001/user-story.md`: *"When the service cannot be reached, the application information dialog shows a connection error that names the server address the tray client tried to reach."*
- **DEV** (2026-10-03, build 0.1.1): the About dialog now shows, instead of "unavailable":
  - de: `Verbindungsfehler: Keine Verbindung zum Server unter localhost:5080 möglich.`
  - en: `Connection error: could not connect to the server at localhost:5080.`

  The address comes from the tray client's connection settings (`IServiceConnection.ServerAddress`). The text is composed by `UI/AboutText` and unit-tested.
- Re-test: `docs/testing/US-001/test-run-02.md`, TC-001-09.

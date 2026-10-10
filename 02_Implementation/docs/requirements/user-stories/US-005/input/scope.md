# US-005 Scope Input: Break Times (Ruhezeiten)

**Source**: Michael, 2026-10-10 (recorded by the Orchestrator, wording kept)
**Mock**: `02_Implementation/docs/requirements/user-stories/US-005/input/us-005_parentApp.png` (parent app UI with requirements 1 to 10 next to it)

## Parent app (from the mock)

1. A new tab view is added, "Rules"/"Regeln", between Settings and Reports.
2. It should have a first section "Break-Times"/"Ruhezeiten" in which the kid shall not use any apps.
3. It should allow to add new entries in a table with an "Add new entry"/"Eintrag hinzufügen" button.
4. Each entry should be editable and deletable (delete via button, edit direct).
5. Each entry can be enabled/disabled.
6. Each entry allows to specify start and end time. During this time the kid shall not use the apps when enabled. (Allowed values 00:00 to 23:59, end time is always greater than start time.)
7. For each entry, the user can specify the days on which the rule shall apply. At least one day must be ticked per entry.
8. The user can specify a custom message, which is displayed to the kid when it tries to start apps in times that are active break times. See the default text in German in the image. It can be changed as a standard edit box and should support emojis.
   Default text (from the mock): "Hi! Leider haben Deine Eltern eine PC-Pause für diese Uhrzeit eingestellt. Du kannst dieses Programm jetzt nicht verwenden. Tut mir leid. Wie wäre es wenn Du die Zeit nutzt, um ein Buch zu lesen? 😊"
9. Entries can be deleted via a trash icon. This is direct (no confirmation dialog).
10. All changes are directly saved and synchronized with the server at once (messaging).

Table columns in the mock: delete (trash icon) · An/Aus · Start-Zeit · End-Zeit · MO · DI · MI · DO · FR · SA · SO. Below the table: "Anzeige Text:" with the message edit box.

## Additions (Michael, 2026-10-10)

- **Per account.** Rules are saved and managed per monitored child account. The mock is missing this: there must be a drop-down at the top to select the account whose rules are shown and edited.
- **Server data model and API.** For each monitored account the server stores the rules shown in the parent app. It needs a data model and APIs to query (display rules) and to create/update (edit rules).
- **Rule states.** Rules may be defined, active or disabled (in general), and in addition active or disabled per weekday.
- **Enforcement.** When a monitored app is started (same rules as for logging usage time: only user applications), the server checks whether any break rule is active for that account at the current time and weekday.
  - If not: the app starts and is usable, and usage time is logged.
  - If a rule is active: the process is terminated right away. Through the tray client, the kid sees the display text configured in the parent app, in a **system-modal message box** (topmost) that must be acknowledged with **OK**.
- **Colours.** The mock may show poor colouring. Use only the standard colours that follow light/dark mode, no special colours. Don't take the mock too seriously in this regard.

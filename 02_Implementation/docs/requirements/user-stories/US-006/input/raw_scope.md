# User Story 006 - Raw Scope

## New feature to define daily time budget for applications

I should be posbile for a parent to specify per monitored account, and for all known applications that were ever started in this account, if that app can be used by the kid and for how long.

## In the parent client application

### In tab "Rules", new section "Application Rules"

- See image "new_rules.png"
- There is a short text, explaining the rules behavior (multi-language) with default text as shown in the image under the header for this section
- The table lists all known and monitored applications for this account. So, for each application that ever had a an entry under "Reports" should be listed here
- Each application has one row.
- Each application can be configured with a maximum dayly time budegt for the kid: Max 23 hours and 59 minutes. There is one input field for the hours and one for minutes (checked for correct ranges 0..23 and 0..59)
- Each application can be configured if that time budget shall be active per week-day. A tick means, the application can be used at that day, up to the configured duration. Not ticked means, the application is blocked on these days.
- The default for newly found applications to monitor is: Dayly budget is 0 hours and 0 minutes and not active at any weekday (non ticked). So new applications are blocked by default
- As with all other configurations, all changes by the user are directly synchronized with the server. The server shall respond with publish of updated configuration so that the orginal parent app gets a confirmation and all other connected parent apps are updated, too.
- In addition, under the table, the parent can configure a text messgage that is displayed to the kid if the time budget for an application is zero for a day. This hits when the configuration is set to 0:00 for an application, or the application was used by the kid and the dayly budget was consumed or there is no tick on the current day so that the budget it not active. In all these cases, when the kids want to start the application, the app is closed (like in US-005) and this text is shown in the dialog box.

### In tab "Settings", new section "Time-elapsed Warnings"

- See image "new_settings.png"
- The EagleEye solution implements two warning levels for the kid. So when it is e.g. playing a game, it gets an early warning to save its progress. And then again a final warning, before the applications are gracefully (then after timeout, hard) closed.
- The parent can configure the time in minutes for both thresholds: Early warning and final warning.
- The parent can configure a text message that is shown to the kid in both cases.
- The parent can not add or change the labels of warning thresholds, so the amount (2) and the labels (early, final) are static.

## For the server

- The server needs to keep now configuration data per each monitored application, per monnitored account. Similar to the usage time records.
- It must be able to store and maintain configuration for the time budget (hours and minutes) and the daily active/inactive switches per week-day.
- The server then must provide a mechanism to compare the used time for a specific application with the configured budget per day. If the used time for this application is exeeding the configured maximum budget (or it is not ticked active for the current week-day), a new start of this application is blocked.
- The break times check remain, and break time rules overrule. So, even if the app would still have time budget, when a break time is hit, no apps will start (US-005).
- The server implements a warning mechanism which uses the two new settings from for early warning and final warning.
- The server therefore has a data model and APIs which all parent apps to read and update these settings, and get notified when another parent app did updates (via the server, same concept as before)
- When the kid uses an application, and the time budget is close to be exausted, e.g. 5 minutes early warning level, it should display the early warning message in a top-most dialog to the kid.
- When the kid uses an application, and the time budget is about to be exausted, e.g. 1 minutes final warning level, it should display the final warning message in a top-most dialog to the kid.
- When the time budget for an app is exauseted, the application shall be closed gracefully by the EagleEye system. When not grafully shut down after 20 seconds, terminated (same behavior as in US-004).
- The same mechanism shall be used when a break time is approaching. Early warning, e.g. 5 minutes, before a break time is configured. Final warning accordingly. Using the same two configured text messages.
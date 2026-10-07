# Implementation Report: US-003 — Account Inventory and Selection of Accounts under Parental Control

**Author**: DEV
**Date**: 2026-10-07
**Branch**: `feature/US-003-monitored-accounts`
**Plan**: `02_Implementation/docs/requirements/user-stories/US-003/implementation-plan.md` (approved 2026-10-07, Q-1 to Q-7 answered), ADR-010
**Machine(s) used**: Windows Developer Machine only (see §7)
**Story status**: Implemented
**Review**: implementation approved by Michael (2026-10-07). Usability issue `US-003/issues/ISSUE-006.md` (account list as a table) to be fixed in patch 0.3.1.

---

## 1. Summary

US-003 is implemented as planned in Steps 0 to 8. The service keeps an inventory of the standard accounts of its PC, checks it every 15 s, stores the parent's selection per SID, writes every change to its new log file in the admin-only folder `%ProgramData%\EagleEye\logs\`, and broadcasts every change to all paired parent apps (ADR-010). The Windows parent app has a third settings section "Benutzerkonten auf dem EagleEye-PC" with one checkbox per account.

| Component | What was built |
|---|---|
| `EagleEye.Shared` | API-first contracts: `IParentHub.GetUserAccounts`, `IParentHub.SetParentalControl`, `IParentClientCallback.OnUserAccountsChanged`; DTOs `UserAccountDto`, `UserAccountListDto`, `StateWriteAckDto`; `Logging/` rolling file provider (`RollingFileLoggerProvider`, `RollingFileLogger`, `RollingFileWriter`, `RollingFileOptions`, `LogLineFormatter`); package `Microsoft.Extensions.Logging.Abstractions` 10.0.12 |
| `EagleEye.Service` | `UserAccounts/`: `NetApiLocalAccountSource` (NetApi32 via `[LibraryImport]`), `AccountInventoryFilter`, `AccountInventory`, `UserAccountService` (state owner, revision, lock), `UserAccountsBroadcaster`, `AccountInventoryMonitor` (15 s), two exceptions; `Data/`: migration 2 `AccountSelections`, `AccountSelectionRepository`; `Diagnostics/`: `LogDirectorySecurity`, `LogDirectoryProtector`; `ServicePaths`: `LogDirectory`, `LogFilePrefix`, `IsOverridden`; `ParentHub`: two methods; `Program`: log folder protection, file provider and filters, DI, inventory initialization before Kestrel accepts connections |
| `EagleEye.ParentApp.Core` | `IParentHubClient`/`ParentHubClient`: two calls + callback handler (registered before `StartAsync`); `ParentHubGateway` (+ `IParentHubGateway`, `IPairedConnectionSink`, `ParentHubNotConnectedException`); `StateReplica<T>`; `Accounts/UserAccountsModel` (+ `IUserAccountsModel`, `AccountsLoadState`); view models `UserAccountsViewModel`, `UserAccountItemViewModel`, `AccountDisplayName`, `AccountsSectionState`; `ConnectionCoordinator` reports confirmed/lost connections to the gateway; 9 new texts (de/en) |
| `EagleEye.ParentApp` (MAUI head) | `SettingsView`: third section; `MauiProgram`: gateway (as `IParentHubGateway` and `IPairedConnectionSink`), model, view model |
| Installer | `setup.iss`: creates `%ProgramData%\EagleEye\logs\` with SYSTEM + Administrators only (idempotent, also on upgrade). `parentapp-setup.iss` unchanged |
| Version | `Directory.Build.props` → `0.3.0` (service reports `EagleEye_v0.3`) |

**Build / test result** (Windows, final state): `build.ps1` 0 warnings, 0 errors for all six steps (Shared, Service, TrayClient, ParentApp.Core, ParentApp Windows, ParentApp Android). `test.ps1` all **921** unit tests pass (Shared 141, Service 303, TrayClient 40, ParentApp 437), 0 failed.

---

## 2. Deviations from the Implementation Plan

| # | Plan | Implemented | Why |
|---|---|---|---|
| D-1 | `NetUserEnum` level 23 (`USER_INFO_23`: name, full name, flags, SID) | `NetUserEnum` level 0 (names, `FILTER_NORMAL_ACCOUNT`), then `NetUserGetInfo` level 23 per account. An account deleted between the two calls (`NERR_UserNotFound`) is skipped. The name of the group S-1-5-32-544 is resolved with `SecurityIdentifier.Translate(typeof(NTAccount))` (which calls `LookupAccountSid`) instead of an own P/Invoke | `NetUserEnum` supports only the levels 0, 1, 2, 3, 10, 11 and 20; level 23 exists only for `NetUserGetInfo`. Same data, same API family. Verified on this PC: `eagleeye-kid` (RID 1002) is the only standard account; `Admin` (member of "Administratoren") and the built-ins are excluded, as `Get-LocalUser` / `Get-LocalGroupMember` show. |
| D-2 | `DeleteMissingAsync(existingSids)` returns the number of deleted rows | Returns the deleted SIDs (`IReadOnlyList<string>`). One statement `DELETE … WHERE Sid NOT IN (@p0, …) RETURNING Sid` | The plan's log entry "Forgot the parental-control selection of {Count} deleted account(s): {Sids}." needs the SIDs. One statement is atomic without an explicit transaction. |
| D-3 | `RefreshInventoryAsync`: if the inventory differs → delete missing rows, revision++, log, broadcast | The comparison also covers the set of **all** local SIDs (admin accounts included). If only admin accounts were added or deleted, rows of deleted SIDs are still deleted, but there is no new revision and no broadcast | Otherwise a ticked account that became an admin (row kept, AC-20/AC-21) and was then deleted while still an admin would never be forgotten (AC-22), because the standard list did not change. Apps show nothing different in that case, so no broadcast is needed. |
| D-4 | `IPairedConnectionSink` internal | Public | `MauiProgram` (MAUI head, another assembly) registers the gateway under this interface, as the plan requires. Feature code still uses `IParentHubGateway` only. |
| D-5 | `RollingFileWriter` cleans up after a roll-over | Also cleans up whenever it opens a file, i.e. at the first entry after a service start. It never creates the log folder. A file that cannot be deleted (open elsewhere) is skipped and deleted at the next clean-up | FR-SVC-103 (no file older than 5 days) would otherwise only apply after a 50 MB roll-over, which may never happen. Creating the folder in the writer could create it without the admin-only ACL (inherits "Users: read" from `%ProgramData%\EagleEye`). |
| D-6 | — (write while the inventory is unavailable not specified) | `SetParentalControlAsync` throws `AccountInventoryUnavailableException`; the hub maps it to "The change could not be saved." | Only possible if Windows could not be read since the start; the app shows the AC-16 error and reverts. |
| D-7 | `Program`: `await IUserAccountService.InitializeAsync()` after `ServiceDatabase.InitializeAsync()` | Inside the existing database `try`: a SQLite error while loading the selections stops the service with the same critical log entry as a damaged database | The selections cannot be shown correctly without them. An enumeration failure never stops the service (as planned: the monitor retries every 15 s). |
| D-8 | — | `EagleEye.Service.csproj`: `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>` | Required by the source generator of `[LibraryImport]` (the plan names `[LibraryImport]`). |

No other deviations. The plan's own story interpretations D-1 to D-7 (file logging, no extra fetch on page open, inline error text, row disabled while pending, 4 s write timeout, full name/disabled changes pushed, no-op writes broadcast) are implemented as written.

---

## 3. Files Created or Modified

All paths relative to `02_Implementation/`.

**Shared** — created: `src/EagleEye.Shared/Models/{UserAccountDto, UserAccountListDto, StateWriteAckDto}.cs`, `Logging/{RollingFileOptions, RollingFileWriter, RollingFileLogger, RollingFileLoggerProvider, LogLineFormatter}.cs`; modified: `Contracts/IParentHub.cs`, `Contracts/IParentClientCallback.cs`, `EagleEye.Shared.csproj`, `README.md`.

**Service** — created: `src/EagleEye.Service/UserAccounts/{LocalAccountInfo, ILocalAccountSource, NetApiLocalAccountSource, AccountInventoryFilter, AccountInventory, UnknownAccountException, AccountInventoryUnavailableException, IUserAccountsBroadcaster, UserAccountsBroadcaster, IUserAccountService, UserAccountService, AccountInventoryMonitor}.cs`, `Data/{IAccountSelectionRepository, AccountSelectionRepository}.cs`, `Diagnostics/{LogDirectorySecurity, LogDirectoryProtector}.cs`; modified: `Program.cs`, `IServicePaths.cs`, `ServicePaths.cs`, `Communication/ParentHub.cs`, `Data/ServiceDatabase.cs`, `EagleEye.Service.csproj`, `README.md`.

**ParentApp.Core** — created: `src/EagleEye.ParentApp.Core/Communication/{IParentHubGateway, IPairedConnectionSink, ParentHubGateway, ParentHubNotConnectedException, StateReplica}.cs`, `Accounts/{AccountsLoadState, IUserAccountsModel, UserAccountsModel}.cs`, `ViewModels/{AccountDisplayName, AccountsSectionState, UserAccountItemViewModel, UserAccountsViewModel}.cs`; modified: `Communication/{IParentHubClient, ParentHubClient, ConnectionCoordinator}.cs`, `AppTexts.cs`, `Resources/AppTexts.resx`, `Resources/AppTexts.en.resx`, `README.md`.

**ParentApp** (MAUI head) — modified: `src/EagleEye.ParentApp/Views/SettingsView.xaml`, `Views/SettingsView.xaml.cs`, `MauiProgram.cs`, `README.md`.

**Tests** — created: `tests/EagleEye.Shared.Tests/Models/UserAccountDtoTests.cs`, `Logging/{TempDirectory, LogLineFormatterTests, RollingFileWriterTests, RollingFileLoggerTests}.cs`; `tests/EagleEye.Service.Tests/TestLogger.cs`, `Communication/ParentHubUserAccountsTests.cs`, `Data/AccountSelectionRepositoryTests.cs`, `Diagnostics/LogDirectorySecurityTests.cs`, `UserAccounts/{AccountInventoryFilterTests, AccountInventoryTests, UserAccountServiceTests, UserAccountsBroadcasterTests, AccountInventoryMonitorTests}.cs`; `tests/EagleEye.ParentApp.Tests/Communication/{ParentHubGatewayTests, StateReplicaTests}.cs`, `Accounts/UserAccountsModelTests.cs`, `ViewModels/{UserAccountsViewModelTests, UserAccountItemViewModelTests, AccountDisplayNameTests}.cs`; modified: `tests/EagleEye.Shared.Tests/EagleEye.Shared.Tests.csproj` (TimeProvider.Testing 10.10.0), `tests/EagleEye.Service.Tests/{ServicePathsTests, Communication/ParentHubTests, Communication/PairingAuthorizationHubFilterTests, Data/PairedDeviceRepositoryTests}.cs`, `tests/EagleEye.ParentApp.Tests/{AppTextsTests, Communication/ConnectionCoordinatorTests}.cs`.

**Build, installer, docs** — modified: `Directory.Build.props`, `installer/windows/setup.iss`, `docs/requirements/user-stories/US-003/user-story.md` (status); created: this report.

---

## 4. Unit Test Coverage

Coverage measured with coverlet (`dotnet test --collect:"XPlat Code Coverage"`), line and branch. Every class in the plan's table has **100 % line and 100 % branch coverage**.

| Class | Test class | Scenarios |
|---|---|---|
| DTOs | `UserAccountDtoTests` | construction, equality/inequality; JSON round trip with web defaults incl. `null` `FullName` and `LastChangeRequestId`; camelCase names; `long.MaxValue` revision |
| `LogLineFormatter` | `LogLineFormatterTests` | every level code incl. `None`/unknown; one line without exception; exception on the following lines; offset `+02:00` / `-05:00` |
| `RollingFileWriter`, `RollingFileOptions` | `RollingFileWriterTests` | first file `-001`; UTF-8 without BOM; continues the highest file below the limit; starts the next number at the limit; roll-over before exceeding; exactly at the limit stays; oversize entry into an empty file; `-1000` after `-999`; keeps newest 3; deletes non-current files older than 5 days, keeps exactly 5 days; never deletes the current file; old file locked elsewhere; unrelated/odd file names ignored; folder missing → swallowed and **not created**; recovers on the next write; access denied swallowed; readable while open; 200 parallel writes do not interleave; dispose closes; guards; defaults 50 MB / 3 / 5 days |
| `RollingFileLogger`, `RollingFileLoggerProvider` | `RollingFileLoggerTests` | `IsEnabled` per level, `None` false; formatted line with local time; exception written; `None`, empty message → nothing; null message with exception; null formatter; `BeginScope` no-op; logger per category (same instance), one file for all categories; null category; dispose closes the file |
| `AccountInventoryFilter` | `AccountInventoryFilterTests` | RIDs 500/501/503/504 excluded (also renamed: "Renamed guest"); 502, 1000+, `S-1-5-18` kept; malformed SIDs → not built-in; `defaultuser0`, `DefaultUser0`, `DEFAULTUSER0` excluded; `defaultuser1`, `defaultuser00`, `xdefaultuser0`, `defaultuser`, `defaultuser100000` kept; admin excluded; local and Microsoft-linked standard accounts and disabled accounts kept; guards |
| `AccountInventory` | `AccountInventoryTests` | `HasSameContent` equal (any order); differs by added, removed, renamed, full name, disabled, became admin, admin became standard, admin added, admin deleted; `HasSameStandardAccounts`; `Compare` (added/removed/changed user names, sorted) |
| `LogDirectorySecurity` | `LogDirectorySecurityTests` | inheritance protected; exactly `S-1-5-18` and `S-1-5-32-544`; FullControl, Allow, container + object inherit; no rule for Users, Authenticated Users, Everyone; owner Administrators |
| `ServicePaths` | `ServicePathsTests` | `LogDirectory` = `<data>\logs`, prefix, `IsOverridden` (constructor, `Resolve` with and without override); `EnsureDirectories` creates data, `certs\`, `logs\` (temp folder) |
| `AccountSelectionRepository`, `ServiceDatabase` | `AccountSelectionRepositoryTests`, `PairedDeviceRepositoryTests` | schema version 2; load empty; set new; lookup ignores case; upsert updates value, name, time; UTC ISO time; `DeleteMissingAsync` deletes only missing and returns them, nothing missing, empty set; guards; **migration 2 on a real version-1 file keeps `PairedDevices`** |
| `UserAccountService` | `UserAccountServiceTests` (42) | initialize: revision 1, standard accounts only, sorted; loads selections; log "loaded: 2 … 1 under parental control"; forgets SIDs deleted while stopped; enumeration failure → Error log + unavailable; no accounts → unavailable, nothing deleted; no broadcast. Set: stores; revisions 2, 3; **broadcast with requestId before return**; later query carries requestId; **log line with user name and yes/no**; no-op write → new revision + broadcast; SID case-insensitive; admin / guest / `defaultuser0` / unknown SID → `UnknownAccountException`, nothing stored, Warning log; unavailable; repository failure → no revision, no broadcast; broadcast failure → Warning, write succeeds; **two concurrent writes → revisions 2, 3, broadcasts in order, last wins**; guards. Refresh: no change → no broadcast; added / deleted / renamed / full name / disabled / became admin / admin became standard → exactly one broadcast with `LastChangeRequestId = null`; rename keeps the tick; log of the difference; became admin → not in snapshot, row kept, write rejected; back to standard → tick restored; deleted → row deleted, new account with the same name unticked; ticked admin deleted → row deleted without broadcast; **enumeration failure or zero accounts → nothing deleted, no broadcast**; unavailable → available on the next refresh; new account unticked |
| `UserAccountsBroadcaster` | `UserAccountsBroadcasterTests` | sends `OnUserAccountsChanged` to group `Parents`; guard |
| `AccountInventoryMonitor` | `AccountInventoryMonitorTests` | interval 15 s; refresh every 15 s (`FakeTimeProvider`), not before; exception logged, loop continues; stop while waiting and during a refresh ends without error |
| `ParentHub` (new methods), `PairingAuthorizationHubFilter` | `ParentHubUserAccountsTests`, `PairingAuthorizationHubFilterTests` | `GetUserAccounts` delegation; unavailable / other failure → logged + "The accounts are not available."; `HubException` passed through; `SetParentalControl` with **device name from `Context.Items`**; empty `requestId`, null/empty/blank/invalid SIDs → "Invalid request." without calling the service; unknown account → "Unknown account."; other → logged + "The change could not be saved."; unpaired `GetUserAccounts` / `SetParentalControl` → "Not paired."; paired → next |
| `ConnectionCoordinator` | `ConnectionCoordinatorTests` (+8) | `SetConnected` on attach and on reconnect with `IsPaired`; never when the service says not paired; `SetDisconnected` on reconnecting, closed, remove pairing, pairing lost after reconnect; events of a replaced client do not reach the sink |
| `ParentHubGateway` | `ParentHubGatewayTests` | not connected / after disconnect → `ParentHubNotConnectedException`; call runs on the current client; token cancels exactly at the timeout; `Connected` on every connect; `Disconnected` once and only if connected; broadcasts forwarded only from the current client and only while connected; switching clients unsubscribes the old one; same client twice subscribes once; guards |
| `StateReplica<T>` | `StateReplicaTests` | applies higher revision; ignores equal and lower; reset → `Current` null, revision 0, accepts revision 1; guards |
| `UserAccountsModel` | `UserAccountsModelTests` (30) | Loading until the fetch completes → Ready; `Changed` for both; gateway already connected at construction; fetch failure → NotAvailable (+ `Changed`); broadcast after failed fetch → Ready; fetch failing after a broadcast stays Ready; **results and failures of an earlier connection's fetch are ignored**; disconnect → NotAvailable, snapshot gone; reconnect resets the revision; stale broadcast ignored. Writes: confirmed by own requestId before the ack; by own broadcast after the ack; by a later foreign revision; by a later service-originated revision; not by a foreign revision below the ack (→ timeout); ack revision already applied → at once; `HubException` → false, no re-fetch; no confirmation within 4 s → false + re-fetch; hanging call → false exactly at 4 s + re-fetch; connection lost → false, next connect fetches; disconnect while waiting → false; not connected → false; guards |
| `UserAccountsViewModel`, `UserAccountItemViewModel`, `AccountDisplayName` | `UserAccountsViewModelTests`, `UserAccountItemViewModelTests`, `AccountDisplayNameTests` | texts per state in de and en (No data, Loading, No accounts); list without status; merge keeps item instances; insert/remove at sorted positions; rename moves the row; sort "anna", "Ärger", "bernd", "Max", "Zoe" (de-DE, ignore case); disabled suffix de/en; disabled row toggleable; toggle calls the model once and disables the row; success → stored value, error cleared; failure → error de/en, reverted, enabled; pending row keeps its requested value while a foreign snapshot updates other rows; write finishing after disconnect or after the account was deleted; error cleared on disconnect and on connect, kept on further snapshots; **programmatic updates never call the model**; full name / user name only (null, empty, blank) |
| `AppTexts` | `AppTextsTests` | every key in de and en (automatic), same key set, two-argument `Format`, section and checkbox texts per language |

**Not unit-tested, as the plan specifies (verified in the smoke check, §6):** `NetApiLocalAccountSource`, `LogDirectoryProtector`, `ParentHubClient`, both `Program` classes, the MAUI view and DI, the installers.

---

## 5. Open Questions and Risks

1. **Service installer not executed by DEV** (no admin rights, as in US-001/US-002). Not yet verified on the real system: the service as SYSTEM writes `%ProgramData%\EagleEye\logs\EagleEye.Service-001.log`; the `logs\` ACL set by the installer and re-applied by the service (owner Administrators — SYSTEM may set this owner because its token contains the Administrators group); the account inventory as SYSTEM; the upgrade 0.2.0 → 0.3.0 with migration 2 (unit-tested on a version-1 file).
2. **If the service cannot apply the `logs\` ACL**, it writes a warning to the Event Log ("The log folder … could not be restricted …; no log file is written.") and runs without a log file. AC-14's log line is then only in the Event Log.
3. **Inventory changes (AC-19 to AC-22) were not smoke-tested**: creating, renaming, disabling or deleting accounts needs admin rights. Covered by unit tests; Michael tests them manually.
4. **AC-16 is hard to provoke on one PC** (see §6): on loopback the app notices a stopped service at once and switches to "Keine Daten verfügbar" before a click can land. Use the second PC (unplug its network, then tick) as the plan suggests.
5. **OQ-7 (accepted risk)**: a kid who pairs an own app could untick their account. The selection has no effect yet.
6. **Q-7**: only `defaultuser0` is excluded. If `defaultuser1` or similar shows up during testing, PRO extends AC-3 (one-line filter change).
7. `ConnectionCoordinator` grew by 6 lines only (the gateway takes the feature calls); it stays one state machine of about 510 lines (see US-002 report §5.6).

---

## 6. How to Test

### Artifacts and version

| Item | Value |
|---|---|
| Service + tray installer | `03_Delivery/windows/EagleEye-Setup-0.3.0.exe` (admin), built 2026-10-07 12:04, SHA-256 `055e09cf…bfeda3` |
| Parent app installer | `03_Delivery/windows/EagleEye-ParentApp-Setup-0.3.0.exe` (per user, no admin), built 2026-10-07 12:11, SHA-256 `71ec14f4…ae1cea` |
| Rebuild | `pwsh 02_Implementation/scripts/package-windows.ps1` (both) or `-Target Service` / `-Target ParentApp` |
| Version | 0.3.0; the service reports `EagleEye_v0.3` (tray → *App Infos*) |

### Install and start

1. **Service PC, admin account:** run `EagleEye-Setup-0.3.0.exe`. Over 0.2.0 it keeps `%ProgramData%\EagleEye` (certificate, pairings); the service applies migration 2 at its first start, so paired apps stay paired. On a fresh install, pair as in US-002. (At the time of this report, no EagleEye service was installed on the Windows Developer Machine.)
2. **Parent app(s):** run `EagleEye-ParentApp-Setup-0.3.0.exe` as a normal user on the service PC and on the second Windows PC (update over 0.2.0 keeps the pairing).
3. Settings page: below "Darstellung" and "Serververbindung" the new section **"Benutzerkonten auf dem EagleEye-PC"**. The menu still has only "Einstellungen".

### UI texts as implemented (de / en)

| Where | German (default) | English |
|---|---|---|
| Section header (AC-6) | Benutzerkonten auf dem EagleEye-PC | User accounts on the EagleEye PC |
| Instruction above the list | Markieren Sie die Konten, die unter Elternkontrolle stehen. | Tick the accounts that are under parental control. |
| Label right of each checkbox (AC-10) | Unter Elternkontrolle | Under parental control |
| Not paired / not connected / fetch failed (AC-7, AC-8) | Keine Daten verfügbar | No data available |
| Connected, no standard accounts (AC-9) | Keine Nicht-Administrator-Konten vorhanden | No non-admin accounts available |
| While fetching (AC-13) | Wird geladen … | Loading … |
| Row name (AC-11) | `Max Adler (max)`, or `max` without full name | same |
| Disabled suffix (AC-12) | `Max Adler (max) (deaktiviert)` | `Max Adler (max) (disabled)` |
| Error below the list (AC-16) | Die Änderung konnte nicht gespeichert werden. Bitte erneut versuchen. | The change could not be saved. Please try again. |

Row layout: name on the left, then the checkbox, then the label "Unter Elternkontrolle". Rows are sorted by the shown name (German/English culture, ignoring case). While a row's change is pending (normally well below 1 s, at most 4 s), its checkbox is disabled; other rows stay usable. The error text stays until the next successful change or the next connect/disconnect.

### What to observe

| Area | Observation |
|---|---|
| Inventory (AC-1 to AC-4) | Only standard accounts. Never: admin accounts (also via a nested group), Administrator, Gast, DefaultAccount, WDAGUtilityAccount (also if enabled or renamed), `defaultuser0` (any casing). On this PC today: only `eagleeye-kid`. |
| Changes on the PC (AC-19 to AC-22) | Appear in all connected apps within about 16 s at most (check every 15 s), typically about 8 s. Rename keeps the tick; standard → admin removes the row; admin → standard brings it back with its earlier tick; delete + new account with the same name → unticked. Full-name and disabled-flag changes are pushed too. |
| Two apps (AC-23, AC-24) | A tick in app A appears in app B within about 1 s. Simultaneous changes: both apps end on the value the service received last; the log shows both with consecutive revisions. |
| Error (AC-16) | Second PC: unplug its network (the app does not notice at once) and tick → after 4 s the tick reverts and the error text appears; after reconnecting the app shows the stored state. On the service PC itself, a stopped service is noticed at once and the section shows "Keine Daten verfügbar" instead. |
| Persistence (AC-15) | Ticks survive app restart, service restart (`services.msc` → *EagleEye Service*), reboot, and re-running `EagleEye-Setup-0.3.0.exe`. |
| Service log (AC-14) | `%ProgramData%\EagleEye\logs\EagleEye.Service-001.log`, readable **as administrator** (Notepad as admin, or `Get-Content -Wait` in an elevated PowerShell). Lines: `… [INF] EagleEye.Service.UserAccounts.UserAccountService: Account eagleeye-kid (S-1-5-21-…-1002): under parental control = yes (set by parent device <name>, request <guid>, revision 2).`; inventory changes `Account inventory changed (revision N): added [lena], removed [], changed [maximilian]; 3 standard accounts.`; at start `Account inventory loaded: 1 standard accounts, 1 under parental control.`; deleted ticked accounts `Forgot the parental-control selection of 1 deleted account(s): S-1-5-21-….` The same entries go to *Ereignisanzeige → Anwendung*, source `EagleEye`. |
| Log folder ACL (AC-14) | `icacls "%ProgramData%\EagleEye\logs"` (as admin): only `NT-AUTORITÄT\SYSTEM:(OI)(CI)(F)` and `VORDEFINIERT\Administratoren:(OI)(CI)(F)`, no inherited entries. As `eagleeye-kid`: opening `C:\ProgramData\EagleEye\logs` is denied; the rest of `C:\ProgramData\EagleEye` stays readable. Delete `logs\` as admin and restart the service → the folder comes back with the same ACL. |
| Retention | 50 MB per file, number grows (`-002`, …), at most 3 files, files older than 5 days deleted except the current one (checked whenever the service opens a log file, i.e. at its first entry after a start and at every roll-over). |
| Database | `%ProgramData%\EagleEye\EagleEye.Service.db`, table `AccountSelections` (`Sid`, `UserName`, `UnderParentalControl` 0/1, `ChangedAtUtc`); rows only for accounts ticked or unticked at least once. |

### DEV smoke check (Windows Developer Machine, 2026-10-07, non-elevated)

- Service (Debug, console, `EAGLEEYE_DATA_DIR` = `02_Implementation/artifacts/smoke-us003`): NetApi enumeration works unelevated; log file `logs\EagleEye.Service-001.log` created; "Account inventory loaded: 1 standard accounts, 0 under parental control." — `eagleeye-kid` only, matching `Get-LocalUser` / `Get-LocalGroupMember` (German group name).
- **Installed** parent app 0.3.0 (`/VERYSILENT`, per user, file version 0.3.0.0), driven through UI Automation: unpaired → section "Benutzerkonten auf dem EagleEye-PC" with "Keine Daten verfügbar"; paired with `localhost` (code from Event Log event 1000) → instruction text and row `eagleeye-kid` with an unticked checkbox, green "Verbunden mit localhost". Tick → log line `under parental control = yes (set by parent device ZOCK-O-MAT-V3, …, revision 2)`. Service stopped → "Keine Daten verfügbar", "Nicht verbunden mit localhost". Service restarted → "loaded: 1 standard accounts, 1 under parental control", app reconnected by itself and showed the tick (persistence). Untick → `= no`.
- Broadcast to a second app: the Debug build of the parent app ran next to the installed one (same user data, same pairing); a tick in the installed app showed up in the Debug app within 1 s.
- AC-16 could not be provoked on loopback (see §5.4).
- **Not done:** service installer (admin), service as SYSTEM, `logs\` ACL on the real folder, account changes (admin), second PC.
- Clean-up: both apps closed, the parent app uninstalled (program folder and `%LocalAppData%\EagleEye` gone), no EagleEye process running. Left behind (git-ignored): `02_Implementation/artifacts/smoke-us003*` (smoke data folder, console output), `artifacts/us003-build.txt`, `artifacts/us003-test.txt`; a test certificate key in the user key store (console mode, US-002 D-1); the service registered device "ZOCK-O-MAT-V3" only in the smoke database.

---

## 7. Machine(s) Used

| Step | Machine |
|---|---|
| Steps 0 to 8 (code, unit tests, `build.ps1`, `test.ps1`, both installers, smoke check, docs) | Windows Developer Machine |
| MacBook | Not used. `Shared` and `ParentApp.Core` stay free of Windows APIs (`LogDirectorySecurity` and the NetApi source are in the Service), so `build.ps1` / `test.ps1` keep working there after `git pull`. |

# EagleEye.Service

Windows service running as `SYSTEM`. The core enforcement engine of EagleEye.

## Responsibility

- Monitor all running processes in the kid's Windows user session
- Enforce app rules: attempt graceful shutdown, then force-kill non-allowed processes
- Manage per-user time budgets and pause windows (counts down, resets at midnight)
- Serve a SignalR hub for parent apps and the tray client
- Manage TLS certificate (auto-generated, self-signed, non-expiring)
- Collect and store application usage statistics per user account
- Auto-discover local standard (non-admin) Windows user accounts

## Component Structure

```
EagleEye.Service/
├── SessionAgent/     Agent mode (`--session-agent`, ADR-011): window scan with the "Apps" rule, JSON-line
│                     reports on stdout; no window, no hooks, no file access, SetDefaultDllDirectories
├── Monitoring/       Sessions (WTS, SCM session/power events), hardened SessionAgentLauncher, supervisor,
│                     process inspection, ProgramPathPolicy, VersionResourceReader, PackageManifestReader,
│                     name resolution (impersonating the session user), agent report processor (US-004)
├── Enforcement/      Enforcement engine — graceful shutdown + force-kill logic
├── Configuration/    Per-user configuration management — rules, budgets, schedules
├── Statistics/       Usage accounting (ADR-012): event queue, UsageTracker (monotonic credit, midnight
│                     split), UsageService (state owner of the usage areas), 5 s accounting loop
├── Communication/    SignalR hub (server side) — serves parent apps + tray client
├── Certificates/     Self-signed TLS certificate generation and management
├── UserAccounts/     Account inventory and selection (US-003): NetApiLocalAccountSource (Win32),
│                     AccountInventoryFilter (standard only; no built-ins by RID, no defaultuser0),
│                     UserAccountService (state owner of the area "UserAccounts", ADR-010),
│                     UserAccountsBroadcaster, AccountInventoryMonitor (checks every 15 s)
├── Data/             ServiceDatabase (migrations: 1 PairedDevices, 2 AccountSelections,
│                     3 AppRecords/AppInstances/DailyUsage), repositories
└── Diagnostics/      Event Log pairing code, admin-only ACL of the logs\ folder
```

## State areas (ADR-010)

| Area | State owner | Query / write / broadcast |
|---|---|---|
| `UserAccounts` | `UserAccounts/UserAccountService` | `GetUserAccounts` / `SetParentalControl` / `OnUserAccountsChanged` |
| `UsageDay:{sid}:{day}` | `Statistics/UsageService` | `GetAccountUsage` (today + days with usage, 90 days) / none / `OnDayUsageChanged` (at most once per account every 5 s) |

Every accepted write and every change of the Windows accounts produces one revision and one broadcast to the group `Parents` (including the sender). Revisions are in memory and start at 1 on every service start.

## Data and logs

| What | Where |
|---|---|
| Database | `%ProgramData%\EagleEye\EagleEye.Service.db` (`PairedDevices`, `AccountSelections`) |
| Certificate | `%ProgramData%\EagleEye\certs\` (SYSTEM + Administrators only) |
| Log files | `%ProgramData%\EagleEye\logs\EagleEye.Service-NNN.log` (SYSTEM + Administrators only; 50 MB per file, at most 3 files, older than 5 days deleted except the current one). The service re-applies the folder ACL on every start; if that fails it writes no log file in that run. |
| Event Log | *Application*, source `EagleEye` (Information and above of `EagleEye.*`) |

## Runtime

- Runs as Windows Service under the `SYSTEM` account
- Installed and managed by the Inno Setup installer (`installer/windows/setup.iss`)
- Communicates with `EagleEye.TrayClient` via SignalR over localhost
- Communicates with parent apps via SignalR over local LAN (HTTPS, self-signed cert)
- Starts one **session agent** (`EagleEye.Service.exe --session-agent`, SYSTEM with a hardened token) in every
  logged-on session of an account under parental control (ADR-011); the agent reports app windows over anonymous pipes

## Debug-only switches (never in Release builds, ADR-011 T-13)

| Variable | Effect |
|---|---|
| `EAGLEEYE_DATA_DIR` | Data folder of the console-mode service (US-002) |
| `EAGLEEYE_DEV_WATCH_SID` | Starts the agent as an ordinary child process in the own session and treats this account as a standard account, so DEV can tick it and smoke-test without SYSTEM rights |
| `EAGLEEYE_DEV_PORT_OFFSET` | Shifts both ports (e.g. 10000 → 15080/15443), so a console service can run next to an installed one; the Debug parent app reads the same variable |

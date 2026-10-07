# ADR-002: SQLite for Data Persistence, YAML for Application Configuration, .NET Logging for Diagnosability

**Status**: Accepted
**Date**: 2026-09-20
**Deciders**: Michael (project owner), ARC

---

## Context

EagleEye consists of four deployable components (Service, TrayClient, ParentApp, Shared library) running on different platforms (Windows, macOS, iOS, Android). Each component needs to persist application data (user configuration, statistics, credentials, caches) and read runtime configuration (ports, timeouts, feature toggles). Additionally, all components need a consistent, configurable logging strategy to support diagnosis of issues in the field.

Three related decisions were made together because they define the complete local data and observability story for every EagleEye component.

---

## Decision

### 1. SQLite for all persisted application data

We will use **SQLite** (via `Microsoft.Data.Sqlite`) as the single persistence technology for all runtime application data across all EagleEye components. Each component has its own database file, stored in the OS-standard application data directory for the host platform.

| Component | Database File | Location |
|-----------|--------------|----------|
| EagleEye.Service | `EagleEye.Service.db` | `%ProgramData%\EagleEye\` |
| EagleEye.TrayClient | `EagleEye.TrayClient.db` | `%ProgramData%\EagleEye\` |
| EagleEye.ParentApp (macOS) | `EagleEye.ParentApp.db` | `~/Library/Application Support/EagleEye/` |
| EagleEye.ParentApp (iOS) | `EagleEye.ParentApp.db` | App sandbox `Documents/` |
| EagleEye.ParentApp (Android) | `EagleEye.ParentApp.db` | App internal storage |

SQLite databases are accessed with WAL (Write-Ahead Logging) mode enabled for concurrent read performance. All write operations use transactions. Schema migrations are versioned and applied automatically on startup.

### 2. YAML for application configuration

Each application has exactly **one YAML configuration file**, named after the application (e.g., `EagleEye.Service.yaml`), stored in the same OS-standard application data directory as its SQLite database. YAML files are parsed using `YamlDotNet`.

The separation principle: **YAML files contain deployment-time configuration** — values an administrator might edit before or between runs (ports, timeouts, feature toggles, debug mode). **SQLite databases contain runtime application data** — values the application creates and manages during operation (user rules, statistics, credentials, caches).

If a YAML file is missing on startup, the application creates one with default values.

### 3. .NET built-in logging for diagnosability

All components use **`Microsoft.Extensions.Logging`** with the .NET 10 built-in file logging provider. No third-party logging libraries (Serilog, NLog, etc.) are used.

Each application supports two modes, controlled via `logging.debug_mode` in its YAML configuration:

| Mode | Levels | Content |
|------|--------|---------|
| **Normal** (default) | Error, Warning, Information | Operational events |
| **Debug** | Error, Warning, Information, Debug, Trace | Full stack traces with parameter values, internal state transitions, SignalR payloads, SQL queries |

Log file management:

| Setting | Value |
|---------|-------|
| Maximum file size | 50 MB |
| Maximum file count | 3 rolling files per application |
| File location | Same directory as SQLite database and YAML config |
| Naming convention | `{ApplicationName}-{sequence}.log` |

**Security rule**: Sensitive data (passwords, tokens, pairing codes, certificate private keys) must never appear in log output, regardless of log level. Sensitive values are replaced with `***`.

The Service supports runtime switching between normal and debug mode via a parent app command (no restart required). TrayClient and ParentApp require an application restart after changing the YAML setting.

---

## Rationale

### SQLite over JSON files

| Criterion | JSON Files | SQLite |
|-----------|-----------|--------|
| ACID transactions | No (manual atomic-write workarounds needed) | Yes (built-in) |
| Crash resilience | Requires temp-file-then-rename pattern | WAL mode handles this natively |
| Query capability | Must deserialize entire file to filter | SQL queries with indexing |
| Schema evolution | Manual migration of file formats | Standard migration tooling |
| Concurrent access | File locking issues | WAL mode supports concurrent readers |
| Cross-platform support | N/A (both work) | Excellent — ships with .NET, works on all target platforms |
| Data size growth (90 days of stats) | Entire file rewritten on every update | Only affected rows touched |

### YAML over JSON/appsettings.json for configuration

- YAML is more human-readable and human-editable than JSON (comments, no trailing-comma issues)
- Clear separation from application data (SQLite) avoids confusion about what is admin-editable vs. machine-managed
- One file per application keeps configuration simple and discoverable
- `YamlDotNet` is a mature, well-maintained .NET library

### .NET built-in logging over third-party libraries

- Zero additional dependencies — `Microsoft.Extensions.Logging` is part of .NET 10
- Consistent with the .NET ecosystem patterns (`ILogger<T>`, DI integration)
- Built-in file provider in .NET 10 covers rolling file output without external sinks
- Debug mode with full stack traces and parameter values provides sufficient diagnosability without specialized structured-logging libraries

### Alternatives Considered

| Option | Reason Rejected |
|--------|----------------|
| JSON files for persistence | No ACID transactions, poor query capability, full-file rewrite on updates, manual crash-safety workarounds |
| LiteDB (document database) | Adds a dependency for marginal benefit over SQLite; SQLite has broader ecosystem support and is the .NET standard for embedded databases |
| `appsettings.json` for configuration | Tied to ASP.NET Core configuration system; less human-readable; would create confusion between .NET's own config pipeline and EagleEye's cross-platform config needs |
| Serilog / NLog for logging | Unnecessary third-party dependency when .NET 10 built-in provider meets all requirements |

---

## Consequences

### Positive

- Uniform persistence pattern across all components — one technology to learn, test, and debug
- ACID transactions and WAL mode eliminate data corruption concerns from crashes or concurrent access
- SQL queries enable efficient statistics retrieval and purge without loading entire datasets into memory
- YAML configuration is self-documenting and safely hand-editable by administrators
- Clear separation between admin-editable config (YAML) and machine-managed data (SQLite) prevents accidental data corruption from manual edits
- All components produce diagnosable log output with configurable verbosity — no "black box" components
- Zero third-party dependencies for persistence, configuration, and logging (SQLite ships with .NET; YamlDotNet is the only addition)

### Negative / Trade-offs

- `YamlDotNet` is an additional NuGet dependency (but lightweight and well-established)
- SQLite files are not human-readable like JSON — inspecting data requires a SQLite tool (e.g., DB Browser for SQLite, `sqlite3` CLI)
- Debug-mode logging with full parameter values can produce large log volumes (mitigated by the 50 MB × 3 file rolling limit)

### Neutral

- Schema migrations need to be written and tested for each database change — standard practice for any database-backed application
- The 3-file rolling limit means at most 150 MB of log data per component — acceptable for all target platforms

---

## Implementation Notes (US-003, 2026-10-07; retention and location answered by Michael, provider approved with the US-003 implementation plan)

- **File logging provider.** .NET 10 has no built-in file logging provider for `Microsoft.Extensions.Logging` (the console, debug, EventLog and EventSource providers are built in; files are not). The decision stays: `Microsoft.Extensions.Logging`, no third-party logging library. EagleEye implements a small provider of its own, `EagleEye.Shared/Logging/RollingFileLoggerProvider` (size-based rolling, retention, one line per entry), so that all components can use it. The service is the first user (US-003 AC-14 requires the service log file).
- **Retention** (Michael, 2026-10-07, US-003 plan Q-1). FR-SVC-103 additionally requires deleting log files older than 5 days. Both apply: at most 3 files and at most 5 days, never deleting the current file.
- **Service log location** (Michael, 2026-10-07, US-003 plan Q-2; FR-SVC-100 v1.3). The service writes its logs to the subfolder `%ProgramData%\EagleEye\logs\`, not next to the database. The folder is readable by SYSTEM and Administrators only (inheritance removed, well-known SIDs `S-1-5-18`, `S-1-5-32-544`), like `certs\` (ADR-008 §7): the installer creates it, and the service re-applies the ACL at every start. If that fails, the service writes no log file in that run (warning in the Event Log), so log content never becomes readable for standard users. The table above ("same directory as the SQLite database") is superseded for the service.

---

## References

- System Architecture: `02_Implementation/docs/architecture/arc42/system-architecture.md` — sections 4.1, 4.2, 7.1, 8.3, 8.4, 8.8
- SQLite: <https://www.sqlite.org/>
- `Microsoft.Data.Sqlite`: <https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/>
- `YamlDotNet`: <https://github.com/aaubry/YamlDotNet>
- `Microsoft.Extensions.Logging`: <https://learn.microsoft.com/en-us/dotnet/core/extensions/logging>

# EagleEye — System Architecture

*Template: arc42 v8 | Status: Approved (2026-09-20, amended 2026-10-03, 2026-10-04, 2026-10-07); US-003-specific parts proposed with the US-003 plan*
*Maintainer: ARC Agent | Last Updated: 2026-10-07*

> **Amendment 2026-10-03 (ADR-007)**: Windows desktop target added to the ParentApp; two-machine development; manual acceptance testing. Affected: §1.1, §2.1, §2.2, §3.2, §4.2, §5.5, §7, §8.10, §9. Amendments approved by Michael on 2026-10-03.
>
> **Amendment 2026-10-04 (US-002, ADR-008, ADR-009)**: two service endpoints (tray: HTTP loopback 5080; parent apps: HTTPS 5443), hub-to-port binding, certificate protection and pinning, explicit `StartPairing`, Windows parent app per-user installer, new `EagleEye.ParentApp.Core` library, Shared `Data/` and `Communication/`. Affected: §3.2, §5.1 to §5.5, §6.1, §7 (incl. §7.1, §7.1.1, §7.3, §7.4), §8.1 to §8.5, §9, §11, §12. ADR-008 and ADR-009 were approved by Michael with the US-002 implementation plan on 2026-10-04.
>
> **Amendment 2026-10-07 (ADR-010, accepted by Michael 2026-10-07)**: event-driven state propagation (service broadcasts full snapshots with revisions to all paired apps including the sender; clients fetch after every (re)connect instead of a server push; last write wins). Affected: §5.2 Communication, §5.3, §5.5, §6.4, §6.6, §8.3, §8.11, §9, §12. Affected: §4.2, §5.2, §5.3, §5.5, §6.4, §6.6 (new), §8.3, §8.4, §8.10, §8.11, §9, §11 R-8, §12.

---

## 1. Introduction and Goals

### 1.1 Requirements Overview

EagleEye is a parental control solution for Windows PCs. It gives parents remote control over which applications their children may use and for how long. The system consists of four components built on .NET 10:

| Component | Role |
|-----------|------|
| **EagleEye.Service** | Windows service (SYSTEM) — process monitoring, enforcement, SignalR hub, data persistence |
| **EagleEye.TrayClient** | Windows tray app (kid's session) — remaining-time display, notifications, pairing code display |
| **EagleEye.ParentApp** | MAUI app (Android + iOS primary for production; Windows client for initial testing; macOS) — remote configuration, statistics, event feed |
| **EagleEye.Shared** | Class library — SignalR contracts, domain models, shared constants |

Core capabilities:

- **Allow-list enforcement**: only parent-approved applications may run; everything else is terminated
- **Time budgets**: per-application, per-weekday daily limits in minutes
- **Pause windows**: per-weekday time-of-day blocks during which all apps are denied
- **Real-time notifications**: budget warnings, enforcement events, pairing codes
- **Remote configuration**: parents manage rules from a mobile or macOS app over the LAN
- **Multi-user**: independent rules and statistics per Windows standard-user account
- **Usage statistics**: 90-day daily-granularity usage history per app per user

### 1.2 Quality Goals

| Priority | Quality Goal | Key Scenario |
|----------|-------------|--------------|
| 1 | **Reliability** | Service survives reboots, session changes, and parent-app disconnects without losing enforcement state or tracked time |
| 2 | **Security** | A standard-user child cannot stop, configure, or bypass the service; only authenticated parent apps may change rules |
| 3 | **Diagnosability** | Every component writes structured log files; debug mode exposes detailed traces with full stack traces and parameter values for rapid root-cause analysis |
| 4 | **Usability** | A non-technical parent can install EagleEye, pair their phone, and configure rules without CLI or certificate management |
| 5 | **Performance** | Process monitoring adds negligible CPU/memory overhead; tray overlay causes no visible frame drops |
| 6 | **Maintainability** | Component architecture with clear boundaries; API-first contracts; testable via DI and interfaces |

### 1.3 Stakeholders

| Role | Expectations |
|------|-------------|
| Parent | Easy remote configuration, reliable enforcement, usage visibility, real-time alerts |
| Kid | Fair warning before shutdowns, clear remaining-time display, no unexpected data loss |
| Developer (Michael) | Clean architecture, testable components, clear contracts, incremental buildability |

---

## 2. Architecture Constraints

### 2.1 Technical Constraints

| Constraint | Source |
|-----------|--------|
| .NET 10 for all components | `technology_selection.md` |
| .NET MAUI for ParentApp (Android, iOS, Windows via WinUI, macOS via Catalyst) | `technology_selection.md`, ADR-007 |
| Each build target is built on its owning machine: Windows machine (Service, TrayClient, ParentApp Windows + Android), MacBook (ParentApp Mac Catalyst) | ADR-007 |
| SignalR (ASP.NET Core) for all inter-component communication | `technology_selection.md`, ADR-001 |
| Windows service runs under SYSTEM account | `questions_and_answers.md` Q2.3 |
| Self-signed TLS certificate, auto-generated, non-expiring | `questions_and_answers.md` Q1.2 |
| Local LAN only (v1) — architecture must not block future WAN | `questions_and_answers.md` Q1.3 |
| PowerShell 7.6 for all scripts and automation | `technology_selection.md` |
| Inno Setup for Windows installer | `technology_selection.md` |
| xUnit + Moq for unit tests | ARC decision |
| SQLite for all persisted application data | Michael's decision (2026-09-20) |
| YAML for application configuration (one file per application) | Michael's decision (2026-09-20) |
| No CI/CD — local PowerShell builds only | `questions_and_answers.md` Q8.1 |

### 2.2 Organizational Constraints

| Constraint | Source |
|-----------|--------|
| Trunk-based development on `main` | `questions_and_answers.md` Q6.2 |
| Monorepo for all components | `questions_and_answers.md` Q6.1 |
| API-first: `EagleEye.Shared/Contracts/` defines interfaces before any implementation | `technology_selection.md` |
| Incremental user-story delivery — one story fully completed before starting the next | `technology_selection.md` |
| Two dev machines (Windows Developer Machine + MacBook) share one repo; the Windows machine is the manual test station | ADR-007 |
| Acceptance/E2E testing is manual (Michael), guided by TES; only unit tests are automated | ADR-007 |
| Android and iOS are the primary production platforms; the Windows parent app is the initial testing vehicle | Product requirements §3.3 (Michael, 2026-10-03) |

### 2.3 Conventions

| Convention | Detail |
|-----------|--------|
| Documentation format | Markdown |
| Diagrams | PlantUML (local Docker server at `http://localhost:8080`) |
| Decision records | ADR format in `docs/architecture/decisions/` |
| Localization | German (default) + English; all user-facing text externalized |

---

## 3. System Scope and Context

### 3.1 Business Context

The system operates entirely within a home LAN. The parent interacts through a mobile/desktop app; the child interacts through the Windows PC. There are no external systems, cloud services, or internet dependencies.

```plantuml
@startuml EagleEye Business Context
skinparam actorStyle awesome
skinparam packageStyle rectangle

actor "Parent" as parent
actor "Kid" as kid

rectangle "EagleEye System" as ee {
}

rectangle "Windows 11 PC" as winpc {
}

parent -right-> ee : Configures rules,\nviews statistics,\nreceives events
ee -right-> kid : Enforces rules,\nshows warnings,\ndisplays remaining time
ee --> winpc : Monitors processes,\nterminates applications
parent ..> ee : via LAN (SignalR/HTTPS)

note bottom of ee
  No internet dependency.
  No cloud services.
  All data stored locally on the Windows PC.
end note
@enduml
```

| Actor / System | Interaction |
|---------------|-------------|
| **Parent** | Connects from mobile/macOS app over LAN. Configures allow-lists, time budgets, pause windows. Views usage statistics. Receives real-time event notifications. |
| **Kid** | Logs into Windows with a standard-user account. Sees tray icon with remaining time. Receives budget/pause warnings. Subject to enforcement. |
| **Windows 11 OS** | Provides process information, user-account discovery, session events. Target of process termination calls. |

### 3.2 Technical Context

```plantuml
@startuml EagleEye Technical Context
skinparam componentStyle rectangle

node "Windows 11 PC" as winbox {
  component "EagleEye.Service\n(Windows Service, SYSTEM)" as svc
  component "EagleEye.TrayClient\n(Kid's user session)" as tray
  database "SQLite + YAML\n(%ProgramData%)" as store
}

node "Parent Device\n(Android / iOS / Windows / macOS)" as parentdev {
  component "EagleEye.ParentApp\n(.NET MAUI)" as app
}

package "EagleEye.Shared\n(compile-time dependency)" as shared {
}

app -down-> svc : SignalR over HTTPS\n(LAN, self-signed TLS, port 5443)
tray -down-> svc : SignalR over HTTP\n(loopback, port 5080)
svc -right-> store : SQLite + YAML
svc ..> shared : references
app ..> shared : references
tray ..> shared : references

note right of svc
  Parent endpoint: all interfaces, port 5443, HTTPS
  with auto-generated self-signed certificate.
  Tray endpoint: localhost:5080, HTTP (ADR-008).
end note
@enduml
```

| Channel | Protocol | Security | Direction |
|---------|----------|----------|-----------|
| ParentApp → Service | SignalR (WebSocket over HTTPS), port 5443 | Self-signed TLS (pinned after pairing) + pairing-based auth token | Bidirectional (hub pattern) |
| TrayClient → Service | SignalR (WebSocket over HTTP), `localhost:5080` | Loopback only (bound to the loopback endpoint, remote address checked); traffic never leaves the machine (ADR-008) | Bidirectional (hub pattern) |
| Service → Local Storage | SQLite database + YAML config file | Windows ACLs (SYSTEM-owned) | Read/Write |

---

## 4. Solution Strategy

### 4.1 Architecture Approach

| Strategy | Rationale |
|----------|-----------|
| **API-first contracts** | `EagleEye.Shared` defines all SignalR hub interfaces and DTOs before any component implements them. This decouples service and client development and makes the interface specification the single source of truth. |
| **Component-based service** | `EagleEye.Service` is decomposed into internal components by functional area (monitoring, enforcement, configuration, statistics, communication, certificates). Each component has a single responsibility and is injected via DI. |
| **SignalR as the universal transport** | One technology handles all three communication patterns: (1) data queries (parent→server→parent via hub method return values), (2) event pushes (server→clients via client callback interfaces), (3) state synchronization (server broadcasts full state snapshots to all connected clients on every mutation). Two hubs (`ParentHub`, `TrayHub`) separate the authenticated parent-app API from the read-only tray-client API. |
| **Server-authoritative state** | The Windows service is the single source of truth for all state. Clients never cache state as authoritative. Every state mutation triggers a full-state broadcast to all connected clients. On connect/reconnect, the server pushes a complete state snapshot. No delta synchronization — full snapshots eliminate ordering bugs and stale-cache drift. |
| **Localhost separation for TrayClient** | The TrayClient runs in the kid's unprivileged session and connects to the service on `localhost`. It is a display-only client — it cannot modify configuration. |
| **SQLite persistence** | All application data (per-user configuration, statistics, pairing credentials, app-name cache) is stored in a SQLite database per component, located in the OS-standard application data directory. SQLite provides ACID transactions, schema evolution, and efficient queries without an external database server. |
| **YAML application configuration** | Each application has exactly one YAML configuration file (named after the application) that provides all runtime parameters. YAML config files are also stored in the OS-standard application data directory. This separates deployment-time configuration (YAML, human-editable) from runtime application data (SQLite, machine-managed). |
| **Graceful-then-force termination** | Applications are terminated with `WM_CLOSE` first, then `Process.Kill()` after a configurable timeout. This gives users time to save work. |
| **Self-signed TLS, zero-touch** | The service generates a self-signed X.509 certificate on first run and stores it locally. All SignalR connections use HTTPS. No user action required. |

### 4.2 Technology Mapping

| Concern | Technology |
|---------|-----------|
| Service hosting | `Microsoft.Extensions.Hosting.WindowsServices` + Kestrel |
| SignalR server | ASP.NET Core SignalR |
| SignalR client (TrayClient, ParentApp) | `Microsoft.AspNetCore.SignalR.Client` |
| Process monitoring | `System.Diagnostics.Process` + Win32 API (via P/Invoke where needed) |
| TLS certificates | `System.Security.Cryptography.X509Certificates` |
| Cross-platform UI | .NET MAUI (Android, iOS, WinUI, Mac Catalyst) |
| TrayClient UI | WinForms (`NotifyIcon`, `Form` for overlay) |
| Data persistence | SQLite via `Microsoft.Data.Sqlite` (all application data) |
| Application configuration | YAML via `YamlDotNet` (one file per application for runtime parameters) |
| Logging | `Microsoft.Extensions.Logging` with the EagleEye rolling file provider (`EagleEye.Shared/Logging`, §8.10) |
| DI container | `Microsoft.Extensions.DependencyInjection` (built-in) |
| Unit testing | xUnit + Moq |
| Installer | Inno Setup |

---

## 5. Building Block View

### 5.1 Level 1 — System Decomposition

```plantuml
@startuml EagleEye Level 1
skinparam componentStyle rectangle
skinparam linetype ortho

package "Windows 11 PC" {
  component [EagleEye.Service] as SVC {
  }
  component [EagleEye.TrayClient] as TRAY {
  }
}

package "Parent Device" {
  component [EagleEye.ParentApp] as APP {
  }
}

package "Shared (compile-time)" {
  component [EagleEye.Shared] as SHARED {
  }
}

APP --> SVC : SignalR/HTTPS (LAN, 5443)
TRAY --> SVC : SignalR/HTTP (loopback, 5080)

SVC ..> SHARED : uses
APP ..> SHARED : uses
TRAY ..> SHARED : uses
@enduml
```

| Component | Responsibility | Technology |
|-----------|---------------|------------|
| **EagleEye.Service** | Process monitoring, enforcement, configuration management, statistics collection, SignalR hub, TLS certificate management, user account discovery, pairing, logging | .NET 10 Windows Service (Kestrel + SignalR) |
| **EagleEye.TrayClient** | System tray icon, remaining-time display, budget/pause notifications, optional overlay, pairing code display | .NET 10 WinForms |
| **EagleEye.ParentApp** | Connection management, pairing flow, allow-list configuration, budget/pause configuration, statistics display, event feed | .NET 10 MAUI |
| **EagleEye.Shared** | SignalR hub/client interface definitions, DTOs, domain models, shared constants, version info | .NET 10 class library (netstandard-compatible TFMs as needed) |

### 5.2 Level 2 — EagleEye.Service Internal Components

```plantuml
@startuml EagleEye Service Level 2
skinparam componentStyle rectangle

package "EagleEye.Service" {
  component [Communication\n(SignalR Hub)] as COMM
  component [Monitoring\n(Process Monitor)] as MON
  component [Enforcement\n(Process Terminator)] as ENF
  component [Configuration\n(Config Manager)] as CONF
  component [Statistics\n(Usage Tracker)] as STATS
  component [Certificates\n(TLS Manager)] as CERT
  component [UserAccounts\n(Account Discovery)] as USERS
  component [Pairing\n(Auth Manager)] as PAIR
  component [AppDiscovery\n(Installed Apps)] as APPS
  component [Logging\n(Log Manager)] as LOG
}

COMM --> CONF : reads/writes config
COMM --> STATS : queries statistics
COMM --> USERS : queries user accounts
COMM --> APPS : triggers scan
COMM --> PAIR : pairing flow
COMM --> MON : queries process state
COMM --> LOG : sets log level

MON --> ENF : triggers termination
MON --> STATS : reports usage time
MON --> CONF : reads rules (allow-list, budgets, pause windows)

ENF --> COMM : pushes enforcement events

CERT --> COMM : provides TLS certificate

PAIR --> COMM : pushes pairing code to tray
@enduml
```

| Internal Component | Responsibility |
|-------------------|---------------|
| **Communication** | Hosts two SignalR hubs on Kestrel, each bound to its own endpoint (ADR-008): `ParentHub` (`/hubs/parent`, HTTPS port 5443) for parent apps and `TrayHub` (`/hubs/tray`, HTTP `localhost:5080`) for tray clients. A default-deny hub filter allows only `[AllowUnpaired]` methods for unpaired parent connections. Routes incoming queries and write commands to the state owner of the affected state area, returns results. Pushes events and full-state snapshots to connected clients via SignalR groups (`Parents`, `Tray:{userSid}`). Every stored change is broadcast as a full snapshot with a revision to all paired apps, including the sender (ADR-010). Clients fetch the full state themselves after every (re)connect; the service does not push on connect. |
| **Monitoring** | Polls running processes at a configurable interval. Classifies processes as ignored/allowed/blocked. Tracks active usage time per allowed app. Detects pause-window and budget-expiry transitions. |
| **Enforcement** | Terminates processes using the two-phase graceful-then-force pattern (ADR-006): sends `WM_CLOSE` first, waits up to the configurable timeout (default 30s), then calls `Process.Kill(entireProcessTree: true)`. Used for blocked-app kills (immediate, no warning), budget-expiry shutdowns (after budget warnings), and pause-window shutdowns (after pause warnings). Pushes enforcement events to all connected clients. |
| **Configuration** | Reads and writes per-user configuration in the SQLite database (allow-lists, budgets, pause windows). Reads general settings from the YAML config file. Validates changes. Notifies other components on config update. |
| **Statistics** | Accumulates per-user, per-app daily usage minutes. Persists to SQLite. Serves historical data (up to 90 days). Purges old data. |
| **Certificates** | Generates a self-signed X.509 certificate on first run (ECDSA P-256, DPAPI-protected PFX, see §8.1). Loads and provides the certificate for the Kestrel HTTPS binding of the parent endpoint. |
| **UserAccounts** | Keeps the inventory of local standard (non-admin, non-built-in, not the setup leftover `defaultuser0`) Windows accounts, read via Win32 (`NetUserEnum`, `NetUserGetLocalGroups`) and re-checked every 15 s; changes are broadcast at once. State owner of the state area `UserAccounts` (ADR-010): stores per SID whether the account is under parental control (`AccountSelections`), keeps the selection of accounts that temporarily become admins, forgets it when the SID is deleted (US-003). |
| **Pairing** | Manages the pairing lifecycle: generates a 6-digit code on explicit request (`StartPairing`), binds it to the requesting connection, enforces 5-minute expiry and one guess per code, stores paired devices (token hash only) in SQLite. Sends the code to all tray clients, or to the Event Log when none is connected. Rejects unpaired clients. |
| **AppDiscovery** | Scans installed applications (registry, Start Menu, file metadata). Resolves human-readable display names. Caches the name dictionary. Supports re-scan on demand. |
| **Logging** | Configures the `Microsoft.Extensions.Logging` pipeline for file output with the EagleEye rolling file provider (`EagleEye.Shared/Logging`, §8.10; first used by the service in US-003). Supports runtime log-level switching (normal ↔ debug) via parent app command. Manages rolling log files (50 MB max, 3 files, 5 days) in the admin-only folder `%ProgramData%\EagleEye\logs\`. In debug mode, logs method traces with parameter values and full stack traces. Never logs sensitive data. |

### 5.3 Level 2 — EagleEye.Shared

```plantuml
@startuml EagleEye Shared Level 2
skinparam componentStyle rectangle

package "EagleEye.Shared" {
  package "Contracts" {
    interface IParentHub <<server hub>>
    interface IParentClientCallback <<client callback>>
    interface ITrayHub <<server hub>>
    interface ITrayClientCallback <<client callback>>
    class AllowUnpairedAttribute
  }
  package "Models" {
    class UserAccountDto
    class AppRuleDto
    class TimeBudgetDto
    class PauseWindowDto
    class UsageStatsDto
    class InstalledAppDto
    class PairedDeviceDto
    class ServiceVersionDto
    class AppBudgetStatusDto
    class PairingStatusDto
    class PairingResultDto
    class UserAccountListDto
    class StateWriteAckDto
  }
  package "Constants" {
    class HubRoutes
    class ServiceDefaults
    class PairingRules
  }
  package "Communication" {
    class ReconnectSchedule
    class ConnectBackoff
  }
  package "Data" {
    abstract class SqliteDatabase
  }
  package "Logging" {
    class RollingFileLoggerProvider
  }
}
@enduml
```

| Package | Contents |
|---------|----------|
| **Contracts** | `IParentHub` — methods the server exposes to parent apps (data queries with `Task<T>` return values, config/rule update commands, pairing). `IParentClientCallback` — callbacks the server invokes on parent apps (enforcement events, state-change broadcasts; empty in US-002). `ITrayHub` — methods the server exposes to tray clients (session registration, version query). `ITrayClientCallback` — callbacks the server invokes on tray clients (budget updates, warnings, pairing code display via `OnShowPairingCode`, enforcement notices). `AllowUnpairedAttribute` marks the `ParentHub` methods an unpaired connection may call (default deny, ADR-008). |
| **Models** | DTOs for all data exchanged over SignalR: user accounts, app rules, budgets, pause windows, statistics, installed apps, paired devices, version info, budget status, pairing status and result (`PairingOutcome`). All state data is used as full-state snapshots in server broadcasts. A snapshot DTO of a state area carries `Revision` and `LastChangeRequestId` (ADR-010 §3, e.g. `UserAccountListDto`); `StateWriteAckDto` acknowledges every write command. Only the DTOs a story needs are created; the list above is the target set. |
| **Constants** | Hub route paths (`HubRoutes`: `/hubs/parent`, `/hubs/tray`), default values (`ServiceDefaults`: ports 5080 and 5443, timeouts, warning thresholds), pairing validation rules (`PairingRules`: 6-digit code, device name length, 5-minute code lifetime). |
| **Communication** | Client-side reconnect timing used by TrayClient and ParentApp: `ReconnectSchedule` (automatic reconnect after a lost connection: 0, 2, 10 s, then every 30 s) and `ConnectBackoff` (initial connect: 1, 2, 4, 8, 16 s, capped at 30 s). |
| **Data** | `SqliteDatabase`: abstract base for every component database. One long-lived connection, pragmas (WAL, `synchronous=NORMAL`, `busy_timeout`, `foreign_keys`), `PRAGMA integrity_check`, ordered transactional migrations with a `SchemaVersion` table, serialized access. Used by the service and the parent app so all databases are configured identically (§8.4). |
| **Logging** | `RollingFileLoggerProvider`: the EagleEye file logging provider for `Microsoft.Extensions.Logging` (size-based rolling files, retention, line format; §8.10). Used by the service from US-003 on; TrayClient and ParentApp reuse it later. |

### 5.4 Level 2 — EagleEye.TrayClient

```plantuml
@startuml EagleEye TrayClient Level 2
skinparam componentStyle rectangle

package "EagleEye.TrayClient" {
  component [Communication\n(SignalR Client)] as TC_COMM
  component [UI\n(Tray Icon + Overlay)] as TC_UI
}

TC_COMM --> TC_UI : budget updates,\nwarnings,\npairing codes
TC_UI --> TC_COMM : user clicks (About, etc.)
@enduml
```

| Internal Component | Responsibility |
|-------------------|---------------|
| **Communication** | Connects to the service on `http://localhost:5080/hubs/tray`. Receives budget updates, warnings, pairing codes. Sends version query. Handles reconnect on disconnect (Shared `ReconnectSchedule`, `ConnectBackoff`). |
| **UI** | Manages the `NotifyIcon` (system tray), popup notifications (balloon tips), the pairing-code window (small topmost window, shown in the taskbar, closes at code expiry, replaced by a newer code; ADR-008 §6), remaining-time summary window, optional topmost overlay, About dialog, connection-status indicator. |

### 5.5 Level 2 — EagleEye.ParentApp

```plantuml
@startuml EagleEye ParentApp Level 2
skinparam componentStyle rectangle

The ParentApp component consists of two projects (ADR-009): `EagleEye.ParentApp.Core`, a plain `net10.0` class library with all logic that does not need MAUI, and `EagleEye.ParentApp`, the multi-targeted MAUI head with views and platform code. Dependencies: `ParentApp → ParentApp.Core → Shared`. Core builds and its unit tests run on both machines without the MAUI workload.

```plantuml
@startuml EagleEye ParentApp Level 2
skinparam componentStyle rectangle

package "EagleEye.ParentApp.Core (net10.0)" {
  component [Communication\n(SignalR client, trust policy,\nConnectionCoordinator)] as PA_COMM
  component [ViewModels\n(MVVM)] as PA_VM
  component [Data\n(SQLite stores)] as PA_DATA
  component [Abstractions\n(platform interfaces)] as PA_ABS
  component [AppTexts\n(resx de/en)] as PA_TXT
}

package "EagleEye.ParentApp (MAUI head)" {
  component [Views\n(MAUI pages)] as PA_VIEWS
  component [Services\n(platform implementations)] as PA_SVC
  component [Windows (WinUI)] as WIN
  component [macOS (Catalyst)] as MAC
  component [Android] as AND
  component [iOS] as IOS
}

PA_VIEWS --> PA_VM : data binding
PA_VM --> PA_COMM : commands / queries
PA_COMM --> PA_VM : state changes
PA_COMM --> PA_DATA : pairing, settings
PA_VM --> PA_TXT
PA_DATA --> PA_ABS : ISecretStore / ISecretProtector
PA_SVC ..|> PA_ABS : implements
PA_SVC --> WIN
PA_SVC --> MAC
PA_SVC --> AND
PA_SVC --> IOS
@enduml
```

| Internal Component | Project | Responsibility |
|-------------------|---------|---------------|
| **Communication** | Core | `ParentHubClient` wraps the SignalR `HubConnection` (Bearer token, certificate trust callback on both the HTTP handler and the WebSocket options). `CertificateTrustPolicy`: trust on first use, then pinned thumbprint (ADR-008 §3). `ConnectionCoordinator`: the connection and pairing state machine; never reports "connected" unless the service confirms the pairing; bounds every connect attempt and hub call to 15 s so an unreachable host ends in a clear error. `HostAddress` validates hostnames and IP addresses. `ParentHubGateway` gives feature models access to the current paired connection (calls, `Connected`/`Disconnected`, forwarded broadcasts) without growing the coordinator. `StateReplica<T>` applies the ADR-010 revision rule on the client. |
| **Feature models** (e.g. `Accounts/`) | Core | Client side of one state area each (ADR-010): fetch after every (re)connect, apply broadcasts, write with correlation id, confirmation and timeout. US-003: `UserAccountsModel`. |
| **ViewModels** | Core | MVVM view models (CommunityToolkit.Mvvm) for each screen. Expose commands and observable properties. US-002: main window/navigation, status bar, appearance, server connection. US-003: user accounts section. |
| **Data** | Core | `ParentDatabase` (Shared `SqliteDatabase`), `PairingStore`, `SettingsStore`, `ProtectedSecretStore` (`ISecretStore` over the `Secrets` table; encryption delegated to `ISecretProtector`). See §8.4. |
| **Abstractions** | Core | Small platform interfaces Core needs: `ISecretStore`, `ISecretProtector`, `IThemeService`, `IDialogService`, `IUiDispatcher`, `IAppDataPaths`. |
| **AppTexts** | Core | All user-facing parent-app texts (German default, English). |
| **Views** | MAUI head | MAUI pages/views. Desktop layout (Windows, macOS): navigation menu, content region, status bar. Later: user selection, allow-list, budgets, pause windows, statistics, event feed, paired devices. |
| **Services / Platform-Specific** | MAUI head | Implementations of the Core abstractions (theme, dialogs, main-thread dispatch, data paths, secret storage: `DpapiSecretProtector` on Windows, `MauiSecureStorageSecretStore` elsewhere). Android/iOS (primary production platforms): portrait lock, platform entry points. macOS (Catalyst) and Windows (WinUI): desktop window sizing. Windows + Android targets are built on the Windows machine; Mac Catalyst + iOS on the MacBook (ADR-007). |

**Windows parent app** (product requirements §3.3.9): the initial testing vehicle for parent-side features. It is a normal `ParentHub` client with no special local access path. On the service PC it connects through the same TLS + pairing flow as a remote client (to `localhost` or the machine's own hostname). It is installed with its own per-user installer (ADR-009, §7.4).

---

## 6. Runtime View

### 6.1 Parent App Pairing

```plantuml
@startuml Pairing Sequence
actor Parent
participant "ParentApp" as APP
participant "Service\n(SignalR Hub)" as SVC
participant "TrayClient" as TRAY
actor Kid

Parent -> APP : Enter hostname, connect
APP -> SVC : SignalR connect (HTTPS 5443,\ntrust on first use: remember thumbprint)
APP -> SVC : StartPairing()
SVC -> SVC : Generate 6-digit code\n(bound to this connection, 5 min expiry,\nreplaces any pending code)
SVC -> TRAY : OnShowPairingCode(code)\n(all tray clients; Event Log if none)
TRAY -> Kid : Topmost pairing-code window
Kid -> Parent : Reads code aloud / shows screen
Parent -> APP : Enter code + device name
APP -> SVC : SubmitPairingCode(code, deviceName)
SVC -> SVC : Validate code, connection, expiry\n(any failure invalidates the code)
SVC -> SVC : Store paired device\n+ SHA-256 hash of token
SVC -> APP : PairingResultDto(Success, deviceId, token)
APP -> APP : Store pairing + pinned thumbprint,\ntoken encrypted (ISecretStore)
APP -> SVC : Reconnect with Bearer token
APP -> SVC : GetPairingStatus() → paired

note over SVC
  All subsequent connections send
  the token as Bearer header and accept
  only the pinned certificate (ADR-008)
end note
@enduml
```

### 6.2 Process Monitoring and Enforcement

```plantuml
@startuml Process Monitoring
participant "Monitoring" as MON
participant "Configuration" as CONF
participant "Enforcement" as ENF
participant "Statistics" as STATS
participant "Communication" as COMM
participant "TrayClient" as TRAY
participant "ParentApp" as APP

loop Every polling interval
  MON -> MON : Enumerate processes\nfor all standard-user sessions
  MON -> CONF : Get rules for user
  
  alt Process is IGNORED
    MON -> MON : Skip (no action)
  else Process is BLOCKED (not on allow-list)
    MON -> ENF : Terminate(process)
    ENF -> ENF : Send WM_CLOSE
    ENF -> ENF : Wait timeout
    ENF -> ENF : Force-kill if still running
    ENF -> COMM : Event: BlockedAppTerminated
    COMM -> TRAY : Push notification
    COMM -> APP : Push event
  else Process is ALLOWED
    MON -> STATS : TrackUsage(user, app, elapsed)
    MON -> CONF : Check budget remaining
    
    alt Budget warning threshold reached
      MON -> COMM : Event: BudgetWarning
      COMM -> TRAY : Push warning notification
      COMM -> APP : Push event
    else Budget expired
      MON -> ENF : Terminate(process)
      ENF -> COMM : Event: BudgetExpired
      COMM -> TRAY : Push shutdown notification
      COMM -> APP : Push event
    end
  end
end
@enduml
```

### 6.3 Pause Window Enforcement

```plantuml
@startuml Pause Window
participant "Monitoring" as MON
participant "Configuration" as CONF
participant "Enforcement" as ENF
participant "Communication" as COMM
participant "TrayClient" as TRAY

MON -> CONF : Check pause windows for user
CONF -> MON : Pause window starts in 5 min

MON -> COMM : Event: PauseWarning(early)
COMM -> TRAY : "Pause begins in 5 minutes"

... 4 minutes later ...

MON -> COMM : Event: PauseWarning(last)
COMM -> TRAY : "Pause begins in 1 minute"

... 1 minute later ...

MON -> CONF : Pause window now active
MON -> MON : Enumerate all non-ignored\nprocesses for user
loop Each running allowed app
  MON -> ENF : Terminate(process)
end
MON -> COMM : Event: PauseWindowActive
COMM -> TRAY : "Pause window active —\nall apps closed"
@enduml
```

### 6.4 Configuration Update from Parent App

Every write by a parent app follows ADR-010. The example uses a later configuration area (time budgets); US-003 implements the same path for the area `UserAccounts` (`SetParentalControl`).

```plantuml
@startuml Config Update
actor Parent
participant "ParentApp 1\n(sender)" as APP
participant "ParentHub" as SVC
participant "State owner\n(one writer per area)" as CONF
database "SQLite" as DB
participant "ParentApp 2" as APP2
participant "TrayClient" as TRAY

Parent -> APP : Change time budget\nfor Minecraft to 60 min
APP -> APP : show requested value as pending\n(requestId r)
APP -> SVC : SetTimeBudget(r, user, app, budget)
SVC -> SVC : default-deny filter: paired connection?
SVC -> CONF : delegate (validated input, device name)
activate CONF
CONF -> DB : store (transaction)
CONF -> CONF : revision n → n+1, log
CONF -> SVC : broadcast snapshot to group "Parents"
SVC -> APP : OnXxxChanged(rev n+1, r, full snapshot)
SVC -> APP2 : OnXxxChanged(rev n+1, r, full snapshot)
SVC -> TRAY : Push: BudgetChanged\n(own group Tray:{sid}, later story)
deactivate CONF
SVC --> APP : StateWriteAckDto(n+1)
APP -> APP : snapshot with own requestId applied\n→ write confirmed
APP2 -> APP2 : rev n+1 > last → apply, update view
note over APP
  Rejected (HubException) or not confirmed
  within the area's write timeout:
  show the last confirmed snapshot again,
  show an error, re-fetch if the outcome is unknown.
end note
@enduml
```

### 6.5 TrayClient Startup and Reconnect

```plantuml
@startuml TrayClient Startup
participant "Windows Session" as WIN
participant "TrayClient" as TRAY
participant "Service" as SVC

WIN -> TRAY : Auto-start on user logon\n(registry Run key)
TRAY -> TRAY : Show tray icon (red = disconnected)
TRAY -> SVC : SignalR connect (localhost)

alt Service is running
  SVC -> TRAY : Connection established
  TRAY -> TRAY : Tray icon → green
  SVC -> TRAY : Push current budgets\nfor this user
  TRAY -> TRAY : Display remaining times
else Service not yet started
  TRAY -> TRAY : Retry with exponential backoff
  ... Service starts ...
  TRAY -> SVC : SignalR connect (localhost)
  SVC -> TRAY : Connection established
  TRAY -> TRAY : Tray icon → green
end

note over TRAY
  On disconnect: icon → red,
  resume retry loop.
  Enforcement continues
  server-side regardless.
end note
@enduml
```

### 6.6 Service-Originated Change and (Re)connect of a Parent App

ADR-010 §2 and §7, with the account inventory of US-003 as the example. The service checks the Windows accounts every 15 s and broadcasts only when something changed. A parent app fetches every area it shows after each (re)connect.

```plantuml
@startuml State Change and Reconnect
participant "Windows\n(local accounts)" as WIN
participant "AccountInventoryMonitor\n(every 15 s)" as MON
participant "UserAccountService\n(state owner)" as UAS
participant "ParentHub" as HUB
participant "ParentApp A\n(connected)" as A
participant "ParentApp B\n(reconnecting)" as B

WIN -> WIN : account renamed / added /\ndeleted / admin rights changed
MON -> UAS : RefreshInventoryAsync()
UAS -> WIN : read accounts
UAS -> UAS : differs → update stored selections,\nrevision n → n+1, log
UAS -> HUB : broadcast (rev n+1, LastChangeRequestId = null)
HUB -> A : OnUserAccountsChanged → view updated

B -> HUB : connect (Bearer token)\n→ joins group "Parents"
B -> HUB : GetPairingStatus() → paired
B -> B : reset revisions, show "Loading …"
B -> HUB : GetUserAccounts()
HUB --> B : snapshot (rev n+1)
note over B
  A broadcast arriving during the fetch is applied
  by the revision rule; the older of the two is ignored.
end note
@enduml
```

---

## 7. Deployment View

```plantuml
@startuml EagleEye Deployment
skinparam nodeStyle rectangle

node "Windows 11 PC (x64)" as winpc {
  node "SYSTEM Session" {
    artifact "EagleEye.Service.exe" as svc_exe
  }
  node "Kid's User Session" {
    artifact "EagleEye.TrayClient.exe" as tray_exe
  }
  folder "%ProgramFiles%\\EagleEye\\" as install {
    artifact "EagleEye.Service.exe"
    artifact "EagleEye.TrayClient.exe"
    artifact "EagleEye.Shared.dll"
  }
  folder "%ProgramData%\\EagleEye\\" as data {
    artifact "EagleEye.Service.yaml" as svc_yaml
    artifact "EagleEye.TrayClient.yaml" as tray_yaml
    database "EagleEye.Service.db\n(SQLite)" as svc_db
    database "EagleEye.TrayClient.db\n(SQLite)" as tray_db
    artifact "EagleEye.Service-NNN.log" as svc_log
    artifact "EagleEye.TrayClient-NNN.log" as tray_log
    folder "certs/ (SYSTEM + Administrators only)" {
      artifact "eagleeye.pfx\n(DPAPI LocalMachine)"
    }
  }
  component "Tray endpoint\nhttp://localhost:5080/hubs/tray" as ep_tray
  component "Parent endpoint\nhttps://*:5443/hubs/parent" as ep_parent
  component "Windows Firewall rule\nTCP 5443, LocalSubnet" as fw
}

tray_exe --> ep_tray : loopback

node "Parent Device" as parentdev {
  node "Windows 11 (per-user install)" {
    artifact "EagleEye.ParentApp.exe\n(WinUI, unpackaged, self-contained)\n%LocalAppData%\\Programs\\EagleEye Parent App\\\nsame PC as service or remote"
    database "%LocalAppData%\\EagleEye\\\nEagleEye.ParentApp.db" as win_db
  }
  node "macOS 26+" {
    artifact "EagleEye.ParentApp\n(.app bundle via .dmg)"
    artifact "EagleEye.ParentApp.yaml" as mac_yaml
    database "EagleEye.ParentApp.db\n(SQLite)" as mac_db
  }
  node "iOS 26+" {
    artifact "EagleEye.ParentApp\n(sideloaded via Xcode)"
  }
  node "Android 14+" {
    artifact "EagleEye.ParentApp\n(sideloaded via adb)"
  }
}

parentdev --> fw : HTTPS (SignalR)\nPort 5443\nLAN (local subnet) only
fw --> ep_parent
@enduml
```

**Endpoints and ports** (ADR-008): the service listens on two Kestrel endpoints. Each hub is bound to its endpoint by the local port of the TCP connection; a request for a hub on the wrong port gets `404`.

| Endpoint | Binding | Protocol | Hub | Reachable from |
|---|---|---|---|---|
| Tray | `localhost:5080` (IPv4 and IPv6 loopback) | HTTP | `TrayHub` `/hubs/tray` | Same PC only; the hub also rejects non-loopback remote addresses |
| Parent | all interfaces, port 5443 | HTTPS (TLS 1.2+) | `ParentHub` `/hubs/parent` | LAN and the same PC; firewall rule "EagleEye Service (Parent apps)" allows TCP 5443 for `EagleEye.Service.exe` from the local subnet, all profiles |

Both ports are constants (`ServiceDefaults`) until the service gets its YAML configuration (ADR-002).

### 7.1 Windows Installation Layout

| Path | Contents | Owner |
|------|----------|-------|
| `%ProgramFiles%\EagleEye\` | Service executable, TrayClient executable, shared DLLs, dependencies | Installer (admin) |
| `%ProgramData%\EagleEye\EagleEye.Service.yaml` | Service runtime configuration (port, timeouts, warning thresholds, log level) | SYSTEM (r/w), admin (r/w) |
| `%ProgramData%\EagleEye\EagleEye.TrayClient.yaml` | TrayClient runtime configuration (service URL, overlay preferences) | SYSTEM (r/w), admin (r/w) |
| `%ProgramData%\EagleEye\EagleEye.Service.db` | SQLite database — per-user config, statistics, paired devices, app-name cache | SYSTEM (r/w) |
| `%ProgramData%\EagleEye\EagleEye.TrayClient.db` | SQLite database — cached display state (optional, lightweight) | Standard user (r/w) |
| `%ProgramData%\EagleEye\logs\EagleEye.Service-NNN.log` | Service rolling log files (50 MB max, 3 files, 5 days) | SYSTEM and Administrators only; folder `logs\` with inheritance removed, set by the installer and re-applied at every service start (US-003, FR-SVC-100) |
| `%ProgramData%\EagleEye\EagleEye.TrayClient-NNN.log` | TrayClient rolling log files (50 MB max, 3 files) | Standard user (r/w) |
| `%ProgramData%\EagleEye\certs\` | Auto-generated self-signed certificate `eagleeye.pfx` (DPAPI LocalMachine, §8.1) | SYSTEM and Administrators only (inheritance removed) |

The service installer sets the ACLs with well-known SIDs (works on German Windows): `%ProgramData%\EagleEye\` SYSTEM and Administrators full control, Users read; `certs\` SYSTEM and Administrators only (ADR-008 §7). Uninstalling the service keeps `%ProgramData%\EagleEye\`, so a reinstall keeps the certificate and the pairings.

### 7.1.1 Parent App Data Locations

Each parent app instance stores its YAML config and SQLite database in the OS-standard application data directory:

| Platform | Config File | Database | Location |
|----------|------------|----------|----------|
| Windows | `EagleEye.ParentApp.yaml` | `EagleEye.ParentApp.db` | `%LocalAppData%\EagleEye\` (per parent user; removed by the parent app uninstaller) |
| macOS | `EagleEye.ParentApp.yaml` | `EagleEye.ParentApp.db` | `~/Library/Application Support/EagleEye/` |
| iOS | `EagleEye.ParentApp.yaml` | `EagleEye.ParentApp.db` | App sandbox `Documents/` |
| Android | `EagleEye.ParentApp.yaml` | `EagleEye.ParentApp.db` | App internal storage |

### 7.2 Windows Service Registration

- Registered via Inno Setup as an auto-start Windows service under the SYSTEM account
- Service name: `EagleEyeService`
- Start type: Automatic (starts on boot)
- Recovery: Restart on failure (first, second, subsequent)

### 7.3 TrayClient Auto-Start

- Registered via Inno Setup in `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` for each standard user, or via `HKLM` with a per-user launch mechanism
- Starts automatically when a standard-user session begins
- Does not require admin privileges to run
- Also starts in admin sessions (HKLM Run key). Michael's decision (2026-10-04): this stays, so a parent pairing at the service PC as admin sees the pairing code there. Admin sessions are still never monitored (§8.8).

### 7.4 Parent App Distribution

| Platform | Distribution | Package |
|----------|-------------|---------|
| Windows 11 | Own Inno Setup installer, **per user**, no admin rights (ADR-009, FR-APP-092). Unpackaged, self-contained (`win-x64`, .NET runtime and Windows App SDK included, no MSIX). Installs to `%LocalAppData%\Programs\EagleEye Parent App\`; repair/update keeps the app data; uninstall removes program folder, Start menu entry and `%LocalAppData%\EagleEye\`. Independent of the service installer (own `AppId`). Runs on the service PC or remotely. | `03_Delivery/windows/EagleEye-ParentApp-Setup-<version>.exe` |
| macOS 26+ | Direct download | `.dmg` with `.app` bundle |
| iOS 26+ (built on MacBook) | Sideload | Xcode / `ios-deploy` |
| Android 14+ | Sideload (built on Windows machine) | `adb install` APK |

---

## 8. Cross-cutting Concepts

### 8.1 TLS and Certificate Management

The service uses HTTPS for the parent endpoint (port 5443). The tray endpoint is plain HTTP on loopback and needs no certificate (§8.3, ADR-008). On first startup (`Certificates/CertificateManager`):

1. **Generate**: Create a self-signed X.509 certificate: ECDSA P-256, `CN=EagleEye`, SAN = machine DNS name and `localhost`, server-authentication EKU, 100-year validity.
2. **Store**: Save as `%ProgramData%\EagleEye\certs\eagleeye.pfx`. The PFX bytes are encrypted with DPAPI (`DataProtectionScope.LocalMachine`), and the `certs\` folder is restricted by ACL to SYSTEM and Administrators (set by the installer). The ACL is the actual protection; DPAPI only keeps the file useless outside this machine.
3. **Load the key**: with `X509KeyStorageFlags.MachineKeySet` (the service runs as SYSTEM). Not `EphemeralKeySet`: SChannel cannot use an ephemeral key for a TLS server. If the machine key store is not writable (only when the service runs in console mode without admin rights, e.g. DEV smoke checks), the import falls back to `UserKeySet` and logs a warning (US-002 deviation D-1).
4. **Bind**: Configure Kestrel to use the certificate for HTTPS on port 5443.
5. **Subsequent starts**: Load the existing certificate from disk. It is regenerated only if the file is missing or unreadable (logged as a warning). A regenerated certificate breaks the pin of every paired app (see below).

The certificate is never installed into an OS certificate store, on either side.

**Parent app trust** (ADR-008 §3): the SignalR client sets the validation callback on both the HTTP handler and the WebSocket options (`ClientWebSocketOptions.RemoteCertificateValidationCallback`), otherwise the WebSocket transport ignores it.

- **Unpaired**: trust on first use. The app accepts the certificate the service presents and remembers its SHA-256 thumbprint for the pairing in progress.
- **On successful pairing**: the thumbprint is stored with the pairing.
- **Paired**: the app accepts **only** the pinned thumbprint. A different certificate is a connection error with its own message ("the identity of the EagleEye PC has changed"). Recovery in US-002: reinstall the app (removes the pairing); a guided recovery follows with device management (FR-APP-015).

### 8.2 Authentication and Authorization

See ADR-004 for the full rationale and pairing flow details.

| Client | Auth Mechanism | Permissions |
|--------|---------------|------------|
| **Paired ParentApp** | Persistent token (256-bit, cryptographically random) issued during pairing | Full: read/write config, read stats, read users, manage pairings, set log level |
| **Unpaired ParentApp** | None (anonymous) | Only the `ParentHub` methods marked `[AllowUnpaired]`: `GetPairingStatus`, `StartPairing`, `SubmitPairingCode`. Everything else is rejected by a default-deny hub filter (ADR-008 §4). |
| **TrayClient** | Loopback endpoint only (`localhost:5080`, remote address checked) + session identity (user SID) | Read-only: receive own user's budget info, warnings, pairing codes. No config changes. |

**Pairing flow** (ADR-004, made concrete by ADR-008 §4): parent app connects unpaired → calls `StartPairing()` → service generates a 6-digit code bound to that connection (5-minute expiry, replaces any pending code) → code displayed in a topmost window by every connected TrayClient (or written to the Windows Event Log, source `EagleEye`, event ID 1000, if no TrayClient is connected) → parent enters code + device name → `SubmitPairingCode` → service validates (any failure invalidates the code: one guess per code) → issues a 256-bit token → both sides store it. The code is requested explicitly, not on connect, so reconnects do not push codes.

**Token security**: Tokens are generated via `System.Security.Cryptography.RandomNumberGenerator`. The service stores only the **SHA-256 hash** of each token in the `PairedDevices` SQLite table, never the plaintext. The parent app stores the token encrypted through its `ISecretStore` (Windows: DPAPI `CurrentUser`, see §8.4; Android/iOS/macOS: MAUI `SecureStorage`). Tokens are transmitted only over TLS and never appear in logs.

**Subsequent connections**: The parent app sends its token as `Authorization: Bearer <token>` (SignalR `AccessTokenProvider`). `ParentHub.OnConnectedAsync` hashes it, looks up the hash in `PairedDevices`, and marks the connection as paired if found (and adds it to the `Parents` group). Unmatched tokens leave the client unpaired; the client learns this via `GetPairingStatus()` after every (re)connect and then deletes its local pairing.

**Device management**: Multiple parent apps may be paired simultaneously. Any paired app can view all paired devices and de-register any device (including itself). De-registration deletes the token hash; the affected device must re-pair.

### 8.3 SignalR Communication Design

EagleEye uses two SignalR hubs, three communication patterns, and a server-authoritative state model. See ADR-003 for the full rationale.

#### Two Hubs

| Hub | Route | Client | Auth | Role |
|-----|-------|--------|------|------|
| `ParentHub` | `/hubs/parent` (HTTPS, port 5443, all interfaces) | ParentApp | Pairing credential required (except `[AllowUnpaired]` methods) | Queries, commands, receives events and state broadcasts |
| `TrayHub` | `/hubs/tray` (HTTP, `localhost:5080`) | TrayClient | Loopback + user SID | Registers session, receives budget info, warnings, pairing codes |

Both hubs run in the same Kestrel process, but on **separate endpoints** (ADR-008 §1, amends the earlier "same port, same certificate" design). Each hub is bound to its endpoint by the **local port** of the TCP connection (`HttpContext.Connection.LocalPort`, `HubEndpointGuard`), never by the `Host` header: `/hubs/tray` on any port other than 5080 and `/hubs/parent` on any port other than 5443 return `404`. A LAN client can therefore never reach the `TrayHub`, which receives pairing codes. The `TrayHub` additionally rejects non-loopback remote addresses. The tray traffic never leaves the machine, so it uses plain HTTP; moving it to TLS later needs no contract change.

**Client timing**: both clients reconnect after a lost connection with the Shared `ReconnectSchedule` (0, 2, 10 s, then every 30 s) and retry the initial connect with `ConnectBackoff` (1, 2, 4, 8, 16, then 30 s). The parent app additionally bounds each connect attempt and each hub call to 15 s, so an unreachable host produces a clear error instead of hanging for the OS TCP timeout (US-002 deviation D-10).

#### Pattern 1 — Data Queries (request/response)

Parent apps invoke methods on `ParentHub` that return `Task<T>`. The server processes the request and returns the result on the same connection. This covers: querying user accounts, configuration, statistics, installed apps, paired devices, and service version.

The TrayClient has no query methods beyond `RegisterSession` and `GetServiceVersion`. It receives all data via server push.

#### Pattern 2 — Event Pushes (server → clients)

The server pushes events to clients by invoking methods on the client callback interfaces (`IParentClientCallback`, `ITrayClientCallback`). Examples: enforcement events (blocked app terminated, budget expired), system events (user session started/ended), pairing code display. Pushes are targeted via SignalR groups:

| Group | Members | Purpose |
|-------|---------|---------|
| `Parents` | All authenticated parent app connections | Config changes, enforcement events, account/app list changes |
| `Tray:{userSid}` | TrayClient for a specific user session | Budget updates, warnings, pairing codes — scoped to that user |

#### Pattern 3 — State Synchronization (server-authoritative, event-driven)

The Windows service is the **single source of truth**. Changes are propagated event-driven through the service: a parent app sends its change to the service, the service stores it and broadcasts the stored state to all connected apps, including the sender. The sender uses the broadcast to confirm its write; the other apps use it as the trigger to update their views and local data. See ADR-010 (refines ADR-003 Pattern 3) for the full rules and rationale.

1. **State areas.** State is partitioned into areas (US-003: `UserAccounts`; later e.g. `UserConfig` per SID, `PairedDevices`, `GeneralSettings`). Each area has a snapshot DTO with `Revision` and `LastChangeRequestId`, a query `GetXxx()`, a broadcast `OnXxxChanged(snapshot)` and write commands `SetXxx(Guid requestId, …)` returning `StateWriteAckDto(Revision)`.

2. **Every stored change triggers one broadcast.** The area's state owner in the service serializes writes, stores, increments the revision, logs, broadcasts the **full snapshot** to the group `Parents` (all paired connections, **including the sender**) and then returns the ack. Changes the service makes itself (e.g. Windows accounts changed) are broadcast the same way with `LastChangeRequestId = null`. Last write received wins.

3. **Confirmation.** The broadcast arrives before the ack (same connection, in order). The sender's write is confirmed when it applied a snapshot with its own `requestId` or a revision ≥ the ack's revision. Rejection (`HubException`) or no confirmation within the area's write timeout → the app shows the last confirmed snapshot again, shows an error, and re-fetches if the outcome is unknown.

4. **Ordering by revision.** Revisions are per area, in memory, strictly increasing during one service run. Clients apply a snapshot only if its revision is higher than the last applied one; this resolves query results and broadcasts overtaking each other.

5. **Client fetches on (re)connect.** After every (re)connect with confirmed pairing, the client resets its revisions and fetches every area it shows. The service does not push state on connect (changed from the original ADR-003 Rule 3). Missed events are not replayed; the fetch is the recovery.

6. **Full state snapshots, not deltas.** Large state is split into keyed areas rather than sent as deltas.

7. **Clients never treat replicas as authoritative**, never poll, and never talk to each other. How the service learns about changes in its environment (events or periodic checks) is internal to the service.

8. **Only paired apps.** Broadcasts go only to `Parents`; queries and writes are behind the default-deny filter (ADR-008 §4). Tray clients receive the state they display through their own groups and callbacks (later stories) and never write.

### 8.4 Data Persistence (SQLite)

All application data is stored in SQLite databases. Each EagleEye component has its own database file, located in the OS-standard application data directory for that platform.

**EagleEye.Service** (`EagleEye.Service.db`) tables include:

- **UserConfig** — per-user allow-list, per-app time budgets (7 weekdays), pause windows (7 weekdays). Keyed by Windows SID (survives username renames).
- **UsageStatistics** — per-user, per-app daily usage minutes. Indexed by date. Rows older than 90 days are purged automatically during the midnight maintenance cycle.
- **PairedDevices** — registered parent apps with device names and authentication credentials.
- **AccountSelections** — per Windows SID whether the account is under parental control (US-003, FR-SVC-072 to FR-SVC-074). A row exists only for accounts the parent has ticked or unticked; no row = not under parental control. Rows of accounts that became admins are kept; rows of deleted SIDs are removed. The account inventory itself is not stored: it is read from Windows. State revisions (ADR-010) are not stored either.
- **AppNameCache** — dictionary mapping executable names to resolved human-readable display names.
- **IgnoreList** — shipped list of essential Windows process names (read-only at runtime, seeded by installer/migration).

**EagleEye.ParentApp** (`EagleEye.ParentApp.db`) tables include:

- **ServerConnections** — stored service endpoints (hostname/IP), device ID, device name, pinned certificate thumbprint (SHA-256) and pairing time. At most one row in US-002.
- **AppSettings** — key/value settings (e.g. `appearance.theme`).
- **Secrets** — the pairing token, encrypted. On Windows, `ProtectedSecretStore` (Core) stores the bytes returned by `ISecretProtector`, implemented by `DpapiSecretProtector` (DPAPI `CurrentUser`, platform code). MAUI `SecureStorage` is not used on Windows because it needs package identity and the app is unpackaged (ADR-009). Other platforms use `MauiSecureStorageSecretStore` (Keychain/Keystore) and do not use this table.
- **CachedState** (planned) — last-known configuration and statistics for offline display.

The service and the parent app derive their databases from the Shared `SqliteDatabase` base (§5.3).

**EagleEye.TrayClient** (`EagleEye.TrayClient.db`) — optional, lightweight cache for display state.

SQLite is accessed via `Microsoft.Data.Sqlite`. All write operations use transactions. WAL (Write-Ahead Logging) mode is enabled for concurrent read performance. Schema migrations are versioned and applied automatically on startup.

### 8.5 Application Configuration (YAML)

Each application has exactly one YAML configuration file, named after the application, that provides all runtime parameters. YAML files are stored in the OS-standard application data directory alongside the SQLite database.

**Separation principle**: YAML files contain *deployment-time configuration* — values that an administrator might edit before or between runs (ports, paths, timeouts, feature toggles). SQLite databases contain *runtime application data* — values that the application creates and manages during operation (user rules, statistics, credentials).

**EagleEye.Service.yaml** — example structure:

```yaml
server:
  port: 5443            # parent endpoint (HTTPS); tray endpoint is localhost:5080
  graceful_shutdown_timeout_seconds: 30
enforcement:
  poll_interval_seconds: 5
  budget_early_warning_minutes: 5
  budget_last_warning_minutes: 1
  pause_early_warning_minutes: 5
  pause_last_warning_minutes: 1
logging:
  debug_mode: false     # true = Debug+Trace levels enabled
```

**EagleEye.TrayClient.yaml** — example structure:

```yaml
service:
  url: http://localhost:5080    # tray endpoint, loopback only (ADR-008)
overlay:
  enabled: false
  opacity: 0.8
logging:
  debug_mode: false
```

**EagleEye.ParentApp.yaml** — example structure:

```yaml
display:
  language: de    # de | en
logging:
  debug_mode: false
```

YAML files are parsed at startup using `YamlDotNet`. If a YAML file is missing, the application creates one with default values on first run.

### 8.6 Statistics Retention

Usage statistics in `EagleEye.Service.db` are retained for 90 days at daily granularity (minutes of use per application per day per user). Data older than 90 days is purged automatically during the daily midnight maintenance cycle.

### 8.7 Time Tracking and Budget Reset

- The Monitoring component tracks elapsed time per allowed process per user, accumulating at each poll interval
- Tracked time is persisted to the SQLite database periodically (every N minutes and on service shutdown) to survive service restarts
- At midnight (local time), all daily budgets reset. Unused time does not carry over. The midnight transition also triggers statistics persistence and old-data purge
- Budget countdown pauses during pause windows (time during pause does not consume budget)

### 8.8 Process Classification

See ADR-005 for the full rationale, ignore-list examples, and alternatives considered.

Every process running under a standard-user session is classified into exactly one of three tiers, evaluated in priority order:

| Tier | Category | Source | Matching | Action |
|------|----------|--------|----------|--------|
| 1 | **Ignored** | Shipped, developer-maintained, non-configurable | Executable name (case-insensitive) in `IgnoreList` SQLite table | Skip — never terminated, never tracked |
| 2 | **Allowed** | Parent-configured per user via parent app | Executable name (case-insensitive) in user's allow-list (`UserConfig` SQLite table) | Track usage time, enforce budget and pause rules |
| 3 | **Blocked** | Default — everything not in tiers 1 or 2 | N/A | Terminate immediately (graceful-then-force, ADR-006) |

**Evaluation**: Both the ignore list and allow-list are loaded into in-memory hash sets for O(1) lookup, refreshed on configuration change. Classification runs on every poll cycle for every process in every standard-user session. Admin sessions are never monitored (per MU-014).

**Deny-by-default**: the blocked tier is the default. Any new or unknown application is blocked unless the parent has explicitly allowed it. This guarantees no gaps in enforcement.

**Display-name resolution**: Handled by AppDiscovery (file version info → installed-programs registry → optional online lookup → executable name fallback). Cached in the SQLite `AppNameCache` table. Display names are for the parent-app UI only — classification always uses the executable file name.

### 8.9 Process Termination Pattern

See ADR-006 for the full rationale, alternatives considered, and the complete termination sequence.

All process terminations use a **two-phase graceful-then-force pattern**:

| Phase | Mechanism | Timeout |
|-------|-----------|---------|
| 1. Graceful | `WM_CLOSE` to the process's main window (via `Process.CloseMainWindow()`) | Configurable (default: 30s, min: 5s, max: 120s) |
| 2. Force | `Process.Kill(entireProcessTree: true)` — kills the process and all child processes | Immediate |

If the process has no main window, Phase 1 is skipped and Phase 2 executes immediately.

**Termination triggers and warning behavior**:

| Trigger | Pre-termination Warnings | Termination |
|---------|------------------------|-------------|
| **Blocked app** (Tier 3) | None — immediate | Graceful-then-force on detection |
| **Budget expired** | Early warning (default: 5 min before) + last warning (default: 1 min before) | Graceful-then-force when budget reaches zero |
| **Pause window** | Early warning (default: 5 min before) + last warning (default: 1 min before) | Graceful-then-force when pause window begins |

Warning thresholds are independently configurable for budget and pause scenarios via `EagleEye.Service.yaml`. Warnings are advisory — they do not delay enforcement.

The timeout is a global setting (`enforcement.graceful_shutdown_timeout_seconds` in `EagleEye.Service.yaml`), applied uniformly to all termination scenarios.

### 8.10 Logging Strategy

Diagnosability is a primary design principle: **every EagleEye component writes log files**. Logging is not optional or service-only — the Service, TrayClient, and ParentApp all log to files using the same framework and conventions.

#### Framework

- `Microsoft.Extensions.Logging` with the EagleEye rolling file provider `EagleEye.Shared/Logging/RollingFileLoggerProvider` (no third-party sinks). .NET has no built-in file logging provider, contrary to the original wording of ADR-002; the own provider is small (size-based rolling, retention, one line per entry) and keeps the "no third-party logging library" decision (ADR-002 implementation note, US-003). The service is the first user (US-003); TrayClient and ParentApp follow.
- All components use the standard `ILogger<T>` / `ILoggerFactory` abstractions via DI

#### Log Modes

Each application supports two modes, controlled via its YAML configuration file (`logging.debug_mode: true|false`):

| Mode | Levels Logged | Content |
|------|--------------|---------|
| **Normal** (default) | Error, Warning, Information | Operational events: enforcement actions, configuration changes, pairing events, connection lifecycle, startup/shutdown, errors with exception messages |
| **Debug** | Error, Warning, Information, Debug, Trace | Everything from normal mode plus: internal state transitions, method entry/exit traces with parameter values, full stack traces on all exceptions, SignalR message payloads, process enumeration details, SQL queries |

Debug mode can be toggled at runtime for the Service via a parent app command (no restart required). For TrayClient and ParentApp, changing the YAML setting requires an application restart.

#### File Management

| Setting | Value |
|---------|-------|
| Maximum file size | 50 MB per log file |
| Maximum file count | 3 rolling files per application (oldest deleted when a 4th would be created) |
| Maximum age | Files older than 5 days are deleted, except the current file (FR-SVC-103; combined with the file count, Michael 2026-10-07, US-003 plan Q-1) |
| File location | Same OS-standard application data directory as the SQLite database and YAML config; for the service its subfolder `logs\` (admin-only, see below) |
| Naming | `{ApplicationName}-{sequence}.log` (e.g., `EagleEye.Service-001.log`) |

Log file locations per component:

| Component | Log Directory |
|-----------|--------------|
| EagleEye.Service | `%ProgramData%\EagleEye\logs\` — SYSTEM and Administrators only (FR-SVC-100 v1.3); the installer creates it like `certs\`, and the service re-applies the ACL at every start and writes no log file if that fails |
| EagleEye.TrayClient | `%ProgramData%\EagleEye\` |
| EagleEye.ParentApp (Windows) | `%LocalAppData%\EagleEye\` |
| EagleEye.ParentApp (macOS) | `~/Library/Application Support/EagleEye/` |
| EagleEye.ParentApp (iOS) | App sandbox `Documents/` |
| EagleEye.ParentApp (Android) | App internal storage |

#### Security Rule

**Sensitive data must never be logged**, regardless of log level. This includes:

- Passwords, pairing codes, authentication tokens/credentials
- Certificate private keys
- Any value that could be used to impersonate a paired device

Sensitive values are replaced with `***` in log output. This rule is enforced by code review — there is no runtime redaction filter.

### 8.11 Error Handling and Resilience

| Scenario | Behavior |
|----------|----------|
| Service crash/restart | Budget tracking resumes from last persisted state. Configuration is intact in SQLite. |
| TrayClient crash | Service continues enforcing. TrayClient auto-restarts via session Run key. |
| ParentApp disconnects | Service continues enforcing stored rules. ParentApp reconnects automatically. |
| SignalR connection lost (LAN) | Clients retry with exponential backoff. After reconnecting, clients fetch the full state of every area they show (ADR-010 §7); missed broadcasts are not replayed. |
| Disk full / write error | Service logs error, continues enforcing from in-memory state. |

### 8.12 Dependency Injection

All service components are registered in the DI container and injected via constructor injection:

```
Host.CreateDefaultBuilder(args)
    .UseWindowsService()
    .ConfigureServices(services =>
    {
        services.AddSingleton<ICertificateManager, CertificateManager>();
        services.AddSingleton<IConfigurationManager, ConfigurationManager>();
        services.AddSingleton<IUserAccountDiscovery, UserAccountDiscovery>();
        services.AddSingleton<IAppDiscovery, AppDiscovery>();
        services.AddSingleton<IPairingManager, PairingManager>();
        services.AddSingleton<IStatisticsCollector, StatisticsCollector>();
        services.AddSingleton<IProcessMonitor, ProcessMonitor>();
        services.AddSingleton<IProcessEnforcer, ProcessEnforcer>();
        // SignalR, Kestrel, logging configured here
    });
```

Each component depends on interfaces, not concrete types, enabling unit testing with Moq.

### 8.13 Localization

- All user-facing strings (TrayClient notifications, ParentApp labels) are externalized into resource files
- Default language: German (`de`). Secondary: English (`en`)
- Service log messages are in English (developer audience)
- Resource files are per-component (`EagleEye.TrayClient/Resources/`, `EagleEye.ParentApp/Resources/`)

---

## 9. Architecture Decisions

All architectural decisions are recorded as ADRs in `02_Implementation/docs/architecture/decisions/`.

| ADR | Title | Status |
|-----|-------|--------|
| ADR-001 | Technology Selection | Accepted |
| ADR-002 | SQLite for Data Persistence, YAML for Application Configuration, .NET Logging for Diagnosability | Accepted |
| ADR-003 | SignalR Hub Design — Two Hubs, Three Communication Patterns, Server-Authoritative State | Accepted |
| ADR-004 | Pairing-Based Authentication for Parent Apps | Accepted |
| ADR-005 | Process Classification Strategy — Ignore, Allow, Block | Accepted |
| ADR-006 | Graceful-Then-Force Process Termination Pattern | Accepted |
| ADR-007 | Two-Machine Development and Manual Acceptance Testing | Accepted (§2 "Distribution" superseded by ADR-009) |
| ADR-008 | Parent App Connectivity — Endpoints, TLS Trust and Pairing Protocol | Accepted |
| ADR-009 | Windows Parent App Packaging and the ParentApp.Core Library | Accepted |
| ADR-010 | Event-Driven State Propagation — Service Broadcasts with Revisions | Accepted |

---

## 10. Quality Requirements

### 10.1 Quality Tree

```plantuml
@startuml Quality Tree
skinparam defaultTextAlignment center

rectangle "EagleEye\nQuality" as root

rectangle "Reliability" as rel
rectangle "Security" as sec
rectangle "Diagnosability" as diag
rectangle "Usability" as usa
rectangle "Performance" as perf
rectangle "Maintainability" as maint

root --> rel
root --> sec
root --> diag
root --> usa
root --> perf
root --> maint

rectangle "Survive reboot" as rel1
rectangle "Persist tracked time" as rel2
rectangle "Enforce without parent" as rel3
rel --> rel1
rel --> rel2
rel --> rel3

rectangle "Child cannot bypass" as sec1
rectangle "Auth for config changes" as sec2
rectangle "TLS for all comms" as sec3
sec --> sec1
sec --> sec2
sec --> sec3

rectangle "All components log" as diag1
rectangle "Debug mode with traces" as diag2
rectangle "No sensitive data in logs" as diag3
diag --> diag1
diag --> diag2
diag --> diag3

rectangle "Zero-touch TLS" as usa1
rectangle "Simple pairing" as usa2
rectangle "Clear warnings" as usa3
usa --> usa1
usa --> usa2
usa --> usa3

rectangle "Low CPU overhead" as perf1
rectangle "No frame drops" as perf2
perf --> perf1
perf --> perf2

rectangle "Component isolation" as maint1
rectangle "API-first contracts" as maint2
rectangle "DI + interfaces" as maint3
maint --> maint1
maint --> maint2
maint --> maint3
@enduml
```

### 10.2 Quality Scenarios

| ID | Quality | Scenario | Measure |
|----|---------|----------|---------|
| QS-01 | Reliability | The Windows PC reboots unexpectedly. After reboot, the service starts automatically and resumes enforcement with persisted budgets. | Budget drift ≤ polling interval (seconds of lost tracking) |
| QS-02 | Reliability | The parent app disconnects. The service continues enforcing all configured rules from local storage. | Zero enforcement gaps during disconnect |
| QS-03 | Security | A child attempts to kill `EagleEye.Service` via Task Manager. | Operation denied (SYSTEM service, standard user has no permission) |
| QS-04 | Security | An unauthenticated network client sends a configuration change. | Service rejects the request; no configuration modified |
| QS-05 | Diagnosability | A bug is reported. The developer enables debug mode in the YAML config and reproduces the issue. Log files contain full stack traces with parameter values for every method in the call chain. | Root cause identifiable from log files alone without attaching a debugger |
| QS-06 | Diagnosability | A credential-related flow is logged in debug mode. | No passwords, tokens, pairing codes, or private keys appear in any log file — all replaced with `***` |
| QS-07 | Usability | A parent installs EagleEye, pairs their phone, and configures one app rule. | Achievable in under 10 minutes with no CLI or certificate steps |
| QS-08 | Performance | The service monitors processes while the child plays a game. | CPU usage from EagleEye ≤ 2% average; no perceptible impact on game FPS |
| QS-09 | Performance | The tray overlay is visible during a borderless-windowed game. | No frame drops caused by the overlay |
| QS-10 | Maintainability | A developer adds a new SignalR method. | Change touches `EagleEye.Shared/Contracts/` first, then implementations — no shotgun surgery |

---

## 11. Risks and Technical Debt

| # | Risk | Probability | Impact | Mitigation |
|---|------|-------------|--------|------------|
| R-1 | Self-signed certificate rejected by iOS/Android MAUI clients | Medium | High | Validate `ServerCertificateCustomValidationCallback` on each platform early in development. Pin thumbprint after pairing. iOS/Android development is deferred — validate when those platforms are addressed. |
| R-2 | Process monitoring causes high CPU under heavy process churn | Low | Medium | Use efficient enumeration (snapshot-based, not per-process polling). Profile during first DEV iteration. Configurable poll interval as safety valve. |
| R-3 | MAUI limitations on macOS Catalyst | Medium | Medium | Validate tray-like features and window management on real hardware in the first user story. Fall back to menu-bar app pattern if needed. |
| R-4 | WinForms topmost overlay z-order lost in some fullscreen games | Medium | Low | Documented as v1 limitation (no DirectX overlay). Use `WS_EX_TOPMOST` + timer-based re-assertion. |
| R-5 | SQLite database corruption on service crash | Very Low | High | SQLite WAL mode provides crash resilience out of the box. Regular `PRAGMA integrity_check` on startup. |
| R-6 | Standard-user child kills TrayClient process | Low | Low | TrayClient is informational only; enforcement continues server-side. TrayClient restarts automatically. Consider process-protection techniques in later iterations. |
| R-7 | Clock manipulation by child to circumvent budget/pause | Low | Medium | Service uses monotonic timers for budget countdown (not wall-clock). Pause-window checks use wall clock but service runs as SYSTEM — standard user cannot change system time. |
| R-8 | Kid pairs their own parent app: the pairing code is shown in the kid's tray session (US-002 Q-1), and the per-user parent app installer needs no admin rights, so a kid can pair an app on the same PC or another device. From the first configuration story on, such an app could change the kid's own rules. | Medium | High | **Accepted risk** (Michael, 2026-10-04): no technical protection for now; revisit before or with the first configuration story. US-003 (first configuration story, OQ-7, Michael 2026-10-07): stays accepted, because the account selection has no effect yet; a protection must be decided before the first enforcement story. Candidate mitigations: show the code only in admin sessions / the Event Log, or require an admin confirmation on the service PC. Paired devices are visible and removable via device management (FR-APP-015). |
| R-9 | Service certificate lost or regenerated (e.g. `%ProgramData%\EagleEye` deleted): every paired app rejects the new certificate (pin mismatch) | Low | Medium | Uninstalling the service keeps `%ProgramData%\EagleEye`. In US-002, recovery means reinstalling the parent app; a guided re-pairing follows with device management (ADR-008). |

---

## 12. Glossary

| Term | Definition |
|------|-----------|
| **Allow-list** | The set of applications a parent has explicitly permitted a child to use. Everything not on this list (and not ignored) is blocked. |
| **Blocked process** | A process that is neither on the ignore list nor the allow-list. Terminated on detection. |
| **Budget** | See *Time budget*. |
| **Enforcement** | The act of terminating a process because it is blocked, its budget expired, or a pause window is active. |
| **Graceful shutdown** | Sending `WM_CLOSE` to a process window, giving it a chance to save state before a force-kill. |
| **Ignore list** | A shipped, non-configurable list of essential Windows processes that are never terminated or tracked. |
| **Kid** | A child using a standard Windows user account, subject to EagleEye enforcement. |
| **Pairing** | The one-time process by which a parent app authenticates with the service using a 6-digit code displayed on the Windows PC. |
| **Pairing code** | A 6-digit numeric code, valid for 5 minutes and for one attempt, requested by the parent app and displayed by the TrayClient (or logged to Event Log) for a parent to enter in their app. |
| **Pinning** | The parent app accepts only the certificate thumbprint it stored at pairing time (ADR-008). |
| **Parent** | The adult who configures EagleEye rules via the ParentApp. |
| **Pause window** | A per-weekday time range (e.g., 20:00–09:00) during which all non-ignored applications are denied for a user, regardless of remaining budget. |
| **Poll interval** | The frequency at which the Monitoring component enumerates running processes. |
| **Revision** | Per state area, a number that the service increments with every stored change during one service run. Clients apply a snapshot only if its revision is higher than the last one they applied (ADR-010). |
| **State area** | A unit of state with its own snapshot, revision, query, broadcast and write commands, e.g. `UserAccounts` (ADR-010). |
| **Under parental control** | An account the parent has ticked in the parent app (US-003). Only these accounts will be monitored and enforced (FR-SVC-010 v1.3). |
| **SID** | Windows Security Identifier — uniquely identifies a user account across renames. Used as the key for per-user config and stats files. |
| **Time budget** | A daily allowance in minutes for a specific application, configured per user and per weekday. Resets at midnight. Does not carry over. |
| **TrayClient** | The lightweight EagleEye executable running in the kid's Windows session, displaying notifications and remaining time. |

---

*End of System Architecture*

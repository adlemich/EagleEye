# EagleEye — Product Coding Guidelines

*Status: Approved (2026-09-20, amended 2026-10-03, 2026-10-04, 2026-10-07)*
*Maintainer: ARC Agent | Last Updated: 2026-10-07*

> **Amendment 2026-10-04 (US-002, ADR-008, ADR-009)**: §10.1, §10.2, §10.4 (`ParentApp.Core`, Shared `Communication/` and `Data/`), §12.2 (pairing code in a topmost window), §16.1 (packaging per ADR-009, token storage via DPAPI `ISecretStore`).

> **Amendment 2026-10-07 (Michael, US-002 deviation D-11)**: §3.4 and §8.2: plain `using` for SQLite commands, readers and transactions instead of `await using`.

This document defines the coding standards all EagleEye components must follow. It complements but does not duplicate the system architecture (`arc42/system-architecture.md`) and ADRs — refer to those for architectural decisions, component responsibilities, and design rationale.

**Target runtime**: .NET 10 / C# 14 (LTS, supported until November 2028)

---

## Table of Contents

1. [General C# Coding Standards](#1-general-c-coding-standards)
2. [Null Safety and Parameter Validation](#2-null-safety-and-parameter-validation)
3. [Exception Handling](#3-exception-handling)
4. [Object-Oriented Design](#4-object-oriented-design)
5. [Async and Concurrency](#5-async-and-concurrency)
6. [Dependency Injection](#6-dependency-injection)
7. [SignalR-Specific Guidelines](#7-signalr-specific-guidelines)
8. [SQLite and Data Access](#8-sqlite-and-data-access)
9. [Logging](#9-logging)
10. [Project Structure and Code Reuse](#10-project-structure-and-code-reuse)
11. [Unit Testing](#11-unit-testing)
12. [Platform-Specific: Windows 11 (Service and TrayClient)](#12-platform-specific-windows-11-service-and-trayclient)
13. [Platform-Specific: macOS (ParentApp via Mac Catalyst)](#13-platform-specific-macos-parentapp-via-mac-catalyst)
14. [Platform-Specific: iOS (ParentApp)](#14-platform-specific-ios-parentapp)
15. [Platform-Specific: Android (ParentApp)](#15-platform-specific-android-parentapp)
16. [Platform-Specific: Windows (ParentApp via WinUI)](#16-platform-specific-windows-parentapp-via-winui)

> **Amendment 2026-10-03 (ADR-007)**: §10.1 and §11.1 now reflect the actual test project layout (`02_Implementation/tests/`); §16 added for the Windows parent app; build hosts per platform noted in §10.4.

---

## 1. General C# Coding Standards

### 1.1 Naming Conventions

Follow the [Microsoft C# naming conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/identifier-names):

| Element | Convention | Example |
|---------|-----------|---------|
| Namespace | PascalCase, matching folder path | `EagleEye.Service.Monitoring` |
| Class / Record / Struct | PascalCase | `ProcessMonitor`, `AppRuleDto` |
| Interface | `I` prefix + PascalCase | `IProcessMonitor` |
| Public method | PascalCase | `GetUserConfig()` |
| Private method | PascalCase | `ClassifyProcess()` |
| Public property | PascalCase | `IsConnected` |
| Private field | `_camelCase` (underscore prefix) | `_configManager` |
| Parameter | camelCase | `userSid` |
| Local variable | camelCase | `remainingMinutes` |
| Constant | PascalCase | `DefaultPort` |
| Enum type | PascalCase (singular) | `ProcessCategory` |
| Enum member | PascalCase | `ProcessCategory.Blocked` |
| Async method | `Async` suffix | `GetUsageStatisticsAsync()` |
| DTO / model class | `Dto` suffix for transfer objects | `UserConfigDto` |

### 1.2 File and Type Organization

- **One type per file.** The file name matches the type name: `ProcessMonitor.cs` contains `class ProcessMonitor`.
- **Exception**: nested private types may live in the parent type's file.
- **Namespace matches folder path.** `EagleEye.Service/Monitoring/ProcessMonitor.cs` → `namespace EagleEye.Service.Monitoring`.
- **File-scoped namespaces.** Use `namespace EagleEye.Service.Monitoring;` (no braces) — the C# 10+ style.

### 1.3 Formatting

- Use the `.editorconfig` checked into the repository root for consistent formatting across all developers and AI agents.
- **Braces**: Allman style (opening brace on its own line) for types and methods. K&R (same line) is acceptable for single-statement property accessors and lambdas.
- **Indentation**: 4 spaces, no tabs.
- **Max line length**: 120 characters (soft limit — prefer readability over rigid wrapping).
- **`using` directives**: at the top of the file, sorted alphabetically, `System` namespaces first. Use global usings in `GlobalUsings.cs` for project-wide imports.

### 1.4 Modern C# 14 Features — Use Where Appropriate

Prefer modern language features when they improve clarity. Do not use features purely for novelty.

| Feature | Guideline |
|---------|-----------|
| **Primary constructors** | Use for service classes with DI injection — makes dependencies visible at the type declaration. |
| **Extension members** (C# 14) | Use for adding behavior to types you do not own (e.g., extension properties on `Process`). Keep extension classes in a `Extensions/` folder. |
| **`field` keyword** (C# 14) | Use for simplified property backing fields when validation or transformation is needed. |
| **Pattern matching** | Prefer `is`, `switch` expressions, and property patterns over nested `if-else` chains. |
| **Records** | Use `record` for immutable DTOs and value objects. Use `record struct` for small, stack-allocated values. |
| **Target-typed `new`** | Use `List<string> items = new();` when the type is obvious from context. |
| **Raw string literals** | Use `"""..."""` for multi-line strings (SQL queries, log templates). |
| **Collection expressions** | Use `[1, 2, 3]` syntax for array/list initialization where supported. |
| **`required` keyword** | Use on properties that must be set during object initialization. |
| **Nullable reference types** | Always enabled (see §2). |

---

## 2. Null Safety and Parameter Validation

### 2.1 Nullable Reference Types

Nullable reference types are **enabled project-wide** in every `.csproj`:

```xml
<Nullable>enable</Nullable>
```

Rules:

- **Non-nullable is the default.** Only add `?` when `null` is a genuinely valid state.
- **Treat compiler warnings as errors** for nullable reference types (`<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`).
- **Never suppress** nullable warnings with `!` (null-forgiving operator) unless there is a documented reason in a comment.

### 2.2 Parameter Validation — Guard Clauses

All **public** and **internal** methods must validate their parameters at the method entry point. Use the .NET built-in guard methods:

```csharp
public UserConfigDto GetUserConfig(string userSid)
{
    ArgumentNullException.ThrowIfNull(userSid);
    ArgumentException.ThrowIfNullOrWhiteSpace(userSid);

    // method body
}

public void UpdateTimeBudgets(string userSid, List<TimeBudgetDto> budgets)
{
    ArgumentNullException.ThrowIfNull(userSid);
    ArgumentNullException.ThrowIfNull(budgets);
    ArgumentOutOfRangeException.ThrowIfZero(budgets.Count);

    // method body
}
```

Rules:

- Use `ArgumentNullException.ThrowIfNull()` for null checks (not manual `if` + `throw`).
- Use `ArgumentException.ThrowIfNullOrWhiteSpace()` for string parameters that must have content.
- Use `ArgumentOutOfRangeException.ThrowIfNegative()`, `ThrowIfZero()`, `ThrowIfGreaterThan()` for numeric bounds.
- **Private methods** may omit guards if the caller is already validated — but add them when the logic is complex or the method is called from multiple places.
- **Never** pass unchecked external input deeper into the call chain. Validate at the boundary (public API, hub method, configuration reader), then trust internally.

### 2.3 Return Values

- Prefer returning empty collections over `null` (`Array.Empty<T>()`, `Enumerable.Empty<T>()`).
- Use the `required` keyword on DTO properties that must always be set.
- Use `[NotNullWhen(true)]` and `[MaybeNullWhen(false)]` attributes on `TryGet` patterns.

---

## 3. Exception Handling

### 3.1 Principles

- **No unhandled exceptions.** Every execution path must either handle or explicitly propagate exceptions. The application must never crash silently.
- **Fail fast at boundaries.** Invalid input from external sources (SignalR clients, config files, database) throws immediately — do not let bad data propagate.
- **Catch specific exceptions.** Never catch `Exception` broadly unless at a top-level handler. Catch the most specific type (`SqliteException`, `JsonException`, `IOException`).
- **Do not swallow exceptions.** Every `catch` block must either log + re-throw, log + return a meaningful error, or handle the condition explicitly. Empty `catch` blocks are forbidden.

### 3.2 Exception Strategy per Layer

| Layer | Strategy |
|-------|----------|
| **SignalR hub methods** | Wrap in try-catch. Log the exception. Return a structured error DTO or throw `HubException` with a safe message. Never leak internal details (stack traces, file paths) to clients. |
| **Service components** | Let exceptions propagate upward to the hub or hosting layer. Log with `ILogger.LogError()` at the point where the exception is handled (not at every re-throw). |
| **Background workers** (monitoring loop, midnight reset) | Catch at the loop level. Log the error. Continue the next iteration. Never let a single iteration failure kill the background service. |
| **Application startup** | Let fatal exceptions (port in use, database corruption, certificate failure) propagate to the host — the service should fail to start with a clear log message rather than run in a broken state. |

### 3.3 Custom Exceptions

Define custom exception types only when the caller needs to distinguish the error programmatically. Prefer existing .NET exception types:

```csharp
// Good — caller can distinguish pairing errors from other failures
public class PairingCodeExpiredException : InvalidOperationException
{
    public PairingCodeExpiredException()
        : base("The pairing code has expired.") { }
}

// Bad — no value over ArgumentException
public class InvalidUserSidException : Exception { } // Don't do this
```

### 3.4 Dispose Pattern

- Implement `IAsyncDisposable` on types that hold database connections, SignalR connections, or file handles.
- Use `await using` for scoped disposable resources.
- **Exception — SQLite objects**: use plain `using` for `SqliteCommand`, `SqliteDataReader` and `SqliteTransaction` (Microsoft.Data.Sqlite). They dispose synchronously, so `await using` gains nothing. It only adds compiler-generated async-dispose branches that tests with in-memory SQLite cannot reach, which breaks the 100 % branch-coverage rule. (Michael, 2026-10-07; US-002 deviation D-11.)
- Register disposable services correctly in DI (prefer `AddSingleton` with `IHostedService` for long-lived resources).

---

## 4. Object-Oriented Design

### 4.1 Single Responsibility

- Each class serves **one responsibility**. If a class name requires "And" or "Manager" to describe what it does, it likely needs splitting.
- The service component boundaries defined in the system architecture (§5.2) are the primary decomposition — each component maps to one or a small number of classes with focused responsibilities.
- Favor composition over inheritance. Inject collaborators via DI rather than inheriting shared behavior.

### 4.2 Interface Segregation

- Define focused interfaces. A consumer should not depend on methods it does not use.
- The `EagleEye.Shared/Contracts/` interfaces (`IParentHub`, `ITrayHub`, `IParentClientCallback`, `ITrayClientCallback`) are the canonical examples — each is scoped to one client role (see ADR-003).
- Internal service component interfaces (e.g., `IProcessMonitor`, `IConfigurationManager`) should expose only the methods needed by their consumers.

### 4.3 Class Design Rules

| Rule | Detail |
|------|--------|
| **Keep classes small** | A class should rarely exceed 300 lines. If it does, look for extraction opportunities. |
| **Keep methods short** | A method should do one thing. Target 20–30 lines; anything beyond 50 lines is a strong signal to extract. |
| **Limit constructor parameters** | More than 5 injected dependencies suggests the class has too many responsibilities. Split it. |
| **Prefer immutability** | Use `record` or `readonly` properties for data objects. Mutable state should be explicit and thread-safe. |
| **Seal classes by default** | Use `sealed` on classes not designed for inheritance. This enables JIT optimizations and communicates intent. |
| **No static mutable state** | Static fields must be `readonly` or `const`. Mutable shared state goes through DI-registered singletons. |

### 4.4 DTOs and Domain Models

- DTOs (suffixed `Dto`) are pure data carriers for SignalR transfer. They live in `EagleEye.Shared/Models/`.
- DTOs use `record` types for immutability and value equality.
- DTOs have no business logic — no methods beyond property access.
- Internal domain models (if needed) are separate from DTOs and may contain validation or computed properties.

---

## 5. Async and Concurrency

### 5.1 Async/Await Rules

- **Async all the way.** If a method calls an async API, make the method async and return `Task` or `Task<T>`. Never use `.Result`, `.Wait()`, or `.GetAwaiter().GetResult()` — these risk deadlocks.
- **Suffix with `Async`.** All async methods are suffixed (e.g., `GetUserConfigAsync()`). Exception: SignalR hub methods and interface implementations where the framework expects specific names.
- **Use `ConfigureAwait(false)`** in library code (`EagleEye.Shared`) and service-layer code that does not touch the UI thread.
- **Use `ValueTask<T>`** for hot paths where the result is often available synchronously (e.g., cache hits).
- **Cancellation tokens.** Accept `CancellationToken` in all async methods that perform I/O or long-running work. Propagate the token to downstream calls. Respect cancellation in loops.

### 5.2 Thread Safety

- The monitoring loop and enforcement logic run on background threads. Shared mutable state (e.g., in-memory allow-list cache, budget counters) must be protected.
- Prefer `ConcurrentDictionary<TKey, TValue>` and `Channel<T>` over manual locking.
- When locks are needed, use `SemaphoreSlim` (async-compatible) rather than `lock` statements for async code paths.
- Never hold a lock while awaiting. Restructure to acquire, compute, release, then await.

### 5.3 Background Services

- Implement long-running work as `BackgroundService` (inherits from `Microsoft.Extensions.Hosting.BackgroundService`).
- The monitoring loop, midnight reset, and statistics persistence are background services.
- Use `IHostApplicationLifetime` to respond to graceful shutdown — persist state before exiting.
- Catch exceptions inside the loop body to prevent a single failure from terminating the service (see §3.2).

---

## 6. Dependency Injection

See system architecture §8.12 for the service registration overview.

### 6.1 Registration Rules

| Lifetime | Use For | Example |
|----------|---------|---------|
| **Singleton** | Components with long-lived state or expensive initialization | `IProcessMonitor`, `IConfigurationManager`, `ICertificateManager` |
| **Scoped** | Per-SignalR-connection or per-request state | Hub-scoped services (if needed) |
| **Transient** | Stateless, lightweight services | Validators, mappers |

Rules:

- **Register interfaces, not concrete types.** Always bind `IFoo` → `FooImpl`.
- **Constructor injection only.** Do not use service locator (`IServiceProvider.GetService<T>()`) in application code. Exception: factory patterns where the type is determined at runtime.
- **Use primary constructors** for classes with DI dependencies — makes the dependency list immediately visible.
- **Do not inject `ILogger`** — inject `ILogger<T>` with the owning class as the type parameter.

### 6.2 Testing with DI

- Every service component is injectable and testable in isolation.
- Mocks are created with Moq, substituting the interface (`Mock<IProcessMonitor>`).
- Integration tests can use `WebApplicationFactory<T>` for end-to-end hub testing.

---

## 7. SignalR-Specific Guidelines

See ADR-003 for the hub design (two hubs, three patterns, server-authoritative state) and system architecture §8.3 for the communication design.

### 7.1 Hub Methods

- **Keep hub methods thin.** The hub class is a routing layer — it validates the request, delegates to an injected service, and returns the result. Business logic does not belong in the hub.

```csharp
// Good — thin hub method
public async Task<UserConfigDto> GetUserConfig(string userSid)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(userSid);
    return await _configManager.GetUserConfigAsync(userSid);
}

// Bad — business logic in the hub
public async Task<UserConfigDto> GetUserConfig(string userSid)
{
    var user = await _db.Users.FindAsync(userSid);
    if (user == null) throw new HubException("User not found");
    var rules = await _db.Rules.Where(r => r.UserSid == userSid).ToListAsync();
    // ... 50 more lines of logic
}
```

- **Validate all input.** Hub methods are a trust boundary — input comes from a network client. Validate every parameter with guard clauses (see §2.2).
- **Do not store state in hub instance fields.** Hubs are transient — a new instance is created per invocation. Use injected singleton services for shared state.
- **Return structured errors.** Throw `HubException` with a user-safe message for client-facing errors. Log the full exception details server-side.
- **Use strongly-typed clients.** Use `Hub<IParentClientCallback>` and `Hub<ITrayClientCallback>` so client callbacks are compile-time checked.

### 7.2 Client Callbacks

- Broadcast via groups, not individual client IDs: `Clients.Group("Parents").OnConfigUpdated(...)`.
- Send full-state snapshots, not deltas (per ADR-003, Pattern 3).
- Fire-and-forget from the server's perspective — do not await client acknowledgment.

### 7.3 Connection Lifecycle

- Implement `OnConnectedAsync()` and `OnDisconnectedAsync()` to manage group membership and cleanup.
- On parent-app connect: validate the auth token (ADR-004), add to `Parents` group, push full state snapshot.
- On tray-client connect: validate localhost origin, register SID, add to `Tray:{userSid}` group, push current budget state.
- On disconnect: clean up group membership. Log the disconnection.

### 7.4 Error Handling in Hubs

- Wrap every hub method body in try-catch.
- Log the exception with `ILogger<ParentHub>.LogError()`.
- Throw `HubException` with a sanitized message for the client. Never expose internal details (stack traces, file paths, SQL errors) to clients.

---

## 8. SQLite and Data Access

See ADR-002 for the persistence decision and system architecture §8.4 for the database design.

### 8.1 Connection Management

- Use a **single long-lived connection** per database (singleton `SqliteConnection` registered in DI), opened at startup and closed at shutdown. SQLite performs best with a single writer connection.
- Enable WAL mode and configure pragmas immediately after opening:

```csharp
var connection = new SqliteConnection($"Data Source={dbPath}");
await connection.OpenAsync();

using var pragmaCmd = connection.CreateCommand();
pragmaCmd.CommandText = """
    PRAGMA journal_mode = WAL;
    PRAGMA synchronous = NORMAL;
    PRAGMA busy_timeout = 5000;
    PRAGMA foreign_keys = ON;
    """;
await pragmaCmd.ExecuteNonQueryAsync();
```

### 8.2 Query Patterns

- Use **parameterized queries** exclusively. Never concatenate user input into SQL strings — this is a hard security rule.

```csharp
// Good — parameterized
var cmd = connection.CreateCommand();
cmd.CommandText = "SELECT * FROM UserConfig WHERE UserSid = @sid";
cmd.Parameters.AddWithValue("@sid", userSid);

// FORBIDDEN — SQL injection risk
cmd.CommandText = $"SELECT * FROM UserConfig WHERE UserSid = '{userSid}'";
```

- Keep SQL queries in the data access class — do not scatter them across service components.
- Use transactions for multi-statement writes:

```csharp
using var transaction = await connection.BeginTransactionAsync(); // plain using, see §3.4
try
{
    // multiple writes
    await transaction.CommitAsync();
}
catch
{
    await transaction.RollbackAsync();
    throw;
}
```

### 8.3 Schema Migrations

- Store the current schema version in a `SchemaVersion` table.
- On startup, compare the stored version with the expected version and apply migrations sequentially.
- Each migration is a method or SQL script that increments the version.
- Migrations are forward-only — no rollback support in v1. Test migrations thoroughly before release.

### 8.4 Data Integrity

- Run `PRAGMA integrity_check;` on startup (see system architecture §11, Risk R-5). If it fails, log a critical error and refuse to start.
- Use `NOT NULL` constraints and foreign keys in the schema — enforce data integrity at the database level, not just in application code.
- Persist data periodically (every N minutes) and on service shutdown to minimize data loss on crash.

---

## 9. Logging

See ADR-002 for the logging decision and system architecture §8.10 for the logging strategy.

### 9.1 Usage Rules

- Use `ILogger<T>` injected via DI. Never create loggers manually.
- Use structured logging with message templates (not string interpolation):

```csharp
// Good — structured logging, parameters captured as properties
_logger.LogInformation("Budget expired for user {UserSid}, app {AppName}",
    userSid, appName);

// Bad — string interpolation defeats structured logging
_logger.LogInformation($"Budget expired for user {userSid}, app {appName}");
```

- Use the correct log level:

| Level | Use For |
|-------|---------|
| `Critical` | Application cannot continue (DB corruption, certificate failure) |
| `Error` | Operation failed but application continues (failed to terminate process, write error) |
| `Warning` | Unexpected condition that was handled (expired pairing code, reconnect attempt) |
| `Information` | Significant operational events (service started, user paired, config updated) |
| `Debug` | Detailed internal state (process enumeration results, budget calculations) |
| `Trace` | Method entry/exit with parameter values, SQL queries, SignalR message payloads |

### 9.2 Security

**Never log sensitive data.** This is an absolute rule (see ADR-002):

- Pairing codes → `***`
- Authentication tokens → `***`
- Certificate private keys → never referenced in log statements
- Passwords → never stored or processed, but if encountered, `***`

### 9.3 Debug Mode Tracing

- In debug mode, log method entry and exit with parameter values for key methods (enforcement, pairing, configuration updates).
- Include full `Exception.ToString()` (stack trace + inner exceptions) on all errors and warnings.
- Debug-mode logging must not alter application behavior — it is purely observational.

---

## 10. Project Structure and Code Reuse

### 10.1 Repository Layout

```
02_Implementation/src/
├── EagleEye.Shared/              # Shared library — contracts, models, constants
│   ├── Contracts/                # IParentHub, ITrayHub, IParentClientCallback, ITrayClientCallback
│   ├── Models/                   # DTOs: UserConfigDto, AppRuleDto, TimeBudgetDto, ...
│   ├── Constants/                # HubRoutes, ServiceDefaults, PairingRules
│   ├── Communication/            # ReconnectSchedule, ConnectBackoff (client reconnect timing)
│   ├── Data/                     # SqliteDatabase base (pragmas, integrity check, migrations)
│   └── Extensions/               # Shared extension methods
├── EagleEye.Service/             # Windows service
│   ├── Communication/            # ParentHub, TrayHub, connection management
│   ├── Monitoring/               # ProcessMonitor, process enumeration
│   ├── Enforcement/              # ProcessEnforcer, termination logic
│   ├── Configuration/            # ConfigurationManager, YAML reader
│   ├── Statistics/               # StatisticsCollector, purge logic
│   ├── Certificates/             # CertificateManager, TLS setup
│   ├── UserAccounts/             # UserAccountDiscovery
│   ├── Pairing/                  # PairingManager, token generation
│   ├── AppDiscovery/             # InstalledAppScanner, display-name resolver
│   ├── Logging/                  # Log configuration, level switching
│   ├── Data/                     # SQLite connection, migrations, data access classes
│   └── Program.cs                # Host builder, DI registration, startup
├── EagleEye.TrayClient/          # Windows tray application
│   ├── Communication/            # SignalR client connection to service
│   ├── UI/                       # NotifyIcon, overlay, notifications
│   ├── Data/                     # SQLite (optional cache), YAML reader
│   └── Program.cs
├── EagleEye.ParentApp.Core/      # Plain net10.0 library, all parent-app logic without MAUI (ADR-009)
│   ├── Abstractions/             # ISecretStore, ISecretProtector, IThemeService, IDialogService, ...
│   ├── Communication/            # SignalR client, certificate trust, ConnectionCoordinator
│   ├── Data/                     # SQLite (pairing, settings, secrets), YAML reader
│   ├── ViewModels/               # MVVM view models
│   └── Resources/                # AppTexts (de, en)
├── EagleEye.ParentApp/           # MAUI head: views and platform code
│   ├── Views/                    # MAUI pages and views
│   ├── Services/                 # Implementations of the Core abstractions
│   ├── Resources/                # Styles, icons, images
│   ├── Platforms/
│   │   ├── Windows/
│   │   ├── MacCatalyst/
│   │   ├── Android/
│   │   └── iOS/
│   └── MauiProgram.cs

02_Implementation/tests/          # Unit tests — one project per component
├── EagleEye.Shared.Tests/
├── EagleEye.Service.Tests/
├── EagleEye.TrayClient.Tests/
└── EagleEye.ParentApp.Tests/
```

### 10.2 Code Reuse Principles

- **`EagleEye.Shared` is the reuse vehicle.** All contracts, DTOs, constants, and shared utilities live here. Both client apps and the service reference this project.
- **Do not duplicate code across projects.** If logic is needed by more than one component, extract it into `EagleEye.Shared`.
- **Do not duplicate code within a project.** If a pattern appears three or more times, extract a shared method or class. Two occurrences are acceptable if the contexts differ enough that an abstraction would be forced.
- **Shared data access patterns** (YAML parsing, SQLite connection setup, PRAGMA configuration) should be in `EagleEye.Shared` or a shared `Data/` utility, so all components configure their databases identically.
- **Do not add project references between `Service`, `TrayClient`, and `ParentApp`.** They communicate only via SignalR at runtime. All compile-time sharing goes through `EagleEye.Shared`. `EagleEye.ParentApp.Core` is part of the ParentApp component (`ParentApp → ParentApp.Core → Shared`, ADR-009); only the ParentApp head and its tests reference it.

### 10.3 Version Management

- All projects share a single version number (`EagleEye_vMAJOR.MINOR`), set via `02_Implementation/Directory.Build.props` (next to `EagleEye.sln`).
- Use `<VersionPrefix>` and `<VersionSuffix>` in `Directory.Build.props` for consistent versioning.

### 10.4 Build Hosts (ADR-007)

- Code for `EagleEye.Service`, `EagleEye.TrayClient` and the ParentApp `Platforms/Windows` and `Platforms/Android` folders is built and verified on the Windows Developer Machine.
- Code in ParentApp `Platforms/MacCatalyst` is built and verified on the MacBook.
- Shared ParentApp code (`EagleEye.ParentApp.Core`, and the Views of the MAUI head) must compile on both hosts. `ParentApp.Core` must not reference MAUI. Do not use platform APIs outside `Platforms/` or behind `#if` guards.
- Never hard-code absolute paths, path separators or line endings. Use `Path.Combine`, `Environment.GetFolderPath`, and `Environment.NewLine` where output is OS-facing.

---

## 11. Unit Testing

### 11.1 Framework and Conventions

- **Framework**: xUnit
- **Mocking**: Moq
- **Projects**: one test project per component in `02_Implementation/tests/` (`EagleEye.<Component>.Tests`), with folders mirroring the source project structure. Unit tests are the only automated tests; acceptance testing is manual (ADR-007).
- **Naming**: `{ClassUnderTest}_{MethodUnderTest}_{Scenario}_{ExpectedResult}`
  - Example: `ProcessMonitor_ClassifyProcess_IgnoredProcess_ReturnsIgnored`
- **One assert per test** (preferred). Multiple asserts are acceptable when testing a single logical outcome with multiple observable effects.

### 11.2 What to Test

| Component | Test Focus |
|-----------|-----------|
| **Process classification** | Ignore list matching, allow-list matching, default-to-blocked, case insensitivity |
| **Configuration** | Validation rules, default values, boundary values (negative budgets, empty allow-lists) |
| **Statistics** | Accumulation, daily reset, 90-day purge, edge cases (midnight crossing) |
| **Pairing** | Code generation, expiry validation, token hashing, rejection of invalid codes |
| **DTOs / Models** | Serialization round-trip (JSON/SignalR), required property enforcement |
| **Hub methods** | Input validation (guard clauses), delegation to service, error mapping to `HubException` |
| **Data access** | Migration application, query correctness, transaction rollback |

### 11.3 Testing Guidelines

- Mock interfaces, not concrete types. Every service component depends on interfaces for exactly this reason.
- Use `SqliteConnection` with `"Data Source=:memory:"` for in-memory database testing — no file cleanup needed.
- Do not test `private` methods directly. Test them through the public API they support.
- Arrange-Act-Assert structure in every test.
- Tests must be deterministic — no dependency on wall-clock time, file system ordering, or network availability. Inject `TimeProvider` (or an `IClock` abstraction) for time-dependent logic.

---

## 12. Platform-Specific: Windows 11 (Service and TrayClient)

### 12.1 Windows Service (`EagleEye.Service`)

- Use `Microsoft.Extensions.Hosting.WindowsServices` with `Host.CreateDefaultBuilder().UseWindowsService()`.
- The service runs as **SYSTEM** — it has full process visibility across all user sessions but no desktop interaction.
- Use the Windows `EventLog` as a fallback for the pairing code when no TrayClient is connected (see ADR-004).
- **Process enumeration**: Use `System.Diagnostics.Process.GetProcesses()` for basic enumeration. Use P/Invoke to `WTSEnumerateProcessesEx` or `NtQuerySystemInformation` if per-session filtering is needed with better performance.
- **Process termination**: Use `Process.CloseMainWindow()` for graceful shutdown, `Process.Kill(entireProcessTree: true)` for force-kill (see ADR-006).
- **User account discovery**: Use `System.DirectoryServices.AccountManagement` to enumerate local standard-user accounts. Filter out admin accounts via group membership checks.
- **File paths**: Use `Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)` for `%ProgramData%`. Never hardcode paths.
- **Service recovery**: Configure restart-on-failure via the Inno Setup installer.
- **Graceful shutdown**: Handle `IHostApplicationLifetime.ApplicationStopping` to persist budget tracking and statistics to SQLite before the service stops.

### 12.2 TrayClient (`EagleEye.TrayClient`)

- The TrayClient is a **WinForms** application, not a Windows service. It runs in the kid's user session.
- Use `System.Windows.Forms.NotifyIcon` for the system tray icon. Set both `Icon` and `Visible = true`.
- Use `NotifyIcon.ShowBalloonTip()` for popup notifications (budget warnings, enforcement notices).
- **Pairing codes are never shown in a balloon tip**: Focus Assist / "Do not disturb" can suppress balloon tips. Show the code in a small `Form` with `TopMost = true` and `ShowInTaskbar = true` (`UI/PairingCodeDialog`), which closes itself when the code expires and is replaced when a newer code arrives (ADR-008 §6).
- The optional overlay is a `Form` with `TopMost = true`, `FormBorderStyle = None`, `TransparencyKey` for see-through regions, and `ShowInTaskbar = false`.
- **Auto-start**: Registered in `HKLM\Software\Microsoft\Windows\CurrentVersion\Run` by the Inno Setup installer.
- **No admin privileges required** — the TrayClient runs under the kid's standard-user account.
- **UI thread marshalling**: All UI updates from SignalR callbacks must be marshalled to the UI thread via `Control.Invoke()` or `SynchronizationContext.Post()`. SignalR callbacks arrive on thread-pool threads.
- **Connection resilience**: Use `HubConnectionBuilder` with `.WithAutomaticReconnect()` (Shared `ReconnectSchedule`) for automatic reconnection to the service on the loopback tray endpoint `http://localhost:5080/hubs/tray` (ADR-008).

### 12.3 Windows-Specific Security

- Standard users cannot stop or uninstall the SYSTEM service (enforced by Windows SCM).
- `%ProgramData%\EagleEye\` ACLs: SYSTEM full control, administrators full control, standard users read-only (except TrayClient log files and TrayClient database).
- Never log or display certificate private keys, pairing tokens, or auth credentials.

---

## 13. Platform-Specific: macOS (ParentApp via Mac Catalyst)

### 13.1 Mac Catalyst Considerations

- The ParentApp runs on macOS via **Mac Catalyst** (MAUI's macOS deployment target). This is a UIKit app running in AppKit compatibility mode.
- **Window sizing**: macOS expects resizable desktop windows, not portrait-locked phone layouts. Set a sensible minimum window size (e.g., 800x600) and support resizing.
- **Menu bar**: Mac Catalyst apps get a basic menu bar automatically. Customize it for macOS-native feel (File, Edit, View, Help menus).
- **MVVM**: Use CommunityToolkit.MVVM for view models. Data-bind views to view models — no code-behind logic for data manipulation.
- **TLS trust**: The self-signed certificate requires a custom `ServerCertificateCustomValidationCallback` on the `HttpClientHandler` used by the SignalR client. Do **not** add the certificate to the system Keychain — handle trust programmatically.

### 13.2 macOS File Paths

- Use `Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)` which resolves to `~/Library/Application Support/` on macOS.
- All app files reside in `~/Library/Application Support/EagleEye/`:
  - Database: `EagleEye.ParentApp.db`
  - Config: `EagleEye.ParentApp.yaml`
  - Logs: `EagleEye.ParentApp-NNN.log`

### 13.3 macOS Distribution

- Distributed as `.dmg` containing the `.app` bundle.
- The app is **not** code-signed for the Mac App Store. Users may need to bypass Gatekeeper on first launch (`System Settings → Privacy & Security → Open Anyway`).
- Document this in user-facing installation instructions.

---

## 14. Platform-Specific: iOS (ParentApp)

*Note: iOS is a primary production platform (with Android). It is built on the MacBook (ADR-007). Its user stories follow once the Windows parent app has validated the parent-side features.*

### 14.1 iOS Considerations

- **Portrait only**: Lock orientation to portrait via `Info.plist` (`UISupportedInterfaceOrientations`).
- **Distribution**: Sideloaded via Xcode or `ios-deploy`. No App Store for v1.
- **App Transport Security (ATS)**: iOS enforces HTTPS strictly. The self-signed certificate requires an ATS exception in `Info.plist` for the service's hostname/IP, or a custom `NSUrlSessionDelegate` to override certificate validation. Test this early (see system architecture §11, Risk R-1).
- **Background behavior**: iOS aggressively suspends background apps. The SignalR connection will drop when the app is backgrounded. On foreground resume, reconnect and receive a full state push from the server (per ADR-003, Pattern 3, Rule 3).
- **Secure storage**: Store the pairing token in the iOS Keychain rather than in plain SQLite, for platform-idiomatic secure storage.

### 14.2 iOS File Paths

- App sandbox `Documents/` for user data (SQLite, YAML, logs).
- Use `Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)` in MAUI.

---

## 15. Platform-Specific: Android (ParentApp)

*Note: Android development is deferred until the macOS ParentApp and Windows Service mature (see product requirements §10). These guidelines apply when Android development begins.*

### 15.1 Android Considerations

- **Portrait only**: Lock orientation to portrait via `AndroidManifest.xml` (`android:screenOrientation="portrait"`).
- **Distribution**: Sideloaded via `adb install`. No Play Store for v1.
- **TLS trust**: Android does not trust self-signed certificates by default. Configure a custom `ServerCertificateCustomValidationCallback` on the `HttpClientHandler`. Alternatively, use a network security configuration (`network_security_config.xml`) to trust the EagleEye certificate. Test this early (see system architecture §11, Risk R-1).
- **Background behavior**: Android may kill background apps under memory pressure. The SignalR connection will drop. On resume, reconnect and receive a full state push from the server (per ADR-003, Pattern 3, Rule 3).
- **Secure storage**: Store the pairing token in Android's `EncryptedSharedPreferences` or the Android Keystore rather than in plain SQLite, for platform-idiomatic secure storage.

### 15.2 Android File Paths

- Internal storage: `Context.FilesDir` for app data (SQLite, YAML, logs).
- Use `Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)` in MAUI.

### 15.3 Android Permissions

- **Network access**: `android.permission.INTERNET` (required for SignalR over LAN).
- **No other special permissions** needed for v1 — EagleEye does not access contacts, camera, location, or other sensitive APIs.

---

## 16. Platform-Specific: Windows (ParentApp via WinUI)

*Added 2026-10-03 (ADR-007). Built and tested on the Windows Developer Machine.*

### 16.1 Windows Considerations

- **Target**: `net10.0-windows10.0.19041.0`. The csproj minimum (`SupportedOSPlatformVersion` / `TargetPlatformMinVersion`) is `10.0.19041.0`, because the SDK rejects a minimum above the TFM version (`NETSDK1135`); the minimum OS **Windows 11** is enforced by the installer (`MinVersion=10.0.22000`). MAUI renders through WinUI 3.
- **Desktop UI**: same desktop-style layout as macOS (§13). Resizable window with a sensible minimum size. No portrait lock.
- **TLS trust**: as on macOS, accept the service's self-signed certificate programmatically (trust on first use, then the pinned thumbprint; `CertificateTrustPolicy` in `ParentApp.Core`, ADR-008 §3). Set the callback on both the HTTP handler and `ClientWebSocketOptions.RemoteCertificateValidationCallback`. Never install the certificate into the Windows certificate store.
- **Secure storage**: store the pairing token via the Core abstraction `ISecretStore`, never in plain SQLite. On Windows, `ProtectedSecretStore` (Core) keeps the encrypted bytes in the `Secrets` table, and the platform class `Platforms/Windows/DpapiSecretProtector` (`ISecretProtector`) encrypts with DPAPI `DataProtectionScope.CurrentUser`. **Do not use MAUI `SecureStorage` on Windows**: it needs package identity, and the app is unpackaged (ADR-009). Other platforms use `MauiSecureStorageSecretStore` (Keychain / Keystore, §14, §15).
- **Role**: initial testing vehicle for parent-side features. Android and iOS are the production platforms, so keep Windows-specific code to a minimum and put everything possible in `EagleEye.ParentApp.Core` (ADR-009).
- **Packaging** (ADR-009, supersedes copy deployment): unpackaged (`WindowsPackageType=None`), self-contained (`SelfContained`, `WindowsAppSDKSelfContained=true`), `win-x64`, no MSIX. Set `RuntimeIdentifier` and the self-contained properties **in the csproj for the Windows TFM only**; do not pass `-r` / `--self-contained` on the command line, which also applies to the Android target and breaks its restore. The publish output is packaged by `installer/windows/parentapp-setup.iss` (Inno Setup, **per user**, `PrivilegesRequired=lowest`, own `AppId`, default folder `%LocalAppData%\Programs\EagleEye Parent App`) into `03_Delivery/windows/EagleEye-ParentApp-Setup-<version>.exe`, built by `scripts/package-windows.ps1 -Target ParentApp`. Uninstall removes the program folder, the Start menu entry and `%LocalAppData%\EagleEye\`; repair/update keeps the app data.
- **Same communication model** (FR-APP-091): the Windows app is an ordinary `ParentHub` client (TLS + pairing), whether it runs on the service PC or remotely. Never add a local shortcut such as direct SQLite access, named pipes or skipping pairing on `localhost`.
- **Same-PC caveat**: if the parent app runs on the PC that hosts the service, it runs under the parent's admin account. Admin accounts are never monitored, so EagleEye does not enforce rules on the parent app itself.

### 16.2 Windows File Paths

- `Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)` → `%LocalAppData%\EagleEye\` for the database, YAML config and logs.

---

*End of Product Coding Guidelines*

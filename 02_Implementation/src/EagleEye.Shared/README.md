# EagleEye.Shared

Shared library used by all EagleEye components. This is the **API contract boundary** — all communication between the Windows service and client applications is defined here.

## Responsibility

- Define all SignalR hub interfaces and method signatures (`Contracts/`)
- Define all data transfer objects (DTOs) exchanged over SignalR (`Contracts/`)
- Define shared domain models used across components (`Models/`)

## Principle

**API-first**: Any change to the communication interface between components must start here, in `Contracts/`, before any implementation changes.

## Folder Structure

```
EagleEye.Shared/
├── Contracts/      SignalR hub interfaces (the API contract)
├── Models/         DTOs, e.g. UserAccountDto, UserAccountListDto, StateWriteAckDto (ADR-010),
│                   AppUsageDto, DayUsageDto, AccountUsageDto (ADR-012), AccountRulesDto, BreakTimeEntryDto,
│                   BreakTimeDays, BreakTimeBoundary, KidMessageResult (US-005); AppRulesDto, AppRuleDto,
│                   WarningSettingsDto, KidMessageDto, AppBudgetPart, WarningLevel, KidMessageReason,
│                   DefaultTextKind (US-006; BreakTimeMessageDto removed, ShowKidMessage replaces it)
├── Constants/      HubRoutes, ServiceDefaults, PairingRules, BreakTimeRules (limits, defaults, default display
│                   text, line-break normalization, display-text validity incl. emojis, times), AppRuleRules
│                   (budget 0…1439, deny by default, IsAllowedOn, Combine, H:MM), WarningRules (1…59, early > final)
├── Resources/      DefaultTexts (US-006, ADR-016): the default kid texts (break display text, budget text, early and
│                   final warning), German neutral + English satellite (en\EagleEye.Shared.resources.dll, shipped
│                   by every installer). The app that shows a text renders the default in its own UI language;
│                   a text equal to the default of any language means "default".
├── Communication/  ReconnectSchedule, ConnectBackoff
├── Data/           SqliteDatabase base (pragmas, integrity check, migrations)
└── Logging/        RollingFileLoggerProvider: EagleEye file logging (ADR-002 note), used by the service
```

## Dependencies

No dependency on other EagleEye components. Packages: `Microsoft.Data.Sqlite`, `Microsoft.Extensions.Logging.Abstractions`.
All other components depend on this library, never on each other.

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
├── Contracts/     SignalR hub interfaces and DTOs (the API contract)
└── Models/        Shared domain models
```

## Dependencies

None. This library has no dependency on other EagleEye components.
All other components depend on this library, never on each other.

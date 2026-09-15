# EagleEye — Product Design Principles

*Status: Stub — to be completed by ARC agent in Phase 2*
*Maintainer: ARC Agent*

---

This document defines the design principles and guardrails that all development on EagleEye must follow. It is the primary reference for the DEV agent when making implementation decisions.

## Principles to Define (Phase 2)

The ARC agent will complete this document covering at minimum:

1. **Separation of Concerns** — one class, one responsibility; component boundaries and what crosses them
2. **API-First** — how and when to change `EagleEye.Shared/Contracts/`; no implementation precedes contract changes
3. **Component Architecture** — how functional areas map to folders and classes; dependency rules between components
4. **Error Handling** — strategy for exceptions, logging, and recovery across service and clients
5. **Configuration Management** — where and how configuration is stored per user on Windows; how clients read/write it
6. **Security** — credential handling, TLS trust, no secrets in code
7. **Async/Await** — when to use async, how to avoid deadlocks in WinForms and MAUI
8. **Testability** — design for unit testability; dependency injection patterns; mock boundaries
9. **Logging** — what to log, at what level, where logs are stored; nothing sensitive in logs
10. **Trunk-Based Development** — branch naming, commit hygiene, feature flags if needed

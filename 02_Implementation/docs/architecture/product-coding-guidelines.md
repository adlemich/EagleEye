# EagleEye — Product Coding Guidelines

*Status: Stub — to be completed by ARC agent in Phase 2*
*Maintainer: ARC Agent*

---

This document defines the coding standards that the DEV agent must follow for all production code in EagleEye. It is derived from .NET 10 best practices and the specific needs of this project.

## Guidelines to Define (Phase 2)

The ARC agent will complete this document covering at minimum:

1. **Naming Conventions** — classes, interfaces (I-prefix), methods (Async suffix), fields, constants, enums
2. **File and Class Organization** — one class per file, namespace matches folder structure, file naming
3. **Method Size and Complexity** — maximum method length, cyclomatic complexity limits
4. **XML Documentation** — required for all public types and members; format and content standards
5. **Null Safety** — nullable reference types enabled; no null suppression without justification
6. **LINQ Usage** — when to use LINQ vs explicit loops; performance considerations
7. **Exception Handling** — what to catch, what to rethrow, how to log; no swallowing exceptions silently
8. **Dependency Injection** — constructor injection only; no service locator; how to register services
9. **Unit Test Standards** — test class naming, method naming (`MethodName_Scenario_Expected`), Arrange/Act/Assert structure, one assertion per test
10. **WinForms Guidelines** — UI thread marshalling, event handler patterns, resource disposal
11. **MAUI Guidelines** — MVVM pattern, data binding, platform-specific code isolation, navigation
12. **SignalR Client Guidelines** — connection lifecycle, reconnection strategy, hub method naming
13. **Windows Service Guidelines** — hosted service pattern, graceful shutdown, background task management

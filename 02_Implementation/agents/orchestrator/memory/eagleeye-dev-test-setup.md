---
name: eagleeye-dev-test-setup
description: EagleEye two-machine development setup (Windows dev machine + MacBook) and manual testing, since 2026-10-03
metadata:
  type: project
---

Since 2026-10-03 (ADR-007, Michael's direction), EagleEye is developed on **two machines sharing the repo** (story work on the story's feature branch, see [[eagleeye-workflow-gates]]):

- **Windows 11 developer machine** (`Platform: win32`; checkout at `C:\Users\Admin\AppDevelopment\EagleEye` as of 2026-10-03, but never hard-code it): Service, TrayClient, Shared, ParentApp **Windows** target (new "Windows client") and **Android** target, Inno Setup installer. It is also the **manual test station**.
- **MacBook** (`Platform: darwin`): ParentApp **macOS** (Mac Catalyst) and **iOS** targets, `.dmg`.

The old MacBook + VMware Fusion VM setup is gone. Acceptance/E2E testing is **100 % manual by Michael**. TES became "Manual Test Lead" and writes test plans and test-run checklists in `02_Implementation/docs/testing/`; Michael ticks results in the Markdown run files. DEV unit tests stay automated. That was my interpretation of "all testing manual", flagged to Michael; revisit if he says otherwise.

**Why:** Michael has a native Windows box now and wants each toolchain on its natural host.

**How to apply:** at session start, state the host and git sync state (`pwsh 02_Implementation/scripts/env-check.ps1`). Route work to the right machine; never attempt the other machine's builds. Docs work is fine anywhere. Full rules: `02_Implementation/docs/dev-process/dev-environments.md`. See [[eagleeye-workflow-gates]].

Toolchain on the Windows machine as of 2026-10-03: .NET SDK 10.0.401, pwsh 7.6.6, MAUI workload present; Inno Setup, adb and Docker/Podman not installed; no `secrets/secrets.json` yet.

Toolchain on the MacBook as of 2026-10-04: macOS 27.0, .NET SDK 10.0.401, pwsh 7.6.5, Xcode 27.0, Podman 6.0.2 (no Docker; `plantuml.ps1` falls back to Podman). **No .NET workloads installed** (no `maui`/`maui-maccatalyst`), so the Mac Catalyst build cannot run until `dotnet workload install maui` (needs internet + Michael's go). No `secrets/secrets.json`.

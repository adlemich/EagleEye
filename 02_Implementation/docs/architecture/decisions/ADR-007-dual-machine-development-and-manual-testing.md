# ADR-007: Two-Machine Development and Manual Acceptance Testing

**Status**: Accepted (§2 "Distribution" superseded by ADR-009, 2026-10-04)
**Date**: 2026-10-03
**Deciders**: Michael (project owner)

---

## Context

The original setup (ADR-001, `01_Intend_and_Constraints/technology_selection.md`) assumed a single MacBook as the development machine, with Windows 11 running as a VMware Fusion VM for testing, and automated E2E tests written by TES.

Michael now has a dedicated **Windows 11 developer machine** in addition to the MacBook, and wants to:

1. develop on both machines with the same git repository,
2. do all Windows work (Service, TrayClient, a Windows parent client) and all Android work on the Windows machine,
3. do the macOS desktop parent app on the MacBook,
4. replace automated acceptance/E2E testing with 100 % manual testing on the Windows machine, supported by checklists and a feedback mechanism.

---

## Decision

### 1. Two development machines, one repository

| Machine | Builds, develops and tests |
|---|---|
| **Windows Developer Machine** | `EagleEye.Shared`, `EagleEye.Service`, `EagleEye.TrayClient`, `EagleEye.ParentApp` Windows target (new) and Android target, Windows installer |
| **MacBook** | `EagleEye.ParentApp` Mac Catalyst target, iOS target, `.dmg` packaging |

- Both machines work on `main` of `https://github.com/adlemich/EagleEye.git`. Git is the only sync mechanism.
- No absolute paths in any instruction, document or script. Everything is repo-relative.
- Agents detect the host (`Platform:` in the Claude Code environment, `$IsWindows` / `$IsMacOS` in PowerShell) and only do the work that belongs to it.
- `EagleEye.ParentApp.csproj` selects its target frameworks by host OS. `Directory.Build.props` enables Windows targeting so the solution loads on macOS.
- Build and test scripts (`scripts/build.ps1`, `scripts/test.ps1`) build and test only the host's components. Packaging scripts refuse to run on the wrong host.
- Contract changes in `EagleEye.Shared/Contracts/` are made on the Windows machine, where both server and clients can be built.

Details: `02_Implementation/docs/dev-process/dev-environments.md`.

### 2. Windows parent client

The parent app gains a **Windows desktop target** (MAUI on WinUI, `net10.0-windows10.0.19041.0`, minimum Windows 11). It shares the single ParentApp codebase and gets a desktop-style UI like the macOS app. The resulting requirement changes are recorded in `general-product-requirements.md` v1.1 (pending Michael's review).

Michael's clarifications (2026-10-03):

- **Role**: Android and iOS are the primary production platforms. The Windows client is the **initial testing vehicle** for parent-side features, which can then be tested on the Windows developer machine before the mobile apps exist.
- ~~**Distribution**: an extra app with no installer, deployed by copying a single `.exe` (FR-APP-092). Feasibility of a true single-file MAUI/WinUI publish must be validated early (coding guidelines §16.1).~~
  **Superseded by ADR-009 (2026-10-04)**: requirements v1.2 (FR-APP-092) replace copy deployment with a separate, self-contained, per-user Inno Setup installer with install, repair and uninstall. The single-file validation is no longer needed.
- **Communication**: same model as the mobile apps (TLS + pairing via `ParentHub`). This holds both on the service PC and remotely (FR-APP-091).

### 3. Manual acceptance testing

- All acceptance and E2E testing is **manual**, executed by Michael, primarily on the Windows Developer Machine. macOS parent-app cases run on the MacBook against the service on the Windows machine.
- TES becomes the **Manual Test Lead**: test plans, test-run checklists, guided sessions, result evaluation, test reports, issues, and the regression checklist. No automated E2E tests. The `tests/EagleEye.E2E.Tests` project is retired.
- Feedback mechanism: Michael records results directly in Markdown test-run files (`docs/testing/US-XXX/test-run-NN.md`): a Pass/Fail/Blocked/Skipped tick box per case, plus Observed, Notes, an evidence folder, and a General Feedback section. TES evaluates these files.
- DEV keeps writing **automated unit tests** (xUnit + Moq). They are developer tooling, not acceptance testing.

Process: `02_Implementation/docs/testing/README.md`.

---

## Rationale

- Windows-specific components (SYSTEM service, WinForms tray, WinUI, Inno Setup) build and debug natively on Windows, with no VM hop.
- The Android toolchain works well on Windows, so the MacBook only has to carry the Apple-only targets.
- The Windows machine is also the real target environment, so testing there is closer to production than a VM.
- Manual testing by the product owner fits the current stage: a UI-heavy product (tray, notifications, installer, multiple apps on several OSes) where E2E automation would cost more than it returns.

### Alternatives Considered

| Option | Reason Rejected |
|---|---|
| Keep MacBook + Windows VM | Michael's direction; a native Windows machine is simpler and faster |
| Build everything on one machine | Mac Catalyst and iOS require macOS/Xcode; Inno Setup and WinUI require Windows |
| Keep automated E2E tests alongside manual tests | Michael's direction: 100 % manual acceptance testing |
| Drop unit tests too | Unit tests are cheap, run in seconds and protect refactoring; they are not acceptance tests. Can be revisited if Michael decides otherwise. |

---

## Consequences

### Positive

- Native toolchains on both machines, and clear ownership of each build target
- Testing happens on the real target OS
- Test results are versioned, reviewable Markdown records, readable on both machines

### Negative / Trade-offs

- Work must be committed (and pushed) before switching machines, or it is invisible on the other machine
- Cross-machine stories (a contract change plus a macOS UI change) need sequencing in the implementation plan
- Manual testing costs Michael's time on every run, including regression. The regression checklist must stay lean.
- The test station is also the dev machine: the installed service enforces rules for every standard account on it (admin accounts are not monitored)

### Neutral

- The previous VMware Fusion and remote-debugging setup is dropped
- iOS (a primary production platform, together with Android) is built on the MacBook.

---

## References

- `02_Implementation/docs/dev-process/dev-environments.md`
- `02_Implementation/docs/testing/README.md`
- `01_Intend_and_Constraints/questions_and_answers.md`, Group 9
- ADR-001: Technology Selection (development setup superseded in part by this ADR)
- ADR-009: Windows Parent App Packaging and the ParentApp.Core Library (supersedes §2 "Distribution")

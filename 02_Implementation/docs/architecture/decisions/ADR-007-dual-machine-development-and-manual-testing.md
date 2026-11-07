# ADR-007: Two-Machine Development and Manual Acceptance Testing

**Status**: Accepted (§2 "Distribution" superseded by ADR-009, 2026-10-04; machine roles and §2 "Role" amended by CR-001 / ADR-019, 2026-10-20; third, test-only machine and Windows parent client minimum OS amended by US-009, accepted 2026-10-31; second test-only machine (gaming PC) amended by US-010, accepted 2026-11-01)
**Date**: 2026-10-03
**Deciders**: the project owner

> **Amendment 2026-11-01 (US-010) — approved by the project owner together with the US-010 implementation plan (2026-11-01)**: a second **test-only machine**: the kid's **gaming PC PILOT-PC** (Windows 11, one monitor, the kids' games; also the pilot service PC that the Mac app paired with in US-008). Like the Windows 10 test PC: no repo, no agent sessions, no builds; the project owner copies the git-ignored installers from `03_Delivery/windows/` to it and runs the game test cases there. It is not the Windows Developer Machine `DEV-PC`. `02_Implementation/docs/dev-process/dev-environments.md` §1 gets the matching row (Orchestrator, after approval).

> **Amended by CR-001 (2026-10-20, ADR-019)**: the iOS and Android parent apps are dropped (the project owner, 2026-10-20). The two-machine setup and manual testing stay as decided. Changed:
>
> 1. **Machine roles** (§1): the Windows Developer Machine builds Shared, Service, TrayClient, the ParentApp **Windows** target and the Windows installers; it has no Android target, workload, SDK, emulator or APK. The MacBook builds Shared and the ParentApp **Mac Catalyst** target and packages the `.dmg`; it has no iOS target. Xcode stays on the MacBook for Mac Catalyst only.
> 2. **Role of the Windows parent app** (§2): it is a **production** platform, together with the macOS app, not only the initial testing vehicle. Both apps have the same features (FR-APP-082); the macOS app catches up in the new US-007 "macOS parent app parity".
> 3. **Communication** (§2): "same model as the mobile apps" now means the same model as the macOS app (FR-APP-091, FR-APP-111).
>
> The original text below is kept as history; superseded statements are struck through.

> **Amendment 2026-10-31 (US-009) — accepted, approved by the project owner together with the US-009 implementation plan (2026-10-31)**:
>
> 1. **Third machine, test only** (§1, §3): the project owner's physical **Windows 10 22H2 PC** (x64, same LAN as the service PC) is used for manual tests of the Windows parent app on Windows 10. It has no repo clone, runs no agent sessions and builds nothing; installers are copied to it from `03_Delivery/windows/` on the Windows Developer Machine. Steps that need it are marked "Windows 10 PC" in implementation plans. `02_Implementation/docs/dev-process/dev-environments.md` §1 gets the matching row (Orchestrator, after approval).
> 2. **Minimum OS of the Windows parent client** (§2): "minimum Windows 11" becomes "Windows 10 22H2 (build 19045) or Windows 11" (requirements v1.9, ADR-009 amendment).

---

## Context

The original setup (ADR-001, `01_Intend_and_Constraints/technology_selection.md`) assumed a single MacBook as the development machine, with Windows 11 running as a VMware Fusion VM for testing, and automated E2E tests written by TES.

The project owner now has a dedicated **Windows 11 developer machine** in addition to the MacBook, and wants to:

1. develop on both machines with the same git repository,
2. do all Windows work (Service, TrayClient, a Windows parent client) ~~and all Android work~~ on the Windows machine *(Android dropped, CR-001)*,
3. do the macOS desktop parent app on the MacBook,
4. replace automated acceptance/E2E testing with 100 % manual testing on the Windows machine, supported by checklists and a feedback mechanism.

---

## Decision

### 1. Two development machines, one repository

| Machine | Builds, develops and tests |
|---|---|
| **Windows Developer Machine** | `EagleEye.Shared`, `EagleEye.Service`, `EagleEye.TrayClient`, `EagleEye.ParentApp` Windows target (new) ~~and Android target~~, Windows installer |
| **MacBook** | `EagleEye.Shared`, `EagleEye.ParentApp` Mac Catalyst target, ~~iOS target,~~ `.dmg` packaging |

- Both machines work on `main` of `https://github.com/adlemich/EagleEye.git`. Git is the only sync mechanism.
- No absolute paths in any instruction, document or script. Everything is repo-relative.
- Agents detect the host (`Platform:` in the Claude Code environment, `$IsWindows` / `$IsMacOS` in PowerShell) and only do the work that belongs to it.
- `EagleEye.ParentApp.csproj` selects its target frameworks by host OS. `Directory.Build.props` enables Windows targeting so the solution loads on macOS.
- Build and test scripts (`scripts/build.ps1`, `scripts/test.ps1`) build and test only the host's components. Packaging scripts refuse to run on the wrong host.
- Contract changes in `EagleEye.Shared/Contracts/` are made on the Windows machine, where both server and clients can be built.

Details: `02_Implementation/docs/dev-process/dev-environments.md`.

### 2. Windows parent client

The parent app gains a **Windows desktop target** (MAUI on WinUI, `net10.0-windows10.0.19041.0`, minimum Windows 11). It shares the single ParentApp codebase and gets a desktop-style UI like the macOS app. The resulting requirement changes are recorded in `general-product-requirements.md` v1.1 (pending the project owner's review).

The project owner's clarifications (2026-10-03):

- ~~**Role**: Android and iOS are the primary production platforms. The Windows client is the **initial testing vehicle** for parent-side features, which can then be tested on the Windows developer machine before the mobile apps exist.~~
  **Amended by CR-001 (2026-10-20)**: Windows and macOS are the two production platforms; iOS and Android are dropped (ADR-019).
- ~~**Distribution**: an extra app with no installer, deployed by copying a single `.exe` (FR-APP-092). Feasibility of a true single-file MAUI/WinUI publish must be validated early (coding guidelines §16.1).~~
  **Superseded by ADR-009 (2026-10-04)**: requirements v1.2 (FR-APP-092) replace copy deployment with a separate, self-contained, per-user Inno Setup installer with install, repair and uninstall. The single-file validation is no longer needed.
- **Communication**: same model as the ~~mobile apps~~ macOS app *(CR-001)* (TLS + pairing via `ParentHub`). This holds both on the service PC and remotely (FR-APP-091).

### 3. Manual acceptance testing

- All acceptance and E2E testing is **manual**, executed by the project owner, primarily on the Windows Developer Machine. macOS parent-app cases run on the MacBook against the service on the Windows machine.
- TES becomes the **Manual Test Lead**: test plans, test-run checklists, guided sessions, result evaluation, test reports, issues, and the regression checklist. No automated E2E tests. The `tests/EagleEye.E2E.Tests` project is retired.
- Feedback mechanism: the project owner records results directly in Markdown test-run files (`docs/testing/US-XXX/test-run-NN.md`): a Pass/Fail/Blocked/Skipped tick box per case, plus Observed, Notes, an evidence folder, and a General Feedback section. TES evaluates these files.
- DEV keeps writing **automated unit tests** (xUnit + Moq). They are developer tooling, not acceptance testing.

Process: `02_Implementation/docs/testing/README.md`.

---

## Rationale

- Windows-specific components (SYSTEM service, WinForms tray, WinUI, Inno Setup) build and debug natively on Windows, with no VM hop.
- ~~The Android toolchain works well on Windows, so the MacBook only has to carry the Apple-only targets.~~ *(Android dropped, CR-001; the MacBook carries the Mac Catalyst target only.)*
- The Windows machine is also the real target environment, so testing there is closer to production than a VM.
- Manual testing by the product owner fits the current stage: a UI-heavy product (tray, notifications, installer, multiple apps on several OSes) where E2E automation would cost more than it returns.

### Alternatives Considered

| Option | Reason Rejected |
|---|---|
| Keep MacBook + Windows VM | The project owner's direction; a native Windows machine is simpler and faster |
| Build everything on one machine | Mac Catalyst (~~and iOS~~, dropped by CR-001) require macOS/Xcode; Inno Setup and WinUI require Windows |
| Keep automated E2E tests alongside manual tests | The project owner's direction: 100 % manual acceptance testing |
| Drop unit tests too | Unit tests are cheap, run in seconds and protect refactoring; they are not acceptance tests. Can be revisited if the project owner decides otherwise. |

---

## Consequences

### Positive

- Native toolchains on both machines, and clear ownership of each build target
- Testing happens on the real target OS
- Test results are versioned, reviewable Markdown records, readable on both machines

### Negative / Trade-offs

- Work must be committed (and pushed) before switching machines, or it is invisible on the other machine
- Cross-machine stories (a contract change plus a macOS UI change) need sequencing in the implementation plan
- Manual testing costs the project owner's time on every run, including regression. The regression checklist must stay lean.
- The test station is also the dev machine: the installed service enforces rules for every standard account on it (admin accounts are not monitored)

### Neutral

- The previous VMware Fusion and remote-debugging setup is dropped
- ~~iOS (a primary production platform, together with Android) is built on the MacBook.~~ *(iOS and Android dropped, CR-001 / ADR-019, 2026-10-20.)*

---

## References

- `02_Implementation/docs/dev-process/dev-environments.md`
- `02_Implementation/docs/testing/README.md`
- `01_Intend_and_Constraints/questions_and_answers.md`, Group 9
- ADR-001: Technology Selection (development setup superseded in part by this ADR)
- ADR-009: Windows Parent App Packaging and the ParentApp.Core Library (supersedes §2 "Distribution")
- ADR-019: Parent App Platforms — Windows and macOS Only (amends §1 and §2, CR-001)

# ADR-009: Windows Parent App Packaging and the ParentApp.Core Library

**Status**: Accepted (approved by Michael with the US-002 implementation plan, 2026-10-04)
**Date**: 2026-10-04
**Deciders**: ARC, Michael

---

## Context

1. **Packaging.** Requirements v1.2 (FR-APP-092, Michael 2026-10-04) replace copy deployment with a separate, self-contained Windows installer with install, repair and uninstall. ADR-007 §2 and coding guidelines §16.1 still describe a single copied `.exe`.
2. **Testability.** `EagleEye.ParentApp` is a multi-targeted MAUI executable (`net10.0-windows…`, `net10.0-android`, `net10.0-maccatalyst`). The unit test project `EagleEye.ParentApp.Tests` targets plain `net10.0` and cannot reference it. US-002 brings the first real parent-app logic (connection, pairing, state), which must be unit-tested with 100 % branch coverage (dev-process §9).
3. **Two-machine build.** The MacBook currently has no MAUI workload. Logic that can be built and tested without MAUI is useful on both machines.

---

## Decision

### 1. Windows packaging: unpackaged, self-contained, Inno Setup, per-user

- The Windows target is published **unpackaged** (`WindowsPackageType=None`), **self-contained** (.NET runtime and Windows App SDK included, `WindowsAppSDKSelfContained=true`), `win-x64`. No MSIX: sideloading MSIX needs a trusted signing certificate on every PC, which is not a "clean" installer for a parent.
- A separate Inno Setup script `installer/windows/parentapp-setup.iss` produces `03_Delivery/windows/EagleEye-ParentApp-Setup-<version>.exe`. It has its own `AppId` and is independent of the service installer.
- **Per-user installation** (`PrivilegesRequired=lowest`, default folder `%LocalAppData%\Programs\EagleEye Parent App`). No admin rights are needed. The app is visible only to the Windows user who installed it, so on a shared PC it stays out of the kid's account (US-002 AC-1).
- Uninstall removes the program folder, the Start menu entry and the app data folder `%LocalAppData%\EagleEye\` of that user (US-002 AC-5). Re-running the installer (repair/update) keeps the app data (AC-4).
- Display name "EagleEye Parent App", publisher "Michael Adler", version from `Directory.Build.props`. Wizard languages German (default) and English.
- `scripts/package-windows.ps1` builds both installers.

This supersedes ADR-007 §2 "Distribution" and coding guidelines §16.1 "Packaging".

### 2. New project `EagleEye.ParentApp.Core`

- `src/EagleEye.ParentApp.Core/` is a plain `net10.0` class library. It holds all parent-app logic that does not need MAUI: communication (SignalR client, certificate pinning, connection and pairing coordination), data access (SQLite), view models (CommunityToolkit.Mvvm), host validation, and the localized texts (resx).
- `src/EagleEye.ParentApp/` (MAUI) keeps views, `MauiProgram`, platform code (`Platforms/…`) and the implementations of the small abstractions Core needs from the platform (main-thread dispatch, theme switching, secret storage, dialogs).
- Dependencies: `ParentApp → ParentApp.Core → Shared`. `ParentApp.Core` is part of the ParentApp component. The rule "components depend only on Shared" (dev-process §6.2) still holds between Service, TrayClient and ParentApp.
- `tests/EagleEye.ParentApp.Tests` references `ParentApp.Core` and runs on both machines without a MAUI workload.

---

## Rationale

### Alternatives Considered

| Option | Reason Rejected |
|---|---|
| Copy deployment (single exe) | Replaced by Michael's decision (FR-APP-092 v1.2). |
| MSIX package | Needs a trusted code-signing certificate on each PC; sideloading self-signed MSIX requires manual certificate import. |
| Per-machine installer (admin) | Needs admin rights for every update, and puts the parent app into every account on the PC, including the kid's. Its uninstaller could not remove the data in other users' profiles. |
| Add a plain `net10.0` TFM to the MAUI project for tests | Works, but mixes MAUI and non-MAUI builds in one project, and view models could still depend on MAUI types. A separate library makes the boundary explicit and buildable without the MAUI workload. |

---

## Consequences

### Positive

- One-click installer without prerequisites; clean uninstall; updates keep the pairing.
- Parent-app logic is unit-testable on Windows and macOS, with no MAUI workload needed for tests.
- Later Android, iOS and macOS work reuses Core unchanged and only adds views and platform code.

### Negative / Trade-offs

- The installer is large (self-contained .NET plus Windows App SDK, roughly 100 to 150 MB). Accepted for a parent tool.
- One more project in the solution.
- A per-user install exists once per Windows account. A second parent on the same PC installs it again in their own account.

### Neutral

- The installer is not code-signed, so SmartScreen may warn on first run, as with the service installer.

---

## Implementation Notes (US-002, accepted deviations, 2026-10-04)

- Secret storage split (D-2): the table logic is `ParentApp.Core/Data/ProtectedSecretStore` (`ISecretStore`, unit-tested); only the encryption is platform code, `ISecretProtector` implemented on Windows by `Platforms/Windows/DpapiSecretProtector` (DPAPI `CurrentUser`). Other platforms register `MauiSecureStorageSecretStore` as `ISecretStore`.
- Windows minimum version (D-3): the csproj sets `SupportedOSPlatformVersion` 10.0.19041.0 (the SDK rejects a minimum above the TFM version); Windows 11 is enforced by the installer (`MinVersion=10.0.22000`).
- Publish (D-4): `RuntimeIdentifier=win-x64`, `SelfContained`, `WindowsAppSDKSelfContained`, `WindowsPackageType=None` are set in the csproj for the Windows TFM only, not on the command line (a command-line `-r` also hits the Android target).
- Installer (D-14): the uninstaller ends running parent app processes of the current user; the Start menu entry sits directly in *Programs*; an optional "launch now" checkbox. Actual installer size is about 74 MB.

---

## References

- Requirements v1.2: FR-APP-092, §9.5
- ADR-007 §2 (superseded in part), coding guidelines §16.1 (superseded in part)
- US-002 implementation plan

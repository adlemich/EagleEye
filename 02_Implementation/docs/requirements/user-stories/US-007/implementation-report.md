# Implementation Report: US-007 — macOS Parent App Parity

**Author**: DEV
**Date**: 2026-10-20 (started)
**Branch**: `feature/US-007-macos-parent-app-parity`
**Plan**: `02_Implementation/docs/requirements/user-stories/US-007/implementation-plan.md` (approved 2026-10-20 with Q-1 and Q-2), ADR-020, the US-007 amendments of ADR-019, arc42 and coding guidelines §13
**Machine(s) used**: MacBook so far (see §7)
**Story status**: Analyzed (in progress: Step 1 "first light")

> This report grows with each plan step. Steps done: 0. Step 1 in progress. Steps 2 to 9 not started (Step 2 waits for ARC's review of `US-007/first-light.md`).

---

## 1. Summary

| Step | State | Content |
|---|---|---|
| 0 | Done | Version 0.7.0 (`Directory.Build.props`). Build and unit tests green on the MacBook. |
| 1 | In progress | Mac app built and launched on the MacBook. Attempt 1 to pair failed: MAUI `SecureStorage` could not store the token (TI-1). First-light fix FL-1: `MacKeychainSecretStore` (ADR-020 §2, K-1) pulled forward from Step 2. Waiting for Michael's second attempt. Gap list in `02_Implementation/docs/requirements/user-stories/US-007/first-light.md`. |

## 2. Deviations from the Implementation Plan

| # | Plan | Done instead | Why |
|---|---|---|---|
| DV-1 | Step 1 allows only `UseNativeHttpHandler`, `NSLocalNetworkUsageDescription`, `ValidateXcodeVersion` as fixes; the Keychain store is Step 2 (Decision 2). | Fix FL-1 in Step 1: `MacKeychainSecretStore` (K-1), `SecretStoreUnavailableException` (Core) with unit tests, `MauiSecureStorageSecretStore` deleted (plan Step 2.5). Sandbox, data path, `MainPage` handling and `ErrorKeychainAccess` stay in Step 2. | Pairing was impossible without it: MAUI `SecureStorage` throws when it stores the token (ad-hoc build, no keychain entitlement; `first-light.md` §3). Approved by the orchestrator for first light on 2026-10-20 ("pull the ADR-020 Keychain store forward if the secret store is the cause"). The sandbox stays on for now, because turning it off without the Decision 4 data path would move the database to `~/Documents`. |

Notes (not deviations):
- Step 1 launches the app through LaunchServices (`open`), not by running the executable inside the bundle from a shell as plan Step 1.1 suggests. Reason: macOS Local Network privacy attributes LAN access to the responsible process; a child of a shell without that permission cannot reach the LAN at all (observed in the DEV shell). Console output is captured with `open --stdout/--stderr`.
- A fallback build with `UseNativeHttpHandler=false` (TI-2) was prepared for attempt 1. It failed in the same way (secret store), and the log shows that the native handler does call the trust callback (G-7). The fallback was deleted, and `UseNativeHttpHandler` is not changed in Step 1.

## 3. Files

**Step 0** — changed: `02_Implementation/Directory.Build.props` (0.6.2 → 0.7.0).
**Step 1** — new: `02_Implementation/docs/requirements/user-stories/US-007/first-light.md`, this report, `src/EagleEye.ParentApp/Platforms/MacCatalyst/MacKeychainSecretStore.cs`, `src/EagleEye.ParentApp.Core/Abstractions/SecretStoreUnavailableException.cs`, `tests/EagleEye.ParentApp.Tests/Abstractions/SecretStoreUnavailableExceptionTests.cs`; changed: `src/EagleEye.ParentApp/MauiProgram.cs` (Mac secret store registration), `src/EagleEye.ParentApp/README.md` (class name), `tests/EagleEye.ParentApp.Tests/Data/PairingStoreTests.cs` (refusing store); deleted: `src/EagleEye.ParentApp/Services/MauiSecureStorageSecretStore.cs`.

## 4. Unit Tests

`scripts/test.ps1` on the MacBook: Step 0 **1 197 passed** (Shared 282, ParentApp 915); after FL-1 **1 202 passed, 0 failed** (Shared 282, ParentApp 920).

New tests: `SecretStoreUnavailableExceptionTests` (operation and status, message without secret, type), `PairingStoreTests.LoadAsync_SecretStoreUnavailable_PropagatesAndKeepsRow`, `PairingStoreTests.SaveAsync_SecretStoreUnavailable_PropagatesAndWritesNoRow`. `MacKeychainSecretStore` is platform code, not unit-tested (plan); DEV self-test of the read path in `first-light.md` §3.

## 5. Open Questions and Risks

- See `first-light.md` for the gaps found so far. G-5 (silent pairing failure, Core) needs ARC's decision for Step 2.
- The Keychain write (`SecItemAdd`, K-1, inside the sandbox) is verified only by Michael's second pairing attempt. If it fails, pairing will again fail without a visible message (G-5); the unified log will then show `SecretStoreUnavailableException`.
- Attempt 1 left most likely two orphan paired devices "Michael's MacBook" on the service (harmless, no UI to remove them).
- The Keychain (TI-1, across rebuilds) and Release trimming (TI-5) probes are still pending.

## 6. How to Test

Not yet (story in progress). Current dev build: `02_Implementation/src/EagleEye.ParentApp/bin/Debug/net10.0-maccatalyst/maccatalyst-arm64/EagleEye.app`, started with `open`.

## 7. Machine(s) Used

| Step | Machine |
|---|---|
| 0 | MacBook |
| 1 | MacBook (+ service PC `192.168.1.37` for the pairing code) |

# US-007 — First Light (Step 1)

**Author**: DEV
**Date**: 2026-10-20
**Status**: In progress. Attempt 1 failed (pairing not stored, §3). Fix FL-1 built and launched; waiting for Michael's second pairing attempt.
**Plan**: `02_Implementation/docs/requirements/user-stories/US-007/implementation-plan.md`, Step 1, Decision 1
**Host**: MacBook (macOS 27.0.1, Apple Silicon), Xcode 27.1, .NET SDK 10.0.401, workload `maui-maccatalyst` 10.0.110
**Build**: 0.7.0, Debug, Mac Catalyst

Purpose: run the Mac app as it is, pair it with the service PC `192.168.1.37`, and record every gap. Only fixes needed to reach a paired app are allowed in this step (Decision 1, plus the orchestrator's go of 2026-10-20 to pull the ADR-020 secret store forward if the secret store is the cause). Everything else is a gap for Steps 2 to 6. ARC reviews this list before Step 2.

---

## 1. Build and launch (DEV, observed)

| Check | Result |
|---|---|
| `scripts/build.ps1` (Debug, MacBook) | 0 warnings, 0 errors. `ValidateXcodeVersion=false` from CR-001 still needed (Xcode 27.1). |
| Signature | `Signature=adhoc`, `TeamIdentifier=not set`, identifier `com.adlemich.eagleeye`. |
| Entitlements in the bundle | `com.apple.security.app-sandbox` = true, `com.apple.security.network.client` = true (as on `main`). |
| Bundle folder | `bin/Debug/net10.0-maccatalyst/maccatalyst-arm64/EagleEye.app` (name "EagleEye", not "EagleEye Parent App"). |
| HTTP handler | Native (`System.Net.Http.UseNativeHttpHandler` = true in `runtimeconfig.bin`). |
| Launch | Starts without exception. One window, 1024 x 768 points, owner "EagleEye". |
| Console output | One system notice: `application:performFetchWithCompletionHandler:` is implemented but `fetch` is not in `UIBackgroundModes` (G-1). When typing: "The selected font is a system font, but the macOS font panel will not show system fonts" (harmless). |
| Data folder used | `~/Library/Containers/com.adlemich.eagleeye/Data/Documents/EagleEye/EagleEye.ParentApp.db` (+ `-wal`, `-shm`): inside the sandbox container, `Documents`, not `Application Support` (G-2). |

**Launch note.** The app must be started through LaunchServices (`open …/EagleEye.app`, Finder or Dock), not as a child of a Terminal or agent shell. macOS Local Network privacy is attributed to the responsible process: a child of a shell without Local Network permission gets "No route to host" for every LAN address (seen for the DEV agent shell, even for the router). Console output: `open --stdout <file> --stderr <file> <app>`. Diagnosis: `/usr/bin/log show --predicate 'process == "EagleEye.ParentApp"'` (in zsh, `log` alone is a shell builtin).

## 2. Gap list

| ID | Area | What was seen | AC | Proposed step |
|---|---|---|---|---|
| G-1 | Info.plist | Console notice at start: `performFetchWithCompletionHandler:` implemented (MAUI default delegate) but no `fetch` background mode. Harmless; noise in the console. | — | 2 (check; likely ignore) |
| G-2 | Data path | Database in the sandbox container under `Documents/EagleEye`, not `~/Library/Application Support/EagleEye/`. | AC-6, AC-24 | 2 (no sandbox, Decision 4) |
| G-3 | App shell | Bundle and app name "EagleEye" (not "EagleEye Parent App"). | AC-4 | 3 |
| G-4 | Secret store | MAUI `SecureStorage` throws when the token is stored after a successful pairing (ad-hoc build, no keychain entitlement; TI-1 confirmed). Pairing impossible. | AC-20, AC-24 | **Fixed in Step 1 (FL-1)**, see §3 |
| G-5 | Pairing UI (Core) | **A pairing failure after the code was accepted is silent.** An exception from `PairingStore.SaveAsync` escapes `ConnectionCoordinator.SubmitCodeAsync`: no message is set, the pairing session stays open, and the UI shows nothing. A second click sends the code again; the service has already used it and answers "wrong code". The service keeps the device registered with a token that no app holds (orphan, see §3). Same on Windows if the DPAPI store failed. | AC-20, AC-24 | 2 (with `SecretStoreUnavailableException` handling: abort the session, show `ErrorKeychainAccess` or a pairing error). ARC to decide whether Core handles it in `CompletePairingAsync`. |
| G-6 | Local network | First connection attempt after "Verbinden" failed with "Local network prohibited" (`NSURLErrorDomain -1009`, "Network is down"); the next attempt 1.5 minutes later went through. Michael reported no prompt text; the app has no `NSLocalNetworkUsageDescription` (TI-4), so the prompt, if shown, had no explanation. The UI showed a connection error for the first attempt (to be confirmed). | AC-17, AC-18 | 2 (Info.plist text, TI-4) |
| G-7 | TLS handler | **Finding for ARC (TI-2 not reproduced):** the native handler (`NSUrlSessionHandler`) *did* call `ServerCertificateCustomValidationCallback` on .NET 10 / Mac Catalyst. Log: "Trust evaluate failure: [leaf AnchorTrusted …]" (system trust), then "auth completion disp=0", "Certificate verification result: OK", negotiate HTTP 200 over h2, and the hub call `SubmitPairingCode` reached the service. The managed handler was therefore not needed in Step 1. ADR-020 §3 can stay (it removes a dependency on Apple's handler behaviour), but it is no longer a blocker. | AC-19 | 2 (keep or drop `UseNativeHttpHandler=false`; ARC) |

*(Walk-through rows follow after the second attempt.)*

## 3. Pairing

### Attempt 1 (2026-10-20, 16:51 to 16:56) — failed

Michael entered `192.168.1.37`, the code from the tray and the device name, and clicked "Koppeln". Nothing visible happened. A second click showed the "code invalid, request a new code" message. Same with the fallback build (`UseNativeHttpHandler=false`).

Analysis (DEV, from the unified log, the container database and the code; no further input from Michael needed):

| Time | Build | Observed |
|---|---|---|
| 16:51:01 | as-is | Connection to `:5443` blocked: "unsatisfied (Local network prohibited)", `-1009` (G-6). |
| 16:52:40 | as-is | TCP + TLS 1.3 to the service; trust callback called and accepted (G-7); negotiate 200. |
| 16:52:59 | as-is | First Keychain call of the process (`SecSecurityClientGet`), then at once a managed `System.Exception` on the UI thread, caught and logged by AppKit (`[com.apple.AppKit:General] System.Exception`, reason redacted). |
| 16:56:21 | fallback | Same: Keychain call, then `System.Exception`. |
| — | both | `ServerConnections` and `Secrets` empty; the database WAL was last written at first start (16:37). No Keychain item with service `com.adlemich.eagleeye` or `com.adlemich.eagleeye.microsoft.maui.essentials.preferences`. |

Code path: `ServerConnectionViewModel.PairCommand` → `ConnectionCoordinator.SubmitCodeAsync` → service answers `Success` with device ID and token → `CompletePairingAsync` → `PairingStore.SaveAsync` → `ISecretStore.SetAsync` = `MauiSecureStorageSecretStore` → MAUI `SecureStorage.SetAsync` throws `System.Exception` (MAUI's error type for a failed `SecItemAdd`; on Mac Catalyst it uses the data-protection keychain, which needs the `keychain-access-groups` entitlement, TI-1). The exception escapes, so G-5.

**Consequence on the service PC**: each attempt consumed one code and created one paired device "Michael's MacBook" with a token that no app holds, so most likely **two orphan devices**. They are harmless: there is no device limit, the tokens never left the service except in the app's memory, and they do not affect the Windows app. There is no UI to remove another device's pairing (FR-APP-015, own device only). Michael can check the service log on the PC for two "paired" entries. Clean-up is optional and not needed for the next attempt.

### Fix FL-1 (first-light fix, pulled forward from plan Step 2, Decision 2 / ADR-020 §2)

- New `src/EagleEye.ParentApp/Platforms/MacCatalyst/MacKeychainSecretStore.cs`: Security framework (`SecKeyChain` / `SecRecord`), generic password, service `com.adlemich.eagleeye`, account = key (`pairing-token`), label "EagleEye Parent App", not synchronisable, **K-1** (`UseDataProtectionKeychain = false`). Not found → `null`; any other status → `SecretStoreUnavailableException` (status only, never the value).
- New `src/EagleEye.ParentApp.Core/Abstractions/SecretStoreUnavailableException.cs` + unit tests.
- `MauiProgram.cs`: Mac registers `MacKeychainSecretStore`; `MauiSecureStorageSecretStore` deleted.
- Not pulled forward (still Step 2): sandbox off, data path, `MainPage` handling of `SecretStoreUnavailableException`, `ErrorKeychainAccess`, G-5.

**DEV self-test without Michael (17:00)**: a dummy row in `ServerConnections` (no token) makes `PairingStore.LoadAsync` read the Keychain at start. Result: the read goes to `com.apple.securityd.xpc` (file-based keychain daemon) and returns without an exception (no managed exception in the log; `SecretStoreUnavailableException` is not caught by `MainPage` and would have shown up as MAUI's did). So **K-1 is reachable from the sandboxed, ad-hoc-signed Catalyst app**. The write (`SecItemAdd`) is only exercised by a real pairing. Dummy row removed afterwards.

### Attempt 2

*Pending: Michael pairs again with a new code.*

## 4. Keychain probe (step 1.4)

*Pending.*

## 5. Release probe (step 1.5)

*Pending.*

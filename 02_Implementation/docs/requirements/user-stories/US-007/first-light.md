# US-007 — First Light (Step 1)

**Author**: DEV
**Date**: 2026-10-20
**Status**: In progress: app built and launched, waiting for Michael to pair it (step 1.2)
**Plan**: `02_Implementation/docs/requirements/user-stories/US-007/implementation-plan.md`, Step 1, Decision 1
**Host**: MacBook (macOS 27.0.1, Apple Silicon), Xcode 27.1, .NET SDK 10.0.401, workload `maui-maccatalyst` 10.0.110
**Build**: 0.7.0, Debug, Mac Catalyst, code as on `main` (no Mac changes yet)

Purpose: run the Mac app as it is, pair it with the service PC `192.168.1.37`, and record every gap. Only fixes from the Decision 1 list are allowed in this step (`UseNativeHttpHandler=false`, `NSLocalNetworkUsageDescription`, `ValidateXcodeVersion`). Everything else is a gap for Steps 2 to 6. ARC reviews this list before Step 2.

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
| Console output | Only one system notice: `application:performFetchWithCompletionHandler:` is implemented but `fetch` is not in `UIBackgroundModes` (G-1). |
| Data folder used | `~/Library/Containers/com.adlemich.eagleeye/Data/Documents/EagleEye/EagleEye.ParentApp.db` (+ `-wal`, `-shm`): inside the sandbox container, `Documents`, not `Application Support` (G-2). |

**Launch note.** The app must be started through LaunchServices (`open …/EagleEye.app`, Finder or Dock), not as a child of a Terminal or agent shell. macOS Local Network privacy is attributed to the responsible process: a child of a shell that has no Local Network permission gets "No route to host" for every LAN address (seen for the DEV agent shell, even for the router). Console output can still be captured with `open --stdout <file> --stderr <file> <app>`.

**Fallback build prepared (not yet used).** Per TI-2, a second Debug build with `UseNativeHttpHandler=false` sits in `src/EagleEye.ParentApp/bin/FirstLight-ManagedHandler/EagleEye.app` (git-ignored, built with `-p:UseNativeHttpHandler=false` only, same code). It is used only if the as-is build cannot connect. Whether it was needed is recorded in §3.

## 2. Gap list

| ID | Area | What was seen | AC | Proposed step |
|---|---|---|---|---|
| G-1 | Info.plist | Console notice at start: `performFetchWithCompletionHandler:` implemented (MAUI default delegate) but no `fetch` background mode. Harmless; noise in the console. | — | 2 (check; likely ignore) |
| G-2 | Data path | Database in the sandbox container under `Documents/EagleEye`, not `~/Library/Application Support/EagleEye/`. | AC-6, AC-24 | 2 (no sandbox, Decision 4) |
| G-3 | App shell | Bundle and app name "EagleEye" (not "EagleEye Parent App"). | AC-4 | 3 |

*(Rows from the walk-through follow after Michael's observations.)*

## 3. Pairing (step 1.2)

*Pending: Michael pairs with `192.168.1.37`.*

## 4. Keychain probe (step 1.4)

*Pending.*

## 5. Release probe (step 1.5)

*Pending.*

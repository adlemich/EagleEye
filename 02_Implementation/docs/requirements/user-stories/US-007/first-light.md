# US-007 — First Light (Step 1)

**Author**: DEV
**Date**: 2026-10-20
**Status**: **Step 1 complete** (2026-10-20). Paired and connected on attempt 3 (FL-2 build). Gap list G-1 to G-18, Keychain probe and Release probe done. Waiting for ARC's review (§6) before Step 2.
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
| G-3 | App shell | Bundle and app name "EagleEye" (not "EagleEye Parent App"). The window title "EagleEye" is as planned (Decision 5). | AC-4 | 3 |
| G-4 | Secret store | MAUI `SecureStorage` throws when the token is stored after a successful pairing (ad-hoc build, no keychain entitlement; TI-1 confirmed). Pairing impossible. | AC-20, AC-24 | **Fixed in Step 1 (FL-1, then FL-2)**, see §3 |
| G-8 | Secret store (design, **for ARC**) | **ADR-020 §2 assumption does not hold:** on Mac Catalyst the `SecItem` API always uses the data-protection keychain. `kSecUseDataProtectionKeychain = false` is ignored; every write returns `errSecMissingEntitlement` (-34018), with and without App Sandbox. K-1 *and* K-2 via `SecItem` are impossible for an ad-hoc build; adding `keychain-access-groups` makes macOS refuse to launch the app. What works: the legacy file-keychain API (`SecKeychainAddGenericPassword` etc., deprecated since macOS 10.10 but present in macOS 27), which writes to the login keychain (K-1) with and without sandbox, without a prompt for the creating build. FL-2 uses it. ARC to confirm this as the ADR-020 §2 implementation (risk: Apple may remove the deprecated API; survival across a rebuild still to be probed in step 1.4 and Step 2). Evidence: `US-007/evidence/keychain-probe-results.txt`. | AC-5, AC-24 | ARC review before Step 2 |
| G-9 | Secret store | The legacy add call sets no label: the item shows in Keychain Access with name = service `com.adlemich.eagleeye`, not "EagleEye Parent App" (plan Data Locations). Setting the label needs an attribute list (`kSecLabelItemAttr`). | AC-24 (presentation only) | 2 |
| G-5 | Pairing UI (Core) | **A pairing failure after the code was accepted is silent.** An exception from `PairingStore.SaveAsync` escapes `ConnectionCoordinator.SubmitCodeAsync`: no message is set, the pairing session stays open, and the UI shows nothing. A second click sends the code again; the service has already used it and answers "wrong code". The service keeps the device registered with a token that no app holds (orphan, see §3). Same on Windows if the DPAPI store failed. | AC-20, AC-24 | 2 (with `SecretStoreUnavailableException` handling: abort the session, show `ErrorKeychainAccess` or a pairing error). ARC to decide whether Core handles it in `CompletePairingAsync`. |
| G-6 | Local network | First connection attempt after "Verbinden" failed with "Local network prohibited" (`NSURLErrorDomain -1009`, "Network is down"); the next attempt 1.5 minutes later went through. Michael reported no prompt text; the app has no `NSLocalNetworkUsageDescription` (TI-4), so the prompt, if shown, had no explanation. The UI showed a connection error for the first attempt (to be confirmed). | AC-17, AC-18 | 2 (Info.plist text, TI-4) |
| G-7 | TLS handler | **Finding for ARC (TI-2 not reproduced):** the native handler (`NSUrlSessionHandler`) *did* call `ServerCertificateCustomValidationCallback` on .NET 10 / Mac Catalyst. Log: "Trust evaluate failure: [leaf AnchorTrusted …]" (system trust), then "auth completion disp=0", "Certificate verification result: OK", negotiate HTTP 200 over h2, and the hub call `SubmitPairingCode` reached the service. The managed handler was therefore not needed in Step 1. ADR-020 §3 can stay (it removes a dependency on Apple's handler behaviour), but it is no longer a blocker. | AC-19 | 2 (keep or drop `UseNativeHttpHandler=false`; ARC) |

| G-10 | Look: accent | Native controls (switch "Dunkel", checkboxes) are **red**: Catalyst takes the macOS accent colour (MacBook: `AppleAccentColor` = 0, red). The heading bars are the app's fixed green (no Mac accent source, TI-7), and the buttons use the fixed `AccentLight/AccentDark` green. Three colour sources on one page. | AC-10 | 5 (heading bars from the macOS accent; decide whether buttons follow) |
| G-11 | Look: buttons | "Kopplung aufheben" and "Eintrag hinzufügen": white text on light green (`AccentDark`) in dark mode, poor contrast. Shared `Button` style; check how it looks on Windows before changing (AC-35). | AC-11 | 5 |
| G-12 | Look: value fields | `Entry` fields ignore their width and stretch to the next cell or beyond: Zeit-Ende Warnungen, "Minuten vor Ende" box runs under the Anzeigetext editor; Ruhezeiten, the Start-Zeit box runs under End-Zeit, and the End-Zeit box runs under the MO–FR checkboxes; Applikations-Regeln, the Stunden and Minuten boxes touch, and the Minuten box runs under MO–FR. Values are readable (no ISSUE-008 equivalent). | AC-11, AC-27, AC-29, AC-30 | 5 |
| G-13 | Look: editors | `Editor` (Anzeigetext on Settings, Anzeige Text on Rules) has no border, only a slightly lighter background. | AC-11 | 5 |
| G-14 | Layout | On Regeln and Berichte the heading bars run to the right window edge without the right margin they have on Einstellungen: probably the content is wider than the window (horizontal overflow, possibly caused by G-12). | AC-7 | 5 |
| G-15 | Look: picker | The Konto picker is a plain black box with no picker affordance (no chevron), black even in the dark theme. | AC-11, AC-29 | 5 |
| G-16 | Look: navigation | The selected menu item is a light grey bar with white text: weak contrast (DEV, from the screenshots). | AC-11 | 5 |
| G-17 | Data (no gap) | Applikations-Regeln lists "EA" four times. This is service data by design: one app record per program path, same display name allowed (`AppRuleDto`, OQ-12; EA ships several executables described as "EA"). The Windows app shows the same rows. To check on the Mac: the program-path tooltip that tells them apart (hover). | AC-30 | 6 (check only) |
| G-18 | Build | `dotnet publish -c Release` builds a universal app (x86_64 + arm64) plus a `.pkg`, because no `RuntimeIdentifier` is set for maccatalyst. | AC-3, §9.4 | 2 (`RuntimeIdentifier=maccatalyst-arm64`, already planned) |

**Seen working (attempt 3, screenshots `US-007/input/first-light-settings.png`, `first-light-rules.png`, `first-light-reports.png`, dark mode)**: green "Verbunden mit 192.168.1.37"; Einstellungen "Gekoppelt", "Gekoppelt mit: 192.168.1.37", "Dieses Gerät: Michael's MacBook"; real accounts Micha and Sebastian with ticks; warning minutes and texts; break times with weekdays; display text with an emoji; app rules with budgets; Reports for today and 17.10.2026 in German date format; dark theme throughout. Michael saw no prompt and no error.

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

### Attempt 2 (2026-10-20, 17:04 to 17:05, FL-1 build) — failed

Michael: "Same problem, it does not pair." Analysis from the unified log (no input from Michael needed):

| Time | Observed |
|---|---|
| 17:04:32 | First connection blocked: "Local network prohibited" (G-6 again; the new build is a new code identity). |
| 17:04:34 | Next attempt: TLS ok, negotiate 200. |
| 17:05:02 | Keychain call, then `EagleEye.ParentApp.Core.Abstractions.SecretStoreUnavailableException` (reason redacted in the log) escapes to AppKit. |
| 17:05:18 | Second click: same exception again. |
| — | No Keychain item, no `ServerConnections` row. Two more orphan devices on the service, most likely (G-5). |

So the flow stops at the Keychain write in `MacKeychainSecretStore.SetAsync`, after the service has accepted the code and returned the token. My startup self-test of FL-1 had only exercised the *read*, which returns "not found" either way, so it could not detect this.

### Keychain probe without Michael (17:06 to 17:15)

A temporary Debug-only probe (environment variable `EAGLEEYE_KCPROBE=1`, passed with `open --env`; the app runs the probe and exits) printed the OSStatus of add, read, update and delete for several variants. Full results: `US-007/evidence/keychain-probe-results.txt`; source: `US-007/evidence/keychain-probe.patch` (not committed to `src/`).

| Variant | Write result |
|---|---|
| `SecItem`, login keychain (`UseDataProtectionKeychain = false`), with or without label and sync attribute | -34018 `errSecMissingEntitlement` |
| `SecItem`, data-protection keychain, with or without `AfterFirstUnlockThisDeviceOnly` | -34018 |
| All of the above **without App Sandbox** (empty entitlements, clean rebuild) | -34018 |
| `keychain-access-groups` entitlement on the ad-hoc build | app does not launch ("Launchd job spawn failed", POSIX 163) |
| Legacy `SecKeychain…` API (P/Invoke), with and without sandbox | add, find, modify, delete all 0; no prompt |

**Root cause**: on Mac Catalyst every `SecItem` call goes to the data-protection keychain, which needs a keychain access group entitlement; an ad-hoc build cannot carry one (G-8).

### Fix FL-2 (replaces the FL-1 implementation; same class, same contract)

`MacKeychainSecretStore` now uses the legacy file-keychain API through `DllImport` into Security.framework: `SecKeychainFindGenericPassword`, `SecKeychainAddGenericPassword`, `SecKeychainItemModifyAttributesAndData`, `SecKeychainItemDelete` (+ `SecKeychainItemFreeContent`, `CFRelease`). Default keychain = login keychain (K-1), service `com.adlemich.eagleeye`, account `pairing-token`. Not found → `null`; other status → `SecretStoreUnavailableException`. Debug builds write one line per operation to stderr: `EagleEye keychain <read|write|delete>: OSStatus <n>` (never the value), so any further failure is visible in the console log of the next attempt. Sandbox, entitlements and `UseNativeHttpHandler` unchanged.

**DEV verification of the real store** (probe, sandbox on): write 0, read 0 (value matches), overwrite 0, read 0 (new value), delete 0, read -25300 (null), delete again -25300 (ok). No prompt; no item left.

### Attempt 3 (2026-10-20, 17:15, FL-2 build) — **paired**

Paired and connected; real data loads (see the screenshots above). Console trace: `EagleEye keychain write: OSStatus 0`. Keychain item: `login.keychain-db`, class `genp`, service `com.adlemich.eagleeye`, account `pairing-token` (no label, G-9). Database: `ServerConnections` row `192.168.1.37 | Michael's MacBook`; `Secrets` table empty (AC-24).

## 4. Keychain probe (step 1.4)

| Check | Result |
|---|---|
| Quit and start the same build (DEV, 17:19) | Silent: `EagleEye keychain read: OSStatus 0`, negotiate 200, no exception, connected without a code. |
| Rebuild with a new code signature (DEV, 17:19; temporary `-p:ApplicationVersion=2`, Info.plist changes, CDHash `bb335b0e…` → `888faa42…`), then start | The Keychain read blocks; `SecurityAgent` shows a Keychain prompt. Answered at 17:19:57 (button not yet known): read OSStatus 0, connected, **no re-pairing**. |
| Quit and start the rebuilt build again (DEV, 17:23) | **Prompt again**, although Michael had clicked „Immer erlauben" at 17:19. He answered „Immer erlauben" again. |
| Release build (Michael, ~17:30), first start | **One** prompt, „Immer erlauben" → green, Regeln and Berichte with his data. |
| Release build, restart (Michael; DEV again at 17:34) | **Silent**, connected. |
| Debug build, two restarts after that (DEV, 17:33) | **Prompt each time** (read blocked). |

Prompt text (German macOS, Michael's screenshot): title „EagleEye möchte deine vertraulichen Informationen verwenden, die in „com.adlemich.eagleeye" in deinem Schlüsselbund gesichert sind.", body „Um dies zu erlauben, gib das Passwort für den Schlüsselbund „Anmeldung" ein.", field „Passwort:", buttons „Immer erlauben" / „Nicht erlauben" / „Erlauben"; icon: the EagleEye icon on a lock.

**Analysis** (DEV, `security dump-keychain -a`, only this item's attributes and ACL, never the secret):

- The item's access list (`decrypt` entry) holds all three approved identities, each as `requirement: cdhash H"…"` with the build path: Release `0fcce897…`, Debug rebuild `888faa42…`, original Debug `bb335b0e…` (creator).
- The **partition list** (`partition_id` entry, checked in addition by macOS since 10.12) holds only `cdhash:0fcce897…` (the Release build, listed twice). Each „Immer erlauben" by a new ad-hoc identity **replaces** the partition list with that build's cdhash instead of adding it.
- Result: **only the most recently approved build starts silently**; any other build prompts, even if it was approved before. The Debug signature itself is fine (cdhash unchanged since 17:19:33, `codesign --verify` valid on disk and for the running process), so neither re-signing nor an invalid signature is the cause.
- Not fully explained: the Debug rebuild prompted again at 17:23 right after its own „Immer erlauben" at 17:19 (before any other build was approved). Most likely the first approval of a non-creator build updates the access list but not the partition list; the second one did. Not reproduced further, because each repeat needs Michael's password.
- Side note: the Release arm64 bundle that ran is only linker-signed (no sealed resources; `dotnet publish` fully signs the universal bundle only). The ACL check worked with it. Packaging (Step 7) must sign and verify the final bundle (`codesign --verify --deep --strict`).

**Conclusion for OQ-3 and AC-5**: for the parent (one installed build, an update replaces it): **one prompt after each update, answered once with „Immer erlauben", then silent**, and the pairing survives (no re-pairing): matches OQ-3. Expect possibly a second prompt at the next start after the first approval (the 17:23 observation); TES should check this in the update test (AC-5).

**Daily development**: every new Debug build, and every switch between two builds (Debug ↔ Release, or two checkouts), asks for the login password at the next start, because only the last-approved build is in the partition list. This is a nuisance, not a blocker. Options for ARC: accept it; or revisit Q-1 (a local self-signed signing identity gives a stable designated requirement across builds, so the partition entry would stay valid); or use a separate Keychain service name for Debug builds (keeps the dev item apart, but does not avoid the prompt).
| MAUI `SecureStorage` items from the dev builds | None (`SecureStorage` never stored anything). The removal script needs no entry for them (plan, Removal script table). |
| Data folder actually used | `~/Library/Containers/com.adlemich.eagleeye/Data/Documents/EagleEye/` (G-2). |

## 5. Release probe (step 1.5)

| Check | Result |
|---|---|
| `dotnet publish -c Release` | 0 warnings. Universal app + `.pkg` (G-18); the arm64 bundle is `src/EagleEye.ParentApp/bin/Release/net10.0-maccatalyst/maccatalyst-arm64/EagleEye.app` (AOT data, trimmed). |
| JSON reflection (TI-5) | `System.Text.Json.JsonSerializer.IsReflectionEnabledByDefault` = **true** in the published `runtimeconfig.bin` (MAUI default for Apple targets). Hub calls are therefore expected to work. |
| Release run with hub calls (Michael, ~17:30) | **Works**: one Keychain prompt, then green; Regeln and Berichte load his data; restart silent. `release-run.log` (console output) holds no exception or error, only system notices. TI-5 needs no change for now; re-check with the final packaged bundle in Step 7. |

## 6. Summary and recommendations for ARC

**Result**: the Mac app runs on the MacBook, pairs with the service PC `192.168.1.37` and shows Michael's real Settings, Rules and Reports, in Debug and Release. Michael sees green "Verbunden mit 192.168.1.37".

**First-light fixes made** (code on the branch):

| Fix | What | Plan step it belongs to |
|---|---|---|
| FL-1 | `MacKeychainSecretStore` registered instead of MAUI `SecureStorage`; `SecretStoreUnavailableException` (Core) + unit tests; `MauiSecureStorageSecretStore` deleted. | 2 (Decision 2), pulled forward |
| FL-2 | `MacKeychainSecretStore` on the legacy `SecKeychain…` API (P/Invoke), login keychain (K-1); Debug-only OSStatus trace on stderr. | 2, pulled forward, deviates from ADR-020 §2 (G-8) |

**Decisions needed before Step 2**:

1. **G-8, ADR-020 §2 revision.** The `SecItem` design cannot work on Mac Catalyst for an ad-hoc build (K-1 and K-2 both -34018; entitlement → no launch). Proposal: ADR-020 §2 states K-1 via the legacy file-keychain API (`SecKeychainAddGenericPassword`, `SecKeychainFindGenericPassword`, `SecKeychainItemModifyAttributesAndData`, `SecKeychainItemDelete`). **Risk**: the API has been deprecated since macOS 10.10; Apple could remove it in a future macOS. Then the store fails with `SecretStoreUnavailableException`, and the way out is a signing identity with a provisioning profile (needs an Apple Developer account, which CR-001 Q-7 excludes) or another secret mechanism. Mitigation: keep the store behind `ISecretStore` (already), check on each macOS major update (dev-environments §5), arc42 R-3. The Step 2 "spike" is largely done by this probe; what remains is the deny case and the removal-script deletion (`security delete-generic-password`).
2. **OQ-3 / AC-5 behaviour** (§4): one prompt per update, then silent, pairing kept. Note the partition-list rule for daily dev use and decide whether Q-1 should be revisited (§4, Daily development).
3. **G-5, silent pairing failure (Core)**: how `ConnectionCoordinator.CompletePairingAsync` handles a failing `SaveAsync` (abort the session, show `ErrorKeychainAccess` or a pairing error). It also affects Windows and leaves an orphan device on the service each time (attempts 1 and 2 most likely left four orphan "Michael's MacBook" devices; harmless).
4. **G-7, `UseNativeHttpHandler=false`**: not needed (the native handler runs the trust callback). Keep for robustness (ADR-020 §3) or drop.
5. **G-9 label**: setting the item label "EagleEye Parent App" with the legacy API needs an attribute list; or accept the service name as the visible name (plan Data Locations).
6. **Debug OSStatus trace** in `MacKeychainSecretStore` (Debug only, never the value): keep as a diagnosis aid or remove in Step 2.

**Gaps for Steps 2 to 6** (details in §2): Step 2: G-1, G-2, G-5, G-6, G-9, G-18 (and the FL items above). Step 3: G-3. Step 5: G-10 to G-16 (accent colours, button contrast, value fields stretching, editor borders, page width on Regeln/Berichte, picker, navigation contrast). Step 6: G-17 (tooltip check). No gap so far requires a Mac-only copy of a view or makes a page unusable on Catalyst.

## 7. Gap status (implementation, Steps 2 to 7)

| Gap | Status | Where |
|---|---|---|
| G-1 | Ignored as decided (R-8); README note in Step 9 | — |
| G-2 | **Done (Step 2)**: no sandbox (`Entitlements.plist` empty); data in `~/Library/Application Support/EagleEye/`, Mac Debug `EagleEye-Debug/` (`MauiAppDataPaths`) | Step 2 |
| G-4, G-8 | Done (FL-1, FL-2; ADR-020 Rev. 1). Debug Keychain service `com.adlemich.eagleeye.debug` (R-2) | Step 2 |
| G-5 | **Done (Step 2)**: `CompletePairingAsync` save-failure handling with revoke and `PairingSaveFailed` / `KeychainSaveFailed` (R-3) | Step 2 |
| G-6 | **Done (Step 2)**: `NSLocalNetworkUsageDescription` (Info.plist + `de.lproj`/`en.lproj` `InfoPlist.strings`); check by Michael pending | Step 2 |
| G-7 | **Done (Step 2)**: `UseNativeHttpHandler=false` kept (R-4) | Step 2 |
| G-9 | **Done (Step 2)**: label "EagleEye Parent App" set at creation (verified: attribute `0x00000007` = "EagleEye Parent App") | Step 2 |
| G-18 | **Done (Step 2)**: `RuntimeIdentifier=maccatalyst-arm64` | Step 2 |
| G-3 | **Done (Step 3)**: `ApplicationTitle` "EagleEye Parent App" for maccatalyst only (bundle `EagleEye Parent App.app`, `CFBundleName`/`CFBundleDisplayName`), window title "EagleEye" unchanged; copyright in Info.plist and `InfoPlist.strings`; close quits (flush + 500 ms + `Quit()`); check by Michael pending | Step 3 |
| (AC-12 to AC-15) | **Done (Step 4)**: `UiCulturePolicy` (Core) + `MacCultureSetup` (first preferred language → en/de; formats = UI language + macOS region); `CFBundleDevelopmentRegion` de, `CFBundleLocalizations` de/en; check by Michael pending | Step 4 |

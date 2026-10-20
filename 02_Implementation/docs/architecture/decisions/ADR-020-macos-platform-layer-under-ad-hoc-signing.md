# ADR-020: macOS Platform Layer under Ad-hoc Signing — Keychain, TLS Handler, No App Sandbox, Language

**Status**: Proposed (with the US-007 implementation plan, 2026-10-20). §2 is decided finally by the Keychain spike of US-007 Step 2, using the decision rule stated there.
**Date**: 2026-10-20
**Deciders**: ARC, Michael

---

## Context

US-007 makes the Mac Catalyst parent app a production platform with full parity to the Windows app (ADR-019, FR-APP-110 to FR-APP-112). The Mac target has compiled from `main` since US-001, but it has never run against the service. Four platform points are open (arc42 R-3):

1. **Code signature.** The `.app` is ad-hoc signed only (ADR-019 §3). An ad-hoc signature has no team ID, and its designated requirement is the hash of the code (cdhash). Every build is therefore a "different app" for macOS. Anything that macOS binds to the code signature can break on update: Keychain access rules, data-protection keychain access groups, and App Sandbox container ownership. Restricted entitlements (e.g. `keychain-access-groups`, `application-identifier`) need a provisioning profile, which needs an Apple Developer account. Michael has none (CR-001 Q-7).
2. **Secret storage.** The pairing token must be in the Keychain (NFR-S-014, US-007 AC-24). After an update the app must still read it. A one-time macOS prompt is acceptable; re-pairing after every update is not (US-007 OQ-3). The removal script must be able to delete the item (US-007 AC-6, OQ-2). Today the Mac target uses MAUI `SecureStorage` (`MauiSecureStorageSecretStore`). On Mac Catalyst it expects a `keychain-access-groups` entitlement, which an ad-hoc build cannot carry.
3. **TLS.** On Apple platforms the default `HttpClientHandler` of .NET is backed by the native `NSUrlSessionHandler`. The aborted iOS work found that this native handler does not call the certificate callback that `CertificateTrustPolicy` relies on (ADR-008 §3). The handler is also subject to App Transport Security. The WebSocket transport (`ClientWebSocket`) is managed on Apple platforms and calls its callback.
4. **Language.** The Windows app takes the Windows display language through .NET (`CurrentUICulture`), with German as the neutral resource language (NFR-L-011). On Mac Catalyst, .NET derives the cultures from `NSLocale`, and macOS offers a per-app language and localizes the standard menu bar only for bundles that declare their localizations.

The entitlements file of the Mac target currently turns the **App Sandbox** on (`com.apple.security.app-sandbox`) with outgoing network only.

---

## Decision

### 1. No App Sandbox

The Mac app runs **without** the App Sandbox. `Platforms/MacCatalyst/Entitlements.plist` holds no `com.apple.security.app-sandbox` key (and no restricted entitlement). The Mac app is not distributed through the Mac App Store (ADR-019), so the sandbox is not required.

- App data lives in `~/Library/Application Support/EagleEye/` (`EagleEye.ParentApp.db`), resolved through `NSFileManager` (Application Support, user domain) plus `EagleEye`, not through `Environment.SpecialFolder`, whose mapping on Mac Catalyst differs between .NET versions.
- Preferences, caches and other system-managed leftovers use the bundle ID `com.adlemich.eagleeye` (list in the US-007 plan, "Data locations").

### 2. Own Keychain secret store through the Security framework

`MauiSecureStorageSecretStore` is replaced by `Platforms/MacCatalyst/MacKeychainSecretStore` (`ISecretStore`), which calls the Security framework (`SecItemAdd`, `SecItemCopyMatching`, `SecItemUpdate`, `SecItemDelete`) directly:

- class *generic password*, service `com.adlemich.eagleeye`, account = the `ISecretStore` key (today only `pairing-token`), label "EagleEye Parent App";
- no access group, no synchronisation (`kSecAttrSynchronizable` false): the item never leaves the Mac;
- the token is never logged and never shown (NFR-S-014).

**Which keychain** (decided by the spike of US-007 Step 2, in this order):

| Variant | Keychain | Accepted if |
|---|---|---|
| **K-1 (preferred)** | The file-based **login keychain** (`kSecUseDataProtectionKeychain` = false). Default access rule: the creating app is trusted. | Store, read and delete work for the ad-hoc-signed app, and after an update (new cdhash) the app reads the token either silently or after **one** macOS prompt that the parent answers with "Always Allow" (OQ-3). |
| K-2 | The **data-protection keychain**, `kSecAttrAccessibleAfterFirstUnlockThisDeviceOnly` (the pattern of the aborted iOS work). | Only if K-1 is not usable from Mac Catalyst, **and** K-2 works without a restricted entitlement and survives an update. Then the removal script cannot delete the item with the `security` tool, and the plan adds an app-side purge (US-007 plan Q-3). |
| None | — | If neither variant survives an update without re-pairing, the question goes back to Michael (story OQ-3) before more work is done; the story's split fallback applies. |

K-1 is preferred because the login keychain is local by nature (it never syncs to iCloud, so "this device only" holds without an attribute), the parent sees the item in *Keychain Access → login*, and the removal script can delete it with `security delete-generic-password`.

**Access denied.** When macOS refuses access (the parent clicks "Deny" in the prompt, or the item is not readable), the store throws `SecretStoreUnavailableException` (new, `ParentApp.Core/Abstractions`). It never returns "no secret" in that case, so the app does **not** treat itself as unpaired and does not overwrite the pairing. The app shows a clear startup message (new text `ErrorKeychainAccess`). "Item not found" still returns `null`.

### 3. Managed HTTP handler on the Mac

The Mac Catalyst target sets the MSBuild property `UseNativeHttpHandler=false` (maccatalyst TFM only, in `EagleEye.ParentApp.csproj`). `HttpClientHandler` is then backed by the managed `SocketsHttpHandler`, so the trust callback of `ParentHubClient` runs on the negotiate and long-polling requests exactly as on Windows, and App Transport Security does not apply. `ParentHubClient` and `CertificateTrustPolicy` stay unchanged. The WebSocket callback is already managed. No certificate is ever added to a Keychain (CN-013).

### 4. Language and formats from macOS

- The bundle declares its localizations: `CFBundleDevelopmentRegion` = `de`, `CFBundleLocalizations` = `de`, `en`, plus `de.lproj` and `en.lproj` folders with `InfoPlist.strings` (local-network usage text). macOS then offers the per-app language (*Language & Region → Applications*) and localizes the standard menu bar.
- **UI language**: at start, before MAUI builds the UI, the Mac app reads the **first** preferred language of the process (`NSLocale.PreferredLanguages[0]`, which reflects a per-app choice). English → `en`; German or any other language → `de` (NFR-L-011, US-007 AC-13). It sets `CurrentUICulture` and `DefaultThreadCurrentUICulture`. The rule is a pure function in Core (`UiCulturePolicy`).
- **Formats**: `CurrentCulture` = UI language + macOS region (e.g. `de-DE`, `en-US`, `en-DE`), from `NSLocale.CurrentLocale` region code, with .NET/ICU patterns. Custom date formats set in macOS are not followed (US-007 plan D-4).
- The Windows app keeps its current behaviour (.NET defaults); the policy is called only under `#if MACCATALYST`.

### 5. Accent colour from macOS

`Platforms/MacCatalyst/MacAccentColor` reads the macOS accent colour (AppKit `NSColor.controlAccentColor` through the Objective-C runtime, converted to sRGB; fallback: the global default `AppleAccentColor` mapped to Apple's fixed palette in Core, `AppleAccentPalette`). `HeadingBarColorService` uses it like `WindowsAccentColor`; the existing refresh on window activation covers changes (US-007 AC-10).

### 6. Window, menu and lifecycle

- One window, `UIApplicationSupportsMultipleScenes` = false (already set). macOS LaunchServices keeps a single instance of the bundle.
- Closing the window quits the app (US-007 OQ-1): the MAUI window's `Destroying` event calls `Application.Quit()` (Mac Catalyst only).
- The standard menu bar of Mac Catalyst (app menu with About/Hide/Quit, Edit with Undo/Cut/Copy/Paste/Select All) is used as is. The app adds **no** `MenuBarItems`, because MAUI would show them on Windows too (AC-35). About panel: name, version (`CFBundleShortVersionString` = product version) and `NSHumanReadableCopyright`.
- **No lifecycle coordinator** by default: the Mac app is not suspended in the background, and the existing reconnect schedule (0 s, 2 s, 10 s, then every 30 s, ADR-008) meets the 60 s of US-007 AC-22. A small reconnect nudge on wake or network return is added only if the US-007 test of AC-22 fails (plan Step 6, contingent).

---

## Rationale

- An ad-hoc signature changes with every build. Every mechanism that macOS binds to the signature is a risk for "pairing and settings survive an update" (AC-5). The sandbox container (ownership checks bound to the code signature) and the data-protection keychain (access groups bound to the team ID) depend on it more than plain files and the login keychain do.
- The login keychain's access rule is also bound to the signature, but its failure mode is the prompt that Michael accepted (OQ-3).
- Calling the Security framework directly makes the keychain variant, the item attributes and the "denied" case explicit, instead of depending on MAUI's internal keychain settings and their entitlement.
- `UseNativeHttpHandler=false` is the smallest change that makes the existing, tested trust code run unchanged on the Mac.

### Alternatives Considered

| Option | Reason Rejected |
|---|---|
| Keep MAUI `SecureStorage` | Needs `keychain-access-groups` on Mac Catalyst, which an ad-hoc build cannot carry; hides the keychain variant and the denied case. |
| Keep the App Sandbox | Container ownership is bound to the code signature (risk for AC-5); data path inside the container; no benefit without the App Store. |
| Token in SQLite, encrypted with a key in a file | Violates NFR-S-014 and AC-24 (Keychain). |
| Keychain item readable by every app (empty trusted-app list) | Avoids the update prompt, but any process of the user could read the token. |
| Stable local self-signed code-signing identity | Would avoid the update prompt (stable designated requirement). Adds a private key and a setup step on the MacBook. Not needed after OQ-3; offered as plan Q-1. |
| Replace the handler in `ParentHubClient` (always `SocketsHttpHandler`) | Changes the Windows code path too (AC-35); the build property only affects the Mac. |
| Lifecycle coordinator as on iOS | iOS suspended the app in the background; macOS does not. Added only if AC-22 fails. |

---

## Consequences

### Positive

- The trust, pairing and coordinator code of `ParentApp.Core` runs unchanged on the Mac.
- The removal script can delete everything with standard tools (`rm`, `defaults delete`, `security delete-generic-password`).
- TES can check the Keychain entry in *Keychain Access* and the data folder in Finder without reading code (AC-24).

### Negative / Trade-offs

- After each update, macOS may ask once for the login password to let the new build use the Keychain item ("Always Allow"). Several builds during a test run mean several prompts.
- Without the sandbox, the Mac app runs with the parent user's full file access. Acceptable for a self-built app on Michael's own Mac.
- Custom date formats of macOS are not followed (.NET/ICU patterns for the culture).

### Neutral

- ADR-019 §4 ("macOS uses `MauiSecureStorageSecretStore`") is amended: macOS uses `MacKeychainSecretStore`.
- arc42 §5.5, §7.1.1, §7.4, §8.1, §8.13 and coding guidelines §13 are amended with the US-007 plan (proposed).

---

## References

- `02_Implementation/docs/requirements/user-stories/US-007/user-story.md` (AC-5, AC-6, AC-10, AC-13, AC-18, AC-19, AC-22, AC-24; OQ-1 to OQ-3)
- `02_Implementation/docs/requirements/user-stories/US-007/implementation-plan.md`
- ADR-008 (TLS trust and pairing), ADR-009 (ParentApp.Core), ADR-019 (platforms, unsigned Mac distribution)
- `02_Implementation/docs/dev-process/dev-environments.md` §5 (MacBook lessons)

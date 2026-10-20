# ADR-019: Parent App Platforms — Windows and macOS Only, Unsigned Mac Distribution

**Status**: Accepted (Michael, 2026-10-20; records CR-001)
**Date**: 2026-10-20
**Deciders**: Michael (project owner), ARC

> **Amendment (US-007, ADR-020) — proposed with the US-007 implementation plan, 2026-10-20**: §4 "macOS uses `MauiSecureStorageSecretStore` (Keychain)" is replaced by `MacKeychainSecretStore` (Security framework, login keychain preferred), because MAUI `SecureStorage` needs a `keychain-access-groups` entitlement that an ad-hoc-signed build cannot carry. ADR-020 also turns the App Sandbox off and sets the managed HTTP handler for the Mac.

> **Numbering note**: ADR-017 and ADR-018 were drafted on the aborted branch `feature/US-007-ios-parent-app`, which was deleted unmerged. They never reached `main`. Like FR-APP-100 to FR-APP-102 (CR-001 §3), the two numbers stay unused to avoid confusion with those drafts.

---

## Context

Until now the parent app had four targets (ADR-001, ADR-007, requirements §3.3 up to v1.6):

- **Android and iOS** as the primary production platforms,
- **Windows** (WinUI, per-user installer, ADR-009) as the initial testing vehicle for parent-side features,
- **macOS** (Mac Catalyst, `.dmg`) as a desktop app.

On 2026-10-20 Michael aborted US-007 (iOS parent app) and approved CR-001 (`02_Implementation/docs/requirements/change-requests/CR-001-remove-mobile-platforms.md`) and requirements v1.7:

- Running a self-built iOS app on one's own iPhone for longer than 7 days needs a paid Apple Developer account. Michael will not pay for that.
- Android is dropped together with iOS, so the product does not keep a single mobile platform on its own.
- Without a paid Apple Developer account, the Mac `.dmg` cannot be Developer-ID signed or notarised. Michael accepts this (CR-001 Q-7): he does not distribute the app and allows it once in the macOS settings on his MacBook.
- The Mac app must reach full parity with the Windows parent app. A new US-007 "macOS parent app parity" covers this after CR-001 is merged (CR-001 Q-6).

---

## Decision

### 1. Two production platforms

The parent app (`EagleEye.ParentApp` + `EagleEye.ParentApp.Core`, ADR-009) has exactly two targets, both production platforms with the same feature set (FR-APP-082, FR-APP-110):

| Platform | TFM | Built on (ADR-007) | Distribution |
|---|---|---|---|
| **Windows 11** | `net10.0-windows10.0.19041.0` (WinUI) | Windows Developer Machine | Own per-user Inno Setup installer, unpackaged, self-contained (ADR-009) |
| **macOS 26+** | `net10.0-maccatalyst` (Mac Catalyst) | MacBook | `.dmg` with the `.app` bundle, unsigned and un-notarised (§3) |

**iOS and Android are removed** from the product: no TFM, no `Platforms/iOS` or `Platforms/Android` folder, no workload, SDK, emulator, APK or sideloading in the build and test setup. The removal from code and scripts is a DEV follow-up (CR-001 §6.2).

### 2. Desktop UI only

Both platforms use the desktop layout (navigation menu, content region, status bar; resizable window with a minimum size). There is no phone layout and no portrait lock (FR-APP-080 removed in v1.7). Platform code stays minimal; everything that does not need MAUI lives in `EagleEye.ParentApp.Core` (ADR-009).

### 3. Mac distribution without a paid Apple Developer account

- The `.dmg` is built by `scripts/package-macos.ps1` on the MacBook.
- The `.app` bundle is **ad-hoc signed** only (the default of the .NET Mac Catalyst build). No Developer-ID signature, no notarisation, no Mac App Store (FR-APP-112, requirements §9.4).
- On the first start, Gatekeeper blocks the app. The user allows it once: *System Settings → Privacy & Security → Open Anyway*. This must be part of the user-facing install instructions.
- No secret or Apple account data is needed for the Mac build.

### 4. Same connection model on both platforms

Both apps are ordinary `ParentHub` clients with TLS trust on first use, a pinned thumbprint and pairing (ADR-004, ADR-008, FR-APP-091, FR-APP-111). Secrets: Windows uses `DpapiSecretProtector` (ADR-009); macOS uses `MauiSecureStorageSecretStore` (Keychain).

---

## Rationale

- iOS sideloading without a paid account expires every 7 days, which is not usable for a parental control app.
- Android alone would keep a mobile toolchain (SDK, emulator, separate layout, TLS configuration) for one platform, with no user for it.
- Windows and macOS share the desktop layout, so parity costs little extra UI work.
- An unsigned `.dmg` is enough for one user on his own Mac. The Gatekeeper step happens once.

### Alternatives Considered

| Option | Reason Rejected |
|---|---|
| Keep iOS with a paid Apple Developer account | Michael's decision: no 99 EUR/year for running his own app on his own phone |
| Keep iOS with the free profile (re-sign every 7 days) | Not usable for a product that must run continuously |
| Keep Android only | Michael's decision: no single mobile platform on its own |
| Developer-ID sign and notarise the `.dmg` | Needs the paid account; no distribution planned (Q-7) |
| Distribute the Mac app via the Mac App Store | Needs the paid account and sandboxing; out of scope (requirements §10) |

---

## Consequences

### Positive

- One toolchain fewer on each machine: the Windows machine no longer needs the Android SDK, emulator or `maui-android` workload; the MacBook needs Xcode only for Mac Catalyst.
- `-r`/`--runtime` on the command line no longer hits an Android target (ADR-009 D-4 remark); DEV may re-check `package-windows.ps1`.
- One UI style (desktop) for all parent-app views.

### Negative / Trade-offs

- Parents cannot manage rules from a phone. They need a Windows PC or a Mac on the LAN.
- The Mac app lags behind: US-002 to US-006 exist only in the Windows parent app until the new US-007 is done.
- Gatekeeper warns on the first start of the Mac app. Unsuitable for distribution to other people without a paid account.

### Neutral

- Closed story artifacts (US-001 to US-006) keep their mentions of Android, iOS and phones as history.
- Risk R-1 (self-signed certificate on iOS/Android) is void; the Mac Catalyst TLS trust is covered by the new US-007.

---

## Affected Documents (amended with this ADR)

| Document | Change |
|---|---|
| ADR-001 | Amended: MAUI targets are Windows + macOS |
| ADR-002 | Amended: no iOS/Android storage rows |
| ADR-004 | Amended (cosmetic): example device name |
| ADR-007 | Amended: machine roles without Android/iOS; Windows parent app is a production platform |
| ADR-008 | Amended: "mobile and macOS apps" → macOS app |
| ADR-009 | Amended: TFM list without `net10.0-android`; Core reused by the macOS app |
| ADR-010 | Amended: note on "relevant later for mobile apps" |
| arc42 `system-architecture.md` | §1.1, §1.2, §2.1, §2.2, §3, §4.2, §5.5, §7, §8.2, §8.4, §8.10, §9, §10.2, §11 |
| `product-coding-guidelines.md` | §10.1, §10.4, §13, §14 and §15 removed, §16.1 |
| `dev-process/dev-environments.md`, `dev-process/dev-process.md` | Machine roles, toolchains, scripts, components |

---

## References

- `02_Implementation/docs/requirements/change-requests/CR-001-remove-mobile-platforms.md`
- `02_Implementation/docs/requirements/general-product-requirements.md` v1.7: §3.3, FR-APP-110 to FR-APP-112, §9.4, §9.5, §10, §11.1 (Q-6, Q-7)
- ADR-007: Two-Machine Development and Manual Acceptance Testing
- ADR-009: Windows Parent App Packaging and the ParentApp.Core Library

# CR-001: Remove the iOS and Android Parent Apps

**Status**: Approved by Michael, 2026-10-20
**Type**: Scope change (not a user story)
**Decided by**: Michael, 2026-10-20 (final)
**Written by**: PRO, 2026-10-20
**Branch**: `change/CR-001-remove-mobile-platforms` (merged into `main` by the Orchestrator after Michael's approval)

---

## 1. Decision

The EagleEye parent app ships **only** as:

1. the **Windows parent app**, with its own per-user installer (FR-APP-092, ADR-009);
2. the **Mac app** (Mac Catalyst, direct `.dmg`).

The **iPhone/iOS app and the Android app are removed from the product entirely**. Windows and Mac are now the **production** parent-app platforms. The Windows parent app is no longer only an "initial testing vehicle".

## 2. Rationale

Running a self-built iOS app on one's own iPhone for longer than 7 days needs a paid Apple Developer account (99 EUR/year). Michael will not pay that just to run his own app on his own phone. Android is dropped together with iOS, so the product does not keep a single mobile platform on its own.

## 3. Background and Consequences

- **US-007 (iOS parent app)** was aborted during implementation (step c). Its branch `feature/US-007-ios-parent-app` was deleted unmerged. None of its changes exist on `main`: the v1.7 requirements draft, FR-APP-100 to FR-APP-102, ADR-017/018, the phone views and spikes are gone.
- **US-008 (Rules on the phone)** is void.
- The requirements version number **v1.7** is used again by this CR. The US-007 draft v1.7 never reached `main`. To avoid confusion, **FR-APP-100 to FR-APP-102 stay unused**; the new macOS requirements are FR-APP-110 to FR-APP-112.
- US-001 to US-006 were built and tested in the **Windows parent app only** (their stories name "ParentApp Windows"). The Mac app has none of these features on `main` yet. Answered by Q-6 (§7): a new US-007 "macOS parent app parity" closes the gap.

## 4. Scope

| In scope (this CR, PRO) | Out of scope (follow-ups, §6) |
|---|---|
| General product requirements | Architecture docs: arc42, ADRs, coding guidelines (ARC) |
| Project intent, Q&A, technology selection, structure plan (dated amendments) | `dev-process/dev-environments.md`, `dev-process/dev-process.md`, `testing/README.md`, test-plan template |
| Repo-root `README.md` | Source code, project files, scripts, `.gitignore`, secrets template, `03_Delivery/` (DEV) |
| User story template (`US-000-template.md`) | Root `CLAUDE.md`, agent `CLAUDE.md` files, Orchestrator memory (Orchestrator) |
| | Closed story artifacts of US-001 to US-006 (history, unchanged) |

No roadmap, backlog or story-list document exists in the repo. The only planning references to US-007/US-008 are in the Orchestrator memory (Orchestrator follow-up).

## 5. What PRO Changed

### 5.1 `02_Implementation/docs/requirements/general-product-requirements.md` → Draft v1.7

| Item | Change |
|---|---|
| Header, v1.7 amendment | Status Draft v1.7; change-log entry citing Michael's decision of 2026-10-20 |
| §1.1 Vision | Parents manage rules from the parent app on a Windows PC or a Mac (was: mobile phone or Mac) |
| G-6 | Windows and macOS parent apps (was: Android and iOS primary, Windows, macOS) |
| §2.1 Parent persona | Devices: Windows 11 PC or Mac; Constraints: manages rules from a Windows PC or a Mac on the LAN (was: Android/iPhone primary, mobile device) |
| FR-SVC-094 | Example device name "Mom's MacBook" (was "Mom's iPhone") |
| §3.3 | Title, intro and platform-roles table: Windows and macOS = production; Android, iOS = removed |
| FR-APP-080 | **Removed (v1.7, 2026-10-20)** (portrait-only on iOS/Android) |
| FR-APP-082 | Functionally identical on Windows and macOS |
| FR-APP-090, FR-APP-091 | Reference the macOS parent app instead of "the other / mobile apps" |
| §3.3.10 (new) | FR-APP-110 (Mac parity, desktop UI), FR-APP-111 (Mac connects with the same TLS + pairing model), FR-APP-112 (`.dmg`, no Mac App Store) |
| FR-CR-011 | Single codebase across Windows and macOS |
| §4.2 step 8 | Install on a Windows PC (installer) or a Mac (`.dmg`) |
| NFR-L-010 | Parent apps: Windows, macOS |
| §9.2 iOS, §9.3 Android | **Removed (v1.7, 2026-10-20)**; section numbers kept, old content summarised |
| §9.4 macOS | Purpose = production; deployment on a Mac on the LAN; unsigned, un-notarised `.dmg` accepted (Q-7) |
| §9.5 Windows | Purpose = production (was: initial testing vehicle) |
| §10 Out of scope | "iOS and Android parent apps" replaces "App Store / Play Store distribution"; Microsoft Store made explicit |
| §11 | Q-1, Q-3 marked superseded; Q-5 (answered, Michael 2026-10-20); Q-6, Q-7 added and answered by Michael 2026-10-20 |

All other requirement IDs are unchanged. No ID was renumbered.

### 5.2 Other documents

| File | Change |
|---|---|
| `01_Intend_and_Constraints/main_intend.md` | Dated amendment note; module 2 (mobile apps) struck through and marked dropped; "mobile" struck in module 1, module 3 and workflow steps 6 to 8 |
| `01_Intend_and_Constraints/questions_and_answers.md` | Q5.2 and Q9.7 marked superseded, Q9.2 and Q9.6 amended (dated notes, original answers kept); new Group 11 (Q11.1, Q11.2) |
| `01_Intend_and_Constraints/technology_selection.md` | Dated amendment: MAUI for Windows and macOS only; later iOS/Android plan cancelled |
| `01_Intend_and_Constraints/project_structure_plan.md` | Dated amendment; `Platforms/iOS/`, `Platforms/Android/` marked dropped, `Platforms/Windows/` added; secrets template notes |
| `README.md` | Windows + Mac parent apps only; no Android target, workload, SDK or `.apk`; transition hint `build.ps1 -SkipAndroid` until DEV removes the target |
| `02_Implementation/docs/requirements/user-stories/US-000-template.md` | "ParentApp Android" removed from the platform choices |

## 6. Follow-ups by Role

Found with `git grep -i -E "android|ios|iphone|ipad|mobile|phone|adb|xcode|sideload"`. Hits that are only `IPAddress`/`RemoteIpAddress` in code are false positives and not listed.

### 6.1 ARC (after Michael approves this CR)

**Status (ARC, 2026-10-20)**: ARC rows done on this branch in `6f23416` (arc42, ADRs, new ADR-019) and `ebba2b1` (coding guidelines, dev-process docs). The new ADR-019 and the arc42/guidelines amendments were approved by Michael on 2026-10-20 (ADR-019 Accepted). The two testing rows stay with TES. **TES (2026-10-20)**: testing rows done in `ae4ecb2`.

| File | What must change |
|---|---|
| `02_Implementation/docs/architecture/arc42/system-architecture.md` | §1 component table and goals ("mobile or macOS app", "pair their phone"); constraints (MAUI targets, machine roles, "Android and iOS primary"); context/deployment diagrams (Parent Device node, "Android 14+ / sideloaded via adb / Xcode"); building-block diagram `component [Android]`; storage tables (Android rows); distribution table (iOS/Android rows); QS-07 ("pairs their phone"); risk R-1 (iOS/Android TLS); secret-storage notes **Done (6f23416).** |
| `02_Implementation/docs/architecture/decisions/ADR-001-technology-selection.md` | MAUI rationale names iOS, Android, macOS: add a dated note (or superseding ADR) that targets are Windows + macOS **Done (6f23416).** |
| `02_Implementation/docs/architecture/decisions/ADR-002-sqlite-yaml-logging.md` | Platform list and Android storage row **Done (6f23416).** |
| `02_Implementation/docs/architecture/decisions/ADR-004-pairing-based-authentication.md` | Example "Mom's iPhone" (cosmetic, optional) **Done (6f23416).** |
| `02_Implementation/docs/architecture/decisions/ADR-007-dual-machine-development-and-manual-testing.md` | Machine roles: no Android on the Windows machine, no iOS on the MacBook; "Android and iOS primary" role statement; Windows client no longer only the testing vehicle **Done (6f23416).** |
| `02_Implementation/docs/architecture/decisions/ADR-008-parent-connectivity-tls-and-pairing-protocol.md` | "ready for the mobile and macOS apps" **Done (6f23416).** |
| `02_Implementation/docs/architecture/decisions/ADR-009-windows-parent-app-packaging-and-parentapp-core.md` | Multi-target list includes `net10.0-android`; "later Android, iOS and macOS work" **Done (6f23416).** |
| `02_Implementation/docs/architecture/decisions/ADR-010-event-driven-state-propagation.md` | "relevant later for mobile apps" in the alternatives: add a dated note or leave as history **Done (6f23416).** |
| New ADR (or amendment) | Record the platform change and the accepted unsigned, un-notarised Mac `.dmg` (Q-7) **Done (6f23416).** `ADR-019-parent-app-platforms-windows-and-macos.md`. |
| `02_Implementation/docs/architecture/product-coding-guidelines.md` | Remove §14 iOS and §15 Android (and TOC entries), `Platforms/Android` in the folder tree and build-host rules; Windows section "Role: initial testing vehicle … Android and iOS are the production platforms"; "phone layouts" wording in the macOS section **Done (ebba2b1).** |
| `02_Implementation/docs/dev-process/dev-environments.md` | Windows machine: no Android target, APK, emulator, `maui-android` workload or Android SDK; MacBook: Xcode for Mac Catalyst only; `build.ps1` table; csproj TFM description **Done (ebba2b1).** |
| `02_Implementation/docs/dev-process/dev-process.md` | §5 component table "Windows, Android, macOS (iOS later)" → Windows, macOS; machine list "Windows + Android targets" **Done (ebba2b1).** |
| `02_Implementation/docs/testing/README.md` (with TES) | Test-station table: remove "Android parent app (emulator or USB device)" **Done (ae4ecb2).** |
| `02_Implementation/docs/testing/templates/test-plan-template.md` (TES) | Example prerequisite "Android emulator running" **Done (ae4ecb2).** (now: macOS parent app from the `.dmg`) |

### 6.2 DEV (after the ARC updates, on its own branch or story as the Orchestrator decides)

**Status (DEV, 2026-10-20)**: DEV rows done on this branch in `0749f74`, verified on the MacBook (`build.ps1` green with 0 warnings, `test.ps1` 1197/1197 green, `env-check.ps1` runs). The Windows build, tests and installer packaging still have to be verified on the Windows machine. The `-SkipAndroid` hints in `README.md` and `02_Implementation/docs/dev-process/dev-environments.md` are removed.

| File | What must change |
|---|---|
| `02_Implementation/src/EagleEye.ParentApp/EagleEye.ParentApp.csproj` | Remove the `net10.0-android` TFM (Windows host keeps only `net10.0-windows10.0.19041.0`), Android `SupportedOSPlatformVersion` and comments **Done (0749f74).** |
| `02_Implementation/src/EagleEye.ParentApp/Platforms/Android/` | Delete (`MainActivity.cs`, `MainApplication.cs`, `AndroidManifest.xml`, `AndroidAccentColor.cs`, resources) **Done (0749f74).** |
| `02_Implementation/src/EagleEye.ParentApp/Platforms/iOS/` | Delete (holds only `.gitkeep`; no iOS TFM on `main`) **Done (0749f74).** |
| `02_Implementation/src/EagleEye.ParentApp/Services/HeadingBarColorService.cs` | Remove the `#elif ANDROID` branch and Android doc comment **Done (0749f74).** |
| `02_Implementation/src/EagleEye.ParentApp/Services/MauiSecureStorageSecretStore.cs` | Doc comment: "Android, iOS and macOS" → macOS (Keychain) **Done (0749f74).** |
| `02_Implementation/src/EagleEye.ParentApp.Core/Appearance/HeadingBarPalette.cs` | Doc comment mentions Android `colorPrimary` **Done (0749f74).** |
| `02_Implementation/src/EagleEye.ParentApp/Platforms/MacCatalyst/Info.plist` | `UISupportedInterfaceOrientations~ipad` key is an iPad leftover from the template; remove if not needed by Mac Catalyst **Done (0749f74).** Removed. |
| `02_Implementation/src/EagleEye.ParentApp/README.md` | Platform table and build hosts: drop Android and iOS rows, sideloading, Android SDK hints and `Platforms/Android` in the tree **Done (0749f74).** |
| `02_Implementation/scripts/build.ps1` | Remove the Android build step, `-SkipAndroid` switch and `Get-AndroidToolchainArgs` **Done (0749f74).** |
| `02_Implementation/scripts/env-check.ps1` | Windows "Responsible for" text; drop the `adb` check and `android`/`ios` workload patterns; keep Xcode for Mac Catalyst **Done (0749f74).** |
| `02_Implementation/scripts/package-windows.ps1` | Comment about the Android target on a multi-targeted project; re-check whether `--runtime` can now be passed **Done (0749f74).** `--runtime win-x64` now passed. |
| `02_Implementation/scripts/package-macos.ps1` | Check only (Xcode requirement stays for Mac Catalyst); no Developer-ID signing or notarisation (Q-7) **Done (0749f74).** Stub aligned: ad-hoc only, no secrets file, no Apple credentials. |
| `02_Implementation/.vscode/launch.json` | Launch config name "Windows / Android on Windows" **Done (0749f74).** |
| `.gitignore` | "MAUI / Mobile" section, `Platforms/Android/GoogleServicesJson/`, `03_Delivery/android/*.apk` **Done (0749f74).** |
| `secrets/secrets.template.json` | Remove the `android` section; remove or neutralise the `apple` section (no iOS; Mac needs no Developer account, Q-7). Never put values in the template **Done (0749f74).** Both sections removed. |
| `03_Delivery/android/` | Delete the folder and its README **Done (0749f74).** |
| Unit tests | Re-run the full suite after removing the Android target; no test references Android today **Done (0749f74).** MacBook: 1197/1197 green (Shared 282, ParentApp 915); Windows run pending. |

Also on the **MacBook** (from the Orchestrator memory): the Mac Catalyst build may need `ValidateXcodeVersion=false` again or a matching Xcode, since that fix was on the deleted US-007 branch. **Done (0749f74)**: the build failed with "requires Xcode 27.0, current 27.1"; `ValidateXcodeVersion=false` is set in `EagleEye.ParentApp.csproj`, scoped to `maccatalyst`, with a comment; decision recorded in `02_Implementation/docs/dev-process/dev-environments.md` §5.

### 6.3 Orchestrator

| File | What must change |
|---|---|
| `CLAUDE.md` (root) | **Done (916abfb).** Project-context table: ParentApp = Windows parent app + Mac app (production), no Android/iOS; "Built on" column; "Where You Run" table: Windows machine without Android target, MacBook without iOS target |
| `02_Implementation/agents/arc/CLAUDE.md` | **Done (916abfb).** Technology constraints: MAUI targets and machine roles (lines on Android + iOS production, Windows + Android targets) |
| `02_Implementation/agents/dev/CLAUDE.md` | **Done (916abfb).** Machine table (Android targets and packaging) and MAUI row (Android, iOS) |
| `02_Implementation/agents/pro/CLAUDE.md` | **Done (916abfb).** Phase 1 item 9 platform list (iOS 26, Android 14); "Platform explicit" rule names "Android" |
| `02_Implementation/agents/tes/CLAUDE.md` | No hit; check only |
| `02_Implementation/agents/orchestrator/memory/` | After approval: record CR-001 approved and merged, requirements v1.7, FR-APP-100..102 unused; update `michael-project-owner.md` ("Windows/Android work"); keep the Mac parity decision (Q-6) and signing decision (Q-7) |
| Process | Decide whether the DEV follow-up is its own change branch (e.g. `change/CR-001-dev-remove-android`) or part of the next story; start the new US-007 "macOS parent app parity" after the merge (Q-6) |

### 6.4 No change (history)

Closed story artifacts mention Android, iOS or phones and stay as they are: `02_Implementation/docs/requirements/user-stories/US-002/` to `US-006/` (user stories, implementation plans and reports, ISSUE-006, ISSUE-007) and `02_Implementation/docs/testing/US-002/` to `US-006/` (test plans, ISSUE-008).

## 7. Questions Answered by Michael (2026-10-20)

| ID | Question | Answer (Michael, 2026-10-20) |
|---|---|---|
| Q-6 | Is the Mac app now a full-parity production platform, and when does it catch up with the features of US-002 to US-006, which exist only in the Windows parent app? | Yes, full parity with the Windows app. A **new US-007 "macOS parent app parity"** covers it, started after CR-001 is merged (the number is reused; the aborted iOS US-007 never reached `main`). After that story, every parent-app story names both apps. |
| Q-7 | Without a paid Apple Developer account the `.dmg` cannot be Developer-ID signed or notarised, so macOS asks for confirmation at the first start (Gatekeeper). Acceptable? | Accepted. An unsigned, un-notarised `.dmg` is fine. Michael does not plan to distribute it; a one-time allow in the macOS settings on his MacBook is enough. |

Both answers are also recorded in `02_Implementation/docs/requirements/general-product-requirements.md` §11.1.

## 8. Approval

| Step | Who | Status |
|---|---|---|
| CR written, requirements v1.7 drafted | PRO | Done 2026-10-20 |
| Approve CR-001 and requirements v1.7, answer Q-6 and Q-7 | Michael | Approved 2026-10-20 |
| Merge `change/CR-001-remove-mobile-platforms` into `main` (`--no-ff`) | Orchestrator | After approval |
| Follow-ups §6 | ARC, DEV, Orchestrator | Orchestrator CLAUDE.md files done (916abfb); ARC docs done (6f23416, ebba2b1), ADR-019 Accepted 2026-10-20; TES done (ae4ecb2); DEV done (0749f74). Merge approved by Michael 2026-10-20 **before** the Windows check; Windows build/test/packaging (incl. `--runtime win-x64` in `package-windows.ps1`) is still open and is done by Michael on `main` |

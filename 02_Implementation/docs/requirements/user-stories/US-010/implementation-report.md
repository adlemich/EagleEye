# Implementation Report: US-010 — Remaining-Time Overlay Replaces the Warning Dialogs (0.10.0)

**Status**: Windows Steps 1 to 4 and 6 done (2026-11-01). **Step 5 and Step 9 passed** by the project owner's pilot check on 2026-11-07 (§9.1); the Windows part is approved as is. **Step 7 (MacBook) done** on 2026-11-07 (§11): build and tests green, Settings look check passed, `.dmg` 0.10.0 built (not installed). No code change on the Mac, so **Step 8 is not needed**. Story → `Implemented`. **Step c approved and story closed by the project owner on 2026-11-07** (TES steps d to f skipped; gaps in `02_Implementation/docs/testing/US-010/test-report.md`).
**Author**: DEV
**Branch**: `feature/US-010-time-overlay`
**Plan**: `02_Implementation/docs/requirements/user-stories/US-010/implementation-plan.md` (approved 2026-11-01)

---

## 1. Summary

- **Version 0.10.0** for all four assets (`02_Implementation/Directory.Build.props`).
- **Contract** (`EagleEye.Shared`): new fire-and-forget push `ITrayClientCallback.OnRemainingTime(RemainingTimeDto)` with `OverlayLineDto`, `OverlayLineKind`, `OverlayLevel`, `OverlaySignal` and `OverlayRules` (`MaxThresholdMinutes` 60, `LevelFor`). Removed: `WarningSettingsDto.EarlyText/FinalText`, `IParentHub.SetWarningText`, `DefaultTextKind.EarlyWarningText/FinalWarningText` and their default texts, the six warning/grace `KidMessageReason` values, `KidMessageDto.Minutes`.
- **Service**: the time-limit engine keeps its warning ledger, grace periods and closes unchanged, but instead of dialogs it builds a remaining-time model per session of a controlled account and pushes it every tick (5 s) through the new `OverlayPublisher` to the newest verified tray connection: own-time lines per app/group (`OverlayLineBuilder`, no breaks), the break line, expiring 0:00 lines while apps are being closed, and the strongest new level of the tick as the signal. Warning and grace log entries end with "shown in overlay" or "not shown (tray client not connected)". `MessageOutbox` and the warning/grace parts of `KidMessageFactory` are gone. **Migration 7** drops `WarningSettings.EarlyText/FinalText`.
- **Tray client**: new `Overlay/` folder (window, renderer, layout, countdown clock, pulse/sound, settings in `HKCU\Software\EagleEye\TrayClient`, foreground hook, controller); new tray menu (Opacity, Position, Show from, Warning sound, About) built from tool-window popups, opened with the right **and** left button; the process is PerMonitorV2. The kid dialog stays for blocked starts and enforced closes only.
- **Parent apps** (shared Core + XAML): the "Display text" column and its editors are gone; new explanation text (AC-31) in German and English.
- **Installers 0.10.0** built (Step 6), not installed anywhere (TI-14).

## 2. Deviations from the Implementation Plan

| # | Plan | Done instead | Why |
|---|---|---|---|
| D-1 | Step 2 and Step 3 as separate commits; in Step 2 "the engine simply sends no warning or grace message" | One commit `aa38a55` for Steps 2 and 3 | Step 2 removes the warning/grace `KidMessageReason` values that the engine and ~30 engine tests use. Keeping Step 2 green on its own would have meant rewriting the engine tests once for "no message" and a second time for the overlay push in Step 3. Each commit still builds and tests green with 0 warnings. |
| D-2 | FR-TRAY-032 layer 1: "**new** explicit tests: a report with `{install}\TrayClient\EagleEye.TrayClient.exe` yields no app …; `AppCloser` refuses the path" | No new layer-1 tests; added layer-2 tests only | The explicit tests already exist with exactly the installed tray path: `Monitoring/AgentReportProcessorTests.Process_EagleEyeProgramInInstallFolder_Dropped` and `Enforcement/AppCloserTests.OpenKillSet_ExcludesExemptOpenAndEagleEyePrograms` (both in `02_Implementation/tests/EagleEye.Service.Tests/`). New in `SessionAgent/AppWindowRuleTests.cs`: the measured tray exstyles `0x00010088` (menu, submenu) and `0x080900A8` (overlay) give no app, and the plain `ContextMenuStrip` popup exstyle `0x00010008` **is** an app (documents the D-10 finding). |
| D-3 | Decision 5: "bottom corners: `y = bottom − margin − height`" | Same position, and in a bottom corner the lines are drawn in **reverse order** (the large top line nearest the corner, the others above it) | Story AC-4: "The pill grows … upwards (bottom corners) from its corner, so the top line always stays in the same place at the corner." With the normal order the top line would move up with every extra line. See Q-DEV-1. |
| D-4 | Decision 7: "`SessionEnding` → `e.Cancel` never set" | The overlay does not subscribe to `SessionEnding` at all | Not subscribing cannot block sign-out either (less code). The window never cancels `FormClosing`. |
| D-5 | Decision 10: settings stored by `IOverlaySettingsStore` | Plus a tiny `OverlaySettingsModel` (current settings, `Set` = store + `Changed`) and the menu in `UI/TrayMenu.cs` | Makes "save/load through a fake store" and the menu structure, check marks and clicks unit-testable without a window (AC-15 tests). The plan allowed `TrayMenu.cs` "whichever is shorter". |
| D-6 | Decision 2.2: break line "only if the session has at least one running subject this tick" | Only if at least one subject is running **and not closed in this tick** | A subject closed in this tick already has its 0:00 expiring line; showing the break line for that one tick only would flicker. Same for a break close: only the expiring break line "0:00" is shown. |
| D-7 | Decision 3 (tray): remove the warning info-line keys | Also renumbered the placeholders of `InfoBreakTime` from `{2}/{3}` to `{1}/{2}` | `{1}` was the minutes, which no remaining reason uses; renumbering avoids passing a dummy argument. Texts unchanged. |
| D-8 | Step 6 "file versions and SHA-256" | Done; in addition DEV ran short **startup** smoke checks (§8.4) | Plan Step 5 (live look) is the project owner's; the startup checks prove the real process start, the tray connection and migration 7 on a real 0.8/0.9 database file without any UI claim. |

No other deviations. Story/plan interpretation kept: the plan's Step 5 row 1 says "'Editor 2:59' yellow" with early 2 / final 1; with these levels 2:59 is **white** and turns yellow at 2:00 (AC-7). The Step 5 checklist (§9) is written accordingly.

## 3. Files

| Area | Files (repo-relative) |
|---|---|
| Version | `02_Implementation/Directory.Build.props` |
| Shared (new) | `02_Implementation/src/EagleEye.Shared/Models/RemainingTimeDto.cs`, `OverlayLineDto.cs`, `OverlayLineKind.cs`, `OverlayLevel.cs`, `OverlaySignal.cs`; `Constants/OverlayRules.cs` |
| Shared (changed) | `Contracts/ITrayClientCallback.cs`, `Contracts/IParentHub.cs`, `Models/WarningSettingsDto.cs`, `Models/KidMessageDto.cs`, `Models/KidMessageReason.cs`, `Models/DefaultTextKind.cs`, `Resources/DefaultTexts.cs`, `Resources/DefaultTexts.resx`, `Resources/DefaultTexts.en.resx`, `README.md` (all under `02_Implementation/src/EagleEye.Shared/`) |
| Service (new) | `02_Implementation/src/EagleEye.Service/Communication/IOverlayPublisher.cs`, `OverlayPublisher.cs`; `Enforcement/OverlayLineBuilder.cs` |
| Service (changed) | `Enforcement/TimeLimitEngine.cs`, `AppEnforcement.cs`, `EnforcementLog.cs`, `KidMessageFactory.cs`; `Communication/IKidMessenger.cs` (`ShownInOverlay`, `ReplacedInTick` removed), `ParentHub.cs`; `Rules/WarningSettings.cs`, `WarningSettingsService.cs`, `AppRuleChangeLog.cs`; `Data/ServiceDatabase.cs` (migration 7), `AppRuleRecords.cs`, `WarningSettingsRepository.cs`; `Program.cs` (DI); `README.md` |
| Service (deleted) | `02_Implementation/src/EagleEye.Service/Enforcement/MessageOutbox.cs` |
| Tray (new) | `02_Implementation/src/EagleEye.TrayClient/Overlay/OverlayController.cs`, `OverlayWindow.cs`, `PillRenderer.cs`, `OverlayLayout.cs`, `RemainingTimeClock.cs`, `PillAnimation.cs`, `OverlaySettings.cs`, `RegistryOverlaySettingsStore.cs`, `ForegroundHook.cs`, `OverlayNative.cs`; `UI/TrayMenu.cs`, `UI/ToolWindowMenus.cs`, `UI/TrayWindowStyles.cs` |
| Tray (changed) | `Program.cs` (PerMonitorV2), `EagleEye.TrayClient.csproj` (`ApplicationHighDpiMode`, `InternalsVisibleTo DynamicProxyGenAssembly2`), `Communication/IServiceConnection.cs`, `ServiceConnection.cs` (`RemainingTimeReceived`), `UI/TrayApplicationContext.cs`, `UI/KidMessageText.cs`, `Resources/TrayTexts.resx`, `TrayTexts.en.resx`, `README.md` |
| Parent app Core | `02_Implementation/src/EagleEye.ParentApp.Core/ViewModels/WarningRowViewModel.cs`, `WarningSettingsViewModel.cs`; `Settings/WarningSettingsModel.cs`; `Communication/IParentHubClient.cs`, `ParentHubClient.cs`; `AppTexts.cs`; `Resources/AppTexts.resx`, `AppTexts.en.resx` |
| Parent app XAML (Windows and Mac) | `02_Implementation/src/EagleEye.ParentApp/Views/SettingsView.xaml`, `SettingsView.xaml.cs` |
| Tests | see §4 |
| Evidence | `02_Implementation/docs/requirements/user-stories/US-010/evidence/step4-pill-render-offline.png`; Step 7 (Mac): `evidence/step7-settings-warnings-light.png`, `evidence/step7-settings-warnings-dark.png`, `evidence/step7-probe-log.txt`, `evidence/step7-ui-probe.cs.txt` (temporary probe source, not in `src/`) |

## 4. Unit Tests

Windows, `pwsh 02_Implementation/scripts/test.ps1`, after the last code commit `0c6d672`: **3,096 tests green**, **0 warnings** (`build.ps1` and each test project: 0 warnings, 0 errors).

| Project | US-009 baseline | 0.10.0 | Change |
|---|---|---|---|
| Shared.Tests | 305 | **314** | + `Constants/OverlayRulesTests` (LevelFor boundaries 0, 1, final×60, +1, early×60, +1), `Models/RemainingTimeDtoTests` (JSON round trip with the web defaults SignalR uses, empty lines, equality); `DefaultTextsTests` now two kinds (warning texts removed, resource keys gone); `AppRulesDtoTests` enums (three reasons, two kinds) |
| Service.Tests | 1,493 | **1,506** | + `Communication/OverlayPublisherTests` (not connected → state, nothing sent; newest connection → `ShownInOverlay`, one `SendAsync`; send failure and client lookup failure never throw, Debug log); + `Enforcement/OverlayLineBuilderTests` (own time ignores a nearer break, beyond horizon → no line, not allowed today = 0 counting, app grace, break line next/in effect/grace/none, rounding); `TimeLimitEngineTests` and `TimeLimitEngineGroupTests` rewritten to the overlay model (one line per ungrouped app and per group, own time vs break, break line only with a running app, grace lines, Expired signal + 0:00 expiring line until closed, replaced when a member runs again, signals once per subject/day, strongest per tick, Counting false when not in use, empty push for every controlled session, none for uncontrolled sessions, no `ShowKidMessage` for warnings/graces, log state "shown in overlay"/"not shown (tray client not connected)", horizon ≥ 60 min); `AppEnforcementTests` (sessions in use reach the engine); `Data/WarningSettingsRepositoryTests` (**migration 7**: v6 file with texts and minutes 7/2 → schema 7, columns `Id, EarlyMinutes, FinalMinutes, ChangedUtc`, minutes 7/2 read back, other data kept; fresh database → schema 7 without texts; repository round trip); schema version 6 → 7 in the other repository tests; `WarningSettingsServiceTests` (no text API, minutes validation unchanged); `ParentHubAppRulesTests` (`SetWarningText` gone); `EnforcementLogTests` (new endings); `SessionAgent/AppWindowRuleTests` (tray exstyles, D-2); text cases of `KidMessageFactory`/`MessageOutbox` deleted |
| TrayClient.Tests | 95 | **168** | + `Overlay/OverlayLayoutTests` (thresholds 15/30/45/60 at 899/900 … 3599/3600, early level above the threshold, sort with ties break → name ignoring case → ordinal, cap 5 with "+N more"/"+N weitere", exactly 5, name truncation incl. an emoji at the cut, m:ss/h:mm:ss, break text de/en/fr, levels per line, AnyFinalAboveZero, nothing due); `RemainingTimeClockTests` (countdown per whole second, non-counting lines, floor 0, stale at 15 s, restart on a new push, clear, next-tick timing); `PillAnimationTests` (base alpha per opacity, quick pulse 3 s at 4 Hz, slow pulse 1 Hz, quick overrides slow, sound only with a signal and the sound on, quick pulse on Early only); `OverlaySettingsTests` (defaults, sanitize every invalid/missing value, model load once/save/raise/unchanged ignored); `UI/TrayMenuTests` (order and texts de/en, submenus are tool windows, default check marks, clicks change and store each setting, check marks refreshed on opening, About, `NotifyIcon.ShowContextMenu` exists — TI-10 guard); `UI/TrayWindowStylesTests` (both masks contain `WS_EX_TOOLWINDOW`, never `WS_EX_APPWINDOW`); `UI/TrayTextsOverlayTests` (12 new keys de/en, 7 removed keys gone); `KidMessageTextTests` reduced to the three remaining reasons |
| ParentApp.Tests | 1,109 | **1,108** | `WarningSettingsViewModelTests` (labels and minutes de/en, no text members, minutes validation/commit/pending/error unchanged, flush commits both rows' minutes); `AppRuleRowViewModelTests.WarningRowViewModelTests` (minutes only); `AppRulesModelTests` (warning model without `SetTextAsync`); `AppTextsTests` (new explanation de/en, `WarningsColumnText` gone) |

MacBook, `pwsh 02_Implementation/scripts/test.ps1`, Step 7 (§11): **Shared.Tests 314** and **ParentApp.Tests 1,108** green, `build.ps1` 0 warnings, 0 errors.

Not unit-tested (manual, Step 5 / test run, as planned): `OverlayWindow`, `PillRenderer` pixels, `ForegroundHook`, `OverlayController`, `RegistryOverlaySettingsStore`, `SystemEvents` handling, MAUI views, installers.

## 5. Open Questions and Risks

| # | Point | Default |
|---|---|---|
| **Q-DEV-1** | **Line order in the bottom corners** (D-3): the large top line sits at the corner (bottom) and the smaller lines stack above it. The alternative is the same order as in the top corners (top line at the top), which moves the top line up with each extra line. | **Keep D-3** (matches AC-4 "the top line always stays in the same place at the corner"). **Closed 2026-11-07**: default accepted by the project owner (§9.1). |
| **Q-DEV-2** | **Step 5 setup** uses the Admin account that owns the repo as the controlled account (`EAGLEEYE_DEV_WATCH_SID`), a fresh data folder `02_Implementation/artifacts/smoke-us010-step5` and a fresh Debug pairing. The installed service 0.8.0 and the project owner's installed tray client keep running untouched (a second tray icon appears). | Proceed as in §9. **Closed 2026-11-07**: not needed, Step 5 was replaced by the project owner's pilot check (§9.1). |
| R-1 | Nothing visible was checked by DEV (no display/input in the DEV session, plan TI-15). Pulse, sound, colours, click-through, re-assert, 150 % and the menu popups are Step 5 / the test run. The pill rendering was checked offline only (§8.4). | — |
| R-2 | The existing `package-windows.ps1` publishes the tray client **self-contained** (as before), not framework-dependent as plan S-20 assumed. Its working set is therefore closer to the spike's 136 MB start value. No script change (plan: "Installers — no script change"); AC-27 is the project owner's pilot check. | — |
| R-3 | The pulse/sound can come up to 5 s after the colour change (plan Q-1, accepted). | — |
| R-4 | US-009 R-1 flaky test (`CloseSequenceRunnerTests.ParallelLimit_…`) did not fail in any run of this step. | — |

## 6. How to Test (final, for TES)

**Artifacts** (git-ignored, version 0.10.0, §8.5): `03_Delivery/windows/EagleEye-Setup-0.10.0.exe` (service + tray client, Windows 11), `03_Delivery/windows/EagleEye-ParentApp-Setup-0.10.0.exe` (Windows 10 22H2 / 11, per user); `03_Delivery/macos/EagleEye-ParentApp-0.10.0.dmg` on the MacBook (29,312,683 bytes, SHA-256 `70606ff8bcb04714b2e1efe23242f81ff4eb8cbb0b6f468ebeea36b93d33e1e3`, `CFBundleShortVersionString` 0.10.0, §11). DEV installed none of them. Note for TES: since this section was first written, the project owner installed 0.10.0 in the pilot environment, including the gaming PC `PILOT-PC` (§9.1). The starting versions for AC-35 (0.9.0 → 0.10.0) must therefore be checked on each machine before the test run. The Mac app is still **0.9.0** (the Mac test starting point).

**Install**: service installer elevated over the installed version (migration 7 runs at the first start; the minutes are kept, the texts dropped); tray *App Infos* shows 0.10. Windows parent app as a normal user. **Mac app** (AC-32, AC-34): quit the 0.9.0 app (Cmd+Q), open the `.dmg`, drag *EagleEye Parent App* to *Applications* and replace it. On the first start, allow it once in *System Settings → Privacy & Security → Open Anyway* (not notarised, ADR-019). If it does not connect, apply the Local Network fix from `02_Implementation/docs/dev-process/dev-environments.md` §5 (switch the EagleEye entries off and on, Keychain "Immer erlauben"; check with `nc -z -G 3 <ip> 5443`). The pairing and the settings of 0.9.0 are kept (same bundle id `com.adlemich.eagleeye`). Do not use a 0.9.0 Mac app against a 0.10.0 service (TI-16).

**What to look for**

- Overlay: one line per running ungrouped app, per group with a running app, and the break line; shown only while due (below "Show from", or at/below the early level); shortest first, top line large; white/yellow/red, "0:00" steady red while the service closes the apps; quick pulse at the early level, slow pulse while red, Windows "Exclamation" sound at early, final and 0:00 (if on).
- Bottom corners: the large line is at the corner (D-3, Q-DEV-1).
- Tray menu: right and left button; Opacity 25/50/75/100 %, Position (4 corners), Show from 60/45/30/15 min, Warning sound, separator, About. Defaults 75 %, top right, 30 min, on.
- Settings per Windows account: `HKCU\Software\EagleEye\TrayClient` (`OverlayOpacity`, `OverlayCorner` 0–3, `OverlayShowFromMinutes`, `OverlaySound` 0/1); invalid values fall back to the defaults.
- Timing: push every 5 s; pill gone ≤ 15 s after connection loss or without pushes; immediately on lock (back with the next push after unlock).
- Parent apps (Windows and Mac), Settings → "Zeit-Ende Warnungen": the explanation text (AC-31) and only the columns "Zeitpunkt" and "Minuten vor Ende" (no display-text column), rows "Früthey Warnung" / "Finale Warnung" with the minutes (AC-32). The Mac look was checked by DEV with sample data only (§11); the connected table against a 0.10.0 service is part of the test run.
- Service log (`%ProgramData%\EagleEye\logs\EagleEye.Service-NNN.log`): "Warning: account …, early warning, 2 min left (budget); shown in overlay." / "Grace period: …; shown in overlay." / "…; not shown (tray client not connected)."; "warning levels changed: early … min, final … min"; no "warning text changed" entries.

**Known limitations**: exclusive fullscreen hides the pill (sound still plays, AC-23); the tray client has no log; nothing visual on Windows was verified by DEV (R-1). On the Mac, the column header "Minuten vor Ende" wraps to two lines in its 96-pt column (seen in the Step 7 screenshots, light and dark). This is cosmetic, comes from the shared XAML, and DEV did not change it (§11). Mac app log: the app's data folder as in the earlier stories (unchanged).

## 7. Machine(s) Used

| Steps | Machine |
|---|---|
| 1 to 4, 6 | Windows Developer Machine `DEV-PC`, DEV session over RDP (session 2, account Admin), no display/input usable for checks |
| 5, 9 | Pilot environment (Windows machines incl. gaming PC PILOT-PC), installed 0.10.0, the project owner — passed 2026-11-07 (§9.1) |
| 7 | MacBook (Platform darwin, macOS 27.0.1, Xcode 27.2, .NET SDK 10.0.401, workload `maui-maccatalyst`), DEV session, 2026-11-07 (§11) |

---

## 8. Windows Part (Steps 1 to 4 and 6)

### 8.1 Step 1 — Shared contracts, version (commit `ba64076`)

As planned (additive). Shared.Tests 305 → 317 at this commit; build and all tests green, 0 warnings.

### 8.2 Steps 2 + 3 — Warning texts removed, service push (commit `aa38a55`, D-1)

As planned (Decisions 1 to 3), with D-6 and D-7. Engine flow per session: subjects → lines (`OverlayLineBuilder`) → break grace log → break close → expiring lines → break line → `Publish` → log entries with the publisher state.

**Migration 7 result in tests**: a schema-6 database file with `WarningSettings` (7/2, both texts) → `InitializeAsync` → schema 7, `PRAGMA table_info(WarningSettings)` = `Id, EarlyMinutes, FinalMinutes, ChangedUtc`, minutes 7/2 read back, account selection kept; a fresh database → schema 7 without text columns. Also on a real database file (§8.4).

### 8.3 Step 4 — Tray client (commit `0c6d672`)

As planned (Decisions 4 to 11, spike S-1 to S-19), with D-3 to D-5. `OverlayController` renders only when the text, colours, alpha, DPI, corner or monitor changed; fonts are cached per DPI and the bitmap is reused while its size is unchanged (TI-11). Re-assert on every foreground change and at most once per second while visible. The covered check `FindCoverer` and the uniform-alpha variant were not carried over (as planned).

### 8.4 DEV smoke checks (no UI claims)

| Check | Result |
|---|---|
| Debug tray client started alone (`EAGLEEYE_DEV_PORT_OFFSET=10000`, no service) | Alive after 6 s (overlay window created, hook installed), no crash; stopped by DEV |
| Debug console service on a copy of the US-008 smoke database (schema 6, early 2 / final 1; `EAGLEEYE_DATA_DIR=02_Implementation/artifacts/smoke-us010`, port offset 10000, no watch SID), then the Debug tray client | Service up on 15080/15443, "Warning levels loaded: early 2 min, final 1 min.", "Tray client connected: … (session 2, verified)."; database afterwards: **schema 7**, `WarningSettings` columns `Id, EarlyMinutes, FinalMinutes, ChangedUtc`, minutes 2/1. Both processes stopped by DEV. Console output `02_Implementation/artifacts/smoke-us010-console.txt` (git-ignored) |
| Offline rendering of the pill (temporary test probe, not committed): one line at 100 %, three lines at 100 % and in a bottom corner at 150 %, 25 % opacity | `evidence/step4-pill-render-offline.png`: spike look (dark pill, Segoe UI Semibold, white/yellow/red), top line large ("Spiele 0:42" red), bottom-corner order reversed (D-3) |
| Release publish | `EagleEye.Service.dll`, `EagleEye.TrayClient.dll`, `EagleEye.ParentApp.dll`, `EagleEye.ParentApp.Core.dll` contain no `EAGLEEYE_DEV` string; `en\EagleEye.TrayClient.resources.dll` and `en\EagleEye.Shared.resources.dll` present |

The installed service 0.8.0, the project owner's installed tray client and the gaming PC were not touched. Nothing was installed.

### 8.5 Step 6 — Installers 0.10.0

`pwsh 02_Implementation/scripts/package-windows.ps1 -Target All`, 2026-11-01, built from commit `0c6d672` (service product version `0.10.0+0c6d672…`). **Not installed** on DEV-PC or PILOT-PC (TI-14).

| File (`03_Delivery/windows/`) | File version | Size | SHA-256 |
|---|---|---|---|
| `EagleEye-Setup-0.10.0.exe` | 0.10.0 | 68.0 MB (71,290,688 bytes) | `49BC687CA9A83AA60815221F0F6198C1E273DE08E923F57E27F99A29BF685D5F` |
| `EagleEye-ParentApp-Setup-0.10.0.exe` | 0.10.0 | 70.9 MB (74,355,343 bytes) | `BA87EBB8C7E2C402A8C7BA0BBDBE65497DC2378806B5913430E2A5EDB649DCA9` |

`03_Delivery/windows/README.md` names no version, so it is unchanged.

If Step 5 finds something that needs a fix in the service or tray client, DEV fixes it in Steps 2 to 4 and builds new installers (new hashes here).

---

## 9. Step 5 — the project owner's short on-screen check (pending, about 20 min)

**Where**: the Windows Developer Machine `DEV-PC`, **at the console**, signed in as **Admin** (the account with the repo; this also ends DEV's RDP session). **No CPU/memory run** (AC-27 is a pilot check). The installed service 0.8.0 stays untouched; the Debug service uses other ports and its own data folder.

**Setup (about 5 min)**

1. *Settings → System → Display → Scale*: **150 %** (so the pairing, kid and About dialogs are seen at 150 %).
2. pwsh window 1 in the repo root (not elevated):
   ```powershell
   git pull
   pwsh 02_Implementation/scripts/build.ps1
   $env:EAGLEEYE_DATA_DIR = Join-Path $PWD "02_Implementation/artifacts/smoke-us010-step5"
   $env:EAGLEEYE_DEV_PORT_OFFSET = "10000"
   $env:EAGLEEYE_DEV_WATCH_SID = ([System.Security.Principal.WindowsIdentity]::GetCurrent()).User.Value
   & ./02_Implementation/src/EagleEye.Service/bin/Debug/net10.0-windows/win-x64/EagleEye.Service.exe
   ```
   Leave it running (console log).
3. pwsh window 2 in the repo root:
   ```powershell
   $env:EAGLEEYE_DEV_PORT_OFFSET = "10000"
   & ./02_Implementation/src/EagleEye.TrayClient/bin/Debug/net10.0-windows/win-x64/EagleEye.TrayClient.exe
   ```
   A second EagleEye tray icon appears (the installed tray's menu has only "App Infos"; the Debug one has the new items).
4. pwsh window 3 in the repo root:
   ```powershell
   $env:EAGLEEYE_DEV_PORT_OFFSET = "10000"
   $env:EAGLEEYE_DEV_APP_DATA_DIR = Join-Path $PWD "02_Implementation/artifacts/smoke-us010-app"
   & ./02_Implementation/src/EagleEye.ParentApp/bin/Debug/net10.0-windows10.0.19041.0/win-x64/EagleEye.ParentApp.exe
   ```
   Host `localhost` → the pairing code appears in the Debug tray's pairing window → enter it, device name "US-010 Step 5".
5. Parent app → **Einstellungen**: tick **Admin** under the user accounts; warnings: early **2**, final **1**.
6. Start **Editor** (Notepad) and **Paint** once each: both are blocked at once (deny by default) with the budget text dialog — look at this kid dialog at 150 % (check 4).
7. Parent app → **Regeln** for Admin: Editor **0:03**, Paint **0:20**, today ticked.

**Checks** (write Pass/Fail per row; phone photo of anything odd)

| # | Do | Expected | Source | Result |
|---|---|---|---|---|
| 1 | Start **Paint**, then **Notepad**; type into Notepad all the time and click once where the pill is | Within 10 s a pill **top right**: first "Paint 19:5x"; after Notepad two lines, **"Editor 2:5x" large on top** (white), "Paint 19:5x" small below. Typing is never interrupted; the click goes to Notepad (cursor keeps blinking); no taskbar button, not in Alt+Tab | AC-1, AC-3, AC-4, AC-20 | |
| 2 | Wait for Editor 2:00, 1:00, 0:00 | **2:00** yellow, quick pulse ~3 s, sound; **1:00** red, slow pulse until 0:00, sound; **0:00** steady red, sound; Notepad closes (≤ 20–30 s; a save prompt is force-closed), the **budget text dialog** appears, the Editor line disappears, Paint stays. No warning dialog at any time | AC-5, AC-7 to AC-9, AC-11, AC-14 | |
| 3 | Debug tray icon: open the menu with the **right** and the **left** button; open each submenu; choose Opacity 25 % then 100 %, back to 75 %; Position: each corner (in the bottom corners the large line is at the corner, D-3), back to top right; Show from **15** (Paint line disappears), **60**, back to **30**; Warning sound off (no sound at the next level, if one is left) and on again | Menu order Deckkraft / Position / Anzeigen ab / Warnton / — / App Infos; current values checked; each change visible within 1 s; menu closes normally | AC-15 to AC-17, Q-DEV-1 | |
| 4 | At **150 %**: pill sharp (text not blurry); kid dialog (setup step 6 / check 2), **App Infos** (opens from the Debug tray menu) and the pairing-code window (setup step 4) look right. Then switch to **100 %**: pill sharp, same physical size relative to the screen | No cut-off or tiny text, no blur | AC-28, TI-8 | |
| 5 | Parent app → **Einstellungen**, section "Zeit-Ende Warnungen" | Only the columns "Zeitpunkt" and "Minuten vor Ende"; no "Anzeigetext"; the new explanation ("… Dein Kind sieht die verbleibende Zeit am Bildschirm …") | AC-30, AC-31 | |

**Clean-up**: close Paint and the Debug parent app; end the Debug tray client (it has no exit item; window 2: `Get-Process EagleEye.TrayClient | Where-Object Path -like "*bin\Debug*" | Stop-Process`); stop the Debug service with Ctrl+C in window 1; set the scale back to your normal value. The data folders `02_Implementation/artifacts/smoke-us010*` are git-ignored and can stay.

**Report to DEV/Orchestrator**: Pass/Fail per row 1 to 5 and photos. Findings go back to Steps 2 to 4 (and new installers).

### 9.1 Result (2026-11-07, recorded by the Orchestrator)

The project owner did not run the Debug setup above. They installed the 0.10.0 installers (§8.5) in the pilot environment on the Windows machines and checked there instead. This covers Step 5 and Step 9 together. Their summary: "the overlay pill was tested OK in different games and applications, all as expected." They approved the Windows part as is and closed the open questions with their defaults (Q-DEV-1 keep D-3, Q-DEV-2 not needed). Bugs found later are filed as issues.

No result was recorded per row, so the rows of the table above and the Step 9 points of the plan stay unrecorded. The test run (TES, step d) covers them. Mixed versions on PILOT-PC (TI-16): the Mac app stays on 0.9.0 until the `.dmg` 0.10.0 from Step 7 is installed.

---

## 10. H1 — Handover to the MacBook (Step 7) — done 2026-11-07

Everything is pushed on `feature/US-010-time-overlay`. The shared changes the Mac must build: `EagleEye.Shared` (contracts), `EagleEye.ParentApp.Core` (warning row without texts, new explanation text) and the shared `EagleEye.ParentApp/Views/SettingsView.xaml(.cs)` (no text column). The Windows installers stay on DEV-PC (`03_Delivery/windows/`, git-ignored).

**What the project owner runs on the MacBook**

1. In the repo (outside iCloud Drive, e.g. `~/EagleEye`):
   `git fetch && git switch feature/US-010-time-overlay && git pull`
2. `pwsh 02_Implementation/scripts/env-check.ps1` → host "MacBook", branch `feature/US-010-time-overlay`, no uncommitted changes, Xcode and the `maccatalyst` workload found.
3. Start a Claude Code DEV session in `02_Implementation/agents/dev/` with this prompt:
   > You are DEV on the MacBook. US-010 step c, plan Step 7 (`02_Implementation/docs/requirements/user-stories/US-010/implementation-plan.md`). Steps 1 to 4 and 6 are done on Windows (`implementation-report.md` §8; Step 5 is the project owner's check on Windows, §9). Run `pwsh 02_Implementation/scripts/build.ps1` and `test.ps1` (Shared 314, ParentApp 1,108 expected; 0 warnings, all green). Build the Mac app Debug and do the look check of the Settings page with the probe/`screencapture` pattern of US-008 §8.3, light and dark: the section "Zeit-Ende Warnungen" has only the columns "Zeitpunkt" and "Minuten vor Ende" and the new explanation text (AC-31, AC-32); the connected two-column table against a 0.10.0 service is checked in the test run (no 0.10.0 service is installed anywhere). Build the `.dmg` with `pwsh 02_Implementation/scripts/package-macos.ps1`, check it is 0.10.0 and put its SHA-256 in the report. Do not install it over the project owner's Mac app (test starting point). Write report §Mac (Step 7) and update §6 "How to test". Step 5 and Step 9 already passed (§9.1), so once Step 7 is green set the story to `Implemented` (status line and change log in `user-story.md`). Commit and push (H2).

**What Step 7 contains** (plan Step 7)

| # | Step 7 | Expected |
|---|---|---|
| 7.1 | `build.ps1`, `test.ps1` | Shared + ParentApp green, 0 warnings |
| 7.2 | Mac Debug build, Settings look check (light/dark) | No text column, new explanation (AC-31, AC-32) |
| 7.3 | `package-macos.ps1` | `03_Delivery/macos/EagleEye-ParentApp-0.10.0.dmg` (`CFBundleShortVersionString` 0.10.0), SHA-256 in the report; not installed |
| 7.4 | Report §Mac | Commit, push (**H2**); back on Windows Step 8 only if Step 7 changed code |

Mac-side note (TI-16): the Mac app paired with the gaming PC PILOT-PC must not be used against a 0.10.0 service there until the Mac app is 0.10.0 too (H3).

---

## 11. Mac Part (Step 7, MacBook, 2026-11-07)

Host MacBook (`env-check.ps1`: machine role MacBook, macOS 27.0.1, Xcode 27.2, .NET SDK 10.0.401, workload `maui-maccatalyst`, PowerShell 7.6.5). Branch `feature/US-010-time-overlay`, clean and in sync with `origin` at `4a8eb3f` before the work started. No internet access was needed (the restore was up to date).

| # | Step | Result |
|---|---|---|
| 7.1 | `pwsh 02_Implementation/scripts/build.ps1`, `pwsh 02_Implementation/scripts/test.ps1` | Build succeeded (Debug, Shared + ParentApp Mac Catalyst): **0 warnings, 0 errors**. Tests: **Shared.Tests 314/314**, **ParentApp.Tests 1,108/1,108** green, as expected. |
| 7.2 | Mac Debug build, Settings look check, light and dark | **Passed.** The section "Zeit-Ende Warnungen" shows the new explanation text (AC-31) and only the columns "Zeitpunkt" and "Minuten vor Ende", with the rows "Früthey Warnung" 5 and "Finale Warnung" 1. There is no display-text column and no text editor (AC-32). Readable in both themes; no stale background (Catalyst unset-colour lesson, `dev-environments.md` §5). |
| 7.3 | `pwsh 02_Implementation/scripts/package-macos.ps1` | `03_Delivery/macos/EagleEye-ParentApp-0.10.0.dmg`, 29,312,683 bytes, SHA-256 `70606ff8bcb04714b2e1efe23242f81ff4eb8cbb0b6f468ebeea36b93d33e1e3`. Mounted read-only: `CFBundleShortVersionString` **0.10.0**, `CFBundleVersion` 1, volume "EagleEye Parent App 0.10.0". All script checks OK (bundle id, `NSLocalNetworkUsageDescription`, de/en strings and resources, arm64 only). The file is git-ignored. **Not installed**: the project owner's Mac app stays on 0.9.0 (test starting point). |
| 7.4 | Report, story status, commit, push (H2) | This section, §6 final, §10 marked done; `user-story.md` → `Implemented`. |

**Look check method** (as in US-008 §8.3): the Debug build has its own data and is not paired, and DEV must not connect it to the pilot service PC PILOT-PC (0.10.0, TI-16). So a **temporary probe** replaced the connection coordinator, the accounts model and the warning settings model with fakes (connected to "DEV-PC", one controlled account `eagleeye-kid`, warnings 5/1). It selected the Settings page and set the theme from `EAGLEEYE_UI_THEME`. The window was captured with `screencapture -l <window id>`. Afterwards the probe and its two hook lines (`MauiProgram.cs`, `MainPage.xaml.cs`) were removed. The committed source tree is unchanged: `git status` shows only the evidence files. The probe source is kept as evidence.

| Evidence (`02_Implementation/docs/requirements/user-stories/US-010/evidence/`) | Shows |
|---|---|
| `step7-settings-warnings-light.png` | Settings page, light: explanation text, columns "Zeitpunkt" / "Minuten vor Ende", rows 5 / 1 |
| `step7-settings-warnings-dark.png` | Same in dark mode |
| `step7-probe-log.txt` | Probe log: page settings, theme light/dark, warnings ready True, early 5, final 1 |
| `step7-ui-probe.cs.txt` | Source of the temporary probe (not in `src/`) |

**Observation (cosmetic, not changed)**: the header "Minuten vor Ende" wraps to two lines ("Minuten / vor Ende") in the fixed 96-pt column of the shared `SettingsView.xaml` (column widths from the plan: 160 | 96). The text is complete and readable. Changing the width would be a shared XAML change, and with it a Windows Step 8, so DEV left it as planned. TES or the project owner can file it as an issue if it should change.

**Code changes in Step 7**: none. Only docs and evidence changed. **Step 8 (Windows close-out) is therefore not needed** (plan: "only if Step 7 changed code").

**Deviations in Step 7**: none. Not done by DEV, as planned: the connected two-column table against a live 0.10.0 service, and installing the `.dmg`. Both are in the TES test run (AC-32, AC-34).

**H2**: committed and pushed on `feature/US-010-time-overlay`. The `.dmg` stays on the MacBook (`03_Delivery/macos/`, git-ignored). Back on Windows: `git pull`; no Step 8.


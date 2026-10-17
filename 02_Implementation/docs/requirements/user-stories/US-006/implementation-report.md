# Implementation Report: US-006 — App Time Budgets and Time-elapsed Warnings

**Author**: DEV
**Date**: 2026-10-12
**Branch**: `feature/US-006-app-time-budgets`
**Plan**: `02_Implementation/docs/requirements/user-stories/US-006/implementation-plan.md` (approved 2026-10-12 with Q-1 to Q-5), ADR-015, ADR-016, the US-006 amendments of ADR-006, ADR-012, ADR-013, ADR-014, arc42 and coding guidelines §10.1
**Machine(s) used**: Windows Developer Machine only (see §9)
**Story status**: Implemented (DEV, 2026-10-12)

---

## 1. Summary

US-006 is implemented as planned in Steps 0 to 12, version **0.6.0**. The parent sets a daily time budget and the allowed weekdays per app record and account on the Rules page (new section "Applikations-Regeln"), and the two global time-elapsed warnings on the Settings page (new section "Zeit-Ende Warnungen"). The service stores them (migration 5), broadcasts every change and enforces them. A start is blocked by a break (US-005), then by the app rule at the time of detection: not allowed today, or budget used up. Deny by default holds through a missing rule row. A blocked start now creates the app record, so the app appears in the Rules table. Once per 5 s accounting tick, the time-limit engine computes the end of each running app's allowed time: budget, not allowed today, midnight, or the next break begin. It shows the early and final warnings once, starts a grace period of the full final-warning minutes after a parent change that moved the end earlier (Q-2), and closes the app with the US-005 close sequence when no time is left. A break begin closes all running apps of the session with one message. The kid dialog shows the parent's text or the default text in the kid's language (ADR-016), with an info line below it. A newer message replaces the text of an open dialog.

| Component | What was built |
|---|---|
| `EagleEye.Shared` | `IParentHub`: 7 methods (`GetAppRules`, `SetAppBudget`, `SetAppDay`, `SetBudgetText`, `GetWarningSettings`, `SetWarningMinutes`, `SetWarningText`); `IParentClientCallback.OnAppRulesChanged`, `OnWarningSettingsChanged`; `ITrayClientCallback.ShowKidMessage` replaces `ShowBreakTimeMessage` (`BreakTimeMessageDto` removed); DTOs `AppRulesDto`, `AppRuleDto`, `WarningSettingsDto`, `KidMessageDto`; enums `AppBudgetPart`, `WarningLevel`, `KidMessageReason`, `DefaultTextKind`, `KidMessageResult.Replaced`; `Constants/AppRuleRules`, `Constants/WarningRules`; `Resources/DefaultTexts` + `DefaultTexts.resx` (de, neutral) + `DefaultTexts.en.resx` (ADR-016) |
| Service — `Data/` | Migration 5 (`AppRules`, `AccountBudgetTexts`, `WarningSettings`, `BlockedStarts` rebuilt with nullable entry columns plus `Kind`, `Reason`, `AfterGrace`, `BudgetMinutes`, `BudgetDays`, `UsedSeconds`); `AppRuleRepository`, `WarningSettingsRepository`; `BlockedStartRepository` with the new columns |
| Service — `Rules/` | `AppRuleService` (state owner of `AppRules:{sid}`, `AppRulesSnapshot` read lock-free, ignore-list programs never listed, new/renamed app records broadcast, budget-text purger), `WarningSettingsService` (state owner of `WarningSettings`), `AppRuleChangeLog`, broadcaster overloads, `AppNotFoundException`, `RuleViolation.FinalNotLessThanEarly`, `BreakTimeSchedule.NextBegin` / `FirstPassUtc`; `BreakTimeService.SetDisplayTextAsync` now stores the English default as "default" too (ADR-016) |
| Service — `Enforcement/` | `AppStartGate` (break → app rule at detection; every controlled account screened), `TodayUsageLedger`, `CloseRequest` (kind, reason, after grace, rule, message, shared message state), `CloseRegistry`, `SessionAppsView`, `CloseSequenceRunner` (app record at t₀ for blocked starts, shared message state for batches, weekday of the close for running apps), `AppCloser` (TI-9), `AppEndCalculator`, `WarningLedger`, `GracePolicy` (minimum grace), `KidMessageFactory`, `MessageOutbox`, `TimeLimitEngine`, `EnforcementLog` (new templates), `AppEnforcement` (facade), `TimeChangeMonitor` (old/new local date, AC-39) |
| Service — other | `ParentHub` (7 methods, safe texts "The app no longer exists." and "The final warning must be less minutes …"); `KidMessenger` (`KidMessageDto`, states `shown`, `shown (replaced the open message)`, …); `UsageService` (`IAppInventory.EnsureAppRecordAsync`, `IAppRecordObserver` notified after its lock); `UsageAccountingLoop` (tick order: advance and reconcile → output → usage → enforcement with the same credits); `Program` (start order … usage → app rules → warning settings → break times …); DI |
| `EagleEye.TrayClient` | `ShowKidMessage` handler, `KidMessageText` (default text + info line by reason in the kid's language, singular/plural, the kid's short time format, 23:59 = midnight), `KidMessagePresenter` (`Shown` / `Replaced`), `KidMessageDialog` (text, separator, info line; `UpdateText` re-lays out, re-asserts topmost, one foreground attempt), 11 info-line texts de/en |
| `EagleEye.ParentApp.Core` | Hub client and gateway (2 events, 7 calls); `Rules/SelectedAccountModel<T>` (common replica and write logic, extracted from `AccountRulesModel`), `AppRulesModel`, `AppBudgetText`; `Settings/WarningSettingsModel`, `WarningEditRules`; `ViewModels/KidTextEditing`, `AppRulesSectionViewModel`, `AppRuleRowViewModel`, `WarningSettingsViewModel`, `WarningRowViewModel`; `RulesViewModel` hosts the section, its display text follows ADR-016; 19 texts de/en |
| `EagleEye.ParentApp` | `RulesView` (section "Applikations-Regeln": columns 220 \| 64 \| 64 \| 7 × 48, program path as tooltip, budget text), `SettingsView` (section "Zeit-Ende Warnungen", columns 160 \| 96 \| \*), `MainPage` (flush also when leaving Settings), `MauiProgram` (DI) |
| Version / installers | `Directory.Build.props` 0.6.0; both installers built with the unchanged `package-windows.ps1` |

---

## 2. Deviations from the Implementation Plan

| # | Plan | Implemented | Why |
|---|---|---|---|
| D-1 | Grace detection: the engine keeps "the end it computed last" and the revisions of `AppRules:{sid}` / `AccountRules:{sid}`; on a revision change it compares old and new end | The engine keeps the account's rules objects of the last tick (`AccountRules`, `AccountAppRules`; immutable, replaced on every write). When either object changed, it computes the old end **with the old rules at the same moment** and compares it with the new end. | `BreakTimeService` has one revision counter for all accounts and no per-account revision. Comparing old and new rules at the same moment is exact; an end stored one tick earlier would differ by the 5 s of use or wall time in between. The decision rule (`GracePolicy.Starts`: new left < final minutes and < old left) is unchanged. |
| D-2 | Start log "App rules loaded: … ; warning levels early E min, final F min." (one line) | Two lines: `App rules loaded: {Rules} rules of {Apps} apps of {Accounts} accounts, {Texts} changed budget texts.` and `Warning levels loaded: early {E} min, final {F} min.` | The two state owners load independently. Same information. |
| D-3 | `TodayUsageLedger.InitializeAsync` in the `Program` start order | `EnsureLoadedAsync` on every tick: it loads the controlled accounts not loaded yet (at the first tick: all), forgets accounts that left control. The `AppId` of each `DailyUsage` row is mapped to the program path through the app-rules snapshot. | The first event of the loop is a tick, so the ledger is loaded before the first report is screened. This also covers "an account becomes controlled later". `IUsageRepository.GetUsageAsync` returns `AppId`, not the path. Credits of a newly loaded account are not added twice: they are applied only to accounts loaded before this tick's persist. |
| D-4 | `KidMessenger` change in Step 7 | Done in Step 5 | The generalized close sequence needs `KidMessageDto`. |
| D-5 | `WarningSettingsChangeLog` as its own class | Its texts are in `AppRuleChangeLog` | Two pure text methods; one class keeps the change texts together. |
| D-6 | — | `MessageOutbox` (internal, Enforcement) | Holds the "one message per session and tick" rule (plan D-4) and the "not shown (replaced in the same tick)" log, so `TimeLimitEngine` stays readable. |
| D-7 | — | After a running-app close has ended, the engine ignores the app while its report still shows exactly the closed processes | The agent's next report can arrive after the next tick. Without this check, the engine would start a second close and send a second message for processes that are already gone. A new start of the app (new processes) is judged normally. |
| D-8 | Grace end | When the grace runs out, the close uses the end the app has now (break text for a break end, budget text otherwise), trigger `grace period ended`, `AfterGrace = 1`. If the natural time left is positive and at least the grace time left (e.g. the parent raised the budget), the grace ends and the app keeps its natural time. | ADR-015 §5 "effective time left = max(grace left, natural left)". |
| D-9 | Triggers of running-app closes | `break began`, `budget ran out`, `grace period ended`, `midnight`, `rules changed`. A midnight end never reaches 0 before midnight: at 00:00 the end becomes "not allowed today", recorded as `midnight` when the previous tick's end was midnight. An app first seen with no time left (e.g. after a service restart) is recorded as `rules changed` (not allowed today) or `budget ran out`. | The plan lists the trigger values but not every mapping. |
| D-10 | `BreakTimeSchedule.NextBegin(entries, local, horizon)` | `NextBegin(entries, nowUtc, zone, horizon)` → `BreakBegin(Entry, StartLocal, Until)` with real time until the start | Real seconds are needed for warnings and graces across DST transitions; the zone tells skipped (invalid) and repeated (first pass) start times apart. |
| D-11 | Renames of Step 2 | As planned, plus `IBlockedStartHandle` → `ICloseSequenceHandle`, `IBreakTimeMessagePresenter` → `IKidMessagePresenter`, `AddBreakTimeEnforcement` → `AddAppEnforcement`. Kept: `BlockedStartTexts`, `BlockedStartRecord`, `IBlockedStartRepository` (the table keeps its name `BlockedStarts`, plan D-7). New constants `CloseKinds`, `CloseReasons` live in `Data/`. | Consistent names; history names follow the table name. |
| D-12 | `RulesViewModel(accounts, rules, dispatcher)` hosts the section | The section view model is injected: `RulesViewModel(accounts, rules, AppRulesSectionViewModel, dispatcher)` | DI creates the section; tests inject it with a mocked model. |
| D-13 | `WarningSettingsModel` "like `UserAccountsModel`" | Derived from the extracted `SelectedAccountModel<T>` with one fixed key | Same fetch-on-connect, revision, write-confirmation and 4 s timeout logic without a third copy; the US-005 `AccountRulesModel` tests run unchanged and green on the base. |
| D-14 | Kid dialog: newer message replaces the text | Also: if the kid closes the dialog at the very moment a replacement arrives, the newer message opens a new dialog | Otherwise that message would be lost although the tray answered `Replaced`. |
| D-15 | Smoke check Step 10.2 items 3 to 6 (Notepad blocked, warnings, close, grace, break begin) and Step 11.2 (CPU with 10 allowed apps running) in DEV's own session | **Not done live.** The smoke check ran with the console service controlling no session (see §8). | Michael's admin session was active the whole time (`query user`: not idle). With deny by default, a Debug service that controls this session closes every running app without a rule at once (VS Code, terminals, browser). These paths are covered by unit tests (§5) and must be checked in the TES run with `kid1`. |
| D-16 | Step 10.1: check the satellite "in both installers (list the Inno Setup output or install into a temp folder)" | Checked in the publish output that both installers package recursively (`[Files] … recursesubdirs`): `Service\en\`, `TrayClient\en\` and `ParentApp\en\EagleEye.Shared.resources.dll` are present | There is no extraction tool on this PC, and installing the per-user parent app would have upgraded Michael's installed 0.5.0 parent app. TES checks the English default in the kid dialog (plan R-10). |

The plan's own story interpretations D-1 to D-11 are implemented as written.

---

## 3. Security notes (coding guidelines §12.4)

| Rule | How it is met |
|---|---|
| Agent unchanged | No change in `SessionAgent/`: the agent still only posts `WM_CLOSE` to app windows of verified targets. Running apps are closed through the same command (`CloseSequenceRunner` → `AppCloser` → supervisor). |
| Kill set (ADR-013 §5, TI-9) | `AppCloser` now removes only the closed app's **own** path from the "other open apps" before `KillSetBuilder` runs, so a running app's own processes are roots again. Other open apps and their descendants, other sessions/owners, EagleEye programs and the ignore list stay excluded (unit test `OpenKillSet_RunningAppIsItselfOpen_ItsOwnPathIsStillARoot`). |
| Hub input | All 7 new hub methods are paired-only (default-deny filter); invalid SID, `Guid.Empty`, out-of-range values, unknown enums, texts > 500 code units or with lone surrogates → "Invalid request."; service exceptions → safe texts; unexpected failures are logged, never returned. |
| Texts never logged | Budget and warning texts are not logged (only "… text changed" / "… reset to the default"); history rows store no texts. |
| Debug-only switches | No new switches. The Release DLLs (`EagleEye.Service.dll`, `EagleEye.TrayClient.dll`, `EagleEye.ParentApp.dll`, `EagleEye.ParentApp.Core.dll` in `artifacts/publish`) contain no `EAGLEEYE_DEV` string (checked). |

---

## 4. Files

All paths relative to `02_Implementation/`.

**Shared** — new: `src/EagleEye.Shared/Constants/{AppRuleRules, WarningRules}.cs`, `Models/{AppBudgetPart, AppRuleDto, AppRulesDto, DefaultTextKind, KidMessageDto, KidMessageReason, WarningLevel, WarningSettingsDto}.cs`, `Resources/{DefaultTexts.cs, DefaultTexts.resx, DefaultTexts.en.resx}`; changed: `Contracts/{IParentHub, IParentClientCallback, ITrayClientCallback}.cs`, `Models/KidMessageResult.cs`, `EagleEye.Shared.csproj` (neutral language de, satellite en), `README.md`; removed: `Models/BreakTimeMessageDto.cs`.

**Service** — new: `src/EagleEye.Service/Data/{AppRuleRecords, IAppRuleRepository, AppRuleRepository, WarningSettingsRepository}.cs`, `Rules/{AppRulesSnapshot, AppRuleChangeLog, IAppRuleService, AppRuleService, WarningSettings, WarningSettingsService}.cs`, `Enforcement/{AppEndCalculator, CloseRegistry, GracePolicy, KidMessageFactory, MessageOutbox, SessionAppsView, TimeLimitEngine, TodayUsageLedger, WarningLedger}.cs`; renamed and changed: `Enforcement/{AppStartGate (BreakTimeGate), CloseRequest (BlockedStart), CloseSequenceRunner (BlockedStartRunner), ICloseSequenceRunner (IBlockedStartRunner), AppCloser (BlockedAppCloser), EnforcementLog (BlockedStartLog), AppEnforcement (BreakTimeEnforcement)}.cs`; changed: `Data/{ServiceDatabase, BlockedStartRepository, BreakTimeRecords}.cs`, `Rules/{AccountRulesBroadcaster, IAccountRulesBroadcaster, BreakTimeSchedule, BreakTimeService, RuleExceptions}.cs`, `Enforcement/{EnforcementRegistration, TimeChangeMonitor}.cs`, `Communication/{ParentHub, KidMessenger, IKidMessenger}.cs`, `Statistics/{IUsageService, UsageService, UsageAccountingLoop, UsageRecordingRegistration}.cs`, `Program.cs`, `README.md`.

**TrayClient** — new: `src/EagleEye.TrayClient/UI/{KidMessagePresenter, KidMessageText}.cs`; renamed and changed: `UI/KidMessageDialog.cs` (BreakTimeMessageDialog); changed: `Communication/{IServiceConnection, ServiceConnection}.cs`, `UI/{TrayApplicationContext, TrayTexts}.cs`, `Resources/TrayTexts*.resx`, `README.md`; removed: `UI/BreakTimeMessagePresenter.cs`.

**ParentApp.Core** — new: `src/EagleEye.ParentApp.Core/Rules/{SelectedAccountModel, AppRulesModel, AppBudgetText}.cs`, `Settings/{WarningSettingsModel, WarningEditRules}.cs`, `ViewModels/{KidTextEditing, AppRulesSectionViewModel, AppRuleRowViewModel, WarningSettingsViewModel, WarningRowViewModel}.cs`; changed: `Communication/{IParentHubClient, ParentHubClient, IParentHubGateway, ParentHubGateway}.cs`, `Rules/AccountRulesModel.cs`, `ViewModels/RulesViewModel.cs`, `AppTexts.cs`, `Resources/AppTexts*.resx`, `README.md`.

**ParentApp** — changed: `src/EagleEye.ParentApp/Views/{RulesView.xaml, RulesView.xaml.cs, SettingsView.xaml, SettingsView.xaml.cs, MainPage.xaml.cs}`, `MauiProgram.cs`, `README.md`.

**Tests** — new: Shared `Constants/{AppRuleRulesTests, WarningRulesTests}`, `Models/AppRulesDtoTests`, `Resources/DefaultTextsTests`; Service `Data/{AppRuleRepositoryTests, WarningSettingsRepositoryTests}`, `Rules/{AppRuleServiceTests, AppRulesSnapshotTests, AppRuleChangeLogTests, WarningSettingsServiceTests, BreakTimeScheduleNextBeginTests}`, `Communication/ParentHubAppRulesTests`, `Enforcement/{AppEndCalculatorTests, EnginePartsTests (WarningLedger, GracePolicy, KidMessageFactory, MessageOutbox), TimeLimitEngineTests, TodayUsageLedgerTests (+ CloseRegistry, SessionAppsView), AppEnforcementTests, EnforcementLogTests}`; TrayClient `UI/{KidMessagePresenterTests, KidMessageTextTests}`; ParentApp `Communication/ParentHubGatewayAppRulesTests`, `Rules/{AppRulesModelTests (+ WarningSettingsModel), AppBudgetTextTests (+ WarningEditRules, KidTextEditing)}`, `ViewModels/{AppRulesSectionViewModelTests, AppRuleRowViewModelTests (+ WarningRowViewModel), WarningSettingsViewModelTests}`; renamed/changed: the US-005 enforcement tests (gate, runner, closer), data tests (schema version 5), hub, messenger, usage service, loop, time-change monitor, broadcaster, break-time service, rules view model and tray text tests.

**Build, docs** — `Directory.Build.props` (0.6.0), this report, `docs/requirements/user-stories/US-006/user-story.md` (status).

---

## 5. Build, Unit Tests and Coverage

**Build** (`scripts/build.ps1`, Debug, final state): **0 warnings, 0 errors** for Shared, Service, TrayClient, ParentApp.Core, ParentApp Windows and ParentApp **Android**.

**Unit tests** (`scripts/test.ps1`, final state): **2 623 passed, 0 failed** — Shared 282, Service 1 365, TrayClient 90, ParentApp 886 (0.5.0: 2 107).

**Coverage** (coverlet, line and branch): **100 % / 100 %** for every new or changed class marked **unit** in the plan: Shared (`AppRuleRules`, `WarningRules`, `DefaultTexts`, the DTOs), Service (`ServiceDatabase`, `AppRuleRepository`, `WarningSettingsRepository`, `BlockedStartRepository`, `AppRuleService`, `AppRulesSnapshot`, `AppRuleChangeLog`, `WarningSettings`, `WarningSettingsService`, `AccountRulesBroadcaster`, `BreakTimeSchedule`, `BreakTimeService`, `ParentHub`, `UsageService`, `UsageAccountingLoop`, `TodayUsageLedger`, `AppEndCalculator`, `WarningLedger`, `GracePolicy`, `KidMessageFactory`, `MessageOutbox`, `TimeLimitEngine`, `CloseRegistry`, `SessionAppsView`, `CloseRequest`, `CloseSequenceRunner`, `AppCloser`, `KillSetBuilder`, `EnforcementLog`, `AppEnforcement`, `AppStartGate`, `TimeChangeMonitor`, `KidMessenger`, `RuleExceptions`), TrayClient (`KidMessageText`, `KidMessagePresenter`, `TrayTexts`), ParentApp.Core (`ParentHubGateway`, `SelectedAccountModel`, `AccountRulesModel`, `AppRulesModel`, `WarningSettingsModel`, `AppBudgetText`, `WarningEditRules`, `KidTextEditing`, `AppRuleRowViewModel`, `AppRulesSectionViewModel`, `WarningRowViewModel`, `WarningSettingsViewModel`, `RulesViewModel`, `AppTexts`). To reach 100 % branch coverage, loops with lambdas that capture locals were written as plain loops, and the custom key comparers became upper-cased tuple keys: coverlet counts the per-closure delegate cache and hash-collision paths as branches. Not unit-tested, as the plan specifies: `KidMessageDialog`, `ServiceConnection`, `ParentHubClient`, `Program`, the MAUI views, the installers.

| Area | Key scenarios |
|---|---|
| Shared | JSON round trips incl. null texts and every `KidMessageReason`; `IsAllowedOn` for every day and 0:00; `Combine` keeps the other part; pair validity; `DefaultTexts.Get` de/en/fallback for every kind; `IsAnyDefault` (de, en; trailing space, `\r\n` appended, other kind, emoji variant → no); English break default exactly the AC-10 text incl. 😊, German unchanged from US-005 |
| Data | schema version 5; migration 5 on a **real version-4 file** (built with migrations 1–4) keeps pairings, selections, usage, break times, display texts and history (old rows → `blocked start` / `break time`), all app records evaluate as the default rule, no warning row; schema checks (budget 0…1439, days 0…127, early > final, kind/reason values); rule equal to the default deletes the row; app of another account → false; cascade on app-record delete; purge of budget texts |
| Rules (service) | init (ignore-list paths left out), get, hours/minutes combined with the stored part, days incl. none, concurrent day writes all kept, budget text (normalized, emojis, empty / de default / en default → default), no-op write = one revision + broadcast + log, storage failure → snapshot unchanged, broadcast failure → warning, new/renamed/unchanged app records, purge; warning levels validated against the stored other level (3/3, 3/4 refused), texts, defaults without row |
| Hub | each method: delegation with the device name, invalid input → "Invalid request.", app not found, final-not-less, unknown account, storage failure → logged + "The change could not be saved.", `HubException` passed through |
| Gate | all US-005 cases unchanged (apps allowed by their rule); default rule, day not ticked, 0:00, used = budget, used > budget → blocked with reason, rule, budget text; budget left → passes; break wins; running-app close keeps the app in the report and adds new processes |
| Engine | AC-22 early at once / final only; AC-24 each level once, restart same day not again; AC-25 close at 0 with rule and budget text, not closed twice, stale report skipped, new processes closed again; first seen without time → closed at once without grace; uncontrolled / not open / ignored / no processes / being closed → nothing; AC-28/29 break warnings once per session, one break message and a shared state for all apps; AC-30; AC-31 break vs budget in one tick (more urgent message sent, other logged "replaced in the same tick"); AC-32 0:00 → grace of the full final minutes, closed after it; 20 s left → full minute; within the early level → early at once, no grace; raised during the grace → grace ends, nothing re-armed (Q-5); end moved later → no grace; old rules without end → grace; AC-33 break now in effect → one grace message per session, closes with the break text; break moved to begin in 30 s → full minute; within the early level → early at once; level change never starts a grace and applies to unshown warnings; not running at the change → no grace; account leaves control → state cleared; midnight with the next day not allowed → final warning "(midnight)", close with trigger midnight |
| Calculator / schedule | each candidate alone, minimum, ties (break first), budget end after midnight with the next day allowed → none, next day not allowed → midnight, midnight inside a skipped DST hour, horizon; `NextBegin` later today, tomorrow 00:00, unticked day, inactive, outside / at the horizon, tie, DST spring (skipped start), DST autumn (first pass) |
| Runner / closer / log | US-005 cases unchanged; blocked start creates the app record (failure → warning, still closed); running app: no app record, kind/reason/after grace/rule/weekday of the close in the record; shared message state used; request without message refused; kill set keeps the closed app's own path; all new log templates; limiter not applied to running-app closes |
| Loop / ledger / usage | tick order: usage persisted first, the enforcement gets the same credits object and the tracker's open state; ledger load, credits, case-insensitive keys, new day → 0, account leaving and returning, load failure logged once; `EnsureAppRecordAsync` without instance/day row, observer called outside the lock (deadlock test), observer failure → warning |
| Time changes | zone change with and without date change, clock jump across midnight → Warning texts with both dates and "daily budgets now use the new day"; one finding per change |
| Tray | every reason de/en, default vs parent text, singular/plural, the kid's time format (en-GB 20:00), end 23:59 → "24:00" / "midnight", other language → German; presenter Shown, Replaced, after OK Shown, reopened, 20 parallel → 1 Shown + 19 Replaced |
| Parent app | gateway forwarding of both events; models: fetch, broadcast filter, writes confirmed, rejection, warning model fetch on connect and when created while connected; parsers (all AC-8 and AC-15 inputs incl. "007" and Arabic digits); `KidTextEditing` (unchanged, line breaks only, empty or default of another language while default → nothing; custom → "" or text); section: states and texts de/en, sorting (case-insensitive, then id), new row inserted in place, renamed row moved, removed row gone, row objects kept, budget text default per language, commit rules, typing kept against snapshots; rows: hours/minutes committed only on leaving, invalid → revert + message, unchanged → nothing, days at the click (all unticked allowed), pending/typed fields kept, failure → message and confirmed value; warnings: two fixed rows with labels, minutes 0/60/abc/2.5/"" and 3/3, 3/4 refused, pending kept, texts, flush; Rules page: account selection selects the section, flush includes the section, break display text default in de/en and stored as default when the other language's default is typed |

---

## 6. Open Questions and Risks

1. **Enforcement not smoke-checked live by DEV** (D-15): blocked starts by app rule, warnings, closes of running apps, grace periods, break begin closing running apps, the kid dialog with the info line and text replacement, and the CPU with 10 allowed apps running were not run in a real session. Michael's admin session was in use. The TES run with `kid1` is the first live run of these paths.
2. **Deny by default right after the upgrade** (plan Q-6, R-1): every app the kids used before is blocked until the rules are set. In the smoke check the migrated database showed all 106 records at 0/0 with no day ticked.
3. **Agent token** (plan Q-4, TC-005-26): graceful closes of running apps depend on the agent's `WM_CLOSE` under the SYSTEM token. Please rerun TC-005-26.
4. **Messages of closes are not part of the per-tick message priority** (plan D-4): a close sends its own message (budget text, or one break text per session) through the close sequence. When a warning of another app is sent in the same tick, the later message replaces the text in the dialog. This is rare and has no effect on enforcement.
5. **Service restart** (plan D-5, D-6, R-9): warnings may repeat; an app in a grace period is closed at once after the restart.
6. **Class size**: `TimeLimitEngine` (~310 lines) and `ParentHub` (~420 lines) are above the ~200-line guideline. The engine's pure parts are separate classes (calculator, ledger, policy, factory, outbox). `ParentHub` stays one thin routing class, because primary-constructor parameters are not shared across partial files.
7. **Second parent app, English UI, 150 % scaling, light mode**: not checked by DEV (one Debug app, German Windows, dark mode). The broadcast path is unit-tested.

---

## 7. Checks for Michael (admin / SYSTEM / real kid session)

Preparation (admin, kids not using the PC, see risk 2): install `03_Delivery/windows/EagleEye-Setup-0.6.0.exe` over 0.5.0, then `EagleEye-ParentApp-Setup-0.6.0.exe` on both parent PCs. Open Rules for `kid1` first and set the test rules (e.g. Notepad 0:03 all days, Paint 0:30 Saturday only, Calculator default). Settings: early 2 / final 1.

| Check | Steps | Expected |
|---|---|---|
| A. Upgrade | First start of 0.6.0 | Log `App rules loaded: 0 rules of N apps …`, `Warning levels loaded: early 5 min, final 1 min.`; history and break times kept |
| B. Agent posts WM_CLOSE for running apps (Q-4) | Notepad (no text) reaches its budget | `App closed: account kid1, Editor (…), budget used up: closed gracefully after 0.x s; message shown.` Also run TC-005-26. |
| C. English defaults (ADR-016, R-10) | A kid account with English Windows display language gets a default warning | The English text and info line (e.g. "Notepad: 2 minutes left."). Proves `en\EagleEye.Shared.resources.dll` is installed. |
| D. CPU (AC-38, Step 11.2) | 10 allowed apps running in `kid1`'s session, 60 s | Service and agent CPU in the range of US-005 (0.009 % / 0.005 %) |

---

## 8. How to Test

### Artifacts and version

| Item | Value |
|---|---|
| Service + tray installer | `03_Delivery/windows/EagleEye-Setup-0.6.0.exe` (admin), built 2026-10-12 13:48, SHA-256 `416E0A74A703A1A7728F541D83E5A40B3D21451A3C462467489507BEB1E03950` |
| Parent app installer | `03_Delivery/windows/EagleEye-ParentApp-Setup-0.6.0.exe` (per user), built 2026-10-12 13:49, SHA-256 `56500A6A4CBDE74301193FC40DA3E6D24A39851ADAE926AABFE2AEFC86ED34E5` |
| Version | 0.6.0 (service reports `EagleEye_v0.6`). Upgrade over 0.5.0 keeps pairings, selections, usage, break times and history; migration 5 runs at the first start. Both installers must be 0.6.0 (a 0.5 parent app has no app-rules section; a 0.5 tray client cannot show `ShowKidMessage`, the service then logs "not shown (tray client did not answer)" and still enforces). |

### UI texts as implemented (de / en)

| Where | German (default) | English |
|---|---|---|
| Rules: heading bar (AC-1) | Applikations-Regeln | Application rules |
| Rules: explanation (AC-1) | Lege hier fest, welche Applikationen an welchen Tagen für wie lange verwendet werden dürfen. … Es werden nur Applikationen angezeigt, die mindestens ein Mal vom Kind gestartet wurden. | Set here which apps may be used on which days and for how long. … Only apps that your kid has started at least once are listed. |
| Rules: columns (AC-2) | Applikation · Tägliches Zeit-Budget · Stunden · Minuten · MO DI MI DO FR SA SO | App · Daily time budget · Hours · Minutes · Mo Tu We Th Fr Sa Su |
| Rules: empty (AC-3) | Noch keine Applikationen aufgezeichnet. | No apps recorded yet. |
| Rules: budget text label (AC-1) | Anzeige Text wenn Budget aufgebraucht: | Display text when budget is used up: |
| Rules: AC-8 | Bitte Stunden zwischen 0 und 23 eingeben. · Bitte Minuten zwischen 0 und 59 eingeben. | Enter hours between 0 and 23. · Enter minutes between 0 and 59. |
| Settings: heading bar, explanation (AC-13) | Zeit-Ende Warnungen · Hier kannst du 2 Zeitpunkte einstellen, … | Time-elapsed warnings · Here you can set 2 points in time … |
| Settings: columns and rows (AC-13) | Zeitpunkt · Minuten vor Ende · Anzeigetext · Frühe Warnung · Finale Warnung | Point in time · Minutes before end · Display text · Early warning · Final warning |
| Settings: AC-15 | Bitte Minuten zwischen 1 und 59 eingeben. · Die finale Warnung muss weniger Minuten vor dem Ende liegen als die frühe Warnung. | Enter minutes between 1 and 59. · The final warning must be less minutes before the end than the early warning. |
| Default texts (AC-10, AC-14) | Du hast deine Zeit aufgebraucht! Leider ist jetzt Schluss. · Achtung, du hast nur noch 5 Minuten! Speichere deinen Fortschritt. · Achtung!!! Nur noch 1 Minute – jetzt aber schnell speichern! · break text unchanged from US-005 | You have used up your time! Sorry, that's it for now. · Attention, you only have 5 minutes left! Save your progress. · Attention!!! Only 1 minute left – save quickly now! · Hi! Unfortunately, your parents have set a PC break for this time. You can't use this program right now. Sorry. How about using the time to read a book? 😊 |
| Kid dialog info line (AC-34) | Editor: noch 2 Minuten. · Editor: noch 1 Minute. · Die Ruhezeit beginnt um 20:00. · Editor: Deine Eltern haben die Regeln geändert. Editor wird in 1 Minute geschlossen. · Deine Eltern haben eine Ruhezeit eingestellt. Alle Apps werden in 1 Minute geschlossen. · Editor: heute nicht erlaubt. · Editor: Zeit-Budget aufgebraucht. · Ruhezeit von 20:00 bis 24:00. | Notepad: 2 minutes left. · Notepad: 1 minute left. · Break time begins at 8:00 PM. · Notepad: your parents changed the rules. Notepad closes in 1 minute. · Your parents set a break time. All apps close in 1 minute. · Notepad: not allowed today. · Notepad: time budget used up. · Break time from 8:00 PM to midnight. |

Behaviour of the editors: an unchanged default is shown in the parent app's language; typing the default of either language, or emptying the box, stores "default". Hours and minutes are saved when the field is left or Enter is pressed, ticks at the click. Typing "24", "-1", "1.5" or an empty value shows the message and the field returns to the stored value. Letters cannot be typed (numeric field).

### Log entries (service log, Information unless noted; from the smoke check and the unit-test templates)

```
App rules loaded: 0 rules of 106 apps of 1 accounts, 0 changed budget texts.
Warning levels loaded: early 5 min, final 1 min.
App rules of account ee-anna (S-1-5-21-…-1003) changed by DEV-Smoke: app rule changed: Battlefield™ 6, 1:30, Mo Sa (request …, revision 5).
   … budget text changed (…) · … budget text reset to the default (…)
Warning settings changed by DEV-Smoke: warning levels changed: early 2 min, final 1 min (request …, revision 3).
   … early warning text changed (…) · … final warning text reset to the default (…)
New app for account kid1: Rechner (CalculatorApp.exe, …).          ← also for blocked starts (the app appears in Rules)
Blocked start: account kid1, Editor (notepad.exe, C:\Windows\System32\notepad.exe), process 5120, budget used up (rule 0:03, Mo Tu We Th Fr Sa Su, used 0:03:00); closing the app.
Blocked start: account kid1, Rechner (…), process …, not allowed today (rule 0:00, no day, used 0:00:00); closing the app.
Blocked start ended: account kid1, Editor (…), budget used up: closed gracefully after 0.3 s; message shown.
Warning: account kid1, Editor (notepad.exe), early warning, 2 min left (budget); message shown.
Warning: account kid1, Editor (notepad.exe), final warning, 1 min left (budget); message shown (replaced the open message).
Warning: account kid1, break time 20:00–23:59 (Mo Tu We Th Fr Sa Su) begins at 20:00, final warning, 1 min left; message shown.
Warning: account kid1, Editor (notepad.exe), early warning, 2 min left (budget); message not shown (replaced in the same tick).
Grace period: account kid1, Editor (notepad.exe): the rules changed (not allowed today); closes in 1 min at 18:31:05; message shown.
Grace period: account kid1, break time 19:00–23:59 (Mo Tu We Th Fr Sa Su) begins at 19:00; 2 app(s) close in 1 min at 19:01:00; message shown.
Closing app: account kid1, Editor (notepad.exe, …), budget used up.          (…, after grace period.)
App closed: account kid1, Editor (notepad.exe, …), budget used up: closed gracefully after 0.4 s; message shown.
App closed: account kid1, Paint (…), break time 20:00–23:59 (Mo Tu We Th Fr Sa Su): terminated by force after 20 s (20.3 s until gone); message shown.
Purged the budget text of deleted account S-1-5-21-….
Warning: The time zone of the PC changed from W. Europe Standard Time (UTC+01:00) to Tokyo Standard Time (UTC+09:00, no DST); the local date changed from 10/12/2026 to 10/13/2026; daily budgets now use the new day; break times now use the new local time. Session in use: kid1.
Warning: The clock of the PC changed from … to …; the local date stays 10/12/2026; break times now use the new local time.
```

### Database (admin, read-only copy)

`%ProgramData%\EagleEye\EagleEye.Service.db`, schema version 5: `AppRules` (`AppId`, `BudgetMinutes`, `Days` bit mask Mo = 1 … Su = 64; **no row = 0:00, no day = blocked**), `AccountBudgetTexts` (no row = default text), `WarningSettings` (no row = 5 / 1 and default texts), `BlockedStarts` with `Kind` (`blocked start` / `running app`), `Reason` (`break time` / `not allowed today` / `budget used up`), `AfterGrace`, `Trigger` (`app start`, `found at agent start`, `break began`, `budget ran out`, `grace period ended`, `midnight`, `rules changed`), `BudgetMinutes`, `BudgetDays`, `UsedSeconds`; break rows keep the entry columns, budget rows have them `NULL`. The plan's Manual Verification Notes apply unchanged.

### DEV smoke check (Windows Developer Machine, 2026-10-12, non-elevated, next to Michael's running 0.5.0 service and tray client, both untouched)

Setup (D-15): copy of the US-005 smoke data (a real **version-4** database) in `artifacts/smoke-us006` (git-ignored). In that copy, the Admin account (this session) was unticked, its 6 app records were moved to `ee-anna` (standard account, ticked, never signed in), and 100 records "Testapp 001…100" were added for TI-18. Debug console service with `EAGLEEYE_DATA_DIR` and `EAGLEEYE_DEV_PORT_OFFSET=10000`, **without** `EAGLEEYE_DEV_WATCH_SID`, so no session is controlled and nothing can be closed. Debug parent app with `EAGLEEYE_DEV_APP_DATA_DIR=artifacts/smoke-us006-app` (copy of the US-005 pairing). Driven with UI Automation patterns only (no mouse, no keyboard).

| What | Result |
|---|---|
| Migration 5 on the v4 copy (AC-5, TI-4, TI-12) | Schema version 5; `App rules loaded: 0 rules of 106 apps of 1 accounts`; 6 old history rows → `blocked start` / `break time`; no `AppRules`, no `WarningSettings` row |
| Rules page (AC-1 to AC-3) | Section heading, explanation, two header rows, 106 rows sorted case-insensitively ("Battlefield™ 6", "EA", "EagleEye.ParentApp", …, "Testapp 001"…, "Visual Studio Code"), all "0" / "0", 742 checkboxes unticked; budget text box with the German default |
| Edit (AC-7, AC-8, AC-11, AC-17 pattern) | Hours `24` → "Bitte Stunden zwischen 0 und 23 eingeben." and back to 0; hours `1` → log `1:00, no day`; minutes `30` → `1:30` (combined with the stored hours); Monday and Saturday ticked → `1:30, Mo` and `1:30, Mo Sa`; `AppRules` row (90, 33) |
| Budget text (AC-10, ADR-016) | "Schluss für heute 😊 / 👍" → "budget text changed"; the English default typed → "budget text reset to the default", no `AccountBudgetTexts` row, the box shows the German default again |
| Settings (AC-13 to AC-16) | Rows "Frühe Warnung" 5 and "Finale Warnung" 1 with the German default texts; early 3 → stored; final 3 → "Die finale Warnung muss weniger Minuten …", back to 1; early 2 → stored; early text "Bald ist Schluss 🚀" → "early warning text changed"; final text emptied while default → nothing sent, default shown again |
| TI-18 (106 rows) | Rules page ready after switching from Settings in **545–584 ms** (3 runs, incl. the UI Automation search) → below 1 s, no `CollectionView` needed |
| Tray client | Debug tray connected to the console service: `Tray client connected: … (session 1, verified)` |
| CPU (console service, no controlled session) | 0.004 % of total CPU over 60 s (20 logical CPUs), 32 MB private. Not representative for AC-38 (no agent, no running apps): see §7 D |
| Not done | Enforcement in a session (D-15), 150 % / light mode, a second parent app, English Windows UI, installers (admin), SYSTEM agent |

Clean-up: all processes started by DEV were stopped; Michael's service, tray client and parent app data were not touched. Left behind (git-ignored): `02_Implementation/artifacts/smoke-us006*`, `smoke-us006-console*.txt`, `artifacts/publish`.

---

## 9. Machine(s) Used

| Step | Machine |
|---|---|
| Steps 0 to 12 | Windows Developer Machine |
| MacBook | Not used. `EagleEye.Shared` (incl. the new `DefaultTexts` resource and its `en` satellite) and `EagleEye.ParentApp.Core` contain no Windows APIs. The new XAML is plain MAUI, and the `CheckBox` mapping stays `#if WINDOWS`. Recommended by ARC at the next MacBook session after the merge: `git pull`, then `pwsh 02_Implementation/scripts/build.ps1` and `pwsh 02_Implementation/scripts/test.ps1`. A failure there is a regression of this story (an issue for DEV). |

---

## 10. Addendum 2026-10-17: patch 0.6.1 (ISSUE-008)

**Defect**: in the Windows parent app, the hours and minutes fields of "Applikations-Regeln" and the minute fields of "Zeit-Ende Warnungen" showed no value (`docs/testing/US-006/issues/ISSUE-008.md`).

**Cause**: `HorizontalTextAlignment="End"` (WinUI `TextAlignment.Right`) in the narrow Entry fields (`WidthRequest="56"`). WinUI draws the right-aligned text in the clipped part of the TextBox. Proven by one-attribute experiments with screenshots. My smoke check of 2026-10-12 read the values through UI Automation, which reports values that are not drawn (lesson: check fields visually with a screenshot).

**Fix**: the `End` alignment was removed from the four Entry types (`Views/SettingsView.xaml`, `Views/RulesView.xaml`); width and column layout are unchanged. Version 0.6.1 (`Directory.Build.props`). Evidence: `docs/testing/US-006/issues/ISSUE-008-after-*.png` (dark and light mode, 200 %).

**Deviations from the 0.6.0 report**:
- D-17: 150 % scaling not checked by DEV (this PC now runs at 200 %); to be checked on `LEOSERV`.
- D-18: **no 0.6.1 installers yet.** The NuGet audit advisory GHSA-334f-vvp4-7g6p (`Microsoft.AspNetCore.SignalR.Common` 10.0.12, published after 2026-10-12) makes the restore fail with warnings as errors. A patched package needs internet access, or the advisory needs a suppression; Michael decides. Build and tests were run locally with `NuGetAudit=false` in the shell environment only: 0 warnings, 2 623 tests passed. New SHA-256 hashes follow when the installers are built.

**Reinstall** (once built): the parent app (`EagleEye-ParentApp-Setup-0.6.1.exe`) on every parent PC. The service and tray client code is unchanged; following the 0.4.1 precedent, the service installer gets the same version, but reinstalling it is optional.

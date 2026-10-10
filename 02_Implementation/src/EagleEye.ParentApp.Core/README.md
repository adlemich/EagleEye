# EagleEye.ParentApp.Core

Plain `net10.0` class library with all parent-app logic that does not need MAUI (ADR-009). It builds and is unit-tested on both machines without a MAUI workload (`tests/EagleEye.ParentApp.Tests`).

Dependencies: `EagleEye.ParentApp → EagleEye.ParentApp.Core → EagleEye.Shared`. Core is part of the ParentApp component; Service and TrayClient never reference it.

## Structure

```
EagleEye.ParentApp.Core/
├── Abstractions/        Implemented by the MAUI head: ISecretStore, ISecretProtector, IThemeService,
│                        IDialogService, IUiDispatcher, IAppDataPaths, ThemeMode
├── Communication/
│   ├── HostAddress              Validates "hostname or IP" input; builds https://<host>:5443/hubs/parent
│   ├── CertificateTrustPolicy   Trust on first use while pairing, then the pinned SHA-256 thumbprint
│   ├── IParentHubClient /       SignalR wrapper (trust callback on HTTP handler and WebSocket,
│   │   ParentHubClient          Bearer token, automatic reconnect for paired connections)
│   ├── ParentHubClientFactory   Lets the coordinator be tested with mocked clients
│   ├── ParentReconnectPolicy    Shared ReconnectSchedule (0, 2, 10, then every 30 s)
│   ├── ConnectionCoordinator    State machine: NotPaired, PairingConnecting, AwaitingCode,
│   │                            PairedConnecting, PairedConnected, PairedDisconnected;
│   │                            reports confirmed/lost connections to the gateway
│   ├── ParentHubGateway         Current confirmed connection for feature models: Connected/Disconnected,
│   │                            forwarded broadcasts, InvokeAsync with timeout (ADR-010)
│   └── StateReplica<T>          Applies a snapshot only if its revision is higher (ADR-010 §4)
├── Accounts/            UserAccountsModel: state area "UserAccounts" (fetch on connect, writes with
│                        requestId/ack/broadcast confirmation, 4 s write timeout)
├── Reports/             AccountUsageModel: usage areas of the selected account (fetch on connect and on
│                        selection, revision rule per day, 90-day and empty-day trimming)
├── Rules/               US-005: AccountRulesModel (area "AccountRules:{sid}" of the selected account, field-level
│                        writes confirmed by snapshot, 4 s timeout), TimeOfDayText (7:30, 730, 7, 20.00 → HH:MM),
│                        BreakTimeEditRules (end > start, ≥ 1 day, ≤ 20 entries)
├── Appearance/         HeadingBarPalette: heading bar colours (platform accent or app primary #1E7B3A,
│                        white or black text by WCAG contrast, ISSUE-007); RgbColor
├── Data/
│   ├── ParentDatabase           Shared SqliteDatabase: ServerConnections, AppSettings, Secrets
│   ├── PairingStore             The one pairing (host, device ID/name, thumbprint); token via ISecretStore
│   ├── SettingsStore            appearance.theme = light | dark (absent = follow the OS)
│   └── ProtectedSecretStore     ISecretStore on the Secrets table + ISecretProtector (Windows: DPAPI)
├── ViewModels/          MainViewModel, AppearanceViewModel, ServerConnectionViewModel, UserAccountsViewModel,
│                        UserAccountItemViewModel, AccountDisplayName, StatusBarViewModel, ReportsViewModel,
│                        DayUsageViewModel, AppUsageRowViewModel, UsageDuration, ControlledAccountSelection
│                        (account picker of Reports and Rules), RulesViewModel, BreakTimeRowViewModel (US-005:
│                        rows merged by entry id; typed or pending fields are never overwritten by snapshots)
│                        (CommunityToolkit.Mvvm, no source generators)
├── AppTexts.cs          Typed access to the localized texts
└── Resources/           AppTexts.resx (German, default) and AppTexts.en.resx (English)
```

## Rules

- The coordinator reports `PairedConnected` (green) only after the service confirmed the pairing for the current connection (`GetPairingStatus`, US-002 AC-20).
- The token never appears in `ConnectionState`, in `StoredPairing.ToString()` or in any log.
- View models raise property changes on the UI thread: coordinator events are marshalled through `IUiDispatcher`, and view-model code does not use `ConfigureAwait(false)`.
- Feature models never poll and never treat their replica as authoritative; snapshot updates never go through the user-input path of a view model (coding guidelines §7.5).
- Every class except `ParentHubClient` (real SignalR, verified manually) has 100 % line and branch coverage.

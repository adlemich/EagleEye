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
│   └── ConnectionCoordinator    State machine: NotPaired, PairingConnecting, AwaitingCode,
│                                PairedConnecting, PairedConnected, PairedDisconnected
├── Data/
│   ├── ParentDatabase           Shared SqliteDatabase: ServerConnections, AppSettings, Secrets
│   ├── PairingStore             The one pairing (host, device ID/name, thumbprint); token via ISecretStore
│   ├── SettingsStore            appearance.theme = light | dark (absent = follow the OS)
│   └── ProtectedSecretStore     ISecretStore on the Secrets table + ISecretProtector (Windows: DPAPI)
├── ViewModels/          MainViewModel, AppearanceViewModel, ServerConnectionViewModel,
│                        StatusBarViewModel (CommunityToolkit.Mvvm, no source generators)
├── AppTexts.cs          Typed access to the localized texts
└── Resources/           AppTexts.resx (German, default) and AppTexts.en.resx (English)
```

## Rules

- The coordinator reports `PairedConnected` (green) only after the service confirmed the pairing for the current connection (`GetPairingStatus`, US-002 AC-20).
- The token never appears in `ConnectionState`, in `StoredPairing.ToString()` or in any log.
- View models raise property changes on the UI thread: coordinator events are marshalled through `IUiDispatcher`, and view-model code does not use `ConfigureAwait(false)`.
- Every class except `ParentHubClient` (real SignalR, verified manually) has 100 % line and branch coverage.

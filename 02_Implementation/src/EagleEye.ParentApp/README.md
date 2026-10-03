# EagleEye.ParentApp

Cross-platform parent application built with .NET MAUI. One codebase targets Android and iOS (primary production platforms), Windows (copy-deployed exe, used for initial testing) and macOS.

**Build hosts (ADR-007)**: Windows and Android targets are built on the Windows developer machine; the macOS (Mac Catalyst) and iOS targets on the MacBook. The `.csproj` selects target frameworks by host OS.

## Responsibility

- Allow parents to configure app rules (allowed/blocked apps per kid user)
- Allow parents to configure time budgets (hours:minutes per app per day)
- Allow parents to configure pause windows (blocked time per weekday per kid)
- Display real-time and historical usage statistics per kid
- Connect to `EagleEye.Service` via SignalR over local LAN (HTTPS, self-signed cert)
- Accept and trust the server's self-signed certificate automatically

## Platform Support

| Platform | Min Version | Built on | Distribution |
|----------|-------------|----------|--------------|
| Windows | Windows 11 | Windows machine | Copy a single .exe (no installer); used for initial testing |
| Android | Android 14 (API 34) | Windows machine | Sideloading (adb) |
| macOS | macOS 26 | MacBook | Direct .dmg download |
| iOS | iOS 26 | MacBook | Sideloading (Xcode / ios-deploy) |

## Component Structure

```
EagleEye.ParentApp/
├── Platforms/
│   ├── Windows/       Windows-specific code (App.xaml, Package.appxmanifest)
│   ├── Android/       Android-specific code (MainActivity, AndroidManifest.xml)
│   ├── MacCatalyst/   macOS-specific code (AppDelegate, Info.plist)
│   └── iOS/           iOS-specific code
├── ViewModels/        Shared MVVM view models
├── Views/             Shared MAUI UI pages and controls
└── Communication/     SignalR client — connects to EagleEye.Service on LAN
```

## Connection

The parent app connects to the Windows server via hostname or IP address (manually entered by the parent). The connection uses SignalR over HTTPS with the server's self-signed certificate trusted on first connection.

# EagleEye.ParentApp

Cross-platform parent application built with .NET MAUI. Targets iOS, Android, and macOS from a single codebase.

## Responsibility

- Allow parents to configure app rules (allowed/blocked apps per kid user)
- Allow parents to configure time budgets (hours:minutes per app per day)
- Allow parents to configure pause windows (blocked time per weekday per kid)
- Display real-time and historical usage statistics per kid
- Connect to `EagleEye.Service` via SignalR over local LAN (HTTPS, self-signed cert)
- Accept and trust the server's self-signed certificate automatically

## Platform Support

| Platform | Min Version | Distribution |
|----------|-------------|--------------|
| iOS | iOS 26 | Sideloading (Xcode / ios-deploy) |
| Android | Android 14 (API 34) | Sideloading (adb) |
| macOS | macOS 26 | Direct .dmg download |

## Component Structure

```
EagleEye.ParentApp/
├── Platforms/
│   ├── iOS/           iOS-specific code (AppDelegate, Info.plist, entitlements)
│   ├── Android/       Android-specific code (MainActivity, AndroidManifest.xml)
│   └── MacCatalyst/   macOS-specific code (AppDelegate, Info.plist)
├── ViewModels/        Shared MVVM view models
├── Views/             Shared MAUI UI pages and controls
└── Communication/     SignalR client — connects to EagleEye.Service on LAN
```

## Connection

The parent app connects to the Windows server via hostname or IP address (manually entered by the parent). The connection uses SignalR over HTTPS with the server's self-signed certificate trusted on first connection.

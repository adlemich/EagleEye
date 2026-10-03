# Implementation Report: US-001 — Basic Service Installation and Tray Client Connectivity

**Author**: DEV (run in the orchestrator session at Michael's request, following `agents/dev/CLAUDE.md`)
**Date**: 2026-10-03
**Machine(s) used**: Windows Developer Machine only (as assigned by the plan)
**Story status**: Implemented

---

## 1. Summary

US-001 is implemented as planned: API-first contracts in `EagleEye.Shared`, a Windows service hosting a SignalR tray hub, a WinForms tray client with a green/red connection icon and an About dialog that queries the server version live, unit tests, and a self-contained Inno Setup installer.

| Component | What was built |
|---|---|
| `EagleEye.Shared` | `Contracts/ITrayHub`, `Contracts/ITrayClientCallback` (empty placeholder), `Models/ServiceVersionDto`, `Constants/HubRoutes`, `Constants/ServiceDefaults` |
| `EagleEye.Service` | `Program` (generic host + `UseWindowsService` + Kestrel + SignalR), `Communication/TrayHub`, `IVersionProvider`, `AssemblyVersionProvider` |
| `EagleEye.TrayClient` | `Program`, `Communication/` (`IServiceConnection`, `ServiceConnection`, `ServiceReconnectPolicy`, `ConnectBackoff`), `UI/` (`TrayApplicationContext`, `AboutDialog`, `TrayIcons`, `TrayTexts`, icon resources) |
| Installer | `installer/windows/setup.iss` completed; `scripts/package-windows.ps1` implemented |
| Build | Version `0.1.0` added to `Directory.Build.props` |

**Build / test result**: `build.ps1` passes with 0 warnings and 0 errors. `test.ps1` passes all 34 unit tests (Shared 3, Service 15, TrayClient 16).

---

## 2. Deviations from the Implementation Plan

| # | Plan | Implemented | Why |
|---|---|---|---|
| D-1 | Kestrel listens on `0.0.0.0:5080` | Listens on **localhost only** (`ListenLocalhost(5080)`) | No remote client is in scope for US-001. Binding all interfaces would expose an unauthenticated, unencrypted hub on the LAN. Remote binding arrives with TLS and pairing in the parent-app story. Verified: the hub is not reachable via the machine's hostname. |
| D-2 | `WithAutomaticReconnect()` with default intervals, assumed to "repeat 30 s" | Custom `ServiceReconnectPolicy` (0 s, 2 s, 10 s, then every 30 s forever). Additionally, `Closed` restarts the connect loop. | The SignalR default policy gives up after 4 attempts and then closes the connection. With it, the tray would stay red forever if the service was stopped for more than about 42 s (AC-9, FR-TRAY-042). The connect back-off moved into `ConnectBackoff` so both can be unit-tested. |
| D-3 | `EagleEye.TrayClient.Tests` stays empty | Contains tests for `ServiceReconnectPolicy` and `ConnectBackoff` | Both are pure logic, and D-2 made them critical for AC-9. Testing them costs nothing and needs no WinForms. |
| D-4 | `AssemblyVersionProvider` reads "the assembly's" version | Constructor takes the `Assembly`; parsing is an `internal static Format()` | Makes the fallback branch (`EagleEye_v0.0`) testable. This is needed for 100 % branch coverage. Also handles the `+<commit>` suffix the .NET SDK appends to the informational version. |
| D-5 | `ServiceDefaults.LocalBaseUrl = $"http://localhost:{ServicePort}"` | String literal `"http://localhost:5080"` | A C# `const` cannot be interpolated from an `int` constant. |
| D-6 | Install to `{autopf}\EagleEye` | `{autopf}\EagleEye\Service\` and `{autopf}\EagleEye\TrayClient\` | Both executables are published **self-contained** (no .NET runtime prerequisite for users). Two self-contained apps in one folder would overwrite each other's runtime files. |
| D-7 | Service registered via `[Run] sc.exe create` | Registered in `[Code]` (post-install) with `sc create` / `sc config`, `sc failure`, `net start`. Uninstall stops the service and the tray client, then runs `sc delete`. Upgrades stop both first. | Lets the binary path be quoted correctly (the path contains spaces, so an unquoted service path would be a security issue). Also handles upgrade/re-install where the service already exists. |
| D-8 | Tray auto-start as an optional installer task | Always installed (HKLM `Run` value `EagleEyeTrayClient`) | AC-5 requires it. The old stub's optional task also used an invalid Inno flag. |
| D-9 | — | Installer languages German (default) + English; optional "Start the EagleEye tray icon now" checkbox at the end (unchecked); Start-menu shortcut "EagleEye Tray" | German default per NFR-L-011. The shortcut lets a user restart a tray client that was closed. |
| D-10 | — | `coverlet.collector` added to the test projects | Needed to measure the 100 % branch-coverage rule. |

---

## 3. Files Created or Modified

**Created**

- `02_Implementation/src/EagleEye.Shared/Constants/HubRoutes.cs`
- `02_Implementation/src/EagleEye.Shared/Constants/ServiceDefaults.cs`
- `02_Implementation/src/EagleEye.Shared/Contracts/ITrayHub.cs`
- `02_Implementation/src/EagleEye.Shared/Contracts/ITrayClientCallback.cs`
- `02_Implementation/src/EagleEye.Shared/Models/ServiceVersionDto.cs`
- `02_Implementation/src/EagleEye.Service/Program.cs`
- `02_Implementation/src/EagleEye.Service/IVersionProvider.cs`
- `02_Implementation/src/EagleEye.Service/AssemblyVersionProvider.cs`
- `02_Implementation/src/EagleEye.Service/Communication/TrayHub.cs`
- `02_Implementation/src/EagleEye.TrayClient/Program.cs`
- `02_Implementation/src/EagleEye.TrayClient/Communication/IServiceConnection.cs`
- `02_Implementation/src/EagleEye.TrayClient/Communication/ServiceConnection.cs`
- `02_Implementation/src/EagleEye.TrayClient/Communication/ServiceReconnectPolicy.cs`
- `02_Implementation/src/EagleEye.TrayClient/Communication/ConnectBackoff.cs`
- `02_Implementation/src/EagleEye.TrayClient/UI/TrayApplicationContext.cs`
- `02_Implementation/src/EagleEye.TrayClient/UI/AboutDialog.cs`
- `02_Implementation/src/EagleEye.TrayClient/UI/TrayIcons.cs`
- `02_Implementation/src/EagleEye.TrayClient/UI/TrayTexts.cs`
- `02_Implementation/src/EagleEye.TrayClient/UI/Resources/eagleeye.ico`, `eagleeye-connected.ico`, `eagleeye-disconnected.ico` (16/32/48 px, 32-bit)
- `02_Implementation/tests/EagleEye.Shared.Tests/Models/ServiceVersionDtoTests.cs`
- `02_Implementation/tests/EagleEye.Service.Tests/AssemblyVersionProviderTests.cs`
- `02_Implementation/tests/EagleEye.Service.Tests/Communication/TrayHubTests.cs`
- `02_Implementation/tests/EagleEye.TrayClient.Tests/Communication/ServiceReconnectPolicyTests.cs`
- `02_Implementation/tests/EagleEye.TrayClient.Tests/Communication/ConnectBackoffTests.cs`

**Modified**

- `02_Implementation/Directory.Build.props` (version 0.1.0)
- `02_Implementation/src/EagleEye.Shared/EagleEye.Shared.csproj`, `EagleEye.Service/EagleEye.Service.csproj` (Web SDK, WindowsServices 10.0.12), `EagleEye.TrayClient/EagleEye.TrayClient.csproj` (SignalR.Client 10.0.12, embedded icons)
- `02_Implementation/tests/EagleEye.{Shared,Service,TrayClient}.Tests/*.csproj` (xunit 2.9.3, xunit.runner.visualstudio 4.0.0, Microsoft.NET.Test.Sdk 18.10.1, Moq 4.21.0, coverlet.collector 10.1.0)
- `02_Implementation/installer/windows/setup.iss`
- `02_Implementation/scripts/package-windows.ps1`

---

## 4. Unit Test Coverage

| Class | Test class | Scenarios | Line / branch coverage |
|---|---|---|---|
| `ServiceVersionDto` | `ServiceVersionDtoTests` | construction, value equality, inequality | 100 % / 100 % |
| `AssemblyVersionProvider` | `AssemblyVersionProviderTests` | real assembly → `EagleEye_vX.Y`; assembly without attribute → fallback; null guard; valid formats (incl. `+sha`, `-beta`); null/empty/whitespace/invalid → fallback | 100 % / 100 % |
| `TrayHub` | `TrayHubTests` | version delegated to provider; connect logged; disconnect logged with and without exception | 100 % / 100 % |
| `ServiceReconnectPolicy` | `ServiceReconnectPolicyTests` | 0/2/10 s, then 30 s forever; null guard | 100 % / 100 % |
| `ConnectBackoff` | `ConnectBackoffTests` | 1/2/4/8/16 s, cap at 30 s incl. `int.MaxValue`; negative guard | 100 % / 100 % |

Not unit-tested, per the plan (verified manually): `TrayApplicationContext`, `AboutDialog`, `TrayIcons`, `ServiceConnection` (real SignalR connection), both `Program` classes, the installer.

**Smoke checks done by DEV (Windows Developer Machine, non-elevated):**

- The service in console mode listens on `http://localhost:5080`. The `/hubs/tray` negotiate succeeds; the endpoint is not reachable via the hostname (D-1).
- A SignalR round trip `GetServiceVersion` returns `EagleEye_v0.1`; the service logs the connect and disconnect.
- The **published self-contained** tray client starts while no service is running, connects automatically when the service starts, and keeps running when the service stops.
- **Not done:** actually running the installer, because the DEV session has no administrator rights. Installation, service registration and auto-start are the first things the manual test run must check.

---

## 5. Open Questions and Risks

- **Tray icon also appears in admin sessions.** The HKLM `Run` key applies to every user. The story does not require the icon in admin sessions and does not forbid it.
- **No single-instance guard.** Starting the tray client twice (e.g. via the Start-menu shortcut while it is already running) shows two icons. This is not in scope for US-001 and is a candidate for a later story.
- **No file logging yet** (planned for a later story). The service logs to the console when run interactively, and warnings and errors go to the Windows Event Log when it runs as a service.
- **Texts are English only.** They are centralised in `TrayTexts` but not yet localized (NFR-L-010). The installer itself is German/English.
- **Plain HTTP on localhost** as planned. TLS comes with remote connectivity.
- **Installer size of about 67 MB**, because both apps are self-contained.

---

## 6. How to Test

| Item | Value |
|---|---|
| Installer | `03_Delivery/windows/EagleEye-Setup-0.1.0.exe` (rebuild with `pwsh 02_Implementation/scripts/package-windows.ps1`) |
| Version shown by the service | `EagleEye_v0.1` |
| Requires | Windows 11 x64, an administrator account to install. No .NET runtime needed. |
| Install location | `C:\Program Files\EagleEye\Service\EagleEye.Service.exe`, `C:\Program Files\EagleEye\TrayClient\EagleEye.TrayClient.exe` |
| Windows service | Name `EagleEyeService`, display name `EagleEye Service`, Log On As `Local System`, startup type `Automatic`, recovery: restart after 5 s |
| Tray auto-start | `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Run` → value `EagleEyeTrayClient` |
| Tray icon | Green disc with a white eye = connected; red = disconnected. Tooltip `EagleEye — Connected` / `EagleEye — Disconnected`. Windows may hide new tray icons in the overflow (^) area. |
| Reconnect timing | After the service is restarted, the icon turns green within about 30 s (retries at 0, 2, 10, then every 30 s). |
| About | Right-click the tray icon → `About` → `Server Version: EagleEye_v0.1`; `Server Version: unavailable` while disconnected |
| Service port | `http://localhost:5080` (local only) |
| Diagnostics | Event Viewer → Windows Logs → Application (warnings/errors from the service). For full logs: stop the service, then run `EagleEye.Service.exe` from a console. |
| Uninstall | Settings → Apps → EagleEye → Uninstall (stops and removes the service and the tray client, removes the `Run` value) |

# EagleEye — Development Environments

*Status: Active (introduced 2026-10-03, see ADR-007)*
*Audience: Michael, Orchestrator, PRO, ARC, DEV, TES*
*Owner: Orchestrator*

> **Amendment 2026-10-20 (CR-001, ADR-019; written by ARC)**: the iOS and Android parent apps are dropped. The Windows machine has no Android target, APK, emulator, `maui-android` workload or Android SDK; the MacBook has no iOS target and needs Xcode for Mac Catalyst only. Both parent apps (Windows, macOS) are production platforms. DEV removed the Android target and the `-SkipAndroid` switch from the code and scripts (CR-001 §6.2). Added: MacBook lessons (repo outside iCloud Drive, Xcode version check).

EagleEye is developed on **two machines that share one git repository** (`https://github.com/adlemich/EagleEye.git`, branch `main`). Every agent must know which machine it is running on and only do work that belongs to that machine.

---

## 1. The Two Machines

| | Windows Developer Machine | MacBook |
|---|---|---|
| **OS** | Windows 11 Pro x64 | macOS 26+ (Apple Silicon) |
| **Role** | Primary dev machine **and the manual test station** | Dev machine for the macOS parent app (a production platform, ADR-019) |
| **Builds / develops** | `EagleEye.Shared`, `EagleEye.Service`, `EagleEye.TrayClient`, `EagleEye.ParentApp` (Windows target) | `EagleEye.Shared`, `EagleEye.ParentApp` (Mac Catalyst target) |
| **Unit tests run** | `Shared.Tests`, `Service.Tests`, `TrayClient.Tests`, `ParentApp.Tests` | `Shared.Tests`, `ParentApp.Tests` |
| **Packaging** | Windows installers (Inno Setup): service + tray, parent app | macOS `.dmg` (unsigned, un-notarised, ADR-019) |
| **Manual testing** | Service, TrayClient, Windows parent app | macOS parent app, connecting over the LAN to the service on the Windows machine |
| **Shell** | PowerShell 7.6 (`pwsh`), Git Bash available | PowerShell 7.6 (`pwsh`), zsh |

`EagleEye.Shared` is built on both machines; it is the only component that both machines change. Changes to `EagleEye.Shared/Contracts/` (API-first) should be made on the Windows machine, because that is where both ends of the contract (service and clients) can be built and tested together.

---

## 2. Detecting Where You Run

Every agent session must determine its host **before doing any work**:

1. **Claude Code environment block**: `Platform: win32` → Windows machine; `Platform: darwin` → MacBook.
2. **In PowerShell scripts**: use the automatic variables `$IsWindows` / `$IsMacOS`.
3. **Quick check**: run `pwsh 02_Implementation/scripts/env-check.ps1` — prints host, repo root, branch, sync state with `origin/main`, and which toolchains are available.

### If the task belongs to the other machine

Do not attempt it (for example, building Mac Catalyst on Windows or Inno Setup on macOS). Instead:

1. Tell Michael plainly: *"This step needs the MacBook / the Windows machine."*
2. Make sure everything needed for the handoff is committed (and pushed, if Michael wants that).
3. State exactly what to run on the other machine and which document to read there.

Documentation-only work (requirements, architecture, plans, test plans) can be done on either machine.

---

## 3. Paths and Scope

- **Never hard-code absolute paths** in instructions, docs, scripts or code. The repo lives in different places on each machine (e.g. `C:\Users\Admin\AppDevelopment\EagleEye` on Windows, `~/EagleEye` on the MacBook). Use paths relative to the repo root.
- **Repo root** = `git rev-parse --show-toplevel`. In scripts use `$PSScriptRoot`-relative paths.
- **Scope rule**: never read or write files outside the repo root (exception: the toolchains themselves, e.g. the .NET SDK).
- Use `Join-Path` in PowerShell; never concatenate paths with `\` or `/`.

---

## 4. Keeping Both Machines in Sync

Git is the only sync mechanism.

| When | Do |
|---|---|
| Start of a session | `git pull` (or at least `git status` / `env-check.ps1` to see whether `main` is behind) |
| Before switching machines | Commit, then push. Uncommitted work on one machine is invisible on the other. |
| Machine-specific files | Never committed: `secrets/secrets.json`, `.claude/settings.local.json`, `bin/`, `obj/`. Each machine has its own copy. |

**Line endings**: `.gitattributes` forces LF for all text files except `*.iss` (CRLF). Do not change `core.autocrlf` to work around this.

**Branching**: one feature branch per user story, `feature/US-XXX-<short-title>`, shared by both machines (push before switching, pull after switching). `main` only receives a story when it is closed. Full rules: `dev-process.md` §4.

---

## 5. Toolchain per Machine

### Windows Developer Machine

| Tool | Purpose |
|---|---|
| .NET SDK 10 | All builds |
| .NET workload `maui-windows` (or `maui`) | ParentApp Windows target |
| PowerShell 7.6 | All scripts |
| Inno Setup 6 (`iscc.exe`) | Windows installer |
| VSCode + C# Dev Kit + .NET MAUI extension | IDE |
| Docker or Podman | Local PlantUML server |

### MacBook

| Tool | Purpose |
|---|---|
| .NET SDK 10 + workload `maui-maccatalyst` (or `maui`) | ParentApp Mac Catalyst target |
| Xcode (latest) | Mac Catalyst toolchain only (no iOS target) |
| PowerShell 7.6 | All scripts |
| VSCode + C# Dev Kit + .NET MAUI extension | IDE |
| Docker or Podman | Local PlantUML server |

**MacBook lessons (known issues)**:

- **Keep the repo outside iCloud Drive**, e.g. `~/EagleEye`, not in `~/Documents` or `~/Desktop` when these sync with iCloud. iCloud sets the `com.apple.FinderInfo` extended attribute on files, and `codesign` then rejects every `.app` bundle ("resource fork, Finder information, or similar detritus not allowed"), including the ad-hoc signature of the Mac Catalyst build.
- **Xcode newer than the .NET Mac Catalyst pack expects**: with Xcode 27.1 installed, the .NET `maccatalyst` workload pack expected Xcode 27.0 and the build stopped at the Xcode version check. Setting the MSBuild property `ValidateXcodeVersion=false` worked around it. This is a known toolchain issue. **DEV decision (2026-10-20, CR-001 §6.2)**: the property is set in `EagleEye.ParentApp.csproj`, scoped to the `maccatalyst` TFM only, with a comment, so `build.ps1`, `package-macos.ps1` and the IDE all build the same way. Re-check after each Xcode or workload update and remove the property once the pack matches the installed Xcode.
- The Mac `.dmg` needs no Apple Developer account and no Apple secrets: it is not Developer-ID signed or notarised (ADR-019). Allow the app once in *System Settings → Privacy & Security → Open Anyway*.

---

## 6. Build and Test Scripts

All scripts in `02_Implementation/scripts/` detect the host and only touch the components that belong to it.

| Script | Windows | macOS |
|---|---|---|
| `env-check.ps1` | Host, repo state, tools | Host, repo state, tools |
| `build.ps1` | Shared, Service, TrayClient, ParentApp (Windows) | Shared, ParentApp (Mac Catalyst) |
| `test.ps1` | All four unit test projects | `Shared.Tests`, `ParentApp.Tests` |
| `package-windows.ps1` | Installers (service + tray, parent app) | refuses to run |
| `package-macos.ps1` | refuses to run | `.dmg` |
| `plantuml.ps1` | Start/stop the PlantUML server | Start/stop the PlantUML server |

The `EagleEye.ParentApp.csproj` selects its target frameworks by host OS (Windows: `net10.0-windows10.0.19041.0`; macOS: `net10.0-maccatalyst`). `Directory.Build.props` sets `EnableWindowsTargeting` so the full solution still loads in the IDE on macOS.

---

## 7. Testing Model

All acceptance and end-to-end testing is **manual**, executed by Michael, primarily on the Windows Developer Machine. TES prepares the test plans and checklists, and evaluates the results. See `02_Implementation/docs/testing/README.md`.

DEV still writes automated **unit tests**, which run via `test.ps1` on the machine that owns the component.

# EagleEye — Development Environments

*Status: Active (introduced 2026-10-03, see ADR-007)*
*Audience: Michael, Orchestrator, PRO, ARC, DEV, TES*
*Owner: Orchestrator*

EagleEye is developed on **two machines that share one git repository** (`https://github.com/adlemich/EagleEye.git`, branch `main`). Every agent must know which machine it is running on and only do work that belongs to that machine.

---

## 1. The Two Machines

| | Windows Developer Machine | MacBook |
|---|---|---|
| **OS** | Windows 11 Pro x64 | macOS 26+ (Apple Silicon) |
| **Role** | Primary dev machine **and the manual test station** | Dev machine for the macOS parent app only |
| **Builds / develops** | `EagleEye.Shared`, `EagleEye.Service`, `EagleEye.TrayClient`, `EagleEye.ParentApp` (Windows target), `EagleEye.ParentApp` (Android target) | `EagleEye.Shared`, `EagleEye.ParentApp` (Mac Catalyst and iOS targets) |
| **Unit tests run** | `Shared.Tests`, `Service.Tests`, `TrayClient.Tests`, `ParentApp.Tests` | `Shared.Tests`, `ParentApp.Tests` |
| **Packaging** | Windows installer (Inno Setup), Android APK | macOS `.dmg` |
| **Manual testing** | Service, TrayClient, Windows parent app, Android parent app (emulator or device) | macOS parent app, connecting over the LAN to the service on the Windows machine |
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

- **Never hard-code absolute paths** in instructions, docs, scripts or code. The repo lives in different places on each machine (e.g. `C:\Users\Admin\AppDevelopment\EagleEye` on Windows). Use paths relative to the repo root.
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
| .NET workloads `maui-windows`, `maui-android` (or `maui`) | ParentApp Windows + Android targets |
| Android SDK + emulator (or USB device) | Android parent app build and test |
| PowerShell 7.6 | All scripts |
| Inno Setup 6 (`iscc.exe`) | Windows installer |
| VSCode + C# Dev Kit + .NET MAUI extension | IDE |
| Docker or Podman | Local PlantUML server |

### MacBook

| Tool | Purpose |
|---|---|
| .NET SDK 10 + workload `maui-maccatalyst` (or `maui`) | ParentApp Mac Catalyst target |
| Xcode (latest) | Mac Catalyst / iOS toolchain |
| PowerShell 7.6 | All scripts |
| VSCode + C# Dev Kit + .NET MAUI extension | IDE |
| Docker or Podman | Local PlantUML server |

---

## 6. Build and Test Scripts

All scripts in `02_Implementation/scripts/` detect the host and only touch the components that belong to it.

| Script | Windows | macOS |
|---|---|---|
| `env-check.ps1` | Host, repo state, tools | Host, repo state, tools |
| `build.ps1` | Shared, Service, TrayClient, ParentApp (Windows + Android) | Shared, ParentApp (Mac Catalyst) |
| `test.ps1` | All four unit test projects | `Shared.Tests`, `ParentApp.Tests` |
| `package-windows.ps1` | Installer | refuses to run |
| `package-macos.ps1` | refuses to run | `.dmg` |
| `plantuml.ps1` | Start/stop the PlantUML server | Start/stop the PlantUML server |

The `EagleEye.ParentApp.csproj` selects its target frameworks by host OS (Windows: `net10.0-android`, `net10.0-windows10.0.19041.0`; macOS: `net10.0-maccatalyst`). `Directory.Build.props` sets `EnableWindowsTargeting` so the full solution still loads in the IDE on macOS.

---

## 7. Testing Model

All acceptance and end-to-end testing is **manual**, executed by Michael, primarily on the Windows Developer Machine. TES prepares the test plans and checklists, and evaluates the results. See `02_Implementation/docs/testing/README.md`.

DEV still writes automated **unit tests**, which run via `test.ps1` on the machine that owns the component.

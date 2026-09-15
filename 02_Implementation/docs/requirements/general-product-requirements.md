# EagleEye -- General Product Requirements

*Status: Approved v1.0*
*Approved: 2026-09-15 by Michael*
*Maintainer: PRO Agent*
*Date: 2026-09-15*

---

## Primary Input Documents

| Document | Location |
|----------|----------|
| Main intent and use cases | `01_Intend_and_Constraints/main_intend.md` |
| Clarifying Q&A | `01_Intend_and_Constraints/questions_and_answers.md` |
| Technology constraints | `01_Intend_and_Constraints/technology_selection.md` |
| Multi-agent process | `01_Intend_and_Constraints/multi_agent_system.md` |

---

## Table of Contents

1. [Product Overview and Goals](#1-product-overview-and-goals)
2. [Personas](#2-personas)
3. [Functional Requirements per Component](#3-functional-requirements-per-component)
4. [Main User Workflow](#4-main-user-workflow)
5. [Time Control Requirements](#5-time-control-requirements)
6. [Multi-User Requirements](#6-multi-user-requirements)
7. [Connection Requirements](#7-connection-requirements)
8. [Non-Functional Requirements](#8-non-functional-requirements)
9. [Platform Requirements](#9-platform-requirements)
10. [Out of Scope for Version 1](#10-out-of-scope-for-version-1)
11. [Open Questions](#11-open-questions)

---

## 1. Product Overview and Goals

### 1.1 Vision

EagleEye is a parental control solution that enables parents to monitor and control application usage on their children's Windows PCs. Parents manage rules remotely from a mobile phone or Mac, while the Windows service enforces those rules transparently and reliably.

### 1.2 Goals

| ID | Goal |
|----|------|
| G-1 | Give parents visibility into which applications a child uses and for how long. |
| G-2 | Let parents define which applications the child is allowed to use. |
| G-3 | Let parents set daily time budgets per application and per-weekday pause windows. |
| G-4 | Enforce budgets and pause windows automatically, preventing unauthorized usage. |
| G-5 | Provide timely notifications to the child before an application is about to be shut down. |
| G-6 | Support remote configuration and statistics viewing from iOS, Android, and macOS parent apps via the local LAN. |
| G-7 | Support multiple child accounts on a single Windows PC, each with independent rules. |

### 1.3 Success Criteria

- A parent can install EagleEye on a Windows PC, connect the parent app, configure rules, and have those rules enforced -- all without command-line interaction or manual certificate handling.
- A child receives a clear warning before an application's budget expires and cannot circumvent the enforcement.
- The system works reliably on a local LAN with no internet dependency.

---

## 2. Personas

### 2.1 Parent

| Attribute | Detail |
|-----------|--------|
| **Role** | Father, mother, grandparent, or other legal guardian responsible for the child. |
| **Windows account** | Local administrator account. Required for installing EagleEye and the applications the child may use. |
| **Devices** | iPhone (iOS 26+), Android phone (Android 14+), or MacBook (macOS 26+, Apple Silicon). |
| **Technical skill** | Can follow a guided installer and enter a hostname/IP address. Does not need developer or networking expertise. |
| **Needs** | (1) Control which apps the child may use. (2) Set time budgets and pause windows. (3) View usage statistics remotely. (4) Receive real-time event notifications (e.g. child attempts a blocked app). |
| **Constraints** | May not always be physically near the Windows PC. Manages rules from a mobile device on the same LAN (or via home VPN at the user's own setup). |

### 2.2 Kid

| Attribute | Detail |
|-----------|--------|
| **Role** | Child of the parent persona. Uses the Windows PC for games, school work, and general use. |
| **Windows account** | Local standard (non-admin) user account. Cannot install or remove software. |
| **Devices** | Windows 11 PC only (the monitored machine). |
| **Technical skill** | Basic PC user. Can log in, launch applications, and respond to on-screen notifications. |
| **Needs** | (1) Know how much time is left for an application. (2) Receive a warning before an application is shut down so they can save work or progress. |
| **Constraints** | Cannot modify EagleEye configuration or bypass enforcement. Has no admin privileges. |

---

## 3. Functional Requirements per Component

### 3.1 EagleEye.Service (Windows Service)

The service runs as a background Windows service under the SYSTEM account. It is the central authority for enforcement and data.

#### 3.1.1 Process Monitoring

| ID | Requirement |
|----|-------------|
| FR-SVC-010 | The service shall continuously monitor running processes for all standard (non-admin) local user accounts. |
| FR-SVC-011 | The service shall detect when a monitored user starts a new process and check it against the allow-list configured for that user. |
| FR-SVC-012 | The service shall track cumulative active usage time (in minutes) per allowed application per user per day. |
| FR-SVC-013 | For each detected process, the service shall attempt to resolve a human-readable display name. Resolution sources include (in priority order): (1) the executable's file version info / product name metadata, (2) the application's entry in Windows installed-programs registry, (3) an optional online lookup. |
| FR-SVC-014 | The service shall maintain a local dictionary mapping process executable names to their resolved display names. Once resolved, a display name is cached so repeated lookups are avoided. |

#### 3.1.2 Process Classification

The service classifies every process running in a standard user's session into one of three categories:

| Category | Description | Enforcement |
|----------|-------------|-------------|
| **Ignored** | Standard Windows processes and OS-level background processes that are essential for a functioning user session (e.g. `explorer.exe`, `dwm.exe`, `taskhostw.exe`, shell components). | Never terminated, never tracked. |
| **Allowed (controlled)** | Applications explicitly placed on the user's allow-list by the parent (e.g. Minecraft, a web browser). | Permitted to run; subject to time budget tracking and enforcement. |
| **Blocked** | Any process that is neither ignored nor on the allow-list. | Terminated on detection. |

| ID | Requirement |
|----|-------------|
| FR-SVC-015 | The service shall maintain a default ignore list of standard Windows processes that are required for normal user session operation. Ignored processes are never terminated and never tracked. |
| FR-SVC-016 | The ignore list shall be shipped with the installer and kept up to date by the development team. It is not configurable by the parent. |
| FR-SVC-017 | Only processes that are not on the ignore list are subject to allow-list evaluation and enforcement. |

#### 3.1.3 App Enforcement

| ID | Requirement |
|----|-------------|
| FR-SVC-020 | If a monitored user starts a process that is not ignored and not on their allow-list, the service shall terminate it. |
| FR-SVC-021 | Termination shall first attempt a graceful shutdown (e.g. `WM_CLOSE`) and wait for a configurable timeout (default: 30 seconds) before force-killing the process. The timeout is a general setting configurable by the parent. |
| FR-SVC-022 | When a time budget for an allowed application expires, the service shall terminate that application using the same graceful-then-force approach. |
| FR-SVC-023 | During a pause window, the service shall prevent all non-ignored application launches for the affected user (regardless of remaining budget). |

#### 3.1.4 Configuration Management

| ID | Requirement |
|----|-------------|
| FR-SVC-030 | The service shall persist its configuration locally on disk under the standard Windows application data folder (e.g. `%ProgramData%`). |
| FR-SVC-031 | The service shall accept configuration updates from connected parent apps remotely over the network. |
| FR-SVC-032 | Configuration includes: allowed applications list, per-app daily time budgets, and per-weekday pause windows -- all per user account. |
| FR-SVC-033 | Configuration data shall be stored in separate files per child user account. |

#### 3.1.5 Statistics Collection

| ID | Requirement |
|----|-------------|
| FR-SVC-040 | The service shall collect per-user, per-application daily usage statistics (minutes used). |
| FR-SVC-041 | The service shall serve collected statistics to parent apps on request. |
| FR-SVC-042 | Statistics shall be stored on disk under the standard Windows application data folder (e.g. `%ProgramData%`), in separate files per child user account. |
| FR-SVC-043 | Statistics shall be retained for 90 days at daily granularity (minutes of use per application per day). Data older than 90 days shall be automatically purged. |

#### 3.1.6 Remote Communication

| ID | Requirement |
|----|-------------|
| FR-SVC-050 | The service shall expose a network endpoint accessible to parent apps over the local network. |
| FR-SVC-051 | The service shall support three communication patterns: (1) request/response data queries, (2) server-to-client event pushes, (3) publish/subscribe to multiple connected clients. |
| FR-SVC-052 | The service shall push real-time events to connected parent apps (e.g. child started a blocked app, budget expired). |
| FR-SVC-053 | When multiple parent apps are connected simultaneously, all shall receive configuration and event updates in sync. |

#### 3.1.7 TLS Certificate Management

| ID | Requirement |
|----|-------------|
| FR-SVC-060 | On first run, the service shall auto-generate a self-signed TLS certificate for its network endpoint. |
| FR-SVC-061 | The certificate shall be non-expiring (or very long-lived). |
| FR-SVC-062 | Certificate generation and usage shall be fully transparent to the end user -- no manual steps required. |

#### 3.1.8 User Account Discovery

| ID | Requirement |
|----|-------------|
| FR-SVC-070 | The service shall auto-discover all local standard (non-admin) Windows user accounts. |
| FR-SVC-071 | Discovered accounts shall be presented to the parent app for configuration. Admin accounts shall be excluded. |

#### 3.1.9 Parent App Pairing (Onboarding)

| ID | Requirement |
|----|-------------|
| FR-SVC-090 | When an unknown parent app connects, the service shall generate a 6-digit numeric pairing code. |
| FR-SVC-091 | The pairing code shall be pushed to the tray client, which displays it as a popup notification to the user at the Windows PC. |
| FR-SVC-092 | If no tray client is connected (no kid session active), the service shall write the pairing code to the Windows Event Log as an informational entry. |
| FR-SVC-093 | The pairing code shall expire after 5 minutes. An expired code must be rejected. |
| FR-SVC-094 | During pairing, the parent app shall submit the code along with a user-chosen device name (e.g. "Mom's iPhone") to identify the client. |
| FR-SVC-095 | Upon successful pairing, the service stores the device name and a persistent credential. All subsequent connections from that client are authenticated using the stored credential. |
| FR-SVC-096 | Multiple parent apps may be paired simultaneously. |
| FR-SVC-097 | Any paired parent app may de-register any other paired parent app (or itself) by sending a de-registration command to the service. |
| FR-SVC-098 | The service shall reject all configuration changes and data queries from unpaired clients. |

#### 3.1.10 Installed Application Discovery

| ID | Requirement |
|----|-------------|
| FR-SVC-080 | The service shall auto-discover applications installed on the Windows PC and present the list to the parent app. |
| FR-SVC-081 | The parent shall be able to trigger a re-scan of installed applications from the parent app. A re-scan shall detect newly installed applications and remove entries for applications that have been uninstalled. |
| FR-SVC-082 | Discovered applications shall be presented using their human-readable display names (FR-SVC-013), with the executable name as fallback. |

#### 3.1.11 Logging

| ID | Requirement |
|----|-------------|
| FR-SVC-100 | The service shall write operational logs (enforcement actions, configuration changes, pairing events, errors) to log files in a dedicated subfolder under the application data folder. |
| FR-SVC-101 | By default, the service shall log at error, warning, and info levels. |
| FR-SVC-102 | The parent app shall be able to switch the service into debug mode, which additionally logs debug-level entries. |
| FR-SVC-103 | Log files shall use rotation: each file must not exceed 50 MB. Logs older than 5 days shall be automatically deleted. |

#### 3.1.12 Version Information

| ID | Requirement |
|----|-------------|
| FR-SVC-110 | The service shall expose a version identifier in the format `EagleEye_vMAJOR.MINOR` (e.g. `EagleEye_v0.1`). The version shall correlate to the installer version. |
| FR-SVC-111 | The version shall be queryable by connected clients (tray client and parent apps) via a live request. |

### 3.2 EagleEye.TrayClient (Windows Tray App)

The tray client is a lightweight executable that runs in the kid's user session. It connects to the local service on localhost.

| ID | Requirement |
|----|-------------|
| FR-TRAY-010 | The tray client shall display a system tray icon in the kid's session. |
| FR-TRAY-011 | Clicking the tray icon shall show a summary of remaining time budgets for the current user's allowed applications. |
| FR-TRAY-020 | The tray client shall display a notification when a time budget is about to expire (warning before shutdown). |
| FR-TRAY-021 | The tray client shall display a notification when an application is being shut down due to budget expiry. |
| FR-TRAY-030 | The tray client shall optionally display a topmost overlay window showing remaining time, visible even when applications are in focus. |
| FR-TRAY-040 | The tray client shall connect to `EagleEye.Service` on `localhost`. |
| FR-TRAY-041 | The tray client shall display a connection status indicator (red/green) showing whether it is connected to the service. |
| FR-TRAY-042 | If the service is unreachable, the tray client shall retry connecting continuously in the background. |
| FR-TRAY-050 | The tray client shall auto-start when the kid's user session begins. |
| FR-TRAY-060 | The tray client shall be packaged and installed together with `EagleEye.Service` in a single installer. |
| FR-TRAY-070 | The tray client shall display the 6-digit pairing code as a popup when requested by the service (FR-SVC-091). |

### 3.3 EagleEye.ParentApp (iOS, Android, macOS)

The parent app is a single MAUI codebase deployed to three platforms. It connects to `EagleEye.Service` over the local LAN.

#### 3.3.1 Connection

| ID | Requirement |
|----|-------------|
| FR-APP-010 | The parent app shall allow the user to enter the Windows machine's hostname or IP address to connect to the service. |
| FR-APP-011 | The parent app shall connect to the service over TLS (accepting the service's self-signed certificate). |
| FR-APP-012 | The parent app shall indicate connection status clearly (connected / disconnected / connecting). |
| FR-APP-013 | On first connection to an unpaired service, the parent app shall prompt the user to enter the 6-digit pairing code (FR-SVC-090) and a device name to identify this client. |
| FR-APP-014 | The parent app shall store the pairing credential locally so that subsequent connections are authenticated automatically. |
| FR-APP-015 | The parent app shall allow the user to view all paired devices and de-register any of them (FR-SVC-097). |

#### 3.3.2 User Account Management

| ID | Requirement |
|----|-------------|
| FR-APP-020 | The parent app shall display all discovered standard user accounts (kids) from the service. |
| FR-APP-021 | The parent shall be able to select a user account and manage its configuration. |

#### 3.3.3 Application Allow-List

| ID | Requirement |
|----|-------------|
| FR-APP-030 | The parent app shall display the list of installed applications as discovered by the service (FR-SVC-080), using their human-readable display names (FR-SVC-013). |
| FR-APP-031 | The parent shall be able to allow or block specific applications per user account. |
| FR-APP-032 | Where a display name could not be resolved, the parent app shall show the executable name as fallback so no application is invisible to the parent. |
| FR-APP-033 | The parent app shall provide a way to trigger a re-scan of installed applications on the Windows PC (FR-SVC-081). |

#### 3.3.4 Time Budget Configuration

| ID | Requirement |
|----|-------------|
| FR-APP-040 | The parent shall be able to set a daily time budget (in hours:minutes) per allowed application per user account. |
| FR-APP-041 | Budget configuration shall be per-weekday (Monday through Sunday), allowing different budgets for school days vs. weekends. |

#### 3.3.5 Pause Window Configuration

| ID | Requirement |
|----|-------------|
| FR-APP-050 | The parent shall be able to define pause windows per weekday per user account (e.g. blocked from 20:00 to 09:00). |
| FR-APP-051 | During a pause window, no applications are allowed for the affected user, regardless of remaining budget. |

#### 3.3.6 Statistics Viewing

| ID | Requirement |
|----|-------------|
| FR-APP-060 | The parent app shall display usage statistics per user, per application, per day. |
| FR-APP-061 | Statistics shall show actual minutes used vs. configured budget. |

#### 3.3.7 Event Notifications

| ID | Requirement |
|----|-------------|
| FR-APP-070 | The parent app shall display real-time events pushed by the service (e.g. blocked app attempt, budget expired, user logged in/out). |

#### 3.3.8 Platform UI

| ID | Requirement |
|----|-------------|
| FR-APP-080 | On iOS and Android, the app shall operate in portrait orientation only and follow platform-typical look and feel. |
| FR-APP-081 | On macOS, the app shall present a desktop-style UI. |
| FR-APP-082 | The app shall be functionally identical across all three platforms. |

### 3.4 Code Reuse

| ID | Requirement |
|----|-------------|
| FR-CR-010 | Common logic, data models, and communication contracts shared across components shall be implemented once and reused, to minimize long-term maintenance effort. |
| FR-CR-011 | The parent app shall share a single codebase across iOS, Android, and macOS. Only platform-specific UI code may differ. |

---

## 4. Main User Workflow

This describes the end-to-end workflow from installation to daily use.

### 4.1 Installation and Setup (Parent)

| Step | Actor | Action |
|------|-------|--------|
| 1 | Parent | Logs into Windows with a local administrator account. |
| 2 | Parent | Runs the EagleEye installer (Inno Setup). The installer installs both `EagleEye.Service` and `EagleEye.TrayClient`. |
| 3 | Parent | The installer registers `EagleEye.Service` as a Windows service running under the SYSTEM account. |
| 4 | Parent | The installer configures `EagleEye.TrayClient` to auto-start for standard user sessions. |
| 5 | Parent | Installs the applications the child is allowed to use (standard Windows application installation). |
| 6 | Parent | Creates a local standard (non-admin) Windows user account for the child (if not already existing). |
| 7 | Parent | Logs out of Windows. |

### 4.2 Parent App Setup

| Step | Actor | Action |
|------|-------|--------|
| 8 | Parent | Installs the EagleEye parent app on their mobile phone (iOS/Android) or MacBook. |
| 9 | Parent | Opens the parent app and enters the Windows machine's hostname (or IP address). |
| 10 | Parent | The app connects to `EagleEye.Service` over TLS on the local LAN. |

### 4.3 Configuration (Parent)

| Step | Actor | Action |
|------|-------|--------|
| 11 | Parent | Views the list of discovered standard user accounts (children). |
| 12 | Parent | Selects a child's account and configures the allow-list of applications. |
| 13 | Parent | Sets daily time budgets (hours:minutes) per allowed application, per weekday. |
| 14 | Parent | Defines pause windows per weekday (e.g. 20:00--09:00 blocked). |
| 15 | Parent | Configuration is sent to the service and persisted. |

### 4.4 Daily Use (Kid)

| Step | Actor | Action |
|------|-------|--------|
| 16 | Kid | Logs into Windows with their standard user account. |
| 17 | System | `EagleEye.TrayClient` auto-starts in the kid's session and connects to the service on localhost. |
| 18 | Kid | Tries to start an application. |
| 19 | System | If the application is not on the allow-list, the service terminates it (graceful then force-kill). |
| 20 | System | If the application is allowed, it launches normally. The service tracks usage time. |
| 21 | System | When remaining budget is low, the tray client displays a warning notification. |
| 22 | System | When the budget expires, the service terminates the application. The tray client notifies the kid. |
| 23 | System | During a pause window, all application launches are blocked for the kid. |

### 4.5 Budget Reset

| Step | Actor | Action |
|------|-------|--------|
| 24 | System | At midnight, all daily time budgets reset to their configured values. Unused budget does not carry over. |

---

## 5. Time Control Requirements

### 5.1 Pause Windows

| ID | Requirement |
|----|-------------|
| TC-010 | Pause windows are defined per weekday (Monday through Sunday) per user account. |
| TC-011 | A pause window specifies a start time and end time (e.g. 20:00--09:00). |
| TC-012 | During an active pause window, no applications are allowed for the affected user, regardless of remaining time budget. |
| TC-013 | The service shall enforce pause windows even if no parent app is connected. |

### 5.2 Time Budgets

| ID | Requirement |
|----|-------------|
| TC-020 | Time budgets are configured per application, per user account, per weekday. |
| TC-021 | The minimum granularity for budget configuration is one minute. |
| TC-022 | Budget countdown only occurs during non-pause (active) time. Time spent during a pause window does not consume budget. |
| TC-023 | Unused daily budget does not carry over to the next day. |
| TC-024 | All daily budgets reset at midnight. |

### 5.3 Budget Expiry Warning and Shutdown

| ID | Requirement |
|----|-------------|
| TC-030 | Before a budget expires, the system shall warn the kid via the tray client using two warning levels: an **early warning** (default: 5 minutes before shutdown) and a **last warning** (default: 1 minute before shutdown). |
| TC-031 | Both budget warning thresholds are configurable by the parent via the parent app. |
| TC-032 | When a budget reaches zero, the service terminates the application using the graceful-then-force approach (FR-SVC-021). |

### 5.4 Pause Window Warning and Shutdown

| ID | Requirement |
|----|-------------|
| TC-040 | Before a pause window begins, the system shall warn the kid via the tray client using two warning levels: an **early warning** (default: 5 minutes before pause) and a **last warning** (default: 1 minute before pause). |
| TC-041 | Both pause window warning thresholds are configurable by the parent via the parent app, independently from the budget expiry warning thresholds (TC-031). |
| TC-042 | When a pause window begins, all running non-ignored applications for the affected user are terminated using the graceful-then-force approach (FR-SVC-021). |

---

## 6. Multi-User Requirements

| ID | Requirement |
|----|-------------|
| MU-010 | The service shall support multiple standard (non-admin) user accounts on the same Windows PC simultaneously. |
| MU-011 | Each user account has its own independent configuration: allow-list, time budgets, pause windows. |
| MU-012 | Each user account has its own independent usage statistics. |
| MU-013 | The service auto-discovers standard user accounts (FR-SVC-070). No manual account registration is needed. |
| MU-014 | Admin accounts are excluded from monitoring and enforcement. |

---

## 7. Connection Requirements

| ID | Requirement |
|----|-------------|
| CN-010 | Communication between parent apps and the service occurs over the local LAN. |
| CN-011 | The parent app connects by entering the Windows machine's hostname (preferred) or IP address. |
| CN-012 | All network connections are encrypted via TLS using the service's self-signed certificate. |
| CN-013 | Certificate handling is fully transparent -- no manual user action required for TLS to work. |
| CN-014 | Multiple parent app instances may connect to the service simultaneously. All receive synchronized updates. |
| CN-015 | The tray client connects to the service on `localhost` (same machine). |
| CN-016 | The system shall function entirely on the local LAN with no internet dependency. |
| CN-017 | The architecture shall not preclude future remote access, but no remote-access implementation is required for v1. |

---

## 8. Non-Functional Requirements

### 8.1 Performance

| ID | Requirement |
|----|-------------|
| NFR-P-010 | Process monitoring and enforcement shall operate with minimal CPU and memory overhead so as not to degrade the child's application experience (especially games). |
| NFR-P-011 | The tray client overlay shall not cause noticeable frame drops in windowed or borderless-windowed applications. |
| NFR-P-012 | Communication latency on the local LAN shall be low enough that configuration changes and events appear near-instantaneous to the parent. |

### 8.2 Security

| ID | Requirement |
|----|-------------|
| NFR-S-010 | The child (standard user) shall not be able to stop, disable, or uninstall `EagleEye.Service`. |
| NFR-S-011 | The child shall not be able to terminate or close `EagleEye.TrayClient` in a way that prevents enforcement. |
| NFR-S-012 | Configuration changes shall only be accepted from authenticated parent app connections, not from the tray client. |
| NFR-S-013 | All network communication shall be TLS-encrypted (CN-012). |
| NFR-S-014 | Credentials and secrets shall never be stored in source control, logs, or displayed in any UI. |

### 8.3 Usability

| ID | Requirement |
|----|-------------|
| NFR-U-010 | The Windows installer shall provide a guided wizard experience (install, repair, uninstall). |
| NFR-U-011 | The parent app shall be usable without technical networking knowledge beyond knowing the PC's hostname. |
| NFR-U-012 | Notifications to the kid shall be clear and non-intrusive, giving adequate time to react (save work). |
| NFR-U-013 | The tray client remaining-time display shall be easily readable at a glance. |

### 8.4 Localization

| ID | Requirement |
|----|-------------|
| NFR-L-010 | The tray client and all parent apps (iOS, Android, macOS) shall support multiple languages. |
| NFR-L-011 | The initial languages to support are German (default) and English. |
| NFR-L-012 | All user-facing text (labels, notifications, messages, error texts) shall be externalised for translation rather than hard-coded. |

### 8.5 Reliability

| ID | Requirement |
|----|-------------|
| NFR-R-010 | `EagleEye.Service` shall start automatically on Windows boot and survive user session logon/logoff cycles. |
| NFR-R-011 | Enforcement shall continue even when no parent app is connected (rules are persisted locally). |
| NFR-R-012 | If the tray client crashes or is terminated, the service shall continue enforcing rules. The tray client should attempt to restart automatically. |
| NFR-R-013 | Budget tracking shall be persistent across service restarts (no loss of tracked time). |

---

## 9. Platform Requirements

### 9.1 Windows (Service + TrayClient)

| Attribute | Requirement |
|-----------|-------------|
| **OS** | Windows 11 (x64) -- Home and Pro editions |
| **Runtime** | .NET 10 |
| **Service account** | SYSTEM |
| **Installer** | Inno Setup -- provides install, repair, uninstall wizard |
| **Components installed** | `EagleEye.Service` (Windows service) and `EagleEye.TrayClient` (user-session tray app) |

### 9.2 iOS (ParentApp)

| Attribute | Requirement |
|-----------|-------------|
| **OS** | iOS 26 and newer |
| **Framework** | .NET MAUI |
| **Orientation** | Portrait only |
| **Distribution** | Manual sideloading (Xcode / ios-deploy). No App Store for v1. |

### 9.3 Android (ParentApp)

| Attribute | Requirement |
|-----------|-------------|
| **OS** | Android 14 and newer |
| **Framework** | .NET MAUI |
| **Orientation** | Portrait only |
| **Distribution** | Manual sideloading (adb). No Play Store for v1. |

### 9.4 macOS (ParentApp)

| Attribute | Requirement |
|-----------|-------------|
| **OS** | macOS 26 and newer |
| **Architecture** | Apple Silicon |
| **Framework** | .NET MAUI (Mac Catalyst) |
| **UI** | Desktop-style (not portrait-locked) |
| **Distribution** | Direct `.dmg` distribution. No Mac App Store for v1. |

---

## 10. Out of Scope for Version 1

The following are explicitly **not** included in the initial version of EagleEye:

| Item | Notes |
|------|-------|
| **Remote access (WAN)** | Parent app connects via local LAN only. Users may set up their own VPN. Architecture should not block future remote access. |
| **Full-screen DirectX overlay** | The tray client overlay uses a topmost window. True DirectX/game overlay injection is deferred. |
| **App Store / Play Store distribution** | iOS and Android apps are sideloaded. Store distribution comes when the product matures. |
| **Mac App Store distribution** | macOS app is distributed as `.dmg`. |
| **Web browser content filtering** | EagleEye controls which applications run, not what content is accessed within an allowed browser. |
| **Internet-based account system** | No cloud accounts, no user registration. All data is local. |
| **CI/CD pipeline** | Builds and tests are run via local PowerShell scripts. No hosted CI/CD. |
| **iOS and Android app development** | Development starts with Windows service + macOS parent app. iOS and Android apps are built after the core product matures. |
| **Budget carry-over** | Unused daily budget does not carry over. This is by design, not a deferral. |
| **Per-app pause windows** | Pause windows apply to all apps for a user. Per-app pause windows are not planned. |

---

## 11. Open Questions

All open questions have been resolved. None remaining.

---

*End of General Product Requirements -- Approved v1.0*

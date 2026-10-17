# EagleEye -- General Product Requirements

*Status: Approved v1.6 (v1.0 approved 2026-09-20; v1.1 approved 2026-10-03; v1.2 approved by Michael 2026-10-04; v1.3 approved by Michael 2026-10-07; v1.4 approved by Michael 2026-10-07; v1.5 approved by Michael 2026-10-10; v1.6 approved by Michael 2026-10-12)*
*Approved: v1.0 by Michael*
*Maintainer: PRO Agent*
*Date: 2026-09-15, amended 2026-10-03, 2026-10-04, 2026-10-07, 2026-10-10, 2026-10-12*

> **v1.6 amendment (2026-10-12, directed by Michael, with US-006; approved by Michael 2026-10-12)**: per-app **daily time budgets** replace the allow-list. For every app in an account's inventory, the parent sets one daily budget (0:00 to 23:59) and the weekdays on which the app may be used. Apps found for the first time are blocked (0:00, no day ticked). A start of an app that is not allowed today or whose budget is used up is blocked like a break-time block, with a budget text per account. Two fixed **warning levels** (early, default 5 min; final, default 1 min; 1..59 min, early > final), each with a text, are set once for all accounts. They apply both before a budget runs out and before a break time begins. When a budget runs out or a break begins, running apps are closed gracefully and, after 20 s, by force. A parent change never leaves a running app less than the final-warning minutes (grace period). The default break display text gets an English version. Time-zone and clock changes that affect budgets or the day boundary are detected and logged as Warnings. Changed: §3.1.2, FR-SVC-011, FR-SVC-020, FR-SVC-021 (note), FR-SVC-022, FR-SVC-024, FR-SVC-025, FR-SVC-032, FR-SVC-017, FR-SVC-047, FR-TRAY-020, FR-TRAY-021, §3.3.3 (FR-APP-030, FR-APP-031, FR-APP-033), FR-APP-040, FR-APP-041, FR-APP-052, FR-APP-053 (new), FR-APP-054 (new), §4.3 steps 12 and 13, §4.4 steps 19 to 23, TC-020, TC-025 (new), TC-030, TC-031, TC-032, TC-033 (new), TC-040, TC-041, TC-042, MU-011, TC-024.

> **v1.5 amendment (2026-10-10, directed by Michael, with US-005; approved by Michael 2026-10-10)**: pause windows become **break times** ("Ruhezeiten"), configured per account on a "Rules" page as a list of entries (on/off, start time, end time within one day, weekdays), plus one display text per account. When an app is started (its first process created) during a break time, the service detects it within 10 seconds, closes it gracefully and, after 20 seconds, by force (at most 30 seconds from start to gone), records the blocked start in a history, and the tray client shows the display text in a topmost dialog. Changes of the time zone are logged and stored as findings (FR-SVC-026, added with the US-005 plan answers on 2026-10-10). Ending apps that are already running when a break time begins (TC-042) and the warnings (TC-040, TC-041) stay for a later story. Changed: FR-SVC-023, FR-SVC-024 (new), FR-SVC-025 (new), FR-SVC-026 (new), FR-SVC-032, FR-SVC-043, FR-SVC-047, FR-TRAY-022 (new), FR-APP-050, FR-APP-052 (new), TC-010, TC-011, TC-014 (new), §4.3 step 14.

> **v1.4 amendment (2026-10-07, directed by Michael, with US-004; approved by Michael 2026-10-07)**: the service records the **apps** (Task Manager's "Apps" group, not background processes) started in the sessions of accounts under parental control: a permanent app inventory per account, a start/end history, and the daily usage **in seconds**, updated at least every 5 seconds. The parent app shows the usage per account and day as HH:MM. Changed: FR-SVC-012, FR-SVC-040, FR-SVC-041, FR-SVC-043, FR-SVC-044 to FR-SVC-047 (new), FR-APP-060.

> **v1.3 amendment (2026-10-07, directed by Michael, with US-003)**: the service no longer monitors all standard accounts automatically. It keeps an inventory of the standard accounts, and the **parent selects which accounts are under parental control**. Only those are monitored. Changed: FR-SVC-010, FR-SVC-070, FR-SVC-071, FR-SVC-072 to FR-SVC-074 (new), FR-APP-020, FR-APP-022 (new), MU-013, §4.3 step 11.

> **v1.2 amendment (2026-10-04, directed by Michael, with US-002)**: the Windows parent app is no longer copy-deployed. It gets **its own, self-contained Windows installer**, separate from the service installer. Also new: light/dark appearance setting for all parent apps (FR-APP-083). Changed: §3.3.8 (FR-APP-083 new), FR-APP-092, §4.2 step 8, §9.5, §11 (Q-1 superseded by Q-4).

> **v1.1 amendment (2026-10-03, directed by Michael, ADR-007)**: the parent app gains a **Windows desktop** target (copy-deployed single exe, used for initial testing). Android and iOS become the primary production platforms, and iOS is no longer out of scope. Changed: §1.2 G-6, §2.1, §3.3 intro and §3.3.8, §3.3.9 (new), §4.2, §8.4, §9.5 (new), §10, §11. Development is split across two machines and acceptance testing is manual. Neither changes product behaviour; both are recorded in `docs/dev-process/`.

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
| G-6 | Support remote configuration and statistics viewing from Android and iOS (primary), Windows and macOS parent apps via the local LAN. |
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
| **Devices** | Android phone (Android 14+) or iPhone (iOS 26+) as primary devices; also Windows 11 PC or MacBook (macOS 26+, Apple Silicon). |
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
| FR-SVC-010 | The service shall continuously monitor running processes for the standard (non-admin) local user accounts that the parent has placed under parental control (FR-SVC-072). Other accounts are not monitored. *(v1.3; was: all standard accounts)* |
| FR-SVC-011 | The service shall detect when a monitored user starts a new application (FR-SVC-046) and check it against the user's break times (TC-010) and the application's app rule and remaining budget (FR-APP-040). *(v1.6; was: allow-list)* |
| FR-SVC-012 | The service shall track cumulative active usage time **in seconds** per application per user per day, and keep it up to date at least every 5 seconds. An application counts as active while it is open (FR-SVC-046) and the user's Windows session is the session in use at the PC (signed in, unlocked, awake, not switched away). Days change at local midnight. *(v1.4; was: minutes, per allowed application)* |
| FR-SVC-013 | For each detected process, the service shall attempt to resolve a human-readable display name. Resolution sources include (in priority order): (1) the executable's file version info / product name metadata, (2) the application's entry in Windows installed-programs registry, (3) an optional online lookup. |
| FR-SVC-014 | The service shall maintain a local dictionary mapping process executable names to their resolved display names. Once resolved, a display name is cached so repeated lookups are avoided. |

#### 3.1.2 Process Classification

The service classifies every process running in a standard user's session into one of three categories:

| Category | Description | Enforcement |
|----------|-------------|-------------|
| **Ignored** | Standard Windows processes and OS-level background processes that are essential for a functioning user session (e.g. `explorer.exe`, `dwm.exe`, `taskhostw.exe`, shell components). | Never terminated, never tracked. |
| **Allowed (controlled)** | Applications whose app rule (FR-APP-040) allows them today: today's weekday is ticked and the daily budget is above 0:00 (e.g. Minecraft, a web browser). *(v1.6: the app rule replaces the allow-list)* | Permitted to run; subject to time budget tracking and enforcement. |
| **Blocked** | Any application that is not ignored and not allowed today, including every application found for the first time (default rule 0:00, no weekday ticked), and any application whose budget for today is used up. *(v1.6)* | Blocked at start (FR-SVC-020). |

| ID | Requirement |
|----|-------------|
| FR-SVC-015 | The service shall maintain a default ignore list of standard Windows processes that are required for normal user session operation. Ignored processes are never terminated and never tracked. |
| FR-SVC-016 | The ignore list shall be shipped with the installer and kept up to date by the development team. It is not configurable by the parent. |
| FR-SVC-017 | Only processes that are not on the ignore list are subject to evaluation (break times, app rules) and enforcement. *(v1.6: allow-list replaced by app rules)* |

#### 3.1.3 App Enforcement

| ID | Requirement |
|----|-------------|
| FR-SVC-020 | If a monitored user starts an application (FR-SVC-046) that is not allowed today (weekday not ticked or budget 0:00) or whose budget for today is used up, the service shall block the start in the same way as a break-time block (FR-SVC-023): detection within 10 seconds, graceful close, forced termination of all its processes after 20 seconds, gone at most 30 seconds after the start. The tray client shows the account's budget text (FR-APP-053). A blocked start counts no usage time. Break times take precedence: during a break time the break display text is shown. *(v1.6; was: process not on the allow-list is terminated)* |
| FR-SVC-021 | Termination shall first attempt a graceful shutdown (e.g. `WM_CLOSE`) and wait for a configurable timeout (default: 30 seconds) before force-killing the process. The timeout is a general setting configurable by the parent. *(v1.6 note: not yet built; all enforcement up to v1.6 uses a fixed timeout of 20 seconds)* |
| FR-SVC-022 | When the budget of a running application is used up (TC-032), or when a break time begins (TC-042), the service shall close that application gracefully and terminate it with all its processes by force if it has not closed after 20 seconds: close request at most 10 seconds after the end, gone at most 30 seconds after it. The tray client shows the budget text, or for a break time the break display text once. *(v1.6: made precise)* |
| FR-SVC-023 | During a pause window (break time, TC-010), the service shall prevent all non-ignored application launches for the affected user (regardless of remaining budget): an application (FR-SVC-046) whose first process the user creates while a break time is in effect is blocked. The **start** of an application is the creation of its first process; its window may appear later, and further processes of an application that is already open are not new starts. A blocked start is detected within 10 seconds after the start (counted from the first window if that appears later; detection may take longer right after sign-in, boot or a restart of the observation, and such starts are still caught afterwards by their process creation time) and then closed with the graceful-then-force approach of FR-SVC-021, with a timeout of 20 seconds: graceful close first; if the application has not closed after 20 seconds, it is terminated by force together with all its processes. At most 30 seconds pass from the start until the application is gone. The tray client shows the account's display text as soon as the block is detected (FR-TRAY-022). Ignored processes (FR-SVC-015) are never ended. Applications that are already running when a break time begins: TC-042. *(v1.5: made precise)* |
| FR-SVC-024 | The service shall store per user account the break time entries (TC-010) and one display text (FR-APP-052), persist them (FR-SVC-030), serve them to parent apps on request, broadcast every stored change at once to all connected parent apps (FR-SVC-053), and apply changes to the next application start. The same applies to the app rules and the budget text per user account (FR-APP-040, FR-APP-053) and to the two warning levels (FR-APP-054), which are one setting for all accounts; changes also apply to running applications within 5 seconds (TC-033). *(v1.5; v1.6: app rules, budget text, warning levels)* |
| FR-SVC-025 | The service shall store a history of blocked application starts (FR-SVC-023) in its local database: time, account, display name, process name and path of the application, the break time entry that matched (start, end, weekday), the outcome (closed gracefully, or terminated by force and after how many seconds) and whether the message was shown to the user. Blocked starts count no usage time (FR-SVC-012). The history is retained per FR-SVC-043 and purged per FR-SVC-047; it is not shown in the parent app yet. From v1.6, each record carries a reason (break time, not allowed today, budget used up), and the history also records running applications closed because their budget ran out or a break time began (FR-SVC-022). *(v1.5; v1.6: reasons and enforced closes)* |
| FR-SVC-026 | The service shall check the time zone of the PC regularly. Each change of the time zone, and each change of the clock if detected, shall take effect for break times within 5 seconds, be logged as a Warning (FR-SVC-100) and be stored as a finding in the service's local database: time, old and new time zone (or clock time), and the session or account that made the change if known. Findings are retained and purged like the history of blocked starts (FR-SVC-025, FR-SVC-043, FR-SVC-047) and are not shown in the parent app yet. Removing the right to change the time zone from standard users is a possible later hardening. *(v1.5, approved by Michael 2026-10-10 with the US-005 implementation plan answers)* |

#### 3.1.4 Configuration Management

| ID | Requirement |
|----|-------------|
| FR-SVC-030 | The service shall persist its configuration locally on disk under the standard Windows application data folder (e.g. `%ProgramData%`). |
| FR-SVC-031 | The service shall accept configuration updates from connected parent apps remotely over the network. |
| FR-SVC-032 | Configuration includes, per user account: the app rules (daily budget and weekdays per application, FR-APP-040) with the budget text (FR-APP-053), and break times (pause windows, TC-010) with their display text. The two warning levels with their texts (FR-APP-054) are one general setting for all accounts. *(v1.5: made precise; v1.6: app rules replace the allowed applications list; warning levels)* |
| FR-SVC-033 | Configuration data shall be stored in separate files per child user account. |

#### 3.1.5 Statistics Collection

| ID | Requirement |
|----|-------------|
| FR-SVC-040 | The service shall collect per-user, per-application daily usage statistics (seconds used, FR-SVC-012). *(v1.4; was: minutes)* |
| FR-SVC-041 | The service shall serve collected statistics to parent apps on request, and push changes of the statistics to connected parent apps (FR-SVC-053). *(v1.4: made precise)* |
| FR-SVC-042 | Statistics shall be stored on disk under the standard Windows application data folder (e.g. `%ProgramData%`), in separate files per child user account. |
| FR-SVC-043 | Statistics shall be retained for 90 days at daily granularity (seconds of use per application per day). The start/end history (FR-SVC-045) and the history of blocked starts (FR-SVC-025) are retained for 90 days as well. Data older than 90 days shall be automatically purged. The application inventory (FR-SVC-044) is not subject to this retention. *(v1.4: made precise; v1.5: history of blocked starts added)* |
| FR-SVC-044 | The service shall keep, per user account, an inventory of the applications the user has started: one record per application, with at least the process name, the display name (FR-SVC-013) and the path of the executable. Records are kept without time limit while the account exists. *(v1.4)* |
| FR-SVC-045 | The service shall store the start time and end time of every application instance in a history, and write both to its log files (FR-SVC-100). *(v1.4)* |
| FR-SVC-046 | Only applications that the user started (by hand or in the user's name, e.g. Autostart) and that have a window of their own in the user's session are recorded, i.e. what Windows Task Manager lists in the "Apps" group. Background processes, services and Windows processes are not recorded. An application consisting of several processes is recorded as one application. *(v1.4)* |
| FR-SVC-047 | When a user account is deleted on the Windows PC, the service shall purge all recorded data of that account (inventory, history, history of blocked starts, statistics) and its configuration (break time entries and display text, FR-SVC-024; app rules and budget text, FR-APP-040, FR-APP-053). *(v1.4; v1.5: history of blocked starts and configuration added; v1.6: app rules and budget text)* |

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
| FR-SVC-070 | The service shall auto-discover all local standard (non-admin) Windows user accounts and keep them as an inventory. The inventory follows changes while the service runs (accounts added, deleted, renamed, or changed between standard and admin). Windows built-in accounts (Administrator, Guest, DefaultAccount, WDAGUtilityAccount) and the Windows setup leftover account `defaultuser0` are not part of it. *(v1.3: made precise)* |
| FR-SVC-071 | Discovered accounts shall be presented to the parent app for configuration. Admin accounts shall be excluded. Connected parent apps receive changes of the inventory without having to ask (FR-SVC-053). *(v1.3: made precise)* |
| FR-SVC-072 | The service shall store for each inventoried account whether it is under parental control. The parent sets this from a paired parent app (FR-APP-022). The service broadcasts every stored change of the selection at once to all connected parent apps (FR-SVC-053). *(v1.3)* |
| FR-SVC-073 | The selection of FR-SVC-072 shall be persisted locally (FR-SVC-030) and kept across service restarts, reboots and service updates. *(v1.3)* |
| FR-SVC-074 | Accounts shall be identified by their Windows security identifier (SID), so that renaming an account does not change its selection or configuration. *(v1.3)* |

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
| FR-SVC-100 | The service shall write operational logs (enforcement actions, configuration changes, pairing events, errors) to log files in a dedicated subfolder under the application data folder (`%ProgramData%\EagleEye\logs\`). The subfolder shall be readable by SYSTEM and administrators only, not by standard users. *(v1.3: made precise)* |
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
| FR-TRAY-020 | The tray client shall show the early and the final warning (TC-030, TC-040) when a time budget is about to run out or a break time is about to begin, in the topmost dialog of FR-TRAY-022: the parent's warning text plus one line of EagleEye text naming the app or the break time and the minutes left. Only one dialog is open per session; a newer message replaces the text of an open one. *(v1.6: made precise)* |
| FR-TRAY-021 | The tray client shall show the account's budget text (FR-APP-053) in the dialog of FR-TRAY-022 when an application start is blocked or a running application is closed because of its budget. *(v1.6: made precise)* |
| FR-TRAY-022 | When an application start is blocked by a break time (FR-SVC-023), the tray client in the user's session shall show the account's display text (FR-APP-052) in a topmost dialog that closes only with "OK" or Enter (not with Esc or Alt+F4). Other windows stay usable (the dialog is not system-modal); the keyboard focus is best effort; exclusive full-screen games may cover the dialog. *(v1.5; wording "system-modal" replaced with the US-005 plan answers, 2026-10-10)* |
| FR-TRAY-030 | The tray client shall optionally display a topmost overlay window showing remaining time, visible even when applications are in focus. |
| FR-TRAY-040 | The tray client shall connect to `EagleEye.Service` on `localhost`. |
| FR-TRAY-041 | The tray client shall display a connection status indicator (red/green) showing whether it is connected to the service. |
| FR-TRAY-042 | If the service is unreachable, the tray client shall retry connecting continuously in the background. |
| FR-TRAY-050 | The tray client shall auto-start when the kid's user session begins. |
| FR-TRAY-060 | The tray client shall be packaged and installed together with `EagleEye.Service` in a single installer. |
| FR-TRAY-070 | The tray client shall display the 6-digit pairing code as a popup when requested by the service (FR-SVC-091). |

### 3.3 EagleEye.ParentApp (Android, iOS, Windows, macOS)

The parent app is a single MAUI codebase deployed to Android, iOS, Windows and macOS. It connects to `EagleEye.Service` over the local LAN.

**Platform roles (Michael, 2026-10-03):**

| Platform | Role |
|----------|------|
| **Android, iOS** | Primary platforms for production use |
| **Windows** | Initial testing vehicle for parent-side functionality. Built first, so features can be exercised on the Windows developer machine before the mobile apps exist. Remains available as an extra app. |
| **macOS** | Desktop parent app, developed on the MacBook |

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
| FR-APP-020 | The parent app shall display all discovered standard user accounts (kids) from the service, as delivered by the service (FR-SVC-070). *(v1.3: made precise)* |
| FR-APP-022 | The parent shall be able to mark each discovered account as under parental control or not. The parent app shall always show the selection stored in the service (FR-SVC-072); changes are sent to the service, and every connected parent app receives the stored setting from the service's broadcast (FR-SVC-072). Parent apps never exchange data with each other directly. *(v1.3)* |
| FR-APP-021 | The parent shall be able to select a user account and manage its configuration. |

#### 3.3.3 Application Allow-List

*v1.6: there is no separate allow-list. An application is allowed or blocked through its app rule (FR-APP-040). The list of installed applications (FR-SVC-080) and its re-scan (FR-APP-033) are not used for this.*

| ID | Requirement |
|----|-------------|
| FR-APP-030 | The parent app shall display, per user account, the applications of the account's application inventory (FR-SVC-044), i.e. every application the user has ever started, including applications whose starts were only blocked, using their human-readable display names (FR-SVC-013). File Explorer is not listed (never limited). *(v1.6; was: installed applications, FR-SVC-080)* |
| FR-APP-031 | The parent shall be able to allow or block specific applications per user account, through the weekday ticks and the daily budget of the app rule (FR-APP-040). *(v1.6: made precise)* |
| FR-APP-032 | Where a display name could not be resolved, the parent app shall show the executable name as fallback so no application is invisible to the parent. |
| FR-APP-033 | The parent app shall provide a way to trigger a re-scan of installed applications on the Windows PC (FR-SVC-081). *(v1.6: not needed for the app rules; kept for a possible later use)* |

#### 3.3.4 Time Budget Configuration

| ID | Requirement |
|----|-------------|
| FR-APP-040 | The parent shall be able to set, per application and per user account, an **app rule**: one daily time budget in hours (0..23) and minutes (0..59), i.e. 0:00 to 23:59, edited on the "Rules" page in the section "Application rules" and saved at once. An application found for the first time gets the rule 0:00 with no weekday ticked, so it is blocked until the parent allows it. *(v1.6; was: per allowed application)* |
| FR-APP-041 | The app rule has one tick per weekday (Monday through Sunday). A ticked day allows the application on that day, up to the daily budget; an unticked day blocks it. All days may be unticked. There are no different budgets per weekday. *(v1.6; was: different budgets per weekday)* |

#### 3.3.5 Pause Window Configuration

| ID | Requirement |
|----|-------------|
| FR-APP-050 | The parent shall be able to define break times (pause windows, TC-010) per user account on a "Rules" page: a table of entries, each with an on/off switch, a start time, an end time and the weekdays it applies to (e.g. 20:00 to 23:59 and 00:00 to 09:00, Monday to Friday). Entries are added, edited in place and deleted directly; every change is saved at once. *(v1.5; was: pause windows per weekday, e.g. blocked from 20:00 to 09:00)* |
| FR-APP-051 | During a pause window, no applications are allowed for the affected user, regardless of remaining budget. |
| FR-APP-052 | The parent shall be able to edit one display text per user account, shown to the kid when an application start is blocked by a break time (FR-TRAY-022). The text supports emojis. The default text exists in German and English; an unchanged default follows the language of the app that shows it. *(v1.5; v1.6: English default, US-006 plan Q-1)* |
| FR-APP-053 | The parent shall be able to edit one **budget text** per user account on the "Rules" page, shown to the kid when an application is blocked or closed because of its budget (FR-TRAY-021). It supports emojis and has at most 500 characters; an empty text restores the default. The default text exists in German and English and follows the language of the app showing it. *(v1.6)* |
| FR-APP-054 | The parent shall be able to set, on the "Settings" page in the section "Time-elapsed warnings", the minutes before the end (whole minutes 1..59, early greater than final) and the text of the two fixed warning levels "early" (default 5) and "final" (default 1), once for all accounts (TC-030, TC-040). Levels cannot be added, renamed or switched off. The default texts exist in German and English and follow the language of the app showing them. *(v1.6)* |

#### 3.3.6 Statistics Viewing

| ID | Requirement |
|----|-------------|
| FR-APP-060 | The parent app shall display usage statistics per user, per application, per day: grouped by user account, then by day with the current day first, durations as HH:MM. *(v1.4: made precise)* |
| FR-APP-061 | Statistics shall show actual minutes used vs. configured budget. |

#### 3.3.7 Event Notifications

| ID | Requirement |
|----|-------------|
| FR-APP-070 | The parent app shall display real-time events pushed by the service (e.g. blocked app attempt, budget expired, user logged in/out). |

#### 3.3.8 Platform UI

| ID | Requirement |
|----|-------------|
| FR-APP-080 | On iOS and Android, the app shall operate in portrait orientation only and follow platform-typical look and feel. |
| FR-APP-081 | On macOS and Windows, the app shall present a desktop-style UI. |
| FR-APP-082 | The app shall be functionally identical across all platforms. |
| FR-APP-083 | The parent shall be able to switch the app between a light and a dark appearance in the app settings. The choice is stored per app installation and kept across restarts and updates. Until the parent has chosen, the app follows the operating system's light/dark setting. *(v1.2)* |

#### 3.3.9 Windows Parent App (added in v1.1)

| ID | Requirement |
|----|-------------|
| FR-APP-090 | The Windows parent app shall provide the same functionality as the other parent apps (FR-APP-082). |
| FR-APP-091 | The Windows parent app shall work both on the same PC as `EagleEye.Service` and remotely from another PC on the LAN. In both cases it uses the same communication model as the mobile apps: TLS-encrypted connection, pairing (FR-SVC-090 to FR-SVC-098) and the parent API. There is no special local or privileged access path. |
| FR-APP-092 | The Windows parent app shall be delivered as its own Windows installer, separate from and independent of the `EagleEye.Service` installer. The installer shall be self-contained: no other software (e.g. a runtime) has to be installed before or alongside it. It shall provide a guided wizard for install, repair (re-install over an existing installation) and uninstall, and uninstalling shall remove the application cleanly. *(v1.2; replaces the v1.1 copy deployment.)* |

### 3.4 Code Reuse

| ID | Requirement |
|----|-------------|
| FR-CR-010 | Common logic, data models, and communication contracts shared across components shall be implemented once and reused, to minimize long-term maintenance effort. |
| FR-CR-011 | The parent app shall share a single codebase across Windows, macOS, Android and iOS. Only platform-specific UI code may differ. |

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
| 8 | Parent | Installs the EagleEye parent app on their mobile phone (Android or iOS), Windows PC (EagleEye parent app installer) or MacBook. |
| 9 | Parent | Opens the parent app and enters the Windows machine's hostname (or IP address). |
| 10 | Parent | The app connects to `EagleEye.Service` over TLS on the local LAN. |

### 4.3 Configuration (Parent)

| Step | Actor | Action |
|------|-------|--------|
| 11 | Parent | Views the list of discovered standard user accounts and selects which of them (the children's) are under parental control. *(v1.3)* |
| 12 | Parent | Selects a child's account and, for each application the child has started, sets the weekdays on which it may be used. New applications are blocked until the parent allows them. *(v1.6)* |
| 13 | Parent | Sets one daily time budget (hours:minutes) per application, the budget text per account, and the two warning levels for all accounts. *(v1.6)* |
| 14 | Parent | Defines break times (pause windows) with their weekdays (e.g. 20:00--23:59 and 00:00--09:00 blocked) and the text shown to the kid. *(v1.5)* |
| 15 | Parent | Configuration is sent to the service and persisted. |

### 4.4 Daily Use (Kid)

| Step | Actor | Action |
|------|-------|--------|
| 16 | Kid | Logs into Windows with their standard user account. |
| 17 | System | `EagleEye.TrayClient` auto-starts in the kid's session and connects to the service on localhost. |
| 18 | Kid | Tries to start an application. |
| 19 | System | If the application is not allowed today or its budget is used up, the service closes it (graceful then force-kill) and the tray client shows the budget text. *(v1.6)* |
| 20 | System | If the application is allowed, it launches normally. The service tracks usage time. |
| 21 | System | When the remaining budget, or the time until a break time, reaches the early and then the final warning level, the tray client shows the warning. *(v1.6)* |
| 22 | System | When the budget expires, the service closes the application. The tray client shows the budget text. *(v1.6)* |
| 23 | System | During a pause window, all application launches are blocked for the kid, and applications still running when it begins are closed. *(v1.6)* |

### 4.5 Budget Reset

| Step | Actor | Action |
|------|-------|--------|
| 24 | System | At midnight, all daily time budgets reset to their configured values. Unused budget does not carry over. |

---

## 5. Time Control Requirements

### 5.1 Pause Windows

| ID | Requirement |
|----|-------------|
| TC-010 | Pause windows (break times, "Ruhezeiten") are defined per user account as a list of entries. Each entry has an on/off switch, a start time, an end time and one or more weekdays (Monday through Sunday). An entry that is switched off is kept but not applied. *(v1.5; was: per weekday per user account)* |
| TC-011 | Start and end time of an entry lie within one day (00:00 to 23:59), and the end time is later than the start time. The end time 23:59 means until midnight. A pause across midnight is entered as two entries (e.g. 20:00--23:59 and 00:00--09:00). *(v1.5; was: e.g. 20:00--09:00)* |
| TC-012 | During an active pause window, no applications are allowed for the affected user, regardless of remaining time budget. |
| TC-013 | The service shall enforce pause windows even if no parent app is connected. |
| TC-014 | An entry is in effect from its start minute (inclusive) to its end minute (exclusive; 23:59 counts as 24:00), on its weekdays, in the local time and on the local weekday of the Windows PC running the service. *(v1.5)* |

### 5.2 Time Budgets

| ID | Requirement |
|----|-------------|
| TC-020 | Time budgets are configured per application and per user account, as one daily budget plus the weekdays on which the application may be used (FR-APP-040, FR-APP-041). Used time is the application's active time of the day as counted for the usage statistics (FR-SVC-012). *(v1.6; was: per weekday)* |
| TC-021 | The minimum granularity for budget configuration is one minute. |
| TC-022 | Budget countdown only occurs during non-pause (active) time. Time spent during a pause window does not consume budget. |
| TC-023 | Unused daily budget does not carry over to the next day. |
| TC-024 | All daily budgets reset at midnight. If an application running at midnight is not allowed on the new day, midnight counts as the end of its budget (warnings before, closed at midnight). *(v1.6: midnight made precise)* |
| TC-025 | Days and budgets follow the local date and time of the Windows PC running the service. The service shall detect changes of the time zone, and clock changes it can detect, that affect budgets or the day boundary, and log each as a Warning (FR-SVC-100). It does not prevent them; protection is a later hardening (see FR-SVC-026). *(v1.6)* |

### 5.3 Budget Expiry Warning and Shutdown

| ID | Requirement |
|----|-------------|
| TC-030 | Before a budget expires, the system shall warn the kid via the tray client using two warning levels: an **early warning** (default: 5 minutes before shutdown) and a **final warning** (default: 1 minute before shutdown). Each level is shown once per application and day; if less time is left at the start, the matching warning is shown at once. *(v1.6: "last" becomes "final"; made precise)* |
| TC-031 | Both warning levels (minutes, 1..59, early greater than final, and text) are configurable by the parent via the parent app, once for all accounts (FR-APP-054). They cannot be switched off. *(v1.6: made precise)* |
| TC-032 | When a budget reaches zero, the service closes the application gracefully and terminates it by force after 20 seconds (FR-SVC-022). *(v1.6: made precise)* |
| TC-033 | A parent change during the day never leaves a running application less than the final-warning minutes. When a change moves the end of a running application earlier so that less than the final-warning minutes are left (budget lowered, today unticked, or a break time now in effect or beginning sooner), the application gets a **grace period** of the full final-warning minutes: the final warning is shown at once, and the application is closed when the grace period ends. New starts during a break time are blocked at once. Raising a budget does not bring back warnings already shown that day. *(v1.6; made precise with the US-006 plan answers Q-2 and Q-5, 2026-10-12)* |

### 5.4 Pause Window Warning and Shutdown

| ID | Requirement |
|----|-------------|
| TC-040 | Before a pause window begins, the system shall warn the kid via the tray client using two warning levels: an **early warning** (default: 5 minutes before pause) and a **final warning** (default: 1 minute before pause), when at least one application of the user is running. *(v1.6: "last" becomes "final")* |
| TC-041 | The pause window warnings use the same two warning levels and texts as the budget warnings (TC-031). For a running application, only the warnings of the earlier end (budget or pause window) are shown. *(v1.6; was: independent thresholds)* |
| TC-042 | When a pause window begins, all running non-ignored applications for the affected user are closed gracefully and terminated by force after 20 seconds (FR-SVC-022); the break display text is shown once. *(v1.6: made precise)* |

---

## 6. Multi-User Requirements

| ID | Requirement |
|----|-------------|
| MU-010 | The service shall support multiple standard (non-admin) user accounts on the same Windows PC simultaneously. |
| MU-011 | Each user account has its own independent configuration: app rules (time budgets and weekdays), budget text, pause windows. The two warning levels are shared by all accounts (FR-APP-054). *(v1.6)* |
| MU-012 | Each user account has its own independent usage statistics. |
| MU-013 | The service auto-discovers standard user accounts (FR-SVC-070). No manual account registration is needed; the parent only selects which discovered accounts are under parental control (FR-SVC-072). *(v1.3)* |
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
| NFR-L-010 | The tray client and all parent apps (Windows, macOS, Android, iOS) shall support multiple languages. |
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

### 9.5 Windows (ParentApp) — added in v1.1

| Attribute | Requirement |
|-----------|-------------|
| **OS** | Windows 11 (x64) |
| **Framework** | .NET MAUI (WinUI) |
| **UI** | Desktop-style (not portrait-locked), like macOS |
| **Distribution** | Separate, extra app with **its own self-contained installer** (install, repair, uninstall wizard), FR-APP-092. Not part of the EagleEye service installer. *(v1.2; v1.1 used copy deployment.)* |
| **Deployment** | May run on the same PC as the service, or on another Windows PC on the LAN (FR-APP-091) |
| **Purpose** | Initial testing vehicle for the parent-side functionality (see §3.3) |

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
| **Budget carry-over** | Unused daily budget does not carry over. This is by design, not a deferral. |
| **Per-app pause windows** | Pause windows apply to all apps for a user. Per-app pause windows are not planned. |

---

## 11. Open Questions

No open questions. The questions raised by the v1.1 amendment were answered by Michael on 2026-10-03:

| ID | Question | Answer (Michael, 2026-10-03) |
|----|----------|------------------------------|
| Q-1 | How is the Windows parent app distributed? | An extra app; no installer needed. A single executable that is simply copied is sufficient. → §9.5, FR-APP-092 |
| Q-2 | May the Windows parent app run on the same PC as the service? | Yes, and remotely as well. It uses the same communication model as the mobile apps. → FR-APP-091 |
| Q-3 | Which parent-app platform comes first? | Android and iOS are the primary platforms for production. The Windows client is used for initial testing. → §3.3 |
| Q-4 | How is the Windows parent app distributed? (supersedes Q-1) | Michael, 2026-10-04: as an additional, full, clean and self-contained Windows installer next to the service installer. This replaces the copy-only deployment. → §9.5, FR-APP-092 |

---

*End of General Product Requirements — Approved v1.6*

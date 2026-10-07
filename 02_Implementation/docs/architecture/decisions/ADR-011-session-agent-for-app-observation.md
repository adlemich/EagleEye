# ADR-011: Session Agent for App Observation — a SYSTEM Helper per Watched Session

**Status**: Proposed — approved with the US-004 implementation plan
**Date**: 2026-10-07
**Deciders**: ARC, Michael

---

## Context

US-004 records the **apps** a kid uses: the programs Windows Task Manager lists in the group "Apps" ("Prozesse → Apps") of the kid's session, i.e. programs with a window of their own (FR-SVC-046, US-004 AC-3 to AC-6). Later enforcement stories (ADR-005, ADR-006) need the same observation, and must close windows (`WM_CLOSE`) in the kid's session.

The service runs as SYSTEM in **session 0**. Windows isolates sessions: a window station and its desktops belong to one session, and a process can only open the window stations of its own session. So the service cannot call `EnumWindows` (or `CloseMainWindow`) for windows in session 1, 2, … . What the service *can* do from session 0:

- enumerate processes of all sessions, open them with `PROCESS_QUERY_LIMITED_INFORMATION` and read path, owner SID, session ID, creation time and package identity;
- read session state through the WTS API (`WTSEnumerateSessions`, `WTSQuerySessionInformation`) and get session-change notifications from the Service Control Manager;
- start a process in another session (`CreateProcessAsUser` with a duplicated SYSTEM token whose `TokenSessionId` is set to the target session).

Process data alone cannot tell an app (Task Manager "Apps") from a background process (OneDrive in the notification area, Runtime Broker, a game launcher's updater), and it cannot find the app behind a Store app window, which belongs to `ApplicationFrameHost.exe`. Window data from inside the session is required.

The tray client already runs in every user session (HKLM Run key) and has a loopback channel to the service (`TrayHub`). US-004 AC-27 says the tray shows nothing new, and AC-5 says the tray itself is never recorded.

---

## Decision

We will observe app windows with a **session agent**: a helper process that the service starts **as SYSTEM inside each watched user session**, on that session's interactive desktop (`WinSta0\Default`). It reports the app windows of its session to the service. The service does everything else.

### 1. What the agent is

- The agent is the **service executable itself**, started with the argument `--session-agent`. `Program` branches before building the web host and runs a small loop. There is no new project or deliverable and no new installer file, and the agent always has exactly the service's version, so the protocol cannot get out of step.
- The agent runs under the **SYSTEM** account at **System integrity**, but with a **hardened token**: all privileges removed except `SeChangeNotifyPrivilege`, Administrators deny-only, and write-restricted where the runtime allows it (§7). A standard user cannot end it, suspend it or inject into it. (The tray client runs as the kid and can be ended by the kid; see Alternatives and the Security Analysis.)
- The agent has no network endpoint, no database, no log file and no UI. It does not load the ASP.NET Core host.

### 2. Who starts it, and when

- `SessionAgentSupervisor` in the service keeps **one agent per session whose user is an account under parental control** (US-003) and that is logged on (WTS state `Active` or `Disconnected`). Sessions of other accounts, admin accounts and session 0 never get an agent (US-004 AC-1).
- Start: the agent is **not** started with the user's token. The service uses `WTSQueryUserToken` only to read the session user's SID. To start the agent, it duplicates its own SYSTEM token (`DuplicateTokenEx`), sets `TokenSessionId` to the target session and calls `CreateProcessAsUser` with `lpDesktop = "WinSta0\Default"` and `CREATE_NO_WINDOW`. Standard input and standard output of the agent are anonymous pipes held by the service.
- The supervisor reconciles every accounting tick (5 s, ADR-012): it starts missing agents, stops agents whose account is no longer controlled, and restarts agents that exited, with a back-off (1, 5, 30 s; a warning in the log each time).

### 3. Channel: anonymous pipes, one JSON line per message

- Agent → service (stdout): one JSON object per line, `{"seq":17,"apps":[{"pid":4711,"kind":"Window"},{"pid":812,"kind":"FileExplorer"}]}`. It is sent when the set changes, and at least every 5 s as a heartbeat. The message holds only process IDs and a kind; it carries no names or paths.
- Service → agent (stdin): no messages. **End of file on stdin means "stop"**; the agent exits at once. If the service dies, the pipe breaks and the agent exits too.
- Agent errors go to stderr; the service logs them as warnings.
- The pipes are inherited handles of exactly two processes. There is no named pipe or port that another process could open, so a kid cannot impersonate an agent.
- The service does not trust PIDs blindly. For every PID it checks that the process runs in the agent's session and that its owner SID is the session's user SID (programs started with another account's rights are out of scope, US-004). It identifies an instance by PID plus process creation time, so a reused PID is never confused with the earlier process.

### 4. What counts as an app window (the "Apps" rule)

The agent calls `EnumWindows` about once per second on `WinSta0\Default`. It counts a top-level window as an app window when the rule `AppWindowRule` (pure, unit-tested) says so. The rule follows the taskbar and Alt+Tab criteria, which Task Manager's "Apps" group also follows:

1. The window is visible (`IsWindowVisible`) and has a non-empty size.
2. It is not cloaked, or it is cloaked only by the shell (`DWM_CLOAKED_SHELL`: a window on another virtual desktop still counts). Windows cloaked by the app itself (`DWM_CLOAKED_APP`, e.g. a suspended Store app frame) do not count.
3. It has no owner window and is not a tool window (`WS_EX_TOOLWINDOW`), or it has `WS_EX_APPWINDOW`.
4. Its window class is not one of the shell's own classes (`Shell_TrayWnd`, `Shell_SecondaryTrayWnd`, `Progman`, `WorkerW`, and the classes of Start, Search and the notification area).
5. **Store apps**: for a window of class `ApplicationFrameWindow` (owned by `ApplicationFrameHost.exe`), the agent reports the process of its child window of class `Windows.UI.Core.CoreWindow`, i.e. the actual app (e.g. `CalculatorApp.exe`). A frame without such a child (suspended or closing) is skipped (US-004 AC-3).
6. **Explorer**: windows of `explorer.exe` count only if their class is `CabinetWClass` (File Explorer and Control Panel windows), reported as kind `FileExplorer`. The taskbar, Start, desktop and other shell windows never count (US-004 AC-4).

A process with several app windows is reported once. Grouping several processes into one app happens in the service, by program path (ADR-012).

**Known limits.** Task Manager's exact rule is not documented. The rule above matches it for ordinary desktop programs, Store apps, File Explorer and notification-area-only programs, which are the US-004 examples. A program that shows a visible, unowned window without a taskbar button (some launchers, splash screens) may be counted while that window is visible. Such cases are fixed in `AppWindowRule` when found, and its unit tests document every case.

### 5. Never recorded

The service never records these processes, whatever their windows:
- EagleEye's own processes, identified by their **full path in the installation folder**, not by name. A kid's program renamed to `EagleEye.TrayClient.exe` is therefore still recorded. This also keeps the tray client's pairing window and About dialog out of the report (US-004 AC-5).
- Processes in session 0.

The window kinds that depend on a specific system program are verified by the service against the program's path:
- `FileExplorer` only for `%SystemRoot%\explorer.exe`.
- The Store-frame resolution only when the frame belongs to `%SystemRoot%\System32\ApplicationFrameHost.exe`.

Otherwise the window is treated as an ordinary window of its own process (see Security Analysis, T-10).

### 6. Reuse by later stories

Enforcement stories will reuse the agent for window-level actions in the kid's session. Graceful close with `WM_CLOSE` (ADR-006) also requires being in that session. Those actions will need a service → agent command channel (stdin messages). That channel is out of scope here; it is added by the story that needs it, without changing this ADR's structure.

### 7. Hardening (result of the Security Analysis below)

**Agent token and process** (built by the service's `SessionAgentLauncher`):
1. Token: duplicate of the service's SYSTEM token, passed through `CreateRestrictedToken` with `DISABLE_MAX_PRIVILEGE` (keeps only `SeChangeNotifyPrivilege`), `BUILTIN\Administrators` as a deny-only SID, and `WRITE_RESTRICTED` with the restricting SID `NT AUTHORITY\RESTRICTED` (S-1-5-12). `TokenSessionId` is set to the target session; the integrity level stays **System**.
   - If the .NET runtime or the desktop connection cannot start under the write-restricted token (verified by DEV in Step 3), the agent runs **without** the write restriction but keeps everything else. DEV records this in the implementation report.
2. Process and thread security descriptors set explicitly (`lpProcessAttributes`, `lpThreadAttributes`):
   - SYSTEM: full access.
   - Administrators: `PROCESS_QUERY_LIMITED_INFORMATION | PROCESS_TERMINATE | SYNCHRONIZE` only.
   - Nobody else, and no inherited entries. The kid gets no access at all.
3. Command line: `lpApplicationName` = the absolute path of the service executable (`Environment.ProcessPath`), command line `"<path>" --session-agent`. The agent accepts no other argument.
4. Environment: an **explicit minimal environment block** built by the service:
   - `SystemRoot`, `windir`, `SystemDrive`, `TEMP` and `TMP` = `%SystemRoot%\Temp`, `PATH` = `%SystemRoot%\System32`, `DOTNET_EnableDiagnostics=0`.
   - Never the kid's environment, and not the service's own environment, so no `DOTNET_*`, `COR_*` or `CORECLR_*` variable can load code.
   - Current directory = the installation folder.
5. Handle inheritance: `STARTUPINFOEX` with `PROC_THREAD_ATTRIBUTE_HANDLE_LIST` containing exactly the agent's ends of the three pipes (stdin, stdout, stderr). All other service handles are non-inheritable anyway.
6. `CREATE_NO_WINDOW`, `CREATE_UNICODE_ENVIRONMENT`, `EXTENDED_STARTUPINFO_PRESENT`; desktop `WinSta0\Default`.

**Agent code rules:**

7. First action: `SetDefaultDllDirectories(LOAD_LIBRARY_SEARCH_SYSTEM32 | LOAD_LIBRARY_SEARCH_APPLICATION_DIR)`. The service executable's `runtimeconfig` sets `System.StartupHookProvider.IsSupported = false` (this applies to the service as well).
8. The agent creates **no window of any kind** (no message-only window), installs no hooks, runs no message loop, uses no COM, no shell API, no UI Automation and no DPI API. It calls only window functions that **do not send messages** to the target window:
   - Allowed: `EnumWindows`, `EnumChildWindows`, `IsWindowVisible`, `IsIconic`, `GetWindowRect`, `GetWindow(GW_OWNER)`, `GetWindowLongPtr(GWL_STYLE/GWL_EXSTYLE)`, `GetClassName`, `GetWindowThreadProcessId`, `DwmGetWindowAttribute(DWMWA_CLOAKED)`.
   - Never: `GetWindowText`, `SendMessage*`, `PostMessage*`.
   - Window titles are never read.
9. Limits: at most 20 000 windows per scan. Beyond that the report is marked `truncated` and the service logs a warning. Class names are read into a 257-character buffer and compared with constants only.
10. At start the agent writes one diagnostic line to stderr: token user, integrity level, enabled privileges, write-restricted yes/no. The service logs it at Information, so the hardening is visible in the service log.

**Service side** (the service processes kid-controlled data in session 0 with full SYSTEM rights):

11. Agent reports are untrusted input:
    - The agent's stdout is read with a line limit of 64 KB; a longer line restarts the agent.
    - Strict schema, at most 1 000 entries.
    - Every PID is checked: session, owner SID, and creation time as identity.
    - Per session only the **latest** report is queued (bounded channel, drop-oldest), so a flood cannot grow memory.
12. Kid-controlled files are never loaded as code. No `LoadLibrary` of user files, no icon extraction, no `SHGetFileInfo`/`IShellItem`/COM shell handlers, no `FileVersionInfo` on untrusted paths.
13. Program paths are classified by `ProgramPathPolicy` (pure):
    - Only **local fixed-drive** paths are read at all. UNC, `\\?\UNC\`, `\\.\` device paths, WebDAV, removable and network drives are never opened; the process name is used as display name.
    - Before deciding, the path is normalized and the file's final path is determined (`GetFinalPathNameByHandle`).
    - Files whose final path is in an admin-only system location (`%SystemRoot%`, `%ProgramFiles%`, `%ProgramFiles(x86)%`, `%ProgramFiles%\WindowsApps`) may be read with the Windows version API (needed for MUI-localized names such as "Windows-Explorer").
    - All other files (user-writable locations) are read with the managed, bounds-checked `VersionResourceReader`. It reads only the `RT_VERSION` resource (≤ 64 KB) of files up to 512 MB.
14. All file access for name resolution runs **while impersonating the kid's token** (`ImpersonateLoggedOnUser` with the session's user token), so SYSTEM rights can never be used as a confused deputy.
15. Store package names:
    - Only for packages installed under `%ProgramFiles%\WindowsApps`.
    - The manifest is read with `XmlReader` (DTD prohibited, no resolver, ≤ 1 MB).
    - Only a resource reference built by the service (`@{<PackageFullName>?ms-resource://…}`) is passed to `SHLoadIndirectString`. A raw manifest string starting with `@` is never passed.
16. Every resolved name is sanitized: control characters removed, trimmed, at most 256 characters. Any exception falls back to the process name, with a warning in the log.
17. Flicker protection (log and history flooding):
    - An app that disappears and reappears within 5 s continues the same instance.
    - Instance start/end log entries are limited to 30 per app per hour; further ones are summarized in one line per hour.
18. Missing heartbeat (15 s) or an agent exit:
    - Recording for that session is **paused** (no credit; instances stay open), the agent is restarted, and a Warning is logged.
    - After 3 failed restarts in a row, an Error is logged ("Usage recording for account … is not possible"), and the supervisor keeps retrying every 30 s.

**Debug-only code** (`DevSessionAgentLauncher`, `EAGLEEYE_DEV_WATCH_SID`, `EAGLEEYE_DATA_DIR`) is compiled only in Debug builds (`#if DEBUG`); installers always package Release.

---

## Rationale

### Alternatives Considered

| Option | Reason Rejected |
|---|---|
| **Tray client reports the windows** (it already runs in each session and has a channel to the service) | The tray client runs **as the kid** and can be ended with Task Manager by the kid (arc42 R-6). Tracking would stop without a trace, and later enforcement could be bypassed the same way. The tray hub is unauthenticated on loopback, so any program of the kid could pretend to be the tray and report fake data. It would also make the tray security-relevant, against ADR-004 ("read-only, informational"). |
| **Service only, process-based** (process list, `GetGuiResources`, heuristics on known names) | It cannot see windows from session 0, cannot tell a notification-area program from an app, and cannot resolve the app behind `ApplicationFrameHost`. That breaks AC-3, AC-4 and AC-5. |
| **ETW (`Microsoft-Windows-Win32k` window events) from session 0** | Undocumented payloads, high event volume, and still no reliable "Apps" decision. |
| **WMI / UI Automation from the service** | They have the same session isolation problem, and are slow and heavy. |
| **Separate agent project / own executable** (e.g. Native AOT) | Smaller memory footprint, but it is a new deliverable with its own build, installer entries and version, and Native AOT needs the C++ build tools on the Windows machine. Possible later if memory matters (see Consequences). |
| **Agent running as the user** (`WTSQueryUserToken`) | The kid could end it, like the tray client. |
| **Named pipe or loopback SignalR between agent and service** | It would need its own authentication. Inherited anonymous pipes are reachable only by the two processes. |
| **Window events in the agent (`SetWinEventHook`) instead of a 1 s scan** | More precise (sub-second), but it needs a message loop and careful handling of missed events. A 1 s scan is far inside the story's bounds (5 s accounting, 15 s display) and costs well below 1 ms per scan. It can be changed inside the agent later without affecting the protocol. |

---

## Consequences

### Positive

- Observation works for every kind of app the story names, and the kid cannot switch it off.
- The service stays the only component with logic and state. The agent is a thin sensor, and its rule is unit-tested.
- Enforcement stories get a process in the kid's session that can act on windows.

### Negative / Trade-offs

- One extra process per watched session. Each agent is a .NET runtime with about 30 to 40 MB of private memory, and its CPU use is negligible. Usually one kid is logged on, so this means one agent.
- The kid sees an `EagleEye.Service.exe` process running as SYSTEM in their session (Task Manager → Details). This is acceptable and even transparent.
- More Win32 interop (token, process creation, pipes, window enumeration, DWM cloaking). It is thin code and verified manually.

### Neutral

- When a session is locked, its input desktop switches to the Winlogon desktop. The agent keeps enumerating `WinSta0\Default`; whether the session is in use comes from the WTS state, not from the agent (ADR-012).

---

## Security Analysis

*Requested by Michael (2026-10-07) before approving US-004. The measures are part of the decision (§7) and of the US-004 plan.*

### Threat model

- **Attacker**: the kid at a standard account, and any program the kid runs: downloaded tools, scripts, AutoHotkey, own code. They run at **Medium** integrity with the kid's token. They have full control over the kid's files, processes, windows, environment variables, network shares the kid opens, and the content of every program the kid runs (file names, PE resources, version info, window classes and styles).
- **Not in scope**: an attacker with admin rights or physical access to the disk (they can uninstall EagleEye anyway). Also not in scope: the separate risk of a kid pairing an own parent app (arc42 R-8).
- **Goals of the attacker**:
  - **G1** gain SYSTEM rights (code execution in the agent or the service);
  - **G2** disable or blind the agent;
  - **G3** fake or suppress usage (less time, or another app's name);
  - **G4** destroy evidence (flood the log or the history).
- **Assets**: the SYSTEM service and agent, the integrity of the usage data, the service log.

### Attack surface: what kid-controlled data reaches which SYSTEM process

| Process | Kid-controlled input | Notes |
|---|---|---|
| **Agent** (SYSTEM, kid's desktop) | Window metadata of the kid's windows: class names (≤ 256 characters), styles, sizes, owner relations, cloak flags, PIDs | No titles, no messages, no files, no network. Integers and short strings compared with constants. |
| **Service** (SYSTEM, session 0) | The agent's reports (indirectly kid-influenced); process image paths, process owners, creation times; file content of the kid's programs (PE version resource); Store package manifests and resources | The heavy parsing is in the service, not in the agent. This exists with any sensor design, because the service must name the apps. |

### Threats, ratings and mitigations

Rating: likelihood (L) and impact (I) as Low / Medium / High; impact "Critical" = SYSTEM code execution.

| ID | Threat | L | I | Mitigations (§7 item) | Residual risk |
|---|---|---|---|---|---|
| T-1 | **Code execution through parsing kid-controlled files in the service** (malformed PE/version resource, MUI tricks, package manifest, resource strings). | Low | Critical | No kid file is ever loaded as code; no icon extraction, shell API or COM handler (12). Untrusted locations are parsed only by the managed, bounds-checked `VersionResourceReader`, which reads only `RT_VERSION`, ≤ 64 KB, and catches every error (13). The Windows version API is used only for admin-only locations (13). XML with DTD prohibited and a size limit; only service-built resource references are passed to `SHLoadIndirectString` (15). Names sanitized and length-limited (16). | **Low.** Remaining: bugs in the Windows APIs for admin-only files and in `SHLoadIndirectString` for installed packages. That content cannot be changed by the kid. |
| T-2 | **Confused deputy / NTLM relay**: the kid runs a program from `\\attacker\share`, WebDAV, a junction or a device path, so the SYSTEM service opens that path and authenticates as the computer account, or reads a protected file. | Medium | High | Only local fixed-drive paths are opened; UNC, `\\?\UNC\`, `\\.\` device, network and removable paths are never touched (13). The final path is checked (`GetFinalPathNameByHandle`). File access runs impersonating the kid (14), so it can never read more than the kid can. | **Low.** |
| T-3 | **Shatter / window messages / hooks into the agent** (the SYSTEM process on the kid's desktop): `SendMessage` attacks, `SetWindowsHookEx` or `SetWinEventHook` DLL injection, accessibility/UIA. | Low | Critical | The agent has **no window**, no message-only window, no message loop, no hooks, no COM/STA, no shell/DPI/UIA APIs, and calls only non-messaging window functions (8). It runs at **System integrity**: UIPI blocks messages and hook injection from Medium processes, and in-context hooks need a message-processing GUI thread, which the agent does not have. `uiAccess` programs must be signed and installed in protected folders (admin). | **Low.** |
| T-4 | **Opening the agent process**: terminate, suspend, debug, read or write memory, create remote threads, duplicate its pipe handles. | Medium (attempts) | High | Explicit process and thread DACL: kid has **no access** (2). Mandatory integrity "no write up" (Medium → System) as a second barrier. The kid has no `SeDebugPrivilege`. The pipes are unnamed, so the kid has no handle to them. | **Low.** |
| T-5 | **Blast radius if the agent were compromised** after all. | Low | Critical | Token without privileges except `SeChangeNotify` (no `SeDebug`, `SeTcb`, `SeImpersonate`, `SeAssignPrimaryToken`, `SeBackup`/`SeRestore`, `SeLoadDriver` …); Administrators deny-only; write-restricted if the runtime allows it (1). No network endpoint; stdout goes only to the service, which treats it as untrusted (11). | **Medium → Low.** The user SID is still SYSTEM, so a compromised agent could still read most files. Without the write restriction (fallback), it could also write where SYSTEM may write. |
| T-6 | **Code injection through the start environment**: `DOTNET_STARTUP_HOOKS`, `COR_PROFILER`/`CORECLR_PROFILER`, `PATH` DLL search, current directory, command-line parsing. | Low | Critical | Explicit minimal environment (never the kid's, not even the service's) (4). Startup hooks disabled in `runtimeconfig` (7). `SetDefaultDllDirectories` (7). Current directory = installation folder; absolute application name; fixed argument (3). The installation folder is writable only by admins (installer ACL; DEV verifies it). | **Low.** |
| T-7 | **Handle leakage** into the agent. | Low | Medium | Handle list with exactly the three pipe ends (5). | **Low.** |
| T-8 | **Malicious data on the channel** (malformed JSON, huge lines, flooding) — the agent is trusted, but a bug or a kid-influenced report must not harm the service. | Low | Medium | Line and entry limits, strict schema, PID validation, keep-latest queue per session (11). The kid cannot write to the pipe (T-4). | **Low.** |
| T-9 | **Blinding the agent (G2)**: crash or hang it, exhaust it with masses of windows, make it miss its heartbeat. | Medium | Medium | No messaging APIs, so a hung kid window cannot block the scan (8). Window cap and `truncated` warning (9). The agent has no other input that could crash it. Heartbeat check → pause, restart, Warning, then Error after 3 failed restarts (18). The kid cannot end, suspend or debug it (T-4). | **Low–Medium.** If the agent cannot run, usage is not recorded and the gap is visible **only in the service log** (see Q-6 in the US-004 plan). |
| T-10 | **Evading detection by window manipulation (G3)**: a kid's tool gives a game window `WS_EX_TOOLWINDOW`, an owner, or an app-cloak (`DwmSetWindowAttribute`), or fakes a window class (`CabinetWClass`, `ApplicationFrameWindow`). | Medium | Medium (US-004: under-reported usage); High for future time budgets | Explorer and Store-frame kinds are honoured only for the real system programs by path (§5). EagleEye's own processes are excluded by path, not name (§5). **Residual design limit:** a window deliberately turned into a tool window or owned window drops out of the "Apps" rule (Task Manager behaves similarly). An app-cloaked window is invisible on screen, so nothing is used. | **Medium, accepted for US-004 (observation only).** Enforcement is **process-based and deny-by-default** (ADR-005), so blinding the usage sensor cannot start a blocked program. The budget story must decide whether budgets for allowed apps also count processes without an app window (noted for that story). |
| T-11 | **Misleading identity (G3)**: the kid copies or renames a game to `notepad.exe` with version info "Editor", or uses hardlinks or junctions to system programs. | Medium | Low | The identity is the real image path (`QueryFullProcessImageName`, final path), so a copy is a separate app record with a user-writable path. Path, process name and display name are in the log and the database. Only the display name is shown in the report. | **Medium–Low.** A disguised game appears as "Editor" in the report. The parent can see the real path in the log (see Q-8). |
| T-12 | **Flooding the log and history (G4)** by opening and closing windows in a loop, pushing older entries out of the 3 × 50 MB / 5-day log rotation. | Low | Medium | 5 s merge of reopenings; log limit of 30 start/end entries per app per hour, then one summary line (17). The log folder is admin-only (US-003), so the kid cannot read or delete it. | **Low.** |
| T-13 | **Debug back doors in Release** (`EAGLEEYE_DEV_WATCH_SID`, `EAGLEEYE_DATA_DIR`, Debug launcher). | Low | High | `#if DEBUG` only; installers package Release builds; TES checks that the variables have no effect. | **Low.** |

### Comparison: does the agent need SYSTEM?

| Variant | Can it see the windows? | Kid can end, suspend or inject? | Kid can fake reports? | Attack surface for G1 | Verdict |
|---|---|---|---|---|---|
| **A. Agent with the kid's own token, treated as untrusted** | yes | **yes** (own process, same integrity) | **yes**: inject into it, or write to its pipe via duplicated handles | none (no SYSTEM code on the desktop) | Blinding and fake reports are trivial for a tech-savvy kid. Acceptable for observation only, but useless as a basis for budgets (FR-SVC-012 is meant to feed them). |
| **B. Agent as `LocalService` or another low-privileged service account** | only after adding ACEs to the kid's window station and desktop (fragile, must be undone at logoff) | no (DACL, System integrity) | no | similar to C; `LocalService` holds `SeImpersonatePrivilege` unless removed | More moving parts (desktop ACL editing) for little gain over C. |
| **C. Hardened SYSTEM agent (chosen)**: no privileges, Administrators deny-only, write-restricted if possible, System integrity, no windows | yes | no | no | Minimal: integers and class names compared with constants; the agent reads no files, no titles, no network | Chosen. |
| D. Service in session 0 only | no | — | — | — | Cannot meet AC-3 to AC-5 (see Alternatives). |

**Result: the decision stays "SYSTEM agent", but hardened (§7).** System integrity is itself a security feature here: UIPI keeps the kid's processes from sending messages to the agent or injecting hooks into it, and a lower integrity would remove that barrier. A less privileged agent (variant A) would be enough to *see* the windows. It would not be enough to *trust* what it reports, because the kid could switch it off or feed it, and the product needs trustworthy usage for budgets. The agent's own attack surface is small. The real untrusted-data processing (file and package parsing) is in the service in every variant, and is mitigated there (T-1, T-2).

### Residual risks needing Michael's decision

These are listed as open questions in the US-004 plan:
- **T-9**: visibility of recording gaps to the parent.
- **T-10**: usage can be under-reported by deliberate window manipulation.
- **T-11**: disguised display names.
- **T-5 fallback**: the agent may run without the write restriction if the runtime does not start with it.

---

## References

- US-004: `02_Implementation/docs/requirements/user-stories/US-004/user-story.md` (AC-1 to AC-6, AC-25 to AC-27) and its implementation plan
- ADR-004 (tray client is read-only), ADR-005 (classification; amended for usage recording), ADR-006 (graceful close needs the session), ADR-012 (usage accounting)
- arc42 §5.2 (Monitoring), §6.2, §7, §11 R-6

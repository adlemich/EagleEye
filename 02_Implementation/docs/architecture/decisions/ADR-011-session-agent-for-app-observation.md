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
- The agent runs under the **SYSTEM** account. A standard user cannot end it, suspend it or inject into it. (The tray client runs as the kid and can be ended by the kid; see Alternatives.)
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

The service never records these processes, whatever their windows: EagleEye's own processes (`EagleEye.TrayClient.exe`, `EagleEye.Service.exe`), so the tray client's pairing window and About dialog never appear (US-004 AC-5); and processes in session 0.

### 6. Reuse by later stories

Enforcement stories will reuse the agent for window-level actions in the kid's session. Graceful close with `WM_CLOSE` (ADR-006) also requires being in that session. Those actions will need a service → agent command channel (stdin messages). That channel is out of scope here; it is added by the story that needs it, without changing this ADR's structure.

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

## References

- US-004: `02_Implementation/docs/requirements/user-stories/US-004/user-story.md` (AC-1 to AC-6, AC-25 to AC-27) and its implementation plan
- ADR-004 (tray client is read-only), ADR-005 (classification; amended for usage recording), ADR-006 (graceful close needs the session), ADR-012 (usage accounting)
- arc42 §5.2 (Monitoring), §6.2, §7, §11 R-6

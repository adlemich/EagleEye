# ADR-013: Break-Time Enforcement at App Start — Blocked Starts, Close Sequence and Kill Set

**Status**: Accepted (approved by Michael with the US-005 implementation plan, 2026-10-10; §3 and §9 reflect his answers Q-1 and Q-2)
**Date**: 2026-10-10
**Deciders**: ARC, Michael

---

## Context

US-005 is the first enforcement story. During a break time ("Ruhezeit", TC-010 to TC-014 v1.5) an app that a controlled account starts (start = creation of its first process) must be detected within 10 s after the start (or after its first window, if that appears more than 10 s later), closed gracefully, terminated by force with all its processes if it has not closed 20 s later, and be gone at most 30 s after the start (FR-SVC-023 v1.5, US-005 AC-21 to AC-29). Apps already running when a break time begins keep running (AC-25). A blocked start creates no usage (AC-24) and is recorded in a history (FR-SVC-025, AC-35).

The existing design does not cover this as written:

1. **ADR-006** lets "the service" send `WM_CLOSE` to the process's main window and then call `Process.Kill(entireProcessTree: true)`. The service runs in **session 0**. Window stations and desktops are per session, so the service cannot find or message windows in the kid's session (ADR-011 Context). `Process.Kill(entireProcessTree: true)` kills every descendant regardless of session, owner, exemptions or whether a child is an allowed app that was already running.
2. **ADR-011 §8** forbids the session agent to call `PostMessage*`, and §6 leaves the service → agent command channel to "the story that needs it". This is that story.
3. ADR-005 describes enforcement by polling all processes and an `IgnoreList` table. US-005 enforces only at **app start**, for **apps** in the US-004 sense (Task Manager "Apps" group, ADR-011 §4), and the table does not exist.
4. "Start" must be defined so that service restarts, agent restarts, sign-in (Autostart) and apps that already run before the break time behave as the story requires.

---

## Decision

### 1. Where the decision is made

The **service** decides, in the accounting loop (`UsageAccountingLoop`, the single consumer of agent reports, ADR-012 §4), **before** the observed apps reach the `UsageTracker`. A new collaborator `BreakTimeGate` screens every `AppsObserved` of a controlled account:

- It removes **blocked processes** from the report, so the tracker never sees them: no app record, no instance, no daily-usage row, no "App started" log line (AC-24). A blocked process stays excluded (by PID and creation time) until it has exited.
- For each new blocked start it starts a **close sequence** (§4) that runs outside the loop.

The break-time rules are evaluated from an **in-memory snapshot** held by the rules state owner (`BreakTimeService`, ADR-010), loaded from SQLite at service start and replaced inside the write lock on every change. Evaluation costs microseconds and needs no database access (AC-26, AC-27, AC-29).

### 2. "In effect" (TC-014, AC-21)

An entry is in effect at the local date-time *t* of the service PC when it is active, the local weekday of *t* is ticked, and `start ≤ t.TimeOfDay < end'`, where `end' = 24:00` if the stored end is 23:59 and `end' = end` otherwise. Times are stored as minutes after midnight (`0 … 1439`).

- Local time is `TimeZoneInfo.ConvertTime(TimeProvider.GetUtcNow(), TimeProvider.LocalTimeZone)`. Wall-clock semantics follow automatically: on the day clocks go forward, local times in the skipped hour never occur; on the day clocks go back, the repeated hour is in effect in both passes (OQ-5).
- .NET caches `TimeZoneInfo.Local` for the life of the process. The accounting loop calls `TimeZoneInfo.ClearCachedData()` at every 5 s tick, so a change of the time zone takes effect within 5 s (OQ-5). A change of the zone is logged as a Warning and stored as a finding (§9).

### 3. What a blocked start is

**A start is the creation of the app's first process** (decided by Michael with the US-005 plan, Q-1; story Terms, AC-21, FR-SVC-023). The app is still *recognised* by its window: the agent reports the processes that have an app window (ADR-011 §3), and an app is identified per account by its program path (ADR-012 §1). Per report of a controlled account that has at least one active entry:

| Situation | Decision |
|---|---|
| The app (program path) is **open** for the account (the tracker has an instance, including its 5 s merge window) | Not a start. Further processes and sub-processes of an open app are never starts. |
| The app is **being closed** (a close sequence of this account and path runs) | Not a new start. Newly reported window processes of it are excluded from usage and **added to the running sequence** (own close command; covered by its force step) (story OQ-17). |
| The app **becomes open** (its first app window is reported; also the first report after a service or agent (re)start) | **Start time** = the **earliest creation time of the running processes with that program path** in the session, owned by the account. **Blocked** iff an entry of the account was in effect at that local time (AC-21: *t* = creation time of the first process). |

Consequences, stated plainly:
- An app whose first process was created before the break keeps running, even if its window appears only after the break began (slow start), and even after a service or agent restart (AC-25).
- A program that waits **in the notification area** since before the break (Steam, Discord) and opens its window during the break is **not** blocked: its first process was created before (accepted consequence of Q-1).
- An app started during a break while no agent was running (sign-in, agent restart, boot) is blocked when its window is first reported; detection is then later than 10 s (story AC-22 note).
- Because the decision uses the creation time, a start in a break that is detected only after the break ended is still blocked.
- The 10 s / 30 s bounds count from the start, or from the first window if that appears more than 10 s after the start (story AC-22).
- Cost: only if the earliest creation time among the reported window processes lies in a break does the service read the process table to look for an older process of the same path.
- Each blocked start records the entry in effect at the start time (the first by entry order) and its trigger (`app start` or `found at agent start`).

### 4. Close sequence (ADR-006 for blocked starts)

Per blocked start, an asynchronous `BlockedStartSequence`:

1. **t₀ = detection.** Insert the history record (outcome open), log "Blocked start …", and in parallel ask the tray client of the session to show the display text (ADR-014). The message never delays the close.
2. **Graceful close (session agent).** The service sends the agent of that session a close command for the blocked window processes (PID + creation time). The agent verifies each process's creation time, and posts **`WM_CLOSE`** with `PostMessageW` to every top-level window of that process that the "Apps" rule counts (for a Store app: to its `ApplicationFrameWindow`). It answers with the number of windows it posted to. `PostMessage` is asynchronous, so a hung app cannot block the agent. If the agent is not running or does not answer, the sequence continues with step 3; the force kill still happens in time.
3. **Wait up to 20 s** (fixed by FR-SVC-023 v1.5 for blocked starts; ADR-006's configurable global timeout is not introduced) for all processes of the kill set to exit, on process handles opened with `SYNCHRONIZE | PROCESS_QUERY_LIMITED_INFORMATION | PROCESS_TERMINATE` at t₀ (identity = PID + creation time).
4. **Force (service).** At t₀ + 20 s the service recomputes the kill set (processes that appeared since, e.g. new renderer processes), and calls `TerminateProcess` on every member, through a handle whose creation time was verified. Up to 3 rounds of "recompute, terminate, wait 1 s" until nothing is left.
5. **Done.** Outcome `closed gracefully` (everything gone within 20 s) or `terminated by force` (anything left at 20 s), with the seconds from t₀ until gone, recorded in the history and in one log entry (AC-28).

The **service** (full SYSTEM in session 0) terminates; the agent never terminates processes. The service can open the kid's processes with `PROCESS_TERMINATE`: SYSTEM is granted full access by the default process DACL, and System integrity is higher than the kid's Medium (or AppContainer) integrity.

**Service stop during a sequence**: open sequences terminate their kill sets at once and record `terminated by force (service stopping)`. A record left open by a crash is completed at the next start with outcome `unknown (service stopped)`.

### 5. Kill set and exemptions

The kill set of a blocked start is computed in the service from a process snapshot (`CreateToolhelp32Snapshot`: PID, parent PID) and the process facts (`IProcessInspector`: session, owner SID, creation time, image path):

- **Roots**: all processes of the program path in the session, owned by the account (plus processes added to the running sequence, §3).
- **Descendants**: processes whose parent is in the set and that were created after their parent (PID-reuse guard), recursively.
- **Always removed from the set**:
  - processes in another session or with another owner SID;
  - EagleEye's own programs (by installation path, ADR-011 §5);
  - the real `%SystemRoot%\explorer.exe` and `%SystemRoot%\System32\ApplicationFrameHost.exe` (by path; File Explorer windows are never closed, OQ-3);
  - the **enforcement ignore list** (§6);
  - processes that belong to **another app that is open and not blocked** for the account (same program path as an open app), and their descendants. Example: Notepad has been open since 19:00; a program started in the break opens another Notepad process; when that program is blocked, the Notepad process stays, because Notepad is an open, allowed app (AC-25).

A Store app's kill set is its app process (e.g. `CalculatorApp.exe`), never the shared `ApplicationFrameHost.exe`.

### 6. Enforcement ignore list (ADR-005 Tier 1 for US-005)

A **shipped, code-defined** list of Windows programs that are never closed or terminated, matched by **full path under `%SystemRoot%`** (not by name, ADR-011 T-10): `explorer.exe`, `System32\ApplicationFrameHost.exe`, `dwm.exe`, `csrss.exe`, `winlogon.exe`, `sihost.exe`, `ctfmon.exe`, `conhost.exe`, `fontdrvhost.exe`, `taskhostw.exe`, `svchost.exe`, `dllhost.exe`, `RuntimeBroker.exe`, `ShellExperienceHost.exe`, `StartMenuExperienceHost.exe`, `SearchHost.exe`, `TextInputHost.exe`, `LockApp.exe`, `LogonUI.exe`, `consent.exe`, `WerFault.exe`, `smartscreen.exe`, `SecurityHealthSystray.exe` (exact list in `EnforcementIgnoreList`, unit-tested). The `IgnoreList` table of ADR-005 is not introduced; the list changes with releases, as ADR-005 intends. Task Manager and the Settings app are **not** on the list (OQ-3).

### 7. History and logs

- Table `BlockedStarts` (migration 4): one row per blocked start with time of the start (process creation, UTC and local), time of detection, account SID and user name, display name, process name, program path, PID, entry (id, start, end, days) and the local weekday, outcome, seconds until gone, message state. No foreign key to the entry (entries can be deleted later). Purged after 90 days (FR-SVC-043) and with the account (FR-SVC-047). Not shown in the parent app (OQ-18).
- Log: "Blocked start …" at detection, one summary entry at the end with outcome and message state (AC-28); Information when a break time of a controlled account begins and ends (evaluated at every tick), for observability.

### 8. Security notes

- The agent's new capability is limited to `PostMessageW(hwnd, WM_CLOSE, 0, 0)` on windows it has itself classified as app windows of a PID the service named, after verifying the creation time (ADR-011 amendment). It still has no window, no message loop, no hooks and never uses `SendMessage*`. UIPI allows messages from System integrity to lower integrity; the agent's restricted token needs no object access for posting.
- The command channel is the agent's inherited stdin pipe; only the service can write to it. The agent parses it strictly (line limit, schema).
- Termination stays in the service, so the hardened agent does not need `PROCESS_TERMINATE` rights over the kid's processes.
- **Bypass risks** (accepted by Michael with the US-005 plan, Q-2, Q-3; arc42 R-13, R-14): a kid can change the **time zone** (Windows grants `SeTimeZonePrivilege` to Users by default) and so shift the local time out of a break time — the service detects, logs (Warning) and stores every such change as a finding (§9), but does not prevent it; a kid with tools can strip a window's app status (ADR-011 T-10) so that no app start is seen. Break-time enforcement is window-based by story; process-based deny-by-default enforcement (ADR-005) comes with allow-lists.

### 9. Time-zone and clock findings (story AC-37, FR-SVC-026)

At every 5 s tick, after `TimeZoneInfo.ClearCachedData()`, `TimeChangeMonitor` compares the local time zone with the previous tick (`Id`, `BaseUtcOffset`, `SupportsDaylightSavingTime`; regular DST transitions are not changes) and the elapsed wall-clock UTC time with the elapsed monotonic time (difference > 30 s = clock jump; intervals with suspend/resume or a gap > 15 s are not judged). Each change is logged as a Warning and stored in `TimeChangeFindings` (time, kind, old and new value, the controlled session in use at the time if exactly one — a hint, not proof; Windows does not report who changed the zone). The first tick after service start is the baseline. Retention 90 days; rows with an account are purged with the account. Not shown in the parent app in US-005.

---

## Rationale

### Alternatives Considered

| Option | Reason Rejected |
|---|---|
| Service sends `WM_CLOSE` itself (ADR-006 as written) | Impossible from session 0: the kid's windows are on another window station. |
| Agent closes **and** terminates | The agent's token was stripped to `SeChangeNotify` on purpose (ADR-011 §7); giving it terminate rights over the kid's processes enlarges the blast radius of the SYSTEM process on the kid's desktop. The service already holds those rights. |
| Tray client closes the app (runs as the kid, same desktop) | The kid can end the tray client (R-6), and enforcement must not depend on it (NFR-R-012, AC-33). |
| `Process.Kill(entireProcessTree: true)` | Ignores session, owner and exemptions; would kill allowed apps that were started from the blocked program before the break time (AC-25), and cannot verify PID reuse for the root. |
| `SendMessage(WM_CLOSE)` or `WM_SYSCOMMAND/SC_CLOSE` | `SendMessage` blocks on hung windows (ADR-011 T-9). `SC_CLOSE` behaves like `WM_CLOSE` for ordinary windows but is ignored by more apps; no gain. |
| `WM_CLOSE` only to the "main window" (`CloseMainWindow`) | The main-window heuristic is not available across sessions and misses multi-window apps; all *app windows* of the process are closed, as the user would by clicking each close button. |
| Block on process creation (ETW / WMI process-start events, kernel callbacks) | Detects console and background processes too, which the story exempts; still needs the window decision; kernel callbacks need a driver. The 1 s window scan meets the 10 s bound. |
| "Start" = appearance of the app's first window (ARC's first proposal) | Michael decided for the first process (Q-1): a slow app started before the break must not be blocked; the price is that a program waiting in the notification area since before the break may be opened during it. |
| "Start" = every first-seen process, also after a restart | A service restart during a break time would close every app that is running, against AC-25. |
| Additional processes of an open app created in a break are blocked (ARC's first proposal "T2") | Michael decided that further processes of an open app are not starts (Q-1). |
| Removing the time-zone right from *Users* in the installer | Out of scope for US-005 (Q-2); a possible later hardening. |
| Global configurable timeout (ADR-006, FR-SVC-021) | US-005 fixes 20 s (FR-SVC-023 v1.5); the YAML setting is not needed yet. |

---

## Consequences

### Positive

- Enforcement works without a parent app, after restarts and reboots (AC-27), with the rules in memory.
- The usage of US-004 stays untouched by blocked starts (AC-24).
- The kill set is explicit, testable and honours AC-23 and AC-25.
- No new process, no new deliverable: the existing agent gains one narrowly defined action.

### Negative / Trade-offs

- Apps that hide in the notification area on close (Steam, Discord) or keep background processes (Edge with startup boost) usually end with outcome `terminated by force` after 20 s, although their window closed at once.
- Store apps are often suspended rather than ended after their window closes; they are also often `terminated by force`.
- Detection can exceed 10 s in rare windows: right after sign-in (the agent starts at the next 5 s tick), after an agent restart (back-off 1, 5, 30 s) or while the service starts. The creation-time rule catches these starts late, not never.
- Programs waiting in the notification area since before a break can be opened during it (Q-1).
- Window-based detection can be evaded by window-style manipulation (ADR-011 T-10) and by a time-zone change (§8).

### Neutral

- ADR-006's warnings, budgets and pause-window start (TC-040 to TC-042) are not affected; the next story reuses the close sequence for running apps.

---

## Relation to existing decisions

| Decision | Effect |
|---|---|
| ADR-005 | Amended: enforcement ignore list as a shipped, path-based code list (§6); US-005 enforces at app start for apps only. |
| ADR-006 | Amended: who sends `WM_CLOSE` (the session agent), to which windows (all app windows), timeout 20 s for blocked starts, kill set instead of `Process.Kill(entireProcessTree: true)`. |
| ADR-011 | Amended: §6 command channel (stdin), §8 `PostMessageW(WM_CLOSE)` as the one allowed message call. |
| ADR-012 | Amended: blocked processes never reach usage accounting. |
| ADR-010 | Applied: state area `AccountRules:{sid}` with field-level write commands (US-005 plan). |
| ADR-014 | The kid's message through the tray client. |

---

## References

- US-005: `02_Implementation/docs/requirements/user-stories/US-005/user-story.md` and `implementation-plan.md`
- Requirements v1.5: FR-SVC-023, FR-SVC-024, FR-SVC-025, FR-SVC-043, FR-SVC-047, TC-010 to TC-014
- ADR-005, ADR-006, ADR-010, ADR-011, ADR-012, ADR-014

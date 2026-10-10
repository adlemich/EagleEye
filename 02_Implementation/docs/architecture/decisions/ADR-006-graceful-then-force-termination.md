# ADR-006: Graceful-Then-Force Process Termination Pattern

**Status**: Accepted (amendment for blocked starts proposed with the US-005 implementation plan)
**Date**: 2026-09-20
**Deciders**: Michael (project owner), ARC

> **Amendment 2026-10-10 (US-005, ADR-013; proposed, approved with the US-005 implementation plan)**
>
> 1. **Who sends `WM_CLOSE`.** The service runs in session 0 and cannot reach windows in user sessions. The graceful phase is carried out by the **session agent** of the kid's session (ADR-011, amended): on a command from the service it posts `WM_CLOSE` (`PostMessageW`, never `SendMessage`) to **every app window** of the affected processes (for Store apps: the `ApplicationFrameWindow`). "Main window" and `Process.CloseMainWindow()` are not used.
> 2. **Who terminates.** The **service** terminates with `TerminateProcess` on verified handles (PID + creation time). `Process.Kill(entireProcessTree: true)` is **not** used: it ignores session, owner and exemptions and would kill allowed apps started from the blocked program before a break time. The set of processes is the **kill set** of ADR-013 §5 (program path or single process, plus descendants, minus exemptions and other open apps), recomputed at the force step.
> 3. **Timeout.** For break-time blocked starts the timeout is **20 s, fixed** (FR-SVC-023 v1.5). The configurable global timeout (`enforcement.graceful_shutdown_timeout_seconds`) is not introduced in US-005.
> 4. **Processes without app windows** in the kill set are not asked to close; they are terminated at the force step if still running.
> 5. **Events.** US-005 pushes no enforcement event to parent apps (US-005 OQ-18); the kid's message goes through the tray client (ADR-014). The outcome is logged and stored in the history of blocked starts (ADR-013 §7).

---

## Context

EagleEye terminates processes in three scenarios:

1. **Blocked app detected**: a process that is neither ignored nor on the allow-list (ADR-005, Tier 3)
2. **Budget expired**: an allowed application's daily time budget reaches zero
3. **Pause window activated**: a pause window begins and all non-ignored applications for the user must be shut down

In all three cases, the process must be terminated. The question is *how* — immediate force-kill risks data loss (unsaved documents, game progress), but waiting indefinitely undermines enforcement.

The child is expected to be actively using the application in scenarios 2 and 3. The system should give a reasonable opportunity to save work before forcing termination.

Requirements: FR-SVC-020 through FR-SVC-023.

---

## Decision

We will use a **two-phase termination pattern**: graceful shutdown first, followed by force-kill after a configurable timeout.

### Phase 1: Graceful Shutdown

The service sends a `WM_CLOSE` message to the process's main window (if it has one). This is the standard Windows mechanism for requesting an application to close — equivalent to clicking the window's close button. Well-behaved applications respond by prompting to save unsaved work or auto-saving and exiting.

For processes without a visible window (background or console processes), the service calls `Process.CloseMainWindow()`. If the process has no main window, this phase is skipped and the process proceeds directly to Phase 2.

### Phase 2: Force-Kill

If the process has not exited within the configurable timeout (default: 30 seconds), the service calls `Process.Kill(entireProcessTree: true)` to forcefully terminate it, including any child processes.

### Timeout Configuration

- **Default**: 30 seconds (configurable via `EagleEye.Service.yaml` → `enforcement.graceful_shutdown_timeout_seconds`)
- **Minimum**: 5 seconds (enforced by validation — shorter values are clamped to 5)
- **Maximum**: 120 seconds (enforced by validation — longer values are clamped to 120)
- The timeout is a **global setting** that applies to all termination scenarios uniformly. Per-app or per-scenario timeouts are not supported in v1.

### Termination Sequence

```
1. Service decides a process must be terminated (blocked / budget expired / pause window)
2. Log the termination intent (app name, user SID, reason)
3. If process has a main window:
     a. Send WM_CLOSE to the main window
     b. Start timeout countdown
     c. Poll process.HasExited at short intervals (e.g., every 500ms)
     d. If process exits within timeout → log success, done
     e. If timeout expires → proceed to step 4
   Else (no main window):
     → proceed directly to step 4
4. Call Process.Kill(entireProcessTree: true)
5. Verify process has exited
6. Log the outcome (graceful exit or force-kill)
7. Push enforcement event to connected clients:
   - TrayClient: OnAppTerminated(appName, reason)
   - Parent apps: OnBlockedAppTerminated / OnBudgetExpired / OnPauseWindowActivated
```

### Process Tree Handling

`Process.Kill(entireProcessTree: true)` (available in .NET 5+) terminates the target process and all its child processes. This prevents orphaned child processes from continuing to run after the parent is killed. This is important for applications that spawn helper processes (e.g., game launchers, browser renderer processes).

### Pre-Termination Warnings (Budget and Pause)

For budget expiry and pause-window activation, the system sends warnings *before* termination begins, giving the child advance notice to save their work:

| Warning | Default Timing | Configurable |
|---------|---------------|-------------|
| Budget early warning | 5 minutes before budget reaches zero | Yes (`enforcement.budget_early_warning_minutes`) |
| Budget last warning | 1 minute before budget reaches zero | Yes (`enforcement.budget_last_warning_minutes`) |
| Pause early warning | 5 minutes before pause window starts | Yes (`enforcement.pause_early_warning_minutes`) |
| Pause last warning | 1 minute before pause window starts | Yes (`enforcement.pause_last_warning_minutes`) |

Warnings are pushed via SignalR to the TrayClient, which displays them as notification popups. **Warnings are advisory only** — they do not delay enforcement. When the budget reaches zero or the pause window begins, the termination sequence starts regardless of whether the child has acted on the warning.

For blocked-app termination (Tier 3, ADR-005), **no warning is given** — the process is terminated as soon as it is detected. Blocked apps are unauthorized; there is no expectation that the child should have time to "save work" in an app they should not be running.

---

## Rationale

### Why WM_CLOSE before Kill?

`WM_CLOSE` triggers the application's normal close handler, which typically:

- Prompts to save unsaved work (documents, game progress)
- Performs cleanup (releasing file locks, flushing buffers)
- Exits gracefully

Force-killing without this phase would cause data loss in applications that don't auto-save. Since the child is often actively using the application (especially in budget-expiry and pause-window scenarios), giving 30 seconds to save is a reasonable compromise between user experience and enforcement strictness.

### Why 30 seconds default?

30 seconds is long enough for most applications to auto-save and shut down (games typically save in 5–10 seconds). It is short enough that a child cannot meaningfully extend their usage time by stalling the shutdown.

### Why not per-app timeouts?

Per-app timeouts add configuration complexity for parents with minimal benefit. The graceful-shutdown timeout is a safety mechanism, not a user-facing feature. A single global value keeps configuration simple. Per-app timeouts can be added in a future version if real usage data shows a need.

### Why kill the entire process tree?

Many modern applications spawn child processes (browser tabs, game helpers, updaters). Killing only the parent process would leave children orphaned and potentially still consuming resources or displaying windows. `Process.Kill(entireProcessTree: true)` handles this cleanly.

### Why no warning for blocked apps?

Blocked apps are unauthorized — the child should not be running them in the first place. Giving a warning would provide a window of usage for a forbidden application, undermining enforcement. Immediate termination is the correct response.

### Alternatives Considered

| Option | Reason Rejected |
|--------|----------------|
| Immediate force-kill only (no graceful phase) | Causes data loss; frustrates children who lose unsaved work; undermines trust in the system |
| Graceful only (no force-kill fallback) | Processes can ignore `WM_CLOSE` indefinitely, defeating enforcement entirely |
| `Process.Kill()` without `entireProcessTree` | Orphaned child processes continue running (browser tabs, game helpers) |
| Configurable per-app timeouts | Over-complicated for v1; global timeout is sufficient; can be added later |
| Send `WM_CLOSE` to all windows (not just main) | Risk of confusing applications with multiple windows; `CloseMainWindow()` is the standard .NET approach |
| Progressively aggressive shutdown (SIGTERM → SIGINT → Kill) | Windows does not have POSIX signal semantics; `WM_CLOSE` is the Windows equivalent of a graceful shutdown request |

---

## Consequences

### Positive

- **Data preservation**: 30-second graceful phase gives applications time to auto-save
- **Reliable enforcement**: force-kill guarantees termination even if the app ignores `WM_CLOSE`
- **Complete cleanup**: process-tree kill prevents orphaned child processes
- **Advance notice**: budget and pause warnings give the child 5 + 1 minutes to save manually before termination even begins
- **Simple configuration**: one global timeout, clearly documented defaults

### Negative / Trade-offs

- A malicious or buggy application could show a "save" dialog during the 30-second window, buying the child some extra time — the force-kill phase caps this at the timeout value
- Console applications without windows skip directly to force-kill (no graceful phase possible) — acceptable since most child-facing applications are windowed
- The child sees the app close without explicit user interaction, which may feel abrupt — mitigated by the advance warning notifications

### Neutral

- The enforcement component is stateless — it does not track whether a process was previously sent `WM_CLOSE`. Each poll cycle re-evaluates from scratch. If a process reappears after being killed (e.g., auto-restart), it will be classified and terminated again.
- The same termination mechanism is used for all three scenarios (blocked, budget expired, pause window), ensuring consistent behavior.

---

## References

- System Architecture: `02_Implementation/docs/architecture/arc42/system-architecture.md` — sections 5.2 (Enforcement component), 6.2 (Process Monitoring sequence), 6.3 (Pause Window sequence)
- Product requirements: FR-SVC-020 through FR-SVC-023
- ADR-005: Process Classification Strategy — defines when termination is triggered (blocked or budget/pause enforcement)
- .NET API: `Process.Kill(bool entireProcessTree)` — <https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.process.kill>
- .NET API: `Process.CloseMainWindow()` — <https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.process.closemainwindow>

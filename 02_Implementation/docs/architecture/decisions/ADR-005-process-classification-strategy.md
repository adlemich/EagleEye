# ADR-005: Process Classification Strategy — Ignore, Allow, Block

**Status**: Accepted
**Date**: 2026-09-20
**Deciders**: Michael (project owner), ARC

---

## Context

EagleEye's core function is controlling which applications a child can run on Windows. The service monitors all processes running under standard-user (non-admin) sessions and must decide what to do with each one. The decision must be fast (evaluated every poll interval), correct (never terminate essential OS processes), and safe (default to denying unknown applications).

A classification strategy is needed that balances:
- **Safety**: essential Windows processes must never be terminated
- **Security**: unknown applications must be blocked by default (deny-by-default)
- **Usability**: parents should only need to manage the apps they care about, not hundreds of system processes
- **Performance**: classification must be O(1) or near-O(1) per process

Requirements: FR-SVC-010 through FR-SVC-017, FR-SVC-020 through FR-SVC-023.

---

## Decision

We will use a **three-tier classification** model evaluated in priority order. Every process running under a standard-user session is classified into exactly one category:

### Tier 1: Ignored (system processes)

A **shipped, non-configurable** list of essential Windows processes that are required for a functioning user session. These are never terminated and never tracked.

**Matching**: by executable file name (case-insensitive). The ignore list is stored in the `IgnoreList` table of `EagleEye.Service.db`, seeded by the installer and updated via application updates.

**Examples**:

| Process | Purpose |
|---------|---------|
| `explorer.exe` | Windows shell, taskbar, file manager |
| `dwm.exe` | Desktop Window Manager (compositing) |
| `taskhostw.exe` | Task host for scheduled tasks |
| `csrss.exe` | Client/Server Runtime Subsystem |
| `conhost.exe` | Console host |
| `sihost.exe` | Shell Infrastructure Host |
| `fontdrvhost.exe` | Font driver host |
| `ctfmon.exe` | Input method framework |
| `RuntimeBroker.exe` | UWP permission broker |
| `ShellExperienceHost.exe` | Start menu, Action Center |
| `SearchHost.exe` | Windows Search UI |
| `TextInputHost.exe` | Touch keyboard, handwriting |
| `svchost.exe` | Service host (user-session instances) |
| `dllhost.exe` | COM surrogate |

**Design rules for the ignore list**:
- Only essential OS processes that, if terminated, would crash or degrade the user session
- Maintained by the development team, not configurable by parents
- Erring on the side of inclusion: if uncertain whether a process is essential, include it — a false ignore is less harmful than crashing a user session
- The list is version-controlled and updated with each EagleEye release

### Tier 2: Allowed (parent-configured)

Applications explicitly placed on the user's allow-list by the parent via the parent app. These are permitted to run but are subject to time-budget tracking and enforcement.

**Matching**: by executable file name (case-insensitive), looked up in the per-user configuration in SQLite (`UserConfig` table, allow-list column).

**Behavior when allowed**:
- Process runs normally
- Usage time is tracked (accumulated per poll interval)
- Time budget is decremented
- Budget warnings are sent when thresholds are reached
- Process is terminated when budget expires or a pause window activates

### Tier 3: Blocked (default)

Any process that is neither ignored nor on the allow-list. **This is the default classification** — deny-by-default.

**Behavior when blocked**:
- Process is terminated immediately using the graceful-then-force pattern (see ADR-006)
- An enforcement event is pushed to all connected parent apps and the user's tray client
- The termination is logged

### Evaluation Order

```
For each process in the user's session:
    1. Is the executable name in the ignore list?
       → YES: skip (no tracking, no enforcement)
    2. Is the executable name in the user's allow-list?
       → YES: track usage, enforce budget/pause rules
    3. Default: BLOCKED
       → terminate (graceful-then-force)
```

The evaluation is performed on every poll cycle. The ignore list and allow-list are loaded into memory (hash sets) for O(1) lookup and refreshed when configuration changes.

### Display Name Resolution

For the parent app UI, processes need human-readable names. The `AppDiscovery` component resolves display names using (in priority order):

1. Executable file version info / product name metadata
2. Windows installed-programs registry entries
3. Optional online lookup (if internet access is granted)
4. Fallback: executable file name

Resolved names are cached in the SQLite `AppNameCache` table. Display-name resolution is decoupled from classification — classification always uses the executable file name, never the display name.

---

## Rationale

### Why deny-by-default?

A deny-by-default (blocklist-everything-except-allowed) model is the only approach that guarantees a child cannot run unauthorized applications. The alternative — allow-by-default with a blocklist — would require the parent to anticipate every possible application the child might install or download, which is impractical.

### Why a separate ignore list instead of a broader allow-list?

The ignore list serves a fundamentally different purpose than the allow-list:
- **Ignore list**: protects system stability. Managed by developers. Not tracked.
- **Allow-list**: implements parental policy. Managed by parents. Time-tracked.

Merging them would force parents to see hundreds of system processes in their configuration UI, or risk accidentally removing an essential OS process from the list.

### Why match by executable name?

Executable name is the simplest, most reliable identifier available at process-enumeration time. It requires no file I/O beyond what `System.Diagnostics.Process` already provides. Alternatives like file path matching would be fragile (apps can be installed in non-standard locations) and slower.

### Why case-insensitive matching?

Windows file system is case-insensitive by default. `Minecraft.exe` and `minecraft.exe` are the same file. The classification must match accordingly.

### Alternatives Considered

| Option | Reason Rejected |
|--------|----------------|
| Allow-by-default with blocklist | Cannot guarantee children don't run unauthorized apps; parent must anticipate all possible applications |
| Match by file path | Fragile (non-standard install locations); slower (path resolution needed); no significant benefit |
| Match by digital signature / publisher | Many legitimate apps are unsigned; adds I/O overhead for certificate validation; overkill for parental control |
| Parent-configurable ignore list | Risk of parents accidentally removing essential OS processes, crashing the session |
| Machine-learning-based classification | Extreme complexity for marginal benefit; opaque decisions; not appropriate for a deterministic enforcement system |

---

## Consequences

### Positive

- **Secure by default**: any unknown application is blocked — no gaps in enforcement
- **Simple mental model**: parents only manage the apps they want to allow; everything else is denied
- **Stable**: essential OS processes are protected by a developer-maintained list — no risk of session crashes from enforcement
- **Fast**: O(1) hash-set lookups per process per poll cycle
- **Clear separation of concerns**: ignore list (developer-managed, stability) vs. allow-list (parent-managed, policy)

### Negative / Trade-offs

- New legitimate applications installed by the parent require explicit allow-listing before the child can use them — by design, not a limitation
- The ignore list requires ongoing maintenance as Windows evolves — new OS processes may need to be added in future EagleEye releases
- A child's session may briefly show a blocked app's window before the next poll cycle detects and terminates it — the poll interval determines the detection latency

### Neutral

- Display-name resolution is best-effort and cached — a missing display name falls back to the executable name, which is always available
- Admin-session processes are never monitored or classified (per MU-014)

---

## References

- System Architecture: `02_Implementation/docs/architecture/arc42/system-architecture.md` — sections 5.2 (Monitoring, Enforcement, AppDiscovery components), 8.8 (Process Classification)
- Product requirements: FR-SVC-010 through FR-SVC-017 (monitoring, classification), FR-SVC-020 through FR-SVC-023 (enforcement)
- ADR-006: Graceful-Then-Force Termination Pattern — the mechanism used when a process is classified as blocked or its budget expires

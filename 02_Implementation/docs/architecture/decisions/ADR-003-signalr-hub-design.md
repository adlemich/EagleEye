# ADR-003: SignalR Hub Design — Two Hubs, Three Communication Patterns, Server-Authoritative State

**Status**: Accepted (Pattern 3 refined by ADR-010, accepted 2026-10-07)
**Date**: 2026-09-20
**Deciders**: Michael (project owner), ARC

> **Note 2026-10-07 (ADR-010, accepted by Michael 2026-10-07)**: Pattern 3 is refined by ADR-010 "Event-Driven State Propagation": snapshots carry a revision and the correlation id of the write that caused them, write commands take a `requestId` and return `StateWriteAckDto`, the broadcast (which includes the sender) confirms the stored state. **Rule 3 changes**: the service no longer pushes state on connect; the client fetches every area it shows after each (re)connect. Rules 1, 2 and 4 stay. The example signatures below are targets that take the ADR-010 shape when their stories come.
>
> **Note 2026-10-10 (US-005, ADR-014; approved by Michael with the US-005 implementation plan, 2026-10-10)**: the tray side is refined by ADR-014. The service binds each tray connection to a **session** itself (owner process of the loopback TCP connection, verified by the installed tray client's path); the client-asserted `RegisterSession(userSid)` and the groups `Tray:{userSid}` below are not introduced. Kid messages go to one verified connection of the session as a request with a result (`ShowBreakTimeMessage` → `KidMessageResult`). `OnShowPairingCode` still goes to all tray connections.

---

## Context

EagleEye uses SignalR as its sole communication technology between the Windows service and all clients (parent apps and tray client). Three distinct communication patterns must be supported:

1. **Data queries** — parent apps request data from the server (configuration, statistics, user accounts, installed apps) and receive a response.
2. **Event pushes** — the server proactively pushes events and data updates to all connected parent apps and/or tray clients (e.g., blocked-app detected, budget expired, configuration changed by another parent).
3. **State synchronization** — all connected clients (multiple parent apps, tray client) must always show consistent data. The Windows service is the single source of truth (server-authoritative).

The two client types have fundamentally different roles:

- **Parent apps**: authenticated, read/write, connect over LAN
- **Tray client**: unauthenticated (localhost), read-only, runs in the kid's session

A design decision is needed for how these patterns map to SignalR hubs, groups, and contract interfaces.

---

## Decision

### Two Hubs — ParentHub and TrayHub

We will use **two separate SignalR hubs**, each serving one client type:

| Hub | Route | Client Type | Auth Required | Direction |
|-----|-------|-------------|---------------|-----------|
| `ParentHub` | `/hubs/parent` | ParentApp | Yes (pairing credential) | Bidirectional: queries + responses + server-push events |
| `TrayHub` | `/hubs/tray` | TrayClient | No (localhost + user SID) | Primarily server → client; client sends only identity and version queries |

Both hubs are hosted by the same Kestrel process inside `EagleEye.Service`, sharing the same port and TLS certificate.

### Pattern 1: Data Queries (Parent → Server → Parent)

Parent apps invoke **hub methods** on `ParentHub` that return data. These are standard SignalR invocations with `Task<T>` return types — the client calls, the server processes, and returns the result on the same connection.

```
// In EagleEye.Shared/Contracts/IParentHub.cs
public interface IParentHub
{
    // Queries (return data)
    Task<List<UserAccountDto>> GetUserAccounts();
    Task<UserConfigDto> GetUserConfig(string userSid);
    Task<List<InstalledAppDto>> GetInstalledApps();
    Task<UsageStatsDto> GetUsageStatistics(string userSid, DateRange range);
    Task<List<PairedDeviceDto>> GetPairedDevices();
    Task<ServiceVersionDto> GetServiceVersion();

    // Commands (mutate state, trigger broadcast — see Pattern 3)
    Task UpdateAllowList(string userSid, List<AppRuleDto> rules);
    Task UpdateTimeBudgets(string userSid, List<TimeBudgetDto> budgets);
    Task UpdatePauseWindows(string userSid, List<PauseWindowDto> windows);
    Task UpdateGeneralSettings(GeneralSettingsDto settings);
    Task TriggerAppRescan();
    Task RemovePairedDevice(string deviceId);
    Task SetDebugMode(bool enabled);

    // Pairing (unauthenticated — only method callable before pairing)
    Task<PairingResultDto> SubmitPairingCode(string code, string deviceName);
}
```

The TrayClient does **not** query data — it receives everything via server push (Pattern 2). The only method on `TrayHub` callable by the client is identity registration:

```
// In EagleEye.Shared/Contracts/ITrayHub.cs
public interface ITrayHub
{
    Task RegisterSession(string userSid);
    Task<ServiceVersionDto> GetServiceVersion();
}
```

### Pattern 2: Event Pushes (Server → Clients)

The server pushes events to clients by invoking **client callback interfaces**. These are fire-and-forget calls from the server's perspective — the client receives and handles them.

```
// In EagleEye.Shared/Contracts/IParentClientCallback.cs
public interface IParentClientCallback
{
    // Enforcement events
    Task OnBlockedAppTerminated(string userSid, string appName, DateTime timestamp);
    Task OnBudgetExpired(string userSid, string appName, DateTime timestamp);
    Task OnPauseWindowActivated(string userSid, DateTime timestamp);

    // State change notifications (trigger client-side refresh or carry new state)
    Task OnConfigUpdated(string userSid, UserConfigDto updatedConfig);
    Task OnStatisticsUpdated(string userSid, DailyStatsDto todayStats);
    Task OnUserAccountsChanged(List<UserAccountDto> accounts);
    Task OnInstalledAppsChanged(List<InstalledAppDto> apps);
    Task OnPairedDevicesChanged(List<PairedDeviceDto> devices);
    Task OnPairingCodeGenerated(string code);

    // System events
    Task OnUserSessionStarted(string userSid);
    Task OnUserSessionEnded(string userSid);
}
```

```
// In EagleEye.Shared/Contracts/ITrayClientCallback.cs
public interface ITrayClientCallback
{
    // Budget and time information
    Task OnBudgetUpdate(List<AppBudgetStatusDto> budgets);
    Task OnBudgetWarning(string appName, int minutesRemaining, WarningLevel level);
    Task OnBudgetExpired(string appName);

    // Pause window
    Task OnPauseWarning(int minutesRemaining, WarningLevel level);
    Task OnPauseWindowActivated();
    Task OnPauseWindowDeactivated();

    // Pairing
    Task OnShowPairingCode(string code);

    // Enforcement
    Task OnAppTerminated(string appName, TerminationReason reason);
}
```

### Pattern 3: State Synchronization (Server-Authoritative)

The Windows service is the **single source of truth** for all state. The synchronization design follows these rules:

#### Rule 1: Every mutation triggers a broadcast

When any state changes — whether from a parent app command, an internal service event (budget expired, process detected), or a timed action (midnight reset) — the server broadcasts the updated state to **all** connected clients that need it:

```
// Example: Parent App A updates a time budget
ParentAppA → ParentHub.UpdateTimeBudgets(userSid, budgets)
    Server: validate, write to SQLite
    Server → ALL ParentApps:  OnConfigUpdated(userSid, updatedConfig)
    Server → TrayClient(userSid): OnBudgetUpdate(updatedBudgets)
```

The calling parent app also receives the broadcast — it replaces its local state with the server-confirmed version rather than optimistically assuming its own command succeeded.

#### Rule 2: Server pushes full state snapshots, not deltas

When broadcasting state changes, the server sends the **complete current state** of the affected data (e.g., the full `UserConfigDto`, the full `List<PairedDeviceDto>`), not incremental deltas. This ensures:

- No delta-ordering bugs
- No missed-update drift
- Clients can simply replace their local view with the server snapshot
- Reconnecting clients get the same data format as connected clients

#### Rule 3: Full state push on connect and reconnect

When a client connects (or reconnects after a disconnection), the server pushes the complete current state immediately:

- **Parent app connects**: receives all user accounts, config per user, today's stats, installed apps, paired devices
- **Tray client connects**: receives current budget status for its user session, active pause-window state

This eliminates the "stale state after reconnect" problem — clients never need to manually poll for a refresh.

#### Rule 4: Clients never cache state as authoritative

Clients may hold state in memory for display, but they **never treat local state as authoritative**. If a client needs current data, it either:

- Uses what the server last pushed (which is kept up to date via broadcasts), or
- Invokes a query method to fetch fresh data from the server

Clients never modify local state and then push it back — they send commands ("set budget to X"), and the server responds with the confirmed new state via broadcast.

### SignalR Groups

The server uses SignalR groups to target broadcasts:

| Group | Members | Used For |
|-------|---------|----------|
| `Parents` | All authenticated parent app connections | Broadcasting config changes, enforcement events, account/app list changes |
| `Tray:{userSid}` | TrayClient connection for a specific user session | Budget updates, warnings, pairing codes — scoped to that user only |

A parent app is added to the `Parents` group upon successful authentication (after pairing). A tray client is added to `Tray:{userSid}` after calling `RegisterSession(userSid)`.

---

## Rationale

### Two hubs vs. one hub

| Criterion | Single Hub | Two Hubs |
|-----------|-----------|----------|
| API clarity | Mixed methods for both client types; needs runtime role checks | Each hub exposes only methods relevant to its client type |
| Security boundary | Must guard every method against unauthorized tray-client calls | `ParentHub` requires auth at the hub level; `TrayHub` is inherently limited |
| Contract interfaces | One large `IServiceHub` with methods that half the clients never call | Focused `IParentHub` and `ITrayHub` interfaces |
| Connection management | One connection handles both roles (but a client is always only one role) | One connection per client, matching its role |
| Complexity | Slightly simpler routing | Slightly more setup, but cleaner separation |

Two hubs wins on API clarity and security. The slight additional setup cost (two hub registrations in `Program.cs`) is negligible.

### Full snapshots vs. deltas

Deltas (sending only what changed) save bandwidth but introduce ordering dependencies, missed-update drift, and complex reconciliation logic. On a local LAN with small payloads (user configs, app lists), bandwidth is not a concern. Full snapshots are simpler, more reliable, and eliminate an entire class of synchronization bugs.

### Server-authoritative vs. client-authoritative

Client-authoritative patterns (optimistic updates, conflict resolution) make sense for distributed systems with high latency. EagleEye operates on a LAN with a single server — there is exactly one source of truth. Making the server authoritative eliminates merge conflicts and split-brain scenarios entirely.

### Alternatives Considered

| Option | Reason Rejected |
|--------|----------------|
| Single hub for all clients | Mixes parent and tray concerns; requires per-method auth checks; less clear contracts |
| REST API + SignalR for events only | Two transport technologies to maintain; SignalR handles request/response natively via hub method return values |
| Delta-based sync | Adds complexity (ordering, missed updates, reconciliation) for negligible bandwidth savings on a LAN |
| Client-side caching with ETags/versioning | Over-engineered for a single-server LAN system; full pushes are simpler and sufficient |

---

## Consequences

### Positive

- Clean separation between parent and tray communication — each hub is a focused, testable unit
- All three communication patterns handled by one technology (SignalR) with no REST layer
- State synchronization is trivial — broadcast full state on every change, push full state on connect
- No delta bugs, no stale-cache bugs, no optimistic-update conflicts
- Contracts in `EagleEye.Shared` are clear and role-specific — `IParentHub`/`IParentClientCallback` vs. `ITrayHub`/`ITrayClientCallback`
- The calling parent app receives the same broadcast as all other parents — single code path for state updates

### Negative / Trade-offs

- Full-state broadcasts are slightly less bandwidth-efficient than deltas (acceptable on LAN)
- Two hubs means two SignalR connections if a future client ever needs both roles (not a current requirement)
- Clients must handle potentially frequent full-state pushes during high-activity periods (e.g., rapid process churn) — mitigated by throttling/debouncing broadcasts in the server

### Neutral

- The parent app still needs local in-memory state for UI binding (ViewModels), but it is always replaced wholesale on server broadcast — no merge logic needed
- The tray client has no query methods beyond version — it is purely a push-notification consumer

---

## References

- System Architecture: `02_Implementation/docs/architecture/arc42/system-architecture.md` — sections 4.1, 5.2, 5.3, 5.4, 5.5, 6.x, 8.2
- ADR-001: Technology Selection (SignalR chosen as sole communication technology)
- Technology constraints: `01_Intend_and_Constraints/technology_selection.md` — three communication patterns
- Product requirements: FR-SVC-050 through FR-SVC-053 (remote communication patterns)

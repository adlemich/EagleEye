# ADR-010: Event-Driven State Propagation — Service Broadcasts with Revisions

**Status**: Accepted (approved by Michael, 2026-10-07)
**Date**: 2026-10-07
**Deciders**: Michael (project owner), ARC

---

## Context

US-003 is the first story in which parent apps **change** state on the service (which accounts are under parental control) and in which several parent apps can be connected at the same time. Every later configuration story (allow-lists, budgets, pause windows, general settings, paired devices) has the same shape.

Michael set the general rule on 2026-10-07:

> "We use event driven communication to push changes to all connected participants. When a parent app changes settings, it is reflected by the server to all connected apps via SignalR mechanisms. The source client of the change can use that to verify that its write request was successful and other clients will have a trigger to update their views and local data."

ADR-003 (Pattern 3, "server-authoritative state") already says that the service is the single source of truth, that every mutation is broadcast as a full snapshot, and that the calling app also receives the broadcast. It leaves open the details a real implementation needs:

1. **Confirmation.** Is the hub method's result or the broadcast the confirmation of a write, and how do the two relate?
2. **Correlation.** How does the sender recognise the broadcast caused by its own write, when other apps write at the same time?
3. **Ordering.** A query result and a broadcast can overtake each other (the query reads revision 5, a concurrent write produces revision 6 and broadcasts it before the query's result is sent). Without an ordering rule the client would show stale state.
4. **Initial load and reconnect.** ADR-003 Rule 3 says the service pushes the full state on connect. The client then cannot tell when its state is complete (needed for a "Loading …" display, US-003 AC-13), and the push races with the client's handler registration and pairing check.
5. **Missed events** while a client is disconnected.
6. **Failure on the sender side**: what the sender shows when its write fails or is not confirmed in time (US-003 AC-16).

ADR-003 rejected "client-side caching with ETags/versioning" as over-engineered. Point 3 shows that some ordering information is needed nonetheless; this ADR adds the minimum for it.

---

## Decision

We will propagate all state that parent apps read or change **event-driven, through the service**, by the following rules. They apply to every current and future *state area* (see 3), not only to US-003.

### 1. Single source of truth

- The **service** holds the only authoritative copy of every piece of state. It persists it (SQLite, ADR-002) or derives it (e.g. the account inventory from Windows).
- Parent apps hold **replicas** for display only. They never change a replica on their own and never treat it as authoritative (ADR-003 Rule 4).
- **No client polling** for state, and **no client-to-client traffic**. Parent apps talk only to the service.
- How the service itself learns about changes in its environment (e.g. Windows accounts) is an internal matter of the service. It may observe events or check periodically; this is not client polling. Clients only ever receive pushes.

### 2. The write path

```
client ──SetX(requestId, …)──▶ hub ──▶ state owner (one writer per area)
                                        1. validate and authorize
                                        2. store (one SQLite transaction)
                                        3. revision := revision + 1
                                        4. log the change (Information)
                                        5. broadcast the full snapshot to group "Parents"
                                           (all paired connections, INCLUDING the sender)
client ◀── StateWriteAckDto(revision) ─ 6. return the acknowledgement
```

- Every write command carries a client-generated correlation id `Guid requestId` as its first parameter.
- A **state owner** (one singleton per area in the service, e.g. `UserAccountService`) serializes the writes of its area (one `SemaphoreSlim` per area). Steps 2 to 5 run inside that lock, so broadcasts leave the service in revision order.
- **Every accepted write produces exactly one new revision and exactly one broadcast**, even if the stored value did not change (e.g. two apps tick the same box). This keeps the sender's confirmation path free of special cases.
- **Last write received wins** — a general default confirmed by Michael (2026-10-07). There is no expected-revision check (optimistic concurrency) by default. An area may introduce one later, if a story needs it, through a new ADR or story decision.
- Changes the service makes on its own (e.g. a Windows account was renamed, a budget expired at midnight) follow steps 2 to 5 with `requestId = null`.

### 3. State areas and the contract shape

State is partitioned into **state areas**. An area is the unit of a snapshot, a revision and a broadcast. US-003 introduces the area `UserAccounts`. Later areas follow the same pattern, e.g. `UserConfig` per account SID, `PairedDevices`, `GeneralSettings`.

Every area has, in `EagleEye.Shared` (API-first):

| Element | Shape | Example (US-003) |
|---|---|---|
| Snapshot DTO | `record XxxDto(long Revision, Guid? LastChangeRequestId, …full state…)` | `UserAccountListDto(Revision, LastChangeRequestId, Accounts)` |
| Query | `Task<XxxDto> GetXxx(…)` on `IParentHub` | `GetUserAccounts()` |
| Broadcast | `Task OnXxxChanged(XxxDto snapshot)` on `IParentClientCallback` | `OnUserAccountsChanged(snapshot)` |
| Write commands | `Task<StateWriteAckDto> SetXxx(Guid requestId, …)` on `IParentHub` | `SetParentalControl(requestId, accountSid, isUnderParentalControl)` |

- **Full snapshots, not deltas** (ADR-003 Rule 2 stays). An area is cut so that its snapshot stays small (guideline: well below 64 KB). Large state is split into keyed areas (e.g. statistics per account), each with its own revision.
- `LastChangeRequestId` is the `requestId` of the write that produced this revision, or `null` if the service produced it. Query results carry it too, so every snapshot has the same shape.
- `StateWriteAckDto(long Revision)` is shared by all areas.

### 4. Revisions and ordering

- Every area has a **revision**: a 64-bit counter, strictly increasing during one run of the service process. It starts at 1 when the service starts (the initial state) and is **not persisted**.
- A client keeps the revision of the last snapshot it applied per area. It applies an incoming snapshot (query result or broadcast) **only if its revision is higher**, and ignores it otherwise. This resolves every overtaking between query results and broadcasts.
- Revisions are only comparable within one connection to one service run. A service restart always closes all connections, so the client **resets the revision of every area to 0 on every new connection** (first connect, automatic reconnect, reconnect after the service restarted), before it fetches.
- Gaps in revisions are harmless: each snapshot is complete. Clients do not try to detect or repair gaps.

### 5. Confirmation of a write (sender side)

The **broadcast is the confirmation of the stored state**; the **method result is the confirmation that the write was accepted**, and it names the revision to wait for.

- The service sends the broadcast **before** it completes the invocation (step 5 before 6). SignalR delivers messages on one connection in order, so the sender receives its broadcast before the acknowledgement.
- The sender treats its write as **confirmed** when it has applied a snapshot with `LastChangeRequestId == requestId`, **or** any snapshot with `Revision >= ack.Revision` (a later write by someone else then already contains or overrides its own; last write wins).
- If the acknowledgement arrives but the snapshot does not (not expected; would mean the broadcast send failed), the sender fetches the area with the query.
- **Failure**: the call throws (`HubException`: rejected by the service; connection lost), or no confirmation arrives within the area's **write timeout**. The sender then
  1. shows the last confirmed snapshot again (its displayed value reverts),
  2. shows an error message,
  3. if the outcome is unknown (timeout or connection loss), fetches the area once it is connected again. A write that was stored after all then appears through the normal path.
- The write timeout is chosen per area from the story's acceptance criteria. For US-003 it is 4 s, so that the display never differs from the stored state for longer than 5 s (AC-16).
- While a write is pending, the client may show the requested value for that item, but it applies incoming snapshots to all other items at once. When the write completes (confirmed or failed), the item shows the value of the latest applied snapshot.

### 6. Other clients (receiver side)

- On receipt of `OnXxxChanged`, a client applies the snapshot (revision rule, 4) and updates its views and local caches at once. No user action, no extra query.
- A client never re-broadcasts or forwards state.

### 7. Initial load, reconnect and missed events

- After every (re)connect, **as soon as the service has confirmed the pairing** (`GetPairingStatus().IsPaired`), the client resets its revisions (4) and **fetches** the full snapshot of every area it shows with the area's query. The service does **not** push state on connect. *(This changes ADR-003 Rule 3; a general default confirmed by Michael, 2026-10-07.)*
- The connection is in the `Parents` group from `OnConnectedAsync`, before the client can invoke anything (SignalR dispatches invocations only after `OnConnectedAsync` has completed). So there is no gap between joining the group and the fetch: anything that changes afterwards arrives as a broadcast, and the revision rule sorts out the overlap.
- Events missed while disconnected are **not replayed**. The fetch on reconnect is the recovery.
- While a client is disconnected, its replicas are stale. What it shows then is decided per story. US-003 shows no data (US-003 AC-8, OQ-5).
- A client whose callback handler fails (e.g. a deserialization problem) logs the error and fetches the area.

### 8. Groups and authorization

- State broadcasts go **only** to the group `Parents`, which contains exactly the paired, token-authenticated connections (ADR-008 §5). Unpaired connections never receive state.
- Queries and write commands are protected by the default-deny hub filter (ADR-008 §4); none of them is `[AllowUnpaired]`.
- When a device is de-registered, its connections leave the group (existing `RemovePairedDevice` behaviour).
- The tray client follows the same principles for the state it shows (later stories), with its own groups (`Tray:{userSid}`, ADR-003) and its own callbacks. Tray clients never write state.

### 9. Service implementation pattern

- Hub methods stay thin (coding guidelines §7.1). They validate input and delegate to the area's state owner.
- The state owner sends broadcasts through a small injected broadcaster interface per area (wrapping `IHubContext<ParentHub, IParentClientCallback>`), so it can be unit-tested without SignalR.
- A failed broadcast send is logged as a warning and does **not** fail the write: the state is stored; clients that missed it recover on their next fetch.

---

## Rationale

- **The broadcast as confirmation** gives every client one code path for state updates: the sender and all other apps apply the same snapshot the same way (ADR-003 "single code path"). The sender sees exactly what everybody else sees.
- **The method result is still needed**: it is the only way to report a rejection (`HubException`) to the sender alone, and its revision lets the sender confirm even if another app's write overtook its own.
- **Correlation id plus revision**: the id identifies the sender's own change exactly; the revision covers the case that a later write by another app arrives first. Neither needs extra round trips.
- **Revisions per area, in memory**: they are only needed to order messages within one connection. Persisting them would add a table and a write per change for no benefit, because every service restart ends all connections and clients reset anyway.
- **Client fetches on connect** instead of a server push: the client knows exactly when its state is complete (Loading state), fetches only the areas it shows (relevant later for mobile apps), and the pairing check happens first. The server stays stateless towards connecting clients.
- **Serialized writes per area with the broadcast inside the lock** give a total order of revisions and broadcasts without any distributed-ordering logic. Write rates are tiny (a parent clicking), so the lock costs nothing.

### Alternatives Considered

| Option | Reason Rejected |
|---|---|
| Method result alone confirms the write; broadcast only to *other* apps (`Clients.OthersInGroup`) | Two code paths for the same state change; the sender would have to build its replica from its own request. Contradicts Michael's rule and ADR-003 Rule 1. |
| Broadcast alone, write method returns `Task` (no ack) | The sender could not tell "rejected" from "slow", and could not confirm when another app's write overtook its own. |
| Server pushes full state on connect (ADR-003 Rule 3 as written) | Client cannot know when the state is complete; push races with handler registration and the pairing check; pushes areas the client may not show. |
| No revisions (apply whatever arrives last) | A query result can overtake a broadcast and show stale state permanently, until the next change. |
| Persisted revisions / ETags with conditional requests | No benefit over in-memory revisions with reset on reconnect; more schema and writes. |
| Delta events | Ordering and missed-update problems (ADR-003 Rule 2). |
| Optimistic concurrency (reject writes based on an expected revision) | Not required by any story yet (US-003 AC-24: last write wins). Can be added per area later. |
| Event replay / message queue for disconnected clients | Full snapshot on reconnect makes it unnecessary. |

---

## Consequences

### Positive

- One uniform pattern for every configuration story: contract shape, service state owner, client replica. New areas are a checklist (coding guidelines §7.5), not a design task.
- Multiple parent apps never disagree for longer than one message round trip; the sender sees confirmed state only.
- Robust against reconnects, service restarts, overtaking messages and missed events, without polling.
- All logic that decides what to show (revision rule, pending writes, timeouts) lives in `EagleEye.ParentApp.Core` and the service's state owners and is unit-testable (ADR-009).

### Negative / Trade-offs

- Every accepted write is broadcast to all parent apps, even no-op writes. Negligible on a LAN with a few apps.
- Broadcasting inside the area lock means a very slow parent connection could delay other writes of that area. Acceptable for a handful of LAN clients; revisit if it is ever observed.
- Last write wins: two parents changing the same item at the same time silently overwrite each other (by design, US-003 AC-24). Both see the final state within seconds.

### Neutral

- Snapshot DTOs gain two fields (`Revision`, `LastChangeRequestId`), and write commands gain a `requestId` parameter and an ack result. ADR-003's example signatures (`Task<List<UserAccountDto>> GetUserAccounts()`, `OnUserAccountsChanged(List<UserAccountDto>)`, `Task UpdateAllowList(…)`) are targets that will take this shape when their stories come.

### Relation to existing decisions

| Decision | Effect |
|---|---|
| ADR-003 Pattern 3 Rules 1, 2, 4 | Confirmed and made concrete (broadcast includes the sender; full snapshots; replicas are not authoritative). |
| ADR-003 Pattern 3 Rule 3 ("server pushes full state on connect") | **Changed**: the client fetches after the pairing is confirmed (§7). |
| ADR-003 "Client-side caching with ETags/versioning" (rejected) | Still rejected as a caching mechanism; a lightweight in-memory revision is added for ordering only (§4). |
| ADR-003 example signatures | Refined to the contract shape of §3. |
| ADR-008 §4 (default-deny filter), §5 (`Parents` group) | Unchanged; this ADR relies on them as the authorization boundary for queries, writes and broadcasts. |
| ADR-009 (`ParentApp.Core`) | Unchanged; the client side of this ADR lives in Core. |
| Coding guidelines §7.2, §7.3 | Amended; new §7.5 (checklist for a new state area). |
| arc42 §5.2, §6.4, §8.3, §8.11 | Amended accordingly. |

---

## References

- ADR-003 (hub design), ADR-008 (connectivity, pairing, default deny), ADR-009 (`ParentApp.Core`), ADR-002 (persistence, logging)
- US-003: `02_Implementation/docs/requirements/user-stories/US-003/user-story.md` (AC-13, AC-14, AC-16, AC-19 to AC-24) and its implementation plan in the same folder
- Product requirements v1.3: FR-SVC-053, FR-SVC-072, FR-APP-022
- arc42: `02_Implementation/docs/architecture/arc42/system-architecture.md` §8.3
- Coding guidelines: `02_Implementation/docs/architecture/product-coding-guidelines.md` §7

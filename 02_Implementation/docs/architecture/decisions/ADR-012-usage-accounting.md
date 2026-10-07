# ADR-012: Usage Accounting — Active Time, Clocks, Day Boundary, Persistence and Usage State Areas

**Status**: Accepted (approved by Michael with the US-004 implementation plan, 2026-10-07)
**Date**: 2026-10-07
**Deciders**: ARC, Michael

---

## Context

US-004 introduces usage time: per account, per app, per day, in seconds, kept up to date at least every 5 s (FR-SVC-012 v1.4, US-004 AC-12 to AC-16). Budgets and pause windows in later stories will be built on the same numbers (NFR-R-013), so the accounting model is a lasting decision. The open points are:

- what an "app" is when it has several processes (AC-6), and what identifies it (OQ-2: the full program path);
- when an app is "active" (OQ-1 b, AC-12): open, and the account's session is the session in use;
- which clock measures durations, and how sleep, clock changes and time-zone changes are handled (AC-15, AC-16);
- how days are cut at local midnight (AC-14);
- how often data is written, so that a crash loses at most 5 s (AC-15);
- how usage is offered to parent apps under ADR-010 when a full 90-day history per account is too big to push every few seconds (AC-22: at most 15 s lag).

---

## Decision

### 1. App identity and instances

- An **app** is identified per account by the **full path of its program file**, case-insensitive (OQ-2). All processes with the same path that have an app window (ADR-011) form **one** app, so several windows and several processes count once (AC-6).
- Store apps are identified by the path of their app process (e.g. `…\WindowsApps\Microsoft.WindowsCalculator_…\CalculatorApp.exe`). Their display name is the package display name, resolved by the service with `SHLoadIndirectString` on the package manifest's `DisplayName`. Other programs use the file description from their version info (Task Manager's name, AC-8). If neither exists, the name is the process name without `.exe`. There is no online lookup.
- An **app instance** (history entry, AC-10) is a continuous period in which the app is open in the account's sessions: it starts when the first app window of that path appears and ends when the last one is gone. It also ends when recording stops: the account is unticked or becomes admin ("monitoring stopped"), the service stops ("service stopping"), or the session ends ("session ended"). An instance left open by a crash is closed at the next start, at its last recorded time ("service stopped unexpectedly").

### 2. Active time

An app is **active** while it is open and **at least one session of the account is in use**. A session is in use when its WTS state is `Active` and it is not locked. A session switched away with "Switch user" is `Disconnected`, so it is not in use. During sleep or hibernation the service does not run, so nothing is counted (§3). Minimised windows count, and parallel apps each count fully (OQ-1 b).

The session state comes from the Service Control Manager's session-change notifications (lock, unlock, logon, logoff, console connect and disconnect), which are handled at once, and from a full WTS re-read at every tick as reconciliation.

### 3. Clocks

- **Durations** are measured with the monotonic clock (`TimeProvider.GetTimestamp`), never with wall-clock differences. A change of the system clock or the time zone therefore never changes a duration (AC-16).
- **Gap rule**: if more than 15 s of monotonic time passed between two crediting points (the process was suspended, the machine slept, the service hung), that gap is **not** credited and is logged once. Power notifications (`PBT_APMSUSPEND`, resume) close and restart the crediting explicitly, so sleep is never counted, whatever the monotonic clock does during sleep.
- The **wall clock** (local time) is used only to decide which day a credited interval belongs to, and for history timestamps (stored in UTC).

### 4. Crediting and the day boundary

- Each open app keeps "credited up to" as a monotonic timestamp. Crediting happens at every **tick (every 5 s)** and at every event that changes the active set: an app opens or closes, a session is locked or unlocked, monitoring stops. On each crediting, the interval `[credited up to, now]` is added if the app was active during it.
- The interval is placed on the local time line as `[now_local − length, now_local]`. If it crosses local midnight, it is split, and each part counts for its own day (AC-14). The history instance is not split.
- A clock change can at most move one interval of ≤ 5 s to a neighbouring day. It can never create negative time, and never more time than really passed (AC-16).
- **Per-day value**: seconds, a 64-bit integer, rounded down to whole seconds. Fractions are carried in memory to the next tick, so no time is lost systematically.

### 5. Persistence cadence

- Every tick writes all credited seconds and the "last seen" time of every open instance in **one SQLite transaction** (upsert per app and day). A crash loses at most the last tick, i.e. ≤ 5 s (AC-15, NFR-R-013).
- A new app record and the start of an instance are written immediately, so a new app appears in the report at once with 00:00 (AC-22).
- Retention: daily usage and history older than 90 days (by local day, or by start time) are purged at the first tick after local midnight and at service start (FR-SVC-043). App records are never purged by age.
- When an account no longer exists on the PC, all of its data is purged after the inventory check that notices it (US-003, every 15 s; FR-SVC-047).

### 6. Usage state areas (ADR-010)

A full 90-day snapshot per account (up to about 100 KB) is too large to broadcast every few seconds. Usage is therefore split into **keyed state areas, one per account and day**, under ADR-010 §3 ("large state is split into keyed areas"):

| Element | Shape |
|---|---|
| Area | `UsageDay:{accountSid}:{day}` |
| Snapshot | `DayUsageDto(long Revision, Guid? LastChangeRequestId, string AccountSid, DateOnly Day, DateOnly ServiceToday, IReadOnlyList<AppUsageDto> Apps)`, with `AppUsageDto(long AppId, string DisplayName, long Seconds)` |
| Query | `GetAccountUsage(accountSid)` → `AccountUsageDto(string AccountSid, DateOnly ServiceToday, IReadOnlyList<DayUsageDto> Days)`: today (always present, possibly empty) plus every day of the last 90 with usage |
| Broadcast | `OnDayUsageChanged(DayUsageDto snapshot)` to group `Parents` |
| Writes | none: usage is produced by the service only (`LastChangeRequestId` is always `null`) |

- **Revision**: one counter per account, in memory, incremented for every day snapshot produced. It is strictly increasing for each `(account, day)` area, as ADR-010 §4 requires. A client applies a day snapshot only if its revision is higher than the one it holds for that day.
- **Push cadence**: at every tick, a snapshot is broadcast for each `(account, day)` whose seconds or app list changed. That is at most one broadcast per account every 5 s, about 1 KB each, and nothing while nothing is active. The service's value is ≤ 5 s behind real time, and the display lag stays well below 15 s (AC-22).
- **Midnight**: at the first tick of a new local day, the service broadcasts an empty snapshot of the new `today` for every controlled account. `ServiceToday` in every snapshot lets clients drop days older than `ServiceToday − 89` without another message.
- On (re)connect and when the selected account changes, the client fetches with `GetAccountUsage` (ADR-010 §7).

---

## Rationale

### Alternatives Considered

| Option | Reason Rejected |
|---|---|
| Durations from wall-clock differences | Clock and time-zone changes would create negative or huge durations (AC-16). |
| Persist only every minute or at shutdown | A crash could lose up to a minute (AC-15 allows 5 s). One small transaction every 5 s is cheap. |
| One instance per process | Multi-process apps would produce many parallel instances and double-counting risks (AC-6). |
| App identity by process name | Two different programs can share a name (`launcher.exe`); Michael chose the path (OQ-2). Allow-lists in later stories may match by name (ADR-005); that is a separate decision. |
| One state area per account (all 90 days) | It would have to broadcast up to ~100 KB every 5 s per active account. |
| Push only on request (client polls) | Forbidden by ADR-010. |
| Separate `UsageToday` area and a static history area | Both are special cases of the per-day areas. The day key keeps midnight and purge simple. |
| A day total ("time at the PC") | Out of scope (OQ-7); it can be derived later from the history and session state. |

---

## Consequences

### Positive

- Budgets and pause windows in later stories can use the same per-day seconds and the same "active" definition.
- The model is robust against sleep, clock changes, crashes and service restarts, with bounded and documented error (≤ 5 s per event).
- Live updates are small and only flow while something changes.

### Negative / Trade-offs

- Parallel apps each count fully, so the sum of a day can exceed the time the kid sat at the PC. This is intended (OQ-1 b, OQ-7).
- An app updated into a new folder gets a second record, and two rows with the same name can appear on a day (OQ-2, accepted).
- Display names of Store apps and the file descriptions are resolved in the system's language (the service runs as SYSTEM), not in the parent app's language.

### Neutral

- FR-SVC-042 ("separate files per child user account") is fulfilled by one table set keyed by account SID in `EagleEye.Service.db` (ADR-002: one SQLite database per component), not by separate files.

---

## References

- US-004 user story and implementation plan (`02_Implementation/docs/requirements/user-stories/US-004/`)
- ADR-002 (SQLite), ADR-005 (classification, amended), ADR-010 (state propagation), ADR-011 (session agent)
- Requirements v1.4: FR-SVC-012, FR-SVC-040 to FR-SVC-047, FR-APP-060, NFR-R-013
- arc42 §8.6, §8.7

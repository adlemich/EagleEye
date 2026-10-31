# ADR-021: App Groups — Shared Budgets Computed from Activity Spans, Enforced per Limit Subject

**Status**: Proposed (with the US-008 implementation plan, 2026-10-31)
**Date**: 2026-10-31
**Deciders**: ARC, Michael

---

## Context

US-008 adds **app groups**: a named set of apps, defined once per service PC, with one daily budget and seven weekday ticks per group and account (the *group rule*). Story AC-19 to AC-23 define the **group used time** as the time during which **at least one** current member app was active for the account, counted once when several members run together, and always computed with the **current** members, also for time before an app joined (AC-22) and for past days on Reports (AC-36, OQ-3).

Today the service stores usage only as **whole seconds per app and local day** (`DailyUsage`, ADR-012 §5). The tracker's instance history (`AppInstances`) holds start and end of each run, but not when the run was *active* (lock, user switch and sleep are not counted, US-004 AC-12). A union ("at least one member active") cannot be computed from daily totals or from instances. At the moment of crediting, the union is easy: within one `UsageTracker.Advance` all credited intervals of an account end at the same instant, so the union of one advance is its longest interval. But a union computed at crediting time is bound to the members of that moment, and AC-22 and AC-36 need it for **any** member set, later.

The enforcement of US-006 (ADR-015) evaluates one **app** at a time: its own rule, its used time, its warnings (keyed per app and day), its grace period and its close. With groups, all running members of a group share one budget, one set of warnings (AC-26: once per group and day), one close message (AC-27) and one grace period per change (AC-32).

The parent app for Windows (0.7.0) is not updated in this story (US-009). It must keep working against the 0.8 service (AC-44, story OQ-2 option B).

---

## Decision

### 1. Groups are keyed by program path and stored once per service PC

We will store groups in `AppGroups` (id, name, a unique case-insensitive name key) and their members in `AppGroupMembers` with the **program path as primary key** (case-insensitive, like `AppRecords`), so every app is in at most one group (AC-13) and one membership covers the app records of all accounts (story term *App*). Group rules are stored per group and account in `AppGroupRules`; **no row = default rule 0:00, no weekday** (deny by default, as ADR-015 §8). Deleting a group cascades to its members and rules. Memberships are not deleted when an account or app record goes away (AC-39, AC-40); a membership without any listed app record is invisible and harmless.

### 2. The service records activity spans

We will persist **activity spans**: for each app record, the intervals (UTC, millisecond precision, split at local midnight like `DaySplitter`) during which the tracker credited it. The tracker builds them from the same crediting it already does (`UsageTracker.Credit`): a new credited part that starts at most 1 s after the end of the app's open span extends it; otherwise a new span starts. `UsageService` writes new spans and extends open ones in the same per-tick transaction as `DailyUsage` (crash loss ≤ 5 s, as ADR-012). Spans are purged with `DailyUsage` (90 days) and with their app record (cascade). Per-app usage (`DailyUsage`, Reports per app, own-rule budgets) is **unchanged** (AC-23).

**Upgrade**: days before the update have no spans. At the first start of 0.8 the service adds, once, one *estimated* span per `DailyUsage` row of the last 90 days, starting at that local day's first instant and lasting its seconds. For those days a group's time is then the time of its **largest member** (overlap assumed). From the update on, all spans are real.

### 3. Group used time = union of the current members' spans

*Group used time* (account, group, day) = the total length of the union of the spans of the account's app records whose program path is **currently** a member of the group, on that day. The same pure calculator serves:

- enforcement: `TodayUsageLedger` keeps today's spans per (account, path) in memory, loaded at account load and fed with the tracker's span updates of each tick (the same output that `UsageService` persists, as ADR-015 §2);
- Reports: `UsageService` loads the spans of the requested days and computes the group lines with the current groups.

Own-rule budgets keep using the per-app seconds (ADR-015 §2). A group with one member can differ from that app's per-app figure by about a second (rounding); this is inside the US-004 tolerance.

### 4. Enforcement works on limit subjects

We will evaluate time limits per **limit subject** instead of per app. The subject of a running or starting app is:

- its **group**, if its program path is a member of a group: the group rule of the account, the group used time, the group name in messages;
- otherwise the **app** itself, exactly as in ADR-015 (own rule, own used time, display name).

A grouped app's own rule is ignored while it is in a group and kept for when it leaves (AC-17, AC-24). Break times still win (ADR-015 §3 order unchanged). Per subject:

- the **end of allowed time** is computed once (`AppEndCalculator`, with the subject's rule and used time);
- **warnings** are keyed per subject and day (`group|sid|id` for groups); member apps get no warnings of their own (AC-26);
- a **close** stops all running members of the subject in the session, with **one** kid message whose task is shared by all close requests (the batch pattern of break begins, ADR-015 §7) (AC-27);
- a **grace period** (ADR-015 §5, minimum grace) belongs to the subject. It is detected per running app by comparing its end under the previous snapshots (break times, app rules, groups, group rules, and the used time with the previous members) with its end under the current ones. An app whose subject changed takes part in the grace of its new subject. Snapshots that change only names (rename) never move an end and so never start a grace (AC-34).

The kid message reuses the US-006 reasons (`AppEarlyWarning`, `AppFinalWarning`, `AppGracePeriod`, `AppNotAllowedToday`, `AppBudgetUsedUp`) with the **subject's name** in `KidMessageDto.AppName`. The tray client renders "Games: 5 minutes left." without a change.

### 5. Contract changes are additive; the parent app joins the areas

We will add two state areas (ADR-010): **`AppGroups`** (global: groups and the listed apps with their group) and **`AppGroupRules:{sid}`** (the group rules of one account, a row for every group), with their own queries, field-level writes, callbacks and revisions. `DayUsageDto` gets an additional, trailing list of group lines. No existing hub method, callback or DTO field changes meaning:

- `AppRulesDto` still lists **all** app records, grouped or not. The parent app with group UI hides grouped apps itself (client-side join with `AppGroups`). An older parent app shows and edits own rules of grouped apps; they are stored and have no effect while the app is grouped (AC-44).
- An older parent app ignores the new callbacks (SignalR logs "no handler" on the client) and the new JSON property (System.Text.Json ignores unknown members).
- A group change does not re-broadcast past days. A parent app showing Reports fetches the account's usage again when it receives an `AppGroups` change. This is a fetch triggered by a broadcast, not polling (ADR-010 holds).

The group UI of the parent app is behind a platform feature switch (`ParentAppFeatures.AppGroups`): on in the Mac app (US-008), off in the Windows app until US-009, so a Windows build of the shared code keeps the 0.7 behaviour.

---

## Rationale

The union over **any** member set needs the activity on the time line; spans are the smallest data that keep it. They are written by the component that already owns usage persistence, on the existing tick and transaction, so there is no new timing path. Using one calculator for budgets and Reports keeps both equal, as ADR-015 §2 demands for apps.

Limit subjects keep ADR-015 intact for ungrouped apps (AC-30): an app subject behaves as today. The group case reuses the break-batch pattern for closes and the existing reasons for messages, so the tray client does not change.

Additive contracts and client-side joins are the only way to keep the unchanged Windows app 0.7.0 working (AC-44) without shipping it again.

### Alternatives Considered

| Option | Reason Rejected |
|--------|----------------|
| Group seconds per day computed at crediting time (max of the members' credits per advance) | Bound to the members at that moment. AC-22 (time before joining) and AC-36 (past days with current members) cannot be met. |
| Per-minute activity bitmaps per app and day | A minute slot counts a partial minute as a full one; with several short gaps the error exceeds the ± 1 minute tolerance and would cost the kid budget time. Per-second bitmaps are ~10 KB per app and day. |
| Reconstructing activity from `AppInstances` | Instances include locked, switched-away and sleep times (US-004 AC-12 excludes them). |
| Sum of the members' daily seconds | Double counts overlap; contradicts AC-19. |
| Groups per account | Michael's answer 3: groups are defined once per service PC. |
| Filtering grouped apps out of `AppRulesDto` on the service | Changes what the Windows app 0.7.0 sees and edits; a membership change would need re-broadcasts of every account's app rules. |
| A new kid-message reason for groups | Needs a tray client change for the same visible result. |
| Re-broadcasting all 90 days of every account on a group change | Large pushes for a rare event; the fetch on change is simpler and bounded to the page that is open. |

---

## Consequences

### Positive

- Exact group times for any member set, today and on the 90 days of Reports.
- Ungrouped apps and the Windows app 0.7.0 behave as before.
- No tray client change; no Windows right change.

### Negative / Trade-offs

- A new table with one row per activity span (typically tens per app and day, purged after 90 days) and one more write per active app per tick.
- Group times of days before the update are estimates (largest member).
- Group lines of past days change when members change (OQ-3, accepted).
- The engine is reorganized around subjects; every US-006 behaviour must stay covered by its existing tests.

### Neutral

- Warning and grace state stay in memory (ADR-015 §9), now keyed per subject.
- Group names are parent-entered labels and are written to the service log (AC-14, AC-31); parent message texts are still never logged.

---

## References

- `02_Implementation/docs/requirements/user-stories/US-008/user-story.md` (AC-19 to AC-34, AC-44, OQ-2, OQ-3)
- `02_Implementation/docs/requirements/user-stories/US-008/implementation-plan.md`
- ADR-010 (event-driven state propagation), ADR-012 (usage accounting), ADR-015 (time limits for running apps), ADR-016 (default texts)
- Amendments to apply on approval: ADR-012 (spans, backfill), ADR-015 (§2 group used time, §4 warning keys per subject, §5 grace per subject, §7 group close batch), arc42 §5.2, §5.5, §8.4, §8.6, §9

# ADR-015: Time Limits for Running Apps — End of Allowed Time, Warnings, Grace Periods and Enforced Closes

**Status**: Accepted (approved by Michael with the US-006 implementation plan, 2026-10-12; §5 reflects his answer to Q-2, §4 his answer to Q-5)
**Date**: 2026-10-12
**Deciders**: ARC, Michael

---

## Context

US-005 (ADR-013) enforces break times **at app start** only. Apps that run when a break begins keep running (US-005 AC-25). US-006 adds:

- a daily **time budget** and **weekday ticks** per app record (the *app rule*). Apps are **blocked by default** (0:00, no day ticked), also apps recorded before the update (US-006 OQ-2);
- two global **warning levels** (early, final). They are shown before a budget runs out and before a break begins;
- **closing running apps** when the budget runs out, when a break begins, or at midnight when the next day is not allowed (US-006 AC-25, AC-29, OQ-4, TC-042);
- a **grace period** of the final-warning minutes when a parent change leaves a running app no time (AC-32, AC-33, TC-033).

The decision is where and how often this is evaluated, what "used time" is, how the end of an app's allowed time is computed, how warnings are de-duplicated, how a grace period is detected, and how closes reuse the US-005 close sequence.

---

## Decision

### 1. One evaluation point: the accounting tick

We will evaluate time limits in the accounting loop (ADR-012 §4), **once per 5 s tick**, after the tick has credited usage. A new component, the `TimeLimitEngine`, runs there. It never blocks the loop: messages and close sequences run as tasks outside the loop, as in ADR-013 §4. Decisions **at app start** stay in the start gate (ADR-013 §1). The gate now checks break times first and then the app rule (§6).

Bounds: a level or an end is detected at most 5 s (one tick) plus at most 1 s of credit rounding after it is reached. The close request and the tray message follow at once. This meets the 10 s bounds of US-006 OQ-17.

### 2. Used time = the Reports figure

*Used time* is the US-004 usage of today for the app record (US-006 OQ-3): today's `DailyUsage` seconds plus the credits of the running service. A `TodayUsageLedger` (service, in memory) is loaded from `DailyUsage` at service start. After that it is fed with the **same** `TrackerOutput.Credits` that `UsageService` persists. Reports and budgets therefore never differ. The ledger is keyed by account SID, program path (case-insensitive) and local day. A new local day starts at zero.

### 3. End of allowed time

For each running, non-exempt app of a controlled account, the engine computes the **end** as the earliest of:

| Candidate | Applies when | Time left |
|---|---|---|
| **Budget end** | today allowed (day ticked, budget > 0:00) | `budget − used` seconds of further use. Only an end that falls before the next local midnight counts; otherwise the app does not end today. |
| **Not allowed today** | today unticked or budget 0:00 | 0 (only possible for a running app after a parent change, or at midnight) |
| **Midnight** | the next local day does **not** allow the app (unticked or 0:00) | wall time until the next local midnight (US-006 OQ-4) |
| **Break start** | an active break entry comes into effect, or is in effect now | wall time until the start, or 0 |

Only ends within a **60-minute horizon** are computed. Warning levels are at most 59 minutes, so later ends do not matter yet. Ties go to the break (break times always win, OQ-13). The end's **kind** decides the texts. A break end shows the break display text and break warnings. Budget, not-allowed and midnight ends show the budget text and the app's warnings (US-006 AC-31).

### 4. Warnings

- **Two global levels** (`WarningSettings`, §8). A level *L* is reached when the time left to the end is ≤ *L* minutes and > 0.
- **App ends**: each level at most **once per app record and local day**. The key is `(account, app, day, level)`.
- **Break ends**: each level at most **once per session and break occurrence**. The key is `(session, break start as a local date-time, level)`. The warning is shown only if at least one running app of the session has this break as its end (AC-28, AC-31). It is one message for all apps.
- **Matching warning at once**: when an app is first seen running, or a change lowers its time left, and it is already below a level, the engine shows the lowest level reached (final if ≤ final, otherwise early). It marks the higher level as shown too (OQ-5, AC-22, AC-30).
- **Level changes** apply to warnings not yet shown (AC-33).
- The warning ledger lives in memory. It is not persisted (§9).

### 5. Grace periods

After a change of the account's app rules, break times or display texts, the engine compares each running app's new end with the end it held before. A grace period starts when **the change moved the end earlier and the new time left is below the grace threshold**. The grace end is `now + final-warning minutes` (monotonic clock). During the grace period:

- the final warning is shown at once, with the grace info line (AC-34);
- the effective time left is `max(grace left, natural left)`. If a later change gives the app more natural time than the grace, the grace ends;
- no further warning is shown for that end;
- when it expires, the app is closed as in §7. The message is the budget text for app ends and the break display text for break ends.

Graces caused by a **break** change are grouped per session: one message for all apps (AC-33).

The **grace threshold** is the final-warning minutes: a **minimum grace** (Michael, US-006 plan Q-2, 2026-10-12; story AC-32 to AC-34). After any parent change that moves a running app's end earlier, so that less than the final-warning minutes are left, the kid gets the full final-warning minutes. This covers today unticked, a budget at or below the used time, a budget leaving e.g. 20 s, a break now in effect, and a break made to begin sooner than the final minutes. The info line always names the full final minutes. A change that leaves at least the final minutes gets no grace, only the warnings of §4. A change of the **warning levels** never starts a grace period. The rejected alternative (grace only when no time is left) would give *less* time for a smaller cut than for a cut to zero.

**Service start**: grace periods are not restored. After a restart, the gate judges every running app again (found at agent start, ADR-013 §3). An app whose time is used up is closed at once with its text and without a new grace period. In practice this only happens to an app that was in a grace period, or to an app when a break began while the service was down (plan D-6).

### 6. Start gate: break first, then app rule; app record on blocked starts

The US-005 gate (ADR-013 §3) keeps its definition of a start (first process creation time). It is extended:

1. A break in effect at the start's creation time blocks the start with the break display text (ADR-013, unchanged).
2. Otherwise the **app rule at the time of detection** decides. The start is blocked if today is not allowed (reason "not allowed today") or used ≥ budget (reason "budget used up"). It is blocked with the budget text.
3. An app without an app rule has the **default rule** (0:00, no day ticked). The default is represented by the absence of a row (§8), so every app found for the first time is blocked, and so is every app recorded before the update.
4. A blocked start now **creates the app record** if needed (US-006 OQ-1), without an instance, usage or day row. It is created by `UsageService` (owner of `AppRecords`) from the close sequence, and the app appears in the Rules table.
5. Blocked processes are still removed before the tracker (ADR-012 amendment of US-005).

### 7. Enforced closes of running apps

The close sequence of ADR-013 §4 is generalized into a **close request** with a kind (`blocked start` or `running app`) and a reason. For **running apps**:

- the processes are **not** removed from the report. The time until the app is gone counts as used time (AC-25, OQ-3);
- the kill set (ADR-013 §5) uses the closed app's own program path as root. It must not exclude that path as "another open app";
- a break that begins closes **every** running, non-exempt app of the account (TC-042). This replaces US-005 AC-25. Starts that were allowed before the break are no longer kept;
- the message is sent **once per session** per break begin. Every history record of that batch carries the same message state. Budget closes send one message per app.

The engine enforces an **invariant** at every tick: no non-exempt app of a controlled account runs while its time left is ≤ 0, outside a grace period and outside a running close sequence. This also covers a service restart during a break (§5, service start).

### 8. Configuration storage

- `AppRules (AppId PK → AppRecords ON DELETE CASCADE, BudgetMinutes 0..1439, Days 0..127)`: **no row = default rule** (0:00, no day). The migration therefore needs no data copy for existing apps (OQ-2).
- `AccountBudgetTexts (AccountSid PK, Text)`: no row = default text (ADR-016).
- `WarningSettings` (one row, `CHECK (EarlyMinutes > FinalMinutes)`, texts nullable): no row = defaults 5 and 1 minutes with the default texts (ADR-016).
- State areas (ADR-010): `AppRules:{sid}` (app list, rules, budget text) and `WarningSettings` (global), each with field-level writes.

### 9. Not persisted

The warning ledger and grace periods live in memory. After a service restart, a warning that was already shown may be shown again, and a running grace period is lost (§5 "Service start"). Used time is persisted (ADR-012), so a restart never refills a budget (AC-36).

---

## Rationale

The accounting tick already owns the open apps, the activity state and the credits. Evaluating there keeps one consistent view: the time that is credited is exactly the time that is budgeted. A 5 s tick meets every bound of the story. Representing "deny by default" as a missing row removes every path where a new app could slip through unconfigured.

### Alternatives Considered

| Option | Reason Rejected |
|---|---|
| A timer per app that fires at the predicted end | The prediction is wrong as soon as the session is locked or the PC sleeps. It needs re-planning on every activity change, and it adds concurrency outside the single-threaded loop. The 5 s tick is accurate enough (§1). |
| Countdown in the tray client | The tray client runs as the kid and can be ended (R-6). Enforcement must stay in the service (NFR-R-011, R-12). |
| Store an explicit row (0:00, no day) for every app record, written by the migration and at record creation | Every creation path (normal start, blocked start, migration) would have to remember it. A missing row would mean "allowed", which is a security hole. With "no row = blocked" a missing row is safe. |
| Persist warnings shown and grace periods | Extra table and writes every few seconds for a rare case (service restart). The consequences are acceptable (§9). |
| Close at once without grace after a parent change | Michael's answer to OQ-11 asks for a grace period. |
| Process-based enforcement (all processes, not only apps) | Out of scope (US-005 Q-3, US-006 Out of Scope). |

---

## Consequences

### Positive

- One consistent definition of used time for Reports and budgets.
- Deny by default holds for every app, including apps recorded before the update, without a data copy.
- Break begins now close running apps, which closes the US-005 gap of AC-25 (R-14 reduced).
- The minimum grace (§5) is monotonic: a parent change never leaves a running app less than the final-warning minutes.

### Negative / Trade-offs

- Detection is tick-based (≤ 5 s plus rounding). Warnings and closes are not to the second.
- After a service restart, warnings may repeat, and an app that was in a grace period is closed at once (§9).
- Right after the update, every app the kid uses is blocked until the parent configures it (accepted, OQ-2).
- An app whose program path changes with each update (installers that use versioned folders) becomes a new, blocked record after each update (accepted, OQ-12).

### Neutral

- The fixed 20 s timeout stays (FR-SVC-021 note).
- No events to parent apps (FR-APP-070 out of scope).

---

## Relation to existing decisions

| Decision | Effect |
|---|---|
| ADR-006 | Amended: the warnings of "Pre-Termination Warnings" are the two global levels of §4. They are shown in the kid dialog (ADR-014), not as notifications. Running apps are closed with the ADR-013 close sequence (20 s, kill set). |
| ADR-010 | Applied: new state areas `AppRules:{sid}` and `WarningSettings`. |
| ADR-012 | Amended: the `TodayUsageLedger` reads the tracker credits. Blocked starts create the app record (without usage). |
| ADR-013 | Amended: the gate also checks app rules. Close requests generalize blocked starts. Running apps are closed at a break begin. The history gets kind and reason. |
| ADR-014 | Amended: `ShowKidMessage` replaces `ShowBreakTimeMessage`. One dialog per session. A newer message replaces the text. Info line. |
| ADR-016 | Default texts follow the language of the displaying app. |

---

## References

- US-006: `02_Implementation/docs/requirements/user-stories/US-006/user-story.md` and `implementation-plan.md`
- Requirements v1.6: FR-SVC-011, FR-SVC-020, FR-SVC-022, FR-SVC-024, FR-SVC-025, FR-TRAY-020, FR-TRAY-021, FR-APP-040, FR-APP-053, FR-APP-054, TC-020 to TC-025, TC-030 to TC-033, TC-040 to TC-042
- ADR-006, ADR-010, ADR-012, ADR-013, ADR-014, ADR-016

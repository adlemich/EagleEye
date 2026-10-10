# ADR-014: Session-Bound Tray Connections and the Kid's Message Box

**Status**: Proposed — approved with the US-005 implementation plan
**Date**: 2026-10-10
**Deciders**: ARC, Michael

---

## Context

When a start is blocked by a break time, the tray client **in the session of that account** must show the account's display text in a topmost message box that the kid acknowledges with "OK" (FR-TRAY-022, US-005 AC-30 to AC-34). The log and the history must say whether the message was shown (AC-28, AC-33, AC-35).

Today:
- `TrayHub` (HTTP, loopback only, ADR-008) knows nothing about the session or user of a connection. The only push, `OnShowPairingCode`, goes to **all** tray connections.
- ADR-003 planned `RegisterSession(userSid)` and groups `Tray:{userSid}`, with the SID **asserted by the client**. The loopback endpoint is unauthenticated: any program of the kid can connect and claim anything.
- The tray client runs as the kid and can be ended by the kid (arc42 R-6); it has no single-instance guard.
- SignalR pushes are fire-and-forget; the service does not learn whether a message was shown.

---

## Decision

### 1. The service binds each tray connection to a session by itself

In `TrayHub.OnConnectedAsync` the service determines the **process on the other end of the loopback TCP connection**: it looks up the connection (local and remote endpoint from `HttpContext.Connection`) in the TCP table (`GetExtendedTcpTable`, `TCP_TABLE_OWNER_PID_CONNECTIONS`, IPv4 and IPv6, because `ListenLocalhost` binds both). From the owning PID it reads the process facts (`IProcessInspector`: session, owner SID, creation time, image path).

The connection is **verified** if the process is the installed tray client (`<install folder>\TrayClient\EagleEye.TrayClient.exe`, by full path, ADR-011 §5) and runs in a user session. `TrayConnectionRegistry` keeps per session the verified connections with owner SID and connection time. Unverified connections keep working as before (version query, pairing codes) but never receive kid messages.

Debug builds only (`#if DEBUG`): a tray client of any path named `EagleEye.TrayClient.exe` counts as verified, so DEV can smoke-test from the build output.

### 2. A request with a result

New callback on `ITrayClientCallback`, sent to **one** verified connection of the session (the most recent one, so a second tray instance started by the kid never causes a second dialog):

```csharp
Task<KidMessageResult> ShowBreakTimeMessage(BreakTimeMessageDto message);
```

It uses SignalR **client results** (`ISingleClientProxy.InvokeAsync<T>`, .NET 7+): the tray answers at once, without waiting for "OK":
- `Shown` — the dialog was created and is visible;
- `AlreadyOpen` — a break-time message is already open in this tray client; nothing new is shown (AC-32).

The service waits at most **3 s**. No verified connection, a timeout or an error → the message state is `not shown (tray client not connected)` or `not shown (tray client did not answer)`. There is **no delayed delivery** (OQ-9). The enforcement never waits for the message (NFR-R-012).

### 3. The message dialog in the tray client

A small WinForms dialog `BreakTimeMessageDialog` instead of the Win32 `MessageBox`, because the story needs behaviour `MessageBox` cannot give:

- Title "EagleEye", an information icon, the display text with its line breaks, one button "OK" (`AcceptButton`, so Enter works). Text width at most about 480 logical pixels, word wrap, a scroll bar only for very long texts (≤ 500 characters, OQ-8). Per-monitor DPI aware (150 %, AC-4 style).
- **Topmost** (`TopMost = true`, `WS_EX_TOPMOST`), shown in the taskbar, centred on the primary screen, no minimise/maximise, no close box. **Esc and Alt+F4 do not close it**; only "OK" and Enter do (AC-30). It never blocks sign-out or shutdown (`CloseReason.WindowsShutDown` and `TaskManagerClosing` close it).
- **Focus**: after `Show()`, the dialog calls `Activate()`/`SetForegroundWindow`. Windows' **foreground lock** lets a background process take the focus only in documented cases. If the dialog is not the foreground window afterwards, the tray uses the established workaround once: a synthetic Alt key press and release (`SendInput`) immediately followed by `SetForegroundWindow`. If Windows still refuses, the dialog stays topmost and flashes in the taskbar, and it normally receives the focus when the blocked app's window closes (Windows then activates the next top-level window in the Z order, which is the topmost dialog). DEV and TES verify this behaviour; it is not guaranteed by a documented API contract.
- **Emojis**: WinForms draws with GDI; Windows' font fallback renders emojis from *Segoe UI Emoji* in **monochrome**, which AC-31 accepts. DEV checks "😊 📚 👍" in the smoke test; if an emoji shows as a box, the text label uses the font "Segoe UI Emoji" explicitly as a fallback.
- One dialog per tray client at a time; after "OK" the next message opens a new one (AC-32).
- The title and the button follow the kid's Windows display language (`TrayTexts`, "OK" in both languages); the display text is shown exactly as stored (AC-31).

The dialog is **not system-modal** in the old Windows 3.x sense: Win32 has no modal state across processes. "System-modal" in FR-TRAY-022 is implemented as *topmost, in front of every normal window, with the keyboard focus*. Other windows remain clickable behind it. Exclusive full-screen DirectX games are drawn above all windows by the display driver; the dialog appears when such a game is closed (by the close sequence, at the latest after 20 s).

### 4. What stays

- The tray client stays read-only and informational (ADR-004): it never writes state and is not needed for enforcement.
- `OnShowPairingCode` keeps going to all tray connections (US-002 behaviour).
- ADR-003's client-asserted `RegisterSession(userSid)` and the `Tray:{userSid}` groups are **replaced** by the service-side binding of §1 (sessions instead of SIDs, because a message belongs to the session in which the app was started, AC-34).

---

## Rationale

### Alternatives Considered

| Option | Reason Rejected |
|---|---|
| Client-asserted session/SID (`RegisterSession`, ADR-003) | Any kid program can connect to the loopback hub and claim a session; it could swallow the message (the log would wrongly say "shown"). The TCP owner lookup costs one Win32 call per connection and cannot be faked by the client. |
| Broadcast to all tray connections; each tray checks its own session | Leaks the display text of one account into other sessions (AC-34) and still cannot report "shown". |
| The session agent shows the message (SYSTEM, already in the session) | A SYSTEM window on the kid's desktop is exactly what ADR-011 §8 forbids (shatter attacks, T-3). |
| `WTSSendMessage` from the service (no tray needed) | Shows a session-0-owned message box in the kid's session; works even without the tray, but: no emoji guarantee, no control over Esc/close, title/button language of the service, and it contradicts FR-TRAY-022 (tray shows it). Kept as a possible fallback for a later story, not used now. |
| Win32 `MessageBox` with `MB_SYSTEMMODAL \| MB_TOPMOST \| MB_SETFOREGROUND` | Simplest, but Esc closes an `MB_OK` box, no DPI control for long texts, and the same foreground-lock limits apply. |
| Fire-and-forget push | The service could not tell "shown" from "tray not running" (AC-33). |

---

## Consequences

### Positive

- The message reaches exactly the session of the blocked start (AC-34), only through the genuine tray client.
- The log and history state truthfully whether a dialog was opened.
- The same binding serves later per-session tray features (remaining time, warnings).

### Negative / Trade-offs

- One more Win32 interop piece in the service (TCP table) and one in the tray (`SendInput`, `SetForegroundWindow`).
- Keyboard focus is best effort, limited by Windows' foreground rules (see §3).
- If the kid ends the tray client, no message is shown; the app is closed anyway (OQ-9, R-6).

### Neutral

- Old tray clients (0.4.x) connected to a 0.5 service are verified but have no handler for the new callback; the call fails and is logged as "not shown". Installers update both together.

---

## References

- US-005: `02_Implementation/docs/requirements/user-stories/US-005/user-story.md` (AC-30 to AC-34) and `implementation-plan.md`
- Requirements v1.5: FR-TRAY-022, FR-APP-052, NFR-R-012
- ADR-003 (hub design; tray groups replaced), ADR-004 (tray read-only), ADR-008 (loopback endpoint), ADR-011 (§5 identity by path, §8), ADR-013

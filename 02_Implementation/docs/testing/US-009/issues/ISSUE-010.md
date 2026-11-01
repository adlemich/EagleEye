# ISSUE-010: Windows parent app: status bar stays green ("Verbunden mit …") when the service is stopped or not responding

**Status**: New (known issue, under observation; see "Decision")
**User Story**: US-009 (AC-6; the behaviour is US-002 AC-10, AC-22)
**Found in**: `02_Implementation/docs/testing/US-009/test-run-01.md`, TC-009-29 (Fail, Windows 10) and TC-009-18 (ticked Pass, with an "Observed" note on Windows 11)
**Date**: 2026-11-01
**Severity**: Medium (TES proposal: the status bar shows a wrong connection state, so the parent may believe a change can be saved; no data is lost, the page shows the save error, and enforcement on the service PC is not affected)
**Machine**: Windows 10 PC (Windows app 0.9.0, remote to the service PC) and Windows Developer Machine = service PC (Windows app 0.9.0 on Windows 11)
**Build**: `03_Delivery/windows/EagleEye-ParentApp-Setup-0.9.0.exe` (SHA-256 `780CE6CB…1D88F0`), service `03_Delivery/windows/EagleEye-Setup-0.9.0.exe` (SHA-256 `4F1EF797…CB1652`)

## Decision

**Known issue, under observation; not fixed in US-009 (decided by Michael 2026-11-01).** US-009 is closed with this issue open. The issue stays `New` until it is picked up (DEV analysis, step c of a later story or patch). It is **not** `Verified/Closed`.

## Description

When the EagleEye service is stopped (Windows 10, TC-009-29) or does not respond (Windows 11, service process suspended, TC-009-18), the status bar at the bottom of the Windows parent app stays **green** with **"Verbunden mit 192.168.1.37"**. AC-6 (US-002 AC-10, AC-22) asks for red **"Nicht verbunden mit 192.168.1.37"** within 30 s after the service stops. In TC-009-18 the page correctly showed **"Die Änderung konnte nicht gespeichert werden. Bitte erneut versuchen."**, so the page and the status bar contradict each other.

The same case on Windows 11 with the app on the service PC itself (TC-009-10 step 1, `Stop-Service`) passed: red within 30 s. The failure was seen with the app on a **remote** PC (Windows 10) after a service stop, and on Windows 11 while the service process was suspended.

## Steps to Reproduce

**A: service stopped (TC-009-29, Windows 10 PC, remote)**

1. Windows app 0.9.0 on the Windows 10 PC, paired with the service PC `192.168.1.37`, status bar green.
2. On the service PC, in Terminal (Administrator): `Stop-Service -DisplayName "EagleEye Service"`; start a stopwatch.
3. Watch the status bar of the app on the Windows 10 PC for at least 30 s.

**B: service not responding (TC-009-18, Windows 11, app on the service PC)**

1. Windows app 0.9.0 on the service PC, on *App Gruppen*, status bar green; all kids signed out.
2. `resmon` → **CPU** → right-click `EagleEye.Service.exe` → **Prozess anhalten**.
3. Within 10 s: **≡** on **Paint** → **School**. Wait up to 15 s.
4. Watch the status bar; at the latest 25 s after step 2: **Prozess fortsetzen**.

## Expected Result

- A: red **"Nicht verbunden mit 192.168.1.37"** within **30 s** after the service stops; green again within 60 s after `Start-Service` (AC-6, US-002 AC-10, AC-21, AC-22).
- B: the save error text and the page returning to the stored state (AC-18; observed as expected). The status bar must not claim "Verbunden" while the app cannot reach the service. *Note for PRO/ARC:* the story gives no time limit for a **suspended** (not stopped) service; Michael expects the bar to turn red as soon as a change fails because the service is down. Whether a failed save should turn the bar red at once, or only after the connection timeout, is a requirement decision.

## Actual Result

- TC-009-29 (Fail), Observed: "The status bar in the App on Windows 10 stayed green."
- TC-009-18 (ticked Pass), Observed: "Even though that the text "...konnte nicht gespeichert werden..." is shown, the status line in the windows parent app still showed green (and connected text). This is a mismatch, the status bar should show red when the server is down."

Not recorded: how long Michael watched in TC-009-29, and the results of TC-009-29 steps 1 and 3 (green ≤ 30 s after app start, green ≤ 60 s after `Start-Service`).

## Affected Platforms

| Platform | Status |
|---|---|
| Windows 10 22H2, Windows app 0.9.0, remote | **Confirmed** (TC-009-29) |
| Windows 11, Windows app 0.9.0 on the service PC | **Confirmed** for a suspended service process (TC-009-18). A plain `Stop-Service` turned the bar red as expected (TC-009-10). |
| macOS, Mac app 0.9.0 | **Not tested in run 01** (no Mac case stops or suspends the service; TC-009-44 skipped). The connection and status-bar logic is shared code in `EagleEye.ParentApp.Core` (see the DEV pointer), so the Mac app is **probably affected in the same way**. |

## Related ACs and Test Cases

- **AC-6** (US-002 AC-10, AC-21, AC-22 on Windows 10): TC-009-29 → Fail.
- **AC-18** (save error path): TC-009-18 → Pass as ticked (its expected results are met); the status bar observation is this issue.
- Same expectation elsewhere: TC-009-10 step 1 (W11, Pass), TC-009-02 (tray client, Pass), TC-009-55 (skipped).

## Evidence

No screenshots and no service-log copies for run 01 are in the repository (`02_Implementation/docs/testing/US-009/evidence/` does not exist; LOG-COPY after TC-009-29 was not done). If the issue is picked up, a re-run of TC-009-29 with a stopwatch, a screenshot of the status bar at 30 s and 60 s, and LOG-COPY into `evidence/run-NN-service-logs/after-TC-009-29` is needed.

## Pointer for DEV (code read by TES read-only, only to locate the cause; not used to define expected behaviour)

1. **Status bar source.** `02_Implementation/src/EagleEye.ParentApp.Core/ViewModels/StatusBarViewModel.cs` shows green only for `ConnectionStatus.PairedConnected` and follows `ConnectionCoordinator.StateChanged`. In `02_Implementation/src/EagleEye.ParentApp.Core/Communication/ConnectionCoordinator.cs` the paired state leaves `PairedConnected` **only** through the SignalR lifecycle events of the hub connection (`OnPairedReconnecting`, `OnPairedClosed`, wired in `AttachPairedClient`) and the connect loop.
2. **Save error path is separate.** A write goes through `ParentHubGateway.InvokeAsync` (`.../Communication/ParentHubGateway.cs`) with a write timeout of **4 s** (`SelectedAccountModel.WriteTimeout`, `UserAccountsModel.WriteTimeout`; `Groups/AppGroupsModel.cs` returns `false` on "rejection, connection loss or timeout"). The page then sets `ErrorText = AppTexts.AccountSaveFailed` (e.g. `ViewModels/AppGroupsViewModel.cs` `SendAsync`). Nothing tells the coordinator or the status bar that the service did not answer. That explains TC-009-18: the save error after 4 s, the bar still green.
3. **Timeouts are SignalR defaults.** `.../Communication/ParentHubClient.cs` sets no `ServerTimeout` / `KeepAliveInterval`, and the service registers `AddSignalR()` with defaults (`02_Implementation/src/EagleEye.Service/Program.cs`). Without a TCP close reaching the client, the client only raises `Reconnecting` after its server timeout (default 30 s), which is at or beyond the AC limit of 30 s. A suspended process keeps the TCP connection open, so in TC-009-18 (resumed within 25 s) the client may never notice. For TC-009-29 (remote PC, service stopped) DEV should check whether the close or reset reaches the remote client (firewall drops after the process ends?) and whether `Reconnecting` fires at all, by reproducing with the Debug app and logging the hub events with time stamps. A 30 s server timeout plus the reconnect schedule (0 s, 2 s, 10 s, 30 s) may also simply exceed "red ≤ 30 s".
4. Options for ARC/DEV (not decided): a shorter client `ServerTimeout` with a matching service `KeepAliveInterval`; and/or let a write that fails with a timeout or connection error trigger a connection check that sets `PairedDisconnected`. The tray client (`EagleEye.TrayClient/Communication/ServiceConnection.cs`) passed the same stop test (TC-009-02) and is a useful comparison.

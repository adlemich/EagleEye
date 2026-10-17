# ISSUE-009: Parent app stuck in "Gekoppelt" after the service identity changed (pairing cannot be removed)

**Status**: Implemented (patch **0.6.2**, installers built 2026-10-17)
**User Story**: US-006 (fix changes approved US-002 behaviour, see below)
**Found in**: Michael's live use on 2026-10-17, after installing 0.6.1
**Date**: 2026-10-17
**Severity**: High (the parent app cannot be used with the service, and no in-app way out exists)
**Machine**: parent app on `ZOCK-O-MAT-V3`, paired with `localhost` (the service on the same PC)
**Build**: `03_Delivery/windows/EagleEye-ParentApp-Setup-0.6.1.exe`
**Evidence**: `Screenshot_Deadlock_Kopplung.png` (Michael); after the fix: `ISSUE-009-*.png` (this folder)

## Description

The Settings page shows "Kopplungsstatus Gekoppelt", "Gekoppelt mit: localhost" and the red message "Die Identität des EagleEye-PCs localhost hat sich geändert. Die Verbindung wurde abgelehnt." The button "Kopplung aufheben" is disabled with the hint "Zum Aufheben der Kopplung ist eine Verbindung zum EagleEye-PC nötig." The status bar shows "Nicht verbunden mit localhost". The app never connects, and it cannot be unpaired, so a fresh pairing with a new code is impossible: a deadlock.

## Steps to Reproduce

1. Pair the parent app with a service.
2. The service gets a new TLS certificate (new data folder, reinstall into a fresh folder, or pairing with a different service at the same host).
3. Start the parent app: the pinned thumbprint no longer matches, so the connection is refused (US-002 AC-12) and removing the pairing needs a connection (US-002 AC-30, Q-3).

## Expected Result

The parent can leave the broken pairing and pair again with a new code.

## Actual Result

There is no way out in the app. Only deleting the app data by hand helps.

## Resolution

*(DEV, 2026-10-17)*

**Status**: Implemented (patch **0.6.2**)

**Root cause** (confirmed): the pairing stored in Michael's `%LocalAppData%\EagleEye` on `ZOCK-O-MAT-V3` does not belong to the installed service. It is left over from DEV's US-004 smoke check on **2026-10-08**. The Debug parent app was paired with the Debug console service, whose data folder was `02_Implementation/artifacts/smoke-us004`. The log `artifacts/smoke-us004/logs/EagleEye.Service-001.log` shows "2026-10-08 07:07:04.725 … TLS certificate created, thumbprint 6231699086A0…F33D", which is the thumbprint pinned in that app data. The US-004 implementation report (§8) states the Debug parent app's data was left in `%LocalAppData%\EagleEye`. Separate parent-app dev data (`EAGLEEYE_DEV_APP_DATA_DIR`) was introduced only in US-005 (D-5). The installed parent app (same data folder) took over that pairing, and the installed service has its own certificate, so pinning correctly refuses it. This is not a 0.6.1 regression. The real defect is the missing way out: US-002 made unpairing online-only (AC-30, Q-3), and no parent app function exists to remove another device's entry on the service (`RemovePairedDeviceAsync` removes only the own device).

**Fix** (Michael's option A, local "forget pairing"):
- New button **"Kopplung auf diesem Gerät löschen"** / "Forget pairing on this device" on the Settings page, below the disabled "Kopplung aufheben".
- **Trigger**: visible and enabled only when the app is paired, *not* connected, and the last connection attempt failed with "identity changed" (`ConnectionMessage.CertificateChanged`) or "unreachable" (`ConnectionMessage.Unreachable`). These are the US-002 AC-12 error states the paired connect loop already sets; they stay set across retries, so the button does not flicker. While connected, only the online "Kopplung aufheben" is offered (unchanged).
- **Limitation**: after a drop at runtime, the SignalR auto-reconnect shows "Nicht verbunden" without a message. The button then appears once a connect attempt fails, at the latest after an app restart.
- **Confirmation dialog** (title / text / buttons):
  - de: "Kopplung auf diesem Gerät löschen?" / "Der EagleEye-PC {host} ist nicht erreichbar oder seine Identität hat sich geändert. Die Kopplung wird nur auf diesem Gerät gelöscht. Danach kann die App mit einem neuen Kopplungscode neu gekoppelt werden. Der Geräte-Eintrag kann auf dem EagleEye-PC bestehen bleiben; eine andere Eltern-App kann ihn dort derzeit nicht entfernen." / "Kopplung auf diesem Gerät löschen", "Abbrechen"
  - en: "Forget pairing on this device?" / "The EagleEye PC {host} cannot be reached or its identity has changed. The pairing is deleted on this device only. Afterwards the app can be paired again with a new pairing code. The device entry may remain on the EagleEye PC; another parent app cannot remove it there at the moment." / "Forget pairing on this device", "Cancel"
- **On confirm**: the connect loop is stopped, and the stored pairing (host, device ID, certificate thumbprint, DPAPI-protected token) is deleted through `PairingStore.DeleteAsync`. The app returns to "Nicht gekoppelt" with the host pre-filled and shows the info "Die Kopplung mit {host} wurde auf diesem Gerät gelöscht. Für eine neue Kopplung ist ein neuer Kopplungscode nötig." (en: "The pairing with {host} was deleted on this device. Pairing again requires a new pairing code."). Log (Information): "Pairing with {Host} forgotten on this device only (service identity changed | not reachable); the device entry may remain on the service." The parent app has no file log sink yet, so in Release the entry goes to the debug output only.
- **On cancel**: nothing changes.

**Visual verification** (Debug parent app 0.6.2 with separate dev data `artifacts/smoke-us009-app` against the Debug console service on the offset ports, data `artifacts/smoke-us006`; the mismatch was reproduced by setting the pinned thumbprint to 64 zeros; screenshots read by DEV; 200 %, dark mode):
- `ISSUE-009-1-identity-changed.png`: Michael's state reproduced, with the new button;
- `ISSUE-009-2-confirm-dialog.png`: the confirmation dialog;
- `ISSUE-009-3-forgotten.png`: "Nicht gekoppelt", host pre-filled, info text; the pairing row is gone from the database;
- `ISSUE-009-4-repaired.png`: paired again with a new code from the Debug tray client, "Verbunden mit localhost", accounts and warnings loaded.

**Workaround for 0.6.1** (if not updating): close the parent app, rename `%LocalAppData%\EagleEye` (for example to `EagleEye.old`), start the app, and pair again with a new code. Note that this also resets the app's local settings (theme).

**Artifacts**:

| Installer | Built | SHA-256 |
|---|---|---|
| `03_Delivery/windows/EagleEye-Setup-0.6.2.exe` | 2026-10-17 15:57 | `9D1942E7FC43B453796E1162A729EFD508F04511AD76A67A8E53A320BD11D776` |
| `03_Delivery/windows/EagleEye-ParentApp-Setup-0.6.2.exe` | 2026-10-17 15:59 | `F1E8179F7FDA36A17BB16116BBC5EC1BF7D9B02DB5C6CA385E1995BE7344E0FC` |

**Reinstall**: `EagleEye-ParentApp-Setup-0.6.2.exe` on every parent PC (on `ZOCK-O-MAT-V3` this resolves Michael's deadlock: press the new button, then pair again). The service and tray client code are unchanged since 0.6.1. Installing `EagleEye-Setup-0.6.2.exe` is optional and only keeps the version numbers in sync.

**Device entry left on the service**: the old device entry of the US-004 smoke pairing exists only in the Debug data folder `artifacts/smoke-us004`, not on the installed service, so nothing is left behind on Michael's service in this case.

**Files**: `02_Implementation/src/EagleEye.ParentApp.Core/Communication/{IConnectionCoordinator,ConnectionCoordinator,ConnectionMessage}.cs`, `.../ViewModels/{ServerConnectionViewModel,ConnectionMessageText}.cs`, `.../AppTexts.cs`, `.../Resources/AppTexts{,.en}.resx`, `02_Implementation/src/EagleEye.ParentApp/Views/SettingsView.xaml`, tests in `02_Implementation/tests/EagleEye.ParentApp.Tests/` (`ConnectionCoordinatorTests`, `ServerConnectionViewModelTests`, `ConnectionMessageTextTests`), `02_Implementation/Directory.Build.props` (0.6.2).

**Build** (audit on): `build.ps1` 0 warnings, 0 errors; no NuGet advisories. `test.ps1` 2 652 passed, 0 failed (Shared 282, Service 1 365, TrayClient 90, ParentApp 915). Coverage of the changed classes (`ConnectionCoordinator`, `ServerConnectionViewModel`, `ConnectionMessageText`, `AppTexts`): 100 % lines and branches.

# ADR-008: Parent App Connectivity — Endpoints, TLS Trust and Pairing Protocol

**Status**: Accepted (approved by Michael with the US-002 implementation plan, 2026-10-04)
**Date**: 2026-10-04
**Deciders**: ARC, Michael

---

## Context

US-002 opens the service to the LAN for the first time. ADR-003 (two hubs) and ADR-004 (pairing) define the model, and arc42 §8.1 to §8.3 the TLS and auth concepts. Several concrete decisions are still open, and US-001 made deliberate simplifications that now need resolving:

1. US-001 runs the `TrayHub` over **plain HTTP on `localhost:5080`** (US-001 plan, deviation D-1). arc42 §8.3 says both hubs share one HTTPS port.
2. Which network interfaces and ports are reachable from the LAN, and how the hubs are kept apart so that a LAN client can never reach the `TrayHub`. The `TrayHub` will receive pairing codes in US-002.
3. How the parent app trusts the self-signed certificate: accept everything, or pin.
4. How exactly an unpaired client gets a code. ADR-004 says "service detects unknown client on connect".
5. How the token travels, and how "only pairing methods for unpaired clients" is enforced.
6. Firewall and file-system protection on the service PC (AC-6 of US-002: no manual configuration).

---

## Decision

### 1. Two endpoints, each hub bound to its own endpoint

| Endpoint | Binding | Protocol | Hub | Clients |
|---|---|---|---|---|
| Tray endpoint | `localhost:5080` (loopback only) | HTTP | `TrayHub` (`/hubs/tray`) | Tray clients on the same PC |
| Parent endpoint | all interfaces, port **5443** | HTTPS (TLS 1.2+) | `ParentHub` (`/hubs/parent`) | Parent apps on the LAN or the same PC |

- The hub-to-endpoint binding is enforced by the **local port of the TCP connection** (`HttpContext.Connection.LocalPort`), never by the `Host` header (a client controls that header). Requests to `/hubs/tray` on any port other than 5080, and to `/hubs/parent` on any port other than 5443, are answered with `404`.
- The `TrayHub` additionally rejects connections whose remote address is not loopback (defence in depth).
- The tray stays on HTTP/loopback: the traffic never leaves the machine, and the tray client does not need to trust a certificate. This **amends arc42 §8.3** ("both hubs share one port and certificate"). Moving the tray to TLS later is possible without contract changes.

### 2. Certificate

- Generated on first start by the service (`Certificates/CertificateManager`): ECDSA P-256, `CN=EagleEye`, SAN = machine DNS name and `localhost`, validity 100 years (FR-SVC-060 to FR-SVC-062).
- Stored as `%ProgramData%\EagleEye\certs\eagleeye.pfx`. The PFX bytes are encrypted with DPAPI (`DataProtectionScope.LocalMachine`), and the `certs\` folder is restricted by ACL to SYSTEM and Administrators (set by the installer). The ACL is the actual protection; DPAPI only keeps the file useless outside this machine. This replaces "password derived from machine-specific data" in arc42 §8.1.
- On later starts the same certificate is loaded. It is regenerated only if the file is missing or unreadable (logged as a warning).

### 3. Parent app trust: trust on first use, then pinning

- **Unpaired**: the parent app accepts the certificate the service presents and remembers its SHA-256 thumbprint for the pairing in progress.
- **On successful pairing**: the thumbprint is stored with the pairing (host, device ID, device name).
- **Paired**: the app accepts **only** the pinned thumbprint. A different certificate is a connection error with its own message ("the identity of the EagleEye PC has changed"). Recovery: uninstalling the app removes the pairing (US-002 AC-5). A guided recovery follows with device management (FR-APP-015).
- Certificates are never installed into an OS certificate store (coding guidelines §13.1, §16.1).

### 4. Pairing protocol on `ParentHub`

ADR-004 stays valid. US-002 makes it concrete:

| Method | Callable by | Purpose |
|---|---|---|
| `GetPairingStatus()` | anyone | Tells the client whether this connection is authenticated as a paired device (and which). Used after every (re)connect. |
| `StartPairing()` | unpaired connections | Generates a **new** 6-digit code, bound to the calling connection, valid 5 minutes. Replaces any earlier pending code. Pushes the code to all tray clients, or writes it to the Event Log when none is connected. |
| `SubmitPairingCode(code, deviceName)` | unpaired connections | Validates code, binding and expiry. Success: issues the token. **Any failure invalidates the pending code**, so the next attempt needs a new code (one guess per code). |
| `RemovePairedDevice(deviceId)` | paired connections | De-registers a device (ADR-003 signature). Other connections of that device are aborted. |

- The code is requested **explicitly** (`StartPairing`) instead of "on connect" as ADR-004 step 2 describes. Reconnects or repeated connects therefore do not flood the kid's screen with codes, and "new attempt → new code" (US-002 AC-18, AC-19) is a client action.
- The code is bound to the connection that requested it. A different connection cannot submit it.
- **Default deny**: a hub filter (`IHubFilter`) rejects every `ParentHub` method for unpaired connections unless the method is marked `[AllowUnpaired]`. New methods are therefore protected by default.

### 5. Token transport and authentication

- After pairing, the client sends the token as `Authorization: Bearer <token>` (SignalR `AccessTokenProvider`). The .NET SignalR client sends it as a header, also for WebSockets.
- `ParentHub.OnConnectedAsync` hashes the token (SHA-256), looks it up in `PairedDevices`, and on success marks the connection as paired (device ID in `Context.Items`) and adds it to the `Parents` group. An unknown token leaves the connection unpaired (ADR-004). The client learns this via `GetPairingStatus()`.
- `Microsoft.AspNetCore` log categories are limited to `Warning`, so request lines (which could contain query strings) are never logged. Tokens and codes are never passed to `ILogger`.

### 6. Pairing code display

- Tray: new callback `ITrayClientCallback.OnShowPairingCode(string code)` (ADR-003 signature) to **all** connected tray clients. The tray shows a small topmost window (not a balloon tip, which Focus Assist or "Do not disturb" can suppress) that closes itself when the code expires.
- No tray connected: Event Log entry, log `Application`, source `EagleEye`, level Information, event ID 1000, text in German and English. This is the **only** place a pairing code is written to a log, as FR-SVC-092 requires. It is written by a dedicated writer, not through `ILogger`.

### 7. Service PC configuration by the installer

- Windows Firewall inbound rule "EagleEye Service (Parent apps)": TCP 5443, program `EagleEye.Service.exe`, **all profiles, remote addresses `LocalSubnet` only**. Home networks are often classified as "Public", so a Private-only rule would break AC-6. `LocalSubnet` keeps the service LAN-only (CN-010).
- ACLs (well-known SIDs, so that German Windows works): `%ProgramData%\EagleEye\` SYSTEM and Administrators full control, Users read; `certs\` SYSTEM and Administrators only.
- Uninstall removes the firewall rule.

---

## Rationale

- Binding hubs to endpoints by local port is the only check a remote client cannot forge. Without it, a LAN client could open `/hubs/tray` on port 5443 and receive every pairing code.
- Pinning after first use protects the token against a man-in-the-middle on the LAN, at almost no cost. The first contact remains trust-on-first-use, which ADR-004 already accepts (the code proves physical access to the PC).
- One guess per code makes brute force pointless, even without rate limiting.

### Alternatives Considered

| Option | Reason Rejected |
|---|---|
| Both hubs on HTTPS 5443 (arc42 as written) | The tray would need certificate trust, and LAN clients would reach the `TrayHub` endpoint. No benefit for loopback traffic. |
| Restrict hubs by `Host` header (`RequireHost`) | The client controls the header, so it is not a security boundary. |
| Accept any certificate, always | The token could be captured by a LAN man-in-the-middle and reused. |
| Generate the code on connect (ADR-004 wording) | Reconnect loops would push codes repeatedly. "New attempt, new code" would need a reconnect. |
| `[Authorize]` attribute per method | Fails open when a future method forgets the attribute. Default-deny filter fails closed. |
| Firewall rule for Private/Domain profiles only | Breaks on networks Windows classifies as Public, which is common at home. |

---

## Consequences

### Positive

- The LAN surface is one TLS endpoint, and every method on it except pairing requires a paired device.
- No manual firewall, certificate or URL ACL steps for the parent (US-002 AC-6, AC-7).
- Contract and auth model are ready for the mobile and macOS apps.

### Negative / Trade-offs

- If the service certificate is lost (e.g. `%ProgramData%\EagleEye` deleted), paired apps refuse to connect until they are re-paired. In US-002 that means reinstalling the app.
- Two ports instead of one (5080 loopback, 5443 LAN).
- The pairing code is visible in the kid's tray session (accepted by Michael for US-002, see the US-002 story, Decision Q-1).
- **Security consequence, accepted risk (Michael, 2026-10-04):** because the code reaches the kid's session and the parent app installer needs no admin rights, a kid can pair their own parent app (on the same PC or another device). Once configuration stories exist, such an app could change the kid's own rules. Michael decided: **no technical protection for now**; revisit before or with the first configuration story. Tracked as arc42 §11 R-8.
- The tray client keeps running in admin sessions (Michael, 2026-10-04), so a parent pairing at the service PC as admin sees the code there.

### Neutral

- `IParentClientCallback` stays empty in US-002. Broadcasting the paired-device list (ADR-003 Rule 1) comes with device management (FR-APP-015).
- Ports remain constants until the service gets its YAML configuration (ADR-002).

---

## Implementation Notes (US-002, accepted deviations, 2026-10-04)

- Certificate key loading: `MachineKeySet` as decided. If the machine key store is not writable (service in console mode without admin rights, used only for DEV smoke checks), `CertificateManager` falls back to `UserKeySet` and logs a warning (US-002 implementation report, D-1). As SYSTEM the service always uses `MachineKeySet`.
- The parent app bounds each connect attempt and each hub call to 15 s, so an unreachable host ends in a clear error (D-10).

---

## References

- ADR-003 (hub design), ADR-004 (pairing), ADR-002 (persistence, logging)
- arc42 §7, §8.1 to §8.3 (amended by this ADR), §11 R-8
- US-002 implementation report: `02_Implementation/docs/requirements/user-stories/US-002/implementation-report.md` §2
- US-002: `02_Implementation/docs/requirements/user-stories/US-002/user-story.md`, implementation plan in the same folder

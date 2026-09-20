# ADR-004: Pairing-Based Authentication for Parent Apps

**Status**: Accepted
**Date**: 2026-09-20
**Deciders**: Michael (project owner), ARC

---

## Context

EagleEye operates on a local LAN with no cloud accounts, no internet dependency, and no user registration. Parent apps connect to the Windows service over the network and must be authorized to read data and modify configuration. The system must distinguish between:

- **Authenticated parent apps** — allowed to query all data and modify all configuration
- **Unknown clients** — must be rejected or limited to the pairing flow only
- **TrayClient** — connects on localhost only, read-only, no configuration access

A mechanism is needed to establish trust between a parent app and the service without relying on external identity providers, cloud accounts, or pre-shared certificates. The solution must be usable by non-technical parents (no CLI, no certificate import, no file sharing).

Requirements: FR-SVC-090 through FR-SVC-098.

---

## Decision

We will use a **pairing-code-based authentication** model, inspired by Bluetooth device pairing. The trust relationship is established once via a short numeric code displayed on the Windows PC, then maintained via a persistent shared secret.

### Pairing Flow

```
1. Parent app connects to ParentHub for the first time (no stored credential)
2. Service detects unknown client → generates a cryptographically random 6-digit numeric code
3. Service stores the code with a 5-minute expiry timestamp
4. Service pushes the code to the connected TrayClient for display as a popup notification
   - If no TrayClient is connected: service writes the code to the Windows Event Log (Information level)
5. Parent (person) reads the code from the Windows PC screen and enters it in the parent app
6. Parent app submits: code + user-chosen device name (e.g., "Mom's iPhone")
7. Service validates:
   a. Code matches the pending code
   b. Code has not expired (< 5 minutes since generation)
8. On success:
   a. Service generates a persistent authentication token (cryptographically random, 256-bit)
   b. Service stores in SQLite (PairedDevices table): device name, token hash (SHA-256), pairing timestamp
   c. Service returns the token to the parent app
   d. Parent app stores the token and the service hostname in its local SQLite database
9. On failure (wrong code or expired):
   a. Service rejects the request with an error
   b. A new code must be generated for the next attempt
```

### Subsequent Connections

After pairing, the parent app includes its stored token in the SignalR connection handshake (as a custom header or query parameter). The service:

1. Receives the token from the connection request
2. Hashes the token (SHA-256) and looks it up in the `PairedDevices` table
3. If found: connection is authenticated → client is added to the `Parents` SignalR group
4. If not found: connection is treated as unpaired → only the `SubmitPairingCode` method is callable

### Token Security

- Tokens are generated using `System.Security.Cryptography.RandomNumberGenerator` (256-bit)
- The service stores only the **SHA-256 hash** of the token, never the plaintext
- The parent app stores the plaintext token in its local SQLite database
- Tokens are transmitted only over TLS-encrypted SignalR connections
- Tokens never appear in log files (replaced with `***`)

### Device Management

- Multiple parent apps may be paired simultaneously (each has its own token)
- Any paired parent app can view all paired devices (`GetPairedDevices()`)
- Any paired parent app can de-register any paired device, including itself (`RemovePairedDevice(deviceId)`)
- De-registration deletes the token hash from the `PairedDevices` table; the next connection from that device will be treated as unpaired

### TrayClient Authentication

The TrayClient does not use the pairing mechanism. It connects to `TrayHub` on `localhost` and authenticates implicitly:

- Connection source must be `localhost` (enforced by the hub)
- Client provides its Windows user SID via `RegisterSession(userSid)`
- The service verifies the SID corresponds to a real standard-user account
- TrayClient has read-only access: it receives pushes but cannot invoke any mutating methods

---

## Rationale

### Why pairing codes?

The pairing model gives a non-technical parent a simple, familiar experience (similar to pairing a Bluetooth speaker or smart TV). It requires no CLI, no certificate import, no account creation, and no internet connection.

### Why hash tokens on the server?

If the SQLite database is compromised (e.g., copied from the PC), the attacker cannot extract usable tokens. They would need the parent app's local database to obtain the plaintext token.

### Why 6 digits?

Six digits provide 1,000,000 combinations. With a 5-minute expiry and one active code at a time, brute-force is infeasible. More digits would reduce usability (harder to read and type).

### Alternatives Considered

| Option | Reason Rejected |
|--------|----------------|
| Pre-shared password set during installation | Requires the parent to remember a password; no standard mechanism for password recovery without cloud |
| QR code scan | Requires camera access on the parent device and a display mechanism on the service (which runs headless as SYSTEM) — adds complexity |
| mTLS (mutual TLS with client certificates) | Complex certificate distribution; non-technical parents cannot manage certificates; overkill for LAN-only v1 |
| Cloud-based OAuth / identity provider | Requires internet; violates the "no internet dependency" constraint (CN-016) |
| No authentication (trust all LAN clients) | Any device on the LAN could modify rules — unacceptable security risk |

---

## Consequences

### Positive

- Simple, familiar UX — parents have done device pairing before
- No internet, no cloud, no accounts — fully local
- Secure: TLS for transport, hashed tokens at rest, 5-minute code expiry
- Multi-device support: each parent device has its own token
- Revocable: any paired device can de-register any other

### Negative / Trade-offs

- Pairing requires physical or visual access to the Windows PC (to read the code) — by design, this is a security feature, not a limitation
- If no TrayClient is connected and no kid is logged in, the code goes to the Windows Event Log — the parent must check Event Viewer (less convenient, but this is an edge case during initial setup)
- Lost token (parent app uninstalled) requires re-pairing — acceptable since it's a one-time, quick process

### Neutral

- Token rotation is not implemented in v1 — tokens are long-lived. Rotation can be added later without changing the pairing model
- The 5-minute code expiry is configurable in the YAML config if needed in the future

---

## References

- System Architecture: `02_Implementation/docs/architecture/arc42/system-architecture.md` — sections 5.2 (Pairing component), 6.1 (Pairing sequence), 8.2 (Authentication), 8.3 (SignalR Communication Design)
- Product requirements: FR-SVC-090 through FR-SVC-098
- ADR-003: SignalR Hub Design — `ParentHub` requires authentication; `TrayHub` uses localhost identity

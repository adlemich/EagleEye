# ISSUE-005: Administrator gets "Zugriff verweigert" on the service certificate folder

**Status**: New
**User Story**: US-002
**Found in**: docs/testing/US-002/test-run-01.md, TC-002-02 (supporting check, no AC)
**Date**: 2026-10-04
**Severity**: Medium
**Machine**: Windows Developer Machine
**Build**: `03_Delivery/windows/EagleEye-Setup-0.2.0.exe`
**Routed to**: DEV for analysis (with ARC if the design must change). Possibly a test-plan expectation problem, see "Classification" below.

## Description

TC-002-02 checks the ACLs that the service installer sets on the service data folder. Step 1 (`icacls` on `%ProgramData%\EagleEye`) raised no complaint (Michael did not report a deviation; the output was not pasted). Steps 2 and 3, run in Michael's admin account, were refused with *Zugriff verweigert* / *Access ... is denied*. The certificate folder could not be listed, and the existence of `eagleeye.pfx` could not be checked.

## Steps to Reproduce

1. Service PC, Admin account. Install `EagleEye-Setup-0.2.0.exe` over 0.1.1 with default settings (TC-002-01).
2. Open the terminal from S-8 (*Terminal (Administrator)*).
3. `icacls "$env:ProgramData\EagleEye\certs"`
4. `Test-Path "$env:ProgramData\EagleEye\certs\eagleeye.pfx"`

## Expected Result

This is a **supporting check without an AC**. The expectation comes from the architecture, not from the user story:
- `docs/architecture/decisions/ADR-008-parent-connectivity-tls-and-pairing-protocol.md` §7 and `docs/architecture/arc42/system-architecture.md` (data folder table): `certs\` is restricted to **SYSTEM and Administrators** (full control), inheritance removed.
- `docs/requirements/user-stories/US-002/implementation-plan.md` (installer step) and `implementation-report.md` ("How to test", ACLs): `icacls "%ProgramData%\EagleEye\certs"` lists **SYSTEM and Administratoren only**.

Test plan TC-002-02: `icacls` lists only SYSTEM (F) and Administratoren (F) for `certs`, and `Test-Path` returns `True`.

US-002 itself has no AC about folder permissions. The related requirement NFR-S-014 only says that secrets are never stored in source control or logs, or displayed in any UI. The goal behind the check (a standard user, i.e. the kid, cannot read the certificate) was **not** tested in this run (TC-002-03 was skipped).

## Actual Result

Quoted from the run file:

```
PS C:\Users\Admin> icacls "$env:ProgramData\EagleEye\certs"
C:\ProgramData\EagleEye\certs: Zugriff verweigert
0 Dateien erfolgreich verarbeitet, bei 1 Dateien ist ein Verarbeitungsfehler aufgetreten.
PS C:\Users\Admin> Test-Path "$env:ProgramData\EagleEye\certs\eagleeye.pfx"
Test-Path: Access to the path 'C:\ProgramData\EagleEye\certs\eagleeye.pfx' is denied.
False
```

`False` here is caused by the access error. It does **not** show that the file is missing. The service accepted TLS connections on port 5443 in TC-002-11 (the parent app reached the pairing form), so a certificate was loaded.

## Classification (for DEV / ARC)

TES cannot decide from the outside which of these applies:

1. **Product defect:** the installer's ACL on `certs\` does not grant *Administratoren* the access the design intends (for example a wrong SID, a missing grant, or an owner/inheritance problem after the service created the folder). An admin then cannot inspect or back up the certificate, and the documented reset ("delete `%ProgramData%\EagleEye` as admin", implementation report) may fail.
2. **Test-plan expectation problem:** the commands ran **without elevation**. With UAC, an admin's non-elevated token does not use the Administrators group, so access is denied by design. The test plan step says only "PowerShell" and does not require *Terminal (Administrator)*. The prompt `PS C:\Users\Admin>` does not show whether the terminal was elevated. If this is the cause, TES will change TC-002-02 to require an elevated terminal, and this issue is closed as "not a defect".

**TES asks Michael:** was the terminal used for TC-002-02 elevated (title *Administrator: …*)? If unsure, please repeat steps 2 and 3 in an elevated terminal and add the output to this issue. Then DEV analyzes.

## Evidence

- Command output above (from `docs/testing/US-002/test-run-01.md`, TC-002-02). No screenshot.

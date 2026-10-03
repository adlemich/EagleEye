# ISSUE-002: Installed app shows publisher "adlemich" instead of "Michael Adler"

**Status**: Verified/Closed
**User Story**: US-001
**Found in**: docs/testing/US-001/test-run-01.md, TC-001-01
**Date**: 2026-10-03
**Severity**: Low
**Machine**: Windows Developer Machine
**Routed to**: DEV (Michael approved the fix for 0.1.1)

## Description

In *Einstellungen → Apps → Installierte Apps*, EagleEye is listed with the publisher "adlemich" (the GitHub account name). Michael expects "Michael Adler".

## Steps to Reproduce

1. Install `EagleEye-Setup-0.1.0.exe`.
2. Open *Einstellungen → Apps → Installierte Apps* and look at the EagleEye entry.

## Expected Result

Publisher: **Michael Adler**

## Actual Result

Publisher: **adlemich**

## Evidence

Run file TC-001-01, Observed.

## Notes

The fix is one line in `installer/windows/setup.iss` (`AppPublisher`). The `Company` property in `Directory.Build.props` ("EagleEye") should be aligned at the same time.

## Resolution

DEV, 2026-10-03: `AppPublisher` in `installer/windows/setup.iss` and `Company` in `Directory.Build.props` set to "Michael Adler". Build 0.1.1. Re-test: `docs/testing/US-001/test-run-02.md`, TC-001-R01.

**Verified**: TES, 2026-10-03, in `docs/testing/US-001/test-run-02.md` (build 0.1.1). Closed by Michael.

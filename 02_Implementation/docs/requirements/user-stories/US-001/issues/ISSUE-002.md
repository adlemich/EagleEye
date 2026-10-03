# ISSUE-002: Installed app shows publisher "adlemich" instead of "Michael Adler"

**Status**: New
**User Story**: US-001
**Found in**: docs/testing/US-001/test-run-01.md, TC-001-01
**Date**: 2026-10-03
**Severity**: Low
**Machine**: Windows Developer Machine
**Routed to**: DEV (pending Michael's go: not part of the "language findings" he asked to fix)

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

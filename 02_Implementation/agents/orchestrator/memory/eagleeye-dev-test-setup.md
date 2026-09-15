---
name: eagleeye-dev-test-setup
description: EagleEye development and testing environment, and the primary test pair
metadata:
  type: project
---

Development happens on Michael's MacBook (macOS 26+). The Windows service and tray client are tested in a Windows 11 x64 VM under VMware Fusion; the macOS parent app runs on the host and connects to the service in the VM over the LAN. **That pair — Windows 11 VM + macOS parent app — is the primary setup for all feature and quality testing.**

Consequence: nothing Windows-specific can be built or verified on the Mac directly, and E2E tests must account for the host↔VM network hop (hostname resolution, self-signed cert trust). Automation is PowerShell 7.6 only (`02_Implementation/scripts/build.ps1`, `test.ps1`, `package-windows.ps1`, `package-macos.ps1`); there is no CI/CD pipeline and none is planned for now. PlantUML renders via a local Docker server on `http://localhost:8080`.

#Requires -Version 7.6
<#
.SYNOPSIS
    Builds and packages the EagleEye macOS parent app as a .dmg.
.DESCRIPTION
    1. Builds EagleEye.ParentApp for net10.0-maccatalyst in Release mode
    2. Creates a .dmg using hdiutil
    3. Output: 03_Delivery/macos/EagleEye-x.x.x.dmg
    The .app bundle is only ad-hoc signed by the .NET Mac Catalyst build. There is no Developer-ID
    signature and no notarisation, so no Apple account and no secrets are needed (ADR-019, CR-001 Q-7).
    On the first start, allow the app once: System Settings -> Privacy & Security -> Open Anyway.
    Requires: Xcode (Mac Catalyst toolchain), .NET 10 SDK with the MAUI workload. Runs on the MacBook
    only (ADR-007). Build from a repo outside iCloud Drive (dev-environments.md §5).
.EXAMPLE
    pwsh scripts/package-macos.ps1
#>

$ErrorActionPreference = "Stop"

if (-not $IsMacOS) {
    Write-Error "package-macos.ps1 runs on the MacBook only (see docs/dev-process/dev-environments.md)."
    exit 1
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " EagleEye macOS Package" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# TODO: Implement with the macOS parent app story (US-007 "macOS parent app parity", CR-001 Q-6).
# Steps:
# 1. dotnet publish EagleEye.ParentApp -f net10.0-maccatalyst -c Release (ad-hoc signed by the build)
# 2. Create the .dmg with hdiutil
# 3. Output: 03_Delivery/macos/EagleEye-x.x.x.dmg
# No Developer-ID signing, no notarisation, no Apple credentials (ADR-019).

Write-Host "TODO: macOS packaging not yet implemented." -ForegroundColor Yellow
Write-Host "This script will be completed by the DEV agent during macOS app implementation." -ForegroundColor Yellow
Write-Host ""

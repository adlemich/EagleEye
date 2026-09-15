#Requires -Version 7.6
<#
.SYNOPSIS
    Builds and packages the EagleEye macOS parent app as a .dmg.
.DESCRIPTION
    1. Builds EagleEye.ParentApp for net10.0-maccatalyst in Release mode
    2. Creates a .dmg using hdiutil
    3. Output: 03_Delivery/macos/EagleEye-x.x.x.dmg
    Requires: Xcode command line tools, .NET 10 SDK with MAUI workload.
.PARAMETER SecretsFile
    Path to secrets.json. Defaults to <repo-root>/secrets/secrets.json.
.EXAMPLE
    pwsh scripts/package-macos.ps1
#>

param(
    [string]$SecretsFile = ""
)

$ErrorActionPreference = "Stop"

$scriptDir  = $PSScriptRoot
$implDir    = $scriptDir
$repoRoot   = Split-Path $implDir -Parent

# Resolve secrets file
if ([string]::IsNullOrEmpty($SecretsFile)) {
    $SecretsFile = Join-Path $repoRoot "secrets/secrets.json"
}

if (-not (Test-Path $SecretsFile)) {
    Write-Error "Secrets file not found: $SecretsFile`nCopy secrets/secrets.template.json to secrets/secrets.json and fill in your values."
    exit 1
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " EagleEye macOS Package" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# TODO: Implement when DEV has completed the macOS parent app
# Steps:
# 1. dotnet publish EagleEye.ParentApp -f net10.0-maccatalyst -c Release
# 2. Code-sign the .app bundle (using Apple credentials from secrets.json)
# 3. Create .dmg with hdiutil
# 4. Notarize (optional, for later)
# 5. Output: 03_Delivery/macos/EagleEye-x.x.x.dmg

Write-Host "TODO: macOS packaging not yet implemented." -ForegroundColor Yellow
Write-Host "This script will be completed by the DEV agent during macOS app implementation." -ForegroundColor Yellow
Write-Host ""

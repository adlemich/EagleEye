#Requires -Version 7.6
<#
.SYNOPSIS
    Builds and packages the EagleEye Windows installer.
.DESCRIPTION
    1. Builds EagleEye.Service and EagleEye.TrayClient in Release mode
    2. Publishes self-contained win-x64 executables
    3. Runs Inno Setup to produce the installer in 03_Delivery/windows/
    NOTE: Inno Setup must be installed in the Windows VM. This script must
    be run inside the Windows 11 VM or via a remote PowerShell session.
.PARAMETER SecretsFile
    Path to secrets.json. Defaults to <repo-root>/secrets/secrets.json.
.EXAMPLE
    pwsh scripts/package-windows.ps1
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
    $SecretsFile = Join-Path $repoRoot "secrets\secrets.json"
}

if (-not (Test-Path $SecretsFile)) {
    Write-Error "Secrets file not found: $SecretsFile`nCopy secrets\secrets.template.json to secrets\secrets.json and fill in your values."
    exit 1
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " EagleEye Windows Package" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# TODO: Implement when DEV has completed the first Windows components
# Steps:
# 1. dotnet publish EagleEye.Service (win-x64, self-contained)
# 2. dotnet publish EagleEye.TrayClient (win-x64, self-contained)
# 3. Run Inno Setup compiler (iscc.exe) with installer/windows/setup.iss
# 4. Output: 03_Delivery/windows/EagleEye-Setup-x.x.x.exe

Write-Host "TODO: Windows packaging not yet implemented." -ForegroundColor Yellow
Write-Host "This script will be completed by the DEV agent during Windows installer implementation." -ForegroundColor Yellow
Write-Host ""

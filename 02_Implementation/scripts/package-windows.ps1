#Requires -Version 7.6
<#
.SYNOPSIS
    Builds and packages the EagleEye Windows installer.
.DESCRIPTION
    1. Builds EagleEye.Service and EagleEye.TrayClient in Release mode
    2. Publishes self-contained win-x64 executables
    3. Runs Inno Setup to produce the installer in 03_Delivery/windows/
    Runs on the Windows Developer Machine only (ADR-007), where Inno Setup is installed.
    No secrets are needed (the installer is not code-signed).
.EXAMPLE
    pwsh scripts/package-windows.ps1
#>

$ErrorActionPreference = "Stop"

if (-not $IsWindows) {
    Write-Error "package-windows.ps1 runs on the Windows Developer Machine only (see docs/dev-process/dev-environments.md)."
    exit 1
}

$implDir  = Split-Path $PSScriptRoot -Parent
$repoRoot = Split-Path $implDir -Parent

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

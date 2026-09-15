#Requires -Version 7.6
<#
.SYNOPSIS
    Builds all EagleEye components.
.DESCRIPTION
    Builds the full EagleEye solution (all src projects) using the specified configuration.
    Run from the repository root or from 02_Implementation/.
.PARAMETER Configuration
    Build configuration: Debug (default) or Release.
.EXAMPLE
    pwsh scripts/build.ps1
    pwsh scripts/build.ps1 -Configuration Release
#>

param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"

$scriptDir  = $PSScriptRoot
$implDir    = $scriptDir   # scripts/ is inside 02_Implementation/
$solutionFile = Join-Path $implDir "EagleEye.sln"

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " EagleEye Build — $Configuration" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

if (-not (Test-Path $solutionFile)) {
    Write-Error "Solution file not found: $solutionFile"
    exit 1
}

dotnet build $solutionFile --configuration $Configuration --nologo

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Error "Build FAILED with exit code $LASTEXITCODE."
    exit $LASTEXITCODE
}

Write-Host ""
Write-Host "Build SUCCEEDED ($Configuration)." -ForegroundColor Green
Write-Host ""

#Requires -Version 7.6
<#
.SYNOPSIS
    Runs all EagleEye unit tests.
.DESCRIPTION
    Executes all unit test projects in the solution. Fails fast if any test fails.
    Run from the repository root or from 02_Implementation/.
.PARAMETER Configuration
    Build configuration: Debug (default) or Release.
.EXAMPLE
    pwsh scripts/test.ps1
    pwsh scripts/test.ps1 -Configuration Release
#>

param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"

$scriptDir    = $PSScriptRoot
$implDir      = $scriptDir
$solutionFile = Join-Path $implDir "EagleEye.sln"

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " EagleEye Tests — $Configuration" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

if (-not (Test-Path $solutionFile)) {
    Write-Error "Solution file not found: $solutionFile"
    exit 1
}

dotnet test $solutionFile `
    --configuration $Configuration `
    --nologo `
    --verbosity normal `
    --logger "console;verbosity=normal"

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Error "One or more tests FAILED (exit code $LASTEXITCODE)."
    exit $LASTEXITCODE
}

Write-Host ""
Write-Host "All tests PASSED." -ForegroundColor Green
Write-Host ""

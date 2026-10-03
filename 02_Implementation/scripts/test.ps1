#Requires -Version 7.6
<#
.SYNOPSIS
    Runs the EagleEye unit tests that belong to the current host machine.
.DESCRIPTION
    Two-machine setup (ADR-007, docs/dev-process/dev-environments.md):
      Windows Developer Machine -> Shared.Tests, Service.Tests, TrayClient.Tests, ParentApp.Tests
      MacBook                   -> Shared.Tests, ParentApp.Tests
    Unit tests are the only automated tests in EagleEye; acceptance testing is manual
    (docs/testing/README.md). Fails on the first failing test project.
    Can be run from any directory.
.PARAMETER Configuration
    Build configuration: Debug (default) or Release.
.EXAMPLE
    pwsh scripts/test.ps1
#>

param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"

$implDir  = Split-Path $PSScriptRoot -Parent
$testsDir = Join-Path $implDir "tests"

if ($IsWindows) {
    $hostName = "Windows Developer Machine"
    $projects = @("EagleEye.Shared.Tests", "EagleEye.Service.Tests", "EagleEye.TrayClient.Tests", "EagleEye.ParentApp.Tests")
}
elseif ($IsMacOS) {
    $hostName = "MacBook"
    $projects = @("EagleEye.Shared.Tests", "EagleEye.ParentApp.Tests")
}
else {
    Write-Error "Unsupported host OS. EagleEye tests run on Windows or macOS only."
    exit 1
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " EagleEye Unit Tests — $Configuration" -ForegroundColor Cyan
Write-Host " Host: $hostName" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

foreach ($name in $projects) {
    $project = Join-Path $testsDir $name "$name.csproj"

    # Test projects without any test source yet are skipped, not failed.
    if (-not (Get-ChildItem (Split-Path $project -Parent) -Recurse -Filter *.cs -File | Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' })) {
        Write-Host ""
        Write-Host "--> $name skipped: no tests yet" -ForegroundColor Yellow
        continue
    }

    Write-Host ""
    Write-Host "--> $name" -ForegroundColor Cyan
    dotnet test $project `
        --configuration $Configuration `
        --nologo `
        --logger "console;verbosity=normal"

    if ($LASTEXITCODE -ne 0) {
        Write-Host ""
        Write-Error "Tests FAILED in $name (exit code $LASTEXITCODE)."
        exit $LASTEXITCODE
    }
}

Write-Host ""
Write-Host "All unit tests PASSED ($hostName)." -ForegroundColor Green
Write-Host ""

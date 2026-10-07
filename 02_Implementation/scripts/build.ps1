#Requires -Version 7.6
<#
.SYNOPSIS
    Builds the EagleEye components that belong to the current host machine.
.DESCRIPTION
    Two-machine setup (ADR-007, docs/dev-process/dev-environments.md):
      Windows Developer Machine -> Shared, Service, TrayClient, ParentApp.Core, ParentApp (Windows + Android)
      MacBook                   -> Shared, ParentApp.Core, ParentApp (Mac Catalyst, only if the MAUI
                                   workload is installed; otherwise skipped with a yellow note)
    Unit test projects for the host are built as part of scripts/test.ps1.
    Can be run from any directory.
.PARAMETER Configuration
    Build configuration: Debug (default) or Release.
.PARAMETER SkipAndroid
    Windows only: skip the ParentApp Android target (e.g. when the Android workload is not installed).
.EXAMPLE
    pwsh scripts/build.ps1
    pwsh scripts/build.ps1 -Configuration Release -SkipAndroid
#>

param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",

    [switch]$SkipAndroid
)

$ErrorActionPreference = "Stop"

$implDir = Split-Path $PSScriptRoot -Parent
$srcDir  = Join-Path $implDir "src"

function Get-ProjectPath([string]$name) {
    Join-Path $srcDir $name "$name.csproj"
}

# Android toolchain (JDK, Android SDK): JAVA_HOME / ANDROID_HOME if set, otherwise the default
# locations under %LOCALAPPDATA%\Android used by the VS Code .NET MAUI extension. The Android
# build does not find these locations on its own.
function Get-AndroidToolchainArgs {
    $toolchainArgs = @()
    $androidRoot = Join-Path $env:LOCALAPPDATA "Android"

    $jdk = $env:JAVA_HOME
    if (-not $jdk) {
        $java = Get-ChildItem (Join-Path $androidRoot "jdk") -Recurse -Filter "java.exe" -File -ErrorAction SilentlyContinue |
            Select-Object -First 1
        if ($java) { $jdk = Split-Path (Split-Path $java.FullName -Parent) -Parent }
    }
    if ($jdk) { $toolchainArgs += "-p:JavaSdkDirectory=$jdk" }

    $sdk = $env:ANDROID_HOME
    if (-not $sdk -and (Test-Path (Join-Path $androidRoot "Sdk"))) { $sdk = Join-Path $androidRoot "Sdk" }
    if ($sdk) { $toolchainArgs += "-p:AndroidSdkDirectory=$sdk" }

    return $toolchainArgs
}

# The Mac Catalyst head needs the MAUI workload; Shared and ParentApp.Core do not (ADR-009).
function Test-MacCatalystWorkload {
    $workloads = dotnet workload list 2>$null | Out-String
    return $workloads -match "(?m)^\s*maui(-maccatalyst)?\s"
}

# Each build step: project + optional target framework
$steps = @()
if ($IsWindows) {
    $hostName = "Windows Developer Machine"
    $steps += @{ Project = Get-ProjectPath "EagleEye.Shared" }
    $steps += @{ Project = Get-ProjectPath "EagleEye.Service" }
    $steps += @{ Project = Get-ProjectPath "EagleEye.TrayClient" }
    $steps += @{ Project = Get-ProjectPath "EagleEye.ParentApp.Core" }
    $steps += @{ Project = Get-ProjectPath "EagleEye.ParentApp"; Framework = "net10.0-windows10.0.19041.0" }
    if (-not $SkipAndroid) {
        $steps += @{ Project = Get-ProjectPath "EagleEye.ParentApp"; Framework = "net10.0-android"; ExtraArgs = Get-AndroidToolchainArgs }
    }
}
elseif ($IsMacOS) {
    $hostName = "MacBook"
    $steps += @{ Project = Get-ProjectPath "EagleEye.Shared" }
    $steps += @{ Project = Get-ProjectPath "EagleEye.ParentApp.Core" }
    if (Test-MacCatalystWorkload) {
        $steps += @{ Project = Get-ProjectPath "EagleEye.ParentApp"; Framework = "net10.0-maccatalyst" }
    }
    else {
        $skippedMessage = "EagleEye.ParentApp [net10.0-maccatalyst] skipped: MAUI workload missing"
    }
}
else {
    Write-Error "Unsupported host OS. EagleEye builds on Windows or macOS only."
    exit 1
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " EagleEye Build — $Configuration" -ForegroundColor Cyan
Write-Host " Host: $hostName" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

if ($skippedMessage) {
    Write-Host ""
    Write-Host "--> $skippedMessage" -ForegroundColor Yellow
}

foreach ($step in $steps) {
    $label = Split-Path $step.Project -LeafBase
    if ($step.Framework) { $label += " [$($step.Framework)]" }

    # Projects that are still empty stubs (no source yet) are skipped, not failed.
    $projectDir = Split-Path $step.Project -Parent
    if (-not (Get-ChildItem $projectDir -Recurse -Filter *.cs -File | Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' })) {
        Write-Host ""
        Write-Host "--> $label skipped: no source files yet" -ForegroundColor Yellow
        continue
    }

    $buildArgs  = @($step.Project, "--configuration", $Configuration, "--nologo")
    if ($step.Framework) {
        $buildArgs  += @("--framework", $step.Framework)
    }
    if ($step.ExtraArgs) {
        $buildArgs += $step.ExtraArgs
    }

    Write-Host ""
    Write-Host "--> $label" -ForegroundColor Cyan
    dotnet build @buildArgs

    if ($LASTEXITCODE -ne 0) {
        Write-Host ""
        Write-Error "Build FAILED for $label (exit code $LASTEXITCODE)."
        exit $LASTEXITCODE
    }
}

Write-Host ""
Write-Host "Build SUCCEEDED ($Configuration, $hostName)." -ForegroundColor Green
Write-Host ""

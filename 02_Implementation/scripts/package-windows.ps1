#Requires -Version 7.6
<#
.SYNOPSIS
    Builds and packages the EagleEye Windows installer (service + tray client).
.DESCRIPTION
    1. Publishes EagleEye.Service and EagleEye.TrayClient (Release, win-x64, self-contained)
       to 02_Implementation/artifacts/publish/ (git-ignored)
    2. Runs Inno Setup (installer/windows/setup.iss) with the product version from
       Directory.Build.props
    3. Output: 03_Delivery/windows/EagleEye-Setup-<version>.exe
    Runs on the Windows Developer Machine only (ADR-007), where Inno Setup 6 is installed.
    No secrets are needed (the installer is not code-signed).
.EXAMPLE
    pwsh scripts/package-windows.ps1
#>

$ErrorActionPreference = "Stop"

if (-not $IsWindows) {
    Write-Error "package-windows.ps1 runs on the Windows Developer Machine only (see docs/dev-process/dev-environments.md)."
    exit 1
}

$implDir     = Split-Path $PSScriptRoot -Parent
$repoRoot    = Split-Path $implDir -Parent
$srcDir      = Join-Path $implDir "src"
$publishDir  = Join-Path $implDir "artifacts" "publish"
$setupScript = Join-Path $implDir "installer" "windows" "setup.iss"
$deliveryDir = Join-Path $repoRoot "03_Delivery" "windows"

$components = [ordered]@{
    Service    = Join-Path $srcDir "EagleEye.Service" "EagleEye.Service.csproj"
    TrayClient = Join-Path $srcDir "EagleEye.TrayClient" "EagleEye.TrayClient.csproj"
}

$iscc = @(
    (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6" "ISCC.exe"),
    (Join-Path $env:ProgramFiles "Inno Setup 6" "ISCC.exe"),
    (Join-Path $env:LOCALAPPDATA "Programs" "Inno Setup 6" "ISCC.exe")
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $iscc) {
    Write-Error "Inno Setup 6 (ISCC.exe) not found. Install Inno Setup 6 on this machine."
    exit 1
}

$version = (dotnet msbuild $components.Service -getProperty:Version).Trim()
if ($LASTEXITCODE -ne 0 -or -not $version) {
    Write-Error "Could not read the product version from Directory.Build.props."
    exit 1
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " EagleEye Windows Package — $version" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

if (Test-Path $publishDir) {
    Remove-Item $publishDir -Recurse -Force
}

foreach ($name in $components.Keys) {
    Write-Host ""
    Write-Host "--> Publishing $name" -ForegroundColor Cyan
    dotnet publish $components[$name] `
        --configuration Release `
        --runtime win-x64 `
        --self-contained true `
        --output (Join-Path $publishDir $name) `
        --nologo

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Publish FAILED for $name (exit code $LASTEXITCODE)."
        exit $LASTEXITCODE
    }
}

Write-Host ""
Write-Host "--> Compiling installer" -ForegroundColor Cyan
& $iscc /Q "/DAppVersion=$version" "/DPublishDir=$publishDir" $setupScript

if ($LASTEXITCODE -ne 0) {
    Write-Error "Inno Setup FAILED (exit code $LASTEXITCODE)."
    exit $LASTEXITCODE
}

$installer = Join-Path $deliveryDir "EagleEye-Setup-$version.exe"
Write-Host ""
Write-Host "Installer created: $installer" -ForegroundColor Green
Write-Host ""

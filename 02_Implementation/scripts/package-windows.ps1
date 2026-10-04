#Requires -Version 7.6
<#
.SYNOPSIS
    Builds and packages the EagleEye Windows installers.
.DESCRIPTION
    Service installer (service + tray client, admin):
      1. Publishes EagleEye.Service and EagleEye.TrayClient (Release, win-x64, self-contained)
      2. Runs Inno Setup with installer/windows/setup.iss
      3. Output: 03_Delivery/windows/EagleEye-Setup-<version>.exe
    Parent app installer (per user, no admin rights, ADR-009):
      1. Publishes EagleEye.ParentApp for Windows (Release, win-x64, unpackaged, self-contained)
      2. Runs Inno Setup with installer/windows/parentapp-setup.iss
      3. Output: 03_Delivery/windows/EagleEye-ParentApp-Setup-<version>.exe
    Publish output goes to 02_Implementation/artifacts/publish/ (git-ignored). The version comes
    from Directory.Build.props. Runs on the Windows Developer Machine only (ADR-007), where
    Inno Setup 6 is installed. No secrets are needed (the installers are not code-signed).
.PARAMETER Target
    All (default), Service or ParentApp.
.EXAMPLE
    pwsh scripts/package-windows.ps1
    pwsh scripts/package-windows.ps1 -Target ParentApp
#>

param(
    [ValidateSet("All", "Service", "ParentApp")]
    [string]$Target = "All"
)

$ErrorActionPreference = "Stop"

if (-not $IsWindows) {
    Write-Error "package-windows.ps1 runs on the Windows Developer Machine only (see docs/dev-process/dev-environments.md)."
    exit 1
}

$implDir      = Split-Path $PSScriptRoot -Parent
$repoRoot     = Split-Path $implDir -Parent
$srcDir       = Join-Path $implDir "src"
$publishDir   = Join-Path $implDir "artifacts" "publish"
$installerDir = Join-Path $implDir "installer" "windows"
$deliveryDir  = Join-Path $repoRoot "03_Delivery" "windows"

$serviceProjects = [ordered]@{
    Service    = Join-Path $srcDir "EagleEye.Service" "EagleEye.Service.csproj"
    TrayClient = Join-Path $srcDir "EagleEye.TrayClient" "EagleEye.TrayClient.csproj"
}
$parentAppProject   = Join-Path $srcDir "EagleEye.ParentApp" "EagleEye.ParentApp.csproj"
$parentAppFramework = "net10.0-windows10.0.19041.0"

$iscc = @(
    (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6" "ISCC.exe"),
    (Join-Path $env:ProgramFiles "Inno Setup 6" "ISCC.exe"),
    (Join-Path $env:LOCALAPPDATA "Programs" "Inno Setup 6" "ISCC.exe")
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $iscc) {
    Write-Error "Inno Setup 6 (ISCC.exe) not found. Install Inno Setup 6 on this machine."
    exit 1
}

$version = (dotnet msbuild $serviceProjects.Service -getProperty:Version).Trim()
if ($LASTEXITCODE -ne 0 -or -not $version) {
    Write-Error "Could not read the product version from Directory.Build.props."
    exit 1
}

function Invoke-Publish([string]$name, [string]$project, [string[]]$publishArgs) {
    $output = Join-Path $publishDir $name
    if (Test-Path $output) {
        Remove-Item $output -Recurse -Force
    }

    Write-Host ""
    Write-Host "--> Publishing $name" -ForegroundColor Cyan
    dotnet publish $project --configuration Release --output $output --nologo @publishArgs
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Publish FAILED for $name (exit code $LASTEXITCODE)."
        exit $LASTEXITCODE
    }
}

function Invoke-Iscc([string]$script, [string]$installerName) {
    Write-Host ""
    Write-Host "--> Compiling $installerName" -ForegroundColor Cyan
    & $iscc /Q "/DAppVersion=$version" "/DPublishDir=$publishDir" (Join-Path $installerDir $script)
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Inno Setup FAILED for $script (exit code $LASTEXITCODE)."
        exit $LASTEXITCODE
    }

    Write-Host "Installer created: $(Join-Path $deliveryDir $installerName)" -ForegroundColor Green
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " EagleEye Windows Package — $version ($Target)" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

if ($Target -in "All", "Service") {
    foreach ($name in $serviceProjects.Keys) {
        Invoke-Publish $name $serviceProjects[$name] @("--runtime", "win-x64", "--self-contained", "true")
    }

    Invoke-Iscc "setup.iss" "EagleEye-Setup-$version.exe"
}

if ($Target -in "All", "ParentApp") {
    # No --runtime here: on a multi-targeted project it would also apply to the Android target
    # during restore. The csproj sets win-x64 and self-contained for the Windows target.
    Invoke-Publish "ParentApp" $parentAppProject @(
        "--framework", $parentAppFramework,
        "-p:WindowsPackageType=None",
        "-p:WindowsAppSDKSelfContained=true")

    Invoke-Iscc "parentapp-setup.iss" "EagleEye-ParentApp-Setup-$version.exe"
}

Write-Host ""

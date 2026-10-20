#Requires -Version 7.6
<#
.SYNOPSIS
    Builds and packages the EagleEye macOS parent app as a .dmg (US-007, plan Decision 9 and Step 1 review R-7).
.DESCRIPTION
    1. Reads the product version from Directory.Build.props.
    2. Warns if the repo is synced by iCloud Drive (codesign rejects Finder metadata, dev-environments.md §5).
    3. Publishes EagleEye.ParentApp for net10.0-maccatalyst, Release, arm64, from a clean intermediate folder
       (a stale obj/AppManifest.plist would hide Info.plist changes).
    4. Copies the bundle to a staging folder as "EagleEye Parent App.app", ad-hoc signs the FINAL bundle
       (codesign --force --deep --sign -, no entitlements) and requires codesign --verify --deep --strict,
       "Signature=adhoc" and sealed resources.
    5. Checks the bundle: version, display name, de/en localizations, en satellite assemblies, no sandbox.
    6. Staging: the app, an "Applications" link, "Remove EagleEye Parent App.command" and
       "Anleitung - Instructions.txt" (version filled in).
    7. hdiutil -> 03_Delivery/macos/EagleEye-ParentApp-<version>.dmg; prints the path and the SHA-256.
    No Developer-ID signature, no notarisation, no Apple account, no secrets (ADR-019, ADR-020).
    Runs on the MacBook only (ADR-007). Publish and staging output: 02_Implementation/artifacts/ (git-ignored).
.EXAMPLE
    pwsh scripts/package-macos.ps1
#>

$ErrorActionPreference = "Stop"

if (-not $IsMacOS) {
    Write-Error "package-macos.ps1 runs on the MacBook only (see docs/dev-process/dev-environments.md)."
    exit 1
}

$implDir      = Split-Path $PSScriptRoot -Parent
$repoRoot     = Split-Path $implDir -Parent
$projectDir   = Join-Path $implDir "src" "EagleEye.ParentApp"
$project      = Join-Path $projectDir "EagleEye.ParentApp.csproj"
$framework    = "net10.0-maccatalyst"
$rid          = "maccatalyst-arm64"
$bundleName   = "EagleEye Parent App.app"
$installerDir = Join-Path $implDir "installer" "macos"
$stagingDir   = Join-Path $implDir "artifacts" "publish" "macos" "staging"
$deliveryDir  = Join-Path $repoRoot "03_Delivery" "macos"
$bundleId     = "com.adlemich.eagleeye"

function Write-Step([string]$text) { Write-Host "`n--> $text" -ForegroundColor Cyan }

function Assert-Check([bool]$condition, [string]$message) {
    if (-not $condition) {
        Write-Error "Check failed: $message"
        exit 1
    }
    Write-Host "    OK  $message" -ForegroundColor Green
}

function Invoke-Native([string]$file, [string[]]$arguments) {
    $output = & $file @arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        $output | ForEach-Object { Write-Host "    $_" }
        Write-Error "$file failed (exit code $LASTEXITCODE)."
        exit 1
    }
    return $output
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " EagleEye macOS Package" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# 1. Version
[xml]$props = Get-Content (Join-Path $implDir "Directory.Build.props")
$version = ($props.Project.PropertyGroup | ForEach-Object { $_.VersionPrefix } | Where-Object { $_ } | Select-Object -First 1)
Assert-Check ([bool]$version) "product version $version from Directory.Build.props"

# 2. iCloud check (warning only)
$repoReal = (Resolve-Path $repoRoot).Path
if ($repoReal -match "Mobile Documents|/Documents/|/Desktop/") {
    Write-Warning "The repo is in a folder that iCloud Drive may sync ($repoReal). codesign can fail there (dev-environments.md §5)."
}

# 3. Publish (clean intermediate folder for this target)
Write-Step "Publishing $framework Release ($rid)"
$objDir = Join-Path $projectDir "obj" "Release" $framework
$binDir = Join-Path $projectDir "bin" "Release" $framework
foreach ($dir in @($objDir, $binDir)) {
    if (Test-Path $dir) { Remove-Item $dir -Recurse -Force }
}
Invoke-Native "dotnet" @("publish", $project, "-f", $framework, "-c", "Release", "-p:CreatePackage=false", "-nologo", "-v:q") | Out-Null
$publishedApp = Join-Path $binDir $rid $bundleName
Assert-Check (Test-Path $publishedApp) "published bundle $publishedApp"

# 4. Staging, final signature
Write-Step "Staging and signing"
if (Test-Path $stagingDir) { Remove-Item $stagingDir -Recurse -Force }
New-Item -ItemType Directory -Path $stagingDir | Out-Null
$app = Join-Path $stagingDir $bundleName
Invoke-Native "ditto" @($publishedApp, $app) | Out-Null
Invoke-Native "xattr" @("-cr", $app) | Out-Null
Invoke-Native "codesign" @("--force", "--deep", "--sign", "-", $app) | Out-Null
Invoke-Native "codesign" @("--verify", "--deep", "--strict", $app) | Out-Null
Assert-Check $true "codesign --verify --deep --strict"
$signature = (Invoke-Native "codesign" @("-dv", $app)) -join "`n"
Assert-Check ($signature -match "Signature=adhoc") "ad-hoc signature"
Assert-Check ($signature -match "Sealed Resources version=2") "sealed resources"
$entitlements = (Invoke-Native "codesign" @("-d", "--entitlements", "-", $app)) -join "`n"
Assert-Check ($entitlements -notmatch "app-sandbox") "no App Sandbox entitlement"

# 5. Bundle checks
Write-Step "Checking the bundle"
$plist = Join-Path $app "Contents" "Info.plist"
function Read-Plist([string]$key) { (& plutil -extract $key raw -o - $plist 2>$null) }
Assert-Check ((Read-Plist "CFBundleShortVersionString") -eq $version) "CFBundleShortVersionString = $version"
Assert-Check ((Read-Plist "CFBundleDisplayName") -eq "EagleEye Parent App") "CFBundleDisplayName = EagleEye Parent App"
Assert-Check ((Read-Plist "CFBundleIdentifier") -eq $bundleId) "CFBundleIdentifier = $bundleId"
Assert-Check ((Read-Plist "CFBundleDevelopmentRegion") -eq "de") "CFBundleDevelopmentRegion = de"
Assert-Check ([bool](Read-Plist "NSLocalNetworkUsageDescription")) "NSLocalNetworkUsageDescription present"
foreach ($lang in @("de", "en")) {
    Assert-Check (Test-Path (Join-Path $app "Contents" "Resources" "$lang.lproj" "InfoPlist.strings")) "$lang.lproj/InfoPlist.strings"
}
foreach ($satellite in @("EagleEye.ParentApp.Core.resources.dll", "EagleEye.Shared.resources.dll")) {
    $found = Get-ChildItem -Path (Join-Path $app "Contents") -Recurse -Filter $satellite | Where-Object { $_.Directory.Name -eq "en" }
    Assert-Check ([bool]$found) "en/$satellite"
}
$arch = (Invoke-Native "lipo" @("-archs", (Join-Path $app "Contents" "MacOS" "EagleEye.ParentApp"))) -join " "
Assert-Check ($arch.Trim() -eq "arm64") "arm64 only"

# 6. Staging contents
$removeScript = Join-Path $stagingDir "Remove EagleEye Parent App.command"
Copy-Item (Join-Path $installerDir "remove-eagleeye-parent-app.command") $removeScript
Invoke-Native "chmod" @("+x", $removeScript) | Out-Null
(Get-Content (Join-Path $installerDir "instructions.txt") -Raw).Replace("{VERSION}", $version) |
    Set-Content -Path (Join-Path $stagingDir "Anleitung - Instructions.txt") -NoNewline
New-Item -ItemType SymbolicLink -Path (Join-Path $stagingDir "Applications") -Target "/Applications" | Out-Null

# 7. Disk image
Write-Step "Creating the disk image"
New-Item -ItemType Directory -Path $deliveryDir -Force | Out-Null
$dmg = Join-Path $deliveryDir "EagleEye-ParentApp-$version.dmg"
Invoke-Native "hdiutil" @("create", "-volname", "EagleEye Parent App $version", "-srcfolder", $stagingDir, "-format", "UDZO", "-ov", $dmg) | Out-Null
$hash = (Get-FileHash -Algorithm SHA256 $dmg).Hash.ToLowerInvariant()

Write-Host ""
Write-Host "Package created:" -ForegroundColor Green
Write-Host "  $dmg"
Write-Host "  SHA-256: $hash"
Write-Host ""

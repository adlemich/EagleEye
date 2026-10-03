#Requires -Version 7.6
<#
.SYNOPSIS
    Shows which EagleEye machine this is, the git sync state, and the available toolchain.
.DESCRIPTION
    Run at the start of every development session (humans and agents). Read-only: it never
    changes files, never fetches from the network, and never prints secret values.
    See docs/dev-process/dev-environments.md.
.EXAMPLE
    pwsh 02_Implementation/scripts/env-check.ps1
#>

$ErrorActionPreference = "Continue"

$implDir  = Split-Path $PSScriptRoot -Parent
$repoRoot = (git -C $implDir rev-parse --show-toplevel 2>$null)

function Write-Item([string]$label, [string]$value, [string]$color = "Gray") {
    Write-Host ("  {0,-22} {1}" -f $label, $value) -ForegroundColor $color
}

function Test-Tool([string]$label, [string]$command, [scriptblock]$version) {
    if (Get-Command $command -ErrorAction SilentlyContinue) {
        $v = try { (& $version | Select-Object -First 1) } catch { "found" }
        Write-Item $label "$v" "Green"
    }
    else {
        Write-Item $label "not found" "Yellow"
    }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " EagleEye Environment Check" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# --- Host -------------------------------------------------------------------
Write-Host ""
Write-Host "Host" -ForegroundColor Cyan
if ($IsWindows) {
    Write-Item "Machine role" "Windows Developer Machine" "Green"
    Write-Item "Responsible for" "Shared, Service, TrayClient, ParentApp (Windows + Android), installer, manual testing"
}
elseif ($IsMacOS) {
    Write-Item "Machine role" "MacBook" "Green"
    Write-Item "Responsible for" "ParentApp (Mac Catalyst), .dmg packaging"
}
else {
    Write-Item "Machine role" "UNKNOWN - not a supported EagleEye dev host" "Red"
}
Write-Item "OS" ([System.Runtime.InteropServices.RuntimeInformation]::OSDescription)

# --- Repository ---------------------------------------------------------------
Write-Host ""
Write-Host "Repository" -ForegroundColor Cyan
if ($repoRoot) {
    Write-Item "Repo root" $repoRoot
    Write-Item "Branch" (git -C $repoRoot branch --show-current)
    $dirty = (git -C $repoRoot status --porcelain | Measure-Object).Count
    Write-Item "Uncommitted changes" "$dirty file(s)" ($(if ($dirty -gt 0) { "Yellow" } else { "Green" }))
    $counts = (git -C $repoRoot rev-list --left-right --count "HEAD...origin/main" 2>$null)
    if ($counts) {
        $ahead, $behind = $counts -split "\s+"
        Write-Item "vs origin/main" "ahead $ahead, behind $behind (as of last fetch)" ($(if ($behind -ne "0") { "Yellow" } else { "Green" }))
    }
    $secrets = Join-Path $repoRoot "secrets" "secrets.json"
    Write-Item "secrets/secrets.json" ($(if (Test-Path $secrets) { "present" } else { "missing (copy from secrets.template.json when needed)" })) ($(if (Test-Path $secrets) { "Green" } else { "Yellow" }))
}
else {
    Write-Item "Repo root" "not inside a git repository" "Red"
}

# --- Toolchain ----------------------------------------------------------------
Write-Host ""
Write-Host "Toolchain" -ForegroundColor Cyan
Write-Item "PowerShell" $PSVersionTable.PSVersion.ToString() "Green"
Test-Tool ".NET SDK" "dotnet" { dotnet --version }
if (Get-Command dotnet -ErrorAction SilentlyContinue) {
    $workloads = dotnet workload list 2>$null | Select-String -Pattern "^\s*(maui\S*|android|maccatalyst|ios)\s" | ForEach-Object { ($_.Line.Trim() -split "\s+")[0] }
    Write-Item ".NET workloads" ($(if ($workloads) { ($workloads -join ", ") } else { "none relevant installed" })) ($(if ($workloads) { "Green" } else { "Yellow" }))
}
Test-Tool "git" "git" { git --version }

if ($IsWindows) {
    $iscc = @(
        (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"),
        (Join-Path $env:ProgramFiles "Inno Setup 6\ISCC.exe")
    ) | Where-Object { Test-Path $_ } | Select-Object -First 1
    Write-Item "Inno Setup (ISCC)" ($(if ($iscc) { $iscc } else { "not found" })) ($(if ($iscc) { "Green" } else { "Yellow" }))
    Test-Tool "adb (Android)" "adb" { (adb version)[0] }
}
if ($IsMacOS) {
    Test-Tool "Xcode" "xcodebuild" { (xcodebuild -version)[0] }
}
Test-Tool "Docker" "docker" { docker --version }
Test-Tool "Podman" "podman" { podman --version }

Write-Host ""

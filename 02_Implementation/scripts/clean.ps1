#Requires -Version 7.6
<#
.SYNOPSIS
    Removes binaries and other build artifacts from the EagleEye repository.
.DESCRIPTION
    Deletes every git-ignored file and folder (git clean -X): bin/, obj/,
    02_Implementation/artifacts/, installer output, .app bundles, logs, .DS_Store.
    Tracked files and untracked-but-not-ignored files are never touched.

    Always kept, although git-ignored:
      secrets/            machine-local credentials (CLAUDE.md, one copy per machine)
      **/.claude/**       machine-local Claude Code settings of the agents
      03_Delivery/        built installers (.exe, .dmg); removed only with -IncludeDelivery

    Works on both machines (ADR-007). The NuGet cache (~/.nuget/packages) is not touched;
    the next build.ps1 restores and rebuilds everything.
    Can be run from any directory.
.PARAMETER IncludeDelivery
    Also delete the installers in 03_Delivery/.
.PARAMETER WhatIf
    Show what would be deleted, delete nothing.
.EXAMPLE
    pwsh 02_Implementation/scripts/clean.ps1 -WhatIf
.EXAMPLE
    pwsh 02_Implementation/scripts/clean.ps1 -IncludeDelivery
#>

param(
    [switch]$IncludeDelivery,
    [switch]$WhatIf
)

$ErrorActionPreference = "Stop"

$repoRoot = git -C $PSScriptRoot rev-parse --show-toplevel
if ($LASTEXITCODE -ne 0) {
    Write-Error "Not inside the EagleEye git repository."
    exit 1
}

$pathspec = @(".", ":!secrets", ":!**/.claude/**")
if (-not $IncludeDelivery) {
    $pathspec += ":!03_Delivery"
}

$mode = if ($WhatIf) { "-ndX" } else { "-fdX" }

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " EagleEye Clean$(if ($WhatIf) { ' — preview (nothing is deleted)' })" -ForegroundColor Cyan
Write-Host " Installers in 03_Delivery: $(if ($IncludeDelivery) { 'DELETED' } else { 'kept' })" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

git -C $repoRoot clean $mode -- @pathspec
if ($LASTEXITCODE -ne 0) {
    Write-Error "git clean failed (exit code $LASTEXITCODE)."
    exit $LASTEXITCODE
}

Write-Host ""
Write-Host "Done. Kept: secrets/, agent .claude settings$(if (-not $IncludeDelivery) { ', 03_Delivery/' })." -ForegroundColor Green

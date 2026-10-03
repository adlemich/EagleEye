#Requires -Version 7.6
<#
.SYNOPSIS
    Starts, stops or updates the local PlantUML server (Windows and macOS).
.DESCRIPTION
    Uses Docker if available, otherwise Podman. The server listens on
    http://localhost:8080, matching .vscode/settings.json ("plantuml.server").
.PARAMETER Action
    start (default), stop, or pull.
.PARAMETER Port
    Host port. Default 8080.
.EXAMPLE
    pwsh scripts/plantuml.ps1
    pwsh scripts/plantuml.ps1 -Action stop
#>

param(
    [ValidateSet("start", "stop", "pull")]
    [string]$Action = "start",

    [int]$Port = 8080
)

$ErrorActionPreference = "Stop"

$containerName = "plantuml-server"
$image         = "docker.io/plantuml/plantuml-server:jetty"

$engine = @("docker", "podman") | Where-Object { Get-Command $_ -ErrorAction SilentlyContinue } | Select-Object -First 1
if (-not $engine) {
    Write-Error "Neither docker nor podman found. Install one of them to run the PlantUML server."
    exit 1
}

$exists = (& $engine ps -a --filter "name=^$containerName$" --format "{{.Names}}") -eq $containerName

switch ($Action) {
    "pull" {
        & $engine pull $image
    }
    "start" {
        if ($exists) {
            & $engine start $containerName | Out-Null
        }
        else {
            & $engine run -d --name $containerName -p "${Port}:8080" $image | Out-Null
        }
        Write-Host "PlantUML server running at http://localhost:$Port ($engine)" -ForegroundColor Green
    }
    "stop" {
        if ($exists) {
            & $engine stop $containerName | Out-Null
            Write-Host "PlantUML server stopped." -ForegroundColor Green
        }
        else {
            Write-Host "No container named '$containerName' found."
        }
    }
}

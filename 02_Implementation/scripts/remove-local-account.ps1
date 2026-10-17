#Requires -RunAsAdministrator
# Removes a local Windows account together with its user profile (folder + ProfileList entry).
# Usage (elevated): pwsh 02_Implementation/scripts/remove-local-account.ps1 -Name <account>
param(
    [Parameter(Mandatory)]
    [string]$Name   # account to remove
)

$ErrorActionPreference = 'Stop'

if ($Name -eq $env:USERNAME) { throw "Refusing to remove the account you are logged in with." }

$user = Get-LocalUser -Name $Name -ErrorAction SilentlyContinue
if ($user) {
    $sid = $user.SID.Value
    $profile = Get-CimInstance Win32_UserProfile -Filter "SID='$sid'"
} else {
    # Account already gone: look for a leftover profile folder with that name
    $profile = Get-CimInstance Win32_UserProfile | Where-Object { (Split-Path $_.LocalPath -Leaf) -eq $Name }
    $sid = $profile.SID
}

if (-not $user -and -not $profile) { Write-Host "Nothing found for '$Name'."; return }

Write-Host "Account : $(if ($user) { $user.Name } else { '<already deleted>' })"
Write-Host "SID     : $sid"
Write-Host "Profile : $(if ($profile) { $profile.LocalPath } else { '<none>' })"
if ((Read-Host "Type the account name to confirm removal") -ne $Name) { Write-Host "Aborted."; return }

if ($profile) {
    if ($profile.Loaded) { throw "Profile is loaded. Sign out '$Name' (or reboot) first." }
    $profile | Remove-CimInstance   # deletes folder + ProfileList registry entry
    Write-Host "Profile removed."
}
if ($user) {
    Remove-LocalUser -Name $Name
    Write-Host "Account removed."
}



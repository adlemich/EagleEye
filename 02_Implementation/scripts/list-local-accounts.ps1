
# Lists local Windows accounts and user profiles; profiles of deleted accounts are flagged as orphans.
# Usage (elevated for a complete profile list): pwsh 02_Implementation/scripts/list-local-accounts.ps1

# Local accounts
Get-LocalUser |
    Select-Object Name, Enabled, LastLogon, SID, Description |
    Sort-Object Name | Format-Table -AutoSize

# User profiles on disk (C:\Users\...), orphans flagged
Get-CimInstance Win32_UserProfile | Where-Object { -not $_.Special } | ForEach-Object {
    $account = try { ([System.Security.Principal.SecurityIdentifier]$_.SID).Translate([System.Security.Principal.NTAccount]).Value } catch { '<orphan - account deleted>' }
    [pscustomobject]@{
        Account  = $account
        Path     = $_.LocalPath
        LastUsed = $_.LastUseTime
        Loaded   = $_.Loaded
        SID      = $_.SID
    }
} | Sort-Object Path | Format-Table -AutoSize
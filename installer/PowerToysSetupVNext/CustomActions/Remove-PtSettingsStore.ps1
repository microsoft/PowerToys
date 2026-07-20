<#
.SYNOPSIS
  PTSettingsSvc - uninstall cleanup (Design-v6-Final.md section 11 uninstall/cleanup).

  Runs as SYSTEM from the per-machine MSI (deferred CustomAction, on uninstall).
  Removes the service and recursively deletes the protected data tree.

  This recursive delete is REQUIRED: the per-user <SID>\blob.bin nodes are created
  by the service at runtime and are NOT in the MSI component table, so the MSI's
  default RemoveFolder won't touch them.  A non-elevated per-user uninstall cannot
  do this (the tree is SYSTEM-owned, user has only RX) - only the elevated/SYSTEM
  per-machine uninstall can.

.PARAMETER RemoveService   Stop + delete the PTSettingsSvc service (default: on).
.PARAMETER RemoveData      Recursively delete the SettingsSvc data tree (default: on).
#>
[CmdletBinding()]
param(
    [string]$ServiceName = 'PTSettingsSvc',
    [switch]$RemoveService = $true,
    [switch]$RemoveData    = $true
)

$ErrorActionPreference = 'Continue'

if ($RemoveService)
{
    $svc = Get-Service $ServiceName -ErrorAction SilentlyContinue
    if ($svc)
    {
        if ($svc.Status -ne 'Stopped') { sc.exe stop $ServiceName | Out-Null; Start-Sleep -Milliseconds 800 }
        sc.exe delete $ServiceName | Out-Null
        Write-Output "service '$ServiceName' removed."
    }
    else { Write-Output "service '$ServiceName' not present." }
}

if ($RemoveData)
{
    $root = Join-Path ([Environment]::GetFolderPath('CommonApplicationData')) 'Microsoft\PowerToys\Settings'
    if (Test-Path $root)
    {
        # Recursive delete works because this runs as SYSTEM/admin (the tree is
        # SYSTEM-owned with the user only RX; a non-elevated user could not).
        Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
        if (Test-Path $root) { Write-Output "WARNING: '$root' not fully removed." }
        else                 { Write-Output "data tree '$root' removed." }
    }
    else { Write-Output "data tree not present." }

    # Also remove the service's runnable-exe copy tree (SettingsSvcBin), which is
    # SYSTEM-owned/protected and not MSI-tracked (Design §12.8).
    $binRoot = Join-Path ([Environment]::GetFolderPath('CommonApplicationData')) 'Microsoft\PowerToys\SettingsSvcBin'
    if (Test-Path $binRoot)
    {
        Remove-Item -LiteralPath $binRoot -Recurse -Force -ErrorAction SilentlyContinue
        if (Test-Path $binRoot) { Write-Output "WARNING: '$binRoot' not fully removed." }
        else                    { Write-Output "bin tree '$binRoot' removed." }
    }

    # Remove the per-user virtual-account profiles (C:\Windows\ServiceProfiles\
    # PTSettingsSvc_<SID> + their HKLM ProfileList entries).  Deleting a service
    # does NOT remove its virtual-account profile, so without this they accumulate
    # across install/uninstall cycles (Design §11/§12.8).
    Get-CimInstance Win32_UserProfile -ErrorAction SilentlyContinue |
        Where-Object { $_.LocalPath -match '\\ServiceProfiles\\PTSettingsSvc_' } |
        ForEach-Object {
            try { Remove-CimInstance -InputObject $_ -ErrorAction Stop; Write-Output "removed vacct profile: $($_.LocalPath)" }
            catch { Write-Output "WARNING: could not remove profile $($_.LocalPath): $($_.Exception.Message)" }
        }
}

exit 0

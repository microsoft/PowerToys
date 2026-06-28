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
}

exit 0

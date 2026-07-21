<#
.SYNOPSIS
  PTSettingsSvc - uninstall cleanup (Design-v6-Final.md section 11 uninstall/cleanup).

  Runs as SYSTEM from the per-machine MSI (deferred CustomAction, on uninstall).
  Removes the service and the service's APP artifacts, but PRESERVES the user's
  protected settings DATA.

  Data preservation (parity with mainline PowerToys, which keeps user settings
  under %LocalAppData% on uninstall): the per-user protected store
  %ProgramData%\Microsoft\PowerToys\Settings\<SID>\... is intentionally LEFT in
  place so an uninstall/reinstall round-trip does not lose the user's workspaces.
  It stays fully protected while orphaned (SYSTEM-owned, user RX-only, protected
  DACL — a normal non-admin user still cannot modify it), and on reinstall the
  same deterministic virtual account re-owns it and ProvisionStore re-asserts the
  DACL.  Only APP artifacts are removed here.

.PARAMETER RemoveService        Stop + delete the PTSettingsSvc service (default: on).
.PARAMETER RemoveAppArtifacts   Remove the service's runnable-exe copy tree
                                (SettingsSvcBin) and the per-user virtual-account
                                profiles.  Does NOT touch the settings DATA tree.
#>
[CmdletBinding()]
param(
    [string]$ServiceName        = 'PTSettingsSvc',
    [switch]$RemoveService      = $true,
    [switch]$RemoveAppArtifacts = $true
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

# DATA is intentionally preserved (see .SYNOPSIS).  Report it for diagnostics but
# do NOT delete the Settings tree.
$store = Join-Path ([Environment]::GetFolderPath('CommonApplicationData')) 'Microsoft\PowerToys\Settings'
if (Test-Path $store) { Write-Output "settings DATA preserved (not removed): '$store'." }

if ($RemoveAppArtifacts)
{
    # Remove the service's runnable-exe copy tree (SettingsSvcBin), which is
    # SYSTEM-owned/protected and not MSI-tracked (Design §12.8).  This is an APP
    # artifact (a copy of the signed exe), NOT user data.
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

# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

<#
.SYNOPSIS
Installs Windows updates in a local Hyper-V UI-test guest and handles required reboots.

.DESCRIPTION
Retail Windows 10 22H2 media starts at an unserviced build that .NET 10 rejects with:
"Your Windows doesn't fully support CET. Please install all available Windows updates."
This script drives the in-box Windows Update COM API over PowerShell Direct, reboots as needed, and
repeats until no software updates remain or MaxPasses is reached.

.EXAMPLE
pwsh ./Update-LocalVmGuest.ps1 -VmName PowerToysUiTest-Win10
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$VmName,
    [string]$CredentialPath = (Join-Path $env:LOCALAPPDATA 'PowerToysUiTestVm\admin.credential.xml'),
    [ValidateRange(1, 10)]
    [int]$MaxPasses = 4,
    [ValidateRange(1, 120)]
    [int]$ReconnectTimeoutMinutes = 30
)

$ErrorActionPreference = 'Stop'

if ($PSVersionTable.PSVersion.Major -lt 7) {
    throw 'Run this script with PowerShell 7 (pwsh).'
}
if (-not (Test-Path $CredentialPath -PathType Leaf)) {
    throw "DPAPI credential file was not found: $CredentialPath"
}

Import-Module Hyper-V -ErrorAction Stop
$credential = Import-Clixml $CredentialPath

function New-GuestSession {
    $deadline = [DateTime]::UtcNow.AddMinutes($ReconnectTimeoutMinutes)
    do {
        try {
            return New-PSSession -VMName $VmName -Credential $credential -ErrorAction Stop
        }
        catch {
            if ([DateTime]::UtcNow -ge $deadline) {
                throw "PowerShell Direct did not reconnect to '$VmName' within $ReconnectTimeoutMinutes minute(s): $($_.Exception.Message)"
            }
            Start-Sleep -Seconds 5
        }
    } while ($true)
}

$vm = Get-VM -Name $VmName -ErrorAction Stop
if ($vm.State -ne 'Running') {
    Start-VM -Name $VmName
}

$passes = @()
for ($pass = 1; $pass -le $MaxPasses; $pass++) {
    $result = $null
    for ($serviceAttempt = 1; $serviceAttempt -le 6; $serviceAttempt++) {
        $session = New-GuestSession
        try {
            $result = Invoke-Command -Session $session -ScriptBlock {
                $ErrorActionPreference = 'Stop'
                $updateSession = New-Object -ComObject Microsoft.Update.Session
                $searcher = $updateSession.CreateUpdateSearcher()
                $search = $searcher.Search("IsInstalled=0 and Type='Software' and IsHidden=0")
                $titles = @($search.Updates | ForEach-Object Title)

                if ($search.Updates.Count -eq 0) {
                    $ubr = (Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion').UBR
                    return [pscustomobject]@{
                        Count = 0
                        Titles = @()
                        DownloadResult = $null
                        InstallResult = $null
                        RebootRequired = $false
                        Version = "$([Environment]::OSVersion.Version.Major).$([Environment]::OSVersion.Version.Minor).$([Environment]::OSVersion.Version.Build).$ubr"
                    }
                }

                $updates = New-Object -ComObject Microsoft.Update.UpdateColl
                foreach ($update in $search.Updates) {
                    if (-not $update.EulaAccepted) { $update.AcceptEula() }
                    [void]$updates.Add($update)
                }

                $downloader = $updateSession.CreateUpdateDownloader()
                $downloader.Updates = $updates
                $download = $downloader.Download()

                $installer = $updateSession.CreateUpdateInstaller()
                $installer.Updates = $updates
                $install = $installer.Install()

                $ubr = (Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion').UBR
                [pscustomobject]@{
                    Count = $updates.Count
                    Titles = $titles
                    DownloadResult = [int]$download.ResultCode
                    InstallResult = [int]$install.ResultCode
                    RebootRequired = [bool]$install.RebootRequired
                    Version = "$([Environment]::OSVersion.Version.Major).$([Environment]::OSVersion.Version.Minor).$([Environment]::OSVersion.Version.Build).$ubr"
                }
            }
            break
        }
        catch {
            if ($_.Exception.ToString() -match '0x8024001E' -and $serviceAttempt -lt 6) {
                Write-Warning "Windows Update service is still restarting after reboot (0x8024001E); retrying in 20 seconds ($serviceAttempt/6)."
                Start-Sleep -Seconds 20
                continue
            }
            throw
        }
        finally {
            Remove-PSSession $session -ErrorAction SilentlyContinue
        }
    }

    $passes += $result
    Write-Host "Windows Update pass $pass`: $($result.Count) update(s), download=$($result.DownloadResult), install=$($result.InstallResult), version=$($result.Version)"
    foreach ($title in $result.Titles) { Write-Host "  $title" }

    if ($result.Count -eq 0) {
        break
    }
    if ($result.DownloadResult -notin 2, 3 -or $result.InstallResult -notin 2, 3) {
        throw "Windows Update failed in '$VmName' (download=$($result.DownloadResult), install=$($result.InstallResult))."
    }

    if ($result.RebootRequired) {
        Write-Host "Restarting '$VmName'..."
        $session = New-GuestSession
        try {
            Invoke-Command -Session $session -ScriptBlock { Restart-Computer -Force } -ErrorAction SilentlyContinue
        }
        finally {
            Remove-PSSession $session -ErrorAction SilentlyContinue
        }
        $null = New-GuestSession | ForEach-Object { Remove-PSSession $_ }
    }
}

if ($passes.Count -eq $MaxPasses -and $passes[-1].Count -gt 0) {
    throw "Windows Update still found updates after $MaxPasses passes in '$VmName'. Re-run the script."
}

[pscustomobject]@{
    VmName = $VmName
    Passes = $passes.Count
    FinalVersion = $passes[-1].Version
    PendingUpdates = $passes[-1].Count
} | ConvertTo-Json -Depth 4

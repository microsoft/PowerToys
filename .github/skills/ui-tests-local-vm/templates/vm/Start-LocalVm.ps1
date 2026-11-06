# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

<#
.SYNOPSIS
Starts the persistent Hyper-V UI-test guest and optionally waits for PowerShell Direct.

.EXAMPLE
pwsh ./Start-LocalVm.ps1 -Wait -TimeoutMinutes 20
#>

[CmdletBinding()]
param(
    [string]$ConfigPath = (Join-Path $PSScriptRoot 'vm.config.psd1'),
    [string]$CredentialPath = (Join-Path $env:LOCALAPPDATA 'PowerToysUiTestVm\admin.credential.xml'),
    [switch]$Wait,
    [ValidateRange(1, 720)]
    [int]$TimeoutMinutes = 30,
    [ValidateSet('Default', 'Constrained')]
    [string]$ResourceProfile = 'Default',
    [switch]$PlanOnly
)

$ErrorActionPreference = 'Stop'

if ($PSVersionTable.PSVersion.Major -lt 7) {
    throw 'Run this script with PowerShell 7 (pwsh).'
}
if (-not (Test-Path $ConfigPath -PathType Leaf)) {
    throw "Configuration was not found: $ConfigPath. Copy vm.config.example.psd1 to vm.config.psd1 first."
}
$configuration = Import-PowerShellDataFile $ConfigPath

$memoryGB = if ($ResourceProfile -eq 'Constrained') {
    if ($configuration.ContainsKey('ConstrainedMemoryStartupGB')) { $configuration.ConstrainedMemoryStartupGB } else { 4 }
}
else {
    $configuration.MemoryStartupGB
}
$processorCount = if ($ResourceProfile -eq 'Constrained') {
    if ($configuration.ContainsKey('ConstrainedProcessorCount')) { $configuration.ConstrainedProcessorCount } else { 1 }
}
else {
    $configuration.ProcessorCount
}

if ($PlanOnly) {
    [pscustomobject]@{
        VmName = $configuration.VmName
        ResourceProfile = $ResourceProfile
        MemoryStartupGB = $memoryGB
        ProcessorCount = $processorCount
    } | ConvertTo-Json
    return
}

try {
    Import-Module Hyper-V -ErrorAction Stop
    Get-VMHost -ErrorAction Stop | Out-Null
}
catch {
    throw 'BLOCKED: Hyper-V is not accessible from this shell. Run from an elevated PowerShell 7 terminal, or add this account to the local "Hyper-V Administrators" group.'
}

$vm = Get-VM -Name $configuration.VmName -ErrorAction SilentlyContinue
if ($null -eq $vm) {
    throw "Virtual machine '$($configuration.VmName)' does not exist. Create it with New-UiTestVm.ps1."
}

if ($vm.State -eq 'Off') {
    Set-VMMemory -VMName $vm.Name -StartupBytes ($memoryGB * 1GB)
    Set-VMProcessor -VMName $vm.Name -Count $processorCount
}
elseif ($vm.MemoryStartup -ne ($memoryGB * 1GB) -or $vm.ProcessorCount -ne $processorCount) {
    Write-Warning "The guest is $($vm.State); the $ResourceProfile profile will apply after the next stop."
}

if ($vm.State -ne 'Running') {
    Start-VM -Name $vm.Name
}

if ($Wait) {
    if (-not (Test-Path $CredentialPath -PathType Leaf)) {
        throw "DPAPI credential file was not found: $CredentialPath"
    }
    $credential = Import-Clixml $CredentialPath
    $deadline = [DateTime]::UtcNow.AddMinutes($TimeoutMinutes)
    $session = $null
    do {
        try {
            $session = New-PSSession -VMName $vm.Name -Credential $credential -ErrorAction Stop
            break
        }
        catch {
            if ([DateTime]::UtcNow -ge $deadline) {
                throw "PowerShell Direct did not answer for '$($vm.Name)' within $TimeoutMinutes minute(s). The guest and its disk are preserved; watch the console with vmconnect.exe localhost $($vm.Name)."
            }
            Start-Sleep -Seconds 5
        }
    } while ($true)

    try {
        $standardUser = [string]$configuration.StandardUser
        $desktopDeadline = [DateTime]::UtcNow.AddMinutes(2)
        do {
            $desktopReady = Invoke-Command -Session $session -ScriptBlock {
                param($User)
                @(Get-Process explorer -IncludeUserName -ErrorAction SilentlyContinue |
                    Where-Object { $_.UserName -like "*\$User" }).Count -gt 0
            } -ArgumentList $standardUser
            if ($desktopReady -or [DateTime]::UtcNow -ge $desktopDeadline) { break }
            Start-Sleep -Seconds 5
        } while ($true)

        if (-not $desktopReady) {
            Write-Warning "No interactive Explorer session exists for '$standardUser'; repairing auto-logon and restarting once."
            $autoLogonScript = Join-Path $PSScriptRoot 'oem\Set-UiTestAutoLogon.ps1'
            if (-not (Test-Path $autoLogonScript -PathType Leaf)) {
                $autoLogonScript = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\oem\Set-UiTestAutoLogon.ps1'))
            }
            if (-not (Test-Path $autoLogonScript -PathType Leaf)) {
                throw "Auto-logon repair helper was not found: $autoLogonScript"
            }
            Invoke-Command -Session $session -FilePath $autoLogonScript -ArgumentList $standardUser | Out-Null
            if ($null -ne $session) { Remove-PSSession $session -ErrorAction SilentlyContinue }
            $session = $null
            Restart-VM -Name $vm.Name -Force

            do {
                try {
                    if ($null -eq $session) {
                        $session = New-PSSession -VMName $vm.Name -Credential $credential -ErrorAction Stop
                    }
                    $desktopReady = Invoke-Command -Session $session -ScriptBlock {
                        param($User)
                        @(Get-Process explorer -IncludeUserName -ErrorAction SilentlyContinue |
                            Where-Object { $_.UserName -like "*\$User" }).Count -gt 0
                    } -ArgumentList $standardUser
                    if ($desktopReady) { break }
                }
                catch {
                    if ($null -ne $session) { Remove-PSSession $session -ErrorAction SilentlyContinue }
                    $session = $null
                }

                if ([DateTime]::UtcNow -ge $deadline) {
                    throw "The '$standardUser' interactive desktop did not appear after auto-logon repair."
                }
                Start-Sleep -Seconds 5
            } while ($true)
        }
    }
    finally {
        if ($null -ne $session) { Remove-PSSession $session -ErrorAction SilentlyContinue }
    }
}

$vm = Get-VM -Name $configuration.VmName
[pscustomobject]@{
    VmName = $vm.Name
    State = [string]$vm.State
    ResourceProfile = $ResourceProfile
    MemoryStartupGB = [math]::Round($vm.MemoryStartup / 1GB, 1)
    ProcessorCount = $vm.ProcessorCount
    Uptime = [string]$vm.Uptime
    Checkpoints = @(Get-VMCheckpoint -VMName $vm.Name -ErrorAction SilentlyContinue | ForEach-Object { $_.Name })
    Console = "vmconnect.exe localhost `"$($vm.Name)`""
    ControlChannel = "vmbus://$($vm.Name)"
} | ConvertTo-Json

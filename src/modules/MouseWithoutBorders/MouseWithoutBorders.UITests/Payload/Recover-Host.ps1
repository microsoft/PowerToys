# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

<#
.SYNOPSIS
Recovers only the recorded user-owned resources after an aborted MWB pilot.
.DESCRIPTION
Run as the original interactive standard user, never SYSTEM or administrator.
The privileged controller separately removes its protected-marker firewall rule.
No arbitrary commands, process command lines, or clipboard contents are accepted.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$JournalPath,
    [string]$ProvisioningMarker = 'C:\ProgramData\PowerToysMwbExperiment\host-provisioning.json'
)

$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\EndpointSupport.ps1"
Add-Type -Path "$PSScriptRoot\NativeSupport.cs"
$deadline = [DateTime]::UtcNow.AddMinutes(3)
$failures = [Collections.Generic.List[string]]::new()
$resultPath = ''
$result = [ordered]@{
    FormatVersion = 1; Status = 'Incomplete'; RunId = ''; SettingsRestored = $false
    ClipboardRestored = $false; RequiresBaselineReset = $true; Errors = @()
    TimestampUtc = ''
}

function Assert-RecoveryDeadline {
    if ([DateTime]::UtcNow -ge $deadline) { throw 'The three-minute user recovery deadline expired.' }
}

function Convert-ProcessRecord {
    param($Record)
    if (-not $Record) { throw 'Missing recorded process identity; refusing to guess.' }
    $identity = [Microsoft.MouseWithoutBorders.UITests.ProcessIdentity]::new()
    $identity.Id = [int]$Record.Id
    $identity.ParentId = [int]$Record.ParentId
    $identity.SessionId = [int]$Record.SessionId
    $identity.Path = [IO.Path]::GetFullPath([string]$Record.Path)
    $identity.StartTimeUtc = ([DateTime]$Record.StartTimeUtc).ToUniversalTime()
    $identity
}

function Assert-ChildIdentity {
    param($Child, $Parent)
    if ($Child.ParentId -ne $Parent.Id -or $Child.SessionId -ne $Parent.SessionId -or
        $Child.StartTimeUtc -lt $Parent.StartTimeUtc) {
        throw "Invalid recorded parent chain for PID $($Child.Id)."
    }
}

function Stop-RecordedProcess {
    param($Record)
    Assert-RecoveryDeadline
    try { $Record.Stop() }
    catch { $failures.Add("Owned PID $($Record.Id) could not stop: $($_.Exception.Message)") }
}

function Read-HostEndpoint {
    param([string]$Path)
    $endpoint = Read-RunJson $Path
    if ($endpoint.RunId -ne $journal.RunId -or $endpoint.Role -ne 'Host' -or
        $endpoint.ProductRoot.TrimEnd('\') -ine $productRoot -or
        $endpoint.SettingsRoot -ine $settingsRoot) {
        throw 'Host endpoint journal identity does not match this user and payload.'
    }
    $endpoint
}

function Restore-RecordedSettings {
    param($Endpoint)
    $allowed = @('settings.json', 'oobe_settings.json', 'MouseWithoutBorders\settings.json') |
        ForEach-Object { Join-Path $settingsRoot $_ }
    $seen = @()
    foreach ($item in $Endpoint.Settings) {
        $target = [IO.Path]::GetFullPath([string]$item.Path)
        $backup = [IO.Path]::GetFullPath([string]$item.Backup)
        if ($target -in $seen -or $target -notin $allowed -or
            (Split-Path -Parent $backup) -ine $backupRoot -or
            (Split-Path -Leaf $backup) -notmatch '^[0-2]\.bin$') {
            throw 'Recovery accepts only the three scoped settings files and this run''s private backups.'
        }
        $seen += $target
        foreach ($path in @($target, $backup)) {
            $ancestor = $path
            while ($ancestor) {
                if ((Test-Path -LiteralPath $ancestor) -and
                    ((Get-Item -LiteralPath $ancestor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
                    throw "Refusing a reparse point in a recovery path: $ancestor"
                }
                $ancestor = Split-Path -Parent $ancestor
            }
        }
        if (-not $Endpoint.SettingsRestored -and $item.Existed) {
            if ($item.Sha256 -notmatch '^[0-9A-Fa-f]{64}$' -or
                (Get-FileHash -LiteralPath $backup -Algorithm SHA256).Hash -ine $item.Sha256) {
                throw 'Private settings backup integrity check failed.'
            }
        }
    }
    foreach ($item in $Endpoint.Settings) {
        Assert-RecoveryDeadline
        if (-not $Endpoint.SettingsRestored) {
            if ($item.Existed) {
                $null = New-Item -ItemType Directory -Path (Split-Path -Parent $item.Path) -Force
                [IO.File]::WriteAllBytes($item.Path, [IO.File]::ReadAllBytes($item.Backup))
            }
            elseif (Test-Path -LiteralPath $item.Path) { Remove-Item -LiteralPath $item.Path -Force }
        }
        if ($item.Existed) {
            if ((Get-FileHash -LiteralPath $item.Path -Algorithm SHA256).Hash -ine $item.Sha256) {
                throw 'Restored settings bytes do not match the original snapshot.'
            }
        }
        elseif (Test-Path -LiteralPath $item.Path) { throw 'A settings file originally absent still exists.' }
    }
    $result.SettingsRestored = $true
}

try {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    try {
        $userSid = $identity.User.Value
        if ($identity.IsSystem -or [Security.Principal.WindowsPrincipal]::new($identity).IsInRole(
            [Security.Principal.WindowsBuiltInRole]::Administrator)) {
            throw 'Recovery must run as the original standard user, never elevated or SYSTEM.'
        }
    }
    finally { $identity.Dispose() }
    $journalPathFull = [IO.Path]::GetFullPath($JournalPath)
    $journal = Read-RunJson $journalPathFull
    $provision = Read-RunJson $ProvisioningMarker
    $runId = ([Guid]::Parse($journal.RunId)).ToString()
    $runRoot = Split-Path -Parent $journalPathFull
    if ($journal.FormatVersion -ne 1 -or $journal.RunId -ne $provision.RunId -or
        $journal.TestUserSid -ne $userSid -or $provision.TestUserSid -ne $userSid -or
        $journal.RunRoot -ine $runRoot -or $journal.HostName -ine (Get-MachineName) -or
        $journal.RuleName -ne $provision.RuleName -or
        $journal.ProductRoot.TrimEnd('\') -ine $provision.ProductRoot.TrimEnd('\')) {
        throw 'Recovery ownership does not match this host, user, run, and protected provisioning marker.'
    }
    $result.RunId = $runId
    $resultPath = Join-Path $runRoot 'recovery-result.json'
    $productRoot = [IO.Path]::GetFullPath($provision.ProductRoot).TrimEnd('\')
    $settingsRoot = Join-Path $env:LOCALAPPDATA 'Microsoft\PowerToys'
    $backupRoot = Join-Path $env:LOCALAPPDATA "Microsoft\PowerToysUiTestRecovery\$runId"
    $testProcess = Convert-ProcessRecord $journal.TestProcess
    if ($testProcess.SessionId -ne (Get-Process -Id $PID).SessionId -or $testProcess.IsCurrent()) {
        throw 'Run recovery in the original interactive session only after the recorded MTP process exits.'
    }

    if ($journal.PSObject.Properties['LeasePublisher'] -and $journal.LeasePublisher) {
        $publisher = Convert-ProcessRecord $journal.LeasePublisher
        Assert-ChildIdentity $publisher $testProcess
        $expectedPowerShell = Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
        if ($publisher.Path -ine $expectedPowerShell) { throw 'Unexpected lease publisher executable.' }
        Stop-RecordedProcess $publisher
        if ($publisher.IsCurrent()) { throw 'The recorded lease publisher is still active.' }
    }

    $worker = $null
    if ($journal.HostWorker) {
        $worker = Convert-ProcessRecord $journal.HostWorker
        Assert-ChildIdentity $worker $testProcess
        $expectedPowerShell = Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
        if ($worker.Path -ine $expectedPowerShell) { throw 'Unexpected host worker executable.' }
        # Give the worker's expired lease a chance to stop its own peers and restore settings.
        $leaseDeadline = [DateTime]::UtcNow.AddSeconds(95)
        while ($worker.IsCurrent() -and [DateTime]::UtcNow -lt $leaseDeadline) {
            Assert-RecoveryDeadline
            Start-Sleep -Milliseconds 500
        }
        Stop-RecordedProcess $worker
        if ($worker.IsCurrent()) { throw 'The recorded host worker is still active; settings recovery is not safe.' }
    }

    $endpointPath = Join-Path $runRoot 'host\endpoint-journal.json'
    $endpoint = $null
    if (Test-Path -LiteralPath $endpointPath) {
        if (-not $worker) { throw 'Endpoint records exist without an owned host worker.' }
        $endpoint = Read-HostEndpoint $endpointPath
        $known = @{}
        $known[$worker.Id] = $worker
        $processes = @($endpoint.Processes | ForEach-Object { Convert-ProcessRecord $_ })
        if ($processes.Count -gt 32) { throw 'Unexpected endpoint process count.' }
        foreach ($record in $processes) {
            if (-not $record.Path.StartsWith($productRoot + '\', [StringComparison]::OrdinalIgnoreCase) -or
                -not $known.ContainsKey($record.ParentId)) {
                throw 'Product recovery record is outside the staged payload or lacks its recorded parent.'
            }
            Assert-ChildIdentity $record $known[$record.ParentId]
            $known[$record.Id] = $record
        }
        foreach ($record in $processes) { Stop-RecordedProcess $record }
    }

    $sandbox = @()
    if ($journal.SandboxProcesses) {
        $sandbox = @($journal.SandboxProcesses | ForEach-Object { Convert-ProcessRecord $_ })
    }
    if ($sandbox.Count -gt 16) { throw 'Unexpected Sandbox process count.' }
    $known = @{}
    $known[$testProcess.Id] = $testProcess
    foreach ($record in $sandbox) {
        if (-not $record.Path.StartsWith($env:WINDIR.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase) -or
            [IO.Path]::GetFileName($record.Path) -notlike 'WindowsSandbox*.exe' -or
            -not $known.ContainsKey($record.ParentId)) {
            throw 'Sandbox recovery record lacks a valid system executable and recorded parent.'
        }
        Assert-ChildIdentity $record $known[$record.ParentId]
        $known[$record.Id] = $record
    }
    $viewer = [IntPtr]([long]$journal.ViewerHwnd)
    $viewerPid = [Microsoft.MouseWithoutBorders.UITests.NativeSupport]::WindowProcessId($viewer)
    $viewerOwner = @($sandbox | Where-Object { $_.Id -eq $viewerPid -and $_.IsCurrent() })
    if ($viewer -ne [IntPtr]::Zero -and $viewerOwner.Count -eq 1) {
        $null = [Microsoft.MouseWithoutBorders.UITests.NativeSupport]::PostMessage($viewer, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero)
        $closeDeadline = [DateTime]::UtcNow.AddSeconds(15)
        while ($viewerOwner[0].IsCurrent() -and [DateTime]::UtcNow -lt $closeDeadline) {
            Assert-RecoveryDeadline
            [Microsoft.MouseWithoutBorders.UITests.NativeSupport]::ConfirmSandboxClose($viewer, $viewerPid)
            Start-Sleep -Milliseconds 250
        }
    }
    [array]::Reverse($sandbox)
    foreach ($record in $sandbox) { Stop-RecordedProcess $record }

    $settleDeadline = [DateTime]::UtcNow.AddSeconds(15)
    do {
        Assert-RecoveryDeadline
        $remaining = ''
        foreach ($process in Get-Process) {
            if ($process.ProcessName -like 'WindowsSandbox*') {
                $remaining = 'Sandbox processes remain; no unrecorded process will be terminated.'
            }
            if ($process.ProcessName -match '^PowerToys($|\.)') {
                try { $path = [Microsoft.MouseWithoutBorders.UITests.NativeSupport]::ImagePath($process.Id) }
                catch {
                    if (-not (Get-Process -Id $process.Id -ErrorAction SilentlyContinue)) { continue }
                    throw
                }
                if ($path.StartsWith($productRoot + '\', [StringComparison]::OrdinalIgnoreCase)) {
                    $remaining = 'A staged product process remains; settings restoration is not safe.'
                }
            }
        }
        if (-not $remaining) { break }
        Start-Sleep -Milliseconds 250
    } while ([DateTime]::UtcNow -lt $settleDeadline)
    if ($remaining) { throw $remaining }
    if ($endpoint) {
        $alreadyRestored = [bool]$endpoint.SettingsRestored
        Restore-RecordedSettings $endpoint
        if (-not $alreadyRestored) {
            $endpoint.SettingsRestored = $true
            $endpoint.TimestampUtc = [DateTime]::UtcNow.ToString('o')
            Write-RunJson $endpointPath $endpoint
        }
        $unchanged = $endpoint.PSObject.Properties['ClipboardMayHaveChanged'] -and -not $endpoint.ClipboardMayHaveChanged
        $result.ClipboardRestored = [bool]$endpoint.ClipboardRestored -or $unchanged
    }
    else {
        $result.SettingsRestored = $true
        $result.ClipboardRestored = $true
    }
    if (-not $result.ClipboardRestored) {
        $failures.Add('Original clipboard was held only in the exited worker; restore the clean VM baseline. No clipboard data was persisted or guessed.')
    }
}
catch { $failures.Add($_.Exception.Message) }
finally {
    $result.Errors = @($failures.ToArray())
    $result.RequiresBaselineReset = $failures.Count -gt 0
    $result.Status = if ($failures.Count -eq 0) { 'Recovered' } else { 'Incomplete' }
    $result.TimestampUtc = [DateTime]::UtcNow.ToString('o')
    if ($resultPath) { Write-RunJson $resultPath $result }
    $result | ConvertTo-Json -Depth 6
}
if ($failures.Count) { exit 1 }
exit 0

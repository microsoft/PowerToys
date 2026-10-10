# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

<#
.SYNOPSIS
Stops an owned experiment and restores the three host settings files exactly.
.DESCRIPTION
Use the same host account. Also supports recovery after the supervisor exits unexpectedly.
Only recorded process identities and the recorded Sandbox GUID can be stopped.
#>
[CmdletBinding()]
param([Parameter(Mandatory = $true)][string]$RunRoot)

$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\Experiment.Common.ps1"
$statePath = Join-Path $RunRoot 'run.json'
$state = Read-ExperimentJson $statePath
$identity = Get-ExperimentIdentity
if ($identity.UserSid -ne $state.Identity.UserSid -or $identity.ComputerName -ine $state.Identity.ComputerName) {
    throw 'Cleanup must use the original host and user account.'
}
$activePath = Join-Path $state.Destination 'active-run.json'
if ($state.Status -eq 'Stopped') {
    if ((Test-Path -LiteralPath $activePath) -and (Read-ExperimentJson $activePath).RunId -eq $state.RunId) {
        Remove-Item -LiteralPath $activePath
    }
    Write-Host "Already restored: $RunRoot"
    return
}
Write-ExperimentJson "$RunRoot\input\stop.json" @{ RunId = $state.RunId }
if ($state.Status -ne 'CleanupFailed' -and $PID -ne $state.Supervisor.Id -and (Test-ExperimentProcess $state.Supervisor)) {
    $deadline = [DateTime]::UtcNow.AddSeconds(45)
    do {
        Start-Sleep -Seconds 1
        $state = Read-ExperimentJson $statePath
        if ($state.Status -eq 'Stopped') { Write-Host "Stopped and restored: $RunRoot"; return }
        if ($state.Status -eq 'CleanupFailed' -or -not (Test-ExperimentProcess $state.Supervisor)) { break }
    } while ([DateTime]::UtcNow -lt $deadline)
    if ($state.Status -ne 'CleanupFailed' -and (Test-ExperimentProcess $state.Supervisor) -and
        (Test-Path -LiteralPath "$RunRoot\input\host-lease.json")) {
        $lease = Read-ExperimentJson "$RunRoot\input\host-lease.json"
        if (([DateTime]::UtcNow - [DateTime]$lease.TimestampUtc).TotalSeconds -le 30) {
            throw 'The supervisor has not completed cleanup. Inspect its shell; do not start another experiment or overwrite its backups.'
        }
    }
}
$cleanupLock = [IO.File]::Open("$RunRoot\cleanup.lock", [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::Write, [IO.FileShare]::None)
try {
$errors = @()
try { Stop-MwbEndpoint "$RunRoot\host" } catch { $errors += $_.ToString() }
try {
    if ($state.SandboxRequested) {
        # Guest cooperatively restores its own settings first. Host restoration never waits on bootstrap.
        $deadline = [DateTime]::UtcNow.AddSeconds(15)
        while (-not (Test-Path -LiteralPath "$RunRoot\guest\stopped.json") -and [DateTime]::UtcNow -lt $deadline) {
            Start-Sleep -Seconds 1
        }
        $inventory = Invoke-ExperimentWsb @('list')
        $ids = @(Get-ExperimentSandboxIds $inventory)
        if ($state.SandboxId -in $ids) {
            $null = Invoke-ExperimentWsb @('stop', '--id', $state.SandboxId)
        }
        $inventory = Invoke-ExperimentWsb @('list')
        if ($state.SandboxId -in @(Get-ExperimentSandboxIds $inventory)) {
            throw "Owned Sandbox $($state.SandboxId) is still running."
        }
    }
}
catch { $errors += $_.ToString() }
if ($errors.Count) {
    $state.Status = 'CleanupFailed'
    Write-ExperimentJson $statePath $state
    Write-ExperimentJson "$RunRoot\cleanup-failed.json" @{ Errors = $errors; TimestampUtc = [DateTime]::UtcNow.ToString('o') }
    throw "Cleanup incomplete. Backups retained. Retry this command after resolving: $($errors -join '; ')"
}
Get-ChildItem -LiteralPath "$RunRoot\input\requests" -File | Remove-Item -Force
foreach ($keyFile in @("$RunRoot\host\generated-key.txt", "$RunRoot\guest\generated-key.txt")) {
    if (Test-Path -LiteralPath $keyFile) { Remove-Item -LiteralPath $keyFile -Force }
}
$state.Status = 'Stopped'
Write-ExperimentJson $statePath $state
if ((Test-Path -LiteralPath $activePath) -and (Read-ExperimentJson $activePath).RunId -eq $state.RunId) {
    Remove-Item -LiteralPath $activePath
}
Write-Host "Stopped and restored: $RunRoot"
}
finally { $cleanupLock.Dispose() }

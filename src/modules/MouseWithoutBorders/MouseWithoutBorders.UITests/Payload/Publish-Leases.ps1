# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

[CmdletBinding()]
param([Parameter(Mandatory = $true)][string]$ConfigurationPath)

$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\EndpointSupport.ps1"
$config = Read-RunJson $ConfigurationPath
$parent = $null
$sequence = [int]$config.InitialSequence
$watch = [Diagnostics.Stopwatch]::StartNew()
$lastCycle = 0L
$maxCycleMilliseconds = 0L
$stalls = @()
$state = [ordered]@{
    RunId = $config.RunId; ProcessId = $PID; Stage = 'Starting'; Sequence = $sequence
    TimestampUtc = [DateTime]::UtcNow.ToString('o'); MaxCycleMilliseconds = 0
    HostWriteMilliseconds = 0; GuestWriteMilliseconds = 0; Stalls = @()
}

try {
    $parent = [Diagnostics.Process]::GetProcessById([int]$config.ParentId)
    # Keep a handle to the original process so PID reuse cannot extend this lease.
    $null = $parent.Handle
    if ($parent.StartTime.ToUniversalTime() -ne ([DateTime]$config.ParentStartTimeUtc).ToUniversalTime() -or
        $parent.SessionId -ne [int]$config.ParentSessionId) {
        throw 'Lease publisher parent identity does not match this run.'
    }
    while (-not $parent.HasExited) {
        if ([DateTime]::UtcNow -ge ([DateTime]$config.HardDeadlineUtc).ToUniversalTime()) {
            throw 'Lease publisher exceeded the run hard deadline.'
        }
        if (Test-Path -LiteralPath ($config.StopPath + '.ready')) {
            $stop = Read-RunJson $config.StopPath
            if ($stop.RunId -ne $config.RunId) { throw 'Lease stop request belongs to another run.' }
            $state.Stage = 'Stopped'
            break
        }
        $now = $watch.ElapsedMilliseconds
        $gap = $now - $lastCycle
        $lastCycle = $now
        $maxCycleMilliseconds = [Math]::Max($maxCycleMilliseconds, $gap)
        if ($gap -gt 10000) {
            $stalls = @($stalls | Select-Object -Last 15) + @{
                Sequence = $sequence; GapMilliseconds = $gap; TimestampUtc = [DateTime]::UtcNow.ToString('o')
            }
        }
        $sequence++
        $state.Sequence = $sequence
        $state.MaxCycleMilliseconds = $maxCycleMilliseconds
        $state.Stalls = $stalls
        foreach ($role in @('Host', 'Guest')) {
            $state.Stage = "Publishing$role"
            $state.TimestampUtc = [DateTime]::UtcNow.ToString('o')
            Write-RunJson $config.OutputPath $state
            $started = $watch.ElapsedMilliseconds
            $inputRoot = if ($role -eq 'Host') { $config.HostInputRoot } else { $config.GuestInputRoot }
            Write-RunJson (Join-Path "$inputRoot\leases" ('{0:D8}.json' -f $sequence)) @{
                RunId = $config.RunId; Sequence = $sequence; TimestampUtc = [DateTime]::UtcNow.ToString('o')
            }
            $state["${role}WriteMilliseconds"] = $watch.ElapsedMilliseconds - $started
        }
        $state.Stage = 'Published'
        $state.TimestampUtc = [DateTime]::UtcNow.ToString('o')
        Write-RunJson $config.OutputPath $state
        if ($parent.WaitForExit(2000)) { break }
    }
    if ($state.Stage -ne 'Stopped') { $state.Stage = 'ParentExited' }
    $state.TimestampUtc = [DateTime]::UtcNow.ToString('o')
    Write-RunJson $config.OutputPath $state
}
catch {
    $state.Stage = 'Failed'
    $state.TimestampUtc = [DateTime]::UtcNow.ToString('o')
    $state.Error = $_.Exception.Message
    Write-RunJson $config.OutputPath $state
    throw
}
finally {
    if ($null -ne $parent) { $parent.Dispose() }
}

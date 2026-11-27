# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

#requires -Version 7.0

<#
.SYNOPSIS
Watches exact Azure DevOps builds until they become terminal.

.DESCRIPTION
Polls checkpointed build IDs through the existing Azure CLI-authenticated REST helper. Every response
must match the expected branch and source commit. Run this as one attached background command in a
client with completion notifications. On Windows, the process requests continuous system sleep
prevention while it waits.

.PARAMETER BuildId
One or more numeric Azure DevOps build IDs.

.PARAMETER ExpectedBranch
The exact source branch ref expected for every build.

.PARAMETER ExpectedSourceVersion
The exact 40-character source commit expected for every build.

.PARAMETER Organization
Azure DevOps organization for all checkpointed build IDs. Defaults to `shine-oss`.

.PARAMETER Project
Azure DevOps project for all checkpointed build IDs. Defaults to `PowerToys`.

.PARAMETER PollIntervalSeconds
Seconds between authenticated status reads. Defaults to 1200 (20 minutes).

.PARAMETER TimeoutMinutes
Maximum total wait. Defaults to 360 minutes (6 hours).

.PARAMETER MaxConsecutiveErrors
Number of consecutive REST failures allowed before stopping. Defaults to 3.

.EXAMPLE
pwsh -NoLogo -NoProfile -File `
  .github\skills\ui-tests-pipeline-ci\scripts\Wait-AzureDevOpsBuild.ps1 `
  -BuildId 123456789 `
  -ExpectedBranch refs/heads/example `
  -ExpectedSourceVersion 0123456789abcdef0123456789abcdef01234567
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [ValidateRange(1, [int]::MaxValue)]
    [int[]] $BuildId,

    [Parameter(Mandatory)]
    [ValidatePattern('^refs/heads/.+')]
    [string] $ExpectedBranch,

    [Parameter(Mandatory)]
    [ValidatePattern('^[0-9a-fA-F]{40}$')]
    [string] $ExpectedSourceVersion,

    [ValidateNotNullOrEmpty()]
    [string] $Organization = 'shine-oss',

    [ValidateNotNullOrEmpty()]
    [string] $Project = 'PowerToys',

    [ValidateRange(10, 3600)]
    [int] $PollIntervalSeconds = 1200,

    [ValidateRange(1, 720)]
    [int] $TimeoutMinutes = 360,

    [ValidateRange(1, 20)]
    [int] $MaxConsecutiveErrors = 3
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'AzureDevOps.ps1')

$buildIds = @($BuildId | Sort-Object -Unique)
$expectedSourceVersionNormalized = $ExpectedSourceVersion.ToLowerInvariant()
$deadlineUtc = [DateTime]::UtcNow.AddMinutes($TimeoutMinutes)
$lastFingerprint = $null
$lastOutputUtc = [DateTime]::MinValue
$lastBuilds = @()
$consecutiveErrors = 0
$executionStateContinuous = [uint32]2147483648
$executionStateSystemRequired = [uint32]2147483649
$executionStateSet = $false

try
{
    if ($IsWindows)
    {
        try
        {
            if (-not ('PowerToys.UiTests.Ci.NativeMethods' -as [type]))
            {
                Add-Type -TypeDefinition @'
using System.Runtime.InteropServices;

namespace PowerToys.UiTests.Ci
{
    public static class NativeMethods
    {
        [DllImport("kernel32.dll")]
        public static extern uint SetThreadExecutionState(uint executionState);
    }
}
'@
            }

            $executionStateSet = [PowerToys.UiTests.Ci.NativeMethods]::SetThreadExecutionState(
                ($executionStateContinuous -bor $executionStateSystemRequired)) -ne 0
            if (-not $executionStateSet)
            {
                Write-Warning 'System sleep prevention was rejected; the Azure DevOps wait will continue.'
            }
        }
        catch
        {
            Write-Warning "System sleep prevention is unavailable; the Azure DevOps wait will continue: $($_.Exception.Message)"
        }
    }

    $null = Test-AzDevOpsSession

    while ($true)
    {
        $observedUtc = [DateTime]::UtcNow
        try
        {
            $responses = @(
                foreach ($id in $buildIds)
                {
                    [pscustomobject]@{
                        RequestedId = $id
                        Build = (Invoke-AzDevOpsRest `
                                -Uri "_apis/build/builds/${id}?api-version=7.1" `
                                -Organization $Organization `
                                -Project $Project).Body
                    }
                }
            )
            $builds = @(
                foreach ($response in $responses)
                {
                    ConvertTo-AzDevOpsBuildSnapshot `
                        -Build $response.Build `
                        -RequestedId $response.RequestedId `
                        -ExpectedBranch $ExpectedBranch `
                        -ExpectedSourceVersion $expectedSourceVersionNormalized
                }
            )
            $consecutiveErrors = 0
        }
        catch
        {
            $consecutiveErrors++
            if ($consecutiveErrors -ge $MaxConsecutiveErrors)
            {
                throw "Azure DevOps status query failed $consecutiveErrors consecutive times: $($_.Exception.Message)"
            }

            [pscustomobject]@{
                Event = 'query-error'
                ObservedUtc = $observedUtc.ToString('o')
                ConsecutiveErrors = $consecutiveErrors
                Message = $_.Exception.Message
            } | ConvertTo-Json -Compress

            $remainingSeconds = [Math]::Floor(($deadlineUtc - [DateTime]::UtcNow).TotalSeconds)
            if ($remainingSeconds -le 0)
            {
                [pscustomobject]@{
                    Event = 'timeout'
                    ObservedUtc = [DateTime]::UtcNow.ToString('o')
                    TimeoutMinutes = $TimeoutMinutes
                    Builds = $lastBuilds
                } | ConvertTo-Json -Depth 6 -Compress
                throw "Timed out after $TimeoutMinutes minutes waiting for build(s): $($buildIds -join ', ')."
            }

            $waitSeconds = [Math]::Min($PollIntervalSeconds, $remainingSeconds)
            Start-Sleep -Seconds $waitSeconds
            continue
        }

        $lastBuilds = $builds

        if (@($builds | Where-Object Status -NE 'completed').Count -eq 0)
        {
            [pscustomobject]@{
                Event = 'terminal'
                ObservedUtc = $observedUtc.ToString('o')
                Builds = $builds
            } | ConvertTo-Json -Depth 6 -Compress
            return
        }

        $fingerprint = @($builds | ForEach-Object {
                "$($_.Id):$($_.Status):$($_.Result):$($_.LastChangedDate)"
            }) -join '|'
        if ($fingerprint -ne $lastFingerprint -or ($observedUtc - $lastOutputUtc).TotalMinutes -ge 20)
        {
            [pscustomobject]@{
                Event = 'progress'
                ObservedUtc = $observedUtc.ToString('o')
                DeadlineUtc = $deadlineUtc.ToString('o')
                Builds = $builds
            } | ConvertTo-Json -Depth 6 -Compress
            $lastFingerprint = $fingerprint
            $lastOutputUtc = $observedUtc
        }

        $remainingSeconds = [Math]::Floor(($deadlineUtc - [DateTime]::UtcNow).TotalSeconds)
        if ($remainingSeconds -le 0)
        {
            [pscustomobject]@{
                Event = 'timeout'
                ObservedUtc = [DateTime]::UtcNow.ToString('o')
                TimeoutMinutes = $TimeoutMinutes
                Builds = $builds
            } | ConvertTo-Json -Depth 6 -Compress
            $states = @($builds | ForEach-Object { "$($_.Id)=$($_.Status)/$($_.Result)" }) -join ', '
            throw "Timed out after $TimeoutMinutes minutes waiting for build(s): $states."
        }

        $waitSeconds = [Math]::Min($PollIntervalSeconds, $remainingSeconds)
        Start-Sleep -Seconds $waitSeconds
    }
}
finally
{
    if ($executionStateSet)
    {
        # Best effort: the request is per-thread and PowerShell may resume finally on another thread.
        # Process exit clears any request that remains associated with the original thread.
        try
        {
            $null = [PowerToys.UiTests.Ci.NativeMethods]::SetThreadExecutionState($executionStateContinuous)
        }
        catch
        {
            Write-Warning "System sleep-prevention cleanup failed: $($_.Exception.Message)"
        }
    }
}
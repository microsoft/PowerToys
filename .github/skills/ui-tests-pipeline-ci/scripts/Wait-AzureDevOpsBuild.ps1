# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

#requires -Version 7.0

<#
.SYNOPSIS
Waits synchronously for exact Azure DevOps builds to become terminal.

.DESCRIPTION
Polls checkpointed build IDs through the existing Azure CLI-authenticated REST helper. Every response
must match the expected branch and source commit. The process remains in the foreground so an active
agent turn resumes when all builds become terminal. On Windows, the process prevents system sleep
while it waits.

.PARAMETER BuildId
One or more numeric Azure DevOps build IDs.

.PARAMETER ExpectedBranch
The exact source branch ref expected for every build.

.PARAMETER ExpectedSourceVersion
The exact 40-character source commit expected for every build.

.PARAMETER PollIntervalSeconds
Seconds between authenticated status reads. Defaults to 120.

.PARAMETER TimeoutMinutes
Maximum total wait. Defaults to 180 minutes.

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

    [ValidateRange(10, 3600)]
    [int] $PollIntervalSeconds = 120,

    [ValidateRange(1, 720)]
    [int] $TimeoutMinutes = 180,

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
$waitHandle = [Threading.ManualResetEventSlim]::new($false)
$executionStateSet = $false

try
{
    if ($IsWindows)
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

        $executionStateSet = [PowerToys.UiTests.Ci.NativeMethods]::SetThreadExecutionState([uint32]2147483649) -ne 0
        if (-not $executionStateSet)
        {
            throw 'Failed to prevent system sleep while waiting for Azure DevOps builds.'
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
                                -Uri "_apis/build/builds/${id}?api-version=7.1").Body
                    }
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
            $null = $waitHandle.Wait([TimeSpan]::FromSeconds($waitSeconds))
            continue
        }

        $builds = @(
            foreach ($response in $responses)
            {
                $id = $response.RequestedId
                $build = $response.Build
                if ([int]$build.id -ne $id)
                {
                    throw "Requested build $id but Azure DevOps returned build $($build.id)."
                }

                if ([string]$build.sourceBranch -cne $ExpectedBranch)
                {
                    throw "Build $id source branch '$($build.sourceBranch)' does not match '$ExpectedBranch'."
                }

                if ([string]$build.sourceVersion -ine $expectedSourceVersionNormalized)
                {
                    throw "Build $id source version '$($build.sourceVersion)' does not match '$ExpectedSourceVersion'."
                }

                [pscustomobject]@{
                    Id = [int]$build.id
                    BuildNumber = [string]$build.buildNumber
                    Status = [string]$build.status
                    Result = [string]$build.result
                    QueueTime = $build.queueTime
                    StartTime = $build.startTime
                    FinishTime = $build.finishTime
                    LastChangedDate = $build.lastChangedDate
                    WebUrl = [string]$build._links.web.href
                }
            }
        )
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
        if ($fingerprint -ne $lastFingerprint -or ($observedUtc - $lastOutputUtc).TotalMinutes -ge 15)
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
        $null = $waitHandle.Wait([TimeSpan]::FromSeconds($waitSeconds))
    }
}
finally
{
    if ($executionStateSet)
    {
        $null = [PowerToys.UiTests.Ci.NativeMethods]::SetThreadExecutionState([uint32]2147483648)
    }

    $waitHandle.Dispose()
}
# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.

#requires -Version 7.0

[CmdletBinding()]
param(
    [string[]] $ProcessName = @('PowerToys', 'PowerToys.Settings'),

    [ValidateRange(1, 300)]
    [int] $TimeoutSeconds = 30,

    [ValidateRange(1, 20)]
    [int] $RequiredStableSamples = 3,

    [ValidateRange(10, 5000)]
    [int] $PollIntervalMilliseconds = 250,

    [ValidateRange(1, 60)]
    [int] $ProcessExitTimeoutSeconds = 5
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$processNames = @($ProcessName | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique)
if ($processNames.Count -eq 0)
{
    throw 'At least one process name is required.'
}

$stopwatch = [Diagnostics.Stopwatch]::StartNew()
$stableAbsentSamples = 0
$remainingDetails = @()
$stopFailures = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)

do
{
    foreach ($name in $processNames)
    {
        foreach ($process in @(Get-Process -Name $name -ErrorAction SilentlyContinue))
        {
            try
            {
                if (-not $process.HasExited)
                {
                    Write-Host "Stopping $name (pid $($process.Id)) before Authenticode signing."
                    $process.Kill($true)
                    if (-not $process.WaitForExit($ProcessExitTimeoutSeconds * 1000))
                    {
                        $null = $stopFailures.Add(
                            "$name pid $($process.Id) did not exit within $ProcessExitTimeoutSeconds seconds.")
                    }
                }
            }
            catch [InvalidOperationException]
            {
                # The process exited between enumeration and inspection.
            }
            catch
            {
                $null = $stopFailures.Add("Failed to stop $name pid $($process.Id): $($_.Exception.Message)")
            }
            finally
            {
                $process.Dispose()
            }
        }
    }

    Start-Sleep -Milliseconds $PollIntervalMilliseconds

    $remaining = @($processNames | ForEach-Object {
            Get-Process -Name $_ -ErrorAction SilentlyContinue
        })
    try
    {
        $remainingDetails = @($remaining | ForEach-Object { "$($_.ProcessName) pid $($_.Id)" })
        if ($remaining.Count -eq 0)
        {
            $stableAbsentSamples++
        }
        else
        {
            $stableAbsentSamples = 0
        }
    }
    finally
    {
        $remaining | ForEach-Object { $_.Dispose() }
    }

    if ($stableAbsentSamples -ge $RequiredStableSamples)
    {
        Write-Host (
            "PowerToys processes remained absent for $RequiredStableSamples consecutive samples " +
            "after $($stopwatch.Elapsed.TotalSeconds.ToString('F1')) seconds.")
        return
    }
}
while ($stopwatch.Elapsed.TotalSeconds -lt $TimeoutSeconds)

$details = [Collections.Generic.List[string]]::new()
if ($remainingDetails.Count -gt 0)
{
    $details.Add("Remaining: $($remainingDetails -join ', ').")
}
else
{
    $details.Add(
        "No process was present in the final sample, but absence was stable for only " +
        "$stableAbsentSamples/$RequiredStableSamples samples.")
}

if ($stopFailures.Count -gt 0)
{
    $details.Add("Stop errors: $($stopFailures -join ' ')")
}

throw (
    "Could not stop PowerToys processes within $($stopwatch.Elapsed.TotalSeconds.ToString('F1')) seconds. " +
    ($details -join ' '))

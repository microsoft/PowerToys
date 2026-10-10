<#
.SYNOPSIS
Compares result files from Measure-StartupPerformance.ps1 and prints a Markdown table.

.DESCRIPTION
Pools the measured samples of each side. To cancel out machine drift, alternate the runs between
the builds (baseline, candidate, candidate, baseline, ...) and pass all the files of each side.
Warm-up samples are left out. Negative changes are improvements for times and memory.

.PARAMETER Baseline
One or more result files for the baseline build.

.PARAMETER Candidate
One or more result files for the build to compare.

.EXAMPLE
.\Compare-StartupPerformance.ps1 -Baseline main-1.json, main-2.json -Candidate r2r-1.json, r2r-2.json
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string[]]$Baseline,

    [Parameter(Mandatory)]
    [string[]]$Candidate
)

$ErrorActionPreference = 'Stop'

function Get-Samples
{
    param([string[]]$Paths)

    $samples = @()
    foreach ($path in $Paths)
    {
        $result = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
        $samples += @($result.Samples | Where-Object { -not $_.IsWarmup })
    }

    return $samples
}

function Get-Percentile
{
    param([double[]]$Values, [double]$Percentile)

    $sorted = @($Values | Sort-Object)
    $index = [Math]::Max(0, [Math]::Ceiling($Percentile * $sorted.Count) - 1)
    return $sorted[$index]
}

function Get-Values
{
    param([object[]]$Samples, [string]$Scenario, [string]$Metric)

    return @($Samples | Where-Object { $_.Scenario -eq $Scenario } | ForEach-Object { $_.$Metric } | Where-Object { $null -ne $_ } | ForEach-Object { [double]$_ })
}

function Format-Change
{
    param([double]$From, [double]$To)

    $delta = $To - $From
    $text = '{0:+0.#;-0.#;0}' -f $delta
    if ($From -ne 0)
    {
        $text += ' ({0:+0.#;-0.#;0}%)' -f (100.0 * $delta / $From)
    }

    return $text
}

$baselineSamples = Get-Samples -Paths $Baseline
$candidateSamples = Get-Samples -Paths $Candidate

Write-Host '| Scenario | Metric | n | Baseline median | Candidate median | Change | Baseline P90 | Candidate P90 | Change |'
Write-Host '|---|---|--:|--:|--:|--:|--:|--:|--:|'

$scenarios = @($baselineSamples | ForEach-Object Scenario | Select-Object -Unique)
foreach ($scenario in $scenarios)
{
    $first = $baselineSamples | Where-Object { $_.Scenario -eq $scenario } | Select-Object -First 1
    $metrics = $first.PSObject.Properties.Name | Where-Object { $_ -notin 'Scenario', 'Iteration', 'IsWarmup' }
    foreach ($metric in $metrics)
    {
        $before = Get-Values -Samples $baselineSamples -Scenario $scenario -Metric $metric
        $after = Get-Values -Samples $candidateSamples -Scenario $scenario -Metric $metric
        if ($before.Count -eq 0 -or $after.Count -eq 0)
        {
            continue
        }

        $medianBefore = Get-Percentile -Values $before -Percentile 0.5
        $medianAfter = Get-Percentile -Values $after -Percentile 0.5
        $p90Before = Get-Percentile -Values $before -Percentile 0.9
        $p90After = Get-Percentile -Values $after -Percentile 0.9
        Write-Host ('| {0} | {1} | {2}/{3} | {4:0.#} | {5:0.#} | {6} | {7:0.#} | {8:0.#} | {9} |' -f
            $scenario, $metric, $before.Count, $after.Count,
            $medianBefore, $medianAfter, (Format-Change -From $medianBefore -To $medianAfter),
            $p90Before, $p90After, (Format-Change -From $p90Before -To $p90After))
    }
}

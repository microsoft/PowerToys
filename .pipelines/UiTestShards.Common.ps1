# Deterministic UI-test sharding: longest-processing-time greedy bin-packing over
# the durations in uiTestDurations.json. Every shard runs the same computation over the
# same suite list, so each one independently agrees on who owns which suite.

function Get-UiTestShardPlan {
    param(
        [Parameter(Mandatory)] [AllowEmptyCollection()] [string[]] $Suites,
        [Parameter(Mandatory)] [ValidateRange(1, 6)] [int] $ShardCount,
        [string] $Platform = 'x64Win11',
        [string] $DurationsPath = (Join-Path $PSScriptRoot 'uiTestDurations.json')
    )

    $durations = Get-Content -LiteralPath $DurationsPath -Raw | ConvertFrom-Json
    $multiplier = 1.0
    if ($durations.platformMultipliers.PSObject.Properties.Name -contains $Platform) {
        $multiplier = [double]$durations.platformMultipliers.$Platform
    }

    $weighted = @($Suites | Sort-Object -Unique | ForEach-Object {
        $minutes = [double]$durations.defaultMinutes
        if ($durations.suites.PSObject.Properties.Name -contains $_) {
            $minutes = [double]$durations.suites.$_
        }
        [pscustomobject]@{ Suite = $_; Minutes = [math]::Round($minutes * $multiplier, 2) }
    })

    $shards = @(1..$ShardCount | ForEach-Object {
        [pscustomobject]@{ Index = $_; Minutes = 0.0; Suites = [Collections.Generic.List[string]]::new() }
    })

    # Heaviest first (name breaks ties); each goes to the lightest shard (lowest index breaks ties).
    foreach ($item in ($weighted | Sort-Object @{ Expression = 'Minutes'; Descending = $true }, Suite)) {
        $target = $shards | Sort-Object Minutes, Index | Select-Object -First 1
        $target.Suites.Add($item.Suite)
        $target.Minutes = [math]::Round($target.Minutes + $item.Minutes, 2)
    }

    return $shards
}

function Get-UiTestShardSuites {
    param(
        [Parameter(Mandatory)] [AllowEmptyCollection()] [string[]] $Suites,
        [Parameter(Mandatory)] [int] $ShardIndex,
        [Parameter(Mandatory)] [int] $ShardCount,
        [string] $Platform = 'x64Win11',
        [string] $DurationsPath = (Join-Path $PSScriptRoot 'uiTestDurations.json')
    )

    if ($ShardIndex -lt 1 -or $ShardIndex -gt $ShardCount) {
        throw "Shard index $ShardIndex is outside 1..$ShardCount."
    }

    $plan = Get-UiTestShardPlan -Suites $Suites -ShardCount $ShardCount -Platform $Platform -DurationsPath $DurationsPath
    foreach ($shard in $plan) {
        $marker = if ($shard.Index -eq $ShardIndex) { '>>' } else { '  ' }
        Write-Host ("{0} shard {1}/{2} ~{3,6:N1} min: {4}" -f $marker, $shard.Index, $ShardCount, $shard.Minutes, ($shard.Suites -join ', '))
    }

    return @($plan[$ShardIndex - 1].Suites)
}

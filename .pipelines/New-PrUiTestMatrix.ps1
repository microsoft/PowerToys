# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.

param(
    [Parameter(Mandatory)][AllowEmptyCollection()][string[]] $Modules,
    [Parameter(Mandatory)][ValidateSet('x64Win10', 'x64Win11', 'arm64')][string] $Platform,
    [switch] $RouteUmdfArm64,
    [string] $DurationsPath = (Join-Path $PSScriptRoot 'uiTestDurations.json')
)

$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\UiTestArtifacts.Common.ps1"
. "$PSScriptRoot\UiTestShards.Common.ps1"
. "$PSScriptRoot\umdf\UmdfCi.Common.ps1"
Assert-UiTestProjectStems $Modules

if ($Platform -eq 'arm64') {
    $excluded = @('MouseWithoutBorders.UITests')
    foreach ($fixture in Get-UmdfFixtures) {
        $selection = Get-UmdfUiTestSelection -Modules $Modules -Platform $Platform `
            -AutoSelect $true -RouteArm64 $RouteUmdfArm64.IsPresent -FixtureModule $fixture.Suite
        if ($selection.Exclude) { $excluded += $fixture.Suite }
    }
    $Modules = @($Modules | Where-Object { $_ -notin $excluded })
}

$matrix = [ordered]@{}
if ($Modules.Count -gt 0) {
    $serial = Get-UiTestShardPlan -Suites $Modules -ShardCount 1 -Platform $Platform -DurationsPath $DurationsPath
    # Leave headroom for ordinary PRs and for installer, signing, and agent startup on each shard.
    $count = [int][Math]::Clamp([Math]::Ceiling($serial.Minutes / 45), 1, [Math]::Min(3, $serial.Suites.Count))
    $shards = if ($count -eq 1) { $serial } else {
        Get-UiTestShardPlan -Suites $Modules -ShardCount $count -Platform $Platform -DurationsPath $DurationsPath
    }
    foreach ($shard in $shards) {
        $matrix["Shard$($shard.Index)"] = @{
            AutoUiTestModules = $shard.Suites -join ';'
            UiTestArtifactPattern = ($shard.Suites | ForEach-Object { "**/tests/$_/**" }) -join "`n"
        }
        Write-Host "$Platform shard $($shard.Index)/${count} (~$($shard.Minutes) min): $($shard.Suites -join ', ')"
    }
}

return $matrix

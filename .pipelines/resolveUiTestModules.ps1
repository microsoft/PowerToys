# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.

param(
    [Parameter(Mandatory)]
    [string] $RepoRoot,

    [string[]] $ChangedFile = @()
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $RepoRoot -PathType Container)) {
    throw "Repository root does not exist: $RepoRoot"
}

$normalizedChangedFiles = @($ChangedFile | ForEach-Object { $_ -replace '\\', '/' })
$touchedModules = @(
    $normalizedChangedFiles |
        ForEach-Object {
            if ($_ -match '^src/modules/([^/]+)(?:/|$)') {
                $Matches[1]
            }
        } |
        Sort-Object -Unique
)

$sharedHarnessChanged = @(
    $normalizedChangedFiles |
        Where-Object { $_ -match '^src/common/UITestAutomation\.Next(?:/|$)' }
).Count -gt 0

$projectRoots = if ($sharedHarnessChanged) {
    @(
        (Join-Path $RepoRoot 'src\modules')
        (Join-Path $RepoRoot 'src\settings-ui')
    )
}
else {
    @($touchedModules | ForEach-Object { Join-Path $RepoRoot "src\modules\$_" })
}

$uiTestModules = foreach ($projectRoot in $projectRoots) {
    if (-not (Test-Path -LiteralPath $projectRoot -PathType Container)) {
        continue
    }

    Get-ChildItem -LiteralPath $projectRoot -Filter '*.csproj' -File -Recurse |
        Where-Object {
            [xml] $project = Get-Content -LiteralPath $_.FullName -Raw
            @($project.SelectNodes("//*[local-name()='ProjectReference']")) |
                Where-Object {
                    ($_.Include -replace '\\', '/') -match '/UITestAutomation\.Next/UITestAutomation\.Next\.csproj$'
                }
        } |
        ForEach-Object { $_.BaseName }
}

[PSCustomObject]@{
    TouchedModules = @($touchedModules)
    SharedHarnessChanged = $sharedHarnessChanged
    UiTestModules = @($uiTestModules | Sort-Object -Unique)
}

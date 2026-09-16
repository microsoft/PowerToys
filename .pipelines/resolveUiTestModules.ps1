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

$touchedModules = @(
    $ChangedFile |
        ForEach-Object { $_ -replace '\\', '/' } |
        ForEach-Object {
            if ($_ -match '^src/modules/([^/]+)(?:/|$)') {
                $Matches[1]
            }
        } |
        Sort-Object -Unique
)

$uiTestModules = foreach ($module in $touchedModules) {
    $moduleRoot = Join-Path $RepoRoot "src\modules\$module"
    if (-not (Test-Path -LiteralPath $moduleRoot -PathType Container)) {
        continue
    }

    Get-ChildItem -LiteralPath $moduleRoot -Filter '*.csproj' -File -Recurse |
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
    UiTestModules = @($uiTestModules | Sort-Object -Unique)
}

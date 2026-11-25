# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

param(
    [string]$HostPath = (Join-Path $PSScriptRoot '..\..\..\x64\Debug\PowerScripts\PowerScripts.Host.exe'),
    [switch]$Check
)

$ErrorActionPreference = 'Stop'
$HostPath = (Resolve-Path -LiteralPath $HostPath).Path
$lines = & $HostPath template cli-matrix
if ($LASTEXITCODE -ne 0) { throw 'Unable to generate the CLI scenario matrix. Build the current Host first.' }
$generated = ($lines -join "`n").TrimEnd("`r", "`n") + "`n"
$path = Join-Path $PSScriptRoot 'CLI-TEMPLATE-SCENARIOS.md'
$document = [IO.File]::ReadAllText($path).Replace("`r`n", "`n")
$beginMarker = '<!-- BEGIN GENERATED CLI SCENARIOS -->'
$endMarker = '<!-- END GENERATED CLI SCENARIOS -->'
$begin = $document.IndexOf($beginMarker, [StringComparison]::Ordinal)
$end = $document.IndexOf($endMarker, [StringComparison]::Ordinal)
if ($begin -lt 0 -or $end -le $begin) { throw 'The scenario matrix requires one ordered pair of generated-section markers.' }
$begin += $beginMarker.Length
$updated = $document.Substring(0, $begin) + "`n" + $generated + $document.Substring($end)
if ($Check) {
    if ($document -cne $updated) { throw 'The CLI scenario matrix is stale. Run Update-CliTemplateMatrix.ps1 with the current Host.' }
    Write-Output 'CLI scenario matrix is current.'
} else {
    [IO.File]::WriteAllText($path, $updated, [Text.UTF8Encoding]::new($false))
    Write-Output "Updated $path"
}

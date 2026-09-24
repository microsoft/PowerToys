#requires -Version 7.0

<#
.SYNOPSIS
Removes pseudo-localized resource files for the selected culture.
.DESCRIPTION
Accepts a source resource file, its pseudo-localized output, or a directory to scan
recursively. The default culture is qps-ploc. Only matching RESX and RESW files are
removed; directory scans leave bin, obj, and unrelated files untouched.
.EXAMPLE
.\Clear-PseudoLocalization.ps1 -Path .\Microsoft.CmdPal.UI
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory, Position = 0)]
    [string]$Path,

    [Parameter()]
    [string]$Culture = 'qps-ploc'
)

Write-Host "-----------------"
Write-Host "-- $Path --"
Write-Host "-----------------"

$scriptPath = Join-Path $PSScriptRoot 'pseudolocalizer.ps1'
. $scriptPath

exit (Clear-PseudoLocalization -Path $Path -Culture $Culture)

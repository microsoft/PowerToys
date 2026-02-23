#requires -Version 7.0

[CmdletBinding()]
param(
    [Parameter(Mandatory, Position = 0)]
    [string]$Path,

    [Parameter()]
    [ValidateSet('brackets', 'diacritics', 'xs')]
    [string]$Mode = 'brackets',

    [Parameter()]
    [string]$Culture = 'qps-ploc'
)

$scriptPath = Join-Path $PSScriptRoot 'pseudolocalizer.ps1'
. $scriptPath

$global:LASTEXITCODE = Invoke-PseudoLocalization -Path $Path -Mode $Mode -Culture $Culture
return

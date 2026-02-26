#requires -Version 7.0

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

$global:LASTEXITCODE = Clear-PseudoLocalization -Path $Path -Culture $Culture
return

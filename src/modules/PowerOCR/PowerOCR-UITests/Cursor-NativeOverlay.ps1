# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

#Requires -Version 7.0

<#
.SYNOPSIS
Enables the opt-in native PowerOCR selection surface and its cursor trace.
.DESCRIPTION
Changes only local diagnostic markers. Does not build, launch, or change SDK versions.
The markers are checked when a new overlay opens. Existing SDK comparison snapshots
predate this prototype; use the updated original PowerOCR project for this test.
#>
[CmdletBinding()]
param(
    [ValidateSet('Enable', 'Disable', 'Status')]
    [string]$Mode = 'Status'
)

$ErrorActionPreference = 'Stop'
$cursorLogRoot = Join-Path $env:LOCALAPPDATA 'Microsoft/PowerToys/TextExtractor/Logs'
$nativeMarker = Join-Path $cursorLogRoot 'cursor-native-overlay.enabled'
$traceMarker = Join-Path $cursorLogRoot 'cursor-diagnostics.enabled'

if ($Mode -eq 'Enable') {
    New-Item -ItemType Directory -Path $cursorLogRoot -Force | Out-Null
    New-Item -ItemType File -Path $nativeMarker -Force | Out-Null
    New-Item -ItemType File -Path $traceMarker -Force | Out-Null
    Write-Host 'Native selection prototype and cursor tracing enabled for the next overlay.'
    Write-Host 'Use the updated original PowerOCR project in VS, Debug/x64. The old SDK comparison snapshots do not contain this prototype.'
}
elseif ($Mode -eq 'Disable') {
    Remove-Item -LiteralPath $nativeMarker -ErrorAction SilentlyContinue
    Write-Host 'The next overlay will use the original WinUI selection surface. Cursor tracing remains enabled for comparison.'
}

[pscustomobject]@{
    NativeSelectionEnabled = Test-Path -LiteralPath $nativeMarker
    CursorTracingEnabled = Test-Path -LiteralPath $traceMarker
    TraceDirectory = $cursorLogRoot
} | ConvertTo-Json

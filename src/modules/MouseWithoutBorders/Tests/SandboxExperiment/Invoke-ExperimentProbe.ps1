# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

<#
.SYNOPSIS
Explicitly runs one UI/session probe against an owned, already-running endpoint.
.DESCRIPTION
Guest requests travel through a run-specific read-only mapping; results use the writable
evidence mapping. No wsb exec, clipboard, simulated keystrokes, or firewall operations.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$RunRoot,
    [Parameter(Mandatory = $true)][ValidateSet('Host', 'Guest')][string]$Target,
    [Parameter(Mandatory = $true)]
    [ValidateSet('Navigate', 'GenerateKey', 'Connect', 'Status', 'Refresh', 'SessionEvidence')][string]$Action,
    [string]$KeyPath,
    [ValidateRange(10, 120)][int]$TimeoutSeconds = 45
)

$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\Experiment.Common.ps1"
$state = Read-ExperimentJson "$RunRoot\run.json"
$identity = Get-ExperimentIdentity
if ($state.Status -ne 'Running' -or -not (Test-ExperimentProcess $state.Supervisor) -or
    $identity.UserSid -ne $state.Identity.UserSid -or $identity.ComputerName -ine $state.Identity.ComputerName) {
    throw 'The experiment is not supervised by the original host user.'
}
if (Test-Path -LiteralPath "$RunRoot\input\stop.json") { throw 'Cleanup has been requested.' }
$null = Assert-ExperimentDesktop -AllowNonConsole
$network = Read-ExperimentJson "$RunRoot\network.json"
$key = ''
if ($Action -eq 'Connect') {
    if (-not $KeyPath) { throw 'Connect requires -KeyPath from the other endpoint''s explicit GenerateKey probe.' }
    $key = [IO.File]::ReadAllText((Resolve-Path -LiteralPath $KeyPath).Path)
}
$peerName = if ($Target -eq 'Guest') { $network.HostName } else { $network.GuestName }
$requestId = [Guid]::NewGuid().ToString()
$resultPath = Join-Path $RunRoot "$($Target.ToLowerInvariant())\$requestId.json"
if ($Target -eq 'Host') {
    if ($Action -eq 'SessionEvidence') {
        $result = Get-EndpointEvidence "$RunRoot\host"
    }
    else {
        $result = & "$PSScriptRoot\Invoke-MwbUi.ps1" -Action $Action `
            -SettingsProcessId (Get-EndpointSettingsProcess "$RunRoot\host") `
            -WinApp "$PSScriptRoot\winapp\winapp.exe" -Key $key -PeerName $peerName `
            -GeneratedKeyPath "$RunRoot\host\generated-key.txt"
    }
    Write-ExperimentJson $resultPath @{ RunId = $state.RunId; RequestId = $requestId; Status = 'Completed'; Result = $result }
}
else {
    $requestPath = Join-Path $RunRoot "input\requests\$requestId.json"
    Write-ExperimentJson $requestPath @{
        RunId = $state.RunId; RequestId = $requestId; Action = $Action; Key = $key; PeerName = $peerName
    }
    try {
        $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
        while (-not (Test-Path -LiteralPath $resultPath)) {
            if ([DateTime]::UtcNow -gt $deadline -or (Test-Path -LiteralPath "$RunRoot\input\stop.json")) {
                throw 'Guest probe did not complete. This is not a connectivity result; inspect guest evidence.'
            }
            Start-Sleep -Milliseconds 250
        }
    }
    finally { Remove-Item -LiteralPath $requestPath -Force -ErrorAction SilentlyContinue }
}
$response = Read-ExperimentJson $resultPath
if ($response.RunId -ne $state.RunId -or $response.RequestId -ne $requestId) { throw 'Mismatched probe response.' }
if ($response.Status -ne 'Completed') { throw "Probe failed: $($response.Error)" }
Write-Host "Evidence: $resultPath"
$response

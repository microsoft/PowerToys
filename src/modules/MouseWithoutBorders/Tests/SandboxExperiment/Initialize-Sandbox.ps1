# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

# Invoked only by the generated .wsb LogonCommand, in the guest interactive session.
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\Experiment.Common.ps1"
$inputRoot = 'C:\MwbRun'
$outputRoot = 'C:\MwbEvidence'
$endpointRoot = Join-Path $outputRoot 'endpoint'
$request = Read-ExperimentJson "$inputRoot\request.json"
$runId = $request.RunId

function Test-HostLease {
    if (Test-Path -LiteralPath "$inputRoot\stop.json") { return $false }
    $lease = Read-ExperimentJson "$inputRoot\host-lease.json"
    $bootstrapping = -not (Test-Path -LiteralPath "$outputRoot\ready.json") -and
        [DateTime]::UtcNow -le [DateTime]$request.BootstrapDeadlineUtc
    if ($lease.RunId -ne $runId -or
        (-not $bootstrapping -and ([DateTime]::UtcNow - [DateTime]$lease.TimestampUtc).TotalSeconds -gt 30)) {
        throw 'Host lease expired. Closing guest endpoint; recover host settings with Stop-Experiment.ps1 if needed.'
    }
    $null = Assert-ExperimentDesktop -AllowNonConsole:([bool]$request.AllowNonConsole)
    Write-ExperimentJson "$outputRoot\heartbeat.json" @{ RunId = $runId; TimestampUtc = [DateTime]::UtcNow.ToString('o') }
    return $true
}

try {
    if (-not $request.AllowNonConsole) { throw 'Missing explicit experiment flag.' }
    Assert-NoPowerToys
    Assert-ExperimentPayload $request.Manifest $PSScriptRoot 'C:\PowerToysBuild'
    $null = Wait-ExperimentDesktop -DeadlineUtc ([DateTime]$request.BootstrapDeadlineUtc) `
        -AllowNonConsole:([bool]$request.AllowNonConsole)
    if (-not (Test-HostLease)) { return }
    Write-ExperimentJson "$outputRoot\ready.json" ([ordered]@{
        RunId = $runId; TimestampUtc = [DateTime]::UtcNow.ToString('o')
        ComputerName = [Net.Dns]::GetHostName()
        PowerShellVersion = $PSVersionTable.PSVersion.ToString()
        Desktop = Get-ExperimentDesktop
        Addresses = @(Get-NetIPAddress -AddressFamily IPv4 | Select-Object InterfaceIndex, InterfaceAlias, IPAddress, PrefixLength)
        Gateways = @(Get-NetRoute -DestinationPrefix '0.0.0.0/0' | Select-Object InterfaceIndex, InterfaceAlias, NextHop)
    })
    while (-not (Test-Path -LiteralPath "$inputRoot\start-endpoint.json")) {
        if (-not (Test-HostLease)) { return }
        Start-Sleep -Seconds 1
    }
    $network = Read-ExperimentJson "$inputRoot\start-endpoint.json"
    if ($network.RunId -ne $runId) { throw 'Endpoint request belongs to a different run.' }
    Start-MwbEndpoint -EndpointRoot $endpointRoot -ProductRoot 'C:\PowerToysBuild' -ModuleNames $request.ModuleNames `
        -PeerName $network.HostName -PeerAddress $network.HostAddress -AllowNonConsole
    $deadline = [DateTime]::UtcNow.AddSeconds(60)
    do {
        if (-not (Test-HostLease)) { return }
        Update-EndpointProcesses $endpointRoot
        $settingsProcess = Get-EndpointSettingsProcess $endpointRoot -AllowStarting
        if ($settingsProcess) { break }
        Start-Sleep -Seconds 1
    } while ([DateTime]::UtcNow -lt $deadline)
    if (-not $settingsProcess) { throw 'The guest Settings process did not appear.' }
    Write-ExperimentJson "$outputRoot\endpoint-started.json" @{ RunId = $runId; SettingsProcessId = $settingsProcess }
    while (Test-HostLease) {
        Update-EndpointProcesses $endpointRoot
        foreach ($file in Get-ChildItem -LiteralPath "$inputRoot\requests" -Filter '*.json' -File) {
            $resultPath = Join-Path $outputRoot $file.Name
            if (Test-Path -LiteralPath $resultPath) { continue }
            $probe = Read-ExperimentJson $file.FullName
            if ($probe.RunId -ne $runId -or $file.BaseName -ne $probe.RequestId) { throw 'Invalid probe correlation.' }
            try {
                if ($probe.Action -eq 'SessionEvidence') {
                    $result = Get-EndpointEvidence $endpointRoot
                }
                else {
                    $result = & "$PSScriptRoot\Invoke-MwbUi.ps1" -Action $probe.Action `
                        -SettingsProcessId (Get-EndpointSettingsProcess $endpointRoot) `
                        -WinApp "$PSScriptRoot\winapp\winapp.exe" -Key $probe.Key -PeerName $probe.PeerName `
                        -GeneratedKeyPath "$outputRoot\generated-key.txt"
                }
                Write-ExperimentJson $resultPath @{ RunId = $runId; RequestId = $probe.RequestId; Status = 'Completed'; Result = $result }
            }
            catch {
                Write-ExperimentJson $resultPath @{ RunId = $runId; RequestId = $probe.RequestId; Status = 'Failed'; Error = $_.ToString() }
            }
        }
        Start-Sleep -Seconds 1
    }
}
catch {
    Write-ExperimentJson "$outputRoot\failed.json" @{
        RunId = $runId; Error = $_.ToString(); ScriptStackTrace = $_.ScriptStackTrace
    }
    throw
}
finally {
    Stop-MwbEndpoint $endpointRoot
    Write-ExperimentJson "$outputRoot\stopped.json" @{ RunId = $runId; TimestampUtc = [DateTime]::UtcNow.ToString('o') }
}

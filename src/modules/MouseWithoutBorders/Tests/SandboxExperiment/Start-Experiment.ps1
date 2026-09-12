# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

<#
.SYNOPSIS
Explicitly starts and supervises a prepared host/Sandbox Debug experiment.
.DESCRIPTION
Keep this foreground command running. Ctrl+C, a stop request, an inactive host desktop,
or a lost guest heartbeat triggers cleanup. It does not pair devices or run UI probes.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Destination,
    [switch]$AllowNonConsole,
    [ValidateRange(30, 600)][int]$StartupTimeoutSeconds = 180
)

$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\Experiment.Common.ps1"
if (-not $AllowNonConsole) { throw 'Starting requires explicit -AllowNonConsole consent for the Debug-only experiment.' }
$mutex = New-Object Threading.Mutex($false, ('Global\PowerToys.MwbSandboxExperiment.' + (Get-ExperimentIdentity).UserSid))
$ownsMutex = $false
try {
try { $ownsMutex = $mutex.WaitOne(0) } catch [Threading.AbandonedMutexException] { $ownsMutex = $true }
if (-not $ownsMutex) { throw 'Another MWB experiment supervisor is running for this user.' }
$manifest = Read-ExperimentJson (Join-Path $Destination 'prepared.json')
if ([IO.Path]::GetFullPath($Destination).TrimEnd('\') -ine $manifest.Destination -or
    $manifest.FormatVersion -ne 1 -or $manifest.Configuration -ne 'Debug' -or $manifest.Platform -ne 'x64') {
    throw 'Use an unmoved x64 Debug preparation made by these scripts.'
}
$Destination = $manifest.Destination
$inputRoot = Join-Path $Destination 'input'
if ($PSScriptRoot -ine $inputRoot) { throw "Run the staged launcher: $inputRoot\Start-Experiment.ps1" }
Assert-ExperimentPayload $manifest $inputRoot $manifest.ProductRoot
$moduleSource = Join-Path $manifest.SourceRoot 'src\settings-ui\Settings.UI.Library\EnabledModules.cs'
if ((Get-FileHash -LiteralPath $moduleSource -Algorithm SHA256).Hash -ne $manifest.EnabledModulesSourceSha256) {
    throw 'The module list changed after preparation. Rebuild and prepare a new destination.'
}
$desktop = Assert-ExperimentDesktop -AllowNonConsole
if ($desktop.Elevated) { throw 'Start from a non-elevated host PowerShell. No elevation/de-elevation mechanism is provided.' }
Assert-NoPowerToys
$null = Get-Command wsb.exe -ErrorAction Stop
$existing = Invoke-ExperimentWsb @('list')
if (@(Get-ExperimentSandboxIds $existing).Count -or
    @(Get-Process | Where-Object { $_.ProcessName -match '^WindowsSandbox(Client|RemoteSession)?$' }).Count) {
    throw 'A Sandbox already exists. Close it yourself first; the shared Sandbox server will not be stopped.'
}
$activePath = Join-Path $Destination 'active-run.json'
if (Test-Path -LiteralPath $activePath) {
    throw "An earlier run needs cleanup. Read $activePath and run Stop-Experiment.ps1 with its RunRoot."
}
$runId = [Guid]::NewGuid().ToString()
$runRoot = Join-Path $Destination "runs\$runId"
$runInput = Join-Path $runRoot 'input'
$guestRoot = Join-Path $runRoot 'guest'
$hostRoot = Join-Path $runRoot 'host'
$null = New-Item -ItemType Directory -Path "$runInput\requests", $guestRoot, $hostRoot
$state = [ordered]@{
    RunId = $runId; RunRoot = $runRoot; Status = 'Starting'
    Identity = Get-ExperimentIdentity
    Supervisor = Get-ExperimentProcessRecord (Get-Process -Id $PID)
    SandboxId = $runId; SandboxRequested = $false
    Destination = $Destination; ProductRoot = $manifest.ProductRoot
    StartedUtc = [DateTime]::UtcNow.ToString('o')
}
$statePath = Join-Path $runRoot 'run.json'
Write-ExperimentJson $statePath $state
# CreateNew refuses a competing supervisor instead of replacing its run marker.
$marker = [IO.File]::Open($activePath, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::Read)
try {
    $bytes = (New-Object Text.UTF8Encoding($false)).GetBytes(
        (@{ RunId = $runId; RunRoot = $runRoot } | ConvertTo-Json))
    $marker.Write($bytes, 0, $bytes.Length)
}
finally { $marker.Dispose() }
Write-Host "Run: $runRoot"
Write-Host "Stop from another host shell: & '$inputRoot\Stop-Experiment.ps1' -RunRoot '$runRoot'"

function Update-HostLease {
    if (Test-Path -LiteralPath "$runInput\stop.json") { return $false }
    $null = Assert-ExperimentDesktop -AllowNonConsole
    Write-ExperimentJson "$runInput\host-lease.json" @{ RunId = $runId; TimestampUtc = [DateTime]::UtcNow.ToString('o') }
    Update-EndpointProcesses $hostRoot
    return $true
}

try {
    Write-ExperimentJson "$runInput\request.json" ([ordered]@{
        RunId = $runId; AllowNonConsole = $true
        BootstrapDeadlineUtc = [DateTime]::UtcNow.AddSeconds($StartupTimeoutSeconds).ToString('o')
        HostName = ConvertTo-MwbMachineName ([Net.Dns]::GetHostName())
        ModuleNames = $manifest.ModuleNames
        Manifest = $manifest
    })
    if (-not (Update-HostLease)) { return }
    $xml = [IO.File]::ReadAllText("$inputRoot\Sandbox.wsb.template").
        Replace('__RUN_INPUT__', [Security.SecurityElement]::Escape($runInput)).
        Replace('__RUN_OUTPUT__', [Security.SecurityElement]::Escape($guestRoot))
    $wsbPath = Join-Path $runRoot 'Sandbox.wsb'
    [IO.File]::WriteAllText($wsbPath, $xml, (New-Object Text.UTF8Encoding($false)))
    $state.SandboxRequested = $true
    Write-ExperimentJson $statePath $state
    $null = Invoke-ExperimentWsb @('start', '--id', $runId, '--config', $xml)
    # Connect provides the interactive guest desktop; bootstrap itself uses LogonCommand, never wsb exec.
    $null = Invoke-ExperimentWsb @('connect', '--id', $runId)
    $deadline = [DateTime]::UtcNow.AddSeconds($StartupTimeoutSeconds)
    do {
        if (-not (Update-HostLease)) { return }
        if (Test-Path -LiteralPath "$guestRoot\failed.json") {
            throw "Guest bootstrap failed: $([IO.File]::ReadAllText("$guestRoot\failed.json"))"
        }
        if (Test-Path -LiteralPath "$guestRoot\ready.json") { break }
        Start-Sleep -Seconds 1
    } while ([DateTime]::UtcNow -lt $deadline)
    $ready = Read-ExperimentJson "$guestRoot\ready.json"
    if ($ready.RunId -ne $runId) { throw 'Guest readiness belongs to a different run.' }

    # The guest gateway must also be a current host vEthernet address. Never reuse an old IP.
    $hostAddresses = @(Get-NetIPAddress -AddressFamily IPv4 | Where-Object { $_.InterfaceAlias -like 'vEthernet*' })
    $gateways = @($ready.Gateways | Where-Object { $_.NextHop -in @($hostAddresses | ForEach-Object { $_.IPAddress }) })
    if ($gateways.Count -ne 1) { throw 'Cannot uniquely match the guest Default Switch gateway to a host vEthernet address.' }
    $guestAddresses = @($ready.Addresses | Where-Object {
        $_.InterfaceIndex -eq $gateways[0].InterfaceIndex -and $_.IPAddress -notmatch '^(127\.|169\.254\.)'
    })
    if ($guestAddresses.Count -ne 1) { throw 'Cannot uniquely determine the current guest IPv4 address.' }
    $network = [ordered]@{
        RunId = $runId
        HostDnsName = [Net.Dns]::GetHostName(); GuestDnsName = $ready.ComputerName
        HostName = ConvertTo-MwbMachineName ([Net.Dns]::GetHostName())
        HostAddress = $gateways[0].NextHop
        GuestName = ConvertTo-MwbMachineName $ready.ComputerName
        GuestAddress = $guestAddresses[0].IPAddress
    }
    Write-ExperimentJson "$runRoot\network.json" $network
    if (-not (Update-HostLease)) { return }
    Start-MwbEndpoint -EndpointRoot $hostRoot -ProductRoot $manifest.ProductRoot -ModuleNames $manifest.ModuleNames `
        -PeerName $network.GuestName -PeerAddress $network.GuestAddress -AllowNonConsole
    Write-ExperimentJson "$runInput\start-endpoint.json" $network
    $deadline = [DateTime]::UtcNow.AddSeconds($StartupTimeoutSeconds)
    do {
        if (-not (Update-HostLease)) { return }
        if (Test-Path -LiteralPath "$guestRoot\failed.json") { throw 'Guest endpoint failed; see guest\failed.json.' }
        $hostSettings = Get-EndpointSettingsProcess $hostRoot -AllowStarting
        if ($hostSettings -and (Test-Path -LiteralPath "$guestRoot\endpoint-started.json")) { break }
        Start-Sleep -Seconds 1
    } while ([DateTime]::UtcNow -lt $deadline)
    $started = Read-ExperimentJson "$guestRoot\endpoint-started.json"
    if ($started.RunId -ne $runId) { throw 'Guest endpoint evidence belongs to a different run.' }
    $null = Get-EndpointSettingsProcess $hostRoot
    $state.Status = 'Running'
    Write-ExperimentJson $statePath $state
    Write-Host 'Endpoints launched, NOT paired or verified. Use Invoke-ExperimentProbe.ps1 explicitly from a second shell.'
    while (Update-HostLease) {
        if (Test-Path -LiteralPath "$guestRoot\failed.json") { throw 'Guest failed; see guest\failed.json.' }
        $heartbeat = Read-ExperimentJson "$guestRoot\heartbeat.json"
        if ($heartbeat.RunId -ne $runId -or
            ([DateTime]::UtcNow - [DateTime]$heartbeat.TimestampUtc).TotalSeconds -gt 30) {
            throw 'Guest heartbeat expired.'
        }
        $null = Get-EndpointSettingsProcess $hostRoot
        Start-Sleep -Seconds 1
    }
}
catch {
    Write-ExperimentJson "$runRoot\failure.json" @{
        RunId = $runId; Error = $_.ToString(); ScriptStackTrace = $_.ScriptStackTrace
        TimestampUtc = [DateTime]::UtcNow.ToString('o')
    }
    throw
}
finally {
    & "$inputRoot\Stop-Experiment.ps1" -RunRoot $runRoot
}
}
finally {
    if ($ownsMutex) { $mutex.ReleaseMutex() }
    $mutex.Dispose()
}

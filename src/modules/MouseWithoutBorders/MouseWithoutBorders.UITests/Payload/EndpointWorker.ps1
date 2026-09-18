# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$InputRoot,
    [Parameter(Mandatory = $true)][string]$OutputRoot,
    [Parameter(Mandatory = $true)][string]$ProductRoot,
    [Parameter(Mandatory = $true)][string]$WinApp,
    [string]$ProductArchive
)

$ErrorActionPreference = 'Stop'
$entryProcess = [Diagnostics.Process]::GetCurrentProcess()
[IO.File]::WriteAllText("$OutputRoot\bootstrap-entry.txt", (
    'Utc={0}; ProcessStartUtc={1}; CpuSeconds={2}' -f [DateTime]::UtcNow.ToString('o'),
    $entryProcess.StartTime.ToUniversalTime().ToString('o'), $entryProcess.TotalProcessorTime.TotalSeconds))
. "$PSScriptRoot\EndpointSupport.ps1"
[IO.File]::WriteAllText("$OutputRoot\bootstrap-support-loaded.txt", (
    'Utc={0}; CpuSeconds={1}' -f [DateTime]::UtcNow.ToString('o'),
    $entryProcess.TotalProcessorTime.TotalSeconds))
$entryProcess.Dispose()
$script:config = Read-RunJson "$InputRoot\bootstrap.json"
$script:settingsRoot = Join-Path $env:LOCALAPPDATA 'Microsoft\PowerToys'
$script:backupRoot = Join-Path $env:LOCALAPPDATA "Microsoft\PowerToysUiTestRecovery\$($script:config.RunId)"
$script:startedUtc = [DateTime]::UtcNow
$script:owned = $null
$script:backups = @()
$script:logOffsets = @{}
$script:settingsRestored = $false
$script:clipboardRestored = $false
$script:clipboardMayHaveChanged = $false
$script:status = 'Bootstrapping'
$script:firewallRule = ''
$script:settingsPid = 0
$script:expectedPeerMapping = ''
$script:receiver = $null
$script:requestNumber = 0
$script:leaseNumber = 0
$script:lastLease = $null
$script:stopping = $false
$script:cleaned = $false
$script:bootstrapStageNumber = 0
$script:bootstrapWatch = [Diagnostics.Stopwatch]::StartNew()

try {
    Write-BootstrapStage 'LoadingReceiverTypes'
    Add-Type -AssemblyName System.Windows.Forms, System.Drawing
    Add-Type -Path @("$PSScriptRoot\NativeSupport.cs", "$PSScriptRoot\Receiver.cs", "$PSScriptRoot\TcpSocketTable.cs") `
        -ReferencedAssemblies System.Windows.Forms, System.Drawing
    $script:owned = [Collections.Generic.List[Microsoft.MouseWithoutBorders.UITests.ProcessIdentity]]::new()
    Write-BootstrapStage 'ReceiverTypesLoaded'
    $null = [Microsoft.MouseWithoutBorders.UITests.NativeSupport]::SetProcessDPIAware()
    Assert-CleanEndpoint
    $deadline = ([DateTime]$script:config.BootstrapDeadlineUtc).ToUniversalTime()
    do {
        $desktop = [Microsoft.MouseWithoutBorders.UITests.NativeSupport]::Desktop()
        if ($desktop.Ready) { break }
        Start-Sleep -Milliseconds 250
    } while ([DateTime]::UtcNow -lt $deadline)
    if (-not $desktop.Ready) { throw 'Initial endpoint desktop did not become active and unlocked before its deadline.' }
    if ($script:config.Role -eq 'Host' -and $desktop.Elevated) { throw 'Host worker must be a standard user.' }
    if ($script:config.Role -eq 'Guest' -and (-not $desktop.Elevated -or $env:USERNAME -ne 'WDAGUtilityAccount')) {
        throw 'This pilot supports only the ordinary elevated WDAGUtilityAccount guest desktop.'
    }
    $script:receiver = [Microsoft.MouseWithoutBorders.UITests.ReceiverController]::new($script:config.Role, $script:config.RunId)
    Write-BootstrapStage 'ReceiverReady'
    if ($script:config.Role -eq 'Guest') {
        if (-not $ProductArchive -or (Test-Path -LiteralPath $ProductRoot)) {
            throw 'Guest runtime must be extracted once into a new local directory.'
        }
        Write-RunJson "$OutputRoot\staging.json" @{ Status = 'ExtractingLocalRuntime'; TimestampUtc = [DateTime]::UtcNow.ToString('o') }
        Write-BootstrapStage 'CopyingLocalArchive'
        $localArchive = "$ProductRoot.zip"
        Write-BootstrapStage 'ArchiveCopied' (Copy-GuestRuntimeArchive $ProductArchive $localArchive $script:config.GuestArchiveSha256)
        Write-BootstrapStage 'ExtractingRuntime' @{ ArchiveBytes = (Get-Item -LiteralPath $localArchive).Length }
        $null = New-Item -ItemType Directory -Path $ProductRoot
        Expand-GuestRuntime $localArchive $ProductRoot $deadline
        Remove-Item -LiteralPath $localArchive -ErrorAction Stop
    }
    Write-BootstrapStage 'ValidatingManifest'
    foreach ($entry in $script:config.Manifest) {
        $path = Join-Path $ProductRoot $entry.Path
        if (-not (Test-Path -LiteralPath $path -PathType Leaf) -or
            (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $entry.Sha256) {
            throw "Payload identity mismatch: $($entry.Path)"
        }
    }
    Write-BootstrapStage 'ManifestValidated'
    if ($script:config.Role -eq 'Guest') {
        # Avoid repeatedly loading the CLI across redirected storage while the
        # nested VM is also loading the product. Keep the staging source read-only.
        $localTools = Join-Path $env:LOCALAPPDATA "PowerToysMwbTools\$($script:config.RunId)"
        $null = New-Item -ItemType Directory -Path $localTools
        foreach ($name in @('winapp.exe', 'libSkiaSharp.dll')) {
            $source = Join-Path (Split-Path $WinApp) $name
            $destination = Join-Path $localTools $name
            Copy-Item -LiteralPath $source -Destination $destination
            if ((Get-FileHash -LiteralPath $source).Hash -ne (Get-FileHash -LiteralPath $destination).Hash) {
                throw 'The locally staged guest CLI differs from its read-only source.'
            }
        }
        $WinApp = Join-Path $localTools 'winapp.exe'
    }
    Write-BootstrapStage 'ToolsReady'
    $bootstrapLeaseMeasured = $false
    while (-not $script:stopping) {
        try {
            if ([DateTime]::UtcNow -gt ([DateTime]$script:config.HardDeadlineUtc).ToUniversalTime()) {
                throw 'Endpoint exceeded the run hard deadline.'
            }
            # Leases are liveness snapshots, not commands. Replaying hundreds of
            # obsolete redirected files after startup can itself delay readiness.
            $candidate = Read-LatestEndpointLease "$InputRoot\leases" $script:config.RunId $script:leaseNumber
            if ($candidate) {
                $script:lastLease = $candidate
                $script:leaseNumber = $candidate.Sequence
            }
            if (-not $bootstrapLeaseMeasured) {
                Write-BootstrapStage 'LeaseBacklogConsumed' @{ LeaseSequence = $script:leaseNumber }
                $bootstrapLeaseMeasured = $true
            }
            $lease = Confirm-EndpointLease "$InputRoot\leases" $script:config.RunId $script:lastLease
            if ($lease.Sequence -ne $script:leaseNumber) {
                Write-RunJson "$OutputRoot\lease-refresh.json" @{
                    PreviousSequence = $script:leaseNumber; PreviousTimestampUtc = $script:lastLease.TimestampUtc
                    Sequence = $lease.Sequence; LeaseTimestampUtc = $lease.TimestampUtc
                    TimestampUtc = [DateTime]::UtcNow.ToString('o')
                }
            }
            $script:lastLease = $lease
            $script:leaseNumber = $lease.Sequence
            if (-not (Test-Path -LiteralPath "$OutputRoot\ready.json")) {
                $script:receiver.FocusInput()
                $script:status = 'Ready'
                Write-EndpointJournal
                Write-BootstrapStage 'DiscoveringNetwork'
                $network = Get-EndpointNetwork
                Write-BootstrapStage 'NetworkDiscovered'
                Write-RunJson "$OutputRoot\ready.json" ([ordered]@{
                    RunId = $script:config.RunId; Role = $script:config.Role; TimestampUtc = [DateTime]::UtcNow.ToString('o')
                    ComputerName = Get-MachineName; Desktop = [Microsoft.MouseWithoutBorders.UITests.NativeSupport]::Desktop()
                    Worker = [Microsoft.MouseWithoutBorders.UITests.ProcessIdentity]::Capture($PID)
                    ReceiverHwnd = $script:receiver.Handle.ToInt64(); PowerShellVersion = $PSVersionTable.PSVersion.ToString()
                    Addresses = $network.Addresses
                    Gateways = $network.Gateways
                })
                Write-BootstrapStage 'ReadyPublished'
            }
            Write-RunJson "$OutputRoot\heartbeat.json" @{
                RunId = $script:config.RunId; TimestampUtc = [DateTime]::UtcNow.ToString('o')
                LeaseSequence = $script:leaseNumber; LeaseTimestampUtc = $lease.TimestampUtc
            }
            $next = $script:requestNumber + 1
            $requestPath = Join-Path "$InputRoot\requests" ("{0:D4}.json" -f $next)
            if (-not (Test-Path -LiteralPath "$requestPath.ready")) {
                Start-Sleep -Milliseconds 200
                continue
            }
            $request = Read-RunJson $requestPath
            if ($request.RunId -ne $script:config.RunId -or $request.Sequence -ne $next) { throw 'Request correlation mismatch.' }
            $script:requestNumber = $next
            $response = @{ RunId = $script:config.RunId; Sequence = $next; Status = 'Completed'; Result = $null }
            try {
                switch ($request.Action) {
                    'Start' {
                        $response.Result = Start-Endpoint $request.PeerName $request.PeerAddress `
                            $request.GlobalSettings $request.ExpectedLocalAddress
                    }
                    'Connect' { $response.Result = Connect-Guest $request.Key $request.PeerName }
                    'VerifyPeerMapping' { $response.Result = Assert-PeerMapping 'before-connect' }
                    'ClipboardSharing' { $response.Result = Set-GuestClipboardSharing ([bool]$request.Enabled) }
                    'Transport' { $response.Result = Get-Transport }
                    'FocusInput' {
                        $focusWatch = [Diagnostics.Stopwatch]::StartNew()
                        $stableFocus = 0
                        do {
                            $script:receiver.FocusInput()
                            $foreground = [Microsoft.MouseWithoutBorders.UITests.NativeSupport]::GetForegroundWindow()
                            if ($foreground -eq $script:receiver.Handle -and $script:receiver.InputFocused) { $stableFocus++ }
                            else { $stableFocus = 0 }
                            if ($stableFocus -ge 2) { break }
                            Start-Sleep -Milliseconds 100
                        } while ($focusWatch.Elapsed.TotalSeconds -lt 10)
                        if ($stableFocus -lt 2) {
                            throw "Receiver focus was refused: foreground=$($foreground.ToInt64()), receiver=$($script:receiver.Handle.ToInt64())."
                        }
                        $response.Result = @{ Hwnd = $script:receiver.Handle.ToInt64(); ForegroundHwnd = $foreground.ToInt64(); InputFocused = $true }
                    }
                    'ClearInput' { $script:receiver.ClearInput(); $response.Result = @{ Cleared = $true } }
                    'PublishClipboard' {
                        $script:clipboardMayHaveChanged = $true
                        Write-EndpointJournal
                        $response.Result = @{ Digest = $script:receiver.PublishClipboard() }
                    }
                    'Observe' {
                        $point = [Windows.Forms.Cursor]::Position
                        $bounds = $script:receiver.ClickBounds
                        $response.Result = @{
                            Text = $script:receiver.ReceivedText; Clicks = $script:receiver.Clicks
                            ClipboardDigest = $script:receiver.ClipboardDigest()
                            CursorX = $point.X; CursorY = $point.Y
                            ClickLeft = $bounds.Left; ClickTop = $bounds.Top; ClickRight = $bounds.Right; ClickBottom = $bounds.Bottom
                            ForegroundHwnd = [Microsoft.MouseWithoutBorders.UITests.NativeSupport]::GetForegroundWindow().ToInt64()
                            ReceiverHwnd = $script:receiver.Handle.ToInt64()
                            Desktop = [Microsoft.MouseWithoutBorders.UITests.NativeSupport]::Desktop()
                        }
                    }
                    'Evidence' { $response.Result = Save-EndpointEvidence }
                    'StopPeers' {
                        Stop-Endpoint
                        $response.Result = @{ PeersStopped = $true; SettingsRestored = $true }
                    }
                    'Finish' {
                        if ($script:status -ne 'PeersStoppedSettingsRestored') { throw 'Stop peers before restoring clipboard.' }
                        if (-not $request.BothPeersStopped) { throw 'Host must confirm both peers stopped before clipboard restoration.' }
                        $script:receiver.RestoreClipboard()
                        $script:clipboardRestored = $true
                        Write-EndpointJournal
                        if ($script:backups.Count) {
                            foreach ($backup in $script:backups) {
                                if ($backup.Existed) { Remove-Item -LiteralPath $backup.Backup -Force }
                            }
                            Remove-Item -LiteralPath $script:backupRoot -ErrorAction Stop
                        }
                        $script:status = 'Cleaned'
                        Write-EndpointJournal
                        $script:cleaned = $true
                        $script:stopping = $true
                        $response.Result = @{ ClipboardRestored = $true }
                    }
                    default { throw "Unsupported endpoint command: $($request.Action)" }
                }
            }
            catch {
                $response.Status = 'Failed'
                $response.Error = $_.Exception.Message
                if ($request.PSObject.Properties['Key'] -and $request.Key) {
                    $response.Error = $response.Error.Replace($request.Key, '[REDACTED]')
                }
                $response.ScriptStackTrace = $_.ScriptStackTrace
            }
            Write-RunJson (Join-Path $OutputRoot ("{0:D4}.json" -f $next)) $response
            if ($script:stopping) { $script:receiver.Close() }
        }
        catch {
            Write-RunJson "$OutputRoot\failed.json" @{
                RunId = $script:config.RunId; Error = $_.Exception.Message; ScriptStackTrace = $_.ScriptStackTrace
            }
            $script:stopping = $true
            $script:receiver.Close()
        }
        if (-not $script:stopping) { Start-Sleep -Milliseconds 200 }
    }
}
catch {
    Write-RunJson "$OutputRoot\failed.json" @{
        RunId = $script:config.RunId; Error = $_.Exception.Message; ScriptStackTrace = $_.ScriptStackTrace
    }
    throw
}
finally {
    if (-not $script:cleaned -and $null -ne $script:owned) {
        try {
            Stop-Endpoint
            # Lease-expiry cleanup stops local peers but never republishes private host clipboard
            # while the coordinator has not confirmed guest shutdown.
            Write-RunJson "$OutputRoot\stopped.json" @{
                RunId = $script:config.RunId; PeersStopped = $true; ClipboardRestored = $false
            }
        }
        catch {
            Write-RunJson "$OutputRoot\cleanup-failed.json" @{ RunId = $script:config.RunId; Error = $_.Exception.Message }
        }
    }
    if ($script:receiver) { $script:receiver.Dispose() }
    Write-RunJson "$OutputRoot\worker-exited.json" @{ RunId = $script:config.RunId; Cleaned = $script:cleaned }
}

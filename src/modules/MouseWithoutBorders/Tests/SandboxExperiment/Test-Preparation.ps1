# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

<#
.SYNOPSIS
Non-launch checks using only the PowerShell parser and isolated file fixtures.
.DESCRIPTION
Run from the source checkout, preferably with Windows PowerShell 5.1. Never starts
PowerToys/Sandbox, invokes UI, or reads/writes live PowerToys settings.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\Experiment.Common.ps1"
$script:checks = 0
function Assert-Check {
    param([bool]$Condition, [string]$Label)
    if (-not $Condition) { throw "FAIL: $Label" }
    $script:checks++
}
function Assert-Throws {
    param([scriptblock]$Action, [string]$Pattern)
    try { & $Action; throw 'DID NOT THROW' }
    catch { Assert-Check ($_.ToString() -match $Pattern) $Pattern }
}

$scripts = @(Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.ps1') +
    @(Get-ChildItem -LiteralPath "$PSScriptRoot\..\..\MouseWithoutBorders.UITests\Payload" -Filter '*.ps1')
foreach ($file in $scripts) {
    $tokens = $null
    $parseErrors = $null
    $null = [Management.Automation.Language.Parser]::ParseFile($file.FullName, [ref]$tokens, [ref]$parseErrors)
    Assert-Check ($parseErrors.Count -eq 0) "Parse $($file.Name): $parseErrors"
}
$fixture = Join-Path $PSScriptRoot ('.preparation-check-' + [Guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path "$fixture\settings\MouseWithoutBorders", "$fixture\endpoint", "$fixture\payload"
try {
    $moduleSettings = New-MwbExperimentSettings 'PEER' '192.0.2.1'
    foreach ($name in @('UseService', 'ShowOriginalUI', 'WrapMouse', 'ShareClipboard', 'TransferFile')) {
        Assert-Check ($moduleSettings.properties.$name.value -ceq $false) "MWB $name uses the BoolProperty value wrapper"
    }
    Assert-Check ($moduleSettings.properties.Name2IP.value -eq 'PEER 192.0.2.1') 'MWB name mapping is retained in the typed settings schema'
    & {
        . "$PSScriptRoot\..\..\MouseWithoutBorders.UITests\Payload\EndpointSupport.ps1"
        $script:settingsRoot = "$fixture\mapping-settings"
        $OutputRoot = "$fixture\endpoint"
        $script:expectedPeerMapping = 'PEER 192.0.2.1'
        $null = New-Item -ItemType Directory -Path "$script:settingsRoot\MouseWithoutBorders"
        $path = "$script:settingsRoot\MouseWithoutBorders\settings.json"
        Write-RunJson $path $moduleSettings
        $result = Assert-PeerMapping 'seeded'
        Assert-Check $result.MatchesExpected 'autonomous endpoint verifies its persisted peer mapping'
        Assert-Check ((Read-RunJson "$OutputRoot\peer-mapping-seeded.json").Actual -ceq
            $script:expectedPeerMapping) 'mapping evidence records the exact inner-network destination'
        $moduleSettings.properties.Name2IP.value = 'PEER 192.0.2.2'
        Write-RunJson $path $moduleSettings
        Assert-Throws { Assert-PeerMapping 'before-connect' } 'Peer mapping changed at before-connect'
        Assert-Check (-not (Read-RunJson "$OutputRoot\peer-mapping-before-connect.json").MatchesExpected) 'mismatched mapping evidence survives the failure'
        $moduleSettings.properties.Name2IP.value = ''
        Write-RunJson $path $moduleSettings
        Assert-Throws { Assert-PeerMapping 'before-connect' } 'Peer mapping changed at before-connect'
        $window = [pscustomobject]@{ Title = 'WinUI Desktop'; Visible = $true; HasOwner = $false }
        Assert-Check (-not (Test-SettingsWindowReady $true @($window))) 'WinUI placeholder is not Settings readiness'
        foreach ($title in @('PowerToys Settings', 'Administrator: PowerToys Settings')) {
            $window.Title = $title
            Assert-Check (Test-SettingsWindowReady $true @($window)) 'responsive initialized Settings is ready'
            Assert-Check (-not (Test-SettingsWindowReady $false @($window))) 'unresponsive Settings is not ready'
        }
        Assert-Check (-not (Test-SettingsWindowReady $true @())) 'absent Settings is not ready'
        Assert-Check (-not (Test-SettingsWindowReady $true @($window, $window))) 'ambiguous Settings windows are not ready'
        $window.Visible = $false
        Assert-Check (-not (Test-SettingsWindowReady $true @($window))) 'hidden Settings is not ready'
        $window.Visible = $true
        $window.HasOwner = $true
        Assert-Check (-not (Test-SettingsWindowReady $true @($window))) 'owned Settings window is not the main surface'
        $launcher = @{ ProcessId = 10; Responding = $true; Windows = @(
            @{ Title = 'WinUI Desktop'; Visible = $true; HasOwner = $false }
        ) }
        $windowOwner = @{ ProcessId = 20; Responding = $true; Windows = @(
            @{ Title = 'PowerToys Settings'; Visible = $true; HasOwner = $false }
        ) }
        $ready = @(Get-ReadySettingsCandidates @($launcher, $windowOwner))
        Assert-Check ($ready.Count -eq 1 -and $ready[0].ProcessId -eq 20) 'a surviving launcher does not hide the initialized window owner'
        Assert-Check (@(Get-ReadySettingsCandidates @($windowOwner, $windowOwner)).Count -eq 2) 'multiple initialized window owners remain ambiguous'
        $script:config = @{ RunId = [Guid]::NewGuid().ToString(); Role = 'Guest' }
        $script:bootstrapStageNumber = 0
        $script:bootstrapWatch = [Diagnostics.Stopwatch]::StartNew()
        $null = New-Item -ItemType Directory -Path "$fixture\archive-input", "$fixture\archive-output"
        [IO.File]::WriteAllText("$fixture\archive-input\sample.txt", 'runtime fixture')
        & tar.exe -a -cf "$fixture\runtime.zip" -C "$fixture\archive-input" .
        if ($LASTEXITCODE -ne 0) { throw 'Could not create the isolated runtime fixture.' }
        $archiveHash = (Get-FileHash "$fixture\runtime.zip").Hash
        $copy = Copy-GuestRuntimeArchive "$fixture\runtime.zip" "$fixture\local-runtime.zip" $archiveHash
        Assert-Check ($copy.Sha256 -ceq $archiveHash) 'buffered local archive copy matches protected source hash'
        Assert-Check ((Get-FileHash "$fixture\local-runtime.zip").Hash -ceq $archiveHash) 'buffered archive copy preserves exact bytes'
        Assert-Throws { Copy-GuestRuntimeArchive "$fixture\runtime.zip" "$fixture\bad-runtime.zip" ('0' * 64) } 'archive hash differs'
        Expand-GuestRuntime "$fixture\local-runtime.zip" "$fixture\archive-output" ([DateTime]::UtcNow.AddSeconds(30))
        Assert-Check ([IO.File]::ReadAllText("$fixture\archive-output\sample.txt") -ceq 'runtime fixture') 'instrumented extraction preserves archive contents'
        Assert-Check ((Read-RunJson "$OutputRoot\bootstrap-001.json").Stage -ceq 'RuntimeExtracted') 'extraction completion has a distinct timing marker'
        foreach ($bufferBytes in @(8KB, 4MB)) {
            $read = Measure-RuntimeRead "$fixture\archive-output\sample.txt" $bufferBytes
            Assert-Check ($read.BytesRead -eq (Get-Item "$fixture\archive-output\sample.txt").Length) 'runtime read measurement uses explicit PS5-compatible stream overloads'
        }
        $leaseRoot = "$fixture\leases"
        $null = New-Item -ItemType Directory -Path $leaseRoot
        foreach ($sequence in 1..400) {
            Write-RunJson (Join-Path $leaseRoot ('{0:D8}.json' -f $sequence)) @{
                RunId = $script:config.RunId; Sequence = $sequence; TimestampUtc = [DateTime]::UtcNow.ToString('o')
            }
        }
        [IO.File]::WriteAllText("$leaseRoot\00000001.json", 'obsolete content must not be replayed')
        $latest = Read-LatestEndpointLease $leaseRoot $script:config.RunId 0
        Assert-Check ($latest.Sequence -eq 400) 'bootstrap consumes only the latest committed liveness snapshot'
        Assert-Check ($null -eq (Read-LatestEndpointLease $leaseRoot $script:config.RunId 400)) 'unchanged lease generation is not reread'
        [IO.File]::WriteAllText("$leaseRoot\00000401.json", '{}')
        Assert-Check ((Read-LatestEndpointLease $leaseRoot $script:config.RunId 0).Sequence -eq 400) 'uncommitted newest lease is ignored'
        Write-RunJson "$leaseRoot\00000401.json" @{ RunId = 'wrong-run'; Sequence = 401 }
        Assert-Throws { Read-LatestEndpointLease $leaseRoot $script:config.RunId 400 } 'Lease generation correlation mismatch'
        Write-RunJson "$leaseRoot\00000401.json" @{ RunId = $script:config.RunId; Sequence = 400 }
        Assert-Throws { Read-LatestEndpointLease $leaseRoot $script:config.RunId 400 } 'Lease generation correlation mismatch'
        $refreshRoot = "$fixture\lease-refresh"
        $null = New-Item -ItemType Directory -Path $refreshRoot
        $expired = @{ RunId = $script:config.RunId; Sequence = 1; TimestampUtc = [DateTime]::UtcNow.AddMinutes(-2).ToString('o') }
        Write-RunJson "$refreshRoot\00000001.json" $expired
        Assert-Throws { Confirm-EndpointLease $refreshRoot $script:config.RunId $null } 'No initial host lease'
        Assert-Throws { Confirm-EndpointLease $refreshRoot 'another-run' $expired } 'Lease generation correlation mismatch'
        Assert-Throws { Confirm-EndpointLease $refreshRoot $script:config.RunId $expired } 'Host lease expired'
        $fresh = @{ RunId = $script:config.RunId; Sequence = 2; TimestampUtc = [DateTime]::UtcNow.ToString('o') }
        [IO.File]::WriteAllText("$refreshRoot\00000002.json", ($fresh | ConvertTo-Json))
        Assert-Throws { Confirm-EndpointLease $refreshRoot $script:config.RunId $expired } 'Host lease expired'
        Write-RunJson "$refreshRoot\00000002.json" $fresh
        $confirmed = Confirm-EndpointLease $refreshRoot $script:config.RunId $expired
        Assert-Check ($confirmed.Sequence -eq 2) 'an aged observation is refreshed to the latest committed lease'
        Assert-Check (([DateTime]$confirmed.TimestampUtc).ToUniversalTime().Ticks -eq
            ([DateTime]$fresh.TimestampUtc).ToUniversalTime().Ticks) 'refresh preserves publication time rather than extending the watchdog'
        Assert-Check ((Confirm-EndpointLease "$fixture\not-enumerated" $script:config.RunId $fresh).Sequence -eq 2) 'fresh observations do not need a second enumeration'
        $fresh.TimestampUtc = $expired.TimestampUtc
        Write-RunJson "$refreshRoot\00000002.json" $fresh
        Assert-Throws { Confirm-EndpointLease $refreshRoot $script:config.RunId $expired } 'Host lease expired'
        $fresh.RunId = 'another-run'
        Write-RunJson "$refreshRoot\00000002.json" $fresh
        Assert-Throws { Confirm-EndpointLease $refreshRoot $script:config.RunId $expired } 'Lease generation correlation mismatch'
        $network = Get-EndpointNetwork
        $loopback = @($network.Addresses | Where-Object IPAddress -eq '127.0.0.1')
        Assert-Check ($loopback.Count -eq 1 -and $loopback[0].PrefixLength -eq 8 -and
            $loopback[0].InterfaceIndex -gt 0) 'managed adapter enumeration retains IPv4 prefix and interface identity'
    }
    Assert-Throws { & "$PSScriptRoot\Prepare-Experiment.ps1" -Destination "$fixture\forbidden" } 'outside the source checkout'
    Assert-Check (-not (Test-Path "$fixture\forbidden")) 'prepare refusal creates nothing'
    Assert-Throws { & "$PSScriptRoot\Start-Experiment.ps1" -Destination "$fixture\missing" } 'explicit -AllowNonConsole'
    $globalBytes = [byte[]](239, 187, 191, 123, 125, 13, 10)
    $moduleBytes = [byte[]](255, 254, 123, 0, 125, 0)
    [IO.File]::WriteAllBytes("$fixture\settings\settings.json", $globalBytes)
    [IO.File]::WriteAllBytes("$fixture\settings\MouseWithoutBorders\settings.json", $moduleBytes)
    Backup-EndpointSettings "$fixture\endpoint" "$fixture\settings"
    foreach ($relative in @('settings.json', 'oobe_settings.json', 'MouseWithoutBorders\settings.json')) {
        Write-ExperimentJson "$fixture\settings\$relative" @{ Changed = $true }
        $written = [IO.File]::ReadAllBytes("$fixture\settings\$relative")
        Assert-Check (-not ($written[0] -eq 239 -and $written[1] -eq 187 -and $written[2] -eq 191)) 'UTF8 without BOM'
    }
    Write-ExperimentJson "$fixture\settings\settings.json" @{ Changed = 'again' }
    Assert-Check ((Read-ExperimentJson "$fixture\settings\settings.json").Changed -eq 'again') 'shared-file rewrite'
    $held = [IO.File]::Open("$fixture\settings\settings.json", [IO.FileMode]::Open, [IO.FileAccess]::Read,
        [IO.FileShare]::ReadWrite)
    try {
        Write-ExperimentJson "$fixture\settings\settings.json" @{ Changed = 'while-mapped' }
        Assert-Check ((Read-ExperimentJson "$fixture\settings\settings.json").Changed -eq 'while-mapped') 'mapped reader without delete sharing'
    }
    finally { $held.Dispose() }
    Assert-Check ((ConvertTo-ExperimentArguments @('connect', '--id', 'sample')) -eq
        '"connect" "--id" "sample"') 'native arguments quoted individually'
    Assert-Check ((ConvertTo-ExperimentArguments @('C:\with space\', 'a"b')) -eq
        '"C:\with space\\" "a\"b"') 'native quotes and trailing backslashes preserved'
    Restore-EndpointSettings "$fixture\endpoint"
    Assert-Check ([Convert]::ToBase64String([IO.File]::ReadAllBytes("$fixture\settings\settings.json")) -eq
        [Convert]::ToBase64String($globalBytes)) 'global restored byte-for-byte'
    Assert-Check ([Convert]::ToBase64String([IO.File]::ReadAllBytes("$fixture\settings\MouseWithoutBorders\settings.json")) -eq
        [Convert]::ToBase64String($moduleBytes)) 'MWB restored byte-for-byte'
    Assert-Check (-not (Test-Path "$fixture\settings\oobe_settings.json")) 'prior absence restored'
    Restore-EndpointSettings "$fixture\endpoint"
    [IO.File]::WriteAllText("$fixture\endpoint\backup\0.bin", 'corrupt')
    Assert-Throws { Restore-EndpointSettings "$fixture\endpoint" } 'Backup integrity failure'
    Assert-Check ([Convert]::ToBase64String([IO.File]::ReadAllBytes("$fixture\settings\settings.json")) -eq
        [Convert]::ToBase64String($globalBytes)) 'corrupt backup did not overwrite target'
    [IO.File]::WriteAllText("$fixture\payload\binary.bin", 'original')
    $manifest = [pscustomobject]@{ Files = @([pscustomobject]@{
        Kind = 'Product'; RelativePath = 'binary.bin'; Sha256 = (Get-FileHash "$fixture\payload\binary.bin").Hash
    }) }
    Assert-ExperimentPayload $manifest "$fixture\payload" "$fixture\payload"
    [IO.File]::WriteAllText("$fixture\payload\binary.bin", 'stale')
    Assert-Throws { Assert-ExperimentPayload $manifest "$fixture\payload" "$fixture\payload" } 'Payload changed or is missing'
    Assert-Check ((ConvertTo-MwbMachineName 'SHORT-HOST') -eq 'SHORT-HOST') 'short DNS name preserved'
    $longName = '12345678-1234-1234-1234-123456789012'
    Assert-Check ((ConvertTo-MwbMachineName $longName) -eq $longName.Substring(0, 32)) 'Sandbox name follows MWB 32-character limit'
    Assert-Throws { ConvertTo-MwbMachineName '' } 'DNS host name is required'
    $id = [Guid]::NewGuid().ToString()
    foreach ($entry in @($id, [pscustomobject]@{ Id = $id }, [pscustomobject]@{ SandboxId = $id })) {
        Assert-Check ((Get-ExperimentSandboxIds ([pscustomobject]@{ WindowsSandboxEnvironments = @($entry) })) -eq $id) 'Sandbox GUID schema'
    }
    Assert-Throws { Get-ExperimentSandboxIds ([pscustomobject]@{
        WindowsSandboxEnvironments = @([pscustomobject]@{ Unknown = $id })
    }) } 'Unrecognized wsb list schema'
    $processRecord = Get-ExperimentProcessRecord (Get-Process -Id $PID)
    Assert-Check (Test-ExperimentProcess $processRecord) 'process identity matches'
    Write-ExperimentJson "$fixture\process-record.json" $processRecord
    $processRecord = Read-ExperimentJson "$fixture\process-record.json"
    Assert-Check (Test-ExperimentProcess $processRecord) 'serialized process timestamp retains ownership'
    $childStart = New-Object Diagnostics.ProcessStartInfo
    $childStart.FileName = (Get-Process -Id $PID).MainModule.FileName
    $childStart.Arguments = '-NoLogo -NoProfile -NonInteractive -Command "Start-Sleep -Seconds 4"'
    $childStart.UseShellExecute = $false
    $childStart.CreateNoWindow = $true
    $child = [Diagnostics.Process]::Start($childStart)
    try {
        $childRecord = Get-ExperimentProcessRecord $child
        Assert-Check ($childRecord.Id -eq $child.Id -and $childRecord.Path -ieq $childStart.FileName) 'newly launched process identity becomes ready'
        $trackingRoot = Join-Path $fixture 'tracking'
        $null = New-Item -ItemType Directory -Path $trackingRoot
        Write-ExperimentJson "$trackingRoot\processes.json" @{
            ProductRoot = Split-Path $childStart.FileName
            Processes = @($processRecord)
        }
        Update-EndpointProcesses $trackingRoot
        $tracked = Read-ExperimentJson "$trackingRoot\processes.json"
        Assert-Check ($child.Id -in @($tracked.Processes | ForEach-Object Id)) 'child discovery normalizes local and UTC timestamps'
        $child.WaitForExit()
    }
    finally { $child.Dispose() }
    $processRecord.StartTimeUtc = [DateTime]::UtcNow.AddYears(-1).ToString('o')
    Assert-Check (-not (Test-ExperimentProcess $processRecord)) 'reused PID timestamp refused'
    function Test-ExperimentProcess { param($Record) return $true }
    $readinessRoot = Join-Path $fixture 'readiness'
    $null = New-Item -ItemType Directory -Path $readinessRoot
    Write-ExperimentJson "$readinessRoot\processes.json" @{ Processes = @(
        @{ Id = 1; Path = 'C:\Fixture\PowerToys.exe' }
    ) }
    Assert-Check ($null -eq (Get-EndpointSettingsProcess $readinessRoot -AllowStarting)) 'startup tolerates missing children only'
    Assert-Throws { Get-EndpointSettingsProcess $readinessRoot } 'have not both started'
    Write-ExperimentJson "$readinessRoot\processes.json" @{ Processes = @() }
    Assert-Throws { Get-EndpointSettingsProcess $readinessRoot -AllowStarting } 'Missing Runner'
    Write-ExperimentJson "$readinessRoot\processes.json" @{ Processes = @(
        @{ Id = 1; Path = 'C:\Fixture\PowerToys.exe' },
        @{ Id = 2; Path = 'C:\Fixture\PowerToys.MouseWithoutBorders.exe' },
        @{ Id = 3; Path = 'C:\Fixture\PowerToys.Settings.exe' }
    ) }
    Assert-Check ((Get-EndpointSettingsProcess $readinessRoot) -eq 3) 'complete endpoint selects recorded Settings PID'
    function Get-ExperimentDesktop { $script:desktop }
    $script:desktop = [pscustomobject]@{
        IsSystem = $false; SessionId = 5; ConsoleSessionId = 1
        WtsState = 0; InputDesktop = 'Default'; ForegroundHwnd = 123; InputAvailable = $true
    }
    Assert-Throws { Assert-ExperimentDesktop } 'explicit -AllowNonConsole'
    $null = Assert-ExperimentDesktop -AllowNonConsole
    $null = Wait-ExperimentDesktop -DeadlineUtc ([DateTime]::UtcNow.AddSeconds(1)) -AllowNonConsole
    $script:desktop.ForegroundHwnd = 0
    Assert-Check ((Assert-ExperimentDesktop -AllowNonConsole).InputAvailable) 'window focus transition does not invalidate an active input desktop'
    foreach ($field in @('IsSystem', 'SessionId', 'WtsState', 'InputDesktop', 'InputAvailable')) {
        $before = $script:desktop.$field
        $script:desktop.$field = switch ($field) {
            IsSystem { $true }
            SessionId { 0 }
            WtsState { 4 }
            InputDesktop { 'Winlogon' }
            InputAvailable { $false }
        }
        Assert-Throws { Assert-ExperimentDesktop -AllowNonConsole } 'active, unlocked default input desktop'
        $script:desktop.$field = $before
    }
    Write-Host "PASS: $($scripts.Count) scripts parsed by PowerShell $($PSVersionTable.PSVersion); $script:checks non-launch checks."
}
finally { Remove-Item -LiteralPath $fixture -Recurse -Force }

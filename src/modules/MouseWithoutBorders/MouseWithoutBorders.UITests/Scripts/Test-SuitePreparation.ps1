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
Set-StrictMode -Version 2.0
$payloadRoot = Join-Path $PSScriptRoot '..\Payload'
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
    @(Get-ChildItem -LiteralPath $payloadRoot -Filter '*.ps1')
foreach ($file in $scripts) {
    $tokens = $null
    $parseErrors = $null
    $null = [Management.Automation.Language.Parser]::ParseFile($file.FullName, [ref]$tokens, [ref]$parseErrors)
    Assert-Check ($parseErrors.Count -eq 0) "Parse $($file.Name): $parseErrors"
}
$fixture = Join-Path $PSScriptRoot ('.preparation-check-' + [Guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path "$fixture\settings\MouseWithoutBorders", "$fixture\endpoint", "$fixture\payload"
try {
    $moduleSettings = Get-Content -LiteralPath (Join-Path $payloadRoot 'MwbSettings.template.json') -Raw | ConvertFrom-Json
    $moduleSettings.properties.Name2IP.value = 'PEER 192.0.2.1'
    foreach ($name in @('UseService', 'ShowOriginalUI', 'WrapMouse', 'ShareClipboard', 'TransferFile')) {
        Assert-Check ($moduleSettings.properties.$name.value -ceq $false) "MWB $name uses the BoolProperty value wrapper"
    }
    Assert-Check ($moduleSettings.properties.Name2IP.value -eq 'PEER 192.0.2.1') 'MWB name mapping is retained in the typed settings schema'
    & {
        Add-Type -Path (Join-Path $payloadRoot 'NativeSupport.cs') -ErrorAction Stop
        $closeQuestion = 'Are you sure you want to close Windows Sandbox? Once Windows Sandbox is closed all of its content will be discarded and permanently lost.'
        Assert-Check ([Microsoft.MouseWithoutBorders.UITests.NativeSupport]::IsSandboxCloseConfirmation($closeQuestion)) 'only the exact owned Sandbox destruction prompt is accepted'
        foreach ($text in @('', 'Windows Sandbox could not start.', 'Close Windows Sandbox',
            'Are you sure you want to close this window?', 'Are you sure you want to close Windows Sandbox?')) {
            Assert-Check (-not [Microsoft.MouseWithoutBorders.UITests.NativeSupport]::IsSandboxCloseConfirmation($text)) 'an unrelated dialog is never confirmed'
        }
        . (Join-Path $payloadRoot 'EndpointSupport.ps1')
        . (Join-Path $payloadRoot 'ModernSandboxRecovery.ps1')
        $birth = [DateTime]::Parse('2026-10-05T23:24:22.6314321Z',
            [Globalization.CultureInfo]::InvariantCulture, [Globalization.DateTimeStyles]::RoundtripKind)
        $processRecord = Convert-EndpointProcessRecord ([pscustomobject]@{
            Id = 1; ParentId = 2; SessionId = 3; Path = 'C:\owned.exe'; StartTimeUtc = $birth
            CommandLine = 'must never be exported'
        })
        Assert-Check ($processRecord.Count -eq 5 -and -not $processRecord.Contains('CommandLine')) 'endpoint journals contain only whitelisted process identity fields'
        Assert-Check ($processRecord.StartTimeUtc -ceq $birth.ToString('o')) 'process identity preserves submillisecond birth-time precision in PS5 JSON'
        $roundtrip = ($processRecord | ConvertTo-Json | ConvertFrom-Json).StartTimeUtc
        Assert-Check (([DateTime]$roundtrip).ToUniversalTime().Ticks -eq $birth.Ticks) 'recovery can revalidate the exact original process birth time'
        $recoveryRunId = [guid]::NewGuid().ToString()
        $control = "$fixture\control\$recoveryRunId"
        $provisioning = [pscustomobject]@{
            RunId = $recoveryRunId; SandboxBackend = 'WinApp'
            SandboxWinAppPath = "$fixture\preview\winapp.exe"; SandboxWinAppSha256 = ('A' * 64)
        }
        $modern = [pscustomobject]@{
            Backend = 'WinApp'; InstanceId = $recoveryRunId; CreationAttempted = $true; CreationConfirmed = $true
            WinAppPath = $provisioning.SandboxWinAppPath; WinAppSha256 = $provisioning.SandboxWinAppSha256
            TargetStateRoot = "$control\winapp-target"
        }
        $identity = Get-MwbModernRecoveryIdentity $modern $provisioning $control
        Assert-Check ($identity.InstanceId -eq [guid]$recoveryRunId) 'modern recovery derives the owned ID from protected provisioning'
        $modern.InstanceId = [guid]::NewGuid().ToString()
        Assert-Throws { Get-MwbModernRecoveryIdentity $modern $provisioning $control } 'protected run identity'
        $modern.InstanceId = $recoveryRunId
        $modern.TargetStateRoot = "$fixture\unrelated"
        Assert-Throws { Get-MwbModernRecoveryIdentity $modern $provisioning $control } 'private state identity changed'
        $profileState = "$fixture\userprofile\.winapp\state\targets"
        $null = New-Item -ItemType Directory -Path $profileState
        [IO.File]::WriteAllText("$profileState\unrelated-deployment.json", 'unowned')
        $modern.TargetStateRoot = $profileState
        Assert-Throws { Get-MwbModernRecoveryIdentity $modern $provisioning $control } 'private state identity changed'
        $modern.TargetStateRoot = "$control\winapp-target"
        $modern.WinAppPath = "$fixture\untrusted\winapp.exe"
        Assert-Throws { Get-MwbModernRecoveryIdentity $modern $provisioning $control } 'private state identity changed'
        $modern.WinAppPath = $provisioning.SandboxWinAppPath
        $modern.CreationAttempted = 'true'
        Assert-Throws { Get-MwbModernRecoveryIdentity $modern $provisioning $control } 'creation evidence is malformed'
        Assert-Check (@(Get-MwbModernInstanceIds ([pscustomobject]@{ WindowsSandboxEnvironments = @() })).Count -eq 0) 'an empty modern provider inventory is valid'
        Assert-Check (@(Get-MwbModernInstanceIds ([pscustomobject]@{
            WindowsSandboxEnvironments = @([pscustomobject]@{ Id = $recoveryRunId })
        }))[0] -eq [guid]$recoveryRunId) 'modern provider inventory preserves exact identity'
        Assert-Throws { Get-MwbModernInstanceIds ([pscustomobject]@{}) } 'expected instance array'
        Assert-Throws { Get-MwbModernInstanceIds ([pscustomobject]@{
            WindowsSandboxEnvironments = @([pscustomobject]@{ Id = $recoveryRunId }, [pscustomobject]@{ Id = $recoveryRunId })
        }) } 'duplicate identities'
        $null = New-Item -ItemType Directory -Path "$control\winapp-target"
        [IO.File]::WriteAllText("$control\winapp-target\fixture.txt", 'private fixture')
        Remove-MwbPrivateTargetState "$control\winapp-target"
        Assert-Check (-not (Test-Path -LiteralPath "$control\winapp-target")) 'only the explicitly scoped private target state is removed'
        Assert-Check ([IO.File]::ReadAllText("$profileState\unrelated-deployment.json") -ceq 'unowned') 'official default-profile state is never a recovery deletion target'
        $toolsSource = "$fixture\official-cli"
        $null = New-Item -ItemType Directory -Path $toolsSource
        foreach ($name in @('winapp.exe', 'libSkiaSharp.dll', 'libHarfBuzzSharp.dll', 'winapp.pdb')) {
            [IO.File]::WriteAllText((Join-Path $toolsSource $name), "fixture-$name")
        }
        $localCli = Copy-EndpointWinAppTools $toolsSource "$fixture\local-cli"
        Assert-Check ($localCli -ceq "$fixture\local-cli\winapp.exe") 'guest CLI resolves to the local staged executable'
        Assert-Check (@(Get-ChildItem "$fixture\local-cli" -File).Count -eq 3) 'guest CLI stages all three official runtime files without symbols'
        foreach ($name in @('winapp.exe', 'libSkiaSharp.dll', 'libHarfBuzzSharp.dll')) {
            Assert-Check ((Get-FileHash "$toolsSource\$name").Hash -ceq
                (Get-FileHash "$fixture\local-cli\$name").Hash) "guest CLI preserves $name"
        }
        Assert-Throws { Copy-EndpointWinAppTools $toolsSource "$fixture\local-cli" } 'destination must be new'
        Remove-Item "$toolsSource\libHarfBuzzSharp.dll"
        Assert-Throws { Copy-EndpointWinAppTools $toolsSource "$fixture\incomplete-cli" } 'missing libHarfBuzzSharp.dll'
        Assert-Check (-not (Test-Path "$fixture\incomplete-cli")) 'incomplete official runtime fails before creating a guest destination'
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
        & {
            $script:connectActions = [Collections.Generic.List[string]]::new()
            function Wait-GuestMwbControl {
                param([string]$AutomationId)
                $script:connectActions.Add("wait:$AutomationId")
            }
            function Invoke-GuestUi {
                param([string[]]$Arguments)
                $script:connectActions.Add("$($Arguments[0]):$($Arguments[1])")
            }
            function Assert-PeerMapping { @{ MatchesExpected = $true } }
            function Get-GuestConnectAcknowledgement { @{ KeyMatches = $false; PeerConfigured = $false } }
            function Wait-GuestConnectAcknowledgement { @{ Acknowledged = $true } }
            function Write-RunJson { }
            $connected = Connect-Guest 'fixture-key' 'PEER'
            $expected = @(
                'wait:MouseWithoutBordersShowConnectFieldsButton',
                'click:MouseWithoutBordersShowConnectFieldsButton',
                'wait:ConnectSecurityKeyTextBox',
                'wait:ConnectPCNameTextBox',
                'set-value:ConnectSecurityKeyTextBox',
                'set-value:ConnectPCNameTextBox',
                'invoke:MouseWithoutBordersConnectButton'
            )
            Assert-Check (($script:connectActions -join '|') -ceq ($expected -join '|')) 'guest opens the ready input row with one click, waits for both fields, and invokes the real Connect command'
            Assert-Check ($connected.Submitted -and $connected.Acknowledged) 'guest connection still requires the persisted pairing acknowledgement'
        }
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
    Write-Host "PASS: $($scripts.Count) scripts parsed by PowerShell $($PSVersionTable.PSVersion); $script:checks non-launch checks."
}
finally { Remove-Item -LiteralPath $fixture -Recurse -Force }

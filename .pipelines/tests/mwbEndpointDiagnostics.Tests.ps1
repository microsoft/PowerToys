# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

$payloadRoot = Join-Path $PSScriptRoot '..\..\src\modules\MouseWithoutBorders\MouseWithoutBorders.UITests\Payload'
function Write-RunJson {
    param($Path, $Value)
    throw 'Unmocked diagnostic publication'
}

Describe 'MWB pairing requires persisted command acknowledgement' {
    BeforeAll {
        $tokens = $null
        $errors = $null
        $ast = [Management.Automation.Language.Parser]::ParseFile(
            (Join-Path $payloadRoot 'EndpointSupport.ps1'), [ref]$tokens, [ref]$errors)
        if ($errors.Count) { throw 'Endpoint support contains parse errors.' }
        foreach ($name in @('Read-RunJson', 'Get-GuestConnectAcknowledgement', 'Wait-GuestConnectAcknowledgement')) {
            $definition = $ast.Find({
                param($node)
                $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq $name
            }, $true)
            . ([scriptblock]::Create($definition.Extent.Text))
        }
    }

    BeforeEach {
        $script:settingsRoot = $TestDrive
        $OutputRoot = $TestDrive
        $script:pairingWrites = [Collections.Generic.List[object]]::new()
        Mock Write-RunJson { param($Path, $Value) $script:pairingWrites.Add($Value) }
        Mock Start-Sleep {}
    }

    It 'does not mistake running listeners or an unchanged key for accepted pairing' {
        Mock Read-RunJson {
            @{ properties = @{ SecurityKey = @{ value = 'old-fixture-key' }; MachineMatrixString = @('HOST') } }
        }
        $state = Get-GuestConnectAcknowledgement 'new-private-fixture-key' 'host'
        $state.KeyMatches | Should Be $false
        $state.PeerConfigured | Should Be $true
        $state.Acknowledged | Should Be $false
    }

    It 'requires both the requested key and exact peer identity' -TestCases @(
        @{ StoredKey = 'private-fixture-key'; Peer = 'HOST'; Expected = $true }
        @{ StoredKey = 'private-fixture-key'; Peer = 'HOST-other'; Expected = $false }
        @{ StoredKey = 'PRIVATE-FIXTURE-KEY'; Peer = 'HOST'; Expected = $false }
    ) {
        param($StoredKey, $Peer, $Expected)
        Mock Read-RunJson {
            @{ properties = @{ SecurityKey = @{ value = $StoredKey }; MachineMatrixString = @($Peer, 'GUEST', '', '') } }
        }
        (Get-GuestConnectAcknowledgement 'private-fixture-key' 'host').Acknowledged | Should Be $Expected
    }

    It 'waits for the real persisted transition without reinvoking Connect and publishes only booleans' {
        $script:pairingReads = 0
        Mock Read-RunJson {
            $script:pairingReads++
            @{
                properties = @{
                    SecurityKey = @{ value = $(if ($script:pairingReads -gt 1) { 'private-fixture-key' } else { 'old-fixture-key' }) }
                    MachineMatrixString = @('HOST', 'GUEST')
                }
            }
        }
        (Wait-GuestConnectAcknowledgement 'private-fixture-key' 'host').Acknowledged | Should Be $true
        $script:pairingWrites.Count | Should Be 2
        $script:pairingWrites[0].Stage | Should Be 'WaitingForSettingsRpc'
        $script:pairingWrites[1].Stage | Should Be 'Acknowledged'
        ($script:pairingWrites | ConvertTo-Json) | Should Not Match 'private-fixture|old-fixture|HOST|GUEST'
    }

    It 'fails an unacknowledged invoke instead of reporting the pairing phase passed' {
        Mock Read-RunJson {
            @{ properties = @{ SecurityKey = @{ value = 'old-fixture-key' }; MachineMatrixString = @('GUEST') } }
        }
        { Wait-GuestConnectAcknowledgement 'private-fixture-key' 'host' -TimeoutSeconds 0 } |
            Should Throw 'did not persist the matching key and peer'
        $script:pairingWrites.Count | Should Be 2
        $script:pairingWrites[0].KeyMatches | Should Be $false
        $script:pairingWrites[0].PeerConfigured | Should Be $false
        $script:pairingWrites[1].Stage | Should Be 'NotAcknowledged'
    }
}

Describe 'MWB guest UI error envelopes' {
    BeforeAll {
        $tokens = $null
        $errors = $null
        $ast = [Management.Automation.Language.Parser]::ParseFile(
            (Join-Path $payloadRoot 'EndpointSupport.ps1'), [ref]$tokens, [ref]$errors)
        $definition = $ast.Find({
            param($node)
            $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Assert-GuestUiResult'
        }, $true)
        . ([scriptblock]::Create($definition.Extent.Text))
    }

    It 'accepts only an explicit zero-match search result for exit one' {
        { Assert-GuestUiResult 'search' 1 ([pscustomobject]@{ matchCount = 0; matches = @() }) } | Should Not Throw
        { Assert-GuestUiResult 'invoke' 0 ([pscustomobject]@{ success = $true }) } | Should Not Throw
        { Assert-GuestUiResult 'search' 1 ([pscustomobject]@{ matchCount = 1 }) } | Should Throw 'Guest winapp search failed'
        { Assert-GuestUiResult 'invoke' 1 ([pscustomobject]@{ matchCount = 0 }) } | Should Throw 'Guest winapp invoke failed'
        { Assert-GuestUiResult 'search' 1 $null } | Should Throw 'Guest winapp search failed'
    }

    It 'does not replace the native CLI failure with a missing-property error or expose raw contents' {
        Set-StrictMode -Version 2.0
        try {
            Assert-GuestUiResult 'search' 1 ([pscustomobject]@{ error = @{ message = 'private-key-do-not-export' } })
            throw 'Expected CLI failure'
        }
        catch {
            $_.Exception.Message | Should Match 'Guest winapp search failed \(exit 1;'
            $_.Exception.Message | Should Not Match 'private-key|matchCount|property.*cannot'
        }
        finally { Set-StrictMode -Off }
    }
}

Describe 'MWB bounded nonsecret guest-command stages' {
    BeforeAll {
        $tokens = $null
        $errors = $null
        $script:support = [Management.Automation.Language.Parser]::ParseFile(
            (Join-Path $payloadRoot 'EndpointSupport.ps1'), [ref]$tokens, [ref]$errors)
        if ($errors.Count) { throw ($errors | Out-String) }
        $definition = $script:support.Find({
            param($node)
            $node -is [Management.Automation.Language.FunctionDefinitionAst] -and
                $node.Name -eq 'Write-EndpointCommandStage'
        }, $true)
        . ([scriptblock]::Create($definition.Extent.Text))
        $script:ui = $script:support.Find({
            param($node)
            $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Invoke-GuestUi'
        }, $true).Extent.Text
        $script:worker = Get-Content (Join-Path $payloadRoot 'EndpointWorker.ps1') -Raw
    }

    BeforeEach {
        $script:config = @{ Role = 'Guest'; RunId = '00112233-4455-6677-8899-aabbccddeeff' }
        $script:commandStageNumber = 0
        $OutputRoot = $TestDrive
        $global:MwbStageCaptures = [Collections.Generic.List[object]]::new()
        Mock Write-RunJson {
            param($Path, $Value)
            $global:MwbStageCaptures.Add([pscustomobject]@{ Path = $Path; Value = $Value })
        }
    }

    It 'publishes only fixed metadata fields to one overwritten snapshot' {
        Write-EndpointCommandStage 'UiProcessStarting' 'set-value' 12
        Write-EndpointCommandStage 'UiProcessStarted' 'set-value' 12 4321 25
        $global:MwbStageCaptures.Count | Should Be 2
        @($global:MwbStageCaptures.Path | Select-Object -Unique).Count | Should Be 1
        $global:MwbStageCaptures[1].Path | Should Be (Join-Path $TestDrive 'command-stage.json')
        $value = $global:MwbStageCaptures[1].Value
        $expectedKeys = (
            @('RunId', 'TimestampUtc', 'StageNumber', 'Stage', 'Operation', 'RequestSequence',
                'WorkerProcessId', 'ChildProcessId', 'ElapsedMilliseconds') | Sort-Object) -join ','
        (($value.Keys | Sort-Object) -join ',') | Should Be $expectedKeys
        $value.StageNumber | Should Be 2
        $value.RequestSequence | Should Be 12
        $value.ChildProcessId | Should Be 4321
        $value.ElapsedMilliseconds | Should Be 25
    }

    It 'never publishes unrecognized operation or stage text' {
        $secret = 'do-not-export-this-argument-' + ('x' * 10000)
        Write-EndpointCommandStage $secret $secret 2
        $value = $global:MwbStageCaptures[0].Value
        $value.Stage | Should Be 'Unknown'
        $value.Operation | Should Be 'Unknown'
        $json = $value | ConvertTo-Json
        $json | Should Not Match 'do-not-export'
        ($json.Length -lt 1024) | Should Be $true
    }

    It 'records a Connect action without accessing its key or other request fields' {
        $request = @{ Action = 'Connect'; Key = 'private-pairing-token'; PeerName = 'private-peer' }
        Write-EndpointCommandStage 'RequestDispatching' $request.Action 2
        $json = $global:MwbStageCaptures[0].Value | ConvertTo-Json
        $json | Should Match 'Connect'
        $json | Should Not Match 'private-pairing-token|private-peer|Arguments|Tree|Error'
    }

    It 'does not add diagnostic writes on the host endpoint' {
        $script:config.Role = 'Host'
        Write-EndpointCommandStage 'RequestDispatching' 'Connect' 2
        $global:MwbStageCaptures.Count | Should Be 0
        $script:commandStageNumber | Should Be 0
    }

    It 'does not echo an invalid correlation identifier' {
        $script:config.RunId = 'private-invalid-identifier'
        Write-EndpointCommandStage 'RequestDispatching' 'Connect' 2
        $global:MwbStageCaptures.Count | Should Be 0
    }

    It 'does not replace a command result with a diagnostic publication error' {
        Mock Write-RunJson { throw 'private diagnostic failure text' }
        $output = @(Write-EndpointCommandStage 'RequestDispatching' 'Connect' 2)
        $output.Count | Should Be 0
    }

    It 'brackets native UI process creation without recording the argument list' {
        $before = $script:ui.IndexOf("Write-EndpointCommandStage 'UiProcessStarting'")
        $launch = $script:ui.IndexOf('[Diagnostics.Process]::Start($start)')
        $after = $script:ui.IndexOf("Write-EndpointCommandStage 'UiProcessStarted'")
        ($before -ge 0 -and $before -lt $launch -and $launch -lt $after) | Should Be $true
        $calls = @($script:support.FindAll({
            param($node)
            $node -is [Management.Automation.Language.CommandAst] -and
                $node.GetCommandName() -eq 'Write-EndpointCommandStage'
        }, $true))
        foreach ($call in $calls) {
            $call.Extent.Text | Should Not Match '\$allArgs|\$start\.Arguments|\$Key|\$raw|\$data'
        }
        $script:ui | Should Match 'WaitForExit\(90000\)'
        $script:ui | Should Match 'WaitAll\([\s\S]*5000\)'
    }

    It 'does not introduce stage writes in the idle request polling path' {
        $marker = $script:worker.IndexOf('if (-not (Test-Path -LiteralPath "$requestPath.ready"))')
        $readStage = $script:worker.IndexOf("Write-EndpointCommandStage 'RequestReadStarting'")
        $read = $script:worker.IndexOf('$request = Read-RunJson $requestPath')
        $dispatch = $script:worker.IndexOf("Write-EndpointCommandStage 'RequestDispatching'")
        ($marker -ge 0 -and $marker -lt $readStage -and $readStage -lt $read -and $read -lt $dispatch) | Should Be $true
        $script:worker.Substring(0, $marker) | Should Not Match 'Write-EndpointCommandStage'
        $script:worker | Should Match 'NextRequestSequence = \$script:requestNumber \+ 1'
        $script:worker | Should Match "Stage = 'BeforeRequestProbe'"
    }

    It 'marks reply publication and initializes its bounded sequence counter' {
        $script:worker | Should Match '\$script:commandStageNumber = 0'
        $before = $script:worker.IndexOf("Write-EndpointCommandStage 'ResponsePublishing'")
        $publication = $script:worker.IndexOf('Write-RunJson (Join-Path $OutputRoot ("{0:D4}.json" -f $next))')
        $after = $script:worker.IndexOf("Write-EndpointCommandStage 'ResponsePublished'")
        ($before -ge 0 -and $before -lt $publication -and $publication -lt $after) | Should Be $true
    }

    It 'observes native mouse routing without injecting or reporting window text' {
        $native = Get-Content (Join-Path $payloadRoot 'NativeSupport.cs') -Raw
        $method = [regex]::Match($native, '(?s)public static MouseTargetState MouseTarget\(.*?\n        \}').Value
        $method | Should Match 'WindowFromPoint'
        $method | Should Match 'GetAncestor'
        $method | Should Match 'GetGUIThreadInfo'
        $method | Should Not Match 'GetWindowText|SendInput|PostMessage|SetCursor'
        $script:worker | Should Match 'MouseTarget\(\$point\.X, \$point\.Y\)'
        $script:worker | Should Match 'ClickTargetHwnd = \$script:receiver\.ClickTargetHandle'
    }

    It 'counts native button messages separately without changing the physical-click assertion' {
        $receiver = Get-Content (Join-Path $payloadRoot 'Receiver.cs') -Raw
        $receiver | Should Match 'message\.Msg == 0x0201'
        $receiver | Should Match 'message\.Msg == 0x0202'
        $receiver | Should Match 'base\.WndProc\(ref message\)'
        ([regex]::Matches($receiver, 'Clicks\+\+')).Count | Should Be 1
        $receiver | Should Match 'clickTarget\.MouseDown \+='
        $receiver | Should Match 'args\.Button == MouseButtons\.Left'
    }

    AfterAll {
        Remove-Variable MwbStageCaptures -Scope Global -ErrorAction SilentlyContinue
    }
}

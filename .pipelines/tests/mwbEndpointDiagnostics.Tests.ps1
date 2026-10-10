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
        foreach ($name in @('Assert-GuestUiResult', 'Get-GuestUiResultDiagnostic', 'Get-SettingsWindowHandle',
            'Get-InspectedGuestElement', 'Get-GuestSecurityKeyHeader', 'Get-GuestUiControlKind')) {
            $definition = $ast.Find({
                param($node)
                $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq $name
            }, $true)
            . ([scriptblock]::Create($definition.Extent.Text))
        }
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

    It 'preserves the released stderr error code without messages, selectors or UI values' {
        $errorData = '{"error":{"code":"stale_element","message":"private-key-do-not-export","selector":"private-selector","details":"private-details","hresult":"0x80040201"}}' |
            ConvertFrom-Json
        $result = Get-GuestUiResultDiagnostic 'search' 1 $null $errorData
        $result.ErrorEnvelopePresent | Should Be $true
        $result.ErrorCode | Should Be 'stale_element'
        $result.HResult | Should Be '0x80040201'
        $result.MatchCountPresent | Should Be $false
        ($result | ConvertTo-Json) | Should Not Match 'private-|message|selector|details'
    }

    It 'withholds unknown error-code text and malformed HRESULTs' {
        $errorData = '{"error":{"code":"private-key-do-not-export","hresult":"0x123-private-value"}}' | ConvertFrom-Json
        $result = Get-GuestUiResultDiagnostic 'set-value' 1 $null $errorData
        $result.ErrorCode | Should BeNullOrEmpty
        $result.HResult | Should BeNullOrEmpty
        ($result | ConvertTo-Json) | Should Not Match 'private-'
    }

    It 'distinguishes confirmed zero matches from a missing result without weakening the failure gate' {
        $result = Get-GuestUiResultDiagnostic 'search' 1 ([pscustomobject]@{ matchCount = 0 }) $null
        $result.MatchCountPresent | Should Be $true
        $result.MatchCount | Should Be 0
        $result.ErrorEnvelopePresent | Should Be $false
        { Assert-GuestUiResult 'search' 1 $null } | Should Throw 'Guest winapp search failed'
    }

    It 'diagnoses a successful empty inspection without treating it as a populated tree' {
        $data = '{"depth":4,"windows":[],"private-field":"private-key"}' | ConvertFrom-Json
        $result = Get-GuestUiResultDiagnostic 'inspect' 0 $data $null
        $result.DataKind | Should Be 'Object'
        $result.WindowsPresent | Should Be $true
        $result.WindowCount | Should Be 0
        $result.RootElementCount | Should Be 0
        ($result.KnownProperties -contains 'depth') | Should Be $true
        ($result | ConvertTo-Json -Depth 5) | Should Not Match 'private-'
    }

    It 'records only counts for populated trees and action results, never control values' {
        $data = '{"windows":[{"elements":[{"value":"private-key"},{"value":"private-peer"}]}],"success":false}' | ConvertFrom-Json
        $result = Get-GuestUiResultDiagnostic 'inspect' 0 $data $null
        $result.WindowCount | Should Be 1
        $result.RootElementCount | Should Be 2
        $result.SuccessPresent | Should Be $true
        $result.Success | Should Be $false
        ($result | ConvertTo-Json -Depth 5) | Should Not Match 'private-'
    }

    It 'labels only fixed known actions without exporting selectors or option values' {
        Get-GuestUiControlKind @('set-value', 'ConnectSecurityKeyTextBox', 'private-key') | Should Be 'ConnectKeyField'
        Get-GuestUiControlKind @('inspect', 'private-selector') | Should Be 'Other'
        Get-GuestUiControlKind @('inspect') | Should Be 'Window'
        Get-GuestUiControlKind @('inspect', '-d', '5') | Should Be 'Window'
    }

    It 'uses exactly the visible ownerless initialized Settings HWND without UIA desktop discovery' {
        $windows = @(
            [pscustomobject]@{ Handle = 1234; Title = 'Administrator: PowerToys Settings'; Visible = $true; HasOwner = $false }
            [pscustomobject]@{ Handle = 1235; Title = 'Default IME'; Visible = $false; HasOwner = $true }
        )
        Get-SettingsWindowHandle $windows | Should Be 1234
    }

    It 'refuses ambiguous or not-yet-initialized Settings windows' -TestCases @(
        @{ WindowCount = 0 }
        @{ WindowCount = 2 }
    ) {
        param($WindowCount)
        $windows = @(
            for ($index = 0; $index -lt $WindowCount; $index++) {
                [pscustomobject]@{ Handle = 1234 + $index; Title = 'PowerToys Settings'; Visible = $true; HasOwner = $false }
            }
        )
        { Get-SettingsWindowHandle $windows } | Should Throw 'exactly one initialized'
    }

    It 'requires the direct inspect root to have the exact requested AutomationId' {
        Set-StrictMode -Version 2.0
        try {
            $tree = '{"windows":[{"elements":[{"automationId":"InputOutputNavItem","expandState":"collapsed"}]}]}' | ConvertFrom-Json
            (Get-InspectedGuestElement $tree 'InputOutputNavItem').expandState | Should Be 'collapsed'
            { Get-InspectedGuestElement $tree 'MouseWithoutBordersNavItem' } | Should Throw 'exact control'
        }
        finally { Set-StrictMode -Off }
    }

    It 'refuses empty, multiwindow, ambiguous or root-fallback inspection' -TestCases @(
        @{ Json = '{}' }
        @{ Json = '{"windows":[]}' }
        @{ Json = '{"windows":[{"elements":[]}]}' }
        @{ Json = '{"windows":[{"elements":[{"automationId":"InputOutputNavItem"},{"automationId":"InputOutputNavItem"}]}]}' }
        @{ Json = '{"windows":[{"elements":[{"automationId":"InputOutputNavItem"}]},{"elements":[]}]}' }
        @{ Json = '{"windows":[{"elements":[{"type":"Window","name":"PowerToys Settings"}]}]}' }
    ) {
        param($Json)
        { Get-InspectedGuestElement ($Json | ConvertFrom-Json) 'InputOutputNavItem' } | Should Throw 'exact control'
    }

    It 'reads expansion from the header button, not the SettingsExpander container' {
        Set-StrictMode -Version 2.0
        try {
            $tree = '{"matches":[{"type":"Button","name":"Security key","expandState":"collapsed","selector":"btn-header-1234","isEnabled":true,"isOffscreen":false},{"type":"Button","name":"Connect"}]}' |
                ConvertFrom-Json
            $header = Get-GuestSecurityKeyHeader $tree
            $header.expandState | Should Be 'collapsed'
            $header.selector | Should Be 'btn-header-1234'
        }
        finally { Set-StrictMode -Off }
    }

    It 'rejects a missing or ambiguous header without toggling anything' -TestCases @(
        @{ HeaderCount = 0 }
        @{ HeaderCount = 2 }
    ) {
        param($HeaderCount)
        $headers = @(for ($index = 0; $index -lt $HeaderCount; $index++) {
            [pscustomobject]@{ type = 'Button'; name = 'Security key'; expandState = 'expanded'; isEnabled = $true; isOffscreen = $false }
        })
        $tree = [pscustomobject]@{ matches = $headers }
        { Get-GuestSecurityKeyHeader $tree } | Should Throw 'one enabled expand/collapse header'
    }

    It 'uses explicit unique AutomationIds for the real Connect and clipboard actions' {
        [xml]$page = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\..\src\settings-ui\Settings.UI\SettingsXAML\Views\MouseWithoutBordersPage.xaml') -Raw
        $buttons = @($page.SelectNodes('//*[@AutomationProperties.AutomationId="MouseWithoutBordersConnectButton"]'))
        $buttons.Count | Should Be 1
        $buttons[0].GetAttribute('Command') | Should Match '\bConnectCommand\b'
        $toggles = @($page.SelectNodes('//*[@AutomationProperties.AutomationId="MouseWithoutBordersShareClipboardToggle"]'))
        $toggles.Count | Should Be 1
        $toggles[0].GetAttribute('IsOn') | Should Match '\bViewModel.ShareClipboard\b'
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
        $script:ui | Should Match '\$window = Get-GuestSettingsWindow'
        $script:ui | Should Match "@\('-a', \`$script:settingsPid.ToString\(\), '--json'\)"
        $script:ui | Should Not Match "@\('-w'"
        $script:ui | Should Match 'SettingsBefore = \$before; SettingsAfter = Get-GuestUiWaitSnapshot'
        $script:ui | Should Match 'Response = \$responseDiagnostic'
        $script:ui | Should Match 'WallElapsedMilliseconds'
        $script:ui | Should Not Match 'if \(\$process.ExitCode -ne 0\) \{'
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

    It 'captures the composed desktop at the request failure before response publication or peer teardown' {
        $failure = $script:worker.IndexOf("Write-EndpointCommandStage 'RequestFailed'")
        $capture = $script:worker.IndexOf("Save-EndpointFailureEvidence 'Request'")
        $publish = $script:worker.IndexOf("Write-EndpointCommandStage 'ResponsePublishing'")
        ($failure -ge 0 -and $failure -lt $capture -and $capture -lt $publish) | Should Be $true
        $receiver = Get-Content -LiteralPath (Join-Path $payloadRoot 'Receiver.cs') -Raw
        $method = [regex]::Match($receiver, '(?s)public static void CaptureFailureDesktop\(string path\)\r?\n        \{.*?\n        \}').Value
        $method | Should Match 'CopyFromScreen'
        $method | Should Match 'NativeSupport.Desktop\(\).Ready'
        $method | Should Not Match 'FocusWindow|Clipboard|SetForegroundWindow|DrawToBitmap'
        $receiver | Should Match 'public void CaptureFailureDesktop\(string path\) \{ Receiver.CaptureFailureDesktop\(path\); \}'
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

Describe 'MWB clipboard helper diagnostics exclude private event and settings contents' {
    BeforeAll {
        $tokens = $null
        $errors = $null
        $ast = [Management.Automation.Language.Parser]::ParseFile(
            (Join-Path $payloadRoot 'EndpointSupport.ps1'), [ref]$tokens, [ref]$errors)
        if ($errors.Count) { throw 'Endpoint support contains parse errors.' }
        foreach ($name in @('Get-ClipboardHelperEventKind', 'Get-ClipboardHelperEvents', 'Save-ClipboardDiagnostics')) {
            $definition = $ast.Find({
                param($node)
                $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq $name
            }, $true)
            . ([scriptblock]::Create($definition.Extent.Text))
        }
    }

    BeforeEach {
        $script:config = @{ Role = 'Guest'; RunId = '00112233-4455-6677-8899-aabbccddeeff' }
        $script:settingsRoot = $TestDrive
        $script:owned = @()
        $OutputRoot = $TestDrive
        $script:clipboardReport = $null
        Mock Write-RunJson { param($Path, $Value) $script:clipboardReport = $Value }
        Mock Get-ClipboardHelperEvents { @{ QueryStatus = 'Succeeded'; Events = @() } }
    }

    It 'classifies only fixed helper event kinds without returning message contents' -TestCases @(
        @{ Message = 'WM_DRAWCLIPBOARD: private-clipboard-token'; Kind = 'ForwardFailed' }
        @{ Message = 'GetClipboardText: TXT = 100, RTF = 0, HTM = 0.'; Kind = 'TextObserved' }
        @{ Message = 'GetClipboardText, Text too big: TXT = 100000000'; Kind = 'TextTooLarge' }
        @{ Message = 'Trace: AddClipboardFormatListener: GetLastError = 5'; Kind = 'ListenerRegistration' }
        @{ Message = 'Trace: SetClipboardViewer: GetLastError = 0'; Kind = 'LegacyListenerRegistration' }
        @{ Message = 'Trace: Clipboard monitor method AddClipboardFormatListener is used.'; Kind = 'MonitorSelected' }
        @{ Message = 'ClipboardMMHelper does not have text/image/file data.'; Kind = 'NoSupportedFormat' }
        @{ Message = 'Null clipboard data returned. See previous messages (if any) for more information.'; Kind = 'EmptyData' }
        @{ Message = 'Private application cannot be used in a remote desktop or virtual machine session.'; Kind = 'DisconnectedNonConsoleHelper' }
        @{ Message = 'private-clipboard-token C:\private-user-path'; Kind = 'Other' }
        @{ Message = ''; Kind = 'Other' }
    ) {
        param($Message, $Kind)
        Get-ClipboardHelperEventKind $Message | Should Be $Kind
    }

    It 'exports only typed sharing flags and never the pairing key or extra settings' {
        Mock Read-RunJson {
            '{"properties":{"ShareClipboard":{"value":true},"UseService":{"value":false},"AllowNonConsoleSessions":{"value":true},"SecurityKey":{"value":"private-pairing-token"},"Name2IP":{"value":"private-peer"}}}' |
                ConvertFrom-Json
        }
        Save-ClipboardDiagnostics
        $script:clipboardReport.ShareClipboard | Should Be $true
        $script:clipboardReport.UseService | Should Be $false
        $script:clipboardReport.AllowNonConsoleSessions | Should Be $true
        $script:clipboardReport.SettingsQueryStatus | Should Be 'Succeeded'
        ($script:clipboardReport | ConvertTo-Json -Depth 8) | Should Not Match 'private-|SecurityKey|Name2IP'
    }

    It 'does not guess missing or malformed configuration values' {
        Mock Read-RunJson {
            '{"properties":{"ShareClipboard":{"value":"private-invalid-value"},"UseService":{"value":1}}}' | ConvertFrom-Json
        }
        Save-ClipboardDiagnostics
        $script:clipboardReport.ShareClipboard | Should BeNullOrEmpty
        $script:clipboardReport.UseService | Should BeNullOrEmpty
        $script:clipboardReport.AllowNonConsoleSessions | Should BeNullOrEmpty
        ($script:clipboardReport | ConvertTo-Json -Depth 8) | Should Not Match 'private-'
    }

    It 'records configuration observation failure explicitly without exporting the exception' {
        Mock Read-RunJson { throw [IO.IOException]::new('private-file-and-clipboard-content') }
        Save-ClipboardDiagnostics
        $script:clipboardReport.SettingsQueryStatus | Should Be 'Failed'
        $script:clipboardReport.SettingsQueryErrorHResult | Should Match '^0x[0-9A-F]{8}$'
        ($script:clipboardReport | ConvertTo-Json -Depth 8) | Should Not Match 'private-'
    }

    It 'keeps helper event collection bounded and stores no raw description or XML' {
        $definition = $ast.Find({
            param($node)
            $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Get-ClipboardHelperEvents'
        }, $true).Extent.Text
        $definition | Should Match '\$index -lt 32'
        $definition | Should Match "Provider\[@Name='MouseWithoutBordersHelper'\]"
        $definition | Should Match 'TimeCreated.ToUniversalTime\(\) -lt \$script:startedUtc'
        $definition | Should Match 'Kind = Get-ClipboardHelperEventKind'
        $definition | Should Not Match 'ToXml|Message =|Description ='
        $definition | Should Match "QueryStatus = 'Failed'"
    }
}

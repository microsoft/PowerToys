# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

$payloadRoot = Join-Path $PSScriptRoot '..\..\src\modules\MouseWithoutBorders\MouseWithoutBorders.UITests\Payload'
function Invoke-GuestUi { param([string[]]$Arguments, [int]$TimeoutSeconds) }
function Request-SettingsMaximize {}
function Get-SettingsWindowLayout {}
function Write-RunJson { param($Path, $Value) }
function Assert-PeerMapping { param($Stage) }
function Get-GuestConnectAcknowledgement { param($Key, $PeerName) }
function Wait-GuestConnectAcknowledgement { param($Key, $PeerName) }

Describe 'MWB owned Settings readiness' {
    BeforeAll {
        $tokens = $null
        $errors = $null
        $ast = [Management.Automation.Language.Parser]::ParseFile(
            (Join-Path $payloadRoot 'EndpointSupport.ps1'), [ref]$tokens, [ref]$errors)
        if ($errors.Count) { throw 'Endpoint support contains parse errors.' }
        foreach ($name in @('Wait-GuestMwbControl', 'Set-SettingsWindowMaximized', 'Connect-Guest')) {
            $definition = $ast.Find({
                param($node)
                $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -ceq $name
            }, $true)
            . ([scriptblock]::Create($definition.Extent.Text))
        }
    }

    BeforeEach {
        Set-StrictMode -Version 2.0
        $script:OutputRoot = $TestDrive
        $script:config = @{ Role = 'Guest' }
        $script:actions = [Collections.Generic.List[string]]::new()
        $script:geometrySamples = 0
        $script:failControl = ''
        $script:wrongResult = $null
        $script:lostWindow = $false
        $script:published = $null
        Mock Start-Sleep {}
        Mock Request-SettingsMaximize {}
        Mock Get-SettingsWindowLayout {
            if ($script:lostWindow) { throw [InvalidOperationException]::new('Owned Settings window disappeared') }
            $script:geometrySamples++
            @{
                Window = 123; Maximized = $script:geometrySamples -gt 1
                Left = -8; Top = -8; Right = 1928; Bottom = 1048
                WorkLeft = 0; WorkTop = 0; WorkRight = 1920; WorkBottom = 1040
            }
        }
        Mock Write-RunJson { param($Path, $Value) $script:published = $Value }
        Mock Invoke-GuestUi {
            param($Arguments)
            $script:actions.Add("$($Arguments[0]):$($Arguments[1])")
            if ($Arguments[1] -eq $script:failControl) { throw [TimeoutException]::new('Owned control readiness failed') }
            if ($null -ne $script:wrongResult) { return $script:wrongResult }
            if ($Arguments[0] -eq 'wait-for') {
                return [pscustomobject]@{
                    found = $true
                    element = [pscustomobject]@{ automationId = $Arguments[1]; isEnabled = $true }
                }
            }
        }
        Mock Assert-PeerMapping { $script:actions.Add('verify:mapping'); @{ MatchesExpected = $true } }
        Mock Get-GuestConnectAcknowledgement { $script:actions.Add('read:pairing'); @{ KeyMatches = $false; PeerConfigured = $false } }
        Mock Wait-GuestConnectAcknowledgement { $script:actions.Add('await:pairing'); @{ Acknowledged = $true } }
    }

    AfterEach { Set-StrictMode -Off }

    It 'posts one maximize request then awaits two stable native full-work-area samples' {
        Set-SettingsWindowMaximized ([DateTime]::UtcNow.AddSeconds(5))
        $script:geometrySamples | Should Be 3
        $script:published.StableSamples | Should Be 2
        $script:published.Layout.Maximized | Should Be $true
        Assert-MockCalled Request-SettingsMaximize -Times 1 -Exactly -Scope It
        Assert-MockCalled Invoke-GuestUi -Times 0 -Exactly -Scope It
    }

    It 'does not issue a maximize request after the existing startup deadline' {
        { Set-SettingsWindowMaximized ([DateTime]::UtcNow.AddSeconds(-1)) } | Should Throw 'startup deadline expired'
        Assert-MockCalled Request-SettingsMaximize -Times 0 -Exactly -Scope It
    }

    It 'does not hide lost ownership or replay a maximize request' {
        $script:lostWindow = $true
        { Set-SettingsWindowMaximized ([DateTime]::UtcNow.AddSeconds(5)) } | Should Throw 'window disappeared'
        Assert-MockCalled Request-SettingsMaximize -Times 1 -Exactly -Scope It
        Assert-MockCalled Write-RunJson -Times 0 -Exactly -Scope It
    }

    It 'requires stable geometry rather than the native maximized flag alone' {
        Mock Get-SettingsWindowLayout {
            $script:geometrySamples++
            @{
                Window = 123; Maximized = $true; Left = -8; Top = -8
                Right = $(if ($script:geometrySamples -eq 1) { 1200 } else { 1928 }); Bottom = 1048
                WorkLeft = 0; WorkTop = 0; WorkRight = 1920; WorkBottom = 1040
            }
        }
        Set-SettingsWindowMaximized ([DateTime]::UtcNow.AddSeconds(5))
        $script:geometrySamples | Should Be 3
    }

    It 'rejects success-shaped missing, substituted or disabled control results' -TestCases @(
        @{ Json = '{}' }
        @{ Json = '{"found":false}' }
        @{ Json = '{"found":true,"element":null}' }
        @{ Json = '{"found":true,"element":{}}' }
        @{ Json = '{"found":true,"element":{"automationId":"other","isEnabled":true}}' }
        @{ Json = '{"found":true,"element":{"automationId":"x","isEnabled":false}}' }
    ) {
        param($Json)
        $script:wrongResult = $Json | ConvertFrom-Json
        { Wait-GuestMwbControl 'x' } | Should Throw 'acknowledge readiness'
    }

    It 'awaits the real field-opening command and both fields before setting values and submitting once' {
        $result = Connect-Guest 'private-fixture-key' 'PEER'
        $result.Submitted | Should Be $true
        $result.Acknowledged | Should Be $true
        ($script:actions -join ',') | Should Be 'wait-for:MouseWithoutBordersShowConnectFieldsButton,click:MouseWithoutBordersShowConnectFieldsButton,wait-for:ConnectSecurityKeyTextBox,wait-for:ConnectPCNameTextBox,set-value:ConnectSecurityKeyTextBox,set-value:ConnectPCNameTextBox,verify:mapping,read:pairing,invoke:MouseWithoutBordersConnectButton,await:pairing'
        Assert-MockCalled Invoke-GuestUi -Times 1 -Exactly -Scope It -ParameterFilter {
            $Arguments[0] -eq 'invoke' -and $Arguments[1] -eq 'MouseWithoutBordersConnectButton'
        }
    }

    It 'does not perform a later action after a field readiness failure' {
        $script:failControl = 'ConnectPCNameTextBox'
        { Connect-Guest 'private-fixture-key' 'PEER' } | Should Throw 'readiness failed'
        Assert-MockCalled Invoke-GuestUi -Times 0 -Exactly -Scope It -ParameterFilter {
            $Arguments[0] -eq 'set-value' -or $Arguments[1] -eq 'MouseWithoutBordersConnectButton'
        }
        Assert-MockCalled Wait-GuestConnectAcknowledgement -Times 0 -Exactly -Scope It
    }

    It 'retains the ninety-second per-command readiness ceiling without whole-window lookups' {
        Connect-Guest 'private-fixture-key' 'PEER'
        Assert-MockCalled Invoke-GuestUi -Times 3 -Exactly -Scope It -ParameterFilter {
            $Arguments[0] -eq 'wait-for' -and $Arguments[3] -eq '90000'
        }
        Assert-MockCalled Invoke-GuestUi -Times 0 -Exactly -Scope It -ParameterFilter {
            $Arguments[0] -eq 'search' -or $Arguments[0] -eq 'inspect'
        }
    }

    It 'opens the supported module page and does not attach UIA during startup diagnostics' {
        $start = $ast.Find({
            param($node)
            $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -ceq 'Start-Endpoint'
        }, $true).Extent.Text
        $start | Should Match "\`$start.Arguments = '--open-settings=MouseWithoutBorders'"
        $start | Should Match 'Set-SettingsWindowMaximized -DeadlineUtc \$deadline'
        $diagnostics = $ast.Find({
            param($node)
            $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -ceq 'Save-SettingsDiagnostics'
        }, $true).Extent.Text
        $diagnostics | Should Match 'Get-Process -Id \$script:settingsPid'
        $diagnostics | Should Not Match 'Invoke-GuestUi|inspect|search'
    }

    It 'allows fifteen minutes for either endpoint with stable Settings and actual owned MWB listeners' -TestCases @(
        @{ Role = 'Host' }
        @{ Role = 'Guest' }
    ) {
        param($Role)
        $script:config.Role = $Role
        $start = $ast.Find({
            param($node)
            $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -ceq 'Start-Endpoint'
        }, $true)
        $budget = $start.Find({
            param($node)
            $node -is [Management.Automation.Language.AssignmentStatementAst] -and
                $node.Left.Extent.Text -ceq '$startupBudgetMinutes'
        }, $true)
        . ([scriptblock]::Create($budget.Extent.Text))
        $startupBudgetMinutes | Should Be 15
        $start.Extent.Text | Should Match '\$deadline = \[DateTime\]::UtcNow.AddMinutes\(\$startupBudgetMinutes\)'
        $start.Extent.Text | Should Match '\$readyCandidates = @\(Get-ReadySettingsCandidates \$candidateStates\)'
        $start.Extent.Text | Should Match 'TcpSocketTable\]::Read\(\$mwb\[0\].Id\)'
        $start.Extent.Text | Should Match 'HasMwbListeners\(\$mwb\[0\].Id, \$mwbSockets\)'
        $start.Extent.Text | Should Match '\$settingsReady -and \$mwbReady'
        $start.Extent.Text | Should Match '\$readyMwbPid -eq \$mwb\[0\].Id'
        $start.Extent.Text | Should Match '\$readySamples -ge 2'
    }

    It 'encloses both endpoint Start calls and recovery startup with the shared request budget' {
        $fixtureRoot = Split-Path $payloadRoot
        $fixture = Get-Content -LiteralPath (Join-Path $fixtureRoot 'TwoEndpointFixture.cs') -Raw
        @([regex]::Matches($fixture, 'timeoutSeconds: SandboxTimeouts.EndpointStartRequestSeconds')).Count | Should Be 2
        $recovery = Get-Content -LiteralPath (Join-Path $fixtureRoot 'EndpointRecoveryFixture.cs') -Raw
        $recovery | Should Match 'Budget\(SandboxTimeouts.EndpointStartRequestSeconds\)'
        $recovery | Should Match 'DateTime.UtcNow \+ SandboxTimeouts.RecoveryRun'
        $recovery | Should Match 'SandboxTimeouts.RecoveryRun - watch.Elapsed'
        $recovery | Should Match 'RecoveryDeadlineSeconds = 180'
    }

    It 'keeps local suite and controller envelopes outside the longer fixture deadlines' {
        $wrapper = Get-Content -LiteralPath (Join-Path $payloadRoot '..\Scripts\Invoke-AutonomousLocalVm.ps1') -Raw
        $wrapper | Should Match "SuiteTimeout = \`$\(if \(\`$backend -eq 'WinApp'\) \{ '95m' \} else \{ '70m' \}\)"
        $wrapper | Should Match "TimeoutMinutes = \`$\(if \(\`$backend -eq 'WinApp'\) \{ 110 \} else \{ 85 \}\)"
    }

    It 'propagates the requested resource profile through both VM startup and test dispatch' {
        $wrapper = Get-Content -LiteralPath (Join-Path $payloadRoot '..\Scripts\Invoke-AutonomousLocalVm.ps1') -Raw
        $wrapper | Should Match "ValidateSet\('Default', 'Constrained'\).*ResourceProfile = 'Default'"
        $wrapper | Should Match 'ResourceProfile = \$ResourceProfile'
        $wrapper | Should Match 'Start-LocalVm.ps1[\s\S]*-ResourceProfile \$ResourceProfile -Wait'
    }

    It 'awaits the guest cleanup and abort-test viewer independently without relaxing fault assertions' {
        $root = Split-Path $payloadRoot
        $fixture = Get-Content -LiteralPath (Join-Path $root 'TwoEndpointFixture.cs') -Raw
        $fixture | Should Match 'guest.Request\("StopPeers", timeoutSeconds: SandboxTimeouts.GuestStopRequestSeconds\)'
        $recovery = Get-Content -LiteralPath (Join-Path $root 'RecoveryTests.cs') -Raw
        $recovery | Should Match 'SandboxTimeouts.LegacyViewerStart'
        $recovery | Should Match 'Assert.ThrowsExactly<RecoveryBoundaryException>'
        $recovery | Should Match 'Assert.IsTrue\(refusalObserved && ownedIdentityPreserved\)'
        $recovery | Should Match 'Assert.IsFalse\(owned.GuestAcknowledged'
    }

    It 'requires stable receiver focus before one checked physical keyboard batch' {
        $source = Get-Content -LiteralPath (Join-Path (Split-Path $payloadRoot) 'ReceiverFocusTests.cs') -Raw
        $source | Should Match 'WaitHelper.WaitForStable'
        $source | Should Match 'requiredConsecutiveMatches: 3'
        $source | Should Match 'NativeSupport.GetForegroundWindow\(\) == receiver.Handle && receiver.InputFocused'
        @([regex]::Matches($source, 'KeyboardHelper.SendChord\(Key.A\)')).Count | Should Be 1
        $source | Should Match 'receiver.ReceivedText == "a"'
        $source | Should Match 'Assert.AreEqual\(1, receiver.StartMenuDismissals'
        $source | Should Match 'receiver-final-state.json'
    }
}

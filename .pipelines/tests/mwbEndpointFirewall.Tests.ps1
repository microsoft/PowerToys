# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

$payloadRoot = Join-Path $PSScriptRoot '..\..\src\modules\MouseWithoutBorders\MouseWithoutBorders.UITests\Payload'

Describe 'MWB test-guest firewall ownership' {
    BeforeAll {
        Add-Type @'
using System;
using System.Collections;
using System.Collections.Generic;
namespace MwbFirewallContracts {
    public sealed class Rule {
        public string Name { get; set; }
        public string ApplicationName { get; set; }
        public int Protocol { get; set; }
        private string localPorts;
        public string LocalPorts {
            get { return localPorts; }
            set {
                if (Protocol != 6) throw new InvalidOperationException("Set protocol before ports");
                localPorts = value;
            }
        }
        public string RemotePorts { get; set; }
        public string LocalAddresses { get; set; }
        public string RemoteAddresses { get; set; }
        public int Direction { get; set; }
        public int Action { get; set; }
        public bool Enabled { get; set; }
        public int Profiles { get; set; }
        public bool EdgeTraversal { get; set; }
        public string InterfaceTypes { get; set; }
        public string ServiceName { get; set; }
    }
    public sealed class Rules : IEnumerable {
        public List<Rule> Items = new List<Rule>();
        public int Adds, Removes, Reads;
        public int ThrowOnRead;
        public string AddFailure, RemoveFailure;
        public bool SuppressAdd, SuppressRemove;
        public string MutationProperty;
        public object MutationValue;
        public void Add(Rule rule) {
            Adds++;
            if (rule.Name == null || rule.ApplicationName == null || rule.RemoteAddresses == null ||
                rule.LocalPorts == null || rule.RemotePorts == null || rule.LocalAddresses == null ||
                rule.Direction != 1 || rule.Action != 1 || !rule.Enabled ||
                rule.Profiles != int.MaxValue || rule.EdgeTraversal || rule.InterfaceTypes != "All")
                throw new InvalidOperationException("Rule was added before its complete scope was configured");
            if (AddFailure == "Before") throw new InvalidOperationException("Add failed before commit");
            if (!SuppressAdd) Items.Add(rule);
            if (MutationProperty != null) typeof(Rule).GetProperty(MutationProperty).SetValue(rule, MutationValue);
            if (AddFailure == "After") throw new InvalidOperationException("Add failed after commit");
        }
        public void Remove(string name) {
            Removes++;
            if (RemoveFailure == "Before") throw new InvalidOperationException("Remove failed before commit");
            if (!SuppressRemove) Items.RemoveAll(rule => string.Equals(rule.Name, name, StringComparison.OrdinalIgnoreCase));
            if (RemoveFailure == "After") throw new InvalidOperationException("Remove failed after commit");
        }
        public IEnumerator GetEnumerator() {
            Reads++;
            if (Reads == ThrowOnRead) throw new InvalidOperationException("Readback unavailable");
            return Items.GetEnumerator();
        }
    }
}
'@
        $tokens = $null
        $errors = $null
        $script:supportAst = [Management.Automation.Language.Parser]::ParseFile(
            (Join-Path $payloadRoot 'EndpointSupport.ps1'), [ref]$tokens, [ref]$errors)
        if ($errors.Count) { throw ($errors | Out-String) }
        foreach ($name in @(
            'New-EndpointFirewallPolicy', 'New-EndpointFirewallRuleObject', 'Release-EndpointFirewallComObject',
            'Read-EndpointFirewallRules', 'Test-EndpointFirewallRuleScope', 'Save-EndpointFirewallOwnership',
            'Add-EndpointFirewallRule', 'Remove-EndpointFirewallRule')) {
            $definition = $script:supportAst.Find({
                param($node)
                $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq $name
            }, $true)
            . ([scriptblock]::Create($definition.Extent.Text))
        }
    }

    BeforeEach {
        $global:MwbFirewallRules = [MwbFirewallContracts.Rules]::new()
        $global:MwbFirewallPolicy = [pscustomobject]@{ Rules = $global:MwbFirewallRules }
        $global:MwbFirewallOwnership = $null
        $global:MwbFirewallStates = [Collections.Generic.List[string]]::new()
        $name = 'PowerToys.Mwb.Guest.00112233-4455-6677-8899-aabbccddeeff'
        $program = 'C:\MwbProduct\PowerToys.MouseWithoutBorders.exe'
        $peer = '192.0.2.1'
        Mock New-Object { throw 'Unexpected COM activation' } -ParameterFilter { $ComObject }
        Mock New-EndpointFirewallPolicy { $global:MwbFirewallPolicy }
        Mock New-EndpointFirewallRuleObject { [MwbFirewallContracts.Rule]::new() }
        Mock Release-EndpointFirewallComObject { }
        Mock Save-EndpointFirewallOwnership {
            param($Ownership)
            $global:MwbFirewallOwnership = $Ownership
            $global:MwbFirewallStates.Add($Ownership.State)
        }
    }

    It 'creates and reads back exactly the approved guest-only scope' {
        Add-EndpointFirewallRule $name $program $peer
        $rule = $global:MwbFirewallRules.Items[0]
        $rule.Name | Should Be $name
        $rule.ApplicationName | Should Be $program
        $rule.Protocol | Should Be 6
        $rule.LocalPorts | Should Be '15100,15101'
        $rule.RemoteAddresses | Should Be $peer
        $rule.LocalAddresses | Should Be '*'
        $rule.RemotePorts | Should Be '*'
        $rule.Direction | Should Be 1
        $rule.Action | Should Be 1
        $rule.Enabled | Should Be $true
        $rule.Profiles | Should Be 2147483647
        $rule.EdgeTraversal | Should Be $false
        $rule.InterfaceTypes | Should Be 'All'
        $global:MwbFirewallStates.ToArray() -join ',' | Should Be 'CreationPending,Verified'
        $global:MwbFirewallRules.Reads | Should Be 3
        $global:MwbFirewallOwnership.CreationReadback.Count | Should Be 1
        $global:MwbFirewallOwnership.CreationReadback[0].PeerAddress | Should Be $peer
        Assert-MockCalled Release-EndpointFirewallComObject -Times 1 -Exactly -Scope It -ParameterFilter {
            [object]::ReferenceEquals($Value, $global:MwbFirewallPolicy)
        }
        Assert-MockCalled Release-EndpointFirewallComObject -Times 4 -Exactly -Scope It -ParameterFilter {
            [object]::ReferenceEquals($Value, $global:MwbFirewallRules)
        }
    }

    It 'rejects a non-IPv4 peer before COM activation' {
        { Add-EndpointFirewallRule $name $program '::1' } | Should Throw 'one IPv4 peer'
        Assert-MockCalled New-EndpointFirewallPolicy -Times 0 -Exactly -Scope It
        Assert-MockCalled Save-EndpointFirewallOwnership -Times 0 -Exactly -Scope It
    }

    It 'does not publish ownership if the COM rule factory fails' {
        Mock New-EndpointFirewallRuleObject { throw 'Factory failed' }
        { Add-EndpointFirewallRule $name $program $peer } | Should Throw 'Factory failed'
        Assert-MockCalled Save-EndpointFirewallOwnership -Times 0 -Exactly -Scope It
        Assert-MockCalled Release-EndpointFirewallComObject -Times 1 -Exactly -Scope It -ParameterFilter {
            [object]::ReferenceEquals($Value, $global:MwbFirewallPolicy)
        }
    }

    It 'rejects a same-friendly-name collision before Add or ownership publication' {
        $existing = [MwbFirewallContracts.Rule]::new()
        $existing.Name = $name.ToUpperInvariant()
        $existing.ApplicationName = 'C:\NotOurs.exe'
        $global:MwbFirewallRules.Items.Add($existing)

        { Add-EndpointFirewallRule $name $program $peer } | Should Throw 'pre-existing'
        $global:MwbFirewallRules.Adds | Should Be 0
        $global:MwbFirewallRules.Items.Count | Should Be 1
        $global:MwbFirewallRules.Items[0].ApplicationName | Should Be 'C:\NotOurs.exe'
        Assert-MockCalled Save-EndpointFirewallOwnership -Times 0 -Exactly -Scope It
        Assert-MockCalled New-EndpointFirewallRuleObject -Times 0 -Exactly -Scope It
    }

    It 'rechecks collisions after configuring the new COM object but before claiming it' {
        Mock New-EndpointFirewallRuleObject {
            $existing = [MwbFirewallContracts.Rule]::new()
            $existing.Name = 'PowerToys.Mwb.Guest.00112233-4455-6677-8899-aabbccddeeff'
            $global:MwbFirewallRules.Items.Add($existing)
            [MwbFirewallContracts.Rule]::new()
        }
        { Add-EndpointFirewallRule $name $program $peer } | Should Throw 'collision'
        $global:MwbFirewallRules.Adds | Should Be 0
        Assert-MockCalled Save-EndpointFirewallOwnership -Times 0 -Exactly -Scope It
    }

    It 'accepts only equivalent host and two-port normalization: <Address>, <Ports>' -TestCases @(
        @{ Address = '192.0.2.1'; Ports = '15100,15101' }
        @{ Address = '192.0.2.1/32'; Ports = '15100-15101' }
        @{ Address = '192.0.2.1/255.255.255.255'; Ports = '15101, 15100' }
    ) {
        param($Address, $Ports)
        Add-EndpointFirewallRule $name $program $peer
        $global:MwbFirewallRules.Items[0].RemoteAddresses = $Address
        $global:MwbFirewallRules.Items[0].LocalPorts = $Ports
        Remove-EndpointFirewallRule $global:MwbFirewallOwnership
        $global:MwbFirewallRules.Items.Count | Should Be 0
        $global:MwbFirewallOwnership.State | Should Be 'Removed'
    }

    It 'refuses readback with altered <Property>' -TestCases @(
        @{ Property = 'ApplicationName'; Value = 'C:\Other.exe' }
        @{ Property = 'Protocol'; Value = 17 }
        @{ Property = 'LocalPorts'; Value = '15100-15102' }
        @{ Property = 'RemoteAddresses'; Value = '*' }
        @{ Property = 'RemoteAddresses'; Value = '192.0.2.0/24' }
        @{ Property = 'RemoteAddresses'; Value = '192.0.2.2' }
        @{ Property = 'RemoteAddresses'; Value = '192.0.2.1,192.0.2.2' }
        @{ Property = 'Direction'; Value = 2 }
        @{ Property = 'Action'; Value = 0 }
        @{ Property = 'Enabled'; Value = $false }
        @{ Property = 'Profiles'; Value = 1 }
        @{ Property = 'EdgeTraversal'; Value = $true }
        @{ Property = 'InterfaceTypes'; Value = 'LAN' }
        @{ Property = 'ServiceName'; Value = 'OtherService' }
        @{ Property = 'LocalAddresses'; Value = '192.0.2.2' }
        @{ Property = 'RemotePorts'; Value = '15100' }
    ) {
        param($Property, $Value)
        $global:MwbFirewallRules.MutationProperty = $Property
        $global:MwbFirewallRules.MutationValue = $Value
        { Add-EndpointFirewallRule $name $program $peer } | Should Throw 'exact scoped rule'
        $global:MwbFirewallOwnership.State | Should Be 'Uncertain'
        { Remove-EndpointFirewallRule $global:MwbFirewallOwnership } | Should Throw 'uncertain'
        $global:MwbFirewallRules.Removes | Should Be 0
        $global:MwbFirewallOwnership.Error | Should Not BeNullOrEmpty
    }

    It 'records a failed Add with verified absence without claiming a rule' {
        $global:MwbFirewallRules.AddFailure = 'Before'
        { Add-EndpointFirewallRule $name $program $peer } | Should Throw 'before commit'
        $global:MwbFirewallOwnership.State | Should Be 'Absent'
        Remove-EndpointFirewallRule $global:MwbFirewallOwnership
        $global:MwbFirewallRules.Removes | Should Be 0
    }

    It 'verifies a partially committed Add before permitting failure cleanup' {
        $global:MwbFirewallRules.AddFailure = 'After'
        { Add-EndpointFirewallRule $name $program $peer } | Should Throw 'after commit'
        $global:MwbFirewallOwnership.State | Should Be 'Verified'
        $global:MwbFirewallOwnership.Error | Should Match 'after commit'
        Remove-EndpointFirewallRule $global:MwbFirewallOwnership
        $global:MwbFirewallRules.Items.Count | Should Be 0
        $global:MwbFirewallOwnership.State | Should Be 'Removed'
    }

    It 'retains uncertainty when successful Add has no readable rule' {
        $global:MwbFirewallRules.SuppressAdd = $true
        { Add-EndpointFirewallRule $name $program $peer } | Should Throw 'exact scoped rule'
        $global:MwbFirewallOwnership.State | Should Be 'Uncertain'
        $global:MwbFirewallRules.Removes | Should Be 0
    }

    It 'retains the creation journal when readback fails after Add' {
        $global:MwbFirewallRules.ThrowOnRead = 3
        { Add-EndpointFirewallRule $name $program $peer } | Should Throw 'Readback unavailable'
        $global:MwbFirewallOwnership.State | Should Be 'Uncertain'
        $global:MwbFirewallRules.Items.Count | Should Be 1
        $global:MwbFirewallOwnership.Error | Should Match 'Readback unavailable'
    }

    It 'refuses to remove a duplicate friendly name' {
        Add-EndpointFirewallRule $name $program $peer
        $duplicate = [MwbFirewallContracts.Rule]::new()
        $duplicate.Name = $name
        $global:MwbFirewallRules.Items.Add($duplicate)
        { Remove-EndpointFirewallRule $global:MwbFirewallOwnership } | Should Throw 'scope changed'
        $global:MwbFirewallRules.Removes | Should Be 0
        $global:MwbFirewallOwnership.State | Should Be 'Uncertain'
    }

    It 'rechecks the exact <Property> before removing a previously verified rule' -TestCases @(
        @{ Property = 'ApplicationName'; Value = 'C:\Other.exe' }
        @{ Property = 'Protocol'; Value = 17 }
        @{ Property = 'LocalPorts'; Value = '*' }
        @{ Property = 'RemoteAddresses'; Value = '192.0.2.0/24' }
        @{ Property = 'Direction'; Value = 2 }
        @{ Property = 'Action'; Value = 0 }
        @{ Property = 'Enabled'; Value = $false }
        @{ Property = 'Profiles'; Value = 1 }
        @{ Property = 'EdgeTraversal'; Value = $true }
    ) {
        param($Property, $Value)
        Add-EndpointFirewallRule $name $program $peer
        $global:MwbFirewallRules.Items[0].$Property = $Value
        { Remove-EndpointFirewallRule $global:MwbFirewallOwnership } | Should Throw 'scope changed'
        $global:MwbFirewallRules.Removes | Should Be 0
    }

    It 'verifies absence and leaves unrelated rules intact' {
        Add-EndpointFirewallRule $name $program $peer
        $other = [MwbFirewallContracts.Rule]::new()
        $other.Name = 'Unrelated'
        $global:MwbFirewallRules.Items.Add($other)
        Remove-EndpointFirewallRule $global:MwbFirewallOwnership
        $global:MwbFirewallRules.Items.Count | Should Be 1
        $global:MwbFirewallRules.Items[0].Name | Should Be 'Unrelated'
        $global:MwbFirewallRules.Reads | Should Be 5
        $global:MwbFirewallOwnership.State | Should Be 'Removed'
        $global:MwbFirewallOwnership.RemovalReadback.Count | Should Be 0
        $global:MwbFirewallOwnership.AbsentVerifiedUtc | Should Not BeNullOrEmpty
        Remove-EndpointFirewallRule $global:MwbFirewallOwnership
        $global:MwbFirewallRules.Removes | Should Be 1
    }

    It 'is idempotent if the previously verified rule is already absent' {
        Add-EndpointFirewallRule $name $program $peer
        $global:MwbFirewallRules.Items.Clear()
        Remove-EndpointFirewallRule $global:MwbFirewallOwnership
        $global:MwbFirewallOwnership.State | Should Be 'Removed'
        $global:MwbFirewallRules.Removes | Should Be 0
    }

    It 'does not accept an ineffective Remove as successful cleanup' {
        Add-EndpointFirewallRule $name $program $peer
        $global:MwbFirewallRules.SuppressRemove = $true
        { Remove-EndpointFirewallRule $global:MwbFirewallOwnership } | Should Throw 'remains'
        $global:MwbFirewallOwnership.State | Should Be 'Verified'
        $global:MwbFirewallOwnership.Error | Should Match 'remains'
    }

    It 'retains errors if Remove throws even when absence is verified' {
        Add-EndpointFirewallRule $name $program $peer
        $global:MwbFirewallRules.RemoveFailure = 'After'
        { Remove-EndpointFirewallRule $global:MwbFirewallOwnership } | Should Throw 'after commit'
        $global:MwbFirewallRules.Items.Count | Should Be 0
        $global:MwbFirewallOwnership.State | Should Be 'Removed'
        $global:MwbFirewallOwnership.Error | Should Match 'after commit'
    }

    It 'retains uncertainty if the post-removal absence check fails' {
        Add-EndpointFirewallRule $name $program $peer
        $global:MwbFirewallRules.ThrowOnRead = 5
        { Remove-EndpointFirewallRule $global:MwbFirewallOwnership } | Should Throw 'Readback unavailable'
        $global:MwbFirewallOwnership.State | Should Be 'Uncertain'
        $global:MwbFirewallOwnership.Error | Should Match 'Readback unavailable'
    }

    It 'never removes a rule when fresh scope verification fails' {
        Add-EndpointFirewallRule $name $program $peer
        $global:MwbFirewallRules.ThrowOnRead = 4
        { Remove-EndpointFirewallRule $global:MwbFirewallOwnership } | Should Throw 'Readback unavailable'
        $global:MwbFirewallRules.Removes | Should Be 0
        $global:MwbFirewallOwnership.State | Should Be 'Uncertain'
    }

    It 'initializes ownership and uses the same checked cleanup on failure' {
        $worker = Get-Content (Join-Path $payloadRoot 'EndpointWorker.ps1') -Raw
        $worker | Should Match '\$script:firewallOwnership = \$null'
        $worker | Should Match 'finally\s*\{[\s\S]*Stop-Endpoint[\s\S]*Write-EndpointJournal'
        $script:supportAst.Extent.Text | Should Match 'GuestFirewallOwnership = \$script:firewallOwnership'
        $script:supportAst.Extent.Text | Should Match 'Remove-EndpointFirewallRule \$script:firewallOwnership'
        $script:supportAst.Extent.Text | Should Not Match '(New|Remove|Set)-NetFirewall'
    }

    AfterAll {
        Remove-Variable MwbFirewallRules,MwbFirewallPolicy,MwbFirewallOwnership,MwbFirewallStates -Scope Global -ErrorAction SilentlyContinue
    }
}

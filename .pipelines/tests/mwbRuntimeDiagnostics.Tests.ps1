# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

#Requires -Version 7.0
. "$PSScriptRoot\..\MwbSandboxCi.Common.ps1"
$tokens = $null
$parseErrors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile(
    (Join-Path $PSScriptRoot '..\Export-MwbRuntimeDiagnostics.ps1'), [ref]$tokens, [ref]$parseErrors)
if ($parseErrors) { throw 'Runtime diagnostics contain parse errors.' }
foreach ($name in @('Get-MwbDiagnosticEndpointNames', 'ConvertTo-MwbRuntimeEvent',
    'Get-MwbRuntimeEventLog', 'Get-MwbRuntimeDiagnostics', 'Export-MwbRuntimeDiagnostics')) {
    $definition = $ast.Find({
        param($node)
        $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq $name
    }, $true)
    . ([scriptblock]::Create($definition.Extent.Text))
}

function New-MwbRuntimeEventFixture {
    param([string]$Data)
    $event = [pscustomobject]@{
        Id = 1116; Level = 3; RecordId = 123
        TimeCreated = [DateTime]::UtcNow
        Xml = "<Event><EventData>$Data</EventData></Event>"
    }
    $event | Add-Member -MemberType ScriptMethod -Name ToXml -Value { $this.Xml }
    $event
}

function Get-MpComputerStatus { throw 'Unmocked Defender query' }

Describe 'MWB bounded and allowlisted runtime event diagnostics' {
    It 'keeps structured error codes and matching product names without exporting private event text' {
        $product = 'C:\private\product'
        $event = New-MwbRuntimeEventFixture @'
<Data Name="ErrorCode">0x80070490</Data>
<Data Name="HResult">-2147024671</Data>
<Data Name="Threat Name">Test:Win32/Diagnostic!fixture</Data>
<Data Name="Threat ID">123456</Data>
<Data Name="Path">file:_C:\private\product\PowerToys.MouseWithoutBorders.exe</Data>
<Data Name="CommandLine">WindowsSandbox.exe /password DO_NOT_PUBLISH</Data>
<Data Name="VmlEventLog">WindowsSandbox.exe /password 0xDEADBEEF</Data>
<Data Name="Unknown">private clipboard or key DO_NOT_PUBLISH</Data>
'@
        $result = ConvertTo-MwbRuntimeEvent $event $product
        $result.RecordId | Should Be 123
        $result.ErrorCodes.Count | Should Be 2
        $result.ErrorCodes[0].Value | Should Be '0x80070490'
        $result.ErrorCodes[1].Value | Should Be '0x800700E1'
        $result.ThreatName | Should Be 'Test:Win32/Diagnostic!fixture'
        $result.MatchingEndpointFiles.Count | Should Be 1
        $result.MatchingEndpointFiles[0] | Should Be 'PowerToys.MouseWithoutBorders.exe'
        ($result | ConvertTo-Json -Depth 5) | Should Not Match 'DO_NOT_PUBLISH|DEADBEEF|C:\\private|CommandLine|clipboard'
    }

    It 'accepts named scalar UserData errors but excludes messages and malformed fields' {
        $event = New-MwbRuntimeEventFixture ''
        $event.Xml = '<Event><UserData><Provider><HResult>0x80070490</HResult><Message>DO_NOT_PUBLISH</Message><Status>999999999999</Status></Provider></UserData></Event>'
        $result = ConvertTo-MwbRuntimeEvent $event 'C:\fixture'
        $result.ErrorCodes.Count | Should Be 1
        $result.ErrorCodes[0].Value | Should Be '0x80070490'
        ($result | ConvertTo-Json -Depth 5) | Should Not Match 'DO_NOT_PUBLISH|999999999999'
    }

    It 'does not attribute a similarly prefixed resource to an endpoint executable' {
        $event = New-MwbRuntimeEventFixture '<Data Name="Path">file:_C:\fixture\PowerToys.exe.unrelated</Data>'
        (ConvertTo-MwbRuntimeEvent $event 'C:\fixture').MatchingEndpointFiles.Count | Should Be 0
    }

    It 'bounds event count and time and reports a potentially truncated query' {
        $start = [DateTime]::UtcNow.AddMinutes(-5)
        $end = [DateTime]::UtcNow
        $event = New-MwbRuntimeEventFixture '<Data Name="ErrorCode">0x80070490</Data>'
        Mock Get-WinEvent {
            if ($ListLog) { [pscustomobject]@{ IsEnabled = $true } }
            else { @(1..32 | ForEach-Object { $event }) }
        }
        $result = Get-MwbRuntimeEventLog 'Microsoft-Windows-Hyper-V-Compute-Admin' $start $end 'C:\fixture'
        $result.QueryStatus | Should Be 'Succeeded'
        $result.EventLimitReached | Should Be $true
        $result.Events.Count | Should Be 32
        Assert-MockCalled Get-WinEvent -Times 1 -Exactly -Scope It -ParameterFilter {
            $MaxEvents -eq 32 -and $FilterHashtable.StartTime -eq $start.ToLocalTime() -and
            $FilterHashtable.EndTime -eq $end.ToLocalTime() -and $FilterHashtable.Level.Count -eq 3
        }
    }

    It 'restricts Defender queries to detection and action events' {
        Mock Get-WinEvent { if ($ListLog) { [pscustomobject]@{ IsEnabled = $true } } else { @() } }
        $null = Get-MwbRuntimeEventLog 'Microsoft-Windows-Windows Defender/Operational' ([DateTime]::UtcNow.AddMinutes(-5)) ([DateTime]::UtcNow) 'C:\fixture'
        Assert-MockCalled Get-WinEvent -Times 1 -Exactly -Scope It -ParameterFilter {
            ($FilterHashtable.Id -join ',') -eq '1116,1117,1118,1119,1121' -and $MaxEvents -eq 32
        }
    }

    It 'does not report access denied as an empty successful event query' {
        Mock Get-WinEvent { throw [UnauthorizedAccessException]::new('DO_NOT_PUBLISH') }
        $result = Get-MwbRuntimeEventLog 'Microsoft-Windows-Hyper-V-Compute-Admin' ([DateTime]::UtcNow.AddMinutes(-5)) ([DateTime]::UtcNow) 'C:\fixture'
        $result.QueryStatus | Should Be 'Failed'
        $result.QueryError.Code | Should Be 'RuntimeEventQueryFailed'
        ($result | ConvertTo-Json -Depth 5) | Should Not Match 'DO_NOT_PUBLISH'
    }

    It 'distinguishes a confirmed empty event query from a query failure' {
        Mock Get-WinEvent {
            if ($ListLog) { return [pscustomobject]@{ IsEnabled = $true } }
            throw [Management.Automation.ErrorRecord]::new(
                [InvalidOperationException]::new('No events'),
                'NoMatchingEventsFound,Microsoft.PowerShell.Commands.GetWinEventCommand',
                [Management.Automation.ErrorCategory]::ObjectNotFound, $null)
        }
        $result = Get-MwbRuntimeEventLog 'Microsoft-Windows-Hyper-V-Compute-Admin' ([DateTime]::UtcNow.AddMinutes(-5)) ([DateTime]::UtcNow) 'C:\fixture'
        $result.QueryStatus | Should Be 'Succeeded'
        $result.Events.Count | Should Be 0
        $result.EventLimitReached | Should Be $false
    }

    It 'reports a disabled event channel without enabling it or claiming an empty history' {
        Mock Get-WinEvent { [pscustomobject]@{ IsEnabled = $false } }
        $result = Get-MwbRuntimeEventLog 'Microsoft-Windows-Hyper-V-Compute-Admin' ([DateTime]::UtcNow.AddMinutes(-5)) ([DateTime]::UtcNow) 'C:\fixture'
        $result.QueryStatus | Should Be 'Unavailable'
        $result.QueryError.Code | Should Be 'RuntimeEventLogDisabled'
        $result.LogEnabled | Should Be $false
        Assert-MockCalled Get-WinEvent -Times 0 -Exactly -Scope It -ParameterFilter { $null -ne $FilterHashtable }
    }
}

Describe 'MWB run-scoped runtime report' {
    BeforeEach {
        $id = [guid]::NewGuid()
        $state = @{
            RunId = $id.ToString()
            ProductRoot = "C:\ProgramData\PowerToysMwbSandboxCi\$($id.ToString('N'))\host-product"
            PreparedUtc = [DateTime]::UtcNow.AddMinutes(-5).ToString('o')
        }
        Mock Assert-MwbCiPlainPath {}
        Mock Get-WinEvent { if ($ListLog) { [pscustomobject]@{ IsEnabled = $true } } else { @() } }
        Mock Get-Service { [pscustomobject]@{ Status = 'Running' } }
        Mock Get-Item { [pscustomobject]@{ Length = 123; LastWriteTimeUtc = [DateTime]::UtcNow } }
        Mock Get-FileHash { [pscustomobject]@{ Hash = 'A' * 64 } }
        Mock Get-MpComputerStatus {
            [pscustomobject]@{
                AMProductVersion = '4.18.1.2'; AMEngineVersion = '1.1.1.2'; AntivirusSignatureVersion = '1.2.3.4'
                AMServiceEnabled = $true; AntivirusEnabled = $true; RealTimeProtectionEnabled = $true
                PrivateField = 'DO_NOT_PUBLISH'
            }
        }
    }

    It 'collects the fixed file inventory and protection state without exporting arbitrary provider fields' {
        $result = Get-MwbRuntimeDiagnostics $state $id
        $result.QueryStatus | Should Be 'Succeeded'
        $result.Events.Count | Should Be 5
        $result.Services.Count | Should Be 4
        $result.EndpointFiles.Count | Should Be 6
        $result.EndpointFiles[0].Sha256 | Should Be ('A' * 64)
        $result.Defender.RealTimeProtectionEnabled | Should Be $true
        ($result | ConvertTo-Json -Depth 8) | Should Not Match 'DO_NOT_PUBLISH|PrivateField'
        $serialized = $result | ConvertTo-Json -Depth 8 | ConvertFrom-Json
        $serialized.Events[0].LogName | Should Be 'Microsoft-Windows-Hyper-V-Compute-Admin'
        $serialized.Events[0].QueryStatus | Should Be 'Succeeded'
        $serialized.EndpointFiles[0].Path | Should Be 'PowerToys.exe'
        $serialized.EndpointFiles[0].Sha256 | Should Be ('A' * 64)
    }

    It 'rejects another run or an unrelated product directory before reading files or events' {
        { Get-MwbRuntimeDiagnostics $state ([guid]::NewGuid()) } | Should Throw 'another run'
        $state.ProductRoot = 'C:\unrelated'
        { Get-MwbRuntimeDiagnostics $state $id } | Should Throw 'not this CI run'
        Assert-MockCalled Get-WinEvent -Times 0 -Exactly -Scope It
        Assert-MockCalled Get-FileHash -Times 0 -Exactly -Scope It
    }

    It 'caps an old marker to two hours and rejects a future timestamp' {
        $state.PreparedUtc = [DateTime]::UtcNow.AddDays(-1).ToString('o')
        $result = Get-MwbRuntimeDiagnostics $state $id
        $result.TimeWindowClamped | Should Be $true
        (([DateTime]$result.CompletedUtc) - ([DateTime]$result.StartedUtc)).TotalHours | Should Be 2
        $state.PreparedUtc = [DateTime]::UtcNow.AddHours(1).ToString('o')
        { Get-MwbRuntimeDiagnostics $state $id } | Should Throw 'reversed'
    }

    It 'records a blocked file hash as partial evidence rather than bypassing the read' {
        Mock Get-FileHash { throw [IO.IOException]::new('DO_NOT_PUBLISH antivirus block') }
        $result = Get-MwbRuntimeDiagnostics $state $id
        $result.QueryStatus | Should Be 'Partial'
        @($result.EndpointFiles | Where-Object QueryStatus -EQ 'Failed').Count | Should Be 6
        $result.EndpointFiles[0].QueryError.Code | Should Be 'RuntimeFileQueryFailed'
        ($result | ConvertTo-Json -Depth 8) | Should Not Match 'DO_NOT_PUBLISH'
        Assert-MockCalled Get-FileHash -Times 6 -Exactly -Scope It
    }

    It 'publishes an explicit failure report when the ownership marker cannot be read' {
        $root = Join-Path $TestDrive 'results'
        Mock Get-Item { throw [UnauthorizedAccessException]::new('DO_NOT_PUBLISH') }
        Export-MwbRuntimeDiagnostics $root $id
        $result = Get-Content (Join-Path $root "mwb-prerequisites-$id\runtime-diagnostics.json") -Raw | ConvertFrom-Json
        $result.QueryStatus | Should Be 'Failed'
        $result.QueryError.Code | Should Be 'RuntimeDiagnosticCollectionFailed'
        Assert-MockCalled Get-WinEvent -Times 0 -Exactly -Scope It
    }

    It 'does not present unavailable Defender status as disabled protection' {
        Mock Get-MpComputerStatus { throw [UnauthorizedAccessException]::new('DO_NOT_PUBLISH') }
        $result = Get-MwbRuntimeDiagnostics $state $id
        $result.QueryStatus | Should Be 'Partial'
        $result.Defender.QueryStatus | Should Be 'Failed'
        $result.Defender.Contains('RealTimeProtectionEnabled') | Should Be $false
        ($result | ConvertTo-Json -Depth 8) | Should Not Match 'DO_NOT_PUBLISH'
    }

    It 'distinguishes an absent optional service from a failed status query' {
        Mock Get-Service {
            throw [Management.Automation.ErrorRecord]::new(
                [InvalidOperationException]::new('No service'),
                'NoServiceFoundForGivenName,Microsoft.PowerShell.Commands.GetServiceCommand',
                [Management.Automation.ErrorCategory]::ObjectNotFound, $null)
        }
        $result = Get-MwbRuntimeDiagnostics $state $id
        $result.QueryStatus | Should Be 'Succeeded'
        @($result.Services | Where-Object Present -EQ $false).Count | Should Be 4
    }

    It 'runs only after failure and before recovery with a bounded pipeline deadline' {
        $yaml = Get-Content (Join-Path $PSScriptRoot '..\v2\templates\steps-mwb-sandbox-experiment.yml') -Raw
        $capture = $yaml.IndexOf('Export-MwbRuntimeDiagnostics.ps1', [StringComparison]::Ordinal)
        $recovery = $yaml.IndexOf('-Mode Recover', [StringComparison]::Ordinal)
        ($capture -gt 0 -and $capture -lt $recovery) | Should Be $true
        $yaml.Substring($capture, $recovery - $capture) |
            Should Match "condition: and\(failed\(\), eq\(variables\['RunMwbSandbox'\], 'true'\)\)"
        $yaml.Substring($capture, $recovery - $capture) | Should Match 'timeoutInMinutes: 2'
    }
}

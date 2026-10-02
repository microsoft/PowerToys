# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

#Requires -Version 7.0
. "$PSScriptRoot\..\MwbSandboxCi.Common.ps1"
$tokens = $null
$parseErrors = $null
$exportAst = [Management.Automation.Language.Parser]::ParseFile(
    (Join-Path $PSScriptRoot '..\Export-MwbSandboxPrerequisites.ps1'), [ref]$tokens, [ref]$parseErrors)
if ($parseErrors) { throw 'Prerequisite exporter contains parse errors.' }
foreach ($name in @('Copy-MwbDiagnosticFields', 'ConvertTo-MwbPublicUserPrerequisites',
    'Get-MwbUserPrerequisiteFiles', 'Export-MwbSandboxPrerequisites')) {
    $definition = $exportAst.Find({
        param($node)
        $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq $name
    }, $true)
    . ([scriptblock]::Create($definition.Extent.Text))
}

function New-MwbUserPrerequisiteFixture {
    $stages = [ordered]@{}
    foreach ($name in @('OperatingSystem', 'UserPackageRegistration', 'UserExecutionAlias',
        'PackageExecutable', 'PrivateToolAndState', 'CliSchema', 'ProviderVersion')) {
        $stages[$name] = [ordered]@{ Status = 'NotChecked' }
    }
    $stages.OperatingSystem = @{ Status = 'Passed'; Version = '10.0.26100.0' }
    $stages.UserPackageRegistration = @{
        Status = 'Failed'; QueryStatus = 'Succeeded'; Present = $false; Count = 0; Packages = @()
        ErrorCodes = @('trusted_provider_unavailable'); ErrorHResults = @('0x80004005')
    }
    [ordered]@{
        SchemaVersion = 1; Scope = 'CurrentInteractiveUser'; UserAccount = 'FIXTURE\ShineTest'
        UserSid = 'S-1-5-21-123-456-789-1001'; IsElevated = $false; IsSystem = $false
        SessionId = 1; ProcessId = 1234; ProcessArchitecture = 'X64'; OsArchitecture = 'X64'
        Stages = $stages
    }
}

Describe 'MWB always-exported prerequisite evidence' {
    BeforeEach {
        $root = (New-Item -ItemType Directory -Path (Join-Path $TestDrive ([guid]::NewGuid().ToString('N')))).FullName
        $id = [guid]::NewGuid()
        $published = Join-Path $root "mwb-prerequisites-$id"
        $launcher = Join-Path $root "ui-$($id.ToString('N').Substring(0, 12))"
        $fixture = New-MwbUserPrerequisiteFixture
    }

    It 'collects the exact filename emitted by the runtime report producer' {
        $producer = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\..\src\modules\MouseWithoutBorders\MouseWithoutBorders.UITests\WinAppSandboxPrerequisiteReport.cs') -Raw
        $producer | Should Match 'public const string FileName = "winapp-prerequisites.json";'
        (Get-Command Get-MwbUserPrerequisiteFiles).Definition | Should Match "-Filter 'winapp-prerequisites.json'"
    }

    It 'creates both NotChecked reports and a summary when Prepare never ran and no TRX exists' {
        Export-MwbSandboxPrerequisites $root $id
        $admin = Get-Content (Join-Path $published 'prerequisite-admin.json') -Raw | ConvertFrom-Json
        $user = Get-Content (Join-Path $published 'prerequisite-user.json') -Raw | ConvertFrom-Json
        $admin.Status | Should Be 'NotChecked'
        $admin.Reason | Should Be 'PrepareDidNotReachInventory'
        $user.Status | Should Be 'NotChecked'
        $user.Reason | Should Be 'PrepareDidNotReachInventory'
        $setup = Get-Content (Join-Path $published 'client-setup.json') -Raw | ConvertFrom-Json
        $setup.Status | Should Be 'NotChecked'
        $setup.Before.Status | Should Be 'NotChecked'
        Test-Path (Join-Path $published 'summary.txt') | Should Be $true
        @(Get-ChildItem $root -Filter '*.trx' -Recurse).Count | Should Be 0
    }

    It 'preserves explicit setup failure evidence when no Prepare or TRX was produced' {
        $null = New-Item -ItemType Directory -Path $published
        @{ SchemaVersion = 1; Status = 'Failed'; ErrorCode = 'ClientSetupDeadlineExceeded' } |
            ConvertTo-Json | Set-Content (Join-Path $published 'client-setup.json')
        Export-MwbSandboxPrerequisites $root $id
        $setup = Get-Content (Join-Path $published 'client-setup.json') -Raw | ConvertFrom-Json
        $setup.ErrorCode | Should Be 'ClientSetupDeadlineExceeded'
        $setup.Status | Should Be 'Failed'
        @(Get-ChildItem $root -Filter '*.trx' -Recurse).Count | Should Be 0
    }

    It 'marks a missing setup report as Legacy-skipped on the Win10 tier' {
        $null = New-Item -ItemType Directory -Path $published
        @{ SchemaVersion = 3; Platform = 'x64Win10' } |
            ConvertTo-Json | Set-Content (Join-Path $published 'prerequisite-admin.json')
        Export-MwbSandboxPrerequisites $root $id
        (Get-Content (Join-Path $published 'client-setup.json') -Raw | ConvertFrom-Json).Status | Should Be 'SkippedLegacy'
    }

    It 'exports a Limited-user failed preflight before the ordinary fixture run directory or TRX exists' {
        $null = New-Item -ItemType Directory -Path $launcher
        $fixture | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $launcher 'prerequisite-user.json')
        Set-Content (Join-Path $launcher 'stdout.log') -Value 'DO_NOT_PUBLISH credentials'
        Export-MwbSandboxPrerequisites $root $id
        $user = Get-Content (Join-Path $published 'prerequisite-user.json') -Raw | ConvertFrom-Json
        $user.Scope | Should Be 'CurrentInteractiveUser'
        $user.UserAccount | Should Be 'ShineTest'
        $user.IsElevated | Should Be $false
        $user.ContextStatus | Should Be 'Passed'
        $user.Overall | Should Be 'Failed'
        $user.Stages.UserPackageRegistration.Status | Should Be 'Failed'
        $user.Stages.UserPackageRegistration.Count | Should Be 0
        $user.Stages.ProviderVersion.Status | Should Be 'NotChecked'
        $user.Stages.UserPackageRegistration.ErrorHResults[0] | Should Be '0x80004005'
        Get-Content (Join-Path $published 'summary.txt') -Raw | Should Match 'Interactive prerequisites: Failed'
        Test-Path (Join-Path $launcher "mwb-$id") | Should Be $false
        @(Get-ChildItem $published -File).Count | Should Be 4
        Get-Content (Join-Path $published 'prerequisite-user.json') -Raw | Should Not Match 'DO_NOT_PUBLISH|FIXTURE|ProcessId'
    }

    It 'rejects an admin, SYSTEM or session-zero context as evidence of Limited-user readiness' -TestCases @(
        @{ Field = 'IsElevated'; Value = $true }; @{ Field = 'IsSystem'; Value = $true }
        @{ Field = 'SessionId'; Value = 0 }
    ) {
        param($Field, $Value)
        $fixture[$Field] = $Value
        $result = ConvertTo-MwbPublicUserPrerequisites $fixture
        $result.ContextStatus | Should Be 'Failed'
        $result.ContextFailureCode | Should Be 'interactive_identity_not_limited'
    }

    It 'does not confuse an elevated explicit-SID inventory with execution as that test user' {
        $fixture.Scope = 'ElevatedPrepareInventory'
        { ConvertTo-MwbPublicUserPrerequisites $fixture } | Should Throw 'actual interactive identity context'
    }

    It 'fails the context when the reported user differs from the exact admin-inventoried SID' {
        $result = ConvertTo-MwbPublicUserPrerequisites $fixture -ExpectedUserSid 'S-1-5-21-123-456-789-9999'
        $result.ContextStatus | Should Be 'Failed'
        $result.ContextFailureCode | Should Be 'interactive_user_mismatch'
    }

    It 'preserves query failures without claiming package absence' {
        $fixture.Stages.UserPackageRegistration = @{
            Status = 'Failed'; QueryStatus = 'Failed'; Present = $null; Count = $null; Packages = $null
            FailureCode = 'com_query_failed'; QueryErrorHResult = '0x80070005'
            ErrorCodes = @('com_query_failed'); ErrorHResults = @('0x80070005')
        }
        $result = ConvertTo-MwbPublicUserPrerequisites $fixture
        $result.Stages.UserPackageRegistration.QueryStatus | Should Be 'Failed'
        $result.Stages.UserPackageRegistration.Present | Should Be $null
        $result.Stages.UserPackageRegistration.Count | Should Be $null
        $result.Stages.UserPackageRegistration.ErrorCodes[0] | Should Be 'com_query_failed'
        $result.Stages.UserPackageRegistration.FailureCode | Should Be 'com_query_failed'
        $result.Stages.UserPackageRegistration.QueryErrorHResult | Should Be '0x80070005'
    }

    It 'allowlists fields instead of copying raw commands, exceptions, paths or authentication state' {
        $fixture.RawException = 'DO_NOT_PUBLISH raw exception'
        $fixture.Stages.ProviderVersion = @{
            Status = 'Failed'; ExitCode = $null; TimedOut = $true; TimeoutSeconds = 15
            ErrorCodes = @('command_timeout'); ErrorHResults = @('0x80004005')
            CommandLine = 'DO_NOT_PUBLISH WindowsSandboxClient --token sensitive'
        }
        $fixture.Stages.PackageExecutable.InstalledLocation = 'DO_NOT_PUBLISH private path'
        $result = ConvertTo-MwbPublicUserPrerequisites $fixture
        ($result | ConvertTo-Json -Depth 10) | Should Not Match 'DO_NOT_PUBLISH|WindowsSandboxClient|CommandLine|RawException|InstalledLocation'
        $result.Stages.ProviderVersion.TimedOut | Should Be $true
        $result.Stages.ProviderVersion.TimeoutSeconds | Should Be 15
    }

    It 'records malformed or unsafe source reports as collection failures without exposing their content' -TestCases @(
        @{ Kind = 'InvalidJson' }; @{ Kind = 'UnsafeVersion' }
    ) {
        param($Kind)
        $null = New-Item -ItemType Directory -Path $launcher
        $source = Join-Path $launcher 'prerequisite-user.json'
        if ($Kind -eq 'InvalidJson') { Set-Content $source 'DO_NOT_PUBLISH invalid json' }
        else {
            $fixture.Stages.ProviderVersion.VersionLine = 'DO_NOT_PUBLISH secret=token'
            $fixture | ConvertTo-Json -Depth 8 | Set-Content $source
        }
        Export-MwbSandboxPrerequisites $root $id
        $raw = Get-Content (Join-Path $published 'prerequisite-user.json') -Raw
        $raw | Should Not Match 'DO_NOT_PUBLISH|secret=token'
        ($raw | ConvertFrom-Json).QueryError.Code | Should Be 'InteractiveReportCollectionFailed'
    }

    It 'records unreached user checks when only early admin inventory exists' {
        $null = New-Item -ItemType Directory -Path $published
        @{ SchemaVersion = 3; InventoryConclusion = 'AbsentEverywhere' } |
            ConvertTo-Json | Set-Content (Join-Path $published 'prerequisite-admin.json')
        Export-MwbSandboxPrerequisites $root $id
        $user = Get-Content (Join-Path $published 'prerequisite-user.json') -Raw | ConvertFrom-Json
        $user.Status | Should Be 'NotChecked'
        $user.Reason | Should Be 'InteractiveProbeNotReached'
        Get-Content (Join-Path $published 'summary.txt') -Raw | Should Match 'AbsentEverywhere'
    }

    It 'replaces an incomplete administrator report with safe failure evidence instead of printing raw parse errors' {
        $null = New-Item -ItemType Directory -Path $published
        Set-Content (Join-Path $published 'prerequisite-admin.json') -Value 'DO_NOT_PUBLISH invalid json'
        Export-MwbSandboxPrerequisites $root $id
        $raw = Get-Content (Join-Path $published 'prerequisite-admin.json') -Raw
        $raw | Should Not Match 'DO_NOT_PUBLISH'
        ($raw | ConvertFrom-Json).QueryError.Code | Should Be 'AdminReportCollectionFailed'
        Test-Path (Join-Path $published 'summary.txt') | Should Be $true
    }

    It 'collects persistent invocation reports recursively before fixture identity and root exist' {
        $invocation = [guid]::NewGuid().ToString('N')
        $sourceDirectory = Join-Path $launcher "TestResults\mwb-preflight-$invocation"
        $null = New-Item -ItemType Directory -Path $sourceDirectory -Force
        $fixture.InvocationId = $invocation
        $fixture.RunId = $null
        $fixture | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $sourceDirectory 'winapp-prerequisites.json')
        Export-MwbSandboxPrerequisites $root $id
        $user = Get-Content (Join-Path $published 'prerequisite-user.json') -Raw | ConvertFrom-Json
        $user.InvocationId | Should Be $invocation
        $user.RunId | Should Be $null
        $user.Stages.UserPackageRegistration.Status | Should Be 'Failed'
        Test-Path (Join-Path $launcher "mwb-$id") | Should Be $false
    }

    It 'retains every current-job invocation instead of silently choosing one report' {
        foreach ($number in 1..2) {
            $invocation = [guid]::NewGuid().ToString('N')
            $sourceDirectory = Join-Path $launcher "mwb-preflight-$invocation"
            $null = New-Item -ItemType Directory -Path $sourceDirectory -Force
            $fixture.InvocationId = $invocation
            $fixture.RunId = $id.ToString()
            $fixture | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $sourceDirectory 'winapp-prerequisites.json')
        }
        Export-MwbSandboxPrerequisites $root $id
        $user = Get-Content (Join-Path $published 'prerequisite-user.json') -Raw | ConvertFrom-Json
        $user.Reports.Count | Should Be 2
        $user.Overall | Should Be 'Failed'
    }

    It 'never discovers reports from another launcher job or trusts mismatched run identities' {
        $invocation = [guid]::NewGuid().ToString('N')
        $otherJob = [guid]::NewGuid()
        $otherDirectory = Join-Path $root "ui-$($otherJob.ToString('N').Substring(0, 12))\mwb-preflight-$invocation"
        $null = New-Item -ItemType Directory -Path $otherDirectory -Force
        $fixture.InvocationId = $invocation
        $fixture.RunId = $otherJob.ToString()
        $fixture | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $otherDirectory 'winapp-prerequisites.json')
        Export-MwbSandboxPrerequisites $root $id
        (Get-Content (Join-Path $published 'prerequisite-user.json') -Raw | ConvertFrom-Json).Status | Should Be 'NotChecked'

        $null = New-Item -ItemType Directory -Path $launcher
        Copy-Item -LiteralPath $otherDirectory -Destination $launcher -Recurse
        Export-MwbSandboxPrerequisites $root $id
        (Get-Content (Join-Path $published 'prerequisite-user.json') -Raw | ConvertFrom-Json).QueryError.Code |
            Should Be 'InteractiveReportCollectionFailed'
    }
}

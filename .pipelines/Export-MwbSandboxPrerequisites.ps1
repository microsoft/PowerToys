# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

#Requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $ResultsDirectory,
    [Parameter(Mandatory)][guid] $RunId
)

$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\MwbSandboxCi.Common.ps1"

function Copy-MwbDiagnosticFields {
    param([Collections.IDictionary] $Source, [Collections.IDictionary] $Target, [Collections.IDictionary] $Fields)

    foreach ($name in $Fields.Keys) {
        if (-not $Source.Contains($name)) { continue }
        $value = $Source[$name]
        if ($null -ne $value) {
            $valid = switch ($Fields[$name]) {
                'Boolean' { $value -is [bool] }
                'Number' { ($value -is [int] -or $value -is [long] -or $value -is [double]) -and [double]::IsFinite($value) }
                default { $value -is [string] -and $value.Length -le 256 -and $value -cmatch $Fields[$name] }
            }
            if (-not $valid) { throw 'The prerequisite report contains an invalid diagnostic field.' }
        }
        $Target[$name] = $value
    }
}

function ConvertTo-MwbPublicUserPrerequisites {
    param([Collections.IDictionary] $Report, [string] $ExpectedUserSid)

    if ($Report.SchemaVersion -ne 1 -or $Report.Scope -cne 'CurrentInteractiveUser' -or
        $Report.IsElevated -isnot [bool] -or $Report.IsSystem -isnot [bool] -or
        $Report.UserSid -isnot [string] -or $Report.UserSid -cnotmatch '^S-1-(?:\d+-)+\d+$' -or
        ($Report.SessionId -isnot [int] -and $Report.SessionId -isnot [long]) -or
        $Report.Stages -isnot [Collections.IDictionary]) {
        throw 'The prerequisite report lacks the actual interactive identity context.'
    }
    $result = [ordered]@{ SchemaVersion = 1; Scope = 'CurrentInteractiveUser' }
    Copy-MwbDiagnosticFields $Report $result @{
        UserSid = '^S-1-(?:\d+-)+\d+$'; SessionId = 'Number'; IsElevated = 'Boolean'; IsSystem = 'Boolean'
        ProcessArchitecture = '^(?:X64|X86|Arm64|Arm)$'; OsArchitecture = '^(?:X64|X86|Arm64|Arm)$'
        InvocationId = '^[0-9a-f]{32}$'
        RunId = '^[0-9a-fA-F]{8}-(?:[0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12}$'
    }
    $account = ([string]$Report.UserAccount -split '\\')[-1]
    $result.UserAccount = if ($account -match '^[\p{L}\p{N}_. -]{1,64}$') { $account } else { 'OtherUser' }
    $result.ContextStatus = 'Passed'
    if ($Report.IsElevated -or $Report.IsSystem -or $Report.SessionId -le 0) {
        $result.ContextStatus = 'Failed'
        $result.ContextFailureCode = 'interactive_identity_not_limited'
    } elseif ($ExpectedUserSid -and $Report.UserSid -cne $ExpectedUserSid) {
        $result.ContextStatus = 'Failed'
        $result.ContextFailureCode = 'interactive_user_mismatch'
    }
    $stages = [ordered]@{}
    foreach ($name in @('OperatingSystem', 'UserPackageRegistration', 'UserExecutionAlias',
        'PackageExecutable', 'PrivateToolAndState', 'CliSchema', 'ProviderVersion')) {
        $source = $Report.Stages[$name]
        if ($source -isnot [Collections.IDictionary] -or $source.Status -cnotin @('NotChecked', 'Passed', 'Failed')) {
            throw 'The prerequisite report has a missing or invalid stage.'
        }
        $stage = [ordered]@{ Status = $source.Status }
        foreach ($field in @('ErrorCodes', 'ErrorHResults')) {
            if (-not $source.Contains($field)) { continue }
            $pattern = if ($field -eq 'ErrorCodes') { '^[a-z][a-z0-9_]{0,95}$' } else { '^0x[0-9A-F]{8}$' }
            if (@($source[$field]).Count -gt 16) { throw 'Too many prerequisite error codes.' }
            $stage[$field] = @($source[$field] | ForEach-Object {
                if ($_ -isnot [string] -or $_ -cnotmatch $pattern) { throw 'Invalid prerequisite error code.' }
                $_
            })
        }
        Copy-MwbDiagnosticFields $source $stage @{
            FailureCode = '^[a-z][a-z0-9_]{0,95}$'
            QueryErrorHResult = '^0x[0-9A-F]{8}$'
            Win32ErrorCode = 'Number'
        }
        switch ($name) {
            'OperatingSystem' { Copy-MwbDiagnosticFields $source $stage @{ Version = '^\d+\.\d+\.\d+(?:\.\d+)?$' } }
            'UserPackageRegistration' {
                Copy-MwbDiagnosticFields $source $stage @{
                    QueryStatus = '^(?:NotChecked|Succeeded|Failed)$'
                    PackageFamily = '^MicrosoftWindows\.WindowsSandbox_cw5n1h2txyewy$'
                    Count = 'Number'; Present = 'Boolean'
                }
                if ($null -ne $source.Packages) {
                    if (@($source.Packages).Count -gt 32) { throw 'Too many Sandbox package records.' }
                    $stage.Packages = @($source.Packages | ForEach-Object {
                        $package = [ordered]@{}
                        Copy-MwbDiagnosticFields $_ $package @{
                            Name = '^MicrosoftWindows\.WindowsSandbox$'
                            FullName = '^MicrosoftWindows\.WindowsSandbox_\d+\.\d+\.\d+\.\d+_(?:x64|arm64|x86|neutral)_[A-Za-z0-9.~]*_cw5n1h2txyewy$'
                            FamilyName = '^MicrosoftWindows\.WindowsSandbox_cw5n1h2txyewy$'
                            Version = '^\d+\.\d+\.\d+\.\d+$'
                            Architecture = '^(?:X64|X86|Arm64|Arm|Neutral)$'
                        }
                        $status = [ordered]@{}
                        Copy-MwbDiagnosticFields $_.Status $status @{
                            IsOk = 'Boolean'; Disabled = 'Boolean'; NotAvailable = 'Boolean'
                            NeedsRemediation = 'Boolean'; DependencyIssue = 'Boolean'
                        }
                        $package.Status = $status
                        $package
                    })
                }
            }
            'UserExecutionAlias' { Copy-MwbDiagnosticFields $source $stage @{ Exists = 'Boolean'; IsReparsePoint = 'Boolean' } }
            'PackageExecutable' { Copy-MwbDiagnosticFields $source $stage @{ Exists = 'Boolean'; Machine = '^(?:Amd64|Arm64|I386)$' } }
            'ProviderVersion' {
                Copy-MwbDiagnosticFields $source $stage @{
                    VersionLine = '^wsb \d{1,5}(?:\.\d{1,5}){2,3}$'
                    ExitCode = 'Number'; TimedOut = 'Boolean'; TimeoutSeconds = 'Number'
                }
            }
        }
        $stages[$name] = $stage
    }
    $result.Stages = $stages
    $result.Overall = if ($result.ContextStatus -eq 'Failed' -or @($stages.Values | Where-Object Status -EQ 'Failed').Count) { 'Failed' }
        elseif (@($stages.Values | Where-Object Status -EQ 'NotChecked').Count) { 'NotChecked' }
        else { 'Passed' }
    $result
}

function Get-MwbUserPrerequisiteFiles {
    param([string] $LauncherDirectory)

    Assert-MwbCiPlainPath $LauncherDirectory
    if (-not (Test-Path -LiteralPath $LauncherDirectory -PathType Container)) { return }
    $files = @(Get-ChildItem -LiteralPath $LauncherDirectory -Filter 'winapp-prerequisites.json' -File -Recurse -Force)
    if ($files.Count -gt 64) { throw 'Too many interactive prerequisite reports for one job.' }
    foreach ($file in $files) {
        Assert-MwbCiPlainPath $file.FullName
        if ($file.Directory.Name -cnotmatch '^mwb-preflight-[0-9a-f]{32}$') {
            throw 'The interactive prerequisite report is not in an invocation-specific directory.'
        }
        $file.FullName
    }
    if (-not $files.Count) {
        # Accept the original fixed-file contract while callers move to per-invocation
        # reports. Discovery stays inside the exact job's launcher directory.
        $legacy = Join-Path $LauncherDirectory 'prerequisite-user.json'
        Assert-MwbCiPlainPath $legacy
        if (Test-Path -LiteralPath $legacy -PathType Leaf) { $legacy }
    }
}

function Export-MwbSandboxPrerequisites {
    param([string] $ResultsDirectory, [guid] $RunId)

    $directory = Get-MwbPrerequisiteDirectory $ResultsDirectory $RunId
    Assert-MwbCiPlainPath $directory
    $null = New-Item -ItemType Directory -Path $directory -Force
    $adminPath = Join-Path $directory 'prerequisite-admin.json'
    Assert-MwbCiPlainPath $adminPath
    if (-not (Test-Path -LiteralPath $adminPath -PathType Leaf)) {
        [ordered]@{
            SchemaVersion = 3; CollectionContext = 'ElevatedPrepareInventory'
            Status = 'NotChecked'; Reason = 'PrepareDidNotReachInventory'
            InteractiveProbe = [ordered]@{ Status = 'NotChecked'; Reason = 'PrepareDidNotReachInventory' }
        } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $adminPath -Encoding utf8
    }
    try {
        if ((Get-Item -LiteralPath $adminPath).Length -gt 1MB) { throw 'The administrator report exceeds its bounded size.' }
        $admin = Get-Content -LiteralPath $adminPath -Raw | ConvertFrom-Json -AsHashtable
        if ($admin.SchemaVersion -ne 3) { throw 'The administrator report schema is not supported.' }
    }
    catch {
        $admin = [ordered]@{
            SchemaVersion = 3; CollectionContext = 'ElevatedPrepareInventory'; Status = 'QueryFailed'
            QueryError = Get-MwbDiagnosticError $_ 'AdminReportCollectionFailed'
        }
        $admin | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $adminPath -Encoding utf8
    }
    $expectedSid = if ($admin.InteractiveUser) { [string]$admin.InteractiveUser.UserSid } else { '' }
    $launcher = Join-Path $ResultsDirectory "ui-$($RunId.ToString('N').Substring(0, 12))"
    $user = [ordered]@{
        SchemaVersion = 1; Scope = 'CurrentInteractiveUser'; Status = 'NotChecked'
        Reason = if ($admin.Status -eq 'NotChecked') { 'PrepareDidNotReachInventory' } else { 'InteractiveProbeNotReached' }
    }
    try {
        $sources = @(Get-MwbUserPrerequisiteFiles $launcher)
        if ($sources.Count) {
            $reports = @($sources | ForEach-Object {
                if ((Get-Item -LiteralPath $_).Length -gt 1MB) { throw 'The prerequisite report exceeds its bounded size.' }
                $data = Get-Content -LiteralPath $_ -Raw | ConvertFrom-Json -AsHashtable
                if ([IO.Path]::GetFileName($_) -ceq 'winapp-prerequisites.json' -and
                    (Split-Path (Split-Path $_ -Parent) -Leaf) -cne "mwb-preflight-$($data.InvocationId)") {
                    throw 'The prerequisite report does not match its invocation directory.'
                }
                if ($data.RunId -and $data.RunId -ine $RunId.ToString()) {
                    throw 'The prerequisite report belongs to another CI run.'
                }
                ConvertTo-MwbPublicUserPrerequisites $data -ExpectedUserSid $expectedSid
            })
            $user = if ($reports.Count -eq 1) { $reports[0] } else {
                [ordered]@{
                    SchemaVersion = 1; Scope = 'CurrentInteractiveUser'; Reports = $reports
                    Overall = if (@($reports | Where-Object Overall -EQ 'Failed').Count) { 'Failed' }
                        elseif (@($reports | Where-Object Overall -EQ 'NotChecked').Count) { 'NotChecked' }
                        else { 'Passed' }
                }
            }
        }
    }
    catch {
        $user = [ordered]@{
            SchemaVersion = 1; Scope = 'CurrentInteractiveUser'; Status = 'QueryFailed'
            QueryError = Get-MwbDiagnosticError $_ 'InteractiveReportCollectionFailed'
        }
    }
    $user | ConvertTo-Json -Depth 10 |
        Set-Content -LiteralPath (Join-Path $directory 'prerequisite-user.json') -Encoding utf8
    $summary = @(
        'Windows Sandbox prerequisites: administrator inventory and actual interactive-user checks are separate.'
        'winapp CLI does not install or register the MicrosoftWindows.WindowsSandbox client.'
        "Administrator report: $(if ($admin.Status) { $admin.Status } else { 'Collected' })."
        "Package inventory: $(if ($admin.InventoryConclusion) { $admin.InventoryConclusion } else { 'NotChecked' })."
        "Interactive prerequisites: $(if ($user.Status) { $user.Status } else { $user.Overall })."
        'See prerequisite-admin.json and prerequisite-user.json for allowlisted evidence and unreached stages.'
        'Raw provisioning logs, initializer markers, commands and authentication state are not published.'
    )
    $summary | Set-Content -LiteralPath (Join-Path $directory 'summary.txt') -Encoding utf8
}

Export-MwbSandboxPrerequisites -ResultsDirectory $ResultsDirectory -RunId $RunId

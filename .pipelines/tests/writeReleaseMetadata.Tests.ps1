# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.

$scriptPath = Join-Path $PSScriptRoot "..\writeReleaseMetadata.ps1"

function Assert-Throws {
    param([scriptblock]$Action)

    $threw = $false
    try {
        & $Action | Out-Null
    }
    catch {
        $threw = $true
    }
    $threw | Should Be $true
}

Describe "writeReleaseMetadata" {
    It "writes the authoritative preview candidate contract" {
        $path = Join-Path $TestDrive "release-metadata.json"
        $result = & $scriptPath `
            -DefinitionId 76541 `
            -BuildId 154000000 `
            -BuildNumber "PowerToys Signed YAML Release Build_2608.06001-main" `
            -Version "0.101.2181.0" `
            -Channel "preview" `
            -Intent "preview-release" `
            -SourceBranch "refs/heads/main" `
            -SourceCommit "0123456789abcdef0123456789abcdef01234567" `
            -BuildReason "Schedule" `
            -ShouldPublishPreview "True" `
            -QueuedAt "2026-08-06T06:00:00Z" `
            -StartedAt "2026-08-06T06:00:20Z" `
            -OutputPath $path

        $result.shouldPublishPreview | Should Be $true
        $stored = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
        $stored.definitionId | Should Be 76541
        $stored.version | Should Be "0.101.2181.0"
        $stored.sourceCommit | Should Be "0123456789abcdef0123456789abcdef01234567"
    }

    It "rejects inconsistent preview intent and channel" {
        Assert-Throws {
            & $scriptPath `
                -DefinitionId 76541 `
                -BuildId 154000000 `
                -BuildNumber "build" `
                -Version "0.101.2181.0" `
                -Channel "stable" `
                -Intent "preview-release" `
                -SourceBranch "refs/heads/stable" `
                -SourceCommit "0123456789abcdef0123456789abcdef01234567" `
                -BuildReason "Manual" `
                -ShouldPublishPreview "True" `
                -OutputPath (Join-Path $TestDrive "invalid.json")
        }
    }

    It "allows a non-publishing preview validation build" {
        $result = & $scriptPath `
            -DefinitionId 76541 `
            -BuildId 154000001 `
            -BuildNumber "manual-main" `
            -Version "0.101.2182.0" `
            -Channel "preview" `
            -Intent "preview-validation" `
            -SourceBranch "refs/heads/main" `
            -SourceCommit "1123456789abcdef0123456789abcdef01234567" `
            -BuildReason "Manual" `
            -ShouldPublishPreview "False" `
            -OutputPath (Join-Path $TestDrive "preview-validation.json")

        $result.intent | Should Be "preview-validation"
        $result.shouldPublishPreview | Should Be $false
    }

    It "records private validation metadata from a feature branch" {
        $result = & $scriptPath `
            -DefinitionId 76541 `
            -BuildId 154000002 `
            -BuildNumber "feature-build" `
            -Version "0.0.21801.0" `
            -Channel "private" `
            -Intent "private-validation" `
            -SourceBranch "refs/heads/user/feature" `
            -SourceCommit "2123456789abcdef0123456789abcdef01234567" `
            -BuildReason "Manual" `
            -ShouldPublishPreview "False" `
            -OutputPath (Join-Path $TestDrive "private-validation.json")

        $result.sourceBranch | Should Be "refs/heads/user/feature"
        $result.channel | Should Be "private"
    }
}

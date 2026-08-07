# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.

$scripts = Join-Path $PSScriptRoot "..\scripts"

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

function Invoke-TestGit {
    param(
        [Parameter(Mandatory)][string]$Repository,
        [Parameter(ValueFromRemainingArguments)][string[]]$Arguments
    )

    $output = & git -C $Repository @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "git $($Arguments -join ' ') failed: $($output -join "`n")"
    }
    return $output
}

function Add-TestCommit {
    param(
        [Parameter(Mandatory)][string]$Repository,
        [Parameter(Mandatory)][string]$FileName,
        [Parameter(Mandatory)][string]$Content,
        [Parameter(Mandatory)][string]$Message
    )

    Set-Content -LiteralPath (Join-Path $Repository $FileName) -Value $Content
    Invoke-TestGit -Repository $Repository add $FileName | Out-Null
    Invoke-TestGit -Repository $Repository commit -q -m $Message | Out-Null
    return ([string](Invoke-TestGit -Repository $Repository rev-parse HEAD)).Trim()
}

Describe "preview release build metadata" {
    It "resolves a valid candidate from offline metadata" {
        $buildPath = Join-Path $TestDrive "build.json"
        $metadataPath = Join-Path $TestDrive "release-metadata.json"
        @'
{
  "id": 154000000,
  "definition": { "id": 76541 },
  "buildNumber": "PowerToys Signed YAML Release Build_2608.06001-main",
  "result": "succeeded",
  "sourceBranch": "refs/heads/main",
  "sourceVersion": "0123456789abcdef0123456789abcdef01234567",
  "reason": "schedule",
  "queueTime": "2026-08-06T06:00:00Z",
  "startTime": "2026-08-06T06:00:20Z",
  "finishTime": "2026-08-06T09:00:00Z",
  "templateParameters": {}
}
'@ | Set-Content -LiteralPath $buildPath
        @'
{
  "schemaVersion": 1,
  "definitionId": 76541,
  "buildId": 154000000,
  "version": "0.101.2181.0",
  "channel": "preview",
  "intent": "preview-release",
  "sourceBranch": "refs/heads/main",
  "sourceCommit": "0123456789abcdef0123456789abcdef01234567",
  "shouldPublishPreview": true
}
'@ | Set-Content -LiteralPath $metadataPath

        $result = & (Join-Path $scripts "get-release-build-metadata.ps1") `
            -Build "https://microsoft.visualstudio.com/Dart/_build/results?buildId=154000000" `
            -BuildJsonPath $buildPath `
            -MetadataJsonPath $metadataPath

        $result.buildId | Should Be 154000000
        $result.version | Should Be "0.101.2181.0"
        $result.intent | Should Be "preview-release"
    }

    It "rejects a non-preview candidate" {
        $buildPath = Join-Path $TestDrive "stable-build.json"
        $metadataPath = Join-Path $TestDrive "stable-metadata.json"
        @'
{
  "id": 154000001,
  "definition": { "id": 76541 },
  "buildNumber": "stable",
  "result": "succeeded",
  "sourceBranch": "refs/heads/stable",
  "sourceVersion": "1123456789abcdef0123456789abcdef01234567",
  "reason": "manual",
  "queueTime": "2026-08-06T06:00:00Z",
  "startTime": "2026-08-06T06:00:20Z",
  "finishTime": "2026-08-06T09:00:00Z",
  "templateParameters": {}
}
'@ | Set-Content -LiteralPath $buildPath
        @'
{
  "version": "0.101.2181.0",
  "channel": "stable",
  "intent": "stable-release",
  "shouldPublishPreview": false
}
'@ | Set-Content -LiteralPath $metadataPath

        Assert-Throws {
            & (Join-Path $scripts "get-release-build-metadata.ps1") `
                -Build 154000001 `
                -BuildJsonPath $buildPath `
                -MetadataJsonPath $metadataPath
        }
    }
}

Describe "previous published release selection" {
    It "selects the latest stable or preview release before queue time" {
        $releasesPath = Join-Path $TestDrive "releases.json"
        @'
[
  {
    "tag_name": "v0.101.2171.0",
    "name": "Preview",
    "draft": false,
    "prerelease": true,
    "published_at": "2026-08-05T10:00:00Z",
    "html_url": "https://example.test/preview",
    "assets": []
  },
  {
    "tag_name": "v0.100.1",
    "name": "Stable",
    "draft": false,
    "prerelease": false,
    "published_at": "2026-08-01T10:00:00Z",
    "html_url": "https://example.test/stable",
    "assets": []
  },
  {
    "tag_name": "v0.101.2191.0",
    "name": "Too new",
    "draft": false,
    "prerelease": true,
    "published_at": "2026-08-07T10:00:00Z",
    "html_url": "https://example.test/new",
    "assets": []
  }
]
'@ | Set-Content -LiteralPath $releasesPath

        $result = & (Join-Path $scripts "get-previous-published-release.ps1") `
            -TargetTag "v0.101.2181.0" `
            -QueuedAt "2026-08-06T06:00:00Z" `
            -ReleasesJsonPath $releasesPath `
            -SkipSourceCommitResolution

        $result.tag | Should Be "v0.101.2171.0"
        $result.prerelease | Should Be $true
    }
}

Describe "preview release delta" {
    It "collects added PRs on the same lineage" {
        $repo = Join-Path $TestDrive "same-lineage"
        New-Item -ItemType Directory -Path $repo | Out-Null
        Invoke-TestGit -Repository $repo init -q | Out-Null
        Invoke-TestGit -Repository $repo config user.email "test@example.com" | Out-Null
        Invoke-TestGit -Repository $repo config user.name "Test User" | Out-Null
        $base = Add-TestCommit -Repository $repo -FileName "base.txt" -Content "base" -Message "Base"
        $target = Add-TestCommit -Repository $repo -FileName "feature.txt" -Content "feature" -Message "Add feature (#101)"
        $output = Join-Path $TestDrive "same-output"

        $result = & (Join-Path $scripts "get-preview-release-delta.ps1") `
            -PreviousCommit $base `
            -TargetCommit $target `
            -RepoPath $repo `
            -OutputDirectory $output `
            -NoGitHubLookup

        $result.deltaMode | Should Be "same-lineage"
        $result.addedPrNumbers.Count | Should Be 1
        $result.addedPrNumbers[0] | Should Be 101
        $result.removedPrNumbers.Count | Should Be 0
    }

    It "reports semantic additions and removals across branches" {
        $repo = Join-Path $TestDrive "branch-transition"
        New-Item -ItemType Directory -Path $repo | Out-Null
        Invoke-TestGit -Repository $repo init -q | Out-Null
        Invoke-TestGit -Repository $repo config user.email "test@example.com" | Out-Null
        Invoke-TestGit -Repository $repo config user.name "Test User" | Out-Null
        $root = Add-TestCommit -Repository $repo -FileName "root.txt" -Content "root" -Message "Root"
        Invoke-TestGit -Repository $repo branch main $root | Out-Null
        Invoke-TestGit -Repository $repo checkout -q main | Out-Null
        $commonPr = Add-TestCommit -Repository $repo -FileName "common.txt" -Content "common" -Message "Common feature (#101)"
        $previous = Add-TestCommit -Repository $repo -FileName "removed.txt" -Content "removed" -Message "Main-only feature (#103)"

        Invoke-TestGit -Repository $repo checkout -q -b stable $root | Out-Null
        Add-TestCommit -Repository $repo -FileName "stable.txt" -Content "stable" -Message "Stable fix (#102)" | Out-Null
        Invoke-TestGit -Repository $repo cherry-pick --no-commit $commonPr | Out-Null
        Invoke-TestGit -Repository $repo commit -q -m "Promoted common change" | Out-Null
        $target = Add-TestCommit -Repository $repo -FileName "added.txt" -Content "added" -Message "Stable addition (#104)"
        $output = Join-Path $TestDrive "transition-output"

        $result = & (Join-Path $scripts "get-preview-release-delta.ps1") `
            -PreviousCommit $previous `
            -TargetCommit $target `
            -RepoPath $repo `
            -OutputDirectory $output `
            -NoGitHubLookup

        $result.deltaMode | Should Be "branch-transition"
        ($result.addedPrNumbers -join ",") | Should Be "102,104"
        ($result.removedPrNumbers -join ",") | Should Be "103"
    }
}

Describe "draft preview release dry run" {
    It "constructs a draft-only operation without contacting GitHub" {
        $bodyPath = Join-Path $TestDrive "release-notes.md"
        $assetsPath = Join-Path $TestDrive "assets"
        New-Item -ItemType Directory -Path $assetsPath | Out-Null
        "Preview notes" | Set-Content -LiteralPath $bodyPath
        "installer" | Set-Content -LiteralPath (Join-Path $assetsPath "PowerToysSetup-0.101.2181.0-x64.exe")

        $result = & (Join-Path $scripts "upsert-draft-preview-release.ps1") `
            -Tag "v0.101.2181.0" `
            -Title "PowerToys Preview v0.101.2181.0" `
            -TargetCommit "0123456789abcdef0123456789abcdef01234567" `
            -BodyPath $bodyPath `
            -AssetsDirectory $assetsPath `
            -DryRun

        $result.draft | Should Be $true
        $result.prerelease | Should Be $true
        $result.assetNames.Count | Should Be 1
    }

    It "preserves human text outside managed body markers" {
        $bodyPath = Join-Path $TestDrive "generated-notes.md"
        $assetsPath = Join-Path $TestDrive "managed-assets"
        $existingPath = Join-Path $TestDrive "existing-release.json"
        $mergedPath = Join-Path $TestDrive "merged-notes.md"
        New-Item -ItemType Directory -Path $assetsPath | Out-Null
        @'
<!-- BEGIN POWERTOYS PREVIEW AGENT -->
New generated notes
<!-- END POWERTOYS PREVIEW AGENT -->
'@ | Set-Content -LiteralPath $bodyPath
        "installer" | Set-Content -LiteralPath (Join-Path $assetsPath "PowerToysSetup-0.101.2181.0-x64.exe")
        @'
{
  "isDraft": true,
  "isPrerelease": true,
  "body": "Human introduction\n\n<!-- BEGIN POWERTOYS PREVIEW AGENT -->\nOld generated notes\n<!-- END POWERTOYS PREVIEW AGENT -->\n\nHuman conclusion"
}
'@ | Set-Content -LiteralPath $existingPath

        & (Join-Path $scripts "upsert-draft-preview-release.ps1") `
            -Tag "v0.101.2181.0" `
            -Title "PowerToys Preview v0.101.2181.0" `
            -TargetCommit "0123456789abcdef0123456789abcdef01234567" `
            -BodyPath $bodyPath `
            -AssetsDirectory $assetsPath `
            -ExistingReleaseJsonPath $existingPath `
            -MergedBodyOutputPath $mergedPath `
            -DryRun | Out-Null

        $merged = Get-Content -LiteralPath $mergedPath -Raw
        $merged.Contains("Human introduction") | Should Be $true
        $merged.Contains("New generated notes") | Should Be $true
        $merged.Contains("Old generated notes") | Should Be $false
        $merged.Contains("Human conclusion") | Should Be $true
    }

    It "refuses to update a published release" {
        $bodyPath = Join-Path $TestDrive "published-notes.md"
        $assetsPath = Join-Path $TestDrive "published-assets"
        $existingPath = Join-Path $TestDrive "published-release.json"
        New-Item -ItemType Directory -Path $assetsPath | Out-Null
        "Preview notes" | Set-Content -LiteralPath $bodyPath
        "installer" | Set-Content -LiteralPath (Join-Path $assetsPath "PowerToysSetup-0.101.2181.0-x64.exe")
        '{"isDraft":false,"isPrerelease":true,"body":""}' | Set-Content -LiteralPath $existingPath

        Assert-Throws {
            & (Join-Path $scripts "upsert-draft-preview-release.ps1") `
                -Tag "v0.101.2181.0" `
                -Title "PowerToys Preview v0.101.2181.0" `
                -TargetCommit "0123456789abcdef0123456789abcdef01234567" `
                -BodyPath $bodyPath `
                -AssetsDirectory $assetsPath `
                -ExistingReleaseJsonPath $existingPath `
                -DryRun
        }
    }
}

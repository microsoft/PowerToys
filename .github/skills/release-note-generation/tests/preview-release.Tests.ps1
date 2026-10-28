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

    Invoke-TestGit -Repository $Repository config core.safecrlf false | Out-Null
    Set-Content -LiteralPath (Join-Path $Repository $FileName) -Value $Content
    Invoke-TestGit -Repository $Repository add $FileName | Out-Null
    Invoke-TestGit -Repository $Repository commit -q -m $Message | Out-Null
    return ([string](Invoke-TestGit -Repository $Repository rev-parse HEAD)).Trim()
}

function Write-TestAssetsManifest {
    param(
        [Parameter(Mandatory)][string]$AssetsPath,
        [Parameter(Mandatory)][string[]]$AssetNames
    )

    $assets = @(
        foreach ($name in $AssetNames) {
            $path = Join-Path $AssetsPath $name
            $file = Get-Item -LiteralPath $path
            [ordered]@{
                name = $file.Name
                size = [long]$file.Length
                sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
            }
        }
    )
    [ordered]@{
        schemaVersion = 1
        assets = $assets
    } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $AssetsPath "assets-manifest.json")
}

Describe "web response content decoding" {
    BeforeAll {
        . (Join-Path $scripts "web-response-content.ps1")
    }

    It "decodes UTF-8 byte arrays returned by Invoke-WebRequest" {
        $expected = "F74FF2A89EA37D582F7E18E34EA6E40554C842FA0405725F68969805E6DA0DA9"
        $content = [System.Text.Encoding]::UTF8.GetBytes("$expected`r`n")

        (ConvertFrom-WebResponseContent -Content $content).Trim() | Should Be $expected
    }

    It "preserves string response content" {
        ConvertFrom-WebResponseContent -Content "response text" | Should Be "response text"
    }
}

Describe "GitHub tag target validation" {
    BeforeAll {
        . (Join-Path $scripts "github-tag-target.ps1")
    }

    It "accepts an unused tag" {
        Assert-GitHubTagTarget `
            -Tag "v0.101.2181.0" `
            -ResolvedCommit $null `
            -TargetCommit "0123456789abcdef0123456789abcdef01234567"
    }

    It "accepts a tag that resolves to the target commit" {
        Assert-GitHubTagTarget `
            -Tag "v0.101.2181.0" `
            -ResolvedCommit "0123456789abcdef0123456789abcdef01234567" `
            -TargetCommit "0123456789abcdef0123456789abcdef01234567"
    }

    It "rejects a tag that resolves to another commit" {
        Assert-Throws {
            Assert-GitHubTagTarget `
                -Tag "v0.101.2181.0" `
                -ResolvedCommit "1123456789abcdef0123456789abcdef01234567" `
                -TargetCommit "0123456789abcdef0123456789abcdef01234567"
        }
    }
}

Describe "preview release asset build marker" {
    BeforeAll {
        . (Join-Path $scripts "preview-release-assets.ps1")
    }

    It "matches the requested build and version" {
        $markerPath = Join-Path $TestDrive "matching-build.json"
        '{"buildId":154000000,"version":"0.101.2181.0"}' | Set-Content -LiteralPath $markerPath

        Test-PreviewReleaseAssetBuildMarker `
            -MarkerPath $markerPath `
            -BuildId 154000000 `
            -Version "0.101.2181.0" |
            Should Be $true
    }

    It "rejects a marker for a different build" {
        $markerPath = Join-Path $TestDrive "different-build.json"
        '{"buildId":154000001,"version":"0.101.2181.0"}' | Set-Content -LiteralPath $markerPath

        Test-PreviewReleaseAssetBuildMarker `
            -MarkerPath $markerPath `
            -BuildId 154000000 `
            -Version "0.101.2181.0" |
            Should Be $false
    }

    It "rejects a missing marker" {
        Test-PreviewReleaseAssetBuildMarker `
            -MarkerPath (Join-Path $TestDrive "missing-build.json") `
            -BuildId 154000000 `
            -Version "0.101.2181.0" |
            Should Be $false
    }
}

Describe "preview release ZIP validation" {
    BeforeAll {
        . (Join-Path $scripts "preview-release-assets.ps1")
    }

    It "reads valid entry payloads" {
        $zipPath = Join-Path $TestDrive "valid.zip"
        $filePath = Join-Path $TestDrive "payload.txt"
        "PowerToys preview release payload" | Set-Content -LiteralPath $filePath
        Compress-Archive -LiteralPath $filePath -DestinationPath $zipPath

        $entries = @(Assert-PreviewReleaseZipReadable -Path $zipPath)

        $entries.Count | Should Be 1
        $entries[0] | Should Be "payload.txt"
    }

    It "rejects a corrupt compressed payload with an intact directory" {
        $zipPath = Join-Path $TestDrive "corrupt.zip"
        $filePath = Join-Path $TestDrive "corrupt-payload.txt"
        ("PowerToys preview release payload " * 100) | Set-Content -LiteralPath $filePath
        Compress-Archive -LiteralPath $filePath -DestinationPath $zipPath

        $bytes = [System.IO.File]::ReadAllBytes($zipPath)
        $fileNameLength = [BitConverter]::ToUInt16($bytes, 26)
        $extraLength = [BitConverter]::ToUInt16($bytes, 28)
        $payloadOffset = 30 + $fileNameLength + $extraLength
        $bytes[$payloadOffset] = 0xFF
        [System.IO.File]::WriteAllBytes($zipPath, $bytes)

        Assert-Throws {
            Assert-PreviewReleaseZipReadable -Path $zipPath
        }
    }
}

Describe "preview release build metadata" {
    It "supports a main-branch candidate regardless of release intent" {
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
        $result.tag | Should Be "v0.101.2181"
        $result.intent | Should Be "preview-release"

        @'
{
  "schemaVersion": 1,
  "definitionId": 76541,
  "buildId": 154000000,
  "version": "0.101.2181.0",
  "channel": "preview",
  "intent": "preview-validation",
  "sourceBranch": "refs/heads/main",
  "sourceCommit": "0123456789abcdef0123456789abcdef01234567",
  "shouldPublishPreview": false
}
'@ | Set-Content -LiteralPath $metadataPath

        $result = & (Join-Path $scripts "get-release-build-metadata.ps1") `
            -Build "https://microsoft.visualstudio.com/Dart/_build/results?buildId=154000000" `
            -BuildJsonPath $buildPath `
            -MetadataJsonPath $metadataPath

        $result.intent | Should Be "preview-validation"
        $result.shouldPublishPreview | Should Be $false
    }

    It "supports a stable-branch candidate regardless of release intent" {
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

        $result = & (Join-Path $scripts "get-release-build-metadata.ps1") `
            -Build 154000001 `
            -BuildJsonPath $buildPath `
            -MetadataJsonPath $metadataPath

        $result.intent | Should Be "stable-release"
        $result.channel | Should Be "stable"
        $result.shouldPublishPreview | Should Be $false
        $result.version | Should Be "0.101.2181.0"
        $result.tag | Should Be "v0.101.2181"

        @'
{
  "version": "0.101.2181.0",
  "channel": "preview",
  "intent": "stable-release",
  "shouldPublishPreview": false
}
'@ | Set-Content -LiteralPath $metadataPath

        $result = & (Join-Path $scripts "get-release-build-metadata.ps1") `
            -Build 154000001 `
            -BuildJsonPath $buildPath `
            -MetadataJsonPath $metadataPath

        $result.intent | Should Be "stable-release"
        $result.channel | Should Be "preview"
    }

    It "rejects a nonzero fourth component instead of discarding version identity" {
        $buildPath = Join-Path $TestDrive "nonzero-build.json"
        $metadataPath = Join-Path $TestDrive "nonzero-metadata.json"
        @'
{
  "id": 154000003,
  "definition": { "id": 76541 },
  "result": "succeeded",
  "sourceBranch": "refs/heads/main",
  "sourceVersion": "0123456789abcdef0123456789abcdef01234567",
  "queueTime": "2026-08-06T06:00:00Z",
  "templateParameters": {}
}
'@ | Set-Content -LiteralPath $buildPath
        '{"version":"0.101.2181.1"}' | Set-Content -LiteralPath $metadataPath

        Assert-Throws {
            & (Join-Path $scripts "get-release-build-metadata.ps1") `
                -Build 154000003 `
                -BuildJsonPath $buildPath `
                -MetadataJsonPath $metadataPath
        }
    }

    It "rejects a release build from an unsupported branch" {
        $buildPath = Join-Path $TestDrive "private-build.json"
        $metadataPath = Join-Path $TestDrive "private-metadata.json"
        @'
{
  "id": 154000002,
  "definition": { "id": 76541 },
  "buildNumber": "private",
  "result": "succeeded",
  "sourceBranch": "refs/heads/user/feature",
  "sourceVersion": "2123456789abcdef0123456789abcdef01234567",
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
  "channel": "private",
  "intent": "private-validation",
  "shouldPublishPreview": false
}
'@ | Set-Content -LiteralPath $metadataPath

        Assert-Throws {
            & (Join-Path $scripts "get-release-build-metadata.ps1") `
                -Build 154000002 `
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
            -TargetTag "v0.101.2181" `
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

    It "does not remove a PR already present before the merge base on the target branch" {
        $repo = Join-Path $TestDrive "full-history-identity"
        New-Item -ItemType Directory -Path $repo | Out-Null
        Invoke-TestGit -Repository $repo init -q | Out-Null
        Invoke-TestGit -Repository $repo config user.email "test@example.com" | Out-Null
        Invoke-TestGit -Repository $repo config user.name "Test User" | Out-Null
        $root = Add-TestCommit -Repository $repo -FileName "root.txt" -Content "root" -Message "Root"
        Invoke-TestGit -Repository $repo checkout -q -b main | Out-Null
        Add-TestCommit -Repository $repo -FileName "feature.txt" -Content "main" -Message "Shared feature (#201)" | Out-Null
        $mainTip = Add-TestCommit -Repository $repo -FileName "anchor.txt" -Content "anchor" -Message "Main anchor (#202)"

        Invoke-TestGit -Repository $repo checkout -q -b stable $mainTip | Out-Null
        $previous = Add-TestCommit -Repository $repo -FileName "stable.txt" -Content "stable adaptation" -Message "Shared feature (#201)"
        Invoke-TestGit -Repository $repo checkout -q main | Out-Null
        $target = Add-TestCommit -Repository $repo -FileName "added.txt" -Content "added" -Message "Main addition (#203)"
        $output = Join-Path $TestDrive "full-history-output"

        $result = & (Join-Path $scripts "get-preview-release-delta.ps1") `
            -PreviousCommit $previous `
            -TargetCommit $target `
            -RepoPath $repo `
            -OutputDirectory $output `
            -NoGitHubLookup

        ($result.addedPrNumbers -join ",") | Should Be "203"
        $result.removedPrNumbers.Count | Should Be 0
    }

    It "matches unattributed patches before the merge base in either direction" -TestCases @(
        @{ Reverse = $false; AddedCount = 1; RemovedCount = 0 }
        @{ Reverse = $true; AddedCount = 0; RemovedCount = 1 }
    ) {
        param($Reverse, $AddedCount, $RemovedCount)

        $repo = Join-Path $TestDrive "full-history-patch-$Reverse"
        New-Item -ItemType Directory -Path $repo | Out-Null
        Invoke-TestGit -Repository $repo init -q | Out-Null
        Invoke-TestGit -Repository $repo config user.email "test@example.com" | Out-Null
        Invoke-TestGit -Repository $repo config user.name "Test User" | Out-Null
        $original = Add-TestCommit -Repository $repo -FileName "shared.txt" -Content "shared" -Message "Original shared change"
        Invoke-TestGit -Repository $repo revert --no-edit $original | Out-Null
        $anchor = Add-TestCommit -Repository $repo -FileName "anchor.txt" -Content "anchor" -Message "Shared anchor"
        Invoke-TestGit -Repository $repo checkout -q -b stable | Out-Null
        Invoke-TestGit -Repository $repo cherry-pick --no-commit $original | Out-Null
        Invoke-TestGit -Repository $repo commit -q -m "Reapplied shared change" | Out-Null
        $previous = ([string](Invoke-TestGit -Repository $repo rev-parse HEAD)).Trim()
        Invoke-TestGit -Repository $repo checkout -q -b main $anchor | Out-Null
        $unique = Add-TestCommit -Repository $repo -FileName "unique.txt" -Content "unique" -Message "Unique main change"
        $target = $unique
        if ($Reverse) {
            $target = $previous
            $previous = $unique
        }
        $output = Join-Path $TestDrive "full-history-patch-output-$Reverse"

        $result = & (Join-Path $scripts "get-preview-release-delta.ps1") `
            -PreviousCommit $previous `
            -TargetCommit $target `
            -RepoPath $repo `
            -OutputDirectory $output `
            -NoGitHubLookup

        $result.deltaMode | Should Be "branch-transition"
        $result.addedPrNumbers.Count | Should Be 0
        $result.removedPrNumbers.Count | Should Be 0
        $result.unattributedCommitCount | Should Be $AddedCount
        $commits = Get-Content -LiteralPath (Join-Path $output "delta-commits.json") -Raw | ConvertFrom-Json
        $commits.removedUnattributedCommits.Count | Should Be $RemovedCount
        $added = Get-Content -LiteralPath (Join-Path $output "unattributed-commits.json") -Raw | ConvertFrom-Json
        $unmatched = @($added) + @($commits.removedUnattributedCommits) | Where-Object { $_ }
        @($unmatched).Count | Should Be 1
        $unmatched.sha | Should Be $unique
    }

    It "retains a PR merge when only its first parent is in the opposite history" {
        $repo = Join-Path $TestDrive "feature-merge"
        New-Item -ItemType Directory -Path $repo | Out-Null
        Invoke-TestGit -Repository $repo init -q | Out-Null
        Invoke-TestGit -Repository $repo config user.email "test@example.com" | Out-Null
        Invoke-TestGit -Repository $repo config user.name "Test User" | Out-Null
        $root = Add-TestCommit -Repository $repo -FileName "root.txt" -Content "root" -Message "Root"
        Invoke-TestGit -Repository $repo checkout -q -b feature | Out-Null
        Add-TestCommit -Repository $repo -FileName "feature.txt" -Content "feature" -Message "Feature implementation" | Out-Null
        Invoke-TestGit -Repository $repo checkout -q -b stable $root | Out-Null
        Invoke-TestGit -Repository $repo merge -q --no-ff feature -m "Merge pull request #401 from feature" | Out-Null
        $previous = ([string](Invoke-TestGit -Repository $repo rev-parse HEAD)).Trim()
        Invoke-TestGit -Repository $repo checkout -q -b main $root | Out-Null
        $target = Add-TestCommit -Repository $repo -FileName "main.txt" -Content "main" -Message "Main addition (#402)"
        $output = Join-Path $TestDrive "feature-merge-output"

        $result = & (Join-Path $scripts "get-preview-release-delta.ps1") `
            -PreviousCommit $previous `
            -TargetCommit $target `
            -RepoPath $repo `
            -OutputDirectory $output `
            -NoGitHubLookup

        ($result.addedPrNumbers -join ",") | Should Be "402"
        ($result.removedPrNumbers -join ",") | Should Be "401"
    }

    It "retains an octopus merge when a merged-in parent is absent from the opposite history" {
        $repo = Join-Path $TestDrive "octopus-merge"
        New-Item -ItemType Directory -Path $repo | Out-Null
        Invoke-TestGit -Repository $repo init -q | Out-Null
        Invoke-TestGit -Repository $repo config user.email "test@example.com" | Out-Null
        Invoke-TestGit -Repository $repo config user.name "Test User" | Out-Null
        $root = Add-TestCommit -Repository $repo -FileName "root.txt" -Content "root" -Message "Root"
        Invoke-TestGit -Repository $repo checkout -q -b main | Out-Null
        Add-TestCommit -Repository $repo -FileName "shared.txt" -Content "shared" -Message "Shared feature (#501)" | Out-Null
        Invoke-TestGit -Repository $repo checkout -q -b feature $root | Out-Null
        Add-TestCommit -Repository $repo -FileName "feature.txt" -Content "feature" -Message "Feature implementation" | Out-Null
        Invoke-TestGit -Repository $repo checkout -q -b stable $root | Out-Null
        Invoke-TestGit -Repository $repo merge -q --no-ff main feature -m "Merge features (#502)" | Out-Null
        $previous = ([string](Invoke-TestGit -Repository $repo rev-parse HEAD)).Trim()
        Invoke-TestGit -Repository $repo checkout -q main | Out-Null
        $target = Add-TestCommit -Repository $repo -FileName "later.txt" -Content "later" -Message "Later main feature (#503)"
        $output = Join-Path $TestDrive "octopus-merge-output"

        $result = & (Join-Path $scripts "get-preview-release-delta.ps1") `
            -PreviousCommit $previous `
            -TargetCommit $target `
            -RepoPath $repo `
            -OutputDirectory $output `
            -NoGitHubLookup

        ($result.addedPrNumbers -join ",") | Should Be "503"
        ($result.removedPrNumbers -join ",") | Should Be "502"
    }

    It "does not report a merge that only promotes the other branch" {
        $repo = Join-Path $TestDrive "promotion-merge"
        New-Item -ItemType Directory -Path $repo | Out-Null
        Invoke-TestGit -Repository $repo init -q | Out-Null
        Invoke-TestGit -Repository $repo config user.email "test@example.com" | Out-Null
        Invoke-TestGit -Repository $repo config user.name "Test User" | Out-Null
        Add-TestCommit -Repository $repo -FileName "root.txt" -Content "root" -Message "Root" | Out-Null
        Invoke-TestGit -Repository $repo checkout -q -b main | Out-Null
        Add-TestCommit -Repository $repo -FileName "feature.txt" -Content "feature" -Message "Main feature (#301)" | Out-Null

        Invoke-TestGit -Repository $repo checkout -q -b stable HEAD~1 | Out-Null
        Add-TestCommit -Repository $repo -FileName "stable.txt" -Content "stable" -Message "Stable fix (#302)" | Out-Null
        Invoke-TestGit -Repository $repo merge -q --no-ff main -m "Merge main into stable (#303)" | Out-Null
        $previous = ([string](Invoke-TestGit -Repository $repo rev-parse HEAD)).Trim()
        Invoke-TestGit -Repository $repo checkout -q main | Out-Null
        $target = Add-TestCommit -Repository $repo -FileName "later.txt" -Content "later" -Message "Later main feature (#304)"
        $output = Join-Path $TestDrive "promotion-output"

        $result = & (Join-Path $scripts "get-preview-release-delta.ps1") `
            -PreviousCommit $previous `
            -TargetCommit $target `
            -RepoPath $repo `
            -OutputDirectory $output `
            -NoGitHubLookup

        ($result.addedPrNumbers -join ",") | Should Be "304"
        ($result.removedPrNumbers -join ",") | Should Be "302"
    }
}

Describe "preview release asset links" {
    BeforeAll {
        function az {
            $global:LASTEXITCODE = 0
            switch -Regex ($args -join " ") {
                "^extension list " { return "azure-devops" }
                "^devops configure " { return }
                "^pipelines build show " { return '{"templateParameters":{}}' }
                "^pipelines runs artifact list " {
                    return @'
[
  {"name":"build-x64-Release","resource":{"downloadUrl":"https://example.test/x64"}},
  {"name":"build-arm64-Release","resource":{"downloadUrl":"https://example.test/arm64"}}
]
'@
                }
                "^account get-access-token " { return "test-token" }
                default { throw "Unexpected Azure CLI command: $args" }
            }
        }
    }

    It "uses the preview tag in links without changing artifact versions or default links" {
        $assetsPath = Join-Path $TestDrive "link-assets"
        $gpoRoot = Join-Path $TestDrive "gpo"
        New-Item -ItemType Directory -Path $assetsPath, (Join-Path $gpoRoot "en-US") -Force | Out-Null
        "admx" | Set-Content -LiteralPath (Join-Path $gpoRoot "PowerToys.admx")
        "adml" | Set-Content -LiteralPath (Join-Path $gpoRoot "en-US\PowerToys.adml")
        Compress-Archive -Path (Join-Path $gpoRoot "*") -DestinationPath (Join-Path $assetsPath "GroupPolicyObjectFiles-0.101.2181.0.zip")
        $symbolsPath = Join-Path $TestDrive "symbols.pdb"
        "symbols" | Set-Content -LiteralPath $symbolsPath
        foreach ($arch in @("x64", "arm64")) {
            Compress-Archive -LiteralPath $symbolsPath -DestinationPath (Join-Path $assetsPath "symbols-$arch.zip")
            foreach ($prefix in @("PowerToysSetup", "PowerToysUserSetup")) {
                "installer" | Set-Content -LiteralPath (Join-Path $assetsPath "$prefix-0.101.2181.0-$arch.exe")
            }
        }
        '{"buildId":154000000,"version":"0.101.2181.0"}' |
            Set-Content -LiteralPath (Join-Path $assetsPath ".buildinfo.json")
        Mock Invoke-WebRequest {
            [pscustomobject]@{
                Content = (Get-FileHash -LiteralPath (Join-Path $TestDrive "link-assets\PowerToysSetup-0.101.2181.0-x64.exe")).Hash
            }
        }
        Mock Get-AuthenticodeSignature {
            [pscustomobject]@{
                Status = [System.Management.Automation.SignatureStatus]::Valid
                SignerCertificate = [pscustomobject]@{ Subject = "CN=Microsoft Corporation" }
            }
        }

        $previousExtensionDirectory = $env:AZURE_EXTENSION_DIR
        $previousNoPrompt = $env:AZURE_CORE_NO_PROMPT
        try {
            $env:AZURE_EXTENSION_DIR = $TestDrive
            $result = & (Join-Path $scripts "prepare-release-assets.ps1") `
                -BuildId 154000000 `
                -Version "0.101.2181.0" `
                -ReleaseTag "v0.101.2181" `
                -DestinationFolder $assetsPath

            $result.version | Should Be "0.101.2181.0"
            $result.assetCount | Should Be 7
            $hashes = Get-Content -LiteralPath $result.hashesPath -Raw
            foreach ($arch in @("x64", "arm64")) {
                foreach ($prefix in @("PowerToysSetup", "PowerToysUserSetup")) {
                    $hashes.Contains("releases/download/v0.101.2181/$prefix-0.101.2181.0-$arch.exe") | Should Be $true
                }
            }
            $manifest = Get-Content -LiteralPath $result.assetsManifestPath -Raw | ConvertFrom-Json
            $manifest.version | Should Be "0.101.2181.0"
            ($manifest.assets.name -contains "GroupPolicyObjectFiles-0.101.2181.0.zip") | Should Be $true

            & (Join-Path $scripts "prepare-release-assets.ps1") `
                -BuildId 154000000 `
                -Version "0.101.2181.0" `
                -DestinationFolder $assetsPath | Out-Null
            $hashes = Get-Content -LiteralPath $result.hashesPath -Raw
            $hashes.Contains("releases/download/v0.101.2181.0/PowerToysSetup-0.101.2181.0-x64.exe") | Should Be $true

            Assert-Throws {
                & (Join-Path $scripts "prepare-release-assets.ps1") `
                    -BuildId 154000000 `
                    -Version "0.101.2181.0" `
                    -ReleaseTag "v0.101.2182" `
                    -DestinationFolder $assetsPath
            }
        }
        finally {
            $env:AZURE_EXTENSION_DIR = $previousExtensionDirectory
            $env:AZURE_CORE_NO_PROMPT = $previousNoPrompt
        }
    }
}

Describe "release title selection" {
    BeforeAll {
        . (Join-Path $scripts "release-title.ps1")
    }

    It "requires a known source branch for a preview title" {
        Assert-Throws { Get-PowerToysReleaseTitle -Tag "v0.101.2181" -Prerelease }
        Assert-Throws { Get-PowerToysReleaseTitle -Tag "v0.101.2181" -SourceBranch "refs/heads/feature" -Prerelease }
    }

    It "generates the default draft title <ExpectedTitle>" -TestCases @(
        @{ Prerelease = $false; SourceBranch = $null; ExpectedTitle = "Release v0.101.2181" },
        @{ Prerelease = $true; SourceBranch = "refs/heads/stable"; ExpectedTitle = "Release preview v0.101.2181" },
        @{ Prerelease = $true; SourceBranch = "refs/heads/main"; ExpectedTitle = "Development preview v0.101.2181" }
    ) {
        param($Prerelease, $SourceBranch, $ExpectedTitle)

        $releasePath = Join-Path $TestDrive "titled-release"
        New-Item -ItemType Directory -Path $releasePath -Force | Out-Null
        $links = @()
        foreach ($entry in @(
            @{ Prefix = "PowerToysUserSetup"; Arch = "x64"; Ref = "ptUserX64" },
            @{ Prefix = "PowerToysUserSetup"; Arch = "arm64"; Ref = "ptUserArm64" },
            @{ Prefix = "PowerToysSetup"; Arch = "x64"; Ref = "ptMachineX64" },
            @{ Prefix = "PowerToysSetup"; Arch = "arm64"; Ref = "ptMachineArm64" }
        )) {
            $name = "$($entry.Prefix)-0.101.2181.0-$($entry.Arch).exe"
            "installer" | Set-Content -LiteralPath (Join-Path $releasePath $name)
            $links += "[$($entry.Ref)]: https://github.com/microsoft/PowerToys/releases/download/v0.101.2181/$name"
        }
        foreach ($name in @("symbols-x64.zip", "symbols-arm64.zip", "GroupPolicyObjectFiles-0.101.2181.0.zip")) {
            "archive" | Set-Content -LiteralPath (Join-Path $releasePath $name)
        }
        $links | Set-Content -LiteralPath (Join-Path $releasePath "release-notes.md")
        $parameters = @{
            ReleaseFolder = $releasePath
            TagName = "v0.101.2181"
            Target = "0123456789abcdef0123456789abcdef01234567"
            Prerelease = $Prerelease
            DryRun = $true
        }
        if ($SourceBranch) {
            $parameters.SourceBranch = $SourceBranch
        }

        $output = & (Join-Path $scripts "create-github-draft-release.ps1") @parameters 6>&1 | Out-String

        $output.Contains("Title:          $ExpectedTitle") | Should Be $true
        $output.Contains("--title $ExpectedTitle --notes-file") | Should Be $true
        $output.Contains("--draft") | Should Be $true
        $output.Contains("--prerelease") | Should Be $Prerelease
    }
}

Describe "preview release body layout" {
    BeforeAll {
        . (Join-Path $scripts "preview-release-body.ps1")
    }

    It "accepts an introduction followed by section headings" {
        Assert-PreviewReleaseBodyLayout -Body "PowerToys preview introduction.`r`n`r`n## Installer Hashes`r`n`r`n## Highlights"
    }

    It "rejects a repeated release title" {
        { Assert-PreviewReleaseBodyLayout -Body "# Dev preview v0.103.3006.0`n`nIntroduction" } |
            Should Throw "must not contain a level-one title heading"
    }

    It "rejects a setext release title" {
        { Assert-PreviewReleaseBodyLayout -Body "Preview v0.103.3006.0`r`n===`r`n`r`nIntroduction" } |
            Should Throw "must not contain a level-one title heading"
    }

    It "validates the managed body without rejecting human headings outside it" {
        Assert-PreviewReleaseBodyLayout -Body "# Human introduction`n<!-- BEGIN POWERTOYS PREVIEW AGENT -->`nPreview notes`n## Highlights`n<!-- END POWERTOYS PREVIEW AGENT -->"
    }
}

Describe "draft preview release dry run" {
    It "rejects a generated release-title heading before creating a draft" {
        $bodyPath = Join-Path $TestDrive "duplicate-title.md"
        $assetsPath = Join-Path $TestDrive "duplicate-title-assets"
        New-Item -ItemType Directory -Path $assetsPath | Out-Null
        "<!-- BEGIN POWERTOYS PREVIEW AGENT -->`n# Dev preview v0.101.2181.0`n`nIntroduction`n<!-- END POWERTOYS PREVIEW AGENT -->" |
            Set-Content -LiteralPath $bodyPath

        {
            & (Join-Path $scripts "upsert-draft-preview-release.ps1") `
                -Tag "v0.101.2181" `
                -SourceBranch "refs/heads/main" `
                -TargetCommit "0123456789abcdef0123456789abcdef01234567" `
                -BodyPath $bodyPath `
                -AssetsDirectory $assetsPath `
                -DryRun
        } | Should Throw "must not contain a level-one title heading"
    }

    It "rejects a generated release-title heading during final verification" {
        $bodyPath = Join-Path $TestDrive "verify-duplicate-title.md"
        "<!-- BEGIN POWERTOYS PREVIEW AGENT -->`n# Preview v0.101.2181.0`n`nIntroduction`n<!-- END POWERTOYS PREVIEW AGENT -->" |
            Set-Content -LiteralPath $bodyPath

        {
            & (Join-Path $scripts "verify-draft-preview-release.ps1") `
                -Tag "v0.101.2181" `
                -SourceBranch "refs/heads/main" `
                -TargetCommit "0123456789abcdef0123456789abcdef01234567" `
                -BodyPath $bodyPath `
                -AssetsDirectory (Join-Path $TestDrive "unused-assets") `
                -OutputPath (Join-Path $TestDrive "invalid-review.md") `
                -DryRun
        } | Should Throw "must not contain a level-one title heading"
    }

    It "constructs a draft-only operation for <SourceBranch> without contacting GitHub" -TestCases @(
        @{ SourceBranch = "refs/heads/main"; ExpectedTitle = "Development preview v0.101.2181" },
        @{ SourceBranch = "refs/heads/stable"; ExpectedTitle = "Release preview v0.101.2181" }
    ) {
        param($SourceBranch, $ExpectedTitle)

        $bodyPath = Join-Path $TestDrive "release-notes.md"
        $assetsPath = Join-Path $TestDrive ("assets-" + (Split-Path $SourceBranch -Leaf))
        New-Item -ItemType Directory -Path $assetsPath | Out-Null
        "Preview notes" | Set-Content -LiteralPath $bodyPath
        "installer" | Set-Content -LiteralPath (Join-Path $assetsPath "PowerToysSetup-0.101.2181.0-x64.exe")
        "local audit only" | Set-Content -LiteralPath (Join-Path $assetsPath "release-manifest.json")
        Write-TestAssetsManifest -AssetsPath $assetsPath -AssetNames @("PowerToysSetup-0.101.2181.0-x64.exe")

        $result = & (Join-Path $scripts "upsert-draft-preview-release.ps1") `
            -Tag "v0.101.2181" `
            -SourceBranch $SourceBranch `
            -TargetCommit "0123456789abcdef0123456789abcdef01234567" `
            -BodyPath $bodyPath `
            -AssetsDirectory $assetsPath `
            -DryRun

        $result.draft | Should Be $true
        $result.prerelease | Should Be $true
        $result.tag | Should Be "v0.101.2181"
        $result.title | Should Be $ExpectedTitle
        $result.assetNames.Count | Should Be 1
        $result.assetNames[0] | Should Be "PowerToysSetup-0.101.2181.0-x64.exe"
        ($result.assetNames -contains "release-manifest.json") | Should Be $false
        ($result.assetNames -contains "assets-manifest.json") | Should Be $false
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
        Write-TestAssetsManifest -AssetsPath $assetsPath -AssetNames @("PowerToysSetup-0.101.2181.0-x64.exe")
        @'
{
  "isDraft": true,
  "isPrerelease": true,
  "body": "Human introduction\n\n<!-- BEGIN POWERTOYS PREVIEW AGENT -->\nOld generated notes\n<!-- END POWERTOYS PREVIEW AGENT -->\n\nHuman conclusion"
}
'@ | Set-Content -LiteralPath $existingPath

        & (Join-Path $scripts "upsert-draft-preview-release.ps1") `
            -Tag "v0.101.2181" `
            -SourceBranch "refs/heads/main" `
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
        Write-TestAssetsManifest -AssetsPath $assetsPath -AssetNames @("PowerToysSetup-0.101.2181.0-x64.exe")
        '{"isDraft":false,"isPrerelease":true,"body":""}' | Set-Content -LiteralPath $existingPath

        Assert-Throws {
            & (Join-Path $scripts "upsert-draft-preview-release.ps1") `
                -Tag "v0.101.2181" `
                -SourceBranch "refs/heads/main" `
                -TargetCommit "0123456789abcdef0123456789abcdef01234567" `
                -BodyPath $bodyPath `
                -AssetsDirectory $assetsPath `
                -ExistingReleaseJsonPath $existingPath `
                -DryRun
        }
    }

    It "rejects undeclared executable and ZIP assets" {
        $bodyPath = Join-Path $TestDrive "extra-notes.md"
        $assetsPath = Join-Path $TestDrive "extra-assets"
        New-Item -ItemType Directory -Path $assetsPath | Out-Null
        "Preview notes" | Set-Content -LiteralPath $bodyPath
        "installer" | Set-Content -LiteralPath (Join-Path $assetsPath "PowerToysSetup-0.101.2181.0-x64.exe")
        "unexpected" | Set-Content -LiteralPath (Join-Path $assetsPath "unexpected.zip")
        Write-TestAssetsManifest -AssetsPath $assetsPath -AssetNames @("PowerToysSetup-0.101.2181.0-x64.exe")

        Assert-Throws {
            & (Join-Path $scripts "upsert-draft-preview-release.ps1") `
                -Tag "v0.101.2181" `
                -SourceBranch "refs/heads/main" `
                -TargetCommit "0123456789abcdef0123456789abcdef01234567" `
                -BodyPath $bodyPath `
                -AssetsDirectory $assetsPath `
                -DryRun
        }
    }

    It "rejects an asset whose contents no longer match the manifest" {
        $bodyPath = Join-Path $TestDrive "tampered-notes.md"
        $assetsPath = Join-Path $TestDrive "tampered-assets"
        New-Item -ItemType Directory -Path $assetsPath | Out-Null
        "Preview notes" | Set-Content -LiteralPath $bodyPath
        $installerPath = Join-Path $assetsPath "PowerToysSetup-0.101.2181.0-x64.exe"
        "installer" | Set-Content -LiteralPath $installerPath -NoNewline
        Write-TestAssetsManifest -AssetsPath $assetsPath -AssetNames @("PowerToysSetup-0.101.2181.0-x64.exe")
        "tampered!" | Set-Content -LiteralPath $installerPath -NoNewline

        Assert-Throws {
            & (Join-Path $scripts "upsert-draft-preview-release.ps1") `
                -Tag "v0.101.2181" `
                -SourceBranch "refs/heads/main" `
                -TargetCommit "0123456789abcdef0123456789abcdef01234567" `
                -BodyPath $bodyPath `
                -AssetsDirectory $assetsPath `
                -DryRun
        }
    }

    It "writes a final review for <SourceBranch> independently of build intent" -TestCases @(
        @{ SourceBranch = "refs/heads/main"; BuildIntent = "preview-release"; ExpectedTitle = "Development preview v0.101.2181" },
        @{ SourceBranch = "refs/heads/stable"; BuildIntent = "stable-release"; ExpectedTitle = "Release preview v0.101.2181" }
    ) {
        param($SourceBranch, $BuildIntent, $ExpectedTitle)

        $bodyPath = Join-Path $TestDrive "dry-run-notes.md"
        $assetsPath = Join-Path $TestDrive ("dry-run-assets-" + (Split-Path $SourceBranch -Leaf))
        $deltaPath = Join-Path $TestDrive ("dry-run-delta-" + (Split-Path $SourceBranch -Leaf))
        $contextPath = Join-Path $TestDrive "release-context.json"
        $previousReleasePath = Join-Path $TestDrive "previous-release.json"
        $reviewPath = Join-Path $TestDrive "final-review.md"
        New-Item -ItemType Directory -Path $assetsPath | Out-Null
        New-Item -ItemType Directory -Path $deltaPath | Out-Null
        @'
<!-- BEGIN POWERTOYS PREVIEW AGENT -->
Preview notes
<!-- END POWERTOYS PREVIEW AGENT -->
'@ | Set-Content -LiteralPath $bodyPath
        "installer" | Set-Content -LiteralPath (Join-Path $assetsPath "PowerToysSetup-0.101.2181.0-x64.exe")
        Write-TestAssetsManifest -AssetsPath $assetsPath -AssetNames @("PowerToysSetup-0.101.2181.0-x64.exe")
        "[]" | Set-Content -LiteralPath (Join-Path $deltaPath "delta-prs.json")
        "[]" | Set-Content -LiteralPath (Join-Path $deltaPath "removed-prs.json")
        '[{"sha":"abcdef0123456789abcdef0123456789abcdef01","subject":"Aggregate promotion commit"}]' |
            Set-Content -LiteralPath (Join-Path $deltaPath "unattributed-commits.json")
        '{"deltaMode":"same-lineage","mergeBase":null}' |
            Set-Content -LiteralPath (Join-Path $deltaPath "delta-commits.json")
        [ordered]@{
            buildId = 154000000
            buildUrl = "https://microsoft.visualstudio.com/Dart/_build/results?buildId=154000000"
            version = "0.101.2181.0"
            sourceBranch = $SourceBranch
            sourceCommit = "0123456789abcdef0123456789abcdef01234567"
            intent = $BuildIntent
            channel = if ($BuildIntent -eq "stable-release") { "stable" } else { "preview" }
        } | ConvertTo-Json | Set-Content -LiteralPath $contextPath
        @'
{
  "tag": "v0.100.0",
  "sourceCommit": "1123456789abcdef0123456789abcdef01234567"
}
'@ | Set-Content -LiteralPath $previousReleasePath

        $result = & (Join-Path $scripts "verify-draft-preview-release.ps1") `
            -Tag "v0.101.2181" `
            -SourceBranch $SourceBranch `
            -TargetCommit "0123456789abcdef0123456789abcdef01234567" `
            -AssetsDirectory $assetsPath `
            -BodyPath $bodyPath `
            -ContextPath $contextPath `
            -PreviousReleasePath $previousReleasePath `
            -DeltaDirectory $deltaPath `
            -OutputPath $reviewPath `
            -DryRun

        $result.status | Should Be "PASS"
        $result.draftUrl | Should Be $null
        $review = Get-Content -LiteralPath $reviewPath -Raw
        $review.Contains("Local dry-run package is complete") | Should Be $true
        (($review -split "\r?\n") -contains "- Title: $ExpectedTitle") | Should Be $true
        $review.Contains("Version: 0.101.2181.0") | Should Be $true
        $review.Contains("abcdef0123456789abcdef0123456789abcdef01") | Should Be $true
        $review.Contains("Aggregate promotion commit") | Should Be $true
        $review.Contains("154000000") | Should Be $true
        $review.Contains("v0.100.0@1123456789ab") | Should Be $true
        $review.Contains("Delta mode: same-lineage") | Should Be $true
    }

    It "rejects a title source branch that conflicts with the build context" {
        $contextPath = Join-Path $TestDrive "conflicting-branch.json"
        '{"sourceBranch":"refs/heads/main"}' | Set-Content -LiteralPath $contextPath

        {
            & (Join-Path $scripts "verify-draft-preview-release.ps1") `
                -Tag "v0.101.2181" `
                -SourceBranch "refs/heads/stable" `
                -TargetCommit "0123456789abcdef0123456789abcdef01234567" `
                -AssetsDirectory $TestDrive `
                -ContextPath $contextPath `
                -OutputPath (Join-Path $TestDrive "conflicting-branch-review.md") `
                -DryRun
        } | Should Throw "does not match build context"
    }

    It "rejects four-component and suffixed tags before creating or verifying a draft" {
        $bodyPath = Join-Path $TestDrive "invalid-tag-notes.md"
        $assetsPath = Join-Path $TestDrive "invalid-tag-assets"
        New-Item -ItemType Directory -Path $assetsPath | Out-Null
        @'
<!-- BEGIN POWERTOYS PREVIEW AGENT -->
Preview notes
<!-- END POWERTOYS PREVIEW AGENT -->
'@ | Set-Content -LiteralPath $bodyPath
        "installer" | Set-Content -LiteralPath (Join-Path $assetsPath "PowerToysSetup-0.101.2181.0-x64.exe")
        Write-TestAssetsManifest -AssetsPath $assetsPath -AssetNames @("PowerToysSetup-0.101.2181.0-x64.exe")

        foreach ($tag in @("v0.101.2181.0", "v0.101.2181-preview")) {
            Assert-Throws {
                & (Join-Path $scripts "upsert-draft-preview-release.ps1") `
                    -Tag $tag `
                    -SourceBranch "refs/heads/main" `
                    -TargetCommit "0123456789abcdef0123456789abcdef01234567" `
                    -BodyPath $bodyPath `
                    -AssetsDirectory $assetsPath `
                    -DryRun
            }
            Assert-Throws {
                & (Join-Path $scripts "verify-draft-preview-release.ps1") `
                    -Tag $tag `
                    -SourceBranch "refs/heads/main" `
                    -TargetCommit "0123456789abcdef0123456789abcdef01234567" `
                    -BodyPath $bodyPath `
                    -AssetsDirectory $assetsPath `
                    -OutputPath (Join-Path $TestDrive "invalid-tag-review.md") `
                    -DryRun
            }
        }
    }
}

Describe "preview PR metadata attribution" {
    It "rejects a missing member list before fetching PRs" {
        Assert-Throws {
            & (Join-Path $scripts "collect-pr-metadata.ps1") `
                -PrNumbers @(123) `
                -OutputDirectory (Join-Path $TestDrive "missing-members") `
                -MemberListPath (Join-Path $TestDrive "MemberList.md")
        }
    }

    It "rejects an empty member list before fetching PRs" {
        $memberListPath = Join-Path $TestDrive "EmptyMemberList.md"
        "" | Set-Content -LiteralPath $memberListPath

        Assert-Throws {
            & (Join-Path $scripts "collect-pr-metadata.ps1") `
                -PrNumbers @(123) `
                -OutputDirectory (Join-Path $TestDrive "empty-members") `
                -MemberListPath $memberListPath
        }
    }
}

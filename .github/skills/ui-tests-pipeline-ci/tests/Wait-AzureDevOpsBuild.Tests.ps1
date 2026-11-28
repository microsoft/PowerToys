# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

Describe 'ConvertTo-AzDevOpsBuildSnapshot' {
    BeforeAll {
        Set-StrictMode -Version Latest
        . (Join-Path $PSScriptRoot '..\scripts\AzureDevOps.ps1')

        $script:branch = 'refs/heads/test-branch'
        $script:sourceVersion = '0123456789abcdef0123456789abcdef01234567'
    }


    It 'projects a not-started build without optional result or time properties' {
        $build = [pscustomobject]@{
            id = 101
            buildNumber = '20260829.1'
            status = 'notStarted'
            sourceBranch = $script:branch
            sourceVersion = $script:sourceVersion
            queueTime = '2026-08-29T18:00:00Z'
            lastChangedDate = '2026-08-29T18:00:00Z'
            _links = [pscustomobject]@{ web = [pscustomobject]@{ href = 'https://example.test/101' } }
        }

        $snapshot = ConvertTo-AzDevOpsBuildSnapshot -Build $build -RequestedId 101 -ExpectedBranch $script:branch -ExpectedSourceVersion $script:sourceVersion

        $snapshot.Status | Should Be 'notStarted'
        $snapshot.Result | Should BeNullOrEmpty
        $snapshot.StartTime | Should BeNullOrEmpty
        $snapshot.FinishTime | Should BeNullOrEmpty
    }

    It 'projects an in-progress build without result or finish time' {
        $build = [pscustomobject]@{
            id = 102
            buildNumber = '20260829.2'
            status = 'inProgress'
            sourceBranch = $script:branch
            sourceVersion = $script:sourceVersion
            queueTime = '2026-08-29T18:00:00Z'
            startTime = '2026-08-29T18:01:00Z'
            lastChangedDate = '2026-08-29T18:02:00Z'
            _links = [pscustomobject]@{ web = [pscustomobject]@{ href = 'https://example.test/102' } }
        }

        $snapshot = ConvertTo-AzDevOpsBuildSnapshot -Build $build -RequestedId 102 -ExpectedBranch $script:branch -ExpectedSourceVersion $script:sourceVersion

        $snapshot.Status | Should Be 'inProgress'
        $snapshot.Result | Should BeNullOrEmpty
        $snapshot.StartTime | Should Be '2026-08-29T18:01:00Z'
        $snapshot.FinishTime | Should BeNullOrEmpty
    }

    It 'projects a completed build with result and times' {
        $build = [pscustomobject]@{
            id = 103
            buildNumber = '20260829.3'
            status = 'completed'
            result = 'succeeded'
            sourceBranch = $script:branch
            sourceVersion = $script:sourceVersion
            queueTime = '2026-08-29T18:00:00Z'
            startTime = '2026-08-29T18:01:00Z'
            finishTime = '2026-08-29T18:03:00Z'
            lastChangedDate = '2026-08-29T18:03:01Z'
            _links = [pscustomobject]@{ web = [pscustomobject]@{ href = 'https://example.test/103' } }
        }

        $snapshot = ConvertTo-AzDevOpsBuildSnapshot -Build $build -RequestedId 103 -ExpectedBranch $script:branch -ExpectedSourceVersion $script:sourceVersion

        $snapshot.Status | Should Be 'completed'
        $snapshot.Result | Should Be 'succeeded'
        $snapshot.StartTime | Should Be '2026-08-29T18:01:00Z'
        $snapshot.FinishTime | Should Be '2026-08-29T18:03:00Z'
    }

    It 'rejects a malformed build response without an id' {
        $build = [pscustomobject]@{
            message = 'transient non-build response'
        }

        $threw = $false
        try {
            ConvertTo-AzDevOpsBuildSnapshot -Build $build -RequestedId 104 -ExpectedBranch $script:branch -ExpectedSourceVersion $script:sourceVersion
        }
        catch {
            $threw = $true
            $_.Exception.Message | Should Match 'malformed build response'
        }

        $threw | Should Be $true
    }
}
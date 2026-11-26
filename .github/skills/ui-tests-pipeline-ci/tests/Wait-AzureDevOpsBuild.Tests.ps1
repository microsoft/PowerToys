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

Describe 'Wait-AzureDevOpsBuild' {
    BeforeAll {
        $script:waiter = Join-Path $TestDrive 'Wait-AzureDevOpsBuild.ps1'
        Copy-Item (Join-Path $PSScriptRoot '..\scripts\Wait-AzureDevOpsBuild.ps1') $script:waiter
        Copy-Item (Join-Path $PSScriptRoot '..\scripts\AzureDevOps.ps1') (Join-Path $TestDrive 'AzureDevOps.Real.ps1')
        ({
            . (Join-Path $PSScriptRoot 'AzureDevOps.Real.ps1')

            function Test-AzDevOpsSession {}

            function Invoke-AzDevOpsRest {
                param([string] $Uri, [string] $Organization, [string] $Project)

                $observedTargets.Add("$Organization/$Project")
                $response = $fakeBuilds.Dequeue()
                if ($response -is [string]) {
                    throw $response
                }

                [pscustomobject]@{ Body = $response }
            }

            function Start-Sleep {
                param([int] $Seconds)

                $observedWaits.Add($Seconds)
            }
        }).ToString() | Set-Content (Join-Path $TestDrive 'AzureDevOps.ps1')

        $script:watchBranch = 'refs/heads/test-watch'
        $script:watchVersion = '0123456789abcdef0123456789abcdef01234567'
    }

    BeforeEach {
        $fakeBuilds = [System.Collections.Generic.Queue[object]]::new()
        $observedWaits = [System.Collections.Generic.List[int]]::new()
        $observedTargets = [System.Collections.Generic.List[string]]::new()
        $build = @{
            id = 101
            buildNumber = '20261119.1'
            sourceBranch = $script:watchBranch
            sourceVersion = $script:watchVersion
            _links = [pscustomobject]@{ web = [pscustomobject]@{ href = 'https://example.test/101' } }
        }
        $pending = $build.Clone()
        $pending.status = 'inProgress'
        $terminal = $build.Clone()
        $terminal.status = 'completed'
        $terminal.result = 'succeeded'
    }

    It 'waits 20 minutes after a progress read and after a transient query failure' {
        $fakeBuilds.Enqueue([pscustomobject]$pending)
        $fakeBuilds.Enqueue('Transient read failure')
        $fakeBuilds.Enqueue([pscustomobject]$terminal)

        $events = @(& $script:waiter -BuildId 101 -ExpectedBranch $script:watchBranch -ExpectedSourceVersion $script:watchVersion | ConvertFrom-Json)

        ($observedWaits -join ',') | Should Be '1200,1200'
        ($observedTargets | Select-Object -Unique) | Should Be 'shine-oss/PowerToys'
        ($events.Event -join ',') | Should Be 'progress,query-error,terminal'
        $waitBudget = ([DateTimeOffset]$events[0].DeadlineUtc) - ([DateTimeOffset]$events[0].ObservedUtc)
        [Math]::Ceiling($waitBudget.TotalMinutes) | Should Be 360
        $events[-1].Builds[0].Result | Should Be 'succeeded'
    }

    It 'shortens the sleep when less than one polling interval remains' {
        $fakeBuilds.Enqueue([pscustomobject]$pending)
        $fakeBuilds.Enqueue([pscustomobject]$terminal)

        $events = @(& $script:waiter -BuildId 101 -ExpectedBranch $script:watchBranch -ExpectedSourceVersion $script:watchVersion -TimeoutMinutes 1 | ConvertFrom-Json)

        $observedWaits.Count | Should Be 1
        ($observedWaits[0] -gt 0 -and $observedWaits[0] -le 60) | Should Be $true
        $events[-1].Event | Should Be 'terminal'
    }

    It 'reads a Dart checkpoint only when explicitly targeted' {
        $fakeBuilds.Enqueue([pscustomobject]$terminal)

        $event = & $script:waiter -BuildId 101 -ExpectedBranch $script:watchBranch -ExpectedSourceVersion $script:watchVersion `
            -Organization microsoft -Project Dart | ConvertFrom-Json

        ($observedTargets -join ',') | Should Be 'microsoft/Dart'
        $event.Event | Should Be 'terminal'
    }
}
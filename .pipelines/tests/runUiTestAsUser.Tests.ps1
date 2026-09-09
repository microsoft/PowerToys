# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

#Requires -Version 7.0
# Run Pester with pwsh: worker probes launch the current PowerShell executable.
$scriptPath = Join-Path $PSScriptRoot '..\runUiTestAsUser.ps1'
$powerShell = (Get-Process -Id $PID).Path

Describe 'runUiTestAsUser' {
    BeforeEach {
        $runId = [guid]::NewGuid().ToString('N')
        $runDirectory = Join-Path $TestDrive "ui-$($runId.Substring(0, 12))"
        New-Item -ItemType Directory -Path $runDirectory | Out-Null
        $requestPath = Join-Path $runDirectory 'request.json'
        $request = @{
            RunId = $runId
            RunDirectory = $runDirectory
            ResultsDirectory = $runDirectory
            InteractiveUser = 'InvalidDomain\InvalidUiTestUser'
            TestExecutable = (Join-Path $TestDrive 'must-not-run.exe')
            TimeoutMinutes = 1
            Environment = @{}
        }
    }

    It 'reports a mismatched interactive identity without starting a test executable' {
        $request | ConvertTo-Json | Set-Content -LiteralPath $requestPath
        & $powerShell -NoProfile -NonInteractive -File $scriptPath -RequestPath $requestPath

        $LASTEXITCODE | Should Be 1
        $status = Get-Content (Join-Path $runDirectory 'status.json') -Raw | ConvertFrom-Json
        $status.RunId | Should Be $runId
        $status.ExitCode | Should Be 1
        $status.Error | Should Match 'requested non-elevated interactive user'
        Test-Path (Join-Path $runDirectory 'status.tmp') | Should Be $false
        Test-Path (Join-Path $runDirectory 'stdout.log') | Should Be $false
    }

    $missingFields = @('RunId', 'RunDirectory', 'TestExecutable', 'ResultsDirectory', 'InteractiveUser', 'TimeoutMinutes', 'Environment')
    It 'rejects a missing <Field> before checking identity' -TestCases @(
        $missingFields | ForEach-Object { @{ Field = $_ } }
    ) {
        param($Field)
        $request.Remove($Field)
        $request | ConvertTo-Json | Set-Content -LiteralPath $requestPath

        & $powerShell -NoProfile -NonInteractive -File $scriptPath -RequestPath $requestPath

        $LASTEXITCODE | Should Be 1
        $status = Get-Content (Join-Path $runDirectory 'status.json') -Raw | ConvertFrom-Json
        $status.ExitCode | Should Be 1
        $status.Error | Should Match "^Invalid UI-test request: $Field"
        Test-Path (Join-Path $runDirectory 'desktop.txt') | Should Be $false
        Test-Path (Join-Path $runDirectory 'stdout.log') | Should Be $false
    }

    It 'rejects malformed <Field>: <Case>' -TestCases @(
        @{ Field = 'RunId'; Case = 'not a GUID'; Value = 'invalid' }
        @{ Field = 'RunId'; Case = 'different run'; Value = ('0' * 32) }
        @{ Field = 'InteractiveUser'; Case = 'empty'; Value = '' }
        @{ Field = 'TestExecutable'; Case = 'relative'; Value = 'must-not-run.exe' }
        @{ Field = 'TestExecutable'; Case = 'invalid path'; Value = "C:\invalid$([char]0).exe" }
        @{ Field = 'TimeoutMinutes'; Case = 'zero'; Value = 0 }
        @{ Field = 'TimeoutMinutes'; Case = 'over limit'; Value = 121 }
        @{ Field = 'TimeoutMinutes'; Case = 'fraction'; Value = 1.5 }
        @{ Field = 'TimeoutMinutes'; Case = 'string'; Value = '1' }
        @{ Field = 'TimeoutMinutes'; Case = 'boolean'; Value = $true }
        @{ Field = 'Environment'; Case = 'array'; Value = @('DOTNET_ROOT') }
        @{ Field = 'Environment'; Case = 'null'; Value = $null }
        @{ Field = 'Environment'; Case = 'unsupported name'; Value = @{ NOT_ALLOWED = 'do-not-serialize-this-value' } }
        @{ Field = 'Environment'; Case = 'non-string value'; Value = @{ DOTNET_ROOT = 123 } }
        @{ Field = 'Filter'; Case = 'array'; Value = @('filter') }
    ) {
        param($Field, $Case, $Value)
        $request[$Field] = $Value
        $request | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $requestPath

        & $powerShell -NoProfile -NonInteractive -File $scriptPath -RequestPath $requestPath

        $LASTEXITCODE | Should Be 1
        $status = Get-Content (Join-Path $runDirectory 'status.json') -Raw | ConvertFrom-Json
        $status.ExitCode | Should Be 1
        $status.Error | Should Match "^Invalid UI-test request: $Field"
        $status.Error | Should Not Match 'do-not-serialize-this-value'
        Test-Path (Join-Path $runDirectory 'desktop.txt') | Should Be $false
    }

    It 'reports redirected <Field> only beside the request' -TestCases @(
        @{ Field = 'RunDirectory' }
        @{ Field = 'ResultsDirectory' }
    ) {
        param($Field)
        $untrustedDirectory = Join-Path $TestDrive 'must-not-create'
        $request[$Field] = $untrustedDirectory
        $request | ConvertTo-Json | Set-Content -LiteralPath $requestPath

        & $powerShell -NoProfile -NonInteractive -File $scriptPath -RequestPath $requestPath

        $LASTEXITCODE | Should Be 1
        $status = Get-Content (Join-Path $runDirectory 'status.json') -Raw | ConvertFrom-Json
        $status.RunId | Should Be $runId
        $status.Error | Should Match '^Invalid UI-test request: RunDirectory and ResultsDirectory'
        Test-Path $untrustedDirectory | Should Be $false
        Test-Path (Join-Path $runDirectory 'desktop.txt') | Should Be $false
    }

    It 'durably rejects unreadable JSON without repeating its contents' -TestCases @(
        @{ Json = '{"do-not-serialize-this-value":' }
        @{ Json = 'null' }
        @{ Json = '[]' }
    ) {
        param($Json)
        $Json | Set-Content -LiteralPath $requestPath

        & $powerShell -NoProfile -NonInteractive -File $scriptPath -RequestPath $requestPath

        $LASTEXITCODE | Should Be 1
        $status = Get-Content (Join-Path $runDirectory 'status.json') -Raw | ConvertFrom-Json
        $status.ExitCode | Should Be 1
        $status.Error | Should Match '^Invalid UI-test request:'
        $status.Error | Should Not Match 'do-not-serialize-this-value'
        Test-Path (Join-Path $runDirectory 'status.tmp') | Should Be $false
    }

    It 'accepts the upper timeout bound and allow-listed environment before rejecting identity' {
        $request.TimeoutMinutes = 120
        $request.Environment = @{
            DOTNET_ROOT = 'C:\private\dotnet'
            DOTNET_ROOT_X64 = 'C:\private\dotnet-x64'
            DOTNET_ROOT_ARM64 = 'C:\private\dotnet-arm64'
            WINAPP_CLI_PATH = 'C:\private\winapp.exe'
            WINAPP_CLI_INVOKE_TIMEOUT_SECONDS = '30'
            platform = 'x64Win11'
            TF_BUILD = 'True'
            useInstallerForTest = 'true'
            POWERTOYS_INSTALL_DIR = 'C:\PowerToys'
        }
        $request | ConvertTo-Json | Set-Content -LiteralPath $requestPath

        & $powerShell -NoProfile -NonInteractive -File $scriptPath -RequestPath $requestPath

        $LASTEXITCODE | Should Be 1
        $status = Get-Content (Join-Path $runDirectory 'status.json') -Raw | ConvertFrom-Json
        $status.Error | Should Match 'requested non-elevated interactive user'
        Test-Path (Join-Path $runDirectory 'stdout.log') | Should Be $false
    }

    It 'does not unwrap an array containing one otherwise valid request' {
        "[$($request | ConvertTo-Json)]" | Set-Content -LiteralPath $requestPath

        & $powerShell -NoProfile -NonInteractive -File $scriptPath -RequestPath $requestPath

        $LASTEXITCODE | Should Be 1
        $status = Get-Content (Join-Path $runDirectory 'status.json') -Raw | ConvertFrom-Json
        $status.Error | Should Be 'Invalid UI-test request: expected a JSON object.'
        Test-Path (Join-Path $runDirectory 'desktop.txt') | Should Be $false
    }

    It 'rejects a missing executable before scheduling any desktop work' {
        $resultsDirectory = Join-Path $TestDrive 'must-not-create'
        & $powerShell -NoProfile -NonInteractive -File $scriptPath `
            -TestExecutable (Join-Path $TestDrive 'missing.exe') `
            -ResultsDirectory $resultsDirectory 2>&1 | Out-Null

        $LASTEXITCODE | Should Be 1
        Test-Path $resultsDirectory | Should Be $false
    }

    It 'resolves the interactive account before creating result directories' {
        $testExecutable = Join-Path $TestDrive 'must-not-run.exe'
        'not an executable' | Set-Content -LiteralPath $testExecutable
        $resultsDirectory = Join-Path $TestDrive 'must-not-create'

        $failure = & $powerShell -NoProfile -NonInteractive -File $scriptPath `
            -TestExecutable $testExecutable -ResultsDirectory $resultsDirectory `
            -InteractiveUser 'InvalidDomain\InvalidUiTestUser' 2>&1 | Out-String

        $LASTEXITCODE | Should Be 1
        $failure | Should Match 'Could not resolve the interactive user'
        Test-Path $resultsDirectory | Should Be $false
    }
}

Describe 'runUiTestAsUser controller contracts without desktop work' {
    BeforeEach {
        $state = @{}
        $state.ResultsDirectory = [IO.Path]::GetFullPath((Join-Path $TestDrive ([guid]::NewGuid().ToString('N'))))
        $testExecutable = Join-Path $TestDrive 'must-not-run.exe'
        'not an executable' | Set-Content -LiteralPath $testExecutable
        $state.TaskStates = @('Ready')
        $state.PollIndex = -1
        $state.LastRunTime = [datetime]'2001-01-01'
        $state.TaskResult = 0
        $state.TaskResults = @()
        $state.InfoCalls = 0
        $state.StatusFields = @{ ExitCode = 0; Error = $null }
        $state.WriteStatus = $true
        $state.WrapStatusInArray = $false
        $state.CapturedRequest = $null
        $state.AclWrites = [Collections.Generic.List[object]]::new()
        $state.OriginalDacl = 'D:AI(A;OICI;FA;;;SY)(A;OICIID;FR;;;BU)'
        $state.FailRequestWrite = $false
        $state.FailRegistration = $false
        $state.FailCleanup = $false

        Mock Get-Acl {
            $acl = [Security.AccessControl.DirectorySecurity]::new()
            $acl.SetSecurityDescriptorSddlForm($state.OriginalDacl)
            $acl
        }
        Mock Set-Acl {
            param($LiteralPath, $AclObject)
            $state.AclWrites.Add([pscustomobject]@{
                Path = $LiteralPath
                Dacl = $AclObject.GetSecurityDescriptorSddlForm([Security.AccessControl.AccessControlSections]::Access)
            })
            if ($state.FailRequestWrite -and $state.AclWrites.Count -eq 1) {
                $requestFile = Join-Path $LiteralPath 'request.json'
                [IO.File]::WriteAllText($requestFile, 'read-only request')
                (Get-Item -LiteralPath $requestFile).IsReadOnly = $true
            }
        }
        Mock New-ScheduledTaskAction { [Microsoft.Management.Infrastructure.CimInstance]::new('MSFT_TaskAction') }
        Mock New-ScheduledTaskPrincipal { [Microsoft.Management.Infrastructure.CimInstance]::new('MSFT_TaskPrincipal') }
        Mock New-ScheduledTaskSettingsSet { [Microsoft.Management.Infrastructure.CimInstance]::new('MSFT_TaskSettings') }
        Mock Register-ScheduledTask {
            if ($state.FailRegistration) { throw 'registration failed' }
        }
        Mock Unregister-ScheduledTask {
            if ($state.FailCleanup) { throw 'task cleanup failed' }
        }
        Mock Stop-ScheduledTask {}
        # Close file handles inside mocks before Pester 3 cleans up the test drive.
        Mock Get-Content {
            param($LiteralPath)
            [IO.File]::ReadAllText([string]$LiteralPath)
        } -ParameterFilter { $null -ne $LiteralPath }
        Mock Start-ScheduledTask {
            $requestFile = Get-ChildItem -LiteralPath $state.ResultsDirectory -Filter request.json -Recurse
            $state.CapturedRequest = Get-Content -LiteralPath $requestFile.FullName -Raw | ConvertFrom-Json
            if ($state.WriteStatus) {
                $status = @{ RunId = $state.CapturedRequest.RunId }
                foreach ($name in $state.StatusFields.Keys) {
                    $status[$name] = $state.StatusFields[$name]
                }
                $json = $status | ConvertTo-Json
                if ($state.WrapStatusInArray) { $json = "[$json]" }
                [IO.File]::WriteAllText((Join-Path $requestFile.DirectoryName 'status.json'), $json)
            }
            [IO.File]::WriteAllText((Join-Path $requestFile.DirectoryName 'desktop.txt'), 'mock desktop readiness')
        }
        Mock Start-Sleep {
            $state.PollIndex++
            if ($state.PollIndex -gt $state.TaskStates.Count) {
                throw 'Controller exceeded mocked scheduler observations.'
            }
        }
        Mock Get-ScheduledTask {
            $index = [Math]::Max(0, [Math]::Min($state.PollIndex, $state.TaskStates.Count - 1))
            [pscustomobject]@{ State = $state.TaskStates[$index] }
        }
        Mock Get-ScheduledTaskInfo {
            $result = $state.TaskResult
            if ($state.TaskResults.Count -gt 0) {
                $result = $state.TaskResults[[Math]::Min($state.InfoCalls, $state.TaskResults.Count - 1)]
            }
            $state.InfoCalls++
            [pscustomobject]@{ LastRunTime = $state.LastRunTime; LastTaskResult = $result }
        }
    }

    It 'uses one short folder for request and test results and restores only its original DACL' {
        & $scriptPath -TestExecutable $testExecutable -ResultsDirectory $state.ResultsDirectory

        $LASTEXITCODE | Should Be 0
        $runDirectory = $state.CapturedRequest.RunDirectory
        (Split-Path $runDirectory -Parent) | Should Be $state.ResultsDirectory
        (Split-Path $runDirectory -Leaf) | Should Match '^ui-[0-9a-f]{12}$'
        $state.CapturedRequest.ResultsDirectory | Should Be $runDirectory
        $state.CapturedRequest.InteractiveUser | Should Be ([Security.Principal.WindowsIdentity]::GetCurrent().Name)
        $state.AclWrites.Count | Should Be 2
        $state.AclWrites[0].Path | Should Be $runDirectory
        $state.AclWrites[1].Path | Should Be $runDirectory
        $state.AclWrites[0].Dacl | Should Not Be $state.OriginalDacl
        $state.AclWrites[1].Dacl | Should Be $state.OriginalDacl
        Assert-MockCalled New-ScheduledTaskPrincipal -Times 1 -Exactly -Scope It -ParameterFilter {
            $LogonType -eq 'Interactive' -and $RunLevel -eq 'Limited'
        }
        Assert-MockCalled Unregister-ScheduledTask -Times 1 -Exactly -Scope It
    }

    It 'waits for a terminal task even when status already exists' {
        $state.TaskStates = @('Running', 'Queued', 'Running', 'Ready')

        & $scriptPath -TestExecutable $testExecutable -ResultsDirectory $state.ResultsDirectory

        $LASTEXITCODE | Should Be 0
        Assert-MockCalled Start-Sleep -Times 4 -Exactly -Scope It
        Assert-MockCalled Stop-ScheduledTask -Times 0 -Exactly -Scope It
    }

    It 'does not treat desktop and status files alone as a launched scheduler task' {
        $state.TaskStates = @('Ready', 'Running', 'Ready')
        $state.LastRunTime = [datetime]'1899-12-30'

        & $scriptPath -TestExecutable $testExecutable -ResultsDirectory $state.ResultsDirectory

        $LASTEXITCODE | Should Be 0
        Assert-MockCalled Start-Sleep -Times 3 -Exactly -Scope It
    }

    It 'rejects scheduler and status disagreement after the running task completes' {
        $state.TaskStates = @('Running', 'Ready')
        $state.TaskResult = 1

        { & $scriptPath -TestExecutable $testExecutable -ResultsDirectory $state.ResultsDirectory } |
            Should Throw 'does not match status exit code'

        Assert-MockCalled Start-Sleep -Times 2 -Exactly -Scope It
        $state.AclWrites[1].Dacl | Should Be $state.OriginalDacl
    }

    It 'propagates child exit code <ExitCode> with scheduler representation <TaskResult>' -TestCases @(
        @{ ExitCode = 7; TaskResult = 7 }
        @{ ExitCode = -1; TaskResult = 4294967295L }
        @{ ExitCode = -2147483648L; TaskResult = 2147483648L }
    ) {
        param($ExitCode, $TaskResult)
        $state.StatusFields.ExitCode = $ExitCode
        $state.TaskResult = $TaskResult

        & $scriptPath -TestExecutable $testExecutable -ResultsDirectory $state.ResultsDirectory

        $LASTEXITCODE | Should Be $ExitCode
        $state.AclWrites[1].Dacl | Should Be $state.OriginalDacl
    }

    It 'refreshes a running scheduler result after observing terminal state' {
        $state.TaskResults = @(267009, 0)

        & $scriptPath -TestExecutable $testExecutable -ResultsDirectory $state.ResultsDirectory

        $LASTEXITCODE | Should Be 0
        Assert-MockCalled Get-ScheduledTaskInfo -Times 2 -Exactly -Scope It
    }

    It 'rejects a completed task without durable status' {
        $state.WriteStatus = $false

        { & $scriptPath -TestExecutable $testExecutable -ResultsDirectory $state.ResultsDirectory } |
            Should Throw 'exited without status'
    }

    It 'rejects an invalid scheduler result: <Case>' -TestCases @(
        @{ Case = 'null'; TaskResult = $null }
        @{ Case = 'string'; TaskResult = '0' }
        @{ Case = 'overflow'; TaskResult = 4294967296L }
    ) {
        param($Case, $TaskResult)
        $state.TaskResult = $TaskResult

        { & $scriptPath -TestExecutable $testExecutable -ResultsDirectory $state.ResultsDirectory } |
            Should Throw 'invalid scheduler result'
    }

    It 'rejects status from a different run' {
        $state.StatusFields.RunId = 'different-run'

        { & $scriptPath -TestExecutable $testExecutable -ResultsDirectory $state.ResultsDirectory } |
            Should Throw 'does not match the requested run'
    }

    It 'rejects malformed status: <Case>' -TestCases @(
        @{ Case = 'missing ExitCode'; Fields = @{ Error = $null } }
        @{ Case = 'missing Error'; Fields = @{ ExitCode = 0 } }
        @{ Case = 'null ExitCode'; Fields = @{ ExitCode = $null; Error = $null } }
        @{ Case = 'string ExitCode'; Fields = @{ ExitCode = '0'; Error = $null } }
        @{ Case = 'fractional ExitCode'; Fields = @{ ExitCode = 0.5; Error = $null } }
        @{ Case = 'overflow ExitCode'; Fields = @{ ExitCode = 4294967296L; Error = $null } }
        @{ Case = 'invalid Error'; Fields = @{ ExitCode = 0; Error = $false } }
    ) {
        param($Case, $Fields)
        $state.StatusFields = $Fields

        { & $scriptPath -TestExecutable $testExecutable -ResultsDirectory $state.ResultsDirectory } |
            Should Throw 'invalid ExitCode or Error'
    }

    It 'rejects a worker error even when both exit codes are zero' {
        $state.StatusFields.Error = 'worker failure'

        { & $scriptPath -TestExecutable $testExecutable -ResultsDirectory $state.ResultsDirectory } |
            Should Throw 'Interactive UI-test launch failed: worker failure'
    }

    It 'does not unwrap an array containing otherwise successful status' {
        $state.WrapStatusInArray = $true

        { & $scriptPath -TestExecutable $testExecutable -ResultsDirectory $state.ResultsDirectory } |
            Should Throw 'invalid ExitCode or Error'
    }

    It 'restores the DACL when request writing fails' {
        $state.FailRequestWrite = $true

        try {
            $failure = $null
            try {
                & $scriptPath -TestExecutable $testExecutable -ResultsDirectory $state.ResultsDirectory
            }
            catch {
                $failure = $_.Exception.Message
            }
            $failure | Should Not BeNullOrEmpty

            $state.AclWrites.Count | Should Be 2
            $state.AclWrites[1].Dacl | Should Be $state.OriginalDacl
            Assert-MockCalled Register-ScheduledTask -Times 0 -Exactly -Scope It
        }
        finally {
            (Get-Item -LiteralPath (Join-Path $state.AclWrites[0].Path 'request.json')).IsReadOnly = $false
        }
    }

    It 'restores the DACL when registration fails' {
        $state.FailRegistration = $true

        { & $scriptPath -TestExecutable $testExecutable -ResultsDirectory $state.ResultsDirectory } |
            Should Throw 'registration failed'

        $state.AclWrites.Count | Should Be 2
        $state.AclWrites[1].Dacl | Should Be $state.OriginalDacl
        Assert-MockCalled Unregister-ScheduledTask -Times 0 -Exactly -Scope It
    }

    It 'restores the DACL even when task cleanup fails' {
        $state.FailCleanup = $true

        { & $scriptPath -TestExecutable $testExecutable -ResultsDirectory $state.ResultsDirectory } |
            Should Throw 'task cleanup failed'

        $state.AclWrites.Count | Should Be 2
        $state.AclWrites[1].Dacl | Should Be $state.OriginalDacl
    }
}

Describe 'UI-test pipeline non-elevated dispatch' {
    It 'fails infrastructure instead of using the DLL when the required executable is absent' {
        $template = Get-Content (Join-Path $PSScriptRoot '..\v2\templates\job-test-project.yml') -Raw
        $template | Should Match '\$nonElevatedSuites = @\(''CropAndLock.UITests''\)'
        $dispatch = [regex]::Match($template, '(?ms)^ {10}if \(\$nonElevatedSuites -contains \$base\) \{.*?^ {10}\}').Value
        $dispatch | Should Not BeNullOrEmpty
        $nonElevatedSuites = @('CropAndLock.UITests')
        $base = 'CropAndLock.UITests'
        $exe = Join-Path $TestDrive "$base.exe"
        $dll = Join-Path $TestDrive "$base.dll"
        'not a test runner' | Set-Content -LiteralPath $dll

        { & ([scriptblock]::Create($dispatch)) } | Should Throw 'requires its staged executable for non-elevated dispatch'
    }
}

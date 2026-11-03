# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

#Requires -Version 7.0
. "$PSScriptRoot\..\mwb\MwbSandboxCi.Common.ps1"
$scriptPath = Join-Path $PSScriptRoot '..\mwb\mwbSandboxExperiment.ps1'
$tokens = $null
$parseErrors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile($scriptPath, [ref]$tokens, [ref]$parseErrors)
if ($parseErrors) { throw 'The MWB CI helper has PowerShell parse errors.' }
# Tests never schedule tasks, provision a firewall, or run UI. Diagnostic ACL checks
# are mocked; launcher subprocesses execute only test-created sibling scripts.
foreach ($name in @('Assert-MwbLocalPath', 'Get-MwbSandboxPayload', 'Get-MwbPublicRunRoot',
    'Assert-MwbProtectedDirectory', 'Write-MwbProvisioningDiagnostics')) {
    $function = $ast.Find({
        param($node)
        $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq $name
    }, $true)
    . ([scriptblock]::Create($function.Extent.Text))
}

function Invoke-MwbTestProvisioner {
    param([string] $Directory, [string[]] $ExtraArguments = @())

    $start = [Diagnostics.ProcessStartInfo]::new((Get-Process -Id $PID).Path)
    $start.UseShellExecute = $false
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    foreach ($argument in @('-NoLogo', '-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass',
        '-File', (Join-Path $Directory 'Invoke-MwbProvisioning.ps1'),
        '-ProductRoot', (Join-Path $Directory 'product & files'), '-TestUser', 'CI\standard-user',
        '-RunId', '01234567-89ab-cdef-0123-456789abcdef',
        '-GuestArchivePath', (Join-Path $Directory 'guest archive.zip'))) {
        $start.ArgumentList.Add($argument)
    }
    foreach ($argument in $ExtraArguments) { $start.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $start
    try {
        if (-not $process.Start()) { throw 'The test logging launcher did not start.' }
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit(30000)) {
            $process.Kill($true)
            throw 'The test logging launcher exceeded 30 seconds.'
        }
        if (-not [Threading.Tasks.Task]::WaitAll([Threading.Tasks.Task[]]@($stdout, $stderr), 5000)) {
            throw 'The test logging launcher did not close its output streams.'
        }
        [pscustomobject]@{
            ExitCode = $process.ExitCode
            StandardOutput = $stdout.GetAwaiter().GetResult()
            StandardError = $stderr.GetAwaiter().GetResult()
        }
    }
    finally {
        $process.Dispose()
    }
}

Describe 'MWB trusted-script execution policy' {
    It 'reuses the running PowerShell host rather than crossing module environments' {
        $assignment = $ast.Find({
            param($node)
            $node -is [Management.Automation.Language.AssignmentStatementAst] -and
                $node.Left.Extent.Text -eq '$powerShell'
        }, $true)
        $assignment.Right.Extent.Text | Should Be '(Get-Process -Id $PID).Path'
    }

    It 'uses process-local Bypass for the protected initializer' {
        $assignment = $ast.Find({
            param($node)
            $node -is [Management.Automation.Language.AssignmentStatementAst] -and
                $node.Left.Extent.Text -eq '$arguments'
        }, $true)
        $assignment.Right.Extent.Text | Should Match '-ExecutionPolicy Bypass -File'
    }

    It 'uses a checked process-local Bypass subprocess for protected cleanup' {
        $command = $ast.Find({
            param($node)
            $node -is [Management.Automation.Language.CommandAst] -and
                $node.CommandElements[0].Extent.Text -eq '$powerShell'
        }, $true)
        $command.Extent.Text | Should Match '-ExecutionPolicy Bypass'
        $command.Extent.Text | Should Match '-File \$cleanupPath -StateRoot \$stateRoot'
        @($ast.FindAll({
            param($node)
            $node -is [Management.Automation.Language.CommandAst] -and
                $node.GetCommandName() -eq 'Set-ExecutionPolicy'
        }, $true)).Count | Should Be 0
    }
}

Describe 'MWB protected provisioning launcher' {
    BeforeEach {
        $directory = Join-Path $TestDrive "logging fixture $([guid]::NewGuid().ToString('N'))"
        $logRoot = Join-Path $directory 'provisioning-logs'
        New-Item -ItemType Directory -Path $logRoot -Force | Out-Null
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot '..\mwb\Invoke-MwbProvisioning.ps1') -Destination $directory
        $initializer = Join-Path $directory 'Initialize-AutonomousHost.ps1'
        $parameters = @'
param([string] $ProductRoot, [string] $TestUser, [guid] $RunId,
    [int] $NetworkTimeoutSeconds, [string] $GuestArchivePath, [string] $Configuration = 'Release')
'@
    }

    It 'captures the original terminating error before a provisioning marker exists' {
        Set-Content -LiteralPath $initializer -Value ($parameters + @'

[Console]::Out.WriteLine('Initializer entered before marker')
throw 'MWB fixture: exact pre-marker failure'
'@)
        $result = Invoke-MwbTestProvisioner $directory
        $result.ExitCode | Should Be 1
        $result.StandardOutput | Should BeNullOrEmpty
        $result.StandardError | Should BeNullOrEmpty
        Get-Content -LiteralPath (Join-Path $logRoot 'stdout.log') -Raw | Should Match 'Initializer entered before marker'
        Get-Content -LiteralPath (Join-Path $logRoot 'stderr.log') -Raw | Should Match 'MWB fixture: exact pre-marker failure'
        Test-Path -LiteralPath (Join-Path $directory 'host-provisioning.json') | Should Be $false
        $started = Get-Content -LiteralPath (Join-Path $directory 'provisioning-started.json') -Raw | ConvertFrom-Json
        $started.RunId | Should Be '01234567-89ab-cdef-0123-456789abcdef'
        $started.ProductRoot | Should Be (Join-Path $directory 'product & files')
        $started.Status | Should Be 'Started'
    }

    It 'captures initializer parser failures before any initializer code runs' {
        Set-Content -LiteralPath $initializer -Value '$value = )'
        $result = Invoke-MwbTestProvisioner $directory
        $result.ExitCode | Should Be 1
        Get-Content -LiteralPath (Join-Path $logRoot 'stderr.log') -Raw | Should Match 'ParserError'
    }

    It 'captures initializer parameter-binding failures' {
        Set-Content -LiteralPath $initializer -Value 'param([int] $ProductRoot)'
        $result = Invoke-MwbTestProvisioner $directory
        $result.ExitCode | Should Be 1
        Get-Content -LiteralPath (Join-Path $logRoot 'stderr.log') -Raw | Should Match 'Cannot process argument transformation'
    }

    It 'preserves the initializer exit code and safely quoted fixed arguments' -TestCases @(
        @{ ExitCode = 0 }
        @{ ExitCode = 37 }
    ) {
        param($ExitCode)
        Set-Content -LiteralPath $initializer -Value ($parameters + @"

[Console]::Out.WriteLine(`$ProductRoot)
[Console]::Out.WriteLine(`$GuestArchivePath)
[Console]::Out.WriteLine(`$NetworkTimeoutSeconds)
[Console]::Error.WriteLine('stderr fixture')
exit $ExitCode
"@)
        $result = Invoke-MwbTestProvisioner $directory
        $result.ExitCode | Should Be $ExitCode
        $result.StandardOutput | Should BeNullOrEmpty
        $result.StandardError | Should BeNullOrEmpty
        $lines = @(Get-Content -LiteralPath (Join-Path $logRoot 'stdout.log'))
        $lines[0] | Should Be (Join-Path $directory 'product & files')
        $lines[1] | Should Be (Join-Path $directory 'guest archive.zip')
        $lines[2] | Should Be '900'
        Get-Content -LiteralPath (Join-Path $logRoot 'stderr.log') -Raw | Should Match 'stderr fixture'
    }

    It 'drains both streams concurrently even with huge output and oversized lines' {
        Set-Content -LiteralPath $initializer -Value ($parameters + @'

$stdout = [Console]::OpenStandardOutput()
$stderr = [Console]::OpenStandardError()
try {
    # Stress redirected pipe draining, not console-host text rendering.
    $line = [Text.Encoding]::ASCII.GetBytes(('x' * 4096) + [Environment]::NewLine)
    $prefix = [Text.Encoding]::ASCII.GetBytes('a' * 1048576)
    $stdout.Write($prefix, 0, $prefix.Length)
    $prefix = [Text.Encoding]::ASCII.GetBytes('b' * 1048576)
    $stderr.Write($prefix, 0, $prefix.Length)
    for ($index = 0; $index -lt 1024; $index++) {
        $stdout.Write($line, 0, $line.Length)
        $stderr.Write($line, 0, $line.Length)
    }
    $tail = [Text.Encoding]::ASCII.GetBytes('stdout complete' + [Environment]::NewLine)
    $stdout.Write($tail, 0, $tail.Length)
    $tail = [Text.Encoding]::ASCII.GetBytes('stderr complete' + [Environment]::NewLine)
    $stderr.Write($tail, 0, $tail.Length)
}
finally {
    $stdout.Dispose()
    $stderr.Dispose()
}
exit 23
'@)
        $result = Invoke-MwbTestProvisioner $directory
        $result.ExitCode | Should Be 23
        $result.StandardOutput | Should BeNullOrEmpty
        $result.StandardError | Should BeNullOrEmpty
        foreach ($name in @('stdout', 'stderr')) {
            $path = Join-Path $logRoot "$name.log"
            (Get-Item -LiteralPath $path).Length | Should BeGreaterThan 5242880
            Get-Content -LiteralPath $path -Tail 1 | Should Be "$name complete"
        }
    }

    It 'forwards the optional modern backend without changing legacy defaults' {
        Set-Content -LiteralPath $initializer -Value @'
param([string] $ProductRoot, [string] $TestUser, [guid] $RunId,
    [int] $NetworkTimeoutSeconds, [string] $GuestArchivePath,
    [string] $SandboxBackend = 'Legacy', [string] $SandboxWinAppPath, [string] $Configuration = 'Release')
[Console]::Out.WriteLine($SandboxBackend)
[Console]::Out.WriteLine($SandboxWinAppPath)
'@
        $preview = Join-Path $directory 'sandbox preview\winapp.exe'
        $result = Invoke-MwbTestProvisioner $directory -ExtraArguments @(
            '-SandboxBackend', 'WinApp', '-SandboxWinAppPath', $preview)
        $result.ExitCode | Should Be 0
        $lines = @(Get-Content -LiteralPath (Join-Path $logRoot 'stdout.log'))
        $lines[0] | Should Be 'WinApp'
        $lines[1] | Should Be $preview
        $result = Invoke-MwbTestProvisioner $directory
        $result.ExitCode | Should Be 0
        (Get-Content -LiteralPath (Join-Path $logRoot 'stdout.log'))[0] | Should Be 'Legacy'
    }

    It 'does not run the initializer or report success when log files cannot be opened' {
        Remove-Item -LiteralPath $logRoot -Recurse
        Set-Content -LiteralPath $initializer -Value ($parameters + @'

Set-Content -LiteralPath (Join-Path $PSScriptRoot 'unexpected-initializer.txt') -Value 'started'
exit 0
'@)
        $result = Invoke-MwbTestProvisioner $directory
        $result.ExitCode | Should Not Be 0
        Test-Path -LiteralPath (Join-Path $directory 'unexpected-initializer.txt') | Should Be $false
    }
}

Describe 'MWB separate provisioning readiness budgets' {
    BeforeEach {
        $script:provisioningRunId = [guid]::NewGuid()
        $script:provisioningProduct = 'C:\fixture\product'
        $script:provisioningEpoch = [datetime]'2026-10-29T00:00:00Z'
        $script:provisioningClock = $script:provisioningEpoch
        $script:provisioningState = [pscustomobject]@{
            Started = $null
            Marker = $null
            TaskRunning = $true
            TaskHasRun = $true
            TaskResult = 267009
        }
        $script:provisioningStarted = [pscustomobject]@{
            RunId = $script:provisioningRunId.ToString()
            ProductRoot = $script:provisioningProduct
            Status = 'Started'
        }
        Mock Get-Date { $script:provisioningClock }
        Mock Start-Sleep { $script:provisioningClock = $script:provisioningClock.AddSeconds(30) }
    }

    It 'allows fingerprinting beyond the startup deadline without accepting a stale removed marker' {
        $script:provisioningState.Started = $script:provisioningStarted
        $script:provisioningState.Marker = [pscustomobject]@{
            RunId = [guid]::NewGuid().ToString()
            Status = 'Removed'
        }
        $marker = Wait-MwbProvisioningReady -RunId $script:provisioningRunId `
            -ProductRoot $script:provisioningProduct -ReadState {
                if (($script:provisioningClock - $script:provisioningEpoch).TotalSeconds -ge 120) {
                    $script:provisioningState.Marker = [pscustomobject]@{
                        RunId = $script:provisioningRunId.ToString()
                        ProductRoot = $script:provisioningProduct
                        Status = 'WaitingForSandbox'
                    }
                }
                $script:provisioningState
            }
        $marker.Status | Should Be 'WaitingForSandbox'
        ($script:provisioningClock - $script:provisioningEpoch).TotalSeconds | Should Be 120
    }

    It 'still bounds a worker that never acknowledges startup' {
        $script:provisioningState.TaskRunning = $false
        $script:provisioningState.TaskHasRun = $false
        { Wait-MwbProvisioningReady -RunId $script:provisioningRunId `
            -ProductRoot $script:provisioningProduct -ReadState { $script:provisioningState } } |
            Should Throw 'did not start within 90 seconds'
        ($script:provisioningClock - $script:provisioningEpoch).TotalSeconds | Should Be 90
    }

    It 'does not treat startup or PreparingTrust as readiness or renew the preparation deadline' -TestCases @(
        @{ Status = $null }
        @{ Status = 'PreparingTrust' }
    ) {
        param($Status)
        $script:provisioningState.Started = $script:provisioningStarted
        if ($Status) {
            $script:provisioningState.Marker = [pscustomobject]@{
                RunId = $script:provisioningRunId.ToString()
                ProductRoot = $script:provisioningProduct
                Status = $Status
            }
        }
        { Wait-MwbProvisioningReady -RunId $script:provisioningRunId `
            -ProductRoot $script:provisioningProduct -ReadState { $script:provisioningState } } |
            Should Throw 'did not finish within 300 seconds'
        ($script:provisioningClock - $script:provisioningEpoch).TotalSeconds | Should Be 300
    }

    It 'does not let a late startup acknowledgement extend an expired startup deadline' {
        { Wait-MwbProvisioningReady -RunId $script:provisioningRunId `
            -ProductRoot $script:provisioningProduct -ReadState {
                $script:provisioningClock = $script:provisioningClock.AddSeconds(90)
                $script:provisioningState.Started = $script:provisioningStarted
                $script:provisioningState
            } } | Should Throw 'did not start within 90 seconds'
    }

    It 'rejects a startup acknowledgement with a changed identity or status' -TestCases @(
        @{ Field = 'RunId' }
        @{ Field = 'ProductRoot' }
        @{ Field = 'Status' }
    ) {
        param($Field)
        $script:provisioningStarted.$Field = 'changed'
        $script:provisioningState.Started = $script:provisioningStarted
        { Wait-MwbProvisioningReady -RunId $script:provisioningRunId `
            -ProductRoot $script:provisioningProduct -ReadState { $script:provisioningState } } |
            Should Throw 'startup acknowledgement does not match'
    }

    It 'fails immediately when the worker exits while trust preparation is incomplete' {
        $script:provisioningState.Started = $script:provisioningStarted
        $script:provisioningState.TaskRunning = $false
        $script:provisioningState.TaskResult = 37
        $script:provisioningState.Marker = [pscustomobject]@{
            RunId = $script:provisioningRunId.ToString()
            ProductRoot = $script:provisioningProduct
            Status = 'PreparingTrust'
        }
        { Wait-MwbProvisioningReady -RunId $script:provisioningRunId `
            -ProductRoot $script:provisioningProduct -ReadState { $script:provisioningState } } |
            Should Throw 'scheduler result 37'
        ($script:provisioningClock - $script:provisioningEpoch).TotalSeconds | Should Be 0
    }

    It 'does not accept a ready marker from another run or payload' -TestCases @(
        @{ Field = 'RunId' }
        @{ Field = 'ProductRoot' }
    ) {
        param($Field)
        $script:provisioningState.Started = $script:provisioningStarted
        $script:provisioningState.Marker = [pscustomobject]@{
            RunId = $script:provisioningRunId.ToString()
            ProductRoot = $script:provisioningProduct
            Status = 'Ready'
        }
        $script:provisioningState.Marker.$Field = 'changed'
        { Wait-MwbProvisioningReady -RunId $script:provisioningRunId `
            -ProductRoot $script:provisioningProduct -ReadState { $script:provisioningState } } |
            Should Throw 'BLOCKED_INFRASTRUCTURE'
    }
}

Describe 'MWB bounded public provisioning diagnostics' {
    BeforeEach {
        $directory = Join-Path $TestDrive ([guid]::NewGuid().ToString('N'))
        $logRoot = Join-Path $directory 'provisioning-logs'
        New-Item -ItemType Directory -Path $logRoot -Force | Out-Null
        $stderrPath = Join-Path $logRoot 'stderr.log'
        $stdoutPath = Join-Path $logRoot 'stdout.log'
        $script:diagnosticLines = [Collections.Generic.List[string]]::new()
        Mock Assert-MwbProtectedDirectory {}
        Mock Write-Host { param($Object) $script:diagnosticLines.Add([string]$Object) }
    }

    It 'reports protected log presence without exposing raw errors or stdout before a marker' {
        Set-Content -LiteralPath $stderrPath -Value 'Exact underlying pre-marker error'
        Set-Content -LiteralPath $stdoutPath -Value 'Last completed initializer step'
        Write-MwbProvisioningDiagnostics $directory
        ($script:diagnosticLines -join "`n") | Should Match 'stderr.log: present; bytes='
        ($script:diagnosticLines -join "`n") | Should Match 'stdout.log: present; bytes='
        ($script:diagnosticLines -join "`n") | Should Not Match 'Exact underlying|Last completed'
        Assert-MockCalled Assert-MwbProtectedDirectory -Times 1 -Exactly -ParameterFilter { $Path -eq $directory }
        Assert-MockCalled Assert-MwbProtectedDirectory -Times 1 -Exactly -ParameterFilter { $Path -eq $logRoot }
        Assert-MockCalled Assert-MwbProtectedDirectory -Times 1 -Exactly -ParameterFilter { $Path -eq $stderrPath }
        Assert-MockCalled Assert-MwbProtectedDirectory -Times 1 -Exactly -ParameterFilter { $Path -eq $stdoutPath }
    }

    It 'publishes phase progress without printing raw stdout or stderr' {
        @('private-authentication-state',
            'MWB provisioning: Inspecting public test trust (1.2s)',
            'MWB provisioning: Hashing Sandbox runtime archive (3.4s)',
            'MWB provisioning: unknown private phase (4.5s)') |
            Set-Content -LiteralPath $stdoutPath
        'private-stderr-state' | Set-Content -LiteralPath $stderrPath
        Write-MwbProvisioningDiagnostics $directory
        $output = $script:diagnosticLines -join "`n"
        $output | Should Match 'Inspecting public test trust \(1[.,]2s\)'
        $output | Should Match 'Hashing Sandbox runtime archive \(3[.,]4s\)'
        $output | Should Not Match 'private-authentication-state|private-stderr-state|unknown private phase'
    }

    It 'explicitly reports unavailable logs when the launcher never started' {
        Remove-Item -LiteralPath $logRoot -Recurse
        Write-MwbProvisioningDiagnostics $directory
        ($script:diagnosticLines -join "`n") | Should Match 'logging launcher may not have started'
    }

    It 'distinguishes missing and empty streams' {
        Set-Content -LiteralPath $stderrPath -Value '' -NoNewline
        Write-MwbProvisioningDiagnostics $directory
        ($script:diagnosticLines -join "`n") | Should Match 'stderr.log: present; bytes=0'
        ($script:diagnosticLines -join "`n") | Should Match 'stdout.log: not created'
    }

    It 'publishes only bounded metadata for a large log' {
        [IO.File]::WriteAllText($stderrPath, (('x' * 200 + "`n") * 20000) + "Final error`n")
        Write-MwbProvisioningDiagnostics $directory
        $script:diagnosticLines.Count | Should BeLessThan 42
        ($script:diagnosticLines -join "`n").Length | Should BeLessThan 20000
        ($script:diagnosticLines -join "`n") | Should Not Match 'Final error'
        ($script:diagnosticLines -join "`n") | Should Match 'raw content remains in protected local storage'
    }

    It 'does not print a partial oversized line whose sensitive prefix is outside the read window' {
        [IO.File]::WriteAllText($stderrPath, 'AccountPassword=' + ('x' * 20000) + 'DO_NOT_PUBLISH')
        Write-MwbProvisioningDiagnostics $directory
        ($script:diagnosticLines -join "`n") | Should Match 'raw content remains in protected local storage'
        ($script:diagnosticLines -join "`n") | Should Not Match 'DO_NOT_PUBLISH'
    }

    It 'suppresses an entire sensitive excerpt including wrapped values' -TestCases @(
        @{ Label = 'AccountPassword' }
        @{ Label = 'WindowsSandboxClient.exe' }
        @{ Label = 'AccessToken' }
        @{ Label = 'Credential' }
        @{ Label = 'Secret' }
        @{ Label = 'Authorization' }
    ) {
        param($Label)
        Set-Content -LiteralPath $stderrPath -Value "Sensitive context: $Label`nDO_NOT_PUBLISH"
        Write-MwbProvisioningDiagnostics $directory
        ($script:diagnosticLines -join "`n") | Should Match 'raw content remains in protected local storage'
        ($script:diagnosticLines -join "`n") | Should Not Match 'DO_NOT_PUBLISH|Sensitive context:'
        Get-Content -LiteralPath $stderrPath -Raw | Should Match 'DO_NOT_PUBLISH'
    }

    It 'never publishes ANSI formatting or Azure logging commands and preserves the raw log privately' {
        $raw = "$([char]27)[31mFixture error$([char]27)[0m`n##vso[task.complete result=Succeeded;]not a command"
        Set-Content -LiteralPath $stderrPath -Value $raw
        Write-MwbProvisioningDiagnostics $directory
        ($script:diagnosticLines -join "`n") | Should Not Match 'Fixture error'
        ($script:diagnosticLines -join "`n") | Should Not Match '##|\x1b'
        Get-Content -LiteralPath $stderrPath -Raw | Should Match '##vso'
    }

    It 'refuses diagnostics with invalid ownership before reading their contents' {
        Set-Content -LiteralPath $stderrPath -Value 'DO_NOT_PUBLISH'
        Mock Assert-MwbProtectedDirectory {
            param($Path)
            if ($Path -eq $stderrPath) { throw 'Invalid diagnostic ownership' }
        }
        { Write-MwbProvisioningDiagnostics $directory } | Should Throw 'Invalid diagnostic ownership'
        $script:diagnosticLines.Count | Should Be 0
    }

    It 'reports a locked log without masking the original provisioning failure' {
        Set-Content -LiteralPath $stderrPath -Value 'Fixture error'
        $lock = [IO.File]::Open($stderrPath, 'Open', 'ReadWrite', 'None')
        try {
            {
                Write-MwbProvisioningDiagnostics $directory
                throw 'Original provisioning failure'
            } | Should Throw 'Original provisioning failure'
            ($script:diagnosticLines -join "`n") | Should Match 'stderr.log: present; bytes='
        }
        finally {
            $lock.Dispose()
        }
    }
}

Describe 'MWB diagnostic task ownership contract' {
    It 'stages and hashes the trusted launcher alongside the fixed initializer and cleanup scripts' {
        $ast.Extent.Text | Should Match "foreach \(\`$name in @\('Initialize-AutonomousHost.ps1', 'Remove-AutonomousHost.ps1', 'MwbTestSigning.ps1', 'Invoke-MwbProvisioning.ps1'\)\)"
        $ast.Extent.Text | Should Match '\$hashes\[\$name\] = \$hash'
        $ast.Extent.Text | Should Match 'ScriptHashes = \$hashes'
        $ast.Extent.Text | Should Match "\`$setupPath = Join-Path \`$runRoot 'Invoke-MwbProvisioning.ps1'"
        $ast.Extent.Text | Should Match 'SetupArguments = \$arguments'
        $ast.Extent.Text | Should Match 'New-ScheduledTaskAction -Execute \$powerShell -Argument \$arguments'
    }

    It 'preserves exact action identity checks before stopping or unregistering the task' {
        $ast.Extent.Text | Should Match '\$task.Actions\[0\].Arguments -cne \$manifest.SetupArguments'
        $ast.Extent.Text | Should Match '\$task.Actions\[0\].Arguments -ceq \$manifest.SetupArguments'
        $ast.Extent.Text | Should Match "New-MwbProtectedDirectory \(Join-Path \`$runRoot 'provisioning-logs'\)"
    }

    It 'prints diagnostics before propagating a readiness failure' {
        $wait = $ast.Find({
            param($node)
            $node -is [Management.Automation.Language.TryStatementAst] -and
                $node.Body.Extent.Text -match 'Wait-MwbProvisioningReady'
        }, $true)
        $wait.CatchClauses.Count | Should Be 1
        $wait.CatchClauses[0].Body.Extent.Text | Should Match 'Write-MwbProvisioningDiagnostics \$runRoot\s+throw'
    }
}

Describe 'MWB Sandbox artifact discovery' {
    BeforeEach {
        $artifact = Join-Path $TestDrive "$([guid]::NewGuid().ToString('N'))\build-x64-Release"
        $product = Join-Path $artifact 'x64\Release\x64\Release'
        $tests = Join-Path $product 'tests\MouseWithoutBorders.UITests'
        New-Item -ItemType Directory -Path $tests -Force | Out-Null
        foreach ($name in @('PowerToys.exe', 'PowerToys.MouseWithoutBorders.exe', 'PowerToys.MouseWithoutBorders.dll')) {
            Set-Content -LiteralPath (Join-Path $product $name) -Value 'test fixture'
        }
        foreach ($name in @('MouseWithoutBorders.UITests.exe', 'MouseWithoutBorders.UITests.runtimeconfig.json')) {
            Set-Content -LiteralPath (Join-Path $tests $name) -Value 'test fixture'
        }
        New-Item -ItemType Directory -Path (Join-Path $tests 'Payload') -Force | Out-Null
        foreach ($name in @('Recover-Host.ps1', 'ModernSandboxRecovery.ps1', 'EndpointSupport.ps1', 'NativeSupport.cs', 'MwbTestSigning.ps1')) {
            Set-Content -LiteralPath (Join-Path $tests "Payload\$name") -Value 'test fixture'
        }
    }

    It 'uses the tests and bundle identity from the same Release artifact' {
        $payload = Get-MwbSandboxPayload $artifact
        $payload.ArtifactRoot | Should Be ([IO.Path]::GetFullPath($artifact))
        $payload.TestExecutable | Should Be ([IO.Path]::GetFullPath((Join-Path $tests 'MouseWithoutBorders.UITests.exe')))
    }

    It 'rejects a different configuration rather than falling back to another build' {
        $debug = Join-Path $TestDrive 'build-x64-Debug'
        New-Item -ItemType Directory -Path $debug -Force | Out-Null
        { Get-MwbSandboxPayload $debug } | Should Throw 'build-x64-Release'
    }

    It 'accepts slim artifacts without using installed or raw product binaries' {
        foreach ($name in @('PowerToys.exe', 'PowerToys.MouseWithoutBorders.exe', 'PowerToys.MouseWithoutBorders.dll')) {
            Remove-Item -LiteralPath (Join-Path $product $name)
        }
        $payload = Get-MwbSandboxPayload $artifact
        $payload.ArtifactRoot | Should Be ([IO.Path]::GetFullPath($artifact))
        $payload.TestExecutable | Should Be (Get-Item -LiteralPath (Join-Path $tests 'MouseWithoutBorders.UITests.exe')).FullName
    }

    It 'rejects duplicate test runners instead of running the pilot twice' {
        $duplicate = Join-Path $product 'tests\duplicate'
        New-Item -ItemType Directory -Path $duplicate -Force | Out-Null
        Copy-Item -LiteralPath (Join-Path $tests 'MouseWithoutBorders.UITests.runtimeconfig.json') -Destination $duplicate
        { Get-MwbSandboxPayload $artifact } | Should Throw 'exactly one MWB Sandbox test runner'
    }

    It 'requires the staged MTP executable for standard-user dispatch' {
        Remove-Item -LiteralPath (Join-Path $tests 'MouseWithoutBorders.UITests.exe')
        { Get-MwbSandboxPayload $artifact } | Should Throw 'MTP executable was not staged'
    }

    It 'requires the complete fixed recovery payload before provisioning' -TestCases @(
        @{ Name = 'Recover-Host.ps1' }
        @{ Name = 'ModernSandboxRecovery.ps1' }
        @{ Name = 'EndpointSupport.ps1' }
        @{ Name = 'NativeSupport.cs' }
        @{ Name = 'MwbTestSigning.ps1' }
    ) {
        param($Name)
        Remove-Item -LiteralPath (Join-Path $tests "Payload\$Name")
        { Get-MwbSandboxPayload $artifact } | Should Throw 'recovery payload is incomplete'
    }

    It 'rejects relative or network paths before discovery' -TestCases @(
        @{ Path = 'build-x64-Release' }
        @{ Path = '\\server\build-x64-Release' }
        @{ Path = 'C:\payload" -Command something' }
    ) {
        param($Path)
        { Get-MwbSandboxPayload $Path } | Should Throw 'local absolute path'
    }
}

Describe 'MWB Sandbox ARM64 artifact discovery' {
    BeforeEach {
        $armArtifact = Join-Path $TestDrive "$([guid]::NewGuid().ToString('N'))\build-arm64-Release"
        $armProduct = Join-Path $armArtifact 'arm64\Release\arm64\Release'
        $armTests = Join-Path $armProduct 'tests\MouseWithoutBorders.UITests'
        New-Item -ItemType Directory -Path $armTests -Force | Out-Null
        foreach ($name in @('PowerToys.exe', 'PowerToys.MouseWithoutBorders.exe', 'PowerToys.MouseWithoutBorders.dll')) {
            Set-Content -LiteralPath (Join-Path $armProduct $name) -Value 'test fixture'
        }
        foreach ($name in @('MouseWithoutBorders.UITests.exe', 'MouseWithoutBorders.UITests.runtimeconfig.json')) {
            Set-Content -LiteralPath (Join-Path $armTests $name) -Value 'test fixture'
        }
        New-Item -ItemType Directory -Path (Join-Path $armTests 'Payload') -Force | Out-Null
        foreach ($name in @('Recover-Host.ps1', 'ModernSandboxRecovery.ps1', 'EndpointSupport.ps1', 'NativeSupport.cs', 'MwbTestSigning.ps1')) {
            Set-Content -LiteralPath (Join-Path $armTests "Payload\$name") -Value 'test fixture'
        }
    }

    It 'can inspect coherent ARM64 Release packaging without claiming live ARM64 support' {
        $payload = Get-MwbSandboxPayload $armArtifact -Architecture arm64
        $payload.ArtifactRoot | Should Be ([IO.Path]::GetFullPath($armArtifact))
        $payload.TestExecutable | Should Be ([IO.Path]::GetFullPath((Join-Path $armTests 'MouseWithoutBorders.UITests.exe')))
    }

    It 'rejects the ARM64 artifact when x64 is the default/requested architecture' {
        { Get-MwbSandboxPayload $armArtifact } | Should Throw 'build-x64-Release'
    }

    It 'rejects an x64 artifact when ARM64 is explicitly requested' {
        $x64Artifact = Join-Path $TestDrive "$([guid]::NewGuid().ToString('N'))\build-x64-Release"
        New-Item -ItemType Directory -Path $x64Artifact -Force | Out-Null
        { Get-MwbSandboxPayload $x64Artifact -Architecture arm64 } | Should Throw 'build-arm64-Release'
    }
}

Describe 'MWB public recovery path' {
    It 'resolves and invokes the shared dispatcher for <Mode> from the grouped directory' -TestCases @(
        @{ Mode = 'Run'; Timeout = 70 }
        @{ Mode = 'Recover'; Timeout = 4 }
    ) {
        param($Mode, $Timeout)
        $assignment = $ast.Find({
            param($node)
            $node -is [Management.Automation.Language.AssignmentStatementAst] -and
                $node.Left.Extent.Text -ceq '$launcherPath'
        }, $true)
        $resolveLauncher = [scriptblock]::Create('param($PSScriptRoot)' + [Environment]::NewLine + $assignment.Right.Extent.Text)
        $productionPath = & $resolveLauncher (Get-Item -LiteralPath $scriptPath).DirectoryName
        $command = Get-Command -Name $productionPath -CommandType ExternalScript -ErrorAction Stop
        $productionPath | Should Be $command.Path
        $command.Parameters.ContainsKey('TestExecutable') | Should Be $true
        $command.Parameters.ContainsKey('MwbRecoveryJournal') | Should Be $true

        $repository = Join-Path $TestDrive "dispatcher-$Mode"
        $group = (New-Item -ItemType Directory -Path (Join-Path $repository 'mwb') -Force).FullName
        Set-Content -LiteralPath (Join-Path $repository 'runUiTestAsUser.ps1') -Value @'
param([string]$TestExecutable, [string]$ResultsDirectory, [string]$InteractiveUser,
    [int]$TimeoutMinutes, [guid]$TaskRunId, [string]$MwbRecoveryJournal)
$PSBoundParameters | ConvertTo-Json -Compress
'@
        $launcherPath = & $resolveLauncher $group
        $payload = [pscustomobject]@{ TestExecutable = 'fixture.exe' }
        $manifest = [pscustomobject]@{ TestExecutable = 'fixture.exe'; InteractiveUser = 'CI\fixture' }
        $ResultsDirectory = Join-Path $TestDrive 'results'
        $RunId = [guid]::NewGuid()
        $suiteTimeoutMinutes = 70
        $journalPath = Join-Path $TestDrive 'cleanup-journal.json'
        $modeBlock = $ast.Find({
            param($node)
            $node -is [Management.Automation.Language.IfStatementAst] -and
                $node.Clauses[0].Item1.Extent.Text -ceq "`$Mode -eq '$Mode'"
        }, $true)
        $dispatch = @($modeBlock.Clauses[0].Item2.FindAll({
            param($node)
            $node -is [Management.Automation.Language.CommandAst] -and
                $node.Extent.Text.Contains('-TestExecutable')
        }, $true))
        $dispatch.Count | Should Be 1
        $received = (& ([scriptblock]::Create($dispatch[0].Extent.Text))) | ConvertFrom-Json
        $received.TestExecutable | Should Be 'fixture.exe'
        $received.InteractiveUser | Should Be 'CI\fixture'
        $received.TimeoutMinutes | Should Be $Timeout
        if ($Mode -eq 'Run') {
            $received.TaskRunId | Should Be $RunId.ToString()
        } else {
            $received.MwbRecoveryJournal | Should Be $journalPath
        }
    }

    It 'uses the correlated launcher directory without reading a test journal' {
        $id = [guid]'01234567-89ab-cdef-0123-456789abcdef'
        $root = Get-MwbPublicRunRoot -ResultsRoot $TestDrive -Id $id
        $root | Should Be (Join-Path ([IO.Path]::GetFullPath($TestDrive)) 'ui-0123456789ab\mwb-01234567-89ab-cdef-0123-456789abcdef')
        Test-Path -LiteralPath $root | Should Be $false
    }
}

Describe 'MWB run-in-place build selection' {
    BeforeEach {
        $templates = Join-Path $PSScriptRoot '..\v2\templates'
        $buildTemplate = Get-Content -LiteralPath (Join-Path $templates 'job-build-project.yml') -Raw
        $pipelineTemplate = Get-Content -LiteralPath (Join-Path $templates 'pipeline-ui-tests-full-build.yml') -Raw
        $installerGroups = @([regex]::Matches($buildTemplate,
            '(?ms)^  - \$\{\{ if eq\(parameters\.buildInstallers, true\) \}\}:\r?\n(?<steps>.*?)(?=^  - |\z)'))
    }

    It 'keeps installer production enabled by default for existing callers' {
        $buildTemplate | Should Match '(?m)^  - name: buildInstallers\r?\n    type: boolean\r?\n    default: true'
    }

    It 'keeps one Release build and its normal installer for MWB and mixed suites' {
        $pipelineTemplate | Should Match 'buildConfigurations: \[Release\]'
        $pipelineTemplate | Should Not Match 'buildConfigurations: \[Debug\]|buildInstallers: false'
        $pipelineTemplate | Should Match 'configuration: Release'
    }

    It 'guards each installer-only step group' -TestCases @(
        @{ Step = 'template: steps-build-installer-vnext.yml' }
        @{ Step = 'displayName: Stage Installers' }
        @{ Step = 'displayName: Calculate file hashes for all installers' }
    ) {
        param($Step)
        $matches = @($installerGroups | Where-Object { $_.Groups['steps'].Value.Contains($Step) })
        $matches.Count | Should Be 1
        ([regex]::Matches($buildTemplate, [regex]::Escape($Step))).Count | Should Be 1
    }

    It 'retains product/test output publication independently of installers' {
        $installerGroups.Count | Should Be 3
        foreach ($group in $installerGroups) {
            $group.Groups['steps'].Value | Should Not Match 'Move entire output directory into artifacts|Publish all outputs'
        }
        $buildTemplate | Should Match 'displayName: Move entire output directory into artifacts'
        $buildTemplate | Should Match 'displayName: Publish all outputs'
    }
}

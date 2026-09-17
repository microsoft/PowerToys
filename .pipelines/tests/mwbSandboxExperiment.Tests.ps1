# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

#Requires -Version 7.0
$scriptPath = Join-Path $PSScriptRoot '..\mwbSandboxExperiment.ps1'
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
    param([string] $Directory)

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
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot '..\Invoke-MwbProvisioning.ps1') -Destination $directory
        $initializer = Join-Path $directory 'Initialize-AutonomousHost.ps1'
        $parameters = @'
param([string] $ProductRoot, [string] $TestUser, [guid] $RunId,
    [int] $NetworkTimeoutSeconds, [string] $GuestArchivePath)
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

$line = 'x' * 4096
[Console]::Out.Write('a' * 1048576)
[Console]::Error.Write('b' * 1048576)
for ($index = 0; $index -lt 1024; $index++) {
    [Console]::Out.WriteLine($line)
    [Console]::Error.WriteLine($line)
}
[Console]::Out.WriteLine('stdout complete')
[Console]::Error.WriteLine('stderr complete')
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

    It 'surfaces the underlying error and stdout context without a provisioning marker' {
        Set-Content -LiteralPath $stderrPath -Value 'Exact underlying pre-marker error'
        Set-Content -LiteralPath $stdoutPath -Value 'Last completed initializer step'
        Write-MwbProvisioningDiagnostics $directory
        ($script:diagnosticLines -join "`n") | Should Match 'Exact underlying pre-marker error'
        ($script:diagnosticLines -join "`n") | Should Match 'Last completed initializer step'
        Assert-MockCalled Assert-MwbProtectedDirectory -Times 1 -Exactly -ParameterFilter { $Path -eq $directory }
        Assert-MockCalled Assert-MwbProtectedDirectory -Times 1 -Exactly -ParameterFilter { $Path -eq $logRoot }
        Assert-MockCalled Assert-MwbProtectedDirectory -Times 1 -Exactly -ParameterFilter { $Path -eq $stderrPath }
        Assert-MockCalled Assert-MwbProtectedDirectory -Times 1 -Exactly -ParameterFilter { $Path -eq $stdoutPath }
    }

    It 'explicitly reports unavailable logs when the launcher never started' {
        Remove-Item -LiteralPath $logRoot -Recurse
        Write-MwbProvisioningDiagnostics $directory
        ($script:diagnosticLines -join "`n") | Should Match 'logging launcher may not have started'
    }

    It 'distinguishes missing and empty streams' {
        Set-Content -LiteralPath $stderrPath -Value '' -NoNewline
        Write-MwbProvisioningDiagnostics $directory
        ($script:diagnosticLines -join "`n") | Should Match 'stderr.log: empty'
        ($script:diagnosticLines -join "`n") | Should Match 'stdout.log: not created'
    }

    It 'bounds reads and public output while retaining the end of a large log' {
        [IO.File]::WriteAllText($stderrPath, (('x' * 200 + "`n") * 20000) + "Final error`n")
        Write-MwbProvisioningDiagnostics $directory
        $script:diagnosticLines.Count | Should BeLessThan 42
        ($script:diagnosticLines -join "`n").Length | Should BeLessThan 20000
        ($script:diagnosticLines -join "`n") | Should Match 'Final error'
    }

    It 'does not print a partial oversized line whose sensitive prefix is outside the read window' {
        [IO.File]::WriteAllText($stderrPath, 'AccountPassword=' + ('x' * 20000) + 'DO_NOT_PUBLISH')
        Write-MwbProvisioningDiagnostics $directory
        ($script:diagnosticLines -join "`n") | Should Match 'oversized line omitted'
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
        ($script:diagnosticLines -join "`n") | Should Match 'sensitive diagnostic content omitted'
        ($script:diagnosticLines -join "`n") | Should Not Match 'DO_NOT_PUBLISH|Sensitive context:'
        Get-Content -LiteralPath $stderrPath -Raw | Should Match 'DO_NOT_PUBLISH'
    }

    It 'removes ANSI formatting and neutralizes Azure logging commands without altering the raw log' {
        $raw = "$([char]27)[31mFixture error$([char]27)[0m`n##vso[task.complete result=Succeeded;]not a command"
        Set-Content -LiteralPath $stderrPath -Value $raw
        Write-MwbProvisioningDiagnostics $directory
        ($script:diagnosticLines -join "`n") | Should Match 'Fixture error'
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
            ($script:diagnosticLines -join "`n") | Should Match 'cannot read the protected log'
        }
        finally {
            $lock.Dispose()
        }
    }
}

Describe 'MWB diagnostic task ownership contract' {
    It 'stages and hashes the trusted launcher alongside the fixed initializer and cleanup scripts' {
        $ast.Extent.Text | Should Match "foreach \(\`$name in @\('Initialize-AutonomousHost.ps1', 'Remove-AutonomousHost.ps1', 'Invoke-MwbProvisioning.ps1'\)\)"
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

    It 'prints diagnostics before each controlled provisioning status, early exit, or timeout failure' {
        $failures = @($ast.FindAll({
            param($node)
            $node -is [Management.Automation.Language.ThrowStatementAst] -and
                $node.Extent.Text -match 'BLOCKED_INFRASTRUCTURE: (privileged setup status|provisioning exited|provisioning did not publish)'
        }, $true))
        $failures.Count | Should Be 4
        foreach ($failure in $failures) {
            $preceding = $ast.Extent.Text.Substring(0, $failure.Extent.StartOffset)
            $preceding | Should Match 'Write-MwbProvisioningDiagnostics \$runRoot\s+$'
        }
    }
}

Describe 'MWB Sandbox artifact discovery' {
    BeforeEach {
        $artifact = Join-Path $TestDrive "$([guid]::NewGuid().ToString('N'))\build-x64-Debug"
        $product = Join-Path $artifact 'x64\Debug\x64\Debug'
        $tests = Join-Path $product 'tests\MouseWithoutBorders.UITests'
        New-Item -ItemType Directory -Path $tests -Force | Out-Null
        foreach ($name in @('PowerToys.exe', 'PowerToys.MouseWithoutBorders.exe', 'PowerToys.MouseWithoutBorders.dll')) {
            Set-Content -LiteralPath (Join-Path $product $name) -Value 'test fixture'
        }
        foreach ($name in @('MouseWithoutBorders.UITests.exe', 'MouseWithoutBorders.UITests.runtimeconfig.json')) {
            Set-Content -LiteralPath (Join-Path $tests $name) -Value 'test fixture'
        }
        New-Item -ItemType Directory -Path (Join-Path $tests 'Payload') -Force | Out-Null
        foreach ($name in @('Recover-Host.ps1', 'EndpointSupport.ps1', 'NativeSupport.cs')) {
            Set-Content -LiteralPath (Join-Path $tests "Payload\$name") -Value 'test fixture'
        }
    }

    It 'uses the product and test outputs from the same Debug artifact' {
        $payload = Get-MwbSandboxPayload $artifact
        $payload.ProductRoot | Should Be ([IO.Path]::GetFullPath($product))
        $payload.TestExecutable | Should Be ([IO.Path]::GetFullPath((Join-Path $tests 'MouseWithoutBorders.UITests.exe')))
    }

    It 'rejects Release artifacts rather than falling back to an installation' {
        $release = Join-Path $TestDrive 'build-x64-Release'
        New-Item -ItemType Directory -Path $release -Force | Out-Null
        { Get-MwbSandboxPayload $release } | Should Throw 'build-x64-Debug'
    }

    It 'rejects an ambiguous staged product' {
        $duplicate = Join-Path $artifact 'duplicate\x64\Debug'
        New-Item -ItemType Directory -Path $duplicate -Force | Out-Null
        foreach ($name in @('PowerToys.exe', 'PowerToys.MouseWithoutBorders.exe', 'PowerToys.MouseWithoutBorders.dll')) {
            Copy-Item -LiteralPath (Join-Path $product $name) -Destination $duplicate
        }
        { Get-MwbSandboxPayload $artifact } | Should Throw 'exactly one staged Debug product'
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
        @{ Name = 'EndpointSupport.ps1' }
        @{ Name = 'NativeSupport.cs' }
    ) {
        param($Name)
        Remove-Item -LiteralPath (Join-Path $tests "Payload\$Name")
        { Get-MwbSandboxPayload $artifact } | Should Throw 'recovery payload is incomplete'
    }

    It 'rejects relative or network paths before discovery' -TestCases @(
        @{ Path = 'build-x64-Debug' }
        @{ Path = '\\server\build-x64-Debug' }
        @{ Path = 'C:\payload" -Command something' }
    ) {
        param($Path)
        { Get-MwbSandboxPayload $Path } | Should Throw 'local absolute path'
    }
}

Describe 'MWB public recovery path' {
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

    It 'disables installers only in the explicit MWB Debug branch' {
        $pipelineTemplate | Should Match '(?m)^          \$\{\{ if eq\(parameters\.mwbSandboxExperiment, true\) \}\}:\r?\n            buildConfigurations: \[Debug\]\r?\n            buildInstallers: false\r?\n          \$\{\{ else \}\}:\r?\n            buildConfigurations: \[Release\]'
        ([regex]::Matches($pipelineTemplate, 'buildInstallers:')).Count | Should Be 1
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

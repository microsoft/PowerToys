# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

#Requires -Version 7.0
$scriptPath = Join-Path $PSScriptRoot '..\mwbSandboxExperiment.ps1'
$tokens = $null
$parseErrors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile($scriptPath, [ref]$tokens, [ref]$parseErrors)
if ($parseErrors) { throw 'The MWB CI helper has PowerShell parse errors.' }
# Load only pure path/discovery helpers. Tests never schedule tasks, provision a firewall, or run UI.
foreach ($name in @('Assert-MwbLocalPath', 'Get-MwbSandboxPayload', 'Get-MwbPublicRunRoot')) {
    $function = $ast.Find({
        param($node)
        $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq $name
    }, $true)
    . ([scriptblock]::Create($function.Extent.Text))
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

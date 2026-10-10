# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

$path = Join-Path $PSScriptRoot '..\..\src\modules\MouseWithoutBorders\Tests\SandboxExperiment\Invoke-AutonomousLocalVm.ps1'
$tokens = $null
$errors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile($path, [ref]$tokens, [ref]$errors)
if ($errors.Count) { throw 'The local MWB controller contains parse errors.' }
$replacement = @($ast.FindAll({
    param($node)
    $node -is [Management.Automation.Language.IfStatementAst] -and
        $node.Clauses[0].Item1.Extent.Text -eq '-not $ReuseProduct'
}, $true) | Where-Object { $_.Extent.Text -match '\$stage =' })
if ($replacement.Count -ne 1) { throw 'Expected exactly one protected product replacement.' }
$body = $replacement[0].Clauses[0].Item2.Extent.Text
$replaceProduct = [scriptblock]::Create($body.Substring(1, $body.Length - 2))

function tar.exe { throw 'Unmocked archive extraction' }

Describe 'MWB protected local product replacement' {
    BeforeEach {
        $product = Join-Path $TestDrive 'product'
        $Archive = Join-Path $TestDrive 'runtime.zip'
        $RunId = [Guid]::NewGuid().ToString()
        $null = New-Item -ItemType Directory -Path $product -Force
        Set-Content -LiteralPath (Join-Path $product 'old.dll') -Value 'original-cohort'
        Mock tar.exe {
            $destination = Join-Path $TestDrive "product-stage-$RunId"
            @(Get-ChildItem -LiteralPath $destination).Count | Should Be 0
            Set-Content -LiteralPath (Join-Path $destination 'new.dll') -Value 'replacement-cohort'
            $global:LASTEXITCODE = 0
        }
    }

    It 'extracts into an empty run-scoped directory before replacing the old cohort' {
        & $replaceProduct
        Test-Path -LiteralPath (Join-Path $product 'old.dll') | Should Be $false
        Get-Content -LiteralPath (Join-Path $product 'new.dll') | Should Be 'replacement-cohort'
        Test-Path -LiteralPath "$product-previous-$RunId" | Should Be $false
        Test-Path -LiteralPath "$product-stage-$RunId" | Should Be $false
    }

    It 'preserves the original cohort on a native extraction failure' {
        Mock tar.exe { $global:LASTEXITCODE = 1 }
        { & $replaceProduct } | Should Throw 'Product archive extraction failed'
        Get-Content -LiteralPath (Join-Path $product 'old.dll') | Should Be 'original-cohort'
        Test-Path -LiteralPath "$product-previous-$RunId" | Should Be $false
    }

    It 'refuses pre-existing replacement paths without adopting or deleting them' -TestCases @(
        @{ Suffix = 'stage' }
        @{ Suffix = 'previous' }
    ) {
        param($Suffix)
        $existing = "$product-$Suffix-$RunId"
        $null = New-Item -ItemType Directory -Path $existing
        Set-Content -LiteralPath (Join-Path $existing 'sentinel') -Value 'unowned'
        { & $replaceProduct } | Should Throw 'new run-scoped directories'
        Get-Content -LiteralPath (Join-Path $existing 'sentinel') | Should Be 'unowned'
        Get-Content -LiteralPath (Join-Path $product 'old.dll') | Should Be 'original-cohort'
        Assert-MockCalled tar.exe -Times 0 -Scope It
    }

    It 'does not let an older unrelated marker mask a failure before task registration' {
        $guard = $ast.Find({
            param($node)
            $node -is [Management.Automation.Language.IfStatementAst] -and
                $node.Clauses[0].Item1.Extent.Text -eq '$state.RunId -ne $RunId'
        }, $true)
        $cleanup = [scriptblock]::Create($guard.Extent.Text + "`nthrow 'Unowned marker was adopted'")
        $state = @{ RunId = 'older-unrelated-run' }
        $TaskRegistered = $false
        { & $cleanup } | Should Not Throw
        $TaskRegistered = $true
        { & $cleanup } | Should Throw 'Provisioning identity changed'
    }
}

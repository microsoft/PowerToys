# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.

$signingPolicies = @{
    Core = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\ESRPSigning_core.json') -Raw | ConvertFrom-Json
    CmdPal = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\ESRPSigning_cmdpal_msix_content.json') -Raw | ConvertFrom-Json
}

function Get-MatchingSigningBatch {
    param(
        [object] $SigningPolicy,
        [string] $PayloadPath
    )

    $normalizedPath = $PayloadPath.Replace('/', '\')
    $fileName = ($normalizedPath -split '\\')[-1]
    foreach ($batch in $SigningPolicy.SignBatches) {
        # The existing non-minimatch policies use recursive filename patterns or relative paths.
        $matchingPatterns = @($batch.MatchedPath | Where-Object {
            $normalizedPath -like $_ -or $fileName -like $_
        })
        if ($matchingPatterns.Count -gt 0) {
            $batch
        }
    }
}

Describe 'WinMD release signing policy' {
    # These are payload paths from the unpackaged product and the embedded CmdPal MSIX.
    # Include duplicate copies and metadata whose filename differs from its implementation DLL.
    $productPayloads = @(
        foreach ($payloadPath in @(
            'BackgroundActivator.winmd',
            'CalculatorEngineCommon.winmd',
            'Microsoft.CommandPalette.Extensions.winmd',
            'PowerToys.GPOWrapper.winmd',
            'PowerToys.Interop.winmd',
            'PowerToys.ZoomItSettingsInterop.winmd',
            'WinUI3Apps\Microsoft.CommandPalette.Extensions.winmd',
            'WinUI3Apps\PowerRenameUI.winmd',
            'WinUI3Apps\PowerToys.FileLocksmithLib.Interop.winmd',
            'WinUI3Apps\PowerToys.MeasureToolCore.winmd',
            'WinUI3Apps\PowerToys.PowerAccentKeyboardService.winmd',
            'WinUI3Apps\CmdPal\PowerToys.Interop.winmd'
        )) {
            @{ PolicyName = 'Core'; PayloadPath = $payloadPath }
        }
        foreach ($payloadPath in @(
            'CalculatorEngineCommon.winmd',
            'Microsoft.CommandPalette.Extensions.winmd',
            'PowerToys.Interop.winmd'
        )) {
            @{ PolicyName = 'CmdPal'; PayloadPath = $payloadPath }
        }
    )

    It 'signs <PolicyName> product metadata <PayloadPath> with the product identity' -TestCases $productPayloads {
        param($PolicyName, $PayloadPath)

        $batches = @(Get-MatchingSigningBatch -SigningPolicy $signingPolicies[$PolicyName] -PayloadPath $PayloadPath)

        $batches.Count | Should Be 1
        $signOperation = @($batches[0].SigningInfo.Operations | Where-Object OperationSetCode -eq 'SigntoolSign')
        $signOperation.Count | Should Be 1
        $signOperation[0].KeyCode | Should Be 'CP-230012'
    }

    It 'uses the external-component identity without replacing upstream signatures for <PayloadPath>' -TestCases @(
        @{ PolicyName = 'Core'; PayloadPath = 'WinUI3Apps\SkiaSharp.Views.WinUI.Native.winmd' },
        @{ PolicyName = 'Core'; PayloadPath = 'SkiaSharp.Views.WinUI.Native.winmd' },
        @{ PolicyName = 'CmdPal'; PayloadPath = 'Microsoft.Graphics.Canvas.winmd' }
    ) {
        param($PolicyName, $PayloadPath)

        $batches = @(Get-MatchingSigningBatch -SigningPolicy $signingPolicies[$PolicyName] -PayloadPath $PayloadPath)

        $batches.Count | Should Be 1
        $signOperation = @($batches[0].SigningInfo.Operations | Where-Object OperationSetCode -eq 'SigntoolSign')
        $signOperation.Count | Should Be 1
        $signOperation[0].KeyCode | Should Be 'CP-231522'
        $appendParameter = @($signOperation[0].Parameters | Where-Object parameterName -eq 'Append')
        $appendParameter.Count | Should Be 1
        $appendParameter[0].parameterValue | Should Be '/as'
        ($batches[0].SigningInfo.Operations | Where-Object OperationSetCode -eq 'SigntoolVerify').KeyCode | Should Be 'CP-231522'
    }

    It 'leaves upstream or unknown metadata <PayloadPath> outside the <PolicyName> signing selection' -TestCases @(
        @{ PolicyName = 'Core'; PayloadPath = 'Microsoft.UI.Xaml.winmd' },
        @{ PolicyName = 'Core'; PayloadPath = 'WinUI3Apps\Microsoft.Windows.AI.Foundation.winmd' },
        @{ PolicyName = 'Core'; PayloadPath = 'WinUI3Apps\Contoso.Dependency.winmd' },
        @{ PolicyName = 'Core'; PayloadPath = 'WinUI3Apps\PowerToys.Unknown.winmd' },
        @{ PolicyName = 'CmdPal'; PayloadPath = 'Microsoft.UI.Xaml.winmd' },
        @{ PolicyName = 'CmdPal'; PayloadPath = 'Microsoft.CmdPal.Common\Microsoft.Management.Deployment.winmd' },
        @{ PolicyName = 'CmdPal'; PayloadPath = 'Contoso.Dependency.winmd' }
    ) {
        param($PolicyName, $PayloadPath)

        $batches = @(Get-MatchingSigningBatch -SigningPolicy $signingPolicies[$PolicyName] -PayloadPath $PayloadPath)

        $batches.Count | Should Be 0
    }

    It 'preserves the existing additional signature for OpenAI.dll' {
        $batches = @(Get-MatchingSigningBatch -SigningPolicy $signingPolicies.Core -PayloadPath 'WinUI3Apps\OpenAI.dll')

        $batches.Count | Should Be 1
        $signOperation = $batches[0].SigningInfo.Operations | Where-Object OperationSetCode -eq 'SigntoolSign'
        $signOperation.KeyCode | Should Be 'CP-231522'
        ($signOperation.Parameters | Where-Object parameterName -eq 'Append').parameterValue | Should Be '/as'
    }
}

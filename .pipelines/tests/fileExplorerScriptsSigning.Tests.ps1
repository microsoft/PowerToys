# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.

Describe 'PowerScripts release signing' {
    It 'signs and verifies <PayloadPath> with the product identity' -TestCases @(
        @{ PayloadPath = 'WinUI3Apps\PowerToys.FileExplorerScripts.exe' },
        @{ PayloadPath = 'WinUI3Apps\PowerToys.FileExplorerScripts.dll' },
        @{ PayloadPath = 'WinUI3Apps\FileExplorerScripts.Core.dll' },
        @{ PayloadPath = 'WinUI3Apps\PowerScripts.Client.dll' },
        @{ PayloadPath = 'WinUI3Apps\PowerToys.FileExplorerScripts.ShellExtension.dll' },
        @{ PayloadPath = 'WinUI3Apps\FileExplorerScriptsPackage.msix' },
        @{ PayloadPath = 'PowerToys.PowerScriptsModuleInterface.dll' },
        @{ PayloadPath = 'PowerScripts\PowerScripts.Host.exe' },
        @{ PayloadPath = 'PowerScripts\PowerScripts.Host.dll' },
        @{ PayloadPath = 'PowerScripts\PowerScripts.Core.dll' },
        @{ PayloadPath = 'PowerScripts\PowerScripts.Client.dll' },
        @{ PayloadPath = 'WinUI3Apps\PowerScripts.Core.dll' },
        @{ PayloadPath = 'WinUI3Apps\PowerToys.PowerScriptsEditor.exe' },
        @{ PayloadPath = 'WinUI3Apps\PowerToys.PowerScriptsEditor.dll' },
        @{ PayloadPath = 'WinUI3Apps\PowerToys.PowerScripts.UI.dll' },
        @{ PayloadPath = 'WinUI3Apps\PowerToys.AdvancedPaste.LocalCLI.exe' },
        @{ PayloadPath = 'WinUI3Apps\PowerToys.AdvancedPaste.LocalCLI.dll' }
    ) {
        param($PayloadPath)

        $policy = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\ESRPSigning_core.json') -Raw | ConvertFrom-Json
        $batches = @($policy.SignBatches | Where-Object {
            @($_.MatchedPath | Where-Object { $PayloadPath -like $_ }).Count -gt 0
        })
        $batches.Count | Should Be 1
        ($batches[0].SigningInfo.Operations | Where-Object OperationSetCode -eq 'SigntoolSign').KeyCode | Should Be 'CP-230012'
        ($batches[0].SigningInfo.Operations | Where-Object OperationSetCode -eq 'SigntoolVerify').KeyCode | Should Be 'CP-230012'
    }

    It 'verifies upstream signatures without changing pinned runtime <PayloadPath>' -TestCases @(
        @{ PayloadPath = 'PowerScripts\wxc-exec.exe' },
        @{ PayloadPath = 'PowerScripts\wxc-wslc-daemon.exe' },
        @{ PayloadPath = 'PowerScripts\plm.exe' },
        @{ PayloadPath = 'PowerScripts\wslcsdk.dll' }
    ) {
        param($PayloadPath)

        $policy = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\ESRPSigning_core.json') -Raw | ConvertFrom-Json
        $batches = @($policy.SignBatches | Where-Object {
            @($_.MatchedPath | Where-Object { $PayloadPath -like $_ }).Count -gt 0
        })
        $batches.Count | Should Be 1
        $operations = @($batches[0].SigningInfo.Operations)
        $operations.Count | Should Be 1
        $operations[0].OperationSetCode | Should Be 'SigntoolVerify'
        $operations[0].KeyCode | Should Be 'CP-230012'
    }
}

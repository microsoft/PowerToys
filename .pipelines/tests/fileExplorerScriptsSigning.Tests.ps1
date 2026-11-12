# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.

Describe 'File Explorer PowerScripts release signing' {
    It 'signs and verifies <PayloadPath> with the product identity' -TestCases @(
        @{ PayloadPath = 'WinUI3Apps\PowerToys.FileExplorerScripts.exe' },
        @{ PayloadPath = 'WinUI3Apps\PowerToys.FileExplorerScripts.dll' },
        @{ PayloadPath = 'WinUI3Apps\FileExplorerScripts.Core.dll' },
        @{ PayloadPath = 'WinUI3Apps\PowerScripts.Client.dll' },
        @{ PayloadPath = 'WinUI3Apps\PowerToys.FileExplorerScripts.ShellExtension.dll' },
        @{ PayloadPath = 'WinUI3Apps\FileExplorerScriptsPackage.msix' }
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
}

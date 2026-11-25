# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.

Describe 'Core signing policy preparation' {
    It 'excludes only loose CmdPal language DLLs from the recursive resource rule' {
        $buildOutput = Join-Path $TestDrive 'Release'
        $sourcePolicyFile = Join-Path $PSScriptRoot '..\ESRPSigning_core.json'
        $outputPolicyFile = Join-Path $TestDrive 'ESRPSigning_core.json'
        $excludedPaths = @(
            'WinUI3Apps\CmdPal\zh-CN\Microsoft.CmdPal.Common.resources.dll'
            'WinUI3Apps\CmdPal\zh-TW\Microsoft.CmdPal.Common.resources.dll'
        )
        $includedPaths = @(
            'fr-FR\PowerToys.resources.dll'
            'WinUI3Apps\fr-FR\Microsoft.CmdPal.Ext.PowerToys.resources.dll'
            'WinUI3Apps\CmdPal\bin\Other.resources.dll'
            'WinUI3Apps\CmdPal\AppPackages\Package_Test\fr-FR\Microsoft.CmdPal.Common.resources.dll'
        )

        foreach ($relativePath in ($excludedPaths + $includedPaths)) {
            $path = Join-Path $buildOutput $relativePath
            New-Item -ItemType Directory -Path (Split-Path -Parent $path) -Force | Out-Null
            [IO.File]::WriteAllText($path, '')
        }

        & (Join-Path $PSScriptRoot '..\prepareCoreSigningPolicy.ps1') -BuildOutputDirectory $buildOutput -PolicyFile $sourcePolicyFile -OutputPolicyFile $outputPolicyFile

        $source = Get-Content -LiteralPath $sourcePolicyFile -Raw | ConvertFrom-Json -AsHashtable
        $generated = Get-Content -LiteralPath $outputPolicyFile -Raw | ConvertFrom-Json -AsHashtable
        $expected = @($source.SignBatches[0].MatchedPath | Where-Object { $_ -ne '*.resources.dll' }) + $includedPaths
        $actual = $generated.SignBatches[0].MatchedPath

        $actual.Count | Should Be $expected.Count
        @(Compare-Object $expected $actual).Count | Should Be 0
        $generated.UseMinimatch | Should Be $source.UseMinimatch
        ($generated.SignBatches[0].SigningInfo | ConvertTo-Json -Depth 20 -Compress) |
            Should Be ($source.SignBatches[0].SigningInfo | ConvertTo-Json -Depth 20 -Compress)
        ($generated.SignBatches[1] | ConvertTo-Json -Depth 20 -Compress) |
            Should Be ($source.SignBatches[1] | ConvertTo-Json -Depth 20 -Compress)

        foreach ($relativePath in $excludedPaths) {
            Test-Path -LiteralPath (Join-Path $buildOutput $relativePath) | Should Be $true
        }
    }
}

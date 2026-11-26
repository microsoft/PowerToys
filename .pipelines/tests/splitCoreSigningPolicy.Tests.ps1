# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.

Describe 'Core ESRP signing policy split' {
    It 'moves the resource rule to its own policy and retains all signing operations' {
        $sourcePolicyFile = Join-Path $PSScriptRoot '..\ESRPSigning_core.json'
        & (Join-Path $PSScriptRoot '..\splitCoreSigningPolicy.ps1') -PolicyFile $sourcePolicyFile -OutputDirectory $TestDrive

        $original = Get-Content -LiteralPath $sourcePolicyFile -Raw | ConvertFrom-Json
        $resources = Get-Content -LiteralPath (Join-Path $TestDrive 'ESRPSigning_core.resources.json') -Raw | ConvertFrom-Json
        $binaries = Get-Content -LiteralPath (Join-Path $TestDrive 'ESRPSigning_core.binaries.json') -Raw | ConvertFrom-Json

        foreach ($policy in @($resources, $binaries)) {
            $policy.Version | Should Be $original.Version
            $policy.UseMinimatch | Should Be $original.UseMinimatch
        }

        $resources.SignBatches.Count | Should Be 1
        $resources.SignBatches[0].MatchedPath.Count | Should Be 1
        $resources.SignBatches[0].MatchedPath[0] | Should Be '*.resources.dll'
        ($resources.SignBatches[0].SigningInfo | ConvertTo-Json -Depth 20 -Compress) |
            Should Be ($original.SignBatches[0].SigningInfo | ConvertTo-Json -Depth 20 -Compress)

        $binaries.SignBatches.Count | Should Be $original.SignBatches.Count
        for ($i = 0; $i -lt $original.SignBatches.Count; $i++) {
            $expected = @($original.SignBatches[$i].MatchedPath | Where-Object { $_ -ne '*.resources.dll' })
            ($binaries.SignBatches[$i].MatchedPath -join '|') | Should Be ($expected -join '|')
            ($binaries.SignBatches[$i].SigningInfo | ConvertTo-Json -Depth 20 -Compress) |
                Should Be ($original.SignBatches[$i].SigningInfo | ConvertTo-Json -Depth 20 -Compress)
        }
    }
}

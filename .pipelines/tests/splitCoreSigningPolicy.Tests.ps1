# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.

Describe 'Core ESRP signing policy split' {
    It 'splits Core signing for <Platform> and retains all signing operations' -TestCases @(
        @{ Platform = 'x64'; IsolateFileLocksmith = $true }
        @{ Platform = 'arm64'; IsolateFileLocksmith = $false }
    ) {
        param($Platform, $IsolateFileLocksmith)

        $sourcePolicyFile = Join-Path $PSScriptRoot '..\ESRPSigning_core.json'
        $fileLocksmithPaths = @(
            'WinUI3Apps\PowerToys.FileLocksmithExt.dll'
            'WinUI3Apps\PowerToys.FileLocksmithLib.Interop.dll'
            'WinUI3Apps\PowerToys.FileLocksmithUI.exe'
            'WinUI3Apps\PowerToys.FileLocksmithUI.dll'
        )
        $buildOutput = Join-Path $TestDrive $Platform
        New-Item -ItemType Directory -Path (Join-Path $buildOutput 'WinUI3Apps') -Force | Out-Null
        foreach ($path in $fileLocksmithPaths) {
            Set-Content -LiteralPath (Join-Path $buildOutput $path) -Value 'test payload'
        }
        $outputPolicy = Join-Path $TestDrive "$Platform-policies"
        New-Item -ItemType Directory -Path $outputPolicy | Out-Null
        & (Join-Path $PSScriptRoot '..\splitCoreSigningPolicy.ps1') -PolicyFile $sourcePolicyFile -OutputDirectory $outputPolicy `
            -Platform $Platform -BuildOutputDirectory $buildOutput

        $original = Get-Content -LiteralPath $sourcePolicyFile -Raw | ConvertFrom-Json
        $resources = Get-Content -LiteralPath (Join-Path $outputPolicy 'ESRPSigning_core.resources.json') -Raw | ConvertFrom-Json
        $binaries = Get-Content -LiteralPath (Join-Path $outputPolicy 'ESRPSigning_core.binaries.json') -Raw | ConvertFrom-Json

        foreach ($policy in @($resources, $binaries)) {
            $policy.Version | Should Be $original.Version
            $policy.UseMinimatch | Should Be $original.UseMinimatch
        }

        $resources.SignBatches.Count | Should Be 1
        $resources.SignBatches[0].MatchedPath.Count | Should Be 1
        $resources.SignBatches[0].MatchedPath[0] | Should Be '*.resources.dll'
        ($resources.SignBatches[0].SigningInfo | ConvertTo-Json -Depth 20 -Compress) |
            Should Be ($original.SignBatches[0].SigningInfo | ConvertTo-Json -Depth 20 -Compress)

        $removedPatterns = @('*.resources.dll')
        $fileLocksmithPolicyFile = Join-Path $outputPolicy 'ESRPSigning_core.filelocksmith.json'
        (Test-Path -LiteralPath $fileLocksmithPolicyFile -PathType Leaf) | Should Be $IsolateFileLocksmith
        if ($IsolateFileLocksmith) {
            $fileLocksmith = Get-Content -LiteralPath $fileLocksmithPolicyFile -Raw | ConvertFrom-Json
            $originalBatch = $original.SignBatches | Where-Object { $_.MatchedPath -contains $fileLocksmithPaths[0] }
            $fileLocksmith.Version | Should Be $original.Version
            $fileLocksmith.UseMinimatch | Should Be $original.UseMinimatch
            $fileLocksmith.SignBatches.Count | Should Be 1
            ($fileLocksmith.SignBatches[0].MatchedPath -join '|') | Should Be ($fileLocksmithPaths -join '|')
            ($fileLocksmith.SignBatches[0].SigningInfo | ConvertTo-Json -Depth 20 -Compress) |
                Should Be ($originalBatch.SigningInfo | ConvertTo-Json -Depth 20 -Compress)
            $removedPatterns += $fileLocksmithPaths
        }

        $binaries.SignBatches.Count | Should Be $original.SignBatches.Count
        for ($i = 0; $i -lt $original.SignBatches.Count; $i++) {
            $expected = @($original.SignBatches[$i].MatchedPath | Where-Object { $_ -notin $removedPatterns })
            ($binaries.SignBatches[$i].MatchedPath -join '|') | Should Be ($expected -join '|')
            ($binaries.SignBatches[$i].SigningInfo | ConvertTo-Json -Depth 20 -Compress) |
                Should Be ($original.SignBatches[$i].SigningInfo | ConvertTo-Json -Depth 20 -Compress)
        }
    }

    It 'fails policy preparation when the x64 File Locksmith batch is incomplete' {
        $sourcePolicyFile = Join-Path $PSScriptRoot '..\ESRPSigning_core.json'
        $buildOutput = Join-Path $TestDrive 'incomplete'
        New-Item -ItemType Directory -Path (Join-Path $buildOutput 'WinUI3Apps') -Force | Out-Null
        Set-Content -LiteralPath (Join-Path $buildOutput 'WinUI3Apps\PowerToys.FileLocksmithExt.dll') -Value 'test payload'
        {
            & (Join-Path $PSScriptRoot '..\splitCoreSigningPolicy.ps1') -PolicyFile $sourcePolicyFile -OutputDirectory $TestDrive `
                -Platform 'x64' -BuildOutputDirectory $buildOutput
        } | Should Throw 'PowerToys.FileLocksmithLib.Interop.dll'
    }
}

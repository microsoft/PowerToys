$splitPolicy = Join-Path $PSScriptRoot '..\splitCoreSigningPolicy.ps1'
$originalPolicy = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\ESRPSigning_core.json') -Raw | ConvertFrom-Json

Describe 'Core ESRP signing requests' {
    It 'keeps the existing operations and splits resource files once by relative path' {
        $root = Join-Path $TestDrive 'release'
        $output = Join-Path $TestDrive 'policies'
        $paths = @(
            'Example.resources.dll'
            'de-DE/Example.resources.dll'
            'fr-FR/Example.resources.dll'
            'WinUI3Apps/de-DE/Example.resources.dll'
            'WinUI3Apps/fr-FR/Example.resources.dll'
        )
        foreach ($path in ($paths + 'NotAResource.dll')) {
            $file = Join-Path $root $path
            New-Item -ItemType Directory -Force -Path (Split-Path -Parent $file) | Out-Null
            New-Item -ItemType File -Path $file | Out-Null
        }

        & $splitPolicy -BuildRoot $root -OutputDirectory $output

        $binaries = Get-Content -LiteralPath (Join-Path $output 'core-binaries.json') -Raw | ConvertFrom-Json
        $first = Get-Content -LiteralPath (Join-Path $output 'core-resources-1.json') -Raw | ConvertFrom-Json
        $second = Get-Content -LiteralPath (Join-Path $output 'core-resources-2.json') -Raw | ConvertFrom-Json
        $resourceBatch = $originalPolicy.SignBatches | Where-Object { $_.MatchedPath -contains '*.resources.dll' }
        $expectedSigning = $resourceBatch.SigningInfo | ConvertTo-Json -Depth 20 -Compress

        $binaries.UseMinimatch | Should Be $originalPolicy.UseMinimatch
        for ($i = 0; $i -lt $originalPolicy.SignBatches.Count; $i++) {
            $expected = $originalPolicy.SignBatches[$i]
            ($binaries.SignBatches[$i].MatchedPath -join ';') |
                Should Be (($expected.MatchedPath | Where-Object { $_ -ne '*.resources.dll' }) -join ';')
            ($binaries.SignBatches[$i].SigningInfo | ConvertTo-Json -Depth 20 -Compress) |
                Should Be ($expected.SigningInfo | ConvertTo-Json -Depth 20 -Compress)
        }
        $first.SignBatches[0].MatchedPath.Count | Should Be 3
        $second.SignBatches[0].MatchedPath.Count | Should Be 2
        foreach ($policy in $first, $second) {
            $policy.UseMinimatch | Should Be $true
            ($policy.SignBatches[0].SigningInfo | ConvertTo-Json -Depth 20 -Compress) | Should Be $expectedSigning
        }

        $actual = @($first.SignBatches[0].MatchedPath) + @($second.SignBatches[0].MatchedPath)
        (($actual | Sort-Object) -join ';') | Should Be (($paths | Sort-Object) -join ';')
    }

    It 'fails instead of creating empty resource signing requests' {
        $root = Join-Path $TestDrive 'empty-release'
        $output = Join-Path $TestDrive 'empty-policies'
        New-Item -ItemType Directory -Path $root | Out-Null

        $failure = $null
        try {
            & $splitPolicy -BuildRoot $root -OutputDirectory $output
        }
        catch {
            $failure = $_.Exception.Message
        }
        $failure | Should Match 'Expected at least two resource DLLs'
        (Test-Path -LiteralPath $output) | Should Be $false
    }
}

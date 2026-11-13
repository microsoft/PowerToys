# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

. "$PSScriptRoot\..\..\src\modules\MouseWithoutBorders\MouseWithoutBorders.UITests\Scripts\MwbRunnerModules.ps1"

Describe 'Release Runner native module-load closure' {
    BeforeEach { $runnerFixturePath = Join-Path $TestDrive ([guid]::NewGuid().ToString('N') + '.cpp') }

    It 'derives the required libraries and provenance from the actual Runner source' {
        $source = Join-Path $PSScriptRoot '..\..\src\runner\main.cpp'
        $result = Get-MwbRunnerModules $source
        $result.Modules.Count | Should BeGreaterThan 1
        ($result.Modules -contains 'PowerToys.MouseWithoutBordersModuleInterface.dll') | Should Be $true
        ($result.Modules -contains 'PowerToys.FancyZonesModuleInterface.dll') | Should Be $true
        ($result.Modules -contains 'WinUI3Apps\PowerToys.ImageResizerExt.dll') | Should Be $true
        $result.SourceSha256 | Should Be (Get-FileHash -LiteralPath $source).Hash
    }

    It 'normalizes source path separators without dropping disabled modules' {
        Set-Content -LiteralPath $runnerFixturePath -Value 'constexpr ModuleRegistration knownModules[] = { { L"First", L"PowerToys.First.dll", DefaultEnabled::Yes, powertoys_gpo::getConfiguredFirstEnabledValue }, { L"Second", L"WinUI3Apps\\PowerToys.Second.dll", DefaultEnabled::No }, { L"Third", L"WinUI3Apps/PowerToys.Third.dll", DefaultEnabled::BeforeWindows11 }, };'
        (Get-MwbRunnerModules $runnerFixturePath).Modules | Should Be @('PowerToys.First.dll', 'WinUI3Apps\PowerToys.Second.dll', 'WinUI3Apps\PowerToys.Third.dll')
    }

    It 'rejects missing, duplicate, conditional, malformed or escaped module declarations' -TestCases @(
        @{ Declaration = 'no declaration' }
        @{ Declaration = 'constexpr ModuleRegistration knownModules[] = {};' }
        @{ Declaration = 'constexpr ModuleRegistration knownModules[] = { { L"A", L"PowerToys.A.dll", DefaultEnabled::Yes }, { L"B", L"PowerToys.A.dll", DefaultEnabled::Yes } };' }
        @{ Declaration = 'constexpr ModuleRegistration knownModules[] = { { L"A", L"..\\PowerToys.A.dll", DefaultEnabled::Yes } };' }
        @{ Declaration = 'constexpr ModuleRegistration knownModules[] = { { L"A", L"C:\\PowerToys.A.dll", DefaultEnabled::Yes } };' }
        @{ Declaration = 'constexpr ModuleRegistration knownModules[] = { { L"A", L"PowerToys.A.exe", DefaultEnabled::Yes } };' }
        @{ Declaration = 'constexpr ModuleRegistration knownModules[] = { { L"A", L"PowerToys.A.dll", DefaultEnabled::Yes }, unknownFunction() };' }
        @{ Declaration = 'constexpr ModuleRegistration knownModules[] = { { L"A", L"PowerToys.A.dll", DefaultEnabled::Yes } }; constexpr ModuleRegistration knownModules[] = { { L"B", L"PowerToys.B.dll", DefaultEnabled::Yes } };' }
    ) {
        param($Declaration)
        Set-Content -LiteralPath $runnerFixturePath -Value $Declaration
        { Get-MwbRunnerModules $runnerFixturePath } | Should Throw 'Runner module-load list'
    }

    It 'includes the whole Release module set before traversing its native import graph' {
        $source = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\..\src\modules\MouseWithoutBorders\MouseWithoutBorders.UITests\Scripts\New-MwbRuntimeArchive.ps1') -Raw
        $source | Should Match "if \(\`$Configuration -eq 'Release'\)"
        $source | Should Match 'Get-MwbRunnerModules \$RunnerSourcePath'
        $source | Should Match 'foreach \(\$relative in \$runnerModules.Modules\) \{ Add-RuntimeFile'
        ($source.IndexOf('Get-MwbRunnerModules') -lt $source.IndexOf('[MwbPeImports]::Read($path)')) | Should Be $true
        $source | Should Match '\$manifest.RunnerModules = \$runnerModules'
    }
}

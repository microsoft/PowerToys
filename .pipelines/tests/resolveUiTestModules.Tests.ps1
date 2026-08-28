# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.

$scriptPath = Join-Path $PSScriptRoot '..\resolveUiTestModules.ps1'

function New-UITestProject {
    param([Parameter(Mandatory)][string] $RelativePath)

    $path = Join-Path $TestDrive $RelativePath
    New-Item -ItemType Directory -Path (Split-Path $path) -Force | Out-Null
    '<Project Sdk="Microsoft.NET.Sdk" />' | Set-Content -LiteralPath $path
}

Describe 'resolveUiTestModules' {
    It 'selects the UI tests for a touched module' {
        New-UITestProject 'src\modules\FileLocksmith\Tests\FileLocksmith.UITests\FileLocksmith.UITests.csproj'

        $result = & $scriptPath -RepoRoot $TestDrive -ChangedFile @(
            'PowerToys.slnx',
            'src/modules/FileLocksmith/FileLocksmithUI/MainWindow.xaml.cs',
            'src\modules\FileLocksmith\FileLocksmithLib\FileLocksmith.cpp'
        )

        $result.TouchedModules | Should Be @('FileLocksmith')
        $result.UiTestModules | Should Be @('FileLocksmith.UITests')
    }

    It 'prefers all Next projects over legacy projects in the same module' {
        New-UITestProject 'src\modules\fancyzones\FancyZones.UITests\FancyZones.UITests.csproj'
        New-UITestProject 'src\modules\fancyzones\FancyZones.UITests.Next\FancyZones.UITests.Next.csproj'
        New-UITestProject 'src\modules\fancyzones\FancyZonesEditor.UITests\FancyZonesEditor.UITests.csproj'
        New-UITestProject 'src\modules\fancyzones\FancyZonesEditor.UITests.Next\FancyZonesEditor.UITests.Next.csproj'

        $result = & $scriptPath -RepoRoot $TestDrive -ChangedFile 'src/modules/fancyzones/dll/FancyZones.cpp'

        $result.UiTestModules | Should Be @(
            'FancyZones.UITests.Next',
            'FancyZonesEditor.UITests.Next'
        )
    }

    It 'ignores modules without UI tests and non-module changes' {
        New-UITestProject 'src\modules\Example\Tests\Example.UnitTests\Example.UnitTests.csproj'

        $result = & $scriptPath -RepoRoot $TestDrive -ChangedFile @(
            'doc/devdocs/modules/example.md',
            'src/modules/Example/Example.cpp'
        )

        $result.TouchedModules | Should Be @('Example')
        @($result.UiTestModules).Count | Should Be 0
    }
}

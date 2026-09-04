# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.

$scriptPath = Join-Path $PSScriptRoot '..\resolveUiTestModules.ps1'

function New-UITestProject {
    param(
        [Parameter(Mandatory)]
        [string] $RelativePath,

        [ValidateSet('Next', 'Legacy', 'None')]
        [string] $Framework = 'Next'
    )

    $path = Join-Path $TestDrive $RelativePath
    New-Item -ItemType Directory -Path (Split-Path $path) -Force | Out-Null
    $projectReference = switch ($Framework) {
        'Next' { '<ProjectReference Include="..\UITestAutomation.Next\UITestAutomation.Next.csproj" />' }
        'Legacy' { '<ProjectReference Include="..\UITestAutomation\UITestAutomation.csproj" />' }
        default { '' }
    }
    @"
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    $projectReference
  </ItemGroup>
</Project>
"@ | Set-Content -LiteralPath $path
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

    It 'selects projects using the Next framework and ignores legacy siblings' {
        New-UITestProject 'src\modules\fancyzones\FancyZones.UITests\FancyZones.UITests.csproj' -Framework Legacy
        New-UITestProject 'src\modules\fancyzones\FancyZones.UITests.Next\FancyZones.UITests.Next.csproj'
        New-UITestProject 'src\modules\fancyzones\FancyZonesEditor.UITests\FancyZonesEditor.UITests.csproj' -Framework Legacy
        New-UITestProject 'src\modules\fancyzones\FancyZonesEditor.UITests.Next\FancyZonesEditor.UITests.Next.csproj'

        $result = & $scriptPath -RepoRoot $TestDrive -ChangedFile 'src/modules/fancyzones/dll/FancyZones.cpp'

        $result.UiTestModules | Should Be @(
            'FancyZones.UITests.Next',
            'FancyZonesEditor.UITests.Next'
        )
    }

    It 'ignores modules with only legacy UI tests and non-module changes' {
        New-UITestProject 'src\modules\Example\Tests\Example.UITests\Example.UITests.csproj' -Framework Legacy
        New-UITestProject 'src\modules\Example\Tests\Example.UnitTests\Example.UnitTests.csproj' -Framework None

        $result = & $scriptPath -RepoRoot $TestDrive -ChangedFile @(
            'doc/devdocs/modules/example.md',
            'src/modules/Example/Example.cpp'
        )

        $result.TouchedModules | Should Be @('Example')
        $result.SharedHarnessChanged | Should Be $false
        @($result.UiTestModules).Count | Should Be 0
    }

    It 'selects every Next UI-test project when the shared harness changes' {
        $repoRoot = Join-Path $TestDrive 'shared-harness-repo'
        New-UITestProject 'shared-harness-repo\src\modules\Alpha\Tests\Alpha.UITests.Next\Alpha.UITests.Next.csproj'
        New-UITestProject 'shared-harness-repo\src\modules\Beta\Tests\Beta.UITests.Next\Beta.UITests.Next.csproj'
        New-UITestProject 'shared-harness-repo\src\modules\Beta\Tests\Beta.UITests\Beta.UITests.csproj' -Framework Legacy
        New-UITestProject 'shared-harness-repo\src\settings-ui\Settings.UITests\Settings.UITests.csproj'

        $result = & $scriptPath -RepoRoot $repoRoot -ChangedFile @(
            'src/common/UITestAutomation.Next/VisualAssert.cs',
            'doc/devdocs/modules/example.md'
        )

        $result.SharedHarnessChanged | Should Be $true
        @($result.TouchedModules).Count | Should Be 0
        $result.UiTestModules | Should Be @(
            'Alpha.UITests.Next',
            'Beta.UITests.Next',
            'Settings.UITests'
        )
    }
}

# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.

. (Join-Path $PSScriptRoot '..\UiTestArtifacts.Common.ps1')

function New-ArtifactTestProject {
    param([string] $Name, [string] $Folder = $Name, [string] $Package = '')

    $directory = Join-Path $script:artifactCaseRoot "src\$Name"
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
    $fileWrites = if ($Package) { '<FileWrites Include="$(TargetDir)' + $Package + ';@(_FixtureFiles)" />' } else { '' }
    @"
<Project>
  <PropertyGroup>
    <AssemblyName>$Name</AssemblyName>
    <OutputPath>`$(RepoRoot)`$(Platform)\`$(Configuration)\tests\$Folder\</OutputPath>
  </PropertyGroup>
  <Target Name="StageFixture"><ItemGroup>$fileWrites</ItemGroup></Target>
</Project>
"@ | Set-Content -LiteralPath (Join-Path $directory "$Name.csproj")
}

function New-ArtifactTestPayload {
    param([object] $Suite, [string] $Framework = 'net10.0-windows', [string] $Omit = '')

    $directory = Join-Path $script:artifactCaseRoot "artifact\x64\Release\tests\$($Suite.Name)\$Framework"
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
    foreach ($name in $Suite.Files | Where-Object { $_ -ne $Omit }) {
        Set-Content -LiteralPath (Join-Path $directory $name) -Value "fixture $name"
    }
}

Describe 'UI test artifact selection and payload' {
    BeforeEach {
        $script:artifactCaseRoot = Join-Path $TestDrive ([guid]::NewGuid().ToString('N'))
        New-ArtifactTestProject -Name 'Alpha.UITests' -Package 'Alpha.TestApp.msix'
        New-ArtifactTestProject -Name 'Beta.UITests.Next'
        $selection = @(Get-UiTestArtifactSelection -RepoRoot $script:artifactCaseRoot -Modules @('Alpha.UITests', 'Beta.UITests.Next') -Platform x64)
        $root = Join-Path $script:artifactCaseRoot 'artifact'
    }

    It 'rejects the literal unresolved variable from the failed download' {
        { Assert-UiTestArtifactPattern -Pattern '$(UiTestArtifactPattern)' -Modules @('Alpha.UITests', 'Beta.UITests.Next') } |
            Should Throw 'patterns do not match'
    }

    It 'rejects a truncated selection even when one suite would download successfully' {
        { Assert-UiTestArtifactPattern -Pattern '**/tests/Alpha.UITests/**' -Modules @('Alpha.UITests', 'Beta.UITests.Next') } |
            Should Throw 'patterns do not match'
    }

    It 'accepts the exact job-resolved selection without a logging command' {
        { Assert-UiTestArtifactPattern -Pattern "**/tests/Alpha.UITests/**`n**/tests/Beta.UITests.Next/**" `
            -Modules @('Alpha.UITests', 'Beta.UITests.Next') } | Should Not Throw
        $yaml = Get-Content (Join-Path $PSScriptRoot '..\v2\templates\job-test-project.yml') -Raw
        $yaml | Should Not Match 'task\.setvariable variable=UiTestArtifactPattern'
        $yaml | Should Match 'UiTestArtifactPattern: \$\{\{ format'
        $yaml | Should Match 'UI_TEST_ARTIFACT_PATTERN: \$\(UiTestArtifactPattern\)'
    }

    It 'retains the all-UI-test include-only pattern for an empty explicit selection' {
        { Assert-UiTestArtifactPattern -Pattern '**/tests/*UITest*/**' } | Should Not Throw
        @(Get-UiTestArtifactSelection -RepoRoot $script:artifactCaseRoot -Platform x64).Count | Should Be 2
    }

    It 'rejects wildcard project stems rather than broadening the download' {
        { Assert-UiTestArtifactPattern -Pattern '**/tests/*/**' -Modules '*' } | Should Throw 'Invalid UI-test project stem'
    }

    It 'passes queue-time module lists only as data and rejects non-stem characters' {
        $yaml = Get-Content (Join-Path $PSScriptRoot '..\v2\templates\job-test-project.yml') -Raw
        $bindings = @($yaml -split '\r?\n' | Where-Object { $_ -match 'join\('';'', parameters\.uiTest(?:Execution)?Modules\)' })
        $bindings.Count | Should BeGreaterThan 0
        foreach ($binding in $bindings) {
            $binding.Trim() | Should Match '^(SELECTED_UI_TEST_MODULES|UI_TEST_EXECUTION_MODULES): \$\{\{'
        }
        { Assert-UiTestProjectStems @('Alpha.UITests', "Beta's.UITests") } | Should Throw 'Invalid UI-test project stem'
    }

    It 'rejects an unknown selected project' {
        { Get-UiTestArtifactSelection -RepoRoot $script:artifactCaseRoot -Modules 'Missing.UITests' -Platform x64 } |
            Should Throw "Expected one UI-test project"
    }

    It 'rejects a noncanonical output folder before a download can silently miss it' {
        New-ArtifactTestProject -Name 'Alpha.UITests' -Folder 'Other.UITests'
        { Get-UiTestArtifactSelection -RepoRoot $script:artifactCaseRoot -Modules 'Alpha.UITests' -Platform x64 } |
            Should Throw 'project stem'
    }

    It 'verifies every selected suite and its declared fixture, recording file hashes' {
        ($selection[0].Files -contains 'Alpha.TestApp.msix') | Should Be $true
        foreach ($suite in $selection) { New-ArtifactTestPayload $suite }
        $receipt = @(Assert-UiTestArtifactPayload -ArtifactRoot $root -Selection $selection)
        $receipt.Count | Should Be 2
        @($receipt[0].Files).Count | Should Be 5
        $receipt[0].Files[0].Sha256 | Should Match '^[A-F0-9]{64}$'
    }

    It 'fails if a selected suite is absent instead of running only the downloaded subset' {
        New-ArtifactTestPayload $selection[0]
        { Assert-UiTestArtifactPayload -ArtifactRoot $root -Selection $selection } |
            Should Throw 'Beta.UITests.Next'
    }

    It 'rejects a missing required file <File>' -TestCases @(
        @{ File = 'Alpha.UITests.exe' }
        @{ File = 'Alpha.UITests.dll' }
        @{ File = 'Alpha.UITests.deps.json' }
        @{ File = 'Alpha.TestApp.msix' }
    ) {
        param($File)
        New-ArtifactTestPayload $selection[0] -Omit $File
        { Assert-UiTestArtifactPayload -ArtifactRoot $root -Selection @($selection[0]) } |
            Should Throw 'required file is missing or empty'
    }

    It 'rejects an empty fixture package' {
        New-ArtifactTestPayload $selection[0]
        $package = Get-ChildItem $root -Recurse -Filter 'Alpha.TestApp.msix' -File
        [IO.File]::WriteAllBytes($package.FullName, [byte[]]@())
        { Assert-UiTestArtifactPayload -ArtifactRoot $root -Selection @($selection[0]) } |
            Should Throw 'required file is missing or empty'
    }

    It 'rejects ambiguous runner outputs rather than choosing an arbitrary framework' {
        New-ArtifactTestPayload $selection[0]
        New-ArtifactTestPayload $selection[0] -Framework 'net9.0-windows'
        { Assert-UiTestArtifactPayload -ArtifactRoot $root -Selection @($selection[0]) } |
            Should Throw 'expected one runner'
    }

    It 'retains the separate MWB runtime validation route on <Platform>' -TestCases @(
        @{ Platform = 'x64'; Route = 'MwbRuntimeManifest' }
        @{ Platform = 'arm64'; Route = 'UnsupportedPlatform' }
    ) {
        param($Platform, $Route)
        New-ArtifactTestProject -Name 'MouseWithoutBorders.UITests'
        $mwb = @(Get-UiTestArtifactSelection -RepoRoot $script:artifactCaseRoot -Modules 'MouseWithoutBorders.UITests' -Platform $Platform)
        $mwb[0].Validation | Should Be $Route
        New-Item -ItemType Directory -Path $root -Force | Out-Null
        $receipt = @(Assert-UiTestArtifactPayload -ArtifactRoot $root -Selection $mwb)
        $receipt[0].Validation | Should Be $Route
        @($receipt[0].Files).Count | Should Be 0
    }
}

Describe 'Repository UI-test artifact contract' {
    It 'resolves every current suite and the MSIX fixtures from its project metadata' {
        $repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
        $selection = @(Get-UiTestArtifactSelection -RepoRoot $repoRoot -Platform x64)
        ($selection.Count -gt 0) | Should Be $true
        (($selection | Where-Object Name -eq 'CropAndLock.UITests').Files -contains 'CropAndLock.TestApp.msix') | Should Be $true
        (($selection | Where-Object Name -eq 'Workspaces.UITests.Next').Files -contains 'Workspaces.TestApp.msix') | Should Be $true
        (($selection | Where-Object Name -eq 'Microsoft.CmdPal.UITests.Next').Files -contains 'SamplePagesExtension.msix') | Should Be $true
    }
}

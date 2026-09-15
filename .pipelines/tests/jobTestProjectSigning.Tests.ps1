# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

#Requires -Version 7.0

$templatePath = Join-Path $PSScriptRoot '..\v2\templates\job-test-project.yml'
$template = Get-Content -LiteralPath $templatePath -Raw
$start = $template.IndexOf('      $modulesRaw = ', [StringComparison]::Ordinal)
if ($start -lt 0) {
    throw 'The companion-signing selection block could not be located.'
}
$end = $template.IndexOf('      if ($requiredAuthenticodeFiles.Count -gt 0)', $start, [StringComparison]::Ordinal)
if ($end -le $start) {
    throw 'The companion-signing selection block could not be located.'
}

# Execute the real selection logic, substituting only pipeline/environment inputs.
# Stop before any process, certificate, file, or signing operation.
$selection = $template.Substring($start, $end - $start)
$selection = [regex]::new('(?m)^\s*\$modulesRaw = [^\r\n]*').Replace($selection, '$modulesRaw = $Modules', 1)
$selection = $selection -replace '(?m)^\s*\$certificateMarkerPath = [^\r\n]*', ''
$selection = $selection.Replace("'`$(TestPlatform)'", '$TestPlatform')
$selection = $selection.Replace('$env:AUTO_UI_TEST_MODULES', '$AutoModules')
$parameters = @'
param([string]$Modules, [string]$TestPlatform, [string]$AutoModules)
'@
$result = @'
[pscustomobject]@{
    Files = @($requiredAuthenticodeFiles)
    Packages = @($requiredPackages)
}
'@
$selectSigningFiles = [scriptblock]::Create($parameters + "`n" + $selection + "`n" + $result)

Describe 'Workspaces UI-test companion signing selection' {
    It 'requires the authenticated UI peer on <Platform> for <Selection>' -TestCases @(
        @{ Platform = 'arm64'; Selection = 'focused'; Modules = 'Workspaces.UITests.Next' }
        @{ Platform = 'x64'; Selection = 'focused'; Modules = 'Workspaces.UITests.Next' }
        @{ Platform = 'x64Win10'; Selection = 'focused'; Modules = 'Workspaces.UITests.Next' }
        @{ Platform = 'arm64'; Selection = 'all modules'; Modules = '' }
        @{ Platform = 'x64'; Selection = 'all modules'; Modules = '' }
        @{ Platform = 'x64Win10'; Selection = 'all modules'; Modules = '' }
    ) {
        param($Platform, $Selection, $Modules)
        $actual = & $selectSigningFiles -Modules $Modules -TestPlatform $Platform
        ($actual.Files -contains 'PowerToys.WorkspacesLauncherUI.exe') | Should Be $true
        ($actual.Files -contains 'PowerToys.QuickAccess.exe') | Should Be $true
        ($actual.Files -contains 'PowerToys.exe') | Should Be $true
        ($actual.Files -contains 'PowerToys.Settings.exe') | Should Be $true
        ($actual.Packages -contains 'Workspaces.TestApp.msix') | Should Be $true
    }

    It 'uses auto-selected Workspaces modules rather than the static selection' {
        $actual = & $selectSigningFiles -Modules 'PowerRename.UITests' -TestPlatform 'arm64' -AutoModules 'Workspaces.UITests.Next'
        ($actual.Files -contains 'PowerToys.WorkspacesLauncherUI.exe') | Should Be $true
        ($actual.Packages -contains 'Workspaces.TestApp.msix') | Should Be $true
        ($actual.Packages -contains 'PowerRenameContextMenuPackage.msix') | Should Be $false
    }

    It 'does not add the Workspaces peer for unrelated or similarly named modules' -TestCases @(
        @{ Modules = 'FancyZonesEditor.UITests.Next' }
        @{ Modules = 'Workspaces.UITests.Next.NotSelected' }
        @{ Modules = 'PowerRename.UITests' }
    ) {
        param($Modules)
        $actual = & $selectSigningFiles -Modules $Modules -TestPlatform 'x64'
        ($actual.Files -contains 'PowerToys.WorkspacesLauncherUI.exe') | Should Be $false
    }

    It 'preserves the existing authenticated Settings selection' {
        $actual = & $selectSigningFiles -Modules 'PowerRename.UITests' -TestPlatform 'x64'
        ($actual.Files -contains 'PowerToys.exe') | Should Be $true
        ($actual.Files -contains 'PowerToys.Settings.exe') | Should Be $true
        ($actual.Packages -contains 'PowerRenameContextMenuPackage.msix') | Should Be $true
    }

    It 'signs Workspaces companions once when multiple suites are selected' {
        $actual = & $selectSigningFiles -Modules 'Hosts.UITests;Workspaces.UITests.Next' -TestPlatform 'arm64'
        @($actual.Files | Where-Object { $_ -eq 'PowerToys.WorkspacesLauncherUI.exe' }).Count | Should Be 1
        @($actual.Files | Select-Object -Unique).Count | Should Be $actual.Files.Count
    }
}

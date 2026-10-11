# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.

$pipelinePath = Join-Path $PSScriptRoot '..\v2\templates\job-test-project.yml'

function Get-UITestSigningSelection {
    param(
        [string[]] $Modules = @(),
        [string] $Platform = 'x64Win11'
    )

    $yaml = Get-Content -LiteralPath $pipelinePath -Raw
    $start = $yaml.IndexOf('      $selectedModules =')
    $end = $yaml.IndexOf('      if ($requiredAuthenticodeFiles.Count -gt 0)', $start)
    if ($start -lt 0 -or $end -le $start) {
        throw 'The signing-selection block was not found in the pipeline.'
    }

    $selection = $yaml.Substring($start, $end - $start).
        Replace('$(TestPlatform)', $Platform).
        Replace('$(Agent.WorkFolder)', $TestDrive)
    $block = [scriptblock]::Create(
        'param($modulesRaw)' + "`n" + $selection + "`n" + @'
[pscustomobject]@{
    RequiresNewPlus = $requiresNewPlus
    RequiresAuthenticatedSettingsIpc = $requiresAuthenticatedSettingsIpc
    Packages = @($requiredPackages)
    Files = @($requiredAuthenticodeFiles)
}
'@)
    & $block ($Modules -join ';')
}

Describe 'UI test signing selection' {
    $authenticatedModules = @(
        'AdvancedPaste-UITests',
        'AdvancedPaste.UITests.Next',
        'ColorPicker.UITests',
        'CropAndLock.UITests',
        'FileLocksmith.UITests',
        'HostsEditor.UITests',
        'Hosts.UITests.Next',
        'ImageResizer.UITests',
        'LightSwitch.UITests.Next',
        'MouseUtils.UITests',
        'MouseUtils.UITests.Next',
        'NewPlus.UITests',
        'NewPlus.UITests.Next',
        'PowerAccent.UITests',
        'PowerRename.UITests',
        'PowerRename.UITests.Next',
        'RegistryPreview.UITests',
        'ScreenRuler.UITests.Next',
        'Workspaces.UITests.Next',
        'ZoomIt.UITests'
    )

    It 'requires both IPC companions for <Module>' -TestCases @(
        $authenticatedModules | ForEach-Object { @{ Module = $_ } }
    ) {
        param($Module)
        $result = Get-UITestSigningSelection -Modules $Module

        $result.RequiresAuthenticatedSettingsIpc | Should Be $true
        ($result.Files -contains 'PowerToys.exe') | Should Be $true
        ($result.Files -contains 'PowerToys.Settings.exe') | Should Be $true
    }

    It 'does not require IPC companions for unrelated project <Module>' -TestCases @(
        'OtherNewPlus.UITests',
        'NewPlus.UITests.Extra',
        'OtherPowerRename.UITests',
        'PowerRename.FuzzingTest',
        'AdvancedPaste.UITests.Next.Extra',
        'FancyZones.UITests.Next',
        'FileLocksmith.UITests.Extra',
        'ImageResizer.UITests.Extra',
        'LightSwitch.UITests.Next.Extra',
        'ScreenRuler.UITests.Next.Extra',
        'Workspaces.Editor.UITests',
        'ZoomIt.UITests.Extra' | ForEach-Object { @{ Module = $_ } }
    ) {
        param($Module)
        $result = Get-UITestSigningSelection -Modules $Module

        $result.RequiresAuthenticatedSettingsIpc | Should Be $false
        ($result.Files -contains 'PowerToys.exe') | Should Be $false
        ($result.Files -contains 'PowerToys.Settings.exe') | Should Be $false
    }

    # Regression: an ImageResizer-only PR selection must not rely on another suite to sign the
    # Runner/Settings companions, or the Release Runner rejects Settings as not-microsoft-signed.
    It 'signs IPC companions for an ImageResizer-only selection on <Platform>' -TestCases @(
        'x64Win10', 'x64Win11', 'ARM64' | ForEach-Object { @{ Platform = $_ } }
    ) {
        param($Platform)
        $result = Get-UITestSigningSelection -Modules 'ImageResizer.UITests' -Platform $Platform

        $result.RequiresNewPlus | Should Be $false
        $result.RequiresAuthenticatedSettingsIpc | Should Be $true
        ($result.Files -contains 'PowerToys.exe') | Should Be $true
        ($result.Files -contains 'PowerToys.Settings.exe') | Should Be $true
        ($result.Packages -contains 'ImageResizerContextMenuPackage.msix') | Should Be ($Platform -ne 'x64Win10')
    }

    It 'uses consistent New+ requirements for <Module> on <Platform>' -TestCases @(
        foreach ($module in @('NewPlus.UITests', 'NewPlus.UITests.Next')) {
            foreach ($platform in @('x64Win10', 'x64Win11', 'ARM64')) {
                @{ Module = $module; Platform = $platform }
            }
        }
    ) {
        param($Module, $Platform)
        $result = Get-UITestSigningSelection -Modules $Module -Platform $Platform

        $result.RequiresNewPlus | Should Be $true
        $result.RequiresAuthenticatedSettingsIpc | Should Be $true
        ($result.Packages -contains 'NewPlusPackage.msix') | Should Be ($Platform -ne 'x64Win10')
    }

    It 'preserves all-module behavior on <Platform>' -TestCases @(
        'x64Win10', 'x64Win11', 'ARM64' | ForEach-Object { @{ Platform = $_ } }
    ) {
        param($Platform)
        $result = Get-UITestSigningSelection -Platform $Platform

        $result.RequiresNewPlus | Should Be $true
        $result.RequiresAuthenticatedSettingsIpc | Should Be $true
        ($result.Packages -contains 'NewPlusPackage.msix') | Should Be ($Platform -ne 'x64Win10')
        ($result.Files -contains 'PowerToys.exe') | Should Be $true
        ($result.Files -contains 'PowerToys.Settings.exe') | Should Be $true
    }

    It 'accepts mixed module selections and preserves case-insensitive matching' {
        $result = Get-UITestSigningSelection -Modules @(
            'FancyZones.UITests.Next',
            'advancedpaste.uitests.next',
            'newplus.uitests'
        )

        $result.RequiresNewPlus | Should Be $true
        $result.RequiresAuthenticatedSettingsIpc | Should Be $true
        ($result.Packages -contains 'NewPlusPackage.msix') | Should Be $true
    }
}

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
        'AltWindowCycle.UITests',
        'ColorPicker.UITests',
        'CropAndLock.UITests',
        'FileLocksmith.UITests',
        'GrabAndMove.UITests',
        'HostsEditor.UITests',
        'Hosts.UITests.Next',
        'ImageResizer.UITests',
        'LightSwitch.UITests.Next',
        'Microsoft.CmdPal.UITests.Next',
        'MouseWithoutBorders.UITests',
        'MouseUtils.UITests',
        'MouseUtils.UITests.Next',
        'NewPlus.UITests',
        'NewPlus.UITests.Next',
        'PowerAccent.UITests',
        'PowerRename.UITests',
        'PowerRename.UITests.Next',
        'RegistryPreview.UITests',
        'ScreenRuler.UITests.Next',
        'ShortcutGuide.UITests',
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
        'AltWindowCycle.UITests.Extra',
        'FancyZones.UITests.Next',
        'FileLocksmith.UITests.Extra',
        'GrabAndMove.UITests.Extra',
        'ImageResizer.UITests.Extra',
        'LightSwitch.UITests.Next.Extra',
        'Microsoft.CmdPal.UITests.Next.Extra',
        'MouseWithoutBorders.UITests.Extra',
        'ScreenRuler.UITests.Next.Extra',
        'ShortcutGuide.UITests.Extra',
        'Workspaces.Editor.UITests',
        'ZoomIt.UITests.Extra' | ForEach-Object { @{ Module = $_ } }
    ) {
        param($Module)
        $result = Get-UITestSigningSelection -Modules $Module

        $result.RequiresAuthenticatedSettingsIpc | Should Be $false
        ($result.Files -contains 'PowerToys.exe') | Should Be $false
        ($result.Files -contains 'PowerToys.Settings.exe') | Should Be $false
    }

    It 'signs IPC companions for a ShortcutGuide-only selection on <Platform>' -TestCases @(
        'x64Win10', 'x64Win11', 'ARM64' | ForEach-Object { @{ Platform = $_ } }
    ) {
        param($Platform)
        $result = Get-UITestSigningSelection -Modules 'ShortcutGuide.UITests' -Platform $Platform

        $result.RequiresAuthenticatedSettingsIpc | Should Be $true
        ($result.Files -contains 'PowerToys.exe') | Should Be $true
        ($result.Files -contains 'PowerToys.Settings.exe') | Should Be $true
        $result.Packages.Count | Should Be 0
    }

    It 'scopes the SamplePages package to CmdPal on <Platform>' -TestCases @(
        'x64Win10', 'x64Win11', 'ARM64' | ForEach-Object { @{ Platform = $_ } }
    ) {
        param($Platform)
        $selected = Get-UITestSigningSelection -Modules 'Microsoft.CmdPal.UITests.Next' -Platform $Platform
        ($selected.Packages -contains 'SamplePagesExtension.msix') | Should Be $true
        $unrelated = Get-UITestSigningSelection -Modules 'Microsoft.CmdPal.UITests.Next.Extra' -Platform $Platform
        ($unrelated.Packages -contains 'SamplePagesExtension.msix') | Should Be $false
        $all = Get-UITestSigningSelection -Platform $Platform
        ($all.Packages -contains 'SamplePagesExtension.msix') | Should Be $true
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

    It 'signs the shared Workspaces session peers on <Platform>' -TestCases @(
        'x64Win10', 'x64Win11', 'ARM64' | ForEach-Object { @{ Platform = $_ } }
    ) {
        param($Platform)
        foreach ($modules in @(@('Workspaces.UITests.Next'), @())) {
            $result = Get-UITestSigningSelection -Modules $modules -Platform $Platform
            foreach ($file in @('PowerToys.WorkspacesCLI.exe', 'PowerToys.WorkspacesLauncher.exe',
                    'PowerToys.WorkspacesWindowArranger.exe', 'PowerToys.WorkspacesLauncherUI.exe')) {
                ($result.Files -contains $file) | Should Be $true
            }
        }
        $unrelated = Get-UITestSigningSelection -Modules 'Workspaces.Editor.UITests' -Platform $Platform
        ($unrelated.Files -contains 'PowerToys.WorkspacesCLI.exe') | Should Be $false
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

    It 'keeps shared signing defaults while allowing an owned payload-specific certificate scope' {
        $source = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\signSparsePackages.ps1') -Raw
        $source | Should Match "\[string\]\`$CertificateFriendlyName = 'PowerToys UI Test Signing'"
        $source | Should Match '\$testCertificateFriendlyName = \$CertificateFriendlyName'
        $mwb = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\mwb\MwbSandboxCi.Common.ps1') -Raw
        $mwb | Should Match '-CertificateFriendlyName "PowerToys MWB UI Test \$RunId"'
        $mwb | Should Match "'PowerToys.MouseWithoutBorders.exe'"
        $mwb | Should Match "'PowerToys.Settings.exe'"
        $mwb | Should Not Match '(?i)Export-PfxCertificate'
    }
}

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
        'Awake.UITests',
        'ColorPicker.UITests',
        'CropAndLock.UITests',
        'FileLocksmith.UITests',
        'GrabAndMove.UITests',
        'HostsEditor.UITests',
        'Hosts.UITests.Next',
        'ImageResizer.UITests',
        'LightSwitch.UITests.Next',
        'MouseWithoutBorders.UITests',
        'MouseUtils.UITests',
        'MouseUtils.UITests.Next',
        'NewPlus.UITests',
        'NewPlus.UITests.Next',
        'PowerAccent.UITests',
        'PowerDisplay.UITests',
        'PowerOCR.UITests.Next',
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
        'Awake.UITests.Extra',
        'FancyZones.UITests.Next',
        'FileLocksmith.UITests.Extra',
        'GrabAndMove.UITests.Extra',
        'ImageResizer.UITests.Extra',
        'LightSwitch.UITests.Next.Extra',
        'MouseWithoutBorders.UITests.Extra',
        'PowerDisplay.UITests.Extra',
        'PowerOCR.UITests.Next.Extra',
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

Describe 'PowerOCR language prerequisites' {
    BeforeEach {
        $yaml = Get-Content -LiteralPath $pipelinePath -Raw
        $end = $yaml.IndexOf('    displayName: "Provision PowerOCR test languages"')
        $start = $yaml.LastIndexOf('  - powershell: |', $end)
        if ($start -lt 0 -or $end -le $start) { throw 'PowerOCR provisioning step was not found.' }
        $start = $yaml.IndexOf("`n", $start) + 1
        $source = $yaml.Substring($start, $end - $start) -replace '(?m)^ {6}', ''
        $source = $source -replace '(?m)^\$modulesRaw = .+\r?\n', ''
        $setup = [scriptblock]::Create('param($modulesRaw)' + "`n" + $source)
        $fixture = @{ States = @{}; Messages = [Collections.Generic.List[string]]::new(); FailInstall = $false }
        foreach ($name in @(
            'Language.Basic~~~en-US~0.0.1.0', 'Language.Basic~~~en-GB~0.0.1.0',
            'Language.OCR~~~en-US~0.0.1.0', 'Language.OCR~~~en-GB~0.0.1.0')) {
            $fixture.States[$name] = 'Installed'
        }
        $originalAutoModules = $env:AUTO_UI_TEST_MODULES
        $originalReady = $env:POWER_OCR_LANGUAGES_READY
        $env:AUTO_UI_TEST_MODULES = $null
        Mock Write-Host { param($Object) $fixture.Messages.Add([string]$Object) }
        Mock Get-WindowsCapability { param($Name) [pscustomobject]@{ State = $fixture.States[$Name] } }
        Mock Add-WindowsCapability {
            param($Name)
            if ($fixture.FailInstall) { throw 'Capability installation failed.' }
            $fixture.States[$Name] = 'Installed'
            [pscustomobject]@{ RestartNeeded = $false }
        }
    }

    AfterEach {
        $env:AUTO_UI_TEST_MODULES = $originalAutoModules
        $env:POWER_OCR_LANGUAGES_READY = $originalReady
    }

    It 'scopes provisioning to <Case>' -TestCases @(
        @{ Case = 'PowerOCR'; Modules = 'PowerOCR.UITests.Next'; Auto = ''; Calls = 8 }
        @{ Case = 'all modules'; Modules = ''; Auto = ''; Calls = 8 }
        @{ Case = 'an unrelated suite'; Modules = 'ColorPicker.UITests'; Auto = ''; Calls = 0 }
        @{ Case = 'a similar name'; Modules = 'PowerOCR.UITests.Next.Extra'; Auto = ''; Calls = 0 }
        @{ Case = 'auto-selected PowerOCR'; Modules = 'ColorPicker.UITests'; Auto = 'PowerOCR.UITests.Next'; Calls = 8 }
        @{ Case = 'an unrelated auto-selection'; Modules = 'PowerOCR.UITests.Next'; Auto = 'ColorPicker.UITests'; Calls = 0 }
    ) {
        param($Case, $Modules, $Auto, $Calls)
        $env:AUTO_UI_TEST_MODULES = $Auto
        & $setup $Modules

        Assert-MockCalled Get-WindowsCapability -Times $Calls -Exactly -Scope It
        Assert-MockCalled Add-WindowsCapability -Times 0 -Exactly -Scope It
        ($fixture.Messages -contains '##vso[task.setvariable variable=PowerOcrLanguagesReady]true') | Should Be ($Calls -gt 0)
    }

    It 'installs a missing capability and marks readiness only after verification' {
        $fixture.States['Language.OCR~~~en-GB~0.0.1.0'] = 'NotPresent'
        & $setup 'PowerOCR.UITests.Next'

        Assert-MockCalled Add-WindowsCapability -Times 1 -Exactly -Scope It -ParameterFilter { $Name -eq 'Language.OCR~~~en-GB~0.0.1.0' }
        ($fixture.Messages -contains '##vso[task.setvariable variable=PowerOcrLanguagesReady]true') | Should Be $true
        $source | Should Not Match '(Get|Set)-WinUserLanguageList'
    }

    It 'leaves PowerOCR blocked when capability installation fails' {
        $fixture.States['Language.OCR~~~en-GB~0.0.1.0'] = 'NotPresent'
        $fixture.FailInstall = $true
        { & $setup 'PowerOCR.UITests.Next' } | Should Throw 'Capability installation failed.'

        ($fixture.Messages -contains '##vso[task.setvariable variable=PowerOcrLanguagesReady]false') | Should Be $true
        ($fixture.Messages -contains '##vso[task.setvariable variable=PowerOcrLanguagesReady]true') | Should Be $false
        $yaml | Should Match '(?s)displayName: "Provision PowerOCR test languages"\s+continueOnError: true'
    }

    It 'isolates PowerOCR dispatch when readiness is <Ready>' -TestCases @(
        @{ Ready = 'false'; Failed = 1; Runners = 'Other.UITests' }
        @{ Ready = 'true'; Failed = 0; Runners = 'PowerOCR.UITests.Next,Other.UITests' }
    ) {
        param($Ready, $Failed, $Runners)
        $env:POWER_OCR_LANGUAGES_READY = $Ready
        $guard = [regex]::Match(
            $yaml,
            '(?ms)^ {10}if \(\$base -eq ''PowerOCR\.UITests\.Next'' -and [^\r\n]*\) \{.*?^ {10}\}').Value
        $guard | Should Not BeNullOrEmpty
        $loop = @'
$failed = 0
$executed = @()
foreach ($base in @('PowerOCR.UITests.Next', 'Other.UITests')) {
    $suiteFailed = $false
    try {
'@ + "`n" + $guard + "`n" + @'
        $executed += $base
    } finally {
        if ($suiteFailed) { $failed++ }
    }
}
[pscustomobject]@{ Failed = $failed; Runners = ($executed -join ',') }
'@
        $result = & ([scriptblock]::Create($loop))
        $result.Failed | Should Be $Failed
        $result.Runners | Should Be $Runners
        $yaml | Should Match 'POWER_OCR_LANGUAGES_READY: \$\(PowerOcrLanguagesReady\)'
    }
}

# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.

# CI supplies the platform/configuration output after building the WinUI applications.
param([string]$BuildOutput)

Describe 'Workspaces installer integration' {
    BeforeEach {
        $script:workspacesRepoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
        $script:workspacesInstallerRoot = Join-Path $script:workspacesRepoRoot 'installer\PowerToysSetupVNext'
        $script:assertWorkspacesEqual = {
            param(
                [AllowNull()][object]$Actual,
                [AllowNull()][object]$Expected,
                [string]$Context
            )

            if (-not [object]::Equals($Actual, $Expected)) {
                throw "${Context}: expected '$Expected', got '$Actual'."
            }
        }
    }

    It 'uses the executable basename for process cleanup and event-log queries' {
        foreach ($list in @(
            @{ Path = 'installer\PowerToysSetupCustomActionsVNext\CustomAction.cpp'; Name = 'processesToTerminate' }
            @{ Path = 'tools\BugReportTool\BugReportTool\ProcessesList.cpp'; Name = 'processes' }
        )) {
            $source = Get-Content -LiteralPath (Join-Path $script:workspacesRepoRoot $list.Path) -Raw
            # Preserve quoted strings, including URLs, while removing line and block comments.
            $source = $source -creplace '("(?:\\.|[^"\\\r\n])*")|//[^\r\n]*|(?s:/\*.*?\*/)', '$1 '
            $pattern = '\b' + [regex]::Escape($list.Name) + '\s*(?:\[\s*\])?\s*=\s*\{(?<entries>[^{}]*)\}\s*;'
            $initializer = [regex]::Match($source, $pattern)
            & $script:assertWorkspacesEqual -Actual $initializer.Success -Expected $true -Context "$($list.Path) $($list.Name) initializer"
            $entries = [regex]::Matches($initializer.Groups['entries'].Value, '(?:^|,)\s*L"([^"\r\n]*WorkspacesEditor\.exe)"\s*(?=,|$)')

            & $script:assertWorkspacesEqual -Actual $entries.Count -Expected 1 -Context "$($list.Path) process entry count"
            & $script:assertWorkspacesEqual -Actual $entries[0].Groups[1].Value -Expected 'PowerToys.WorkspacesEditor.exe' -Context "$($list.Path) process basename"
        }
    }

    It 'omits Editor satellites while retaining the Launcher UI resx satellites' {
        [xml]$resources = Get-Content -LiteralPath (Join-Path $script:workspacesInstallerRoot 'Resources.wxs') -Raw
        [xml]$product = Get-Content -LiteralPath (Join-Path $script:workspacesInstallerRoot 'Product.wxs') -Raw
        $namespace = New-Object System.Xml.XmlNamespaceManager($resources.NameTable)
        $namespace.AddNamespace('w', $resources.DocumentElement.NamespaceURI)
        $files = @($resources.SelectNodes('//w:File', $namespace))

        $editorName = 'PowerToys.WorkspacesEditor.resources.dll'
        $launcherName = 'PowerToys.WorkspacesLauncherUI.resources.dll'
        $editorFiles = @($files | Where-Object {
            [IO.Path]::GetFileName($_.Source) -eq $editorName -or $_.GetAttribute('Name') -eq $editorName
        })
        & $script:assertWorkspacesEqual -Actual $editorFiles.Count -Expected 0 -Context 'Editor satellite installation entries'
        $launcherFiles = @($files | Where-Object { [IO.Path]::GetFileName($_.Source) -eq $launcherName })
        & $script:assertWorkspacesEqual -Actual $launcherFiles.Count -Expected 1 -Context 'Launcher UI satellite installation entries'
        $launcherFile = $launcherFiles[0]
        & $script:assertWorkspacesEqual -Actual $launcherFile.Source -Expected '$(var.BinDir)\WinUI3Apps\$(var.Language)\PowerToys.WorkspacesLauncherUI.resources.dll' -Context 'Launcher UI satellite source'
        & $script:assertWorkspacesEqual -Actual ($launcherFile.GetAttribute('Name') -in @('', $launcherName)) -Expected $true -Context 'Installed Launcher UI satellite filename'
        & $script:assertWorkspacesEqual -Actual ($launcherFile.ParentNode.GetAttribute('Directory')) -Expected 'Resource$(var.IdSafeLanguage)WinUI3AppsInstallFolder' -Context 'Launcher UI satellite installation directory'
        $resourceGroup = $launcherFile.SelectSingleNode('ancestor::w:ComponentGroup', $namespace)
        $coreGroups = @($product.SelectNodes('//w:Feature[@Id="CoreFeature"]/w:ComponentGroupRef', $namespace)).Id
        & $script:assertWorkspacesEqual -Actual ($coreGroups -contains $resourceGroup.Id) -Expected $true -Context 'Launcher UI satellite is part of CoreFeature'
    }

    if ($BuildOutput) {
        It 'stages the Editor PRI and both Workspaces icons in the built WinUI applications directory' {
            $pri = Get-Item -LiteralPath (Join-Path $BuildOutput 'WinUI3Apps\PowerToys.WorkspacesEditor.pri') -ErrorAction Stop
            & $script:assertWorkspacesEqual -Actual ($pri -is [IO.FileInfo] -and $pri.Length -gt 0) -Expected $true -Context 'Built Editor PRI'
            $source = Join-Path $script:workspacesRepoRoot 'src\modules\Workspaces\Assets\Workspaces'
            $output = Join-Path $BuildOutput 'WinUI3Apps\Assets\Workspaces'
            foreach ($iconName in @('Workspaces.ico', 'DefaultIcon.ico')) {
                $expected = Get-FileHash -LiteralPath (Join-Path $source $iconName) -ErrorAction Stop
                $built = Get-FileHash -LiteralPath (Join-Path $output $iconName) -ErrorAction Stop
                & $script:assertWorkspacesEqual -Actual $built.Hash -Expected $expected.Hash -Context "$iconName in $output"
            }
        }
    }

    It 'installs the Editor PRI and both Workspaces icons on <Platform>' -TestCases @(
        @{ Platform = 'x64' }
        @{ Platform = 'ARM64' }
    ) {
        param($Platform)
        $fixtureRoot = Join-Path $TestDrive $Platform
        $fixtureInstallerRoot = Join-Path $fixtureRoot 'installer\PowerToysSetupVNext'
        $winuiOutput = Join-Path $fixtureRoot "$Platform\Release\WinUI3Apps"
        $assetsOutput = Join-Path $winuiOutput 'Assets\Workspaces'
        New-Item -ItemType Directory -Path $fixtureInstallerRoot, $assetsOutput -Force | Out-Null
        Copy-Item -Path (Join-Path $script:workspacesInstallerRoot '*.wxs') -Destination $fixtureInstallerRoot
        foreach ($scriptName in @('generateAllFileComponents.ps1', 'InstallerMedia.ps1')) {
            Copy-Item -LiteralPath (Join-Path $script:workspacesInstallerRoot $scriptName) -Destination $fixtureInstallerRoot
        }
        $iconNames = @('Workspaces.ico', 'DefaultIcon.ico')
        foreach ($iconName in $iconNames) {
            [IO.File]::WriteAllText((Join-Path $assetsOutput $iconName), "$Platform $iconName")
        }
        $editorPri = 'PowerToys.WorkspacesEditor.pri'
        [IO.File]::WriteAllText((Join-Path $winuiOutput $editorPri), "$Platform $editorPri")

        # Other modules have no payload in this fixture. Give the generator each requested
        # dependency's real parent path even when that directory does not exist on disk.
        Mock Get-ChildItem { [IO.FileInfo]::new([string]$Path) } -ParameterFilter { $Path -like '*.deps.json' }
        Mock Get-Content { '{"targets":{"empty":{}}}' } -ParameterFilter { $Path -like '*.deps.json' }
        Mock Get-ChildItem { } -ParameterFilter {
            $File -and $Name -and $Path -and $Path -notlike '*\Assets\Workspaces*' -and !(Test-Path -LiteralPath $Path)
        }
        & (Join-Path $fixtureInstallerRoot 'generateAllFileComponents.ps1') -platform $Platform

        [xml]$workspaces = Get-Content -LiteralPath (Join-Path $fixtureInstallerRoot 'Workspaces.wxs') -Raw
        [xml]$product = Get-Content -LiteralPath (Join-Path $script:workspacesInstallerRoot 'Product.wxs') -Raw
        $namespace = New-Object System.Xml.XmlNamespaceManager($workspaces.NameTable)
        $namespace.AddNamespace('w', $workspaces.DocumentElement.NamespaceURI)
        [xml]$winui = Get-Content -LiteralPath (Join-Path $fixtureInstallerRoot 'WinUI3Applications.wxs') -Raw
        $winuiDirectory = $product.SelectSingleNode('//w:Directory[@Id="INSTALLFOLDER"]/w:Directory[@Name="WinUI3Apps"]', $namespace)
        & $script:assertWorkspacesEqual -Actual ($null -ne $winuiDirectory) -Expected $true -Context 'Installed WinUI applications directory'
        $coreGroups = @($product.SelectNodes('//w:Feature[@Id="CoreFeature"]/w:ComponentGroupRef', $namespace)).Id
        $winuiComponents = @($winui.SelectNodes('//w:ComponentGroup', $namespace) | Where-Object {
            $_.Id -in $coreGroups
        } | ForEach-Object { $_.SelectNodes('w:ComponentRef', $namespace) }).Id
        $installedPri = @($winui.SelectNodes("//w:DirectoryRef[@Id='$($winuiDirectory.Id)']/w:Component/w:File", $namespace) | Where-Object {
            [IO.Path]::GetFileName($_.Source) -eq $editorPri -and
                $_.GetAttribute('Name') -in @('', $editorPri) -and $_.ParentNode.Id -in $winuiComponents
        })
        & $script:assertWorkspacesEqual -Actual $installedPri.Count -Expected 1 -Context "$Platform Editor PRI installed with CoreFeature in WinUI3Apps"
        $featureGroup = $product.SelectSingleNode('//w:Feature[@Id="CoreFeature"]/w:ComponentGroupRef[@Id="WorkspacesComponentGroup"]', $namespace)
        & $script:assertWorkspacesEqual -Actual ($null -ne $featureGroup) -Expected $true -Context 'Installed Workspaces component group'
        $assets = $product.SelectSingleNode('//w:Directory[@Id="INSTALLFOLDER"]/w:Directory[@Name="WinUI3Apps"]/w:Directory[@Name="Assets"]', $namespace)
        & $script:assertWorkspacesEqual -Actual ($null -ne $assets) -Expected $true -Context 'Installed WinUI assets directory'
        $workspaceAssets = $workspaces.SelectSingleNode("//w:DirectoryRef[@Id='$($assets.Id)']/w:Directory[@Name='Workspaces']", $namespace)
        & $script:assertWorkspacesEqual -Actual ($null -ne $workspaceAssets) -Expected $true -Context 'Installed WinUI Workspaces assets directory'

        $buildAssets = @('$(var.BinDir)WinUI3Apps\Assets\Workspaces', '$(var.BinDir)\WinUI3Apps\Assets\Workspaces')
        $sourceDirectories = @(
            $buildAssets
            foreach ($definition in $workspaces.DocumentElement.SelectNodes('processing-instruction("define")')) {
                $symbol, $value = $definition.Value -split '=', 2
                if ($value.Trim().TrimEnd('\') -in $buildAssets) {
                    '$(var.' + $symbol.Trim() + ')'
                }
            }
        )
        $group = $workspaces.SelectSingleNode('//w:ComponentGroup[@Id="WorkspacesComponentGroup"]', $namespace)
        $componentIds = @($group.SelectNodes('w:ComponentRef', $namespace)).Id
        $files = $workspaces.SelectNodes("//w:DirectoryRef[@Id='$($workspaceAssets.Id)']/w:Component/w:File", $namespace)
        foreach ($iconName in $iconNames) {
            $installed = @($files | Where-Object {
                [IO.Path]::GetFileName($_.Source) -eq $iconName -and
                    $_.GetAttribute('Name') -in @('', $iconName) -and
                    [IO.Path]::GetDirectoryName($_.Source) -in $sourceDirectories -and
                    $_.ParentNode.Id -in $componentIds
            })
            & $script:assertWorkspacesEqual -Actual $installed.Count -Expected 1 -Context "$iconName installed from the WinUI output"
        }
        $cleanup = $group.SelectNodes("w:Component/w:RemoveFolder[@Directory='$($workspaceAssets.Id)' and @On='uninstall']", $namespace)
        & $script:assertWorkspacesEqual -Actual $cleanup.Count -Expected 1 -Context 'WinUI Workspaces assets uninstall cleanup'
    }
}

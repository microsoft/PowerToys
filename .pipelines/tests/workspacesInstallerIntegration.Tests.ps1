# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.

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
        $namespace = New-Object System.Xml.XmlNamespaceManager($resources.NameTable)
        $namespace.AddNamespace('w', $resources.DocumentElement.NamespaceURI)
        $files = @($resources.SelectNodes('//w:File', $namespace))

        $editorFiles = @($files | Where-Object { $_.Source -like '*PowerToys.WorkspacesEditor.resources.dll' })
        & $script:assertWorkspacesEqual -Actual $editorFiles.Count -Expected 0 -Context 'Editor satellite installation entries'
        $launcherFiles = @($files | Where-Object { $_.Source -like '*PowerToys.WorkspacesLauncherUI.resources.dll' })
        & $script:assertWorkspacesEqual -Actual $launcherFiles.Count -Expected 1 -Context 'Launcher UI satellite installation entries'
        & $script:assertWorkspacesEqual -Actual $launcherFiles[0].Source -Expected '$(var.BinDir)\WinUI3Apps\$(var.Language)\PowerToys.WorkspacesLauncherUI.resources.dll' -Context 'Launcher UI satellite source'
        & $script:assertWorkspacesEqual -Actual ($launcherFiles[0].ParentNode.GetAttribute('Directory')) -Expected 'Resource$(var.IdSafeLanguage)WinUI3AppsInstallFolder' -Context 'Launcher UI satellite installation directory'
    }

    It 'installs the window icon and missing application fallback beside the WinUI apps' {
        $fixtureRoot = Join-Path $TestDrive 'workspace'
        $fixtureInstallerRoot = Join-Path $fixtureRoot 'installer\PowerToysSetupVNext'
        $fixtureBuildPlatform = 'x64'
        $assetsOutput = Join-Path $fixtureRoot "$fixtureBuildPlatform\Release\WinUI3Apps\Assets\Workspaces"
        New-Item -ItemType Directory -Path $fixtureInstallerRoot, $assetsOutput -Force | Out-Null
        Copy-Item -LiteralPath (Join-Path $script:workspacesInstallerRoot 'Workspaces.wxs') -Destination $fixtureInstallerRoot
        $iconNames = @('Workspaces.ico', 'DefaultIcon.ico')
        foreach ($iconName in $iconNames) {
            Copy-Item -LiteralPath (Join-Path $script:workspacesRepoRoot "src\modules\Workspaces\Assets\Workspaces\$iconName") -Destination $assetsOutput
        }

        $generatorPath = Join-Path $script:workspacesInstallerRoot 'generateAllFileComponents.ps1'
        $tokens = $null
        $parseErrors = $null
        $generator = [System.Management.Automation.Language.Parser]::ParseFile($generatorPath, [ref]$tokens, [ref]$parseErrors)
        & $script:assertWorkspacesEqual -Actual $parseErrors.Count -Expected 0 -Context 'Asset generator syntax errors'
        $statements = $generator.EndBlock.Statements
        $harvestCalls = @($statements | Where-Object {
            $_ -is [Management.Automation.Language.PipelineAst] -and
                $_.PipelineElements[0] -is [Management.Automation.Language.CommandAst] -and
                $_.PipelineElements[0].GetCommandName() -in @('Generate-FileList', 'Generate-FileComponents')
        })
        $workspaceCalls = @($harvestCalls | Where-Object { $_.PipelineElements[0].CommandElements.Extent.Text -match '[\\/]Workspaces\.wxs[''"]?$' })
        & $script:assertWorkspacesEqual -Actual ($workspaceCalls.Count -gt 0) -Expected $true -Context 'Workspaces.wxs harvesting calls'

        # Keep the generator's parameter block, initialization, and helper definitions together.
        # Run its Workspaces calls in their original order with their own input path assignments.
        $inputVariables = foreach ($call in $workspaceCalls) {
            $call.FindAll({ param($node) $node -is [Management.Automation.Language.VariableExpressionAst] }, $true).VariablePath.UserPath
        }
        $fixtureScript = @(
            $generator.Extent.Text.Substring(0, $harvestCalls[0].Extent.StartOffset)
            foreach ($statement in $statements) {
                if ($statement -in $workspaceCalls -or
                    ($statement -is [Management.Automation.Language.AssignmentStatementAst] -and
                     $statement.Left -is [Management.Automation.Language.VariableExpressionAst] -and
                     $statement.Left.VariablePath.UserPath -in $inputVariables)) {
                    $statement.Extent.Text
                }
            }
        )
        $mediaHelper = Join-Path $script:workspacesInstallerRoot 'InstallerMedia.ps1'
        if (Test-Path -LiteralPath $mediaHelper) {
            Copy-Item -LiteralPath $mediaHelper -Destination $fixtureInstallerRoot
        }
        $fixtureGenerator = Join-Path $fixtureInstallerRoot 'generateAllFileComponents.ps1'
        Set-Content -LiteralPath $fixtureGenerator -Value $fixtureScript -Encoding UTF8
        & $fixtureGenerator -platform $fixtureBuildPlatform

        [xml]$workspaces = Get-Content -LiteralPath (Join-Path $fixtureInstallerRoot 'Workspaces.wxs') -Raw
        [xml]$product = Get-Content -LiteralPath (Join-Path $script:workspacesInstallerRoot 'Product.wxs') -Raw
        $namespace = New-Object System.Xml.XmlNamespaceManager($workspaces.NameTable)
        $namespace.AddNamespace('w', $workspaces.DocumentElement.NamespaceURI)
        $featureGroup = $product.SelectSingleNode('//w:Feature/w:ComponentGroupRef[@Id="WorkspacesComponentGroup"]', $namespace)
        & $script:assertWorkspacesEqual -Actual ($null -ne $featureGroup) -Expected $true -Context 'Installed Workspaces component group'
        $assets = $product.SelectSingleNode('//w:Directory[@Name="WinUI3Apps"]/w:Directory[@Name="Assets"]', $namespace)
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

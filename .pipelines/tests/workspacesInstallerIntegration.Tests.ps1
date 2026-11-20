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

    It 'harvests the WinUI output for both installed asset locations without an old root output' {
        $fixtureRoot = Join-Path $TestDrive 'workspace'
        $fixtureInstallerRoot = Join-Path $fixtureRoot 'installer\PowerToysSetupVNext'
        $fixtureBuildPlatform = 'x64'
        $assetsOutput = Join-Path $fixtureRoot "$fixtureBuildPlatform\Release\WinUI3Apps\Assets\Workspaces"
        New-Item -ItemType Directory -Path $fixtureInstallerRoot, $assetsOutput -Force | Out-Null
        Copy-Item -LiteralPath (Join-Path $script:workspacesInstallerRoot 'Workspaces.wxs') -Destination $fixtureInstallerRoot
        foreach ($iconName in @('DefaultIcon.ico', 'Workspaces.ico')) {
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
        $namespace = New-Object System.Xml.XmlNamespaceManager($workspaces.NameTable)
        $namespace.AddNamespace('w', $workspaces.DocumentElement.NamespaceURI)
        $rootAssets = $workspaces.SelectSingleNode('//w:DirectoryRef[@Id="BaseApplicationsAssetsFolder"]/w:Directory', $namespace)
        & $script:assertWorkspacesEqual -Actual $rootAssets.Id -Expected 'WorkspacesAssetsInstallFolder' -Context 'Root asset directory ID'
        & $script:assertWorkspacesEqual -Actual ($rootAssets.GetAttribute('Name')) -Expected 'Workspaces' -Context 'Root asset directory name'
        $winUIAssets = $workspaces.SelectSingleNode('//w:DirectoryRef[@Id="WinUI3AppsAssetsFolder"]/w:Directory', $namespace)
        & $script:assertWorkspacesEqual -Actual $winUIAssets.Id -Expected 'WorkspacesWinUIAssetsInstallFolder' -Context 'WinUI asset directory ID'
        & $script:assertWorkspacesEqual -Actual ($winUIAssets.GetAttribute('Name')) -Expected 'Workspaces' -Context 'WinUI asset directory name'

        $componentReferences = @($workspaces.SelectNodes('//w:ComponentGroup[@Id="WorkspacesComponentGroup"]/w:ComponentRef', $namespace)).Id
        foreach ($directoryId in @('WorkspacesAssetsInstallFolder', 'WorkspacesWinUIAssetsInstallFolder')) {
            $component = $workspaces.SelectSingleNode("//w:DirectoryRef[@Id='$directoryId']/w:Component", $namespace)
            $files = @($component.SelectNodes('w:File', $namespace))
            & $script:assertWorkspacesEqual -Actual $files.Count -Expected 2 -Context "$directoryId icon count"
            $fileNames = @($files | ForEach-Object { [System.IO.Path]::GetFileName($_.Source) })
            & $script:assertWorkspacesEqual -Actual ($fileNames -contains 'DefaultIcon.ico') -Expected $true -Context "$directoryId default icon"
            & $script:assertWorkspacesEqual -Actual ($fileNames -contains 'Workspaces.ico') -Expected $true -Context "$directoryId Workspaces icon"
            & $script:assertWorkspacesEqual -Actual ($componentReferences -contains $component.Id) -Expected $true -Context "$directoryId component reference"
            $removeFolders = $workspaces.SelectNodes("//w:RemoveFolder[@Directory='$directoryId' and @On='uninstall']", $namespace)
            & $script:assertWorkspacesEqual -Actual $removeFolders.Count -Expected 1 -Context "$directoryId uninstall cleanup"
        }

        $fileIds = @($workspaces.SelectNodes('//w:File', $namespace)).Id
        & $script:assertWorkspacesEqual -Actual (@($fileIds | Select-Object -Unique).Count) -Expected 4 -Context 'Unique installed icon file IDs'
        foreach ($definition in @('WorkspacesImagesComponentFilesPath', 'WorkspacesWinUIImagesComponentFilesPath')) {
            $sourcePath = [regex]::Match($workspaces.OuterXml, "<\?define $definition=([^?]+)\?>").Groups[1].Value
            & $script:assertWorkspacesEqual -Actual $sourcePath -Expected '$(var.BinDir)WinUI3Apps\Assets\Workspaces\' -Context "$definition source directory"
        }
    }
}

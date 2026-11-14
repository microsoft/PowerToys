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
        foreach ($relativePath in @(
            'installer\PowerToysSetupCustomActionsVNext\CustomAction.cpp',
            'tools\BugReportTool\BugReportTool\ProcessesList.cpp'
        )) {
            $source = Get-Content -LiteralPath (Join-Path $script:workspacesRepoRoot $relativePath) -Raw
            $entries = @([regex]::Matches($source, 'L"([^"\r\n]*WorkspacesEditor\.exe)"'))

            & $script:assertWorkspacesEqual -Actual $entries.Count -Expected 1 -Context "$relativePath process entry count"
            & $script:assertWorkspacesEqual -Actual $entries[0].Groups[1].Value -Expected 'PowerToys.WorkspacesEditor.exe' -Context "$relativePath process basename"
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
    }

    It 'harvests the WinUI output for both installed asset locations without an old root output' {
        $fixtureRoot = Join-Path $TestDrive 'workspace'
        $fixtureInstallerRoot = Join-Path $fixtureRoot 'installer\PowerToysSetupVNext'
        $fixtureBuildPlatform = 'x64'
        $assetsOutput = Join-Path $fixtureRoot 'x64\Release\WinUI3Apps\Assets\Workspaces'
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
        $helpers = @($generator.FindAll({
            param($node)
            $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and
                $node.Name -in @('Generate-FileList', 'Generate-FileComponents')
        }, $true))
        & $script:assertWorkspacesEqual -Actual $helpers.Count -Expected 2 -Context 'Extracted harvesting helper count'

        # Run only the file-harvesting helpers and Workspaces block against the fixture.
        # The remaining generator and its build output are never executed or changed.
        $generatorText = Get-Content -LiteralPath $generatorPath -Raw
        $start = $generatorText.IndexOf('#Workspaces')
        $end = $generatorText.IndexOf('#DSC Resources', $start)
        & $script:assertWorkspacesEqual -Actual ($start -ge 0 -and $end -gt $start) -Expected $true -Context 'Workspaces harvesting block boundaries'
        $workspacesBlock = $generatorText.Substring($start, $end - $start).
            Replace('$PSScriptRoot', '$fixtureInstallerRoot').
            Replace('$platform', '$fixtureBuildPlatform')
        $fixtureHarvest = [scriptblock]::Create(($helpers.Extent.Text -join "`n") + "`n" + $workspacesBlock)
        & $fixtureHarvest

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

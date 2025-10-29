[CmdletBinding()]
Param(
    [Parameter(Mandatory = $True, Position = 1)]
    [string]$platform,
    [Parameter(Mandatory = $False, Position = 2)]
    [string]$installscopeperuser = "false"
)

Function Generate-FileList() {
    [CmdletBinding()]
    Param(
        # Can be multiple files separated by ; as long as they're on the same directory
        [Parameter(Mandatory = $True, Position = 1)]
        [AllowEmptyString()]
        [string]$fileDepsJson,
        [Parameter(Mandatory = $True, Position = 2)]
        [string]$fileListName,
        [Parameter(Mandatory = $True, Position = 3)]
        [string]$wxsFilePath,
        # If there is no deps.json file, just pass path to files
        [Parameter(Mandatory = $False, Position = 4)]
        [string]$depsPath,
        # launcher plugins are being loaded into launcher process,
        # so there are some additional dependencies to skip
        [Parameter(Mandatory = $False, Position = 5)]
        [bool]$isLauncherPlugin
    )

    $fileWxs = Get-Content $wxsFilePath;

    $fileExclusionList = @("*.pdb", "*.lastcodeanalysissucceeded", "createdump.exe", "powertoys.exe")

    $fileInclusionList = @("*.dll", "*.exe", "*.json", "*.msix", "*.png", "*.gif", "*.ico", "*.cur", "*.svg", "index.html", "reg.js", "gitignore.js", "srt.js", "monacoSpecialLanguages.js", "customTokenThemeRules.js", "*.pri")

    $dllsToIgnore = @("System.CodeDom.dll", "WindowsBase.dll")

    if ($fileDepsJson -eq [string]::Empty) {
        $fileDepsRoot = $depsPath
    } else {
        $multipleDepsJson = $fileDepsJson.Split(";")

        foreach ( $singleDepsJson in $multipleDepsJson )
        {

            $fileDepsRoot = (Get-ChildItem $singleDepsJson).Directory.FullName
            $depsJson = Get-Content $singleDepsJson | ConvertFrom-Json

            $runtimeList = ([array]$depsJson.targets.PSObject.Properties)[-1].Value.PSObject.Properties | Where-Object {
                $_.Name -match "runtimepack.*Runtime"
            };

            $runtimeList | ForEach-Object {
                $_.Value.PSObject.Properties.Value | ForEach-Object {
                    $fileExclusionList += $_.PSObject.Properties.Name
                }
            }
        }
    }

    $fileExclusionList = $fileExclusionList | Where-Object {$_ -notin $dllsToIgnore}

    if ($isLauncherPlugin -eq $True) {
        $fileInclusionList += @("*.deps.json")
        $fileExclusionList += @("Ijwhost.dll", "PowerToys.Common.UI.dll", "PowerToys.GPOWrapper.dll", "PowerToys.GPOWrapperProjection.dll", "PowerToys.PowerLauncher.Telemetry.dll", "PowerToys.ManagedCommon.dll", "PowerToys.Settings.UI.Lib.dll", "Wox.Infrastructure.dll", "Wox.Plugin.dll")
    }

    $fileList = Get-ChildItem $fileDepsRoot -Include $fileInclusionList -Exclude $fileExclusionList -File -Name

    $fileWxs = $fileWxs -replace "(<\?define $($fileListName)=)", "<?define $fileListName=$($fileList -join ';')"

    Set-Content -Path $wxsFilePath -Value $fileWxs
}

Function Generate-FileComponents() {
    [CmdletBinding()]
    Param(
        [Parameter(Mandatory = $True, Position = 1)]
        [string]$fileListName,
        [Parameter(Mandatory = $True, Position = 2)]
        [string]$wxsFilePath,
        [Parameter(Mandatory = $True, Position = 3)]
        [string]$regroot
    )

    $wxsFile = Get-Content $wxsFilePath;

    $wxsFile | ForEach-Object {
        if ($_ -match "(<?define $fileListName=)(.*)\?>") {
            [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseDeclaredVarsMoreThanAssignments', 'fileList',
            Justification = 'variable is used in another scope')]

            $fileList = $matches[2] -split ';'
            return
        }
    }

    $componentId = "$($fileListName)_Component"

    $componentDefs = "`r`n"
    $componentDefs +=
    @"
            <Component Id="$($componentId)" Guid="$((New-Guid).ToString().ToUpper())">
              <RegistryKey Root="$($regroot)" Key="Software\Classes\powertoys\components">
                <RegistryValue Type="string" Name="$($componentId)" Value="" KeyPath="yes"/>
              </RegistryKey>`r`n
"@

    foreach ($file in $fileList) {
        $fileTmp = $file -replace "-", "_"
        $componentDefs +=
    @"
              <File Id="$($fileListName)_File_$($fileTmp)" Source="`$(var.$($fileListName)Path)\$($file)" />`r`n
"@
    }

    $componentDefs +=
    @"
            </Component>`r`n
"@

    $wxsFile = $wxsFile -replace "\s+(<!--$($fileListName)_Component_Def-->)", $componentDefs

    $componentRef =
    @"
            <ComponentRef Id="$($componentId)" />
"@

    $wxsFile = $wxsFile -replace "\s+(</ComponentGroup>)", "$componentRef`r`n    </ComponentGroup>"

    Set-Content -Path $wxsFilePath -Value $wxsFile
}

if ($platform -ceq "arm64") {
    $platform = "ARM64"
}

if ($installscopeperuser -eq "true") {
    $registryroot = "HKCU"
} else {
    $registryroot = "HKLM"
}

#BaseApplications
Generate-FileList -fileDepsJson "" -fileListName BaseApplicationsFiles -wxsFilePath $PSScriptRoot\BaseApplications.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release"
Generate-FileComponents -fileListName "BaseApplicationsFiles" -wxsFilePath $PSScriptRoot\BaseApplications.wxs -regroot $registryroot

#WinUI3Applications
Generate-FileList -fileDepsJson "" -fileListName WinUI3ApplicationsFiles -wxsFilePath $PSScriptRoot\WinUI3Applications.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\WinUI3Apps"
Generate-FileComponents -fileListName "WinUI3ApplicationsFiles" -wxsFilePath $PSScriptRoot\WinUI3Applications.wxs -regroot $registryroot

#AdvancedPaste
Generate-FileList -fileDepsJson "" -fileListName AdvancedPasteAssetsFiles -wxsFilePath $PSScriptRoot\AdvancedPaste.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\WinUI3Apps\Assets\AdvancedPaste"
Generate-FileComponents -fileListName "AdvancedPasteAssetsFiles" -wxsFilePath $PSScriptRoot\AdvancedPaste.wxs -regroot $registryroot

#AwakeFiles
Generate-FileList -fileDepsJson "" -fileListName AwakeImagesFiles -wxsFilePath $PSScriptRoot\Awake.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\Assets\Awake"
Generate-FileComponents -fileListName "AwakeImagesFiles" -wxsFilePath $PSScriptRoot\Awake.wxs -regroot $registryroot

#ColorPicker
Generate-FileList -fileDepsJson "" -fileListName ColorPickerAssetsFiles -wxsFilePath $PSScriptRoot\ColorPicker.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\Assets\ColorPicker"
Generate-FileComponents -fileListName "ColorPickerAssetsFiles" -wxsFilePath $PSScriptRoot\ColorPicker.wxs -regroot $registryroot

#Environment Variables
Generate-FileList -fileDepsJson "" -fileListName EnvironmentVariablesAssetsFiles -wxsFilePath $PSScriptRoot\EnvironmentVariables.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\WinUI3Apps\Assets\EnvironmentVariables"
Generate-FileComponents -fileListName "EnvironmentVariablesAssetsFiles" -wxsFilePath $PSScriptRoot\EnvironmentVariables.wxs -regroot $registryroot

#FileExplorerAdd-ons
Generate-FileList -fileDepsJson "" -fileListName MonacoPreviewHandlerMonacoAssetsFiles -wxsFilePath $PSScriptRoot\FileExplorerPreview.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\Assets\Monaco"
Generate-FileList -fileDepsJson "" -fileListName MonacoPreviewHandlerCustomLanguagesFiles -wxsFilePath $PSScriptRoot\FileExplorerPreview.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\Assets\Monaco\customLanguages"
Generate-FileComponents -fileListName "MonacoPreviewHandlerMonacoAssetsFiles" -wxsFilePath $PSScriptRoot\FileExplorerPreview.wxs -regroot $registryroot
Generate-FileComponents -fileListName "MonacoPreviewHandlerCustomLanguagesFiles" -wxsFilePath $PSScriptRoot\FileExplorerPreview.wxs -regroot $registryroot

#FileLocksmith
Generate-FileList -fileDepsJson "" -fileListName FileLocksmithAssetsFiles -wxsFilePath $PSScriptRoot\FileLocksmith.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\WinUI3Apps\Assets\FileLocksmith"
Generate-FileComponents -fileListName "FileLocksmithAssetsFiles" -wxsFilePath $PSScriptRoot\FileLocksmith.wxs -regroot $registryroot

#Hosts
Generate-FileList -fileDepsJson "" -fileListName HostsAssetsFiles -wxsFilePath $PSScriptRoot\Hosts.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\WinUI3Apps\Assets\Hosts"
Generate-FileComponents -fileListName "HostsAssetsFiles" -wxsFilePath $PSScriptRoot\Hosts.wxs -regroot $registryroot

#ImageResizer
Generate-FileList -fileDepsJson "" -fileListName ImageResizerAssetsFiles -wxsFilePath $PSScriptRoot\ImageResizer.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\WinUI3Apps\Assets\ImageResizer"
Generate-FileComponents -fileListName "ImageResizerAssetsFiles" -wxsFilePath $PSScriptRoot\ImageResizer.wxs -regroot $registryroot

# Light Switch Service
Generate-FileList -fileDepsJson "" -fileListName LightSwitchFiles -wxsFilePath $PSScriptRoot\LightSwitch.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\LightSwitchService"
Generate-FileComponents -fileListName "LightSwitchFiles" -wxsFilePath $PSScriptRoot\LightSwitch.wxs -regroot $registryroot

#New+
Generate-FileList -fileDepsJson "" -fileListName NewPlusAssetsFiles -wxsFilePath $PSScriptRoot\NewPlus.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\WinUI3Apps\Assets\NewPlus"
Generate-FileComponents -fileListName "NewPlusAssetsFiles" -wxsFilePath $PSScriptRoot\NewPlus.wxs -regroot $registryroot

#Peek
Generate-FileList -fileDepsJson "" -fileListName PeekAssetsFiles -wxsFilePath $PSScriptRoot\Peek.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\WinUI3Apps\Assets\Peek\"
Generate-FileComponents -fileListName "PeekAssetsFiles" -wxsFilePath $PSScriptRoot\Peek.wxs -regroot $registryroot

#PowerRename
Generate-FileList -fileDepsJson "" -fileListName PowerRenameAssetsFiles -wxsFilePath $PSScriptRoot\PowerRename.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\WinUI3Apps\Assets\PowerRename\"
Generate-FileComponents -fileListName "PowerRenameAssetsFiles" -wxsFilePath $PSScriptRoot\PowerRename.wxs -regroot $registryroot

#RegistryPreview
Generate-FileList -fileDepsJson "" -fileListName RegistryPreviewAssetsFiles -wxsFilePath $PSScriptRoot\RegistryPreview.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\WinUI3Apps\Assets\RegistryPreview\"
Generate-FileComponents -fileListName "RegistryPreviewAssetsFiles" -wxsFilePath $PSScriptRoot\RegistryPreview.wxs -regroot $registryroot

#Run
Generate-FileList -fileDepsJson "" -fileListName launcherImagesComponentFiles -wxsFilePath $PSScriptRoot\Run.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\Assets\PowerLauncher"
Generate-FileComponents -fileListName "launcherImagesComponentFiles" -wxsFilePath $PSScriptRoot\Run.wxs -regroot $registryroot
## Plugins
###Calculator
Generate-FileList -fileDepsJson "$PSScriptRoot..\..\..\$platform\Release\RunPlugins\Calculator\Microsoft.PowerToys.Run.Plugin.Calculator.deps.json" -fileListName calcComponentFiles -wxsFilePath $PSScriptRoot\Run.wxs -isLauncherPlugin 1
Generate-FileList -fileDepsJson "" -fileListName calcImagesComponentFiles -wxsFilePath $PSScriptRoot\Run.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\RunPlugins\Calculator\Images"
Generate-FileComponents -fileListName "calcComponentFiles" -wxsFilePath $PSScriptRoot\Run.wxs -regroot $registryroot
Generate-FileComponents -fileListName "calcImagesComponentFiles" -wxsFilePath $PSScriptRoot\Run.wxs -regroot $registryroot
###Folder
Generate-FileList -fileDepsJson "$PSScriptRoot..\..\..\$platform\Release\RunPlugins\Folder\Microsoft.Plugin.Folder.deps.json" -fileListName FolderComponentFiles -wxsFilePath $PSScriptRoot\Run.wxs -isLauncherPlugin 1
Generate-FileList -fileDepsJson "" -fileListName FolderImagesComponentFiles -wxsFilePath $PSScriptRoot\Run.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\RunPlugins\Folder\Images"
Generate-FileComponents -fileListName "FolderComponentFiles" -wxsFilePath $PSScriptRoot\Run.wxs -regroot $registryroot
Generate-FileComponents -fileListName "FolderImagesComponentFiles" -wxsFilePath $PSScriptRoot\Run.wxs -regroot $registryroot
###Program
Generate-FileList -fileDepsJson "$PSScriptRoot..\..\..\$platform\Release\RunPlugins\Program\Microsoft.Plugin.Program.deps.json" -fileListName ProgramComponentFiles -wxsFilePath $PSScriptRoot\Run.wxs -isLauncherPlugin 1
Generate-FileList -fileDepsJson "" -fileListName ProgramImagesComponentFiles -wxsFilePath $PSScriptRoot\Run.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\RunPlugins\Program\Images"
Generate-FileComponents -fileListName "ProgramComponentFiles" -wxsFilePath $PSScriptRoot\Run.wxs -regroot $registryroot
Generate-FileComponents -fileListName "ProgramImagesComponentFiles" -wxsFilePath $PSScriptRoot\Run.wxs -regroot $registryroot
###Shell
Generate-FileList -fileDepsJson "$PSScriptRoot..\..\..\$platform\Release\RunPlugins\Shell\Microsoft.Plugin.Shell.deps.json" -fileListName ShellComponentFiles -wxsFilePath $PSScriptRoot\Run.wxs -isLauncherPlugin 1
Generate-FileList -fileDepsJson "" -fileListName ShellImagesComponentFiles -wxsFilePath $PSScriptRoot\Run.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\RunPlugins\Shell\Images"
Generate-FileComponents -fileListName "ShellComponentFiles" -wxsFilePath $PSScriptRoot\Run.wxs -regroot $registryroot
Generate-FileComponents -fileListName "ShellImagesComponentFiles" -wxsFilePath $PSScriptRoot\Run.wxs -regroot $registryroot
###Indexer
Generate-FileList -fileDepsJson "$PSScriptRoot..\..\..\$platform\Release\RunPlugins\Indexer\Microsoft.Plugin.Indexer.deps.json" -fileListName IndexerComponentFiles -wxsFilePath $PSScriptRoot\Run.wxs -isLauncherPlugin 1
Generate-FileList -fileDepsJson "" -fileListName IndexerImagesComponentFiles -wxsFilePath $PSScriptRoot\Run.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\RunPlugins\Indexer\Images"
Generate-FileComponents -fileListName "IndexerComponentFiles" -wxsFilePath $PSScriptRoot\Run.wxs -regroot $registryroot
Generate-FileComponents -fileListName "IndexerImagesComponentFiles" -wxsFilePath $PSScriptRoot\Run.wxs -regroot $registryroot
###UnitConverter
Generate-FileList -fileDepsJson "$PSScriptRoot..\..\..\$platform\Release\RunPlugins\UnitConverter\Community.PowerToys.Run.Plugin.UnitConverter.deps.json" -fileListName UnitConvCompFiles -wxsFilePath $PSScriptRoot\Run.wxs -isLauncherPlugin 1
Generate-FileList -fileDepsJson "" -fileListName UnitConvImagesCompFiles -wxsFilePath $PSScriptRoot\Run.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\RunPlugins\UnitConverter\Images"
Generate-FileComponents -fileListName "UnitConvCompFiles" -wxsFilePath $PSScriptRoot\Run.wxs -regroot $registryroot
Generate-FileComponents -fileListName "UnitConvImagesCompFiles" -wxsFilePath $PSScriptRoot\Run.wxs -regroot $registryroot
###WebSearch
Generate-FileList -fileDepsJson "$PSScriptRoot..\..\..\$platform\Release\RunPlugins\WebSearch\Community.PowerToys.Run.Plugin.WebSearch.deps.json" -fileListName WebSrchCompFiles -wxsFilePath $PSScriptRoot\Run.wxs -isLauncherPlugin 1
Generate-FileList -fileDepsJson "" -fileListName WebSrchImagesCompFiles -wxsFilePath $PSScriptRoot\Run.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\RunPlugins\WebSearch\Images"
Generate-FileComponents -fileListName "WebSrchCompFiles" -wxsFilePath $PSScriptRoot\Run.wxs -regroot $registryroot
Generate-FileComponents -fileListName "WebSrchImagesCompFiles" -wxsFilePath $PSScriptRoot\Run.wxs -regroot $registryroot
###History
Generate-FileList -fileDepsJson "$PSScriptRoot..\..\..\$platform\Release\RunPlugins\History\Microsoft.PowerToys.Run.Plugin.History.deps.json" -fileListName HistoryPluginComponentFiles -wxsFilePath $PSScriptRoot\Run.wxs -isLauncherPlugin 1
Generate-FileList -fileDepsJson "" -fileListName HistoryPluginImagesComponentFiles -wxsFilePath $PSScriptRoot\Run.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\RunPlugins\History\Images"
Generate-FileComponents -fileListName "HistoryPluginComponentFiles" -wxsFilePath $PSScriptRoot\Run.wxs -regroot $registryroot
Generate-FileComponents -fileListName "HistoryPluginImagesComponentFiles" -wxsFilePath $PSScriptRoot\Run.wxs -regroot $registryroot
###Uri
Generate-FileList -fileDepsJson "$PSScriptRoot..\..\..\$platform\Release\RunPlugins\Uri\Microsoft.Plugin.Uri.deps.json" -fileListName UriComponentFiles -wxsFilePath $PSScriptRoot\Run.wxs -isLauncherPlugin 1
Generate-FileList -fileDepsJson "" -fileListName UriImagesComponentFiles -wxsFilePath $PSScriptRoot\Run.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\RunPlugins\Uri\Images"
Generate-FileComponents -fileListName "UriComponentFiles" -wxsFilePath $PSScriptRoot\Run.wxs -regroot $registryroot
Generate-FileComponents -fileListName "UriImagesComponentFiles" -wxsFilePath $PSScriptRoot\Run.wxs -regroot $registryroot
###VSCodeWorkspaces
Generate-FileList -fileDepsJson "$PSScriptRoot..\..\..\$platform\Release\RunPlugins\VSCodeWorkspaces\Community.PowerToys.Run.Plugin.VSCodeWorkspaces.deps.json" -fileListName VSCWrkCompFiles -wxsFilePath $PSScriptRoot\Run.wxs -isLauncherPlugin 1
Generate-FileList -fileDepsJson "" -fileListName VSCWrkImagesCompFiles -wxsFilePath $PSScriptRoot\Run.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\RunPlugins\VSCodeWorkspaces\Images"
Generate-FileComponents -fileListName "VSCWrkCompFiles" -wxsFilePath $PSScriptRoot\Run.wxs -regroot $registryroot
Generate-FileComponents -fileListName "VSCWrkImagesCompFiles" -wxsFilePath $PSScriptRoot\Run.wxs -regroot $registryroot
###WindowWalker
Generate-FileList -fileDepsJson "$PSScriptRoot..\..\..\$platform\Release\RunPlugins\WindowWalker\Microsoft.Plugin.WindowWalker.deps.json" -fileListName WindowWlkrCompFiles -wxsFilePath $PSScriptRoot\Run.wxs -isLauncherPlugin 1
Generate-FileList -fileDepsJson "" -fileListName WindowWlkrImagesCompFiles -wxsFilePath $PSScriptRoot\Run.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\RunPlugins\WindowWalker\Images"
Generate-FileComponents -fileListName "WindowWlkrCompFiles" -wxsFilePath $PSScriptRoot\Run.wxs -regroot $registryroot
Generate-FileComponents -fileListName "WindowWlkrImagesCompFiles" -wxsFilePath $PSScriptRoot\Run.wxs -regroot $registryroot
###OneNote
Generate-FileList -fileDepsJson "$PSScriptRoot..\..\..\$platform\Release\RunPlugins\OneNote\Microsoft.PowerToys.Run.Plugin.OneNote.deps.json" -fileListName OneNoteComponentFiles -wxsFilePath $PSScriptRoot\Run.wxs -isLauncherPlugin 1
Generate-FileList -fileDepsJson "" -fileListName OneNoteImagesComponentFiles -wxsFilePath $PSScriptRoot\Run.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\RunPlugins\OneNote\Images"
Generate-FileComponents -fileListName "OneNoteComponentFiles" -wxsFilePath $PSScriptRoot\Run.wxs -regroot $registryroot
Generate-FileComponents -fileListName "OneNoteImagesComponentFiles" -wxsFilePath $PSScriptRoot\Run.wxs -regroot $registryroot
###Registry
Generate-FileList -fileDepsJson "$PSScriptRoot..\..\..\$platform\Release\RunPlugins\Registry\Microsoft.PowerToys.Run.Plugin.Registry.deps.json" -fileListName RegistryComponentFiles -wxsFilePath $PSScriptRoot\Run.wxs -isLauncherPlugin 1
Generate-FileList -fileDepsJson "" -fileListName RegistryImagesComponentFiles -wxsFilePath $PSScriptRoot\Run.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\RunPlugins\Registry\Images"
Generate-FileComponents -fileListName "RegistryComponentFiles" -wxsFilePath $PSScriptRoot\Run.wxs -regroot $registryroot
Generate-FileComponents -fileListName "RegistryImagesComponentFiles" -wxsFilePath $PSScriptRoot\Run.wxs -regroot $registryroot
###Service
Generate-FileList -fileDepsJson "$PSScriptRoot..\..\..\$platform\Release\RunPlugins\Service\Microsoft.PowerToys.Run.Plugin.Service.deps.json" -fileListName ServiceComponentFiles -wxsFilePath $PSScriptRoot\Run.wxs -isLauncherPlugin 1
Generate-FileList -fileDepsJson "" -fileListName ServiceImagesComponentFiles -wxsFilePath $PSScriptRoot\Run.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\RunPlugins\Service\Images"
Generate-FileComponents -fileListName "ServiceComponentFiles" -wxsFilePath $PSScriptRoot\Run.wxs -regroot $registryroot
Generate-FileComponents -fileListName "ServiceImagesComponentFiles" -wxsFilePath $PSScriptRoot\Run.wxs -regroot $registryroot
###System
Generate-FileList -fileDepsJson "$PSScriptRoot..\..\..\$platform\Release\RunPlugins\System\Microsoft.PowerToys.Run.Plugin.System.deps.json" -fileListName SystemComponentFiles -wxsFilePath $PSScriptRoot\Run.wxs -isLauncherPlugin 1
Generate-FileList -fileDepsJson "" -fileListName SystemImagesComponentFiles -wxsFilePath $PSScriptRoot\Run.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\RunPlugins\System\Images"
Generate-FileComponents -fileListName "SystemComponentFiles" -wxsFilePath $PSScriptRoot\Run.wxs -regroot $registryroot
Generate-FileComponents -fileListName "SystemImagesComponentFiles" -wxsFilePath $PSScriptRoot\Run.wxs -regroot $registryroot
###TimeDate
Generate-FileList -fileDepsJson "$PSScriptRoot..\..\..\$platform\Release\RunPlugins\TimeDate\Microsoft.PowerToys.Run.Plugin.TimeDate.deps.json" -fileListName TimeDateComponentFiles -wxsFilePath $PSScriptRoot\Run.wxs -isLauncherPlugin 1
Generate-FileList -fileDepsJson "" -fileListName TimeDateImagesComponentFiles -wxsFilePath $PSScriptRoot\Run.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\RunPlugins\TimeDate\Images"
Generate-FileComponents -fileListName "TimeDateComponentFiles" -wxsFilePath $PSScriptRoot\Run.wxs -regroot $registryroot
Generate-FileComponents -fileListName "TimeDateImagesComponentFiles" -wxsFilePath $PSScriptRoot\Run.wxs -regroot $registryroot
###WindowsSettings
Generate-FileList -fileDepsJson "$PSScriptRoot..\..\..\$platform\Release\RunPlugins\WindowsSettings\Microsoft.PowerToys.Run.Plugin.WindowsSettings.deps.json" -fileListName WinSetCmpFiles -wxsFilePath $PSScriptRoot\Run.wxs -isLauncherPlugin 1
Generate-FileList -fileDepsJson "" -fileListName WinSetImagesCmpFiles -wxsFilePath $PSScriptRoot\Run.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\RunPlugins\WindowsSettings\Images"
Generate-FileComponents -fileListName "WinSetCmpFiles" -wxsFilePath $PSScriptRoot\Run.wxs -regroot $registryroot
Generate-FileComponents -fileListName "WinSetImagesCmpFiles" -wxsFilePath $PSScriptRoot\Run.wxs -regroot $registryroot
###WindowsTerminal
Generate-FileList -fileDepsJson "$PSScriptRoot..\..\..\$platform\Release\RunPlugins\WindowsTerminal\Microsoft.PowerToys.Run.Plugin.WindowsTerminal.deps.json" -fileListName WinTermCmpFiles -wxsFilePath $PSScriptRoot\Run.wxs -isLauncherPlugin 1
Generate-FileList -fileDepsJson "" -fileListName WinTermImagesCmpFiles -wxsFilePath $PSScriptRoot\Run.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\RunPlugins\WindowsTerminal\Images"
Generate-FileComponents -fileListName "WinTermCmpFiles" -wxsFilePath $PSScriptRoot\Run.wxs -regroot $registryroot
Generate-FileComponents -fileListName "WinTermImagesCmpFiles" -wxsFilePath $PSScriptRoot\Run.wxs -regroot $registryroot
###PowerToys
Generate-FileList -fileDepsJson "$PSScriptRoot..\..\..\$platform\Release\RunPlugins\PowerToys\Microsoft.PowerToys.Run.Plugin.PowerToys.deps.json" -fileListName PowerToysCmpFiles -wxsFilePath $PSScriptRoot\Run.wxs -isLauncherPlugin 1
Generate-FileList -fileDepsJson "" -fileListName PowerToysImagesCmpFiles -wxsFilePath $PSScriptRoot\Run.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\RunPlugins\PowerToys\Images"
Generate-FileComponents -fileListName "PowerToysCmpFiles" -wxsFilePath $PSScriptRoot\Run.wxs -regroot $registryroot
Generate-FileComponents -fileListName "PowerToysImagesCmpFiles" -wxsFilePath $PSScriptRoot\Run.wxs -regroot $registryroot
###ValueGenerator
Generate-FileList -fileDepsJson "$PSScriptRoot..\..\..\$platform\Release\RunPlugins\ValueGenerator\Community.PowerToys.Run.Plugin.ValueGenerator.deps.json" -fileListName ValueGeneratorCmpFiles -wxsFilePath $PSScriptRoot\Run.wxs -isLauncherPlugin 1
Generate-FileList -fileDepsJson "" -fileListName ValueGeneratorImagesCmpFiles -wxsFilePath $PSScriptRoot\Run.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\RunPlugins\ValueGenerator\Images"
Generate-FileComponents -fileListName "ValueGeneratorCmpFiles" -wxsFilePath $PSScriptRoot\Run.wxs -regroot $registryroot
Generate-FileComponents -fileListName "ValueGeneratorImagesCmpFiles" -wxsFilePath $PSScriptRoot\Run.wxs -regroot $registryroot
## Plugins

#ShortcutGuide
Generate-FileList -fileDepsJson "" -fileListName ShortcutGuideSvgFiles -wxsFilePath $PSScriptRoot\ShortcutGuide.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\Assets\ShortcutGuide\"
Generate-FileComponents -fileListName "ShortcutGuideSvgFiles" -wxsFilePath $PSScriptRoot\ShortcutGuide.wxs -regroot $registryroot

#Settings
Generate-FileList -fileDepsJson "" -fileListName SettingsV2AssetsFiles -wxsFilePath $PSScriptRoot\Settings.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\WinUI3Apps\Assets\Settings\"
Generate-FileList -fileDepsJson "" -fileListName SettingsV2AssetsModulesFiles -wxsFilePath $PSScriptRoot\Settings.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\WinUI3Apps\Assets\Settings\Modules\"
Generate-FileList -fileDepsJson "" -fileListName SettingsV2OOBEAssetsModulesFiles -wxsFilePath $PSScriptRoot\Settings.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\WinUI3Apps\Assets\Settings\Modules\OOBE\"
Generate-FileList -fileDepsJson "" -fileListName SettingsV2OOBEAssetsFluentIconsFiles -wxsFilePath $PSScriptRoot\Settings.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\WinUI3Apps\Assets\Settings\Icons\"
Generate-FileComponents -fileListName "SettingsV2AssetsFiles" -wxsFilePath $PSScriptRoot\Settings.wxs -regroot $registryroot
Generate-FileComponents -fileListName "SettingsV2AssetsModulesFiles" -wxsFilePath $PSScriptRoot\Settings.wxs -regroot $registryroot
Generate-FileComponents -fileListName "SettingsV2OOBEAssetsModulesFiles" -wxsFilePath $PSScriptRoot\Settings.wxs -regroot $registryroot
Generate-FileComponents -fileListName "SettingsV2OOBEAssetsFluentIconsFiles" -wxsFilePath $PSScriptRoot\Settings.wxs -regroot $registryroot

#Workspaces
Generate-FileList -fileDepsJson "" -fileListName WorkspacesImagesComponentFiles -wxsFilePath $PSScriptRoot\Workspaces.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\Assets\Workspaces\"
Generate-FileComponents -fileListName "WorkspacesImagesComponentFiles" -wxsFilePath $PSScriptRoot\Workspaces.wxs -regroot $registryroot

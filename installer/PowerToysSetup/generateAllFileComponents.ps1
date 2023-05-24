[CmdletBinding()]
Param(
    [Parameter(Mandatory = $True, Position = 1)]
    [string]$platform,
    [Parameter(Mandatory = $False, Position = 2)]
    [string]$installscopeperuser = "false"
)

if ($platform -ceq "arm64") {
    $platform = "ARM64"
}

if ($installscopeperuser -eq "true") {
    $registryroot = "HKCU"
} else {
    $registryroot = "HKLM"
}

#AlwaysOnTop
Invoke-Expression -Command "$PSScriptRoot\generateFileList.ps1 -fileDepsJson """" -fileListName AlwaysOnTopFiles -wxsFilePath $PSScriptRoot\AlwaysOnTop.wxs -depsPath ""$PSScriptRoot..\..\..\$platform\Release\modules\AlwaysOnTop"""
Invoke-Expression -Command "$PSScriptRoot\generateFileComponents.ps1 -fileListName ""AlwaysOnTopFiles"" -wxsFilePath $PSScriptRoot\AlwaysOnTop.wxs -regroot $registryroot"

#AwakeFiles
Invoke-Expression -Command "$PSScriptRoot\generateFileList.ps1 -fileDepsJson ""$PSScriptRoot..\..\..\$platform\Release\modules\Awake\PowerToys.Awake.deps.json"" -fileListName AwakeFiles -wxsFilePath $PSScriptRoot\Awake.wxs"
Invoke-Expression -Command "$PSScriptRoot\generateFileList.ps1 -fileDepsJson """" -fileListName AwakeImagesFiles -wxsFilePath $PSScriptRoot\Awake.wxs -depsPath ""$PSScriptRoot..\..\..\$platform\Release\modules\Awake\Images"""
Invoke-Expression -Command "$PSScriptRoot\generateFileComponents.ps1 -fileListName ""AwakeFiles"" -wxsFilePath $PSScriptRoot\Awake.wxs -regroot $registryroot"
Invoke-Expression -Command "$PSScriptRoot\generateFileComponents.ps1 -fileListName ""AwakeImagesFiles"" -wxsFilePath $PSScriptRoot\Awake.wxs -regroot $registryroot"

#ColorPicker
Invoke-Expression -Command "$PSScriptRoot\generateFileList.ps1 -fileDepsJson ""$PSScriptRoot..\..\..\$platform\Release\modules\ColorPicker\PowerToys.ColorPickerUI.deps.json"" -fileListName ColorPickerFiles -wxsFilePath $PSScriptRoot\ColorPicker.wxs"
Invoke-Expression -Command "$PSScriptRoot\generateFileList.ps1 -fileDepsJson """" -fileListName ColorPickerResourcesFiles -wxsFilePath $PSScriptRoot\ColorPicker.wxs -depsPath ""$PSScriptRoot..\..\..\$platform\Release\modules\ColorPicker\Resources"""
Invoke-Expression -Command "$PSScriptRoot\generateFileComponents.ps1 -fileListName ""ColorPickerFiles"" -wxsFilePath $PSScriptRoot\ColorPicker.wxs -regroot $registryroot"
Invoke-Expression -Command "$PSScriptRoot\generateFileComponents.ps1 -fileListName ""ColorPickerResourcesFiles"" -wxsFilePath $PSScriptRoot\ColorPicker.wxs -regroot $registryroot"

Invoke-Expression -Command "$PSScriptRoot\generateFileList.ps1 -fileDepsJson ""$PSScriptRoot..\..\..\$platform\Release\modules\ColorPicker\PowerToys.ColorPickerUI.deps.json"" -fileListName ColorPickerFiles -wxsFilePath $PSScriptRoot\ColorPicker.wxs"
Invoke-Expression -Command "$PSScriptRoot\generateFileComponents.ps1 -fileListName ""ColorPickerFiles"" -wxsFilePath $PSScriptRoot\ColorPicker.wxs -regroot $registryroot"

#FancyZones
Invoke-Expression -Command "$PSScriptRoot\generateFileList.ps1 -fileDepsJson ""$PSScriptRoot..\..\..\$platform\Release\modules\FancyZones\PowerToys.FancyZonesEditor.deps.json"" -fileListName FancyZonesFiles -wxsFilePath $PSScriptRoot\FancyZones.wxs"
Invoke-Expression -Command "$PSScriptRoot\generateFileComponents.ps1 -fileListName ""FancyZonesFiles"" -wxsFilePath $PSScriptRoot\FancyZones.wxs -regroot $registryroot"

#FileExplorerAdd-ons
#TODO: There are multiple .deps.json files, make sure it works as expected
Invoke-Expression -Command "$PSScriptRoot\generateFileList.ps1 -fileDepsJson ""$PSScriptRoot..\..\..\$platform\Release\modules\FileExplorerPreview\PowerToys.SvgThumbnailProvider.deps.json"" -fileListName PowerPreviewFiles -wxsFilePath $PSScriptRoot\FileExplorerPreview.wxs"
Invoke-Expression -Command "$PSScriptRoot\generateFileList.ps1 -fileDepsJson """" -fileListName MonacoPreviewHandlerCustomLanguagesFiles -wxsFilePath $PSScriptRoot\FileExplorerPreview.wxs -depsPath ""$PSScriptRoot..\..\..\$platform\Release\modules\FileExplorerPreview\customLanguages"""
Invoke-Expression -Command "$PSScriptRoot\generateFileComponents.ps1 -fileListName ""PowerPreviewFiles"" -wxsFilePath $PSScriptRoot\FileExplorerPreview.wxs -regroot $registryroot"
Invoke-Expression -Command "$PSScriptRoot\generateFileComponents.ps1 -fileListName ""MonacoPreviewHandlerCustomLanguagesFiles"" -wxsFilePath $PSScriptRoot\FileExplorerPreview.wxs -regroot $registryroot"

#FileLocksmith
#TODO: There are multiple .deps.json files, make sure it works as expected
Invoke-Expression -Command "$PSScriptRoot\generateFileList.ps1 -fileDepsJson ""$PSScriptRoot..\..\..\$platform\Release\modules\FileLocksmith\PowerToys.FileLocksmithUI.deps.json"" -fileListName FileLocksmithFiles -wxsFilePath $PSScriptRoot\FileLocksmith.wxs -isWinAppSdkProj 1"
Invoke-Expression -Command "$PSScriptRoot\generateFileList.ps1 -fileDepsJson """" -fileListName FileLocksmithAssetsFiles -wxsFilePath $PSScriptRoot\FileLocksmith.wxs -depsPath ""$PSScriptRoot..\..\..\$platform\Release\modules\FileLocksmith\Assets\"""
Invoke-Expression -Command "$PSScriptRoot\generateFileComponents.ps1 -fileListName ""FileLocksmithFiles"" -wxsFilePath $PSScriptRoot\FileLocksmith.wxs -regroot $registryroot"
Invoke-Expression -Command "$PSScriptRoot\generateFileComponents.ps1 -fileListName ""FileLocksmithAssetsFiles"" -wxsFilePath $PSScriptRoot\FileLocksmith.wxs -regroot $registryroot"

#Hosts
Invoke-Expression -Command "$PSScriptRoot\generateFileList.ps1 -fileDepsJson ""$PSScriptRoot..\..\..\$platform\Release\modules\Hosts\PowerToys.Hosts.deps.json"" -fileListName HostsFiles -wxsFilePath $PSScriptRoot\Hosts.wxs -isWinAppSdkProj 1"
Invoke-Expression -Command "$PSScriptRoot\generateFileList.ps1 -fileDepsJson """" -fileListName HostsAssetsFiles -wxsFilePath $PSScriptRoot\Hosts.wxs -depsPath ""$PSScriptRoot..\..\..\$platform\Release\modules\Hosts\Assets\"""
Invoke-Expression -Command "$PSScriptRoot\generateFileComponents.ps1 -fileListName ""HostsFiles"" -wxsFilePath $PSScriptRoot\Hosts.wxs -regroot $registryroot"
Invoke-Expression -Command "$PSScriptRoot\generateFileComponents.ps1 -fileListName ""HostsAssetsFiles"" -wxsFilePath $PSScriptRoot\Hosts.wxs -regroot $registryroot"

#ImageResizer
Invoke-Expression -Command "$PSScriptRoot\generateFileList.ps1 -fileDepsJson ""$PSScriptRoot..\..\..\$platform\Release\modules\ImageResizer\PowerToys.ImageResizer.deps.json"" -fileListName ImageResizerFiles -wxsFilePath $PSScriptRoot\ImageResizer.wxs"
Invoke-Expression -Command "$PSScriptRoot\generateFileList.ps1 -fileDepsJson """" -fileListName ImageResizerAssetsFiles -wxsFilePath $PSScriptRoot\ImageResizer.wxs -depsPath ""$PSScriptRoot..\..\..\$platform\Release\modules\ImageResizer\Assets\"""
Invoke-Expression -Command "$PSScriptRoot\generateFileComponents.ps1 -fileListName ""ImageResizerFiles"" -wxsFilePath $PSScriptRoot\ImageResizer.wxs -regroot $registryroot"
Invoke-Expression -Command "$PSScriptRoot\generateFileComponents.ps1 -fileListName ""ImageResizerAssetsFiles"" -wxsFilePath $PSScriptRoot\ImageResizer.wxs -regroot $registryroot"

#MouseUtils
Invoke-Expression -Command "$PSScriptRoot\generateFileList.ps1 -fileDepsJson """" -fileListName MouseUtilsFiles -wxsFilePath $PSScriptRoot\MouseUtils.wxs -depsPath ""$PSScriptRoot..\..\..\$platform\Release\modules\MouseUtils\"""
Invoke-Expression -Command "$PSScriptRoot\generateFileComponents.ps1 -fileListName ""MouseUtilsFiles"" -wxsFilePath $PSScriptRoot\MouseUtils.wxs -regroot $registryroot"
#MouseJumpUI
Invoke-Expression -Command "$PSScriptRoot\generateFileList.ps1 -fileDepsJson ""$PSScriptRoot..\..\..\$platform\Release\modules\MouseUtils\MouseJumpUI\PowerToys.MouseJumpUI.deps.json"" -fileListName MouseJumpUIFiles -wxsFilePath $PSScriptRoot\MouseUtils.wxs"
Invoke-Expression -Command "$PSScriptRoot\generateFileComponents.ps1 -fileListName ""MouseJumpUIFiles"" -wxsFilePath $PSScriptRoot\MouseUtils.wxs -regroot $registryroot"

#MouseWithoutBorders
Invoke-Expression -Command "$PSScriptRoot\generateFileList.ps1 -fileDepsJson ""$PSScriptRoot..\..\..\$platform\Release\modules\MouseWithoutBorders\PowerToys.MouseWithoutBorders.deps.json;$PSScriptRoot..\..\..\$platform\Release\modules\MouseWithoutBorders\PowerToys.MouseWithoutBordersHelper.deps.json;$PSScriptRoot..\..\..\$platform\Release\modules\MouseWithoutBorders\PowerToys.MouseWithoutBordersService.deps.json"" -fileListName MouseWithoutBordersFiles -wxsFilePath $PSScriptRoot\MouseWithoutBorders.wxs"
Invoke-Expression -Command "$PSScriptRoot\generateFileComponents.ps1 -fileListName ""MouseWithoutBordersFiles"" -wxsFilePath $PSScriptRoot\MouseWithoutBorders.wxs -regroot $registryroot"

#MeasureTool
Invoke-Expression -Command "$PSScriptRoot\generateFileList.ps1 -fileDepsJson ""$PSScriptRoot..\..\..\$platform\Release\modules\MeasureTool\PowerToys.MeasureToolUI.deps.json"" -fileListName MeasureToolFiles -wxsFilePath $PSScriptRoot\MeasureTool.wxs -isWinAppSdkProj 1"
Invoke-Expression -Command "$PSScriptRoot\generateFileComponents.ps1 -fileListName ""MeasureToolFiles"" -wxsFilePath $PSScriptRoot\MeasureTool.wxs -regroot $registryroot"

#Peek
Invoke-Expression -Command "$PSScriptRoot\generateFileList.ps1 -fileDepsJson ""$PSScriptRoot..\..\..\$platform\Release\modules\Peek\PowerToys.Peek.UI.deps.json"" -fileListName PeekFiles -wxsFilePath $PSScriptRoot\Peek.wxs"
Invoke-Expression -Command "$PSScriptRoot\generateFileList.ps1 -fileDepsJson """" -fileListName PeekAssetsFiles -wxsFilePath $PSScriptRoot\Peek.wxs -depsPath ""$PSScriptRoot..\..\..\$platform\Release\modules\Peek\Assets\"""
Invoke-Expression -Command "$PSScriptRoot\generateFileComponents.ps1 -fileListName ""PeekFiles"" -wxsFilePath $PSScriptRoot\Peek.wxs -regroot $registryroot"
Invoke-Expression -Command "$PSScriptRoot\generateFileComponents.ps1 -fileListName ""PeekAssetsFiles"" -wxsFilePath $PSScriptRoot\Peek.wxs -regroot $registryroot"

#PowerAccent
Invoke-Expression -Command "$PSScriptRoot\generateFileList.ps1 -fileDepsJson ""$PSScriptRoot..\..\..\$platform\Release\modules\PowerAccent\PowerToys.PowerAccent.deps.json"" -fileListName PowerAccentFiles -wxsFilePath $PSScriptRoot\PowerAccent.wxs"
Invoke-Expression -Command "$PSScriptRoot\generateFileComponents.ps1 -fileListName ""PowerAccentFiles"" -wxsFilePath $PSScriptRoot\PowerAccent.wxs -regroot $registryroot"

#PowerRename
Invoke-Expression -Command "$PSScriptRoot\generateFileList.ps1 -fileDepsJson """" -fileListName PowerRenameFiles -wxsFilePath $PSScriptRoot\PowerRename.wxs -depsPath ""$PSScriptRoot..\..\..\$platform\Release\modules\PowerRename\"" -isWinAppSdkProj 1"
Invoke-Expression -Command "$PSScriptRoot\generateFileList.ps1 -fileDepsJson """" -fileListName PowerRenameAssetsFiles -wxsFilePath $PSScriptRoot\PowerRename.wxs -depsPath ""$PSScriptRoot..\..\..\$platform\Release\modules\PowerRename\Assets\"""
Invoke-Expression -Command "$PSScriptRoot\generateFileComponents.ps1 -fileListName ""PowerRenameFiles"" -wxsFilePath $PSScriptRoot\PowerRename.wxs -regroot $registryroot"
Invoke-Expression -Command "$PSScriptRoot\generateFileComponents.ps1 -fileListName ""PowerRenameAssetsFiles"" -wxsFilePath $PSScriptRoot\PowerRename.wxs -regroot $registryroot"

#RegistryPreview
Invoke-Expression -Command "$PSScriptRoot\generateFileList.ps1 -fileDepsJson ""$PSScriptRoot..\..\..\$platform\Release\modules\RegistryPreview\PowerToys.RegistryPreview.deps.json"" -fileListName RegistryPreviewFiles -wxsFilePath $PSScriptRoot\RegistryPreview.wxs"
Invoke-Expression -Command "$PSScriptRoot\generateFileList.ps1 -fileDepsJson """" -fileListName RegistryPreviewAssetsFiles -wxsFilePath $PSScriptRoot\RegistryPreview.wxs -depsPath ""$PSScriptRoot..\..\..\$platform\Release\modules\RegistryPreview\Assets\"""
Invoke-Expression -Command "$PSScriptRoot\generateFileComponents.ps1 -fileListName ""RegistryPreviewFiles"" -wxsFilePath $PSScriptRoot\RegistryPreview.wxs -regroot $registryroot"
Invoke-Expression -Command "$PSScriptRoot\generateFileComponents.ps1 -fileListName ""RegistryPreviewAssetsFiles"" -wxsFilePath $PSScriptRoot\RegistryPreview.wxs -regroot $registryroot"

#Run
Invoke-Expression -Command "$PSScriptRoot\generateFileList.ps1 -fileDepsJson ""$PSScriptRoot..\..\..\$platform\Release\modules\launcher\PowerToys.PowerLauncher.deps.json"" -fileListName launcherFiles -wxsFilePath $PSScriptRoot\Run.wxs"
Invoke-Expression -Command "$PSScriptRoot\generateFileList.ps1 -fileDepsJson """" -fileListName launcherImagesComponentFiles -wxsFilePath $PSScriptRoot\Run.wxs -depsPath ""$PSScriptRoot..\..\..\$platform\Release\modules\launcher\Images"""
Invoke-Expression -Command "$PSScriptRoot\generateFileComponents.ps1 -fileListName ""launcherFiles"" -wxsFilePath $PSScriptRoot\Run.wxs -regroot $registryroot"
Invoke-Expression -Command "$PSScriptRoot\generateFileComponents.ps1 -fileListName ""launcherImagesComponentFiles"" -wxsFilePath $PSScriptRoot\Run.wxs -regroot $registryroot"
## Plugins
###Calculator
Invoke-Expression -Command "$PSScriptRoot\generateFileList.ps1 -fileDepsJson ""$PSScriptRoot..\..\..\$platform\Release\modules\launcher\Plugins\Calculator\Microsoft.PowerToys.Run.Plugin.Calculator.deps.json"" -fileListName calcComponentFiles -wxsFilePath $PSScriptRoot\Run.wxs -isLauncherPlugin 1"
Invoke-Expression -Command "$PSScriptRoot\generateFileList.ps1 -fileDepsJson """" -fileListName calcImagesComponentFiles -wxsFilePath $PSScriptRoot\Run.wxs -depsPath ""$PSScriptRoot..\..\..\$platform\Release\modules\launcher\Plugins\Calculator\Images"""
Invoke-Expression -Command "$PSScriptRoot\generateFileComponents.ps1 -fileListName ""calcComponentFiles"" -wxsFilePath $PSScriptRoot\Run.wxs -regroot $registryroot"
Invoke-Expression -Command "$PSScriptRoot\generateFileComponents.ps1 -fileListName ""calcImagesComponentFiles"" -wxsFilePath $PSScriptRoot\Run.wxs -regroot $registryroot"
###Folder
Invoke-Expression -Command "$PSScriptRoot\generateFileList.ps1 -fileDepsJson ""$PSScriptRoot..\..\..\$platform\Release\modules\launcher\Plugins\Folder\Microsoft.Plugin.Folder.deps.json"" -fileListName FolderComponentFiles -wxsFilePath $PSScriptRoot\Run.wxs -isLauncherPlugin 1"
Invoke-Expression -Command "$PSScriptRoot\generateFileList.ps1 -fileDepsJson """" -fileListName FolderImagesComponentFiles -wxsFilePath $PSScriptRoot\Run.wxs -depsPath ""$PSScriptRoot..\..\..\$platform\Release\modules\launcher\Plugins\Folder\Images"""
Invoke-Expression -Command "$PSScriptRoot\generateFileComponents.ps1 -fileListName ""FolderComponentFiles"" -wxsFilePath $PSScriptRoot\Run.wxs -regroot $registryroot"
Invoke-Expression -Command "$PSScriptRoot\generateFileComponents.ps1 -fileListName ""FolderImagesComponentFiles"" -wxsFilePath $PSScriptRoot\Run.wxs -regroot $registryroot"
###Program
Invoke-Expression -Command "$PSScriptRoot\generateFileList.ps1 -fileDepsJson ""$PSScriptRoot..\..\..\$platform\Release\modules\launcher\Plugins\Program\Microsoft.Plugin.Program.deps.json"" -fileListName ProgramComponentFiles -wxsFilePath $PSScriptRoot\Run.wxs -isLauncherPlugin 1"
Invoke-Expression -Command "$PSScriptRoot\generateFileList.ps1 -fileDepsJson """" -fileListName ProgramImagesComponentFiles -wxsFilePath $PSScriptRoot\Run.wxs -depsPath ""$PSScriptRoot..\..\..\$platform\Release\modules\launcher\Plugins\Program\Images"""
Invoke-Expression -Command "$PSScriptRoot\generateFileComponents.ps1 -fileListName ""ProgramComponentFiles"" -wxsFilePath $PSScriptRoot\Run.wxs -regroot $registryroot"
Invoke-Expression -Command "$PSScriptRoot\generateFileComponents.ps1 -fileListName ""ProgramImagesComponentFiles"" -wxsFilePath $PSScriptRoot\Run.wxs -regroot $registryroot"
###Shell
Invoke-Expression -Command "$PSScriptRoot\generateFileList.ps1 -fileDepsJson ""$PSScriptRoot..\..\..\$platform\Release\modules\launcher\Plugins\Shell\Microsoft.Plugin.Shell.deps.json"" -fileListName ShellComponentFiles -wxsFilePath $PSScriptRoot\Run.wxs -isLauncherPlugin 1"
Invoke-Expression -Command "$PSScriptRoot\generateFileList.ps1 -fileDepsJson """" -fileListName ShellImagesComponentFiles -wxsFilePath $PSScriptRoot\Run.wxs -depsPath ""$PSScriptRoot..\..\..\$platform\Release\modules\launcher\Plugins\Shell\Images"""
Invoke-Expression -Command "$PSScriptRoot\generateFileComponents.ps1 -fileListName ""ShellComponentFiles"" -wxsFilePath $PSScriptRoot\Run.wxs -regroot $registryroot"
Invoke-Expression -Command "$PSScriptRoot\generateFileComponents.ps1 -fileListName ""ShellImagesComponentFiles"" -wxsFilePath $PSScriptRoot\Run.wxs -regroot $registryroot"
###Indexer
Invoke-Expression -Command "$PSScriptRoot\generateFileList.ps1 -fileDepsJson ""$PSScriptRoot..\..\..\$platform\Release\modules\launcher\Plugins\Indexer\Microsoft.Plugin.Indexer.deps.json"" -fileListName IndexerComponentFiles -wxsFilePath $PSScriptRoot\Run.wxs -isLauncherPlugin 1"
Invoke-Expression -Command "$PSScriptRoot\generateFileList.ps1 -fileDepsJson """" -fileListName IndexerImagesComponentFiles -wxsFilePath $PSScriptRoot\Run.wxs -depsPath ""$PSScriptRoot..\..\..\$platform\Release\modules\launcher\Plugins\Indexer\Images"""
Invoke-Expression -Command "$PSScriptRoot\generateFileComponents.ps1 -fileListName ""IndexerComponentFiles"" -wxsFilePath $PSScriptRoot\Run.wxs -regroot $registryroot"
Invoke-Expression -Command "$PSScriptRoot\generateFileComponents.ps1 -fileListName ""IndexerImagesComponentFiles"" -wxsFilePath $PSScriptRoot\Run.wxs -regroot $registryroot"
###UnitConverter
Invoke-Expression -Command "$PSScriptRoot\generateFileList.ps1 -fileDepsJson ""$PSScriptRoot..\..\..\$platform\Release\modules\launcher\Plugins\UnitConverter\Community.PowerToys.Run.Plugin.UnitConverter.deps.json"" -fileListName UnitConvCompFiles -wxsFilePath $PSScriptRoot\Run.wxs -isLauncherPlugin 1"
Invoke-Expression -Command "$PSScriptRoot\generateFileList.ps1 -fileDepsJson """" -fileListName UnitConvImagesCompFiles -wxsFilePath $PSScriptRoot\Run.wxs -depsPath ""$PSScriptRoot..\..\..\$platform\Release\modules\launcher\Plugins\UnitConverter\Images"""
Invoke-Expression -Command "$PSScriptRoot\generateFileComponents.ps1 -fileListName ""UnitConvCompFiles"" -wxsFilePath $PSScriptRoot\Run.wxs -regroot $registryroot"
Invoke-Expression -Command "$PSScriptRoot\generateFileComponents.ps1 -fileListName ""UnitConvImagesCompFiles"" -wxsFilePath $PSScriptRoot\Run.wxs -regroot $registryroot"
###WebSearch
Invoke-Expression -Command "$PSScriptRoot\generateFileList.ps1 -fileDepsJson ""$PSScriptRoot..\..\..\$platform\Release\modules\launcher\Plugins\WebSearch\Community.PowerToys.Run.Plugin.WebSearch.deps.json"" -fileListName WebSrchCompFiles -wxsFilePath $PSScriptRoot\Run.wxs -isLauncherPlugin 1"
Invoke-Expression -Command "$PSScriptRoot\generateFileList.ps1 -fileDepsJson """" -fileListName WebSrchImagesCompFiles -wxsFilePath $PSScriptRoot\Run.wxs -depsPath ""$PSScriptRoot..\..\..\$platform\Release\modules\launcher\Plugins\WebSearch\Images"""
Invoke-Expression -Command "$PSScriptRoot\generateFileComponents.ps1 -fileListName ""WebSrchCompFiles"" -wxsFilePath $PSScriptRoot\Run.wxs -regroot $registryroot"
Invoke-Expression -Command "$PSScriptRoot\generateFileComponents.ps1 -fileListName ""WebSrchImagesCompFiles"" -wxsFilePath $PSScriptRoot\Run.wxs -regroot $registryroot"
###History
Invoke-Expression -Command "$PSScriptRoot\generateFileList.ps1 -fileDepsJson ""$PSScriptRoot..\..\..\$platform\Release\modules\launcher\Plugins\History\Microsoft.PowerToys.Run.Plugin.History.deps.json"" -fileListName HistoryPluginComponentFiles -wxsFilePath $PSScriptRoot\Run.wxs -isLauncherPlugin 1"
Invoke-Expression -Command "$PSScriptRoot\generateFileList.ps1 -fileDepsJson """" -fileListName HistoryPluginImagesComponentFiles -wxsFilePath $PSScriptRoot\Run.wxs -depsPath ""$PSScriptRoot..\..\..\$platform\Release\modules\launcher\Plugins\History\Images"""
Invoke-Expression -Command "$PSScriptRoot\generateFileComponents.ps1 -fileListName ""HistoryPluginComponentFiles"" -wxsFilePath $PSScriptRoot\Run.wxs -regroot $registryroot"
Invoke-Expression -Command "$PSScriptRoot\generateFileComponents.ps1 -fileListName ""HistoryPluginImagesComponentFiles"" -wxsFilePath $PSScriptRoot\Run.wxs -regroot $registryroot"
###Uri
Invoke-Expression -Command "$PSScriptRoot\generateFileList.ps1 -fileDepsJson ""$PSScriptRoot..\..\..\$platform\Release\modules\launcher\Plugins\Uri\Microsoft.Plugin.Uri.deps.json"" -fileListName UriComponentFiles -wxsFilePath $PSScriptRoot\Run.wxs -isLauncherPlugin 1"
Invoke-Expression -Command "$PSScriptRoot\generateFileList.ps1 -fileDepsJson """" -fileListName UriImagesComponentFiles -wxsFilePath $PSScriptRoot\Run.wxs -depsPath ""$PSScriptRoot..\..\..\$platform\Release\modules\launcher\Plugins\Uri\Images"""
Invoke-Expression -Command "$PSScriptRoot\generateFileComponents.ps1 -fileListName ""UriComponentFiles"" -wxsFilePath $PSScriptRoot\Run.wxs -regroot $registryroot"
Invoke-Expression -Command "$PSScriptRoot\generateFileComponents.ps1 -fileListName ""UriImagesComponentFiles"" -wxsFilePath $PSScriptRoot\Run.wxs -regroot $registryroot"
###VSCodeWorkspaces
Invoke-Expression -Command "$PSScriptRoot\generateFileList.ps1 -fileDepsJson ""$PSScriptRoot..\..\..\$platform\Release\modules\launcher\Plugins\VSCodeWorkspaces\Community.PowerToys.Run.Plugin.VSCodeWorkspaces.deps.json"" -fileListName VSCWrkCompFiles -wxsFilePath $PSScriptRoot\Run.wxs -isLauncherPlugin 1"
Invoke-Expression -Command "$PSScriptRoot\generateFileList.ps1 -fileDepsJson """" -fileListName VSCWrkImagesCompFiles -wxsFilePath $PSScriptRoot\Run.wxs -depsPath ""$PSScriptRoot..\..\..\$platform\Release\modules\launcher\Plugins\VSCodeWorkspaces\Images"""
Invoke-Expression -Command "$PSScriptRoot\generateFileComponents.ps1 -fileListName ""VSCWrkCompFiles"" -wxsFilePath $PSScriptRoot\Run.wxs -regroot $registryroot"
Invoke-Expression -Command "$PSScriptRoot\generateFileComponents.ps1 -fileListName ""VSCWrkImagesCompFiles"" -wxsFilePath $PSScriptRoot\Run.wxs -regroot $registryroot"
###WindowWalker
Invoke-Expression -Command "$PSScriptRoot\generateFileList.ps1 -fileDepsJson ""$PSScriptRoot..\..\..\$platform\Release\modules\launcher\Plugins\WindowWalker\Microsoft.Plugin.WindowWalker.deps.json"" -fileListName WindowWlkrCompFiles -wxsFilePath $PSScriptRoot\Run.wxs -isLauncherPlugin 1"
Invoke-Expression -Command "$PSScriptRoot\generateFileList.ps1 -fileDepsJson """" -fileListName WindowWlkrImagesCompFiles -wxsFilePath $PSScriptRoot\Run.wxs -depsPath ""$PSScriptRoot..\..\..\$platform\Release\modules\launcher\Plugins\WindowWalker\Images"""
Invoke-Expression -Command "$PSScriptRoot\generateFileComponents.ps1 -fileListName ""WindowWlkrCompFiles"" -wxsFilePath $PSScriptRoot\Run.wxs -regroot $registryroot"
Invoke-Expression -Command "$PSScriptRoot\generateFileComponents.ps1 -fileListName ""WindowWlkrImagesCompFiles"" -wxsFilePath $PSScriptRoot\Run.wxs -regroot $registryroot"
###OneNote
Invoke-Expression -Command "$PSScriptRoot\generateFileList.ps1 -fileDepsJson ""$PSScriptRoot..\..\..\$platform\Release\modules\launcher\Plugins\OneNote\Microsoft.PowerToys.Run.Plugin.OneNote.deps.json"" -fileListName OneNoteComponentFiles -wxsFilePath $PSScriptRoot\Run.wxs -isLauncherPlugin 1"
Invoke-Expression -Command "$PSScriptRoot\generateFileList.ps1 -fileDepsJson """" -fileListName OneNoteImagesComponentFiles -wxsFilePath $PSScriptRoot\Run.wxs -depsPath ""$PSScriptRoot..\..\..\$platform\Release\modules\launcher\Plugins\OneNote\Images"""
Invoke-Expression -Command "$PSScriptRoot\generateFileComponents.ps1 -fileListName ""OneNoteComponentFiles"" -wxsFilePath $PSScriptRoot\Run.wxs -regroot $registryroot"
Invoke-Expression -Command "$PSScriptRoot\generateFileComponents.ps1 -fileListName ""OneNoteImagesComponentFiles"" -wxsFilePath $PSScriptRoot\Run.wxs -regroot $registryroot"
###Registry
Invoke-Expression -Command "$PSScriptRoot\generateFileList.ps1 -fileDepsJson ""$PSScriptRoot..\..\..\$platform\Release\modules\launcher\Plugins\Registry\Microsoft.PowerToys.Run.Plugin.Registry.deps.json"" -fileListName RegistryComponentFiles -wxsFilePath $PSScriptRoot\Run.wxs -isLauncherPlugin 1"
Invoke-Expression -Command "$PSScriptRoot\generateFileList.ps1 -fileDepsJson """" -fileListName RegistryImagesComponentFiles -wxsFilePath $PSScriptRoot\Run.wxs -depsPath ""$PSScriptRoot..\..\..\$platform\Release\modules\launcher\Plugins\Registry\Images"""
Invoke-Expression -Command "$PSScriptRoot\generateFileComponents.ps1 -fileListName ""RegistryComponentFiles"" -wxsFilePath $PSScriptRoot\Run.wxs -regroot $registryroot"
Invoke-Expression -Command "$PSScriptRoot\generateFileComponents.ps1 -fileListName ""RegistryImagesComponentFiles"" -wxsFilePath $PSScriptRoot\Run.wxs -regroot $registryroot"
###Service
Invoke-Expression -Command "$PSScriptRoot\generateFileList.ps1 -fileDepsJson ""$PSScriptRoot..\..\..\$platform\Release\modules\launcher\Plugins\Service\Microsoft.PowerToys.Run.Plugin.Service.deps.json"" -fileListName ServiceComponentFiles -wxsFilePath $PSScriptRoot\Run.wxs -isLauncherPlugin 1"
Invoke-Expression -Command "$PSScriptRoot\generateFileList.ps1 -fileDepsJson """" -fileListName ServiceImagesComponentFiles -wxsFilePath $PSScriptRoot\Run.wxs -depsPath ""$PSScriptRoot..\..\..\$platform\Release\modules\launcher\Plugins\Service\Images"""
Invoke-Expression -Command "$PSScriptRoot\generateFileComponents.ps1 -fileListName ""ServiceComponentFiles"" -wxsFilePath $PSScriptRoot\Run.wxs -regroot $registryroot"
Invoke-Expression -Command "$PSScriptRoot\generateFileComponents.ps1 -fileListName ""ServiceImagesComponentFiles"" -wxsFilePath $PSScriptRoot\Run.wxs -regroot $registryroot"
###System
Invoke-Expression -Command "$PSScriptRoot\generateFileList.ps1 -fileDepsJson ""$PSScriptRoot..\..\..\$platform\Release\modules\launcher\Plugins\System\Microsoft.PowerToys.Run.Plugin.System.deps.json"" -fileListName SystemComponentFiles -wxsFilePath $PSScriptRoot\Run.wxs -isLauncherPlugin 1"
Invoke-Expression -Command "$PSScriptRoot\generateFileList.ps1 -fileDepsJson """" -fileListName SystemImagesComponentFiles -wxsFilePath $PSScriptRoot\Run.wxs -depsPath ""$PSScriptRoot..\..\..\$platform\Release\modules\launcher\Plugins\System\Images"""
Invoke-Expression -Command "$PSScriptRoot\generateFileComponents.ps1 -fileListName ""SystemComponentFiles"" -wxsFilePath $PSScriptRoot\Run.wxs -regroot $registryroot"
Invoke-Expression -Command "$PSScriptRoot\generateFileComponents.ps1 -fileListName ""SystemImagesComponentFiles"" -wxsFilePath $PSScriptRoot\Run.wxs -regroot $registryroot"
###TimeDate
Invoke-Expression -Command "$PSScriptRoot\generateFileList.ps1 -fileDepsJson ""$PSScriptRoot..\..\..\$platform\Release\modules\launcher\Plugins\TimeDate\Microsoft.PowerToys.Run.Plugin.TimeDate.deps.json"" -fileListName TimeDateComponentFiles -wxsFilePath $PSScriptRoot\Run.wxs -isLauncherPlugin 1"
Invoke-Expression -Command "$PSScriptRoot\generateFileList.ps1 -fileDepsJson """" -fileListName TimeDateImagesComponentFiles -wxsFilePath $PSScriptRoot\Run.wxs -depsPath ""$PSScriptRoot..\..\..\$platform\Release\modules\launcher\Plugins\TimeDate\Images"""
Invoke-Expression -Command "$PSScriptRoot\generateFileComponents.ps1 -fileListName ""TimeDateComponentFiles"" -wxsFilePath $PSScriptRoot\Run.wxs -regroot $registryroot"
Invoke-Expression -Command "$PSScriptRoot\generateFileComponents.ps1 -fileListName ""TimeDateImagesComponentFiles"" -wxsFilePath $PSScriptRoot\Run.wxs -regroot $registryroot"
###WindowsSettings
Invoke-Expression -Command "$PSScriptRoot\generateFileList.ps1 -fileDepsJson ""$PSScriptRoot..\..\..\$platform\Release\modules\launcher\Plugins\WindowsSettings\Microsoft.PowerToys.Run.Plugin.WindowsSettings.deps.json"" -fileListName WinSetCmpFiles -wxsFilePath $PSScriptRoot\Run.wxs -isLauncherPlugin 1"
Invoke-Expression -Command "$PSScriptRoot\generateFileList.ps1 -fileDepsJson """" -fileListName WinSetImagesCmpFiles -wxsFilePath $PSScriptRoot\Run.wxs -depsPath ""$PSScriptRoot..\..\..\$platform\Release\modules\launcher\Plugins\WindowsSettings\Images"""
Invoke-Expression -Command "$PSScriptRoot\generateFileComponents.ps1 -fileListName ""WinSetCmpFiles"" -wxsFilePath $PSScriptRoot\Run.wxs -regroot $registryroot"
Invoke-Expression -Command "$PSScriptRoot\generateFileComponents.ps1 -fileListName ""WinSetImagesCmpFiles"" -wxsFilePath $PSScriptRoot\Run.wxs -regroot $registryroot"
###WindowsTerminal
Invoke-Expression -Command "$PSScriptRoot\generateFileList.ps1 -fileDepsJson ""$PSScriptRoot..\..\..\$platform\Release\modules\launcher\Plugins\WindowsTerminal\Microsoft.PowerToys.Run.Plugin.WindowsTerminal.deps.json"" -fileListName WinTermCmpFiles -wxsFilePath $PSScriptRoot\Run.wxs -isLauncherPlugin 1"
Invoke-Expression -Command "$PSScriptRoot\generateFileList.ps1 -fileDepsJson """" -fileListName WinTermImagesCmpFiles -wxsFilePath $PSScriptRoot\Run.wxs -depsPath ""$PSScriptRoot..\..\..\$platform\Release\modules\launcher\Plugins\WindowsTerminal\Images"""
Invoke-Expression -Command "$PSScriptRoot\generateFileComponents.ps1 -fileListName ""WinTermCmpFiles"" -wxsFilePath $PSScriptRoot\Run.wxs -regroot $registryroot"
Invoke-Expression -Command "$PSScriptRoot\generateFileComponents.ps1 -fileListName ""WinTermImagesCmpFiles"" -wxsFilePath $PSScriptRoot\Run.wxs -regroot $registryroot"
## Plugins

#ShortcutGuide
Invoke-Expression -Command "$PSScriptRoot\generateFileList.ps1 -fileDepsJson """" -fileListName ShortcutGuideSvgFiles -wxsFilePath $PSScriptRoot\ShortcutGuide.wxs -depsPath ""$PSScriptRoot..\..\..\$platform\Release\modules\ShortcutGuide\ShortcutGuide\svgs\"""
Invoke-Expression -Command "$PSScriptRoot\generateFileComponents.ps1 -fileListName ""ShortcutGuideSvgFiles"" -wxsFilePath $PSScriptRoot\ShortcutGuide.wxs -regroot $registryroot"

#TextExtractor
Invoke-Expression -Command "$PSScriptRoot\generateFileList.ps1 -fileDepsJson ""$PSScriptRoot..\..\..\$platform\Release\modules\PowerOCR\PowerToys.PowerOCR.deps.json"" -fileListName TextExtractorFiles -wxsFilePath $PSScriptRoot\TextExtractor.wxs"
Invoke-Expression -Command "$PSScriptRoot\generateFileComponents.ps1 -fileListName ""TextExtractorFiles"" -wxsFilePath $PSScriptRoot\TextExtractor.wxs -regroot $registryroot"

#Settings
Invoke-Expression -Command "$PSScriptRoot\generateFileList.ps1 -fileDepsJson ""$PSScriptRoot..\..\..\$platform\Release\Settings\PowerToys.Settings.deps.json"" -fileListName SettingsV2Files -wxsFilePath $PSScriptRoot\Settings.wxs"
Invoke-Expression -Command "$PSScriptRoot\generateFileList.ps1 -fileDepsJson """" -fileListName SettingsV2AssetsFiles -wxsFilePath $PSScriptRoot\Settings.wxs -depsPath ""$PSScriptRoot..\..\..\$platform\Release\Settings\Assets\"""
Invoke-Expression -Command "$PSScriptRoot\generateFileList.ps1 -fileDepsJson """" -fileListName SettingsV2AssetsModulesFiles -wxsFilePath $PSScriptRoot\Settings.wxs -depsPath ""$PSScriptRoot..\..\..\$platform\Release\Settings\Assets\Modules\"""
Invoke-Expression -Command "$PSScriptRoot\generateFileList.ps1 -fileDepsJson """" -fileListName SettingsV2OOBEAssetsModulesFiles -wxsFilePath $PSScriptRoot\Settings.wxs -depsPath ""$PSScriptRoot..\..\..\$platform\Release\Settings\Assets\Modules\OOBE\"""
Invoke-Expression -Command "$PSScriptRoot\generateFileList.ps1 -fileDepsJson """" -fileListName SettingsV2OOBEAssetsFluentIconsFiles -wxsFilePath $PSScriptRoot\Settings.wxs -depsPath ""$PSScriptRoot..\..\..\$platform\Release\Settings\Assets\FluentIcons\"""
Invoke-Expression -Command "$PSScriptRoot\generateFileComponents.ps1 -fileListName ""SettingsV2Files"" -wxsFilePath $PSScriptRoot\Settings.wxs -regroot $registryroot"
Invoke-Expression -Command "$PSScriptRoot\generateFileComponents.ps1 -fileListName ""SettingsV2AssetsFiles"" -wxsFilePath $PSScriptRoot\Settings.wxs -regroot $registryroot"
Invoke-Expression -Command "$PSScriptRoot\generateFileComponents.ps1 -fileListName ""SettingsV2AssetsModulesFiles"" -wxsFilePath $PSScriptRoot\Settings.wxs -regroot $registryroot"
Invoke-Expression -Command "$PSScriptRoot\generateFileComponents.ps1 -fileListName ""SettingsV2OOBEAssetsModulesFiles"" -wxsFilePath $PSScriptRoot\Settings.wxs -regroot $registryroot"
Invoke-Expression -Command "$PSScriptRoot\generateFileComponents.ps1 -fileListName ""SettingsV2OOBEAssetsFluentIconsFiles"" -wxsFilePath $PSScriptRoot\Settings.wxs -regroot $registryroot"

#WinAppSdk
Invoke-Expression -Command "$PSScriptRoot\generateFileComponents.ps1 -fileListName ""WinAppSDKFiles"" -wxsFilePath $PSScriptRoot\WinAppSDK.wxs -regroot $registryroot"

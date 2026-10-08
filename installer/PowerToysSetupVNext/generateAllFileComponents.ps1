[CmdletBinding()]
Param(
    [Parameter(Mandatory = $True, Position = 1)]
    [string]$platform,
    # Where the populated .wxs files are written. The checked-in .wxs files next to this script are
    # templates and are never modified. Defaults to obj\<platform>\Generated, which
    # PowerToysInstallerVNext.wixproj compiles from.
    [Parameter(Mandatory = $False)]
    [string]$outputDir
)

# Fixed namespace for the RFC 4122 version 5 (SHA-1, name-based) GUIDs of generated components.
# Never change it: doing so changes every generated component GUID.
$componentGuidNamespace = [guid]"DA7240B1-6FC9-4C3C-B921-50AC82EE65D1"

Function New-DeterministicGuid() {
    Param(
        [Parameter(Mandatory = $True)]
        [guid]$namespace,
        [Parameter(Mandatory = $True)]
        [string]$name
    )

    # Guid.ToByteArray() stores the first three fields little-endian; RFC 4122 hashes network order.
    $namespaceBytes = $namespace.ToByteArray()
    [Array]::Reverse($namespaceBytes, 0, 4)
    [Array]::Reverse($namespaceBytes, 4, 2)
    [Array]::Reverse($namespaceBytes, 6, 2)

    $sha1 = [System.Security.Cryptography.SHA1]::Create()
    try {
        $hash = $sha1.ComputeHash([byte[]]($namespaceBytes + [System.Text.Encoding]::UTF8.GetBytes($name)))
    } finally {
        $sha1.Dispose()
    }

    $guidBytes = [byte[]]$hash[0..15]
    $guidBytes[6] = ($guidBytes[6] -band 0x0F) -bor 0x50
    $guidBytes[8] = ($guidBytes[8] -band 0x3F) -bor 0x80
    [Array]::Reverse($guidBytes, 0, 4)
    [Array]::Reverse($guidBytes, 4, 2)
    [Array]::Reverse($guidBytes, 6, 2)

    return ([guid]::new($guidBytes)).ToString().ToUpperInvariant()
}

Function Get-ComponentGuid() {
    Param(
        [Parameter(Mandatory = $True)]
        [string]$componentId,
        [Parameter(Mandatory = $True)]
        [string]$scope,
        [Parameter(Mandatory = $True)]
        [string]$installPath,
        [Parameter(Mandatory = $True)]
        [string[]]$fileList
    )

    # Stable across builds, but a different scope (HKCU vs HKLM key path), platform, install
    # directory, or set of files yields a different GUID, so the component rules hold if the
    # component moves to a new directory (even with the same ID and file set) or its file set
    # changes.
    $files = [string[]]($fileList | ForEach-Object { $_.ToLowerInvariant() })
    [Array]::Sort($files, [System.StringComparer]::Ordinal)
    $name = "$componentId|$scope|$platform|$($installPath.ToLowerInvariant())|$($files -join '|')"
    return New-DeterministicGuid -namespace $componentGuidNamespace -name $name
}

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

    # *.winmd: WinRT metadata for the Windows App SDK AI APIs (Phi Silica, Imaging, etc.). The AI
    # runtime resolves these from the app directory at runtime, so they must ship with the product.
    # Without them GetReadyState() reports NotReady and EnsureReadyAsync() fails with
    # RO_E_METADATA_NAME_NOT_FOUND (0x8000000F). The build already emits them into the app output
    # (e.g. WinUI3Apps); they were previously dropped here because the harvest didn't include them.
    $fileInclusionList = @("*.dll", "*.exe", "*.json", "*.msix", "*.png", "*.gif", "*.ico", "*.cur", "*.svg", "index.html", "reg.js", "gitignore.js", "srt.js", "monacoSpecialLanguages.js", "customTokenThemeRules.js", "*.pri", "*.yml", "*.winmd")

    # MFC DLLs leak into the output via WindowsAppSDKSelfContained but no PowerToys binary imports them.
    # Verified with dumpbin /dependents across all 2176 binaries — zero consumers.
    $fileExclusionList += @("mfc140.dll", "mfc140u.dll", "mfcm140.dll", "mfcm140u.dll")

    # Microsoft.CommandPalette.Extensions.winmd already has a dedicated WiX component
    # (Microsoft_CommandPalette_Extensions_winmd in BaseApplications.wxs, placed in WinUI3Apps for
    # CmdPal's WinRT resolution). Exclude it from the generic *.winmd harvest so it isn't declared
    # by two components (WIX ICE30 "installed by two different components" breaks ref-counting).
    $fileExclusionList += @("Microsoft.CommandPalette.Extensions.winmd")

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
        [string]$wxsFilePath
    )

    $wxsFile = Get-Content $wxsFilePath;

    $wxsFile | ForEach-Object {
        if ($_ -match "(<?define $($fileListName)Path=)(.*)\?>") {
            [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseDeclaredVarsMoreThanAssignments', 'installPath',
            Justification = 'variable is used in another scope')]

            $installPath = $matches[2]
            return
        }
        if ($_ -match "(<?define $fileListName=)(.*)\?>") {
            [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseDeclaredVarsMoreThanAssignments', 'fileList',
            Justification = 'variable is used in another scope')]

            $fileList = $matches[2] -split ';' | Where-Object { $_ -ne '' }
            return
        }
    }

    if ($null -eq $fileList -or $fileList.Count -eq 0) {
        # No files to generate components for — leave placeholder intact
        return
    }

    $componentId = "$($fileListName)_Component"
    $componentGuidVar = "$($fileListName)_ComponentGuid"
    # $installPath is the unexpanded <fileListName>Path define (e.g. "$(var.BinDir)WinUI3Apps\Assets\ColorPicker\")
    # rather than its resolved value, but it still uniquely identifies the component's target
    # directory and changes if the component is retargeted to a new path/define.
    $perUserGuid = Get-ComponentGuid -componentId $componentId -scope "perUser" -installPath $installPath -fileList $fileList
    $perMachineGuid = Get-ComponentGuid -componentId $componentId -scope "perMachine" -installPath $installPath -fileList $fileList

    $componentDefs = "`r`n"
    $componentDefs +=
    @"
<?if `$(var.PerUser) = "true" ?>
<?define $($componentGuidVar)="$($perUserGuid)" ?>
<?else?>
<?define $($componentGuidVar)="$($perMachineGuid)" ?>
<?endif?>
            <Component Id="$($componentId)" Guid="`$(var.$($componentGuidVar))">
              <RegistryKey Root="`$(var.RegistryScope)" Key="Software\Classes\powertoys\components">
                <RegistryValue Type="string" Name="$($componentId)" Value="" KeyPath="yes"/>
              </RegistryKey>`r`n
"@

    foreach ($file in $fileList) {
        $fileTmp = $file -replace "-", "_"
        $fileTmp = $fileTmp -replace "[^A-Za-z0-9_.]", "_"
        if ($fileTmp -match "^[^A-Za-z_]") { $fileTmp = "_$fileTmp" }
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

if ([string]::IsNullOrEmpty($outputDir)) {
    $outputDir = "$PSScriptRoot\obj\$platform\Generated"
}

# Every template the generator populates. PowerToysInstallerVNext.wixproj compiles these from $outputDir.
$templateWxsFiles = @(
    "AdvancedPaste.wxs",
    "Awake.wxs",
    "BaseApplications.wxs",
    "ColorPicker.wxs",
    "DscResources.wxs",
    "EnvironmentVariables.wxs",
    "FileExplorerPreview.wxs",
    "FileLocksmith.wxs",
    "Hosts.wxs",
    "ImageResizer.wxs",
    "KeyboardManager.wxs",
    "LightSwitch.wxs",
    "NewPlus.wxs",
    "Peek.wxs",
    "PowerDisplay.wxs",
    "PowerRename.wxs",
    "RegistryPreview.wxs",
    "Run.wxs",
    "Settings.wxs",
    "ShortcutGuide.wxs",
    "WinUI3Applications.wxs",
    "Workspaces.wxs"
)

# Start from fresh copies of the templates on every run so the output only depends on the
# templates and the build output, never on a previous run.
New-Item -Path $outputDir -ItemType Directory -Force | Out-Null
foreach ($templateWxsFile in $templateWxsFiles) {
    Copy-Item -Path "$PSScriptRoot\$templateWxsFile" -Destination "$outputDir\$templateWxsFile" -Force
}

#BaseApplications
# WORKAROUND: Exclude app-host files that leak into the root output directory.
# A SelfContained Exe with a ProjectReference to a SelfContained WinExe makes MSBuild copy the
# referenced WinExe's apphost (.exe, .deps.json, .runtimeconfig.json) to the root output directory
# as a side effect. These files are incomplete (missing the managed .dll) and should not be in the
# installer; the complete app is in WinUI3Apps/ and is handled by WinUI3ApplicationsFiles.
#   - ImageResizer: ImageResizerCLI (Exe) references ImageResizerUI (WinExe).
#   - ColorPicker: ColorPickerUI.UnitTests (Exe) references ColorPickerUI (WinExe). The leaked root
#     PowerToys.ColorPickerUI.exe is also unsigned (ESRP only signs the WinUI3Apps copy), so it must
#     be stripped or the "Verify all binaries are signed and versioned" stage fails.
# TODO: Refactor these to use a shared Library project instead.
Generate-FileList -fileDepsJson "" -fileListName BaseApplicationsFiles -wxsFilePath $outputDir\BaseApplications.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release"

# Remove leaked app-host artifacts from BaseApplications
$baseAppWxsPath = "$outputDir\BaseApplications.wxs"
$baseAppWxs = Get-Content $baseAppWxsPath -Raw
$baseAppWxs = $baseAppWxs -replace 'PowerToys\.ImageResizer\.exe;?', ''
$baseAppWxs = $baseAppWxs -replace 'PowerToys\.ImageResizer\.deps\.json;?', ''
$baseAppWxs = $baseAppWxs -replace 'PowerToys\.ImageResizer\.runtimeconfig\.json;?', ''
$baseAppWxs = $baseAppWxs -replace 'PowerToys\.ColorPickerUI\.exe;?', ''
$baseAppWxs = $baseAppWxs -replace 'PowerToys\.ColorPickerUI\.deps\.json;?', ''
$baseAppWxs = $baseAppWxs -replace 'PowerToys\.ColorPickerUI\.runtimeconfig\.json;?', ''
# Clean up trailing/double semicolons left after removal
$baseAppWxs = $baseAppWxs -replace ';;+', ';'
$baseAppWxs = $baseAppWxs -replace '=;', '='
$baseAppWxs = $baseAppWxs -replace ';"', '"'
Set-Content -Path $baseAppWxsPath -Value $baseAppWxs
Generate-FileComponents -fileListName "BaseApplicationsFiles" -wxsFilePath $outputDir\BaseApplications.wxs

#WinUI3Applications
Generate-FileList -fileDepsJson "" -fileListName WinUI3ApplicationsFiles -wxsFilePath $outputDir\WinUI3Applications.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\WinUI3Apps"

# Deduplicate: Remove files from WinUI3Apps that are identical to root (same name + same hash).
# These will be re-created as plain file copies at install time by CreateWinAppSDKHardlinksCA.
# (The CA's name is historical: it now uses fs::copy_file rather than CreateHardLinkW to avoid
# DACL contamination across the shared inode -- see CustomAction.cpp for details.)
$rootPath = "$PSScriptRoot..\..\..\$platform\Release"
$winui3Path = "$PSScriptRoot..\..\..\$platform\Release\WinUI3Apps"
$winui3WxsPath = "$outputDir\WinUI3Applications.wxs"
$winui3Wxs = Get-Content $winui3WxsPath -Raw
$manifestPath = Join-Path $winui3Path "hardlinks.txt"

if ($winui3Wxs -match "\<\?define WinUI3ApplicationsFiles=([^?]*)\?\>") {
    $winui3FileList = $matches[1] -split ';' | Where-Object { $_ -ne '' }
    $hardlinkFiles = @()

    # Read the BaseApplications WXS file list so we only deduplicate files that the MSI
    # is actually deploying to the install root. If a file was stripped from BaseApplications
    # by an earlier step (e.g., the ImageResizer leaked-apphost workaround above), the
    # install-time CA's source would be missing and both copies would disappear.
    $baseAppsWxs = Get-Content $baseAppWxsPath -Raw
    $baseAppsFileList = @()
    if ($baseAppsWxs -match "\<\?define BaseApplicationsFiles=([^?]*)\?\>") {
        $baseAppsFileList = $matches[1] -split ';' | Where-Object { $_ -ne '' }
    }

    foreach ($file in $winui3FileList) {
        # Skip files that were intentionally not deployed to root by the build
        if ($baseAppsFileList -notcontains $file) { continue }

        $rootFile = Join-Path $rootPath $file
        $winui3File = Join-Path $winui3Path $file
        if ((Test-Path $rootFile) -and (Test-Path $winui3File)) {
            $rootHash = (Get-FileHash $rootFile -Algorithm SHA256).Hash
            $winui3Hash = (Get-FileHash $winui3File -Algorithm SHA256).Hash
            if ($rootHash -eq $winui3Hash) {
                $hardlinkFiles += $file
            }
        }
    }

    if ($hardlinkFiles.Count -gt 0) {
        # Remove deduplicated files from WinUI3Apps file list
        $remainingFiles = $winui3FileList | Where-Object { $_ -notin $hardlinkFiles }
        if ($remainingFiles.Count -eq 0) {
            # All files are duplicates — keep at least a dummy entry won't be emitted
            # Generate-FileComponents handles empty defines by producing no <File> entries
            $winui3Wxs = $winui3Wxs -replace "\<\?define WinUI3ApplicationsFiles=[^?]*\?\>", "<?define WinUI3ApplicationsFiles=?>"
        } else {
            $winui3Wxs = $winui3Wxs -replace "\<\?define WinUI3ApplicationsFiles=[^?]*\?\>", "<?define WinUI3ApplicationsFiles=$($remainingFiles -join ';')?>"
        }
        Set-Content -Path $winui3WxsPath -Value $winui3Wxs
        Write-Host "Deduplicated $($hardlinkFiles.Count) files from WinUI3Apps (will be copied at install time)"
    }

    # Always write hardlinks.txt (may be empty — CA handles that gracefully)
    # Write as UTF-8 without BOM so the install-time CA can read it via std::ifstream
    # + MultiByteToWideChar(CP_UTF8) without dealing with PS-version-dependent default
    # encodings or a leading BOM.
    [System.IO.File]::WriteAllLines($manifestPath, [string[]]$hardlinkFiles, (New-Object System.Text.UTF8Encoding($false)))
}

Generate-FileComponents -fileListName "WinUI3ApplicationsFiles" -wxsFilePath $outputDir\WinUI3Applications.wxs

#AdvancedPaste
Generate-FileList -fileDepsJson "" -fileListName AdvancedPasteAssetsFiles -wxsFilePath $outputDir\AdvancedPaste.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\WinUI3Apps\Assets\AdvancedPaste"
Generate-FileComponents -fileListName "AdvancedPasteAssetsFiles" -wxsFilePath $outputDir\AdvancedPaste.wxs

#AwakeFiles
Generate-FileList -fileDepsJson "" -fileListName AwakeImagesFiles -wxsFilePath $outputDir\Awake.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\Assets\Awake"
Generate-FileComponents -fileListName "AwakeImagesFiles" -wxsFilePath $outputDir\Awake.wxs

#ColorPicker
Generate-FileList -fileDepsJson "" -fileListName ColorPickerAssetsFiles -wxsFilePath $outputDir\ColorPicker.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\WinUI3Apps\Assets\ColorPicker"
Generate-FileComponents -fileListName "ColorPickerAssetsFiles" -wxsFilePath $outputDir\ColorPicker.wxs

#Environment Variables
Generate-FileList -fileDepsJson "" -fileListName EnvironmentVariablesAssetsFiles -wxsFilePath $outputDir\EnvironmentVariables.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\WinUI3Apps\Assets\EnvironmentVariables"
Generate-FileComponents -fileListName "EnvironmentVariablesAssetsFiles" -wxsFilePath $outputDir\EnvironmentVariables.wxs

#FileExplorerAdd-ons
Generate-FileList -fileDepsJson "" -fileListName MonacoPreviewHandlerMonacoAssetsFiles -wxsFilePath $outputDir\FileExplorerPreview.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\Assets\Monaco"
Generate-FileList -fileDepsJson "" -fileListName MonacoPreviewHandlerCustomLanguagesFiles -wxsFilePath $outputDir\FileExplorerPreview.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\Assets\Monaco\customLanguages"
Generate-FileComponents -fileListName "MonacoPreviewHandlerMonacoAssetsFiles" -wxsFilePath $outputDir\FileExplorerPreview.wxs
Generate-FileComponents -fileListName "MonacoPreviewHandlerCustomLanguagesFiles" -wxsFilePath $outputDir\FileExplorerPreview.wxs

#FileLocksmith
Generate-FileList -fileDepsJson "" -fileListName FileLocksmithAssetsFiles -wxsFilePath $outputDir\FileLocksmith.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\WinUI3Apps\Assets\FileLocksmith"
Generate-FileComponents -fileListName "FileLocksmithAssetsFiles" -wxsFilePath $outputDir\FileLocksmith.wxs

#Hosts
Generate-FileList -fileDepsJson "" -fileListName HostsAssetsFiles -wxsFilePath $outputDir\Hosts.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\WinUI3Apps\Assets\Hosts"
Generate-FileComponents -fileListName "HostsAssetsFiles" -wxsFilePath $outputDir\Hosts.wxs

#ImageResizer
Generate-FileList -fileDepsJson "" -fileListName ImageResizerAssetsFiles -wxsFilePath $outputDir\ImageResizer.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\WinUI3Apps\Assets\ImageResizer"
Generate-FileComponents -fileListName "ImageResizerAssetsFiles" -wxsFilePath $outputDir\ImageResizer.wxs

#KeyboardManager
Generate-FileList -fileDepsJson "" -fileListName KeyboardManagerAssetsFiles -wxsFilePath $outputDir\KeyboardManager.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\WinUI3Apps\Assets\KeyboardManager"
Generate-FileList -fileDepsJson "" -fileListName KeyboardManagerAssetsWinUI3Files -wxsFilePath $outputDir\KeyboardManager.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\WinUI3Apps\Assets\KeyboardManagerEditor"
Generate-FileComponents -fileListName "KeyboardManagerAssetsFiles" -wxsFilePath $outputDir\KeyboardManager.wxs
Generate-FileComponents -fileListName "KeyboardManagerAssetsWinUI3Files" -wxsFilePath $outputDir\KeyboardManager.wxs

# Light Switch Service
Generate-FileList -fileDepsJson "" -fileListName LightSwitchFiles -wxsFilePath $outputDir\LightSwitch.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\LightSwitchService"
Generate-FileComponents -fileListName "LightSwitchFiles" -wxsFilePath $outputDir\LightSwitch.wxs

#New+
Generate-FileList -fileDepsJson "" -fileListName NewPlusAssetsFiles -wxsFilePath $outputDir\NewPlus.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\WinUI3Apps\Assets\NewPlus"
Generate-FileComponents -fileListName "NewPlusAssetsFiles" -wxsFilePath $outputDir\NewPlus.wxs

#Peek
Generate-FileList -fileDepsJson "" -fileListName PeekAssetsFiles -wxsFilePath $outputDir\Peek.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\WinUI3Apps\Assets\Peek\"
Generate-FileComponents -fileListName "PeekAssetsFiles" -wxsFilePath $outputDir\Peek.wxs

#PowerRename
Generate-FileList -fileDepsJson "" -fileListName PowerRenameAssetsFiles -wxsFilePath $outputDir\PowerRename.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\WinUI3Apps\Assets\PowerRename\"
Generate-FileComponents -fileListName "PowerRenameAssetsFiles" -wxsFilePath $outputDir\PowerRename.wxs

#PowerDisplay
Generate-FileList -fileDepsJson "" -fileListName PowerDisplayAssetsFiles -wxsFilePath $outputDir\PowerDisplay.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\WinUI3Apps\Assets\PowerDisplay\"
Generate-FileComponents -fileListName "PowerDisplayAssetsFiles" -wxsFilePath $outputDir\PowerDisplay.wxs

#RegistryPreview
Generate-FileList -fileDepsJson "" -fileListName RegistryPreviewAssetsFiles -wxsFilePath $outputDir\RegistryPreview.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\WinUI3Apps\Assets\RegistryPreview\"
Generate-FileComponents -fileListName "RegistryPreviewAssetsFiles" -wxsFilePath $outputDir\RegistryPreview.wxs

#Run
Generate-FileList -fileDepsJson "" -fileListName launcherImagesComponentFiles -wxsFilePath $outputDir\Run.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\Assets\PowerLauncher"
Generate-FileComponents -fileListName "launcherImagesComponentFiles" -wxsFilePath $outputDir\Run.wxs
## Plugins
###Calculator
Generate-FileList -fileDepsJson "$PSScriptRoot..\..\..\$platform\Release\RunPlugins\Calculator\Microsoft.PowerToys.Run.Plugin.Calculator.deps.json" -fileListName calcComponentFiles -wxsFilePath $outputDir\Run.wxs -isLauncherPlugin 1
Generate-FileList -fileDepsJson "" -fileListName calcImagesComponentFiles -wxsFilePath $outputDir\Run.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\RunPlugins\Calculator\Images"
Generate-FileComponents -fileListName "calcComponentFiles" -wxsFilePath $outputDir\Run.wxs
Generate-FileComponents -fileListName "calcImagesComponentFiles" -wxsFilePath $outputDir\Run.wxs
###Folder
Generate-FileList -fileDepsJson "$PSScriptRoot..\..\..\$platform\Release\RunPlugins\Folder\Microsoft.Plugin.Folder.deps.json" -fileListName FolderComponentFiles -wxsFilePath $outputDir\Run.wxs -isLauncherPlugin 1
Generate-FileList -fileDepsJson "" -fileListName FolderImagesComponentFiles -wxsFilePath $outputDir\Run.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\RunPlugins\Folder\Images"
Generate-FileComponents -fileListName "FolderComponentFiles" -wxsFilePath $outputDir\Run.wxs
Generate-FileComponents -fileListName "FolderImagesComponentFiles" -wxsFilePath $outputDir\Run.wxs
###Program
Generate-FileList -fileDepsJson "$PSScriptRoot..\..\..\$platform\Release\RunPlugins\Program\Microsoft.Plugin.Program.deps.json" -fileListName ProgramComponentFiles -wxsFilePath $outputDir\Run.wxs -isLauncherPlugin 1
Generate-FileList -fileDepsJson "" -fileListName ProgramImagesComponentFiles -wxsFilePath $outputDir\Run.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\RunPlugins\Program\Images"
Generate-FileComponents -fileListName "ProgramComponentFiles" -wxsFilePath $outputDir\Run.wxs
Generate-FileComponents -fileListName "ProgramImagesComponentFiles" -wxsFilePath $outputDir\Run.wxs
###Shell
Generate-FileList -fileDepsJson "$PSScriptRoot..\..\..\$platform\Release\RunPlugins\Shell\Microsoft.Plugin.Shell.deps.json" -fileListName ShellComponentFiles -wxsFilePath $outputDir\Run.wxs -isLauncherPlugin 1
Generate-FileList -fileDepsJson "" -fileListName ShellImagesComponentFiles -wxsFilePath $outputDir\Run.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\RunPlugins\Shell\Images"
Generate-FileComponents -fileListName "ShellComponentFiles" -wxsFilePath $outputDir\Run.wxs
Generate-FileComponents -fileListName "ShellImagesComponentFiles" -wxsFilePath $outputDir\Run.wxs
###Indexer
Generate-FileList -fileDepsJson "$PSScriptRoot..\..\..\$platform\Release\RunPlugins\Indexer\Microsoft.Plugin.Indexer.deps.json" -fileListName IndexerComponentFiles -wxsFilePath $outputDir\Run.wxs -isLauncherPlugin 1
Generate-FileList -fileDepsJson "" -fileListName IndexerImagesComponentFiles -wxsFilePath $outputDir\Run.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\RunPlugins\Indexer\Images"
Generate-FileComponents -fileListName "IndexerComponentFiles" -wxsFilePath $outputDir\Run.wxs
Generate-FileComponents -fileListName "IndexerImagesComponentFiles" -wxsFilePath $outputDir\Run.wxs
###UnitConverter
Generate-FileList -fileDepsJson "$PSScriptRoot..\..\..\$platform\Release\RunPlugins\UnitConverter\Community.PowerToys.Run.Plugin.UnitConverter.deps.json" -fileListName UnitConvCompFiles -wxsFilePath $outputDir\Run.wxs -isLauncherPlugin 1
Generate-FileList -fileDepsJson "" -fileListName UnitConvImagesCompFiles -wxsFilePath $outputDir\Run.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\RunPlugins\UnitConverter\Images"
Generate-FileComponents -fileListName "UnitConvCompFiles" -wxsFilePath $outputDir\Run.wxs
Generate-FileComponents -fileListName "UnitConvImagesCompFiles" -wxsFilePath $outputDir\Run.wxs
###WebSearch
Generate-FileList -fileDepsJson "$PSScriptRoot..\..\..\$platform\Release\RunPlugins\WebSearch\Community.PowerToys.Run.Plugin.WebSearch.deps.json" -fileListName WebSrchCompFiles -wxsFilePath $outputDir\Run.wxs -isLauncherPlugin 1
Generate-FileList -fileDepsJson "" -fileListName WebSrchImagesCompFiles -wxsFilePath $outputDir\Run.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\RunPlugins\WebSearch\Images"
Generate-FileComponents -fileListName "WebSrchCompFiles" -wxsFilePath $outputDir\Run.wxs
Generate-FileComponents -fileListName "WebSrchImagesCompFiles" -wxsFilePath $outputDir\Run.wxs
###History
Generate-FileList -fileDepsJson "$PSScriptRoot..\..\..\$platform\Release\RunPlugins\History\Microsoft.PowerToys.Run.Plugin.History.deps.json" -fileListName HistoryPluginComponentFiles -wxsFilePath $outputDir\Run.wxs -isLauncherPlugin 1
Generate-FileList -fileDepsJson "" -fileListName HistoryPluginImagesComponentFiles -wxsFilePath $outputDir\Run.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\RunPlugins\History\Images"
Generate-FileComponents -fileListName "HistoryPluginComponentFiles" -wxsFilePath $outputDir\Run.wxs
Generate-FileComponents -fileListName "HistoryPluginImagesComponentFiles" -wxsFilePath $outputDir\Run.wxs
###Uri
Generate-FileList -fileDepsJson "$PSScriptRoot..\..\..\$platform\Release\RunPlugins\Uri\Microsoft.Plugin.Uri.deps.json" -fileListName UriComponentFiles -wxsFilePath $outputDir\Run.wxs -isLauncherPlugin 1
Generate-FileList -fileDepsJson "" -fileListName UriImagesComponentFiles -wxsFilePath $outputDir\Run.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\RunPlugins\Uri\Images"
Generate-FileComponents -fileListName "UriComponentFiles" -wxsFilePath $outputDir\Run.wxs
Generate-FileComponents -fileListName "UriImagesComponentFiles" -wxsFilePath $outputDir\Run.wxs
###VSCodeWorkspaces
Generate-FileList -fileDepsJson "$PSScriptRoot..\..\..\$platform\Release\RunPlugins\VSCodeWorkspaces\Community.PowerToys.Run.Plugin.VSCodeWorkspaces.deps.json" -fileListName VSCWrkCompFiles -wxsFilePath $outputDir\Run.wxs -isLauncherPlugin 1
Generate-FileList -fileDepsJson "" -fileListName VSCWrkImagesCompFiles -wxsFilePath $outputDir\Run.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\RunPlugins\VSCodeWorkspaces\Images"
Generate-FileComponents -fileListName "VSCWrkCompFiles" -wxsFilePath $outputDir\Run.wxs
Generate-FileComponents -fileListName "VSCWrkImagesCompFiles" -wxsFilePath $outputDir\Run.wxs
###WindowWalker
Generate-FileList -fileDepsJson "$PSScriptRoot..\..\..\$platform\Release\RunPlugins\WindowWalker\Microsoft.Plugin.WindowWalker.deps.json" -fileListName WindowWlkrCompFiles -wxsFilePath $outputDir\Run.wxs -isLauncherPlugin 1
Generate-FileList -fileDepsJson "" -fileListName WindowWlkrImagesCompFiles -wxsFilePath $outputDir\Run.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\RunPlugins\WindowWalker\Images"
Generate-FileComponents -fileListName "WindowWlkrCompFiles" -wxsFilePath $outputDir\Run.wxs
Generate-FileComponents -fileListName "WindowWlkrImagesCompFiles" -wxsFilePath $outputDir\Run.wxs
###OneNote
Generate-FileList -fileDepsJson "$PSScriptRoot..\..\..\$platform\Release\RunPlugins\OneNote\Microsoft.PowerToys.Run.Plugin.OneNote.deps.json" -fileListName OneNoteComponentFiles -wxsFilePath $outputDir\Run.wxs -isLauncherPlugin 1
Generate-FileList -fileDepsJson "" -fileListName OneNoteImagesComponentFiles -wxsFilePath $outputDir\Run.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\RunPlugins\OneNote\Images"
Generate-FileComponents -fileListName "OneNoteComponentFiles" -wxsFilePath $outputDir\Run.wxs
Generate-FileComponents -fileListName "OneNoteImagesComponentFiles" -wxsFilePath $outputDir\Run.wxs
###Registry
Generate-FileList -fileDepsJson "$PSScriptRoot..\..\..\$platform\Release\RunPlugins\Registry\Microsoft.PowerToys.Run.Plugin.Registry.deps.json" -fileListName RegistryComponentFiles -wxsFilePath $outputDir\Run.wxs -isLauncherPlugin 1
Generate-FileList -fileDepsJson "" -fileListName RegistryImagesComponentFiles -wxsFilePath $outputDir\Run.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\RunPlugins\Registry\Images"
Generate-FileComponents -fileListName "RegistryComponentFiles" -wxsFilePath $outputDir\Run.wxs
Generate-FileComponents -fileListName "RegistryImagesComponentFiles" -wxsFilePath $outputDir\Run.wxs
###Service
Generate-FileList -fileDepsJson "$PSScriptRoot..\..\..\$platform\Release\RunPlugins\Service\Microsoft.PowerToys.Run.Plugin.Service.deps.json" -fileListName ServiceComponentFiles -wxsFilePath $outputDir\Run.wxs -isLauncherPlugin 1
Generate-FileList -fileDepsJson "" -fileListName ServiceImagesComponentFiles -wxsFilePath $outputDir\Run.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\RunPlugins\Service\Images"
Generate-FileComponents -fileListName "ServiceComponentFiles" -wxsFilePath $outputDir\Run.wxs
Generate-FileComponents -fileListName "ServiceImagesComponentFiles" -wxsFilePath $outputDir\Run.wxs
###System
Generate-FileList -fileDepsJson "$PSScriptRoot..\..\..\$platform\Release\RunPlugins\System\Microsoft.PowerToys.Run.Plugin.System.deps.json" -fileListName SystemComponentFiles -wxsFilePath $outputDir\Run.wxs -isLauncherPlugin 1
Generate-FileList -fileDepsJson "" -fileListName SystemImagesComponentFiles -wxsFilePath $outputDir\Run.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\RunPlugins\System\Images"
Generate-FileComponents -fileListName "SystemComponentFiles" -wxsFilePath $outputDir\Run.wxs
Generate-FileComponents -fileListName "SystemImagesComponentFiles" -wxsFilePath $outputDir\Run.wxs
###TimeDate
Generate-FileList -fileDepsJson "$PSScriptRoot..\..\..\$platform\Release\RunPlugins\TimeDate\Microsoft.PowerToys.Run.Plugin.TimeDate.deps.json" -fileListName TimeDateComponentFiles -wxsFilePath $outputDir\Run.wxs -isLauncherPlugin 1
Generate-FileList -fileDepsJson "" -fileListName TimeDateImagesComponentFiles -wxsFilePath $outputDir\Run.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\RunPlugins\TimeDate\Images"
Generate-FileComponents -fileListName "TimeDateComponentFiles" -wxsFilePath $outputDir\Run.wxs
Generate-FileComponents -fileListName "TimeDateImagesComponentFiles" -wxsFilePath $outputDir\Run.wxs
###WindowsSettings
Generate-FileList -fileDepsJson "$PSScriptRoot..\..\..\$platform\Release\RunPlugins\WindowsSettings\Microsoft.PowerToys.Run.Plugin.WindowsSettings.deps.json" -fileListName WinSetCmpFiles -wxsFilePath $outputDir\Run.wxs -isLauncherPlugin 1
Generate-FileList -fileDepsJson "" -fileListName WinSetImagesCmpFiles -wxsFilePath $outputDir\Run.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\RunPlugins\WindowsSettings\Images"
Generate-FileComponents -fileListName "WinSetCmpFiles" -wxsFilePath $outputDir\Run.wxs
Generate-FileComponents -fileListName "WinSetImagesCmpFiles" -wxsFilePath $outputDir\Run.wxs
###WindowsTerminal
Generate-FileList -fileDepsJson "$PSScriptRoot..\..\..\$platform\Release\RunPlugins\WindowsTerminal\Microsoft.PowerToys.Run.Plugin.WindowsTerminal.deps.json" -fileListName WinTermCmpFiles -wxsFilePath $outputDir\Run.wxs -isLauncherPlugin 1
Generate-FileList -fileDepsJson "" -fileListName WinTermImagesCmpFiles -wxsFilePath $outputDir\Run.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\RunPlugins\WindowsTerminal\Images"
Generate-FileComponents -fileListName "WinTermCmpFiles" -wxsFilePath $outputDir\Run.wxs
Generate-FileComponents -fileListName "WinTermImagesCmpFiles" -wxsFilePath $outputDir\Run.wxs
###PowerToys
Generate-FileList -fileDepsJson "$PSScriptRoot..\..\..\$platform\Release\RunPlugins\PowerToys\Microsoft.PowerToys.Run.Plugin.PowerToys.deps.json" -fileListName PowerToysCmpFiles -wxsFilePath $outputDir\Run.wxs -isLauncherPlugin 1
Generate-FileList -fileDepsJson "" -fileListName PowerToysImagesCmpFiles -wxsFilePath $outputDir\Run.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\RunPlugins\PowerToys\Images"
Generate-FileComponents -fileListName "PowerToysCmpFiles" -wxsFilePath $outputDir\Run.wxs
Generate-FileComponents -fileListName "PowerToysImagesCmpFiles" -wxsFilePath $outputDir\Run.wxs
###ValueGenerator
Generate-FileList -fileDepsJson "$PSScriptRoot..\..\..\$platform\Release\RunPlugins\ValueGenerator\Community.PowerToys.Run.Plugin.ValueGenerator.deps.json" -fileListName ValueGeneratorCmpFiles -wxsFilePath $outputDir\Run.wxs -isLauncherPlugin 1
Generate-FileList -fileDepsJson "" -fileListName ValueGeneratorImagesCmpFiles -wxsFilePath $outputDir\Run.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\RunPlugins\ValueGenerator\Images"
Generate-FileComponents -fileListName "ValueGeneratorCmpFiles" -wxsFilePath $outputDir\Run.wxs
Generate-FileComponents -fileListName "ValueGeneratorImagesCmpFiles" -wxsFilePath $outputDir\Run.wxs
## Plugins

#ShortcutGuide
# Ensure manifest yml files are in the build output (the Build target's CopyToOutputDirectory
# may not run reliably under -graph mode in solution builds).
$sgManifestsSrc = "$PSScriptRoot..\..\..\src\modules\ShortcutGuide\ShortcutGuide.Ui\Assets\ShortcutGuide\Manifests"
$sgManifestsDst = "$PSScriptRoot..\..\..\$platform\Release\WinUI3Apps\Assets\ShortcutGuide\Manifests"
Write-Host "ShortcutGuide manifests: src=$sgManifestsSrc exists=$(Test-Path $sgManifestsSrc)"
Write-Host "ShortcutGuide manifests: dst=$sgManifestsDst exists=$(Test-Path $sgManifestsDst)"
if (Test-Path $sgManifestsSrc) {
    New-Item -Path $sgManifestsDst -ItemType Directory -Force | Out-Null
    Copy-Item "$sgManifestsSrc\*.yml" -Destination $sgManifestsDst -Force
    $copied = (Get-ChildItem "$sgManifestsDst\*.yml" -ErrorAction SilentlyContinue).Count
    Write-Host "ShortcutGuide manifests: copied $copied yml files to build output"
} else {
    Write-Host "WARNING: ShortcutGuide manifest source not found at $sgManifestsSrc"
}
Generate-FileList -fileDepsJson "" -fileListName ShortcutGuideAssetsFiles -wxsFilePath $outputDir\ShortcutGuide.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\WinUI3Apps\Assets\ShortcutGuide\"
Generate-FileComponents -fileListName "ShortcutGuideAssetsFiles" -wxsFilePath $outputDir\ShortcutGuide.wxs
Generate-FileList -fileDepsJson "" -fileListName ShortcutGuideManifestsFiles -wxsFilePath $outputDir\ShortcutGuide.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\WinUI3Apps\Assets\ShortcutGuide\Manifests\"
Generate-FileComponents -fileListName "ShortcutGuideManifestsFiles" -wxsFilePath $outputDir\ShortcutGuide.wxs

#Settings
Generate-FileList -fileDepsJson "" -fileListName SettingsV2AssetsFiles -wxsFilePath $outputDir\Settings.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\WinUI3Apps\Assets\Settings\"
Generate-FileList -fileDepsJson "" -fileListName SettingsV2AssetsModulesFiles -wxsFilePath $outputDir\Settings.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\WinUI3Apps\Assets\Settings\Modules\"
Generate-FileList -fileDepsJson "" -fileListName SettingsV2OOBEAssetsModulesFiles -wxsFilePath $outputDir\Settings.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\WinUI3Apps\Assets\Settings\Modules\OOBE\"
Generate-FileList -fileDepsJson "" -fileListName SettingsV2OOBEAssetsFluentIconsFiles -wxsFilePath $outputDir\Settings.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\WinUI3Apps\Assets\Settings\Icons\"
Generate-FileList -fileDepsJson "" -fileListName SettingsV2IconsModelsFiles -wxsFilePath $outputDir\Settings.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\WinUI3Apps\Assets\Settings\Icons\Models\"
Generate-FileList -fileDepsJson "" -fileListName SettingsV2AssetsCmdPalFiles -wxsFilePath $outputDir\Settings.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\WinUI3Apps\Assets\Settings\CmdPal\"
Generate-FileComponents -fileListName "SettingsV2AssetsFiles" -wxsFilePath $outputDir\Settings.wxs
Generate-FileComponents -fileListName "SettingsV2AssetsModulesFiles" -wxsFilePath $outputDir\Settings.wxs
Generate-FileComponents -fileListName "SettingsV2OOBEAssetsModulesFiles" -wxsFilePath $outputDir\Settings.wxs
Generate-FileComponents -fileListName "SettingsV2OOBEAssetsFluentIconsFiles" -wxsFilePath $outputDir\Settings.wxs
Generate-FileComponents -fileListName "SettingsV2IconsModelsFiles" -wxsFilePath $outputDir\Settings.wxs
Generate-FileComponents -fileListName "SettingsV2AssetsCmdPalFiles" -wxsFilePath $outputDir\Settings.wxs

#Workspaces
Generate-FileList -fileDepsJson "" -fileListName WorkspacesImagesComponentFiles -wxsFilePath $outputDir\Workspaces.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\Assets\Workspaces\"
Generate-FileComponents -fileListName "WorkspacesImagesComponentFiles" -wxsFilePath $outputDir\Workspaces.wxs

#DSC Resources - JSON manifest files in DSCModules subfolder
Generate-FileList -fileDepsJson "" -fileListName DscJsonFiles -wxsFilePath $outputDir\DscResources.wxs -depsPath "$PSScriptRoot..\..\..\$platform\Release\DSCModules\"
Generate-FileComponents -fileListName "DscJsonFiles" -wxsFilePath $outputDir\DscResources.wxs

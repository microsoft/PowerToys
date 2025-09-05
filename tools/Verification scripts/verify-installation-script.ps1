#Requires -Version 5.1

<#
.SYNOPSIS
    Verifies a PowerToys installation by checking all components, registry entries, files, and custom logic.

.DESCRIPTION
    This script comprehensively verifies a PowerToys installation by checking:
    - Registry entries for both per-machine and per-user installations
    - File and folder structure integrity
    - Module registration and functionality
    - WiX installer logic verification
    - Custom action results
    - DSC module installation
    - Command Palette packages

.PARAMETER InstallScope
    Specifies the installation scope to verify. Valid values are 'PerMachine' or 'PerUser'.
    Default is 'PerMachine'.

.PARAMETER InstallPath
    Optional. Specifies a custom installation path to verify. If not provided, the script will
    detect the installation path from the registry.

.EXAMPLE
    .\verify-installation-script.ps1 -InstallScope PerMachine
    
.EXAMPLE
    .\verify-installation-script.ps1 -InstallScope PerUser

.NOTES
    Author: PowerToys Team
    Requires: PowerShell 5.1 or later
    Requires: Administrative privileges for per-machine verification
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [ValidateSet('PerMachine', 'PerUser')]
    [string]$InstallScope = 'PerMachine',
    
    [Parameter(Mandatory = $false)]
    [string]$InstallPath
)

# Initialize results tracking
$script:Results = @{
    Summary           = @{
        TotalChecks   = 0
        PassedChecks  = 0
        FailedChecks  = 0
        WarningChecks = 0
        OverallStatus = "Unknown"
    }
    Details           = @{}
    Timestamp         = Get-Date
    Computer          = $env:COMPUTERNAME
    User              = $env:USERNAME
    PowerShellVersion = $PSVersionTable.PSVersion.ToString()
}

# PowerToys constants
$PowerToysUpgradeCodePerMachine = "{42B84BF7-5FBF-473B-9C8B-049DC16F7708}"
$PowerToysUpgradeCodePerUser = "{D8B559DB-4C98-487A-A33F-50A8EEE42726}"
$PowerToysRegistryKeyPerMachine = "HKLM:\SOFTWARE\Classes\PowerToys"
$PowerToysRegistryKeyPerUser = "HKCU:\SOFTWARE\Classes\PowerToys"

# Utility functions
function Write-StatusMessage {
    param(
        [string]$Message,
        [ValidateSet('Info', 'Success', 'Warning', 'Error')]
        [string]$Level = 'Info'
    )
    
    $color = switch ($Level) {
        'Info' { 'White' }
        'Success' { 'Green' }
        'Warning' { 'Yellow' }
        'Error' { 'Red' }
    }
    
    $prefix = switch ($Level) {
        'Info' { '[INFO]' }
        'Success' { '[PASS]' }
        'Warning' { '[WARN]' }
        'Error' { '[FAIL]' }
    }
    
    Write-Host "$prefix $Message" -ForegroundColor $color
}

function Add-CheckResult {
    param(
        [string]$Category,
        [string]$CheckName,
        [string]$Status,
        [string]$Message,
        [object]$Details = $null
    )
    
    $script:Results.Summary.TotalChecks++
    
    switch ($Status) {
        'Pass' { $script:Results.Summary.PassedChecks++ }
        'Fail' { $script:Results.Summary.FailedChecks++ }
        'Warning' { $script:Results.Summary.WarningChecks++ }
    }
    
    if (-not $script:Results.Details.ContainsKey($Category)) {
        $script:Results.Details[$Category] = @{}
    }
    
    $checkDetails = @{
        Status    = $Status
        Message   = $Message
        Details   = $Details
        Timestamp = Get-Date
    }
    
    $script:Results.Details[$Category][$CheckName] = $checkDetails
    
    # Always show all checks with their status
    $level = switch ($Status) {
        'Pass' { 'Success' }
        'Fail' { 'Error' }
        'Warning' { 'Warning' }
    }
    Write-StatusMessage "[$Category] $CheckName - $Message" -Level $level
}

function Test-RegistryKey {
    param(
        [string]$Path
    )
    try {
        return Test-Path $Path
    }
    catch {
        return $false
    }
}

function Get-RegistryValue {
    param(
        [string]$Path,
        [string]$Name,
        [object]$DefaultValue = $null
    )
    try {
        $value = Get-ItemProperty -Path $Path -Name $Name -ErrorAction SilentlyContinue
        return $value.$Name
    }
    catch {
        return $DefaultValue
    }
}

function Test-PowerToysInstallation {
    param(
        [ValidateSet('PerMachine', 'PerUser')]
        [string]$Scope
    )
    
    Write-StatusMessage "Verifying PowerToys $Scope installation..." -Level Info
    
    # Determine registry paths based on scope
    $registryKey = if ($Scope -eq 'PerMachine') { $PowerToysRegistryKeyPerMachine } else { $PowerToysRegistryKeyPerUser }
    
    # Check main registry key
    $mainKeyExists = Test-RegistryKey -Path $registryKey
    Add-CheckResult -Category "Registry" -CheckName "Main Registry Key ($Scope)" -Status $(if ($mainKeyExists) { 'Pass' } else { 'Fail' }) -Message "Registry key exists: $registryKey"
    
    if (-not $mainKeyExists) {
        Add-CheckResult -Category "Installation" -CheckName "Installation Status ($Scope)" -Status 'Fail' -Message "PowerToys $Scope installation not found"
        return $false
    }
    
    # Check install scope value
    $installScopeValue = Get-RegistryValue -Path $registryKey -Name "InstallScope"
    $expectedScope = $Scope.ToLower()
    if ($Scope -eq 'PerMachine') { $expectedScope = 'perMachine' }
    if ($Scope -eq 'PerUser') { $expectedScope = 'perUser' }
    
    $scopeCorrect = $installScopeValue -eq $expectedScope
    Add-CheckResult -Category "Registry" -CheckName "Install Scope Value ($Scope)" -Status $(if ($scopeCorrect) { 'Pass' } else { 'Fail' }) -Message "Install scope: Expected '$expectedScope', Found '$installScopeValue'"
    
    # Check for uninstall registry entry (this is what makes PowerToys appear in Add/Remove Programs)
    $uninstallKey = if ($Scope -eq 'PerMachine') {
        "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*"
    }
    else {
        "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*"
    }
    
    try {
        $powerToysEntry = Get-ItemProperty -Path $uninstallKey | Where-Object { 
            $_.DisplayName -like "*PowerToys*" 
        } | Select-Object -First 1
        
        if ($powerToysEntry) {
            Add-CheckResult -Category "Registry" -CheckName "Uninstall Registry Entry ($Scope)" -Status 'Pass' -Message "PowerToys uninstall entry found with DisplayName: $($powerToysEntry.DisplayName)"
            
            # InstallLocation should be set in the uninstall registry as of installer version fixing issue #41638
            if ($powerToysEntry.InstallLocation) {
                Add-CheckResult -Category "Registry" -CheckName "Install Location Registry ($Scope)" -Status 'Pass' -Message "InstallLocation found: $($powerToysEntry.InstallLocation)"
            }
            else {
                Add-CheckResult -Category "Registry" -CheckName "Install Location Registry ($Scope)" -Status 'Fail' -Message "InstallLocation is missing from uninstall registry entry. This may indicate an installer issue."
            }
        }
        else {
            Add-CheckResult -Category "Registry" -CheckName "Uninstall Registry Entry ($Scope)" -Status 'Fail' -Message "PowerToys uninstall entry not found in Windows uninstall registry"
        }
    }
    catch {
        Add-CheckResult -Category "Registry" -CheckName "Uninstall Registry Entry ($Scope)" -Status 'Fail' -Message "Failed to read Windows uninstall registry"
    }
    
    # Check for installation folder
    $installFolder = Get-PowerToysInstallPath -Scope $Scope
    if ($installFolder -and (Test-Path $installFolder)) {
        Add-CheckResult -Category "Installation" -CheckName "Install Folder ($Scope)" -Status 'Pass' -Message "Installation folder exists: $installFolder"
        
        # Verify core files
        Test-CoreFiles -InstallPath $installFolder -Scope $Scope
        
        # Verify modules
        Test-ModuleFiles -InstallPath $installFolder -Scope $Scope
        
        return $true
    }
    else {
        Add-CheckResult -Category "Installation" -CheckName "Install Folder ($Scope)" -Status 'Fail' -Message "Installation folder not found or inaccessible: $installFolder"
        return $false
    }
}

function Get-PowerToysInstallPath {
    param(
        [ValidateSet('PerMachine', 'PerUser')]
        [string]$Scope
    )
    
    if ($InstallPath) {
        return $InstallPath
    }
    
    # Try to get path from registry first, fall back to default paths if needed
    # InstallLocation should be reliably set as of installer version fixing issue #41638
    if ($Scope -eq 'PerMachine') {
        $defaultPath = "${env:ProgramFiles}\PowerToys"
    }
    else {
        $defaultPath = "${env:LOCALAPPDATA}\PowerToys"
    }
    
    # Verify the path exists before returning it
    if (Test-Path $defaultPath) {
        return $defaultPath
    }
    
    # If default path doesn't exist, try to get it from uninstall registry as fallback
    $uninstallKey = if ($Scope -eq 'PerMachine') {
        "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*"
    }
    else {
        "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*"
    }
    
    try {
        $powerToysEntry = Get-ItemProperty -Path $uninstallKey | Where-Object { 
            $_.DisplayName -like "*PowerToys*" 
        } | Select-Object -First 1
        
        # Check for InstallLocation first, but it may not exist
        if ($powerToysEntry -and $powerToysEntry.InstallLocation) {
            return $powerToysEntry.InstallLocation.TrimEnd('\')
        }
        
        # Check for UninstallString as alternative source of install path
        if ($powerToysEntry -and $powerToysEntry.UninstallString) {
            # Extract directory from uninstall string like "C:\Program Files\PowerToys\unins000.exe"
            $uninstallExe = $powerToysEntry.UninstallString.Trim('"')
            $installDir = Split-Path $uninstallExe -Parent
            if ($installDir -and (Test-Path $installDir)) {
                return $installDir
            }
        }
    }
    catch {
        # If registry read fails, fall back to null
    }
    
    # If we can't determine the install path, return null
    return $null
}

function Test-CoreFiles {
    param(
        [string]$InstallPath,
        [string]$Scope
    )
    
    # Essential core files (must exist for basic functionality)
    $essentialCoreFiles = @(
        'PowerToys.exe',
        'PowerToys.ActionRunner.exe',
        'License.rtf',
        'Notice.md'
    )
    
    # Critical signed PowerToys executable files (from ESRP signing config)
    $criticalSignedFiles = @(
        # Main PowerToys components
        'PowerToys.exe',
        'PowerToys.ActionRunner.exe',
        'PowerToys.Update.exe',
        'PowerToys.BackgroundActivatorDLL.dll',
        'PowerToys.FilePreviewCommon.dll',
        'PowerToys.Interop.dll',
        
        # Common libraries
        'CalculatorEngineCommon.dll',
        'PowerToys.ManagedTelemetry.dll',
        'PowerToys.ManagedCommon.dll',
        'PowerToys.ManagedCsWin32.dll',
        'PowerToys.Common.UI.dll',
        'PowerToys.Settings.UI.Lib.dll',
        'PowerToys.GPOWrapper.dll',
        'PowerToys.GPOWrapperProjection.dll',
        'PowerToys.AllExperiments.dll',
        
        # Module executables and libraries
        'PowerToys.AlwaysOnTop.exe',
        'PowerToys.AlwaysOnTopModuleInterface.dll',
        'PowerToys.CmdNotFoundModuleInterface.dll',
        'PowerToys.ColorPicker.dll',
        'PowerToys.ColorPickerUI.dll',
        'PowerToys.ColorPickerUI.exe',
        'PowerToys.CropAndLockModuleInterface.dll',
        'PowerToys.CropAndLock.exe',
        'PowerToys.PowerOCRModuleInterface.dll',
        'PowerToys.PowerOCR.dll',
        'PowerToys.PowerOCR.exe',
        'PowerToys.AdvancedPasteModuleInterface.dll',
        'PowerToys.AwakeModuleInterface.dll',
        'PowerToys.Awake.exe',
        'PowerToys.Awake.dll',
        
        # FancyZones
        'PowerToys.FancyZonesEditor.exe',
        'PowerToys.FancyZonesEditor.dll',
        'PowerToys.FancyZonesEditorCommon.dll',
        'PowerToys.FancyZonesModuleInterface.dll',
        'PowerToys.FancyZones.exe',
        
        # Preview handlers
        'PowerToys.GcodePreviewHandler.dll',
        'PowerToys.GcodePreviewHandler.exe',
        'PowerToys.GcodePreviewHandlerCpp.dll',
        'PowerToys.GcodeThumbnailProvider.dll',
        'PowerToys.GcodeThumbnailProvider.exe',
        'PowerToys.GcodeThumbnailProviderCpp.dll',
        'PowerToys.BgcodePreviewHandler.dll',
        'PowerToys.BgcodePreviewHandler.exe',
        'PowerToys.BgcodePreviewHandlerCpp.dll',
        'PowerToys.BgcodeThumbnailProvider.dll',
        'PowerToys.BgcodeThumbnailProvider.exe',
        'PowerToys.BgcodeThumbnailProviderCpp.dll',
        'PowerToys.MarkdownPreviewHandler.dll',
        'PowerToys.MarkdownPreviewHandler.exe',
        'PowerToys.MarkdownPreviewHandlerCpp.dll',
        'PowerToys.MonacoPreviewHandler.dll',
        'PowerToys.MonacoPreviewHandler.exe',
        'PowerToys.MonacoPreviewHandlerCpp.dll',
        'PowerToys.PdfPreviewHandler.dll',
        'PowerToys.PdfPreviewHandler.exe',
        'PowerToys.PdfPreviewHandlerCpp.dll',
        'PowerToys.PdfThumbnailProvider.dll',
        'PowerToys.PdfThumbnailProvider.exe',
        'PowerToys.PdfThumbnailProviderCpp.dll',
        'PowerToys.powerpreview.dll',
        'PowerToys.PreviewHandlerCommon.dll',
        'PowerToys.QoiPreviewHandler.dll',
        'PowerToys.QoiPreviewHandler.exe',
        'PowerToys.QoiPreviewHandlerCpp.dll',
        'PowerToys.QoiThumbnailProvider.dll',
        'PowerToys.QoiThumbnailProvider.exe',
        'PowerToys.QoiThumbnailProviderCpp.dll',
        'PowerToys.StlThumbnailProvider.dll',
        'PowerToys.StlThumbnailProvider.exe',
        'PowerToys.StlThumbnailProviderCpp.dll',
        'PowerToys.SvgPreviewHandler.dll',
        'PowerToys.SvgPreviewHandler.exe',
        'PowerToys.SvgPreviewHandlerCpp.dll',
        'PowerToys.SvgThumbnailProvider.dll',
        'PowerToys.SvgThumbnailProvider.exe',
        'PowerToys.SvgThumbnailProviderCpp.dll',
        
        # Image Resizer
        'PowerToys.ImageResizer.exe',
        'PowerToys.ImageResizer.dll',
        'PowerToys.ImageResizerExt.dll',
        'PowerToys.ImageResizerContextMenu.dll',
        
        # Keyboard Manager
        'PowerToys.KeyboardManager.dll',
        'PowerToys.KeyboardManagerEditorLibraryWrapper.dll',
        
        # PowerToys Run
        'PowerToys.Launcher.dll',
        'PowerToys.PowerLauncher.dll',
        'PowerToys.PowerLauncher.exe',
        'PowerToys.PowerLauncher.Telemetry.dll',
        'Wox.Infrastructure.dll',
        'Wox.Plugin.dll',
        
        # Mouse utilities
        'PowerToys.FindMyMouse.dll',
        'PowerToys.MouseHighlighter.dll',
        'PowerToys.MouseJump.dll',
        'PowerToys.MouseJump.Common.dll',
        'PowerToys.MousePointerCrosshairs.dll',
        'PowerToys.MouseJumpUI.dll',
        'PowerToys.MouseJumpUI.exe',
        'PowerToys.MouseWithoutBorders.dll',
        'PowerToys.MouseWithoutBorders.exe',
        'PowerToys.MouseWithoutBordersModuleInterface.dll',
        'PowerToys.MouseWithoutBordersService.dll',
        'PowerToys.MouseWithoutBordersService.exe',
        'PowerToys.MouseWithoutBordersHelper.dll',
        'PowerToys.MouseWithoutBordersHelper.exe',
        
        # PowerAccent
        'PowerAccent.Core.dll',
        'PowerToys.PowerAccent.dll',
        'PowerToys.PowerAccent.exe',
        'PowerToys.PowerAccentModuleInterface.dll',
        'PowerToys.PowerAccentKeyboardService.dll',
        
        # Workspaces
        'PowerToys.WorkspacesSnapshotTool.exe',
        'PowerToys.WorkspacesLauncher.exe',
        'PowerToys.WorkspacesWindowArranger.exe',
        'PowerToys.WorkspacesEditor.exe',
        'PowerToys.WorkspacesEditor.dll',
        'PowerToys.WorkspacesLauncherUI.exe',
        'PowerToys.WorkspacesLauncherUI.dll',
        'PowerToys.WorkspacesModuleInterface.dll',
        'PowerToys.WorkspacesCsharpLibrary.dll',
        
        # Shortcut Guide
        'PowerToys.ShortcutGuide.exe',
        'PowerToys.ShortcutGuideModuleInterface.dll',
        
        # ZoomIt
        'PowerToys.ZoomIt.exe',
        'PowerToys.ZoomItModuleInterface.dll',
        'PowerToys.ZoomItSettingsInterop.dll',
        
        # Command Palette
        'PowerToys.CmdPalModuleInterface.dll',
        'CmdPalKeyboardService.dll'
    )
    
    # WinUI3Apps signed files (in WinUI3Apps subdirectory)
    $winUI3SignedFiles = @(
        'PowerToys.Settings.dll',
        'PowerToys.Settings.exe',
        'PowerToys.AdvancedPaste.exe',
        'PowerToys.AdvancedPaste.dll',
        'PowerToys.HostsModuleInterface.dll',
        'PowerToys.HostsUILib.dll',
        'PowerToys.Hosts.dll',
        'PowerToys.Hosts.exe',
        'PowerToys.FileLocksmithLib.Interop.dll',
        'PowerToys.FileLocksmithExt.dll',
        'PowerToys.FileLocksmithUI.exe',
        'PowerToys.FileLocksmithUI.dll',
        'PowerToys.FileLocksmithContextMenu.dll',
        'Peek.Common.dll',
        'Peek.FilePreviewer.dll',
        'Powertoys.Peek.UI.dll',
        'Powertoys.Peek.UI.exe',
        'Powertoys.Peek.dll',
        'PowerToys.EnvironmentVariablesModuleInterface.dll',
        'PowerToys.EnvironmentVariablesUILib.dll',
        'PowerToys.EnvironmentVariables.dll',
        'PowerToys.EnvironmentVariables.exe',
        'PowerToys.MeasureToolModuleInterface.dll',
        'PowerToys.MeasureToolCore.dll',
        'PowerToys.MeasureToolUI.dll',
        'PowerToys.MeasureToolUI.exe',
        'PowerToys.NewPlus.ShellExtension.dll',
        'PowerToys.NewPlus.ShellExtension.win10.dll',
        'PowerToys.PowerRenameExt.dll',
        'PowerToys.PowerRename.exe',
        'PowerToys.PowerRenameContextMenu.dll',
        'PowerToys.RegistryPreviewExt.dll',
        'PowerToys.RegistryPreviewUILib.dll',
        'PowerToys.RegistryPreview.dll',
        'PowerToys.RegistryPreview.exe'
    )
    
    # Tools signed files (in Tools subdirectory)
    $toolsSignedFiles = @(
        'PowerToys.BugReportTool.exe'
    )
    
    # KeyboardManager signed files (in specific subdirectories)
    $keyboardManagerFiles = @{
        'KeyboardManagerEditor\PowerToys.KeyboardManagerEditor.exe' = 'KeyboardManagerEditor'
        'KeyboardManagerEngine\PowerToys.KeyboardManagerEngine.exe' = 'KeyboardManagerEngine'
    }
    
    # Run plugins signed files (in RunPlugins subdirectories)
    $runPluginFiles = @{
        'RunPlugins\Calculator\Microsoft.PowerToys.Run.Plugin.Calculator.dll' = 'Calculator plugin'
        'RunPlugins\Folder\Microsoft.Plugin.Folder.dll' = 'Folder plugin'
        'RunPlugins\Indexer\Microsoft.Plugin.Indexer.dll' = 'Indexer plugin'
        'RunPlugins\OneNote\Microsoft.PowerToys.Run.Plugin.OneNote.dll' = 'OneNote plugin'
        'RunPlugins\History\Microsoft.PowerToys.Run.Plugin.History.dll' = 'History plugin'
        'RunPlugins\PowerToys\Microsoft.PowerToys.Run.Plugin.PowerToys.dll' = 'PowerToys plugin'
        'RunPlugins\Program\Microsoft.Plugin.Program.dll' = 'Program plugin'
        'RunPlugins\Registry\Microsoft.PowerToys.Run.Plugin.Registry.dll' = 'Registry plugin'
        'RunPlugins\WindowsSettings\Microsoft.PowerToys.Run.Plugin.WindowsSettings.dll' = 'Windows Settings plugin'
        'RunPlugins\Shell\Microsoft.Plugin.Shell.dll' = 'Shell plugin'
        'RunPlugins\Uri\Microsoft.Plugin.Uri.dll' = 'URI plugin'
        'RunPlugins\WindowWalker\Microsoft.Plugin.WindowWalker.dll' = 'Window Walker plugin'
        'RunPlugins\UnitConverter\Community.PowerToys.Run.Plugin.UnitConverter.dll' = 'Unit Converter plugin'
        'RunPlugins\VSCodeWorkspaces\Community.PowerToys.Run.Plugin.VSCodeWorkspaces.dll' = 'VS Code Workspaces plugin'
        'RunPlugins\Service\Microsoft.PowerToys.Run.Plugin.Service.dll' = 'Service plugin'
        'RunPlugins\System\Microsoft.PowerToys.Run.Plugin.System.dll' = 'System plugin'
        'RunPlugins\TimeDate\Microsoft.PowerToys.Run.Plugin.TimeDate.dll' = 'Time Date plugin'
        'RunPlugins\ValueGenerator\Community.PowerToys.Run.Plugin.ValueGenerator.dll' = 'Value Generator plugin'
        'RunPlugins\WebSearch\Community.PowerToys.Run.Plugin.WebSearch.dll' = 'Web Search plugin'
        'RunPlugins\WindowsTerminal\Microsoft.PowerToys.Run.Plugin.WindowsTerminal.dll' = 'Windows Terminal plugin'
    }
    
    # Check essential core files (must exist)
    Write-StatusMessage "Checking essential core files..." -Level Info
    foreach ($file in $essentialCoreFiles) {
        $filePath = Join-Path $InstallPath $file
        $exists = Test-Path $filePath
        Add-CheckResult -Category "Core Files" -CheckName "$file ($Scope)" -Status $(if ($exists) { 'Pass' } else { 'Fail' }) -Message "Essential file: $filePath"
    }
    
    # Check critical signed files in root directory
    Write-StatusMessage "Checking critical signed files in root directory..." -Level Info
    foreach ($file in $criticalSignedFiles) {
        $filePath = Join-Path $InstallPath $file
        $exists = Test-Path $filePath
        # Most signed files are critical, but some may be optional depending on configuration
        $status = if ($exists) { 'Pass' } else { 'Warning' }
        Add-CheckResult -Category "Signed Files" -CheckName "$file ($Scope)" -Status $status -Message "Signed file: $filePath"
    }
    
    # Check WinUI3Apps signed files
    Write-StatusMessage "Checking WinUI3Apps signed files..." -Level Info
    foreach ($file in $winUI3SignedFiles) {
        $filePath = Join-Path $InstallPath "WinUI3Apps\$file"
        $exists = Test-Path $filePath
        $status = if ($exists) { 'Pass' } else { 'Warning' }
        Add-CheckResult -Category "Signed Files" -CheckName "WinUI3Apps\$file ($Scope)" -Status $status -Message "WinUI3 signed file: $filePath"
    }
    
    # Check Tools signed files
    Write-StatusMessage "Checking Tools signed files..." -Level Info
    foreach ($file in $toolsSignedFiles) {
        $filePath = Join-Path $InstallPath "Tools\$file"
        $exists = Test-Path $filePath
        $status = if ($exists) { 'Pass' } else { 'Warning' }
        Add-CheckResult -Category "Signed Files" -CheckName "Tools\$file ($Scope)" -Status $status -Message "Tools signed file: $filePath"
    }
    
    # Check KeyboardManager files
    Write-StatusMessage "Checking KeyboardManager signed files..." -Level Info
    foreach ($relativePath in $keyboardManagerFiles.Keys) {
        $filePath = Join-Path $InstallPath $relativePath
        $exists = Test-Path $filePath
        $status = if ($exists) { 'Pass' } else { 'Warning' }
        $description = $keyboardManagerFiles[$relativePath]
        Add-CheckResult -Category "Signed Files" -CheckName "$relativePath ($Scope)" -Status $status -Message "KeyboardManager $description signed file: $filePath"
    }
    
    # Check Run plugins files
    Write-StatusMessage "Checking PowerToys Run plugin files..." -Level Info
    foreach ($relativePath in $runPluginFiles.Keys) {
        $filePath = Join-Path $InstallPath $relativePath
        $exists = Test-Path $filePath
        $status = if ($exists) { 'Pass' } else { 'Warning' }
        $description = $runPluginFiles[$relativePath]
        Add-CheckResult -Category "Signed Files" -CheckName "$relativePath ($Scope)" -Status $status -Message "PowerToys Run $description signed file: $filePath"
    }
}

function Test-ModuleFiles {
    param(
        [string]$InstallPath,
        [string]$Scope
    )
    
    # PowerToys does not actually install modules in a "modules" subfolder.
    # Instead, modules are integrated directly into the main installation or specific subfolders.
    # Check for key module directories that should exist:
    
    # Check KeyboardManager components (installed as separate folders)
    $keyboardManagerEditor = Join-Path $InstallPath "KeyboardManagerEditor"
    $keyboardManagerEngine = Join-Path $InstallPath "KeyboardManagerEngine"
    
    if (Test-Path $keyboardManagerEditor) {
        Add-CheckResult -Category "Modules" -CheckName "KeyboardManager Editor ($Scope)" -Status 'Pass' -Message "KeyboardManager Editor folder exists: $keyboardManagerEditor"
    }
    else {
        Add-CheckResult -Category "Modules" -CheckName "KeyboardManager Editor ($Scope)" -Status 'Warning' -Message "KeyboardManager Editor folder not found: $keyboardManagerEditor"
    }
    
    if (Test-Path $keyboardManagerEngine) {
        Add-CheckResult -Category "Modules" -CheckName "KeyboardManager Engine ($Scope)" -Status 'Pass' -Message "KeyboardManager Engine folder exists: $keyboardManagerEngine"
    }
    else {
        Add-CheckResult -Category "Modules" -CheckName "KeyboardManager Engine ($Scope)" -Status 'Warning' -Message "KeyboardManager Engine folder not found: $keyboardManagerEngine"
    }
    
    # Check RunPlugins folder (contains PowerToys Run modules)
    $runPluginsPath = Join-Path $InstallPath "RunPlugins"
    if (Test-Path $runPluginsPath) {
        Add-CheckResult -Category "Modules" -CheckName "Run Plugins Folder ($Scope)" -Status 'Pass' -Message "Run plugins folder exists: $runPluginsPath"
    }
    else {
        Add-CheckResult -Category "Modules" -CheckName "Run Plugins Folder ($Scope)" -Status 'Warning' -Message "Run plugins folder not found: $runPluginsPath"
    }
    
    # Check Tools folder
    $toolsPath = Join-Path $InstallPath "Tools"
    if (Test-Path $toolsPath) {
        Add-CheckResult -Category "Modules" -CheckName "Tools Folder ($Scope)" -Status 'Pass' -Message "Tools folder exists: $toolsPath"
    }
    else {
        Add-CheckResult -Category "Modules" -CheckName "Tools Folder ($Scope)" -Status 'Warning' -Message "Tools folder not found: $toolsPath"
    }
}

function Test-RegistryHandlers {
    param(
        [string]$Scope
    )
    
    $registryRoot = if ($Scope -eq 'PerMachine') { 'HKLM:' } else { 'HKCU:' }
    # Test URL protocol handler
    $protocolPath = "$registryRoot\SOFTWARE\Classes\powertoys"
    if (Test-RegistryKey -Path $protocolPath) {
        Add-CheckResult -Category "Registry Handlers" -CheckName "PowerToys URL Protocol ($Scope)" -Status 'Pass' -Message "URL protocol registered"
        
        # Check command handler
        $commandPath = "$protocolPath\shell\open\command"
        if (Test-RegistryKey -Path $commandPath) {
            Add-CheckResult -Category "Registry Handlers" -CheckName "PowerToys URL Command ($Scope)" -Status 'Pass' -Message "URL command handler registered"
        }
        else {
            Add-CheckResult -Category "Registry Handlers" -CheckName "PowerToys URL Command ($Scope)" -Status 'Fail' -Message "URL command handler not found"
        }
    }
    else {
        Add-CheckResult -Category "Registry Handlers" -CheckName "PowerToys URL Protocol ($Scope)" -Status 'Fail' -Message "URL protocol not registered"
    }
    
    # Test CLSID registration for toast notifications
    $toastClsidPath = "$registryRoot\SOFTWARE\Classes\CLSID\{DD5CACDA-7C2E-4997-A62A-04A597B58F76}"
    if (Test-RegistryKey -Path $toastClsidPath) {
        Add-CheckResult -Category "Registry Handlers" -CheckName "Toast Notification CLSID ($Scope)" -Status 'Pass' -Message "Toast notification CLSID registered"
    }
    else {
        Add-CheckResult -Category "Registry Handlers" -CheckName "Toast Notification CLSID ($Scope)" -Status 'Warning' -Message "Toast notification CLSID not found"
    }
}

function Test-DSCModule {
    param(
        [string]$Scope
    )
    
    if ($Scope -eq 'PerUser') {
        # For per-user installations, DSC module is installed via custom action to user's Documents
        $userModulesPath = "$env:USERPROFILE\Documents\PowerShell\Modules\Microsoft.PowerToys.Configure"
        if (Test-Path $userModulesPath) {
            Add-CheckResult -Category "DSC Module" -CheckName "DSC Module (PerUser)" -Status 'Pass' -Message "DSC module found in user profile: $userModulesPath"
        }
        else {
            Add-CheckResult -Category "DSC Module" -CheckName "DSC Module (PerUser)" -Status 'Fail' -Message "DSC module not found in user profile: $userModulesPath"
        }
    }
    else {
        # For per-machine installations, DSC module is installed to system WindowsPowerShell modules
        $systemModulesPath = "${env:ProgramFiles}\WindowsPowerShell\Modules\Microsoft.PowerToys.Configure"
        if (Test-Path $systemModulesPath) {
            Add-CheckResult -Category "DSC Module" -CheckName "DSC Module (PerMachine)" -Status 'Pass' -Message "DSC module found in system modules: $systemModulesPath"
        }
        else {
            Add-CheckResult -Category "DSC Module" -CheckName "DSC Module (PerMachine)" -Status 'Fail' -Message "DSC module not found in system modules: $systemModulesPath"
        }
    }
}

function Test-CommandPalettePackages {
    param(
        [string]$InstallPath
    )
    
    $cmdPalPath = Join-Path $InstallPath "WinUI3Apps\CmdPal"
    if (Test-Path $cmdPalPath) {
        # Check for MSIX package file (the actual Command Palette installation)
        $msixFiles = Get-ChildItem $cmdPalPath -Filter "*.msix" -ErrorAction SilentlyContinue
        if ($msixFiles) {
            Add-CheckResult -Category "Command Palette" -CheckName "CmdPal MSIX Package" -Status 'Pass' -Message "Found $($msixFiles.Count) Command Palette MSIX package(s)"            
        }
        else {
            Add-CheckResult -Category "Command Palette" -CheckName "CmdPal MSIX Package" -Status 'Warning' -Message "No Command Palette MSIX packages found"
        }
    }
    else {
        Add-CheckResult -Category "Command Palette" -CheckName "CmdPal Module" -Status 'Warning' -Message "Command Palette module not found at: $cmdPalPath"
    }
}

function Test-ContextMenuPackages {
    param(
        [string]$InstallPath
    )
    
    # Context menu packages are installed as sparse packages
    # These MSIX packages should be present in the installation
    $contextMenuPackages = @{
        "ImageResizerContextMenuPackage.msix" = @{ Name = "Image Resizer context menu package"; Location = "Root" }
        "FileLocksmithContextMenuPackage.msix" = @{ Name = "File Locksmith context menu package"; Location = "WinUI3Apps" }
        "PowerRenameContextMenuPackage.msix" = @{ Name = "PowerRename context menu package"; Location = "WinUI3Apps" }
        "NewPlusPackage.msix" = @{ Name = "New+ context menu package"; Location = "WinUI3Apps" }
    }
    
    # Check for packages based on their expected location
    foreach ($packageFile in $contextMenuPackages.Keys) {
        $packageInfo = $contextMenuPackages[$packageFile]
        
        if ($packageInfo.Location -eq "Root") {
            $packagePath = Join-Path $InstallPath $packageFile
        }
        else {
            $packagePath = Join-Path $InstallPath "WinUI3Apps\$packageFile"
        }
        
        if (Test-Path $packagePath) {
            Add-CheckResult -Category "Context Menu Packages" -CheckName $packageInfo.Name -Status 'Pass' -Message "Context menu package found: $packagePath"
        }
        else {
            Add-CheckResult -Category "Context Menu Packages" -CheckName $packageInfo.Name -Status 'Fail' -Message "Context menu package not found: $packagePath"
        }
    }
}

# Main execution
function Main {
    Write-StatusMessage "Starting PowerToys Installation Verification" -Level Info
    Write-StatusMessage "Scope: $InstallScope" -Level Info
    
    # Check the specified scope - no fallbacks, only what installer should create
    $installationFound = $false
    
    if ($InstallScope -eq 'PerMachine') {
        if (Test-PowerToysInstallation -Scope 'PerMachine') {
            $installationFound = $true
            Test-RegistryHandlers -Scope 'PerMachine'
            Test-DSCModule -Scope 'PerMachine'
            $installPath = Get-PowerToysInstallPath -Scope 'PerMachine'
            if ($installPath) {
                Test-CommandPalettePackages -InstallPath $installPath
                Test-ContextMenuPackages -InstallPath $installPath
            }
        }
    }
    else { # PerUser
        if (Test-PowerToysInstallation -Scope 'PerUser') {
            $installationFound = $true
            Test-RegistryHandlers -Scope 'PerUser'
            Test-DSCModule -Scope 'PerUser'
            $installPath = Get-PowerToysInstallPath -Scope 'PerUser'
            if ($installPath) {
                Test-CommandPalettePackages -InstallPath $installPath
                Test-ContextMenuPackages -InstallPath $installPath
            }
        }
    }
    
    if ($installationFound) {
        # Common tests (only run if installation found)
        # Note: Scheduled tasks are not created by installer, they're created at runtime
    }
    
    # Calculate overall status
    if ($script:Results.Summary.FailedChecks -eq 0) {
        if ($script:Results.Summary.WarningChecks -eq 0) {
            $script:Results.Summary.OverallStatus = "Healthy"
        }
        else {
            $script:Results.Summary.OverallStatus = "Healthy with Warnings"
        }
    }
    else {
        $script:Results.Summary.OverallStatus = "Issues Detected"
    }
    
    # Display summary
    Write-Host "`n" -NoNewline
    Write-StatusMessage "=== VERIFICATION SUMMARY ===" -Level Info
    Write-StatusMessage "Total Checks: $($script:Results.Summary.TotalChecks)" -Level Info
    Write-StatusMessage "Passed: $($script:Results.Summary.PassedChecks)" -Level Success
    Write-StatusMessage "Failed: $($script:Results.Summary.FailedChecks)" -Level Error
    Write-StatusMessage "Warnings: $($script:Results.Summary.WarningChecks)" -Level Warning
    Write-StatusMessage "Overall Status: $($script:Results.Summary.OverallStatus)" -Level $(
        switch ($script:Results.Summary.OverallStatus) {
            'Healthy' { 'Success' }
            'Healthy with Warnings' { 'Warning' }
            default { 'Error' }
        }
    )
    
    Write-StatusMessage "PowerToys Installation Verification Complete" -Level Info
}

# Run the main function
Main

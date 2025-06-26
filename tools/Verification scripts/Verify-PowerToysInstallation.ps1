#Requires -Version 5.1

<#
.SYNOPSIS
    Verifies a PowerToys installation by checking all components, registry entries, files, and custom logic.

.DESCRIPTION
    This script comprehensively verifies a PowerToys installation by checking:
    - Registry entries for both per-machine and per-user installations
    - File and folder structure integrity
    - Module regi        "PowerToys.Settings.UI.Lib.dll",
        "PowerToys.GPOWrapper.dll",
        "PowerToys.GPOWrapperProjection.dll",

        # AlwaysOnTop
        "PowerToys.AlwaysOnTop.exe",
        "PowerToys.AlwaysOnTopModuleInterface.dll",

        # CmdNotFound
        "PowerToys.CmdNotFoundModuleInterface.dll", functionality
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

.PARAMETER ExportReport
    Exports the verification results to a JSON report file.

.EXAMPLE
    .\Verify-PowerToysInstallation.ps1 -InstallScope PerMachine
    
.EXAMPLE
    .\Verify-PowerToysInstallation.ps1 -InstallScope PerUser
    
.EXAMPLE
    .\Verify-PowerToysInstallation.ps1 -InstallScope PerMachine -ExportReport

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
    [string]$InstallPath,
    
    [Parameter(Mandatory = $false)]
    [switch]$ExportReport
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

# PowerToys modules to verify
$PowerToysModules = @(
    "AdvancedPaste", "Awake", "ColorPicker", "CmdPal", "EnvironmentVariables",
    "FileLocksmith", "FancyZones", "Hosts", "ImageResizer", "KeyboardManager",
    "MouseWithoutBorders", "NewPlus", "Peek", "PowerRename", "RegistryPreview",
    "Run", "ShortcutGuide", "Settings", "Workspaces"
)

# Expected registry handlers
$ExpectedHandlers = @{
    "powertoys" = @{
        "Description" = "URL:PowerToys custom internal URI protocol"
        "URLProtocol" = ""
    }
}

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
        [object]$Details = $null,
        [switch]$Silent
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
    
    # Show test results immediately unless Silent is specified
    if (-not $Silent) {
        $level = switch ($Status) {
            'Pass' { 'Success' }
            'Fail' { 'Error' }
            'Warning' { 'Warning' }
        }
        Write-StatusMessage "[$Category] $CheckName - $Message" -Level $level
    }
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
    $registryRoot = if ($Scope -eq 'PerMachine') { 'HKLM' } else { 'HKCU' }
    $registryKey = if ($Scope -eq 'PerMachine') { $PowerToysRegistryKeyPerMachine } else { $PowerToysRegistryKeyPerUser }
    $upgradeCode = if ($Scope -eq 'PerMachine') { $PowerToysUpgradeCodePerMachine } else { $PowerToysUpgradeCodePerUser }
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
            
            # Note: InstallLocation may or may not be set in the uninstall registry
            # This is normal behavior as PowerToys uses direct file references for system bindings
            if ($powerToysEntry.InstallLocation) {
                Add-CheckResult -Category "Registry" -CheckName "Install Location Registry ($Scope)" -Status 'Pass' -Message "InstallLocation found: $($powerToysEntry.InstallLocation)"
            }
            # No need to report missing InstallLocation as it's not required
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
    
    # Since InstallLocation may not be reliably set in the uninstall registry,
    # we'll use the default installation paths based on scope
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
    
    # Core files in root installation directory
    $rootCoreFiles = @(
        'PowerToys.exe',
        'PowerToys.ActionRunner.exe',
        'License.rtf',
        'Notice.md'
    )
    
    # Settings application in WinUI3Apps subdirectory
    $settingsFile = 'WinUI3Apps\PowerToys.Settings.exe'
    
    foreach ($file in $rootCoreFiles) {
        $filePath = Join-Path $InstallPath $file
        $exists = Test-Path $filePath
        Add-CheckResult -Category "Core Files" -CheckName "$file ($Scope)" -Status $(if ($exists) { 'Pass' } else { 'Fail' }) -Message "File exists: $filePath"
    }
    
    # Check Settings application in WinUI3Apps folder
    $settingsPath = Join-Path $InstallPath $settingsFile
    $settingsExists = Test-Path $settingsPath
    Add-CheckResult -Category "Core Files" -CheckName "PowerToys.Settings.exe ($Scope)" -Status $(if ($settingsExists) { 'Pass' } else { 'Fail' }) -Message "File exists: $settingsPath"
}

function Test-ModuleFiles {
    param(
        [string]$InstallPath,
        [string]$Scope
    )
    
    Write-StatusMessage "Checking file existence..." -Level Info
    
    # This function checks for all files that should be signed according to ESRPSigning_core.json
    # This ensures we verify exactly what the build pipeline expects to be present
    
    # Define the files from ESRPSigning_core.json that should be present
    # These are the executables and DLLs that are signed and should exist in the installation
    $signedFiles = @(
        # Core executables and DLLs
        "PowerToys.ActionRunner.exe",
        "PowerToys.Update.exe", 
        "PowerToys.BackgroundActivatorDLL.dll",
        "PowerToys.exe",
        "PowerToys.FilePreviewCommon.dll",
        "PowerToys.Interop.dll",
        "Tools\PowerToys.BugReportTool.exe",
        "PowerToys.ManagedTelemetry.dll",
        "PowerToys.ManagedCommon.dll",
        "PowerToys.Common.UI.dll",
        "PowerToys.Settings.UI.Lib.dll",
        "PowerToys.GPOWrapper.dll",
        "PowerToys.GPOWrapperProjection.dll",
        "WinUI3Apps\PowerToys.AllExperiments.dll",

        # AlwaysOnTop
        "PowerToys.AlwaysOnTop.exe",
        "PowerToys.AlwaysOnTopModuleInterface.dll",

        # CmdNotFound
        "PowerToys.CmdNotFoundModuleInterface.dll",

        # ColorPicker
        "PowerToys.ColorPicker.dll",
        "PowerToys.ColorPickerUI.dll",
        "PowerToys.ColorPickerUI.exe",

        # CropAndLock
        "PowerToys.CropAndLockModuleInterface.dll",
        "PowerToys.CropAndLock.exe",

        # PowerOCR
        "PowerToys.PowerOCRModuleInterface.dll",
        "PowerToys.PowerOCR.dll",
        "PowerToys.PowerOCR.exe",

        # AdvancedPaste
        "PowerToys.AdvancedPasteModuleInterface.dll",
        "WinUI3Apps\PowerToys.AdvancedPaste.exe",
        "WinUI3Apps\PowerToys.AdvancedPaste.dll",

        # Awake
        "PowerToys.AwakeModuleInterface.dll",
        "PowerToys.Awake.exe",
        "PowerToys.Awake.dll",

        # FancyZones
        "PowerToys.FancyZonesEditor.exe",
        "PowerToys.FancyZonesEditor.dll",
        "PowerToys.FancyZonesEditorCommon.dll",
        "PowerToys.FancyZonesModuleInterface.dll",
        "PowerToys.FancyZones.exe",

        # Hosts
        "WinUI3Apps\PowerToys.HostsModuleInterface.dll",
        "WinUI3Apps\PowerToys.HostsUILib.dll",
        "WinUI3Apps\PowerToys.Hosts.dll",
        "WinUI3Apps\PowerToys.Hosts.exe",

        # FileLocksmith
        "WinUI3Apps\PowerToys.FileLocksmithLib.Interop.dll",
        "WinUI3Apps\PowerToys.FileLocksmithExt.dll",
        "WinUI3Apps\PowerToys.FileLocksmithUI.exe",
        "WinUI3Apps\PowerToys.FileLocksmithUI.dll",
        "WinUI3Apps\PowerToys.FileLocksmithContextMenu.dll",

        # Peek
        "WinUI3Apps\Peek.Common.dll",
        "WinUI3Apps\Peek.FilePreviewer.dll",
        "WinUI3Apps\Powertoys.Peek.UI.dll",
        "WinUI3Apps\Powertoys.Peek.UI.exe",
        "WinUI3Apps\Powertoys.Peek.dll",

        # EnvironmentVariables
        "WinUI3Apps\PowerToys.EnvironmentVariablesModuleInterface.dll",
        "WinUI3Apps\PowerToys.EnvironmentVariablesUILib.dll",
        "WinUI3Apps\PowerToys.EnvironmentVariables.dll",
        "WinUI3Apps\PowerToys.EnvironmentVariables.exe",

        # ImageResizer
        "PowerToys.ImageResizer.exe",
        "PowerToys.ImageResizer.dll",
        "PowerToys.ImageResizerExt.dll",
        "PowerToys.ImageResizerContextMenu.dll",

        # KeyboardManager
        "PowerToys.KeyboardManager.dll",
        "KeyboardManagerEditor\PowerToys.KeyboardManagerEditor.exe",
        "KeyboardManagerEngine\PowerToys.KeyboardManagerEngine.exe",
        "PowerToys.KeyboardManagerEditorLibraryWrapper.dll",

        # PowerLauncher/Run
        "PowerToys.Launcher.dll",
        "PowerToys.PowerLauncher.dll",
        "PowerToys.PowerLauncher.exe",
        "PowerToys.PowerLauncher.Telemetry.dll",
        "Wox.Infrastructure.dll",
        "Wox.Plugin.dll",

        # Run Plugins
        "RunPlugins\Calculator\Microsoft.PowerToys.Run.Plugin.Calculator.dll",
        "RunPlugins\Folder\Microsoft.Plugin.Folder.dll",
        "RunPlugins\Indexer\Microsoft.Plugin.Indexer.dll",
        "RunPlugins\OneNote\Microsoft.PowerToys.Run.Plugin.OneNote.dll",
        "RunPlugins\History\Microsoft.PowerToys.Run.Plugin.History.dll",
        "RunPlugins\PowerToys\Microsoft.PowerToys.Run.Plugin.PowerToys.dll",
        "RunPlugins\Program\Microsoft.Plugin.Program.dll",
        "RunPlugins\Registry\Microsoft.PowerToys.Run.Plugin.Registry.dll",
        "RunPlugins\WindowsSettings\Microsoft.PowerToys.Run.Plugin.WindowsSettings.dll",
        "RunPlugins\Shell\Microsoft.Plugin.Shell.dll",
        "RunPlugins\Uri\Microsoft.Plugin.Uri.dll",
        "RunPlugins\WindowWalker\Microsoft.Plugin.WindowWalker.dll",
        "RunPlugins\UnitConverter\Community.PowerToys.Run.Plugin.UnitConverter.dll",
        "RunPlugins\VSCodeWorkspaces\Community.PowerToys.Run.Plugin.VSCodeWorkspaces.dll",
        "RunPlugins\Service\Microsoft.PowerToys.Run.Plugin.Service.dll",
        "RunPlugins\System\Microsoft.PowerToys.Run.Plugin.System.dll",
        "RunPlugins\TimeDate\Microsoft.PowerToys.Run.Plugin.TimeDate.dll",
        "RunPlugins\ValueGenerator\Community.PowerToys.Run.Plugin.ValueGenerator.dll",
        "RunPlugins\WebSearch\Community.PowerToys.Run.Plugin.WebSearch.dll",
        "RunPlugins\WindowsTerminal\Microsoft.PowerToys.Run.Plugin.WindowsTerminal.dll",

        # MeasureTool
        "WinUI3Apps\PowerToys.MeasureToolModuleInterface.dll",
        "WinUI3Apps\PowerToys.MeasureToolCore.dll",
        "WinUI3Apps\PowerToys.MeasureToolUI.dll",
        "WinUI3Apps\PowerToys.MeasureToolUI.exe",

        # Mouse utilities
        "PowerToys.FindMyMouse.dll",
        "PowerToys.MouseHighlighter.dll",
        "PowerToys.MouseJump.dll",
        "PowerToys.MouseJump.Common.dll",
        "PowerToys.MousePointerCrosshairs.dll",
        "PowerToys.MouseJumpUI.dll",
        "PowerToys.MouseJumpUI.exe",

        # MouseWithoutBorders
        "PowerToys.MouseWithoutBorders.dll",
        "PowerToys.MouseWithoutBorders.exe",
        "PowerToys.MouseWithoutBordersModuleInterface.dll",
        "PowerToys.MouseWithoutBordersService.dll",
        "PowerToys.MouseWithoutBordersService.exe",
        "PowerToys.MouseWithoutBordersHelper.dll",
        "PowerToys.MouseWithoutBordersHelper.exe",

        # NewPlus
        "WinUI3Apps\PowerToys.NewPlus.ShellExtension.dll",
        "WinUI3Apps\PowerToys.NewPlus.ShellExtension.win10.dll",

        # PowerAccent
        "PowerAccent.Core.dll",
        "PowerToys.PowerAccent.dll",
        "PowerToys.PowerAccent.exe",
        "PowerToys.PowerAccentModuleInterface.dll",
        "PowerToys.PowerAccentKeyboardService.dll",

        # PowerRename
        "WinUI3Apps\PowerToys.PowerRenameExt.dll",
        "WinUI3Apps\PowerToys.PowerRename.exe",
        "WinUI3Apps\PowerToys.PowerRenameContextMenu.dll",

        # Workspaces
        "PowerToys.WorkspacesSnapshotTool.exe",
        "PowerToys.WorkspacesLauncher.exe",
        "PowerToys.WorkspacesWindowArranger.exe",
        "PowerToys.WorkspacesEditor.exe",
        "PowerToys.WorkspacesEditor.dll",
        "PowerToys.WorkspacesLauncherUI.exe",
        "PowerToys.WorkspacesLauncherUI.dll",
        "PowerToys.WorkspacesModuleInterface.dll",
        "PowerToys.WorkspacesCsharpLibrary.dll",

        # RegistryPreview
        "WinUI3Apps\PowerToys.RegistryPreviewExt.dll",
        "WinUI3Apps\PowerToys.RegistryPreviewUILib.dll",
        "WinUI3Apps\PowerToys.RegistryPreview.dll",
        "WinUI3Apps\PowerToys.RegistryPreview.exe",

        # ShortcutGuide
        "PowerToys.ShortcutGuide.exe",
        "PowerToys.ShortcutGuideModuleInterface.dll",

        # ZoomIt
        "PowerToys.ZoomIt.exe",
        "PowerToys.ZoomItModuleInterface.dll",
        "PowerToys.ZoomItSettingsInterop.dll",

        # Settings
        "WinUI3Apps\PowerToys.Settings.dll",
        "WinUI3Apps\PowerToys.Settings.exe",

        # CmdPal
        "PowerToys.CmdPalModuleInterface.dll",
        "CmdPalKeyboardService.dll"
    )

    # Check each signed file
    $missingFiles = @()
    $totalFiles = $signedFiles.Count
    $existingFiles = 0
    $currentCheck = 0
    
    foreach ($relativeFilePath in $signedFiles) {
        $currentCheck++
        $fullPath = Join-Path $InstallPath $relativeFilePath
        $fileName = Split-Path $relativeFilePath -Leaf
        $exists = Test-Path $fullPath
        
        # Show progress for every 20th file or if file is missing
        if (($currentCheck % 20 -eq 0) -or (-not $exists)) {
            if ($exists) {
                Write-StatusMessage "Checking files... ($currentCheck/$totalFiles)" -Level Info
            }
        }
        
        if ($exists) {
            $existingFiles++
        }
        else {
            $missingFiles += $fullPath
            Add-CheckResult -Category "Signed Files" -CheckName "$fileName ($Scope)" -Status 'Fail' -Message "Signed file missing: $fullPath"
        }
    }
    
    # Add summary result
    if ($missingFiles.Count -eq 0) {
        Add-CheckResult -Category "Signed Files" -CheckName "File Existence Summary ($Scope)" -Status 'Pass' -Message "All $totalFiles signed files found"
    }
    else {
        Add-CheckResult -Category "Signed Files" -CheckName "File Existence Summary ($Scope)" -Status 'Fail' -Message "$($missingFiles.Count) of $totalFiles signed files missing"
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
        $urlProtocol = Get-RegistryValue -Path $protocolPath -Name "URL Protocol"
        $description = Get-RegistryValue -Path $protocolPath -Name "(Default)"
        
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

function Get-DocumentsPath {
    # Use Windows Shell API to get the real Documents folder path (same as C++ code)
    # This handles OneDrive redirection correctly
    try {
        Add-Type -TypeDefinition @"
            using System;
            using System.Runtime.InteropServices;
            using System.Text;
            
            public class Shell32 {
                [DllImport("shell32.dll")]
                public static extern int SHGetKnownFolderPath(
                    [MarshalAs(UnmanagedType.LPStruct)] Guid rfid,
                    uint dwFlags,
                    IntPtr hToken,
                    out IntPtr pszPath
                );
                
                [DllImport("ole32.dll")]
                public static extern void CoTaskMemFree(IntPtr ptr);
                
                public static readonly Guid FOLDERID_Documents = new Guid("FDD39AD0-238F-46AF-ADB4-6C85480369C7");
            }
"@

        $documentsPtr = [IntPtr]::Zero
        $result = [Shell32]::SHGetKnownFolderPath([Shell32]::FOLDERID_Documents, 0, [IntPtr]::Zero, [ref]$documentsPtr)
        
        if ($result -eq 0 -and $documentsPtr -ne [IntPtr]::Zero) {
            $documentsPath = [System.Runtime.InteropServices.Marshal]::PtrToStringUni($documentsPtr)
            [Shell32]::CoTaskMemFree($documentsPtr)
            return $documentsPath
        }
        else {
            # Fallback to environment variable if API fails
            return "$env:USERPROFILE\Documents"
        }
    }
    catch {
        # Fallback to environment variable if anything goes wrong
        return "$env:USERPROFILE\Documents"
    }
}

function Test-DSCModule {
    param(
        [string]$Scope
    )
    
    if ($Scope -eq 'PerUser') {
        # For per-user installations, DSC module is installed via custom action to user's Documents
        # Use the same logic as the C++ installer to get the real Documents path
        $documentsPath = Get-DocumentsPath
        $userModulesPath = Join-Path $documentsPath "PowerShell\Modules\Microsoft.PowerToys.Configure"
        
        if (Test-Path $userModulesPath) {
            # Check if there are version folders inside the module directory
            $versionFolders = Get-ChildItem $userModulesPath -Directory -ErrorAction SilentlyContinue
            if ($versionFolders) {
                $latestVersion = $versionFolders | Sort-Object Name -Descending | Select-Object -First 1
                $versionPath = $latestVersion.FullName
                
                # Check for required module files
                $moduleManifest = Join-Path $versionPath "Microsoft.PowerToys.Configure.psd1"
                $moduleScript = Join-Path $versionPath "Microsoft.PowerToys.Configure.psm1"
                
                $manifestExists = Test-Path $moduleManifest
                $scriptExists = Test-Path $moduleScript
                
                if ($manifestExists -and $scriptExists) {
                    Add-CheckResult -Category "DSC Module" -CheckName "DSC Module (PerUser)" -Status 'Pass' -Message "DSC module found in user profile: $userModulesPath (version: $($latestVersion.Name)) with required files"
                }
                else {
                    $missingFiles = @()
                    if (-not $manifestExists) { $missingFiles += "*.psd1" }
                    if (-not $scriptExists) { $missingFiles += "*.psm1" }
                    Add-CheckResult -Category "DSC Module" -CheckName "DSC Module (PerUser)" -Status 'Fail' -Message "DSC module version folder exists but missing files: $($missingFiles -join ', ')"
                }
            }
            else {
                Add-CheckResult -Category "DSC Module" -CheckName "DSC Module (PerUser)" -Status 'Warning' -Message "DSC module folder exists but no version folders found: $userModulesPath"
            }
        }
        else {
            Add-CheckResult -Category "DSC Module" -CheckName "DSC Module (PerUser)" -Status 'Fail' -Message "DSC module not found in user profile: $userModulesPath"
        }
    }
    else {
        # For per-machine installations, DSC module is installed to system WindowsPowerShell modules
        $systemModulesPath = "${env:ProgramFiles}\WindowsPowerShell\Modules\Microsoft.PowerToys.Configure"
        if (Test-Path $systemModulesPath) {
            # Check if there are version folders inside the module directory
            $versionFolders = Get-ChildItem $systemModulesPath -Directory -ErrorAction SilentlyContinue
            if ($versionFolders) {
                $latestVersion = $versionFolders | Sort-Object Name -Descending | Select-Object -First 1
                $versionPath = $latestVersion.FullName
                
                # Check for required module files
                $moduleManifest = Join-Path $versionPath "Microsoft.PowerToys.Configure.psd1"
                $moduleScript = Join-Path $versionPath "Microsoft.PowerToys.Configure.psm1"
                
                $manifestExists = Test-Path $moduleManifest
                $scriptExists = Test-Path $moduleScript
                
                if ($manifestExists -and $scriptExists) {
                    Add-CheckResult -Category "DSC Module" -CheckName "DSC Module (PerMachine)" -Status 'Pass' -Message "DSC module found in system modules: $systemModulesPath (version: $($latestVersion.Name)) with required files"
                }
                else {
                    $missingFiles = @()
                    if (-not $manifestExists) { $missingFiles += "*.psd1" }
                    if (-not $scriptExists) { $missingFiles += "*.psm1" }
                    Add-CheckResult -Category "DSC Module" -CheckName "DSC Module (PerMachine)" -Status 'Fail' -Message "DSC module version folder exists but missing files: $($missingFiles -join ', ')"
                }
            }
            else {
                Add-CheckResult -Category "DSC Module" -CheckName "DSC Module (PerMachine)" -Status 'Warning' -Message "DSC module folder exists but no version folders found: $systemModulesPath"
            }
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
            Add-CheckResult -Category "Command Palette" -CheckName "CmdPal MSIX Package" -Status 'Fail' -Message "No Command Palette MSIX packages found"
        }
    }
    else {
        Add-CheckResult -Category "Command Palette" -CheckName "CmdPal Module" -Status 'Fail' -Message "Command Palette module not found at: $cmdPalPath"
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

function Export-Report {
    if ($ExportReport) {
        $timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
        $reportPath = "PowerToys_Verification_Report_$timestamp.json"
        
        try {
            $script:Results | ConvertTo-Json -Depth 10 | Out-File -FilePath $reportPath -Encoding UTF8
            Write-StatusMessage "Verification report exported to: $reportPath" -Level Info
        }
        catch {
            Write-StatusMessage "Failed to export report: $($_.Exception.Message)" -Level Error
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
    
    # Export report if requested
    Export-Report
    
    Write-StatusMessage "PowerToys Installation Verification Complete" -Level Info
}

# Run the main function
Main

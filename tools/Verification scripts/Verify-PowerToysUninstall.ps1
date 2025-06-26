#Requires -Version 5.1

<#
.SYNOPSIS
    Verifies that PowerToys has been completely uninstalled by checking that all components, registry entries, files, and custom logic have been properly removed.

.DESCRIPTION
    This script comprehensively verifies PowerToys uninstallation by checking that the following have been removed:
    - Registry entries for both per-machine and per-user installations
    - File and folder structure cleanup
    - Module registry functionality cleanup
    - WiX installer cleanup verification
    - DSC module removal
    - Command Palette packages removal
    - Context menu packages cleanup
    - Scheduled tasks cleanup
    - Remaining processes

.PARAMETER InstallScope
    Specifies the installation scope that was uninstalled. Valid values are 'PerMachine' or 'PerUser'.
    Default is 'PerMachine'.

.PARAMETER InstallPath
    Optional. Specifies the installation path that should have been cleaned up. If not provided, the script will
    check the default installation paths based on scope.

.PARAMETER ExportReport
    Exports the verification results to a JSON report file.

.EXAMPLE
    .\Verify-PowerToysUninstall.ps1 -InstallScope PerMachine
    
.EXAMPLE
    .\Verify-PowerToysUninstall.ps1 -InstallScope PerUser
    
.EXAMPLE
    .\Verify-PowerToysUninstall.ps1 -InstallScope PerMachine -ExportReport

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

# Expected PowerToys processes that should not be running
$PowerToysProcesses = @(
    "PowerToys",
    "PowerToys.Settings",
    "PowerToys.AdvancedPaste",
    "PowerToys.Awake", 
    "PowerToys.FancyZones",
    "PowerToys.FancyZonesEditor",
    "PowerToys.FileLocksmithUI",
    "PowerToys.MouseJumpUI",
    "PowerToys.ColorPickerUI",
    "PowerToys.AlwaysOnTop",
    "PowerToys.RegistryPreview",
    "PowerToys.Hosts",
    "PowerToys.PowerRename",
    "PowerToys.ImageResizer",
    "PowerToys.MonacoPreviewHandler",
    "PowerToys.MarkdownPreviewHandler",
    "PowerToys.Peek.UI",
    "PowerToys.MouseWithoutBorders",
    "PowerToys.MouseWithoutBordersHelper",
    "PowerToys.MouseWithoutBordersService",
    "PowerToys.CropAndLock",
    "PowerToys.EnvironmentVariables",
    "PowerToys.WorkspacesSnapshotTool",
    "PowerToys.WorkspacesLauncher",
    "PowerToys.WorkspacesLauncherUI",
    "PowerToys.WorkspacesEditor",
    "PowerToys.WorkspacesWindowArranger",
    "Microsoft.CmdPal.UI",
    "PowerToys.ZoomIt",
    "PowerToys.PowerLauncher"
)

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

function Test-PowerToysUninstallation {
    param(
        [ValidateSet('PerMachine', 'PerUser')]
        [string]$Scope
    )
    
    Write-StatusMessage "Verifying PowerToys $Scope uninstallation..." -Level Info
    
    # Determine registry paths based on scope
    $registryRoot = if ($Scope -eq 'PerMachine') { 'HKLM' } else { 'HKCU' }
    $registryKey = if ($Scope -eq 'PerMachine') { $PowerToysRegistryKeyPerMachine } else { $PowerToysRegistryKeyPerUser }
    
    # Check that main registry key has been removed
    $mainKeyExists = Test-RegistryKey -Path $registryKey
    Add-CheckResult -Category "Registry Cleanup" -CheckName "Main Registry Key Removed ($Scope)" -Status $(if (-not $mainKeyExists) { 'Pass' } else { 'Fail' }) -Message "Registry key properly removed: $registryKey"
    
    # Check that uninstall registry entry has been removed
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
        
        if (-not $powerToysEntry) {
            Add-CheckResult -Category "Registry Cleanup" -CheckName "Uninstall Registry Entry Removed ($Scope)" -Status 'Pass' -Message "PowerToys uninstall entry properly removed from Windows uninstall registry"
        }
        else {
            Add-CheckResult -Category "Registry Cleanup" -CheckName "Uninstall Registry Entry Removed ($Scope)" -Status 'Fail' -Message "PowerToys uninstall entry still exists: $($powerToysEntry.DisplayName)"
        }
    }
    catch {
        Add-CheckResult -Category "Registry Cleanup" -CheckName "Uninstall Registry Entry Removed ($Scope)" -Status 'Warning' -Message "Could not read Windows uninstall registry"
    }
    
    # Check that installation folder has been removed
    $installFolder = Get-ExpectedInstallPath -Scope $Scope
    if (-not (Test-Path $installFolder)) {
        Add-CheckResult -Category "File Cleanup" -CheckName "Install Folder Removed ($Scope)" -Status 'Pass' -Message "Installation folder properly removed: $installFolder"
    }
    else {
        # Check if folder exists but is empty
        $items = Get-ChildItem $installFolder -Force -ErrorAction SilentlyContinue
        if ($items.Count -eq 0) {
            Add-CheckResult -Category "File Cleanup" -CheckName "Install Folder Removed ($Scope)" -Status 'Warning' -Message "Installation folder exists but is empty: $installFolder"
        }
        else {
            Add-CheckResult -Category "File Cleanup" -CheckName "Install Folder Removed ($Scope)" -Status 'Fail' -Message "Installation folder still contains files: $installFolder ($($items.Count) items remaining)"
            
            # List some remaining files for debugging
            $remainingFiles = $items | Select-Object -First 5 | ForEach-Object { $_.Name }
            Add-CheckResult -Category "File Cleanup" -CheckName "Remaining Files ($Scope)" -Status 'Fail' -Message "Some remaining files: $($remainingFiles -join ', ')$(if ($items.Count -gt 5) { '...' })"
        }
    }
    
    return $true
}

function Get-ExpectedInstallPath {
    param(
        [ValidateSet('PerMachine', 'PerUser')]
        [string]$Scope
    )
    
    if ($InstallPath) {
        return $InstallPath
    }
    
    # Use the default installation paths based on scope
    if ($Scope -eq 'PerMachine') {
        return "${env:ProgramFiles}\PowerToys"
    }
    else {
        return "${env:LOCALAPPDATA}\PowerToys"
    }
}

function Test-RegistryHandlersCleanup {
    param(
        [string]$Scope
    )
    
    $registryRoot = if ($Scope -eq 'PerMachine') { 'HKLM:' } else { 'HKCU:' }
    
    # Test URL protocol handler removal
    $protocolPath = "$registryRoot\SOFTWARE\Classes\powertoys"
    if (-not (Test-RegistryKey -Path $protocolPath)) {
        Add-CheckResult -Category "Registry Handlers Cleanup" -CheckName "PowerToys URL Protocol Removed ($Scope)" -Status 'Pass' -Message "URL protocol properly removed"
    }
    else {
        Add-CheckResult -Category "Registry Handlers Cleanup" -CheckName "PowerToys URL Protocol Removed ($Scope)" -Status 'Fail' -Message "URL protocol still registered"
    }
    
    # Test CLSID cleanup for toast notifications
    $toastClsidPath = "$registryRoot\SOFTWARE\Classes\CLSID\{DD5CACDA-7C2E-4997-A62A-04A597B58F76}"
    if (-not (Test-RegistryKey -Path $toastClsidPath)) {
        Add-CheckResult -Category "Registry Handlers Cleanup" -CheckName "Toast Notification CLSID Removed ($Scope)" -Status 'Pass' -Message "Toast notification CLSID properly removed"
    }
    else {
        Add-CheckResult -Category "Registry Handlers Cleanup" -CheckName "Toast Notification CLSID Removed ($Scope)" -Status 'Fail' -Message "Toast notification CLSID still registered"
    }
}

function Get-DocumentsPath {
    # Use Windows Shell API to get the real Documents folder path (same as C++ code)
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
            return "$env:USERPROFILE\Documents"
        }
    }
    catch {
        return "$env:USERPROFILE\Documents"
    }
}

function Test-DSCModuleCleanup {
    param(
        [string]$Scope
    )
    
    if ($Scope -eq 'PerUser') {
        # For per-user installations, DSC module should be removed from user's Documents
        $documentsPath = Get-DocumentsPath
        $userModulesPath = Join-Path $documentsPath "PowerShell\Modules\Microsoft.PowerToys.Configure"
        
        if (-not (Test-Path $userModulesPath)) {
            Add-CheckResult -Category "DSC Module Cleanup" -CheckName "DSC Module Removed (PerUser)" -Status 'Pass' -Message "DSC module properly removed from user profile: $userModulesPath"
        }
        else {
            # Check if directory exists but is empty
            $items = Get-ChildItem $userModulesPath -ErrorAction SilentlyContinue
            if ($items.Count -eq 0) {
                Add-CheckResult -Category "DSC Module Cleanup" -CheckName "DSC Module Removed (PerUser)" -Status 'Warning' -Message "DSC module directory exists but is empty: $userModulesPath"
            }
            else {
                Add-CheckResult -Category "DSC Module Cleanup" -CheckName "DSC Module Removed (PerUser)" -Status 'Fail' -Message "DSC module still exists in user profile: $userModulesPath"
            }
        }
    }
    else {
        # For per-machine installations, DSC module should be removed from system WindowsPowerShell modules
        $systemModulesPath = "${env:ProgramFiles}\WindowsPowerShell\Modules\Microsoft.PowerToys.Configure"
        
        if (-not (Test-Path $systemModulesPath)) {
            Add-CheckResult -Category "DSC Module Cleanup" -CheckName "DSC Module Removed (PerMachine)" -Status 'Pass' -Message "DSC module properly removed from system modules: $systemModulesPath"
        }
        else {
            # Check if directory exists but is empty
            $items = Get-ChildItem $systemModulesPath -ErrorAction SilentlyContinue
            if ($items.Count -eq 0) {
                Add-CheckResult -Category "DSC Module Cleanup" -CheckName "DSC Module Removed (PerMachine)" -Status 'Warning' -Message "DSC module directory exists but is empty: $systemModulesPath"
            }
            else {
                Add-CheckResult -Category "DSC Module Cleanup" -CheckName "DSC Module Removed (PerMachine)" -Status 'Fail' -Message "DSC module still exists in system modules: $systemModulesPath"
            }
        }
    }
}

function Test-CommandPalettePackagesCleanup {
    # Command Palette packages are MSIX packages that should be unregistered
    try {
        # Check if Command Palette package is still registered
        $cmdPalPackages = Get-AppxPackage -Name "*CommandPalette*" -ErrorAction SilentlyContinue
        
        if ($cmdPalPackages.Count -eq 0) {
            Add-CheckResult -Category "Command Palette Cleanup" -CheckName "CmdPal Package Unregistered" -Status 'Pass' -Message "Command Palette packages properly unregistered"
        }
        else {
            Add-CheckResult -Category "Command Palette Cleanup" -CheckName "CmdPal Package Unregistered" -Status 'Fail' -Message "Found $($cmdPalPackages.Count) remaining Command Palette package(s): $($cmdPalPackages.Name -join ', ')"
        }
    }
    catch {
        Add-CheckResult -Category "Command Palette Cleanup" -CheckName "CmdPal Package Unregistered" -Status 'Warning' -Message "Could not check Command Palette package registration status"
    }
}

function Test-ContextMenuPackagesCleanup {
    # Context menu packages are sparse MSIX packages that should be unregistered
    $contextMenuPackageNames = @(
        "*ImageResizer*",
        "*FileLocksmith*", 
        "*PowerRename*",
        "*NewPlus*"
    )
    
    try {
        $remainingPackages = @()
        
        foreach ($packagePattern in $contextMenuPackageNames) {
            $packages = Get-AppxPackage -Name $packagePattern -ErrorAction SilentlyContinue
            if ($packages) {
                $remainingPackages += $packages
            }
        }
        
        if ($remainingPackages.Count -eq 0) {
            Add-CheckResult -Category "Context Menu Cleanup" -CheckName "Context Menu Packages Unregistered" -Status 'Pass' -Message "All context menu packages properly unregistered"
        }
        else {
            $packageNames = $remainingPackages | ForEach-Object { $_.Name }
            Add-CheckResult -Category "Context Menu Cleanup" -CheckName "Context Menu Packages Unregistered" -Status 'Fail' -Message "Found $($remainingPackages.Count) remaining context menu package(s): $($packageNames -join ', ')"
        }
    }
    catch {
        Add-CheckResult -Category "Context Menu Cleanup" -CheckName "Context Menu Packages Unregistered" -Status 'Warning' -Message "Could not check context menu package registration status"
    }
}

function Test-ScheduledTasksCleanup {
    # Check that PowerToys scheduled tasks have been removed
    try {
        $powerToysScheduledTasks = Get-ScheduledTask | Where-Object { 
            $_.TaskPath -like "*PowerToys*" -or $_.TaskName -like "*PowerToys*" 
        }
        
        if ($powerToysScheduledTasks.Count -eq 0) {
            Add-CheckResult -Category "Scheduled Tasks Cleanup" -CheckName "Scheduled Tasks Removed" -Status 'Pass' -Message "All PowerToys scheduled tasks properly removed"
        }
        else {
            $taskNames = $powerToysScheduledTasks | ForEach-Object { "$($_.TaskPath)\$($_.TaskName)" }
            Add-CheckResult -Category "Scheduled Tasks Cleanup" -CheckName "Scheduled Tasks Removed" -Status 'Fail' -Message "Found $($powerToysScheduledTasks.Count) remaining scheduled task(s): $($taskNames -join ', ')"
        }
    }
    catch {
        Add-CheckResult -Category "Scheduled Tasks Cleanup" -CheckName "Scheduled Tasks Removed" -Status 'Warning' -Message "Could not check scheduled tasks status"
    }
}

function Test-ProcessesCleanup {
    # Check that no PowerToys processes are still running
    $runningProcesses = @()
    
    foreach ($processName in $PowerToysProcesses) {
        $processes = Get-Process -Name $processName -ErrorAction SilentlyContinue
        if ($processes) {
            $runningProcesses += $processes
        }
    }
    
    if ($runningProcesses.Count -eq 0) {
        Add-CheckResult -Category "Process Cleanup" -CheckName "PowerToys Processes Stopped" -Status 'Pass' -Message "No PowerToys processes are running"
    }
    else {
        $processNames = $runningProcesses | ForEach-Object { "$($_.ProcessName) (PID: $($_.Id))" }
        Add-CheckResult -Category "Process Cleanup" -CheckName "PowerToys Processes Stopped" -Status 'Fail' -Message "Found $($runningProcesses.Count) running PowerToys process(es): $($processNames -join ', ')"
    }
}

function Test-ServicesCleanup {
    # Check that PowerToys services have been removed
    try {
        $powerToysServices = Get-Service | Where-Object { 
            $_.Name -like "*PowerToys*" -or $_.DisplayName -like "*PowerToys*" 
        }
        
        if ($powerToysServices.Count -eq 0) {
            Add-CheckResult -Category "Services Cleanup" -CheckName "PowerToys Services Removed" -Status 'Pass' -Message "All PowerToys services properly removed"
        }
        else {
            $serviceNames = $powerToysServices | ForEach-Object { "$($_.Name) ($($_.DisplayName))" }
            Add-CheckResult -Category "Services Cleanup" -CheckName "PowerToys Services Removed" -Status 'Fail' -Message "Found $($powerToysServices.Count) remaining service(s): $($serviceNames -join ', ')"
        }
    }
    catch {
        Add-CheckResult -Category "Services Cleanup" -CheckName "PowerToys Services Removed" -Status 'Warning' -Message "Could not check services status"
    }
}

function Test-StartMenuCleanup {
    # Check that Start Menu shortcuts have been removed
    $startMenuPaths = @(
        "${env:ProgramData}\Microsoft\Windows\Start Menu\Programs\PowerToys",
        "${env:APPDATA}\Microsoft\Windows\Start Menu\Programs\PowerToys"
    )
    
    $foundShortcuts = @()
    
    foreach ($path in $startMenuPaths) {
        if (Test-Path $path) {
            $items = Get-ChildItem $path -ErrorAction SilentlyContinue
            if ($items.Count -gt 0) {
                $foundShortcuts += @{
                    Path = $path
                    Items = $items
                }
            }
        }
    }
    
    if ($foundShortcuts.Count -eq 0) {
        Add-CheckResult -Category "Start Menu Cleanup" -CheckName "Start Menu Shortcuts Removed" -Status 'Pass' -Message "Start Menu shortcuts properly removed"
    }
    else {
        foreach ($shortcut in $foundShortcuts) {
            $itemNames = $shortcut.Items | ForEach-Object { $_.Name }
            Add-CheckResult -Category "Start Menu Cleanup" -CheckName "Start Menu Shortcuts Removed" -Status 'Fail' -Message "Found shortcuts in $($shortcut.Path): $($itemNames -join ', ')"
        }
    }
}

function Test-TempFilesCleanup {
    # Check for common PowerToys temp files that should be cleaned up
    $tempPaths = @(
        "${env:TEMP}\PowerToys*",
        "${env:LOCALAPPDATA}\Temp\PowerToys*"
    )
    
    $foundTempFiles = @()
    
    foreach ($pattern in $tempPaths) {
        $items = Get-ChildItem -Path $pattern -ErrorAction SilentlyContinue
        if ($items) {
            $foundTempFiles += $items
        }
    }
    
    if ($foundTempFiles.Count -eq 0) {
        Add-CheckResult -Category "Temp Files Cleanup" -CheckName "Temp Files Cleaned" -Status 'Pass' -Message "PowerToys temp files properly cleaned"
    }
    else {
        $fileNames = $foundTempFiles | Select-Object -First 5 | ForEach-Object { $_.Name }
        Add-CheckResult -Category "Temp Files Cleanup" -CheckName "Temp Files Cleaned" -Status 'Warning' -Message "Found $($foundTempFiles.Count) PowerToys temp file(s): $($fileNames -join ', ')$(if ($foundTempFiles.Count -gt 5) { '...' })"
    }
}

function Export-Report {
    if ($ExportReport) {
        $timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
        $reportPath = "PowerToys_Uninstall_Verification_Report_$timestamp.json"
        
        try {
            $script:Results | ConvertTo-Json -Depth 10 | Out-File -FilePath $reportPath -Encoding UTF8
            Write-StatusMessage "Uninstall verification report exported to: $reportPath" -Level Info
        }
        catch {
            Write-StatusMessage "Failed to export report: $($_.Exception.Message)" -Level Error
        }
    }
}

# Main execution
function Main {
    Write-StatusMessage "Starting PowerToys Uninstallation Verification" -Level Info
    Write-StatusMessage "Scope: $InstallScope" -Level Info
    
    # Test the specified scope uninstallation
    Test-PowerToysUninstallation -Scope $InstallScope
    Test-RegistryHandlersCleanup -Scope $InstallScope
    Test-DSCModuleCleanup -Scope $InstallScope
    
    # Test common cleanup items (regardless of install scope)
    Test-CommandPalettePackagesCleanup
    Test-ContextMenuPackagesCleanup
    Test-ScheduledTasksCleanup
    Test-ProcessesCleanup
    Test-ServicesCleanup
    Test-StartMenuCleanup
    Test-TempFilesCleanup
    
    # Calculate overall status
    if ($script:Results.Summary.FailedChecks -eq 0) {
        if ($script:Results.Summary.WarningChecks -eq 0) {
            $script:Results.Summary.OverallStatus = "Clean Uninstall"
        }
        else {
            $script:Results.Summary.OverallStatus = "Clean with Warnings"
        }
    }
    else {
        $script:Results.Summary.OverallStatus = "Incomplete Uninstall"
    }
    
    # Display summary
    Write-Host "`n" -NoNewline
    Write-StatusMessage "=== UNINSTALL VERIFICATION SUMMARY ===" -Level Info
    Write-StatusMessage "Total Checks: $($script:Results.Summary.TotalChecks)" -Level Info
    Write-StatusMessage "Passed: $($script:Results.Summary.PassedChecks)" -Level Success
    Write-StatusMessage "Failed: $($script:Results.Summary.FailedChecks)" -Level Error
    Write-StatusMessage "Warnings: $($script:Results.Summary.WarningChecks)" -Level Warning
    Write-StatusMessage "Overall Status: $($script:Results.Summary.OverallStatus)" -Level $(
        switch ($script:Results.Summary.OverallStatus) {
            'Clean Uninstall' { 'Success' }
            'Clean with Warnings' { 'Warning' }
            default { 'Error' }
        }
    )
    
    # Export report if requested
    Export-Report
    
    Write-StatusMessage "PowerToys Uninstallation Verification Complete" -Level Info
}

# Run the main function
Main

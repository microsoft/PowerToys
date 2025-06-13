# Bug Report Tool

The Bug Report Tool is a utility that collects logs and system information to help diagnose issues with PowerToys. It creates a comprehensive report that can be shared with developers to help troubleshoot problems.

## Location and Access

- Source code: `/tools/BugReportTool/`
- Users can trigger the tool via:
  - Right-click on PowerToys tray icon → Report Bug
  - Left-click on tray icon → Open Settings → Bug Report Tool

## What It Does

The Bug Report Tool creates a zip file on the desktop named "PowerToys_Report_[date]_[time].zip" containing logs and system information. It:

1. Copies logs from PowerToys application directories
2. Collects system information relevant to PowerToys functionality
3. Redacts sensitive information
4. Packages everything into a single zip file for easy sharing

## Information Collected

### Logs
- Copies logs from:
  - `%LOCALAPPDATA%\Microsoft\PowerToys\Logs` - Regular logs
  - `%USERPROFILE%\AppData\LocalLow\Microsoft\PowerToys` - Low-privilege logs

### System Information
- Windows version and build information
- Language and locale settings
- Monitor information (crucial for FancyZones and multi-monitor scenarios)
- .NET installation details
- PowerToys registry entries
- Group Policy Object (GPO) settings
- Application compatibility mode settings
- Event Viewer logs related to PowerToys executables
- PowerToys installer logs
- Windows 11 context menu package information

### PowerToys Configuration
- Settings files
- Module configurations
- Installation details
- File structure and integrity (with hashes)

## Key Files in the Report

- `compatibility_tab_info.txt` - Application compatibility mode settings
- `installed_context_menu_packages.txt` - Windows 11 context menu package information
- `dotnet_installation_info.txt` - Installed .NET versions
- `event_viewer.xml` - Event Viewer logs for PowerToys processes
- `gpo_configuration.txt` - Group Policy settings affecting PowerToys
- `installation_folder_structure.txt` - Lists all PowerToys files with hashes
- `last_version_run.json` - Information about the last PowerToys version used
- `monitor_report_info.txt` - Monitor configuration details
- `general_settings.json` - PowerToys general settings
- `windows_version.txt` - OS version information
- `windows_settings.txt` - OS settings like language and regional settings

## Privacy Considerations

The tool redacts certain types of private information:
- Mouse Without Borders security keys
- FancyZones app zone history
- User-specific paths
- Machine names

## Implementation Details

The tool is implemented as a C# console application that:
1. Creates a temporary directory
2. Copies logs and configuration files to this directory
3. Runs commands to collect system information
4. Redacts sensitive information
5. Compresses everything into a zip file
6. Cleans up the temporary directory

### Core Components

- `BugReportTool.exe` - Main executable
- Helper classes for collecting specific types of information
- Redaction logic to remove sensitive data

## Extending the Bug Report Tool

When adding new PowerToys features, the Bug Report Tool may need to be updated to collect relevant information. Areas to consider:

1. New log locations to include
2. Additional registry keys to examine
3. New GPO values to report
4. Process names to include in Event Viewer data collection
5. New configuration files to include

## Build Process

The Bug Report Tool is built separately from the main PowerToys solution:

1. Path from root: `tools\BugReportTool\BugReportTool.sln`
2. Must be built before building the installer
3. Built version is included in the PowerToys installer

### Building from the Command Line

```
nuget restore .\tools\BugReportTool\BugReportTool.sln
msbuild -p:Platform=x64 -p:Configuration=Release .\tools\BugReportTool\BugReportTool.sln
```

### Building from Visual Studio

1. Open `tools\BugReportTool\BugReportTool.sln`
2. Set the Solution Configuration to `Release`
3. Build the solution

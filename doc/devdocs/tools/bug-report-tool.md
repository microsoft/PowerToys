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

* `compatibility-tab-info.txt` - Information about [compatibility settings](https://support.microsoft.com/windows/make-older-apps-or-programs-compatible-with-windows-783d6dd7-b439-bdb0-0490-54eea0f45938) set for certain PowerToys executables both in the user and system scope.
* `context-menu-packages.txt` - Information about the packages that are registered for the new Windows 11 context menu.
* `dotnet-installation-info.txt` - Information about the installed .NET versions.
* `EventViewer-*.xml` - These files contain event logs from the Windows Event Viewer for the executable specified in the file name.
* `EventViewer-Microsoft-Windows-AppXDeploymentServer/Operational.xml` - Contains event logs from the AppXDeployment-Server which are useful for diagnosing MSIX installation issues.
* `gpo-configuration-info.txt` - Information about the configured [GPO](doc/devdocs/processes/gpo.md).
* `installationFolderStructure.txt` - Information about the folder structure of the installation. All lines with files have the following structure: `FileName Version MD5Hash`.
* `last_version_run.json` - Information about the last version of PowerToys that was run.
* `log_settings.json` - Information about the log level settings.
* `monitor-report-info.txt` - Information about the monitors connected to the system. This file is created by the [monitor info report tool](/doc/devdocs/tools/monitor-info-report.md).
* `oobe_settings.json` - Information about the OOBE settings.
* `registry-report-info.txt` - Information about the registry keys that are used by PowerToys.
* `settings_placement.json` - Information about the placement of the settings window.
* `settings-telemetry.json` - Information about the last time telemetry data was sent.
* `UpdateState.json` - Information about the last update check and the current status of the update download.
* `windows-settings.txt` - Information about the Windows language settings.
* `windows-version.txt` - Information about the Windows version.

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
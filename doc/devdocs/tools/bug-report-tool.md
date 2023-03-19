# [Bug report tool](/tools/BugReportTool/)

This tool is used to collect logs and system information for bug reports. The bug report is then saved as a zip file on the desktop.

## Launching

It can launch from the PowerToys tray icon by clicking "Report Bug", by clicking the bug report icon in the PowerToys flyout  or by running the executable directly.

## Included files

The bug report includes the following files:

* Settings files of the modules.
* Logs of the modules and the runner.
* Update log files.
* `compatibility-tab-info.txt` - Information about [compatibility settings](https://support.microsoft.com/windows/make-older-apps-or-programs-compatible-with-windows-783d6dd7-b439-bdb0-0490-54eea0f45938) set for certain PowerToys executables both in the user and system scope.
* `context-menu-packages.txt` - Information about the packages that are registered for the new Windows 11 context menu.
* `dotnet-installation-info.txt` - Information about the installed .NET versions.
* `EventViewer-*.xml` - These files contain event logs from the Windows Event Viewer for the executable specified in the file name.
* `gpo-configuration-info.txt` - Information about the configured [GPO](/doc/gpo/README.md).
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

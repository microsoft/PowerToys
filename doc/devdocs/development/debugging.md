# Debugging PowerToys

This document covers techniques and tools for debugging PowerToys.

## Pre-Debugging Setup

Before you can start debugging PowerToys, you need to set up your development environment:

1. Fork the repository and clone it to your machine
2. Navigate to the repository root directory
3. Run `git submodule update --init --recursive` to initialize all submodules
4. Change directory to `.config` and run `winget configure .\configuration.vsEnterprise.winget` (pick the configuration file that matches your Visual Studio distribution)

### Optional: Building Outside Visual Studio

You can build the entire solution from the command line, which is sometimes faster than building within Visual Studio:

1. Open Developer Command Prompt for VS 2022
2. Navigate to the repository root directory
3. Run the following command(don't forget to set the correct platform):
   ```pwsh
   msbuild -restore -p:RestorePackagesConfig=true -p:Platform=ARM64 -m PowerToys.slnx /tl /p:NuGetInteractive="true"
   ```
4. This process should complete in approximately 13-14 minutes for a full build

## Debugging Techniques

### Visual Studio Debugging

To debug the PowerToys application in Visual Studio, set the `runner` project as your start-up project, then start the debugger.

Some PowerToys modules must be run with the highest permission level if the current user is a member of the Administrators group. The highest permission level is required to be able to perform some actions when an elevated application (e.g. Task Manager) is in the foreground or is the target of an action. Without elevated privileges some PowerToys modules will still work but with some limitations:

- The `FancyZones` module will not be able to move an elevated window to a zone.
- The `Shortcut Guide` module will not appear if the foreground window belongs to an elevated application.

Therefore, it is recommended to run Visual Studio with elevated privileges when debugging these scenarios. If you want to avoid running Visual Studio with elevated privileges and don't mind the limitations described above, you can do the following: open the `runner` project properties and navigate to the `Linker -> Manifest File` settings, edit the `UAC Execution Level` property and change it from `highestAvailable (level='highestAvailable')` to `asInvoker (/level='asInvoker').

### Shell Process Debugging Tool

The Shell Process Debugging Tool is a Visual Studio extension that helps debug multiple processes, which is especially useful for PowerToys modules started by the runner.

#### Debugging Setup Process

1. Install ["Debug Child Processes"](https://marketplace.visualstudio.com/items?itemName=vsdbgplat.MicrosoftChildProcessDebuggingPowerTool2022) Visual Studio extension
2. Configure which processes to debug and what debugger to use for each
3. Start PowerToys from Visual Studio
4. The extension will automatically attach to specified child processes when launched

#### Debugging Color Picker Example

1. Set breakpoints in both ColorPicker and its module interface
2. Use Shell Process Debugging to attach to ColorPickerUI.exe
3. Debug .NET and native code together
4. Runner needs to be running to properly test activation

#### Debugging DLL Main/Module Interface

- Breakpoints in DLL code will be hit when loaded by runner
- No special setup needed as DLL is loaded into runner process

#### Debugging Short-Lived Processes

- For processes with short lifetimes (like in Workspaces)
- List all processes explicitly in debugging configuration
- Set correct debugger type (.NET debugger for C# code)

### Finding Registered Events

1. Run WinObj tool from SysInternals as administrator
2. Search for event name
3. Shows handles to the event (typically runner and module)

### Common Debugging Usage Patterns

#### Debugging with Bug Report
1. Check module-specific logs for exceptions/crashes
2. Copy user's settings to your AppData to reproduce their configuration
3. Check Event Viewer XML files if logs don't show crashes
4. Compare installation_folder_structure.txt to detect corrupted installations
5. Check installer logs for installation-related issues
6. Look at Windows version and language settings for patterns across users

#### Installer Debugging
- Can only build installer in Release mode
- Typically debug using logs and message boxes
- Logs located in:
  - `%LOCALAPPDATA%\Temp\PowerToys_bootstrapper_*.log` - MSI tool logs
  - `%LOCALAPPDATA%\Temp\PowerToys_*.log` - Custom installer logs
- Logs in Bug Reports are useful for troubleshooting installation issues

#### Settings UI Debugging
- Use shell process debugging to connect to newly created processes
- Debug the `PowerToys.Settings.exe` process
- Add breakpoints as needed for troubleshooting
- Logs are stored in the local app directory: `%LOCALAPPDATA%\Microsoft\PowerToys`
- Check Event Viewer for application crashes related to `PowerToys.Settings.exe`
- Crash dumps can be obtained from Event Viewer

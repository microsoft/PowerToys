# Logging and Telemetry in PowerToys

## Logging Types in PowerToys

PowerToys has several types of logging mechanisms:
1. Text file logs (application writes logs to files)
2. Telemetry/diagnostic data (sent to Microsoft servers)
3. Event Viewer logs (used by some utilities like Mouse Without Borders)
4. Watson reports (crash reports sent to Microsoft)

## Log File Locations

### Regular Logs
- Located at: `%LOCALAPPDATA%\Microsoft\PowerToys\Logs`
- Organized by utility and sometimes by version
- Examples: PowerToys Run logs, module interface logs
- C# and C++ components both write logs to these locations

### Low-Privilege Logs
- Some components (like preview handlers and thumbnail providers) are started by Explorer and have low privileges
- These components write logs to: `%USERPROFILE%/AppData/LocalLow/Microsoft/PowerToys`
- Example: Monaco preview handler logs

### Module Logs
- Logs always stored in user's AppData regardless of installation type
- Each module creates its own log
- Even with machine-wide installation, logs are per-user
- Different users can have different logs even with a machine-wide installation

## Log Implementation

### C++ Logging

In C++ projects we use the awesome [spdlog](https://github.com/gabime/spdlog) library for logging as a git submodule under the `deps` directory. To use it in your project, just include [spdlog.props](/deps/spdlog.props) in a .vcxproj like this:

```xml
<Import Project="..\..\..\deps\spdlog.props" />
```
It'll add the required include dirs and link the library binary itself.

- Projects need to include the logging project as a dependency
- Uses a git submodule for the actual logging library
- Logs are initialized in the main file:
  ```cpp
  init_logger();
  ```
- After initialization, any file can use the logger
- Logger settings contain constants like log file locations

### C# Logging

For C# projects there is a static logger class in Managed Common called `Logger`.

To use it, add a project reference to `ManagedCommon` and add the following line of code to all the files using the logger:

```Csharp
using ManagedCommon;
```

In the `Main` function (or a function with a similar meaning (like `App` in a `App.xaml.cs` file)) you have to call `InitializeLogger` and specify the location where the logs will be saved (always use a path scheme similar to this example):

```Csharp
Logger.InitializeLogger("\\FancyZones\\Editor\\Logs");
```

For a low-privilege process you have to set the optional second parameter to `true`:

```Csharp
Logger.InitializeLogger("\\FileExplorer\\Monaco\\Logs", true);
```

The `Logger` class contains the following logging functions:

```Csharp
// Logs an error that the utility encountered
Logger.LogError(string message);
Logger.LogError(string message, Exception ex);
// Logs an error that isn't that grave
Logger.LogWarning(string message);
// Logs what the app is doing at the moment
Logger.LogInfo(string message);
// Like LogInfo just with infos important for debugging
Logger.LogDebug(string message);
// Logs the current state of the utility.
Logger.LogTrace();
```

## Log File Management
- Currently, most logs are not automatically cleaned up
- Some modules have community contributions to clean old logs, but not universally implemented
- By default, all info-level logs are written
- Debug and trace logs may not be written by default
- Log settings can be found in settings.json, but not all APIs honor these settings

## Telemetry

### Implementation
- Uses Event Tracing for Windows (ETW) for telemetry
- Different from the text file logging system
- Keys required to send telemetry to the right server
  - Keys are not stored in the repository
  - Obfuscated in public code
  - Replaced during the release process
  - Stored in private NuGet packages for release builds

### C++ Telemetry
- Managed through trace_base.h which:
  - Registers the provider
  - Checks if user has disabled diagnostics
  - Defines events
- Example from Always On Top:
  ```cpp
  Trace::AlwaysOnTop::Enable(true);
  ```

### C# Telemetry
- Uses PowerToysTelemetry class
- WriteEvent method sends telemetry
- Projects add a reference to the PowerToys.Telemetry project
- Example:
  ```csharp
  PowerToysTelemetry.Log.WriteEvent(new LauncherShowEvent(hotKey));
  ```

### User Controls
- Settings page allows users to:
  - Turn off/on sending telemetry
  - Enable viewing of telemetry data

### Viewing Telemetry Data
- When "Enable viewing" is turned on, PowerToys starts ETW tracing
- Saves ETL files for 28 days
- Located at: `%LOCALAPPDATA%\Microsoft\PowerToys\ETL` (for most utilities)
- Low-privilege components save to a different location
- Button in settings converts ETL to XML for user readability
- XML format chosen to follow approved compliance pattern from Windows Subsystem for Android
- Files older than 28 days are automatically deleted

## Bug Report Tool

The [BugReportTool](/tools/BugReportTool) can be triggered via:
- Right-click on PowerToys tray icon → Report Bug
- Left-click on tray icon → Open Settings → Bug Report Tool

It creates a zip file on desktop named "PowerToys_Report_[date]_[time].zip" containing logs and system information.

See [Bug Report Tool](../tools/bug-report-tool.md) for more detailed information about the tool.

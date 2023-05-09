# Logging

Logging plays an important part in determining bugs in our code. It provides context for the developers about where and when errors occur.

## Where are the logs saved

* Most of the logs are saved under `%LOCALAPPDATA%/Microsoft/PowerToys`.
* For low-privilege processes (like preview handlers) the logs are saved under `%USERPROFILE%/AppData/LocalLow/Microsoft/PowerToys`.

Logs are normally in a subfolder with the module name as title.

The [BugReportTool](/tools/BugReportTool) will take logs from both locations when executed.

## Using a logger in a project

### Spdlog

In C++ projects we use the awesome [spdlog](https://github.com/gabime/spdlog) library for logging as a git submodule under the `deps` directory. To use it in your project, just include [spdlog.props](/deps/spdlog.props) in a .vcxproj like this:

```xml
<Import Project="..\..\..\deps\spdlog.props" />
```
It'll add the required include dirs and link the library binary itself.

### PowerToys Logger in ManagedCommon

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

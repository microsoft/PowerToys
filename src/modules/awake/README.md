# PowerToys Awake Module

A PowerToys utility that prevents Windows from sleeping and/or turning off the display.

**Author:** [Den Delimarsky](https://den.dev)

## Resources

- [Awake Website](https://awake.den.dev) - Official documentation and guides
- [Microsoft Learn Documentation](https://learn.microsoft.com/windows/powertoys/awake) - Usage instructions and feature overview
- [GitHub Issues](https://github.com/microsoft/PowerToys/issues?q=is%3Aissue+label%3AProduct-Awake) - Report bugs or request features

## Overview

The Awake module consists of three projects:

| Project | Purpose |
|---------|---------|
| `Awake/` | Main WinExe application with CLI support |
| `Awake.ModuleServices/` | Service layer for PowerToys integration |
| `AwakeModuleInterface/` | C++ native module bridge |

## How It Works

The module uses the Win32 `SetThreadExecutionState()` API to signal Windows that the system should remain awake:

- `ES_SYSTEM_REQUIRED` - Prevents system sleep
- `ES_DISPLAY_REQUIRED` - Prevents display sleep
- `ES_CONTINUOUS` - Maintains state until explicitly changed

## Operating Modes

| Mode | Description |
|------|-------------|
| **PASSIVE** | Normal power behavior (off) |
| **INDEFINITE** | Keep awake until manually stopped |
| **TIMED** | Keep awake for a specified duration |
| **EXPIRABLE** | Keep awake until a specific date/time |

## Command-Line Usage

Awake can be run standalone with the following options:

```
PowerToys.Awake.exe [options]

Options:
  -c, --use-pt-config    Use PowerToys configuration file
  -d, --display-on       Keep display on (default: true)
  -t, --time-limit       Time limit in seconds
  -p, --pid              Process ID to bind to
  -e, --expire-at        Expiration date/time
  -u, --use-parent-pid   Bind to parent process
```

### Examples

Keep system awake indefinitely:
```powershell
PowerToys.Awake.exe
```

Keep awake for 1 hour with display on:
```powershell
PowerToys.Awake.exe --time-limit 3600 --display-on
```

Keep awake until a specific time:
```powershell
PowerToys.Awake.exe --expire-at "2024-12-31 23:59:59"
```

Keep awake while another process is running:
```powershell
PowerToys.Awake.exe --pid 1234
```

## Architecture

### Design Highlights

1. **Pure Win32 API for Tray UI** - No WPF/WinForms dependencies, keeping the binary small. Uses direct `Shell_NotifyIcon` API for tray icon management.

2. **Reactive Extensions (Rx.NET)** - Used for timed operations via `Observable.Interval()` and `Observable.Timer()`. File system watching uses 25ms throttle to debounce rapid config changes.

3. **Custom SynchronizationContext** - Queue-based message dispatch ensures tray operations run on a dedicated thread for thread-safe UI updates.

4. **Dual-Mode Operation**
   - Standalone: Command-line arguments only
   - Integrated: PowerToys settings file + process binding

5. **Process Binding** - The `--pid` parameter keeps the system awake only while a target process runs, with auto-exit when the parent PowerToys runner terminates.

## Key Files

| File | Purpose |
|------|---------|
| `Program.cs` | Entry point & CLI parsing |
| `Core/Manager.cs` | State orchestration & power management |
| `Core/TrayHelper.cs` | System tray UI management |
| `Core/Native/Bridge.cs` | Win32 P/Invoke declarations |
| `Core/Threading/SingleThreadSynchronizationContext.cs` | Threading utilities |

## Building

### Prerequisites

- Visual Studio 2022 with C++ and .NET workloads
- Windows SDK 10.0.26100.0 or later

### Build Commands

From the `src/modules/awake` directory:

```powershell
# Using the build script
.\scripts\Build-Awake.ps1

# Or with specific configuration
.\scripts\Build-Awake.ps1 -Configuration Debug -Platform x64
```

Or using MSBuild directly:

```powershell
msbuild Awake\Awake.csproj /p:Configuration=Release /p:Platform=x64
```

## Dependencies

- **System.CommandLine** - Command-line parsing
- **System.Reactive** - Rx.NET for timer management
- **PowerToys.ManagedCommon** - Shared PowerToys utilities
- **PowerToys.Settings.UI.Lib** - Settings integration
- **PowerToys.Interop** - Native interop layer

## Configuration

When running with PowerToys (`--use-pt-config`), settings are stored in:
```
%LOCALAPPDATA%\Microsoft\PowerToys\Awake\settings.json
```

## Known Limitations

### Task Scheduler Idle Detection ([#44134](https://github.com/microsoft/PowerToys/issues/44134))

When "Keep display on" is enabled, Awake uses the `ES_DISPLAY_REQUIRED` flag which blocks Windows Task Scheduler from detecting the system as idle. This prevents scheduled maintenance tasks (like SSD TRIM, disk defragmentation, and other idle-triggered tasks) from running.

Per [Microsoft's documentation](https://learn.microsoft.com/en-us/windows/win32/taskschd/task-idle-conditions):

> "An exception would be for any presentation type application that sets the ES_DISPLAY_REQUIRED flag. This flag forces Task Scheduler to not consider the system as being idle, regardless of user activity or resource consumption."

**Workarounds:**

1. **Disable "Keep display on"** - With this setting off, Awake only uses `ES_SYSTEM_REQUIRED` which still prevents sleep but allows Task Scheduler to detect idle state.

2. **Manually run maintenance tasks** - For example, to run TRIM manually:
   ```powershell
   # Run as Administrator
   Optimize-Volume -DriveLetter C -ReTrim -Verbose
   ```

## Telemetry

The module emits telemetry events for:
- Keep-awake mode changes (indefinite, timed, expirable, passive)
- Privacy-compliant event tagging via `Microsoft.PowerToys.Telemetry`

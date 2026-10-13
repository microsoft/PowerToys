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

Awake can be run standalone with human-readable commands or the legacy options:

```
PowerToys.Awake.exe for <duration> [options]
PowerToys.Awake.exe until <time> [options]
PowerToys.Awake.exe [options]

Options:
  -c, --use-pt-config    Use PowerToys configuration file
  -d, --display-on       Keep display on (default: false)
  -t, --time-limit       Time limit in seconds
  -p, --pid              Process ID to bind to
  -e, --expire-at        Expiration date/time
  -u, --use-parent-pid,
      --parent           Bind to parent process
```

### Examples

Keep system awake indefinitely:
```powershell
PowerToys.Awake.exe
```

Keep awake for 1 hour with display on:
```powershell
PowerToys.Awake.exe for 1h --display-on
```

Keep awake until the next occurrence of a local time:
```powershell
PowerToys.Awake.exe until 18:00
```

Keep awake until a specific date and time:
```powershell
PowerToys.Awake.exe until "2026-12-31 23:59:59"
```

Keep awake while another process is running:
```powershell
PowerToys.Awake.exe --pid 1234
```

Put `for` or `until` before the options. Durations accept whole-number `d`, `h`, `m`,
and `s` components (for example, `1h30m`); zero, fractional, and overflowing durations
are rejected. A time without a date means its next local occurrence. Nonexistent
daylight-saving times are rejected, and explicit dates must be in the future.

Combining a modern command with `--pid` or `--parent` ends the session at the deadline
or when the process exits, whichever comes first. Do not combine these commands with
`--use-pt-config`, `--time-limit`, or `--expire-at`; use the legacy options separately
for existing scripts. Legacy process-binding precedence and console behavior are
unchanged. `--pid` and `--expire-at` now require a value when supplied.

## Architecture

### Design Highlights

1. **Pure Win32 API for Tray UI** - Uses direct `Shell_NotifyIcon` calls rather than WPF or WinForms windows. Shared project references can still bring desktop-framework dependencies into publish output.

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

- Visual Studio 2022 or 2026 with C++ and .NET workloads
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

Release builds are NativeAOT-compatible. Use the `Publish` target to produce the native executable:

```powershell
msbuild Awake\Awake.csproj /t:Publish /p:Configuration=Release /p:Platform=x64
```

The native output is `x64\Release\win-x64\publish\PowerToys.Awake.exe` relative to
the repository root. A normal `Build` still produces the managed executable.
This branch is an experiment: it does not change the installer or release pipeline
to select the native output.

### NativeAOT experiment results

Measured on one x64 Windows machine on October 13, 2026, using the `measure` example
from the final Rust rewrite commit `93a394bbcc` in PowerToys PR #51181. Each Release
executable ran `for 5m --pid {pid}`. Startup is the median of five launches until
the tray message window exists; footprint and CPU are one sample after three
seconds of settling, followed by a ten-second countdown interval. These are
development-machine observations, not release performance guarantees.

| Metric | Managed C# (this branch) | NativeAOT C# (this branch) | Rust PR |
|--------|-------------------------|---------------------------|---------|
| Startup median | 209.4 ms | 64.9 ms | 30.4 ms |
| Private memory | 12.8 MiB | 9.3 MiB | 2.1 MiB |
| Working set | 45.1 MiB | 24.9 MiB | 12.0 MiB |
| Handles | 349 | 267 | 162 |
| Threads | 14 | 11 | 6 |
| CPU cycles during countdown / 10 seconds | 43.4 million | 4.4 million | 2.6 million |
| x64 executable bytes | 266,752 (managed apphost only) | 12,554,240 | 938,496 |

NativeAOT significantly improves startup and countdown CPU, but does not match
Rust's footprint. It compiles the existing runtime services, Rx timers, shared
settings serialization and telemetry into a native application; it does not adopt
Rust's smaller event-driven architecture. Its executable is approximately 12 MiB.
Since other PowerToys modules still need the shared managed libraries and .NET
runtime, converting Awake cannot reclaim their installed bytes. The native
executable is therefore a significant additional package cost; the publish
directory is not an installer-size measurement and also contains debug symbols
and transitive native dependencies.

The four state icons retain the original pixel-identical 32-bit artwork at
16, 24, 32, 48, and 128 pixels, stored with lossless PNG compression. The application
icon is unchanged. Unused `normal.ico` and `scheduled.ico` are removed. Total source
icon bytes fall from 689,266 to 158,376 without dropping the high-resolution frame.

#### Follow-up: compiler settings versus retained code

Additional x64 experiments used the same source and timed workload:

| Experiment | Executable bytes | Startup median | Private memory | Disposition |
|------------|------------------|----------------|----------------|-------------|
| Speed optimization, identical-method folding off | 16,099,840 | 60.8 / 63.2 ms | 6.7 / 6.6 MiB | Command-line build override only |
| Size optimization, identical-method folding on | 12,554,240 | 67.9 ms | 9.1 MiB | Existing branch configuration retained |
| Shared settings serializer: metadata generation only | 12,407,808 | Not measured | Not measured | One-line experiment reverted |
| Defer settings service initialization in standalone mode | 12,554,240 | 64.6 ms | 9.2 MiB | Experiment reverted; no meaningful measured gain |

Speed versus size changes two compiler settings together, so these results do not
isolate either setting's individual effect. The speed configuration was measured
twice. The size configuration saves 3,545,600 executable bytes (22%), but its observed
private memory is higher; executable size is not a proxy for process memory.
The small startup and CPU-cycle differences are not sufficient for statistical
claims about which configuration is faster.

The NativeAOT size map identifies about 2.39 million bytes of nodes whose names
refer to the shared settings library, including about 0.76 million bytes referring
to its generated JSON context. These are overlapping attribution groups, not
guaranteed removable package bytes. The context registers settings for many
modules, while Awake needs only its own settings. It remains reachable through the
shared settings infrastructure even though Awake has its own serialization context.
Switching off generated fast-write methods alone saved just 146,432 bytes (1.2%).

These experiments do not demonstrate a large, safe code-deletion win. The remaining
gap combines .NET runtime costs with a broader dependency graph and a heavier
event/timer/interop design than the Rust rewrite. Further narrowing the shared
serialization boundary or replacing the timer architecture would be substantive
engineering work, not another compiler switch. No such refactor was attempted.

### Tests

From `src\modules\awake\Awake.UnitTests`, build with the repository scripts and
run the Microsoft.Testing.Platform executable:

```powershell
..\..\..\..\tools\build\build.ps1 -Platform x64 -Configuration Release -ExtraArgs "/p:SolutionDir=$((Resolve-Path ..\..\..\..).Path)\"
..\..\..\..\x64\Release\tests\Awake.UnitTests\Awake.UnitTests.exe
```

The x64 Release suite passes 69 tests: execution-state flags, lock transitions,
duration and date parsing, legacy command compatibility, daylight-saving boundaries,
arbitrary Unicode input, settings serialization, countdown formatting, and Windows
loading of all compressed icon frames. This ports applicable behaviors from the
Rust tests, not the entire Rust-specific engine or test harness.

Both x64 and ARM64 NativeAOT publishes are checked. Native x64 process smoke tests
cover help/error output, missing option values, timed/expiring sessions with process
binding, and successful timer completion. ARM64 runtime, translated release resources,
installer upgrades, and manual multi-monitor UI validation are not covered by these
checks.

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

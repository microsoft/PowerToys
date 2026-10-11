# PowerToys Awake Module

A PowerToys utility that prevents Windows from sleeping and/or turning off the display.

**Author:** [Den Delimarsky](https://den.dev)

## Resources

- [Awake Website](https://awake.den.dev) - Official documentation and guides
- [Microsoft Learn Documentation](https://learn.microsoft.com/windows/powertoys/awake) - Usage instructions and feature overview
- [GitHub Issues](https://github.com/microsoft/PowerToys/issues?q=is%3Aissue+label%3AProduct-Awake) - Report bugs or request features

## Overview

| Project | Language | Purpose |
|---------|----------|---------|
| `Awake/` | Rust | `PowerToys.Awake.exe`: tray icon, keep-awake engine and command line |
| `AwakeModuleInterface/` | C++ | Runner module: starts and stops `PowerToys.Awake.exe`, stores settings |
| `Awake.ModuleServices/` | C# | Command Palette integration; reads and writes `settings.json` |

`Awake` is a Cargo workspace with two crates:

| Crate | Purpose |
|-------|---------|
| `awake-core` | Platform-independent logic: the keep-awake state machine (`engine`), `settings.json` model, command line (`cli`), terminal text (`text`) and the tray tooltip and menu (`tray_model`). No `unsafe`, fully unit-tested. |
| `awake` | The Windows executable: window and tray icon, power requests, file watching, process binding, logging, and a small C++ bridge (`native/bridge.cpp`) that reuses the shared PowerToys headers for group policy, telemetry and kernel object names. |

## How It Works

The module uses the Win32 `SetThreadExecutionState()` API to signal Windows that the system should remain awake:

- `ES_SYSTEM_REQUIRED` - Prevents system sleep
- `ES_DISPLAY_REQUIRED` - Prevents display sleep (released while the session is locked)
- `ES_CONTINUOUS` - Maintains state until explicitly changed

## Operating Modes

| Mode | Description |
|------|-------------|
| **PASSIVE** | Normal power behavior (off) |
| **INDEFINITE** | Keep awake until manually stopped |
| **TIMED** | Keep awake for a specified duration |
| **EXPIRABLE** | Keep awake until a specific date/time |

## Command-Line Usage

```
PowerToys.Awake [options] [command]

Commands:
  for <duration>      Keep awake for a duration, like 45m or 1h30m
  until <time>        Keep awake until a time, like 17:30 or 2026-12-24T18:00

Options:
  -d, --display-on    Keep the display on too
      --pid <pid>     Stop when the process with this ID exits
      --parent        Stop when the program that started Awake exits
  -h, --help          Show this help
  -v, --version       Show the version
```

Without a command, Awake keeps the computer awake until you press Ctrl+C. Run from a terminal, it shows
what it is doing and a live countdown.

```powershell
PowerToys.Awake.exe for 1h30m --display-on
PowerToys.Awake.exe until 18:00 --parent
PowerToys.Awake.exe --pid 1234
```

The original options still work, so existing scripts and shortcuts keep working:
`-c/--use-pt-config`, `-t/--time-limit <seconds>`, `-e/--expire-at <date>`, `-p/--pid <pid>`,
`-u/--use-parent-pid` and `-d/--display-on`.

Run from a terminal, Awake stays in the foreground with a live status line. Two cases return
control to the caller at once and continue in the background instead, after checking the request
and reporting any error with exit code 1:

- `-c`, `-t`, `-e` or `-u`, which never blocked the calling shell before. Without a process
  binding, the session gets a console window of its own, as before.
- A process binding that points at the caller itself, such as `--pid $PID` in a script or
  `--parent` from a shell. Waiting in the foreground for a process that waits for Awake would
  never end.

## Architecture

- **One thread owns all state.** The window thread holds the keep-awake request (which Windows ties
  to the calling thread), the tray icon, the menu and the state machine. Background threads only
  wait or write: a waiter thread watches the exit event, the bound process, the deadline timer and
  `settings.json`, and posts messages; a logger thread and a console thread write output. No locks
  guard application state, so there is nothing to deadlock on.
- **Functional core.** `awake_core::engine::Engine` turns inputs (settings, tray choices, ticks,
  lock and power events) into effects (`SetExecutionState`, `ScheduleDeadline`, `Persist`, ...).
  It diffs against the live state, so a settings write that changes nothing restarts nothing.
- **`settings.json` is the single source of truth.** The runner writes it for the Settings app,
  Command Palette and DSC write it directly, and Awake writes it for its own menu and timers. Awake
  applies its own changes immediately and its file watcher sees the echo as a no-op. Writes go to a
  temporary file, are flushed to disk and replace the original only once complete, then refresh its
  write time so the Settings app's watchers notice. They run on the window thread: the file is
  small and writes are rare, and keeping them there keeps pending changes and retries in one place.
- **Never holds the computer awake by accident.** Every exit path releases the request. When a stop
  arrives from outside the window thread (the runner's exit event, the bound process ending, the
  deadline of a command-line session, Ctrl+C) and the window thread does not finish within 10
  seconds, the process is ended. A crash ends the process too, and Windows drops the request
  with it. A Restart Manager request to close (from an installer) ends Awake like any other stop.
- **Quiet while idle.** Nothing polls. A timed session redraws its countdown once a second on a
  timer Windows may batch with others, and skips that redraw while the session is locked (a session
  that ends while locked still updates the icon, and unlocking redraws it).
  Power broadcasts only matter on resume. If lock notifications are not available yet early in a
  logon, Awake retries for about a minute, then catches up with the current lock state.

## Building

### Prerequisites

- Visual Studio 2022 or 2026 with C++ and .NET workloads
- Windows SDK 10.0.26100.0 or later
- [Rust](https://rustup.rs) via `rustup`; the toolchain version is pinned in
  `Awake/rust-toolchain.toml` and installed automatically on first build

### Build Commands

The solution builds `Awake/Awake.vcxproj`, which runs Cargo and places
`PowerToys.Awake.exe` with the other PowerToys binaries. To build just Awake:

```powershell
# From src/modules/awake/Awake
cargo build               # debug
cargo build --release     # release
cargo test-all            # unit and end-to-end tests
cargo lint -- -D warnings # clippy, as CI runs it
cargo fmt --all --check   # formatting
```

Or, from `src/modules/awake`:

```powershell
.\scripts\Build-Awake.ps1 -Configuration Debug -Platform x64
```

`test-all` and `lint` are aliases in `.cargo/config.toml`. A plain `cargo test --workspace` runs
the unit tests only: the end-to-end tests need the `test-hooks` feature, and Cargo skips them
silently without it. The solution's Test target runs all three checks and writes the test results
to `awake.trx` in its target folder, which the pipeline publishes.

Run Cargo from `src/modules/awake/Awake`, not with `--manifest-path` from elsewhere, and leave
`RUSTFLAGS` unset: Cargo only reads `.cargo/config.toml`, which adds Control Flow Guard, CET and
the static VC++ runtime, from the current folder upwards, and `RUSTFLAGS` replaces those flags.
The build stops with an error when they are missing.

## Dependencies

| Crate | Why |
|-------|-----|
| `windows-sys` | Win32 API bindings, published by Microsoft |
| `serde_json` | Reading and writing `settings.json` with key order preserved |
| `clap` | Command-line parsing |
| `log` | Logging facade |
| `cc` (build only) | Compiles the C++ bridge |

## Configuration

When running with PowerToys (`--use-pt-config`), settings are stored in:
```
%LOCALAPPDATA%\Microsoft\PowerToys\Awake\settings.json
```

Logs are written to `%LOCALAPPDATA%\Microsoft\PowerToys\Awake\Logs\<version>\`.

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

The module emits the same telemetry events as before (`Awake_EnableAwake`, `Awake_CLICommand` and
one event per keep-awake mode), through the shared PowerToys provider and only when the user has
opted in to diagnostic data.

# PowerToys Awake Module

A PowerToys utility that keeps Windows from sleeping and, optionally, the display from turning off.

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

`Awake` is a Cargo workspace with three crates:

| Crate | Purpose |
|-------|---------|
| `awake-core` | Platform-independent logic: the keep-awake state machine (`engine`), `settings.json` model, command line (`cli`), terminal text (`text`) and the tray tooltip and menu (`tray_model`). Forbids `unsafe` and runs without Windows, so unit tests cover it. |
| `awake` | The Windows executable: window and tray icon, power requests, file watching, process binding, logging, and a small C++ bridge (`native/bridge.cpp`) that reuses the shared PowerToys headers for group policy, telemetry and kernel object names. |
| `awake-testkit` | Test support only: starts Awake the way the runner does and measures its process, for the end-to-end tests and the `measure` example. It includes the executable's `hook_names.rs` and `log_lines.rs`, so the tests and the executable agree on test hook names and on the log lines the tests wait for. |

## How It Works

Awake calls the Win32 `SetThreadExecutionState()` API with:

- `ES_SYSTEM_REQUIRED` - Prevents system sleep
- `ES_DISPLAY_REQUIRED` - Prevents display sleep (released while the session is locked)
- `ES_CONTINUOUS` - Keeps the request in place until it is changed

| Mode | Description |
|------|-------------|
| **PASSIVE** | Normal power behavior (off) |
| **INDEFINITE** | Keep awake until manually stopped |
| **TIMED** | Keep awake for a specified duration |
| **EXPIRABLE** | Keep awake until a specific date/time |

A timed session ends by the wall clock, as in the previous version: moving the clock back extends
it by the same amount.

## Command-Line Usage

```
Usage: PowerToys.Awake [options] [command]

Commands:
  for <duration>    Keep awake for a duration, like 45m or 1h30m
  until <time>      Keep awake until a time, like 17:30 or 2026-12-24T18:00

Options:
  -d, --display-on  Keep the display on too
      --pid <pid>   Stop when the process with this ID exits
      --parent      Stop when the program that started Awake exits
  -h, --help        Show this help
  -v, --version     Show the version
```

Without a command, Awake keeps the computer awake until you press Ctrl+C. `--pid` without a value
is an error; `--pid 0` means no process.

```powershell
PowerToys.Awake.exe for 1h30m --display-on
PowerToys.Awake.exe until 18:00 --parent
PowerToys.Awake.exe --pid 1234
```

Run from a terminal, Awake stays in the foreground with a live status line. Two cases return
control to the caller at once and continue in the background instead, after checking the request
and reporting any error with exit code 1:

- `-c`, `-t`, `-e` or `-u` (see below), which never blocked the calling shell.
- A process binding that points at the caller itself, such as `--pid $PID` in a script or
  `--parent` from a shell. Waiting in the foreground for a process that waits for Awake would
  never end.

### Options from earlier versions

These still work, hidden from help, so existing scripts and shortcuts keep running:
`-c/--use-pt-config`, `-t/--time-limit <seconds>`, `-e/--expire-at <date>` and
`-u/--use-parent-pid`, plus `-p` as a short form of `--pid`. The colon form (`-t:60`, `-c:true`)
is accepted too, and `-e` without a value is an error. Two cases behave differently than before:

- With a process binding, `-t` and `-e` used to be ignored. They now apply as well, and the session
  ends at whichever comes first.
- A `-t` or `-e` session without a process binding still gets a console window of its own, but `-c`
  no longer does: it follows `settings.json` without one.

## Architecture

- **One thread owns all state.** The window thread holds the keep-awake request (which Windows ties
  to the calling thread), the tray icon, the menu and the state machine. Background threads only
  wait or write: a waiter thread watches the exit event, the bound process, the deadline timer and
  `settings.json`, and posts messages; a logger thread and a console thread write output. No locks
  guard application state, so there is nothing to deadlock on.
- **Functional core.** `awake_core::engine::Engine` turns inputs (settings, tray choices, ticks,
  lock and power events) into effects (`SetExecutionState`, `ScheduleDeadline`, `Persist`, ...).
  It diffs against the live state, so Awake's own writes echoing back are no-ops.
- **`settings.json` is the single source of truth.** The runner writes it for the Settings app,
  Command Palette and DSC write it directly, and Awake writes it for its own menu and timers. Awake
  applies its own changes immediately and recognizes their echo by the file's write time. Saving
  unchanged values is otherwise a no-op, with one deliberate exception: when another program saves
  the running timed interval again, the countdown starts over, as choosing the same preset from the
  menu does; a save that only changes "Keep screen on" or the presets leaves it alone. Writes go to
  a temporary file, are flushed to disk and replace the original only once complete, then refresh
  its write time so the Settings app's watchers notice. They run on the window thread: the file is
  small, writes are rare, and pending changes and retries stay in one place.
- **Never holds the computer awake by accident.** Every exit path releases the request. When a stop
  arrives from outside the window thread (the runner's exit event, the bound process ending, the
  deadline of a command-line session, Ctrl+C) and the window thread does not finish within 10
  seconds, the process is ended. A crash ends the process too, and Windows drops the request
  with it. A Restart Manager request to close (from an installer) ends Awake like any other stop.
- **No console nobody can see.** The runner starts Awake with no console at all, so a hidden
  console host no longer runs alongside it. A console made just for Awake by a program without one
  is dropped for sessions bound to a process or run under PowerToys.
- **Quiet while idle.** Nothing polls. A timed session redraws its countdown once a second on a
  timer Windows may batch with others, and skips that redraw while the session is locked (a session
  that ends while locked still updates the icon, and unlocking redraws it). Power broadcasts only
  matter on resume. If lock notifications are not available yet early in a logon, Awake retries
  for about a minute and reads the current lock state once they are.

### Where things live

In `Awake/awake`:

| Path | What it holds |
|------|---------------|
| `src/main.rs` | Entry point, panic hook, version and build id |
| `src/app.rs` | The running session: what each window message means, and carrying out the engine's effects |
| `src/app/launch.rs` | From the command line to a running session: parsing, start checks and the hand-off to a detached process |
| `src/app/settings_file.rs` | Keeping a PowerToys session and `settings.json` in step |
| `src/app/window_proc.rs` | The window procedure and messages that arrive while the app is busy |
| `src/platform/` | Every Win32 call, one concern per module: tray, menu and theme; power and lock state; the waiter, file watch and watchdog threads; named objects and their security; processes, console and paths |
| `src/logging.rs` | The log file, written on a background thread, capped per day, old files removed |
| `src/bridge.rs`, `native/bridge.cpp` | Group policy, telemetry and kernel object names from the shared PowerToys C++ code |
| `src/hooks.rs`, `src/hook_names.rs`, `src/log_lines.rs` | Test hooks, and the names and log lines the tests share with the executable |
| `build.rs`, `assets/`, `native/awake.manifest` | Compiles the bridge and the Win32 resources: version, icons and manifest |
| `tests/` | End-to-end tests (`process.rs`), the memory budget (`low_memory.rs`) and the runner contract (`module_interface.rs`) |
| `examples/measure.rs` | Startup time, footprint and idle CPU of an Awake build, started as the runner starts it |

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
```

Or, from `src/modules/awake`, with the module interface and Command Palette service as well:

```powershell
.\scripts\Build-Awake.ps1 -Configuration Debug -Platform x64
```

Run Cargo from `src/modules/awake/Awake`, not with `--manifest-path` from elsewhere, and leave
`RUSTFLAGS` unset: Cargo only reads `.cargo/config.toml`, which adds Control Flow Guard, CET and
the static VC++ runtime, from the current folder upwards, and `RUSTFLAGS` replaces those flags.
The build stops with an error when they are missing.

### Testing

From `src/modules/awake/Awake`, in a terminal:

```powershell
cargo test-all                     # unit and end-to-end tests
cargo lint -- -D warnings          # clippy, with the test hooks
cargo lint-shipped -- -D warnings  # clippy as shipped, without them
cargo fmt --all --check            # formatting
```

`test-all`, `lint` and `lint-shipped` are aliases in `.cargo/config.toml`. A plain
`cargo test --workspace` runs the unit tests only: the end-to-end tests need the `test-hooks`
feature, and Cargo skips them silently without it. Code that only the shipped build compiles is
linted only by `lint-shipped`. Clippy runs with its pedantic group on; the workspace `Cargo.toml`
lists the few lints turned off and why. The hand-off tests need a console, so run them from a
terminal rather than an IDE test runner that gives the process none.

The Test target of `Awake.vcxproj` runs all of the above and writes the results to `awake.trx`
in its target folder, which the pipeline publishes. In pipeline builds on x64 it also runs the
runner contract test, `tests/module_interface.rs`, which loads the real
`PowerToys.AwakeModuleInterface.dll`, enables it, pushes settings and disables it. The test is
ignored by default because it uses the real `%LOCALAPPDATA%\Microsoft\PowerToys` folder,
overwriting Awake's `settings.json` there, and the real kernel object names, so no other Awake
may run. To run it locally, exit PowerToys, build `AwakeModuleInterface` for x64 Debug (or
Release, for `cargo test --release`), and run
`cargo test -p awake --test module_interface -- --ignored`; or pass
`-p:RunAwakeRunnerContractTest=true` to the Test target.

The end-to-end tests run each Awake in a sandbox set by two environment variables, read only by
builds with `test-hooks`: `POWERTOYS_AWAKE_DATA_DIR` replaces `%LOCALAPPDATA%\Microsoft\PowerToys`,
and `POWERTOYS_AWAKE_OBJECT_SUFFIX` is appended to the kernel object names. Set them to run a
second Awake by hand next to an installed PowerToys:

```powershell
$env:POWERTOYS_AWAKE_DATA_DIR = "$env:TEMP\awake-sandbox"
$env:POWERTOYS_AWAKE_OBJECT_SUFFIX = "-sandbox"
cargo run -p awake --features test-hooks -- for 5m
```

`src/hook_names.rs` lists the other test hooks. Two more tools are run by hand:

- `tools\Test-SettingsWatcher.ps1` checks that Awake's settings writes reach .NET file watchers
  set up like the Settings app's.
- `cargo run --release -p awake --features test-hooks --example measure -- <label> <exe> <args...>`
  measures an Awake build; the header of `examples/measure.rs` explains the output.

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

When "Keep screen on" is enabled (`--display-on` on the command line), Awake uses the `ES_DISPLAY_REQUIRED` flag, which keeps Windows Task Scheduler from treating the system as idle. Scheduled maintenance tasks that wait for idle (such as SSD TRIM and disk defragmentation) then do not run.

Per [Microsoft's documentation](https://learn.microsoft.com/en-us/windows/win32/taskschd/task-idle-conditions):

> "An exception would be for any presentation type application that sets the ES_DISPLAY_REQUIRED flag. This flag forces Task Scheduler to not consider the system as being idle, regardless of user activity or resource consumption."

**Workarounds:**

1. **Turn off "Keep screen on"** - Awake then only uses `ES_SYSTEM_REQUIRED`, which still prevents sleep but lets Task Scheduler detect the idle state.

2. **Run maintenance tasks manually** - For example, to run TRIM:
   ```powershell
   # Run as Administrator
   Optimize-Volume -DriveLetter C -ReTrim -Verbose
   ```

## Telemetry

The module emits the same events with the same fields as the previous version (`Awake_EnableAwake`,
`Awake_CLICommand` and one event per keep-awake mode), through the shared PowerToys provider and
only when the user has opted in to diagnostic data. The events `PowerToys.Awake.exe` writes now
carry the privacy tag `ProductAndServicePerformance`, the one every native PowerToys event uses,
where the .NET version declared `ProductAndServiceUsage`.

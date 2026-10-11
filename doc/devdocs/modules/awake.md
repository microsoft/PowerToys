# Awake

[Public overview - Microsoft Learn](https://learn.microsoft.com/en-us/windows/powertoys/awake)

## Quick Links

[All Issues](https://github.com/microsoft/PowerToys/issues?q=is%3Aopen%20label%3AProduct-Awake)<br>
[Bugs](https://github.com/microsoft/PowerToys/issues?q=is%3Aopen%20label%3AIssue-Bug%20%20label%3AProduct-Awake)<br>
[Pull Requests](https://github.com/microsoft/PowerToys/pulls?q=is%3Apr+is%3Aopen++label%3A%22Product-Awake%22+)

## Overview
Awake keeps the computer from sleeping, and optionally the display from turning off, without changing the Windows power plan: it holds a keep-awake request only while it runs, so there is nothing to undo afterward. A session lasts indefinitely, for a set time, or until a set date and time.

## Architecture

`PowerToys.Awake.exe` is a Rust program: a Cargo workspace under `src/modules/awake/Awake`, built through `Awake.vcxproj` so that the solution and the pipeline build it like any other project. The runner loads the C++ module interface in `src/modules/awake/AwakeModuleInterface`, which starts and stops the executable and stores its settings. Command Palette reads and writes the same `settings.json` through `Awake.ModuleServices`.

The [module README](../../../src/modules/awake/README.md) describes the crates, the design, the command line, and how to build and test Awake.

## User Experience
- **PowerToys Settings**: every mode, including a custom duration and an expiry time, and whether to keep the screen on.
- **Notification-area icon**: Awake shows its own icon while it runs. When PowerToys started it, the menu switches between off, indefinite and a preset interval, shows when an expiry is set, and turns "Keep screen on" on or off (unavailable while Awake is off). A session started from the command line offers only "Exit", and nothing when it is bound to a process.
- **Command Palette**: turns Awake off, or keeps the computer awake indefinitely or for a number of minutes.

## Command Line Support
`PowerToys.Awake.exe` also runs on its own from a terminal or a script, for example `PowerToys.Awake.exe for 1h30m` or `PowerToys.Awake.exe --pid 1234`. The module README lists every option.

# Awake

[Public overview - Microsoft Learn](https://learn.microsoft.com/en-us/windows/powertoys/awake)

## Quick Links

[All Issues](https://github.com/microsoft/PowerToys/issues?q=is%3Aopen%20label%3AProduct-Awake)<br>
[Bugs](https://github.com/microsoft/PowerToys/issues?q=is%3Aopen%20label%3AIssue-Bug%20%20label%3AProduct-Awake)<br>
[Pull Requests](https://github.com/microsoft/PowerToys/pulls?q=is%3Apr+is%3Aopen++label%3A%22Product-Awake%22+)

## Overview
Awake is a PowerToys utility designed to keep your computer awake without permanently modifying system power settings. It prevents the computer from sleeping and can keep the monitor on, providing a convenient alternative to changing system power configurations.

## Key Features
- Temporarily override system sleep settings
- Keep monitor on (prevent display from turning off)
- Set time intervals for keeping the system awake
- One-time setup with no need to revert power settings afterward

## Advantages Over System Power Settings
- **Convenience**: Easy UI for quick toggling of sleep prevention
- **Flexibility**: Support for different time intervals (indefinitely, for specific duration)
- **Non-persistent**: Changes are temporary and don't require manual reversion
- **Quick Access**: Available directly from the system tray

## Architecture

`PowerToys.Awake.exe` is a Rust program: a Cargo workspace under `src/modules/awake/Awake`, built through `Awake.vcxproj` so that the solution and the pipeline build it like any other project. The runner loads the C++ module interface in `src/modules/awake/AwakeModuleInterface`, which starts and stops the executable and stores its settings. Command Palette reads and writes the same `settings.json` through `Awake.ModuleServices`.

The [module README](../../../src/modules/awake/README.md) describes the crates, the design, the command line, and how to build and test Awake.

## User Experience
- **PowerToys Settings**: every mode, including a custom duration and an expiry time, and whether to keep the screen on.
- **Notification-area icon**: Awake shows its own icon while it runs. Its menu switches between off, indefinite, a preset interval and the configured expiry, and turns "Keep screen on" on or off.
- **Command Palette**: turns Awake off, or keeps the computer awake indefinitely or for a number of minutes.

## Command Line Support
`PowerToys.Awake.exe` also runs on its own from a terminal or a script, for example `PowerToys.Awake.exe for 1h30m` or `PowerToys.Awake.exe --pid 1234`. The module README lists every option.

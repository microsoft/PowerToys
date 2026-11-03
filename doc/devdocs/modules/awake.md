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

### Components
- **System Tray UI**: Provides user interface for controlling Awake settings
- **Backend Threads**: Manages the power state prevention functionality
- **Command Line Interface**: Supports various commands for controlling Awake functionality programmatically

## Technical Implementation
Awake works by preventing system sleep through Windows power management APIs. The module runs as a background process that interfaces with the Windows power management system to keep the device awake according to user preferences.

## User Experience
Users can access Awake through the PowerToys system tray icon. From there, they can:
1. Toggle Awake on/off
2. Set a specific duration for keeping the system awake
3. Choose whether to keep the display on or allow it to turn off
4. Access additional configuration options

## Command Line Support
Awake includes command-line functionality for power users and automation scenarios, allowing programmatic control of the utility's features.

## UI tests

`src\modules\awake\Awake.UITests` uses `Microsoft.PowerToys.UITest.Next` and winappcli.
The suite covers Settings lifecycle and modes, display requests, duration and expiration edits,
native tray actions, and standalone CLI help, errors, timers, process binding, and configuration
hot reload. Timer checks include minimum durations, and power-state checks read the Windows kernel's
execution-state requests without changing the active power plan.

Build the project with the repository script:

```powershell
tools\build\build.cmd -Path src\modules\awake\Awake.UITests -Platform x64 -Configuration Release
```

Run the resulting executable from a dedicated interactive test desktop:

```powershell
& .\x64\Release\tests\Awake.UITests\net10.0-windows10.0.26100.0\Awake.UITests.exe `
    --report-trx --report-trx-filename Awake.UITests.trx --results-directory .\TestResults\Awake
```

Prefer the persistent VM workflow in
[ui-tests-local-vm](../../../.github/skills/ui-tests-local-vm/SKILL.md), including complete Windows 10
and Windows 11 default and constrained runs. The suite starts and stops PowerToys, temporarily
rewrites module/general settings, and uses the clipboard to enter commands in the default terminal.
Fixtures restore their settings and clipboard snapshots. A quiet power baseline is required:
unrelated media or maintenance applications can hold system/display requests, so startup waits
allow up to three minutes for those requests to clear. Do not run the suite on a desktop used for
other work.

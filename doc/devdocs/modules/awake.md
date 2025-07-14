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

# Light Switch

[Public Overview – Microsoft Learn](https://learn.microsoft.com/en-us/windows/powertoys/light-switch)

## Quick Links

* [All Issues](https://github.com/microsoft/PowerToys/issues?q=is%3Aissue%20state%3Aopen%20label%3AProduct-LightSwitch)
* [Bugs](https://github.com/microsoft/PowerToys/issues?q=is%3Aissue%20state%3Aopen%20label%3AProduct-LightSwitch%20label%3AIssue-Bug)
* [Pull Requests](https://github.com/microsoft/PowerToys/pulls?q=is%3Apr+is%3Aopen+label%3AProduct-LightSwitch)

## Overview

The **Light Switch** module lets users automatically transition between light and dark mode using a timed schedule or a keyboard shortcut.

## Features

* Set custom times to start and stop dark mode.
* Use geolocation to determine local sunrise and sunset times.
* Apply offsets in sunrise mode (e.g., 15 minutes before sunset).
* Quickly toggle between modes with a keyboard shortcut (`Ctrl+Shift+Win+D` by default).
* Choose whether theme changes apply to:

  * Apps only
  * System only
  * Both apps and system

## Architecture

### Main Components

* **Shortcut/Hotkey**
  Listens for a hotkey event. Calling `onHotkey()` flips the theme flags.

  > **Note:** Using the shortcut overrides the current schedule until the next transition event.

* **LightSwitchService**
  Reads settings and applies theming. Runs a check every minute to ensure the state is correct.

* **SettingsXAML/LightSwitch**
  Provides the settings UI for configuring schedules, syncing location, and customizing shortcuts.

* **Settings.UI/ViewModels/LightSwitchViewModel.cs**
  Handles updates to the settings file and communicates changes to the front end.

* **modules/LightSwitch/Tests**
  Contains UI tests that verify interactions between the settings UI, system state, and `settings.json`.

### Data Flow

1. User configures settings in the UI (default: manual mode, light mode from 06:00–18:00).
2. Every minute, the service checks the time.

   * If it’s not a threshold, the service sleeps until the next minute.
   * If it matches a threshold, the service applies the theme based on settings and returns to sleep.
3. At **midnight**, when in *Sunrise to Sunset* mode, the service updates daily sunrise and sunset times.
4. If the machine was asleep during a scheduled event, the service applies the correct settings at the next check.

## User Interface

The module’s settings are exposed in the PowerToys Settings UI. Options include:

* Shortcut customization
* Mode selection (Manual or Sunrise to Sunset)
* Manual start/stop times (manual mode only)
* Automatic sunrise/sunset calculation (location-based)
* Time offsets (sunrise mode)
* Target scope (system, apps, or both)

## Development Environment Setup

### Prerequisites

* Visual Studio 2019 or later
* Windows 10 SDK
* PowerToys repository cloned from GitHub

### Building and Testing

1. Clone the repo:

   ```sh
   git clone https://github.com/microsoft/PowerToys.git
   ```
2. Initialize submodules:

   ```sh
   git submodule update --init --recursive
   ```
3. Build the solution:

   ```sh
   msbuild -restore -p:RestorePackagesConfig=true -p:Platform=ARM64 -m PowerToys.sln
   ```

   > Note: This may take some time.
4. Set `runner` as the startup project and press **F5**.
5. Enable Light Switch in PowerToys Settings.
6. To debug the service:

   * Press `Ctrl+Alt+P` or go to **Debug > Attach to Process**.
   * Select `LightSwitchService.exe` and click **Attach**.
   * You can now set breakpoints in the service files.
7. To debug the Settings UI:

   * Set the startup project to `PowerToys.Settings` and press **F5**.
   * Note: Light Switch settings will not persist in this mode (they depend on the service executable).
   * Alternatively, you can attach `PowerToys.Settings.exe` to the debugger while `runner` is running to test the full flow with breakpoints.

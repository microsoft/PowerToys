# Always on Top

[Public overview - Microsoft Learn](https://learn.microsoft.com/en-us/windows/powertoys/always-on-top)

## Quick Links

[All Issues](https://github.com/microsoft/PowerToys/issues?q=is%3Aopen%20label%3A%22Product-Always%20On%20Top%22)<br>
[Bugs](https://github.com/microsoft/PowerToys/issues?q=is%3Aopen%20label%3AIssue-Bug%20%20label%3A%22Product-Always%20On%20Top%22)<br>
[Pull Requests](https://github.com/microsoft/PowerToys/pulls?q=is%3Apr+is%3Aopen++label%3A%22Product-Always+On+Top%22+)

## Overview

The Always on Top module allows users to pin windows on top of others, ensuring they remain visible even when switching between applications. The module provides visual indicators (customizable borders) to identify which windows are pinned.

## Features

- Pin any window to stay on top of other windows
- Customizable border color, opacity, and thickness around pinned windows
- User-defined keyboard shortcut for toggling window pinning
- Visual indicators to identify pinned windows

## Architecture

### Main Components

- **Hotkey Listener**: Detects the user-defined hotkey to toggle the Always on Top state
- **AlwaysOnTop**: Manages the state of windows, ensuring the selected window stays on top
- **Settings**: Stores user preferences and configurations
- **WindowHook**: Hooks all window events

### Data Flow

1. The Hotkey Listener detects the hotkey press and notifies the AlwaysOnTop
2. The AlwaysOnTop updates the window state and interacts with the operating system to keep the window on top
3. User preferences are saved and loaded from the Settings

## Code Structure

### Key Files

- **AlwaysOnTop.cpp**: Contains the core logic for the module, including initialization and event handling
- **Settings.cpp**: Defines the settings structure and provides methods to load and save settings
- **main.cpp**: Starts thread and initializes AlwaysOnTop

### Initialization

The module is initialized in the AlwaysOnTop class. During initialization, the following steps are performed:

1. **LoadSettings**: The module loads user settings from a configuration file
2. **RegisterHotkey**: The HotkeyManager registers the keyboard shortcut for pinning/unpinning windows
3. **SubscribeToEvents**: Event handlers are attached to respond to user actions, such as pressing the hotkey

### Pinning and Unpinning Windows

The AlwaysOnTop class handles the pinning and unpinning of windows. Key methods include:

- **PinTopmostWindow**: Pins the specified window on top of others and applies visual indicators
- **UnpinTopmostWindows**: Removes the pinning status and visual indicators from the specified window
- **AssignBorder**: Applies a colored border around the pinned window based on user settings

### Settings Management

The Settings class manages the module's settings. Key methods include:

- **LoadSettings**: Loads settings from a configuration file
- **NotifyObservers**: Distributes the data for the settings
- **GetDefaultSettings**: Returns the default settings for the module

## User Interface

The module provides a user interface for configuring settings in the PowerToys Settings UI. This interface is implemented using XAML and includes options for customizing the:

- Border color
- Border opacity
- Border thickness
- Keyboard shortcut

## Development Environment Setup

### Prerequisites

- Visual Studio 2019 or later
- Windows 10 SDK
- PowerToys repository cloned from GitHub

### Building and Testing

1. Clone the repository: `git clone https://github.com/microsoft/PowerToys.git`
2. Open PowerToys.slnx in Visual Studio
3. Select the Release configuration and build the solution
4. Run PowerToys.exe from the output directory to test the module

### Debug
1. build the entire project
2. launch the built Powertoys
3. select AlwaysOnTop as the startup project in VS
4. In the debug button, choose "Attach to process". ![image](https://github.com/user-attachments/assets/a7624ec2-63f1-4720-9540-a916b0ada282)
5. Attach to AlwaysOnTop.![image](https://github.com/user-attachments/assets/815c0f89-8fd1-48d6-b7fd-0e4a92e222d0)

# FancyZones

## Quick Links

[All Issues](https://github.com/microsoft/PowerToys/issues?q=is%3Aopen%20label%3AProduct-FancyZones)<br>
[Bugs](https://github.com/microsoft/PowerToys/issues?q=is%3Aopen%20label%3AProduct-FancyZones%20label%3AIssue-Bug%20)<br>
[Pull Requests](https://github.com/microsoft/PowerToys/pulls?q=is%3Apr+FancyZone)

## Overview

FancyZones is a window manager utility that allows users to create custom layouts for organizing windows on their screen.

## Architecture Overview

FancyZones consists of several interconnected components:

### Directory Structure
- **src**: Contains the source code for FancyZones.
  - **Editor**: Code for the zone editor.
  - **Runner**: Code for the zone management and window snapping.
  - **Settings**: Code for managing user settings.
- **tests**: Contains unit and integration tests for FancyZones and UI test code.

### Project Structure
FancyZones is divided into several projects:

- **FancyZones**: Used for thread starting and module initialization.
- **FancyZonesLib**: Contains the main backend logic, called by FancyZones (via COM).
  - **FancyZonesData** folder: Contains classes and utilities for managing FancyZones data.
- **FancyZonesEditor**: Main UI implementation for creating and editing layouts.
- **FancyZonesEditorCommon**: Stores editor's data and provides shared functionality.
- **FancyZonesModuleInterface**: Interface layer between FancyZones and the PowerToys Runner.

### Interface Layer: FancyZonesModuleInterface
- Exposes interface between FancyZones and the Runner
- Handles communication and configuration exchange
- Contains minimal code, most logic implemented in other modules

### UI Layer: FancyZonesEditor and FancyZonesEditorCommon
- **FancyZonesEditor**: Main UI implementation with MainWindow.xaml as entry point
- **FancyZonesEditorCommon**: Provides data structures and I/O helpers for the Editor
- Acts as a visual config editor for layout configuration

![Editor Code Map](../images/fancyzones/editor_map.png)
![Editor Common Code Map](../images/fancyzones/editor_common_map.png)

### Backend Implementation: FancyZones and FancyZonesLib
- **FancyZonesLib**: Core logic implementation
  - All drag-and-drop behavior
  - Layout UI during dragging (generated in C++ via WorkArea.cpp, NewZonesOverlayWindow function)
  - Core data structures
- **FancyZones**: Wrapper around FancyZonesLib

### Data Flow
- User interactions with the Editor are saved in the Settings
- The Runner reads the Settings to apply the zones and manage window positions
- Editor sends update events, which trigger FancyZones to refresh memory data

## Key Files

### FancyZones and FancyZonesLib Projects

- **FancyZonesApp.h/cpp**:
  - **FancyZonesApp Class**: Initializes and manages the FancyZones application.
  - **Constructor**: Initializes DPI awareness, sets up event hooks, creates the FancyZones instance.
  - **Destructor**: Cleans up resources, destroys the FancyZones instance, unhooks event hooks.
  - **Run Method**: Starts the FancyZones application.
  - **InitHooks Method**: Sets up Windows event hooks to monitor system events.
  - **DisableModule Method**: Posts a quit message to the main thread.
  - **HandleWinHookEvent/HandleKeyboardHookEvent Methods**: Handle Windows event hooks.

- **Data Management Files**:
  - **AppliedLayouts.h/cpp**: Manages applied layouts for different monitors and virtual desktops.
  - **AppZoneHistory.h/cpp**: Tracks history of app zones.
  - **CustomLayouts.h/cpp**: Handles user-created layouts.
  - **DefaultLayouts.h/cpp**: Manages default layouts for different monitor configurations.
  - **LayoutHotkeys.h/cpp**: Manages hotkeys for switching layouts.
  - **LayoutTemplates.h/cpp**: Handles layout templates.

- **Core Functionality**:
  - **FancyZonesDataTypes.h**: Defines data types used throughout FancyZones.
  - **FancyZonesWindowProcessing.h/cpp**: Processes window events like moving and resizing.
  - **FancyZonesWindowProperties.h/cpp**: Manages window properties like assigned zones.
  - **JsonHelpers.h/cpp**: Utilities for JSON serialization/deserialization.
  - **Layout.h/cpp**: Defines the Layout class for zone layout management.
  - **LayoutConfigurator.h/cpp**: Configures different layout types (grid, rows, columns).
  - **Settings.h/cpp**: Manages FancyZones module settings.

### FancyZonesEditor and FancyZonesEditorCommon Projects

- **UI Components**:
  - **MainWindow.xaml/cs**: Main window of the FancyZones Editor.
  - **EditorOverlay.xaml/cs**: Overlay window for editing zones.
  - **EditorSettings.xaml/cs**: Settings window for the FancyZones Editor.
  - **LayoutPreview.xaml/cs**: Provides layout preview.
  - **ZoneSettings.xaml/cs**: Manages individual zone settings.

- **Data Components**:
  - **EditorParameters.cs**: Parameters used by the FancyZones Editor.
  - **LayoutData.cs**: Manages data for individual layouts.
  - **LayoutHotkeys.cs**: Manages hotkeys for switching layouts.
  - **LayoutTemplates.cs**: Manages layout templates.
  - **Zone.cs**: Represents an individual zone.
  - **ZoneSet.cs**: Manages sets of zones within a layout.

## Configuration Management

### Configuration Files Location
- Path: `C:\Users\[username]\AppData\Local\Microsoft\PowerToys\FancyZones`
- Files:
  - EditorParameters
  - AppliedLayouts
  - CustomLayouts
  - DefaultLayouts
  - LayoutHotkeys
  - LayoutTemplates
  - AppZoneHistory

### Configuration Handling
- No central configuration handler
- Editor: Read/write handlers in FancyZonesEditorCommon project
- FancyZones: Read/write handlers in FancyZonesLib project
- Data synchronization: Editor sends update events, FancyZones refreshes memory data

## Window Management

### Monitor Detection and DPI Scaling
- Monitor detection handled in `FancyZones::MoveSizeUpdate` function
- DPI scaling: FancyZones retrieves window position without needing mouse DPI scaling info
- Window scaling uses system interface via `WindowMouseSnap::MoveSizeEnd()` function

### Zone Tracking
- Window-to-zone tracking implemented in `FancyZones::MoveSizeUpdate` function
- Maintains history of which windows belong to which zones

## Development History

- FancyZones was originally developed as a proof of concept
- Many configuration options were added based on community feedback after initial development
- Some options were added to address specific issues:
  - Options for child windows or pop-up windows
  - Some options were removed later
  - Community feedback led to more interactions being implemented
## Admin Mode Considerations

- FancyZones can't move admin windows unless running as admin
- By default, all utilities run as admin if PowerToys is running as admin

## Development Environment Setup

### Prerequisites
- Visual Studio 2022: Required for building and debugging
- Windows 10 SDK: Ensure the latest version is installed
- PowerToys Repository: Clone from GitHub

### Setup Steps
1. Clone the Repository:
   ```
   git clone https://github.com/microsoft/PowerToys.git
   ```
2. Open `PowerToys.sln` in Visual Studio
3. Select the Release configuration and build the solution
4. If you encounter build errors, try deleting the x64 output folder and rebuild

## Debugging

### Before Successfully Building the Project
1. In Visual Studio 2022, set FancyZonesEditor as the startup project
2. Set breakpoints in the code where needed
3. Click Run to start debugging

### During Active Development
- You can perform breakpoint debugging to troubleshoot issues
- Attach to running processes if needed to debug the module in context

## Deployment and Release Process

### Deployment

#### Local Testing
1. Build the solution in Visual Studio
2. Run PowerToys.exe from the output directory

#### Packaging
- Use the MSIX packaging tool to create an installer
- Ensure all dependencies are included

### Release

#### Versioning
- Follow semantic versioning for releases

#### Release Notes
- Document all changes, fixes, and new features

#### Publishing
1. Create a new release on GitHub
2. Upload the installer and release notes

## Troubleshooting

### First Run JSON Error
**Error**: "The input does not contain any JSON tokens. Expected the input to start with a valid JSON token, when isFinalBlock is true. Path: $ | LineNumber: 0 | BytePositionInLine: 0."

**Solution**: Launch the FancyZones Editor once through PowerToys Settings UI. Running the Editor directly within the project will not initialize the required configuration files.

### Known Issues
- Potential undiscovered bugs related to data updates in the Editor
- Some automated tests pass in CI but fail on specific machines
- Complex testing requirements across different monitor configurations

## FancyZones UI Testing

UI tests are implemented using [Windows Application Driver](https://github.com/microsoft/WinAppDriver).

### Before running tests

  - Install Windows Application Driver v1.2.1 from https://github.com/microsoft/WinAppDriver/releases/tag/v1.2.1. 
  - Enable Developer Mode in Windows settings

### Running tests
  
  - Exit PowerToys if it's running
  - Run WinAppDriver.exe from the installation directory. Skip this step if installed in the default directory (`C:\Program Files (x86)\Windows Application Driver`); in this case, it'll be launched automatically during tests.
  - Open `PowerToys.sln` in Visual Studio and build the solution.
  - Run tests in the Test Explorer (`Test > Test Explorer` or `Ctrl+E, T`). 

>Note: notifications or other application windows, that are shown above the window under test, can disrupt the testing process.

### Testing Strategy

#### Unit Tests
- Build the unit test project
- Run using the Visual Studio Test Explorer (`Test > Test Explorer` or `Ctrl+E, T`)

#### Integration Tests
- Ensure the entire FancyZones module works as expected
- Test different window layouts and snapping behaviors

### Test Framework Structure

#### UI Test Requirements
All test cases require pre-configured user data and must reset this data before each test.

**Required User Data Files**:
- EditorParameters
- AppliedLayouts
- CustomLayouts
- DefaultLayouts
- LayoutHotkeys
- LayoutTemplates
- AppZoneHistory

#### Editor Test Suite

**ApplyLayoutTest.cs**
- Verifies layout application and selection per monitor
- Tests file updates and behavior under display switching
- Validates virtual desktop changes

**CopyLayoutTests.cs**
- Tests copying various layout types
- Validates UI and file correctness

**CreateLayoutTests.cs**
- Tests layout creation and cancellation
- Focuses on file correctness validation

**CustomLayoutsTests.cs**
- Tests user-created layout operations
- Covers renaming, highlight line changes, zone count changes

**DefaultLayoutsTest.cs**
- Validates default and user layout files

**DeleteLayoutTests.cs**
- Tests layout deletion across types
- Checks both UI and file updates

**EditLayoutTests.cs**
- Tests zone operations: add/delete/move/reset/split/merge

**FirstLaunchTest.cs**
- Verifies Editor launches correctly on first run

**LayoutHotkeysTests.cs**
- Tests hotkey configuration file correctness
- Note: Actual hotkey behavior tested in FancyZones backend

**TemplateLayoutsTests.cs**
- Tests operations on built-in layouts
- Covers renaming, highlight changes, zone count changes

#### FancyZones Backend Tests

**LayoutApplyHotKeyTests.cs**
- Focuses on hotkey-related functionality
- Tests actual hotkey behavior implementation

### Extra tools and information

**Test samples**: https://github.com/microsoft/WinAppDriver/tree/master/Samples

While working on tests, you may need a tool that helps you to view the element's accessibility data, e.g. for finding the button to click. For this purpose, you could use [AccessibilityInsights](https://accessibilityinsights.io/docs/windows/overview) or [WinAppDriver UI Recorder](https://github.com/microsoft/WinAppDriver/wiki/WinAppDriver-UI-Recorder).

>Note: close helper tools while running tests. Overlapping windows can affect test results.


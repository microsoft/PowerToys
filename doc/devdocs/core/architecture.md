# PowerToys Architecture

## Module Interface Overview

Each PowerToys utility is defined by a module interface (DLL) that provides a standardized way for the PowerToys Runner to interact with it. The module interface defines:

- Structure for hotkeys
- Name and key for the utility
- Configuration management
- Enable/disable functionality
- Telemetry settings
- Group Policy Object (GPO) configuration

### Types of Modules

1. **Simple Modules** (like Mouse Pointer Crosshairs, Find My Mouse)
   - Entirely contained in the module interface
   - No external application
   - Example: Mouse Pointer Crosshairs implements the module interface directly

2. **External Application Launchers** (like Color Picker)
   - Start a separate application (e.g., WPF application in C#)
   - Handle events when hotkeys are pressed
   - Communication via named pipes or other IPC mechanisms

3. **Context Handler Modules** (like Power Rename)
   - Shell extensions for File Explorer
   - Add right-click context menu entries
   - Windows 11 context menu integration through MSIX

4. **Registry-based Modules** (like Power Preview)
   - Register preview handlers and thumbnail providers
   - Modify registry keys during enable/disable operations

## Common Dependencies and Libraries

- SPD logs for C++ (centralized logging system)
- CPP Win RT (used by most utilities)
- Common utilities in `common` folder for reuse across modules
- Interop library for C++/C# communication (converted to C++ Win RT)
- Common.UI library has WPF and WinForms dependencies

## Resource Management

- For C++ applications and module interfaces:
  - Resource files (.resx) need to be converted to .rc
  - Use conversion tools before building
  
- Different resource approaches:
  - WPF applications use .resx files
  - WinUI 3 apps use .resw files
  
- PRI file naming requirements:
  - Need to override default names to avoid conflicts during flattening

## Implementation details

### [`Runner`](runner.md)

The PowerToys Runner contains the project for the PowerToys.exe executable.
It's responsible for:

- Loading the individual PowerToys modules.
- Passing registered events to the PowerToys.
- Showing a system tray icon to manage the PowerToys.
- Bridging between the PowerToys modules and the Settings editor.

### [`Interface`](../modules/interface.md)

The definition of the interface used by the [`runner`](/src/runner) to manage the PowerToys. All PowerToys must implement this interface.

### [`Common`](../common.md)

The common lib, as the name suggests, contains code shared by multiple PowerToys components and modules, e.g. [json parsing](/src/common/utils/json.h) and [IPC primitives](/src/common/interop/two_way_pipe_message_ipc.h).

### [`Settings`](settings/readme.md)

Settings v2 is our current settings implementation. Please head over to the dev docs that describe the current settings system.

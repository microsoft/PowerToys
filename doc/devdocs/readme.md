# Code Organization

## Rules

- **Follow the pattern of what you already see in the code**
- Try to package new ideas/components into libraries that have nicely defined interfaces
- Package new ideas into classes or refactor existing ideas into a class as you extend

## Code Overview

General project organization:

#### The [`doc`](/doc) folder
Documentation for the project, including a [coding guide](/doc/coding) and [design docs](/doc/specs).

#### The [`installer`](/installer) folder
Contains the source code of the PowerToys installer.

#### The [`src`](/src) folder
Contains the source code of the PowerToys runner and of all of the PowerToys modules. **This is where the most of the magic happens.**

#### The [`tools`](/tools) folder
Various tools used by PowerToys. Includes the Visual Studio 2019 project template for new PowerToys.

# Implementation details

### [`Runner`](/doc/devdocs/runner.md)
The PowerToys Runner contains the project for the PowerToys.exe executable.
It's responsible for:
- Loading the individual PowerToys modules.
- Passing registered events to the PowerToys.
- Showing a system tray icon to manage the PowerToys.
- Bridging between the PowerToys modules and the Settings editor.

![Image of the tray icon](/doc/images/runner/tray.png)

### [`Interface`](/doc/devdocs/modules/interface.md)
Definition of the interface used by the [`runner`](/src/runner) to manage the PowerToys. All PowerToys must implement this interface.

### [`Common`](/doc/devdocs/common.md)
The common lib, as the name suggests, contains code shared by multiple PowerToys components and modules, e.g. [json parsing](/src/common/json.h) and [IPC primitives](/src/common/two_way_pipe_message_ipc.h).


### [`Settings`](/doc/devdocs/settings.md)
WebView project for editing the PowerToys settings.

The html portion of the project that is shown in the WebView is contained in [`settings-html`](/src/settings/settings-heml).
Instructions on how build a new version and update this project are in the [Web project for the Settings UI](./settings-web.md).

While developing, it's possible to connect the WebView to the development server running in localhost by setting the `_DEBUG_WITH_LOCALHOST` flag to `1` and following the instructions near it in `./main.cpp`.

### [`Settings-web`](/doc/devdocs/settings-web.md)
This project generates the web UI shown in the [PowerToys Settings](/src/editor).
It's a `ReactJS` project created using [UI Fabric](https://developer.microsoft.com/en-us/fabric#/).

## Current modules
### [`FancyZones`](/doc/devdocs/modules/fancyzones.md)
The FancyZones PowerToy that allows users to create custom zones on the screen, to which the windows will snap when moved.

### [`PowerRename`](/doc/devdocs/modules/powerrename.md)
PowerRename is a Windows Shell Context Menu Extension for advanced bulk renaming using simple search and replace or more powerful regular expression matching.

### [`Shortcut Guide`](/doc/devdocs/modules/shortcut_guide.md)
The Windows Shortcut Guide, displayed when the WinKey is held for some time.

### _obsolete_ [`example_powertoy`](/doc/devdocs/modules/example_powertoy.md)
An example PowerToy, that demonstrates how to create new ones. Please note, that this is going to become a Visual Studio project template soon.

This PowerToy serves as a sample to show how to implement the [PowerToys interface](/src/modules/interface/) when creating a PowerToy. It also showcases the currently implemented settings.

#### Options
This module has a setting to serve as an example for each of the currently implemented settings property:
  - BoolToggle property
  - IntSpinner property
  - String property
  - ColorPicker property
  - CustomAction property

![Image of the Options](/doc/images/example_powertoy/settings.png)

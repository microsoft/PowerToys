# Dev Documentation

## Rules

- **Follow the pattern of what you already see in the code.**
- [Coding style](style.md).
- Try to package new ideas/components into libraries that have nicely defined interfaces.
- Package new ideas into classes or refactor existing ideas into a class as you extend.
- When adding new classes/methos/changing existing code: add new unit tests or update the existing tests.

## Github Workflow

- Follow the PR template, in particular make sure there is open issue for the new PR.
- When the PR is approved, let the owner of the PR merge it.
- Use the `Squash and merge` option to merge a PR, if you don't want to squash it because there are logically different commits, use `Rebase and merge`.
- We don't close issues automatically when referenced in a PR, so after the OR is merged:
  - mark the issue(s) fixed by the PR with the `resolved` label.
  - don't close the issue if it's a bug in the current release since users tend to not search for closed issues, we will close the resolved issues when a new released is published.

## Repository Overview

General project organization:

### The [`doc`](/doc) folder

Documentation for the project.

### The [`Wiki`](/wiki)

The Wiki contains the current specs for the project.

### The [`installer`](/installer) folder

Contains the source code of the PowerToys installer.

### The [`src`](/src) folder

Contains the source code of the PowerToys runner and of all of the PowerToys modules. **This is where the most of the magic happens.**

### The [`tools`](/tools) folder

Various tools used by PowerToys. Includes the Visual Studio 2019 project template for new PowerToys.

## Building code

### Build Prerequisites

- Windows 10 1803 (build 10.0.17134.0) or above to build and run PowerToys.
- Visual Studio 2019 Community edition or higher, with the 'Desktop Development with C++' component and the Windows 10 SDK version 10.0.18362.0 or higher.

### Building the Code

- Open `powertoys.sln` in Visual Studio, in the `Solutions Configuration` drop-down menu select `Release` or `Debug`, from the `Build` menu choose `Build Solution`.
- The PowerToys binaries will be in your repo under `x64\Release`.
- If you want to copy the `PowerToys.exe` binary to a different location, you'll also need to copy the `modules` and the `svgs` folders.

### Building the .msi Installer

* From the `installer` folder open `PowerToysSetup.sln` in Visual Studio, in the `Solutions Configuration` drop-down menu select `Release` or `Debug`, from the `Build` menu choose `Build Solution`.
* The resulting `PowerToysSetup.msi` installer will be available in the `installer\PowerToysSetup\x64\Release\` folder.

#### Prerequisites to Build the MSI Installer

* Install the [WiX Toolset Visual Studio 2019 Extension](https://marketplace.visualstudio.com/items?itemName=RobMensching.WiXToolset).
* Install the [WiX Toolset build tools](https://wixtoolset.org/releases/).

### Building the MSIX Installer

Please follow the [installer instructions](./installer/readme.md) which include items such as creating the self-signed cert for testing.

## Debugging

The following configuration issue only applies if the user is a member of the Administrators group.

Some PowerToys modules require being run with the highest permission level if the current user is a member of the Administrators group. The highest permission level is required to be able to perform some actions when an elevated application (e.g. Task Manager) is in the foreground or is the target of an action. Without elevated privileges some PowerToys modules will still work but with some limitations:

- the `FancyZones` module will be not be able to move an elevated window to a zone.
- the `Shortcut Guide` module will not appear if the foreground window belongs to an elevated application.

To run and debug PowerToys from Visual Studio when the user is a member of the Administrators group, Visual Studio has to be started with elevated privileges. If you want to avoid running Visual Studio with elevated privileges and don't mind the limitations described above, you can do the following: open the `runner` project properties and navigate to the `Linker -> Manifest File` settings, edit the `UAC Execution Level` property and change it from `highestAvailable (level='highestAvailable')` to `asInvoker (/level='asInvoker')`, save the changes.

## How to create new PowerToys

See the instructions on [how to install the PowerToys Module project template](tools/project_template). <br />
Specifications for the [PowerToys settings API](doc/specs/PowerToys-settings.md).

## Implementation details

### [`Runner`](runner.md)

The PowerToys Runner contains the project for the PowerToys.exe executable.
It's responsible for:

- Loading the individual PowerToys modules.
- Passing registered events to the PowerToys.
- Showing a system tray icon to manage the PowerToys.
- Bridging between the PowerToys modules and the Settings editor.

![Image of the tray icon](/doc/images/runner/tray.png)

### [`Interface`](modules/interface.md)

Definition of the interface used by the [`runner`](/src/runner) to manage the PowerToys. All PowerToys must implement this interface.

### [`Common`](common.md)

The common lib, as the name suggests, contains code shared by multiple PowerToys components and modules, e.g. [json parsing](/src/common/json.h) and [IPC primitives](/src/common/two_way_pipe_message_ipc.h).

### [`Settings`](settings.md)

WebView project for editing the PowerToys settings.

The html portion of the project that is shown in the WebView is contained in [`settings-html`](/src/settings/settings-heml).
Instructions on how build a new version and update this project are in the [Web project for the Settings UI](./settings-web.md).

While developing, it's possible to connect the WebView to the development server running in localhost by setting the `_DEBUG_WITH_LOCALHOST` flag to `1` and following the instructions near it in `./main.cpp`.

### [`Settings-web`](settings-web.md)
This project generates the web UI shown in the [PowerToys Settings](/src/editor).
It's a `ReactJS` project created using [UI Fabric](https://developer.microsoft.com/en-us/fabric#/).

## Current modules
### [`FancyZones`](modules/fancyzones.md)
The FancyZones PowerToy that allows users to create custom zones on the screen, to which the windows will snap when moved.

### [`PowerRename`](modules/powerrename.md)
PowerRename is a Windows Shell Context Menu Extension for advanced bulk renaming using simple search and replace or more powerful regular expression matching.

### [`Shortcut Guide`](modules/shortcut_guide.md)
The Windows Shortcut Guide, displayed when the WinKey is held for some time.

### _obsolete_ [`example_powertoy`](modules/example_powertoy.md)
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

# Dev Documentation

## Fork, Clone, Branch and Create your PR

Once you've discussed your proposed feature/fix/etc. with a team member, and an approach or a spec has been written and approved, it's time to start development:

1. Fork the repo on GitHub if you haven't already
1. Clone your fork locally
1. Create a feature branch
1. Work on your changes
1. Create a [Draft Pull Request (PR)](https://github.blog/2019-02-14-introducing-draft-pull-requests/)
1. When ready, mark your PR as "ready for review".

## Rules

- **Follow the pattern of what you already see in the code.**
- [Coding style](style.md).
- Try to package new functionality/components into libraries that have nicely defined interfaces.
- Package new functionality into classes or refactor existing functionality into a class as you extend the code.
- When adding new classes/methods/changing existing code, add new unit tests or update the existing tests.

## GitHub Workflow

- Before starting to work on a fix/feature, make sure there is an open issue to track the work.
- Add the `In progress` label to the issue, if not already present. Also add a `Cost-Small/Medium/Large` estimate and make sure all appropriate labels are set.
- If you are a community contributor, you will not be able to add labels to the issue; in that case just add a comment saying that you have started work on the issue and try to give an estimate for the delivery date.
- If the work item has a medium/large cost, using the markdown task list, list each sub item and update the list with a check mark after completing each sub item.
- When opening a PR, follow the PR template.
- When you'd like the team to take a look (even if the work is not yet fully complete) mark the PR as 'Ready For Review' so that the team can review your work and provide comments, suggestions, and request changes. It may take several cycles, but the end result will be solid, testable, conformant code that is safe for us to merge.
- When the PR is approved, let the owner of the PR merge it. For community contributions, the reviewer who approved the PR can also merge it.
- Use the `Squash and merge` option to merge a PR. If you don't want to squash it because there are logically different commits, use `Rebase and merge`.
- We don't close issues automatically when referenced in a PR, so after the PR is merged:
  - mark the issue(s) that the PR solved with the `Resolution-Fix-Committed` label, remove the `In progress` label and if the issue is assigned to a project, move the item to the `Done` status.
  - don't close the issue if it's a bug in the current released version; since users tend to not search for closed issues, we will close the resolved issues when a new version is released.
  - if it's not a code fix that effects the end user, the issue can be closed (for example a fix in the build or a code refactoring and so on).

## Compiling PowerToys

### Prerequisites for Compiling PowerToys

1. Windows 10 April 2018 Update (version 1803) or newer
1. Visual Studio Community/Professional/Enterprise 2022 17.4 or newer
1. A local clone of the PowerToys repository

### Install Visual Studio dependencies

1. Open the `PowerToys.sln` file.
1. If you see a dialog that says `install extra components` in the solution explorer pane, click `install`

### Get Submodules to compile

We have submodules that need to be initialized before you can compile most parts of PowerToys.  This should be a one-time step.

1. Open a terminal
1. Navigate to the folder you cloned PowerToys to.
1. Run `git submodule update --init --recursive`

### Compiling Source Code

- Open `PowerToys.sln` in Visual Studio.
- In the `Solutions Configuration` drop-down menu select `Release` or `Debug`.
- From the `Build` menu choose `Build Solution`, or press <kbd>Control</kbd>+<kbd>Shift</kbd>+<kbd>b</kbd> on your keyboard.
- The build process may take several minutes depending on your computer's performance. Once it completes, the PowerToys binaries will be in your repo under `x64\Release\`.
    - You can run `x64\Release\PowerToys.exe` directly without installing PowerToys, but some modules (i.e. PowerRename, ImageResizer, File Explorer extension etc.) will not be available unless you also build the installer and install PowerToys.

## Compile the installer

Our installer is two parts, an EXE and an MSI.  The EXE (Bootstrapper) contains the MSI and handles more complex installation logic. 
- The EXE installs all prerequisites and installs PowerToys via the MSI. It has additional features such as the installation flags (see below).
- The MSI installs the PowerToys binaries.

The installer can only be compiled in `Release` mode; steps 1 and 2 must be performed before the MSI can be compiled.

1. Compile `PowerToys.sln`. Instructions are listed above.
1. Compile `BugReportTool.sln` tool. Path from root: `tools\BugReportTool\BugReportTool.sln` (details listed below)
1. Compile `WebcamReportTool.sln` tool. Path from root: `tools\WebcamReportTool\WebcamReportTool.sln` (details listed below)
1. Compile `StylesReportTool.sln` tool. Path from root: `tools\StylesReportTool\StylesReportTool.sln` (details listed below)
1. Compile `PowerToysSetup.sln` Path from root: `installer\PowerToysSetup.sln` (details listed below)

### Prerequisites for building the MSI installer

1. Install the [WiX Toolset Visual Studio 2022 Extension](https://marketplace.visualstudio.com/items?itemName=WixToolset.WixToolsetVisualStudio2022Extension).
1. Install the [WiX Toolset build tools](https://github.com/wixtoolset/wix3/releases/tag/wix3141rtm). (installer [direct link](https://github.com/wixtoolset/wix3/releases/download/wix3141rtm/wix314.exe))
1. Download [WiX binaries](https://github.com/wixtoolset/wix3/releases/download/wix3141rtm/wix314-binaries.zip) and extract `wix.targets` to `C:\Program Files (x86)\WiX Toolset v3.14`.

### Building prerequisite projects

#### From the command line

1. From the start menu, open a `Developer Command Prompt for VS 2022`
1. Ensure `nuget.exe` is in your `%path%`
1. In the repo root, run these commands:
  
```
nuget restore .\tools\BugReportTool\BugReportTool.sln
msbuild -p:Platform=x64 -p:Configuration=Release .\tools\BugReportTool\BugReportTool.sln

nuget restore .\tools\WebcamReportTool\WebcamReportTool.sln
msbuild -p:Platform=x64 -p:Configuration=Release .\tools\WebcamReportTool\WebcamReportTool.sln

nuget restore .\tools\StylesReportTool\StylesReportTool.sln
msbuild -p:Platform=x64 -p:Configuration=Release .\tools\StylesReportTool\StylesReportTool.sln
```

#### From Visual Studio

If you prefer, you can alternatively build prerequisite projects for the installer using the Visual Studio UI.

1. Open `tools\BugReportTool\BugReportTool.sln`
1. In Visual Studio, in the `Solutions Configuration` drop-down menu select `Release`
1. From the `Build` menu, choose `Build Solution`.
1. Open `tools\WebcamReportTool\WebcamReportTool.sln`
1. In Visual Studio, in the `Solutions Configuration` drop-down menu select `Release`
1. From the `Build` menu, choose `Build Solution`.
1. Open `tools\StylesReportTool\StylesReportTool.sln`
1. In Visual Studio, in the `Solutions Configuration` drop-down menu select `Release`
1. From the `Build` menu, choose `Build Solution`.

### Locally compiling the installer

1. Open `installer\PowerToysSetup.sln`
1. In Visual Studio, in the `Solutions Configuration` drop-down menu select `Release`
1. From the `Build` menu choose `Build Solution`.

The resulting `PowerToysSetup.msi` installer will be available in the `installer\PowerToysSetup\x64\Release\` folder.

#### Supported arguments for the .EXE Bootstrapper installer

Head over to the wiki to see the [full list of supported installer arguments][installerArgWiki].

## Debugging

To debug the PowerToys application in Visual Studio, set the `runner` project as your start-up project, then start the debugger.

Some PowerToys modules must be run with the highest permission level if the current user is a member of the Administrators group. The highest permission level is required to be able to perform some actions when an elevated application (e.g. Task Manager) is in the foreground or is the target of an action. Without elevated privileges some PowerToys modules will still work but with some limitations:

- The `FancyZones` module will not be able to move an elevated window to a zone.
- The `Shortcut Guide` module will not appear if the foreground window belongs to an elevated application.

Therefore, it is recommended to run Visual Studio with elevated privileges when debugging these scenarios. If you want to avoid running Visual Studio with elevated privileges and don't mind the limitations described above, you can do the following: open the `runner` project properties and navigate to the `Linker -> Manifest File` settings, edit the `UAC Execution Level` property and change it from `highestAvailable (level='highestAvailable')` to `asInvoker (/level='asInvoker').

## How to create new PowerToys

See the instructions on [how to install the PowerToys Module project template](/tools/project_template). <br />
Specifications for the [PowerToys settings API](settingsv2/readme.md).

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

The definition of the interface used by the [`runner`](/src/runner) to manage the PowerToys. All PowerToys must implement this interface.

### [`Common`](common.md)

The common lib, as the name suggests, contains code shared by multiple PowerToys components and modules, e.g. [json parsing](/src/common/utils/json.h) and [IPC primitives](/src/common/interop/two_way_pipe_message_ipc.h).

### [`Settings`](settingsv2/)

Settings v2 is our current settings implementation.  Please head over to the dev docs that describe the current settings system.

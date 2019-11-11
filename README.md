# Overview

<img align="right" width="200" src="./doc/images/Logo.jpg">

PowerToys is a set of utilities for power users to tune and streamline their Windows experience for greater productivity. Inspired by the [Windows 95 era PowerToys project](https://en.wikipedia.org/wiki/Microsoft_PowerToys), this reboot provides power users with ways to squeeze more efficiency out of the Windows 10 shell and customize it for individual workflows.  A great overview of the Windows 95 PowerToys can be found [here](https://socket3.wordpress.com/2016/10/22/using-windows-95-powertoys/).

## Build Status

[![Build Status](https://dev.azure.com/ms/PowerToys/_apis/build/status/microsoft.PowerToys?branchName=master)](https://dev.azure.com/ms/PowerToys/_build?definitionId=35096)

## Installation

_**Note:** in order to run PowerToys, you'll need to be running at least Windows build 17134 or higher._

The latest release of PowerToys can be downloaded currently a few different ways.  Our current recommended way is via GitHub.

| x64 | x86 | ARM |
|:---:|:---:|:---:|
| Supported | [Issue #620](https://github.com/microsoft/PowerToys/issues/602)  | [Issue #490](https://github.com/microsoft/PowerToys/issues/490)|

### GitHub

The preview of these utilities can be installed from the [PowerToys GitHub releases page](https://github.com/Microsoft/powertoys/releases). Click on `Assets` to show the files available in the release and then click on `PowerToysSetup.msi` to download the PowerToys installer. <br />
PDB symbols for the release are available in a separate zip file `PDB symbols.zip`.

### Windows Store

On backlog, [Issue #413](https://github.com/microsoft/PowerToys/issues/413) 

### Chocolatey (Unofficial)

Download and upgrade PowerToys from [Chocolatey](https://chocolatey.org).

To install PowerToys, run the following command from the command line or from PowerShell:

```powershell
choco install powertoys
```

To upgrade PowerToys, run the following command from the command line or from PowerShell:

```powershell
choco upgrade powertoys
```

If you have any issues when installing/upgrading the package please go to the [package page](https://chocolatey.org/packages/powertoys) and follow the [Chocolatey triage process](https://chocolatey.org/docs/package-triage-process)

## PowerToy Utilities

### FancyZones

[FancyZones](/src/modules/fancyzones/) - FancyZones is a window manager that makes it easy to create complex window layouts and quickly position windows into those layouts.  The FancyZones backlog can be found [here](https://github.com/Microsoft/PowerToys/tree/master/doc/planning/FancyZonesBacklog.md)

### Shortcut

[Windows key shortcut guide](/src/modules/shortcut_guide) - The shortcut guide appears when a user holds the Windows key down for more than one second and shows the available shortcuts for the current state of the desktop.  The shortcut guide backlog can be found [here](https://github.com/Microsoft/PowerToys/tree/master/doc/planning/ShortcutGuideBacklog.md)

### PowerRename

[PowerRename](/src/modules/powerrename) - PowerRename is a Windows Shell Extension for advanced bulk renaming using search and replace or regular expressions. PowerRename allows simple search and replace or more advanced regular expression matching. While you type in the search and replace input fields, the preview area will show what the items will be renamed to. PowerRename then calls into the Windows Explorer file operations engine to perform the rename. This has the benefit of allowing the rename operation to be undone after PowerRename exits.  

Chris Davis contributed his [SmartRename tool](https://github.com/chrdavis/SmartRename) into PowerToys!

### Additional utilities in the pipeline are

* Maximize to new desktop widget - The MTND widget shows a pop-up button when a user hovers over the maximize / restore button on any window.  Clicking it creates a new desktop, sends the app to that desktop and maximizes the app on the new desktop.
* [Process terminate tool](https://github.com/indierawk2k2/PowerToys-1/blob/master/specs/Terminate%20Spec.md)
* [Animated gif screen recorder](https://github.com/indierawk2k2/PowerToys-1/blob/master/specs/GIF%20Maker%20Spec.md)

### Backlog

The full backlog of utilities can be found [here](https://github.com/Microsoft/PowerToys/tree/master/doc/planning/PowerToysBacklog.md)

## What's Happening

### October Update

**Update** - We've shipped 0.12!  This release includes bug fixes, addresses issues and implements many feature suggestions.  This build and installer is also signed by Microsoft.  Last, but not least, this release includes a new utility: Chris Davis has integrated  [SmartRename tool](https://github.com/chrdavis/SmartRename) into PowerToys!

![SmartRename](https://github.com/microsoft/PowerToys/raw/master/src/modules/powerrename/images/PowerRenameDemo.gif)

## Developer Guidance

### Build Prerequisites

* Windows 10 1803 (build 10.0.17134.0) or above to build and run PowerToys.
* Visual Studio 2019 Community edition or higher, with the 'Desktop Development with C++' component and the Windows 10 SDK version 10.0.18362.0 or higher.

### Building the Code

* Open `powertoys.sln` in Visual Studio, in the `Solutions Configuration` drop-down menu select `Release` or `Debug`, from the `Build` menu choose `Build Solution`.
* The PowerToys binaries will be in your repo under `x64\Release`.
* If you want to copy the `PowerToys.exe` binary to a different location, you'll also need to copy the `modules` and the `svgs` folders.

### Prerequisites to Build the Installer

* Install the [WiX Toolset Visual Studio 2019 Extension](https://marketplace.visualstudio.com/items?itemName=RobMensching.WiXToolset).
* Install the [WiX Toolset build tools](https://wixtoolset.org/releases/).

### Building the .msi Installer

* From the `installer` folder open `PowerToysSetup.sln` in Visual Studio, in the `Solutions Configuration` drop-down menu select `Release` or `Debug`, from the `Build` menu choose `Build Solution`.
* The resulting `PowerToysSetup.msi` installer will be available in the `installer\PowerToysSetup\x64\Release\` folder.

### Debugging

The following configuration issue only applies if the user is a member of the Administrators group.

Some PowerToys modules require being run with the highest permission level if the current user is a member of the Administrators group. The highest permission level is required to be able to perform some actions when an elevated application (e.g. Task Manager) is in the foreground or is the target of an action. Without elevated privileges some PowerToys modules will still work but with some limitations:

* the `FancyZones` module will be not be able to move an elevated window to a zone.
* the `Shortcut Guide` module will not appear if the foreground window belongs to an elevated application.

To run and debug PowerToys from Visual Studio when the user is a member of the Administrators group, Visual Studio has to be started with elevated privileges. If you want to avoid running Visual Studio with elevated privileges and don't mind the limitations described above, you can do the following: open the `runner` project properties and navigate to the `Linker -> Manifest File` settings, edit the `UAC Execution Level` property and change it from `highestAvailable (level='highestAvailable')` to `asInvoker (/level='asInvoker')`, save the changes.

### How to create new PowerToys

See the instructions on [how to install the PowerToys Module project template](tools/project_template). <br />
Specifications for the [PowerToys settings API](doc/specs/PowerToys-settings.md).

### Coding Guidance

Please review these brief docs below relating to our coding standards etc.

> 👉 If you find something missing from these docs, feel free to contribute to any of our documentation files anywhere in the repository (or make some new ones\!)

This is a work in progress as we learn what we'll need to provide people in order to be effective contributors to our project.

* [Coding Style](doc/coding/style.md)
* [Code Organization](doc/coding/organization.md)

## Contributing

This project welcomes contributions and suggestions and we are excited to work with the power user community to build a set of tools for helping you get the most out of Windows.

We ask that **before you start work on a feature that you would like to contribute**, please read our [Contributor's Guide](contributing.md). We will be happy to work with you to figure out the best approach, provide guidance and mentorship throughout feature development, and help avoid any wasted or duplicate effort.

> ⚠ **Note**: PowerToys is still a nascent project and the team is actively working out of this repository.  We will be periodically re-structuring the code to make it easier to comprehend, navigate, build, test, and contribute to, so **DO expect significant changes to code layout on a regular basis**.

> ⚠ **License Info**: Most contributions require you to agree to a Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us the rights to use your contribution. For details, visit https://cla.opensource.microsoft.com.

When you submit a pull request, a CLA-bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g. status check, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

## Code of Conduct

This project has adopted the [Microsoft Open Source Code of Conduct][conduct-code]. For more information see the [Code of Conduct FAQ][conduct-FAQ] or contact [opencode@microsoft.com][conduct-email] with any additional questions or comments.

[conduct-code]: https://opensource.microsoft.com/codeofconduct/ 
[conduct-FAQ]: https://opensource.microsoft.com/codeofconduct/faq/
[conduct-email]: mailto:opencode@microsoft.com

## Privacy Statement

The application logs basic telemetry. Our Telemetry Data page (Coming Soon) has the trends from the telemetry. Please read the [Microsoft privacy statement](http://go.microsoft.com/fwlink/?LinkId=521839) for more information.

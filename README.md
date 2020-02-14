# Overview

<img align="right" width="200" src="./doc/images/Logo.jpg">

Microsoft PowerToys is a set of utilities for power users to tune and streamline their Windows experience for greater productivity. Inspired by the [Windows 95 era PowerToys project](https://en.wikipedia.org/wiki/Microsoft_PowerToys), this reboot provides power users with ways to squeeze more efficiency out of the Windows 10 shell and customize it for individual workflows.  A great overview of the Windows 95 PowerToys can be found [here](https://socket3.wordpress.com/2016/10/22/using-windows-95-powertoys/).

## Build Status

[![Build Status](https://dev.azure.com/ms/PowerToys/_apis/build/status/microsoft.PowerToys?branchName=master)](https://dev.azure.com/ms/PowerToys/_build?definitionId=35096)

## Installing and running Microsoft PowerToys

> 👉 Note: Microsoft PowerToys requires Windows 10 1803 (build 17134) or later

### Microsoft Store [RECOMMENED]

Install [Microsoft PowerToys from the Microsoft Store][store-install-link]. This allows you to always be on the latest version when we release new builds with automatic upgrades. 

This is our preferred method.

### Other install methods

#### Via GitHub

The preview of these utilities can be installed from the [PowerToys GitHub releases page](/releases). Click on `Assets` to show the files available in the release and then click on `PowerToysSetup.msix` to download the PowerToys installer. <br />
PDB symbols for the release are available in a separate zip file `PDB symbols.zip`.

#### Via Chocolatey (Unofficial)

Download and upgrade PowerToys from [Chocolatey](https://chocolatey.org). If you have any issues when installing/upgrading the package please go to the [package page](https://chocolatey.org/packages/powertoys) and follow the [Chocolatey triage process](https://chocolatey.org/docs/package-triage-process)

To install PowerToys, run the following command from the command line / PowerShell:

```powershell
choco install powertoys
```

To upgrade PowerToys, run the following command from the command line / PowerShell:

```powershell
choco upgrade powertoys
```

### Processor support

We currently support the matrix below.  Adding Store/MSIX support will make supporting x86 and ARM much easier.

| x64 | x86 | ARM |
|:---:|:---:|:---:|
| [Install][store-install-link] | [Issue #602](/issues/602)  | [Issue #490](/issues/490)|

## Current PowerToy Utilities

### FancyZones

[FancyZones](/src/modules/fancyzones/) - FancyZones is a window manager that makes it easy to create complex window layouts and quickly position windows into those layouts.  The FancyZones backlog can be found [here](https://github.com/Microsoft/PowerToys/tree/master/doc/planning/FancyZonesBacklog.md)

### Shortcut

[Windows key shortcut guide](/src/modules/shortcut_guide) - The shortcut guide appears when a user holds the Windows key down for more than one second and shows the available shortcuts for the current state of the desktop.  The shortcut guide backlog can be found [here](https://github.com/Microsoft/PowerToys/tree/master/doc/planning/ShortcutGuideBacklog.md)

### PowerRename

[PowerRename](/src/modules/powerrename) - PowerRename is a Windows Shell Extension for advanced bulk renaming using search and replace or regular expressions. PowerRename allows simple search and replace or more advanced regular expression matching. While you type in the search and replace input fields, the preview area will show what the items will be renamed to. PowerRename then calls into the Windows Explorer file operations engine to perform the rename. This has the benefit of allowing the rename operation to be undone after PowerRename exits.  

### Version 1.0 plan

Our plan for all the [goals and utilities for v1.0 detailed over here in the wiki](/wiki/Version-1.0-Strategy).

## What's Happening

### February 2020 Update

CLINT TO FILL IN

## Developer Guidance

Please read the [developer docs](/docs/devdocs/) for a detailed breakdown.

## Contributing

This project welcomes contributions of all times. Help spec'ing, design, documentation, finding bugs are ways everyone can help on top of coding features / bug fixes. We are excited to work with the power user community to build a set of tools for helping you get the most out of Windows.

We ask that **before you start work on a feature that you would like to contribute**, please read our [Contributor's Guide](contributing.md). We will be happy to work with you to figure out the best approach, provide guidance and mentorship throughout feature development, and help avoid any wasted or duplicate effort.

> ⚠ **Note**: PowerToys is still a nascent project and the team is actively working out of this repository.  We will be periodically re-structuring the code to make it easier to comprehend, navigate, build, test, and contribute to, so **DO expect significant changes to code layout on a regular basis**.

> ⚠ **License Info**: Most contributions require you to agree to a Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us the rights to use your contribution. For details, visit <https://cla.opensource.microsoft.com.>

## Code of Conduct

This project has adopted the [Microsoft Open Source Code of Conduct][conduct-code]. For more information see the [Code of Conduct FAQ][conduct-FAQ] or contact [opencode@microsoft.com][conduct-email] with any additional questions or comments.

## Privacy Statement

The application logs basic telemetry. Our Telemetry Data page (Coming Soon) has the trends from the telemetry. Please read the [Microsoft privacy statement](http://go.microsoft.com/fwlink/?LinkId=521839) for more information.

[conduct-code]: https://opensource.microsoft.com/codeofconduct/
[conduct-FAQ]: https://opensource.microsoft.com/codeofconduct/faq/
[conduct-email]: mailto:opencode@microsoft.com
[store-install-link]: https://microsoft.com
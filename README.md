# Overview

<img align="right" width="200" src="./doc/images/Logo.jpg" />

Microsoft PowerToys is a set of utilities for power users to tune and streamline their Windows experience for greater productivity. Inspired by the [Windows 95 era PowerToys project](https://en.wikipedia.org/wiki/Microsoft_PowerToys), this reboot provides power users with ways to squeeze more efficiency out of the Windows 10 shell and customize it for individual workflows.  A great overview of the Windows 95 PowerToys can be found [here](https://socket3.wordpress.com/2016/10/22/using-windows-95-powertoys/).

## Build Status

[![Build Status](https://dev.azure.com/ms/PowerToys/_apis/build/status/microsoft.PowerToys?branchName=master)](https://dev.azure.com/ms/PowerToys/_build?definitionId=35096)

## Installing and running Microsoft PowerToys

> 👉 **Note:** Microsoft PowerToys requires Windows 10 1803 (build 17134) or later.

> 👉 **Upgrading to 0.15:** You need to reapply your zone layout for FancyZones.  Don't worry, your custom zone sets are preserved.

### Via Github with MSI [Recommended]

Install from the [Microsoft PowerToys GitHub releases page][github-release-link]. Click on `Assets` to show the files available in the release and then click on `PowerToysSetup-0.15.0-x64.msi` to download the PowerToys installer.

This is our preferred method.

### Other install methods

#### Via GitHub with MSIX - ⚠ Experimental ⚠

The experimental version of PowerToys using MSIX is available.  It can be installed from the [PowerToys GitHub releases page][github-release-link].

Click on `Assets` to show the files available in the release and then click on `PowerToysSetup-MSIX-0.15.0.zip` to download the PowerToys installer zip.  From there, please read the ReadMe and you can double click to install the MSIX file.

##### Known issues with MSIX Build

- For PowerRename, you may need to restart your machine to get this to work for the first time.

#### Via Chocolatey - ⚠ Unofficial ⚠

Download and upgrade PowerToys from [Chocolatey](https://chocolatey.org). If you have any issues when installing/upgrading the package please go to the [package page](https://chocolatey.org/packages/powertoys) and follow the [Chocolatey triage process](https://chocolatey.org/docs/package-triage-process)

To install PowerToys, run the following command from the command line / PowerShell:

```powershell
choco install powertoys
```

To upgrade PowerToys, run the following command from the command line / PowerShell:

```powershell
choco upgrade powertoys
```

### Microsoft Store

On backlog, [Issue #413](https://github.com/microsoft/PowerToys/issues/413)

### Processor support

We currently support the matrix below.  Adding MSIX support will make supporting x86 and ARM much easier.

| x64 | x86 | ARM |
|:---:|:---:|:---:|
| [Install][github-release-link] | [Issue #602](https://github.com/microsoft/PowerToys/issues/602)  | [Issue #490](https://github.com/microsoft/PowerToys/issues/490)|

## Current PowerToy Utilities

### FancyZones

[FancyZones](/src/modules/fancyzones/) - FancyZones is a window manager that makes it easy to create complex window layouts and quickly position windows into those layouts.

### Shortcut

[Windows key shortcut guide](/src/modules/shortcut_guide) - The shortcut guide appears when a user holds the Windows key down for more than one second and shows the available shortcuts for the current state of the desktop.

### PowerRename

[PowerRename](/src/modules/powerrename) - PowerRename is a Windows Shell Extension for advanced bulk renaming using search and replace or regular expressions. PowerRename allows simple search and replace or more advanced regular expression matching. While you type in the search and replace input fields, the preview area will show what the items will be renamed to. PowerRename then calls into the Windows Explorer file operations engine to perform the rename. This has the benefit of allowing the rename operation to be undone after PowerRename exits.

### Version 1.0 plan

Our plan for all the [goals and utilities for v1.0 detailed over here in the wiki][v1].

## What's Happening

### February 2020 Update

Our mantra for the 0.15 was infrastructure, quality, stability and work toward getting a way to auto-update PowerToys.  While it took a bit longer to get here, we feel it was worth the extra time to fix bugs that really impacted your experience with PowerToys.

Below are just a few of the bullet items from this release.

- We shipped [v0.15][github-release-link]!
- Make you aware there is a new version from within PowerToys
- Removed requirement to always 'run as admin'
- Added almost 300 unit tests to increase stability and prevent regressions.
- Resolved almost 100 issues
- Made .NET Framework parts of the source run faster with NGEN
- Improved for how we store data locally
- Increased FancyZones compatibility with applications
- Initial work for 4 new PowerToys added for 0.16!
- Created the [v1.0 strategy][v1], the [launcher](https://github.com/microsoft/PowerToys/wiki/Launcher), the [keyboard manager](https://github.com/microsoft/PowerToys/wiki/Keyboard-Manager) specs
- Work on cleaning up our issue backlog and labels

For 0.16, we have some fun things planned and hopefully will be able to ship pretty quickly. Here are the new utilities we'll enable:

- An alternative to Alt-Tab PowerToy
- SVG preview pane for support Explorer
- Markdown preview pane support for Explorer
- Image Resizer PowerToy

## Developer Guidance

Please read the [developer docs](/doc/devdocs) for a detailed breakdown.

## Contributing

This project welcomes contributions of all times. Help spec'ing, design, documentation, finding bugs are ways everyone can help on top of coding features / bug fixes. We are excited to work with the power user community to build a set of tools for helping you get the most out of Windows.

We ask that **before you start work on a feature that you would like to contribute**, please read our [Contributor's Guide](contributing.md). We will be happy to work with you to figure out the best approach, provide guidance and mentorship throughout feature development, and help avoid any wasted or duplicate effort.

### ⚠ State of code ⚠

PowerToys is still a very fluidic project and the team is actively working out of this repository.  We will be periodically re-structuring/refactoring the code to make it easier to comprehend, navigate, build, test, and contribute to, so **DO expect significant changes to code layout on a regular basis**.

### License Info

 Most contributions require you to agree to a [Contributor License Agreement (CLA)][oss-CLA] declaring that you have the right to, and actually do, grant us the rights to use your contribution.

## Code of Conduct

This project has adopted the [Microsoft Open Source Code of Conduct][oss-conduct-code]. For more information see the [Code of Conduct FAQ][oss-conduct-FAQ] or contact [opencode@microsoft.com][oss-conduct-email] with any additional questions or comments.

## Privacy Statement

The application logs basic telemetry. Our Telemetry Data page (Coming Soon) has the trends from the telemetry. Please read the [Microsoft privacy statement][privacyLink] for more information.

[oss-CLA]: https://cla.opensource.microsoft.com
[oss-conduct-code]: https://opensource.microsoft.com/codeofconduct/
[oss-conduct-FAQ]: https://opensource.microsoft.com/codeofconduct/faq/
[oss-conduct-email]: mailto:opencode@microsoft.com
[github-release-link]: https://github.com/microsoft/PowerToys/releases/
[v1]: https://github.com/microsoft/PowerToys/wiki/Version-1.0-Strategy
[privacyLink]: http://go.microsoft.com/fwlink/?LinkId=521839

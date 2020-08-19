# Overview

<img src="./doc/images/overview/PT%20hero%20image.png"/>

Microsoft PowerToys is a set of utilities for power users to tune and streamline their Windows experience for greater productivity. Inspired by the [Windows 95 era PowerToys project](https://en.wikipedia.org/wiki/Microsoft_PowerToys), this reboot provides power users with ways to squeeze more efficiency out of the Windows 10 shell and customize it for individual workflows.  A great overview of the Windows 95 PowerToys can be found [here](https://socket3.wordpress.com/2016/10/22/using-windows-95-powertoys/).

[What's Happening](#whats-happening)   |   [Downloading & Release notes][github-release-link]   |   [Contributing to PowerToys](#contributing) | [Known issues](#known-issues)

## Build status

| Branch | Status x64 |
|---|---|
| Master | [![Build Status for Master](https://dev.azure.com/ms/PowerToys/_apis/build/status/microsoft.PowerToys?branchName=master)](https://dev.azure.com/ms/PowerToys/_build/latest?definitionId=219&branchName=master) |
| Stable | [![Build Status for Stable](https://dev.azure.com/ms/PowerToys/_apis/build/status/microsoft.PowerToys?branchName=stable)](https://dev.azure.com/ms/PowerToys/_build/latest?definitionId=219&branchName=stable) |
| Installer | [![Build Status for Installer](https://github-private.visualstudio.com/microsoft/_apis/build/status/CDPX/powertoys/powertoys-Windows-Official-master-Test?branchName=master)](https://github-private.visualstudio.com/microsoft/_build/latest?definitionId=61&branchName=master) | 

## Current PowerToy Utilities

### Color Picker

[<img align="left" src="./doc/images/overview/ColorPicker_small.png" />](https://aka.ms/PowerToysOverview_ColorPicker) [ColorPicker](https://aka.ms/PowerToysOverview_ColorPicker) is a simple and quick system-wide color picker with <kbd>Win</kbd>+<kbd>Shift</kbd>+<kbd>C</kbd>. Color Picker allows to pick colors from any currently running application and automatically copies the HEX or RGB values to your clipboard. This code is based on [Martin Chrzan's Color Picker](https://github.com/martinchrzan/ColorPicker).
<br/>
<br/>
<br/>
<br/>

### FancyZones

[<img align="left" src="./doc/images/overview/FancyZones_small.png" />](https://aka.ms/PowerToysOverview_FancyZones) [FancyZones](https://aka.ms/PowerToysOverview_FancyZones) is a window manager that makes it easy to create complex window layouts and quickly position windows into those layouts.
<br/>
<br/>
<br/>
<br/>
<br/>

### File Explorer Add-ons

[<img align="left" src="./doc/images/overview/PowerPreview_small.png" />](https://aka.ms/PowerToysOverview_FileExplorerAddOns) [File Explorer](https://aka.ms/PowerToysOverview_FileExplorerAddOns) add-ons will enable SVG icon rendering and Preview Pane additions for File Explorer. 

Preview Pane is an existing feature in the File Explorer.  To enable it, you just click the View tab in the ribbon and then click "Preview Pane". PowerToys will now enable two types of files to be previewed: Markdown (.md) & SVG (.svg)
<br/>
<br/>

### Image Resizer

[<img align="left" src="./doc/images/overview/ImageResizer_small.png" />](https://aka.ms/PowerToysOverview_ImageResizer) [Image Resizer](https://aka.ms/PowerToysOverview_ImageResizer) is a Windows Shell Extension for quickly resizing images.  With a simple right click from File Explorer, resize one or many images instantly. This code is based on [Brice Lambson's Image Resizer](https://github.com/bricelam/ImageResizer).
<br/>
<br/>
<br/>
<br/>

### Keyboard Manager

[<img align="left" src="./doc/images/overview/KBM_small.png" />](https://aka.ms/PowerToysOverview_KeyboardManager) [Keyboard Manager](https://aka.ms/PowerToysOverview_KeyboardManager) allows you to customize the keyboard to be more productive by remapping keys and creating your own keyboard shortcuts. This PowerToy requires Windows 10 1903 (build 18362) or later.
<br/>
<br/>
<br/>
<br/>

### PowerRename

[<img align="left" src="./doc/images/overview/PowerRename_small.png" />](https://aka.ms/PowerToysOverview_PowerRename) [PowerRename](https://aka.ms/PowerToysOverview_PowerRename) is a Windows Shell Extension for advanced bulk renaming using search and replace or regular expressions. PowerRename allows simple search and replace or more advanced regular expression matching. While you type in the search and replace input fields, the preview area will show what the items will be renamed to. PowerRename then calls into the Windows Explorer file operations engine to perform the rename. This has the benefit of allowing the rename operation to be undone after PowerRename exits. This code is based on [Chris Davis's SmartRename](https://github.com/chrdavis/SmartRename).
<br/>

### PowerToys Run

[<img align="left" src="./doc/images/overview/PowerLauncher_small.png" />](https://aka.ms/PowerToysOverview_PowerToysRun) [PowerToys Run](https://aka.ms/PowerToysOverview_PowerToysRun) is a new toy in PowerToys that can help you search and launch your app instantly with a simple <kbd>Alt</kbd>+<kbd>Space</kbd> and start typing! It is open source and modular for additional plugins.  Window Walker is now inside too! This PowerToy requires Windows 10 1903 (build 18362) or later.
<br/>
<br/>
<br/>
<br/>

### Shortcut Guide

[<img align="left" src="./doc/images/overview/ShortcutGuide_small.png" />](https://aka.ms/PowerToysOverview_ShortcutGuide)  [Windows key shortcut guide](https://aka.ms/PowerToysOverview_ShortcutGuide) appears when a user holds the Windows key down for more than one second and shows the available shortcuts for the current state of the desktop.
<br/>
<br/>
<br/>
<br/>
<br/>

## Installing and running Microsoft PowerToys

#### Requirements
- Windows 10 1803 (build 17134) or later.
- Have [.NET Core 3.1 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet-core/thank-you/runtime-desktop-3.1.4-windows-x64-installer). The installer should handle this but we want to directly make people aware.

#### 0.18 users for updating via notifications

- We adjusted how upgrading works in 0.20.  In 0.19 we accounted for this upcoming change but if you are going from 0.18 to 0.20, please directly use the installer file.

### Via GitHub with EXE [Recommended]

Install from the [Microsoft PowerToys GitHub releases page][github-release-link]. Click on `Assets` to show the files available in the release and then click on `PowerToysSetup-0.20.1-x64.exe` to download the PowerToys installer.

This is our preferred method.

### Via WinGet (Preview)
Download PowerToys from [WinGet](https://github.com/microsoft/winget-cli/releases). To install PowerToys, run the following command from the command line / PowerShell:

```powershell
WinGet install powertoys
```

### Other install methods

There are [community driven install methods](./doc/unofficalInstallMethods.md) such as Chocolatey and Scoop.  If these are your perferred install solutions, this will have the install instructions.

### Known issues

- Color Picker at times won't work when PT is running elevated - [#5348](https://github.com/microsoft/PowerToys/issues/5348).  We are currently working on a fix now for this.

### Processor support

We currently support the matrix below.

| x64 | x86 | ARM |
|:---:|:---:|:---:|
| [Supported][github-release-link] | [Issue #602](https://github.com/microsoft/PowerToys/issues/602) | [Issue #490](https://github.com/microsoft/PowerToys/issues/490) |

## What's Happening

### July 2020 Update

Our goals for 0.20 release cycle were to focus on adding in a few new features along with a heavy focus on stability / quality fixes. Martin Chrzan helped contribute a new utility that was in our 2021 plans, a screen wide color picker!  Chris Davis also helped contribute a SVG icon support for File Explorer!

Between 0.19 and 0.20 releases, we tracked down a lot of performance and memory issues with PowerToys Run as well. The most important ones got added to 0.19.1 and 0.19.2 releases. If you find something not working correctly, please make us aware.

In addition, we'd like to thank everyone who filed a bug, gave feedback or made a pull-request. The PowerToys team is extremely grateful to have the support of an amazing active community.

Here are a few highlights from July

- We shipped [v0.20][github-release-link]!
- [Martin Chrzan's Color Picker](https://github.com/martinchrzan/) was added in!  With a quick <kbd>Win</kbd>+<kbd>Shift</kbd>+<kbd>C</kbd>, get the color from your screen
- File Explorer - Can now render SVG icons thanks to Chris Davis
- FancyZones - you can now snap to any number of zones in FancyZones holding <kbd>Shift</kbd>+<kbd>Ctrl</kbd> while dragging a window
- PT Run - keyboard interaction improvements
- PT Run - freshly installed apps are now being detected
- PT Run - Lots of perf and bug fixes
- Keyboard manager - app level shortcuts 
   - Example: For Outlook, Remap <kbd>Ctrl</kbd>+<kbd>F</kbd> to <kbd>F4</kbd> and now <kbd>Ctrl</kbd>+<kbd>F</kbd> will put up the find window :)
- Keyboard manager - Now can remap key to shortcut and shortcut to key.
- Settings - Now has improved OOBE based on the work the Microsoft Garage Interns did during their hackathon
- PowerRename improvements

For [0.21](https://github.com/microsoft/PowerToys/issues?q=is%3Aopen+is%3Aissue+project%3Amicrosoft%2FPowerToys%2F8), we are proactively working on:

- Stability
- Quality
- Localization
- FancyZones improvements

### PowerToys 2020 roadmap

Our roadmap for all the [goals and utilities for 2020 detailed over here in the wiki][v1].

## Developer Guidance

Please read the [developer docs](/doc/devdocs) for a detailed breakdown.

## Contributing

This project welcomes contributions of all types. Help spec'ing, design, documentation, finding bugs are ways everyone can help on top of coding features / bug fixes. We are excited to work with the power user community to build a set of tools for helping you get the most out of Windows.

We ask that **before you start work on a feature that you would like to contribute**, please read our [Contributor's Guide](contributing.md). We will be happy to work with you to figure out the best approach, provide guidance and mentorship throughout feature development, and help avoid any wasted or duplicate effort.

### ⚠ State of code ⚠

PowerToys is still a very fluidic project and the team is actively working out of this repository.  We will be periodically re-structuring/refactoring the code to make it easier to comprehend, navigate, build, test, and contribute to, so **DO expect significant changes to code layout on a regular basis**.

### License Info

 Most contributions require you to agree to a [Contributor License Agreement (CLA)][oss-CLA] declaring that you have the right to, and actually do, grant us the rights to use your contribution.

## Code of Conduct

This project has adopted the [Microsoft Open Source Code of Conduct][oss-conduct-code].

## Privacy Statement

The application logs basic telemetry. Our Telemetry Data page (Coming Soon) has the trends from the telemetry. Please read the [Microsoft privacy statement][privacyLink] for more information.

[oss-CLA]: https://cla.opensource.microsoft.com
[oss-conduct-code]: CODE_OF_CONDUCT.md
[github-release-link]: https://github.com/microsoft/PowerToys/releases/
[v1]: https://github.com/microsoft/PowerToys/wiki/Version-1.0-Strategy
[privacyLink]: http://go.microsoft.com/fwlink/?LinkId=521839

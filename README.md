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

[<img align="left" src="https://aka.ms/powerToysColorPickerImageSmall" />](https://aka.ms/PowerToysOverview_ColorPicker) [ColorPicker](https://aka.ms/PowerToysOverview_ColorPicker) is a simple and quick system-wide color picker with <kbd>Win</kbd>+<kbd>Shift</kbd>+<kbd>C</kbd>. Color Picker allows to pick colors from any currently running application and automatically copies the HEX or RGB values to your clipboard. This code is based on [Martin Chrzan's Color Picker](https://github.com/martinchrzan/ColorPicker).
<br/>
<br/>
<br/>

### FancyZones

[<img align="left" src="https://aka.ms/powerToysFancyZoneImageSmall" />](https://aka.ms/PowerToysOverview_FancyZones) [FancyZones](https://aka.ms/PowerToysOverview_FancyZones) is a window manager that makes it easy to create complex window layouts and quickly position windows into those layouts.
<br/>
<br/>
<br/>
<br/>
<br/>

### File Explorer Add-ons

[<img align="left" src="https://aka.ms/powerToysPowerPreviewImageSmall" />](https://aka.ms/PowerToysOverview_FileExplorerAddOns) [File Explorer](https://aka.ms/PowerToysOverview_FileExplorerAddOns) add-ons will enable SVG icon rendering and Preview Pane additions for File Explorer. 

Preview Pane is an existing feature in the File Explorer.  To enable it, you just click the View tab in the ribbon and then click "Preview Pane". PowerToys will now enable two types of files to be previewed: Markdown (.md) & SVG (.svg)
<br/>
<br/>

### Image Resizer

[<img align="left" src="https://aka.ms/powerToysImageResizerImageSmall" />](https://aka.ms/PowerToysOverview_ImageResizer) [Image Resizer](https://aka.ms/PowerToysOverview_ImageResizer) is a Windows Shell Extension for quickly resizing images.  With a simple right click from File Explorer, resize one or many images instantly. This code is based on [Brice Lambson's Image Resizer](https://github.com/bricelam/ImageResizer).
<br/>
<br/>
<br/>
<br/>

### Keyboard Manager

[<img align="left" src="https://aka.ms/powerToysKBMImageSmall" />](https://aka.ms/PowerToysOverview_KeyboardManager) [Keyboard Manager](https://aka.ms/PowerToysOverview_KeyboardManager) allows you to customize the keyboard to be more productive by remapping keys and creating your own keyboard shortcuts. This PowerToy requires Windows 10 1903 (build 18362) or later.
<br/>
<br/>
<br/>
<br/>

### PowerRename

[<img align="left" src="https://aka.ms/powerToysPowerRenameImageSmall" />](https://aka.ms/PowerToysOverview_PowerRename) [PowerRename](https://aka.ms/PowerToysOverview_PowerRename) is a Windows Shell Extension for advanced bulk renaming using search and replace or regular expressions. PowerRename allows simple search and replace or more advanced regular expression matching. While you type in the search and replace input fields, the preview area will show what the items will be renamed to. PowerRename then calls into the Windows Explorer file operations engine to perform the rename. This has the benefit of allowing the rename operation to be undone after PowerRename exits. This code is based on [Chris Davis's SmartRename](https://github.com/chrdavis/SmartRename).
<br/>

### PowerToys Run

[<img align="left" src="https://aka.ms/powerToysPowerLauncherImageSmall" />](https://aka.ms/PowerToysOverview_PowerToysRun) [PowerToys Run](https://aka.ms/PowerToysOverview_PowerToysRun) is a new toy in PowerToys that can help you search and launch your app instantly with a simple <kbd>Alt</kbd>+<kbd>Space</kbd> and start typing! It is open source and modular for additional plugins.  Window Walker is now inside too! This PowerToy requires Windows 10 1903 (build 18362) or later.
<br/>
<br/>
<br/>

### Shortcut Guide

[<img align="left" src="https://aka.ms/powerToysShortcutGuideImageSmall" />](https://aka.ms/PowerToysOverview_ShortcutGuide)  [Windows key shortcut guide](https://aka.ms/PowerToysOverview_ShortcutGuide) appears when a user holds the Windows key down for more than one second and shows the available shortcuts for the current state of the desktop.
<br/>
<br/>
<br/>
<br/>

### Video Conference Mute (Experimental)

[<img align="left" src="https://aka.ms/powerToysVideoConferenceImageSmall" />](https://aka.ms/PowerToysOverview_VideoConference)  [Video Conference Mute](https://aka.ms/PowerToysOverview_VideoConference) is a quick and easy way to do a global "mute" of both your microphone and webcam via <kbd>Win</kbd>+<kbd>N</kbd>. Just set your webcam in the target application to the PowerToys VideoConference camera.

**Note:** This is only included in the [pre-release version of PowerToys installer][github-prerelease-link]. This PowerToy requires Windows 10 1903 (build 18362) or later.
<br/>
<br/>
<br/>

## Installing and running Microsoft PowerToys

#### Requirements
- Windows 10 1803 (build 17134) or later.
- Have [.NET Core 3.1 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet-core/thank-you/runtime-desktop-3.1.4-windows-x64-installer). The installer should handle this but we want to directly make people aware.

#### 0.18 users for updating via notifications

- We adjusted how upgrading works in 0.20.  In 0.19 we accounted for this upcoming change but if you are going from 0.18 to 0.21, please directly use the installer file.

### Via GitHub with EXE [Recommended]

Install from the [Microsoft PowerToys GitHub releases page][github-release-link]. Click on `Assets` to show the files available in the release and then click on `PowerToysSetup-0.23.0-x64.exe` to download the PowerToys installer.

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

### September 2020 Update

Our goals for 0.23 release cycle was to focus on stability, accessibility, localization and quality of life improvements for both the development team and our end users. We have a full accessibility pass being done starting end of September to audit all of PowerToys. Our localization efforts now had data flowing both directions as well.

Our [prioritized roadmap][roadmap] of features and utilites that the core team is focusing on.

#### Highlights from September

- We shipped [v0.23][github-release-link]! (0.24 Experimental build coming shortly)

**General**

- Localization pipeline is flowing from our Github to the loc system and back.  0.25 should be localized now.
- The EXE installer should be at parity now with the MSI.  Please go to the wiki for (installer args)[https://github.com/microsoft/PowerToys/wiki/Installer-arguments-for-exe]

**FancyZones**

- Fixed bug on not seeing a newly attached screen
- Fixed spanning across monitors bug
- Added in default layout for new users, a Priority Grid
- Added keyboard support to grow / shrink to multiple zones
- General bug fixes

**PT Run**

- Multiple crash bugs fixed.  Prioritized any users reported along with top hits from Watson reporting
- Stopped PT Run from interfering with an install
- Fixed folder bug if it had a # in it (Thanks @jjw24 for the PR!)
- Fixed a screen flicker for 
- General bug fixes

**Keyboard manager**

- Multiple crash bugs fixed.  Prioritized any users reported along with top hits from Watson reporting
- Fixed multiple accessibility issues.
- General bug fixes

**Preview Pane**

- Added in Frontmatter and better (but still basic) latex support. 

**Settings**

- Fixed scaling issue for responsive design on Image Resizer
- Fixed crash on empty color value.
- Fixed crash for toggling FancyZones on/off
- Fixed 0x00 NFTS crash for settings
- Fixed multiple accessibility issues.
- Layout adjustments (Thanks @niels9001)
- General bug fixes

**Dev related**

- FxCop is being rolled out across all PowerToys. This should catch a lot of possible leaks.
- Unified PT Run's log system
- PT Run's calc plugin now has unit tests (Thanks @P-Storm)
- Dev setup install script now supports VS preview (Thanks @TobiasSekan)
- @CaelestisZ, @kameshkotwani, @adriancampos, @RahulDas782 for doc tweaks
- Thanks @Aaron-Junker, @jay-o-way and @htcfreek for helping triage!
- Thanks for everyone that filled an issue.  It really does help us prioritize

#### Experiential PowerToys utility - Video conference muting:

We'll ship the 0.24 experiential build shortly which will include all improvements from 0.23 and additional fixes.

#### Video / GIF capture functional spec for public review

Deondre Davis created our [functional spec for creating a light weight, video / GIF recording tool](https://github.com/microsoft/PowerToys/pull/6900). We encourage everyone to review it and please leave comments in the pull request so we can adjust as needed. We'll be closing it for feedback on October 12th, 2020.

This is for work [post-stabilization of current roadmap work](https://github.com/microsoft/PowerToys/wiki/Roadmap#post-stabilization) and is only the spec for what we are thinking about support.  Just want to set expectations here.

### What is being planned for 0.25

For [0.25](https://github.com/microsoft/PowerToys/issues?q=is%3Aopen+is%3Aissue+project%3Amicrosoft%2FPowerToys%2F13), we are proactively working on:

- Stability
- Localization
- Improve interactions with elevated windows and keeping most of the PT utilities non-elevated so we still have a 'shell' like experience
- OOBE work

### PowerToys roadmap

Our [prioritized roadmap][roadmap] of features and utilites that the core team is focusing on.

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
[github-prerelease-link]: https://github.com/microsoft/PowerToys/releases/tag/v0.22.0-Experimental
[roadmap]: https://github.com/microsoft/PowerToys/wiki/Roadmap
[privacyLink]: http://go.microsoft.com/fwlink/?LinkId=521839
[vidConfOverview]: https://aka.ms/PowerToysOverview_VideoConference

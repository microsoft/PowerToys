# Overview

<img align="right" width="200" src="./doc/images/Logo.jpg" />

Microsoft PowerToys is a set of utilities for power users to tune and streamline their Windows experience for greater productivity. Inspired by the [Windows 95 era PowerToys project](https://en.wikipedia.org/wiki/Microsoft_PowerToys), this reboot provides power users with ways to squeeze more efficiency out of the Windows 10 shell and customize it for individual workflows.  A great overview of the Windows 95 PowerToys can be found [here](https://socket3.wordpress.com/2016/10/22/using-windows-95-powertoys/).

## Build Status

[![Build Status](https://dev.azure.com/ms/PowerToys/_apis/build/status/microsoft.PowerToys?branchName=master)](https://dev.azure.com/ms/PowerToys/_build?definitionId=219)

## Installing and running Microsoft PowerToys 0.16
 👉 **Note:** Microsoft PowerToys requires Windows 10 1803 (build 17134) or later.

### Via Github with MSI [Recommended]

Install from the [Microsoft PowerToys GitHub releases page][github-release-link]. Click on `Assets` to show the files available in the release and then click on `PowerToysSetup-0.16.0-x64.msi` to download the PowerToys installer.

This is our preferred method.

### Other install methods

##### MSIX / Store Build Update

- We put in a lot of effort here but currently our plan of record is to make the MSI our only installer option and built-in auto-upgrade. MSIX is a great installer / container tech but there are few spots we are working with the team to improve so we can adopt.

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

### Processor support

We currently support the matrix below.

| x64 | x86 | ARM |
|:---:|:---:|:---:|
| [Supported][github-release-link] | [Issue #602](https://github.com/microsoft/PowerToys/issues/602) | [Issue #490](https://github.com/microsoft/PowerToys/issues/490) |

## Current PowerToy Utilities

### FancyZones

[FancyZones](/src/modules/fancyzones/) - FancyZones is a window manager that makes it easy to create complex window layouts and quickly position windows into those layouts.

### Shortcut

[Windows key shortcut guide](/src/modules/shortcut_guide) - The shortcut guide appears when a user holds the Windows key down for more than one second and shows the available shortcuts for the current state of the desktop.

### PowerRename

[PowerRename](/src/modules/powerrename) - PowerRename is a Windows Shell Extension for advanced bulk renaming using search and replace or regular expressions. PowerRename allows simple search and replace or more advanced regular expression matching. While you type in the search and replace input fields, the preview area will show what the items will be renamed to. PowerRename then calls into the Windows Explorer file operations engine to perform the rename. This has the benefit of allowing the rename operation to be undone after PowerRename exits.

This code is based off of [Chris Davis's SmartRename](https://github.com/chrdavis).

### File Explorer (Preview Panes)

[File Explorer](/src/modules/previewpane) add-ons right now are just limited to Preview Pane additions for File Explorer. Preview Pane is an existing feature in the File Explorer.  To enable it, you just click the View tab in the ribbon and then click "Preview Pane".

PowerToys will now enable two types of files to be previewed:

- Markdown files (.md)
- SVG (.svg)

### Image Resizer

[Image Resizer](/src/modules/imageresizer) is a Windows Shell Extension for quickly resizing images.  With a simple right click from File Explorer, resize one or many images instantly. 

This code is based off of [Brice Lambson's Image Rezier](https://github.com/bricelam).

### Window Walker (Text based alt-tab alternative)

[Window Walker](src/modules/windowwalker/) is an app that lets you search and switch between windows that you have open, all from the comfort of your keyboard. As you are searching for an app, you can use the keyboard up and down arrows to see an Alt-Tab style preview of the windows.  In the future, this will be merged into the Launcher project.  

This code is based off of [Beta Tadele's Window Walker](https://github.com/betsegaw).

### Version 1.0 plan

Our plan for all the [goals and utilities for v1.0 detailed over here in the wiki][v1].

## What's Happening

### March 2020 Update

Our mantra for the 0.16 was adding in new features along with a continual push for quality and stability.  We are working toward getting a way to auto-update PowerToys and have a good plan for this.  We want to proactively thank the community for quickly identifying a few bugs inside 0.15 and allowing us to quickly release 0.15.1 and 0.15.2.

Below are just a few of the bullet items from this release.

- We shipped [v0.16][github-release-link]!
- FancyZone improvement: 
  - Multi-Monitor improvement: Zone flipping switching now works between monitors!
  - Simplified UX: Removed layout hot-swap and flashing feature due to need to improve multi-monitor support
- New Utilities!
  - Markdown Preview pane extension
  - SVG Preview pane extension
  - Image Resizer Window Shell extension
  - Window Walker, an alt-tab alternative
- Fixed over 100 issues!
- Testing improvements
  - 54 UX Functional tests
  - 161 new Unit tests

For [0.17](https://github.com/microsoft/PowerToys/issues?q=is%3Aopen+is%3Aissue+project%3Amicrosoft%2FPowerToys%2F3), we are proactively working on:

- Auto-updating
- Win+R replacement (Launcher)
- Keyboard remapping
- Performance improvements with FancyZones
- A testing utility for FancyZones to be sure we can test different window configurations.

Future release work, we are proactively working on:

- Settings v2 / Fix bug #243

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

This project has adopted the [Microsoft Open Source Code of Conduct][oss-conduct-code]. 

## Privacy Statement

The application logs basic telemetry. Our Telemetry Data page (Coming Soon) has the trends from the telemetry. Please read the [Microsoft privacy statement][privacyLink] for more information.

[oss-CLA]: https://cla.opensource.microsoft.com
[oss-conduct-code]: CODE_OF_CONDUCT.md
[github-release-link]: https://github.com/microsoft/PowerToys/releases/
[v1]: https://github.com/microsoft/PowerToys/wiki/Version-1.0-Strategy
[privacyLink]: http://go.microsoft.com/fwlink/?LinkId=521839

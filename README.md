# Microsoft PowerToys

<img src="./doc/images/overview/PT%20hero%20image.png"/>

[Downloads & Release notes][github-release-link] | [Contributing to PowerToys](#contributing) | [What's Happening](#whats-happening) | [Roadmap](#powertoys-roadmap)

## Build status

| Architecture | Master | Stable | Installer |
|--------------|--------|--------|-----------|
| x64 | [![Build Status for Master](https://dev.azure.com/ms/PowerToys/_apis/build/status/microsoft.PowerToys?branchName=master)](https://dev.azure.com/ms/PowerToys/_build/latest?definitionId=219&branchName=master) | [![Build Status for Stable](https://dev.azure.com/ms/PowerToys/_apis/build/status/microsoft.PowerToys?branchName=stable)](https://dev.azure.com/ms/PowerToys/_build/latest?definitionId=219&branchName=stable) | [![Build Status for Installer](https://github-private.visualstudio.com/microsoft/_apis/build/status/CDPX/powertoys/powertoys-Windows-Official-master-Test?branchName=master)](https://github-private.visualstudio.com/microsoft/_build/latest?definitionId=61&branchName=master) |

## About

Microsoft PowerToys is a set of utilities for power users to tune and streamline their Windows 10 experience for greater productivity. For more info on [PowerToys overviews and guides][usingPowerToys-docs-link], or any other tools and resources for [Windows development environments](https://docs.microsoft.com/windows/dev-environment/overview), head over to [docs.microsoft.com][usingPowerToys-docs-link]! 

|              | Current utilities: |              |
|--------------|--------------------|--------------|
| [Awake](https://aka.ms/PowerToysOverview_Awake) | [Color Picker](https://aka.ms/PowerToysOverview_ColorPicker) | [FancyZones](https://aka.ms/PowerToysOverview_FancyZones) |
| [File Explorer Add-ons](https://aka.ms/PowerToysOverview_FileExplorerAddOns) | [Image Resizer](https://aka.ms/PowerToysOverview_ImageResizer) | [Keyboard Manager](https://aka.ms/PowerToysOverview_KeyboardManager) |
| [PowerRename](https://aka.ms/PowerToysOverview_PowerRename) | [PowerToys Run](https://aka.ms/PowerToysOverview_PowerToysRun) | [Shortcut Guide](https://aka.ms/PowerToysOverview_ShortcutGuide) |
| [Video Conference Mute (Experimental)](https://aka.ms/PowerToysOverview_VideoConference) |  |  |

## Installing and running Microsoft PowerToys

### Requirements

- ⚠️ PowerToys (v0.37.0 and newer) requires Windows 10 v1903 (18362) or newer.

- Have [.NET Core 3.1.15 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/thank-you/runtime-desktop-3.1.15-windows-x64-installer). The installer should handle this but we want to directly make people aware.

### Via GitHub with EXE [Recommended]

#### Stable version

Install from the [Microsoft PowerToys GitHub releases page][github-release-link]. Click on `Assets` to show the files available in the release and then click on `PowerToysSetup-0.45.0-x64.exe` to download the PowerToys installer.

This is our preferred method.

#### Experimental version
To install the Video Conference mute, please use the [v0.36 experimental version of PowerToys][github-prerelease-link] to try out this version. It includes all improvements from v0.35 in addition to the Video conference utility. Click on `Assets` to show the files available in the release and then download the .exe installer.

### Via WinGet (Preview)
Download PowerToys from [WinGet](https://github.com/microsoft/winget-cli#installing-the-client). To install PowerToys, run the following command from the command line / PowerShell:

```powershell
WinGet install powertoys
```

### Other install methods

There are [community driven install methods](./doc/unofficialInstallMethods.md) such as Chocolatey and Scoop.  If these are your preferred install solutions, this will have the install instructions.

### Processor support

We currently support the matrix below.

| x64 | x86 | ARM64 |
|:---:|:---:|:---:|
| [Supported][github-release-link] | [Issue #602](https://github.com/microsoft/PowerToys/issues/602) | [Issue #490](https://github.com/microsoft/PowerToys/issues/490) |

## Contributing

This project welcomes contributions of all types. Help spec'ing, design, documentation, finding bugs are ways everyone can help on top of coding features / bug fixes. We are excited to work with the power user community to build a set of tools for helping you get the most out of Windows.

We ask that **before you start work on a feature that you would like to contribute**, please read our [Contributor's Guide](CONTRIBUTING.md). We will be happy to work with you to figure out the best approach, provide guidance and mentorship throughout feature development, and help avoid any wasted or duplicate effort.

Most contributions require you to agree to a [Contributor License Agreement (CLA)][oss-CLA] declaring that you have the right to, and actually do, grant us the rights to use your contribution.

For guidance on developing for PowerToys, please read the [developer docs](/doc/devdocs) for a detailed breakdown. This includes how to setup your computer to compile.

## What's Happening

### PowerToys Roadmap

Our [prioritized roadmap][roadmap] of features and utilities that the core team is focusing on.

### 0.45 - August 2021 Update

Our goals for the [v0.45 release cycle](https://github.com/microsoft/PowerToys/issues?q=is%3Aopen+is%3Aissue+project%3Amicrosoft%2FPowerToys%2F23) primarily centered around stability updates and optimizations, installer updates, general bug fixes, and accessibility improvements.

We also worked extensively with the community to build an updated settings menu UI. This UI update takes advantage of the latest styling elements to provide users with a refreshed, modern experience navigating the various utilities and their settings.

An experimental version of PowerToys (v0.46) will be released the 2nd week of September and will include an updated version of Video Conference Mute.

#### Highlights from v0.45

**General**

- Settings and OOBE windows updated with Fluent UX! We hope you enjoy the new modern feel of the application menus as we align our product with upcoming Windows 11 interfaces. Special thanks to @niels9001 for driving the development of this UI, along with many thanks to the various community members who offered constant feedback and adjustments to make this a truly spectacular update!
- Added button to settings which displays version history. Thanks @niels9001!
- Signed PowerToysSetupCustomActions.dll.
- Improved auto-update experience. Thanks @niels9001!
- Aligned OOBE theme color with Settings theme color. Thanks @niels9001!
- Adjusted labeling of "Restart as Administrator" button to "Restart PowerToys as Administrator" to avoid ambiguity in meaning. Thanks @niels9001!
- Added colored icons to settings sidebar. Thanks @niels9001!
- Fixed accessibility issue in OOBE where Microsoft Docs and PowerToys release notes links could not be navigated to via keyboard. Thanks @niels9001!
- Fixed settings header alignment. Thanks @niels9001!
- Fixed text under updates section to be visible when in light mode. Thanks @niels9001!
- Updated "Learn More" text to be more descriptive. Thanks @niels9001!
- Updated "Read more" text on updates to be more descriptive. Thanks @niels9001!
- Added link to documentation in system tray. Thanks @BenConstable9!
- Fixed error caused by file in use issues when installing PowerToys.
- Fixed issue where opening settings from start menu didn't work when PowerToys was run as admin. Thanks @davidegiacometti!
 
### Awake

- Added PowerToys Awake as option in translation bug template. Thanks @Aaron-Junker!
- Adjusted description of inactive setting to improve distinguishing between the utility being disabled vs inactive. Thanks @niels9001!

### Color Picker

- Fixed bug where changing RGB values doesn't update color's HEX value. Thanks @martinchrzan!
- Fixed accessibility issue with screen reader not announcing when "Copied to Clipboard" is activated.
- Fixed accessibility issue where user could not hover the content of the info icon using a mouse. Thanks @niels9001!
- Fixed color picker format order not being accessible via keyboard. Thanks @niels9001!
- Fixed accessibility issue where screen reader announces incorrect name for "Editor color format" button and not announcing "Toggle switch" button at all. Thanks @niels9001!

### FancyZones

- Adjusted "Save and apply" editor button to adjust with text size for localizations. Thanks @niels9001!
- Fixed "Create new layout" button visibility when in high contrast mode. Thanks @niels9001!
- Fixed scaling quirks related to editor UI. Thanks @niels9001!
- Fixed editor crashing when double clicking the "edit layout" button.
- Fixed issue with editor crashing immediately after displaying zones.
- Fixed bug when navigating editor options via keyboard where pressing enter on unselected Canvas option launches Grid editor instead.
- Fixed issue where FancyZones would not restore Console Applications.
- Fixed Canvas editor and Grid editor window heights. Thanks @niels9001!
- Fixed crash due to KERNELBASE.dll.
- Fixed FancyZone icons to be smoother at higher DPI settings. Thanks @niels9001!
- Fixed crash when changing between zone layouts.
- Fixed regression where FancyZones does not resize windows on layout change.
- Adjusted layout settings to reset shortcut key after canceling changes on a particular layout.

### File Explorer add-ons

- Fixed issue where markdown files were still previewed even when "Enable Markdown" was turned off.

### Image Resizer

- Added warning that GIF files with animations may not correctly resize if the encoding used for the files is incompatible.

### Keyboard Manager

- Improved UI for KBM re-mappings list. Thanks @niels9001!

### PowerRename

- Expanding a plugin option in settings can now be toggled. Thanks @niels9001!
- Fixed race condition causing PowerRename to crash File Explorer. Thanks @ianjoneill!

### PowerToys Run

- Fixed lag caused from PowerToys running in background and invoking Alt-Tab.
- Resolved file not found exception when loading "System.Windows.Controls.Ribbon".
- Fixed null reference exception crash.
- Fixed registry plugin load crash.
- Fixed unauthorized access exception crash when setting registry keys for the utility.
- Added search for Plugin Manager. Thanks @davidegiacometti!
- Fixed VSCode workspace plugin not working. Thanks @BenConstable9!

### Video Conference Mute

- Fixed toolbar top right vertical offset to allow users to close other app windows.
- Fixed compatibility issues for certain systems when compiling from source.
- Fixed toolbox from persisting on screen.
- Fixed microphone un-muting when changing Video Conference Mute toolbar position.
- Added Video Conference Mute to OOBE.

## Community contributions

We'd like to directly mention certain contributors (in alphabetical order) for their continued community support this month and helping directly make PowerToys a better piece of software.  

[@Aaron-Junker](https://github.com/Aaron-Junker), [@BenConstable9](https://github.com/BenConstable9), [@davidegiacometti](https://github.com/davidegiacometti), [@dchristensen](https://github.com/dchristensen), [@dend](https://github.com/dend), [@franky920920](https://github.com/franky920920), [@htcfreek](https://github.com/htcfreek), [@ianjoneill](https://github.com/ianjoneill), [@jakeoeding](https://github.com/jakeoeding), [@Jay-o-Way](https://github.com/Jay-o-Way), [@jsoref](https://github.com/jsoref), [@martinchrzan](https://github.com/martinchrzan), [@niels9001](https://github.com/niels9001) and [@royvou](https://github.com/royvou)

#### What is being planned for v0.47

For [v0.47][github-next-release-work], we are planning to work on:

- Stability and bug fixes
- Upgrading PowerToys Run to .NET 5
- Preliminary UI/UX investigations to adopt WinUI and improve accessibility
- Configuring Shortcut guide to re-enable long `Win` key press to activate
- Testing PDF preview functionality for File Explorer add-ons
- Planning for new utilities and enhancements

## PowerToys Community

The PowerToys team is extremely grateful to have the [support of an amazing active community][community-link]. The work you do is incredibly important. PowerToys wouldn’t be nearly what it is today without your help filing bugs, updating documentation, guiding the design, or writing features. We want to say thank you and take time to recognize your work.

## Code of Conduct

This project has adopted the [Microsoft Open Source Code of Conduct][oss-conduct-code].

## Privacy Statement

The application logs basic telemetry. Our Telemetry Data page (Coming Soon) has the trends from the telemetry. Please read the [Microsoft privacy statement][privacy-link] for more information.

[oss-CLA]: https://cla.opensource.microsoft.com
[oss-conduct-code]: CODE_OF_CONDUCT.md
[community-link]: COMMUNITY.md
[github-release-link]: https://github.com/microsoft/PowerToys/releases/
[roadmap]: https://github.com/microsoft/PowerToys/wiki/Roadmap
[privacy-link]: http://go.microsoft.com/fwlink/?LinkId=521839
[vidConfOverview]: https://aka.ms/PowerToysOverview_VideoConference
[loc-bug]: https://github.com/microsoft/PowerToys/issues/new?assignees=&labels=&template=translation_issue.md&title=
[usingPowerToys-docs-link]: https://docs.microsoft.com/windows/powertoys/

<!-- items that need to be updated release to release -->
[github-next-release-work]: https://github.com/microsoft/PowerToys/issues?q=is%3Aopen+is%3Aissue+project%3Amicrosoft%2FPowerToys%2F21
[github-prerelease-link]: https://github.com/microsoft/PowerToys/releases/tag/v0.36.0

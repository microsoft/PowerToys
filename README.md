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

- Windows 10 v1903 (build 18362) or newer.
   - ⚠️ PowerToys (v0.37.0 and newer) requires Windows 10 v1903 (18362) or newer.
- Have [.NET Core 3.1.15 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/thank-you/runtime-desktop-3.1.15-windows-x64-installer). The installer should handle this but we want to directly make people aware.

### Via GitHub with EXE [Recommended]

#### Stable version

Install from the [Microsoft PowerToys GitHub releases page][github-release-link]. Click on `Assets` to show the files available in the release and then click on `PowerToysSetup-0.41.2-x64.exe` to download the PowerToys installer.

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

### 0.41 - June 2021 Update

Our goals for [v0.39 release cycle](https://github.com/microsoft/PowerToys/issues?q=is%3Aopen+is%3Aissue+project%3Amicrosoft%2FPowerToys%2F20) and [v0.41 release cycle](https://github.com/microsoft/PowerToys/issues?q=is%3Aopen+is%3Aissue+project%3Amicrosoft%2FPowerToys%2F21) cycle included stability updates and optimizations, general bug fixes, accessibility improvements, and supporting the integration of the new community led project, Awake, which allows Power-Users to now keep their computer awake on demand! 

#### Highlights from v0.39 / v0.41

The PowerToys team delayed our 0.39 release. We decided that we wanted to do more for the next release of PowerToys, so this longer time allowed us to get in more amazing pull requests by you, the community, to add / improve functionality which we merged into 0.41.

**General**

- New Awake utility added! Power-Users can now keep their computer awake on-demand without having to manage its power settings. A huge thank you to [@dend](https://github.com/dend) for driving the development of this feature. Check out complete guidance and getting started info on [Microsoft Docs](https://aka.ms/PowerToysOverview_Awake)
- Improved auto-update experience in PowerToys Settings
- Improved settings layout for radio button groups. Updated images and menu for OOBE. Thanks [@niels9001](https://github.com/niels9001)!
- Updated general bug report information.

### Color Picker

- New fix to prevent the creation of duplicate colors in the selection history. Thanks [@DoctorNefario](https://github.com/DoctorNefario)!
- Fixed OOBE hotkey description. Thanks [@coc0a](https://github.com/coc0a)!
- Improved editor UX to better support keyboard navigation. Thanks [@niels9001](https://github.com/niels9001)!
- Updated Color Picker GIF for OOBE. Thanks [@niels9001](https://github.com/niels9001)!

### FancyZones

- Full keyboard support added for the canvas editor’s main window and context. Thanks [@niels9001](https://github.com/niels9001)!
    - Use `Arrows` to move a zone by 10 pixels or `Ctrl + Arrows` to move the zone by 1 pixel
    - `Shift + Arrows` to resize a zone by 10 pixels (5 per edge), `Ctrl + Shift + Arrows` to resize a zone by 2 pixels (1 per edge)
    - `Ctrl + Tab` to switch between the editor and dialog
- New support for faster layout selection by double clicking a desired layout from the editor to automatically apply it and dismiss the editor.
- New zone activation behavior allows users to snap a window to the zone who's center is closest to the cursor. Thanks [@ulazy1](https://github.com/ulazy1)!
- Added process icon for FancyZones.
- Fixed issue with zoning minimized windows.
- Fixed a bunch of accessibility bugs
- Now an independent exe, detached from the runner process.

### Image Resizer

- Fixed bug where specifying a width but no height generated a 1x1 px image instead of auto-adjusting the height. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!

### PowerToys Run

- Multiple crashing bugs resolved.
- New Unit Converter plugin! Activate in the query prompt with the default activation phrase `%%`. Ex: `%% 10 ft in m`. Thanks [@jsoref](https://github.com/jsoref) and [@ThiefZero](https://github.com/ThiefZero)!
- New Windows Settings plugin! Search for specific Windows settings from PowerToys Run by utilizing the default activation phrase `$` followed by the desired setting. Ex: `$ Add/Remove Programs` To list all settings of an area category, type `:` after the category name. Ex: `$ Device:`. Thanks [@TobiasSekan](https://github.com/TobiasSekan) and [@htcfreek](https://github.com/htcfreek).
- Updated the URL plugin to enable quickly launching the default browser with  the action keyword, which defaults to `//`.
- Added remainder/modulo support for Calculator plugin via `%` operator.
- Faster launching from improved Win32 program indexing. Thanks [@royvou](https://github.com/royvou)!
- Search text results now highlight matched characters from input. Thanks [@niels9001](https://github.com/niels9001)!

### Shortcut Guide 

- Now an independent exe, detached from the runner process.
- Removed support for long `Win` press to activate Shortcut Guide. Users can now `Win + ?` to launch and new customization settings added for users to define their own shortcut.

## Community contributions

We'd like to directly mention (in alphabetical order) for their continued community support this month and helping directly make PowerToys a better piece of software.  

[@Aaron-Junker](https://github.com/Aaron-Junker), [@coc0a](https://github.com/coc0a), [@davidegiacometti](https://github.com/davidegiacometti), [@dend](https://github.com/dend), [@DoctorNefario](https://github.com/DoctorNefario), [@dogancelik](https://github.com/dogancelik), [@htcfreek](https://github.com/htcfreek), [@itsme-alan](https://github.com/itsme-alan), [@Jay-o-Way](https://github.com/Jay-o-Way), [@djsoref](https://github.com/jsoref), [@niels9001](https://github.com/niels9001), [@nitroin](https://github.com/nitroin), [@ricardosantos9521](https://github.com/ricardosantos9521), [@ThiefZero](https://github.com/ThiefZero), [@TobiasSekan](https://github.com/TobiasSekan), and [@ulazy1](https://github.com/ulazy1)

#### What is being planned for v0.43

For [v0.43][github-next-release-work], we are planning to work on:

- Stability and bug fixes
- Installer improvements

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

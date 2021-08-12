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

### 0.43 - July 2021 Update

Our goals for the [v0.43 release cycle](https://github.com/microsoft/PowerToys/issues?q=is%3Aopen+is%3Aissue+project%3Amicrosoft%2FPowerToys%2F22) primarily centered around stability updates and optimizations, installer updates, general bug fixes, and accessibility improvements.

An experimental version of PowerToys (v0.44) will be released the 2nd week of August and will include an updated version of Video Conference Mute.

#### Highlights from v0.43

**General**

- New UI for sizes list view in Image Resizer settings. Thanks @niels9001!
- Fixed FileInUse errors during install/update scenarios.
- Fixed toggle switches on PowerToys run settings to display correctly.
- Fixed header text not updating when theme color is changed. Thanks @niels9001!
- Changed tooltip text for systray icon to be on a single line for Windows 11 compatibility.
- Expanded the Report Bug tool to collect more robust diagnostic information.
- Fixed screen reader functionality to stop announcing hidden text in settings.
 
### Awake

- Fixed bug when right-clicking menu of Awake app icon. Thanks @dend!
- Fixed high CPU usage for timed keep awake. Thanks @dend!
- Fixed Awake icon spamming notification tray. Thanks @dend!
- Added telemetry to collect Awake settings and logs.

### Color Picker

- Fixed escape behavior so that only the fly-out is closed if active.
- Fixed several accessibility issues related to screen reader functionality and general usage of Color Picker.

### FancyZones

- Fixed bug causing multi-monitor spanning errors.
- Added minimum zone size limit to the settings.
- Fixed issue where re-opened windows don't appear in previously assigned zone.
- Fixed excluded apps setting to save on text change instead of when leaving focus.
- Fixed corrupt/outdated plugins load crash. 
- Fixed issue with FancyZones not working after computer goes to sleep.
- Added screen reader confirmation to canvas editor when new zones are added.

### Keyboard Manager

- Fixed screen reader usage bugs to increase intuitiveness.

### PowerToys Run

- Fixed crashing bug due to missing image file app.dark.png.
- Fixed URI plugin bug with handling numeric input. Thanks @davidegiacometti!
- Improved launch performance of PowerToys run on first call. Thanks @davidegiacometti!
- Changed URI plugin to launch HTTPS by default instead of HTTP. Thanks @chrisharris333!
- Added confirmation dialog when system commands are executed from PowerToys Run. Thanks @chrisharris333!

### Video Conference Mute

- Fixed toolbar top right vertical offset to allow users to close other app windows.
- Fixed compatibility issues for certain systems when compiling from source.

## Community contributions

We'd like to directly mention certain contributors (in alphabetical order) for their continued community support this month and helping directly make PowerToys a better piece of software.  

[@Aaron-Junker](https://github.com/Aaron-Junker), [@chrisharris333](https://github.com/chrisharris333), [@davidegiacometti](https://github.com/davidegiacometti), [@dend](https://github.com/dend), [@franky920920](https://github.com/franky920920) [@htcfreek](https://github.com/htcfreek), [@Jay-o-Way](https://github.com/Jay-o-Way), [@jsoref](https://github.com/jsoref), [@niels9001](https://github.com/niels9001), [@royvou](https://github.com/royvou), and [@tony-xia](https://github.com/tony-xia)

#### What is being planned for v0.45

For [v0.45][github-next-release-work], we are planning to work on:

- Stability and bug fixes
- Installer improvements
- Upgrading PowerToys Run to .NET 5
- Preliminary UI/UX investigations to adopt WinUI and improve accessibility

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

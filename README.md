# Welcome to the Microsoft PowerToys repo

<img src="./doc/images/overview/PT%20hero%20image.png"/>

[Downloads & Release notes][github-release-link] | [Contributing to PowerToys](#contributing) | [What's Happening](#whats-happening) | [Roadmap](#powertoys-roadmap) |  [Known issues](#known-issues)

## Build status

| Architecture | Master | Stable | Installer |
|--------------|--------|--------|-----------|
| x64 | [![Build Status for Master](https://dev.azure.com/ms/PowerToys/_apis/build/status/microsoft.PowerToys?branchName=master)](https://dev.azure.com/ms/PowerToys/_build/latest?definitionId=219&branchName=master) | [![Build Status for Stable](https://dev.azure.com/ms/PowerToys/_apis/build/status/microsoft.PowerToys?branchName=stable)](https://dev.azure.com/ms/PowerToys/_build/latest?definitionId=219&branchName=stable) | [![Build Status for Installer](https://github-private.visualstudio.com/microsoft/_apis/build/status/CDPX/powertoys/powertoys-Windows-Official-master-Test?branchName=master)](https://github-private.visualstudio.com/microsoft/_build/latest?definitionId=61&branchName=master) |

## About

Microsoft PowerToys is a set of utilities for power users to tune and streamline their Windows 10 experience for greater productivity. For more info on [PowerToys overviews and guides][usingPowerToys-docs-link], or any other tools and resources for [Windows development environments](https://docs.microsoft.com/windows/dev-environment/overview), head over to [docs.microsoft.com][usingPowerToys-docs-link]! 

|   | Current utilities: |   |
|--------------|--------|--------|
| [Color Picker](https://aka.ms/PowerToysOverview_ColorPicker) |  [FancyZones](https://aka.ms/PowerToysOverview_FancyZones) | [File Explorer Add-ons](https://aka.ms/PowerToysOverview_FileExplorerAddOns) |
| [Image Resizer](https://aka.ms/PowerToysOverview_ImageResizer) | [Keyboard Manager](https://aka.ms/PowerToysOverview_KeyboardManager) | [PowerRename](https://aka.ms/PowerToysOverview_PowerRename) |
| [PowerToys Run](https://aka.ms/PowerToysOverview_PowerToysRun) | [Shortcut Guide](https://aka.ms/PowerToysOverview_ShortcutGuide) | [Video Conference Mute (Experimental)](https://aka.ms/PowerToysOverview_VideoConference) |

## Installing and running Microsoft PowerToys

### Requirements

- Windows 10 v1903 (build 18362) or better preferred, Windows 10 v1803 (build 17134) minimum.  
- Have [.NET Core 3.1 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet-core/thank-you/runtime-desktop-3.1.10-windows-x64-installer). The installer should handle this but we want to directly make people aware.

### Via GitHub with EXE [Recommended]

#### Stable version

Install from the [Microsoft PowerToys GitHub releases page][github-release-link]. Click on `Assets` to show the files available in the release and then click on `PowerToysSetup-0.29.3-x64.exe` to download the PowerToys installer.

This is our preferred method.

#### Experimental version
To install the Video Conference mute, please use the [v0.28 pre-release experimental version of PowerToys][github-prerelease-link] to try out this version. It includes all improvements from v0.27 in addition to the Video conference utility. Click on `Assets` to show the files available in the release and then download the .exe installer.

### Via WinGet (Preview)
Download PowerToys from [WinGet](https://github.com/microsoft/winget-cli/releases). To install PowerToys, run the following command from the command line / PowerShell:

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

### 0.29 - December 2020 Update

Our goals for [v0.29 release cycle][github-release-link] were to focus on adding on end-user experience, stability, accessibility, localization and quality of life improvements for both the development team and our end users. Due to the short dev cycle due to the holidays this month, larger work items will show up next release such as FZ editor improvements and three new plug-ins for PowerToys Run (service, regkey, system commands).

Our [prioritized roadmap][roadmap] of features and utilities that the core team is focusing on for the near future. We fixed a lot of localization issues from our initial release but we may not still be perfect. If you find an issue, please file a [localization bug][loc-bug].

#### Highlights from v0.29

**General**
- Bug report tool and improved logging.
- Various localization improvements. 
- CodeQL added.  Triggered via a cron timer twice a day.
- "How to use" docs moved to https://docs.microsoft.com/windows/powertoys/
   - This will allow the community to do direct PRs against those documents

**ARM64 Progress**
- .NET Core upgrade for code bases the PowerToys team controls is complete.  We still have two external dependencies that are .NET Framework that need to be updated.

**Color Picker** 
- General bug fixes
- Added ability to provide the name of the color at parity with Office and WinUI Color Picker.

**FancyZones**
- Allows to use Windows Snap on desktops that don't have a layout applied and for apps that are in the excluded list.
- Bug fixes

**PowerToys Run**
- Improved performance
- PT Run now supports accented characters.

**Installer**
   - Option to extract the MSI from the .exe for enterprise scenarios and more options to do unattended installations.
   - Removed toast notifications during installation.

We'd like to directly mention (in alphabetical order) for their continued community support this month and helping directly make PowerToys a better piece of software.  

[@Aaron-Junker](https://github.com/Aaron-Junker), 
[@benjamhooper](https://github.com/benjamhooper), 
[@davidegiacometti](https://github.com/davidegiacometti), 
[@eriawan](https://github.com/eriawan), 
[@htcfreek](https://github.com/htcfreek), 
[@jay-o-way](https://github.com/jay-o-way), 
[@jhutchings1](https://github.com/jhutchings1), 
[@jsoref](https://github.com/jsoref), 
[@martinchrzan](https://github.com/martinchrzan), 
[@niels9001](https://github.com/niels9001), 
[@riverar](https://github.com/riverar), 
[@snickler](https://github.com/snickler), 
and 
[@TobiasSekan](https://github.com/TobiasSekan) 

#### What is being planned for v0.31 - January 2021

For [v0.31][github-next-release-work], we are proactively working on:

- Stability
- ARM64 work
- Video conference mute investigation toward a DirectShow filter versus a driver
- OOBE work

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
[github-next-release-work]: https://github.com/microsoft/PowerToys/issues?q=is%3Aopen+is%3Aissue+project%3Amicrosoft%2FPowerToys%2F16
[github-prerelease-link]: https://github.com/microsoft/PowerToys/releases/tag/v0.28.0

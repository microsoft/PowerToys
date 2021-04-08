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
| [Color Picker](https://aka.ms/PowerToysOverview_ColorPicker) |  [FancyZones](https://aka.ms/PowerToysOverview_FancyZones) | [File Explorer Add-ons](https://aka.ms/PowerToysOverview_FileExplorerAddOns) |
| [Image Resizer](https://aka.ms/PowerToysOverview_ImageResizer) | [Keyboard Manager](https://aka.ms/PowerToysOverview_KeyboardManager) | [PowerRename](https://aka.ms/PowerToysOverview_PowerRename) |
| [PowerToys Run](https://aka.ms/PowerToysOverview_PowerToysRun) | [Shortcut Guide](https://aka.ms/PowerToysOverview_ShortcutGuide) | [Video Conference Mute (Experimental)](https://aka.ms/PowerToysOverview_VideoConference) |

## Installing and running Microsoft PowerToys

### Requirements

- Windows 10 v1903 (build 18362) or newer preferred, Windows 10 v1803 (build 17134) minimum.
   - ⚠️ PowerToys minimum version of Windows 10 will be increased to v1903 starting with the 0.37 release
- Have [.NET Core 3.1.13 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet-core/thank-you/runtime-desktop-3.1.13-windows-x64-installer). The installer should handle this but we want to directly make people aware.

### Via GitHub with EXE [Recommended]

#### Stable version

Install from the [Microsoft PowerToys GitHub releases page][github-release-link]. Click on `Assets` to show the files available in the release and then click on `PowerToysSetup-0.33.1-x64.exe` to download the PowerToys installer.

This is our preferred method.

#### Experimental version
To install the Video Conference mute, please use the [v0.28 pre-release experimental version of PowerToys][github-prerelease-link] to try out this version. It includes all improvements from v0.27 in addition to the Video conference utility. Click on `Assets` to show the files available in the release and then download the .exe installer.

We hope to have an updated version in February 2021 with the new DirectShow driver.

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

### 0.35 - March 2021 Update

Our goals for the [v0.35 release cycle][github-release-link] were to add in new functionality to support quick swapping layouts for FancyZones, wrap up work for the DirectShow migration for Video Conference Mute so we can migrate into the main dev branch as well as fixing bugs. The 0.36 experimental release will happen the week of April 5th toward the end of the week pending testing. Throughout these efforts, we continue working towards stability across all PowerToys utilities. 

Our [prioritized roadmap][roadmap] of features and utilities will dictate what the core team is focusing on for the near future. 

#### Highlights from v0.35 Stable/0.36 Experimental 

**General**
- PowerToys will start requiring Windows 10 v1903 or greater after 0.35.x release.  The v1 settings, which supports older Windows versions, will be removed in 0.37.  
   - Note: We may be able to bring back support when we migrate to WinUI3 but as of now, we will be increasing the minimum version of Windows to 1903 or greater.
- Localization corrections
- Improved GitHub report bug template.
- Increased .NET Core to 3.1.13
- Fixed installer 'run as user' regression

**Color Picker**
- UX adjustments to editor. Thanks [@niels9001](https://github.com/niels9001)!
- `Esc` can now be used to exit the editor. Thanks [@BenConstable9](https://github.com/BenConstable9)!

**FancyZones**
- Added hotkeys and quick swap functionality for custom layouts! Users can now assign a hotkey in the editor and use it to quickly set a desktop's zones with `Ctrl + Win + Alt + NUMBER` key binding, or by pressing the hotkey while dragging a window.
- UX updates. Thanks [@niels9001](https://github.com/niels9001)!
- Fixed zone placement algorithm for when the Taskbar is vertical
- Bug fixes

**PowerToys Run**
- Users can specify where to show the launcher window. Thanks [@addrum](https://github.com/addrum)!
- New plugin added to support opening previously used Visual Studio Code workspaces, remote machines (SSH or Codespaces), and containers! When enabled, use `{` to query for available workspaces. Thanks [@ricardosantos9521](https://github.com/ricardosantos9521)!  Please note, this plugin is off by default.
- Shell history now saves the raw command instead of the resolved command. A command like `%appdata%` would now save in the Shell history as is instead of `C:\Users\YourUserName\AppData\Roaming`. Thanks [@mayitbeegh](https://github.com/mayitbeegh)!
- Better logging to try to track down some bugs
- Bug fixes

**Video Conference Mute (Experimental)**
- Tracking work remaining at issue [#7944](https://github.com/microsoft/PowerToys/issues/7944)
- Goal is to have 0.36 experimental release week of April 5th (Yes, we've stated this before, we know)

**Contributor workflow**
- Main project now has a vsconfig which will prompt you to install needed items versus having to use a script.  This will aid in keeping you up-to-date when something changes.
- Updated spell checker.  Thanks [@jsoref](https://github.com/jsoref)!

#### Community contributions

We'd like to directly mention (in alphabetical order) for their continued community support this month and helping directly make PowerToys a better piece of software.  

[@Aaron-Junker](https://github.com/Aaron-Junker), 
[@addrum](https://github.com/addrum), 
[@BenConstable9](https://github.com/BenConstable9), 
[@htcfreek](https://github.com/htcfreek), 
[@Jay-o-Way](https://github.com/Jay-o-Way), 
[@jsoref](https://github.com/jsoref),
[@mayitbeegh](https://github.com/mayitbeegh),
[@niels9001](https://github.com/niels9001),
[@pc-v2](https://github.com/pc-v2),
and
[@ricardosantos9521](https://github.com/ricardosantos9521)

#### What is being planned for v0.37 - April 2021

For [v0.37][github-next-release-work], we are planning to work on:

- Stability and bug fixes
- Adding VCM to the stable release
- Removing v1 Settings / PT minimum version will become Windows 10 v1903
- Post-update guidance prompt work

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
[github-next-release-work]: https://github.com/microsoft/PowerToys/issues?q=is%3Aopen+is%3Aissue+project%3Amicrosoft%2FPowerToys%2F19
[github-prerelease-link]: https://github.com/microsoft/PowerToys/releases/tag/v0.28.0

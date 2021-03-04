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

- Windows 10 v1903 (build 18362) or better preferred, Windows 10 v1803 (build 17134) minimum.  
- Have [.NET Core 3.1 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet-core/thank-you/runtime-desktop-3.1.11-windows-x64-installer). The installer should handle this but we want to directly make people aware.

### Via GitHub with EXE [Recommended]

#### Stable version

Install from the [Microsoft PowerToys GitHub releases page][github-release-link]. Click on `Assets` to show the files available in the release and then click on `PowerToysSetup-0.33.1-x64.exe` to download the PowerToys installer.

This is our preferred method.

#### Experimental version
To install the Video Conference mute, please use the [v0.28 pre-release experimental version of PowerToys][github-prerelease-link] to try out this version. It includes all improvements from v0.27 in addition to the Video conference utility. Click on `Assets` to show the files available in the release and then download the .exe installer.

We hope to have an updated version in February 2021 with the new DirectShow driver.

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

### 0.33 - February 2021 Update

Our goals for [v0.33 release cycle][github-release-link] was to add in some critical new functionality into the new user experience as well as a plug-in manager for PowerToys Run.  In addition, we feel we are near ready to add in Video Conference mute into the stable release pending feedback from the pending 0.34 experimental release.  The 0.34 experimental release will happen week of March 8th toward the end of the week pending testing.

Our [prioritized roadmap][roadmap] of features and utilities that the core team is focusing on for the near future. We fixed a lot of localization issues from our initial release but we may not still be perfect. If you find an issue, please file a [localization bug][loc-bug].

#### Highlights from v0.33 Stable/0.34 Experimental 

**General**
- Updated overview links to be language agnostic to the docs site.
- 'First time load' experience.  The hope is a quick, light way to learn about basic functionality.  We have some more work to do and want to also use the same framework for teaching about updates as well.
- Localization corrections

**FancyZones**
- Adjusted editor UX based on feedback.  Thanks [@niels9001](https://github.com/niels9001)!
- New options to change zone activation algorithm.

**File Explorer**
- Improved how SVG images are previewed in the preview pane, thanks[@Drakula44](https://github.com/Drakula44)!
- [@Aaron-Junker](Aaron-Junker) has created a proof of concept for using [Monaco editor](https://github.com/microsoft/monaco-editor) for previewing dev files.  This will enable over 125+ file types.

**PowerToys Run**
- Plugin Manager now is in settings.  You can directly turn on / off, include items in general search, and change the action key!  Thanks [@htcfreek](https://github.com/htcfreek) for the great feedback!
- Improved support for additional window managers by abstracting out shell process calls. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
- Fix for PT Run registering the hotkey on non-supported OS versions.
- `~` will now act as the user home directory in Folder plugin.  Thanks [@davidegiacometti](https://github.com/davidegiacometti)
- Service plugin has adjusted status messages

**Video Conference Mute (Experimental)**
- Adjust video muting to leverage DirectShow.
- Goal is to have 0.34 experimental release week of March 8th.

**Settings**
- When restarting as admin, the settings now will reopen. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!

**ARM64 Progress**
- Investigation on how we'll accomplish Settings with the XAML Island and WPF app.

#### Community contributions

We'd like to directly mention (in alphabetical order) for their continued community support this month and helping directly make PowerToys a better piece of software.  

[@Aaron-Junker](https://github.com/Aaron-Junker), 
[@davidegiacometti](https://github.com/davidegiacometti), 
[@Drakula44](https://github.com/Drakula44), 
[@htcfreek](https://github.com/htcfreek),
[@Jay-o-Way](https://github.com/Jay-o-Way),
[@niels9001](https://github.com/niels9001),
and 
[@notDevagya](https://github.com/notDevagya)

#### What is being planned for v0.35 - March 2021

For [v0.35][github-next-release-work], we are planning to work on:

- Stability and bug fixes
- FZ Editor hotkey layout swap support
- Integrating VCM in main release 
- Start process for removal support for old settings system and migrating our minimum OS version to Windows 10 1903.

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
[github-next-release-work]: https://github.com/microsoft/PowerToys/issues?q=is%3Aopen+is%3Aissue+project%3Amicrosoft%2FPowerToys%2F18
[github-prerelease-link]: https://github.com/microsoft/PowerToys/releases/tag/v0.28.0

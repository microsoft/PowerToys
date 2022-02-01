# Microsoft PowerToys

![Hero image for Microsoft PowerToys](doc/images/overview/PT_hero_image.png)

[How to use PowerToys][usingPowerToys-docs-link] | [Downloads & Release notes][github-release-link] | [Contributing to PowerToys](#contributing) | [What's Happening](#whats-happening) | [Roadmap](#powertoys-roadmap)

## Build status

| Architecture | Solution (Main) | Solution (Stable) | Installer (Main) |
|--------------|-----------------|-------------------|------------------|
| x64 | [![Build Status for Main](https://dev.azure.com/ms/PowerToys/_apis/build/status/microsoft.PowerToys?branchName=main)](https://dev.azure.com/ms/PowerToys/_build/latest?definitionId=219&branchName=main) | [![Build Status for Stable](https://dev.azure.com/ms/PowerToys/_apis/build/status/microsoft.PowerToys?branchName=stable)](https://dev.azure.com/ms/PowerToys/_build/latest?definitionId=219&branchName=stable) | [![Build Status Installer pipeline](https://dev.azure.com/microsoft/Dart/_apis/build/status/microsoft.PowerToys?branchName=main)](https://dev.azure.com/microsoft/Dart/_build/latest?definitionId=76541&branchName=main) |
| ARM64 | Currently investigating | [Issue #490](https://github.com/microsoft/PowerToys/issues/490) |  |

## About

Microsoft PowerToys is a set of utilities for power users to tune and streamline their Windows experience for greater productivity. For more info on [PowerToys overviews and how to use the utilities][usingPowerToys-docs-link], or any other tools and resources for [Windows development environments](https://docs.microsoft.com/windows/dev-environment/overview), head over to [docs.microsoft.com][usingPowerToys-docs-link]! 

|              | Current utilities: |              |
|--------------|--------------------|--------------|
| [Always on Top](https://aka.ms/PowerToysOverview_AoT) | [PowerToys Awake](https://aka.ms/PowerToysOverview_Awake) | [Color Picker](https://aka.ms/PowerToysOverview_ColorPicker) |
| [FancyZones](https://aka.ms/PowerToysOverview_FancyZones) | [File Explorer Add-ons](https://aka.ms/PowerToysOverview_FileExplorerAddOns) | [Image Resizer](https://aka.ms/PowerToysOverview_ImageResizer) |
| [Keyboard Manager](https://aka.ms/PowerToysOverview_KeyboardManager) | [Mouse utilities](https://aka.ms/PowerToysOverview_MouseUtilities) | [PowerRename](https://aka.ms/PowerToysOverview_PowerRename) |
| [PowerToys Run](https://aka.ms/PowerToysOverview_PowerToysRun) | [Shortcut Guide](https://aka.ms/PowerToysOverview_ShortcutGuide) | [Video Conference Mute](https://aka.ms/PowerToysOverview_VideoConference) |

## Installing and running Microsoft PowerToys

### Requirements

- Windows 11 or Windows 10 v1903 (18362) or newer.
- Our installer will install the following items:
   - [.NET Core 3.1.22 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/thank-you/runtime-desktop-3.1.22-windows-x64-installer) or a newer 3.1.x runtime. This is needed currently for the Settings application.
   - [.NET 5.0.13 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/thank-you/runtime-desktop-5.0.13-windows-x64-installer) or a newer 5.0.x runtime. 
   - [Microsoft Edge WebView2 Runtime](https://go.microsoft.com/fwlink/p/?LinkId=2124703) bootstrapper. This will install the latest version. 

### Via GitHub with EXE [Recommended]

 [Microsoft PowerToys GitHub releases page][github-release-link], click on `Assets` at the bottom to show the files available in the release and then click on `PowerToysSetup-0.55.0-x64.exe` to download the PowerToys installer.

This is our preferred method.

### Via Microsoft Store

Install from the [Microsoft Store's PowerToys page][microsoft-store-link]. You must be using the [new Microsoft Store](https://blogs.windows.com/windowsExperience/2021/06/24/building-a-new-open-microsoft-store-on-windows-11/) which will be available for both Windows 11 and Windows 10.

### Via WinGet (Preview)
Download PowerToys from [WinGet][winget-link]. To install PowerToys, run the following command from the command line / PowerShell:

```powershell
winget install Microsoft.PowerToys -s winget
```

### Other install methods

There are [community driven install methods](./doc/unofficialInstallMethods.md) such as Chocolatey and Scoop.  If these are your preferred install solutions, this will have the install instructions.

## Contributing

This project welcomes contributions of all types. Help spec'ing, design, documentation, finding bugs are ways everyone can help on top of coding features / bug fixes. We are excited to work with the power user community to build a set of tools for helping you get the most out of Windows.

We ask that **before you start work on a feature that you would like to contribute**, please read our [Contributor's Guide](CONTRIBUTING.md). We will be happy to work with you to figure out the best approach, provide guidance and mentorship throughout feature development, and help avoid any wasted or duplicate effort.

Most contributions require you to agree to a [Contributor License Agreement (CLA)][oss-CLA] declaring that you have the right to, and actually do, grant us the rights to use your contribution.

For guidance on developing for PowerToys, please read the [developer docs](/doc/devdocs) for a detailed breakdown. This includes how to setup your computer to compile.

## What's Happening

### PowerToys Roadmap

Our [prioritized roadmap][roadmap] of features and utilities that the core team is focusing on.

### 0.55 - January 2022 Update

In this release, we are continuing our progress toward getting PowerToys ARM64 ready, fix some top issues and new utilities.  Work from last month helped us enable us to upgrade the code base to .NET 5 and next month onward to .NET 6. This will provide stability and speed improvements.

We're also extremely excited to bring on 3 new PowerToy utilities.

- File Explorer add-on: Developer files for preview pane. This should add about 150 file extensions total. We are using the [Monaco Editor](https://github.com/Microsoft/monaco-editor) to power this experience.  Thanks [@aaron-junker](https://github.com/aaron-junker)!
- File Explorer add-on: STL file format thumbnail generation!  Since STL is a common 3D file format, this allows a quick visual check. Thanks [@pedrolamas](https://github.com/pedrolamas)!  Preview pane support is already in Windows.
- Mouse Utility: Crosshair over pointer via <kbd>Ctrl</kbd>+<kbd>Alt</kbd>+<kbd>P</kbd>. This feature was co-developed with the accessibility team at Microsoft. When the team told us about the idea and described trying to find your cursor by looking through a straw, we knew we could leverage code from the other mouse utilities to quickly enable this feature. Below is a quote from one of the testers with a rough validation build:

> "This will change my life and allow me to use any PC without constantly losing the pointer. This is huge! I will be able to work at my normal speed again. It is a total game changer for people with visual field impairments!" – Joanna A.

#### Community
- We would love to directly say THANK YOU. Filing issues and feature requests takes time and we greatly appreciate it. You help us quickly diagnose, spot trends, and prioritize. We love when people fix bugs and develop new PowerToys every little bit does really help.
- [@edwinzap](https://github.com/edwinzap) really helped us validate translation issues when our localization system was in transition.
- [@bdoserror](https://github.com/bdoserror) quickly pointed out a release note error

#### General
- .NET runtime is now on 5, our next release will be upgraded to .NET 6. Moving to .NET 5 and then 6 helped reduce our moving parts in a single release so we went this route.  Why this is important is this is one of the major work items needed for ARM64 support. In addition, this should help provide a speed boosts once we are on .NET 6.
- [@jsoref's](https://github.com/jsoref) spelling plugin help

#### Always on Top
- Fixed one of two borders showing incorrectly bugs.
- Border defaults to OS accent color now. Thanks [@davidegiacometti](https://github.com/davidegiacometti)
- Reduced CPU / GPU activity. Not done improving, we know we can do better.

#### FancyZones 
- Bug fixed to not lose zones after update
- Fixed editor margin issue for Chinese language.  Thanks [@niels9001](https://github.com/niels9001)

#### File explorer add-ons
- GCode thumbnails now have transparency. Thanks [@pedrolamas](https://github.com/pedrolamas)
- New Utility - Developer files for File Explorer preview pane. This should add about 150 file extensions total. We are using the [Monaco Editor](https://github.com/Microsoft/monaco-editor) to power this experience.  Thanks [@aaron-junker](https://github.com/aaron-junker)!
- New Utility - STL thumbnails added! Preview pane support is already in Windows. Thanks [@pedrolamas](https://github.com/pedrolamas)!

#### Image Resizer
- Fixed bug with too much meta data.  Thanks [@CleanCodeDeveloper](https://github.com/CleanCodeDeveloper)
- Fixed bug resizing bug for constant height while maintaining aspect ratio.  Thanks [@CleanCodeDeveloper](https://github.com/CleanCodeDeveloper)

#### Mouse utilities
- New Utility - Crosshair over pointer via <kbd>Ctrl</kbd>+<kbd>Alt</kbd>+<kbd>P</kbd>. This feature was co-developed with the accessibility team at Microsoft. Thanks [@niels9001](https://github.com/niels9001) for helping with the icon!

#### PowerRename
- Files are sorted now how File Explorer sorts.

#### PowerToys Run
- Improved speed and fixed bugs with Window walker plugin.  Thanks [@htcfreek](https://github.com/htcfreek)
- Window Walker will now show path of elevated apps.  Thanks [@davidegiacometti](https://github.com/davidegiacometti)
- Added UEFI command to system commands. Thanks [@htcfreek](https://github.com/htcfreek)
- Fixed crashing bug in EnvironmentHelper class.  Thanks [@htcfreek](https://github.com/htcfreek)
- Fix URI plugin bug with `^:`.  Thanks [@franky920920](https://github.com/franky920920)
- VS Code plugin not showing workspaces with latest Code version was corrected.  Thanks [@ricardosantos9521](https://github.com/ricardosantos9521)
- Fixed bug that caused plugins to not load. Thanks [@davidegiacometti](https://github.com/davidegiacometti)
- Fixed crash in Uri plugin and Web search plugin. Thanks [@cyberrex5](https://github.com/cyberrex5)!

#### Settings
- Fixed a regression with settings being reset when moving from admin to non-admin

#### Video Conference Mute
- Fixed crashing bug with Zoom and other clients. We found someone we could remotely debug with and identify the actual crashing part.
- Change of behavior: When leaving a meeting, VCM will now leave your microphone in the state it was.  This mimics behavior of applications if VCM was not present.
- Change of behavior: When you exit PowerToys, your current microphone state will remain.

#### Community contributions

We'd like to directly mention certain contributors (in alphabetical order) for their continued community support this month and helping directly make PowerToys a better piece of software. 
[@Aaron-Junker](https://github.com/Aaron-Junker), [@bdoserror](https://github.com/bdoserror), [@CleanCodeDeveloper](https://github.com/CleanCodeDeveloper), [@cyberrex5](https://github.com/cyberrex5), [@davidegiacometti](https://github.com/davidegiacometti), [@edwinzap](https://github.com/edwinzap), [@franky920920](https://github.com/franky920920),  [@jay-o-way](https://github.com/jay-o-way), [@jsoref](https://github.com/jsoref), [@niels9001](https://github.com/niels9001), and [@ricardosantos9521](https://github.com/ricardosantos9521)


#### What is being planned for v0.56

For [v0.56][github-next-release-work], we plan on finishing up the .NET upgrade path to 6.  This will require development to migrate to Visual Studio 2022. We are also shifting back to a continuous version number system versus Odd for main and Even for experimental releases.

- .NET 6 upgrade to all available surfaces
- A Dialog on update making you aware of what has changed. 
- 'Shake to activate' find my mouse
- PowerToy Run plugin improvements

## PowerToys Community

The PowerToys team is extremely grateful to have the [support of an amazing active community][community-link]. The work you do is incredibly important. PowerToys wouldn’t be nearly what it is today without your help filing bugs, updating documentation, guiding the design, or writing features. We want to say thank you and take time to recognize your work.

## Code of Conduct

This project has adopted the [Microsoft Open Source Code of Conduct][oss-conduct-code].

## Privacy Statement

The application logs basic telemetry. Our Telemetry Data page (Coming Soon) has the trends from the telemetry. Please read the [Microsoft privacy statement][privacy-link] for more information.

[oss-CLA]: https://cla.opensource.microsoft.com
[oss-conduct-code]: CODE_OF_CONDUCT.md
[community-link]: COMMUNITY.md
[github-release-link]: https://aka.ms/installPowerToys
[microsoft-store-link]: https://aka.ms/getPowertoys
[winget-link]: https://github.com/microsoft/winget-cli#installing-the-client
[roadmap]: https://github.com/microsoft/PowerToys/wiki/Roadmap
[privacy-link]: http://go.microsoft.com/fwlink/?LinkId=521839
[vidConfOverview]: https://aka.ms/PowerToysOverview_VideoConference
[loc-bug]: https://github.com/microsoft/PowerToys/issues/new?assignees=&labels=&template=translation_issue.md&title=
[usingPowerToys-docs-link]: https://aka.ms/powertoys-docs

<!-- items that need to be updated release to release -->
[github-next-release-work]: https://github.com/microsoft/PowerToys/issues?q=is%3Aopen+is%3Aissue+project%3Amicrosoft%2FPowerToys%2F29
[github-current-release-work]: https://github.com/microsoft/PowerToys/issues?q=is%3Aopen+is%3Aissue+project%3Amicrosoft%2FPowerToys%2F28

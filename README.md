# Microsoft PowerToys

![Hero image for Microsoft PowerToys](doc/images/overview/PT_hero_image.png)

[How to use PowerToys][usingPowerToys-docs-link] | [Downloads & Release notes][github-release-link] | [Contributing to PowerToys](#contributing) | [What's Happening](#whats-happening) | [Roadmap](#powertoys-roadmap)

## Build status

| Architecture | Solution (Main) | Solution (Stable) | Installer (Main) |
|--------------|-----------------|-------------------|------------------|
| x64 | [![Build Status for Main](https://dev.azure.com/ms/PowerToys/_apis/build/status/microsoft.PowerToys?branchName=main&jobName=Build%20x64%20Release)](https://dev.azure.com/ms/PowerToys/_build/latest?definitionId=219&branchName=main&jobName=Build%20x64%20Release) | [![Build Status for Stable](https://dev.azure.com/ms/PowerToys/_apis/build/status/microsoft.PowerToys?branchName=stable&jobName=Build%20x64%20Release)](https://dev.azure.com/ms/PowerToys/_build/latest?definitionId=219&branchName=stable) | [![Build Status Installer pipeline](https://dev.azure.com/microsoft/Dart/_apis/build/status/PowerToys/PowerToys%20Signed%20YAML%20Release%20Build?branchName=main&jobName=Build&configuration=Build%20Release_x64)](https://dev.azure.com/microsoft/Dart/_build/latest?definitionId=76541&branchName=main) |
| ARM64 | [![Build Status for Main](https://dev.azure.com/ms/PowerToys/_apis/build/status/microsoft.PowerToys?branchName=main&jobName=Build%20arm64%20Release)](https://dev.azure.com/ms/PowerToys/_build/latest?definitionId=219&branchName=main) | [![Build Status for Main](https://dev.azure.com/ms/PowerToys/_apis/build/status/microsoft.PowerToys?branchName=main&jobName=Build%20arm64%20Release)](https://dev.azure.com/ms/PowerToys/_build/latest?definitionId=219&branchName=stable) | [![Build Status Installer pipeline](https://dev.azure.com/microsoft/Dart/_apis/build/status/PowerToys/PowerToys%20Signed%20YAML%20Release%20Build?branchName=main&jobName=Build&configuration=Build%20Release_arm64)](https://dev.azure.com/microsoft/Dart/_build/latest?definitionId=76541&branchName=main) |

## About

Microsoft PowerToys is a set of utilities for power users to tune and streamline their Windows experience for greater productivity. For more info on [PowerToys overviews and how to use the utilities][usingPowerToys-docs-link], or any other tools and resources for [Windows development environments](https://learn.microsoft.com/windows/dev-environment/overview), head over to [learn.microsoft.com][usingPowerToys-docs-link]! 

|              | Current utilities: |              |
|--------------|--------------------|--------------|
| [Always on Top](https://aka.ms/PowerToysOverview_AoT) | [PowerToys Awake](https://aka.ms/PowerToysOverview_Awake) | [Color Picker](https://aka.ms/PowerToysOverview_ColorPicker) |
| [FancyZones](https://aka.ms/PowerToysOverview_FancyZones) | [File Explorer Add-ons](https://aka.ms/PowerToysOverview_FileExplorerAddOns) | [Image Resizer](https://aka.ms/PowerToysOverview_ImageResizer) |
| [Keyboard Manager](https://aka.ms/PowerToysOverview_KeyboardManager) | [Mouse utilities](https://aka.ms/PowerToysOverview_MouseUtilities) | [PowerRename](https://aka.ms/PowerToysOverview_PowerRename) |
| [PowerToys Run](https://aka.ms/PowerToysOverview_PowerToysRun) | [Quick Accent](https://aka.ms/PowerToysOverview_QuickAccent) | [Screen Ruler](https://aka.ms/PowerToysOverview_ScreenRuler) |
| [Shortcut Guide](https://aka.ms/PowerToysOverview_ShortcutGuide) | [Text Extractor](https://aka.ms/PowerToysOverview_TextExtractor) | [Video Conference Mute](https://aka.ms/PowerToysOverview_VideoConference) |

## Installing and running Microsoft PowerToys

### Requirements

- Windows 11 or Windows 10 version 2004 (code name 20H1 / build number 19041) or newer.
- Our installer will install the following items:
   - [.NET 6.0.9 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/6.0#runtime-desktop-6.0.9) or a newer 6.0.x runtime.
   - [Microsoft Edge WebView2 Runtime](https://go.microsoft.com/fwlink/p/?LinkId=2124703) bootstrapper. This will install the latest version.

### Via GitHub with EXE [Recommended]

 [Microsoft PowerToys GitHub releases page][github-release-link], click on `Assets` at the bottom to show the files available in the release. Please use the appropriate the PowerToys installer that matches your machine's architecture.  For most people, it is `x64`.
 
 - **For x64 processors (most common):** [PowerToysSetup-0.63.0-x64.exe](https://github.com/microsoft/PowerToys/releases/download/v0.63.0/PowerToysSetup-0.63.0-x64.exe)
 - **For ARM64 processors:** [PowerToysSetup-0.63.0-arm64.exe](https://github.com/microsoft/PowerToys/releases/download/v0.63.0/PowerToysSetup-0.63.0-arm64.exe)

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

### 0.63 - September 2022 Update

In this release, we focused on stability and improvement.

**Highlights**

- QuickAccent contains a new setting to select a language. This should reduce the number of accented characters a user needs to pick from. Thanks [@damienleroy](https://github.com/damienleroy)!
- Reduced installer file size (83 MB for 0.63.0 compared to 125 MB for 0.62.1) and drive storage use (587 MB for 0.63.0 compared to 817 MB for 0.62.1) by sharing the Windows App SDK, VC++ redistributable and PowerToys Interop runtime files between utilities. This is a step towards removing the UAC requirement on install. The next step is shipping .NET self-contained and shared between utilities.

### Known issues

- The Text Extractor utility [fails to recognize text in some cases on ARM64 devices running Windows 10](https://github.com/microsoft/PowerToys/issues/20278).
- After installing PowerToys, [the new Windows 11 context menu entries for PowerRename and Image Resizer might not appear before a system restart](https://github.com/microsoft/PowerToys/issues/19124).
- There are reports of users who are [unable to open the Settings window](https://github.com/microsoft/PowerToys/issues/18015). This is being caused by incompatibilities with some applications (RTSS RivaTuner Statistics Server and MSI AfterBurner are known examples of this). If you're affected by this, please check the  linked issue to verify if any of the presented solutions works for you.

### General

- Fixed an issue that caused bug report generation to fail. (This was a hotfix for 0.62)
- Updated the Windows App SDK runtimes to 1.1.5.

### Always on Top

- Fixed an issue causing the border to linger when moving a window between virtual desktops.
- The minimum thickness for the borders is now 1. Thanks [@unuing](https://github.com/unuing)!
- Borders were showing in Virtual Desktop thumbnails. These were removed.
- Corrected the borders visuals to more closely follow the application borders.

### Awake

- Fixed utility exit logic to close all threads and avoid crashes. (This was a hotfix for 0.62)

### Color Picker

- Fixed utility exit logic to close all threads and avoid crashes. (This was a hotfix for 0.62)
- Fixed initialization error that caused the mouse position to be incorrectly set.

### FancyZones

- Fixed FancyZones Editor exit logic to close all threads and avoid crashes. (This was a hotfix for 0.62)

### File explorer add-ons

- Updated the WebView 2 dependency to 1.0.1343.22. Thanks [@Aaron-Junker](https://github.com/Aaron-Junker)!
- Fixed preview of .reg files.

### Image Resizer

- Fixed a bug causing File Explorer to crash under some conditions when accessing the context menu.

### PowerToys Run

- Added support to opening Terminal windows in quake mode. Thanks [@FWest98](https://github.com/FWest98)!
- Fixed utility exit logic to close all threads and avoid crashes. (This was a hotfix for 0.62)
- Improve the icon shown in the Program plugin for application execution aliases. Thanks [@MikeBarker-MSFT](https://github.com/MikeBarker-MSFT)!
- Fix calls to the default browser when Firefox is installed from the Microsoft Store.
- Fixed accessibility issue in which controls appended to the result entries weren't announced.
- Search was improved and should now return results where the terms in the query appear at the end of the result.

### Quick Accent

- Improved the keyboard hooks performance. (This was a hotfix for 0.62)
- Fixed a bug that was causing Quick Accent to interfere with Keyboard Manager. (This was a hotfix for 0.62)
- Added the correct ß uppercase character. Thanks [@Aaron-Junker](https://github.com/Aaron-Junker)!
- Accent character selection should now wrap around. Thanks [@wmentha](https://github.com/wmentha)!
- Added language selection setting to reduce the number of accented characters shown. The available accented character sets are Currency, Czech, Dutch, French, Hungarian, Icelandic, Italian, Maori, Pinyin, Polish, Romanian, Slovakian, Spanish and Turkish. Thanks [@damienleroy](https://github.com/damienleroy)!

### Screen Ruler

- Improved UI/UX and settings descriptions.
- Fixed utility exit logic to close all threads and avoid crashes. (This was a hotfix for 0.62)

### Settings

- UI icons updated. Thanks [@Jay-o-Way](https://github.com/Jay-o-Way)!
- Descriptions improvement and disambiguation. Thanks [@Jay-o-Way](https://github.com/Jay-o-Way)!
- Fixed checkbox margins and other design tweaks. Thanks [@Jay-o-Way](https://github.com/Jay-o-Way)!

### Text Extractor

- Removed extra spaces when recognizing Chinese, Japanese or Korean languages. Thanks [@TheJoeFin](https://github.com/TheJoeFin)!
- Fixed utility exit logic to close all threads and avoid crashes. (This was a hotfix for 0.62)
- Fixed an issue where a selection would start on right-click.

### Installer

- Added logic to exit PowerToys on upgrade before trying to update .NET.
- Updated the .NET dependency to 6.0.9.
- Added clearer installation step names for the bootstrapper. Thanks [@htcfreek](https://github.com/htcfreek) and [@Jay-o-Way](https://github.com/Jay-o-Way)!
- Windows App SDK, VC++ redistributable and PowerToys Interop runtime files are now shared between utilities through hardlinks, reducing installation size.

### Documentation

- Fixed typos in Keyboard Manager documentation. Thanks [@eltociear](https://github.com/eltociear)!
- Replaced docs.microsoft.com links with learn.microsoft.com. Thanks [@Aaron-Junker](https://github.com/Aaron-Junker)!

### Development

- Fixed a build error that was restricting developers to switch between configuration without first cleaning local build files.
- C++ exception catches were corrected to be caught by reference to avoid unnecessary copy operations. Thanks [@NN---](https://github.com/NN---)!
- General C# code clean up, format fixing and removal of unused code analysis suppressions.
- Removed unnecessary `muxc` prefix from XAML files. Thanks [@Jay-o-Way](https://github.com/Jay-o-Way)!
- Re-enabled tests on our pipeline that depend on WebView2.
- Windows 11 tier 1 context menu packages now contain the "Microsoft.PowerToys" prefix.

#### What is being planned for v0.64

For [v0.64][github-next-release-work], we'll work on below:

- Environment Variables Editor PowerToy
- GPO policies for PowerToys
- Utility to list which processes are using a file
- Ship .NET self contained and shared between utilities
- Hosts file editor, contributed by [@davidegiacometti](https://github.com/davidegiacometti). Thank you!
- Settings backup and restore, contributed by [@jefflord](https://github.com/jefflord). Thank you!
- Stability / bug fixes

## PowerToys Community

The PowerToys team is extremely grateful to have the [support of an amazing active community][community-link]. The work you do is incredibly important. PowerToys wouldn’t be nearly what it is today without your help filing bugs, updating documentation, guiding the design, or writing features. We want to say thank you and take time to recognize your work.  Month over month, you directly help make PowerToys a better piece of software.

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
[github-next-release-work]: https://github.com/microsoft/PowerToys/issues?q=is%3Aopen+is%3Aissue+project%3Amicrosoft%2FPowerToys%2F37
[github-current-release-work]: https://github.com/microsoft/PowerToys/issues?q=is%3Aopen+is%3Aissue+project%3Amicrosoft%2FPowerToys%2F36

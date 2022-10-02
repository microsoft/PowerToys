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
   - [.NET 6.0.8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/6.0#runtime-desktop-6.0.8) or a newer 6.0.x runtime.
   - [Microsoft Edge WebView2 Runtime](https://go.microsoft.com/fwlink/p/?LinkId=2124703) bootstrapper. This will install the latest version.
   - [Microsoft Visual C++ Redistributable](https://learn.microsoft.com/cpp/windows/latest-supported-vc-redist?view=msvc-170#visual-studio-2015-2017-2019-and-2022) installer. This will install one of the latest versions available.

### Via GitHub with EXE [Recommended]

 [Microsoft PowerToys GitHub releases page][github-release-link], click on `Assets` at the bottom to show the files available in the release. Please use the appropriate the PowerToys installer that matches your machine's architecture.  For most people, it is `x64`.
 
 - **For x64 processors (most common):** [PowerToysSetup-0.62.0-x64.exe](https://github.com/microsoft/PowerToys/releases/download/v0.62.0/PowerToysSetup-0.62.0-x64.exe)
 - **For ARM64 processors:** [PowerToysSetup-0.62.0-arm64.exe](https://github.com/microsoft/PowerToys/releases/download/v0.62.0/PowerToysSetup-0.62.0-arm64.exe)

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

### 0.62 - August 2022 Update

In this release, we focused on releasing three new PowerToys.

**Highlights**

- New utility: Screen Ruler is a quick and easy way to measure pixels on your screen.
- New utility: Quick Accent is an easy way to write letters with accents. Thanks [@damienleroy](https://github.com/damienleroy)!
- New utility: Text Extractor works like Snipping Tool, but copies the text out of the selected region using OCR and puts it on the clipboard. Thanks [@TheJoeFin](https://github.com/TheJoeFin)!
- PowerToy Run ships with a new Plugin letting you search in past query results. Thanks [@jefflord](https://github.com/jefflord)!

### Known issues
- The Text Extractor utility [fails to recognize text in some cases on ARM64 devices running Windows 10](https://github.com/microsoft/PowerToys/issues/20278).
- After installing PowerToys, [the new Windows 11 context menu entries for PowerRename and Image Resizer might not appear before a system restart](https://github.com/microsoft/PowerToys/issues/19124).
- There are reports of users who are [unable to open the Settings window](https://github.com/microsoft/PowerToys/issues/18015). This is being caused by incompatibilities with some applications (RTSS RivaTuner Statistics Server and MSI AfterBurner are known examples of this). If you're affected by this, please check the  linked issue to verify if any of the presented solutions works for you.

### General

- Added a new utility: Screen Ruler.
- Added a new utility: Quick Accent. Thanks [@damienleroy](https://github.com/damienleroy)!
- Added a new utility: Text Extractor. Thanks [@TheJoeFin](https://github.com/TheJoeFin)!
- Upgraded the Windows App SDK runtimes to 1.1.4.

### Always on Top

- Fixed a bug causing the border to linger when closing an Outlook popup window.

### Color Picker

- Fixed the HSB color format to correctly track HSV instead of HSL.
- Fixed an issue where the zoom factor wasn't reset when reopening the zoom window. Thanks [@FWest98](https://github.com/FWest98)!

### FancyZones

- Removed the button to open Settings from the FancyZones Editor, as it was opening behind the overlay.
- Changed the Highlight distance control to a slider in the FancyZones Editor, to address accessibility issues with screen readers.
- Fixed an issue where the FancyZones Editor would duplicate or edit the wrong layout.
- Fixed an issue that caused canvas layout width/height to be changed without even opening the layout in FancyZones Editor.

### File explorer add-ons

- Quality of life improvements to Developer Files preview, including a progress bar while loading, performance improvements, an improved dark mode, and logs. Thanks [@Aaron-Junker](https://github.com/Aaron-Junker)!
- Fixed possible WebView related vulnerabilities in the SVG and Markdown handlers.
- Fixed some race conditions in Developer Files preview causing the loading bar to hang.
- Added localization support to the Developer Files preview messages.
- It's now possible to configure default color for Stl Thumbnails. Thanks [@pedrolamas](https://github.com/pedrolamas)!
- Added an option to format JSON and XML files before rendering. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!

### PowerRename

- Fixed an issue that was generating a silent crash when the context menu was triggered when not selecting any file or folder. (This was a hotfix for 0.61)
- Improved performance when loading a big number of files.
- Fixed a specific case in which PowerRename tried to rename a file to an empty string.
- The UI now shows when a file can't be renamed due to its name being too long or containing invalid characters.

### PowerToys Run

- Added a fix to the VSCodeWorkspaces plugin to better support portable installations. Thanks [@bkmeneguello](https://github.com/bkmeneguello)!
- The Folder plugin now expands `%HOMEPATH%` correctly.
- Fixed a case where a previous result was being activated when searching for new results. Added a setting to better control input throttling. Thanks [@jefflord](https://github.com/jefflord)!
- Added support for port numbers in the URI plugin. Thanks [@KohGeek](https://github.com/KohGeek)!
- Fixed query errors when the search delay option was turned off.
- New History plugin to search for old search results. Thanks [@jefflord](https://github.com/jefflord)!
- Changed the default TimeDate activation keyword to `)`, as queries starting by `(` are expected as Calculator global queries, and added information in Settings so users know that some activation keywords may conflict with normal usage of some plugins when trying to do a global query. Thanks [@htcfreek](https://github.com/htcfreek)!
- The Unit Converter plugin updated its UnitsNet dependency and now supports plural units. Thanks [@FWest98](https://github.com/FWest98)!
- Improved the validation logic in the Calculator plugin. Thanks [@htcfreek](https://github.com/htcfreek)!

### Runner

- Improved: Clean up old install folders and logs at startup. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!

### Settings

- Image and phrasing adjustments.
- Icon and image updates for the new utilities. Thanks [@niels9001](https://github.com/niels9001)!

### Shortcut Guide

- Fixed the Narrator shortcut to include the newly added Control key.

### Installer

- Fixed a regression that was causing the PowerToys shortcut to be deleted on update. (This was a hotfix for 0.61)
- Updated the .NET dependency to 6.0.8.

### Documentation

- Fixed wrong links to installers in README. Thanks [@unuing](https://github.com/unuing)!

### Development

- Removed FXCop leftovers. Thanks [@CleanCodeDeveloper](https://github.com/CleanCodeDeveloper)!
- Added version number to missing binaries and added a CI script to verify that all binaries have their version numbers set correctly.
- Updated a dependency to fix building on Visual Studio 17.3 C++ tools.
- Fixed and reactivated the CI unit tests for FancyZones.
- Cleaned up and removed dead code from PowerRename code base.
- Added a script for verifying the solution targets match the expected CPU architectures. Thanks [@snickler](https://github.com/snickler)!
- Obsolete package Castle.Core was removed. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
- Language typos were corrected across the PowerToys assets. Thanks [@pea-sys](https://github.com/pea-sys), [@eltociear](https://github.com/eltociear) and [@obairka](https://github.com/obairka)!

#### What is being planned for v0.63

For [v0.63][github-next-release-work], we'll work on below:

- Environment Variables Editor PowerToy
- GPO policies for PowerToys
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
[github-next-release-work]: https://github.com/microsoft/PowerToys/issues?q=is%3Aopen+is%3Aissue+project%3Amicrosoft%2FPowerToys%2F36
[github-current-release-work]: https://github.com/microsoft/PowerToys/issues?q=is%3Aopen+is%3Aissue+project%3Amicrosoft%2FPowerToys%2F35

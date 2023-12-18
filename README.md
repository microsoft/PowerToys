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
| [Crop And Lock](https://aka.ms/PowerToysOverview_CropAndLock) | [Environment Variables](https://aka.ms/PowerToysOverview_EnvironmentVariables) | [FancyZones](https://aka.ms/PowerToysOverview_FancyZones) |
| [File Explorer Add-ons](https://aka.ms/PowerToysOverview_FileExplorerAddOns) | [File Locksmith](https://aka.ms/PowerToysOverview_FileLocksmith) | [Hosts File Editor](https://aka.ms/PowerToysOverview_HostsFileEditor) |
| [Image Resizer](https://aka.ms/PowerToysOverview_ImageResizer) | [Keyboard Manager](https://aka.ms/PowerToysOverview_KeyboardManager) | [Mouse utilities](https://aka.ms/PowerToysOverview_MouseUtilities) |
| [Mouse Without Borders](https://aka.ms/PowerToysOverview_MouseWithoutBorders) | [Peek](https://aka.ms/PowerToysOverview_Peek) | [Paste as Plain Text](https://aka.ms/PowerToysOverview_PastePlain) |
| [PowerRename](https://aka.ms/PowerToysOverview_PowerRename) | [PowerToys Run](https://aka.ms/PowerToysOverview_PowerToysRun) | [Quick Accent](https://aka.ms/PowerToysOverview_QuickAccent) |
| [Registry Preview](https://aka.ms/PowerToysOverview_RegistryPreview) | [Screen Ruler](https://aka.ms/PowerToysOverview_ScreenRuler) | [Shortcut Guide](https://aka.ms/PowerToysOverview_ShortcutGuide) |
| [Text Extractor](https://aka.ms/PowerToysOverview_TextExtractor) | [Video Conference Mute](https://aka.ms/PowerToysOverview_VideoConference) |

## Installing and running Microsoft PowerToys

### Requirements

- Windows 11 or Windows 10 version 2004 (code name 20H1 / build number 19041) or newer.
- x64 or ARM64 processor
- Our installer will install the following items:
   - [Microsoft Edge WebView2 Runtime](https://go.microsoft.com/fwlink/p/?LinkId=2124703) bootstrapper. This will install the latest version.

### Via GitHub with EXE [Recommended]

Go to the [Microsoft PowerToys GitHub releases page][github-release-link] and click on `Assets` at the bottom to show the files available in the release. Please use the appropriate PowerToys installer that matches your machine's architecture and install scope. For most, it is `x64` and per-user.

<!-- items that need to be updated release to release -->
[github-next-release-work]: https://github.com/microsoft/PowerToys/issues?q=project%3Amicrosoft%2FPowerToys%2F50
[github-current-release-work]: https://github.com/microsoft/PowerToys/issues?q=project%3Amicrosoft%2FPowerToys%2F49
[ptUserX64]: https://github.com/microsoft/PowerToys/releases/download/v0.76.2/PowerToysUserSetup-0.76.2-x64.exe
[ptUserArm64]: https://github.com/microsoft/PowerToys/releases/download/v0.76.2/PowerToysUserSetup-0.76.2-arm64.exe
[ptMachineX64]: https://github.com/microsoft/PowerToys/releases/download/v0.76.2/PowerToysSetup-0.76.2-x64.exe
[ptMachineArm64]: https://github.com/microsoft/PowerToys/releases/download/v0.76.2/PowerToysSetup-0.76.2-arm64.exe
 
|  Description   | Filename | sha256 hash |
|----------------|----------|-------------|
| Per user - x64       | [PowerToysUserSetup-0.76.2-x64.exe][ptUserX64] | 73D734FC34B3F9D7484081EC0F0B6ACD4789A55203A185904CC5C62ABD02AF16 |
| Per user - ARM64     | [PowerToysUserSetup-0.76.2-arm64.exe][ptUserArm64] | 5284CC5DA399DC37858A2FD260C30F20C484BA1B5616D0EB67CD90A8A286CB8B |
| Machine wide - x64   | [PowerToysSetup-0.76.2-x64.exe][ptMachineX64] | 72B87381C9E5C7447FB59D7CE478B3102C9CEE3C6EB3A6BC7EC7EC7D9EFAB2A0 |
| Machine wide - ARM64 | [PowerToysSetup-0.76.2-arm64.exe][ptMachineArm64] | F28C7DA377C25309775AB052B2B19A299C26C41582D05B95C3492A4A8C952BFE |

This is our preferred method.

### Via Microsoft Store

Install from the [Microsoft Store's PowerToys page][microsoft-store-link]. You must be using the [new Microsoft Store](https://blogs.windows.com/windowsExperience/2021/06/24/building-a-new-open-microsoft-store-on-windows-11/) which is available for both Windows 11 and Windows 10.

### Via WinGet
Download PowerToys from [WinGet][winget-link]. Updating PowerToys via winget will respect current PowerToys installation scope. To install PowerToys, run the following command from the command line / PowerShell:

#### User scope installer [default]
```powershell
winget install Microsoft.PowerToys -s winget
```

#### Machine-wide scope installer

```powershell
winget install --scope machine Microsoft.PowerToys -s winget
```

### Other install methods

There are [community driven install methods](./doc/unofficialInstallMethods.md) such as Chocolatey and Scoop. If these are your preferred install solutions, you can find the install instructions there.

## Third-Party Run Plugins

There is a collection of [third-party plugins](./doc/thirdPartyRunPlugins.md) created by the community that aren't distributed with PowerToys.

## Contributing

This project welcomes contributions of all types.  Besides  coding features / bug fixes,  other ways to assist include spec writing, design, documentation, and finding bugs. We are excited to work with the power user community to build a set of tools for helping you get the most out of Windows.

We ask that **before you start work on a feature that you would like to contribute**, please read our [Contributor's Guide](CONTRIBUTING.md). We would be happy to work with you to figure out the best approach, provide guidance and mentorship throughout feature development, and help avoid any wasted or duplicate effort.

Most contributions require you to agree to a [Contributor License Agreement (CLA)][oss-CLA] declaring that you grant us the rights to use your contribution and that you have permission to do so.

For guidance on developing for PowerToys, please read the [developer docs](/doc/devdocs) for a detailed breakdown. This includes how to setup your computer to compile.

## What's Happening

### PowerToys Roadmap

Our [prioritized roadmap][roadmap] of features and utilities that the core team is focusing on.

### 0.76 - November 2023 Update

In this release, we focused on new features, stability and improvements.

**Highlights**

 - Upgrade to .NET 8. Thanks [@snickler](https://github.com/snickler)!
 - Keyboard Manager can now remap keys and shortcuts to send sequences of unicode text.
 - Modernized the Keyboard Manager Editor UI. Thanks [@dillydylann](https://github.com/dillydylann)!
 - Modernized the PowerToys Run, Quick Accent and Text Extractor UIs. Thanks [@niels9001](https://github.com/niels9001)!
 - New File Explorer Add-ons: QOI image Preview Handler and Thumbnail Provider. Thanks [@pedrolamas](https://github.com/pedrolamas)!

### General
 - Updated the WebView 2 dependency to 1.0.2088.41. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Fixed unreadable color brushes used across WinUI3 applications for improved accessibility. Thanks [@niels9001](https://github.com/niels9001)!
 - Flyouts used across WinUI3 applications are no longer constrained to the application's bounds. Thanks [@Jay-o-Way](https://github.com/Jay-o-Way)!
 - Upgraded the WPF-UI dependency to preview.9 and then preview.11. Thanks [@niels9001](https://github.com/niels9001) and [@pomianowski](https://github.com/pomianowski)!
 - Upgraded to .NET 8. Thanks [@snickler](https://github.com/snickler)!
 - Updated the WinAppSDK dependency to 1.4.3.

### Awake

 - Added localization to the tray icon context menu.

### Crop And Lock

 - Fixed restoring windows that were reparented while maximized.

### Environment Variables

 - Fixed crash caused by WinAppSDK version bump by replacing ListView elements with ItemsControl.

### FancyZones

 - Reverted a change that caused some applications, like the Windows Calculator, to not snap correctly. (This was a hotfix for 0.75)
 - FancyZones Editor will no longer apply a layout to the current monitor after editing it.
 - Fixed and refactored the code that detected if a window can be snapped. Added tests to it with known application window styles to avoid regressions in the future.

### File Explorer add-ons

 - Solved an issue incorrectly detecting encoding when previewing code files preview.
 - Fixed the background color for Gcode preview handler on dark theme. Thanks [@pedrolamas](https://github.com/pedrolamas)!
 - New utilities: Preview Handler and Thumbnail Provider for QOI image files. Thanks [@pedrolamas](https://github.com/pedrolamas)!
 - GCode Thumbnails are now in the 32 bit ARGB format. Thanks [@pedrolamas](https://github.com/pedrolamas)!
 - Added the perceived type to SVG and QOI file thumbnails. Thanks [@pedrolamas](https://github.com/pedrolamas)!

### GPO

 - Added the missing Environment Variables utility policy to the .admx and .adml files. (This was a hotfix for 0.75)
 - Fixed some typos and text improvements in the .adml file. Thanks [@htcfreek](https://github.com/htcfreek)!

### Hosts File Editor

 - Added a proper warning when the hosts file is read-only and a button to make it writable. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!

### Image Resizer
 
 - Fixed a WPF-UI issue regarding the application's background brushes. Thanks [@niels9001](https://github.com/niels9001)!

### Installer

 - Included the Text Extractor and Awake localization files in the install process.

### Keyboard Manager

 - Modernized the UI with the Fluent design. Thanks [@dillydylann](https://github.com/dillydylann)!
 - Added the feature to remap keys and shortcuts to arbitrary unicode text sequences.

### Mouse Without Borders

 - Removed Thread.Suspend calls when exiting the utility. That call is deprecated, unneeded and was causing a silent crash.

### Peek

 - Added the possibility to pause/resume videos with the space bar. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Fixed high CPU usage when idle before initializing the main window. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Implemented Ctrl+W as a shortcut to close Peek. Thanks [@Physalis2](https://github.com/Physalis2)!
 - Solved an issue incorrectly detecting encoding when previewing code files.
 - Fixed background issues when peeking into HTML files after the WebView 2 upgrade.

### PowerToys Run

 - Moved to WPF-UI and redesigned according to Fluent UX principles. Thanks [@niels9001](https://github.com/niels9001)!
 - Fixed an issue causing 3rd party plugins to not have their custom settings correctly initialized with default values. (This was a hotfix for 0.75) Thanks [@waaverecords](https://github.com/waaverecords)!
 - Fixed a crash in the VSCode plugin when the VSCode path had trailing backspaces. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Fixed a crash when trying to load invalid image icons.
 - Fixed a crash in the Programs plugin when getting images for some .lnk files.
 - Fixed a rare startup initialization error and removed cold start operations that were no longer needed. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Improved calculations for Windows File Time and Unix Epoch Time in the DateTime plugin. Thanks [@htcfreek](https://github.com/htcfreek)!
 - Fixed a crash when trying to get the icon for a link that pointed to no file.
 - Cleaned up code in the WindowWalker plugin improving the logic. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!

### Quick Accent

 - Moved from ModernWPF to WPF-UI. Thanks [@niels9001](https://github.com/niels9001)!
 - Added support to the Finnish language character set. Thanks [@davidtlascelles](https://github.com/davidtlascelles)!
 - Added currency symbols for Croatian, Gaeilge, Gàidhlig and Welsh. Thanks [@PesBandi](https://github.com/PesBandi)!
 - Added a missing Latin letter ꝡ. Thanks [@cubedhuang](https://github.com/cubedhuang)!
 - Added fraction characters. Thanks [@PesBandi](https://github.com/PesBandi)!
 - Added support to the Danish language character set. Thanks [@PesBandi](https://github.com/PesBandi)!
 - Added the Kazakhstani Tenge character to the Currencies characters set. Thanks [@PesBandi](https://github.com/PesBandi)!
 - Renamed Slovakian to Slovak, which is the correct term. Thanks [@PesBandi](https://github.com/PesBandi)!
 - Added the Greek language character set. Thanks [@mcbabo](https://github.com/mcbabo)!

### Settings

 - When clicking a module's name on the Dashboard, it will navigate to that module's page.
 - Fixed the clipping of information in the Backup and Restore section of the General Settings page. Thanks [@niels9001](https://github.com/niels9001)!
 - Updated the File Explorer Add-ons fluent icon. Thanks [@niels9001](https://github.com/niels9001)!
 - Added a warning when trying to set a shortcut that might conflict with "Alt Gr" key combinations.
 - Added a direct link to the OOBE's "What's New page" from the main Settings window. Thanks [@iakrayna](https://github.com/iakrayna)!
 - Changed mentions from Microsoft Docs to Microsoft Learn.
 - Fixed the slow reaction to system theme changes.

### Text Extractor

 - Move to WPF-UI, localization and light theme support. Thanks [@niels9001](https://github.com/niels9001)!
 - Disabled by default on Windows 11, with a information box on Settings to prefer using the Windows Snipping Tool, which now supports OCR.

### Documentation

 - Fixed some typos in the README. Thanks [@Asymtode712](https://github.com/Asymtode712)!
 - Reworked the gpo docs on learn.microsoft.com, adding .admx, registry and Intune information. Thanks [@htcfreek](https://github.com/htcfreek)!

### Development

 - Updated the check-spelling ci action to 0.22. Thanks [@jsoref](https://github.com/jsoref)!
 - Refactored the modules data model used between the Settings Dashboard and Flyout.
 - Fixed a flaky interop test that was causing automated CI to hang occasionally.
 - Increased the WebView 2 loading timeout to reduce flakiness in those tests. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Added support for building with the Dev Drive CopyOnWrite feature, increasing build speed. Thanks [@pedrolamas](https://github.com/pedrolamas)!
 - Addressed the C# static analyzers suggestions. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Addressed the C++ static analyzers suggestions.
 - PRs that only contain Markdown or text files changes no longer trigger the full CI. Thanks [@snickler](https://github.com/snickler)!
 - Updated the Microsoft.Windows.CsWinRT to 2.0.4 to fix building with the official Visual Studio 17.8 release.
 - Fixed new code quality issues caught by the official Visual Studio 17.8 release.
 - Added a bot trigger to point contributors to the main new contribution issue on GitHub. Thanks [@Aaron-Junker](https://github.com/Aaron-Junker)!
 - Removed unneeded entries from expect.txt.
 - Turned off a new feature from Visual Studio that was adding the commit hash to the binary files Product Version.
 - Refactored and reviewed the spellcheck entries into different files. Thanks [@Jay-o-Way](https://github.com/Jay-o-Way)!
 - Added Spectre mitigation and SHA256 hash creation for some DLLs.
 - Reverted the release pipeline template to a previous release that's stable for shipping PowerToys.

#### What is being planned for version 0.77

For [v0.77][github-next-release-work], we'll work on the items below:

 - New utility: Command Not Found
 - Language selection
 - Automated UI testing through WinAppDriver
 - Develop support for Desired State Configuration
 - Modernize and refresh the UX of PowerToys based on WPF. Here's the Work in Progress preview for "Color Picker":

![ColorPicker UI refresh WIP](https://github.com/microsoft/PowerToys/assets/9866362/ceebe54b-de63-4ce7-afcb-2cd4280bf4d1)

 - Stability / bug fixes

## PowerToys Community

The PowerToys team is extremely grateful to have the [support of an amazing active community][community-link]. The work you do is incredibly important. PowerToys wouldn’t be nearly what it is today without your help filing bugs, updating documentation, guiding the design, or writing features. We want to say thank you and take time to recognize your work.  Month by month, you directly help make PowerToys a better piece of software.

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

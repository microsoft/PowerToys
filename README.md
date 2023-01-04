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
| [FancyZones](https://aka.ms/PowerToysOverview_FancyZones) | [File Explorer Add-ons](https://aka.ms/PowerToysOverview_FileExplorerAddOns) | [File Locksmith](https://aka.ms/PowerToysOverview_FileLocksmith) |
| [Hosts File Editor](https://aka.ms/PowerToysOverview_HostsFileEditor) | [Image Resizer](https://aka.ms/PowerToysOverview_ImageResizer) | [Keyboard Manager](https://aka.ms/PowerToysOverview_KeyboardManager) |
| [Mouse utilities](https://aka.ms/PowerToysOverview_MouseUtilities) | [PowerRename](https://aka.ms/PowerToysOverview_PowerRename) | [PowerToys Run](https://aka.ms/PowerToysOverview_PowerToysRun) |
| [Quick Accent](https://aka.ms/PowerToysOverview_QuickAccent) | [Screen Ruler](https://aka.ms/PowerToysOverview_ScreenRuler) | [Shortcut Guide](https://aka.ms/PowerToysOverview_ShortcutGuide) |
| [Text Extractor](https://aka.ms/PowerToysOverview_TextExtractor) | [Video Conference Mute](https://aka.ms/PowerToysOverview_VideoConference) |

## Installing and running Microsoft PowerToys

### Requirements

- Windows 11 or Windows 10 version 2004 (code name 20H1 / build number 19041) or newer.
- Our installer will install the following items:
   - [Microsoft Edge WebView2 Runtime](https://go.microsoft.com/fwlink/p/?LinkId=2124703) bootstrapper. This will install the latest version.

### Via GitHub with EXE [Recommended]

Go to [Microsoft PowerToys GitHub releases page][github-release-link], click on `Assets` at the bottom to show the files available in the release. Please use the appropriate the PowerToys installer that matches your machine's architecture. For most, it is `x64`.
 
 - **For x64 processors (most common):** [PowerToysSetup-0.66.0-x64.exe](https://github.com/microsoft/PowerToys/releases/download/v0.66.0/PowerToysSetup-0.66.0-x64.exe)
 - **For ARM64 processors:** [PowerToysSetup-0.66.0-arm64.exe](https://github.com/microsoft/PowerToys/releases/download/v0.66.0/PowerToysSetup-0.66.0-arm64.exe)

This is our preferred method.

### Via Microsoft Store

Install from the [Microsoft Store's PowerToys page][microsoft-store-link]. You must be using the [new Microsoft Store](https://blogs.windows.com/windowsExperience/2021/06/24/building-a-new-open-microsoft-store-on-windows-11/) which will be available for both Windows 11 and Windows 10.

### Via WinGet (Preview)
Download PowerToys from [WinGet][winget-link]. To install PowerToys, run the following command from the command line / PowerShell:

```powershell
winget install Microsoft.PowerToys -s winget
```

### Other install methods

There are [community driven install methods](./doc/unofficialInstallMethods.md) such as Chocolatey and Scoop. If these are your preferred install solutions, this will have the install instructions.

## Contributing

This project welcomes contributions of all types. Help spec'ing, design, documentation, finding bugs are ways everyone can help on top of coding features / bug fixes. We are excited to work with the power user community to build a set of tools for helping you get the most out of Windows.

We ask that **before you start work on a feature that you would like to contribute**, please read our [Contributor's Guide](CONTRIBUTING.md). We will be happy to work with you to figure out the best approach, provide guidance and mentorship throughout feature development, and help avoid any wasted or duplicate effort.

Most contributions require you to agree to a [Contributor License Agreement (CLA)][oss-CLA] declaring that you have the right to, and actually do, grant us the rights to use your contribution.

For guidance on developing for PowerToys, please read the [developer docs](/doc/devdocs) for a detailed breakdown. This includes how to setup your computer to compile.

## What's Happening

### PowerToys Roadmap

Our [prioritized roadmap][roadmap] of features and utilities that the core team is focusing on.

### 0.66 - December 2022 Update

In this release, we focused on stability and improvements.

**Highlights**

- PowerToy utilities now ship with self-contained .NET 7, meaning it's not necessary to install .NET as part of the installer and it's easier to keep up to date.
- It's possible to pick which of the installed OCR languages is used by Text Extractor by selecting it in the right-click context menu.
- Added a setting to sort the order of the accented characters by usage frequency in Quick Accent.

### General

- Reduced resource consumption caused by logging. A thread for each logger was being created even for disabled utilities.
- The .NET 7 dependency is now shipped self-contained within the utilities, using deep links to reduce storage space usage.

### Color Picker

- Fixed an issue where the custom color formats were not working when picking colors without using the editor.
- Fixed a crash when using duplicated names for color formats.
- Added two decimal formats, to distinguish between RGB and BGR.
- Fixed color name localization, which was not working correctly on 0.65.

### FancyZones

- Fixed an editor crash caused by deleting a zone while trying to move it.
- Reduce the time it takes the tooltip for layout shortcut setting to appear in the editor.

### File Locksmith

- Fixed an issue causing File Locksmith to hang when looking for open handles in some machines.

### Hosts File Editor

- Added a warning when duplicated entries are detected. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!

### PowerToys Run

- Support drag and dropping for file results. Thanks [@daniel-richter](https://github.com/daniel-richter)!

### Quick Accent

- Added support for dark theme. Thanks [@niels9001](https://github.com/niels9001)!
- Increased default input delay to improve out of the box experience.
- Fixed a bug causing the first character to not be selected when opening the overlay. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
- Fixed the positioning of the overlay when showing near the horizontal edges of the screen.
- Added additional Pinyin characters. Thanks [@char-46](https://github.com/char-46)!
- Added Macedonian characters. Thanks [@ad-mca-mk](https://github.com/ad-mca-mk)!
- Added a setting to sort characters by usage frequency.
- Added a setting to always start selection in the first character, even when using the arrow keys as the activation method.

### Settings

- Fixed an error that hid the option to keep the display on when using the "Indefinitely Awake" mode.
- Fixed an accessibility issue causing the navigation bar to not work with narrator in scan mode.
- Fixed an accessibility issue where the name for the shortcut control was not being read correctly.
- Tweaked the Color Picker custom color format UI. Thanks [@niels9001](https://github.com/niels9001)!
- Improved the shortcut control visibility and accessibility. Thanks [@niels9001](https://github.com/niels9001)!
- Fixed an issue causing the Settings to not be saved correctly on scenarios where the admin user would be different then the user running PowerToys.
- Added a setting to pick which language should be used by default when using Text Extractor.

### Text Extractor

- Improve behavior for CJK languages by not adding spaces for some characters that don't need them. Thanks [@AO2233](https://github.com/AO2233)!
- OCR language can now be picked in the right-click context menu.

### Video Conference Mute

- Reduced resource consumption by not starting the File Watchers when the utility is disabled.

### Documentation

- Updated the development setup documentation.
- Improved the Markdown documentation lists numbering in many docs. Thanks [@sanidhyas3s](https://github.com/sanidhyas3s)!

### Development

- Turned on C++ code analysis and incrementally fixing warnings.
- C++ code analysis no longer runs on release CI to speed up building release candidates. It still runs on GitHub CI and when building locally to maintain code quality.
- Cleaned up "to-do" comments referring to disposing memory on C#. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
- Added a fabric bot rule for localization issues.
- Fixed a CI build error after a .NET tools update.
- Update the Windows App SDK dependency version to 1.2.
- When building for arm64, the arm64 build tools are now preferred when building on an arm64 device. Thanks [@snickler](https://github.com/snickler)!
- Updated the C# test framework and removed unused Newtonsoft.Json package references.
- Updated StyleCop and fixed/enabled more warnings. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
- Fixed a language typo in the code. Thanks [@eltociear](https://github.com/eltociear)!
- Improved code quality around some silent crashes that were being reported to Microsoft servers.
- Moved the GPO asset files to source instead of docs in the repo.
- Upgraded the unit test NuGet packages.

#### What is being planned for version 0.67

For [v0.67][github-next-release-work], we'll work on below:

- Allow installing without UAC.
- Add a flyout menu to the system tray icon for quick access.
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
[github-next-release-work]: https://github.com/microsoft/PowerToys/issues?q=is%3Aopen+is%3Aissue+project%3Amicrosoft%2FPowerToys%2F40
[github-current-release-work]: https://github.com/microsoft/PowerToys/issues?q=is%3Aopen+is%3Aissue+project%3Amicrosoft%2FPowerToys%2F39

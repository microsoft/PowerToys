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
| [Mouse utilities](https://aka.ms/PowerToysOverview_MouseUtilities) | [Paste as Plain Text](https://aka.ms/PowerToysOverview_PastePlain) | [PowerRename](https://aka.ms/PowerToysOverview_PowerRename) |
| [PowerToys Run](https://aka.ms/PowerToysOverview_PowerToysRun) | [Quick Accent](https://aka.ms/PowerToysOverview_QuickAccent) | [Screen Ruler](https://aka.ms/PowerToysOverview_ScreenRuler) |
| [Shortcut Guide](https://aka.ms/PowerToysOverview_ShortcutGuide) | [Text Extractor](https://aka.ms/PowerToysOverview_TextExtractor) | [Video Conference Mute](https://aka.ms/PowerToysOverview_VideoConference) |

## Installing and running Microsoft PowerToys

### Requirements

- Windows 11 or Windows 10 version 2004 (code name 20H1 / build number 19041) or newer.
- Our installer will install the following items:
   - [Microsoft Edge WebView2 Runtime](https://go.microsoft.com/fwlink/p/?LinkId=2124703) bootstrapper. This will install the latest version.

### Via GitHub with EXE [Recommended]

Go to [Microsoft PowerToys GitHub releases page][github-release-link], click on `Assets` at the bottom to show the files available in the release. Please use the appropriate PowerToys installer that matches your machine's architecture. For most, it is `x64`.
 
 - **For x64 processors (most common):** [PowerToysSetup-0.68.0-x64.exe](https://github.com/microsoft/PowerToys/releases/download/v0.68.0/PowerToysSetup-0.68.0-x64.exe)
 - **For ARM64 processors:** [PowerToysSetup-0.68.0-arm64.exe](https://github.com/microsoft/PowerToys/releases/download/v0.68.0/PowerToysSetup-0.68.0-arm64.exe)

This is our preferred method.

### Via Microsoft Store

Install from the [Microsoft Store's PowerToys page][microsoft-store-link]. You must be using the [new Microsoft Store](https://blogs.windows.com/windowsExperience/2021/06/24/building-a-new-open-microsoft-store-on-windows-11/) which is available for both Windows 11 and Windows 10.

### Via WinGet
Download PowerToys from [WinGet][winget-link]. To install PowerToys, run the following command from the command line / PowerShell:

```powershell
winget install Microsoft.PowerToys -s winget
```

### Other install methods

There are [community driven install methods](./doc/unofficialInstallMethods.md) such as Chocolatey and Scoop. If these are your preferred install solutions, you can find the install instructions there.

## Contributing

This project welcomes contributions of all types. Help spec'ing, design, documentation, finding bugs are ways everyone can help on top of coding features / bug fixes. We are excited to work with the power user community to build a set of tools for helping you get the most out of Windows.

We ask that **before you start work on a feature that you would like to contribute**, please read our [Contributor's Guide](CONTRIBUTING.md). We will be happy to work with you to figure out the best approach, provide guidance and mentorship throughout feature development, and help avoid any wasted or duplicate effort.

Most contributions require you to agree to a [Contributor License Agreement (CLA)][oss-CLA] declaring that you have the right to, and actually do, grant us the rights to use your contribution.

For guidance on developing for PowerToys, please read the [developer docs](/doc/devdocs) for a detailed breakdown. This includes how to setup your computer to compile.

## What's Happening

### PowerToys Roadmap

Our [prioritized roadmap][roadmap] of features and utilities that the core team is focusing on.

### 0.68 - February 2023 Update

In this release, we focused on releasing new features, stability and improvements.

**Highlights**

- New utility: Paste as Plain Text allows pasting the text contents of your clipboard without formatting. Note: the formatted text in the clipboard is replaced with the unformatted text. Thanks [@carlos-zamora](https://github.com/carlos-zamora)!
- New utility: Mouse Jump allows to quickly move the mouse pointer long distances on a single screen or across multiple screens. Thanks [@mikeclayton](https://github.com/mikeclayton)!
- Add new GPO policies for automatic update downloads and update toast notifications. Thanks [@htcfreek](https://github.com/htcfreek)!
- Support MSC and CPL files in "Run command" results of PowerToys Run Program plugin. Thanks [@htcfreek](https://github.com/htcfreek)!
- Add support for log2 and log10 in PowerToys Run Calculator plugin. Thanks [@RickLuiken](https://github.com/RickLuiken)!
- Added experimentation to PowerToys first run experience.  There are current page which says "welcome" and a variant with direct instructions on how to use some of the utilities.  We want to see if directly showing how to use PowerToys leads to more people using the features :) 

### General

- Improve metered network detection in runner. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
- Update PowerToys logo used by installer. Thanks [@ChaseKnowlden](https://github.com/ChaseKnowlden1)!
- Add new GPO policies for automatic update downloads and update toast notifications. Thanks [@htcfreek](https://github.com/htcfreek)!
- Update copyright year to 2023. Thanks [@ChaseKnowlden](https://github.com/ChaseKnowlden)!

### FancyZones

- Refactored and improved code quality.
- Fix crashing on moving window between monitors with Win + arrows. (This was a hotfix for 0.67)
- Fix issue causing window attributes to not be reset properly. (This was a hotfix for 0.67)
- Fix issue causing window to not be adjusted when layout is changed. (This was a hotfix for 0.67)
- Fix issue causing window not to be unsnapped on drag started. (This was a hotfix for 0.67)
- Fix issue causing layouts not to be applied to new virtual desktops. (This was a hotfix for 0.67)
- Fix issues causing windows not to be restored correctly to their last known zone.

### File explorer add-ons

- Add Developer files previewer option to set max file size and fix styling issue. Thanks [@Aaron-Junker](https://github.com/Aaron-Junker)!
- Improve Developer files previewer exception handling and printing of error messages.
- Fix crash when generating PDF and Gcode file thumbnails. (This was a hotfix for 0.67)

### Hosts file editor

- Improve hosts file loading. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
- Improved duplicate hosts finding. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!

### Keyboard Manager

- Fix typo in Keyboard Manager Editor. Thanks [@ChristianLW](https://github.com/ChristianLW)!

### Mouse Utils

- Resolve grammatical error in Mouse Highlighter description. Thanks [@WordlessSafe1](https://github.com/WordlessSafe1)!
- New utility: Mouse Jump allows to quickly move the mouse pointer long distances on single or across screens. Thanks [@mikeclayton](https://github.com/mikeclayton)!

### Paste as Plain Text

- New utility: Paste as Plain Text allows pasting the text contents of your clipboard without formatting. Note: the formatted text in the clipboard is replaced with the unformatted text. Thanks [@carlos-zamora](https://github.com/carlos-zamora)!

### PowerToys Run

- Show Steam (steam://open/) shortcuts in the Program plugin. 
- Localize paths of Program plugin results. Thanks [@htcfreek](https://github.com/htcfreek)!
- Improved stability of the code used to get the localized names and paths. Thanks [@htcfreek](https://github.com/htcfreek)!
- Support MSC and CPL files in "Run command" results of Program plugin. Thanks [@htcfreek](https://github.com/htcfreek)!
- Added missing MSC and CPL settings to the results of Windows Settings plugin. Thanks [@htcfreek](https://github.com/htcfreek)!
- System plugin: Setting for separate "Open/Empty Recycle bin" results or single result with context menu. (This was implemented based on user feedback for a change in the last build.) Thanks [@htcfreek](https://github.com/htcfreek)!
- Add support for log2 and log10 in Calculator plugin. Thanks [@RickLuiken](https://github.com/RickLuiken)!
- Removed the TimeZone plugin.
- Fix the crash when loading thumbnail for PDF files. (This was a hotfix for 0.67)

### Shortcut Guide

- Added: Dismiss Shortcut Guide with mouse click. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!

### Quick Accent

- Added Lithuanian characters. Thanks [@saulens22](https://github.com/saulens22)!
- Added additional (Chinese) characters. Thanks [@ChaseKnowlden](https://github.com/ChaseKnowlden)!


### Settings

- Add missing flyout borders on Windows 10. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
- Add experimentation for oobe landing page. Thanks [@chenss3](https://github.com/chenss3)!
- Show icons of user-installed PowerToys Run plugins. Thanks [@al2me6](https://github.com/al2me6)!
- Fixed crash when clicking Browse for backup and restore location while running elevated.
- Respect taskbar position when showing system tray flyout. (This was a hotfix for 0.67)
- Show correct Hosts module image. (This was a hotfix for 0.67)

### Development

- Turned on C++ code analysis and incrementally fixing warnings.
- Centralize .NET NuGet packages versions. Thanks [@snickler](https://github.com/snickler)!
- Separate PowerToys installer logs and MSI logs to different files.
- Added new GPO rules to the reporting tool.
- Move PowerToys registry entries back to HKLM to fix context menu entries not working on some configurations. (This was a hotfix for 0.67)

#### What is being planned for version 0.69

For [v0.69][github-next-release-work], we'll work on below:

- Allow installing without UAC.
- New utility: [PowerToys Peek](https://github.com/microsoft/PowerToys/issues/80)
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
[github-next-release-work]: https://github.com/microsoft/PowerToys/issues?q=is%3Aopen+is%3Aissue+project%3Amicrosoft%2FPowerToys%2F42
[github-current-release-work]: https://github.com/microsoft/PowerToys/issues?q=is%3Aopen+is%3Aissue+project%3Amicrosoft%2FPowerToys%2F41

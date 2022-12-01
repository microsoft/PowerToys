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
   - [.NET 6.0.10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/6.0#runtime-desktop-6.0.10).
   - [Microsoft Edge WebView2 Runtime](https://go.microsoft.com/fwlink/p/?LinkId=2124703) bootstrapper. This will install the latest version.

### Via GitHub with EXE [Recommended]

Go to [Microsoft PowerToys GitHub releases page][github-release-link], click on `Assets` at the bottom to show the files available in the release. Please use the appropriate the PowerToys installer that matches your machine's architecture. For most, it is `x64`.
 
 - **For x64 processors (most common):** [PowerToysSetup-0.64.1-x64.exe](https://github.com/microsoft/PowerToys/releases/download/v0.64.1/PowerToysSetup-0.64.1-x64.exe)
 - **For ARM64 processors:** [PowerToysSetup-0.64.1-arm64.exe](https://github.com/microsoft/PowerToys/releases/download/v0.64.1/PowerToysSetup-0.64.1-arm64.exe)

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

### 0.64 - October 2022 Update

In this release, we focused on releasing new features and improvements.

**Highlights**

- New utility: File Locksmith allows seeing which processes are currently using the selected files.
- New utility: Hosts File Editor allows you to edit your hosts file in an Editor UI. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
- Settings has a new feature for backing up / restoring the settings from a file. Thanks [@jefflord](https://github.com/jefflord)!
- FancyZones allows you to set defaults for horizontal/vertical screens to get better intended behavior for new screens and cases where a monitor ID resets.
- PowerToys ships with Group Policy Objects settings for force disabling and enabling PowerToys utilities in organizations. Check the [GPO docs](https://github.com/microsoft/PowerToys/tree/main/doc/gpo) for more details.
- Added a warning about deprecating Video Conference Mute in the future (v0.67), please check https://github.com/microsoft/PowerToys/issues/21473 for more information.

### Known issues

- The Text Extractor utility [fails to recognize text in some cases on ARM64 devices running Windows 10](https://github.com/microsoft/PowerToys/issues/20278).
- After installing PowerToys, [the new Windows 11 context menu entries for PowerRename and Image Resizer might not appear before a system restart](https://github.com/microsoft/PowerToys/issues/19124).
- There are reports of users who are [unable to open the Settings window](https://github.com/microsoft/PowerToys/issues/18015). This is being caused by incompatibilities with some applications (RTSS RivaTuner Statistics Server is a known examples of this). If you're affected by this, please check the  linked issue to verify if any of the presented solutions works for you.

### Always on Top

- Detect and put a window on top again if it's no longer on top.

### Color Picker

- Added the hexadecimal integer format. Thanks [@marius-bughiu](https://github.com/marius-bughiu)!

### FancyZones

- Added a way for users to configure default layouts for horizontal and vertical screens.
- Replaced remaining Number Boxes in FancyZones Editor with Sliders, to improve accessibility for screen readers.
- Fixed an issue breaking window switching shortcuts.

### File Locksmith

- Added a new utility: File Locksmith.
- Thanks [@niels9001](https://github.com/niels9001) for the design on the UI!

### Group Policy Objects

- Group Policy Objects settings for force disabling and enabling PowerToys utilities.
- Thanks [@htcfreek](https://github.com/htcfreek) for your help in reviewing to make sure the shipped settings conform to system administrators expectations!

### Hosts File Editor

- Added a new utility: Hosts File Editor. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
- Thanks [@niels9001](https://github.com/niels9001) for the design help on the UI!
- Thanks [@davidegiacometti](https://github.com/davidegiacometti) for fixing the bugs found and adding features up until release!
- Thanks [@AtariDreams](https://github.com/AtariDreams) for consolidating the packages comparing to the rest of the project!
- Thanks [@htcfreek](https://github.com/htcfreek) for adding a scrollviewer to the entry editor!

### Keyboard Manager

- Fixed a delay that was not being cancelled properly. Thanks [@AtariDreams](https://github.com/AtariDreams)!

### Mouse Utilities

- Changed the opacity setting to the 1-100 range. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!

### PowerToys Run

- Changed image loading to release the images in PowerToys Run main executable. This is a try to fix the "app.dark.png" missing issues received after a PowerToys update.
- Fixed the PowerToys Run hiding after the default action failed. Thanks [@hlaueriksson](https://github.com/hlaueriksson)!
- Fixed the PowerToys Run allows showing after a context menu action succeeded. Thanks [@hlaueriksson](https://github.com/hlaueriksson)!

### Quick Accent

- Corrected "Dutch" word to "German". Thanks [@damienleroy](https://github.com/damienleroy)!
- Added the Portuguese language accents. Thanks [@pcanavar](https://github.com/pcanavar)!
- Fixed positioning of toolbar on scaled desktops.

### Screen Ruler

- Improved the acrylic brush used in the menu. Thanks [@niels9001](https://github.com/niels9001)!

### Settings

- Added a feature to backup/restore settings to/from a file. Thanks [@jefflord](https://github.com/jefflord)!
- Fixed an issue causing shortcuts shown in OOBE not updating to new values when the window was re-opened.
- Fixed the "Documents" folder usage in the backup/restore feature. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!

### Text Extractor

- Added a warning about how to install languages for OCR recognition.
- Fixed the overlay not focusing after the first activation.
- Added spaces between CJK and non-CJK words. Thanks [@maggch97](https://github.com/maggch97)!

### Video Conference Mute
- Added a setting to hide the Video Conference Mute overlay when muted. Thanks [@akabhirav](https://github.com/akabhirav)!
- Added a warning about deprecating Video Conference Mute in the future (v0.67), please check https://github.com/microsoft/PowerToys/issues/21473 for more information.

### Installer

- Added some missing files that were causing Settings and PowerRename to not function correctly on some configurations.
- Updated the .NET dependency to 6.0.10.

### Development

- Consolidated nuget packages and removed a few unused packages.
- Updated the Windows.CppRT to the latest version. Thanks [@AtariDreams](https://github.com/AtariDreams)!
- Removed the cxxopts dependency, which was no longer used. Thanks [@AtariDreams](https://github.com/AtariDreams)!
- Updated the cziplob dependency to 0.25. Thanks [@AtariDreams](https://github.com/AtariDreams)!
- Updated the System.IO.Abstractions dependency. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
- Turned on C++ code analysis and incrementally fixing warnings.
- Added the install method to the issue template on GitHub, since some issues seem to be related to specific installation methods.
- Automated installer hash creation in the release CI.
- Simplified use of `.First()` on ImageResizer. Thanks [@AtariDreams](https://github.com/AtariDreams)!
- Improved and clarified the issues templates. Thanks [@Jay-o-Way](https://github.com/Jay-o-Way)!
- Fixed a PTRun unit test to be more compatible with .NET 6. Thanks [@AtariDreams](https://github.com/AtariDreams)!

#### What is being planned for version 0.65

For [v0.65][github-next-release-work], we'll work on below:

- Ship .NET self contained and shared between utilities
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
[github-next-release-work]: https://github.com/microsoft/PowerToys/issues?q=is%3Aopen+is%3Aissue+project%3Amicrosoft%2FPowerToys%2F38
[github-current-release-work]: https://github.com/microsoft/PowerToys/issues?q=is%3Aopen+is%3Aissue+project%3Amicrosoft%2FPowerToys%2F37

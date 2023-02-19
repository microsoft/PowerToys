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

Go to [Microsoft PowerToys GitHub releases page][github-release-link], click on `Assets` at the bottom to show the files available in the release. Please use the appropriate PowerToys installer that matches your machine's architecture. For most, it is `x64`.
 
 - **For x64 processors (most common):** [PowerToysSetup-0.67.0-x64.exe](https://github.com/microsoft/PowerToys/releases/download/v0.67.0/PowerToysSetup-0.67.0-x64.exe)
 - **For ARM64 processors:** [PowerToysSetup-0.67.0-arm64.exe](https://github.com/microsoft/PowerToys/releases/download/v0.67.0/PowerToysSetup-0.67.0-arm64.exe)

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

### 0.67 - January 2023 Update

In this release, we focused on releasing new features and improvements.

**Highlights**

- Added an option for PowerToys Run to tab through results instead of context buttons. Thanks [@maws6502](https://github.com/maws6502)!
- All PowerToys registry entries are moved from machine scope (HKLM) to user scope (HKCU).
- Quick access system tray launcher. Thanks [@niels9001](https://github.com/niels9001)!

### Awake

- Disable instead of hiding "Keep screen on" option.

### FancyZones

- Refactored and improved code quality.

### File explorer add-ons

- Fixed escaping HTML-sensitive characters when previewing developer files. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!

### Image Resizer

- Improved code quality around a silent crash that was being reported to Microsoft servers.

### PowerToys Run

- Add option to tab through results only. Thanks [@maws6502](https://github.com/maws6502)!
- System plugin - Updated Recycle Bin command to allow opening the Recycle Bin. Thanks [@htcfreek](https://github.com/htcfreek)!
- System plugin - Improved Recycle Bin command to not block PT Run while the deletion is running. Thanks [@htcfreek](https://github.com/htcfreek)!
- System plugin - Small other changes to improve the usability of the Recycle Bin command. Thanks [@htcfreek](https://github.com/htcfreek)!
- WindowWalker plugin - Show all open windows with action keyword. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!

### Quick Accent

- Added dashes characters. Thanks [@Aaron-Junker](https://github.com/Aaron-Junker)!
- Added Estonian characters. Thanks [@jovark](https://github.com/jovark)!
- Added Hebrew characters. Thanks [@Evyatar-E](https://github.com/Evyatar-E)!
- Added diacritical marks. Thanks [@Aaron-Junker](https://github.com/Aaron-Junker)!
- Added Norwegian characters. Thanks [@norwayman22](https://github.com/norwayman22)!

### Settings

- Fixed URL click crash on the "What's New" screen.
- Added quick access system tray launcher. Thanks [@niels9001](https://github.com/niels9001)!

### Documentation

- Added PowerToys [disk usage footprint document](/doc/devdocs/disk-usage-footprint.md).
- Fixed some grammar issues on main readme / Wiki.  Thanks [@CanePlayz](https://github.com/CanePlayz)!

### Development

- Verify notice.md file and used NuGet packages are synced.
- Turned on C++ code analysis and incrementally fixing warnings.
- Automatically add list of .NET Runtime deps to Installer during build. Thanks [@snickler](https://github.com/snickler)!
- Move all installer registry entries to HKCU.
- Refactor installer - extract module related stuff from Product.wxs to per-module .wxs file.
- Enhance ARM64 build configuration verification. Thanks [@snickler](https://github.com/snickler)!
- Helped identify a WebView2 issue affecting PowerToys File explorer add-ons, which has been fixed upstream and released as an update through the usual Windows Update channels.

#### What is being planned for version 0.68

For [v0.68][github-next-release-work], we'll work on below:

- Allow installing without UAC.
- New utility: [PowerToys Peek](https://github.com/microsoft/PowerToys/issues/80)
- New utility: [Paste as plain text](https://github.com/microsoft/PowerToys/issues/1684)
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
[github-next-release-work]: https://github.com/microsoft/PowerToys/issues?q=is%3Aopen+is%3Aissue+project%3Amicrosoft%2FPowerToys%2F41
[github-current-release-work]: https://github.com/microsoft/PowerToys/issues?q=is%3Aopen+is%3Aissue+project%3Amicrosoft%2FPowerToys%2F40

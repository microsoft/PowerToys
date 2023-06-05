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
| [Mouse utilities](https://aka.ms/PowerToysOverview_MouseUtilities) | [Mouse Without Borders](https://aka.ms/PowerToysOverview_MouseWithoutBorders) | [Peek](https://aka.ms/PowerToysOverview_Peek) |
| [Paste as Plain Text](https://aka.ms/PowerToysOverview_PastePlain) | [PowerRename](https://aka.ms/PowerToysOverview_PowerRename) | [PowerToys Run](https://aka.ms/PowerToysOverview_PowerToysRun) |
| [Quick Accent](https://aka.ms/PowerToysOverview_QuickAccent) | [Registry Preview](https://aka.ms/PowerToysOverview_RegistryPreview) | [Screen Ruler](https://aka.ms/PowerToysOverview_ScreenRuler) |
| [Shortcut Guide](https://aka.ms/PowerToysOverview_ShortcutGuide) | [Text Extractor](https://aka.ms/PowerToysOverview_TextExtractor) | [Video Conference Mute](https://aka.ms/PowerToysOverview_VideoConference) |

## Installing and running Microsoft PowerToys

### Requirements

- Windows 11 or Windows 10 version 2004 (code name 20H1 / build number 19041) or newer.
- Our installer will install the following items:
   - [Microsoft Edge WebView2 Runtime](https://go.microsoft.com/fwlink/p/?LinkId=2124703) bootstrapper. This will install the latest version.

### Via GitHub with EXE [Recommended]

Go to [Microsoft PowerToys GitHub releases page][github-release-link], click on `Assets` at the bottom to show the files available in the release. Please use the appropriate PowerToys installer that matches your machine's architecture and install scope. For most, it is `x64` and per-user.

[github-current-release-work]: https://github.com/microsoft/PowerToys/issues?q=issue+project%3Amicrosoft%2FPowerToys%2F43
[ptUserX64]: https://github.com/microsoft/PowerToys/releases/download/v0.70.1/PowerToysUserSetup-0.70.1-x64.exe
[ptUserArm64]: https://github.com/microsoft/PowerToys/releases/download/v0.70.1/PowerToysUserSetup-0.70.1-arm64.exe
[ptMachineX64]: https://github.com/microsoft/PowerToys/releases/download/v0.70.1/PowerToysSetup-0.70.1-x64.exe
[ptMachineArm64]: https://github.com/microsoft/PowerToys/releases/download/v0.70.1/PowerToysSetup-0.70.1-arm64.exe

|  Description   | Filename | sha256 hash |
|----------------|----------|-------------|
| Per user - x64       | [PowerToysUserSetup-0.70.1-x64.exe][ptUserX64] | B8FD209310B9847DA3AC35C2C5A89F99CE5EA91F456D9D3595DD2840D62A1AC1 | 
| Per user - ARM64     | [PowerToysUserSetup-0.70.1-arm64.exe][ptUserArm64] | 5155EA186230876EF1DA6F49DC33E40D552B2BFFA0E03F66FBA71FBEB8713594 | 
| Machine wide - x64   | [PowerToysSetup-0.70.1-x64.exe][ptMachineX64] | 1BE4760558765EF363E12126282F1E3340A8ADFF657C5C51714F7E096F86EE50 | 
| Machine wide - ARM64 | [PowerToysSetup-0.70.1-arm64.exe][ptMachineArm64] | 9F267B7AD91E5FAE86ED5050A08A24756CE3EA9875FFCFDE195F1F4F299F0933 |

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

## Contributing

This project welcomes contributions of all types. Help spec'ing, design, documentation, finding bugs are ways everyone can help on top of coding features / bug fixes. We are excited to work with the power user community to build a set of tools for helping you get the most out of Windows.

We ask that **before you start work on a feature that you would like to contribute**, please read our [Contributor's Guide](CONTRIBUTING.md). We will be happy to work with you to figure out the best approach, provide guidance and mentorship throughout feature development, and help avoid any wasted or duplicate effort.

Most contributions require you to agree to a [Contributor License Agreement (CLA)][oss-CLA] declaring that you have the right to, and actually do, grant us the rights to use your contribution.

For guidance on developing for PowerToys, please read the [developer docs](/doc/devdocs) for a detailed breakdown. This includes how to setup your computer to compile.

## What's Happening

### PowerToys Roadmap

Our [prioritized roadmap][roadmap] of features and utilities that the core team is focusing on.

### 0.70 - May 2023 Update

In this release, we focused on releasing new features, stability and improvements.

**Highlights**

- New utility: Mouse Without Borders enables you to interact with other computers from the same keyboard and mouse and share clipboard and files between the machines. We’ve upgraded it to .NET 7 and made a few small adjustments to fit inside the PowerToys model. Thanks [@truong2d](https://github.com/truong2d) and the rest of the contributors from the Microsoft Garage!
- New utility: Peek is a utility that shows a quick preview of files selected in File Explorer when you press a shortcut (`Ctrl`+`Space` by default). Thanks [@SamChaps](https://github.com/SamChaps)!
- Registry preview Quality of Life improvements. Thanks [@randyrants](https://github.com/randyrants)!
- Awake Quality of Life improvements. Thanks [@dend](https://github.com/dend)!
- Mouse Jump Quality of Life improvements. Thanks [@mikeclayton](https://github.com/mikeclayton)!

### General

- New utility: Mouse Without Borders. Thanks [@truong2d](https://github.com/truong2d) and [other original contributors](https://github.com/microsoft/PowerToys/blob/main/COMMUNITY.md#mouse-without-borders-original-contributors)!
- New utility: Peek. Thanks [@SamChaps](https://github.com/SamChaps)!
- Fixed a bug causing saved settings to clear sometimes when upgrading PowerToys.
- Font, icon and corner radius adjustments in the UI across utilities. Thanks [@Jay-o-Way](https://github.com/Jay-o-Way)!

### Awake

- Update to command line output to match the arguments. Thanks [@rpr69](https://github.com/rpr69) for creating a PR to help fix this.
- Fix crash happening when setting an expiration date on time zones with a negative offset relative to UTC (This was a hotfix for 0.69).
- Fix missing logging file when installing (This was a hotfix for 0.69).
- Upgraded Awake to a new version, with Quality of Life improvements and fixing many issues regarding Awake not resetting or not keeping the computer awake when expected. Thanks [@dend](https://github.com/dend)!

### FancyZones

- Fixed accessibility issues on the Editor.

### File Locksmith

- Fixed tooltips having a transparent background (This was a hotfix for 0.69).

### File Explorer add-ons

- Add a Setting to select a background for the SVG Preview. Thanks [@zanseb](https://github.com/zanseb)!

### Installer

- Added more utilities to terminate when installing to help prevent files that sometimes are leftover from uninstall.

### Keyboard Manager

- Fixed an issue causing mapping to media keys to type additional characters.

### Measure Tool

- Created a setting to specify the default measure tool. Thanks [@zanseb](https://github.com/zanseb)!

### Mouse Jump

- Reduced dependency on WinForms utility classes. Thanks [@mikeclayton](https://github.com/mikeclayton)!
- Improved popup responsiveness. Thanks [@mikeclayton](https://github.com/mikeclayton)!
- Added a setting to set a custom sized window. Thanks [@mikeclayton](https://github.com/mikeclayton)!
- Added some shortcuts for screen navigation. Thanks [@mikeclayton](https://github.com/mikeclayton)!

### Peek
- New utility: Peek. Thanks [@SamChaps](https://github.com/SamChaps), who drove the effort! Many thanks for all the contributors who made it possible: [@danielchau](https://github.com/danielchau), [@estebanm123](https://github.com/estebanm123), [@Joanna-Zhou](https://github.com/Joanna-Zhou), [@jth-ms](https://github.com/jth-ms), [@miksalmon](https://github.com/miksalmon), [@niels9001](https://github.com/niels9001), [@RobsonPontin](https://github.com/RobsonPontin), [@sujessie](https://github.com/sujessie), and [@Sytta](https://github.com/Sytta)!

### PowerToys Run

- Add a plugin to start other PowerToys. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
- Added code to the Shell plugin to use Windows Terminal. Currently accessible only through manipulating the settings file directly. Thanks [@phoenix172](https://github.com/phoenix172)!

### Quick Accent
- Added a missing character to the Welsh language.

### Registry Preview

- Specify minimum size / position values for the UI (This was a hotfix for 0.69). Thanks [@randyrants](https://github.com/randyrants)!
- Fixes in the UI command bar (This was a hotfix for 0.69). Thanks [@randyrants](https://github.com/randyrants)!
- Fix crash on opening a file picker when running elevated (This was a hotfix for 0.69). Thanks [@randyrants](https://github.com/randyrants)!
- Fixed tooltips having a transparent background (This was a hotfix for 0.69).
- Fixed a file size limit typo. Thanks [@idma88](https://github.com/idma88)!
- Improve hexadecimal value parsing. Thanks [@randyrants](https://github.com/randyrants)!
- Added a button to open the Registry Editor at a selected key. Thanks [@randyrants](https://github.com/randyrants)!
- Improve key and value parsing. Thanks [@randyrants](https://github.com/randyrants)!
- Better theme support for caption bar. Thanks [@randyrants](https://github.com/randyrants)!
- Fix an issue handling empty DWORD and QWORD values. Thanks [@randyrants](https://github.com/randyrants)!

### Settings

- Update the What's New screen to hide the installer hashes in the new format (This was a hotfix for 0.69).
- Fix crashes happening when using the Shortcut Control (This was a hotfix for 0.69).
- The Settings window now has a minimum width. Thanks [@niels9001](https://github.com/niels9001)!
- Prevent a second Settings instance from being opened on upgrade.
- Fix accessibility issues on many pages. Thanks [@niels9001](https://github.com/niels9001)!

### Documentation

- Fix a dead link in documentation that was pointing to the wrong settings specification. Thanks [@zanseb](https://github.com/zanseb)!
- Added some missing contributors to COMMUNITY.md

### Development

- Fixed the CI release pipelines hash generation (This was a hotfix for 0.69).
- Added per-user installers to the winget package submission script.
- Upgraded the Community Toolkit Labs dependency. Thanks [@niels9001](https://github.com/niels9001)!
- Fixed building with Visual Studio 17.6. Thanks [@snickler](https://github.com/snickler)!
- Upgraded the WebView 2 dependency.
- Upgraded the WinAppSDK dependency to 1.3.1.
- Fixed a typo preventing the clean up script to run. Thanks [@Sajad-Lx](https://github.com/Sajad-Lx)!
- Fixed encoding on a test file to fix running tests in some configurations. Thanks [@VisualBasist](https://github.com/VisualBasist)!
- Made the GPO release assets come named with a version in the build CI output.

#### What is being planned for version 0.71

For [v0.71][github-next-release-work], we'll work on below:

- Adjustments on feedback / stability / bug fixes

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
[github-next-release-work]: https://github.com/microsoft/PowerToys/issues?q=is%3Aopen+is%3Aissue+project%3Amicrosoft%2FPowerToys%2F44
[github-current-release-work]: https://github.com/microsoft/PowerToys/issues?q=is%3Aopen+is%3Aissue+project%3Amicrosoft%2FPowerToys%2F43

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
| [PowerToys Run](https://aka.ms/PowerToysOverview_PowerToysRun) | [Quick Accent](https://aka.ms/PowerToysOverview_QuickAccent) | [Registry Preview](https://aka.ms/PowerToysOverview_RegistryPreview) |
| [Screen Ruler](https://aka.ms/PowerToysOverview_ScreenRuler) | [Shortcut Guide](https://aka.ms/PowerToysOverview_ShortcutGuide) | [Text Extractor](https://aka.ms/PowerToysOverview_TextExtractor) |
| [Video Conference Mute](https://aka.ms/PowerToysOverview_VideoConference) |

## Installing and running Microsoft PowerToys

### Requirements

- Windows 11 or Windows 10 version 2004 (code name 20H1 / build number 19041) or newer.
- Our installer will install the following items:
   - [Microsoft Edge WebView2 Runtime](https://go.microsoft.com/fwlink/p/?LinkId=2124703) bootstrapper. This will install the latest version.

### Via GitHub with EXE [Recommended]

Go to [Microsoft PowerToys GitHub releases page][github-release-link], click on `Assets` at the bottom to show the files available in the release. Please use the appropriate PowerToys installer that matches your machine's architecture and install scope. For most, it is `x64` and per-user.
 
 - **For x64 processors (most common) per-user installer:** [PowerToysUserSetup-0.69.0-x64.exe](https://github.com/microsoft/PowerToys/releases/download/v0.69.0/PowerToysUserSetup-0.69.0-x64.exe)
 - **For x64 processors per-machine installer:** [PowerToysSetup-0.69.0-x64.exe](https://github.com/microsoft/PowerToys/releases/download/v0.69.0/PowerToysSetup-0.69.0-x64.exe)
 - **For ARM64 processors per-user installer:** [PowerToysUserSetup-0.69.0-arm64.exe](https://github.com/microsoft/PowerToys/releases/download/v0.69.0/PowerToysUserSetup-0.69.0-arm64.exe)
 - **For ARM64 processors per-machine installer:** [PowerToysSetup-0.69.0-arm64.exe](https://github.com/microsoft/PowerToys/releases/download/v0.69.0/PowerToysSetup-0.69.0-arm64.exe)

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

### 0.69 - March 2023 Update

In this release, we focused on releasing new features, stability and improvements. Early notice for v0.70, we will be releasing it later in May 2023.

**Highlights**

- New utility: Registry Preview is a utility to visualize and edit Windows Registry files. Thanks [@randyrants](https://github.com/randyrants)!
- Support per-user scope installation.
- Awake: Quality-of-life improvements and introduced keeping system awake until expiration time and date. Thanks [@dend](https://github.com/dend)!
- PowerToys Run: Fix crashing issue caused by thumbnail image loading.

### General

- New utility: Registry Preview. Thanks [@randyrants](https://github.com/randyrants)!
- Fix issue causing folders to not be removed on uninstall.
- Support per-user scope installation.
   - Companies can control this using the new GPO.

### Awake

- Quality-of-life improvements and introduced keeping system awake until expiration time and date. Thanks [@dend](https://github.com/dend)!

### Color Picker

- Fix issue sampling timing and grid issue causing Color Picker to sample the color of its own grid. Thanks [@IHorvalds](https://github.com/IHorvalds)!

### FancyZones

- Fix window cycling on multiple monitors issue.

### File Locksmith

- Add context menu icon. Thanks [@htcfreek](https://github.com/htcfreek)!

### Mouse Utils

- Mouse Jump - Simulate mouse input event on mouse jump in addition to cursor move.
- Mouse Jump - Improve performance of screenshot generation. Thanks [@mikeclayton](https://github.com/mikeclayton)!

### Paste as Plain Text

- Support Ctrl+V as activation shortcut. (This was a hotfix for 0.67)
- Repress modifier keys after plain paste. (This was a hotfix for 0.67) Thanks [@UnderKoen](https://github.com/UnderKoen)!
- Set default shortcut to Ctrl+Win+Alt+V. (This was a hotfix for 0.67)
- Update icons. Thanks [@niels9001](https://github.com/niels9001)!

### PowerRename

- Show PowerRename in directory background context menu.
- Fix the crash on clicking Select/UnselectAll checkbox while showing only files to be renamed.
- Improve performance on populating Renamed items when many items are being renamed.

### PowerToys Run

- Add setting to disable thumbnails generation for files. (This was a hotfix for 0.67)
- Calculator plugin - handle implied multiplication expressions. Thanks [@jjavierdguezas](https://github.com/jjavierdguezas)!
- Fix Calculator plugin unit tests to respect decimal separator locale. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
- Fix crashing caused by thumbnail image loading.
- Date & Time plugin - Add filename-compatible date & time format. Thanks [@Picazsoo](https://github.com/Picazsoo)!
- Improved the error message shown on plugin loading error. Thanks [@htcfreek](https://github.com/htcfreek)!

### Quick Accent

- Fix existing and add missing Hebrew and Pinyin characters. Thanks [@stevenlele](https://github.com/stevenlele)!

### Registry Preview

- Added a new utility: Registry Preview.
- Thanks [@htcfreek](https://github.com/htcfreek)! for the help shipping this utility!
- Thanks [@niels9001](https://github.com/niels9001) for the help on the UI!

### Video Conference Mute

- Add toolbar DPI scaling support.
- Fix selecting overlay image when Settings app is running elevated.
- Add push-to-talk (and push-to-reverse) feature. Thanks [@pajawojciech](https://github.com/pajawojciech)!

### Settings

- Fix Experiment bitmap icon rendering on theme change and bump CommunityToolkit.Labs.WinUI.SettingsControls package version. Thanks [@niels9001](https://github.com/niels9001)!
- Video Conference Mute page improvements. Thanks [@Jay-o-Way](https://github.com/Jay-o-Way)!
- Add warning that PowerToys Run might get no focus if "Use centralized keyboard hook" settings is enabled. Thanks [@Aaron-Junker](https://github.com/Aaron-Junker)!
- Fix ShortcutControl issues related to keyboard input focus, theme change and missing error badge when invalid key is pressed. Thanks [@htcfreek](https://github.com/htcfreek)!
- Add warning when Ctrl+V and Ctrl+Shift+V is used as an activation shortcut for Paste as Plain Text. Thanks [@htcfreek](https://github.com/htcfreek)! 

### Documentation

- Update CONTRIBUTING.md with information about localization issues. Thanks [@Aaron-Junker](https://github.com/Aaron-Junker)!
- Remove localization from URLs. Thanks [@Jay-o-Way](https://github.com/Jay-o-Way)!
- Add dev docs for tools. Thanks [@Aaron-Junker](https://github.com/Aaron-Junker)!

### Development

- Ignore spellcheck for MouseJumpUI/MainForm.resx file. (This was a hotfix for 0.67)
- Optimize versionAndSignCheck.ps1 script. Thanks [@snickler](https://github.com/snickler)!
- Upgraded NetAnalyzers to 7.0.1. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
- Move all DLL imports in Settings project to NativeMethods.cs file.
- Fix FancyZones tools build issues. Thanks [@Aaron-Junker](https://github.com/Aaron-Junker)!
- Centralize Logger used in C# projects. Thanks [@Aaron-Junker](https://github.com/Aaron-Junker)!
- Add missing project references. Thanks [@ACGNnsj](https://github.com/ACGNnsj)!

#### What is being planned for version 0.70

For [v0.70][github-next-release-work], we'll work on below:

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
[github-next-release-work]: https://github.com/microsoft/PowerToys/issues?q=is%3Aopen+is%3Aissue+project%3Amicrosoft%2FPowerToys%2F43
[github-current-release-work]: https://github.com/microsoft/PowerToys/issues?q=is%3Aopen+is%3Aissue+project%3Amicrosoft%2FPowerToys%2F42

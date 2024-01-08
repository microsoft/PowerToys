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
| [Always on Top](https://aka.ms/PowerToysOverview_AoT) | [PowerToys Awake](https://aka.ms/PowerToysOverview_Awake) | [Command Not Found](https://aka.ms/PowerToysOverview_CmdNotFound) |
| [Color Picker](https://aka.ms/PowerToysOverview_ColorPicker) | [Crop And Lock](https://aka.ms/PowerToysOverview_CropAndLock) | [Environment Variables](https://aka.ms/PowerToysOverview_EnvironmentVariables) |
| [FancyZones](https://aka.ms/PowerToysOverview_FancyZones) | [File Explorer Add-ons](https://aka.ms/PowerToysOverview_FileExplorerAddOns) | [File Locksmith](https://aka.ms/PowerToysOverview_FileLocksmith) |
| [Hosts File Editor](https://aka.ms/PowerToysOverview_HostsFileEditor) | [Image Resizer](https://aka.ms/PowerToysOverview_ImageResizer) | [Keyboard Manager](https://aka.ms/PowerToysOverview_KeyboardManager) |
| [Mouse utilities](https://aka.ms/PowerToysOverview_MouseUtilities) | [Mouse Without Borders](https://aka.ms/PowerToysOverview_MouseWithoutBorders) | [Peek](https://aka.ms/PowerToysOverview_Peek) |
| [Paste as Plain Text](https://aka.ms/PowerToysOverview_PastePlain) | [PowerRename](https://aka.ms/PowerToysOverview_PowerRename) | [PowerToys Run](https://aka.ms/PowerToysOverview_PowerToysRun) |
| [Quick Accent](https://aka.ms/PowerToysOverview_QuickAccent) | [Registry Preview](https://aka.ms/PowerToysOverview_RegistryPreview) | [Screen Ruler](https://aka.ms/PowerToysOverview_ScreenRuler) |
| [Shortcut Guide](https://aka.ms/PowerToysOverview_ShortcutGuide) | [Text Extractor](https://aka.ms/PowerToysOverview_TextExtractor) | [Video Conference Mute](https://aka.ms/PowerToysOverview_VideoConference) |

## Installing and running Microsoft PowerToys

### Requirements

- Windows 11 or Windows 10 version 2004 (code name 20H1 / build number 19041) or newer.
- x64 or ARM64 processor
- Our installer will install the following items:
   - [Microsoft Edge WebView2 Runtime](https://go.microsoft.com/fwlink/p/?LinkId=2124703) bootstrapper. This will install the latest version.

### Via GitHub with EXE [Recommended]

Go to the [Microsoft PowerToys GitHub releases page][github-release-link] and click on `Assets` at the bottom to show the files available in the release. Please use the appropriate PowerToys installer that matches your machine's architecture and install scope. For most, it is `x64` and per-user.

<!-- items that need to be updated release to release -->
[github-next-release-work]: https://github.com/microsoft/PowerToys/issues?q=project%3Amicrosoft%2FPowerToys%2F51
[github-current-release-work]: https://github.com/microsoft/PowerToys/issues?q=project%3Amicrosoft%2FPowerToys%2F50
[ptUserX64]: https://github.com/microsoft/PowerToys/releases/download/v0.77.0/PowerToysUserSetup-0.77.0-x64.exe
[ptUserArm64]: https://github.com/microsoft/PowerToys/releases/download/v0.77.0/PowerToysUserSetup-0.77.0-arm64.exe
[ptMachineX64]: https://github.com/microsoft/PowerToys/releases/download/v0.77.0/PowerToysSetup-0.77.0-x64.exe
[ptMachineArm64]: https://github.com/microsoft/PowerToys/releases/download/v0.77.0/PowerToysSetup-0.77.0-arm64.exe
 
|  Description   | Filename | sha256 hash |
|----------------|----------|-------------|
| Per user - x64       | [PowerToysUserSetup-0.77.0-x64.exe][ptUserX64] | 3485D3F45A3DE6ED7FA151A4CE9D6F941491C30E83AB51FD59B4ADCD20611F1A |
| Per user - ARM64     | [PowerToysUserSetup-0.77.0-arm64.exe][ptUserArm64] | 762DF383A01006A20C0BAB2D321667E855236EBA7108CDD475E4E2A8AB752E0E |
| Machine wide - x64   | [PowerToysSetup-0.77.0-x64.exe][ptMachineX64] | 1B6D4247313C289B07A3BF3531E215B3F9BEDBE9254919637F2AC502B4773C31 |
| Machine wide - ARM64 | [PowerToysSetup-0.77.0-arm64.exe][ptMachineArm64] | CF740B3AC0EB5C23E18B07ACC2D0C6EC5F4CE4B3A2EDC67C2C9FDF6EF78F0352 |

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

### 0.77 - December 2023 Update

In this release, we focused on new features, stability and improvements.

**Highlights**

 - New utility: Command Not Found PowerShell 7.4 module - adds the ability to detect failed commands in PowerShell 7.4 and suggest a package to install using winget. Thanks [@carlos-zamora](https://github.com/carlos-zamora)!
 - Keyboard manager does not register low level hook if there are no remappings anymore.
 - Added support for QOI file type in Peek. Thanks [@pedrolamas](https://github.com/pedrolamas)!
 - Added support for loading 3rd-party plugins with additional dependencies in PowerToys Run. Thanks [@coreyH](https://github.com/CoreyHayward)!

### General

 - Bump WPF-UI package version to fix crashes related to theme changes. (This was a hotfix for 0.76)
 - Fixed typo in version change notification. Thanks [@PesBandi](https://github.com/PesBandi)!
 - Code improvements and fixed silenced warnings introduced by upgrade to .NET 8.
 - Update copyright year for 2024.
 - Added setting to disable warning notifications about detecting an application running as Administrator.

### AlwaysOnTop

 - Show notification when elevated app is in the foreground but AlwaysOnTop is running non-elevated.

### Command Not Found

 - Added a new utility: A Command Not Found PowerShell 7.4 module. It adds the ability to detect failed commands in PowerShell 7.4 and suggest a package to install using winget. Thanks [@carlos-zamora](https://github.com/carlos-zamora)!

### Environment Variables

 - Fixed issue causing Environment Variables window not to appear as a foreground window.

### FancyZones

 - Fixed snapping specific apps (e.g. Facebook messenger). (This was a hotfix for 0.76)
 - Fixed behavior of Move newly created windows to current active monitor setting to keep maximize state on moving. Thanks [@quyenvsp](https://github.com/quyenvsp)!
 - Fixed issue causing FancyZones Editor layout window to be zoned.

### File Explorer add-ons

 - Fixed WebView2 based previewers issue caused by the latest WebView update. (This was a hotfix for 0.76)

### Hosts File Editor

 - Fixed issue causing settings not to be preserved on update.

### Image Resizer

 - Fixed crash caused by WpfUI ThemeWatcher. (This was a hotfix for 0.76)

### Keyboard Manager

 - Do not register low level hook if there are no remappings.

### Peek

 - Improved icon and title showing for previewed files. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Added QOI file type support. Thanks [@pedrolamas](https://github.com/pedrolamas)!

### PowerToys Run

 - Fixed results list UI element height for different maximum number of results value. (This was a hotfix for 0.76)
 - Fixed icon extraction for .lnk files. (This was a hotfix for 0.76)
 - Fixed search box UI glitch when FlowDirection is RightToLeft. (This was a hotfix for 0.76)
 - Fixed theme setting. (This was a hotfix for 0.76)
 - Fixed error reporting window UI issue. Thanks [@niels9001](https://github.com/niels9001)!
 - UI improvements and ability to show/hide plugins overview panel. Thanks [@niels9001](https://github.com/niels9001)!
 - Allow interaction with plugin hints. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Switch to WPF-UI theme manager. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Fixed issue causing 3rd party plugin's dependencies dll not being loaded properly. Thanks [@coreyH](https://github.com/CoreyHayward)!
 - Added configurable font sizes. Thanks [@niels9001](https://github.com/niels9001)!
 - Changed the text color of plugin hints to improve the contrast when light theme is used. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Fix scientific notation errors in Calculator plugin. Thanks [@viggyd](https://github.com/viggyd)!
 - Add URI/URL features to Value generator plugin. Thanks [@htcfreek](https://github.com/htcfreek)!

### Quick Accent

 - Moved Greek specific characters from All language set to Greek. Thanks [@Aaron-Junker](https://github.com/Aaron-Junker)!
 - Add more mathematical symbols. Thanks [@kevinfu2](https://github.com/kevinfu2)!

### Settings

 - Fixed exception occurring on theme change.
 - Fix "What's new" icon. Thanks [@niels9001](https://github.com/niels9001)!
 - Remove obsolete UI Font icon properties. Thanks [@Jay-o-Way](https://github.com/Jay-o-Way)!
 - OOBE UI improvements. Thanks [@Jay-o-Way](https://github.com/Jay-o-Way)!
 - XAML Binding improvements. Thanks [@Jay-o-Way](https://github.com/Jay-o-Way)!
 - Fixed crash caused by ThemeListener constructor exceptions.

### Documentation

 - Improved docs for adding new languages to monaco. Thanks [@PesBandi](https://github.com/PesBandi)!
 - Update README.md to directly state x64 & ARM processor in requirements.
 - Added Scoop plugin to PowerToys Run thirdPartyRunPlugins.md docs. Thanks [@Quriz](https://github.com/Quriz)!

### Development

 - Adopted XamlStyler for PowerToys Run source code. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Consolidate Microsoft.Windows.SDK.BuildTools across solution.
 - Upgraded Boost's lib to v1.84.
 - Upgraded HelixToolkit packages to the latest versions.
 - Updated sdl baselines.

#### What is being planned for version 0.78

For [v0.78][github-next-release-work], we'll work on the items below:

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

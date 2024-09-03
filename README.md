# Microsoft PowerToys

![Hero image for Microsoft PowerToys](doc/images/overview/PT_hero_image.png)

[How to use PowerToys][usingPowerToys-docs-link] | [Downloads & Release notes][github-release-link] | [Contributing to PowerToys](#contributing) | [What's Happening](#whats-happening) | [Roadmap](#powertoys-roadmap)

## Build status

| Architecture | Solution (Main) | Solution (Stable) | Installer (Main) |
|--------------|-----------------|-------------------|------------------|
| x64 | [![Build Status for Main](https://dev.azure.com/shine-oss/PowerToys/_apis/build/status%2FPowerToys%20CI?branchName=main&jobName=Build%20x64%20Release)](https://dev.azure.com/shine-oss/PowerToys/_build/latest?definitionId=3&branchName=main) | [![Build Status for Stable](https://dev.azure.com/shine-oss/PowerToys/_apis/build/status%2FPowerToys%20CI?branchName=stable&jobName=Build%20x64%20Release)](https://dev.azure.com/shine-oss/PowerToys/_build/latest?definitionId=3&branchName=stable) | [![Build Status Installer pipeline](https://dev.azure.com/microsoft/Dart/_apis/build/status/PowerToys/PowerToys%20Signed%20YAML%20Release%20Build?branchName=main&jobName=Build&configuration=Build%20Release_x64)](https://dev.azure.com/microsoft/Dart/_build/latest?definitionId=76541&branchName=main) |
| ARM64 | [![Build Status for Main](https://dev.azure.com/shine-oss/PowerToys/_apis/build/status%2FPowerToys%20CI?branchName=main&jobName=Build%20arm64%20Release)](https://dev.azure.com/shine-oss/PowerToys/_build/latest?definitionId=3&branchName=main) | [![Build Status for Stable](https://dev.azure.com/shine-oss/PowerToys/_apis/build/status%2FPowerToys%20CI?branchName=main&jobName=Build%20arm64%20Release)](https://dev.azure.com/shine-oss/PowerToys/_build/latest?definitionId=3&branchName=main) | [![Build Status Installer pipeline](https://dev.azure.com/microsoft/Dart/_apis/build/status/PowerToys/PowerToys%20Signed%20YAML%20Release%20Build?branchName=main&jobName=Build&configuration=Build%20Release_arm64)](https://dev.azure.com/microsoft/Dart/_build/latest?definitionId=76541&branchName=main) |

## About

Microsoft PowerToys is a set of utilities for power users to tune and streamline their Windows experience for greater productivity. For more info on [PowerToys overviews and how to use the utilities][usingPowerToys-docs-link], or any other tools and resources for [Windows development environments](https://learn.microsoft.com/windows/dev-environment/overview), head over to [learn.microsoft.com][usingPowerToys-docs-link]!

|              | Current utilities: |              |
|--------------|--------------------|--------------|
| [Advanced Paste](https://aka.ms/PowerToysOverview_AdvancedPaste) | [Always on Top](https://aka.ms/PowerToysOverview_AoT) | [PowerToys Awake](https://aka.ms/PowerToysOverview_Awake) |
| [Command Not Found](https://aka.ms/PowerToysOverview_CmdNotFound) | [Color Picker](https://aka.ms/PowerToysOverview_ColorPicker) | [Crop And Lock](https://aka.ms/PowerToysOverview_CropAndLock) |
| [Environment Variables](https://aka.ms/PowerToysOverview_EnvironmentVariables) | [FancyZones](https://aka.ms/PowerToysOverview_FancyZones) | [File Explorer Add-ons](https://aka.ms/PowerToysOverview_FileExplorerAddOns) |
| [File Locksmith](https://aka.ms/PowerToysOverview_FileLocksmith) | [Hosts File Editor](https://aka.ms/PowerToysOverview_HostsFileEditor) | [Image Resizer](https://aka.ms/PowerToysOverview_ImageResizer) |
| [Keyboard Manager](https://aka.ms/PowerToysOverview_KeyboardManager) | [Mouse utilities](https://aka.ms/PowerToysOverview_MouseUtilities) | [Mouse Without Borders](https://aka.ms/PowerToysOverview_MouseWithoutBorders) |
| [Peek](https://aka.ms/PowerToysOverview_Peek) | [Paste as Plain Text](https://aka.ms/PowerToysOverview_PastePlain) | [PowerRename](https://aka.ms/PowerToysOverview_PowerRename) |
| [PowerToys Run](https://aka.ms/PowerToysOverview_PowerToysRun) | [Quick Accent](https://aka.ms/PowerToysOverview_QuickAccent) | [Registry Preview](https://aka.ms/PowerToysOverview_RegistryPreview) |
| [Screen Ruler](https://aka.ms/PowerToysOverview_ScreenRuler) | [Shortcut Guide](https://aka.ms/PowerToysOverview_ShortcutGuide) | [Text Extractor](https://aka.ms/PowerToysOverview_TextExtractor) |
| [Video Conference Mute](https://aka.ms/PowerToysOverview_VideoConference) | [Workspaces](https://aka.ms/PowerToysOverview_Workspaces) |

## Installing and running Microsoft PowerToys

### Requirements

- Windows 11 or Windows 10 version 2004 (code name 20H1 / build number 19041) or newer.
- x64 or ARM64 processor
- Our installer will install the following items:
   - [Microsoft Edge WebView2 Runtime](https://go.microsoft.com/fwlink/p/?LinkId=2124703) bootstrapper. This will install the latest version.

### Via GitHub with EXE [Recommended]

Go to the [Microsoft PowerToys GitHub releases page][github-release-link] and click on `Assets` at the bottom to show the files available in the release. Please use the appropriate PowerToys installer that matches your machine's architecture and install scope. For most, it is `x64` and per-user.

<!-- items that need to be updated release to release -->
[github-next-release-work]: https://github.com/microsoft/PowerToys/issues?q=is%3Aissue+milestone%3A%22PowerToys+0.85%22
[github-current-release-work]: https://github.com/microsoft/PowerToys/issues?q=is%3Aissue+milestone%3A%22PowerToys+0.84%22
[ptUserX64]: https://github.com/microsoft/PowerToys/releases/download/v0.84.0/PowerToysUserSetup-0.84.0-x64.exe
[ptUserArm64]: https://github.com/microsoft/PowerToys/releases/download/v0.84.0/PowerToysUserSetup-0.84.0-arm64.exe
[ptMachineX64]: https://github.com/microsoft/PowerToys/releases/download/v0.84.0/PowerToysSetup-0.84.0-x64.exe
[ptMachineArm64]: https://github.com/microsoft/PowerToys/releases/download/v0.84.0/PowerToysSetup-0.84.0-arm64.exe
 
|  Description   | Filename | sha256 hash |
|----------------|----------|-------------|
| Per user - x64       | [PowerToysUserSetup-0.84.0-x64.exe][ptUserX64] | 6792180D697ED9FDF9AA7B3F0AB92767CF4C79B526715C802F545E2DCB201BE3 |
| Per user - ARM64     | [PowerToysUserSetup-0.84.0-arm64.exe][ptUserArm64] | 3D071F009B5E3DBAD21D7450ADB53CBC85CAFB21016E44F414E2A03C188D2FAF |
| Machine wide - x64   | [PowerToysSetup-0.84.0-x64.exe][ptMachineX64] | 67B7E685AAF635803A87D8EE96CA1AF5024910B0BF00A9277CD77C810D049446 |
| Machine wide - ARM64 | [PowerToysSetup-0.84.0-arm64.exe][ptMachineArm64] | 259DA1EFB33A616CF64840B8D8AB84F86A43F61687578B43849D5DE11F77AF82 |

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

### 0.84 - August 2024 Update

In this release, we focused on adding a new utility (PowerToys Workspaces), Advanced paste custom actions feature, stability, and improvements.

**Highlights**

 - New utility: PowerToys Workspaces - this utility can launch a set of applications to a custom layout and configuration on the desktop. App arrangements can be saved as a workspace and then relaunched with one click from the Workspaces Editor or from a desktop shortcut. In the editor, app configuration can be customized using CLI arguments and "launch as admin" modifiers, and app window sizes and positions can be updated as desired. This is our first public version of Workspaces and we are excited for you to try it out for yourself! Make sure to file issues you encounter on our GitHub so the team can continue to improve the utility.
    - Known issues - the team is actively working on fixing these:
       - Apps that launch as admin are unable to be repositioned to the desired layout.
       - Border of "Remove" / "Add Back" app button in editor is not clearly visible on light themes.
 - Added Awake --use-parent-pid CLI argument to attach to parent process. Thanks [@dend](https://github.com/dend)!
 - Added custom actions - user-specified pre-defined prompts for the AI model. Additionally, actions (both standard and custom) are now searchable from prompt box and Ctrl + number in-app shortcuts are now applicable for first 9 search results.
 - Ported all C++/CX code to C++/WinRT as part of a refactor and upgrade series aimed at enabling AOT (Ahead of Time) compilation for enhanced performance and reduced disk footprint.

### General

 - Added DSC support for ImageResizer resize sizes property.
 
### Advanced Paste

 - Added custom actions - user-specified pre-defined prompts for the AI model. Additionally, actions (both standard and custom) are now searchable from prompt box and Ctrl + number in-app shortcuts are now applicable for first 9 search results.

### Awake

 - Added --use-parent-pid CLI argument to attach to parent process and fixed issue causing tray icon to disappear. Thanks [@dend](https://github.com/dend)!

### Hosts File Editor

 -  Fixed save failure when the hosts file is hidden. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!

### File Explorer add-ons

 - Fixed multiple preview form positioning issues causing floating, detached windows, CoreWebView2 related exception and process leak. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!

### Keyboard Manager

 - Convert RemapBufferRow to a struct with descriptive field names. Thanks [@masaru-iritani](https://github.com/masaru-iritani)!
 - Fixed issue causing stuck Ctrl key when shortcuts contain AltGr key.

### Peek

 - Added long paths support. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!

### Quick Accent

 - Moved number superscripts and subscripts from Portuguese to all languages definition. Thanks [@octastylos-pseudodipteros](https://github.com/octastylos-pseudodipteros)!

### PowerRename

 - Updated the tooltip text of the replace box info button. Thanks [@Agnibaan](https://github.com/Agnibaan)!

### PowerToys Run

 - Fixed window positioning on start-up introduced in 0.83.
 - Improved default web browser detection. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Fixed volume ounces conversion to support both imperial and metric. Thanks [@GhostVaibhav](https://github.com/GhostVaibhav)!
 - Fixed thread-safety issue causing results not to be shown on first launch.

### Screen Ruler

 - Added multiple measurements support for all measuring tools.

### Settings

 - Improved disabled animations InfoBar in Find My Mouse page. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!

### Workspaces

 - New utility: PowerToys Workspaces - this utility can launch a set of applications to a custom layout and configuration on the desktop. App arrangements can be saved as a workspace and then relaunched with one click from the Workspaces Editor or from a desktop shortcut. In the editor, app configuration can be customized using CLI arguments and "launch as admin" modifiers, and app window sizes and positions can be updated as desired. This is our first public version of Workspaces and we are excited for you to try it out for yourself! Make sure to file issues you encounter on our GitHub so the team can continue to improve the utility.

### Documentation

 - Added ChatGPTPowerToys plugin mention to thirdPartyRunPlugins.md. Thanks [@ferraridavide](https://github.com/ferraridavide)!

### Development

 - Ported all C++/CX code to C++/WinRT.
 - Moved Version.props import to Directory.Build.props.
 - Extracted self-containment related .csproj properties to src/Common.SelfContained.props.
 - Unused and obsolete dependencies cleanup. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Extracted CSWinRT related .csproj properties to src/Common.Dotnet.CsWinRT.props.
 - Upgraded Microsoft.Windows.CsWinRT to 2.0.8 and updated verifyDepsJsonLibraryVersions.ps1 to unblock PRs.
 - Explicitly Set NuGet Audit Mode to Direct in Directory.Build.props to revert changes made with VS 17.12 update. Thanks [@snickler](https://github.com/snickler)!
 - Upgraded UnitsNet to 5.56.0.

#### What is being planned for version 0.84

For [v0.85][github-next-release-work], we'll work on the items below:

 - Stability / bug fixes
 - Language selection
 - New module: File Actions Menu
 - New module: New+

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

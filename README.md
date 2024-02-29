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
[github-next-release-work]: https://github.com/microsoft/PowerToys/issues?q=project%3Amicrosoft%2FPowerToys%2F52
[github-current-release-work]: https://github.com/microsoft/PowerToys/issues?q=project%3Amicrosoft%2FPowerToys%2F51
[ptUserX64]: https://github.com/microsoft/PowerToys/releases/download/v0.78.0/PowerToysUserSetup-0.78.0-x64.exe
[ptUserArm64]: https://github.com/microsoft/PowerToys/releases/download/v0.78.0/PowerToysUserSetup-0.78.0-arm64.exe
[ptMachineX64]: https://github.com/microsoft/PowerToys/releases/download/v0.78.0/PowerToysSetup-0.78.0-x64.exe
[ptMachineArm64]: https://github.com/microsoft/PowerToys/releases/download/v0.78.0/PowerToysSetup-0.78.0-arm64.exe
 
|  Description   | Filename | sha256 hash |
|----------------|----------|-------------|
| Per user - x64       | [PowerToysUserSetup-0.78.0-x64.exe][ptUserX64] | 120B1CEFC94D76EC593A61D717BBB2E12AF195D19E04C811F519D3F9B9B3B5C0 |
| Per user - ARM64     | [PowerToysUserSetup-0.78.0-arm64.exe][ptUserArm64] | 3C3C8A8A549ABDD1C5E5DA7DC22D254F7BBD0F9DC05DA17E51020B153662F083 |
| Machine wide - x64   | [PowerToysSetup-0.78.0-x64.exe][ptMachineX64] | 19E025381588ABAEC209CDD0A18BB779EE58FC24646D898C2A7C38A4858EAEDB |
| Machine wide - ARM64 | [PowerToysSetup-0.78.0-arm64.exe][ptMachineArm64] | 5C70054A8991885A958F066B00D7FAFE608C730FC7A99178D6C64A1F03A3C109 |

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

### 0.78 - January 2024 Update

In this release, we focused on stability and improvements.

**Highlights**

 - New languages added: Arabic (Saudi Arabia), Hebrew, Persian and Ukrainian. We are going to assume we have some bugs. We want to identify & fix them and are open for community help.
 - Many dependencies updated, aiming for security and stability.
 - Fixed commonly reported PowerToys Run startup crashes after an upgrade.
 - New settings and GPO policies to help control behavior after an upgrade. Thanks [@htcfreek](https://github.com/htcfreek)!

Here are some screenshots of the new languages:
![Arabic SA Settings screenshot](https://github.com/microsoft/PowerToys/assets/26118718/be27096d-6c03-4b09-afc4-478ca427e3ec)
![Hebrew Settings screenshot](https://github.com/microsoft/PowerToys/assets/26118718/e1435060-1f94-4e41-adee-1d0a609584ca)
![Persian Settings screenshot](https://github.com/microsoft/PowerToys/assets/26118718/8592dcb7-8a04-4831-9325-a8b9b05787df)
![Ukrainian Settings screenshot](https://github.com/microsoft/PowerToys/assets/26118718/24242dd8-eb17-4859-b2e4-1e5c63ffbffd)

### General

 - Added Arabic (Saudi Arabia) translation.
 - Added Hebrew translation.
 - Added Persian translation.
 - Added Ukrainian translation.
 - Improved the file watcher used across many utilities to consume less resources. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!

### AlwaysOnTop

 - Fixed an invisible border issue when the border color was set to the black color.
 - Added the AlwayOnTop icon to the base application executable. Thanks [@ckirby19](https://github.com/ckirby19)!

### Command Not Found

 - Signed the PowerShell scripts used by the Command Not Found installation process.

### File Explorer add-ons

 - Fixed an issue causing SVG Thumbnail generation to hang when trying to preview SVG files at the same time.

### File Locksmith

 - Improved the context menu entry caption. Thanks [@niels9001](https://github.com/niels9001)!

### Find My Mouse

 - Added more settings to tune shake detection when activating through mouse shake.

### Hosts File Editor

 - Added a feature to duplicate an entry. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!

### Installer

 - Included the new languages localization files in the installer.

### Image Resizer

 - Improved the context menu entry caption. Thanks [@niels9001](https://github.com/niels9001)!

### Peek

 - Added a missing tooltip for the file size. Thanks [@HydroH](https://github.com/HydroH)!

### PowerRename

 - Improved and added localization to the context menu entry caption. Thanks [@niels9001](https://github.com/niels9001)!

### PowerToys Run

 - Removed references to unused settings from the code, which were causing crashes on some machines. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Fixed an issue causing a scrollbar to be out of view. Thanks [@niels9001](https://github.com/niels9001)!
 - Added logic to try and detect running games to full screen detection. Thanks [@anaisbetts](https://github.com/anaisbetts)!
 - Added support for converting negative values in the Unit Converter plugin. Thanks [@Dub1shu](https://github.com/Dub1shu)!
 - Fixed stale results in the Visual Studio Code Workspaces plugin by checking if files still exist. Thanks [@anderspk](https://github.com/anderspk)!
 - Fixed an activation crash that occurred after 0.77 on some configurations.
 - Fixed a startup crash that occurred when saving the new version of settings after an upgrade.
 - You can now calculate bigger hexadecimal numbers in the Calculator plugin.
 - The "max results to show before scrolling" setting can now also be applied to the initial plugin hint listing.

### Quick Accent

 - Added the ellipses character to all languages. Thanks [@HydroH](https://github.com/HydroH)!
 - Added an option to not activate when playing a game. Thanks [@HydroH](https://github.com/HydroH)!
 - Added the E with breve and pilcrow characters to all languages. Thanks [@PesBandi](https://github.com/PesBandi)!

### Settings

 - Removed the Command Not Found listing from the Settings dashboard and flyout, since it can't really be enabled or disabled from there.
 - Added a settings and GPO rule to disable opening the What's New OOBE page after an update. Thanks [@htcfreek](https://github.com/htcfreek)!
 - Added a settings and GPO rule to disable toast notifications about new updates being available. Thanks [@htcfreek](https://github.com/htcfreek)!
 - Fixed an issue causing the Settings window to not be brought to the foreground after activating through the system tray icon.
 - Standardized accent brush and corner radius on the dashboard page.
 - Improved UI and messages for GPO locked settings. Thanks [@htcfreek](https://github.com/htcfreek)!
 - Fixed an issue causing the OOBE window to maximize and hide the system taskbar.
 - Reworked the update settings in the General page. Thanks [@htcfreek](https://github.com/htcfreek)!
 - Tweaked UI for the update settings in the General page. Thanks [@niels9001](https://github.com/niels9001)!
 - Updated the modules images in the Settings and OOBE screens. Thanks [@niels9001](https://github.com/niels9001)!
 - Updated OOBE descriptions to take into account the changes in context menu captions. Thanks [@niels9001](https://github.com/niels9001)!

### Documentation

 - Added Spotify plugin to PowerToys Run thirdPartyRunPlugins.md docs. Thanks [@waaverecords](https://github.com/waaverecords)! 
 - Added InputTyper and ClipboardManager plugins to PowerToys Run thirdPartyRunPlugins.md docs. Thanks [@CoreyHayward](https://github.com/CoreyHayward)! 
 - Added CurrencyConverter plugin to PowerToys Run thirdPartyRunPlugins.md docs. Thanks [@Advaith3600](https://github.com/Advaith3600)! 
 - Updated and cleaned up the new PowerToys plugin checklist documentation. Thanks [@Parvezkhan0](https://github.com/Parvezkhan0) and [@hlaueriksson](https://github.com/hlaueriksson)!
 - Added a documentation page to describe status code colors for Mouse Without Borders. Thanks [@ckirby19](https://github.com/ckirby19)!

### Development

 - Fixed dependency issues on upgrading .NET from 8.0.0 to 8.0.1.
 - Upgraded Microsoft.Extensions.ObjectPool from .NET 5 to .NET 8.
 - Upgraded the Windows SDK Build Tools to 10.0.22621.2428.
 - Upgraded the Windows Implementation Library to 1.0.231216.1.
 - Upgraded NLog.Schema to 5.2.8 and NLog.Extensions.Logging to 5.3.8.
 - Upgraded Markdig.Signed to 0.34.0.
 - Upgraded Microsoft.NET.Test.Sdk to 17.8.
 - Upgraded CommunityToolkit.WinUI dependencies to 8.0.240109.
 - Upgraded CommunityToolkit.Mvvm to 8.2.2. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Upgraded Windows App SDK to 1.4.4. Thanks [@snickler](https://github.com/snickler)!
 - Upgraded WPFUI version to 3.0.0-preview.13. Thanks [@niels9001](https://github.com/niels9001)!
 - Upgraded StyleCop.Analyzers to 1.2.0-beta.556. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Upgraded Microsoft.Windows.Compatibility to 8.0.1.
 - Upgraded System.Data.SqlClient to 4.8.6.
 - Consolidate XAML Namespaces across the solutions. Thanks [@Jay-o-Way](https://github.com/Jay-o-Way)!
 - Removed the toolkit labs package source reference, since the controls we were using made it to the generally available community toolkit.
 - Added Microsoft.MSBuildCache to experiment with build caching to reduce pipeline runs duration. Thanks [@dfederm](https://github.com/dfederm)!
 - Configured the release CI to follow the latest 1ES pipeline release version again.
 - Removed the copyright year from assembly information. Thanks [@Aaron-Junker](https://github.com/Aaron-Junker)!
 - Added the Command Not Found entry to the GitHub templates.
 - Removed unused code for a GPO policy to control auto updating of PowerToys. Thanks [@htcfreek](https://github.com/htcfreek)!
 - Improved code behind for getting the localization of context menu entries.
 - Locked some terms in resource files to avoid localization.

#### What is being planned for version 0.79

For [v0.79][github-next-release-work], we'll work on the items below:

 - Stability / bug fixes
 - Language selection
 - Automated UI testing through WinAppDriver
 - Develop support for Desired State Configuration
 - Modernize and refresh the UX of PowerToys based on WPF. Here's the Work in Progress preview for "Color Picker":

![ColorPicker UI refresh WIP](https://github.com/microsoft/PowerToys/assets/9866362/ceebe54b-de63-4ce7-afcb-2cd4280bf4d1)

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

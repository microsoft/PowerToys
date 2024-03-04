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
[github-next-release-work]: https://github.com/microsoft/PowerToys/issues?q=project%3Amicrosoft%2FPowerToys%2F53
[github-current-release-work]: https://github.com/microsoft/PowerToys/issues?q=project%3Amicrosoft%2FPowerToys%2F52
[ptUserX64]: https://github.com/microsoft/PowerToys/releases/download/v0.79.0/PowerToysUserSetup-0.79.0-x64.exe
[ptUserArm64]: https://github.com/microsoft/PowerToys/releases/download/v0.79.0/PowerToysUserSetup-0.79.0-arm64.exe
[ptMachineX64]: https://github.com/microsoft/PowerToys/releases/download/v0.79.0/PowerToysSetup-0.79.0-x64.exe
[ptMachineArm64]: https://github.com/microsoft/PowerToys/releases/download/v0.79.0/PowerToysSetup-0.79.0-arm64.exe
 
|  Description   | Filename | sha256 hash |
|----------------|----------|-------------|
| Per user - x64       | [PowerToysUserSetup-0.79.0-x64.exe][ptUserX64] | CF1C715F952A34416CDBE5D06D24FFF47790DDA1D4CA3F81BCAD9D28FF0039A1 |
| Per user - ARM64     | [PowerToysUserSetup-0.79.0-arm64.exe][ptUserArm64] | ADE572B6F1B59DCDC60A2550D9FD00B8CC7C78BE9330F534691CE4B056ED76F1 |
| Machine wide - x64   | [PowerToysSetup-0.79.0-x64.exe][ptMachineX64] | 3FD2A6BD9C8F8973BFBBF5DB9236C3D8AF3AE57E5AEC275DDEB5EF31581F80FE |
| Machine wide - ARM64 | [PowerToysSetup-0.79.0-arm64.exe][ptMachineArm64] | B93017C2A5CFB0DEF708DB412570AA39828E91D85E800EFD22481B46F0DC6852 |

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

### 0.79 - February 2024 Update

In this release, we focused on stability and improvements.

**Highlights**

 - New feature: Keyboard Manager allows mapping shortcuts to start applications or opening URIs. Thanks [@jefflord](https://github.com/jefflord)!
 - New feature: Keyboard Manager allows shortcuts with chords. Thanks [@jefflord](https://github.com/jefflord)!
 - Modernized Color Picker with Fluent UX. Thanks [@niels9001](https://github.com/niels9001)!
 - Peek now is able to preview drives. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - File Locksmith has now an entry in the Windows 11 tier 1 context menu.

### General

 - Refactored code so that English is used as a fallback language when a localized resource cannot be found.

### Awake

 - The setting now reverts to "Keep using the current power plan" after Awake deactivates itself after any of the timed modes has expired.

### Color Picker

 - Now uses WPFUI and the UI was updated to follow Fluent UX principles. Thanks [@niels9001](https://github.com/niels9001)!
 - Added enable and disable telemetry to align it with the other utilities.

### Command Not Found

 - Added telemetry for when a module instance is created in PowerShell.

### FancyZones

 - Fixed a memory leak occurring on work area changes.

### File Explorer add-ons

 - Added support to the .ksh, .zsh, .bsh and .env file types to Monaco previewer. Thanks [@Aaron-Junker](https://github.com/Aaron-Junker)!
 - Re-enabled the RendererAppContainer feature in WebView2, since the associated crash has been fixed in the latest WebView2 releases.

### File Locksmith

 - Added as an entry in the Windows 11 tier 1 context menu.

### Hosts File Editor

 - Tweaked filter button style to indicate if filters are applied.
 - Added an error indicator to each input field to indicate why a new entry can't be created.
 - Added an in-line delete button for each entry.

### Image Resizer

 - Units and resize modes are now localized.
 - Tweaked and improved UI. Thanks [@Jay-o-Way](https://github.com/Jay-o-Way)!

### Keyboard Manager

 - Added a feature that allows remapping a shortcut to starting an application. Thanks [@jefflord](https://github.com/jefflord)!
 - Added a feature that allows remapping a shortcut to open a URI. Thanks [@jefflord](https://github.com/jefflord)!
 - Added chords to shortcuts. Thanks [@jefflord](https://github.com/jefflord)!
 - Send telemetry about the key/shortcut to key/shortcut remappings that are set. This doesn't include remap to text, application or URI since those might contain personal information.
 - Added telemetry to send a daily event that at least a key/shortcut to key/shortcut remapping was used.
 - Tweaked and fixed the chords code to better follow conventions when trying to call the same chord multiple times.

### Mouse Without Borders
 - Fixed an issue causing the target path string to be corrupted when registering as a service.

### Paste as Plain Text

 - Prevent the start menu from activating when the Windows key is part of the activation shortcut and is released sooner.

### Peek

 - Fixed a title bar issue after maximizing Peek's window. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Fixed a crash when trying to use Peek in File Explorer alternatives.
 - Added a previewer for drives. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - The folder previewer will now asynchronously calculate size, similar to the Properties screen in File Explorer. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Added support to the .ksh, .zsh, .bsh and .env file types to Monaco previewer. Thanks [@Aaron-Junker](https://github.com/Aaron-Junker)!

### PowerRename

 - PowerRename context menu accelerator key readded.
 - Tweaked PowerRename apply button style. Thanks [@niels9001](https://github.com/niels9001)!

### PowerToys Run

 - Fixed an issue causing win32 application icons to not appear correctly in the Programs plugin.
 - Unified phrasing in the plugin descriptions.
 - Fixed an issue causing the PowerToys Run plugin settings to be cleared with each upgrade.
 - Fixed an issue causing VSCodeWorkspaces plugin to not find WSL workspaces.
 - Fixed results tooltip closing fast. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Improved the Registry plugin tooltip spacing. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Allow pressing '=' to replace the query with the current result when using the calculator plugin. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Optimized the code that gathers results from the plugin to reduce CPU consumption.
 - Optimized memory usage in the Window Walker plugin.
 - Fixed crashes and improved error handling when saving json configuration files.
 - The Program plugin will now correctly get the icon for a newly installed packaged application. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!

### Quick Accent

 - Added support for the Slovenian character set. Thanks [@aklemen](https://github.com/aklemen)!

### Registry Preview

 - Fixed a crash when closing the application and the editor's right click menu is opened.

### Settings

 - Fixed an alignment issue in the flyout icons causing some icons to be centered when they shouldn't. Thanks [@niels9001](https://github.com/niels9001)!
 - Added the mention that Monaco supports .txt files. Thanks [@Aaron-Junker](https://github.com/Aaron-Junker)!
 - Fixed an issue causing the Settings window to lose its previous maximized state. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!

### Documentation

 - Fixed broken links in doc/devdocs/readme.md. Thanks [@jem-experience](https://github.com/jem-experience)!

### Development

 - Updated Microsoft.MSBuildCache to 0.1.258-preview. Thanks [@dfederm](https://github.com/dfederm)!
 - Fixed CI to point VCToolsVersion to VC.CRT instead of VC.Redist version. Thanks [@snickler](https://github.com/snickler)!
 - Updated MSTest adapter and framework to 3.2.
 - Fixed CI by pointing WiX 3.14 urls and hashes to the latest release on GitHub.
 - Added Pro and Enterprise editions of Visual Studio to the repository's development configuration DSC scripts.
 - Updated CppWinRT to 2.0.240111.5.
 - Updated System.Drawing.Common to 8.0.2 to fix CI builds after the .NET 8.0.2 upgrade was released.
 - Updated WPFUI version to 3.0.0. Thanks [@niels9001](https://github.com/niels9001)!
 - XAML Styler is now fully tested in the solution when CI runs. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Fixed a faulty XAML binding in the Text Extractor settings page.
 - Updated Microsoft.Web.WebView2 to 1.0.2365.46.

#### What is being planned for version 0.80

For [v0.80][github-next-release-work], we'll work on the items below:

 - Stability / bug fixes
 - Language selection
 - Automated UI testing through WinAppDriver
 - Develop support for Desired State Configuration
 - New module: File Actions Menu

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

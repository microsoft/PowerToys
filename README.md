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
[github-next-release-work]: https://github.com/microsoft/PowerToys/issues?q=project%3Amicrosoft%2FPowerToys%2F54
[github-current-release-work]: https://github.com/microsoft/PowerToys/issues?q=project%3Amicrosoft%2FPowerToys%2F53
[ptUserX64]: https://github.com/microsoft/PowerToys/releases/download/v0.80.1/PowerToysUserSetup-0.80.1-x64.exe
[ptUserArm64]: https://github.com/microsoft/PowerToys/releases/download/v0.80.1/PowerToysUserSetup-0.80.1-arm64.exe
[ptMachineX64]: https://github.com/microsoft/PowerToys/releases/download/v0.80.1/PowerToysSetup-0.80.1-x64.exe
[ptMachineArm64]: https://github.com/microsoft/PowerToys/releases/download/v0.80.1/PowerToysSetup-0.80.1-arm64.exe
 
|  Description   | Filename | sha256 hash |
|----------------|----------|-------------|
| Per user - x64       | [PowerToysUserSetup-0.80.1-x64.exe][ptUserX64] | 23E35F7B33C6F24237BCA3D5E8EDF9B3BD4802DD656C402B40A4FC82670F8BE3 |
| Per user - ARM64     | [PowerToysUserSetup-0.80.1-arm64.exe][ptUserArm64] | C5EECF0D9D23AB8C14307F91CA28D2CF4DA5932D705F07AE93576C259F74B4D1 |
| Machine wide - x64   | [PowerToysSetup-0.80.1-x64.exe][ptMachineX64] | 62373A08BB8E1C1173D047509F3EA5DCC0BE1845787E07BCDA3F6A09DA2A0C17 |
| Machine wide - ARM64 | [PowerToysSetup-0.80.1-arm64.exe][ptMachineArm64] | 061EF8D1B10D68E69D04F98A2D8E1D8047436174C757770778ED23E01CC3B06C |

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

### 0.80 - March 2024 Update

In this release, we focused on stability and improvements. The next release is planned to be released during [Microsoft Build 2024](https://build.microsoft.com/) (late May).

**Highlights**

 - New feature: Desired State Configuration support, allowing the use of winget configure for PowerToys. Check the [DSC documentation](https://aka.ms/powertoys-docs-dsc-configure) for more information.
 - The Windows App SDK dependency was updated to 1.5.1, fixing many underlying UI issues.
 - WebP/WebM files support was added to Peek. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Audio files support was added to Peek. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Automated UI testing for FancyZones Editor was added to CI.

### General

 - Added a Quick Access entry to access the flyout from PowerToys' tray icon right click menu. Thanks [@pekvasnovsky](https://github.com/pekvasnovsky)!
 - Added support for Desired State Configuration in PowerToys, allowing the use of winget configure to configure many settings.

### Awake

 - Fix an issue causing the "Keep screen on" option to disable after Awake deactivated itself.

### Color Picker

 - Fixed a UI issue causing the color picker modal to hide part of the color bar. Thanks [@TheChilledBuffalo](https://github.com/TheChilledBuffalo)!

### Command Not Found

 - Now tries to find a preview version of PowerShell if no stable version is found.

### FancyZones

 - Fixed a crash loading the editor when there's a layout with an empty name in the configuration file.
 - Refactored layout internal data structures and common code to allow for automated testing.
 - The pressing of the shift key is now detected through raw input to fix an issue causing the shift key to be locked for some users.

### File Explorer add-ons

 - Fixed a crash occurring in the Monaco previewer when a file being previewed isn't found by the code behind.
 - Fixed an issue in the Markdown previewer adding a leading space to code blocks. Thanks [@Aaron-Junker](https://github.com/Aaron-Junker)!
 - Fixed wrong location and scaling of preview results on screens with different DPIs.
 - Added better clean up code to thumbnail handlers to prevent locking files.

### File Locksmith

 - Allow multiple lines to wrap when viewing the modal with selected file paths. Thanks [@sanidhyas3s](https://github.com/sanidhyas3s)!

### Installer

 - Fixed the final directory name of the PowerToys Run VSCode Workspaces plugin in the installation directory to match the plugin name. Thanks [@zetaloop](https://github.com/zetaloop)!
 - Used more generic names for the bootstrap steps, so that "Installing PowerToys" is not shown when uninstalling.

### Keyboard Manager

 - Fixed an issue that would clear out KBM mappings when certain numpad keys were used as the second key of a chord.
 - Added a comment in localization files so that translators won't translate "Text" as "SMS".

### Peek

 - Added support to .WebP/.WebM files in the image/video previewer. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Added support for audio files. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Fixed an issue causing the open file button in the title bar to be un-clickable. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Fixed an issue when previewing a folder with a dot in the name that caused Peek to try to preview it as a file. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!

### PowerToys Run

 - Added a setting to the Windows Search plugin to exclude files and patterns from the results. Thanks [@HydroH](https://github.com/HydroH)!
 - Fixed an issue showing thumbnails caused by a hash collision between similar images.
 - Added the "checkbox and multiline text box" additional property type for plugins and improved multiline text handling. Thanks [@htcfreek](https://github.com/htcfreek)!

### Quick Accent

 - Added the Schwa character to the Italian character set. Thanks [@damantioworks](https://github.com/damantioworks)!

### Registry Preview

 - Allow alternative valid names for the root keys. Thanks [@e-t-l](https://github.com/e-t-l)! 
 - Fixed an issue causing many pick file windows to be opened simultaneously. Thanks [@randyrants](https://github.com/randyrants)!

### Screen Ruler

 - Updated the measure icons for clarity. Thanks [@Aaron-Junker](https://github.com/Aaron-Junker) and [@niels9001](https://github.com/niels9001)!

### Shortcut Guide

 - Updated the Emoji shortcut that is shown to the new Windows key + period (.) hotkey.

### Text Extractor

 - Fixed issues creating the extract layout on certain monitor configurations.

### Video Conference Mute

 - Added enable/disable telemetry to get usage data.

### Settings

 - Added locks to some terms (like the name of some utilities) so that they aren't localized.
 - Fixed some shortcuts not being shown properly in the Flyout and Dashboard. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Updated image for Color Picker and outdated animations for utilities in OOBE. Thanks [@niels9001](https://github.com/niels9001)!

### Documentation

 - Added FastWeb plugin to PowerToys Run thirdPartyRunPlugins.md docs. Thanks [@CCcat8059](https://github.com/CCcat8059)!
 - Removed the old security link to MSRC from the create new issue page, since security.md is already linked there.
 - Added clarity regarding unofficial plugins to the PowerToys Run thirdPartyRunPlugins.md docs.

### Development

 - Updated System.Drawing.Common to 8.0.3 to fix CI builds after the .NET 8.0.3 upgrade was released.
 - Adjusted the GitHub action names for releasing to winget and Microsoft Store so they're clearer in the UI.
 - Upgraded WinAppSDK to 1.5.1, fixing many related issues.
 - Consolidate the WebView2 version used by WinUI 2 in the Keyboard Manager Editor.
 - Unified the use of Precompiled Headers when building on CI. Thanks [@dfederm](https://github.com/dfederm)!
 - Added UI tests for FancyZones Editor in CI.
 - Added a GitHub bot to identify possible duplicates when a new issue is created. Thanks [@craigloewen-msft](https://github.com/craigloewen-msft)!
 - Updated the WiX installer dependency to 3.14.1 to fix possible security issues.
 - Changed the pipelines to use pipeline artifacts instead of build artifacts. Thanks [@dfederm](https://github.com/dfederm)!
 - Added the -graph parameter for pipelines. Thanks [@dfederm](https://github.com/dfederm)!
 - Tests in the pipelines now run as part of the build step to save on CI time. Thanks [@dfederm](https://github.com/dfederm)!

#### What is being planned for version 0.81

For [v0.81][github-next-release-work], we'll work on the items below:

 - Stability / bug fixes
 - Language selection
 - New module: File Actions Menu

The next release is planned to be released during Microsoft Build 2024.

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

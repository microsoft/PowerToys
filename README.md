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
| [Advanced Paste](https://aka.ms/PowerToysOverview_AdvancedPaste) | [Always on Top](https://aka.ms/PowerToysOverview_AoT) | [PowerToys Awake](https://aka.ms/PowerToysOverview_Awake) |
| [Command Not Found](https://aka.ms/PowerToysOverview_CmdNotFound) | [Color Picker](https://aka.ms/PowerToysOverview_ColorPicker) | [Crop And Lock](https://aka.ms/PowerToysOverview_CropAndLock) |
| [Environment Variables](https://aka.ms/PowerToysOverview_EnvironmentVariables) | [FancyZones](https://aka.ms/PowerToysOverview_FancyZones) | [File Explorer Add-ons](https://aka.ms/PowerToysOverview_FileExplorerAddOns) |
| [File Locksmith](https://aka.ms/PowerToysOverview_FileLocksmith) | [Hosts File Editor](https://aka.ms/PowerToysOverview_HostsFileEditor) | [Image Resizer](https://aka.ms/PowerToysOverview_ImageResizer) |
| [Keyboard Manager](https://aka.ms/PowerToysOverview_KeyboardManager) | [Mouse utilities](https://aka.ms/PowerToysOverview_MouseUtilities) | [Mouse Without Borders](https://aka.ms/PowerToysOverview_MouseWithoutBorders) |
| [Peek](https://aka.ms/PowerToysOverview_Peek) | [Paste as Plain Text](https://aka.ms/PowerToysOverview_PastePlain) | [PowerRename](https://aka.ms/PowerToysOverview_PowerRename) |
| [PowerToys Run](https://aka.ms/PowerToysOverview_PowerToysRun) | [Quick Accent](https://aka.ms/PowerToysOverview_QuickAccent) | [Registry Preview](https://aka.ms/PowerToysOverview_RegistryPreview) |
| [Screen Ruler](https://aka.ms/PowerToysOverview_ScreenRuler) | [Shortcut Guide](https://aka.ms/PowerToysOverview_ShortcutGuide) | [Text Extractor](https://aka.ms/PowerToysOverview_TextExtractor) |
| [Video Conference Mute](https://aka.ms/PowerToysOverview_VideoConference) |

## Installing and running Microsoft PowerToys

### Requirements

- Windows 11 or Windows 10 version 2004 (code name 20H1 / build number 19041) or newer.
- x64 or ARM64 processor
- Our installer will install the following items:
   - [Microsoft Edge WebView2 Runtime](https://go.microsoft.com/fwlink/p/?LinkId=2124703) bootstrapper. This will install the latest version.

### Via GitHub with EXE [Recommended]

Go to the [Microsoft PowerToys GitHub releases page][github-release-link] and click on `Assets` at the bottom to show the files available in the release. Please use the appropriate PowerToys installer that matches your machine's architecture and install scope. For most, it is `x64` and per-user.

<!-- items that need to be updated release to release -->
[github-next-release-work]: https://github.com/microsoft/PowerToys/issues?q=is%3Aissue+milestone%3A%22PowerToys+0.83%22
[github-current-release-work]: https://github.com/microsoft/PowerToys/issues?q=is%3Aissue+milestone%3A%22PowerToys+0.82%22
[ptUserX64]: https://github.com/microsoft/PowerToys/releases/download/v0.82.1/PowerToysUserSetup-0.82.1-x64.exe 
[ptUserArm64]: https://github.com/microsoft/PowerToys/releases/download/v0.82.1/PowerToysUserSetup-0.82.1-arm64.exe 
[ptMachineX64]: https://github.com/microsoft/PowerToys/releases/download/v0.82.1/PowerToysSetup-0.82.1-x64.exe 
[ptMachineArm64]: https://github.com/microsoft/PowerToys/releases/download/v0.82.1/PowerToysSetup-0.82.1-arm64.exe
 
|  Description   | Filename | sha256 hash |
|----------------|----------|-------------|
| Per user - x64       | [PowerToysUserSetup-0.82.1-x64.exe][ptUserX64] | B594C9A32125079186DCE776431E2DC77B896774D2AEE2ACF51BAB8791683485 |
| Per user - ARM64     | [PowerToysUserSetup-0.82.1-arm64.exe][ptUserArm64] | 41C1D9C0E8FA7EFFCE6F605C92C143AE933F8C999A2933A4D9D1115B16F14F67 |
| Machine wide - x64   | [PowerToysSetup-0.82.1-x64.exe][ptMachineX64] | B8FA7E7C8F88B69E070E234F561D32807634E2E9D782EDBB9DC35F3A454F2264 |
| Machine wide - ARM64 | [PowerToysSetup-0.82.1-arm64.exe][ptMachineArm64] | 58F22306F22CF9878C6DDE6AC128388DF4DFF78B76165E38A695490E55B3C8C4 |

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

### 0.82 - June 2024 Update

In this release, we focused on stability and improvements.

**Highlights**

 - New feature added to PowerRename to allow using sequences of random characters and UUIDs when renaming files. Thanks [@jhirvioja](https://github.com/jhirvioja)!
 - Improvements in the Paste As JSON feature to better handle other CSV delimiters and converting from ini files. Thanks [@htcfreek](https://github.com/htcfreek)!
 - Fixed UI issues that were reported after upgrading to WPF UI on Color Picker and PowerToys Run.
 - Bug fixes and stability.

### Advanced Paste

 - Fixed an issue causing external applications triggering Advanced Paste. (This was a hotfix for 0.81)
 - Added a GPO rule to disallow using online models in Advanced Paste. (This was a hotfix for 0.81)
 - Improved CSV delimiter handling and plain text parsing for the Paste as JSON feature. Thanks [@htcfreek](https://github.com/htcfreek)!
 - Added support to convert from ini in the Paste as JSON feature. Thanks [@htcfreek](https://github.com/htcfreek)!
 - Fixed a memory leak caused by images not being properly cleaned out from clipboard history.
 - Added an option to hide the UI when it loses focus. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Improved telemetry to get better data about token usage and if clipboard history is a popular feature. Thanks [@craigloewen-msft](https://github.com/craigloewen-msft)!

### Color Picker

 - Fixed the opaque background corners in the picker that were introduced after the upgrade to WPFUI.

### Developer Files Preview (Monaco)

 - Improved the syntax highlight for .gitignore files. Thanks [@PesBandi](https://github.com/PesBandi)!
 - Checking for the sticky scroll option in code behind was being done twice. Removed one of the checks. Thanks [@downarowiczd](https://github.com/downarowiczd)!

### Environment Variables Editor

 - Added clarity to the UI section tooltips. Thanks [@anson-poon](https://github.com/anson-poon)!

### File Explorer add-ons

 - Fixed a crash when the preview handlers received a 64-bit handle from the OS. Thanks [@z4pf1sh](https://github.com/z4pf1sh)!
 - Fixed a crash when trying to update window bounds and File Explorer already disposed the preview.

### Find My Mouse

 - Added the option to have to use the Windows + Control keys to activate. Thanks [@Gentoli](https://github.com/Gentoli)!

### Hosts File Editor

 - Improved spacing definitions in the UI so that hosts name are not hidden when resizing and icons are well aligned. Thanks [@htcfreek](https://github.com/htcfreek)!
 - Changed the additional lines dialog to show the horizontal scrollbar instead of wrapping contents. Thanks [@htcfreek](https://github.com/htcfreek)!
 - Improved the duplication check's logic to improve performance and take into account features that were introduced after it. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!

### Installer

 - Fixed the remaining install failures when the folders the DSC module is to be installed in isn't accessible by the WiX installer for user scope installations.
 - Fixed an issue causing ARM 64 uninstall process to not correctly finding powershell 7 to run uninstall scripts.

### Peek

 - Prevent activating Peek when the user is renaming a file. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Added support to preview special folders like Recycle Bin and My PC instead of throwing an error.
 - Fixed a crash caused by double releasing a COM object from the module interface.

### Power Rename

 - Improved apostrophe character handling for the Capitalize and Titlecase renaming flags. Thanks [@anthonymonforte](https://github.com/anthonymonforte)!
 - Added a feature to allow using sequences of random characters or UUIDs when renaming files. Thanks [@jhirvioja](https://github.com/jhirvioja)!

### PowerToys Run

 - Improved the plugin descriptions for consistency in the UI. Thanks [@HydroH](https://github.com/HydroH)!
 - Fixed UI scaling for different dpi scenarios.
 - Fixed crash on a racing condition when updating UWP icon paths in the Program plugin. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Fixed PowerToys Run hanging when trying to close an unresponsive window in the WindowWalker plugin. Thanks [@GhostVaibhav](https://github.com/GhostVaibhav)!
 - Fixed the example in the UnitConverter description to reduce confusion with the inches abbreviation (now uses "to" instead of "in"). Thanks [@acekirkpatrick](https://github.com/acekirkpatrick)!
 - Brought the acrylic background back and applied a proper fix to the titlebar accent showing through transparency.
 - Fixed an issue causing the transparency from the UI disappearing after some time.

### Quick Accent

 - Added support for the Crimean Tatar character set. Thanks [@cor-bee](https://github.com/cor-bee)!
 - Added the Numero symbol and double acute accent character. Thanks [@PesBandi](https://github.com/PesBandi)!
 - Added the International Phonetic Alphabet characters. Thanks [@PesBandi](https://github.com/PesBandi)!
 - Fixed the character description center positioning. Thanks [@PesBandi](https://github.com/PesBandi)!
 - Added feminine and masculine ordinal indicator characters to the Portuguese character set. Thanks [@octastylos-pseudodipteros](https://github.com/octastylos-pseudodipteros)!

### Screen Ruler

 - Updated the default activation hotkey to Win+Control+Shift+M, in order to not conflict with the Windows shortcut that restores minimized windows (Win+Shift+M). Thanks [@nx-frost](https://github.com/nx-frost)!

### Settings

 - Disabled the UI to enable/disable clipboard history in the Advanced Paste settings page when clipboard history is disabled by GPO in the system. (This was a hotfix for 0.81)
 - Updated Advanced Paste's Settings and OOBE page to clarify that the AI use is optional and opt-in. (This was a hotfix for 0.81)
 - Corrected a spelling fix in Advanced Paste's settings page. Thanks [@htcfreek](https://github.com/htcfreek)!
 - Added localization support for the "Configure OpenAI Key" button in Advanced Paste's settings page. Thanks [@zetaloop](https://github.com/zetaloop)!
 - Fixed extra GPO warnings being shown in Advanced Paste's settings page even if the module is disabled. Thanks [@htcfreek](https://github.com/htcfreek)!
 - Fixed a crash when a PowerToys Run plugin icon path is badly formed.
 - Disabled the experimentation paths in code behind to improve performance, since there's no current experimentation going on.

### Documentation

 - Adjusted the readme and release notes to clarify use of AI on Advanced Paste. (This was a hotfix for 0.81)
 - Added the Edge Workspaces plugin to PowerToys Run thirdPartyRunPlugins.md docs. Thanks [@quachpas](https://github.com/quachpas)!
 - Removed the deprecated Guid plugin from PowerToys Run thirdPartyRunPlugins.md docs. Thanks [@abduljawada](https://github.com/abduljawada)!
 - Added the PowerHexInspector plugin to PowerToys Run thirdPartyRunPlugins.md docs. Thanks [@NaroZeol](https://github.com/NaroZeol)!
 - Fixed a broken link in the communication-with-modules.md file. Thanks [@PesBandi](https://github.com/PesBandi)! 
 - Updated COMMUNITY.md with missing and former members.

### Development

 - Fixed ci UI tests to point to the correct Visual Studio vstest location after a Visual Studio upgrade. (This was a hotfix for 0.81)
 - Updated System.Drawing.Common to 8.0.6 to fix CI builds after the .NET 8.0.6 upgrade was released.
 - Removed an incorrect file reference to long removed documentation from the solution file. Thanks [@Kissaki](https://github.com/Kissaki)!
 - Upgraded Windows App SDK to 1.5.3.
 - Removed use of the BinaryFormatter API from Mouse Without Borders, which is expected to be deprecated in .NET 9.
 - The user scope installer is now sent to the Microsoft store instead of the machine scope installer.
 - Refactored Mouse Jump's internal code to allow for a future introduction of customizable appearance features. Thanks [@mikeclayton](https://github.com/mikeclayton)!
 - Removed a noisy error from spell check ci runs.
 - Improved the ci agent pool selection code.
 - Updated Xamlstyler.console to 3.2404.2. Thanks [@Jvr2022](https://github.com/Jvr2022)!
 - Updated UnitsNet to 5.50.0 Thanks [@Jvr2022](https://github.com/Jvr2022)!
 - Replaced LPINPUT with std::vector of INPUT instances in Keyboard Manager internal code. Thanks [@masaru-iritani](https://github.com/masaru-iritani)!
 - Improved the Microsoft Store submission ci action to use the proper cli and authentication.

#### What is being planned for version 0.83

For [v0.83][github-next-release-work], we'll work on the items below:

 - Stability / bug fixes
 - New utility: Dev Projects
 - Language selection
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

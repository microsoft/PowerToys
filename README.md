# Microsoft PowerToys

![Hero image for Microsoft PowerToys](doc/images/overview/PT_hero_image.png)

[How to use PowerToys][usingPowerToys-docs-link] | [Downloads & Release notes][github-release-link] | [Contributing to PowerToys](#contributing) | [What's Happening](#whats-happening) | [Roadmap](#powertoys-roadmap)

## About

Microsoft PowerToys is a set of utilities for power users to tune and streamline their Windows experience for greater productivity. For more info on [PowerToys overviews and how to use the utilities][usingPowerToys-docs-link], or any other tools and resources for [Windows development environments](https://learn.microsoft.com/windows/dev-environment/overview), head over to [learn.microsoft.com][usingPowerToys-docs-link]!

|              | Current utilities: |              |
|--------------|--------------------|--------------|
| [Advanced Paste](https://aka.ms/PowerToysOverview_AdvancedPaste) | [Always on Top](https://aka.ms/PowerToysOverview_AoT) | [PowerToys Awake](https://aka.ms/PowerToysOverview_Awake) |
| [Command Not Found](https://aka.ms/PowerToysOverview_CmdNotFound) | [Color Picker](https://aka.ms/PowerToysOverview_ColorPicker) | [Crop And Lock](https://aka.ms/PowerToysOverview_CropAndLock) |
| [Environment Variables](https://aka.ms/PowerToysOverview_EnvironmentVariables) | [FancyZones](https://aka.ms/PowerToysOverview_FancyZones) | [File Explorer Add-ons](https://aka.ms/PowerToysOverview_FileExplorerAddOns) |
| [File Locksmith](https://aka.ms/PowerToysOverview_FileLocksmith) | [Hosts File Editor](https://aka.ms/PowerToysOverview_HostsFileEditor) | [Image Resizer](https://aka.ms/PowerToysOverview_ImageResizer) |
| [Keyboard Manager](https://aka.ms/PowerToysOverview_KeyboardManager) | [Mouse utilities](https://aka.ms/PowerToysOverview_MouseUtilities) | [Mouse Without Borders](https://aka.ms/PowerToysOverview_MouseWithoutBorders) |
| [New+](https://aka.ms/PowerToysOverview_NewPlus) | [Peek](https://aka.ms/PowerToysOverview_Peek) | [Paste as Plain Text](https://aka.ms/PowerToysOverview_PastePlain) |
| [PowerRename](https://aka.ms/PowerToysOverview_PowerRename) | [PowerToys Run](https://aka.ms/PowerToysOverview_PowerToysRun) | [Quick Accent](https://aka.ms/PowerToysOverview_QuickAccent) |
| [Registry Preview](https://aka.ms/PowerToysOverview_RegistryPreview) | [Screen Ruler](https://aka.ms/PowerToysOverview_ScreenRuler) | [Shortcut Guide](https://aka.ms/PowerToysOverview_ShortcutGuide) |
| [Text Extractor](https://aka.ms/PowerToysOverview_TextExtractor) | [Workspaces](https://aka.ms/PowerToysOverview_Workspaces) | [ZoomIt](https://aka.ms/PowerToysOverview_ZoomIt) |

## Installing and running Microsoft PowerToys

### Requirements

- Windows 11 or Windows 10 version 2004 (code name 20H1 / build number 19041) or newer.
- x64 or ARM64 processor
- Our installer will install the following items:
   - [Microsoft Edge WebView2 Runtime](https://go.microsoft.com/fwlink/p/?LinkId=2124703) bootstrapper. This will install the latest version.

### Via GitHub with EXE [Recommended]

Go to the [Microsoft PowerToys GitHub releases page][github-release-link] and click on `Assets` at the bottom to show the files available in the release. Please use the appropriate PowerToys installer that matches your machine's architecture and install scope. For most, it is `x64` and per-user.

<!-- items that need to be updated release to release -->
[github-next-release-work]: https://github.com/microsoft/PowerToys/issues?q=is%3Aissue+milestone%3A%22PowerToys+0.89%22
[github-current-release-work]: https://github.com/microsoft/PowerToys/issues?q=is%3Aissue+milestone%3A%22PowerToys+0.88%22
[ptUserX64]: https://github.com/microsoft/PowerToys/releases/download/v0.88.0/PowerToysUserSetup-0.88.0-x64.exe 
[ptUserArm64]: https://github.com/microsoft/PowerToys/releases/download/v0.88.0/PowerToysUserSetup-0.88.0-arm64.exe 
[ptMachineX64]: https://github.com/microsoft/PowerToys/releases/download/v0.88.0/PowerToysSetup-0.88.0-x64.exe 
[ptMachineArm64]: https://github.com/microsoft/PowerToys/releases/download/v0.88.0/PowerToysSetup-0.88.0-arm64.exe
 
|  Description   | Filename | sha256 hash |
|----------------|----------|-------------|
| Per user - x64       | [PowerToysUserSetup-0.88.0-x64.exe][ptUserX64] | 5BBA2E06603CAAE0269DFBC991095C6664FD934130335197C1BA3120E19B7CA3 |
| Per user - ARM64     | [PowerToysUserSetup-0.88.0-arm64.exe][ptUserArm64] | E79723F9F94068C699E01334C8CC0C85F37818EB4664FC772D2B545A1C37C3FA |
| Machine wide - x64   | [PowerToysSetup-0.88.0-x64.exe][ptMachineX64] | C43742DB7AA3F8B01FE7AE1DA591F0342767AFE5BBACB72F2968CE5E8EE1E3AC |
| Machine wide - ARM64 | [PowerToysSetup-0.88.0-arm64.exe][ptMachineArm64] | AEE4A67643C886336F31F86C4117BA5F01BCA5E0E99FF34524217DC91AFA7132 |

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

### 0.88 - January 2025 Update

In this release, we focused on new features, stability, and improvements.

**Highlights**

 - New utility: ZoomIt - a screen zoom, annotation, and recording tool for technical presentations and demos. This utility from Sysinternals has had its source code released and included in PowerToys. ZoomIt will still continue to be updated and shipped by Sysinternals for users who prefer to have it as a standalone utility outside of PowerToys. Thanks [@markrussinovich](https://github.com/markrussinovich), [@foxmsft](https://github.com/foxmsft) and [@johnstep](https://github.com/johnstep) for contributing the original code and reviewing the PowerToys integration!
 - Video Conference Mute has been deprecated and was removed from PowerToys.
 - .Net 9.0.1 fixed many issue in WPF, improving stability for PowerToys Run.

### General
 - Applied a workaround for the Windows App SDK applications title bar override that was causing accent color to not be shown on the top bar of applications on Windows 10. Thanks [@pingzing](https://github.com/pingzing)!
 - Improved the "admin application running" notification checking logic to be less demanding on resources. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Fixed an issue causing many utilities to crash when the GPO to disable data diagnostics was applied.

### Advanced Paste

 - Fixed a crash when the application was exiting. (This was a hotfix for 0.87)
 - Added a Json format validation step to verify if a conversion to Json should be applied.
 - Fixed accessibility issues when using a screen reader.
 - Added support for all BitmapDecoder supported image file types to the Image to Text functionality. Thanks [@daverayment](https://github.com/daverayment)!
 - Fixed an issue causing Advanced Paste initialization errors to hang the PowerToys main process.

### FancyZones

 - Removed Workspaces Editor from the exclusions list so it can be snapped by FancyZones.

### Keyboard Manager

 - Added an option to make a shortcut remapping only trigger with exact modifiers.

### Monaco Preview

 - Added support for .resx and .resw files in Peek and File Explorer add-ons. Thanks [@asif4318](https://github.com/asif4318)!
 - Added a setting to make the code minimap toggle-able in Peek and File Explorer add-ons. Thanks [@PesBandi](https://github.com/PesBandi)!
 - Fixed an issue causing Json format preview setting to not be applied correctly.
 - Fixed an issue causing the wrong Monaco assets to be used at runtime.

### Mouse Without Borders

 - Fixed an issue causing clipboard to stop working after going through a UAC screen when using the Service mode. Thanks [@YDKK](https://github.com/YDKK)!

### New+

 - Fixed an issue causing New+ to override the New file or folder creation from the File Explorer Ribbon buttons or keyboard shortcuts on Windows 10.
 - When creating file or folders through a template, they should now have the current time as the last modified date. Thanks [@cgaarden](https://github.com/cgaarden)!

### Peek

 - Fixed an issue causing Peek to not appear if it was previously minimized. Thanks [@asif4318](https://github.com/asif4318)!

### PowerToys Run

 - Fixed a transparent border issue on Windows 10. (This was a hotfix for 0.87)
 - Fixed a crash in the OneNote plugin after the .Net 9 update. (This was a hotfix for 0.87)
 - Fixed an issue causing the Calculator plugin to return division by zero errors when dividing by hexadecimal numbers. Thanks [@plante-msft](https://github.com/plante-msft)!
 - Updated the Calculator plugin Mages library to 3.0.0 and added support for the random integer function. Thanks [@htcfreek](https://github.com/htcfreek)!
 - Improved handling of non-base 10 numbers to add support for binary and octal numbers in the Calculator plugin. Thanks [@PesBandi](https://github.com/PesBandi)!
 - Added a setting to enable selection of which units to use for trigonometric functions. Thanks [@OldUser101](https://github.com/OldUser101)!
 - Fixed a .NET 9 regression causing the PowerToys Run dialog to not be draggable. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Added context menu buttons for the VS Code Workspaces plugin, for copying the path, opening in File Explorer or in Console. Thanks [@programming-with-ia](https://github.com/programming-with-ia)!
 - Added some telemetry to gather data on which hotkey is used to trigger PowerToys Run.
 - Removed the workarounds that were in place to fix some WPF issues that were fixed in .NET 9.0.1.
 - Fixed a typo in the Value Generator plugin messages. Thanks [@OldUser101](https://github.com/OldUser101)!

### Quick Accent

 - Added the ć character to the Slovenian character set. Thanks [@dsoklic](https://github.com/dsoklic)!
 - Added the Proto-Indo-European character set. 

### Registry Preview

 - Fixed an issue causing line breaks to not be parsed correctly for REG_MULTI_SZ values. Thanks [@htcfreek](https://github.com/htcfreek)!
 - Added a tooltip to values to show multiple lines of data. Thanks [@htcfreek](https://github.com/htcfreek)!
 - Added a context menu to enable copying type, value and key paths. Thanks [@htcfreek](https://github.com/htcfreek)!

### Settings

 - Made the Advanced Paste paste OpenAI configuration modal scrollable.
 - Fixed the text on the Quick Accent page to refer to "character sets" instead of "character set". Thanks [@PesBandi](https://github.com/PesBandi)!
 - Added the plugin's dll file version and website to the PowerToys Run plugin settings. Thanks [@htcfreek](https://github.com/htcfreek)!
 - Added the Workspaces file to the list of files that gets backed up by the Back up / Restore functionality.
 - Fixed an issue causing some of the selected character sets to be unselected when opening the character set expander in the Quick Accent page.
 - Improved GPO logic, icons, info bar layout and enabled state of all modules settings pages. Thanks [@htcfreek](https://github.com/htcfreek)!
 - Fixed some accessibility issues and refactored and improved quality of the code related to image sizes in the Image Resizer page. Thanks [@daverayment](https://github.com/daverayment)!
 - Fixed mentions of "Backup" to "Back up" when it should be used as a verb. Thanks [@JackStuart](https://github.com/JackStuart)!
 - Added a "New" label to Settings to better highlight new utilities that get released. Thanks [@niels9001](https://github.com/niels9001) for the UI tweaks!

### Text Extractor

 - Fixed many accessibility and UI issues on the overlay UI. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!

### Workspaces

 - Fixed an issue causing the Workspaces Editor to start outside of visible desktop area.
 - Fixed an issue to maintain command line arguments for applications when trying using the "Launch and Edit" feature.

### Video Conference Mute

 - The module has been deprecated in 0.88.0, being removed from PowerToys.

### ZoomIt

 - New utility: Zoom It - a screen zoom, annotation, and recording tool for technical presentations and demos. This utility from Sysinternals has had its source code released and included in PowerToys. ZoomIt will still continue to be updated and shipped by Sysinternals for users who prefer to have it as a standalone utility outside of PowerToys. Thanks [@markrussinovich](https://github.com/markrussinovich), [@foxmsft](https://github.com/foxmsft) and [@johnstep](https://github.com/johnstep) for contributing the original code and reviewing the PowerToys integration!

### Documentation

 - Updated the PowerToys Run documentation to reflect documentation pages for new plugins.
 - Added YubicoOauthOTP plugin mention to thirdPartyRunPlugins.md. Thanks [@dlnilsson](https://github.com/dlnilsson)!

### Development

 - Added fuzz testing for AdvancedPaste, with a new pipeline for OneFuzz.
 - Added a new CI pipeline to build with the latest WindowsAppSDK.
 - Added a new CI pipeline to build with the latest webview2 from Edge Canary.
 - Made the HostsUILib project AOT compatible. Thanks [@snickler](https://github.com/snickler) for your help reviewing this!
 - Made FilePreviewCommon and MarkdownPreviewHandler AOT compatible. Thanks [@snickler](https://github.com/snickler) for your help reviewing this!
 - Made the PowerAccent.Core project AOT compatible. Thanks [@snickler](https://github.com/snickler) for your help reviewing this!
 - Cleaned up some code for AOT compatibility in the Advanced Paste module. Thanks [@snickler](https://github.com/snickler) for your help reviewing this!
 - Removed the prerelease flag from the PowerToys development DSC configurations. Thanks [@denelon](https://github.com/denelon)!
 - Improved Dart CI reliability by improving error messages and retrying to the step that installs the correct dotnet version.
 - Improved Dart CI reliability by fixing retries when downloading the localization files.
 - Improved Dart CI build times by removing the steps to build the no longer needed abstracted utility nuget packages.
 - Removed the solution.props file from the solution root.
 - Fixed PowerToys Run Calculator plugin tests when running in systems with different number formats. Thanks [@htcfreek](https://github.com/htcfreek)!
 - Updated many .NET packages from .NET 9.0.0 to 9.0.1 for security fixes. Thanks [@snickler](https://github.com/snickler)!
 - Refactored the Mouse Without Borders Common.Log.cs and Common.Receiver.cs files. Thanks [@mikeclayton](https://github.com/mikeclayton)!

#### What is being planned for version 0.89

For [v0.89][github-next-release-work], we'll work on the items below:

 - Stability / bug fixes
 - New module: File Actions Menu
 - PowerToys Run v2 development work

## PowerToys Community

The PowerToys team is extremely grateful to have the [support of an amazing active community][community-link]. The work you do is incredibly important. PowerToys wouldn’t be nearly what it is today without your help filing bugs, updating documentation, guiding the design, or writing features. We want to say thank you and take time to recognize your work.  Month by month, you directly help make PowerToys a better piece of software.

## Code of Conduct

This project has adopted the [Microsoft Open Source Code of Conduct][oss-conduct-code].

## Privacy Statement

The application logs basic diagnostic data (telemetry). For more information on privacy and what we collect, see our [PowerToys Data and Privacy documentation](https://aka.ms/powertoys-data-and-privacy-documentation).

[oss-CLA]: https://cla.opensource.microsoft.com
[oss-conduct-code]: CODE_OF_CONDUCT.md
[community-link]: COMMUNITY.md
[github-release-link]: https://aka.ms/installPowerToys
[microsoft-store-link]: https://aka.ms/getPowertoys
[winget-link]: https://github.com/microsoft/winget-cli#installing-the-client
[roadmap]: https://github.com/microsoft/PowerToys/wiki/Roadmap
[privacy-link]: http://go.microsoft.com/fwlink/?LinkId=521839
[loc-bug]: https://github.com/microsoft/PowerToys/issues/new?assignees=&labels=&template=translation_issue.md&title=
[usingPowerToys-docs-link]: https://aka.ms/powertoys-docs

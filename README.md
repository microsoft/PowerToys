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
| [Crop And Lock](https://aka.ms/PowerToysOverview_CropAndLock) | [Environment Variables](https://aka.ms/PowerToysOverview_EnvironmentVariables) | [FancyZones](https://aka.ms/PowerToysOverview_FancyZones) |
| [File Explorer Add-ons](https://aka.ms/PowerToysOverview_FileExplorerAddOns) | [File Locksmith](https://aka.ms/PowerToysOverview_FileLocksmith) | [Hosts File Editor](https://aka.ms/PowerToysOverview_HostsFileEditor) |
| [Image Resizer](https://aka.ms/PowerToysOverview_ImageResizer) | [Keyboard Manager](https://aka.ms/PowerToysOverview_KeyboardManager) | [Mouse utilities](https://aka.ms/PowerToysOverview_MouseUtilities) |
| [Mouse Without Borders](https://aka.ms/PowerToysOverview_MouseWithoutBorders) | [Peek](https://aka.ms/PowerToysOverview_Peek) | [Paste as Plain Text](https://aka.ms/PowerToysOverview_PastePlain) |
| [PowerRename](https://aka.ms/PowerToysOverview_PowerRename) | [PowerToys Run](https://aka.ms/PowerToysOverview_PowerToysRun) | [Quick Accent](https://aka.ms/PowerToysOverview_QuickAccent) |
| [Registry Preview](https://aka.ms/PowerToysOverview_RegistryPreview) | [Screen Ruler](https://aka.ms/PowerToysOverview_ScreenRuler) | [Shortcut Guide](https://aka.ms/PowerToysOverview_ShortcutGuide) |
| [Text Extractor](https://aka.ms/PowerToysOverview_TextExtractor) | [Video Conference Mute](https://aka.ms/PowerToysOverview_VideoConference) |

## Installing and running Microsoft PowerToys

### Requirements

- Windows 11 or Windows 10 version 2004 (code name 20H1 / build number 19041) or newer.
- Our installer will install the following items:
   - [Microsoft Edge WebView2 Runtime](https://go.microsoft.com/fwlink/p/?LinkId=2124703) bootstrapper. This will install the latest version.

### Via GitHub with EXE [Recommended]

Go to the [Microsoft PowerToys GitHub releases page][github-release-link] and click on `Assets` at the bottom to show the files available in the release. Please use the appropriate PowerToys installer that matches your machine's architecture and install scope. For most, it is `x64` and per-user.

<!-- items that need to be updated release to release -->
[github-next-release-work]: https://github.com/microsoft/PowerToys/issues?q=project%3Amicrosoft%2FPowerToys%2F49
[github-current-release-work]: https://github.com/microsoft/PowerToys/issues?q=project%3Amicrosoft%2FPowerToys%2F48
[ptUserX64]: https://github.com/microsoft/PowerToys/releases/download/v0.75.1/PowerToysUserSetup-0.75.1-x64.exe
[ptUserArm64]: https://github.com/microsoft/PowerToys/releases/download/v0.75.1/PowerToysUserSetup-0.75.1-arm64.exe
[ptMachineX64]: https://github.com/microsoft/PowerToys/releases/download/v0.75.1/PowerToysSetup-0.75.1-x64.exe
[ptMachineArm64]: https://github.com/microsoft/PowerToys/releases/download/v0.75.1/PowerToysSetup-0.75.1-arm64.exe

|  Description   | Filename | sha256 hash |
|----------------|----------|-------------|
| Per user - x64       | [PowerToysUserSetup-0.75.1-x64.exe][ptUserX64] | CFDAE52607689A695F4E4DDD7C1FE68400359AEF0D2B23C86122835E9D32A20F |
| Per user - ARM64     | [PowerToysUserSetup-0.75.1-arm64.exe][ptUserArm64] | 9BAD3EF71DEDE70445416AC7369D115FAE095152722BC4F23EE393D8A10F45CA |
| Machine wide - x64   | [PowerToysSetup-0.75.1-x64.exe][ptMachineX64] | 18FEB9377B0BA45189FFF4F89627B152DD794CCC15F005592B34A40A3EA62EA8 |
| Machine wide - ARM64 | [PowerToysSetup-0.75.1-arm64.exe][ptMachineArm64] | F5CDF5A35876A0B581F446BF728B7AC52B6B701C0850D9CEA9A1874523745CFD |

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

### 0.75 - October 2023 Update

In this release, we focused on new features, stability and improvements.

**Highlights**

 - New utility: An environment variables editor with the functionality to configure profiles that can be enabled/disabled. Thanks [@niels9001](https://github.com/niels9001) for the design and UI work that made this possible!
 - Settings has a new Dashboard home page, with quick access for enabling modules, short descriptions and activation methods. Thanks [@niels9001](https://github.com/niels9001) for the design and UI work that made this possible!
 - Added a previewer to Peek that hosts File Explorer previewers to support every file type that a machine is currently able to preview. For example, this means that if Microsoft Office handlers are installed, Peek can preview Office files. Thanks [@dillydylann](https://github.com/dillydylann)!

### General

 - Many typo fixes through the projects and documentation. Thanks [@brianteeman](https://github.com/brianteeman)!
 - Refactored and improved the logic across utilities for bringing a window to the foreground after activation.

### Color Picker

 - After activating Color Picker, it's now possible to cancel the session by clicking the right mouse button. Thanks [@fredso90](https://github.com/fredso90)!

### Environment Variables
 - Added a new utility: An environment variables editor that has the functionality to configure profiles that can be enabled/disabled. Thanks [@niels9001](https://github.com/niels9001) for the design and UI work that made this possible!
 - Shows in the title bar if it's running as an administrator. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!

### FancyZones

 - Fixed an issue causing context menu pop-ups from some apps to automatically snap to a zone. (This was a hotfix for 0.74)
 - Applied the fix for the context menu pop-ups to the logic that decides which windows can be snapped.
 - Reworked the "Keep windows in their zones" option to include the work area and turn it on by default, fixing an incompatibility with the Copilot flyout.
 - Fixed an issue causing windows to be snapped while moving to a different virtual desktop.

### File Explorer add-ons

 - Fixed an issue blocking some SVG files from being previewed correctly. (This was a hotfix for 0.74)
 - Fixed crashes on invalid files in the STL Thumbnail generator.

### GPO

 - Added a global GPO rule that applies for all utilities unless it's overridden. Thanks [@htcfreek](https://github.com/htcfreek)!
 - Added GPO rules to control which PowerToys Run plugins should be enabled/disabled by policy. Thanks [@htcfreek](https://github.com/htcfreek)!
   * All plugins have to provide its plugin ID as static property in its Main method.

### Image Resizer

 - Fixed wrong .bmp file association in the registry. Thanks [@meitinger](https://github.com/meitinger)!

### Keyboard Manager

 - Visually distinguish between the Numpad and regular period characters in the UI.
 - This utility is now disabled by default on new installations, since it requires user configuration to affect keyboard behavior.
 - Fixed a typo in the Numpad Subtract key in the editor.

### Mouse Highlighter

 - Removed the lower limit of fade delay and duration, to allow better signaling of doing a double click. Thanks [@fredso90](https://github.com/fredso90)!

### Mouse Jump

 - The process now runs in the background, for a faster activation time. Thanks [@mikeclayton](https://github.com/mikeclayton)!

### Peek

 - Reported file sizes will now more closely match what's reported by File Explorer. Thanks [@Deepak-Sangle](https://github.com/Deepak-Sangle)!
 - Added a previewer that hosts File Explorer previewers to support every file type that a machine is currently able to preview. Thanks [@dillydylann](https://github.com/dillydylann)!
 - Fixed an issue causing the preview of the first file to be stuck loading. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Fixed showing the previously previewed video file when invoking Peek with a new file. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Added the wrap and file formatting options to the Monaco previewer. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!

### PowerRename

 - Save data from the last run in a different file to avoid conflicting with changing settings in the Settings application.

### PowerToys Run

 - Fixed a case where the query wasn't being cleared after invoking a result action through the keyboard. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Improved the shell selection option for Windows Terminal in the Shell plugin and improved the backend code for adding combo box options to plugins. Thanks [@htcfreek](https://github.com/htcfreek)!
   * The implementation of the combo box items has changed and isn't backward compatible. (Old plugins won't crash, but the combo box setting isn't shown in settings ui anymore.)
 - Added Unix time in milliseconds, fixed negative unix time input and improved error messages in the TimeDate plugin. Thanks [@htcfreek](https://github.com/htcfreek)!
 - The PowerToys plugin allows calling the new Environment Variables utility. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Refactored and added support to VSCodium Stable, VSCodium Insider and Remote Tunnels workspaces. Thanks [@eternalphane](https://github.com/eternalphane)!

### Quick Accent

 - Fixed characters that were removed from "All languages" because they were not in any single language. (This was a hotfix for 0.74)
 - Added Asturian characters to the Spanish character set. Thanks [@blakestack](https://github.com/blakestack)!
 - Added Greek characters with tonos. Thanks [@PesBandi](https://github.com/PesBandi)! 

### Registry Preview

 - Fixed a parsing error that crashed the Application. (This was a hotfix for 0.74)
 - Fixed opening file names with non-ASCII characters. Thanks [@randyrants](https://github.com/randyrants)!
 - Fixed wrong parsing when the file contained an assignment with spaces around the equals sign. Thanks [@randyrants](https://github.com/randyrants)!
 - Fixed key transversal issues when a key was a substring of a parent key. Thanks [@randyrants](https://github.com/randyrants)!

### Runner

 - Fixed the update notification toast to show a Unicode arrow. Thanks [@TheJoeFin](https://github.com/TheJoeFin)!

### Settings

 - Added a new Dashboard home page, with quick access for enabling modules, short descriptions and activation methods. Thanks [@niels9001](https://github.com/niels9001) for the design and UI work that made this possible!
 - Fixed a typo in the Hosts File Editor page. Thanks [@Deepak-Sangle](https://github.com/Deepak-Sangle)!
 - Added a lock icon to the flyout listing of all modules when its enabled state is controlled by policy.
 - The "All apps" list in the flyout will now list all apps even if their enabled state is controlled by policy.

### Video Conference Mute

 - Added an option to allow for the toolbar to hide after some time passed. Thanks [@quyenvsp](https://github.com/quyenvsp)!
 - Added an option to select to mute or unmute at startup. Thanks [@quyenvsp](https://github.com/quyenvsp)!
 - Fixed an issue causing a cascade of mute/unmute triggers.

### Documentation

 - Updated the Group Policy documentation on learn.microsoft.com, removed the Group Policy documentation from the repository and linked to the published documentation on learn.microsoft.com instead.

### Development

 - Added project dependencies to the version project and headers to avoid building errors. Thanks [@johnterickson](https://github.com/johnterickson)!
 - Enabled Control Flow Guard in the C++ projects. Thanks [@DHowett](https://github.com/DHowett)!
 - Switched the release pipeline to the 1ES governed template. Thanks [@DHowett](https://github.com/DHowett)!
 - Styled XAML files and added a XAML Style checker to the solution, with a CI action to check if code being contributed is compliant. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Suppressed redundant midl file warnings in PowerRename.
 - Add unit tests to FancyZones Editor. Thanks [@garv5014](https://github.com/garv5014), [@andrewbengordon](https://github.com/andrewbengordon) and [@Cwighty](https://github.com/Cwighty)!
 - Improved the Default Layouts internal structure in FancyZones Editor. Thanks [@garv5014](https://github.com/garv5014)!
 - Fixed code issues to allow building in Visual Studio 17.8 Preview 4.

#### What is being planned for version 0.76

For [v0.76][github-next-release-work], we'll work on the items below:

 - Language selection
 - .NET 8 upgrade
 - Allowing Keyboard Manager to output arbitrary Unicode sequences
 - Automated UI testing through WinAppDriver
 - Modernize and refresh the UX of PowerToys based on WPF. Here's Work in Progress previews for the modules "PowerToys Run" and "Color Picker":

![PowerToys Run UI refresh WIP](https://github.com/microsoft/PowerToys/assets/9866362/16903bcb-c18e-49fb-93ca-738b81957055)

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

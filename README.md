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
[github-next-release-work]: https://github.com/microsoft/PowerToys/issues?q=is%3Aissue+milestone%3A%22PowerToys+0.84%22
[github-current-release-work]: https://github.com/microsoft/PowerToys/issues?q=is%3Aissue+milestone%3A%22PowerToys+0.83%22
[ptUserX64]: https://github.com/microsoft/PowerToys/releases/download/v0.83.0/PowerToysUserSetup-0.83.0-x64.exe 
[ptUserArm64]: https://github.com/microsoft/PowerToys/releases/download/v0.83.0/PowerToysUserSetup-0.83.0-arm64.exe 
[ptMachineX64]: https://github.com/microsoft/PowerToys/releases/download/v0.83.0/PowerToysSetup-0.83.0-x64.exe 
[ptMachineArm64]: https://github.com/microsoft/PowerToys/releases/download/v0.83.0/PowerToysSetup-0.83.0-arm64.exe
 
|  Description   | Filename | sha256 hash |
|----------------|----------|-------------|
| Per user - x64       | [PowerToysUserSetup-0.83.0-x64.exe][ptUserX64] | C78E24F21C611F2BD774D8460ADD4B9AC8519085CA1253941CB46129331AB8C8 |
| Per user - ARM64     | [PowerToysUserSetup-0.83.0-arm64.exe][ptUserArm64] | BA1C16003D55587D523A41B960D4A03718123CA37577D5F2A75E151D7653E6D3 |
| Machine wide - x64   | [PowerToysSetup-0.83.0-x64.exe][ptMachineX64] | 7EC435A10849187D21A383E56A69213C1FF110B7FECA65900D9319D2F8162F35 |
| Machine wide - ARM64 | [PowerToysSetup-0.83.0-arm64.exe][ptMachineArm64] | 5E147424D1D12DFCA88DC4AA0657B7CC1F3B02812F1EBA3E564FAF691908D840 |

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

### 0.83 - July 2024 Update

In this release, we focused on stability and improvements.

**Highlights**

 - Awake Quality of Life changes, including changing the tray icon to reflect the current mode. Thanks [@dend](https://github.com/dend)!
 - Changes to general GPO policies and new policies for Mouse Without Borders. The names for some intune policy configuration sets might need to be updated as seen in https://github.com/MicrosoftDocs/windows-dev-docs/pull/5045/files . Thanks [@htcfreek](https://github.com/htcfreek)!

### General
 - Reordered GPO policies, making it easier to find some policies. Thanks [@htcfreek](https://github.com/htcfreek)!

### Advanced Paste

 - Fixed CSV parser to support double quotes and escape delimiters when pasting as JSON. Thanks [@GhostVaibhav](https://github.com/GhostVaibhav)!
 - Improved double quote handling in the CSV parser when pasting as JSON. Thanks [@htcfreek](https://github.com/htcfreek)!

### Awake

 - Different modes will now show different icons in the system tray. Thanks [@dend](https://github.com/dend), and [@niels9001](https://github.com/niels9001) for the icon design!
 - Removed the dependency on Windows Forms and used native Win32 APIs instead for the tray icon. Thanks [@dend](https://github.com/dend) and [@BrianPeek](https://github.com/BrianPeek)!
 - Fixed an issue where the UI would become non-responsive after selecting no time for the timed mode. Thanks [@dend](https://github.com/dend)!
 - Refactored code for easier maintenance. Thanks [@dend](https://github.com/dend)!
 - The tray icon will now be shown when running Awake standalone to signal mode. Thanks [@dend](https://github.com/dend)!
 - The tray icon tooltip shows how much time is left on the timer. Thanks [@dend](https://github.com/dend)!
 - Added DPI awareness to the tray icon context menu. Thanks [@dend](https://github.com/dend)!

### Color Picker

 - Added support to using the mouse wheel to scroll through the color history. Thanks [@Fefedu973](https://github.com/Fefedu973)!

### File Explorer add-ons

 - Allow copying from the right-click menu in Monaco and Markdown previewers.

### File Locksmith

 - Fixed a crash when there were a big number of entries being shown by moving the opened files of a process to another dialog.

### Installer

 - Fixed the path where DSC module files were installed for the user-scope installer. (This was a hotfix for 0.82)

### Mouse Without Borders

 - Disabled non supported options in the old Mouse Without Borders UI. Thanks [@htcfreek](https://github.com/htcfreek)!
 - Added new GPO policies to control the use of some features. Thanks [@htcfreek](https://github.com/htcfreek)!

### Peek

 - Allow copying from the right-click menu in Dev files and Markdown previews.

### PowerToys Run

 - Fixed a crash on Windows 11 build 22000. (This was a hotfix for 0.82)
 - Blocked a transparency fix code from running on Windows 10, since it was causing graphical glitches. (This was a hotfix for 0.82)
 - Accept speed abbreviations like kilometers per hour (kmph) in the Unit Converter plugin. Thanks [@GhostVaibhav](https://github.com/GhostVaibhav)!
 - Added settings to configure behavior of the "First week of year" and "First day of week" calculations in the DateTime plugin. Thanks [@htcfreek](https://github.com/htcfreek)!
 - Fixed wrong initial position of the PowerToys Run when switching between monitors with different dpi values.
 - Started allowing interchangeable use of / and \ in the registry plugin paths.
 - Added support to automatic sign-in after rebooting with the System plugin. Thanks [@htcfreek](https://github.com/htcfreek)!
 - Added suggested use example results to the Value Generator plugin. Thanks [@azlkiniue](https://github.com/azlkiniue)!

### Quick Accent

 - Added support for the Bulgarian character set. Thanks [@octastylos-pseudodipteros](https://github.com/octastylos-pseudodipteros)!

### Runner

 - Add code to handle release tags with an upper V when trying to detect new updates. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!

### Settings

 - Fixed the UI spacing in the "update available" card. Thanks [@Agnibaan](https://github.com/Agnibaan)!
 - Fixed the information bars in the Mouse Without Borders settings page to hide when the module is disabled. Thanks [@htcfreek](https://github.com/htcfreek)!
 - Improved consistency of the icons used in the Mouse Without Borders settings page. Thanks [@htcfreek](https://github.com/htcfreek)!
 - Improved action keyword information bar padding in the PowerToys Run plugins section. Thanks [@htcfreek](https://github.com/htcfreek)!
 - Fixed a crash in the dashboard when Keyboard Manager Editor settings file became locked.

### Documentation

 - Added the RDP plugin to PowerToys Run thirdPartyRunPlugins.md docs. Thanks [@anthony81799](https://github.com/anthony81799)!
 - Added the GitHubRepo and ProcessKiller plugins to PowerToys Run thirdPartyRunPlugins.md docs. Thanks [@8LWXpg](https://github.com/8LWXpg)!
 - Fixed a typo in the 0.82.0 release notes in README. Thanks [@walex999](https://github.com/walex999)!

### Development

 - Disabled FancyZone UI tests, to unblock PRs. We plan to bring them back in the future. (This was a hotfix for 0.82)
 - Fixed an issue where flakiness in CI was causing the installer custom actions DLL from being signed. (This was a hotfix for 0.82)
 - Upgraded the Microsoft.Windows.Compatibility dependency to 8.0.7.
 - Upgraded the System.Text.Json dependency to 8.0.4.
 - Upgraded the Microsoft.Data.Sqlite dependency to 8.0.7.
 - Upgraded the MSBuildCache dependency to 0.1.283-preview. Thanks [@dfederm](https://github.com/dfederm)!
 - Removed an unneeded /Zm compiler flag from Keyboard Manager Editor common build flags.
 - Fixed the winget publish action to handle upper case V in the tag name. Thanks [@mdanish-kh](https://github.com/mdanish-kh)!
 - Removed wildcard items from vcxproj files. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Removed the similar issues bot GitHub actions. Thanks [@craigloewen-msft](https://github.com/craigloewen-msft)!
 - Fixed CODEOWNERS to better protect changes in some files.
 - Switched machines being used in CI and pointed status badges in README to the new machines.
 - Fixed NU1503 build warnings when building PowerToys. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Use the MSTest meta dependency for running the tests instead of the individual testing packages. Thanks [@stan-sz](https://github.com/stan-sz)!
 - Added missing CppWinRT references.

#### What is being planned for version 0.84

For [v0.84][github-next-release-work], we'll work on the items below:

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

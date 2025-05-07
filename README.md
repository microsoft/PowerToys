# Microsoft PowerToys

![Hero image for Microsoft PowerToys](doc/images/overview/PT_hero_image.png)

[How to use PowerToys][usingPowerToys-docs-link] | [Downloads & Release notes][github-release-link] | [Contributing to PowerToys](#contributing) | [What's Happening](#whats-happening) | [Roadmap](#powertoys-roadmap)

## About

Microsoft PowerToys is a set of utilities for power users to tune and streamline their Windows experience for greater productivity. For more info on [PowerToys overviews and how to use the utilities][usingPowerToys-docs-link], or any other tools and resources for [Windows development environments](https://learn.microsoft.com/windows/dev-environment/overview), head over to [learn.microsoft.com][usingPowerToys-docs-link]!

|              | Current utilities: |              |
|--------------|--------------------|--------------|
| [Advanced Paste](https://aka.ms/PowerToysOverview_AdvancedPaste) | [Always on Top](https://aka.ms/PowerToysOverview_AoT) | [PowerToys Awake](https://aka.ms/PowerToysOverview_Awake) |
| [Color Picker](https://aka.ms/PowerToysOverview_ColorPicker) | [Command Not Found](https://aka.ms/PowerToysOverview_CmdNotFound) | [Command Palette](https://aka.ms/PowerToysOverview_CmdPal) |
| [Crop And Lock](https://aka.ms/PowerToysOverview_CropAndLock) | [Environment Variables](https://aka.ms/PowerToysOverview_EnvironmentVariables) | [FancyZones](https://aka.ms/PowerToysOverview_FancyZones) |
| [File Explorer Add-ons](https://aka.ms/PowerToysOverview_FileExplorerAddOns) | [File Locksmith](https://aka.ms/PowerToysOverview_FileLocksmith) | [Hosts File Editor](https://aka.ms/PowerToysOverview_HostsFileEditor) |
| [Image Resizer](https://aka.ms/PowerToysOverview_ImageResizer) | [Keyboard Manager](https://aka.ms/PowerToysOverview_KeyboardManager) | [Mouse utilities](https://aka.ms/PowerToysOverview_MouseUtilities) |
| [Mouse Without Borders](https://aka.ms/PowerToysOverview_MouseWithoutBorders) | [New+](https://aka.ms/PowerToysOverview_NewPlus) | [Paste as Plain Text](https://aka.ms/PowerToysOverview_PastePlain) |
| [Peek](https://aka.ms/PowerToysOverview_Peek) | [PowerRename](https://aka.ms/PowerToysOverview_PowerRename) | [PowerToys Run](https://aka.ms/PowerToysOverview_PowerToysRun) |
| [Quick Accent](https://aka.ms/PowerToysOverview_QuickAccent) | [Registry Preview](https://aka.ms/PowerToysOverview_RegistryPreview) | [Screen Ruler](https://aka.ms/PowerToysOverview_ScreenRuler) |
| [Shortcut Guide](https://aka.ms/PowerToysOverview_ShortcutGuide) | [Text Extractor](https://aka.ms/PowerToysOverview_TextExtractor) | [Workspaces](https://aka.ms/PowerToysOverview_Workspaces) |
| [ZoomIt](https://aka.ms/PowerToysOverview_ZoomIt) |

## Installing and running Microsoft PowerToys

### Requirements

- Windows 11 or Windows 10 version 2004 (code name 20H1 / build number 19041) or newer.
- x64 or ARM64 processor
- Our installer will install the following items:
   - [Microsoft Edge WebView2 Runtime](https://go.microsoft.com/fwlink/p/?LinkId=2124703) bootstrapper. This will install the latest version.

### Via GitHub with EXE [Recommended]

Go to the [Microsoft PowerToys GitHub releases page][github-release-link] and click on `Assets` at the bottom to show the files available in the release. Please use the appropriate PowerToys installer that matches your machine's architecture and install scope. For most, it is `x64` and per-user.

<!-- items that need to be updated release to release -->
[github-next-release-work]: https://github.com/microsoft/PowerToys/issues?q=is%3Aissue+milestone%3A%22PowerToys+0.92%22
[github-current-release-work]: https://github.com/microsoft/PowerToys/issues?q=is%3Aissue+milestone%3A%22PowerToys+0.91%22
[ptUserX64]: https://github.com/microsoft/PowerToys/releases/download/v0.90.0/PowerToysUserSetup-0.91.0-x64.exe 
[ptUserArm64]: https://github.com/microsoft/PowerToys/releases/download/v0.90.0/PowerToysUserSetup-0.91.0-arm64.exe 
[ptMachineX64]: https://github.com/microsoft/PowerToys/releases/download/v0.90.0/PowerToysSetup-0.91.0-x64.exe 
[ptMachineArm64]: https://github.com/microsoft/PowerToys/releases/download/v0.90.0/PowerToysSetup-0.91.0-arm64.exe
 
|  Description   | Filename | sha256 hash |
|----------------|----------|-------------|
| Per user - x64       | [PowerToysUserSetup-0.91.0-x64.exe][ptUserX64] | TBD |
| Per user - ARM64     | [PowerToysUserSetup-0.91.0-arm64.exe][ptUserArm64] | TBD |
| Machine wide - x64   | [PowerToysSetup-0.91.0-x64.exe][ptMachineX64] | TBD |
| Machine wide - ARM64 | [PowerToysSetup-0.91.0-arm64.exe][ptMachineArm64] | TBD |

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

### 0.91 - May 2025 Update

In this release, we focused on new features, stability, and automation.

**✨Highlights**

![Gif for Command Palette](doc/images/overview/CmdPal_Hero.gif)

- New module: Command Palette ("CmdPal") -  Created as the evolution of PowerToys Run with extensibility at the forefront, Command Palette is a quick launcher with a richer display and additional capabilities without sacrificing performance, allowing you to start anything with the shortcut **Win+Alt+Space**! Thanks [@zadjii-msft](https://github.com/zadjii-msft), [@niels9001](https://github.com/niels9001), [@michael-hawker](https://github.com/michael-hawker), [@joadoumie](https://github.com/joadoumie), [@plante-msft](https://github.com/plante-msft), [@ethanfangg](https://github.com/ethanfangg) and [@krschau](https://github.com/krschau)!
 - Enhanced the Color Picker by switching from WPF UI to .NET WPF, resulting in improved themes and visual consistency across different modes. Thanks [@mantaionut](https://github.com/mantaionut)! Thanks [@Jay-o-Way](https://github.com/Jay-o-Way) and [@niels9001](https://github.com/niels9001) for helping with the review!
 - Added the ability to delete files directly from Peek, enhancing file management efficiency. Thanks [@daverayment](https://github.com/daverayment) and thanks [@htcfreek](https://github.com/htcfreek) for the review!
 - Added support for variables in template filenames, enabling dynamic elements like date components and environment variables for enhanced customization in New+. Thanks [@cgaarden](https://github.com/cgaarden)!

### Advanced Paste

 - Fixed an issue where Advanced Paste failed to create the OCR engine for certain English language tags (e.g., en-CA) by initializing the OCR engine with the user profile language. Thanks [@cryolithic](https://github.com/cryolithic)!

### Color Picker

 - Fixed an issue where a resource leak caused hangs or crashes by properly disposing of the Graphics object. Thanks [@dcog989](https://github.com/dcog989)!
 - Fixed an issue where Color Picker exited on Backspace keypress by ensuring it only closes when focused and aligning Escape/Backspace behavior. Thanks [@PesBandi](https://github.com/PesBandi)!

### Command Not Found

 - Updated the WinGet Command Not Found script to only enable the experimental features if they actually exist. Thanks [@carlos-zamora](https://github.com/carlos-zamora)!

### Command Palette

- Updated bug template to include Command Palette module.
- Fixed an issue where the toast window was not scaled for DPI, causing layout issues under display scaling. Thanks [@zadjii-msft](https://github.com/zadjii-msft)!
- Fixed an issue where Up/Down keyboard navigation didn't move selection when caret was at position 0, and add continuous navigation like PT Run v1. Thanks [@davidegiacometti](https://github.com/davidegiacometti)! Thanks [@zadjii-msft](https://github.com/zadjii-msft) for helping with the review!
- Updated the Time and Date extension code to simplify it and improve clarity.
- Fixed an issue where capitalization in the command causes failure when trying to go to the mouse pointer, resolved by adjusting the command to lowercase. Thanks [@zadjii-msft](https://github.com/zadjii-msft) for helping with the review!
- Added the ability to open URLs directly in the browser from Command Palette. Thanks [@htcfreek](https://github.com/htcfreek) and [@zadjii-msft](https://github.com/zadjii-msft) for helping with the review!
- Added setting to enable/disable system tray icon in CmdPal and align terminology with Windows 11. Thanks [@davidegiacometti](https://github.com/davidegiacometti)! Thanks [@htcfreek](https://github.com/htcfreek) and [@zadjii-msft](https://github.com/zadjii-msft) for helping with the review!
- Fixed an alias update issue by removing the old alias when a new one is set. Thanks [@zadjii-msft](https://github.com/zadjii-msft) for review!
- Resolved GitHub casing conflict by migrating Exts and exts into a new ext directory, ensuring consistent structure across platforms and preventing path fragmentation. Thanks [@zadjii-msft](https://github.com/zadjii-msft)!
- Fix an issue where the 'Create New Extension' command generated empty file names. Thanks [@zadjii-msft](https://github.com/zadjii-msft) for review!
- Added the ability to make the global hotkey a low-level keyboard hook. Thanks [@zadjii-msft](https://github.com/zadjii-msft)!
- Added support for JUMBO thumbnails, enabling access to high-resolution icons. Thanks [@zadjii-msft](https://github.com/zadjii-msft)!

### Image Resizer

 - Fixed an issue where deleting an Image Resizer preset removed the wrong preset.Thanks [@daverayment](https://github.com/daverayment) for your help reviewing this!

### Keyboard Manager

 - Fixed an issue where a modifier key, when set without specifying left or right, would get stuck due to incorrect key handling, by tracking the pressed keys and sending the correct key accordingly.Thanks [@mantaionut](https://github.com/mantaionut)!

### PowerRename

 - Enhanced PowerRename's time formatting capabilities by adding 12-hour time format patterns with AM/PM support. Thanks [@bitmap4](https://github.com/bitmap4)!

### PowerToys Run

 - Added support for custom formats in the "Time and Date" plugin and improves error messages for invalid input formats. Thanks [@htcfreek](https://github.com/htcfreek)!
 - Fix two crashes: one for WFT on very early dates and another for calculating the week of the month on very late dates (e.g., 31.12.9999), and reorder UI settings. Thanks [@htcfreek](https://github.com/htcfreek)!
 - Fix an issue where capitalization in the command causes failure when trying to go to the mouse pointer, resolved by adjusting the command to lowercase.Thanks [@zadjii-msft](https://github.com/zadjii-msft) for helping with the review!
 - Added version details to plugin error messages for 'Loading error' and 'Init error'. Thanks [@htcfreek](https://github.com/htcfreek)!

### Quick Accent

 - Updated the letter mapping in GetDefaultLetterKeyEPO, replacing "ǔ" with "ŭ" for the VK_U key to accurately reflect Esperanto phonetics. Thanks [@OlegKharchevkin](https://github.com/OlegKharchevkin)!
 - Fixed an issue where Quick Accent did not work properly when using the on-screen keyboard. Thanks [@davidegiacometti](https://github.com/davidegiacometti)! Thanks [@jaysondapo](https://github.com/jaysondapo) for helping with the review!

### Registry Preview

 - Enhanced Registry Preview to support pasting registry keys and values without manually writing the file header, and added a new button for resetting the app. Thanks [@htcfreek](https://github.com/htcfreek)! Thanks [@randyrants](https://github.com/randyrants) for helping with the review!

### Settings

 - Fix an issue where the Settings app randomly showed a blank icon in the taskbar by deferring icon assignment until the window is activated.
 - Added the ability to maximize the "What's New" window for a more comfortable reading experience.Thanks [@Jay-o-Way](https://github.com/Jay-o-Way) for review!

### Workspaces

 - Fixed an issue where Steam games were not captured or launched correctly by updating window filtering and integrating Steam URL protocol handling.

### Documentation

 - Added QuickNotes to the third-party plugins documentation for PowerToys Run. Thanks [@ruslanlap](https://github.com/ruslanlap)!
 - Added Weather and Pomodoro plugins to the PowerToys Run third-party plugin documentation. Thanks [@ruslanlap](https://github.com/ruslanlap)!

### Development

 - Updated GitHub Action to install .NET 9 for MSStore release support.
 - Updated version placeholder in bug_report.yml to prevent incorrect v0.70.0 versioning in issue reports. Thanks [@htcfreek](https://github.com/htcfreek) for review!
 - Updated GitHub Action to upgrade actions/setup-dotnet from version 3 to version 4 for MSStore release.
 - Added securityContext to WinGet configuration files, allowing invocation from user context and prompting a single UAC for elevated resources in a separate process. Thanks [@mdanish-kh](https://github.com/mdanish-kh)! Thanks [@denelon](https://github.com/denelon) for review!
 - Changed log file extensions from .txt to .log to support proper file associations and tooling compatibility, and added logs for Workspace. Thanks [@benwa](https://github.com/benwa)! Thanks [@Jay-o-Way](https://github.com/Jay-o-Way) for review!
 - Upgraded testing framework dependencies and aligned package versions across components.
 - Upgraded dependencies to fix vulnerabilities.
 - Enhanced repository security by pinning GitHub Actions and Docker tags to immutable full-length commits and integrating automated dependency vulnerability scanning via Dependency Review Workflow. Thanks [@Nick2bad4u](https://github.com/Nick2bad4u)!
 - Upgraded Boost dependencies to a newer version.
 - Upgraded toolkit to the latest version, suppressed AOT-related warnings.
  
### Tool/General

 - Added support for automating bug report creation by generating a pre-filled GitHub issue URL with system and diagnostic information. Thanks [@donlaci](https://github.com/donlaci)! Thanks [@htcfreek](https://github.com/htcfreek) for review!
   
### What is being planned for version 0.92

For [v0.92][github-next-release-work], we'll work on the items below:

 - New module: File Actions Menu
 - New UI Automation tests
 - Working on installer upgrades
 - Upgrading Keyboard Manager's editor UI
 - Stability / bug fixes

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

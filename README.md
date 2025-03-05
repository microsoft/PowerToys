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
[github-next-release-work]: https://github.com/microsoft/PowerToys/issues?q=is%3Aissue+milestone%3A%22PowerToys+0.90%22
[github-current-release-work]: https://github.com/microsoft/PowerToys/issues?q=is%3Aissue+milestone%3A%22PowerToys+0.89%22
[ptUserX64]: https://github.com/microsoft/PowerToys/releases/download/v0.89.0/PowerToysUserSetup-0.89.0-x64.exe 
[ptUserArm64]: https://github.com/microsoft/PowerToys/releases/download/v0.89.0/PowerToysUserSetup-0.89.0-arm64.exe 
[ptMachineX64]: https://github.com/microsoft/PowerToys/releases/download/v0.89.0/PowerToysSetup-0.89.0-x64.exe 
[ptMachineArm64]: https://github.com/microsoft/PowerToys/releases/download/v0.89.0/PowerToysSetup-0.89.0-arm64.exe
 
|  Description   | Filename | sha256 hash |
|----------------|----------|-------------|
| Per user - x64       | [PowerToysUserSetup-0.89.0-x64.exe][ptUserX64] | B4F130CC96F321024A257499247F6FF6DA56612215ED3882E868AAE26C689E33 |
| Per user - ARM64     | [PowerToysUserSetup-0.89.0-arm64.exe][ptUserArm64] | F69B00F4E520EB09FA0D1D1669E21910C5225FE7A2EEDC0FA7C283B201A5F9C6 |
| Machine wide - x64   | [PowerToysSetup-0.89.0-x64.exe][ptMachineX64] | E18AC8F9023E341CF7DAD35367FB9DDDB6565D83D8155DBCDDB40AE8A24AE731 |
| Machine wide - ARM64 | [PowerToysSetup-0.89.0-arm64.exe][ptMachineArm64] | 17DEADEC601D6061D7AF4F487595CC36D9191813003CC2ECE381017F0EC71FBB |

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

### 0.89 - February 2025 Update

In this release, we focused on new features, stability, accessibility and automation.

**✨Highlights**

 - Enhanced Advanced Paste by adding media transcoding support to convert different video and audio file formats! Thanks [@snickler](https://github.com/snickler) for your help!
 - Fixed crashes when loading thumbnails after the .NET 9 update and resolved PowerLauncher.exe blocking other MSI installers from creating shortcuts!
 - Fixed accessibility issues across FancyZones, Image Resizer, and Settings to improve screen reader support and clarity!
 - Enhanced UI automation framework across modules and added new tests to cover manual checks, with more improvements coming!

### General

 - Fixed an issue where updating PowerToys on Windows 11 did not properly update context menu entries, impacting New+, PowerRename, Image Resizer, and File Locksmith.
 - Updated .NET Packages from 9.0.1 to 9.0.2. Thanks [@snickler](https://github.com/snickler) for this.
 - Enabled compatibility with VS17.3 and later, for C++23. Thanks [@LNKLEO](https://github.com/LNKLEO) for this.

### Advanced Paste

 - Added media transcoding support to convert different video and audio file formats, improved UI layouts, refined clipboard handling, and integrated Semantic Kernel for smarter pasting. Thanks [@snickler](https://github.com/snickler) for your help!

### FancyZones

 - Fixed accessibility by improving the text for monitors, ensuring clearer naming and help text for screen readers.

### Image Resizer
 - Fixed issues with Width and Height fields in Image Resizer's Custom preset, ensuring empty values no longer cause errors, settings save correctly, and auto-scaling behaves as expected. Thanks [@daverayment](https://github.com/daverayment)!
 - Fixed accessibility by ensuring screen readers announce selected image dimensions in the combo-box for better navigation.

### Monaco Preview

 - Fixed open link in default browser rather than Microsoft Edge. Thanks [@OldUser101](https://github.com/OldUser101)!

### Mouse Highlighter

 - Fixed a highlight released on an Administrator window will start fading, instead of staying on the screen indefinitely until the mouse button is pressed again on an unelevated window.

### Mouse Without Borders
 - Fixed an issue in service mode where copy-paste and drag-drop file transfers didn’t work, ensuring seamless file operations.
 - Enabled GPO for enable/disable for Mouse Without Borders in Service Mode. Thanks [@htcfreek](https://github.com/htcfreek) for review and comments!
 - Fixed code maintainability by refactoring the oversized 'Common' class in Mouse Without Borders into smaller, focused classes for better structure and clarity. Thanks [@mikeclayton](https://github.com/mikeclayton) and thanks [@htcfreek](https://github.com/htcfreek) for review!

### PowerRename
 - Supported negative value as Start value in regular expression, e.g. ${start=-1314}
 - Enhanced RegEx help by adding $, ^, quantifiers, and common patterns for better usability. Thanks [@PesBandi](https://github.com/PesBandi) and thanks [@htcfreek](https://github.com/htcfreek) for review.

### PowerToys Run
 - Fixed crashes when loading thumbnails after the .NET 9 update by disabling CETCompat.
 - Fixed PowerLauncher.exe blocking other MSI installers creating shortcuts. Thanks [@OneBlue](https://github.com/OneBlue)!
 - Fixed Run’s dark mode detection to work reliably, preventing issues with incorrect theme detection and ensuring a smoother user experience. Thanks [@daverayment](https://github.com/daverayment)!
 - Fixed list separator handling in Calculator, allowing functions with multiple arguments to work correctly across different locales. For example pow(2;3) would be replaced with pow(2,3). Thanks [@PesBandi](https://github.com/PesBandi) and thanks [@htcfreek](https://github.com/htcfreek) for review!
 - Fixed angle unit conversions in the PowerToys Run calculator, allowing quick conversions between radians, degrees, and gradians. Thanks [@OldUser101](https://github.com/OldUser101)!

### Quick Accent

 - Added ǎ, ǒ and ǔ to the IPA character set. Thanks [@PesBandi](https://github.com/PesBandi)!
 - Added ` (backtick) and ~ (tilde) to the VK_OEM_5 character set. Thanks [@xanatos](https://github.com/xanatos)!
 - Added ς (final sigma) to the Greek character set. Thanks [@IamSmeagol](https://github.com/IamSmeagol)!

### Settings

 - Enabled GPO for the "run at startup" setting. Thanks [@htcfreek](https://github.com/htcfreek) for review and comments!
 - Fixed accessibility issue by allowing screen readers to announce the group name for secondary links in Settings pages, instead of reading link descriptions without context.
 - Fixed an issue where the Color Picker shortcut was not displaying correctly in the Dashboard.

### Workspaces

 - Fixed if a window was last placed on a disconnected monitor, it launches minimized and repositions within the main monitor's visible area when restored, instead of remaining off-screen and invisible.
 - Fixed on ARM64 to correctly display icons for packaged apps by resolving path mismatches.

### ZoomIt

 - Fixed warning C4706 and related error C2220 during build. Thanks [@xanatos](https://github.com/xanatos)!

### Documentation

 - Fixed runner-ipc.md doc on the broken link. Thanks [@daverayment](https://github.com/daverayment)!
 - Fixed the new plugin checklist by updating the target framework, removing duplicates, and improving statement organization. Thanks [@hlaueriksson](https://github.com/hlaueriksson)!
 - Updated runner documentation to align with the latest code structure.

### Development

 - Stabilized pipeline on ARM64 and forked build.
 - Added fuzz testing for HostUILib, added as part of pipeline for OneFuzz.
 - Fixed and improved UI-Test automation framework, and added new test cases for the FancyZones and Hosts module.
 - Optimized Logger function as AOT compatible, improving performance by 18%.
 - Made Common.UI and Setting.UI to be AOT compatible.
 
### What is being planned for version 0.90

For [v0.90][github-next-release-work], we'll work on the items below:

 - New module: PowerToys Run v2
 - New module: File Actions Menu
 - Working on installer upgrades
 - Upgrading keyboard manager's editor UI
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

# Microsoft PowerToys

### ITI - SA

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
[github-next-release-work]: https://github.com/microsoft/PowerToys/issues?q=is%3Aissue+milestone%3A%22PowerToys+0.93%22
[github-current-release-work]: https://github.com/microsoft/PowerToys/issues?q=is%3Aissue+milestone%3A%22PowerToys+0.92%22
[ptUserX64]: https://github.com/microsoft/PowerToys/releases/download/v0.92.1/PowerToysUserSetup-0.92.1-x64.exe 
[ptUserArm64]: https://github.com/microsoft/PowerToys/releases/download/v0.92.1/PowerToysUserSetup-0.92.1-arm64.exe 
[ptMachineX64]: https://github.com/microsoft/PowerToys/releases/download/v0.92.1/PowerToysSetup-0.92.1-x64.exe 
[ptMachineArm64]: https://github.com/microsoft/PowerToys/releases/download/v0.92.1/PowerToysSetup-0.92.1-arm64.exe
 
|  Description   | Filename |
|----------------|----------|
| Per user - x64       | [PowerToysUserSetup-0.92.1-x64.exe][ptUserX64] |
| Per user - ARM64     | [PowerToysUserSetup-0.92.1-arm64.exe][ptUserArm64] |
| Machine wide - x64   | [PowerToysSetup-0.92.1-x64.exe][ptMachineX64] |
| Machine wide - ARM64 | [PowerToysSetup-0.92.1-arm64.exe][ptMachineArm64] |

This is our preferred method.

### Via Microsoft Store

Install from the [Microsoft Store's PowerToys page][microsoft-store-link]. You must be using the [new Microsoft Store](https://blogs.windows.com/windowsExperience/2021/06/24/building-a-new-open-microsoft-store-on-windows-11/), which is available for both Windows 11 and Windows 10.

### Via WinGet
Download PowerToys from [WinGet][winget-link]. Updating PowerToys via winget will respect the current PowerToys installation scope. To install PowerToys, run the following command from the command line / PowerShell:

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

This project welcomes contributions of all types. Besides coding features / bug fixes, other ways to assist include spec writing, design, documentation, and finding bugs. We are excited to work with the power user community to build a set of tools for helping you get the most out of Windows.

We ask that **before you start work on a feature that you would like to contribute**, please read our [Contributor's Guide](CONTRIBUTING.md). We would be happy to work with you to figure out the best approach, provide guidance and mentorship throughout feature development, and help avoid any wasted or duplicate effort.

Most contributions require you to agree to a [Contributor License Agreement (CLA)][oss-CLA] declaring that you grant us the rights to use your contribution and that you have permission to do so.

For guidance on developing for PowerToys, please read the [developer docs](./doc/devdocs) for a detailed breakdown. This includes how to setup your computer to compile.

## What's Happening

### PowerToys Roadmap

Our [prioritized roadmap][roadmap] of features and utilities that the core team is focusing on.

### 0.92 - June 2025 Update

In this release, we focused on new features, stability, optimization improvements, and automation.

**✨Highlights**

 - PowerToys settings now has a toggle for the system tray icon, giving users control over its visibility based on personal preference. Thanks [@BLM16](https://github.com/BLM16)!  
 - Command Palette now has Ahead-of-Time ([AOT](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot)) compatibility for all first-party extensions, improved extensibility, and core UX fixes, resulting in better performance and stability across commands.
 - Color Picker now has customizable mouse button actions, enabling more personalized workflows by assigning functions to left, right, and middle clicks. Thanks [@PesBandi](https://github.com/PesBandi)!
 - Bug Report Tool now has a faster and clearer reporting process, with progress indicators, improved compression, auto-cleanup of old trace logs, and inclusion of MSIX installer logs for more efficient diagnostics.
 - File Explorer add-ons now have improved rendering stability, resolving issues with PDF previews, blank thumbnails, and text file crashes during file browsing.

### Color Picker

 - Added mouse button actions so you can choose what left, right, or middle click does. Thanks [@PesBandi](https://github.com/PesBandi)!  

### Crop & Lock

 - Aligned window styling with current Windows theme for a cleaner look. Thanks [@sadirano](https://github.com/sadirano)!  

### Command Palette

 - Enhanced performance by resolving a regression in page loading.
 - Applied consistent hotkey handling across all Command Palette commands for a smoother user experience.
 - Improved graceful closing of Command Palette. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Fixed consistency issue for extensions' alias with "Direct" setting and enabled localization for "Direct" and "Indirect" for better user understanding. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Improved visual clarity by styling critical context items correctly.
 - Automatically focused the field when only one is present on the content page.
 - Improved stability and efficiency when loading file icons in SDK ThumbnailHelper.cs by removing unnecessary operations. Thanks [@OldUser101](https://github.com/OldUser101)!
 - Enhanced details view with commands implementation. (See [Extension sample](./src/modules/cmdpal/ext/SamplePagesExtension/Pages/SampleListPageWithDetails.cs))

### Command Palette extensions

 - Added "Copy Path" command to *App* search results for convenience. Thanks [@PesBandi](https://github.com/PesBandi)!
 - Improved *Calculator* input experience by ignoring leading equal signs. Thanks [@PesBandi](https://github.com/PesBandi)!
 - Corrected input handling in the *Calculator* extension to avoid showing errors for input with only leading whitespace.
 - Improved *New Extension* wizard by validating names to prevent namespace errors.
 - Ensured consistent context items display for the *Run* extension between fallback and top-level results.
 - Fixed missing *Time & Date* commands in fallback results. Thanks [@htcfreek](https://github.com/htcfreek)!
 - Fixed outdated results in the *Time & Date* extension. Thanks [@htcfreek](https://github.com/htcfreek)!
 - Fixed an issue where *Web Search* always opened Microsoft Edge instead of the user's default browser on Windows 11 24H2 and later. Thanks [@RuggMatt](https://github.com/RuggMatt)!
 - Improved ordering of *Windows Settings* extension search results from alphabetical to relevance-based for quicker access.
 - Added "Restart Windows Explorer" command to the *Windows System Commands* provider for gracefully terminate and relaunch explorer.exe. Thanks [@jiripolasek](https://github.com/jiripolasek)!

### Command Palette Ahead-of-Time (AOT) readiness

 - We’ve made foundational changes to prepare the Command Palette for future Ahead-of-Time (AOT) publishing. This includes replacing the calculator library with ExprTk, improving COM object handling, refining Win32 interop, and correcting trimming behavior—all to ensure compatibility, performance, and reliability under AOT constraints. All first-party extensions are now AOT-compatible. These improvements lay the groundwork for publishing Command Palette as an AOT application in the next release.
 - Special thanks to [@Sergio0694](https://github.com/Sergio0694) for guidance on making COM APIs AOT-compatible, [@jtschuster](https://github.com/jtschuster) for fixing COM object handling, [@ArashPartow](https://github.com/ArashPartow) from ExprTk for integration suggestions, and [@tian-lt](https://github.com/tian-lt) from the Windows Calculator team for valuable suggestion throughout the migration journey and review.
 - As part of the upcoming release, we’re also enabling AOT compatibility for key dependencies, including markdown rendering, Adaptive Cards, internal logging and telemetry library, and the core Command Palette UX.

### FancyZones

 - Fixed DPI-scaling issues to ensure FancyZones Editor displays crisply on high-resolution monitors. Thanks [@HO-COOH](https://github.com/HO-COOH)! This inspired us a broader review across other PowerToys modules, leading to DPI display optimizations in Awake, Color Picker, PowerAccent, and more.

### File Explorer add-ons

 - Fixed potential failures in PDF previewer and thumbnail generation, improving reliability when browsing PDF files. Thanks [@mohiuddin-khan-shiam](https://github.com/mohiuddin-khan-shiam)!  
 - Prevented Monaco Preview Handler crash when opening UTF-8-BOM text files.  

### Hosts File Editor

 - Added an in-app *“Learn more”* link to warning dialogs for quick guidance. Thanks [@PesBandi](https://github.com/PesBandi)!  

### Mouse Without Borders

 - Fixed firewall rule so MWB now accepts connections from IPs outside your local subnet.
 - Cleaned legacy logs to reduce disk usage and noise.  

### Peek

 - Updated QOI reader so 3-channel QOI images preview correctly in Peek and File Explorer. Thanks [@mbartlett21](https://github.com/mbartlett21)!  
 - Added codec detection with a clear warning when a video can’t be previewed, along with a link to the Microsoft Store to download the required codec.

### PowerRename

 - Added support for $YY-$MM-$DD in ModificationTime and AccessTime to enable flexible date-based renaming.

### PowerToys Run

 - Suppressed error UI for known WPF-related crashes to reduce user confusion, while retaining diagnostic logging for analysis. This targets COMException 0xD0000701 and 0x80263001 caused by temporary DWM unavailability.

### Registry Preview

 - Added "Extended data preview" via magnifier icon and context menu in the Data Grid, enabled easier inspection of complex registry types like REG_BINARY, REG_EXPAND_SZ, and REG_MULTI_SZ, etc. Thanks [@htcfreek](https://github.com/htcfreek)! 
 - Improved file-saving experience in Registry Preview by aligning with Notepad-like behavior, enhancing user prompts, error handling, and preventing crashes during unsaved or interrupted actions. Thanks [@htcfreek](https://github.com/htcfreek)!  

### Settings

 - Added an option to hide or show the PowerToys system tray icon. Thanks [@BLM16](https://github.com/BLM16)!
 - Improved settings to show progress while a bug report package is being generated.

### Workspaces

 - Stored Workspaces icons in user AppData to ensure profile portability and prevent loss during temporary folder cleanup.
 - Enabled capture and launch of PWAs on non-default Edge or Chrome profiles, ensuring consistent behavior during creation and execution.

### Documentation

 - Added SpeedTest and Dictionary Definition to the third-party plugins documentation for PowerToys Run. Thanks [@ruslanlap](https://github.com/ruslanlap)!
 - Corrected sample links and typo in Command Palette documentation. Thanks [@daverayment](https://github.com/daverayment) and [@roycewilliams](https://github.com/roycewilliams)!

### Development

 - Updated .NET libraries to 9.0.6 for performance and security. Thanks [@snickler](https://github.com/snickler)!  
 - Updated WinAppSDK to 1.7.2 for better stability and Windows support.  
 - Introduced a one-step local build script that generates a signed installer, enhancing developer productivity.
 - Generated portable PDBs so cross-platform debuggers can read symbol files, improving debugging experience in VSCode and other tools.
 - Simplified WinGet configuration files by using the [Microsoft.Windows.Settings](https://www.powershellgallery.com/packages/Microsoft.Windows.Settings) module to enable Developer Mode. Thanks [@mdanish-kh](https://github.com/mdanish-kh)! 
 - Adjusted build scripts for the latest Az.Accounts module to keep CI green.
 - Streamlined release pipeline by removing hard-coded telemetry version numbers, and unified Command Palette versioning with Windows Terminal's versioning method for consistent updates.
 - Enhanced the build validation step to show detailed differences between NOTICE.md and actual package dependencies and versions.
 - Improved spell-checking accuracy across the repo. Thanks [@rovercoder](https://github.com/rovercoder)!  
 - Upgraded CI to TouchdownBuild v5 for faster pipelines.  
 - Added context comments to *Resources.resw* to help translators.
 - Expanded fuzz testing coverage to include FancyZones.
 - Integrated all unit tests into the CI pipeline, increasing from ~3,000 to ~5,000 tests.
 - Enabled daily UI test automation on the main branch, now covering over 370 UI tests for end-to-end validation.
 - Newly added unit tests for WorkspacesLib to improve reliability and maintainability.

### General

- Updated bug report compression library (cziplib 0.3.3) for faster and more reliable package creation. Thanks [@Chubercik](https://github.com/Chubercik)!
- Included App Installer (“AppX Deployment Server”) event logs in bug reports for more thorough diagnostics.  

### What is being planned for version 0.93

For [v0.93][github-next-release-work], we'll work on the items below:

 - Continued Command Palette polish
 - New UI automation tests
 - Working on installer upgrades
 - Working on shortcut conflict detection
 - Upgrading Keyboard Manager's editor UI
 - Stability, bug fixes

## PowerToys Community

The PowerToys team is extremely grateful to have the [support of an amazing active community][community-link]. The work you do is incredibly important. PowerToys wouldn’t be nearly what it is today without your help filing bugs, updating documentation, guiding the design, or writing features. We want to say thank you and take time to recognize your work.  Month by month, you directly help make PowerToys a better piece of software.

## Code of Conduct

This project has adopted the [Microsoft Open Source Code of Conduct][oss-conduct-code].

## Privacy Statement

The application logs basic diagnostic data (telemetry). For more privacy information and what we collect, see our [PowerToys Data and Privacy documentation](https://aka.ms/powertoys-data-and-privacy-documentation).

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

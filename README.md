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
| [Image Resizer](https://aka.ms/PowerToysOverview_ImageResizer) | [Keyboard Manager](https://aka.ms/PowerToysOverview_KeyboardManager) | [Mouse Utilities](https://aka.ms/PowerToysOverview_MouseUtilities) |
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
[github-next-release-work]: https://github.com/microsoft/PowerToys/issues?q=is%3Aissue+milestone%3A%22PowerToys+0.94%22
[github-current-release-work]: https://github.com/microsoft/PowerToys/issues?q=is%3Aissue+milestone%3A%22PowerToys+0.93%22
[ptUserX64]: https://github.com/microsoft/PowerToys/releases/download/v0.93.0/PowerToysUserSetup-0.93.0-x64.exe 
[ptUserArm64]: https://github.com/microsoft/PowerToys/releases/download/v0.93.0/PowerToysUserSetup-0.93.0-arm64.exe 
[ptMachineX64]: https://github.com/microsoft/PowerToys/releases/download/v0.93.0/PowerToysSetup-0.93.0-x64.exe 
[ptMachineArm64]: https://github.com/microsoft/PowerToys/releases/download/v0.93.0/PowerToysSetup-0.93.0-arm64.exe
 
|  Description   | Filename |
|----------------|----------|
| Per user - x64       | [PowerToysUserSetup-0.93.0-x64.exe][ptUserX64] |
| Per user - ARM64     | [PowerToysUserSetup-0.93.0-arm64.exe][ptUserArm64] |
| Machine wide - x64   | [PowerToysSetup-0.93.0-x64.exe][ptMachineX64] |
| Machine wide - ARM64 | [PowerToysSetup-0.93.0-arm64.exe][ptMachineArm64] |

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

### 0.93 - Aug 2025 Update

In this release, we focused on new features, stability, optimization improvements, and automation.

**✨Highlights**

 - PowerToys settings debuts a modern, card-based dashboard with clearer descriptions and faster navigation for a streamlined user experience.
 - Command Palette had over 99 issues resolved, including bringing back Clipboard History, adding context menu shortcuts, pinning favorite apps, and supporting history in Run.
 - Command Palette reduced its startup memory usage by ~15%, load time by ~40%, built-in extensions loading time by ~70%, and installation size by ~55%—all due to using the full Ahead-of-Time (AOT) compilation mode in Windows App SDK.
 - Peek now supports instant previews and embedded thumbnails for Binary G-code (.bgcode) 3D printing files, making it easy to inspect models at a glance. Thanks [@pedrolamas](https://github.com/pedrolamas)!
 - Mouse Utilities introduces a new spotlight highlighting mode that dims the screen and draws attention to your cursor, perfect for presentations.
 - Test coverage improvements for multiple PowerToys modules including Command Palette, Advanced Paste, Peek, Text Extractor, and PowerRename — ensuring better reliability and quality, with over 600 new unit tests (mostly for Command Palette) and doubled UI automation coverage.

### Command Palette

 - Ensured screen readers are notified when the selected item in the list changes for better accessibility.
 - Fixed command title changes not being properly notified to screen readers. Thanks [@jiripolasek](https://github.com/jiripolasek)!
 - Made icon controls excluded from keyboard navigation by default for better accessibility. Thanks [@jiripolasek](https://github.com/jiripolasek)!
 - Improved UI design with better text sizing and alignment.
 - Fixed keyboard shortcuts to work better in text boxes and context menus.
 - Added right-click context menus with critical command styling and separators.
 - Improved various context menu issues, improving item selection, handling of long titles, search bar text scaling, initial item behavior, and primary button functionality.
 - Fixed context menu crashes with better type handling.
 - Fixed "Reload" command to work with both uppercase and lowercase letters.
 - Added mouse back button support for easier navigation. Thanks [@jiripolasek](https://github.com/jiripolasek)!
 - Fixed Alt+Left Arrow navigation not working when search box contains text. Thanks [@jiripolasek](https://github.com/jiripolasek)!
 - Updated back button tooltip to show keyboard shortcut information. Thanks [@jiripolasek](https://github.com/jiripolasek)!
 - Fixed Command Palette window not appearing properly when activated. Thanks [@jiripolasek](https://github.com/jiripolasek)!
 - Fixed Command Palette window staying hidden from taskbar after File Explorer restarts. Thanks [@jiripolasek](https://github.com/jiripolasek)!
 - Fixed window focus not returning to previous app properly.
 - Fixed Command Palette window to always appear on top when shown and move to bottom when hidden. Thanks [@jiripolasek](https://github.com/jiripolasek)!
 - Fixed window hiding to properly work on UI thread. Thanks [@jiripolasek](https://github.com/jiripolasek)!
 - Fixed crashes and improved stability with better synchronization of Command list updates. Thanks [@jiripolasek](https://github.com/jiripolasek)!
 - Improved extension disposal with better error handling to prevent crashes. Thanks [@jiripolasek](https://github.com/jiripolasek)!
 - Improved stability by fixing a UI threading issue when loading more results, preventing possible crashes and ensuring the loading state resets if loading fails. Thanks [@jiripolasek](https://github.com/jiripolasek)!
 - Enhanced icon loading stability with better exception handling. Thanks [@jiripolasek](https://github.com/jiripolasek)!
 - Added thread safety to recent commands to prevent crashes. Thanks [@MaoShengelia](https://github.com/MaoShengelia)!
 - Fixed acrylic (frosted glass) system backdrop display issues by ensuring proper UI thread handling. Thanks [@jiripolasek](https://github.com/jiripolasek)!

### Command Palette extensions

 - Added settings to each provider to control which fallback commands are enabled. Thanks [@jiripolasek](https://github.com/jiripolasek)! for fixing a regression in this feature.
 - Added sample code showing how Command Palette extensions can track when their pages are loaded or unloaded. [Check it out here](./src/modules/cmdpal/ext/SamplePagesExtension/OnLoadPage.cs).
 - Fixed *Calculator* to accept regular spaces in numbers that use space separators. Thanks [@PesBandi](https://github.com/PesBandi)!
 - Added a new setting to *Calculator* to make "Copy" the primary button (replacing “Save”) and enable "Close on Enter", streamlining the workflow. Thanks [@PesBandi](https://github.com/PesBandi)!
 - Improved *Apps* indexing error handling and removed obsolete code. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Prevented apps from showing in search when the *Apps* extension is disabled. Thanks [@jiripolasek](https://github.com/jiripolasek)!
 - Added ability to pin/unpin *Apps* using Ctrl+P shortcut.
 - Added keyboard shortcuts to the *Apps* context menu items for faster access. 
 - Added all file context menu options to the *Apps* items context menu, making all file actions available there for better functionality.
 - Streamlined All *Apps* extension settings by removing redundant descriptions, making the UI clearer.
 - Added command history to the *Run* page for easier access to previous commands.
 - Fixed directory path handling in *Run* fallback for better file navigation.
 - Fixed URL fallback item hiding properly in *Web Search* extension when search query becomes invalid. Thanks [@jiripolasek](https://github.com/jiripolasek)!
 - Added proper empty state message for *Web Search* extension when no results found. Thanks [@jiripolasek](https://github.com/jiripolasek)!
 - Added fallback command to *Windows Settings* extension for better search results.
 - Re-enabled *Clipboard History* feature with proper window handling.
 - Improved *Add Bookmark* extension to automatically detect file, folder, or URL types without manual input.
 - Updated terminology from "Kill process" to "End task" in *Window Walker* for consistency with Windows.
 - Fixed minor grammar error in SamplePagesExtension code comments. Thanks [@purofle](https://github.com/purofle)!

### Mouse Utilities

 - Added a new spotlight highlighting mode that creates a large transparent circle around your cursor with a backdrop effect, providing an alternative to the traditional circle highlight. Perfect for presentations where you want to focus attention on a specific area while dimming the rest of the screen.

### Peek

 - Added preview and thumbnail support for Binary G-code (.bgcode) files used in 3D printing. You can now see embedded thumbnails and preview these compressed 3D printing files directly in Peek and File Explorer. Thanks [@pedrolamas](https://github.com/pedrolamas)!

### Quick Accent

 - Added Vietnamese language support to Quick Accent, mappings for Vietnamese vowels (a, e, i, o, u, y) and the letter d. Thanks [@octastylos-pseudodipteros](https://github.com/octastylos-pseudodipteros)!

### Settings

 - Completely redesigned the Settings dashboard with a modern card-based layout featuring organized sections for quick actions and shortcuts overview, replacing the old module list.
 - Rewrote setting descriptions to be more concise and follow Windows writing style guidelines, making them easier to understand.
 - Improved formatting and readability of release notes in the "What's New" section with better typography and spacing.
 - Added missing deep link support for various settings pages (Peek, Quick Accent, PowerToys Run, etc.) so you can jump directly to specific settings.
 - Resolved an issue where the settings page header would drift away from its position when resizing the settings window.
 - Resolved a settings crash related to incompatible property names in ZoomIt configuration.

### Documentation

 - Added detailed step-by-step instructions for first-time developers building the Command Palette module, including prerequisites and Visual Studio setup guidance. Thanks [@chatasweetie](https://github.com/chatasweetie)!
 - **Fixed Broken SDK Link**: Corrected a broken markdown link in the Command Palette SDK README that was pointing to an incorrect directory path. Thanks [@ChrisGuzak](https://github.com/ChrisGuzak)!
 - Added documentation for the "Open With Cursor" plugin that enables opening Visual Studio and VS Code recent files using Cursor AI. Thanks [@VictorNoxx](https://github.com/VictorNoxx)!
 - Added documentation for two new community plugins - Hotkeys plugin for creating custom keyboard shortcuts, and RandomGen plugin for generating random data like passwords, colors, and placeholder text. Thanks [@ruslanlap](https://github.com/ruslanlap)!

### Development

 - Updated .NET libraries to 9.0.8 for performance and security. Thanks [@snickler](https://github.com/snickler)!
 - Updated the spell check system to version 0.0.25 with better GitHub integration and SARIF reporting, plus fixed numerous spelling errors throughout the codebase including property names and documentation. Thanks [@jsoref](https://github.com/jsoref)!
 - Cleaned up spelling check configuration to eliminate false positives and excessive noise that was appearing in every pull request, making the development process smoother.
 - Replaced NuGet feed with Azure Artifacts for better package management.
 - Implemented configurable UI test pipeline that can use pre-built official releases instead of building everything from scratch, reducing test execution time from 2+ hours.
 - Replaced brittle pixel-by-pixel image comparison with perceptual hash (pHash) technology that's more robust to minor rendering differences - no more test failures due to anti-aliasing or compression artifacts.
 - Reduced CI/fuzzing/UI test timeouts from 4 hours to 90 minutes, dramatically improving developer feedback loops and preventing long waits when builds get stuck.
 - Standardized test project naming across the entire codebase and improved pipeline result identification by adding platform/install mode context to test run titles. Thanks [@khmyznikov](https://github.com/khmyznikov)!
 - Added comprehensive UI test suites for multiple PowerToys modules including Command Palette, Advanced Paste, Peek, Text Extractor, and PowerRename - ensuring better reliability and quality.
 - Enhanced UI test automation with command-line argument support, better session management, and improved element location methods using pattern matching to avoid failures from minor differences in exact matches.

### What is being planned over the next few releases

For [v0.94][github-next-release-work], we'll work on the items below:

 - Continued Command Palette polish
 - Working on Shortcut Guide v2 (Thanks [@noraa-junker](https://github.com/noraa-junker)!)
 - Working on upgrading the installer to WiX 5
 - Working on shortcut conflict detection
 - Working on setting search
 - Upgrading Keyboard Manager's editor UI
 - New UI automation tests
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

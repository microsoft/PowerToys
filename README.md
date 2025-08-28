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

### 0.94 - Sep 2025 Update

In this release, we focused on new features, stability, optimization improvements, and automation.

**✨Highlights**

 - The installer was upgraded to WiX 5 after WiX 3 reached end-of-life; this move improved installer security, reliability, and community support.
 - PowerToys Settings added a Settings search with fuzzy matching, suggestions, a results page, and UX polish to make finding options faster.
 - A comprehensive hotkey conflict detection system was introduced in Settings to surface and help resolve conflicting shortcuts.
 - Command Palette received stability, accessibility, and UX improvements — fixed single‑click activation; added support for path shortcuts (~, /, \\) in file search; fixed race conditions and cancellation issues; and improved diagnostics to reduce memory leaks.
 - Peek added XML syntax support for .shproj and .projitems in its Monaco preview. Thanks [@rezanid](https://github.com/rezanid)!
 - Context menu registration was moved from the installer to runtime to avoid loading disabled modules (runtime registrations).
 - Mouse Utilities added a “Gliding cursor” accessibility feature to Mouse Pointer Crosshairs for single‑button cursor movement and clicking. Thanks [@mikehall-ms](https://github.com/mikehall-ms)!

### Always On Top

 - Fixes issue where wait cursor was incorrectly displayed when hovering over Always On Top window border, ensuring proper arrow cursor is shown. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!

### Command Palette

 - Honors “Single-click activation” only for pointer clicks; keyboard always activates immediately. Thanks [@jiripolasek](https://github.com/jiripolasek)!
 - Removed constraints that kept context menu flyout within window bounds, allowing proper positioning. Thanks [@jiripolasek](https://github.com/jiripolasek)!
 - Improves error messages with timestamps, HRESULTs, and full exception details. Thanks [@jiripolasek](https://github.com/jiripolasek)!
 - Fixes regression when updating a provider with no commands by appending items safely. Thanks [@jiripolasek](https://github.com/jiripolasek)!
 - Brings the existing Settings window to the front after opening. Thanks [@jiripolasek](https://github.com/jiripolasek)!
 - Replaces Clipboard History outline icon with a colorful Fluent icon. Thanks [@jiripolasek](https://github.com/jiripolasek)!
 - Prevents duplicate parenting in ContentIcon with checks and a debug assert. Thanks [@jiripolasek](https://github.com/jiripolasek)!
 - Uses pattern matching “is null/is not null” for safer null checks across the codebase.
 - Makes the Activation Shortcut dialog focusable for screen readers. Thanks [@chatasweetie](https://github.com/chatasweetie)!
 - Uses a stable Windows SDK for the extension toolkit and reorganizes message classes.
 - Supports ~, /, and \ as path shortcuts in file search. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Fixes race condition by switching SetCanceled to TrySetCanceled. Thanks [@jiripolasek](https://github.com/jiripolasek)!
 - Uses modern WinUI 3 brush for menu separators. Thanks [@jiripolasek](https://github.com/jiripolasek)!
 - Adds ARM64 PDBs to the Extensions SDK package.
 - Adds single‑select Filters to DynamicListPage and updates Windows Services sample.
 - Changes main page filter placeholder to “Search for apps, files and commands…”. Thanks [@jiripolasek](https://github.com/jiripolasek)!
 - Removes explicit WinAppSDK/WebView2 deps from toolkit and API so clients control versions. Thanks [@rluengen](https://github.com/rluengen)!
 - Adds a local keyboard listener to handle the GoBack key. Thanks [@jiripolasek](https://github.com/jiripolasek)!
 - Ensures alias changes propagate and resolve conflicts without crashes.
 - Marks CommandProvider.Dispose as virtual.
 - Cleans up ListItemViewModels to prevent leaks during updates and cancels.
 - Sorts DateTime extension results by relevance.
 - Fixed race condition causing search text to 'jiggle' or bounce during rapid updates.
 - Moves accessibility announcements into a shared UIHelper and cleans up settings UI. Thanks [@chatasweetie](https://github.com/chatasweetie)!
 - Preserves Adaptive Card action types during trimming using DynamicDependency.
 - Adds acrylic backdrop and style tweaks to the context menu. Thanks [@jiripolasek](https://github.com/jiripolasek)!
 - Prevents disposed ContentPage instances from handling messages; cleans up on close. Thanks [@jiripolasek](https://github.com/jiripolasek)!
 - Paves the way for future API additions with new interfaces and type‑cache prepopulation.
 - Adds “evil” samples to repro tricky behavior.
 - Fixes WinGet issues in release builds by avoiding non‑trim‑safe LINQ.
 - Cancels in-progress item fetches when a new one starts in CmdPal to prevent stale results.

### Command Palette extensions

 - Adds helpful empty states for Window Walker, Windows Settings, and Windows Search. Thanks [@htcfreek](https://github.com/htcfreek)!
 - Adds app icons to the “Run” command in All Apps when available.
 - Fixes missing built‑in icons by centralizing extension dependencies.
 - Adds WinAppSDK dependency to two sample extensions for local deployment.

### Hosts File Editor

 - Adds a “No leading spaces” option to keep active lines unindented when saving hosts; preserves current behavior by default. Thanks [@mohammed-saalim](https://github.com/mohammed-saalim)!

### Image Resizer

 - Fixes Image Resizer localization by installing satellite resource DLLs under the WinUI3Apps culture path expected at runtime.

### Mouse Utilities

- Adds a “Gliding cursor” accessibility feature to Mouse Pointer Crosshairs for single-button cursor movement and clicking. Thanks [@mikehall-ms](https://github.com/mikehall-ms)!

### Mouse Without Borders

- Adds an option to block Easy Mouse from switching machines while a fullscreen app is active, with app allow‑list. Thanks [@dot-tb](https://github.com/dot-tb)!

### Peek

 - Adds XML syntax support for .shproj and .projitems in Peek’s Monaco preview. Thanks [@rezanid](https://github.com/rezanid)!
 - Fixes bgcode preview handler registration and events for reliable previews. Thanks [@pedrolamas](https://github.com/pedrolamas)!

### PowerRename

 - Changes the Explorer context menu accelerator from W to E to avoid conflict with “New”. Thanks [@aaron-ni](https://github.com/aaron-ni)!

### Quick Accent

 - Remembers character usage between sessions to keep your most-used accents first. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Adds Maltese language support with accented characters and € symbol. Thanks [@rovercoder](https://github.com/rovercoder)!
 - Reduces hybrid‑graphics issues by only making the window Topmost while the picker is open. Thanks [@daverayment](https://github.com/daverayment)!

### Settings

 - Adds visual templates for Office and Copilot keys in key visuals.
 - Moved the shutdown button from title bar to navigation view footer menu item. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Introduces comprehensive hotkey conflict detection.
 - Adds settings search with fuzzy matching, suggestions, and results page, with follow‑up fixes and UX polish.
 - Ensures product names like “Awake” remain untranslated across Spanish UI by adding explicit “do not localize” comments.
 - Simplifies the Advanced Paste description in settings and fixes capitalization (Markdown/JSON). Thanks [@OldUser101](https://github.com/OldUser101)!

### Installer

 - Upgrades the installer to WiX 5 (side‑by‑side with WiX 3 during transition) with silent “Files in Use” handling for winget.
 - Moves legacy context menu registration from installer to runtime on to avoid DLL loading when disabled.

### Documentation

 - Adds docs for building the installer locally and testing winget installs.
 - Fixes broken link to the coding style guide in README. Thanks [@denizmaral](https://github.com/denizmaral)!

### Development

 - Configures BinSkim to skip tests and coverage DLLs to cut scans from 11k to 4.7k and reduce false positives.
 - Simplifies NOTICE by removing versions and excluding Microsoft/System packages to avoid build breaks.
 - Improves NuGet validation; bumps NLog to 5.2.8 and adds dotnet restore checks to catch downgrades.
 - Updates UTF.Unknown to 2.6.0 for modern frameworks (non‑breaking). Thanks [@304NotModified](https://github.com/304NotModified)!
 - Updates package catalog before installing gnome-keyring in CI to fix Linux package failures.
 - Refactors CmdPal system command unit tests with interfaces and a shared test base for better isolation.
 - Adds tests verifying Calculator “Close on Enter” swaps Copy/Save actions correctly. Thanks [@mohammed-saalim](https://github.com/mohammed-saalim)!
 - Adds accessibility IDs to CmdPal UI controls to make UI tests reliable.
 - Adds/migrates unit tests for WebSearch, Shell, Apps and Bookmarks with DI abstractions.
 - Cleans up AI‑generated tests; adds meaningful query tests across extensions.
 - Removes debug dialog; debug state is already shown in the titlebar.

### What is being planned over the next few releases

For [v0.94][github-next-release-work], we'll work on the items below:

 - Continued Command Palette polish
 - Working on Shortcut Guide v2 (Thanks [@noraa-junker](https://github.com/noraa-junker)!)
 - Automatic window tiling with FancyZones
 - Upgrading Keyboard Manager's editor UI
 - UI tweaking utility with day/night theme switcher
 - DSC v3 support for top utilities
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

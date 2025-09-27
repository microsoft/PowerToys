<p align="center">
    <picture>
      <source media="(prefers-color-scheme: light)" srcset="./doc/images/readme/pt-hero.light.png" />
      <img src="./doc/images/readme/pt-hero.dark.png" />
  </picture>
</p>

<h1 align="center">
  <span>Microsoft PowerToys</span>
</h1>

<h3 align="center">
  <a href="#-installation">Installation</a>
  <span> · </span>
  <a href="https://aka.ms/powertoys-docs">Documentation</a>
  <span> · </span>
  <a href="https://aka.ms/powertoys-releaseblog">Blog</a>
  <span> · </span>
  <a href="#-whats-new">Release notes</a>
</h3>
<br/><br/>
Microsoft PowerToys is a collection of utilities that help you customize Windows and streamline everyday tasks.
<br/><br/>

|   |   |   |
|---|---|---|
| [<img src="doc/images/icons/AdvancedPaste.png" alt="Advanced Paste icon" height="16"> Advanced Paste](https://aka.ms/PowerToysOverview_AdvancedPaste) | [<img src="doc/images/icons/Always%20On%20Top.png" alt="Always on Top icon" height="16"> Always on Top](https://aka.ms/PowerToysOverview_AoT) | [<img src="doc/images/icons/Awake.png" alt="Awake icon" height="16"> Awake](https://aka.ms/PowerToysOverview_Awake) |
| [<img src="doc/images/icons/Color%20Picker.png" alt="Color Picker icon" height="16"> Color Picker](https://aka.ms/PowerToysOverview_ColorPicker) | [<img src="doc/images/icons/Command%20Not%20Found.png" alt="Command Not Found icon" height="16"> Command Not Found](https://aka.ms/PowerToysOverview_CmdNotFound) | [<img src="doc/images/icons/Command Palette.png" alt="Command Palette icon" height="16"> Command Palette](https://aka.ms/PowerToysOverview_CmdPal) |
| [<img src="doc/images/icons/Crop%20And%20Lock.png" alt="Crop and Lock icon" height="16"> Crop And Lock](https://aka.ms/PowerToysOverview_CropAndLock) | [<img src="doc/images/icons/Environment%20Manager.png" alt="Environment Variables icon" height="16"> Environment Variables](https://aka.ms/PowerToysOverview_EnvironmentVariables) | [<img src="doc/images/icons/FancyZones.png" alt="FancyZones icon" height="16"> FancyZones](https://aka.ms/PowerToysOverview_FancyZones) |
| [<img src="doc/images/icons/File%20Explorer%20Preview.png" alt="File Explorer Add-ons icon" height="16"> File Explorer Add-ons](https://aka.ms/PowerToysOverview_FileExplorerAddOns) | [<img src="doc/images/icons/File%20Locksmith.png" alt="File Locksmith icon" height="16"> File Locksmith](https://aka.ms/PowerToysOverview_FileLocksmith) | [<img src="doc/images/icons/Host%20File%20Editor.png" alt="Hosts File Editor icon" height="16"> Hosts File Editor](https://aka.ms/PowerToysOverview_HostsFileEditor) |
| [<img src="doc/images/icons/Image%20Resizer.png" alt="Image Resizer icon" height="16"> Image Resizer](https://aka.ms/PowerToysOverview_ImageResizer) | [<img src="doc/images/icons/Keyboard%20Manager.png" alt="Keyboard Manager icon" height="16"> Keyboard Manager](https://aka.ms/PowerToysOverview_KeyboardManager) | [<img src="doc/images/icons/Find My Mouse.png" alt="Mouse Utilities icon" height="16"> Mouse Utilities](https://aka.ms/PowerToysOverview_MouseUtilities) |
| [<img src="doc/images/icons/MouseWithoutBorders.png" alt="Mouse Without Borders icon" height="16"> Mouse Without Borders](https://aka.ms/PowerToysOverview_MouseWithoutBorders) | [<img src="doc/images/icons/NewPlus.png" alt="New+ icon" height="16"> New+](https://aka.ms/PowerToysOverview_NewPlus) | [<img src="doc/images/icons/Peek.png" alt="Peek icon" height="16"> Peek](https://aka.ms/PowerToysOverview_Peek) |
| [<img src="doc/images/icons/PowerRename.png" alt="PowerRename icon" height="16"> PowerRename](https://aka.ms/PowerToysOverview_PowerRename) | [<img src="doc/images/icons/PowerToys%20Run.png" alt="PowerToys Run icon" height="16"> PowerToys Run](https://aka.ms/PowerToysOverview_PowerToysRun) | [<img src="doc/images/icons/PowerAccent.png" alt="Quick Accent icon" height="16"> Quick Accent](https://aka.ms/PowerToysOverview_QuickAccent) |
| [<img src="doc/images/icons/Registry%20Preview.png" alt="Registry Preview icon" height="16"> Registry Preview](https://aka.ms/PowerToysOverview_RegistryPreview) | [<img src="doc/images/icons/MeasureTool.png" alt="Screen Ruler icon" height="16"> Screen Ruler](https://aka.ms/PowerToysOverview_ScreenRuler) | [<img src="doc/images/icons/Shortcut%20Guide.png" alt="Shortcut Guide icon" height="16"> Shortcut Guide](https://aka.ms/PowerToysOverview_ShortcutGuide) |
| [<img src="doc/images/icons/PowerOCR.png" alt="Text Extractor icon" height="16"> Text Extractor](https://aka.ms/PowerToysOverview_TextExtractor) | [<img src="doc/images/icons/Workspaces.png" alt="Workspaces icon" height="16"> Workspaces](https://aka.ms/PowerToysOverview_Workspaces) | [<img src="doc/images/icons/ZoomIt.png" alt="ZoomIt icon" height="16"> ZoomIt](https://aka.ms/PowerToysOverview_ZoomIt) |


## 📋 Installation

For detailed installation instructions, visit the [installation docs](https://learn.microsoft.com/windows/powertoys/install). 

Before you begin, make sure your device meets the system requirements:

> [!NOTE]
> - Windows 11 or Windows 10 version 2004 (20H1 / build 19041) or newer
> - 64-bit processor: x64 or ARM64
> - Latest stable version of [Microsoft Edge WebView2 Runtime](https://go.microsoft.com/fwlink/p/?LinkId=2124703) is installed via the bootstrapper during setup

Choose one of the installation methods below:

<details>
<summary>Download .exe from GitHub</summary>

Go to the [PowerToys GitHub releases][github-release-link], click Assets to reveal the downloads, and choose the installer that matches your architecture and install scope. For most devices, that's the x64 per-user installer.

<!-- items that need to be updated release to release -->
[github-next-release-work]: https://github.com/microsoft/PowerToys/issues?q=is%3Aissue+milestone%3A%22PowerToys+0.95%22
[github-current-release-work]: https://github.com/microsoft/PowerToys/issues?q=is%3Aissue+milestone%3A%22PowerToys+0.94%22
[ptUserX64]: https://github.com/microsoft/PowerToys/releases/download/v0.94.0/PowerToysUserSetup-0.94.0-x64.exe 
[ptUserArm64]: https://github.com/microsoft/PowerToys/releases/download/v0.94.0/PowerToysUserSetup-0.94.0-arm64.exe 
[ptMachineX64]: https://github.com/microsoft/PowerToys/releases/download/v0.94.0/PowerToysSetup-0.94.0-x64.exe 
[ptMachineArm64]: https://github.com/microsoft/PowerToys/releases/download/v0.94.0/PowerToysSetup-0.94.0-arm64.exe
 
|  Description   | Filename |
|----------------|----------|
| Per user - x64       | [PowerToysUserSetup-0.94.0-x64.exe][ptUserX64] |
| Per user - ARM64     | [PowerToysUserSetup-0.94.0-arm64.exe][ptUserArm64] |
| Machine wide - x64   | [PowerToysSetup-0.94.0-x64.exe][ptMachineX64] |
| Machine wide - ARM64 | [PowerToysSetup-0.94.0-arm64.exe][ptMachineArm64] |

</details>

<details>
<summary>Microsoft Store</summary>
You can easily install PowerToys from the Microsoft Store:
<p>
  <a style="text-decoration:none" href="https://aka.ms/getPowertoys">
    <picture>
      <source media="(prefers-color-scheme: light)" srcset="doc/images/readme/StoreBadge-dark.png" width="148" />
      <img src="doc/images/readme/StoreBadge-light.png" width="148" />
  </picture></a>
</p>
</details>


<details>
<summary>WinGet</summary>

Download PowerToys from [WinGet][winget-link]. Updating PowerToys via winget will respect the current PowerToys installation scope. To install PowerToys, run the following command from the command line / PowerShell:

*User scope installer [default]*
```powershell
winget install Microsoft.PowerToys -s winget
```

*Machine-wide scope installer*
```powershell
winget install --scope machine Microsoft.PowerToys -s winget
```
</details>

<details>
<summary>Other methods</summary>

There are [community driven install methods](./doc/unofficialInstallMethods.md) such as Chocolatey and Scoop. If these are your preferred install solutions, you can find the install instructions there.
</details>

## ✨ What's new
**Version 0.94 (September 2025)**

For an in-depth look at the latest changes, visit the [Windows Command Line blog](https://aka.ms/powertoys-releaseblog).

**✨ Highlights**

 - PowerToys Settings added a Settings search with fuzzy matching, suggestions, a results page, and UX polish to make finding options faster.
 - A comprehensive hotkey conflict detection system was introduced in Settings to surface and help resolve conflicting shortcuts. Note that the default hotkey settings (Win+Ctrl+Shift+T, Win+Ctrl+V, Win+Ctrl+T, Win+Shift+T) may overlap with existing Windows system shortcuts. This is expected. You can resolve the conflict by assigning different hotkeys.
 - Mouse Utilities added a “Gliding cursor” accessibility feature to Mouse Pointer Crosshairs for single‑button cursor movement and clicking. Thanks [@mikehall-ms](https://github.com/mikehall-ms)!
 - The installer was upgraded to WiX 5 after WiX 3 reached end-of-life; this move improved installer security, reliability, and community support.
 - Tons of bug fixes and improvements for Command Palette, including visual updates and new support for filters on ListPages (handy for extension developers).
 - Hosts Editor now has a “No leading spaces” option so active host entries can start at column 0 even if others are disabled. Thanks [@mohammed-saalim](https://github.com/mohammed-saalim)!
 - Context menu registration was moved from the installer to runtime to avoid loading disabled modules (runtime registrations).
 - Quick Accent now supports Maltese, and frequently used accents appear first (and are remembered across sessions). Thanks [@rovercoder](https://github.com/rovercoder)! [@davidegiacometti](https://github.com/davidegiacometti)!

### Always On Top

 - Fixed the border hover cursor so it shows the arrow instead of the wait cursor. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!

### Command Palette

 - Applied single-click activation only to pointer input; keyboard always activates immediately. Thanks [@jiripolasek](https://github.com/jiripolasek)!
 - Let context menus open at the cursor by removing window-bound constraints. Thanks [@jiripolasek](https://github.com/jiripolasek)!
 - Made error messages clearer with timestamps, HRESULTs, and full details for easier diagnosis. Thanks [@jiripolasek](https://github.com/jiripolasek)!
 - Prevented crashes and improved robustness when updating providers without commands. Thanks [@jiripolasek](https://github.com/jiripolasek)!
 - Ensured the Settings window reliably comes to the front when opened. Thanks [@jiripolasek](https://github.com/jiripolasek)!
 - Replaced the Clipboard History icon with a colorful Fluent icon. Thanks [@jiripolasek](https://github.com/jiripolasek)!
 - Hardened ContentIcon to avoid duplicate parenting and improve diagnostics. Thanks [@jiripolasek](https://github.com/jiripolasek)!
 - Standardized null checks using C# pattern matching for safer behavior.
 - Improved accessibility by focusing the activation shortcut dialog and making text reachable. Thanks [@chatasweetie](https://github.com/chatasweetie)!
 - Moved the extension SDK to a stable Windows SDK and cleaned up message namespaces.
 - Added path shortcuts: ~ to home, and / or \\ to system root, plus UNC support. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Fixed a race in cancellation handling to avoid InvalidOperationException. Thanks [@jiripolasek](https://github.com/jiripolasek)!
 - Aligned separator styling with WinUI 3 for consistent visuals. Thanks [@jiripolasek](https://github.com/jiripolasek)!
 - Added ARM64 PDBs to the Extensions SDK NuGet for better debugging.
 - Added single-select filters to DynamicListPage and updated Windows Services sample.
 - Updated main page placeholder text to better describe what can be searched. Thanks [@jiripolasek](https://github.com/jiripolasek)!
 - Removed explicit WinAppSDK/WebView2 dependencies from toolkit and API. Thanks [@rluengen](https://github.com/rluengen)!
 - Added a local keyboard hook to handle the GoBack key reliably. Thanks [@jiripolasek](https://github.com/jiripolasek)!
 - Propagated alias changes safely and resolved conflicts across view models.
 - Allowed providers to override Dispose with a virtual method.
 - Fixed memory leaks by cleaning up removed or cancelled list items.
 - Sorted DateTime extension results by relevance for better usability.
 - Reduced search text "jiggling" by avoiding redundant change notifications.
 - Centralized automation notifications in a UIHelper for better accessibility. Thanks [@chatasweetie](https://github.com/chatasweetie)!
 - Preserved Adaptive Card action types during trimming via DynamicDependency.
 - Added an acrylic backdrop and refined styling to the context menu. Thanks [@jiripolasek](https://github.com/jiripolasek)!
 - Prevented disposed pages and Settings windows from handling stale messages. Thanks [@jiripolasek](https://github.com/jiripolasek)!
 - Made the extension API easier to evolve without breaking clients.
 - Added "evil" sample pages to help reproduce tricky bugs.
 - Fixed WinGet trim-safety issues by replacing LINQ with manual iteration.
 - Cancelled stale list fetches to avoid older results overwriting newer ones in CmdPal.

### Command Palette extensions

 - Improved empty states and ranking logic for multiple extensions. Thanks [@htcfreek](https://github.com/htcfreek)!
 - Added app icons to the All Apps "Run" context command when available.
 - Restored missing builtin icons by standardizing extension dependencies.
 - Unblocked local deployment by adding WinAppSDK to two sample extensions.

### Hosts File Editor

 - Added a "No leading spaces" option so active hosts entries can start at column 0 even when others are disabled. Thanks [@mohammed-saalim](https://github.com/mohammed-saalim)!

### Image Resizer

 - Fixed Image Resizer localization by installing satellite resources under the WinUI 3 apps culture path.

### Mouse Utilities

 - Introduced "Gliding cursor" to control the pointer and click with a single hotkey for better accessibility. Thanks [@mikehall-ms](https://github.com/mikehall-ms)!

### Mouse Without Borders

 - Blocked Easy Mouse from switching machines during fullscreen apps, with an allow-list for exceptions. Thanks [@dot-tb](https://github.com/dot-tb)!

### Peek

 - Added Visual Studio shared project file types to XML preview and fixed bgcode handler registration. Thanks [@rezanid](https://github.com/rezanid)!
 - Fixes bgcode preview handler registration and events for reliable previews. Thanks [@pedrolamas](https://github.com/pedrolamas)!

### PowerRename

 - Changed the Explorer accelerator key to PowErRename to avoid clashing with the New menu. Thanks [@aaron-ni](https://github.com/aaron-ni)!

### Quick Accent

 - Remembered character usage across sessions so frequently used accents appear first. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Added Maltese language support with specific characters and the Euro symbol. Thanks [@rovercoder](https://github.com/rovercoder)!
 - Reduced GPU usage issues by making the window Topmost only when the picker is visible. Thanks [@daverayment](https://github.com/daverayment)!

### Settings

 - Added telemetry to track usage of the new shortcut conflict detection workflow.
 - Moved the shutdown action from the title bar to a footer menu item with confirmation. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Implemented comprehensive hotkey conflict detection with a dedicated resolution dialog.
 - Added branded visuals for Office and Copilot keys in the KeyVisual control.
 - Introduced Settings search with fuzzy matching and navigation to specific controls.
 - Corrected Spanish localization so product names like Awake remain in English across Settings and OOBE.
 - Simplified the Advanced Paste description in Settings for quicker reading and consistent capitalization. Thanks [@OldUser101](https://github.com/OldUser101)!
 - Localized conflict messages in the conflict window and dialog.

### Installer

 - Upgraded the installer to WiX 5 with silent "Files in Use" handling for smoother winget installs.
 - Switched Win10 context menu modules to runtime registration and added cleanup on uninstall to avoid stale entries.

### Documentation

 - Adds docs for building the installer locally and testing winget installs.
 - Fixed a broken style guide link in developer documentation. Thanks [@denizmaral](https://github.com/denizmaral)!

### Development

 - Excluded test and coverage DLLs from BinSkim scans to cut false positives and speed up security analysis.
 - Simplified NOTICE maintenance by removing version numbers and filtering out Microsoft/System packages.
 - Improved NuGet dependency validation to prevent package downgrades and catch issues during restore.
 - Updated UTF.Unknown to a modern version to improve compatibility without breaking changes. Thanks [@304NotModified](https://github.com/304NotModified)!
 - Refreshed package catalog in CI before installing dependencies to prevent Linux workflow failures.
 - Refactored CmdPal tests with dependency injection and added coverage for queries and settings.
 - Added unit tests to verify Close on Enter swaps Copy/Save as expected. Thanks [@mohammed-saalim](https://github.com/mohammed-saalim)!
 - Added accessibility IDs to CmdPal UI for stable UI tests.
 - Rewrote system command tests with a new test base and cleaner patterns.
 - Added unit tests for WebSearch and Shell extensions with mockable settings.
 - Added unit tests and abstractions for Apps and Bookmarks extensions.
 - Cleans up AI-generated tests; adds meaningful query tests across extensions.
 - Removed the obsolete debug dialog from Settings for a smoother developer loop.

## 🛣️ Roadmap

For [v0.95][github-next-release-work], we'll work on the items below:

 - Continued Command Palette polish
 - Working on Shortcut Guide v2 (Thanks [@noraa-junker](https://github.com/noraa-junker)!)
 - Upgrading Keyboard Manager's editor UI
 - UI tweaking utility with day/night theme switcher
 - DSC v3 support for top utilities
 - New UI automation tests
 - Stability, bug fixes

## ❤️ PowerToys Community

The PowerToys team is extremely grateful to have the [support of an amazing active community][community-link]. The work you do is incredibly important. PowerToys wouldn't be nearly what it is today without your help filing bugs, updating documentation, guiding the design, or writing features. We want to say thank you and take time to recognize your work. Your contributions and feedback improve PowerToys month after month!

## Contributing

This project welcomes contributions of all types. Besides coding features / bug fixes, other ways to assist include spec writing, design, documentation, and finding bugs. We are excited to work with the power user community to build a set of tools for helping you get the most out of Windows.

We ask that **before you start work on a feature that you would like to contribute**, please read our [Contributor's Guide](CONTRIBUTING.md). We would be happy to work with you to figure out the best approach, provide guidance and mentorship throughout feature development, and help avoid any wasted or duplicate effort.

Most contributions require you to agree to a [Contributor License Agreement (CLA)][oss-CLA] declaring that you grant us the rights to use your contribution and that you have permission to do so.

For guidance on developing for PowerToys, please read the [developer docs](./doc/devdocs) for a detailed breakdown. This includes how to setup your computer to compile.

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

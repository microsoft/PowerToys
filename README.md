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
| [FancyZones](https://aka.ms/PowerToysOverview_FancyZones) | [File Explorer Add-ons](https://aka.ms/PowerToysOverview_FileExplorerAddOns) | [File Locksmith](https://aka.ms/PowerToysOverview_FileLocksmith) |
| [Hosts File Editor](https://aka.ms/PowerToysOverview_HostsFileEditor) | [Image Resizer](https://aka.ms/PowerToysOverview_ImageResizer) | [Keyboard Manager](https://aka.ms/PowerToysOverview_KeyboardManager) |
| [Mouse utilities](https://aka.ms/PowerToysOverview_MouseUtilities) | [PowerRename](https://aka.ms/PowerToysOverview_PowerRename) | [PowerToys Run](https://aka.ms/PowerToysOverview_PowerToysRun) |
| [Quick Accent](https://aka.ms/PowerToysOverview_QuickAccent) | [Screen Ruler](https://aka.ms/PowerToysOverview_ScreenRuler) | [Shortcut Guide](https://aka.ms/PowerToysOverview_ShortcutGuide) |
| [Text Extractor](https://aka.ms/PowerToysOverview_TextExtractor) | [Video Conference Mute](https://aka.ms/PowerToysOverview_VideoConference) |

## Installing and running Microsoft PowerToys

### Requirements

- Windows 11 or Windows 10 version 2004 (code name 20H1 / build number 19041) or newer.
- Our installer will install the following items:
   - [.NET 7.0.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/7.0#runtime-desktop-7.0.0).
   - [Microsoft Edge WebView2 Runtime](https://go.microsoft.com/fwlink/p/?LinkId=2124703) bootstrapper. This will install the latest version.

### Via GitHub with EXE [Recommended]

Go to [Microsoft PowerToys GitHub releases page][github-release-link], click on `Assets` at the bottom to show the files available in the release. Please use the appropriate the PowerToys installer that matches your machine's architecture. For most, it is `x64`.
 
 - **For x64 processors (most common):** [PowerToysSetup-0.65.0-x64.exe](https://github.com/microsoft/PowerToys/releases/download/v0.65.0/PowerToysSetup-0.65.0-x64.exe)
 - **For ARM64 processors:** [PowerToysSetup-0.65.0-arm64.exe](https://github.com/microsoft/PowerToys/releases/download/v0.65.0/PowerToysSetup-0.65.0-arm64.exe)

This is our preferred method.

### Via Microsoft Store

Install from the [Microsoft Store's PowerToys page][microsoft-store-link]. You must be using the [new Microsoft Store](https://blogs.windows.com/windowsExperience/2021/06/24/building-a-new-open-microsoft-store-on-windows-11/) which will be available for both Windows 11 and Windows 10.

### Via WinGet (Preview)
Download PowerToys from [WinGet][winget-link]. To install PowerToys, run the following command from the command line / PowerShell:

```powershell
winget install Microsoft.PowerToys -s winget
```

### Other install methods

There are [community driven install methods](./doc/unofficialInstallMethods.md) such as Chocolatey and Scoop. If these are your preferred install solutions, this will have the install instructions.

## Contributing

This project welcomes contributions of all types. Help spec'ing, design, documentation, finding bugs are ways everyone can help on top of coding features / bug fixes. We are excited to work with the power user community to build a set of tools for helping you get the most out of Windows.

We ask that **before you start work on a feature that you would like to contribute**, please read our [Contributor's Guide](CONTRIBUTING.md). We will be happy to work with you to figure out the best approach, provide guidance and mentorship throughout feature development, and help avoid any wasted or duplicate effort.

Most contributions require you to agree to a [Contributor License Agreement (CLA)][oss-CLA] declaring that you have the right to, and actually do, grant us the rights to use your contribution.

For guidance on developing for PowerToys, please read the [developer docs](/doc/devdocs) for a detailed breakdown. This includes how to setup your computer to compile.

## What's Happening

### PowerToys Roadmap

Our [prioritized roadmap][roadmap] of features and utilities that the core team is focusing on.

### 0.65 - November 2022 Update

In this release, we focused on stability and improvements.

**Highlights**

- The codebase was upgraded to work with .NET 7. Thanks [@snickler](https://github.com/snickler)!
- Quick Accent can now show a description of the selected character. Thanks [@Aaron-Junker](https://github.com/Aaron-Junker)!
- ColorPicker now supports adding custom formats.

### Known issues

- The Text Extractor utility [fails to recognize text in some cases on ARM64 devices running Windows 10](https://github.com/microsoft/PowerToys/issues/20278).
- After installing PowerToys, [the new Windows 11 context menu entries for PowerRename and Image Resizer might not appear before a system restart](https://github.com/microsoft/PowerToys/issues/19124).
- There are reports of users who are [unable to open the Settings window](https://github.com/microsoft/PowerToys/issues/18015). This is being caused by incompatibilities with some applications (RTSS RivaTuner Statistics Server is a known examples of this). If you're affected by this, please check the  linked issue to verify if any of the presented solutions works for you.

### General

- Downgraded the ModernWPF dependency to 0.9.4 to avoid issues on certain virtualization technologies. (This was a hotfix for 0.64)
- Upgraded and fixed the code to work with .NET 7. Thanks [@snickler](https://github.com/snickler)!

### Always on Top

- Added telemetry for the pinning/unpinning events.

### Awake

- Added telemetry.
- Removed exiting Awake from the tray icon when starting from the runner. Utilities started from the runner should be disabled in the Settings to avoid discrepancies.

### Color Picker

- Fixed an infinite loop due to a looping UI refresh. (This was a hotfix for 0.64)
- Added a feature to allow users to create their own color formats.

### FancyZones

- Fixed an issue that caused turning off spaces between zones to not apply correctly. (This was a hotfix for 0.64)
- Prevent the shift key press from trickling down to the focused window. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
- Fixed a bug causing FancyZones to try resizing hidden windows.
- Fixed the focus layout preview being empty on first run in the editor.
- Fixed UI margin in the "Create new layout" dialog.
- Fixed window positioning issues when switching between virtual desktops.
- Fixed snapping by hotkey in single zone layouts.

### File explorer add-ons

- Added .log file support to the Monaco preview handler. Thanks [@Eagle3386](https://github.com/Eagle3386)!

### File Locksmith

- Query system and other users processes when elevated. (This was a hotfix for 0.64)
- Icon and UI fixes. Thanks [@niels9001](https://github.com/niels9001)! (This was a hotfix for 0.64)

### Group Policy Objects

- Removed a obsolete dependency from the admx file to fix importing on Intune. Thanks [@htcfreek](https://github.com/htcfreek)! (This was a hotfix for 0.64)

### Hosts File Editor

- Added a scrollbar to the additional lines dialog. Thanks [@davidegiacometti](https://github.com/davidegiacometti)! (This was a hotfix for 0.64)
- Updated the plus icon. Thanks [@niels9001](https://github.com/niels9001)! (This was a hotfix for 0.64)
- Prevent the new entry content dialog from overlapping the title bar.
- Updated the name for the additional lines feature. Thanks [@htcfreek](https://github.com/htcfreek)!
- Added a workaround for an issue causing the context menu not opening on right-click. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!

### Image Resizer
- Fixed a silent crash when trying to show the tier 1 context menu on Windows 11.

### PowerToys Run

- Added pinyin support to the search. Thanks [@frg2089](https://github.com/frg2089)!
- Fixed an error in the TimeZone plugin preventing searching for standard time zones. Thanks [@Tantalus13A98B5F](https://github.com/Tantalus13A98B5F)!
- Added the English abbreviations as fallbacks in the UnitConverter plugin. Thanks [@Tantalus13A98B5F](https://github.com/Tantalus13A98B5F)!

### Quick Accent

- Added mappings for the mu, omicron, upsilon and thorn characters.
- Added a setting to exclude apps from activating Quick Accent.
- Fixed an issue causing the selector to trigger when leaving the lock screen. Thanks [@damienleroy](https://github.com/damienleroy)!
- Added the Croatian, Netherlands, Swedish and Welsh character sets. Thanks [@damienleroy](https://github.com/damienleroy)!
- Added support for more unicode characters. Thanks [@char-46](https://github.com/char-46)!
- Shift-space can now navigate backwards in the selector. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
- Added the Catalan accented characters. Thanks [@ivocarbajo](https://github.com/ivocarbajo) and [@codingneko](https://github.com/codingneko)!
- Added the Kurdish accented characters.
- Added the Serbian accented characters. Thanks [@damienleroy](https://github.com/damienleroy)!
- Added the Irish and Scottish accented characters.
- Added the description for the currently selected character in the selector.
- Fixed a bug causing the selector window to appear blank.

### Runner

- Fixed a crash on a racing condition accessing the IPC communication with Settings.

### Settings

- Fixed settings name in the QuickAccent page. Thanks [@htcfreek](https://github.com/htcfreek)!
- Added a message indicating there's no network available when looking for updates.
- Fixed an error causing the backup/restore feature to not find the backup file. Thanks [@jefflord](https://github.com/jefflord)!
- Fixed localization for the "All apps" expression in the keyboard manager page.
- UI refactoring, clean-up and bringing in modern controls. Thanks [@niels9001](https://github.com/niels9001)!
- Improved settings/OOBE screens text. Thanks [@Jay-o-Way](https://github.com/Jay-o-Way)!
- The backup/restore feature also backs up FancyZones layouts.

### Shortcut Guide
- Added a setting to make the shortcuts and taskbar icons have different configurable response times. Thanks [@OkamiWong](https://github.com/OkamiWong)!

### Video Conference Mute

- Changed the warning about deprecating Video Conference Mute to saying it's going to go into legacy mode, thanks to community feedback.  (This was a hotfix for 0.64)

### Documentation

- Added the core team to COMMUNITY.md

### Development

- Fixed some errors in the GitHub issue templates. Thanks [@Aaron-Junker](https://github.com/Aaron-Junker)!
- Updated the Windows implementation library. Thanks [@AtariDreams](https://github.com/AtariDreams)!
- Added Hosts File Editor to the issue templates. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
- Turned on C++ code analysis and incrementally fixing warnings.
- Cleaned up unused dependencies. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
- Fixed building on the latest MSVC.
- Fixed multi-processor build on the latest MSBuild.
- Added a message to suggest the feedback hub to the fabric bot triggers.
- Optimized every png file with the zopfli algorithm. Thanks [@pea-sys](https://github.com/pea-sys)!
- Updated the .vsconfig file for a quicker development setup. Thanks [@ChaseKnowlden](https://github.com/ChaseKnowlden)!
- Fixed a language typo in the code. Thanks [@eltociear](https://github.com/eltociear)!
- Fixed wrong x86 target in the solution file.
- Added a script to fail building when the nuget packages aren't consolidated. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
- Upgraded the Vanara.Invoke dependencies.
- Upgraded and brought back the spell-checker. Thanks [@jsoref](https://github.com/jsoref)!
- Added a new dependencies feed and fixed release CI. Thanks [@Aaron-Junker](https://github.com/Aaron-Junker)!

#### What is being planned for version 0.66

For [v0.66][github-next-release-work], we'll work on below:

- Ship .NET self contained and shared between utilities
- Stability / bug fixes

## PowerToys Community

The PowerToys team is extremely grateful to have the [support of an amazing active community][community-link]. The work you do is incredibly important. PowerToys wouldn’t be nearly what it is today without your help filing bugs, updating documentation, guiding the design, or writing features. We want to say thank you and take time to recognize your work.  Month over month, you directly help make PowerToys a better piece of software.

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

<!-- items that need to be updated release to release -->
[github-next-release-work]: https://github.com/microsoft/PowerToys/issues?q=is%3Aopen+is%3Aissue+project%3Amicrosoft%2FPowerToys%2F39
[github-current-release-work]: https://github.com/microsoft/PowerToys/issues?q=is%3Aopen+is%3Aissue+project%3Amicrosoft%2FPowerToys%2F38

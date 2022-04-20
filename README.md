# Microsoft PowerToys

![Hero image for Microsoft PowerToys](doc/images/overview/PT_hero_image.png)

[How to use PowerToys][usingPowerToys-docs-link] | [Downloads & Release notes][github-release-link] | [Contributing to PowerToys](#contributing) | [What's Happening](#whats-happening) | [Roadmap](#powertoys-roadmap)

## Build status

| Architecture | Solution (Main) | Solution (Stable) | Installer (Main) |
|--------------|-----------------|-------------------|------------------|
| x64 | [![Build Status for Main](https://dev.azure.com/ms/PowerToys/_apis/build/status/microsoft.PowerToys?branchName=main)](https://dev.azure.com/ms/PowerToys/_build/latest?definitionId=219&branchName=main) | [![Build Status for Stable](https://dev.azure.com/ms/PowerToys/_apis/build/status/microsoft.PowerToys?branchName=stable)](https://dev.azure.com/ms/PowerToys/_build/latest?definitionId=219&branchName=stable) | [![Build Status Installer pipeline](https://dev.azure.com/microsoft/Dart/_apis/build/status/microsoft.PowerToys?branchName=main)](https://dev.azure.com/microsoft/Dart/_build/latest?definitionId=76541&branchName=main) |
| ARM64 | Currently investigating | [Issue #490](https://github.com/microsoft/PowerToys/issues/490) |  |

## About

Microsoft PowerToys is a set of utilities for power users to tune and streamline their Windows experience for greater productivity. For more info on [PowerToys overviews and how to use the utilities][usingPowerToys-docs-link], or any other tools and resources for [Windows development environments](https://docs.microsoft.com/windows/dev-environment/overview), head over to [docs.microsoft.com][usingPowerToys-docs-link]! 

|              | Current utilities: |              |
|--------------|--------------------|--------------|
| [Always on Top](https://aka.ms/PowerToysOverview_AoT) | [PowerToys Awake](https://aka.ms/PowerToysOverview_Awake) | [Color Picker](https://aka.ms/PowerToysOverview_ColorPicker) |
| [FancyZones](https://aka.ms/PowerToysOverview_FancyZones) | [File Explorer Add-ons](https://aka.ms/PowerToysOverview_FileExplorerAddOns) | [Image Resizer](https://aka.ms/PowerToysOverview_ImageResizer) |
| [Keyboard Manager](https://aka.ms/PowerToysOverview_KeyboardManager) | [Mouse utilities](https://aka.ms/PowerToysOverview_MouseUtilities) | [PowerRename](https://aka.ms/PowerToysOverview_PowerRename) |
| [PowerToys Run](https://aka.ms/PowerToysOverview_PowerToysRun) | [Shortcut Guide](https://aka.ms/PowerToysOverview_ShortcutGuide) | [Video Conference Mute](https://aka.ms/PowerToysOverview_VideoConference) |

## Installing and running Microsoft PowerToys

### Requirements

- Windows 11 or Windows 10 v1903 (18362) or newer.
- Our installer will install the following items:
   - [.NET Core 3.1.23 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/thank-you/runtime-desktop-3.1.23-windows-x64-installer) or a newer 3.1.x runtime. This is needed currently for the Settings application.
   - [.NET 6.0.3 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/thank-you/runtime-desktop-6.0.3-windows-x64-installer) or a newer 6.0.x runtime. 
   - [Microsoft Edge WebView2 Runtime](https://go.microsoft.com/fwlink/p/?LinkId=2124703) bootstrapper. This will install the latest version. 
   - [Windows App SDK Runtime 1.0.3](https://docs.microsoft.com/en-us/windows/apps/windows-app-sdk/downloads). This will install version 1.0.3 if this or newer version is not installed already.

### Via GitHub with EXE [Recommended]

 [Microsoft PowerToys GitHub releases page][github-release-link], click on `Assets` at the bottom to show the files available in the release and then click on `PowerToysSetup-0.57.0-x64.exe` to download the PowerToys installer.

This is our preferred method.

### Via Microsoft Store

Install from the [Microsoft Store's PowerToys page][microsoft-store-link]. You must be using the [new Microsoft Store](https://blogs.windows.com/windowsExperience/2021/06/24/building-a-new-open-microsoft-store-on-windows-11/) which will be available for both Windows 11 and Windows 10.

### Via WinGet (Preview)
Download PowerToys from [WinGet][winget-link]. To install PowerToys, run the following command from the command line / PowerShell:

```powershell
winget install Microsoft.PowerToys -s winget
```

### Other install methods

There are [community driven install methods](./doc/unofficialInstallMethods.md) such as Chocolatey and Scoop.  If these are your preferred install solutions, this will have the install instructions.

## Contributing

This project welcomes contributions of all types. Help spec'ing, design, documentation, finding bugs are ways everyone can help on top of coding features / bug fixes. We are excited to work with the power user community to build a set of tools for helping you get the most out of Windows.

We ask that **before you start work on a feature that you would like to contribute**, please read our [Contributor's Guide](CONTRIBUTING.md). We will be happy to work with you to figure out the best approach, provide guidance and mentorship throughout feature development, and help avoid any wasted or duplicate effort.

Most contributions require you to agree to a [Contributor License Agreement (CLA)][oss-CLA] declaring that you have the right to, and actually do, grant us the rights to use your contribution.

For guidance on developing for PowerToys, please read the [developer docs](/doc/devdocs) for a detailed breakdown. This includes how to setup your computer to compile.

## What's Happening

### PowerToys Roadmap

Our [prioritized roadmap][roadmap] of features and utilities that the core team is focusing on.

### 0.57 - March 2022 Update

In this release, we focused heavily on stability and improvements.  Below are some of the highlights!

**Highlights**

- PowerToys Run quality pass. Old standing issues were re-evaluated and fixed.
- Additional features and improvements were added to existing PowerToys Run plugins.
- New plugin for time and date values/information in PowerToys Run. Thanks [@htcfreek](https://github.com/htcfreek)!
- The [PowerToys Run documentation](https://aka.ms/PowerToysOverview_PowerToysRun) is also receiving a required update. Thanks [@htcfreek](https://github.com/htcfreek)!
- PowerToys will register SVGs as a picture kind when SVG Thumbnails are enabled so they appear when searching for pictures in File Explorer.
- We've disabled PDF preview by default, given its incompatibilities with Outlook and that Edge is now being registered for previewing PDF files on Windows 10 too.
- From a coding quality point of view, every project now has code analyzer active. Thanks [@CleanCodeDeveloper](https://github.com/CleanCodeDeveloper)!
- A double click on the tray icon is needed instead of single click to open settings.

### Always on Top

- New sound for Always on Top activation. Thanks [@franky920920](https://github.com/franky920920)!

### Awake
- Fixes for the system tray icon. Thanks [@dend](https://github.com/dend)!
- Temporary duration presets are now configurable. Thanks [@dend](https://github.com/dend)!
- Fix for an issue causing Awake to not be closed properly. Thanks [@dend](https://github.com/dend)!

### ColorPicker

- It's now possible to delete multiple colors from the history, or to export a list of colors to a file. Thanks [@mshtang](https://github.com/mshtang)!
- The CIEXYZ format has increased precision. Thanks [@m13253](https://github.com/m13253)!
- Performance improved by reducing the use of low level keyboard hooks.

### FancyZones

- Fixed a bug where the same layout applied with different configurations to different screens would reset to a single configuration. (This was a hotfix for 0.56)
- When snapping windows with rounded corners on Windows 11, set the correct corner preferences to avoid gaps between zones. Thanks [@hallatore](https://github.com/hallatore)!
- Fix for canvas layout resetting due to resolution changes.

### File explorer

- Additional markdown file extensions added for Markdown Preview. Thanks [@skycommand](https://github.com/skycommand)!
- SVG files are now registered as a picture kind on Windows.
- Added a text wrapping setting and copy context menu to dev file preview. Thanks [@Aaron-Junker](https://github.com/Aaron-Junker)!
- PDF file preview and thumbnails are now disabled by default, due to incompatibility with Outlook and an appropriate warning is shown in Settings.

### Mouse utility

- Find my mouse has a new setting to specify a minimum moving distance for activation. (This was a hotfix for 0.56)
- Fix for the bug causing the task bar to be hidden behind other windows when a mouse utility was active.
- Fix for the bug causing shortcuts set on icons to not activate when a mouse utility was active.
- Fixed a slight offset in Mouse Pointer Crosshairs when it's configured with an odd thickness.

### PowerToys Run

- New plugin for time and date values/information. Thanks [@htcfreek](https://github.com/htcfreek)!
- WindowWalker now has a tooltip, additional features like killing the process and closing the window, additional settings and improvements. Thanks [@htcfreek](https://github.com/htcfreek)!
- Unit converter now accepts alternative syntax for feet and gallon. It interprets as either imperial or US gallons depending on current culture. Thanks [@yifan-k](https://github.com/yifan-k)!
- Unit converter now accepts "metre" and "meter".
- Localization for Web Search and Unit Converter (not including units) has been enabled.
- Localization for Windows Terminal has been enabled. (This was a hotfix for 0.56)
- Calculator now tries to always interpret the dot (.) symbol as a decimal separator, despite configured culture, to meet expectations.
- Calculator now handles trailing zeroes on hexadecimal numbers correctly.
- System commands plugin can now show the local ip and mac addresses. Thanks [@htcfreek](https://github.com/htcfreek)!
- Folder plugin has improved results, with improved tooltips. Thanks [@htcfreek](https://github.com/htcfreek)!
- Windows settings plugin has added entries for Screen Saver and Connect Wiring Display Panel. Thanks [@htcfreek](https://github.com/htcfreek)!
- Plugins can now show descriptions for their configurations in settings. Thanks [@htcfreek](https://github.com/htcfreek)!
- Fix for the focus issue when calling PowerToys Run for the first time after login and after returning from some windows.
- Fix for a bug on Program when creating a shortcut.
- Validated that upgrading to .NET framework 6 fixed the error appearing when shutting down the system with PowerToys Run running.

### Video conference mute

- Newly added microphones are now updated and tracked by VCM.

### Settings

- _What's new_ button in the bottom with a new look, with a few more UI tweaks. Thanks [@niels9001](https://github.com/niels9001)!
- Fixed a bug causing Settings not to open when a racing condition caused Keyboard Manager settings to not be read correctly.
- To open settings from the tray icon a double click is needed instead of a single click.
- Fix for a bug which would cause checking for updates to run indefinitely.
- When auto-updating, pass a flag to avoid rebooting the computer without being prompted.

### Installer

- Dependencies installers are now executed with /norestart to avoid unprompted reboots. (This was a hotfix for 0.56). Thanks [@franky920920](https://github.com/franky920920)!
- Upgraded .NET framework dependency to 6.0.3.
- Installer logs are now saved where they can be collected and sent by the bug report tool.
- Reverted changes to start with proper elevation and when installed under a different user since those changes ended up causing more issues where PowerToys would start running with the wrong user.

### Development

- OOBE code refactor to have all module information in XAML, like in Settings. Thanks [@niels9001](https://github.com/niels9001)!
- Every project now has analyzers turned on and warnings fixed. Thanks [@CleanCodeDeveloper](https://github.com/CleanCodeDeveloper)!
- New patterns added for code spell-checking and stale entries removed. Thanks [@jsoref](https://github.com/jsoref)
- Additional logging has been added to Fancy Zones and PowerToys Run.
- A new CI release build will not be triggered if all that was changed was just documentation.
- Fixed a racing condition causing flaky build errors when building PowerRename.
- Centralization of common csproj/vcxproj settings underway. Thanks [@CleanCodeDeveloper](https://github.com/CleanCodeDeveloper)!

#### Community contributions

We'd like to directly mention certain contributors (in alphabetical order) for their continued community support this month and helping directly make PowerToys a better piece of software.  
[@Aaron-Junker](https://github.com/Aaron-Junker), [@CleanCodeDeveloper](https://github.com/CleanCodeDeveloper), [@dend](https://github.com/dend), [@franky920920](https://github.com/franky920920), [@htcfreek](https://github.com/htcfreek), [@jay-o-way](https://github.com/jay-o-way), [@jsoref](https://github.com/jsoref), [@niels9001](https://github.com/niels9001), [@snickler](https://github.com/snickler).


#### What is being planned for v0.58

For [v0.58][github-next-release-work], we'll start work on below:

- Environment Variables Editor PowerToy
- Continue work on another new PowerToy
- Stability / bug fixes
- Adding new file types to dev file preview

## PowerToys Community

The PowerToys team is extremely grateful to have the [support of an amazing active community][community-link]. The work you do is incredibly important. PowerToys wouldn’t be nearly what it is today without your help filing bugs, updating documentation, guiding the design, or writing features. We want to say thank you and take time to recognize your work.

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
[github-next-release-work]: https://github.com/microsoft/PowerToys/issues?q=is%3Aopen+is%3Aissue+project%3Amicrosoft%2FPowerToys%2F31
[github-current-release-work]: https://github.com/microsoft/PowerToys/issues?q=is%3Aopen+is%3Aissue+project%3Amicrosoft%2FPowerToys%2F30

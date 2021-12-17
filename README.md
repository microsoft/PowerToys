# Microsoft PowerToys

![Hero image for Microsoft PowerToys](doc/images/overview/PT_hero_image.png)

[How to use PowerToys][usingPowerToys-docs-link] | [Downloads & Release notes][github-release-link] | [Contributing to PowerToys](#contributing) | [What's Happening](#whats-happening) | [Roadmap](#powertoys-roadmap)

## Build status

| Architecture | Main | Installer (Stable) | Installer (Main) |
|--------------|------|--------|-----------|
| x64 | [![Build Status for Main](https://dev.azure.com/ms/PowerToys/_apis/build/status/microsoft.PowerToys?branchName=main)](https://dev.azure.com/ms/PowerToys/_build/latest?definitionId=219&branchName=main) | [![Build Status for Stable](https://dev.azure.com/ms/PowerToys/_apis/build/status/microsoft.PowerToys?branchName=stable)](https://dev.azure.com/ms/PowerToys/_build/latest?definitionId=219&branchName=stable) | [![Build Status for Installer](https://github-private.visualstudio.com/microsoft/_apis/build/status/CDPX/powertoys/powertoys-Windows-Official-master-Test?branchName=main)](https://github-private.visualstudio.com/microsoft/_build/latest?definitionId=61&branchName=main) |
| ARM64 | Currently investigating | [Issue #490](https://github.com/microsoft/PowerToys/issues/490) |  |

## About

Microsoft PowerToys is a set of utilities for power users to tune and streamline their Windows experience for greater productivity. For more info on [PowerToys overviews and how to use the utilities][usingPowerToys-docs-link], or any other tools and resources for [Windows development environments](https://docs.microsoft.com/windows/dev-environment/overview), head over to [docs.microsoft.com][usingPowerToys-docs-link]! 

|              | Current utilities: |              |
|--------------|--------------------|--------------|
| [PowerToys Awake](https://aka.ms/PowerToysOverview_Awake) | [Color Picker](https://aka.ms/PowerToysOverview_ColorPicker) | [FancyZones](https://aka.ms/PowerToysOverview_FancyZones) |
| [File Explorer Add-ons](https://aka.ms/PowerToysOverview_FileExplorerAddOns) | [Image Resizer](https://aka.ms/PowerToysOverview_ImageResizer) | [Keyboard Manager](https://aka.ms/PowerToysOverview_KeyboardManager) | 
| [Mouse utilities](https://aka.ms/PowerToysOverview_MouseUtilities) | [PowerRename](https://aka.ms/PowerToysOverview_PowerRename) | [PowerToys Run](https://aka.ms/PowerToysOverview_PowerToysRun) | 
| [Shortcut Guide](https://aka.ms/PowerToysOverview_ShortcutGuide) | [Video Conference Mute](https://aka.ms/PowerToysOverview_VideoConference) |  |

## Installing and running Microsoft PowerToys

### Requirements

- Windows 11 or Windows 10 v1903 (18362) or newer.
- [.NET Core 3.1.20 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/thank-you/runtime-desktop-3.1.20-windows-x64-installer) or a newer 3.1.x runtime. The installer will handle this if not present.

### Via GitHub with EXE [Recommended]

 [Microsoft PowerToys GitHub releases page][github-release-link], click on `Assets` at the bottom to show the files available in the release and then click on `PowerToysSetup-0.51.1-x64.exe` to download the PowerToys installer.

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

### 0.51 - November 2021 Update

The [v0.51 release cycle][github-current-release-work] introduces some new things regarding our mouse utilities. First is we've added in a presentation mode helper to highlight your mouse when you click. We've also added additional settings toward Find my mouse to enable more customization.

Next we've been focusing work on "Always on Top" system to help make any window you want to be the top most. A lot of thought is currently going into interaction models to make sure it 'feels' right for toggling as well as visualizing.

Last, we've been working on our engineering systems this month and into next. This work will improve our localization integration and our 'build farm' match our CI system far more. Behind the scene work but very important work for working faster. 

#### Highlights from v0.51

**Things to note**
- We shifted our localization internal service and are working on adding automated integrations back in. 

**PowerToys Awake**
- System tray and settings use same language for turning things on.

**Color Picker**
- New formats added to copy colors as a float or decimal value.
- Adjust color window now accepts lower-case HEX codes.

**FancyZones**
- New window switching functionality! Now users can assign multiple windows to a zone and cycle between them using the <kbd>Win</kbd> + <kbd>PgDn/PgUp</kbd> commands by default. Thanks [@FLOAT4](https://github.com/FLOAT4)!
- Added functionality for zones to adopt system accent color and theme. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
- Added visual preview of zone appearance in settings menu. Thanks [@niels9001](https://github.com/niels9001)!
- Fixed bug where FancyZones crashes on launch.

**Image Resizer**
- Fixed bug where resizing images creates empty folders.
- Added option to remove non-essential metadata. Helps significantly reduce the size of files. Thanks [@CleanCodeDeveloper](https://github.com/CleanCodeDeveloper)!
- Fixed bug caused by Image Resizer receiving an unexpected property type or value. Thanks [@CleanCodeDeveloper](https://github.com/CleanCodeDeveloper)!

**Mouse utilities**
- Find My Mouse: Improved functionality to activate when user double click time configuration is set above 100ms.
- Find My Mouse: Fixed display on all virtual desktops as opposed to only the virtual desktop where it was created.
- Find My Mouse: New settings to enable a lot more customization based on your feedback.
- Minor UI tweaks for fluent icons, appearance, <kbd>Ctrl</kbd> usage, and utility descriptions. Thanks [@niels9001](https://github.com/niels9001)!
- New Mouse Highlighter PowerToy! When enabled, activate mouse highlighting with <kbd>Win</kbd> + <kbd>Shift</kbd> + <kbd>H</kbd> by default to begin displaying visual cues on your display when either the left or right mouse buttons are clicked. There is a much more powerful tool called [SysInternal ZoomIt](https://docs.microsoft.com/en-us/sysinternals/downloads/zoomit) that is very helpful as well.

![highlighter turned on while dragging mouse](https://user-images.githubusercontent.com/9866362/142475413-77b00bae-bd28-42ae-a6c8-0dc4356e8525.gif)

**PowerRename**
- Improved rename performance! This is now at parity (or better) with the prior version based on multiple tests.
- Added keyboard accelerators with <kbd>Enter</kbd> and <kbd>Ctrl</kbd> + <kbd>Enter</kbd> to execute rename. Thanks [@niels9001](https://github.com/niels9001)!
- UI tweaks to now add number of items selected, grid-lines for improved readability, reduced font sizes & margins, and improved window resizing.
- Fixed UI focus issues. Thanks [@niels9001](https://github.com/niels9001)!
- Added default window width and height. Thanks [@niels9001](https://github.com/niels9001)!
- Added PowerRename event logging for BugReportTool

**PowerToys Run**
- New entries added for settings plugin. Thanks [@htcfreek](https://github.com/htcfreek)! 
- Added support for application URI handling like `mailto:` and  `ms-settings:`. Thanks [@franky920920](https://github.com/franky920920)!
- Added DevContainer workspaces to search results of the VSCode Workspaces Plugin. Thanks [@JacobDeuchert](https://github.com/JacobDeuchert)!
- Fixes for crashing issues.

**Shortcut Guide**
- Added rounded corners to keys and tooltips, and system accent colors for desktop backdrop. Thanks [@niels9001](https://github.com/niels9001)!

**Settings**
- Fixed default settings window size to prevent it from opening offscreen. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!

**Video Conference Mute**
- Minor UI tweaks for icon, clear button, and overlay image selection [#14248](https://github.com/microsoft/PowerToys/issues/14248). Thanks [@niels9001](https://github.com/niels9001)!

**Prototype work**
- Always on top prototype of being actively worked on.  Right now you hit a key-combo and it enables it.  We are investigating ways to highlight the window in some form as well.

**Installer**
- Investigated how to fully shift to WIX bootstrapper and remove custom boot strapper
- Investigated how to fully shift to HKCU vs HKLM.

**Random helping out**
- Spell check fix - Thanks [@franky920920](https://github.com/franky920920)!
- Fix a URL - Thanks [@JeffersonQin](https://github.com/JeffersonQin)!

**Development relevant**
- Focusing on cleaning up backlog of issues and developing a method to aid in prioritizing. [@Dend](https://github.com/dend) and [@crutkas](https://github.com/crutkas) are partnering to see if we can develop one signal to see what we are calling ['centers of gravity'](https://gravity-issues.netlify.app/).
- Our primary dev branch is now named `Main`.
- Adjusting plugin folder structure for PT Run [#10796](https://github.com/microsoft/PowerToys/issues/10796)
- Working on shifting our release pipeline onto same system that Windows Terminal uses.
- Improvements to environment variable usage/update process in PT Run. Thanks [@htcfreek](https://github.com/htcfreek)!
- Update .NET to 3.1.20.
- Centralized process list in the BugReportTool.
- Registry handling improvement for MSI and File Explorer add-ons.

**Community contributions**

We'd like to directly mention certain contributors (in alphabetical order) for their continued community support this month and helping directly make PowerToys a better piece of software. 

[@AnonymousWP](https://github.com/AnonymousWP), [@Aaron-Junker](https://github.com/Aaron-Junker), [@CleanCodeDeveloper](https://github.com/CleanCodeDeveloper), [@davidegiacometti](https://github.com/davidegiacometti), [@FLOAT4](https://github.com/FLOAT4), [@franky920920](https://github.com/franky920920), [@htcfreek](https://github.com/htcfreek), [@JacobDeuchert](https://github.com/JacobDeuchert), [@Jay-o-Way](https://github.com/jay-o-way) [@JeffersonQin](https://github.com/JeffersonQin), [@niels9001](https://github.com/niels9001), and [@rdeveen](https://github.com/rdeveen). 

#### What is being planned for v0.53

For [v0.53][github-next-release-work], due to holidays, we'll be in a maintenance sprint but here are some of the larger items:

- Hope to add Always on Top into PowerToys. We currently have a proof of concept ready.
- We are working to heavily reduce / remove the UAC prompt over the next few releases on install. This is a big shift so it is spanning multiple releases so we can isolate issues if they do occur. Work is tracked in [#10126](https://github.com/microsoft/PowerToys/issues/10126)
- Update the PowerToys Build Pipeline to allow .NET 6 integration
- Engineering Systems/Stability/Bug fixes

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
[github-next-release-work]: https://github.com/microsoft/PowerToys/issues?q=is%3Aopen+is%3Aissue+project%3Amicrosoft%2FPowerToys%2F27
[github-current-release-work]: https://github.com/microsoft/PowerToys/issues?q=is%3Aopen+is%3Aissue+project%3Amicrosoft%2FPowerToys%2F26

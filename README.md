# Microsoft PowerToys

![Hero image for Microsoft PowerToys](doc/images/overview/PT_hero_image.png)

[How to use PowerToys][usingPowerToys-docs-link] | [Downloads & Release notes][github-release-link] | [Contributing to PowerToys](#contributing) | [What's Happening](#whats-happening) | [Roadmap](#powertoys-roadmap)

## Build status

| Architecture | Solution (Main) | Solution (Stable) | Installer (Main) |
|--------------|-----------------|-------------------|------------------|
| x64 | [![Build Status for Main](https://dev.azure.com/ms/PowerToys/_apis/build/status/microsoft.PowerToys?branchName=main&jobName=Build%20x64%20Release)](https://dev.azure.com/ms/PowerToys/_build/latest?definitionId=219&branchName=main&jobName=Build%20x64%20Release) | [![Build Status for Stable](https://dev.azure.com/ms/PowerToys/_apis/build/status/microsoft.PowerToys?branchName=stable&jobName=Build%20x64%20Release)](https://dev.azure.com/ms/PowerToys/_build/latest?definitionId=219&branchName=stable) | [![Build Status Installer pipeline](https://dev.azure.com/microsoft/Dart/_apis/build/status/PowerToys/PowerToys%20Signed%20YAML%20Release%20Build?branchName=main&jobName=Build&configuration=Build%20Release_x64)](https://dev.azure.com/microsoft/Dart/_build/latest?definitionId=76541&branchName=main) |
| ARM64 | [![Build Status for Main](https://dev.azure.com/ms/PowerToys/_apis/build/status/microsoft.PowerToys?branchName=main&jobName=Build%20arm64%20Release)](https://dev.azure.com/ms/PowerToys/_build/latest?definitionId=219&branchName=main) | [![Build Status for Main](https://dev.azure.com/ms/PowerToys/_apis/build/status/microsoft.PowerToys?branchName=main&jobName=Build%20arm64%20Release)](https://dev.azure.com/ms/PowerToys/_build/latest?definitionId=219&branchName=stable) | [![Build Status Installer pipeline](https://dev.azure.com/microsoft/Dart/_apis/build/status/PowerToys/PowerToys%20Signed%20YAML%20Release%20Build?branchName=main&jobName=Build&configuration=Build%20Release_arm64)](https://dev.azure.com/microsoft/Dart/_build/latest?definitionId=76541&branchName=main) |

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

- Windows 11 or Windows 10 version 2004 (code name 20H1 / build number 19041) or newer.
- Our installer will install the following items:
   - [.NET 6.0.7 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/6.0#runtime-desktop-6.0.7) or a newer 6.0.x runtime.
   - [Microsoft Edge WebView2 Runtime](https://go.microsoft.com/fwlink/p/?LinkId=2124703) bootstrapper. This will install the latest version.
   - [Microsoft Visual C++ Redistributable](https://docs.microsoft.com/cpp/windows/latest-supported-vc-redist?view=msvc-170#visual-studio-2015-2017-2019-and-2022) installer. This will install one of the latest versions available.

### Via GitHub with EXE [Recommended]

 [Microsoft PowerToys GitHub releases page][github-release-link], click on `Assets` at the bottom to show the files available in the release. Please use the appropriate the PowerToys installer that matches your machine's architecture.  For most people, it is `x64`.
 
 - **For x64 processors (most common):** [PowerToysSetup-0.61.1-x64.exe](https://github.com/microsoft/PowerToys/releases/download/v0.61.1/PowerToysSetup-0.61.1-x64.exe)
 - **For ARM64 processors:** [PowerToysSetup-0.61.1-arm64.exe](https://github.com/microsoft/PowerToys/releases/download/v0.61.1/PowerToysSetup-0.61.1-arm64.exe)

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

### 0.61 - July 2022 Update

This is a lighter release, with a shorter development cycle and focused on stability and improvements.

**Highlights**

- Quality of life improvements for Always on Top, FancyZones and PowerToys Run.

### Known issues
- After installing PowerToys, [the new Windows 11 context menu entries for PowerRename and Image Resizer might not appear before a system restart](https://github.com/microsoft/PowerToys/issues/19124).
- There are reports of users who are [unable to open the Settings window](https://github.com/microsoft/PowerToys/issues/18015). This is being caused by incompatibilities with some applications (RTSS RivaTuner Statistics Server and MSI AfterBurner are known examples of this). If you're affected by this, please check the  linked issue to verify if any of the presented solutions works for you.

### General
- Upgraded the Windows App SDK runtimes to 1.1.2.
- The new Windows 11 context menu entries are now correctly added to Windows 11 dev channel insider builds. (This was a hotfix for 0.60)
- The old context menu entries are shown alongside the new Windows 11 context menu entries to be compatible with software that overrides the Windows 11 context menu behavior. (This was a hotfix for 0.60)
- Consolidated C# language version across the solution. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
- Removed deprecated Segoe icon glyph codes and replaced them with the correct ones.  Thanks [@niels9001](https://github.com/niels9001) and [@Jay-o-Way](https://github.com/Jay-o-Way)!
- Fixed an issue that caused a random accent key to be pressed on certain keyboard layouts when enabling some modules.

### Always on Top

- Fixed border flickering when activating. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
- Fixed a bug causing Always on Top to activate and hang when exiting PowerToys. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
- Fixed black edges appearing on rounded corners.
- Fixed a bug that was causing 100% CPU consumption.

### FancyZones

- Fixed a bug that caused layouts to not be applied correctly when many monitors reported having the same serial number. (This was a hotfix for 0.60)
- Fixed a bug that caused layouts to not be applied correctly on some virtual monitor setups (This was a hotfix for 0.60)
- A "Rows" default layout is now applied to vertical monitors, instead of a "Columns" layout. Thanks [@augustkarlstedt](https://github.com/augustkarlstedt)!

### Image Resizer

- Screen reader now announces the size name instead of the class name.

### File explorer add-ons

- Fixed an issue when creating thumbnails for SVG files created using Inkscape.

### Keyboard Manager

- Adjusted wording on the editor when keys are orphaned.

### Mouse utility

- Fixed a bug that caused the current Find My Mouse spotlight to hang when activated in the top left corner of the screen. (This was a hotfix for 0.60)

### PowerRename

- The PowerRename window reacts to current dpi when created.

### PowerToys Run

- Fixed a typo in the WindowWalker plugin UI. Thanks [@rohanrdy](https://github.com/rohanrdy)!
- Improved performance by saving the search history files only on exit. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
- PowerToys Run no longer shows results for some plugins when querying for empty spaces in a global query. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
- Added support for showing localized names for some win32 programs in the programs plugin. Thanks [@htcfreek](https://github.com/htcfreek)!
- The program plugin will now consider settings changed directly in ProgramPluginSettings.json. Thanks [@bezgumption](https://github.com/bezgumption)!

### Settings

- PowerToys Run settings page properly greys out the score adjustment setting when a plugin is not global. Thanks [@jefflord](https://github.com/jefflord)!
- PowerToys Run plugins score adjustment field accepts only numeric characters. Thanks [@jefflord](https://github.com/jefflord)!
- Will not run if started directly from its executable, as it was before the WinUI 3 upgrade.
- Fixed a typo in a PowerToys Run settings page description. Thanks [@eltociear](https://github.com/eltociear)!

### Installer
- Removed the dead code to make a msix installer.
- Updated the .NET dependency to 6.0.7.
- Won't create a new PowerToys shortcut on update if it's been removed manually by the user.

### Development

- Updated the Windows Store Package submission script to show less UI while installing PowerToys. (This was a hotfix for 0.60)
- Added more functionality to the Monitor Report Tool.
- The release CI now includes the version number in the symbols artifacts.
- GitHub should now show .vsconfig as a JSON file. Thanks [@osfanbuff63](https://github.com/osfanbuff63)!
- Centralized the configurations for NetAnalyzers and StyleCop. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
- Check-spelling has been upgraded to version 0.0.20. Thanks [@jsoref](https://github.com/jsoref)!

#### What is being planned for v0.62

For [v0.62][github-next-release-work], we'll work on below:

- Environment Variables Editor PowerToy
- Screen Measure PowerToy
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
[github-next-release-work]: https://github.com/microsoft/PowerToys/issues?q=is%3Aopen+is%3Aissue+project%3Amicrosoft%2FPowerToys%2F35
[github-current-release-work]: https://github.com/microsoft/PowerToys/issues?q=is%3Aopen+is%3Aissue+project%3Amicrosoft%2FPowerToys%2F34

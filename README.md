# Microsoft PowerToys

<img src="./doc/images/overview/PT%20hero%20image.png"/>

[How to use PowerToys][usingPowerToys-docs-link] | [Downloads & Release notes][github-release-link] | [Contributing to PowerToys](#contributing) | [What's Happening](#whats-happening) | [Roadmap](#powertoys-roadmap)

## Build status

| Architecture | Master | Stable | Installer |
|--------------|--------|--------|-----------|
| x64 | [![Build Status for Master](https://dev.azure.com/ms/PowerToys/_apis/build/status/microsoft.PowerToys?branchName=master)](https://dev.azure.com/ms/PowerToys/_build/latest?definitionId=219&branchName=master) | [![Build Status for Stable](https://dev.azure.com/ms/PowerToys/_apis/build/status/microsoft.PowerToys?branchName=stable)](https://dev.azure.com/ms/PowerToys/_build/latest?definitionId=219&branchName=stable) | [![Build Status for Installer](https://github-private.visualstudio.com/microsoft/_apis/build/status/CDPX/powertoys/powertoys-Windows-Official-master-Test?branchName=master)](https://github-private.visualstudio.com/microsoft/_build/latest?definitionId=61&branchName=master) |

## About

Microsoft PowerToys is a set of utilities for power users to tune and streamline their Windows experience for greater productivity. For more info on [PowerToys overviews and how to use the utilities][usingPowerToys-docs-link], or any other tools and resources for [Windows development environments](https://docs.microsoft.com/windows/dev-environment/overview), head over to [docs.microsoft.com][usingPowerToys-docs-link]! 

|              | Current utilities: |              |
|--------------|--------------------|--------------|
| [Awake](https://aka.ms/PowerToysOverview_Awake) | [Color Picker](https://aka.ms/PowerToysOverview_ColorPicker) | [FancyZones](https://aka.ms/PowerToysOverview_FancyZones) |
| [File Explorer Add-ons](https://aka.ms/PowerToysOverview_FileExplorerAddOns) | [Image Resizer](https://aka.ms/PowerToysOverview_ImageResizer) | [Keyboard Manager](https://aka.ms/PowerToysOverview_KeyboardManager) |
| [PowerRename](https://aka.ms/PowerToysOverview_PowerRename) | [PowerToys Run](https://aka.ms/PowerToysOverview_PowerToysRun) | [Shortcut Guide](https://aka.ms/PowerToysOverview_ShortcutGuide) |
| [Video Conference Mute (Experimental)](https://aka.ms/PowerToysOverview_VideoConference) | [Mouse utilities](https://aka.ms/PowerToysOverview_MouseUtilities) |  |

## Installing and running Microsoft PowerToys

### Requirements

- ⚠️ PowerToys (v0.37.0 and newer) requires Windows 10 v1903 (18362) or newer.

- Have [.NET Core 3.1.15 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/thank-you/runtime-desktop-3.1.15-windows-x64-installer). The installer should handle this but we want to directly make people aware.

### Via GitHub with EXE [Recommended]

#### Stable version

Install from the [Microsoft Store's PowerToys page][microsoft-store-link] or use [Microsoft PowerToys GitHub releases page][github-release-link]. 

- For GitHub, click on `Assets` to show the files available in the release and then click on `PowerToysSetup-0.47.1-x64.exe` to download the PowerToys installer.
- For Microsoft Store, you must be using the [new Microsoft Store](https://blogs.windows.com/windowsExperience/2021/06/24/building-a-new-open-microsoft-store-on-windows-11/) which will be available for both Windows 11 and Windows 10.

This is our preferred method.

#### Experimental version
To install the Video Conference mute, please use the [v0.46 experimental version of PowerToys][github-prerelease-link] to try out this version. It includes all improvements from v0.45 in addition to the Video conference utility. Click on `Assets` to show the files available in the release and then download the .exe installer.

### Via WinGet (Preview)
Download PowerToys from [WinGet](https://github.com/microsoft/winget-cli#installing-the-client). To install PowerToys, run the following command from the command line / PowerShell:

```powershell
winget install Microsoft.PowerToys -s winget
```

### Other install methods

There are [community driven install methods](./doc/unofficialInstallMethods.md) such as Chocolatey and Scoop.  If these are your preferred install solutions, this will have the install instructions.

### Processor support

We currently support the matrix below.

| x64 | x86 | ARM64 |
|:---:|:---:|:---:|
| [Supported][github-release-link] | [Issue #602](https://github.com/microsoft/PowerToys/issues/602) | [Issue #490](https://github.com/microsoft/PowerToys/issues/490) |

## Contributing

This project welcomes contributions of all types. Help spec'ing, design, documentation, finding bugs are ways everyone can help on top of coding features / bug fixes. We are excited to work with the power user community to build a set of tools for helping you get the most out of Windows.

We ask that **before you start work on a feature that you would like to contribute**, please read our [Contributor's Guide](CONTRIBUTING.md). We will be happy to work with you to figure out the best approach, provide guidance and mentorship throughout feature development, and help avoid any wasted or duplicate effort.

Most contributions require you to agree to a [Contributor License Agreement (CLA)][oss-CLA] declaring that you have the right to, and actually do, grant us the rights to use your contribution.

For guidance on developing for PowerToys, please read the [developer docs](/doc/devdocs) for a detailed breakdown. This includes how to setup your computer to compile.

## What's Happening

### PowerToys Roadmap

Our [prioritized roadmap][roadmap] of features and utilities that the core team is focusing on.

### 0.47 - September 2021 Update

Our goals for the [v0.47 release cycle](https://github.com/microsoft/PowerToys/issues?q=is%3Aopen+is%3Aissue+project%3Amicrosoft%2FPowerToys%2F24) primarily centered around stability updates and optimizations, installer updates, general bug fixes, and accessibility improvements.

Notably, based on the community feedback received, PowerToys has re-introduced the highly-requested ability to activate Shortcut Guide via holding the <kbd>Win</kbd> key. PowerToys also now allows various commands in PowerToys Run to be used in either the universal English phrasing or system-localized translation. The great feedback the community provides is invaluable in helping PowerToys continually grow and improve as a product.

An experimental version of PowerToys ([v0.48.1](https://github.com/microsoft/PowerToys/releases/tag/v0.48.1)) is also available, introducing improvements to our Video Conference Mute utility! All updates from the v0.47.1 release apply in v0.48.1.

#### Highlights from v0.47

**General**

- Fixed issue with new updates changing the PowerToys install location.
- Fixed settings with NumberBox elements overlapping the delete button.
- Fixed issue with the bug report tool not generating .zip files.
- Updated the shortcut configuration experience in Settings. Thanks @niels9001!
- Fixed inconsistent width of sidebar icons. Thanks @niels9001!
- Fixed sidebar UI not scaling for longer text strings in certain localizations. Thanks @niels9001!
- Fixed issue with settings not displaying invalid keystroke assignments. Thanks @niels9001!
- Added user defined shortcuts when set to the "Welcome to PowerToys" instead of the default shortcuts.
 

### Color Picker

- Accessibility issues addressed. Thanks @niels9001!
- Added CIELAB and CIEXYZ color formats. Thanks @RubenFricke!
- Fixed bug where changing RGB values manually doesn't automatically update the color displayed. Thanks @martinchrzan!

### FancyZones

- Fixed regression where restarting computer resets user defined layouts to the default selection.
- Fixed issues with Grid layout editor not showing the "Save" and "Cancel" buttons.
- Fixed accessibility issue where users could not add or merge zones using the keyboard.
- Added a flyout describe the prerequisites for the "Allow zones to span across monitors" option. 
- Fixed various crashing bugs.

### File Explorer add-ons

- Added PDF preview and thumbnail provider for Windows Explorer. Thanks @rdeveen!

### Image Resizer

- Added default values for newly added sizes. Thanks @htcfreek!
- Fixed regression where spaces in the filename format settings couldn't be registered.
- Corrected scaling issues with Image Resizer Window. Thanks @niels9001!
- Fixed issue where PowerToys crashes when json settings are not formatted properly. Thanks @davidegiacometti!

### Keyboard Manager

- Fixed crash when adding a shortcut.
- Fixed issue with Re-mappings window not displaying.
- Fixed issue when remapping a shortcut to <kbd>Alt</kbd>+<kbd>Tab</kbd> breaks the <kbd>Alt</kbd>+<kbd>Tab</kbd> navigation with arrow keys.

### PowerToys Run

- Improvements on subtitle layout for Settings plugin. Thanks @htcfreek!
- Added path filters for Settings plugin via `>` character. Thanks @htcfreek! 
- Translation improvements for Settings plugin. Thanks @htcfreek!
- Enabled translation for Settings Plugin. Thanks @htcfreek!
- Fixed issue with PowerToys Run not being in focus when launched.
- Fixed crash on empty/deleted environment variables when updating variables after a change. Thanks @htcfreek!
- Corrected Registry Plugin query results.
- Fixed crash in Registry plugin queries.
- Fixed crash when Windows shuts down.
- Added better description in the global results settings for plugins. Thanks @niels9001!
- Added a confirmation box before running system commands. Thanks @chrisharris333 and @davidegiacometti!
- Added option to use system localization our universal terminology for system commands. Thanks @davidegiacometti!

### Shortcut Guide

- Re-added the long <kbd>Win</kbd> key press to activate utility.

### Video Conference Mute

- Fixed an issue with the first hotkey input in the settings being focused when the page loads. Prevents unintentionally shortcut reassignment. Thanks @niels9001!

## Community contributions

We'd like to directly mention certain contributors (in alphabetical order) for their continued community support this month and helping directly make PowerToys a better piece of software.  

[@Aaron-Junker](https://github.com/Aaron-Junker), [@chrisharris333](https://github.com/chrisharris333), [@davidegiacometti](https://github.com/davidegiacometti), [@dend](https://github.com/dend), [@franky920920](https://github.com/franky920920), [@htcfreek](https://github.com/htcfreek), [@Jay-o-Way](https://github.com/Jay-o-Way), [@jsoref](https://github.com/jsoref), [@martinchrzan](https://github.com/martinchrzan), [@niels9001](https://github.com/niels9001), [@rdeveen](https://github.com/rdeveen) and [@RubenFricke](https://github.com/RubenFricke)

#### What is being planned for v0.49

For [v0.49][github-next-release-work], we are planning to work on:

- Execution on new utilities and enhancements
- UI/UX investigations to adopt WinUI and improve accessibility
- Stability and bug fixes
- Upgrading PowerToys Run to .NET 5


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
[roadmap]: https://github.com/microsoft/PowerToys/wiki/Roadmap
[privacy-link]: http://go.microsoft.com/fwlink/?LinkId=521839
[vidConfOverview]: https://aka.ms/PowerToysOverview_VideoConference
[loc-bug]: https://github.com/microsoft/PowerToys/issues/new?assignees=&labels=&template=translation_issue.md&title=
[usingPowerToys-docs-link]: https://aka.ms/powertoys-docs

<!-- items that need to be updated release to release -->
[github-next-release-work]: https://github.com/microsoft/PowerToys/issues?q=is%3Aopen+is%3Aissue+project%3Amicrosoft%2FPowerToys%2F25
[github-prerelease-link]: https://github.com/microsoft/PowerToys/releases/tag/v0.46.0

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
| [Mouse utilities](https://aka.ms/PowerToysOverview_MouseUtilities) | [Mouse Without Borders](https://aka.ms/PowerToysOverview_MouseWithoutBorders) | [Peek](https://aka.ms/PowerToysOverview_Peek) |
| [Paste as Plain Text](https://aka.ms/PowerToysOverview_PastePlain) | [PowerRename](https://aka.ms/PowerToysOverview_PowerRename) | [PowerToys Run](https://aka.ms/PowerToysOverview_PowerToysRun) |
| [Quick Accent](https://aka.ms/PowerToysOverview_QuickAccent) | [Registry Preview](https://aka.ms/PowerToysOverview_RegistryPreview) | [Screen Ruler](https://aka.ms/PowerToysOverview_ScreenRuler) |
| [Shortcut Guide](https://aka.ms/PowerToysOverview_ShortcutGuide) | [Text Extractor](https://aka.ms/PowerToysOverview_TextExtractor) | [Video Conference Mute](https://aka.ms/PowerToysOverview_VideoConference) |

## Installing and running Microsoft PowerToys

### Requirements

- Windows 11 or Windows 10 version 2004 (code name 20H1 / build number 19041) or newer.
- Our installer will install the following items:
   - [Microsoft Edge WebView2 Runtime](https://go.microsoft.com/fwlink/p/?LinkId=2124703) bootstrapper. This will install the latest version.

### Via GitHub with EXE [Recommended]

Go to [Microsoft PowerToys GitHub releases page][github-release-link], click on `Assets` at the bottom to show the files available in the release. Please use the appropriate PowerToys installer that matches your machine's architecture and install scope. For most, it is `x64` and per-user.

<!-- items that need to be updated release to release -->
[github-next-release-work]: https://github.com/microsoft/PowerToys/issues?q=project%3Amicrosoft%2FPowerToys%2F46
[github-current-release-work]: https://github.com/microsoft/PowerToys/issues?q=project%3Amicrosoft%2FPowerToys%2F45
[ptUserX64]: https://github.com/microsoft/PowerToys/releases/download/v0.72.0/PowerToysUserSetup-0.72.0-x64.exe
[ptUserArm64]: https://github.com/microsoft/PowerToys/releases/download/v0.72.0/PowerToysUserSetup-0.72.0-arm64.exe
[ptMachineX64]: https://github.com/microsoft/PowerToys/releases/download/v0.72.0/PowerToysSetup-0.72.0-x64.exe
[ptMachineArm64]: https://github.com/microsoft/PowerToys/releases/download/v0.72.0/PowerToysSetup-0.72.0-arm64.exe

|  Description   | Filename | sha256 hash |
|----------------|----------|-------------|
| Per user - x64       | [PowerToysUserSetup-0.72.0-x64.exe][ptUserX64] | 9925894D797458C78A8C3DF6FE4BD748580638B01BB43680477763662915109A |
| Per user - ARM64     | [PowerToysUserSetup-0.72.0-arm64.exe][ptUserArm64] | 2E68139C22C56648E64514E4E8E0A0D12882F6CF30B48EB20ECC66B4CCDD5909 |
| Machine wide - x64   | [PowerToysSetup-0.72.0-x64.exe][ptMachineX64] | 788EE4D828169F092737A739030B218CEFEC79583E42858BB8F9F036B701BE6F |
| Machine wide - ARM64 | [PowerToysSetup-0.72.0-arm64.exe][ptMachineArm64] | 39C1D430A538B0F3D7869D39DF7F636A64AAFAD8DFB3C82059A97F4EBD3369C4 |

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

## Contributing

This project welcomes contributions of all types. Help spec'ing, design, documentation, finding bugs are ways everyone can help on top of coding features / bug fixes. We are excited to work with the power user community to build a set of tools for helping you get the most out of Windows.

We ask that **before you start work on a feature that you would like to contribute**, please read our [Contributor's Guide](CONTRIBUTING.md). We will be happy to work with you to figure out the best approach, provide guidance and mentorship throughout feature development, and help avoid any wasted or duplicate effort.

Most contributions require you to agree to a [Contributor License Agreement (CLA)][oss-CLA] declaring that you have the right to, and actually do, grant us the rights to use your contribution.

For guidance on developing for PowerToys, please read the [developer docs](/doc/devdocs) for a detailed breakdown. This includes how to setup your computer to compile.

## What's Happening

### PowerToys Roadmap

Our [prioritized roadmap][roadmap] of features and utilities that the core team is focusing on.

### 0.72 - July 2023 Update

In this release, we focused on stability and improvements.

**Highlights**

 - Greatly reduced the PowerToys installed space by having utilities share the same installed path. When compared to 0.71, the 0.72 x64 machine installed version of PowerToys reduces the size reported in the Installed Apps screen from 1.15GB to 785 MB and the size in File Explorer properties for the installation folder from 3.10GB to 554 MB.
 - Value Generator - A new PowerToys Run plugin that generates hashes and GUID values. Thanks [@IHorvalds](https://github.com/IHorvalds)!
 - Mouse Highlighter has a new feature to have a highlight always follow the mouse pointer. Thanks [@hayatogh](https://github.com/hayatogh)!
 - PowerRename was reworked to support a bigger number of files without crashing.

### Known issues

 - Due to changing paths in the installation folder, the Mouse Without Borders service might be pointing to the wrong place. Users not running as admin will have to enable service mode again after install. A toast notification will appear if Mouse Without Borders is unable to start the service correctly.
 - File Explorer extensions changed paths might not be loaded correctly until File Explorer and Preview Host processes are restarted, so we advise restarting the computer when possible after updating PowerToys.

### General

 - Shared dependencies between applications in order to greatly reduce the installed size.
 - Added missing icons and icon sizes. Thanks [@niels9001](https://github.com/niels9001)!

### FancyZones

 - Fixed an issue where FancyZones wouldn't register a change to the "Switch between windows in the current zone" setting.
 - Added a Setting to enable the behavior of clicking the middle mouse button to toggle multiple zone spanning.

### File Locksmith

 - Fixed a File Explorer crash when deleting a file, updating PowerToys and then trying to right-click the background of a folder in File Explorer.
 - UI tweaks. Thanks [@Jay-o-Way](https://github.com/Jay-o-Way)!

### File Explorer add-ons

 - Updated the Monaco dependency for Developer Files Preview, supporting new file extensions and fixing issues. Thanks [@Aaron-Junker](https://github.com/Aaron-Junker)!

### Hosts File Editor

 - Consolidated the way the Hosts application is launched. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - UI tweaks. Thanks [@Jay-o-Way](https://github.com/Jay-o-Way)!

### Installer

 - Refactored the Monaco dependency inclusion. What to install is now being generated automatically.
 - Removed hardlinks and simplified the installer files, now that many utilities use the same paths.

### Mouse Highlighter

 - Added a feature so that a highlight follows the mouse even if no mouse button is being pressed. Thanks [@hayatogh](https://github.com/hayatogh)!

### Mouse Pointer Crosshairs

 - Added a setting to hide the crosshairs when the mouse pointer is also hidden. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Added a setting to select a fixed length for the crosshairs, which also spans across screens. Thanks [@Epp-code](https://github.com/Epp-code)! 

### Mouse Without Borders

 - Switched to a UWP mouse input API to fix mouse pointer lag issues that were reported.
 - A toast notification will appear when the service can't be started and Mouse Without Borders will try to start in non-service mode instead.
 - Fixed a bug where the service path wouldn't update to the new binary path when trying to re-enable service mode.
 - Fixed some grammar errors in the Mouse Without Borders user facing strings. Thanks [@KhurramJalil](https://github.com/KhurramJalil)!
 - Allow changing the shortcuts in the same way as other utilities and changed them to better defaults to avoid conflicting with Alt Gr+letter combos on international layouts.

### Peek

 - Also benefits from the Monaco dependency update when peeking into files supported by the Developer Files Preview. Thanks [@Aaron-Junker](https://github.com/Aaron-Junker)!
 - Fixed a flash on PowerToys starting due to the Peek window activating and hiding right away. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Updated icon design. Thanks [@niels9001](https://github.com/niels9001)!
 - Fixed flipped content issues on systems with RTL languages.

### PowerRename

 - Reworked the UI and resource consumption to fix crashes and hangs when trying to rename a huge number of files.
 - Added the Mica background material and some UI tweaks. Thanks [@niels9001](https://github.com/niels9001)!

### PowerToys Run

 - New plugin: Value Generator - generates values like hashes and GUIDs. Thanks [@IHorvalds](https://github.com/IHorvalds)!
 - The default input smoothing values were changed to the recommended values. Thanks [@SamMercer172](https://github.com/SamMercer172)!
 - Fixed tab navigation issues when using Shift+Tab to go backwards. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Fixed a crash caused by images not being found in the image cache due to racing conditions.
 - Fixed synchronization issues in the WindowWalker plugin. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Fixed a synchronization crash when getting localized system paths.
 - The PowerToys plugin is now activated by default. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!

### Quick Accent
 - Added the ("ḍ", U+1E0D) unicode character. Thanks [@SamMercer172](https://github.com/SamMercer172)!
 - Fixed an issue causing the left and right keys being discarded even when Quick Accent didn't activate.

### Registry Preview

 - Fixed a bug causing DWORD values to not be shown correctly. Thanks [@randyrants](https://github.com/randyrants)!
 - UI tweaks. Thanks [@Jay-o-Way](https://github.com/Jay-o-Way)!

### Runner

 - Show a warning asking the user to restart the computer after updating the PowerToys version.

### Screen Ruler

 - UI tweaks. Thanks [@Jay-o-Way](https://github.com/Jay-o-Way)!

### Settings

 - Fix an unused Expander in the File Locksmith settings page.
 - Added an info box to better explain what the extended context menu is.

### Development

 - Projects were restructured to allow sharing the same folder and dependencies and to avoid resource name conflicts.
 - Added scripts to CI to guard against applications having conflicting resources.
 - Added scripts to CI to guard against depending on different versions of the same dependency.
 - Test projects now build to a separate path.
 - Dependencies updated across the solution to ensure every project is using the same dependencies.

#### What is being planned for version 0.73

For [v0.73][github-next-release-work], we'll work on below:

 - New utility: Crop and Lock
 - Language selection
 - PowerRename enumeration keywords
 - Modernize and refresh UX of PowerToys based on WPF
 - Stability / bug fixes
- Peek: UI improvements

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

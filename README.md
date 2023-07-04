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

[github-current-release-work]: https://github.com/microsoft/PowerToys/issues?q=issue+project%3Amicrosoft%2FPowerToys%2F44
[ptUserX64]: https://github.com/microsoft/PowerToys/releases/download/v0.71.0/PowerToysUserSetup-0.71.0-x64.exe
[ptUserArm64]: https://github.com/microsoft/PowerToys/releases/download/v0.71.0/PowerToysUserSetup-0.71.0-arm64.exe
[ptMachineX64]: https://github.com/microsoft/PowerToys/releases/download/v0.71.0/PowerToysSetup-0.71.0-x64.exe
[ptMachineArm64]: https://github.com/microsoft/PowerToys/releases/download/v0.71.0/PowerToysSetup-0.71.0-arm64.exe

|  Description   | Filename | sha256 hash |
|----------------|----------|-------------|
| Per user - x64       | [PowerToysUserSetup-0.71.0-x64.exe][ptUserX64] | 4C6CCB3055E3838DA50FF529A670BAAD129570F4BFABF497B5D92259D3052794 | 
| Per user - ARM64     | [PowerToysUserSetup-0.71.0-arm64.exe][ptUserArm64] | 48633758DFBB99DE34BA2D3E3F294A60EF7E01015296D29A884251068B6FE3F6 | 
| Machine wide - x64   | [PowerToysSetup-0.71.0-x64.exe][ptMachineX64] | 44F092DFAC002536A27ABC701750D8C78FF30F8879768990BC4A0AFD0D5119F1 | 
| Machine wide - ARM64 | [PowerToysSetup-0.71.0-arm64.exe][ptMachineArm64] | 283A67539EDA5D3AD88735C7B0150852ECB57D569BAC80396F942C60D6ACB33F |

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

### 0.71 - June 2023 Update

In this release, we focused on stability and improvements.

**Highlights**
 - Support previewing archive files with Peek. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Fixed PT Run crash caused by missing App.Dark.png file.
 - Added setting to set Registry Preview as default app for opening .reg files. Thanks [@randyrants](https://github.com/randyrants)!
 - Modernized Settings app title bar and styling (Mica background material) to be inline with Windows 11 guidelines. Thanks [@niels9001](https://github.com/niels9001)!

### General

 - Fixed infinite loop issue caused by global event not being reset. (This was a hotfix for 0.70)
 - Bump CommunityToolkit.Mvvm package version to 8.2.0. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Fixed issue causing runner lag by moving check for updates and running bug report logic to the background thread.
 - Bump WinUIEx package version to 2.2. Thanks [@niels9001](https://github.com/niels9001)!
 - Fixed issue causing Settings app crash when launching a second app process. Thanks [@BLM16](https://github.com/BLM16)!
 - Fixed network errors when checking for updates on virtual machines.
 - Bump Microsoft.CodeAnalysis.NetAnalyzers package version to 7.0.3. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Bump Microsoft.Windows.Compatibility package version to 7.0.3.
 - Bump System.Management package version to 7.0.2.
 - Fixed issue causing PowerToys to start with Below Normal priority on startup.

### ColorPicker

 - Store color history in a separated file.

### FancyZones

 - Added feature to use middle click to toggle multiple zones spanning. Thanks [@BasitAli](https://github.com/BasitAli)!
 - Fixed issue causing zoning not to happen until the cursor is moved.
 - Improved monitor identification logic to mitigate issues causing layout reset.
 - Fixed issue where default layout was applied instead of blank layout.

### File Locksmith

 - Added setting to show only in extended context menu.

### File Explorer add-ons

 - Developer files preview support for .vsconfig, .sln, .vcproj, .vbproj, .fsproj and .vcxproj files. (This was a hotfix for 0.70)
 - Developer files preview support .vbs, .inf, .gitconfig, .gitattributes and .editorconfig files. (This was a hotfix for 0.70) Thanks [@Aaron-Junker](https://github.com/Aaron-Junker)!
 - Changed order of developer files preview` context menu items. Thanks [@Aaron-Junker](https://github.com/Aaron-Junker)!
 - Developer files preview support for .gitignore files. (This was a hotfix for 0.70) Thanks [@Aaron-Junker](https://github.com/Aaron-Junker)!
 - Fixed issue causing preview pane flickering on file selection and resizing. Thanks [@tanchekwei](https://github.com/tanchekwei)!

### Hosts

 - Improved UX by adding keyboard shortcuts. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Added setting to select the file encoding. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Fixed parsing of commented lines with an address and host in the middle of the comment. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Fixed issue on adding first entry and improve empty hosts list UI. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Added logic to handle more than 9 hosts per entry (Windows limitation) by splitting them into separate entries. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!

### ImageResizer

 - Added Enter key event handler when setting width/height of the new custom size.

### Installer

 - Fixed PowerToys Plugin installation. (This was a hotfix for 0.70) Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Fixed issue causing missing Mouse Without Borders service after upgrade. (This was a hotfix for 0.70)
 - Removed unneeded PT Run registry entries.

### Mouse Without Borders

 - Added Name2IP setting. (This was a hotfix for 0.70)
 - Fixed device layout issues. (This was a hotfix for 0.70)
 - Fixed hiding cursor at the top of the screen when "Hide mouse at the screen edge" is enabled. (This was a hotfix for 0.70)
 - Fixed issue that was preventing OS going to sleep mode. (This was a hotfix for 0.70)
 - Remove shortcut for deprecated VKMap functionality. (This was a hotfix for 0.70) Thanks [@dtaylor84](https://github.com/dtaylor84)!
 - Make MWB work without service if service doesn't start properly. (This was a hotfix for 0.70)
 - Fixed focus issue causing "Hide mouse at the screen edge" not to work properly. (This was a hotfix for 0.70)
 - Fixed issue causing app to hijack shortcut keys if they are only partially matched.

### Peek

 - Consume Ctrl+Space shortcut only if Desktop or Shell are in the foreground. (This was a hotfix for 0.70)
 - Added feature to hide window with Esc key. (This was a hotfix for 0.70) Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Added a setting to always run not-elevated (enabled by default). (This was a hotfix for 0.70)
 - Support .vsconfig, .sln, .vcproj, .vbproj, .fsproj and .vcxproj files. (This was a hotfix for 0.70)
 - Fixed blinking issue while loading developer files. (This was a hotfix for 0.70)
 - Reset preview Source on Peek window close. (This was a hotfix for 0.70)
 - Center Peek window on File Explorer activated monitor. (This was a hotfix for 0.70) Thanks [@SamChaps](https://github.com/SamChaps)!
 - Fix previewing unsupported file types by using effective pixels. (This was a hotfix for 0.70) Thanks [@SamChaps](https://github.com/SamChaps)!
 - Support .vbs, .inf, .gitconfig, .gitattributes and .editorconfig files. (This was a hotfix for 0.70) Thanks [@Aaron-Junker](https://github.com/Aaron-Junker)!
 - Fixed memory leak by clearing generated thumbnails. (This was a hotfix for 0.70)
 - Added setting to close on focus lost. (This was a hotfix for 0.70)
 - Fixed crash when triggering Peek with no files being selected. (This was a hotfix for 0.70)
 - Fixed setting Peek window as a foreground window. (This was a hotfix for 0.70)
 - Fixed race condition causing low quality preview to be displayed even if high quality preview is present. (This was a hotfix for 0.70)
 - Added support for .htm files.
 - Fixed issue where title bar button colors were not update on Windows theme change.
 - Added up/down arrow key item navigation. Thanks [@DanWiseProgramming](https://github.com/DanWiseProgramming)!
 - Improved UX by defining minimum window size and adding tooltips for shown text. Thanks [@htcfreek](https://github.com/htcfreek)!
 - Fixed crash on previewing internet shortcuts files.
 - Support previewing archive files. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!

### PowerToys Run

 - Fixed crash caused by non thread-safe Results update.
 - Fixed crash caused by missing App.Dark.png
 - Code cleanup and fixed possible crash caused by missing VS Code instance in VS Code plugin. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Fix environment helper for nested environment variables. Thanks [@htcfreek](https://github.com/htcfreek)!

### Quick Accent

 - Added multiplication and division signs. Thanks [@ailintom](https://github.com/ailintom)!
 - Added opening exclamation mark to Catalan and Spanish language. Thanks [@Aaron-Junker](https://github.com/Aaron-Junker)!
 - Added the section sign ("§", U+00A7). Thanks [@EikeJoo](https://github.com/EikeJoo)!
 - Added accent units and more additional signs. Thanks [@WilkoLu](https://github.com/WilkoLu)!

### Registry Preview

 - Added setting to set the app as default app for opening .reg files. Thanks [@randyrants](https://github.com/randyrants)!
 - Merge settings to single folder.
 - Fixed issue of saving files without truncation. Thanks [@qwerty472123](https://github.com/qwerty472123)!

### Text Extractor

 - Various improvements and fixes. Thanks [@TheJoeFin](https://github.com/TheJoeFin)!

### Settings

 - Styling fixes for Peek and Mouse Without Borders pages. (This was a hotfix for 0.70) Thanks [@niels9001](https://github.com/niels9001)!
 - Fixed Mouse Without Borders machine connection status styling. (This was a hotfix for 0.70)
 - Improved Mouse Without Border page Uninstall service UX when it is inaccessible. (This was a hotfix for 0.70)
 - Updated File Explorer module screenshots and instructions to reflect the Windows 11 File Explorer. Thanks [@infinitepower18](https://github.com/infinitepower18)!
 - Modernized the app title bar and styling (Mica background material) to be inline with Windows11 guidelines. Thanks [@niels9001](https://github.com/niels9001)!
 - Improved error handling on settings backup failure.
 - Added Reset button to shortcut control to reset activation shortcut to default value. Thanks [@Svenlaa](https://github.com/Svenlaa)!
 - Improved Exclude apps setting for all modules to also detect apps by title.

### Development

 - Added Peek and Mouse Without Borders to GitHub templates. (This was a hotfix for 0.70)
 - Fixed the CI release pipelines winget package submission. (This was a hotfix for 0.70)
 - Fixed process report and termination lists for Peek and Mouse Without Borders. (This was a hotfix for 0.70)
 - Added Winget configuration file. (This was a hotfix for 0.70) Thanks [@ryfu-msft](https://github.com/ryfu-msft)!
 - Fixed tests localization issues.
 - Added Microsoft.VisualStudio.Component.VC.ATL library to .vsconfig.
 - Onboarding to GitOps.ResourceManagement.

#### What is being planned for version 0.72

For [v0.72][github-next-release-work], we'll work on below:

 - Adjustments on feedback / stability / bug fixes
 - Modernize and refresh UX of PowerToys based on WPF

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
[github-next-release-work]: https://github.com/microsoft/PowerToys/issues?q=is%3Aopen+is%3Aissue+project%3Amicrosoft%2FPowerToys%2F45
[github-current-release-work]: https://github.com/microsoft/PowerToys/issues?q=is%3Aopen+is%3Aissue+project%3Amicrosoft%2FPowerToys%2F44

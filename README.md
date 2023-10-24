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
| [Crop And Lock](https://aka.ms/PowerToysOverview_CropAndLock) | [Environment Variables](https://aka.ms/PowerToysOverview_EnvironmentVariables) | [FancyZones](https://aka.ms/PowerToysOverview_FancyZones) |
| [File Explorer Add-ons](https://aka.ms/PowerToysOverview_FileExplorerAddOns) | [File Locksmith](https://aka.ms/PowerToysOverview_FileLocksmith) | [Hosts File Editor](https://aka.ms/PowerToysOverview_HostsFileEditor) |
| [Image Resizer](https://aka.ms/PowerToysOverview_ImageResizer) | [Keyboard Manager](https://aka.ms/PowerToysOverview_KeyboardManager) | [Mouse utilities](https://aka.ms/PowerToysOverview_MouseUtilities) |
| [Mouse Without Borders](https://aka.ms/PowerToysOverview_MouseWithoutBorders) | [Peek](https://aka.ms/PowerToysOverview_Peek) | [Paste as Plain Text](https://aka.ms/PowerToysOverview_PastePlain) |
| [PowerRename](https://aka.ms/PowerToysOverview_PowerRename) | [PowerToys Run](https://aka.ms/PowerToysOverview_PowerToysRun) | [Quick Accent](https://aka.ms/PowerToysOverview_QuickAccent) |
| [Registry Preview](https://aka.ms/PowerToysOverview_RegistryPreview) | [Screen Ruler](https://aka.ms/PowerToysOverview_ScreenRuler) | [Shortcut Guide](https://aka.ms/PowerToysOverview_ShortcutGuide) |
| [Text Extractor](https://aka.ms/PowerToysOverview_TextExtractor) | [Video Conference Mute](https://aka.ms/PowerToysOverview_VideoConference) |

## Installing and running Microsoft PowerToys

### Requirements

- Windows 11 or Windows 10 version 2004 (code name 20H1 / build number 19041) or newer.
- Our installer will install the following items:
   - [Microsoft Edge WebView2 Runtime](https://go.microsoft.com/fwlink/p/?LinkId=2124703) bootstrapper. This will install the latest version.

### Via GitHub with EXE [Recommended]

Go to the [Microsoft PowerToys GitHub releases page][github-release-link] and click on `Assets` at the bottom to show the files available in the release. Please use the appropriate PowerToys installer that matches your machine's architecture and install scope. For most, it is `x64` and per-user.

<!-- items that need to be updated release to release -->
[github-next-release-work]: https://github.com/microsoft/PowerToys/issues?q=project%3Amicrosoft%2FPowerToys%2F48
[github-current-release-work]: https://github.com/microsoft/PowerToys/issues?q=project%3Amicrosoft%2FPowerToys%2F47
[ptUserX64]: https://github.com/microsoft/PowerToys/releases/download/v0.74.1/PowerToysUserSetup-0.74.1-x64.exe
[ptUserArm64]: https://github.com/microsoft/PowerToys/releases/download/v0.74.1/PowerToysUserSetup-0.74.1-arm64.exe
[ptMachineX64]: https://github.com/microsoft/PowerToys/releases/download/v0.74.1/PowerToysSetup-0.74.1-x64.exe
[ptMachineArm64]: https://github.com/microsoft/PowerToys/releases/download/v0.74.1/PowerToysSetup-0.74.1-arm64.exe

|  Description   | Filename | sha256 hash |
|----------------|----------|-------------|
| Per user - x64       | [PowerToysUserSetup-0.74.1-x64.exe][ptUserX64] | 748BF7BA33913237D36D6F48E3839D0C8035967305137A17DEFF39D775735C81 | 
| Per user - ARM64     | [PowerToysUserSetup-0.74.1-arm64.exe][ptUserArm64] | F5DAA89A9CF3A2805E121085AFD056A890F241A170FAB5007AA58E2755C88C54 | 
| Machine wide - x64   | [PowerToysSetup-0.74.1-x64.exe][ptMachineX64] | 298C6F4E4391BDC06E128BED86A303C3300A68EAF754B4630AF7542C78C0944A | 
| Machine wide - ARM64 | [PowerToysSetup-0.74.1-arm64.exe][ptMachineArm64] | A65F3C300A48F9F81312B7FC7B306382CB87F591612D0CEC7E5C0E47E868904B |

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

## Third-Party Run Plugins

There is a collection of [third-party plugins](./doc/thirdPartyRunPlugins.md) created by the community that aren't distributed with PowerToys.

## Contributing

This project welcomes contributions of all types.  Besides  coding features / bug fixes,  other ways to assist include spec writing, design, documentation, and finding bugs. We are excited to work with the power user community to build a set of tools for helping you get the most out of Windows.

We ask that **before you start work on a feature that you would like to contribute**, please read our [Contributor's Guide](CONTRIBUTING.md). We would be happy to work with you to figure out the best approach, provide guidance and mentorship throughout feature development, and help avoid any wasted or duplicate effort.

Most contributions require you to agree to a [Contributor License Agreement (CLA)][oss-CLA] declaring that you grant us the rights to use your contribution and that you have permission to do so.

For guidance on developing for PowerToys, please read the [developer docs](/doc/devdocs) for a detailed breakdown. This includes how to setup your computer to compile.

## What's Happening

### PowerToys Roadmap

Our [prioritized roadmap][roadmap] of features and utilities that the core team is focusing on.

### 0.74 - September 2023 Update

In this release, we focused on stability and improvements.

**Highlights**

 - Upgraded to Windows App SDK 1.4.1, increasing stability of WinUI3 utilities. Thanks [@dongle-the-gadget](https://github.com/dongle-the-gadget) for starting the upgrade!
 - Text Extractor was upgraded to its version 2.0, with a new overlay, table mode and more Quality of Life improvements. Thanks [@TheJoeFin](https://github.com/TheJoeFin)!
 - Improved FancyZones stability, fixing some layout resets and improving handling of newly created windows on Windows 11.
 - Fixed many silent crashes that were reported to Watson and the user's event viewer.

### General

 - Turning animations off in Windows Settings will now also turn them off in PowerToys.
 - Upgraded the Windows App SDK dependency to 1.4.1. Thanks [@dongle-the-gadget](https://github.com/dongle-the-gadget) for the original 1.4.0 upgrade!
 - Show in the thumbnail label and application titles when running as administrator. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Upgraded the Win UI Community Toolkit dependency to 8.0. Thanks [@niels9001](https://github.com/niels9001)!

### Awake

 - Added down-sampled variants to the application's icon. Thanks [@morriscurtis](https://github.com/morriscurtis)!

### Color Picker

 - After adding a new color in the editor, the history will scroll the new color into view. Thanks [@peerpalo](https://github.com/peerpalo)!

### Crop and Lock
 - Fixed a Crop and Lock crash that would occur when trying to reparent a window crashes the target application. An error message is shown instead.

### FancyZones

 - Set the process and main thread priority to normal.
 - Fixed handling newly created windows on Windows 11.
 - Fixed scenarios where opening the FancyZones Editor would reset the layouts.

### File Explorer add-ons

 - Optimized CPU usage for generating SVG thumbnails.
 - Improved handling of Gcode Thumbnails, including JPG and QOI formats. Thanks [@pedrolamas](https://github.com/pedrolamas)!
 - Better handled errors when sending telemetry, which were causing reported crashes.
 - Fixed some thumbnails not being shown centered like before the optimization.

### File Locksmith

 - Shows files opened by processes with PID greater than 65535. Thanks [@poke30744](https://github.com/poke30744)!
 - Fixed a GDI object leak in the context menu which would crash Explorer.
 
### Find My Mouse

 - Added new activation methods, including by hotkey. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!

### Hosts File Editor

 - Ignore the default ACME sample entries in the hosts file. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Improved save error handling and added better error messages. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Corrected a check for an error when signaling the application to start as administrator.
 - Refactored the context menu. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Fixed dialogs overlapping the title bar after the upgrade to Windows App SDK 1.4. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!

### Keyboard Manager

 - Distinguish between the regular minus key and the numpad minus key.

### Mouse Without Borders

 - Fixed a crash when trying to restart the application.

### Peek

 - Using Peek on HTML files will show a white background by default, similar to a browser's default behavior.
 - Fix a white flash on Dark theme when switching file and improved the development file preview detection and adjustments.

### PowerRename

 - Fixed a crash caused by big counter values on the new enumeration method.

### PowerToys Run

 - It's now possible to select which shell is used by the Shell plugin.
 - A combobox option type was added to the plugin options.
 - Fixed a bug in the Calculator plugin that was causing decimal numbers to be misinterpreted on locales where the dot (`.`) character isn't used as a decimal or digit separator.
 - Improved the Program plugin stability when it fails to load a program's thumbnail at startup.
 - The use of Pinyin for querying some plugins can now be turned on in Settings. Thanks [@ChaseKnowlden](https://github.com/ChaseKnowlden)!
 - Refactored option types for plugin and added number, string and composite types to be used in the future. Thanks [@htcfreek](https://github.com/htcfreek)!
 - Fixed the entry for searching for Windows updates in the Settings plugin. Thanks [@htcfreek](https://github.com/htcfreek)!

### Quick Accent

 - The "All languages" character set is now calculated by programmatically querying the characters for every available language. Thanks [@dannysummerlin](https://github.com/dannysummerlin)!
 - Added é to the Norwegian and Swedish languages. Thanks [@Aaron-Junker](https://github.com/Aaron-Junker)!
 - Added a runtime cache to the "All languages" character set, to only calculate accents once per key.

### Registry Preview

 - Fixed focusing issues at startup.
 - Improved the data visualization to show data in a similar way to the Windows Registry Editor. Thanks [@dillydylann](https://github.com/dillydylann)!

### Runner

 - Fixed hanging when a bug report was generated from the flyout. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!

### Settings

 - Improved the way the OOBE window reacts to Windows theme change.
 - Fixed an issue that made it impossible to change the "Switch between windows in the current zone" "Next window" shortcut for FancyZones.
 - Fixed a crash when entering a duplicate name for a color in the Color Picker page and improved clean up when cancelling a color edit. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!

### Text Extractor

 - Text Extractor 2.0, with a new overlay, table mode and more Quality of Life improvements. Thanks [@TheJoeFin](https://github.com/TheJoeFin)!

### Documentation

 - SECURITY.md was updated from 0.0.2 to 0.0.9. Thanks [@Aaron-Junker](https://github.com/Aaron-Junker)!
 - Improved the README and main development document for clarity and completeness. Thanks [@codeofdusk](https://github.com/codeofdusk) and [@aprilbbrockhoeft](https://github.com/aprilbbrockhoeft)!

### Development

 - Fixed PowerToys Run DateTime plugin tests that were failing depending on locale, so that they can be run correctly on all dev machines.
 - Fixed PowerToys Run System plugin tests that were failing for certain network interfaces, so that they can be run correctly on all dev machines. Thanks [@snickler](https://github.com/snickler)!
 - Fixed a markdown bug on the GitHub /helped command.
 - Switched build pipelines to a new agent pool. Thanks [@DHowett](https://github.com/DHowett)!
 - New .cs files created in Visual Studio get the header added automatically. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!

#### What is being planned for version 0.75

For [v0.75][github-next-release-work], we'll work on the items below:

 - Language selection
 - .NET 8 upgrade
 - Policy support for managing PowerToys Run plugins.
*Attention*: A breaking change is planned (for 0.75), in which each plugin has to declare its identifier programmatically so that it can be controlled through GPO. For third-party plugin developers, please check https://github.com/microsoft/PowerToys/pull/27468 for more details.

 - New utility: Environment Variables Editor. Here's a Work in Progress preview:

![Environment Variables Editor WIP](https://github.com/microsoft/PowerToys/assets/26118718/f99532a8-5aae-481b-a662-19a95f4aa03d)

 - New Settings homepage. Here's a Work in Progress preview:

![PowerToys Settings Dashboard WIP](https://github.com/microsoft/PowerToys/assets/26118718/938a5715-0a9b-4fe9-9e15-adfec92da694)

 - Modernize and refresh the UX of PowerToys based on WPF. Here's Work in Progress previews for the modules "PowerToys Run" and "Color Picker":

![PowerToys Run UI refresh WIP](https://github.com/microsoft/PowerToys/assets/9866362/16903bcb-c18e-49fb-93ca-738b81957055)

![ColorPicker UI refresh WIP](https://github.com/microsoft/PowerToys/assets/9866362/ceebe54b-de63-4ce7-afcb-2cd4280bf4d1)

 - Stability / bug fixes

## PowerToys Community

The PowerToys team is extremely grateful to have the [support of an amazing active community][community-link]. The work you do is incredibly important. PowerToys wouldn’t be nearly what it is today without your help filing bugs, updating documentation, guiding the design, or writing features. We want to say thank you and take time to recognize your work.  Month by month, you directly help make PowerToys a better piece of software.

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

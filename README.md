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
| [Crop And Lock](https://aka.ms/PowerToysOverview_CropAndLock) | [FancyZones](https://aka.ms/PowerToysOverview_FancyZones) | [File Explorer Add-ons](https://aka.ms/PowerToysOverview_FileExplorerAddOns) |
| [File Locksmith](https://aka.ms/PowerToysOverview_FileLocksmith) | [Hosts File Editor](https://aka.ms/PowerToysOverview_HostsFileEditor) | [Image Resizer](https://aka.ms/PowerToysOverview_ImageResizer) |
| [Keyboard Manager](https://aka.ms/PowerToysOverview_KeyboardManager) | [Mouse utilities](https://aka.ms/PowerToysOverview_MouseUtilities) | [Mouse Without Borders](https://aka.ms/PowerToysOverview_MouseWithoutBorders) |
| [Peek](https://aka.ms/PowerToysOverview_Peek) | [Paste as Plain Text](https://aka.ms/PowerToysOverview_PastePlain) | [PowerRename](https://aka.ms/PowerToysOverview_PowerRename) |
| [PowerToys Run](https://aka.ms/PowerToysOverview_PowerToysRun) | [Quick Accent](https://aka.ms/PowerToysOverview_QuickAccent) | [Registry Preview](https://aka.ms/PowerToysOverview_RegistryPreview) |
| [Screen Ruler](https://aka.ms/PowerToysOverview_ScreenRuler) | [Shortcut Guide](https://aka.ms/PowerToysOverview_ShortcutGuide) | [Text Extractor](https://aka.ms/PowerToysOverview_TextExtractor) |
| [Video Conference Mute](https://aka.ms/PowerToysOverview_VideoConference) |

## Installing and running Microsoft PowerToys

### Requirements

- Windows 11 or Windows 10 version 2004 (code name 20H1 / build number 19041) or newer.
- Our installer will install the following items:
   - [Microsoft Edge WebView2 Runtime](https://go.microsoft.com/fwlink/p/?LinkId=2124703) bootstrapper. This will install the latest version.

### Via GitHub with EXE [Recommended]

Go to [Microsoft PowerToys GitHub releases page][github-release-link], click on `Assets` at the bottom to show the files available in the release. Please use the appropriate PowerToys installer that matches your machine's architecture and install scope. For most, it is `x64` and per-user.

<!-- items that need to be updated release to release -->
[github-next-release-work]: https://github.com/microsoft/PowerToys/issues?q=project%3Amicrosoft%2FPowerToys%2F47
[github-current-release-work]: https://github.com/microsoft/PowerToys/issues?q=project%3Amicrosoft%2FPowerToys%2F46
[ptUserX64]: https://github.com/microsoft/PowerToys/releases/download/v0.73.0/PowerToysUserSetup-0.73.0-x64.exe
[ptUserArm64]: https://github.com/microsoft/PowerToys/releases/download/v0.73.0/PowerToysUserSetup-0.73.0-arm64.exe
[ptMachineX64]: https://github.com/microsoft/PowerToys/releases/download/v0.73.0/PowerToysSetup-0.73.0-x64.exe
[ptMachineArm64]: https://github.com/microsoft/PowerToys/releases/download/v0.73.0/PowerToysSetup-0.73.0-arm64.exe

|  Description   | Filename | sha256 hash |
|----------------|----------|-------------|
| Per user - x64       | [PowerToysUserSetup-0.73.0-x64.exe][ptUserX64] | BA55D245BDD734FD6F19803DD706A3AB8E0ABC491591195534997CF2122D3B7E |
| Per user - ARM64     | [PowerToysUserSetup-0.73.0-arm64.exe][ptUserArm64] | FBFA40EA5FFA05236A7CCDD05E5142EE0C93D7485B965784196ED9B086BFEBF4 |
| Machine wide - x64   | [PowerToysSetup-0.73.0-x64.exe][ptMachineX64] | 7FDA06292C7C2E6DA5AEF88D8E9D3DE89D331E9E356A232289F9B37CE4503894 |
| Machine wide - ARM64 | [PowerToysSetup-0.73.0-arm64.exe][ptMachineArm64] | 4260AA30A1F52F194EE07E9E7ECD9E9F4CF35289267F213BC933F7A5191AC17C |

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

Collection of [third-party plugins](./doc/thirdPartyRunPlugins.md) created by the community that aren't distributed with PowerToys.

## Contributing

This project welcomes contributions of all types. Help spec'ing, design, documentation, finding bugs are ways everyone can help on top of coding features / bug fixes. We are excited to work with the power user community to build a set of tools for helping you get the most out of Windows.

We ask that **before you start work on a feature that you would like to contribute**, please read our [Contributor's Guide](CONTRIBUTING.md). We will be happy to work with you to figure out the best approach, provide guidance and mentorship throughout feature development, and help avoid any wasted or duplicate effort.

Most contributions require you to agree to a [Contributor License Agreement (CLA)][oss-CLA] declaring that you have the right to, and actually do, grant us the rights to use your contribution.

For guidance on developing for PowerToys, please read the [developer docs](/doc/devdocs) for a detailed breakdown. This includes how to setup your computer to compile.

## What's Happening

### PowerToys Roadmap

Our [prioritized roadmap][roadmap] of features and utilities that the core team is focusing on.

### 0.73 - August 2023 Update

In this release, we focused on releasing new features, stability and improvements.

**Highlights**

 - Keyboard manager now supports Numpad. Note, with previously bound hotkeys stored in settings.json would only react to non-Numpad keys now. If a user wishes to restore the previous behavior, it could be done by manually adding another binding for the Numpad variant.
 - New utility: Crop And Lock allows you to crop a current application into a smaller window or just create a thumbnail. Focus the target window and press the shortcut to start cropping.
 - FancyZones code improvements and refactor.
 - Modernized ImageResizer UX.
 - PowerRename advanced counter functionality.

### General

 - Added missing CoUninitialize call in elevation logic. Thanks [@sredna](https://github.com/sredna)!
 - New utility: Crop And Lock. Thanks [@robmikh](https://github.com/robmikh)! and [@kevinguo305](https://github.com/kevinguo305)!
 - Added new /helped fabric bot command to GitHub repo. Thanks [@Aaron-Junker](https://github.com/Aaron-Junker)!
 - Fixed crashes caused by invalid settings. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!

### Always On Top

 - Added border transparency.

### FancyZones

 - Fixed issue causing canvas zones being drawn only when dragging in zone area.
 - Fixed user-defined default layout highlighting issue.
 - Refactored and improved code quality.
 - Fixed issue causing wrong layout to be applied when duplicating non-selected layout.

### File Locksmith

 - Icon update. Thanks [@jmaraujouy](https://github.com/jmaraujouy)!
 
### File Explorer add-ons

 - Fixed issue causing thumbnail previewers to lock files.
 - Open URIs from developer files in default browser. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!

### Installer

 - Fixed PowerToys autorun after installing as SYSTEM user. 
 - Removed CreateScheduledTask custom action to handle task creation only from runner code.

### Image Resizer

 -  Moved from ModernWPF to WpfUI to refresh and modernize UI/UX. Thanks [@niels9001](https://github.com/niels9001)!

### Keyboard Manager

 - Rephrased labels to enhance clarity. Thanks [@Jay-o-Way](https://github.com/Jay-o-Way)!
 - Keyboard manager now supports Numpad. Note, with previously bound hotkeys stored in settings.json would only react to non-Numpad keys now. If a user wishes to restore the previous behavior, it could be done by manually adding another binding for the Numpad variant.

### Mouse Highlighter

 - Fixed highlighter being invisible issue for Always on Top windows.
 - Added settings for automatic activation on startup. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!

### Mouse Pointer Crosshairs

 - Added settings for automatic activation on startup. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!

### Peek

 - Show correct file type for shortcuts. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Fixed issue causing wrong file size to be displayed. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Show 1 byte instead of 1 bytes file size. Thanks [@Deepak-Sangle](https://github.com/Deepak-Sangle)!
 - Open URIs from developer files in default browser. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Show thumbnail and fallback to icon for unsupported files. Thanks [@pedrolamas](https://github.com/pedrolamas)!

### PowerRename

 - Updated OOBE gif. Thanks [@ChaseKnowlden](https://github.com/ChaseKnowlden)!
 - Localized renamed parts combo box.
 - Introduced advanced counter functionality.
 - Added remember last window size logic and optimized items sorting.
 - Enable "Enumerate items" option by default.

### PowerToys Run

 - Fixed issue causing original search to be abandoned when cycling through results.
 - Updated device and bluetooth results for Settings plugin. Thanks [@htcfreek](https://github.com/htcfreek)!
 - Fixed InvalidOperationException exception thrown. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Add Base64 Decoding function to the Value Generator plugin. Thanks [@LeagueOfPoro](https://github.com/LeagueOfPoro)!
 - Added Keep shell open option for Shell plugin.
 - Added Crop And Lock to PowerToys plugin. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!

### Registry Preview

 - Updated AppBarButtons to use an explicit AppBarButton.Icon. Thanks [@randyrants](https://github.com/randyrants)!
 - Fixed crash on clicking Save As button.

### Runner

 - Removed unneeded RegisterWindowMessage from tray icon logic. Thanks [@sredna](https://github.com/sredna)!
 - Fixed startup looping issue.
 - Improved old logs and installers cleanup logic. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!

### Screen Ruler

 - Use proper resources file.

### Settings

 - Fixed issue causing problems with modifier keys and ShortcutControl. Thanks [@sh0ckj0ckey](https://github.com/sh0ckj0ckey)!
 - Fixed crash when clicking "Windows color settings" link.
 - Added support for launching Settings app directly.
 - Fixed issue causing DisplayDescription not showing for PowerToys Run PluginAdditionalOption.
 - Fixed issue causing FileLocksmith 'Show File Locksmith in' setting not showing correct value.
 - Fixed issue causing Awake on/off toggle in Settings flyout not to work when Settings Awake page is opened.

### Documentation

 - Added documentation for PowerToys Run third-party plugins. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Fixed broken links in keyboardmanagerui.md. Thanks [@shubhsardana29](https://github.com/shubhsardana29)!
 - Updated core team in COMMUNITY.md.
 - Fixed broken links in ui-architecture.md. Thanks [@SamB](https://github.com/SamB)!
 - Updated community.valuegenerator.md with Base64DecodeRequest description.

### Development

 - Updated test packages and StyleCop. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Condense NuGet Restore into MSBuild Tasks. Thanks [@snickler](https://github.com/snickler)!

#### What is being planned for version 0.74

For [v0.74][github-next-release-work], we'll work on below:

 - Language selection
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

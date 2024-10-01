# Microsoft PowerToys

![Hero image for Microsoft PowerToys](doc/images/overview/PT_hero_image.png)

[How to use PowerToys][usingPowerToys-docs-link] | [Downloads & Release notes][github-release-link] | [Contributing to PowerToys](#contributing) | [What's Happening](#whats-happening) | [Roadmap](#powertoys-roadmap)

## About

Microsoft PowerToys is a set of utilities for power users to tune and streamline their Windows experience for greater productivity. For more info on [PowerToys overviews and how to use the utilities][usingPowerToys-docs-link], or any other tools and resources for [Windows development environments](https://learn.microsoft.com/windows/dev-environment/overview), head over to [learn.microsoft.com][usingPowerToys-docs-link]!

|              | Current utilities: |              |
|--------------|--------------------|--------------|
| [Advanced Paste](https://aka.ms/PowerToysOverview_AdvancedPaste) | [Always on Top](https://aka.ms/PowerToysOverview_AoT) | [PowerToys Awake](https://aka.ms/PowerToysOverview_Awake) |
| [Command Not Found](https://aka.ms/PowerToysOverview_CmdNotFound) | [Color Picker](https://aka.ms/PowerToysOverview_ColorPicker) | [Crop And Lock](https://aka.ms/PowerToysOverview_CropAndLock) |
| [Environment Variables](https://aka.ms/PowerToysOverview_EnvironmentVariables) | [FancyZones](https://aka.ms/PowerToysOverview_FancyZones) | [File Explorer Add-ons](https://aka.ms/PowerToysOverview_FileExplorerAddOns) |
| [File Locksmith](https://aka.ms/PowerToysOverview_FileLocksmith) | [Hosts File Editor](https://aka.ms/PowerToysOverview_HostsFileEditor) | [Image Resizer](https://aka.ms/PowerToysOverview_ImageResizer) |
| [Keyboard Manager](https://aka.ms/PowerToysOverview_KeyboardManager) | [Mouse utilities](https://aka.ms/PowerToysOverview_MouseUtilities) | [Mouse Without Borders](https://aka.ms/PowerToysOverview_MouseWithoutBorders) |
| [New+](https://aka.ms/PowerToysOverview_NewPlus) | [Peek](https://aka.ms/PowerToysOverview_Peek) | [Paste as Plain Text](https://aka.ms/PowerToysOverview_PastePlain) |
| [PowerRename](https://aka.ms/PowerToysOverview_PowerRename) | [PowerToys Run](https://aka.ms/PowerToysOverview_PowerToysRun) | [Quick Accent](https://aka.ms/PowerToysOverview_QuickAccent) |
| [Registry Preview](https://aka.ms/PowerToysOverview_RegistryPreview) | [Screen Ruler](https://aka.ms/PowerToysOverview_ScreenRuler) | [Shortcut Guide](https://aka.ms/PowerToysOverview_ShortcutGuide) |
| [Text Extractor](https://aka.ms/PowerToysOverview_TextExtractor) | [Video Conference Mute](https://aka.ms/PowerToysOverview_VideoConference) | [Workspaces](https://aka.ms/PowerToysOverview_Workspaces) |

## Installing and running Microsoft PowerToys

### Requirements

- Windows 11 or Windows 10 version 2004 (code name 20H1 / build number 19041) or newer.
- x64 or ARM64 processor
- Our installer will install the following items:
   - [Microsoft Edge WebView2 Runtime](https://go.microsoft.com/fwlink/p/?LinkId=2124703) bootstrapper. This will install the latest version.

### Via GitHub with EXE [Recommended]

Go to the [Microsoft PowerToys GitHub releases page][github-release-link] and click on `Assets` at the bottom to show the files available in the release. Please use the appropriate PowerToys installer that matches your machine's architecture and install scope. For most, it is `x64` and per-user.

<!-- items that need to be updated release to release -->
[github-next-release-work]: https://github.com/microsoft/PowerToys/issues?q=is%3Aissue+milestone%3A%22PowerToys+0.86%22
[github-current-release-work]: https://github.com/microsoft/PowerToys/issues?q=is%3Aissue+milestone%3A%22PowerToys+0.85%22
[ptUserX64]: https://github.com/microsoft/PowerToys/releases/download/v0.85.0/PowerToysUserSetup-0.85.0-x64.exe
[ptUserArm64]: https://github.com/microsoft/PowerToys/releases/download/v0.85.0/PowerToysUserSetup-0.85.0-arm64.exe
[ptMachineX64]: https://github.com/microsoft/PowerToys/releases/download/v0.85.0/PowerToysSetup-0.85.0-x64.exe
[ptMachineArm64]: https://github.com/microsoft/PowerToys/releases/download/v0.85.0/PowerToysSetup-0.85.0-arm64.exe
 
|  Description   | Filename | sha256 hash |
|----------------|----------|-------------|
| Per user - x64       | [PowerToysUserSetup-0.85.0-x64.exe][ptUserX64] | 28A8BEA61040751287FF47C9BAC627A53A4670CFEA0C17B96EE947219E9A6EA9 |
| Per user - ARM64     | [PowerToysUserSetup-0.85.0-arm64.exe][ptUserArm64] | 2CA077E842B7C53BAFC75A25DBD16C1A4FCE20924C36FDA5AD8CF23CD836B855 |
| Machine wide - x64   | [PowerToysSetup-0.85.0-x64.exe][ptMachineX64] | 4A248AA914EEE339AA99D467FDFBDB1FCD7A49A8564DDBBB811D0EC69CEBAB75 |
| Machine wide - ARM64 | [PowerToysSetup-0.85.0-arm64.exe][ptMachineArm64] | B5FB04EAF44C4203E785411FF55025842B9C39D4970C0C934CB8ADBE79EF31AF |

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

### 0.85 - September 2024 Update

In this release, we focused on new features, stability, and improvements.

**Highlights**

 - New utility: New+ - allows setting a personalized set of templates to quickly create files and folders from a File Explorer context menu. Thanks [@cgaarden](https://github.com/cgaarden)!
 - Language selection - it's now possible to select which UI language should be used by PowerToys utilities.
 - Lots of quality fixes for Workspaces, improving the number of supported applications.
 - Reduced Peek memory usage by fixing image leaks. Thanks [@daverayment](https://github.com/daverayment)!

### General

 - Added a general setting to select which UI language should be used in PowerToys utilities.
 - Fixed internal code of some policies for Group Policy Objects, that were reading registry entries using the wrong internal functions, and structured code better to avoid future mistakes of the same kind. Thanks [@htcfreek](https://github.com/htcfreek)!

### Advanced Paste

 - Fixed some telemetry calls to signal Advanced Paste activation on the cases where a direct shortcut is being used without showing the UI.
 - User-defined custom actions can only be used with AI turned on, so custom actions were disabled on Settings when AI is disabled and were hidden from the Advanced Paste UI.

### Awake

 - Fixed tray icon behaviors, not appearing and showing incorrect time. Thanks [@dend](https://github.com/dend)!

### Environment Variables Editor

 - Added the `_NT_SYMBOL_PATH`, `_NT_ALT_SYMBOL_PATH` and `_NT_SYMCACHE_PATH` as variables that are shown as lists. Thanks [@chwarr](https://github.com/chwarr)!

### FancyZones

 - Allow snapping applications that were launched by Workspaces.

### File Locksmith

 - Fixed an issue causing File Locksmith to be triggered by unrelated verbs in the context menu.

### Mouse Pointer Crosshairs

 - Allow crosshairs radius to be 0 pixels. Thanks [@octastylos-pseudodipteros](https://github.com/octastylos-pseudodipteros)!

### New+

 - New utility - Allows setting a personalized set of templates to quickly create files and folders from a File Explorer context menu. Thanks [@cgaarden](https://github.com/cgaarden)!
 - Added missing entry for New+ policy state reporting in the Bug Report tool. Thanks [@htcfreek](https://github.com/htcfreek)!
 - Added a policy for enabling/disabling whether filename extensions should be shown. Thanks [@htcfreek](https://github.com/htcfreek)!

### Peek

 - Properly show file's modified date instead of creation date in the file previewer. Thanks [@daverayment](https://github.com/daverayment)!
 - Fixed memory leak caused by unmanaged bitmap images not being freed. Thanks [@daverayment](https://github.com/daverayment)!
 - Fixed an issue causing Peek to not be displayed the first time when using a preview handler to display files. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Prevent tooltip in file previewer from overlapping with title bar controls. Thanks [@daverayment](https://github.com/daverayment)!
 - Fixed memory leaks in thumbnails and refactored image previewer. Thanks [@daverayment](https://github.com/daverayment)!

### PowerToys Run

 - Improved the message boxes to be more specific when PowerToys Run failed to initialize itself or any plugin. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Use capital letters when showing degree results in the Unit Converter plugin. Thanks [@PesBandi](https://github.com/PesBandi)!

### Quick Accent

 - Add the Middle Eastern Romanization character set. Thanks [@PesBandi](https://github.com/PesBandi)!
 - Add the degree sign, integral and vertical ellipsis when "All Languages" is selected. Thanks [@rddunphy](https://github.com/rddunphy)!

### Settings

 - Fixed the link to the Workspaces documentation. (This was a hotfix for 0.84)
 - Fixed flyout issues after the Windows App SDK upgrade. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Fixed initialization for the New+ settings page. Thanks [@htcfreek](https://github.com/htcfreek)!
 - Fixed enabled state of a control on the New+ settings page if the module is enabled by policy. Thanks [@htcfreek](https://github.com/htcfreek)!
 - Fixed a crash when cancelling the template folder selection in the New+ settings page.

### Workspaces

 - Fixed detecting and snapping applications like Discord. (This was a hotfix for 0.84)
 - Fixed detecting and snapping applications like Steam. (This was a hotfix for 0.84)
 - Fixed button visibility in the UI. (This was a hotfix for 0.84)
 - Fixed an issue launching the wrong project when the editor was closed without saving or cancelling a new project.
 - Properly handle repositioning windows running as administrator.
 - Properly handle cases where the monitor where a workspace was saved is no longer present.
 - Fixed the workspace launcher restarting itself in a loop without success.
 - Properly handle standalone applications.
 - Fixed issues causing icons to not show.

### Documentation

 - Fixed the thirdPartyRunPlugins.md entry for the RDP plugin. Thanks [@YisroelTech](https://github.com/YisroelTech)! 

### Development

 - Upgraded Windows App SDK to 1.6.
 - Upgraded the Target Platform Version to 10.0.22621.0.
 - Added a bot trigger to automatically add a label to Workspaces issues. Thanks [@plante-msft](https://github.com/plante-msft)!
 - Fixed a regular expression in the bot triggers for wanting to submit community contributions. Thanks [@PesBandi](https://github.com/PesBandi)!
 - Fixed analyzer errors after the Visual Studio 17.12 update. Thanks [@snickler](https://github.com/snickler)!
 - Fixed the TSA configuration for release CI builds.
 - Refactored automated file component generation during installer builds.
 - Rewrote the Azure Devops build system to be more modular and share more definitions between PR CI and Release CI.
 - Fixed debugging of the New+ page of the Settings application when a settings file was not present.
 - Fixed setting the version of the App Manifest in the File Locksmith and New+ context menu app packages.
 - Fixed abstracted UI library nuget package signing on release CI.
 - Removed build status from GitHub README.

#### What is being planned for version 0.86

For [v0.86][github-next-release-work], we'll work on the items below:

 - Stability / bug fixes
 - New module: File Actions Menu
 - Integrate Sysinternals ZoomIt

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

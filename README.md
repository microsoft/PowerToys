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
[github-next-release-work]: https://github.com/microsoft/PowerToys/issues?q=is%3Aissue+milestone%3A%22PowerToys+0.88%22
[github-current-release-work]: https://github.com/microsoft/PowerToys/issues?q=is%3Aissue+milestone%3A%22PowerToys+0.87%22
[ptUserX64]: https://github.com/microsoft/PowerToys/releases/download/v0.87.1/PowerToysUserSetup-0.87.1-x64.exe 
[ptUserArm64]: https://github.com/microsoft/PowerToys/releases/download/v0.87.1/PowerToysUserSetup-0.87.1-arm64.exe 
[ptMachineX64]: https://github.com/microsoft/PowerToys/releases/download/v0.87.1/PowerToysSetup-0.87.1-x64.exe 
[ptMachineArm64]: https://github.com/microsoft/PowerToys/releases/download/v0.87.1/PowerToysSetup-0.87.1-arm64.exe
 
|  Description   | Filename | sha256 hash |
|----------------|----------|-------------|
| Per user - x64       | [PowerToysUserSetup-0.87.1-x64.exe][ptUserX64] | 8EFAF47ED00BF230D2C2CC3CB6765C903A6A47E0AAED0BBB329CEF918207B486 |
| Per user - ARM64     | [PowerToysUserSetup-0.87.1-arm64.exe][ptUserArm64] | 212FC8055789BD2DC4DE554B9AEE291A9C077907E263A302939266263A9D512B |
| Machine wide - x64   | [PowerToysSetup-0.87.1-x64.exe][ptMachineX64] | 69AD65DDAC6436AEF292D2CC6AB1530021CE98083CB3F5FD3380A52A3B0DBB9A |
| Machine wide - ARM64 | [PowerToysSetup-0.87.1-arm64.exe][ptMachineArm64] | AEC9F1D02F1E23F0C1FCFDF95C337C962902394F44C0568012DF78BEDB45CF19 |

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

### 0.87 - December 2024 Update

In this release, we focused on new features, stability, and improvements.

**Highlights**

 - Advanced Paste has a new feature called "Advanced AI" that uses Semantic Kernel to allow setting up the orchestration of sequential clipboard transformations.
 - Workspaces supports Progressive Web Applications.
 - Workspaces has a new feature to move existing windows instead of creating new ones.
 - Mouse Jump added new settings to allow customization of screens pop-up. Thanks [@mikeclayton](https://github.com/mikeclayton)!
 - New+ now works on Windows 10. Thanks [@cgaarden](https://github.com/cgaarden)!
 - Quick Accent allows selecting the character sets that should appear on the UI. Thanks [@Sirozha1337](https://github.com/Sirozha1337)!

### Advanced Paste

 - Added a new optional feature allowing using AI to set up the orchestration of sequential clipboard transformations.

### Awake

 - Initialization, logging and tray icon setup improvements. Thanks [@dend](https://github.com/dend)!

### File Explorer add-ons

 - Preview Pane extensions now use the PerMonitorV2 DPI mode to fix errors on different scales. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!

### Keyboard Manager.

 - Added labels to the IME On, IME Off keys. Thanks [@kit494way](https://github.com/kit494way)!
 - Fixed an issue that caused the Shift key to remain stuck if a numpad key was mapped to the Shift key.

### Monaco Preview

 - Added support for .ahk files to be shown as a plaintext file in Peek and File Explorer add-ons. Thanks [@daverayment](https://github.com/daverayment)!
 - Added support for .ion files to be shown as a plaintext file in Peek and File Explorer add-ons. Thanks [@octastylos-pseudodipteros](https://github.com/octastylos-pseudodipteros)!
 - Added support for syntax highlighting for .srt files in Peek and File Explorer add-ons. Thanks [@PesBandi](https://github.com/PesBandi)!

### Mouse Jump

 - Allow customizing the appearance of the UI of the Mouse Jump pop-up. Thanks [@mikeclayton](https://github.com/mikeclayton)!

### New+

 - Added support for Windows 10. Thanks [@cgaarden](https://github.com/cgaarden)!
 - Fixed an issue causing the renaming of new files to not trigger some times. Thanks [@cgaarden](https://github.com/cgaarden)!
 - Updated the New+ icons. Thanks [@niels9001](https://github.com/niels9001)!

### Peek

 - Peek now checks local capabilities to decide what image formats Image Previewer is able to support. Thanks [@daverayment](https://github.com/daverayment)!
 - Fixed an issue causing the Code Files Previewer to not load correctly under certain conditions. Thanks [@daverayment](https://github.com/daverayment)!
 - Refactored, improved and fixed logging when loading the user settings file. Thanks [@daverayment](https://github.com/daverayment)!

### PowerToys Run

 - Added a scoring function for proper ordering of the WindowWalker plugin results. Thanks [@andbartol](https://github.com/andbartol)!
 - Added UUIDv7 support to the ValueGenerator plugin. Thanks [@frederik-hoeft](https://github.com/frederik-hoeft)!
 - The calculator plugin now allows scientific notation numbers with a lowercase 'e'. Thanks [@PesBandi](https://github.com/PesBandi)!
 - Ported the UI from WPF-UI to .NET 9 WPF, to fix "Desktop composition is disabled" crashes.

### Quick Accent

 - Added a setting to allow selecting which character sets to show. Thanks [@Sirozha1337](https://github.com/Sirozha1337)!

### Screen Ruler

 - Added a Setting to also allow showing measurements in inches, centimeters or millimeters. Thanks [@Sophanatprime](https://github.com/Sophanatprime)!

### Settings

 - Fixed an issue causing all the links to milestones in the "What's new?" OOBE page to point to the same milestone.
 - Removed extra space from the Welcome page. Thanks [@agarwalishita](https://github.com/agarwalishita)!
 - Updated left navigation bar icons. Thanks [@niels9001](https://github.com/niels9001)!
 - Fixed accessibility issues in the dashboard page. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!

### Workspaces

 - Added support for Progressive Web Applications to Workspaces.
 - Implemented a feature to move existing windows instead of creating new ones.
 - Fixed a crash when opening the workspaces editor that was caused by passing incorrect encoder parameters when saving Bitmap files.
 - Workspaces editor position is now saved so that we can start it at the same position when we open it again.
 - Fixed an issue causing many instances of the same application to be put in the same position instead of the intended position due to timer issues.
 - Fixed detection of exact application version when many versions of the same application are installed.

### Documentation

 - Improved language in CONTRIBUTE.md. Thanks [@sanskaarz](https://github.com/sanskaarz)!
 - Added Bilibili plugin mention to thirdPartyRunPlugins.md. Thanks [@Whuihuan](https://github.com/Whuihuan)!
 - Added CanIUse and TailwindCSS plugins mention to thirdPartyRunPlugins.md. Thanks [@skttl](https://github.com/skttl)!
 - Added HttpStatusCodes plugin mention to thirdPartyRunPlugins.md. Thanks [@grzhan](https://github.com/grzhan)!
 - Updated COMMUNITY.md with more contributors.

### Development

 - Upgraded to .NET 9. Thanks [@snickler](https://github.com/snickler)!
 - Fixed building on Visual Studio 17.12.
 - Upgraded the System.IO.Abstractions dependency to 21.0.29. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Upgraded the WindowsAppSDK dependency to 1.6.241114003. Thanks [@shuaiyuanxx](https://github.com/shuaiyuanxx)!
 - Upgraded the MSTest dependency to 3.6.3. Thanks [@Youssef1313](https://github.com/Youssef1313)!
 - Upgraded the check-spelling CI dependency to 0.0.24 and fixed related spell checking issues. Thanks [@jsoref](https://github.com/jsoref)!
 - Removed duplicate names from the spellcheck allowed names file. Thanks [@htcfreek](https://github.com/htcfreek)!
 - Improved logging of asynchronous methods call stacks when logging an error.
 - Created a MSBuild props file to be imported by other projects to enable AOT support.
 - Made the Peek utility source code AOT compatible.
 - Updated .editorconfig rules to relax squiggly IDE errors in Visual Studio 17.12. Thanks [@snickler](https://github.com/snickler)!
 - Moved Xaml.Styler from the root to the src folder.

#### What is being planned for version 0.88

For [v0.88][github-next-release-work], we'll work on the items below:

 - Stability / bug fixes
 - New module: File Actions Menu
 - Integrate Sysinternals ZoomIt

## PowerToys Community

The PowerToys team is extremely grateful to have the [support of an amazing active community][community-link]. The work you do is incredibly important. PowerToys wouldn’t be nearly what it is today without your help filing bugs, updating documentation, guiding the design, or writing features. We want to say thank you and take time to recognize your work.  Month by month, you directly help make PowerToys a better piece of software.

## Code of Conduct

This project has adopted the [Microsoft Open Source Code of Conduct][oss-conduct-code].

## Privacy Statement

The application logs basic diagnostic data (telemetry). For more information on privacy and what we collect, see our [PowerToys Data and Privacy documentation](https://aka.ms/powertoys-data-and-privacy-documentation).

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

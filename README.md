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
| [Advanced Paste](https://aka.ms/PowerToysOverview_AdvancedPaste) | [Always on Top](https://aka.ms/PowerToysOverview_AoT) | [PowerToys Awake](https://aka.ms/PowerToysOverview_Awake) |
| [Command Not Found](https://aka.ms/PowerToysOverview_CmdNotFound) | [Color Picker](https://aka.ms/PowerToysOverview_ColorPicker) | [Crop And Lock](https://aka.ms/PowerToysOverview_CropAndLock) |
| [Environment Variables](https://aka.ms/PowerToysOverview_EnvironmentVariables) | [FancyZones](https://aka.ms/PowerToysOverview_FancyZones) | [File Explorer Add-ons](https://aka.ms/PowerToysOverview_FileExplorerAddOns) |
| [File Locksmith](https://aka.ms/PowerToysOverview_FileLocksmith) | [Hosts File Editor](https://aka.ms/PowerToysOverview_HostsFileEditor) | [Image Resizer](https://aka.ms/PowerToysOverview_ImageResizer) |
| [Keyboard Manager](https://aka.ms/PowerToysOverview_KeyboardManager) | [Mouse utilities](https://aka.ms/PowerToysOverview_MouseUtilities) | [Mouse Without Borders](https://aka.ms/PowerToysOverview_MouseWithoutBorders) |
| [Peek](https://aka.ms/PowerToysOverview_Peek) | [Paste as Plain Text](https://aka.ms/PowerToysOverview_PastePlain) | [PowerRename](https://aka.ms/PowerToysOverview_PowerRename) |
| [PowerToys Run](https://aka.ms/PowerToysOverview_PowerToysRun) | [Quick Accent](https://aka.ms/PowerToysOverview_QuickAccent) | [Registry Preview](https://aka.ms/PowerToysOverview_RegistryPreview) |
| [Screen Ruler](https://aka.ms/PowerToysOverview_ScreenRuler) | [Shortcut Guide](https://aka.ms/PowerToysOverview_ShortcutGuide) | [Text Extractor](https://aka.ms/PowerToysOverview_TextExtractor) |
| [Video Conference Mute](https://aka.ms/PowerToysOverview_VideoConference) |

## Installing and running Microsoft PowerToys

### Requirements

- Windows 11 or Windows 10 version 2004 (code name 20H1 / build number 19041) or newer.
- x64 or ARM64 processor
- Our installer will install the following items:
   - [Microsoft Edge WebView2 Runtime](https://go.microsoft.com/fwlink/p/?LinkId=2124703) bootstrapper. This will install the latest version.

### Via GitHub with EXE [Recommended]

Go to the [Microsoft PowerToys GitHub releases page][github-release-link] and click on `Assets` at the bottom to show the files available in the release. Please use the appropriate PowerToys installer that matches your machine's architecture and install scope. For most, it is `x64` and per-user.

<!-- items that need to be updated release to release -->
[github-next-release-work]: https://github.com/microsoft/PowerToys/issues?q=is%3Aissue+milestone%3A%22PowerToys+0.82%22
[github-current-release-work]: https://github.com/microsoft/PowerToys/issues?q=project%3Amicrosoft%2FPowerToys%2F54
[ptUserX64]: https://github.com/microsoft/PowerToys/releases/download/v0.81.0/PowerToysUserSetup-0.81.0-x64.exe
[ptUserArm64]: https://github.com/microsoft/PowerToys/releases/download/v0.81.0/PowerToysUserSetup-0.81.0-arm64.exe
[ptMachineX64]: https://github.com/microsoft/PowerToys/releases/download/v0.81.0/PowerToysSetup-0.81.0-x64.exe
[ptMachineArm64]: https://github.com/microsoft/PowerToys/releases/download/v0.81.0/PowerToysSetup-0.81.0-arm64.exe
 
|  Description   | Filename | sha256 hash |
|----------------|----------|-------------|
| Per user - x64       | [PowerToysUserSetup-0.81.0-x64.exe][ptUserX64] | E62B1EE81954A75355C04E7567B1C9AAD6034AA0C61AD22587F8746D0DC488C8 |
| Per user - ARM64     | [PowerToysUserSetup-0.81.0-arm64.exe][ptUserArm64] | 75330A2DB4F9EF9B548B3B58F8BF3262C8C67E680042639BBBBC87EA244F24E2 |
| Machine wide - x64   | [PowerToysSetup-0.81.0-x64.exe][ptMachineX64] | 29F151B01FE3C94D4FD75F2D6E8F09A6C0F0962385B83A5A733F6717312F639D |
| Machine wide - ARM64 | [PowerToysSetup-0.81.0-arm64.exe][ptMachineArm64] | FCE636220E1FB854771258D9558E07B7532728AD4C722A7920338DEE60DEECF7 |

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

### 0.81 - Build 2024 Update

In this release, we focused on new features, stability and improvements.

**Highlights**

 - New utility: Advanced Paste - This is an evolution based on feedback of the Paste As Plain Text utility to do more. It can paste as plain text, markdown, or json directly with the new UX or with a direct keystroke invoke.  These are fully locally executed. In addition, it now has an AI powered option as well if you wish with the free form text box.  The AI feature is 100% opt-in and requires an Open AI key. This new system will allow us to have more freedom in the future to quickly add in new features like pasting an image directly to a file or handle additional meta data types past just text. 
    - Thanks [@craigloewen-msft](https://github.com/craigloewen-msft) for the core functionality and [@niels9001](https://github.com/niels9001) for the UI/UX design!
 - Command Not Found now uses the PowerShell Gallery release and now supports ARM64. Thanks [@carlos-zamora](https://github.com/carlos-zamora)!
 - Fixed most accessibility issues opened after the latest accessibility review.
 - Refactored, packaged and released the main Environment Variables Editor, Hosts File Editor and Registry Preview utilities functionality as controls to be integrated into DevHome. Thanks [@dabhattimsft](https://github.com/dabhattimsft) for validating and integrating into DevHome!

### General

 - Fixed crashes on older CPUS by updating .NET to 8.0.4. (This was a hotfix for 0.80)

### Advanced Paste

 - New utility: Advanced Paste - This is an evolution based on feedback of the Paste As Plain Text utility to do more. It can paste as plain text, markdown, or json directly with the new UX or with a direct keystroke invoke.  These are fully locally executed. In addition, it now has an AI powered option as well if you wish with the free form text box.  The AI feature is 100% opt-in and requires an Open AI key. This new system will allow us to have more freedom in the future to quickly add in new features like pasting an image directly to a file or handle additional meta data types past just text. 
    - Thanks [@craigloewen-msft](https://github.com/craigloewen-msft) for the core functionality and [@niels9001](https://github.com/niels9001) for the UI/UX design!

### AlwaysOnTop

 - Enable border anti-aliasing. Thanks [@ewancg](https://github.com/ewancg)! 

### Color Picker

 - Improved accessibility by making the Settings and Copy to clipboard buttons focusable.
 - Improved accessibility by supporting picking a color using the keyboard.

### Command Not Found

 - Upgraded the Command Not Found to use the new PowerShell Gallery release and support ARM64. Thanks [@carlos-zamora](https://github.com/carlos-zamora)!

### Environment Variables Editor

 - Refactored, packaged and released the main Environment Variables Editor functionality as a control to be integrated into DevHome. Thanks [@dabhattimsft](https://github.com/dabhattimsft) for validating and integrating into DevHome!

### FancyZones

 - Fixed window wrap around behavior when overriding Windows key and arrow shortcuts on single monitor scenarios. Thanks [@DanRosenberry](https://github.com/DanRosenberry)!
 - Improved accessibility of the editor by listing the keyboard shortcuts in the Canvas Editor.

### File Explorer add-ons

 - Updated Monaco to 0.47 and added the new sticky scroll setting for DevFiles viewer. Thanks [@Aaron-Junker](https://github.com/Aaron-Junker)!
 - Added the new font size setting for DevFiles viewer. Thanks [@Aaron-Junker](https://github.com/Aaron-Junker)!
 - Added support for .srt (subtitle) file previewing in DevFiles viewer. Thanks [@PesBandi](https://github.com/PesBandi)!

### Hosts File Editor

 - Refactored, packaged and released the main Hosts File Editor functionality as a control to be integrated into DevHome. Thanks [@dabhattimsft](https://github.com/dabhattimsft) for validating and integrating into DevHome!

### Image Resizer

 - Supported narrator announcing the checkboxes in the UI and the sizes combobox. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Improved accessibility by increasing contrast in the text color of combobox items.

### Installer

 - Fixed some install failures when the folders the DSC module is to be installed in isn't accessible by the WiX installer. (This was a hotfix for 0.80)
 - Detecting install location for DSC now uses registry instead of WMI to improve performance. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Fixed an error causing the machine scope installer to not install correctly in machines where the documents folder is in a UNC network path. We're still working in a fix for the user scope installer.

### Keyboard Manager

 - Fixed startup crashes in the editor when the Visual C++ Redistributable wasn't installed. (This was a hotfix for 0.80)
 - Fixed an accessibility issue where the first button wasn't focused after adding a new row in the editor.
 - Environment Variables are now expanded in arguments of programs started through a shortcut. Thanks [@HydroH](https://github.com/HydroH)!

### Paste as Plain Text

 - Paste as Plain Text was removed as a separate utility, since its functionality is now part of the Advanced Paste utility.

### Peek

 - Updated icons, tweaked UI and refactored internal code. Thanks [@Jay-o-Way](https://github.com/Jay-o-Way)!
 - Updated Monaco to 0.47 and added the new sticky scroll setting for DevFiles viewer. Thanks [@Aaron-Junker](https://github.com/Aaron-Junker)!
 - Added the new font size setting for DevFiles viewer. Thanks [@Aaron-Junker](https://github.com/Aaron-Junker)!
 - Upgrade the SharpCompress dependency to 0.37.2 and fixed archive parsing. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Fixed aliasing in the image viewer.
 - Added support for .srt (subtitle) file previewing in DevFiles viewer. Thanks [@PesBandi](https://github.com/PesBandi)!

### Power Rename

 - Fixed the descriptions that were mixed up in the regex helper (\S and \w).

### PowerToys Run

 - Added support for UNC paths starting with // in the Folder plugin. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Fixed the plugin load failed message to list the failed plugins. Thanks [@belkiss](https://github.com/belkiss)!
 - Icons for MSIX packages are now updated when a package update is detected. Thanks [@HydroH](https://github.com/HydroH)!
 - Use Mica backdrop instead of Acrylic to fix random crashes caused by the Windows composition being momentarily turned off.
 - Improved accessibility in the results list action buttons by improving contrast of hovered/focused buttons.

### Quick Accent

 - Added support for the Esperanto character set. Thanks [@salutontalk](https://github.com/salutontalk) and [@ccmywish](https://github.com/ccmywish)!
 - Added the ǽ and ϑ characters. Thanks [@PesBandi](https://github.com/PesBandi)!

### Registry Preview

 - Refactored, packaged and released the main Registry Preview functionality as a control to be integrated into DevHome. Thanks [@dabhattimsft](https://github.com/dabhattimsft) for validating and integrating into DevHome!

### Text Extractor

 - Fixed an issue causing the Settings page to not be opened when clicking the Settings button in Text Extractor's overlay. (This was a hotfix for 0.80)

### Settings

 - Improved UI ordering of the File Explorer add-ons. Thanks [@niels9001](https://github.com/niels9001)!
 - Applied fixes to theme overriding and cleaned up unneeded code. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Fixed misspells in references to the Hosts File Editor utility. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Improved accessibility of the Select Folder button in the Settings Backup UI.
 - Improved accessibility by improving focus and tab navigation in the ColorPicker page. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Added a description to the fallback encoder setting in the Image Resizer page. Thanks [@Kissaki](https://github.com/Kissaki)!
 - Refactored and improved performance in the PowerToys Run plugins UI in the Settings page. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
 - Fixed a crash when a user cleared the contents of a Number Box in the PowerToys Run plugins additional options. Thanks [@htcfreek](https://github.com/htcfreek)!
 - Update the PATH environment variables with the user scope PATH when entering the Command Not Found page to improve PowerShell detection.

### Documentation

 - Added the WebSearchShortcut plugin to PowerToys Run thirdPartyRunPlugins.md docs. Thanks [@Daydreamer-riri](https://github.com/Daydreamer-riri)!
 - Updated COMMUNITY.md with the project managers that are part of the core team.
 - Improved the DSC samples.
 - Added the 1Password plugin to PowerToys Run thirdPartyRunPlugins.md docs. Thanks [@KairuDeibisu](https://github.com/KairuDeibisu)!
 - Added the UnicodeInput plugin to PowerToys Run thirdPartyRunPlugins.md docs. Thanks [@nathancartlidge](https://github.com/nathancartlidge)!

### Development

 - Updated System.Drawing.Common to 8.0.5 to fix CI builds after the .NET 8.0.5 upgrade was released.
 - Fixed file permissions when doing a build using cache on PR CI. Thanks [@dfederm](https://github.com/dfederm)!
 - Removed the Test SDK reference on ARM64 to fix local building for ARM64. Thanks [@dfederm](https://github.com/dfederm)!
 - Replaced make_pair with RemapBufferRow in Keyboard Manager internal code. Thanks [@masaru-iritani](https://github.com/masaru-iritani)!
 - Added CODEOWNERS file to protect sensitive parts of the repo. Thanks [@htcfreek](https://github.com/htcfreek) for the help in figuring out how to make the spellcheck folder an exception!
 - Added comments in code. to make it clear what the error badge in PowerToys Run plugin list in Settings means. Thanks [@Jay-o-Way](https://github.com/Jay-o-Way)!
 - Enabled caching by default in the PR CI pipelines. Thanks [@dfederm](https://github.com/dfederm)!
 - Disabled caching for PR started from forks, since those were failing. Thanks [@dfederm](https://github.com/dfederm)!
 - Removed baseline files for policy checking and turned on the "TSA" process in the release pipelines instead.
 - Added caching of nuget packages in the PR CI pipelines. Thanks [@dfederm](https://github.com/dfederm)!
 - Updated the release CI pipelines TouchdownBuildTask to v3.
 - Moved the release CI pipelines to ESRPv5.
 - Added a policy for GitHub Copilot Workspaces for the repo on GitHub. Thanks [@Aaron-Junker](https://github.com/Aaron-Junker)!

#### What is being planned for version 0.82

For [v0.82][github-next-release-work], we'll work on the items below:

 - Stability / bug fixes
 - Language selection
 - New module: File Actions Menu

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

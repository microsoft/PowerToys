# Microsoft PowerToys

![Hero image for Microsoft PowerToys](doc/images/overview/PT_hero_image.png)

[How to use PowerToys][usingPowerToys-docs-link] | [Downloads & Release notes][github-release-link] | [Contributing to PowerToys](#contributing) | [What's Happening](#whats-happening) | [Roadmap](#powertoys-roadmap)

## Build status

| Architecture | Solution (Main) | Solution (Stable) | Installer (Main) |
|--------------|-----------------|-------------------|------------------|
| x64 | [![Build Status for Main](https://dev.azure.com/ms/PowerToys/_apis/build/status/microsoft.PowerToys?branchName=main&jobName=Build%20x64%20Release)](https://dev.azure.com/ms/PowerToys/_build/latest?definitionId=219&branchName=main) | [![Build Status for Stable](https://dev.azure.com/ms/PowerToys/_apis/build/status/microsoft.PowerToys?branchName=stable&jobName=Build%20x64%20Release)](https://dev.azure.com/ms/PowerToys/_build/latest?definitionId=219&branchName=stable) | [![Build Status Installer pipeline](https://dev.azure.com/microsoft/Dart/_apis/build/status/PowerToys/PowerToys%20Signed%20YAML%20Release%20Build?branchName=main&jobName=Build&configuration=Build%20Release_x64)](https://dev.azure.com/microsoft/Dart/_build/latest?definitionId=76541&branchName=main) |
| ARM64 | [![Build Status for Main](https://dev.azure.com/ms/PowerToys/_apis/build/status/microsoft.PowerToys?branchName=main&jobName=Build%20arm64%20Release)](https://dev.azure.com/ms/PowerToys/_build/latest?definitionId=219&branchName=main) | not on stable yet | [![Build Status Installer pipeline](https://dev.azure.com/microsoft/Dart/_apis/build/status/PowerToys/PowerToys%20Signed%20YAML%20Release%20Build?branchName=main&jobName=Build&configuration=Build%20Release_arm64)](https://dev.azure.com/microsoft/Dart/_build/latest?definitionId=76541&branchName=main) |

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

- Windows 11 or Windows 10 v1903 (18362) or newer.
- Our installer will install the following items:
   - [.NET 6.0.4 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/thank-you/runtime-desktop-6.0.4-windows-x64-installer) or a newer 6.0.x runtime. 
   - [Microsoft Edge WebView2 Runtime](https://go.microsoft.com/fwlink/p/?LinkId=2124703) bootstrapper. This will install the latest version. 
   - [Windows App SDK Runtime 1.0.3](https://docs.microsoft.com/en-us/windows/apps/windows-app-sdk/downloads). This will install version 1.0.3 if this or newer version is not installed already.

### Via GitHub with EXE [Recommended]

 [Microsoft PowerToys GitHub releases page][github-release-link], click on `Assets` at the bottom to show the files available in the release and then click on `PowerToysSetup-0.58.0-x64.exe` to download the PowerToys installer.

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

### 0.58 - April 2022 Update

In this release, we focused on upgrading to new dependencies and building for native ARM64.  Below are some of the highlights!

**Highlights**

- Most of the work for running natively on ARM64 has been included in the repo. Thanks [@snickler](https://github.com/snickler)!
- Uses of the obsolete WebBrowser control were replaced with WebView 2.
- All uses of .NET Core 3.1 were removed. PowerToys now runs on .NET 6.
- Settings no longer runs on XAML Islands and is now running on WinUI 3, fixing many bugs related to XAML islands.

### General

- Spell checking fixes in the code. Thanks [@jsoref](https://github.com/jsoref)!
- Fix for a CI error related to spell checking due to a GitHub API change. Thanks [@jsoref](https://github.com/jsoref)!
- Fixed the documentation references to GitHub. Thanks [@Cyl18](https://github.com/Cyl18)!

### ARM64

- Prepare solution and property files for ARM64 port. Thanks [@snickler](https://github.com/snickler)!
- Port unhandled exception handler to ARM64. Thanks [@snickler](https://github.com/snickler)!
- Port of the Settings projects to ARM64. Thanks [@snickler](https://github.com/snickler)!
- Port of most of the PowerToys to ARM64. Thanks [@snickler](https://github.com/snickler)!
- Port of the debug utilities to ARM64.

### Always on Top

- Fix for topmost state of the window resetting for some applications. (This was a hotfix for 0.57)

### ColorPicker

- The CIEXYZ format is now properly show in upper case.

### FancyZones

- Restore rounded corners on Windows 11 and add a setting to control this behavior. (This was a hotfix for 0.57)
- Fixed an edge case where the Windows Terminal window wouldn't be snapped when opened. (This was a hotfix for 0.57)
- Improved narrator support in the Grid Editor. (This was a hotfix for 0.57)
- Fixed a bug when restoring rounded corners on Windows 11. (This was a hotfix for 0.57)
- Fix for windows not being resized correctly on different dpi settings. (This was a hotfix for 0.57)
- Removed resolution from the screen identifier so zones aren't reset when resolution changes.
- Scale the canvas layout when editing according to new scaling/resolution.
- Shipping a new tool to help debug windows interactions with FancyZones.

### File explorer

- Fix for a crash in dev file preview if the settings file hadn't been created yet. (This was a hotfix for 0.57)
- New file types were added to dev file preview (".reg", ".xslt", ".xsd", ".wsdl", ".ino", ".pde", ".razor"). Thanks [@Aaron-Junker](https://github.com/Aaron-Junker)!
- Fix an existing "file still in use" issue in dev file preview. Thanks [@Aaron-Junker](https://github.com/Aaron-Junker)!
- Dev file preview is now able to interpret file extensions in a case-insensitive way. Thanks [@Aaron-Junker](https://github.com/Aaron-Junker)!
- SVG and markdown viewers no longer use WebBrowser and use WebView2 instead.
- Markdown preview now respects the dark mode settings on Windows. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!

### Mouse utility

- Fix for the bug causing shortcuts set on icons to not activate when a mouse utility was active on specific monitor configurations.

### PowerToys Run

- Fix for PowerToys Run using high CPU and memory when updating its settings. (This was a hotfix for 0.57)
- Add the "Run as different user" feature to the Program, Shell, and Search plugins. Thanks [@htcfreek](https://github.com/htcfreek)! (This was a hotfix for 0.57)
- Fix for a WindowWalker crash when a Virtual Desktop registry key is not set. Thanks [@htcfreek](https://github.com/htcfreek)! (This was a hotfix for 0.57)
- Fix for VS Code Workspaces not using the user's path variable right after an install or update. Thanks [@ricardosantos9521](https://github.com/ricardosantos9521)! (This was a hotfix for 0.57)
- Fix for the System plugin causing PowerToys Run to be slow when many network interfaces exist. Thanks [@htcfreek](https://github.com/htcfreek)! (This was a hotfix for 0.57)
- Fix for the Program plugin not showing special shortcuts with empty targets, like Control Panel. (This was a hotfix for 0.57)
- Additional logging for the Terminal plugin. Thanks [@davidegiacometti](https://github.com/davidegiacometti)! (This was a hotfix for 0.57)
- Web Search and URI plugins have better code for detecting the default browser now.
- Fix for the Services plugin not manipulating service names with spaces correctly. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
- Fix for the Terminal plugin not recognizing profiles correctly. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
- Fix for latest VSCode insiders build not showing up in the VSCode Workspaces plugin. Thanks [@JacobDeuchert](https://github.com/JacobDeuchert)!
- Increased floating number precision in the Unit Converter plugin.
- VSCode Workspaces now finds portable installations of VS Code. Thanks [@harvastum](https://github.com/harvastum)
- Fixed an issue starting PowerToys Run when the desktop is not initialized. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!

### Settings

- Settings now runs on WinUI3 instead of XAML islands.
- Settings no longer runs as an administrator when runner is started as an administrator.

### Runner

- Use sensible default times for rechecking for an update, to avoid writing to the logs in a loop. (This was a hotfix for 0.57)
- Runner cleans up the update directory if the installation is up to date. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!

### Installer

- Distribute a signed .msi inside the .exe installer bootstrapper. (This was a hotfix for 0.57)
- Removed the .NET core dependency from the installer.
- Partial support for an ARM64 installer.
- Updated the .NET to 6.0.4.
- Force update all files on reinstall/update, to try and fix installation issues.


### Development

- PowerToys no longer takes a dependency on .NET core.
- WinUI3 is a new dependency. Settings now targets win10-x64 and win10-arm64 due to this.

#### Community contributions

We'd like to directly mention certain contributors (in alphabetical order) for their continued community support this month and helping directly make PowerToys a better piece of software.  
[@Aaron-Junker](https://github.com/Aaron-Junker), [@CleanCodeDeveloper](https://github.com/CleanCodeDeveloper), [@davidegiacometti](https://github.com/davidegiacometti), [@dend](https://github.com/dend), [@franky920920](https://github.com/franky920920), [@htcfreek](https://github.com/htcfreek), [@jay-o-way](https://github.com/jay-o-way), [@jsoref](https://github.com/jsoref), [@niels9001](https://github.com/niels9001), [@snickler](https://github.com/snickler).


#### What is being planned for v0.59

For [v0.59][github-next-release-work], we'll work on below:

- Environment Variables Editor PowerToy
- Wrap up the ARM64 build
- Stability / bug fixes

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
[github-next-release-work]: https://github.com/microsoft/PowerToys/issues?q=is%3Aopen+is%3Aissue+project%3Amicrosoft%2FPowerToys%2F32
[github-current-release-work]: https://github.com/microsoft/PowerToys/issues?q=is%3Aopen+is%3Aissue+project%3Amicrosoft%2FPowerToys%2F31

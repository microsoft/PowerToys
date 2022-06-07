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

- Windows 11 or Windows 10 v1903 (18362) or newer.
- Our installer will install the following items:
   - [.NET 6.0.5 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/6.0#runtime-desktop-6.0.5) or a newer 6.0.x runtime. 
   - [Microsoft Edge WebView2 Runtime](https://go.microsoft.com/fwlink/p/?LinkId=2124703) bootstrapper. This will install the latest version. 
   - [Microsoft Visual C++ Redistributable](https://docs.microsoft.com/cpp/windows/latest-supported-vc-redist?view=msvc-170#visual-studio-2015-2017-2019-and-2022) installer. This will install one of the latest versions available.

### Via GitHub with EXE [Recommended]

 [Microsoft PowerToys GitHub releases page][github-release-link], click on `Assets` at the bottom to show the files available in the release. Please use the appropriate the PowerToys installer that matches your machine's architecture.  For most people, it is `x64`.
 
 - **For x64 (most common):** click on `PowerToysSetup-0.59.0-x64.exe`
 - **For ARM64:** `PowerToysSetup-0.59.0-arm64.exe` 

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

### 0.59 - May 2022 Update

In this release, we focused on wrapping up building for native ARM64 and releasing the first experimental build. Below are some of the highlights!

**Highlights**

- The work for running natively on ARM64 has been wrapped up and a build is released. Thanks [@snickler](https://github.com/snickler)!
- Power Rename now is running on WinUI 3.
- Keyboard Manager now allows up to 4 modifier keys for shortcuts and has received some quality fixes.
- Upgraded the Windows App SDK runtimes to 1.1.0, fixing an issue where Settings wouldn't start with UAC off and improving performance.
- The Windows App SDK runtime binaries are being shipped with PowerToys which should resolve the installations issues reported with WinAppSDK.

### Known issues
- There are reports of users who are [unable to open the Settings window](https://github.com/microsoft/PowerToys/issues/18015). This is being caused by incompatibilities with some applications (RTSS RivaTuner Statistics Server and MSI AfterBurner are known examples of this). If you're affected by this, please check the  linked issue to verify if any of the presented solutions works for you.  

### General

- Some typos were fixed. Thanks [@eltociear](https://github.com/eltociear), [@rcmaehl](https://github.com/rcmaehl) and [@ShyPixie](https://github.com/ShyPixie)!

### ARM64

- ARM64 support now fully added!
- Fixed PowerRename to work on ARM64.
- Fixed File Explorer tools to work on ARM64.
- Made changes for the installer projects to build ARM64 installers.
- Configured the CI and Release pipelines to build for ARM64.
- Added ARM64 build status to the README.

### Always on Top

- Fixed an issue where the borders where sticking around when a window was minimized with Win+D.

### FancyZones

- Fixed a bug that was consuming CPU cycles when the default layout was set.
- Fixed a bug where apps were not opened in their last known zones due to Virtual Desktop ID changes.
- Fixed a bug that was snapping popup menus opened by applications.
- Fixed a bug causing windows not to be snapped under some configurations.

### Image Resizer

- No longer tries to change metadata on files that were not actually resized. Thanks [@adamchilders](https://github.com/adamchilders)!

### File explorer add-ons

- Fixed a bug where modules depending on WebView2 would be limited to opening files smaller than 2 MB. Now the resulting html is generated into a temporary file before presenting it.
- Add a viewBox attribute to svg files that don't have one so that the preview tries to show the whole image.
- Remove scrollbar that was showing when rendering svg thumbnails.

### Keyboard Manager

- Now up to four modifier keys can be used in shortcuts. This will allow you to use the Office key (which sends Win+Ctrl+Shift+Alt), for example.
- Fixed a bug locking Keyboard Manager when two shortcut mapping were pressed at the same time.
- Removed event spam for certain telemetry events.

### PowerRename

- Ported to use WinUI 3 instead of WinUI 2.

### PowerToys Run

- The Services plugin is able to search for parts of the name, display name or the service type or state. Thanks [@htcfreek](https://github.com/htcfreek)!
- The Services plugin now supports the startup type 'Automatic (Delayed Autostart)'. Thanks [@htcfreek](https://github.com/htcfreek)!
- The Services plugin now has tooltips for large service names and other UI improvements. Thanks [@htcfreek](https://github.com/htcfreek)!
- The TimeDate plugin gave results for queries containing just numbers on global queries. This has been fixed. Thanks [@htcfreek](https://github.com/htcfreek)!
- We've introduced a throttle before a query is done to ensure typing is done to increase performance. Thanks [@shandsj](https://github.com/shandsj)!
- Fixed a crash in WebSearch when there's an empty pattern setup for the system's default browser.
- Fixed a bug where VSCodeWorkspaces was not finding portable installations of VSCode. Thanks [@harvastum](https://github.com/harvastum)!
- The Calculator plugin reacts better to invalid input and internal errors. Thanks [@htcfreek](https://github.com/htcfreek)!
- The Calculator plugin can now be configured to use the US number format instead of the system one. Thanks [@htcfreek](https://github.com/htcfreek)!
- The Folder plugin supports paths containing "/". Thanks [@davidegiacometti](https://github.com/davidegiacometti)!

### Settings

- Now the UI for adding excluded apps for FindMyMouse is disabled when the module is disabled. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
- Text was improved in the Settings UI for File Explorer. Thanks [@Jay-o-Way](https://github.com/Jay-o-Way)!
- Settings won't try to launch if everything runs elevated in the machine, and a warning message is shown instead.
- Some minor UI fixes. Thanks [@niels9001](https://github.com/niels9001)!
- The Settings screen should now open correctly if the OOBE screen was opened first.
- The rounded corner settings for FancyZones now only show on Windows 11. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
- Fixed a UI freeze when entering the Keyboard Manager page with clean settings.
- Fixed a UI glitch where a message was being shown that all PowerToys Run plugins were disabled when using the search function. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
- Upgraded the Windows App SDK runtimes to 1.1.0, fixing an issue where Settings wouldn't start with UAC off and improving performance.

### Runner 

- Auto-update takes into account if it's running on x64 or arm64 to download the right installer.

### Installer

- Updated the .NET dependency to 6.0.5.
- The installer is now built using a beta version of Wix 3.14 for arm64 support.
- Added the VC++ Redistributable binary as a requirement.
- The Windows App SDK runtime binaries are being shipped with PowerToys instead of running its installer. This should fix most of the install issues with 0.58.

### Development

- New action added to GitHub to publish the winget package to PowerToys.
- New action added to GitHub to publish for the Microsoft Store. Thanks [azchohfi](https://github.com/azchohfi)!
- Documentation for installing the Windows App SDK dependencies and building the installer was updated.
- FxCop removed from the PowerToys Run TimeZone plugin and was replaced with NetAnalyzers. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!

#### Community contributions

We'd like to directly mention certain contributors (in alphabetical order) for their continued community support this month and helping directly make PowerToys a better piece of software.  
[@Aaron-Junker](https://github.com/Aaron-Junker), [@CleanCodeDeveloper](https://github.com/CleanCodeDeveloper), [@davidegiacometti](https://github.com/davidegiacometti), [@dend](https://github.com/dend), [@franky920920](https://github.com/franky920920), [@htcfreek](https://github.com/htcfreek), [@jay-o-way](https://github.com/jay-o-way), [@jsoref](https://github.com/jsoref), [@niels9001](https://github.com/niels9001), [@snickler](https://github.com/snickler).

#### What is being planned for v0.60

For [v0.60][github-next-release-work], we'll work on below:

- Environment Variables Editor PowerToy
- Screen Measure PowerToy
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
[github-next-release-work]: https://github.com/microsoft/PowerToys/issues?q=is%3Aopen+is%3Aissue+project%3Amicrosoft%2FPowerToys%2F33
[github-current-release-work]: https://github.com/microsoft/PowerToys/issues?q=is%3Aopen+is%3Aissue+project%3Amicrosoft%2FPowerToys%2F32

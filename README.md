# Microsoft PowerToys

![Hero image for Microsoft PowerToys](doc/images/overview/PT_hero_image.png)

[How to use PowerToys][usingPowerToys-docs-link] | [Downloads & Release notes][github-release-link] | [Contributing to PowerToys](#contributing) | [What's Happening](#whats-happening) | [Roadmap](#powertoys-roadmap)

## Build status

| Architecture | Solution (Main) | Solution (Stable) | Installer (Main) |
|--------------|-----------------|-------------------|------------------|
| x64 | [![Build Status for Main](https://dev.azure.com/ms/PowerToys/_apis/build/status/microsoft.PowerToys?branchName=main)](https://dev.azure.com/ms/PowerToys/_build/latest?definitionId=219&branchName=main) | [![Build Status for Stable](https://dev.azure.com/ms/PowerToys/_apis/build/status/microsoft.PowerToys?branchName=stable)](https://dev.azure.com/ms/PowerToys/_build/latest?definitionId=219&branchName=stable) | [![Build Status Installer pipeline](https://dev.azure.com/microsoft/Dart/_apis/build/status/microsoft.PowerToys?branchName=main)](https://dev.azure.com/microsoft/Dart/_build/latest?definitionId=76541&branchName=main) |
| ARM64 | Currently investigating | [Issue #490](https://github.com/microsoft/PowerToys/issues/490) |  |

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
   - [.NET Core 3.1.22 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/thank-you/runtime-desktop-3.1.22-windows-x64-installer) or a newer 3.1.x runtime. This is needed currently for the Settings application.
   - [.NET 6.02 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/thank-you/runtime-desktop-6.0.2-windows-x64-installer) or a newer 6.0.x runtime. 
   - [Microsoft Edge WebView2 Runtime](https://go.microsoft.com/fwlink/p/?LinkId=2124703) bootstrapper. This will install the latest version. 

### Via GitHub with EXE [Recommended]

 [Microsoft PowerToys GitHub releases page][github-release-link], click on `Assets` at the bottom to show the files available in the release and then click on `PowerToysSetup-0.56.2-x64.exe` to download the PowerToys installer.

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

### 0.56 - February 2022 Update

In this release, we focused heavily on stability and improvements.  Below are some of the highlights!

**Highlights**

- Upgrade notes!  A big feedback items for new versions was upgrade notes.  We have the release notes on GitHub as well.
- FancyZones zone limit has been increased to 128 zones.  Before the limit was 40.
- Timezone conversion plugin for PowerToys Run!  Thanks [@TobiasSekan](https://github.com/TobiasSekan)
- Child and Popup window support for FancyZones.  To enable, go to Settings→FancyZones→Windows. These were bugs we fixed that without realizing it was a useful feature to most.
- Find my mouse will now activate via shaking the mouse with a settings change!
![Find my mouse setting for Activate to shake](https://user-images.githubusercontent.com/1462282/156048784-5a16ae0e-3551-47c6-a601-833acc9e893b.png)

### Always on Top

- Fixed excess GPU / CPU usage when enabled
- If border has focus, not closable via F4
- Changing border sizes should resize correctly for existing windows
- Border goes away with Outlook modal windows 

### ColorPicker

- No longer crashes during theme change

### FancyZones

- Increased zone limit from 40 to 128.
- Child and Popup window support for FancyZones. To enable, go to Settings→FancyZones→Windows. These were bugs we fixed that without realizing it was a useful feature to most.

### File explorer

- Fixes for Dev file preview: (Thanks [@Aaron-Junker](https://github.com/Aaron-Junker))
  - Fix fix for object reference not set.
  - Fix for encoding UTF-8.
  - Fix for file is in use. 
  - Fix for saying the file is too big.

### Image Resizer

- Fix for `invalid operation` error. [@CleanCodeDeveloper](https://github.com/CleanCodeDeveloper)!

### Mouse utility

- Find my mouse will now activate via shaking the mouse with a settings change!
- Find my mouse now can excludes apps.
- Fix for crosshair thickness looking odd due to anti-aliasing
- Fix for a hard crash on crosshair (This was a hotfix for 0.55)

### PowerRename

- Will not spells check file names anymore. Thanks [@niels9001](https://github.com/niels9001)
- Autocomplete result box to not obscure apply button. Thanks [@niels9001](https://github.com/niels9001)
- Regex fix

### PowerToys Run

- Timezone conversion plugin for PowerToys Run!  Thanks [@TobiasSekan](https://github.com/TobiasSekan)
- Hexadecimal and binary numbers now are supported in the calculator plugin. This was added a bit ago and we'd like to extend a belated thanks to [@gsuberland](https://github.com/gsuberland)
- Terminal plugin performance boost.  Thanks [@htcfreek](https://github.com/htcfreek)!
- Terminal will now be found via the Program plugin again.  
- Shutdown command is now using hybrid fast argument for shutting down
- Support for VSCodium with VS Code workplace plugin. Thanks [@makeProjectGreatAgain](https://github.com/makeProjectGreatAgain)

### Video conference mute

- nVidia Broadcast software won't crash anymore

### Settings

- Upgrade notes in OOBE
- Fix for settings being lost (This was a hotfix for 0.55)
- UX improvements. Thanks [@niels9001](https://github.com/niels9001)

### Installer

- Believe we have a fix for the long hated "app.dark.png is missing" error. Thanks to [@robmen](https://github.com/robmen) for having a great blog!
- Installer will launch PowerToys under appropriate elevation versus Admin only due to UAC prompt for installation (This was a hotfix for 0.55)
- PowerToys will now start if installed under different user

### Development

- ARM64 - We removed the last .NET Framework dependency.  Thanks [@snickler](https://github.com/snickler) for helping get this across the finish line!
- .NET 6 upgrade, now on the newest and hottest .NET runtime
- Code analyzers have been upgraded!  Thanks [@CleanCodeDeveloper](https://github.com/CleanCodeDeveloper)!
- Symbols are back!
- Code refactoring, thanks [@CleanCodeDeveloper](https://github.com/CleanCodeDeveloper) and [@cyberrex5](https://github.com/cyberrex5) for helping here!
- We are now on VS 2022 with the .NET 6 upgrade.

#### Community contributions

We'd like to directly mention certain contributors (in alphabetical order) for their continued community support this month and helping directly make PowerToys a better piece of software. 
[@Aaron-Junker](https://github.com/Aaron-Junker), [@CleanCodeDeveloper](https://github.com/CleanCodeDeveloper), [@cyberrex5](https://github.com/cyberrex5), [@franky920920](https://github.com/franky920920), [@gsuberland](https://github.com/gsuberland), [@htcfreek](https://github.com/htcfreek), [@jay-o-way](https://github.com/jay-o-way), [@makeProjectGreatAgain](https://github.com/makeProjectGreatAgain), [@niels9001](https://github.com/niels9001), [@robmen](https://github.com/robmen), [@snickler](https://github.com/snickler), and [@TobiasSekan ](https://github.com/TobiasSekan).


#### What is being planned for v0.57

For [v0.57][github-next-release-work], we'll start work on below:

- Start work on two new PowerToys
- Improvements to PowerToy Run plugins
- Stability / bug fixes
- Validation pass again using WinUI 3.1 for Settings
- Adding new file types to dev file preview

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
[github-next-release-work]: https://github.com/microsoft/PowerToys/issues?q=is%3Aopen+is%3Aissue+project%3Amicrosoft%2FPowerToys%2F30
[github-current-release-work]: https://github.com/microsoft/PowerToys/issues?q=is%3Aopen+is%3Aissue+project%3Amicrosoft%2FPowerToys%2F29

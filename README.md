# Microsoft PowerToys

<img src="./doc/images/overview/PT%20hero%20image.png"/>

[How to use PowerToys][usingPowerToys-docs-link] | [Downloads & Release notes][github-release-link] | [Contributing to PowerToys](#contributing) | [What's Happening](#whats-happening) | [Roadmap](#powertoys-roadmap)

## Build status

| Architecture | Main | Stable | Installer |
|--------------|------|--------|-----------|
| x64 | [![Build Status for Main](https://dev.azure.com/ms/PowerToys/_apis/build/status/microsoft.PowerToys?branchName=main)](https://dev.azure.com/ms/PowerToys/_build/latest?definitionId=219&branchName=main) | [![Build Status for Stable](https://dev.azure.com/ms/PowerToys/_apis/build/status/microsoft.PowerToys?branchName=stable)](https://dev.azure.com/ms/PowerToys/_build/latest?definitionId=219&branchName=stable) | [![Build Status for Installer](https://github-private.visualstudio.com/microsoft/_apis/build/status/CDPX/powertoys/powertoys-Windows-Official-master-Test?branchName=main)](https://github-private.visualstudio.com/microsoft/_build/latest?definitionId=61&branchName=main) |

## About

Microsoft PowerToys is a set of utilities for power users to tune and streamline their Windows experience for greater productivity. For more info on [PowerToys overviews and how to use the utilities][usingPowerToys-docs-link], or any other tools and resources for [Windows development environments](https://docs.microsoft.com/windows/dev-environment/overview), head over to [docs.microsoft.com][usingPowerToys-docs-link]! 

|              | Current utilities: |              |
|--------------|--------------------|--------------|
| [PowerToys Awake](https://aka.ms/PowerToysOverview_Awake) | [Color Picker](https://aka.ms/PowerToysOverview_ColorPicker) | [FancyZones](https://aka.ms/PowerToysOverview_FancyZones) |
| [File Explorer Add-ons](https://aka.ms/PowerToysOverview_FileExplorerAddOns) | [Image Resizer](https://aka.ms/PowerToysOverview_ImageResizer) | [Keyboard Manager](https://aka.ms/PowerToysOverview_KeyboardManager) | 
| [Mouse utilities](https://aka.ms/PowerToysOverview_MouseUtilities) | [PowerRename](https://aka.ms/PowerToysOverview_PowerRename) | [PowerToys Run](https://aka.ms/PowerToysOverview_PowerToysRun) | 
| [Shortcut Guide](https://aka.ms/PowerToysOverview_ShortcutGuide) | [Video Conference Mute](https://aka.ms/PowerToysOverview_VideoConference) |  |

## Installing and running Microsoft PowerToys

### Requirements

- Windows 11 or Windows 10 v1903 (18362) or newer.
- [.NET Core 3.1.20 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/thank-you/runtime-desktop-3.1.20-windows-x64-installer) or a newer 3.1.x runtime. The installer will handle this if not present.

### Via GitHub with EXE [Recommended]

#### Stable version

 [Microsoft PowerToys GitHub releases page][github-release-link], click on `Assets` at the bottom to show the files available in the release and then click on `PowerToysSetup-0.51.0-x64.exe` to download the PowerToys installer.

This is our preferred method.

### Via Microsoft Store

Install from the [Microsoft Store's PowerToys page][microsoft-store-link]. You must be using the [new Microsoft Store](https://blogs.windows.com/windowsExperience/2021/06/24/building-a-new-open-microsoft-store-on-windows-11/) which will be available for both Windows 11 and Windows 10.

### Via WinGet (Preview)
Download PowerToys from [WinGet](https://github.com/microsoft/winget-cli#installing-the-client). To install PowerToys, run the following command from the command line / PowerShell:

```powershell
winget install Microsoft.PowerToys -s winget
```

### Other install methods

There are [community driven install methods](./doc/unofficialInstallMethods.md) such as Chocolatey and Scoop.  If these are your preferred install solutions, this will have the install instructions.

### Processor support

We currently support the matrix below.

| x64 | x86 | ARM64 |
|:---:|:---:|:---:|
| [Supported][github-release-link] | [Issue #602](https://github.com/microsoft/PowerToys/issues/602) | [Issue #490](https://github.com/microsoft/PowerToys/issues/490) |

## Contributing

This project welcomes contributions of all types. Help spec'ing, design, documentation, finding bugs are ways everyone can help on top of coding features / bug fixes. We are excited to work with the power user community to build a set of tools for helping you get the most out of Windows.

We ask that **before you start work on a feature that you would like to contribute**, please read our [Contributor's Guide](CONTRIBUTING.md). We will be happy to work with you to figure out the best approach, provide guidance and mentorship throughout feature development, and help avoid any wasted or duplicate effort.

Most contributions require you to agree to a [Contributor License Agreement (CLA)][oss-CLA] declaring that you have the right to, and actually do, grant us the rights to use your contribution.

For guidance on developing for PowerToys, please read the [developer docs](/doc/devdocs) for a detailed breakdown. This includes how to setup your computer to compile.

## What's Happening

### PowerToys Roadmap

Our [prioritized roadmap][roadmap] of features and utilities that the core team is focusing on.

### 0.51 - November 2021 Update

TODO WRITE INTRO

```The [v0.49 release cycle](https://github.com/microsoft/PowerToys/issues?q=is%3Aopen+is%3Aissue+project%3Amicrosoft%2FPowerToys%2F25) introduces exciting new updates primarily centered around modernizing PowerRename's UI, adding a brand new mouse utility, and merging Video Conference Mute into the stable releases!

PowerRename's new UI brings a refreshed experience that reflects the modern UI theming of Windows 11, along with helpful regular expression guidance and file formatting tips. 

With the new mouse utility, PowerToys introduces functionality to quickly find your mouse position by double pressing the left <kbd>ctrl</kbd> key. This is ideal for large, high-resolution displays and low-vision users, with additional features and enhancements planned for future releases. Special thanks to [Raymond Chen](https://github.com/oldnewthing) for providing the base code PowerToys used to develop this feature. To learn more, check out our [Mouse Utilities documentation](https://aka.ms/PowerToysOverview_MouseUtilities) on Microsoft Docs!

As Video Conference Mute becomes available in the stable releases, there are still known quirks that we are actively working to address. These bugs are [tracked on our GitHub](https://github.com/microsoft/PowerToys/issues?q=is%3Aopen+is%3Aissue+label%3A%22Product-Video+Conference+Mute%22), and we welcome any and all feedback as we work to isolate and resolve the cause.

Color Picker's HEX format will no longer have the `#` character. This addresses issues with various color inputs that only accept six characters cutting off the last value. We apologize for any inconvenience this causes as we understand it impacts users who may prefer having `#` included. However, we believe this is the best solution while the custom string functionality ([#8305](https://github.com/microsoft/PowerToys/issues/8305)) is in development. 

Additional work in this release include stability updates and optimizations, installer updates, general bug fixes, and accessibility improvements.```

#### Highlights from v0.49

TODO 
- UPDATE LINKS
- ADD PROPER URLS FOR PEOPLE

**Prototype work**
- Always on top prototype of being actively worked on.  Right now you hit a key-combo and it enables it.  We are investigating ways to highlight the window in some form as well.

**Mouse utilities**
- Presentation mouse highlighter mode introduced! <kbd>Win</kbd>+<kbd>Shift</kbd>+<kbd>H</kbd> to toggle.<br />![dragmouse](https://user-images.githubusercontent.com/9866362/142475413-77b00bae-bd28-42ae-a6c8-0dc4356e8525.gif)
- Mouse modes play nicely with virtual desktops

**Settings**
- Settings UI tweaks [#14248](https://github.com/microsoft/PowerToys/issues/14248) - Thanks [@niels9001]()
- Resizing regression (Multiple) - Thanks [@davidegiacometti]()
- Live visual preview for FancyZones inside Settings []() - Thanks  Thanks [@niels9001]()

**Installer**
- Investigated how to fully shift to WIX bootstrapper and remove custom
- Investigated how to fully hsift to HKCU vs HKLM.

**Random helping out**

- Spellcheck fix - Thanks [@franky920920]()
- Fix a URL - Thanks [@JeffersonQin]()

**Development relevant**
- Our primary dev branch is now named `Main`.
- Adjusting plugin folder structure for PT Run [#10796](10796)

**Community contributions**

We'd like to directly mention certain contributors (in alphabetical order) for their continued community support this month and helping directly make PowerToys a better piece of software.  

TODO FULL LIST

#### What is being planned for v0.53

For [v0.53][github-next-release-work], due to holidays, we'll be in a maintance sprint but here are some of the larger items:

- Hope to add Always on top into PowerToys
- We are working to heavily reduce / remove the UAC prompt over the next few releases on install. This is a big shift so it is spanning multiple releases so we can isolate issues if they do occur. Work is tracked in [#10126](https://github.com/microsoft/PowerToys/issues/10126)
- Update the PowerToys Build Pipeline to allow .NET 6 integration
- Stability and bug fixes

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
[roadmap]: https://github.com/microsoft/PowerToys/wiki/Roadmap
[privacy-link]: http://go.microsoft.com/fwlink/?LinkId=521839
[vidConfOverview]: https://aka.ms/PowerToysOverview_VideoConference
[loc-bug]: https://github.com/microsoft/PowerToys/issues/new?assignees=&labels=&template=translation_issue.md&title=
[usingPowerToys-docs-link]: https://aka.ms/powertoys-docs

<!-- items that need to be updated release to release -->
[github-next-release-work]: https://github.com/microsoft/PowerToys/issues?q=is%3Aopen+is%3Aissue+project%3Amicrosoft%2FPowerToys%2F27

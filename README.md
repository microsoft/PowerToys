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

- Windows 11 or Windows 10 v2004 (19041) or newer.
- Our installer will install the following items:
   - [.NET 6.0.6 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/6.0#runtime-desktop-6.0.6) or a newer 6.0.x runtime.
   - [Microsoft Edge WebView2 Runtime](https://go.microsoft.com/fwlink/p/?LinkId=2124703) bootstrapper. This will install the latest version.
   - [Microsoft Visual C++ Redistributable](https://docs.microsoft.com/cpp/windows/latest-supported-vc-redist?view=msvc-170#visual-studio-2015-2017-2019-and-2022) installer. This will install one of the latest versions available.

### Via GitHub with EXE [Recommended]

 [Microsoft PowerToys GitHub releases page][github-release-link], click on `Assets` at the bottom to show the files available in the release. Please use the appropriate the PowerToys installer that matches your machine's architecture.  For most people, it is `x64`.
 
 - **For x64 processors (most common):** click on `PowerToysSetup-0.60.1-x64.exe`
 - **For ARM64 processors:** `PowerToysSetup-0.60.1-arm64.exe`

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

### 0.60 - June 2022 Update

In this release, we focused heavily on stability and improvements. Below are some of the highlights!

**Highlights**

- PowerRename and Image Resizer now appear on the new Windows 11 context menu.
- There's a new PowerToys Run plugin for OneNote. Thanks [@palenshus](https://github.com/palenshus)!
- FancyZones uses a new zone identification with monitor Id to increase stability and avoid zone resets.
- AlwaysOnTop now uses rounded corners for highlighting rounded windows on Windows 11.
- Added settings to PowerToys Run to better control the query results order. Thanks [@jefflord](https://github.com/jefflord)!

### Known issues
- After installing PowerToys, [the new Windows 11 context menu entries for PowerRename and Image Resizer might not appear before a system restart](https://github.com/microsoft/PowerToys/issues/19124). On some Windows 11 dev channel insider builds, the new context menu entries are not registering correctly and the classic context menu entries will be shown instead.
- There are reports of users who are [unable to open the Settings window](https://github.com/microsoft/PowerToys/issues/18015). This is being caused by incompatibilities with some applications (RTSS RivaTuner Statistics Server and MSI AfterBurner are known examples of this). If you're affected by this, please check the  linked issue to verify if any of the presented solutions works for you.

### General
- Upgraded the Windows App SDK runtimes to 1.1.1. (This was a hotfix for 0.59)

### Always on Top

- Added support for more diverse keyboard shortcuts with a fallback to low level keyboard hooks. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
- Added rounded corner highlights for rounded corner windows on Windows 11.

### FancyZones

- Fixed a bug where windows shown on all desktops were not working with FancyZones. (This was a hotfix for 0.59)
- When changing zone layouts, windows will match a new size/position if the option is on. (This was a hotfix for 0.59)
- Minor UI fix in FancyZones Editor. Thanks [@ZetaSp](https://github.com/ZetaSp)!
- Fixed proper canvas layout rendering in FancyZones Editor and the unscaled monitor resolution is now shown.
- Fixed an issue with transparency in certain windows causing the window to go blank.

### Image Resizer

- The Image Resizer entry is now shown in the new Windows 11 context menu.

### File explorer add-ons

- Add a viewBox attribute to svg file thumbnails so that it tries to show the whole image, similar to what was done in the preview handler.
- Removed access to a remote image in the tests for markdown preview.
- Fixed flakiness in the markdown preview test suite with proper component initialization timeouts.
- Fixed the leaking WebView2 resources caused by svg thumbnails.

### Keyboard Manager

- The Editor title bar is now shown in the immersive dark mode theme. Thanks [@WilliamABradley](https://github.com/WilliamABradley)!

### Mouse utility

- The Mouse Pointer Crosshairs default activation shortcut was changed to not collide with a special character combination on some internation keyboards.

### PowerRename

- Fixed the file enumeration logic to only change enumerations at the end of the file name.
- Clicking on regex/date and time cheat sheet appends that item to the selected search or replace text field.
- The PowerRename entry is now shown in the new Windows 11 context menu.
- The title bar is now shown in the immersive dark mode theme. Thanks [@WilliamABradley](https://github.com/WilliamABradley)!

### PowerToys Run

- A setting was added to disable and configure the input delay on searching queries. (This was a hotfix for 0.59)
- Fixed and added logs for default Web Browser detection. (This was a hotfix for 0.59)
- The Program plugin can now search .lnk shortcuts by their executable name. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
- A new plugin for OneNote was added. Thanks [@palenshus](https://github.com/palenshus)!
- Query caching and delayed execution was added to the OneNote plugin. Thanks [@palenshus](https://github.com/palenshus)!
- Quality of life fixes for the TimeZone plugin, including fixes for empty subtitles, missing time zones and results not being found when expected. Thanks [@TobiasSekan](https://github.com/TobiasSekan)!
- Calls to the obsolete WebRequest API were removed. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
- Added common names for the share settings in the Settings plugins. Thanks [@htcfreek](https://github.com/htcfreek)!
- The Calculator Mages engine was updated to 2.0.1, for higher precision. Thanks for the testing and for pushing for the changes [@htcfreek](https://github.com/htcfreek)!
- Translation fixes for the Calculator and TimeDate plugins. Thanks [@htcfreek](https://github.com/htcfreek)!
- An entry for "Search Settings" was added to the Settings plugin. Thanks [@jefflord](https://github.com/jefflord)!
- Removed uses of the deprecated BinaryFormatter, which contained vulnerabilities. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
- Added settings to better control the query results order. Thanks [@jefflord](https://github.com/jefflord)!
- The Terminal plugin now uses a better API to detect installed Terminal packages. Thanks [@htcfreek](https://github.com/htcfreek)!

### Settings

- Fixed a bug that said an update had failed, even if PowerToys was fully updated. (This was a hotfix for 0.59)
- OOBE window is resizable. (This was a hotfix for 0.59)
- OOBE can now show release notes through authenticated proxies.
- OOBE now hides the x64 and ARM64 installer hashes on the What's New page.
- Minor UI fix in the Keyboard Manager page. Thanks [@ZetaSp](https://github.com/ZetaSp)!
- Fix in internal data type of CheckBox controls. Thanks [@ghost1372](https://github.com/ghost1372)!
- The title bar is now shown in the immersive dark mode theme. Thanks [@WilliamABradley](https://github.com/WilliamABradley)!
- Fixed a crash accessing/loading the System.Management API on ARM64 versions of Windows.

### Installer
- Fixed signing of the setup custom actions dll in the new pipeline.
- The Visual C++ redistributable was updated to 14.32.31332 and fixed an installer error when a newer version was installed. Thanks [@snickler](https://github.com/snickler)!
- Updated the .NET dependency to 6.0.6.

### Development

- Clean up of the CA1031 warning suppression. Thanks [@davidegiacometti](https://github.com/davidegiacometti)!
- Support for ARM64 binaries was added to the Microsoft Store submission task. Thanks [@azchohfi](https://github.com/azchohfi)!
- Added code for a tool to help identify monitor IDs.
- Support for ARM64 binaries was added to the winget package creation task.
- Updated the Pull Request template to better reflect project changes.
- Component Governance checks were re-activated on the new main branch.
- CI is failing to run tests calling the newer WebView 2 version, so these were disabled until a fix is found.
- Updated the tests SDK to 17.2.0.
- Nuget package versions used in the solution were consolidated.
- The CodeQL CI task was disabled in the repo, but was causing issues on forks, so it was removed.
- A specific Newtonsoft.Json version was specified in tests to avoid a vulnerability present in previous versions.
- FabricBot configurations were added to the repository.
- Added a dependabot configuration for updating GitHub actions dependencies. Thanks [@naveensrinivasan](https://github.com/naveensrinivasan)!
- Updated the check-spelling action and added quality of life fixes to the workflow. Thanks [@jsoref](https://github.com/jsoref)!

#### What is being planned for v0.61

For [v0.61][github-next-release-work], we'll work on below:

- Environment Variables Editor PowerToy
- Screen Measure PowerToy
- Stability / bug fixes

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
[github-next-release-work]: https://github.com/microsoft/PowerToys/issues?q=is%3Aopen+is%3Aissue+project%3Amicrosoft%2FPowerToys%2F34
[github-current-release-work]: https://github.com/microsoft/PowerToys/issues?q=is%3Aopen+is%3Aissue+project%3Amicrosoft%2FPowerToys%2F33

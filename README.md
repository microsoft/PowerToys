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
- [.NET Core 3.1.22 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/thank-you/runtime-desktop-3.1.22-windows-x64-installer) or a newer 3.1.x runtime. The installer will handle this if not present.

### Via GitHub with EXE [Recommended]

 [Microsoft PowerToys GitHub releases page][github-release-link], click on `Assets` at the bottom to show the files available in the release and then click on `PowerToysSetup-0.53.3-x64.exe` to download the PowerToys installer.

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

#### 0.53 - December 2021 Update

We hope everyone has had a wonderful December 2021. The PowerToys community has been busy with a bunch of improvements. We're still working on improving the installer but this should drastically improve things.  3 things you will want to check out are G-code support in file explorer preview pane and thumbnails, the new web search plugin from PowerToys Run via <kbd>??</kbd> action phrase, and the new Always on Top utility via <kbd>Win</kbd>+<kbd>Ctrl</kbd>+<kbd>T</kbd>.

[@Aaron-Junker](https://github.com/Aaron-Junker) also has done some great progress on [implementing developer file preview pane](https://github.com/microsoft/PowerToys/issues/1527) via the Monaco engine from Visual Studio Code.

#### Things to be aware of
- The new installer currently has a visual quirk when upgrade if you have a custom install path.  It will show the default install path but it will actually overwrite the current location.  We are investigating how to fix this.

#### Always on Top
- Welcome to the family!  With a quick <kbd>Win</kbd>+<kbd>Ctrl</kbd>+<kbd>T</kbd>, the window in focus is toggled to be on top.  Toggle again, and it reverts back to normal.

#### ColorPicker
- HEX input improvements for adjust color menu including support for hex code without hashtag and short hex code like #CF0.  Thanks @htcfreek! 
- Better bottom right screen detection for overlay

#### FancyZones
- Increased negative space margin
- Fix for not snapping child windows
- Fix for clearing keyboard focus on editor launch
- Fix to improve overlays to reduce brightness and hide numbers. Thanks @davidegiacometti

#### File Explorer
- Added G-code support for thumbnails and preview pane.  Thanks @pedrolamas 

#### Image Resizer
- Fixed regression from Metadata tag removal of ColorSpace.  Thanks @CleanCodeDeveloper

#### PowerRename
- Row highlighting + preview support now implemented. Thanks @niels9001
- Fixed AltGR input issue
- Improved folder renaming support
- Opens on active monitor

#### PowerToys Run
- Web searching has been added! `?? What is the answer to life` will go to your favorite search engine via your browser.  You can change the default action key too!  Thanks @cyberrex5 for primary implementation and @franky920920 and @htcfreek for supporting
- VS Code workspace improvements. Thanks @ricardosantos9521
- Binary and Hex number support.  Thanks @gsuberland
- Ability to use factorials in calculations
- PT Run will not show in Window Walker results anymore.  Thanks @davidegiacometti
- Fix log / ln calculations
- Fix to make previous results clear
- Fix to detect symlinks and prevent recursive loops
- Fix for trackpad scrolling being too fast
- Removed unneeded nuget package.  Thanks @ChaseKnowlden
- Better detection for if a packaged app can be elevated
- Improve crash resiliency for Program plugin.  Thanks @davidegiacometti
- Improved Windows setting results.  Thanks @htcfreek
- Fixed a bug where some similar activation phrases aren't working as expected. Thanks @htcfreek and @cyberrex5.

#### Video conference mute
- Disabled by default as this requires elevation to register the virtual camera.
- Changed (default) hotkey for mute camera & microphone from <kbd>Win</kbd>+<kbd>N</kbd> to <kbd>Win</kbd>+<kbd>Shift</kbd>+<kbd>Q</kbd> to not conflict with a Windows 11 keyboard shortcut

#### Settings
- Multiple accessibility, layout, image, string and icons fixes. Thanks @niels9001

#### Runner
- Improved mutex support to prevent multiple PT Run instances from running

#### Installer
- **NOTE:**  The new installer currently has a visual quirk when upgrade if you have a custom install path.  It will show the default install path but it will actually overwrite the current location.  We are investigating how to fix this.
- Large progress toward user based installing vs machine wide. Upgrade scenario still needs additional work.
- Removed custom bootstrapper and now are using a WiX bundle.
- Removed unused image assets that were still being shipped. Thanks @niels9001

#### ARM64 support
- Setting WinUI3 proof-of-concept and validate we do need at least one more feature, elevation support from WinUI 3 unpackaged applications.

#### Dev improvements
- New YAML based pipeline for building our signed installer. This will allow us to consolidate our CI to use same file. This was critical for us to unblock ARM64 and .NET 6 migration.
- Our submodules will no longer auto fetch to prevent locking issues. If you want a refresher on how to do this, head to [our dev docs](https://github.com/microsoft/PowerToys/tree/main/doc/devdocs#get-submodules-to-compile)
- Localization system shifted to Touchdown from CDPx.  This should remove many of the loc issues.
- Consolidated a lot of the naming of EXEs and DLLs along with projects
- Update to spell checker.  Thanks @jsoref
- /dup response has been added
- /reportbug /bugreport will ask for a "report bug" zip

#### Community contributions

We'd like to directly mention certain contributors (in alphabetical order) for their continued community support this month and helping directly make PowerToys a better piece of software. 

[@Aaron-Junker](https://github.com/Aaron-Junker), [@ChaseKnowlden](https://github.com/ChaseKnowlden), [@CleanCodeDeveloper](https://github.com/CleanCodeDeveloper), [@cyberrex5](https://github.com/cyberrex5), [@davidegiacometti](https://github.com/davidegiacometti), [@franky920920](https://github.com/franky920920), [@gsuberland](https://github.com/gsuberland),  [@jay-o-way](https://github.com/jay-o-way), [@jsoref](https://github.com/jsoref), [@niels9001](https://github.com/niels9001), and [@ricardosantos9521](https://github.com/ricardosantos9521)

#### What is being planned for v0.55

For [v0.55][github-next-release-work], we'll work on adding more stability in with VCM and getting dev file preview pane added in so we get 150 file types :)

- We are working to heavily reduce / remove the UAC prompt over the next few releases on install. This is a big shift so it is spanning multiple releases so we can isolate issues if they do occur. Work is tracked in [#10126](https://github.com/microsoft/PowerToys/issues/10126)
- Getting the dev file preview pane work integrated. (Monaco Editor)
- .NET 6 upgrade to all available surfaces
- Find my mouse feature, accessibility cross-hair

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
[github-next-release-work]: https://github.com/microsoft/PowerToys/issues?q=is%3Aopen+is%3Aissue+project%3Amicrosoft%2FPowerToys%2F28
[github-current-release-work]: https://github.com/microsoft/PowerToys/issues?q=is%3Aopen+is%3Aissue+project%3Amicrosoft%2FPowerToys%2F27

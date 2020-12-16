# Overview

<img src="./doc/images/overview/PT%20hero%20image.png"/>

Welcome to Microsoft PowerToys! **For new users, check out our newly published [Mircosoft Docs: PowerToys](https://docs.microsoft.com/en-us/windows/powertoys/) page to get started.** The content on this repo will be focused on relevant updates and resources for active contributors and developers of the various PowerToys projects. For info on [downloads & installation](https://docs.microsoft.com/en-us/windows/powertoys/install), [PowerToys guides and overviews](https://docs.microsoft.com/en-us/windows/powertoys/), or any other tools and resources for [Windows development environments](https://docs.microsoft.com/en-us/windows/dev-environment/overview), be sure to check out Microsoft Docs!

[Known issues](#known-issues) | [What's Happening](#whats-happening) | [Core-Team Roadmap](#powertoys-roadmap) | [Contributing to PowerToys](#contributing)

## Build status

| Architecture | Master | Stable | Installer |
|--------------|--------|--------|-----------|
| x64 | [![Build Status for Master](https://dev.azure.com/ms/PowerToys/_apis/build/status/microsoft.PowerToys?branchName=master)](https://dev.azure.com/ms/PowerToys/_build/latest?definitionId=219&branchName=master) | [![Build Status for Stable](https://dev.azure.com/ms/PowerToys/_apis/build/status/microsoft.PowerToys?branchName=stable)](https://dev.azure.com/ms/PowerToys/_build/latest?definitionId=219&branchName=stable) | [![Build Status for Installer](https://github-private.visualstudio.com/microsoft/_apis/build/status/CDPX/powertoys/powertoys-Windows-Official-master-Test?branchName=master)](https://github-private.visualstudio.com/microsoft/_build/latest?definitionId=61&branchName=master) |


## Known Issues

- Color Picker at times won't work when PT is running elevated - [#5348](https://github.com/microsoft/PowerToys/issues/5348).  We are currently working on a fix now for this.

### Processor support

We currently support the matrix below.

| x64 | x86 | ARM |
|:---:|:---:|:---:|
| [Supported][github-release-link] | [Issue #602](https://github.com/microsoft/PowerToys/issues/602) | [Issue #490](https://github.com/microsoft/PowerToys/issues/490) |

## What's Happening

### November 2020 Update

Our goals for [v0.27 release cycle][github-release-link] were to focus on adding on end-user experience, stability, accessibility, localization and quality of life improvements for both the development team and our end users.  Our [prioritized roadmap][roadmap] of features and utilities that the core team is focusing on for the near future. We fixed a lot of localization issues from our initial release but we may not still be perfect. If you find an issue, please file a [localization bug][loc-bug].

#### Highlights from v0.27

**General**
- Installer improvements including dark mode
- Large sums of accessibility issues fixed.
- Worked on localization effort. If you find issues, please [make us aware so we can correct them][loc-bug].

**Color Picker**
- Updated interface and new editor experience done by [@martinchrzan](https://github.com/martinchrzan) and [@niels9001](https://github.com/niels9001)

**FancyZones**
- Multi-monitor editor experience now drastically improved for discoverability.
- Zones being forgotten on restart
- Added in ability to have no layout

**Image Resizer**
- Updated interface

**PowerToys Run**
- Removed unused dependencies

**PowerRename**
- Added Lookbehind support via Boost library

I'd like to directly call out [@davidegiacometti](https://github.com/davidegiacometti), [@gordonwatts](https://github.com/gordonwatts), [@martinchrzan](https://github.com/martinchrzan), [@niels9001](https://github.com/niels9001), [@p-storm](https://github.com/p-storm), [@TobiasSekan](https://github.com/TobiasSekan), [@Aaron-Junker](https://github.com/Aaron-Junker), [@htcfreek](https://github.com/htcfreek) and [@alannt777](https://github.com/alannt777) for their continued community support and helping directly make PowerToys a better piece of software.

#### Experimental PowerToys utility with Video conference muting

Install the [v0.28 pre-release experimental version of PowerToys][github-prerelease-link] to try out this version. It includes all improvements from v0.27 in addition to the Video conference utility. Click on `Assets` to show the files available in the release and then download the .exe installer.

#### What is being planned for v0.29 - December 2020

For [v0.29](https://github.com/microsoft/PowerToys/issues?q=is%3Aopen+is%3Aissue+project%3Amicrosoft%2FPowerToys%2F15), we are proactively working on:

- Stability
- Accessibility
- Video conference mute investigation toward a DirectShow filter versus a driver
- OOBE work

## PowerToys Roadmap

Our [prioritized roadmap][roadmap] of features and utilities that the core team is focusing on.

## Contributing

This project welcomes contributions of all types. Help spec'ing, design, documentation, finding bugs are ways everyone can help on top of coding features / bug fixes. We are excited to work with the power user community to build a set of tools for helping you get the most out of Windows.

We ask that **before you start work on a feature that you would like to contribute**, please read our [Contributor's Guide](CONTRIBUTING.md). We will be happy to work with you to figure out the best approach, provide guidance and mentorship throughout feature development, and help avoid any wasted or duplicate effort.

For guidance on developing for PowerToys, please read the [developer docs](/doc/devdocs) for a detailed breakdown.

### ⚠ State of code ⚠

PowerToys is still a very fluidic project and the team is actively working out of this repository.  We will be periodically re-structuring/refactoring the code to make it easier to comprehend, navigate, build, test, and contribute to, so **DO expect significant changes to code layout on a regular basis**.

### License Info

 Most contributions require you to agree to a [Contributor License Agreement (CLA)][oss-CLA] declaring that you have the right to, and actually do, grant us the rights to use your contribution.

## Acknowledgements

The PowerToys team is extremely grateful to have the support of an amazing active community. The work you do is incredibly important. PowerToys wouldn’t be nearly what it is today without your help filing bugs, updating documentation, guiding the design, or writing features. We want to say thank you and take time to recognize your work.

Below we'd like to specifically call-out and thank our high impact community members, ordered alphabetical based on first name. More special call-outs and thanks can be found in our [COMMUNITY.md](COMMUNITY.md) file.

#### [@davidegiacometti](https://github.com/davidegiacometti) - [Davide Giacometti](https://www.linkedin.com/in/davidegiacometti/)
Davide has helped fix multiple bugs, added new features, as well as helps us with the ARM64 effort by porting applications to .NET Core.

#### [@Niels9001](https://github.com/niels9001/) - [Niels Laute](https://nielslaute.com/)

Niels has helped drive large sums of our update toward a new [consistent and modern UX](https://github.com/microsoft/PowerToys/issues/891). This includes the [launcher work](https://github.com/microsoft/PowerToys/issues/44), color picker UX update and [icon design](https://github.com/microsoft/PowerToys/issues/1118).

#### [@riverar](https://github.com/riverar) - [Rafael Rivera](https://withinrafael.com/)

Rafael has helped do the [upgrade from CppWinRT 1.x to 2.0](https://github.com/microsoft/PowerToys/issues/1907). He directly provided feedback to the CppWinRT team for bugs from this migration as well.

#### [@royvou](https://github.com/royvou)
Roy has helped out contributing features to PowerToys Run

## Code of Conduct

This project has adopted the [Microsoft Open Source Code of Conduct][oss-conduct-code].

## Privacy Statement

The application logs basic telemetry. Our Telemetry Data page (Coming Soon) has the trends from the telemetry. Please read the [Microsoft privacy statement][privacyLink] for more information.

[oss-CLA]: https://cla.opensource.microsoft.com
[oss-conduct-code]: CODE_OF_CONDUCT.md
[github-release-link]: https://github.com/microsoft/PowerToys/releases/
[github-prerelease-link]: https://github.com/microsoft/PowerToys/releases/tag/v0.28.0
[roadmap]: https://github.com/microsoft/PowerToys/wiki/Roadmap
[privacyLink]: http://go.microsoft.com/fwlink/?LinkId=521839
[vidConfOverview]: https://aka.ms/PowerToysOverview_VideoConference
[loc-bug]: https://github.com/microsoft/PowerToys/issues/new?assignees=&labels=&template=translation_issue.md&title=

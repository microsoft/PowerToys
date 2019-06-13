
# Overview

PowerToys is a set of utilities for power users to tune and streamline their Windows experience for greater productivity.  

Inspired by the [Windows 95 era PowerToys project](https://en.wikipedia.org/wiki/Microsoft_PowerToys), this reboot provides power users with ways to squeeze more efficiency out of the Windows 10 shell and customize it for individual workflows.  A great overview of the Windows 95 PowerToys can be found [here](https://socket3.wordpress.com/2016/10/22/using-windows-95-powertoys/).

The first preview of these utilities and corresponding source code will be released Summer 2019.

![logo](Logo.jpg)

# What's Happening

## June Update
Since the announcement of the PowerToys reboot at BUILD, the interest in the project has been incredible to see.  Due to the excitement we are optimizing the first preview to make it easy to integrate new utilities into the repo.  We also have two interns working on additional PowerToys.  The specs for these are:

* [Process terminate tool](https://github.com/indierawk2k2/PowerToys-1/blob/master/specs/Terminate%20Spec.md)
* [Batch file renamer](https://github.com/indierawk2k2/PowerToys-1/blob/master/specs/File%20Classification%20Spec.md)
* [Animated gif screen recorder](https://github.com/indierawk2k2/PowerToys-1/blob/master/specs/GIF%20Maker%20Spec.md)

Finally, we are organizing a team to productize an internal window manager into the PowerToys project for the 2019 [One Week Hackathon](https://www.onmsft.com/news/take-a-peek-inside-microsofts-recent-one-week-hackathon).

We are still targeting to release the preview and code during Summer 2019.

## The first two utilities we're working on are:

1. Maximize to new desktop widget - The MTND widget shows a pop-up button when a user hovers over the maximize / restore button on any window.  Clicking it creates a new desktop, sends the app to that desktop and maximizes the app on the new desktop.

![Maximize to new desktop widget](MTNDWidget.jpg)

2. Windows key shortcut guide - The shortcut guide appears when a user holds the Windows key down for more than one second and shows the available shortcuts for the current state of the desktop.

![Windows key shortcut guide](WindowsKeyShortcutGuide.jpg)

# Backlog

Here's the current set of utilities we're considering.  Please use issues and +1's to guide the project to suggest new ideas and help us prioritize the list below.

1. [Full window manager including specific layouts for docking and undocking laptops](https://github.com/microsoft/PowerToys/issues/4)
2. [Keyboard shortcut manager](https://github.com/microsoft/PowerToys/issues/6)
3. [Win+R replacement](https://github.com/microsoft/PowerToys/issues/44)
4. Better Alt+Tab including browser tab integration and search for running apps
5. [Battery tracker](https://github.com/microsoft/PowerToys/issues/7)
6. [Batch file re-namer](https://github.com/microsoft/PowerToys/issues/101)
7. [Quick resolution swaps in taskbar](https://github.com/microsoft/PowerToys/issues/27)
8. Mouse events without focus
9. Cmd (or PS or Bash) from here
10. Contents menu file browsing

# Contributing

This project welcomes contributions and suggestions.  Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit https://cla.microsoft.com.

When you submit a pull request, a CLA-bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., label, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.

# UI Architecture

 The UI code is distributed between two projects: [`PowerToys.Settings`](/src/settings-ui/Settings.UI) and [`Settings.UI`](/src/settings-ui/Settings.UI.Library). [`PowerToys.Settings`](/src/settings-ui/Settings.UI) is a Windows App Sdk .net Unpackaged application. It contains the views for base navigation and modules. Parent display window and corresponding code is present in [`MainWindow.xaml`](/src/settings-ui/Settings.UI/SettingsXAML/MainWindow.xaml). Fig 1 provides a description of the UI controls hierarchy and each of the controls have been summarized below : 
- [`ShellPage.xaml`](/src/settings-ui/Settings.UI/SettingsXAML/Views/ShellPage.xaml) is a WinUI control, consisting of a side navigation panel with an icon for each module. Clicking on a module icon loads the corresponding `setting.json` file and displays the data in the UI.

![Settings UI architecture](/doc/images/settingsv2/ui-architecture.png)
**Fig 1: UI Architecture for settingsv2**

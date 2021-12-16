# UI Architecture

 The UI code is distributed between two projects: [`PowerToys.Settings`](/src/settings-ui/PowerToys.Settings) and [`Settings.UI`](/src/settings-ui/Settings.UI.Library). [`PowerToys.Settings`](/src/settings-ui/PowerToys.Settings) is a WPF .net core application. It contains the parent display window and corresponding code is present in [`MainWindow.xaml.`](/src/settings-ui/PowerToys.Settings/MainWindow.xaml) [`Settings.UI`](/src/settings-ui/Settings.UI.Library) is UWP applications and contains views for base navigation and modules. Fig 1 provides a description of the UI controls hierarchy and each of the controls have been summarized below : 
- [`MainWindow.xaml`](/src/settings-ui/PowerToys.Settings/MainWindow.xaml) is the parent WPF control.
- `WindowsXamlHost` control is used to host UWP control to [`MainWindow.xaml`](/src/settings-ui/PowerToys.Settings/MainWindow.xaml)  parent control.
- [`ShellPage.xaml`](/src/settings-ui/Settings.UI/Views/ShellPage.xaml) is a UWP control, consisting of a side navigation panel with an icon for each module. Clicking on a module icon loads the corresponding `setting.json` file and displays the data in the UI.

![Settings UI architecture](/doc/images/settingsv2/ui-architecture.png)
**Fig 1: UI Architecture for settingsv2**

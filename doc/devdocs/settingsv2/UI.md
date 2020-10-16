# UI Architecture

 The UI code is distributed between two projects: `Microsoft.PowerToys.Settings.UI.Runner` and `Microsoft.PowerToys.Settings.UI.` `Microsoft.PowerToys.Settings.UI.Runner` is a WPF .net core application. It contains the parent display window and corresponding code is present in [`MainWindow.xaml`](/src/core/Microsoft.PowerToys.Settings.UI.Runner/MainWindow.xaml). `Microsoft.PowerToys.Settings.UI` is UWP applications and contains views for base navigation and modules. XAML island and `WindowsXamlHost` control display the custom UWP views on a WPF window. [ShellPage.xaml]() consists of a side navigation panel with an icon for each module. Clicking on a module icon loads the underlying `setting.json` file and displays the data in the UI.  Fig 1 below provides a description of the UI controls hierarchy.

 ![Settings UI architecture](/doc/images/SettingsV2/ui_architecture.png)
**Fig 1: UI Architecture for settingsv2**
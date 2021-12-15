# Overview
`Settingsv2` is WPF .net core desktop application. It uses the `WindowsXamlHost` control from the Windows Community Toolkit to host UWP controls from `WinUI3` library. More details about WinUI can be found [here](https://microsoft.github.io/microsoft-ui-xaml/about.html#what-is-it).

## Settings V2 Project structure
The Settings project is a XAML island based project which
follows the [MVVM architectural pattern][MVVM] where the graphical user interface is separated from the view models.

#### [UI Components:](/src/settings-ui/Settings.UI)
The Settings.UI project contains the xaml files for each of the UI components. It also contains the Hotkey logic for the settings control.

#### [Viewmodels:](/src/settings-ui/Settings.UI.Library)
The Settings.UI.Library project contains the data that is to be rendered by the UI components.

#### [Settings Runner:](/src/settings-ui/PowerToys.Settings)
The function of the settings runner project is to communicate all changes that the user makes in the user interface, to the runner so that it can be dispatched and reflected in all the modules.

[MVVM]: https://docs.microsoft.com/en-us/windows/uwp/data-binding/data-binding-and-mvvm
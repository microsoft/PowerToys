# Settings V2 Project structure

The Settings project is a XAML island based project which
follows the [MVVM architectural pattern][MVVM] where the graphical user interface is separated from the view models.

#### UI Components: [project source](/src/core/Microsoft.PowerToys.Settings.UI)
The Settings.UI project contains the xaml files for each of the UI components. It also contains the Hotkey logic for the settings control.

#### Viewmodels: [project source](/src/core/Microsoft.PowerToys.Settings.UI.Lib)
The Settings.UI.Lib project contains the data that is to be rendered by the UI components.

#### Settings Runner: [project source](/src/core/Microsoft.PowerToys.Settings.UI.Runner)
The function of the settings runner project is to communicate all changes that the user makes in the user interface, to the runner so that it can be dispatched and reflected in all the modules.

[MVVM]: https://docs.microsoft.com/en-us/windows/uwp/data-binding/data-binding-and-mvvm
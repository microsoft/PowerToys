# Window Walker plugin
The window walker plugin matches the user entered query with the open windows on the system.

![Image of Window Walker plugin](/doc/images/launcher/plugins/windowwalker.png)

### [`OpenWindows.cs`](src/modules/launcher/Plugins/Microsoft.Plugin.WindowWalker/Components/OpenWindows.cs)
- The window walker plugin uses the `EnumWindows` function to enumerate all the open windows in the [`OpenWindows.cs`](src/modules/launcher/Plugins/Microsoft.Plugin.WindowWalker/Components/OpenWindows.cs) class.


### [`SearchController.cs`](src/modules/launcher/Plugins/Microsoft.Plugin.WindowWalker/Components/SearchController.cs)
- The [`SearchController`](src/modules/launcher/Plugins/Microsoft.Plugin.WindowWalker/Components/SearchController.cs) encapsulates the functions needed to search and find matches.
- It is responsible for updating the search text and performing a fuzzy search on all the open windows in an asynchronous manner.

### [`Window.cs`](src/modules/launcher/Plugins/Microsoft.Plugin.WindowWalker/Components/Window.cs)
- The [`Window`](src/modules/launcher/Plugins/Microsoft.Plugin.WindowWalker/Components/Window.cs) class represents a specific window and has functions to get the name of the process, the state of the window (whether it is visible or not), and the `SwitchTowindow` function which switches the desktop focus to the selected window. This action is performed when the user clicks on a window walker plugin result.

### Score
The window walker plugin uses [`FuzzyMatching`](src/modules/launcher/Plugins/Microsoft.Plugin.WindowWalker/Components/FuzzyMatching.cs) to get the matching indices and calculates the score by creating a 2 dimensional array of the window and the query text.

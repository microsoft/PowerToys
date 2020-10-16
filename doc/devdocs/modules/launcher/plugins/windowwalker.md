# Window Walker plugin
The window walker plugin matches the user entered query with the open windows on the system.

### [`OpenWindows.cs`](src/modules/launcher/Plugins/Microsoft.Plugin.WindowWalker/Components/OpenWindows.cs)
- The window walker plugin uses the `EnumWindows` function to enumerate all the open windows in the [`OpenWindows.cs`](src/modules/launcher/Plugins/Microsoft.Plugin.WindowWalker/Components/OpenWindows.cs) class.


### [`SearchController.cs`](src/modules/launcher/Plugins/Microsoft.Plugin.WindowWalker/Components/SearchController.cs)
- 

### [`Window.cs`](src/modules/launcher/Plugins/Microsoft.Plugin.WindowWalker/Components/Window.cs)
- 

### Score
The window walker plugin uses [`FuzzyMatching`](src/modules/launcher/Plugins/Microsoft.Plugin.WindowWalker/Components/FuzzyMatching.cs) to get the matching indices and calculates the score by creating a 2 dimensional array of the window and the query text.

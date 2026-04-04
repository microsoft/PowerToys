# OneNote Plugin

The OneNote plugin searches your locally synced OneNote notebooks based on the user query.

<p>
<img src="/doc/images/launcher/plugins/onenote.png" alt="default menu" height="200"/>

<img src="/doc/images/launcher/plugins/onenote_search.png" alt="search" height="200"/>

<img src="/doc/images/launcher/plugins/onenote_notebook_explorer.png" alt="notebook explorer" height="200"/>
</p>

This is essentially a port of this [OneNote plugin](https://github.com/Odotocodot/Flow.Launcher.Plugin.OneNote) for [Flow Launcher](https://github.com/Flow-Launcher/Flow.Launcher) (also built on Wox) directly into PowerToys Run.

The code is largely a wrapper around the [LinqToOneNote](https://github.com/Odotocodot/LinqToOneNote) library.

## OneNote Interop COM Object

Typically the slowest part of the plugin is acquiring the OneNote COM object (via `OneNoteApplication.InitComObject` or lazily), and once acquired it stays in memory and is visible in the task manager (See [LinqToOneNote docs](https://odotocodot.github.io/LinqToOneNote/articles/memory_management.html) for more info).

To avoid this, once the COM object is acquired there is a timer (`_comObjectTimeout`) that starts, which is reset whenever the user continues searching. When this timer reaches zero, the COM object is released, freeing it from memory and removing it from the task manager.

This is done in [`Main.cs`](/src/modules/launcher/Plugins/Microsoft.PowerToys.Run.Plugin.OneNote/Main.cs).

The COM Object is also released when the user selects a result that would close the PowerToys Run window, this is done in [`ResultCreator.ResultAction`](/src/modules/launcher/Plugins/Microsoft.PowerToys.Run.Plugin.OneNote/Components/ResultCreator.cs)

The timeout is used because there is currently no way to know when the PTRun window has been closed when the user is querying.

## Technical Details

### [`SearchManager.cs`](/src/modules/launcher/Plugins/Microsoft.PowerToys.Run.Plugin.OneNote/Components/Search/SearchManager.cs)
- Responsible for converting the user query into the appropriate OneNote items.
- [`NotebookExplorer.cs`](/src/modules/launcher/Plugins/Microsoft.PowerToys.Run.Plugin.OneNote/Components/Search/NotebookExplorer.cs)
    - Handles the "notebook explorer" feature which allows the user to navigate their OneNote items like Windows File Explorer.
    - The function `AddCreateNewOneNoteItemResults` is responsible for allowing the user to create new OneNote items quickly, the type of items that can be created are dependent on the current parent in the notebook explorer.
- There are 3 main types of searching:
    - `DefaultSearch` - Uses `OneNoteApplication.FindPages(query)`, which relies on Windows Indexing and is essentially the same as using the search box inside of OneNote. (Only returns OneNote pages)
    - `TitleSearch` - Searches items only by their title, can return OneNote pages, sections, section groups etc, uses `StringMatcher.FuzzySearch`.
    - `ScopedSearch` - Similar to `DefaultSearch` but only searches within a specific item. For instance, searching pages only in a certain section group.
- The function `SettingsCheck` is used to filter the search results by the user configured settings.

### [`ResultCreator.cs`](/src/modules/launcher/Plugins/Microsoft.PowerToys.Run.Plugin.OneNote/Components/ResultCreator.cs)
- Responsible for taking any data and converting it into a Wox `Result` for displaying.

### [`IconProvider.cs`](/src/modules/launcher/Plugins/Microsoft.PowerToys.Run.Plugin.OneNote/Components/IconProvider.cs)
- Responsible for providing all the icons for the plugin.
- If the user has set the `ColoredIcons` setting to true. Colored icons are created and saved/cached for OneNote notebooks and sections.
- Supports light and dark theme.
- When the user sets `ColoredIcons` to false, the colored icons are deleted when the PowerToys Run is closed.

# History Plugin

The History Plugin allows users to search or display results they have used (selected).

## How it works
The plugin uses data that was already being captured which is, what results were clicked, and how many times. We do add a little more data to this set now.
When this plugin is queried, it creates results based on this previously selected results data.

In order to make sure selected results in the history are still valid, we re-query the plugin the relevant plug using the PluginManager. If there are no results,
this history item is not included. This usually means that the result is no longer valid. For instance, if a file was deleted, but it's still in the selected history
we don't want to show it as a selectable result.

Because the results from the History Plugin are actually created from calls to the PluginManager, they will be exactly the same is if they did not come from the History Plugin.

## Special notes
While the results returned from the plugin are from the PluginManager, they are sometimes modified before returning. One example is the Calculator plugin.
Since the Calculator plugin operates on the current query input by the user, the results from Calculator plugin don't include that in the title. However, as a history item,
the query is very important. In this case, and maybe others in the future, we modify the tile to also include the search.

### Modified title example:

This is what the Calculator plugin normally might show:
![image](https://user-images.githubusercontent.com/4396667/184661303-4f8cf0da-2956-46b9-bdc1-ed879cd0b7cc.png)

But this is how it will look returned from the History plugin

![image](https://user-images.githubusercontent.com/4396667/184661450-9ec3c416-66df-40c8-b004-da8b0cebc5c5.png)

As you can see, here and maybe other places, other non-history plugin might be able to include extra data for the History plugin to use later.
For example, in future, plugins might be able to also set a "History Title", "History Icon", etc... But for now, it's not needed.


## Duplicates from the History Plugin in global results
If the History plugin is set to show in the global results, it might return a result that is also returned from another plugin. If a match is found,
the result from the history plugin is discarded.

## Removing items from history
A new context menu item is added to each History result, which can be used to delete it from the history.
![image](https://user-images.githubusercontent.com/4396667/184656195-6d9f1a49-652c-4027-a424-535e9fb1f2a8.png)

## Context menus
Because these results are coming from the History plugin, this plugin must invoke each menu items `LoadContextMenus` method.
We then also add the "Remove this from history" context menu action.

## Results score
When the plugin is used with the activation command, the scores are configured so the results show with the more recently selected items at the top.
If the history results are shown in the global results, the scores are not modified from that the original plugin set.

## Old Data
Items selected before this plugin was created will not show in the history because they don't contain enough data.

## Important for developers

### Important plugin values (meta-data)

| Name            | Value                                                |
| --------------- | ---------------------------------------------------- |
| ActionKeyword   | `!!`                                                  |
| ExecuteFileName | `Microsoft.PowerToys.Run.Plugin.History.dll` |
| ID              | `C88512156BB74580AADF7252E130BA8D`                   |

### Interfaces used by this plugin

The plugin uses only these interfaces (all inside the `Main.cs`):

* `Wox.Plugin.IPlugin`
* `Wox.Plugin.IContextMenu`
* `Wox.Plugin.IPluginI18n`

### Program files

| File                                  | Content                                                                 |
| ------------------------------------- | ----------------------------------------------------------------------- |
| `Images\history.dark.png`             | Symbol for the results for the dark theme                               |
| `Images\history.light.png`            | Symbol for the results for the light theme                              |
| `Properties\Resources.Designer.resx`  | File that contain all translatable keys                                 |
| `Properties\Resources.resx`           | File that contains all translatable strings in the neutral language      |
| `Main.cs`                             | Main class, the only place that implements the WOX interfaces            |
| `ErrorHandler.cs`                     | Class to build error result on plugin failure                           |
| `plugin.json`                         | All meta-data for this plugin                                           |

### Important project values (*.csproj)

| Name            | Value                                             |
| --------------- | ------------------------------------------------- |
| TargetFramework | `net6.0-windows10.0.19041.0`                      |

### Project dependencies

#### Projects

* `Wox.Infrastructure`
* `Wox.Plugin`
* `PowerToys.PowerLauncher`


#### Build Dependency
Access to PluginManager was needed to make this plugin work. Because of this a reference to PowerToys.PowerLauncher was needed.
Since History Plugin needs a reference to PowerToys.PowerLauncher, it can not be set as a dependency reference in PowerToys.PowerLauncher project (else a circular reference would exist).
This means that if you build PowerToys.PowerLauncher only it will not build History Plugin. You will need to manually build History Plugin at least once and again manually if you change it.

### Caching
Right now, there is no caching. But since this plugin does cause more queries than expected to many plugins, the `BuildResult` method is likely to be improved with some level of caching.
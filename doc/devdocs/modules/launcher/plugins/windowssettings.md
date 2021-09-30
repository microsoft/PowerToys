# Windows Settings Plugin

The Windows settings Plugin allows users to search the Windows settings.

## Special functions (differ from the regular functions)

* Support modern Windows settings (Windows 10+)
* Support legacy Windows settings (Windows 7, 8.1)
* Support extra programs for setting (like ODBC)

* Support search by the are of the setting (like `Privacy`)
* Support search by the command line of the setting (like `workplace` found `ms-settings:workplace` )

## How to add a new Windows Setting or change one

All Windows settings are located in `WindowsSettings.json` in root folder of the project.
The `WindowsSettings.json` use a JSON schema file that make it easier to edit it.

A minimum entry for the `WindowsSettings.json` looks like:

```
  {
    "Name": "mySetting",
    "Areas": [ "AreaMySetting" ],
    "Type": "AppSettingsApp",
    "AltNames": [ "NiceSetting" ],
    "Command": "ms-settings:mySetting"
  }
```
Optional values for each entry are: `Note`, `IntroducedInBuild`, `DeprecatedInBuild`

### Remarks
* The strings under `Areas` must start with `Area`
* The string for `Type` must start with `App`
* The string for `Note` must start with `Note`
* The `Command` for modern Windows settings should start with `ms-settings:`
* The `Command` for legacy Windows settings should start with `control`
* The numeric value for `IntroducedInBuild` and  `DeprecatedInBuild` must be in range of `0` to `4294967295`
* The strings for `Areas`, `Type` and `Note` are used as ids for the resource file under `Properties\Resources.resx`
* When you add new strings make sure you have add add all translations for it.

## Important for developers

### General

* The assembly name is cached into `_assemblyName` (to avoid to many calls of `Assembly.GetExecutingAssembly()`)

## Microsoft.Plugin.Registry project

### Important plugin values (meta-data)

| Name            | Value                                                |
| --------------- | ---------------------------------------------------- |
| ActionKeyword   | `$`                                                  |
| ExecuteFileName | `Microsoft.PowerToys.Run.Plugin.WindowsSettings.dll` |
| ID              | `5043CECEE6A748679CBE02D27D83747A`                   |

### Interfaces used by this plugin

The plugin use only these interfaces (all inside the `Main.cs`):

* `Wox.Plugin.IPlugin`
* `Wox.Plugin.IContextMenu`
* `Wox.Plugin.IPluginI18n`

### Program files

| File                                  | Content                                                                  |
| ------------------------------------- | ------------------------------------------------------------------------ |
| `Classes\WindowsSetting.cs`           | `TODO`                                                                   |
| `Classes\WindowsSettings.cs`          | `TODO`                                                                   |
| `Helper\ContextMenuHelper.cs`         | All functions to build the context menu (for each result entry)          |
| `Helper\JsonSettingsListHelper.cs`    | All functions to `TODO`
| `Helper\ResultHelper.cs`              | All functions to convert internal results into WOX results               |
| `Helper\TranslationHelper.cs`         | All functions to translate the result in the surface language            |
| `Helper\UnsupportedSettingsHelper.cs` | All functions to filter not supported Windows settings out               |
| `Helper\WindowsSettingsPathHelper.cs` | All functions to `TODO`                                                  |
| `Images\WindowsSettings.dark.png`     | Symbol for the results for the dark theme                                |
| `Images\WindowsSettings.light.png`    | Symbol for the results for the light theme                               |
| `Properties\Resources.Designer.resx`  | File that contain all translatable keys                                  |
| `Properties\Resources.resx`           | File that contain all translatable strings in the neutral language       |
| `GlobalSuppressions.cs`               | Code suppressions (no real file, linked via *.csproj)                    |
| `Main.cs`                             | Main class, the only place that implement the WOX interfaces             |
| `plugin.json`                         | All meta-data for this plugin                                            |
| `StyleCop.json`                       | Code style (no real file, linked via *.csproj)                           |

### Important project values (*.csproj)

| Name            | Value                                                                                               |
| --------------- | --------------------------------------------------------------------------------------------------- |
| TargetFramework | `netcoreapp3.1` (means .NET Core 3.1)                                                               |
| Platforms       | `x64`                                                                                               |
| Output          | `..\..\..\..\..\x64\Debug\modules\launcher\Plugins\Microsoft.PowerToys.Run.Plugin.WindowsSettings\` |
| RootNamespace   | `Microsoft.PowerToys.Run.Plugin.WindowsSettings`                                                    |
| AssemblyName    | `Microsoft.PowerToys.Run.Plugin.WindowsSettings`                                                    |

### Project dependencies

#### Packages

| Package                                                                               | Version |
| ------------------------------------------------------------------------------------- | ------- |
| [`StyleCop.Analyzers`](https://github.com/DotNetAnalyzers/StyleCopAnalyzers)          | 1.1.118 |

#### Projects

* `Wox.Infrastructure`
* `Wox.Plugin`

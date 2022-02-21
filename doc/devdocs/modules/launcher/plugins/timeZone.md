# Time Zone Plugin

The Time Zone plugin allows users to search a time zone.

## Special functions (differ from the regular functions)

* Search for a country, like Kamchatka, Prince Edward Island, France
* Search for a shortcuts, like WEST, UTC, PST
* Search for a offset, like -12:00, -7, 5, 9:30
* Search for a military time zone name (must activate in plugin settings)

## How to add a new time zone  or change one

All time zones are located in `TimeZone.json` in root folder of the project.
The `TimeZone.json` use a JSON schema file that make it easier to edit it.

| Key                 | Optional | Value type        |
| ------------------- | -------- | ----------------- |
| `Offset`            | **No**   | String            |
| `Name`              | Yes      | String            |
| `MilitaryName`      | Yes      | String            |
| `Shortcut`          | Yes      | String            |
| `TimeNamesStandard` | Yes      | List with strings |
| `TimeNamesDaylight` | Yes      | List with strings |
| `ShortcutsStandard` | Yes      | List with strings |
| `ShortcutsDaylight` | Yes      | List with strings |
| `CountriesStandard` | Yes      | List with strings |
| `CountriesDaylight` | Yes      | List with strings |

A minimum entry for the `TimeZone.json` looks like:

```json
  {
    "Offset": "11:55",
    "Name": "My crazy time zone",
  }
```

A full entry for the `TimeZone.json` looks like:

```json
  {
    "Offset": "11:55",
    "Name": "My crazy time zone",
    "Shortcut" : "MYTZ",
    "MilitaryName" : "Order Time Zone",
    "TimeNamesStandard": [
        "My crazy standard time"
    ],
    "ShortcutsStandard": [
        "MCST"
    ],
    "TimeNamesDaylight": [
        "My crazy daylight time"
    ],
    "ShortcutsDaylight": [
        "MCDT"
    ],
    "CountriesStandard": [
      "Crazy Land East"
    ],
    "CountriesDaylight": [
      "Crazy Land West"
    ]
  }
```

### Remarks

* At minimum one of the optional value should be filled.

## Scores

* Scores are not used

## Important for developers

### General

* The assembly name is cached into `_assemblyName` (to avoid to many calls of `Assembly.GetExecutingAssembly()`)

## Microsoft.PowerToys.Run.Plugin.TimeZone project

### Important plugin values (meta-data)

| Name            | Value                                                |
| --------------- | ---------------------------------------------------- |
| ActionKeyword   | `&`                                                  |
| ExecuteFileName | `Microsoft.PowerToys.Run.Plugin.TimeZone.dll`        |
| ID              | `BADD1B06EF0A4B61AD95395F24241D69`                   |

### Interfaces used by this plugin

The plugin use only these interfaces (all inside the `Main.cs`):

* `Wox.Plugin.IPlugin`
* `Wox.Plugin.IContextMenu`
* `Wox.Plugin.IPluginI18n`
* `Wox.Plugin.ISettingProvider`
* `IDisposable`

### Program files

| File                                   | Content                                                                 |
| -------------------------------------- | ----------------------------------------------------------------------- |
| `Classes\TimeZoneProperties.cs`        | A class that represent one time zone                                    |
| `Classes\TimeZones.cs`                 | A wrapper class that only contains a list with time zones  (see 1)      |
| `Classes\TimeZoneSettings.cs`          | A class that contains all settings for the Time Zone plugin             |
| `Extensions\StringBuilderExtension.cs` | Extension methods for `StringBuilder` Objects                           |
| `Helper\ContextMenuHelper.cs`          | All functions to build the context menu (for each result entry)         |
| `Helper\JsonHelper.cs`                 | All functions to load the time zones from a JSON file                   |
| `Helper\ResultHelper.cs`               | All functions to convert internal results into WOX results              |
| `Helper\TranslationHelper.cs`          | All functions to translate the result in the surface language           |
| `Images\timeZone.dark.png`             | Symbol for the results for the dark theme                               |
| `Images\timeZone.light.png`            | Symbol for the results for the light theme                              |
| `Properties\Resources.Designer.resx`   | File that contain all translatable keys                                 |
| `Properties\Resources.resx`            | File that contain all translatable strings in the neutral language      |
| `GlobalSuppressions.cs`                | Code suppressions (no real file, linked via *.csproj)                   |
| `Main.cs`                              | Main class, the only place that implement the WOX interfaces            |
| `plugin.json`                          | All meta-data for this plugin                                           |
| `StyleCop.json`                        | Code style (no real file, linked via *.csproj)                          |
| `timezones.json`                       | File that contains all time zone information                            |
| `timeZones.schema.json`                | JSON schema for `timezones.json`                                        |
| `StyleCop.json`                        | Code style (no real file, linked via *.csproj)                          |

1. We need this extra wrapper class to make it possible that the JSON file can have and use a JSON schema file.
Because the JSON file must have a object as root type, instead of a array.

### Important project values (*.csproj)

| Name            | Value                                                         |
| --------------- | ------------------------------------------------------------- |
| TargetFramework | `net6.0-windows`                                              |
| Platforms       | `x64`                                                         |
| Output          | `..\..\..\..\..\x64\Debug\modules\launcher\Plugins\TimeZone\` |
| RootNamespace   | `Microsoft.PowerToys.Run.Plugin.TimeZone`                     |
| AssemblyName    | `Microsoft.PowerToys.Run.Plugin.TimeZone`                     |

### Project dependencies

#### Packages

| Package                                                                               | Version |
| ------------------------------------------------------------------------------------- | ------- |
| [`StyleCop.Analyzers`](https://github.com/DotNetAnalyzers/StyleCopAnalyzers)          | 1.1.118 |

#### Projects

* `Wox.Infrastructure`
* `Wox.Plugin`

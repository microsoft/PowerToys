# Registry Plugin

The registry plugin allows users to search the Windows registry.

## Special functions (differ from the regular functions)

* Support full base keys and short base keys (e.g. `HKLM` for `HKEY_LOCALE_MACHINE`).
* Show count of subkeys and count of values in the second result line.
* Search for value names and value data inside a registry key (syntax: `[RegistryKey]\\[ValueName]` and `[RegistryKey]\\[ValueData]`)

## The Windows Registry

The registry contains all settings for the Windows operating system and many settings of the installed (Windows only) programs.

*Note: Linux and macOS program ports typical store the settings in their own configuration files and not in the Windows registry.*

For more information about the Windows registry, see [the official documentation](https://learn.microsoft.com/windows/win32/sysinfo/registry).

For advanced information about the Windows registry, see [Windows registry information for advanced users](https://learn.microsoft.com/troubleshoot/windows-server/performance/windows-registry-advanced-users).

## Score

The score is currently not set on the results.

## Important for developers

### General

* The assembly name is cached into `_assemblyName` (to avoid to many calls of `Assembly.GetExecutingAssembly()`)

### Results

* All results override the visible search result via `QueryTextDisplay` to avoid problems with short registry base keys (e.g. `HKLM`).
* The length of a `Title` and `Subtitle` is automatic truncated, when it is to long.

## Microsoft.Plugin.Registry project

### Important plugin values (meta-data)

| Name            | Value                                         |
| --------------- | --------------------------------------------- |
| ActionKeyword   | `:`                                           |
| ExecuteFileName | `Microsoft.PowerToys.Run.Plugin.Registry.dll` |
| ID              | `303417D927BF4C97BCFFC78A123BE0C8`            |

### Interfaces used by this plugin

The plugin use only these interfaces (all inside the `Main.cs`):

* `Wox.Plugin.IPlugin`
* `Wox.Plugin.IContextMenu`
* `Wox.Plugin.IPluginI18n`
* `System.IDisposable`

### Program files

| File                                 | Content                                                                  |
| ------------------------------------ | ------------------------------------------------------------------------ |
| `Classes\RegistryEntry.cs`           | Wrapper class for a registry key with a possible exception on access     |
| `Constants\KeyName.cs`               | Static used short registry key names (to avoid code and string doubling) |
| `Constants\MaxTextLength.cs`         | Contain all maximum text lengths (for truncating)                        |
| `Enumeration\TruncateSide.cs`        | Contain the possible truncate sides                                      |
| `Helper\ContextMenuHelper.cs`        | All functions to build the context menu (for each result entry)          |
| `Helper\QueryHelper.cs`              | All functions to analyze the search query                                |
| `Helper\RegistryHelper.cs`           | All functions to search into the Windows registry (via `Win32.Registry`) |
| `Helper\ResultHelper.cs`             | All functions to convert internal results into WOX results               |
| `Helper\ValueHelper.cs`              | All functions to convert values into human readable values               |
| `Images\reg.dark.png`                | Symbol for the results for the dark theme                                |
| `Images\reg.light.png`               | Symbol for the results for the light theme                               |
| `Properties\Resources.Designer.resx` | File that contain all translatable keys                                  |
| `Properties\Resources.resx`          | File that contain all translatable strings in the neutral language       |
| `Main.cs`                            | Main class, the only place that implement the WOX interfaces             |
| `plugin.json`                        | All meta-data for this plugin                                            |

### Important project values (*.csproj)

| Name            | Value                                                                          |
| --------------- | ------------------------------------------------------------------------------ |
| TargetFramework | `net6.0-windows` (.NET 5) or `net6.0-windows10.0.19041.0` (OS version specific)|
| LangVersion     | `8.0` (mean C# 8.0)                                                            |
| Platforms       | `x64`                                                                          |
| Nullable        | `true`                                                                         |
| Output          | `..\..\..\..\..\x64\Debug\modules\launcher\Plugins\Microsoft.Plugin.Registry\` |
| RootNamespace   | `Microsoft.PowerToys.Run.Plugin.Registry`                                      |
| AssemblyName    | `Microsoft.PowerToys.Run.Plugin.Registry`                                      |

### Project dependencies

#### Projects

* `Wox.Infrastructure`
* `Wox.Plugin`

# New plugin checklist

- [ ] The plugin is a project under `modules\launcher\Plugins`
- [ ] Microsoft plugin project name pattern: `Microsoft.PowerToys.Run.Plugin.{PluginName}`
- [ ] Community plugin project name pattern: `Community.PowerToys.Run.Plugin.{PluginName}`
- [ ] The plugin target framework should be `net9.0-windows10.0.22621.0`
- [ ] If the plugin uses any 3rd party dependencies the project file should import `DynamicPlugin.props`
- [ ] 3rd party dependencies must be compatible with .NET 9
- [ ] The plugin has to contain a `plugin.json` file of the following format in its root folder:

```json
{
  "ID": string, // GUID string
  "ActionKeyword": string, // Direct activation phrase
  "IsGlobal": boolean,
  "Name": string, // Has to be unique, same as 'PluginName' in the project name pattern  
  "Author": string,
  "Version": "1.0.0", // For future compatibility
  "Language": "csharp", // So far we support only csharp 
  "Website": "https://aka.ms/powertoys", // Has to be an absolute uri starting with "http://" or "https://".
  "ExecuteFileName": string, // Should be {Type}.PowerToys.Run.Plugin.{PluginName}.dll
  "IcoPathDark": string, // Path to dark theme icon. The path is relative to the root plugin folder 
  "IcoPathLight": string // Path to light theme icon. The path is relative to the root plugin folder
  "DynamicLoading": bool // Sets whether the plugin should dynamically load any dependencies isolated from the core application.  
}
```

- [ ] Make sure your `Main` class contains a public, static string property for the `PluginID`. The plugin id has to be the same as the one in the `plugin.json`file.

```csharp
public static string PluginID => "xxxxxxx"; // The part xxxxxxx stands for the plugin ID.
```

- [ ] Do not use plugin name or PowerToys as prefixes for entities inside of the plugin project
- [ ] The plugin has to have Unit tests. Use MSTest framework
- [ ] Plugin's output code and assets have to be included in the installer [`Product.wxs`](/installer/PowerToysSetup/Product.wxs)
- [ ] Test the plugin with a local build. Build the installer, install, check that the plugin works as expected
- [ ] All plugin's binaries have to be included in the signed build [`pipeline.user.windows.yml`](/.pipelines/pipeline.user.windows.yml)

Some localization steps can only be done after the first pass by the localization team to provide the localized resources.
In the PR that adds a new plugin, reference a new issue to track the work for fully enabling localization for the new plugin.

- [ ] Add the resource folder to https://github.com/microsoft/PowerToys/blob/21247c0bb09a1bee3d14d6efa53d0c247f7236af/installer/PowerToysSetup/Product.wxs#L825
- [ ] Add the resource files under the section https://github.com/microsoft/PowerToys/blob/21247c0bb09a1bee3d14d6efa53d0c247f7236af/installer/PowerToysSetup/Product.wxs#L882
- [ ] Your plugin's executable file (DLL) has to have correct version informations after building it. (This version information will be shown on the settings page.)

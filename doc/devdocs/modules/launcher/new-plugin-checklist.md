# New plugin checklist
- [ ] The plugin is a project under `modules\launcher\Plugins`
- [ ] Microsoft plugin project name pattern: `Microsoft.PowerToys.Run.Plugin.{PluginName}`
- [ ] Community plugin project name pattern: `Community.PowerToys.Run.Plugin.{PluginName}`
- [ ] The project file should import `Version.props` and specify `<Version>$(Version).0</Version>`
- [ ] Make sure `*.csproj` specify only x64 platform target
- [ ] The plugin has to contain a `plugin.json` file of the following format in its root folder
```
{
  "ID": string, // GUID string
  "ActionKeyword": string, // Direct activation phrase
  "IsGlobal": boolean,
  "Name": string, // Has to be unique, same as 'PluginName' in the project name pattern  
  "Author": string,
  "Version": "1.0.0", // For future compatibility
  "Language": "csharp", // So far we support only csharp 
  "Website": "https://aka.ms/powertoys",
  "ExecuteFileName": string, // Should be {Type}.PowerToys.Run.Plugin.{PluginName}.dll
  "IcoPathDark": string, // Path to dark theme icon. The path is relative to the root plugin folder 
  "IcoPathLight": string // Path to light theme icon. The path is relative to the root plugin folder 
}
```
- [ ] Do not use plugin name or PowerToys as prefixes for entities inside of the plugin project
- [ ] The plugin has to have Unit tests. Use MSTest framework
- [ ] Plugin's output code and assets have to be included in the installer [`Product.wxs`](/installer/PowerToysSetup/Product.wxs)
- [ ] Test the plugin with a local build. Build the installer, install, check that the plugin works as expected
- [ ] All plugin's binaries have to be included in the signed build [`pipeline.user.windows.yml`](/.pipelines/pipeline.user.windows.yml)
- [ ] The plugin target framework has to be .NET Core 3.1. All dependencies have to have .NET 5 version

Some localization steps can only be done after the first pass by the localization team to provide the localized resources.
In the PR that adds a new plugin, reference a new issue to track the work for fully enabling localization for the new plugin.

 - [ ] Add the resource folder to https://github.com/microsoft/PowerToys/blob/21247c0bb09a1bee3d14d6efa53d0c247f7236af/installer/PowerToysSetup/Product.wxs#L825
 - [ ] Add the resource files under the section https://github.com/microsoft/PowerToys/blob/21247c0bb09a1bee3d14d6efa53d0c247f7236af/installer/PowerToysSetup/Product.wxs#L882
 

# New plugin checklist
- A plugin is a project under `modules\launcher\Plugins`
- Microsoft plugin project name pattern: `Microsoft.PowerToys.Run.Plugin.{PluginName}`
- Community plugin project name pattern: `Community.PowerToys.Run.Plugin.{PluginName}`
- [`GlobalSuppressions.cs`](/src/codeAnalysis/GlobalSuppressions.cs) and [`StyleCop.json`](/src/codeAnalysis/StyleCop.json) have to be included in the plugin project so it follows PowerToys code guidelines
- A plugin has to have `{PowerToys version}.0` version
- Make sure `*.csproj` specify only x64 platform target
- A plugin has to contain a `plugin.json` file of the following format in its root folder
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
- Do not use plugin name or PowerToys as prefixes for entities inside of the plugin project
- A plugin has to have Unit tests. Use MSTest framework
- To enable localization add `LocProject.json` file to the plugin root folder. For details see [`localization.md`](/doc/devdocs/localization.md#enabling-localization-on-a-new-project)
- Plugin's output code and assets have to be included in the installer [`Product.wxs`](/installer/PowerToysSetup/Product.wxs)
- Test a plugin with a local build. Build the installer, install, check that the plugin works as expected
- All plugin's binaries have to be included in the signed build [`pipeline.user.windows.yml`](/.pipelines/pipeline.user.windows.yml)
- A plugin target framework has to be .NET Core 3.1

# New plugin checklist
- A plugin is a project under `modules\launcher\Plugins`
- Microsoft plugin project name pattern: Microsoft.PowerToys.Run.Plugin.{PluginName}
- Comunity plugin project name pattern: `Community.PowerToys.Run.Plugin.{PluginName}`
- [`GlobalSuppressions.cs`](/src/codeAnalysis/GlobalSuppressions.cs) and [`StyleCop.json`](/src/codeAnalysis/StyleCop.json) have to be included in the plugin project so it follows PowerToys code guidelines
- A plugin has to have `{PowerToys version}.0` version
- A plugin has to have only x64 platform target
- A plugin has to contain `plugin.json` file of the following format in its root folder
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
  "IcoPathDark": string, // Path to dark theme icon. Path is relative to root plugin folder 
  "IcoPathLight": string // Path to light theme icon. Path is relative to root plugin folder 
}
```
- Do not use plugin name or PowerToys as prefixes for entities inside of the plugin project
- A plugin has to have Unit tests. Use MSTest framework
- To enable localization add `LocProject.json` file to the plugin root folder. For details see [`localization.md`](/doc/devdocs/localization.md#enabling-localization-on-a-new-project)
- Plugin's output code and assets have to be included into the installer [`Product.wxs`](\installer\PowerToysSetup\Prodcut.wsx)
- Test a plugin with a local build. Build the installer, install, check that the plugin works as expected
- All plugin's binaries have to be included into signed build `pipeline.user.windows.yml`
- All plugin's extrenal dependencies has to be ARM compatible. It usually means they have to be .NET 5 compiled.

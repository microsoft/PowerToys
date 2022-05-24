# how-to-make-a-community-plugin

- [ ] Community plugin project name pattern: `Community.PowerToys.Run.Plugin.{PluginName}`
- [ ] Make sure `*.csproj` specify only x64 platform target
- [ ] The plugin has to contain a `plugin.json` file of the following format in its root folder
```json
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
- [ ] The plugin target framework has to be net6.0-windows.
```xml
<PropertyGroup>
    <OutputType>Library</OutputType>
    <TargetFramework>net6.0-windows</TargetFramework>
    <Platforms>x64</Platforms>
    ....
</PropertyGroup>
```
```

- [ ] Reference the sdk from installed PowerToys's directory.
```xml
  <ItemGroup>
    <Reference Include="PowerToys.Common.UI">
      <HintPath>..\..\..\..\..\..\Program Files\PowerToys\modules\launcher\PowerToys.Common.UI.dll</HintPath>
    </Reference>
    <Reference Include="PowerToys.ManagedCommon">
      <HintPath>..\..\..\..\..\..\Program Files\PowerToys\modules\launcher\PowerToys.ManagedCommon.dll</HintPath>
    </Reference>
    <Reference Include="PowerToys.Settings.UI.Lib">
      <HintPath>..\..\..\..\..\..\Program Files\PowerToys\modules\launcher\PowerToys.Settings.UI.Lib.dll</HintPath>
    </Reference>
    <Reference Include="Wox.Infrastructure">
      <HintPath>..\..\..\..\..\..\Program Files\PowerToys\modules\launcher\Wox.Infrastructure.dll</HintPath>
    </Reference>
    <Reference Include="Wox.Plugin">
      <HintPath>..\..\..\..\..\..\Program Files\PowerToys\modules\launcher\Wox.Plugin.dll</HintPath>
    </Reference>
  </ItemGroup>
```

- [ ] Then you can add other package by `Nuget`
- [ ] you  should add <EnableDynamicLoading>true</EnableDynamicLoading> to the project properties so that they copy all of their dependencies to the output of `dotnet build`, or you can use `dotnet publish` will also copy all of its dependencies to the publish output. otherwise you will lose the dependencies dll.

- [ ] Copy all output to `<PowerToys>\modules\launcher\Plugins` **except the sdk dll you Reference above** , cause it loaded by PowerToys already.

- [ ] Restart the PowerToys.
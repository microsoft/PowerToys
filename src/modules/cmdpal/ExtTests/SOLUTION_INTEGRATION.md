# PowerToys.sln Solution File Updates

## Summary
Added the new unit test projects for the migrated cmdpal extensions to the PowerToys.sln solution file under the "Built-in Extension Tests" folder.

## Changes Made

### 1. Added Project Definitions
Added the following new project entries after the existing Microsoft.CmdPal.Ext.TimeDate.UnitTests project:

```xml
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Microsoft.CmdPal.Ext.Registry.UnitTests", "src\modules\cmdpal\ExtTests\Microsoft.CmdPal.Ext.Registry.UnitTests\Microsoft.CmdPal.Ext.Registry.UnitTests.csproj", "{A1B2C3D4-E5F6-7890-ABCD-56789ABCDEF0}"
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Microsoft.CmdPal.Ext.Calc.UnitTests", "src\modules\cmdpal\ExtTests\Microsoft.CmdPal.Ext.Calc.UnitTests\Microsoft.CmdPal.Ext.Calc.UnitTests.csproj", "{B2C3D4E5-F6A7-8901-BCDE-6789ABCDEF01}"
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Microsoft.CmdPal.Ext.WindowWalker.UnitTests", "src\modules\cmdpal\ExtTests\Microsoft.CmdPal.Ext.WindowWalker.UnitTests\Microsoft.CmdPal.Ext.WindowWalker.UnitTests.csproj", "{C3D4E5F6-A7B8-9012-CDEF-789ABCDEF012}"
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Microsoft.CmdPal.Ext.System.UnitTests", "src\modules\cmdpal\ExtTests\Microsoft.CmdPal.Ext.System.UnitTests\Microsoft.CmdPal.Ext.System.UnitTests.csproj", "{D4E5F6A7-B8C9-0123-DEF0-89ABCDEF0123}"
```

### 2. Added Build Configurations
Added Debug and Release configurations for both ARM64 and x64 platforms for each new project:

For each project GUID, added:
- Debug|ARM64 (ActiveCfg and Build.0)
- Debug|x64 (ActiveCfg and Build.0)
- Release|ARM64 (ActiveCfg and Build.0)
- Release|x64 (ActiveCfg and Build.0)

### 3. Added Project Nesting
Added the new projects to the "Built-in Extension Tests" folder by adding entries in the GlobalSection(NestedProjects):

```
{A1B2C3D4-E5F6-7890-ABCD-56789ABCDEF0} = {7872E99B-71E6-4AE9-976A-922F0A6B3113}
{B2C3D4E5-F6A7-8901-BCDE-6789ABCDEF01} = {7872E99B-71E6-4AE9-976A-922F0A6B3113}
{C3D4E5F6-A7B8-9012-CDEF-789ABCDEF012} = {7872E99B-71E6-4AE9-976A-922F0A6B3113}
{D4E5F6A7-B8C9-0123-DEF0-89ABCDEF0123} = {7872E99B-71E6-4AE9-976A-922F0A6B3113}
```

Where `{7872E99B-71E6-4AE9-976A-922F0A6B3113}` is the GUID of the "Built-in Extension Tests" folder.

## Project GUIDs
Generated unique GUIDs for each new project:
- **Registry.UnitTests**: `{A1B2C3D4-E5F6-7890-ABCD-56789ABCDEF0}`
- **Calc.UnitTests**: `{B2C3D4E5-F6A7-8901-BCDE-6789ABCDEF01}`
- **WindowWalker.UnitTests**: `{C3D4E5F6-A7B8-9012-CDEF-789ABCDEF012}`
- **System.UnitTests**: `{D4E5F6A7-B8C9-0123-DEF0-89ABCDEF0123}`

## Visual Studio Integration
These changes will ensure that:
1. The test projects appear in the Visual Studio Solution Explorer under "Built-in Extension Tests"
2. The projects can be built in both Debug and Release configurations
3. The projects support both ARM64 and x64 architectures
4. The test projects are properly integrated with the PowerToys build system

## Files Affected
- `PowerToys.sln` - Main solution file with project definitions, build configurations, and folder structure

## Next Steps
After these changes, the test projects should be visible in Visual Studio and can be built and run using the standard MSBuild tools and Visual Studio test runner.

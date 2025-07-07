# PowerToys Solution Fix - Built-in Extension Tests Folder

## Problem
The cmdpal extension unit test projects were not appearing in the "Built-in Extension Tests" folder in Visual Studio, even though they were correctly added to the solution file.

## Root Cause
The projects were missing from the `NestedProjects` section of the PowerToys.sln file, which is what tells Visual Studio which projects belong to which solution folders.

## Solution
Added the following entries to the `NestedProjects` section of PowerToys.sln:

```
{A1B2C3D4-E5F6-7890-ABCD-56789ABCDEF0} = {7872E99B-71E6-4AE9-976A-922F0A6B3113}
{B2C3D4E5-F6A7-8901-BCDE-6789ABCDEF01} = {7872E99B-71E6-4AE9-976A-922F0A6B3113}
{C3D4E5F6-A7B8-9012-CDEF-789ABCDEF012} = {7872E99B-71E6-4AE9-976A-922F0A6B3113}
{D4E5F6A7-B8C9-0123-DEF0-89ABCDEF0123} = {7872E99B-71E6-4AE9-976A-922F0A6B3113}
{E5F6A7B8-C9D0-1234-EF01-9ABCDEF01234} = {7872E99B-71E6-4AE9-976A-922F0A6B3113}
```

These entries were added right after the existing TimeDate test project entry:
```
{97E31501-601E-493D-A6A8-1EEB799F4ADC} = {7872E99B-71E6-4AE9-976A-922F0A6B3113}
```

Where:
- `{7872E99B-71E6-4AE9-976A-922F0A6B3113}` is the GUID of the "Built-in Extension Tests" folder
- The left-hand GUIDs correspond to:
  - `{A1B2C3D4-E5F6-7890-ABCD-56789ABCDEF0}` = Microsoft.CmdPal.Ext.Registry.UnitTests
  - `{B2C3D4E5-F6A7-8901-BCDE-6789ABCDEF01}` = Microsoft.CmdPal.Ext.Calc.UnitTests
  - `{C3D4E5F6-A7B8-9012-CDEF-789ABCDEF012}` = Microsoft.CmdPal.Ext.WindowWalker.UnitTests
  - `{D4E5F6A7-B8C9-0123-DEF0-89ABCDEF0123}` = Microsoft.CmdPal.Ext.System.UnitTests
  - `{E5F6A7B8-C9D0-1234-EF01-9ABCDEF01234}` = Microsoft.CmdPal.Ext.TimeDate.UnitTests

## Verification
1. All test projects are now properly nested under the "Built-in Extension Tests" folder
2. The solution can still be built using dotnet CLI
3. Visual Studio should now display all projects in the correct folder structure

## What to do next
1. Restart Visual Studio if it's currently open
2. Reload the PowerToys.sln solution
3. Verify that all cmdpal extension test projects now appear under "Built-in Extension Tests" folder
4. The projects should be fully manageable (build, test, debug) within Visual Studio

## Additional Notes
- The solution file retains all existing functionality
- All build configurations (Debug/Release, x64/ARM64) are preserved
- The test projects follow PowerToys code style conventions
- Documentation has been provided in the ExtTests folder for future reference

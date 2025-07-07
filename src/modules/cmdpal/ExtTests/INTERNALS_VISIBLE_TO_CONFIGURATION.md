# InternalsVisibleTo Configuration for CmdPal Extension Unit Tests

## Problem
Unit test projects for cmdpal extensions were unable to access internal types and members in the extension assemblies, causing compilation errors when trying to test internal classes like `ResultHelper`, `BracketHelper`, `ErrorHandler`, etc.

## Solution
Added `InternalsVisibleTo` attributes to each extension project's `.csproj` file to allow their corresponding unit test projects to access internal types.

## Changes Made

### 1. Microsoft.CmdPal.Ext.Registry
**File**: `src\modules\cmdpal\ext\Microsoft.CmdPal.Ext.Registry\Microsoft.CmdPal.Ext.Registry.csproj`

Added:
```xml
<ItemGroup>
  <AssemblyAttribute Include="System.Runtime.CompilerServices.InternalsVisibleTo">
    <_Parameter1>Microsoft.CmdPal.Ext.Registry.UnitTests</_Parameter1>
  </AssemblyAttribute>
</ItemGroup>
```

This allows the test project to access internal types like:
- `Microsoft.CmdPal.Ext.Registry.Helpers.ResultHelper`
- Other internal helper classes

### 2. Microsoft.CmdPal.Ext.Calc
**File**: `src\modules\cmdpal\ext\Microsoft.CmdPal.Ext.Calc\Microsoft.CmdPal.Ext.Calc.csproj`

Added:
```xml
<ItemGroup>
  <AssemblyAttribute Include="System.Runtime.CompilerServices.InternalsVisibleTo">
    <_Parameter1>Microsoft.CmdPal.Ext.Calc.UnitTests</_Parameter1>
  </AssemblyAttribute>
</ItemGroup>
```

This allows the test project to access internal types like:
- `Microsoft.CmdPal.Ext.Calc.Helper.BracketHelper`
- `Microsoft.CmdPal.Ext.Calc.Helper.ErrorHandler`
- Other internal helper classes

### 3. Microsoft.CmdPal.Ext.WindowWalker
**File**: `src\modules\cmdpal\ext\Microsoft.CmdPal.Ext.WindowWalker\Microsoft.CmdPal.Ext.WindowWalker.csproj`

Added:
```xml
<ItemGroup>
  <AssemblyAttribute Include="System.Runtime.CompilerServices.InternalsVisibleTo">
    <_Parameter1>Microsoft.CmdPal.Ext.WindowWalker.UnitTests</_Parameter1>
  </AssemblyAttribute>
</ItemGroup>
```

### 4. Microsoft.CmdPal.Ext.System
**File**: `src\modules\cmdpal\ext\Microsoft.CmdPal.Ext.System\Microsoft.CmdPal.Ext.System.csproj`

Added:
```xml
<ItemGroup>
  <AssemblyAttribute Include="System.Runtime.CompilerServices.InternalsVisibleTo">
    <_Parameter1>Microsoft.CmdPal.Ext.System.UnitTests</_Parameter1>
  </AssemblyAttribute>
</ItemGroup>
```

### 5. Microsoft.CmdPal.Ext.TimeDate
**File**: `src\modules\cmdpal\ext\Microsoft.CmdPal.Ext.TimeDate\Microsoft.CmdPal.Ext.TimeDate.csproj`

Added:
```xml
<ItemGroup>
  <AssemblyAttribute Include="System.Runtime.CompilerServices.InternalsVisibleTo">
    <_Parameter1>Microsoft.CmdPal.Ext.TimeDate.UnitTests</_Parameter1>
  </AssemblyAttribute>
</ItemGroup>
```

## How It Works

The `InternalsVisibleTo` attribute is a .NET feature that allows an assembly to specify which other assemblies can access its internal types and members. By adding this attribute to each extension project, we grant the corresponding unit test projects access to internal classes, methods, and properties that need to be tested.

## Benefits

1. **Comprehensive Testing**: Unit tests can now access and test internal implementation details
2. **Maintains Encapsulation**: Internal types remain internal to external consumers while being testable
3. **Follows Best Practices**: This is the standard .NET approach for testing internal members
4. **Future-Proof**: Any new internal types added to the extensions will automatically be accessible to their tests

## Verification

After applying these changes:
1. Unit test projects can compile successfully
2. Tests can access internal helper classes and methods
3. The extension assemblies maintain their internal encapsulation for external consumers
4. The solution builds correctly in both Visual Studio and via dotnet CLI

## Security Considerations

The `InternalsVisibleTo` attribute only grants access to the specific test assemblies named. This maintains security while enabling comprehensive testing of internal implementation details.

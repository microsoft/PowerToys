# WorkspacesLauncherUI Unit Tests

Pre-migration baseline tests for the Workspaces Launcher UI. These validate that the data layer, ViewModel, and display logic remain functionally identical after the WPF → WinUI 3 migration.

## Prerequisites

- Visual Studio 2022 17.4+ or Visual Studio 2026
- .NET SDK (see `global.json` in repo root)
- Submodules initialized: `git submodule update --init --recursive`

## Build

From this directory:

```powershell
# Quick build (auto-detects platform)
& "$env:RepoRoot\tools\build\build.cmd"

# Or with explicit options
& "$env:RepoRoot\tools\build\build.cmd" -Platform arm64 -Configuration Debug
```

If you get NuGet restore errors on first build:

```powershell
& "$env:RepoRoot\tools\build\build-essentials.cmd"
```

## Run Tests

### Option 1: dotnet test (recommended for CI)

```powershell
dotnet test "<output-dir>\tests\WorkspacesLauncherUI.Tests\PowerToys.WorkspacesLauncherUI.Tests.dll" --verbosity normal
```

The output directory depends on your platform/config. For arm64 Debug:

```powershell
dotnet test "arm64\Debug\tests\WorkspacesLauncherUI.Tests\PowerToys.WorkspacesLauncherUI.Tests.dll" --verbosity normal
```

### Option 2: Visual Studio Test Explorer

1. Open `PowerToys.slnx` in Visual Studio
2. Build the `WorkspacesLauncherUI.UnitTests` project
3. Open Test Explorer (`Ctrl+E, T`)
4. Run all tests in `PowerToys.WorkspacesLauncherUI.Tests`

### Option 3: Filter by category

```powershell
dotnet test <dll-path> --filter "TestCategory=Scenario"
dotnet test <dll-path> --filter "TestCategory=Deserialization"
dotnet test <dll-path> --filter "TestCategory=ViewModel"
dotnet test <dll-path> --filter "TestCategory=Model"
dotnet test <dll-path> --filter "TestCategory=Serialization"
dotnet test <dll-path> --filter "TestCategory=DataModel"
dotnet test <dll-path> --filter "TestCategory=Converter"
```

### Generate TRX Report

```powershell
dotnet test <dll-path> --logger "trx;LogFileName=TestResults.trx"
```

Report saved to `TestResults/TestResults.trx`.

## Test Categories

| Category | File | What It Validates |
|----------|------|-------------------|
| `Deserialization` | `IpcMessageDeserializationTests.cs` | C++ launcher engine JSON → C# data models |
| `ViewModel` | `LauncherViewModelStateManagementTests.cs` | IPC callback → ObservableCollection pipeline |
| `Model` | `LaunchStatusDisplayLogicTests.cs` | Spinner/glyph/color for each launch state |
| `Scenario` | `UserWorkflowIntegrationTests.cs` | Full user workflows (launch, cancel, fail) |
| `Serialization` | `IpcJsonPropertyNamingTests.cs` | JSON key names match C++ IPC protocol |
| `DataModel` | `WindowPositionDataTests.cs` | Window coordinates and equality |
| `DataModel` | `ApplicationDataModelTests.cs` | All application fields |
| `DataModel` | `LaunchStateEnumContractTests.cs` | Enum integers match `LaunchingStateEnum.h` |
| `Converter` | `StatusIndicatorVisibilityTests.cs` | Loading → Visibility toggle |

## When to Run

- **Before migration changes**: Establish baseline (all 129 should pass)
- **After each migration PR**: Verify no regressions
- **After IPC contract changes**: Deserialization + Serialization categories
- **After UI state changes**: Model + ViewModel categories

## Adding New Tests

Follow the naming convention: `{WhatIsUnderTest}_{GivenCondition}_{ExpectedBehavior}`

Example:
```csharp
[TestMethod]
[TestCategory("ViewModel")]
public void ReceiveIpcMessage_NewFieldAdded_DeserializesWithoutBreakingExistingFields()
```

## Note on WPF Dependencies

Some tests (e.g., `LaunchStatusDisplayLogicTests`) reference `System.Windows.Media.SolidColorBrush`. After the WinUI migration:
- Update these to use `Microsoft.UI` equivalents
- The test *logic* (color values, glyph strings) should remain identical
- Only the brush/color type assertions need updating

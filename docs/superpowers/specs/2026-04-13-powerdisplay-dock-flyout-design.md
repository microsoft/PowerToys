# PowerDisplay Dock Mode Flyout via CLI Position Arguments

## Summary

Revert the `IControlsPage` CmdPal extension (commit `9a600dea16`) and instead implement a simpler approach: clicking a PowerDisplay dock band item shows the existing PowerDisplay `MainWindow` at the clicked position using CLI arguments.

## Motivation

The `IControlsPage` approach added significant complexity to the CmdPal extension SDK (3 new IDL interfaces, ViewModels, XAML views) to render controls inside CmdPal's palette window. A simpler approach is to reuse PowerDisplay's existing UI and position it near the dock band item.

## Scope

### In Scope

1. Revert commit `9a600dea16` (IControlsPage page type)
2. Add `--show-at X Y` CLI argument support to PowerDisplay
3. Create a sample dock band command in SamplePagesExtension that launches PowerDisplay at the cursor position

### Out of Scope

- Creating a PowerDisplay CmdPal extension/CommandProvider (future work)
- Modifying the `IInvokableCommand` interface to pass position
- Named pipe or shared memory IPC for position passing

## Part 1: Revert IControlsPage

Full revert of commit `9a600dea16`. This removes:

- **IDL**: `IControlItem`, `IControlsSection`, `IControlsPage` interfaces from `Microsoft.CommandPalette.Extensions.idl`
- **Toolkit**: `ControlItem.cs`, `ControlsSection.cs`, `ControlsPage.cs`
- **ViewModels**: `ControlItemViewModel.cs`, `ControlsSectionViewModel.cs`, `ControlsPageViewModel.cs`
- **UI**: `ExtViews/ControlsPage.xaml`, `ExtViews/ControlsPage.xaml.cs`
- **Sample**: `SampleControlsPage.cs`, `SampleControlsDockBand.cs`
- **Wiring**: `CommandPalettePageViewModelFactory.cs` routing, `MainWindow.xaml.cs` preferred width handling, `SamplesListPage.cs` entry, `SamplePagesCommandsProvider.cs` dock band registration

## Part 2: PowerDisplay CLI Position Support

### 2.1 Program.cs Changes

Add parsing for `--show-at X Y` arguments alongside existing `runner_pid` and `pipe_name` args.

**New instance**: Pass position to `App` constructor, which stores it and applies it when `MainWindow` is created.

**Existing instance (redirect activation)**: The `OnActivated` handler receives launch args from the redirected process. Parse `--show-at X Y` from the activation args and call `MainWindow.ShowWindowAt(x, y)`.

Argument parsing:
```
PowerDisplay.exe [runner_pid] [pipe_name]              # existing mode
PowerDisplay.exe --show-at <x_pixels> <y_pixels>       # new CLI mode
```

The coordinates are in **absolute screen pixels** (not DIPs), because the caller (CmdPal extension) gets cursor position in screen pixels from Win32 `GetCursorPos`.

### 2.2 MainWindow.xaml.cs Changes

Add a new public method:

```csharp
public void ShowWindowAt(int screenX, int screenY)
```

This method follows the same structure as `ShowWindow()` but uses a different positioning strategy:
1. Measures content height (same as `AdjustWindowSizeToContent()`)
2. Calls `WindowHelper.PositionWindowNear()` instead of `PositionWindowBottomRight()`
3. Activates, shows, sets `IsAlwaysOnTop = true`, and brings to front

### 2.3 WindowHelper.cs Changes

Add a new positioning method:

```csharp
public static void PositionWindowNear(
    WindowEx window,
    int anchorScreenXPixels,
    int anchorScreenYPixels,
    int widthDip,
    int heightDip)
```

This method:
1. Finds the `DisplayArea` containing the anchor point
2. Gets DPI scale for that display
3. Converts width/height from DIP to physical pixels
4. Chooses anchor direction: positions the window so it appears adjacent to the anchor point, preferring to open **above** the anchor (since dock is typically at the bottom), but falls back to below if there's not enough space above
5. Clamps the window position to stay within the display's work area bounds

### 2.4 App.xaml.cs Changes

- Accept optional position in constructor or via a method
- On `OnLaunched`: if position was provided via CLI, call `MainWindow.ShowWindowAt()` after window creation instead of the normal startup flow
- In the existing Windows Event handler for `TogglePowerDisplayEvent`: no changes needed (this path is for hotkey toggle, not CLI invocation)

## Part 3: SampleExtension Dock Band Command

### 3.1 SampleShowPowerDisplayCommand.cs

A new `InvokableCommand` in SamplePagesExtension:

```csharp
internal sealed partial class SampleShowPowerDisplayCommand : InvokableCommand
{
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetCursorPos(out POINT lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    public override ICommandResult Invoke()
    {
        GetCursorPos(out var cursorPos);

        // Resolve PowerDisplay.exe path relative to this extension's assembly location,
        // since SamplePagesExtension ships alongside the PowerToys modules.
        var extensionDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        var exePath = Path.Combine(extensionDir!, "..", "PowerDisplay", "PowerDisplay.exe");

        if (!File.Exists(exePath))
        {
            return CommandResult.ShowToast("PowerDisplay.exe not found");
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = $"--show-at {cursorPos.X} {cursorPos.Y}",
            UseShellExecute = false,
        });
        return CommandResult.Dismiss();
    }
}
```

The path to `PowerDisplay.exe` is resolved relative to the extension's assembly location. Since SamplePagesExtension and PowerDisplay are both under the PowerToys modules directory, the relative path `../PowerDisplay/PowerDisplay.exe` should work. If a different layout is used, this path will need adjustment.

### 3.2 SamplePowerDisplayDockBand.cs

A new `WrappedDockItem`:

```csharp
internal sealed partial class SamplePowerDisplayDockBand : WrappedDockItem
{
    public SamplePowerDisplayDockBand()
        : base(new SampleShowPowerDisplayCommand(), "PowerDisplay")
    {
        Icon = new IconInfo("\uE7F4"); // Monitor icon
    }
}
```

### 3.3 Registration

Add to `SamplePagesCommandsProvider.GetDockBands()`:

```csharp
public override ICommandItem[] GetDockBands()
{
    List<ICommandItem> bands = new()
    {
        new SampleDockBand(),
        new SampleButtonsDockBand(),
        new SamplePowerDisplayDockBand(), // new
    };
    return bands.ToArray();
}
```

Note: `SampleControlsDockBand` is removed since `IControlsPage` is reverted.

## Data Flow

```
User clicks PowerDisplay dock band item
  -> DockControl.BandItem_Tapped()
    -> InvokeItem() detects IInvokableCommand (not a page)
      -> SampleShowPowerDisplayCommand.Invoke()
        -> GetCursorPos() returns screen pixel coordinates
        -> Process.Start("PowerDisplay.exe --show-at X Y")
          -> If no instance running:
             Program.Main() parses --show-at
             -> App stores position
             -> MainWindow created
             -> ShowWindowAt(X, Y) called
          -> If instance already running:
             Program.Main() redirects activation
             -> OnActivated() receives args
             -> Parses --show-at X Y
             -> MainWindow.ShowWindowAt(X, Y) on UI thread
```

## Files Changed

### Reverted (from commit 9a600dea16)
| File | Change |
|------|--------|
| `extensionsdk/.../Microsoft.CommandPalette.Extensions.idl` | Remove IControlItem, IControlsSection, IControlsPage |
| `extensionsdk/.../Toolkit/ControlItem.cs` | Delete |
| `extensionsdk/.../Toolkit/ControlsSection.cs` | Delete |
| `extensionsdk/.../Toolkit/ControlsPage.cs` | Delete |
| `Microsoft.CmdPal.UI.ViewModels/ControlItemViewModel.cs` | Delete |
| `Microsoft.CmdPal.UI.ViewModels/ControlsSectionViewModel.cs` | Delete |
| `Microsoft.CmdPal.UI.ViewModels/ControlsPageViewModel.cs` | Delete |
| `Microsoft.CmdPal.UI.ViewModels/CommandPalettePageViewModelFactory.cs` | Remove ControlsPage routing |
| `Microsoft.CmdPal.UI/ExtViews/ControlsPage.xaml` | Delete |
| `Microsoft.CmdPal.UI/ExtViews/ControlsPage.xaml.cs` | Delete |
| `Microsoft.CmdPal.UI/MainWindow.xaml.cs` | Remove PreferredWidth handling |
| `SamplePagesExtension/SampleControlsPage.cs` | Delete |
| `SamplePagesExtension/SampleControlsDockBand.cs` | Delete |
| `SamplePagesExtension/SamplesListPage.cs` | Remove controls page entry |
| `SamplePagesExtension/SamplePagesCommandsProvider.cs` | Remove SampleControlsDockBand |
| `Microsoft.CmdPal.UI/Pages/ShellPage.xaml.cs` | Revert changes |

### New/Modified for PowerDisplay CLI
| File | Change |
|------|--------|
| `PowerDisplay/Program.cs` | Add `--show-at X Y` argument parsing |
| `PowerDisplay/PowerDisplayXAML/App.xaml.cs` | Accept and propagate position, handle in OnActivated |
| `PowerDisplay/PowerDisplayXAML/MainWindow.xaml.cs` | Add `ShowWindowAt(x, y)` method |
| `PowerDisplay/Helpers/WindowHelper.cs` | Add `PositionWindowNear()` method |

### New for SampleExtension
| File | Change |
|------|--------|
| `SamplePagesExtension/SampleShowPowerDisplayCommand.cs` | New InvokableCommand |
| `SamplePagesExtension/SamplePowerDisplayDockBand.cs` | New WrappedDockItem |
| `SamplePagesExtension/SamplePagesCommandsProvider.cs` | Register new dock band |

## Error Handling

- If `PowerDisplay.exe` is not found: `Invoke()` catches the exception and returns `CommandResult.ShowToast("Failed to launch PowerDisplay")`
- If position is off-screen or invalid: `PositionWindowNear()` clamps to work area bounds
- If `--show-at` args are malformed: fall back to default `ShowWindow()` (bottom-right positioning)

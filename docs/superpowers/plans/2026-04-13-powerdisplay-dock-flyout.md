# PowerDisplay Dock Mode Flyout Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Revert the IControlsPage CmdPal extension and implement dock-mode PowerDisplay flyout via CLI position arguments and a sample dock band command.

**Architecture:** PowerDisplay.exe gains `--show-at X Y` CLI argument support. When a new process starts with these args, it writes position to a known temp file and signals a Windows Event. The already-running instance reads the file and shows at the given position. A sample `IInvokableCommand` in SamplePagesExtension gets cursor position and launches PowerDisplay.exe with the args.

**Tech Stack:** C#, WinUI 3, WinUIEx, CsWin32 (P/Invoke source gen), Win32 Events, AppInstance single-instance pattern

---

## File Structure

### Reverted Files (deleted)
- `src/modules/cmdpal/Microsoft.CmdPal.UI.ViewModels/ControlItemViewModel.cs`
- `src/modules/cmdpal/Microsoft.CmdPal.UI.ViewModels/ControlsPageViewModel.cs`
- `src/modules/cmdpal/Microsoft.CmdPal.UI.ViewModels/ControlsSectionViewModel.cs`
- `src/modules/cmdpal/Microsoft.CmdPal.UI/ExtViews/ControlsPage.xaml`
- `src/modules/cmdpal/Microsoft.CmdPal.UI/ExtViews/ControlsPage.xaml.cs`
- `src/modules/cmdpal/ext/SamplePagesExtension/SampleControlsPage.cs`
- `src/modules/cmdpal/ext/SamplePagesExtension/SampleControlsDockBand.cs`
- `src/modules/cmdpal/extensionsdk/Microsoft.CommandPalette.Extensions.Toolkit/ControlItem.cs`
- `src/modules/cmdpal/extensionsdk/Microsoft.CommandPalette.Extensions.Toolkit/ControlsPage.cs`
- `src/modules/cmdpal/extensionsdk/Microsoft.CommandPalette.Extensions.Toolkit/ControlsSection.cs`

### Reverted Files (modified, restore to main)
- `src/modules/cmdpal/extensionsdk/Microsoft.CommandPalette.Extensions/Microsoft.CommandPalette.Extensions.idl`
- `src/modules/cmdpal/Microsoft.CmdPal.UI.ViewModels/CommandPalettePageViewModelFactory.cs`
- `src/modules/cmdpal/Microsoft.CmdPal.UI.ViewModels/PageViewModel.cs`
- `src/modules/cmdpal/Microsoft.CmdPal.UI/MainWindow.xaml.cs`
- `src/modules/cmdpal/Microsoft.CmdPal.UI/Pages/ShellPage.xaml.cs`
- `src/modules/cmdpal/ext/SamplePagesExtension/SamplesListPage.cs`
- `src/modules/cmdpal/ext/SamplePagesExtension/SamplePagesCommandsProvider.cs`

### New Files (PowerDisplay CLI support)
- `src/modules/powerdisplay/PowerDisplay/Helpers/WindowHelper.cs` — add `PositionWindowNear()` method
- `src/modules/powerdisplay/PowerDisplay/PowerDisplayXAML/MainWindow.xaml.cs` — add `ShowWindowAt()` method
- `src/modules/powerdisplay/PowerDisplay/PowerDisplayXAML/App.xaml.cs` — add show-at position handling
- `src/modules/powerdisplay/PowerDisplay/Program.cs` — add `--show-at` CLI arg parsing

### New Files (SampleExtension dock band)
- `src/modules/cmdpal/ext/SamplePagesExtension/SampleShowPowerDisplayCommand.cs` — new InvokableCommand
- `src/modules/cmdpal/ext/SamplePagesExtension/SamplePowerDisplayDockBand.cs` — new WrappedDockItem
- `src/modules/cmdpal/ext/SamplePagesExtension/SamplePagesCommandsProvider.cs` — register new dock band
- `src/modules/cmdpal/ext/SamplePagesExtension/NativeMethods.txt` — add GetCursorPos

---

### Task 1: Revert IControlsPage commit

**Files:**
- All files from commit `9a600dea16` (see reverted file lists above)

- [ ] **Step 1: Revert the IControlsPage commit**

```bash
git revert --no-commit 9a600dea16
```

This stages all revert changes without committing, so we can verify and commit in one step.

- [ ] **Step 2: Verify the revert is clean**

```bash
git diff --cached --stat
```

Expected: 17 files changed — 11 files deleted, 6 files modified (restored to main state). The files should match:
- Deleted: `ControlItemViewModel.cs`, `ControlsPageViewModel.cs`, `ControlsSectionViewModel.cs`, `ControlsPage.xaml`, `ControlsPage.xaml.cs`, `SampleControlsPage.cs`, `SampleControlsDockBand.cs`, `ControlItem.cs`, `ControlsPage.cs` (toolkit), `ControlsSection.cs`
- Modified: `Microsoft.CommandPalette.Extensions.idl`, `CommandPalettePageViewModelFactory.cs`, `PageViewModel.cs`, `MainWindow.xaml.cs` (CmdPal), `ShellPage.xaml.cs`, `SamplesListPage.cs`, `SamplePagesCommandsProvider.cs`

- [ ] **Step 3: Commit the revert**

```bash
git commit -m "$(cat <<'EOF'
Revert "[CmdPal] Add IControlsPage page type with toggle/slider controls"

This reverts commit 9a600dea16. IControlsPage is no longer needed for
PowerDisplay dock mode — using CLI-based flyout positioning instead.

Co-Authored-By: Claude Opus 4 <noreply@anthropic.com>
EOF
)"
```

---

### Task 2: Add `PositionWindowNear()` to WindowHelper

**Files:**
- Modify: `src/modules/powerdisplay/PowerDisplay/Helpers/WindowHelper.cs:159` (add new method after `PositionWindowBottomRight`)

- [ ] **Step 1: Add the `PositionWindowNear()` method**

Add the following method to `WindowHelper.cs` after the `PositionWindowBottomRight` method (after line 173):

```csharp
/// <summary>
/// Position a window near an anchor point on screen.
/// Prefers placing the window above the anchor (for bottom-docked panels),
/// but falls back to below if there isn't enough space above.
/// Clamps to the display's work area to avoid going off-screen.
/// </summary>
/// <param name="window">WinUIEx window to position</param>
/// <param name="anchorScreenXPixels">Anchor X coordinate in absolute screen pixels</param>
/// <param name="anchorScreenYPixels">Anchor Y coordinate in absolute screen pixels</param>
/// <param name="widthDip">Window width in device-independent pixels (DIP)</param>
/// <param name="heightDip">Window height in device-independent pixels (DIP)</param>
/// <param name="marginDip">Margin between the anchor point and the window edge in DIP</param>
public static void PositionWindowNear(
    WindowEx window,
    int anchorScreenXPixels,
    int anchorScreenYPixels,
    int widthDip,
    int heightDip,
    int marginDip = 8)
{
    // Find the display containing the anchor point
    var displayArea = DisplayArea.GetFromPoint(
        new Windows.Graphics.PointInt32(anchorScreenXPixels, anchorScreenYPixels),
        DisplayAreaFallback.Nearest);

    if (displayArea is null)
    {
        ManagedCommon.Logger.LogWarning("PositionWindowNear: Unable to determine target display, skipping positioning");
        return;
    }

    double dpiScale = GetDpiScale(displayArea);
    int w = ScaleToPhysicalPixels(widthDip, dpiScale);
    int h = ScaleToPhysicalPixels(heightDip, dpiScale);
    int margin = ScaleToPhysicalPixels(marginDip, dpiScale);

    // WorkArea relative to DisplayArea (accounts for taskbar position)
    var rel = GetWorkAreaRelativeToDisplay(displayArea);

    // Convert anchor point to be relative to the display area
    var outer = displayArea.OuterBounds;
    int anchorRelX = anchorScreenXPixels - outer.X;
    int anchorRelY = anchorScreenYPixels - outer.Y;

    // Center horizontally on the anchor point
    int x = anchorRelX - (w / 2);

    // Prefer placing above the anchor point (common for bottom dock)
    int y = anchorRelY - h - margin;

    // If not enough space above, place below
    if (y < rel.Y)
    {
        y = anchorRelY + margin;
    }

    // Clamp to work area bounds
    x = Math.Max(rel.X, Math.Min(x, rel.X + rel.Width - w));
    y = Math.Max(rel.Y, Math.Min(y, rel.Y + rel.Height - h));

    window.AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(x, y, w, h), displayArea);
}
```

- [ ] **Step 2: Verify it builds**

```bash
cd src/modules/powerdisplay/PowerDisplay && dotnet build --no-restore 2>&1 | tail -5
```

Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add src/modules/powerdisplay/PowerDisplay/Helpers/WindowHelper.cs
git commit -m "$(cat <<'EOF'
Add WindowHelper.PositionWindowNear() for anchor-based positioning

Positions a window near a screen coordinate, preferring above the
anchor point (for bottom dock panels), with fallback and clamping.

Co-Authored-By: Claude Opus 4 <noreply@anthropic.com>
EOF
)"
```

---

### Task 3: Add `ShowWindowAt()` to PowerDisplay MainWindow

**Files:**
- Modify: `src/modules/powerdisplay/PowerDisplay/PowerDisplayXAML/MainWindow.xaml.cs`

- [ ] **Step 1: Add the `ShowWindowAt()` method**

Add the following method after `ShowWindow()` (after line 195 in MainWindow.xaml.cs):

```csharp
/// <summary>
/// Show the window positioned near a specific screen coordinate.
/// Used when PowerDisplay is invoked from a dock band with position info.
/// </summary>
/// <param name="screenXPixels">Target X screen coordinate in physical pixels</param>
/// <param name="screenYPixels">Target Y screen coordinate in physical pixels</param>
public void ShowWindowAt(int screenXPixels, int screenYPixels)
{
    Logger.LogInfo($"ShowWindowAt: Called with position ({screenXPixels}, {screenYPixels})");
    _isShowingWindow = true;
    try
    {
        if (!_hasInitialized)
        {
            Logger.LogWarning("ShowWindowAt: Window not fully initialized yet, showing anyway");
        }

        // Measure content height
        RootGrid?.UpdateLayout();
        MainContainer?.Measure(new Windows.Foundation.Size(AppConstants.UI.WindowWidthDip, double.PositiveInfinity));
        var contentHeight = (int)Math.Ceiling(MainContainer?.DesiredSize.Height ?? 0);
        var maxHeightDip = GetAdaptiveWindowMaxHeightDip();
        var finalHeightDip = Math.Min(contentHeight, maxHeightDip);

        // Position near the anchor point
        using (_dpiSuppressor?.Suppress() ?? default)
        {
            WindowHelper.PositionWindowNear(
                this,
                screenXPixels,
                screenYPixels,
                AppConstants.UI.WindowWidthDip,
                finalHeightDip);
        }

        this.Activate();
        this.Show();
        this.IsAlwaysOnTop = true;
        this.BringToFront();
        RootGrid.Focus(FocusState.Programmatic);

        Logger.LogInfo($"ShowWindowAt: Window shown at ({screenXPixels}, {screenYPixels})");
    }
    catch (Exception ex)
    {
        Logger.LogError($"ShowWindowAt: Failed: {ex.Message}\n{ex.StackTrace}");
        throw;
    }
    finally
    {
        _isShowingWindow = false;
    }
}
```

- [ ] **Step 2: Verify it builds**

```bash
cd src/modules/powerdisplay/PowerDisplay && dotnet build --no-restore 2>&1 | tail -5
```

Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add src/modules/powerdisplay/PowerDisplay/PowerDisplayXAML/MainWindow.xaml.cs
git commit -m "$(cat <<'EOF'
Add MainWindow.ShowWindowAt() for position-based show

Shows PowerDisplay window near a given screen pixel coordinate,
measuring content height and using PositionWindowNear for placement.

Co-Authored-By: Claude Opus 4 <noreply@anthropic.com>
EOF
)"
```

---

### Task 4: Add `--show-at` CLI argument support to Program.cs and App.xaml.cs

**Files:**
- Modify: `src/modules/powerdisplay/PowerDisplay/Program.cs`
- Modify: `src/modules/powerdisplay/PowerDisplay/PowerDisplayXAML/App.xaml.cs`

- [ ] **Step 1: Add `--show-at` parsing to Program.cs**

Replace the argument parsing section in `Program.cs` (lines 64-81) with:

```csharp
        // Parse command line arguments:
        // Mode 1 (PowerToys runner): args[0] = runner_pid, args[1] = pipe_name
        // Mode 2 (CLI show-at):      --show-at <x_pixels> <y_pixels>
        int runnerPid = -1;
        string? pipeName = null;
        int? showAtX = null;
        int? showAtY = null;

        if (args.Length >= 2 && args[0] == "--show-at")
        {
            if (int.TryParse(args[1], out int x) && args.Length >= 3 && int.TryParse(args[2], out int y))
            {
                showAtX = x;
                showAtY = y;
            }
        }
        else
        {
            if (args.Length >= 1 && int.TryParse(args[0], out int parsedPid))
            {
                runnerPid = parsedPid;
            }

            if (args.Length >= 2)
            {
                pipeName = args[1];
            }
        }
```

Update the App construction (line 87) to pass position:

```csharp
        Microsoft.UI.Xaml.Application.Start((p) =>
        {
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            _app = new App(runnerPid, pipeName, showAtX, showAtY);
        });
```

- [ ] **Step 2: Handle `--show-at` in redirect activation (Program.cs)**

For the already-running instance case, the redirecting process needs to communicate the position. Write position to a temp file before redirecting.

Replace the single-instance check block (lines 49-54) with:

```csharp
        if (!keyInstance.IsCurrent)
        {
            // Another instance exists - write show-at position to temp file if provided,
            // then redirect activation
            if (showAtX.HasValue && showAtY.HasValue)
            {
                var tempPath = Path.Combine(Path.GetTempPath(), "PowerDisplay_ShowAt.txt");
                File.WriteAllText(tempPath, $"{showAtX.Value} {showAtY.Value}");
            }

            RedirectActivationTo(activationArgs, keyInstance);
            return 0;
        }
```

Note: This requires adding `using System.IO;` at the top of Program.cs (it should already be implicitly available, but add if needed).

- [ ] **Step 3: Update `OnActivated` in Program.cs to read position from temp file**

Replace the `OnActivated` handler (lines 138-156) with:

```csharp
    private static void OnActivated(object? sender, AppActivationArguments args)
    {
        Logger.LogInfo("OnActivated: Redirect activation received");

        if (_app?.MainWindow is MainWindow mainWindow)
        {
            mainWindow.DispatcherQueue.TryEnqueue(() =>
            {
                // Check if a show-at position was written by the redirecting process
                var tempPath = Path.Combine(Path.GetTempPath(), "PowerDisplay_ShowAt.txt");
                if (File.Exists(tempPath))
                {
                    try
                    {
                        var content = File.ReadAllText(tempPath).Trim();
                        File.Delete(tempPath);
                        var parts = content.Split(' ');
                        if (parts.Length == 2 && int.TryParse(parts[0], out int x) && int.TryParse(parts[1], out int y))
                        {
                            Logger.LogInfo($"OnActivated: Showing window at ({x}, {y}) from temp file");
                            mainWindow.ShowWindowAt(x, y);
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning($"OnActivated: Failed to read show-at position: {ex.Message}");
                    }
                }

                // Fallback: toggle window (original behavior)
                Logger.LogTrace("OnActivated: Toggling window (no position specified)");
                mainWindow.ToggleWindow();
            });
        }
        else
        {
            Logger.LogWarning("OnActivated: MainWindow not available");
        }
    }
```

- [ ] **Step 4: Update App constructor and OnLaunched to handle show-at position**

Update the `App` constructor (line 32 in App.xaml.cs) to accept and store position:

```csharp
    private readonly int? _showAtX;
    private readonly int? _showAtY;

    public App(int runnerPid, string? pipeName, int? showAtX = null, int? showAtY = null)
    {
        Logger.LogInfo($"App constructor: Starting with runnerPid={runnerPid}, pipeName={pipeName ?? "null"}, showAt=({showAtX}, {showAtY})");
        _powerToysRunnerPid = runnerPid;
        _pipeName = pipeName;
        _showAtX = showAtX;
        _showAtY = showAtY;
```

The rest of the constructor body stays the same.

Update the window visibility section in `OnLaunched()` (lines 162-178) to handle show-at:

```csharp
            // Window visibility depends on launch mode
            bool isStandaloneMode = _powerToysRunnerPid <= 0;
            Logger.LogInfo($"OnLaunched: isStandaloneMode={isStandaloneMode}");

            if (_showAtX.HasValue && _showAtY.HasValue)
            {
                // CLI show-at mode - show window at specified position
                Logger.LogInfo($"OnLaunched: Showing window at ({_showAtX.Value}, {_showAtY.Value})");
                _mainWindow.ShowWindowAt(_showAtX.Value, _showAtY.Value);
            }
            else if (isStandaloneMode)
            {
                // Standalone mode - activate and show window immediately
                Logger.LogInfo("OnLaunched: Activating window (standalone mode)");
                _mainWindow.Activate();
                Logger.LogInfo("OnLaunched: Window activated (standalone mode)");
            }
            else
            {
                // PowerToys mode - window remains hidden until show event received
                Logger.LogInfo("OnLaunched: Window created but hidden, waiting for show/toggle event (PowerToys mode)");
            }
```

- [ ] **Step 5: Verify it builds**

```bash
cd src/modules/powerdisplay/PowerDisplay && dotnet build --no-restore 2>&1 | tail -5
```

Expected: Build succeeded.

- [ ] **Step 6: Commit**

```bash
git add src/modules/powerdisplay/PowerDisplay/Program.cs src/modules/powerdisplay/PowerDisplay/PowerDisplayXAML/App.xaml.cs
git commit -m "$(cat <<'EOF'
Add --show-at X Y CLI argument support to PowerDisplay

When launched with --show-at, positions the window at the given screen
coordinates. For already-running instances, passes position via temp
file before redirect activation.

Co-Authored-By: Claude Opus 4 <noreply@anthropic.com>
EOF
)"
```

---

### Task 5: Create SampleExtension dock band command for PowerDisplay

**Files:**
- Create: `src/modules/cmdpal/ext/SamplePagesExtension/SampleShowPowerDisplayCommand.cs`
- Create: `src/modules/cmdpal/ext/SamplePagesExtension/SamplePowerDisplayDockBand.cs`
- Modify: `src/modules/cmdpal/ext/SamplePagesExtension/SamplePagesCommandsProvider.cs`
- Modify: `src/modules/cmdpal/ext/SamplePagesExtension/NativeMethods.txt`

- [ ] **Step 1: Add `GetCursorPos` to NativeMethods.txt**

Append to `src/modules/cmdpal/ext/SamplePagesExtension/NativeMethods.txt`:

```
GetCursorPos
```

The file should now contain:
```
GetForegroundWindow
GetWindowTextLength
GetWindowText
GetCursorPos
```

- [ ] **Step 2: Create `SampleShowPowerDisplayCommand.cs`**

Create `src/modules/cmdpal/ext/SamplePagesExtension/SampleShowPowerDisplayCommand.cs`:

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Win32;

namespace SamplePagesExtension;

internal sealed partial class SampleShowPowerDisplayCommand : InvokableCommand
{
    public SampleShowPowerDisplayCommand()
    {
        Name = "Show PowerDisplay";
    }

    public override ICommandResult Invoke()
    {
        try
        {
            PInvoke.GetCursorPos(out var cursorPos);

            var extensionDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var exePath = Path.GetFullPath(Path.Combine(extensionDir!, "..", "..", "..", "PowerDisplay", "PowerDisplay.exe"));

            if (!File.Exists(exePath))
            {
                return CommandResult.ShowToast($"PowerDisplay.exe not found at {exePath}");
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = $"--show-at {cursorPos.X} {cursorPos.Y}",
                UseShellExecute = false,
            });

            return CommandResult.Dismiss();
        }
        catch (Exception ex)
        {
            return CommandResult.ShowToast($"Failed to launch PowerDisplay: {ex.Message}");
        }
    }
}
```

Note: The relative path `../../../PowerDisplay/PowerDisplay.exe` navigates from the SamplePagesExtension output directory (`WinUI3Apps/CmdPalExtensions/SamplePagesExtension/`) up to the `WinUI3Apps/` level and into `PowerDisplay/`. Verify this path matches your build output layout and adjust if necessary.

- [ ] **Step 3: Create `SamplePowerDisplayDockBand.cs`**

Create `src/modules/cmdpal/ext/SamplePagesExtension/SamplePowerDisplayDockBand.cs`:

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SamplePagesExtension;

internal sealed partial class SamplePowerDisplayDockBand : WrappedDockItem
{
    public SamplePowerDisplayDockBand()
        : base(new SampleShowPowerDisplayCommand(), "PowerDisplay")
    {
        Icon = new IconInfo("\uE7F4"); // Monitor icon
    }
}
```

- [ ] **Step 4: Register the new dock band in `SamplePagesCommandsProvider.cs`**

In `src/modules/cmdpal/ext/SamplePagesExtension/SamplePagesCommandsProvider.cs`, update the `GetDockBands()` method (line 35-40). The current code after revert is:

```csharp
    public override ICommandItem[] GetDockBands()
    {
        List<ICommandItem> bands = new()
        {
            new SampleDockBand(),
            new SampleButtonsDockBand(),
        };

        return bands.ToArray();
    }
```

Change to:

```csharp
    public override ICommandItem[] GetDockBands()
    {
        List<ICommandItem> bands = new()
        {
            new SampleDockBand(),
            new SampleButtonsDockBand(),
            new SamplePowerDisplayDockBand(),
        };

        return bands.ToArray();
    }
```

- [ ] **Step 5: Verify it builds**

```bash
cd src/modules/cmdpal/ext/SamplePagesExtension && dotnet build --no-restore 2>&1 | tail -5
```

Expected: Build succeeded.

- [ ] **Step 6: Commit**

```bash
git add src/modules/cmdpal/ext/SamplePagesExtension/SampleShowPowerDisplayCommand.cs \
        src/modules/cmdpal/ext/SamplePagesExtension/SamplePowerDisplayDockBand.cs \
        src/modules/cmdpal/ext/SamplePagesExtension/SamplePagesCommandsProvider.cs \
        src/modules/cmdpal/ext/SamplePagesExtension/NativeMethods.txt
git commit -m "$(cat <<'EOF'
Add SampleExtension dock band for launching PowerDisplay at cursor

Creates SampleShowPowerDisplayCommand (IInvokableCommand) that gets
cursor position and launches PowerDisplay.exe --show-at X Y, plus a
SamplePowerDisplayDockBand to register it in the dock.

Co-Authored-By: Claude Opus 4 <noreply@anthropic.com>
EOF
)"
```

---

### Task 6: Manual smoke test

- [ ] **Step 1: Build the full solution**

```bash
cd <repo-root> && dotnet build --no-restore 2>&1 | tail -10
```

Expected: Build succeeded with no errors related to ControlsPage, IControlsPage, or the new PowerDisplay changes.

- [ ] **Step 2: Manual test plan**

Test the following scenarios:

1. **PowerDisplay not running**: Click the PowerDisplay dock band item in CmdPal. PowerDisplay.exe should launch and its window should appear near the cursor position (above the dock bar).

2. **PowerDisplay already running**: With PowerDisplay already open, click the dock band item again. The window should reposition to the new cursor location (no duplicate instances).

3. **Default positioning preserved**: Launch PowerDisplay.exe without `--show-at` args (e.g., from tray icon or hotkey). Window should appear at the default bottom-right position.

4. **Revert verification**: Confirm that the CmdPal palette no longer shows the "Sample controls page" entry in the samples list and the SampleControlsDockBand no longer appears in the dock.

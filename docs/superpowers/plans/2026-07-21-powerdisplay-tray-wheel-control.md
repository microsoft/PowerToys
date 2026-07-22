# PowerDisplay Tray Mouse Wheel Control Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add configurable mouse-wheel brightness adjustment over the existing PowerDisplay tray icon, with Off, Primary display, and All displays modes, while allowing Off to disable flyout slider wheel input too.

**Architecture:** Store a shared three-state mode in `PowerDisplay.Models` and propagate it through Settings UI and the PowerDisplay view models. Explorer's icon-specific `WM_MOUSEMOVE` callback arms a dedicated-thread `WH_MOUSE_LL` listener only while the real icon is hovered; `TrayIconService` revalidates icon bounds and emits complete wheel notches on the UI thread. `MainViewModel` resolves the primary GDI display, delegates relative value calculation to a pure planner, and reuses each monitor view model's existing debounced brightness setter.

**Tech Stack:** C# preview/.NET 10, WinUI 3, CommunityToolkit.Mvvm, CsWin32-compatible `LibraryImport`, Win32 Shell and hook APIs, System.Text.Json source generation, MSTest, PowerToys build scripts.

## Global Constraints

- Work in `C:\Users\yuleng\source\repos\powerdisplay-wheel-design` on branch `yuleng/worktree/issue-49410-design`.
- Follow `docs/superpowers/specs/2026-07-21-issue-49410-design.md`.
- Do not add third-party dependencies or modify Runner, IPC, GPO, installer, CLI, or telemetry.
- Do not add a brightness OSD, toast, or other feedback surface.
- Keep the setting JSON-compatible: missing `mouse_wheel_control_mode` means `PrimaryDisplay`; unsupported numeric values normalize to `Disabled`.
- `Disabled` must disable tray scrolling and all four flyout slider wheel handlers without swallowing flyout page scrolling.
- `PrimaryDisplay` and `AllDisplays` affect only tray scrolling. Flyout sliders keep their existing per-control behavior.
- All-display adjustment is relative per monitor and preserves brightness differences.
- Ignore hidden monitors, monitors without brightness support, and monitors without `MonitorReadFlags.Brightness`.
- Tray adjustment ignores linked-brightness mode and exclusions; the next All Displays slider action may resynchronize displays.
- Never use `MonitorNumber == 1` as the primary-display test.
- Install `WH_MOUSE_LL` only after Explorer confirms hover over the PowerDisplay icon; remove it on leave, disable, icon removal, Explorer restart, and shutdown.
- Keep tray icon identity/configuration separate from live Explorer registration. Ordinary settings
  refreshes never repeat `NIM_ADD`; missing registration self-heals with 250 ms, 500 ms, 1 s, 2 s,
  then 5-second capped retries until success, with no user notification.
- The hook callback must not call Shell, WinUI, logging, LINQ, monitor, or hardware APIs. It must always return `CallNextHookEx`.
- Use AOT-safe interop and keep native callback delegates rooted for their complete lifetime.
- Run `tools\build\build-essentials.cmd` before the first targeted build.
- Build test projects before running `vstest.console.exe`; do not use `dotnet test`.
- Use x64 Debug for the TDD loop, then validate PowerDisplay on x64 and ARM64.
- All source, comments, resources, commit messages, and plan artifacts remain in English.
- Every implementation commit must include the trailers required by the active Copilot session.

---

## File and Responsibility Map

| File | Responsibility |
| --- | --- |
| `src/modules/powerdisplay/PowerDisplay.Models/MouseWheelControlMode.cs` | Shared persisted enum and safe normalization. |
| `src/settings-ui/Settings.UI.Library/PowerDisplayProperties.cs` | JSON property and legacy default. |
| `src/modules/powerdisplay/PowerDisplay.Lib/Services/WheelDeltaAccumulator.cs` | Pure high-resolution wheel delta accumulation. |
| `src/modules/powerdisplay/PowerDisplay.Lib/Services/TrayIconBounds.cs` | Pure screen-coordinate rectangle checks shared by the listener and tests. |
| `src/modules/powerdisplay/PowerDisplay.Lib/Services/TrayIconRegistrationBackoff.cs` | Pure capped retry sequence for Explorer registration recovery. |
| `src/modules/powerdisplay/PowerDisplay.Lib/Services/TrayWheelAdjustmentPlanner.cs` | Pure target selection, relative adjustment, and clamping. |
| `src/settings-ui/Settings.UI/ViewModels/PowerDisplayViewModel.cs` | Settings mode adapter, enabled state, persistence, and live update signal. |
| `src/settings-ui/Settings.UI/SettingsXAML/Views/PowerDisplayPage.xaml` | Three-state selector and disabled increment card. |
| `src/settings-ui/Settings.UI/Strings/en-us/Resources.resw` | Localized labels, descriptions, choices, and accessible name. |
| `src/modules/powerdisplay/PowerDisplay/ViewModels/MainViewModel.cs` | Runtime mode and derived wheel-enabled state. |
| `src/modules/powerdisplay/PowerDisplay/ViewModels/MonitorViewModel.cs` | Flyout binding proxy plus planner inputs. |
| `src/modules/powerdisplay/PowerDisplay/PowerDisplayXAML/MainWindow.xaml` | Bind all slider wheel handlers to the mode. |
| `src/modules/powerdisplay/PowerDisplay/Helpers/TrayIconMouseWheelListener.cs` | Dedicated message-loop thread and scoped low-level hook. |
| `src/modules/powerdisplay/PowerDisplay/Helpers/TrayIconService.cs` | Real-icon hover confirmation, bounds cache, sample validation, and notch event. |
| `src/modules/powerdisplay/PowerDisplay/ViewModels/MainViewModel.TrayWheel.cs` | Primary-display resolution, planner invocation, and brightness application. |
| `src/modules/powerdisplay/PowerDisplay/PowerDisplayXAML/App.xaml.cs` | Connect validated tray notches to `MainViewModel`. |
| `src/modules/powerdisplay/PowerDisplay.Lib.UnitTests/*.cs` | Settings, accumulator, bounds, and planner behavior. |
| `src/settings-ui/Settings.UI.UnitTests/ViewModelTests/PowerDisplay.cs` | Settings view-model persistence and enabled-state notifications. |

---

### Task 1: Add the Shared Mouse Wheel Mode Contract

**Files:**
- Create: `src/modules/powerdisplay/PowerDisplay.Models/MouseWheelControlMode.cs`
- Modify: `src/settings-ui/Settings.UI.Library/PowerDisplayProperties.cs:17-30,52-57`
- Create: `src/modules/powerdisplay/PowerDisplay.Lib.UnitTests/MouseWheelControlModeSettingsTests.cs`

**Interfaces:**
- Produces: `MouseWheelControlMode` with `Disabled`, `PrimaryDisplay`, and `AllDisplays`.
- Produces: `MouseWheelControlMode Normalize(this MouseWheelControlMode mode)`.
- Produces: `PowerDisplayProperties.MouseWheelControlMode`, serialized as `mouse_wheel_control_mode`.
- Consumed by: every later task.

- [ ] **Step 1: Restore and build repository essentials**

Run from the repository root:

```powershell
tools\build\build-essentials.cmd -Platform x64 -Configuration Debug
```

Expected: exit code 0. If it fails, inspect `build.debug.x64.errors.log` before continuing.

- [ ] **Step 2: Write the failing settings contract tests**

Create `src/modules/powerdisplay/PowerDisplay.Lib.UnitTests/MouseWheelControlModeSettingsTests.cs`:

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Models;

namespace PowerDisplay.UnitTests;

[TestClass]
public class MouseWheelControlModeSettingsTests
{
    [TestMethod]
    public void Default_IsPrimaryDisplay()
    {
        var properties = new PowerDisplayProperties();

        Assert.AreEqual(MouseWheelControlMode.PrimaryDisplay, properties.MouseWheelControlMode);
    }

    [TestMethod]
    public void Deserialize_LegacyJsonMissingField_DefaultsToPrimaryDisplay()
    {
        const string legacyJson = """
        {
            "monitor_refresh_delay": 5,
            "mouse_wheel_increment": 5,
            "show_system_tray_icon": true
        }
        """;

        var properties = JsonSerializer.Deserialize<PowerDisplayProperties>(legacyJson);

        Assert.IsNotNull(properties);
        Assert.AreEqual(MouseWheelControlMode.PrimaryDisplay, properties.MouseWheelControlMode);
    }

    [TestMethod]
    public void RoundTrip_PreservesEverySupportedMode()
    {
        MouseWheelControlMode[] modes =
        [
            MouseWheelControlMode.Disabled,
            MouseWheelControlMode.PrimaryDisplay,
            MouseWheelControlMode.AllDisplays,
        ];

        foreach (var mode in modes)
        {
            var json = JsonSerializer.Serialize(new PowerDisplayProperties { MouseWheelControlMode = mode });
            var restored = JsonSerializer.Deserialize<PowerDisplayProperties>(json);

            Assert.IsNotNull(restored);
            Assert.AreEqual(mode, restored.MouseWheelControlMode);
        }
    }

    [TestMethod]
    public void Serialize_UsesSnakeCaseJsonKey()
    {
        var properties = new PowerDisplayProperties
        {
            MouseWheelControlMode = MouseWheelControlMode.AllDisplays,
        };

        var json = JsonSerializer.Serialize(properties);

        StringAssert.Contains(json, "\"mouse_wheel_control_mode\":2");
    }

    [TestMethod]
    public void Normalize_UnsupportedValue_ReturnsDisabled()
    {
        var unsupported = (MouseWheelControlMode)99;

        Assert.AreEqual(MouseWheelControlMode.Disabled, unsupported.Normalize());
    }
}
```

- [ ] **Step 3: Build to verify the tests fail for the missing contract**

Run:

```powershell
$repo = (git rev-parse --show-toplevel)
& "$repo\tools\build\build.ps1" `
    -Platform x64 `
    -Configuration Debug `
    -Path "$repo\src\modules\powerdisplay\PowerDisplay.Lib.UnitTests" `
    "/p:SolutionDir=$repo\"
```

Expected: non-zero exit code with compiler errors for missing `MouseWheelControlMode` and `PowerDisplayProperties.MouseWheelControlMode`.

- [ ] **Step 4: Add the enum and normalization helper**

Create `src/modules/powerdisplay/PowerDisplay.Models/MouseWheelControlMode.cs`:

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerDisplay.Models;

/// <summary>
/// Defines how PowerDisplay handles mouse-wheel input.
/// </summary>
public enum MouseWheelControlMode
{
    /// <summary>
    /// Disables tray-icon and flyout-slider mouse-wheel adjustment.
    /// </summary>
    Disabled = 0,

    /// <summary>
    /// Enables flyout-slider adjustment and targets the primary display from the tray icon.
    /// </summary>
    PrimaryDisplay = 1,

    /// <summary>
    /// Enables flyout-slider adjustment and targets all visible displays from the tray icon.
    /// </summary>
    AllDisplays = 2,
}

/// <summary>
/// Provides validation helpers for <see cref="MouseWheelControlMode"/>.
/// </summary>
public static class MouseWheelControlModeExtensions
{
    /// <summary>
    /// Returns a supported mode, or <see cref="MouseWheelControlMode.Disabled"/> for an
    /// unsupported persisted numeric value.
    /// </summary>
    /// <param name="mode">The persisted mode value.</param>
    /// <returns>A supported mode value.</returns>
    public static MouseWheelControlMode Normalize(this MouseWheelControlMode mode)
        => mode is MouseWheelControlMode.Disabled
            or MouseWheelControlMode.PrimaryDisplay
            or MouseWheelControlMode.AllDisplays
            ? mode
            : MouseWheelControlMode.Disabled;
}
```

- [ ] **Step 5: Add the persisted property with an explicit legacy default**

In `PowerDisplayProperties()` add the assignment immediately after `MouseWheelIncrement = 5;`:

```csharp
MouseWheelControlMode = PowerDisplay.Models.MouseWheelControlMode.PrimaryDisplay;
```

After `MouseWheelIncrement`, add:

```csharp
/// <summary>
/// Gets or sets how PowerDisplay handles mouse-wheel input. The selected display target applies
/// to tray-icon scrolling; Disabled also turns off wheel input for flyout sliders.
/// </summary>
[JsonPropertyName("mouse_wheel_control_mode")]
public MouseWheelControlMode MouseWheelControlMode { get; set; }
```

- [ ] **Step 6: Build and run the focused tests**

Run:

```powershell
$repo = (git rev-parse --show-toplevel)
& "$repo\tools\build\build.ps1" `
    -Platform x64 `
    -Configuration Debug `
    -Path "$repo\src\modules\powerdisplay\PowerDisplay.Lib.UnitTests" `
    "/p:SolutionDir=$repo\"

$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$vsRoot = & $vswhere -latest -prerelease -products * -property installationPath
$vstest = Get-ChildItem -Path $vsRoot -Filter vstest.console.exe -Recurse |
    Select-Object -First 1 -ExpandProperty FullName
& $vstest `
    "$repo\x64\Debug\tests\PowerDisplay.Lib.UnitTests\PowerDisplay.Lib.UnitTests.dll" `
    '/TestCaseFilter:FullyQualifiedName~MouseWheelControlModeSettingsTests'
```

Expected: build exit code 0 and all `MouseWheelControlModeSettingsTests` pass.

- [ ] **Step 7: Commit the shared contract**

```powershell
git add `
    src/modules/powerdisplay/PowerDisplay.Models/MouseWheelControlMode.cs `
    src/settings-ui/Settings.UI.Library/PowerDisplayProperties.cs `
    src/modules/powerdisplay/PowerDisplay.Lib.UnitTests/MouseWheelControlModeSettingsTests.cs
git commit -m "Add PowerDisplay mouse wheel mode"
```

---

### Task 2: Add Pure Wheel Input Primitives

**Files:**
- Create: `src/modules/powerdisplay/PowerDisplay.Lib/Services/WheelDeltaAccumulator.cs`
- Create: `src/modules/powerdisplay/PowerDisplay.Lib/Services/TrayIconBounds.cs`
- Create: `src/modules/powerdisplay/PowerDisplay.Lib.UnitTests/WheelDeltaAccumulatorTests.cs`
- Create: `src/modules/powerdisplay/PowerDisplay.Lib.UnitTests/TrayIconBoundsTests.cs`

**Interfaces:**
- Produces: `int WheelDeltaAccumulator.Add(int delta)`.
- Produces: `void WheelDeltaAccumulator.Reset()`.
- Produces: `TrayIconBounds.IsValid` and `bool Contains(int x, int y)`.
- Consumed by: `TrayIconMouseWheelListener` and `TrayIconService`.

- [ ] **Step 1: Write failing accumulator tests**

Create `src/modules/powerdisplay/PowerDisplay.Lib.UnitTests/WheelDeltaAccumulatorTests.cs`:

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Common.Services;

namespace PowerDisplay.UnitTests;

[TestClass]
public class WheelDeltaAccumulatorTests
{
    [TestMethod]
    public void Add_FullPositiveNotch_ReturnsOne()
    {
        var accumulator = new WheelDeltaAccumulator();

        Assert.AreEqual(1, accumulator.Add(120));
    }

    [TestMethod]
    public void Add_FullNegativeNotch_ReturnsMinusOne()
    {
        var accumulator = new WheelDeltaAccumulator();

        Assert.AreEqual(-1, accumulator.Add(-120));
    }

    [TestMethod]
    public void Add_MultipleNotchesInOnePacket_ReturnsAllNotches()
    {
        var accumulator = new WheelDeltaAccumulator();

        Assert.AreEqual(3, accumulator.Add(360));
    }

    [TestMethod]
    public void Add_PartialPackets_EmitsOnlyAfterCompleteNotch()
    {
        var accumulator = new WheelDeltaAccumulator();

        Assert.AreEqual(0, accumulator.Add(30));
        Assert.AreEqual(0, accumulator.Add(30));
        Assert.AreEqual(0, accumulator.Add(30));
        Assert.AreEqual(1, accumulator.Add(30));
    }

    [TestMethod]
    public void Add_DirectionReversal_CancelsPartialRemainder()
    {
        var accumulator = new WheelDeltaAccumulator();

        Assert.AreEqual(0, accumulator.Add(80));
        Assert.AreEqual(0, accumulator.Add(-40));
        Assert.AreEqual(0, accumulator.Add(-40));
    }

    [TestMethod]
    public void Reset_DropsPartialRemainder()
    {
        var accumulator = new WheelDeltaAccumulator();
        Assert.AreEqual(0, accumulator.Add(60));

        accumulator.Reset();

        Assert.AreEqual(0, accumulator.Add(60));
    }
}
```

- [ ] **Step 2: Write failing bounds tests**

Create `src/modules/powerdisplay/PowerDisplay.Lib.UnitTests/TrayIconBoundsTests.cs`:

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Common.Services;

namespace PowerDisplay.UnitTests;

[TestClass]
public class TrayIconBoundsTests
{
    [TestMethod]
    public void Contains_UsesLeftTopInclusiveAndRightBottomExclusive()
    {
        var bounds = new TrayIconBounds(10, 20, 30, 40);

        Assert.IsTrue(bounds.Contains(10, 20));
        Assert.IsTrue(bounds.Contains(29, 39));
        Assert.IsFalse(bounds.Contains(30, 39));
        Assert.IsFalse(bounds.Contains(29, 40));
    }

    [TestMethod]
    public void IsValid_RejectsEmptyOrInvertedRectangles()
    {
        Assert.IsTrue(new TrayIconBounds(10, 20, 30, 40).IsValid);
        Assert.IsFalse(new TrayIconBounds(10, 20, 10, 40).IsValid);
        Assert.IsFalse(new TrayIconBounds(10, 20, 30, 20).IsValid);
        Assert.IsFalse(new TrayIconBounds(30, 40, 10, 20).IsValid);
    }
}
```

- [ ] **Step 3: Build to verify the tests fail for missing types**

Run:

```powershell
$repo = (git rev-parse --show-toplevel)
& "$repo\tools\build\build.ps1" `
    -Platform x64 `
    -Configuration Debug `
    -Path "$repo\src\modules\powerdisplay\PowerDisplay.Lib.UnitTests" `
    "/p:SolutionDir=$repo\"
```

Expected: non-zero exit code with compiler errors for `WheelDeltaAccumulator` and `TrayIconBounds`.

- [ ] **Step 4: Implement the accumulator**

Create `src/modules/powerdisplay/PowerDisplay.Lib/Services/WheelDeltaAccumulator.cs`:

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerDisplay.Common.Services;

/// <summary>
/// Accumulates high-resolution wheel deltas into complete wheel notches.
/// </summary>
public sealed class WheelDeltaAccumulator
{
    /// <summary>
    /// The Win32 delta value for one complete wheel notch.
    /// </summary>
    public const int WheelDelta = 120;

    private int _remainder;

    /// <summary>
    /// Adds a signed wheel delta and returns the number of newly completed notches.
    /// </summary>
    /// <param name="delta">The signed Win32 wheel delta.</param>
    /// <returns>The number of newly completed notches.</returns>
    public int Add(int delta)
    {
        var total = (long)_remainder + delta;
        var notches = (int)(total / WheelDelta);
        _remainder = (int)(total % WheelDelta);
        return notches;
    }

    /// <summary>
    /// Clears any incomplete wheel-notch remainder.
    /// </summary>
    public void Reset()
    {
        _remainder = 0;
    }
}
```

- [ ] **Step 5: Implement the bounds value type**

Create `src/modules/powerdisplay/PowerDisplay.Lib/Services/TrayIconBounds.cs`:

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerDisplay.Common.Services;

/// <summary>
/// Represents a notification icon rectangle in virtual-screen coordinates.
/// </summary>
public readonly record struct TrayIconBounds(int Left, int Top, int Right, int Bottom)
{
    /// <summary>
    /// Gets a value indicating whether the rectangle has positive width and height.
    /// </summary>
    public bool IsValid => Right > Left && Bottom > Top;

    /// <summary>
    /// Determines whether a screen point is inside the rectangle.
    /// </summary>
    /// <param name="x">The virtual-screen X coordinate.</param>
    /// <param name="y">The virtual-screen Y coordinate.</param>
    /// <returns><see langword="true"/> when the point is inside the rectangle.</returns>
    public bool Contains(int x, int y)
        => IsValid && x >= Left && x < Right && y >= Top && y < Bottom;
}
```

- [ ] **Step 6: Build and run the focused primitive tests**

Run the x64 Debug unit-test build, then:

```powershell
$repo = (git rev-parse --show-toplevel)
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$vsRoot = & $vswhere -latest -prerelease -products * -property installationPath
$vstest = Get-ChildItem -Path $vsRoot -Filter vstest.console.exe -Recurse |
    Select-Object -First 1 -ExpandProperty FullName
& $vstest `
    "$repo\x64\Debug\tests\PowerDisplay.Lib.UnitTests\PowerDisplay.Lib.UnitTests.dll" `
    '/TestCaseFilter:FullyQualifiedName~WheelDeltaAccumulatorTests|FullyQualifiedName~TrayIconBoundsTests'
```

Expected: all accumulator and bounds tests pass.

- [ ] **Step 7: Commit the wheel primitives**

```powershell
git add `
    src/modules/powerdisplay/PowerDisplay.Lib/Services/WheelDeltaAccumulator.cs `
    src/modules/powerdisplay/PowerDisplay.Lib/Services/TrayIconBounds.cs `
    src/modules/powerdisplay/PowerDisplay.Lib.UnitTests/WheelDeltaAccumulatorTests.cs `
    src/modules/powerdisplay/PowerDisplay.Lib.UnitTests/TrayIconBoundsTests.cs
git commit -m "Add PowerDisplay wheel input primitives"
```

---

### Task 3: Add the Relative Brightness Planner

**Files:**
- Create: `src/modules/powerdisplay/PowerDisplay.Lib/Services/TrayWheelAdjustmentPlanner.cs`
- Create: `src/modules/powerdisplay/PowerDisplay.Lib.UnitTests/TrayWheelAdjustmentPlannerTests.cs`

**Interfaces:**
- Consumes: `MouseWheelControlMode.Normalize()` from Task 1.
- Produces: `TrayWheelAdjustmentPlanner.Target`.
- Produces: `TrayWheelAdjustmentPlanner.Adjustment`.
- Produces: `IReadOnlyList<Adjustment> Plan(MouseWheelControlMode, IEnumerable<Target>, string?, long)`.
- Consumed by: `MainViewModel.AdjustBrightnessFromTrayWheel`.

- [ ] **Step 1: Write failing planner tests**

Create `src/modules/powerdisplay/PowerDisplay.Lib.UnitTests/TrayWheelAdjustmentPlannerTests.cs`:

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Common.Services;
using PowerDisplay.Models;
using static PowerDisplay.Common.Services.TrayWheelAdjustmentPlanner;

namespace PowerDisplay.UnitTests;

[TestClass]
public class TrayWheelAdjustmentPlannerTests
{
    private static Target Monitor(
        string id,
        string gdi,
        int brightness,
        bool supportsBrightness = true,
        bool hasBrightnessReading = true)
        => new(id, gdi, supportsBrightness, hasBrightnessReading, brightness);

    [TestMethod]
    public void Plan_Disabled_ReturnsNoAdjustments()
    {
        var result = Plan(
            MouseWheelControlMode.Disabled,
            [Monitor("a", @"\\.\DISPLAY1", 50)],
            @"\\.\DISPLAY1",
            5);

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void Plan_PrimaryDisplay_SelectsByGdiNameNotMonitorOrder()
    {
        Target[] targets =
        [
            Monitor("first", @"\\.\DISPLAY2", 40),
            Monitor("primary", @"\\.\DISPLAY7", 60),
        ];

        var result = Plan(
            MouseWheelControlMode.PrimaryDisplay,
            targets,
            @"\\.\display7",
            5);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(new Adjustment("primary", 65), result[0]);
    }

    [TestMethod]
    public void Plan_PrimaryDisplay_SelectsEveryMirroredPhysicalTarget()
    {
        Target[] targets =
        [
            Monitor("mirror-a", @"\\.\DISPLAY1", 30),
            Monitor("mirror-b", @"\\.\DISPLAY1", 70),
            Monitor("other", @"\\.\DISPLAY2", 50),
        ];

        var result = Plan(
            MouseWheelControlMode.PrimaryDisplay,
            targets,
            @"\\.\DISPLAY1",
            -10);

        CollectionAssert.AreEqual(
            new[] { new Adjustment("mirror-a", 20), new Adjustment("mirror-b", 60) },
            result.ToArray());
    }

    [TestMethod]
    public void Plan_PrimaryDisplayWithoutResolvedGdi_ReturnsNoAdjustments()
    {
        var result = Plan(
            MouseWheelControlMode.PrimaryDisplay,
            [Monitor("a", @"\\.\DISPLAY1", 50)],
            null,
            5);

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void Plan_AllDisplays_PreservesPerMonitorOffsets()
    {
        Target[] targets =
        [
            Monitor("a", @"\\.\DISPLAY1", 20),
            Monitor("b", @"\\.\DISPLAY2", 80),
        ];

        var result = Plan(
            MouseWheelControlMode.AllDisplays,
            targets,
            null,
            5);

        CollectionAssert.AreEqual(
            new[] { new Adjustment("a", 25), new Adjustment("b", 85) },
            result.ToArray());
    }

    [TestMethod]
    public void Plan_SkipsUnsupportedUnreadAndEmptyIdTargets()
    {
        Target[] targets =
        [
            Monitor("valid", @"\\.\DISPLAY1", 50),
            Monitor("unsupported", @"\\.\DISPLAY2", 50, supportsBrightness: false),
            Monitor("unread", @"\\.\DISPLAY3", 0, hasBrightnessReading: false),
            Monitor(string.Empty, @"\\.\DISPLAY4", 50),
        ];

        var result = Plan(MouseWheelControlMode.AllDisplays, targets, null, 5);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(new Adjustment("valid", 55), result[0]);
    }

    [TestMethod]
    public void Plan_ClampsEachTargetAtBrightnessBoundaries()
    {
        Target[] targets =
        [
            Monitor("low", @"\\.\DISPLAY1", 2),
            Monitor("high", @"\\.\DISPLAY2", 100),
        ];

        var result = Plan(MouseWheelControlMode.AllDisplays, targets, null, 10);

        CollectionAssert.AreEqual(
            new[] { new Adjustment("low", 12), new Adjustment("high", 100) },
            result.ToArray());
    }

    [TestMethod]
    public void Plan_LargeDeltaCannotOverflow()
    {
        var result = Plan(
            MouseWheelControlMode.AllDisplays,
            [Monitor("a", @"\\.\DISPLAY1", 50)],
            null,
            long.MaxValue);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(new Adjustment("a", 100), result[0]);
    }

    [TestMethod]
    public void Plan_LargeNegativeDeltaCannotOverflow()
    {
        var result = Plan(
            MouseWheelControlMode.AllDisplays,
            [Monitor("a", @"\\.\DISPLAY1", 50)],
            null,
            long.MinValue);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(new Adjustment("a", 0), result[0]);
    }
}
```

- [ ] **Step 2: Build to verify the tests fail for the missing planner**

Run the x64 Debug unit-test build.

Expected: non-zero exit code with compiler errors for `TrayWheelAdjustmentPlanner`, `Target`, and `Adjustment`.

- [ ] **Step 3: Implement the pure planner**

Create `src/modules/powerdisplay/PowerDisplay.Lib/Services/TrayWheelAdjustmentPlanner.cs`:

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using PowerDisplay.Models;

namespace PowerDisplay.Common.Services;

/// <summary>
/// Plans relative brightness changes for validated tray wheel input.
/// </summary>
public static class TrayWheelAdjustmentPlanner
{
    /// <summary>
    /// Describes a visible monitor's state needed for tray wheel planning.
    /// </summary>
    public readonly record struct Target(
        string Id,
        string GdiDeviceName,
        bool SupportsBrightness,
        bool HasBrightnessReading,
        int CurrentBrightness);

    /// <summary>
    /// Describes one monitor brightness update.
    /// </summary>
    public readonly record struct Adjustment(string Id, int Brightness);

    /// <summary>
    /// Selects eligible targets and computes clamped brightness values.
    /// </summary>
    /// <param name="mode">The effective mouse-wheel mode.</param>
    /// <param name="targets">Visible monitor states.</param>
    /// <param name="primaryGdiDeviceName">The primary logical display's GDI name.</param>
    /// <param name="delta">The signed relative brightness delta.</param>
    /// <returns>Brightness updates in target enumeration order.</returns>
    public static IReadOnlyList<Adjustment> Plan(
        MouseWheelControlMode mode,
        IEnumerable<Target> targets,
        string? primaryGdiDeviceName,
        long delta)
    {
        ArgumentNullException.ThrowIfNull(targets);

        mode = mode.Normalize();
        if (mode == MouseWheelControlMode.Disabled || delta == 0)
        {
            return [];
        }

        if (mode == MouseWheelControlMode.PrimaryDisplay &&
            string.IsNullOrWhiteSpace(primaryGdiDeviceName))
        {
            return [];
        }

        var boundedDelta = Math.Clamp(delta, -100L, 100L);
        var adjustments = new List<Adjustment>();
        foreach (var target in targets)
        {
            if (string.IsNullOrEmpty(target.Id) ||
                !target.SupportsBrightness ||
                !target.HasBrightnessReading)
            {
                continue;
            }

            if (mode == MouseWheelControlMode.PrimaryDisplay &&
                !string.Equals(
                    target.GdiDeviceName,
                    primaryGdiDeviceName,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var brightness = (int)Math.Clamp(
                target.CurrentBrightness + boundedDelta,
                0,
                100);

            adjustments.Add(new Adjustment(target.Id, brightness));
        }

        return adjustments;
    }
}
```

- [ ] **Step 4: Build and run the planner tests**

Run the x64 Debug unit-test build, then:

```powershell
$repo = (git rev-parse --show-toplevel)
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$vsRoot = & $vswhere -latest -prerelease -products * -property installationPath
$vstest = Get-ChildItem -Path $vsRoot -Filter vstest.console.exe -Recurse |
    Select-Object -First 1 -ExpandProperty FullName
& $vstest `
    "$repo\x64\Debug\tests\PowerDisplay.Lib.UnitTests\PowerDisplay.Lib.UnitTests.dll" `
    '/TestCaseFilter:FullyQualifiedName~TrayWheelAdjustmentPlannerTests'
```

Expected: all planner tests pass.

- [ ] **Step 5: Commit the planner**

```powershell
git add `
    src/modules/powerdisplay/PowerDisplay.Lib/Services/TrayWheelAdjustmentPlanner.cs `
    src/modules/powerdisplay/PowerDisplay.Lib.UnitTests/TrayWheelAdjustmentPlannerTests.cs
git commit -m "Add tray brightness adjustment planner"
```

---

### Task 4: Add the Three-State Setting to Settings UI

**Files:**
- Create: `src/settings-ui/Settings.UI.UnitTests/ViewModelTests/PowerDisplay.cs`
- Modify: `src/settings-ui/Settings.UI/ViewModels/PowerDisplayViewModel.cs:393-411`
- Modify: `src/settings-ui/Settings.UI/SettingsXAML/Views/PowerDisplayPage.xaml:81-92`
- Modify: `src/settings-ui/Settings.UI/Strings/en-us/Resources.resw:5584-5595`

**Interfaces:**
- Consumes: `MouseWheelControlMode` and `Normalize()` from Task 1.
- Produces: `int PowerDisplayViewModel.MouseWheelControlModeIndex`.
- Produces: `bool PowerDisplayViewModel.IsMouseWheelControlEnabled`.
- Persists the mode and signals `SettingsUpdatedPowerDisplayEvent`.

- [ ] **Step 1: Write failing Settings UI view-model tests**

Create `src/settings-ui/Settings.UI.UnitTests/ViewModelTests/PowerDisplay.cs`:

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.UnitTests.BackwardsCompatibility;
using Microsoft.PowerToys.Settings.UI.UnitTests.Mocks;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Models;

namespace ViewModelTests;

[TestClass]
public class PowerDisplayViewModelTests
{
    [TestMethod]
    public void MouseWheelMode_DefaultsToEnabledPrimaryDisplay()
    {
        using var viewModel = CreateViewModel(out _);

        Assert.AreEqual(
            (int)MouseWheelControlMode.PrimaryDisplay,
            viewModel.MouseWheelControlModeIndex);
        Assert.IsTrue(viewModel.IsMouseWheelControlEnabled);
    }

    [TestMethod]
    public void MouseWheelMode_SetDisabled_PersistsAndRaisesEnabledState()
    {
        using var viewModel = CreateViewModel(out var settings);
        var changedProperties = new List<string?>();
        viewModel.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        viewModel.MouseWheelControlModeIndex = (int)MouseWheelControlMode.Disabled;

        Assert.AreEqual(
            MouseWheelControlMode.Disabled,
            settings.Properties.MouseWheelControlMode);
        Assert.IsFalse(viewModel.IsMouseWheelControlEnabled);
        CollectionAssert.Contains(
            changedProperties,
            nameof(PowerDisplayViewModel.IsMouseWheelControlEnabled));
    }

    [TestMethod]
    public void MouseWheelMode_UnsupportedIndex_IsIgnored()
    {
        using var viewModel = CreateViewModel(out var settings);

        viewModel.MouseWheelControlModeIndex = 99;

        Assert.AreEqual(
            MouseWheelControlMode.PrimaryDisplay,
            settings.Properties.MouseWheelControlMode);
    }

    private static PowerDisplayViewModel CreateViewModel(out PowerDisplaySettings settings)
    {
        var powerDisplaySettingsUtils =
            ISettingsUtilsMocks.GetStubSettingsUtils<PowerDisplaySettings>();
        var generalSettingsUtils =
            ISettingsUtilsMocks.GetStubSettingsUtils<GeneralSettings>();

        settings = powerDisplaySettingsUtils.Object.GetSettingsOrDefault<PowerDisplaySettings>(
            PowerDisplaySettings.ModuleName);

        return new PowerDisplayViewModel(
            powerDisplaySettingsUtils.Object,
            new BackCompatTestProperties.MockSettingsRepository<GeneralSettings>(
                generalSettingsUtils.Object),
            new BackCompatTestProperties.MockSettingsRepository<PowerDisplaySettings>(
                powerDisplaySettingsUtils.Object),
            _ => 0,
            (_, _) => { });
    }
}
```

- [ ] **Step 2: Build to verify the Settings UI tests fail**

Run:

```powershell
$repo = (git rev-parse --show-toplevel)
& "$repo\tools\build\build.ps1" `
    -Platform x64 `
    -Configuration Debug `
    -Path "$repo\src\settings-ui\Settings.UI.UnitTests" `
    "/p:SolutionDir=$repo\"
```

Expected: non-zero exit code for missing `MouseWheelControlModeIndex` and `IsMouseWheelControlEnabled`.

- [ ] **Step 3: Add the Settings UI view-model properties**

Insert before `MouseWheelIncrement` in `PowerDisplayViewModel.cs`:

```csharp
/// <summary>
/// Gets or sets the selected mouse-wheel mode as the ComboBox index.
/// Enum values intentionally match the displayed item order.
/// </summary>
public int MouseWheelControlModeIndex
{
    get => (int)_settings.Properties.MouseWheelControlMode.Normalize();
    set
    {
        var mode = ((MouseWheelControlMode)value).Normalize();
        if ((int)mode != value)
        {
            return;
        }

        if (SetSettingsProperty(
            _settings.Properties.MouseWheelControlMode,
            mode,
            v => _settings.Properties.MouseWheelControlMode = v))
        {
            OnPropertyChanged(nameof(IsMouseWheelControlEnabled));
            SignalSettingsUpdated();
        }
    }
}

/// <summary>
/// Gets a value indicating whether PowerDisplay mouse-wheel control is enabled.
/// </summary>
public bool IsMouseWheelControlEnabled
    => _settings.Properties.MouseWheelControlMode.Normalize() !=
       MouseWheelControlMode.Disabled;
```

- [ ] **Step 4: Add the localized selector and disable the increment card**

Replace the existing mouse-wheel increment card block in `PowerDisplayPage.xaml` with:

```xml
<tkcontrols:SettingsCard x:Uid="PowerDisplay_MouseWheelControlMode">
    <ComboBox
        x:Uid="PowerDisplay_MouseWheelControlModeComboBox"
        MinWidth="{StaticResource PowerDisplayCompactActionControlMinWidth}"
        SelectedIndex="{x:Bind ViewModel.MouseWheelControlModeIndex, Mode=TwoWay}">
        <ComboBoxItem x:Uid="PowerDisplay_MouseWheelControlMode_Off" />
        <ComboBoxItem x:Uid="PowerDisplay_MouseWheelControlMode_PrimaryDisplay" />
        <ComboBoxItem x:Uid="PowerDisplay_MouseWheelControlMode_AllDisplays" />
    </ComboBox>
</tkcontrols:SettingsCard>
<tkcontrols:SettingsCard
    x:Uid="PowerDisplay_MouseWheelIncrement"
    IsEnabled="{x:Bind ViewModel.IsMouseWheelControlEnabled, Mode=OneWay}">
    <ComboBox
        MinWidth="{StaticResource PowerDisplayCompactActionControlMinWidth}"
        ItemsSource="{x:Bind ViewModel.MouseWheelIncrementOptions}"
        SelectedItem="{x:Bind ViewModel.MouseWheelIncrement, Mode=TwoWay}" />
</tkcontrols:SettingsCard>
```

- [ ] **Step 5: Add English resources**

Insert before `PowerDisplay_MouseWheelIncrement.Header` in `Resources.resw`:

```xml
  <data name="PowerDisplay_MouseWheelControlMode.Header" xml:space="preserve">
    <value>Mouse wheel control</value>
  </data>
  <data name="PowerDisplay_MouseWheelControlMode.Description" xml:space="preserve">
    <value>Choose which displays are adjusted when scrolling over the PowerDisplay tray icon. Off also disables mouse wheel adjustment in the PowerDisplay flyout.</value>
  </data>
  <data name="PowerDisplay_MouseWheelControlModeComboBox.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.Name" xml:space="preserve">
    <value>Mouse wheel control mode</value>
  </data>
  <data name="PowerDisplay_MouseWheelControlMode_Off.Content" xml:space="preserve">
    <value>Off</value>
  </data>
  <data name="PowerDisplay_MouseWheelControlMode_PrimaryDisplay.Content" xml:space="preserve">
    <value>Primary display</value>
  </data>
  <data name="PowerDisplay_MouseWheelControlMode_AllDisplays.Content" xml:space="preserve">
    <value>All displays</value>
  </data>
```

Update the existing `PowerDisplay_MouseWheelIncrement.Description` value to:

```xml
  <data name="PowerDisplay_MouseWheelIncrement.Description" xml:space="preserve">
    <value>How much brightness changes from tray scrolling, and how much brightness, contrast, and volume sliders change per mouse wheel notch.</value>
  </data>
```

- [ ] **Step 6: Build and run the Settings UI tests**

Format the modified XAML, build Settings UI UnitTests, and run the focused test:

```powershell
$repo = (git rev-parse --show-toplevel)
& "$repo\.pipelines\applyXamlStyling.ps1" -Unstaged

& "$repo\tools\build\build.ps1" `
    -Platform x64 `
    -Configuration Debug `
    -Path "$repo\src\settings-ui\Settings.UI.UnitTests" `
    "/p:SolutionDir=$repo\"

$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$vsRoot = & $vswhere -latest -prerelease -products * -property installationPath
$vstest = Get-ChildItem -Path $vsRoot -Filter vstest.console.exe -Recurse |
    Select-Object -First 1 -ExpandProperty FullName
& $vstest `
    "$repo\Debug\x64\tests\SettingsTests\Settings.UI.UnitTests.dll" `
    '/TestCaseFilter:FullyQualifiedName~PowerDisplayViewModelTests'

& "$repo\tools\build\build.ps1" `
    -Platform x64 `
    -Configuration Debug `
    -Path "$repo\src\settings-ui\Settings.UI" `
    "/p:SolutionDir=$repo\"
```

Expected: focused tests pass and `PowerToys.Settings.csproj` builds with valid XAML bindings.

- [ ] **Step 7: Commit the Settings UI**

```powershell
git add `
    src/settings-ui/Settings.UI.UnitTests/ViewModelTests/PowerDisplay.cs `
    src/settings-ui/Settings.UI/ViewModels/PowerDisplayViewModel.cs `
    src/settings-ui/Settings.UI/SettingsXAML/Views/PowerDisplayPage.xaml `
    src/settings-ui/Settings.UI/Strings/en-us/Resources.resw
git commit -m "Add PowerDisplay mouse wheel settings"
```

---

### Task 5: Propagate the Mode to the Flyout

**Files:**
- Modify: `src/modules/powerdisplay/PowerDisplay/ViewModels/MainViewModel.cs:91-110,134-139`
- Modify: `src/modules/powerdisplay/PowerDisplay/ViewModels/MainViewModel.cs:524-547`
- Modify: `src/modules/powerdisplay/PowerDisplay/ViewModels/MonitorViewModel.cs:211-218,866-885`
- Modify: `src/modules/powerdisplay/PowerDisplay/PowerDisplayXAML/MainWindow.xaml:233-243,523-594`

**Interfaces:**
- Consumes: persisted `MouseWheelControlMode`.
- Produces: `MainViewModel.MouseWheelControlMode`.
- Produces: `MainViewModel.IsMouseWheelControlEnabled`.
- Produces: `MonitorViewModel.IsMouseWheelControlEnabled`.
- Makes `Disabled` detach all four `SliderExtensions.PointerWheelChanged` handlers.

- [ ] **Step 1: Add the runtime mode and derived enabled property**

In the `MainViewModel` constructor, after `MouseWheelIncrement = 5;`, add:

```csharp
MouseWheelControlMode = PowerDisplay.Models.MouseWheelControlMode.PrimaryDisplay;
```

After `MouseWheelIncrement`, add:

```csharp
/// <summary>
/// Gets or sets the active mouse-wheel control mode loaded from PowerDisplay settings.
/// </summary>
[ObservableProperty]
[NotifyPropertyChangedFor(nameof(IsMouseWheelControlEnabled))]
public partial MouseWheelControlMode MouseWheelControlMode { get; set; }

/// <summary>
/// Gets a value indicating whether tray and flyout mouse-wheel adjustment is enabled.
/// </summary>
public bool IsMouseWheelControlEnabled
    => MouseWheelControlMode != PowerDisplay.Models.MouseWheelControlMode.Disabled;
```

- [ ] **Step 2: Load and normalize the mode**

In `LoadUIDisplaySettings()`, immediately after `MouseWheelIncrement` is assigned, add:

```csharp
MouseWheelControlMode = settings.Properties.MouseWheelControlMode.Normalize();
```

Keep `LoadUIDisplaySettings()` inside the existing settings-update path so mode changes take effect without restarting or rescanning hardware.

- [ ] **Step 3: Expose the mode to monitor item bindings**

After `MonitorViewModel.MouseWheelIncrement`, add:

```csharp
/// <summary>
/// Gets a value indicating whether this monitor's flyout sliders accept mouse-wheel input.
/// </summary>
public bool IsMouseWheelControlEnabled
    => _mainViewModel?.IsMouseWheelControlEnabled ?? false;
```

In `OnMainViewModelPropertyChanged`, add this branch before the linked-level branch:

```csharp
else if (e.PropertyName == nameof(MainViewModel.IsMouseWheelControlEnabled))
{
    OnPropertyChanged(nameof(IsMouseWheelControlEnabled));
}
```

- [ ] **Step 4: Bind all four slider wheel handlers**

In `MainWindow.xaml`, replace:

```xml
helpers:SliderExtensions.IsMouseWheelEnabled="True"
```

on the linked All Displays brightness slider with:

```xml
helpers:SliderExtensions.IsMouseWheelEnabled="{x:Bind ViewModel.IsMouseWheelControlEnabled, Mode=OneWay}"
```

Replace the same literal on each per-monitor brightness, contrast, and volume slider with:

```xml
helpers:SliderExtensions.IsMouseWheelEnabled="{x:Bind IsMouseWheelControlEnabled, Mode=OneWay}"
```

- [ ] **Step 5: Build PowerDisplay**

Run:

```powershell
$repo = (git rev-parse --show-toplevel)
& "$repo\.pipelines\applyXamlStyling.ps1" -Unstaged

& "$repo\tools\build\build.ps1" `
    -Platform x64 `
    -Configuration Debug `
    -Path "$repo\src\modules\powerdisplay\PowerDisplay" `
    "/p:SolutionDir=$repo\"
```

Expected: exit code 0 with source-generated property notifications and XAML bindings resolved.

- [ ] **Step 6: Manually verify disabled flyout wheel behavior**

Run the x64 Debug PowerDisplay build. In the flyout:

1. Select `Disabled`.
2. Hover each brightness, contrast, and volume slider and scroll.
3. Confirm values do not change.
4. Confirm the enclosing flyout page still scrolls.
5. Select `Primary display`.
6. Confirm slider wheel adjustment resumes using the configured increment.

Expected: all six checks pass.

- [ ] **Step 7: Commit runtime mode propagation**

```powershell
git add `
    src/modules/powerdisplay/PowerDisplay/ViewModels/MainViewModel.cs `
    src/modules/powerdisplay/PowerDisplay/ViewModels/MonitorViewModel.cs `
    src/modules/powerdisplay/PowerDisplay/PowerDisplayXAML/MainWindow.xaml
git commit -m "Honor mouse wheel mode in PowerDisplay flyout"
```

---

### Task 6: Add Scoped Native Tray Wheel Capture

**Files:**
- Create: `src/modules/powerdisplay/PowerDisplay/Helpers/TrayIconMouseWheelListener.cs`
- Modify: `src/modules/powerdisplay/PowerDisplay/Helpers/TrayIconService.cs:31-171,197-321`

**Interfaces:**
- Consumes: `TrayIconBounds` and `WheelDeltaAccumulator` from Task 2.
- Produces: `TrayIconService.MouseWheelScrolled`, an `Action<int>` event carrying complete signed notches.
- Keeps icon identity, hover generation, Shell bounds, native hook lifetime, and delta accumulation out of `MainViewModel`.

- [ ] **Step 1: Create the dedicated-thread listener**

Create `src/modules/powerdisplay/PowerDisplay/Helpers/TrayIconMouseWheelListener.cs`:

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using ManagedCommon;
using PowerDisplay.Common.Services;

namespace PowerDisplay.Helpers;

internal readonly record struct TrayWheelSample(
    int X,
    int Y,
    uint Timestamp,
    int Delta,
    long HoverGeneration);

internal sealed partial class TrayIconMouseWheelListener : IDisposable
{
    private const int WhMouseLl = 14;
    private const int HcAction = 0;
    private const uint WmMouseMove = 0x0200;
    private const uint WmMouseWheel = 0x020A;
    private const uint WmApp = 0x8000;
    private const uint WmSetEnabled = WmApp + 1;
    private const uint WmArm = WmApp + 2;
    private const uint WmDisarm = WmApp + 3;
    private const uint WmDrainSamples = WmApp + 4;
    private const uint WmShutdown = WmApp + 5;
    private const uint PmNoRemove = 0;
    private const int MaxQueuedSamples = 32;

    private readonly Action<TrayWheelSample[]> _sampleBatchHandler;
    private readonly Action<long> _disarmedHandler;
    private readonly ManualResetEventSlim _ready = new();
    private readonly object _pendingStateLock = new();
    private readonly Queue<TrayWheelSample> _samples = new(MaxQueuedSamples);
    private readonly Thread _thread;

    private LowLevelMouseProc? _hookProc;
    private uint _threadId;
    private nint _hookHandle;
    private bool _enabled;
    private volatile bool _armed;
    private bool _hookInstallFailureLogged;
    private bool _hookReleaseFailureLogged;
    private TrayIconBounds _pendingBounds;
    private long _pendingGeneration;
    private TrayIconBounds _activeBounds;
    private long _activeGeneration;
    private int _disposed;

    public TrayIconMouseWheelListener(
        Action<TrayWheelSample[]> sampleBatchHandler,
        Action<long> disarmedHandler)
    {
        ArgumentNullException.ThrowIfNull(sampleBatchHandler);
        ArgumentNullException.ThrowIfNull(disarmedHandler);

        _sampleBatchHandler = sampleBatchHandler;
        _disarmedHandler = disarmedHandler;
        _thread = new Thread(ThreadMain)
        {
            IsBackground = true,
            Name = "PowerDisplay.TrayMouseWheel",
        };
        _thread.Start();

        if (!_ready.Wait(TimeSpan.FromSeconds(5)))
        {
            throw new InvalidOperationException("Timed out starting the tray mouse-wheel thread.");
        }
    }

    /// <summary>
    /// Gets a value indicating whether the low-level hook is armed for the current hover.
    /// </summary>
    public bool IsArmed => _armed;

    public void SetEnabled(bool enabled)
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        PostCommand(WmSetEnabled, enabled ? 1u : 0u);
    }

    public void Arm(TrayIconBounds bounds, long hoverGeneration)
    {
        if (Volatile.Read(ref _disposed) != 0 || !bounds.IsValid)
        {
            return;
        }

        lock (_pendingStateLock)
        {
            _pendingBounds = bounds;
            _pendingGeneration = hoverGeneration;
        }

        PostCommand(WmArm, 0);
    }

    public void Disarm()
    {
        if (Volatile.Read(ref _disposed) == 0)
        {
            PostCommand(WmDisarm, 0);
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        if (!PostThreadMessageNative(_threadId, WmShutdown, 0, 0))
        {
            Logger.LogWarning(
                $"[TrayWheel] Failed to request hook thread shutdown with error {Marshal.GetLastPInvokeError()}");
        }

        if (!_thread.Join(TimeSpan.FromSeconds(5)))
        {
            Logger.LogError("[TrayWheel] Timed out stopping the hook thread");
        }

        _ready.Dispose();
        GC.SuppressFinalize(this);
    }

    private void ThreadMain()
    {
        _hookProc = HookCallback;
        _ = PeekMessageNative(out _, 0, 0, 0, PmNoRemove);
        _threadId = GetCurrentThreadIdNative();
        _ready.Set();

        var running = true;
        while (running)
        {
            var result = GetMessageNative(out var message, 0, 0, 0);
            if (result == 0)
            {
                break;
            }

            if (result < 0)
            {
                Logger.LogError(
                    $"[TrayWheel] GetMessage failed with error {Marshal.GetLastPInvokeError()}");
                break;
            }

            switch (message.Message)
            {
                case WmSetEnabled:
                    HandleSetEnabled(message.WParam != 0);
                    break;
                case WmArm:
                    HandleArm();
                    break;
                case WmDisarm:
                    DisarmCore(notify: true);
                    break;
                case WmDrainSamples:
                    DrainSamples();
                    break;
                case WmShutdown:
                    running = false;
                    break;
                default:
                    _ = TranslateMessageNative(ref message);
                    _ = DispatchMessageNative(ref message);
                    break;
            }
        }

        DisarmCore(notify: false);
    }

    private void HandleSetEnabled(bool enabled)
    {
        _enabled = enabled;
        if (!enabled)
        {
            DisarmCore(notify: true);
        }
    }

    private void HandleArm()
    {
        if (!_enabled)
        {
            return;
        }

        lock (_pendingStateLock)
        {
            _activeBounds = _pendingBounds;
            _activeGeneration = _pendingGeneration;
        }

        _armed = _activeBounds.IsValid && _activeGeneration != 0;
        if (_armed && !EnsureHook())
        {
            DisarmCore(notify: true);
        }
    }

    private bool EnsureHook()
    {
        if (_hookHandle != 0)
        {
            return true;
        }

        var hookPointer = Marshal.GetFunctionPointerForDelegate(_hookProc!);
        _hookHandle = SetWindowsHookExNative(
            WhMouseLl,
            hookPointer,
            GetModuleHandleNative(null),
            0);

        if (_hookHandle != 0)
        {
            _hookInstallFailureLogged = false;
            return true;
        }

        if (!_hookInstallFailureLogged)
        {
            Logger.LogWarning(
                $"[TrayWheel] SetWindowsHookEx failed with error {Marshal.GetLastPInvokeError()}");
            _hookInstallFailureLogged = true;
        }

        return false;
    }

    private void DisarmCore(bool notify)
    {
        var generation = _activeGeneration;
        _armed = false;
        _activeGeneration = 0;
        _activeBounds = default;
        _samples.Clear();

        if (_hookHandle != 0)
        {
            var hook = _hookHandle;
            _hookHandle = 0;
            if (!UnhookWindowsHookExNative(hook))
            {
                if (!_hookReleaseFailureLogged)
                {
                    Logger.LogWarning(
                        $"[TrayWheel] UnhookWindowsHookEx failed with error {Marshal.GetLastPInvokeError()}");
                    _hookReleaseFailureLogged = true;
                }
            }
            else
            {
                _hookReleaseFailureLogged = false;
            }
        }

        if (notify && generation != 0)
        {
            _disarmedHandler(generation);
        }
    }

    private void DrainSamples()
    {
        if (_samples.Count == 0)
        {
            return;
        }

        var batch = _samples.ToArray();
        _samples.Clear();
        _sampleBatchHandler(batch);
    }

    private unsafe nint HookCallback(int nCode, nuint wParam, nint lParam)
    {
        if (nCode == HcAction && _armed)
        {
            var data = *(MsllHookStruct*)lParam;
            var message = (uint)wParam;

            if (message == WmMouseMove && !_activeBounds.Contains(data.Point.X, data.Point.Y))
            {
                _armed = false;
                _ = PostThreadMessageNative(_threadId, WmDisarm, 0, 0);
            }
            else if (message == WmMouseWheel)
            {
                var delta = unchecked((short)(data.MouseData >> 16));
                if (delta != 0)
                {
                    if (_samples.Count == MaxQueuedSamples)
                    {
                        _ = _samples.Dequeue();
                    }

                    _samples.Enqueue(new TrayWheelSample(
                        data.Point.X,
                        data.Point.Y,
                        data.Time,
                        delta,
                        _activeGeneration));
                    _ = PostThreadMessageNative(_threadId, WmDrainSamples, 0, 0);
                }
            }
        }

        return CallNextHookExNative(_hookHandle, nCode, wParam, lParam);
    }

    private void PostCommand(uint message, nuint wParam)
    {
        if (!PostThreadMessageNative(_threadId, message, wParam, 0))
        {
            Logger.LogWarning(
                $"[TrayWheel] PostThreadMessage failed with error {Marshal.GetLastPInvokeError()}");
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate nint LowLevelMouseProc(int nCode, nuint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MsllHookStruct
    {
        public NativePoint Point;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public nuint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeMessage
    {
        public nint HWnd;
        public uint Message;
        public nuint WParam;
        public nint LParam;
        public uint Time;
        public NativePoint Point;
        public uint Private;
    }

    [LibraryImport("user32.dll", EntryPoint = "SetWindowsHookExW", SetLastError = true)]
    private static partial nint SetWindowsHookExNative(
        int hookType,
        nint hookProc,
        nint module,
        uint threadId);

    [LibraryImport("user32.dll", EntryPoint = "UnhookWindowsHookEx", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UnhookWindowsHookExNative(nint hook);

    [LibraryImport("user32.dll", EntryPoint = "CallNextHookEx")]
    private static partial nint CallNextHookExNative(
        nint hook,
        int code,
        nuint wParam,
        nint lParam);

    [LibraryImport("user32.dll", EntryPoint = "GetMessageW", SetLastError = true)]
    private static partial int GetMessageNative(
        out NativeMessage message,
        nint window,
        uint minimumMessage,
        uint maximumMessage);

    [LibraryImport("user32.dll", EntryPoint = "PeekMessageW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool PeekMessageNative(
        out NativeMessage message,
        nint window,
        uint minimumMessage,
        uint maximumMessage,
        uint removeMessage);

    [LibraryImport("user32.dll", EntryPoint = "PostThreadMessageW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool PostThreadMessageNative(
        uint threadId,
        uint message,
        nuint wParam,
        nint lParam);

    [LibraryImport("kernel32.dll", EntryPoint = "GetCurrentThreadId")]
    private static partial uint GetCurrentThreadIdNative();

    [LibraryImport("user32.dll", EntryPoint = "TranslateMessage")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool TranslateMessageNative(ref NativeMessage message);

    [LibraryImport("user32.dll", EntryPoint = "DispatchMessageW")]
    private static partial nint DispatchMessageNative(ref NativeMessage message);

    [LibraryImport("kernel32.dll", EntryPoint = "GetModuleHandleW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint GetModuleHandleNative(string? moduleName);
}
```

- [ ] **Step 2: Add wheel-capture state to `TrayIconService`**

Add `using Microsoft.UI.Dispatching;`, `using PowerDisplay.Common.Services;`, and `using PowerDisplay.Models;`.

Add these fields and event:

```csharp
private const uint WmMouseMove = 0x0200;
private const long BoundsCacheLifetimeMs = 1000;
private const uint MaxSampleAgeMs = 500;

private readonly DispatcherQueue _dispatcherQueue;
private readonly WheelDeltaAccumulator _wheelDeltaAccumulator = new();

private TrayIconMouseWheelListener? _mouseWheelListener;
private MouseWheelControlMode _mouseWheelControlMode;
private TrayIconBounds? _cachedBounds;
private long _boundsCacheTimestamp;
private long _hoverGeneration;
private bool _sampleDispatchFailureLogged;
private bool _boundsFailureLogged;

internal event Action<int>? MouseWheelScrolled;

/// <summary>
/// Gets or sets the UI-state gate checked before wheel deltas enter the accumulator.
/// </summary>
internal Func<bool>? CanProcessMouseWheel { get; set; }
```

In the constructor, initialize:

```csharp
_dispatcherQueue = DispatcherQueue.GetForCurrentThread();
```

- [ ] **Step 3: Add mode and listener lifecycle helpers**

Add these methods to `TrayIconService`:

```csharp
private void UpdateMouseWheelMode(MouseWheelControlMode mode)
{
    mode = mode.Normalize();
    if (_mouseWheelControlMode == mode)
    {
        if (mode != MouseWheelControlMode.Disabled && _trayIconData is not null)
        {
            EnsureMouseWheelListener();
        }

        return;
    }

    _mouseWheelControlMode = mode;
    InvalidateMouseWheelHover(disarm: true);

    if (mode == MouseWheelControlMode.Disabled)
    {
        DisposeMouseWheelListener();
    }
    else if (_trayIconData is not null)
    {
        EnsureMouseWheelListener();
    }
}

private void EnsureMouseWheelListener()
{
    if (_mouseWheelControlMode == MouseWheelControlMode.Disabled)
    {
        return;
    }

    _mouseWheelListener ??= new TrayIconMouseWheelListener(
        OnWheelSampleBatch,
        OnMouseWheelListenerDisarmed);
    _mouseWheelListener.SetEnabled(true);
}

private void DisposeMouseWheelListener()
{
    _mouseWheelListener?.Dispose();
    _mouseWheelListener = null;
    _cachedBounds = null;
    _wheelDeltaAccumulator.Reset();
}

private void InvalidateMouseWheelHover(bool disarm)
{
    unchecked
    {
        _hoverGeneration++;
    }

    _cachedBounds = null;
    _boundsCacheTimestamp = 0;
    _wheelDeltaAccumulator.Reset();

    if (disarm)
    {
        _mouseWheelListener?.Disarm();
    }
}
```

- [ ] **Step 4: Add icon bounds lookup and hover arming**

Add:

```csharp
private void HandleTrayMouseMove()
{
    if (_mouseWheelControlMode == MouseWheelControlMode.Disabled)
    {
        return;
    }

    if (!GetCursorPos(out var cursor))
    {
        if (!_boundsFailureLogged)
        {
            Logger.LogWarning("[TrayWheel] GetCursorPos failed while arming tray hover");
            _boundsFailureLogged = true;
        }

        return;
    }

    var now = Environment.TickCount64;
    if (_cachedBounds is TrayIconBounds cached &&
        now - _boundsCacheTimestamp <= BoundsCacheLifetimeMs &&
        cached.Contains(cursor.X, cursor.Y))
    {
        EnsureMouseWheelListener();
        if (_mouseWheelListener?.IsArmed != true)
        {
            unchecked
            {
                _hoverGeneration++;
            }

            _wheelDeltaAccumulator.Reset();
        }

        _mouseWheelListener?.Arm(cached, _hoverGeneration);
        return;
    }

    if (!TryGetCurrentIconBounds(out var bounds) ||
        !bounds.Contains(cursor.X, cursor.Y))
    {
        InvalidateMouseWheelHover(disarm: true);
        return;
    }

    var previousBounds = _cachedBounds;
    var startsNewHover =
        _mouseWheelListener?.IsArmed != true ||
        !previousBounds.HasValue ||
        previousBounds.Value != bounds;
    if (startsNewHover)
    {
        unchecked
        {
            _hoverGeneration++;
        }

        _wheelDeltaAccumulator.Reset();
    }

    _cachedBounds = bounds;
    _boundsCacheTimestamp = now;
    EnsureMouseWheelListener();
    _mouseWheelListener?.Arm(bounds, _hoverGeneration);
}

private bool TryGetCurrentIconBounds(out TrayIconBounds bounds)
{
    bounds = default;
    if (_hwnd == 0 || _trayIconData is null)
    {
        return false;
    }

    var identifier = new NotifyIconIdentifier
    {
        CbSize = (uint)Marshal.SizeOf<NotifyIconIdentifier>(),
        HWnd = _hwnd,
        Id = MyNotifyId,
        GuidItem = Guid.Empty,
    };

    var result = ShellNotifyIconGetRectNative(ref identifier, out var rect);
    bounds = new TrayIconBounds(rect.Left, rect.Top, rect.Right, rect.Bottom);
    if (result < 0 || !bounds.IsValid)
    {
        if (!_boundsFailureLogged)
        {
            Logger.LogWarning(
                $"[TrayWheel] Shell_NotifyIconGetRect failed with HRESULT 0x{result:X8}");
            _boundsFailureLogged = true;
        }

        return false;
    }

    _boundsFailureLogged = false;
    return true;
}
```

- [ ] **Step 5: Add UI-thread sample batching and disarm handling**

Add:

```csharp
private void OnWheelSampleBatch(TrayWheelSample[] samples)
{
    if (!_dispatcherQueue.TryEnqueue(() => ProcessWheelSampleBatch(samples)) &&
        !_sampleDispatchFailureLogged)
    {
        Logger.LogWarning("[TrayWheel] Failed to enqueue wheel samples to the UI thread");
        _sampleDispatchFailureLogged = true;
    }
}

private void ProcessWheelSampleBatch(TrayWheelSample[] samples)
{
    _sampleDispatchFailureLogged = false;

    if (_mouseWheelControlMode == MouseWheelControlMode.Disabled)
    {
        InvalidateMouseWheelHover(disarm: true);
        return;
    }

    if (CanProcessMouseWheel?.Invoke() != true)
    {
        _wheelDeltaAccumulator.Reset();
        return;
    }

    if (!TryGetCurrentIconBounds(out var currentBounds))
    {
        InvalidateMouseWheelHover(disarm: true);
        return;
    }

    var totalNotches = 0;
    var now = unchecked((uint)Environment.TickCount);
    foreach (var sample in samples)
    {
        if (sample.HoverGeneration != _hoverGeneration ||
            unchecked(now - sample.Timestamp) > MaxSampleAgeMs ||
            !currentBounds.Contains(sample.X, sample.Y))
        {
            InvalidateMouseWheelHover(disarm: true);
            return;
        }

        totalNotches += _wheelDeltaAccumulator.Add(sample.Delta);
    }

    _cachedBounds = currentBounds;
    _boundsCacheTimestamp = Environment.TickCount64;

    if (totalNotches != 0)
    {
        MouseWheelScrolled?.Invoke(totalNotches);
    }
}

private void OnMouseWheelListenerDisarmed(long generation)
{
    if (!_dispatcherQueue.TryEnqueue(() =>
    {
        if (generation == _hoverGeneration)
        {
            InvalidateMouseWheelHover(disarm: false);
        }
    }) &&
        !_sampleDispatchFailureLogged)
    {
        Logger.LogWarning("[TrayWheel] Failed to enqueue hover cleanup to the UI thread");
        _sampleDispatchFailureLogged = true;
    }
}
```

- [ ] **Step 6: Wire mode and hover handling into existing tray lifecycle**

In `SetupTrayIcon`, after reading settings, add:

```csharp
var mouseWheelMode = settings.Properties.MouseWheelControlMode.Normalize();
UpdateMouseWheelMode(mouseWheelMode);
```

Immediately after successful `NIM_ADD`, add:

```csharp
if (mouseWheelMode != MouseWheelControlMode.Disabled)
{
    EnsureMouseWheelListener();
}
```

At the start of `Destroy()`, add:

```csharp
DisposeMouseWheelListener();
_mouseWheelControlMode = MouseWheelControlMode.Disabled;
InvalidateMouseWheelHover(disarm: false);
```

When handling `_wmTaskbarRestart`, invalidate before re-adding:

```csharp
InvalidateMouseWheelHover(disarm: true);
SetupTrayIcon();
```

Restrict tray callbacks to this icon and handle its mouse move:

```csharp
else if (uMsg == WmTrayIcon && (uint)wParam == MyNotifyId)
{
    switch ((uint)lParam)
    {
        case WmMouseMove:
            HandleTrayMouseMove();
            break;
        case PInvoke.WM_RBUTTONUP:
            if (_popupMenu != 0)
            {
                GetCursorPos(out var cursorPos);
                SetForegroundWindow(_hwnd);
                TrackPopupMenuExNative(
                    _popupMenu,
                    (uint)TRACK_POPUP_MENU_FLAGS.TPM_LEFTALIGN |
                        (uint)TRACK_POPUP_MENU_FLAGS.TPM_BOTTOMALIGN,
                    cursorPos.X,
                    cursorPos.Y,
                    _hwnd,
                    0);
            }

            break;
        case PInvoke.WM_LBUTTONUP:
            _toggleWindowAction?.Invoke();
            break;
    }
}
```

In the existing `WM_COMMAND` Exit branch, enqueue the action so listener teardown does not
close the hidden tray window from inside its own window procedure:

```csharp
else if (wParam == PInvoke.WM_USER + 2)
{
    if (!_dispatcherQueue.TryEnqueue(_exitAction))
    {
        Logger.LogWarning("[TrayIcon] Failed to enqueue the exit action");
        _exitAction();
    }
}
```

- [ ] **Step 7: Add Shell bounds interop**

Add to the bottom of `TrayIconService`:

```csharp
[StructLayout(LayoutKind.Sequential)]
private struct NotifyIconIdentifier
{
    public uint CbSize;
    public nint HWnd;
    public uint Id;
    public Guid GuidItem;
}

[StructLayout(LayoutKind.Sequential)]
private struct NativeRect
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;
}

[LibraryImport("shell32.dll", EntryPoint = "Shell_NotifyIconGetRect")]
private static partial int ShellNotifyIconGetRectNative(
    ref NotifyIconIdentifier identifier,
    out NativeRect iconLocation);
```

- [ ] **Step 8: Build PowerDisplay**

Run:

```powershell
$repo = (git rev-parse --show-toplevel)
& "$repo\tools\build\build.ps1" `
    -Platform x64 `
    -Configuration Debug `
    -Path "$repo\src\modules\powerdisplay\PowerDisplay" `
    "/p:SolutionDir=$repo\"
```

Expected: exit code 0, including Native AOT analyzer checks.

- [ ] **Step 9: Manually verify scoped hook lifecycle without brightness wiring**

Run PowerDisplay under the debugger and set breakpoints in:

- `TrayIconMouseWheelListener.HandleArm`
- `TrayIconMouseWheelListener.DisarmCore`
- `TrayIconService.ProcessWheelSampleBatch`

Verify:

1. Hovering the PowerDisplay icon arms once.
2. Moving inside the icon does not repeatedly reinstall the hook.
3. Moving outside disarms.
4. Hovering only the overflow chevron never arms.
5. Hovering the PowerDisplay icon inside the open overflow panel arms.
6. Scrolling reaches `ProcessWheelSampleBatch`.
7. The original wheel event remains observable by the next hook.
8. Disabling the mode and hiding the tray icon both dispose the listener.

Expected: all eight checks pass.

- [ ] **Step 10: Commit scoped tray capture**

```powershell
git add `
    src/modules/powerdisplay/PowerDisplay/Helpers/TrayIconMouseWheelListener.cs `
    src/modules/powerdisplay/PowerDisplay/Helpers/TrayIconService.cs
git commit -m "Capture wheel input over PowerDisplay tray icon"
```

---

### Task 7: Apply Validated Tray Notches to Brightness

**Files:**
- Create: `src/modules/powerdisplay/PowerDisplay/ViewModels/MainViewModel.TrayWheel.cs`
- Modify: `src/modules/powerdisplay/PowerDisplay/ViewModels/MonitorViewModel.cs:255-310`
- Modify: `src/modules/powerdisplay/PowerDisplay/PowerDisplayXAML/App.xaml.cs:169-181`

**Interfaces:**
- Consumes: `TrayIconService.MouseWheelScrolled`.
- Consumes: `TrayWheelAdjustmentPlanner.Plan`.
- Produces: `void MainViewModel.AdjustBrightnessFromTrayWheel(int notches)`.
- Uses `MonitorViewModel.Brightness` as the only hardware-write entry point.

- [ ] **Step 1: Expose planner inputs on `MonitorViewModel`**

After `MonitorNumber`, add:

```csharp
/// <summary>
/// Gets the GDI display source name used to match the Windows primary display.
/// </summary>
public string GdiDeviceName => _monitor.GdiDeviceName;
```

After `SupportsBrightness`, add:

```csharp
/// <summary>
/// Gets a value indicating whether discovery read a trustworthy current brightness.
/// </summary>
public bool HasValidBrightnessReading
    => _monitor.ReadValues.HasFlag(MonitorReadFlags.Brightness);
```

- [ ] **Step 2: Implement primary-display resolution and adjustment**

Create `src/modules/powerdisplay/PowerDisplay/ViewModels/MainViewModel.TrayWheel.cs`:

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using ManagedCommon;
using PowerDisplay.Common.Drivers;
using PowerDisplay.Common.Services;
using PowerDisplay.Models;
using MouseWheelMode = PowerDisplay.Models.MouseWheelControlMode;

namespace PowerDisplay.ViewModels;

public partial class MainViewModel
{
    private const uint MonitorDefaultToPrimary = 1;

    private bool _trayWheelNoTargetLogged;

    /// <summary>
    /// Applies complete tray wheel notches to the configured brightness targets.
    /// </summary>
    /// <param name="notches">The signed number of complete wheel notches.</param>
    public void AdjustBrightnessFromTrayWheel(int notches)
    {
        var mode = MouseWheelControlMode.Normalize();
        if (mode == MouseWheelMode.Disabled ||
            notches == 0 ||
            MouseWheelIncrement <= 0 ||
            !IsInitialized ||
            !IsInteractionEnabled)
        {
            return;
        }

        string? primaryGdiDeviceName = null;
        if (mode == MouseWheelMode.PrimaryDisplay)
        {
            primaryGdiDeviceName = GetPrimaryGdiDeviceName();
        }

        var targets = new List<TrayWheelAdjustmentPlanner.Target>(Monitors.Count);
        foreach (var monitor in Monitors)
        {
            targets.Add(new TrayWheelAdjustmentPlanner.Target(
                monitor.Id,
                monitor.GdiDeviceName,
                monitor.SupportsBrightness,
                monitor.HasValidBrightnessReading,
                monitor.Brightness));
        }

        var delta = (long)notches * MouseWheelIncrement;
        var adjustments = TrayWheelAdjustmentPlanner.Plan(
            mode,
            targets,
            primaryGdiDeviceName,
            delta);

        if (adjustments.Count == 0)
        {
            if (!_trayWheelNoTargetLogged)
            {
                Logger.LogWarning("[TrayWheel] No valid brightness target was available");
                _trayWheelNoTargetLogged = true;
            }

            return;
        }

        _trayWheelNoTargetLogged = false;
        foreach (var adjustment in adjustments)
        {
            foreach (var monitor in Monitors)
            {
                if (MonitorIdComparer.Equal(monitor.Id, adjustment.Id))
                {
                    monitor.Brightness = adjustment.Brightness;
                    break;
                }
            }
        }
    }

    private static unsafe string? GetPrimaryGdiDeviceName()
    {
        var monitor = MonitorFromPointNative(
            new NativePoint(0, 0),
            MonitorDefaultToPrimary);
        if (monitor == 0)
        {
            return null;
        }

        var monitorInfo = new MonitorInfoEx
        {
            CbSize = (uint)sizeof(MonitorInfoEx),
        };

        return GetMonitorInfo(monitor, ref monitorInfo)
            ? monitorInfo.GetDeviceName()
            : null;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativePoint
    {
        public NativePoint(int x, int y)
        {
            X = x;
            Y = y;
        }

        public readonly int X;

        public readonly int Y;
    }

    [LibraryImport("user32.dll", EntryPoint = "MonitorFromPoint")]
    private static partial nint MonitorFromPointNative(
        NativePoint point,
        uint flags);
}
```

- [ ] **Step 3: Connect `TrayIconService` to the main view model**

In `App.OnLaunched`, keep a typed local for the created window:

```csharp
var mainWindow = new MainWindow();
_mainWindow = mainWindow;
```

After creating `TrayIconService`, subscribe before `SetupTrayIcon()`:

```csharp
_trayIconService.MouseWheelScrolled +=
    mainWindow.ViewModel.AdjustBrightnessFromTrayWheel;
_trayIconService.CanProcessMouseWheel =
    () => mainWindow.ViewModel.IsInitialized &&
        mainWindow.ViewModel.IsInteractionEnabled;
```

Change the tray Exit action from direct process termination to the existing cleanup path:

```csharp
Shutdown,
```

The resulting constructor call is:

```csharp
_trayIconService = new TrayIconService(
    _settingsUtils,
    ToggleMainWindow,
    Shutdown,
    OpenSettings);
_trayIconService.MouseWheelScrolled +=
    mainWindow.ViewModel.AdjustBrightnessFromTrayWheel;
_trayIconService.CanProcessMouseWheel =
    () => mainWindow.ViewModel.IsInitialized &&
        mainWindow.ViewModel.IsInteractionEnabled;
_trayIconService.SetupTrayIcon();
```

- [ ] **Step 4: Build and run all PowerDisplay library tests**

Run the x64 Debug unit-test build, then:

```powershell
$repo = (git rev-parse --show-toplevel)
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$vsRoot = & $vswhere -latest -prerelease -products * -property installationPath
$vstest = Get-ChildItem -Path $vsRoot -Filter vstest.console.exe -Recurse |
    Select-Object -First 1 -ExpandProperty FullName
& $vstest "$repo\x64\Debug\tests\PowerDisplay.Lib.UnitTests\PowerDisplay.Lib.UnitTests.dll"
```

Expected: all PowerDisplay.Lib.UnitTests pass.

- [ ] **Step 5: Build PowerDisplay**

Run the x64 Debug PowerDisplay build.

Expected: exit code 0.

- [ ] **Step 6: Manually verify primary and all-display behavior**

Use at least two visible brightness-capable displays:

1. Set different starting brightness values, such as 30 and 70.
2. Choose `Primary display`.
3. Hover the tray icon and scroll up one notch.
4. Confirm only physical displays sharing the primary GDI source increase by the configured increment.
5. Choose `All displays`.
6. Scroll down one notch.
7. Confirm every visible valid display decreases by the same increment and preserves the original difference.
8. Hide one monitor in PowerDisplay and confirm all-display mode no longer changes it.
9. Enable linked brightness, select primary mode, and confirm tray scrolling can temporarily diverge values.
10. Move the linked All Displays slider and confirm existing linked behavior resynchronizes its targets.

Expected: all ten checks pass.

- [ ] **Step 7: Commit brightness integration**

```powershell
git add `
    src/modules/powerdisplay/PowerDisplay/ViewModels/MainViewModel.TrayWheel.cs `
    src/modules/powerdisplay/PowerDisplay/ViewModels/MonitorViewModel.cs `
    src/modules/powerdisplay/PowerDisplay/PowerDisplayXAML/App.xaml.cs
git commit -m "Adjust brightness from PowerDisplay tray wheel"
```

---

### Task 8: Complete Cross-Architecture and End-to-End Verification

**Files:**
- Verify only; modify source files only if a check exposes a defect.

**Interfaces:**
- Verifies all contracts and behavior produced by Tasks 1 through 7.

- [ ] **Step 1: Run all targeted unit suites**

Run:

```powershell
$repo = (git rev-parse --show-toplevel)

& "$repo\tools\build\build.ps1" `
    -Platform x64 `
    -Configuration Debug `
    -Path "$repo\src\modules\powerdisplay\PowerDisplay.Lib.UnitTests" `
    "/p:SolutionDir=$repo\"

& "$repo\tools\build\build.ps1" `
    -Platform x64 `
    -Configuration Debug `
    -Path "$repo\src\settings-ui\Settings.UI.UnitTests" `
    "/p:SolutionDir=$repo\"

$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$vsRoot = & $vswhere -latest -prerelease -products * -property installationPath
$vstest = Get-ChildItem -Path $vsRoot -Filter vstest.console.exe -Recurse |
    Select-Object -First 1 -ExpandProperty FullName

& $vstest "$repo\x64\Debug\tests\PowerDisplay.Lib.UnitTests\PowerDisplay.Lib.UnitTests.dll"
& $vstest `
    "$repo\Debug\x64\tests\SettingsTests\net10.0-windows10.0.26100.0\Settings.UI.UnitTests.dll" `
    '/TestCaseFilter:FullyQualifiedName~ViewModelTests.PowerDisplay'
```

Expected: both test invocations exit 0 with no failures.

- [ ] **Step 2: Build PowerDisplay for x64 and ARM64**

Run:

```powershell
$repo = (git rev-parse --show-toplevel)
& "$repo\tools\build\build.ps1" `
    -Platform x64 `
    -Configuration Debug `
    -Path "$repo\src\modules\powerdisplay\PowerDisplay" `
    "/p:SolutionDir=$repo\"

& "$repo\tools\build\build-essentials.cmd" -Platform arm64 -Configuration Debug

& "$repo\tools\build\build.ps1" `
    -Platform arm64 `
    -Configuration Debug `
    -Path "$repo\src\modules\powerdisplay\PowerDisplay" `
    "/p:SolutionDir=$repo\"
```

Expected: both builds exit 0, proving the hook interop compiles for both architectures.

- [ ] **Step 3: Build Settings UI**

Run:

```powershell
$repo = (git rev-parse --show-toplevel)
& "$repo\tools\build\build.ps1" `
    -Platform x64 `
    -Configuration Debug `
    -Path "$repo\src\settings-ui\Settings.UI" `
    "/p:SolutionDir=$repo\"
```

Expected: exit code 0 with no XAML binding or resource errors.

- [ ] **Step 4: Run installed or sideloaded end-to-end verification**

Invoke the project `powertoys-verification` skill and verify:

1. Pinned tray icon on Windows 10 and Windows 11.
2. Tray icon in the overflow panel.
3. Overflow chevron alone does not change brightness.
4. Standard mouse and high-resolution wheel or touchpad.
5. Bottom, top, left, and right taskbar positions where supported.
6. Mixed DPI displays.
7. Primary display changes at runtime.
8. Mirrored primary displays.
9. Values at 0 and 100.
10. A display whose brightness read failed.
11. Mode changes without restarting PowerDisplay.
12. Tray icon disabled and re-enabled.
13. Explorer restart while the listener is armed.
14. Module shutdown while the listener is armed.
15. Existing tray left-click and context-menu behavior.
16. Scrolling during monitor discovery produces no immediate or delayed adjustment.

Expected: each item is recorded as PASS, or BLOCKED with an explicit environment reason. Any product failure must be fixed before completion.

- [ ] **Step 5: Review the final branch diff**

Run:

```powershell
git --no-pager diff --check origin/main...HEAD
git --no-pager diff --stat origin/main...HEAD
git --no-pager status --short
```

Expected:

- `git diff --check` exits 0.
- The diff contains only the approved PowerDisplay mouse-wheel feature, its tests, localization, and design/plan documents.
- The working tree is clean.

---

### Task 9: Make Tray Registration Self-Healing

**Files:**
- Create: `src/modules/powerdisplay/PowerDisplay.Lib/Services/TrayIconRegistrationBackoff.cs`
- Create: `src/modules/powerdisplay/PowerDisplay.Lib.UnitTests/TrayIconRegistrationBackoffTests.cs`
- Modify: `src/modules/powerdisplay/PowerDisplay/Helpers/TrayIconService.cs`
- Verify: `.superpowers/sdd/task-8-review-fix*.ps1` (ignored runtime harnesses; do not commit)

**Interfaces:**
- Produces: `TimeSpan TrayIconRegistrationBackoff.NextDelay()`.
- Produces: `void TrayIconRegistrationBackoff.Reset()`.
- Separates `TrayIconService` icon identity/configuration from live Explorer registration.
- Preserves the existing version-0 callback and wheel listener interfaces.

- [ ] **Step 1: Write failing capped-backoff tests**

Create `src/modules/powerdisplay/PowerDisplay.Lib.UnitTests/TrayIconRegistrationBackoffTests.cs`:

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Common.Services;

namespace PowerDisplay.UnitTests;

[TestClass]
public class TrayIconRegistrationBackoffTests
{
    [TestMethod]
    public void NextDelay_UsesCappedRecoverySequence()
    {
        var backoff = new TrayIconRegistrationBackoff();

        Assert.AreEqual(250, backoff.NextDelay().TotalMilliseconds);
        Assert.AreEqual(500, backoff.NextDelay().TotalMilliseconds);
        Assert.AreEqual(1000, backoff.NextDelay().TotalMilliseconds);
        Assert.AreEqual(2000, backoff.NextDelay().TotalMilliseconds);
        Assert.AreEqual(5000, backoff.NextDelay().TotalMilliseconds);
        Assert.AreEqual(5000, backoff.NextDelay().TotalMilliseconds);
    }

    [TestMethod]
    public void Reset_RestartsAtFirstDelay()
    {
        var backoff = new TrayIconRegistrationBackoff();
        _ = backoff.NextDelay();
        _ = backoff.NextDelay();

        backoff.Reset();

        Assert.AreEqual(250, backoff.NextDelay().TotalMilliseconds);
    }
}
```

- [ ] **Step 2: Build to verify RED**

Run:

```powershell
$repo = (git rev-parse --show-toplevel)
& "$repo\tools\build\build.ps1" `
    -Platform x64 `
    -Configuration Debug `
    -Path "$repo\src\modules\powerdisplay\PowerDisplay.Lib.UnitTests" `
    "/p:SolutionDir=$repo\"
```

Expected: non-zero exit code because `TrayIconRegistrationBackoff` does not exist.

- [ ] **Step 3: Implement the pure retry sequence**

Create `src/modules/powerdisplay/PowerDisplay.Lib/Services/TrayIconRegistrationBackoff.cs`:

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace PowerDisplay.Common.Services;

/// <summary>
/// Provides the capped retry sequence for notification-icon registration recovery.
/// </summary>
public sealed class TrayIconRegistrationBackoff
{
    private static readonly int[] DelayMilliseconds = [250, 500, 1000, 2000, 5000];

    private int _index;

    /// <summary>
    /// Gets the next retry delay, capped at five seconds.
    /// </summary>
    /// <returns>The next retry delay.</returns>
    public TimeSpan NextDelay()
    {
        var delay = DelayMilliseconds[_index];
        if (_index < DelayMilliseconds.Length - 1)
        {
            _index++;
        }

        return TimeSpan.FromMilliseconds(delay);
    }

    /// <summary>
    /// Restarts the sequence at 250 milliseconds.
    /// </summary>
    public void Reset()
    {
        _index = 0;
    }
}
```

- [ ] **Step 4: Run focused and full library tests**

Build the unit-test project, then run:

```powershell
$repo = (git rev-parse --show-toplevel)
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$vsRoot = & $vswhere -latest -prerelease -products * -property installationPath
$vstest = Get-ChildItem -Path $vsRoot -Filter vstest.console.exe -Recurse |
    Select-Object -First 1 -ExpandProperty FullName
$dll = "$repo\x64\Debug\tests\PowerDisplay.Lib.UnitTests\PowerDisplay.Lib.UnitTests.dll"
& $vstest $dll '/TestCaseFilter:FullyQualifiedName~TrayIconRegistrationBackoffTests'
& $vstest $dll
```

Expected: 2/2 focused tests and the complete suite pass.

- [ ] **Step 5: Reproduce the runtime RED with persisted state backup**

Run the existing ignored downstream harness before changing `TrayIconService`. It must persist raw
PowerDisplay/global settings backups and restore them in `finally`.

Expected RED on current-branch bits:

- AllDisplays + increment 10, real-icon `+120`: `50,50` remains `50,50`.
- `show_system_tray_icon=false`: one icon remains.
- Re-enable: two icons appear.
- Logs contain duplicate `NIM_ADD` failure.

- [ ] **Step 6: Separate desired visibility, identity, and live registration**

Add to `TrayIconService`:

```csharp
private static readonly TimeSpan RegistrationHealthInterval = TimeSpan.FromSeconds(5);
private static readonly TimeSpan ImmediateRegistrationCheck = TimeSpan.FromMilliseconds(1);

private readonly TrayIconRegistrationBackoff _registrationBackoff = new();

private DispatcherQueueTimer? _registrationTimer;
private bool _desiredTrayIconVisible;
private bool _isTrayIconRegistered;
private bool _registrationFailureLogged;
```

Restructure `SetupTrayIcon` so:

```csharp
_desiredTrayIconVisible = shouldShow;
UpdateMouseWheelMode(settings.Properties.MouseWheelControlMode.Normalize());

if (!shouldShow)
{
    Destroy();
    return;
}

// Preserve the existing window, NOTIFYICONDATA, icon, and menu creation blocks.
// Do not call NIM_ADD directly from a normal settings refresh.
EnsureTrayIconRegistration();
```

Add these helpers, adapting the existing `NOTIFYICONDATAW` and bounds interop without duplicating
their layouts:

```csharp
private void EnsureTrayIconRegistration()
{
    if (!_desiredTrayIconVisible || _trayIconData is null || _hwnd == 0)
    {
        return;
    }

    if (IsTrayIconRegistrationHealthy())
    {
        _isTrayIconRegistered = true;
        _registrationBackoff.Reset();
        _registrationFailureLogged = false;
        EnsureMouseWheelListener();
        ScheduleRegistrationCheck(RegistrationHealthInterval);
        return;
    }

    if (_isTrayIconRegistered)
    {
        _isTrayIconRegistered = false;
        InvalidateMouseWheelHover(disarm: true);
    }

    var data = (NOTIFYICONDATAW)_trayIconData;
    bool added;
    unsafe
    {
        added = Shell_NotifyIconNative((uint)NOTIFY_ICON_MESSAGE.NIM_ADD, &data);
    }

    if (added)
    {
        _isTrayIconRegistered = true;
        _registrationBackoff.Reset();
        _registrationFailureLogged = false;
        EnsureMouseWheelListener();
        ScheduleRegistrationCheck(RegistrationHealthInterval);
        return;
    }

    DisposeMouseWheelListener();
    if (!_registrationFailureLogged)
    {
        Logger.LogWarning("[TrayIcon] Shell_NotifyIcon(NIM_ADD) failed; retrying registration");
        _registrationFailureLogged = true;
    }

    ScheduleRegistrationCheck(_registrationBackoff.NextDelay());
}

private bool IsTrayIconRegistrationHealthy()
{
    if (_trayIconData is null || _hwnd == 0)
    {
        return false;
    }

    var identifier = new NotifyIconIdentifier
    {
        CbSize = (uint)Marshal.SizeOf<NotifyIconIdentifier>(),
        HWnd = _hwnd,
        Id = MyNotifyId,
        GuidItem = Guid.Empty,
    };

    return ShellNotifyIconGetRectNative(ref identifier, out _) >= 0;
}

private void ScheduleRegistrationCheck(TimeSpan delay)
{
    if (!_desiredTrayIconVisible)
    {
        return;
    }

    _registrationTimer ??= _dispatcherQueue.CreateTimer();
    _registrationTimer.IsRepeating = false;
    _registrationTimer.Tick -= OnRegistrationTimerTick;
    _registrationTimer.Tick += OnRegistrationTimerTick;
    _registrationTimer.Stop();
    _registrationTimer.Interval = delay;
    _registrationTimer.Start();
}

private void OnRegistrationTimerTick(DispatcherQueueTimer sender, object args)
{
    sender.Stop();
    EnsureTrayIconRegistration();
}

private void StopRegistrationRecovery()
{
    _registrationTimer?.Stop();
    _registrationBackoff.Reset();
    _registrationFailureLogged = false;
}
```

Apply the state consistently:

- `UpdateMouseWheelMode` may create a listener only when `_isTrayIconRegistered` is true.
- `TryGetCurrentIconBounds` requires `_isTrayIconRegistered`; a failed current-bounds query marks
  registration stale and schedules immediate recovery instead of destroying identity.
- `TaskbarCreated` marks `_isTrayIconRegistered=false`, invalidates hover, resets backoff, and
  schedules `ImmediateRegistrationCheck`.
- `WM_WINDOWPOSCHANGING` schedules an immediate check only when
  `_desiredTrayIconVisible && !_isTrayIconRegistered`.
- `Destroy` sets `_desiredTrayIconVisible=false`, stops recovery, issues `NIM_DELETE` when
  `_isTrayIconRegistered`, then clears registration, listener, identity, window, icon, and menu
  state idempotently.
- Never clear `_trayIconData` merely because `NIM_ADD` failed.

- [ ] **Step 7: Build and run automated regression coverage**

Run all PowerDisplay.Lib.UnitTests and build PowerDisplay x64 Debug.

Expected: all tests pass and the build exits 0 with no new warnings/errors.

- [ ] **Step 8: Verify runtime GREEN and exact cleanup**

Using the persisted-backup harness and current-branch process-path proof, verify:

1. Disabled blocks wheel adjustment without changing module PID.
2. AllDisplays + increment 10 changes `50,50` to `60,60` without changing PID.
3. Hide produces zero real PowerDisplay icons.
4. Re-enable produces exactly one icon and wheel input still works.
5. Explorer restart removes then self-heals exactly one icon; post-recovery wheel input works.
6. Logs contain no duplicate-add loop and at most one warning per registration outage.
7. Raw PowerDisplay/global settings SHA256, brightness, global enable, Explorer, and original
   runner/module paths are exactly restored.

Update `.superpowers/sdd/task-8-report.md` verdicts and evidence after the run.

- [ ] **Step 9: Re-run cross-architecture build**

Run ARM64 build essentials and PowerDisplay ARM64 Debug build.

Expected: both commands exit 0.

- [ ] **Step 10: Commit the self-healing registration fix**

```powershell
git add `
    src/modules/powerdisplay/PowerDisplay.Lib/Services/TrayIconRegistrationBackoff.cs `
    src/modules/powerdisplay/PowerDisplay.Lib.UnitTests/TrayIconRegistrationBackoffTests.cs `
    src/modules/powerdisplay/PowerDisplay/Helpers/TrayIconService.cs
git commit -m "Make PowerDisplay tray registration self-healing"
```

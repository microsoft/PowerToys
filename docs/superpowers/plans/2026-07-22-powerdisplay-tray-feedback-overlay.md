# PowerDisplay Tray Feedback Overlay Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace PowerDisplay's Shell tooltip with a no-activate, click-through WinUI overlay that handles ordinary tray hover and immediate wheel-adjustment feedback.

**Architecture:** Pure `TrayWheelFeedbackSession` and `TrayWheelFeedbackPlacement` units own timing and geometry decisions. A reusable `TransparentWindow`-based overlay owns only XAML measurement, no-activate display, click-through native style, positioning, and live accessibility notifications. `TrayIconService` coordinates Shell hover callbacks, a 100 ms hover-only poll, formatter output, overlay lifecycle, and the existing wheel listener without changing the low-level hook hot path.

**Tech Stack:** C# preview/.NET 10, WinUI 3, shared `TransparentWindow` and `FlyoutWindowHelper`, `Shell_NotifyIconGetRect`, `WindowEx`, Win32 window styles and WndProc subclassing, CommunityToolkit/MSTest, PowerToys build scripts.

## Global Constraints

- Work in `C:\Users\yuleng\source\repos\powerdisplay-wheel-design` on `yuleng/worktree/issue-49410-design`.
- Follow `docs/superpowers/specs/2026-07-22-powerdisplay-tray-feedback-overlay-design.md`.
- Retain completed feedback model/formatter/data-flow commits `51f3299be` and `4dd116ee3`.
- Permanently remove `NIF_TIP`; no Shell-managed PowerDisplay tooltip remains.
- Ordinary icon hover shows `Power Display` after 500 ms.
- Pointer containment is checked every 100 ms only during an active hover session.
- A complete wheel adjustment shows formatted feedback immediately.
- Two seconds after the last adjustment, restore `Power Display` if still hovered; otherwise hide.
- Pointer leave hides immediately on the next hover poll.
- Ordinary hover remains active when wheel mode is `Disabled`, but Disabled creates no low-level wheel hook.
- Overlay is topmost, no-activate, absent from task switchers, and click-through through both
  `WS_EX_TRANSPARENT` and `WM_NCHITTEST -> HTTRANSPARENT`.
- Derive the overlay from shared `TransparentWindow`; do not duplicate its frame/backdrop/SW_SHOWNA implementation.
- Anchor to `Shell_NotifyIconGetRect`, classify the nearest display edge, position inward by 8 DIP, and clamp to work area.
- Use `FlyoutWindowHelper.GetDpiScale` and `MoveAndResizeOnDisplay`.
- Visual style: 12x8 DIP padding, 8 DIP radius, 120-420 DIP one-line width, current theme resources.
- Use `AutomationProperties.LiveSetting="Polite"` and `RaiseNotificationEvent` with `MostRecent`.
- Overlay hover polling uses a non-destructive icon-bounds query; a single poll failure never alters tray registration state.
- Registration loss, icon hide, Explorer restart, and Destroy stop and hide the overlay idempotently.
- Overlay failure never blocks brightness adjustment and never changes registration backoff.
- No overlay work runs on `TrayIconMouseWheelListener.HookCallback`.
- Preserve version-0 callbacks, scoped hook behavior, target selection, debounce writes, click/menu, and self-healing registration.
- Do not add dependencies or modify Settings schema/UI, Runner, IPC, GPO, installer, CLI, or telemetry.
- Use x64 Debug for TDD, then build PowerDisplay on x64 and ARM64.
- Run build essentials before the first targeted build for each architecture.
- Build tests before `vstest.console.exe`; never use `dotnet test`.
- All source, comments, resources, documentation, and commit messages are English.
- Every implementation commit includes the trailers required by the active Copilot session.

---

## Existing Prerequisites

Already committed and reviewed:

- `PowerDisplay.Models/TrayWheelAdjustmentFeedback.cs`
- `PowerDisplay.Lib/Services/TrayWheelFeedbackTemplates.cs`
- `PowerDisplay.Lib/Services/TrayWheelFeedbackFormatter.cs`
- `PowerDisplay.Lib.UnitTests/TrayWheelFeedbackFormatterTests.cs`
- `MainViewModel.AdjustBrightnessFromTrayWheel` returns nullable feedback
- `App` currently invokes and discards that return value

The failed native-tooltip feasibility probe created no tracked product changes.

## File and Responsibility Map

| File | Responsibility |
| --- | --- |
| `PowerDisplay.Lib/Services/TrayWheelFeedbackSession.cs` | Pure 500 ms hover and two-second adjustment state. |
| `PowerDisplay.Lib/Services/TrayWheelFeedbackPlacement.cs` | Pure nearest-edge physical-pixel geometry and work-area clamping. |
| `PowerDisplay.Lib.UnitTests/TrayWheelFeedbackSessionTests.cs` | Session timing, leave, reset, and wrap behavior. |
| `PowerDisplay.Lib.UnitTests/TrayWheelFeedbackPlacementTests.cs` | Four edges, ties, centering, overflow, and clamping. |
| `PowerDisplay/PowerDisplayXAML/TrayWheelFeedbackWindow.xaml` | Native-looking one-line transient surface. |
| `PowerDisplay/PowerDisplayXAML/TrayWheelFeedbackWindow.xaml.cs` | TransparentWindow styles, measurement, DPI placement, show/hide, and UIA. |
| `PowerDisplay/Helpers/TrayIconService.cs` | Native-tooltip removal, hover coordinator, polling, formatter, and overlay lifecycle. |
| `PowerDisplay/PowerDisplayXAML/App.xaml.cs` | Pass nullable adjustment feedback to TrayIconService. |
| `PowerDisplay/Strings/en-us/Resources.resw` | Localized formatter templates. |

---

### Task 1: Add Pure Hover Session and Placement Logic

**Files:**
- Create: `src/modules/powerdisplay/PowerDisplay.Lib/Services/TrayWheelFeedbackSession.cs`
- Create: `src/modules/powerdisplay/PowerDisplay.Lib/Services/TrayWheelFeedbackPlacement.cs`
- Create: `src/modules/powerdisplay/PowerDisplay.Lib.UnitTests/TrayWheelFeedbackSessionTests.cs`
- Create: `src/modules/powerdisplay/PowerDisplay.Lib.UnitTests/TrayWheelFeedbackPlacementTests.cs`

**Interfaces:**
- Produces: `TrayWheelFeedbackSession.PresentationKind`.
- Produces: `TrayWheelFeedbackSession.Presentation`.
- Produces: `StartHover`, `ShowAdjustment`, `ClearAdjustment`, `Tick`, and `Stop`.
- Produces: `RectInt32 TrayWheelFeedbackPlacement.Calculate(TrayIconBounds, RectInt32 outer, RectInt32 work, int width, int height, int gap)`.
- Consumed by: TrayIconService and TrayWheelFeedbackWindow.

- [ ] **Step 1: Write failing hover-session tests**

Create `TrayWheelFeedbackSessionTests.cs`:

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Common.Services;
using Kind = PowerDisplay.Common.Services.TrayWheelFeedbackSession.PresentationKind;

namespace PowerDisplay.UnitTests;

[TestClass]
public class TrayWheelFeedbackSessionTests
{
    [TestMethod]
    public void StartHover_BeforeDelay_IsHidden()
    {
        var session = new TrayWheelFeedbackSession();

        var result = session.StartHover(1000);

        Assert.AreEqual(Kind.Hidden, result.Kind);
        Assert.AreEqual(Kind.Hidden, session.Tick(1499, pointerInside: true).Kind);
    }

    [TestMethod]
    public void Tick_AtHoverDelay_ShowsAppName()
    {
        var session = new TrayWheelFeedbackSession();
        _ = session.StartHover(1000);

        Assert.AreEqual(Kind.AppName, session.Tick(1500, pointerInside: true).Kind);
    }

    [TestMethod]
    public void RepeatedStartHover_DoesNotResetDelay()
    {
        var session = new TrayWheelFeedbackSession();
        _ = session.StartHover(1000);
        _ = session.StartHover(1300);

        Assert.AreEqual(Kind.AppName, session.Tick(1500, pointerInside: true).Kind);
    }

    [TestMethod]
    public void ShowAdjustment_IsImmediate()
    {
        var session = new TrayWheelFeedbackSession();

        var result = session.ShowAdjustment("Primary display \u00B7 55%", 1000);

        Assert.AreEqual(Kind.Adjustment, result.Kind);
        Assert.AreEqual("Primary display \u00B7 55%", result.Text);
    }

    [TestMethod]
    public void SubsequentAdjustment_ExtendsDeadline()
    {
        var session = new TrayWheelFeedbackSession();
        _ = session.ShowAdjustment("55%", 1000);
        _ = session.ShowAdjustment("60%", 2500);

        Assert.AreEqual(Kind.Adjustment, session.Tick(4499, pointerInside: true).Kind);
        Assert.AreEqual(Kind.AppName, session.Tick(4500, pointerInside: true).Kind);
    }

    [TestMethod]
    public void AdjustmentExpiryInside_ReturnsAppName()
    {
        var session = new TrayWheelFeedbackSession();
        _ = session.ShowAdjustment("55%", 1000);

        Assert.AreEqual(Kind.AppName, session.Tick(3000, pointerInside: true).Kind);
    }

    [TestMethod]
    public void PointerLeave_HidesAndClearsSession()
    {
        var session = new TrayWheelFeedbackSession();
        _ = session.ShowAdjustment("55%", 1000);

        Assert.AreEqual(Kind.Hidden, session.Tick(1100, pointerInside: false).Kind);
        Assert.IsFalse(session.IsHovering);
    }

    [TestMethod]
    public void ClearAdjustmentInside_ShowsAppNameImmediately()
    {
        var session = new TrayWheelFeedbackSession();
        _ = session.ShowAdjustment("55%", 1000);

        Assert.AreEqual(
            Kind.AppName,
            session.ClearAdjustment(1100, pointerInside: true).Kind);
    }

    [TestMethod]
    public void ClearAdjustmentOutside_Hides()
    {
        var session = new TrayWheelFeedbackSession();
        _ = session.ShowAdjustment("55%", 1000);

        Assert.AreEqual(
            Kind.Hidden,
            session.ClearAdjustment(1100, pointerInside: false).Kind);
    }

    [TestMethod]
    public void Stop_IsIdempotent()
    {
        var session = new TrayWheelFeedbackSession();
        _ = session.StartHover(1000);

        Assert.AreEqual(Kind.Hidden, session.Stop().Kind);
        Assert.AreEqual(Kind.Hidden, session.Stop().Kind);
    }

    [TestMethod]
    public void Tick_HandlesMonotonicWraparound()
    {
        var session = new TrayWheelFeedbackSession();
        _ = session.StartHover(long.MaxValue - 100);

        Assert.AreEqual(
            Kind.AppName,
            session.Tick(long.MinValue + 399, pointerInside: true).Kind);
    }
}
```

- [ ] **Step 2: Write failing placement tests**

Create `TrayWheelFeedbackPlacementTests.cs` with helpers:

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Common.Services;
using Windows.Graphics;

namespace PowerDisplay.UnitTests;

[TestClass]
public class TrayWheelFeedbackPlacementTests
{
    private static readonly RectInt32 Outer = new(0, 0, 1000, 800);
    private static readonly RectInt32 Work = new(0, 0, 1000, 760);

    [TestMethod]
    public void Calculate_BottomEdge_PositionsAboveIcon()
    {
        var result = Calculate(new TrayIconBounds(700, 760, 740, 800));

        Assert.AreEqual(new RectInt32(620, 702, 200, 50), result);
    }

    [TestMethod]
    public void Calculate_TopEdge_PositionsBelowIcon()
    {
        var result = Calculate(new TrayIconBounds(480, 0, 520, 40));

        Assert.AreEqual(new RectInt32(400, 48, 200, 50), result);
    }

    [TestMethod]
    public void Calculate_LeftEdge_PositionsRightOfIcon()
    {
        var result = Calculate(new TrayIconBounds(0, 350, 40, 390));

        Assert.AreEqual(new RectInt32(48, 345, 200, 50), result);
    }

    [TestMethod]
    public void Calculate_RightEdge_PositionsLeftOfIcon()
    {
        var result = Calculate(new TrayIconBounds(960, 350, 1000, 390));

        Assert.AreEqual(new RectInt32(752, 345, 200, 50), result);
    }

    [TestMethod]
    public void Calculate_ClampsToWorkArea()
    {
        var result = Calculate(new TrayIconBounds(0, 760, 40, 800));

        Assert.AreEqual(new RectInt32(0, 702, 200, 50), result);
    }

    [TestMethod]
    public void Calculate_OverflowIconStillUsesNearestOuterEdge()
    {
        var result = Calculate(new TrayIconBounds(800, 650, 840, 690));

        Assert.AreEqual(new RectInt32(720, 592, 200, 50), result);
    }

    [TestMethod]
    public void Calculate_TiePrefersBottom()
    {
        var squareOuter = new RectInt32(0, 0, 800, 800);
        var squareWork = new RectInt32(0, 0, 800, 760);
        var result = TrayWheelFeedbackPlacement.Calculate(
            new TrayIconBounds(380, 380, 420, 420),
            squareOuter,
            squareWork,
            200,
            50,
            8);

        Assert.AreEqual(new RectInt32(300, 322, 200, 50), result);
    }

    private static RectInt32 Calculate(TrayIconBounds icon)
        => TrayWheelFeedbackPlacement.Calculate(icon, Outer, Work, 200, 50, 8);
}
```

- [ ] **Step 3: Build to verify RED**

Build `PowerDisplay.Lib.UnitTests` x64 Debug with `/p:SolutionDir=<repo>\`.

Expected: missing session and placement type compiler errors.

- [ ] **Step 4: Implement the pure session**

Create `TrayWheelFeedbackSession.cs`:

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace PowerDisplay.Common.Services;

/// <summary>
/// Tracks ordinary-hover and adjustment-feedback presentation timing.
/// </summary>
public sealed class TrayWheelFeedbackSession
{
    /// <summary>
    /// Identifies the presentation requested by the current session state.
    /// </summary>
    public enum PresentationKind
    {
        Hidden,
        AppName,
        Adjustment,
    }

    /// <summary>
    /// Describes the presentation requested by one state transition.
    /// </summary>
    public readonly record struct Presentation(PresentationKind Kind, string? Text = null);

    private const long HoverDelayMilliseconds = 500;
    private const long AdjustmentDurationMilliseconds = 2000;

    private bool _isHovering;
    private long _hoverStartedAt;
    private string? _adjustmentText;
    private long _adjustmentStartedAt;

    /// <summary>
    /// Gets a value indicating whether a pointer hover session is active.
    /// </summary>
    public bool IsHovering => _isHovering;

    /// <summary>
    /// Starts or continues a hover session.
    /// </summary>
    public Presentation StartHover(long now)
    {
        if (!_isHovering)
        {
            _isHovering = true;
            _hoverStartedAt = now;
        }

        return Tick(now, pointerInside: true);
    }

    /// <summary>
    /// Shows adjustment text immediately and restarts its expiration deadline.
    /// </summary>
    public Presentation ShowAdjustment(string text, long now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        if (!_isHovering)
        {
            _isHovering = true;
            _hoverStartedAt = now;
        }

        _adjustmentText = text;
        _adjustmentStartedAt = now;
        return new Presentation(PresentationKind.Adjustment, text);
    }

    /// <summary>
    /// Clears adjustment text and returns the appropriate ordinary-hover state.
    /// </summary>
    public Presentation ClearAdjustment(long now, bool pointerInside)
    {
        _adjustmentText = null;
        if (!pointerInside)
        {
            return Stop();
        }

        _isHovering = true;
        _hoverStartedAt = unchecked(now - HoverDelayMilliseconds);
        return new Presentation(PresentationKind.AppName);
    }

    /// <summary>
    /// Advances presentation state for the current pointer and timestamp.
    /// </summary>
    public Presentation Tick(long now, bool pointerInside)
    {
        if (!_isHovering || !pointerInside)
        {
            return Stop();
        }

        if (_adjustmentText is not null)
        {
            if (Elapsed(now, _adjustmentStartedAt) < AdjustmentDurationMilliseconds)
            {
                return new Presentation(PresentationKind.Adjustment, _adjustmentText);
            }

            _adjustmentText = null;
            return new Presentation(PresentationKind.AppName);
        }

        return Elapsed(now, _hoverStartedAt) >= HoverDelayMilliseconds
            ? new Presentation(PresentationKind.AppName)
            : new Presentation(PresentationKind.Hidden);
    }

    /// <summary>
    /// Stops the session and requests a hidden presentation.
    /// </summary>
    public Presentation Stop()
    {
        _isHovering = false;
        _hoverStartedAt = 0;
        _adjustmentText = null;
        _adjustmentStartedAt = 0;
        return new Presentation(PresentationKind.Hidden);
    }

    private static long Elapsed(long now, long startedAt)
        => unchecked(now - startedAt);
}
```

- [ ] **Step 5: Implement pure placement**

Create `TrayWheelFeedbackPlacement.cs`:

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Windows.Graphics;

namespace PowerDisplay.Common.Services;

/// <summary>
/// Calculates a tray-feedback rectangle from physical-pixel display geometry.
/// </summary>
public static class TrayWheelFeedbackPlacement
{
    /// <summary>
    /// Positions the overlay inward from the nearest outer display edge and clamps it to work area.
    /// </summary>
    public static RectInt32 Calculate(
        TrayIconBounds icon,
        RectInt32 outer,
        RectInt32 work,
        int width,
        int height,
        int gap)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfNegative(gap);

        width = Math.Min(width, Math.Max(1, work.Width));
        height = Math.Min(height, Math.Max(1, work.Height));

        var centerX = ((long)icon.Left + icon.Right) / 2;
        var centerY = ((long)icon.Top + icon.Bottom) / 2;
        var bottomDistance = Math.Abs((long)outer.Y + outer.Height - centerY);
        var topDistance = Math.Abs(centerY - outer.Y);
        var leftDistance = Math.Abs(centerX - outer.X);
        var rightDistance = Math.Abs((long)outer.X + outer.Width - centerX);

        var edge = Edge.Bottom;
        var nearest = bottomDistance;
        if (topDistance < nearest)
        {
            edge = Edge.Top;
            nearest = topDistance;
        }

        if (leftDistance < nearest)
        {
            edge = Edge.Left;
            nearest = leftDistance;
        }

        if (rightDistance < nearest)
        {
            edge = Edge.Right;
        }

        long x;
        long y;
        switch (edge)
        {
            case Edge.Top:
                x = centerX - (width / 2);
                y = (long)icon.Bottom + gap;
                break;
            case Edge.Left:
                x = (long)icon.Right + gap;
                y = centerY - (height / 2);
                break;
            case Edge.Right:
                x = (long)icon.Left - gap - width;
                y = centerY - (height / 2);
                break;
            default:
                x = centerX - (width / 2);
                y = (long)icon.Top - gap - height;
                break;
        }

        var maxX = (long)work.X + work.Width - width;
        var maxY = (long)work.Y + work.Height - height;
        x = Math.Clamp(x, work.X, maxX);
        y = Math.Clamp(y, work.Y, maxY);
        return new RectInt32((int)x, (int)y, width, height);
    }

    private enum Edge
    {
        Bottom,
        Top,
        Left,
        Right,
    }
}
```

- [ ] **Step 6: Run GREEN and commit**

Build the test project, run focused session/placement tests, then run the complete deployed root
PowerDisplay.Lib.UnitTests DLL.

Expected: all new tests and the complete suite pass.

```powershell
git add `
    src/modules/powerdisplay/PowerDisplay.Lib/Services/TrayWheelFeedbackSession.cs `
    src/modules/powerdisplay/PowerDisplay.Lib/Services/TrayWheelFeedbackPlacement.cs `
    src/modules/powerdisplay/PowerDisplay.Lib.UnitTests/TrayWheelFeedbackSessionTests.cs `
    src/modules/powerdisplay/PowerDisplay.Lib.UnitTests/TrayWheelFeedbackPlacementTests.cs
git commit -m "Add tray feedback session and placement"
```

---

### Task 2: Add the No-Activate Feedback Window

**Files:**
- Create: `src/modules/powerdisplay/PowerDisplay/PowerDisplayXAML/TrayWheelFeedbackWindow.xaml`
- Create: `src/modules/powerdisplay/PowerDisplay/PowerDisplayXAML/TrayWheelFeedbackWindow.xaml.cs`

**Interfaces:**
- Consumes: `TrayIconBounds` and `TrayWheelFeedbackPlacement`.
- Produces: `bool ShowText(string text, TrayIconBounds iconBounds)`.
- Produces: `void HideFeedback()`.
- Provides idempotent `IDisposable`.

- [ ] **Step 1: Create the XAML surface**

Create `TrayWheelFeedbackWindow.xaml`:

```xml
<?xml version="1.0" encoding="utf-8" ?>
<common:TransparentWindow
    x:Class="PowerDisplay.PowerDisplayXAML.TrayWheelFeedbackWindow"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:common="using:Microsoft.PowerToys.Common.UI.Controls.Window">
    <common:TransparentWindow.Resources>
        <ThemeShadow x:Key="FeedbackShadow" />
    </common:TransparentWindow.Resources>
    <Grid
        x:Name="FeedbackRoot"
        Padding="8">
        <Border
            x:Name="FeedbackSurface"
            MinWidth="120"
            MaxWidth="420"
            Padding="12,8"
            Background="{ThemeResource SolidBackgroundFillColorBaseBrush}"
            BorderBrush="{ThemeResource CardStrokeColorDefaultBrush}"
            BorderThickness="1"
            CornerRadius="8"
            Shadow="{StaticResource FeedbackShadow}"
            Translation="0,0,32">
            <TextBlock
                x:Name="FeedbackText"
                VerticalAlignment="Center"
                AutomationProperties.LiveSetting="Polite"
                Foreground="{ThemeResource TextFillColorPrimaryBrush}"
                TextTrimming="CharacterEllipsis"
                TextWrapping="NoWrap" />
        </Border>
    </Grid>
</common:TransparentWindow>
```

- [ ] **Step 2: Implement no-activate, click-through window setup**

Create `TrayWheelFeedbackWindow.xaml.cs` with:

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.PowerToys.Common.UI.Controls.Window;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using PowerDisplay.Common.Services;
using Windows.Foundation;
using Windows.Graphics;
using WinUIEx;

namespace PowerDisplay.PowerDisplayXAML;

/// <summary>
/// No-activate, click-through visual host for tray hover and wheel feedback.
/// </summary>
public sealed partial class TrayWheelFeedbackWindow : TransparentWindow, IDisposable
{
    private const int GwlExStyle = -20;
    private const int GwlWndProc = -4;
    private const int WsExNoActivate = 0x08000000;
    private const int WsExTransparent = 0x00000020;
    private const uint WmNcHitTest = 0x0084;
    private static readonly nint HtTransparent = -1;

    private readonly nint _hwnd;
    private nint _originalWndProc;
    private WndProcDelegate? _wndProcDelegate;
    private string? _currentText;
    private TrayIconBounds? _currentIconBounds;
    private bool _isVisible;
    private bool _disposed;

    public TrayWheelFeedbackWindow()
    {
        InitializeComponent();
        IsAlwaysOnTop = true;
        _hwnd = this.GetWindowHandle();
        ApplyExtendedStyles();

        _wndProcDelegate = WndProc;
        Marshal.SetLastPInvokeError(0);
        _originalWndProc = SetWindowLongPtrNative(
            _hwnd,
            GwlWndProc,
            Marshal.GetFunctionPointerForDelegate(_wndProcDelegate));
        var error = Marshal.GetLastPInvokeError();
        if (_originalWndProc == 0 && error != 0)
        {
            throw new Win32Exception(error);
        }

        Closed += (_, _) => Dispose();
    }

    private delegate nint WndProcDelegate(
        nint hwnd,
        uint message,
        nuint wParam,
        nint lParam);

    private nint WndProc(
        nint hwnd,
        uint message,
        nuint wParam,
        nint lParam)
    {
        if (message == WmNcHitTest)
        {
            return HtTransparent;
        }

        return CallWindowProcNative(
            _originalWndProc,
            hwnd,
            message,
            wParam,
            lParam);
    }

    private void ApplyExtendedStyles()
    {
        var current = GetWindowLongPtrNative(_hwnd, GwlExStyle);
        _ = SetWindowLongPtrNative(
            _hwnd,
            GwlExStyle,
            current | WsExNoActivate | WsExTransparent);
    }
```

- [ ] **Step 3: Implement measurement, placement, show, and accessibility**

Continue the class:

```csharp
    /// <summary>
    /// Shows or updates feedback text at the supplied notification-icon rectangle.
    /// </summary>
    public bool ShowText(string text, TrayIconBounds iconBounds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        var center = new PointInt32(
            iconBounds.Left + ((iconBounds.Right - iconBounds.Left) / 2),
            iconBounds.Top + ((iconBounds.Bottom - iconBounds.Top) / 2));
        var displayArea = DisplayArea.GetFromPoint(center, DisplayAreaFallback.Nearest);
        if (displayArea is null)
        {
            return false;
        }

        var textChanged = !string.Equals(_currentText, text, StringComparison.Ordinal);
        if (_isVisible &&
            !textChanged &&
            _currentIconBounds == iconBounds)
        {
            return true;
        }

        FeedbackText.Text = text;
        FeedbackRoot.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

        var widthDip = Math.Clamp(
            (int)Math.Ceiling(FeedbackRoot.DesiredSize.Width),
            120,
            436);
        var heightDip = Math.Max(
            1,
            (int)Math.Ceiling(FeedbackRoot.DesiredSize.Height));
        var dpiScale = FlyoutWindowHelper.GetDpiScale(displayArea);
        var width = FlyoutWindowHelper.ScaleToPhysicalPixels(widthDip, dpiScale);
        var height = FlyoutWindowHelper.ScaleToPhysicalPixels(heightDip, dpiScale);
        var gap = FlyoutWindowHelper.ScaleToPhysicalPixels(8, dpiScale);
        var rect = TrayWheelFeedbackPlacement.Calculate(
            iconBounds,
            displayArea.OuterBounds,
            displayArea.WorkArea,
            width,
            height,
            gap);

        FlyoutWindowHelper.MoveAndResizeOnDisplay(this, displayArea, rect);
        if (!_isVisible)
        {
            Show();
        }

        _currentText = text;
        _currentIconBounds = iconBounds;
        _isVisible = true;

        if (textChanged)
        {
            DispatcherQueue.TryEnqueue(() => Announce(text));
        }

        return true;
    }

    /// <summary>
    /// Hides feedback without closing the reusable window.
    /// </summary>
    public void HideFeedback()
    {
        _currentText = null;
        _currentIconBounds = null;
        if (_isVisible)
        {
            _isVisible = false;
            Hide();
        }
    }

    private void Announce(string text)
    {
        var peer = FrameworkElementAutomationPeer.FromElement(FeedbackText) ??
            FrameworkElementAutomationPeer.CreatePeerForElement(FeedbackText);
        peer?.RaiseNotificationEvent(
            AutomationNotificationKind.Other,
            AutomationNotificationProcessing.MostRecent,
            text,
            "PowerDisplayTrayFeedback");
    }
```

- [ ] **Step 4: Add disposal and P/Invokes**

Finish:

```csharp
    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_originalWndProc != 0 && IsWindowNative(_hwnd))
        {
            _ = SetWindowLongPtrNative(_hwnd, GwlWndProc, _originalWndProc);
            _originalWndProc = 0;
        }

        _wndProcDelegate = null;
        GC.SuppressFinalize(this);
    }

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static partial nint GetWindowLongPtrNative(nint hwnd, int index);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static partial nint SetWindowLongPtrNative(
        nint hwnd,
        int index,
        nint newValue);

    [LibraryImport("user32.dll", EntryPoint = "CallWindowProcW")]
    private static partial nint CallWindowProcNative(
        nint previous,
        nint hwnd,
        uint message,
        nuint wParam,
        nint lParam);

    [LibraryImport("user32.dll", EntryPoint = "IsWindow")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsWindowNative(nint hwnd);
}
```

- [ ] **Step 5: Format XAML and build**

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

Expected: XAML Styler accepts the file and x64 PowerDisplay builds.

- [ ] **Step 6: Commit the overlay window**

```powershell
git add `
    src/modules/powerdisplay/PowerDisplay/PowerDisplayXAML/TrayWheelFeedbackWindow.xaml `
    src/modules/powerdisplay/PowerDisplay/PowerDisplayXAML/TrayWheelFeedbackWindow.xaml.cs
git commit -m "Add tray wheel feedback overlay window"
```

---

### Task 3: Integrate Hover and Wheel Feedback

**Files:**
- Modify: `src/modules/powerdisplay/PowerDisplay/Helpers/TrayIconService.cs`
- Modify: `src/modules/powerdisplay/PowerDisplay/PowerDisplayXAML/App.xaml.cs`
- Modify: `src/modules/powerdisplay/PowerDisplay/Strings/en-us/Resources.resw`

**Interfaces:**
- Consumes: formatter, session, placement window, current notification bounds.
- Produces: custom ordinary-hover and adjustment overlay.
- Removes: native notification tooltip registration.

- [ ] **Step 1: Add localized formatter resources**

Add the six resources from the superseded native-tooltip plan:

```xml
  <data name="TrayWheelFeedbackPrimaryFormat" xml:space="preserve">
    <value>Primary display · {0}</value>
    <comment>{0} is the formatted brightness value, for example "55%".</comment>
  </data>
  <data name="TrayWheelFeedbackPrimaryPluralFormat" xml:space="preserve">
    <value>Primary displays · {0}</value>
    <comment>{0} is a list or range of brightness values, for example "35%, 70%".</comment>
  </data>
  <data name="TrayWheelFeedbackAllFormat" xml:space="preserve">
    <value>All displays · {0}</value>
    <comment>{0} is a list or range of brightness values, for example "35%, 70%".</comment>
  </data>
  <data name="TrayWheelFeedbackPercentageFormat" xml:space="preserve">
    <value>{0}%</value>
    <comment>{0} is an integer brightness percentage from 0 through 100.</comment>
  </data>
  <data name="TrayWheelFeedbackRangeFormat" xml:space="preserve">
    <value>{0}–{1} ({2} displays)</value>
    <comment>{0} is the minimum formatted percentage, {1} is the maximum formatted percentage, and {2} is the physical display count.</comment>
  </data>
  <data name="TrayWheelFeedbackListSeparator" xml:space="preserve">
    <value>, </value>
    <comment>Separator between brightness percentages in tray feedback.</comment>
  </data>
```

- [ ] **Step 2: Remove native Shell tooltip registration**

In `EnsureTrayIconIdentity`, change:

```csharp
uFlags = NOTIFY_ICON_DATA_FLAGS.NIF_MESSAGE | NOTIFY_ICON_DATA_FLAGS.NIF_ICON,
```

Remove the `szTip` assignment. Do not add any `NIM_MODIFY` tooltip path.

- [ ] **Step 3: Add coordinator state**

Add:

```csharp
private static readonly TimeSpan FeedbackPollInterval = TimeSpan.FromMilliseconds(100);

private readonly TrayWheelFeedbackSession _feedbackSession = new();

private DispatcherQueueTimer? _feedbackPollTimer;
private TrayWheelFeedbackWindow? _feedbackWindow;
private TrayIconBounds? _feedbackIconBounds;
private bool _feedbackWindowFailureLogged;
```

- [ ] **Step 4: Add non-destructive bounds query**

Extract the native query into:

```csharp
private unsafe bool TryQueryTrayIconBounds(out TrayIconBounds bounds)
{
    bounds = default;
    if (!_isTrayIconRegistered || _hwnd == 0 || _trayIconData is null)
    {
        return false;
    }

    var identifier = new NotifyIconIdentifier
    {
        CbSize = (uint)sizeof(NotifyIconIdentifier),
        HWnd = _hwnd,
        Id = MyNotifyId,
        GuidItem = Guid.Empty,
    };
    var result = ShellNotifyIconGetRectNative(ref identifier, out var rect);
    bounds = new TrayIconBounds(rect.Left, rect.Top, rect.Right, rect.Bottom);
    return result >= 0 && bounds.IsValid;
}
```

Make `TryGetCurrentIconBounds` call this helper. On failure, it retains the existing registration
stale/recovery behavior. The 100 ms feedback poll calls only the non-destructive helper.

- [ ] **Step 5: Refactor tray mouse move**

At the start of `HandleTrayMouseMove`, resolve cursor and current bounds for every mode. On success:

```csharp
var now = Environment.TickCount64;
_feedbackIconBounds = bounds;
ApplyFeedbackPresentation(_feedbackSession.StartHover(now), bounds);
EnsureFeedbackPollTimer();
```

Only after starting hover, branch:

```csharp
if (_mouseWheelControlMode == MouseWheelControlMode.Disabled)
{
    return;
}
```

Then preserve the existing wheel-listener cache/generation/arm logic using the already-resolved
`cursor`, `bounds`, and `now`. Repeated icon mouse moves do not reset session hover timing.

- [ ] **Step 6: Add polling and presentation**

Add:

```csharp
private void EnsureFeedbackPollTimer()
{
    _feedbackPollTimer ??= _dispatcherQueue.CreateTimer();
    _feedbackPollTimer.Interval = FeedbackPollInterval;
    _feedbackPollTimer.IsRepeating = true;
    _feedbackPollTimer.Tick -= OnFeedbackPollTimerTick;
    _feedbackPollTimer.Tick += OnFeedbackPollTimerTick;
    if (!_feedbackPollTimer.IsRunning)
    {
        _feedbackPollTimer.Start();
    }
}

private void OnFeedbackPollTimerTick(DispatcherQueueTimer sender, object args)
{
    if (!GetCursorPos(out var cursor) ||
        !TryQueryTrayIconBounds(out var bounds) ||
        !bounds.Contains(cursor.X, cursor.Y))
    {
        StopHoverFeedback();
        return;
    }

    _feedbackIconBounds = bounds;
    ApplyFeedbackPresentation(
        _feedbackSession.Tick(Environment.TickCount64, pointerInside: true),
        bounds);
}

private void ApplyFeedbackPresentation(
    TrayWheelFeedbackSession.Presentation presentation,
    TrayIconBounds bounds)
{
    switch (presentation.Kind)
    {
        case TrayWheelFeedbackSession.PresentationKind.AppName:
            ShowFeedbackOverlay(GetString("AppName"), bounds);
            break;
        case TrayWheelFeedbackSession.PresentationKind.Adjustment:
            if (!string.IsNullOrEmpty(presentation.Text))
            {
                ShowFeedbackOverlay(presentation.Text, bounds);
            }

            break;
        default:
            _feedbackWindow?.HideFeedback();
            break;
    }
}

private void ShowFeedbackOverlay(string text, TrayIconBounds bounds)
{
    if (_feedbackWindowFailureLogged && _feedbackWindow is null)
    {
        return;
    }

    try
    {
        _feedbackWindow ??= new TrayWheelFeedbackWindow();
        if (_feedbackWindow.ShowText(text, bounds))
        {
            _feedbackWindowFailureLogged = false;
        }
        else
        {
            _feedbackWindow.HideFeedback();
        }
    }
    catch (Exception ex) when (
        ex is System.ComponentModel.Win32Exception or
        System.Runtime.InteropServices.COMException or
        InvalidOperationException)
    {
        _feedbackWindow?.HideFeedback();
        if (!_feedbackWindowFailureLogged)
        {
            Logger.LogWarning($"[TrayFeedback] Unable to show overlay: {ex.Message}");
            _feedbackWindowFailureLogged = true;
        }
    }
}
```

The catch is confined to the optional overlay boundary and always logs the failure once.

- [ ] **Step 7: Connect nullable adjustment feedback**

Add:

```csharp
internal void UpdateAdjustmentFeedback(TrayWheelAdjustmentFeedback? feedback)
{
    var now = Environment.TickCount64;
    var pointerInside =
        GetCursorPos(out var cursor) &&
        TryQueryTrayIconBounds(out var bounds) &&
        bounds.Contains(cursor.X, cursor.Y);
    if (pointerInside)
    {
        _feedbackIconBounds = bounds;
    }

    if (!pointerInside)
    {
        StopHoverFeedback();
        return;
    }

    if (feedback is null)
    {
        var presentation = _feedbackSession.ClearAdjustment(now, pointerInside: true);
        if (_feedbackIconBounds is TrayIconBounds currentBounds)
        {
            ApplyFeedbackPresentation(presentation, currentBounds);
        }
        else
        {
            _feedbackWindow?.HideFeedback();
        }

        EnsureFeedbackPollTimer();
        return;
    }

    var templates = new TrayWheelFeedbackTemplates(
        ResourceLoaderInstance.ResourceLoader.GetString("TrayWheelFeedbackPrimaryFormat"),
        ResourceLoaderInstance.ResourceLoader.GetString("TrayWheelFeedbackPrimaryPluralFormat"),
        ResourceLoaderInstance.ResourceLoader.GetString("TrayWheelFeedbackAllFormat"),
        ResourceLoaderInstance.ResourceLoader.GetString("TrayWheelFeedbackPercentageFormat"),
        ResourceLoaderInstance.ResourceLoader.GetString("TrayWheelFeedbackRangeFormat"),
        ResourceLoaderInstance.ResourceLoader.GetString("TrayWheelFeedbackListSeparator"));
    var text = TrayWheelFeedbackFormatter.Format(
        feedback,
        templates,
        CultureInfo.CurrentCulture);
    if (text is null || !pointerInside || !_feedbackIconBounds.HasValue)
    {
        StopHoverFeedback();
        return;
    }

    ApplyFeedbackPresentation(
        _feedbackSession.ShowAdjustment(text, now),
        _feedbackIconBounds.Value);
    EnsureFeedbackPollTimer();
}
```

Add `using System.Globalization;` and the overlay namespace.

Replace the App discard lambda with:

```csharp
_trayIconService.MouseWheelScrolled += notches =>
{
    var feedback = mainWindow.ViewModel.AdjustBrightnessFromTrayWheel(notches);
    _trayIconService.UpdateAdjustmentFeedback(feedback);
};
```

- [ ] **Step 8: Add unified cleanup**

Add:

```csharp
private void StopHoverFeedback()
{
    _feedbackPollTimer?.Stop();
    _feedbackSession.Stop();
    _feedbackIconBounds = null;
    _feedbackWindow?.HideFeedback();
}

private void DisposeFeedbackWindow()
{
    StopHoverFeedback();
    if (_feedbackWindow is not null)
    {
        _feedbackWindow.Dispose();
        _feedbackWindow.Close();
        _feedbackWindow = null;
    }
}
```

Call `StopHoverFeedback()` when registration is marked stale. Call `DisposeFeedbackWindow()` from
`Destroy()`.

- [ ] **Step 9: Format, build, and commit**

Run XAML Styler, full deployed PowerDisplay.Lib.UnitTests, and PowerDisplay x64 Debug build.

Then:

```powershell
git add `
    src/modules/powerdisplay/PowerDisplay/Helpers/TrayIconService.cs `
    src/modules/powerdisplay/PowerDisplay/PowerDisplayXAML/App.xaml.cs `
    src/modules/powerdisplay/PowerDisplay/Strings/en-us/Resources.resw
git commit -m "Integrate tray feedback overlay"
```

---

### Task 4: Verify Overlay Behavior and Update PR

**Files:**
- Verify current branch and ignored runtime reports.

**Interfaces:**
- Verifies all overlay and parent tray-wheel contracts.

- [ ] **Step 1: Run complete automated validation**

Run full PowerDisplay.Lib.UnitTests from the deployed root DLL, then build PowerDisplay x64 and
ARM64 Debug (ARM64 essentials first).

- [ ] **Step 2: Run current-branch runtime verification**

Use persisted feature/global settings backups and verify every runtime item from the design:

- no native tooltip
- 500 ms ordinary hover
- leave hide
- Disabled hover without wheel adjustment
- immediate Primary/All feedback
- +60/+60 complete-notch behavior
- two-second restore
- no foreground or Alt+Tab entry
- click-through
- bottom/overflow placement
- hide/re-enable and Explorer restart cleanup
- existing click/menu, targeting, brightness, and registration recovery

Record PASS/FAIL/BLOCKED with exact evidence and restore all state.

- [ ] **Step 3: Final branch review**

Run:

```powershell
git --no-pager diff --check origin/main...HEAD
git --no-pager diff --stat origin/main...HEAD
git status --short
```

Dispatch a whole-branch review using the approved overlay design and the complete diff.

- [ ] **Step 4: Push and update PR #49446**

Push the same branch:

```powershell
git push origin yuleng/worktree/issue-49410-design
```

Update PR #49446's summary and validation section with the overlay behavior, new test counts,
cross-architecture builds, and runtime verdicts. Do not create another PR.

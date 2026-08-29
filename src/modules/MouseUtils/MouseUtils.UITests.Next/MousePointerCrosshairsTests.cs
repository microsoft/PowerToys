// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static MouseUtils.UITests.MouseUtilsTestHelper;

namespace MouseUtils.UITests;

[TestClass]
public class MousePointerCrosshairsTests : UITestBase
{
    private const string ModuleName = "MousePointerCrosshairs";
    private const string ToggleId = "MouseUtils_MousePointerCrosshairsToggleId";
    private const string WindowClass = "MousePointerCrosshairs";
    private const string CrosshairsColor = "#FF0000";

    private static readonly IDisposable ModuleSettings = SettingsConfigHelper.PreserveModuleSettings(ModuleName);

    static MousePointerCrosshairsTests()
    {
    }

    public MousePointerCrosshairsTests()
        : base(PowerToysModule.PowerToysSettings, enableModules: new[] { ModuleName })
    {
    }

    protected override void PrepareTestState()
    {
        var configuration = TestContext.TestName switch
        {
            nameof(DisabledModuleRejectsActivationAndChangedShortcutWorks) => new CrosshairsConfiguration(ShortcutCode: (int)Key.O),
            nameof(HorizontalOrientationFixedLengthAndBorderAreApplied) => new CrosshairsConfiguration(
                Color: "#00FF00",
                Radius: 35,
                Thickness: 12,
                BorderColor: "#0000FF",
                BorderSize: 6,
                Orientation: 2,
                FixedLengthEnabled: true,
                FixedLength: 120),
            nameof(OpacityBlendsWithDesktop) => new CrosshairsConfiguration(Opacity: 50),
            nameof(AutoActivateStartsVisible) => new CrosshairsConfiguration(AutoActivate: true),
            _ => new CrosshairsConfiguration(),
        };

        MouseUtilsTestHelper.ReplaceModuleSettings(ModuleName, CreateSettings(configuration));
    }

    [ClassCleanup]
    public static void RestoreModuleSettings() => ModuleSettings.Dispose();

    [TestCleanup]
    public async Task CleanupInput()
    {
        await CaptureFailureArtifactsBeforeCleanupAsync();
        MouseHelper.LeftUp();
        MouseHelper.RightUp();
        KeyboardHelper.ReleaseKey(Key.Ctrl);
        KeyboardHelper.ReleaseKey(Key.Shift);
        KeyboardHelper.ReleaseKey(Key.Alt);
        KeyboardHelper.ReleaseKey(Key.LWin);
    }

    [TestMethod]
    [TestCategory("MouseUtils")]
    [TestCategory("Mouse Utils #29")]
    [TestCategory("Mouse Utils #30")]
    public void ActivationTracksCursorAndHides()
    {
        MouseUtilsTestHelper.NavigateToMouseUtilities(this);
        MouseUtilsTestHelper.SetModuleEnabled(this, ToggleId, true);
        Key[] activationKeys = [Key.LWin, Key.Alt, Key.P];

        var crosshairsWindow = MouseUtilsTestHelper.WaitForWindowClass(WindowClass);
        Assert.IsFalse(crosshairsWindow.IsVisible, "Crosshairs should start hidden before activation.");

        WindowHelper.MinimizeWindow(new IntPtr(Session.WindowHandle));
        var (centerX, centerY) = WindowHelper.GetScreenCenter();
        MouseHelper.MoveTo(centerX, centerY);

        MouseUtilsTestHelper.Step(this, "Signaling the Crosshairs named event as a positive detector control");
        using (var detectorControl = new WindowShowWatcher(WindowClass, crosshairsWindow.Hwnd.ToInt64()))
        {
            Assert.IsTrue(
                NamedEventHelper.WaitAndSignal(NamedEventHelper.MouseCrosshairsToggle, 10_000),
                "The Crosshairs named event was not created by the enabled module.");
            Assert.IsTrue(
                detectorControl.Wait(5_000),
                "The known-good named event did not produce a Crosshairs SHOW event.");
            AssertCrosshairsAt(centerX, centerY);

            Assert.IsTrue(
                NamedEventHelper.TrySignal(NamedEventHelper.MouseCrosshairsToggle),
                "The Crosshairs named event disappeared before the positive control could hide the overlay.");
            Assert.IsTrue(
                detectorControl.WaitForHidden(5_000),
                $"The positive-control Crosshairs HWND did not hide. Events: {string.Join(", ", detectorControl.Events)}");
        }

        using var watcher = new WindowShowWatcher(WindowClass, crosshairsWindow.Hwnd.ToInt64());
        MouseUtilsTestHelper.Step(this, "Sending the Crosshairs shortcut after the module-ready positive control");
        KeyboardHelper.SendKeys(activationKeys);
        var shown = watcher.Wait(10_000);

        MouseUtilsTestHelper.Step(this, $"Crosshairs window events: {string.Join(", ", watcher.Events)}");
        Assert.IsTrue(shown, "Crosshairs did not emit a SHOW event after the activation shortcut.");
        AssertCrosshairsAt(centerX, centerY);

        MouseUtilsTestHelper.Step(this, "Moving the cursor with relative input and checking the new crosshair origin");
        MouseHelper.MoveBy(160, 100, steps: 10);
        var moved = MouseHelper.GetMousePosition();
        Assert.IsTrue(
            Math.Abs(moved.X - centerX) > 50 || Math.Abs(moved.Y - centerY) > 50,
            $"Relative input did not move the cursor far enough: ({centerX},{centerY}) -> ({moved.X},{moved.Y}).");
        var movedOrigin = AssertCrosshairsNear(moved.X, moved.Y);
        Assert.IsTrue(
            Math.Abs(movedOrigin.X - centerX) > 50 || Math.Abs(movedOrigin.Y - centerY) > 50,
            $"Crosshairs did not follow the cursor away from ({centerX},{centerY}); observed origin ({movedOrigin.X},{movedOrigin.Y}).");

        MouseUtilsTestHelper.Step(this, "Sending the shortcut again and waiting for the exact Crosshairs HWND to hide");
        KeyboardHelper.SendKeys(activationKeys);
        Assert.IsTrue(
            watcher.WaitForHidden(5_000),
            $"Crosshairs HWND 0x{crosshairsWindow.Hwnd:X} did not hide. Events: {string.Join(", ", watcher.Events)}");
        Assert.IsFalse(WindowControl.IsAnyWindowOfClassVisible(WindowClass), "Crosshairs remained visible after the second shortcut.");
    }

    [TestMethod]
    [TestCategory("MouseUtils")]
    [TestCategory("Mouse Utils #31")]
    [TestCategory("Mouse Utils #32")]
    public void DisabledModuleRejectsActivationAndChangedShortcutWorks()
    {
        MouseUtilsTestHelper.NavigateToMouseUtilities(this);
        MouseUtilsTestHelper.SetModuleEnabled(this, ToggleId, false);
        Assert.IsTrue(
            NamedEventHelper.WaitUntilUnavailable(NamedEventHelper.MouseCrosshairsToggle),
            "Crosshairs trigger event remained available after disabling the module.");

        using (var disabledWatcher = new WindowShowWatcher(WindowClass))
        {
            KeyboardHelper.SendKeys(Key.LWin, Key.Alt, Key.O);
            Assert.IsFalse(disabledWatcher.Wait(1_500), "The changed shortcut showed Crosshairs while the module was disabled.");
        }

        MouseUtilsTestHelper.SetModuleEnabled(this, ToggleId, true);
        Assert.IsTrue(
            NamedEventHelper.WaitUntilAvailable(NamedEventHelper.MouseCrosshairsToggle),
            "Crosshairs trigger event was not recreated after enabling the module.");
        var window = MouseUtilsTestHelper.WaitForWindowClass(WindowClass);
        using var enabledWatcher = new WindowShowWatcher(WindowClass, window.Hwnd.ToInt64());
        KeyboardHelper.SendKeys(Key.LWin, Key.Alt, Key.O);
        Assert.IsTrue(enabledWatcher.Wait(5_000), "The changed Win+Alt+O shortcut did not show Crosshairs.");
    }

    [TestMethod]
    [TestCategory("MouseUtils")]
    [TestCategory("Mouse Utils #33")]
    public void HorizontalOrientationFixedLengthAndBorderAreApplied()
    {
        WindowHelper.MinimizeWindow(new IntPtr(Session.WindowHandle));
        var (centerX, centerY) = WindowHelper.GetScreenCenter();
        MouseHelper.MoveTo(centerX, centerY);
        Assert.IsTrue(NamedEventHelper.WaitAndSignal(NamedEventHelper.MouseCrosshairsToggle), "Crosshairs trigger event was unavailable.");

        // Radius 35 leaves the center gap; 12px core plus 6px border determines the Y probes, and
        // fixed length 120 from the gap determines the arm-end probes around X=150.
        var result = WaitHelper.WaitForStable(
            () => new
            {
                Gap = WindowHelper.GetPixelColorHex(centerX + 25, centerY),
                BorderStart = WindowHelper.GetPixelColorHex(centerX + 31, centerY),
                Core = WindowHelper.GetPixelColorHex(centerX + 45, centerY),
                CoreThickness = WindowHelper.GetPixelColorHex(centerX + 60, centerY + 5),
                BorderThickness = WindowHelper.GetPixelColorHex(centerX + 60, centerY + 9),
                OutsideThickness = WindowHelper.GetPixelColorHex(centerX + 60, centerY + 13),
                CoreEnd = WindowHelper.GetPixelColorHex(centerX + 150, centerY),
                BorderEnd = WindowHelper.GetPixelColorHex(centerX + 158, centerY),
                Vertical = WindowHelper.GetPixelColorHex(centerX, centerY - 60),
                Beyond = WindowHelper.GetPixelColorHex(centerX + 165, centerY),
            },
            sample => sample is not null &&
                !sample.Gap.Equals("#00FF00", StringComparison.OrdinalIgnoreCase) &&
                sample.BorderStart.Equals("#0000FF", StringComparison.OrdinalIgnoreCase) &&
                sample.Core.Equals("#00FF00", StringComparison.OrdinalIgnoreCase) &&
                sample.CoreThickness.Equals("#00FF00", StringComparison.OrdinalIgnoreCase) &&
                sample.BorderThickness.Equals("#0000FF", StringComparison.OrdinalIgnoreCase) &&
                !sample.OutsideThickness.Equals("#00FF00", StringComparison.OrdinalIgnoreCase) &&
                !sample.OutsideThickness.Equals("#0000FF", StringComparison.OrdinalIgnoreCase) &&
                sample.CoreEnd.Equals("#00FF00", StringComparison.OrdinalIgnoreCase) &&
                sample.BorderEnd.Equals("#0000FF", StringComparison.OrdinalIgnoreCase) &&
                !sample.Vertical.Equals("#00FF00", StringComparison.OrdinalIgnoreCase) &&
                !sample.Vertical.Equals("#0000FF", StringComparison.OrdinalIgnoreCase) &&
                !sample.Beyond.Equals("#00FF00", StringComparison.OrdinalIgnoreCase) &&
                !sample.Beyond.Equals("#0000FF", StringComparison.OrdinalIgnoreCase),
            timeoutMS: 5_000,
            pollIntervalMS: 100);

        Assert.IsTrue(result.Succeeded, $"Horizontal fixed-length Crosshairs geometry did not match. Last sample: {result.LastObservation}.");
    }

    [TestMethod]
    [TestCategory("MouseUtils")]
    public void OpacityBlendsWithDesktop()
    {
        WindowHelper.MinimizeWindow(new IntPtr(Session.WindowHandle));
        var (centerX, centerY) = WindowHelper.GetScreenCenter();
        MouseHelper.MoveTo(centerX, centerY);
        var probeX = centerX + 60;
        Color? previous = null;
        var baseline = WaitHelper.WaitForStable(
            () => WindowHelper.GetPixelColor(probeX, centerY),
            color =>
            {
                var matchesPrevious = previous.HasValue && color.ToArgb() == previous.Value.ToArgb();
                previous = color;
                return matchesPrevious;
            },
            2_000,
            requiredConsecutiveMatches: 4,
            pollIntervalMS: 100).LastObservation;

        Assert.IsTrue(NamedEventHelper.WaitAndSignal(NamedEventHelper.MouseCrosshairsToggle), "Crosshairs trigger event was unavailable.");
        var expected = Blend(Color.Red, baseline, 128);
        var result = WaitHelper.WaitForStable(
            () => WindowHelper.GetPixelColor(probeX, centerY),
            color => IsNear(color, expected, 5),
            5_000,
            requiredConsecutiveMatches: 3,
            pollIntervalMS: 100);

        Assert.IsTrue(result.Succeeded, $"50% Crosshairs opacity did not blend as expected. Expected {expected}; observed {result.LastObservation}.");
    }

    [TestMethod]
    [TestCategory("MouseUtils")]
    public void AutoActivateStartsVisible()
    {
        var result = WaitHelper.WaitForStable(
            () => WindowControl.IsAnyWindowOfClassVisible(WindowClass),
            visible => visible,
            timeoutMS: 10_000,
            requiredConsecutiveMatches: 3,
            pollIntervalMS: 100);
        Assert.IsTrue(result.Succeeded, "Crosshairs did not start visible when auto-activate was enabled.");
    }

    [TestMethod]
    [TestCategory("MouseUtils")]
    public void GlidingCursorMovesAndEscapeCancels()
    {
        WindowHelper.MinimizeWindow(new IntPtr(Session.WindowHandle));
        var (centerX, centerY) = WindowHelper.GetScreenCenter();
        MouseHelper.MoveTo(centerX, centerY);
        var window = MouseUtilsTestHelper.WaitForWindowClass(WindowClass);
        using var watcher = new WindowShowWatcher(WindowClass, window.Hwnd.ToInt64());

        KeyboardHelper.PressKey(Key.LWin);
        KeyboardHelper.PressKey(Key.Alt);
        try
        {
            KeyboardHelper.SendKey(Key.OemPeriod);
        }
        finally
        {
            KeyboardHelper.ReleaseKey(Key.Alt);
            KeyboardHelper.ReleaseKey(Key.LWin);
        }

        Assert.IsTrue(watcher.Wait(5_000), "The gliding-cursor shortcut did not show Crosshairs.");
        var reset = WaitHelper.WaitForStable(
            MouseHelper.GetMousePosition,
            position => position.X < centerX - 100,
            5_000,
            requiredConsecutiveMatches: 1,
            pollIntervalMS: 20);
        Assert.IsTrue(reset.Succeeded, $"Gliding cursor did not reset to the left side. Last position: {reset.LastObservation}.");
        var resetPosition = reset.LastObservation;
        var moved = WaitHelper.WaitForStable(
            MouseHelper.GetMousePosition,
            position => position.X >= resetPosition.X + 100,
            5_000,
            requiredConsecutiveMatches: 2,
            pollIntervalMS: 20);
        Assert.IsTrue(moved.Succeeded, $"Gliding cursor did not travel at least 100px after reset. Reset: {resetPosition}; last: {moved.LastObservation}.");

        KeyboardHelper.SendKeys(Key.Esc);
        Assert.IsTrue(watcher.WaitForHidden(5_000), "Escape did not hide the gliding-cursor Crosshairs.");
        var stoppedAt = MouseHelper.GetMousePosition();
        var stopped = WaitHelper.WaitForStable(
            MouseHelper.GetMousePosition,
            position => Math.Abs(position.X - stoppedAt.X) <= 2 && Math.Abs(position.Y - stoppedAt.Y) <= 2,
            2_000,
            requiredConsecutiveMatches: 5,
            pollIntervalMS: 100);
        Assert.IsTrue(stopped.Succeeded, "The cursor continued gliding after Escape.");
    }

    private static void AssertCrosshairsAt(int cursorX, int cursorY)
    {
        var result = WaitHelper.WaitForStable(
            () => new[]
            {
                WindowHelper.GetPixelColorHex(cursorX - 60, cursorY),
                WindowHelper.GetPixelColorHex(cursorX + 60, cursorY),
                WindowHelper.GetPixelColorHex(cursorX, cursorY - 60),
                WindowHelper.GetPixelColorHex(cursorX, cursorY + 60),
            },
            colors => colors is not null && colors.All(color => color.Equals(CrosshairsColor, StringComparison.OrdinalIgnoreCase)),
            timeoutMS: 5_000,
            requiredConsecutiveMatches: 3,
            pollIntervalMS: 100);

        Assert.IsTrue(
            result.Succeeded,
            $"Expected red Crosshairs arms around ({cursorX},{cursorY}); last samples: {string.Join(", ", result.LastObservation ?? Array.Empty<string>())}");
    }

    private static CrosshairsOrigin AssertCrosshairsNear(int cursorX, int cursorY)
    {
        const int searchRadius = 80;
        const int maximumLag = 64;
        var result = WaitHelper.WaitForStable(
            () => FindCrosshairsOriginNear(cursorX, cursorY, searchRadius),
            origin => origin is not null &&
                Math.Abs(origin.X - cursorX) <= maximumLag &&
                Math.Abs(origin.Y - cursorY) <= maximumLag,
            timeoutMS: 5_000,
            requiredConsecutiveMatches: 1,
            pollIntervalMS: 100);

        Assert.IsTrue(
            result.Succeeded,
            $"Expected Crosshairs within {maximumLag}px of ({cursorX},{cursorY}); last observed origin: {result.LastObservation}.");
        return result.LastObservation!;
    }

    private static CrosshairsOrigin? FindCrosshairsOriginNear(int cursorX, int cursorY, int searchRadius)
    {
        var verticalProbeY = cursorY - searchRadius;
        var horizontalProbeX = cursorX - searchRadius;
        var verticalPixels = new List<int>();
        var horizontalPixels = new List<int>();

        for (var offset = -searchRadius; offset <= searchRadius; offset += 2)
        {
            if (WindowHelper.GetPixelColorHex(cursorX + offset, verticalProbeY).Equals(CrosshairsColor, StringComparison.OrdinalIgnoreCase))
            {
                verticalPixels.Add(cursorX + offset);
            }

            if (WindowHelper.GetPixelColorHex(horizontalProbeX, cursorY + offset).Equals(CrosshairsColor, StringComparison.OrdinalIgnoreCase))
            {
                horizontalPixels.Add(cursorY + offset);
            }
        }

        return verticalPixels.Count > 0 && horizontalPixels.Count > 0
            ? new CrosshairsOrigin((int)verticalPixels.Average(), (int)horizontalPixels.Average())
            : null;
    }

    private static string CreateSettings(CrosshairsConfiguration configuration) => $$"""
                {
                    "name": "MousePointerCrosshairs",
                    "version": "1.0",
                    "properties": {
                        "activation_shortcut": { "win": true, "ctrl": false, "alt": true, "shift": false, "code": {{configuration.ShortcutCode}}, "key": "" },
                        "gliding_cursor_activation_shortcut": { "win": true, "ctrl": false, "alt": true, "shift": false, "code": 190, "key": "" },
                        "crosshairs_color": { "value": "{{configuration.Color}}" },
                        "crosshairs_opacity": { "value": {{configuration.Opacity}} },
                        "crosshairs_radius": { "value": {{configuration.Radius}} },
                        "crosshairs_thickness": { "value": {{configuration.Thickness}} },
                        "crosshairs_border_color": { "value": "{{configuration.BorderColor}}" },
                        "crosshairs_border_size": { "value": {{configuration.BorderSize}} },
                        "crosshairs_orientation": { "value": {{configuration.Orientation}} },
                        "crosshairs_auto_hide": { "value": {{configuration.AutoHide.ToString().ToLowerInvariant()}} },
                        "crosshairs_is_fixed_length_enabled": { "value": {{configuration.FixedLengthEnabled.ToString().ToLowerInvariant()}} },
                        "crosshairs_fixed_length": { "value": {{configuration.FixedLength}} },
                        "auto_activate": { "value": {{configuration.AutoActivate.ToString().ToLowerInvariant()}} },
                        "gliding_travel_speed": { "value": 25 },
                        "gliding_delay_speed": { "value": 5 }
                    }
                }
                """;

    private sealed record CrosshairsOrigin(int X, int Y);

    private sealed record CrosshairsConfiguration(
        int ShortcutCode = (int)Key.P,
        string Color = "#FF0000",
        int Opacity = 100,
        int Radius = 20,
        int Thickness = 9,
        string BorderColor = "#00FF00",
        int BorderSize = 0,
        int Orientation = 0,
        bool AutoHide = false,
        bool FixedLengthEnabled = false,
        int FixedLength = 100,
        bool AutoActivate = false);
}

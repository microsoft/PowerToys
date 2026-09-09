// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static MouseUtils.UITests.MouseUtilsTestHelper;

namespace MouseUtils.UITests;

[TestClass]
public class MouseHighlighterTests : UITestBase
{
    private const string ModuleName = "MouseHighlighter";
    private const string ToggleId = "MouseUtils_MouseHighlighterToggleId";
    private const string WindowClass = "MouseHighlighter";
    private static readonly IDisposable ModuleSettings = SettingsConfigHelper.PreserveModuleSettings(ModuleName);
    private static IDisposable? clientAreaAnimations;

    static MouseHighlighterTests()
    {
    }

    public MouseHighlighterTests()
        : base(PowerToysModule.PowerToysSettings, enableModules: new[] { ModuleName })
    {
    }

    [ClassInitialize]
    public static void PrepareClass(TestContext testContext)
    {
        _ = testContext;
        clientAreaAnimations = MouseUtilsTestHelper.PreserveClientAreaAnimationsEnabled();
    }

    [ClassCleanup]
    public static void RestoreClassState()
    {
        DisposeAll(clientAreaAnimations, ModuleSettings);
    }

    protected override void PrepareTestState()
    {
        var configuration = TestContext.TestName switch
        {
            nameof(ChangedShortcutTogglesAndDisabledModuleRejectsActivation) => new HighlighterConfiguration(ShortcutCode: (int)Key.O),
            nameof(CircleAlphaRadiusAndFadeTimingAreApplied) => new HighlighterConfiguration(
                LeftColor: "#80FF0000",
                RightColor: "#8000FF00",
                Radius: 100,
                FadeDelayMs: 2_000,
                FadeDurationMs: 4_000),
            nameof(SpotlightModeUsesAlwaysColorAndRadius) => new HighlighterConfiguration(
                AlwaysColor: "#80FF0000",
                Radius: 80,
                SpotlightMode: true),
            nameof(RippleQuickClickUsesSizeIntensityAndDuration) => RippleConfiguration(),
            nameof(RippleHeldIndicatorFollowsDragWhenEnabled) => RippleConfiguration(showDragTrail: true),
            nameof(RippleHeldIndicatorStaysAtPressWhenDisabled) => RippleConfiguration(showDragTrail: false),
            nameof(RippleRightReleasePulseIsDrawn) => RippleConfiguration(showReleasePulse: true),
            nameof(AutoActivateStartsVisible) => new HighlighterConfiguration(AutoActivate: true),
            _ => new HighlighterConfiguration(),
        };

        MouseUtilsTestHelper.ReplaceModuleSettings(ModuleName, CreateSettings(configuration));
    }

    [TestCleanup]
    public async Task CleanupInput()
    {
        await CaptureFailureArtifactsBeforeCleanupAsync();
        MouseHelper.LeftUp();
        MouseHelper.RightUp();
        KeyboardHelper.ReleaseKey(Key.LWin);
        KeyboardHelper.ReleaseKey(Key.Shift);
    }

    [TestMethod]
    [TestCategory("MouseUtils")]
    [TestCategory("Mouse Utils #17")]
    [TestCategory("Mouse Utils #18")]
    [TestCategory("Mouse Utils #19")]
    [TestCategory("Mouse Utils #20")]
    public void CircleClicksAndDragsFollowCursor()
    {
        var window = MouseUtilsTestHelper.WaitForWindowClass(WindowClass);
        WindowHelper.MinimizeWindow(new IntPtr(Session.WindowHandle));
        using var activationWatcher = new WindowShowWatcher(WindowClass, window.Hwnd.ToInt64());
        KeyboardHelper.SendKeys(Key.LWin, Key.Shift, Key.H);
        Assert.IsTrue(activationWatcher.Wait(5_000), "The default Win+Shift+H shortcut did not show Mouse Highlighter.");
        var (centerX, centerY) = WindowHelper.GetScreenCenter();
        MouseHelper.MoveTo(centerX, centerY);

        MouseHelper.LeftDown();
        AssertPixelNear(centerX + 25, centerY, Color.Red, 5, "left-button circle");
        MouseHelper.MoveBy(180, 100, steps: 20, delayMs: 20);
        var moved = MouseHelper.GetMousePosition();
        AssertColorNearPoint(moved.X, moved.Y, Color.Red, 45, "left-button circle did not follow the drag");
        MouseHelper.LeftUp();

        MouseHelper.MoveTo(centerX, centerY);
        MouseHelper.RightDown();
        AssertPixelNear(centerX + 25, centerY, Color.Lime, 5, "right-button circle");
        MouseHelper.MoveBy(-180, 100, steps: 20, delayMs: 20);
        moved = MouseHelper.GetMousePosition();
        AssertColorNearPoint(moved.X, moved.Y, Color.Lime, 45, "right-button circle did not follow the drag");
        MouseHelper.RightUp();

        KeyboardHelper.SendKeys(Key.LWin, Key.Shift, Key.H);
        Assert.IsTrue(activationWatcher.WaitForHidden(5_000), "The default shortcut did not hide Mouse Highlighter.");
        using var deactivatedClick = new WindowShowWatcher(WindowClass, window.Hwnd.ToInt64());
        MouseHelper.LeftClickAt(centerX, centerY);
        Assert.IsFalse(deactivatedClick.Wait(1_000), "A click showed Mouse Highlighter after the shortcut toggled it off.");
    }

    [TestMethod]
    [TestCategory("MouseUtils")]
    [TestCategory("Mouse Utils #21")]
    [TestCategory("Mouse Utils #22")]
    public void ChangedShortcutTogglesAndDisabledModuleRejectsActivation()
    {
        var window = MouseUtilsTestHelper.WaitForWindowClass(WindowClass);
        using (var enabledWatcher = new WindowShowWatcher(WindowClass, window.Hwnd.ToInt64()))
        {
            KeyboardHelper.SendKeys(Key.LWin, Key.Shift, Key.O);
            Assert.IsTrue(enabledWatcher.Wait(5_000), "Changed Win+Shift+O shortcut did not show Mouse Highlighter.");
            KeyboardHelper.SendKeys(Key.LWin, Key.Shift, Key.O);
            Assert.IsTrue(enabledWatcher.WaitForHidden(5_000), "Changed shortcut did not hide Mouse Highlighter.");
        }

        MouseUtilsTestHelper.NavigateToMouseUtilities(this);
        MouseUtilsTestHelper.SetModuleEnabled(this, ToggleId, false);
        Assert.IsTrue(
            NamedEventHelper.WaitUntilUnavailable(NamedEventHelper.MouseHighlighterToggle),
            "Mouse Highlighter trigger event remained available after disabling the module.");
        using var disabledWatcher = new WindowShowWatcher(WindowClass);
        KeyboardHelper.SendKeys(Key.LWin, Key.Shift, Key.O);
        Assert.IsFalse(disabledWatcher.Wait(1_500), "Mouse Highlighter appeared from its shortcut while disabled.");
    }

    [TestMethod]
    [TestCategory("MouseUtils")]
    [TestCategory("Mouse Utils #23")]
    [TestCategory("Mouse Utils #24")]
    public void CircleAlphaRadiusAndFadeTimingAreApplied()
    {
        MouseUtilsTestHelper.RunWithClientAreaAnimationsEnabled(() =>
        {
            WindowHelper.MinimizeWindow(new IntPtr(Session.WindowHandle));
            var (centerX, centerY) = WindowHelper.GetScreenCenter();
            MouseHelper.MoveTo(centerX, centerY);
            var inner = (X: centerX + 60, Y: centerY);
            var fadePoint = (X: centerX + 35, Y: centerY);
            var outer = (X: centerX + 130, Y: centerY);
            var innerBase = GetStablePixel(inner.X, inner.Y);
            var fadeBase = GetStablePixel(fadePoint.X, fadePoint.Y);
            var outerBase = GetStablePixel(outer.X, outer.Y);
            Activate();

            MouseHelper.LeftDown();
            var expectedInner = Blend(Color.Red, innerBase, 128);
            var expectedFadePoint = Blend(Color.Red, fadeBase, 128);
            AssertPixelNear(inner.X, inner.Y, expectedInner, 5, "semi-transparent left circle inside its 100px radius and 70px pressed radius");
            AssertPixelNear(fadePoint.X, fadePoint.Y, expectedFadePoint, 5, "semi-transparent left circle inside its pressed radius");
            AssertPixelNear(outer.X, outer.Y, outerBase, 5, "desktop outside the configured 100px radius");
            var fullContrast = ColorDistance(expectedFadePoint, fadeBase);
            var fadeStopwatch = System.Diagnostics.Stopwatch.StartNew();
            MouseHelper.LeftUp();

            var fadeStarted = WaitHelper.WaitForStable(
                () => WindowHelper.GetPixelColor(fadePoint.X, fadePoint.Y),
                color => ColorDistance(color, fadeBase) <= fullContrast * 0.9,
                4_000,
                pollIntervalMS: 20);
            Assert.IsTrue(fadeStarted.Succeeded, "The circle did not begin fading after the button was released.");
            var fadeStartedAt = fadeStopwatch.Elapsed;
            Assert.IsTrue(
                fadeStartedAt >= TimeSpan.FromSeconds(1.4) && fadeStartedAt <= TimeSpan.FromSeconds(3.5),
                $"Configured 2-second fade delay began after {fadeStartedAt.TotalSeconds:F1}s; expected 1.4-3.5s.");
            var fadeCompleted = WaitHelper.WaitForStable(
                () => WindowHelper.GetPixelColor(fadePoint.X, fadePoint.Y),
                color => IsNear(color, fadeBase, 5),
                7_000,
                requiredConsecutiveMatches: 3,
                pollIntervalMS: 50);
            Assert.IsTrue(fadeCompleted.Succeeded, "Circle did not return to the desktop color after its configured fade duration.");
            var observedFadeDuration = fadeStopwatch.Elapsed - fadeStartedAt;
            Assert.IsTrue(
                observedFadeDuration >= TimeSpan.FromSeconds(1.5) && observedFadeDuration <= TimeSpan.FromSeconds(6.5),
                $"Configured 4-second fade completed {observedFadeDuration.TotalSeconds:F1}s after its first visible transition; expected 1.5-6.5s.");

            MouseHelper.RightDown();
            var expectedRight = Blend(Color.Lime, fadeBase, 128);
            AssertPixelNear(fadePoint.X, fadePoint.Y, expectedRight, 5, "semi-transparent right circle inside its 70px radius");
            MouseHelper.RightUp();
        });
    }

    [TestMethod]
    [TestCategory("MouseUtils")]
    public void SpotlightModeUsesAlwaysColorAndRadius()
    {
        WindowHelper.MinimizeWindow(new IntPtr(Session.WindowHandle));
        var (centerX, centerY) = WindowHelper.GetScreenCenter();
        MouseHelper.MoveTo(centerX, centerY);
        var inside = (X: centerX + 40, Y: centerY);
        var outside = (X: centerX + 120, Y: centerY);
        var insideBase = GetStablePixel(inside.X, inside.Y);
        var outsideBase = GetStablePixel(outside.X, outside.Y);
        MouseHelper.MoveBy(160, 80, steps: 20, delayMs: 20);
        var calibratedTarget = MouseHelper.GetMousePosition();
        var movedInside = (X: calibratedTarget.X + 30, Y: calibratedTarget.Y);
        var movedOutside = (X: calibratedTarget.X + 120, Y: calibratedTarget.Y);
        var movedInsideBase = GetStablePixel(movedInside.X, movedInside.Y);
        var movedOutsideBase = GetStablePixel(movedOutside.X, movedOutside.Y);
        MouseHelper.MoveTo(centerX, centerY);
        Activate();

        AssertPixelNear(inside.X, inside.Y, insideBase, 5, "transparent Spotlight hole inside the configured radius");
        AssertPixelNear(outside.X, outside.Y, Blend(Color.Red, outsideBase, 128), 5, "Spotlight tint outside the configured radius");

        MouseHelper.MoveBy(160, 80, steps: 20, delayMs: 20);
        var moved = MouseHelper.GetMousePosition();
        Assert.IsTrue(
            Distance(moved.X, moved.Y, calibratedTarget.X, calibratedTarget.Y) <= 10,
            $"Calibrated relative movement ended at ({calibratedTarget.X},{calibratedTarget.Y}), but overlay movement ended at ({moved.X},{moved.Y}).");
        AssertPixelNear(movedInside.X, movedInside.Y, movedInsideBase, 5, "transparent Spotlight hole after cursor movement");
        AssertPixelNear(movedOutside.X, movedOutside.Y, Blend(Color.Red, movedOutsideBase, 128), 5, "Spotlight tint after cursor movement");
    }

    [TestMethod]
    [TestCategory("MouseUtils")]
    public void RippleQuickClickUsesSizeIntensityAndDuration()
    {
        WindowHelper.MinimizeWindow(new IntPtr(Session.WindowHandle));
        var (centerX, centerY) = WindowHelper.GetScreenCenter();
        var nearPoint = (X: centerX + 10, Y: centerY);
        var farPoint = (X: centerX + 100, Y: centerY);
        const double rendererRadiusScale = 1.4;
        const int minimumChangedAnnulusPixels = 12;
        var defaultMaximumRadius = (int)Math.Ceiling(60 * rendererRadiusScale);
        var configuredMaximumRadius = (int)Math.Ceiling(120 * rendererRadiusScale);
        var captureRadius = configuredMaximumRadius + 10;
        MouseHelper.MoveTo(centerX - 250, centerY - 200);
        var nearBase = GetStablePixel(nearPoint.X, nearPoint.Y);
        var farBase = GetStablePixel(farPoint.X, farPoint.Y);
        using var outerBaseline = CaptureSquare(centerX, centerY, captureRadius);
        MouseHelper.MoveTo(centerX, centerY);
        Activate();

        var expectedHighIntensityGlow = Blend(Color.Magenta, nearBase, 95);
        var observedNearGlow = false;
        var observedOuterRipple = false;
        var maximumChangedOuterSamples = 0;
        for (var attempt = 1; attempt <= 3 && !observedOuterRipple; attempt++)
        {
            MouseHelper.MoveTo(centerX, centerY);
            MouseHelper.LeftClick();
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            MouseHelper.MoveTo(centerX + 250, centerY + 200);
            var attemptObservedNearGlow = WaitHelper.WaitForStable(
                () => WindowHelper.GetPixelColor(nearPoint.X, nearPoint.Y),
                color => IsNear(color, expectedHighIntensityGlow, 20),
                750,
                requiredConsecutiveMatches: 1,
                pollIntervalMS: 10).Succeeded;
            observedNearGlow |= attemptObservedNearGlow;

            if (attemptObservedNearGlow)
            {
                if (stopwatch.ElapsedMilliseconds < 900)
                {
                    Thread.Sleep(900 - (int)stopwatch.ElapsedMilliseconds);
                }

                var outerRipple = WaitHelper.WaitForStable(
                    () =>
                    {
                        using var current = CaptureSquare(centerX, centerY, captureRadius);
                        return CountRipplePixels(
                            outerBaseline,
                            current,
                            minimumRadius: defaultMaximumRadius + 4,
                            maximumRadius: configuredMaximumRadius + 4);
                    },
                    changedPixels =>
                    {
                        maximumChangedOuterSamples = Math.Max(maximumChangedOuterSamples, changedPixels);
                        return changedPixels >= minimumChangedAnnulusPixels;
                    },
                    900,
                    requiredConsecutiveMatches: 1,
                    pollIntervalMS: 20);
                observedOuterRipple = outerRipple.Succeeded;
            }

            if (!observedOuterRipple && attempt < 3)
            {
                MouseUtilsTestHelper.Step(
                    this,
                    $"Ripple click attempt {attempt}/3 did not expose the complete configured effect; retrying after it clears");
                Assert.IsTrue(
                    WaitForRippleToClear(nearPoint, farPoint, nearBase, farBase, 3_000),
                    "Ripple did not clear before retrying the complete click effect.");
            }
        }

        Assert.IsTrue(observedNearGlow, "The configured high-intensity Ripple glow did not render near the click point.");
        Assert.IsTrue(
            observedOuterRipple,
            $"The configured 120px, 1.8-second Ripple was absent outside the default maximum radius after 900ms on three complete attempts; " +
            $"annulus={defaultMaximumRadius + 4}-{configuredMaximumRadius + 4}px, maximum changed pixels={maximumChangedOuterSamples}.");
        Assert.IsTrue(
            WaitForRippleToClear(nearPoint, farPoint, nearBase, farBase, 2_000),
            "Ripple remained after its configured 1.8-second duration.");
    }

    [TestMethod]
    [TestCategory("MouseUtils")]
    public void RippleHeldIndicatorFollowsDragWhenEnabled()
    {
        AssertRippleDragTrail(expectedToFollow: true);
    }

    [TestMethod]
    [TestCategory("MouseUtils")]
    public void RippleHeldIndicatorStaysAtPressWhenDisabled()
    {
        AssertRippleDragTrail(expectedToFollow: false);
    }

    [TestMethod]
    [TestCategory("MouseUtils")]
    public void RippleRightReleasePulseIsDrawn()
    {
        WindowHelper.MinimizeWindow(new IntPtr(Session.WindowHandle));
        var (centerX, centerY) = WindowHelper.GetScreenCenter();
        MouseHelper.MoveTo(centerX, centerY);
        Activate();

        MouseHelper.RightDown();
        Thread.Sleep(350);
        const int captureRadius = 55;
        using var heldFrame = CaptureSquare(centerX, centerY, captureRadius);
        MouseHelper.RightUp();
        MouseHelper.MoveTo(centerX + 250, centerY + 200);
        var releasePulse = WaitHelper.WaitForStable(
            () =>
            {
                using var current = CaptureSquare(centerX, centerY, captureRadius);
                return CountAxisPixelsTowardColor(heldFrame, current, Color.Yellow, minimumRadius: 18, maximumRadius: 50);
            },
            changedPixels => changedPixels >= 20,
            1_500,
            pollIntervalMS: 20);
        Assert.IsTrue(
            releasePulse.Succeeded,
            $"Right-button release did not draw the configured yellow crosshair lines; maximum qualifying axis pixels={releasePulse.LastObservation}.");
    }

    [TestMethod]
    [TestCategory("MouseUtils")]
    public void AutoActivateStartsVisible()
    {
        Assert.IsTrue(
            WaitHelper.WaitForStable(
                () => WindowControl.IsAnyWindowOfClassVisible(WindowClass),
                visible => visible,
                10_000,
                requiredConsecutiveMatches: 3).Succeeded,
            "Mouse Highlighter did not start visible when auto-activate was enabled.");
    }

    private static HighlighterConfiguration RippleConfiguration(bool showDragTrail = true, bool showReleasePulse = true) => new(
        LeftColor: "#FFFF00FF",
        RightColor: "#FFFFFF00",
        AlwaysColor: "#00000000",
        RippleMode: true,
        RippleSize: 120,
        RippleIntensity: 1.35,
        RippleDurationMs: 1_800,
        RippleShowDragTrail: showDragTrail,
        RippleShowReleasePulse: showReleasePulse);

    private static string CreateSettings(HighlighterConfiguration configuration) => $$"""
        {
          "name": "MouseHighlighter",
          "version": "1.2",
          "properties": {
            "activation_shortcut": { "win": true, "ctrl": false, "alt": false, "shift": true, "code": {{configuration.ShortcutCode}}, "key": "" },
            "left_button_click_color": { "value": "{{configuration.LeftColor}}" },
            "right_button_click_color": { "value": "{{configuration.RightColor}}" },
            "always_color": { "value": "{{configuration.AlwaysColor}}" },
            "highlight_radius": { "value": {{configuration.Radius}} },
            "highlight_fade_delay_ms": { "value": {{configuration.FadeDelayMs}} },
            "highlight_fade_duration_ms": { "value": {{configuration.FadeDurationMs}} },
            "auto_activate": { "value": {{configuration.AutoActivate.ToString().ToLowerInvariant()}} },
            "spotlight_mode": { "value": {{configuration.SpotlightMode.ToString().ToLowerInvariant()}} },
            "ripple_mode": { "value": {{configuration.RippleMode.ToString().ToLowerInvariant()}} },
            "ripple_size": { "value": {{configuration.RippleSize}} },
            "ripple_intensity": { "value": {{configuration.RippleIntensity.ToString(System.Globalization.CultureInfo.InvariantCulture)}} },
            "ripple_duration_ms": { "value": {{configuration.RippleDurationMs}} },
            "ripple_show_drag_trail": { "value": {{configuration.RippleShowDragTrail.ToString().ToLowerInvariant()}} },
            "ripple_show_release_pulse": { "value": {{configuration.RippleShowReleasePulse.ToString().ToLowerInvariant()}} }
          }
        }
        """;

    private static void Activate()
    {
        Assert.IsTrue(
            NamedEventHelper.WaitAndSignal(NamedEventHelper.MouseHighlighterToggle),
            "Mouse Highlighter did not create or respond to its trigger event.");
        Assert.IsTrue(
            WaitHelper.WaitForStable(
                () => WindowControl.IsAnyWindowOfClassVisible(WindowClass),
                visible => visible,
                5_000,
                requiredConsecutiveMatches: 2).Succeeded,
            "Mouse Highlighter window did not become visible.");
    }

    private void AssertRippleDragTrail(bool expectedToFollow)
    {
        WindowHelper.MinimizeWindow(new IntPtr(Session.WindowHandle));
        var (centerX, centerY) = WindowHelper.GetScreenCenter();
        MouseHelper.MoveTo(centerX, centerY);
        Activate();

        MouseHelper.LeftDown();
        Thread.Sleep(350);
        AssertRippleRingNear(centerX, centerY, "held Ripple indicator did not appear at the press point");
        MouseHelper.MoveBy(180, 80, steps: 20, delayMs: 20);
        var moved = MouseHelper.GetMousePosition();
        if (expectedToFollow)
        {
            AssertRippleRingNear(moved.X, moved.Y, "held Ripple indicator did not follow the drag");
        }
        else
        {
            AssertRippleRingNear(centerX, centerY, "held Ripple indicator moved despite drag trail being disabled");
        }

        MouseHelper.LeftUp();
    }

    private static void AssertRippleRingNear(int centerX, int centerY, string message)
    {
        // The configured 120px ripple renders its held magenta ring across radii 42-69; requiring
        // magenta to dominate green by 30 rejects neutral desktop pixels without demanding exact alpha.
        const int heldRingMinimumRadius = 42;
        const int heldRingRadiusCount = 28;
        const int magentaChannelDominance = 30;
        Assert.IsTrue(
            WaitHelper.WaitForStable(
                () => Enumerable.Range(heldRingMinimumRadius, heldRingRadiusCount)
                    .SelectMany(radius => new[]
                    {
                        WindowHelper.GetPixelColor(centerX + radius, centerY),
                        WindowHelper.GetPixelColor(centerX - radius, centerY),
                        WindowHelper.GetPixelColor(centerX, centerY + radius),
                        WindowHelper.GetPixelColor(centerX, centerY - radius),
                    })
                    .Count(color => color.R > color.G + magentaChannelDominance && color.B > color.G + magentaChannelDominance),
                count => count >= 2,
                1_500,
                requiredConsecutiveMatches: 1,
                pollIntervalMS: 30).Succeeded,
            message);
    }

    private static void AssertColorNearPoint(int centerX, int centerY, Color expected, int searchRadius, string message)
    {
        Assert.IsTrue(
            WaitHelper.WaitForStable(
                () => Enumerable.Range(-searchRadius, (searchRadius * 2) + 1)
                    .Where(offset => offset % 5 == 0)
                    .SelectMany(offset => new[]
                    {
                        WindowHelper.GetPixelColor(centerX + offset, centerY),
                        WindowHelper.GetPixelColor(centerX, centerY + offset),
                    })
                    .Any(color => IsNear(color, expected, 5)),
                found => found,
                2_000,
                requiredConsecutiveMatches: 1,
                pollIntervalMS: 50).Succeeded,
            message);
    }

    private static bool WaitForRippleToClear(
        (int X, int Y) nearPoint,
        (int X, int Y) farPoint,
        Color nearBase,
        Color farBase,
        int timeoutMs) =>
        WaitHelper.WaitForStable(
            () => new
            {
                Near = WindowHelper.GetPixelColor(nearPoint.X, nearPoint.Y),
                Far = WindowHelper.GetPixelColor(farPoint.X, farPoint.Y),
            },
            sample => IsNear(sample.Near, nearBase, 5) && IsNear(sample.Far, farBase, 5),
            timeoutMs,
            requiredConsecutiveMatches: 5,
            pollIntervalMS: 50).Succeeded;

    private static Bitmap CaptureSquare(int centerX, int centerY, int radius)
    {
        var bitmap = new Bitmap((radius * 2) + 1, (radius * 2) + 1);
        try
        {
            using var graphics = Graphics.FromImage(bitmap);
            graphics.CopyFromScreen(centerX - radius, centerY - radius, 0, 0, bitmap.Size);
            return bitmap;
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }

    private static int CountRipplePixels(Bitmap baseline, Bitmap current, int minimumRadius, int maximumRadius)
    {
        const int minimumColorDistanceImprovement = 6;
        var center = baseline.Width / 2;
        var minimumSquared = minimumRadius * minimumRadius;
        var maximumSquared = maximumRadius * maximumRadius;
        var count = 0;
        for (var y = 0; y < baseline.Height; y++)
        {
            var deltaY = y - center;
            for (var x = 0; x < baseline.Width; x++)
            {
                var deltaX = x - center;
                var distanceSquared = (deltaX * deltaX) + (deltaY * deltaY);
                if (distanceSquared < minimumSquared || distanceSquared > maximumSquared)
                {
                    continue;
                }

                var before = baseline.GetPixel(x, y);
                var after = current.GetPixel(x, y);
                if (ColorDistance(after, Color.Magenta) <= ColorDistance(before, Color.Magenta) - minimumColorDistanceImprovement)
                {
                    count++;
                }
            }
        }

        return count;
    }

    private static int CountAxisPixelsTowardColor(Bitmap baseline, Bitmap current, Color target, int minimumRadius, int maximumRadius)
    {
        var center = baseline.Width / 2;
        var count = 0;
        for (var radius = minimumRadius; radius <= maximumRadius; radius++)
        {
            for (var bandOffset = -2; bandOffset <= 2; bandOffset++)
            {
                var points = new[]
                {
                    (X: center + radius, Y: center + bandOffset),
                    (X: center - radius, Y: center + bandOffset),
                    (X: center + bandOffset, Y: center + radius),
                    (X: center + bandOffset, Y: center - radius),
                };
                foreach (var point in points)
                {
                    var beforeDistance = ColorDistance(baseline.GetPixel(point.X, point.Y), target);
                    var afterDistance = ColorDistance(current.GetPixel(point.X, point.Y), target);
                    if (afterDistance <= 60 && afterDistance <= beforeDistance - 20)
                    {
                        count++;
                    }
                }
            }
        }

        return count;
    }

    private static double ColorDistance(Color first, Color second)
    {
        var red = first.R - second.R;
        var green = first.G - second.G;
        var blue = first.B - second.B;
        return Math.Sqrt((red * red) + (green * green) + (blue * blue));
    }

    private sealed record HighlighterConfiguration(
        int ShortcutCode = (int)Key.H,
        string LeftColor = "#FFFF0000",
        string RightColor = "#FF00FF00",
        string AlwaysColor = "#00000000",
        int Radius = 50,
        int FadeDelayMs = 100,
        int FadeDurationMs = 300,
        bool AutoActivate = false,
        bool SpotlightMode = false,
        bool RippleMode = false,
        int RippleSize = 60,
        double RippleIntensity = 0.7,
        int RippleDurationMs = 480,
        bool RippleShowDragTrail = true,
        bool RippleShowReleasePulse = true);
}

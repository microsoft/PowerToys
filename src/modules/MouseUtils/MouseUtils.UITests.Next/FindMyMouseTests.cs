// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MouseUtils.UITests;

[TestClass]
public class FindMyMouseTests : UITestBase
{
    private const string ModuleName = "FindMyMouse";
    private const string ToggleId = "MouseUtils_FindMyMouseToggleId";
    private const string WindowClass = "FindMyMouse";
    private static readonly IDisposable ModuleSettings = SettingsConfigHelper.PreserveModuleSettings(ModuleName);
    private static IDisposable? clientAreaAnimations;

    public FindMyMouseTests()
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
        try
        {
            clientAreaAnimations?.Dispose();
        }
        finally
        {
            ModuleSettings.Dispose();
        }
    }

    protected override void PrepareTestState()
    {
        var configuration = TestContext.TestName switch
        {
            nameof(AppearanceColorsRadiusAndAlphaAreApplied) => new FindMyMouseConfiguration(
                BackgroundColor: "#80FF0000",
                SpotlightColor: "#8000FF00",
                Radius: 80,
                AnimationDurationMs: 1,
                InitialZoom: 1),
            nameof(InitialZoomIsApplied) => new FindMyMouseConfiguration(
                BackgroundColor: "#FFFF0000",
                SpotlightColor: "#FF00FF00",
                Radius: 40,
                AnimationDurationMs: 2_000,
                InitialZoom: 1),
            nameof(AnimationDurationIsApplied) => new FindMyMouseConfiguration(
                BackgroundColor: "#FFFF0000",
                SpotlightColor: "#FF00FF00",
                Radius: 40,
                AnimationDurationMs: 10_000,
                InitialZoom: 9),
            nameof(RightControlActivates) => new FindMyMouseConfiguration(ActivationMethod: 1),
            nameof(CustomShortcutActivates) => new FindMyMouseConfiguration(ActivationMethod: 3),
            nameof(IncludeWinKeyGatesDoubleControlActivation) => new FindMyMouseConfiguration(IncludeWinKey: true),
            nameof(ExcludedForegroundAppBlocksActivation) => new FindMyMouseConfiguration(ExcludedApps: "PowerToys.Settings.exe"),
            _ => new FindMyMouseConfiguration(),
        };

        MouseUtilsTestHelper.ReplaceModuleSettings(ModuleName, CreateSettings(configuration));
    }

    [TestCleanup]
    public async Task CleanupInput()
    {
        await CaptureFailureArtifactsBeforeCleanupAsync();
        MouseHelper.LeftUp();
        MouseHelper.RightUp();
        KeyboardHelper.ReleaseKey(Key.LCtrl);
        KeyboardHelper.ReleaseKey(Key.RCtrl);
        KeyboardHelper.ReleaseKey(Key.LWin);
    }

    [TestMethod]
    [TestCategory("MouseUtils")]
    [TestCategory("Mouse Utils #1")]
    [TestCategory("Mouse Utils #2")]
    [TestCategory("Mouse Utils #3")]
    [TestCategory("Mouse Utils #4")]
    public void ActivationAndKeyboardMouseDismissal()
    {
        MouseUtilsTestHelper.NavigateToMouseUtilities(this);
        MouseUtilsTestHelper.SetModuleEnabled(this, ToggleId, true);
        var window = MouseUtilsTestHelper.WaitForWindowClass(WindowClass);

        using (var keyboardDismissal = new WindowShowWatcher(WindowClass, window.Hwnd.ToInt64()))
        {
            DoubleTap(Key.LCtrl);
            Assert.IsTrue(keyboardDismissal.Wait(5_000), "Double left Ctrl did not show Find My Mouse.");
            KeyboardHelper.SendKey(Key.A);
            Assert.IsTrue(keyboardDismissal.WaitForHidden(5_000), "A keyboard key did not dismiss Find My Mouse.");
        }

        using var mouseDismissal = new WindowShowWatcher(WindowClass, window.Hwnd.ToInt64());
        DoubleTap(Key.LCtrl);
        Assert.IsTrue(mouseDismissal.Wait(5_000), "Second double left Ctrl did not show Find My Mouse.");
        MouseHelper.LeftClick();
        Assert.IsTrue(mouseDismissal.WaitForHidden(5_000), "A mouse button did not dismiss Find My Mouse.");
    }

    [TestMethod]
    [TestCategory("MouseUtils")]
    [TestCategory("Mouse Utils #5")]
    [TestCategory("Mouse Utils #6")]
    public void DisabledModuleRejectsActivationAndReenableWorks()
    {
        MouseUtilsTestHelper.NavigateToMouseUtilities(this);
        var toggle = MouseUtilsTestHelper.SetModuleEnabled(this, ToggleId, false);
        Assert.IsTrue(
            NamedEventHelper.WaitUntilUnavailable(NamedEventHelper.FindMyMouseTrigger),
            "Find My Mouse trigger event remained available after disabling the module.");

        using (var disabledWatcher = new WindowShowWatcher(WindowClass))
        {
            DoubleTap(Key.LCtrl);
            Assert.IsFalse(disabledWatcher.Wait(1_500), "Find My Mouse appeared while disabled.");
        }

        _ = toggle;
        MouseUtilsTestHelper.SetModuleEnabled(this, ToggleId, true);
        Assert.IsTrue(
            NamedEventHelper.WaitUntilAvailable(NamedEventHelper.FindMyMouseTrigger),
            "Find My Mouse trigger event was not recreated after enabling.");
        var window = MouseUtilsTestHelper.WaitForWindowClass(WindowClass);
        using var enabledWatcher = new WindowShowWatcher(WindowClass, window.Hwnd.ToInt64());
        TriggerUntilShown(enabledWatcher, () => DoubleTap(Key.LCtrl), "double left Ctrl after re-enabling");
    }

    [TestMethod]
    [TestCategory("MouseUtils")]
    [TestCategory("Mouse Utils #10")]
    [TestCategory("Mouse Utils #11")]
    [TestCategory("Mouse Utils #12")]
    public void AppearanceColorsRadiusAndAlphaAreApplied()
    {
        WindowHelper.MinimizeWindow(new IntPtr(Session.WindowHandle));
        var (centerX, centerY) = WindowHelper.GetScreenCenter();
        MouseHelper.MoveTo(centerX, centerY);
        var spotlightPoint = (X: centerX + 40, Y: centerY);
        var backgroundPoint = (X: centerX + 120, Y: centerY);
        var spotlightBase = GetStablePixel(spotlightPoint.X, spotlightPoint.Y);
        var backgroundBase = GetStablePixel(backgroundPoint.X, backgroundPoint.Y);
        var window = MouseUtilsTestHelper.WaitForWindowClass(WindowClass);
        using var watcher = new WindowShowWatcher(WindowClass, window.Hwnd.ToInt64());

        Assert.IsTrue(NamedEventHelper.WaitAndSignal(NamedEventHelper.FindMyMouseTrigger), "Find My Mouse trigger event was unavailable.");
        Assert.IsTrue(watcher.Wait(5_000), "Find My Mouse did not show for appearance validation.");

        var expectedSpotlight = Blend(Color.Lime, spotlightBase, 128);
        var expectedBackground = Blend(Color.Red, backgroundBase, 128);
        AssertPixelNear(spotlightPoint.X, spotlightPoint.Y, expectedSpotlight, "spotlight color/alpha inside the configured radius");
        AssertPixelNear(backgroundPoint.X, backgroundPoint.Y, expectedBackground, "background color/alpha outside the configured radius");
    }

    [TestMethod]
    [TestCategory("MouseUtils")]
    public void InitialZoomIsApplied()
    {
        MouseUtilsTestHelper.RunWithClientAreaAnimationsEnabled(() =>
        {
            WindowHelper.MinimizeWindow(new IntPtr(Session.WindowHandle));
            var (centerX, centerY) = WindowHelper.GetScreenCenter();
            MouseHelper.MoveTo(centerX, centerY);
            var window = MouseUtilsTestHelper.WaitForWindowClass(WindowClass);
            using var watcher = new WindowShowWatcher(WindowClass, window.Hwnd.ToInt64());

            Assert.IsTrue(NamedEventHelper.WaitAndSignal(NamedEventHelper.FindMyMouseTrigger), "Find My Mouse trigger event was unavailable.");
            Assert.IsTrue(watcher.Wait(5_000), "Find My Mouse did not show for initial-zoom validation.");
            var result = WaitHelper.WaitForStable(
                () => new
                {
                    Inside = WindowHelper.GetPixelColor(centerX + 20, centerY),
                    Outside = WindowHelper.GetPixelColor(centerX + 80, centerY),
                },
                sample => sample is not null && sample.Inside.G > sample.Inside.R && sample.Outside.R > sample.Outside.G,
                5_000,
                requiredConsecutiveMatches: 2,
                pollIntervalMS: 25);

            Assert.IsTrue(result.Succeeded, $"The configured 1x initial zoom did not keep the 80px probe outside the 40px spotlight. Last sample: {result.LastObservation}.");
        });
    }

    [TestMethod]
    [TestCategory("MouseUtils")]
    public void AnimationDurationIsApplied()
    {
        MouseUtilsTestHelper.RunWithClientAreaAnimationsEnabled(() =>
        {
            WindowHelper.MinimizeWindow(new IntPtr(Session.WindowHandle));
            var (centerX, centerY) = WindowHelper.GetScreenCenter();
            MouseHelper.MoveTo(centerX, centerY);
            var probeX = centerX + 80;
            _ = MouseUtilsTestHelper.WaitForWindowClass(WindowClass);

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            Assert.IsTrue(NamedEventHelper.WaitAndSignal(NamedEventHelper.FindMyMouseTrigger), "Find My Mouse trigger event was unavailable.");
            var initialSpotlight = WaitHelper.WaitForStable(
                () => new
                {
                    Center = WindowHelper.GetPixelColor(centerX + 20, centerY),
                    Probe = WindowHelper.GetPixelColor(probeX, centerY),
                },
                sample => sample is not null && sample.Center.G > sample.Center.R && sample.Probe.G > sample.Probe.R,
                8_000,
                pollIntervalMS: 20);
            Assert.IsTrue(
                initialSpotlight.Succeeded,
                $"The 9x initial spotlight never covered the 80px probe; last sample: {initialSpotlight.LastObservation}.");
            var finalRadius = WaitHelper.WaitForStable(
                () => new
                {
                    Center = WindowHelper.GetPixelColor(centerX + 20, centerY),
                    Probe = WindowHelper.GetPixelColor(probeX, centerY),
                },
                sample => sample is not null && sample.Center.G > sample.Center.R && sample.Probe.R > sample.Probe.G,
                12_000,
                requiredConsecutiveMatches: 3,
                pollIntervalMS: 20);
            Assert.IsTrue(finalRadius.Succeeded, "The spotlight did not reach its configured 40px radius after the 10-second animation.");
            var crossingTime = stopwatch.Elapsed;
            Assert.IsTrue(
                crossingTime >= TimeSpan.FromSeconds(3) && crossingTime <= TimeSpan.FromSeconds(12),
                $"Configured 10-second spotlight animation crossed the 80px probe after {crossingTime.TotalSeconds:F1}s; expected 3-12s for the eased transition.");
        });
    }

    [TestMethod]
    [TestCategory("MouseUtils")]
    public void RightControlActivates()
    {
        AssertActivation(() => DoubleTap(Key.RCtrl), "double right Ctrl");
    }

    [TestMethod]
    [TestCategory("MouseUtils")]
    public void CustomShortcutActivates()
    {
        AssertActivation(() => KeyboardHelper.SendKeys(Key.LWin, Key.Shift, Key.F), "Win+Shift+F");
    }

    [TestMethod]
    [TestCategory("MouseUtils")]
    public void IncludeWinKeyGatesDoubleControlActivation()
    {
        var window = MouseUtilsTestHelper.WaitForWindowClass(WindowClass);
        using (var withoutWin = new WindowShowWatcher(WindowClass, window.Hwnd.ToInt64()))
        {
            DoubleTap(Key.LCtrl);
            Assert.IsFalse(withoutWin.Wait(1_500), "Double Ctrl activated despite the required Windows key not being held.");
        }

        using var withWin = new WindowShowWatcher(WindowClass, window.Hwnd.ToInt64());
        KeyboardHelper.PressKey(Key.LWin);
        try
        {
            DoubleTap(Key.LCtrl);
        }
        finally
        {
            KeyboardHelper.ReleaseKey(Key.LWin);
        }

        Assert.IsTrue(withWin.Wait(5_000), "Win plus double Ctrl did not activate Find My Mouse.");
    }

    [TestMethod]
    [TestCategory("MouseUtils")]
    public void ExcludedForegroundAppBlocksActivation()
    {
        var settingsHwnd = new IntPtr(Session.WindowHandle);
        Assert.IsTrue(WindowControl.WaitForForeground(settingsHwnd, 5_000, 2), "Settings could not become foreground for the exclusion precondition.");
        using (var excluded = new WindowShowWatcher(WindowClass))
        {
            Assert.IsTrue(NamedEventHelper.WaitAndSignal(NamedEventHelper.FindMyMouseTrigger), "Find My Mouse trigger event was unavailable.");
            Assert.IsFalse(excluded.Wait(1_500), "Find My Mouse activated while excluded PowerToys.Settings.exe owned foreground.");
        }

        using var notepad = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("notepad.exe") { UseShellExecute = true });
        Assert.IsNotNull(notepad, "Could not start the allowed foreground Notepad fixture.");
        try
        {
            var notepadWindow = WindowsFinder.WaitForWindowByProcess("notepad", 10_000);
            Assert.IsNotNull(notepadWindow, "Notepad did not create a visible window.");
            Assert.IsTrue(
                WindowControl.WaitForForeground(new IntPtr(notepadWindow.WindowHandle), 5_000, 2),
                $"Notepad could not become foreground. Current foreground: {WindowControl.GetForegroundWindowInfo()}.");

            using var allowed = new WindowShowWatcher(WindowClass);
            Assert.IsTrue(NamedEventHelper.TrySignal(NamedEventHelper.FindMyMouseTrigger), "Find My Mouse trigger event disappeared.");
            Assert.IsTrue(allowed.Wait(5_000), "Find My Mouse remained blocked after an allowed app gained foreground.");
        }
        finally
        {
            WindowControl.TryKillProcessTreeByNameAndWait("notepad", 5_000);
        }
    }

    private static string CreateSettings(FindMyMouseConfiguration configuration) => $$"""
        {
          "name": "FindMyMouse",
          "version": "1.1",
          "properties": {
            "activation_method": { "value": {{configuration.ActivationMethod}} },
            "include_win_key": { "value": {{configuration.IncludeWinKey.ToString().ToLowerInvariant()}} },
            "activation_shortcut": { "win": true, "ctrl": false, "alt": false, "shift": true, "code": 70, "key": "" },
            "do_not_activate_on_game_mode": { "value": false },
            "background_color": { "value": "{{configuration.BackgroundColor}}" },
            "spotlight_color": { "value": "{{configuration.SpotlightColor}}" },
            "spotlight_radius": { "value": {{configuration.Radius}} },
            "animation_duration_ms": { "value": {{configuration.AnimationDurationMs}} },
            "spotlight_initial_zoom": { "value": {{configuration.InitialZoom}} },
            "excluded_apps": { "value": "{{configuration.ExcludedApps}}" },
            "shaking_minimum_distance": { "value": 100 },
            "shaking_interval_ms": { "value": 2000 },
            "shaking_factor": { "value": 150 }
          }
        }
        """;

    private static void DoubleTap(Key controlKey)
    {
        KeyboardHelper.SendKey(controlKey);
        Thread.Sleep(150);
        KeyboardHelper.SendKey(controlKey);
    }

    private void AssertActivation(Action trigger, string description)
    {
        var window = MouseUtilsTestHelper.WaitForWindowClass(WindowClass);
        using var watcher = new WindowShowWatcher(WindowClass, window.Hwnd.ToInt64());
        TriggerUntilShown(watcher, trigger, description);
    }

    private static void TriggerUntilShown(WindowShowWatcher watcher, Action trigger, string description)
    {
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            if (watcher.Wait(0))
            {
                return;
            }

            trigger();
            if (watcher.Wait(5_000))
            {
                return;
            }
        }

        Assert.Fail($"Find My Mouse did not activate from {description} after three attempts.");
    }

    private static Color Blend(Color foreground, Color background, int alpha)
    {
        var inverse = 255 - alpha;
        return Color.FromArgb(
            ((foreground.R * alpha) + (background.R * inverse) + 127) / 255,
            ((foreground.G * alpha) + (background.G * inverse) + 127) / 255,
            ((foreground.B * alpha) + (background.B * inverse) + 127) / 255);
    }

    private static Color GetStablePixel(int x, int y)
    {
        Color? previous = null;
        var result = WaitHelper.WaitForStable(
            () => WindowHelper.GetPixelColor(x, y),
            color =>
            {
                var matchesPrevious = previous.HasValue && color.ToArgb() == previous.Value.ToArgb();
                previous = color;
                return matchesPrevious;
            },
            2_000,
            requiredConsecutiveMatches: 4,
            pollIntervalMS: 100);
        return result.LastObservation;
    }

    private static void AssertPixelNear(int x, int y, Color expected, string description)
    {
        const int tolerance = 4;
        var result = WaitHelper.WaitForStable(
            () => WindowHelper.GetPixelColor(x, y),
            actual =>
                Math.Abs(actual.R - expected.R) <= tolerance &&
                Math.Abs(actual.G - expected.G) <= tolerance &&
                Math.Abs(actual.B - expected.B) <= tolerance,
            5_000,
            requiredConsecutiveMatches: 2,
            pollIntervalMS: 100);
        Assert.IsTrue(result.Succeeded, $"Unexpected {description} at ({x},{y}). Expected {expected}; observed {result.LastObservation}.");
    }

    private sealed record FindMyMouseConfiguration(
        int ActivationMethod = 0,
        bool IncludeWinKey = false,
        string BackgroundColor = "#FFFF0000",
        string SpotlightColor = "#FF00FF00",
        int Radius = 80,
        int AnimationDurationMs = 1,
        int InitialZoom = 1,
        string ExcludedApps = "");
}

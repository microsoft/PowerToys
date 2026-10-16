// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.AlwaysOnTop.UITests;

[TestClass]
public sealed class AlwaysOnTopTests : UITestBase
{
    private const string AlwaysOnTopProcessName = "PowerToys.AlwaysOnTop";
    private const string BorderWindowClass = "AlwaysOnTop_Border";

    // The module's file watcher has no acknowledgement signal. Use this only before negative
    // assertions where no product state can prove that the new setting has loaded.
    private const int SettingsReloadDelayMs = 3_000;
    private static readonly Key[] ActivationShortcut = [Key.LWin, Key.Ctrl, Key.T];
    private static IDisposable? originalModuleSettings;

    private TestWindow? fixture;
    private Direct3DFullScreenScope? fullScreen;
    private bool extraVirtualDesktopCreated;

    public AlwaysOnTopTests()
        : base(PowerToysModule.PowerToysSettings, enableModules: [AlwaysOnTopSettingsSeed.ModuleName])
    {
    }

    protected override IReadOnlyList<string> StaleProcessNames { get; } =
    [
        "PowerToys",
        "PowerToys.Settings",
        "PowerToys.AlwaysOnTop",
        "PowerToys.FancyZonesEditor",
    ];

    protected override bool ReuseScopeAcrossTests => true;

    [ClassInitialize]
    public static void InitializeClass(TestContext testContext)
    {
        _ = testContext;
        originalModuleSettings = SettingsConfigHelper.PreserveModuleSettings(AlwaysOnTopSettingsSeed.ModuleName);
        try
        {
            AlwaysOnTopSettingsSeed.ApplyBaseline();
        }
        catch
        {
            originalModuleSettings.Dispose();
            originalModuleSettings = null;
            throw;
        }
    }

    [ClassCleanup]
    public static void CleanupClass()
    {
        originalModuleSettings?.Dispose();
        originalModuleSettings = null;
    }

    [TestInitialize]
    public void PrepareTest()
    {
        Step("Waiting for the Always On Top process");
        Assert.IsTrue(
            WaitForProcess(expected: true, timeoutMs: 15_000),
            $"{AlwaysOnTopProcessName} did not start from the deterministic enabled-module baseline.");

        Step("Applying the default Always On Top test settings");
        AlwaysOnTopSettingsSeed.ApplyBaseline();
        WaitForSettingsReloadWithoutObservableSignal();
    }

    [TestCleanup]
    public async Task CleanupTest()
    {
        await CaptureFailureArtifactsBeforeCleanupAsync(TimeSpan.FromSeconds(2));

        CleanupExtraVirtualDesktop();

        // Exclusive D3D device release can fail transiently while Windows leaves full-screen mode.
        for (var attempt = 1; attempt <= 2 && fullScreen is not null; attempt++)
        {
            try
            {
                fullScreen.Dispose();
                fullScreen = null;
            }
            catch (Exception ex)
            {
                TestContext.WriteLine($"Full-screen fixture cleanup attempt {attempt}/2 failed: {ex.Message}");
                Thread.Sleep(250);
            }
        }

        if (fixture is not null)
        {
            try
            {
                if (fixture.IsPinned)
                {
                    // Destroying a pinned target can race the product's border timer with a stale HWND.
                    Step("Cleanup: unpinning the fixture before destroying its window");
                    TogglePin(expected: false);
                    _ = WaitForBorder(expected: false, timeoutMs: 5_000);
                }
            }
            catch (Exception ex)
            {
                TestContext.WriteLine($"Pinned fixture cleanup failed: {ex.Message}");
            }
        }

        fixture?.Dispose();
        fixture = null;

        try
        {
            AlwaysOnTopSettingsSeed.ApplyBaseline();
        }
        catch (Exception ex)
        {
            TestContext.WriteLine($"Always On Top settings cleanup failed: {ex.Message}");
        }
    }

    [TestMethod]
    [TestCategory("AlwaysOnTop")]
    [TestCategory("Always On Top #1")]
    [TestCategory("Always On Top #2")]
    public void PinAndUnpinChangesTopmostStateAndBorder()
    {
        fixture = TestWindow.Create("Always On Top pin and border fixture");
        AssertPinState(expected: false);
        AssertBorderState(expected: false);

        TogglePin(expected: true);
        AssertPinState(expected: true);
        AssertBorderState(expected: true);

        TogglePin(expected: false);
        AssertPinState(expected: false);
        AssertBorderState(expected: false);
    }

    [TestMethod]
    [TestCategory("AlwaysOnTop")]
    [TestCategory("Always On Top #3")]
    public void BorderTracksVirtualDesktop()
    {
        fixture = TestWindow.Create("Always On Top virtual desktop fixture");
        TogglePin(expected: true);
        var originalBorder = WaitForBorder(expected: true);

        Step("Creating and switching to a new virtual desktop");
        KeyboardHelper.SendKeys(Key.LWin, Key.Ctrl, Key.D);
        extraVirtualDesktopCreated = true;
        var switchedAway = WaitHelper.WaitForStable(
            () => VirtualDesktopHelper.IsWindowOnCurrentDesktop(fixture.Handle),
            isCurrent => isCurrent == false,
            timeoutMS: 10_000,
            requiredConsecutiveMatches: 2,
            pollIntervalMS: 100);
        Assert.IsTrue(switchedAway.Succeeded, "Windows did not switch away from the fixture's original virtual desktop.");
        AssertBorderState(expected: false, timeoutMs: 15_000);
        AssertPinState(expected: true);

        Step("Returning to the fixture's original virtual desktop");
        KeyboardHelper.SendKeys(Key.LWin, Key.Ctrl, Key.Left);
        var switchedBack = WaitHelper.WaitForStable(
            () => VirtualDesktopHelper.IsWindowOnCurrentDesktop(fixture.Handle),
            isCurrent => isCurrent == true,
            timeoutMS: 10_000,
            requiredConsecutiveMatches: 2,
            pollIntervalMS: 100);
        Assert.IsTrue(switchedBack.Succeeded, "Windows did not return to the fixture's original virtual desktop.");
        var returnedBorder = WaitForBorder(expected: true, timeoutMs: 15_000);
        Assert.AreEqual(originalBorder.Width, returnedBorder.Width, "The border width changed after returning to the original desktop.");
        Assert.AreEqual(originalBorder.Height, returnedBorder.Height, "The border height changed after returning to the original desktop.");
    }

    [TestMethod]
    [TestCategory("AlwaysOnTop")]
    [TestCategory("Always On Top #4")]
    public void BorderTracksMinimizeAndMaximize()
    {
        fixture = TestWindow.Create("Always On Top window state fixture");
        TogglePin(expected: true);
        _ = WaitForBorder(expected: true);

        Step("Minimizing the pinned fixture window");
        WindowHelper.MinimizeWindow(fixture.Handle);
        Assert.IsTrue(
            WaitHelper.WaitForStable(
                () => fixture.IsMinimized,
                minimized => minimized,
                timeoutMS: 5_000,
                requiredConsecutiveMatches: 2,
                pollIntervalMS: 100).Succeeded,
            "The fixture did not reach the minimized state.");
        AssertBorderState(expected: false, timeoutMs: 10_000);
        AssertPinState(expected: true);

        Step("Restoring the pinned fixture window");
        WindowHelper.RestoreWindow(fixture.Handle);
        Assert.IsTrue(
            WaitHelper.WaitForStable(
                () => fixture.IsMinimized,
                minimized => !minimized,
                timeoutMS: 5_000,
                requiredConsecutiveMatches: 2,
                pollIntervalMS: 100).Succeeded,
            "The fixture did not restore from its minimized state.");
        fixture.Focus();
        _ = WaitForBorder(expected: true, timeoutMs: 10_000);
        AssertPinState(expected: true);

        Step("Maximizing the pinned fixture window");
        WindowHelper.MaximizeWindow(fixture.Handle);
        Assert.IsTrue(
            WaitHelper.WaitForStable(
                () => fixture.IsMaximized,
                maximized => maximized,
                timeoutMS: 5_000,
                requiredConsecutiveMatches: 2,
                pollIntervalMS: 100).Succeeded,
            "The fixture did not reach the maximized state.");
        var maximizedBorder = WaitForBorder(expected: true, timeoutMs: 10_000);
        AssertPinState(expected: true);
        AssertBorderSurroundsVisibleFrame(maximizedBorder);
    }

    [TestMethod]
    [TestCategory("AlwaysOnTop")]
    [TestCategory("Always On Top #5")]
    public void BorderColorAndThicknessUpdateWhilePinned()
    {
        const int initialThickness = 4;
        const int updatedThickness = 18;
        var initialColor = Color.FromArgb(0x00, 0xCC, 0x66);
        var updatedColor = Color.FromArgb(0xFF, 0x00, 0xFF);

        Step("Applying the initial custom border color and thickness");
        AlwaysOnTopSettingsSeed.Apply(
            ("frame-enabled", true),
            ("frame-thickness", initialThickness),
            ("frame-color", "#00CC66"),
            ("frame-opacity", 100),
            ("frame-accent-color", false),
            ("round-corners-enabled", false),
            ("sound-enabled", false));

        fixture = TestWindow.Create("Always On Top border settings fixture");
        TogglePin(expected: true);
        var initialBorder = WaitForBorder(expected: true);
        Assert.IsTrue(
            WaitForBorderColor(initialColor, initialThickness, timeoutMs: 10_000),
            "The initial custom green border color was not visible.");

        Step("Changing the pinned window border to magenta and increasing its thickness");
        AlwaysOnTopSettingsSeed.Apply(
            ("frame-thickness", updatedThickness),
            ("frame-color", "#FF00FF"));

        var update = WaitHelper.WaitForStable(
            observe: () =>
            {
                var border = FindNearestVisibleBorder();
                return (
                    Border: border,
                    HasUpdatedColor: border.Hwnd != IntPtr.Zero && BorderContainsColor(border, updatedColor, updatedThickness));
            },
            isMatch: state =>
                state.Border.Hwnd != IntPtr.Zero
                && state.Border.Width >= initialBorder.Width + ((updatedThickness - initialThickness) * 2) - 4
                && state.Border.Height >= initialBorder.Height + ((updatedThickness - initialThickness) * 2) - 4
                && state.HasUpdatedColor,
            timeoutMS: 15_000,
            requiredConsecutiveMatches: 2,
            pollIntervalMS: 250);

        var updateDiagnostics = FormattableString.Invariant(
            $"Last border={update.LastObservation.Border.Width}x{update.LastObservation.Border.Height}, magenta={update.LastObservation.HasUpdatedColor}.");
        Assert.IsTrue(update.Succeeded, $"The live border update did not settle. {updateDiagnostics}");
    }

    [TestMethod]
    [TestCategory("AlwaysOnTop")]
    [TestCategory("Always On Top #6")]
    public void SoundSettingControlsPlaybackAttempt()
    {
        fixture = TestWindow.Create("Always On Top sound fixture");
        using var soundFile = SoundFileFixture.Create(TestContext.WriteLine);
        AlwaysOnTopSettingsSeed.Apply(("sound-enabled", true), ("frame-enabled", false));
        WaitForSettingsReloadWithoutObservableSignal();

        // The oplock proves that the product opened the sound file; speaker output is intentionally
        // not asserted because Hyper-V and CI agents do not expose a stable audio endpoint.
        using (var enabledWatcher = SoundPlaybackWatcher.Create(soundFile.FilePath, TestContext.WriteLine))
        {
            Assert.IsFalse(
                enabledWatcher.WaitForAccess(timeoutMs: 250),
                "Another process opened the pin sound file before Always On Top was triggered.");
            Step($"Watching access to '{enabledWatcher.FilePath}' with sound enabled");
            TogglePin(expected: true);
            Assert.IsTrue(
                enabledWatcher.WaitForAccess(timeoutMs: 5_000),
                "Enabling sound did not cause Always On Top to open its pin sound file.");
        }

        Step("Disabling sound and returning the fixture to an unpinned state");
        AlwaysOnTopSettingsSeed.Apply(("sound-enabled", false));
        WaitForSettingsReloadWithoutObservableSignal();
        TogglePin(expected: false);

        using (var disabledWatcher = SoundPlaybackWatcher.Create(soundFile.FilePath, TestContext.WriteLine))
        {
            Assert.IsFalse(
                disabledWatcher.WaitForAccess(timeoutMs: 250),
                "Another process opened the pin sound file before the disabled-sound check.");
            Step($"Watching access to '{disabledWatcher.FilePath}' with sound disabled");
            TogglePin(expected: true);
            Assert.IsFalse(
                disabledWatcher.WaitForAccess(timeoutMs: 3_000),
                "Always On Top opened its pin sound file even though sound was disabled.");
        }

        TestContext.WriteLine(
            "Playback gating is validated by observing the relative sound file opened by PlaySound; " +
            "the Hyper-V guest has no stable audio-capture endpoint for asserting speaker output.");
    }

    [TestMethod]
    [TestCategory("AlwaysOnTop")]
    [TestCategory("Always On Top #7")]
    [TestCategory("Always On Top #8")]
    public void ExcludedApplicationCannotBePinnedAndIsUnpinnedWhenAdded()
    {
        fixture = TestWindow.Create("Always On Top exclusion fixture");

        Step($"Excluding the fixture process '{fixture.ProcessFileName}'");
        AlwaysOnTopSettingsSeed.Apply(("excluded-apps", fixture.ProcessFileName));
        WaitForSettingsReloadWithoutObservableSignal();
        fixture.Focus();
        KeyboardHelper.SendKeys(ActivationShortcut);
        Thread.Sleep(2_000);
        AssertPinState(expected: false);
        AssertBorderState(expected: false);

        Step("Removing the exclusion and pinning the fixture");
        AlwaysOnTopSettingsSeed.Apply(("excluded-apps", string.Empty));
        TogglePin(expected: true);
        AssertBorderState(expected: true);

        Step("Excluding the already pinned fixture");
        AlwaysOnTopSettingsSeed.Apply(("excluded-apps", fixture.ProcessFileName));
        AssertPinState(expected: false, timeoutMs: 10_000);
        AssertBorderState(expected: false, timeoutMs: 10_000);
    }

    [TestMethod]
    [TestCategory("AlwaysOnTop")]
    [TestCategory("Always On Top #9")]
    public void GameModeSettingBlocksPinning()
    {
        fixture = TestWindow.Create("Always On Top Direct3D game fixture");

        Step("Entering Direct3D full-screen exclusive mode");
        fullScreen = Direct3DFullScreenScope.Enter(fixture);
        var gameMode = WaitHelper.WaitForStable(
            Direct3DFullScreenScope.QueryUserNotificationState,
            state => state == Direct3DFullScreenScope.UserNotificationState.RunningDirect3DFullScreen,
            timeoutMS: 10_000,
            requiredConsecutiveMatches: 2,
            pollIntervalMS: 250);
        Assert.IsTrue(
            gameMode.Succeeded,
            $"Windows did not report Direct3D full-screen Game Mode. Last state: {gameMode.LastObservation}.");

        Step("Enabling the Always On Top Game Mode block");
        AlwaysOnTopSettingsSeed.Apply(("do-not-activate-on-game-mode", true));
        WaitForSettingsReloadWithoutObservableSignal();
        fixture.Focus();
        KeyboardHelper.SendKeys(ActivationShortcut);
        Thread.Sleep(2_000);
        Assert.IsFalse(fixture.IsPinned, "Always On Top marked the Direct3D fixture as pinned while Game Mode blocking was enabled.");

        Step("Leaving Direct3D full-screen mode");
        fullScreen.Dispose();
        fullScreen = null;
        var normalMode = WaitHelper.WaitForStable(
            Direct3DFullScreenScope.QueryUserNotificationState,
            state => state != Direct3DFullScreenScope.UserNotificationState.RunningDirect3DFullScreen,
            timeoutMS: 10_000,
            requiredConsecutiveMatches: 2,
            pollIntervalMS: 250);
        Assert.IsTrue(normalMode.Succeeded, "Windows continued reporting Direct3D full-screen mode after the fixture exited it.");
        Assert.IsFalse(fixture.IsTopmost, "The Direct3D fixture remained topmost after leaving full-screen mode.");

        Step("Disabling the Game Mode block and verifying the same shortcut outside Game Mode");
        AlwaysOnTopSettingsSeed.Apply(("do-not-activate-on-game-mode", false));

        TogglePin(expected: true);
        AssertPinState(expected: true);
    }

    private void TogglePin(bool expected)
    {
        var targetState = expected ? "pinned" : "unpinned";
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            if (PinStateMatches(expected))
            {
                return;
            }

            Step($"Sending {string.Join(" + ", ActivationShortcut)} to make the fixture {targetState} (attempt {attempt}/3)");
            fixture!.Focus();
            KeyboardHelper.SendKeys(ActivationShortcut);

            if (WaitForPinState(expected, timeoutMs: 3_000))
            {
                return;
            }
        }

        AssertPinState(expected);
    }

    private void AssertPinState(bool expected, int timeoutMs = 7_000)
    {
        var succeeded = WaitForPinState(expected, timeoutMs);
        var diagnostics = $"PinnedProperty={fixture!.IsPinned}, TopmostStyle={fixture.IsTopmost}.";
        Assert.IsTrue(succeeded, $"The fixture did not reach the expected pin state. {diagnostics}");
    }

    private bool WaitForPinState(bool expected, int timeoutMs)
    {
        var result = WaitHelper.WaitForStable(
            observe: () => (fixture!.IsPinned, fixture.IsTopmost),
            isMatch: state => state.IsPinned == expected && state.IsTopmost == expected,
            timeoutMS: timeoutMs,
            requiredConsecutiveMatches: 2,
            pollIntervalMS: 100);
        return result.Succeeded;
    }

    private bool PinStateMatches(bool expected)
    {
        return fixture!.IsPinned == expected && fixture.IsTopmost == expected;
    }

    private void AssertBorderState(bool expected, int timeoutMs = 7_000)
    {
        var border = WaitForBorder(expected, timeoutMs);
        if (expected)
        {
            Assert.AreNotEqual(IntPtr.Zero, border.Hwnd, "The Always On Top border window was not visible.");
        }
    }

    private WindowControl.ProcessWindow WaitForBorder(bool expected, int timeoutMs = 7_000)
    {
        var result = WaitHelper.WaitForStable(
            FindNearestVisibleBorder,
            border => (border.Hwnd != IntPtr.Zero) == expected,
            timeoutMS: timeoutMs,
            requiredConsecutiveMatches: 2,
            pollIntervalMS: 100);

        var diagnostics = FormattableString.Invariant(
            $"Last HWND={result.LastObservation.Hwnd}, size={result.LastObservation.Width}x{result.LastObservation.Height}.");
        Assert.IsTrue(result.Succeeded, $"The Always On Top border did not reach expected visibility within {timeoutMs} ms. {diagnostics}");
        return result.LastObservation;
    }

    private WindowControl.ProcessWindow FindNearestVisibleBorder()
    {
        if (fixture is null)
        {
            return default;
        }

        var targetCenter = WindowHelper.GetWindowCenter(fixture.Handle);
        var display = WindowHelper.GetDisplaySize();
        var candidate = WindowControl.EnumerateAllWindows()
            .Where(window =>
                window.IsVisible
                && window.ClassName.Equals(BorderWindowClass, StringComparison.OrdinalIgnoreCase)
                && IsOnScreen(window.Hwnd, display))
            .OrderBy(
                window =>
                {
                    var center = WindowHelper.GetWindowCenter(window.Hwnd);
                    return Math.Abs(center.CenterX - targetCenter.CenterX) + Math.Abs(center.CenterY - targetCenter.CenterY);
                })
            .FirstOrDefault();
        if (candidate.Hwnd == IntPtr.Zero)
        {
            return default;
        }

        var candidateCenter = WindowHelper.GetWindowCenter(candidate.Hwnd);
        var centerDistance = Math.Abs(candidateCenter.CenterX - targetCenter.CenterX) + Math.Abs(candidateCenter.CenterY - targetCenter.CenterY);

        // Border and target centers should coincide; tolerate DWM shadow and DPI rounding only.
        return centerDistance <= 32 ? candidate : default;
    }

    private static bool IsOnScreen(IntPtr window, (int Width, int Height) display)
    {
        var bounds = WindowHelper.GetWindowBounds(window);
        return bounds.Right > 0
            && bounds.Bottom > 0
            && bounds.Left < display.Width
            && bounds.Top < display.Height;
    }

    private void AssertBorderSurroundsVisibleFrame(WindowControl.ProcessWindow border)
    {
        var frame = fixture!.VisibleFrameBounds;
        var borderBounds = WindowHelper.GetWindowBounds(border.Hwnd);
        var leftMargin = frame.Left - borderBounds.Left;
        var topMargin = frame.Top - borderBounds.Top;
        var rightMargin = borderBounds.Right - frame.Right;
        var bottomMargin = borderBounds.Bottom - frame.Bottom;

        var marginDiagnostics = FormattableString.Invariant(
            $"Margins: L={leftMargin}, T={topMargin}, R={rightMargin}, B={bottomMargin}.");

        // Bound the maximized DWM inset while requiring near-symmetric opposite edges.
        var surroundsFrame = leftMargin is > 0 and <= 64
            && topMargin is > 0 and <= 64
            && rightMargin is > 0 and <= 64
            && bottomMargin is > 0 and <= 64;
        Assert.IsTrue(surroundsFrame, $"The border did not surround the maximized DWM frame. {marginDiagnostics}");
        Assert.IsTrue(Math.Abs(leftMargin - rightMargin) <= 4, "The maximized border was not horizontally symmetric.");
        Assert.IsTrue(Math.Abs(topMargin - bottomMargin) <= 4, "The maximized border was not vertically symmetric.");
    }

    private bool WaitForBorderColor(Color expectedColor, int thickness, int timeoutMs)
    {
        var result = WaitHelper.WaitForStable(
            observe: () =>
            {
                var border = FindNearestVisibleBorder();
                return border.Hwnd != IntPtr.Zero && BorderContainsColor(border, expectedColor, thickness);
            },
            isMatch: matches => matches,
            timeoutMS: timeoutMs,
            requiredConsecutiveMatches: 2,
            pollIntervalMS: 250);
        return result.Succeeded;
    }

    private static bool BorderContainsColor(WindowControl.ProcessWindow border, Color expectedColor, int thickness)
    {
        var bounds = WindowHelper.GetWindowBounds(border.Hwnd);
        var width = bounds.Right - bounds.Left;
        var height = bounds.Bottom - bounds.Top;
        if (width <= 0 || height <= 0)
        {
            return false;
        }

        using var image = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(image))
        {
            graphics.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, image.Size);
        }

        var bitmapData = image.LockBits(
            new Rectangle(0, 0, width, height),
            ImageLockMode.ReadOnly,
            PixelFormat.Format32bppArgb);
        var stride = Math.Abs(bitmapData.Stride);
        var pixels = new byte[stride * height];
        try
        {
            for (var y = 0; y < height; y++)
            {
                Marshal.Copy(IntPtr.Add(bitmapData.Scan0, y * bitmapData.Stride), pixels, y * stride, stride);
            }
        }
        finally
        {
            image.UnlockBits(bitmapData);
        }

        var band = Math.Min(Math.Max(thickness + 2, 4), Math.Min(width, height) / 3);
        var matches = 0;
        var samples = 0;
        for (var x = 2; x < width - 2; x += 4)
        {
            for (var y = 1; y < band; y += 2)
            {
                samples += 2;
                if (PixelMatches(pixels, stride, x, y, expectedColor))
                {
                    matches++;
                }

                if (PixelMatches(pixels, stride, x, height - 1 - y, expectedColor))
                {
                    matches++;
                }
            }
        }

        for (var y = band; y < height - band; y += 4)
        {
            for (var x = 1; x < band; x += 2)
            {
                samples += 2;
                if (PixelMatches(pixels, stride, x, y, expectedColor))
                {
                    matches++;
                }

                if (PixelMatches(pixels, stride, width - 1 - x, y, expectedColor))
                {
                    matches++;
                }
            }
        }

        // Require a meaningful share of the sampled edge band while allowing DWM-composited pixels.
        return matches >= Math.Max(8, samples / 12);
    }

    private static bool PixelMatches(byte[] pixels, int stride, int x, int y, Color expected)
    {
        var rowOffset = y * stride;
        var pixelOffset = rowOffset + (x * 4);
        const int tolerance = 24;

        // Absorb small DWM blending and color-management differences at the rendered edge.
        return Math.Abs(pixels[pixelOffset + 2] - expected.R) <= tolerance
            && Math.Abs(pixels[pixelOffset + 1] - expected.G) <= tolerance
            && Math.Abs(pixels[pixelOffset] - expected.B) <= tolerance;
    }

    private static bool WaitForProcess(bool expected, int timeoutMs)
    {
        var result = WaitHelper.WaitForStable(
            observe: () =>
            {
                var processes = Process.GetProcessesByName(AlwaysOnTopProcessName);
                try
                {
                    return processes.Length > 0;
                }
                finally
                {
                    foreach (var process in processes)
                    {
                        process.Dispose();
                    }
                }
            },
            isMatch: present => present == expected,
            timeoutMS: timeoutMs,
            requiredConsecutiveMatches: 2,
            pollIntervalMS: 100);
        return result.Succeeded;
    }

    private void CleanupExtraVirtualDesktop()
    {
        if (!extraVirtualDesktopCreated)
        {
            return;
        }

        var fixtureHandle = fixture?.Handle;
        if (fixtureHandle is null)
        {
            TestContext.WriteLine("Virtual desktop cleanup has no fixture handle; it will not close any desktop.");
            return;
        }

        try
        {
            var fixtureOnCurrentDesktop = VirtualDesktopHelper.IsWindowOnCurrentDesktop(fixtureHandle.Value);
            if (fixtureOnCurrentDesktop)
            {
                KeyboardHelper.SendKeys(Key.LWin, Key.Ctrl, Key.Right);
                var switchedToCreatedDesktop = WaitHelper.WaitForStable(
                    () => VirtualDesktopHelper.IsWindowOnCurrentDesktop(fixtureHandle.Value),
                    isCurrent => isCurrent == false,
                    timeoutMS: 10_000,
                    requiredConsecutiveMatches: 2,
                    pollIntervalMS: 100);
                if (!switchedToCreatedDesktop.Succeeded)
                {
                    TestContext.WriteLine("Virtual desktop cleanup did not reach the created desktop; it will not close any desktop.");
                    return;
                }
            }

            KeyboardHelper.SendKeys(Key.LWin, Key.Ctrl, Key.F4);
            var returnedToOriginalDesktop = WaitHelper.WaitForStable(
                () => VirtualDesktopHelper.IsWindowOnCurrentDesktop(fixtureHandle.Value),
                isCurrent => isCurrent == true,
                timeoutMS: 10_000,
                requiredConsecutiveMatches: 2,
                pollIntervalMS: 100);
            if (!returnedToOriginalDesktop.Succeeded)
            {
                TestContext.WriteLine("Virtual desktop cleanup could not confirm a return to the original desktop.");
            }
        }
        catch (Exception ex)
        {
            TestContext.WriteLine($"Virtual desktop cleanup failed: {ex.Message}");
        }
        finally
        {
            extraVirtualDesktopCreated = false;
        }
    }

    private void Step(string message)
    {
        var timestamp = DateTime.UtcNow.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
        TestContext.WriteLine($"[{timestamp}] {message}");
    }

    private static void WaitForSettingsReloadWithoutObservableSignal()
    {
        Thread.Sleep(SettingsReloadDelayMs);
    }
}

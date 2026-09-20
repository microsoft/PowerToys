// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;
using System.Drawing.Imaging;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.ZoomIt.UITests;

[TestClass]
[TestCategory("ZoomIt")]
[DoNotParallelize]
public sealed partial class ZoomItTests : UITestBase
{
    private static readonly string[] OwnedProcessNames = ["PowerToys", "PowerToys.Settings", ZoomItUi.ProcessName];
    private static bool shellPrepared;
    private DesktopFixture? desktop;
    private ZoomItUi ui = null!;
    private string clipboardText = string.Empty;

    public ZoomItTests()
        : base(PowerToysModule.PowerToysSettings, enableModules: ["ZoomIt"])
    {
    }

    protected override IReadOnlyList<string> StaleProcessNames => OwnedProcessNames;

    protected override void PrepareTestState()
    {
        ZoomItState.RestorePending();
        if (!shellPrepared)
        {
            Assert.IsTrue(ExplorerControl.RestartShell(timeoutMS: 60_000, log: TestContext.WriteLine), "Could not establish a fresh notification area before launching ZoomIt.");
            shellPrepared = true;
        }

        clipboardText = ClipboardHelper.GetText();
        var backup = CaptureEvidencePath("zoomit-registry-original.json");
        ZoomItState.CaptureForTest(backup);
        TestContext.WriteLine($"Original ZoomIt registry values are preserved in {backup}.");
    }

    [TestInitialize]
    public async Task NavigateToZoomIt()
    {
        try
        {
            ui = new ZoomItUi(TestContext);
            ui.Navigate();
        }
        catch
        {
            await CaptureFailureArtifactsAsync();
            try
            {
                StopOwnedProcesses();
            }
            finally
            {
                try
                {
                    ZoomItState.RestorePending();
                }
                finally
                {
                    Dispose();
                }
            }

            throw;
        }
    }

    [ClassCleanup(ClassCleanupBehavior.EndOfClass)]
    public static void RestoreRegistryAfterInitializationFailure()
    {
        try
        {
            StopOwnedProcesses();
        }
        finally
        {
            ZoomItState.RestorePending();
        }
    }

    private static void StopOwnedProcesses()
    {
        var failed = OwnedProcessNames.Where(name => !WindowControl.TryKillProcessTreeByNameAndWait(name)).ToArray();
        Assert.IsEmpty(failed, $"Could not stop processes before restoring ZoomIt state: {string.Join(", ", failed)}.");
    }

    [TestCleanup]
    public async Task RestoreZoomIt()
    {
        await CaptureFailureArtifactsBeforeCleanupAsync();
        try
        {
            CloseNotepadDocument();
            WindowControl.TryCloseByApp(ZoomItUi.ProcessName, window => window.ClassName == "#32770");
            WindowControl.TryCloseByApp("PowerToys.Settings", window => window.ClassName == "#32770");

            // Remove the Shell icon before stopping its owner; abrupt termination leaves ghost
            // notifications in persistent desktops until the mouse next passes over them.
            ui.SetToggle("ZoomItToggleShowTrayIcon", false, "ShowTrayIcon");
            ui.SetToggle("ZoomItEnableToggleControlHeaderText", false);
            ZoomItUi.WaitForProcess(false);
        }
        finally
        {
            try
            {
                StopOwnedProcesses();
                desktop?.Dispose();
                CleanupCaptureAndTray();
                CleanupBreakAndDemo();
            }
            finally
            {
                try
                {
                    ZoomItState.RestorePending();
                }
                finally
                {
                    Assert.IsTrue(
                        clipboardText.Length == 0 ? ClipboardHelper.Clear() : ClipboardHelper.SetText(clipboardText),
                        "Could not restore clipboard text.");
                }
            }
        }
    }

    [TestMethod]
    public void SettingsEnableDisableControlsRuntime()
    {
        ui.SetToggle("ZoomItEnableToggleControlHeaderText", false);
        ZoomItUi.WaitForProcess(false);
        ui.SetToggle("ZoomItEnableToggleControlHeaderText", true);
        ZoomItUi.WaitForProcess(true);
        var shortcut = ui.Shortcut("ZoomItZoomShortcut");
        desktop = new DesktopFixture();
        ui.Activate(shortcut);
        AssertMagnification(2);
        ui.Exit();
    }

    [TestMethod]
    public void ZoomFreezesDesktopAndBothExitMethodsWork()
    {
        var shortcut = ui.Shortcut("ZoomItZoomShortcut");
        desktop = new DesktopFixture();
        ui.Activate(shortcut);
        AssertMagnification(2);
        desktop.ChangeMarker(Color.Blue);
        AssertMagnification(2);
        SaveDesktop("static-zoom");
        ui.Exit();
        AssertCenterColor(Color.Blue);

        desktop.ChangeMarker(DesktopFixture.MarkerColor);
        desktop.Show();
        ui.Activate(shortcut);
        AssertMagnification(2);
        ui.Exit(shortcut);
        AssertMagnification(1);
    }

    [TestMethod]
    public void LiveZoomMagnifiesAndUpdatesDesktop()
    {
        var shortcut = ui.Shortcut("ZoomItLiveZoomShortcut");
        desktop = new DesktopFixture();
        ui.Activate(shortcut, ZoomItUi.LiveZoomClass);
        AssertMagnification(2);
        desktop.ChangeMarker(Color.Blue);
        AssertCenterColor(Color.Blue);
        SaveDesktop("live-zoom-updated");
        ui.Exit(shortcut, ZoomItUi.LiveZoomClass);
    }

    [TestMethod]
    public void DrawWithoutZoomRendersStrokeAndEscapeExits()
    {
        var shortcut = ui.Shortcut("ZoomItDrawShortcut");
        desktop = new DesktopFixture();
        var overlay = ui.Activate(shortcut);
        AssertMagnification(1);
        Assert.IsTrue(WindowControl.WaitForForeground(new IntPtr(overlay.WindowHandle), 10_000), "Draw must own foreground.");
        var center = desktop.Center;
        ui.Step("Drawing a red stroke across the source surface");
        KeyboardHelper.SendKeys(Key.R);
        MouseHelper.Drag(center.X - 150, center.Y - 100, center.X + 150, center.Y - 100, steps: 30);
        var result = WaitHelper.WaitForStable(
            () =>
            {
                using var bitmap = DesktopFixture.Capture();
                return DesktopFixture.ColorBounds(bitmap, color => color.R > 220 && color.G < 40 && color.B < 40);
            },
            bounds => bounds.Width >= 280 && bounds.Height >= 3,
            10_000,
            2);
        Assert.IsTrue(result.Succeeded, $"The red stroke did not render: {result.LastObservation}.");
        SaveDesktop("draw-stroke");
        ui.Exit();
        AssertMagnification(1);
    }

    [TestMethod]
    [DataRow(0, 1.25)]
    [DataRow(5, 4.0)]
    public void InitialMagnificationSettingChangesRenderedScale(int sliderValue, double scale)
    {
        ui.SetSlider("ZoomItSliderInitialMagnification", sliderValue, "ZoominSliderLevel", sliderValue);
        var shortcut = ui.Shortcut("ZoomItZoomShortcut");
        desktop = new DesktopFixture();
        ui.Activate(shortcut);
        AssertMagnification(scale);
        SaveDesktop($"zoom-{scale}");
        ui.Exit();
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AnimationSettingPreservesZoomAndExit(bool animate)
    {
        ui.SetCheck("ZoomItToggleAnimateZoom", animate, "AnimnateZoom");
        var shortcut = ui.Shortcut("ZoomItZoomShortcut");
        desktop = new DesktopFixture();
        ui.Activate(shortcut);
        AssertMagnification(2);
        ui.Exit();
        AssertMagnification(1);
        TestContext.WriteLine("Checklist 11: setting delivery and settled zoom are automated; animation smoothness requires visual review.");
    }

    [TestMethod]
    public void BreakTimerRendersAndEscapeExits()
    {
        var shortcut = ui.Shortcut("ZoomItBreakShortcut");
        desktop = new DesktopFixture();
        ui.Activate(shortcut);
        var bounds = WaitForTimerPixels();
        Assert.IsTrue(Math.Abs(((bounds.Left + bounds.Right) / 2) - desktop.Center.X) < 30, $"Timer was not centered: {bounds}.");
        SaveDesktop("break-timer");
        ui.Exit();
    }

    private Rectangle WaitForTimerPixels()
    {
        var screen = System.Windows.Forms.SystemInformation.PrimaryMonitorSize;
        var workArea = System.Windows.Forms.Screen.PrimaryScreen!.WorkingArea;
        var result = WaitHelper.WaitForStable(
            () =>
            {
                using var bitmap = DesktopFixture.Capture();
                return DesktopFixture.ColorBounds(bitmap, color => color.R > 235 && color.G > 235 && color.B > 235, workArea);
            },
            bounds => bounds.Width > 100 && bounds.Width < screen.Width * 0.8 && bounds.Height > 40 && bounds.Height < screen.Height / 2,
            15_000,
            3);
        Assert.IsTrue(result.Succeeded, $"Break timer digits did not render: {result.LastObservation}.");
        return result.LastObservation;
    }

    private void AssertMagnification(double scale)
    {
        Assert.IsNotNull(desktop);
        var expected = DesktopFixture.MarkerSize * scale;
        var result = WaitHelper.WaitForStable(desktop.ReadMarkerWidth, width => Math.Abs(width - expected) <= 5, 15_000, 3);
        Assert.IsTrue(result.Succeeded, $"Expected {scale}x magnification ({expected}px marker), actual {result.LastObservation}px.");
    }

    private void AssertCenterColor(Color color)
    {
        Assert.IsNotNull(desktop);
        var result = WaitHelper.WaitForStable(
            () =>
            {
                using var bitmap = DesktopFixture.Capture();
                return bitmap.GetPixel(desktop.Center.X - 10, desktop.Center.Y - 10);
            },
            actual => DesktopFixture.IsColor(actual, color),
            10_000,
            3);
        Assert.IsTrue(result.Succeeded, $"Expected visible marker {color}, actual {result.LastObservation}.");
    }

    private void SaveDesktop(string name)
    {
        var directory = Path.Combine(Path.GetDirectoryName(TestContext.TestRunDirectory!)!, "ZoomItEvidence", TestContext.TestName!);
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, name + ".png");
        using var bitmap = DesktopFixture.Capture();
        bitmap.Save(path, ImageFormat.Png);
        TestContext.AddResultFile(path);
    }
}

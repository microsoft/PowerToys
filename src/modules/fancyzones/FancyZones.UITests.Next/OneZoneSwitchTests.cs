// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using FancyZones.UITests.Utils;
using FancyZonesEditorCommon.Data;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static FancyZones.UITests.Utils.FancyZonesSettingsSeed;

namespace FancyZones.UITests;

/// <summary>
/// Port of the legacy <c>OneZoneSwitchTests</c>: snap two windows into the same zone and verify the
/// "switch between windows in the current zone" shortcut.
/// </summary>
/// <remarks>
/// The legacy suite snapped the Hosts File Editor and the Settings window. Both are WinUI 3 windows
/// that move themselves rather than running the standard <c>DefWindowProc</c> move loop, so
/// FancyZones never observes the drag and neither window ever snaps (see <see cref="DragWindowTests"/>).
/// The port snaps two File Explorer windows opened at different folders instead: classic Win32
/// windows with distinct titles, which is what the switching assertions need. "Both landed in the
/// same zone" is asserted from the window rectangles rather than <c>app-zone-history.json</c>, since
/// two Explorer windows share one app-path entry.
/// </remarks>
[TestClass]
public class OneZoneSwitchTests : UITestBase
{
    /// <summary>Windows may inset a snapped window slightly; compare rectangles with a tolerance.</summary>
    private const int SnapTolerance = 24;

    private readonly FancyZonesFiles files = new();

    public OneZoneSwitchTests()
        : base(PowerToysModule.PowerToysSettings, WindowSize.UnSpecified, [ModuleName])
    {
    }

    protected override IReadOnlyList<string> StaleProcessNames => FancyZonesTestHelper.StaleProcessNames;

    [TestCleanup]
    public async Task CleanupTest()
    {
        await CaptureFailureArtifactsBeforeCleanupAsync();

        MouseHelper.LeftUp();
        KeyboardHelper.ReleaseKey(Key.LShift);

        FancyZonesTestHelper.CloseLayoutEditor(this);
        FancyZonesTestHelper.CloseExplorerWindows();
        files.RestoreAll();
    }

    /// <summary>
    /// Verifies that after snapping two windows into one zone, Win+PageDown switches the active window
    /// back to the previously snapped one.
    /// </summary>
    [TestMethod]
    [TestCategory("FancyZones")]
    [TestCategory("FancyZones #Switch between windows in the current zone #1")]
    public void TestSwitchWindow()
    {
        Arrange(windowSwitching: true);
        var (previousWindow, activeWindow) = SnapBothWindowsToOneZone();

        Assert.IsTrue(
            FancyZonesTestHelper.WaitForForegroundTitle(activeWindow, 5_000),
            $"The last snapped window ('{activeWindow}') should be active, but '{FancyZonesTestHelper.GetForegroundWindowTitle()}' is.");

        FancyZonesTestHelper.Step(this, "Sending Win+PageDown to switch within the zone");
        KeyboardHelper.SendKeys(Key.LWin, Key.PageDown);

        Assert.IsTrue(
            FancyZonesTestHelper.WaitForForegroundTitle(previousWindow, 5_000),
            $"Win+PageDown should switch to '{previousWindow}', but '{FancyZonesTestHelper.GetForegroundWindowTitle()}' is active.");
    }

    /// <summary>
    /// Verifies that a window remains correctly snapped after switching virtual desktops and can still
    /// be switched with Win+PageDown.
    /// </summary>
    [TestMethod]
    [TestCategory("FancyZones")]
    [TestCategory("FancyZones #Switch between windows in the current zone #2")]
    public void TestSwitchAfterDesktopChange()
    {
        Arrange(windowSwitching: true);
        var (previousWindow, activeWindow) = SnapBothWindowsToOneZone();

        Assert.IsTrue(
            FancyZonesTestHelper.WaitForForegroundTitle(activeWindow, 5_000),
            $"The last snapped window ('{activeWindow}') should be active before changing desktops.");

        try
        {
            FancyZonesTestHelper.Step(this, "Creating a virtual desktop and returning to the original one");
            KeyboardHelper.SendKeys(Key.Ctrl, Key.LWin, Key.D);
            Thread.Sleep(1500);
            KeyboardHelper.SendKeys(Key.Ctrl, Key.LWin, Key.Left);
            Thread.Sleep(1500);

            Assert.IsTrue(
                FancyZonesTestHelper.WaitForForegroundTitle(activeWindow, 10_000),
                $"'{activeWindow}' should still be active after returning to the original desktop.");

            FancyZonesTestHelper.Step(this, "Sending Win+PageDown to switch within the zone");
            KeyboardHelper.SendKeys(Key.LWin, Key.PageDown);

            Assert.IsTrue(
                FancyZonesTestHelper.WaitForForegroundTitle(previousWindow, 5_000),
                $"Win+PageDown should switch to '{previousWindow}' after the desktop change.");
        }
        finally
        {
            CloseExtraVirtualDesktop();
        }
    }

    /// <summary>
    /// Verifies that Win+PageDown does not switch windows when the window-switching shortcut is
    /// disabled in the FancyZones settings.
    /// </summary>
    [TestMethod]
    [TestCategory("FancyZones")]
    [TestCategory("FancyZones #Switch between windows in the current zone #3")]
    public void TestSwitchShortCutDisable()
    {
        Arrange(windowSwitching: false);
        var (_, activeWindow) = SnapBothWindowsToOneZone();

        Assert.IsTrue(
            FancyZonesTestHelper.WaitForForegroundTitle(activeWindow, 5_000),
            $"The last snapped window ('{activeWindow}') should be active.");

        FancyZonesTestHelper.Step(this, "Sending Win+PageDown with window switching disabled");
        KeyboardHelper.SendKeys(Key.LWin, Key.PageDown);
        Thread.Sleep(2000);

        Assert.IsTrue(
            FancyZonesTestHelper.GetForegroundWindowTitle().Contains(activeWindow, StringComparison.OrdinalIgnoreCase),
            $"Win+PageDown must not switch windows while the shortcut is disabled, but '{FancyZonesTestHelper.GetForegroundWindowTitle()}' became active.");
    }

    private void Arrange(bool windowSwitching)
    {
        FancyZonesTestHelper.Step(this, $"Seeding a two-zone layout (window switching: {windowSwitching})");

        files.AppZoneHistory.Delete();
        files.AppliedLayouts.Delete();
        files.CustomLayouts.Write(new CustomLayouts().Serialize(LayoutFixtures.TwoZoneColumns));

        new FancyZonesSettingsSeed()
            .Set(Setting.ShiftDrag, true)
            .Set(Setting.MouseSwitch, false)
            .Set(Setting.MakeDraggedWindowTransparent, false)
            .Set(Setting.ShowZoneNumber, false)
            .Set(Setting.WindowSwitching, windowSwitching)
            .Set(Setting.AllowChildWindowSnap, true)
            .Set(Setting.AllowPopupWindowSnap, true)
            .Apply();

        // Resets the FancyZones editor toggle state, not the settings — see DragWindowTests.Arrange.
        FancyZonesTestHelper.Step(this, "Restarting PowerToys to reset the FancyZones editor toggle state");
        FancyZonesTestHelper.RestartPowerToys(this);

        FancyZonesTestHelper.EnsureFancyZonesRunning(this);
        FancyZonesTestHelper.ApplyLayoutThroughEditor(
            this,
            By.Name(FancyZonesTestHelper.LayoutName.CustomColumn),
            LayoutFixtures.CustomColumnUuid);

        WindowHelper.MinimizeWindow(new IntPtr(Session.WindowHandle));
        Thread.Sleep(500);
    }

    /// <summary>
    /// Snap two Explorer windows into the same (right) zone and confirm both landed there. Returns the
    /// two window titles, oldest first.
    /// </summary>
    private (string PreviousWindow, string ActiveWindow) SnapBothWindowsToOneZone()
    {
        var first = FancyZonesTestHelper.OpenExplorerWindow(this, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        var second = FancyZonesTestHelper.OpenExplorerWindow(this, Environment.GetFolderPath(Environment.SpecialFolder.Windows));

        var firstTitle = FancyZonesTestHelper.GetWindowTitle(first);
        var secondTitle = FancyZonesTestHelper.GetWindowTitle(second);
        Assert.AreNotEqual(
            firstTitle,
            secondTitle,
            "The two Explorer windows must have distinct titles for the switching assertions to mean anything.");

        var (targetX, targetY) = RightZoneCenter();
        SnapWindowToPoint(first, targetX, targetY, firstTitle);
        SnapWindowToPoint(second, targetX, targetY, secondTitle);

        var firstBounds = WindowHelper.GetWindowBounds(first);
        var secondBounds = WindowHelper.GetWindowBounds(second);
        FancyZonesTestHelper.Step(this, $"Snapped bounds: '{firstTitle}' {firstBounds}, '{secondTitle}' {secondBounds}");

        Assert.IsTrue(
            IsSameZone(firstBounds, secondBounds),
            $"Both windows should occupy the same zone, but '{firstTitle}' is at {firstBounds} and '{secondTitle}' is at {secondBounds}.");

        return (firstTitle, secondTitle);
    }

    private static bool IsSameZone(
        (int Left, int Top, int Right, int Bottom) a,
        (int Left, int Top, int Right, int Bottom) b) =>
        Math.Abs(a.Left - b.Left) <= SnapTolerance &&
        Math.Abs(a.Top - b.Top) <= SnapTolerance &&
        Math.Abs(a.Right - b.Right) <= SnapTolerance &&
        Math.Abs(a.Bottom - b.Bottom) <= SnapTolerance;

    /// <summary>Centre of the right-hand zone of the seeded two-column layout.</summary>
    private static (int X, int Y) RightZoneCenter()
    {
        var (_, _, width, height) = FancyZonesTestHelper.ScreenBounds();
        return (3 * width / 4, height / 2);
    }

    /// <summary>Shift-drag a window by its title bar and drop it on the given point so it snaps.</summary>
    /// <remarks>
    /// Verified and retried because this is setup, not the behaviour under test: a drop that races the
    /// zone highlight leaves the window unsnapped at the drop point, which would otherwise surface as a
    /// confusing "both windows should occupy the same zone" failure in the switching assertions.
    /// </remarks>
    private void SnapWindowToPoint(IntPtr window, int targetX, int targetY, string label)
    {
        const int attempts = 2;

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            FancyZonesTestHelper.Step(this, $"Snapping '{label}' to ({targetX},{targetY})");

            WindowControl.TryBringToForeground(window);
            WindowHelper.RestoreWindow(window);
            WindowHelper.SetWindowSize(window, WindowSize.Medium);
            Thread.Sleep(500);

            var (left, top, right, bottom) = WindowHelper.GetWindowBounds(window);

            KeyboardHelper.PressKey(Key.LShift);
            FancyZonesTestHelper.BeginWindowDrag(
                this,
                window,
                targetX - ((left + right) / 2),
                targetY - ((top + bottom) / 2));
            MouseHelper.LeftUp();
            KeyboardHelper.ReleaseKey(Key.LShift);

            Thread.Sleep(1500);

            // A snapped window takes the zone's size; one that missed keeps the size it was dragged at.
            var after = WindowHelper.GetWindowBounds(window);
            if (after.Right - after.Left != right - left || after.Bottom - after.Top != bottom - top)
            {
                return;
            }

            FancyZonesTestHelper.Step(
                this,
                $"'{label}' is still at its dragged size {after}, so the drop did not snap (attempt {attempt}/{attempts})");
        }
    }

    private void CloseExtraVirtualDesktop()
    {
        FancyZonesTestHelper.Step(this, "Closing the extra virtual desktop");
        KeyboardHelper.SendKeys(Key.Ctrl, Key.LWin, Key.Right);
        Thread.Sleep(1000);
        KeyboardHelper.SendKeys(Key.Ctrl, Key.LWin, Key.F4);
        Thread.Sleep(1000);
    }
}

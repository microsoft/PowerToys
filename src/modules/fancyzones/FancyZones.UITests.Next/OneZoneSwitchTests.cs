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
/// windows tracked by HWND, so delayed title updates cannot change their identity. Since two Explorer
/// windows share one <c>app-zone-history.json</c> entry, each exact HWND is verified through the
/// <c>FancyZones_zones</c> property the product stamps when it assigns that window to a zone.
/// </remarks>
[TestClass]
public class OneZoneSwitchTests : UITestBase
{
    private const long RightZoneBitmask = 1L << 1;

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
            FancyZonesTestHelper.WaitForForegroundWindow(activeWindow, 5_000),
            $"The last snapped window ({DescribeWindow(activeWindow)}) should be active, but {WindowControl.GetForegroundWindowInfo()} is.");

        FancyZonesTestHelper.Step(this, "Sending Win+PageDown to switch within the zone");
        KeyboardHelper.SendKeys(Key.LWin, Key.PageDown);

        Assert.IsTrue(
            FancyZonesTestHelper.WaitForForegroundWindow(previousWindow, 5_000),
            $"Win+PageDown should switch to {DescribeWindow(previousWindow)}, but {WindowControl.GetForegroundWindowInfo()} is active.");
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
            FancyZonesTestHelper.WaitForForegroundWindow(activeWindow, 5_000),
            $"The last snapped window ({DescribeWindow(activeWindow)}) should be active before changing desktops.");

        try
        {
            FancyZonesTestHelper.Step(this, "Creating a virtual desktop and returning to the original one");
            KeyboardHelper.SendKeys(Key.Ctrl, Key.LWin, Key.D);
            Thread.Sleep(1500);
            KeyboardHelper.SendKeys(Key.Ctrl, Key.LWin, Key.Left);
            Thread.Sleep(1500);

            Assert.IsTrue(
                FancyZonesTestHelper.WaitForForegroundWindow(activeWindow, 10_000),
                $"{DescribeWindow(activeWindow)} should still be active after returning to the original desktop.");

            FancyZonesTestHelper.Step(this, "Sending Win+PageDown to switch within the zone");
            KeyboardHelper.SendKeys(Key.LWin, Key.PageDown);

            Assert.IsTrue(
                FancyZonesTestHelper.WaitForForegroundWindow(previousWindow, 5_000),
                $"Win+PageDown should switch to {DescribeWindow(previousWindow)} after the desktop change.");
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
            FancyZonesTestHelper.WaitForForegroundWindow(activeWindow, 5_000),
            $"The last snapped window ({DescribeWindow(activeWindow)}) should be active.");

        FancyZonesTestHelper.Step(this, "Sending Win+PageDown with window switching disabled");
        KeyboardHelper.SendKeys(Key.LWin, Key.PageDown);
        Thread.Sleep(2000);

        Assert.IsTrue(
            FancyZonesTestHelper.WaitForForegroundWindow(activeWindow, 5_000),
            $"Win+PageDown must not switch windows while the shortcut is disabled, but {WindowControl.GetForegroundWindowInfo()} became active.");
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
    /// two HWNDs, oldest first.
    /// </summary>
    private (IntPtr PreviousWindow, IntPtr ActiveWindow) SnapBothWindowsToOneZone()
    {
        var first = FancyZonesTestHelper.OpenExplorerWindow(this, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        var second = FancyZonesTestHelper.OpenExplorerWindow(this, Environment.GetFolderPath(Environment.SpecialFolder.Windows));

        var firstTitle = FancyZonesTestHelper.GetWindowTitle(first);
        var secondTitle = FancyZonesTestHelper.GetWindowTitle(second);

        var (targetX, targetY) = RightZoneCenter();
        var firstZone = SnapWindowToPoint(first, targetX, targetY, firstTitle);
        var secondZone = SnapWindowToPoint(second, targetX, targetY, secondTitle);

        var firstBounds = WindowHelper.GetWindowBounds(first);
        var secondBounds = WindowHelper.GetWindowBounds(second);
        FancyZonesTestHelper.Step(this, $"Snapped bounds: '{firstTitle}' {firstBounds}, '{secondTitle}' {secondBounds}");

        Assert.AreEqual(
            RightZoneBitmask,
            firstZone,
            $"'{firstTitle}' should be stamped into the right zone, but its bitmask is 0x{firstZone:X}.");
        Assert.AreEqual(
            firstZone,
            secondZone,
            $"Both windows should occupy the same zone, but their bitmasks are 0x{firstZone:X} and 0x{secondZone:X}.");

        return (first, second);
    }

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
    private long SnapWindowToPoint(IntPtr window, int targetX, int targetY, string label)
    {
        const int attempts = 3;

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            FancyZonesTestHelper.Step(this, $"Snapping '{label}' to ({targetX},{targetY})");

            WindowControl.TryBringToForeground(window);
            WindowHelper.RestoreWindow(window);
            WindowHelper.SetWindowSize(window, WindowSize.Medium);
            Thread.Sleep(500);

            if (!FancyZonesTestHelper.WaitForZonesOverlayHidden())
            {
                FancyZonesTestHelper.Step(this, $"The previous overlay did not hide before regrabbing '{label}' (attempt {attempt}/{attempts})");
                MouseHelper.LeftUp();
                KeyboardHelper.ReleaseKey(Key.LShift);
                continue;
            }

            if (!FancyZonesTestHelper.BeginWindowDrag(this, window, targetX, targetY))
            {
                FancyZonesTestHelper.Step(this, $"Could not acquire '{label}' by its title bar (attempt {attempt}/{attempts})");
                MouseHelper.LeftUp();
                KeyboardHelper.ReleaseKey(Key.LShift);
                Thread.Sleep(500);
                continue;
            }

            var zonesActivated = FancyZonesTestHelper.ActivateZonesWithShiftDuringDrag(this);
            MouseHelper.LeftUp();

            if (!zonesActivated)
            {
                KeyboardHelper.ReleaseKey(Key.LShift);
                FancyZonesTestHelper.WaitForZonesOverlayHidden();
                FancyZonesTestHelper.Step(this, $"'{label}' never showed a stable overlay (attempt {attempt}/{attempts}); regrabbing");
                Thread.Sleep(500);
                continue;
            }

            // Keep Shift down until MoveSizeEnd stamps this exact HWND. Releasing it immediately
            // after the asynchronous mouse-up can disable snapping before MOVESIZEEND is delivered.
            var zoneReady = FancyZonesTestHelper.WaitForZoneBitmask(window, RightZoneBitmask, 5_000);
            KeyboardHelper.ReleaseKey(Key.LShift);
            var zoneBitmask = FancyZonesTestHelper.GetZoneBitmask(window);
            var after = WindowHelper.GetWindowBounds(window);
            if (zoneReady)
            {
                FancyZonesTestHelper.Step(this, $"'{label}' snapped with zone bitmask 0x{zoneBitmask:X} at {after}");
                return zoneBitmask;
            }

            FancyZonesTestHelper.Step(
                this,
                $"'{label}' was not stamped as zoned at {after}; overlay activated={zonesActivated} (attempt {attempt}/{attempts})");
            FancyZonesTestHelper.WaitForZonesOverlayHidden();
        }

        Assert.Fail($"Could not snap '{label}' after {attempts} attempts.");
        return 0;
    }

    private static string DescribeWindow(IntPtr window) =>
        $"'{FancyZonesTestHelper.GetWindowTitle(window)}' (HWND {window})";

    private void CloseExtraVirtualDesktop()
    {
        FancyZonesTestHelper.Step(this, "Closing the extra virtual desktop");
        KeyboardHelper.SendKeys(Key.Ctrl, Key.LWin, Key.Right);
        Thread.Sleep(1000);
        KeyboardHelper.SendKeys(Key.Ctrl, Key.LWin, Key.F4);
        Thread.Sleep(1000);
    }
}

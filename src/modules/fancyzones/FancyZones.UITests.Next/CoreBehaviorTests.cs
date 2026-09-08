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
/// Focused backend workflows selected from the broader FancyZones manual checklist because they
/// have durable one-monitor signals and do not duplicate editor-only coverage.
/// </summary>
[TestClass]
public class CoreBehaviorTests : UITestBase
{
    private const long FirstZoneBitmask = 1L << 0;
    private const long SecondZoneBitmask = 1L << 1;

    private readonly FancyZonesFiles files = new();

    public CoreBehaviorTests()
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

    /// <summary>Excluded applications must never receive a FancyZones zone assignment.</summary>
    [TestMethod]
    [TestCategory("FancyZones")]
    [TestCategory("FancyZones #Excluded apps")]
    public void TestExcludedAppDoesNotSnap()
    {
        Arrange(seed => seed
            .Set(Setting.ShiftDrag, true)
            .Set(Setting.ExcludedApps, "explorer.exe"));

        var window = OpenExplorerForTest();
        var (targetX, targetY) = FancyZonesTestHelper.ScreenCenter();
        Assert.IsTrue(
            FancyZonesTestHelper.BeginWindowDrag(this, window, targetX, targetY),
            "Could not start the excluded Explorer window's title-bar drag.");

        using var overlayWatcher = new WindowShowWatcher(FancyZonesTestHelper.ZonesOverlayClassName);
        KeyboardHelper.PressKey(Key.LShift);
        overlayWatcher.Wait(2_000);
        Assert.IsTrue(
            FancyZonesTestHelper.WaitForZonesOverlayHidden(2_000, requiredConsecutiveMatches: 10),
            "An excluded Explorer window should keep the zones overlay hidden.");
        Assert.AreEqual(
            0,
            overlayWatcher.Events.Count,
            $"An excluded Explorer window triggered overlay events: {string.Join(", ", overlayWatcher.Events)}.");

        MouseHelper.LeftUp();
        KeyboardHelper.ReleaseKey(Key.LShift);
        Thread.Sleep(1000);

        Assert.AreEqual(
            0L,
            FancyZonesTestHelper.GetZoneBitmask(window),
            "An excluded Explorer HWND must not be stamped with a zone assignment.");

        var history = files.AppZoneHistory.Exists ? files.AppZoneHistory.Read() : string.Empty;
        Assert.IsNull(
            ZoneHistory.GetZoneIndexSetByAppName("explorer.exe", history),
            "An excluded Explorer window must not be written to app-zone-history.json.");
    }

    /// <summary>
    /// Covers one-monitor zone-index keyboard snapping without duplicating every arrow permutation:
    /// native Windows Snap while override is off, FancyZones first/next/previous zones when on, and
    /// moving a newly opened window to the final known zone.
    /// </summary>
    [TestMethod]
    [TestCategory("FancyZones")]
    [TestCategory("FancyZones #Override Windows Snap")]
    [TestCategory("FancyZones #Move newly created windows to their last known zone")]
    public void TestKeyboardSnapCycleAndRestoreLastZone()
    {
        Arrange(seed => seed
            .Set(Setting.OverrideSnapHotkeys, false)
            .Set(Setting.MoveWindowsBasedOnPosition, false)
            .Set(Setting.MoveWindowAcrossMonitors, false)
            .Set(Setting.AppLastZoneMoveWindows, true));

        var folder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var window = OpenExplorerForTest(folder);
        var beforeNativeSnap = WindowHelper.GetWindowBounds(window);

        SendWinArrow(window, Key.Right, "native Windows Snap while FancyZones override is disabled");
        var nativeSnap = WaitHelper.WaitForStable(
            () => WindowHelper.GetWindowBounds(window),
            bounds => bounds != beforeNativeSnap,
            5_000,
            requiredConsecutiveMatches: 2,
            pollIntervalMS: 100);
        Assert.IsTrue(
            nativeSnap.Succeeded,
            $"Win+Right did not change Explorer HWND {window} geometry while Override Windows Snap was disabled. " +
            $"Before: {beforeNativeSnap}; after: {nativeSnap.LastObservation}.");
        Assert.AreEqual(
            0L,
            FancyZonesTestHelper.GetZoneBitmask(window),
            "Disabling Override Windows Snap should leave the HWND without a FancyZones zone stamp.");

        Assert.IsTrue(
            WindowControl.TryCloseByApp(
                "explorer",
                candidate => candidate.Hwnd == window,
                10_000),
            $"Could not close the native-snapped Explorer HWND {window} before the enabled phase.");

        new FancyZonesSettingsSeed()
            .Set(Setting.OverrideSnapHotkeys, true)
            .Apply();
        FancyZonesTestHelper.Step(this, "Restarting PowerToys with Override Windows Snap enabled");
        FancyZonesTestHelper.RestartPowerToys(this);
        FancyZonesTestHelper.EnsureFancyZonesRunning(this);
        WindowHelper.MinimizeWindow(new IntPtr(Session.WindowHandle));
        window = OpenExplorerForTest(folder);

        SendWinArrowAndAssertZone(window, Key.Right, FirstZoneBitmask);
        SendWinArrowAndAssertZone(window, Key.Right, SecondZoneBitmask);
        SendWinArrowAndAssertZone(window, Key.Left, FirstZoneBitmask);

        Assert.IsTrue(
            WindowControl.TryCloseByApp(
                "explorer",
                candidate => candidate.Hwnd == window,
                10_000),
            $"Could not close the zoned Explorer HWND {window} before testing last-zone restore.");

        var reopened = FancyZonesTestHelper.OpenExplorerWindow(this, folder);
        Assert.AreNotEqual(
            window,
            reopened,
            "The last-zone restore assertion requires a newly created Explorer HWND.");
        Assert.IsTrue(
            FancyZonesTestHelper.WaitForZoneBitmask(reopened, FirstZoneBitmask, 10_000),
            $"The reopened Explorer HWND {reopened} did not return to zone 0. " +
            $"Observed bitmask: 0x{FancyZonesTestHelper.GetZoneBitmask(reopened):X}.");
    }

    private void Arrange(Action<FancyZonesSettingsSeed> configure)
    {
        files.AppZoneHistory.Delete();
        files.AppliedLayouts.Delete();
        files.CustomLayouts.Write(new CustomLayouts().Serialize(LayoutFixtures.QuickSwitchCustomLayouts));
        files.LayoutHotkeys.Write(new LayoutHotkeys().Serialize(LayoutFixtures.QuickSwitchHotkeys));

        var seed = new FancyZonesSettingsSeed()
            .Set(Setting.QuickLayoutSwitch, true)
            .Set(Setting.FlashZonesOnQuickSwitch, false)
            .Set(Setting.ShiftDrag, true)
            .Set(Setting.MouseSwitch, false)
            .Set(Setting.MakeDraggedWindowTransparent, false)
            .Set(Setting.ShowZoneNumber, false)
            .Set(Setting.ExcludedApps, string.Empty)
            .Set(Setting.OverrideSnapHotkeys, false)
            .Set(Setting.MoveWindowsBasedOnPosition, false)
            .Set(Setting.MoveWindowAcrossMonitors, false)
            .Set(Setting.AppLastZoneMoveWindows, false);
        configure(seed);
        seed.Apply();

        FancyZonesTestHelper.Step(this, "Restarting PowerToys for the focused backend scenario");
        FancyZonesTestHelper.RestartPowerToys(this);
        FancyZonesTestHelper.EnsureFancyZonesRunning(this);

        FancyZonesTestHelper.Step(this, "Applying the 2x2 custom layout with Win+Ctrl+Alt+0");
        KeyboardHelper.SendKeys(Key.LWin, Key.Ctrl, Key.Alt, Key.Num0);
        Assert.IsTrue(
            FancyZonesTestHelper.AppliedLayoutContains(LayoutFixtures.GridCustomLayoutUuid, 15_000),
            $"Could not apply setup layout {LayoutFixtures.GridCustomLayoutUuid}. " +
            $"Last content: {FancyZonesTestHelper.ReadAppliedLayouts()}");

        WindowHelper.MinimizeWindow(new IntPtr(Session.WindowHandle));
    }

    private IntPtr OpenExplorerForTest(string? folder = null)
    {
        var window = FancyZonesTestHelper.OpenExplorerWindow(this, folder);
        ResetWindowForKeyboardSnap(window);
        return window;
    }

    private static void ResetWindowForKeyboardSnap(IntPtr window)
    {
        WindowHelper.RestoreWindow(window);
        WindowHelper.SetWindowSize(window, WindowSize.Medium);
        WindowControl.TryBringToForeground(window);
        Assert.IsTrue(
            WindowControl.WaitForForeground(window, 5_000, 2),
            $"Explorer HWND {window} did not take foreground for keyboard snapping.");
        Thread.Sleep(300);
    }

    private void SendWinArrowAndAssertZone(IntPtr window, Key arrow, long expectedBitmask)
    {
        SendWinArrow(window, arrow, $"FancyZones zone bitmask 0x{expectedBitmask:X}");
        Assert.IsTrue(
            FancyZonesTestHelper.WaitForZoneBitmask(window, expectedBitmask, 5_000),
            $"Win+{arrow} did not move Explorer HWND {window} to bitmask 0x{expectedBitmask:X}. " +
            $"Observed: 0x{FancyZonesTestHelper.GetZoneBitmask(window):X}.");
    }

    private void SendWinArrow(IntPtr window, Key arrow, string purpose)
    {
        WindowControl.TryBringToForeground(window);
        Assert.IsTrue(
            WindowControl.WaitForForeground(window, 5_000, 2),
            $"Explorer HWND {window} was not foreground before {purpose}.");
        FancyZonesTestHelper.Step(this, $"Sending Win+{arrow} for {purpose}");
        KeyboardHelper.SendKeys(Key.LWin, arrow);
    }
}

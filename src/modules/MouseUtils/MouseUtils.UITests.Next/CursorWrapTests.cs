// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MouseUtils.UITests;

[TestClass]
public class CursorWrapTests : UITestBase
{
    private const string ModuleName = "CursorWrap";
    private const string ToggleId = "MouseUtils_CursorWrapToggleId";
    private static readonly IDisposable ModuleSettings = SettingsConfigHelper.PreserveModuleSettings(ModuleName);

    static CursorWrapTests()
    {
    }

    public CursorWrapTests()
        : base(PowerToysModule.PowerToysSettings, enableModules: new[] { ModuleName })
    {
    }

    [ClassCleanup]
    public static void RestoreModuleSettings() => ModuleSettings.Dispose();

    protected override void PrepareTestState()
    {
        var configuration = TestContext.TestName switch
        {
            nameof(HorizontalOnlyWrapsOnlyHorizontalEdges) => new CursorWrapConfiguration(WrapMode: 2),
            nameof(VerticalOnlyWrapsOnlyVerticalEdges) => new CursorWrapConfiguration(WrapMode: 1),
            nameof(CtrlActivationModeRequiresCtrl) => new CursorWrapConfiguration(ActivationMode: 1),
            nameof(ShiftActivationModeRequiresShift) => new CursorWrapConfiguration(ActivationMode: 2),
            nameof(SingleMonitorSuppressionBlocksWrapping) => new CursorWrapConfiguration(DisableOnSingleMonitor: true),
            nameof(AutoActivateStartsWrappingWithoutShortcut) => new CursorWrapConfiguration(AutoActivate: true),
            nameof(ChangedShortcutTogglesWrapping) => new CursorWrapConfiguration(ShortcutCode: (int)Key.Y),
            _ => new CursorWrapConfiguration(),
        };

        MouseUtilsTestHelper.ReplaceModuleSettings(ModuleName, CreateSettings(configuration));
    }

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
    [TestCategory("CursorWrap")]
    public void ShortcutTogglesWrappingAndModuleDisableStopsIt()
    {
        MouseUtilsTestHelper.NavigateToMouseUtilities(this);
        var toggle = MouseUtilsTestHelper.SetModuleEnabled(this, ToggleId, true);
        Assert.IsTrue(
            NamedEventHelper.WaitUntilAvailable(NamedEventHelper.CursorWrapToggle),
            "CursorWrap did not create its trigger event after being enabled.");

        MouseUtilsTestHelper.Step(this, "Activating CursorWrap with Win+Alt+U");
        KeyboardHelper.SendKeys(Key.LWin, Key.Alt, Key.U);
        AssertWraps(CursorEdge.Left);

        MouseUtilsTestHelper.Step(this, "Deactivating CursorWrap with Win+Alt+U");
        KeyboardHelper.SendKeys(Key.LWin, Key.Alt, Key.U);
        AssertDoesNotWrap(CursorEdge.Left);

        toggle = MouseUtilsTestHelper.SetModuleEnabled(this, ToggleId, false);
        Assert.IsFalse(toggle.IsOn, "CursorWrap toggle should be off.");
        Assert.IsTrue(
            NamedEventHelper.WaitUntilUnavailable(NamedEventHelper.CursorWrapToggle),
            "CursorWrap trigger event remained available after the module was disabled.");
        AssertDoesNotWrap(CursorEdge.Left);
    }

    [TestMethod]
    [TestCategory("MouseUtils")]
    [TestCategory("CursorWrap")]
    public void BothModeWrapsAllEdges()
    {
        ActivateWithNamedEvent();
        foreach (var edge in Enum.GetValues<CursorEdge>())
        {
            AssertWraps(edge);
        }
    }

    [TestMethod]
    [TestCategory("MouseUtils")]
    [TestCategory("CursorWrap")]
    public void HorizontalOnlyWrapsOnlyHorizontalEdges()
    {
        ActivateWithNamedEvent();
        AssertWraps(CursorEdge.Left);
        AssertWraps(CursorEdge.Right);
        AssertDoesNotWrap(CursorEdge.Top);
        AssertDoesNotWrap(CursorEdge.Bottom);
    }

    [TestMethod]
    [TestCategory("MouseUtils")]
    [TestCategory("CursorWrap")]
    public void VerticalOnlyWrapsOnlyVerticalEdges()
    {
        ActivateWithNamedEvent();
        AssertWraps(CursorEdge.Top);
        AssertWraps(CursorEdge.Bottom);
        AssertDoesNotWrap(CursorEdge.Left);
        AssertDoesNotWrap(CursorEdge.Right);
    }

    [TestMethod]
    [TestCategory("MouseUtils")]
    [TestCategory("CursorWrap")]
    public void CtrlActivationModeRequiresCtrl()
    {
        ActivateWithNamedEvent();
        AssertDoesNotWrap(CursorEdge.Left);
        AssertWraps(CursorEdge.Left, Key.Ctrl);
    }

    [TestMethod]
    [TestCategory("MouseUtils")]
    [TestCategory("CursorWrap")]
    public void ShiftActivationModeRequiresShift()
    {
        ActivateWithNamedEvent();
        AssertDoesNotWrap(CursorEdge.Top);
        AssertWraps(CursorEdge.Top, Key.Shift);
    }

    [TestMethod]
    [TestCategory("MouseUtils")]
    [TestCategory("CursorWrap")]
    public void DragSuppressionBlocksWrappingWhileLeftButtonIsDown()
    {
        ActivateWithNamedEvent();
        AssertDoesNotWrap(CursorEdge.Left, holdLeftButton: true);
        AssertWraps(CursorEdge.Left);
    }

    [TestMethod]
    [TestCategory("MouseUtils")]
    [TestCategory("CursorWrap")]
    public void SingleMonitorSuppressionBlocksWrapping()
    {
        Assert.AreEqual(1, MonitorInfo.Count, "This scenario requires the single-monitor VM profile.");
        ActivateWithNamedEvent();
        foreach (var edge in Enum.GetValues<CursorEdge>())
        {
            AssertDoesNotWrap(edge);
        }
    }

    [TestMethod]
    [TestCategory("MouseUtils")]
    [TestCategory("CursorWrap")]
    public void AutoActivateStartsWrappingWithoutShortcut()
    {
        Assert.IsTrue(
            NamedEventHelper.WaitUntilAvailable(NamedEventHelper.CursorWrapToggle),
            "CursorWrap did not create its trigger event.");
        AssertWraps(CursorEdge.Right);
    }

    [TestMethod]
    [TestCategory("MouseUtils")]
    [TestCategory("CursorWrap")]
    public void ChangedShortcutTogglesWrapping()
    {
        Assert.IsTrue(
            NamedEventHelper.WaitUntilAvailable(NamedEventHelper.CursorWrapToggle),
            "CursorWrap did not create its trigger event.");

        KeyboardHelper.SendKeys(Key.LWin, Key.Alt, Key.U);
        AssertDoesNotWrap(CursorEdge.Left);

        KeyboardHelper.SendKeys(Key.LWin, Key.Alt, Key.Y);
        AssertWraps(CursorEdge.Left);
    }

    private static string CreateSettings(CursorWrapConfiguration configuration) => $$"""
        {
          "name": "CursorWrap",
          "version": "1.0",
          "properties": {
            "activation_shortcut": { "win": true, "ctrl": false, "alt": true, "shift": false, "code": {{configuration.ShortcutCode}}, "key": "" },
            "auto_activate": { "value": {{configuration.AutoActivate.ToString().ToLowerInvariant()}} },
            "disable_wrap_during_drag": { "value": {{configuration.DisableDuringDrag.ToString().ToLowerInvariant()}} },
            "wrap_mode": { "value": {{configuration.WrapMode}} },
            "activation_mode": { "value": {{configuration.ActivationMode}} },
            "disable_cursor_wrap_on_single_monitor": { "value": {{configuration.DisableOnSingleMonitor.ToString().ToLowerInvariant()}} }
          }
        }
        """;

    private void ActivateWithNamedEvent()
    {
        MouseUtilsTestHelper.Step(this, "Activating CursorWrap through its named event");
        Assert.IsTrue(
            NamedEventHelper.WaitAndSignal(NamedEventHelper.CursorWrapToggle),
            "CursorWrap did not create or respond to its trigger event.");
    }

    private void AssertWraps(CursorEdge edge, Key? heldKey = null, bool holdLeftButton = false)
    {
        var monitor = MonitorInfo.GetPrimary()!;
        var attempts = new List<(int X, int Y)>();
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            var after = MoveAcrossEdge(edge, heldKey, holdLeftButton);
            attempts.Add(after);
            var wrapped = edge switch
            {
                CursorEdge.Left => after.X >= monitor.Right - 10,
                CursorEdge.Right => after.X <= monitor.Left + 10,
                CursorEdge.Top => after.Y >= monitor.Bottom - 10,
                CursorEdge.Bottom => after.Y <= monitor.Top + 10,
                _ => false,
            };
            if (wrapped)
            {
                return;
            }

            Thread.Sleep(100);
        }

        Assert.Fail($"Cursor did not wrap from {edge} after three complete crossings; final positions: {string.Join(", ", attempts.Select(position => $"({position.X},{position.Y})"))}.");
    }

    private void AssertDoesNotWrap(CursorEdge edge, Key? heldKey = null, bool holdLeftButton = false)
    {
        var after = MoveAcrossEdge(edge, heldKey, holdLeftButton);
        var monitor = MonitorInfo.GetPrimary()!;
        var stayed = edge switch
        {
            CursorEdge.Left => after.X <= monitor.Left + 10,
            CursorEdge.Right => after.X >= monitor.Right - 10,
            CursorEdge.Top => after.Y <= monitor.Top + 10,
            CursorEdge.Bottom => after.Y >= monitor.Bottom - 10,
            _ => false,
        };

        Assert.IsTrue(stayed, $"Cursor unexpectedly wrapped from {edge}; final position was ({after.X},{after.Y}).");
    }

    private (int X, int Y) MoveAcrossEdge(CursorEdge edge, Key? heldKey, bool holdLeftButton)
    {
        var monitor = MonitorInfo.GetPrimary();
        Assert.IsNotNull(monitor, "No primary monitor was reported.");
        var centerX = monitor.Left + (monitor.Width / 2);
        var centerY = monitor.Top + (monitor.Height / 2);
        MouseHelper.MoveTo(centerX, centerY);
        MouseHelper.MoveBy(2, 2);

        var start = edge switch
        {
            CursorEdge.Left => (monitor.Left + 120, centerY),
            CursorEdge.Right => (monitor.Right - 121, centerY),
            CursorEdge.Top => (centerX, monitor.Top + 120),
            CursorEdge.Bottom => (centerX, monitor.Bottom - 121),
            _ => throw new ArgumentOutOfRangeException(nameof(edge)),
        };
        var inward = edge switch
        {
            CursorEdge.Left => (2, 0),
            CursorEdge.Right => (-2, 0),
            CursorEdge.Top => (0, 2),
            CursorEdge.Bottom => (0, -2),
            _ => throw new ArgumentOutOfRangeException(nameof(edge)),
        };
        var outward = edge switch
        {
            CursorEdge.Left => (-500, 0),
            CursorEdge.Right => (500, 0),
            CursorEdge.Top => (0, -500),
            CursorEdge.Bottom => (0, 500),
            _ => throw new ArgumentOutOfRangeException(nameof(edge)),
        };

        MouseHelper.MoveTo(start.Item1, start.Item2);
        if (heldKey.HasValue)
        {
            KeyboardHelper.PressKey(heldKey.Value);
        }

        if (holdLeftButton)
        {
            MouseHelper.LeftDown();
        }

        try
        {
            MouseHelper.MoveBy(inward.Item1, inward.Item2);
            MouseHelper.MoveBy(outward.Item1, outward.Item2);
            Thread.Sleep(150);
            var after = MouseHelper.GetMousePosition();
            MouseUtilsTestHelper.Step(this, $"Cursor {edge} crossing ended at ({after.X},{after.Y})");
            return after;
        }
        finally
        {
            if (holdLeftButton)
            {
                MouseHelper.LeftUp();
            }

            if (heldKey.HasValue)
            {
                KeyboardHelper.ReleaseKey(heldKey.Value);
            }
        }
    }

    private sealed record CursorWrapConfiguration(
        bool AutoActivate = false,
        bool DisableDuringDrag = true,
        int WrapMode = 0,
        int ActivationMode = 0,
        bool DisableOnSingleMonitor = false,
        int ShortcutCode = (int)Key.U);

    private enum CursorEdge
    {
        Left,
        Right,
        Top,
        Bottom,
    }
}

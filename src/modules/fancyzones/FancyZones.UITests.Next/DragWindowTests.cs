// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows.Forms;
using FancyZones.UITests.Utils;
using FancyZonesEditorCommon.Data;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static FancyZones.UITests.Utils.FancyZonesSettingsSeed;

namespace FancyZones.UITests;

/// <summary>
/// Port of the legacy <c>DragWindowTests</c>: the zone-behaviour matrix — Shift key, non-primary
/// mouse button, and dragged-window transparency — exercised by dragging a window across a seeded
/// single-zone layout.
/// </summary>
/// <remarks>
/// <para>
/// Two deliberate departures from the legacy suite, both forced by what is actually observable:
/// </para>
/// <list type="bullet">
///   <item><description><b>The subject window is File Explorer, not PowerToys Settings.</b> Synthetic
///   title-bar drags of the Settings window did not start a move loop FancyZones could see, so the
///   window moved without any zones appearing. Explorer is a plain top-level window that responds to
///   an injected drag the same way it does to a real one. (This is about driving the drag from a test,
///   not about the window's UI framework - dragging Settings or Notepad by hand activates zones
///   normally.)</description></item>
///   <item><description><b>Zone activation is asserted through the snap outcome, not the zone
///   colour.</b> The legacy tests sampled the highlight colour off the screen, but FancyZones paints
///   zones on a layered, DWM-composited overlay that GDI screen reads (both <c>GetPixel</c> and
///   <c>CopyFromScreen</c>) do not include — a probe there returns the wallpaper whether or not zones
///   are drawn. Whether the drop snaps the window is the same behaviour seen from the outside, and it
///   is durable: FancyZones records it in <c>app-zone-history.json</c>.</description></item>
/// </list>
/// <para>
/// The zone-behaviour options themselves are seeded into the module's <c>settings.json</c> instead of
/// being clicked through the Settings page, which is both deterministic and locale independent.
/// </para>
/// </remarks>
[TestClass]
public class DragWindowTests : UITestBase
{
    /// <summary>Executable recorded in <c>app-zone-history.json</c> for the dragged window.</summary>
    private const string DraggedApp = "explorer.exe";

    /// <summary>Alpha FancyZones applies while a dragged window is made transparent (50%).</summary>
    private const byte TransparentAlpha = 127;

    private readonly FancyZonesFiles files = new();

    private IntPtr draggedWindow;

    public DragWindowTests()
        : base(PowerToysModule.PowerToysSettings, WindowSize.UnSpecified, [ModuleName])
    {
    }

    protected override IReadOnlyList<string> StaleProcessNames => FancyZonesTestHelper.StaleProcessNames;

    /// <summary>The button that toggles zone activation, honouring a swapped-buttons mouse.</summary>
    private static bool NonPrimaryIsRight => !SystemInformation.MouseButtonsSwapped;

    [TestCleanup]
    public async Task CleanupTest()
    {
        await CaptureFailureArtifactsBeforeCleanupAsync();

        // A test that failed mid-gesture can leave the button/modifier down; free them first.
        MouseHelper.LeftUp();
        KeyboardHelper.ReleaseKey(Key.LShift);

        FancyZonesTestHelper.CloseLayoutEditor(this);
        FancyZonesTestHelper.CloseExplorerWindows();
        files.RestoreAll();
    }

    /// <summary>
    /// Test Use Shift key to activate zones while dragging a window in FancyZones Zone Behaviour Settings.
    /// Verifies that holding Shift while dragging activates the zones, so the drop snaps the window.
    /// </summary>
    [TestMethod]
    [TestCategory("FancyZones")]
    [TestCategory("FancyZones_Dragging #1")]
    public void TestShowZonesOnShiftDuringDrag()
    {
        Arrange(shiftDrag: true, mouseSwitch: false, transparent: false);

        StartShiftActivatedDrag();
        DropAndAssertSnapped("Holding Shift during the drag should activate the zones.");
    }

    /// <summary>
    /// Test dragging a window while the Shift key is already held.
    /// Verifies that starting the drag with Shift down activates the zones.
    /// </summary>
    [TestMethod]
    [TestCategory("FancyZones")]
    [TestCategory("FancyZones_Dragging #2")]
    public void TestShowZonesOnDragDuringShift()
    {
        Arrange(shiftDrag: true, mouseSwitch: false, transparent: false);

        KeyboardHelper.PressKey(Key.LShift);
        Thread.Sleep(200);
        Assert.IsTrue(StartDrag(), "Could not start a title-bar drag while Shift was held.");
        DropAndAssertSnapped("Starting the drag with Shift already held should activate the zones.");
    }

    /// <summary>
    /// Test toggling zones using a non-primary mouse click during window dragging.
    /// Verifies that clicking a non-primary mouse button deactivates zones while dragging a window.
    /// </summary>
    [TestMethod]
    [TestCategory("FancyZones")]
    [TestCategory("FancyZones_Dragging #3")]
    public void TestToggleZonesWithNonPrimaryMouseClick()
    {
        Arrange(shiftDrag: false, mouseSwitch: true, transparent: false);

        Assert.IsTrue(StartDrag(), "Could not start the title-bar drag.");
        Assert.IsTrue(
            FancyZonesTestHelper.WaitForZonesOverlayVisible(),
            "The drag never activated the zones overlay, so the non-primary click had no active state to toggle.");
        ClickNonPrimaryButton();
        Assert.IsTrue(
            FancyZonesTestHelper.WaitForZonesOverlayHidden(),
            "The zones overlay remained visible after the non-primary mouse click.");
        Drop();

        AssertSnapped(false, "A non-primary mouse click during the drag should deactivate the zones.");
    }

    /// <summary>
    /// Test both "use Shift" and "non-primary mouse" settings off.
    /// Verifies that zones are active as soon as the drag starts, and that holding Shift deactivates
    /// them again.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Asserted through the dragged window's alpha rather than the snap outcome: FancyZones fades the
    /// window from <c>SwitchSnappingMode(true)</c> and clears the fade in the same <c>false</c> branch
    /// that hides the zones, so the alpha tracks exactly the zones-active state the legacy test sampled
    /// as a colour - and unlike the snap, it can be read at both points of the same drag.
    /// </para>
    /// <para>
    /// The Shift-before-the-drag case is the control for the mid-drag one: it drives the same setting
    /// down the same state machine, differing only in whether FancyZones was already showing zones when
    /// the key went down.
    /// </para>
    /// </remarks>
    [TestMethod]
    [TestCategory("FancyZones")]
    [TestCategory("FancyZones_Dragging #4")]
    public void TestShowZonesWhenShiftAndMouseOff()
    {
        Arrange(shiftDrag: false, mouseSwitch: false, transparent: true);

        Assert.IsTrue(StartDrag(), "Could not start the title-bar drag.");
        var whileDragging = WaitForWindowAlpha(TransparentAlpha);
        FancyZonesTestHelper.Step(this, $"Alpha at drag start: {whileDragging}");

        // Positive control for the overlay detector: at alpha 127 the zones are provably on screen, so
        // this is the one moment a visible-overlay probe must succeed.
        FancyZonesTestHelper.Step(
            this,
            $"Zones overlay reported visible while zones are active: {FancyZonesTestHelper.IsZonesOverlayVisible()}");

        PressShiftDuringDrag();
        var shiftReachedTheSystem = KeyboardHelper.IsKeyDown(Key.Shift);
        var afterShift = WaitForWindowAlpha(255);
        FancyZonesTestHelper.Step(
            this,
            $"Alpha after pressing Shift mid-drag: {afterShift} (system reports Shift held: {shiftReachedTheSystem})");

        MouseHelper.LeftUp();
        KeyboardHelper.ReleaseKey(Key.LShift);
        Thread.Sleep(1000);

        var alphaWhenShiftHeldFirst = DragWithShiftHeldFromTheStart();
        FancyZonesTestHelper.Step(this, $"Alpha when Shift was held before the drag: {alphaWhenShiftHeldFirst}");

        Assert.AreEqual(TransparentAlpha, whileDragging, "Zones should be active as soon as the drag starts.");
        Assert.AreEqual(
            (byte)255,
            alphaWhenShiftHeldFirst,
            "With Shift-to-activate off, a drag started while Shift is held should leave the zones inactive.");
        Assert.AreEqual(
            (byte)255,
            afterShift,
            "With Shift-to-activate off, holding Shift should deactivate the zones. This regressed once " +
            "before: FancyZones' low-level hook swallows the bare Shift while zones are showing, which " +
            "also hid it from the module's own raw-input handler, so OnKeyDown must record the press " +
            $"itself. (The system still reports Shift held = {shiftReachedTheSystem}, because the key is " +
            "deliberately kept from the foreground app.)");
    }

    /// <summary>
    /// Test zone visibility when both the Shift key and non-primary mouse settings are on.
    /// Verifies that Shift activates the zones during a drag and a non-primary mouse click then
    /// deactivates them again.
    /// </summary>
    [TestMethod]
    [TestCategory("FancyZones")]
    [TestCategory("FancyZones_Dragging #5")]
    public void TestShowZonesWhenShiftAndMouseOn()
    {
        Arrange(shiftDrag: true, mouseSwitch: true, transparent: false);

        StartShiftActivatedDrag();
        ClickNonPrimaryButton();
        Drop();
        KeyboardHelper.ReleaseKey(Key.LShift);

        AssertSnapped(false, "The non-primary mouse click should deactivate the zones Shift had activated.");
    }

    /// <summary>
    /// Test that a window becomes transparent during dragging when the transparent window setting is
    /// enabled.
    /// </summary>
    [TestMethod]
    [TestCategory("FancyZones")]
    [TestCategory("FancyZones_Dragging #8")]
    public void TestMakeDraggedWindowTransparentOn()
    {
        Arrange(shiftDrag: true, mouseSwitch: false, transparent: true);

        Assert.AreEqual(
            TransparentAlpha,
            DragAndReadWindowAlpha(),
            $"The dragged window should be faded to alpha {TransparentAlpha} while the zones are active.");
    }

    /// <summary>
    /// Test that a window remains opaque during dragging when the transparent window setting is
    /// disabled.
    /// </summary>
    [TestMethod]
    [TestCategory("FancyZones")]
    [TestCategory("FancyZones_Dragging #8")]
    public void TestMakeDraggedWindowTransparentOff()
    {
        Arrange(shiftDrag: true, mouseSwitch: false, transparent: false, twoZones: true);

        Assert.AreEqual(
            (byte)255,
            DragAndReadWindowAlpha(),
            "The dragged window should stay opaque while the transparency setting is off.");
    }

    /// <summary>
    /// Seed the layout and zone-behaviour settings for one scenario, relaunch PowerToys so the module
    /// reads them, apply the seeded layout through the editor, and open the window that will be
    /// dragged.
    /// </summary>
    private void Arrange(bool shiftDrag, bool mouseSwitch, bool transparent, bool twoZones = false)
    {
        FancyZonesTestHelper.Step(this, $"Seeding layout ({(twoZones ? "two zones" : "one zone")}) and zone-behaviour settings");

        files.AppZoneHistory.Delete();
        files.AppliedLayouts.Delete();
        files.CustomLayouts.Write(new CustomLayouts().Serialize(
            twoZones ? LayoutFixtures.TwoZoneColumns : LayoutFixtures.SingleZoneColumn));

        new FancyZonesSettingsSeed()
            .Set(Setting.ShiftDrag, shiftDrag)
            .Set(Setting.MouseSwitch, mouseSwitch)
            .Set(Setting.MakeDraggedWindowTransparent, transparent)
            .Set(Setting.ShowZoneNumber, false)
            .Set(Setting.SystemTheme, false)
            .Set(Setting.HighlightOpacity, 100)
            .Set(Setting.AllowChildWindowSnap, true)
            .Set(Setting.AllowPopupWindowSnap, true)
            .Apply();

        // The settings themselves are hot-reloaded and would not need this. The restart is here for
        // the LAYOUT EDITOR: FancyZones' ToggleEditor treats the toggle event as "close" while its
        // terminate-editor handle is alive, and that state survives between tests, so a long-lived
        // module ends up swallowing the next open. Measured: 14/17 with the restart, 3/17 without.
        FancyZonesTestHelper.Step(this, "Restarting PowerToys to reset the FancyZones editor toggle state");
        FancyZonesTestHelper.RestartPowerToys(this);

        FancyZonesTestHelper.EnsureFancyZonesRunning(this);
        FancyZonesTestHelper.ApplyLayoutThroughEditor(
            this,
            By.Name(FancyZonesTestHelper.LayoutName.CustomColumn),
            LayoutFixtures.CustomColumnUuid);

        // The Settings window is only here because it owns the runner; keep it off the drag surface.
        WindowHelper.MinimizeWindow(new IntPtr(Session.WindowHandle));
        Thread.Sleep(500);

        draggedWindow = FancyZonesTestHelper.OpenExplorerWindow(this);
        WindowControl.TryBringToForeground(draggedWindow);

        // Restore first: SetWindowPos resizes a maximized window without clearing its maximized
        // state, and dragging it would then make Windows restore it mid-gesture instead of moving it.
        WindowHelper.RestoreWindow(draggedWindow);
        Thread.Sleep(300);
        WindowHelper.SetWindowSize(draggedWindow, WindowSize.Medium);
        Thread.Sleep(500);

        // app-zone-history is the assertion signal, so it must start empty even if opening the window
        // restored it to a previously remembered zone.
        files.AppZoneHistory.Delete();

        FancyZonesTestHelper.Step(this, $"Window to drag ready at {WindowHelper.GetWindowBounds(draggedWindow)}");
    }

    /// <summary>Grab the window by its title bar and drag it towards the centre, button still down.</summary>
    private bool StartDrag()
    {
        var (centerX, centerY) = FancyZonesTestHelper.ScreenCenter();
        return FancyZonesTestHelper.BeginWindowDrag(this, draggedWindow, centerX, centerY);
    }

    /// <summary>Release the drag and let FancyZones settle the snap.</summary>
    private void Drop()
    {
        MouseHelper.LeftUp();
        Thread.Sleep(1500);
    }

    /// <summary>Release the mouse but keep Shift held until FancyZones records MoveSizeEnd.</summary>
    private void DropAndAssertSnapped(string because)
    {
        MouseHelper.LeftUp();
        try
        {
            AssertSnapped(true, because);
        }
        finally
        {
            KeyboardHelper.ReleaseKey(Key.LShift);
        }
    }

    /// <summary>Hold Shift mid-drag; the product posts its own location update for the new key state.</summary>
    private void PressShiftDuringDrag()
    {
        FancyZonesTestHelper.Step(this, "Pressing Shift during the drag");
        KeyboardHelper.PressKey(Key.LShift);
        Thread.Sleep(300);
    }

    /// <summary>Retry the complete grab-and-activate gesture on the same Explorer HWND.</summary>
    private void StartShiftActivatedDrag()
    {
        const int attempts = 3;

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            FancyZonesTestHelper.Step(this, $"Shift-activated drag attempt {attempt}/{attempts}");
            if (!FancyZonesTestHelper.WaitForZonesOverlayHidden())
            {
                FancyZonesTestHelper.Step(this, "The previous overlay did not hide; resetting before the next attempt");
                ResetDraggedWindowForRetry();
                continue;
            }

            if (!StartDrag())
            {
                FancyZonesTestHelper.Step(this, "Could not acquire the title bar; resetting the same window before retrying");
                ResetDraggedWindowForRetry();
                continue;
            }

            if (FancyZonesTestHelper.ActivateZonesWithShiftDuringDrag(this))
            {
                return;
            }

            FancyZonesTestHelper.Step(this, "The overlay did not stabilize; releasing input and regrabbing the same window");
            MouseHelper.LeftUp();
            KeyboardHelper.ReleaseKey(Key.LShift);
            FancyZonesTestHelper.WaitForZonesOverlayHidden();
            ResetDraggedWindowForRetry();
        }

        Assert.Fail($"Holding Shift during the drag never made the zones overlay stable after {attempts} complete drag attempts.");
    }

    private void ResetDraggedWindowForRetry()
    {
        MouseHelper.LeftUp();
        KeyboardHelper.ReleaseKey(Key.LShift);
        WindowControl.TryBringToForeground(draggedWindow);
        WindowHelper.RestoreWindow(draggedWindow);
        WindowHelper.SetWindowSize(draggedWindow, WindowSize.Medium);
        Thread.Sleep(750);
    }

    /// <summary>
    /// Park the window back in the top-left quadrant, hold Shift, and only then start a drag. Reports
    /// the dragged window's alpha, which is 255 while the zones are inactive.
    /// </summary>
    private byte DragWithShiftHeldFromTheStart()
    {
        WindowHelper.MoveWindow(draggedWindow, 100, 100);
        Thread.Sleep(500);

        FancyZonesTestHelper.Step(this, "Holding Shift before the drag starts");
        KeyboardHelper.PressKey(Key.LShift);
        Thread.Sleep(500);

        Assert.IsTrue(StartDrag(), "Could not start the title-bar drag while Shift was held.");
        Thread.Sleep(1000);
        var alpha = WindowHelper.GetWindowAlpha(draggedWindow);

        MouseHelper.LeftUp();
        KeyboardHelper.ReleaseKey(Key.LShift);
        Thread.Sleep(500);
        return alpha;
    }

    private void ClickNonPrimaryButton()
    {
        FancyZonesTestHelper.Step(this, $"Clicking the non-primary mouse button ({(NonPrimaryIsRight ? "right" : "left")})");
        if (NonPrimaryIsRight)
        {
            MouseHelper.RightClick();
        }
        else
        {
            MouseHelper.LeftClick();
        }

        Thread.Sleep(800);
    }

    /// <summary>Poll the dragged window's alpha until it reaches <paramref name="expected"/>.</summary>
    private byte WaitForWindowAlpha(byte expected, int timeoutMs = 5_000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        byte alpha;
        do
        {
            alpha = WindowHelper.GetWindowAlpha(draggedWindow);
            if (alpha == expected)
            {
                break;
            }

            Thread.Sleep(200);
        }
        while (DateTime.UtcNow < deadline);

        return alpha;
    }

    /// <summary>Drag with the zones active and read the dragged window's alpha mid-gesture.</summary>
    private byte DragAndReadWindowAlpha()
    {
        KeyboardHelper.PressKey(Key.LShift);
        Thread.Sleep(200);
        Assert.IsTrue(StartDrag(), "Could not start the title-bar drag while Shift was held.");

        // The fade is applied when the zones engage, which can trail the first move on a slow machine.
        var alpha = WaitForWindowAlpha(TransparentAlpha);
        FancyZonesTestHelper.Step(this, $"Dragged window alpha while dragging: {alpha}");

        MouseHelper.LeftUp();
        KeyboardHelper.ReleaseKey(Key.LShift);
        Thread.Sleep(500);
        return alpha;
    }

    /// <summary>
    /// Assert whether the drop snapped the window, read from <c>app-zone-history.json</c> — the record
    /// FancyZones writes when a window is assigned to a zone.
    /// </summary>
    private void AssertSnapped(bool expected, string because)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        string? zoneIndex;
        do
        {
            zoneIndex = ZoneHistory.GetZoneIndexSetByAppName(
                DraggedApp,
                files.AppZoneHistory.Exists ? files.AppZoneHistory.Read() : string.Empty);

            if ((zoneIndex is not null) == expected)
            {
                break;
            }

            Thread.Sleep(500);
        }
        while (DateTime.UtcNow < deadline);

        FancyZonesTestHelper.Step(this, $"app-zone-history zone index for {DraggedApp}: {zoneIndex ?? "<none>"}");

        var observed = zoneIndex is null ? "no entry" : $"zone {zoneIndex}";
        Assert.AreEqual(
            expected,
            zoneIndex is not null,
            $"{because} Expected the window {(expected ? "to snap into a zone" : "not to snap")}, but app-zone-history reported {observed}.");
    }
}

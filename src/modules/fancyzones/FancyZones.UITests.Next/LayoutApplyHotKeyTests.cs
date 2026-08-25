// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using FancyZones.UITests.Utils;
using FancyZonesEditorCommon.Data;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static FancyZones.UITests.Utils.FancyZonesSettingsSeed;
using Id = FancyZones.UITests.Utils.FancyZonesTestHelper.AccessibilityId;

namespace FancyZones.UITests;

/// <summary>
/// Port of the legacy <c>LayoutApplyHotKeyTests</c>: quick layout switching (Win+Ctrl+Alt+digit),
/// zone flashing, virtual-desktop layout persistence, custom-layout deletion and the editor's
/// reaction to a monitor change.
/// </summary>
[TestClass]
public class LayoutApplyHotKeyTests : UITestBase
{
    private const string SaveButtonName = "Save";
    private const string NewLayoutName = "Custom layout 1";

    private readonly FancyZonesFiles files = new();

    public LayoutApplyHotKeyTests()
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
    /// Verifies that each quick-switch hotkey applies the custom layout it is bound to.
    /// </summary>
    [TestMethod]
    [TestCategory("FancyZones")]
    [TestCategory("FancyZones #1")]
    public void TestApplyHotKey()
    {
        Arrange(quickLayoutSwitch: true);
        AssignQuickKeyThroughEditor(Id.GridCustomLayoutCard, "0");

        AssertLayoutAfterHotkey(Key.Num0, Id.GridCustomLayoutCard, expectSelected: true);
        AssertLayoutAfterHotkey(Key.Num1, Id.Grid9LayoutCard, expectSelected: true);
        AssertLayoutAfterHotkey(Key.Num2, Id.CanvasCustomLayoutCard, expectSelected: true);
    }

    /// <summary>
    /// Verifies that the quick-layout chord applies its layout while a window move loop is active.
    /// The checklist's historical "digit only" wording is obsolete; current FancyZones deliberately
    /// requires Win+Ctrl+Alt while dragging to avoid stealing number keys from applications.
    /// </summary>
    [TestMethod]
    [TestCategory("FancyZones")]
    [TestCategory("FancyZones #2")]
    public void TestQuickLayoutHotKeyDuringDrag()
    {
        Arrange(quickLayoutSwitch: true, shiftDrag: false);

        FancyZonesTestHelper.Step(this, "Applying Grid-9 as setup with Win+Ctrl+Alt+1");
        KeyboardHelper.SendKeys(Key.LWin, Key.Ctrl, Key.Alt, Key.Num1);
        Assert.IsTrue(
            FancyZonesTestHelper.AppliedLayoutContains(LayoutFixtures.Grid9LayoutUuid, 15_000),
            $"Could not apply the setup layout {LayoutFixtures.Grid9LayoutUuid}. " +
            $"Last content: {FancyZonesTestHelper.ReadAppliedLayouts()}");

        WindowHelper.MinimizeWindow(new IntPtr(Session.WindowHandle));
        var window = FancyZonesTestHelper.OpenExplorerWindow(this);
        WindowHelper.RestoreWindow(window);
        WindowHelper.SetWindowSize(window, WindowSize.Medium);
        Thread.Sleep(500);

        var (targetX, targetY) = FancyZonesTestHelper.ScreenCenter();
        Assert.IsTrue(
            FancyZonesTestHelper.BeginWindowDrag(this, window, targetX, targetY),
            "Could not start the Explorer title-bar drag needed to test quick layout switching.");

        try
        {
            Assert.IsTrue(
                FancyZonesTestHelper.WaitForZonesOverlayVisible(),
                "The drag move loop never activated the zones overlay.");

            FancyZonesTestHelper.Step(this, "Sending Win+Ctrl+Alt+0 while the drag is active");
            KeyboardHelper.SendKeys(Key.LWin, Key.Ctrl, Key.Alt, Key.Num0);

            Assert.IsTrue(
                FancyZonesTestHelper.AppliedLayoutContains(LayoutFixtures.GridCustomLayoutUuid, 15_000),
                $"The drag-specific quick-layout chord did not apply {LayoutFixtures.GridCustomLayoutUuid}. " +
                $"Last content: {FancyZonesTestHelper.ReadAppliedLayouts()}");
        }
        finally
        {
            MouseHelper.LeftUp();
        }
    }

    /// <summary>
    /// Verifies that switching layout with the hotkey flashes the zones when that option is on.
    /// </summary>
    [TestMethod]
    [TestCategory("FancyZones")]
    [TestCategory("FancyZones #3")]
    public void HotKeyWindowFlashTest()
    {
        Arrange(quickLayoutSwitch: true, flashZones: true);

        // Zones flash when the layout CHANGES, so each attempt switches to a different layout —
        // re-sending the same chord re-applies the layout already in effect and flashes nothing. The
        // list cycles so that whichever layout happens to be applied first, a real switch follows.
        Key[] chords = [Key.Num0, Key.Num1, Key.Num2, Key.Num0, Key.Num1];
        var flashed = false;
        for (var attempt = 0; attempt < chords.Length && !flashed; attempt++)
        {
            var chord = chords[attempt];
            var before = FancyZonesTestHelper.ReadAppliedLayouts();
            FancyZonesTestHelper.Step(this, $"Sending Win+Ctrl+Alt+{chord} and watching for the zones overlay");
            flashed = FancyZonesTestHelper.DidZonesFlash(
                this,
                () => KeyboardHelper.SendKeys(Key.LWin, Key.Ctrl, Key.Alt, chord),
                5_000);

            var after = FancyZonesTestHelper.ReadAppliedLayouts();
            FancyZonesTestHelper.Step(
                this,
                $"{chord}: flashed={flashed}, layout changed={!string.Equals(before, after, StringComparison.Ordinal)}");
        }

        Assert.IsTrue(
            flashed,
            $"No visible '{FancyZonesTestHelper.ZonesOverlayClassName}' window appeared, so the zones did not flash on the layout switch.");
    }

    /// <summary>
    /// Verifies that the quick-switch hotkeys do nothing while quick layout switching is disabled.
    /// </summary>
    [TestMethod]
    [TestCategory("FancyZones")]
    [TestCategory("FancyZones #4")]
    public void TestDisableApplyHotKey()
    {
        Arrange(quickLayoutSwitch: false);

        AssertLayoutAfterHotkey(Key.Num0, Id.GridCustomLayoutCard, expectSelected: false);
        AssertLayoutAfterHotkey(Key.Num1, Id.Grid9LayoutCard, expectSelected: false);
        AssertLayoutAfterHotkey(Key.Num2, Id.CanvasCustomLayoutCard, expectSelected: false);
    }

    /// <summary>
    /// Verifies that a layout applied on one virtual desktop is still applied after a new desktop is
    /// created and PowerToys restarts.
    /// </summary>
    [TestMethod]
    [TestCategory("FancyZones")]
    [TestCategory("FancyZones #6")]
    public void TestVirtualDesktopLayout()
    {
        Arrange(quickLayoutSwitch: true);
        SelectLayoutInEditor(Id.GridCustomLayoutCard);

        try
        {
            FancyZonesTestHelper.Step(this, "Creating a virtual desktop and restarting PowerToys");
            KeyboardHelper.SendKeys(Key.Ctrl, Key.LWin, Key.D);
            Thread.Sleep(1000);

            FancyZonesTestHelper.RestartPowerToys(this);
            FancyZonesTestHelper.EnsureFancyZonesRunning(this);

            AssertLayoutSelected(Id.GridCustomLayoutCard, expectSelected: true);
        }
        finally
        {
            CloseExtraVirtualDesktop();
        }
    }

    /// <summary>
    /// Verifies that each virtual desktop keeps its own layout: selecting a different layout on a new
    /// desktop must not change the layout of the original one.
    /// </summary>
    [TestMethod]
    [TestCategory("FancyZones")]
    [TestCategory("FancyZones #7")]
    public void TestVirtualDesktopLayoutExt()
    {
        Arrange(quickLayoutSwitch: true);
        SelectLayoutInEditor(Id.GridCustomLayoutCard);

        try
        {
            FancyZonesTestHelper.Step(this, "Creating a second virtual desktop and applying a different layout there");
            KeyboardHelper.SendKeys(Key.Ctrl, Key.LWin, Key.D);
            Thread.Sleep(1000);

            FancyZonesTestHelper.RestartPowerToys(this);
            FancyZonesTestHelper.EnsureFancyZonesRunning(this);
            SelectLayoutInEditor(Id.Grid9LayoutCard);

            FancyZonesTestHelper.Step(this, "Returning to the first virtual desktop");
            KeyboardHelper.SendKeys(Key.Ctrl, Key.LWin, Key.Left);
            Thread.Sleep(1000);

            FancyZonesTestHelper.RestartPowerToys(this);
            FancyZonesTestHelper.EnsureFancyZonesRunning(this);

            AssertLayoutSelected(Id.GridCustomLayoutCard, expectSelected: true);
        }
        finally
        {
            CloseExtraVirtualDesktop();
        }
    }

    /// <summary>
    /// Verifies that deleting the applied custom layout falls back to the empty ("No layout") template.
    /// </summary>
    [TestMethod]
    [TestCategory("FancyZones")]
    [TestCategory("FancyZones #8")]
    public void TestDeleteCustomLayoutBehavior()
    {
        Arrange(quickLayoutSwitch: true);

        var editor = FancyZonesTestHelper.OpenLayoutEditor(this);
        try
        {
            FancyZonesTestHelper.ApplyLayout(this, editor, By.AccessibilityId(Id.GridCustomLayoutCard));

            FancyZonesTestHelper.Step(this, "Deleting the applied custom layout");
            FancyZonesTestHelper.OpenEditLayoutDialog(
                this,
                editor,
                Id.GridCustomLayoutCard,
                By.AccessibilityId(Id.DeleteLayoutButton));

            editor.Find<Button>(By.AccessibilityId(Id.DeleteLayoutButton), FancyZonesTestHelper.FindTimeoutMs).Click(msPostAction: 500);
            editor.Find<Button>(By.AccessibilityId(Id.PrimaryButton), FancyZonesTestHelper.FindTimeoutMs).Click(msPostAction: 1000);

            var blank = editor.Find<Element>(By.Name(FancyZonesTestHelper.LayoutName.NoLayout), FancyZonesTestHelper.FindTimeoutMs);
            Assert.IsTrue(blank.Selected, "Deleting the applied custom layout should fall back to the empty layout.");
        }
        finally
        {
            FancyZonesTestHelper.CloseLayoutEditor(this);
        }
    }

    /// <summary>
    /// Verifies that a newly created grid layout is listed, and that the editor reports the monitor's
    /// current resolution after a display-mode change.
    /// </summary>
    /// <remarks>
    /// The legacy test switched to the smallest enumerated mode and asserted the literal
    /// <c>640 × 480</c>. The port switches to 1024x768 — universally available, unlike the smallest
    /// mode of an arbitrary adapter — and asserts the editor reports exactly that, which is the same
    /// behaviour without depending on one machine's mode list.
    /// </remarks>
    [TestMethod]
    [TestCategory("FancyZones")]
    [TestCategory("FancyZones #9")]
    public void TestCreateGridLayoutChangeMonitorSetting()
    {
        const int reducedWidth = 1024;
        const int reducedHeight = 768;

        Arrange(quickLayoutSwitch: true);

        var editor = FancyZonesTestHelper.OpenLayoutEditor(this);
        try
        {
            FancyZonesTestHelper.Step(this, "Creating a new grid layout");
            editor.Find<Element>(By.AccessibilityId(Id.NewLayoutButton), FancyZonesTestHelper.FindTimeoutMs).Click(msPostAction: 500);
            editor.Find<Element>(By.AccessibilityId(Id.PrimaryButton), FancyZonesTestHelper.FindTimeoutMs).Click(msPostAction: 800);

            Session? gridEditor = null;
            Assert.IsTrue(
                editor.WaitFor(
                    () =>
                    {
                        foreach (var window in WindowsFinder.ListByApp(FancyZonesTestHelper.EditorProcess))
                        {
                            var candidate = WindowsFinder.WaitForWindowByApp(
                                FancyZonesTestHelper.EditorProcess,
                                current => current.Hwnd == window.Hwnd,
                                timeoutMS: 100,
                                pollIntervalMS: 25);
                            if (candidate is not null &&
                                candidate.FindAll<Element>(By.Name("Grid layout editor"), 0)
                                    .Any(element => string.Equals(element.Name, "Grid layout editor", StringComparison.Ordinal) && element.Displayed))
                            {
                                gridEditor = candidate;
                                return true;
                            }
                        }

                        return false;
                    },
                    FancyZonesTestHelper.FindTimeoutMs,
                    100),
                "The newly opened grid layout editor window did not become automation-ready.");
            gridEditor!.Find<Button>(SaveButtonName, FancyZonesTestHelper.FindTimeoutMs).Click(msPostAction: 1000);

            Assert.IsNotNull(
                editor.Find<Element>(By.Name(NewLayoutName), FancyZonesTestHelper.FindTimeoutMs),
                $"The newly created layout '{NewLayoutName}' should be listed in the editor.");
        }
        finally
        {
            FancyZonesTestHelper.CloseLayoutEditor(this);
        }

        var (originalWidth, originalHeight) = WindowHelper.GetDisplaySize();
        try
        {
            FancyZonesTestHelper.Step(this, $"Changing the display resolution to {reducedWidth}x{reducedHeight}");
            DisplayHelper.NormalizeResolution(reducedWidth, reducedHeight);
            Thread.Sleep(2000);

            var (currentWidth, currentHeight) = WindowHelper.GetDisplaySize();
            Assert.AreEqual(
                (reducedWidth, reducedHeight),
                (currentWidth, currentHeight),
                "The display did not accept the requested resolution, so the editor's report cannot be verified.");

            editor = FancyZonesTestHelper.OpenLayoutEditor(this);
            var resolutionTexts = editor.FindAll<Element>(By.AccessibilityId(Id.ResolutionText), FancyZonesTestHelper.FindTimeoutMs);
            Assert.IsTrue(resolutionTexts.Count > 0, "The editor did not render a resolution label for any monitor.");
            Assert.AreEqual(
                $"{currentWidth} × {currentHeight}",
                resolutionTexts[0].GetValue(),
                "The editor should report the monitor's current resolution.");
        }
        finally
        {
            FancyZonesTestHelper.CloseLayoutEditor(this);
            DisplayHelper.NormalizeResolution(originalWidth, originalHeight);
            Thread.Sleep(2000);
        }
    }

    /// <summary>
    /// Seed the editor's JSON fixtures and the quick-layout-switch settings, then relaunch PowerToys so
    /// FancyZones reads them and land on the FancyZones settings page with the module enabled.
    /// </summary>
    private void Arrange(bool quickLayoutSwitch, bool flashZones = false, bool shiftDrag = true)
    {
        FancyZonesTestHelper.Step(this, $"Seeding layouts and hotkeys (quick layout switch: {quickLayoutSwitch})");

        files.Parameters.Write(new EditorParameters().Serialize(LayoutFixtures.TwoMonitorParameters));
        files.LayoutTemplates.Write(new LayoutTemplates().Serialize(LayoutFixtures.TemplateLayouts));
        files.CustomLayouts.Write(new CustomLayouts().Serialize(LayoutFixtures.QuickSwitchCustomLayouts));
        files.DefaultLayouts.Write(new DefaultLayouts().Serialize(LayoutFixtures.DefaultLayouts));
        files.LayoutHotkeys.Write(new LayoutHotkeys().Serialize(LayoutFixtures.QuickSwitchHotkeys));
        files.AppliedLayouts.Write(new AppliedLayouts().Serialize(new AppliedLayouts.AppliedLayoutsListWrapper
        {
            AppliedLayouts = [],
        }));
        files.AppZoneHistory.Delete();

        new FancyZonesSettingsSeed()
            .Set(Setting.QuickLayoutSwitch, quickLayoutSwitch)
            .Set(Setting.FlashZonesOnQuickSwitch, flashZones)
            .Set(Setting.ShiftDrag, shiftDrag)
            .Set(Setting.ShowZoneNumber, false)
            .Apply();

        // Resets the FancyZones editor toggle state, not the settings — see DragWindowTests.Arrange.
        FancyZonesTestHelper.Step(this, "Restarting PowerToys to reset the FancyZones editor toggle state");
        FancyZonesTestHelper.RestartPowerToys(this);

        FancyZonesTestHelper.EnsureFancyZonesRunning(this);

        FancyZonesTestHelper.Step(
            this,
            $"After the restart, settings hold quickLayoutSwitch={FancyZonesSettingsSeed.ReadCurrent(Setting.QuickLayoutSwitch)}, " +
            $"flashZonesOnQuickSwitch={FancyZonesSettingsSeed.ReadCurrent(Setting.FlashZonesOnQuickSwitch)}");
    }

    /// <summary>Open the editor, bind the given quick key to a layout through its dropdown, and save.</summary>
    private void AssignQuickKeyThroughEditor(string layoutCardId, string quickKey)
    {
        var editor = FancyZonesTestHelper.OpenLayoutEditor(this);
        try
        {
            FancyZonesTestHelper.Step(this, $"Assigning quick key '{quickKey}' to {layoutCardId}");
            FancyZonesTestHelper.OpenEditLayoutDialog(
                this,
                editor,
                layoutCardId,
                By.AccessibilityId(Id.HotkeyComboBox));

            editor.Find<ComboBox>(By.AccessibilityId(Id.HotkeyComboBox), FancyZonesTestHelper.FindTimeoutMs).Select(quickKey);
            Thread.Sleep(500);

            editor.Find<Button>(SaveButtonName, FancyZonesTestHelper.FindTimeoutMs).Click(msPostAction: 1000);
        }
        finally
        {
            FancyZonesTestHelper.CloseLayoutEditor(this);
        }
    }

    /// <summary>Open the editor, click a layout card so it is applied, and close the editor.</summary>
    private void SelectLayoutInEditor(string layoutCardId) =>
        FancyZonesTestHelper.ApplyLayoutThroughEditor(this, By.AccessibilityId(layoutCardId));

    /// <summary>Send a quick-switch chord, then confirm through the editor whether the layout was applied.</summary>
    private void AssertLayoutAfterHotkey(Key digit, string layoutCardId, bool expectSelected)
    {
        WindowControl.TryBringToForeground(new IntPtr(Session.WindowHandle));
        Thread.Sleep(300);

        FancyZonesTestHelper.Step(this, $"Sending Win+Ctrl+Alt+{digit}");
        KeyboardHelper.SendKeys(Key.LWin, Key.Ctrl, Key.Alt, digit);
        Thread.Sleep(2000);

        AssertLayoutSelected(layoutCardId, expectSelected);
    }

    /// <summary>Open the editor and assert whether a layout card is the applied one.</summary>
    private void AssertLayoutSelected(string layoutCardId, bool expectSelected)
    {
        var editor = FancyZonesTestHelper.OpenLayoutEditor(this);
        try
        {
            var card = editor.Find<Element>(By.AccessibilityId(layoutCardId), FancyZonesTestHelper.FindTimeoutMs);
            Assert.AreEqual(
                expectSelected,
                card.Selected,
                $"Layout card '{layoutCardId}' should {(expectSelected ? "be" : "not be")} the applied layout.");
        }
        finally
        {
            FancyZonesTestHelper.CloseLayoutEditor(this);
        }
    }

    private void CloseExtraVirtualDesktop()
    {
        FancyZonesTestHelper.Step(this, "Closing the extra virtual desktop");
        KeyboardHelper.SendKeys(Key.Ctrl, Key.LWin, Key.Right);
        Thread.Sleep(800);
        KeyboardHelper.SendKeys(Key.Ctrl, Key.LWin, Key.F4);
        Thread.Sleep(800);
    }
}

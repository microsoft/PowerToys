// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MouseUtils.UITests;

[TestClass]
public class CursorBrakeSettingsTests : UITestBase
{
    private const string ModuleName = "CursorBrake";
    private const string GroupId = "MouseUtils_CursorBrakeTestId";
    private const string ModuleToggleId = "MouseUtils_CursorBrakeToggleId";
    private const string OptionsExpanderId = "MouseUtils_CursorBrakeOptionsId";
    private const string PushDistanceId = "MouseUtils_CursorBrakePushDistanceId";
    private const string FreeReturnId = "MouseUtils_CursorBrakeFreeReturnId";
    private const string EdgeFilterId = "MouseUtils_CursorBrakeEdgeFilterId";
    private const string DisableWhileDraggingId = "MouseUtils_CursorBrakeDisableWhileDraggingId";
    private const string CrossModifierId = "MouseUtils_CursorBrakeCrossModifierId";

    // Combo box item text, in index order. The index is the persisted value.
    private const string EdgeFilterAll = "All edges between monitors";
    private const string EdgeFilterSideBySide = "Side-by-side only";
    private const string EdgeFilterStacked = "Stacked only";
    private const string CrossModifierCtrl = "Holding Ctrl";
    private const string CrossModifierShift = "Holding Shift";

    // dllmain.cpp's HookThreadMain logs a session line, then exactly one install line, each time the
    // hook thread starts. The test waits for a fresh install line, so the hook is live before the move
    // assertions run rather than absent and trivially passing.
    private const string HookInstalledLogLine = "CursorBrake hook installed.";
    private const string HookInstallFailedLogPrefix = "Failed to install CursorBrake mouse hook, error: ";
    private const string SessionLocalLogLine = "CursorBrake is active: the session is local.";
    private const string SessionRemoteLogLine = "CursorBrake stands down: the session is remote.";

    // dwExtraInfo marker the module brakes like hardware input (TEST_MOVE_MARKER in CursorBrakeCore.h).
    // Unmarked injected moves pass the module untouched, so only a marked move exercises the engine.
    private static readonly UIntPtr TestMoveMarker = new(0x4342524B);

    // Settings applies module enable/disable through the runner asynchronously.
    private const int HookSettleTimeoutMs = 10_000;

    private const int MoveSteps = 8;

    // CursorBrakeCore.h's MOVE_CAP and FREE_RETURN_LIMIT in logical pixels, and its BASE_DPI. The
    // engine keeps every distance in physical pixels times BASE_DPI, so the crossing arithmetic here is
    // exact.
    private const int MoveCapPx = 10;
    private const int FreeReturnLimitPx = 100;
    private const int BaseDpi = 96;

    // How far past an edge a pushing move lands: past the per-move cap at every scale up to 400%, and
    // under it at every scale.
    private const int FlingPx = 50;
    private const int NudgePx = 4;

    private const string TwoMonitorsNeeded = "This case needs two monitors that share an edge.";

    private static readonly IDisposable ModuleSettings = SettingsConfigHelper.PreserveModuleSettings(ModuleName);

    public CursorBrakeSettingsTests()
        : base(PowerToysModule.PowerToysSettings, enableModules: new[] { ModuleName })
    {
    }

    [ClassCleanup]
    public static void RestoreModuleSettings() => ModuleSettings.Dispose();

    protected override void PrepareTestState()
    {
        // Each persistence case starts from values different from the ones it writes last, so a value
        // read after the restart can only have come from the UI.
        var settings = TestContext.TestName switch
        {
            // The cross key case's data row names the key it holds, so the module must read that key.
            nameof(HoldingTheCrossKeyCrossesOnTheFirstMove) =>
                CreateSettings(pushDistance: 300, freeReturn: true, edgeFilter: 0, disableWhileDragging: false, crossModifier: TestContext.TestData is [Key.Shift] ? 1 : 0),
            nameof(StrengthPersistsAtBoundaries) =>
                CreateSettings(pushDistance: 500, freeReturn: true, edgeFilter: 0, disableWhileDragging: false, crossModifier: 0),
            _ =>
                CreateSettings(pushDistance: 300, freeReturn: true, edgeFilter: 0, disableWhileDragging: false, crossModifier: 0),
        };
        MouseUtilsTestHelper.ReplaceModuleSettings(ModuleName, settings);
    }

    [TestCleanup]
    public async Task DisableModule()
    {
        await CaptureFailureArtifactsBeforeCleanupAsync();

        // A crossing case that ends early can leave a cross key down or the cursor on another
        // monitor, and neither may reach the next case.
        foreach (var key in new[] { Key.Ctrl, Key.Shift })
        {
            if (KeyboardHelper.IsKeyDown(key))
            {
                KeyboardHelper.ReleaseKey(key);
            }
        }

        try
        {
            MouseHelper.MoveTo(GetSystemMetrics(SmCxScreen) / 2, GetSystemMetrics(SmCyScreen) / 2);
        }
        catch (Win32Exception ex)
        {
            TestContext.WriteLine($"Could not park the cursor on the primary monitor: {ex.Message}");
        }

        try
        {
            if (Session.Has(By.AccessibilityId(ModuleToggleId), 500))
            {
                var toggle = Session.Find<ToggleSwitch>(By.AccessibilityId(ModuleToggleId), 500);
                if (toggle.IsOn)
                {
                    toggle.Toggle(false);
                    toggle.WaitForProperty("ToggleState", "Off", 5_000);
                }
            }
        }
        catch
        {
            // The base cleanup will stop the test-owned Runner if Settings is unreachable.
        }
    }

    [TestMethod]
    [TestCategory("MouseUtils")]
    [TestCategory("CursorBrake")]
    public void OptionsFollowModuleToggle()
    {
        MouseUtilsTestHelper.NavigateToMouseUtilities(this);

        var group = Session.Find<Element>(By.AccessibilityId(GroupId), 10_000);
        Assert.IsTrue(group.Displayed, "CursorBrake settings group was not visible.");

        MouseUtilsTestHelper.SetModuleEnabled(this, ModuleToggleId, false);
        AssertOptionsEnabled(expected: false);

        MouseUtilsTestHelper.SetModuleEnabled(this, ModuleToggleId, true);
        AssertOptionsEnabled(expected: true);
    }

    [TestMethod]
    [TestCategory("MouseUtils")]
    [TestCategory("CursorBrake")]
    public void StrengthPersistsAtBoundaries()
    {
        OpenOptions();

        SetStrength(300);
        AssertPersistedInt("push_distance", 300);
        SetStrength(0);
        AssertPersistedInt("push_distance", 0);
        SetStrength(1000);
        AssertPersistedInt("push_distance", 1000);

        RestartScope();
        AssertPersistedInt("push_distance", 1000);

        OpenOptions();
        Session.EnsureForeground();
        Session.Find<Slider>(By.AccessibilityId(PushDistanceId), 5_000).Focus();

        // Stepping the restored slider checks its UI value, since winapp reads no number from a WinUI
        // Slider.
        KeyboardHelper.SendKeys(Key.Left);
        AssertPersistedInt("push_distance", 975);
        KeyboardHelper.SendKeys(Key.Right);
        AssertPersistedInt("push_distance", 1000);
    }

    [TestMethod]
    [TestCategory("MouseUtils")]
    [TestCategory("CursorBrake")]
    public void FreeReturnAndDisableWhileDraggingPersistIndependently()
    {
        OpenOptions();

        AssertCheckBoxState(FreeReturnId, expectedChecked: true);
        AssertCheckBoxState(DisableWhileDraggingId, expectedChecked: false);

        SetCheckBox(FreeReturnId, check: false);
        AssertPersistedBool("free_return", false);
        AssertPersistedBool("disable_while_dragging", false);

        SetCheckBox(DisableWhileDraggingId, check: true);
        AssertPersistedBool("free_return", false);
        AssertPersistedBool("disable_while_dragging", true);

        SetCheckBox(FreeReturnId, check: true);
        AssertPersistedBool("free_return", true);
        AssertPersistedBool("disable_while_dragging", true);

        SetCheckBox(FreeReturnId, check: false);
        AssertPersistedBool("free_return", false);
        AssertPersistedBool("disable_while_dragging", true);

        // The final pair is the inverse of both defaults, so neither can survive the restart by
        // coincidence.
        RestartScope();
        OpenOptions();
        AssertCheckBoxState(FreeReturnId, expectedChecked: false);
        AssertCheckBoxState(DisableWhileDraggingId, expectedChecked: true);
        AssertPersistedBool("free_return", false);
        AssertPersistedBool("disable_while_dragging", true);
    }

    [TestMethod]
    [TestCategory("MouseUtils")]
    [TestCategory("CursorBrake")]
    public void ApplyToAndCrossKeyPersistEachValue()
    {
        OpenOptions();

        SelectComboItem(EdgeFilterId, EdgeFilterSideBySide);
        AssertPersistedInt("edge_filter", 1);
        SelectComboItem(EdgeFilterId, EdgeFilterAll);
        AssertPersistedInt("edge_filter", 0);
        SelectComboItem(EdgeFilterId, EdgeFilterStacked);
        AssertPersistedInt("edge_filter", 2);

        SelectComboItem(CrossModifierId, CrossModifierShift);
        AssertPersistedInt("cross_modifier", 1);
        SelectComboItem(CrossModifierId, CrossModifierCtrl);
        AssertPersistedInt("cross_modifier", 0);
        SelectComboItem(CrossModifierId, CrossModifierShift);
        AssertPersistedInt("cross_modifier", 1);

        // Changing one combo box must not move the other.
        AssertPersistedInt("edge_filter", 2);

        RestartScope();
        AssertPersistedInt("edge_filter", 2);
        AssertPersistedInt("cross_modifier", 1);

        OpenOptions();
        AssertComboText(EdgeFilterId, EdgeFilterStacked);
        AssertComboText(CrossModifierId, CrossModifierShift);
    }

    // The contract: a move that stays on one monitor is never held. On a single-monitor machine
    // that is every move, which makes the module a no-op there. Marked moves are the ones the module
    // judges, so an engine that held them would leave the cursor short of where it was sent.
    [TestMethod]
    [TestCategory("MouseUtils")]
    [TestCategory("CursorBrake")]
    public void SingleMonitorMarkedMovesAreNotHeld()
    {
        RestartHook();

        var primaryWidth = GetSystemMetrics(SmCxScreen);
        var primaryHeight = GetSystemMetrics(SmCyScreen);
        var start = (X: primaryWidth / 2, Y: primaryHeight / 2);
        var end = (X: primaryWidth * 3 / 4, Y: primaryHeight * 3 / 4);

        WithSettingsMinimized(() =>
        {
            MouseHelper.MoveTo(start.X, start.Y);
            for (var step = 1; step <= MoveSteps; step++)
            {
                var x = start.X + ((end.X - start.X) * step / MoveSteps);
                var y = start.Y + ((end.Y - start.Y) * step / MoveSteps);
                SendMarkedAbsoluteMove(x, y);
                AssertCursorLandsAt((x, y), $"A marked move within one monitor was held at step {step}");
            }

            for (var step = MoveSteps - 1; step >= 0; step--)
            {
                var x = start.X + ((end.X - start.X) * step / MoveSteps);
                var y = start.Y + ((end.Y - start.Y) * step / MoveSteps);
                SendMarkedAbsoluteMove(x, y);
                AssertCursorLandsAt((x, y), $"A marked move within one monitor was held at step {step}");
            }
        });
    }

    // The crossing cases. CI agents have one monitor, so these report Inconclusive there. Each takes
    // an edge from the live monitor layout: the primary monitor's first edge in enumeration order,
    // which is side-by-side or stacked as the layout has it, and the free return case prefers an edge
    // into a monitor of lower DPI. Every move lands on an exact pixel: a held move leaves the cursor on
    // the leaving monitor's last pixel before the edge, and a move that crosses leaves it where it was
    // sent.

    // The contract: marked moves into an inner edge are held until the push past it reaches Strength,
    // and the move that reaches it crosses. Each move here lands past the per-move cap, so it adds one
    // cap, which scales with the DPI like Strength does: the push crosses on move Strength / 10 on
    // every monitor.
    [TestMethod]
    [TestCategory("MouseUtils")]
    [TestCategory("CursorBrake")]
    public void AMarkedPushIsHeldUntilItReachesStrength()
    {
        var way = FindEdgeWay();
        var pushDistance = ReadPushDistance();
        RestartHook();

        WithSettingsMinimized(() =>
        {
            var expected = CrossingMove(pushDistance, FlingPx, way.SourceDpi);
            PlaceCursor(way.At(0, 0));
            var crossed = PushUntilCrossed(way, FlingPx, MaxMoves(expected));
            Assert.AreEqual(expected, crossed, $"Moves landing {FlingPx} px past the edge, at Strength {pushDistance}, should be held until move {expected} crosses. 0 means none of {MaxMoves(expected)} crossed.");
        });
    }

    // The contract: with the cross key held, Ctrl or Shift as the setting names it, the first marked
    // move into an inner edge crosses. The same move without the key is held, so the crossing comes
    // from the key. PrepareTestState seeds the setting with the row's key.
    [TestMethod]
    [TestCategory("MouseUtils")]
    [TestCategory("CursorBrake")]
    [DataRow(Key.Ctrl)]
    [DataRow(Key.Shift)]
    public void HoldingTheCrossKeyCrossesOnTheFirstMove(Key crossKey)
    {
        var way = FindEdgeWay();
        RestartHook();

        WithSettingsMinimized(() =>
        {
            PlaceCursor(way.At(0, 0));
            KeyboardHelper.PressKey(crossKey);
            try
            {
                AssertKeyState(crossKey, down: true);
                Assert.IsTrue(MarkedMoveCrosses(way, FlingPx), $"With {crossKey} held, the first marked move into the edge was held.");
            }
            finally
            {
                KeyboardHelper.ReleaseKey(crossKey);
            }

            AssertKeyState(crossKey, down: false);
            PlaceCursor(way.At(0, 0));
            Assert.IsFalse(MarkedMoveCrosses(way, FlingPx), $"Without {crossKey}, the first marked move into the edge crossed, so the crossing with {crossKey} held shows nothing about the key.");
        });
    }

    // The contract: right after a crossing, the move straight back crosses without resistance while
    // the cursor stays within 100 logical px of the crossed edge, counted from the first pixel across
    // and scaled by the DPI of the monitor it crossed onto. Past that reach, the way back brakes again.
    // The case checks the move straight back at once, after a wander 10 px inside the reach, and after
    // a wander 2 px past it. Over an edge between monitors of different DPI it checks both ways, into
    // the lower DPI first: a reach scaled by the monitor crossed from fails the way into lower DPI, and an
    // unscaled reach fails the way into higher DPI.
    [TestMethod]
    [TestCategory("MouseUtils")]
    [TestCategory("CursorBrake")]
    public void AMoveStraightBackAfterACrossingIsFree()
    {
        var ways = FindEdgeWays();
        if (ways.Count == 0)
        {
            Assert.Inconclusive(TwoMonitorsNeeded);
        }

        var mixed = ways.Where(candidate => candidate.TargetDpi < candidate.SourceDpi).ToList();
        var checkedWays = mixed.Count > 0 ? new[] { mixed[0], mixed[0].Reversed } : new[] { ways[0] };
        var pushDistance = ReadPushDistance();
        RestartHook();

        WithSettingsMinimized(() =>
        {
            foreach (var way in checkedWays)
            {
                var maxMoves = MaxMoves(CrossingMove(pushDistance, FlingPx, way.SourceDpi));
                var reachPx = FreeReturnLimitPx * way.TargetDpi / BaseDpi;
                var inside = way.At(1 + reachPx - 10, 0);
                var beyond = way.At(1 + reachPx + 2, 0);
                var direction = $"From {way.SourceDpi} to {way.TargetDpi} DPI";

                // A crossing leaves the cursor FlingPx - 1 px into the monitor, inside the reach at every scale.
                CrossFrom(way, maxMoves);
                Assert.IsTrue(MarkedMoveCrosses(way.Reversed, FlingPx), $"{direction}: the first move straight back after a crossing was held.");

                CrossFrom(way, maxMoves);
                SendMarkedAbsoluteMove(inside.X, inside.Y);
                AssertCursorLandsAt(inside, $"{direction}: a marked move within the monitor the cursor crossed onto was held");
                Assert.IsTrue(MarkedMoveCrosses(way.Reversed, FlingPx), $"{direction}: after a wander to {reachPx - 10} px into the monitor, inside free return's reach of {reachPx} px, the move straight back was held.");

                CrossFrom(way, maxMoves);
                SendMarkedAbsoluteMove(beyond.X, beyond.Y);
                AssertCursorLandsAt(beyond, $"{direction}: a marked move within the monitor the cursor crossed onto was held");
                Assert.IsFalse(MarkedMoveCrosses(way.Reversed, FlingPx), $"{direction}: after a wander to {reachPx + 2} px into the monitor, past free return's reach of {reachPx} px, the move back crossed without resistance.");
            }
        });
    }

    // The contract: the push needed is Strength in logical pixels of the monitor the cursor leaves, so
    // it scales by that monitor's DPI. Each move here lands under the per-move cap, so every pixel of
    // the push counts, and the move that crosses differs between the two ways over an edge whose
    // monitors differ in DPI.
    [TestMethod]
    [TestCategory("MouseUtils")]
    [TestCategory("CursorBrake")]
    public void ThePushNeededScalesWithTheLeavingMonitorsDpi()
    {
        var ways = FindEdgeWays();
        if (ways.Count == 0)
        {
            Assert.Inconclusive(TwoMonitorsNeeded);
        }

        var mixed = ways.Where(way => way.SourceDpi != way.TargetDpi).ToList();
        if (mixed.Count == 0)
        {
            Assert.Inconclusive("This case needs two monitors of different DPI that share an edge.");
        }

        var pushDistance = ReadPushDistance();
        RestartHook();

        WithSettingsMinimized(() =>
        {
            foreach (var way in new[] { mixed[0], mixed[0].Reversed })
            {
                // Unmarked moves to the push's pixels show each push lands exactly NudgePx past the edge.
                PlaceCursor(way.At(NudgePx, 0));
                PlaceCursor(way.At(NudgePx, 1));
                PlaceCursor(way.At(0, 0));

                var expected = CrossingMove(pushDistance, NudgePx, way.SourceDpi);
                var crossed = PushUntilCrossed(way, NudgePx, MaxMoves(expected));
                Assert.AreEqual(expected, crossed, $"Leaving a monitor at {way.SourceDpi} DPI for one at {way.TargetDpi} DPI, moves landing {NudgePx} px past the edge, at Strength {pushDistance}, should be held until move {expected} crosses. 0 means none of {MaxMoves(expected)} crossed.");
            }
        });
    }

    private void OpenOptions()
    {
        MouseUtilsTestHelper.NavigateToMouseUtilities(this);
        MouseUtilsTestHelper.SetModuleEnabled(this, ModuleToggleId, true);
        MouseUtilsTestHelper.ExpandSettings(this, OptionsExpanderId);

        Assert.IsTrue(Session.Has(By.AccessibilityId(PushDistanceId), 5_000), "Strength slider was not available.");
        Assert.IsTrue(Session.Has(By.AccessibilityId(FreeReturnId), 5_000), "Free return checkbox was not available.");
        Assert.IsTrue(Session.Has(By.AccessibilityId(EdgeFilterId), 5_000), "Apply to combo box was not available.");
        Assert.IsTrue(Session.Has(By.AccessibilityId(DisableWhileDraggingId), 5_000), "Disable resistance while dragging checkbox was not available.");
        Assert.IsTrue(Session.Has(By.AccessibilityId(CrossModifierId), 5_000), "Cross freely while combo box was not available.");
    }

    private void AssertOptionsEnabled(bool expected)
    {
        var result = WaitHelper.WaitForStable(
            () => Session.Find<Element>(By.AccessibilityId(OptionsExpanderId), 5_000).IsEnabled,
            actual => actual == expected,
            timeoutMS: HookSettleTimeoutMs,
            requiredConsecutiveMatches: 2,
            pollIntervalMS: 200);
        Assert.IsTrue(
            result.Succeeded,
            $"Resistance and behavior options should be {(expected ? "enabled" : "disabled")} with the module {(expected ? "on" : "off")}.");
    }

    private void SetStrength(int value)
    {
        Session.Find<Slider>(By.AccessibilityId(PushDistanceId), 5_000).SetValue(value);
    }

    private void SetCheckBox(string accessibilityId, bool check)
    {
        var checkBox = Session.Find<CheckBox>(By.AccessibilityId(accessibilityId), 5_000);
        if (checkBox.IsChecked != check)
        {
            checkBox.Invoke(msPostAction: 0);
        }

        Assert.IsTrue(
            checkBox.WaitForProperty("ToggleState", check ? "On" : "Off", 10_000),
            $"{accessibilityId} did not reach the expected checked state.");
    }

    private void AssertCheckBoxState(string accessibilityId, bool expectedChecked)
    {
        var checkBox = Session.Find<CheckBox>(By.AccessibilityId(accessibilityId), 5_000);
        Assert.AreEqual(expectedChecked, checkBox.IsChecked, $"{accessibilityId} did not have the expected persisted state.");
    }

    // WinUI puts the dropdown items in a popup that Settings surfaces as a separate window, which the
    // window-scoped test Session cannot see, so selection goes through a process-scoped session. The
    // combo and its items are driven through UIA patterns rather than clicks, because the combo can sit
    // below the visible part of the page.
    private static void SelectComboItem(string accessibilityId, string itemText)
    {
        var settingsProcess = Session.FromProcess("PowerToys.Settings", PowerToysModule.PowerToysSettings);
        var comboBox = settingsProcess.Find<ComboBox>(By.AccessibilityId(accessibilityId), 5_000);
        comboBox.ScrollIntoView();

        if (!string.Equals(comboBox.GetProperty("ExpandCollapseState"), "Expanded", StringComparison.OrdinalIgnoreCase))
        {
            comboBox.Invoke(msPostAction: 0);
            Assert.IsTrue(comboBox.WaitForProperty("ExpandCollapseState", "Expanded", 5_000), $"{accessibilityId} did not open.");
        }

        settingsProcess.Find<Element>(By.Name(itemText), 5_000).Invoke(msPostAction: 0);

        // Selecting through UIA can leave the dropdown open, and an open popup would cover the next step.
        if (string.Equals(comboBox.GetProperty("ExpandCollapseState"), "Expanded", StringComparison.OrdinalIgnoreCase))
        {
            comboBox.Invoke(msPostAction: 0);
            Assert.IsTrue(comboBox.WaitForProperty("ExpandCollapseState", "Collapsed", 5_000), $"{accessibilityId} did not close.");
        }

        AssertComboText(accessibilityId, itemText);
    }

    private static void AssertComboText(string accessibilityId, string expectedText)
    {
        var settingsProcess = Session.FromProcess("PowerToys.Settings", PowerToysModule.PowerToysSettings);
        var comboBox = settingsProcess.Find<ComboBox>(By.AccessibilityId(accessibilityId), 5_000);
        Assert.IsTrue(
            comboBox.WaitForValue(expectedText, timeoutMS: 5_000),
            $"{accessibilityId} did not show '{expectedText}'. It showed '{comboBox.SelectedText}'.");
    }

    private static void AssertHookInstalledLocally(int installLinesBefore)
    {
        var result = WaitHelper.WaitForStable(
            ReadHookLogLines,
            lines => CountInstallLogLines(lines) > installLinesBefore,
            timeoutMS: HookSettleTimeoutMs,
            requiredConsecutiveMatches: 1,
            pollIntervalMS: 200);
        Assert.IsTrue(result.Succeeded, "CursorBrake's hook thread logged no install result after the module was enabled.");

        var lines = result.LastObservation!;
        var installIndex = lines.FindLastIndex(IsInstallLogLine);
        var installLine = lines[installIndex];
        if (installLine.Contains(HookInstallFailedLogPrefix, StringComparison.Ordinal))
        {
            Assert.Fail($"CursorBrake failed to install its mouse hook: {installLine}");
        }

        // The session line logged just before this install says whether the engine judges moves at all.
        var sessionIndex = lines.FindLastIndex(installIndex, line => line.Contains(SessionLocalLogLine, StringComparison.Ordinal) || line.Contains(SessionRemoteLogLine, StringComparison.Ordinal));
        if (sessionIndex >= 0 && lines[sessionIndex].Contains(SessionRemoteLogLine, StringComparison.Ordinal))
        {
            Assert.Inconclusive("The test session is remote, where CursorBrake stands down by design, so no marked move reaches its engine.");
        }
    }

    private static bool IsInstallLogLine(string line) =>
        line.Contains(HookInstalledLogLine, StringComparison.Ordinal) || line.Contains(HookInstallFailedLogPrefix, StringComparison.Ordinal);

    private static int CountInstallLogLines(List<string>? lines) => lines?.Count(IsInstallLogLine) ?? 0;

    // Turns the module off and on and waits for the new hook thread's install line, so the moves that
    // follow reach a live hook.
    private void RestartHook()
    {
        MouseUtilsTestHelper.NavigateToMouseUtilities(this);
        MouseUtilsTestHelper.SetModuleEnabled(this, ModuleToggleId, false);
        var installLinesBefore = CountInstallLogLines(ReadHookLogLines());
        MouseUtilsTestHelper.SetModuleEnabled(this, ModuleToggleId, true);
        AssertHookInstalledLocally(installLinesBefore);
    }

    // Settings stays minimized while the moves run, so its window is not under them.
    private void WithSettingsMinimized(Action moves)
    {
        WindowHelper.MinimizeWindow(new IntPtr(Session.WindowHandle));
        try
        {
            moves();
        }
        finally
        {
            Session.EnsureForeground();
        }
    }

    private static void AssertCursorLandsAt((int X, int Y) point, string what)
    {
        var result = WaitHelper.WaitForStable(
            MouseHelper.GetMousePosition,
            position => position == point,
            timeoutMS: 1_000,
            requiredConsecutiveMatches: 2,
            pollIntervalMS: 20);
        Assert.IsTrue(result.Succeeded, $"{what}: sent to {point}, cursor at {result.LastObservation}.");
    }

    private static void AssertKeyState(Key key, bool down)
    {
        var result = WaitHelper.WaitForStable(
            () => KeyboardHelper.IsKeyDown(key),
            isDown => isDown == down,
            timeoutMS: 1_000,
            requiredConsecutiveMatches: 1,
            pollIntervalMS: 10);
        Assert.IsTrue(result.Succeeded, $"{key} did not go {(down ? "down" : "up")}.");
    }

    // Strength as the module read it at start, from the settings.json PrepareTestState wrote.
    private static int ReadPushDistance()
    {
        var result = WaitHelper.WaitForStable(
            () =>
            {
                try
                {
                    return ReadPersistedInt("push_distance");
                }
                catch (Exception ex) when (ex is IOException or JsonException or KeyNotFoundException or InvalidOperationException)
                {
                    // Settings may be mid-write; the next poll reads the whole file.
                    return null;
                }
            },
            pushDistance => pushDistance.HasValue,
            timeoutMS: 5_000,
            requiredConsecutiveMatches: 2,
            pollIntervalMS: 100);
        Assert.IsTrue(result.Succeeded, "settings.json held no readable push_distance to derive the crossing from.");
        return result.LastObservation!.Value;
    }

    // The number of the move that crosses when each move lands pastPx physical pixels past the edge.
    // The engine adds the overshoot, capped at MOVE_CAP scaled by the leaving monitor's DPI, for each
    // held move, and lets the move through once the sum reaches Strength scaled the same way.
    private static int CrossingMove(int pushDistance, int pastPx, int sourceDpi)
    {
        var perMove = Math.Min((long)pastPx * BaseDpi, (long)MoveCapPx * sourceDpi);
        var threshold = (long)pushDistance * sourceDpi;
        return (int)Math.Max(1, (threshold + perMove - 1) / perMove);
    }

    // Leaves room for a push that crosses late to report its move number.
    private static int MaxMoves(int expected) => (expected * 2) + 10;

    private static EdgeWay FindEdgeWay()
    {
        var ways = FindEdgeWays();
        if (ways.Count == 0)
        {
            Assert.Inconclusive(TwoMonitorsNeeded);
        }

        return ways[0];
    }

    // Every inner edge of the live layout, each way, the primary monitor's first. Each runs through
    // the middle of the span the two monitors share, away from their corners. A span of at least
    // 3 px keeps both coordinates along the edge that a move takes, the middle and the one after it,
    // on both monitors.
    private static List<EdgeWay> FindEdgeWays()
    {
        var monitors = MonitorInfo.GetAll().OrderByDescending(monitor => monitor.IsPrimary).ToList();
        var ways = new List<EdgeWay>();
        foreach (var source in monitors)
        {
            foreach (var target in monitors.Where(monitor => monitor != source))
            {
                var spanTop = Math.Max(source.Top, target.Top);
                var spanHeight = Math.Min(source.Bottom, target.Bottom) - spanTop;
                var spanLeft = Math.Max(source.Left, target.Left);
                var spanWidth = Math.Min(source.Right, target.Right) - spanLeft;
                if (target.Left == source.Right && spanHeight >= 3)
                {
                    ways.Add(NewEdgeWay(source.Right - 1, spanTop + (spanHeight / 2), 1, 0));
                }
                else if (target.Right == source.Left && spanHeight >= 3)
                {
                    ways.Add(NewEdgeWay(source.Left, spanTop + (spanHeight / 2), -1, 0));
                }
                else if (target.Top == source.Bottom && spanWidth >= 3)
                {
                    ways.Add(NewEdgeWay(spanLeft + (spanWidth / 2), source.Bottom - 1, 0, 1));
                }
                else if (target.Bottom == source.Top && spanWidth >= 3)
                {
                    ways.Add(NewEdgeWay(spanLeft + (spanWidth / 2), source.Top, 0, -1));
                }
            }
        }

        return ways;

        static EdgeWay NewEdgeWay(int lastX, int lastY, int inwardX, int inwardY) =>
            new(lastX, lastY, inwardX, inwardY, DpiAt(lastX, lastY), DpiAt(lastX + inwardX, lastY + inwardY));
    }

    // The effective DPI of the monitor holding the point, which is the DPI the module reads.
    private static int DpiAt(int x, int y)
    {
        var monitor = MonitorFromPoint(new NativePoint { X = x, Y = y }, MonitorDefaultToNull);
        if (monitor == IntPtr.Zero || GetDpiForMonitor(monitor, MdtEffectiveDpi, out var dpiX, out _) != 0)
        {
            throw new InvalidOperationException($"Could not read the DPI of the monitor at ({x},{y}).");
        }

        return (int)dpiX;
    }

    // Puts the cursor on a pixel with an unmarked move. The module passes unmarked injected input
    // through as absolute input, which starts the push and the free return over.
    private static void PlaceCursor((int X, int Y) point)
    {
        SendAbsoluteMove(point.X, point.Y, UIntPtr.Zero);
        AssertCursorLandsAt(point, "An unmarked move, which the module never holds, did not land where it was sent");
    }

    // Starts from the leaving monitor's last pixel with no push or free return, and pushes until a
    // move crosses.
    private static void CrossFrom(EdgeWay way, int maxMoves)
    {
        PlaceCursor(way.At(0, 0));
        Assert.AreNotEqual(0, PushUntilCrossed(way, FlingPx, maxMoves), $"None of {maxMoves} marked moves crossed the edge.");
    }

    // Sends one marked move from the cursor to pastPx pixels across the edge and returns whether it
    // crossed. The move takes the other of two coordinates along the edge than the cursor's, so a held
    // move, which leaves the cursor on the leaving monitor's last pixel at the move's coordinate, ends
    // on a pixel the cursor was not on.
    private static bool MarkedMoveCrosses(EdgeWay way, int pastPx)
    {
        var along = way.AlongOf(MouseHelper.GetMousePosition()) == 0 ? 1 : 0;
        var target = way.At(pastPx, along);
        var held = way.At(0, along);
        SendMarkedAbsoluteMove(target.X, target.Y);

        var result = WaitHelper.WaitForStable(
            MouseHelper.GetMousePosition,
            position => position == target || position == held,
            timeoutMS: 2_000,
            requiredConsecutiveMatches: 2,
            pollIntervalMS: 10);
        Assert.IsTrue(result.Succeeded, $"A marked move to {target} neither crossed there nor was held at {held}; the cursor is at {result.LastObservation}.");
        return result.LastObservation == target;
    }

    // Pushes into the edge one marked move at a time and returns the number of the move that crossed,
    // or 0 when none of maxMoves did. Every move before it was held.
    private static int PushUntilCrossed(EdgeWay way, int pastPx, int maxMoves)
    {
        for (var move = 1; move <= maxMoves; move++)
        {
            if (MarkedMoveCrosses(way, pastPx))
            {
                return move;
            }
        }

        return 0;
    }

    // Returns the session and install lines from every module log, oldest file first, in file order.
    private static List<string>? ReadHookLogLines()
    {
        var logRoot = Path.Combine(ModuleSettingsDirectory, "ModuleInterface", "Logs");
        if (!Directory.Exists(logRoot))
        {
            return null;
        }

        var lines = new List<string>();
        foreach (var file in new DirectoryInfo(logRoot).EnumerateFiles("*.log", SearchOption.AllDirectories).OrderBy(file => file.LastWriteTimeUtc))
        {
            // The runner holds the log open for writing, so it is read with sharing.
            using var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            while (reader.ReadLine() is { } line)
            {
                if (IsInstallLogLine(line) || line.Contains(SessionLocalLogLine, StringComparison.Ordinal) || line.Contains(SessionRemoteLogLine, StringComparison.Ordinal))
                {
                    lines.Add(line);
                }
            }
        }

        return lines;
    }

    private static void SendMarkedAbsoluteMove(int x, int y) => SendAbsoluteMove(x, y, TestMoveMarker);

    // Each pixel owns an equal share of the 0-65535 range across the virtual desktop. Aiming at the
    // middle of the pixel's share lands the move on that pixel exactly.
    private static void SendAbsoluteMove(int x, int y, UIntPtr extraInfo)
    {
        var left = GetSystemMetrics(SmXVirtualScreen);
        var top = GetSystemMetrics(SmYVirtualScreen);
        var width = GetSystemMetrics(SmCxVirtualScreen);
        var height = GetSystemMetrics(SmCyVirtualScreen);

        var inputs = new[]
        {
            new Input
            {
                Type = InputMouse,
                Mouse = new MouseInput
                {
                    Dx = (int)((((long)(x - left) * 65536) + 32768) / width),
                    Dy = (int)((((long)(y - top) * 65536) + 32768) / height),
                    DwFlags = MouseEventFMove | MouseEventFAbsolute | MouseEventFVirtualDesk,
                    DwExtraInfo = extraInfo,
                },
            },
        };

        if (SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>()) != inputs.Length)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    private static void AssertPersistedBool(string propertyName, bool expected)
    {
        var result = WaitHelper.WaitForStable(
            () => ReadPersistedBool(propertyName),
            actual => actual == expected,
            timeoutMS: 10_000,
            requiredConsecutiveMatches: 2,
            pollIntervalMS: 200);
        Assert.IsTrue(
            result.Succeeded,
            $"{propertyName} did not persist as {expected}. Last observed value: {result.LastObservation}.");
    }

    private static void AssertPersistedInt(string propertyName, int expected)
    {
        var result = WaitHelper.WaitForStable(
            () => ReadPersistedInt(propertyName),
            actual => actual == expected,
            timeoutMS: 10_000,
            requiredConsecutiveMatches: 2,
            pollIntervalMS: 200);
        Assert.IsTrue(
            result.Succeeded,
            $"{propertyName} did not persist as {expected}. Last observed value: {result.LastObservation}.");
    }

    private static bool? ReadPersistedBool(string propertyName)
    {
        if (!File.Exists(SettingsPath))
        {
            return null;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(SettingsPath));
        return document.RootElement.GetProperty("properties").GetProperty(propertyName).GetProperty("value").GetBoolean();
    }

    private static int? ReadPersistedInt(string propertyName)
    {
        if (!File.Exists(SettingsPath))
        {
            return null;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(SettingsPath));
        return document.RootElement.GetProperty("properties").GetProperty(propertyName).GetProperty("value").GetInt32();
    }

    private static string ModuleSettingsDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Microsoft",
        "PowerToys",
        ModuleName);

    private static string SettingsPath => Path.Combine(ModuleSettingsDirectory, "settings.json");

    private static string CreateSettings(int pushDistance, bool freeReturn, int edgeFilter, bool disableWhileDragging, int crossModifier) => $$"""
        {
          "name": "CursorBrake",
          "version": "1.0",
          "properties": {
            "push_distance": { "value": {{pushDistance}} },
            "free_return": { "value": {{freeReturn.ToString().ToLowerInvariant()}} },
            "edge_filter": { "value": {{edgeFilter}} },
            "disable_while_dragging": { "value": {{disableWhileDragging.ToString().ToLowerInvariant()}} },
            "cross_modifier": { "value": {{crossModifier}} }
          }
        }
        """;

    private const int SmCxScreen = 0;
    private const int SmCyScreen = 1;
    private const int SmXVirtualScreen = 76;
    private const int SmYVirtualScreen = 77;
    private const int SmCxVirtualScreen = 78;
    private const int SmCyVirtualScreen = 79;

    private const uint InputMouse = 0;
    private const uint MouseEventFMove = 0x0001;
    private const uint MouseEventFVirtualDesk = 0x4000;
    private const uint MouseEventFAbsolute = 0x8000;

    private const uint MonitorDefaultToNull = 0;
    private const int MdtEffectiveDpi = 0;

    // One way over an inner edge: (LastX, LastY) is the leaving monitor's last pixel before the edge,
    // (InwardX, InwardY) the unit step across it, and the DPIs are the leaving and the entered
    // monitor's.
    private readonly record struct EdgeWay(int LastX, int LastY, int InwardX, int InwardY, int SourceDpi, int TargetDpi)
    {
        // The same edge crossed the other way, whose last pixel is this way's first pixel across.
        public EdgeWay Reversed => new(LastX + InwardX, LastY + InwardY, -InwardX, -InwardY, TargetDpi, SourceDpi);

        // The pixel past pixels across the edge from the last pixel, so 0 is the last pixel and 1 the
        // first across, moved along pixels along the edge.
        public (int X, int Y) At(int past, int along) =>
            (LastX + (InwardX * past) + (Math.Abs(InwardY) * along), LastY + (InwardY * past) + (Math.Abs(InwardX) * along));

        // How far along the edge a point lies from the last pixel.
        public int AlongOf((int X, int Y) point) => InwardX != 0 ? point.Y - LastY : point.X - LastX;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int Dx;
        public int Dy;
        public uint MouseData;
        public uint DwFlags;
        public uint Time;
        public UIntPtr DwExtraInfo;
    }

    // INPUT is a union; the mouse member is the largest, so it alone sets the size.
    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public MouseInput Mouse;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, Input[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(NativePoint point, uint flags);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr monitor, int dpiType, out uint dpiX, out uint dpiY);
}

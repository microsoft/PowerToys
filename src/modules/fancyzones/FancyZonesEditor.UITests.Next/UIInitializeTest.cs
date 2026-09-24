// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using FancyZonesEditor.UITests.Utils;
using FancyZonesEditorCommon.Data;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Globalization;
using System.Text.Json;

namespace FancyZonesEditor.UITests;

public abstract class UIInitializeTestBase : FancyZonesEditorTestBase
{
    private const string Monitor1Name = "Monitor 1";
    private const string Monitor2Name = "Monitor 2";

    protected UIInitializeTestBase(Action<FancyZonesEditorFiles> seedFixture)
    {
        seedFixture(Files);
    }

    protected static Element FindLayoutByExactName(Session session, string layoutName)
    {
        var candidates = session.FindAll<Element>(By.Name(layoutName), 2_000)
            .Where(x =>
                string.Equals(x.Name, layoutName, StringComparison.Ordinal) &&
                string.Equals(x.ControlType, "ListItem", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.AreEqual(1, candidates.Count, $"Expected one exact layout named '{layoutName}', but found {candidates.Count}.");
        return candidates[0];
    }

    protected static Element FindMonitorByExactName(Session session, string monitorName)
    {
        var candidates = session.FindAll<Element>(By.Name(monitorName), 2_000)
            .Where(x =>
                string.Equals(x.Name, monitorName, StringComparison.Ordinal) &&
                string.Equals(x.ControlType, "ListItem", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.AreEqual(1, candidates.Count, $"Expected one exact monitor named '{monitorName}', but found {candidates.Count}.");
        return candidates[0];
    }

    protected static AppliedLayouts.AppliedLayoutsListWrapper ReadAppliedLayoutsDurably(
        UITestBase test,
        Func<AppliedLayouts.AppliedLayoutsListWrapper, bool> predicate,
        string reason)
    {
        EditorUiTestHelper.Step(test, $"Waiting for applied-layouts file update: {reason}");

        var stableResult = WaitHelper.WaitForStable(
            observe: () =>
            {
                try
                {
                    return EditorUiTestHelper.ReadAppliedLayouts();
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
                {
                    return (AppliedLayouts.AppliedLayoutsListWrapper?)null;
                }
            },
            isMatch: data => data.HasValue && predicate(data.Value),
            timeoutMS: 10_000,
            requiredConsecutiveMatches: 2,
            pollIntervalMS: 100);

        Assert.IsTrue(
            stableResult.Succeeded && stableResult.LastObservation.HasValue,
            $"The applied-layouts file did not reach the expected state for '{reason}'.");

        return stableResult.LastObservation.Value;
    }

    protected static void SelectAndAssertMonitor(UITestBase test, Session session, string monitorName)
    {
        EditorUiTestHelper.Step(test, $"Selecting {monitorName}");
        var monitor = FindMonitorByExactName(session, monitorName);
        monitor.Click();

        monitor = FindMonitorByExactName(session, monitorName);
        Assert.IsTrue(monitor.Selected, $"Expected {monitorName} to be selected.");
    }

    protected static string Monitor1 => Monitor1Name;

    protected static string Monitor2 => Monitor2Name;
}

[TestClass]
public class UIInitializeEditorParamsVerifySelectedMonitorTests : UIInitializeTestBase
{
    public UIInitializeEditorParamsVerifySelectedMonitorTests()
        : base(EditorTestData.WriteForUIInitializeEditorParamsVerifySelectedMonitor)
    {
    }

    [TestMethod("FancyZonesEditor.Basic.EditorParams_VerifySelectedMonitor")]
    [TestCategory("FancyZones Editor #10")]
    public void EditorParams_VerifySelectedMonitor()
    {
        EditorUiTestHelper.EnsureEditorReady(this, Session);

        EditorUiTestHelper.Step(this, "Clicking monitors in sequence to verify persisted selection");
        FindMonitorByExactName(Session, Monitor1).Click();
        FindMonitorByExactName(Session, Monitor2).Click();

        Assert.IsFalse(FindMonitorByExactName(Session, Monitor1).Selected);
        Assert.IsTrue(FindMonitorByExactName(Session, Monitor2).Selected);
    }
}

[TestClass]
public class UIInitializeEditorParamsVerifyMonitorScalingTests : UIInitializeTestBase
{
    public UIInitializeEditorParamsVerifyMonitorScalingTests()
        : base(EditorTestData.WriteForUIInitializeEditorParamsVerifyMonitorScaling)
    {
    }

    [TestMethod]
    public void EditorParams_VerifyMonitorScaling()
    {
        EditorUiTestHelper.EnsureEditorReady(this, Session);

        SelectAndAssertMonitor(this, Session, Monitor1);
        var monitor = FindMonitorByExactName(Session, Monitor1);
        var scaling = monitor.Find<TextBlock>(By.AccessibilityId(EditorUiTestHelper.AccessibilityId.ScalingText));

        Assert.AreEqual("200%", scaling.Text);
    }
}

[TestClass]
public class UIInitializeEditorParamsVerifyMonitorResolutionTests : UIInitializeTestBase
{
    public UIInitializeEditorParamsVerifyMonitorResolutionTests()
        : base(EditorTestData.WriteForUIInitializeEditorParamsVerifyMonitorResolution)
    {
    }

    [TestMethod]
    public void EditorParams_VerifyMonitorResolution()
    {
        EditorUiTestHelper.EnsureEditorReady(this, Session);

        SelectAndAssertMonitor(this, Session, Monitor1);
        var monitor = FindMonitorByExactName(Session, Monitor1);
        var resolution = monitor.Find<TextBlock>(By.AccessibilityId(EditorUiTestHelper.AccessibilityId.ResolutionText));

        Assert.AreEqual("1920 × 1080", resolution.Text);
    }
}

[TestClass]
public class UIInitializeEditorParamsSpanAcrossMonitorsTests : UIInitializeTestBase
{
    public UIInitializeEditorParamsSpanAcrossMonitorsTests()
        : base(EditorTestData.WriteForUIInitializeEditorParamsSpanAcrossMonitors)
    {
    }

    [TestMethod]
    public void EditorParams_SpanAcrossMonitors()
    {
        EditorUiTestHelper.EnsureEditorReady(this, Session);

        SelectAndAssertMonitor(this, Session, Monitor1);

        var parameters = new EditorParameters().Read(new EditorParameters().File);
        Assert.IsTrue(parameters.SpanZonesAcrossMonitors, "Expected SpanZonesAcrossMonitors to remain true.");

        var editorWindow = new IntPtr(Session.WindowHandle);
        WindowHelper.RestoreWindow(editorWindow);

        var (displayWidth, _) = WindowHelper.GetDisplaySize();
        var expectedCenter = displayWidth / 2.0;
        var centered = WaitHelper.WaitForStable(
            observe: () =>
            {
                var (left, _, right, _) = WindowHelper.GetWindowBounds(editorWindow);
                return (left + right) / 2.0;
            },
            isMatch: actualCenter => Math.Abs(actualCenter - expectedCenter) <= 2.0,
            timeoutMS: 5_000,
            requiredConsecutiveMatches: 3,
            pollIntervalMS: 100);
        Assert.IsTrue(
            centered.Succeeded,
            $"Span-across-monitors mode should center the editor on the combined desktop. Expected center {expectedCenter}, actual {centered.LastObservation}.");
    }
}

[TestClass]
public class UIInitializeAppliedLayoutsLayoutsAppliedTests : UIInitializeTestBase
{
    private const string CustomLayoutName = "Custom layout 1";

    public UIInitializeAppliedLayoutsLayoutsAppliedTests()
        : base(EditorTestData.WriteForUIInitializeAppliedLayoutsLayoutsApplied)
    {
    }

    [TestMethod]
    public void AppliedLayouts_LayoutsApplied()
    {
        EditorUiTestHelper.EnsureEditorReady(this, Session);

        var layoutOnMonitor1 = FindLayoutByExactName(Session, EditorUiTestHelper.TemplateLayoutName.Columns);
        Assert.IsTrue(layoutOnMonitor1.Selected);

        SelectAndAssertMonitor(this, Session, Monitor2);
        var layoutOnMonitor2 = FindLayoutByExactName(Session, CustomLayoutName);
        Assert.IsTrue(layoutOnMonitor2.Selected);
    }
}

[TestClass]
public class UIInitializeAppliedLayoutsCustomLayoutsAppliedLayoutIdNotFoundTests : UIInitializeTestBase
{
    public UIInitializeAppliedLayoutsCustomLayoutsAppliedLayoutIdNotFoundTests()
        : base(EditorTestData.WriteForUIInitializeAppliedLayoutsCustomLayoutsAppliedLayoutIdNotFound)
    {
    }

    [TestMethod]
    public void AppliedLayouts_CustomLayoutsApplied_LayoutIdNotFound()
    {
        EditorUiTestHelper.EnsureEditorReady(this, Session);

        var emptyLayout = FindLayoutByExactName(Session, EditorUiTestHelper.TemplateLayoutName.Blank);
        Assert.IsTrue(emptyLayout.Selected);
    }
}

[TestClass]
public class UIInitializeAppliedLayoutsNoLayoutsAppliedCustomDefaultLayoutTests : UIInitializeTestBase
{
    private const string CustomLayoutName = "Custom layout 1";

    public UIInitializeAppliedLayoutsNoLayoutsAppliedCustomDefaultLayoutTests()
        : base(EditorTestData.WriteForUIInitializeAppliedLayoutsNoLayoutsAppliedCustomDefaultLayout)
    {
    }

    [TestMethod]
    public void AppliedLayouts_NoLayoutsApplied_CustomDefaultLayout()
    {
        EditorUiTestHelper.EnsureEditorReady(this, Session);

        var defaultLayout = FindLayoutByExactName(Session, CustomLayoutName);
        defaultLayout.Click();

        defaultLayout = FindLayoutByExactName(Session, CustomLayoutName);
        Assert.IsTrue(defaultLayout.Selected);
    }
}

[TestClass]
public class UIInitializeAppliedLayoutsNoLayoutsAppliedTemplateDefaultLayoutTests : UIInitializeTestBase
{
    public UIInitializeAppliedLayoutsNoLayoutsAppliedTemplateDefaultLayoutTests()
        : base(EditorTestData.WriteForUIInitializeAppliedLayoutsNoLayoutsAppliedTemplateDefaultLayout)
    {
    }

    [TestMethod]
    public void AppliedLayouts_NoLayoutsApplied_TemplateDefaultLayout()
    {
        EditorUiTestHelper.EnsureEditorReady(this, Session);

        var defaultLayout = FindLayoutByExactName(Session, EditorUiTestHelper.TemplateLayoutName.Grid);
        defaultLayout.Click();
        defaultLayout = FindLayoutByExactName(Session, EditorUiTestHelper.TemplateLayoutName.Grid);
        Assert.IsTrue(defaultLayout.Selected);

        EditorUiTestHelper.OpenEditLayoutDialog(this, Session, EditorUiTestHelper.TemplateLayoutName.Grid);
        var zoneCountSlider = Session.Find<Element>(By.AccessibilityId(EditorUiTestHelper.AccessibilityId.TemplateZoneSlider));
        Assert.AreEqual(6, EditorUiTestHelper.ReadSliderValueAsInt(this, zoneCountSlider, EditorUiTestHelper.AccessibilityId.TemplateZoneSlider));

        var spacingSlider = Session.Find<Element>(By.AccessibilityId(EditorUiTestHelper.AccessibilityId.SpacingSlider));
        var spacingToggle = Session.Find<Element>(By.AccessibilityId(EditorUiTestHelper.AccessibilityId.SpacingToggle));
        Assert.AreEqual(5, EditorUiTestHelper.ReadSliderValueAsInt(this, spacingSlider, EditorUiTestHelper.AccessibilityId.SpacingSlider));
        Assert.IsTrue(spacingSlider.IsEnabled);
        Assert.AreEqual("On", spacingToggle.GetProperty("ToggleState"));

        var sensitivitySlider = Session.Find<Element>(By.AccessibilityId(EditorUiTestHelper.AccessibilityId.SensitivitySlider));
        Assert.AreEqual(20, EditorUiTestHelper.ReadSliderValueAsInt(this, sensitivitySlider, EditorUiTestHelper.AccessibilityId.SensitivitySlider));
        Assert.IsNotNull(Session.Find<Element>(By.AccessibilityId(EditorUiTestHelper.AccessibilityId.HorizontalDefaultButtonChecked)));
    }
}

[TestClass]
public class UIInitializeAppliedLayoutsVerifyDisconnectedMonitorsLayoutsAreNotChangedTests : UIInitializeTestBase
{
    public UIInitializeAppliedLayoutsVerifyDisconnectedMonitorsLayoutsAreNotChangedTests()
        : base(EditorTestData.WriteForUIInitializeAppliedLayoutsVerifyDisconnectedMonitorsLayoutsAreNotChanged)
    {
    }

    [TestMethod]
    public void AppliedLayouts_VerifyDisconnectedMonitorsLayoutsAreNotChanged()
    {
        EditorUiTestHelper.EnsureEditorReady(this, Session);

        FindLayoutByExactName(Session, EditorUiTestHelper.TemplateLayoutName.Rows).Click();

        var data = ReadAppliedLayoutsDurably(
            this,
            applied => applied.AppliedLayouts.Count == 3,
            "updating connected monitor while preserving disconnected monitor layouts");

        Assert.IsNotNull(data.AppliedLayouts.Find(x => x.Device.Monitor == "monitor-1"));
        Assert.IsNotNull(data.AppliedLayouts.Find(x => x.Device.Monitor == "monitor-2"));
        Assert.IsNotNull(data.AppliedLayouts.Find(x => x.Device.Monitor == "monitor-3"));
    }
}

[TestClass]
public class UIInitializeAppliedLayoutsVerifyOtherVirtualDesktopsAreNotChangedTests : UIInitializeTestBase
{
    private const string VirtualDesktop1 = "{11111111-1111-1111-1111-111111111111}";
    private const string VirtualDesktop2 = "{22222222-2222-2222-2222-222222222222}";

    public UIInitializeAppliedLayoutsVerifyOtherVirtualDesktopsAreNotChangedTests()
        : base(EditorTestData.WriteForUIInitializeAppliedLayoutsVerifyOtherVirtualDesktopsAreNotChanged)
    {
    }

    [TestMethod]
    public void AppliedLayouts_VerifyOtherVirtualDesktopsAreNotChanged()
    {
        EditorUiTestHelper.EnsureEditorReady(this, Session);

        FindLayoutByExactName(Session, EditorUiTestHelper.TemplateLayoutName.Rows).Click();

        var data = ReadAppliedLayoutsDurably(
            this,
            applied => applied.AppliedLayouts.Count == 2
                && applied.AppliedLayouts.Any(x => x.Device.VirtualDesktop == VirtualDesktop1)
                && applied.AppliedLayouts.Any(x => x.Device.VirtualDesktop == VirtualDesktop2),
            "updating only the current virtual desktop layout");

        var untouchedDesktopLayout = data.AppliedLayouts.Find(x => x.Device.VirtualDesktop == VirtualDesktop2);
        var currentDesktopLayout = data.AppliedLayouts.Find(x => x.Device.VirtualDesktop == VirtualDesktop1);

        Assert.AreEqual(Constants.TemplateLayoutJsonTags[Constants.TemplateLayout.Focus], untouchedDesktopLayout.AppliedLayout.Type);
        Assert.AreEqual(Constants.TemplateLayoutJsonTags[Constants.TemplateLayout.Rows], currentDesktopLayout.AppliedLayout.Type);
    }
}
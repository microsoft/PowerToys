// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using FancyZonesEditor.UITests.Utils;
using FancyZonesEditorCommon.Data;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Globalization;
using System.Text.RegularExpressions;

namespace FancyZonesEditor.UITests;

[TestClass]
public class ApplyLayoutTests : FancyZonesEditorTestBase
{
    private const int FirstMonitorNumber = 1;
    private const int SecondMonitorNumber = 2;
    private const string CustomLayoutName = "Custom layout";
    private const string CustomLayoutUuid = "{E7807D0D-6223-4883-B15B-1F3883944C09}";
    private const string ColumnsType = "columns";

    public ApplyLayoutTests()
    {
        EditorTestData.WriteForApplyLayoutTests(Files);
    }

    [TestMethod]
    public void ApplyCustomLayout()
    {
        EditorUiTestHelper.EnsureEditorReady(this, Session);

        var data = EditorUiTestHelper.ApplyLayoutAndWait(
            this,
            Session,
            CustomLayoutName,
            applied => FindFixtureMonitorLayout(applied, FirstMonitorNumber)?.AppliedLayout.Uuid == CustomLayoutUuid,
            $"Monitor {FirstMonitorNumber} to use custom layout {CustomLayoutUuid}");
        AssertFixtureMonitorLayouts(data);

        var firstMonitorLayout = FindFixtureMonitorLayout(data, FirstMonitorNumber)!.Value;
        Assert.AreEqual(CustomLayoutUuid, firstMonitorLayout.AppliedLayout.Uuid);
        Assert.AreEqual(FirstMonitorNumber, firstMonitorLayout.Device.MonitorNumber);
    }

    [TestMethod]
    public void ApplyTemplateLayout()
    {
        EditorUiTestHelper.EnsureEditorReady(this, Session);

        var data = EditorUiTestHelper.ApplyLayoutAndWait(
            this,
            Session,
            EditorUiTestHelper.TemplateLayoutName.Columns,
            applied => FindFixtureMonitorLayout(applied, FirstMonitorNumber)?.AppliedLayout.Type == ColumnsType,
            $"Monitor {FirstMonitorNumber} to use template type {ColumnsType}");
        AssertFixtureMonitorLayouts(data);

        var firstMonitorLayout = FindFixtureMonitorLayout(data, FirstMonitorNumber)!.Value;
        Assert.AreEqual(ColumnsType, firstMonitorLayout.AppliedLayout.Type);
        Assert.AreEqual(FirstMonitorNumber, firstMonitorLayout.Device.MonitorNumber);
    }

    [TestMethod("FancyZonesEditor.Basic.ApplyLayoutsOnEachMonitor")]
    [TestCategory("FancyZones Editor #10")]
    public void ApplyLayoutsOnEachMonitor()
    {
        EditorUiTestHelper.EnsureEditorReady(this, Session);

        _ = EditorUiTestHelper.ApplyLayoutAndWait(
            this,
            Session,
            EditorUiTestHelper.TemplateLayoutName.Columns,
            applied => FindFixtureMonitorLayout(applied, FirstMonitorNumber)?.AppliedLayout.Type == ColumnsType,
            $"Monitor {FirstMonitorNumber} to use template type {ColumnsType}");

        EditorUiTestHelper.SelectMonitor(this, Session, "Monitor 2");
        var data = EditorUiTestHelper.ApplyLayoutAndWait(
            this,
            Session,
            CustomLayoutName,
            applied => FindFixtureMonitorLayout(applied, SecondMonitorNumber)?.AppliedLayout.Uuid == CustomLayoutUuid,
            $"Monitor {SecondMonitorNumber} to use custom layout {CustomLayoutUuid}");

        EditorUiTestHelper.SelectMonitor(this, Session, "Monitor 1");
        Assert.IsTrue(
            Session.WaitFor(
                () => Session.FindAll<Element>(By.Name(EditorUiTestHelper.TemplateLayoutName.Columns), 0)
                    .Any(element =>
                        string.Equals(element.Name, EditorUiTestHelper.TemplateLayoutName.Columns, StringComparison.Ordinal) &&
                        string.Equals(element.ControlType, "ListItem", StringComparison.OrdinalIgnoreCase) &&
                        element.Selected),
                10_000,
                250),
            "Monitor 1 did not restore its selected Columns layout card.");
        AssertFixtureMonitorLayouts(data);

        var firstMonitorLayout = FindFixtureMonitorLayout(data, FirstMonitorNumber)!.Value;
        var secondMonitorLayout = FindFixtureMonitorLayout(data, SecondMonitorNumber)!.Value;

        Assert.AreEqual(ColumnsType, firstMonitorLayout.AppliedLayout.Type);
        Assert.AreEqual(CustomLayoutUuid, secondMonitorLayout.AppliedLayout.Uuid);
    }

    [TestMethod("FancyZonesEditor.Basic.ApplyTemplateWithDifferentParametersOnEachMonitor")]
    [TestCategory("FancyZones Editor #10")]
    public void ApplyTemplateWithDifferentParametersOnEachMonitor()
    {
        EditorUiTestHelper.EnsureEditorReady(this, Session);

        _ = EditorUiTestHelper.ApplyLayoutAndWait(
            this,
            Session,
            EditorUiTestHelper.TemplateLayoutName.Columns,
            applied => FindFixtureMonitorLayout(applied, FirstMonitorNumber)?.AppliedLayout.Type == ColumnsType,
            $"Monitor {FirstMonitorNumber} to use template type {ColumnsType}");

        EditorUiTestHelper.OpenEditLayoutDialog(this, Session, EditorUiTestHelper.TemplateLayoutName.Columns);
        var firstMonitorSlider = Session.Find<Element>(By.AccessibilityId(EditorUiTestHelper.AccessibilityId.TemplateZoneSlider));
        firstMonitorSlider.Focus();
        EditorUiTestHelper.Step(this, "Increasing Monitor 1 zone count twice");
        KeyboardHelper.SendKeys(Key.Right);
        KeyboardHelper.SendKeys(Key.Right);
        var expectedFirstLayoutZoneCount = ReadZoneCount();
        EditorUiTestHelper.Step(this, $"Setting Monitor 1 zone count to {expectedFirstLayoutZoneCount}");
        Session.Find<Button>(EditorUiTestHelper.ElementName.Save).Invoke();
        _ = EditorUiTestHelper.WaitForAppliedLayouts(
            this,
            applied => FindFixtureMonitorLayout(applied, FirstMonitorNumber)?.AppliedLayout.ZoneCount == expectedFirstLayoutZoneCount,
            $"Monitor {FirstMonitorNumber} to persist zone count {expectedFirstLayoutZoneCount}");

        EditorUiTestHelper.SelectMonitor(this, Session, "Monitor 2");
        _ = EditorUiTestHelper.ApplyLayoutAndWait(
            this,
            Session,
            EditorUiTestHelper.TemplateLayoutName.Columns,
            applied => FindFixtureMonitorLayout(applied, SecondMonitorNumber)?.AppliedLayout.Type == ColumnsType,
            $"Monitor {SecondMonitorNumber} to use template type {ColumnsType}");

        EditorUiTestHelper.OpenEditLayoutDialog(this, Session, EditorUiTestHelper.TemplateLayoutName.Columns);
        var secondMonitorSlider = Session.Find<Element>(By.AccessibilityId(EditorUiTestHelper.AccessibilityId.TemplateZoneSlider));
        secondMonitorSlider.Focus();
        EditorUiTestHelper.Step(this, "Decreasing Monitor 2 zone count once");
        KeyboardHelper.SendKeys(Key.Left);
        var expectedSecondLayoutZoneCount = ReadZoneCount();
        EditorUiTestHelper.Step(this, $"Setting Monitor 2 zone count to {expectedSecondLayoutZoneCount}");
        Session.Find<Button>(EditorUiTestHelper.ElementName.Save).Invoke();
        var data = EditorUiTestHelper.WaitForAppliedLayouts(
            this,
            applied => FindFixtureMonitorLayout(applied, SecondMonitorNumber)?.AppliedLayout.ZoneCount == expectedSecondLayoutZoneCount,
            $"Monitor {SecondMonitorNumber} to persist zone count {expectedSecondLayoutZoneCount}");

        EditorUiTestHelper.SelectMonitor(this, Session, "Monitor 1");
        EditorUiTestHelper.OpenEditLayoutDialog(this, Session, EditorUiTestHelper.TemplateLayoutName.Columns);
        Assert.AreEqual(expectedFirstLayoutZoneCount, ReadZoneCount());
        Session.Find<Button>(EditorUiTestHelper.ElementName.Cancel).Invoke();
        AssertFixtureMonitorLayouts(data);

        var firstMonitorLayout = FindFixtureMonitorLayout(data, FirstMonitorNumber)!.Value;
        var secondMonitorLayout = FindFixtureMonitorLayout(data, SecondMonitorNumber)!.Value;

        Assert.AreEqual(ColumnsType, firstMonitorLayout.AppliedLayout.Type);
        Assert.AreEqual(expectedFirstLayoutZoneCount, firstMonitorLayout.AppliedLayout.ZoneCount);
        Assert.AreEqual(ColumnsType, secondMonitorLayout.AppliedLayout.Type);
        Assert.AreEqual(expectedSecondLayoutZoneCount, secondMonitorLayout.AppliedLayout.ZoneCount);
    }

    private static AppliedLayouts.AppliedLayoutWrapper? FindFixtureMonitorLayout(
        AppliedLayouts.AppliedLayoutsListWrapper data,
        int monitorNumber)
    {
        var expectedMonitor = $"monitor-{monitorNumber}";
        var expectedInstance = $"instance-id-{monitorNumber}";
        var expectedSerial = $"serial-number-{monitorNumber}";
        var matches = data.AppliedLayouts
            .Where(layout =>
                layout.Device.MonitorNumber == monitorNumber &&
                layout.Device.Monitor == expectedMonitor &&
                layout.Device.MonitorInstance == expectedInstance &&
                layout.Device.SerialNumber == expectedSerial)
            .ToList();
        return matches.Count == 1 ? matches[0] : null;
    }

    private static void AssertFixtureMonitorLayouts(AppliedLayouts.AppliedLayoutsListWrapper data)
    {
        Assert.IsTrue(FindFixtureMonitorLayout(data, FirstMonitorNumber).HasValue, "Monitor 1 layout record was not persisted.");
        Assert.IsTrue(FindFixtureMonitorLayout(data, SecondMonitorNumber).HasValue, "Monitor 2 layout record was not persisted.");
    }

    private int ReadZoneCount()
    {
        var value = Session.Find<Element>(By.AccessibilityId(EditorUiTestHelper.AccessibilityId.TemplateZoneSlider)).GetValue();
        if (double.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var numericValue))
        {
            return (int)Math.Round(numericValue, MidpointRounding.AwayFromZero);
        }

        var numbers = Regex.Matches(value, @"-?\d+");
        Assert.IsTrue(numbers.Count > 0, $"The zone-count slider exposed no numeric value: '{value}'.");
        return int.Parse(numbers[^1].Value, CultureInfo.InvariantCulture);
    }
}
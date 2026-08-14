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

        var layoutCard = Session.Find<Element>(CustomLayoutName);
        Assert.IsFalse(layoutCard.Selected);

        EditorUiTestHelper.Step(this, $"Applying custom layout '{CustomLayoutName}' on Monitor 1");
        layoutCard.Click();

        layoutCard = Session.Find<Element>(CustomLayoutName);
        Assert.IsTrue(layoutCard.Selected);

        var data = EditorUiTestHelper.ReadAppliedLayouts();
        Assert.AreEqual(2, data.AppliedLayouts.Count);

        var firstMonitorLayout = data.AppliedLayouts.Find(x => x.Device.MonitorNumber == FirstMonitorNumber);
        Assert.AreEqual(CustomLayoutUuid, firstMonitorLayout.AppliedLayout.Uuid);
        Assert.AreEqual(FirstMonitorNumber, firstMonitorLayout.Device.MonitorNumber);
    }

    [TestMethod]
    public void ApplyTemplateLayout()
    {
        EditorUiTestHelper.EnsureEditorReady(this, Session);

        var layoutCard = Session.Find<Element>(EditorUiTestHelper.TemplateLayoutName.Columns);
        Assert.IsFalse(layoutCard.Selected);

        EditorUiTestHelper.Step(this, "Applying template layout 'Columns' on Monitor 1");
        layoutCard.Click();

        layoutCard = Session.Find<Element>(EditorUiTestHelper.TemplateLayoutName.Columns);
        Assert.IsTrue(layoutCard.Selected);

        var data = EditorUiTestHelper.ReadAppliedLayouts();
        Assert.AreEqual(2, data.AppliedLayouts.Count);

        var firstMonitorLayout = data.AppliedLayouts.Find(x => x.Device.MonitorNumber == FirstMonitorNumber);
        Assert.AreEqual(ColumnsType, firstMonitorLayout.AppliedLayout.Type);
        Assert.AreEqual(FirstMonitorNumber, firstMonitorLayout.Device.MonitorNumber);
    }

    [TestMethod("FancyZonesEditor.Basic.ApplyLayoutsOnEachMonitor")]
    [TestCategory("FancyZones Editor #10")]
    public void ApplyLayoutsOnEachMonitor()
    {
        EditorUiTestHelper.EnsureEditorReady(this, Session);

        EditorUiTestHelper.Step(this, "Applying template layout 'Columns' on Monitor 1");
        var firstMonitorLayoutCard = Session.Find<Element>(EditorUiTestHelper.TemplateLayoutName.Columns);
        firstMonitorLayoutCard.Click();
        firstMonitorLayoutCard = Session.Find<Element>(EditorUiTestHelper.TemplateLayoutName.Columns);
        Assert.IsTrue(firstMonitorLayoutCard.Selected);

        EditorUiTestHelper.SelectMonitor(this, Session, "Monitor 2");
        EditorUiTestHelper.Step(this, $"Applying custom layout '{CustomLayoutName}' on Monitor 2");
        var secondMonitorLayoutCard = Session.Find<Element>(CustomLayoutName);
        secondMonitorLayoutCard.Click();
        secondMonitorLayoutCard = Session.Find<Element>(CustomLayoutName);
        Assert.IsTrue(secondMonitorLayoutCard.Selected);

        EditorUiTestHelper.SelectMonitor(this, Session, "Monitor 1");
        firstMonitorLayoutCard = Session.Find<Element>(EditorUiTestHelper.TemplateLayoutName.Columns);
        Assert.IsTrue(firstMonitorLayoutCard.Selected);

        var data = EditorUiTestHelper.ReadAppliedLayouts();
        Assert.AreEqual(2, data.AppliedLayouts.Count);

        var firstMonitorLayout = data.AppliedLayouts.Find(x => x.Device.MonitorNumber == FirstMonitorNumber);
        var secondMonitorLayout = data.AppliedLayouts.Find(x => x.Device.MonitorNumber == SecondMonitorNumber);

        Assert.AreEqual(ColumnsType, firstMonitorLayout.AppliedLayout.Type);
        Assert.AreEqual(CustomLayoutUuid, secondMonitorLayout.AppliedLayout.Uuid);
    }

    [TestMethod("FancyZonesEditor.Basic.ApplyTemplateWithDifferentParametersOnEachMonitor")]
    [TestCategory("FancyZones Editor #10")]
    public void ApplyTemplateWithDifferentParametersOnEachMonitor()
    {
        EditorUiTestHelper.EnsureEditorReady(this, Session);

        EditorUiTestHelper.Step(this, "Applying template layout 'Columns' on Monitor 1");
        Session.Find<Element>(EditorUiTestHelper.TemplateLayoutName.Columns).Click();

        EditorUiTestHelper.OpenEditLayoutDialog(this, Session, EditorUiTestHelper.TemplateLayoutName.Columns);
        var firstMonitorSlider = Session.Find<Element>(By.AccessibilityId(EditorUiTestHelper.AccessibilityId.TemplateZoneSlider));
        firstMonitorSlider.Focus();
        EditorUiTestHelper.Step(this, "Increasing Monitor 1 zone count twice");
        KeyboardHelper.SendKeys(Key.Right);
        KeyboardHelper.SendKeys(Key.Right);
        var expectedFirstLayoutZoneCount = ReadZoneCount();
        EditorUiTestHelper.Step(this, $"Setting Monitor 1 zone count to {expectedFirstLayoutZoneCount}");
        Session.Find<Button>(EditorUiTestHelper.ElementName.Save).Click();

        EditorUiTestHelper.SelectMonitor(this, Session, "Monitor 2");
        EditorUiTestHelper.Step(this, "Applying template layout 'Columns' on Monitor 2");
        Session.Find<Element>(EditorUiTestHelper.TemplateLayoutName.Columns).Click();

        EditorUiTestHelper.OpenEditLayoutDialog(this, Session, EditorUiTestHelper.TemplateLayoutName.Columns);
    var secondMonitorSlider = Session.Find<Element>(By.AccessibilityId(EditorUiTestHelper.AccessibilityId.TemplateZoneSlider));
    secondMonitorSlider.Focus();
    EditorUiTestHelper.Step(this, "Decreasing Monitor 2 zone count once");
    KeyboardHelper.SendKeys(Key.Left);
    var expectedSecondLayoutZoneCount = ReadZoneCount();
        EditorUiTestHelper.Step(this, $"Setting Monitor 2 zone count to {expectedSecondLayoutZoneCount}");
        Session.Find<Button>(EditorUiTestHelper.ElementName.Save).Click();

        EditorUiTestHelper.SelectMonitor(this, Session, "Monitor 1");
        EditorUiTestHelper.OpenEditLayoutDialog(this, Session, EditorUiTestHelper.TemplateLayoutName.Columns);
    Assert.AreEqual(expectedFirstLayoutZoneCount, ReadZoneCount());
        Session.Find<Button>(EditorUiTestHelper.ElementName.Cancel).Click();

        var data = EditorUiTestHelper.ReadAppliedLayouts();
        Assert.AreEqual(2, data.AppliedLayouts.Count);

        var firstMonitorLayout = data.AppliedLayouts.Find(x => x.Device.MonitorNumber == FirstMonitorNumber);
        var secondMonitorLayout = data.AppliedLayouts.Find(x => x.Device.MonitorNumber == SecondMonitorNumber);

        Assert.AreEqual(ColumnsType, firstMonitorLayout.AppliedLayout.Type);
        Assert.AreEqual(expectedFirstLayoutZoneCount, firstMonitorLayout.AppliedLayout.ZoneCount);
        Assert.AreEqual(ColumnsType, secondMonitorLayout.AppliedLayout.Type);
        Assert.AreEqual(expectedSecondLayoutZoneCount, secondMonitorLayout.AppliedLayout.ZoneCount);
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
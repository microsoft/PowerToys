// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using FancyZonesEditor.UITests.Utils;
using FancyZonesEditorCommon.Data;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FancyZonesEditor.UITests;

[TestClass]
public class TemplateLayoutsTests : FancyZonesEditorTestBase
{
    private static readonly (string Type, string Name, int ZoneCount, bool ShowSpacing, int Spacing, int SensitivityRadius)[] TemplateLayouts =
    [
        ("blank", EditorUiTestHelper.TemplateLayoutName.Blank, 0, false, 0, 0),
        ("focus", EditorUiTestHelper.TemplateLayoutName.Focus, 10, false, 0, 0),
        ("rows", EditorUiTestHelper.TemplateLayoutName.Rows, 2, true, 10, 10),
        ("columns", EditorUiTestHelper.TemplateLayoutName.Columns, 2, true, 20, 20),
        ("grid", EditorUiTestHelper.TemplateLayoutName.Grid, 4, false, 10, 30),
        ("priority-grid", EditorUiTestHelper.TemplateLayoutName.PriorityGrid, 3, true, 1, 40),
    ];

    public TemplateLayoutsTests()
    {
        EditorTestData.WriteForTemplateLayoutsTests(Files);
    }

    [TestMethod("FancyZonesEditor.Basic.ZoneNumber_Cancel")]
    [TestCategory("FancyZones Editor #6")]
    public void ZoneNumber_Cancel()
    {
        const string type = "rows";

        EditorUiTestHelper.EnsureEditorReady(this, Session);
        var expected = GetTemplate(type).ZoneCount;

        EditorUiTestHelper.OpenEditLayoutDialog(this, Session, EditorUiTestHelper.TemplateLayoutName.Rows);
        _ = EditorUiTestHelper.NudgeSliderAndRead(this, Session, EditorUiTestHelper.AccessibilityId.TemplateZoneSlider, Key.Left, "decreasing template zone count");
        EditorUiTestHelper.Step(this, "Cancelling template zone-count changes");
        Session.Find<Button>(EditorUiTestHelper.ElementName.Cancel).Click();

        Assert.AreEqual(expected, GetTemplateFromFile(type).ZoneCount);
    }

    [TestMethod("FancyZonesEditor.Basic.HighlightDistance_Initialize")]
    [TestCategory("FancyZones Editor #6")]
    public void HighlightDistance_Initialize()
    {
        EditorUiTestHelper.EnsureEditorReady(this, Session);

        foreach (var layout in TemplateLayouts.Where(layout => layout.Type != "blank"))
        {
            EditorUiTestHelper.OpenEditLayoutDialog(this, Session, layout.Name);
            var slider = Session.Find<Element>(By.AccessibilityId(EditorUiTestHelper.AccessibilityId.SensitivitySlider));
            var actual = EditorUiTestHelper.ReadSliderValueAsInt(this, slider, EditorUiTestHelper.AccessibilityId.SensitivitySlider);
            Assert.AreEqual(layout.SensitivityRadius, actual);
            Session.Find<Button>(EditorUiTestHelper.ElementName.Cancel).Click();
        }
    }

    [TestMethod("FancyZonesEditor.Basic.HighlightDistance_Save")]
    [TestCategory("FancyZones Editor #6")]
    public void HighlightDistance_Save()
    {
        const string type = "focus";

        EditorUiTestHelper.EnsureEditorReady(this, Session);
        EditorUiTestHelper.OpenEditLayoutDialog(this, Session, EditorUiTestHelper.TemplateLayoutName.Focus);

        var expected = EditorUiTestHelper.NudgeSliderAndRead(
            this,
            Session,
            EditorUiTestHelper.AccessibilityId.SensitivitySlider,
            Key.Right,
            "increasing focus sensitivity");

        EditorUiTestHelper.Step(this, "Saving highlight-distance changes");
        Session.Find<Button>(EditorUiTestHelper.ElementName.Save).Click();

        Assert.AreEqual(expected, GetTemplateFromFile(type).SensitivityRadius);
    }

    [TestMethod("FancyZonesEditor.Basic.HighlightDistance_Cancel")]
    [TestCategory("FancyZones Editor #6")]
    public void HighlightDistance_Cancel()
    {
        const string type = "focus";

        EditorUiTestHelper.EnsureEditorReady(this, Session);
        var expected = GetTemplate(type).SensitivityRadius;

        EditorUiTestHelper.OpenEditLayoutDialog(this, Session, EditorUiTestHelper.TemplateLayoutName.Focus);
        _ = EditorUiTestHelper.NudgeSliderAndRead(this, Session, EditorUiTestHelper.AccessibilityId.SensitivitySlider, Key.Right, "attempting to change focus sensitivity");
        EditorUiTestHelper.Step(this, "Cancelling highlight-distance changes");
        Session.Find<Button>(EditorUiTestHelper.ElementName.Cancel).Click();

        Assert.AreEqual(expected, GetTemplateFromFile(type).SensitivityRadius);
    }

    [TestMethod("FancyZonesEditor.Basic.SpaceAroundZones_Initialize")]
    [TestCategory("FancyZones Editor #6")]
    public void SpaceAroundZones_Initialize()
    {
        EditorUiTestHelper.EnsureEditorReady(this, Session);

        foreach (var layout in TemplateLayouts.Where(layout => layout.Type is not "blank" and not "focus"))
        {
            EditorUiTestHelper.OpenEditLayoutDialog(this, Session, layout.Name);

            var toggle = Session.Find<Element>(By.AccessibilityId(EditorUiTestHelper.AccessibilityId.SpacingToggle));
            var spacingEnabled = string.Equals(toggle.GetProperty("ToggleState"), "On", StringComparison.OrdinalIgnoreCase);
            Assert.AreEqual(layout.ShowSpacing, spacingEnabled);

            var slider = Session.Find<Element>(By.AccessibilityId(EditorUiTestHelper.AccessibilityId.SpacingSlider));
            Assert.AreEqual(layout.ShowSpacing, slider.IsEnabled);
            var actualSpacing = EditorUiTestHelper.ReadSliderValueAsInt(this, slider, EditorUiTestHelper.AccessibilityId.SpacingSlider);
            Assert.AreEqual(layout.Spacing, actualSpacing);

            Session.Find<Button>(EditorUiTestHelper.ElementName.Cancel).Click();
        }
    }

    [TestMethod("FancyZonesEditor.Basic.SpaceAroundZones_Slider_Save")]
    [TestCategory("FancyZones Editor #6")]
    public void SpaceAroundZones_Slider_Save()
    {
        const string type = "priority-grid";

        EditorUiTestHelper.EnsureEditorReady(this, Session);
        EditorUiTestHelper.OpenEditLayoutDialog(this, Session, EditorUiTestHelper.TemplateLayoutName.PriorityGrid);

        var expected = EditorUiTestHelper.NudgeSliderAndRead(
            this,
            Session,
            EditorUiTestHelper.AccessibilityId.SpacingSlider,
            Key.Right,
            "increasing spacing for priority grid");

        EditorUiTestHelper.Step(this, "Saving spacing changes");
        Session.Find<Button>(EditorUiTestHelper.ElementName.Save).Click();

        Assert.AreEqual(expected, GetTemplateFromFile(type).Spacing);
    }

    [TestMethod("FancyZonesEditor.Basic.SpaceAroundZones_Slider_Cancel")]
    [TestCategory("FancyZones Editor #6")]
    public void SpaceAroundZones_Slider_Cancel()
    {
        const string type = "priority-grid";

        EditorUiTestHelper.EnsureEditorReady(this, Session);
        var expected = GetTemplate(type).Spacing;

        EditorUiTestHelper.OpenEditLayoutDialog(this, Session, EditorUiTestHelper.TemplateLayoutName.PriorityGrid);
        _ = EditorUiTestHelper.NudgeSliderAndRead(this, Session, EditorUiTestHelper.AccessibilityId.SpacingSlider, Key.Right, "attempting to change spacing");
        EditorUiTestHelper.Step(this, "Cancelling spacing changes");
        Session.Find<Button>(EditorUiTestHelper.ElementName.Cancel).Click();

        Assert.AreEqual(expected, GetTemplateFromFile(type).Spacing);
    }

    [TestMethod("FancyZonesEditor.Basic.SpaceAroundZones_Toggle_Save")]
    [TestCategory("FancyZones Editor #6")]
    public void SpaceAroundZones_Toggle_Save()
    {
        const string type = "priority-grid";

        EditorUiTestHelper.EnsureEditorReady(this, Session);
        var expected = !GetTemplate(type).ShowSpacing;

        EditorUiTestHelper.OpenEditLayoutDialog(this, Session, EditorUiTestHelper.TemplateLayoutName.PriorityGrid);
        var stateAfterToggle = EditorUiTestHelper.SetToggleState(this, Session, EditorUiTestHelper.AccessibilityId.SpacingToggle, expected);
        Assert.AreEqual(expected, stateAfterToggle);

        var spacingSlider = Session.Find<Element>(By.AccessibilityId(EditorUiTestHelper.AccessibilityId.SpacingSlider));
        Assert.AreEqual(expected, spacingSlider.IsEnabled);

        EditorUiTestHelper.Step(this, "Saving spacing-toggle changes");
        Session.Find<Button>(EditorUiTestHelper.ElementName.Save).Click();

        Assert.AreEqual(expected, GetTemplateFromFile(type).ShowSpacing);
    }

    [TestMethod("FancyZonesEditor.Basic.SpaceAroundZones_Toggle_Cancel")]
    [TestCategory("FancyZones Editor #6")]
    public void SpaceAroundZones_Toggle_Cancel()
    {
        const string type = "priority-grid";

        EditorUiTestHelper.EnsureEditorReady(this, Session);
        var expected = GetTemplate(type).ShowSpacing;

        EditorUiTestHelper.OpenEditLayoutDialog(this, Session, EditorUiTestHelper.TemplateLayoutName.PriorityGrid);
        var stateAfterToggle = EditorUiTestHelper.SetToggleState(this, Session, EditorUiTestHelper.AccessibilityId.SpacingToggle, !expected);
        Assert.AreNotEqual(expected, stateAfterToggle);

        var spacingSlider = Session.Find<Element>(By.AccessibilityId(EditorUiTestHelper.AccessibilityId.SpacingSlider));
        Assert.AreNotEqual(expected, spacingSlider.IsEnabled);

        EditorUiTestHelper.Step(this, "Cancelling spacing-toggle changes");
        Session.Find<Button>(EditorUiTestHelper.ElementName.Cancel).Click();

        Assert.AreEqual(expected, GetTemplateFromFile(type).ShowSpacing);
    }

    private static (string Type, string Name, int ZoneCount, bool ShowSpacing, int Spacing, int SensitivityRadius) GetTemplate(string type)
    {
        return TemplateLayouts.First(layout => layout.Type == type);
    }

    private static LayoutTemplates.TemplateLayoutWrapper GetTemplateFromFile(string type)
    {
        return EditorUiTestHelper.ReadTemplateLayouts().LayoutTemplates.First(layout => layout.Type == type);
    }
}

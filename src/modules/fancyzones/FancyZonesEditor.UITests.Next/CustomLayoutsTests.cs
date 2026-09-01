// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using FancyZonesEditor.UITests.Utils;
using FancyZonesEditorCommon.Data;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FancyZonesEditor.UITests;

[TestClass]
public class CustomLayoutsTests : FancyZonesEditorTestBase
{
    private const string FirstLayoutUuid = "{0D6D2F58-9184-4804-81E4-4E4CC3476DC1}";
    private const string SecondLayoutUuid = "{E7807D0D-6223-4883-B15B-1F3883944C09}";
    private const string ThirdLayoutUuid = "{F1A94F38-82B6-4876-A653-70D0E882DE2A}";
    private const string FirstLayoutName = "Grid custom layout";

    public CustomLayoutsTests()
    {
        EditorTestData.WriteForCustomLayoutsTests(Files);
    }

    [TestMethod]
    public void Name_Initialize()
    {
        EditorUiTestHelper.EnsureEditorReady(this, Session);

        foreach (var layout in EditorUiTestHelper.ReadCustomLayouts().CustomLayouts)
        {
            Assert.IsNotNull(Session.Find<Element>(layout.Name));
        }
    }

    [TestMethod]
    public void Rename_Save()
    {
        const string newName = "New layout name";

        EditorUiTestHelper.EnsureEditorReady(this, Session);
        EditorUiTestHelper.OpenEditLayoutDialog(this, Session, FirstLayoutName);

        EditorUiTestHelper.Step(this, $"Renaming '{FirstLayoutName}' to '{newName}'");
        EditorUiTestHelper.FindEditLayoutNameTextBox(this, Session).SetText(newName);
        EditorUiTestHelper.Step(this, "Saving renamed layout");
        Session.Find<Button>(EditorUiTestHelper.ElementName.Save).Click();

        var data = EditorUiTestHelper.ReadCustomLayouts();
        Assert.AreEqual(3, data.CustomLayouts.Count);
        Assert.IsFalse(data.CustomLayouts.Exists(layout => layout.Name == FirstLayoutName));
        Assert.IsTrue(data.CustomLayouts.Exists(layout => layout.Name == newName));
        Assert.AreEqual(newName, data.CustomLayouts.First(layout => layout.Uuid == FirstLayoutUuid).Name);
    }

    [TestMethod]
    public void Rename_Cancel()
    {
        const string newName = "New layout name";

        EditorUiTestHelper.EnsureEditorReady(this, Session);
        EditorUiTestHelper.OpenEditLayoutDialog(this, Session, FirstLayoutName);

        EditorUiTestHelper.Step(this, $"Attempting to rename '{FirstLayoutName}' to '{newName}' and canceling");
        EditorUiTestHelper.FindEditLayoutNameTextBox(this, Session).SetText(newName);
        Session.Find<Button>(EditorUiTestHelper.ElementName.Cancel).Click();

        var data = EditorUiTestHelper.ReadCustomLayouts();
        Assert.AreEqual(3, data.CustomLayouts.Count);
        Assert.IsTrue(data.CustomLayouts.Exists(layout => layout.Name == FirstLayoutName));
        Assert.IsFalse(data.CustomLayouts.Exists(layout => layout.Name == newName));
        Assert.AreEqual(FirstLayoutName, data.CustomLayouts.First(layout => layout.Uuid == FirstLayoutUuid).Name);
    }

    [TestMethod]
    public void HighlightDistance_Initialize()
    {
        EditorUiTestHelper.EnsureEditorReady(this, Session);

        foreach (var layout in EditorUiTestHelper.ReadCustomLayouts().CustomLayouts)
        {
            EditorUiTestHelper.OpenEditLayoutDialog(this, Session, layout.Name);

            var expected = GetSensitivityRadius(layout);
            var slider = Session.Find<Element>(By.AccessibilityId(EditorUiTestHelper.AccessibilityId.SensitivitySlider));
            var actual = EditorUiTestHelper.ReadSliderValueAsInt(this, slider, EditorUiTestHelper.AccessibilityId.SensitivitySlider);
            Assert.AreEqual(expected, actual);

            Session.Find<Button>(EditorUiTestHelper.ElementName.Cancel).Click();
        }
    }

    [TestMethod]
    public void HighlightDistance_Save()
    {
        EditorUiTestHelper.EnsureEditorReady(this, Session);
        var baseline = EditorUiTestHelper.ReadCustomLayouts().CustomLayouts.First(layout => layout.Uuid == FirstLayoutUuid);

        EditorUiTestHelper.OpenEditLayoutDialog(this, Session, baseline.Name);
        var expected = EditorUiTestHelper.NudgeSliderAndRead(
            this,
            Session,
            EditorUiTestHelper.AccessibilityId.SensitivitySlider,
            Key.Right,
            "increasing custom-layout sensitivity");

        EditorUiTestHelper.Step(this, "Saving custom highlight-distance changes");
        Session.Find<Button>(EditorUiTestHelper.ElementName.Save).Click();

        var actualLayout = EditorUiTestHelper.ReadCustomLayouts().CustomLayouts.First(layout => layout.Uuid == FirstLayoutUuid);
        Assert.AreEqual(expected, GetSensitivityRadius(actualLayout));
    }

    [TestMethod]
    public void HighlightDistance_Cancel()
    {
        EditorUiTestHelper.EnsureEditorReady(this, Session);
        var baseline = EditorUiTestHelper.ReadCustomLayouts().CustomLayouts.First(layout => layout.Uuid == FirstLayoutUuid);
        var expected = GetSensitivityRadius(baseline);

        EditorUiTestHelper.OpenEditLayoutDialog(this, Session, baseline.Name);
        _ = EditorUiTestHelper.NudgeSliderAndRead(this, Session, EditorUiTestHelper.AccessibilityId.SensitivitySlider, Key.Right, "attempting to change custom sensitivity");
        Session.Find<Button>(EditorUiTestHelper.ElementName.Cancel).Click();

        var actualLayout = EditorUiTestHelper.ReadCustomLayouts().CustomLayouts.First(layout => layout.Uuid == FirstLayoutUuid);
        Assert.AreEqual(expected, GetSensitivityRadius(actualLayout));
    }

    [TestMethod]
    public void SpaceAroundZones_Initialize()
    {
        EditorUiTestHelper.EnsureEditorReady(this, Session);

        foreach (var layout in EditorUiTestHelper.ReadCustomLayouts().CustomLayouts.Where(layout => layout.Type == "grid"))
        {
            EditorUiTestHelper.OpenEditLayoutDialog(this, Session, layout.Name);

            var expectedGrid = new CustomLayouts().GridFromJsonElement(layout.Info.GetRawText());
            var toggle = Session.Find<Element>(By.AccessibilityId(EditorUiTestHelper.AccessibilityId.SpacingToggle));
            var toggleState = string.Equals(toggle.GetProperty("ToggleState"), "On", StringComparison.OrdinalIgnoreCase);
            Assert.AreEqual(expectedGrid.ShowSpacing, toggleState);

            var slider = Session.Find<Element>(By.AccessibilityId(EditorUiTestHelper.AccessibilityId.SpacingSlider));
            Assert.AreEqual(expectedGrid.ShowSpacing, slider.IsEnabled);
            var actualSpacing = EditorUiTestHelper.ReadSliderValueAsInt(this, slider, EditorUiTestHelper.AccessibilityId.SpacingSlider);
            Assert.AreEqual(expectedGrid.Spacing, actualSpacing);

            Session.Find<Button>(EditorUiTestHelper.ElementName.Cancel).Click();
        }
    }

    [TestMethod]
    public void SpaceAroundZones_Slider_Save()
    {
        EditorUiTestHelper.EnsureEditorReady(this, Session);
        var baseline = EditorUiTestHelper.ReadCustomLayouts().CustomLayouts.First(layout => layout.Uuid == ThirdLayoutUuid);

        EditorUiTestHelper.OpenEditLayoutDialog(this, Session, baseline.Name);
        Assert.IsTrue(EditorUiTestHelper.SetToggleState(this, Session, EditorUiTestHelper.AccessibilityId.SpacingToggle, expectedState: true));

        var expected = EditorUiTestHelper.NudgeSliderAndRead(
            this,
            Session,
            EditorUiTestHelper.AccessibilityId.SpacingSlider,
            Key.Right,
            "increasing custom spacing");

        EditorUiTestHelper.Step(this, "Saving custom spacing changes");
        Session.Find<Button>(EditorUiTestHelper.ElementName.Save).Click();

        var actualLayout = EditorUiTestHelper.ReadCustomLayouts().CustomLayouts.First(layout => layout.Uuid == ThirdLayoutUuid);
        var actual = new CustomLayouts().GridFromJsonElement(actualLayout.Info.GetRawText()).Spacing;
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void SpaceAroundZones_Slider_Cancel()
    {
        EditorUiTestHelper.EnsureEditorReady(this, Session);
        var baseline = EditorUiTestHelper.ReadCustomLayouts().CustomLayouts.First(layout => layout.Uuid == ThirdLayoutUuid);
        var expected = new CustomLayouts().GridFromJsonElement(baseline.Info.GetRawText()).Spacing;

        EditorUiTestHelper.OpenEditLayoutDialog(this, Session, baseline.Name);
        _ = EditorUiTestHelper.NudgeSliderAndRead(this, Session, EditorUiTestHelper.AccessibilityId.SpacingSlider, Key.Right, "attempting to change custom spacing");
        Session.Find<Button>(EditorUiTestHelper.ElementName.Cancel).Click();

        var actualLayout = EditorUiTestHelper.ReadCustomLayouts().CustomLayouts.First(layout => layout.Uuid == ThirdLayoutUuid);
        var actual = new CustomLayouts().GridFromJsonElement(actualLayout.Info.GetRawText()).Spacing;
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void SpaceAroundZones_Toggle_Save()
    {
        EditorUiTestHelper.EnsureEditorReady(this, Session);
        var baseline = EditorUiTestHelper.ReadCustomLayouts().CustomLayouts.First(layout => layout.Uuid == FirstLayoutUuid);
        var initial = new CustomLayouts().GridFromJsonElement(baseline.Info.GetRawText()).ShowSpacing;
        var expected = !initial;

        EditorUiTestHelper.OpenEditLayoutDialog(this, Session, baseline.Name);
        var toggleState = EditorUiTestHelper.SetToggleState(this, Session, EditorUiTestHelper.AccessibilityId.SpacingToggle, expected);
        Assert.AreEqual(expected, toggleState);

        var spacingSlider = Session.Find<Element>(By.AccessibilityId(EditorUiTestHelper.AccessibilityId.SpacingSlider));
        Assert.AreEqual(expected, spacingSlider.IsEnabled);

        EditorUiTestHelper.Step(this, "Saving custom spacing-toggle changes");
        Session.Find<Button>(EditorUiTestHelper.ElementName.Save).Click();

        var actualLayout = EditorUiTestHelper.ReadCustomLayouts().CustomLayouts.First(layout => layout.Uuid == FirstLayoutUuid);
        var actual = new CustomLayouts().GridFromJsonElement(actualLayout.Info.GetRawText()).ShowSpacing;
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void SpaceAroundZones_Toggle_Cancel()
    {
        EditorUiTestHelper.EnsureEditorReady(this, Session);
        var baseline = EditorUiTestHelper.ReadCustomLayouts().CustomLayouts.First(layout => layout.Uuid == FirstLayoutUuid);
        var expected = new CustomLayouts().GridFromJsonElement(baseline.Info.GetRawText()).ShowSpacing;

        EditorUiTestHelper.OpenEditLayoutDialog(this, Session, baseline.Name);
        var toggleState = EditorUiTestHelper.SetToggleState(this, Session, EditorUiTestHelper.AccessibilityId.SpacingToggle, !expected);
        Assert.AreNotEqual(expected, toggleState);

        var spacingSlider = Session.Find<Element>(By.AccessibilityId(EditorUiTestHelper.AccessibilityId.SpacingSlider));
        Assert.AreNotEqual(expected, spacingSlider.IsEnabled);

        Session.Find<Button>(EditorUiTestHelper.ElementName.Cancel).Click();

        var actualLayout = EditorUiTestHelper.ReadCustomLayouts().CustomLayouts.First(layout => layout.Uuid == FirstLayoutUuid);
        var actual = new CustomLayouts().GridFromJsonElement(actualLayout.Info.GetRawText()).ShowSpacing;
        Assert.AreEqual(expected, actual);
    }

    private static int GetSensitivityRadius(CustomLayouts.CustomLayoutWrapper layout)
    {
        return layout.Type == "canvas"
            ? new CustomLayouts().CanvasFromJsonElement(layout.Info.GetRawText()).SensitivityRadius
            : new CustomLayouts().GridFromJsonElement(layout.Info.GetRawText()).SensitivityRadius;
    }
}

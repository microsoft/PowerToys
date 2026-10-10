// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using FancyZonesEditor.UITests.Utils;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FancyZonesEditor.UITests;

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

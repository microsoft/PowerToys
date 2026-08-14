// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using FancyZonesEditor.UITests.Utils;
using FancyZonesEditorCommon.Data;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FancyZonesEditor.UITests;

[TestClass]
public class CreateLayoutTests : FancyZonesEditorTestBase
{
    public CreateLayoutTests()
    {
        EditorTestData.WriteForCreateLayoutTests(Files);
    }

    [TestMethod]
    public void CreateWithDefaultName()
    {
        const string name = "Custom layout 1";
        EditorUiTestHelper.EnsureEditorReady(this, Session);

        EditorUiTestHelper.Step(this, "Opening the new layout dialog");
        Session.Find<Element>(By.AccessibilityId(EditorUiTestHelper.AccessibilityId.NewLayoutButton)).Click();
        EditorUiTestHelper.Step(this, "Confirming layout type selection");
        Session.Find<Element>(By.AccessibilityId(EditorUiTestHelper.AccessibilityId.PrimaryButton)).Click();
        EditorUiTestHelper.Step(this, "Saving the new layout");
        Session.Find<Button>(EditorUiTestHelper.ElementName.Save).Click();

        Assert.IsNotNull(Session.Find<Element>(name));

        var data = EditorUiTestHelper.ReadCustomLayouts();
        Assert.AreEqual(1, data.CustomLayouts.Count);
        Assert.IsTrue(data.CustomLayouts.Exists(x => x.Name == name));
    }

    [TestMethod("FancyZonesEditor.Basic.CreateWithCustomName")]
    [TestCategory("FancyZones Editor #3")]
    public void CreateWithCustomName()
    {
        const string name = "Layout Name";
        EditorUiTestHelper.EnsureEditorReady(this, Session);

        EditorUiTestHelper.Step(this, "Opening the new layout dialog");
        Session.Find<Element>(By.AccessibilityId(EditorUiTestHelper.AccessibilityId.NewLayoutButton)).Click();

        EditorUiTestHelper.Step(this, $"Setting custom layout name to '{name}'");
        var input = Session.Find<TextBox>(By.Name("Name"));
        Assert.IsNotNull(input);
        input.SetText(name);

        EditorUiTestHelper.Step(this, "Confirming layout type selection");
        Session.Find<Element>(By.AccessibilityId(EditorUiTestHelper.AccessibilityId.PrimaryButton)).Click();
        EditorUiTestHelper.Step(this, "Saving the new layout");
        Session.Find<Button>(EditorUiTestHelper.ElementName.Save).Click();

        Assert.IsNotNull(Session.Find<Element>(name));

        var data = EditorUiTestHelper.ReadCustomLayouts();
        Assert.AreEqual(1, data.CustomLayouts.Count);
        Assert.IsTrue(data.CustomLayouts.Exists(x => x.Name == name));
    }

    [TestMethod("FancyZonesEditor.Basic.CreateGrid")]
    [TestCategory("FancyZones Editor #3")]
    public void CreateGrid()
    {
        var type = CustomLayout.Grid;
        EditorUiTestHelper.EnsureEditorReady(this, Session);

        EditorUiTestHelper.Step(this, "Opening the new layout dialog");
        Session.Find<Element>(By.AccessibilityId(EditorUiTestHelper.AccessibilityId.NewLayoutButton)).Click();
        EditorUiTestHelper.Step(this, "Selecting Grid layout type");
        Session.Find<Element>(By.AccessibilityId(EditorUiTestHelper.AccessibilityId.GridRadioButton)).Click();
        EditorUiTestHelper.Step(this, "Confirming layout type selection");
        Session.Find<Element>(By.AccessibilityId(EditorUiTestHelper.AccessibilityId.PrimaryButton)).Click();
        EditorUiTestHelper.Step(this, "Saving the new layout");
        Session.Find<Button>(EditorUiTestHelper.ElementName.Save).Click();

        var data = EditorUiTestHelper.ReadCustomLayouts();
        Assert.AreEqual(1, data.CustomLayouts.Count);
        Assert.IsTrue(data.CustomLayouts.Exists(x => x.Type == type.TypeToString()));
    }

    [TestMethod("FancyZonesEditor.Basic.CreateCanvas")]
    [TestCategory("FancyZones Editor #3")]
    public void CreateCanvas()
    {
        var type = CustomLayout.Canvas;
        EditorUiTestHelper.EnsureEditorReady(this, Session);

        EditorUiTestHelper.Step(this, "Opening the new layout dialog");
        Session.Find<Element>(By.AccessibilityId(EditorUiTestHelper.AccessibilityId.NewLayoutButton)).Click();
        EditorUiTestHelper.Step(this, "Selecting Canvas layout type");
        Session.Find<Element>(By.AccessibilityId(EditorUiTestHelper.AccessibilityId.CanvasRadioButton)).Click();
        EditorUiTestHelper.Step(this, "Confirming layout type selection");
        Session.Find<Element>(By.AccessibilityId(EditorUiTestHelper.AccessibilityId.PrimaryButton)).Click();
        EditorUiTestHelper.Step(this, "Saving the new layout");
        Session.Find<Button>(EditorUiTestHelper.ElementName.Save).Click();

        var data = EditorUiTestHelper.ReadCustomLayouts();
        Assert.AreEqual(1, data.CustomLayouts.Count);
        Assert.IsTrue(data.CustomLayouts.Exists(x => x.Type == type.TypeToString()));
    }

    [TestMethod("FancyZonesEditor.Basic.CancelGridCreation")]
    [TestCategory("FancyZones Editor #3")]
    public void CancelGridCreation()
    {
        EditorUiTestHelper.EnsureEditorReady(this, Session);

        EditorUiTestHelper.Step(this, "Opening the new layout dialog");
        Session.Find<Element>(By.AccessibilityId(EditorUiTestHelper.AccessibilityId.NewLayoutButton)).Click();
        EditorUiTestHelper.Step(this, "Selecting Grid layout type");
        Session.Find<Element>(By.AccessibilityId(EditorUiTestHelper.AccessibilityId.GridRadioButton)).Click();
        EditorUiTestHelper.Step(this, "Confirming layout type selection");
        Session.Find<Element>(By.AccessibilityId(EditorUiTestHelper.AccessibilityId.PrimaryButton)).Click();
        EditorUiTestHelper.Step(this, "Cancelling layout creation");
        Session.Find<Button>(EditorUiTestHelper.ElementName.Cancel).Click();

        var data = EditorUiTestHelper.ReadCustomLayouts();
        Assert.AreEqual(0, data.CustomLayouts.Count);
    }

    [TestMethod("FancyZonesEditor.Basic.CancelCanvasCreation")]
    [TestCategory("FancyZones Editor #3")]
    public void CancelCanvasCreation()
    {
        EditorUiTestHelper.EnsureEditorReady(this, Session);

        EditorUiTestHelper.Step(this, "Opening the new layout dialog");
        Session.Find<Element>(By.AccessibilityId(EditorUiTestHelper.AccessibilityId.NewLayoutButton)).Click();
        EditorUiTestHelper.Step(this, "Selecting Canvas layout type");
        Session.Find<Element>(By.AccessibilityId(EditorUiTestHelper.AccessibilityId.CanvasRadioButton)).Click();
        EditorUiTestHelper.Step(this, "Confirming layout type selection");
        Session.Find<Element>(By.AccessibilityId(EditorUiTestHelper.AccessibilityId.PrimaryButton)).Click();
        EditorUiTestHelper.Step(this, "Cancelling layout creation");
        Session.Find<Button>(EditorUiTestHelper.ElementName.Cancel).Click();

        var data = EditorUiTestHelper.ReadCustomLayouts();
        Assert.AreEqual(0, data.CustomLayouts.Count);
    }
}
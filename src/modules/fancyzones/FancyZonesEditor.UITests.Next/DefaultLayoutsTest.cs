// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using FancyZonesEditor.UITests.Utils;
using FancyZonesEditorCommon.Data;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FancyZonesEditor.UITests;

[TestClass]
public class DefaultLayoutsTest : FancyZonesEditorTestBase
{
    private const string VerticalConfiguration = "vertical";
    private const string HorizontalConfiguration = "horizontal";
    private const string FocusType = "focus";
    private const string GridType = "grid";
    private const string CustomType = "custom";
    private const string Layout0Uuid = "{0D6D2F58-9184-4804-81E4-4E4CC3476DC1}";

    private static readonly (string Name, string Type)[] TemplateLayouts =
    [
        (EditorUiTestHelper.TemplateLayoutName.Focus, FocusType),
        (EditorUiTestHelper.TemplateLayoutName.Rows, "rows"),
        (EditorUiTestHelper.TemplateLayoutName.Columns, "columns"),
        (EditorUiTestHelper.TemplateLayoutName.Grid, GridType),
        (EditorUiTestHelper.TemplateLayoutName.PriorityGrid, "priority-grid"),
    ];

    private static readonly (string Name, string Uuid)[] CustomLayouts =
    [
        ("Layout 0", "{0D6D2F58-9184-4804-81E4-4E4CC3476DC1}"),
        ("Layout 1", "{E7807D0D-6223-4883-B15B-1F3883944C09}"),
        ("Layout 2", "{F1A94F38-82B6-4876-A653-70D0E882DE2A}"),
        ("Layout 3", "{F5FDBC04-0760-4776-9F05-96AAC4AE613F}"),
    ];

    public DefaultLayoutsTest()
    {
        EditorTestData.WriteForDefaultLayoutsTests(Files);
    }

    [TestMethod("FancyZonesEditor.Basic.Default_Initialize")]
    [TestCategory("FancyZones Editor #12")]
    public void Initialize()
    {
        EditorUiTestHelper.EnsureEditorReady(this, Session);

        CheckTemplateLayouts(horizontalDefaultLayoutType: GridType, verticalDefaultLayoutType: null);
        CheckCustomLayouts(horizontalDefaultLayoutUuid: string.Empty, verticalDefaultLayoutUuid: Layout0Uuid);
        AssertDefaultLayoutsFile(horizontalType: GridType, verticalType: CustomType, verticalUuid: Layout0Uuid);
    }

    [TestMethod("FancyZonesEditor.Basic.Default_Assign_Cancel")]
    [TestCategory("FancyZones Editor #12")]
    public void Assign_Cancel()
    {
        EditorUiTestHelper.EnsureEditorReady(this, Session);

        EditorUiTestHelper.OpenEditLayoutDialog(this, Session, EditorUiTestHelper.TemplateLayoutName.Focus);
        Session.Find<Button>(By.AccessibilityId(EditorUiTestHelper.AccessibilityId.HorizontalDefaultButtonUnchecked)).Click();
        Session.Find<Button>(By.AccessibilityId(EditorUiTestHelper.AccessibilityId.VerticalDefaultButtonUnchecked)).Click();

        EditorUiTestHelper.Step(this, "Cancelling default-layout assignment changes");
        Session.Find<Button>(EditorUiTestHelper.ElementName.Cancel).Click();

        CheckTemplateLayouts(horizontalDefaultLayoutType: GridType, verticalDefaultLayoutType: null);
        CheckCustomLayouts(horizontalDefaultLayoutUuid: string.Empty, verticalDefaultLayoutUuid: Layout0Uuid);
        AssertDefaultLayoutsFile(horizontalType: GridType, verticalType: CustomType, verticalUuid: Layout0Uuid);
    }

    [TestMethod("FancyZonesEditor.Basic.Default_Assign_Save")]
    [TestCategory("FancyZones Editor #12")]
    public void Assign_Save()
    {
        EditorUiTestHelper.EnsureEditorReady(this, Session);

        EditorUiTestHelper.OpenEditLayoutDialog(this, Session, EditorUiTestHelper.TemplateLayoutName.Focus);
        Session.Find<Button>(By.AccessibilityId(EditorUiTestHelper.AccessibilityId.HorizontalDefaultButtonUnchecked)).Click();
        Session.Find<Button>(By.AccessibilityId(EditorUiTestHelper.AccessibilityId.VerticalDefaultButtonUnchecked)).Click();

        EditorUiTestHelper.Step(this, "Saving default-layout assignment changes");
        Session.Find<Button>(EditorUiTestHelper.ElementName.Save).Click();

        CheckTemplateLayouts(horizontalDefaultLayoutType: FocusType, verticalDefaultLayoutType: FocusType);
        CheckCustomLayouts(horizontalDefaultLayoutUuid: string.Empty, verticalDefaultLayoutUuid: string.Empty);
        AssertDefaultLayoutsFile(horizontalType: FocusType, verticalType: FocusType, verticalUuid: string.Empty);
    }

    private void CheckTemplateLayouts(string? horizontalDefaultLayoutType, string? verticalDefaultLayoutType)
    {
        foreach (var (name, type) in TemplateLayouts)
        {
            EditorUiTestHelper.OpenEditLayoutDialog(this, Session, name);

            var expectedHorizontalButtonId = type == horizontalDefaultLayoutType
                ? EditorUiTestHelper.AccessibilityId.HorizontalDefaultButtonChecked
                : EditorUiTestHelper.AccessibilityId.HorizontalDefaultButtonUnchecked;
            var expectedVerticalButtonId = type == verticalDefaultLayoutType
                ? EditorUiTestHelper.AccessibilityId.VerticalDefaultButtonChecked
                : EditorUiTestHelper.AccessibilityId.VerticalDefaultButtonUnchecked;

            Assert.IsNotNull(
                Session.Find<Button>(By.AccessibilityId(expectedHorizontalButtonId)),
                "Incorrect horizontal default layout set at " + name);
            Assert.IsNotNull(
                Session.Find<Button>(By.AccessibilityId(expectedVerticalButtonId)),
                "Incorrect vertical default layout set at " + name);

            Session.Find<Button>(EditorUiTestHelper.ElementName.Cancel).Click();
        }
    }

    private void CheckCustomLayouts(string horizontalDefaultLayoutUuid, string verticalDefaultLayoutUuid)
    {
        foreach (var (name, uuid) in CustomLayouts)
        {
            EditorUiTestHelper.OpenEditLayoutDialog(this, Session, name);

            var expectedHorizontalButtonId = uuid == horizontalDefaultLayoutUuid
                ? EditorUiTestHelper.AccessibilityId.HorizontalDefaultButtonChecked
                : EditorUiTestHelper.AccessibilityId.HorizontalDefaultButtonUnchecked;
            var expectedVerticalButtonId = uuid == verticalDefaultLayoutUuid
                ? EditorUiTestHelper.AccessibilityId.VerticalDefaultButtonChecked
                : EditorUiTestHelper.AccessibilityId.VerticalDefaultButtonUnchecked;

            Assert.IsNotNull(
                Session.Find<Button>(By.AccessibilityId(expectedHorizontalButtonId)),
                "Incorrect horizontal custom layout set at " + name);
            Assert.IsNotNull(
                Session.Find<Button>(By.AccessibilityId(expectedVerticalButtonId)),
                "Incorrect vertical custom layout set at " + name);

            Session.Find<Button>(EditorUiTestHelper.ElementName.Cancel).Click();
        }
    }

    private static void AssertDefaultLayoutsFile(string horizontalType, string verticalType, string verticalUuid)
    {
        var data = EditorUiTestHelper.ReadDefaultLayouts();
        Assert.AreEqual(2, data.DefaultLayouts.Count);

        var horizontal = data.DefaultLayouts.Find(x => x.MonitorConfiguration == HorizontalConfiguration);
        var vertical = data.DefaultLayouts.Find(x => x.MonitorConfiguration == VerticalConfiguration);

        Assert.AreEqual(horizontalType, horizontal.Layout.Type);
        Assert.AreEqual(verticalType, vertical.Layout.Type);

        if (string.IsNullOrEmpty(verticalUuid))
        {
            Assert.IsTrue(string.IsNullOrEmpty(vertical.Layout.Uuid));
        }
        else
        {
            Assert.AreEqual(verticalUuid, vertical.Layout.Uuid);
        }
    }
}
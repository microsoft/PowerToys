// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using FancyZonesEditor.UITests.Utils;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FancyZonesEditor.UITests;

[TestClass]
public class RunFancyZonesEditorTest : FancyZonesEditorTestBase
{
    public RunFancyZonesEditorTest()
    {
        EditorTestData.WriteForRunFancyZonesEditorTests(Files);
    }

    [TestMethod]
    public void OpenNewLayoutDialog()
    {
        EditorUiTestHelper.EnsureEditorReady(this, Session);

        EditorUiTestHelper.Step(this, "Opening the new layout dialog");
        Session.Find<Button>(By.AccessibilityId(EditorUiTestHelper.AccessibilityId.NewLayoutButton)).Click();

        Assert.IsNotNull(Session.Find<Element>("Choose layout type"));
    }

    [TestMethod]
    public void OpenEditLayoutDialog()
    {
        EditorUiTestHelper.EnsureEditorReady(this, Session);

        EditorUiTestHelper.Step(this, $"Opening edit dialog for '{EditorUiTestHelper.TemplateLayoutName.Grid}' from the layout card");
        Session.Find<Button>(EditorUiTestHelper.TemplateLayoutName.Grid).Click();

        Assert.IsNotNull(Session.Find<Element>(By.AccessibilityId(EditorUiTestHelper.AccessibilityId.DialogTitle)));
        Assert.IsNotNull(Session.Find<Element>($"Edit '{EditorUiTestHelper.TemplateLayoutName.Grid}'"));
    }

    [TestMethod]
    public void OpenEditLayoutDialog_ByContextMenu_TemplateLayout()
    {
        EditorUiTestHelper.EnsureEditorReady(this, Session);

        var editItem = EditorUiTestHelper.OpenContextMenuAndFindItem(
            this,
            Session,
            EditorUiTestHelper.TemplateLayoutName.Grid,
            EditorUiTestHelper.ElementName.Edit);

        EditorUiTestHelper.Step(this, "Invoking Edit from the context menu");
        editItem.Invoke();

        Assert.IsNotNull(Session.Find<Element>(By.AccessibilityId(EditorUiTestHelper.AccessibilityId.DialogTitle)));
        Assert.IsNotNull(Session.Find<Element>($"Edit '{EditorUiTestHelper.TemplateLayoutName.Grid}'"));
    }

    [TestMethod]
    public void OpenEditLayoutDialog_ByContextMenu_CustomLayout()
    {
        const string layoutName = "Custom layout";
        EditorUiTestHelper.EnsureEditorReady(this, Session);

        var editItem = EditorUiTestHelper.OpenContextMenuAndFindItem(
            this,
            Session,
            layoutName,
            EditorUiTestHelper.ElementName.Edit);

        EditorUiTestHelper.Step(this, "Invoking Edit from the custom-layout context menu");
        editItem.Invoke();

        Assert.IsNotNull(Session.Find<Element>(By.AccessibilityId(EditorUiTestHelper.AccessibilityId.DialogTitle)));
        Assert.IsNotNull(Session.Find<Element>($"Edit '{layoutName}'"));
    }

    [TestMethod]
    public void OpenContextMenu()
    {
        EditorUiTestHelper.EnsureEditorReady(this, Session);

        var editItem = EditorUiTestHelper.OpenContextMenuAndFindItem(
            this,
            Session,
            EditorUiTestHelper.TemplateLayoutName.Columns,
            EditorUiTestHelper.ElementName.Edit);

        Assert.IsNotNull(editItem);
    }

    [TestMethod]
    public void ClickMonitor()
    {
        EditorUiTestHelper.EnsureEditorReady(this, Session);

        var monitor1 = Session.Find<Element>("Monitor 1");
        var monitor2 = Session.Find<Element>("Monitor 2");

        Assert.IsNotNull(monitor1);
        Assert.IsNotNull(monitor2);
        Assert.IsTrue(monitor1.Selected);
        Assert.IsFalse(monitor2.Selected);

        EditorUiTestHelper.Step(this, "Selecting Monitor 2");
        monitor2.Click();

        Assert.IsFalse(monitor1.Selected);
        Assert.IsTrue(monitor2.Selected);
    }
}
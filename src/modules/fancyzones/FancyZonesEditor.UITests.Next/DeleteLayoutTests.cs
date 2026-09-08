// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using FancyZonesEditor.UITests.Utils;
using FancyZonesEditorCommon.Data;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FancyZonesEditor.UITests;

[TestClass]
public class DeleteLayoutTests : FancyZonesEditorTestBase
{
    private const string FirstCustomLayoutName = "Custom layout 1";
    private const string SecondCustomLayoutName = "Custom layout 2";
    private const string MonitorName = "monitor-1";

    public DeleteLayoutTests()
    {
        EditorTestData.WriteForDeleteLayoutTests(Files);
    }

    [TestMethod("FancyZonesEditor.Basic.DeleteNotAppliedLayout")]
    [TestCategory("FancyZones Editor #5")]
    public void DeleteNotAppliedLayout()
    {
        EditorUiTestHelper.EnsureEditorReady(this, Session);
        SelectAppliedBaselineLayout();

        EditorUiTestHelper.OpenEditLayoutDialog(this, Session, SecondCustomLayoutName);
        EditorUiTestHelper.Step(this, "Requesting deletion from the custom layout edit dialog");
        Session.Find<Button>(By.AccessibilityId(EditorUiTestHelper.AccessibilityId.DeleteLayoutButton)).Click();
        EditorUiTestHelper.RespondToDeleteDialog(this, Session, confirm: true);

        Assert.AreEqual(0, Session.FindAll<Element>(By.Name(SecondCustomLayoutName)).Count);

        var data = EditorUiTestHelper.ReadCustomLayouts();
        Assert.AreEqual(1, data.CustomLayouts.Count);
        Assert.IsFalse(data.CustomLayouts.Exists(x => x.Name == SecondCustomLayoutName));
    }

    [TestMethod("FancyZonesEditor.Basic.DeleteAppliedLayout")]
    [TestCategory("FancyZones Editor #5")]
    public void DeleteAppliedLayout()
    {
        EditorUiTestHelper.EnsureEditorReady(this, Session);
        SelectAppliedBaselineLayout();

        EditorUiTestHelper.OpenEditLayoutDialog(this, Session, FirstCustomLayoutName);
        EditorUiTestHelper.Step(this, "Requesting deletion of the currently applied custom layout");
        Session.Find<Button>(By.AccessibilityId(EditorUiTestHelper.AccessibilityId.DeleteLayoutButton)).Click();
        EditorUiTestHelper.RespondToDeleteDialog(this, Session, confirm: true);

        Assert.AreEqual(0, Session.FindAll<Element>(By.Name(FirstCustomLayoutName)).Count);
        Assert.IsTrue(Session.Find<Element>(EditorUiTestHelper.TemplateLayoutName.Blank).Selected);

        var customLayoutsData = EditorUiTestHelper.ReadCustomLayouts();
        Assert.AreEqual(1, customLayoutsData.CustomLayouts.Count);
        Assert.IsFalse(customLayoutsData.CustomLayouts.Exists(x => x.Name == FirstCustomLayoutName));

        var appliedLayoutsData = EditorUiTestHelper.ReadAppliedLayouts();
        var appliedLayout = appliedLayoutsData.AppliedLayouts.Find(x => x.Device.Monitor == MonitorName);
        Assert.AreEqual("blank", appliedLayout.AppliedLayout.Type);
    }

    [TestMethod("FancyZonesEditor.Basic.CancelDeletion")]
    [TestCategory("FancyZones Editor #5")]
    public void CancelDeletion()
    {
        EditorUiTestHelper.EnsureEditorReady(this, Session);
        SelectAppliedBaselineLayout();

        EditorUiTestHelper.OpenEditLayoutDialog(this, Session, SecondCustomLayoutName);
        EditorUiTestHelper.Step(this, "Requesting deletion and cancelling from the confirmation dialog");
        Session.Find<Button>(By.AccessibilityId(EditorUiTestHelper.AccessibilityId.DeleteLayoutButton)).Click();
        EditorUiTestHelper.RespondToDeleteDialog(this, Session, confirm: false);

        Assert.IsNotNull(Session.Find<Element>(SecondCustomLayoutName));

        var data = EditorUiTestHelper.ReadCustomLayouts();
        Assert.AreEqual(2, data.CustomLayouts.Count);
        Assert.IsTrue(data.CustomLayouts.Exists(x => x.Name == SecondCustomLayoutName));
    }

    [TestMethod("FancyZonesEditor.Basic.DeleteFromContextMenu")]
    [TestCategory("FancyZones Editor #5")]
    public void DeleteFromContextMenu()
    {
        EditorUiTestHelper.EnsureEditorReady(this, Session);
        SelectAppliedBaselineLayout();

        var deleteItem = EditorUiTestHelper.OpenContextMenuAndFindItem(this, Session, SecondCustomLayoutName, EditorUiTestHelper.ElementName.Delete);
        EditorUiTestHelper.Step(this, "Invoking delete from the custom-layout context menu");
        deleteItem.Invoke();
        EditorUiTestHelper.RespondToDeleteDialog(this, Session, confirm: true);

        Assert.AreEqual(0, Session.FindAll<Element>(By.Name(SecondCustomLayoutName)).Count);

        var data = EditorUiTestHelper.ReadCustomLayouts();
        Assert.AreEqual(1, data.CustomLayouts.Count);
        Assert.IsFalse(data.CustomLayouts.Exists(x => x.Name == SecondCustomLayoutName));
    }

    [TestMethod("FancyZonesEditor.Basic.DeleteDefaultLayout")]
    [TestCategory("FancyZones Editor #5")]
    public void DeleteDefaultLayout()
    {
        EditorUiTestHelper.EnsureEditorReady(this, Session);
        SelectAppliedBaselineLayout();

        var deleteItem = EditorUiTestHelper.OpenContextMenuAndFindItem(this, Session, SecondCustomLayoutName, EditorUiTestHelper.ElementName.Delete);
        EditorUiTestHelper.Step(this, "Invoking delete for the default custom layout from context menu");
        deleteItem.Invoke();
        EditorUiTestHelper.RespondToDeleteDialog(this, Session, confirm: true);

        EditorUiTestHelper.OpenEditLayoutDialog(this, Session, EditorUiTestHelper.TemplateLayoutName.PriorityGrid);
        Assert.IsNotNull(Session.Find<Button>(By.AccessibilityId(EditorUiTestHelper.AccessibilityId.HorizontalDefaultButtonChecked)));
        Session.Find<Button>(EditorUiTestHelper.ElementName.Cancel).Click();

        var defaultLayoutsData = EditorUiTestHelper.ReadDefaultLayouts();
        var horizontal = defaultLayoutsData.DefaultLayouts.Find(x => x.MonitorConfiguration == "horizontal");
        Assert.AreEqual("priority-grid", horizontal.Layout.Type);
    }

    [TestMethod("FancyZonesEditor.Basic.DeleteLayoutWithHotkey")]
    [TestCategory("FancyZones Editor #5")]
    public void DeleteLayoutWithHotkey()
    {
        EditorUiTestHelper.EnsureEditorReady(this, Session);
        SelectAppliedBaselineLayout();

        var deleteItem = EditorUiTestHelper.OpenContextMenuAndFindItem(this, Session, SecondCustomLayoutName, EditorUiTestHelper.ElementName.Delete);
        EditorUiTestHelper.Step(this, "Invoking delete for custom layout that currently owns a hotkey");
        deleteItem.Invoke();
        EditorUiTestHelper.RespondToDeleteDialog(this, Session, confirm: true);

        EditorUiTestHelper.OpenEditLayoutDialog(this, Session, FirstCustomLayoutName);

        var hotkeyComboBox = Session.Find<Element>(By.AccessibilityId(EditorUiTestHelper.AccessibilityId.HotkeyComboBox));
        Assert.IsNotNull(hotkeyComboBox);
        EditorUiTestHelper.Step(this, "Opening the layout shortcut combo to verify free keys");
        hotkeyComboBox.Click();

        for (var i = 0; i < 10; i++)
        {
            Assert.IsNotNull(Session.Find<Element>(By.Name($"{i}")), $"Expected hotkey option '{i}' was not found.");
        }

        EditorUiTestHelper.Step(this, "Dismissing the layout shortcut popup");
        KeyboardHelper.SendKeys(Key.Esc);
        var visibleDialog = Session.FindAll<Element>(By.AccessibilityId(EditorUiTestHelper.AccessibilityId.DialogTitle), 500)
            .Any(element => element.Displayed && element.Width > 0 && element.Height > 0);
        if (visibleDialog)
        {
            var cancelButton = Session.FindAll<Button>(By.Name(EditorUiTestHelper.ElementName.Cancel), 500)
                .FirstOrDefault(button => button.Displayed && button.IsEnabled && button.Width > 0 && button.Height > 0);
            cancelButton?.Invoke();
        }

        var hotkeysData = EditorUiTestHelper.ReadLayoutHotkeys();
        var layoutHotkeyCount = hotkeysData.LayoutHotkeys.Count(layout => layout.Key != -1);
        Assert.AreEqual(0, layoutHotkeyCount);
    }

    private void SelectAppliedBaselineLayout()
    {
        var selected = Session.Find<Element>(FirstCustomLayoutName);
        if (!selected.Selected)
        {
            EditorUiTestHelper.Step(this, $"Applying '{FirstCustomLayoutName}' to establish baseline");
            selected.Click();
        }

        selected = Session.Find<Element>(FirstCustomLayoutName);
        Assert.IsTrue(selected.Selected);
    }
}
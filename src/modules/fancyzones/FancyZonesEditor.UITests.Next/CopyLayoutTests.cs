// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using FancyZonesEditor.UITests.Utils;
using FancyZonesEditorCommon.Data;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FancyZonesEditor.UITests;

[TestClass]
public class CopyLayoutTests : FancyZonesEditorTestBase
{
    private const string SourceCustomLayoutName = "Grid custom layout";
    private const string SourceCustomLayoutUuid = "{0D6D2F58-9184-4804-81E4-4E4CC3476DC1}";

    private static readonly DefaultLayouts.DefaultLayoutsListWrapper ExpectedDefaultLayouts = new()
    {
        DefaultLayouts =
        [
            new DefaultLayouts.DefaultLayoutWrapper
            {
                MonitorConfiguration = "vertical",
                Layout = new DefaultLayouts.DefaultLayoutWrapper.LayoutWrapper
                {
                    Type = "custom",
                    Uuid = SourceCustomLayoutUuid,
                },
            },
        ],
    };

    private static readonly LayoutHotkeys.LayoutHotkeysWrapper ExpectedHotkeys = new()
    {
        LayoutHotkeys =
        [
            new LayoutHotkeys.LayoutHotkeyWrapper
            {
                LayoutId = SourceCustomLayoutUuid,
                Key = 0,
            },
        ],
    };

    public CopyLayoutTests()
    {
        EditorTestData.WriteForCopyLayoutTests(Files);
    }

    [TestMethod("FancyZonesEditor.Basic.CopyTemplate_FromEditLayoutWindow")]
    [TestCategory("FancyZones Editor #4")]
    public void CopyTemplate_FromEditLayoutWindow()
    {
        const string sourceLayoutName = EditorUiTestHelper.TemplateLayoutName.Focus;
        const string copiedLayoutName = EditorUiTestHelper.TemplateLayoutName.Focus + " (1)";

        EditorUiTestHelper.EnsureEditorReady(this, Session);
        EditorUiTestHelper.OpenEditLayoutDialog(this, Session, sourceLayoutName);
        EditorUiTestHelper.ClickCopyOrDuplicate(this, Session);

        Assert.IsNotNull(Session.Find<Element>(copiedLayoutName));

        var data = EditorUiTestHelper.ReadCustomLayouts();
        Assert.AreEqual(2, data.CustomLayouts.Count);
        Assert.IsTrue(data.CustomLayouts.Exists(x => x.Name == copiedLayoutName));
    }

    [TestMethod("FancyZonesEditor.Basic.CopyTemplate_FromEditLayoutWindow")]
    [TestCategory("FancyZones Editor #4")]
    public void CopyTemplate_FromContextMenu()
    {
        const string sourceLayoutName = EditorUiTestHelper.TemplateLayoutName.Rows;
        const string copiedLayoutName = EditorUiTestHelper.TemplateLayoutName.Rows + " (1)";

        EditorUiTestHelper.EnsureEditorReady(this, Session);

        var createItem = EditorUiTestHelper.OpenContextMenuAndFindItem(this, Session, sourceLayoutName, EditorUiTestHelper.ElementName.CreateCustomLayout);
        EditorUiTestHelper.Step(this, "Invoking create-custom-layout from the context menu");
        createItem.Invoke();

        Assert.IsNotNull(Session.Find<Element>(copiedLayoutName));

        var data = EditorUiTestHelper.ReadCustomLayouts();
        Assert.AreEqual(2, data.CustomLayouts.Count);
        Assert.IsTrue(data.CustomLayouts.Exists(x => x.Name == copiedLayoutName));
    }

    [TestMethod("FancyZonesEditor.Basic.CopyTemplate_DefaultLayout")]
    [TestCategory("FancyZones Editor #13")]
    public void CopyTemplate_DefaultLayout()
    {
        const string sourceLayoutName = EditorUiTestHelper.TemplateLayoutName.PriorityGrid;
        const string copiedLayoutName = EditorUiTestHelper.TemplateLayoutName.PriorityGrid + " (1)";

        EditorUiTestHelper.EnsureEditorReady(this, Session);
        EditorUiTestHelper.OpenEditLayoutDialog(this, Session, sourceLayoutName);
        EditorUiTestHelper.ClickCopyOrDuplicate(this, Session);

        Assert.IsNotNull(Session.Find<Element>(copiedLayoutName));

        var customLayoutsData = EditorUiTestHelper.ReadCustomLayouts();
        Assert.AreEqual(2, customLayoutsData.CustomLayouts.Count);

        EditorUiTestHelper.OpenEditLayoutDialog(this, Session, sourceLayoutName);
        Assert.IsNotNull(Session.Find<Button>(By.AccessibilityId(EditorUiTestHelper.AccessibilityId.HorizontalDefaultButtonChecked)));
        Session.Find<Button>(EditorUiTestHelper.ElementName.Cancel).Click();

        EditorUiTestHelper.OpenEditLayoutDialog(this, Session, copiedLayoutName);
        Assert.IsNotNull(Session.Find<Button>(By.AccessibilityId(EditorUiTestHelper.AccessibilityId.HorizontalDefaultButtonUnchecked)));
        Session.Find<Button>(EditorUiTestHelper.ElementName.Cancel).Click();

        var defaultLayouts = new DefaultLayouts();
        var defaultLayoutsData = defaultLayouts.Read(defaultLayouts.File);
        Assert.AreEqual(defaultLayouts.Serialize(ExpectedDefaultLayouts), defaultLayouts.Serialize(defaultLayoutsData));
    }

    [TestMethod("FancyZonesEditor.Basic.CopyCustomLayout_FromEditLayoutWindow")]
    [TestCategory("FancyZones Editor #4")]
    public void CopyCustomLayout_FromEditLayoutWindow()
    {
        const string copiedLayoutName = SourceCustomLayoutName + " (1)";

        EditorUiTestHelper.EnsureEditorReady(this, Session);
        EditorUiTestHelper.OpenEditLayoutDialog(this, Session, SourceCustomLayoutName);
        EditorUiTestHelper.ClickCopyOrDuplicate(this, Session);

        Assert.IsNotNull(Session.Find<Element>(copiedLayoutName));

        var data = EditorUiTestHelper.ReadCustomLayouts();
        Assert.AreEqual(2, data.CustomLayouts.Count);
        Assert.IsTrue(data.CustomLayouts.Exists(x => x.Name == copiedLayoutName));
    }

    [TestMethod("FancyZonesEditor.Basic.CopyCustomLayout_FromContextMenu")]
    [TestCategory("FancyZones Editor #4")]
    public void CopyCustomLayout_FromContextMenu()
    {
        const string copiedLayoutName = SourceCustomLayoutName + " (1)";

        EditorUiTestHelper.EnsureEditorReady(this, Session);

        var duplicateItem = EditorUiTestHelper.OpenContextMenuAndFindItem(this, Session, SourceCustomLayoutName, EditorUiTestHelper.ElementName.Duplicate);
        EditorUiTestHelper.Step(this, "Invoking duplicate for custom layout from the context menu");
        duplicateItem.Invoke();

        Assert.IsNotNull(Session.Find<Element>(copiedLayoutName));

        var data = EditorUiTestHelper.ReadCustomLayouts();
        Assert.AreEqual(2, data.CustomLayouts.Count);
        Assert.IsTrue(data.CustomLayouts.Exists(x => x.Name == copiedLayoutName));
    }

    [TestMethod("FancyZonesEditor.Basic.CopyCustomLayout_DefaultLayout")]
    [TestCategory("FancyZones Editor #13")]
    public void CopyCustomLayout_DefaultLayout()
    {
        const string copiedLayoutName = SourceCustomLayoutName + " (1)";

        EditorUiTestHelper.EnsureEditorReady(this, Session);
        EditorUiTestHelper.OpenEditLayoutDialog(this, Session, SourceCustomLayoutName);
        EditorUiTestHelper.ClickCopyOrDuplicate(this, Session);

        Assert.IsNotNull(Session.Find<Element>(copiedLayoutName));

        var customLayoutsData = EditorUiTestHelper.ReadCustomLayouts();
        Assert.AreEqual(2, customLayoutsData.CustomLayouts.Count);

        EditorUiTestHelper.OpenEditLayoutDialog(this, Session, SourceCustomLayoutName);
        Assert.IsNotNull(Session.Find<Button>(By.AccessibilityId(EditorUiTestHelper.AccessibilityId.VerticalDefaultButtonChecked)));
        Session.Find<Button>(EditorUiTestHelper.ElementName.Cancel).Click();

        EditorUiTestHelper.OpenEditLayoutDialog(this, Session, copiedLayoutName);
        Assert.IsNotNull(Session.Find<Button>(By.AccessibilityId(EditorUiTestHelper.AccessibilityId.VerticalDefaultButtonUnchecked)));
        Session.Find<Button>(EditorUiTestHelper.ElementName.Cancel).Click();

        var defaultLayouts = new DefaultLayouts();
        var defaultLayoutsData = defaultLayouts.Read(defaultLayouts.File);
        Assert.AreEqual(defaultLayouts.Serialize(ExpectedDefaultLayouts), defaultLayouts.Serialize(defaultLayoutsData));
    }

    [TestMethod("FancyZonesEditor.Basic.CopyCustomLayout_Hotkey")]
    [TestCategory("FancyZones Editor #4")]
    public void CopyCustomLayout_Hotkey()
    {
        const string copiedLayoutName = SourceCustomLayoutName + " (1)";

        EditorUiTestHelper.EnsureEditorReady(this, Session);
        EditorUiTestHelper.OpenEditLayoutDialog(this, Session, SourceCustomLayoutName);
        EditorUiTestHelper.ClickCopyOrDuplicate(this, Session);

        Assert.IsNotNull(Session.Find<Element>(copiedLayoutName));

        var customLayoutsData = EditorUiTestHelper.ReadCustomLayouts();
        Assert.AreEqual(2, customLayoutsData.CustomLayouts.Count);

        EditorUiTestHelper.OpenEditLayoutDialog(this, Session, SourceCustomLayoutName);
        var hotkeyComboBox = Session.Find<Element>(By.AccessibilityId(EditorUiTestHelper.AccessibilityId.HotkeyComboBox));
        Assert.IsNotNull(hotkeyComboBox);
        Assert.AreEqual("0", hotkeyComboBox.GetValue());
        Session.Find<Button>(EditorUiTestHelper.ElementName.Cancel).Click();

        EditorUiTestHelper.OpenEditLayoutDialog(this, Session, copiedLayoutName);
        hotkeyComboBox = Session.Find<Element>(By.AccessibilityId(EditorUiTestHelper.AccessibilityId.HotkeyComboBox));
        Assert.IsNotNull(hotkeyComboBox);
        Assert.AreEqual("None", hotkeyComboBox.GetValue());
        Session.Find<Button>(EditorUiTestHelper.ElementName.Cancel).Click();

        var hotkeys = new LayoutHotkeys();
        var hotkeysData = hotkeys.Read(hotkeys.File);
        Assert.AreEqual(hotkeys.Serialize(ExpectedHotkeys), hotkeys.Serialize(hotkeysData));
    }
}
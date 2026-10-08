// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using FancyZonesEditor.UITests.Utils;
using FancyZonesEditorCommon.Data;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Globalization;

namespace FancyZonesEditor.UITests;

[TestClass]
public class LayoutHotkeysTests : FancyZonesEditorTestBase
{
    private const string EditorProcessName = "PowerToys.FancyZonesEditor";

    private static readonly (string Name, string Uuid)[] Layouts =
    [
        ("Layout 0", "{0D6D2F58-9184-4804-81E4-4E4CC3476DC1}"),
        ("Layout 1", "{E7807D0D-6223-4883-B15B-1F3883944C09}"),
        ("Layout 2", "{F1A94F38-82B6-4876-A653-70D0E882DE2A}"),
        ("Layout 3", "{F5FDBC04-0760-4776-9F05-96AAC4AE613F}"),
    ];

    private static readonly LayoutHotkeys.LayoutHotkeysWrapper InitialHotkeys = new()
    {
        LayoutHotkeys =
        [
            new LayoutHotkeys.LayoutHotkeyWrapper
            {
                LayoutId = Layouts[0].Uuid,
                Key = 0,
            },
            new LayoutHotkeys.LayoutHotkeyWrapper
            {
                LayoutId = Layouts[1].Uuid,
                Key = 1,
            },
        ],
    };

    public LayoutHotkeysTests()
    {
        EditorTestData.WriteForLayoutHotkeysTests(Files);
    }

    [TestMethod("FancyZonesEditor.Basic.HotKey_Initialize")]
    [TestCategory("FancyZones Editor #11")]
    public void Initialize()
    {
        EditorUiTestHelper.EnsureEditorReady(this, Session);

        foreach (var (name, uuid) in Layouts)
        {
            EditorUiTestHelper.OpenEditLayoutDialog(this, Session, name);

            var assigned = InitialHotkeys.LayoutHotkeys.FirstOrDefault(x => x.LayoutId == uuid);
            var expected = assigned.LayoutId == uuid
                ? assigned.Key.ToString(CultureInfo.InvariantCulture)
                : "None";
            Assert.AreEqual(expected, ReadSelectedHotkeyValue(), $"Unexpected selected hotkey for '{name}'.");

            var processSession = OpenHotkeyPopup(name);
            AssertOptionPresent(processSession, expected);
            for (var i = 2; i < 10; i++)
            {
                AssertOptionPresent(processSession, i.ToString(CultureInfo.InvariantCulture));
            }

            ClosePopupIfOpen(processSession);
            CloseEditDialog(cancel: true);
        }
    }

    [TestMethod("FancyZonesEditor.Basic.HotKey_Assign_Save")]
    [TestCategory("FancyZones Editor #11")]
    public void Assign_Save()
    {
        EditorUiTestHelper.EnsureEditorReady(this, Session);

        const string key = "3";
        var target = Layouts[2];

        EditorUiTestHelper.OpenEditLayoutDialog(this, Session, target.Name);
        SelectHotkeyOption(target.Name, key);
        Assert.AreEqual(key, ReadSelectedHotkeyValue());

        EditorUiTestHelper.Step(this, "Saving assigned layout shortcut");
        SaveHotkeyAndWait(target.Uuid, 3);

        var checkLayout = Layouts[3];
        EditorUiTestHelper.OpenEditLayoutDialog(this, Session, checkLayout.Name);
        var processSession = OpenHotkeyPopup(checkLayout.Name);
        AssertOptionMissing(processSession, key);
        ClosePopupIfOpen(processSession);
        CloseEditDialog(cancel: true);
    }

    [TestMethod("FancyZonesEditor.Basic.HotKey_Assign_Cancel")]
    [TestCategory("FancyZones Editor #11")]
    public void Assign_Cancel()
    {
        EditorUiTestHelper.EnsureEditorReady(this, Session);

        const string key = "3";
        var target = Layouts[2];

        EditorUiTestHelper.OpenEditLayoutDialog(this, Session, target.Name);
        SelectHotkeyOption(target.Name, key);
        Assert.AreEqual(key, ReadSelectedHotkeyValue());

        EditorUiTestHelper.Step(this, "Cancelling assigned layout shortcut");
        Session.Find<Button>(EditorUiTestHelper.ElementName.Cancel).Click();

        var hotkeys = new LayoutHotkeys();
        var actual = EditorUiTestHelper.ReadLayoutHotkeys();
        Assert.AreEqual(hotkeys.Serialize(InitialHotkeys), hotkeys.Serialize(actual));

        var checkLayout = Layouts[3];
        EditorUiTestHelper.OpenEditLayoutDialog(this, Session, checkLayout.Name);
        var processSession = OpenHotkeyPopup(checkLayout.Name);
        AssertOptionPresent(processSession, key);
        ClosePopupIfOpen(processSession);
        CloseEditDialog(cancel: true);
    }

    [TestMethod("FancyZonesEditor.Basic.HotKey_Assign_AllPossibleValues")]
    [TestCategory("FancyZones Editor #11")]
    public void Assign_AllPossibleValues()
    {
        EditorUiTestHelper.EnsureEditorReady(this, Session);

        foreach (var (name, _) in Layouts)
        {
            EditorUiTestHelper.OpenEditLayoutDialog(this, Session, name);
            SelectHotkeyOption(name, "None");
            SaveHotkeyAndWait(Layouts.First(layout => layout.Name == name).Uuid, null);
        }

        var target = Layouts[3];
        for (var key = 0; key < 10; key++)
        {
            var expected = key.ToString(CultureInfo.InvariantCulture);
            EditorUiTestHelper.OpenEditLayoutDialog(this, Session, target.Name);
            SelectHotkeyOption(target.Name, expected);
            SaveHotkeyAndWait(target.Uuid, key);

            EditorUiTestHelper.OpenEditLayoutDialog(this, Session, target.Name);
            Assert.AreEqual(expected, ReadSelectedHotkeyValue(), $"Assigned key '{expected}' was not persisted.");
            CloseEditDialog(cancel: true);
        }
    }

    [TestMethod("FancyZonesEditor.Basic.HotKey_Reset_Save")]
    [TestCategory("FancyZones Editor #11")]
    public void Reset_Save()
    {
        EditorUiTestHelper.EnsureEditorReady(this, Session);

        var target = Layouts[0];
        const int assignedKey = 0;

        EditorUiTestHelper.OpenEditLayoutDialog(this, Session, target.Name);
        SelectHotkeyOption(target.Name, "None");
        Assert.AreEqual("None", ReadSelectedHotkeyValue());

        EditorUiTestHelper.Step(this, "Saving layout shortcut reset");
        var data = SaveHotkeyAndWait(target.Uuid, null);
        Assert.IsFalse(data.LayoutHotkeys.Any(x => x.LayoutId == target.Uuid && x.Key == assignedKey));

        var checkLayout = Layouts[3];
        EditorUiTestHelper.OpenEditLayoutDialog(this, Session, checkLayout.Name);
        var processSession = OpenHotkeyPopup(checkLayout.Name);
        AssertOptionPresent(processSession, assignedKey.ToString(CultureInfo.InvariantCulture));
        ClosePopupIfOpen(processSession);
        CloseEditDialog(cancel: true);
    }

    [TestMethod("FancyZonesEditor.Basic.HotKey_Reset_Cancel")]
    [TestCategory("FancyZones Editor #11")]
    public void Reset_Cancel()
    {
        EditorUiTestHelper.EnsureEditorReady(this, Session);

        var target = Layouts[0];
        const int assignedKey = 0;

        EditorUiTestHelper.OpenEditLayoutDialog(this, Session, target.Name);
        SelectHotkeyOption(target.Name, "None");
        Assert.AreEqual("None", ReadSelectedHotkeyValue());

        EditorUiTestHelper.Step(this, "Cancelling layout shortcut reset");
        Session.Find<Button>(EditorUiTestHelper.ElementName.Cancel).Click();

        var data = EditorUiTestHelper.ReadLayoutHotkeys();
        Assert.IsTrue(data.LayoutHotkeys.Any(x => x.LayoutId == target.Uuid && x.Key == assignedKey));

        var checkLayout = Layouts[3];
        EditorUiTestHelper.OpenEditLayoutDialog(this, Session, checkLayout.Name);
        var processSession = OpenHotkeyPopup(checkLayout.Name);
        AssertOptionMissing(processSession, assignedKey.ToString(CultureInfo.InvariantCulture));
        ClosePopupIfOpen(processSession);
        CloseEditDialog(cancel: true);
    }

    private Session OpenHotkeyPopup(string layoutName)
    {
        EditorUiTestHelper.Step(this, $"Opening the shortcut popup for '{layoutName}'");
        Session.Find<Element>(By.AccessibilityId(EditorUiTestHelper.AccessibilityId.HotkeyComboBox)).Invoke();

        var processSession = Session.FromProcess(EditorProcessName, PowerToysModule.FancyZonesEditor, timeoutMS: 10_000);
        Assert.IsTrue(
            processSession.WaitFor(() => FindHotkeyOption(processSession, "None") is not null, 10_000),
            $"The shortcut popup did not open for '{layoutName}'.");

        return processSession;
    }

    private void SelectHotkeyOption(string layoutName, string optionName)
    {
        var processSession = OpenHotkeyPopup(layoutName);
        var option = RequireHotkeyOption(processSession, optionName);

        EditorUiTestHelper.Step(this, $"Selecting shortcut option '{optionName}' using real mouse input");
        option.MouseClick();

        Assert.IsTrue(
            Session.WaitFor(() => string.Equals(ReadSelectedHotkeyValue(), optionName, StringComparison.Ordinal), 5_000),
            $"The shortcut selection did not update to '{optionName}'.");
    }

    private string ReadSelectedHotkeyValue()
    {
        var combo = Session.Find<Element>(By.AccessibilityId(EditorUiTestHelper.AccessibilityId.HotkeyComboBox));
        return string.IsNullOrWhiteSpace(combo.GetValue()) ? combo.GetProperty("Name") : combo.GetValue();
    }

    private static bool IsHotkeyOption(Element element)
    {
        return string.Equals(element.ControlType, "ListItem", StringComparison.OrdinalIgnoreCase)
            || string.Equals(element.ControlType, "MenuItem", StringComparison.OrdinalIgnoreCase);
    }

    private static Element? FindHotkeyOption(Session processSession, string optionName)
    {
        return processSession
            .FindAll<Element>(By.Name(optionName), 500)
            .FirstOrDefault(element =>
                IsHotkeyOption(element) &&
                string.Equals(element.Name, optionName, StringComparison.Ordinal));
    }

    private static Element RequireHotkeyOption(Session processSession, string optionName)
    {
        Element? option = null;
        Assert.IsTrue(
            processSession.WaitFor(() =>
            {
                option = FindHotkeyOption(processSession, optionName);
                return option is not null;
            }, 10_000),
            $"Shortcut option '{optionName}' was not found in the popup.");

        return option!;
    }

    private static void AssertOptionPresent(Session processSession, string optionName)
    {
        Assert.IsNotNull(RequireHotkeyOption(processSession, optionName));
    }

    private static void AssertOptionMissing(Session processSession, string optionName)
    {
        var missing = processSession.WaitFor(() => FindHotkeyOption(processSession, optionName) is null, 5_000);
        Assert.IsTrue(missing, $"Shortcut option '{optionName}' was unexpectedly available.");
    }

    private void ClosePopupIfOpen(Session processSession)
    {
        if (FindHotkeyOption(processSession, "None") is null)
        {
            return;
        }

        EditorUiTestHelper.Step(this, "Dismissing the shortcut popup");
        KeyboardHelper.SendKeys(Key.Esc);
        _ = processSession.WaitFor(() => FindHotkeyOption(processSession, "None") is null, 2_000);
    }

    private void CloseEditDialog(bool cancel)
    {
        if (!Session.WaitForElement(By.AccessibilityId(EditorUiTestHelper.AccessibilityId.DialogTitle), 1_000))
        {
            return;
        }

        var buttonName = cancel ? EditorUiTestHelper.ElementName.Cancel : EditorUiTestHelper.ElementName.Save;
        var button = Session.FindAll<Button>(By.Name(buttonName), 500).FirstOrDefault();
        button?.Invoke();
    }

    private LayoutHotkeys.LayoutHotkeysWrapper SaveHotkeyAndWait(string layoutUuid, int? expectedKey)
    {
        EditorUiTestHelper.Step(
            this,
            expectedKey.HasValue
                ? $"Saving layout shortcut {expectedKey.Value} for '{layoutUuid}'"
                : $"Saving removal of the layout shortcut for '{layoutUuid}'");
        Session.Find<Button>(EditorUiTestHelper.ElementName.Save).Invoke();

        return EditorUiTestHelper.WaitForLayoutHotkeys(
            this,
            data => expectedKey.HasValue
                                ? data.LayoutHotkeys.Count(item => item.LayoutId == layoutUuid) == 1 &&
                                    data.LayoutHotkeys.Any(item => item.LayoutId == layoutUuid && item.Key == expectedKey.Value)
                : !data.LayoutHotkeys.Any(item => item.LayoutId == layoutUuid),
            expectedKey.HasValue
                ? $"layout '{layoutUuid}' to own key {expectedKey.Value}"
                : $"layout '{layoutUuid}' to have no assigned key");
    }
}

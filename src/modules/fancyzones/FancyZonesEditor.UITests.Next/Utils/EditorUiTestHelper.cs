// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using FancyZonesEditorCommon.Data;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FancyZonesEditor.UITests.Utils;

public static class EditorUiTestHelper
{
    private const string EditorProcessName = "PowerToys.FancyZonesEditor";
    private const string LayoutOverlayTitle = "FancyZones Layout";

    public static class AccessibilityId
    {
        public const string MainWindow = "MainWindow1";
        public const string Monitors = "Monitors";
        public const string ScalingText = "ScalingText";
        public const string ResolutionText = "ResolutionText";
        public const string NewLayoutButton = "NewLayoutButton";
        public const string EditLayoutButton = "EditLayoutButton";
        public const string DialogTitle = "EditLayoutDialogTitle";
        public const string GridRadioButton = "GridLayoutRadioButton";
        public const string CanvasRadioButton = "CanvasLayoutRadioButton";
        public const string LayoutNameText = "LayoutNameText";
        public const string TemplateZoneSlider = "TemplateZoneCount";
        public const string SensitivitySlider = "SensitivityInput";
        public const string SpacingSlider = "Spacing";
        public const string SpacingToggle = "spaceAroundSetting";
        public const string HorizontalDefaultButtonUnchecked = "SetLayoutAsHorizontalDefaultButton";
        public const string VerticalDefaultButtonUnchecked = "SetLayoutAsVerticalDefaultButton";
        public const string HorizontalDefaultButtonChecked = "HorizontalDefaultLayoutButton";
        public const string VerticalDefaultButtonChecked = "VerticalDefaultLayoutButton";
        public const string CopyTemplate = "createFromTemplateLayoutButton";
        public const string DuplicateLayoutButton = "duplicateLayoutButton";
        public const string DeleteLayoutButton = "deleteLayoutButton";
        public const string HotkeyComboBox = "quickKeySelectionComboBox";
        public const string EditZonesButton = "editZoneLayoutButton";
        public const string NewZoneButton = "newZoneButton";
        public const string DeleteZoneButton = "DeleteButton";
        public const string TopRightCorner = "NEResize";
        public const string PrimaryButton = "PrimaryButton";
        public const string SecondaryButton = "SecondaryButton";
    }

    public static class ElementName
    {
        public const string Save = "Save";
        public const string Cancel = "Cancel";
        public const string Edit = "Edit";
        public const string EditZones = "Edit zones";
        public const string Delete = "Delete";
        public const string Duplicate = "Duplicate";
        public const string CreateCustomLayout = "Create custom layout";
        public const string MergeZones = "Merge zones";
        public const string CanvasLayoutEditor = "Canvas layout editor";
        public const string GridLayoutEditor = "Grid layout editor";
    }

    public static class ClassName
    {
        public const string Popup = "Popup";
        public const string ContextMenu = "ContextMenu";
        public const string CanvasZone = "CanvasZone";
        public const string GridZone = "GridZone";
        public const string Thumb = "Thumb";
        public const string Button = "Button";
    }

    public static class TemplateLayoutName
    {
        public const string Blank = "No layout";
        public const string Focus = "Focus";
        public const string Rows = "Rows";
        public const string Columns = "Columns";
        public const string Grid = "Grid";
        public const string PriorityGrid = "Priority Grid";
    }

    public static void Step(UITestBase testBase, string message)
    {
        testBase.TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] {message}");
    }

    public static void EnsureEditorReady(UITestBase testBase, Session session)
    {
        Step(testBase, "Waiting for the FancyZones Editor main window");
        Assert.IsTrue(
            session.WaitForElement(By.AccessibilityId(AccessibilityId.MainWindow), 30_000),
            "The editor process started but its main window was not ready for automation.");

        Step(testBase, "Waiting for the monitor list");
        Assert.IsTrue(
            session.WaitForElement(By.AccessibilityId(AccessibilityId.Monitors), 30_000),
            "The editor opened but did not render its monitor list.");

        Step(testBase, "Waiting for the new-layout button");
        Assert.IsTrue(
            session.WaitForElement(By.AccessibilityId(AccessibilityId.NewLayoutButton), 30_000),
            "The editor opened but did not render the new-layout button.");
    }

    public static void EnsureForeground(UITestBase testBase, Session session, string reason)
    {
        Step(testBase, $"Bringing the editor window to foreground before {reason}");
        var foregroundStable = WindowControl.WaitForForeground(new IntPtr(session.WindowHandle), 10_000, requiredConsecutiveMatches: 3);
        Assert.IsTrue(foregroundStable, $"The editor window did not become foreground before {reason}.");
    }

    public static void SelectMonitor(UITestBase testBase, Session session, string monitorName)
    {
        EnsureForeground(testBase, session, $"selecting {monitorName}");
        Step(testBase, $"Selecting {monitorName}");

        var monitors = session.Find<Element>(By.AccessibilityId(AccessibilityId.Monitors));
        var monitor = monitors.Find<Element>(monitorName);
        monitor.Click();
    }

    public static void OpenEditLayoutDialog(UITestBase testBase, Session session, string layoutName)
    {
        EnsureForeground(testBase, session, $"opening the edit dialog for '{layoutName}'");
        Step(testBase, $"Opening edit dialog for '{layoutName}'");

        var layoutCard = session.Find<Button>(layoutName);
        var editButton = session.FindAll<Button>(By.AccessibilityId(AccessibilityId.EditLayoutButton))
            .FirstOrDefault(button => CenterIsInside(button, layoutCard));
        Assert.IsNotNull(editButton, $"No edit button was found inside the '{layoutName}' layout card.");
        editButton.Invoke();

        Assert.IsTrue(
            session.WaitForElement(By.AccessibilityId(AccessibilityId.DialogTitle), 10_000),
            $"Edit dialog for '{layoutName}' did not appear.");
    }

    public static Session EnterZoneEditModeFromDialog(UITestBase testBase, Session session, string expectedEditorWindowName)
    {
        var knownEditorWindows = WindowsFinder.ListByApp(EditorProcessName)
            .Select(window => window.Hwnd)
            .ToHashSet();

        EnsureForeground(testBase, session, $"opening '{expectedEditorWindowName}' from the edit dialog");
        Step(testBase, $"Opening '{expectedEditorWindowName}' from the edit dialog");
        session.Find<Button>(By.AccessibilityId(AccessibilityId.EditZonesButton)).Invoke();

        return WaitForZoneEditorWindow(testBase, knownEditorWindows, expectedEditorWindowName);
    }

    public static Session EnterZoneEditModeFromContextMenu(UITestBase testBase, Session session, string layoutName, string expectedEditorWindowName)
    {
        var editZonesItem = OpenContextMenuAndFindItem(testBase, session, layoutName, ElementName.EditZones);
        var knownEditorWindows = WindowsFinder.ListByApp(EditorProcessName)
            .Select(window => window.Hwnd)
            .ToHashSet();

        Step(testBase, $"Invoking context menu item '{ElementName.EditZones}' for '{layoutName}'");
        editZonesItem.Invoke();

        return WaitForZoneEditorWindow(testBase, knownEditorWindows, expectedEditorWindowName);
    }

    public static Session FindZoneEditorSurface(UITestBase testBase)
    {
        Step(testBase, $"Locating the full-screen zone editor surface '{LayoutOverlayTitle}'");
        var surface = WindowsFinder.WaitForWindowByApp(
            EditorProcessName,
            window => string.Equals(window.Title, LayoutOverlayTitle, StringComparison.Ordinal) && window.Width >= 500 && window.Height >= 400,
            timeoutMS: 10_000,
            pollIntervalMS: 100);

        Assert.IsNotNull(surface, $"The zone editor surface '{LayoutOverlayTitle}' did not become visible.");
        return surface!;
    }

    public static Element OpenContextMenuAndFindItem(UITestBase testBase, Session session, string layoutName, string menuItem)
    {
        EnsureForeground(testBase, session, $"opening the context menu for '{layoutName}'");

        Step(testBase, $"Opening context menu for layout '{layoutName}'");
        session.Find<Button>(layoutName).Click(rightClick: true);

        Step(testBase, $"Waiting for context menu item '{menuItem}'");
        Element? foundItem = null;
        var itemAppeared = session.WaitFor(() =>
        {
            var candidates = session.FindAll<Element>(By.Name(menuItem), 500);
            foundItem = candidates.FirstOrDefault(IsMenuItem);
            return foundItem is not null;
        }, 10_000);

        Assert.IsTrue(itemAppeared && foundItem is not null, $"Context menu item '{menuItem}' was not found after right-clicking '{layoutName}'.");
        return foundItem!;
    }

    public static void ClickCopyOrDuplicate(UITestBase testBase, Session session)
    {
        var copyTemplateButton = session.FindAll<Button>(By.AccessibilityId(AccessibilityId.CopyTemplate)).FirstOrDefault();
        if (copyTemplateButton is not null)
        {
            Step(testBase, "Duplicating from template using the create-custom-from-template button");
            copyTemplateButton.Click();
            return;
        }

        Step(testBase, "Duplicating custom layout from the edit dialog");
        session.Find<Button>(By.AccessibilityId(AccessibilityId.DuplicateLayoutButton)).Click();
    }

    public static void RespondToDeleteDialog(UITestBase testBase, Session session, bool confirm)
    {
        Assert.IsTrue(
            session.WaitForElement(By.AccessibilityId(AccessibilityId.PrimaryButton), 10_000),
            "Delete confirmation dialog did not appear.");

        var buttonId = confirm ? AccessibilityId.PrimaryButton : AccessibilityId.SecondaryButton;
        var action = confirm ? "Confirming layout deletion" : "Cancelling layout deletion";
        Step(testBase, action);
        session.Find<Button>(By.AccessibilityId(buttonId)).Click();
    }

    public static CustomLayouts.CustomLayoutListWrapper ReadCustomLayouts()
    {
        var customLayouts = new CustomLayouts();
        return customLayouts.Read(customLayouts.File);
    }

    public static AppliedLayouts.AppliedLayoutsListWrapper ReadAppliedLayouts()
    {
        var appliedLayouts = new AppliedLayouts();
        return appliedLayouts.Read(appliedLayouts.File);
    }

    public static DefaultLayouts.DefaultLayoutsListWrapper ReadDefaultLayouts()
    {
        var defaultLayouts = new DefaultLayouts();
        return defaultLayouts.Read(defaultLayouts.File);
    }

    public static LayoutHotkeys.LayoutHotkeysWrapper ReadLayoutHotkeys()
    {
        var hotkeys = new LayoutHotkeys();
        return hotkeys.Read(hotkeys.File);
    }

    public static LayoutTemplates.TemplateLayoutsListWrapper ReadTemplateLayouts()
    {
        var templates = new LayoutTemplates();
        return templates.Read(templates.File);
    }

    public static TextBox FindEditLayoutNameTextBox(UITestBase testBase, Session session)
    {
        Step(testBase, "Locating the edit-layout name field");
        return session.Find<TextBox>(By.Name("Name"));
    }

    public static int NudgeSliderAndRead(UITestBase testBase, Session session, string sliderId, Key direction, string reason)
    {
        var slider = session.Find<Element>(By.AccessibilityId(sliderId));
        Step(testBase, $"Focusing '{sliderId}' before {reason}");
        slider.Focus();
        KeyboardHelper.SendKeys(direction);
        slider = session.Find<Element>(By.AccessibilityId(sliderId));
        return ReadSliderValueAsInt(testBase, slider, sliderId);
    }

    public static int ReadSliderValueAsInt(UITestBase testBase, Element slider, string sliderId)
    {
        var rawValue = slider.GetValue();
        if (double.TryParse(rawValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var numericValue))
        {
            return (int)Math.Round(numericValue, MidpointRounding.AwayFromZero);
        }

        var description = string.Join(
            " ",
            rawValue,
            slider.GetProperty("Name"),
            slider.GetProperty("HelpText"),
            slider.GetProperty("Value.Value"));

        var numbers = Regex.Matches(description, @"-?\d+");
        Assert.IsTrue(numbers.Count > 0, $"The '{sliderId}' slider exposed no numeric value. Raw: '{description}'.");
        return int.Parse(numbers[^1].Value, CultureInfo.InvariantCulture);
    }

    public static bool SetToggleState(UITestBase testBase, Session session, string toggleId, bool expectedState)
    {
        var probe = session.Find<Element>(By.AccessibilityId(toggleId));
        var isToggleSwitch = string.Equals(probe.ClassName, "ToggleSwitch", StringComparison.OrdinalIgnoreCase)
            && string.Equals(probe.ControlType, "Button", StringComparison.OrdinalIgnoreCase);

        if (isToggleSwitch)
        {
            var typedToggle = session.Find<ToggleSwitch>(By.AccessibilityId(toggleId));
            Step(testBase, $"Setting toggle '{toggleId}' to {(expectedState ? "On" : "Off")} using ToggleSwitch wrapper");
            typedToggle.Toggle(expectedState);
            return session.Find<ToggleSwitch>(By.AccessibilityId(toggleId)).IsOn;
        }

        var currentState = string.Equals(probe.GetProperty("ToggleState"), "On", StringComparison.OrdinalIgnoreCase);
        if (currentState != expectedState)
        {
            Step(testBase, $"Setting toggle '{toggleId}' to {(expectedState ? "On" : "Off")} using untyped element fallback");
            probe.Click();
        }

        var refreshed = session.Find<Element>(By.AccessibilityId(toggleId));
        return string.Equals(refreshed.GetProperty("ToggleState"), "On", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMenuItem(Element element) =>
        string.Equals(element.ControlType, "MenuItem", StringComparison.OrdinalIgnoreCase);

    private static Session WaitForZoneEditorWindow(UITestBase testBase, HashSet<long> knownEditorWindows, string expectedEditorWindowName)
    {
        Step(testBase, "Discovering zone editor top-level window via Win32 process-window enumeration");
        var zoneEditor = WindowsFinder.WaitForWindowByApp(
            EditorProcessName,
            window => !knownEditorWindows.Contains(window.Hwnd) && window.Width >= 150 && window.Height >= 150,
            timeoutMS: 10_000,
            pollIntervalMS: 100);

        Assert.IsNotNull(
            zoneEditor,
            $"No new top-level window appeared for '{expectedEditorWindowName}' after opening zone editing.");

        Step(testBase, $"Waiting for zone editor window '{expectedEditorWindowName}' controls to become automation-ready");
        Assert.IsTrue(
            zoneEditor!.WaitForElement(By.Name(ElementName.Save), 10_000) &&
            zoneEditor.WaitForElement(By.Name(ElementName.Cancel), 10_000),
            $"The discovered zone editor window for '{expectedEditorWindowName}' did not expose Save and Cancel controls.");
        Assert.IsTrue(
            zoneEditor.WaitFor(() => ContainsExactName(zoneEditor.Inspect(depth: 2), expectedEditorWindowName), 10_000),
            $"The discovered zone editor window did not expose the expected root name '{expectedEditorWindowName}'.");

        return zoneEditor;
    }

    private static bool ContainsExactName(JsonElement element, string expectedName)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("name", out var name) &&
                string.Equals(name.GetString(), expectedName, StringComparison.Ordinal))
            {
                return true;
            }

            return element.EnumerateObject().Any(property => ContainsExactName(property.Value, expectedName));
        }

        return element.ValueKind == JsonValueKind.Array &&
               element.EnumerateArray().Any(item => ContainsExactName(item, expectedName));
    }

    private static bool CenterIsInside(Element child, Element parent)
    {
        var centerX = child.X + (child.Width / 2);
        var centerY = child.Y + (child.Height / 2);
        return centerX >= parent.X && centerX <= parent.X + parent.Width &&
               centerY >= parent.Y && centerY <= parent.Y + parent.Height;
    }
}
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
    private const string EditorMainWindowTitle = "FancyZones Editor";
    private const string LayoutOverlayTitle = "FancyZones Layout";
    private const string DeleteDialogTitle = "Are you sure?";

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
        Step(testBase, "Verifying the FancyZones Editor main window");
        var mainWindow = WindowsFinder.ListByApp(EditorProcessName)
            .FirstOrDefault(window =>
                window.Hwnd == session.WindowHandle &&
                string.Equals(window.Title, EditorMainWindowTitle, StringComparison.Ordinal) &&
                window.Width > 200 &&
                window.Height > 200);
        Assert.IsNotNull(
            mainWindow,
            $"The session HWND {session.WindowHandle} is not the '{EditorMainWindowTitle}' main window; it may be bound to the '{LayoutOverlayTitle}' overlay.");

        Step(testBase, "Waiting for the monitor list");
        Assert.IsTrue(
            WaitForFreshElement(session, By.AccessibilityId(AccessibilityId.Monitors), 60_000),
            "The editor opened but did not render its monitor list.");

        Step(testBase, "Waiting for the new-layout button");
        Assert.IsTrue(
            WaitForFreshElement(session, By.AccessibilityId(AccessibilityId.NewLayoutButton), 60_000),
            "The editor opened but did not render the new-layout button.");
    }

    public static Element FindLayoutCard(Session session, string layoutName, int timeoutMS = 30_000)
    {
        Element? card = null;
        var found = session.WaitFor(() => (card = TryFindLayoutCard(session, layoutName)) is not null, timeoutMS, 250);

        Assert.IsTrue(found && card is not null, $"Layout card '{layoutName}' was not ready for interaction.");
        return card!;
    }

    public static AppliedLayouts.AppliedLayoutsListWrapper ApplyLayoutAndWait(
        UITestBase testBase,
        Session session,
        string layoutName,
        Func<AppliedLayouts.AppliedLayoutsListWrapper, bool> expectedState,
        string expectedStateDescription)
    {
        const int attempts = 3;
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            EnsureForeground(testBase, session, $"applying layout '{layoutName}'");
            Step(testBase, $"Attempt {attempt}/{attempts}: applying layout '{layoutName}'");
            FindLayoutCard(session, layoutName).Click(msPostAction: 500, timeoutMS: 10_000);

            var result = WaitForAppliedLayouts(
                testBase,
                expectedState,
                expectedStateDescription,
                timeoutMS: 15_000,
                assertOnTimeout: false);
            if (result.HasValue)
            {
                Assert.IsTrue(
                    session.WaitFor(() => TryFindLayoutCard(session, layoutName)?.Selected == true, 10_000, 250),
                    $"Layout '{layoutName}' was persisted but its card never became selected.");
                return result.Value;
            }
        }

        Assert.Fail($"Layout '{layoutName}' was not applied after {attempts} attempts. Expected {expectedStateDescription}.");
        return default;
    }

    public static AppliedLayouts.AppliedLayoutsListWrapper WaitForAppliedLayouts(
        UITestBase testBase,
        Func<AppliedLayouts.AppliedLayoutsListWrapper, bool> expectedState,
        string expectedStateDescription,
        int timeoutMS = 30_000)
    {
        var result = WaitForAppliedLayouts(testBase, expectedState, expectedStateDescription, timeoutMS, assertOnTimeout: true);
        return result!.Value;
    }

    public static LayoutHotkeys.LayoutHotkeysWrapper WaitForLayoutHotkeys(
        UITestBase testBase,
        Func<LayoutHotkeys.LayoutHotkeysWrapper, bool> expectedState,
        string expectedStateDescription,
        int timeoutMS = 30_000)
    {
        Step(testBase, $"Waiting for layout-hotkeys.json: {expectedStateDescription}");
        var result = WaitHelper.WaitForStable(
            observe: TryReadLayoutHotkeys,
            isMatch: data => data.HasValue && expectedState(data.Value),
            timeoutMS,
            requiredConsecutiveMatches: 2,
            pollIntervalMS: 200);

        Assert.IsTrue(
            result.Succeeded && result.LastObservation.HasValue,
            $"layout-hotkeys.json did not reach the expected state: {expectedStateDescription}.");
        return result.LastObservation!.Value;
    }

    public static void EnsureForeground(UITestBase testBase, Session session, string reason)
    {
        Step(testBase, $"Bringing the editor window to foreground before {reason}");
        var foregroundStable = WindowControl.WaitForForeground(new IntPtr(session.WindowHandle), 10_000, requiredConsecutiveMatches: 3);
        Assert.IsTrue(foregroundStable, $"The editor window did not become foreground before {reason}.");
    }

    public static void SelectMonitor(UITestBase testBase, Session session, string monitorName)
    {
        const int attempts = 3;
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            EnsureForeground(testBase, session, $"selecting {monitorName}");
            var monitor = FindMonitorCard(session, monitorName);
            if (monitor.Selected)
            {
                return;
            }

            Step(testBase, $"Attempt {attempt}/{attempts}: selecting {monitorName}");
            monitor.Click(msPostAction: 500, timeoutMS: 10_000);
            if (session.WaitFor(() => TryFindMonitorCard(session, monitorName)?.Selected == true, 10_000, 250))
            {
                return;
            }
        }

        Assert.Fail($"Monitor '{monitorName}' was not selected after {attempts} attempts.");
    }

    private static Element FindMonitorCard(Session session, string monitorName, int timeoutMS = 30_000)
    {
        Element? monitor = null;
        var found = session.WaitFor(() => (monitor = TryFindMonitorCard(session, monitorName)) is not null, timeoutMS, 250);

        Assert.IsTrue(found && monitor is not null, $"Monitor card '{monitorName}' was not ready for interaction.");
        return monitor!;
    }

    private static Element? TryFindLayoutCard(Session session, string layoutName) =>
        session.FindAll<Element>(By.Name(layoutName), 0)
            .FirstOrDefault(element =>
                string.Equals(element.Name, layoutName, StringComparison.Ordinal) &&
                string.Equals(element.ControlType, "ListItem", StringComparison.OrdinalIgnoreCase));

    private static Element? TryFindMonitorCard(Session session, string monitorName) =>
        session.FindAll<Element>(By.Name(monitorName), 0)
            .FirstOrDefault(element =>
                string.Equals(element.Name, monitorName, StringComparison.Ordinal) &&
                string.Equals(element.ControlType, "ListItem", StringComparison.OrdinalIgnoreCase));

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

    public static Session ConfirmNewLayoutAndOpenEditor(UITestBase testBase, Session session, string expectedEditorWindowName)
    {
        var knownEditorWindows = WindowsFinder.ListByApp(EditorProcessName)
            .Select(window => window.Hwnd)
            .ToHashSet();

        EnsureForeground(testBase, session, $"confirming the new layout type for '{expectedEditorWindowName}'");
        Step(testBase, $"Confirming the new layout type and opening '{expectedEditorWindowName}'");
        session.Find<Button>(By.AccessibilityId(AccessibilityId.PrimaryButton)).Invoke();

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
        Assert.IsTrue(
            surface!.WaitFor(
                () => ContainsZoneEditorMarker(surface.Inspect(depth: 12, hideOffscreen: true)),
                10_000,
                100),
            $"The '{LayoutOverlayTitle}' window never entered canvas or grid edit mode.");
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
        var buttonId = confirm ? AccessibilityId.PrimaryButton : AccessibilityId.SecondaryButton;
        var buttonName = confirm ? ElementName.Delete : ElementName.Cancel;
        Element? dialogTitle = null;
        Button? responseButton = null;
        Assert.IsTrue(
            session.WaitFor(
                () =>
                {
                    dialogTitle = session.FindAll<Element>(By.Name(DeleteDialogTitle), 0)
                        .FirstOrDefault(element =>
                            string.Equals(element.Name, DeleteDialogTitle, StringComparison.Ordinal) &&
                            element.Width > 0 &&
                            element.Height > 0);
                    if (dialogTitle is null)
                    {
                        return false;
                    }

                    responseButton = session.FindAll<Button>(By.AccessibilityId(buttonId), 0)
                        .Where(button =>
                            string.Equals(button.Name, buttonName, StringComparison.Ordinal) &&
                            button.IsEnabled &&
                            button.Width > 0 &&
                            button.Height > 0)
                        .OrderBy(button => Math.Abs(button.Y - dialogTitle.Y))
                        .FirstOrDefault();
                    return responseButton is not null;
                },
                10_000,
                100),
            "Delete confirmation dialog did not expose an actionable response button.");

        var action = confirm ? "Confirming layout deletion" : "Cancelling layout deletion";
        Step(
            testBase,
            $"{action} with '{responseButton!.Name}' at ({responseButton.X},{responseButton.Y}) {responseButton.Width}x{responseButton.Height}, selector '{responseButton.Selector}'");

        var before = ReadCustomLayouts();
        var beforeHotkeys = TryReadLayoutHotkeys();
        var beforeIds = before.CustomLayouts
            .Select(layout => layout.Uuid)
            .OrderBy(uuid => uuid, StringComparer.Ordinal)
            .ToArray();
        responseButton.Click();

        Assert.IsTrue(
            session.WaitFor(
                () => session.FindAll<Element>(By.Name(DeleteDialogTitle), 0)
                    .All(element => !string.Equals(element.Name, DeleteDialogTitle, StringComparison.Ordinal)),
                10_000,
                100),
            "Delete confirmation dialog did not close after the response.");

        var expectedState = WaitHelper.WaitForStable(
            observe: () =>
            {
                try
                {
                    return (CustomLayouts.CustomLayoutListWrapper?)ReadCustomLayouts();
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
                {
                    return null;
                }
            },
            isMatch: current =>
            {
                if (!current.HasValue)
                {
                    return false;
                }

                if (confirm)
                {
                    return current.Value.CustomLayouts.Count == before.CustomLayouts.Count - 1;
                }

                var currentIds = current.Value.CustomLayouts
                    .Select(layout => layout.Uuid)
                    .OrderBy(uuid => uuid, StringComparer.Ordinal);
                return currentIds.SequenceEqual(beforeIds, StringComparer.Ordinal);
            },
            timeoutMS: 10_000,
            requiredConsecutiveMatches: 2,
            pollIntervalMS: 100);
        Assert.IsTrue(
            expectedState.Succeeded,
            confirm
                ? "The custom-layout collection did not shrink after confirming deletion."
                : "The custom-layout collection changed after cancelling deletion.");

        if (beforeHotkeys.HasValue && expectedState.LastObservation.HasValue)
        {
            var remainingLayoutIds = expectedState.LastObservation.Value.CustomLayouts
                .Select(layout => layout.Uuid)
                .ToHashSet(StringComparer.Ordinal);
            var expectedHotkeys = beforeHotkeys.Value.LayoutHotkeys
                .Where(hotkey => remainingLayoutIds.Contains(hotkey.LayoutId))
                .OrderBy(hotkey => hotkey.LayoutId, StringComparer.Ordinal)
                .ThenBy(hotkey => hotkey.Key)
                .Select(hotkey => (hotkey.LayoutId, hotkey.Key))
                .ToArray();
            var hotkeyState = WaitHelper.WaitForStable(
                observe: TryReadLayoutHotkeys,
                isMatch: current => current.HasValue && current.Value.LayoutHotkeys
                    .OrderBy(hotkey => hotkey.LayoutId, StringComparer.Ordinal)
                    .ThenBy(hotkey => hotkey.Key)
                    .Select(hotkey => (hotkey.LayoutId, hotkey.Key))
                    .SequenceEqual(expectedHotkeys),
                timeoutMS: 10_000,
                requiredConsecutiveMatches: 2,
                pollIntervalMS: 100);
            Assert.IsTrue(
                hotkeyState.Succeeded,
                confirm
                    ? "Layout hotkeys were not updated after confirming deletion."
                    : "Layout hotkeys changed after cancelling deletion.");
        }
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

    private static bool WaitForFreshElement(Session session, By selector, int timeoutMS) =>
        session.WaitFor(() => session.FindAll<Element>(selector, 0).Count > 0, timeoutMS, 250);

    private static AppliedLayouts.AppliedLayoutsListWrapper? WaitForAppliedLayouts(
        UITestBase testBase,
        Func<AppliedLayouts.AppliedLayoutsListWrapper, bool> expectedState,
        string expectedStateDescription,
        int timeoutMS,
        bool assertOnTimeout)
    {
        Step(testBase, $"Waiting for applied-layouts.json: {expectedStateDescription}");
        var result = WaitHelper.WaitForStable(
            observe: TryReadAppliedLayouts,
            isMatch: data => data.HasValue && expectedState(data.Value),
            timeoutMS,
            requiredConsecutiveMatches: 2,
            pollIntervalMS: 200);

        if (assertOnTimeout)
        {
            Assert.IsTrue(
                result.Succeeded && result.LastObservation.HasValue,
                $"applied-layouts.json did not reach the expected state: {expectedStateDescription}.");
        }

        return result.Succeeded ? result.LastObservation : null;
    }

    private static AppliedLayouts.AppliedLayoutsListWrapper? TryReadAppliedLayouts()
    {
        try
        {
            return ReadAppliedLayouts();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    private static LayoutHotkeys.LayoutHotkeysWrapper? TryReadLayoutHotkeys()
    {
        try
        {
            return ReadLayoutHotkeys();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    public static TextBox FindEditLayoutNameTextBox(UITestBase testBase, Session session)
    {
        Step(testBase, "Locating the edit-layout name field");
        return session.Find<TextBox>(By.Name("Name"));
    }

    public static int NudgeSliderAndRead(UITestBase testBase, Session session, string sliderId, Key direction, string reason)
    {
        var slider = session.Find<Element>(By.AccessibilityId(sliderId));
        var initialValue = ReadSliderValueAsInt(testBase, slider, sliderId);
        Step(testBase, $"Focusing '{sliderId}' before {reason}");
        slider.Focus();
        KeyboardHelper.SendKeys(direction);

        var changed = WaitHelper.WaitForStable(
            observe: () => ReadSliderValueAsInt(
                testBase,
                session.Find<Element>(By.AccessibilityId(sliderId)),
                sliderId),
            isMatch: value => value != initialValue,
            timeoutMS: 5000,
            requiredConsecutiveMatches: 2,
            pollIntervalMS: 100);
        Assert.IsTrue(changed.Succeeded, $"The '{sliderId}' slider did not change from {initialValue} while {reason}.");

        Step(testBase, $"Moving focus from '{sliderId}' to Save after {reason}");
        session.Find<Button>(ElementName.Save).Focus();

        var committed = WaitHelper.WaitForStable(
            observe: () => ReadSliderValueAsInt(
                testBase,
                session.Find<Element>(By.AccessibilityId(sliderId)),
                sliderId),
            isMatch: value => value == changed.LastObservation,
            timeoutMS: 5000,
            requiredConsecutiveMatches: 2,
            pollIntervalMS: 100);
        Assert.IsTrue(committed.Succeeded, $"The '{sliderId}' slider did not remain at {changed.LastObservation} after focus changed.");
        return committed.LastObservation;
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
            window =>
                !knownEditorWindows.Contains(window.Hwnd) &&
                !string.Equals(window.Title, LayoutOverlayTitle, StringComparison.Ordinal) &&
                window.Width >= 150 &&
                window.Height >= 150,
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

    private static bool ContainsZoneEditorMarker(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("className", out var className) &&
                (string.Equals(className.GetString(), ClassName.CanvasZone, StringComparison.Ordinal) ||
                 string.Equals(className.GetString(), ClassName.GridZone, StringComparison.Ordinal)))
            {
                return true;
            }

            return element.EnumerateObject().Any(property => ContainsZoneEditorMarker(property.Value));
        }

        return element.ValueKind == JsonValueKind.Array &&
               element.EnumerateArray().Any(ContainsZoneEditorMarker);
    }

    private static bool CenterIsInside(Element child, Element parent)
    {
        var centerX = child.X + (child.Width / 2);
        var centerY = child.Y + (child.Height / 2);
        return centerX >= parent.X && centerX <= parent.X + parent.Width &&
               centerY >= parent.Y && centerY <= parent.Y + parent.Height;
    }
}
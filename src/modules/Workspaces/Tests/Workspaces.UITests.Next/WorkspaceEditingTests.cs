// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Workspaces.UITests
{
    public sealed partial class WorkspacesTests
    {
        [TestMethod]
        public void SearchSuggestsWorkspacesAndApplicationsAndOpensSelection()
        {
            var projects = SeedSearchWorkspaces();
            var names = projects.Select(project => WorkspaceTestState.Text(project, "name")).ToArray();
            var original = File.ReadAllBytes(WorkspaceTestState.WorkspacesPath);
            OpenEditor();
            AssertWorkspaceNames(names);

            // Correct a typo through the keyboard so the AutoSuggestBox receives a real text edit.
            Search("aTlAsx");
            var searchInput = SearchInput();
            Editor.EnsureForeground();
            searchInput.Focus();
            Assert.IsTrue(searchInput.WaitForProperty("HasKeyboardFocus", "True", 5_000), "The search input did not receive keyboard focus.");
            KeyboardHelper.SendChord(Key.End);
            KeyboardHelper.SendChord(Key.Backspace);
            Assert.IsTrue(searchInput.WaitForValue("aTlAs"), "Correcting the query should update the search input.");
            AssertWorkspaceNames(names);
            WaitForSearchSuggestions(names[0]);
            KeyboardHelper.SendChord(Key.Down);
            Assert.IsTrue(searchInput.WaitForValue(names[0]), $"Down should fill the selected workspace name. Observed '{searchInput.Value}'.");
            KeyboardHelper.SendChord(Key.Enter);
            Assert.IsTrue(
                Editor.Find<TextBox>(By.AccessibilityId("EditNameTextBox"), 15_000).WaitForValue(names[0], timeoutMS: 10_000),
                "Enter should open the selected workspace for editing.");
            CancelEditing();

            Search("editor fixture");
            AssertWorkspaceNames(names);
            var appMatches = WaitForSearchSuggestions(names[1], names[2]);
            Assert.IsTrue(appMatches[0].Y < appMatches[1].Y, "The two matching suggestions should occupy distinct rows.");
            ChooseSearchSuggestion(appMatches[1].Name);
            CancelEditing();

            Search("no matching workspace or application");
            WaitForSearchSuggestions();
            SearchInput().Focus();
            KeyboardHelper.SendKeys(Key.Enter);
            AssertWorkspaceListReady();
            AssertWorkspaceNames(names);

            Search("editor fixture");
            var inputToClear = SearchInput();
            Assert.IsTrue(inputToClear.WaitForProperty("HasKeyboardFocus", "True", 5_000), "The search input did not receive keyboard focus before clearing.");
            WaitForSearchSuggestions(names[1], names[2]);
            KeyboardHelper.SendChord(Key.Ctrl, Key.A);
            KeyboardHelper.SendChord(Key.Backspace);
            Assert.IsTrue(inputToClear.WaitForValue(string.Empty), "Clearing the query should empty the search input.");
            WaitForSearchSuggestions();
            AssertWorkspaceNames(names);
            CollectionAssert.AreEqual(original, File.ReadAllBytes(WorkspaceTestState.WorkspacesPath), "Searching or opening a suggestion changed saved workspaces.");
        }

        private TextBlock[] WaitForSearchSuggestions(params string[] expected)
        {
            var searchBox = Editor.Find<Element>(By.AccessibilityId("SearchBox"), 15_000);
            var expectedNames = expected.Order(StringComparer.Ordinal).ToArray();

            // WinUI exposes the same popup under both PopupHost and its AutoSuggestBox.
            // Inspect one subtree so each list item contributes its own name, including duplicates.
            var ready = WaitHelper.WaitForStable(
                () => FindDescendants<TextBlock>(searchBox, "Text", "WorkspaceSearchSuggestionName")
                    .Where(text => text.Displayed).OrderBy(text => text.Y).ToArray(),
                suggestions => suggestions is not null && suggestions.All(text => text.Width > 0 && text.Height > 0) &&
                    suggestions.Select(text => text.Name).Order(StringComparer.Ordinal).SequenceEqual(expectedNames),
                15_000,
                requiredConsecutiveMatches: 2);
            var observed = ready.LastObservation ?? [];
            Assert.IsTrue(ready.Succeeded, $"Expected suggestions [{string.Join(", ", expected)}], observed [{string.Join(", ", observed.Select(text => text.Name))}].");
            return observed;
        }

        private void ChooseSearchSuggestion(string name)
        {
            string process = Editor.ProcessId.ToString(CultureInfo.InvariantCulture);
            (long Handle, string Selector)? FindSuggestion()
            {
                var tree = WinappCli.InvokeJson("ui", "inspect", "-a", process, "--json", "-d", "20");
                var visibleWindows = WindowControl.EnumerateProcessWindows([Editor.ProcessId])
                    .Where(window => window.IsVisible).Select(window => window.Hwnd.ToInt64()).ToHashSet();
                var items = WindowRoots(tree)
                    .Where(window => visibleWindows.Contains(window.Handle))
                    .SelectMany(window => Subtree(window.Element)
                        .Where(item => UiString(item, "type") == "ListItem" && IsVisible(item) &&
                            Subtree(item).Any(child => UiString(child, "automationId") == "WorkspaceSearchSuggestionName" && UiString(child, "name") == name))
                        .Select(item => (window.Handle, Selector: UiString(item, "selector"))))
                    .Where(item => !string.IsNullOrEmpty(item.Selector))
                    .Distinct()
                    .ToArray();

                // A popup can also be exposed as part of the editor. Prefer its owning HWND.
                var popupItems = items.Where(item => item.Handle != Editor.WindowHandle).ToArray();
                var candidates = popupItems.Length > 0 ? popupItems : items;
                return candidates is [var match] ? (match.Handle, match.Selector!) : null;
            }

            var ready = WaitHelper.WaitForStable(FindSuggestion, match => match.HasValue, 15_000, shouldRetryException: ShellMenu.IsTransientElementException);
            Assert.IsTrue(ready.Succeeded, $"Could not identify the '{name}' suggestion. {ready.LastException?.Message}");
            var (handle, selector) = ready.LastObservation!.Value;
            WinappCli.InvokeAssertSuccess("ui", "invoke", selector, "-w", handle.ToString(CultureInfo.InvariantCulture), "--action", "invoke", "--json");
            Assert.IsTrue(
                Editor.Find<TextBox>(By.AccessibilityId("EditNameTextBox"), 15_000).WaitForValue(name, timeoutMS: 10_000),
                $"Choosing the '{name}' suggestion did not open that workspace.");
        }

        [TestMethod]
        [DataRow("Last launched", 0)]
        [DataRow("Created", 1)]
        [DataRow("Name", 2)]
        public void SortOrderPersistsAcrossEditorRestart(string order, int index)
        {
            var projects = SeedSearchWorkspaces();
            var names = projects.Select(project => WorkspaceTestState.Text(project, "name")).ToArray();
            string[] expected = index switch
            {
                0 => [names[1], names[2], names[0]],
                1 => [names[2], names[0], names[1]],
                2 => names.Order(StringComparer.Ordinal).ToArray(),
                _ => throw new ArgumentOutOfRangeException(nameof(index)),
            };
            OpenEditor();
            if (StoredSortIndex() == index)
            {
                SelectWorkspaceSort((index + 1) % 3);
            }

            SelectWorkspaceSort(index);
            AssertWorkspaceOrder(expected);
            CloseEditor();
            OpenEditor();
            AssertWorkspaceOrder(expected);
            AssertWorkspaceSortSelected(index, order);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void WorkspaceCardOpensEditingPageWithMouseAndKeyboard(bool useKeyboard)
        {
            var project = SeedOneWorkspace();
            var original = File.ReadAllBytes(WorkspaceTestState.WorkspacesPath);
            OpenEditor();
            EditWorkspace(WorkspaceTestState.Text(project, "name"), useKeyboard);
            Assert.IsTrue(Editor.Find<Button>(By.AccessibilityId("SaveButton")).IsEnabled);
            Assert.IsTrue(Editor.Find<Button>(By.AccessibilityId("PART_BackButton")).IsEnabled);
            CollectionAssert.AreEqual(original, File.ReadAllBytes(WorkspaceTestState.WorkspacesPath), "Opening the editing page changed stored data.");
        }

        [TestMethod]
        public void MouseOpensWorkspaceInitiallyOutsideTheListViewport()
        {
            var projects = Enumerable.Range(1, 16).Select(index =>
            {
                string rank = index.ToString("D2", CultureInfo.InvariantCulture);
                return State.Project($"viewport-{rank}", State.Application($"Viewport fixture {rank}", $"{State.Prefix}-{rank}"));
            }).ToArray();
            State.WriteProjects([.. projects.Reverse()]);
            string firstId = WorkspaceTestState.Text(projects[0], "id");
            string targetId = WorkspaceTestState.Text(projects[^1], "id");
            string targetName = WorkspaceTestState.Text(projects[^1], "name");
            string targetApplication = WorkspaceTestState.Text(projects[^1]["applications"]!.AsArray().Single()!, "application");
            OpenEditor();
            using (new PhysicalCoordinateScope())
            {
                var handle = new IntPtr(Editor.WindowHandle);
                var monitor = MonitorInfo.GetFromWindow(handle);
                var dpi = NativeMethods.GetDpiForWindow(handle);
                Assert.IsTrue(dpi > 0, "The editor window should report its display DPI.");
                double scale = dpi / 96.0;
                int height = (int)Math.Ceiling(720 * scale);
                if (monitor.WorkHeight > height)
                {
                    WindowHelper.RestoreWindow(handle);
                    WindowHelper.MoveWindow(handle, monitor.WorkLeft, monitor.WorkTop);
                    WindowHelper.SetMainWindowSize(handle, Math.Min(monitor.WorkWidth, (int)Math.Ceiling(900 * scale)), height);
                }
            }

            SelectWorkspaceSort(2);
            var initial = WaitHelper.WaitForStable(
                () => WorkspaceCards(out var viewport) is { } cards && viewport is not null &&
                    cards.Any(card => card.Id == firstId && card.VisibleEditArea.Width > 0 && card.VisibleEditArea.Height > 0) &&
                    !cards.Any(card => card.Id == targetId && card.VisibleEditArea.Width > 0 && card.VisibleEditArea.Height > 0),
                targetOutside => targetOutside,
                15_000,
                requiredConsecutiveMatches: 2);
            Assert.IsTrue(initial.Succeeded, $"The list should show its first workspace and require scrolling to reach '{targetName}'.");

            EditWorkspace(targetName, useKeyboard: false);
            Assert.IsTrue(Editor.Has(By.AccessibilityId(targetApplication), 10_000), "The mouse should open the last workspace and its application.");
        }

        [TestMethod]
        public void DeleteRemovesOnlyTheSelectedWorkspace()
        {
            var projects = SeedSearchWorkspaces();
            var names = projects.Select(project => WorkspaceTestState.Text(project, "name")).ToArray();
            OpenEditor();
            AssertWorkspaceNames(names);
            EditWorkspace(names[1]);
            Step("Deleting the selected workspace.");
            ConfirmWorkspaceDeletion();
            AssertWorkspaceNames(names[0], names[2]);
            CollectionAssert.AreEquivalent(
                new[] { WorkspaceTestState.Text(projects[0], "id"), WorkspaceTestState.Text(projects[2], "id") },
                WorkspaceTestState.ReadProjects().Select(project => WorkspaceTestState.Text(project!, "id")).ToArray(),
                "Deleting a workspace removed the wrong stored project.");
        }

        [TestMethod]
        public void NameArgumentsAdminAndGeometryEditsPersist()
        {
            var project = SeedOneWorkspace();
            var id = WorkspaceTestState.Text(project, "id");
            var newName = State.Prefix + "-renamed";
            var arguments = $"--title \"{State.Prefix}-edited\" --payload \"argument with spaces\"";
            State.TrackShortcut(newName);
            OpenEditor();
            EditWorkspace(WorkspaceTestState.Text(project, "name"));
            var row = ExpandApplication("Primary fixture");
            Step("Editing application arguments, administrator preference, and all four position fields.");
            row.Find<TextBox>(By.AccessibilityId("CommandLineTextBox")).SetText(arguments);
            SetCheck(row.Find<CheckBox>(By.AccessibilityId("LaunchAsAdminCheckBox")), true);
            using var before = PreviewSnapshot.Capture(TestContext, Editor, "before-position");
            row.Find<TextBox>(By.AccessibilityId("LeftTextBox")).SetText("420");
            row.Find<TextBox>(By.AccessibilityId("TopTextBox")).SetText("310");
            row.Find<TextBox>(By.AccessibilityId("WidthTextBox")).SetText("810");
            row.Find<TextBox>(By.AccessibilityId("HeightTextBox")).SetText("510");
            PreviewSnapshot.AssertChanged(TestContext, Editor, before);
            Editor.Find<TextBox>(By.AccessibilityId("EditNameTextBox")).SetText(newName);
            SaveWorkspace();
            var saved = WorkspaceTestState.WaitForProject(id, value => WorkspaceTestState.Text(value, "name") == newName);
            var app = saved["applications"]!.AsArray().Single()!;
            Assert.AreEqual(arguments, WorkspaceTestState.Text(app, "command-line-arguments"));
            Assert.IsTrue(app["is-elevated"]!.GetValue<bool>(), "The administrator launch preference was not saved.");
            AssertPosition(app["position"]!, 420, 310, 810, 510);
            AssertWorkspaceNames(newName);
            EditWorkspace(newName);
            row = ExpandApplication("Primary fixture");
            Assert.AreEqual(arguments, row.Find<TextBox>(By.AccessibilityId("CommandLineTextBox")).Value);
            Assert.IsTrue(row.Find<CheckBox>(By.AccessibilityId("LaunchAsAdminCheckBox")).IsChecked);
            Assert.AreEqual("420", row.Find<TextBox>(By.AccessibilityId("LeftTextBox")).Value);
            Assert.AreEqual("310", row.Find<TextBox>(By.AccessibilityId("TopTextBox")).Value);
            Assert.AreEqual("810", row.Find<TextBox>(By.AccessibilityId("WidthTextBox")).Value);
            Assert.AreEqual("510", row.Find<TextBox>(By.AccessibilityId("HeightTextBox")).Value);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void BackAndEscapeDiscardUnsavedEdits(bool useBackButton)
        {
            var project = SeedOneWorkspace();
            var name = WorkspaceTestState.Text(project, "name");
            var original = File.ReadAllBytes(WorkspaceTestState.WorkspacesPath);
            OpenEditor();
            EditWorkspace(name);
            var row = ExpandApplication("Primary fixture");
            row.Find<TextBox>(By.AccessibilityId("LeftTextBox")).SetText("600");
            row.Find<TextBox>(By.AccessibilityId("CommandLineTextBox")).SetText("--payload discarded");
            Editor.Find<TextBox>(By.AccessibilityId("EditNameTextBox")).SetText(State.Prefix + "-discarded");
            CancelEditing(useBackButton);
            CollectionAssert.AreEqual(original, File.ReadAllBytes(WorkspaceTestState.WorkspacesPath), "Discarding edits changed stored workspace data.");
            AssertWorkspaceNames(name);
            EditWorkspace(name);
            row = ExpandApplication("Primary fixture");
            Assert.AreEqual(
                project["applications"]!.AsArray()[0]!["position"]!["X"]!.GetValue<int>().ToString(System.Globalization.CultureInfo.InvariantCulture),
                row.Find<TextBox>(By.AccessibilityId("LeftTextBox")).Value);
            Assert.AreEqual(
                WorkspaceTestState.Text(project["applications"]!.AsArray()[0]!, "command-line-arguments"),
                row.Find<TextBox>(By.AccessibilityId("CommandLineTextBox")).Value);
        }

        [TestMethod]
        public void RemoveAndAddBackUpdatePreviewAndSavedApplicationList()
        {
            var first = State.Application("Primary fixture", State.Prefix + "-first");
            var second = State.Application("Secondary fixture", State.Prefix + "-second");
            second["position"] = WorkspaceTestState.Position(1080, 550, 560, 390);
            var project = State.Project("applications", first, second);
            State.WriteProjects(project);
            OpenEditor();
            EditWorkspace(WorkspaceTestState.Text(project, "name"));
            using var before = PreviewSnapshot.Capture(TestContext, Editor, "both-applications");
            ToggleApplicationInclusion("Primary fixture", include: false);
            Assert.IsFalse(Editor.Find(By.AccessibilityId("Primary fixture")).IsEnabled, "The removed application remained editable.");
            PreviewSnapshot.AssertChanged(TestContext, Editor, before);

            var save = Editor.Find<Button>(By.AccessibilityId("SaveButton"));
            ToggleApplicationInclusion("Secondary fixture", include: false);
            Assert.IsTrue(save.WaitForProperty("IsEnabled", "False", 5_000), "Save should be disabled when every application is excluded.");
            ToggleApplicationInclusion("Secondary fixture", include: true);
            Assert.IsTrue(save.WaitForProperty("IsEnabled", "True", 5_000), "Adding an application back should re-enable Save.");

            ToggleApplicationInclusion("Primary fixture", include: true);
            Assert.IsTrue(Editor.Find(By.AccessibilityId("Primary fixture")).IsEnabled, "Add back did not restore the application.");
            PreviewSnapshot.AssertRestored(TestContext, Editor, before);
            ToggleApplicationInclusion("Primary fixture", include: false);
            SaveWorkspace();
            var saved = WorkspaceTestState.WaitForProject(
                WorkspaceTestState.Text(project, "id"),
                value => value["applications"]!.AsArray().Count == 1);
            Assert.AreEqual("Secondary fixture", WorkspaceTestState.Text(saved["applications"]!.AsArray().Single()!, "application"));
            EditWorkspace(WorkspaceTestState.Text(project, "name"));
            Assert.IsFalse(Editor.Has(By.AccessibilityId("Primary fixture"), 500), "The excluded application reappeared after reopening.");
            Assert.IsTrue(Editor.Has(By.AccessibilityId("Secondary fixture"), 5_000));
        }

        [TestMethod]
        [DataRow("Minimized", true, false)]
        [DataRow("Maximized", false, true)]
        public void WindowStateChangesUpdatePreviewAndPersist(string selected, bool minimized, bool maximized)
        {
            var project = SeedOneWorkspace();
            OpenEditor();
            EditWorkspace(WorkspaceTestState.Text(project, "name"));
            var row = ExpandApplication("Primary fixture");
            AssertCustomCoordinatesAvailable(row, expected: true);
            using var before = PreviewSnapshot.Capture(TestContext, Editor, "custom-position");
            SelectWindowPosition(row, selected);
            AssertCustomCoordinatesAvailable(row, expected: false);
            PreviewSnapshot.AssertChanged(TestContext, Editor, before);
            SaveWorkspace();
            var saved = WorkspaceTestState.ReadProject(WorkspaceTestState.Text(project, "id"));
            var app = saved["applications"]!.AsArray().Single()!;
            Assert.AreEqual(minimized, app["minimized"]!.GetValue<bool>());
            Assert.AreEqual(maximized, app["maximized"]!.GetValue<bool>());
            EditWorkspace(WorkspaceTestState.Text(project, "name"));
            row = ExpandApplication("Primary fixture");
            Assert.AreEqual(selected, row.Find<ComboBox>(By.AccessibilityId("WindowPositionComboBox")).SelectedText);
            SelectWindowPosition(row, "Custom");
            AssertCustomCoordinatesAvailable(row, expected: true);
            PreviewSnapshot.AssertRestored(TestContext, Editor, before);
        }

        private JsonObject SeedOneWorkspace()
        {
            var app = State.Application("Primary fixture", State.Prefix + "-app");
            var project = State.Project("workspace", app);
            State.WriteProjects(project);
            return project;
        }

        private JsonObject[] SeedSearchWorkspaces()
        {
            var atlas = State.Project("Atlas", State.Application("Browser fixture", "browser"));
            var zenith = State.Project("Zenith", State.Application("Editor fixture", "editor"));
            var mesa = State.Project("Mesa", State.Application("Editor fixture", "editor-two"));
            atlas["creation-time"] = 2_000;
            zenith["creation-time"] = 1_000;
            mesa["creation-time"] = 3_000;
            atlas["last-launched-time"] = 1_000;
            zenith["last-launched-time"] = 3_000;
            mesa["last-launched-time"] = 2_000;
            State.WriteProjects(atlas, zenith, mesa);
            return [atlas, zenith, mesa];
        }

        private void AssertWorkspaceOrder(string[] expected)
        {
            var result = WaitHelper.WaitForStable(WorkspaceNames, actual => actual is not null && actual.SequenceEqual(expected), 15_000, requiredConsecutiveMatches: 2);
            Assert.IsTrue(result.Succeeded, $"Expected order [{string.Join(", ", expected)}], observed [{string.Join(", ", result.LastObservation ?? [])}].");
        }

        private void SelectWorkspaceSort(int index)
        {
            if (StoredSortIndex() == index)
            {
                return;
            }

            Step($"Selecting workspace sort order {index}.");
            string itemId = SortItemId(index);
            WinappCli.Result ToggleItem()
            {
                var (window, item) = OpenWorkspaceSortItem(itemId);
                Assert.AreEqual("off", UiString(item, "toggleState"), $"'{itemId}' should be unchecked before changing the sort order.");
                return WinappCli.Invoke("ui", "invoke", itemId, "-w", window, "--action", "toggle", "--json");
            }

            var activation = ToggleItem();
            if (!activation.Success && ItemWasNotFound(activation, itemId))
            {
                // winappcli reports element_not_found before dispatching the requested action.
                // The flyout may have closed between inspection and invocation.
                activation = ToggleItem();
            }

            Assert.IsTrue(activation.Success, activation.DescribeFailure());
            var saved = WaitHelper.WaitForStable(
                StoredSortIndex,
                actual => actual == index,
                15_000,
                shouldRetryException: error => error is IOException or JsonException);
            Assert.IsTrue(saved.Succeeded, $"Selecting '{itemId}' did not persist sort index {index}. Observed {saved.LastObservation}. {saved.LastException?.Message}");

            static bool ItemWasNotFound(WinappCli.Result failure, string expectedId)
            {
                try
                {
                    return JsonNode.Parse(failure.StdErr) is JsonObject response &&
                        response["error"] is JsonObject error &&
                        (string?)error["code"] == "element_not_found" && (string?)error["selector"] == expectedId;
                }
                catch (JsonException)
                {
                    return false;
                }
            }
        }

        private static int StoredSortIndex() => WorkspaceTestState.ReadObject(WorkspaceTestState.SettingsPath)["properties"]!["sortby"]!.GetValue<int>();

        private void AssertWorkspaceSortSelected(int index, string expectedName)
        {
            var (_, item) = OpenWorkspaceSortItem(SortItemId(index));
            Assert.AreEqual(expectedName, UiString(item, "name"));
            Assert.AreEqual("on", UiString(item, "toggleState"), "The sort menu did not retain its selected option after restart.");
        }

        private (string Window, JsonElement Item) OpenWorkspaceSortItem(string itemId)
        {
            string process = Editor.ProcessId.ToString(CultureInfo.InvariantCulture);
            (string Window, JsonElement Item)? FindOpenItem()
            {
                // A MenuFlyout may be hosted by its own HWND. Inspect the editor process
                // and use the owning window of the visible item for subsequent actions.
                var tree = WinappCli.InvokeJson("ui", "inspect", "-a", process, "--json", "-d", "12");
                foreach (var (handle, root) in WindowRoots(tree))
                {
                    foreach (var item in Subtree(root))
                    {
                        if (UiString(item, "type") == "MenuItem" && UiString(item, "automationId") == itemId && IsVisible(item))
                        {
                            return (handle.ToString(CultureInfo.InvariantCulture), item);
                        }
                    }
                }

                return null;
            }

            if (FindOpenItem() is { } openItem)
            {
                return openItem;
            }

            Assert.IsTrue(
                WindowControl.WaitForForeground(new IntPtr(Editor.WindowHandle), 10_000, requiredConsecutiveMatches: 2),
                $"The editor did not acquire foreground to open the sort menu. Foreground: {WindowControl.GetForegroundWindowInfo()}.");
            WinappCli.InvokeAssertSuccess("ui", "invoke", "WorkspaceSortComboBox", "-w", Editor.WindowHandleArg, "--action", "invoke");
            var ready = WaitHelper.WaitForStable(FindOpenItem, hit => hit.HasValue, 10_000, shouldRetryException: IsStaleElement);
            Assert.IsTrue(ready.Succeeded, $"The editor did not show sort option '{itemId}'. Foreground: {WindowControl.GetForegroundWindowInfo()}.");
            return ready.LastObservation!.Value;
        }

        private static string SortItemId(int index) => index switch
        {
            0 => "SortLastLaunchedItem",
            1 => "SortCreatedItem",
            2 => "SortNameItem",
            _ => throw new ArgumentOutOfRangeException(nameof(index)),
        };

        private void AssertCustomCoordinatesAvailable(Element row, bool expected)
        {
            // WinUI removes a collapsed SettingsCard's children from UIA. A text box scrolled
            // out of view stays in the tree and can still accept keyboard input.
            var ready = WaitHelper.WaitForStable(
                () => FindDescendants<TextBox>(row, "Edit", "LeftTextBox"),
                fields => expected
                    ? fields is [var input] &&
                        string.Equals(input.GetProperty("IsKeyboardFocusable"), bool.TrueString, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(input.GetProperty("IsReadOnly"), bool.FalseString, StringComparison.OrdinalIgnoreCase)
                    : fields is { Length: 0 },
                10_000,
                requiredConsecutiveMatches: 2);
            string message = expected
                ? "Custom window coordinates did not become editable."
                : "Custom coordinates remained in the automation tree for a minimized/maximized app.";
            Assert.IsTrue(ready.Succeeded, message);
        }

        private void SelectWindowPosition(Element row, string item)
        {
            const string comboId = "WindowPositionComboBox";
            ComboBox FindPositionControl()
            {
                var controls = FindDescendants<ComboBox>(row, "ComboBox", comboId).DistinctBy(control => control.Selector).ToArray();
                Assert.HasCount(1, controls, "The application row should expose one window-position control.");
                return controls[0];
            }

            Step($"Selecting '{item}'.");
            var combo = FindPositionControl();
            if (combo.SelectedText == item)
            {
                return;
            }

            string itemId = item switch
            {
                "Custom" => "WindowPositionCustomItem",
                "Maximized" => "WindowPositionMaximizedItem",
                "Minimized" => "WindowPositionMinimizedItem",
                _ => throw new ArgumentOutOfRangeException(nameof(item), item, "Unknown window position."),
            };
            WinappCli.InvokeAssertSuccess("ui", "invoke", combo.Selector, "-w", Editor.WindowHandleArg, "--action", "expand", "--json");

            // WinUI exposes the same ComboBoxItem through the control and the PopupHost. Explicit
            // --action select rejects even those identical aliases. Check the underlying identity
            // before invoking the stable AutomationId in this HWND; the CLI then resolves it afresh.
            bool HasMatchingOption()
            {
                var nodes = EditorRoots(Editor.Inspect(depth: 20)).SelectMany(Subtree).ToArray();
                var options = nodes.Where(node => UiString(node, "automationId") == itemId).ToArray();
                if (options.Length == 0 || !options.All(option => UiString(option, "type") == "ListItem" &&
                    UiString(option, "className") == "ComboBoxItem" && UiString(option, "name") == item && IsVisible(option)))
                {
                    return false;
                }

                var selector = UiString(options[0], "selector");
                return !string.IsNullOrEmpty(selector) && options.All(option => UiString(option, "selector") == selector) &&
                    nodes.Where(node => UiString(node, "automationId") == comboId && UiString(node, "expandState") == "expanded")
                        .SelectMany(Subtree).Any(option => UiString(option, "selector") == selector);
            }

            var ready = WaitHelper.WaitForStable(
                HasMatchingOption,
                visible => visible,
                10_000,
                shouldRetryException: ShellMenu.IsTransientElementException);
            Assert.IsTrue(ready.Succeeded, $"The expanded window-position ComboBox did not expose '{item}' as '{itemId}'.");
            WinappCli.InvokeAssertSuccess("ui", "invoke", itemId, "-w", Editor.WindowHandleArg, "--json");
            combo = FindPositionControl();
            if (string.Equals(combo.GetProperty("ExpandCollapseState"), "Expanded", StringComparison.OrdinalIgnoreCase))
            {
                WinappCli.InvokeAssertSuccess("ui", "invoke", combo.Selector, "-w", Editor.WindowHandleArg, "--action", "collapse", "--json");
            }

            Assert.IsTrue(combo.WaitForValue(item, timeoutMS: 10_000), $"The selection did not change to '{item}'.");
        }

        private void SetCheck(CheckBox checkBox, bool value)
        {
            Step($"Setting '{checkBox.Name}' to {value}.");
            Assert.IsTrue(checkBox.IsEnabled, $"'{checkBox.Name}' is not available.");
            if (checkBox.IsChecked != value)
            {
                checkBox.Invoke(msPostAction: 0);
            }

            Assert.IsTrue(checkBox.WaitForProperty("ToggleState", value ? "On" : "Off", 10_000));
        }

        private void ToggleApplicationInclusion(string name, bool include)
        {
            var action = include ? "Add back" : "Remove";
            Step($"{action}: '{name}'.");
            var ready = WaitHelper.WaitForStable(
                () => Editor.FindAll<Button>(By.AccessibilityId("ToggleApplicationInclusionButton"), 0)
                    .Where(button => button.Name == $"{action} {name}").ToArray(),
                buttons => buttons is { Length: 1 },
                10_000,
                requiredConsecutiveMatches: 2);
            Assert.IsTrue(ready.Succeeded, $"The '{action}' action for '{name}' was not uniquely addressable.");
            var button = ready.LastObservation![0];
            Assert.IsTrue(button.IsEnabled, $"The '{action}' action for '{name}' is disabled.");
            button.Invoke(msPostAction: 0);
            Assert.IsTrue(
                Editor.Find<Element>(By.AccessibilityId(name)).WaitForProperty("IsEnabled", include ? "True" : "False", 10_000),
                $"The application inclusion state did not change after '{action}'.");
        }

        private void CancelEditing(bool useBackButton = true)
        {
            Step(useBackButton ? "Discarding edits through the title-bar Back button." : "Discarding edits with Escape.");
            if (useBackButton)
            {
                Editor.Find<Button>(By.AccessibilityId("PART_BackButton"), 15_000).Invoke(msPostAction: 0);
            }
            else
            {
                Editor.EnsureForeground();
                Editor.Find<Button>(By.AccessibilityId("SaveButton"), 15_000).Focus();
                KeyboardHelper.SendKeys(Key.Esc);
            }

            AssertWorkspaceListReady();
        }

        private void ConfirmWorkspaceDeletion()
        {
            Editor.Find<Button>(By.AccessibilityId("DeleteWorkspaceButton"), 15_000).Invoke(msPostAction: 0);
            ClickConfirmationButton("Remove");
            AssertWorkspaceListReady();
        }

        private void ClickConfirmationButton(string name)
        {
            var process = Microsoft.PowerToys.UITest.Next.Session.FromProcess(EditorProcess);
            var ready = WaitHelper.WaitForStable(
                () => process.FindAll<Button>(By.Name(name), 0)
                    .Where(button => button.Name == name && button.Displayed && button.IsEnabled).ToArray(),
                buttons => buttons is { Length: 1 },
                10_000,
                requiredConsecutiveMatches: 2);
            Assert.IsTrue(ready.Succeeded, $"The confirmation dialog's '{name}' button was not uniquely addressable.");
            ready.LastObservation![0].Invoke(msPostAction: 0);
        }
    }
}

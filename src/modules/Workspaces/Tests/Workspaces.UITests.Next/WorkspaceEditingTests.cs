// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
            Search("aTlAs");
            AssertSearchSuggestions(names[0]);
            AssertWorkspaceNames(names);
            ChooseSearchSuggestion(names[0]);
            CancelEditing();

            Search("editor fixture");
            AssertSearchSuggestions(names[1], names[2]);
            AssertWorkspaceNames(names);
            ChooseSearchSuggestion(names[2]);
            CancelEditing();

            Search("no matching workspace or application");
            AssertSearchSuggestions();
            SearchInput().Focus();
            KeyboardHelper.SendKeys(Key.Enter);
            AssertWorkspaceListReady();
            AssertWorkspaceNames(names);
            Search(string.Empty);
            AssertSearchSuggestions();
            AssertWorkspaceNames(names);
            CollectionAssert.AreEqual(original, File.ReadAllBytes(WorkspaceTestState.WorkspacesPath), "Searching or opening a suggestion changed saved workspaces.");
        }

        private void AssertSearchSuggestions(params string[] expected)
        {
            var process = Microsoft.PowerToys.UITest.Next.Session.FromProcess(EditorProcess);
            var ready = WaitHelper.WaitForStable(
                () => process.FindAll<TextBlock>(By.AccessibilityId("WorkspaceSearchSuggestionName"), 0)
                    .Where(text => text.Displayed).Select(text => text.Name).Order(StringComparer.Ordinal).ToArray(),
                names => names is not null && names.SequenceEqual(expected.Order(StringComparer.Ordinal)),
                15_000,
                requiredConsecutiveMatches: 2);
            Assert.IsTrue(ready.Succeeded, $"Expected suggestions [{string.Join(", ", expected)}], observed [{string.Join(", ", ready.LastObservation ?? [])}].");
        }

        private void ChooseSearchSuggestion(string name)
        {
            using var physicalCoordinates = new PhysicalCoordinateScope();
            var process = Microsoft.PowerToys.UITest.Next.Session.FromProcess(EditorProcess);
            var suggestions = process.FindAll<TextBlock>(By.AccessibilityId("WorkspaceSearchSuggestionName"), 10_000)
                .Where(text => text.Name == name && text.Displayed && text.Width > 0 && text.Height > 0).ToArray();
            Assert.HasCount(1, suggestions, $"The '{name}' search suggestion was not uniquely addressable.");
            suggestions[0].Click(msPostAction: 0);
            Assert.IsTrue(
                Editor.Find<TextBox>(By.AccessibilityId("EditNameTextBox"), 15_000).WaitForValue(name, timeoutMS: 10_000),
                "Choosing a search suggestion did not open the corresponding workspace.");
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
            SelectWorkspaceSort(index);
            AssertWorkspaceOrder(expected);
            Assert.AreEqual(index, WorkspaceTestState.ReadObject(WorkspaceTestState.SettingsPath)["properties"]!["sortby"]!.GetValue<int>());
            CloseEditor();
            OpenEditor();
            AssertWorkspaceSortSelected(index, order);
            AssertWorkspaceOrder(expected);
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
            using var before = PreviewSnapshot.Capture(TestContext, Editor, "custom-position");
            SelectCombo(row.Find<ComboBox>(By.AccessibilityId("WindowPositionComboBox")), selected, EditorProcess);
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
            SelectCombo(row.Find<ComboBox>(By.AccessibilityId("WindowPositionComboBox")), "Custom", EditorProcess);
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
            Step($"Selecting workspace sort order {index}.");
            Editor.Find<Button>(By.AccessibilityId("WorkspaceSortButton"), 15_000).Invoke(msPostAction: 0);
            var process = Microsoft.PowerToys.UITest.Next.Session.FromProcess(EditorProcess);
            process.Find<Element>(By.AccessibilityId(SortItemId(index)), 10_000).Invoke(msPostAction: 0);
            AssertWorkspaceSortSelected(index);
        }

        private void AssertWorkspaceSortSelected(int index, string? expectedName = null)
        {
            Editor.Find<Button>(By.AccessibilityId("WorkspaceSortButton"), 15_000).Invoke(msPostAction: 0);
            var process = Microsoft.PowerToys.UITest.Next.Session.FromProcess(EditorProcess);
            var selected = process.Find<Element>(By.AccessibilityId(SortItemId(index)), 10_000);
            Assert.IsTrue(selected.WaitForProperty("ToggleState", "On", 5_000), "The sort menu did not retain its selected option.");
            if (expectedName is not null)
            {
                Assert.AreEqual(expectedName, selected.Name);
            }

            KeyboardHelper.SendKeys(Key.Esc);
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
            var ready = WaitHelper.WaitForStable(
                () => FindDescendants<TextBox>(row, "Edit", "LeftTextBox")
                    .Any(textBox => textBox.Displayed && textBox.IsEnabled),
                available => available == expected,
                10_000,
                requiredConsecutiveMatches: 2);
            string message = expected
                ? "Custom window coordinates did not become editable."
                : "Custom coordinates stayed editable for a minimized/maximized app.";
            Assert.IsTrue(ready.Succeeded, message);
        }

        private void SelectCombo(ComboBox combo, string item, string processName)
        {
            using var physicalCoordinates = new PhysicalCoordinateScope();
            Step($"Selecting '{item}'.");
            if (combo.SelectedText == item)
            {
                return;
            }

            Editor.EnsureForeground();
            combo.Invoke(msPostAction: 0);
            var process = Microsoft.PowerToys.UITest.Next.Session.FromProcess(processName);

            // The dropdown's popup is process-scoped; scope identical item peers by their on-screen
            // bounds while keeping two genuinely different matching items an error.
            var options = process.FindAll<Element>(By.Name(item), 15_000)
                .Where(option => option.Name.Equals(item, StringComparison.OrdinalIgnoreCase) && option.ControlType == "ListItem")
                .GroupBy(option => (option.X, option.Y, option.Width, option.Height))
                .ToArray();
            Assert.HasCount(1, options, $"The '{item}' selection item was not uniquely addressable.");
            options[0].First().Click(msPostAction: 0);
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

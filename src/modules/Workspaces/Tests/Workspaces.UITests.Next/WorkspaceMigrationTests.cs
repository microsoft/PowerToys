// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Workspaces.UITests
{
    public sealed partial class WorkspacesTests
    {
        [TestMethod]
        public void DefaultPrefixWorkspaceDoesNotBlockCreatingAnotherWorkspace()
        {
            var existing = SeedOneWorkspace();
            existing["name"] = "Workspace";
            State.WriteProjects(existing);
            State.Fixture.OpenUnpackaged(State.Prefix + "-new-capture", TestContext);
            OpenEditor();
            Assert.AreEqual("Workspace", Editor.Find<Element>(By.AccessibilityId(WorkspaceTestState.Text(existing, "id"))).Name);

            Capture(StartCapture());
            var nameInput = Editor.Find<TextBox>(By.AccessibilityId("EditNameTextBox"), 15_000);
            Assert.AreEqual("Workspace 1", nameInput.Value, "The unnumbered default workspace name blocked or corrupted the next capture's name.");
            var newName = State.Prefix + "-created-after-prefix";
            State.TrackShortcut(newName);
            nameInput.SetText(newName);
            SaveWorkspace();

            var saved = WorkspaceTestState.ReadProjects();
            Assert.AreEqual(2, saved.Count, "Creating a workspace replaced the existing one.");
            Assert.AreEqual("Workspace", WorkspaceTestState.Text(WorkspaceTestState.ReadProject(WorkspaceTestState.Text(existing, "id")), "name"));
            Assert.HasCount(1, saved.Where(project => WorkspaceTestState.Text(project!, "name") == newName).ToArray());
            AssertWorkspaceNames(newName);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void RevertAfterRecaptureReturnsDirectlyToWorkspaceList(bool useBackButton)
        {
            var original = SeedOneWorkspace();
            var name = WorkspaceTestState.Text(original, "name");
            var stored = File.ReadAllBytes(WorkspaceTestState.WorkspacesPath);
            var originalTitle = WorkspaceTestState.Text(original["applications"]!.AsArray()[0]!, "title");
            var addedTitle = State.Prefix + "-recaptured";
            OpenEditor();
            EditWorkspace(name);
            Editor.Find<Button>(By.AccessibilityId("LaunchEditButton"), 15_000).Invoke(msPostAction: 0);
            State.Fixture.WaitForWindow(originalTitle);
            var capture = WaitForCaptureWindow();
            State.Fixture.OpenUnpackaged(addedTitle, TestContext);
            Capture(capture, addedTitle);
            Assert.AreEqual(name, Editor.Find<TextBox>(By.AccessibilityId("EditNameTextBox")).Value, "Recapture lost the saved workspace name.");

            Step("Reverting a recaptured workspace before leaving the editing page.");
            var revert = Editor.Find<Button>(By.AccessibilityId("RevertButton"), 15_000);
            revert.Invoke(msPostAction: 0);
            Assert.IsTrue(revert.WaitForProperty("IsEnabled", "False", 15_000), "Revert did not restore the original editing state.");
            Assert.IsTrue(Editor.Find<TextBox>(By.AccessibilityId("EditNameTextBox")).WaitForValue(name, timeoutMS: 15_000));
            CancelEditing(useBackButton);
            AssertWorkspaceNames(name);
            CollectionAssert.AreEqual(stored, File.ReadAllBytes(WorkspaceTestState.WorkspacesPath), "Recapture/Revert saved unsaved changes.");
            Assert.IsFalse(File.Exists(WorkspaceTestState.TemporaryPath), "Leaving the reverted workspace retained its temporary draft.");

            EditWorkspace(name);
            Assert.IsTrue(Editor.Has(By.AccessibilityId("Primary fixture"), 10_000), "Reopening the workspace did not restore its original application.");
            Assert.IsFalse(Editor.Find<Button>(By.AccessibilityId("RevertButton")).IsEnabled, "A previous editor's recapture state survived re-navigation.");
            CancelEditing();
        }

        [TestMethod]
        public void RenamingImmediatelyReordersCardsAndRetainsWorkspaceIdentity()
        {
            var projects = SeedSearchWorkspaces();
            var oldName = WorkspaceTestState.Text(projects[0], "name");
            var renamed = State.Prefix + "-Zulu";
            State.TrackShortcut(renamed);
            OpenEditor();
            SelectWorkspaceSort(2);
            var originalId = WaitForCard(oldName).Id;
            EditWorkspace(oldName);
            Editor.Find<TextBox>(By.AccessibilityId("EditNameTextBox")).SetText(renamed);
            SaveWorkspace();

            var expected = new[] { renamed, WorkspaceTestState.Text(projects[1], "name"), WorkspaceTestState.Text(projects[2], "name") }
                .Order(StringComparer.Ordinal).ToArray();
            AssertWorkspaceOrder(expected);
            Assert.AreEqual(originalId, WaitForCard(renamed).Id, "Renaming replaced the workspace's stable card identity.");
            Assert.AreEqual(renamed, WorkspaceTestState.Text(WorkspaceTestState.ReadProject(WorkspaceTestState.Text(projects[0], "id")), "name"));
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void RenamingRemovesTheOldShortcutAndAppliesTheNewPreference(bool keepShortcut)
        {
            var project = SeedOneWorkspace();
            var oldName = WorkspaceTestState.Text(project, "name");
            var newName = State.Prefix + "-renamed";
            State.TrackShortcut(newName);
            var oldPath = WorkspaceTestState.ShortcutPath(oldName);
            var newPath = WorkspaceTestState.ShortcutPath(newName);
            OpenEditor();
            EditWorkspace(oldName);
            SetCheck(Editor.Find<CheckBox>(By.AccessibilityId("CreateShortcutCheckBox")), true);
            SaveWorkspace();
            Assert.IsTrue(File.Exists(oldPath), "The shortcut precondition was not established.");

            EditWorkspace(oldName);
            Editor.Find<TextBox>(By.AccessibilityId("EditNameTextBox")).SetText(newName);
            SetCheck(Editor.Find<CheckBox>(By.AccessibilityId("CreateShortcutCheckBox")), keepShortcut);
            SaveWorkspace();
            var ready = WaitHelper.WaitForStable(
                () => !File.Exists(oldPath) && File.Exists(newPath) == keepShortcut,
                matches => matches,
                15_000,
                requiredConsecutiveMatches: 2);
            Assert.IsTrue(ready.Succeeded, "Renaming retained the old shortcut or did not apply the new shortcut preference.");
            var saved = WorkspaceTestState.ReadProject(WorkspaceTestState.Text(project, "id"));
            Assert.AreEqual(newName, WorkspaceTestState.Text(saved, "name"));
            Assert.AreEqual(keepShortcut, saved["is-shortcut-needed"]!.GetValue<bool>());
            if (keepShortcut)
            {
                var (_, arguments) = ReadShortcutTargetAndArguments(newPath);
                StringAssert.Contains(arguments, WorkspaceTestState.Text(project, "id"), "Renaming redirected the shortcut to another workspace.");
            }

            EditWorkspace(newName);
            ConfirmWorkspaceDeletion();
            AssertWorkspaceNames();
            Assert.IsFalse(File.Exists(oldPath) || File.Exists(newPath), "Deleting the renamed workspace retained one of its shortcuts.");
        }

        [TestMethod]
        public void CancellingDeletionPreservesTheWorkspaceAndItsUnsavedEdits()
        {
            var project = SeedOneWorkspace();
            var originalName = WorkspaceTestState.Text(project, "name");
            var draftName = State.Prefix + "-unsaved";
            var stored = File.ReadAllBytes(WorkspaceTestState.WorkspacesPath);
            OpenEditor();
            EditWorkspace(originalName);
            Editor.Find<TextBox>(By.AccessibilityId("EditNameTextBox")).SetText(draftName);
            Editor.Find<Button>(By.AccessibilityId("DeleteWorkspaceButton")).Invoke(msPostAction: 0);
            ClickConfirmationButton("Cancel");
            Assert.AreEqual(draftName, Editor.Find<TextBox>(By.AccessibilityId("EditNameTextBox")).Value, "Cancelling deletion discarded other edits.");
            CollectionAssert.AreEqual(stored, File.ReadAllBytes(WorkspaceTestState.WorkspacesPath), "Cancelling deletion modified stored workspaces.");
            CancelEditing();
            AssertWorkspaceNames(originalName);
        }

        [TestMethod]
        public void ClosingEditorDuringCaptureClosesAllCaptureWindows()
        {
            SeedOneWorkspace();
            var stored = File.ReadAllBytes(WorkspaceTestState.WorkspacesPath);
            OpenEditor();
            StartCapture();
            Step("Closing the minimized editor while its capture controls are open.");
            Assert.IsTrue(WindowControl.TryCloseWindow(Editor.WindowHandle), "The editor did not accept its close request.");
            Assert.IsTrue(WaitForProcess(EditorProcess, false), "Closing the editor left the capture window or overlay process alive.");
            Assert.HasCount(0, WindowsFinder.ListByApp(EditorProcess), "Capture windows survived the editor's close request.");
            CollectionAssert.AreEqual(stored, File.ReadAllBytes(WorkspaceTestState.WorkspacesPath), "Closing capture changed stored workspace data.");
            Assert.IsFalse(File.Exists(WorkspaceTestState.TemporaryPath), "Closing capture before Capture created a temporary draft.");
            editor = null;
        }

        [TestMethod]
        public void EditorWindowPlacementPersistsAcrossRestart()
        {
            SeedOneWorkspace();
            using var physicalCoordinates = new PhysicalCoordinateScope();
            OpenEditor(maximize: false);
            var handle = new IntPtr(Editor.WindowHandle);
            var target = State.Target;
            WindowHelper.RestoreWindow(handle);
            WindowHelper.MoveWindow(handle, target.WorkLeft + 16, target.WorkTop + 16);
            var dpiReady = WaitHelper.WaitForStable(
                () => NativeMethods.GetDpiForWindow(handle),
                dpi => dpi == target.Dpi,
                10_000,
                requiredConsecutiveMatches: 2);
            Assert.IsTrue(dpiReady.Succeeded, "The editor did not move to the target display's DPI.");

            var scale = dpiReady.LastObservation / 96.0;

            // Match the editor's 750 x 680 effective-pixel minimum while positioning in physical pixels.
            var minimumWidth = (int)Math.Ceiling(750 * scale);
            var minimumHeight = (int)Math.Ceiling(680 * scale);
            var workWidth = target.WorkRight - target.WorkLeft;
            var workHeight = target.WorkBottom - target.WorkTop;
            Assert.IsTrue(
                workWidth > minimumWidth + 8 && workHeight > minimumHeight + 8,
                "The primary work area must fit the editor's minimum normal size with room to move it.");
            var margin = Math.Min((int)Math.Ceiling(16 * scale), Math.Min((workWidth - minimumWidth) / 4, (workHeight - minimumHeight) / 4));
            var width = Math.Clamp((int)Math.Ceiling(960 * scale), minimumWidth, workWidth - (2 * margin));
            var height = Math.Clamp((int)Math.Ceiling(720 * scale), minimumHeight, workHeight - (2 * margin));
            var expected = (Left: target.WorkLeft + margin, Top: target.WorkTop + margin, Right: target.WorkLeft + margin + width, Bottom: target.WorkTop + margin + height);

            Step("Changing the editor's normal position and size before closing it.");
            WindowHelper.SetMainWindowSize(handle, width, height);
            WindowHelper.MoveWindow(handle, expected.Left, expected.Top);
            AssertNormalEditorBounds(handle, expected);
            CloseEditor();
            Assert.IsTrue(File.Exists(WorkspaceTestState.WindowPlacementPath), "Closing the editor did not persist its placement.");

            Step("Reopening the editor without the test restoring or maximizing it.");
            OpenEditor(maximize: false);
            handle = new IntPtr(Editor.WindowHandle);
            AssertNormalEditorBounds(handle, expected);

            Step("Maximizing the editor before another close and reopen.");
            WindowHelper.MaximizeWindow(handle);
            Assert.IsTrue(
                WaitHelper.WaitForStable(
                    () => WindowHelper.IsWindowMaximized(handle),
                    maximized => maximized,
                    10_000,
                    requiredConsecutiveMatches: 2).Succeeded,
                "The editor did not establish its maximized precondition.");
            CloseEditor();
            OpenEditor(maximize: false);
            handle = new IntPtr(Editor.WindowHandle);
            Assert.IsTrue(
                WaitHelper.WaitForStable(
                    () => !NativeMethods.IsIconic(handle) && WindowHelper.IsWindowMaximized(handle),
                    maximized => maximized,
                    10_000,
                    requiredConsecutiveMatches: 2).Succeeded,
                "Reopening the editor did not restore its saved maximized state.");
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void ActivationShortcutRestoresTheEditorAndKeepsItMaximized(bool minimizeFirst)
        {
            var shortcut = ReadActivationShortcut();
            OpenEditor();
            var handle = new IntPtr(Editor.WindowHandle);
            Assert.IsTrue(
                WaitHelper.WaitForStable(
                    () => WindowHelper.IsWindowMaximized(handle),
                    maximized => maximized,
                    10_000,
                    requiredConsecutiveMatches: 2).Succeeded,
                "The editor did not establish its maximized precondition.");
            if (minimizeFirst)
            {
                WindowHelper.MinimizeWindow(handle);
                Assert.IsTrue(
                    WaitHelper.WaitForStable(
                        () => NativeMethods.IsIconic(handle),
                        minimized => minimized,
                        10_000,
                        requiredConsecutiveMatches: 2).Succeeded,
                    "The editor did not establish its minimized precondition.");
            }

            SettingsUi.EnsureForeground();
            Assert.IsTrue(WindowControl.WaitForForeground(SettingsWindow(), 10_000), "Settings did not acquire foreground before editor reactivation.");
            Step("Reactivating the existing editor with its registered shortcut.");
            KeyboardHelper.SendKeys(shortcut);

            // Read native state only: AttachEditor/EnsureForeground would restore the window on the
            // test's behalf and hide a failure in the editor's shortcut handler.
            var ready = WaitHelper.WaitForStable(
                () => !NativeMethods.IsIconic(handle) && WindowHelper.IsWindowMaximized(handle) &&
                    WindowControl.GetForegroundWindowInfo().Hwnd == handle,
                restored => restored,
                15_000,
                requiredConsecutiveMatches: 2);
            Assert.IsTrue(ready.Succeeded, "The activation shortcut did not restore and foreground the editor while preserving its maximized state.");
        }

        private static void AssertNormalEditorBounds(IntPtr handle, (int Left, int Top, int Right, int Bottom) expected)
        {
            var ready = WaitHelper.WaitForStable(
                () => WindowHelper.GetWindowBounds(handle),
                bounds => !NativeMethods.IsIconic(handle) && !WindowHelper.IsWindowMaximized(handle) &&
                    Math.Abs(bounds.Left - expected.Left) <= 2 && Math.Abs(bounds.Top - expected.Top) <= 2 &&
                    Math.Abs(bounds.Right - expected.Right) <= 2 && Math.Abs(bounds.Bottom - expected.Bottom) <= 2,
                10_000,
                requiredConsecutiveMatches: 2);
            Assert.IsTrue(ready.Succeeded, $"The editor did not retain normal bounds {expected}. Observed: {ready.LastObservation}.");
        }
    }
}

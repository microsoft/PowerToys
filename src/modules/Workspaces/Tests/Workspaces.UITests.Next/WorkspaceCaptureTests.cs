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
        public void CaptureIncludesPackagedUnpackagedAndMinimizedWindows()
        {
            var firstTitle = State.Prefix + "-normal";
            var minimizedTitle = State.Prefix + "-minimized";
            var first = State.Fixture.OpenUnpackaged(firstTitle, TestContext);
            var firstBounds = NormalizedOuterBounds(new IntPtr(first.WindowHandle));
            var minimized = State.Fixture.OpenUnpackaged(minimizedTitle, TestContext);
            WindowHelper.MinimizeWindow(new IntPtr(minimized.WindowHandle));
            Assert.IsTrue(
                WaitHelper.WaitForStable(
                    () => NativeMethods.IsIconic(new IntPtr(minimized.WindowHandle)),
                    value => value,
                    10_000,
                    requiredConsecutiveMatches: 2).Succeeded,
                "The minimized fixture did not establish its capture precondition.");

            OpenEditor();
            var capture = StartCapture();
            State.Fixture.OpenPackaged(TestContext);
            var captured = Capture(capture);
            var apps = captured["applications"]!.AsArray();
            var normalApp = AppByTitle(apps, firstTitle);
            var minimizedApp = AppByTitle(apps, minimizedTitle);
            var packagedApp = AppByTitle(apps, TestAppFixture.DefaultTitle);

            Assert.AreEqual(State.Fixture.ExecutablePath, WorkspaceTestState.Text(normalApp, "application-path"), true);
            Assert.AreEqual(string.Empty, WorkspaceTestState.Text(normalApp, "package-full-name"), "The unpackaged app must not acquire package identity.");
            Assert.IsFalse(normalApp["is-elevated"]!.GetValue<bool>(), "The non-elevated fixture was captured as administrator.");
            Assert.IsFalse(normalApp["minimized"]!.GetValue<bool>());
            Assert.IsFalse(normalApp["maximized"]!.GetValue<bool>());
            AssertPosition(normalApp["position"]!, firstBounds.X, firstBounds.Y, firstBounds.Width, firstBounds.Height);
            Assert.IsTrue(minimizedApp["minimized"]!.GetValue<bool>(), "The minimized fixture lost its captured state.");
            Assert.IsFalse(minimizedApp["is-elevated"]!.GetValue<bool>());
            Assert.AreEqual(State.Fixture.PackageFullName, WorkspaceTestState.Text(packagedApp, "package-full-name"), "The app opened after Create Workspace was not captured with its package identity.");
            Assert.AreEqual(State.Fixture.AppUserModelId, WorkspaceTestState.Text(packagedApp, "app-user-model-id"));

            var monitorNumber = minimizedApp["monitor"]!.GetValue<int>();
            var monitor = captured["monitor-configuration"]!.AsArray().Single(node => node!["monitor-number"]!.GetValue<int>() == monitorNumber)!;
            var minimizedBounds = monitor["monitor-rect-dpi-unaware"]!;
            AssertPosition(
                minimizedApp["position"]!,
                minimizedBounds["left"]!.GetValue<int>(),
                minimizedBounds["top"]!.GetValue<int>(),
                minimizedBounds["width"]!.GetValue<int>(),
                minimizedBounds["height"]!.GetValue<int>());

            var name = State.Prefix + "-captured";
            State.TrackShortcut(name);
            Editor.Find<TextBox>(By.AccessibilityId("EditNameTextBox")).SetText(name);
            SaveWorkspace();
            AssertWorkspaceNames(name);
            var saved = WorkspaceTestState.ReadProjects().Single()!.AsObject();
            Assert.AreEqual(name, WorkspaceTestState.Text(saved, "name"));
            var savedApps = saved["applications"]!.AsArray();
            foreach (var title in new[] { firstTitle, minimizedTitle, TestAppFixture.DefaultTitle })
            {
                var app = AppByTitle(savedApps, title);
                Assert.IsTrue(Guid.TryParse(WorkspaceTestState.Text(app, "id"), out _), $"Saved application '{title}' needs a stable ID.");
            }
        }

        [TestMethod]
        public void CancelCaptureLeavesWorkspaceListAndStorageUnchanged()
        {
            var project = SeedOneWorkspace();
            var name = WorkspaceTestState.Text(project, "name");
            var original = File.ReadAllBytes(WorkspaceTestState.WorkspacesPath);
            OpenEditor();
            var capture = StartCapture();
            Step("Cancelling the capture before taking a snapshot.");
            capture.Find<Button>(By.AccessibilityId("CancelButton"), 15_000).Invoke(msPostAction: 0);
            Assert.IsTrue(
                WaitHelper.WaitForStable(
                    () => WindowsFinder.ListByApp(EditorProcess).Any(window => window.Hwnd == capture.WindowHandle),
                    visible => !visible,
                    10_000,
                    requiredConsecutiveMatches: 2).Succeeded,
                "The cancelled capture window remained visible.");
            Assert.IsTrue(Editor.Has(By.AccessibilityId("NewProjectButton"), 15_000));
            AssertWorkspaceNames(name);
            CollectionAssert.AreEqual(original, File.ReadAllBytes(WorkspaceTestState.WorkspacesPath), "Cancelling capture changed the stored workspaces.");
            Assert.IsFalse(File.Exists(WorkspaceTestState.TemporaryPath), "Cancelling before Capture unexpectedly created a snapshot.");
        }

        [TestMethod]
        public void CancellingCapturedWorkspaceDiscardsIt()
        {
            State.Fixture.OpenUnpackaged(State.Prefix + "-capture-cancel", TestContext);
            var original = File.ReadAllBytes(WorkspaceTestState.WorkspacesPath);
            OpenEditor();
            Capture(StartCapture());
            Step("Discarding the newly captured workspace.");
            Editor.Find<Button>(By.AccessibilityId("CancelButton")).Invoke(msPostAction: 0);
            Assert.IsTrue(Editor.Has(By.AccessibilityId("NewProjectButton"), 15_000));
            AssertWorkspaceNames();
            CollectionAssert.AreEqual(original, File.ReadAllBytes(WorkspaceTestState.WorkspacesPath), "Cancelling the new workspace persisted it.");
            Assert.IsFalse(File.Exists(WorkspaceTestState.TemporaryPath), "The discarded temporary snapshot was not removed.");
        }

        private static JsonObject AppByTitle(JsonArray apps, string title)
        {
            var matches = apps.Where(app => app is not null && WorkspaceTestState.Text(app, "title") == title).ToArray();
            Assert.HasCount(1, matches, $"Expected exactly one captured window titled '{title}'. Actual: {apps}");
            return matches[0]!.AsObject();
        }

        private static void AssertPosition(JsonNode position, int x, int y, int width, int height)
        {
            Assert.AreEqual(x, position["X"]!.GetValue<int>(), 2, $"Unexpected X coordinate: {position}");
            Assert.AreEqual(y, position["Y"]!.GetValue<int>(), 2, $"Unexpected Y coordinate: {position}");
            Assert.AreEqual(width, position["width"]!.GetValue<int>(), 2, $"Unexpected width: {position}");
            Assert.AreEqual(height, position["height"]!.GetValue<int>(), 2, $"Unexpected height: {position}");
        }
    }
}

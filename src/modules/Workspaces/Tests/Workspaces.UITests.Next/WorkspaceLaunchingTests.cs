// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Workspaces.UITests
{
    public sealed partial class WorkspacesTests
    {
        [TestMethod]
        [DataRow(false, false)]
        [DataRow(true, false)]
        [DataRow(false, true)]
        public void LaunchFromEditorRestoresWindowPlacement(bool minimized, bool maximized)
        {
            var title = State.Prefix + "-placement";
            var app = State.Application("Placement fixture", title);
            app["minimized"] = minimized;
            app["maximized"] = maximized;
            var project = State.Project("launch", SettingsWindow(), app);
            State.WriteProjects(project);
            OpenEditor();
            LaunchWorkspace(WorkspaceTestState.Text(project, "name"));
            var window = State.Fixture.WaitForWindow(title);
            AssertPositioned(window, app);
            WaitForLaunchCompletion(project);
            Assert.AreEqual(1, State.Fixture.ProcessIds().Count, "Launching one application created unexpected fixture processes.");
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void CommandLineArgumentsReachUnpackagedAndPackagedApps(bool packaged)
        {
            if (packaged)
            {
                State.Fixture.OpenPackaged(TestContext);
                State.Fixture.CloseAll();
            }

            var title = State.Prefix + "-arguments";
            var app = State.Application(
                "Argument fixture",
                title,
                path: packaged ? State.Fixture.PackagedExecutablePath : State.Fixture.ExecutablePath);
            if (packaged)
            {
                app["package-full-name"] = State.Fixture.PackageFullName;
                app["app-user-model-id"] = State.Fixture.AppUserModelId;
            }

            var project = State.Project("arguments", SettingsWindow(), app);
            State.WriteProjects(project);
            OpenEditor();
            EditWorkspace(WorkspaceTestState.Text(project, "name"));
            const string payload = "Workspaces argument with spaces";
            var arguments = $"--title \"{title}\" --payload \"{payload}\"";
            ExpandApplication("Argument fixture").Find<TextBox>(By.AccessibilityId("CommandLineTextBox")).SetText(arguments);
            SaveWorkspace();
            var saved = WorkspaceTestState.ReadProject(WorkspaceTestState.Text(project, "id"));
            Assert.AreEqual(arguments, WorkspaceTestState.Text(saved["applications"]!.AsArray().Single()!, "command-line-arguments"));
            LaunchWorkspace(WorkspaceTestState.Text(project, "name"));
            var window = State.Fixture.WaitForWindow(title);
            Assert.AreEqual(payload, window.Find<TextBox>(By.AccessibilityId("PayloadTextBox"), 15_000).Value, "The launched application did not receive the saved command line.");
            Assert.AreEqual(packaged ? State.Fixture.PackageFullName : null, NativeMethods.PackageFullName(window.ProcessId));
            AssertPositioned(window, app);
            WaitForLaunchCompletion(project);
        }

        [TestMethod]
        public void DesktopShortcutTracksFilePresenceAndLaunchesWorkspace()
        {
            var title = State.Prefix + "-shortcut";
            var app = State.Application("Shortcut fixture", title);
            var project = State.Project("shortcut", SettingsWindow(), app);
            State.WriteProjects(project);
            var name = WorkspaceTestState.Text(project, "name");
            var shortcut = WorkspaceTestState.ShortcutPath(name);
            OpenEditor();
            EditWorkspace(name);
            var checkBox = Editor.Find<CheckBox>(By.Name("Create desktop shortcut"));
            Assert.IsFalse(checkBox.IsChecked, "A missing desktop shortcut should be shown as unchecked.");
            SetCheck(checkBox, true);
            SaveWorkspace();
            Assert.IsTrue(
                WaitHelper.WaitForStable(() => File.Exists(shortcut), exists => exists, 15_000, requiredConsecutiveMatches: 2).Succeeded,
                "Save did not create the desktop shortcut.");

            // Read the created .lnk through the Windows Script Host shell. StorageFile.GetFileFromPathAsync
            // refuses desktop shortcuts on some images ("Access is denied ... system or hidden"), while the
            // WScript.Shell shortcut object reads the target and arguments the Shell actually stored.
            var (target, shortcutArguments) = ReadShortcutTargetAndArguments(shortcut);
            Assert.AreEqual(
                Path.Combine(Path.GetDirectoryName(SessionHelper.GetExecutablePath(PowerToysModule.Workspaces))!, "PowerToys.WorkspacesLauncher.exe"),
                target,
                true,
                "The shortcut does not target the Workspaces launcher.");
            StringAssert.Contains(shortcutArguments, WorkspaceTestState.Text(project, "id"), "The shortcut targets the wrong workspace.");

            Step("Launching the actual desktop .lnk through the Windows Shell.");
            using var launch = Process.Start(new ProcessStartInfo(shortcut) { UseShellExecute = true });
            var window = State.Fixture.WaitForWindow(title);
            AssertPositioned(window, app);
            WaitForLaunchCompletion(project);

            EditWorkspace(name);
            Assert.IsTrue(Editor.Find<CheckBox>(By.Name("Create desktop shortcut")).IsChecked, "The existing desktop shortcut was not recognized.");
            CancelEditing();
            File.Delete(shortcut);
            EditWorkspace(name);
            Assert.IsFalse(Editor.Find<CheckBox>(By.Name("Create desktop shortcut")).IsChecked, "The checkbox did not notice an externally removed shortcut.");
            SetCheck(Editor.Find<CheckBox>(By.Name("Create desktop shortcut")), true);
            SaveWorkspace();
            Assert.IsTrue(File.Exists(shortcut), "The shortcut was not recreated.");
            EditWorkspace(name);
            SetCheck(Editor.Find<CheckBox>(By.Name("Create desktop shortcut")), false);
            SaveWorkspace();
            Assert.IsFalse(File.Exists(shortcut), "Unchecking the option did not remove the desktop shortcut.");
            Assert.IsFalse(WorkspaceTestState.ReadProject(WorkspaceTestState.Text(project, "id"))["is-shortcut-needed"]!.GetValue<bool>());
        }

        [TestMethod]
        public void LaunchAndEditCaptureAddsWindowsToTheExistingWorkspace()
        {
            var originalTitle = State.Prefix + "-original";
            var addedTitle = State.Prefix + "-added";
            var app = State.Application("Workspaces.TestApp", originalTitle);
            var project = State.Project("launch-edit", SettingsWindow(), app);
            State.WriteProjects(project);
            var name = WorkspaceTestState.Text(project, "name");
            OpenEditor();
            EditWorkspace(name);
            Step("Launching the saved workspace from Launch and Edit.");
            Editor.Find<Button>(By.AccessibilityId("LaunchEditButton")).Invoke(msPostAction: 0);
            var original = State.Fixture.WaitForWindow(originalTitle);
            AssertPositioned(original, app);
            var capture = WaitForCaptureWindow();
            State.Fixture.OpenUnpackaged(addedTitle, TestContext);
            Capture(capture, addedTitle);
            Assert.IsTrue(Editor.Find<Button>(By.AccessibilityId("RevertButton")).IsEnabled, "Launch and Edit did not expose its revert action.");
            SaveWorkspace();
            var saved = WorkspaceTestState.WaitForProject(
                WorkspaceTestState.Text(project, "id"),
                value => value["applications"]!.AsArray().Any(application => WorkspaceTestState.Text(application!, "title") == addedTitle));
            Assert.AreEqual(1, WorkspaceTestState.ReadProjects().Count, "Launch and Edit created a second workspace instead of updating the existing one.");
            Assert.AreEqual(name, WorkspaceTestState.Text(saved, "name"));
            Assert.AreEqual(
                Guid.Parse(WorkspaceTestState.Text(app, "id")),
                Guid.Parse(WorkspaceTestState.Text(AppByTitle(saved["applications"]!.AsArray(), originalTitle), "id")),
                "Recapture did not retain the identity of the originally launched application.");
            AppByTitle(saved["applications"]!.AsArray(), addedTitle);
        }

        [TestMethod]
        public void MoveExistingWindowsReusesTheOriginalProcessAndWindow()
        {
            var title = State.Prefix + "-reuse";
            var original = State.Fixture.OpenUnpackaged(title, TestContext);
            var app = State.Application("Existing fixture", title);
            var project = State.Project("reuse", SettingsWindow(), app);
            State.WriteProjects(project);
            OpenEditor();
            EditWorkspace(WorkspaceTestState.Text(project, "name"));
            SetCheck(Editor.Find<CheckBox>(By.Name("Move existing windows")), true);
            SaveWorkspace();
            LaunchWorkspace(WorkspaceTestState.Text(project, "name"));
            AssertPositioned(original, app);
            WaitForLaunchCompletion(project);
            CollectionAssert.AreEqual(new[] { original.ProcessId }, State.Fixture.ProcessIds().ToArray(), "Move existing windows launched a duplicate application.");
            var reused = State.Fixture.FindWindow(title);
            Assert.IsNotNull(reused, "Move existing windows closed the original window.");
            Assert.AreEqual(original.WindowHandle, reused.Hwnd, "Move existing windows replaced the existing HWND.");
        }

        [TestMethod]
        public void MissingSavedMonitorUsesTheRemainingDisplay()
        {
            var title = State.Prefix + "-missing-monitor";
            var app = State.Application("Topology fixture", title);
            var missingNumber = MonitorInfo.Count + 10;
            app["monitor"] = missingNumber;
            var project = State.Project("topology", SettingsWindow(), app);
            var missingMonitor = project["monitor-configuration"]!.AsArray()[0]!.DeepClone();
            missingMonitor["monitor-number"] = missingNumber;
            missingMonitor["id"] = "Disconnected test monitor";
            project["monitor-configuration"]!.AsArray().Add(missingMonitor);
            State.WriteProjects(project);
            OpenEditor();
            LaunchWorkspace(WorkspaceTestState.Text(project, "name"));
            var window = State.Fixture.WaitForWindow(title);
            var fallback = app.DeepClone().AsObject();
            fallback["minimized"] = true;
            AssertPositioned(window, fallback);
            Assert.IsTrue(MonitorInfo.GetFromWindow(new IntPtr(window.WindowHandle)).IsPrimary, "The missing-monitor fallback did not use the remaining primary display.");
            WaitForLaunchCompletion(project);
            TestContext.WriteLine("This exercises replay of a saved disconnected monitor, not physical monitor hot-plugging or mixed-DPI hardware.");
        }

        [TestMethod]
        public void LauncherDisplaysLaunchingLaunchedAndFailedStates()
        {
            using var gate = new EventWaitHandle(false, EventResetMode.ManualReset, $@"Local\{State.Prefix}-progress-gate");
            var firstTitle = State.Prefix + "-launched";
            var first = State.Application("Ready fixture", firstTitle);
            var missing = State.Application("Missing fixture", "missing", path: Path.Combine(Path.GetTempPath(), State.Prefix + "-missing.exe"));
            var applications = new List<JsonObject> { first, missing };
            applications.AddRange(PendingApplications(10, $@"Local\{State.Prefix}-progress-gate"));
            var project = State.Project("progress", SettingsWindow(), applications.ToArray());
            State.WriteProjects(project);
            OpenEditor();
            LaunchWorkspace(WorkspaceTestState.Text(project, "name"));
            var window = State.Fixture.WaitForWindow(firstTitle);
            AssertPositioned(window, first);
            var progress = LauncherWindow();
            AssertLaunchGlyph(progress, "Ready fixture", "\uF78C");

            // The missing app has no executable on disk, so it reaches Failed. Failed and the "\uEF2C"
            // fallback share a glyph, but a still-pending app (Waiting/Launched) has Loading == true and
            // hides its glyph behind the progress ring, and Canceled cannot occur before the Cancel below.
            // A displayed "\uEF2C" here therefore uniquely proves the Failed state without a product hook.
            AssertLaunchGlyph(progress, "Missing fixture", "\uEF2C");
            var loading = progress.Find(By.AccessibilityId("LaunchProgress_Pending fixture 01"), 15_000);
            Assert.IsTrue(loading.Displayed, "The not-yet-ready application did not show its launching indicator.");
            Assert.IsNull(State.Fixture.FindWindow(State.Prefix + "-pending-01"), "The gated fixture unexpectedly created a window.");
            progress.Find<Button>(By.AccessibilityId("CancelButton")).Invoke(msPostAction: 0);
        }

        [TestMethod]
        public void CancelLaunchStopsPendingAppsAndPreservesOpenedWindows()
        {
            var gateName = $@"Local\{State.Prefix}-cancel-gate";
            var startedName = $@"Local\{State.Prefix}-first-started";
            var tailName = $@"Local\{State.Prefix}-tail-started";
            using var gate = new EventWaitHandle(false, EventResetMode.ManualReset, gateName);
            using var started = new EventWaitHandle(false, EventResetMode.ManualReset, startedName);
            using var tailStarted = new EventWaitHandle(false, EventResetMode.ManualReset, tailName);
            var firstTitle = State.Prefix + "-opened";
            var first = State.Application("Opened fixture", firstTitle);
            var applications = new List<JsonObject> { first };
            applications.AddRange(PendingApplications(16, gateName, startedName, tailName));
            var project = State.Project("cancel-launch", SettingsWindow(), applications.ToArray());
            State.WriteProjects(project);
            OpenEditor();
            LaunchWorkspace(WorkspaceTestState.Text(project, "name"));
            var opened = State.Fixture.WaitForWindow(firstTitle);
            AssertPositioned(opened, first);
            Assert.IsTrue(started.WaitOne(20_000), "The blocking fixture never reached its startup gate.");
            Assert.IsFalse(tailStarted.WaitOne(0), "The final fixture started before the cancellation precondition was established.");
            Step("Cancelling the launch with one window open and more applications pending.");
            var progress = LauncherWindow();
            progress.Find<Button>(By.AccessibilityId("CancelButton"), 15_000).Invoke(msPostAction: 0);
            Assert.IsTrue(WaitForProcess(LauncherUiProcess, false), "Cancel launch did not close the progress UI.");
            Assert.IsTrue(WaitForProcess(LauncherProcess, false, 45_000), "Cancel launch did not stop the launch engine.");
            Assert.IsFalse(tailStarted.WaitOne(0), "Cancel launch still started an application that was pending.");
            var remaining = State.Fixture.FindWindow(firstTitle);
            Assert.IsNotNull(remaining, "Cancel launch closed an already-open application.");
            Assert.AreEqual(opened.WindowHandle, remaining.Hwnd, "Cancel launch replaced an already-open application.");
        }

        [TestMethod]
        public void DismissClosesProgressWhileApplicationsContinueLaunching()
        {
            var gateName = $@"Local\{State.Prefix}-dismiss-gate";
            var startedName = $@"Local\{State.Prefix}-dismiss-started";
            using var gate = new EventWaitHandle(false, EventResetMode.ManualReset, gateName);
            using var started = new EventWaitHandle(false, EventResetMode.ManualReset, startedName);
            var applications = PendingApplications(8, gateName, startedName).ToArray();
            var project = State.Project("dismiss", SettingsWindow(), applications);
            State.WriteProjects(project);
            OpenEditor();
            LaunchWorkspace(WorkspaceTestState.Text(project, "name"));
            Assert.IsTrue(started.WaitOne(20_000), "The fixture did not reach its startup gate.");
            var progress = LauncherWindow();
            Step("Dismissing progress without cancelling the pending launch.");
            progress.Find<Button>(By.AccessibilityId("DismissButton"), 15_000).Invoke(msPostAction: 0);
            Assert.IsTrue(WaitForProcess(LauncherUiProcess, false), "Dismiss did not close the progress UI.");
            Assert.IsTrue(WaitForProcess(LauncherProcess, true, 5_000), "Dismiss cancelled the launch engine.");
            Step("Releasing the application startup gate after the progress UI has closed.");
            gate.Set();
            foreach (var app in applications)
            {
                var window = State.Fixture.WaitForWindow(WorkspaceTestState.Text(app, "title"), 60_000);
                AssertPositioned(window, app);
            }

            WaitForLaunchCompletion(project);
            Assert.AreEqual(applications.Length, State.Fixture.ProcessIds().Count, "Dismiss did not allow every configured app to launch.");
        }

        private void LaunchWorkspace(string name)
        {
            var card = WaitForCard(name);
            var launch = LaunchButtonsForCard(card);
            if (launch.Length != 1)
            {
                // The Launch button is a card descendant, so its automation peer can be missing after the
                // list re-projected its items (e.g. right after a save). Re-render the list once from a
                // fresh editor to restore live descendant peers, then reacquire the button.
                Step("Re-rendering the workspace list to restore the card's Launch button.");
                CloseEditor();
                OpenEditor();
                card = WaitForCard(name);
                launch = LaunchButtonsForCard(card);
            }

            Assert.HasCount(1, launch, "The workspace Launch button was not uniquely addressable.");
            Step($"Launching workspace '{name}' from its editor card.");
            launch[0].Invoke(msPostAction: 0);
        }

        private Button[] LaunchButtonsForCard(WorkspaceCard card) =>
            [.. Editor.FindAll<Button>(By.Name("Launch"), 5_000)
                .Where(button => button.Name == "Launch" && button.Y >= card.Y && button.Y <= card.Y + card.Height)];

        private static (string Target, string Arguments) ReadShortcutTargetAndArguments(string path)
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell") ?? throw new InvalidOperationException("WScript.Shell is not registered.");
            dynamic shell = Activator.CreateInstance(shellType)!;
            try
            {
                dynamic link = shell.CreateShortcut(path);
                try
                {
                    return ((string)link.TargetPath, (string)link.Arguments);
                }
                finally
                {
                    Marshal.FinalReleaseComObject(link);
                }
            }
            finally
            {
                Marshal.FinalReleaseComObject(shell);
            }
        }

        private Session LauncherWindow()
        {
            var progress = WindowsFinder.WaitForWindowByApp(LauncherUiProcess, window => window.Width > 200 && window.Height > 100, 30_000);
            Assert.IsNotNull(progress, "The launcher progress window did not appear.");
            Assert.IsTrue(progress.Has(By.AccessibilityId("CancelButton"), 15_000), "The progress window did not become interactive.");
            return progress;
        }

        private void AssertLaunchGlyph(Session progress, string application, string expected)
        {
            Step($"Reading the launch state of '{application}'.");
            var result = WaitHelper.WaitForStable(
                () =>
                {
                    var state = progress.FindAll<Element>(By.AccessibilityId("LaunchState_" + application), 0).SingleOrDefault();
                    return state is not null && state.Displayed ? state.GetValue() : string.Empty;
                },
                glyph => glyph == expected,
                15_000,
                requiredConsecutiveMatches: 2);
            Assert.IsTrue(result.Succeeded, $"'{application}' did not show the expected launch state glyph. Actual: '{result.LastObservation}'.");
        }

        private IEnumerable<JsonObject> PendingApplications(int count, string gate, string? firstStarted = null, string? lastStarted = null)
        {
            for (var index = 1; index <= count; index++)
            {
                var title = $"{State.Prefix}-pending-{index:00}";
                var started = index == 1 ? firstStarted : index == count ? lastStarted : null;
                var arguments = $"--title \"{title}\" --wait-event \"{gate}\"";
                if (started is not null)
                {
                    arguments += $" --started-event \"{started}\"";
                }

                yield return State.Application($"Pending fixture {index:00}", title, arguments);
            }
        }

        private static void WaitForLaunchCompletion(JsonObject project)
        {
            Assert.IsTrue(WaitForProcess(LauncherProcess, false, 60_000), "The workspace launch did not finish.");
            WorkspaceTestState.WaitForProject(
                WorkspaceTestState.Text(project, "id"),
                value => value["last-launched-time"]!.GetValue<long>() > 0);
        }

        private static void AssertPositioned(Session window, JsonObject application)
        {
            var handle = new IntPtr(window.WindowHandle);
            var minimized = application["minimized"]!.GetValue<bool>();
            var maximized = application["maximized"]!.GetValue<bool>();
            var target = application["position"]!;
            var result = WaitHelper.WaitForStable(
                () =>
                {
                    var stamped = WindowHelper.GetWindowPropertyValue(handle, "PowerToys_LaunchedByWorkspaces") != 0;
                    if (minimized || maximized)
                    {
                        return stamped && NativeMethods.IsIconic(handle) == minimized && WindowHelper.IsWindowMaximized(handle) == maximized;
                    }

                    var bounds = NormalizedOuterBounds(handle);
                    return stamped && !NativeMethods.IsIconic(handle) && !WindowHelper.IsWindowMaximized(handle) &&
                        Math.Abs(bounds.X - target["X"]!.GetValue<int>()) <= 2 &&
                        Math.Abs(bounds.Y - target["Y"]!.GetValue<int>()) <= 2 &&
                        Math.Abs(bounds.Width - target["width"]!.GetValue<int>()) <= 2 &&
                        Math.Abs(bounds.Height - target["height"]!.GetValue<int>()) <= 2;
                },
                match => match,
                60_000,
                requiredConsecutiveMatches: 3,
                pollIntervalMS: 150);
            Assert.IsTrue(
                result.Succeeded,
                $"Workspaces did not place '{window.WindowTitle}' as configured: {application}. " +
                $"Actual bounds: {WindowHelper.GetWindowBounds(handle)}, minimized: {NativeMethods.IsIconic(handle)}, maximized: {WindowHelper.IsWindowMaximized(handle)}.");
        }
    }
}

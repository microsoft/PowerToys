// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Workspaces.UITests
{
    [TestClass]
    [TestCategory("Workspaces")]
    [DoNotParallelize]
    public sealed partial class WorkspacesTests : UITestBase
    {
        private const string EditorProcess = "PowerToys.WorkspacesEditor";
        private const string LauncherProcess = "PowerToys.WorkspacesLauncher";
        private const string LauncherUiProcess = "PowerToys.WorkspacesLauncherUI";
        private static readonly string[] ModuleProcesses =
        [
            LauncherProcess,
            "PowerToys.WorkspacesWindowArranger",
            LauncherUiProcess,
            "PowerToys.WorkspacesSnapshotTool",
            EditorProcess,
        ];
        private static WorkspaceTestState? state;
        private Session? editor;

        public WorkspacesTests()
            : base(PowerToysModule.PowerToysSettings, enableModules: ["Workspaces"])
        {
        }

        protected override bool ReuseScopeAcrossTests => true;

        protected override IReadOnlyList<string> StaleProcessNames =>
            [.. base.StaleProcessNames, .. ModuleProcesses, "PowerToys.QuickAccess"];

        private static WorkspaceTestState State => state ?? throw new InvalidOperationException("Workspaces test state has not been prepared.");

        private Session Editor => editor ?? throw new InvalidOperationException("The editor has not been opened.");

        protected override void PrepareTestState()
        {
            StopModuleProcesses();
            state ??= new WorkspaceTestState();
            State.PrepareSettings();
        }

        [TestInitialize]
        public void PrepareWorkspace()
        {
            Assert.IsFalse(ElevationHelper.IsProcessElevated(Environment.ProcessId), "Run the Workspaces suite in a standard-user desktop.");
            StopModuleProcesses();
            State.Fixture.CloseAll();
            State.Reset();
            NavigateToSettings();
        }

        [TestCleanup]
        public async Task CleanUpWorkspace()
        {
            try
            {
                await CaptureFailureArtifactsBeforeCleanupAsync();
                if (TestContext.CurrentTestOutcome != UnitTestOutcome.Passed)
                {
                    AttachDiagnostics();
                }
            }
            finally
            {
                try
                {
                    StopModuleProcesses();
                }
                finally
                {
                    State.Fixture.CloseAll();
                }
            }
        }

        [ClassCleanup(ClassCleanupBehavior.EndOfClass)]
        public static void RestoreWorkspaceState()
        {
            try
            {
                StopModuleProcesses();
                StopSharedScope();
            }
            finally
            {
                state?.Dispose();
                state = null;
            }
        }

        private void NavigateToSettings()
        {
            Step("Navigating to Workspaces Settings.");
            if (!Session.Has(By.AccessibilityId("WorkspacesNavItem"), 500))
            {
                Session.Find<NavigationViewItem>(By.AccessibilityId("WindowingAndLayoutsNavItem"), 15_000).Invoke(msPostAction: 0);
            }

            Session.Find<NavigationViewItem>(By.AccessibilityId("WorkspacesNavItem"), 15_000).Invoke(msPostAction: 0);
            Assert.IsTrue(Session.Has(By.AccessibilityId("WorkspacesLaunchEditorButtonControl"), 15_000), "The Workspaces Settings page did not become ready.");
        }

        private ToggleSwitch ModuleToggle() =>
            Session.Find<Element>(By.AccessibilityId("WorkspacesEnableToggleControlHeaderText"), 15_000)
                .Find<ToggleSwitch>(By.Name("Workspaces"), 15_000);

        private void OpenEditor()
        {
            Step("Clicking Open editor in Settings.");
            var launch = Session.Find<Element>(By.AccessibilityId("WorkspacesLaunchEditorButtonControl"), 15_000);
            Assert.IsTrue(launch.Displayed && launch.Width > 0 && launch.Height > 0, "The launch card is not visible.");
            Session.EnsureForeground();
            Assert.IsTrue(
                WindowControl.WaitForForeground(new IntPtr(Session.WindowHandle), 10_000),
                $"Settings did not acquire foreground for the launch card: {WindowControl.GetForegroundWindowInfo()}.");
            // SettingsCard exposes a Button role but no InvokePattern; its pointer command is the user action.
            launch.Click(msPostAction: 0);
            AttachEditor();
        }

        private void AttachEditor()
        {
            editor = WindowsFinder.WaitForWindowByApp(EditorProcess, window => window.Width > 400 && window.Height > 300, 45_000);
            Assert.IsNotNull(editor, "Workspaces Editor did not open.");
            Assert.IsFalse(editor.IsElevated, "Workspaces Editor must run at the test user's integrity level.");
            WindowHelper.MaximizeWindow(new IntPtr(editor.WindowHandle));
            Assert.IsTrue(editor.Has(By.AccessibilityId("NewProjectButton"), 20_000), "The editor did not present its workspace list.");
        }

        private void CloseEditor()
        {
            Step("Closing Workspaces Editor and waiting for its process to exit.");
            Assert.IsTrue(WindowControl.TryCloseWindow(Editor.WindowHandle), "Could not close the editor window.");
            Assert.IsTrue(WaitForProcess(EditorProcess, false), "The editor process did not exit after closing its window.");
            editor = null;
        }

        private void EditWorkspace(string name, bool useMenu = false)
        {
            var card = WaitForCard(name);
            if (useMenu)
            {
                OpenWorkspaceMenu(card);
                var process = Microsoft.PowerToys.UITest.Next.Session.FromProcess(EditorProcess);
                var buttons = process.FindAll<Button>(By.Name("Edit"), 10_000).Where(button => button.Name == "Edit").ToArray();
                var menuButton = buttons.Single(button => button.Width > 0 && button.Width < 300);
                Step("Invoking the workspace's Edit menu command.");
                menuButton.Invoke(msPostAction: 0);
            }
            else
            {
                Step($"Invoking the workspace card '{name}'.");
                OpenCardForEditing(card);
            }

            Assert.IsTrue(Editor.Has(By.AccessibilityId("EditNameTextBox"), 15_000), "The editing page did not open.");
            Assert.AreEqual(name, Editor.Find<TextBox>(By.AccessibilityId("EditNameTextBox")).Value);
        }

        // Open a workspace's editing page with a real mouse click on the card's name region. The whole
        // card is the Edit button, and its left name area never overlaps the Launch/More buttons on the
        // right. WPF drops the cards' descendant automation peers after the list re-projects its items
        // (a save, sort, or filter), so a UIA invoke on the descendant Edit button is unreliable; the item
        // container keeps correct on-screen bounds, so a physical click on it always reaches the button.
        private void OpenCardForEditing(WorkspaceCard card)
        {
            Editor.EnsureForeground();
            Assert.IsTrue(
                WindowControl.WaitForForeground(new IntPtr(Editor.WindowHandle), 10_000),
                $"The editor did not acquire foreground to open a workspace card: {WindowControl.GetForegroundWindowInfo()}.");
            var x = card.X + Math.Clamp(card.Width / 8, 24, 120);
            var y = card.Y + (card.Height / 2);
            MouseHelper.MoveTo(x, y);
            Thread.Sleep(50);
            MouseHelper.LeftClick();
            Thread.Sleep(200);
        }

        private void OpenWorkspaceMenu(WorkspaceCard card)
        {
            Step("Opening the workspace actions menu.");

            // Scope the More button to the target card by its on-screen row so the correct card's menu
            // opens even when several cards are present. This relies on live descendant peers, so it is
            // only used on a freshly rendered list (before any save/sort/filter re-projects the items).
            var more = Editor.FindAll<Button>(By.AccessibilityId("MoreButton"), 10_000)
                .Where(button => button.Y >= card.Y && button.Y <= card.Y + card.Height)
                .ToArray();
            Assert.HasCount(1, more, "The workspace actions button was not uniquely addressable.");
            more[0].Invoke(msPostAction: 0);
            Assert.IsTrue(
                // The delete command's accessible name is the localized Resources.Delete, which reads "Remove".
                Microsoft.PowerToys.UITest.Next.Session.FromProcess(EditorProcess).Has<Button>(By.Name("Remove"), 10_000),
                "The workspace actions menu did not open.");
        }

        private void Search(string term)
        {
            Step($"Searching workspaces for '{term}'.");
            Editor.Find<TextBox>(By.AccessibilityId("SearchTextBox"), 15_000).SetText(term);
        }

        // Reproduce the product's snapshot geometry contract (workspaces-common WindowUtils::GetWindowRect):
        // take the raw Win32 GetWindowRect (outer window, including invisible resize borders) and convert
        // it to DPI-normalized/logical coordinates with DPIAware::InverseConvert (logical = physical * 96 / dpi).
        // This is the rectangle Workspaces persists and later restores via PlacementHelper::SizeWindowToRect,
        // so both capture and launch assertions must compare against it. DWM extended-frame bounds
        // (WindowHelper.GetVisibleBounds) are only correct for composed preview pixels, not saved placement.
        private static (int X, int Y, int Width, int Height) NormalizedOuterBounds(IntPtr handle)
        {
            var (left, top, right, bottom) = WindowHelper.GetWindowBounds(handle);
            var dpi = NativeMethods.GetDpiForWindow(handle);
            Assert.IsTrue(dpi > 0, $"Could not read DPI for fixture HWND {handle}.");

            var scale = 96f / dpi;
            var originX = left * scale;
            var originY = top * scale;
            var width = (right - left) * scale;
            var height = (bottom - top) * scale;
            var x = (int)Math.Round(originX);
            var y = (int)Math.Round(originY);
            return (x, y, (int)Math.Round(originX + width) - x, (int)Math.Round(originY + height) - y);
        }

        private WorkspaceCard[] WorkspaceCards()
        {
            // Read each workspace card from its container's AutomationId (bound to the workspace name via
            // the ItemsControl item-container style in MainPage.xaml) plus its on-screen bounds. WPF drops
            // the cards' descendant automation peers after a filter/sort/save re-projects the ItemsControl
            // source, but the item container itself stays in the tree with correct bounds, so it is the one
            // handle on a workspace's identity and location that survives every re-projection.
            var found = new List<WorkspaceCard>();

            void Walk(JsonElement element)
            {
                if (element.TryGetProperty("automationId", out var idProperty) &&
                    idProperty.GetString() is { } id &&
                    id.StartsWith(State.Prefix, StringComparison.Ordinal))
                {
                    found.Add(new WorkspaceCard(
                        id,
                        element.TryGetProperty("x", out var xp) ? xp.GetInt32() : 0,
                        element.TryGetProperty("y", out var yp) ? yp.GetInt32() : 0,
                        element.TryGetProperty("width", out var wp) ? wp.GetInt32() : 0,
                        element.TryGetProperty("height", out var hp) ? hp.GetInt32() : 0));
                }

                if (element.TryGetProperty("children", out var children) && children.ValueKind == JsonValueKind.Array)
                {
                    foreach (var child in children.EnumerateArray())
                    {
                        Walk(child);
                    }
                }
            }

            var tree = Editor.Inspect(depth: 10);
            if (tree.TryGetProperty("windows", out var windows) && windows.ValueKind == JsonValueKind.Array)
            {
                foreach (var window in windows.EnumerateArray())
                {
                    if (window.TryGetProperty("elements", out var elements) && elements.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var element in elements.EnumerateArray())
                        {
                            Walk(element);
                        }
                    }
                }
            }

            return [.. found.OrderBy(card => card.Y)];
        }

        private string[] WorkspaceNames() => WorkspaceCards().Select(card => card.Name).ToArray();

        private WorkspaceCard WaitForCard(string name)
        {
            var result = WaitHelper.WaitForStable(
                () => WorkspaceCards().Where(card => card.Name == name).ToArray(),
                cards => cards is { Length: 1 } && cards[0].Width > 0 && cards[0].Height > 0,
                timeoutMS: 15_000,
                requiredConsecutiveMatches: 2,
                pollIntervalMS: 200);
            Assert.IsTrue(result.Succeeded, $"The workspace card '{name}' did not become uniquely addressable.");
            return result.LastObservation![0];
        }

        private readonly record struct WorkspaceCard(string Name, int X, int Y, int Width, int Height);

        private void AssertWorkspaceNames(params string[] expected)
        {
            var ready = WaitHelper.WaitForStable(
                WorkspaceNames,
                actual => actual is not null && actual.Order(StringComparer.Ordinal).SequenceEqual(expected.Order(StringComparer.Ordinal)),
                timeoutMS: 15_000,
                requiredConsecutiveMatches: 2,
                pollIntervalMS: 150);
            Assert.IsTrue(
                ready.Succeeded,
                $"Expected workspaces [{string.Join(", ", expected)}], observed [{string.Join(", ", ready.LastObservation ?? [])}].");
        }

        private Element ExpandApplication(string name)
        {
            Step($"Expanding application '{name}'.");
            var row = Editor.Find<Element>(By.AccessibilityId(name), 15_000);
            if (!string.Equals(row.GetProperty("ExpandCollapseState"), "Expanded", StringComparison.OrdinalIgnoreCase))
            {
                row.Invoke(msPostAction: 0);
            }

            Assert.IsTrue(row.Find<TextBox>(By.AccessibilityId("CommandLineTextBox"), 15_000).IsEnabled, "Application details did not expand.");
            return row;
        }

        private void SaveWorkspace()
        {
            Step("Saving the workspace.");
            var save = Editor.Find<Button>(By.AccessibilityId("SaveButton"), 15_000);
            Assert.IsTrue(save.IsEnabled, "The workspace cannot be saved.");
            save.Invoke(msPostAction: 0);
            Assert.IsTrue(Editor.Has(By.AccessibilityId("NewProjectButton"), 15_000), "Save did not return to the workspace list.");
        }

        private Session StartCapture()
        {
            Step("Creating a workspace from the current desktop.");
            Editor.Find<Button>(By.AccessibilityId("NewProjectButton"), 15_000).Invoke(msPostAction: 0);
            return WaitForCaptureWindow();
        }

        private Session WaitForCaptureWindow()
        {
            var capture = WindowsFinder.WaitForWindowByApp(
                EditorProcess,
                window => window.Hwnd != Editor.WindowHandle && window.Title == "Snapshot Creator",
                60_000);
            Assert.IsNotNull(capture, "The snapshot control window did not appear.");
            Assert.IsTrue(capture.Has(By.AccessibilityId("SnapshotButton"), 15_000), "The snapshot window is not ready.");
            return capture;
        }

        private JsonObject Capture(Session capture, string? expectedAddedTitle = null)
        {
            var previousId = File.Exists(WorkspaceTestState.TemporaryPath)
                ? WorkspaceTestState.Text(WorkspaceTestState.ReadObject(WorkspaceTestState.TemporaryPath), "id")
                : null;
            Step("Capturing the desktop.");
            capture.Find<Button>(By.AccessibilityId("SnapshotButton"), 15_000).Invoke(msPostAction: 0);
            Assert.IsTrue(Editor.Has(By.AccessibilityId("EditNameTextBox"), 45_000), "The captured workspace did not open in the editing page.");
            WindowHelper.MaximizeWindow(new IntPtr(Editor.WindowHandle));

            // Recapture keeps the old editing page visible while the snapshot tool runs. Require the new
            // snapshot's identity/content, then the editor's own completed-recapture state before saving.
            var result = WaitHelper.WaitForStable(
                () => File.Exists(WorkspaceTestState.TemporaryPath)
                    ? WorkspaceTestState.ReadObject(WorkspaceTestState.TemporaryPath)
                    : null,
                snapshot => snapshot is not null &&
                    WorkspaceTestState.Text(snapshot, "id") != previousId &&
                    snapshot["applications"] is JsonArray { Count: > 0 } apps &&
                    (expectedAddedTitle is null || apps.Any(app => WorkspaceTestState.Text(app!, "title") == expectedAddedTitle)),
                timeoutMS: 30_000,
                requiredConsecutiveMatches: 2,
                pollIntervalMS: 300,
                shouldRetryException: error => error is IOException or JsonException);
            Assert.IsTrue(result.Succeeded, $"A fresh snapshot containing '{expectedAddedTitle ?? "the captured applications"}' was not written.");
            if (expectedAddedTitle is not null)
            {
                Assert.IsTrue(
                    Editor.Find<Button>(By.AccessibilityId("RevertButton"), 15_000).WaitForProperty("IsEnabled", "True", 30_000),
                    "The editor did not finish applying the recaptured workspace.");
            }

            return result.LastObservation!;
        }

        private static bool WaitForProcess(string name, bool expected, int timeoutMS = 20_000)
        {
            return WaitHelper.WaitForStable(
                () =>
                {
                    var processes = Process.GetProcessesByName(name);
                    try
                    {
                        return processes.Length > 0;
                    }
                    finally
                    {
                        foreach (var process in processes)
                        {
                            process.Dispose();
                        }
                    }
                },
                running => running == expected,
                timeoutMS,
                requiredConsecutiveMatches: 3,
                pollIntervalMS: 150).Succeeded;
        }

        private static void StopModuleProcesses()
        {
            foreach (var name in ModuleProcesses)
            {
                Assert.IsTrue(WindowControl.TryKillProcessTreeByNameAndWait(name, 15_000), $"Could not stop test-owned {name}.");
            }
        }

        private void Step(string message) => TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] {message}");

        private void AttachDiagnostics()
        {
            try
            {
                var directory = TestContext.TestRunResultsDirectory ?? throw new InvalidOperationException("No test results directory is available.");
                Directory.CreateDirectory(directory);
                foreach (var path in new[] { WorkspaceTestState.WorkspacesPath, WorkspaceTestState.TemporaryPath, WorkspaceTestState.SettingsPath })
                {
                    if (File.Exists(path))
                    {
                        var copy = Path.Combine(directory, $"{TestContext.TestName}-{Guid.NewGuid():N}-{Path.GetFileName(path)}");
                        File.Copy(path, copy);
                        TestContext.AddResultFile(copy);
                    }
                }

                if (editor is not null && WaitForProcess(EditorProcess, true, 500))
                {
                    var tree = Path.Combine(directory, $"{TestContext.TestName}-{Guid.NewGuid():N}-editor.json");
                    File.WriteAllText(tree, editor.Inspect(depth: 20).GetRawText());
                    TestContext.AddResultFile(tree);
                }

                Step($"Foreground: {WindowControl.GetForegroundWindowInfo()}");
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException or AssertFailedException or Win32Exception or JsonException or TimeoutException)
            {
                TestContext.WriteLine($"Could not attach supplementary Workspaces diagnostics: {error.Message}");
            }
        }
    }
}

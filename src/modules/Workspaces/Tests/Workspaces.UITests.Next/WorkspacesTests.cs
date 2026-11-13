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
        private const string SettingsProcess = "PowerToys.Settings";
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
        private Session? settingsUi;

        public WorkspacesTests()
            : base(PowerToysModule.PowerToysSettings, enableModules: ["Workspaces"])
        {
        }

        protected override bool ReuseScopeAcrossTests => true;

        protected override IReadOnlyList<string> StaleProcessNames =>
            [.. base.StaleProcessNames, .. ModuleProcesses, "PowerToys.QuickAccess"];

        private static WorkspaceTestState State => state ?? throw new InvalidOperationException("Workspaces test state has not been prepared.");

        private Session Editor => editor ?? throw new InvalidOperationException("The editor has not been opened.");

        // The PowerToys.Settings process can replace its top-level window while it finishes initializing:
        // the HWND the base Session bound to at launch is destroyed and a fresh one is created (observed in
        // CI as a still-initializing "PowerToys Settings (Not Responding)" window with a new handle). A
        // window-scoped session pinned to the original HWND then fails every lookup with "Window HWND ...
        // not found". Route Settings UIA reads through a process-scoped session (-a) so navigation and
        // toggles survive the swap — winappcli re-resolves the live window on every call.
        private Session SettingsUi => settingsUi ??= Microsoft.PowerToys.UITest.Next.Session.FromProcess(SettingsProcess, timeoutMS: 30_000);

        [ClassInitialize]
        public static void ValidateDesktop(TestContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            Assert.IsFalse(ElevationHelper.IsProcessElevated(Environment.ProcessId), "Run Test Explorer and the Workspaces UI tests without elevation.");
            var target = WorkspacesDisplay.GetPrimaryTarget();
            context.WriteLine($"Workspaces placement target: {target.DeviceName}, monitor {target.Number}, DPI {target.Dpi}, physical size {target.MonitorWidth}x{target.MonitorHeight}, logical width {target.LogicalMonitorRight - target.LogicalMonitorLeft}.");
        }

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

            // Refresh the topology between cases; the class preflight runs before base initialization.
            State.ResolveTarget();
            StopModuleProcesses();
            State.Fixture.CloseAll();
            State.Reset();
            settingsUi = null;
            NavigateToSettings();
        }

        [TestCleanup]
        public async Task CleanUpWorkspace()
        {
            // MSTest appends cleanup errors to the original failure and fails passing tests on leaks.
            // Capture evidence first; fixture cleanup must still run if stopping module processes fails.
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
            if (state is null)
            {
                return;
            }

            try
            {
                StopModuleProcesses();

                // Defensive and idempotent: stop before file restoration even if inherited cleanup already ran.
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

            // Wait out any startup window replacement before driving the UI, so navigation runs against the
            // window Settings actually settles on rather than the transient handle it was launched with.
            _ = SettingsWindow();

            if (!SettingsUi.Has(By.AccessibilityId("WorkspacesNavItem"), 500))
            {
                SettingsUi.Find<NavigationViewItem>(By.AccessibilityId("WindowingAndLayoutsNavItem"), 15_000).Invoke(msPostAction: 0);
            }

            SettingsUi.Find<NavigationViewItem>(By.AccessibilityId("WorkspacesNavItem"), 15_000).Invoke(msPostAction: 0);
            Assert.IsTrue(SettingsUi.Has(By.AccessibilityId("WorkspacesLaunchEditorButtonControl"), 15_000), "The Workspaces Settings page did not become ready.");
        }

        // Freshly resolve the current PowerToys.Settings main window for the few physical operations that
        // need a real handle (foreground gating, coordinate clicks, and DPI seeding for fixture placement).
        // requiredConsecutiveMatches guards against reading a handle that startup is about to replace.
        private IntPtr SettingsWindow()
        {
            var result = WaitHelper.WaitForStable(
                () => WindowsFinder.ListByApp(SettingsProcess)
                    .Where(window => window.ClassName == "WinUIDesktopWin32WindowClass" && window.Width > 400 && window.Height > 300)
                    .OrderByDescending(window => (long)window.Width * window.Height)
                    .Select(window => (long?)window.Hwnd)
                    .FirstOrDefault(),
                hwnd => hwnd is > 0,
                timeoutMS: 30_000,
                requiredConsecutiveMatches: 2,
                pollIntervalMS: 200);
            Assert.IsTrue(result.Succeeded && result.LastObservation is > 0, "The PowerToys Settings window did not stabilize.");
            return new IntPtr(result.LastObservation!.Value);
        }

        private ToggleSwitch ModuleToggle() =>
            SettingsUi.Find<Element>(By.AccessibilityId("WorkspacesEnableToggleControlHeaderText"), 15_000)
                .Find<ToggleSwitch>(By.Name("Workspaces"), 15_000);

        private void OpenEditor(bool maximize = true)
        {
            Step("Opening editor from Settings with Enter.");
            SettingsUi.EnsureForeground();
            Assert.IsTrue(
                WindowControl.WaitForForeground(SettingsWindow(), 10_000),
                $"Settings did not acquire foreground for the launch card: {WindowControl.GetForegroundWindowInfo()}.");

            var launch = SettingsUi.Find<Button>(By.AccessibilityId("WorkspacesLaunchEditorButtonControl"), 15_000);
            Assert.AreEqual("WorkspacesLaunchEditorButtonControl", launch.AutomationId, "The Settings launch action resolved to the wrong control.");
            Assert.IsTrue(launch.Displayed && launch.IsEnabled, "The launch card is not available.");

            // SettingsCard handles Enter through ButtonBase even when its automation peer does not
            // expose InvokePattern. Keep activation on the actual Settings card and command path.
            launch.Focus();
            Assert.IsTrue(launch.WaitForProperty("HasKeyboardFocus", "True", 5_000), "The Settings launch card did not receive keyboard focus.");
            KeyboardHelper.SendKeys(Key.Enter);
            AttachEditor(maximize);
        }

        private void AttachEditor(bool maximize = true)
        {
            editor = WindowsFinder.WaitForWindowByApp(EditorProcess, window => window.Width > 400 && window.Height > 300, 45_000);
            Assert.IsNotNull(editor, "Workspaces Editor did not open.");
            Assert.IsFalse(editor.IsElevated, "Workspaces Editor must run at the test user's integrity level.");
            if (maximize)
            {
                WindowHelper.MaximizeWindow(new IntPtr(editor.WindowHandle));
            }

            Assert.IsTrue(editor.Has(By.AccessibilityId("NewProjectButton"), 20_000), "The editor did not present its workspace list.");
        }

        private void CloseEditor()
        {
            Step("Closing Workspaces Editor and waiting for its process to exit.");
            Assert.IsTrue(WindowControl.TryCloseWindow(Editor.WindowHandle), "Could not close the editor window.");
            Assert.IsTrue(WaitForProcess(EditorProcess, false), "The editor process did not exit after closing its window.");
            editor = null;
        }

        private void EditWorkspace(string name, bool useKeyboard = false)
        {
            var card = WaitForCard(name);
            if (useKeyboard)
            {
                Step($"Opening workspace '{name}' with the keyboard.");
                Editor.EnsureForeground();
                var element = Editor.Find<Element>(By.AccessibilityId(card.Id));
                element.Focus();
                Assert.IsTrue(element.WaitForProperty("HasKeyboardFocus", "True", 5_000), "The workspace card did not receive keyboard focus.");
                KeyboardHelper.SendKeys(Key.Enter);
            }
            else
            {
                Step($"Invoking the workspace card '{name}'.");
                OpenCardForEditing(name);
            }

            Assert.IsTrue(Editor.Has(By.AccessibilityId("EditNameTextBox"), 15_000), "The editing page did not open.");
            Assert.AreEqual(name, Editor.Find<TextBox>(By.AccessibilityId("EditNameTextBox")).Value);
        }

        // Open a workspace's editing page with a real mouse click on the card's name region. The whole
        // card opens the editor, and its left name area never overlaps the Launch button on the right.
        // Reacquire its stable workspace ID after a save, sort, or filter changes the list.
        // A single physical click can still be silently dropped while the editor is settling foreground /
        // z-order right after it opens (observed in CI), so retry a bounded number of times: re-acquire the
        // card by name (its bounds survive re-projection), click, and stop the moment the editing page's
        // ready signal (EditNameTextBox) appears. Only click while the workspace list is still active, so a
        // retry can never land a click on the editing page itself — this stays idempotent, never a longer
        // arbitrary sleep.
        private void OpenCardForEditing(string name)
        {
            using var physicalCoordinates = new PhysicalCoordinateScope();
            const int maxAttempts = 4;
            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                if (Editor.Has(By.AccessibilityId("EditNameTextBox"), 500))
                {
                    return;
                }

                if (!Editor.Has(By.AccessibilityId("NewProjectButton"), 500))
                {
                    // The list page is gone but the editing page has not rendered yet: a prior click
                    // registered and the page is transitioning. Wait for the ready signal, do not re-click.
                    if (Editor.Has(By.AccessibilityId("EditNameTextBox"), 3_000))
                    {
                        return;
                    }

                    continue;
                }

                var card = WaitForCard(name);
                Editor.EnsureForeground();
                Assert.IsTrue(
                    WindowControl.WaitForForeground(new IntPtr(Editor.WindowHandle), 10_000),
                    $"The editor did not acquire foreground to open a workspace card: {WindowControl.GetForegroundWindowInfo()}.");
                var x = card.X + Math.Clamp(card.Width / 8, 24, 120);
                var y = card.Y + (card.Height / 2);
                Step($"Clicking the workspace card '{name}' (attempt {attempt}).");
                MouseHelper.MoveTo(x, y);
                Thread.Sleep(50);
                MouseHelper.LeftClick();
                if (Editor.Has(By.AccessibilityId("EditNameTextBox"), 3_000))
                {
                    return;
                }
            }
        }

        private void Search(string term)
        {
            Step($"Searching workspaces for '{term}'.");
            var input = SearchInput();
            input.Focus();
            input.SetText(term);
            Assert.IsTrue(input.WaitForValue(term, timeoutMS: 5_000), "The search input did not accept the query.");
        }

        private TextBox SearchInput()
        {
            var search = Editor.Find<Element>(By.AccessibilityId("SearchTextBox"), 15_000);
            var inputs = FindDescendants<TextBox>(search, "Edit");
            Assert.HasCount(1, inputs, "The AutoSuggestBox should contain one editable search input.");
            return inputs[0];
        }

        private T[] FindDescendants<T>(Element parent, string controlType, string? automationId = null)
            where T : Element, new()
        {
            // Element.Find forwards to a window-wide lookup in this harness. Inspect the selected
            // subtree explicitly before resolving its child slugs, so repeated IDs stay scoped.
            var tree = WinappCli.InvokeJson(
                "ui",
                "inspect",
                parent.Selector,
                "-w",
                Editor.WindowHandle.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "--json",
                "-d",
                "20");
            var selectors = new List<By>();
            void Walk(JsonElement element)
            {
                var selector = element.TryGetProperty("selector", out var selectorProperty) ? selectorProperty.GetString() : null;
                if (selector is not null && selector != parent.Selector &&
                    element.TryGetProperty("type", out var typeProperty) && typeProperty.GetString() == controlType &&
                    (automationId is null || (element.TryGetProperty("automationId", out var idProperty) && idProperty.GetString() == automationId)))
                {
                    // Inspect returns an AutomationId for unique controls and a semantic slug for
                    // repeated controls. Resolve IDs through search so a partial parent ID cannot
                    // cause inspect to return that parent instead of the requested control.
                    selectors.Add(element.TryGetProperty("automationId", out var automationIdProperty) && selector == automationIdProperty.GetString()
                        ? By.AccessibilityId(selector)
                        : By.Slug(selector));
                }

                if (element.TryGetProperty("children", out var children) && children.ValueKind == JsonValueKind.Array)
                {
                    foreach (var child in children.EnumerateArray())
                    {
                        Walk(child);
                    }
                }
            }

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

            return selectors.Select(selector => Editor.Find<T>(selector, 0)).ToArray();
        }

        // Reproduce the product's snapshot geometry contract (workspaces-common WindowUtils::GetWindowRect):
        // take the raw Win32 GetWindowRect (outer window, including invisible resize borders) and convert
        // it to DPI-normalized/logical coordinates with DPIAware::InverseConvert (logical = physical * 96 / dpi).
        // This is the rectangle Workspaces persists and later restores via PlacementHelper::SizeWindowToRect,
        // so both capture and launch assertions must compare against it. DWM extended-frame bounds
        // (WindowHelper.GetVisibleBounds) are only correct for composed preview pixels, not saved placement.
        private static (int X, int Y, int Width, int Height) NormalizedOuterBounds(IntPtr handle)
        {
            using var physicalCoordinates = new PhysicalCoordinateScope();
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
            Assert.IsTrue(
                Editor.FindAll<Button>(By.AccessibilityId("NewProjectButton"), 0).Any(button => button.Displayed),
                "Workspace cards can only be read from the visible overview page.");

            // ItemsRepeater does not expose an automation peer. Read the actual SettingsCard buttons
            // by their GUID and test name instead; TextBlocks and search suggestions cannot match.
            var found = new List<WorkspaceCard>();

            void Walk(JsonElement element)
            {
                var id = element.TryGetProperty("automationId", out var idProperty) ? idProperty.GetString() : null;
                if (element.TryGetProperty("type", out var typeProperty) && typeProperty.GetString() == "Button" &&
                    Guid.TryParse(id, out _) &&
                    element.TryGetProperty("name", out var nameProperty) &&
                    nameProperty.GetString() is { } name && name.StartsWith(State.Prefix, StringComparison.Ordinal))
                {
                    found.Add(new WorkspaceCard(
                        id!,
                        name,
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

        private readonly record struct WorkspaceCard(string Id, string Name, int X, int Y, int Width, int Height);

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
            var expanders = FindDescendants<Button>(row, "Button")
                .Where(button => button.ClassName == "Microsoft.UI.Xaml.Controls.Expander").ToArray();
            Assert.HasCount(1, expanders, "The application row should expose one expandable header.");
            var expander = expanders[0];
            if (!string.Equals(expander.GetProperty("ExpandCollapseState"), "Expanded", StringComparison.OrdinalIgnoreCase))
            {
                expander.Invoke(msPostAction: 0);
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
            AssertWorkspaceListReady();
        }

        private void AssertWorkspaceListReady()
        {
            var ready = WaitHelper.WaitForStable(
                () => Editor.FindAll<Button>(By.AccessibilityId("NewProjectButton"), 0)
                    .Any(button => button.Displayed),
                visible => visible,
                15_000,
                requiredConsecutiveMatches: 2);
            Assert.IsTrue(ready.Succeeded, "The editor did not return to the visible workspace list.");
            Assert.IsFalse(Editor.Has(By.AccessibilityId("EditNameTextBox"), 500), "An old editing page remained active after returning to the list.");
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

            var snapshot = result.LastObservation!;
            State.TrackProjectIcon(WorkspaceTestState.Text(snapshot, "id"));
            return snapshot;
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

// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
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

        private static WorkspaceProfileSnapshot? profileLease;
        private static WorkspaceTestState? state;
        private static WorkspaceShortcutJournalGuard? existingShortcutJournals;
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
        private Session SettingsUi => settingsUi ??= OpenWorkspacesSettings();

        [ClassInitialize]
        public static void InitializeWorkspaceSuite(TestContext context)
        {
            ValidateDesktop(context);
            try
            {
                existingShortcutJournals = WorkspaceShortcutJournalGuard.AcquireForProfile(WorkspaceTestState.DirectoryPath);
            }
            catch
            {
                profileLease!.Dispose();
                profileLease = null;
                throw;
            }
        }

        [ClassCleanup(ClassCleanupBehavior.EndOfClass)]
        public static void CleanUpWorkspaceSuite()
        {
            using var journals = Interlocked.Exchange(ref existingShortcutJournals, null);
            RestoreWorkspaceState();
        }

        private static void ValidateDesktop(TestContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            Assert.IsFalse(ElevationHelper.IsProcessElevated(Environment.ProcessId), "Run Test Explorer and the Workspaces UI tests without elevation.");
            var target = WorkspacesDisplay.GetPrimaryTarget();
            context.WriteLine($"Workspaces placement target: {target.DeviceName}, monitor {target.Number}, DPI {target.Dpi}, physical size {target.MonitorWidth}x{target.MonitorHeight}, logical width {target.LogicalMonitorRight - target.LogicalMonitorLeft}.");

            // Hold the profile before inherited initialization can close another testhost's windows.
            profileLease = new WorkspaceProfileSnapshot(WorkspaceTestState.DirectoryPath);
        }

        protected override void PrepareTestState()
        {
            StopModuleProcesses();
            AssertSettingsProcessesStopped();
            state ??= new WorkspaceTestState(profileLease ?? throw new InvalidOperationException("The Workspaces profile has not been reserved."));
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
        }

        [TestCleanup]
        public async Task CleanUpWorkspace()
        {
            if (state is null)
            {
                // Initialization failed before the user's files were isolated.
                return;
            }

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

        public static void RestoreWorkspaceState()
        {
            var completedState = state;
            var lease = profileLease;
            state = null;
            profileLease = null;
            if (completedState is null)
            {
                lease?.Dispose();
                return;
            }

            try
            {
                StopModuleProcesses();

                // Defensive and idempotent: stop before file restoration even if inherited cleanup already ran.
                StopSharedScope();
                AssertSettingsProcessesStopped();
            }
            catch
            {
                lease!.LeaveForRecovery();
                throw;
            }
            finally
            {
                completedState.Dispose();
            }
        }

        private static void AssertSettingsProcessesStopped()
        {
            Assert.IsTrue(WaitForProcess(SettingsProcess, false), "PowerToys Settings still has access to the Workspaces profile.");
            Assert.IsTrue(WaitForProcess("PowerToys", false), "PowerToys Runner still has access to the Workspaces profile.");
        }

        private Session OpenWorkspacesSettings()
        {
            Step("Navigating to Workspaces Settings.");

            const string pageControl = "WorkspacesLaunchEditorButtonControl";
            const string workspaceItem = "WorkspacesNavItem";
            const string groupItem = "WindowingAndLayoutsNavItem";
            string? selectedWindow = null;
            var ready = WaitHelper.WaitForStable(
                () =>
                {
                    string window = SettingsWindow().ToInt64().ToString(System.Globalization.CultureInfo.InvariantCulture);
                    if (FindControl(window, pageControl, "Button") is not null)
                    {
                        return true;
                    }

                    if (window != selectedWindow)
                    {
                        if (FindControl(window, workspaceItem, "ListItem") is not null)
                        {
                            // Select by AutomationId within this HWND. The item's runtime slug can
                            // change between search and activation while the navigation pane settles.
                            WinappCli.InvokeAssertSuccess("ui", "invoke", workspaceItem, "-w", window, "--action", "select", "--json");
                            selectedWindow = window;
                        }
                        else if (FindControl(window, groupItem, "ListItem") is { } group && UiString(group, "expandState") == "collapsed")
                        {
                            WinappCli.InvokeAssertSuccess("ui", "invoke", groupItem, "-w", window, "--action", "expand", "--json");
                        }
                    }

                    return false;
                },
                arrived => arrived,
                timeoutMS: 30_000,
                pollIntervalMS: 250,
                shouldRetryException: error => ShellMenu.IsTransientElementException(error) ||
                    (error is AssertFailedException && error.Message.Contains("internal_error", StringComparison.OrdinalIgnoreCase) &&
                     error.Message.Contains("Window HWND ", StringComparison.OrdinalIgnoreCase) &&
                     error.Message.Contains("not found or not accessible", StringComparison.OrdinalIgnoreCase)));
            Assert.IsTrue(ready.Succeeded, $"The Workspaces Settings page did not become ready. {ready.LastException?.Message}");
            return Microsoft.PowerToys.UITest.Next.Session.FromProcess(SettingsProcess, timeoutMS: 30_000);

            static JsonElement? FindControl(string window, string automationId, string controlType)
            {
                var hits = WinappCli.InvokeJson("ui", "search", automationId, "-w", window, "--type", controlType, "--json");
                if (!hits.TryGetProperty("matches", out var matches))
                {
                    return null;
                }

                return matches.EnumerateArray()
                    .Where(hit => UiString(hit, "automationId") == automationId)
                    .Select(hit => (JsonElement?)hit)
                    .SingleOrDefault();
            }
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

            AssertWorkspaceListReady();
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
                Step($"Opening the workspace card '{name}' with the mouse.");
                OpenCardForEditing(card);
            }

            Assert.IsTrue(Editor.Has(By.AccessibilityId("EditNameTextBox"), 15_000), "The editing page did not open.");
            Assert.AreEqual(name, Editor.Find<TextBox>(By.AccessibilityId("EditNameTextBox")).Value);
        }

        private void OpenCardForEditing(WorkspaceCard target)
        {
            using var physicalCoordinates = new PhysicalCoordinateScope();
            var handle = new IntPtr(Editor.WindowHandle);
            const int maxAttempts = 4;
            int clicks = 0;
            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                if (Editor.Has(By.AccessibilityId("EditNameTextBox"), 500))
                {
                    break;
                }

                if (!Editor.Has(By.AccessibilityId("NewProjectButton"), 500))
                {
                    // The list page is gone but the editing page has not rendered yet: a prior click
                    // registered and the page is transitioning. Wait for the ready signal, do not re-click.
                    if (Editor.Has(By.AccessibilityId("EditNameTextBox"), 3_000))
                    {
                        break;
                    }

                    continue;
                }

                Editor.EnsureForeground();
                if (!WindowControl.WaitForForeground(handle, 2_500))
                {
                    Step($"Card {target.Id}, attempt {attempt}: editor HWND {handle} did not acquire foreground (HWND {WindowControl.GetForegroundWindowHandle()}).");
                    continue;
                }

                // Bringing the editor forward can restore it. Read the card's bounds from the active overview afterward.
                var cards = WorkspaceCards()?.Where(candidate => candidate.Id == target.Id).ToArray();
                if (cards is not [var card] || card.Name != target.Name)
                {
                    Step($"Card {target.Id}, attempt {attempt}: the overview did not expose the requested card.");
                    continue;
                }

                var area = card.VisibleEditArea;
                if (area.Width <= 0 || area.Height <= 0)
                {
                    // This card was realized outside the scroll viewport. The pinned CLI also
                    // supports a scrollable ancestor when the card lacks ScrollItemPattern.
                    try
                    {
                        WinappCli.InvokeAssertSuccess("ui", "scroll-into-view", card.Id, "-w", Editor.WindowHandleArg, "--json");
                    }
                    catch (AssertFailedException error) when (ShellMenu.IsTransientElementException(error))
                    {
                        Step($"Card {card.Id} was recycled during scrolling. Reacquiring it on the overview.");
                    }

                    continue;
                }

                var x = Math.Clamp(card.Bounds.X + Math.Clamp(card.Bounds.Width / 8, 24, 120), area.Left, area.Right - 1);
                var y = area.Top + (area.Height / 2);
                if (!WindowControl.IsPointOwnedByWindow(handle, x, y))
                {
                    Step($"Card {target.Id}, attempt {attempt}: ({x}, {y}) was covered before moving the pointer. Foreground HWND {WindowControl.GetForegroundWindowHandle()}.");
                    continue;
                }

                Step($"Card {target.Id}, attempt {attempt}: targeting ({x}, {y}) on editor HWND {handle}.");
                MouseHelper.MoveTo(x, y);
                Thread.Sleep(50);
                var afterMove = WorkspaceCards()?.Where(candidate => candidate.Id == target.Id).ToArray();
                if (afterMove is not [var current] || current != card)
                {
                    Step($"Skipped mouse-down. The overview, card identity, or card bounds changed after moving to ({x}, {y}).");
                    continue;
                }

                var cursor = MouseHelper.GetMousePosition();
                var foreground = WindowControl.GetForegroundWindowHandle();
                bool owned = WindowControl.IsPointOwnedByWindow(handle, cursor.X, cursor.Y);
                if (cursor != (x, y) || foreground != handle || !owned)
                {
                    Step($"Skipped mouse-down. Cursor {cursor}, foreground HWND {foreground}, editor owns cursor point: {owned}.");
                    continue;
                }

                MouseHelper.LeftClick();
                clicks++;
                if (Editor.Has(By.AccessibilityId("EditNameTextBox"), 3_000))
                {
                    break;
                }
            }

            Assert.IsTrue(clicks > 0, $"No mouse click was sent to card {target.Id} in {maxAttempts} attempts. Editor HWND {handle}, foreground HWND {WindowControl.GetForegroundWindowHandle()}.");
            Assert.IsTrue(
                Editor.Has(By.AccessibilityId("EditNameTextBox"), 15_000),
                $"Card {target.Id} did not open. Sent {clicks} mouse clicks in {maxAttempts} attempts. Editor HWND {handle}, foreground HWND {WindowControl.GetForegroundWindowHandle()}.");
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
            var search = Editor.Find<Element>(By.AccessibilityId("SearchBox"), 15_000);
            var inputs = FindDescendants<TextBox>(search, "Edit");
            Assert.HasCount(1, inputs, "The AutoSuggestBox should contain one editable search input.");
            return inputs[0];
        }

        private T[] FindDescendants<T>(Element parent, string controlType, string? automationId = null)
            where T : Element, new()
        {
            T[]? ReadChildren()
            {
                // winappcli can return the whole window when the requested parent was recycled.
                var tree = WinappCli.InvokeJson("ui", "inspect", parent.Selector, "-w", Editor.WindowHandleArg, "--json", "-d", "20");
                var roots = EditorRoots(tree).ToArray();
                if (roots.Length != 1 || UiString(roots[0], "selector") != parent.Selector)
                {
                    return null;
                }

                var matches = new List<T>();
                foreach (var element in Subtree(roots[0]).Skip(1))
                {
                    if (UiString(element, "type") != controlType || (automationId is not null && UiString(element, "automationId") != automationId))
                    {
                        continue;
                    }

                    // Unique AutomationIds use search, repeated controls use their exact runtime slug.
                    if (UiString(element, "selector") is not { Length: > 0 } selector)
                    {
                        return null;
                    }

                    var by = selector == UiString(element, "automationId") ? By.AccessibilityId(selector) : By.Slug(selector);
                    var resolved = Editor.FindAll<T>(by, 0).Where(hit => hit.Selector == selector).ToArray();
                    if (resolved.Length != 1)
                    {
                        return null;
                    }

                    matches.Add(resolved[0]);
                }

                return [.. matches];
            }

            var ready = WaitHelper.WaitForStable(ReadChildren, matches => matches is not null, 5_000, shouldRetryException: IsStaleElement);
            Assert.IsTrue(ready.Succeeded, $"Could not resolve descendants of '{parent.Selector}' in the editor.");
            return ready.LastObservation!;
        }

        private IEnumerable<JsonElement> EditorRoots(JsonElement tree) =>
            WindowRoots(tree).Where(root => root.Handle == Editor.WindowHandle).Select(root => root.Element);

        private static IEnumerable<(long Handle, JsonElement Element)> WindowRoots(JsonElement tree)
        {
            // A stale target can return a JSON error envelope while the overview is being rebuilt.
            if (!tree.TryGetProperty("windows", out var windows))
            {
                yield break;
            }

            foreach (var window in windows.EnumerateArray())
            {
                if (window.TryGetProperty("elements", out var elements))
                {
                    var handle = window.GetProperty("hwnd").GetInt64();
                    foreach (var element in elements.EnumerateArray())
                    {
                        yield return (handle, element);
                    }
                }
            }
        }

        private static IEnumerable<JsonElement> Subtree(JsonElement element)
        {
            yield return element;
            if (element.TryGetProperty("children", out var children))
            {
                foreach (var descendant in children.EnumerateArray().SelectMany(Subtree))
                {
                    yield return descendant;
                }
            }
        }

        private static string? UiString(JsonElement element, string property) =>
            element.TryGetProperty(property, out var value) ? value.GetString() : null;

        private static int UiCoordinate(JsonElement element, string property) =>
            element.TryGetProperty(property, out var value) ? (int)Math.Round(value.GetDouble()) : 0;

        private static bool IsVisible(JsonElement element) =>
            (!element.TryGetProperty("isOffscreen", out var offscreen) || !offscreen.GetBoolean()) &&
            UiCoordinate(element, "width") > 0 && UiCoordinate(element, "height") > 0;

        private static bool IsStaleElement(Exception error) =>
            error is AssertFailedException && error.Message.Contains("stale_element", StringComparison.OrdinalIgnoreCase);

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

        private WorkspaceCard[]? WorkspaceCards() => WorkspaceCards(out _);

        private WorkspaceCard[]? WorkspaceCards(out string? scrollSelector)
        {
            scrollSelector = null;

            // Read the overview and its cards in the same frame so navigation cannot turn a
            // temporarily missing list into an observation of an empty workspace.
            JsonElement[] elements;
            try
            {
                elements = EditorRoots(Editor.Inspect(depth: 20)).SelectMany(Subtree).ToArray();
            }
            catch (AssertFailedException error) when (IsStaleElement(error))
            {
                return null;
            }

            if (!elements.Any(element => UiString(element, "type") == "Button" && UiString(element, "automationId") == "NewProjectButton" && IsVisible(element)) ||
                elements.Any(element => UiString(element, "automationId") == "EditNameTextBox"))
            {
                return null;
            }

            var viewport = elements.FirstOrDefault(element => UiString(element, "className") == "ScrollViewer" &&
                Subtree(element).Any(IsWorkspaceCard));
            Rectangle viewportBounds = Rectangle.Empty;
            if (viewport.ValueKind == JsonValueKind.Object && IsVisible(viewport))
            {
                viewportBounds = UiBounds(viewport);
                if (UiString(viewport, "scrollDir")?.Contains('v') == true)
                {
                    scrollSelector = UiString(viewport, "selector");
                }
            }

            var cards = new List<WorkspaceCard>();
            foreach (var element in elements.Where(IsWorkspaceCard))
            {
                var bounds = UiBounds(element);

                // Only the visible left half is eligible for editing. The right side contains Launch.
                var editArea = new Rectangle(bounds.Left, bounds.Top, (bounds.Width + 1) / 2, bounds.Height);
                var visibleEditArea = IsVisible(element) ? Rectangle.Intersect(editArea, viewportBounds) : Rectangle.Empty;
                cards.Add(new WorkspaceCard(UiString(element, "automationId")!, UiString(element, "name") ?? string.Empty, bounds, visibleEditArea));
            }

            if (cards.Count == 0 && !elements.Any(element => UiString(element, "type") == "Text" &&
                UiString(element, "name") == "There are no saved Workspaces." && IsVisible(element)))
            {
                return null;
            }

            return [.. cards.OrderBy(card => card.Bounds.Y)];
        }

        private static bool IsWorkspaceCard(JsonElement element) =>
            UiString(element, "type") == "Button" && UiString(element, "className") == "SettingsCard" &&
            Guid.TryParse(UiString(element, "automationId"), out _);

        private static Rectangle UiBounds(JsonElement element) =>
            new(UiCoordinate(element, "x"), UiCoordinate(element, "y"), UiCoordinate(element, "width"), UiCoordinate(element, "height"));

        private string[]? WorkspaceNames() => WorkspaceCards()?.Select(card => card.Name).ToArray();

        private WorkspaceCard WaitForCard(string name)
        {
            string? viewport = null;
            bool fromTop = true;
            var result = WaitHelper.WaitForStable(
                () => WorkspaceCards(out viewport)?.Where(card => card.Name == name).ToArray(),
                cards => cards is [var card] && card.Bounds.Width > 0 && card.Bounds.Height > 0,
                timeoutMS: 15_000,
                requiredConsecutiveMatches: 2,
                pollIntervalMS: 200,
                recover: cards =>
                {
                    if (cards is null || cards.Length > 1 || viewport is null)
                    {
                        return;
                    }

                    // An unrealized item has no selector. Scroll the overview until its card is realized.
                    if (fromTop)
                    {
                        WinappCli.InvokeAssertSuccess("ui", "scroll", viewport, "-w", Editor.WindowHandleArg, "--to", "top", "--json");
                        fromTop = false;
                    }
                    else
                    {
                        WinappCli.InvokeAssertSuccess("ui", "scroll", viewport, "-w", Editor.WindowHandleArg, "--direction", "down", "--json");
                    }
                },
                shouldRetryException: ShellMenu.IsTransientElementException);
            Assert.IsTrue(result.Succeeded, $"The workspace card '{name}' did not become uniquely addressable.");
            return result.LastObservation![0];
        }

        private readonly record struct WorkspaceCard(string Id, string Name, Rectangle Bounds, Rectangle VisibleEditArea);

        private void AssertWorkspaceNames(params string[] expected)
        {
            var ready = WaitHelper.WaitForStable(
                WorkspaceNames,
                actual => actual is not null && actual.Order(StringComparer.Ordinal).SequenceEqual(expected.Order(StringComparer.Ordinal)),
                timeoutMS: 15_000,
                requiredConsecutiveMatches: 2,
                pollIntervalMS: 150);
            string observed = ready.LastObservation is { } names ? $"[{string.Join(", ", names)}]" : "overview not ready";
            Assert.IsTrue(ready.Succeeded, $"Expected workspaces [{string.Join(", ", expected)}], observed {observed}.");
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

            var ready = WaitHelper.WaitForStable(
                () => FindDescendants<TextBox>(row, "Edit", "CommandLineTextBox").Any(input => input.IsEnabled),
                expanded => expanded,
                15_000);
            Assert.IsTrue(ready.Succeeded, "Application details did not expand.");
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
                WorkspaceCards,
                cards => cards is not null,
                15_000,
                requiredConsecutiveMatches: 2);
            Assert.IsTrue(ready.Succeeded, "The editor did not return to the visible workspace list.");
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

        private void CaptureIntoEditor(Session capture, bool recapture = false)
        {
            Step("Capturing the desktop.");
            capture.Find<Button>(By.AccessibilityId("SnapshotButton"), 15_000).Invoke(msPostAction: 0);
            Assert.IsTrue(Editor.Has(By.AccessibilityId("EditNameTextBox"), 45_000), "The captured workspace did not open in the editing page.");
            WindowHelper.MaximizeWindow(new IntPtr(Editor.WindowHandle));
            if (recapture)
            {
                // The old draft stays visible until the editor has accepted the new capture.
                Assert.IsTrue(
                    Editor.Find<Button>(By.AccessibilityId("RevertButton"), 15_000).WaitForProperty("IsEnabled", "True", 30_000),
                    "The editor did not finish applying the recaptured workspace.");
            }
        }

        private JsonObject CapturePublishedSnapshot(Session capture)
        {
            var previousId = File.Exists(WorkspaceTestState.TemporaryPath)
                ? WorkspaceTestState.Text(WorkspaceTestState.ReadObject(WorkspaceTestState.TemporaryPath), "id")
                : null;
            CaptureIntoEditor(capture);
            var result = WaitHelper.WaitForStable(
                () => File.Exists(WorkspaceTestState.TemporaryPath)
                    ? WorkspaceTestState.ReadObject(WorkspaceTestState.TemporaryPath)
                    : null,
                snapshot => snapshot is not null &&
                    WorkspaceTestState.Text(snapshot, "id") != previousId &&
                    snapshot["applications"] is JsonArray { Count: > 0 },
                timeoutMS: 30_000,
                requiredConsecutiveMatches: 2,
                pollIntervalMS: 300,
                shouldRetryException: error => error is IOException or JsonException);
            Assert.IsTrue(result.Succeeded, "A fresh snapshot containing the captured applications was not published.");

            var snapshot = result.LastObservation!;
            State.TrackProjectShortcutFiles(WorkspaceTestState.Text(snapshot, "id"));
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

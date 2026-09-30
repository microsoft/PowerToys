// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FancyZones.UITests.Utils;

/// <summary>
/// Shared building blocks for the FancyZones <c>.Next</c> UI tests: Settings navigation, layout-editor
/// lifecycle, process/window waits and the screen geometry the drag tests measure against.
/// </summary>
public static class FancyZonesTestHelper
{
    /// <summary>The FancyZones module hosted by the runner (owns zone overlays and the snap hooks).</summary>
    public const string FancyZonesProcess = "PowerToys.FancyZones";

    /// <summary>The standalone WPF layout editor the Settings page launches.</summary>
    public const string EditorProcess = "PowerToys.FancyZonesEditor";

    public const string SettingsProcess = "PowerToys.Settings";

    /// <summary>Processes that must not survive into the next FancyZones test.</summary>
    public static IReadOnlyList<string> StaleProcessNames { get; } =
    new List<string>
    {
        "PowerToys",
        SettingsProcess,
        FancyZonesProcess,
        EditorProcess,
    };

    /// <summary>Window class of the zone overlay (<c>FancyZonesLib/WorkArea.cpp</c>).</summary>
    public const string ZonesOverlayClassName = "FancyZones_ZonesOverlay";

    /// <summary>Per-HWND bitmask stamped by FancyZones when a window is assigned to zones 0-63.</summary>
    public const string ZonedWindowProperty = "FancyZones_zones";

    /// <summary>
    /// Default timeout for a UI lookup. Deliberately generous: every <c>Find</c> shells out to
    /// <c>winapp.exe</c>, and a single call takes tens of seconds on a loaded machine. A deadline
    /// shorter than one call turns a recoverable <c>stale_element</c> into a hard failure, because the
    /// retry inside <c>FindAll</c> never gets a second attempt.
    /// </summary>
    public const int FindTimeoutMs = 30_000;

    /// <summary>AutomationIds used by the tests, ported from the legacy <c>FancyZonesEditorHelper</c>.</summary>
    public static class AccessibilityId
    {
        // PowerToys Settings, FancyZones page.
        public const string WindowingNavItem = "WindowingAndLayoutsNavItem";
        public const string FancyZonesNavItem = "FancyZonesNavItem";
        public const string LaunchLayoutEditorButton = "LaunchLayoutEditorButton";

        // Layout editor.
        public const string Monitors = "Monitors";
        public const string NewLayoutButton = "NewLayoutButton";
        public const string EditLayoutButton = "EditLayoutButton";
        public const string EditLayoutDialogTitle = "EditLayoutDialogTitle";
        public const string GridCustomLayoutCard = "GridcustomlayoutCard";
        public const string Grid9LayoutCard = "Grid-9Card";
        public const string CanvasCustomLayoutCard = "CanvascustomlayoutCard";
        public const string HotkeyComboBox = "quickKeySelectionComboBox";
        public const string DeleteLayoutButton = "deleteLayoutButton";
        public const string PrimaryButton = "PrimaryButton";
        public const string SecondaryButton = "SecondaryButton";
        public const string ResolutionText = "ResolutionText";
    }

    /// <summary>Layout names the tests seed into <c>custom-layouts.json</c> / read back in the editor.</summary>
    public static class LayoutName
    {
        public const string CustomColumn = "Custom Column";
        public const string GridCustomLayout = "Grid custom layout";
        public const string Grid9 = "Grid-9";
        public const string CanvasCustomLayout = "Canvas custom layout";
        public const string NoLayout = "No layout";
    }

    /// <summary>
    /// Timestamped trace of a UI action. Written before the blocking call so that on a hang or CI
    /// timeout the last line names the step that stuck (ci-stability Principle 7).
    /// </summary>
    public static void Step(UITestBase testBase, string message) =>
        testBase.TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] {message}");

    /// <summary>
    /// Stop FancyZones before restarting its runner so a slow child teardown cannot retain the
    /// single-instance mutex and make the new runner track a short-lived duplicate process.
    /// </summary>
    public static void RestartPowerToys(UITestBase testBase)
    {
        Step(testBase, $"Stopping {FancyZonesProcess} before restarting PowerToys");
        Assert.IsTrue(
            WindowControl.TryKillProcessTreeByNameAndWait(FancyZonesProcess, 10_000),
            $"Could not stop {FancyZonesProcess} before restarting PowerToys. " +
            $"Live instances: {DescribeProcesses(FancyZonesProcess)}.");

        testBase.RestartScope();
    }

    /// <summary>Live instances of a process with their ids and start times, for failure messages.</summary>
    public static string DescribeProcesses(string processName)
    {
        var live = Process.GetProcessesByName(processName);
        if (live.Length == 0)
        {
            return "none";
        }

        return string.Join(", ", live.Select(p =>
        {
            try
            {
                return $"pid {p.Id} (started {p.StartTime:HH:mm:ss})";
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                return $"pid {p.Id} (start time unavailable)";
            }
        }));
    }

    /// <summary>
    /// Wait for the FancyZones module process, which the runner starts because the module is enabled
    /// in the global settings.
    /// </summary>
    /// <remarks>
    /// Tests that only need FancyZones <i>running</i> use this instead of navigating Settings and
    /// flipping the page toggle: the toggle route costs several UIA tree walks through the Settings
    /// window, while the process check is a local Win32 query.
    /// </remarks>
    public static void EnsureFancyZonesRunning(UITestBase testBase)
    {
        Step(testBase, "Waiting for the FancyZones module process");
        Assert.IsTrue(
            WaitForProcess(FancyZonesProcess, true, 30_000),
            $"{FancyZonesProcess} is not running, so the module was not enabled for this test.");

        // The process appears well before the module can draw anything: it first identifies the
        // monitors and builds the work areas, and only then creates the zone overlay window. Acting on
        // the process alone races that startup, and a drag or a layout switch issued too early shows
        // no zones at all.
        Step(testBase, "Waiting for the zone overlay window to be created");
        Assert.IsTrue(
            WaitForZonesOverlayWindow(90_000),
            $"FancyZones never created its '{ZonesOverlayClassName}' window, so it cannot show zones.");
    }

    /// <summary>Poll until FancyZones has created its zone overlay window (visible or not).</summary>
    /// <remarks>
    /// Only meaningful right after a restart: <c>WorkArea.cpp</c> keeps a per-process pool of overlay
    /// windows and recycles them, so once the module has run, such a window exists whether or not a
    /// work area is currently using it.
    /// </remarks>
    public static bool WaitForZonesOverlayWindow(int timeoutMs = 60_000)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(timeoutMs);
        while (true)
        {
            if (WindowControl.AnyWindowOfClassExists(ZonesOverlayClassName))
            {
                return true;
            }

            if (DateTime.UtcNow >= deadline)
            {
                return false;
            }

            Thread.Sleep(250);
        }
    }

    /// <summary>
    /// Open the layout editor and bind a session to it. The window is discovered with Win32
    /// enumeration; the returned session is <b>process</b>-scoped so that the editor's WPF popups (the
    /// hotkey dropdown) and dialogs resolve as well as its main window.
    /// </summary>
    /// <remarks>
    /// The editor is launched by signalling the module's toggle event rather than by clicking
    /// "Launch layout editor" in Settings — same product code path, without the Settings navigation
    /// and button lookup that dominate the runtime of every test.
    /// </remarks>
    public static Session OpenLayoutEditor(UITestBase testBase, int timeoutMs = 60_000)
    {
        // FancyZones opens the editor only when it does not already believe one is running: while its
        // terminate-editor handle is alive, ToggleEditor treats the event as "close" and returns. That
        // handle is released when FancyZones processes the editor's exit, so the editor must be shut
        // down and observed as gone before signalling, or the open is silently swallowed.
        CloseLayoutEditor(testBase);

        const int signalAttempts = 3;
        for (var attempt = 1; attempt <= signalAttempts; attempt++)
        {
            Step(testBase, $"Attempt {attempt}/{signalAttempts}: signalling the FancyZones editor toggle event");
            Assert.IsTrue(
                NamedEventHelper.WaitAndSignal(NamedEventHelper.FancyZonesEditorToggle, 30_000),
                "The FancyZones module never created its editor toggle event, so the editor cannot be opened.");

            if (WaitForProcess(EditorProcess, true, 30_000))
            {
                break;
            }

            Step(testBase, "No editor process appeared; the signal was consumed as a terminate request");
            Thread.Sleep(1_500);
        }

        Assert.IsTrue(
            WaitForProcess(EditorProcess, true, 0),
            $"The FancyZones layout editor ({EditorProcess}) did not start after {signalAttempts} toggle-event signals.");

        Step(testBase, "Waiting for the layout editor window");
        var editorWindow = WindowsFinder.WaitForWindowByApp(
            EditorProcess,
            w => w.Width > 200 && w.Height > 200,
            timeoutMs);

        Assert.IsNotNull(editorWindow, $"The FancyZones layout editor ({EditorProcess}) did not open within {timeoutMs}ms.");

        var editor = Session.FromProcess(EditorProcess, PowerToysModule.FancyZonesEditor, FindTimeoutMs);

        // The editor's card list binds asynchronously; wait for a real layout card before touching it.
        Assert.IsTrue(
            editor.WaitForElement(By.AccessibilityId(AccessibilityId.Monitors), FindTimeoutMs),
            "The layout editor opened but never rendered its monitor/layout list.");

        return editor;
    }

    /// <summary>
    /// True while a FancyZones zone overlay is on screen. Detected through Win32 window lookup by
    /// window class, never a UIA walk: the overlay is a layered tool window that a UIA client can
    /// disturb, and it exposes no useful automation tree.
    /// </summary>
    public static bool IsZonesOverlayVisible() =>
        WindowControl.IsAnyWindowOfClassVisible(ZonesOverlayClassName);

    /// <summary>Wait until a FancyZones overlay remains visible across consecutive samples.</summary>
    public static bool WaitForZonesOverlayVisible(int timeoutMs = 5_000) =>
        WaitHelper.WaitForStable(
            IsZonesOverlayVisible,
            visible => visible,
            timeoutMs,
            requiredConsecutiveMatches: 5,
            pollIntervalMS: 100).Succeeded;

    /// <summary>Press Shift once, then wait without moving the cursor until the overlay is stable.</summary>
    public static bool ActivateZonesWithShiftDuringDrag(UITestBase testBase, int timeoutMs = 5_000)
    {
        Step(testBase, "Pressing Shift and waiting for the zones overlay to remain visible");
        KeyboardHelper.PressKey(Key.LShift);

        return WaitForZonesOverlayVisible(timeoutMs);
    }

    /// <summary>Wait until no FancyZones overlay remains visible between complete drag attempts.</summary>
    public static bool WaitForZonesOverlayHidden(int timeoutMs = 5_000, int requiredConsecutiveMatches = 3) =>
        WaitHelper.WaitForStable(
            IsZonesOverlayVisible,
            visible => !visible,
            timeoutMs,
            requiredConsecutiveMatches,
            pollIntervalMS: 100).Succeeded;

    /// <summary>Current zone-assignment bitmask stamped on an exact HWND.</summary>
    public static long GetZoneBitmask(IntPtr window) =>
        WindowHelper.GetWindowPropertyValue(window, ZonedWindowProperty);

    /// <summary>Wait until an exact HWND is stamped with the expected zone bitmask.</summary>
    public static bool WaitForZoneBitmask(IntPtr window, long expected, int timeoutMs = 5_000) =>
        WaitHelper.WaitForStable(
            () => GetZoneBitmask(window),
            bitmask => bitmask == expected,
            timeoutMs,
            requiredConsecutiveMatches: 2,
            pollIntervalMS: 100).Succeeded;

    /// <summary>
    /// Run <paramref name="trigger"/> and report whether FancyZones showed a zone overlay.
    /// </summary>
    /// <remarks>
    /// Uses a <c>WinEvent</c> hook rather than polling. The flash is a one-shot show/auto-hide, and a
    /// poll can only see a window that outlives its sample interval — it cannot tell "never shown"
    /// apart from "shown and hidden again immediately". (Polling at 12ms, 58ms and 500ms all reported
    /// nothing here, while the same probe correctly saw the overlay during a drag, where
    /// <c>MoveSizeUpdate</c> re-shows it on every mouse move.)
    /// </remarks>
    public static bool DidZonesFlash(UITestBase testBase, Action trigger, int timeoutMs = 5_000)
    {
        using var watcher = new WindowShowWatcher(ZonesOverlayClassName);

        trigger();
        var flashed = watcher.Wait(timeoutMs);

        var seen = watcher.Events;
        Step(testBase, $"Overlay window events: {(seen.Count == 0 ? "<none>" : string.Join(", ", seen))}");
        return flashed;
    }

    /// <summary>Select a layout card in the editor, which applies it to the current work area.</summary>
    public static void ApplyLayout(UITestBase testBase, Session editor, By card)
    {
        Step(testBase, $"Applying layout {card} in the editor");
        editor.Find<Element>(card, FindTimeoutMs).Click(msPostAction: 800);
    }

    /// <summary>
    /// Open a layout card's edit dialog and wait until <paramref name="requiredControl"/> is actionable.
    /// </summary>
    /// <remarks>
    /// Two traps here. The card's edit button is picked by GEOMETRY rather than by a card-scoped
    /// <c>Find</c>: every card exposes an <c>EditLayoutButton</c> with the same AutomationId and the
    /// scoped search can return another card's, which silently opens the wrong dialog — observed as
    /// "Edit 'Focus'" offering <c>createFromTemplateLayoutButton</c> instead of the custom layout's
    /// delete/hotkey controls. Readiness requires both the visible dialog identity and the actionable
    /// control the caller needs because both remain in the automation tree while the dialog is closed.
    /// </remarks>
    public static void OpenEditLayoutDialog(UITestBase testBase, Session editor, string layoutCardId, By requiredControl)
    {
        const int attempts = 3;
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            var card = editor.Find<Element>(By.AccessibilityId(layoutCardId), FindTimeoutMs);
            var editButtons = editor.FindAll<Button>(By.AccessibilityId(AccessibilityId.EditLayoutButton), FindTimeoutMs);
            var editButton = editButtons.FirstOrDefault(b => IsInside(card, b));

            var cardBounds = $"({card.X},{card.Y}) {card.Width}x{card.Height}";
            Assert.IsNotNull(
                editButton,
                $"None of the {editButtons.Count} '{AccessibilityId.EditLayoutButton}' elements lie inside the '{layoutCardId}' card at {cardBounds}.");

            Step(testBase, $"Attempt {attempt}/{attempts}: opening the edit dialog for {layoutCardId} via the button at ({editButton!.X},{editButton.Y})");
            editButton.Click(msPostAction: 1000);

            var dialogReady = editor.WaitFor(
                () =>
                {
                    var dialogIdentity = editor.FindAll<Element>(By.AccessibilityId(AccessibilityId.EditLayoutDialogTitle), 0)
                        .FirstOrDefault(element =>
                            element.Displayed &&
                            element.Width > 0 &&
                            element.Height > 0 &&
                            !string.IsNullOrWhiteSpace(element.Name));
                    var requiredElement = editor.FindAll<Element>(requiredControl, 0)
                        .FirstOrDefault(element =>
                            element.Displayed &&
                            element.IsEnabled &&
                            element.Width > 0 &&
                            element.Height > 0);
                    return dialogIdentity is not null && requiredElement is not null;
                },
                5_000,
                100);

            if (dialogReady)
            {
                return;
            }

            Step(testBase, "The dialog did not expose the expected control; cancelling it before retrying");
            TryCancelDialog(editor);
        }

        Assert.Fail(
            $"The edit-layout dialog for '{layoutCardId}' never exposed {requiredControl} after {attempts} attempts. " +
            $"Editor tree at failure: {DescribeEditorTree(testBase, editor)}");
    }

    /// <summary>True when <paramref name="element"/>'s top-left sits within <paramref name="container"/>.</summary>
    private static bool IsInside(Element container, Element element) =>
        element.X >= container.X &&
        element.X < container.X + container.Width &&
        element.Y >= container.Y &&
        element.Y < container.Y + container.Height;

    /// <summary>Dismiss an open editor dialog so the next interaction isn't blocked by it.</summary>
    private static void TryCancelDialog(Session editor)
    {
        try
        {
            if (editor.Has(By.AccessibilityId(AccessibilityId.SecondaryButton), 2_000))
            {
                editor.Find<Button>(By.AccessibilityId(AccessibilityId.SecondaryButton), 5_000).Click(msPostAction: 800);
            }
        }
        catch (Exception)
        {
            // Cancelling is best-effort; the retry re-reads state anyway.
        }
    }

    /// <summary>
    /// Dump the editor's UIA tree so a missing dialog control can be told apart from a dialog that
    /// never opened. Best-effort and truncated — it goes into an assertion message.
    /// </summary>
    private static string DescribeEditorTree(UITestBase testBase, Session editor)
    {
        try
        {
            var tree = editor.Inspect(depth: 10).ToString() ?? string.Empty;
            var path = Path.Combine(
                testBase.TestContext.TestResultsDirectory ?? Path.GetTempPath(),
                $"editor-tree-{DateTime.UtcNow:HHmmssfff}.json");
            File.WriteAllText(path, tree);
            testBase.TestContext.AddResultFile(path);

            return tree.Length <= 3000 ? tree : tree[..3000] + $"… (full tree attached as {Path.GetFileName(path)})";
        }
        catch (Exception ex)
        {
            return $"<could not inspect the editor: {ex.GetType().Name}: {ex.Message}>";
        }
    }

    /// <summary>Close the layout editor and wait for FancyZones to observe that it is gone.</summary>
    public static void CloseLayoutEditor(UITestBase testBase)
    {
        if (!WaitForProcess(EditorProcess, true, 0))
        {
            return;
        }

        Step(testBase, "Closing the layout editor");
        WindowControl.TryCloseByApp(EditorProcess);

        if (!WaitForProcess(EditorProcess, false, 10_000))
        {
            WindowControl.TryKillProcessTreeByNameAndWait(EditorProcess, 5_000);
        }

        // FancyZones releases its terminate-editor handle only when it processes the editor's exit;
        // signalling the toggle before that would be swallowed as a close request.
        Thread.Sleep(1_500);
    }

    /// <summary>
    /// Open the layout editor, select <paramref name="card"/>, and close it again — the sequence that
    /// makes FancyZones write the chosen layout into <c>applied-layouts.json</c> for the live work area.
    /// </summary>
    /// <param name="expectedAppliedFragment">
    /// When given, the applied-layouts file must end up containing this fragment (normally the layout
    /// UUID). Verifying the file makes "the layout was applied" an authoritative signal instead of an
    /// assumption — a click that silently missed otherwise surfaces much later as an unrelated failure.
    /// </param>
    public static void ApplyLayoutThroughEditor(UITestBase testBase, By card, string? expectedAppliedFragment = null)
    {
        // Selecting a layout is idempotent, so a card click that didn't register (the editor's list
        // binds asynchronously) can simply be repeated until the file confirms the result.
        const int attempts = 3;
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            var editor = OpenLayoutEditor(testBase);
            try
            {
                ApplyLayout(testBase, editor, card);
            }
            finally
            {
                CloseLayoutEditor(testBase);
            }

            if (expectedAppliedFragment is null || AppliedLayoutContains(expectedAppliedFragment, 15_000))
            {
                return;
            }

            Step(testBase, $"Attempt {attempt}/{attempts}: the layout did not take; reopening the editor");
        }

        Assert.Fail(
            $"The layout was not applied: applied-layouts.json never referenced '{expectedAppliedFragment}' " +
            $"after {attempts} attempts. Last content: {ReadAppliedLayouts()}");
    }

    /// <summary>Poll <c>applied-layouts.json</c> until it references the expected layout.</summary>
    public static bool AppliedLayoutContains(string expectedFragment, int timeoutMs = 15_000)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(timeoutMs);
        while (true)
        {
            if (ReadAppliedLayouts().Contains(expectedFragment, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (DateTime.UtcNow >= deadline)
            {
                return false;
            }

            Thread.Sleep(300);
        }
    }

    /// <summary>Current content of <c>applied-layouts.json</c>, or an empty string when it is absent.</summary>
    public static string ReadAppliedLayouts()
    {
        var appliedLayouts = new FancyZonesFiles().AppliedLayouts;
        return appliedLayouts.Exists ? appliedLayouts.Read() : string.Empty;
    }

    /// <summary>Poll until a process reaches the expected presence. No built-in equivalent exists.</summary>
    public static bool WaitForProcess(string processName, bool expected, int timeoutMs)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(timeoutMs);
        while (true)
        {
            if ((Process.GetProcessesByName(processName).Length > 0) == expected)
            {
                return true;
            }

            if (DateTime.UtcNow >= deadline)
            {
                return false;
            }

            Thread.Sleep(200);
        }
    }

    /// <summary>Window class of a File Explorer browser window.</summary>
    public const string ExplorerWindowClass = "CabinetWClass";

    /// <summary>
    /// Open a File Explorer window and return its handle. Explorer is the drag subject for the
    /// zone-behaviour tests because it is a classic Win32 window whose caption drag runs through the
    /// standard <c>DefWindowProc</c> move loop, which is what raises
    /// <c>EVENT_SYSTEM_MOVESIZESTART</c> — the event FancyZones listens to. A WinUI 3 window such as
    /// PowerToys Settings implements its custom title bar in user space and moves itself with
    /// <c>SetWindowPos</c>, so dragging it moves the window without FancyZones ever seeing a drag and
    /// no zones are drawn. The returned HWND has stable title/bounds but is not guaranteed foreground;
    /// callers must acquire foreground at the physical-input boundary.
    /// </summary>
    public static IntPtr OpenExplorerWindow(UITestBase testBase, string? folder = null, int timeoutMs = 30_000)
    {
        folder ??= Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var existing = ExplorerWindows().Select(w => w.Hwnd).ToHashSet();

        Step(testBase, $"Opening a File Explorer window at '{folder}'");
        using (Process.Start(new ProcessStartInfo("explorer.exe", folder)
        {
            UseShellExecute = true,
        }))
        {
        }

        var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            var fresh = ExplorerWindows().FirstOrDefault(w => !existing.Contains(w.Hwnd));
            if (fresh.Hwnd != IntPtr.Zero)
            {
                (string Title, (int Left, int Top, int Right, int Bottom) Bounds)? previous = null;
                var ready = WaitHelper.WaitForStable(
                    () =>
                    {
                        var current = ExplorerWindows().FirstOrDefault(w => w.Hwnd == fresh.Hwnd);
                        var bounds = WindowHelper.GetWindowBounds(fresh.Hwnd);
                        var observation = (current.Title, bounds);
                        var stable = current.Hwnd == fresh.Hwnd &&
                                     !string.IsNullOrWhiteSpace(current.Title) &&
                                     bounds.Right - bounds.Left > 300 &&
                                     bounds.Bottom - bounds.Top > 200 &&
                                     previous == observation;
                        previous = observation;
                        return stable;
                    },
                    value => value,
                    10_000,
                    requiredConsecutiveMatches: 3,
                    pollIntervalMS: 200);

                Assert.IsTrue(
                    ready.Succeeded,
                    $"Explorer window {fresh.Hwnd} appeared but its title and bounds never stabilized. " +
                    $"Last title: '{GetWindowTitle(fresh.Hwnd)}'; bounds: {WindowHelper.GetWindowBounds(fresh.Hwnd)}.");

                Step(testBase, $"Explorer window {fresh.Hwnd} ready ('{GetWindowTitle(fresh.Hwnd)}')");
                return fresh.Hwnd;
            }

            Thread.Sleep(300);
        }

        Assert.Fail($"No File Explorer window appeared within {timeoutMs}ms.");
        return IntPtr.Zero;
    }

    /// <summary>Close every File Explorer browser window (never the desktop/shell itself).</summary>
    public static void CloseExplorerWindows() =>
        WindowControl.TryCloseByApp(
            "explorer",
            w => w.ClassName.Equals(ExplorerWindowClass, StringComparison.OrdinalIgnoreCase));

    /// <summary>Win32 title of a window.</summary>
    public static string GetWindowTitle(IntPtr hwnd) =>
        WindowControl.EnumerateAllWindows().FirstOrDefault(w => w.Hwnd == hwnd).Title ?? string.Empty;

    private static IReadOnlyList<WindowControl.ProcessWindow> ExplorerWindows() =>
        WindowControl.EnumerateAllWindows()
            .Where(w => w.IsVisible && w.ClassName.Equals(ExplorerWindowClass, StringComparison.OrdinalIgnoreCase))
            .ToList();

    /// <summary>Wait for an exact HWND to own foreground without changing foreground as a recovery.</summary>
    public static bool WaitForForegroundWindow(IntPtr window, int timeoutMs = 5_000) =>
        WaitHelper.WaitForStable(
            WindowControl.GetForegroundWindowHandle,
            foreground => foreground == window,
            timeoutMs,
            requiredConsecutiveMatches: 2).Succeeded;

    /// <summary>Primary display bounds in physical pixels.</summary>
    public static (int Left, int Top, int Right, int Bottom) ScreenBounds()
    {
        var (width, height) = WindowHelper.GetDisplaySize();
        return (0, 0, width, height);
    }

    /// <summary>Centre of the primary display — the safe anchor for every coordinate gesture.</summary>
    public static (int X, int Y) ScreenCenter() => WindowHelper.GetScreenCenter();

    /// <summary>
    /// Move the cursor to (<paramref name="x"/>, <paramref name="y"/>) in a couple of steps so the
    /// zone overlay sees the cursor MOVE. A single <c>SetCursorPos</c> can land without a tracked move
    /// and leave the drag unnoticed (ui-tests-migration Recipe 11).
    /// </summary>
    public static void MoveCursorTracked(int x, int y, int settleMs = 200)
    {
        var (currentX, currentY) = MouseHelper.GetMousePosition();
        MouseHelper.MoveTo((currentX + x) / 2, (currentY + y) / 2);
        Thread.Sleep(60);
        MouseHelper.MoveTo(x, y);
        Thread.Sleep(settleMs);
    }

    /// <summary>
    /// Press the left button on the window's title bar and drag it by
    /// cursor to (<paramref name="targetX"/>, <paramref name="targetY"/>), leaving the button DOWN.
    /// Returns the cursor position at the end of the drag.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The grab point is verified rather than assumed. A title bar is mostly non-draggable chrome —
    /// the Settings window packs a hamburger, an app icon, a wide centred search box and the caption
    /// buttons into it — so a fixed offset can press a control instead of the drag region. The window
    /// then never moves, FancyZones never enters its drag state, and the test fails much later with a
    /// misleading "no zone colour" assertion. Each candidate point is therefore nudged and confirmed
    /// against the window's real position before the drag continues.
    /// </para>
    /// <para>
    /// The input shape matters as much as the point. The button-down is injected with
    /// <c>SendInput</c> (asynchronous) while the moves use <c>SetCursorPos</c> (synchronous), so a
    /// move issued immediately after the press can overtake it on a slow machine — the window then
    /// sees a press already at the destination and no drag ever begins. Hence the settle after the
    /// press and the small stepped moves, which also keep the travel above the system drag threshold
    /// without teleporting.
    /// </para>
    /// </remarks>
    public static bool BeginWindowDrag(
        UITestBase testBase,
        IntPtr window,
        int targetX,
        int targetY,
        params (int X, int Y)[] preferredGrabPoints)
    {
        var originalBounds = WindowHelper.GetWindowBounds(window);
        var originalWidth = originalBounds.Right - originalBounds.Left;
        var originalHeight = originalBounds.Bottom - originalBounds.Top;
        const int nudge = 60;

        // Convert caller-supplied absolute points to window-relative offsets so they can be
        // recomputed after a failed attempt moves or restores the window.
        var preferredOffsets = preferredGrabPoints
            .Select(point => (X: point.X - originalBounds.Left, Y: point.Y - originalBounds.Top))
            .ToArray();
        var candidateCount = preferredOffsets.Length + 5;

        for (var candidateIndex = 0; candidateIndex < candidateCount; candidateIndex++)
        {
            // A failed press can begin a desktop selection drag while Explorer is still rendering.
            // Reset the whole acquisition before every candidate, then derive the next point from
            // the current rectangle rather than stale pre-attempt coordinates.
            MouseHelper.LeftUp();
            WindowHelper.RestoreWindow(window);
            WindowHelper.MoveWindow(window, originalBounds.Left, originalBounds.Top);
            WindowHelper.SetMainWindowSize(window, originalWidth, originalHeight);
            WindowControl.TryBringToForeground(window);

            if (!TryGetStableGrabPoint(window, candidateIndex, preferredOffsets, out var grabPoint))
            {
                Step(
                    testBase,
                    $"Candidate {candidateIndex + 1}/{candidateCount} never became owned by HWND {window}; " +
                    $"bounds {WindowHelper.GetWindowBounds(window)}, foreground {WindowControl.GetForegroundWindowInfo()}");
                continue;
            }

            var (grabX, grabY) = grabPoint;
            Step(testBase, $"Grabbing the window at ({grabX},{grabY})");
            MouseHelper.MoveTo(grabX, grabY);
            Thread.Sleep(300);

            if (!WindowControl.IsPointOwnedByWindow(window, grabX, grabY))
            {
                Step(testBase, "The grab point stopped belonging to the target before mouse-down; recomputing the next candidate");
                continue;
            }

            var before = WindowHelper.GetWindowBounds(window);
            MouseHelper.LeftDown();
            Thread.Sleep(300);
            DragCursorTo(grabX, grabY, grabX + nudge, grabY + nudge);

            var moved = WindowHelper.GetWindowBounds(window);
            if ((moved.Left != before.Left || moved.Top != before.Top) && WindowControl.GetForegroundWindowHandle() == window)
            {
                Step(testBase, $"Window is moving (now at {moved}); dragging on to ({targetX},{targetY})");
                DragCursorTo(grabX + nudge, grabY + nudge, targetX, targetY);

                // Let the move-loop event reach FancyZones without moving away from the requested
                // target. Shift activation posts its own location update, so no cursor jiggle is needed.
                Thread.Sleep(750);
                return true;
            }

            Step(
                testBase,
                $"The target window did not keep a foreground move from that point (bounds {moved}); releasing and trying the next candidate");
            MouseHelper.LeftUp();
            Thread.Sleep(400);
        }

        Step(
            testBase,
            $"Could not start a title-bar drag: none of {candidateCount} recomputed candidate points moved HWND {window}. " +
            $"Bounds: {WindowHelper.GetWindowBounds(window)}. Foreground owner: {WindowControl.GetForegroundWindowInfo()}");
        return false;
    }

    private static bool TryGetStableGrabPoint(
        IntPtr window,
        int candidateIndex,
        IReadOnlyList<(int X, int Y)> preferredOffsets,
        out (int X, int Y) grabPoint)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        var consecutiveMatches = 0;
        var previousBounds = (Left: 0, Top: 0, Right: 0, Bottom: 0);
        grabPoint = default;

        while (DateTime.UtcNow < deadline)
        {
            WindowControl.TryBringToForeground(window);
            var bounds = WindowHelper.GetWindowBounds(window);
            var point = ResolveGrabPoint(candidateIndex, preferredOffsets, bounds);
            var ready = bounds.Right > bounds.Left &&
                        bounds.Bottom > bounds.Top &&
                        bounds == previousBounds &&
                        WindowControl.GetForegroundWindowHandle() == window &&
                        WindowControl.IsPointOwnedByWindow(window, point.X, point.Y);

            consecutiveMatches = ready ? consecutiveMatches + 1 : 0;
            if (consecutiveMatches >= 4)
            {
                grabPoint = point;
                return true;
            }

            previousBounds = bounds;
            Thread.Sleep(200);
        }

        return false;
    }

    private static (int X, int Y) ResolveGrabPoint(
        int candidateIndex,
        IReadOnlyList<(int X, int Y)> preferredOffsets,
        (int Left, int Top, int Right, int Bottom) bounds)
    {
        if (candidateIndex < preferredOffsets.Count)
        {
            var offset = preferredOffsets[candidateIndex];
            return (bounds.Left + offset.X, bounds.Top + offset.Y);
        }

        var width = bounds.Right - bounds.Left;
        return (candidateIndex - preferredOffsets.Count) switch
        {
            0 => (bounds.Left + (width / 2), bounds.Top + 16),
            1 => (bounds.Left + 150, bounds.Top + 20),
            2 => (bounds.Left + width - 200, bounds.Top + 20),
            3 => (bounds.Left + (width / 3), bounds.Top + 12),
            _ => (bounds.Left + 70, bounds.Top + 25),
        };
    }

    /// <summary>
    /// Grab points either side of the Settings window's title-bar search box — the only reliably
    /// draggable strips of that title bar.
    /// </summary>
    /// <remarks>
    /// PowerToys Settings hosts an <c>AutoSuggestBox</c> inside its custom title bar with
    /// <c>TitleBarContentMinWidth = 516</c>, plus a hamburger, an app icon and the caption buttons.
    /// On a narrow window that leaves only a few pixels of caption to grab, which is why a fixed
    /// offset silently fails to start a drag. Reading the box's real bounds and stepping just outside
    /// them lands in the caption region whatever the window's size.
    /// </remarks>
    public static (int X, int Y)[] SettingsTitleBarGrabPoints(UITestBase testBase)
    {
        try
        {
            var searchBox = testBase.Session.Find<Element>(By.AccessibilityId("SearchBox"), FindTimeoutMs);
            var middleY = searchBox.Y + (searchBox.Height / 2);
            Step(testBase, $"Settings search box at ({searchBox.X},{searchBox.Y}) {searchBox.Width}x{searchBox.Height}");

            return
            [
                (searchBox.X - 25, middleY),
                (searchBox.X + searchBox.Width + 25, middleY),
                (searchBox.X - 60, middleY),
            ];
        }
        catch (Exception ex)
        {
            Step(testBase, $"Could not locate the Settings search box ({ex.GetType().Name}); falling back to generic grab points");
            return [];
        }
    }

    /// <summary>Move the cursor along a straight line in small steps, as a real drag would travel.</summary>
    private static void DragCursorTo(int fromX, int fromY, int toX, int toY, int steps = 12, int stepDelayMs = 30)
    {
        for (var i = 1; i <= steps; i++)
        {
            MouseHelper.MoveTo(
                fromX + ((toX - fromX) * i / steps),
                fromY + ((toY - fromY) * i / steps));
            Thread.Sleep(stepDelayMs);
        }

        MouseHelper.MoveTo(toX, toY);
        Thread.Sleep(300);
    }
}

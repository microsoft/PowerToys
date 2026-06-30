// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ScreenRuler.UITests.Next;

/// <summary>
/// Shared helpers for the Screen Ruler <c>.Next</c> UI tests. Ported from the legacy
/// <c>ScreenRuler.UITests/TestHelper.cs</c> (WinAppDriver) to the winappcli harness.
/// </summary>
/// <remarks>
/// Key differences from the legacy helper:
/// <list type="bullet">
///   <item><description>The Settings tree is driven through <c>testBase.Session</c> (the Settings
///   window). The Screen Ruler toolbar buttons live in a <b>different process/window</b>
///   (<c>PowerToys.MeasureToolUI</c>), so they're found through a process-scoped
///   <see cref="Session.FromProcess(string, PowerToysModule, int)"/> session — the winappcli
///   equivalent of the legacy <c>global: true</c> Find.</description></item>
///   <item><description>Mouse / keyboard / clipboard go through the static
///   <c>MouseHelper</c> / <c>KeyboardHelper</c> / <c>ClipboardHelper</c> instead of instance
///   methods on <c>Session</c> / <c>UITestBase</c>.</description></item>
/// </list>
/// </remarks>
public static class TestHelper
{
    private static readonly string[] ShortcutSeparators = { " + ", "+", " " };

    // Button automation ids from the Measure Tool's Resources.resw.
    public const string BoundsButtonId = "Button_Bounds";
    public const string SpacingButtonName = "Button_Spacing";
    public const string HorizontalSpacingButtonName = "Button_SpacingHorizontal";
    public const string VerticalSpacingButtonName = "Button_SpacingVertical";
    public const string CloseButtonId = "Button_Close";

    // The Measure Tool UI process (the toolbar + measurement overlays). NOTE: the window TITLE is
    // "PowerToys.ScreenRuler", but the PROCESS name winappcli's -a flag needs is "PowerToys.MeasureToolUI".
    public const string ScreenRulerProcess = "PowerToys.MeasureToolUI";

    // The module's key in the global settings.json "enabled" section (note the space). Pass this to
    // the UITestBase ctor's enableModules so the runner boots ONLY this module — much faster on a
    // fresh profile (CI), where otherwise all ~30 modules start, and more isolated (no other module's
    // hotkeys/overlays interfere).
    public const string ModuleSettingsKey = "Measure Tool";

    // Ambient per-test diagnostics. ScreenRuler UI tests run sequentially, so a single ambient
    // instance is safe. The logger is created in InitializeTest and flushed (as a TestExecutionLog
    // artifact) in CleanupTest; Log(...) is a no-op when no test is active.
    private static DiagnosticLogger? log;

    /// <summary>Append a verbose, timestamped step to the current test's execution log.</summary>
    private static void Log(string message) => log?.Step(message);

    /// <summary>Navigate to the Screen Ruler settings page, enable the toggle, and read the shortcut.</summary>
    public static Key[] InitializeTest(UITestBase testBase, string testName)
    {
        log = new DiagnosticLogger(testBase, testName);

        Log("InitializeTest: navigating to the Screen Ruler settings page");
        LaunchFromSetting(testBase);

        Log("InitializeTest: enabling the Screen Ruler toggle");
        var toggleSwitch = SetScreenRulerToggle(testBase, enable: true);
        Assert.IsTrue(toggleSwitch.IsOn, $"Screen Ruler toggle switch should be ON for {testName}");

        var activationKeys = ReadActivationShortcut(testBase);
        Assert.IsNotNull(activationKeys, "Should be able to read activation shortcut");
        Assert.IsTrue(activationKeys.Length > 0, "Activation shortcut should contain at least one key");

        Log($"InitializeTest: ready; activation shortcut = {string.Join(" + ", activationKeys)}");
        return activationKeys;
    }

    /// <summary>Close the Screen Ruler UI (best-effort) and flush the execution-log artifact.</summary>
    public static void CleanupTest(UITestBase testBase)
    {
        try
        {
            Log("CleanupTest: closing the Screen Ruler UI");
            CloseScreenRulerUI(testBase);
        }
        finally
        {
            log?.Save();
            log = null;
        }
    }

    /// <summary>Navigate to the Screen Ruler (Measure Tool) settings page.</summary>
    public static void LaunchFromSetting(UITestBase testBase)
    {
        // The "System Tools" group is collapsed by default, so the Screen Ruler child item isn't in
        // the tree until the group is expanded. Expand it only when the child isn't already present.
        if (!testBase.Session.Has(By.AccessibilityId("ScreenRulerNavItem"), 500))
        {
            testBase.Session.Find<NavigationViewItem>(By.AccessibilityId("SystemToolsNavItem"), 5000).Click(msPostAction: 500);
        }

        testBase.Session.Find<NavigationViewItem>(By.AccessibilityId("ScreenRulerNavItem"), 5000).Click(msPostAction: 800);
    }

    /// <summary>Set the Screen Ruler toggle to the requested state.</summary>
    public static ToggleSwitch SetScreenRulerToggle(UITestBase testBase, bool enable)
    {
        var toggleSwitch = testBase.Session.Find<ToggleSwitch>(By.AccessibilityId("Toggle_ScreenRuler"), 5000);
        toggleSwitch.Toggle(enable);
        toggleSwitch.WaitForProperty("ToggleState", enable ? "On" : "Off", 5000);
        return toggleSwitch;
    }

    /// <summary>Set the Screen Ruler toggle and assert it reached the requested state.</summary>
    public static void SetAndVerifyScreenRulerToggle(UITestBase testBase, bool enable, string testName)
    {
        var toggleSwitch = SetScreenRulerToggle(testBase, enable);
        Assert.AreEqual(enable, toggleSwitch.IsOn, $"Screen Ruler toggle switch should be {(enable ? "ON" : "OFF")} for {testName}");
    }

    /// <summary>
    /// Read the activation shortcut straight from the Settings window's ShortcutControl — the
    /// EditButton's UIA HelpText, which the control sets to the live shortcut (e.g.
    /// "Win + Ctrl + Shift + M"). Polls until the window reports a real shortcut (a chord that
    /// includes a non-modifier key) rather than the "Configure shortcut" placeholder or a transient
    /// empty value while the page is still binding. Never substitutes a hard-coded default: the test
    /// must send exactly what the module is bound to, because a wrong/stale default would silently
    /// fail to activate and mask the real problem.
    /// </summary>
    public static Key[] ReadActivationShortcut(UITestBase testBase)
    {
        var shortcutCard = testBase.Session.Find<Element>(By.AccessibilityId("Shortcut_ScreenRuler"), 5000);
        var shortcutButton = shortcutCard.Find<Element>(By.AccessibilityId("EditButton"), 5000);

        string helpText = string.Empty;
        var deadline = DateTime.UtcNow.AddMilliseconds(5000);
        do
        {
            helpText = shortcutButton.HelpText ?? string.Empty;
            var keys = ParseShortcutText(helpText);
            if (HasMainKey(keys))
            {
                testBase.TestContext.WriteLine($"Activation shortcut read from Settings: '{helpText}'.");
                return keys;
            }

            Thread.Sleep(200);
        }
        while (DateTime.UtcNow < deadline);

        Assert.Fail(
            $"Could not read the Screen Ruler activation shortcut from the Settings window: the " +
            $"ShortcutControl EditButton HelpText was '{helpText}' (expected a chord such as " +
            $"'Win + Ctrl + Shift + M'). Refusing to fall back to a hard-coded default.");
        return Array.Empty<Key>(); // unreachable — Assert.Fail throws.
    }

    /// <summary>
    /// Parse a shortcut string like "Win + Ctrl + Shift + M" into a <see cref="Key"/> chord (note:
    /// "win" maps to <see cref="Key.LWin"/>). Returns exactly the keys present — NO default
    /// substitution; the caller decides whether the result is a usable shortcut.
    /// </summary>
    public static Key[] ParseShortcutText(string shortcutText)
    {
        var keys = new List<Key>();
        if (string.IsNullOrEmpty(shortcutText))
        {
            return keys.ToArray();
        }

        foreach (var part in shortcutText.Split(ShortcutSeparators, StringSplitOptions.RemoveEmptyEntries))
        {
            var key = ParseKeyToken(part);
            if (key.HasValue)
            {
                keys.Add(key.Value);
            }
        }

        return keys.ToArray();
    }

    /// <summary>Map one display token ("Win"/"Ctrl"/"Shift"/"Alt", a letter, a digit, "F5", "Space"…) to a <see cref="Key"/>.</summary>
    private static Key? ParseKeyToken(string token)
    {
        var t = token.Trim();
        if (t.Length == 0)
        {
            return null;
        }

        switch (t.ToLowerInvariant())
        {
            case "win":
            case "windows":
                return Key.LWin;
            case "ctrl":
            case "control":
                return Key.Ctrl;
            case "shift":
                return Key.Shift;
            case "alt":
                return Key.Alt;
        }

        // Single digit 0-9 → enum names Num0..Num9.
        if (t.Length == 1 && t[0] >= '0' && t[0] <= '9')
        {
            return Enum.TryParse<Key>("Num" + t, out var num) ? num : null;
        }

        // Letters, function keys ("F5") and named keys ("Space"/"Enter"/"Esc"/"Tab"/"Home"…) match the
        // Key enum names. Require a leading letter so numeric strings aren't cast straight to enum values.
        if (char.IsLetter(t[0]) && Enum.TryParse<Key>(t, ignoreCase: true, out var k))
        {
            return k;
        }

        return null;
    }

    /// <summary>True when the chord includes a non-modifier (main) key — i.e. a real, activatable shortcut.</summary>
    private static bool HasMainKey(Key[] keys) =>
        keys.Any(k => k is not (Key.LWin or Key.Ctrl or Key.Shift or Key.Alt));

    /// <summary>
    /// True when the Measure Tool UI is up. Uses a Win32 PROCESS check, NOT winappcli's
    /// <c>list-windows</c>: enumerating the live/frozen overlay's UIA tree costs seconds on CI (and can
    /// hang). MeasureToolUI exists only while the ruler is open, so process-presence is an accurate,
    /// instant, hang-free proxy.
    /// </summary>
    public static bool IsScreenRulerUIOpen(UITestBase testBase) =>
        Process.GetProcessesByName(ScreenRulerProcess).Length > 0;

    /// <summary>Poll until the Measure Tool UI reaches the requested presence.</summary>
    public static bool WaitForScreenRulerUIState(UITestBase testBase, bool shouldBeOpen, int timeoutMs = 5000, int pollingIntervalMs = 100)
    {
        var endTime = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < endTime)
        {
            if (IsScreenRulerUIOpen(testBase) == shouldBeOpen)
            {
                return true;
            }

            Thread.Sleep(pollingIntervalMs);
        }

        return false;
    }

    public static bool WaitForScreenRulerUI(UITestBase testBase, int timeoutMs = 5000) =>
        WaitForScreenRulerUIState(testBase, shouldBeOpen: true, timeoutMs);

    public static bool WaitForScreenRulerUIToDisappear(UITestBase testBase, int timeoutMs = 5000) =>
        WaitForScreenRulerUIState(testBase, shouldBeOpen: false, timeoutMs);

    /// <summary>
    /// Close the Measure Tool UI via Win32 — gracefully (WM_CLOSE to the main window), then kill as a
    /// last resort. Deliberately avoids winappcli: a process-scoped <see cref="Session.FromProcess"/>,
    /// the Close-button search, and <c>list-windows</c> all walk the live/frozen overlay's UIA tree,
    /// which costs 5–30s on CI. The test's assertions have already run by here, so a fast, reliable
    /// teardown matters more than a UI-driven close.
    /// </summary>
    public static void CloseScreenRulerUI(UITestBase testBase)
    {
        var procs = Process.GetProcessesByName(ScreenRulerProcess);
        if (procs.Length == 0)
        {
            Log("CloseScreenRulerUI: not running — nothing to close");
            return;
        }

        foreach (var p in procs)
        {
            try
            {
                // Graceful first: WM_CLOSE to the main window; if it doesn't exit, kill it.
                if (p.MainWindowHandle != IntPtr.Zero && p.CloseMainWindow() && p.WaitForExit(2000))
                {
                    Log($"CloseScreenRulerUI: pid {p.Id} closed via WM_CLOSE");
                }
                else if (!p.HasExited)
                {
                    Log($"CloseScreenRulerUI: pid {p.Id} didn't close on WM_CLOSE; killing it");
                    p.Kill(entireProcessTree: true);
                    p.WaitForExit(2000);
                }
            }
            catch (Exception ex)
            {
                Log($"CloseScreenRulerUI: closing pid {p.Id} failed: {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                p.Dispose();
            }
        }

        Log("CloseScreenRulerUI: done");
    }

    /// <summary>Clear the clipboard (STA handled inside the helper).</summary>
    public static void ClearClipboard() => ClipboardHelper.Clear();

    /// <summary>Read the clipboard text.</summary>
    public static string GetClipboardText() => ClipboardHelper.GetText();

    /// <summary>Validate clipboard content holds a valid spacing measurement for the given tool.</summary>
    public static bool ValidateSpacingClipboardContent(string clipboardText, string spacingType)
    {
        if (string.IsNullOrEmpty(clipboardText))
        {
            return false;
        }

        return spacingType switch
        {
            "Spacing" => Regex.IsMatch(clipboardText, @"\d+\s*[x×]\s*\d+"),
            "Horizontal Spacing" or "Vertical Spacing" => Regex.IsMatch(clipboardText, @"^\d+$"),
            _ => false,
        };
    }

    /// <summary>
    /// Send the activation chord, retrying until the Measure Tool UI appears. The runner arms its
    /// keyboard hook asynchronously after the module is enabled, so the first chord can be lost
    /// (skill Recipe 4). An initial settle gives the just-enabled module time to register its
    /// hotkey before the first send. Returns true once a Measure Tool window is visible.
    /// </summary>
    public static bool SendShortcutUntilVisible(UITestBase testBase, Key[] activationKeys, int attempts = 5, int perAttemptMs = 3000)
    {
        // Let the runner finish wiring the global hotkey after the module was just toggled on.
        Thread.Sleep(1500);

        for (int i = 0; i < attempts; i++)
        {
            Log($"SendShortcutUntilVisible: attempt {i + 1}/{attempts} — sending the activation chord");
            KeyboardHelper.SendKeys(activationKeys);
            if (WaitForScreenRulerUI(testBase, perAttemptMs))
            {
                Log($"SendShortcutUntilVisible: MeasureToolUI process detected on attempt {i + 1}");
                return true;
            }

            Log($"SendShortcutUntilVisible: still not visible after attempt {i + 1}");
        }

        return false;
    }

    /// <summary>Activate Screen Ruler via the shortcut and wait for the toolbar window.</summary>
    public static Session ActivateScreenRuler(UITestBase testBase, Key[] activationKeys, string testName)
    {
        ClearClipboard();

        // Park the cursor on the primary-monitor centre so the Measure Tool initialises tracking at a
        // predictable on-screen spot before activation (the cursor can otherwise be anywhere).
        var (cx, cy) = ScreenCenter();
        MouseHelper.MoveTo(cx, cy);
        Thread.Sleep(200);

        Log($"ActivateScreenRuler: sending activation chord {string.Join(" + ", activationKeys)}");
        Assert.IsTrue(
            SendShortcutUntilVisible(testBase, activationKeys),
            $"ScreenRulerUI should appear after pressing activation shortcut for {testName}: {string.Join(" + ", activationKeys)}");

        // Process-scoped session so the toolbar buttons resolve regardless of which Measure Tool
        // window owns them (the winappcli equivalent of the legacy global Find).
        Log("ActivateScreenRuler: toolbar is up; building the process-scoped session");
        var ruler = Session.FromProcess(ScreenRulerProcess, PowerToysModule.ScreenRuler, timeoutMS: 5000);
        Log("ActivateScreenRuler: session ready");
        return ruler;
    }

    /// <summary>Run a spacing-tool measurement and validate the clipboard output.</summary>
    public static void PerformSpacingToolTest(UITestBase testBase, string buttonId, string testName)
    {
        var activationKeys = ReadActivationShortcut(testBase);
        var ruler = ActivateScreenRuler(testBase, activationKeys, testName);

        SelectToolAndVerify(ruler, buttonId, testName);

        var clipboardText = MeasureWithRetry(testName, PerformMeasurementAction);
        Assert.IsFalse(string.IsNullOrEmpty(clipboardText), $"{testName}: Clipboard should contain measurement data");
        Assert.IsTrue(
            ValidateSpacingClipboardContent(clipboardText, testName),
            $"{testName}: Clipboard should contain valid spacing measurement, but contained: '{clipboardText}'");

        CloseScreenRulerUI(testBase);
        Assert.IsTrue(
            WaitForScreenRulerUIToDisappear(testBase, 2000),
            $"{testName}: ScreenRulerUI should close after calling CloseScreenRulerUI");
    }

    /// <summary>Run a bounds-tool measurement (drag a 100x100 box) and validate the clipboard output.</summary>
    public static void PerformBoundsToolTest(UITestBase testBase)
    {
        var activationKeys = ReadActivationShortcut(testBase);
        var ruler = ActivateScreenRuler(testBase, activationKeys, "bounds test");

        SelectToolAndVerify(ruler, BoundsButtonId, "Bounds");

        var clipboardText = MeasureWithRetry("Bounds", PerformBoundsMeasurement);
        Assert.IsFalse(string.IsNullOrEmpty(clipboardText), "Clipboard should contain measurement data");
        Assert.IsTrue(
            clipboardText.Contains("100 × 100") || clipboardText.Contains("100 x 100"),
            $"Clipboard should contain '100 x 100', but contained: '{clipboardText}'");

        CloseScreenRulerUI(testBase);
        Assert.IsTrue(
            WaitForScreenRulerUIToDisappear(testBase, 2000),
            "ScreenRulerUI should close after calling CloseScreenRulerUI");
    }

    /// <summary>
    /// Select a toolbar tool with a coordinate-free winappcli UIA invoke, move the cursor onto the
    /// capture surface (a single tracked move), then CONFIRM the tool engaged by polling for the
    /// full-screen measurement overlay window (<c>PowerToys.MeasureToolOverlay</c>) — its presence
    /// means a following drag/click will actually measure. The overlay only shows once the cursor
    /// leaves the toolbar onto the surface.
    /// </summary>
    private static void SelectToolAndVerify(Session ruler, string buttonId, string testName)
    {
        Log($"SelectToolAndVerify[{testName}]: UIA invoke of {buttonId}");
        ruler.Find<Element>(By.AccessibilityId(buttonId), 15000).Click(msPostAction: 300);
        Log($"SelectToolAndVerify[{testName}]: UIA invoke of {buttonId} successful");

        // Moving the cursor off the toolbar onto the capture surface is what makes the overlay appear.
        // ActivateScreenRuler parked the cursor at the screen centre, so move to an offset to produce a
        // real tracked move (moving to the centre would be a no-op). The overlay shows right after the
        // move, so just settle briefly and confirm once — no need to poll.
        var (cx, cy) = ScreenCenter();
        MouseHelper.MoveTo(cx, cy);
        Log($"SelectToolAndVerify[{testName}]: cursor moved to ({cx},{cy}); settling 500ms before the overlay check");
        Thread.Sleep(500);

        Log($"SelectToolAndVerify[{testName}]: checking for the measurement overlay");
        Assert.IsTrue(
            IsMeasureOverlayPresent(),
            $"{testName}: the measurement overlay (PowerToys.MeasureToolOverlay) never appeared after the " +
            "tool invoke — the Measure Tool never entered capture state, so a measurement can't be taken.");
    }

    /// <summary>
    /// True when the Measure Tool's full-screen measurement overlay is up. Detection uses the pure
    /// Win32 <c>EnumWindows</c> API (via <see cref="WindowControl.EnumerateProcessWindows"/>) filtered
    /// to the <c>PowerToys.MeasureToolUI</c> process, looking for the overlay window
    /// (class <c>*OverlayWindow</c> / title <c>PowerToys.MeasureToolOverlay</c>). Win32 is used
    /// deliberately: winappcli's <c>list-windows</c> attaches a UI Automation client and walks the
    /// overlay's UIA tree, which disturbs the Measure Tool's live screen-capture session and yields an
    /// empty measurement on the very next click.
    /// </summary>
    private static bool IsMeasureOverlayPresent()
    {
        var pids = Process.GetProcessesByName(ScreenRulerProcess).Select(p => p.Id).ToList();
        var windows = WindowControl.EnumerateProcessWindows(pids);
        var present = windows.Any(w =>
            w.Title.Contains("MeasureToolOverlay", StringComparison.OrdinalIgnoreCase) ||
            w.ClassName.Contains("OverlayWindow", StringComparison.OrdinalIgnoreCase));

        var summary = windows.Count == 0
            ? "(none)"
            : string.Join(", ", windows.Select(w => $"'{w.Title}'[{w.ClassName}]"));
        Log($"IsMeasureOverlayPresent (Win32 EnumWindows): {windows.Count} window(s): {summary} => overlay {(present ? "PRESENT" : "absent")}");
        return present;
    }

    /// <summary>
    /// Take a measuring gesture and return the resulting clipboard text, retrying the gesture IN PLACE
    /// (without closing/reopening the tool) while the clipboard comes back empty. The Measure Tool only
    /// produces a measurement after its screen-capture and cursor-tracking threads have delivered data
    /// for the gesture point; the FIRST overlay of each kind pays a one-time cold start (slowest on
    /// Win10), so the very first gesture can fire before any frame is processed and yield an empty
    /// clipboard. The gesture itself (cursor move + click/drag) drives those threads, so simply repeating
    /// it on the SAME overlay succeeds once the pipeline is warm — closing and reopening would reset the
    /// cold start every time.
    /// </summary>
    private static string MeasureWithRetry(string testName, Action gesture, int maxAttempts = 3)
    {
        var clipboard = string.Empty;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            gesture();
            clipboard = GetClipboardText();
            Log($"{testName}: measurement attempt {attempt}/{maxAttempts}; clipboard = '{clipboard}' (length {clipboard.Length})");
            if (!string.IsNullOrEmpty(clipboard))
            {
                break;
            }

            if (attempt < maxAttempts)
            {
                Log($"{testName}: clipboard empty — retrying the measurement in place");
            }
        }

        return clipboard;
    }

    /// <summary>Spacing measuring gesture: move to a point near the centre and left-click to copy the spacing there.</summary>
    private static void PerformMeasurementAction()
    {
        var (cx, cy) = ScreenCenter();

        Log($"PerformMeasurementAction: move to ({cx - 50},{cy - 50}) then left-click to capture spacing");
        MouseHelper.MoveTo(cx - 50, cy - 50);
        Thread.Sleep(300);
        MouseHelper.LeftClick();
        Thread.Sleep(500);
    }

    /// <summary>
    /// Bounds measuring gesture: drag a 100x100 box from the centre. The drag's button-up is what copies
    /// the measurement to the clipboard, so no right-click is needed — and we deliberately skip it so a
    /// retry can re-drag on the SAME overlay (a right-click with no pending selection closes the bounds
    /// tool). The 99px delta measures 100x100 inclusive on a per-monitor-DPI-aware host (app.manifest).
    /// </summary>
    private static void PerformBoundsMeasurement()
    {
        var (cx, cy) = ScreenCenter();
        Log($"PerformBoundsMeasurement: dragging a 100x100 box from ({cx},{cy})");
        MouseHelper.Drag(cx, cy, cx + 99, cy + 99);
        Thread.Sleep(400);
    }

    /// <summary>
    /// Primary-monitor centre in PHYSICAL pixels. Correct only when the test host is per-monitor
    /// DPI aware (see the project's app.manifest); otherwise the size is virtualized by the display
    /// scale factor.
    /// </summary>
    private static (int X, int Y) ScreenCenter()
    {
        var size = System.Windows.Forms.SystemInformation.PrimaryMonitorSize;
        return (size.Width / 2, size.Height / 2);
    }
}

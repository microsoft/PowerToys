// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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

    /// <summary>Navigate to the Screen Ruler settings page, enable the toggle, and read the shortcut.</summary>
    public static Key[] InitializeTest(UITestBase testBase, string testName)
    {
        LaunchFromSetting(testBase);

        var toggleSwitch = SetScreenRulerToggle(testBase, enable: true);
        Assert.IsTrue(toggleSwitch.IsOn, $"Screen Ruler toggle switch should be ON for {testName}");

        var activationKeys = ReadActivationShortcut(testBase);
        Assert.IsNotNull(activationKeys, "Should be able to read activation shortcut");
        Assert.IsTrue(activationKeys.Length > 0, "Activation shortcut should contain at least one key");

        return activationKeys;
    }

    /// <summary>Close the Screen Ruler UI (best-effort).</summary>
    public static void CleanupTest(UITestBase testBase)
    {
        CloseScreenRulerUI(testBase);
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

    /// <summary>True when at least one Measure Tool window is open.</summary>
    public static bool IsScreenRulerUIOpen(UITestBase testBase) =>
        WindowsFinder.ListByApp(ScreenRulerProcess).Count > 0;

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

    /// <summary>Close the Measure Tool UI if it's open (best-effort, tolerant).</summary>
    public static void CloseScreenRulerUI(UITestBase testBase)
    {
        if (!IsScreenRulerUIOpen(testBase))
        {
            return;
        }

        // Prefer the toolbar's Close button; fall back to WM_CLOSE on every Measure Tool window.
        try
        {
            var ruler = Session.FromProcess(ScreenRulerProcess, PowerToysModule.ScreenRuler, timeoutMS: 2000);
            if (ruler.Has(By.AccessibilityId(CloseButtonId), 1000))
            {
                ruler.Find<Element>(By.AccessibilityId(CloseButtonId), 2000).Click();
            }
        }
        catch
        {
            // Ignore — fall through to the tolerant WM_CLOSE.
        }

        WindowControl.TryCloseByApp(ScreenRulerProcess);
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
            KeyboardHelper.SendKeys(activationKeys);
            if (WaitForScreenRulerUI(testBase, perAttemptMs))
            {
                return true;
            }
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

        Assert.IsTrue(
            SendShortcutUntilVisible(testBase, activationKeys),
            $"ScreenRulerUI should appear after pressing activation shortcut for {testName}: {string.Join(" + ", activationKeys)}");

        // Process-scoped session so the toolbar buttons resolve regardless of which Measure Tool
        // window owns them (the winappcli equivalent of the legacy global Find).
        return Session.FromProcess(ScreenRulerProcess, PowerToysModule.ScreenRuler, timeoutMS: 5000);
    }

    /// <summary>Run a spacing-tool measurement and validate the clipboard output.</summary>
    public static void PerformSpacingToolTest(UITestBase testBase, string buttonId, string testName)
    {
        var activationKeys = ReadActivationShortcut(testBase);
        var ruler = ActivateScreenRuler(testBase, activationKeys, testName);

        SelectToolAndVerify(ruler, buttonId, AcceleratorFor(buttonId), testName);

        PerformMeasurementAction();

        var clipboardText = GetClipboardText();
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

        SelectToolAndVerify(ruler, BoundsButtonId, Key.Num1, "Bounds");

        // Drag a 100x100 box centred on the primary monitor. Move to the start first so the Measure
        // Tool overlay is tracking the cursor before the drag. The 99px delta measures 100x100
        // inclusive once the host is per-monitor DPI aware (app.manifest).
        var (cx, cy) = ScreenCenter();
        int startX = cx - 50;
        int startY = cy - 50;
        MouseHelper.MoveTo(startX, startY);
        Thread.Sleep(300);
        MouseHelper.Drag(startX, startY, startX + 99, startY + 99, steps: 16);
        Thread.Sleep(400);

        // Right-click to dismiss the selection (commits the measurement to the clipboard).
        MouseHelper.RightClick();
        Thread.Sleep(300);

        var clipboardText = GetClipboardText();
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
    /// Select a toolbar tool and CONFIRM it engaged by waiting for the full-screen measurement overlay
    /// window (<c>PowerToys.MeasureToolOverlay</c>) to appear — its presence means the tool is in
    /// capture state, so a following drag/click will actually measure. Per attempt: a UIA invoke
    /// (winappcli — needs no focus or on-screen bounds, so it sidesteps the Win10 0×0-bounds problem),
    /// then, as a backup, the tool's keyboard accelerator (the toolbar labels them "Bounds (Ctrl+1)",
    /// "Spacing (Ctrl+2)", …). The overlay only shows once the cursor leaves the toolbar onto the
    /// capture surface, so each check parks the cursor at the measurement spot first. Up to 3 attempts.
    /// </summary>
    private static void SelectToolAndVerify(Session ruler, string buttonId, Key acceleratorDigit, string testName)
    {
        var (cx, cy) = ScreenCenter();
        const int attempts = 3;

        for (int attempt = 1; attempt <= attempts; attempt++)
        {
            // (a) Accessibility invoke via winappcli — coordinate-free, so a 0×0 button rect is fine.
            ruler.Find<Element>(By.AccessibilityId(buttonId), 15000).Click(msPostAction: 300);
            if (MoveOffToolbarAndWaitForOverlay(cx, cy))
            {
                return;
            }

            // (b) Keyboard accelerator backup (Ctrl+<n>) — the toolbar is the foreground window.
            KeyboardHelper.SendKeys(Key.Ctrl, acceleratorDigit);
            if (MoveOffToolbarAndWaitForOverlay(cx, cy))
            {
                return;
            }
        }

        Assert.Fail(
            $"{testName}: the measurement overlay (PowerToys.MeasureToolOverlay) never appeared after " +
            $"{attempts} accessibility-invoke + keyboard attempts — the Measure Tool never entered " +
            "capture state, so a measurement can't be taken.");
    }

    /// <summary>
    /// Park the cursor at the measurement spot (off the toolbar, two-step so the move is tracked) and
    /// poll briefly for the measurement overlay window. Returns true once it's present.
    /// </summary>
    private static bool MoveOffToolbarAndWaitForOverlay(int cx, int cy)
    {
        MouseHelper.MoveTo(cx - 40, cy - 40);
        Thread.Sleep(150);
        MouseHelper.MoveTo(cx, cy);

        var deadline = DateTime.UtcNow.AddMilliseconds(1500);
        do
        {
            if (IsMeasureOverlayPresent())
            {
                return true;
            }

            Thread.Sleep(250);
        }
        while (DateTime.UtcNow < deadline);

        return false;
    }

    /// <summary>
    /// True when the Measure Tool's full-screen measurement overlay is up — winappcli reports a
    /// <c>PowerToys.MeasureToolOverlay</c> window (class <c>*OverlayWindow</c>) alongside the toolbar
    /// once a tool is engaged and the cursor is over the capture surface. This is the reliable
    /// "we're in capture state" signal that the blind click-then-measure approach was missing.
    /// </summary>
    private static bool IsMeasureOverlayPresent() =>
        WindowsFinder.ListByApp(ScreenRulerProcess).Any(w =>
            w.Title.Contains("MeasureToolOverlay", StringComparison.OrdinalIgnoreCase) ||
            w.ClassName.Contains("OverlayWindow", StringComparison.OrdinalIgnoreCase));

    /// <summary>The toolbar keyboard accelerator (Ctrl+1..4) that selects each tool.</summary>
    private static Key AcceleratorFor(string buttonId) => buttonId switch
    {
        BoundsButtonId => Key.Num1,
        SpacingButtonName => Key.Num2,
        HorizontalSpacingButtonName => Key.Num3,
        VerticalSpacingButtonName => Key.Num4,
        _ => Key.Num1,
    };

    /// <summary>Move to the screen centre, left-click to capture, right-click to dismiss.</summary>
    private static void PerformMeasurementAction()
    {
        // Move to centre in two steps so the Measure Tool registers cursor MOVEMENT (a single
        // SetCursorPos can land without a tracked move, leaving the measurement empty), then click
        // to capture.
        var (cx, cy) = ScreenCenter();
        MouseHelper.MoveTo(cx - 60, cy - 60);
        Thread.Sleep(200);
        MouseHelper.MoveTo(cx, cy);
        Thread.Sleep(400);
        MouseHelper.LeftClick();
        Thread.Sleep(500);
        MouseHelper.RightClick();
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

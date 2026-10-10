// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.ShortcutGuide.UITests;

/// <summary>
/// Shared helpers for the Shortcut Guide UI tests.
/// </summary>
/// <remarks>
/// Shortcut Guide is an overlay module: the runner (via <c>PowerToys.exe</c>) owns the
/// activation hotkey and module lifecycle. Tests drive it through the
/// <see cref="PowerToysModule.PowerToysSettings"/> scope — navigate to the Shortcut Guide
/// settings page to toggle the module and read the shortcut, then fire the hotkey to assert
/// the overlay appears.
///
/// The overlay runs in <c>PowerToys.ShortcutGuide.exe</c>. It is a transparent, full-screen
/// WinUI window. Presence is checked via <see cref="Process.GetProcessesByName"/> to avoid
/// the expensive UIA tree walk on the transparent overlay.
/// </remarks>
public static class TestHelper
{
    private static readonly string[] ShortcutSeparators = [" + ", "+", " "];

    /// <summary>Process name of the Shortcut Guide overlay.</summary>
    public const string ShortcutGuideProcess = "PowerToys.ShortcutGuide";

    /// <summary>
    /// Module key in the global settings.json "enabled" section. Passed to the
    /// <see cref="UITestBase"/> constructor so only this module is booted by the runner.
    /// </summary>
    public const string ModuleSettingsKey = "Shortcut Guide";

    /// <summary>Navigate to the Shortcut Guide settings page, enable the toggle, and return the parsed activation shortcut.</summary>
    public static Key[] InitializeTest(UITestBase testBase, string testName)
    {
        NavigateToSettingsPage(testBase);

        var toggle = SetShortcutGuideToggle(testBase, enable: true);
        Assert.IsTrue(toggle.IsOn, $"Shortcut Guide toggle should be ON for {testName}");

        var activationKeys = ReadActivationShortcut(testBase);
        Assert.IsNotNull(activationKeys, "Should be able to read the activation shortcut");
        Assert.IsTrue(activationKeys.Length > 0, "Activation shortcut should contain at least one key");

        testBase.TestContext.WriteLine($"InitializeTest ready; activation shortcut = [{string.Join(", ", activationKeys)}]");
        return activationKeys;
    }

    /// <summary>Close the Shortcut Guide overlay (best-effort) and navigate back to the dashboard.</summary>
    public static void CleanupTest(UITestBase testBase)
    {
        try
        {
            CloseShortcutGuideOverlay();
        }
        catch (Exception ex)
        {
            testBase.TestContext.WriteLine($"CleanupTest: CloseShortcutGuideOverlay threw: {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>Navigate to the Shortcut Guide page via the Settings left-nav.</summary>
    public static void NavigateToSettingsPage(UITestBase testBase)
    {
        // "Shortcut Guide" is under the top-level nav — click its nav item directly.
        testBase.Session.Find<NavigationViewItem>(By.AccessibilityId("ShortcutGuideNavItem"), 5000).Click(msPostAction: 800);
    }

    /// <summary>Set the Shortcut Guide enable toggle to the requested state and wait for it to settle.</summary>
    public static ToggleSwitch SetShortcutGuideToggle(UITestBase testBase, bool enable)
    {
        var toggleSwitch = testBase.Session.Find<ToggleSwitch>(By.AccessibilityId("Toggle_ShortcutGuide"), 5000);
        toggleSwitch.Toggle(enable);
        toggleSwitch.WaitForProperty("ToggleState", enable ? "On" : "Off", 5000);
        return toggleSwitch;
    }

    /// <summary>Set the toggle and assert it reached the requested state.</summary>
    public static void SetAndVerifyShortcutGuideToggle(UITestBase testBase, bool enable, string testName)
    {
        var toggleSwitch = SetShortcutGuideToggle(testBase, enable);
        Assert.AreEqual(
            enable,
            toggleSwitch.IsOn,
            $"Shortcut Guide toggle should be {(enable ? "ON" : "OFF")} for {testName}");
    }

    /// <summary>
    /// Read the activation shortcut from the Settings page's ShortcutControl EditButton.
    /// Polls until a real chord (with a non-modifier key) is available.
    /// </summary>
    public static Key[] ReadActivationShortcut(UITestBase testBase)
    {
        var shortcutButton = testBase.Session.Find<Button>(By.AccessibilityId("EditButton"), 5000);

        string helpText = string.Empty;
        var deadline = DateTime.UtcNow.AddMilliseconds(5000);
        do
        {
            helpText = shortcutButton.HelpText ?? string.Empty;
            var keys = ParseShortcutText(helpText);
            if (HasMainKey(keys))
            {
                testBase.TestContext.WriteLine($"Activation shortcut read from Settings: '{helpText}'");
                return keys;
            }

            Thread.Sleep(200);
        }
        while (DateTime.UtcNow < deadline);

        Assert.Fail(
            $"Could not read the Shortcut Guide activation shortcut from the Settings window: the " +
            $"ShortcutControl EditButton HelpText was '{helpText}' (expected a chord such as " +
            $"'Win + /'). Refusing to fall back to a hard-coded default.");
        return []; // unreachable — Assert.Fail throws
    }

    /// <summary>Parse a shortcut string like "Win + Ctrl + Shift + M" into a <see cref="Key"/> chord.</summary>
    public static Key[] ParseShortcutText(string shortcutText)
    {
        var keys = new List<Key>();
        if (string.IsNullOrEmpty(shortcutText))
        {
            return [.. keys];
        }

        foreach (var part in shortcutText.Split(ShortcutSeparators, StringSplitOptions.RemoveEmptyEntries))
        {
            var key = ParseKeyToken(part);
            if (key.HasValue)
            {
                keys.Add(key.Value);
            }
        }

        return [.. keys];
    }

    /// <summary>Returns true when the Shortcut Guide overlay process is running.</summary>
    public static bool IsShortcutGuideOverlayOpen()
    {
        var procs = Process.GetProcessesByName(ShortcutGuideProcess);
        bool running = procs.Length > 0;
        foreach (var p in procs)
        {
            p.Dispose();
        }

        return running;
    }

    /// <summary>Poll until the overlay reaches the expected presence state.</summary>
    public static bool WaitForShortcutGuideOverlayState(bool shouldBeOpen, int timeoutMs = 8000, int pollingIntervalMs = 200)
    {
        var endTime = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < endTime)
        {
            if (IsShortcutGuideOverlayOpen() == shouldBeOpen)
            {
                return true;
            }

            Thread.Sleep(pollingIntervalMs);
        }

        return false;
    }

    /// <summary>Wait up to <paramref name="timeoutMs"/> ms for the overlay to appear.</summary>
    public static bool WaitForShortcutGuideOverlay(int timeoutMs = 8000) =>
        WaitForShortcutGuideOverlayState(shouldBeOpen: true, timeoutMs);

    /// <summary>Wait up to <paramref name="timeoutMs"/> ms for the overlay to disappear.</summary>
    public static bool WaitForShortcutGuideOverlayToDisappear(int timeoutMs = 5000) =>
        WaitForShortcutGuideOverlayState(shouldBeOpen: false, timeoutMs);

    /// <summary>
    /// Close the Shortcut Guide overlay via Win32 (graceful WM_CLOSE, then kill as last resort).
    /// Using direct process management avoids an expensive UIA walk of the transparent overlay.
    /// </summary>
    public static void CloseShortcutGuideOverlay()
    {
        var procs = Process.GetProcessesByName(ShortcutGuideProcess);
        if (procs.Length == 0)
        {
            return;
        }

        foreach (var p in procs)
        {
            try
            {
                if (p.MainWindowHandle != IntPtr.Zero && p.CloseMainWindow() && p.WaitForExit(2000))
                {
                    // Closed gracefully.
                }
                else if (!p.HasExited)
                {
                    p.Kill(entireProcessTree: true);
                    p.WaitForExit(2000);
                }
            }
            catch (Exception)
            {
                // Best-effort teardown — never throw from cleanup.
            }
            finally
            {
                p.Dispose();
            }
        }
    }

    /// <summary>
    /// Send the activation chord, retrying until the Shortcut Guide overlay appears.
    /// Includes an initial settle period for the runner to arm its keyboard hook.
    /// </summary>
    public static bool SendShortcutUntilVisible(Key[] activationKeys, int attempts = 5, int perAttemptMs = 3000)
    {
        // Give the runner time to register the global hotkey after the module is enabled.
        Thread.Sleep(1500);

        for (int i = 0; i < attempts; i++)
        {
            KeyboardHelper.SendKeys(activationKeys);
            if (WaitForShortcutGuideOverlay(perAttemptMs))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Map a single display token ("Win"/"Ctrl"/"Shift"/"Alt", a letter, "F5"…) to a <see cref="Key"/>.</summary>
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

        // Single digit 0–9 → enum names Num0..Num9.
        if (t.Length == 1 && t[0] >= '0' && t[0] <= '9')
        {
            return Enum.TryParse<Key>("Num" + t, out var num) ? num : null;
        }

        // Letters, function keys ("F5"), and named keys ("Space"/"Enter"/"Esc"/"Tab"…) match Key enum names.
        if (char.IsLetter(t[0]) && Enum.TryParse<Key>(t, ignoreCase: true, out var k))
        {
            return k;
        }

        return null;
    }

    /// <summary>True when the chord includes at least one non-modifier (main) key.</summary>
    private static bool HasMainKey(Key[] keys) =>
        keys.Any(k => k is not (Key.LWin or Key.Ctrl or Key.Shift or Key.Alt));
}

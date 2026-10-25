// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.ShortcutGuide.UITests;

internal static class TestHelper
{
    internal const string ShortcutGuideProcess = "PowerToys.ShortcutGuide";
    internal const string ModuleSettingsKey = "Shortcut Guide";

    // VK_OEM_2 is the default Shortcut Guide shortcut's slash key on the test desktop.
    private const Key SlashKey = (Key)0xBF;

    internal static void Step(UITestBase testBase, string message) =>
        testBase.TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] {message}");

    internal static void NavigateToSettingsPage(UITestBase testBase)
    {
        Step(testBase, "Navigating to Shortcut Guide settings.");
        if (!testBase.Session.Has(By.AccessibilityId("ShortcutGuideNavItem"), 500))
        {
            testBase.Session.Find<NavigationViewItem>(By.AccessibilityId("SystemToolsNavItem"), 5_000).Click(msPostAction: 0);
        }

        testBase.Session.Find<NavigationViewItem>(By.AccessibilityId("ShortcutGuideNavItem"), 5_000).Click(msPostAction: 0);
        Assert.IsTrue(
            testBase.Session.Has(By.AccessibilityId("Toggle_ShortcutGuide"), 10_000),
            "Shortcut Guide settings did not appear after navigation.");
    }

    internal static ToggleSwitch GetEnableToggle(UITestBase testBase) =>
        testBase.Session.Find<ToggleSwitch>(By.AccessibilityId("Toggle_ShortcutGuide"), 5_000);

    internal static void SetEnabled(UITestBase testBase, bool enabled)
    {
        Step(testBase, $"Setting Shortcut Guide enabled={enabled} through Settings.");
        var toggle = GetEnableToggle(testBase);
        if (toggle.IsOn != enabled)
        {
            toggle.Invoke(msPostAction: 0);
        }

        Assert.IsTrue(
            toggle.WaitForProperty("ToggleState", enabled ? "On" : "Off", 5_000),
            $"Shortcut Guide toggle did not reach enabled={enabled}.");
        Assert.IsTrue(
            WaitForProcess(enabled),
            $"Shortcut Guide process did not reach running={enabled}. {GetDiagnostics()}");
    }

    internal static Key[] ReadActivationShortcut(UITestBase testBase)
    {
        Step(testBase, "Reading the activation shortcut from Settings.");
        var button = testBase.Session.Find<Button>(By.AccessibilityId("EditButton"), 5_000);
        var shortcutText = button.HelpText;
        Assert.IsFalse(string.IsNullOrWhiteSpace(shortcutText), "The activation shortcut HelpText is empty.");

        var keys = shortcutText.Split(" + ", StringSplitOptions.RemoveEmptyEntries)
            .Select(token => ParseKeyToken(token.Trim()))
            .ToArray();
        Assert.IsTrue(
            keys.Any(key => key is not (Key.LWin or Key.Ctrl or Key.Shift or Key.Alt)),
            $"The activation shortcut '{shortcutText}' has no main key.");
        Step(testBase, $"Activation shortcut: '{shortcutText}' [{string.Join(", ", keys)}].");
        return keys;
    }

    internal static Session ActivateOverlay(UITestBase testBase, Key[] keys)
    {
        for (var attempt = 1; attempt <= 3 && !IsOverlayVisible(); attempt++)
        {
            FocusWindow(testBase.Session);
            Step(testBase, $"Sending activation shortcut, attempt {attempt}/3.");
            KeyboardHelper.SendChord(keys);
            WaitForOverlay(visible: true, timeoutMS: 5_000);
        }

        Assert.IsTrue(IsOverlayVisible(), $"Shortcut Guide overlay never appeared. {GetDiagnostics()}");
        var window = GetOverlayWindows().First(IsVisibleOverlay);
        Step(testBase, $"Waiting for overlay HWND={window.Hwnd} and its search control.");
        var overlay = WindowsFinder.WaitForWindowByApp(
            ShortcutGuideProcess,
            candidate => candidate.Hwnd == window.Hwnd.ToInt64(),
            timeoutMS: 15_000);
        Assert.IsNotNull(overlay, $"The visible overlay was not exposed to UI Automation. {GetDiagnostics()}");
        Assert.IsTrue(
            overlay.Has(By.AccessibilityId("ShortcutGuide_SearchBox"), 30_000),
            $"Shortcut Guide did not finish initializing its content. {GetDiagnostics()}");
        return overlay;
    }

    internal static void DismissOverlay(UITestBase testBase, Session overlay)
    {
        FocusWindow(overlay);
        Step(testBase, "Dismissing the visible overlay with Escape.");
        KeyboardHelper.SendKeys(Key.Esc);
        Assert.IsTrue(
            WaitForOverlay(visible: false),
            $"Shortcut Guide overlay stayed visible after Escape. {GetDiagnostics()}");
        Assert.IsTrue(
            WaitForProcess(running: true),
            "Dismissing the overlay must preserve the listening Shortcut Guide process.");
    }

    internal static void VerifyDisabledShortcut(UITestBase testBase, Key[] keys)
    {
        FocusWindow(testBase.Session);
        Step(testBase, "Sending the shortcut while Shortcut Guide is disabled.");
        KeyboardHelper.SendChord(keys);
        Assert.IsFalse(
            WaitForOverlay(visible: true, timeoutMS: 3_000),
            $"The disabled Shortcut Guide displayed an overlay. {GetDiagnostics()}");
        Assert.IsTrue(
            WaitForProcess(running: false),
            $"The disabled Shortcut Guide started a process after the shortcut. {GetDiagnostics()}");
    }

    internal static bool WaitForOverlay(bool visible, int timeoutMS = 10_000) =>
        WaitHelper.WaitForStable(
            IsOverlayVisible,
            actual => actual == visible,
            timeoutMS,
            requiredConsecutiveMatches: 3,
            pollIntervalMS: 100).Succeeded;

    internal static bool WaitForProcess(bool running, int timeoutMS = 15_000) =>
        WaitHelper.WaitForStable(
            GetProcessIds,
            ids => (ids?.Length > 0) == running,
            timeoutMS,
            requiredConsecutiveMatches: 3,
            pollIntervalMS: 200).Succeeded;

    internal static void CleanupTest(UITestBase testBase, bool originalEnabled)
    {
        try
        {
            SetEnabled(testBase, originalEnabled);
        }
        catch (Exception ex)
        {
            Step(testBase, $"Failed to restore the Shortcut Guide enable toggle: {ex}");
            throw;
        }
        finally
        {
            if (!WindowControl.TryCloseByApp(ShortcutGuideProcess))
            {
                Step(testBase, "No Shortcut Guide window could be closed during cleanup.");
            }
        }
    }

    private static void FocusWindow(Session session)
    {
        Assert.IsTrue(
            WindowControl.WaitForForeground(new IntPtr(session.WindowHandle), timeoutMS: 10_000, requiredConsecutiveMatches: 3),
            $"HWND={session.WindowHandle} did not own foreground before keyboard input. {GetDiagnostics()}");
    }

    private static Key ParseKeyToken(string token) => token.ToLowerInvariant() switch
    {
        "win" or "windows" => Key.LWin,
        "ctrl" or "control" => Key.Ctrl,
        "shift" => Key.Shift,
        "alt" => Key.Alt,
        "/" => SlashKey,
        _ when token.Length == 1 && char.IsAsciiDigit(token[0]) => Enum.Parse<Key>("Num" + token),
        _ when Enum.TryParse<Key>(token, ignoreCase: true, out var key) => key,
        _ => throw new NotSupportedException($"Unsupported activation shortcut token '{token}'."),
    };

    private static int[] GetProcessIds()
    {
        var processes = Process.GetProcessesByName(ShortcutGuideProcess);
        try
        {
            return processes.Select(process => process.Id).ToArray();
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
    }

    private static IReadOnlyList<WindowControl.ProcessWindow> GetOverlayWindows() =>
        WindowControl.EnumerateProcessWindows(GetProcessIds());

    private static bool IsVisibleOverlay(WindowControl.ProcessWindow window) =>
        window.IsVisible && window.Width > 300 && window.Height > 300 && !WindowHelper.IsWindowCloaked(window.Hwnd);

    private static bool IsOverlayVisible() => GetOverlayWindows().Any(IsVisibleOverlay);

    private static string GetDiagnostics() =>
        $"Processes=[{string.Join(", ", GetProcessIds())}]; " +
        $"windows=[{string.Join("; ", GetOverlayWindows())}]; " +
        $"foreground={WindowControl.GetForegroundWindowInfo()}.";
}

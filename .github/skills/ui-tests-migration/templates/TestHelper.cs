// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// TEMPLATE — a static helper for a `.Next` UI-test project, distilled from the validated ScreenRuler
// port. Copy alongside ModuleEndToEndTests.cs, then:
//   • Replace __MODULE__ (project name) and __MODULEUI__ (the module's PROCESS name, e.g.
//     "PowerToys.MeasureToolUI" — NOT the window title; see ModuleConfigData.cs in the harness).
//   • Fill in the AutomationIds for your module's nav item(s), toggle, and shortcut card from the
//     module's XAML (or discover them live: `winapp ui search "<id>" -a PowerToys.Settings --json`).
//   • Delete the helpers you don't need. Keep each helper ADAPTABLE — every module is different.
// See references/patterns-and-pitfalls.md for the full recipe catalog these are based on.
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.__MODULE__.UITests;

public static class TestHelper
{
    // ── Customize: AutomationIds + process name ───────────────────────────────────────────────
    // The module's PROCESS name (winappcli -a). Window TITLE may differ — use the process name.
    public const string ModuleProcess = "__MODULEUI__";

    // Left-nav item AutomationId for the module's Settings page, and its parent group (if the item
    // lives under a collapsible group like "System Tools"). Set ParentNavItemId to null if there's none.
    public const string NavItemId = "__MODULE__NavItem";
    public const string? ParentNavItemId = "SystemToolsNavItem";

    // The page enable ToggleSwitch and the ShortcutControl card AutomationIds.
    public const string ToggleId = "Toggle___MODULE__";
    public const string ShortcutCardId = "Shortcut___MODULE__";

    // ── Navigation ────────────────────────────────────────────────────────────────────────────

    /// <summary>Navigate to the module's Settings page (expanding its parent nav group if needed).</summary>
    public static void NavigateToPage(UITestBase testBase)
    {
        // A collapsible parent group hides its children until expanded; expand only when the child
        // isn't already in the tree (re-clicking an expanded group would collapse it).
        if (ParentNavItemId is not null && !testBase.Session.Has(By.AccessibilityId(NavItemId), 500))
        {
            testBase.Session.Find<NavigationViewItem>(By.AccessibilityId(ParentNavItemId), 5000).Click(msPostAction: 500);
        }

        testBase.Session.Find<NavigationViewItem>(By.AccessibilityId(NavItemId), 5000).Click(msPostAction: 800);
    }

    // ── Toggle ────────────────────────────────────────────────────────────────────────────────

    /// <summary>Set the page enable toggle and wait for the UI to reflect the new state.</summary>
    public static ToggleSwitch SetToggle(UITestBase testBase, bool enable)
    {
        var toggle = testBase.Session.Find<ToggleSwitch>(By.AccessibilityId(ToggleId), 5000);
        toggle.Toggle(enable);
        toggle.WaitForProperty("ToggleState", enable ? "On" : "Off", 5000);
        return toggle;
    }

    /// <summary>Set the toggle and assert it (and optionally the module process) reached the state.</summary>
    public static void SetAndVerifyToggle(UITestBase testBase, bool enable, bool verifyProcess = false, int timeoutMs = 10_000)
    {
        var toggle = SetToggle(testBase, enable);
        Assert.AreEqual(enable, toggle.IsOn, $"Toggle should be {(enable ? "On" : "Off")}.");
        if (verifyProcess)
        {
            Assert.IsTrue(
                WaitForProcess(ModuleProcess, expected: enable, timeoutMs),
                $"Process '{ModuleProcess}' should be {(enable ? "running" : "stopped")} after toggling.");
        }
    }

    // ── Activation shortcut ───────────────────────────────────────────────────────────────────

    /// <summary>Read the activation shortcut from the ShortcutControl's EditButton HelpText.</summary>
    public static Key[] ReadActivationShortcut(UITestBase testBase)
    {
        var card = testBase.Session.Find<Element>(By.AccessibilityId(ShortcutCardId), 5000);
        var editButton = card.Find<Element>(By.AccessibilityId("EditButton"), 5000);
        return ParseShortcutText(editButton.HelpText);
    }

    /// <summary>Parse "Win + Ctrl + Shift + M" into a Key chord (note: "win" maps to <see cref="Key.LWin"/>).</summary>
    public static Key[] ParseShortcutText(string shortcutText)
    {
        var keys = new List<Key>();
        if (string.IsNullOrEmpty(shortcutText))
        {
            return keys.ToArray();
        }

        foreach (var raw in shortcutText.Split(new[] { " + ", "+", " " }, StringSplitOptions.RemoveEmptyEntries))
        {
            var part = raw.Trim().ToLowerInvariant();
            Key? key = part switch
            {
                "win" or "windows" => Key.LWin,
                "ctrl" or "control" => Key.Ctrl,
                "shift" => Key.Shift,
                "alt" => Key.Alt,
                _ when part.Length == 1 && part[0] >= 'a' && part[0] <= 'z' =>
                    (Key)Enum.Parse(typeof(Key), part.ToUpperInvariant()),
                _ => null,
            };

            if (key.HasValue)
            {
                keys.Add(key.Value);
            }
        }

        return keys.ToArray();
    }

    // ── Module window lifecycle ───────────────────────────────────────────────────────────────

    /// <summary>True when at least one of the module's windows is open.</summary>
    public static bool IsModuleUIOpen() => WindowsFinder.ListByApp(ModuleProcess).Count > 0;

    /// <summary>Poll until the module UI reaches the requested presence.</summary>
    public static bool WaitForModuleUIState(bool shouldBeOpen, int timeoutMs = 5000, int pollMs = 100)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (IsModuleUIOpen() == shouldBeOpen)
            {
                return true;
            }

            Thread.Sleep(pollMs);
        }

        return false;
    }

    public static bool WaitForModuleUI(int timeoutMs = 5000) => WaitForModuleUIState(true, timeoutMs);

    public static bool WaitForModuleUIToDisappear(int timeoutMs = 5000) => WaitForModuleUIState(false, timeoutMs);

    /// <summary>
    /// Send the activation chord, retrying until the module UI appears. The runner arms its keyboard
    /// hook asynchronously after the module is enabled, so the first chord is easily lost — settle
    /// first, then retry (see Recipe 4 / Pitfall 14).
    /// </summary>
    public static bool SendShortcutUntilVisible(UITestBase testBase, Key[] activationKeys, int attempts = 5, int perAttemptMs = 3000)
    {
        Thread.Sleep(1500); // let the just-enabled module register its global hotkey
        for (int i = 0; i < attempts; i++)
        {
            KeyboardHelper.SendKeys(activationKeys);
            if (WaitForModuleUI(perAttemptMs))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Activate the module via its shortcut and return a PROCESS-scoped session for its window(s).
    /// Process scope (<see cref="Session.FromProcess"/>) resolves controls across whichever of the
    /// module's windows owns them — the winappcli equivalent of the legacy <c>global: true</c> Find.
    /// </summary>
    public static Session ActivateModule(UITestBase testBase, Key[] activationKeys, string testName)
    {
        ClipboardHelper.Clear();

        Assert.IsTrue(
            SendShortcutUntilVisible(testBase, activationKeys),
            $"Module UI should appear after the activation shortcut for {testName}: {string.Join(" + ", activationKeys)}");

        return Session.FromProcess(ModuleProcess, PowerToysModule.PowerToysSettings, timeoutMS: 5000);
    }

    /// <summary>Close the module UI if open (best-effort, tolerant — safe in a finally).</summary>
    public static void CloseModuleUI(UITestBase testBase)
    {
        if (!IsModuleUIOpen())
        {
            return;
        }

        // Prefer an in-UI Close button if the module has one; otherwise WM_CLOSE every window.
        // try { Session.FromProcess(ModuleProcess).Find<Element>(By.AccessibilityId("Button_Close"), 2000).Click(); } catch { }
        WindowControl.TryCloseByApp(ModuleProcess);
    }

    // ── Utilities ─────────────────────────────────────────────────────────────────────────────

    /// <summary>Poll for a process becoming present/absent (no built-in wait for this).</summary>
    public static bool WaitForProcess(string processName, bool expected, int timeoutMs)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if ((System.Diagnostics.Process.GetProcessesByName(processName).Length > 0) == expected)
            {
                return true;
            }

            Thread.Sleep(250);
        }

        return false;
    }

    /// <summary>
    /// Primary-monitor centre in PHYSICAL pixels — the right anchor for coordinate gestures (don't
    /// offset from the current cursor, which can be off-screen). Correct only when the test host is
    /// per-monitor DPI aware (add the app.manifest, Pitfall 12); otherwise the size is virtualized.
    /// </summary>
    public static (int X, int Y) ScreenCenter()
    {
        var size = System.Windows.Forms.SystemInformation.PrimaryMonitorSize;
        return (size.Width / 2, size.Height / 2);
    }
}

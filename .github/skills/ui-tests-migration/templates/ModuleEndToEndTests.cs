// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// TEMPLATE — a starting scaffold for a `.Next` UI-test class. Replace __MODULE__ / __MODULEUI__ /
// selectors with the real values for your module, delete what you don't need, and add test methods.
// See the skill's references/patterns-and-pitfalls.md for the full recipe catalog and
// ColorPickerEndToEndTests.cs for a complete worked example.
using System.Diagnostics;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.__MODULE__.UITests;

[TestClass]
public class __MODULE__EndToEndTests : UITestBase
{
    // Drive overlay/utility modules through the Settings scope so the runner owns the activation
    // hotkey and module toggles. `enableModules` enables ONLY the listed modules (disabling the rest)
    // before launch — pass just the one under test so the runner boots a single module (faster on a
    // fresh CI profile + isolated from other modules' hotkeys/overlays). The name is the settings.json
    // "enabled" key (note spaces, e.g. "Measure Tool", "PowerToys Run"). Add a WindowSize if needed.
    public __MODULE__EndToEndTests()
        : base(PowerToysModule.PowerToysSettings, enableModules: new[] { "__MODULE_SETTINGS_KEY__" })
    {
    }

    [TestMethod]
    [TestCategory("__MODULE__")]
    public void ExampleScenario()
    {
        try
        {
            RunTest();
        }
        finally
        {
            // Tolerant cleanup — close any window the test spawned, then Settings. Never throws, so it
            // can't mask the real failure.
            WindowControl.TryCloseByApp("__MODULEUI__");
            WindowControl.TryCloseByApp("PowerToys.Settings");
        }
    }

    private void RunTest()
    {
        // 1. Navigate to the module's Settings page (adjust selector / nav-item id for your module).
        //    Some pages use a left-nav NavigationViewItem by AutomationId; others a dashboard label.
        // Session.Find<NavigationViewItem>(By.AccessibilityId("__MODULE__NavItem")).Click(msPostAction: 500);

        // 2. Find the page enable toggle and verify the module process follows it.
        var toggle = Find<ToggleSwitch>(By.Name("__MODULE__"));
        bool initialIsOn = toggle.IsOn;

        try
        {
            if (!toggle.IsOn)
            {
                toggle.Toggle(true);
                Assert.IsTrue(toggle.WaitForProperty("ToggleState", "On", 5_000), "Toggle didn't turn On.");
                Assert.IsTrue(WaitForProcess("__MODULEUI__", expected: true, 10_000), "Process didn't start.");
            }

            // 3. Read the activation shortcut from the ShortcutControl's EditButton (HelpText carries
            //    the readable chord, e.g. "Win + Shift + C").
            var editButton = Find<Button>(By.AccessibilityId("EditButton"));
            Key[] keys = ParseShortcutText(editButton.HelpText);
            Assert.IsTrue(keys.Length > 0, $"Could not parse shortcut '{editButton.HelpText}'.");

            // 4. Fire the hotkey (retry — the runner arms its hook asynchronously) and wait for the
            //    module window/overlay to appear.
            Session? appWindow = null;
            for (int attempt = 1; attempt <= 3 && appWindow is null; attempt++)
            {
                KeyboardHelper.SendKeys(keys);
                appWindow = WindowsFinder.WaitForWindowByApp("__MODULEUI__", _ => true, timeoutMS: 2_500);
            }

            Assert.IsNotNull(appWindow, "Module window did not appear after firing the shortcut.");

            // 5. ... assert on the module's UI (read values, click, inspect tree, check clipboard) ...
            TestContext.WriteLine($"Module window appeared: hwnd={appWindow!.WindowHandle}");
        }
        finally
        {
            // Restore the toggle to its initial state, tolerantly.
            try
            {
                if (toggle.IsOn != initialIsOn)
                {
                    toggle.Toggle(initialIsOn);
                }
            }
            catch
            {
            }
        }
    }

    /// <summary>Poll for a process becoming present/absent (no built-in wait for this).</summary>
    private static bool WaitForProcess(string name, bool expected, int timeoutMS)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(timeoutMS);
        while (DateTime.UtcNow < deadline)
        {
            if ((Process.GetProcessesByName(name).Length > 0) == expected)
            {
                return true;
            }

            Thread.Sleep(250);
        }

        return false;
    }

    /// <summary>Parse a UI shortcut string like "Win + Shift + C" into the Key chord.</summary>
    private static Key[] ParseShortcutText(string shortcutText)
    {
        var parts = shortcutText.Split(new[] { " + ", "+", " " }, StringSplitOptions.RemoveEmptyEntries);
        var keys = new List<Key>();
        foreach (var raw in parts)
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
}

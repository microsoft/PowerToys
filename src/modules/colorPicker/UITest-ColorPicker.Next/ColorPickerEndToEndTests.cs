// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Text.Json;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.ColorPicker.UITests.Next;

/// <summary>
/// Full end-to-end Color Picker scenario, driven entirely through the Settings UI:
///   1. From the Settings app, navigate to the Color Picker page via the left nav
///      (NavigationViewItem "ColorPickerNavItem").
///   2. On the page, toggle the module OFF and verify <c>PowerToys.ColorPickerUI</c> exits.
///   3. Toggle it back ON and verify <c>PowerToys.ColorPickerUI</c> respawns.
///   4. Read the activation shortcut from the page's <c>ShortcutControl</c> (the EditButton
///      exposes <c>HotkeySettings.ToString()</c> via <c>AutomationProperties.HelpText</c>).
///   5. Move the cursor, send the shortcut chord.
///   6. Wait for the small picker overlay window, read the displayed HEX value.
///   7. Left-click to capture and open the editor.
///   8. Wait for the editor window and assert the same HEX appears in its tree.
/// </summary>
/// <remarks>
/// Inspired by <c>ScreenRuler.UITests/TestHelper.cs</c>'s pattern of navigating via the
/// settings nav item, flipping the toggle, and reading the shortcut from the
/// <c>EditButton.HelpText</c> — but driven through winappcli with no third-party engine.
/// </remarks>
[TestClass]
public class ColorPickerEndToEndTests : UITestBase
{
    public ColorPickerEndToEndTests()
        : base(PowerToysModule.PowerToysSettings)
    {
    }

    [TestMethod]
    [TestCategory("ColorPicker")]
    [TestCategory("winappcli-POC")]
    public void NavigateReadShortcutActivateAndCapture()
    {
        // Flip the picker to two-line layout (`showcolorname=true`) for the test. In one-line mode
        // (the default) the single ColorTextBlock has AutomationProperties.Name="{Binding ColorName}"
        // which masks its rendered Text — UIA only exposes the friendly name ("Black"), not the HEX.
        // In two-line mode the HEX TextBlock has no Name override, so WPF/UIA falls back to
        // exposing its rendered Text as the UIA Name. We restore the original value in finally.
        var originalShowColorName = ColorPickerSettingsFile.ReadShowColorName();
        if (!originalShowColorName)
        {
            ColorPickerSettingsFile.WriteShowColorName(true);
            Thread.Sleep(500); // FileSystemWatcher debounce + UI rebind
        }

        try
        {
            RunTest();
        }
        finally
        {
            // Universal cleanup: close any leftover ColorPicker window (overlay or editor),
            // then close the Settings window. Tolerant — never throws so it can't mask the
            // real test failure.
            WindowControl.TryCloseByApp("PowerToys.ColorPickerUI");
            WindowControl.TryCloseByApp("PowerToys.Settings");

            if (!originalShowColorName)
            {
                ColorPickerSettingsFile.WriteShowColorName(false);
            }
        }
    }

    private void RunTest()
    {
        // -- 1. Navigate via the utilities stack on the right of the dashboard ----------------
        // The Dashboard's right-side ModuleList renders each utility as a clickable SettingsCard
        // whose header is a TextBlock with the module's Label (e.g. "Color Picker"). The
        // SettingsCard itself isn't surfaced by name "Color Picker" in winappcli's search — only
        // its inner TextBlock label is — and the TextBlock has no InvokePattern (the click is
        // handled by the SettingsCard's OnSettingsCardClick).
        //
        // A "Color Picker" search returns 4 elements: the Quick-Access tile (Button) and its
        // label (TextBlock with invokableAncestor) on the left, plus the utility-stack label
        // (TextBlock) and ToggleSwitch on the right. We pick the rightmost TextBlock (largest
        // X coordinate) — that's the utility-stack label — and mouse-click it (winapp ui click
        // uses real mouse simulation, which triggers the ancestor SettingsCard's click).
        var matches = Session.FindAll<Element>(By.Name("Color Picker"));
        TestContext.WriteLine($"'Color Picker' search returned {matches.Count} elements:");
        foreach (var m in matches)
        {
            TestContext.WriteLine($"  [{m.ControlType,-10}] class='{m.ClassName}' at ({m.X},{m.Y}) {m.Width}x{m.Height} sel='{m.Selector}'");
        }

        var utilityItem = matches
            .Where(m => m.ClassName.Equals("TextBlock", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(m => m.X)
            .FirstOrDefault();
        Assert.IsNotNull(
            utilityItem,
            "Could not find a 'Color Picker' TextBlock to click. Is the dashboard visible? See element dump above.");
        TestContext.WriteLine($"Clicking utility-stack 'Color Picker' TextBlock at x={utilityItem!.X}, y={utilityItem.Y}");
        utilityItem.MouseClick(msPostAction: 800);
        TestContext.WriteLine("Navigated to Color Picker page (clicked utility-stack item).");

        // -- 2. Find the page-level enable toggle ---------------------------------------------
        // After navigation, the dashboard is gone and the page's enable toggle is the only
        // "Color Picker" ToggleSwitch in the tree. The ToggleSwitch wrapper pins
        // ClassName="ToggleSwitch" so the search is unambiguous.
        var toggle = Find<ToggleSwitch>(By.Name("Color Picker"));
        var initialIsOn = toggle.IsOn;
        TestContext.WriteLine($"Initial toggle state: IsOn={initialIsOn}");

        try
        {
            // -- 3. Toggle the module OFF and verify the runner terminates ColorPickerUI -----
            // If currently OFF, prime ON first so OFF→ON→OFF gives us a real lifecycle signal.
            if (!toggle.IsOn)
            {
                toggle.Toggle(true);
                Assert.IsTrue(
                    toggle.WaitForProperty("ToggleState", "On", timeoutMS: 5_000),
                    "Priming: toggle UI did not flip to On.");
                Assert.IsTrue(
                    WaitForProcess("PowerToys.ColorPickerUI", expected: true, timeoutMS: 10_000),
                    "Priming: PowerToys.ColorPickerUI did not start after enabling.");
            }

            toggle.Toggle(false);
            Assert.IsTrue(
                toggle.WaitForProperty("ToggleState", "Off", timeoutMS: 5_000),
                "Toggle UI did not flip to Off.");
            Assert.IsTrue(
                WaitForProcess("PowerToys.ColorPickerUI", expected: false, timeoutMS: 10_000),
                "PowerToys.ColorPickerUI did not exit within 10s after toggling module OFF.");
            TestContext.WriteLine("Toggled OFF; ColorPickerUI process exited.");

            // -- 4. Toggle the module ON and verify the runner respawns ColorPickerUI -------
            toggle.Toggle(true);
            Assert.IsTrue(
                toggle.WaitForProperty("ToggleState", "On", timeoutMS: 5_000),
                "Toggle UI did not flip to On.");
            Assert.IsTrue(
                WaitForProcess("PowerToys.ColorPickerUI", expected: true, timeoutMS: 10_000),
                "PowerToys.ColorPickerUI did not start within 10s after toggling module ON.");
            TestContext.WriteLine("Toggled ON; ColorPickerUI process running.");

            // -- 5. Read the activation shortcut from the UI --------------------------------
            // ShortcutControl renders the current shortcut on an inner Button (x:Name="EditButton")
            // whose AutomationProperties.HelpText is set to HotkeySettings.ToString() (e.g.
            // "Win + Shift + C"). x:Name reflects as the UIA AutomationId in WinUI when no
            // explicit AutomationId is set, so we look it up by that.
            var editButton = Find<Button>(By.AccessibilityId("EditButton"));
            var shortcutText = editButton.HelpText;
            TestContext.WriteLine($"Activation shortcut (from EditButton HelpText): '{shortcutText}'");
            Assert.IsFalse(
                string.IsNullOrWhiteSpace(shortcutText),
                "Could not read activation shortcut HelpText from the ShortcutControl EditButton.");

            var keys = ParseShortcutText(shortcutText);
            Assert.IsTrue(
                keys.Length > 0,
                $"Could not parse any keys from shortcut text '{shortcutText}'.");
            TestContext.WriteLine($"Parsed key chord: [{string.Join(", ", keys)}]");

            // -- 6. Park the cursor at a known on-screen location ---------------------------
            var screen = System.Windows.Forms.SystemInformation.PrimaryMonitorSize;
            int cx = screen.Width / 2;
            int cy = screen.Height / 2;
            MouseHelper.MoveTo(cx, cy);
            TestContext.WriteLine($"Cursor parked at ({cx}, {cy}) — primary screen center.");

            // -- 7. Activate via the configured shortcut ------------------------------------
            KeyboardHelper.SendKeys(keys);

            // -- 8. Wait for the small picker overlay window --------------------------------
            // ColorPickerUI's MainWindow (the cursor-following picker) is ~167x61 — much smaller
            // than the editor (~660x570). Filter by size to disambiguate when both could exist.
            var overlay = WindowsFinder.WaitForWindowByApp(
                "PowerToys.ColorPickerUI",
                w => w.Width < 300 && w.Height < 200,
                timeoutMS: 5_000);

            if (overlay is null)
            {
                var dump = string.Join(
                    Environment.NewLine,
                    WindowsFinder.ListByApp("PowerToys.ColorPickerUI")
                        .Select(w => $"    hwnd={w.Hwnd} title='{w.Title}' class='{w.ClassName}' size={w.Width}x{w.Height}"));
                Assert.Fail(
                    "Picker overlay did not appear within 5s after sending the shortcut." + Environment.NewLine +
                    "  Hotkey may not have fired through the runner's WH_KEYBOARD_LL hook." + Environment.NewLine +
                    "  Current ColorPickerUI windows:" + Environment.NewLine +
                    (dump.Length > 0 ? dump : "    (none)"));
            }

            TestContext.WriteLine($"Picker overlay: hwnd={overlay!.WindowHandle} title='{overlay.WindowTitle}'");

            // -- 9. Read the HEX color shown in the picker overlay --------------------------
            // In two-line layout (showcolorname=true, which we forced above), the overlay contains
            // two unnamed TextBlocks: row 0 shows ColorText (HEX), row 1 shows ColorName.
            // Neither sets AutomationProperties.Name, so WPF/UIA falls back to exposing each
            // TextBlock's rendered Text as its UIA Name. We pick the one whose Name is a 6-hex-char
            // RGB string — that's the HEX value.
            var overlayTree = overlay.Inspect(depth: 5);
            var overlayElements = new List<(string Type, string Name, string Value)>();
            WalkElements(overlayTree, overlayElements);

            TestContext.WriteLine($"Picker overlay exposed {overlayElements.Count} elements:");
            foreach (var v in overlayElements)
            {
                TestContext.WriteLine($"  [{v.Type,-8}] name='{v.Name}' value='{v.Value}'");
            }

            var pickerHex = overlayElements
                .Where(v => v.Type.Equals("Text", StringComparison.OrdinalIgnoreCase))
                .Select(v => ExtractHex(v.Name))
                .FirstOrDefault(h => h is not null);

            Assert.IsFalse(
                string.IsNullOrEmpty(pickerHex),
                "Could not find a HEX TextBlock in the picker overlay. " +
                "Did showcolorname flip take effect? See element dump above.");
            TestContext.WriteLine($"Picker HEX (normalized RGB): #{pickerHex}");

            // -- 10. Click to capture the color ---------------------------------------------
            MouseHelper.LeftClick();
            TestContext.WriteLine("Sent left-click to capture color.");

            // -- 11. Wait for the editor window ---------------------------------------------
            var editor = WindowsFinder.WaitForWindowByApp(
                "PowerToys.ColorPickerUI",
                w => w.Width > 300 && w.Height > 300,
                timeoutMS: 10_000);

            if (editor is null)
            {
                var dump = string.Join(
                    Environment.NewLine,
                    WindowsFinder.ListByApp("PowerToys.ColorPickerUI")
                        .Select(w => $"    hwnd={w.Hwnd} title='{w.Title}' class='{w.ClassName}' size={w.Width}x{w.Height}"));
                Assert.Fail(
                    "ColorPicker editor window did not appear within 10s after the click." + Environment.NewLine +
                    "  Current ColorPickerUI windows:" + Environment.NewLine +
                    (dump.Length > 0 ? dump : "    (none)"));
            }

            TestContext.WriteLine($"Editor window: hwnd={editor!.WindowHandle} title='{editor.WindowTitle}'");

            // -- 12. Find the picker HEX inside the editor's tree ---------------------------
            // From ColorEditorView.xaml the format list is populated from `ColorRepresentations`,
            // and the first enabled format in settings.json is HEX. Each format renders as a
            // ColorFormatControl (DataItem in the UIA tree) that contains a TextBox holding the
            // formatted color string. We inspect deep enough to walk past the DataItems into the
            // TextBox/Edit children, collect every textual value, and look for our picker HEX.
            var tree = editor.Inspect(depth: 12);
            var values = new List<(string Type, string Name, string Value)>();
            WalkElements(tree, values);

            TestContext.WriteLine($"Editor exposed {values.Count} elements. First 40:");
            foreach (var v in values.Take(40))
            {
                TestContext.WriteLine($"  [{v.Type,-12}] name='{v.Name}' value='{v.Value}'");
            }

            Assert.IsTrue(values.Count > 0, "Editor reported no readable elements via inspect --json.");

            // Match: find any element whose Name or Value contains the picker's 6-char RGB hex,
            // case-insensitively (e.g. picker "000000" matches editor "#FF000000" or "#000000";
            // for non-hex formats we accept the equivalent hex embedded in an ARGB rendering).
            var match = values.FirstOrDefault(v =>
                v.Name.Contains(pickerHex!, StringComparison.OrdinalIgnoreCase) ||
                v.Value.Contains(pickerHex!, StringComparison.OrdinalIgnoreCase));

            if (string.IsNullOrEmpty(match.Name) && string.IsNullOrEmpty(match.Value))
            {
                // Diagnostic: show what HEX-looking values the editor exposed.
                var hexFound = string.Join(
                    Environment.NewLine,
                    values
                        .Where(v => ExtractHex(v.Name) is not null || ExtractHex(v.Value) is not null)
                        .Take(15)
                        .Select(v => $"    [{v.Type}] name='{v.Name}' value='{v.Value}'"));
                Assert.Fail(
                    $"Picker HEX '{pickerHex}' not found in editor tree." + Environment.NewLine +
                    "  HEX-looking values seen in editor:" + Environment.NewLine +
                    (hexFound.Length > 0 ? hexFound : "    (none)"));
            }

            TestContext.WriteLine(
                $"MATCH: picker HEX '{pickerHex}' found in editor element [{match.Type}] Name='{match.Name}' Value='{match.Value}'");
        }
        finally
        {
            // Restore the toggle to its initial state regardless of pass/fail. Best-effort so
            // a cleanup failure can't mask the real test failure.
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

    /// <summary>
    /// Parse a UI-rendered shortcut string like <c>"Win + Shift + C"</c> into the
    /// <see cref="Key"/> sequence the harness's keyboard helper expects. Matches the parser
    /// pattern used by <c>ScreenRuler.UITests/TestHelper.cs</c>.
    /// </summary>
    private static Key[] ParseShortcutText(string shortcutText)
    {
        var separators = new[] { " + ", "+", " " };
        var parts = shortcutText.Split(separators, StringSplitOptions.RemoveEmptyEntries);
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

    /// <summary>Poll <see cref="Process.GetProcessesByName"/> until presence matches <paramref name="expected"/>.</summary>
    private static bool WaitForProcess(string name, bool expected, int timeoutMS)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(timeoutMS);
        while (DateTime.UtcNow < deadline)
        {
            var running = Process.GetProcessesByName(name).Length > 0;
            if (running == expected)
            {
                return true;
            }

            Thread.Sleep(250);
        }

        return false;
    }

    /// <summary>
    /// Extract a normalized RGB hex string (uppercase, no #, 6 chars) from arbitrary text,
    /// or null if the text doesn't represent a hex color. Handles "#RRGGBB", "RRGGBB",
    /// "#AARRGGBB", "AARRGGBB" forms. We strip the alpha prefix so the picker's RGB-only
    /// format (default "%Rex%Grx%Blx") matches the editor's ARGB rendering.
    /// </summary>
    private static string? ExtractHex(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var trimmed = text.Trim().TrimStart('#');
        if ((trimmed.Length != 6 && trimmed.Length != 8)
            || !System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^[0-9A-Fa-f]+$"))
        {
            return null;
        }

        if (trimmed.Length == 8)
        {
            trimmed = trimmed.Substring(2);
        }

        return trimmed.ToUpperInvariant();
    }

    /// <summary>
    /// Walk the nested <c>inspect --json</c> tree and collect every element with a non-empty
    /// name or value. Output shape (from winappcli):
    /// <c>{ "windows": [{ "elements": [{ "type", "name", "value", "children": [...] }] }] }</c>.
    /// </summary>
    private static void WalkElements(JsonElement root, List<(string Type, string Name, string Value)> sink)
    {
        if (!root.TryGetProperty("windows", out var windows) || windows.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var w in windows.EnumerateArray())
        {
            if (w.TryGetProperty("elements", out var els) && els.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in els.EnumerateArray())
                {
                    Walk(el, sink);
                }
            }
        }
    }

    private static void Walk(JsonElement el, List<(string Type, string Name, string Value)> sink)
    {
        var type = el.TryGetProperty("type", out var t) ? (t.GetString() ?? string.Empty) : string.Empty;
        var name = el.TryGetProperty("name", out var n) ? (n.GetString() ?? string.Empty) : string.Empty;
        var value = el.TryGetProperty("value", out var v) ? (v.GetString() ?? string.Empty) : string.Empty;

        if (!string.IsNullOrEmpty(name) || !string.IsNullOrEmpty(value))
        {
            sink.Add((type, name, value));
        }

        if (el.TryGetProperty("children", out var ch) && ch.ValueKind == JsonValueKind.Array)
        {
            foreach (var c in ch.EnumerateArray())
            {
                Walk(c, sink);
            }
        }
    }
}

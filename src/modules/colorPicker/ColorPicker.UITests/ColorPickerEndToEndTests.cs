// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Text.Json;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.ColorPicker.UITests;

/// <summary>
/// Full end-to-end Color Picker scenario, driven entirely through the Settings UI:
///   1. From the Settings app, navigate to the Color Picker page via the utilities stack.
///   2. On the page, toggle the module OFF and verify <c>PowerToys.ColorPickerUI</c> exits.
///   3. Toggle it back ON and verify <c>PowerToys.ColorPickerUI</c> respawns.
///   4. Read the activation shortcut from the page's <c>ShortcutControl</c> (the EditButton
///      exposes <c>HotkeySettings.ToString()</c> via <c>AutomationProperties.HelpText</c>).
///   5. Clear the clipboard, move the cursor, send the shortcut chord.
///   6. Wait for the picker overlay window and read the displayed HEX from the overlay's
///      automation-peer TextBlock (AutomationId="ColorHexAutomationPeer").
///   7. Left-click to capture. ColorPicker writes the captured color to the clipboard.
///   8. Read the captured value from the clipboard and assert it matches the overlay HEX.
///   9. Wait for the editor window and assert the captured value appears in its tree.
/// </summary>
/// <remarks>
/// The overlay's visible ColorTextBlock has <c>AutomationProperties.Name="{Binding ColorName}"</c>
/// so UIA exposes the friendly color name (e.g. "White"), not the HEX. To work around that,
/// MainView.xaml carries a hidden sibling TextBlock bound to <c>ColorText</c> with
/// <c>AutomationId="ColorHexAutomationPeer"</c> — a test-only UIA hook that lets us read the
/// actually-displayed HEX value without affecting the visual layout or accessibility UX.
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

            // -- 6. Clear the clipboard and park the cursor ---------------------------------
            // ClipboardHelper.Clear runs the Clipboard call on an STA thread (required by
            // System.Windows.Forms.Clipboard) and swallows any contention errors.
            var seedClipboard = ClipboardHelper.GetText();
            ClipboardHelper.Clear();
            TestContext.WriteLine($"Cleared clipboard. (Previous content was {seedClipboard.Length} chars.)");

            var screen = System.Windows.Forms.SystemInformation.PrimaryMonitorSize;
            int cx = screen.Width / 2;
            int cy = screen.Height / 2;
            MouseHelper.MoveTo(cx, cy);
            TestContext.WriteLine($"Cursor parked at ({cx}, {cy}) — primary screen center.");

            // -- 7+8. Activate via the shortcut, then wait for the picker overlay ------------
            // Enabling the module from Settings spawns the ColorPickerUI process, but the runner
            // wires its activation hotkey into the centralized WH_KEYBOARD_LL hook
            // asynchronously — the first chord can land before the hook is live. So send the
            // chord, wait briefly for the overlay, and resend if it didn't appear. ColorPickerUI's
            // MainWindow (cursor-following picker) is ~167x61 — much smaller than the editor
            // (~660x570) — so filter by size to disambiguate when both could exist.
            const int activationAttempts = 3;
            Session? overlay = null;
            for (int attempt = 1; attempt <= activationAttempts && overlay is null; attempt++)
            {
                TestContext.WriteLine($"Sending activation chord [{string.Join(", ", keys)}] (attempt {attempt}/{activationAttempts}).");
                KeyboardHelper.SendKeys(keys);

                overlay = WindowsFinder.WaitForWindowByApp(
                    "PowerToys.ColorPickerUI",
                    w => w.Width < 300 && w.Height < 200,
                    timeoutMS: attempt < activationAttempts ? 2_000 : 5_000);
            }

            if (overlay is null)
            {
                var dump = string.Join(
                    Environment.NewLine,
                    WindowsFinder.ListByApp("PowerToys.ColorPickerUI")
                        .Select(w => $"    hwnd={w.Hwnd} title='{w.Title}' class='{w.ClassName}' size={w.Width}x{w.Height}"));
                Assert.Fail(
                    $"Picker overlay did not appear after {activationAttempts} shortcut attempts." + Environment.NewLine +
                    "  Hotkey may not have fired through the runner's WH_KEYBOARD_LL hook." + Environment.NewLine +
                    "  Current ColorPickerUI windows:" + Environment.NewLine +
                    (dump.Length > 0 ? dump : "    (none)"));
            }

            TestContext.WriteLine($"Picker overlay appeared: hwnd={overlay!.WindowHandle}");

            // -- 9. Read the displayed HEX from the overlay's automation-peer TextBlock -----
            // The peer is a Visibility=Visible, Opacity=0 TextBlock added to MainView.xaml
            // specifically so UIA-driven tests can read the live HEX value. It is bound to
            // the same `ColorText` source as the visible TextBlock, so it always matches
            // what the user sees.
            string overlayHex = string.Empty;
            try
            {
                var peer = overlay.Find(By.AccessibilityId("ColorHexAutomationPeer"), timeoutMS: 2_000);
                overlayHex = peer.Name;
                TestContext.WriteLine($"Overlay HEX (from automation peer): '{overlayHex}'");
            }
            catch (Exception ex)
            {
                TestContext.WriteLine($"Could not read ColorHexAutomationPeer: {ex.Message}");
            }

            Assert.IsFalse(
                string.IsNullOrEmpty(overlayHex),
                "Failed to read the overlay's HEX value from the ColorHexAutomationPeer TextBlock.");

            // -- 10. Click to capture; ColorPicker writes the configured format to clipboard
            MouseHelper.LeftClick();
            TestContext.WriteLine("Sent left-click to capture color.");

            var capturedColor = ClipboardHelper.WaitForText(ignoredValue: string.Empty, timeoutMS: 3_000);
            Assert.IsFalse(
                string.IsNullOrEmpty(capturedColor),
                "Nothing was written to the clipboard within 3s after the click. " +
                "Did the picker actually capture? (Check that left-click is mapped to a 'PickColor' action.)");
            TestContext.WriteLine($"Captured color (clipboard): '{capturedColor}'");

            // Cross-check: the clipboard value should be the same HEX the overlay was showing.
            // Both come from `ColorText` in MainViewModel, just routed differently (overlay
            // binding vs. ColorPickerHelper.CopyToClipboard on Picker_MouseDown).
            Assert.IsTrue(
                ContainsIgnoringHash(capturedColor, overlayHex) || ContainsIgnoringHash(overlayHex, capturedColor),
                $"Overlay HEX '{overlayHex}' and clipboard '{capturedColor}' don't match.");
            TestContext.WriteLine("Overlay HEX matches clipboard value.");

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

            // -- 12. Find the captured color inside the editor's tree ------------------------
            // From ColorEditorView.xaml the format list is populated from `ColorRepresentations`.
            // Each format renders as a ColorFormatControl (DataItem in the UIA tree) that
            // contains a TextBox holding the formatted color string. The captured clipboard
            // value will be ONE of those formats — we just need to find any element whose Name
            // or Value contains it.
            var tree = editor.Inspect(depth: 12);
            var values = new List<(string Type, string Name, string Value)>();
            WalkElements(tree, values);

            TestContext.WriteLine($"Editor exposed {values.Count} elements. First 40:");
            foreach (var v in values.Take(40))
            {
                TestContext.WriteLine($"  [{v.Type,-12}] name='{v.Name}' value='{v.Value}'");
            }

            Assert.IsTrue(values.Count > 0, "Editor reported no readable elements via inspect --json.");

            // Match: find any element whose Name or Value contains the clipboard text
            // case-insensitively. If the clipboard had a '#' prefix (e.g. "#FFFFFF") and the
            // editor renders without it, also try the bare-hex form.
            var needle = capturedColor.Trim();
            var needleBareHex = needle.TrimStart('#');

            var match = values.FirstOrDefault(v =>
                v.Name.Contains(needle, StringComparison.OrdinalIgnoreCase) ||
                v.Value.Contains(needle, StringComparison.OrdinalIgnoreCase) ||
                (needleBareHex.Length > 0 &&
                    (v.Name.Contains(needleBareHex, StringComparison.OrdinalIgnoreCase) ||
                     v.Value.Contains(needleBareHex, StringComparison.OrdinalIgnoreCase))));

            if (string.IsNullOrEmpty(match.Name) && string.IsNullOrEmpty(match.Value))
            {
                Assert.Fail(
                    $"Captured color '{capturedColor}' not found in editor tree." + Environment.NewLine +
                    "  See element dump above.");
            }

            TestContext.WriteLine(
                $"MATCH: captured '{capturedColor}' found in editor element [{match.Type}] Name='{match.Name}' Value='{match.Value}'");
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
    /// Case-insensitive substring comparison that ignores a leading <c>#</c> on either side.
    /// Used to cross-check the overlay HEX against the clipboard value when only one of them
    /// carries the prefix.
    /// </summary>
    private static bool ContainsIgnoringHash(string haystack, string needle)
    {
        if (string.IsNullOrEmpty(haystack) || string.IsNullOrEmpty(needle))
        {
            return false;
        }

        return haystack.TrimStart('#').Contains(needle.TrimStart('#'), StringComparison.OrdinalIgnoreCase);
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

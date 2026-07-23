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
///   1. From the Settings app, expand System Tools and navigate to the Color Picker page.
///   2. On the page, toggle the module OFF and verify <c>PowerToys.ColorPickerUI</c> exits.
///   3. Toggle it back ON and verify <c>PowerToys.ColorPickerUI</c> respawns.
///   4. Read the activation shortcut from the page's <c>ShortcutControl</c> (the EditButton
///      exposes <c>HotkeySettings.ToString()</c> via <c>AutomationProperties.HelpText</c>).
///   5. Clear the clipboard, move the cursor, send the shortcut chord.
///   6. Wait for the picker overlay window and read the displayed HEX from the overlay's
///      automation-peer TextBlock (AutomationId="ColorHexAutomationPeer").
///   7. Zoom to 4x and 8x, then move across the highlighted source pixel and verify its color is unchanged.
///   8. Left-click to capture. ColorPicker writes the captured color to the clipboard.
///   9. Read the captured value from the clipboard and assert it matches the overlay HEX.
///  10. Wait for the editor window and assert the captured value appears in its tree.
/// </summary>
/// <remarks>
/// The overlay's visible ColorTextBlock uses its accessible name for screen-reader announcements,
/// which can include the friendly color name (e.g. "White"), not just the HEX. To keep the raw
/// value stable for UI automation, ColorPickerView.xaml carries a hidden sibling TextBlock with
/// <c>AutomationId="ColorHexAutomationPeer"</c> — a test-only UIA hook that lets us read the
/// actually-displayed HEX value without affecting the visual layout or accessibility UX.
/// </remarks>
[TestClass]
public class ColorPickerEndToEndTests : UITestBase
{
    // Enum wire values used by the settings schema: OpenColorPicker = 1 and
    // PickColorThenEditor = 0. The fixed shortcut keeps the Runner and UI test in sync.
    private const string DeterministicColorPickerSettings = """
        {
          "name": "ColorPicker",
          "version": "2.1",
          "properties": {
            "ActivationShortcut": {
              "win": true,
              "ctrl": false,
              "alt": false,
              "shift": true,
              "code": 67,
              "key": ""
            },
            "copiedcolorrepresentation": "HEX",
            "activationaction": 1,
            "primaryclickaction": 0,
            "colorhistorylimit": 20,
            "visiblecolorformats": {
              "HEX": {
                "Key": true,
                "Value": "%Rex%Grx%Blx"
              }
            }
          }
        }
        """;

    private static readonly string ColorPickerSettingsDirectory = Path.Combine(
        SettingsConfigHelper.PowerToysSettingsRoot,
        "ColorPicker");

    private static readonly string ColorPickerSettingsPath = Path.Combine(
        ColorPickerSettingsDirectory,
        "settings.json");

    private static readonly string ColorPickerHistoryPath = Path.Combine(
        ColorPickerSettingsDirectory,
        "colorHistory.json");

    private static readonly string[] EnabledModules = ["ColorPicker"];

    private static byte[]? originalSettingsContent;
    private static byte[]? originalHistoryContent;
    private static bool snapshotsCaptured;

    public ColorPickerEndToEndTests()
        : base(PowerToysModule.PowerToysSettings, enableModules: EnabledModules)
    {
    }

    [ClassInitialize]
    public static void PrepareColorPickerState(TestContext testContext)
    {
        ArgumentNullException.ThrowIfNull(testContext);
        StopPowerToysProcesses();

        originalSettingsContent = ReadFileIfPresent(ColorPickerSettingsPath);
        originalHistoryContent = ReadFileIfPresent(ColorPickerHistoryPath);
        snapshotsCaptured = true;

        try
        {
            Directory.CreateDirectory(ColorPickerSettingsDirectory);
            File.WriteAllText(ColorPickerSettingsPath, DeterministicColorPickerSettings);
            File.WriteAllText(ColorPickerHistoryPath, "[]");
        }
        catch (Exception setupError)
        {
            try
            {
                RestoreOriginalColorPickerFiles();
            }
            catch (Exception restoreError)
            {
                throw new AggregateException(
                    "Could not prepare or restore the Color Picker test state.",
                    setupError,
                    restoreError);
            }

            throw;
        }
    }

    [ClassCleanup(ClassCleanupBehavior.EndOfClass)]
    public static void RestoreColorPickerState()
    {
        if (!snapshotsCaptured)
        {
            return;
        }

        Exception? cleanupError = null;
        try
        {
            StopPowerToysProcesses();
        }
        catch (Exception ex)
        {
            cleanupError = ex;
        }

        try
        {
            RestoreOriginalColorPickerFiles();
        }
        catch (Exception ex)
        {
            cleanupError = cleanupError is null ? ex : new AggregateException(cleanupError, ex);
        }

        if (cleanupError is not null)
        {
            throw new IOException("Could not clean up the Color Picker test state.", cleanupError);
        }
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
        // -- 1. Navigate through the stable NavigationView automation contract ----------------
        // System Tools is collapsed by default, so expand it only when Color Picker is not yet
        // present in the UIA tree. These IDs are independent of display text and dashboard layout.
        if (!Session.Has(By.AccessibilityId("ColorPickerNavItem"), timeoutMS: 500))
        {
            Find<NavigationViewItem>(By.AccessibilityId("SystemToolsNavItem"), timeoutMS: 5_000).Click(msPostAction: 500);
        }

        Find<NavigationViewItem>(By.AccessibilityId("ColorPickerNavItem"), timeoutMS: 5_000).Click(msPostAction: 800);
        TestContext.WriteLine("Navigated to the Color Picker settings page.");

        // -- 2. Find the page-level enable toggle ---------------------------------------------
        var toggle = Find<ToggleSwitch>(By.AccessibilityId("Toggle_ColorPicker"), timeoutMS: 5_000);
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
            // The overlay (ColorPickerUI's MainWindow) is a small, LAYERED, transparent, topmost,
            // no-taskbar window. Once activated it stays visible and follows the cursor until a
            // click/Esc, so normally it shows immediately and winapp enumerates it WITHOUT any
            // cursor movement (the common path locally and on most agents). Intermittently on a
            // slow/loaded agent, winapp lists ZERO ColorPickerUI windows even though the runner
            // logged the hotkey firing and ColorPicker activated — i.e. the overlay never reached a
            // UIA-visible, on-screen state. Two mitigations, applied per attempt:
            //   * Poll patiently BEFORE re-sending: re-issuing the chord runs
            //     StartUserSession -> EndUserSession, which HIDES then re-shows the overlay, so the
            //     old 2s re-send cadence churned the window and a slow winapp poll kept missing it.
            //   * Reposition the cursor once mid-wait: ColorPicker re-positions + re-renders the
            //     overlay at the live cursor, recovering it if it landed off-screen/uncomposited.
            // We still retry because the very first chord can be lost if the runner hasn't finished
            // arming its WH_KEYBOARD_LL hook. The overlay is ~120x64 (vs the ~660x570 editor), so
            // filter by size; the cursor settles on a stable pixel for the later HEX read + click.
            const int activationAttempts = 3;
            Session? overlay = null;
            for (int attempt = 1; attempt <= activationAttempts && overlay is null; attempt++)
            {
                TestContext.WriteLine($"Sending activation chord [{string.Join(", ", keys)}] (attempt {attempt}/{activationAttempts}).");
                KeyboardHelper.SendKeys(keys);

                overlay = WindowsFinder.WaitForWindowByApp(
                    "PowerToys.ColorPickerUI", w => w.Width < 300 && w.Height < 200, timeoutMS: 2_500);

                if (overlay is null)
                {
                    // Recovery kick: nudge the overlay to a fresh on-screen spot, then keep polling
                    // before re-sending (which would hide/re-show it and restart the churn).
                    MouseHelper.MoveTo(cx + 60, cy + 60);
                    overlay = WindowsFinder.WaitForWindowByApp(
                        "PowerToys.ColorPickerUI", w => w.Width < 300 && w.Height < 200, timeoutMS: 2_500);
                }
            }

            if (overlay is null)
            {
                var dump = string.Join(
                    Environment.NewLine,
                    WindowsFinder.ListByApp("PowerToys.ColorPickerUI")
                        .Select(w => $"    hwnd={w.Hwnd} title='{w.Title}' class='{w.ClassName}' size={w.Width}x{w.Height}"));
                Assert.Fail(
                    $"Picker overlay did not appear after {activationAttempts} shortcut attempts." + Environment.NewLine +
                    "  The hotkey DID reach the runner (it logs 'ColorPicker hotkey is invoked') and ColorPicker" + Environment.NewLine +
                    "  activated, so this is the overlay (a small layered/transparent/topmost window) failing to" + Environment.NewLine +
                    "  become UIA-visible/on-screen on this agent — a rendering/enumeration issue, not input." + Environment.NewLine +
                    "  Current ColorPickerUI windows:" + Environment.NewLine +
                    (dump.Length > 0 ? dump : "    (none)"));
            }

            TestContext.WriteLine($"Picker overlay appeared: hwnd={overlay!.WindowHandle}");

            // -- 9. Read the displayed HEX from the overlay's automation-peer TextBlock -----
            // The peer is a Visibility=Visible, Opacity=0 TextBlock added to ColorPickerView.xaml
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

            // -- 10. Zoom without moving the cursor; the magnifier must not alter the sample --
            // Establish the baseline from the factor-1 magnifier after its captured image is on
            // screen. This avoids comparing against desktop content that may redraw between the
            // initial UIA read and the first zoom capture.
            MouseHelper.ScrollUp();

            var zoomWindow = WindowsFinder.WaitForWindowByApp(
                "PowerToys.ColorPickerUI",
                w => w.Width > 300 && w.Height > 300,
                timeoutMS: 2_500);
            Assert.IsNotNull(zoomWindow, "The zoom magnifier window did not appear after scrolling.");
            long zoomWindowHandle = zoomWindow!.WindowHandle;
            var (zoomLeft, _, zoomRight, _) = WindowHelper.GetWindowBounds(new IntPtr(zoomWindowHandle));
            Assert.IsTrue(zoomRight > zoomLeft, "Could not resolve the zoom window bounds.");
            double zoomDpiScale = (zoomRight - zoomLeft) / 430.0;
            (int zoomAnchorX, int zoomAnchorY) = MouseHelper.GetMousePosition();

            Thread.Sleep(750); // Window show/layout plus at least one color-sampling timer tick.
            string factorOneHex = ReadOverlayColor(overlay);
            Assert.IsFalse(string.IsNullOrEmpty(factorOneHex), "The factor-1 magnifier exposed no sampled color.");

            // Two more ticks reach factor 4, where the grid and center highlight appear.
            // The sampled color must still be the captured center pixel rather than that overlay.
            MouseHelper.ScrollUp();
            MouseHelper.ScrollUp();
            Thread.Sleep(750); // 200ms resize animation plus color-sampling timer ticks.
            string factorFourHex = ReadOverlayColor(overlay);
            Assert.AreEqual(
                factorOneHex,
                factorFourHex,
                "The 4x magnifier changed the color sampled at the stationary cursor.");

            // At factor 4 the center source cell is four DIPs wide. Moving +3 DIPs stays
            // inside that same source pixel but lands on the old static highlight's right stroke.
            MouseHelper.MoveTo(zoomAnchorX + (int)Math.Ceiling(3 * zoomDpiScale), zoomAnchorY);
            Thread.Sleep(750);
            string factorFourMovedHex = ReadOverlayColor(overlay);
            Assert.AreEqual(
                factorOneHex,
                factorFourMovedHex,
                "The 4x magnifier sampled its grid/highlight after the cursor moved.");

            MouseHelper.MoveTo(zoomAnchorX, zoomAnchorY);
            Thread.Sleep(750);

            MouseHelper.ScrollUp();
            Thread.Sleep(750);
            string factorEightHex = ReadOverlayColor(overlay);
            Assert.AreEqual(
                factorOneHex,
                factorEightHex,
                "The 8x magnifier changed the color sampled at the stationary cursor.");

            // The equivalent regression point at factor 8 is +7 DIPs within the same source cell.
            MouseHelper.MoveTo(zoomAnchorX + (int)Math.Ceiling(7 * zoomDpiScale), zoomAnchorY);
            Thread.Sleep(750);
            string factorEightMovedHex = ReadOverlayColor(overlay);
            Assert.AreEqual(
                factorOneHex,
                factorEightMovedHex,
                "The 8x magnifier sampled its grid/highlight after the cursor moved.");
            TestContext.WriteLine("Overlay color remained stable at 4x and 8x zoom, including pointer movement.");

            // Use the latest displayed value for the clipboard cross-check below.
            overlayHex = factorEightMovedHex;

            // -- 11. Click to capture; ColorPicker writes the configured format to clipboard
            MouseHelper.LeftClick();
            TestContext.WriteLine("Sent left-click to capture color.");

            var capturedColor = ClipboardHelper.WaitForText(ignoredValue: string.Empty, timeoutMS: 3_000);
            const string captureFailureMessage =
                "Nothing was written to the clipboard within 3s after the click. " +
                "Did the picker actually capture? (Check that left-click is mapped to a 'PickColor' action.)";
            Assert.IsFalse(
                string.IsNullOrEmpty(capturedColor),
                captureFailureMessage);
            TestContext.WriteLine($"Captured color (clipboard): '{capturedColor}'");

            // Cross-check: the clipboard value should be the same HEX the overlay was showing.
            // Both come from `ColorText` in MainViewModel, just routed differently (overlay
            // binding vs. ManagedCommon.ClipboardHelper.TrySetText in HandleMouseClickAction).
            Assert.IsTrue(
                ContainsIgnoringHash(capturedColor, overlayHex) || ContainsIgnoringHash(overlayHex, capturedColor),
                $"Overlay HEX '{overlayHex}' and clipboard '{capturedColor}' don't match.");
            TestContext.WriteLine("Overlay HEX matches clipboard value.");

            // -- 12. Wait for the editor window ---------------------------------------------
            var editor = WindowsFinder.WaitForWindowByApp(
                "PowerToys.ColorPickerUI",
                w => w.Hwnd != zoomWindowHandle && w.Width > 300 && w.Height > 300,
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

            // -- 13. Find the captured color inside the editor's tree ------------------------
            // From ColorEditorView.xaml the format list is populated from `ColorRepresentations`.
            // Each format renders a selectable TextBlock whose visible text must also be its UIA
            // Name. Unlike the WPF TextBox it replaced, a TextBlock has no ValuePattern fallback,
            // so accepting the value only through UIA Value would miss an accessibility regression.
            var tree = editor.Inspect(depth: 12);
            var values = new List<(string Type, string Name, string Value)>();
            WalkElements(tree, values);

            TestContext.WriteLine($"Editor exposed {values.Count} elements. First 40:");
            foreach (var v in values.Take(40))
            {
                TestContext.WriteLine($"  [{v.Type,-12}] name='{v.Name}' value='{v.Value}'");
            }

            Assert.IsTrue(values.Count > 0, "Editor reported no readable elements via inspect --json.");

            // Match the clipboard text through UIA Name, ignoring an optional leading '#'.
            var needle = capturedColor.Trim();
            var match = values.FirstOrDefault(v =>
                v.Type.Equals("Text", StringComparison.OrdinalIgnoreCase) &&
                ContainsIgnoringHash(v.Name, needle));

            if (string.IsNullOrEmpty(match.Name))
            {
                Assert.Fail(
                    $"Captured color '{capturedColor}' was not exposed through the formatted color TextBlock's UIA Name." + Environment.NewLine +
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

    private static string ReadOverlayColor(Session overlay)
    {
        return overlay.Find(By.AccessibilityId("ColorHexAutomationPeer"), timeoutMS: 2_000).Name;
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
            var processes = Process.GetProcessesByName(name);
            bool running;
            try
            {
                running = processes.Length > 0;
            }
            finally
            {
                foreach (Process process in processes)
                {
                    process.Dispose();
                }
            }

            if (running == expected)
            {
                return true;
            }

            Thread.Sleep(250);
        }

        return false;
    }

    private static void StopPowerToysProcesses()
    {
        string[] processNames =
        {
            "PowerToys",
            "PowerToys.Settings",
            "PowerToys.ColorPickerUI",
        };

        foreach (string processName in processNames)
        {
            WindowControl.TryKillProcessByName(processName);
        }

        foreach (string processName in processNames)
        {
            if (!WaitForProcess(processName, expected: false, timeoutMS: 5_000))
            {
                throw new InvalidOperationException($"Could not stop '{processName}' before changing the Color Picker test state.");
            }
        }
    }

    private static byte[]? ReadFileIfPresent(string path)
    {
        return File.Exists(path) ? File.ReadAllBytes(path) : null;
    }

    private static void RestoreOriginalColorPickerFiles()
    {
        if (!snapshotsCaptured)
        {
            return;
        }

        Exception? restoreError = null;
        try
        {
            RestoreFile(ColorPickerSettingsPath, originalSettingsContent);
        }
        catch (Exception ex)
        {
            restoreError = ex;
        }

        try
        {
            RestoreFile(ColorPickerHistoryPath, originalHistoryContent);
        }
        catch (Exception ex)
        {
            restoreError = restoreError is null ? ex : new AggregateException(restoreError, ex);
        }

        if (restoreError is not null)
        {
            throw new IOException("Could not restore the original Color Picker test files.", restoreError);
        }

        snapshotsCaptured = false;
        originalSettingsContent = null;
        originalHistoryContent = null;
    }

    private static void RestoreFile(string path, byte[]? originalContent)
    {
        const int maxAttempts = 5;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                if (originalContent is null)
                {
                    File.Delete(path);
                }
                else
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    File.WriteAllBytes(path, originalContent);
                }

                return;
            }
            catch (IOException) when (attempt < maxAttempts)
            {
                Thread.Sleep(100);
            }
            catch (UnauthorizedAccessException) when (attempt < maxAttempts)
            {
                Thread.Sleep(100);
            }
        }
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

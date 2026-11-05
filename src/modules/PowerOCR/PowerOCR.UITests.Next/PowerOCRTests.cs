// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Drawing;
using System.Text.Json;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Forms = System.Windows.Forms;

namespace PowerOCR.UITests;

[TestClass]
[DoNotParallelize]
public class PowerOCRTests : UITestBase
{
    private const string ProcessName = "PowerToys.PowerOCR";
    private const string CaptureText = "PowerToys Text Extractor";
    private const string LanguageMenuItemPrefix = "OCRLanguageMenuItem_";
    private const string DefaultSettings = """
        {
          "name": "TextExtractor",
          "version": "1",
          "properties": {
            "ActivationShortcut": {
              "win": true,
              "ctrl": false,
              "alt": false,
              "shift": true,
              "code": 84
            },
            "PreferredLanguage": ""
          }
        }
        """;

    private static readonly string[] ToolbarAutomationIds =
    [
        "OCROverlayLanguagesComboBox",
        "SingleLineToggleButton",
        "TableToggleButton",
        "SettingsButton",
        "CancelButton",
    ];

    private IDisposable? settingsSnapshot;
    private string? originalClipboard;
    private Point originalPointer;
    private Forms.Form? captureBackground;
    private IntPtr captureWindowHandle;
    private Thread? captureThread;
    private ManualResetEventSlim? captureReady;

    public PowerOCRTests()
        : base(PowerToysModule.PowerToysSettings, enableModules: ["TextExtractor"])
    {
    }

    protected override IReadOnlyList<string> StaleProcessNames { get; } =
        ["PowerToys", "PowerToys.Settings", ProcessName];

    private static Rectangle CaptureMonitorBounds =>
        (Forms.Screen.PrimaryScreen ?? throw new InvalidOperationException("The capture fixture needs a primary monitor.")).Bounds;

    protected override void PrepareTestState()
    {
        settingsSnapshot = SettingsConfigHelper.PreserveModuleSettings("TextExtractor");
        originalClipboard = ClipboardHelper.GetText();
        var pointer = MouseHelper.GetMousePosition();
        originalPointer = new Point(pointer.X, pointer.Y);
        SettingsConfigHelper.UpdateModuleSettings(
            "TextExtractor",
            DefaultSettings,
            settings =>
            {
                var defaults = System.Text.Json.Nodes.JsonNode.Parse(DefaultSettings)!;
                settings["properties"] = defaults["properties"]!.DeepClone();
                settings["properties"]!["PreferredLanguage"] = new Windows.Globalization.Language("en-US").NativeName;
            });
    }

    [TestInitialize]
    public void NavigateToTextExtractor()
    {
        Step("Opening Text Extractor Settings");
        if (!Session.Has(By.AccessibilityId("TextExtractorNavItem"), 500))
        {
            Find<NavigationViewItem>(By.AccessibilityId("SystemToolsNavItem")).Click(msPostAction: 0);
        }

        Find<NavigationViewItem>(By.AccessibilityId("TextExtractorNavItem")).Click(msPostAction: 0);
        Assert.IsTrue(
            Find<ToggleSwitch>(By.AccessibilityId("EnableTextExtractorToggleSwitch")).IsOn,
            "Text Extractor should start enabled.");
    }

    [TestCleanup]
    public async Task RestoreTextExtractorState()
    {
        try
        {
            await CaptureFailureArtifactsBeforeCleanupAsync();
        }
        finally
        {
            try
            {
                MouseHelper.MoveTo(originalPointer.X, originalPointer.Y);
                foreach (string name in new[] { ProcessName, "PowerToys.Settings", "PowerToys" })
                {
                    Assert.IsTrue(
                        WindowControl.TryKillProcessTreeByNameAndWait(name),
                        $"Could not stop test-owned {name} before restoring settings.");
                }
            }
            finally
            {
                try
                {
                    if (captureBackground is { IsHandleCreated: true, IsDisposed: false })
                    {
                        captureBackground.BeginInvoke(new Action(captureBackground.Close));
                    }

                    if (captureThread is not null)
                    {
                        Assert.IsTrue(captureThread.Join(TimeSpan.FromSeconds(5)), "The capture fixture should close after the test.");
                    }
                }
                finally
                {
                    captureReady?.Dispose();
                    settingsSnapshot?.Dispose();
                    if (originalClipboard is not null)
                    {
                        bool restored = originalClipboard.Length == 0
                            ? ClipboardHelper.Clear()
                            : ClipboardHelper.SetText(originalClipboard);
                        Assert.IsTrue(restored, "The original clipboard text should be restored.");
                    }
                }
            }
        }
    }

    [TestMethod]
    [TestCategory("PowerOCR Detection")]
    public void DetectTextExtractorTest()
    {
        var overlay = Activate();
        Step("Dismissing overlay with Escape");
        KeyboardHelper.SendKeys(Key.Esc);
        AssertOverlayClosed();

        overlay = Activate();
        Step("Opening overlay context menu with a right click");
        var bounds = GetBounds(overlay);
        var process = Microsoft.PowerToys.UITest.Next.Session.FromProcess(ProcessName);
        var menu = WaitHelper.WaitForStable(
            () => process.FindAll<Element>(By.AccessibilityId("CancelMenuItem"), 500)
                .FirstOrDefault(item => item.Width > 0 && item.Height > 0 && item.Displayed),
            item => item is not null,
            timeoutMS: 15_000,
            recover: _ =>
            {
                MouseHelper.MoveTo(bounds.Left + (bounds.Width / 2), bounds.Top + (bounds.Height / 2));
                MouseHelper.RightClick();
            });
        Assert.IsTrue(menu.Succeeded, $"The right-click context menu should appear. Foreground: {WindowControl.GetForegroundWindowInfo()}");
        Step("Clicking context-menu Cancel");
        ClickWithoutRaising(menu.LastObservation!);
        AssertOverlayClosed();
    }

    [TestMethod]
    [TestCategory("PowerOCR Settings")]
    public void DisableTextExtractorTest()
    {
        // Prove the shortcut works before checking its absence after the real Settings IPC.
        Activate();
        KeyboardHelper.SendKeys(Key.Esc);
        AssertOverlayClosed();

        Step("Disabling Text Extractor through Settings");
        var toggle = Find<ToggleSwitch>(By.AccessibilityId("EnableTextExtractorToggleSwitch"));
        toggle.Invoke(msPostAction: 0);
        Assert.IsTrue(toggle.WaitForProperty("ToggleState", "Off", 5_000), "The Settings switch should turn off.");
        Step("Sending the disabled utility's activation shortcut");
        KeyboardHelper.SendKeys(Key.LWin, Key.Shift, Key.T);
        Assert.IsNull(
            WindowsFinder.WaitForWindowByApp(ProcessName, IsOverlay, timeoutMS: 10_000),
            "Text Extractor should not activate after being disabled through Settings.");
    }

    [TestMethod]
    [TestCategory("PowerOCR Settings")]
    public void ActivationShortcutSettingsTest()
    {
        Step("Editing the activation shortcut");
        Find<Element>(By.AccessibilityId("TextExtractorActivationShortcut"))
            .Find<Button>(By.AccessibilityId("EditButton")).Invoke(msPostAction: 0);
        var save = Find<Button>(By.AccessibilityId("PrimaryButton"));
        Assert.IsTrue(
            WindowControl.WaitForForeground(new IntPtr(Session.WindowHandle), requiredConsecutiveMatches: 2),
            $"Settings should own keyboard input: {WindowControl.GetForegroundWindowInfo()}");
        KeyboardHelper.SendKeys(Key.Ctrl, Key.Shift, Key.T);
        Assert.IsTrue(save.WaitForProperty("IsEnabled", "true", 5_000), "The new shortcut should be valid.");
        save.Invoke(msPostAction: 0);
        Assert.IsTrue(save.WaitForGone(5_000), "The shortcut dialog should close after Save.");
        Activate(Key.Ctrl, Key.Shift, Key.T);
    }

    [TestMethod]
    [TestCategory("PowerOCR Settings")]
    public void OCRLanguageSettingsTest()
    {
        Step("Opening the Settings OCR language dropdown");
        Find<ComboBox>(By.AccessibilityId("TextExtractorLanguageComboBox")).Click(msPostAction: 0);
        var settings = Microsoft.PowerToys.UITest.Next.Session.FromProcess("PowerToys.Settings");
        Assert.IsTrue(ReadLanguageItems(settings).Count > 0, "Settings should offer OCR languages.");
        KeyboardHelper.SendKeys(Key.Esc);
    }

    [TestMethod]
    [TestCategory("PowerOCR Language")]
    public void OCRLanguageSelectionTest()
    {
        var overlay = Activate();
        var process = Microsoft.PowerToys.UITest.Next.Session.FromProcess(ProcessName);
        var combo = overlay.Find<ComboBox>(By.AccessibilityId("OCROverlayLanguagesComboBox"));
        Step("Reading the toolbar OCR languages");
        combo.Click(msPostAction: 0);
        var languages = ReadLanguageItems(process);
        int selectedIndex = ReadSelectedLanguageIndex(languages);
        Assert.IsTrue(languages.Count >= 2, "Install at least two OCR languages to verify changing languages.");
        string selectedName = languages[selectedIndex].Name;
        string targetName = languages[selectedIndex == 0 ? 1 : 0].Name;
        KeyboardHelper.SendKeys(Key.Esc);

        Step("Opening the language context menu with Shift+F10 from the ComboBox");
        KeyboardHelper.SendKeys(Key.Shift, Key.F10);
        var menuItems = process.FindAll<Element>(By.AccessibilityId(LanguageMenuItemPrefix));
        var selected = menuItems.Single(item => item.Name == selectedName);
        var target = menuItems.Single(item => item.Name == targetName);
        AssertToggleState(selected, "On", "The current language should be checked in the context menu.");
        AssertToggleState(target, "Off", "The target language should initially be unchecked.");
        int targetIndex = ReadLanguageMenuIndex(target);

        Step($"Selecting context-menu language '{targetName}' using the keyboard");
        KeyboardHelper.SendKeys(Key.Home);
        for (int index = 0; index < targetIndex; index++)
        {
            KeyboardHelper.SendKeys(Key.Down);
        }

        KeyboardHelper.SendKeys(Key.Enter);
        combo.Click(msPostAction: 0);
        languages = ReadLanguageItems(process, targetName);
        Assert.AreEqual(targetName, languages[ReadSelectedLanguageIndex(languages)].Name, "The toolbar should reflect the context-menu language change.");
    }

    [TestMethod]
    [TestCategory("PowerOCR Toolbar")]
    public void ToolbarModesTest()
    {
        var overlay = Activate();
        foreach (string id in new[] { "SingleLineToggleButton", "TableToggleButton" })
        {
            Step($"Toggling toolbar mode {id}");
            var button = overlay.Find<Element>(By.AccessibilityId(id));
            string initial = button.GetProperty("ToggleState");
            Assert.IsTrue(initial is "On" or "Off", $"{id} should expose a two-state toggle, not '{initial}'.");
            if (initial == "On")
            {
                ClickWithoutRaising(button);
                AssertToggleState(button, "Off", $"{id} should reset to off.");
            }

            ClickWithoutRaising(button);
            AssertToggleState(button, "On", $"{id} should turn on.");
        }

        KeyboardHelper.SendKeys(Key.Esc);
        AssertOverlayClosed();
    }

    [TestMethod]
    [TestCategory("PowerOCR Selection")]
    public void CursorStaysCrossDuringPointerMovementTest()
    {
        const int sweepCount = 3;
        const int stepsPerSweep = 60;
        var expectedCursor = Forms.Cursors.Cross.Handle;
        var overlay = Activate();
        var bounds = GetBounds(overlay);
        Assert.IsTrue(bounds.Width >= 400 && bounds.Height >= 400, "The overlay must have room for an interior pointer sweep.");
        Assert.AreEqual(Forms.MouseButtons.None, Forms.Control.MouseButtons, "Pointer sweeps must start without a pressed mouse button.");

        // Stay below the toolbar and away from monitor edges, where cursor changes are intentional.
        var interior = new Rectangle(bounds.Left + (bounds.Width / 4), bounds.Top + (bounds.Height / 2), bounds.Width / 2, bounds.Height / 4);
        int startX = interior.Left + 1;
        int startY = interior.Top + (interior.Height / 2);
        Step("Waiting for the cross cursor on the selection surface");

        // Real mouse input wakes a headless desktop's pointer; SetCursorPos alone does not.
        MouseHelper.MoveBy(1, 0, delayMs: 0);
        MouseHelper.MoveTo(startX, startY);
        Assert.IsTrue(
            SpinWait.SpinUntil(
                () =>
                {
                    var cursor = CursorStateReader.Read();
                    return cursor.IsVisible && cursor.Handle == expectedCursor && cursor.Position == new Point(startX, startY);
                },
                TimeSpan.FromSeconds(5)),
            $"The selection surface should display the system cross cursor before sweeping. Expected 0x{expectedCursor.ToInt64():X}; observed {CursorStateReader.Read()}; foreground {WindowControl.GetForegroundWindowInfo()}.");

        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var samplingStarted = new ManualResetEventSlim();
        var token = cancellation.Token;
        string? samplingFailure = null;
        int sampleCount = 0;
        var sampledPositions = new HashSet<Point>();
        var sampler = new Thread(() =>
        {
            var timer = Stopwatch.StartNew();
            samplingStarted.Set();
            try
            {
                while (!token.IsCancellationRequested)
                {
                    var cursor = CursorStateReader.Read();
                    if (interior.Contains(cursor.Position))
                    {
                        sampleCount++;
                        sampledPositions.Add(cursor.Position);
                        if (!cursor.IsVisible || cursor.Handle != expectedCursor)
                        {
                            samplingFailure = $"Cursor changed at {timer.Elapsed.TotalMilliseconds:F3} ms, position ({cursor.Position.X}, {cursor.Position.Y}): expected 0x{expectedCursor.ToInt64():X}, observed 0x{cursor.Handle.ToInt64():X}, visible={cursor.IsVisible}, sample {sampleCount}.";
                            return;
                        }
                    }

                    Thread.Yield();
                }
            }
            catch (Exception exception)
            {
                samplingFailure = $"Cursor sampling failed: {exception}";
            }
        })
        {
            IsBackground = true,
        };
        sampler.Start();

        int completedSteps = 0;
        try
        {
            Assert.IsTrue(samplingStarted.Wait(TimeSpan.FromSeconds(2)), "Cursor sampling should start before pointer movement.");
            Step("Dwelling for hover timers, then sampling three complete pointer sweeps");
            _ = sampler.Join(TimeSpan.FromMilliseconds(1500));
            for (int sweep = 0; sweep < sweepCount && sampler.IsAlive; sweep++)
            {
                for (int step = 0; step <= stepsPerSweep && sampler.IsAlive; step++)
                {
                    int offset = (interior.Width - 3) * step / stepsPerSweep;
                    int x = sweep % 2 == 0 ? startX + offset : interior.Right - 2 - offset;
                    MouseHelper.MoveTo(x, startY);
                    completedSteps++;
                    Thread.Sleep(10);
                }
            }
        }
        finally
        {
            cancellation.Cancel();
            Assert.IsTrue(sampler.Join(TimeSpan.FromSeconds(5)), "Cursor sampling should stop after the pointer sweep.");
        }

        Assert.IsNull(samplingFailure, samplingFailure);
        Assert.AreEqual(sweepCount * (stepsPerSweep + 1), completedSteps, "All three pointer sweeps should finish before the sampling deadline.");
        Assert.IsTrue(sampleCount >= 100, $"The pointer sweep should collect enough visible cursor samples; observed {sampleCount}.");
        Assert.IsTrue(sampledPositions.Count >= 20, $"Cursor samples should cover moving positions, not just the final frame; observed {sampledPositions.Count}.");
    }

    [TestMethod]
    [TestCategory("PowerOCR Selection")]
    public void TextSelectionAndClipboardTest()
    {
        WithCaptureBackground(showText: true, () =>
        {
            ClipboardHelper.Clear();
            var overlay = Activate();
            Step("Selecting the rendered text fixture");
            var bounds = GetBounds(overlay);
            DragCenter(overlay, bounds.Width * 3 / 4, bounds.Height / 3);
            string text = ClipboardHelper.WaitForText(string.Empty, timeoutMS: 15_000);
            string normalized = string.Join(" ", text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            Assert.IsTrue(
                normalized.Contains(CaptureText, StringComparison.OrdinalIgnoreCase),
                $"Clipboard should contain the rendered fixture text '{CaptureText}', but contained '{text}'.");
        });
    }

    [TestMethod]
    [TestCategory("PowerOCR Toolbar")]
    public void BlankCaptureKeepsToolbarShortcutsTest()
    {
        WithCaptureBackground(showText: false, () =>
        {
            var overlay = Activate();
            var singleLine = overlay.Find<Element>(By.AccessibilityId("SingleLineToggleButton"));
            var table = overlay.Find<Element>(By.AccessibilityId("TableToggleButton"));
            string initialSingleLine = singleLine.GetProperty("ToggleState");
            string initialTable = table.GetProperty("ToggleState");
            Assert.IsTrue(initialSingleLine is "On" or "Off", "Single-line mode should expose a two-state toggle.");
            Assert.IsTrue(initialTable is "On" or "Off", "Table mode should expose a two-state toggle.");

            var error = CaptureBlankRegion(overlay);
            foreach (string id in ToolbarAutomationIds)
            {
                var control = overlay.Find<Element>(By.AccessibilityId(id));
                Assert.IsTrue(control.Width > 0 && control.Height > 0, $"{id} should expose its bounds.");
                Assert.IsTrue(error.Y >= control.Y + control.Height, $"The error InfoBar must be below {id}, not overlap it.");
            }

            // Focusing a toolbar control here would mask the failed-capture focus regression.
            Step("Testing S/T shortcuts without refocusing the toolbar after a blank capture");
            KeyboardHelper.SendKeys(Key.S);
            AssertToggleState(singleLine, initialSingleLine == "Off" ? "On" : "Off", "S should toggle single-line mode immediately after a failed capture.");
            KeyboardHelper.SendKeys(Key.T);
            AssertToggleState(table, initialTable == "Off" ? "On" : "Off", "T should toggle table mode immediately after a failed capture.");
            KeyboardHelper.SendKeys(Key.S);
            AssertToggleState(singleLine, initialSingleLine, "S should restore single-line mode on the next press.");
            KeyboardHelper.SendKeys(Key.T);
            AssertToggleState(table, initialTable, "T should restore table mode on the next press.");
        });
    }

    [TestMethod]
    [TestCategory("PowerOCR Language")]
    public void BlankCaptureKeepsLanguageShortcutTest()
    {
        WithCaptureBackground(showText: false, () =>
        {
            var overlay = Activate();
            var process = Microsoft.PowerToys.UITest.Next.Session.FromProcess(ProcessName);
            var combo = overlay.Find<ComboBox>(By.AccessibilityId("OCROverlayLanguagesComboBox"));
            combo.Click(msPostAction: 0);
            var languages = ReadLanguageItems(process);
            int selectedIndex = ReadSelectedLanguageIndex(languages);
            Assert.IsTrue(languages.Count >= 2, "Install at least two OCR languages to verify the language shortcut.");
            string selectedName = languages[selectedIndex].Name;
            KeyboardHelper.SendKeys(Key.Esc);

            Step("Reading the product's numbered language indices from the context menu");
            KeyboardHelper.SendKeys(Key.Shift, Key.F10);
            var targets = process.FindAll<Element>(By.AccessibilityId(LanguageMenuItemPrefix))
                .Select(item => (item.Name, Index: ReadLanguageMenuIndex(item)))
                .Where(item => item.Name != selectedName && item.Index < 9)
                .OrderBy(item => item.Index)
                .ToList();
            Assert.IsTrue(targets.Count > 0, "A different OCR language should have a number shortcut.");
            var target = targets[0];
            KeyboardHelper.SendKeys(Key.Esc);
            CaptureBlankRegion(overlay);

            Step($"Selecting '{target.Name}' using its number shortcut immediately after the blank capture");
            KeyboardHelper.SendKeys((Key)((byte)Key.Num1 + target.Index));
            combo.Click(msPostAction: 0);
            languages = ReadLanguageItems(process, target.Name);
            Assert.AreEqual(
                target.Name,
                languages[ReadSelectedLanguageIndex(languages)].Name,
                "The language shortcut should change selection after a failed capture without a toolbar click.");
        });
    }

    private static bool IsOverlay(WindowsFinder.WindowInfo window) =>
        window.Title == "Text Extractor" && window.Width >= 400 && window.Height >= 400;

    private static void ClickWithoutRaising(Element element)
    {
        Assert.IsTrue(element.Width > 0 && element.Height > 0, $"'{element.Name}' should expose physical click bounds.");
        MouseHelper.LeftClickAt(element.X + (element.Width / 2), element.Y + (element.Height / 2));
    }

    private static Rectangle GetBounds(Session overlay)
    {
        var (left, top, right, bottom) = WindowHelper.GetWindowBounds(new IntPtr(overlay.WindowHandle));
        Assert.IsTrue(right > left && bottom > top, "The overlay should expose selection bounds.");
        return Rectangle.FromLTRB(left, top, right, bottom);
    }

    private static void DragCenter(Session overlay, int width, int height)
    {
        var bounds = GetBounds(overlay);
        int centerX = bounds.Left + (bounds.Width / 2);
        int centerY = bounds.Top + (bounds.Height / 2);
        MouseHelper.Drag(centerX - (width / 2), centerY - (height / 2), centerX + (width / 2), centerY + (height / 2), steps: 20);
    }

    private static List<LanguageItem> ReadLanguageItems(Session session, string? expectedSelectedName = null)
    {
        var result = WaitHelper.WaitForStable(
            () => ReadLanguageSnapshot(session),
            items => items is { Count: > 0 } &&
                items.Count(item => item.Selected) == 1 &&
                (expectedSelectedName is null || items.Single(item => item.Selected).Name == expectedSelectedName),
            timeoutMS: 10_000,
            requiredConsecutiveMatches: 2);
        Assert.IsTrue(
            result.Succeeded,
            $"The language dropdown should expose a stable selection '{expectedSelectedName ?? "(any)"}'. Last items: {string.Join(", ", result.LastObservation ?? [])}");
        return result.LastObservation!;
    }

    private static List<LanguageItem> ReadLanguageSnapshot(Session session)
    {
        var popup = WindowsFinder.WaitForWindowByApp(
            session.ProcessName,
            window => window.Title == "PopupHost" || window.ClassName.Contains("Popup", StringComparison.OrdinalIgnoreCase),
            timeoutMS: 5_000);
        Assert.IsNotNull(popup, $"The language dropdown should expose a popup HWND. Windows: {string.Join(", ", WindowsFinder.ListByApp(session.ProcessName))}");

        // Virtualized ComboBoxItem RuntimeIds can change between CLI invocations.
        // Keep names from the open dropdown, then resolve selection by name and class in one call.
        return ReadLanguageNames(popup.Inspect(depth: 12, hideOffscreen: true))
            .Distinct(StringComparer.Ordinal)
            .Select(name => new LanguageItem(name, ReadLanguageSelected(popup, name)))
            .ToList();
    }

    private static bool ReadLanguageSelected(Session session, string name)
    {
        var result = WinappCli.InvokeJson(
            "ui",
            "get-property",
            name,
            "--class-name",
            "ComboBoxItem",
            "-p",
            "IsSelected",
            session.TargetFlag,
            session.TargetValue,
            "--json");
        var selected = result.GetProperty("properties").GetProperty("IsSelected");
        Assert.IsTrue(bool.TryParse(selected.ToString(), out bool isSelected), $"The selection state for '{name}' should be boolean: {result}");
        return isSelected;
    }

    private static IEnumerable<string> ReadLanguageNames(JsonElement node)
    {
        if (node.ValueKind == JsonValueKind.Object)
        {
            if (node.TryGetProperty("className", out var className) &&
                className.GetString() == "ComboBoxItem" &&
                node.GetProperty("width").GetDouble() > 0 &&
                node.GetProperty("height").GetDouble() > 0 &&
                !node.GetProperty("isOffscreen").GetBoolean())
            {
                yield return node.GetProperty("name").GetString() ?? throw new InvalidOperationException("The language item has no name.");
            }

            foreach (var property in node.EnumerateObject())
            {
                foreach (string child in ReadLanguageNames(property.Value))
                {
                    yield return child;
                }
            }
        }
        else if (node.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in node.EnumerateArray())
            {
                foreach (string child in ReadLanguageNames(item))
                {
                    yield return child;
                }
            }
        }
    }

    private static int ReadSelectedLanguageIndex(List<LanguageItem> languages)
    {
        var selectedIndices = languages.Select((item, index) => (item, index)).Where(pair => pair.item.Selected).Select(pair => pair.index).ToList();
        Assert.AreEqual(
            1,
            selectedIndices.Count,
            $"The language ComboBox should have exactly one selected language. Items: {string.Join(", ", languages)}");
        return selectedIndices[0];
    }

    private static int ReadLanguageMenuIndex(Element item)
    {
        string id = item.AutomationId;
        Assert.IsTrue(id.StartsWith(LanguageMenuItemPrefix, StringComparison.Ordinal), $"The language menu item should expose its product index: '{id}'.");
        Assert.IsTrue(int.TryParse(id.AsSpan(LanguageMenuItemPrefix.Length), out int index) && index >= 0, $"Invalid language menu index: '{id}'.");
        return index;
    }

    private static void AssertToggleState(Element element, string expected, string message)
    {
        Assert.IsTrue(element.WaitForProperty("ToggleState", expected, 5_000), message);
    }

    private Session Activate(params Key[] keys)
    {
        if (keys.Length == 0)
        {
            keys = [Key.LWin, Key.Shift, Key.T];
        }

        Session? overlay = null;
        var captureBounds = CaptureMonitorBounds;
        for (int attempt = 1; attempt <= 3 && overlay is null; attempt++)
        {
            Step($"Activating Text Extractor with {string.Join(" + ", keys)}, attempt {attempt}");
            KeyboardHelper.SendKeys(keys);
            overlay = WindowsFinder.WaitForWindowByApp(ProcessName, IsOverlay, timeoutMS: 5_000);
        }

        Assert.IsNotNull(overlay, $"The Text Extractor overlay should appear after activation. Foreground: {WindowControl.GetForegroundWindowInfo()}");

        overlay = WindowsFinder.WaitForWindowByApp(
            ProcessName,
            window => IsOverlay(window) &&
                WindowHelper.GetWindowBounds(new IntPtr(window.Hwnd)) ==
                    (captureBounds.Left, captureBounds.Top, captureBounds.Right, captureBounds.Bottom),
            timeoutMS: 10_000);
        Assert.IsNotNull(overlay, "The overlay should finish positioning on the fixture's capture monitor.");

        // The native selection surface and the WinUI toolbar are independent topmost HWNDs.
        // Raising the toolbar's full-screen host would cover the native cross-cursor surface.
        var foreground = WaitHelper.WaitForStable(
            WindowControl.GetForegroundWindowInfo,
            info => info.ProcessId == overlay.ProcessId,
            timeoutMS: 5_000,
            requiredConsecutiveMatches: 2,
            recover: _ => WindowControl.TryFocusByApp(ProcessName));
        Assert.IsTrue(foreground.Succeeded, $"Text Extractor should own input: {foreground.LastObservation}");
        return overlay;
    }

    private void AssertOverlayClosed()
    {
        var result = WaitHelper.WaitForStable(
            () => WindowsFinder.ListByApp(ProcessName).Count(IsOverlay),
            count => count == 0,
            timeoutMS: 10_000,
            requiredConsecutiveMatches: 2);
        Assert.IsTrue(result.Succeeded, $"The overlay should be dismissed; remaining windows: {result.LastObservation}.");
    }

    private Element CaptureBlankRegion(Session overlay)
    {
        Assert.IsFalse(
            overlay.FindAll<Element>(By.AccessibilityId("ErrorInfoBar"), 500).Any(item => item.Displayed),
            "Overlay activation must succeed before testing a blank capture.");
        Step("Capturing a blank region and waiting for the error InfoBar");
        DragCenter(overlay, 150, 100);
        var result = WaitHelper.WaitForStable(
            () => overlay.FindAll<Element>(By.AccessibilityId("ErrorInfoBar"), 500)
                .FirstOrDefault(item => item.Displayed && item.Width > 0 && item.Height > 0),
            item => item is not null,
            timeoutMS: 15_000);
        Assert.IsTrue(result.Succeeded, "A blank capture should leave the overlay open with a new visible error InfoBar.");
        return result.LastObservation!;
    }

    private void WithCaptureBackground(bool showText, Action test)
    {
        captureReady = new ManualResetEventSlim();
        var ready = captureReady;
        Exception? backgroundError = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var font = new Font("Arial", 32);
                using var form = new Forms.Form
                {
                    BackColor = Color.White,
                    Bounds = CaptureMonitorBounds,
                    FormBorderStyle = Forms.FormBorderStyle.None,
                    ShowInTaskbar = false,
                    StartPosition = Forms.FormStartPosition.Manual,
                    Text = "PowerOCR capture fixture",
                    TopMost = true,
                };
                if (showText)
                {
                    form.Controls.Add(new Forms.Label
                    {
                        Dock = Forms.DockStyle.Fill,
                        Text = CaptureText,
                        TextAlign = ContentAlignment.MiddleCenter,
                        Font = font,
                        ForeColor = Color.Black,
                    });
                }

                captureBackground = form;
                form.Shown += (_, _) =>
                {
                    captureWindowHandle = form.Handle;
                    form.Refresh();
                    ready.Set();
                };
                Forms.Application.Run(form);
            }
            catch (Exception exception)
            {
                backgroundError = exception;
                ready.Set();
            }
        })
        {
            IsBackground = true,
        };
        thread.SetApartmentState(ApartmentState.STA);
        captureThread = thread;
        thread.Start();

        Step("Preparing the capture background fixture");
        Assert.IsTrue(ready.Wait(TimeSpan.FromSeconds(10)), "The capture background fixture should become visible.");
        Assert.IsNull(backgroundError, $"The capture background fixture failed: {backgroundError}");
        var fixture = captureBackground ?? throw new InvalidOperationException("The capture fixture was not created.");
        Assert.IsTrue(
            WindowControl.WaitForForeground(captureWindowHandle, requiredConsecutiveMatches: 2),
            $"The capture fixture should be foreground: {WindowControl.GetForegroundWindowInfo()}");
        fixture.Invoke(new Action(() => fixture.TopMost = false));
        test();
    }

    private void Step(string message) => TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] {message}");

    private sealed record LanguageItem(string Name, bool Selected);
}

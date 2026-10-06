// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Forms = System.Windows.Forms;

namespace Microsoft.PowerToys.ZoomIt.UITests;

public sealed partial class ZoomItTests
{
    private readonly string fixtureDirectory = Path.Combine(Path.GetTempPath(), "PowerToys.ZoomIt.UITests", Guid.NewGuid().ToString("N"));
    private Session? notepad;
    private string? notepadPath;
    private bool notepadSettingsOpen;
    private IntPtr notepadEditorWindow;
    private readonly Dictionary<string, bool> notepadEditingOptions = [];

    [TestMethod]
    [DataRow("Top left corner", 0)]
    [DataRow("Bottom right corner", 8)]
    public void BreakTimerPositionChangesRenderedLocation(string caption, int position)
    {
        ui.Select("ZoomItBreakTimerPosition", caption, "BreakTimerPosition", position);
        var shortcut = ui.Shortcut("ZoomItBreakShortcut");
        desktop = new DesktopFixture();
        ui.Activate(shortcut);
        var bounds = WaitForTimerPixels();
        var size = Forms.SystemInformation.PrimaryMonitorSize;
        Assert.IsTrue(
            position == 0
                ? bounds.Left < size.Width / 3 && bounds.Top < size.Height / 3
                : bounds.Right > size.Width * 2 / 3 && bounds.Bottom > size.Height * 2 / 3,
            $"The {caption} timer rendered in the wrong location: {bounds}.");
        SaveDesktop($"timer-position-{position}");
        ui.Exit();
    }

    [TestMethod]
    [DataRow(40)]
    [DataRow(70)]
    public void BreakTimerOpacityChangesWindowAndPixels(int opacity)
    {
        ui.SetSlider("ZoomItBreakTimerOpacity", opacity, "BreakOpacity", opacity);
        var shortcut = ui.Shortcut("ZoomItBreakShortcut");
        desktop = new DesktopFixture();
        var overlay = ui.Activate(shortcut);
        var expectedAlpha = (byte)(opacity * 255 / 100);
        var applied = WaitHelper.WaitForStable(
            () => GetLayeredWindowAttributes(new IntPtr(overlay.WindowHandle), out _, out var alpha, out _) ? (int)alpha : -1,
            alpha => Math.Abs(alpha - expectedAlpha) <= 1,
            10_000,
            3);
        Assert.IsTrue(applied.Succeeded, $"Break opacity {opacity}% did not reach the window; alpha={applied.LastObservation}.");

        var source = DesktopFixture.BackgroundColor;
        var expected = Color.FromArgb(source.R * (100 - opacity) / 100, source.G * (100 - opacity) / 100, source.B * (100 - opacity) / 100);
        var pixels = WaitHelper.WaitForStable(
            () =>
            {
                using var bitmap = DesktopFixture.Capture();
                return bitmap.GetPixel(100, 100);
            },
            color => DesktopFixture.IsColor(color, expected, 3),
            10_000,
            3);
        Assert.IsTrue(pixels.Succeeded, $"Break opacity did not blend the visible background: expected {expected}, actual {pixels.LastObservation}.");
        SaveDesktop($"timer-opacity-{opacity}");
        ui.Exit();
    }

    [TestMethod]
    public void BreakTimerUsesSelectedBackgroundImage()
    {
        Directory.CreateDirectory(fixtureDirectory);
        var path = Path.Combine(fixtureDirectory, "break-background.bmp");
        var color = Color.FromArgb(0, 96, 192);
        using (var bitmap = new Bitmap(320, 180))
        {
            using var graphics = Graphics.FromImage(bitmap);
            graphics.Clear(color);
            bitmap.Save(path, ImageFormat.Bmp);
        }

        ui.Select("ZoomItBreakShowBackgroundBitmap", "Use image file as background", "BreakShowBackgroundFile", 1);
        ui.PickFile("ZoomItBreakBackgroundFile", path, "BreakBackgroundFile");
        var shortcut = ui.Shortcut("ZoomItBreakShortcut");
        desktop = new DesktopFixture();
        ui.Activate(shortcut);
        WaitForTimerPixels();
        var result = WaitHelper.WaitForStable(
            () =>
            {
                using var bitmap = DesktopFixture.Capture();
                return new[] { bitmap.GetPixel(100, 100), bitmap.GetPixel(bitmap.Width - 100, bitmap.Height - 100) };
            },
            colors => colors is not null && colors.All(actual => DesktopFixture.IsColor(actual, color)),
            10_000,
            3);
        Assert.IsTrue(result.Succeeded, "The selected image did not fill the timer background.");
        SaveDesktop("timer-image");
        ui.Exit();
    }

    [TestMethod]
    public void TypeFontSelectionChangesBreakTimerGlyphs()
    {
        var shortcut = ui.Shortcut("ZoomItBreakShortcut");
        desktop = new DesktopFixture();
        ui.Activate(shortcut);
        var original = WaitForTimerPixels();
        SaveDesktop("timer-segoe-ui");
        ui.Exit();

        ui.Step("Choosing Courier New through the native font dialog");
        var dialog = ui.OpenDialog<ComboBox>("ZoomItTypeTextFont", "1136");
        Assert.IsTrue(WindowControl.WaitForForeground(new IntPtr(dialog.WindowHandle), 10_000), "The font picker did not acquire foreground.");
        dialog.Find<ComboBox>(By.AccessibilityId("1136")).Focus();
        Assert.IsTrue(ClipboardHelper.SetText("Courier New"), "Could not prepare the font name.");
        KeyboardHelper.SendChord(Key.Ctrl, Key.A);
        KeyboardHelper.SendChord(Key.Ctrl, Key.V);
        KeyboardHelper.SendChord(Key.Tab);
        dialog.Find<Button>(By.AccessibilityId("1")).Invoke(msPostAction: 0);
        var font = WaitHelper.WaitForStable(
            () => ZoomItState.Read("Font") is byte[] bytes && bytes.Length == 92 ? Encoding.Unicode.GetString(bytes, 28, 64).TrimEnd('\0') : string.Empty,
            name => name == "Courier New",
            10_000,
            2);
        Assert.IsTrue(font.Succeeded, $"The native font selection did not persist: {font.LastObservation}.");
        desktop.Show();
        ui.Activate(shortcut);
        var changed = WaitForTimerPixels();
        Assert.IsTrue(Math.Abs(changed.Width - original.Width) > original.Width * 0.05, $"Changing the font did not change timer glyph geometry: {original} -> {changed}.");
        SaveDesktop("timer-courier-new");
        ui.Exit();
    }

    [TestMethod]
    public void NativeFontDialogWaitsForDelayedAppearance()
    {
        var process = Environment.ProcessId.ToString(CultureInfo.InvariantCulture);
        desktop = new DesktopFixture();
        var timer = Stopwatch.StartNew();
        desktop.ShowFontDialogAfterDelay(8_000);
        try
        {
            var dialog = ui.WaitForNativeDialog<ComboBox>(process, "1136");
            Assert.IsTrue(timer.Elapsed >= TimeSpan.FromSeconds(8), "The regression dialog must appear after the former five-second timeout.");
            Assert.IsTrue(dialog.Find<ComboBox>(By.AccessibilityId("1136")).IsEnabled, "The delayed font selector must be ready for input.");
            SaveDesktop("delayed-font-dialog");
        }
        finally
        {
            foreach (var dialog in WindowsFinder.ListByApp(process).Where(window => window.ClassName == "#32770"))
            {
                Assert.IsTrue(WindowControl.TryCloseWindow(dialog.Hwnd), "The test-owned delayed font dialog did not close.");
            }
        }
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("    ")]
    [DataRow("prefix ")]
    public void DemoTypeTypesSelectedFileIntoNotepad(string prefix)
    {
        const string expected = "ZoomIt types this exact sentence into Notepad.";
        PrepareDemoScript(expected + "[end]");
        ui.SetSlider("ZoomItDemoTypeSpeedSlider", 10, "DemoTypeSpeedSlider", 10);
        var shortcut = ui.Shortcut("ZoomItDemoTypeShortcut");
        OpenNotepad();
        if (prefix.Length > 0)
        {
            Assert.IsTrue(ClipboardHelper.SetText(prefix), "Could not prepare the existing Notepad text.");
            KeyboardHelper.SendChord(Key.Ctrl, Key.V);
            WaitForNotepadText(prefix, 10_000);
        }

        KeyboardHelper.SendChord(Key.Ctrl, Key.End);
        var caret = WaitHelper.WaitForStable(
            ReadNotepadSelection,
            selection => selection.Start == prefix.Length && selection.End == prefix.Length,
            10_000,
            2);
        Assert.IsTrue(caret.Succeeded, $"Notepad's caret must follow the existing text before Demo Type: {caret.LastObservation}.");
        ui.Step($"Typing the selected Demo Type file into Notepad; foreground: {WindowControl.GetForegroundWindowInfo()}");
        KeyboardHelper.SendChord(shortcut);
        WaitForNotepadText(prefix + expected, 90_000);
        SaveDesktop("demo-type-notepad");
    }

    [TestMethod]
    public void DemoTypeSpeedChangesTypingDurationAndEscapeStopsTyping()
    {
        var expected = new string('a', 80);
        PrepareDemoScript(expected + "[end]");
        var shortcut = ui.Shortcut("ZoomItDemoTypeShortcut");
        var durations = new List<TimeSpan>();
        desktop = new DesktopFixture();
        desktop.ShowEditor();

        foreach (var speed in new[] { 10, 100 })
        {
            ui.SetSlider("ZoomItDemoTypeSpeedSlider", speed, "DemoTypeSpeedSlider", speed);
            desktop.FocusEditor();
            desktop.ClearEditor();
            var timer = Stopwatch.StartNew();
            ui.Step($"Measuring Demo Type at speed {speed}");
            KeyboardHelper.SendChord(shortcut);
            var completed = WaitHelper.WaitForStable(desktop.ReadEditorText, text => text == expected, 45_000, pollIntervalMS: 50);
            Assert.IsTrue(completed.Succeeded, $"Demo Type did not finish at speed {speed}: '{completed.LastObservation}'.");
            durations.Add(timer.Elapsed);
        }

        Assert.IsTrue(durations[0] > durations[1] + TimeSpan.FromSeconds(2), $"The faster speed did not reduce typing time: slow={durations[0]}, fast={durations[1]}.");

        PrepareDemoScript(new string('b', 2_000) + "[end]");
        desktop.FocusEditor();
        desktop.ClearEditor();
        KeyboardHelper.SendChord(shortcut);
        bool IsPartialScript(string? text) => text is { Length: > 0 and < 2_000 } && text.All(character => character == 'b');
        var started = WaitHelper.WaitForStable(desktop.ReadEditorText, IsPartialScript, 15_000);
        Assert.IsTrue(started.Succeeded, $"Long Demo Type script did not begin: '{started.LastObservation}'.");
        ui.Step("Stopping Demo Type with Escape before the script completes");
        KeyboardHelper.SendChord(Key.Esc);
        string? previous = null;
        var stopped = WaitHelper.WaitForStable(
            desktop.ReadEditorText,
            text =>
            {
                var stable = text == previous && IsPartialScript(text);
                previous = text;
                return stable;
            },
            10_000,
            4,
            250);
        Assert.IsTrue(stopped.Succeeded, "Escape did not stop Demo Type before the file completed.");
        SaveDesktop("demo-type-stopped");
    }

    [TestMethod]
    public void BreakTimerExpiresWithSelectedSoundConfigured()
    {
        Directory.CreateDirectory(fixtureDirectory);
        var path = Path.Combine(fixtureDirectory, "alarm.wav");
        WriteAlarm(path);
        ui.SetCheck("ZoomItBreakPlaySoundsFile", true, "BreakPlaySoundFile");
        ui.PickFile("ZoomItBreakSoundFile", path, "BreakSoundFile");
        var shortcut = ui.Shortcut("ZoomItBreakShortcut");
        desktop = new DesktopFixture();
        ui.Activate(shortcut);
        var initial = WaitForTimerPixels();
        Rectangle ReadOvertime()
        {
            using var bitmap = DesktopFixture.Capture();
            using var belowTimer = bitmap.Clone(
                new Rectangle(0, initial.Bottom + 20, bitmap.Width, bitmap.Height - initial.Bottom - 20),
                bitmap.PixelFormat);
            return DesktopFixture.ColorBounds(belowTimer, color => color.R > 235 && color.G > 235 && color.B > 235);
        }

        Assert.IsTrue(ReadOvertime().IsEmpty, "The overtime line must be absent before expiration.");
        ui.Step("Waiting for the real one-minute timer to expire");
        var clock = Stopwatch.StartNew();
        var expired = WaitHelper.WaitForStable(
            ReadOvertime,
            bounds => clock.Elapsed >= TimeSpan.FromSeconds(59) && bounds.Height > 20 && bounds.Height < initial.Height * 0.8,
            80_000,
            3,
            500);
        Assert.IsTrue(expired.Succeeded, $"The one-minute timer did not add its smaller overtime line below 0:00: {expired.LastObservation}.");
        SaveDesktop("timer-expired");
        ui.Exit();
        TestContext.WriteLine("Checklist 18: sound selection and real timer expiration are automated. Audible playback requires an audio-output-equipped machine; these VMs cannot establish that signal.");
    }

    private void PrepareDemoScript(string script)
    {
        Directory.CreateDirectory(fixtureDirectory);
        var path = Path.Combine(fixtureDirectory, Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllText(path, script, Encoding.Unicode);
        ui.PickFile("ZoomItDemoTypeFile", path, "DemoTypeFile");
    }

    private Element OpenNotepad()
    {
        Directory.CreateDirectory(fixtureDirectory);
        var target = Path.Combine(fixtureDirectory, "ZoomIt-" + Guid.NewGuid().ToString("N") + ".txt");
        notepadPath = target;
        File.WriteAllText(target, string.Empty);
        ui.Step("Opening the test-owned Notepad document");
        using var launcher = Process.Start(new ProcessStartInfo("notepad.exe") { UseShellExecute = true, ArgumentList = { target } });
        var document = WaitForNotepadDocument();
        WindowHelper.MaximizeWindow(new IntPtr(document.Window.WindowHandle));
        document = WaitForNotepadDocument(requireSettings: true);
        notepad = document.Window;
        if (document.SettingsButton is not null)
        {
            notepadSettingsOpen = true;
            document.SettingsButton.Invoke(msPostAction: 0);
            var settingsWindow = WaitForNotepadSettings();
            foreach (var option in new[] { "Autocorrect", "Spell check" })
            {
                if (settingsWindow.Has<ToggleSwitch>(By.Name(option), 2_000))
                {
                    var toggle = settingsWindow.Find<ToggleSwitch>(By.Name(option));
                    notepadEditingOptions.Add(option, toggle.IsOn);
                    if (toggle.IsOn && toggle.IsEnabled)
                    {
                        toggle.Invoke(msPostAction: 0);
                        Assert.IsTrue(toggle.WaitForProperty("ToggleState", "Off", 10_000), $"Notepad {option} did not turn off.");
                    }
                }
            }
        }

        var editor = FocusNotepad();
        KeyboardHelper.SendChord(Key.Shift, Key.X);
        WaitForNotepadText("X", 10_000);
        KeyboardHelper.SendChord(Key.Backspace);
        WaitForNotepadText(string.Empty, 10_000);
        return editor;
    }

    private Session? FindOwnedNotepadWindow()
    {
        Assert.IsNotNull(notepadPath);
        string document = Path.GetFileNameWithoutExtension(notepadPath);
        return WindowsFinder.WaitForWindowByApp(
            "notepad",
            window => window.ProcessName.Equals("notepad", StringComparison.OrdinalIgnoreCase) &&
                (window.Title.Contains(document, StringComparison.OrdinalIgnoreCase) ||
                    (notepadSettingsOpen && notepad is not null && window.Hwnd == notepad.WindowHandle && window.ProcessId == notepad.ProcessId)) &&
                !WindowHelper.IsWindowCloaked(new IntPtr(window.Hwnd)),
            timeoutMS: 100,
            pollIntervalMS: 25);
    }

    private NotepadDocument WaitForNotepadDocument(bool requireSettings = false)
    {
        var backInvoked = new HashSet<long>();
        var result = NotepadReadiness.WaitForStableWindow(
            () =>
            {
                var window = FindOwnedNotepadWindow();
                if (window is null)
                {
                    return null;
                }

                // Modern Notepad can replace its startup HWND or retain its Settings page.
                if (DismissNotepadPathError(window))
                {
                    return null;
                }

                var back = window.FindAll<Button>(By.Name("Back"), 0)
                    .SingleOrDefault(button => button.Name == "Back" && button.Displayed && button.IsEnabled);
                if (back is not null && !backInvoked.Contains(window.WindowHandle))
                {
                    back.Invoke(msPostAction: 0);
                    backInvoked.Add(window.WindowHandle);
                    return null;
                }

                var editors = window.FindAll<Element>(By.Name("Text Editor"), 0)
                    .Where(element => element.ControlType is "Edit" or "Document").ToArray();
                Assert.IsTrue(editors.Length <= 1, "More than one text editor was exposed by the test-owned Notepad document.");
                var editor = editors.SingleOrDefault();
                if (!window.WindowTitle.Contains(Path.GetFileNameWithoutExtension(notepadPath!), StringComparison.OrdinalIgnoreCase) ||
                    editor is not { Width: > 0, Height: > 0 } || !editor.IsEnabled || !editor.Displayed)
                {
                    return null;
                }

                var settings = window.FindAll<Button>(By.Name("Settings"), 0)
                    .SingleOrDefault(button => button.Name == "Settings" && button.Displayed && button.IsEnabled);
                return requireSettings && !NotepadReadiness.IsSettingsReady(editor.ClassName, settings is not null)
                    ? null
                    : new NotepadDocument(window, editor, settings);
            },
            document => document.Window.WindowHandle);
        Assert.IsTrue(result.Succeeded, $"The test-owned Notepad editor did not become ready on a stable window. {result.LastException?.Message}");
        var document = result.LastObservation!;
        notepad = document.Window;
        notepadSettingsOpen = false;
        return document;
    }

    private Session WaitForNotepadSettings()
    {
        var result = NotepadReadiness.WaitForStableWindow(
            () =>
            {
                var window = FindOwnedNotepadWindow();
                if (window is null || DismissNotepadPathError(window))
                {
                    return null;
                }

                return window.FindAll<Button>(By.Name("Back"), 0)
                    .Any(button => button.Name == "Back" && button.Displayed && button.IsEnabled)
                    ? window
                    : null;
            },
            window => window.WindowHandle);
        Assert.IsTrue(result.Succeeded, $"The test-owned Notepad Settings page did not become ready. {result.LastException?.Message}");
        var window = result.LastObservation!;
        notepad = window;
        return window;
    }

    private bool DismissNotepadPathError(Session window)
    {
        const string message = "The system cannot find the path specified.";
        var errors = window.FindAll<Element>(By.Name(message), 0).Where(element => element.Name == message && element.Displayed).ToArray();
        if (errors.Length == 0)
        {
            return false;
        }

        Assert.IsTrue(File.Exists(notepadPath), "The test-owned Notepad file is missing; do not dismiss a genuine fixture error.");
        var accept = window.FindAll<Button>(By.Name("OK"), 0)
            .Where(button => button.Name == "OK" && button.Displayed && button.IsEnabled).ToArray();
        Assert.HasCount(1, accept, "Expected the path-error acknowledgement in the test-owned Notepad window.");
        ui.Step("Dismissing a restored-session path error while the new test-owned document still exists");
        SaveDesktop("notepad-restored-path-error");
        accept[0].Invoke(msPostAction: 0);
        return true;
    }

    private Element FocusNotepad()
    {
        var document = WaitForNotepadDocument();
        Assert.IsNotNull(notepad);
        Assert.IsTrue(WindowControl.WaitForForeground(new IntPtr(notepad.WindowHandle), 10_000), $"Notepad did not own foreground: {WindowControl.GetForegroundWindowInfo()}.");
        document.Editor.Focus();
        var ready = WaitHelper.WaitForStable(
            () =>
            {
                var info = new GuiThreadInfo { Size = (uint)Marshal.SizeOf<GuiThreadInfo>() };
                if (!GetGUIThreadInfo(0, ref info) || info.ActiveWindow != new IntPtr(notepad.WindowHandle))
                {
                    return IntPtr.Zero;
                }

                var name = new StringBuilder(256);
                var length = GetClassNameW(info.FocusedWindow, name, name.Capacity);
                var className = length > 0 ? name.ToString() : string.Empty;
                return className == "Edit" || className.Contains("RichEdit", StringComparison.OrdinalIgnoreCase)
                    ? info.FocusedWindow
                    : IntPtr.Zero;
            },
            window => window != IntPtr.Zero,
            10_000,
            2);
        Assert.IsTrue(ready.Succeeded, "The test-owned Notepad editor did not acquire native keyboard focus.");
        notepadEditorWindow = ready.LastObservation;
        return document.Editor;
    }

    private void WaitForNotepadText(string expected, int timeoutMS)
    {
        var result = WaitHelper.WaitForStable(ReadNotepadText, text => text == expected, timeoutMS, pollIntervalMS: 50);
        Assert.IsTrue(result.Succeeded, $"Notepad text mismatch. Expected '{expected}', actual '{result.LastObservation}'.");
    }

    private string ReadNotepadText()
    {
        // Native Edit/RichEdit reads preserve empty text and keep timing independent of CLI startup.
        var buffer = new StringBuilder(8_192);
        Assert.AreNotEqual(
            IntPtr.Zero,
            SendMessageTimeoutW(notepadEditorWindow, 0x000D, new IntPtr(buffer.Capacity), buffer, 2, 2_000, out _),
            "Could not read the test-owned Notepad editor.");
        return buffer.ToString().TrimEnd('\r', '\n');
    }

    private (uint Start, uint End) ReadNotepadSelection()
    {
        Assert.AreNotEqual(
            IntPtr.Zero,
            SendSelectionMessageTimeout(notepadEditorWindow, 0x00B0, out var start, out var end, 2, 2_000, out _),
            "Could not read the test-owned Notepad selection.");
        return (start, end);
    }

    private void CloseNotepadDocument()
    {
        if (notepadPath is not null)
        {
            ui.Step("Saving and closing only the test-owned Notepad document");
            var editor = FocusNotepad();
            KeyboardHelper.SendChord(Key.Esc);
            var text = ReadNotepadText();
            try
            {
                KeyboardHelper.SendChord(Key.Ctrl, Key.S);
                var saved = WaitHelper.WaitForStable(
                    () =>
                    {
                        using var stream = new FileStream(notepadPath!, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                        using var reader = new StreamReader(stream);
                        return reader.ReadToEnd().TrimEnd('\r', '\n');
                    },
                    content => content == text,
                    10_000,
                    2,
                    shouldRetryException: exception => exception is IOException io && (io.HResult & 0xffff) is 32 or 33);
                Assert.IsTrue(saved.Succeeded, "Notepad did not finish saving the test-owned document before closing its tab.");
            }
            finally
            {
                if (notepadEditingOptions.Count > 0)
                {
                    var currentDocument = WaitForNotepadDocument(requireSettings: true);
                    Assert.IsNotNull(currentDocument.SettingsButton, "Notepad Settings must be available to restore editing options.");
                    notepadSettingsOpen = true;
                    currentDocument.SettingsButton.Invoke(msPostAction: 0);
                    var settingsWindow = WaitForNotepadSettings();
                    foreach (var option in notepadEditingOptions.Reverse())
                    {
                        var toggle = settingsWindow.Find<ToggleSwitch>(By.Name(option.Key));
                        if (toggle.IsEnabled && toggle.IsOn != option.Value)
                        {
                            toggle.Invoke(msPostAction: 0);
                            Assert.IsTrue(toggle.WaitForProperty("ToggleState", option.Value ? "On" : "Off", 10_000), $"Could not restore Notepad {option.Key}.");
                        }
                    }

                    editor = FocusNotepad();
                }
            }

            KeyboardHelper.SendChord(editor.ControlType == "Document" ? [Key.Ctrl, Key.W] : [Key.Alt, Key.F4]);
            Assert.IsTrue(
                WaitHelper.WaitForStable(
                    () => WindowsFinder.ListByApp("notepad").Any(window =>
                        window.Title.Contains(Path.GetFileNameWithoutExtension(notepadPath!), StringComparison.OrdinalIgnoreCase) &&
                        !WindowHelper.IsWindowCloaked(new IntPtr(window.Hwnd))),
                    visible => !visible,
                    10_000).Succeeded,
                "The test-owned Notepad tab did not close.");
            notepad = null;
            notepadPath = null;
        }
    }

    private void CleanupBreakAndDemo()
    {
        if (Directory.Exists(fixtureDirectory))
        {
            if (notepadPath is not null)
            {
                TestContext.WriteLine($"Preserving {fixtureDirectory}: closing the owned Notepad document was not confirmed.");
                return;
            }

            Directory.Delete(fixtureDirectory, recursive: true);
        }
    }

    private sealed record NotepadDocument(Session Window, Element Editor, Button? SettingsButton);

    private static void WriteAlarm(string path)
    {
        const int sampleRate = 16_000;
        const int samples = sampleRate * 3;
        using var writer = new BinaryWriter(File.Create(path), Encoding.ASCII);
        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + (samples * 2));
        writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(sampleRate);
        writer.Write(sampleRate * 2);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(samples * 2);
        for (var index = 0; index < samples; index++)
        {
            writer.Write((short)(Math.Sin(2 * Math.PI * 440 * index / sampleRate) * 4_000));
        }
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetLayeredWindowAttributes(IntPtr hwnd, out uint colorKey, out byte alpha, out uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassNameW(IntPtr window, StringBuilder className, int maximumCount);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetGUIThreadInfo(uint thread, ref GuiThreadInfo info);

    [StructLayout(LayoutKind.Sequential)]
    private struct GuiThreadInfo
    {
        internal uint Size;
        internal uint Flags;
        internal IntPtr ActiveWindow;
        internal IntPtr FocusedWindow;
        internal IntPtr CaptureWindow;
        internal IntPtr MenuOwnerWindow;
        internal IntPtr MoveSizeWindow;
        internal IntPtr CaretWindow;
        internal int CaretLeft;
        internal int CaretTop;
        internal int CaretRight;
        internal int CaretBottom;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SendMessageTimeoutW(IntPtr window, uint message, IntPtr parameter, StringBuilder text, uint flags, uint timeout, out UIntPtr result);

    [DllImport("user32.dll", EntryPoint = "SendMessageTimeoutW", SetLastError = true)]
    private static extern IntPtr SendSelectionMessageTimeout(IntPtr window, uint message, out uint start, out uint end, uint flags, uint timeout, out UIntPtr result);
}

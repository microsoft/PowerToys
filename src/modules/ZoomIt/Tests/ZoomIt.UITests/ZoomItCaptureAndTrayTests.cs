// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.Text.Json;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Devices.Enumeration;
using Windows.Graphics.Capture;
using Windows.Media.Editing;
using Windows.Storage;

namespace Microsoft.PowerToys.ZoomIt.UITests;

public sealed partial class ZoomItTests
{
    private const string CaptureRectangleClass = "ZoomitSelectRectangle";
    private static readonly string[] TrayCommands = ["Break Timer", "Draw", "Zoom", "Record"];
    private static readonly string[] TrayChevronNames = ["hidden icon", "Notification Chevron", "NotificationChevron"];
    private readonly string captureEvidenceId = Guid.NewGuid().ToString("N");

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void TrayIconLeftAndRightClickShowExactlyFourActions(bool rightClick)
    {
        OpenTrayMenu(rightClick);
        SaveDesktop(rightClick ? "tray-right-click" : "tray-left-click");
        ShellMenu.Dismiss();
    }

    [TestMethod]
    public void ShowTrayIconSettingRemovesAndRestoresShellIcon()
    {
        WaitForTrayIcon(true);
        ShellMenu.Dismiss();
        ui.SetToggle("ZoomItToggleShowTrayIcon", false, "ShowTrayIcon");
        WaitForTrayIcon(false);
        SaveDesktop("tray-icon-hidden");
        ShellMenu.Dismiss();
        ui.SetToggle("ZoomItToggleShowTrayIcon", true, "ShowTrayIcon");
        WaitForTrayIcon(true);
        SaveDesktop("tray-icon-restored");
        OpenTrayMenu(rightClick: true);
        ShellMenu.Dismiss();
    }

    [TestMethod]
    [DataRow("Break Timer")]
    [DataRow("Draw")]
    [DataRow("Zoom")]
    public void TrayActionActivatesCorrespondingMode(string caption)
    {
        desktop = new DesktopFixture();
        ClickTrayCommand(caption);
        var overlay = WindowsFinder.WaitForWindowByApp(
            ZoomItUi.ProcessName,
            window => window.ClassName == ZoomItUi.OverlayClass,
            15_000);
        Assert.IsNotNull(overlay, $"The real '{caption}' tray action did not open ZoomIt.");
        Assert.IsTrue(WindowControl.WaitForForeground(new IntPtr(overlay.WindowHandle), 10_000), "The tray action's ZoomIt window must own foreground.");
        if (caption == "Break Timer")
        {
            WaitForTimerPixels();
        }
        else if (caption == "Zoom")
        {
            AssertMagnification(2);
        }
        else
        {
            AssertMagnification(1);
            KeyboardHelper.SendKeys(Key.R);
            MouseHelper.Drag(desktop.Center.X - 150, desktop.Center.Y - 100, desktop.Center.X + 150, desktop.Center.Y - 100, steps: 30);
            var stroke = WaitHelper.WaitForStable(
                () =>
                {
                    using var image = DesktopFixture.Capture();
                    return DesktopFixture.ColorBounds(image, color => color.R > 220 && color.G < 40 && color.B < 40);
                },
                bounds => bounds.Width >= 280 && bounds.Height >= 3,
                10_000,
                2);
            Assert.IsTrue(stroke.Succeeded, $"The tray Draw action did not draw a red stroke: {stroke.LastObservation}.");
        }

        SaveDesktop("tray-" + caption.Replace(' ', '-'));
        ui.Exit();
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task HotkeyAndTrayRecordingSaveDecodableMp4(bool activateFromTray)
    {
        ui.Select("ZoomItRecordFormat", "GIF", "RecordingFormat", 0);
        ui.Select("ZoomItRecordFormat", "MP4", "RecordingFormat", 1);
        ui.SetSlider("ZoomItRecordScaling", 1, "RecordScalingMP4", 100);
        var shortcut = ui.Shortcut("ZoomItRecordShortcut");
        desktop = new DesktopFixture();
        desktop.StartAnimation();
        Assert.IsTrue(ClipboardHelper.Clear(), "Could not clear the clipboard before recording.");
        Assert.IsTrue(
            GraphicsCaptureSession.IsSupported(),
            "Recording capability unavailable: Windows.Graphics.Capture is not supported on this desktop. Recording was not validated.");

        if (activateFromTray)
        {
            ClickTrayCommand("Record");
        }
        else
        {
            ui.Step("Starting recording with the configured hotkey");
            KeyboardHelper.SendKeys(shortcut);
        }

        WaitForRecordingFrames();
        SaveDesktop(activateFromTray ? "tray-recording-active" : "hotkey-recording-active");
        ui.Step("Stopping recording with the same configured hotkey");
        KeyboardHelper.SendKeys(shortcut);
        var picker = WindowsFinder.WaitForWindowByApp(
            ZoomItUi.ProcessName,
            window => window.ClassName == "#32770" && window.Title.Contains("Save", StringComparison.OrdinalIgnoreCase),
            45_000);
        Assert.IsNotNull(
            picker,
            $"Stopping did not open the native video save picker. {RecordingDiagnostics()}");
        Assert.IsTrue(
            WaitHelper.WaitForStable(() => ZoomItUi.IsVisible(CaptureRectangleClass), visible => !visible, 10_000, 2).Succeeded,
            "The recording border did not disappear after stopping.");

        var path = CaptureEvidencePath($"recording-{Guid.NewGuid():N}.mp4");
        Assert.IsFalse(File.Exists(path), "The recording destination must be unique.");
        var filename = picker.Find<TextBox>(By.Name("File name:"));
        Assert.IsTrue(WindowControl.WaitForForeground(new IntPtr(picker.WindowHandle), 10_000), "The native save picker must own foreground.");
        filename.Focus();
        Assert.IsTrue(ClipboardHelper.SetText(path), "Could not prepare the recording filename.");
        KeyboardHelper.SendKeys(Key.Ctrl, Key.A);
        KeyboardHelper.SendKeys(Key.Ctrl, Key.V);
        KeyboardHelper.SendKeys(Key.Enter);
        var saved = WaitHelper.WaitForStable(
            () => File.Exists(path) ? new FileInfo(path).Length : 0,
            size => size > 1_024 && !WindowsFinder.ListByApp(ZoomItUi.ProcessName).Any(window => window.Hwnd == picker.WindowHandle),
            45_000,
            3,
            pollIntervalMS: 300,
            shouldRetryException: exception => exception is FileNotFoundException);
        Assert.IsTrue(saved.Succeeded, $"The native picker did not save a nonempty MP4 at '{path}'. Last size: {saved.LastObservation}.");
        TestContext.AddResultFile(path);
        await AssertRecordedVideoAsync(path);
    }

    [TestMethod]
    public void SnipShortcutCopiesSelectedFixturePixels()
    {
        var shortcut = ui.Shortcut("ZoomItSnipShortcut");
        desktop = new DesktopFixture();
        Assert.IsTrue(ClipboardHelper.Clear(), "Could not clear the clipboard before snipping.");
        var selection = new Rectangle(desktop.Center.X - 160, desktop.Center.Y - 120, 320, 240);
        ui.Step($"Snipping the known desktop rectangle {selection}");
        ui.Activate(shortcut, CaptureRectangleClass);
        var selector = WindowsFinder.WaitForWindowByApp(ZoomItUi.ProcessName, window => window.ClassName == CaptureRectangleClass, 10_000);
        Assert.IsNotNull(selector, "The snip selection surface did not appear.");
        Assert.IsTrue(WindowControl.WaitForForeground(new IntPtr(selector.WindowHandle), 10_000), "The snip selection surface must own foreground.");
        MouseHelper.Drag(selection.Left, selection.Top, selection.Right - 1, selection.Bottom - 1, steps: 30);
        var copied = WaitHelper.WaitForStable(
            () =>
            {
                using var image = desktop.ReadClipboardImage();
                return image?.Size ?? Size.Empty;
            },
            size => size == selection.Size,
            15_000,
            3,
            shouldRetryException: exception => exception is System.Runtime.InteropServices.ExternalException);
        Assert.IsTrue(copied.Succeeded, $"Snip clipboard bitmap has wrong dimensions: expected {selection.Size}, actual {copied.LastObservation}.");
        using var bitmap = desktop.ReadClipboardImage();
        Assert.IsNotNull(bitmap, "The clipboard bitmap disappeared before validation.");
        var marker = new Rectangle(120, 80, DesktopFixture.MarkerSize, DesktopFixture.MarkerSize);
        var mismatches = 0;
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                var expected = marker.Contains(x, y) ? DesktopFixture.MarkerColor : DesktopFixture.BackgroundColor;
                if (!DesktopFixture.IsColor(bitmap.GetPixel(x, y), expected))
                {
                    mismatches++;
                }
            }
        }

        var path = CaptureEvidencePath("snip-clipboard.png");
        bitmap.Save(path, ImageFormat.Png);
        TestContext.AddResultFile(path);
        Assert.AreEqual(0, mismatches, "The copied snip must contain the selected fixture pixels, not a blank, scaled, or wrong-region image.");
        Assert.IsTrue(
            WaitHelper.WaitForStable(
                () => ZoomItUi.IsVisible(CaptureRectangleClass) || ZoomItUi.IsVisible(ZoomItUi.OverlayClass),
                visible => !visible,
                10_000,
                2).Succeeded,
            "Snip did not dismiss both capture surfaces.");
        TestContext.WriteLine("Checklist 10: compared the clipboard bitmap directly, replacing Paint's manual visual oracle with exact dimensions and every source pixel.");
    }

    [TestMethod]
    public async Task MicrophoneDropdownMatchesWindowsAudioCaptureDevices()
    {
        ui.SetCheck("ZoomItRecordCaptureAudio", true, "CaptureAudio");
        var devices = await DeviceInformation.FindAllAsync(DeviceClass.AudioCapture).AsTask().WaitAsync(TimeSpan.FromSeconds(30));
        var expected = devices.Select(device => device.Name).Prepend("Default").Order(StringComparer.Ordinal).ToArray();
        var combo = ui.Control<ComboBox>("ZoomItRecordMicrophone", "ComboBox");
        combo.ScrollIntoView();
        combo = ui.Control<ComboBox>("ZoomItRecordMicrophone", "ComboBox");
        combo.Invoke(msPostAction: 0);
        var settings = Session.FromProcess("PowerToys.Settings");
        var listed = WaitHelper.WaitForStable(
            () => ZoomItUi.Nodes(settings.Inspect(depth: 24, hideOffscreen: true))
                .Where(node => ZoomItUi.Property(node, "type") == "ListItem" && ZoomItUi.Property(node, "className").EndsWith("ComboBoxItem", StringComparison.Ordinal))
                .DistinctBy(node => ZoomItUi.Property(node, "selector"))
                .Select(node => ZoomItUi.Property(node, "name"))
                .Order(StringComparer.Ordinal)
                .ToArray(),
            actual => actual is not null && actual.SequenceEqual(expected, StringComparer.Ordinal),
            20_000,
            2,
            pollIntervalMS: 250);
        var path = CaptureEvidencePath("microphone-inventory.json");
        File.WriteAllText(path, JsonSerializer.Serialize(new
        {
            Expected = expected,
            Actual = listed.LastObservation,
            Devices = devices.Select(device => new { device.Id, device.Name }),
        }));
        TestContext.AddResultFile(path);
        SaveDesktop("microphone-dropdown");
        CollectionAssert.AreEqual(expected, listed.LastObservation ?? [], "The actual dropdown must list Default plus every Windows AudioCapture device, including duplicate display names.");
        Assert.IsTrue(listed.Succeeded, "The microphone list did not remain stable.");
        if (devices.Count == 0)
        {
            TestContext.WriteLine("Checklist 19: Windows reports no microphones; verified the opened dropdown contains only Default. Physical microphone capture is not claimed.");
        }

        KeyboardHelper.SendKeys(Key.Esc);
    }

    private Element? WaitForTrayIcon(bool expected)
    {
        ui.Step($"Waiting for the real Shell ZoomIt tray icon to be {(expected ? "present" : "absent")}");
        ShellMenu.Dismiss();
        KeyboardHelper.SendKeys(Key.LWin, Key.B);
        var screen = System.Windows.Forms.SystemInformation.PrimaryMonitorSize;
        MouseHelper.MoveTo(screen.Width / 2, screen.Height / 2);
        var explorer = Session.FromProcess("explorer");
        WindowsFinder.WindowInfo? OverflowWindow() => WindowsFinder.ListByApp("explorer").FirstOrDefault(window =>
            window.ClassName is "NotifyIconOverflowWindow" or "TopLevelWindowForOverflowXamlIsland" &&
            !WindowHelper.IsWindowCloaked(new IntPtr(window.Hwnd)));
        Element? FindChevron() => TrayChevronNames
            .SelectMany(name => explorer.FindAll<Element>(By.Name(name), 0))
            .FirstOrDefault(item => item.ControlType == "Button" && item.Width > 0 && item.Height > 0 && item.Displayed);
        var hasOverflow = FindChevron() is not null;
        var opening = false;

        var result = WaitHelper.WaitForStable(
            () =>
            {
                if (OverflowWindow() is not null)
                {
                    opening = false;
                }

                return FindTrayIcons(explorer);
            },
            icons => icons is not null && icons.Length == (expected ? 1 : 0) &&
                (!hasOverflow || OverflowWindow() is not null) &&
                (!expected || WindowsFinder.ListByApp("explorer").Any(window =>
                    window.ClassName is "Shell_TrayWnd" or "Shell_SecondaryTrayWnd" or "NotifyIconOverflowWindow" or "TopLevelWindowForOverflowXamlIsland" &&
                    WindowControl.IsPointOwnedByWindow(new IntPtr(window.Hwnd), icons[0].X + (icons[0].Width / 2), icons[0].Y + (icons[0].Height / 2)))),
            30_000,
            3,
            pollIntervalMS: 250,
            recover: _ =>
            {
                var overflow = OverflowWindow();
                if (overflow is not null)
                {
                    // An open flyout can be behind the source window; do not toggle it closed.
                    WindowControl.TryBringToForeground(new IntPtr(overflow.Hwnd));
                }
                else if (!opening && FindChevron() is { } chevron)
                {
                    var x = chevron.X + (chevron.Width / 2);
                    var y = chevron.Y + (chevron.Height / 2);
                    if (WindowsFinder.ListByApp("explorer").Any(window =>
                        window.ClassName is "Shell_TrayWnd" or "Shell_SecondaryTrayWnd" &&
                        WindowControl.IsPointOwnedByWindow(new IntPtr(window.Hwnd), x, y)))
                    {
                        ui.Step($"Opening the notification overflow with '{chevron.Name}'.");
                        MouseHelper.LeftClickAt(x, y);
                        opening = true;
                    }
                }
            },
            shouldRetryException: ShellMenu.IsTransientElementException);
        if (!result.Succeeded)
        {
            ui.Step($"Shell notification tree: {explorer.Inspect(depth: 12)}");
        }

        Assert.IsTrue(result.Succeeded, $"Expected {(expected ? "one" : "no")} ZoomIt notification icon in the taskbar or opened overflow, found {result.LastObservation?.Length}.");
        return result.LastObservation!.SingleOrDefault();
    }

    private static Element[] FindTrayIcons(Session explorer) =>
        explorer.FindAll<Element>(By.Name("ZoomIt"), 0)
            .Where(item => item.Name.Equals("ZoomIt", StringComparison.OrdinalIgnoreCase) &&
                item.ControlType == "Button" && item.Width > 0 && item.Height > 0 && item.Displayed)
            .DistinctBy(item => item.Selector)
            .ToArray();

    private Session OpenTrayMenu(bool rightClick)
    {
        Session? menu = null;
        for (var attempt = 1; attempt <= 3 && menu is null; attempt++)
        {
            var icon = WaitForTrayIcon(true);
            Assert.IsNotNull(icon);
            MouseHelper.MoveTo(icon.X + (icon.Width / 2), icon.Y + (icon.Height / 2));
            if (rightClick)
            {
                MouseHelper.RightClick();
            }
            else
            {
                MouseHelper.LeftClick();
            }

            menu = ShellMenu.WaitForWindow(ShellMenu.ClassicWindowClassName, 5_000, ZoomItUi.ProcessName);
        }

        Assert.IsNotNull(menu, "Clicking the Shell ZoomIt icon did not open its real popup menu.");
        var entries = WaitHelper.WaitForStable(
            () => ShellMenu.TryReadClassicItemCaptions(new IntPtr(menu.WindowHandle)),
            captions => captions is not null && captions.SequenceEqual(TrayCommands, StringComparer.Ordinal),
            10_000,
            2);
        Assert.IsTrue(entries.Succeeded, $"Expected exactly [{string.Join(", ", TrayCommands)}], including order and no extra entries or separators; actual [{string.Join(", ", entries.LastObservation ?? [])}].");
        return menu;
    }

    private void ClickTrayCommand(string caption)
    {
        ui.Step($"Clicking the real ZoomIt tray command '{caption}'");
        var menu = OpenTrayMenu(rightClick: true);
        var command = ShellMenu.FindVisibleMenuItem(menu, caption, 10_000, StringComparison.Ordinal);
        Assert.IsNotNull(command, $"The '{caption}' tray entry has no visible actionable menu item.");
        MouseHelper.LeftClickAt(command.X + (command.Width / 2), command.Y + (command.Height / 2));
        if (desktop is not null)
        {
            // ZoomIt delays tray activation until its menu closes; position the zoom focal point now.
            MouseHelper.MoveTo(desktop.Center.X, desktop.Center.Y);
        }

        Assert.IsTrue(
            WaitHelper.WaitForStable(
                () => WindowsFinder.ListByApp(ZoomItUi.ProcessName).Any(window => window.Hwnd == menu.WindowHandle),
                visible => !visible,
                10_000,
                2).Succeeded,
            $"Clicking '{caption}' did not dismiss the tray menu.");
    }

    private void WaitForRecordingFrames()
    {
        ui.Step("Waiting for the first-frame recording border transition, without attaching UIA to capture");
        var recording = WaitHelper.WaitForStable(
            () =>
            {
                var border = WindowsFinder.ListByApp(ZoomItUi.ProcessName).SingleOrDefault(window => window.ClassName == CaptureRectangleClass);
                return border is not null && ZoomItCaptureHelpers.HasRecordingFrameBorder(new IntPtr(border.Hwnd));
            },
            active => active,
            45_000,
            requiredConsecutiveMatches: 5,
            pollIntervalMS: 500);
        Assert.IsTrue(
            recording.Succeeded,
            $"No sustained first-frame recording border transition. The initial border alone does not prove capture. {RecordingDiagnostics()}");
    }

    private static string RecordingDiagnostics()
    {
        var windows = WindowsFinder.ListByApp(ZoomItUi.ProcessName);
        var dialogs = windows.Where(window => window.ClassName == "#32770")
            .Select(window => ZoomItCaptureHelpers.ReadDialogText(new IntPtr(window.Hwnd)));
        return $"Windows: {string.Join("; ", windows)}. Dialog text: {string.Join("; ", dialogs)}. " +
            "A GPU-backed WDDM display is required; a no-video-frames capability failure is not a passing recording test.";
    }

    private async Task AssertRecordedVideoAsync(string path)
    {
        Assert.IsNotNull(desktop);
        var file = await StorageFile.GetFileFromPathAsync(path).AsTask().WaitAsync(TimeSpan.FromSeconds(30));
        var clip = await MediaClip.CreateFromFileAsync(file).AsTask().WaitAsync(TimeSpan.FromSeconds(45));
        var encoding = clip.GetVideoEncodingProperties();
        var screen = System.Windows.Forms.SystemInformation.PrimaryMonitorSize;
        Assert.AreEqual((uint)screen.Width, encoding.Width, "Full-screen recording has the wrong encoded width at 100% scaling.");
        Assert.AreEqual((uint)screen.Height, encoding.Height, "Full-screen recording has the wrong encoded height at 100% scaling.");
        Assert.IsTrue(encoding.FrameRate.Numerator > 0 && encoding.FrameRate.Denominator > 0, "The saved video has no valid frame rate.");
        Assert.IsTrue(clip.OriginalDuration >= TimeSpan.FromSeconds(1), $"The saved recording is too short: {clip.OriginalDuration}.");
        var composition = new MediaComposition();
        composition.Clips.Add(clip);
        foreach (var fraction in new[] { 0.25, 0.75 })
        {
            var position = TimeSpan.FromTicks((long)(clip.OriginalDuration.Ticks * fraction));
            using var frame = await ZoomItCaptureHelpers.DecodeVideoFrameAsync(composition, position, screen);
            var framePath = CaptureEvidencePath($"recording-frame-{fraction.ToString("F2", CultureInfo.InvariantCulture)}.png");
            frame.Save(framePath, ImageFormat.Png);
            TestContext.AddResultFile(framePath);
            var marker = DesktopFixture.ColorBounds(frame, color => DesktopFixture.IsColor(color, DesktopFixture.MarkerColor, 25));
            Assert.IsTrue(
                Math.Abs(marker.Width - DesktopFixture.MarkerSize) <= 6 &&
                Math.Abs(marker.Height - DesktopFixture.MarkerSize) <= 6 &&
                Math.Abs(marker.Left - (desktop.Center.X - 40)) <= 4 &&
                Math.Abs(marker.Top - (desktop.Center.Y - 40)) <= 4,
                $"Decoded frame at {position} does not contain the source marker: {marker}.");
            Assert.IsTrue(
                DesktopFixture.IsColor(frame.GetPixel(desktop.Center.X - 120, desktop.Center.Y - 100), DesktopFixture.BackgroundColor, 25),
                $"Decoded frame at {position} does not contain the fixture background.");
        }

        TestContext.WriteLine($"Decoded MP4: {new FileInfo(path).Length} bytes, {encoding.Width}x{encoding.Height}, " +
            $"{encoding.FrameRate.Numerator}/{encoding.FrameRate.Denominator} fps, duration {clip.OriginalDuration}. Two decoded frames match the desktop fixture.");
    }

    private string CaptureEvidencePath(string filename)
    {
        var directory = Path.Combine(Path.GetDirectoryName(TestContext.TestRunDirectory!)!, "ZoomItEvidence", TestContext.TestName!, "Capture-" + captureEvidenceId);
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, filename);
    }

    private static void CleanupCaptureAndTray() => ShellMenu.Dismiss();
}

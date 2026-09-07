// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Drawing;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static MouseUtils.UITests.MouseUtilsTestHelper;

namespace MouseUtils.UITests;

[TestClass]
public class MouseJumpTests : UITestBase
{
    private const string ModuleName = "MouseJump";
    private const string ProcessName = "PowerToys.MouseJump.WinUI3";
    private const string WindowTitle = "MouseJump.WinUI3";
    private const string ToggleId = "MouseUtils_MouseJumpToggleId";
    private const int DefaultBorderThickness = 6;
    private static readonly IDisposable ModuleSettings = SettingsConfigHelper.PreserveModuleSettings(ModuleName);
    private NotepadFixture? notepadFixture;

    static MouseJumpTests()
    {
    }

    public MouseJumpTests()
        : base(PowerToysModule.PowerToysSettings, enableModules: new[] { ModuleName })
    {
    }

    protected override IReadOnlyList<string> StaleProcessNames { get; } = new[]
    {
        "PowerToys",
        "PowerToys.Settings",
        "PowerToys.FancyZonesEditor",
        ProcessName,
        "PowerToys.MouseJumpUI",
    };

    [ClassCleanup]
    public static void RestoreModuleSettings() => ModuleSettings.Dispose();

    protected override void PrepareTestState()
    {
        var configuration = TestContext.TestName switch
        {
            nameof(ChangedShortcutActivatesPreview) => new MouseJumpConfiguration(ShortcutCode: (int)Key.Z),
            nameof(ThumbnailSizeSetsPreviewBounds) => new MouseJumpConfiguration(Width: 640, Height: 480, PreviewType: "Compact"),
            nameof(CustomPreviewStyleRendersConfiguredColors) => new MouseJumpConfiguration(
                Width: 640,
                Height: 480,
                PreviewType: "Custom",
                BackgroundColor1: "#FF00FF",
                BackgroundColor2: "#FF00FF",
                BorderThickness: 12,
                BorderColor: "#00FF00",
                BorderPadding: 12,
                BezelThickness: 12,
                BezelColor: "#FFFF00",
                ScreenMargin: 10,
                ScreenColor1: "#00FFFF",
                ScreenColor2: "#00FFFF"),
            _ => new MouseJumpConfiguration(),
        };

        MouseUtilsTestHelper.ReplaceModuleSettings(ModuleName, CreateSettings(configuration));
    }

    [TestCleanup]
    public async Task CleanupWindows()
    {
        await CaptureFailureArtifactsBeforeCleanupAsync();
        KeyboardHelper.SendKeys(Key.Esc);
        notepadFixture?.Dispose();
        notepadFixture = null;
        WindowControl.TryKillProcessTreeByNameAndWait(ProcessName, 10_000);
    }

    [TestMethod]
    [TestCategory("MouseUtils")]
    [TestCategory("Mouse Utils #39")]
    public void SettingsPageLoadsAndProcessRuns()
    {
        MouseUtilsTestHelper.NavigateToMouseUtilities(this);
        var toggle = MouseUtilsTestHelper.SetModuleEnabled(this, ToggleId, true);
        Assert.IsTrue(toggle.IsOn, "Mouse Jump toggle should be on.");
        Assert.IsTrue(WaitForProcess(expected: true), "Mouse Jump WinUI3 process did not start.");
        Assert.IsTrue(
            NamedEventHelper.WaitUntilAvailable(NamedEventHelper.MouseJumpShowPreview),
            "Mouse Jump did not create its show-preview event.");
        var previewWindow = WaitForPreviewWindow();

        using var shortcutWatcher = new WindowShowWatcher(previewWindow.ClassName, previewWindow.Hwnd.ToInt64());
        KeyboardHelper.SendKeys(Key.LWin, Key.Shift, Key.D);
        Assert.IsTrue(shortcutWatcher.Wait(10_000), "The default Win+Shift+D shortcut did not show Mouse Jump.");
    }

    [TestMethod]
    [TestCategory("MouseUtils")]
    [TestCategory("Mouse Utils #39")]
    [TestCategory("Mouse Utils #41")]
    [TestCategory("Mouse Utils #45")]
    public void PreviewClickMovesCursorAndDisableStopsActivation()
    {
        var preview = ShowPreview();
        var previewClassName = WaitForPreviewWindow().ClassName;
        var bounds = WindowHelper.GetWindowBounds(new IntPtr(preview.WindowHandle));
        var clickX = bounds.Left + ((bounds.Right - bounds.Left) / 2);
        var clickY = bounds.Top + ((bounds.Bottom - bounds.Top) / 2);
        using (var watcher = new WindowShowWatcher(previewClassName, preview.WindowHandle))
        {
            var previewHwnd = new IntPtr(preview.WindowHandle);
            WindowControl.WaitForForeground(previewHwnd, 2_000);
            Assert.IsTrue(
                WindowControl.IsPointOwnedByWindow(previewHwnd, clickX, clickY),
                $"Mouse Jump did not own its preview midpoint ({clickX},{clickY}) before the click.");
            MouseHelper.LeftClickAt(clickX, clickY);
            Assert.IsTrue(watcher.WaitForHidden(5_000), "Clicking the Mouse Jump preview did not hide it.");
        }

        var primary = MonitorInfo.GetPrimary();
        Assert.IsNotNull(primary, "No primary monitor was reported.");
        var expectedX = primary.Left + (primary.Width / 2);
        var expectedY = primary.Top + (primary.Height / 2);
        var cursor = MouseHelper.GetMousePosition();
        Assert.IsTrue(
            Distance(cursor.X, cursor.Y, expectedX, expectedY) <= 20,
            $"Preview-center click mapped to ({cursor.X},{cursor.Y}), expected primary midpoint ({expectedX},{expectedY}).");

        MouseUtilsTestHelper.NavigateToMouseUtilities(this);
        MouseUtilsTestHelper.SetModuleEnabled(this, ToggleId, false);
        Assert.IsTrue(WaitForProcess(expected: false), "Mouse Jump process did not exit after disabling the module.");
        using var disabledActivation = new WindowShowWatcher(previewClassName);
        KeyboardHelper.SendKeys(Key.LWin, Key.Shift, Key.D);
        Assert.IsFalse(disabledActivation.Wait(2_000), "Mouse Jump preview appeared while the module was disabled.");
        Assert.IsFalse(WaitForProcess(expected: true, timeoutMs: 1_000), "Mouse Jump process restarted while the module was disabled.");
    }

    [TestMethod]
    [TestCategory("MouseUtils")]
    [TestCategory("Mouse Utils #40")]
    public void ChangedShortcutActivatesPreview()
    {
        var previewWindow = WaitForPreviewReady();
        using (var defaultShortcut = new WindowShowWatcher(previewWindow.ClassName, previewWindow.Hwnd.ToInt64()))
        {
            KeyboardHelper.SendKeys(Key.LWin, Key.Shift, Key.D);
            Assert.IsFalse(defaultShortcut.Wait(1_500), "The old Mouse Jump shortcut still showed the preview.");
        }

        using var changedShortcut = new WindowShowWatcher(previewWindow.ClassName, previewWindow.Hwnd.ToInt64());
        KeyboardHelper.SendKeys(Key.LWin, Key.Shift, Key.Z);
        Assert.IsTrue(changedShortcut.Wait(10_000), "Changed Win+Shift+Z shortcut did not show Mouse Jump.");
    }

    [TestMethod]
    [TestCategory("MouseUtils")]
    public void FocusLossDismissesPreview()
    {
        var preview = ShowPreview();
        Assert.IsTrue(
            WindowControl.WaitForForeground(new IntPtr(preview.WindowHandle), 5_000, 2),
            $"Mouse Jump was not foreground before the focus-loss transition. Current foreground: {WindowControl.GetForegroundWindowInfo()}.");
        using var focusWatcher = new WindowShowWatcher(WaitForPreviewWindow().ClassName, preview.WindowHandle);
        notepadFixture = NotepadFixture.Start();
        var notepadWindow = notepadFixture.Window;
        Assert.IsTrue(
            WindowControl.WaitForForeground(new IntPtr(notepadWindow.WindowHandle), 5_000, 2),
            "Notepad could not gain foreground to exercise Mouse Jump focus-loss dismissal.");
        Assert.IsTrue(focusWatcher.WaitForHidden(5_000), "Mouse Jump did not dismiss after losing focus.");
    }

    [TestMethod]
    [TestCategory("MouseUtils")]
    public void ThumbnailSizeSetsPreviewBounds()
    {
        var preview = ShowPreview();
        var bounds = WindowHelper.GetWindowBounds(new IntPtr(preview.WindowHandle));
        var width = bounds.Right - bounds.Left;
        var height = bounds.Bottom - bounds.Top;
        Assert.IsTrue(width is >= 620 and <= 650, $"Configured 640px preview width rendered as {width}px.");
        Assert.IsTrue(height <= 480, $"Configured 480px maximum preview height rendered as {height}px.");

        var primary = MonitorInfo.GetPrimary();
        Assert.IsNotNull(primary, "No primary monitor was reported.");
        var borderContribution = 2d * DefaultBorderThickness;
        var renderedContentAspectRatio = (width - borderContribution) / (height - borderContribution);
        var displayAspectRatio = primary.Width / (double)primary.Height;
        Assert.IsTrue(
            Math.Abs(renderedContentAspectRatio - displayAspectRatio) <= 0.02,
            $"Preview content aspect ratio {renderedContentAspectRatio:F3} did not preserve display ratio {displayAspectRatio:F3}.");
    }

    [TestMethod]
    [TestCategory("MouseUtils")]
    public void CustomPreviewStyleRendersConfiguredColors()
    {
        var preview = ShowPreview();
        var path = Path.Combine(TestContext.TestResultsDirectory ?? Path.GetTempPath(), $"mouse-jump-custom-{Guid.NewGuid():N}.png");
        WindowHelper.CaptureVisibleWindow(new IntPtr(preview.WindowHandle), path);
        try
        {
            using var image = new Bitmap(path);
            var colors = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["#FF00FF"] = 0,
                ["#00FF00"] = 0,
                ["#FFFF00"] = 0,
            };

            for (var y = 0; y < image.Height; y += 2)
            {
                for (var x = 0; x < image.Width; x += 2)
                {
                    var pixel = image.GetPixel(x, y);
                    foreach (var key in colors.Keys.ToArray())
                    {
                        var expected = ColorTranslator.FromHtml(key);
                        if (IsNear(pixel, expected, 4))
                        {
                            colors[key]++;
                        }
                    }
                }
            }

            Assert.IsTrue(colors["#FF00FF"] > 100, $"Custom canvas background was not rendered; magenta sample count={colors["#FF00FF"]}.");
            Assert.IsTrue(colors["#00FF00"] > 100, $"Custom canvas border was not rendered; green sample count={colors["#00FF00"]}.");
            Assert.IsTrue(colors["#FFFF00"] > 100, $"Custom screen bezel was not rendered; yellow sample count={colors["#FFFF00"]}.");
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string CreateSettings(MouseJumpConfiguration configuration) => $$"""
        {
          "name": "MouseJump",
          "version": "1.1",
          "properties": {
            "activation_shortcut": { "win": true, "ctrl": false, "alt": false, "shift": true, "code": {{configuration.ShortcutCode}}, "key": "" },
            "thumbnail_size": { "width": {{configuration.Width}}, "height": {{configuration.Height}} },
            "preview_type": "{{configuration.PreviewType}}",
            "background_color_1": "{{configuration.BackgroundColor1}}",
            "background_color_2": "{{configuration.BackgroundColor2}}",
            "border_thickness": {{configuration.BorderThickness}},
            "border_color": "{{configuration.BorderColor}}",
            "border_3d_depth": 0,
            "border_padding": {{configuration.BorderPadding}},
            "bezel_thickness": {{configuration.BezelThickness}},
            "bezel_color": "{{configuration.BezelColor}}",
            "bezel_3d_depth": 0,
            "screen_margin": {{configuration.ScreenMargin}},
            "screen_color_1": "{{configuration.ScreenColor1}}",
            "screen_color_2": "{{configuration.ScreenColor2}}"
          }
        }
        """;

    private static Session ShowPreview()
    {
        _ = WaitForPreviewReady();

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            Assert.IsTrue(
                NamedEventHelper.WaitAndSignal(NamedEventHelper.MouseJumpShowPreview, 10_000),
                "Mouse Jump show-preview event was unavailable.");
            var preview = WindowsFinder.WaitForWindowByApp(
                ProcessName,
                window => window.Title.Equals(WindowTitle, StringComparison.OrdinalIgnoreCase),
                timeoutMS: 5_000);
            if (preview is not null)
            {
                WindowControl.WaitForForeground(new IntPtr(preview.WindowHandle), 2_000);
                return preview;
            }
        }

        Assert.Fail("Mouse Jump preview did not become visible after three show-event attempts.");
        return null!;
    }

    private static WindowControl.ProcessWindow GetPreviewWindow()
    {
        var processes = Process.GetProcessesByName(ProcessName);
        try
        {
            var processIds = processes.Select(process => process.Id).ToArray();
            return WindowControl.EnumerateProcessWindows(processIds)
                .FirstOrDefault(window => window.Title.Equals(WindowTitle, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
    }

    private static WindowControl.ProcessWindow WaitForPreviewReady()
    {
        Assert.IsTrue(
            NamedEventHelper.WaitUntilAvailable(NamedEventHelper.MouseJumpShowPreview),
            "Mouse Jump module did not create its show-preview event.");
        Assert.IsTrue(WaitForProcess(expected: true), "Mouse Jump WinUI3 process did not start.");
        return WaitForPreviewWindow();
    }

    private static WindowControl.ProcessWindow WaitForPreviewWindow()
    {
        var result = WaitHelper.WaitForStable(
            GetPreviewWindow,
            window => window.Hwnd != IntPtr.Zero && !string.IsNullOrWhiteSpace(window.ClassName),
            15_000,
            requiredConsecutiveMatches: 2,
            pollIntervalMS: 100);
        Assert.IsTrue(result.Succeeded, "Mouse Jump did not create a preview window with a usable HWND and class name.");
        return result.LastObservation;
    }

    private static bool WaitForProcess(bool expected, int timeoutMs = 15_000)
    {
        return WaitHelper.WaitForStable(
            () =>
            {
                var processes = Process.GetProcessesByName(ProcessName);
                try
                {
                    return processes.Length > 0;
                }
                finally
                {
                    foreach (var process in processes)
                    {
                        process.Dispose();
                    }
                }
            },
            running => running == expected,
            timeoutMs,
            requiredConsecutiveMatches: 2,
            pollIntervalMS: 100).Succeeded;
    }

    private sealed record MouseJumpConfiguration(
        int ShortcutCode = (int)Key.D,
        int Width = 800,
        int Height = 600,
        string PreviewType = "Bezelled",
        string BackgroundColor1 = "#0D57D2",
        string BackgroundColor2 = "#0344C0",
        int BorderThickness = DefaultBorderThickness,
        string BorderColor = "#0078D4",
        int BorderPadding = 4,
        int BezelThickness = 12,
        string BezelColor = "#222222",
        int ScreenMargin = 4,
        string ScreenColor1 = "#191970",
        string ScreenColor2 = "#191970");
}

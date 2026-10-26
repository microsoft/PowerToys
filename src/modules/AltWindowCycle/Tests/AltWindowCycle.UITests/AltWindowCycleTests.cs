// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;
using System.Drawing.Imaging;
using System.Text.Json.Nodes;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using AutomationButton = Microsoft.PowerToys.UITest.Next.Button;

namespace Microsoft.AltWindowCycle.UITests;

[TestClass]
[DoNotParallelize]
[TestCategory("AltWindowCycle")]
public sealed class AltWindowCycleTests : UITestBase
{
    private const string ModuleName = "AltWindowCycle";
    private const string OverlayClass = "AltWindowCycleOverlay";
    private const Key Backtick = (Key)0xC0;
    private const string DefaultSettings = """
        {
          "name": "AltWindowCycle",
          "version": "1.0",
          "properties": {
            "next_window_shortcut": { "win": false, "ctrl": false, "alt": true, "shift": false, "code": 192 },
            "previous_window_shortcut": { "win": false, "ctrl": false, "alt": true, "shift": true, "code": 192 }
          }
        }
        """;

    private readonly List<Key> heldModifiers = [];
    private IDisposable? settingsSnapshot;
    private WindowFixture? fixture;

    public AltWindowCycleTests()
        : base(PowerToysModule.PowerToysSettings, enableModules: [ModuleName])
    {
    }

    protected override IReadOnlyList<string> StaleProcessNames => [.. base.StaleProcessNames, "AltWindowCycle.TestApp"];

    private WindowFixture Fixture => fixture ?? throw new InvalidOperationException("Window fixtures were not initialized.");

    [ClassCleanup(ClassCleanupBehavior.EndOfClass)]
    public static void RemoveCopiedApplication() => WindowFixture.RemoveApplicationCopy();

    protected override void PrepareTestState()
    {
        Assert.IsFalse(ElevationHelper.IsCurrentProcessElevated(), "Run this suite non-elevated to cover the normal-user scenario.");
        settingsSnapshot = SettingsConfigHelper.PreserveModuleSettings(ModuleName);
        try
        {
            var directory = Path.Combine(SettingsConfigHelper.PowerToysSettingsRoot, ModuleName);
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "settings.json"), DefaultSettings);
        }
        catch
        {
            settingsSnapshot.Dispose();
            settingsSnapshot = null;
            throw;
        }
    }

    [TestInitialize]
    public void PrepareWindows()
    {
        Step("Navigating to Window Hopper Settings.");
        if (!Session.Has(By.AccessibilityId("AltWindowCycleNavItem"), 500))
        {
            Session.Find<NavigationViewItem>(By.AccessibilityId("WindowingAndLayoutsNavItem"), 10_000).Click(msPostAction: 0);
        }

        Session.Find<NavigationViewItem>(By.AccessibilityId("AltWindowCycleNavItem"), 10_000).Click(msPostAction: 0);
        Assert.IsTrue(ModuleToggle().IsOn, "Window Hopper must start enabled.");
        Assert.IsTrue(
            WaitHelper.WaitForStable(() => WindowControl.AnyWindowOfClassExists(OverlayClass), exists => exists, 15_000).Succeeded,
            "The enabled in-process module did not create its overlay HWND.");
        var shortcuts = ShortcutButtons();
        Assert.IsFalse(string.IsNullOrWhiteSpace(shortcuts[0].HelpText), "The next-window shortcut has no accessible text.");
        Assert.IsFalse(string.IsNullOrWhiteSpace(shortcuts[1].HelpText), "The previous-window shortcut has no accessible text.");
        fixture = new WindowFixture(TestContext);
    }

    [TestMethod]
    [DataRow(false, 1)]
    [DataRow(false, 2)]
    [DataRow(false, 3)]
    [DataRow(false, 4)]
    [DataRow(true, 1)]
    [DataRow(true, 2)]
    [DataRow(true, 3)]
    [DataRow(true, 4)]
    public void HeldShortcutCyclesOnlyFocusedApplicationAndWraps(bool reverse, int presses)
    {
        using var watcher = new WindowShowWatcher(OverlayClass);
        StartCycle(reverse ? [Key.Alt, Key.Shift, Backtick] : [Key.Alt, Backtick]);
        Assert.IsTrue(watcher.Wait(5_000), "The event observer did not detect the positively confirmed overlay.");
        AssertPreviewMembership();

        for (var press = 1; press < presses; press++)
        {
            Step($"Cycling {(reverse ? "backward" : "forward")} again while Alt remains held.");
            Tap(Backtick);
        }

        AssertAnchorForeground();
        Assert.IsTrue(WindowControl.IsAnyWindowOfClassVisible(OverlayClass), "The switcher closed before Alt was released.");
        var expected = reverse ? (3 - (presses % 3)) % 3 : presses % 3;
        CommitCycle(Fixture.Windows[expected]);
    }

    [TestMethod]
    public void ShiftChangesDirectionWithoutCommittingUntilAltIsReleased()
    {
        StartCycle([Key.Alt, Backtick]);
        Step("Cycling forward again before reversing to a non-anchor selection.");
        Tap(Backtick);
        Hold(Key.Shift);
        Step("Cycling backward within the open forward cycle.");
        Tap(Backtick);
        var overlay = WindowControl.EnumerateAllWindows().Single(window => window.ClassName == OverlayClass && window.IsVisible);
        using var watcher = new WindowShowWatcher(OverlayClass, overlay.Hwnd.ToInt64());
        Step("Releasing only Shift; Alt must keep the cycle open.");
        Release(Key.Shift);
        Assert.IsFalse(watcher.WaitForHidden(1_000), $"Releasing Shift hid the Alt-held switcher: {string.Join(", ", watcher.Events)}.");
        Assert.IsTrue(WindowControl.IsAnyWindowOfClassVisible(OverlayClass), "Releasing Shift prematurely committed the Alt-held cycle.");
        AssertAnchorForeground();
        Tap(Backtick);
        CommitCycle(Fixture.Windows[2]);
        Assert.IsTrue(watcher.WaitForHidden(5_000), "The event observer did not detect the positively confirmed Alt-release commit.");
    }

    [TestMethod]
    public void EscapeCancelsWithoutActivatingSelectionAndNextCycleStillWorks()
    {
        StartCycle([Key.Alt, Backtick]);
        Step("Cancelling the visible switcher with Escape while Alt is held.");
        KeyboardHelper.SendKey(Key.Esc);
        WaitForOverlayHidden();
        AssertAnchorForeground();
        ReleaseModifiers();
        AssertAnchorForeground();

        StartCycle([Key.Alt, Backtick]);
        CommitCycle(Fixture.Windows[1]);
    }

    [TestMethod]
    public void SingleWindowApplicationDoesNotOpenSwitcherOrSelectAnotherApplication()
    {
        // Positive control: prove both the module and the event observer work before the negative case.
        using (var positive = new WindowShowWatcher(OverlayClass))
        {
            StartCycle([Key.Alt, Backtick]);
            Assert.IsTrue(positive.Wait(5_000), "The positive-control switcher show event was not observed.");
            KeyboardHelper.SendKey(Key.Esc);
            WaitForOverlayHidden();
            ReleaseModifiers();
        }

        Fixture.Focus(Fixture.OtherWindow);
        AssertShortcutInactive([Key.Alt, Backtick], Fixture.OtherWindow);
        AssertShortcutInactive([Key.Alt, Key.Shift, Backtick], Fixture.OtherWindow);
    }

    [TestMethod]
    public void SettingsChangesBothShortcutsLiveAndUnregistersOldShortcuts()
    {
        SetShortcut(0, [Key.Ctrl, Key.F9], "next_window_shortcut", 120);
        SetShortcut(1, [Key.Ctrl, Key.F10], "previous_window_shortcut", 121);

        Fixture.ArrangeForegroundOrder();
        StartCycle([Key.Ctrl, Key.F9]);
        CommitCycle(Fixture.Windows[1]);
        Fixture.ArrangeForegroundOrder();
        StartCycle([Key.Ctrl, Key.F10]);
        CommitCycle(Fixture.Windows[2]);

        Fixture.ArrangeForegroundOrder();
        AssertShortcutInactive([Key.Alt, Backtick], Fixture.Windows[0]);
        AssertShortcutInactive([Key.Alt, Key.Shift, Backtick], Fixture.Windows[0]);
    }

    [TestMethod]
    public void SettingsDisableStopsRuntimeAndBothShortcutsUntilReenabled()
    {
        StartCycle([Key.Alt, Backtick]);
        CommitCycle(Fixture.Windows[1]);
        Session.EnsureForeground();
        var toggle = ModuleToggle();
        Step("Disabling Window Hopper through the real Settings switch.");
        toggle.Invoke(msPostAction: 0);
        Assert.IsTrue(toggle.WaitForProperty("ToggleState", "Off", 10_000), "The Settings switch did not turn off.");
        Assert.IsTrue(
            WaitHelper.WaitForStable(() => WindowControl.AnyWindowOfClassExists(OverlayClass), exists => !exists, 15_000, 3).Succeeded,
            "The Runner did not destroy the module's overlay after Settings disabled it. Check Settings IPC authentication.");

        Fixture.ArrangeForegroundOrder();
        AssertShortcutInactive([Key.Alt, Backtick], Fixture.Windows[0]);
        AssertShortcutInactive([Key.Alt, Key.Shift, Backtick], Fixture.Windows[0]);

        Step("Re-enabling Window Hopper through Settings without restarting PowerToys.");
        toggle.Invoke(msPostAction: 0);
        Assert.IsTrue(toggle.WaitForProperty("ToggleState", "On", 10_000), "The Settings switch did not turn on.");
        Assert.IsTrue(
            WaitHelper.WaitForStable(() => WindowControl.AnyWindowOfClassExists(OverlayClass), exists => exists, 15_000).Succeeded,
            "The Runner did not recreate the module's overlay after re-enabling.");
        Fixture.ArrangeForegroundOrder();
        StartCycle([Key.Alt, Key.Shift, Backtick]);
        CommitCycle(Fixture.Windows[2]);
    }

    [TestCleanup]
    public async Task RestoreTestState()
    {
        await CaptureFailureArtifactsBeforeCleanupAsync();
        var errors = new List<Exception>();
        Clean(ReleaseModifiers);
        Clean(() => fixture?.Dispose());
        Clean(() => Assert.IsTrue(WindowControl.TryKillProcessTreeByNameAndWait("PowerToys", 10_000), "Could not stop the test Runner before restoring settings."));
        Clean(() => Assert.IsTrue(WindowControl.TryKillProcessTreeByNameAndWait("PowerToys.Settings", 10_000), "Could not stop test Settings before restoring module settings."));
        Clean(() => settingsSnapshot?.Dispose());
        settingsSnapshot = null;
        if (errors.Count > 0)
        {
            throw new AggregateException("Window Hopper test cleanup failed.", errors);
        }

        void Clean(Action action)
        {
            try
            {
                action();
            }
            catch (Exception error)
            {
                TestContext.WriteLine($"Cleanup failure: {error}");
                errors.Add(error);
            }
        }
    }

    private ToggleSwitch ModuleToggle()
    {
        var tree = Session.Inspect(depth: 16);
        var nodes = Nodes(tree).Where(node => Property(node, "className") == "ToggleSwitch").ToArray();
        Assert.HasCount(1, nodes, "The Window Hopper page must expose exactly one module switch.");
        return Session.Find<ToggleSwitch>(By.Slug(Property(nodes[0], "selector")));
    }

    private AutomationButton[] ShortcutButtons()
    {
        var buttons = Session.FindAll<AutomationButton>(By.AccessibilityId("EditButton"), 10_000).OrderBy(button => button.Y).ToArray();
        Assert.HasCount(2, buttons, "Expected the next-window and previous-window shortcut editors in page order.");
        return buttons;
    }

    private void SetShortcut(int index, Key[] keys, string property, int virtualKey)
    {
        Step($"Editing {property} through Settings.");
        ShortcutButtons()[index].Invoke(msPostAction: 0);
        var captureFocus = Session.Find(By.AccessibilityId("ResetBtn"), 10_000);
        Session.EnsureForeground();
        Assert.IsTrue(
            WindowControl.WaitForForeground(new IntPtr(Session.WindowHandle), 10_000),
            $"Settings did not acquire foreground for shortcut capture: {WindowControl.GetForegroundWindowInfo()}.");
        captureFocus.Focus();
        Assert.IsTrue(captureFocus.WaitForProperty("HasKeyboardFocus", "true", 5_000), "The shortcut capture control did not receive focus.");
        KeyboardHelper.SendChord(keys);
        var save = Session.Find<AutomationButton>(By.AccessibilityId("PrimaryButton"));
        Assert.IsTrue(save.WaitForProperty("IsEnabled", "true", 5_000), "The shortcut editor rejected the chord.");
        save.Invoke(msPostAction: 0);
        Assert.IsTrue(save.WaitForGone(10_000), "The shortcut dialog did not close.");
        var persisted = WaitHelper.WaitForStable(
            () => ReadModuleSettings()["properties"]?[property]?.DeepClone(),
            shortcut => shortcut?["code"]?.GetValue<int>() == virtualKey &&
                shortcut["ctrl"]!.GetValue<bool>() && !shortcut["alt"]!.GetValue<bool>() &&
                !shortcut["shift"]!.GetValue<bool>() && !shortcut["win"]!.GetValue<bool>(),
            10_000,
            shouldRetryException: error => error is IOException or System.Text.Json.JsonException);
        Assert.IsTrue(persisted.Succeeded, $"Settings did not persist {property}: {persisted.LastObservation}.");
        var helpText = ShortcutButtons()[index].HelpText;
        StringAssert.Contains(helpText, "Ctrl", "The saved shortcut must be exposed accessibly.");
        StringAssert.Contains(helpText, keys[^1].ToString(), "The saved shortcut displays the wrong key.");
    }

    private void StartCycle(Key[] keys)
    {
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            Step($"Holding {string.Join(" + ", keys[..^1])} and tapping {keys[^1]} (attempt {attempt}).");
            foreach (var key in keys[..^1])
            {
                Hold(key);
            }

            Tap(keys[^1]);
            if (WaitHelper.WaitForStable(() => WindowControl.IsAnyWindowOfClassVisible(OverlayClass), visible => visible, 3_000, 2).Succeeded)
            {
                AssertAnchorForeground();
                return;
            }

            ReleaseModifiers();
            Fixture.ArrangeForegroundOrder();
        }

        Assert.Fail($"The shortcut did not display the switcher. Foreground: {WindowControl.GetForegroundWindowInfo()}.");
    }

    private void CommitCycle(IntPtr expected)
    {
        Step($"Releasing the held modifiers; expecting HWND 0x{expected.ToInt64():X}.");
        ReleaseModifiers();
        WaitForOverlayHidden();
        var result = WaitHelper.WaitForStable(
            WindowControl.GetForegroundWindowInfo,
            foreground => foreground.Hwnd == expected,
            10_000,
            requiredConsecutiveMatches: 3);
        Assert.IsTrue(result.Succeeded, $"The selected window was not activated. Expected 0x{expected.ToInt64():X}; actual {result.LastObservation}.");
    }

    private void AssertShortcutInactive(Key[] keys, IntPtr expectedForeground)
    {
        Step($"Checking that {string.Join(" + ", keys)} does not display or activate the switcher.");
        using var watcher = new WindowShowWatcher(OverlayClass);
        foreach (var key in keys[..^1])
        {
            Hold(key);
        }

        Tap(keys[^1]);
        var shownWhileHeld = watcher.Wait(1_000);
        ReleaseModifiers();
        var shownAfterRelease = watcher.Wait(1_000);
        Assert.IsFalse(shownWhileHeld || shownAfterRelease, $"An inactive shortcut displayed the switcher: {string.Join(", ", watcher.Events)}.");
        Assert.IsFalse(WindowControl.IsAnyWindowOfClassVisible(OverlayClass), "An inactive shortcut left the switcher visible.");
        Assert.AreEqual(expectedForeground, WindowControl.GetForegroundWindowInfo().Hwnd, "An inactive shortcut changed the foreground window.");
    }

    private void AssertPreviewMembership()
    {
        Step("Checking composed previews for all three source colors and no foreign-application preview.");
        var result = WaitHelper.WaitForStable(
            ReadPreviewColors,
            counts => counts is { Length: 4 } && counts.Take(3).All(count => count >= 100) && counts[3] == 0,
            10_000,
            requiredConsecutiveMatches: 2);
        var path = Path.Combine(TestContext.TestResultsDirectory!, $"switcher-{Guid.NewGuid():N}.png");
        Assert.IsTrue(ScreenCapture.TryCaptureDesktop(path), "Could not capture the visible switcher.");
        TestContext.AddResultFile(path);
        Assert.IsTrue(result.Succeeded, $"The switcher must show all three same-image previews and no foreign preview. Color samples: {string.Join(", ", result.LastObservation ?? [])}.");
    }

    private static int[] ReadPreviewColors()
    {
        var overlay = WindowControl.EnumerateAllWindows().Single(window => window.ClassName == OverlayClass && window.IsVisible);
        var (left, top, right, bottom) = WindowHelper.GetWindowBounds(overlay.Hwnd);
        using var bitmap = new Bitmap(right - left, bottom - top, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.CopyFromScreen(new Point(left, top), Point.Empty, bitmap.Size);
        }

        var colors = WindowFixture.Colors.Append(WindowFixture.OtherColor).ToArray();
        var counts = new int[colors.Length];
        for (var y = 0; y < bitmap.Height; y += 6)
        {
            for (var x = 0; x < bitmap.Width; x += 6)
            {
                var pixel = bitmap.GetPixel(x, y);
                for (var color = 0; color < colors.Length; color++)
                {
                    if (Math.Abs(pixel.R - colors[color].R) <= 5 &&
                        Math.Abs(pixel.G - colors[color].G) <= 5 &&
                        Math.Abs(pixel.B - colors[color].B) <= 5)
                    {
                        counts[color]++;
                    }
                }
            }
        }

        return counts;
    }

    private void AssertAnchorForeground() =>
        Assert.AreEqual(Fixture.Windows[0], WindowControl.GetForegroundWindowInfo().Hwnd, "The switcher must not activate a selection before the held modifier is released.");

    private static void WaitForOverlayHidden() =>
        Assert.IsTrue(
            WaitHelper.WaitForStable(() => WindowControl.IsAnyWindowOfClassVisible(OverlayClass), visible => !visible, 10_000, 3).Succeeded,
            "The switcher did not hide after commit or cancellation.");

    private void Hold(Key key)
    {
        heldModifiers.Add(key);
        KeyboardHelper.PressKey(key);
    }

    private void Release(Key key)
    {
        KeyboardHelper.ReleaseKey(key);
        heldModifiers.Remove(key);
    }

    private void ReleaseModifiers()
    {
        foreach (var key in heldModifiers.AsEnumerable().Reverse().ToArray())
        {
            Release(key);
        }
    }

    private static void Tap(Key key)
    {
        KeyboardHelper.SendKey(key);
        Thread.Sleep(150); // Pace distinct user key presses; final HWND assertions verify asynchronous processing.
    }

    private static JsonNode ReadModuleSettings()
    {
        var path = Path.Combine(SettingsConfigHelper.PowerToysSettingsRoot, ModuleName, "settings.json");
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return JsonNode.Parse(file) ?? throw new InvalidDataException($"Invalid settings object: {path}");
    }

    private static IEnumerable<System.Text.Json.JsonElement> Nodes(System.Text.Json.JsonElement node)
    {
        if (node.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            if (node.TryGetProperty("type", out _))
            {
                yield return node;
            }

            foreach (var property in node.EnumerateObject())
            {
                foreach (var child in Nodes(property.Value))
                {
                    yield return child;
                }
            }
        }
        else if (node.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            foreach (var item in node.EnumerateArray())
            {
                foreach (var child in Nodes(item))
                {
                    yield return child;
                }
            }
        }
    }

    private static string Property(System.Text.Json.JsonElement node, string name) =>
        node.TryGetProperty(name, out var value) && value.ValueKind == System.Text.Json.JsonValueKind.String
            ? value.GetString()!
            : string.Empty;

    private void Step(string message) => TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] {message}");
}

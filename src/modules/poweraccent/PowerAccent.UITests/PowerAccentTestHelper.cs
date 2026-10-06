// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PowerAccent.UITests;

internal static class PowerAccentTestHelper
{
    internal const string ModuleName = "QuickAccent";
    internal const string ProcessName = "PowerToys.PowerAccent";
    internal const string ToggleId = "Toggle_QuickAccent";
    internal const string NavigationItemId = "QuickAccentNavItem";
    internal const string NavigationGroupId = "InputOutputNavItem";

    internal static readonly string[] CurrencySCharacters = ["$", "₪"];
    internal static readonly string[] FrenchACharacters = ["à", "â", "á", "ä", "ã", "æ"];

    private const int EnglishUnitedStatesLanguageId = 0x0409;
    private const int OverlayStartupTimeoutMs = 30_000;
    private const int SettingsReloadTimeoutMs = 30_000;
    private const int VkCapital = 0x14;

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetKeyboardLayout(uint threadId);

    [DllImport("user32.dll")]
    private static extern short GetKeyState(int virtualKey);

    internal enum ActivationKey
    {
        LeftRightArrow,
        Space,
        Both,
        PressAndHold,
    }

    internal sealed record Settings(
        ActivationKey Activation = ActivationKey.Both,
        string ToolbarPosition = "Top center",
        int InputTimeMs = 300,
        int HoldDurationMs = 500,
        string SelectedLanguage = "ALL",
        string ExcludedApps = "",
        bool SortByUsageFrequency = false,
        bool StartSelectionFromTheLeft = false);

    private sealed record OverlayObservation(
        WindowControl.ProcessWindow Window,
        bool IsCloaked,
        (int Left, int Top, int Right, int Bottom) Bounds);

    internal static void PrepareDefaultState()
    {
        WriteSettings(new Settings());
        File.Delete(UsageInfoPath);
    }

    internal static void ReplaceSettings(UITestBase testBase, Settings settings)
    {
        WaitForOverlayState(testBase, revealed: false, timeoutMs: OverlayStartupTimeoutMs);
        var logOffsets = CaptureQuickAccentLogOffsets();
        WriteSettings(settings);
        WaitForSettingsReload(testBase, settings.SelectedLanguage, logOffsets);
    }

    internal static void AssertInputEnvironment()
    {
        var languageId = (int)(GetKeyboardLayout(0).ToInt64() & 0xFFFF);
        Assert.AreEqual(
            EnglishUnitedStatesLanguageId,
            languageId,
            $"Quick Accent UI tests require the en-US keyboard layout (0x0409), but the active layout is 0x{languageId:X4}.");
        Assert.IsFalse(
            (GetKeyState(VkCapital) & 1) != 0,
            "Quick Accent UI tests require Caps Lock to be off because raw virtual-key input is asserted as lowercase text.");
    }

    private static void WriteSettings(Settings settings)
    {
        var desired = CreateSettings(settings);
        SettingsConfigHelper.UpdateModuleSettings(
            ModuleName,
            desired.ToJsonString(),
            current =>
            {
                current.Clear();
                foreach (var property in desired)
                {
                    current[property.Key] = property.Value?.DeepClone();
                }
            });
    }

    private static IReadOnlyDictionary<string, long> CaptureQuickAccentLogOffsets()
    {
        var offsets = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(QuickAccentLogRoot))
        {
            return offsets;
        }

        foreach (var path in Directory.EnumerateFiles(QuickAccentLogRoot, "*.log", SearchOption.AllDirectories))
        {
            try
            {
                offsets[path] = new FileInfo(path).Length;
            }
            catch (FileNotFoundException)
            {
                // A logger rotated the file between enumeration and inspection.
            }
        }

        return offsets;
    }

    private static void WaitForSettingsReload(
        UITestBase testBase,
        string selectedLanguage,
        IReadOnlyDictionary<string, long> logOffsets)
    {
        var languages = selectedLanguage
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(language => language.Trim().ToUpperInvariant());
        var acknowledgement = $"Languages selected: {string.Join(", ", languages)}";

        var result = WaitHelper.WaitForStable(
            () => FindSettingsReloadAcknowledgement(logOffsets, acknowledgement),
            matchedLog => matchedLog is not null,
            SettingsReloadTimeoutMs,
            pollIntervalMS: 100,
            shouldRetryException: ex => ex is IOException or UnauthorizedAccessException);
        var failure = $"Quick Accent did not acknowledge its settings reload within {SettingsReloadTimeoutMs} ms. " +
                      $"Expected a new '{acknowledgement}' log entry under '{QuickAccentLogRoot}'. " +
                      $"Last read error: {result.LastException?.Message ?? "(none)"}.";
        Assert.IsTrue(
            result.Succeeded,
            failure);
        Step(testBase, $"Quick Accent acknowledged settings reload in '{result.LastObservation}'.");
    }

    private static string? FindSettingsReloadAcknowledgement(
        IReadOnlyDictionary<string, long> logOffsets,
        string acknowledgement)
    {
        if (!Directory.Exists(QuickAccentLogRoot))
        {
            return null;
        }

        foreach (var path in Directory.EnumerateFiles(QuickAccentLogRoot, "*.log", SearchOption.AllDirectories))
        {
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            var originalLength = logOffsets.TryGetValue(path, out var length) ? length : 0;
            stream.Position = Math.Min(originalLength, stream.Length);
            using var reader = new StreamReader(stream);
            if (reader.ReadToEnd().Contains(acknowledgement, StringComparison.Ordinal))
            {
                return path;
            }
        }

        return null;
    }

    internal static IDisposable PreserveUsageInfo() => SettingsConfigHelper.PreserveFile(UsageInfoPath);

    internal static void DisposeAll(params IDisposable[] snapshots)
    {
        List<Exception>? failures = null;
        foreach (var snapshot in snapshots.Reverse())
        {
            try
            {
                snapshot.Dispose();
            }
            catch (Exception ex)
            {
                failures ??= [];
                failures.Add(ex);
            }
        }

        if (failures is { Count: 1 })
        {
            ExceptionDispatchInfo.Capture(failures[0]).Throw();
        }

        if (failures is not null)
        {
            throw new AggregateException("Quick Accent test state could not be fully restored.", failures);
        }
    }

    internal static void RunWithCleanup(Action action, Action cleanup)
    {
        Exception? actionFailure = null;
        try
        {
            action();
        }
        catch (Exception ex)
        {
            actionFailure = ex;
        }

        Exception? cleanupFailure = null;
        try
        {
            cleanup();
        }
        catch (Exception ex)
        {
            cleanupFailure = ex;
        }

        if (actionFailure is not null)
        {
            if (cleanupFailure is not null)
            {
                throw new AggregateException("The test action and its state restoration both failed.", actionFailure, cleanupFailure);
            }

            ExceptionDispatchInfo.Capture(actionFailure).Throw();
        }

        if (cleanupFailure is not null)
        {
            ExceptionDispatchInfo.Capture(cleanupFailure).Throw();
        }
    }

    internal static void NavigateToSettings(UITestBase testBase)
    {
        Step(testBase, "Navigating to Quick Accent settings");
        if (!testBase.Session.Has(By.AccessibilityId(NavigationItemId), 500))
        {
            testBase.Session.Find<NavigationViewItem>(By.AccessibilityId(NavigationGroupId), 5_000).Click(msPostAction: 500);
        }

        testBase.Session.Find<NavigationViewItem>(By.AccessibilityId(NavigationItemId), 5_000).Click(msPostAction: 800);
    }

    internal static ToggleSwitch SetModuleEnabled(UITestBase testBase, bool enabled)
    {
        var expectedState = enabled ? "On" : "Off";
        Step(testBase, $"Setting Quick Accent to {expectedState}");

        ToggleSwitch? toggle = null;
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            toggle = testBase.Session.Find<ToggleSwitch>(By.AccessibilityId(ToggleId), 10_000);
            if (toggle.GetProperty("ToggleState").Equals(expectedState, StringComparison.OrdinalIgnoreCase))
            {
                return toggle;
            }

            toggle.Invoke(msPostAction: 500);
            if (toggle.WaitForProperty("ToggleState", expectedState, 15_000))
            {
                return toggle;
            }

            Step(testBase, $"Quick Accent toggle did not reach {expectedState} on attempt {attempt}/3");
        }

        Assert.Fail($"Quick Accent toggle did not reach {expectedState} after three attempts.");
        return toggle!;
    }

    internal static bool WaitForProcess(bool expected, int timeoutMs = 15_000)
    {
        var result = WaitHelper.WaitForStable(
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
            requiredConsecutiveMatches: 3,
            pollIntervalMS: 150);
        return result.Succeeded;
    }

    internal static string RunTriggeredGesture(
        UITestBase testBase,
        NotepadFixture notepad,
        Key letter,
        Key trigger,
        Action<Session>? whileToolbarShown = null,
        int revealTimeoutMs = 10_000)
    {
        notepad.Clear();
        WaitForOverlayState(testBase, revealed: false, timeoutMs: OverlayStartupTimeoutMs);
        notepad.Focus();

        var letterDown = false;
        var triggerDown = false;
        try
        {
            Step(testBase, $"Holding {letter} and pressing {trigger}");
            KeyboardHelper.PressKey(letter);
            letterDown = true;
            Thread.Sleep(50);
            KeyboardHelper.PressKey(trigger);
            triggerDown = true;

            var toolbar = WaitForOverlayState(testBase, revealed: true, timeoutMs: revealTimeoutMs);
            KeyboardHelper.ReleaseKey(trigger);
            triggerDown = false;

            whileToolbarShown?.Invoke(toolbar);

            notepad.Focus();
            KeyboardHelper.ReleaseKey(letter);
            letterDown = false;
            WaitForOverlayState(testBase, revealed: false, timeoutMs: 5_000);
            return notepad.WaitForNonEmptyText();
        }
        finally
        {
            if (triggerDown)
            {
                KeyboardHelper.ReleaseKey(trigger);
            }

            if (letterDown)
            {
                KeyboardHelper.ReleaseKey(letter);
            }
        }
    }

    internal static string RunPressAndHoldGesture(
        UITestBase testBase,
        NotepadFixture notepad,
        Key letter,
        Action<Session> whileToolbarShown,
        int revealTimeoutMs = 10_000)
    {
        notepad.Clear();
        WaitForOverlayState(testBase, revealed: false, timeoutMs: OverlayStartupTimeoutMs);
        notepad.Focus();

        var letterDown = false;
        try
        {
            Step(testBase, $"Holding {letter} until the Quick Accent toolbar appears");
            KeyboardHelper.PressKey(letter);
            letterDown = true;

            var toolbar = WaitForOverlayState(testBase, revealed: true, timeoutMs: revealTimeoutMs);
            whileToolbarShown(toolbar);

            notepad.Focus();
            KeyboardHelper.ReleaseKey(letter);
            letterDown = false;
            WaitForOverlayState(testBase, revealed: false, timeoutMs: 5_000);
            return notepad.WaitForNonEmptyText();
        }
        finally
        {
            if (letterDown)
            {
                KeyboardHelper.ReleaseKey(letter);
            }
        }
    }

    internal static string RunSuppressedGesture(
        UITestBase testBase,
        NotepadFixture notepad,
        Key letter,
        Key trigger,
        int observationMs = 800)
    {
        notepad.Clear();
        WaitForOverlayState(testBase, revealed: false, timeoutMs: OverlayStartupTimeoutMs);
        notepad.Focus();

        var letterDown = false;
        var triggerDown = false;
        try
        {
            Step(testBase, $"Holding {letter} + {trigger}; the toolbar must stay cloaked");
            KeyboardHelper.PressKey(letter);
            letterDown = true;
            Thread.Sleep(50);
            KeyboardHelper.PressKey(trigger);
            triggerDown = true;

            AssertOverlayRemainsCloaked(testBase, observationMs);

            KeyboardHelper.ReleaseKey(trigger);
            triggerDown = false;
            KeyboardHelper.ReleaseKey(letter);
            letterDown = false;
            return notepad.WaitForNonEmptyText();
        }
        finally
        {
            if (triggerDown)
            {
                KeyboardHelper.ReleaseKey(trigger);
            }

            if (letterDown)
            {
                KeyboardHelper.ReleaseKey(letter);
            }
        }
    }

    internal static string RunFalseStart(
        UITestBase testBase,
        NotepadFixture notepad,
        Key letter,
        Key trigger,
        int heldMs,
        int verificationMs)
    {
        notepad.Clear();
        WaitForOverlayState(testBase, revealed: false, timeoutMs: OverlayStartupTimeoutMs);
        notepad.Focus();

        var letterDown = false;
        var triggerDown = false;
        try
        {
            Step(testBase, $"Sending a {heldMs} ms {letter} + {trigger} false start");
            KeyboardHelper.PressKey(letter);
            letterDown = true;
            Thread.Sleep(50);
            KeyboardHelper.PressKey(trigger);
            triggerDown = true;
            Thread.Sleep(heldMs);
            KeyboardHelper.ReleaseKey(trigger);
            triggerDown = false;
            KeyboardHelper.ReleaseKey(letter);
            letterDown = false;

            AssertOverlayRemainsCloaked(testBase, verificationMs);
            return notepad.WaitForNonEmptyText();
        }
        finally
        {
            if (triggerDown)
            {
                KeyboardHelper.ReleaseKey(trigger);
            }

            if (letterDown)
            {
                KeyboardHelper.ReleaseKey(letter);
            }
        }
    }

    internal static string RunUnmodifiedGesture(NotepadFixture notepad, Key letter, Key trigger)
    {
        notepad.Clear();
        notepad.Focus();

        var letterDown = false;
        var triggerDown = false;
        try
        {
            KeyboardHelper.PressKey(letter);
            letterDown = true;
            Thread.Sleep(50);
            KeyboardHelper.PressKey(trigger);
            triggerDown = true;
            Thread.Sleep(100);
            KeyboardHelper.ReleaseKey(trigger);
            triggerDown = false;
            KeyboardHelper.ReleaseKey(letter);
            letterDown = false;
            return notepad.WaitForNonEmptyText();
        }
        finally
        {
            if (triggerDown)
            {
                KeyboardHelper.ReleaseKey(trigger);
            }

            if (letterDown)
            {
                KeyboardHelper.ReleaseKey(letter);
            }
        }
    }

    internal static string GetSelectedCharacter(Session toolbar, IReadOnlyList<string> candidates, int timeoutMs = 5_000)
    {
        string lastObservation = string.Empty;
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            foreach (var character in candidates)
            {
                var item = FindCharacterItem(toolbar, character);
                if (item is not null && item.Selected)
                {
                    return character;
                }
            }

            lastObservation = string.Join(
                ", ",
                candidates.Select(character =>
                {
                    var item = FindCharacterItem(toolbar, character);
                    return $"{character}:{(item is null ? "missing" : item.GetProperty("IsSelected"))}";
                }));
            Thread.Sleep(150);
        }

        Assert.Fail($"No selected Quick Accent character appeared within {timeoutMs} ms. Last observation: {lastObservation}");
        return string.Empty;
    }

    internal static IReadOnlyList<string> GetCharactersInVisualOrder(Session toolbar, IReadOnlyList<string> expectedCharacters)
    {
        var items = expectedCharacters
            .Select(character => (Character: character, Item: FindCharacterItem(toolbar, character)))
            .ToList();
        var missing = items.Where(item => item.Item is null).Select(item => item.Character).ToList();
        Assert.AreEqual(0, missing.Count, $"Quick Accent toolbar was missing expected characters: {string.Join(", ", missing)}");

        return items
            .OrderBy(item => item.Item!.X)
            .Select(item => item.Character)
            .ToList();
    }

    internal static void AssertToolbarPlacement(
        UITestBase testBase,
        Session toolbar,
        string horizontalAnchor,
        string verticalAnchor)
    {
        var bounds = GetStableVisibleBounds(toolbar);
        var handle = new IntPtr(toolbar.WindowHandle);
        var monitor = MonitorInfo.GetFromWindow(handle);
        var centerY = bounds.Top + ((bounds.Bottom - bounds.Top) / 2);
        var characterList = toolbar.Find<Element>(By.AccessibilityId("QuickAccentCharacterList"), 5_000);
        var dpiScale = Math.Max(1d, GetDpiForWindow(handle) / 96d);

        const int edgeOffset = 24; // Calculation.GetRawCoordinatesFromPosition offset.

        // SelectorControl.HorizontalSurfaceOverheadDip (24 + 24 margin, 1 + 1 border)
        // plus MainWindow.LayoutRoundingDip (1).
        const int horizontalChromeDip = 51;
        const int tolerance = 4;
        var positioningWidth = characterList.Width + (int)Math.Ceiling(horizontalChromeDip * dpiScale);
        var horizontalExpected = horizontalAnchor switch
        {
            "Left" => monitor.WorkLeft + edgeOffset,
            "Center" => monitor.WorkLeft + (int)Math.Round((monitor.WorkWidth - positioningWidth) / 2d),
            "Right" => monitor.WorkRight - positioningWidth - edgeOffset,
            _ => throw new ArgumentOutOfRangeException(nameof(horizontalAnchor), horizontalAnchor, "Unknown horizontal anchor."),
        };

        var verticalActual = verticalAnchor switch
        {
            "Top" => bounds.Top,
            "Center" => centerY,
            "Bottom" => bounds.Bottom,
            _ => throw new ArgumentOutOfRangeException(nameof(verticalAnchor), verticalAnchor, "Unknown vertical anchor."),
        };
        var verticalExpected = verticalAnchor switch
        {
            "Top" => monitor.WorkTop + edgeOffset,
            "Center" => monitor.WorkTop + (monitor.WorkHeight / 2),
            "Bottom" => monitor.WorkBottom - edgeOffset,
            _ => throw new ArgumentOutOfRangeException(nameof(verticalAnchor), verticalAnchor, "Unknown vertical anchor."),
        };

        var workArea = $"{monitor.DeviceName} " +
                       $"({monitor.WorkLeft},{monitor.WorkTop})-({monitor.WorkRight},{monitor.WorkBottom}) " +
                       $"{monitor.WorkWidth}x{monitor.WorkHeight}";
        var placementDetails = $"Toolbar visible bounds are ({bounds.Left},{bounds.Top})-({bounds.Right},{bounds.Bottom}); " +
                               $"character list=({characterList.X},{characterList.Y}) {characterList.Width}x{characterList.Height}; " +
                               $"positioning width={positioningWidth}; work area={workArea}";
        Step(testBase, placementDetails);
        var horizontalFailure = $"Toolbar horizontal {horizontalAnchor} coordinate was {bounds.Left}; " +
                                $"expected {horizontalExpected} +/- {tolerance}. Visible bounds: {bounds}; " +
                                $"character list width={characterList.Width}; work area={workArea}.";
        Assert.IsTrue(
            Math.Abs(bounds.Left - horizontalExpected) <= tolerance,
            horizontalFailure);
        var verticalFailure = $"Toolbar vertical {verticalAnchor} coordinate was {verticalActual}; " +
                              $"expected {verticalExpected} +/- {tolerance}. Visible bounds: {bounds}; work area={workArea}.";
        Assert.IsTrue(
            Math.Abs(verticalActual - verticalExpected) <= tolerance,
            verticalFailure);
    }

    internal static IDisposable PreserveClipboardText() => new ClipboardTextSnapshot();

    internal static void Step(UITestBase testBase, string message) =>
        testBase.TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] {message}");

    private static string UsageInfoPath => Path.Combine(
        SettingsConfigHelper.PowerToysSettingsRoot,
        ModuleName,
        "UsageInfo.json");

    private static string QuickAccentLogRoot => Path.Combine(
        SettingsConfigHelper.PowerToysSettingsRoot,
        ModuleName,
        "Logs");

    private static JsonObject CreateSettings(Settings settings) =>
        new()
        {
            ["name"] = ModuleName,
            ["version"] = "0.0.1",
            ["properties"] = new JsonObject
            {
                ["activation_key"] = (int)settings.Activation,
                ["do_not_activate_on_game_mode"] = true,
                ["toolbar_position"] = Value(settings.ToolbarPosition),
                ["input_time_ms"] = Value(settings.InputTimeMs),
                ["hold_duration_ms"] = Value(settings.HoldDurationMs),
                ["selected_lang"] = Value(settings.SelectedLanguage),
                ["excluded_apps"] = Value(settings.ExcludedApps),
                ["show_description"] = false,
                ["sort_by_usage_frequency"] = settings.SortByUsageFrequency,
                ["start_selection_from_the_left"] = settings.StartSelectionFromTheLeft,
            },
        };

    private static JsonObject Value(string value) => new() { ["value"] = value };

    private static JsonObject Value(int value) => new() { ["value"] = value };

    private static Session WaitForOverlayState(UITestBase testBase, bool revealed, int timeoutMs)
    {
        var result = WaitHelper.WaitForStable(
            ObserveOverlay,
            observation => observation is not null && observation.IsCloaked == !revealed,
            timeoutMs,
            requiredConsecutiveMatches: 3,
            pollIntervalMS: 100,
            shouldRetryException: ex => ex is InvalidOperationException);

        var last = result.LastObservation;
        var stateName = revealed ? "revealed" : "cloaked";
        var timeoutFailure = $"Quick Accent overlay did not become {stateName} within {timeoutMs} ms. " +
                             $"Last observation: {FormatObservation(last)}";
        Assert.IsTrue(
            result.Succeeded && last is not null,
            timeoutFailure);

        Step(testBase, $"Quick Accent overlay is {stateName}: {FormatObservation(last)}");
        return WindowsFinder.WaitForWindowByApp(
            ProcessName,
            candidate => candidate.Hwnd == last!.Window.Hwnd.ToInt64(),
            timeoutMS: 2_000)
            ?? throw new AssertFailedException($"Could not bind winappcli to Quick Accent HWND {last!.Window.Hwnd}.");
    }

    private static void AssertOverlayRemainsCloaked(UITestBase testBase, int durationMs)
    {
        var stopwatch = Stopwatch.StartNew();
        OverlayObservation? last = null;
        while (stopwatch.ElapsedMilliseconds < durationMs)
        {
            last = ObserveOverlay();
            Assert.IsNotNull(last, $"Quick Accent process lost its overlay window while it should remain cloaked.");
            Assert.IsTrue(
                last.IsCloaked,
                $"Quick Accent overlay revealed during a suppressed gesture after {stopwatch.ElapsedMilliseconds} ms: {FormatObservation(last)}");
            Thread.Sleep(50);
        }

        Step(testBase, $"Quick Accent overlay remained cloaked for {durationMs} ms: {FormatObservation(last)}");
    }

    private static (int Left, int Top, int Right, int Bottom) GetStableVisibleBounds(Session toolbar)
    {
        var handle = new IntPtr(toolbar.WindowHandle);
        var hasPrevious = false;
        var previous = default((int Left, int Top, int Right, int Bottom));
        var result = WaitHelper.WaitForStable(
            () => WindowHelper.GetVisibleBounds(handle),
            bounds =>
            {
                var valid = bounds.Right > bounds.Left && bounds.Bottom > bounds.Top;
                var unchanged = hasPrevious && bounds == previous;
                previous = bounds;
                hasPrevious = true;
                return valid && unchanged;
            },
            timeoutMS: 5_000,
            requiredConsecutiveMatches: 3,
            pollIntervalMS: 100,
            shouldRetryException: ex => ex is InvalidOperationException);
        var failure = $"Quick Accent toolbar bounds did not stabilize. Last bounds: {result.LastObservation}. " +
                      $"Last DWM error: {result.LastException?.Message ?? "(none)"}.";
        Assert.IsTrue(
            result.Succeeded,
            failure);
        return result.LastObservation;
    }

    private static OverlayObservation? ObserveOverlay()
    {
        var processes = Process.GetProcessesByName(ProcessName);
        try
        {
            var ids = processes.Select(process => process.Id).ToHashSet();
            if (ids.Count == 0)
            {
                return null;
            }

            var window = WindowControl.EnumerateProcessWindows(ids)
                .Where(candidate => candidate.Width > 0 && candidate.Height > 0)
                .OrderByDescending(candidate => candidate.Title.Equals("Quick Accent", StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(candidate => candidate.Width * candidate.Height)
                .FirstOrDefault();
            if (window.Hwnd == IntPtr.Zero)
            {
                return null;
            }

            return new OverlayObservation(
                window,
                IsCloaked: WindowHelper.IsWindowCloaked(window.Hwnd),
                WindowHelper.GetWindowBounds(window.Hwnd));
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
    }

    private static Element? FindCharacterItem(Session toolbar, string character) =>
        toolbar.FindAll<Element>(By.Name(character), timeoutMS: 0)
            .FirstOrDefault(element =>
                element.ControlType.Equals("ListItem", StringComparison.OrdinalIgnoreCase) ||
                element.ClassName.Equals("ListViewItem", StringComparison.OrdinalIgnoreCase));

    private static string FormatObservation(OverlayObservation? observation) =>
        observation is null
            ? "(overlay window absent)"
            : $"hwnd={observation.Window.Hwnd}, title='{observation.Window.Title}', " +
              $"cloaked={observation.IsCloaked}, bounds={observation.Bounds}";

    internal sealed class NotepadFixture : IDisposable
    {
        private readonly UITestBase testBase;
        private readonly string filePath;
        private bool disposed;

        private NotepadFixture(UITestBase testBase, string filePath, Session window)
        {
            this.testBase = testBase;
            this.filePath = filePath;
            Window = window;
        }

        internal Session Window { get; }

        internal static NotepadFixture Start(UITestBase testBase)
        {
            var filePath = Path.Combine(Path.GetTempPath(), $"PowerToys-PowerAccent-{Guid.NewGuid():N}.txt");
            File.WriteAllText(filePath, string.Empty);
            var fileName = Path.GetFileName(filePath);
            var baseFileName = Path.GetFileNameWithoutExtension(filePath);

            try
            {
                var settingsHandle = new IntPtr(testBase.Session.WindowHandle);
                if (settingsHandle != IntPtr.Zero)
                {
                    Step(testBase, $"Minimizing Settings HWND {settingsHandle} before launching Notepad");
                    WindowHelper.MinimizeWindow(settingsHandle);
                }

                Session? window = null;
                for (var attempt = 1; attempt <= 2 && window is null; attempt++)
                {
                    Step(testBase, $"Opening Notepad fixture (attempt {attempt}/2)");
                    using var launcher = Process.Start(new ProcessStartInfo
                    {
                        FileName = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.System),
                            "notepad.exe"),
                        Arguments = $"\"{filePath}\"",
                        UseShellExecute = true,
                    });
                    Assert.IsNotNull(launcher, "Could not start the Notepad fixture.");

                    window = WindowsFinder.WaitForWindowByApp(
                        "notepad",
                        candidate =>
                            candidate.Title.Contains(fileName, StringComparison.OrdinalIgnoreCase) ||
                            candidate.Title.Contains(baseFileName, StringComparison.OrdinalIgnoreCase),
                        timeoutMS: 15_000);
                }

                Assert.IsNotNull(window, $"Notepad did not open the fixture document '{fileName}'.");
                return new NotepadFixture(testBase, filePath, window);
            }
            catch
            {
                RestoreSettingsWindow(testBase);
                File.Delete(filePath);
                throw;
            }
        }

        internal void Focus()
        {
            var handle = new IntPtr(Window.WindowHandle);
            var focused = WindowControl.WaitForForeground(handle, timeoutMS: 10_000, requiredConsecutiveMatches: 3);
            if (!focused)
            {
                var (centerX, centerY) = WindowHelper.GetWindowCenter(handle);
                Step(testBase, $"Notepad foreground handoff stalled; recovering with a guarded click at ({centerX},{centerY})");
                var pointReady = WaitHelper.WaitForStable(
                    () => WindowControl.IsPointOwnedByWindow(handle, centerX, centerY),
                    owned => owned,
                    timeoutMS: 5_000,
                    requiredConsecutiveMatches: 2,
                    pollIntervalMS: 100,
                    recover: _ => WindowControl.TryBringToForeground(handle));
                if (pointReady.Succeeded)
                {
                    MouseHelper.LeftClickAt(centerX, centerY);
                    focused = WindowControl.WaitForForeground(handle, timeoutMS: 5_000, requiredConsecutiveMatches: 3);
                }
            }

            Assert.IsTrue(
                focused,
                $"Notepad HWND {handle} did not become foreground. Current foreground: {WindowControl.GetForegroundWindowInfo()}");
        }

        internal void Clear()
        {
            Focus();
            SendControlChord(Key.A);
            KeyboardHelper.SendKey(Key.Backspace);
            Thread.Sleep(100);
        }

        internal string WaitForNonEmptyText(int timeoutMs = 5_000)
        {
            string text = string.Empty;
            var result = WaitHelper.WaitForStable(
                () =>
                {
                    text = ReadText();
                    return text;
                },
                value => !string.IsNullOrEmpty(value),
                timeoutMs,
                requiredConsecutiveMatches: 2,
                pollIntervalMS: 100);
            Assert.IsTrue(result.Succeeded, $"Notepad remained empty for {timeoutMs} ms.");
            return text;
        }

        internal string ReadText()
        {
            Focus();
            ClipboardHelper.Clear();
            SendControlChord(Key.A);
            SendControlChord(Key.C);
            return ClipboardHelper.WaitForText(string.Empty, timeoutMS: 1_000);
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            try
            {
                using var process = Process.GetProcessById(Window.ProcessId);
                if (!process.HasExited && (!process.CloseMainWindow() || !process.WaitForExit(3_000)))
                {
                    Step(testBase, $"Notepad pid {process.Id} did not close gracefully; terminating its process tree");
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(5_000);
                }
            }
            catch (ArgumentException)
            {
            }
            catch (InvalidOperationException)
            {
            }
            finally
            {
                try
                {
                    File.Delete(filePath);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }

                RestoreSettingsWindow(testBase);
            }
        }

        private static void SendControlChord(Key key)
        {
            KeyboardHelper.PressKey(Key.Ctrl);
            try
            {
                Thread.Sleep(30);
                KeyboardHelper.SendKey(key);
            }
            finally
            {
                KeyboardHelper.ReleaseKey(Key.Ctrl);
            }

            Thread.Sleep(50);
        }

        private static void RestoreSettingsWindow(UITestBase testBase)
        {
            var settingsHandle = new IntPtr(testBase.Session.WindowHandle);
            if (settingsHandle != IntPtr.Zero)
            {
                WindowHelper.MaximizeWindow(settingsHandle);
            }
        }
    }

    private sealed class ClipboardTextSnapshot : IDisposable
    {
        private readonly string originalText = ClipboardHelper.GetText();
        private bool disposed;

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            if (string.IsNullOrEmpty(originalText))
            {
                ClipboardHelper.Clear();
            }
            else
            {
                ClipboardHelper.SetText(originalText);
            }
        }
    }
}

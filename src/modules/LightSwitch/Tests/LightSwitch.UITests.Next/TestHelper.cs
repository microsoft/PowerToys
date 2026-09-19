// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.LightSwitch.UITests;

internal sealed class TestHelper
{
    public const string ServiceProcess = "PowerToys.LightSwitchService";
    private readonly Session window;
    private readonly TestContext context;
    private Session ui;
    private LogCursor? scheduleUpdate;

    public TestHelper(Session window, TestContext context)
    {
        this.window = window;
        this.context = context;
        ui = window;
    }

    public void Navigate()
    {
        Step("Navigating to Light Switch settings");
        if (!window.Has(By.AccessibilityId("LightSwitchNavItem"), 500))
        {
            window.Find<NavigationViewItem>(By.AccessibilityId("SystemToolsNavItem")).Invoke(msPostAction: 0);
        }

        window.Find<NavigationViewItem>(By.AccessibilityId("LightSwitchNavItem"), 10_000).Invoke(msPostAction: 0);
        window.Find<ToggleSwitch>(By.AccessibilityId("Toggle_LightSwitch"), 10_000);
        ui = Session.FromProcess(window.ProcessId.ToString(CultureInfo.InvariantCulture), PowerToysModule.PowerToysSettings);
    }

    public void SetEnabled(bool enabled)
    {
        Step($"Setting module enabled={enabled} through Settings IPC");
        var toggle = ui.Find<ToggleSwitch>(By.AccessibilityId("Toggle_LightSwitch"));
        if (toggle.IsOn != enabled)
        {
            toggle.Invoke(msPostAction: 0);
        }

        VerifyEnabled(enabled);
    }

    public void VerifyEnabled(bool enabled)
    {
        var toggle = ui.Find<ToggleSwitch>(By.AccessibilityId("Toggle_LightSwitch"));
        Assert.IsTrue(toggle.WaitForProperty("ToggleState", enabled ? "On" : "Off", 10_000), "The enable switch did not reflect the requested state.");
        var service = WaitHelper.WaitForStable(
            ObserveService,
            pids => enabled ? pids!.Length == 1 : pids!.Length == 0,
            timeoutMS: 20_000,
            requiredConsecutiveMatches: 3,
            pollIntervalMS: 250);
        Assert.IsTrue(service.Succeeded, $"Settings enabled={enabled}, but service PIDs were [{string.Join(", ", service.LastObservation ?? [])}]. Check RunnerLogs for rejected Settings IPC.");
    }

    public Key[] ReadShortcut()
    {
        Step("Reading the configured activation shortcut");
        ui.Find(By.AccessibilityId("Shortcut_LightSwitch"));
        var buttons = ui.FindAll<Button>(By.AccessibilityId("EditButton"));
        Assert.HasCount(1, buttons, "The Light Switch page should contain exactly one shortcut editor.");
        string text = buttons[0].HelpText;
        Assert.IsFalse(string.IsNullOrWhiteSpace(text), "The shortcut HelpText must not be empty.");
        var keys = text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(token => token.ToLowerInvariant() switch
            {
                "win" or "windows" => Key.LWin,
                "ctrl" or "control" => Key.Ctrl,
                "shift" => Key.Shift,
                "alt" => Key.Alt,
                _ => Enum.Parse<Key>(token, ignoreCase: true),
            }).ToArray();
        Assert.IsTrue(keys.Any(key => key is >= Key.A and <= Key.Z), $"Shortcut '{text}' must include a main key.");
        Step($"Shortcut from Settings: {text}");
        return keys;
    }

    public void SelectMode(string setting, string itemId, string caption)
    {
        Step($"Selecting schedule mode {setting}");
        var combo = ui.Find<ComboBox>(By.AccessibilityId("ModeSelection_LightSwitch"));
        if (combo.SelectedText != caption)
        {
            scheduleUpdate = new LogCursor();
            combo.Invoke(msPostAction: 0);
            ui.Find(By.AccessibilityId(itemId), 10_000).Invoke(msPostAction: 0);
        }

        Assert.IsTrue(combo.WaitForValue(caption, timeoutMS: 10_000), $"The mode selector must display '{caption}'.");
        WaitForSetting("scheduleMode", setting);
    }

    public void SetThemeTargets(bool system, bool apps)
    {
        SetCheck("ChangeSystemCheckbox_LightSwitch", system);
        WaitForSetting("changeSystem", system);
        SetCheck("ChangeAppsCheckbox_LightSwitch", apps);
        WaitForSetting("changeApps", apps);
    }

    public void SendShortcut()
    {
        var keys = ReadShortcut();
        RequireForeground();
        var logs = new LogCursor();
        Step($"Sending one theme-toggle chord: {string.Join(" + ", keys)}");
        KeyboardHelper.SendKeys(keys);
        var received = WaitHelper.WaitForStable(
            () => logs.ReadNew(),
            text => text!.Contains("[Light Switch] Hotkey triggered: Toggle Theme", StringComparison.Ordinal),
            timeoutMS: 20_000,
            pollIntervalMS: 250);
        Assert.IsTrue(received.Succeeded, $"The Runner did not acknowledge the single shortcut. Foreground: {WindowControl.GetForegroundWindowInfo()}. New LightSwitch logs:\n{received.LastObservation}");

        // Theme setters synchronously broadcast to desktop windows, so hotkey receipt is not completion.
        var completed = WaitHelper.WaitForStable(
            () => logs.ReadNew(),
            text => text!.Contains("[Light Switch] Manual override event set", StringComparison.Ordinal),
            timeoutMS: 120_000,
            pollIntervalMS: 250);
        Assert.IsTrue(completed.Succeeded, $"The Runner did not finish applying the single theme-toggle chord. New LightSwitch logs:\n{completed.LastObservation}");
    }

    public void WaitForTheme(ThemeState expected)
    {
        Step($"Waiting for HKCU theme state {expected}");
        var result = WaitHelper.WaitForStable(
            TestState.ReadTheme,
            theme => theme == expected,
            timeoutMS: 30_000,
            requiredConsecutiveMatches: 3,
            pollIntervalMS: 250);
        Assert.IsTrue(result.Succeeded, $"Expected HKCU theme {expected}, observed {result.LastObservation}.");
    }

    public void AssertThemeUnchanged(ThemeState expected)
    {
        Step($"Verifying both themes remain {expected} throughout five seconds after the acknowledged shortcut");
        var watch = Stopwatch.StartNew();
        do
        {
            Assert.AreEqual(expected, TestState.ReadTheme(), "A theme changed with both target checkboxes unchecked.");
            Assert.HasCount(1, ObserveService(), "The negative hotkey check requires the LightSwitch service to remain alive.");
            Thread.Sleep(100);
        }
        while (watch.Elapsed < TimeSpan.FromSeconds(5));
    }

    public void ChangeTime(string pickerId)
    {
        Step($"Opening {pickerId}");
        scheduleUpdate = new LogCursor();
        FindDescendant<Button>(pickerId, "Button").Invoke(msPostAction: 0);
        var minute = ui.Find(By.AccessibilityId("MinuteLoopingSelector"), 10_000);
        Step($"Moving the minute selection in {pickerId}");
        RequireForeground();
        minute.Focus();
        KeyboardHelper.SendKeys(Key.Up);
        ui.Find<Button>(By.AccessibilityId("AcceptButton"), 10_000).Invoke(msPostAction: 0);
        Assert.IsTrue(minute.WaitForGone(10_000), "The time picker flyout did not close after accepting.");
    }

    public int WaitForChangedTime(string name, int previous)
    {
        var changed = WaitHelper.WaitForStable(
            () => ReadSetting<int>(name),
            value => value != previous && value is >= 0 and < 1440,
            timeoutMS: 15_000,
            requiredConsecutiveMatches: 2,
            shouldRetryException: IsSettingsWriteInProgress);
        Assert.IsTrue(changed.Succeeded, $"{name} was not persisted after the UI edit; before={previous}, last={changed.LastObservation}.");
        return changed.LastObservation;
    }

    public void SetNumber(string id, int value)
    {
        Step($"Setting {id} to {value} through its UIA range-value pattern");
        scheduleUpdate = new LogCursor();
        var number = ui.Find(By.AccessibilityId(id));
        WinappCli.InvokeAssertSuccess("ui", "set-value", number.Selector, value.ToString(CultureInfo.InvariantCulture), ui.TargetFlag, ui.TargetValue);
    }

    public TimelineState WaitForTimeline(int start, int end)
    {
        var timeline = ui.Find(By.AccessibilityId("Timeline_LightSwitch"), 10_000);
        var result = WaitHelper.WaitForStable(
            () => timeline.HelpText,
            text => TryReadTimeline(text, out var state) && state.Start == start && state.End == end,
            timeoutMS: 15_000,
            requiredConsecutiveMatches: 2,
            pollIntervalMS: 200);
        Assert.IsTrue(result.Succeeded, $"Timeline must show Start={start}, End={end} minutes; last HelpText='{result.LastObservation}'.");
        Assert.IsTrue(TryReadTimeline(result.LastObservation, out var actual));
        return actual;
    }

    public TimelineState WaitForPersistedTimeline()
    {
        WaitForScheduleReload();
        int light = ReadSetting<int>("lightTime");
        int dark = ReadSetting<int>("darkTime");
        Assert.IsTrue(light is >= 0 and < 1440 && dark is >= 0 and < 1440 && light != dark, $"Sun times must be distinct valid minutes: {light}/{dark}.");
        return WaitForTimeline(light + ReadSetting<int>("sunrise_offset"), dark + ReadSetting<int>("sunset_offset"));
    }

    public void WaitForScheduledTheme()
    {
        WaitForScheduleReload();
        string mode = ReadSetting<string>("scheduleMode");
        Assert.IsTrue(mode is "FixedHours" or "SunsetToSunrise", "This check requires an active time schedule.");
        int light = ReadSetting<int>("lightTime") + (mode == "SunsetToSunrise" ? ReadSetting<int>("sunrise_offset") : 0);
        int dark = ReadSetting<int>("darkTime") + (mode == "SunsetToSunrise" ? ReadSetting<int>("sunset_offset") : 0);
        Step($"Waiting for native scheduler to apply boundaries {light}/{dark} to both HKCU themes");
        var result = WaitHelper.WaitForStable(
            () =>
            {
                int now = (DateTime.Now.Hour * 60) + DateTime.Now.Minute;
                bool isLight = light < dark ? now >= light && now < dark : now >= light || now < dark;
                return (Actual: TestState.ReadTheme(), Expected: new ThemeState(isLight ? 1 : 0, isLight ? 1 : 0));
            },
            state => state.Actual == state.Expected,
            timeoutMS: 30_000,
            requiredConsecutiveMatches: 3,
            pollIntervalMS: 500);
        Assert.IsTrue(result.Succeeded, $"Native schedule did not reach the expected HKCU theme state: {result.LastObservation}.");
    }

    public void OpenLocation()
    {
        Step("Opening the location dialog");
        ui.Find<Button>(By.AccessibilityId("SetLocationButton_LightSwitch")).Invoke(msPostAction: 0);
        var ready = WaitHelper.WaitForStable(
            () =>
            {
                // PickerHost's UWP dialog can own foreground without appearing in EnumWindows.
                var foreground = WindowControl.GetForegroundWindowInfo();
                WindowControl.ForegroundWindowInfo? consent = null;
                if (foreground.ProcessName.Equals("PickerHost", StringComparison.OrdinalIgnoreCase) &&
                    foreground.ClassName == "Shell_SystemDialog" &&
                    foreground.Title == "Let Windows and apps access your location?")
                {
                    consent = foreground;
                }

                return (Consent: consent, DialogOpen: ui.Has(By.AccessibilityId("LatitudeBox_LightSwitch"), 0));
            },
            state => state.Consent is not null || state.DialogOpen,
            timeoutMS: 30_000,
            pollIntervalMS: 250);
        Assert.IsTrue(ready.Succeeded, $"Neither the location dialog nor Windows location consent appeared. Foreground: {WindowControl.GetForegroundWindowInfo()}.");

        if (ready.LastObservation.Consent is { } prompt)
        {
            Step("Granting the Windows location consent requested by PowerToys Settings");
            string handle = prompt.Hwnd.ToInt64().ToString(CultureInfo.InvariantCulture);
            WinappCli.InvokeAssertSuccess("ui", "wait-for", "Yes", "-w", handle, "-t", "5000");
            var tree = WinappCli.InvokeJson("ui", "inspect", "-w", handle, "--json", "-d", "8");
            var accept = new List<string>();
            CollectSelectors(tree, "Button", accept, name: "Yes");
            Assert.HasCount(1, accept, "Expected exactly one Yes button in the Windows location consent window.");
            WinappCli.InvokeAssertSuccess("ui", "invoke", accept[0], "-w", handle);
        }

        ui.Find(By.AccessibilityId("LatitudeBox_LightSwitch"), 20_000);
        ui.Find(By.AccessibilityId("LongitudeBox_LightSwitch"));
    }

    public void DetectLocation()
    {
        Step("Requesting the real Windows geolocation provider");
        var sync = ui.Find<Button>(By.AccessibilityId("SyncLocationButton_LightSwitch"));
        Assert.AreEqual("true", sync.GetProperty("IsEnabled").ToLowerInvariant(), $"Geolocation prerequisite unavailable: {ReadLocationError()}. Enable Windows location services and provide a location source; manual coordinates are not a substitute for this test.");
        sync.Invoke(msPostAction: 0);
        var result = WaitHelper.WaitForStable(
            () => (Ready: sync.GetProperty("IsEnabled"), Error: ReadLocationError(), Times: HasSunTimes()),
            state => state.Ready.Equals("true", StringComparison.OrdinalIgnoreCase) && (state.Times || state.Error.Length > 0),
            timeoutMS: 30_000,
            pollIntervalMS: 250);
        Assert.IsTrue(result.Succeeded, $"Geolocation did not complete within 30s: {result.LastObservation}.");
        Assert.AreEqual(string.Empty, result.LastObservation.Error, $"Windows geolocation did not supply a position: {result.LastObservation.Error}. This is not a successful location-sync test.");
        Assert.IsTrue(result.LastObservation.Times, "A successful geolocation request must expose both sunrise and sunset.");
    }

    public SunTimes WaitForSunTimes()
    {
        Step("Reading both calculated sun times from the location dialog");
        var ready = WaitHelper.WaitForStable(HasSunTimes, value => value, timeoutMS: 15_000, requiredConsecutiveMatches: 2, pollIntervalMS: 200);
        Assert.IsTrue(ready.Succeeded, $"The location dialog must show nonblank sunrise and sunset. Error: {ReadLocationError()}");
        string sunrise = ui.Find(By.AccessibilityId("SunriseText_LightSwitch")).GetValue();
        string sunset = ui.Find(By.AccessibilityId("SunsetText_LightSwitch")).GetValue();
        Assert.IsTrue(DateTime.TryParse(sunrise, CultureInfo.CurrentCulture, DateTimeStyles.NoCurrentDateDefault, out var light), $"Invalid sunrise display '{sunrise}'.");
        Assert.IsTrue(DateTime.TryParse(sunset, CultureInfo.CurrentCulture, DateTimeStyles.NoCurrentDateDefault, out var dark), $"Invalid sunset display '{sunset}'.");
        return new SunTimes((light.Hour * 60) + light.Minute, (dark.Hour * 60) + dark.Minute);
    }

    public void SaveLocation()
    {
        Step("Saving the location dialog through Settings IPC");
        scheduleUpdate = new LogCursor();
        var save = ui.Find<Button>(By.AccessibilityId("PrimaryButton"));
        Assert.AreEqual("true", save.GetProperty("IsEnabled").ToLowerInvariant(), "Valid calculated coordinates must enable Save.");
        save.Invoke(msPostAction: 0);
        Assert.IsTrue(save.WaitForGone(10_000), "The location dialog did not close after saving.");
    }

    public void AssertSavedSunTimes(SunTimes times)
    {
        WaitForSetting("lightTime", times.Sunrise);
        WaitForSetting("darkTime", times.Sunset);
        WaitForTimeline(times.Sunrise, times.Sunset);
    }

    public void WaitForSetting<T>(string name, T expected)
    {
        Step($"Waiting for Runner-persisted {name}={expected}");
        var result = WaitHelper.WaitForStable(
            () => ReadSetting<T>(name),
            value => EqualityComparer<T>.Default.Equals(value, expected),
            timeoutMS: 15_000,
            requiredConsecutiveMatches: 2,
            pollIntervalMS: 200,
            shouldRetryException: IsSettingsWriteInProgress);
        Assert.IsTrue(result.Succeeded, $"Settings IPC did not persist {name}={expected}; last={result.LastObservation}; read error={result.LastException?.Message}.");
    }

    public static T ReadSetting<T>(string name)
    {
        using var stream = new FileStream(TestState.SettingsPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var document = JsonDocument.Parse(stream);
        return document.RootElement.GetProperty("properties").GetProperty(name).GetProperty("value").Deserialize<T>()!;
    }

    public static void StopProcesses()
    {
        foreach (string process in new[] { "PowerToys", "PowerToys.Settings", ServiceProcess })
        {
            Assert.IsTrue(WindowControl.TryKillProcessTreeByNameAndWait(process, 15_000), $"Could not stop test-owned {process} before restoring settings/themes.");
        }
    }

    public void SaveFailureState()
    {
        string directory = context.TestResultsDirectory ?? throw new InvalidOperationException("A results directory is required for failure evidence.");
        string prefix = Path.Combine(directory, $"LightSwitch-{context.TestName}");
        var result = WinappCli.Invoke("ui", "inspect", ui.TargetFlag, ui.TargetValue, "--json", "-d", "14");
        File.WriteAllText(prefix + "-uia.json", result.StdOut + Environment.NewLine + result.StdErr);
        context.AddResultFile(prefix + "-uia.json");
        File.Copy(TestState.SettingsPath, prefix + "-settings.json", overwrite: true);
        context.AddResultFile(prefix + "-settings.json");
        File.WriteAllText(prefix + "-state.txt", $"Theme: {TestState.ReadTheme()}\nService PIDs: {string.Join(", ", ObserveService())}\nForeground: {WindowControl.GetForegroundWindowInfo()}");
        context.AddResultFile(prefix + "-state.txt");
    }

    private void SetCheck(string id, bool expected)
    {
        Step($"Setting {id}={expected}");
        var check = ui.Find<CheckBox>(By.AccessibilityId(id));
        string state = check.GetProperty("ToggleState");
        Assert.IsTrue(state is "On" or "Off", $"Cannot determine checkbox state for {id}: '{state}'.");
        if ((state == "On") != expected)
        {
            check.Invoke(msPostAction: 0);
        }

        Assert.IsTrue(check.WaitForProperty("ToggleState", expected ? "On" : "Off", 10_000), $"{id} did not reach the requested state.");
    }

    private void RequireForeground()
    {
        Step("Establishing foreground ownership for physical keyboard input");
        Assert.IsTrue(
            WindowControl.WaitForForeground(new IntPtr(window.WindowHandle), 10_000, requiredConsecutiveMatches: 3),
            $"Settings must own foreground before SendInput. Observed {WindowControl.GetForegroundWindowInfo()}.");
    }

    private T FindDescendant<T>(string parentId, string type)
        where T : Element, new()
    {
        var parent = ui.Find(By.AccessibilityId(parentId));

        // Element.Find currently searches the entire session, not the parent's subtree.
        var tree = WinappCli.InvokeJson("ui", "inspect", parent.Selector, ui.TargetFlag, ui.TargetValue, "--json", "-d", "8");
        var selectors = new List<string>();
        CollectSelectors(tree, type, selectors);
        Assert.HasCount(1, selectors, $"Expected one {type} below {parentId}; inspect={tree}.");
        return ui.Find<T>(By.Slug(selectors[0]));
    }

    private static void CollectSelectors(JsonElement node, string type, List<string> selectors, string? name = null)
    {
        if (node.ValueKind == JsonValueKind.Object)
        {
            if (node.TryGetProperty("type", out var controlType) && controlType.GetString() == type &&
                (name is null || (node.TryGetProperty("name", out var controlName) && controlName.GetString() == name)) &&
                node.TryGetProperty("selector", out var selector))
            {
                selectors.Add(selector.GetString()!);
            }

            foreach (var property in node.EnumerateObject())
            {
                CollectSelectors(property.Value, type, selectors, name);
            }
        }
        else if (node.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in node.EnumerateArray())
            {
                CollectSelectors(child, type, selectors, name);
            }
        }
    }

    private void WaitForScheduleReload()
    {
        Assert.IsNotNull(scheduleUpdate, "A real Settings edit must precede the native scheduling check.");
        Step("Waiting for the LightSwitch service's debounced settings reload");
        var result = WaitHelper.WaitForStable(
            () => scheduleUpdate.ReadNew(),
            text => text!.Contains("[LightSwitchSettings] Settings file stabilized, reloading.", StringComparison.Ordinal),
            timeoutMS: 20_000,
            pollIntervalMS: 250);
        Assert.IsTrue(result.Succeeded, $"The service did not reload the changed schedule. New logs:\n{result.LastObservation}");
        Assert.HasCount(1, ObserveService(), "The native scheduler must remain alive after loading settings.");
    }

    private bool HasSunTimes()
    {
        foreach (string id in new[] { "SunriseText_LightSwitch", "SunsetText_LightSwitch" })
        {
            var elements = ui.FindAll<Element>(By.AccessibilityId(id), 0);
            if (elements.Count != 1 || !elements[0].Displayed || string.IsNullOrWhiteSpace(elements[0].GetValue()))
            {
                return false;
            }
        }

        return true;
    }

    private string ReadLocationError()
    {
        var errors = ui.FindAll<Element>(By.AccessibilityId("LocationErrorText"), 0);
        return errors.Count == 1 && errors[0].Displayed ? errors[0].GetValue() : string.Empty;
    }

    private static bool TryReadTimeline(string? helpText, out TimelineState state)
    {
        state = default;
        if (string.IsNullOrWhiteSpace(helpText))
        {
            return false;
        }

        var parts = helpText.Split(';').Select(part => part.Split('=', 2)).Where(part => part.Length == 2)
            .ToDictionary(part => part[0], part => part[1], StringComparer.Ordinal);
        if (parts.TryGetValue("Start", out var start) && parts.TryGetValue("End", out var end) &&
            TimeSpan.TryParse(start, CultureInfo.InvariantCulture, out var light) &&
            TimeSpan.TryParse(end, CultureInfo.InvariantCulture, out var dark))
        {
            state = new TimelineState((int)light.TotalMinutes, (int)dark.TotalMinutes);
            return true;
        }

        return false;
    }

    private static int[] ObserveService()
    {
        var processes = Process.GetProcessesByName(ServiceProcess);
        try
        {
            return processes.Select(process => process.Id).ToArray();
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
    }

    private static bool IsSettingsWriteInProgress(Exception exception) => exception is IOException or JsonException;

    private void Step(string message) => context.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] {message}");

    internal readonly record struct TimelineState(int Start, int End);

    internal readonly record struct SunTimes(int Sunrise, int Sunset);

    private sealed class LogCursor
    {
        private readonly Dictionary<string, long> offsets = LogFiles().ToDictionary(path => path, path => new FileInfo(path).Length, StringComparer.OrdinalIgnoreCase);

        public string ReadNew()
        {
            var text = new System.Text.StringBuilder();
            foreach (string path in LogFiles())
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                if (offsets.TryGetValue(path, out long offset) && offset <= stream.Length)
                {
                    stream.Position = offset;
                }

                using var reader = new StreamReader(stream);
                text.AppendLine(reader.ReadToEnd());
            }

            return text.ToString();
        }

        private static IEnumerable<string> LogFiles()
        {
            string root = Path.Combine(SettingsConfigHelper.PowerToysSettingsRoot, "LightSwitch");
            return Directory.Exists(root) ? Directory.EnumerateFiles(root, "*.log", SearchOption.AllDirectories) : [];
        }
    }
}

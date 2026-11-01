// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.Awake.UITests;

internal sealed class AwakeUi(TestContext context)
{
    internal const string ProcessName = "PowerToys.Awake";
    internal const string Passive = "Keep using the selected power plan";
    internal const string Indefinite = "Keep awake indefinitely";
    internal const string Timed = "Keep awake for a time interval";
    internal const string Expirable = "Keep awake until expiration";
    internal const string TrayPassive = "Off (keep using the selected power plan)";
    internal const string TrayTimed = "Keep awake on interval";
    internal const string TrayExpiration = "Keep awake until expiration date and time";
    internal const string ExitEvent = @"Local\PowerToysAwakeExitEvent-c0d5e305-35fc-4fb5-83ec-f6070cfaf7fe";

    private static readonly string[] ChevronNames = ["hidden icon", "Notification Chevron", "NotificationChevron"];

    internal static string SettingsPath => Path.Combine(SettingsConfigHelper.PowerToysSettingsRoot, "Awake", "settings.json");

    internal Session Settings => Session.FromProcess("PowerToys.Settings");

    internal static void SeedSettings()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, new JsonObject
        {
            ["name"] = "Awake",
            ["version"] = "1.0",
            ["properties"] = new JsonObject
            {
                ["mode"] = 0,
                ["keepDisplayOn"] = false,
                ["intervalHours"] = 0,
                ["intervalMinutes"] = 1,
                ["expirationDateTime"] = DateTimeOffset.Now.AddDays(1).ToString("O", CultureInfo.InvariantCulture),
                ["customTrayTimes"] = new JsonObject(),
            },
        }.ToJsonString());
    }

    internal static IDisposable PreserveSettings()
    {
        StopProcesses();
        return SettingsConfigHelper.PreserveModuleSettings("Awake");
    }

    internal static void RestoreSettings(IDisposable? snapshot)
    {
        var failures = new List<Exception>();
        try
        {
            StopProcesses();
        }
        catch (Exception failure)
        {
            failures.Add(failure);
        }

        try
        {
            snapshot?.Dispose();
        }
        catch (Exception failure)
        {
            failures.Add(failure);
        }

        if (failures.Count > 0)
        {
            throw new AggregateException("Could not completely restore the Awake test settings.", failures);
        }
    }

    internal static void StopProcesses()
    {
        foreach (var name in new[] { "PowerToys.Settings", "PowerToys", ProcessName })
        {
            Assert.IsTrue(WindowControl.TryKillProcessTreeByNameAndWait(name, 15_000), $"Could not stop {name} before restoring Awake state.");
        }
    }

    internal void Step(string message) => context.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] {message}");

    internal void WaitForPassiveBaseline()
    {
        Step("Waiting for unrelated startup power requests to clear before testing passive Awake.");
        WaitForPowerState(false, false, timeoutMS: 180_000);
    }

    internal void Navigate()
    {
        Step("Navigating to Awake settings.");
        if (!Settings.Has(By.AccessibilityId("AwakeNavItem"), 500))
        {
            Settings.Find<NavigationViewItem>(By.AccessibilityId("SystemToolsNavItem")).Invoke(msPostAction: 0);
        }

        Settings.Find<NavigationViewItem>(By.AccessibilityId("AwakeNavItem")).Invoke(msPostAction: 0);
        Assert.IsTrue(Settings.Has(By.AccessibilityId("AwakeEnableSettingsCard"), 15_000), "Awake settings did not appear.");
    }

    internal ToggleSwitch EnableToggle() => Settings.Find<ToggleSwitch>(By.Name("Awake"));

    internal ComboBox ModeCombo() => Settings.Find<ComboBox>(By.Name("Mode"));

    internal ToggleSwitch DisplayToggle() => Settings.Find<ToggleSwitch>(By.Name("Keep screen on"));

    internal void Enable(bool enabled)
    {
        Step($"Setting Awake enabled={enabled} through Settings IPC.");
        SetToggle(EnableToggle(), enabled);
        WaitForProcess(enabled);
    }

    internal void SelectMode(string caption, int wireValue)
    {
        Step($"Selecting mode '{caption}'.");
        var combo = ModeCombo();
        if (combo.SelectedText != caption)
        {
            combo.Invoke(msPostAction: 0);
            var item = Settings.FindAll<Element>(By.Name(caption), 5_000)
                .FirstOrDefault(element => element.ControlType == "ListItem" && element.Name == caption &&
                    element.ClassName.EndsWith("ComboBoxItem", StringComparison.Ordinal) &&
                    element.Width > 0 && element.Height > 0 && element.Displayed);
            Assert.IsNotNull(item, $"The mode dropdown did not expose '{caption}'.");
            item.Invoke(msPostAction: 0);
        }

        Assert.IsTrue(combo.WaitForValue(caption, timeoutMS: 15_000), $"Selected mode did not become '{caption}'.");
        WaitForSettings(properties => properties["mode"]!.GetValue<int>() == wireValue, $"mode={wireValue}");
    }

    internal void SetDisplay(bool enabled)
    {
        Step($"Setting keep screen on={enabled}.");
        SetToggle(DisplayToggle(), enabled);
        WaitForSettings(properties => properties["keepDisplayOn"]!.GetValue<bool>() == enabled, $"keepDisplayOn={enabled}");
    }

    internal void SetPickerPart(string cardId, string partId, string expectedValue)
    {
        Step($"Opening {cardId} and selecting {partId}={expectedValue}.");
        var settings = Settings;
        var card = settings.Find<Element>(By.AccessibilityId(cardId));
        var tree = WinappCli.InvokeJson("ui", "inspect", card.Selector, settings.TargetFlag, settings.TargetValue, "--json", "-d", "8");
        var button = Nodes(tree).Single(node => node.TryGetProperty("automationId", out var id) && id.GetString() == "FlyoutButton");
        settings.Find<Button>(By.Slug(button.GetProperty("selector").GetString()!)).Invoke(msPostAction: 0);
        var part = settings.Find<Element>(By.AccessibilityId(partId));
        var choices = WinappCli.InvokeJson("ui", "inspect", part.Selector, settings.TargetFlag, settings.TargetValue, "--json", "-d", "2");
        var target = Nodes(choices).FirstOrDefault(node =>
            node.TryGetProperty("className", out var className) && className.GetString() == "LoopingSelectorItem" &&
            node.TryGetProperty("name", out var name) && new string(name.GetString()!.Where(char.IsDigit).ToArray()) == expectedValue);
        Assert.IsTrue(target.ValueKind == JsonValueKind.Object, $"{partId} did not expose {expectedValue}. Tree: {choices}");
        var item = settings.Find<Element>(By.Slug(target.GetProperty("selector").GetString()!));
        item.Invoke(msPostAction: 0);
        Assert.IsTrue(item.WaitForProperty("IsSelected", "True", 10_000), $"{partId} did not select {expectedValue}.");
        settings.Find<Button>(By.AccessibilityId("AcceptButton")).Invoke(msPostAction: 0);
        Assert.IsTrue(settings.Find<Element>(By.AccessibilityId(cardId)).Displayed, "The expiration card disappeared after accepting the picker.");
    }

    internal static void SetToggle(ToggleSwitch toggle, bool enabled)
    {
        if (toggle.IsOn != enabled)
        {
            toggle.Invoke(msPostAction: 0);
        }

        Assert.IsTrue(toggle.WaitForProperty("ToggleState", enabled ? "On" : "Off", 15_000), $"The switch did not become {enabled}.");
    }

    internal void WaitForProcess(bool expected)
    {
        Step($"Waiting for Awake process present={expected}.");
        var result = WaitHelper.WaitForStable(
            () =>
            {
                var processes = Process.GetProcessesByName(ProcessName);
                try
                {
                    return processes.Length;
                }
                finally
                {
                    foreach (var process in processes)
                    {
                        process.Dispose();
                    }
                }
            },
            count => count == (expected ? 1 : 0),
            20_000,
            3);
        Assert.IsTrue(result.Succeeded, $"Expected {(expected ? "one" : "no")} Awake process; last count={result.LastObservation}.");
    }

    internal void WaitForSettings(Func<JsonObject, bool> predicate, string description, int timeoutMS = 20_000)
    {
        Step($"Waiting for persisted Awake {description}.");
        var result = WaitHelper.WaitForStable(
            ReadProperties,
            properties => properties is not null && predicate(properties),
            timeoutMS,
            2,
            shouldRetryException: exception => exception is IOException or System.Text.Json.JsonException);
        Assert.IsTrue(result.Succeeded, $"Awake did not persist {description}. Last state: {result.LastObservation}; error: {result.LastException?.Message}");
    }

    internal static JsonObject ReadProperties()
    {
        return ReadSettings()["properties"] as JsonObject
            ?? throw new InvalidDataException("Awake settings do not contain a properties object.");
    }

    internal static void UpdateProperties(Action<JsonObject> update)
    {
        var settings = ReadSettings();
        var properties = settings["properties"] as JsonObject
            ?? throw new InvalidDataException("Awake settings do not contain a properties object.");
        update(properties);
        File.WriteAllText(SettingsPath, settings.ToJsonString());
    }

    internal static IEnumerable<JsonElement> Nodes(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Object)
        {
            yield return root;
            foreach (var property in root.EnumerateObject())
            {
                foreach (var node in Nodes(property.Value))
                {
                    yield return node;
                }
            }
        }
        else if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray())
            {
                foreach (var node in Nodes(item))
                {
                    yield return node;
                }
            }
        }
    }

    private static JsonObject ReadSettings()
    {
        using var stream = new FileStream(SettingsPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return JsonNode.Parse(stream) as JsonObject
            ?? throw new InvalidDataException("Awake settings are not a JSON object.");
    }

    internal static void WaitForPowerState(bool systemRequired, bool displayRequired, int timeoutMS = 20_000)
    {
        // Read the kernel's execution-state requests, not just the product's advertised tray state.
        var result = WaitHelper.WaitForStable(
            ReadPowerState,
            state => (state & 3) == ((systemRequired ? 1u : 0u) | (displayRequired ? 2u : 0u)),
            timeoutMS,
            3);
        Assert.IsTrue(result.Succeeded, $"Kernel execution state should be system={systemRequired}, display={displayRequired}; last flags=0x{result.LastObservation:X}.");
    }

    internal static uint ReadPowerState()
    {
        var status = CallNtPowerInformation(16, IntPtr.Zero, 0, out var state, sizeof(uint));
        if (status != 0)
        {
            throw new InvalidOperationException($"CallNtPowerInformation(SystemExecutionState) failed: NTSTATUS=0x{status:X8}.");
        }

        return state;
    }

    internal static string ReadPowerPlan()
    {
        using var process = Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "powercfg.exe"), "/query")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        }) ?? throw new InvalidOperationException("Could not start powercfg.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        Assert.IsTrue(process.WaitForExit(20_000), "powercfg /query timed out.");
        Assert.AreEqual(0, process.ExitCode, $"powercfg /query failed: {error.GetAwaiter().GetResult()}");
        return output.GetAwaiter().GetResult();
    }

    internal Element TrayIcon(Func<string, bool>? matches = null)
    {
        Step("Finding Awake's real notification icon.");
        var explorer = Session.FromProcess("explorer");
        var screen = System.Windows.Forms.SystemInformation.PrimaryMonitorSize;
        MouseHelper.MoveTo(screen.Width / 4, screen.Height / 2);
        Stopwatch? lastChevronClick = null;
        var result = WaitHelper.WaitForStable(
            () => explorer.FindAll<Element>(By.Name("PowerToys Awake"), 0)
                .FirstOrDefault(item => item.ControlType == "Button" && item.Width > 0 && item.Height > 0 &&
                    item.Displayed && (matches?.Invoke(item.Name) ?? true)),
            icon => icon is not null && WindowsFinder.ListByApp("explorer").Any(window =>
                window.ClassName is "Shell_TrayWnd" or "Shell_SecondaryTrayWnd" or "NotifyIconOverflowWindow" or "TopLevelWindowForOverflowXamlIsland" &&
                WindowControl.IsPointOwnedByWindow(new IntPtr(window.Hwnd), icon.X + (icon.Width / 2), icon.Y + (icon.Height / 2))),
            30_000,
            2,
            pollIntervalMS: 250,
            recover: _ =>
            {
                var overflow = WindowsFinder.ListByApp("explorer").FirstOrDefault(window =>
                    window.ClassName is "NotifyIconOverflowWindow" or "TopLevelWindowForOverflowXamlIsland" &&
                    !WindowHelper.IsWindowCloaked(new IntPtr(window.Hwnd)));
                if (overflow is not null)
                {
                    WindowControl.TryBringToForeground(new IntPtr(overflow.Hwnd));
                }
                else if (lastChevronClick is null || lastChevronClick.ElapsedMilliseconds > 3_000)
                {
                    var chevron = ChevronNames.SelectMany(name => explorer.FindAll<Element>(By.Name(name), 0))
                        .FirstOrDefault(item => item.ControlType == "Button" && item.Width > 0 && item.Height > 0 && item.Displayed);
                    if (chevron is not null)
                    {
                        MouseHelper.LeftClickAt(chevron.X + (chevron.Width / 2), chevron.Y + (chevron.Height / 2));
                        lastChevronClick = Stopwatch.StartNew();
                        MouseHelper.MoveTo(screen.Width / 4, screen.Height / 2);
                    }
                }
            },
            shouldRetryException: ShellMenu.IsTransientElementException);
        Assert.IsTrue(result.Succeeded, $"Awake's notification icon did not become visible with the expected state. {result.LastException?.Message}");
        return result.LastObservation!;
    }

    internal Session OpenTrayMenu(bool rightClick = true)
    {
        Step($"Opening Awake tray menu with {(rightClick ? "right" : "left")} click.");
        ShellMenu.Dismiss();
        Session? menu = null;
        for (var attempt = 1; attempt <= 3 && menu is null; attempt++)
        {
            var icon = TrayIcon();
            Step($"Clicking tray icon '{icon.Name}' at ({icon.X}, {icon.Y}), attempt {attempt}.");
            MouseHelper.MoveTo(icon.X + (icon.Width / 2), icon.Y + (icon.Height / 2));
            if (rightClick)
            {
                MouseHelper.RightClick();
            }
            else
            {
                MouseHelper.LeftClick();
            }

            // A crashed previous process can leave a ghost icon that Shell removes on hover.
            // Re-resolve only when no menu opened; never toggle an already-open menu.
            menu = ShellMenu.WaitForWindow(ShellMenu.ClassicWindowClassName, 5_000, ProcessName);
        }

        Assert.IsNotNull(menu, "Awake did not open its real tray menu.");
        var (left, top, right, bottom) = WindowHelper.GetWindowBounds(new IntPtr(menu.WindowHandle));
        Assert.IsTrue(right > left && bottom > top, "Awake's native menu has no visible bounds.");
        MouseHelper.MoveTo(left + ((right - left) / 2), top + ((bottom - top) / 2));
        return menu;
    }

    internal void TrayCommand(string caption)
    {
        Step($"Invoking Awake tray command '{caption}'.");
        var menu = OpenTrayMenu();
        var item = ShellMenu.FindVisibleMenuItem(menu, caption);
        Assert.IsNotNull(item, $"Awake tray menu is missing '{caption}'.");
        ClickMenuItem(menu, item);
    }

    internal static void ClickMenuItem(Session menu, Element item)
    {
        // Focusing the popup HWND dismisses Awake's MNS_AUTO_DISMISS menu. Keep its owner active.
        // The retained Shell tooltip can cover either side or the middle of the menu row.
        var padding = Math.Min(8, item.Width / 2);
        int[] candidates = [item.X + padding, item.X + item.Width - padding, item.X + (item.Width / 2)];
        var y = item.Y + (item.Height / 2);
        var ready = WaitHelper.WaitForStable(
            () =>
            {
                foreach (var candidate in candidates)
                {
                    if (WindowControl.IsPointOwnedByWindow(new IntPtr(menu.WindowHandle), candidate, y))
                    {
                        MouseHelper.MoveTo(candidate, y);
                        if (WindowControl.IsPointOwnedByWindow(new IntPtr(menu.WindowHandle), candidate, y))
                        {
                            return true;
                        }
                    }
                }

                return false;
            },
            owned => owned,
            10_000,
            2);
        Assert.IsTrue(ready.Succeeded, $"The native Awake menu has no unoccluded click point on row y={y} at x=[{string.Join(", ", candidates)}].");
        MouseHelper.LeftClick();
    }

    [DllImport("powrprof.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint CallNtPowerInformation(int informationLevel, IntPtr input, uint inputSize, out uint output, uint outputSize);
}

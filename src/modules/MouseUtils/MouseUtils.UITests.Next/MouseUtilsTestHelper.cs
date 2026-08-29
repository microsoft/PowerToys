// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MouseUtils.UITests;

internal static class MouseUtilsTestHelper
{
    private static readonly string[] ShortcutSeparators = [" + ", "+", " "];

    internal const string InputOutputNavItemId = "InputOutputNavItem";
    internal const string MouseUtilitiesNavItemId = "MouseUtilitiesNavItem";

    internal static void NavigateToMouseUtilities(UITestBase testBase)
    {
        Step(testBase, "Navigating to Mouse Utilities settings");
        if (!testBase.Session.Has(By.AccessibilityId(MouseUtilitiesNavItemId), 500))
        {
            testBase.Session.Find<NavigationViewItem>(By.AccessibilityId(InputOutputNavItemId), 5_000).Click(msPostAction: 500);
        }

        testBase.Session.Find<NavigationViewItem>(By.AccessibilityId(MouseUtilitiesNavItemId), 5_000).Click(msPostAction: 800);
    }

    internal static ToggleSwitch SetModuleEnabled(UITestBase testBase, string toggleId, bool enabled)
    {
        Step(testBase, $"Setting {toggleId} to {(enabled ? "On" : "Off")}");
        var expectedState = enabled ? "On" : "Off";
        ToggleSwitch? toggle = null;
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            toggle = testBase.Session.Find<ToggleSwitch>(By.AccessibilityId(toggleId), 10_000);
            var actualState = toggle.GetProperty("ToggleState");
            if (actualState.Equals(expectedState, StringComparison.OrdinalIgnoreCase))
            {
                return toggle;
            }

            var oppositeState = enabled ? "Off" : "On";
            if (!actualState.Equals(oppositeState, StringComparison.OrdinalIgnoreCase))
            {
                Step(testBase, $"{toggleId} returned unreadable ToggleState '{actualState}' on attempt {attempt}/3; reacquiring it");
                if (toggle.WaitForProperty("ToggleState", expectedState, 3_000))
                {
                    return toggle;
                }

                continue;
            }

            toggle.Invoke(msPostAction: 500);
            if (toggle.WaitForProperty("ToggleState", expectedState, 15_000))
            {
                return toggle;
            }

            Step(testBase, $"{toggleId} did not reach {expectedState} on attempt {attempt}/3; reacquiring it");
        }

        Assert.Fail($"{toggleId} did not reach {expectedState} after three coordinate-free attempts.");
        return toggle!;
    }

    internal static void EnsureModuleStateApplied(
        UITestBase testBase,
        string moduleName,
        bool enabled,
        Func<bool> waitForRuntimeState,
        string failureMessage)
    {
        var runnerProcesses = GetProcessStartTimes(PowerToysModule.Runner);
        var settingsProcesses = GetProcessStartTimes(PowerToysModule.PowerToysSettings);
        var settingsPipeRejected = HasUnsignedSettingsPipeRejection(runnerProcesses, settingsProcesses);

        if (!settingsPipeRejected)
        {
            if (waitForRuntimeState())
            {
                return;
            }

            runnerProcesses = GetProcessStartTimes(PowerToysModule.Runner);
            settingsProcesses = GetProcessStartTimes(PowerToysModule.PowerToysSettings);
            settingsPipeRejected = HasUnsignedSettingsPipeRejection(runnerProcesses, settingsProcesses);
        }

        if (!settingsPipeRejected)
        {
            Assert.Fail(failureMessage);
            return;
        }

        if (WaitForModuleEnabledSetting(moduleName, enabled))
        {
            Assert.IsTrue(waitForRuntimeState(), failureMessage);
            return;
        }

        var persistedState = WaitForReadableModuleEnabledSetting(moduleName);
        Assert.IsTrue(
            persistedState.HasValue,
            $"Could not read enabled.{moduleName} after the Settings pipe client was rejected.");
        Assert.AreEqual(
            !enabled,
            persistedState.Value,
            $"enabled.{moduleName} reached an unexpected state after the Settings pipe client was rejected.");

        Step(testBase, $"The Release runner rejected the test-signed Settings pipe client; seeding and restarting it to apply enabled.{moduleName}={enabled.ToString().ToLowerInvariant()}");
        testBase.RestartScope(enabled ? [moduleName] : []);
        Assert.IsTrue(
            waitForRuntimeState(),
            $"{failureMessage} The persisted state was not applied after restarting the runner.");
        NavigateToMouseUtilities(testBase);
    }

    internal static WindowControl.ProcessWindow WaitForWindowClass(string className, int timeoutMs = 10_000)
    {
        var result = WaitHelper.WaitForStable(
            () => WindowControl.EnumerateAllWindows().FirstOrDefault(window =>
                window.ClassName.Equals(className, StringComparison.OrdinalIgnoreCase)),
            window => window.Hwnd != IntPtr.Zero,
            timeoutMs,
            requiredConsecutiveMatches: 2,
            pollIntervalMS: 100);

        Assert.IsTrue(result.Succeeded, $"Top-level window class '{className}' did not appear within {timeoutMs} ms.");
        return result.LastObservation;
    }

    internal static Key[] ReadShortcut(UITestBase testBase, string groupId, int ordinal = 0)
    {
        Element? group = null;
        for (var attempt = 0; attempt < 12 && group is null; attempt++)
        {
            group = testBase.Session.FindAll<Element>(By.AccessibilityId(groupId), 0).FirstOrDefault();
            if (group is null)
            {
                MouseHelper.ScrollDown();
                Thread.Sleep(150);
            }
        }

        Assert.IsNotNull(group, $"Settings group '{groupId}' was not found after scrolling the Mouse Utilities page.");
        group.ScrollIntoView();
        var buttons = testBase.Session.FindAll<Button>(By.AccessibilityId("EditButton"), 5_000)
            .Where(button =>
                button.X >= group!.X &&
                button.X < group.X + group.Width &&
                button.Y >= group.Y &&
                button.Y < group.Y + group.Height)
            .OrderBy(button => button.Y)
            .ToList();

        Assert.IsTrue(
            buttons.Count > ordinal,
            $"Expected shortcut button {ordinal} inside '{groupId}', but found {buttons.Count}.");

        var shortcutText = buttons[ordinal].HelpText;
        var keys = ParseShortcutText(shortcutText);
        Assert.IsTrue(
            keys.Any(key => key is not (Key.LWin or Key.Ctrl or Key.Shift or Key.Alt)),
            $"Shortcut text '{shortcutText}' from '{groupId}' did not contain a main key.");
        Step(testBase, $"Shortcut from {groupId}: '{shortcutText}' => [{string.Join(", ", keys)}]");
        return keys;
    }

    internal static Key[] ParseShortcutText(string shortcutText)
    {
        var keys = new List<Key>();
        foreach (var raw in shortcutText.Split(ShortcutSeparators, StringSplitOptions.RemoveEmptyEntries))
        {
            var token = raw.Trim();
            Key? key = token.ToLowerInvariant() switch
            {
                "win" or "windows" => Key.LWin,
                "ctrl" or "control" => Key.Ctrl,
                "shift" => Key.Shift,
                "alt" => Key.Alt,
                _ when token.Length == 1 && token[0] is >= '0' and <= '9' =>
                    Enum.TryParse<Key>("Num" + token, out var number) ? number : null,
                _ when token.Length > 0 && char.IsLetter(token[0]) && Enum.TryParse<Key>(token, true, out var parsed) => parsed,
                _ => null,
            };

            if (key.HasValue)
            {
                keys.Add(key.Value);
            }
        }

        return keys.ToArray();
    }

    internal static void ReplaceModuleSettings(string moduleName, string settingsJson)
    {
        SettingsConfigHelper.UpdateModuleSettings(
            moduleName,
            settingsJson,
            current =>
            {
                var desired = JsonNode.Parse(settingsJson)?.AsObject()
                    ?? throw new InvalidOperationException($"Settings seed for '{moduleName}' is not a JSON object.");
                current.Clear();
                foreach (var property in desired)
                {
                    current[property.Key] = property.Value?.DeepClone();
                }
            });
    }

    internal static void RunWithClientAreaAnimationsEnabled(Action action)
    {
        const int spiGetClientAreaAnimation = 0x1042;
        const int spiSetClientAreaAnimation = 0x1043;
        var originalValue = 0;
        Assert.IsTrue(
            SystemParametersInfo(spiGetClientAreaAnimation, 0, ref originalValue, 0),
            "Could not read the Windows client-area animation setting.");

        Exception? actionFailure = null;
        try
        {
            if (originalValue == 0)
            {
                var enabled = 1;
                Assert.IsTrue(
                    SystemParametersInfo(spiSetClientAreaAnimation, 0, ref enabled, 0),
                    "Could not enable Windows client-area animations for the timing fixture.");
            }

            action();
        }
        catch (Exception ex)
        {
            actionFailure = ex;
        }

        var restored = true;
        var restoreError = 0;
        if (originalValue == 0)
        {
            var disabled = 0;
            restored = SystemParametersInfo(spiSetClientAreaAnimation, 0, ref disabled, 0);
            if (!restored)
            {
                restoreError = Marshal.GetLastWin32Error();
            }
        }

        if (actionFailure is not null)
        {
            if (!restored)
            {
                throw new AggregateException(
                    "The test action failed and the Windows client-area animation setting could not be restored.",
                    actionFailure,
                    new InvalidOperationException($"SystemParametersInfoW failed with Win32 error {restoreError}."));
            }

            ExceptionDispatchInfo.Capture(actionFailure).Throw();
        }

        Assert.IsTrue(restored, $"Could not restore the Windows client-area animation setting. Win32 error: {restoreError}.");
    }

    internal static IDisposable PreserveClientAreaAnimationsEnabled()
    {
        const int spiGetClientAreaAnimation = 0x1042;
        const int spiSetClientAreaAnimation = 0x1043;
        var originalValue = 0;
        Assert.IsTrue(
            SystemParametersInfo(spiGetClientAreaAnimation, 0, ref originalValue, 0),
            "Could not read the Windows client-area animation setting before module launch.");

        if (originalValue == 0)
        {
            var enabled = 1;
            Assert.IsTrue(
                SystemParametersInfo(spiSetClientAreaAnimation, 0, ref enabled, 0),
                "Could not enable Windows client-area animations before module launch.");
        }

        return new ClientAreaAnimationsSnapshot(originalValue);
    }

    internal static void Step(UITestBase testBase, string message) =>
        testBase.TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] {message}");

    private static Dictionary<int, DateTime> GetProcessStartTimes(PowerToysModule module)
    {
        var processes = new Dictionary<int, DateTime>();
        foreach (var process in Process.GetProcessesByName(SessionHelper.GetProcessName(module)))
        {
            using (process)
            {
                try
                {
                    processes[process.Id] = process.StartTime;
                }
                catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
                {
                }
            }
        }

        return processes;
    }

    private static bool HasUnsignedSettingsPipeRejection(
        IReadOnlyDictionary<int, DateTime> runnerProcesses,
        IReadOnlyDictionary<int, DateTime> settingsProcesses)
    {
        if (runnerProcesses.Count == 0 || settingsProcesses.Count == 0)
        {
            return false;
        }

        var runnerLogs = Path.Combine(SettingsConfigHelper.PowerToysSettingsRoot, "RunnerLogs");
        if (!Directory.Exists(runnerLogs))
        {
            return false;
        }

        foreach (var path in Directory.EnumerateFiles(runnerLogs, "runner-log*.log"))
        {
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream);
                while (reader.ReadLine() is { } line)
                {
                    if (line.Contains("Rejected unauthenticated Settings pipe client", StringComparison.Ordinal) &&
                        line.Contains("reason=not-microsoft-signed", StringComparison.Ordinal) &&
                        TryReadLogTimestamp(line, out var timestamp) &&
                        runnerProcesses.Any(process =>
                            timestamp >= process.Value.AddSeconds(-1) &&
                            line.Contains($"[p-{process.Key}]", StringComparison.Ordinal)) &&
                        settingsProcesses.Any(process =>
                            timestamp >= process.Value.AddSeconds(-1) &&
                            line.Contains($"pid={process.Key} ", StringComparison.Ordinal)))
                    {
                        return true;
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }

        return false;
    }

    private static bool TryReadLogTimestamp(string line, out DateTime timestamp)
    {
        const int timestampLength = 26;
        timestamp = default;
        return line.Length > timestampLength + 1 &&
               DateTime.TryParseExact(
                   line.AsSpan(1, timestampLength),
                   "yyyy-MM-dd HH:mm:ss.ffffff",
                   CultureInfo.InvariantCulture,
                   DateTimeStyles.AssumeLocal,
                   out timestamp);
    }

    private static bool WaitForModuleEnabledSetting(string moduleName, bool enabled) =>
        WaitHelper.WaitForStable(
            () => ReadModuleEnabledSetting(moduleName),
            value => value == enabled,
            5_000,
            requiredConsecutiveMatches: 2,
            pollIntervalMS: 100).Succeeded;

    private static bool? WaitForReadableModuleEnabledSetting(string moduleName)
    {
        var result = WaitHelper.WaitForStable(
            () => ReadModuleEnabledSetting(moduleName),
            value => value.HasValue,
            2_000,
            requiredConsecutiveMatches: 2,
            pollIntervalMS: 100);
        return result.Succeeded ? result.LastObservation : null;
    }

    private static bool? ReadModuleEnabledSetting(string moduleName)
    {
        try
        {
            var path = Path.Combine(SettingsConfigHelper.PowerToysSettingsRoot, "settings.json");
            var root = JsonNode.Parse(File.ReadAllText(path));
            return root?["enabled"]?[moduleName]?.GetValue<bool>();
        }
        catch
        {
            return null;
        }
    }

    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    private static extern bool SystemParametersInfo(int uiAction, int uiParam, ref int pvParam, int fWinIni);

    private sealed class ClientAreaAnimationsSnapshot(int originalValue) : IDisposable
    {
        private bool disposed;

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            if (originalValue == 0)
            {
                const int spiSetClientAreaAnimation = 0x1043;
                var disabled = 0;
                var restored = SystemParametersInfo(spiSetClientAreaAnimation, 0, ref disabled, 0);
                var error = restored ? 0 : Marshal.GetLastWin32Error();
                Assert.IsTrue(restored, $"Could not restore the Windows client-area animation setting after the test class. Win32 error: {error}.");
            }
        }
    }
}

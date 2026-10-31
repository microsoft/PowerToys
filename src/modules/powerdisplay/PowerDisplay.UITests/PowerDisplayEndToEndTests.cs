// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerDisplay.UITests;

[TestClass]
[DoNotParallelize]
[TestCategory("PowerDisplay")]
public sealed class PowerDisplayEndToEndTests : UITestBase
{
    private const string ProcessName = "PowerToys.PowerDisplay";
    private const string TerminateEvent = @"Local\PowerToysPowerDisplay-TerminateEvent-7b9c2e1f-8a5d-4c3e-9f6b-2a1d8c5e3b7a";
    private const string InitialSettings = """
        {
          "name": "PowerDisplay",
          "version": "1",
          "properties": {
            "activation_shortcut": { "win": true, "ctrl": true, "alt": false, "shift": true, "code": 80 },
            "restore_settings_on_startup": false,
            "show_system_tray_icon": true,
            "show_identify_monitors_button": true,
            "linked_levels_active": false,
            "monitors": []
          }
        }
        """;

    private static readonly List<IDisposable> Snapshots = [];
    private Key[] activationKeys = [];

    public PowerDisplayEndToEndTests()
        : base(PowerToysModule.PowerToysSettings, enableModules: ["PowerDisplay"])
    {
    }

    protected override bool ReuseScopeAcrossTests => true;

    private static string ModuleDirectory => Path.Combine(SettingsConfigHelper.PowerToysSettingsRoot, "PowerDisplay");

    private static string SettingsPath => Path.Combine(ModuleDirectory, "settings.json");

    [ClassInitialize]
    public static void PreserveState(TestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        StopProduct();
        AssertNoCrashMarkers();
        try
        {
            Snapshots.Add(SettingsConfigHelper.PreserveModuleSettings("PowerDisplay"));
            Snapshots.Add(SettingsConfigHelper.PreserveModuleSettings("LightSwitch"));
            Snapshots.Add(SettingsConfigHelper.PreserveFile(Path.Combine(ModuleDirectory, "profiles.json")));
            Snapshots.Add(SettingsConfigHelper.PreserveFile(Path.Combine(ModuleDirectory, "monitor_state.json")));
        }
        catch
        {
            RestoreSnapshots();
            throw;
        }
    }

    [ClassCleanup(ClassCleanupBehavior.EndOfClass)]
    public static void RestoreState()
    {
        try
        {
            StopProduct();
            AssertNoCrashMarkers();
        }
        finally
        {
            RestoreSnapshots();
        }
    }

    protected override void PrepareTestState()
    {
        StopProduct();
        AssertNoCrashMarkers();
        Directory.CreateDirectory(ModuleDirectory);
        File.WriteAllText(SettingsPath, InitialSettings);
        LaunchRunnerThroughExplorer();
    }

    [TestInitialize]
    public void OpenSettingsPage()
    {
        Step("Navigating to Power Display Settings");
        NavigateToModule();
        Assert.IsFalse(Session.IsElevated, "Settings must run as a standard user.");
        SetEnabled(true);
        Step("Reading the configured shortcut from Settings");
        var shortcut = Session.Find<Button>(By.AccessibilityId("EditButton")).HelpText;
        Assert.IsFalse(string.IsNullOrWhiteSpace(shortcut), "The activation shortcut is not accessible.");
        activationKeys = shortcut.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.ToUpperInvariant() switch
            {
                "WIN" or "WINDOWS" => Key.LWin,
                "CTRL" or "CONTROL" => Key.Ctrl,
                "SHIFT" => Key.Shift,
                "ALT" => Key.Alt,
                _ => Enum.Parse<Key>(part, ignoreCase: true),
            }).ToArray();
        Assert.IsTrue(activationKeys.Length >= 2, $"Invalid shortcut: '{shortcut}'.");
    }

    [TestCleanup]
    public async Task CloseFlyoutAsync()
    {
        await CaptureFailureArtifactsBeforeCleanupAsync();
        WindowControl.TryCloseByApp(ProcessName, window => window.Title == "Power Display");

        NavigateToModule();
        SetEnabled(true);
        ShellMenu.Dismiss();
    }

    [TestMethod]
    public void EnableStartsProcessAndShowsNotificationIcon()
    {
        Step("Disabling and re-enabling through the real Settings switch");
        SetEnabled(false);
        SetEnabled(true);
        var process = GetModuleProcesses().Single();
        using (process)
        {
            Assert.IsFalse(ElevationHelper.IsProcessElevated(process.Id), "Power Display must run without elevation.");
        }

        Step("Finding the actual Power Display icon in the Shell notification area");
        Assert.IsNotNull(PowerDisplayTray.FindIcon());
    }

    [TestMethod]
    public void ConfiguredShortcutOpensStableMonitorInventory()
    {
        var flyout = OpenFlyout();
        AssertStableInventory(flyout);
    }

    [TestMethod]
    public void NotificationIconOpensFlyoutAndEscapeHidesIt()
    {
        Step("Clicking the actual notification-area icon");
        var icon = PowerDisplayTray.FindIcon();
        MouseHelper.LeftClickAt(icon.X + (icon.Width / 2), icon.Y + (icon.Height / 2));
        PowerDisplayTray.ParkCursor();
        var flyout = WaitForFlyout();
        AssertStableInventory(flyout);
        CloseFlyout();
        AssertProcessState(true);
    }

    [TestMethod]
    public void RefreshRediscoversMonitorsAndReturnsToStableState()
    {
        var flyout = OpenFlyout();
        AssertStableInventory(flyout);
        Refresh(flyout);
    }

    [TestMethod]
    public void IdentifyShowsAnOverlayOnEveryConnectedDisplay()
    {
        var flyout = OpenFlyout();
        AssertStableInventory(flyout);
        var displays = MonitorInfo.GetAll();
        Assert.IsTrue(displays.Count > 0, "Windows must report at least one display.");
        var existingWindows = WindowsFinder.ListByApp(ProcessName).Select(window => window.Hwnd).ToHashSet();
        OpenDiscoveryMenu(flyout);
        Step($"Identifying all {displays.Count} Windows displays, including displays without DDC/CI");
        Session.FromProcess(ProcessName).Find<Element>(By.AccessibilityId("IdentifyButton")).Invoke(msPostAction: 0);

        // Overlays last only three seconds. Observe all HWNDs together before making any UIA calls.
        var overlays = WaitHelper.WaitForStable(
            () => WindowsFinder.ListByApp(ProcessName).Where(window => !existingWindows.Contains(window.Hwnd)).ToArray(),
            windows => windows is not null && windows.Length == displays.Count &&
                displays.All(display => windows.Count(window =>
                    MonitorInfo.GetFromWindow(new IntPtr(window.Hwnd)).DeviceName == display.DeviceName) == 1),
            2_500,
            pollIntervalMS: 50);
        Assert.IsTrue(overlays.Succeeded, $"Expected one identification overlay per display; observed {JsonSerializer.Serialize(overlays.LastObservation)}.");
        var path = EvidencePath("identify-desktop.png");
        flyout.Screenshot(path, captureScreen: true);
        TestContext.AddResultFile(path);
        Assert.IsTrue(
            WaitHelper.WaitForStable(
                () => WindowsFinder.ListByApp(ProcessName).Any(window => !existingWindows.Contains(window.Hwnd)),
                visible => !visible,
                10_000,
                2).Succeeded,
            "The identification overlays did not dismiss automatically.");
    }

    [TestMethod]
    public void FlyoutSettingsButtonNavigatesToPowerDisplay()
    {
        Step("Navigating away so the flyout must select the Power Display page");
        Session.Find<NavigationViewItem>(By.AccessibilityId("DashboardNavItem")).Click(msPostAction: 0);
        Assert.IsTrue(
            Session.WaitFor(() => !Session.Has(By.Name("Enable Power Display"), 0), timeoutMS: 10_000),
            "Dashboard navigation did not leave the Power Display settings page.");
        var flyout = OpenFlyout();
        Step("Opening Settings from the flyout");
        flyout.Find<Button>(By.AccessibilityId("SettingsBtn")).Invoke(msPostAction: 0);
        var settings = Session.FromProcess("PowerToys.Settings");
        Assert.IsTrue(
            settings.WaitForElement(By.Name("Enable Power Display"), 20_000),
            "The flyout did not open the Power Display settings page.");
        Assert.IsTrue(settings.Find<NavigationViewItem>(By.AccessibilityId("PowerDisplayNavItem")).Selected, "The wrong Settings page was selected.");
    }

    [TestMethod]
    public void BrightnessRoundTripsThroughMonitorRediscoveryWhenSupported()
    {
        var flyout = OpenFlyout();
        AssertStableInventory(flyout);
        var sliders = BrightnessSliders(flyout);
        if (sliders.Length == 0)
        {
            Step("HARDWARE NOT AVAILABLE: discovery exposed no brightness-capable monitor. Verified the stable inventory; a physical brightness write is not claimed.");
            return;
        }

        var slider = sliders.FirstOrDefault(control => control.IsEnabled);
        Assert.IsNotNull(slider, "Brightness controls must be enabled after discovery with linked levels disabled.");
        var monitorId = slider.AutomationId;
        Assert.IsTrue(
            ReadMonitors().Any(monitor => string.Equals(monitor.GetProperty("id").GetString(), monitorId, StringComparison.OrdinalIgnoreCase)),
            "The brightness slider did not identify a discovered monitor.");
        slider = FindMonitorBrightnessSlider(flyout, monitorId);
        var original = ReadBrightness(slider);
        var target = original <= 90 ? original + 5 : original - 5;
        try
        {
            Step($"Setting monitor '{monitorId}' brightness from {original} to {target}");
            slider.SetValue(target);
            Assert.IsTrue(slider.WaitForValue(target.ToString(CultureInfo.InvariantCulture), timeoutMS: 10_000), "The slider did not accept the requested brightness.");
            AssertSavedBrightness(monitorId, target);
            Refresh(flyout);
            Assert.AreEqual(target, ReadBrightness(FindMonitorBrightnessSlider(flyout, monitorId)), "Rediscovery did not read the requested brightness back from the monitor.");
        }
        finally
        {
            Step($"Restoring monitor '{monitorId}' brightness to {original}");
            if (!FlyoutVisible())
            {
                flyout = OpenFlyout();
            }

            FindMonitorBrightnessSlider(flyout, monitorId).SetValue(original);
            AssertSavedBrightness(monitorId, original);
            Refresh(flyout);
            Assert.AreEqual(original, ReadBrightness(FindMonitorBrightnessSlider(flyout, monitorId)), "The original hardware brightness was not restored.");
        }
    }

    [TestMethod]
    public void DisableExitsProcessAndUnregistersActivationShortcut()
    {
        OpenFlyout();
        CloseFlyout();
        Step("Disabling Power Display through Settings");
        SetEnabled(false);
        Step("Sending the configured shortcut while Power Display is disabled");
        KeyboardHelper.SendKeys(activationKeys);
        var observation = Stopwatch.StartNew();
        while (observation.ElapsedMilliseconds < 3_000)
        {
            Assert.IsFalse(FlyoutVisible(), "A disabled Power Display reopened its flyout.");
            Assert.AreEqual(0, ModuleProcessCount(), "The activation shortcut restarted a disabled module.");
            Thread.Sleep(100);
        }
    }

    private static Slider[] BrightnessSliders(Session flyout) =>
        flyout.FindAll<Slider>(By.Name("Brightness"), 0)
            .Where(control => control.Name == "Brightness").OrderBy(control => control.Y).ToArray();

    private static Slider FindMonitorBrightnessSlider(Session flyout, string monitorId)
    {
        var matches = flyout.FindAll<Slider>(By.AccessibilityId(monitorId))
            .Where(slider => string.Equals(slider.AutomationId, monitorId, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        Assert.AreEqual(1, matches.Length, $"Expected one brightness slider for exact monitor ID '{monitorId}'.");
        return matches[0];
    }

    private static double ReadBrightness(Slider slider) =>
        double.Parse(slider.GetValue(), CultureInfo.InvariantCulture);

    private static Process[] GetModuleProcesses() => Process.GetProcessesByName(ProcessName);

    private static int ModuleProcessCount()
    {
        var processes = GetModuleProcesses();
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
    }

    private static void AssertProcessState(bool expected)
    {
        var result = WaitHelper.WaitForStable(ModuleProcessCount, count => count == (expected ? 1 : 0), 20_000, 3, 200);
        Assert.IsTrue(result.Succeeded, $"Expected {(expected ? "one" : "no")} Power Display process, found {result.LastObservation}.");
    }

    private static bool FlyoutVisible() => WindowsFinder.ListByApp(ProcessName).Any(window => window.Title == "Power Display");

    private static JsonElement[] ReadMonitors()
    {
        using var stream = new FileStream(SettingsPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var document = JsonDocument.Parse(stream);
        return document.RootElement.GetProperty("properties").GetProperty("monitors").EnumerateArray().Select(monitor => monitor.Clone()).ToArray();
    }

    private static void AssertSavedBrightness(string monitorId, double expected)
    {
        var result = WaitHelper.WaitForStable(
            () =>
            {
                var path = Path.Combine(ModuleDirectory, "monitor_state.json");
                if (!File.Exists(path))
                {
                    return (double?)null;
                }

                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var document = JsonDocument.Parse(stream);
                var monitor = document.RootElement.GetProperty("monitors").EnumerateObject()
                    .FirstOrDefault(property => string.Equals(property.Name, monitorId, StringComparison.OrdinalIgnoreCase)).Value;
                return monitor.ValueKind == JsonValueKind.Object &&
                    monitor.TryGetProperty("brightness", out var brightness) && brightness.ValueKind == JsonValueKind.Number
                    ? brightness.GetDouble() : (double?)null;
            },
            brightness => brightness == expected,
            15_000,
            2,
            200,
            shouldRetryException: exception => exception is IOException or JsonException);
        Assert.IsTrue(result.Succeeded, $"Monitor '{monitorId}' did not finish applying brightness {expected}. Last state: {result.LastObservation}.");
    }

    private static void AssertNoCrashMarkers()
    {
        foreach (var name in new[] { "discovery.lock", "crash_detected.flag" })
        {
            Assert.IsFalse(File.Exists(Path.Combine(ModuleDirectory, name)), $"Power Display crash marker '{name}' exists; preserving it rather than bypassing quarantine.");
        }
    }

    private static void StopProduct()
    {
        var processes = GetModuleProcesses();
        try
        {
            if (processes.Length > 0)
            {
                var signaled = NamedEventHelper.WaitAndSignal(TerminateEvent, timeoutMS: 10_000);
                var failureMessage = signaled
                    ? "Power Display did not exit cooperatively; refusing to kill it during discovery."
                    : "Power Display's termination event was unavailable and its process did not exit; refusing to kill it during discovery.";
                foreach (var process in processes)
                {
                    Assert.IsTrue(process.WaitForExit(20_000), failureMessage);
                }
            }
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }

        foreach (var name in new[] { "PowerToys.Settings", "PowerToys" })
        {
            Assert.IsTrue(WindowControl.TryKillProcessTreeByNameAndWait(name), $"Could not stop {name}.");
        }
    }

    private static void RestoreSnapshots()
    {
        var failures = new List<Exception>();
        foreach (var snapshot in Enumerable.Reverse(Snapshots))
        {
            try
            {
                snapshot.Dispose();
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        }

        Snapshots.Clear();
        if (failures.Count > 0)
        {
            throw new AggregateException("Could not restore Power Display test state.", failures);
        }
    }

    private void LaunchRunnerThroughExplorer()
    {
        Step("Launching Runner through Explorer without inheriting the test host's elevated token");
        var executable = SessionHelper.GetExecutablePath(PowerToysModule.Runner);
        var launcher = Path.Combine(Path.GetTempPath(), $"PowerDisplay.UITests-{Guid.NewGuid():N}.vbs");
        var script =
            $"Set shell = CreateObject(\"WScript.Shell\"){Environment.NewLine}" +
            $"shell.CurrentDirectory = \"{Path.GetDirectoryName(executable)!.Replace("\"", "\"\"")}\"{Environment.NewLine}" +
            $"shell.Run \"\"\"{executable.Replace("\"", "\"\"")}\"\" --open-settings\", 0, False{Environment.NewLine}";
        try
        {
            // Explorer supplies its user token; the hidden launcher cannot steal foreground.
            File.WriteAllText(launcher, script, Encoding.Unicode);
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{launcher}\"",
                UseShellExecute = true,
            }) ?? throw new InvalidOperationException("Explorer did not accept the Power Display test launcher.");

            Assert.IsNotNull(
                WindowsFinder.WaitForWindowByApp(
                    "PowerToys.Settings",
                    window => window.Title.Contains("PowerToys", StringComparison.OrdinalIgnoreCase),
                    timeoutMS: 150_000),
                "The shell-launched Runner did not open Settings. Verify VBScript and the .vbs file association, and inspect Runner startup logs.");
        }
        finally
        {
            File.Delete(launcher);
        }
    }

    private void NavigateToModule()
    {
        if (!Session.Has(By.AccessibilityId("PowerDisplayNavItem"), 500))
        {
            Session.Find<NavigationViewItem>(By.AccessibilityId("InputOutputNavItem")).Click(msPostAction: 0);
        }

        Session.Find<NavigationViewItem>(By.AccessibilityId("PowerDisplayNavItem")).Click(msPostAction: 0);
        Assert.IsTrue(Session.WaitForElement(By.Name("Enable Power Display"), 20_000), "Power Display Settings did not load.");
    }

    private void SetEnabled(bool enabled)
    {
        var toggle = Session.Find<ToggleSwitch>(By.Name("Enable Power Display"));
        if (toggle.IsOn != enabled)
        {
            Step($"Invoking the Settings enable switch: {enabled}");
            toggle.Invoke(msPostAction: 0);
            if (enabled)
            {
                Step("Accepting Power Display's monitor-discovery warning through its Enable button");
                Assert.IsTrue(Session.WaitForElement(By.AccessibilityId("WarningBody"), 10_000), "Enabling Power Display did not show its safety confirmation.");
                var enable = Session.Find<Button>(By.AccessibilityId("PrimaryButton"));
                Assert.AreEqual("Enable", enable.Name, "The discovery warning exposed an unexpected confirmation action.");
                enable.Invoke(msPostAction: 0);
                Assert.IsTrue(enable.WaitForGone(10_000), "The monitor-discovery warning did not close.");
            }
        }

        Assert.IsTrue(toggle.WaitForProperty("ToggleState", enabled ? "On" : "Off", 10_000), "The Settings switch did not change.");
        AssertProcessState(enabled);
    }

    private Session OpenFlyout()
    {
        Step("Opening the flyout with the configured activation shortcut");
        ShellMenu.Dismiss();
        PowerDisplayTray.ParkCursor();
        for (var attempt = 0; attempt < 3 && !FlyoutVisible(); attempt++)
        {
            KeyboardHelper.SendKeys(activationKeys);
            if (WaitHelper.WaitForStable(FlyoutVisible, visible => visible, 5_000).Succeeded)
            {
                break;
            }
        }

        return WaitForFlyout();
    }

    private Session WaitForFlyout()
    {
        var flyout = WindowsFinder.WaitForWindowByApp(ProcessName, window => window.Title == "Power Display", 10_000);
        Assert.IsNotNull(flyout, "Power Display's flyout did not appear.");
        return flyout;
    }

    private void CloseFlyout()
    {
        Step("Dismissing the flyout with Escape");
        var flyout = WaitForFlyout();
        Assert.IsTrue(
            WindowControl.WaitForForeground(new IntPtr(flyout.WindowHandle), 10_000),
            $"Cannot send Escape to the flyout. Foreground: {WindowControl.GetForegroundWindowInfo()}.");
        KeyboardHelper.SendKeys(Key.Esc);
        Assert.IsTrue(WaitHelper.WaitForStable(FlyoutVisible, visible => !visible, 10_000, 2).Succeeded, "Escape did not hide the flyout.");
    }

    private void AssertStableInventory(Session flyout)
    {
        Step("Waiting for discovery to finish and expose monitors or the informational empty state");
        var result = WaitHelper.WaitForStable(
            () => flyout.Has(By.AccessibilityId("ScanningMonitorsTextBlock"), 0)
                ? "scanning"
                : flyout.Has(By.AccessibilityId("NoMonitorsInfoBar"), 0) ? "empty"
                : flyout.Has(By.AccessibilityId("MonitorsRepeater"), 0) ? "monitors" : "unready",
            state => state is "empty" or "monitors",
            60_000,
            2,
            200);
        Assert.IsTrue(result.Succeeded, $"Discovery did not reach a stable state: {result.LastObservation}.");
        var monitors = ReadMonitors();
        if (result.LastObservation == "empty")
        {
            Assert.AreEqual(0, monitors.Length, "The flyout incorrectly reports no monitors.");
            Assert.IsTrue(flyout.Has(By.Name("No monitors detected")), "The empty state did not explain that no monitors were detected.");
        }
        else
        {
            Assert.IsTrue(monitors.Length > 0, "The flyout did not publish its discovered monitor inventory.");
            foreach (var monitor in monitors)
            {
                Assert.IsTrue(flyout.Has(By.Name(monitor.GetProperty("name").GetString()!)), $"A discovered monitor is missing: {monitor}.");
            }
        }

        Step($"Stable discovery: {result.LastObservation}; {monitors.Length} controllable monitors");
        var path = EvidencePath("monitor-inventory.json");
        File.WriteAllText(path, JsonSerializer.Serialize(monitors));
        TestContext.AddResultFile(path);
    }

    private void OpenDiscoveryMenu(Session flyout)
    {
        Step("Opening the flyout's discovery menu");
        flyout.Find<Button>(By.AccessibilityId("RefreshMenuButton")).Invoke(msPostAction: 0);
    }

    private void Refresh(Session flyout)
    {
        OpenDiscoveryMenu(flyout);
        var lastWrite = File.GetLastWriteTimeUtc(SettingsPath);
        Step("Invoking Refresh and waiting for a newly published discovery result");
        Session.FromProcess(ProcessName).Find<Element>(By.AccessibilityId("RefreshButton")).Invoke(msPostAction: 0);
        Assert.IsTrue(
            WaitHelper.WaitForStable(
                () => File.GetLastWriteTimeUtc(SettingsPath),
                write => write > lastWrite,
                60_000).Succeeded,
            "Refresh did not publish a new monitor inventory.");
        AssertStableInventory(flyout);
        AssertProcessState(true);
    }

    private string EvidencePath(string name)
    {
        var directory = Path.Combine(TestContext.TestRunResultsDirectory!, TestContext.TestName!);
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, name);
    }

    private void Step(string message) => TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] {message}");
}

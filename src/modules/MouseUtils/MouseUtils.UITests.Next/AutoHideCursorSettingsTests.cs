// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MouseUtils.UITests;

[TestClass]
public class AutoHideCursorSettingsTests : UITestBase
{
    private const string ModuleName = "AutoHideCursor";
    private const string WorkerProcessName = "PowerToys.AutoHideCursor";
    private const string GroupId = "MouseUtils_AutoHideCursorTestId";
    private const string ModuleToggleId = "MouseUtils_AutoHideCursorToggleId";
    private const string SettingsExpanderId = "MouseUtilsAutoHideCursorSettingsExpander";
    private const string HideOnTypingToggleId = "MouseUtils_AutoHideCursorHideOnTypingToggleId";
    private const string HideOnIdleToggleId = "MouseUtils_AutoHideCursorHideOnIdleToggleId";
    private const string IdleDelayId = "MouseUtils_AutoHideCursorIdleDelayId";
    private const int SpiSetCursors = 0x57;
    private static readonly IDisposable ModuleSettings = SettingsConfigHelper.PreserveModuleSettings(ModuleName);

    static AutoHideCursorSettingsTests()
    {
    }

    public AutoHideCursorSettingsTests()
        : base(PowerToysModule.PowerToysSettings, enableModules: new[] { ModuleName })
    {
    }

    [ClassCleanup]
    public static void RestoreModuleSettings() => ModuleSettings.Dispose();

    [TestCleanup]
    public async Task RestoreCursorAndDisableModule()
    {
        await CaptureFailureArtifactsBeforeCleanupAsync();

        try
        {
            if (Session.Has(By.AccessibilityId(ModuleToggleId), 500))
            {
                var toggle = Session.Find<ToggleSwitch>(By.AccessibilityId(ModuleToggleId), 500);
                if (toggle.IsOn)
                {
                    toggle.Toggle(false);
                    toggle.WaitForProperty("ToggleState", "Off", 5_000);
                }
            }
        }
        catch
        {
            // The base cleanup will stop the test-owned Runner if Settings is no longer reachable.
        }
        finally
        {
            WaitForWorkerState(expectedRunning: false, timeoutMs: 5_000);
            SystemParametersInfo(SpiSetCursors, 0, IntPtr.Zero, 0);
        }
    }

    protected override void PrepareTestState()
    {
        MouseUtilsTestHelper.ReplaceModuleSettings(
            ModuleName,
            CreateSettings(hideOnTyping: true, hideOnIdle: false, idleDelayMs: 5000));
    }

    [TestMethod]
    [TestCategory("MouseUtils")]
    [TestCategory("AutoHideCursor")]
    public void SectionNavigationAndModuleLifecycleAreAvailable()
    {
        MouseUtilsTestHelper.NavigateToMouseUtilities(this);

        var group = Session.Find<Element>(By.AccessibilityId(GroupId), 10_000);
        Assert.IsTrue(group.Displayed, "Auto Hide Cursor settings group was not visible.");
        AssertWorkerState(expectedRunning: true);

        MouseUtilsTestHelper.SetModuleEnabled(this, ModuleToggleId, false);
        AssertWorkerState(expectedRunning: false);
        var expander = Session.Find<Element>(By.AccessibilityId(SettingsExpanderId), 5_000);
        Assert.IsFalse(expander.IsEnabled, "Activation settings should be disabled with the module.");

        MouseUtilsTestHelper.SetModuleEnabled(this, ModuleToggleId, true);
        AssertWorkerState(expectedRunning: true);
        expander = Session.Find<Element>(By.AccessibilityId(SettingsExpanderId), 5_000);
        Assert.IsTrue(expander.IsEnabled, "Activation settings should be enabled with the module.");
    }

    [TestMethod]
    [TestCategory("MouseUtils")]
    [TestCategory("AutoHideCursor")]
    public void TriggerTogglesPersistIndependentlyAndControlWorker()
    {
        OpenSettings();
        AssertToggleState(HideOnTypingToggleId, expectedOn: true);
        AssertToggleState(HideOnIdleToggleId, expectedOn: false);
        AssertWorkerState(expectedRunning: true);

        SetToggleState(HideOnTypingToggleId, enabled: false);
        AssertWorkerState(expectedRunning: false);

        SetToggleState(HideOnIdleToggleId, enabled: true);
        AssertWorkerState(expectedRunning: true);

        RestartScope();
        OpenSettings();
        AssertToggleState(HideOnTypingToggleId, expectedOn: false);
        AssertToggleState(HideOnIdleToggleId, expectedOn: true);
        AssertWorkerState(expectedRunning: true);
    }

    [TestMethod]
    [TestCategory("MouseUtils")]
    [TestCategory("AutoHideCursor")]
    public void IdleDelayEnabledStateAndBoundaryValuesPersist()
    {
        OpenSettings();

        var idleDelay = Session.Find<NumberBox>(By.AccessibilityId(IdleDelayId), 5_000);
        Assert.IsFalse(idleDelay.IsEnabled, "Idle delay should be disabled until hide-on-idle is enabled.");

        SetToggleState(HideOnIdleToggleId, enabled: true);
        idleDelay = Session.Find<NumberBox>(By.AccessibilityId(IdleDelayId), 5_000);
        Assert.IsTrue(idleDelay.IsEnabled, "Idle delay should be enabled with hide-on-idle.");

        idleDelay.SetValue(1);
        AssertIdleDelaySetting(1000);
        Session.Find<NumberBox>(By.AccessibilityId(IdleDelayId), 5_000).SetValue(60);
        AssertIdleDelaySetting(60000);

        RestartScope();
        OpenSettings();
        idleDelay = Session.Find<NumberBox>(By.AccessibilityId(IdleDelayId), 5_000);
        Assert.IsTrue(idleDelay.IsEnabled, "Persisted hide-on-idle should keep the delay enabled.");
        AssertIdleDelaySetting(60000);
    }

    private void OpenSettings()
    {
        MouseUtilsTestHelper.NavigateToMouseUtilities(this);
        MouseUtilsTestHelper.SetModuleEnabled(this, ModuleToggleId, true);

        if (!Session.Has(By.AccessibilityId(HideOnTypingToggleId), 500))
        {
            Session.Find<Element>(By.AccessibilityId(SettingsExpanderId), 5_000).Invoke(msPostAction: 500);
        }

        Assert.IsTrue(Session.Has(By.AccessibilityId(HideOnTypingToggleId), 5_000), "Hide-on-typing toggle was not available.");
        Assert.IsTrue(Session.Has(By.AccessibilityId(HideOnIdleToggleId), 5_000), "Hide-on-idle toggle was not available.");
        Assert.IsTrue(Session.Has(By.AccessibilityId(IdleDelayId), 5_000), "Idle delay control was not available.");
    }

    private void SetToggleState(string accessibilityId, bool enabled)
    {
        var toggle = Session.Find<ToggleSwitch>(By.AccessibilityId(accessibilityId), 5_000);
        toggle.Toggle(enabled);
        Assert.IsTrue(
            toggle.WaitForProperty("ToggleState", enabled ? "On" : "Off", 10_000),
            $"{accessibilityId} did not reach the expected {(enabled ? "On" : "Off")} state.");
    }

    private void AssertToggleState(string accessibilityId, bool expectedOn)
    {
        var toggle = Session.Find<ToggleSwitch>(By.AccessibilityId(accessibilityId), 5_000);
        Assert.AreEqual(
            expectedOn,
            toggle.IsOn,
            $"{accessibilityId} did not have the expected persisted state.");
    }

    private static void AssertIdleDelaySetting(int expectedMilliseconds)
    {
        var result = WaitHelper.WaitForStable(
            ReadIdleDelaySetting,
            actual => actual == expectedMilliseconds,
            timeoutMS: 10_000,
            requiredConsecutiveMatches: 2,
            pollIntervalMS: 200);
        Assert.IsTrue(
            result.Succeeded,
            $"Idle delay did not persist as {expectedMilliseconds} ms. Last observed value: {result.LastObservation}.");
    }

    private static int ReadIdleDelaySetting()
    {
        var settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft",
            "PowerToys",
            ModuleName,
            "settings.json");
        if (!File.Exists(settingsPath))
        {
            return -1;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(settingsPath));
        return document.RootElement
            .GetProperty("properties")
            .GetProperty("idle_delay_ms")
            .GetProperty("value")
            .GetInt32();
    }

    private static void AssertWorkerState(bool expectedRunning)
    {
        var result = WaitForWorkerState(expectedRunning, 15_000);
        Assert.IsTrue(
            result.Succeeded,
            $"Auto Hide Cursor worker was expected to be {(expectedRunning ? "running" : "stopped")}.");
    }

    private static WaitHelper.StableWaitResult<bool> WaitForWorkerState(bool expectedRunning, int timeoutMs) =>
        WaitHelper.WaitForStable(
            IsWorkerRunning,
            actual => actual == expectedRunning,
            timeoutMS: timeoutMs,
            requiredConsecutiveMatches: 2,
            pollIntervalMS: 250);

    private static bool IsWorkerRunning()
    {
        var processes = Process.GetProcessesByName(WorkerProcessName);
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
    }

    private static string CreateSettings(bool hideOnTyping, bool hideOnIdle, int idleDelayMs) => $$"""
        {
          "name": "AutoHideCursor",
          "version": "1.0",
          "properties": {
            "hide_on_typing": { "value": {{hideOnTyping.ToString().ToLowerInvariant()}} },
            "hide_on_idle": { "value": {{hideOnIdle.ToString().ToLowerInvariant()}} },
            "idle_delay_ms": { "value": {{idleDelayMs}} }
          }
        }
        """;

    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    private static extern bool SystemParametersInfo(int uiAction, int uiParam, IntPtr pvParam, int fWinIni);
}

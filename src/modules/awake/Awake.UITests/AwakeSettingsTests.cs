// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Globalization;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.Awake.UITests;

[TestClass]
[TestCategory("Awake")]
public sealed class AwakeSettingsTests : UITestBase
{
    private static readonly string[] DefaultTrayIntervals = ["30 minutes", "1 hour", "2 hours"];
    private static IDisposable? snapshot;
    private AwakeUi ui = null!;

    public AwakeSettingsTests()
        : base(PowerToysModule.PowerToysSettings, enableModules: ["Awake"])
    {
    }

    protected override IReadOnlyList<string> StaleProcessNames => ["PowerToys.Settings", "PowerToys", AwakeUi.ProcessName];

    protected override void PrepareTestState() => AwakeUi.SeedSettings();

    [ClassInitialize]
    public static void PrepareSettings(TestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        snapshot = AwakeUi.PreserveSettings();
    }

    [ClassCleanup(ClassCleanupBehavior.EndOfClass)]
    public static void RestoreSettings() => AwakeUi.RestoreSettings(snapshot);

    [TestInitialize]
    public void NavigateToAwake()
    {
        ui = new AwakeUi(TestContext);
        ui.Navigate();
        ui.WaitForProcess(true);
        ui.WaitForPassiveBaseline();
    }

    [TestCleanup]
    public async Task DismissMenus()
    {
        await CaptureFailureArtifactsBeforeCleanupAsync();
        ShellMenu.Dismiss();
        if (NamedEventHelper.Exists(AwakeUi.ExitEvent))
        {
            Assert.IsTrue(NamedEventHelper.TrySignal(AwakeUi.ExitEvent), "Could not gracefully stop Awake during cleanup.");
            ui.WaitForProcess(false);
        }
    }

    [TestMethod]
    public void EnableDisableControlsTheRuntime()
    {
        ui.SelectMode(AwakeUi.Indefinite, 1);
        AwakeUi.WaitForPowerState(true, false);
        ui.Enable(false);
        Assert.IsFalse(ui.ModeCombo().IsEnabled, "Disabled Awake must disable mode configuration.");
        Assert.IsFalse(ui.DisplayToggle().IsEnabled, "Disabled Awake must disable display configuration.");
        AwakeUi.WaitForPowerState(false, false);
        ui.Enable(true);
        Assert.IsTrue(ui.ModeCombo().IsEnabled, "Enabling Awake must enable its mode selector.");
        AwakeUi.WaitForPowerState(true, false);
        ui.SelectMode(AwakeUi.Passive, 0);
        AwakeUi.WaitForPowerState(false, false);
    }

    [TestMethod]
    public void IndefiniteAndDisplayModesReleaseRequestsWithoutChangingPowerPlan()
    {
        var powerPlan = AwakeUi.ReadPowerPlan();
        Assert.IsFalse(ui.DisplayToggle().IsEnabled, "Passive mode must not allow keeping the screen on.");
        AwakeUi.WaitForPowerState(false, false);
        ui.SelectMode(AwakeUi.Indefinite, 1);
        AwakeUi.WaitForPowerState(true, false);
        ui.TrayIcon(name => name.Contains("Indefinite", StringComparison.Ordinal) && name.Contains("Screen: Off", StringComparison.Ordinal));
        ShellMenu.Dismiss();
        ui.SetDisplay(true);
        AwakeUi.WaitForPowerState(true, true);
        ui.TrayIcon(name => name.Contains("Screen: On", StringComparison.Ordinal));
        ShellMenu.Dismiss();
        ui.SetDisplay(false);
        AwakeUi.WaitForPowerState(true, false);
        ui.SelectMode(AwakeUi.Passive, 0);
        AwakeUi.WaitForPowerState(false, false);
        Assert.IsFalse(ui.DisplayToggle().IsEnabled, "Returning to passive must disable display configuration.");
        Assert.AreEqual(powerPlan, AwakeUi.ReadPowerPlan(), "Awake must not alter the active power plan or its settings.");
    }

    [TestMethod]
    public void TimedModeEditsHoursAndMinutesAndHidesExpirationControls()
    {
        ui.SelectMode(AwakeUi.Timed, 2);
        var interval = ui.Settings.Find<Element>(By.AccessibilityId("AwakeIntervalSettingsCard"));
        var hours = interval.Find<TextBox>(By.Name("Hours"));
        var minutes = interval.Find<TextBox>(By.Name("Minutes"));
        ui.Step("Editing the interval to 1 hour and 2 minutes.");
        hours.SetText("1");
        minutes.Focus();
        minutes.SetText("2");
        hours.Focus();
        ui.WaitForSettings(properties => properties["intervalHours"]!.GetValue<int>() == 1 && properties["intervalMinutes"]!.GetValue<int>() == 2, "interval=1h 2m");
        AwakeUi.WaitForPowerState(true, false);
        Assert.IsFalse(ui.Settings.Has(By.AccessibilityId("AwakeExpirationSettingsExpanderDate"), 500), "Timed mode must hide expiration controls.");
        ui.SelectMode(AwakeUi.Passive, 0);
        Assert.IsFalse(ui.Settings.Has(By.AccessibilityId("AwakeIntervalSettingsCard"), 500), "Passive mode must hide interval controls.");
        AwakeUi.WaitForPowerState(false, false);
    }

    [TestMethod]
    public void IntegratedTimerReturnsToPassiveWithoutStoppingAwake()
    {
        ui.SelectMode(AwakeUi.Timed, 2);
        ui.SetDisplay(true);
        AwakeUi.WaitForPowerState(true, true);
        var activeTime = Stopwatch.StartNew();
        ui.WaitForSettings(properties => properties["mode"]!.GetValue<int>() == 0, "timer completion returned to passive", 80_000);
        Assert.IsTrue(activeTime.Elapsed >= TimeSpan.FromSeconds(45), $"The one-minute integrated timer ended prematurely after {activeTime.Elapsed}.");
        Assert.IsTrue(ui.ModeCombo().WaitForValue(AwakeUi.Passive, timeoutMS: 15_000), "The Settings mode selector did not reflect timer completion.");
        AwakeUi.WaitForPowerState(false, false);
        ui.WaitForProcess(true);
    }

    [TestMethod]
    public void ExpirableModeEditsDateAndTimeAndAppliesDisplaySetting()
    {
        ui.SelectMode(AwakeUi.Expirable, 3);
        Assert.IsTrue(ui.Settings.Has(By.AccessibilityId("AwakeExpirationSettingsExpanderDate"), 5_000), "Expirable mode must expose the end date picker.");
        Assert.IsTrue(ui.Settings.Has(By.AccessibilityId("AwakeExpirationSettingsExpanderTime"), 5_000), "Expirable mode must expose the end time picker.");
        var original = DateTimeOffset.Parse(AwakeUi.ReadProperties()["expirationDateTime"]!.GetValue<string>(), CultureInfo.InvariantCulture);
        ui.SetPickerPart("AwakeExpirationSettingsExpanderDate", "YearLoopingSelector", (original.Year + 1).ToString(CultureInfo.InvariantCulture));
        ui.WaitForSettings(
            properties => DateTimeOffset.Parse(properties["expirationDateTime"]!.GetValue<string>(), CultureInfo.InvariantCulture).Date == original.AddYears(1).Date,
            "edited expiration date");
        var expectedHour = (original.Hour + 1) % 24;
        ui.SetPickerPart("AwakeExpirationSettingsExpanderTime", "HourLoopingSelector", expectedHour.ToString(CultureInfo.InvariantCulture));
        ui.WaitForSettings(
            properties =>
            {
                var expiration = DateTimeOffset.Parse(properties["expirationDateTime"]!.GetValue<string>(), CultureInfo.InvariantCulture);
                return expiration.Date == original.AddYears(1).Date && expiration.Hour == expectedHour && expiration.Minute == original.Minute;
            },
            "edited expiration time");
        Assert.IsFalse(ui.Settings.Has(By.AccessibilityId("AwakeIntervalSettingsCard"), 500), "Expirable mode must hide duration controls.");
        ui.WaitForSettings(properties => DateTimeOffset.Parse(properties["expirationDateTime"]!.GetValue<string>(), CultureInfo.InvariantCulture) > DateTimeOffset.Now, "future expiration");
        AwakeUi.WaitForPowerState(true, false);
        ui.TrayIcon(name => name.Contains("Until", StringComparison.Ordinal));
        ShellMenu.Dismiss();
        ui.SetDisplay(true);
        AwakeUi.WaitForPowerState(true, true);
        ui.SelectMode(AwakeUi.Passive, 0);
        AwakeUi.WaitForPowerState(false, false);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void TrayLeftAndRightClickExposeIntegratedActions(bool rightClick)
    {
        var menu = ui.OpenTrayMenu(rightClick);
        var names = ShellMenu.ReadMenuNames(menu);
        foreach (var caption in new[] { AwakeUi.TrayTimed, AwakeUi.TrayExpiration, AwakeUi.Indefinite, AwakeUi.TrayPassive, "Keep screen on" })
        {
            CollectionAssert.Contains(names.ToArray(), caption, $"Awake's integrated tray menu is missing '{caption}'.");
        }

        CollectionAssert.DoesNotContain(names.ToArray(), "Exit", "Runner-owned Awake must not offer standalone Exit.");
        var expiration = ShellMenu.FindVisibleMenuItem(menu, AwakeUi.TrayExpiration);
        Assert.IsNotNull(expiration);
        Assert.IsFalse(expiration.IsEnabled, "Expiration must be configured in Settings, not through a disabled tray action.");
    }

    [TestMethod]
    public void TrayModesDisplayAndDefaultIntervalsUpdateSettingsLive()
    {
        ui.TrayCommand(AwakeUi.Indefinite);
        ui.WaitForSettings(properties => properties["mode"]!.GetValue<int>() == 1, "tray indefinite mode");
        AwakeUi.WaitForPowerState(true, false);
        ui.TrayCommand("Keep screen on");
        ui.WaitForSettings(properties => properties["keepDisplayOn"]!.GetValue<bool>(), "tray display on");
        AwakeUi.WaitForPowerState(true, true);
        var menu = ui.OpenTrayMenu();
        var timed = ShellMenu.FindVisibleMenuItem(menu, AwakeUi.TrayTimed);
        Assert.IsNotNull(timed);
        AwakeUi.ClickMenuItem(menu, timed);
        var submenu = ShellMenu.WaitForSubmenu(menu, "30 minutes", 10_000);
        Assert.IsNotNull(submenu, "Awake did not open the default interval submenu.");
        CollectionAssert.AreEquivalent(DefaultTrayIntervals, ShellMenu.ReadMenuNames(submenu).ToArray());
        var interval = ShellMenu.FindVisibleMenuItem(submenu, "30 minutes");
        Assert.IsNotNull(interval, "The default interval submenu is missing 30 minutes.");
        AwakeUi.ClickMenuItem(submenu, interval);
        ui.WaitForSettings(
            properties => properties["mode"]!.GetValue<int>() == 2 && properties["intervalHours"]!.GetValue<int>() == 0 &&
                properties["intervalMinutes"]!.GetValue<int>() == 30,
            "tray interval=30 minutes");
        AwakeUi.WaitForPowerState(true, true);
        ui.TrayCommand(AwakeUi.TrayPassive);
        ui.WaitForSettings(properties => properties["mode"]!.GetValue<int>() == 0, "tray passive mode");
        AwakeUi.WaitForPowerState(false, false);
        ui.WaitForProcess(true);
    }
}

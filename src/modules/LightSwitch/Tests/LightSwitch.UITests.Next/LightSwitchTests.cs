// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.LightSwitch.UITests;

[TestClass]
[TestCategory("LightSwitch")]
[DoNotParallelize]
public sealed class LightSwitchTests : UITestBase
{
    private static TestState? originalState;
    private TestHelper page = null!;

    public LightSwitchTests()
        : base(PowerToysModule.PowerToysSettings, enableModules: ["LightSwitch"])
    {
    }

    protected override IReadOnlyList<string> StaleProcessNames =>
        [.. base.StaleProcessNames, TestHelper.ServiceProcess];

    [ClassInitialize]
    public static void CaptureOriginalState(TestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        TestHelper.StopProcesses();
        originalState = new TestState();
    }

    [ClassCleanup(ClassCleanupBehavior.EndOfClass)]
    public static void RestoreOriginalState()
    {
        TestHelper.StopProcesses();
        originalState?.Restore();
    }

    protected override void PrepareTestState()
    {
        // Each test owns a fresh runner/service: manual overrides and debounce work cannot leak.
        TestHelper.StopProcesses();
        originalState!.Restore();
        TestState.SeedSettings();
    }

    [TestInitialize]
    public void OpenLightSwitchPage()
    {
        page = new TestHelper(Session, TestContext);
        page.Navigate();
        page.VerifyEnabled(true);
        page.ReadShortcut();
    }

    [TestCleanup]
    public async Task RestoreTestState()
    {
        await CaptureFailureArtifactsBeforeCleanupAsync();
        try
        {
            if (TestContext.CurrentTestOutcome != UnitTestOutcome.Passed && page is not null)
            {
                page.SaveFailureState();
            }
        }
        finally
        {
            TestHelper.StopProcesses();
            originalState?.Restore();
        }
    }

    [TestMethod]
    [TestCategory("Shortcut")]
    public void TestLightSwitchShortcut()
    {
        page.SetEnabled(false);
        page.SetEnabled(true);
        page.SelectMode("Off", "OffCBItem_LightSwitch", "Off");
        page.SetThemeTargets(system: true, apps: true);

        var before = TestState.ReadTheme();
        page.SendShortcut();
        page.WaitForTheme(new ThemeState(1 - before.System, 1 - before.Apps));

        page.SetThemeTargets(system: false, apps: false);
        before = TestState.ReadTheme();
        page.SendShortcut();
        page.AssertThemeUnchanged(before);
    }

    [TestMethod]
    [TestCategory("Time")]
    public void TestUpdateTime()
    {
        page.SelectMode("FixedHours", "ManualCBItem_LightSwitch", "Fixed hours");
        var initial = page.WaitForTimeline(360, 1080);
        page.WaitForScheduledTheme();

        int darkBefore = TestHelper.ReadSetting<int>("darkTime");
        page.ChangeTime("DarkTimePicker");
        int darkAfter = page.WaitForChangedTime("darkTime", darkBefore);
        var afterDark = page.WaitForTimeline(360, darkAfter);
        Assert.AreNotEqual(initial.End, afterDark.End, "Changing the dark TimePicker must move the timeline end.");
        Assert.AreEqual(initial.Start, afterDark.Start, "Changing dark time must not move the light boundary.");

        int lightBefore = TestHelper.ReadSetting<int>("lightTime");
        page.ChangeTime("LightTimePicker");
        int lightAfter = page.WaitForChangedTime("lightTime", lightBefore);
        var afterLight = page.WaitForTimeline(lightAfter, darkAfter);
        Assert.AreNotEqual(afterDark.Start, afterLight.Start, "Changing the light TimePicker must move the timeline start.");
        Assert.AreEqual(afterDark.End, afterLight.End, "Changing light time must not move the dark boundary.");
        page.WaitForScheduledTheme();
    }

    [TestMethod]
    [TestCategory("Time")]
    public void TestTimeOffset()
    {
        page.SelectMode("SunsetToSunrise", "SunCBItem_LightSwitch", "Sunset to sunrise");
        var initial = page.WaitForPersistedTimeline();
        int light = TestHelper.ReadSetting<int>("lightTime");
        int dark = TestHelper.ReadSetting<int>("darkTime");

        page.SetNumber("SunriseOffset_LightSwitch", 1);
        page.WaitForSetting("sunrise_offset", 1);
        var afterSunrise = page.WaitForTimeline(light + 1, dark);
        Assert.AreNotEqual(initial.Start, afterSunrise.Start, "Sunrise offset must move the timeline start.");
        Assert.AreEqual(initial.End, afterSunrise.End, "Sunrise offset must not move the timeline end.");

        page.SetNumber("SunsetOffset_LightSwitch", -1);
        page.WaitForSetting("sunset_offset", -1);
        var afterSunset = page.WaitForTimeline(light + 1, dark - 1);
        Assert.AreNotEqual(afterSunrise.End, afterSunset.End, "Sunset offset must move the timeline end.");
        Assert.AreEqual(afterSunrise.Start, afterSunset.Start, "Sunset offset must not move the timeline start.");
        page.WaitForScheduledTheme();
    }

    [TestMethod]
    [TestCategory("Location")]
    public void TestUserSelectedLocationUpdate()
    {
        page.SelectMode("SunsetToSunrise", "SunCBItem_LightSwitch", "Sunset to sunrise");
        page.OpenLocation();
        page.SetNumber("LatitudeBox_LightSwitch", 1);
        page.SetNumber("LongitudeBox_LightSwitch", TestState.LocalLongitude - 1);
        var times = page.WaitForSunTimes();
        page.SaveLocation();

        page.WaitForSetting("latitude", "1");
        page.WaitForSetting("longitude", (TestState.LocalLongitude - 1).ToString(CultureInfo.InvariantCulture));
        page.AssertSavedSunTimes(times);
        page.WaitForScheduledTheme();
    }

    [TestMethod]
    [TestCategory("Location")]
    [TestCategory("Geolocation")]
    public void TestGeolocationUpdate()
    {
        page.SelectMode("SunsetToSunrise", "SunCBItem_LightSwitch", "Sunset to sunrise");
        page.OpenLocation();
        page.DetectLocation();
        var times = page.WaitForSunTimes();
        page.SaveLocation();
        page.AssertSavedSunTimes(times);
        page.WaitForScheduledTheme();
    }
}

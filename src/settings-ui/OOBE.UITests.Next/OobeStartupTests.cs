// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OOBE.UITests;

[TestClass]
public sealed class OobeStartupTests : UITestBase
{
    private readonly string versionPath = Path.Combine(SettingsConfigHelper.PowerToysSettingsRoot, "last_version_run.json");
    private IDisposable? versionSnapshot;

    public OobeStartupTests()
        : base(PowerToysModule.PowerToysSettings, enableModules: [])
    {
    }

    protected override void PrepareTestState()
    {
        versionSnapshot = SettingsConfigHelper.PreserveFile(versionPath);
    }

    [TestCleanup]
    public async Task RestoreStartupState()
    {
        await CaptureFailureArtifactsBeforeCleanupAsync();
        try
        {
            OobeTestHelper.StopPowerToys();
        }
        finally
        {
            versionSnapshot?.Dispose();
        }
    }

    [TestMethod]
    [TestCategory("OOBE")]
    public void FirstLaunchOpensGettingStartedAndRecordsOnboarding()
    {
        Step("Stopping Runner and resetting only the onboarding/version markers");
        OobeTestHelper.StopPowerToys();
        var marker = Path.Combine(SettingsConfigHelper.PowerToysSettingsRoot, "oobe_settings.json");
        File.Delete(marker);
        File.Delete(versionPath);
        Step("Starting Runner with a fresh onboarding profile and no --open-settings");
        OobeTestHelper.LaunchRunner();
        var welcome = OobeTestHelper.WaitForSettingsWindow(OobeTestHelper.WelcomeTitle);
        Assert.IsTrue(welcome.Has(By.AccessibilityId("WelcomeTitle"), 30_000), "First launch did not render Getting started.");
        var opened = WaitHelper.WaitForStable(
            () =>
            {
                if (!File.Exists(marker))
                {
                    return false;
                }

                using var json = OobeTestHelper.ReadJson(marker);
                return json.RootElement.GetProperty("openedAtFirstLaunch").GetBoolean();
            },
            observed => observed,
            timeoutMS: 30_000,
            shouldRetryException: error => error is JsonException or FileNotFoundException);
        Assert.IsTrue(opened.Succeeded, $"Runner did not record first-launch onboarding. Last observation: {opened}.");
        OobeTestHelper.CloseWindow(welcome);
    }

    [TestMethod]
    [TestCategory("OOBE")]
    public void VersionChangeOpensWhatsNewInsteadOfWelcome()
    {
        Step("Stopping Runner and setting the actual previous-version marker");
        OobeTestHelper.StopPowerToys();
        File.WriteAllText(versionPath, """{"last_version":"v0.0.0"}""");
        var settingsPath = Path.Combine(SettingsConfigHelper.PowerToysSettingsRoot, "settings.json");
        var settings = JsonNode.Parse(File.ReadAllText(settingsPath))!.AsObject();
        settings["show_whats_new_after_updates"] = true;
        File.WriteAllText(settingsPath, settings.ToJsonString());

        Step("Starting Runner after a version change, without --open-settings");
        OobeTestHelper.LaunchRunner();
        var whatsNew = OobeTestHelper.WaitForSettingsWindow(OobeTestHelper.WhatsNewTitle);
        Assert.IsFalse(
            WindowControl.EnumerateAllWindows().Any(w => w.IsVisible && w.Title == OobeTestHelper.WelcomeTitle),
            "An already-onboarded profile reopened Welcome after an update.");
        using var version = OobeTestHelper.ReadJson(versionPath);
        Assert.AreNotEqual("v0.0.0", version.RootElement.GetProperty("last_version").GetString(), "Runner did not update its last-run version.");
        TestContext.WriteLine("Release-note downloading is not required for the startup-routing assertion; offline guests may show the supported error state.");
        OobeTestHelper.CloseWindow(whatsNew);
    }

    private void Step(string message) => TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] {message}");
}

// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OOBE.UITests;

[TestClass]
public sealed class OobeStartupTests : UITestBase
{
    private readonly string versionPath = Path.Combine(SettingsConfigHelper.PowerToysSettingsRoot, "last_version_run.json");
    private readonly List<IDisposable> startupSnapshots = [];

    public OobeStartupTests()
        : base(PowerToysModule.PowerToysSettings, enableModules: [])
    {
    }

    protected override void PrepareTestState()
    {
        startupSnapshots.Add(SettingsConfigHelper.PreserveFile(versionPath));
        startupSnapshots.Add(SettingsConfigHelper.PreserveFile(
            Path.Combine(SettingsConfigHelper.PowerToysSettingsRoot, "settings-placement.json")));
    }

    [TestCleanup]
    public async Task RestoreStartupState()
    {
        await CaptureFailureArtifactsBeforeCleanupAsync();
        var failures = new List<Exception>();
        try
        {
            OobeTestHelper.StopPowerToys();
        }
        catch (Exception error)
        {
            failures.Add(error);
        }

        foreach (var snapshot in startupSnapshots.AsEnumerable().Reverse())
        {
            try
            {
                snapshot.Dispose();
            }
            catch (Exception error)
            {
                failures.Add(error);
            }
        }

        if (failures.Count > 0)
        {
            throw new AggregateException("Could not restore the onboarding test state.", failures);
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
        OobeTestHelper.EnableUpdateOnboarding();
        Step("Starting Runner with a fresh onboarding profile and no --open-settings");
        OobeTestHelper.LaunchRunner();
        var welcome = OobeTestHelper.WaitForSettingsWindow(OobeTestHelper.WelcomeTitle);
        Assert.IsTrue(welcome.Has(By.AccessibilityId("WelcomeTitle"), 30_000), "First launch did not render Getting started.");
        OobeTestHelper.AssertWindowStaysClosed(OobeTestHelper.WhatsNewTitle);
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
        OobeTestHelper.EnableUpdateOnboarding();

        Step("Starting Runner after a version change, without --open-settings");
        OobeTestHelper.LaunchRunner();
        var whatsNew = OobeTestHelper.WaitForSettingsWindow(OobeTestHelper.WhatsNewTitle);
        OobeTestHelper.AssertWindowStaysClosed(OobeTestHelper.WelcomeTitle);
        using var version = OobeTestHelper.ReadJson(versionPath);
        Assert.AreNotEqual("v0.0.0", version.RootElement.GetProperty("last_version").GetString(), "Runner did not update its last-run version.");
        TestContext.WriteLine("Release-note downloading is not required for the startup-routing assertion; offline guests may show the supported error state.");
        TestContext.WriteLine("Upgrade startup may emit the standard delayed restart toast; this routing test leaves the product's notification policy unchanged.");
        OobeTestHelper.CloseWindow(whatsNew);
    }

    private void Step(string message) => TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] {message}");
}

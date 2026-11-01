// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.Awake.UITests;

[TestClass]
[TestCategory("Awake")]
[TestCategory("AwakeCLI")]
public sealed class AwakeCliTests : UITestBase
{
    private static IDisposable? snapshot;
    private AwakeTerminal? terminal;
    private AwakeUi ui = null!;
    private string originalClipboard = string.Empty;
    private bool clipboardCaptured;

    public AwakeCliTests()
        : base(PowerToysModule.PowerToysSettings, enableModules: [])
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
    public void PrepareTerminal()
    {
        ui = new AwakeUi(TestContext);
        ui.WaitForProcess(false);
        ui.Step("Waiting for background execution-state requests to clear before standalone CLI testing.");
        AwakeUi.WaitForPowerState(false, false, timeoutMS: 180_000);
        originalClipboard = ClipboardHelper.GetText();
        clipboardCaptured = true;
        terminal = new AwakeTerminal(TestContext);
    }

    [TestCleanup]
    public async Task CloseTerminal()
    {
        await CaptureFailureArtifactsBeforeCleanupAsync();
        try
        {
            terminal?.Dispose();
            ui?.WaitForProcess(false);
            ShellMenu.Dismiss();
        }
        finally
        {
            if (clipboardCaptured)
            {
                Assert.IsTrue(
                    originalClipboard.Length == 0 ? ClipboardHelper.Clear() : ClipboardHelper.SetText(originalClipboard),
                    "Could not restore the original clipboard text.");
            }
        }
    }

    [TestMethod]
    [DataRow("--help")]
    [DataRow("-h")]
    public void HelpShowsDocumentedOptionsInTerminal(string argument)
    {
        terminal!.Start(argument, captureOutput: true);
        Assert.AreEqual(0, terminal.WaitForExit());
        foreach (var option in new[] { "--use-pt-config", "--display-on", "--time-limit", "--pid", "--expire-at", "--use-parent-pid" })
        {
            terminal.WaitForVisibleText(option);
        }

        ui.WaitForProcess(false);
        AwakeUi.WaitForPowerState(false, false);
    }

    [TestMethod]
    [DataRow("--time-limit -1", "--time-limit")]
    [DataRow("--pid not-a-pid", "--pid")]
    [DataRow("--expire-at not-a-date", "--expire-at")]
    public void InvalidOptionsShowErrorsAndDoNotKeepAwake(string arguments, string option)
    {
        terminal!.Start(arguments, captureOutput: true);
        Assert.AreNotEqual(0, terminal.WaitForExit());
        terminal.WaitForVisibleText(option);
        terminal.WaitForVisibleText("could not be parsed correctly");
        StringAssert.Contains(terminal.ReadOutput(), "could not be parsed correctly");
        ui.WaitForProcess(false);
        AwakeUi.WaitForPowerState(false, false);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void StandaloneIndefiniteHonorsDisplayOptionAndExitsCleanly(bool displayOn)
    {
        var powerPlan = AwakeUi.ReadPowerPlan();
        terminal!.Start(displayOn ? "--display-on" : "--display-on false");
        ui.WaitForProcess(true);
        AwakeUi.WaitForPowerState(true, displayOn);
        ui.TrayIcon(name => name.Contains("Indefinite", StringComparison.Ordinal) &&
            name.Contains(displayOn ? "Screen: On" : "Screen: Off", StringComparison.Ordinal));
        ShellMenu.Dismiss();
        Assert.IsTrue(NamedEventHelper.WaitAndSignal(AwakeUi.ExitEvent), "Awake did not accept its normal exit event.");
        Assert.AreEqual(0, terminal.WaitForExit());
        ui.WaitForProcess(false);
        AwakeUi.WaitForPowerState(false, false);
        Assert.AreEqual(powerPlan, AwakeUi.ReadPowerPlan(), "Standalone Awake must leave the power plan unchanged.");
        Assert.AreEqual(0, AwakeUi.ReadProperties()["mode"]!.GetValue<int>(), "Standalone arguments must not change PowerToys configuration.");
    }

    [TestMethod]
    public void StandaloneTimeLimitExpiresAndReleasesDisplayRequest()
    {
        terminal!.Start("-t 20 -d");
        ui.WaitForProcess(true);
        AwakeUi.WaitForPowerState(true, true);
        Assert.AreEqual(0, terminal.WaitForExit(40_000));
        ui.WaitForProcess(false);
        AwakeUi.WaitForPowerState(false, false);
    }

    [TestMethod]
    public void StandaloneExpirationTakesPrecedenceOverTimeLimit()
    {
        terminal!.Start("--time-limit 300", expirationDelaySeconds: 30);
        ui.WaitForProcess(true);
        AwakeUi.WaitForPowerState(true, false);
        Assert.AreEqual(0, terminal.WaitForExit(50_000), "The absolute expiration must take precedence over the longer time limit.");
        ui.WaitForProcess(false);
        AwakeUi.WaitForPowerState(false, false);
    }

    [TestMethod]
    public void ExplicitPidBindingEndsWhenTargetExits()
    {
        using var target = Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe"), "-NoProfile -Command Start-Sleep -Seconds 120")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        }) ?? throw new InvalidOperationException("Could not create the test-owned PID target.");
        try
        {
            terminal!.Start($"--pid {target.Id} --display-on");
            ui.WaitForProcess(true);
            AwakeUi.WaitForPowerState(true, true);
            ui.TrayIcon(name => name.Contains($"PID: {target.Id}", StringComparison.Ordinal));
            ShellMenu.Dismiss();
            target.Kill();
            Assert.IsTrue(target.WaitForExit(10_000));
            Assert.AreEqual(0, terminal.WaitForExit());
            AwakeUi.WaitForPowerState(false, false);
        }
        finally
        {
            if (!target.HasExited)
            {
                target.Kill();
                Assert.IsTrue(target.WaitForExit(10_000), "Could not stop the PID-binding fixture.");
            }
        }
    }

    [TestMethod]
    public void ParentPidBindingEndsWithoutKillingAwakeDirectly()
    {
        terminal!.Start("--use-parent-pid");
        ui.WaitForProcess(true);
        AwakeUi.WaitForPowerState(true, false);
        ui.TrayIcon(name => name.Contains($"PID: {terminal.ShellId}", StringComparison.Ordinal));
        ShellMenu.Dismiss();
        terminal.StopParent();
        Assert.IsTrue(terminal.AwakeProcess.WaitForExit(20_000), "Awake did not detect its parent's termination.");
        Assert.AreEqual(0, terminal.AwakeProcess.ExitCode);
        ui.WaitForProcess(false);
        AwakeUi.WaitForPowerState(false, false);
    }

    [TestMethod]
    public void ConfigOptionOverridesStandaloneArgumentsAndHotReloads()
    {
        terminal!.Start($"--use-pt-config --pid {terminal.ShellId} --display-on --time-limit 1");
        ui.WaitForProcess(true);
        AwakeUi.WaitForPowerState(false, false);
        ui.TrayIcon(name => name.EndsWith("Off", StringComparison.Ordinal) && !name.Contains("Screen:", StringComparison.Ordinal));
        ShellMenu.Dismiss();
        ui.Step("Updating the configuration-bound CLI session to indefinite without restarting.");
        AwakeUi.UpdateProperties(properties => properties["mode"] = 1);
        ui.WaitForSettings(properties => properties["mode"]!.GetValue<int>() == 1, "CLI config indefinite");
        AwakeUi.WaitForPowerState(true, false);
        AwakeUi.UpdateProperties(properties => properties["keepDisplayOn"] = true);
        AwakeUi.WaitForPowerState(true, true);
        AwakeUi.UpdateProperties(properties => properties["mode"] = 0);
        AwakeUi.WaitForPowerState(false, false);
        Assert.IsFalse(terminal.AwakeProcess.HasExited, "Config-bound Awake must remain running after returning to passive.");
        Assert.IsTrue(NamedEventHelper.WaitAndSignal(AwakeUi.ExitEvent), "Config-bound Awake did not accept its normal exit event.");
        Assert.AreEqual(0, terminal.WaitForExit());
    }
}

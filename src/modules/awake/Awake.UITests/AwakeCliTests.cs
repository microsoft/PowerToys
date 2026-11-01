// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Drawing;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Forms = System.Windows.Forms;

namespace Microsoft.PowerToys.Awake.UITests;

[TestClass]
[TestCategory("Awake")]
[TestCategory("AwakeCLI")]
public sealed class AwakeCliTests : UITestBase
{
    private static readonly string[] HelpOptions = ["--use-pt-config", "--display-on", "--time-limit", "--pid", "--expire-at", "--use-parent-pid"];
    private static IDisposable? snapshot;
    private AwakeTerminal? terminal;
    private AwakeUi ui = null!;
    private ClipboardSnapshot? clipboardSnapshot;

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
        ui.WaitForPassiveBaseline();
        clipboardSnapshot = new ClipboardSnapshot();
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
            clipboardSnapshot?.Dispose();
        }
    }

    [TestMethod]
    [DataRow("--help")]
    [DataRow("-h")]
    public void HelpShowsDocumentedOptionsInTerminal(string argument)
    {
        terminal!.Start(argument, waitForCommandExit: true);
        Assert.AreEqual(0, terminal.WaitForExit());
        terminal.WaitForVisibleText(HelpOptions);

        ui.WaitForProcess(false);
        AwakeUi.WaitForPowerState(false, false);
    }

    [TestMethod]
    [DataRow("--time-limit -1", "Interval in --time-limit could not be parsed correctly")]
    [DataRow("--pid not-a-pid", "PID value in --pid could not be parsed correctly")]
    [DataRow("--expire-at not-a-date", "Date and time value in --expire-at could not be parsed correctly")]
    public void InvalidOptionsShowErrorsAndDoNotKeepAwake(string arguments, string diagnostic)
    {
        terminal!.Start(arguments, waitForCommandExit: true);
        Assert.AreNotEqual(0, terminal.WaitForExit());
        terminal.WaitForVisibleText(diagnostic);
        ui.WaitForProcess(false);
        AwakeUi.WaitForPowerState(false, false);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void TerminalInputCanRestoreImageAndFileClipboardContents(bool image)
    {
        var path = SessionHelper.GetExecutablePath(PowerToysModule.Runner);
        ClipboardSnapshot.OnSta(() =>
        {
            if (image)
            {
                using var bitmap = new Bitmap(2, 2);
                bitmap.SetPixel(0, 0, Color.Red);
                Forms.Clipboard.SetImage(bitmap);
            }
            else
            {
                Forms.Clipboard.SetFileDropList(new System.Collections.Specialized.StringCollection { path });
            }

            return true;
        });
        using (var fixtureSnapshot = new ClipboardSnapshot())
        {
            terminal!.Start("--help", waitForCommandExit: true);
            Assert.AreEqual(0, terminal.WaitForExit());
        }

        ClipboardSnapshot.OnSta(() =>
        {
            if (image)
            {
                using var restored = Forms.Clipboard.GetImage();
                Assert.IsNotNull(restored, "The terminal fixture did not restore the clipboard image.");
                using var bitmap = new Bitmap(restored);
                Assert.AreEqual(Color.Red.ToArgb(), bitmap.GetPixel(0, 0).ToArgb(), "The restored clipboard image changed.");
            }
            else
            {
                CollectionAssert.Contains(Forms.Clipboard.GetFileDropList(), path, "The terminal fixture did not restore copied files.");
            }

            return true;
        });
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
        var activeTime = Stopwatch.StartNew();
        Assert.IsTrue(terminal.AwakeProcess.WaitForExit(40_000), "The 20-second CLI timer did not exit.");
        Assert.IsTrue(activeTime.Elapsed >= TimeSpan.FromSeconds(15), $"The 20-second CLI timer exited prematurely after {activeTime.Elapsed}.");
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
        var activeTime = Stopwatch.StartNew();
        Assert.IsTrue(terminal.AwakeProcess.WaitForExit(50_000), "The 30-second CLI expiration did not exit.");
        Assert.IsTrue(activeTime.Elapsed >= TimeSpan.FromSeconds(20), $"The 30-second CLI expiration exited prematurely after {activeTime.Elapsed}.");
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

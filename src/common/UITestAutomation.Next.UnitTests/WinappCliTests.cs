// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.UITestAutomationNext.UnitTests;

[TestClass]
[DoNotParallelize]
public sealed class WinappCliTests
{
    private string? originalInvokeTimeout;

    [TestMethod]
    public void InvokingUiDoesNotStopAnIndependentlyOwnedSameImageProcess()
    {
        var root = Path.Combine(Path.GetTempPath(), "winapp-owner-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        System.Diagnostics.Process? persistent = null;
        try
        {
            var executable = Path.Combine(root, "winapp.exe");
            File.Copy(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "ping.exe"), executable);
            var start = new System.Diagnostics.ProcessStartInfo(executable)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            start.ArgumentList.Add("-t");
            start.ArgumentList.Add("127.0.0.1");
            persistent = System.Diagnostics.Process.Start(start)!;
            Assert.IsFalse(persistent.WaitForExit(100));

            var result = WinappCli.InvokeExecutable(executable, "-n", "1", "127.0.0.1");

            Assert.AreEqual(0, result.ExitCode);
            Assert.IsFalse(persistent.HasExited, "A UI invocation must not terminate a separately owned transport sharing its executable.");
        }
        finally
        {
            if (persistent is not null)
            {
                if (!persistent.HasExited)
                {
                    persistent.Kill();
                    Assert.IsTrue(persistent.WaitForExit(5000));
                }

                persistent.Dispose();
            }

            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void StrayCleanupRequiresThisInvocationParentLifetimeAndExecutable()
    {
        var started = new DateTime(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc);
        var exited = started.AddSeconds(10);
        const string LocalCli = @"C:\UiTools\winapp.exe";
        Assert.IsTrue(WinappCli.IsOwnedWinappChild(42, started, exited, LocalCli, 42, started.AddSeconds(1), @"c:\uitools\WINAPP.exe"));
        Assert.IsFalse(
            WinappCli.IsOwnedWinappChild(42, started, exited, LocalCli, 99, started.AddSeconds(1), LocalCli),
            "A persistent transport launched by the test host is a sibling, not this invocation's child.");
        Assert.IsFalse(WinappCli.IsOwnedWinappChild(42, started, exited, LocalCli, 42, started.AddSeconds(1), @"C:\Preview\winapp.exe"));
        Assert.IsFalse(WinappCli.IsOwnedWinappChild(42, started, exited, LocalCli, 42, started.AddSeconds(-1), LocalCli));
        Assert.IsFalse(
            WinappCli.IsOwnedWinappChild(42, started, exited, LocalCli, 42, exited.AddSeconds(1), LocalCli),
            "Reused parent PIDs must not authorize stopping a later process.");
        Assert.IsFalse(WinappCli.IsOwnedWinappChild(42, started, exited, LocalCli, 42, started.AddSeconds(1), null));
    }

    [TestMethod]
    public void ResolveOwnerImagePathIgnoresAnAliasStubLaunchPathWhileTheProcessIsAlive()
    {
        // Regression for an App Execution Alias: ProcessStartInfo.FileName is the reparse-point stub
        // under %LOCALAPPDATA%\Microsoft\WindowsApps, not the real installed image the OS resolved to
        // and that Process.MainModule reports. Using the launch path as "the owner" would make every
        // genuine same-image child look unowned and leak pipe-holding strays.
        var root = Path.Combine(Path.GetTempPath(), "winapp-alias-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var realExecutable = Path.Combine(root, "winapp.exe");
        File.Copy(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "ping.exe"), realExecutable);
        const string FakeAliasStubPath = @"C:\Users\Fake\AppData\Local\Microsoft\WindowsApps\winapp.exe";

        var start = new System.Diagnostics.ProcessStartInfo(realExecutable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.ArgumentList.Add("-t");
        start.ArgumentList.Add("127.0.0.1");
        using var owner = System.Diagnostics.Process.Start(start)!;
        try
        {
            Assert.IsFalse(owner.WaitForExit(100));

            var resolved = WinappCli.ResolveOwnerImagePath(owner, FakeAliasStubPath);

            Assert.AreEqual(realExecutable, resolved, "Must report the real running image, not the alias stub launch path.");
            Assert.AreNotEqual(FakeAliasStubPath, resolved);

            // A genuine child sharing the real resolved image is recognized as owned...
            var ownerStarted = owner.StartTime.ToUniversalTime();
            var ownerExited = DateTime.UtcNow;
            Assert.IsTrue(WinappCli.IsOwnedWinappChild(owner.Id, ownerStarted, ownerExited, resolved, owner.Id, ownerStarted, realExecutable));

            // ...while matching against the raw alias launch path (the bug) would have rejected it.
            Assert.IsFalse(WinappCli.IsOwnedWinappChild(owner.Id, ownerStarted, ownerExited, FakeAliasStubPath, owner.Id, ownerStarted, realExecutable));
        }
        finally
        {
            owner.Kill();
            Assert.IsTrue(owner.WaitForExit(5000));
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void ResolveOwnerImagePathFallsBackToTheLaunchPathOnceTheOwnerHasExited()
    {
        var root = Path.Combine(Path.GetTempPath(), "winapp-exited-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var realExecutable = Path.Combine(root, "winapp.exe");
        File.Copy(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "ping.exe"), realExecutable);

        var start = new System.Diagnostics.ProcessStartInfo(realExecutable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.ArgumentList.Add("-n");
        start.ArgumentList.Add("1");
        start.ArgumentList.Add("127.0.0.1");
        using var owner = System.Diagnostics.Process.Start(start)!;
        try
        {
            Assert.IsTrue(owner.WaitForExit(5000), "The short-lived probe must exit on its own before the assertion.");

            var resolved = WinappCli.ResolveOwnerImagePath(owner, realExecutable);

            Assert.AreEqual(realExecutable, resolved, "Module enumeration fails post-exit; must fall back without throwing.");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestInitialize]
    public void SaveEnvironment()
    {
        originalInvokeTimeout = Environment.GetEnvironmentVariable(WinappCli.InvokeTimeoutSecondsEnvironmentVariable);
    }

    [TestCleanup]
    public void RestoreEnvironment()
    {
        Environment.SetEnvironmentVariable(WinappCli.InvokeTimeoutSecondsEnvironmentVariable, originalInvokeTimeout);
    }

    [TestMethod]
    public void ResolveInvokeTimeoutHonorsEnvironmentOverride()
    {
        Environment.SetEnvironmentVariable(WinappCli.InvokeTimeoutSecondsEnvironmentVariable, "180");

        Assert.AreEqual(TimeSpan.FromSeconds(180), WinappCli.ResolveInvokeTimeout([]));
    }

    [TestMethod]
    [DataRow("invalid")]
    [DataRow("0")]
    [DataRow("3601")]
    public void ResolveInvokeTimeoutRejectsInvalidEnvironmentOverride(string value)
    {
        Environment.SetEnvironmentVariable(WinappCli.InvokeTimeoutSecondsEnvironmentVariable, value);

        Assert.AreEqual(TimeSpan.FromSeconds(60), WinappCli.ResolveInvokeTimeout([]));
    }

    [TestMethod]
    public void ResolveInvokeTimeoutExtendsPastLongerCommandTimeout()
    {
        Environment.SetEnvironmentVariable(WinappCli.InvokeTimeoutSecondsEnvironmentVariable, "180");

        Assert.AreEqual(
            TimeSpan.FromSeconds(230),
            WinappCli.ResolveInvokeTimeout(["wait-for", "target", "--timeout", "200000"]));
    }
}

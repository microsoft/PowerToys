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
        Assert.IsFalse(WinappCli.IsOwnedWinappChild(42, started, exited, null, 42, started.AddSeconds(1), LocalCli));
        Assert.IsFalse(WinappCli.IsOwnedWinappChild(42, started, exited, string.Empty, 42, started.AddSeconds(1), LocalCli));
    }

    [TestMethod]
    public void ResolveOwnerImagePathIgnoresAnAliasStubLaunchPathWhileTheProcessIsAlive()
    {
        // Model alias launch metadata while retaining a real, independently queryable process image.
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

            owner.StartInfo.FileName = FakeAliasStubPath;
            var resolved = WinappCli.ResolveOwnerImagePath(owner);

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
    public void CapturedImageSurvivesOwnerExitWithoutFallingBackToItsAlias()
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
        start.ArgumentList.Add("-t");
        start.ArgumentList.Add("127.0.0.1");
        using var owner = System.Diagnostics.Process.Start(start)!;
        try
        {
            Assert.IsFalse(owner.WaitForExit(100));
            var capturedImage = WinappCli.ResolveOwnerImagePath(owner);
            Assert.AreEqual(realExecutable, capturedImage);
            owner.Kill();
            Assert.IsTrue(owner.WaitForExit(5000));

            const string FakeAliasStubPath = @"C:\Users\Fake\AppData\Local\Microsoft\WindowsApps\winapp.exe";
            owner.StartInfo.FileName = FakeAliasStubPath;
            var resolved = WinappCli.ResolveOwnerImagePath(owner);

            Assert.AreEqual(realExecutable, capturedImage, "Cleanup must retain the image captured before exit.");
            Assert.IsTrue(resolved is null || resolved == realExecutable, "An exited process must either remain queryable or fail closed.");
            Assert.AreNotEqual(FakeAliasStubPath, resolved, "The alias launch path is not process identity.");
        }
        finally
        {
            if (!owner.HasExited)
            {
                owner.Kill();
                Assert.IsTrue(owner.WaitForExit(5000));
            }

            Directory.Delete(root, recursive: true);
        }
    }

    [DataTestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void PipeCleanupRequiresTheOwnedImageAndNeverReturnsTruncatedSuccess(bool sameImage)
    {
        var root = Path.Combine(Path.GetTempPath(), "winapp-pipe-owner-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var executable = Path.Combine(root, "winapp.exe");
        var childIdFile = Path.Combine(root, "child-id.txt");
        var ownerExitPermissionFile = Path.Combine(root, "owner-can-exit.txt");
        File.Copy(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe"),
            executable);
        var childExecutable = executable;
        if (!sameImage)
        {
            Directory.CreateDirectory(Path.Combine(root, "other"));
            childExecutable = Path.Combine(root, "other", "winapp.exe");
            File.Copy(executable, childExecutable);
        }

        System.Diagnostics.Process? child = null;
        Task<WinappCli.Result>? invocation = null;
        try
        {
            Environment.SetEnvironmentVariable(WinappCli.InvokeTimeoutSecondsEnvironmentVariable, "15");
            var childCommand = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes("Start-Sleep -Seconds 30"));
            var command = $$"""
                $start = New-Object Diagnostics.ProcessStartInfo
                $start.FileName = '{{childExecutable.Replace("'", "''")}}'
                $start.Arguments = '-NoProfile -NonInteractive -EncodedCommand {{childCommand}}'
                $start.UseShellExecute = $false
                $start.CreateNoWindow = $true
                $child = [Diagnostics.Process]::Start($start)
                [IO.File]::WriteAllText('{{childIdFile.Replace("'", "''")}}.tmp', [string]$child.Id)
                [IO.File]::Move('{{childIdFile.Replace("'", "''")}}.tmp', '{{childIdFile.Replace("'", "''")}}')
                $deadline = [DateTime]::UtcNow.AddSeconds(10)
                while (-not [IO.File]::Exists('{{ownerExitPermissionFile.Replace("'", "''")}}')) {
                    if ([DateTime]::UtcNow -ge $deadline) { throw 'Fixture identity acknowledgement timed out' }
                    [Threading.Thread]::Sleep(10)
                }
                [Console]::WriteLine('owned-output')
                """;
            var encoded = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(command));
            invocation = Task.Run(() => WinappCli.InvokeExecutable(executable, "-NoProfile", "-NonInteractive", "-EncodedCommand", encoded));
            Assert.IsTrue(SpinWait.SpinUntil(() => File.Exists(childIdFile) || invocation.IsCompleted, TimeSpan.FromSeconds(20)));
            Assert.IsTrue(File.Exists(childIdFile), "The helper must publish its direct child's identity.");
            child = System.Diagnostics.Process.GetProcessById(int.Parse(File.ReadAllText(childIdFile), System.Globalization.CultureInfo.InvariantCulture));
            _ = child.SafeHandle;
            Assert.AreEqual(childExecutable, WinappCli.ResolveOwnerImagePath(child));
            File.WriteAllText(ownerExitPermissionFile, string.Empty);

            if (sameImage)
            {
                var result = invocation.GetAwaiter().GetResult();
                Assert.AreEqual(0, result.ExitCode);
                Assert.AreEqual("owned-output", result.StdOut.Trim(), "Pipe cleanup must retain the already-written output.");
                Assert.IsTrue(child.WaitForExit(5000), "Only the proved same-image pipe-holding child should be stopped.");
            }
            else
            {
                var error = Assert.ThrowsExactly<TimeoutException>(() => invocation.GetAwaiter().GetResult());
                StringAssert.Contains(error.Message, "output pipes remained open");
                Assert.IsFalse(child.HasExited, "A different image must not be terminated merely to recover pipe output.");
            }
        }
        finally
        {
            if (child is not null)
            {
                if (!child.HasExited)
                {
                    child.Kill();
                    Assert.IsTrue(child.WaitForExit(5000));
                }

                child.Dispose();
            }

            if (invocation is { IsCompleted: false })
            {
                Assert.IsTrue(invocation.Wait(TimeSpan.FromSeconds(25)), "The bounded invocation must finish before fixture cleanup.");
            }

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

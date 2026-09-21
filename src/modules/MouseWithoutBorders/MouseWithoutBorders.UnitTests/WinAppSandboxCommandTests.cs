// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

using Microsoft.MouseWithoutBorders.UITests;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MouseWithoutBorders.UnitTests;

[TestClass]
[DoNotParallelize]
public sealed class WinAppSandboxCommandTests
{
    private static readonly string[] TimeoutAndStreamErrors = ["command_timeout", "command_stream_incomplete"];

    [TestMethod]
    public void OneShotCompletionClosesUnwrittenStandardInput()
    {
        var root = Path.Combine(Path.GetTempPath(), "mwb-stdin-command-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var executable = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"System32\WindowsPowerShell\v1.0\powershell.exe");
            const string script = "$null=[Console]::In.ReadToEnd(); [Console]::Out.WriteLine('input-closed')";
            using var command = WinAppSandboxCommand.Start(executable, ["-NoProfile", "-NonInteractive", "-Command", script], root, root);
            var result = command.CompleteAndDispose(TimeSpan.FromSeconds(15));
            Assert.AreEqual(0, result.ExitCode);
            Assert.AreEqual("input-closed", result.StandardOutput.Trim());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void ClientLaunchDoesNotInheritOrRedirectRunnerPipesAndRestoresTheirFlags()
    {
        var output = GetStdHandle(-11);
        Assert.IsTrue(GetHandleInformation(output, out var original));
        Assert.IsTrue(SetHandleInformation(output, 1, 1));
        var invoked = false;
        try
        {
            using var client = WinAppSandboxCommand.StartClient("not-launched.exe", ["connect"], Environment.CurrentDirectory, start =>
            {
                invoked = true;
                Assert.IsFalse(start.UseShellExecute);
                Assert.IsTrue(start.CreateNoWindow);
                Assert.IsFalse(start.RedirectStandardInput);
                Assert.IsFalse(start.RedirectStandardOutput);
                Assert.IsFalse(start.RedirectStandardError);
                Assert.IsTrue(GetHandleInformation(output, out var during));
                Assert.AreEqual(0U, during & 1);
                return System.Diagnostics.Process.GetCurrentProcess();
            });
            Assert.IsTrue(invoked);
            Assert.IsTrue(GetHandleInformation(output, out var restored));
            Assert.AreEqual(1U, restored & 1);
        }
        finally
        {
            Assert.IsTrue(SetHandleInformation(output, 1, original & 1));
        }
    }

    [TestMethod]
    public void CommandsKeepStatePrivateAndDisableOptionalNetworkWork()
    {
        var root = Path.Combine(Path.GetTempPath(), "mwb-command-environment-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var originalTelemetry = Environment.GetEnvironmentVariable("WINAPP_CLI_TELEMETRY_OPTOUT");
        var originalUpdates = Environment.GetEnvironmentVariable("WINAPP_CLI_UPDATE_CHECK");
        try
        {
            var executable = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"System32\WindowsPowerShell\v1.0\powershell.exe");
            const string script = "[Console]::WriteLine($env:WINAPP_TARGET_STATE_ROOT); [Console]::WriteLine($env:WINAPP_CLI_TELEMETRY_OPTOUT); [Console]::WriteLine($env:WINAPP_CLI_UPDATE_CHECK)";
            using var command = WinAppSandboxCommand.Start(executable, ["-NoProfile", "-NonInteractive", "-Command", script], root, root);
            var output = command.CompleteAndDispose(TimeSpan.FromSeconds(15)).RequireSuccess().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            Assert.AreEqual(root, output[0]);
            Assert.AreEqual("1", output[1]);
            Assert.AreEqual("0", output[2]);
            Assert.AreEqual(originalTelemetry, Environment.GetEnvironmentVariable("WINAPP_CLI_TELEMETRY_OPTOUT"));
            Assert.AreEqual(originalUpdates, Environment.GetEnvironmentVariable("WINAPP_CLI_UPDATE_CHECK"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void TimeoutRetainsItsCauseAndStopsOnlyTheOwnedCommand()
    {
        var root = Path.Combine(Path.GetTempPath(), "mwb-timeout-command-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        System.Diagnostics.Process? child = null;
        try
        {
            var executable = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"System32\WindowsPowerShell\v1.0\powershell.exe");
            var stop = Path.Combine(root, "stop");
            var childScript = $"while (-not [IO.File]::Exists('{stop}')) {{ Start-Sleep -Milliseconds 100 }}";
            var encoded = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(childScript));
            var script = $"$start=[Diagnostics.ProcessStartInfo]::new('{executable}'); $start.UseShellExecute=$false; $start.CreateNoWindow=$true; $start.RedirectStandardInput=$true; " +
                $"$start.Arguments='-NoProfile -NonInteractive -EncodedCommand {encoded}'; $child=[Diagnostics.Process]::Start($start); " +
                "[Console]::Out.WriteLine($child.Id); [Console]::Out.Flush(); Start-Sleep -Seconds 60";
            using var command = WinAppSandboxCommand.Start(executable, ["-NoProfile", "-NonInteractive", "-Command", script], Path.Combine(root, "private"), root);
            RunFiles.Wait(() => int.TryParse(command.StandardOutput.Trim(), out _), TimeSpan.FromSeconds(15), "The fixture child did not start.");
            child = System.Diagnostics.Process.GetProcessById(int.Parse(command.StandardOutput.Trim(), System.Globalization.CultureInfo.InvariantCulture));
            try
            {
                command.CompleteAndDispose(TimeSpan.FromMilliseconds(50));
                Assert.Fail("The long-running command did not time out.");
            }
            catch (AggregateException error)
            {
                CollectionAssert.AreEqual(
                    TimeoutAndStreamErrors,
                    error.InnerExceptions.Cast<WinAppSandboxException>().Select(item => item.Code).ToArray());
            }
            catch (WinAppSandboxException error)
            {
                Assert.AreEqual("command_timeout", error.Code);
            }

            Assert.IsTrue(command.HasExited);
            Assert.IsFalse(child.HasExited, "The command wrapper must not terminate an unowned descendant.");
        }
        finally
        {
            File.WriteAllText(Path.Combine(root, "stop"), string.Empty);
            if (child is not null)
            {
                if (!child.WaitForExit(10000))
                {
                    child.Kill();
                    Assert.IsTrue(child.WaitForExit(10000), "The owned fixture child did not stop.");
                }

                child.Dispose();
            }

            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void RecordingReadinessArrivesOnStderrBeforeStdoutSummary()
    {
        var root = Path.Combine(Path.GetTempPath(), "mwb-record-command-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var executable = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"System32\WindowsPowerShell\v1.0\powershell.exe");
            const string script = "[Console]::Error.WriteLine('{\"event\":\"recording-started\"}'); [Console]::Error.Flush(); $null=[Console]::ReadLine(); [Console]::Out.WriteLine('{\"completed\":true}')";
            using var command = WinAppSandboxCommand.Start(
                executable,
                ["-NoLogo", "-NoProfile", "-NonInteractive", "-Command", script],
                Path.Combine(root, "private"),
                root);
            var healthChecks = 0;
            command.WaitForRecordingStart(TimeSpan.FromSeconds(15), () => healthChecks++);
            Assert.IsTrue(healthChecks > 0, "The attached worker must remain monitored while recording starts.");
            Assert.AreEqual(string.Empty, command.StandardOutput, "Stdout should remain withheld until recording stops.");
            Assert.IsFalse(command.HasExited);
            command.RequestRecordingStop();
            var result = command.Complete(TimeSpan.FromSeconds(10));
            Assert.AreEqual(0, result.ExitCode);
            StringAssert.Contains(result.StandardOutput, "\"completed\":true");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void RecordingReadinessTimeoutDoesNotAcceptAnUnconfirmedLiveProcess()
    {
        var root = Path.Combine(Path.GetTempPath(), "mwb-record-timeout-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var executable = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"System32\WindowsPowerShell\v1.0\powershell.exe");
            const string script = "[Console]::Out.WriteLine('{\"event\":\"recording-started\"}'); [Console]::Out.Flush(); $null=[Console]::ReadLine()";
            using var command = WinAppSandboxCommand.Start(executable, ["-NoProfile", "-NonInteractive", "-Command", script], root, root);
            RunFiles.Wait(() => command.StandardOutput.Length > 0, TimeSpan.FromSeconds(15), "The fixture did not start.");
            var error = Assert.ThrowsExactly<WinAppSandboxException>(
                () => command.WaitForRecordingStart(TimeSpan.FromMilliseconds(100), () => { }));
            Assert.AreEqual("recording_start_timeout", error.Code);
            Assert.IsFalse(command.HasExited);
            command.RequestRecordingStop();
            Assert.AreEqual(0, command.Complete(TimeSpan.FromSeconds(10)).ExitCode);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void RecordingReadinessPreservesEarlyExitAndWorkerFailures()
    {
        var root = Path.Combine(Path.GetTempPath(), "mwb-record-failure-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var executable = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"System32\WindowsPowerShell\v1.0\powershell.exe");
            const string script = "[Console]::Error.WriteLine('{\"error\":{\"code\":\"sandbox_artifact_failed\"}}'); exit 1";
            using var failed = WinAppSandboxCommand.Start(executable, ["-NoProfile", "-NonInteractive", "-Command", script], root, root);
            Assert.IsTrue(failed.WaitForExit(TimeSpan.FromSeconds(15)));
            var error = Assert.ThrowsExactly<WinAppSandboxException>(
                () => failed.WaitForRecordingStart(TimeSpan.FromSeconds(1), () => { }));
            Assert.AreEqual("sandbox_artifact_failed", error.Code);

            using var waiting = WinAppSandboxCommand.Start(
                executable, ["-NoProfile", "-NonInteractive", "-Command", "$null=[Console]::ReadLine()"], root, root);
            error = Assert.ThrowsExactly<WinAppSandboxException>(
                () => waiting.WaitForRecordingStart(TimeSpan.FromSeconds(15), () => throw new WinAppSandboxException("worker_exited")));
            Assert.AreEqual("worker_exited", error.Code);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint GetStdHandle(int kind);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetHandleInformation(nint handle, out uint flags);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetHandleInformation(nint handle, uint mask, uint flags);
}

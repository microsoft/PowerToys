// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerToys.TryRun.Core;

namespace PowerToys.TryRun.UnitTests;

[TestClass]
public sealed class InstalledApplicationTests
{
    [TestMethod]
    public void InstallationDirectoryComesFromAnExistingRegularApplication()
    {
        using var source = new RunSession();
        var application = Path.Combine(source.WorkingDirectory, "app.exe");
        File.Copy(Path.Combine(Environment.SystemDirectory, "findstr.exe"), application);
        Assert.AreEqual(source.WorkingDirectory, RuntimeFile.ResolveInstallationDirectory(application));
        Assert.ThrowsException<IOException>(() => RuntimeFile.ResolveInstallationDirectory(source.WorkingDirectory));
        Assert.ThrowsException<FileNotFoundException>(() => RuntimeFile.ResolveInstallationDirectory(Path.Combine(source.WorkingDirectory, "missing.exe")));
    }

    [TestMethod]
    [TestCategory("MXCIntegration")]
    public async Task PackagedApplicationReceivesOnlyItsReadonlyInstallationDirectory()
    {
        var application = Environment.GetEnvironmentVariable("POWERTOYS_TRYRUN_PACKAGED_APP");
        if (string.IsNullOrWhiteSpace(application) || !File.Exists(application))
        {
            Assert.Inconclusive("Set POWERTOYS_TRYRUN_PACKAGED_APP to an installed packaged desktop EXE, such as Notepad.");
        }

        var directory = Path.GetDirectoryName(RuntimeFile.Resolve(application))!;
        Assert.AreEqual(directory, RuntimeFile.ResolveInstallationDirectory(application));
        using var run = new RunSession();
        var policy = PolicySettings.Defaults(false);
        using var snapshot = await PolicyTests.DescribeAsync(new ExecutionRequest(string.Empty, run.WorkingDirectory, run.TemporaryDirectory, 30)
        {
            Kind = WorkloadKind.WindowsApplication,
            ApplicationPath = application,
            Policy = policy,
        });
        var filesystem = snapshot.RootElement.GetProperty("Policy").GetProperty("filesystem");
        var reads = filesystem.GetProperty("readonlyPaths").EnumerateArray().Select(value => value.GetString()).ToArray();
        var writes = filesystem.GetProperty("readwritePaths").EnumerateArray().Select(value => value.GetString()).ToArray();
        CollectionAssert.Contains(reads, directory);
        CollectionAssert.DoesNotContain(reads, Path.GetDirectoryName(directory));
        CollectionAssert.DoesNotContain(writes, directory);
        CollectionAssert.AreEquivalent(new[] { run.WorkingDirectory, run.TemporaryDirectory }, writes);
    }

    [TestMethod]
    public void WorkspaceErrorsIncludeTheFailingPathAndWindowsError()
    {
        using var source = new RunSession();
        var missing = Path.Combine(source.WorkingDirectory, "missing", "app.exe");
        var failure = Assert.ThrowsException<IOException>(() => PolicyPaths.ResolveExisting(missing));
        StringAssert.Contains(failure.Message, Path.GetDirectoryName(missing)!);
        StringAssert.Contains(failure.Message, "Windows error");
        Assert.IsInstanceOfType<System.ComponentModel.Win32Exception>(failure.InnerException);
    }

    [TestMethod]
    [TestCategory("MXCIntegration")]
    [Ignore("Known compatibility failure: Notepad 11.2501.31.0 exits with 0xC0000409 and AppModel/activation-store denials under the pinned MXC policy. See CMDPAL-INTEGRATION.md. This probe is retained for future backend compatibility work.")]
    public async Task PackagedApplicationOpensItsOwnWindowAndCanBeStopped()
    {
        var application = Environment.GetEnvironmentVariable("POWERTOYS_TRYRUN_PACKAGED_APP");
        if (Environment.GetEnvironmentVariable("POWERTOYS_TRYRUN_GUI_TESTS") != "1" || !File.Exists(application))
        {
            Assert.Inconclusive("Set POWERTOYS_TRYRUN_GUI_TESTS=1 and POWERTOYS_TRYRUN_PACKAGED_APP to test a packaged desktop EXE.");
        }

        using var session = new RunSession();
        using var cancellation = new CancellationTokenSource();
        var messages = new ConcurrentQueue<WorkerMessage>();
        var policy = PolicySettings.Defaults(false);
        policy.Values["captureEnabled"] = "true";
        var request = new ExecutionRequest(string.Empty, session.WorkingDirectory, session.TemporaryDirectory, 30)
        {
            Kind = WorkloadKind.WindowsApplication,
            ApplicationPath = application,
            Policy = policy,
            CaptureDenials = true,
        };
        var run = new WorkerClient(MultiBackendTests.Worker()).RunAsync(request, new MultiBackendTests.ImmediateProgress(messages.Enqueue), cancellation.Token);
        try
        {
            var visible = false;
            var timer = Stopwatch.StartNew();
            while (!run.IsCompleted && !visible && timer.Elapsed < TimeSpan.FromSeconds(20))
            {
                await Task.Delay(100);
                if (messages.FirstOrDefault(message => message.Process is not null)?.Process is { } identity)
                {
                    try
                    {
                        using var process = Process.GetProcessById((int)identity.ProcessId);
                        visible = WindowsProcessIdentity.Capture(identity.ProcessId) == identity && process.MainWindowHandle != IntPtr.Zero;
                    }
                    catch (ArgumentException)
                    {
                        // The sandbox child may have exited before the poll.
                    }
                }
            }

            var detail = string.Join("\n", messages.Select(message => message.Text));
            detail += "\n" + System.Text.Json.JsonSerializer.Serialize(messages.LastOrDefault(message => message.Report is not null)?.Report);
            if (run.IsCompletedSuccessfully)
            {
                detail += $"\nExit code: {run.Result.ExitCode}";
            }

            Assert.IsTrue(visible, "The sandbox child did not expose a window. " + detail);
            await cancellation.CancelAsync();
            await Assert.ThrowsExceptionAsync<OperationCanceledException>(async () => await run.WaitAsync(TimeSpan.FromSeconds(15)));
        }
        finally
        {
            await cancellation.CancelAsync();
            try
            {
                await run.WaitAsync(TimeSpan.FromSeconds(15));
            }
            catch (OperationCanceledException)
            {
                // The test deliberately stops its own sandbox job.
            }
        }
    }

    [TestMethod]
    public void FailedApplicationExplainsNativeActivationDenialsWithoutTrustingWorkloadText()
    {
        var resource = @"C:\ProgramData\Microsoft\Windows\AppRepository\Packages\Example\ActivationStore.dat";
        var observation = new IsolationEvent("Workload file (self-reported)", resource, "write", "Blocked", string.Empty);
        var report = new IsolationReport("Complete", string.Empty, [observation]);
        var withoutEvidence = WindowsApplicationFailure.Describe(-1073740791, false, report)!;
        StringAssert.Contains(withoutEvidence, "0xC0000409");
        Assert.IsFalse(withoutEvidence.Contains("activation state", StringComparison.Ordinal));
        var native = observation with { NativeDenial = new NativeAccessDenial(resource, "file", "write") };
        var explained = WindowsApplicationFailure.Describe(-1073740791, false, report with { Events = [native] })!;
        StringAssert.Contains(explained, "MXC recorded denied access to Windows app activation state");
        Assert.IsNull(WindowsApplicationFailure.Describe(0, false, report));
        Assert.IsNull(WindowsApplicationFailure.Describe(-1, true, report));
    }
}

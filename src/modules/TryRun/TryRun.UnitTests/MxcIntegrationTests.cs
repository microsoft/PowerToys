// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerToys.TryRun.Core;

namespace PowerToys.TryRun.UnitTests;

[TestClass]
[TestCategory("MXCIntegration")]
public sealed class MxcIntegrationTests
{
    private string workerPath = string.Empty;

    [TestInitialize]
    public void RequireOptInWorker()
    {
        workerPath = Environment.GetEnvironmentVariable("POWERTOYS_TRYRUN_WORKER") ?? string.Empty;
        if (!File.Exists(workerPath))
        {
            Assert.Inconclusive("Set POWERTOYS_TRYRUN_WORKER to the built worker to run trusted-script integration tests.");
        }
    }

    [TestMethod]
    public async Task RunsTrustedScriptAndWritesOnlyItsWorkspace()
    {
        using var session = new RunSession();
        var messages = new ConcurrentQueue<WorkerMessage>();
        var request = new ExecutionRequest("Write-Output 'hello-from-mxc'; Set-Content -LiteralPath result.txt -Value result -NoNewline -ErrorAction Stop", session.WorkingDirectory, session.TemporaryDirectory, 30);
        var result = await new WorkerClient(workerPath).RunAsync(request, new InlineProgress(messages.Enqueue), CancellationToken.None);
        Assert.AreEqual(0, result.ExitCode, string.Join("\n", messages.Select(message => message.Text)));
        Assert.AreEqual("result", File.ReadAllText(Path.Combine(session.WorkingDirectory, "result.txt")));
        StringAssert.Contains(string.Concat(messages.Select(message => message.Text)), "hello-from-mxc");
    }

    [TestMethod]
    public async Task DoesNotInheritTheCallersSecretEnvironment()
    {
        using var session = new RunSession();
        Environment.SetEnvironmentVariable("TRYRUN_TEST_SECRET", "should-not-be-inherited");
        try
        {
            var request = new ExecutionRequest("if ($env:TRYRUN_TEST_SECRET) { exit 42 }; exit 0", session.WorkingDirectory, session.TemporaryDirectory, 30);
            var result = await new WorkerClient(workerPath).RunAsync(request, new InlineProgress(_ => { }), CancellationToken.None);
            Assert.AreEqual(0, result.ExitCode);
        }
        finally
        {
            Environment.SetEnvironmentVariable("TRYRUN_TEST_SECRET", null);
        }
    }

    [TestMethod]
    public async Task RejectsWritingAnotherSessionsWorkspace()
    {
        using var session = new RunSession();
        using var outside = new RunSession();
        var target = Path.Combine(outside.WorkingDirectory, "must-not-exist.txt");
        var escaped = target.Replace("'", "''", StringComparison.Ordinal);
        var messages = new ConcurrentQueue<WorkerMessage>();
        var script = $"Write-Output 'before-denied-write'; Set-Content -LiteralPath '{escaped}' -Value unexpected -ErrorAction Stop";
        var request = new ExecutionRequest(script, session.WorkingDirectory, session.TemporaryDirectory, 30);
        var result = await new WorkerClient(workerPath).RunAsync(request, new InlineProgress(messages.Enqueue), CancellationToken.None);
        StringAssert.Contains(string.Concat(messages.Select(message => message.Text)), "before-denied-write");
        Assert.AreNotEqual(0, result.ExitCode);
        Assert.IsFalse(File.Exists(target));
    }

    [TestMethod]
    public async Task TimeoutStopsTheRun()
    {
        using var session = new RunSession();
        var request = new ExecutionRequest("Start-Sleep -Seconds 60", session.WorkingDirectory, session.TemporaryDirectory, 2);
        var result = await new WorkerClient(workerPath).RunAsync(request, new InlineProgress(_ => { }), CancellationToken.None);
        Assert.IsTrue(result.TimedOut, result.Text);
    }

    [TestMethod]
    public async Task StopTerminatesAnActiveRun()
    {
        using var session = new RunSession();
        using var cancellation = new CancellationTokenSource();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var request = new ExecutionRequest("Write-Output 'started'; Start-Sleep -Seconds 60", session.WorkingDirectory, session.TemporaryDirectory, 120);
        var progress = new InlineProgress(message =>
        {
            if (message.Kind == WorkerMessage.Output && message.Text.Contains("started", StringComparison.Ordinal))
            {
                started.TrySetResult();
            }
        });
        var run = new WorkerClient(workerPath).RunAsync(request, progress, cancellation.Token);
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(30));
            var watch = Stopwatch.StartNew();
            await cancellation.CancelAsync();
            await Assert.ThrowsExceptionAsync<OperationCanceledException>(async () => await run);
            Assert.IsTrue(watch.Elapsed < TimeSpan.FromSeconds(15));
        }
        finally
        {
            await cancellation.CancelAsync();
        }
    }

    [TestMethod]
    public async Task LargeOutputIsDrainedWithoutDeadlock()
    {
        using var session = new RunSession();
        var messages = new ConcurrentQueue<WorkerMessage>();
        var request = new ExecutionRequest("Write-Output ('x' * 1000000)", session.WorkingDirectory, session.TemporaryDirectory, 30);
        var result = await new WorkerClient(workerPath).RunAsync(request, new InlineProgress(messages.Enqueue), CancellationToken.None);
        Assert.AreEqual(0, result.ExitCode);
        var output = string.Concat(messages.Select(message => message.Text));
        Assert.IsTrue(output.Length < OutputBuffer.MaximumCharacters + 1000);
        StringAssert.Contains(output, "Output limit reached");
    }

    private sealed class InlineProgress(Action<WorkerMessage> report) : IProgress<WorkerMessage>
    {
        public void Report(WorkerMessage value)
        {
            report(value);
        }
    }
}

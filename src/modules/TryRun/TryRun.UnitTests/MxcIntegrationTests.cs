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
    public async Task CopiedFilesCanBeModifiedReviewedAndExported()
    {
        using var input = new RunSession();
        using var session = new RunSession();
        using var destination = new RunSession();
        var original = Path.Combine(input.WorkingDirectory, "notes.txt");
        File.WriteAllText(original, "original");
        var workspace = new FileWorkspace(session);
        workspace.Import([original], CancellationToken.None);
        var messages = new ConcurrentQueue<WorkerMessage>();
        var request = new ExecutionRequest("Set-Content notes.txt changed -NoNewline -ErrorAction Stop; New-Item -ItemType Directory results -ErrorAction Stop | Out-Null; Set-Content results/new.txt created -NoNewline -ErrorAction Stop", session.WorkingDirectory, session.TemporaryDirectory, 30);
        var result = await new WorkerClient(workerPath).RunAsync(request, new InlineProgress(messages.Enqueue), CancellationToken.None);
        Assert.AreEqual(0, result.ExitCode, string.Join("\n", messages.Select(message => message.Text)));
        var changes = workspace.Review(CancellationToken.None);
        Assert.AreEqual(FileChangeKind.Modified, changes.Single(change => change.RelativePath == "notes.txt").Kind);
        Assert.AreEqual(FileChangeKind.Added, changes.Single(change => change.RelativePath == "results\\new.txt").Kind);
        var exported = workspace.Export(destination.WorkingDirectory, changes.Select(change => change.RelativePath), CancellationToken.None);
        Assert.AreEqual("changed", File.ReadAllText(Path.Combine(exported, "notes.txt")));
        Assert.AreEqual("created", File.ReadAllText(Path.Combine(exported, "results", "new.txt")));
        Assert.AreEqual("original", File.ReadAllText(original));
    }

    [TestMethod]
    public async Task TimedOutRunCanExportPartialResults()
    {
        using var session = new RunSession();
        using var destination = new RunSession();
        var workspace = new FileWorkspace(session);
        workspace.Import([], CancellationToken.None);
        var request = new ExecutionRequest("Set-Content partial.txt saved -NoNewline -ErrorAction Stop; Start-Sleep -Seconds 60", session.WorkingDirectory, session.TemporaryDirectory, 5);
        var result = await new WorkerClient(workerPath).RunAsync(request, new InlineProgress(_ => { }), CancellationToken.None);
        Assert.IsTrue(result.TimedOut);
        Assert.AreEqual(FileChangeKind.Added, workspace.Review(CancellationToken.None).Single().Kind);
        var exported = workspace.Export(destination.WorkingDirectory, ["partial.txt"], CancellationToken.None);
        Assert.AreEqual("saved", File.ReadAllText(Path.Combine(exported, "partial.txt")));
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
    public async Task AvailabilityProbeVerifiesReadWriteAccess()
    {
        Assert.IsNull(await new WorkerClient(workerPath).GetAvailabilityFailureAsync(CancellationToken.None));
    }

    [TestMethod]
    public async Task RelativePathsAndNavigationStayInTheWorkspaceDrive()
    {
        using var session = new RunSession();
        var messages = new ConcurrentQueue<WorkerMessage>();
        var script = "New-Item -ItemType Directory nested -ErrorAction Stop | Out-Null; Set-Location nested -ErrorAction Stop; Set-Content result.txt ready -NoNewline -ErrorAction Stop; Set-Location .. -ErrorAction Stop; Write-Output $PWD.Path; Get-Content nested/result.txt -ErrorAction Stop";
        var request = new ExecutionRequest(script, session.WorkingDirectory, session.TemporaryDirectory, 30);
        var result = await new WorkerClient(workerPath).RunAsync(request, new InlineProgress(messages.Enqueue), CancellationToken.None);
        var output = string.Concat(messages.Select(message => message.Text));
        Assert.AreEqual(0, result.ExitCode, output);
        StringAssert.Contains(output, "TryRun:\\");
        StringAssert.Contains(output, "ready");
        Assert.AreEqual("ready", File.ReadAllText(Path.Combine(session.WorkingDirectory, "nested", "result.txt")));
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

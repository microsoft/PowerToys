// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerToys.TryRun.Core;

namespace PowerToys.TryRun.UnitTests;

[TestClass]
[TestCategory("WSLCIntegration")]
public sealed class LinuxBackendTests
{
    [TestInitialize]
    public void RequirePreparedImages()
    {
        if (Environment.GetEnvironmentVariable("POWERTOYS_TRYRUN_WSLC_TESTS") != "1")
        {
            Assert.Inconclusive("Set POWERTOYS_TRYRUN_WSLC_TESTS=1 after preparing alpine:3.22 and python:3.12-alpine images.");
        }
    }

    [TestMethod]
    public async Task LinuxModifiesOnlyCopiesAndExportsResults()
    {
        using var original = new RunSession();
        using var session = new RunSession();
        using var destination = new RunSession();
        var file = Path.Combine(original.WorkingDirectory, "notes.txt");
        File.WriteAllText(file, "original");
        var workspace = new FileWorkspace(session);
        workspace.Import([file], CancellationToken.None);
        var outside = CommandEncoding.ShellArgument(CommandEncoding.LinuxPath(file));
        var request = new ExecutionRequest($"uname -s; test ! -e {outside} || exit 42; printf changed > notes.txt; printf created > new.txt", session.WorkingDirectory, session.TemporaryDirectory, 60)
        {
            Kind = WorkloadKind.LinuxShell,
        };
        StringAssert.Contains(await MultiBackendTests.Execute(request), "Linux");
        var changes = workspace.Review(CancellationToken.None);
        Assert.AreEqual(FileChangeKind.Modified, changes.Single(change => change.RelativePath == "notes.txt").Kind);
        var exported = workspace.Export(destination.WorkingDirectory, ["notes.txt", "new.txt"], CancellationToken.None);
        Assert.AreEqual("changed", File.ReadAllText(Path.Combine(exported, "notes.txt")));
        Assert.AreEqual("original", File.ReadAllText(file));
    }

    [TestMethod]
    public async Task RunsSelectedLinuxFolderWithAdjacentData()
    {
        using var source = new RunSession();
        using var session = new RunSession();
        var folder = Path.Combine(source.WorkingDirectory, "linux package's files");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "run.sh"), "#!/bin/sh\ncat data.txt\nprintf changed > data.txt\nprintf created > result.txt\n");
        File.WriteAllText(Path.Combine(folder, "data.txt"), "original");
        var bundle = TaskBundle.Inspect([folder], CancellationToken.None);
        var entry = bundle.EntryPoints.Single();
        var workspace = new FileWorkspace(session);
        workspace.Import(bundle.Inputs, CancellationToken.None);
        var output = await MultiBackendTests.Execute(new ExecutionRequest(string.Empty, session.WorkingDirectory, session.TemporaryDirectory, 60)
        {
            Kind = entry.Kind,
            FileRelativePath = entry.RelativePath,
            WorkingSubdirectory = entry.WorkingSubdirectory,
            Interpreter = entry.Interpreter,
        });
        StringAssert.Contains(output, "original");
        Assert.AreEqual("original", File.ReadAllText(Path.Combine(folder, "data.txt")));
        var changes = workspace.Review(CancellationToken.None);
        Assert.AreEqual(1, changes.Count(change => change.Kind == FileChangeKind.Modified));
        Assert.AreEqual(1, changes.Count(change => change.Kind == FileChangeKind.Added));
    }

    [TestMethod]
    public async Task LinuxScriptFileReceivesLiteralArguments()
    {
        using var session = new RunSession();
        File.WriteAllText(Path.Combine(session.WorkingDirectory, "task.sh"), "printf '%s' \"$1\" > result.txt\n");
        const string argument = "literal ' \" ; $(touch injected)";
        await MultiBackendTests.Execute(new ExecutionRequest(string.Empty, session.WorkingDirectory, session.TemporaryDirectory, 60)
        {
            Kind = WorkloadKind.LinuxShell,
            FileRelativePath = "task.sh",
            Arguments = [argument],
        });
        Assert.AreEqual(argument, File.ReadAllText(Path.Combine(session.WorkingDirectory, "result.txt")));
        Assert.IsFalse(File.Exists(Path.Combine(session.WorkingDirectory, "injected")));
    }

    [TestMethod]
    public async Task RunsPythonFileInsideLinuxImage()
    {
        using var session = new RunSession();
        File.WriteAllText(Path.Combine(session.WorkingDirectory, "task.py"), "import platform, pathlib\npathlib.Path('result.txt').write_text(platform.system())\n");
        await MultiBackendTests.Execute(new ExecutionRequest(string.Empty, session.WorkingDirectory, session.TemporaryDirectory, 60)
        {
            Kind = WorkloadKind.LinuxPython,
            Image = "python:3.12-alpine",
            FileRelativePath = "task.py",
        });
        Assert.AreEqual("Linux", File.ReadAllText(Path.Combine(session.WorkingDirectory, "result.txt")));
    }

    [TestMethod]
    public async Task RunsLinuxExecutableCopiedFromAFile()
    {
        using var source = new RunSession();
        using var session = new RunSession();

        // BusyBox chooses an applet from argv[0]; preserve its executable name.
        await MultiBackendTests.Execute(new ExecutionRequest("cp /bin/busybox ./busybox", source.WorkingDirectory, source.TemporaryDirectory, 60) { Kind = WorkloadKind.LinuxShell });
        var workspace = new FileWorkspace(session);
        workspace.Import([Path.Combine(source.WorkingDirectory, "busybox")], CancellationToken.None);
        var output = await MultiBackendTests.Execute(new ExecutionRequest(string.Empty, session.WorkingDirectory, session.TemporaryDirectory, 60)
        {
            Kind = WorkloadKind.LinuxApplication,
            FileRelativePath = "busybox",
            Arguments = ["echo", "native Linux binary"],
        });
        StringAssert.Contains(output, "native Linux binary");
    }

    [TestMethod]
    public async Task LinuxHasNoExternalNetworkInterfaceOrCallerSecret()
    {
        using var session = new RunSession();
        Environment.SetEnvironmentVariable("TRYRUN_TEST_SECRET", "must-not-leak");
        try
        {
            await MultiBackendTests.Execute(new ExecutionRequest("test -z \"${TRYRUN_TEST_SECRET+x}\" || exit 41; for i in /sys/class/net/*; do test \"${i##*/}\" = lo || exit 42; done", session.WorkingDirectory, session.TemporaryDirectory, 60) { Kind = WorkloadKind.LinuxShell });
        }
        finally
        {
            Environment.SetEnvironmentVariable("TRYRUN_TEST_SECRET", null);
        }
    }

    [TestMethod]
    public async Task LinuxStopTerminatesAnActiveContainer()
    {
        using var session = new RunSession();
        using var cancellation = new CancellationTokenSource();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var request = new ExecutionRequest("printf started; sleep 60", session.WorkingDirectory, session.TemporaryDirectory, 120) { Kind = WorkloadKind.LinuxShell };
        var progress = new MultiBackendTests.ImmediateProgress(message =>
        {
            if (message.Kind == WorkerMessage.Output && message.Text.Contains("started", StringComparison.Ordinal))
            {
                started.TrySetResult();
            }
        });
        var run = new WorkerClient(MultiBackendTests.Worker()).RunAsync(request, progress, cancellation.Token);
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(45));
            await cancellation.CancelAsync();
            await Assert.ThrowsExceptionAsync<OperationCanceledException>(async () => await run.WaitAsync(TimeSpan.FromSeconds(15)));
        }
        finally
        {
            await cancellation.CancelAsync();
        }
    }

    [TestMethod]
    public async Task LinuxTimeoutStopsTheWorkload()
    {
        using var session = new RunSession();
        var request = new ExecutionRequest("sleep 60", session.WorkingDirectory, session.TemporaryDirectory, 3) { Kind = WorkloadKind.LinuxShell };
        var result = await new WorkerClient(MultiBackendTests.Worker()).RunAsync(request, new MultiBackendTests.ImmediateProgress(_ => { }), CancellationToken.None);
        Assert.IsTrue(result.TimedOut, result.Text);
    }
}

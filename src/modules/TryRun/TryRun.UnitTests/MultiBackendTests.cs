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
public sealed class MultiBackendTests
{
    [TestMethod]
    public async Task RunsCopiedWindowsApplicationAlongsideSelectedData()
    {
        using var source = new RunSession();
        using var session = new RunSession();
        var folder = Path.Combine(source.WorkingDirectory, "app package");
        Directory.CreateDirectory(folder);
        File.Copy(Path.Combine(Environment.SystemDirectory, "findstr.exe"), Path.Combine(folder, "findstr.exe"));
        File.WriteAllText(Path.Combine(folder, "data.txt"), "copied application\n");
        var bundle = TaskBundle.Inspect([folder], CancellationToken.None);
        var entry = bundle.EntryPoints.Single();
        var workspace = new FileWorkspace(session);
        workspace.Import(bundle.Inputs, CancellationToken.None);
        var output = await Execute(new ExecutionRequest(string.Empty, session.WorkingDirectory, session.TemporaryDirectory, 30)
        {
            Kind = entry.Kind,
            FileRelativePath = entry.RelativePath,
            WorkingSubdirectory = entry.WorkingSubdirectory,
            Arguments = ["/c:copied application", "data.txt"],
        });
        StringAssert.Contains(output, "copied application");
        Assert.AreEqual("copied application\n", File.ReadAllText(Path.Combine(folder, "data.txt")));
    }

    [TestMethod]
    public async Task RunsNestedPowerShellBundleAndExportsOnlyCopies()
    {
        using var source = new RunSession();
        using var session = new RunSession();
        using var destination = new RunSession();
        var folder = Path.Combine(source.WorkingDirectory, "a package's files");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "run.ps1"), "Get-Content data.txt; Set-Content data.txt changed -NoNewline -ErrorAction Stop; Set-Content result.txt created -NoNewline -ErrorAction Stop");
        File.WriteAllText(Path.Combine(folder, "data.txt"), "original");
        var bundle = TaskBundle.Inspect([folder], CancellationToken.None);
        var entry = bundle.EntryPoints.Single();
        var workspace = new FileWorkspace(session);
        workspace.Import(bundle.Inputs, CancellationToken.None);
        var output = await Execute(new ExecutionRequest(string.Empty, session.WorkingDirectory, session.TemporaryDirectory, 30)
        {
            Kind = entry.Kind,
            FileRelativePath = entry.RelativePath,
            WorkingSubdirectory = entry.WorkingSubdirectory,
        });
        StringAssert.Contains(output, "original");
        var changes = workspace.Review(CancellationToken.None);
        Assert.AreEqual(1, changes.Count(change => change.Kind == FileChangeKind.Modified));
        Assert.AreEqual(1, changes.Count(change => change.Kind == FileChangeKind.Added));
        var exported = workspace.Export(destination.WorkingDirectory, changes.Where(change => change.Kind is FileChangeKind.Modified or FileChangeKind.Added).Select(change => change.RelativePath), CancellationToken.None);
        Assert.AreEqual("original", File.ReadAllText(Path.Combine(folder, "data.txt")));
        Assert.AreEqual("changed", File.ReadAllText(Path.Combine(exported, Path.GetFileName(folder), "data.txt")));
    }

    [TestMethod]
    public async Task RunsBatchInItsSelectedSubdirectory()
    {
        using var session = new RunSession();
        var folder = Path.Combine(session.WorkingDirectory, "package folder");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "run.cmd"), "@echo off\r\ntype data.txt\r\n>result.txt echo created\r\n");
        File.WriteAllText(Path.Combine(folder, "data.txt"), "adjacent data");
        var output = await Execute(new ExecutionRequest(string.Empty, session.WorkingDirectory, session.TemporaryDirectory, 30)
        {
            Kind = WorkloadKind.WindowsBatch,
            FileRelativePath = "package folder\\run.cmd",
            WorkingSubdirectory = "package folder",
        });
        StringAssert.Contains(output, "adjacent data");
        Assert.IsTrue(File.Exists(Path.Combine(folder, "result.txt")));
        Assert.IsFalse(File.Exists(Path.Combine(session.WorkingDirectory, "result.txt")));
    }

    [TestMethod]
    public async Task WindowsGuiApplicationCreatesAWindowAndCanBeStopped()
    {
        if (Environment.GetEnvironmentVariable("POWERTOYS_TRYRUN_GUI_TESTS") != "1")
        {
            Assert.Inconclusive("Set POWERTOYS_TRYRUN_GUI_TESTS=1 to briefly launch Windows version dialog through MXC.");
        }

        using var session = new RunSession();
        using var cancellation = new CancellationTokenSource();
        var existing = new HashSet<int>();
        foreach (var process in Process.GetProcessesByName("winver"))
        {
            using (process)
            {
                existing.Add(process.Id);
            }
        }

        var request = new ExecutionRequest(string.Empty, session.WorkingDirectory, session.TemporaryDirectory, 30)
        {
            Kind = WorkloadKind.WindowsApplication,
            ApplicationPath = Path.Combine(Environment.SystemDirectory, "winver.exe"),
        };
        var messages = new ConcurrentQueue<string>();
        var run = new WorkerClient(Worker()).RunAsync(request, new ImmediateProgress(message => messages.Enqueue(message.Text)), cancellation.Token);
        try
        {
            var visible = false;
            for (var attempt = 0; attempt < 40 && !run.IsCompleted && !visible; attempt++)
            {
                await Task.Delay(250);
                foreach (var process in Process.GetProcessesByName("winver"))
                {
                    using (process)
                    {
                        visible |= !existing.Contains(process.Id) && process.MainWindowHandle != IntPtr.Zero;
                    }
                }
            }

            var exitDetail = run.IsCompletedSuccessfully ? $" Exit code: {run.Result.ExitCode}." : string.Empty;
            Assert.IsTrue(visible, "Windows version dialog did not expose a window." + exitDetail + " " + string.Join("\n", messages));
            await cancellation.CancelAsync();
            await Assert.ThrowsExceptionAsync<OperationCanceledException>(async () => await run.WaitAsync(TimeSpan.FromSeconds(15)));
        }
        finally
        {
            await cancellation.CancelAsync();
        }
    }

    [TestMethod]
    public async Task ReportsAvailableExecutionBackends()
    {
        var availability = await new WorkerClient(Worker()).GetBackendsAsync(CancellationToken.None);
        Assert.IsTrue(availability.WindowsAvailable, availability.WindowsDetail);
        Assert.IsFalse(string.IsNullOrWhiteSpace(availability.NativeVersion));
    }

    [TestMethod]
    public async Task RunsNativeWindowsExecutableWithLiteralArguments()
    {
        using var session = new RunSession();
        File.WriteAllText(Path.Combine(session.WorkingDirectory, "input.txt"), "hello world\n");
        var request = new ExecutionRequest(string.Empty, session.WorkingDirectory, session.TemporaryDirectory, 30)
        {
            Kind = WorkloadKind.WindowsApplication,
            ApplicationPath = Path.Combine(Environment.SystemDirectory, "findstr.exe"),
            Arguments = ["/c:hello world", "input.txt"],
        };
        StringAssert.Contains(await Execute(request), "hello world");
    }

    [TestMethod]
    public async Task RunsPowerShellFileWithQuotedArguments()
    {
        using var session = new RunSession();
        File.WriteAllText(Path.Combine(session.WorkingDirectory, "test.ps1"), "param([string]$message)\nSet-Content result.txt $message -NoNewline -Encoding UTF8 -ErrorAction Stop");
        const string argument = "literal ' \" $env:PATH & value";
        await Execute(new ExecutionRequest(string.Empty, session.WorkingDirectory, session.TemporaryDirectory, 30)
        {
            FileRelativePath = "test.ps1",
            Arguments = [argument],
        });
        Assert.AreEqual(argument, File.ReadAllText(Path.Combine(session.WorkingDirectory, "result.txt")));
    }

    [TestMethod]
    public async Task RunsBatchFileFromCopiedWorkspace()
    {
        using var session = new RunSession();
        File.WriteAllText(Path.Combine(session.WorkingDirectory, "task with spaces.cmd"), "@echo off\r\n>result.txt echo %~1\r\n");
        await Execute(new ExecutionRequest(string.Empty, session.WorkingDirectory, session.TemporaryDirectory, 30)
        {
            Kind = WorkloadKind.WindowsBatch,
            FileRelativePath = "task with spaces.cmd",
            Arguments = ["hello world"],
        });
        Assert.AreEqual("hello world", File.ReadAllText(Path.Combine(session.WorkingDirectory, "result.txt")).Trim());
    }

    internal static string Worker()
    {
        var worker = Environment.GetEnvironmentVariable("POWERTOYS_TRYRUN_WORKER");
        if (!File.Exists(worker))
        {
            Assert.Inconclusive("Set POWERTOYS_TRYRUN_WORKER to the built worker.");
        }

        return worker!;
    }

    internal static async Task<string> Execute(ExecutionRequest request)
    {
        var messages = new ConcurrentQueue<string>();
        var result = await new WorkerClient(Worker()).RunAsync(request, new ImmediateProgress(message => messages.Enqueue(message.Text)), CancellationToken.None);
        var output = string.Join("\n", messages);
        Assert.AreEqual(0, result.ExitCode, output);
        return output;
    }

    internal sealed class ImmediateProgress(Action<WorkerMessage> report) : IProgress<WorkerMessage>
    {
        public void Report(WorkerMessage value)
        {
            report(value);
        }
    }
}

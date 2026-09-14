// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerToys.TryRun.Core;

namespace PowerToys.TryRun.UnitTests;

[TestClass]
public sealed class IsolationTests
{
    [TestMethod]
    public void DenialsAreBoundedAndTruncationIsExplicit()
    {
        var rows = Enumerable.Range(0, 150).Select(index => new { resource = $"C:\\private\\{index}.txt", resourceType = "file", accessType = "read" });
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new { denials = rows, summary = new { totalDenials = 150, deniedResourcesTruncated = false } });
        var report = IsolationReportParser.ReadDenials(bytes);
        Assert.AreEqual("Partial", report.CaptureStatus);
        Assert.AreEqual(100, report.Events.Count);
        Assert.IsTrue(report.Events.All(row => row.Source == "MXC denial capture (block)" && row.Outcome == "Blocked"));
        Assert.ThrowsException<InvalidDataException>(() => IsolationReportParser.ReadDenials(new byte[IsolationReportParser.MaximumDocumentBytes + 1]));
    }

    [TestMethod]
    public void EmptyCaptureDoesNotClaimNoAccessWasAttempted()
    {
        var report = IsolationReportParser.ReadDenials("{\"denials\":[],\"summary\":{\"totalDenials\":0,\"deniedResourcesTruncated\":false}}"u8.ToArray());
        Assert.AreEqual("Complete", report.CaptureStatus);
        StringAssert.Contains(report.CaptureDetail, "not proof");
        Assert.ThrowsException<InvalidDataException>(() => IsolationReportParser.ReadDenials("{\"denials\":[],\"summary\":{\"totalDenials\":1,\"deniedResourcesTruncated\":false}}"u8.ToArray()));
    }

    [TestMethod]
    public void WorkloadFilesCannotImpersonateMxcEvidence()
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(new
        {
            version = 1,
            observations = new[] { new { source = "MXC", resource = new string('x', 1000), access = "read", outcome = "blocked", detail = "line\nwith\0controls" } },
        });
        var bytes = new byte[json.Length + 3];
        new byte[] { 0xEF, 0xBB, 0xBF }.CopyTo(bytes, 0);
        json.CopyTo(bytes, 3);
        var row = IsolationReportParser.ReadObservations(bytes).Single();
        Assert.AreEqual("Workload file (self-reported)", row.Source);
        Assert.AreEqual("Reported: blocked", row.Outcome);
        Assert.AreEqual(512, row.Resource.Length);
        Assert.IsFalse(row.Detail.Any(char.IsControl));
        Assert.ThrowsException<InvalidDataException>(() => IsolationReportParser.ReadObservations(new byte[IsolationReportParser.MaximumObservationBytes + 1]));
    }

    [TestMethod]
    public void OriginalFileChecksDistinguishChangesAndMissingFiles()
    {
        using var source = new RunSession();
        using var target = new RunSession();
        var first = Path.Combine(source.WorkingDirectory, "first.txt");
        var second = Path.Combine(source.WorkingDirectory, "second.txt");
        File.WriteAllText(first, "original");
        File.WriteAllText(second, "second");
        var workspace = new FileWorkspace(target);
        workspace.Import([first, second], CancellationToken.None);
        File.WriteAllText(Path.Combine(target.WorkingDirectory, "first.txt"), "run copy");
        Assert.AreEqual(new OriginalFilesCheck(2, 0, 0), workspace.CheckOriginals(CancellationToken.None));
        File.WriteAllText(first, "changed outside the run");
        File.Delete(second);
        Assert.AreEqual(new OriginalFilesCheck(0, 1, 1), workspace.CheckOriginals(CancellationToken.None));
    }

    [TestMethod]
    public void ReportsMustStayInTheirOwnedDirectory()
    {
        using var session = new RunSession();
        var request = new ExecutionRequest("echo hello", session.WorkingDirectory, session.TemporaryDirectory, 30);
        var diagnostics = RunArtifacts.DiagnosticsDirectory(request);
        Assert.AreEqual(Path.GetDirectoryName(session.WorkingDirectory), Path.GetDirectoryName(diagnostics));
        var report = Path.Combine(diagnostics, "denials.json");
        File.WriteAllText(report, "{}");
        CollectionAssert.AreEqual("{}"u8.ToArray(), RunArtifacts.ReadFile(report, diagnostics, 2));
        Assert.ThrowsException<IOException>(() => RunArtifacts.ReadFile(report, session.WorkingDirectory, 4096));
        Assert.ThrowsException<IOException>(() => RunArtifacts.ReadFile(report, diagnostics, 1));
    }

    [TestMethod]
    public void DiagnosticsRequestRejectsUnsupportedCombinations()
    {
        var request = new ExecutionRequest("echo hello", "C:\\work", "C:\\temp", 30);
        Assert.ThrowsException<ArgumentException>(() => (request with { Kind = WorkloadKind.LinuxShell, CaptureDenials = true }).Validate());
        Assert.ThrowsException<ArgumentException>(() => (request with { Kind = WorkloadKind.LinuxShell, IsolationDemo = true, PrepareImage = true }).Validate());
        (request with { CaptureDenials = true, IsolationDemo = true }).Validate();
    }

    [TestMethod]
    [TestCategory("MXCIntegration")]
    public async Task WindowsDemoReportsBlockedProbeAndUnchangedOriginals()
    {
        await RunDemoAsync(linux: false);
    }

    [TestMethod]
    [TestCategory("WSLCIntegration")]
    public async Task LinuxDemoReportsMountObservationsAndUnchangedOriginals()
    {
        if (Environment.GetEnvironmentVariable("POWERTOYS_TRYRUN_WSLC_TESTS") != "1")
        {
            Assert.Inconclusive("Prepare the Alpine image and enable WSLC integration tests.");
        }

        await RunDemoAsync(linux: true);
    }

    [TestMethod]
    [TestCategory("MXCIntegration")]
    public async Task TimeoutStillProducesAnIsolationReport()
    {
        using var session = new RunSession();
        var messages = new ConcurrentQueue<WorkerMessage>();
        var request = new ExecutionRequest("Start-Sleep -Seconds 10", session.WorkingDirectory, session.TemporaryDirectory, 1) { CaptureDenials = true };
        var result = await new WorkerClient(MultiBackendTests.Worker()).RunAsync(request, new MultiBackendTests.ImmediateProgress(messages.Enqueue), CancellationToken.None);
        Assert.IsTrue(result.TimedOut);
        var report = messages.Last(message => message.Report is not null).Report!;
        Assert.AreNotEqual("Collecting", report.CaptureStatus);
    }

    private static async Task RunDemoAsync(bool linux)
    {
        var worker = MultiBackendTests.Worker();
        var folder = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(worker)!, "..", "Samples", linux ? "Linux-isolation" : "Windows-isolation"));
        using var session = new RunSession();
        using var destination = new RunSession();
        var workspace = new FileWorkspace(session);
        var bundle = TaskBundle.Inspect([folder], CancellationToken.None);
        var entry = bundle.EntryPoints.Single();
        workspace.Import(bundle.Inputs, CancellationToken.None);
        var request = new ExecutionRequest(string.Empty, session.WorkingDirectory, session.TemporaryDirectory, 30)
        {
            Kind = entry.Kind,
            FileRelativePath = entry.RelativePath,
            WorkingSubdirectory = entry.WorkingSubdirectory,
            Interpreter = entry.Interpreter,
            CaptureDenials = !linux,
            IsolationDemo = true,
        };
        var messages = new ConcurrentQueue<WorkerMessage>();
        var client = new WorkerClient(worker);
        var capabilities = await client.GetBackendsAsync(CancellationToken.None);
        var result = await client.RunAsync(request, new MultiBackendTests.ImmediateProgress(messages.Enqueue), CancellationToken.None);
        Assert.AreEqual(0, result.ExitCode, string.Join("\n", messages.Select(message => message.Text)));
        var report = messages.Last(message => message.Report is not null).Report!;
        var environment = messages.Single(message => message.Environment is not null).Environment!;
        Assert.AreEqual(2, environment.WritableFolders.Count);
        Assert.IsFalse(environment.WritableFolders.Any(path => path.EndsWith("Diagnostics", StringComparison.Ordinal)));
        Assert.IsTrue(report.Events.Any(row => row.Source == "Host verification" && row.Outcome == "Unchanged"), JsonSerializer.Serialize(report));
        Assert.IsTrue(report.Events.Any(row => row.Source == "Workload file (self-reported)" && row.Resource == "Harmless host-only fixture" && row.Outcome == "Reported: blocked or unavailable"), JsonSerializer.Serialize(report));
        if (!linux && capabilities.NativeDenialCaptureAvailable)
        {
            Assert.IsTrue(report.CaptureStatus is "Complete" or "Partial", report.CaptureDetail);
            Assert.IsTrue(report.Events.Any(row => row.Source == "MXC denial capture (block)" && row.Outcome == "Blocked"));
        }
        else
        {
            Assert.AreEqual("Not collected", report.CaptureStatus);
        }

        Assert.AreEqual(new OriginalFilesCheck(2, 0, 0), workspace.CheckOriginals(CancellationToken.None));
        var changes = workspace.Review(CancellationToken.None);
        var modified = changes.Single(change => change.Kind == FileChangeKind.Modified);
        Assert.IsTrue(modified.RelativePath.EndsWith("notes.txt", StringComparison.Ordinal));
        var exported = workspace.Export(destination.WorkingDirectory, [modified.RelativePath], CancellationToken.None);
        StringAssert.Contains(File.ReadAllText(Path.Combine(exported, modified.RelativePath)), linux ? "Linux run copy" : "Windows run copy");
    }
}

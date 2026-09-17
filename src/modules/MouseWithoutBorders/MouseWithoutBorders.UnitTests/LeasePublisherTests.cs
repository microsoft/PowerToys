// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using Microsoft.MouseWithoutBorders.UITests;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MouseWithoutBorders.UnitTests;

[TestClass]
public sealed class LeasePublisherTests
{
    private string root = null!;
    private string runId = null!;
    private EndpointChannel host = null!;
    private EndpointChannel guest = null!;

    [TestInitialize]
    public void Initialize()
    {
        root = Path.Combine(Path.GetTempPath(), "mwb-publisher-" + Guid.NewGuid().ToString("N"));
        runId = Guid.NewGuid().ToString();
        host = new EndpointChannel(runId, Path.Combine(root, "host-input"), Path.Combine(root, "host"));
        guest = new EndpointChannel(runId, Path.Combine(root, "guest-input"), Path.Combine(root, "guest"));
        host.WriteLease();
        guest.WriteLease();
    }

    [TestCleanup]
    public void Cleanup() => Directory.Delete(root, recursive: true);

    [TestMethod]
    public void PublisherContinuesWhileOwnerWaitsAndPreservesCommittedGenerations()
    {
        using var owner = Process.GetCurrentProcess();
        using var publisher = CreatePublisher(owner, DateTime.UtcNow.AddMinutes(1));
        publisher.WaitForReady();
        var first = Path.Combine(host.InputRoot, "leases", "00000002.json");
        var original = File.ReadAllBytes(first);
        using (var held = new FileStream(first, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            // No timer or callback in the owner is needed to keep both endpoints alive.
            RunFiles.Wait(
                () => File.Exists(Path.Combine(host.InputRoot, "leases", "00000004.json.ready")) &&
                    File.Exists(Path.Combine(guest.InputRoot, "leases", "00000004.json.ready")),
                TimeSpan.FromSeconds(30),
                "Both endpoints did not receive committed generations while the owner waited.",
                publisher.ThrowIfFailed);
            var state = RunFiles.Read(Path.Combine(root, "lease-publisher.json"));
            var sequence = state["Sequence"]!.GetValue<int>();
            Assert.IsTrue(sequence >= 4, $"Only {sequence} generations were published while the owner waited.");
            foreach (var channel in new[] { host, guest })
            {
                var path = Path.Combine(channel.InputRoot, "leases", $"{sequence - 1:D8}.json");
                Assert.IsTrue(File.Exists(path + ".ready"));
                var lease = RunFiles.Read(path);
                Assert.AreEqual(runId, lease["RunId"]!.GetValue<string>());
                Assert.AreEqual(sequence - 1, lease["Sequence"]!.GetValue<int>());
            }

            CollectionAssert.AreEqual(original, File.ReadAllBytes(first));
        }

        publisher.Stop();
        Assert.AreEqual("Stopped", RunFiles.Read(Path.Combine(root, "lease-publisher.json"))["Stage"]!.GetValue<string>());
    }

    [TestMethod]
    public void PublisherStopsWhenItsExactOwnerExits()
    {
        using var owner = Process.Start(new ProcessStartInfo(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"System32\WindowsPowerShell\v1.0\powershell.exe"),
            "-NoLogo -NoProfile -NonInteractive -Command \"[Threading.Thread]::Sleep(60000)\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        })!;
        try
        {
            using var publisher = CreatePublisher(owner, DateTime.UtcNow.AddMinutes(1));
            publisher.WaitForReady();
            owner.Kill();
            Assert.IsTrue(owner.WaitForExit(5000));
            RunFiles.Wait(
                () => RunFiles.Read(Path.Combine(root, "lease-publisher.json"))["Stage"]!.GetValue<string>() == "ParentExited",
                TimeSpan.FromSeconds(10),
                "Publisher did not stop when its owner exited.");
            publisher.Stop();
        }
        finally
        {
            if (!owner.HasExited)
            {
                owner.Kill();
                Assert.IsTrue(owner.WaitForExit(5000));
            }
        }
    }

    [TestMethod]
    public void PublisherCannotExtendTheRunHardDeadline()
    {
        using var owner = Process.GetCurrentProcess();
        var publisher = CreatePublisher(owner, DateTime.UtcNow.AddSeconds(-1));
        try
        {
            Assert.ThrowsExactly<InvalidOperationException>(publisher.WaitForReady);
            var state = RunFiles.Read(Path.Combine(root, "lease-publisher.json"));
            Assert.AreEqual("Failed", state["Stage"]!.GetValue<string>());
            StringAssert.Contains(state["Error"]!.GetValue<string>(), "hard deadline");
        }
        finally
        {
            Assert.ThrowsExactly<AggregateException>(publisher.Dispose);
        }
    }

    [TestMethod]
    public void PublisherRejectsAnUncorrelatedStopRequest()
    {
        using var owner = Process.GetCurrentProcess();
        var publisher = CreatePublisher(owner, DateTime.UtcNow.AddMinutes(1));
        try
        {
            publisher.WaitForReady();
            RunFiles.Write(Path.Combine(root, "lease-stop.json"), new { RunId = "another-run" });
            RunFiles.Wait(
                () => RunFiles.Read(Path.Combine(root, "lease-publisher.json"))["Stage"]!.GetValue<string>() == "Failed",
                TimeSpan.FromSeconds(10),
                "Publisher accepted another run's stop request.");
        }
        finally
        {
            Assert.ThrowsExactly<AggregateException>(publisher.Dispose);
        }
    }

    private LeasePublisher CreatePublisher(Process owner, DateTime deadline) => new(
        Path.Combine(AppContext.BaseDirectory, "ExperimentPayload"), root, root, runId, host, guest, deadline, owner);
}

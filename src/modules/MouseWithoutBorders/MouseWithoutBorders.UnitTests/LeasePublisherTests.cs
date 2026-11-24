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
    // The test envelope includes two cold shell initializations, not just lease operations.
    private const int FixtureLifetimeMinutes = 5;

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
        var deadline = DateTime.UtcNow.AddMinutes(FixtureLifetimeMinutes);
        using var publisher = CreatePublisher(owner, deadline);
        using var guestPublisher = CreatePublisher(owner, deadline, forGuest: true);
        publisher.WaitForReady();
        guestPublisher.WaitForReady();
        var first = Path.Combine(host.InputRoot, "leases", "00000002.json");
        var original = File.ReadAllBytes(first);
        using (var held = new FileStream(first, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            const int expectedSequence = 4;

            // No timer or callback in the owner is needed to keep both endpoints alive.
            RunFiles.Wait(
                () => File.Exists(Path.Combine(host.InputRoot, "leases", $"{expectedSequence:D8}.json.ready")) &&
                    File.Exists(Path.Combine(guest.InputRoot, "leases", $"{expectedSequence:D8}.json.ready")),
                TimeSpan.FromSeconds(30),
                "Both endpoints did not receive committed generations while the owner waited.",
                () =>
                {
                    publisher.ThrowIfFailed();
                    guestPublisher.ThrowIfFailed();
                });
            foreach (var channel in new[] { host, guest })
            {
                var path = Path.Combine(channel.InputRoot, "leases", $"{expectedSequence:D8}.json");
                Assert.IsTrue(File.Exists(path + ".ready"));
                var lease = RunFiles.Read(path);
                Assert.AreEqual(runId, lease["RunId"]!.GetValue<string>());
                Assert.AreEqual(expectedSequence, lease["Sequence"]!.GetValue<int>());
            }

            CollectionAssert.AreEqual(original, File.ReadAllBytes(first));
        }

        publisher.Stop();
        guestPublisher.ThrowIfFailed();
        guestPublisher.Stop();
        Assert.AreEqual("Stopped", RunFiles.Read(Path.Combine(root, "Host-lease-publisher.json"))["Stage"]!.GetValue<string>());
        Assert.AreEqual("Stopped", RunFiles.Read(Path.Combine(root, "Guest-lease-publisher.json"))["Stage"]!.GetValue<string>());
    }

    [TestMethod]
    public void PublisherStatusUpdatesPreserveOpenSnapshots()
    {
        using var owner = Process.GetCurrentProcess();
        using var publisher = CreatePublisher(owner, DateTime.UtcNow.AddMinutes(FixtureLifetimeMinutes));
        publisher.WaitForReady();
        var path = Path.Combine(root, "Host-lease-publisher.json");
        using var snapshot = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var before = new MemoryStream();
        snapshot.CopyTo(before);
        var next = RunFiles.Read(path)["Sequence"]!.GetValue<int>() + 2;
        RunFiles.Wait(
            () => File.Exists(Path.Combine(host.InputRoot, "leases", $"{next:D8}.json.ready")),
            TimeSpan.FromSeconds(30),
            "Publisher did not advance while the previous status snapshot was open.",
            publisher.ThrowIfFailed);

        snapshot.Position = 0;
        using var after = new MemoryStream();
        snapshot.CopyTo(after);
        CollectionAssert.AreEqual(before.ToArray(), after.ToArray(), "Status publication must not truncate an already-open reader's snapshot.");
    }

    [TestMethod]
    public void FailingGuestPublicationDoesNotStarveHostLeases()
    {
        using var owner = Process.GetCurrentProcess();
        var deadline = DateTime.UtcNow.AddMinutes(FixtureLifetimeMinutes);
        using var hostPublisher = CreatePublisher(owner, deadline);
        hostPublisher.WaitForReady();
        Directory.Delete(Path.Combine(guest.InputRoot, "leases"), recursive: true);
        var guestPublisher = CreatePublisher(owner, deadline, forGuest: true);
        try
        {
            Assert.ThrowsExactly<InvalidOperationException>(guestPublisher.WaitForReady);
            var next = RunFiles.Read(Path.Combine(root, "Host-lease-publisher.json"))["Sequence"]!.GetValue<int>() + 2;
            RunFiles.Wait(
                () => File.Exists(Path.Combine(host.InputRoot, "leases", $"{next:D8}.json.ready")),
                TimeSpan.FromSeconds(30),
                "A guest publication failure stopped host liveness.",
                hostPublisher.ThrowIfFailed);
        }
        finally
        {
            Assert.ThrowsExactly<AggregateException>(guestPublisher.Dispose);
        }
    }

    [TestMethod]
    public void PublisherStopsWhenItsExactOwnerExits()
    {
        using var owner = Process.Start(new ProcessStartInfo(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"System32\WindowsPowerShell\v1.0\powershell.exe"),
            "-NoLogo -NoProfile -NonInteractive -Command \"[Threading.Thread]::Sleep(300000)\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        })!;
        try
        {
            using var publisher = CreatePublisher(owner, DateTime.UtcNow.AddMinutes(FixtureLifetimeMinutes));
            publisher.WaitForReady();
            owner.Kill();
            Assert.IsTrue(owner.WaitForExit(5000));
            RunFiles.Wait(
                () => RunFiles.Read(Path.Combine(root, "Host-lease-publisher.json"))["Stage"]!.GetValue<string>() == "ParentExited",
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
            var state = RunFiles.Read(Path.Combine(root, "Host-lease-publisher.json"));
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
        var publisher = CreatePublisher(owner, DateTime.UtcNow.AddMinutes(FixtureLifetimeMinutes));
        try
        {
            publisher.WaitForReady();
            RunFiles.Write(Path.Combine(root, "lease-stop-Host.json"), new { RunId = "another-run" });
            RunFiles.Wait(
                () => RunFiles.Read(Path.Combine(root, "Host-lease-publisher.json"))["Stage"]!.GetValue<string>() == "Failed",
                TimeSpan.FromSeconds(10),
                "Publisher accepted another run's stop request.");
        }
        finally
        {
            Assert.ThrowsExactly<AggregateException>(publisher.Dispose);
        }
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void PublishedFailureIsReportedOnlyForTheOwnedRun(bool matchingRun)
    {
        using var owner = Process.GetCurrentProcess();
        var reportedRun = matchingRun ? runId : Guid.NewGuid().ToString();
        var body = $$"""
            $config=Read-RunJson $ConfigurationPath
            Write-RunJson $config.OutputPath @{RunId='{{reportedRun}}';Stage='Failed';Error='synthetic publisher failure'}
            while(-not [IO.File]::Exists($config.StopPath+'.ready')){Start-Sleep -Milliseconds 20}
            exit 1
            """;
        var publisher = CreatePublisher(owner, DateTime.UtcNow.AddMinutes(FixtureLifetimeMinutes), publisherScript: body);
        using var child = Process.GetProcessById(publisher.ProcessId);
        try
        {
            RunFiles.Wait(
                () => File.Exists(Path.Combine(root, "Host-lease-publisher.json.ready")),
                TimeSpan.FromSeconds(30),
                "The fixture publisher did not commit its failure.");
            Assert.IsFalse(child.HasExited, "The fixture must remain alive while its failure is observed.");
            if (matchingRun)
            {
                var error = Assert.ThrowsExactly<InvalidOperationException>(publisher.ThrowIfFailed);
                StringAssert.Contains(error.Message, "synthetic publisher failure");
            }
            else
            {
                publisher.ThrowIfFailed();
            }

            Assert.IsFalse(child.HasExited, "Failure observation must not terminate the publisher.");
        }
        finally
        {
            Assert.ThrowsExactly<AggregateException>(publisher.Dispose);
        }
    }

    private LeasePublisher CreatePublisher(Process owner, DateTime deadline, bool forGuest = false, string? publisherScript = null)
    {
        var fixture = new PowerShellFixture(root);
        var script = Path.Combine(AppContext.BaseDirectory, "ExperimentPayload", "Publish-Leases.ps1");
        var support = Path.Combine(AppContext.BaseDirectory, "ExperimentPayload", "EndpointSupport.ps1");
        var initialize = $". '{support.Replace("'", "''", StringComparison.Ordinal)}'; $null=Read-RunJson $ConfigurationPath";
        var body = publisherScript ?? $"& '{script.Replace("'", "''", StringComparison.Ordinal)}' -ConfigurationPath $ConfigurationPath";
        var wrappedScript = "param([string]$ConfigurationPath)" + Environment.NewLine +
            fixture.Wrap(body, initialize);
        File.WriteAllText(
            Path.Combine(fixture.ScriptRoot, "Publish-Leases.ps1"),
            wrappedScript);
        var publisher = new LeasePublisher(
            fixture.ScriptRoot,
            root,
            root,
            runId,
            forGuest ? guest : host,
            forGuest ? "Guest" : "Host",
            deadline,
            owner);
        try
        {
            fixture.WaitForStartup(publisher.ThrowIfFailed);
            return publisher;
        }
        catch (Exception startupError)
        {
            try
            {
                publisher.Dispose();
            }
            catch (Exception cleanupError)
            {
                throw new AggregateException("Publisher fixture initialization and cleanup failed.", startupError, cleanupError);
            }

            throw;
        }
    }
}

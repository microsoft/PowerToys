// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.MouseWithoutBorders.UITests;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MouseWithoutBorders.UnitTests;

[TestClass]
public sealed class ExperimentChannelTests
{
    [TestMethod]
    public void CommittedReadinessBypassesDiscovery()
    {
        WithChannel((channel, runId) =>
        {
            RunFiles.Write(Path.Combine(channel.OutputRoot, "ready.json"), new { RunId = runId });
            var result = channel.WaitForReady(() => throw new InvalidOperationException("Discovery must not run after readiness."));
            Assert.AreEqual(runId, result["RunId"]!.GetValue<string>());
        });
    }

    [TestMethod]
    public void UncommittedReadinessDoesNotBypassDiscovery()
    {
        WithChannel((channel, runId) =>
        {
            var path = Path.Combine(channel.OutputRoot, "ready.json");
            File.WriteAllText(path, "{}");
            var calls = 0;
            channel.WaitForReady(() =>
            {
                calls++;
                RunFiles.Write(path, new { RunId = runId });
            });
            Assert.AreEqual(1, calls);
        });
    }

    [TestMethod]
    public void ReadinessStillRequiresMatchingRunIdentity()
    {
        WithChannel((channel, _) =>
        {
            RunFiles.Write(Path.Combine(channel.OutputRoot, "ready.json"), new { RunId = "another-run" });
            Assert.ThrowsExactly<InvalidDataException>(() => channel.WaitForReady());
        });
    }

    [TestMethod]
    public void FailedEndpointIsNotAcceptedEvenWithReadiness()
    {
        WithChannel((channel, runId) =>
        {
            RunFiles.Write(Path.Combine(channel.OutputRoot, "ready.json"), new { RunId = runId });
            RunFiles.Write(Path.Combine(channel.OutputRoot, "failed.json"), new { RunId = runId, Error = "Lease expired." });
            Assert.ThrowsExactly<InvalidOperationException>(() => channel.WaitForReady());
        });
    }

    [TestMethod]
    public void SuccessfulEvidenceLivesBesideTheDeploymentTree()
    {
        var results = Path.Combine(Path.GetTempPath(), "mwb-results");
        var deployment = Path.Combine(results, "Deploy_TestUser");

        Assert.AreEqual(results, RunFiles.PersistentResultsRoot(deployment));
    }

    [TestMethod]
    public void EvidenceIncludesSanitizedLogsWithoutRecursingIntoOtherFolders()
    {
        var root = Path.Combine(Path.GetTempPath(), "mwb-evidence-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "logs"));
            Directory.CreateDirectory(Path.Combine(root, "requests"));
            Directory.CreateDirectory(Path.Combine(root, "recovery"));
            var phase = Path.Combine(root, "phases.json");
            var screenshot = Path.Combine(root, "desktop.png");
            var log = Path.Combine(root, "logs", "MouseWithoutBorders_filtered.txt");
            foreach (var path in new[]
            {
                phase, screenshot, log, Path.Combine(root, "ignored.bin"),
                Path.Combine(root, "requests", "pending.json"), Path.Combine(root, "recovery", "settings.json"),
            })
            {
                File.WriteAllText(path, "fixture");
            }

            CollectionAssert.AreEquivalent(new[] { phase, screenshot, log }, RunFiles.EvidenceFiles(root).ToArray());
            Directory.Delete(Path.Combine(root, "logs"), recursive: true);
            CollectionAssert.AreEquivalent(new[] { phase, screenshot }, RunFiles.EvidenceFiles(root).ToArray());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void LeasePublicationUsesImmutableCorrelatedGenerations()
    {
        var root = Path.Combine(Path.GetTempPath(), "mwb-channel-" + Guid.NewGuid().ToString("N"));
        try
        {
            var runId = Guid.NewGuid().ToString();
            var input = Path.Combine(root, "input");
            var channel = new EndpointChannel(runId, input, Path.Combine(root, "output"));
            channel.WriteLease();
            Assert.AreEqual(1, channel.LeaseSequence);
            var first = Path.Combine(input, "leases", "00000001.json");
            Assert.IsTrue(File.Exists(first + ".ready"));
            var original = File.ReadAllBytes(first);
            using var held = new FileStream(first, FileMode.Open, FileAccess.Read, FileShare.Read);
            channel.WriteLease();
            Assert.AreEqual(2, channel.LeaseSequence);
            var second = RunFiles.Read(Path.Combine(input, "leases", "00000002.json"));

            Assert.AreEqual(runId, second["RunId"]!.GetValue<string>());
            Assert.AreEqual(2, second["Sequence"]!.GetValue<int>());
            Assert.IsTrue(File.Exists(Path.Combine(input, "leases", "00000002.json.ready")));
            CollectionAssert.AreEqual(original, File.ReadAllBytes(first));
            Assert.IsFalse(File.Exists(Path.Combine(input, "lease.json")));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static void WithChannel(Action<EndpointChannel, string> action)
    {
        var root = Path.Combine(Path.GetTempPath(), "mwb-channel-" + Guid.NewGuid().ToString("N"));
        try
        {
            var runId = Guid.NewGuid().ToString();
            var channel = new EndpointChannel(runId, Path.Combine(root, "input"), Path.Combine(root, "output"));
            RunFiles.Write(Path.Combine(channel.InputRoot, "bootstrap.json"), new { RunId = runId });
            channel.BeginBootstrap();
            action(channel, runId);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}

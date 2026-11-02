// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerScripts.Client;
using PowerScripts.Core.Activity;

namespace PowerScripts.Core.Tests;

[TestClass]
public sealed class ActivityTests
{
    private string _directory = string.Empty;

    [TestInitialize]
    public void Initialize() =>
        _directory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "PowerScripts-activity-" + Guid.NewGuid().ToString("N"))).FullName;

    [TestCleanup]
    public void Cleanup() => Directory.Delete(_directory, recursive: true);

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("Advanced Paste")]
    [DataRow("--source")]
    [DataRow("module\nsecret")]
    public void ClientRejectsMissingOrInvalidConsumerIdentity(string? consumerId) =>
        Assert.ThrowsException<ArgumentException>(() => new PowerScriptsClient(consumerId!));

    [TestMethod]
    public void RunLifecyclePersistsOnlyBoundedMetadata()
    {
        var store = new ActivityStore(_directory);
        var run = store.Start("script-id", new string('n', 1000), PowerScriptConsumer.AdvancedPaste);
        Assert.AreEqual(ActivityStatus.Running, new ActivityStore(_directory).List().Single().Status);
        store.Complete(run, 0);
        var completed = new ActivityStore(_directory).List().Single();
        Assert.AreEqual(ActivityStatus.Succeeded, completed.Status);
        Assert.AreEqual("advanced-paste", completed.ConsumerId);
        Assert.AreEqual("script-id", completed.ScriptId);
        Assert.AreEqual(256, completed.ScriptName.Length);
        Assert.IsTrue(completed.DurationMilliseconds >= 0);
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(_directory, "activity.json")));
        CollectionAssert.AreEquivalent(
            new[] { "id", "scriptId", "scriptName", "consumerId", "startedUtc", "finishedUtc", "status", "exitCode", "hostProcessId", "hostStartTimeUtcTicks", "durationMilliseconds" },
            json.RootElement[0].EnumerateObject().Select(property => property.Name).ToArray());
    }

    [TestMethod]
    public void ClearPreservesRunningInvocationsAndTheirSubsequentCompletion()
    {
        var store = new ActivityStore(_directory);
        var done = store.Start("failed", "Failed", "command-line");
        store.Complete(done, 7);
        Assert.AreEqual(ActivityStatus.Failed, store.List().Single().Status);
        Assert.AreEqual(7, store.List().Single().ExitCode);
        var running = store.Start("running", "Running", "advanced-paste");
        store.ClearCompleted();
        Assert.AreEqual(running.Id, store.List().Single().Id);
        store.Complete(running, 0);
        Assert.AreEqual(ActivityStatus.Succeeded, store.List().Single().Status);
        store.ClearCompleted();
        Assert.AreEqual(0, store.List().Count);
    }

    [TestMethod]
    public void DeadOrReusedHostIsInterruptedWithoutInventingDurationOrExitCode()
    {
        var store = new ActivityStore(_directory);
        var run = store.Start("interrupted", "Interrupted", "advanced-paste");
        File.WriteAllText(Path.Combine(_directory, "activity.json"),
            JsonSerializer.Serialize(new[] { run with { HostStartTimeUtcTicks = 0 } }, ActivityStore.JsonOptions));
        var interrupted = new ActivityStore(_directory).List().Single();
        Assert.AreEqual(ActivityStatus.Interrupted, interrupted.Status);
        Assert.IsNull(interrupted.ExitCode);
        Assert.IsNull(interrupted.FinishedUtc);
        Assert.IsNull(interrupted.DurationMilliseconds);
        store.ClearCompleted();
        Assert.AreEqual(0, store.List().Count);
    }

    [TestMethod]
    public void UnavailableOwnerStatusIsRetriedRatherThanPermanentlyFrozen()
    {
        var store = new ActivityStore(_directory);
        var run = store.Start("retry", "Retry", "test-harness");
        File.WriteAllText(Path.Combine(_directory, "activity.json"),
            JsonSerializer.Serialize(new[] { run with { Status = ActivityStatus.Unknown } }, ActivityStore.JsonOptions));
        Assert.AreEqual(ActivityStatus.Running, store.List().Single().Status);
    }

    [TestMethod]
    public async Task ConcurrentStoresDoNotLoseRunsOrCompletions()
    {
        await Task.WhenAll(Enumerable.Range(0, 24).Select(index => Task.Run(() =>
        {
            var store = new ActivityStore(index % 2 == 0 ? _directory : _directory + Path.DirectorySeparatorChar);
            var run = store.Start(index.ToString(), "Concurrent", "test-harness");
            store.Complete(run, index % 2);
        })));
        var records = new ActivityStore(_directory).List();
        Assert.AreEqual(24, records.Select(record => record.Id).Distinct().Count());
        Assert.AreEqual(12, records.Count(record => record.Status == ActivityStatus.Succeeded));
        Assert.AreEqual(12, records.Count(record => record.Status == ActivityStatus.Failed));
    }

    [TestMethod]
    public void RetentionKeepsNewestRunsEvenWhenAnOlderRunCompletesLate()
    {
        var store = new ActivityStore(_directory);
        var oldest = store.Start("oldest", "Oldest", "test-harness");
        for (var index = 0; index < ActivityStore.MaximumRecords; index++)
        {
            var recent = store.Start(index.ToString(), "Recent", "test-harness");
            store.Complete(recent, 0);
        }

        store.Complete(oldest, 0);
        var records = store.List();
        Assert.AreEqual(ActivityStore.MaximumRecords, records.Count);
        Assert.IsFalse(records.Any(record => record.Id == oldest.Id));
        CollectionAssert.AreEqual(
            records.OrderByDescending(record => record.StartedUtc).Select(record => record.Id).ToArray(),
            records.Select(record => record.Id).ToArray());
        store.ClearCompleted();
        store.Complete(oldest, 0);
        Assert.AreEqual(0, store.List().Count, "Late completion must not revive aged-out, cleared history.");
    }

    [TestMethod]
    public void HistoryIoErrorsAreNotEmptyHistoryAndFailedWritesCleanTheirTemporaryFile()
    {
        Directory.CreateDirectory(Path.Combine(_directory, "activity.json"));
        var store = new ActivityStore(_directory);
        Assert.ThrowsException<UnauthorizedAccessException>(() => store.List());
        Assert.ThrowsException<UnauthorizedAccessException>(() => store.Start("id", "Name", "test-harness"));
        Assert.AreEqual(0, Directory.GetFiles(_directory, "*.tmp").Length);
    }

    [TestMethod]
    public void CorruptHistoryIsReportedAndNeverSilentlyOverwritten()
    {
        var path = Path.Combine(_directory, "activity.json");
        File.WriteAllText(path, "corrupt");
        var store = new ActivityStore(_directory);
        Assert.ThrowsException<JsonException>(() => store.List());
        Assert.ThrowsException<JsonException>(() => store.Start("id", "Name", "test-harness"));
        Assert.AreEqual("corrupt", File.ReadAllText(path));
    }
}

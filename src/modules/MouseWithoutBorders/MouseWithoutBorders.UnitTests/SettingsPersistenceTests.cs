// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO.Abstractions;
using System.Text.Json;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MouseWithoutBorders.Core;
using MwbSettings = MouseWithoutBorders.Class.Settings;

namespace MouseWithoutBorders.UnitTests;

[TestClass]
[DoNotParallelize]
public sealed class SettingsPersistenceTests
{
    private const string RequestedKey = "SyntheticRequested1234";
    private const string PreviousKey = "SyntheticPrevious1234";

    [TestMethod]
    public void QueuedSavePreservesSnapshotWhenLoadedSettingsAreReplaced()
    {
        var writer = new RecordingSettingsUtils();
        var scheduler = new QueuedSaveScheduler();
        var original = CreateSettings(Encryption.MyKey);
        original.Version = "2.0";
        var settings = new MwbSettings(writer, original, scheduler);

        settings.SaveSettings();
        settings.ApplyLoadedSettings(CreateSettings(Encryption.MyKey), () => { }, () => { }, () => { }, () => { });
        scheduler.RunNext();

        using var saved = JsonDocument.Parse(writer.Writes.Single());
        Assert.AreEqual(original.Properties.SecurityKey.Value, GetKey(saved));
        Assert.AreEqual(original.Name, saved.RootElement.GetProperty("name").GetString());
        Assert.AreEqual("2.0", saved.RootElement.GetProperty("version").GetString());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void NewestSnapshotWinsRegardlessOfQueuedTaskOrder(bool olderRunsLast)
    {
        var writer = new RecordingSettingsUtils();
        var scheduler = new QueuedSaveScheduler();
        var original = CreateSettings(PreviousKey);
        var settings = new MwbSettings(writer, original, scheduler);

        settings.SaveSettings();
        original.Properties.SecurityKey.Value = RequestedKey;
        settings.SaveSettings();

        scheduler.RunNext(newest: olderRunsLast);
        scheduler.RunNext();

        Assert.AreEqual(olderRunsLast ? 1 : 2, writer.Writes.Count);
        using var saved = JsonDocument.Parse(writer.Writes.Last());
        Assert.AreEqual(RequestedKey, GetKey(saved));
    }

    [TestMethod]
    public void OlderIoRetryCannotOverwriteNewerSuccessfulSave()
    {
        var writer = new RecordingSettingsUtils();
        var scheduler = new QueuedSaveScheduler();
        var original = CreateSettings(PreviousKey);
        var settings = new MwbSettings(writer, original, scheduler);
        settings.SaveSettings();
        original.Properties.SecurityKey.Value = RequestedKey;
        settings.SaveSettings();

        writer.BeforeWrite = () =>
        {
            writer.BeforeWrite = null;
            scheduler.RunNext();
            throw new IOException("Synthetic first-write failure.");
        };
        scheduler.RunNext();

        using var saved = JsonDocument.Parse(writer.Writes.Single());
        Assert.AreEqual(RequestedKey, GetKey(saved));
    }

    [TestMethod]
    public void QueuedSavePreservesPropertiesWhenLiveValuesChange()
    {
        var writer = new RecordingSettingsUtils();
        var scheduler = new QueuedSaveScheduler();
        var original = CreateSettings(RequestedKey);
        original.Properties.MachineMatrixString.Add("OriginalPeer");
        original.Properties.AllowNonConsoleSessions.Value = true;
        var settings = new MwbSettings(writer, original, scheduler);

        settings.SaveSettings();
        original.Properties.SecurityKey.Value = PreviousKey;
        original.Properties.MachineMatrixString[0] = "LaterPeer";
        original.Properties.AllowNonConsoleSessions.Value = false;
        scheduler.RunNext();

        using var saved = JsonDocument.Parse(writer.Writes.Single());
        Assert.AreEqual(RequestedKey, GetKey(saved));
        var properties = saved.RootElement.GetProperty("properties");
        Assert.AreEqual("OriginalPeer", properties.GetProperty("MachineMatrixString")[0].GetString());
        Assert.IsTrue(properties.GetProperty("AllowNonConsoleSessions").GetProperty("value").GetBoolean());
    }

    private static string? GetKey(JsonDocument saved)
    {
        return saved.RootElement.GetProperty("properties").GetProperty("SecurityKey").GetProperty("value").GetString();
    }

    private static MouseWithoutBordersSettings CreateSettings(string key)
    {
        var settings = new MouseWithoutBordersSettings();
        settings.Properties.SecurityKey.Value = key;
        return settings;
    }

    private sealed class RecordingSettingsUtils : SettingsUtils
    {
        public List<string> Writes { get; } = new();

        public Action? BeforeWrite { get; set; }

        public RecordingSettingsUtils()
            : base(new FileSystem())
        {
        }

        public override void SaveSettings(string json, string powertoy = "", string fileName = DefaultFileName)
        {
            BeforeWrite?.Invoke();
            Writes.Add(json);
        }
    }

    private sealed class QueuedSaveScheduler : TaskScheduler
    {
        private readonly LinkedList<Task> tasks = new();

        public void RunNext(bool newest = false)
        {
            var node = newest ? tasks.Last! : tasks.First!;
            tasks.Remove(node);
            Assert.IsTrue(TryExecuteTask(node.Value));
            node.Value.GetAwaiter().GetResult();
        }

        protected override IEnumerable<Task> GetScheduledTasks() => tasks;

        protected override void QueueTask(Task task) => tasks.AddLast(task);

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued) => false;
    }
}

// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Concurrent;
using System.IO.Abstractions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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
        using var writer = new RecordingSettingsUtils(expectedWrites: 1);
        var gate = new System.Threading.Lock();
        var original = CreateSettings(RequestedKey);
        original.Version = "2.0";
        var settings = CreateIsolatedSettings(original, writer, gate);

        lock (gate)
        {
            settings.SaveSettings();
            SetField(settings, "_settings", CreateSettings(PreviousKey));
        }

        Assert.IsTrue(writer.Completed.Wait(TimeSpan.FromSeconds(10)), "The queued settings save did not complete.");
        using var saved = JsonDocument.Parse(writer.Writes.Single());
        Assert.AreEqual(RequestedKey, GetKey(saved));
        Assert.AreEqual(original.Name, saved.RootElement.GetProperty("name").GetString());
        Assert.AreEqual("2.0", saved.RootElement.GetProperty("version").GetString());
    }

    [TestMethod]
    public void EachQueuedSavePreservesItsOwnSnapshot()
    {
        using var writer = new RecordingSettingsUtils(expectedWrites: 2);
        var gate = new System.Threading.Lock();
        var original = CreateSettings(PreviousKey);
        var settings = CreateIsolatedSettings(original, writer, gate);

        lock (gate)
        {
            settings.SaveSettings();
            original.Properties.SecurityKey.Value = RequestedKey;
            settings.SaveSettings();
        }

        Assert.IsTrue(writer.Completed.Wait(TimeSpan.FromSeconds(10)), "Both queued settings saves must complete.");
        var keys = writer.Writes.Select(json =>
        {
            using var saved = JsonDocument.Parse(json);
            return GetKey(saved);
        }).ToArray();
        CollectionAssert.AreEquivalent(new[] { PreviousKey, RequestedKey }, keys);
    }

    [TestMethod]
    public void QueuedSavePreservesPropertiesWhenLiveValuesChange()
    {
        using var writer = new RecordingSettingsUtils(expectedWrites: 1);
        var gate = new System.Threading.Lock();
        var original = CreateSettings(RequestedKey);
        original.Properties.MachineMatrixString.Add("OriginalPeer");
        original.Properties.AllowNonConsoleSessions.Value = true;
        var settings = CreateIsolatedSettings(original, writer, gate);

        lock (gate)
        {
            settings.SaveSettings();
            original.Properties.SecurityKey.Value = PreviousKey;
            original.Properties.MachineMatrixString[0] = "LaterPeer";
            original.Properties.AllowNonConsoleSessions.Value = false;
        }

        Assert.IsTrue(writer.Completed.Wait(TimeSpan.FromSeconds(10)), "The queued settings save did not complete.");
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

    private static MwbSettings CreateIsolatedSettings(MouseWithoutBordersSettings snapshot, SettingsUtils writer, System.Threading.Lock gate)
    {
        // Avoid constructing a watcher for the real user profile; exercise the actual queued saver.
        var settings = (MwbSettings)RuntimeHelpers.GetUninitializedObject(typeof(MwbSettings));
        SetField(settings, "_settingsUtils", writer);
        SetField(settings, "_loadingSettingsLock", gate);
        SetField(settings, "_settings", snapshot);
        SetField(settings, "_properties", snapshot.Properties);
        return settings;
    }

    private static void SetField<T>(MwbSettings settings, string name, T value)
    {
        typeof(MwbSettings).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(settings, value);
    }

    private sealed class RecordingSettingsUtils : SettingsUtils, IDisposable
    {
        public ConcurrentQueue<string> Writes { get; } = new();

        public CountdownEvent Completed { get; }

        public RecordingSettingsUtils(int expectedWrites)
            : base(new FileSystem())
        {
            Completed = new CountdownEvent(expectedWrites);
        }

        public override void SaveSettings(string json, string powertoy = "", string fileName = DefaultFileName)
        {
            Writes.Enqueue(json);
            Completed.Signal();
        }

        public void Dispose()
        {
            Completed.Dispose();
        }
    }
}

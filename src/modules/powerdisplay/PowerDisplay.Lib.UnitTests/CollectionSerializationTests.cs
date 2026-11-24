// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Common.Serialization;
using PowerDisplay.Models;

namespace PowerDisplay.UnitTests;

[TestClass]
public class CollectionSerializationTests
{
    [TestMethod]
    [DataRow("{}")]
    [DataRow("{\"profiles\":null}")]
    [DataRow("{\"profiles\":[]}")]
    public void Profiles_IncompleteJson_CanAddAndRoundTrip(string json)
    {
        var profiles = JsonSerializer.Deserialize(json, ProfileSerializationContext.Default.PowerDisplayProfiles);
        Assert.IsNotNull(profiles);
        Assert.IsNotNull(profiles.Profiles);
        Assert.AreEqual(0, profiles.GetAssignedProfiles().Count());
        profiles.SetProfile(new PowerDisplayProfile("New", [new ProfileMonitorSetting("MON1", 50)]));

        var saved = JsonSerializer.Serialize(profiles, ProfileSerializationContext.Default.PowerDisplayProfiles);
        var reloaded = JsonSerializer.Deserialize(saved, ProfileSerializationContext.Default.PowerDisplayProfiles);
        Assert.IsNotNull(reloaded);
        Assert.AreEqual("MON1", reloaded.GetAssignedProfiles().Single().MonitorSettings[0].MonitorId);
    }

    [TestMethod]
    [DataRow("{\"id\":1,\"name\":\"Partial\"}")]
    [DataRow("{\"id\":1,\"name\":\"Partial\",\"monitorSettings\":null}")]
    [DataRow("{\"id\":1,\"name\":\"Partial\",\"monitorSettings\":[]}")]
    public void Profile_IncompleteJson_PreservesEditableCollection(string profileJson)
    {
        var profiles = JsonSerializer.Deserialize("{\"profiles\":[" + profileJson + "]}", ProfileSerializationContext.Default.PowerDisplayProfiles);
        Assert.IsNotNull(profiles);
        var profile = profiles.GetAssignedProfiles().Single();
        Assert.IsNotNull(profile.MonitorSettings);
        Assert.AreEqual(0, profile.MonitorSettings.Count);
        Assert.IsFalse(profile.IsValid());
        profile.MonitorSettings.Add(new ProfileMonitorSetting("MON1", 50));
        Assert.IsTrue(profile.IsValid());

        var saved = JsonSerializer.Serialize(profiles, ProfileSerializationContext.Default.PowerDisplayProfiles);
        var reloaded = JsonSerializer.Deserialize(saved, ProfileSerializationContext.Default.PowerDisplayProfiles);
        Assert.IsNotNull(reloaded);
        Assert.AreEqual(50, reloaded.GetAssignedProfiles().Single().MonitorSettings[0].Brightness);
    }

    [TestMethod]
    [DataRow("{}", 0)]
    [DataRow("{\"entries\":[]}", 0)]
    [DataRow("{\"entries\":[{\"edidId\":\"DELD1A8\",\"comments\":\"Example\"}]}", 1)]
    public void Blacklist_PreservesCollectionDefaultsAndValues(string json, int count)
    {
        var file = JsonSerializer.Deserialize(json, MonitorBlacklistSerializationContext.Default.BuiltInMonitorBlacklistFile);
        Assert.IsNotNull(file);
        Assert.IsNotNull(file.Entries);
        Assert.AreEqual(count, file.Entries.Count);
        var saved = JsonSerializer.Serialize(file, MonitorBlacklistSerializationContext.Default.BuiltInMonitorBlacklistFile);
        Assert.AreEqual(count, JsonSerializer.Deserialize(saved, MonitorBlacklistSerializationContext.Default.BuiltInMonitorBlacklistFile)!.Entries.Count);
    }

    [TestMethod]
    [DataRow("{}", 0)]
    [DataRow("{\"monitors\":{}}", 0)]
    [DataRow("{\"monitors\":{\"MON1\":{\"brightness\":50}}}", 1)]
    public void MonitorState_PreservesCollectionDefaultsAndValues(string json, int count)
    {
        var file = JsonSerializer.Deserialize(json, MonitorStateSerializationContext.Default.MonitorStateFile);
        Assert.IsNotNull(file);
        Assert.IsNotNull(file.Monitors);
        Assert.AreEqual(count, file.Monitors.Count);
        foreach (var entry in file.Monitors.Values)
        {
            Assert.IsNotNull(entry.KnownGoodVcpFeatures);
            Assert.AreEqual(0, entry.KnownGoodVcpFeatures.Count);
        }

        var saved = JsonSerializer.Serialize(file, MonitorStateSerializationContext.Default.MonitorStateFile);
        Assert.AreEqual(count, JsonSerializer.Deserialize(saved, MonitorStateSerializationContext.Default.MonitorStateFile)!.Monitors.Count);
    }

    [TestMethod]
    [DataRow("{}", 0)]
    [DataRow("{\"knownGoodVcpFeatures\":[]}", 0)]
    [DataRow("{\"knownGoodVcpFeatures\":[{\"code\":16,\"current\":50,\"maximum\":100}]}", 1)]
    public void KnownGoodFeatures_PreserveDefaultsAndValues(string json, int count)
    {
        var entry = JsonSerializer.Deserialize(json, MonitorStateSerializationContext.Default.MonitorStateEntry);
        Assert.IsNotNull(entry);
        Assert.IsNotNull(entry.KnownGoodVcpFeatures);
        Assert.AreEqual(count, entry.KnownGoodVcpFeatures.Count);
        var saved = JsonSerializer.Serialize(entry, MonitorStateSerializationContext.Default.MonitorStateEntry);
        Assert.AreEqual(count, JsonSerializer.Deserialize(saved, MonitorStateSerializationContext.Default.MonitorStateEntry)!.KnownGoodVcpFeatures!.Count);
    }

    [TestMethod]
    public void OptionalPersistedCollections_ExplicitNullRetainsExistingSemantics()
    {
        Assert.IsNull(JsonSerializer.Deserialize("{\"entries\":null}", MonitorBlacklistSerializationContext.Default.BuiltInMonitorBlacklistFile)!.Entries);
        Assert.IsNull(JsonSerializer.Deserialize("{\"monitors\":null}", MonitorStateSerializationContext.Default.MonitorStateFile)!.Monitors);
        Assert.IsNull(JsonSerializer.Deserialize("{\"knownGoodVcpFeatures\":null}", MonitorStateSerializationContext.Default.MonitorStateEntry)!.KnownGoodVcpFeatures);
    }
}

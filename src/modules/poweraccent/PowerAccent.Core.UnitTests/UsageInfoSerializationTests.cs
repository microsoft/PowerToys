// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerAccent.Core.SerializationContext;

namespace PowerAccent.Core.UnitTests;

[TestClass]
public sealed class UsageInfoSerializationTests
{
    [DataTestMethod]
    [DataRow("{}")]
    [DataRow("{\"CharacterUsageCounters\":{},\"CharacterUsageTimestamp\":{}}")]
    public void DefaultDictionariesCanRecordAndPersistUsage(string json)
    {
        var context = SourceGenerationContext.Default.UsageInfoData;
        var usage = JsonSerializer.Deserialize(json, context);

        Assert.IsNotNull(usage);
        Assert.IsNotNull(usage.CharacterUsageCounters);
        Assert.IsNotNull(usage.CharacterUsageTimestamp);
        Assert.AreEqual(0, usage.CharacterUsageCounters.Count);
        Assert.AreEqual(0, usage.CharacterUsageTimestamp.Count);
        usage.CharacterUsageCounters["é"] = 2;
        usage.CharacterUsageTimestamp["é"] = 123;

        var saved = JsonSerializer.Serialize(usage, context);
        var reloaded = JsonSerializer.Deserialize(saved, context);

        Assert.IsNotNull(reloaded);
        Assert.AreEqual(2u, reloaded.CharacterUsageCounters["é"]);
        Assert.AreEqual(123L, reloaded.CharacterUsageTimestamp["é"]);
    }

    [TestMethod]
    public void GeneratedContextDeserializesPopulatedDictionaries()
    {
        const string json = "{\"CharacterUsageCounters\":{\"é\":7},\"CharacterUsageTimestamp\":{\"é\":456}}";
        var context = SourceGenerationContext.Default.UsageInfoData;
        var usage = JsonSerializer.Deserialize(json, context);

        Assert.IsNotNull(usage);
        Assert.AreEqual(7u, usage.CharacterUsageCounters["é"]);
        Assert.AreEqual(456L, usage.CharacterUsageTimestamp["é"]);
    }

    [TestMethod]
    public void ExplicitNullDictionariesKeepTheirExistingMeaning()
    {
        var usage = JsonSerializer.Deserialize("{\"CharacterUsageCounters\":null,\"CharacterUsageTimestamp\":null}", SourceGenerationContext.Default.UsageInfoData);

        Assert.IsNotNull(usage);
        Assert.IsNull(usage.CharacterUsageCounters);
        Assert.IsNull(usage.CharacterUsageTimestamp);
    }
}

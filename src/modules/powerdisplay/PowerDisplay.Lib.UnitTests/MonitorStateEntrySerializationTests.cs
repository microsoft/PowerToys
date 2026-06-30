// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Common.Models;
using PowerDisplay.Common.Serialization;

namespace PowerDisplay.UnitTests;

/// <summary>Serialization tests for the MonitorStateEntry VCP-code persistence field.</summary>
[TestClass]
public class MonitorStateEntrySerializationTests
{
    [TestMethod]
    public void VcpFeatureCodes_RoundTripsThroughStateContext()
    {
        var entry = new MonitorStateEntry
        {
            Brightness = 75,
            VcpFeatureCodes = new Dictionary<string, int> { ["brightness"] = 0x6B, ["volume"] = -1 },
        };

        var json = JsonSerializer.Serialize(entry, MonitorStateSerializationContext.Default.MonitorStateEntry);
        var roundTripped = JsonSerializer.Deserialize(json, MonitorStateSerializationContext.Default.MonitorStateEntry);

        Assert.IsNotNull(roundTripped);
        Assert.IsNotNull(roundTripped!.VcpFeatureCodes);
        Assert.AreEqual(0x6B, roundTripped.VcpFeatureCodes!["brightness"]);
        Assert.AreEqual(-1, roundTripped.VcpFeatureCodes["volume"]);
        StringAssert.Contains(json, "vcpFeatureCodes");
    }

    [TestMethod]
    public void NullVcpFeatureCodes_OmittedFromJson()
    {
        var entry = new MonitorStateEntry { Brightness = 50 };
        var json = JsonSerializer.Serialize(entry, MonitorStateSerializationContext.Default.MonitorStateEntry);
        Assert.IsFalse(json.Contains("vcpFeatureCodes"), "Null map must be omitted (WhenWritingNull).");
    }
}

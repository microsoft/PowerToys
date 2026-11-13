// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FileExplorerScripts.Core.Tests;

[TestClass]
public sealed class ExplorerSettingsTests
{
    [TestMethod]
    [DataRow("""{"properties":{}}""", 2)]
    [DataRow("""{"properties":{"powerscripts-status-delay-seconds":{"value":0}}}""", 0)]
    [DataRow("""{"properties":{"powerscripts-status-delay-seconds":{"value":7}}}""", 7)]
    [DataRow("""{"properties":{"powerscripts-status-delay-seconds":600}}""", 600)]
    public void StatusDelayReadsPersistedSettingAndPreservesLegacyDefault(string json, int seconds)
    {
        using var document = JsonDocument.Parse(json);
        Assert.AreEqual(TimeSpan.FromSeconds(seconds), ExplorerSettings.ReadStatusDelay(document.RootElement));
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(601)]
    [DataRow(1.5)]
    public void InvalidStatusDelayIsReportedInsteadOfSilentlyChangingBehavior(double seconds)
    {
        using var document = JsonDocument.Parse(
            JsonSerializer.Serialize(new { properties = new Dictionary<string, object> { ["powerscripts-status-delay-seconds"] = seconds } }));
        Assert.ThrowsExactly<InvalidDataException>(() => ExplorerSettings.ReadStatusDelay(document.RootElement));
    }
}

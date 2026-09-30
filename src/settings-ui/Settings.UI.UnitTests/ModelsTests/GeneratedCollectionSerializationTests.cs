// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Models;

namespace CommonLibTest;

[TestClass]
public class GeneratedCollectionSerializationTests
{
    [TestMethod]
    [DataRow("{}")]
    [DataRow("{\"valueList\":null}")]
    [DataRow("{\"valueList\":[]}")]
    public void VcpValues_IncompleteJson_RemainMutableAndRoundTrip(string json)
    {
        var info = JsonSerializer.Deserialize(json, SettingsSerializationContext.Default.VcpCodeDisplayInfo);
        Assert.IsNotNull(info);
        Assert.IsNotNull(info.ValueList);
        Assert.IsEmpty(info.ValueList);
        info.ValueList.Add(new VcpValueInfo { Value = "0x05", Name = "6500K" });
        var saved = JsonSerializer.Serialize(info, SettingsSerializationContext.Default.VcpCodeDisplayInfo);
        var reloaded = JsonSerializer.Deserialize(saved, SettingsSerializationContext.Default.VcpCodeDisplayInfo);
        Assert.IsNotNull(reloaded);
        Assert.AreEqual("6500K", reloaded.ValueList[0].Name);
    }

    [TestMethod]
    [DataRow("{}")]
    [DataRow("{\"monitors\":null,\"excluded_from_sync_monitor_ids\":null,\"custom_vcp_mappings\":null}")]
    [DataRow("{\"monitors\":[],\"excluded_from_sync_monitor_ids\":[],\"custom_vcp_mappings\":[]}")]
    public void PowerDisplay_IncompleteJson_RetainsMutableCollections(string json)
    {
        var settings = JsonSerializer.Deserialize(json, SettingsSerializationContext.Default.PowerDisplayProperties);
        Assert.IsNotNull(settings);
        Assert.IsNotNull(settings.Monitors);
        Assert.IsNotNull(settings.ExcludedFromSyncMonitorIds);
        Assert.IsNotNull(settings.CustomVcpMappings);
        settings.Monitors.Add(new MonitorInfo { Id = "MON1" });
        settings.ExcludedFromSyncMonitorIds.Add("MON1");
        settings.CustomVcpMappings.Add(new CustomVcpValueMapping { VcpCode = 20, Value = 5, CustomName = "Warm" });
        var saved = JsonSerializer.Serialize(settings, SettingsSerializationContext.Default.PowerDisplayProperties);
        var reloaded = JsonSerializer.Deserialize(saved, SettingsSerializationContext.Default.PowerDisplayProperties);
        Assert.IsNotNull(reloaded);
        Assert.AreEqual("MON1", reloaded.Monitors[0].Id);
        Assert.AreEqual("MON1", reloaded.ExcludedFromSyncMonitorIds[0]);
        Assert.AreEqual("Warm", reloaded.CustomVcpMappings[0].CustomName);
    }

    [TestMethod]
    [DataRow("{}")]
    [DataRow("{\"vcpCodesFormatted\":null}")]
    [DataRow("{\"vcpCodesFormatted\":[]}")]
    public void Monitor_IncompleteJson_RetainsVcpCollection(string json)
    {
        var monitor = JsonSerializer.Deserialize(json, SettingsSerializationContext.Default.MonitorInfo);
        Assert.IsNotNull(monitor);
        Assert.IsNotNull(monitor.VcpCodesFormatted);
        monitor.VcpCodesFormatted.Add(new VcpCodeDisplayInfo { Code = "0x14" });
        var saved = JsonSerializer.Serialize(monitor, SettingsSerializationContext.Default.MonitorInfo);
        var reloaded = JsonSerializer.Deserialize(saved, SettingsSerializationContext.Default.MonitorInfo);
        Assert.IsNotNull(reloaded);
        Assert.AreEqual("0x14", reloaded.VcpCodesFormatted[0].Code);
    }

    [TestMethod]
    [DataRow("{}")]
    [DataRow("{\"MachineMatrixString\":null}")]
    [DataRow("{\"MachineMatrixString\":[]}")]
    public void MouseWithoutBorders_IncompleteJson_RetainsMachineMatrix(string json)
    {
        var settings = JsonSerializer.Deserialize(json, SettingsSerializationContext.Default.MouseWithoutBordersProperties);
        Assert.IsNotNull(settings);
        Assert.IsNotNull(settings.MachineMatrixString);
        settings.MachineMatrixString.Add("Machine");
        var saved = JsonSerializer.Serialize(settings, SettingsSerializationContext.Default.MouseWithoutBordersProperties);
        var reloaded = JsonSerializer.Deserialize(saved, SettingsSerializationContext.Default.MouseWithoutBordersProperties);
        Assert.IsNotNull(reloaded);
        Assert.AreEqual("Machine", reloaded.MachineMatrixString[^1]);
    }

    [TestMethod]
    [DataRow("{}")]
    [DataRow("{\"providers\":null}")]
    [DataRow("{\"providers\":[]}")]
    public void PasteAi_IncompleteJson_RetainsProviders(string json)
    {
        var settings = JsonSerializer.Deserialize(json, SettingsSerializationContext.Default.PasteAIConfiguration);
        Assert.IsNotNull(settings);
        Assert.IsNotNull(settings.Providers);
        settings.Providers.Add(new PasteAIProviderDefinition { Id = "test-provider" });
        var saved = JsonSerializer.Serialize(settings, SettingsSerializationContext.Default.PasteAIConfiguration);
        var reloaded = JsonSerializer.Deserialize(saved, SettingsSerializationContext.Default.PasteAIConfiguration);
        Assert.IsNotNull(reloaded);
        Assert.AreEqual("test-provider", reloaded.Providers[^1].Id);
    }
}

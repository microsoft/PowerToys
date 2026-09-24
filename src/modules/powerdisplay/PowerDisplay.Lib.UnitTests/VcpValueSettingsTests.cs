// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Models;

namespace PowerDisplay.UnitTests;

[TestClass]
public class VcpValueSettingsTests
{
    private static readonly int[] OriginalColorPresetValues = { 0x05, 0x08 };
    private static readonly int[] UpdatedColorPresetValues = { 0x05, 0x06 };

    [TestMethod]
    public void Rule_SourceGeneratedJsonPersistsNumericCodeAndValues()
    {
        var rule = new VcpValueBlock { VcpCode = 0xE0, Values = new List<int> { 0x05, 0x1234 } };

        var json = JsonSerializer.Serialize(rule, VcpValueSerializationContext.Default.VcpValueBlock);
        using var document = JsonDocument.Parse(json);
        var restored = JsonSerializer.Deserialize(json, VcpValueSerializationContext.Default.VcpValueBlock);

        Assert.AreEqual(JsonValueKind.Number, document.RootElement.GetProperty("vcpCode").ValueKind);
        Assert.AreEqual(224, document.RootElement.GetProperty("vcpCode").GetInt32());
        Assert.AreEqual(JsonValueKind.Number, document.RootElement.GetProperty("values")[1].ValueKind);
        Assert.AreEqual(4660, document.RootElement.GetProperty("values")[1].GetInt32());
        Assert.IsNotNull(restored);
        Assert.AreEqual(rule.VcpCode, restored.VcpCode);
        CollectionAssert.AreEqual(rule.Values, restored.Values);
    }

    [TestMethod]
    [DataRow("{}")]
    [DataRow("{\"disabledVcpValues\":null}")]
    public void Monitor_LegacyAndNullJsonDefaultToEmptyRules(string json)
    {
        var monitor = JsonSerializer.Deserialize(json, SettingsSerializationContext.Default.MonitorInfo);

        Assert.IsNotNull(monitor);
        Assert.IsNotNull(monitor.DisabledVcpValues);
        Assert.AreEqual(0, monitor.DisabledVcpValues.Count);
    }

    [TestMethod]
    public void Monitor_SourceGeneratedJsonRoundTripPreservesRules()
    {
        var original = new MonitorInfo
        {
            Id = @"\\?\DISPLAY#SAM105C#5&abc&0&UID111",
            DisabledVcpValues = new List<VcpValueBlock>
            {
                new() { VcpCode = 0xD6, Values = new List<int> { 0x04, 0x05 } },
                new() { VcpCode = 0x60, Values = new List<int> { 0x11 } },
            },
        };

        var json = JsonSerializer.Serialize(original, SettingsSerializationContext.Default.MonitorInfo);
        using var document = JsonDocument.Parse(json);
        var rules = document.RootElement.GetProperty("disabledVcpValues");
        var restored = JsonSerializer.Deserialize(json, SettingsSerializationContext.Default.MonitorInfo);

        Assert.AreEqual(214, rules[0].GetProperty("vcpCode").GetInt32());
        Assert.AreEqual(5, rules[0].GetProperty("values")[1].GetInt32());
        Assert.IsNotNull(restored);
        Assert.AreEqual(original.Id, restored.Id);
        Assert.AreEqual(2, restored.DisabledVcpValues.Count);
        Assert.AreEqual((byte)0xD6, restored.DisabledVcpValues[0].VcpCode);
        CollectionAssert.AreEqual(original.DisabledVcpValues[0].Values, restored.DisabledVcpValues[0].Values);
        Assert.AreEqual((byte)0x60, restored.DisabledVcpValues[1].VcpCode);
    }

    [TestMethod]
    public void Rule_NullValuesJsonNormalizesToEmptyList()
    {
        const string json = "{\"vcpCode\":214,\"values\":null}";

        var rule = JsonSerializer.Deserialize(json, VcpValueSerializationContext.Default.VcpValueBlock);

        Assert.IsNotNull(rule);
        Assert.IsNotNull(rule.Values);
        Assert.AreEqual(0, rule.Values.Count);
    }

    [TestMethod]
    public void Monitor_UpdateFromCopiesRestrictionsWithoutSharingMutableLists()
    {
        var source = new MonitorInfo
        {
            DisabledVcpValues = new List<VcpValueBlock>
            {
                new() { VcpCode = 0xD6, Values = new List<int> { 0x05 } },
            },
        };
        var target = new MonitorInfo();
        var notifications = new List<string?>();
        target.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);

        target.UpdateFrom(source);

        Assert.AreEqual(1, target.DisabledVcpValues.Count);
        Assert.AreEqual((byte)0xD6, target.DisabledVcpValues[0].VcpCode);
        Assert.AreEqual(0x05, target.DisabledVcpValues[0].Values.Single());
        CollectionAssert.Contains(notifications, nameof(MonitorInfo.DisabledVcpValues));
        Assert.AreNotSame(source.DisabledVcpValues, target.DisabledVcpValues);
        Assert.AreNotSame(source.DisabledVcpValues[0].Values, target.DisabledVcpValues[0].Values);

        source.DisabledVcpValues[0].Values.Clear();

        Assert.AreEqual(0x05, target.DisabledVcpValues[0].Values.Single());
    }

    [TestMethod]
    public void Monitor_ReplacingRulesInvalidatesColorPresetsAndDoesNotReinsertBlockedCurrentValue()
    {
        var monitor = CreateColorPresetMonitor();
        var originalPresets = monitor.ColorPresetsForDisplay;

        monitor.DisabledVcpValues = new List<VcpValueBlock>
        {
            new() { VcpCode = 0x14, Values = new List<int> { 0x05 } },
        };

        Assert.AreNotSame(originalPresets, monitor.ColorPresetsForDisplay);
        Assert.AreEqual(0x08, monitor.ColorPresetsForDisplay.Single().VcpValue);
        Assert.AreEqual(0x05, monitor.ColorTemperatureVcp, "Restrictions must not change the observed current value.");
        Assert.AreEqual(2, monitor.VcpCodesFormatted[0].ValueList.Count, "Raw supported options remain available for the restriction editor.");

        monitor.DisabledVcpValues = null!;

        Assert.IsNotNull(monitor.DisabledVcpValues);
        CollectionAssert.AreEqual(OriginalColorPresetValues, monitor.ColorPresetsForDisplay.Select(preset => preset.VcpValue).ToArray());
    }

    [TestMethod]
    public void Monitor_SupportOptionsUpdateWhenValuesAndNamesChangeWithoutChangingCount()
    {
        var monitor = CreateColorPresetMonitor();
        var originalPresets = monitor.ColorPresetsForDisplay;

        monitor.VcpCodesFormatted = new List<VcpCodeDisplayInfo>
        {
            new()
            {
                Code = "0x14",
                ValueList = new List<VcpValueInfo>
                {
                    new() { Value = "0x05", Name = "Custom warm" },
                    new() { Value = "0x06", Name = "7500 K" },
                },
            },
        };

        Assert.AreNotSame(originalPresets, monitor.ColorPresetsForDisplay);
        CollectionAssert.AreEqual(UpdatedColorPresetValues, monitor.ColorPresetsForDisplay.Select(preset => preset.VcpValue).ToArray());
        Assert.AreEqual("Custom warm", monitor.VcpCodesFormatted[0].ValueList[0].Name);
        Assert.AreEqual("Custom warm", monitor.ColorPresetsForDisplay[0].DisplayName);
    }

    private static MonitorInfo CreateColorPresetMonitor()
    {
        return new MonitorInfo
        {
            SupportsColorTemperature = true,
            ColorTemperatureVcp = 0x05,
            VcpCodesFormatted = new List<VcpCodeDisplayInfo>
            {
                new()
                {
                    Code = "0x14",
                    ValueList = new List<VcpValueInfo>
                    {
                        new() { Value = "0x05", Name = "6500 K" },
                        new() { Value = "0x08", Name = "9300 K" },
                    },
                },
            },
        };
    }
}

// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.UI;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public class SettingsModelSerializationTests
{
    public static IEnumerable<object[]> PersistedSettingNames
    {
        get => Serialize(new SettingsModel()).Select(static setting => new object[] { setting.Key });
    }

    public static IEnumerable<object[]> BooleanSettingValues
    {
        get
        {
            return Serialize(new SettingsModel())
                .Where(static setting => setting.Value?.GetValueKind() is JsonValueKind.True or JsonValueKind.False)
                .SelectMany(static setting => new[] { new object[] { setting.Key, false }, new object[] { setting.Key, true } });
        }
    }

    [TestMethod]
    [DynamicData(nameof(PersistedSettingNames))]
    public void Deserialize_EmptyJson_UsesExpectedDefaults(string settingName)
    {
        var defaults = Serialize(new SettingsModel());
        var settings = JsonSerializer.Deserialize("{}", JsonSerializationContext.Default.SettingsModel);

        Assert.IsNotNull(settings);
        AssertExpectedDefault(settingName, defaults, Serialize(settings));
    }

    [TestMethod]
    [DynamicData(nameof(PersistedSettingNames))]
    public void Deserialize_OmittedSetting_UsesExpectedDefault(string settingName)
    {
        var defaults = Serialize(new SettingsModel());
        var json = defaults.DeepClone().AsObject();
        Assert.IsTrue(json.Remove(settingName));

        var settings = JsonSerializer.Deserialize(json, JsonSerializationContext.Default.SettingsModel);

        Assert.IsNotNull(settings);
        AssertExpectedDefault(settingName, defaults, Serialize(settings));
    }

    [TestMethod]
    [DynamicData(nameof(BooleanSettingValues))]
    public void Deserialize_ExplicitBoolean_PreservesValue(string settingName, bool enabled)
    {
        var json = new JsonObject { [settingName] = enabled };
        var settings = JsonSerializer.Deserialize(json, JsonSerializationContext.Default.SettingsModel);

        Assert.IsNotNull(settings);
        Assert.AreEqual(enabled, Serialize(settings)[settingName]?.GetValue<bool>(), settingName);
    }

    [TestMethod]
    [DataRow(nameof(SettingsModel.Hotkey), "null")]
    [DataRow(nameof(SettingsModel.Language), "null")]
    [DataRow(nameof(SettingsModel.Language), "\"\"")]
    [DataRow(nameof(SettingsModel.Language), "\"cs-CZ\"")]
    [DataRow(nameof(SettingsModel.AutoGoHomeInterval), "\"00:00:00\"")]
    [DataRow(nameof(SettingsModel.AutoGoHomeInterval), "\"00:05:00\"")]
    [DataRow(nameof(SettingsModel.CustomThemeColor), """{"A":0,"R":0,"G":0,"B":0}""")]
    [DataRow(nameof(SettingsModel.CustomThemeColor), """{"A":128,"R":10,"G":20,"B":30}""")]
    [DataRow(nameof(SettingsModel.CustomThemeColorIntensity), "0")]
    [DataRow(nameof(SettingsModel.CustomThemeColorIntensity), "75")]
    [DataRow(nameof(SettingsModel.BackgroundImageOpacity), "0")]
    [DataRow(nameof(SettingsModel.BackgroundImageOpacity), "60")]
    [DataRow(nameof(SettingsModel.BackdropOpacity), "0")]
    [DataRow(nameof(SettingsModel.BackdropOpacity), "80")]
    public void Deserialize_ExplicitValue_PreservesValue(string settingName, string jsonValue)
    {
        var expected = JsonNode.Parse(jsonValue);
        var json = new JsonObject { [settingName] = expected?.DeepClone() };
        var settings = JsonSerializer.Deserialize(json, JsonSerializationContext.Default.SettingsModel);

        Assert.IsNotNull(settings);
        var actual = Serialize(settings);
        Assert.IsTrue(actual.ContainsKey(settingName), settingName);
        Assert.IsTrue(JsonNode.DeepEquals(expected, actual[settingName]), settingName);
    }

    [TestMethod]
    public void Deserialize_ExplicitNullAndZero_ExposesSavedModelValues()
    {
        var settings = JsonSerializer.Deserialize(
            """{"Hotkey":null,"AutoGoHomeInterval":"00:00:00","CustomThemeColor":{"A":0,"R":0,"G":0,"B":0}}""",
            JsonSerializationContext.Default.SettingsModel);

        Assert.IsNotNull(settings);
        Assert.IsNull(settings.Hotkey);
        Assert.AreEqual(TimeSpan.Zero, settings.AutoGoHomeInterval);
        Assert.AreEqual(default(Color), settings.CustomThemeColor);
    }

    [TestMethod]
    public void Serialize_CustomModelValues_SurviveRoundTrip()
    {
        var original = new SettingsModel
        {
            Hotkey = new HotkeySettings(false, true, false, false, 0x41),
            AutoGoHomeInterval = TimeSpan.FromMinutes(5),
            CustomThemeColor = new Color { A = 128, R = 10, G = 20, B = 30 },
        };
        var json = Serialize(original);
        var roundTripped = JsonSerializer.Deserialize(json, JsonSerializationContext.Default.SettingsModel);

        Assert.IsNotNull(roundTripped);
        Assert.AreEqual(original.Hotkey, roundTripped.Hotkey);
        Assert.AreEqual(original.AutoGoHomeInterval, roundTripped.AutoGoHomeInterval);
        Assert.AreEqual(original.CustomThemeColor, roundTripped.CustomThemeColor);
    }

    private static JsonObject Serialize(SettingsModel settings)
    {
        var json = JsonSerializer.SerializeToNode(settings, JsonSerializationContext.Default.SettingsModel);
        Assert.IsNotNull(json);
        return json.AsObject();
    }

    private static void AssertExpectedDefault(string settingName, JsonObject defaults, JsonObject actual)
    {
        var expected = defaults[settingName];

        Assert.IsTrue(
            JsonNode.DeepEquals(expected, actual[settingName]),
            $"{settingName}: expected {expected?.ToJsonString() ?? "null"}, got {actual[settingName]?.ToJsonString() ?? "null"}.");
    }
}

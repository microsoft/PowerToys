// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public class SettingsModelSerializationTests
{
    // Existing differences for omitted settings
    private static readonly JsonObject LegacyMissingSettingValues = new()
    {
        [nameof(SettingsModel.Hotkey)] = null,
        [nameof(SettingsModel.HighlightSearchOnActivate)] = false,
        [nameof(SettingsModel.ShowSystemTrayIcon)] = false,
        [nameof(SettingsModel.Language)] = null,
        [nameof(SettingsModel.IgnoreShortcutWhenFullscreen)] = false,
        [nameof(SettingsModel.DisableAnimations)] = false,
        [nameof(SettingsModel.AutoGoHomeInterval)] = "00:00:00",
        [nameof(SettingsModel.CustomThemeColor)] = new JsonObject { ["A"] = 0, ["R"] = 0, ["G"] = 0, ["B"] = 0 },
        [nameof(SettingsModel.CustomThemeColorIntensity)] = 0,
        [nameof(SettingsModel.BackgroundImageOpacity)] = 0,
        [nameof(SettingsModel.BackdropOpacity)] = 0,
    };

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
    public void LegacyMissingSettingValues_OnlyContainsExistingDifferences()
    {
        var defaults = Serialize(new SettingsModel());

        foreach (var setting in LegacyMissingSettingValues)
        {
            Assert.IsTrue(defaults.ContainsKey(setting.Key), $"Remove the legacy baseline for missing setting {setting.Key}.");
            Assert.IsFalse(
                JsonNode.DeepEquals(defaults[setting.Key], setting.Value),
                $"{setting.Key} matches its declared default; remove its legacy baseline.");
        }
    }

    private static JsonObject Serialize(SettingsModel settings)
    {
        var json = JsonSerializer.SerializeToNode(settings, JsonSerializationContext.Default.SettingsModel);
        Assert.IsNotNull(json);
        return json.AsObject();
    }

    private static void AssertExpectedDefault(string settingName, JsonObject defaults, JsonObject actual)
    {
        var expected = LegacyMissingSettingValues.TryGetPropertyValue(settingName, out var legacyValue)
            ? legacyValue
            : defaults[settingName];

        Assert.IsTrue(
            JsonNode.DeepEquals(expected, actual[settingName]),
            $"{settingName}: expected {expected?.ToJsonString() ?? "null"}, got {actual[settingName]?.ToJsonString() ?? "null"}.");
    }
}

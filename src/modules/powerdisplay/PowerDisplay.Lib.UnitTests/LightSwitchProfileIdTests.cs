// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Models;

namespace PowerDisplay.UnitTests;

[TestClass]
public class LightSwitchProfileIdTests
{
    private static readonly Guid DayId = new("4f7d8a46-9e11-43e0-bbc6-450402bb8abc");
    private static readonly Guid NightId = new("a5f24452-4389-4f76-b6dd-4434f6c9c124");

    [TestMethod]
    public void ProfileIdProperty_SettingsCommandRoundTripPreservesUnresolvedNumericId()
    {
        var reference = new ProfileIdProperty { LegacyId = 7 };
        var representation = ICmdLineRepresentable.ToCmdRepr(typeof(ProfileIdProperty), reference);
        var restored = (ProfileIdProperty)ICmdLineRepresentable.ParseFor(typeof(ProfileIdProperty), representation);

        Assert.AreEqual("7", representation);
        Assert.IsNotNull(restored);
        Assert.AreEqual(Guid.Empty, restored.Value);
        Assert.AreEqual(7, restored.LegacyId);
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(restored, SettingsSerializationContext.Default.ProfileIdProperty));
        Assert.AreEqual(7, document.RootElement.GetProperty("value").GetInt32());
    }

    [TestMethod]
    [DataRow("4f7d8a46-9e11-43e0-bbc6-450402bb8abc")]
    [DataRow("00000000-0000-0000-0000-000000000000")]
    public void ProfileIdProperty_SettingsCommandRoundTripPreservesUuid(string uuid)
    {
        var reference = new ProfileIdProperty(Guid.Parse(uuid));
        var representation = ICmdLineRepresentable.ToCmdRepr(typeof(ProfileIdProperty), reference);
        var restored = (ProfileIdProperty)ICmdLineRepresentable.ParseFor(typeof(ProfileIdProperty), representation);

        Assert.AreEqual(uuid, representation);
        Assert.IsNotNull(restored);
        Assert.AreEqual(reference.Value, restored.Value);
        Assert.IsNull(restored.LegacyId);
    }

    [TestMethod]
    public void ProfileIdProperty_SettingsCommandAcceptsLegacyNone()
    {
        var restored = (ProfileIdProperty)ICmdLineRepresentable.ParseFor(typeof(ProfileIdProperty), "0");

        Assert.IsNotNull(restored);
        Assert.AreEqual(Guid.Empty, restored.Value);
        Assert.IsNull(restored.LegacyId);
    }

    [TestMethod]
    [DataRow("-1")]
    [DataRow("+7")]
    [DataRow("2147483648")]
    [DataRow("not-a-uuid")]
    public void ProfileIdProperty_SettingsCommandRejectsInvalidReference(string value)
    {
        Assert.IsFalse(ProfileIdProperty.TryParseFromCmd(value, out var result));
        Assert.IsNull(result);
    }

    [TestMethod]
    public void LegacyNumericReference_UnrelatedSaveAndClonePreserveIt()
    {
        var settings = JsonSerializer.Deserialize(
            """{"properties":{"darkModeProfileId":{"value":7},"lightModeProfileId":{"value":0}}}""",
            SettingsSerializationContext.Default.LightSwitchSettings)!;
        settings.Properties.ChangeApps.Value = false;
        var clone = (LightSwitchSettings)settings.Clone();
        var json = clone.ToJsonString();
        using var document = JsonDocument.Parse(json);

        Assert.AreEqual(Guid.Empty, clone.Properties.DarkModeProfileId.Value);
        Assert.AreEqual(7, clone.Properties.DarkModeProfileId.LegacyId);
        Assert.IsNull(clone.Properties.LightModeProfileId.LegacyId);
        Assert.AreEqual(7, document.RootElement.GetProperty("properties").GetProperty("darkModeProfileId").GetProperty("value").GetInt32());
        var reloaded = JsonSerializer.Deserialize(json, SettingsSerializationContext.Default.LightSwitchSettings)!;
        Assert.AreEqual(7, reloaded.Properties.DarkModeProfileId.LegacyId);
        Assert.IsFalse(reloaded.Properties.ChangeApps.Value);
        clone.Properties.DarkModeProfileId.LegacyId = 8;
        Assert.AreEqual(7, settings.Properties.DarkModeProfileId.LegacyId);
    }

    [TestMethod]
    public void ProfilesCommittedBeforeLightSwitchSave_RetryResolvesTheSameUuid()
    {
        const string oldSettings = """{"properties":{"enableDarkModeProfile":{"value":true},"darkModeProfileId":{"value":7}}}""";
        var profiles = new PowerDisplayProfiles
        {
            Profiles = new List<PowerDisplayProfile>
            {
                new PowerDisplayProfile("Night", new List<ProfileMonitorSetting>()) { Id = NightId, LegacyId = 7, Order = 0 },
            },
        };
        var persistedProfiles = JsonSerializer.Serialize(profiles, ProfileSerializationContext.Default.PowerDisplayProfiles);
        var firstAttempt = JsonSerializer.Deserialize(oldSettings, SettingsSerializationContext.Default.LightSwitchSettings)!;
        Assert.IsTrue(LightSwitchProfileReferenceHelper.ReconcileReferences(firstAttempt.Properties, profiles));

        // Simulate restarting after profiles.json was committed but the LightSwitch save failed.
        var retry = JsonSerializer.Deserialize(oldSettings, SettingsSerializationContext.Default.LightSwitchSettings)!;
        var reloadedProfiles = JsonSerializer.Deserialize(persistedProfiles, ProfileSerializationContext.Default.PowerDisplayProfiles)!;
        Assert.IsTrue(LightSwitchProfileReferenceHelper.ReconcileReferences(retry.Properties, reloadedProfiles));
        Assert.AreEqual(firstAttempt.Properties.DarkModeProfileId.Value, retry.Properties.DarkModeProfileId.Value);
        Assert.AreEqual(NightId, LightSwitchProfileReferenceHelper.GetProfileIdForTheme(retry.Properties, isLightMode: false));

        using var migrated = JsonDocument.Parse(retry.ToJsonString());
        Assert.AreEqual(NightId, migrated.RootElement.GetProperty("properties").GetProperty("darkModeProfileId").GetProperty("value").GetGuid());
        Assert.IsNull(retry.Properties.DarkModeProfileId.LegacyId);
    }

    [TestMethod]
    public void UnresolvedLegacyName_SurvivesAnUnrelatedSettingsSave()
    {
        var settings = new LightSwitchSettings();
        settings.Properties.DarkModeProfile.Value = "Night";
        Assert.IsFalse(LightSwitchProfileReferenceHelper.ReconcileReferences(settings.Properties, new PowerDisplayProfiles()));

        var reloaded = JsonSerializer.Deserialize(settings.ToJsonString(), SettingsSerializationContext.Default.LightSwitchSettings)!;
        Assert.AreEqual("Night", reloaded.Properties.DarkModeProfile.Value);
    }

    [TestMethod]
    public void ProfileIdProperty_ExplicitNoneWritesEmptyUuid()
    {
        var reference = new ProfileIdProperty { LegacyId = 7 };
        LightSwitchProfileReferenceHelper.SetProfileId(reference, new StringProperty("Night"), Guid.Empty);
        var json = JsonSerializer.Serialize(reference, SettingsSerializationContext.Default.ProfileIdProperty);
        using var document = JsonDocument.Parse(json);

        Assert.AreEqual(Guid.Empty, document.RootElement.GetProperty("value").GetGuid());
    }

    [TestMethod]
    [DataRow("{\"value\":-1}")]
    [DataRow("{\"value\":\"invalid-uuid\"}")]
    [DataRow("{\"value\":true}")]
    public void ProfileIdProperty_InvalidReference_Throws(string json)
    {
        Assert.ThrowsExactly<JsonException>(() => JsonSerializer.Deserialize(json, SettingsSerializationContext.Default.ProfileIdProperty));
    }

    [TestMethod]
    public void LightSwitchProperties_ProfileIds_RoundTripThroughJson_DefaultEmpty()
    {
        var props = new LightSwitchProperties();
        Assert.AreEqual(Guid.Empty, props.LightModeProfileId.Value);
        Assert.AreEqual(Guid.Empty, props.DarkModeProfileId.Value);

        props.LightModeProfileId.Value = DayId;
        props.DarkModeProfileId.Value = NightId;

        var json = JsonSerializer.Serialize(props);
        var back = JsonSerializer.Deserialize<LightSwitchProperties>(json);

        Assert.IsNotNull(back);
        Assert.AreEqual(DayId, back!.LightModeProfileId.Value);
        Assert.AreEqual(NightId, back.DarkModeProfileId.Value);
    }

    [TestMethod]
    public void LightSwitchSettings_Clone_PreservesProfileIds()
    {
        var settings = new LightSwitchSettings();
        settings.Properties.DarkModeProfileId.Value = NightId;
        settings.Properties.LightModeProfileId.Value = DayId;
        settings.Properties.DarkModeProfile.Value = "Night";
        settings.Properties.LightModeProfile.Value = "Day";

        var clone = (LightSwitchSettings)settings.Clone();

        Assert.AreEqual(NightId, clone.Properties.DarkModeProfileId.Value);
        Assert.AreEqual(DayId, clone.Properties.LightModeProfileId.Value);
        Assert.AreEqual("Night", clone.Properties.DarkModeProfile.Value);
        Assert.AreEqual("Day", clone.Properties.LightModeProfile.Value);
    }

    [TestMethod]
    public void LightSwitchProperties_LegacyProfileNames_RemainDeserializable()
    {
        const string json = """
            {
              "darkModeProfile": { "value": "Night" },
              "lightModeProfile": { "value": "Day" }
            }
            """;

        var properties = JsonSerializer.Deserialize<LightSwitchProperties>(json);

        Assert.IsNotNull(properties);
        Assert.AreEqual("Night", properties!.DarkModeProfile.Value);
        Assert.AreEqual("Day", properties.LightModeProfile.Value);
        Assert.AreEqual(Guid.Empty, properties.DarkModeProfileId.Value);
        Assert.AreEqual(Guid.Empty, properties.LightModeProfileId.Value);
    }

    [TestMethod]
    public void LightSwitchSettings_ToJsonString_RoundTripsAllKnownProperties()
    {
        var settings = new LightSwitchSettings();
        settings.Properties.ChangeSystem.Value = false;
        settings.Properties.ChangeApps.Value = false;
        settings.Properties.ScheduleMode.Value = "SunsetToSunrise";
        settings.Properties.LightTime.Value = 451;
        settings.Properties.DarkTime.Value = 1217;
        settings.Properties.SunriseOffset.Value = -15;
        settings.Properties.SunsetOffset.Value = 20;
        settings.Properties.Latitude.Value = "47.642";
        settings.Properties.Longitude.Value = "-122.136";
        settings.Properties.ToggleThemeHotkey.Value = new HotkeySettings(
            win: false,
            ctrl: true,
            alt: true,
            shift: false,
            code: 0x4C);
        settings.Properties.EnableDarkModeProfile.Value = true;
        settings.Properties.EnableLightModeProfile.Value = true;
        settings.Properties.DarkModeProfile.Value = "Night";
        settings.Properties.LightModeProfile.Value = "Day";
        settings.Properties.DarkModeProfileId.Value = NightId;
        settings.Properties.LightModeProfileId.Value = DayId;

        var json = settings.ToJsonString();
        var roundTripped = JsonSerializer.Deserialize(
            json,
            SettingsSerializationContext.Default.LightSwitchSettings);

        Assert.IsNotNull(roundTripped);
        Assert.AreEqual(settings.Name, roundTripped.Name);
        Assert.AreEqual(settings.Version, roundTripped.Version);
        Assert.IsFalse(roundTripped.Properties.ChangeSystem.Value);
        Assert.IsFalse(roundTripped.Properties.ChangeApps.Value);
        Assert.AreEqual("SunsetToSunrise", roundTripped.Properties.ScheduleMode.Value);
        Assert.AreEqual(451, roundTripped.Properties.LightTime.Value);
        Assert.AreEqual(1217, roundTripped.Properties.DarkTime.Value);
        Assert.AreEqual(-15, roundTripped.Properties.SunriseOffset.Value);
        Assert.AreEqual(20, roundTripped.Properties.SunsetOffset.Value);
        Assert.AreEqual("47.642", roundTripped.Properties.Latitude.Value);
        Assert.AreEqual("-122.136", roundTripped.Properties.Longitude.Value);
        Assert.IsFalse(roundTripped.Properties.ToggleThemeHotkey.Value.Win);
        Assert.IsTrue(roundTripped.Properties.ToggleThemeHotkey.Value.Ctrl);
        Assert.IsTrue(roundTripped.Properties.ToggleThemeHotkey.Value.Alt);
        Assert.IsFalse(roundTripped.Properties.ToggleThemeHotkey.Value.Shift);
        Assert.AreEqual(0x4C, roundTripped.Properties.ToggleThemeHotkey.Value.Code);
        Assert.IsTrue(roundTripped.Properties.EnableDarkModeProfile.Value);
        Assert.IsTrue(roundTripped.Properties.EnableLightModeProfile.Value);
        Assert.AreEqual("Night", roundTripped.Properties.DarkModeProfile.Value);
        Assert.AreEqual("Day", roundTripped.Properties.LightModeProfile.Value);
        Assert.AreEqual(NightId, roundTripped.Properties.DarkModeProfileId.Value);
        Assert.AreEqual(DayId, roundTripped.Properties.LightModeProfileId.Value);
    }
}

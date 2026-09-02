// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PowerDisplay.UnitTests;

[TestClass]
public class LightSwitchProfileIdTests
{
    [TestMethod]
    public void LightSwitchProperties_ProfileIds_RoundTripThroughJson_DefaultZero()
    {
        var props = new LightSwitchProperties();
        Assert.AreEqual(0, props.LightModeProfileId.Value);
        Assert.AreEqual(0, props.DarkModeProfileId.Value);

        props.LightModeProfileId.Value = 3;
        props.DarkModeProfileId.Value = 5;

        var json = JsonSerializer.Serialize(props);
        var back = JsonSerializer.Deserialize<LightSwitchProperties>(json);

        Assert.IsNotNull(back);
        Assert.AreEqual(3, back!.LightModeProfileId.Value);
        Assert.AreEqual(5, back.DarkModeProfileId.Value);
    }

    [TestMethod]
    public void LightSwitchSettings_Clone_PreservesProfileIds()
    {
        var settings = new LightSwitchSettings();
        settings.Properties.DarkModeProfileId.Value = 7;
        settings.Properties.LightModeProfileId.Value = 3;
        settings.Properties.DarkModeProfile.Value = "Night";
        settings.Properties.LightModeProfile.Value = "Day";

        var clone = (LightSwitchSettings)settings.Clone();

        Assert.AreEqual(7, clone.Properties.DarkModeProfileId.Value);
        Assert.AreEqual(3, clone.Properties.LightModeProfileId.Value);
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
        Assert.AreEqual(0, properties.DarkModeProfileId.Value);
        Assert.AreEqual(0, properties.LightModeProfileId.Value);
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
        settings.Properties.DarkModeProfileId.Value = 7;
        settings.Properties.LightModeProfileId.Value = 3;

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
        Assert.AreEqual(7, roundTripped.Properties.DarkModeProfileId.Value);
        Assert.AreEqual(3, roundTripped.Properties.LightModeProfileId.Value);
    }
}

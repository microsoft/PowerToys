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
}

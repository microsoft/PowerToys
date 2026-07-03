// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PowerDisplay.UnitTests;

/// <summary>
/// Covers the persisted shape of the mouse-wheel-increment setting on
/// <see cref="PowerDisplayProperties"/>: its default of 5 (the historical hardcoded step),
/// its snake_case JSON key, round-trip fidelity, and the forward-compatibility promise that
/// settings.json written before the feature existed deserializes to the default of 5 with no
/// migration.
/// </summary>
[TestClass]
public class MouseWheelIncrementSettingsTests
{
    [TestMethod]
    public void Default_IsFive()
    {
        var properties = new PowerDisplayProperties();

        Assert.AreEqual(5, properties.MouseWheelIncrement, "Default must preserve the historical hardcoded step of 5.");
    }

    [TestMethod]
    public void Deserialize_LegacyJsonMissingField_DefaultsToFive()
    {
        // A settings.json captured before this feature shipped has no mouse_wheel_increment key.
        // Deserializing must fall back to the constructor default of 5, not 0. System.Text.Json
        // calls the parameterless constructor (which sets MouseWheelIncrement = 5) and then fills
        // only the fields present in JSON. If PowerDisplayProperties ever gains a
        // [JsonConstructor]-annotated constructor, re-verify this "defaults to 5" behavior.
        const string legacyJson = """
        {
            "monitor_refresh_delay": 5,
            "restore_settings_on_startup": false,
            "show_system_tray_icon": true
        }
        """;

        var properties = JsonSerializer.Deserialize<PowerDisplayProperties>(legacyJson);

        Assert.IsNotNull(properties);
        Assert.AreEqual(5, properties.MouseWheelIncrement);
    }

    [TestMethod]
    public void RoundTrip_PreservesValue()
    {
        var original = new PowerDisplayProperties { MouseWheelIncrement = 15 };

        var json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<PowerDisplayProperties>(json);

        Assert.IsNotNull(restored);
        Assert.AreEqual(15, restored.MouseWheelIncrement);
    }

    [TestMethod]
    public void Serialize_UsesSnakeCaseJsonKey()
    {
        var properties = new PowerDisplayProperties { MouseWheelIncrement = 10 };

        var json = JsonSerializer.Serialize(properties);

        StringAssert.Contains(json, "\"mouse_wheel_increment\":10");
    }
}

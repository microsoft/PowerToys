// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Models;

namespace PowerDisplay.UnitTests;

[TestClass]
public class MouseWheelControlModeSettingsTests
{
    [TestMethod]
    public void Default_IsPrimaryDisplay()
    {
        var properties = new PowerDisplayProperties();

        Assert.AreEqual(MouseWheelControlMode.PrimaryDisplay, properties.MouseWheelControlMode);
    }

    [TestMethod]
    public void Deserialize_LegacyJsonMissingField_DefaultsToPrimaryDisplay()
    {
        const string legacyJson = """
        {
            "monitor_refresh_delay": 5,
            "mouse_wheel_increment": 5,
            "show_system_tray_icon": true
        }
        """;

        var properties = JsonSerializer.Deserialize<PowerDisplayProperties>(legacyJson);

        Assert.IsNotNull(properties);
        Assert.AreEqual(MouseWheelControlMode.PrimaryDisplay, properties.MouseWheelControlMode);
    }

    [TestMethod]
    public void RoundTrip_PreservesEverySupportedMode()
    {
        MouseWheelControlMode[] modes =
        [
            MouseWheelControlMode.Disabled,
            MouseWheelControlMode.PrimaryDisplay,
            MouseWheelControlMode.AllDisplays,
        ];

        foreach (var mode in modes)
        {
            var json = JsonSerializer.Serialize(new PowerDisplayProperties { MouseWheelControlMode = mode });
            var restored = JsonSerializer.Deserialize<PowerDisplayProperties>(json);

            Assert.IsNotNull(restored);
            Assert.AreEqual(mode, restored.MouseWheelControlMode);
        }
    }

    [TestMethod]
    public void Serialize_UsesSnakeCaseJsonKey()
    {
        var properties = new PowerDisplayProperties
        {
            MouseWheelControlMode = MouseWheelControlMode.AllDisplays,
        };

        var json = JsonSerializer.Serialize(properties);

        StringAssert.Contains(json, "\"mouse_wheel_control_mode\":2");
    }

    [TestMethod]
    public void Normalize_UnsupportedValue_ReturnsDisabled()
    {
        var unsupported = (MouseWheelControlMode)99;

        Assert.AreEqual(MouseWheelControlMode.Disabled, unsupported.Normalize());
    }
}

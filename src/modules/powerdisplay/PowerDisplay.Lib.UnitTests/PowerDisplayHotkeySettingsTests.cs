// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using System.Text.Json;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PowerDisplay.UnitTests;

[TestClass]
public class PowerDisplayHotkeySettingsTests
{
    [TestMethod]
    public void Defaults_LeaveAdjustmentShortcutsUnbound()
    {
        var properties = new PowerDisplayProperties();

        Assert.IsTrue(properties.ActivationShortcut.IsValid());
        Assert.IsFalse(properties.IncreaseBrightnessShortcut.IsValid());
        Assert.IsFalse(properties.DecreaseBrightnessShortcut.IsValid());
        Assert.IsFalse(properties.IncreaseContrastShortcut.IsValid());
        Assert.IsFalse(properties.DecreaseContrastShortcut.IsValid());
        Assert.IsFalse(properties.IncreaseVolumeShortcut.IsValid());
        Assert.IsFalse(properties.DecreaseVolumeShortcut.IsValid());
        Assert.IsFalse(properties.IncreaseSdrContentBrightnessShortcut.IsValid());
        Assert.IsFalse(properties.DecreaseSdrContentBrightnessShortcut.IsValid());
        Assert.IsFalse(properties.SdrContentBrightnessReplacesPrimarySlider);
    }

    [TestMethod]
    public void Deserialize_LegacyJsonMissingShortcuts_UsesUnboundDefaults()
    {
        const string legacyJson = """
        {
            "monitor_refresh_delay": 5,
            "show_system_tray_icon": true
        }
        """;

        var properties = JsonSerializer.Deserialize<PowerDisplayProperties>(legacyJson);

        Assert.IsNotNull(properties);
        Assert.IsFalse(properties.IncreaseBrightnessShortcut.IsValid());
        Assert.IsFalse(properties.DecreaseSdrContentBrightnessShortcut.IsValid());
        Assert.IsFalse(properties.SdrContentBrightnessReplacesPrimarySlider);
    }

    [TestMethod]
    public void RoundTrip_PreservesAdjustmentShortcut()
    {
        var original = new PowerDisplayProperties
        {
            IncreaseContrastShortcut = new HotkeySettings(true, true, false, false, 0xBB),
        };

        var json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<PowerDisplayProperties>(json);

        Assert.IsNotNull(restored);
        Assert.AreEqual(original.IncreaseContrastShortcut, restored.IncreaseContrastShortcut);
        StringAssert.Contains(json, "\"increase_contrast_shortcut\"");
    }

    [TestMethod]
    public void RoundTrip_PreservesSdrPrimarySliderPreference()
    {
        var original = new PowerDisplayProperties
        {
            SdrContentBrightnessReplacesPrimarySlider = true,
        };

        var json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<PowerDisplayProperties>(json);

        Assert.IsNotNull(restored);
        Assert.IsTrue(restored.SdrContentBrightnessReplacesPrimarySlider);
        StringAssert.Contains(json, "\"sdr_content_brightness_replaces_primary_slider\"");
    }

    [TestMethod]
    public void HotkeyAccessors_ExposeActivationAndAllAdjustmentActions()
    {
        var accessors = new PowerDisplaySettings().GetAllHotkeyAccessors();

        Assert.AreEqual(9, accessors.Length);
        Assert.IsTrue(accessors.All(accessor => accessor.Value != null));
    }
}

// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PowerDisplay.UnitTests;

/// <summary>
/// Covers the persisted shape of the "follow built-in display brightness" setting on
/// <see cref="PowerDisplayProperties"/>: off by default, absent from older settings files
/// without needing a migration, and stored under a snake_case key.
/// </summary>
[TestClass]
public class FollowBuiltInBrightnessSettingsTests
{
    [TestMethod]
    public void Defaults_FollowDisabled()
    {
        Assert.IsFalse(new PowerDisplayProperties().FollowBuiltInDisplayBrightness);
    }

    [TestMethod]
    public void Deserialize_LegacyJsonMissingField_DefaultsToDisabled()
    {
        const string legacyJson = """
        {
            "monitor_refresh_delay": 5,
            "linked_levels_active": true
        }
        """;

        var properties = JsonSerializer.Deserialize<PowerDisplayProperties>(legacyJson);

        Assert.IsNotNull(properties);
        Assert.IsFalse(properties.FollowBuiltInDisplayBrightness);
        Assert.IsTrue(properties.LinkedLevelsActive);
    }

    [TestMethod]
    public void RoundTrip_UsesSnakeCaseKeyAndPreservesValue()
    {
        var original = new PowerDisplayProperties { FollowBuiltInDisplayBrightness = true };

        var json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<PowerDisplayProperties>(json);

        StringAssert.Contains(json, "\"follow_builtin_display_brightness\":true");
        Assert.IsNotNull(restored);
        Assert.IsTrue(restored.FollowBuiltInDisplayBrightness);
    }
}

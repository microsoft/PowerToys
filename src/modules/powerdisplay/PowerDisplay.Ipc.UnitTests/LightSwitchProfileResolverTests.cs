// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Models;

namespace PowerDisplay.Ipc.UnitTests;

[TestClass]
public class LightSwitchProfileResolverTests
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

    private static PowerDisplayProfiles ProfilesWith(params (int Id, string Name)[] items)
    {
        var p = new PowerDisplayProfiles();
        foreach (var (id, name) in items)
        {
            var prof = new PowerDisplayProfile(name, new List<ProfileMonitorSetting>
            {
                new ProfileMonitorSetting("MON1", 50, null, null, null),
            });
            prof.Id = id;
            p.Profiles.Add(prof);
        }

        return p;
    }

    [TestMethod]
    public void Resolve_PrefersExistingId()
    {
        var profiles = ProfilesWith((3, "A"), (4, "B"));
        Assert.AreEqual(3, PowerDisplay.Services.LightSwitchProfileResolver.Resolve(3, "stale-name", profiles));
    }

    [TestMethod]
    public void Resolve_IdMissing_ReturnsNull_DoesNotFallBackToName()
    {
        var profiles = ProfilesWith((3, "A"));
        Assert.IsNull(PowerDisplay.Services.LightSwitchProfileResolver.Resolve(9, "A", profiles));
    }

    [TestMethod]
    public void Resolve_ZeroId_FallsBackToName_FirstMatch()
    {
        var profiles = ProfilesWith((3, "Dup"), (4, "Dup"));
        Assert.AreEqual(3, PowerDisplay.Services.LightSwitchProfileResolver.Resolve(0, "Dup", profiles));
    }

    [TestMethod]
    public void Resolve_ZeroId_NoneOrUnknownName_ReturnsNull()
    {
        var profiles = ProfilesWith((3, "A"));
        Assert.IsNull(PowerDisplay.Services.LightSwitchProfileResolver.Resolve(0, "(None)", profiles));
        Assert.IsNull(PowerDisplay.Services.LightSwitchProfileResolver.Resolve(0, string.Empty, profiles));
        Assert.IsNull(PowerDisplay.Services.LightSwitchProfileResolver.Resolve(0, "missing", profiles));
    }
}

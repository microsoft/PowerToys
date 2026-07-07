// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Models;

namespace PowerDisplay.UnitTests;

[TestClass]
public class PowerDisplayProfilesTests
{
    private static PowerDisplayProfile MakeProfile(string name, int id = 0)
    {
        var p = new PowerDisplayProfile(name, new List<ProfileMonitorSetting>
        {
            new ProfileMonitorSetting("MON1", 50, null, null, null),
        });
        p.Id = id;
        return p;
    }

    [TestMethod]
    public void IdAndNextId_RoundTripThroughJson_AndDefaultToZero()
    {
        var profiles = new PowerDisplayProfiles();
        var p = MakeProfile("Gaming", id: 7);
        profiles.Profiles.Add(p);
        profiles.NextId = 8;

        var json = JsonSerializer.Serialize(profiles, ProfileSerializationContext.Default.PowerDisplayProfiles);
        var back = JsonSerializer.Deserialize(json, ProfileSerializationContext.Default.PowerDisplayProfiles);

        Assert.IsNotNull(back);
        Assert.AreEqual(8, back!.NextId);
        Assert.AreEqual(7, back.Profiles[0].Id);
        Assert.AreEqual(0, new PowerDisplayProfile().Id);
        Assert.AreEqual(0, new PowerDisplayProfiles().NextId);
    }
}

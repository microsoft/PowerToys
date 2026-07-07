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

    [TestMethod]
    public void GetById_ReturnsMatch_OrNullForZeroAndMissing()
    {
        var profiles = new PowerDisplayProfiles();
        var a = MakeProfile("A", id: 1);
        var b = MakeProfile("B", id: 2);
        profiles.Profiles.Add(a);
        profiles.Profiles.Add(b);

        Assert.AreSame(b, profiles.GetById(2));
        Assert.IsNull(profiles.GetById(0));
        Assert.IsNull(profiles.GetById(99));
    }

    [TestMethod]
    public void RemoveProfileById_RemovesWhenPresent()
    {
        var profiles = new PowerDisplayProfiles();
        profiles.Profiles.Add(MakeProfile("A", id: 1));
        profiles.Profiles.Add(MakeProfile("B", id: 2));

        Assert.IsTrue(profiles.RemoveProfile(2));
        Assert.AreEqual(1, profiles.Profiles.Count);
        Assert.IsFalse(profiles.RemoveProfile(2));
    }

    [TestMethod]
    public void SetProfile_AssignsIncreasingIds_AndAllowsDuplicateNames()
    {
        var profiles = new PowerDisplayProfiles { NextId = 1 };
        var a = MakeProfile("Same");
        var b = MakeProfile("Same");

        profiles.SetProfile(a);
        profiles.SetProfile(b);

        Assert.AreEqual(1, a.Id);
        Assert.AreEqual(2, b.Id);
        Assert.AreEqual(3, profiles.NextId);
        Assert.AreEqual(2, profiles.Profiles.Count); // both kept: duplicate names allowed
    }

    [TestMethod]
    public void SetProfile_WithExplicitId_ReplacesSameIdAndHealsNextId()
    {
        var profiles = new PowerDisplayProfiles { NextId = 1 };
        var original = MakeProfile("A", id: 5);
        profiles.SetProfile(original); // explicit id 5

        var replacement = MakeProfile("A-edited", id: 5);
        profiles.SetProfile(replacement);

        Assert.AreEqual(1, profiles.Profiles.Count);
        Assert.AreSame(replacement, profiles.GetById(5));
        Assert.IsTrue(profiles.NextId > 5); // healed past the explicit id
    }

    [TestMethod]
    public void EnsureIds_BackfillsInOrder_SetsNextId_AndIsIdempotent()
    {
        var profiles = new PowerDisplayProfiles(); // NextId defaults to 0 (legacy file)
        profiles.Profiles.Add(MakeProfile("A")); // Id 0
        profiles.Profiles.Add(MakeProfile("B")); // Id 0

        Assert.IsTrue(profiles.EnsureIds());
        Assert.AreEqual(1, profiles.Profiles[0].Id);
        Assert.AreEqual(2, profiles.Profiles[1].Id);
        Assert.AreEqual(3, profiles.NextId);

        Assert.IsFalse(profiles.EnsureIds()); // second run: no change
    }

    [TestMethod]
    public void EnsureIds_SelfHealsNextId_AndPreservesExistingIds()
    {
        var profiles = new PowerDisplayProfiles { NextId = 2 }; // corrupt: <= existing max
        profiles.Profiles.Add(MakeProfile("A", id: 5));
        profiles.Profiles.Add(MakeProfile("B")); // Id 0

        Assert.IsTrue(profiles.EnsureIds());
        Assert.AreEqual(5, profiles.Profiles[0].Id); // preserved
        Assert.AreEqual(6, profiles.Profiles[1].Id); // assigned above max
        Assert.AreEqual(7, profiles.NextId);
    }
}

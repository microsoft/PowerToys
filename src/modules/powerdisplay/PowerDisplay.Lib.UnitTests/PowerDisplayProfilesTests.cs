// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Resources;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Models;

namespace PowerDisplay.UnitTests;

[TestClass]
public class PowerDisplayProfilesTests
{
    private static readonly string[] ExpectedAssignedProfileNames = { "Assigned" };

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
    public void GetAssignedProfiles_ExcludesNonPositiveIds()
    {
        var profiles = new PowerDisplayProfiles();
        profiles.Profiles.Add(MakeProfile("Negative", id: -1));
        profiles.Profiles.Add(MakeProfile("Legacy", id: 0));
        profiles.Profiles.Add(MakeProfile("Assigned", id: 4));

        var assigned = profiles.GetAssignedProfiles().Select(profile => profile.Name).ToArray();

        CollectionAssert.AreEqual(ExpectedAssignedProfileNames, assigned);
    }

    [TestMethod]
    public void GetLegacyProfileByName_ReturnsFirstCaseInsensitiveMatch()
    {
        var profiles = new PowerDisplayProfiles();
        var first = MakeProfile("Same", id: 1);
        var second = MakeProfile("same", id: 2);
        profiles.Profiles.Add(first);
        profiles.Profiles.Add(second);

        Assert.AreSame(first, profiles.GetLegacyProfileByName("SAME"));
    }

    [TestMethod]
    public void GetLegacyProfileByName_ReturnsNull_WhenNoCaseInsensitiveMatchExists()
    {
        var profiles = new PowerDisplayProfiles();
        profiles.Profiles.Add(MakeProfile("Same", id: 1));

        Assert.IsNull(profiles.GetLegacyProfileByName("Different"));
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

    [TestMethod]
    public void ProfileDisplayNameFormatter_UsesProvidedFormatOrder()
    {
        Assert.AreEqual(
            "#4: Gaming",
            ProfileDisplayNameFormatter.Format("Gaming", 4, "#{1}: {0}"));
    }

    [TestMethod]
    public void ProfileDisplayNameFormatter_InvalidFormat_FallsBackToNeutral()
    {
        Assert.AreEqual(
            "Gaming (#4)",
            ProfileDisplayNameFormatter.Format("Gaming", 4, "{0"));
    }

    [TestMethod]
    public void ProfileDisplayNameResource_ContainsNeutralFormat()
    {
        var resourceManager = new ResourceManager(
            "PowerDisplay.Models.Properties.Resources",
            typeof(PowerDisplayProfile).Assembly);

        Assert.AreEqual(
            "{0} (#{1})",
            resourceManager.GetString("ProfileDisplayNameFormat", CultureInfo.InvariantCulture));
    }

    [TestMethod]
    public void DisplayName_CombinesNameAndId()
    {
        var p = MakeProfile("Gaming", id: 4);
        Assert.AreEqual("Gaming (#4)", p.DisplayName);
    }

    [TestMethod]
    public void EnsureIds_ThenEditByAssignedId_ReplacesInsteadOfDuplicating()
    {
        // Legacy collection: profiles without ids (a pre-id profiles.json the app hasn't migrated).
        var profiles = new PowerDisplayProfiles();
        profiles.Profiles.Add(MakeProfile("A")); // Id 0
        profiles.Profiles.Add(MakeProfile("B")); // Id 0

        // The scanning migration back-fills ids before legacy profiles become editable, so the
        // edited profile carries a stable id and SetProfile replaces it in place instead of adding a copy.
        profiles.EnsureIds();
        var editedId = profiles.Profiles[0].Id;

        var edited = MakeProfile("A-renamed", id: editedId);
        profiles.SetProfile(edited);

        Assert.AreEqual(2, profiles.Profiles.Count); // no duplicate created
        Assert.AreSame(edited, profiles.GetById(editedId));
        Assert.AreEqual("A-renamed", profiles.GetById(editedId)!.Name);
    }

    [TestMethod]
    public void SetProfile_NewProfile_SelfHealsCorruptNextId_NoIdCollision()
    {
        // Corrupt/legacy counter: NextId sits at or below an id already in use. Adding new profiles
        // (Id == 0) must still hand out ids above the highest in use, never colliding with it, even
        // if SetProfile runs before EnsureIds has healed the counter.
        var profiles = new PowerDisplayProfiles { NextId = 1 };
        profiles.Profiles.Add(MakeProfile("Existing", id: 5));

        for (var i = 0; i < 6; i++)
        {
            profiles.SetProfile(MakeProfile("New"));
        }

        var ids = profiles.Profiles.Select(p => p.Id).ToList();
        Assert.AreEqual(ids.Count, ids.Distinct().Count(), "profile ids must be unique");
        Assert.IsTrue(profiles.NextId > profiles.Profiles.Max(p => p.Id));
    }
}

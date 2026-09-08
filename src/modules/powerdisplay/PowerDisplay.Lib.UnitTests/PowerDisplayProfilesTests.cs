// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Resources;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Models;

namespace PowerDisplay.UnitTests;

[TestClass]
public class PowerDisplayProfilesTests
{
    private static readonly string[] ExpectedAssignedProfileNames = { "Assigned" };

    private static PowerDisplayProfile MakeProfile(string name, int id = 0, int order = -1)
    {
        var p = new PowerDisplayProfile(name, new List<ProfileMonitorSetting>
        {
            new ProfileMonitorSetting("MON1", 50, null, null, null),
        });
        p.Id = id;
        p.Order = order;
        return p;
    }

    [TestMethod]
    public void IdOrderAndNextId_RoundTripThroughJson_AndUseExpectedDefaults()
    {
        var profiles = new PowerDisplayProfiles();
        var p = MakeProfile("Gaming", id: 7, order: 3);
        profiles.Profiles.Add(p);
        profiles.NextId = 8;

        var json = JsonSerializer.Serialize(profiles, ProfileSerializationContext.Default.PowerDisplayProfiles);
        var back = JsonSerializer.Deserialize(json, ProfileSerializationContext.Default.PowerDisplayProfiles);

        Assert.IsNotNull(back);
        Assert.AreEqual(8, back!.NextId);
        Assert.AreEqual(7, back.Profiles[0].Id);
        Assert.AreEqual(3, back.Profiles[0].Order);
        Assert.AreEqual(-1, new PowerDisplayProfile().Order);
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
        Assert.AreEqual(0, a.Order);
        Assert.AreEqual(1, b.Order);
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

    [TestMethod]
    public void GetAssignedProfiles_UsesOrderRegardlessOfArrayPosition()
    {
        var profiles = MakeOrderedProfiles();
        profiles.Profiles.Reverse();

        Assert.AreEqual("1,2,3,4", DisplayOrder(profiles));
    }

    [TestMethod]
    public void GetLegacyProfileByName_UsesFirstArrayMatchRegardlessOfDisplayOrder()
    {
        var profiles = MakeOrderedProfiles();
        profiles.Profiles[0].Name = "Same";
        profiles.Profiles[1].Name = "same";
        profiles.MoveProfileBefore(2, 1);

        Assert.AreSame(profiles.Profiles[0], profiles.GetLegacyProfileByName("SAME"));
        Assert.IsNull(profiles.GetLegacyProfileByName("Missing"));
    }

    [TestMethod]
    public void RemoveProfile_ClosesOrderGap()
    {
        var profiles = MakeOrderedProfiles();
        profiles.MoveProfileBefore(3, 1);

        Assert.IsTrue(profiles.RemoveProfile(1));
        Assert.IsFalse(profiles.RemoveProfile(1));
        Assert.IsFalse(profiles.RemoveProfile(0));

        Assert.AreEqual("3,2,4", DisplayOrder(profiles));
        Assert.AreEqual("0,1,2", string.Join(",", profiles.GetAssignedProfiles().Select(profile => profile.Order)));
        Assert.AreEqual(3, profiles.Profiles.Count);
    }

    [TestMethod]
    [DataRow(4, 1, "4,1,2,3")]
    [DataRow(1, 4, "2,3,1,4")]
    [DataRow(2, 1, "2,1,3,4")]
    [DataRow(2, 4, "1,3,2,4")]
    [DataRow(1, null, "2,3,4,1")]
    [DataRow(3, null, "1,2,4,3")]
    public void MoveProfileBefore_ChangesOnlyOrderAndPreservesPhysicalArray(int profileId, int? beforeProfileId, string expectedOrder)
    {
        var profiles = MakeOrderedProfiles();
        var originalArray = profiles.Profiles.ToArray();
        var originalContents = profiles.Profiles.ToDictionary(profile => profile.Id, SerializeContents);

        Assert.IsTrue(profiles.MoveProfileBefore(profileId, beforeProfileId));

        Assert.AreEqual(expectedOrder, DisplayOrder(profiles));
        Assert.AreEqual("0,1,2,3", string.Join(",", profiles.GetAssignedProfiles().Select(profile => profile.Order)));
        Assert.IsTrue(profiles.LastUpdated > DateTime.UnixEpoch);
        for (var index = 0; index < profiles.Profiles.Count; index++)
        {
            var profile = profiles.Profiles[index];
            Assert.AreSame(originalArray[index], profile);
            Assert.AreEqual(originalContents[profile.Id], SerializeContents(profile));
        }
    }

    [TestMethod]
    [DataRow(-1, 1)]
    [DataRow(1, -1)]
    [DataRow(0, 1)]
    [DataRow(1, 0)]
    [DataRow(99, 1)]
    [DataRow(1, 99)]
    [DataRow(1, 1)]
    [DataRow(1, 2)]
    [DataRow(4, null)]
    public void MoveProfileBefore_InvalidOrUnchangedPosition_LeavesCollectionUnchanged(int profileId, int? beforeProfileId)
    {
        var profiles = MakeOrderedProfiles();
        var originalJson = JsonSerializer.Serialize(profiles, ProfileSerializationContext.Default.PowerDisplayProfiles);

        Assert.IsFalse(profiles.MoveProfileBefore(profileId, beforeProfileId));

        Assert.AreEqual(originalJson, JsonSerializer.Serialize(profiles, ProfileSerializationContext.Default.PowerDisplayProfiles));
    }

    [TestMethod]
    public void MoveProfileBefore_EmptyOrSingleProfile_LeavesCollectionUnchanged()
    {
        var profiles = new PowerDisplayProfiles { LastUpdated = DateTime.UnixEpoch };
        Assert.IsFalse(profiles.MoveProfileBefore(1, null));
        Assert.IsFalse(profiles.MoveProfileBefore(1, 2));

        var profile = MakeProfile("Only", 1, order: 0);
        profiles.Profiles.Add(profile);

        Assert.IsFalse(profiles.MoveProfileBefore(1, null));
        Assert.IsFalse(profiles.MoveProfileBefore(1, 1));
        Assert.AreSame(profile, profiles.Profiles[0]);
        Assert.AreEqual(0, profile.Order);
        Assert.AreEqual(DateTime.UnixEpoch, profiles.LastUpdated);
    }

    [TestMethod]
    public void MoveProfileBefore_ShuffledArray_RoundTripsIndependentDisplayOrder()
    {
        var profiles = MakeOrderedProfiles();
        profiles.Profiles.Reverse();
        var originalArrayIds = profiles.Profiles.Select(profile => profile.Id).ToArray();

        Assert.IsTrue(profiles.MoveProfileBefore(2, 1));

        var json = JsonSerializer.Serialize(profiles, ProfileSerializationContext.Default.PowerDisplayProfiles);
        var restored = JsonSerializer.Deserialize(json, ProfileSerializationContext.Default.PowerDisplayProfiles);
        Assert.IsNotNull(restored);
        Assert.AreEqual("2,1,3,4", DisplayOrder(restored));
        CollectionAssert.AreEqual(originalArrayIds, restored.Profiles.Select(profile => profile.Id).ToArray());
        Assert.IsFalse(restored.EnsureIdsAndOrder());
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    public void SetProfile_AfterReordering_PreservesOrderAndArrayPosition(int profileId)
    {
        var profiles = MakeOrderedProfiles();
        profiles.MoveProfileBefore(3, 1);
        var existing = profiles.GetById(profileId)!;
        var originalOrder = existing.Order;
        var replacement = MakeProfile("Edited", existing.Id, order: 99);

        profiles.SetProfile(replacement);

        Assert.AreEqual("3,1,2,4", DisplayOrder(profiles));
        Assert.AreSame(replacement, profiles.Profiles[profileId - 1]);
        Assert.AreEqual(originalOrder, replacement.Order);
    }

    [TestMethod]
    public void SetProfile_NewProfileAfterReordering_AppendsToDisplayOrder()
    {
        var profiles = MakeOrderedProfiles();
        profiles.MoveProfileBefore(3, 1);
        var added = MakeProfile("New", order: 0);

        profiles.SetProfile(added);

        Assert.AreSame(added, profiles.GetAssignedProfiles().Last());
        Assert.AreEqual(4, added.Order);
    }

    [TestMethod]
    public void EnsureIdsAndOrder_BackfillsMissingIdsAndOrdersIdempotently()
    {
        const string json = """
            {"nextId":8,"profiles":[{"name":"Existing ID","id":7},{"name":"No ID"}]}
            """;
        var profiles = JsonSerializer.Deserialize(json, ProfileSerializationContext.Default.PowerDisplayProfiles)!;

        Assert.IsTrue(profiles.EnsureIdsAndOrder());

        Assert.AreEqual(7, profiles.Profiles[0].Id);
        Assert.AreEqual(8, profiles.Profiles[1].Id);
        Assert.AreEqual(9, profiles.NextId);
        Assert.AreEqual(0, profiles.Profiles[0].Order);
        Assert.AreEqual(1, profiles.Profiles[1].Order);
        var repairedJson = JsonSerializer.Serialize(profiles, ProfileSerializationContext.Default.PowerDisplayProfiles);
        Assert.IsFalse(profiles.EnsureIdsAndOrder());
        Assert.AreEqual(repairedJson, JsonSerializer.Serialize(profiles, ProfileSerializationContext.Default.PowerDisplayProfiles));
    }

    [TestMethod]
    public void EnsureIdsAndOrder_RepairsMissingDuplicateAndNegativeOrdersWithoutRearrangingArray()
    {
        var profiles = MakeOrderedProfiles();
        profiles.Profiles[0].Order = 5;
        profiles.Profiles[1].Order = -1;
        profiles.Profiles[2].Order = 5;
        profiles.Profiles[3].Order = -4;
        var originalArray = profiles.Profiles.ToArray();
        var originalContents = profiles.Profiles.ToDictionary(profile => profile.Id, SerializeContents);

        Assert.IsTrue(profiles.EnsureIdsAndOrder());

        Assert.AreEqual("2,4,1,3", DisplayOrder(profiles));
        Assert.AreEqual("0,1,2,3", string.Join(",", profiles.GetAssignedProfiles().Select(profile => profile.Order)));
        Assert.AreEqual(5, profiles.NextId);
        CollectionAssert.AreEqual(originalArray, profiles.Profiles.ToArray());
        foreach (var profile in profiles.Profiles)
        {
            Assert.AreEqual(originalContents[profile.Id], SerializeContents(profile));
        }

        var repairedJson = JsonSerializer.Serialize(profiles, ProfileSerializationContext.Default.PowerDisplayProfiles);
        Assert.IsFalse(profiles.EnsureIdsAndOrder());
        Assert.AreEqual(repairedJson, JsonSerializer.Serialize(profiles, ProfileSerializationContext.Default.PowerDisplayProfiles));
    }

    private static PowerDisplayProfiles MakeOrderedProfiles()
    {
        var profiles = new PowerDisplayProfiles { NextId = 5, LastUpdated = DateTime.UnixEpoch };
        for (var id = 1; id <= 4; id++)
        {
            var profile = MakeProfile("Same", id, order: id - 1);
            profile.CreatedDate = DateTime.UnixEpoch;
            profile.LastModified = DateTime.UnixEpoch;
            profiles.Profiles.Add(profile);
        }

        return profiles;
    }

    private static string DisplayOrder(PowerDisplayProfiles profiles)
        => string.Join(",", profiles.GetAssignedProfiles().Select(profile => profile.Id));

    private static string SerializeContents(PowerDisplayProfile profile)
    {
        var node = JsonNode.Parse(JsonSerializer.Serialize(profile, ProfileSerializationContext.Default.PowerDisplayProfile))!.AsObject();
        node.Remove("order");
        return node.ToJsonString();
    }
}

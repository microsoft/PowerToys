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
    private static readonly Guid[] ProfileIds =
    {
        Guid.Parse("11111111-1111-4111-8111-111111111111"),
        Guid.Parse("22222222-2222-4222-8222-222222222222"),
        Guid.Parse("33333333-3333-4333-8333-333333333333"),
        Guid.Parse("44444444-4444-4444-8444-444444444444"),
    };

    private static readonly Guid MissingId = Guid.Parse("99999999-9999-4999-8999-999999999999");

    [TestMethod]
    public void UuidOrderAndLegacyId_RoundTripThroughJson()
    {
        var profiles = new PowerDisplayProfiles();
        var profile = MakeProfile("Gaming", ProfileIds[0], order: 3);
        profile.LegacyId = 7;
        profiles.Profiles.Add(profile);
        profiles.Profiles.Add(MakeProfile("New", ProfileIds[1], order: 0));

        var json = JsonSerializer.Serialize(profiles, ProfileSerializationContext.Default.PowerDisplayProfiles);
        var restored = JsonSerializer.Deserialize(json, ProfileSerializationContext.Default.PowerDisplayProfiles);

        Assert.IsNotNull(restored);
        Assert.AreEqual(json, JsonSerializer.Serialize(restored, ProfileSerializationContext.Default.PowerDisplayProfiles));
        Assert.AreEqual(ProfileIds[0], restored.Profiles[0].Id);
        Assert.AreEqual(3, restored.Profiles[0].Order);
        Assert.AreEqual(7, restored.Profiles[0].LegacyId);
        Assert.AreEqual(Guid.Empty, new PowerDisplayProfile().Id);
        Assert.AreEqual(-1, new PowerDisplayProfile().Order);

        using var document = JsonDocument.Parse(json);
        var savedProfile = document.RootElement.GetProperty("profiles")[0];
        Assert.AreEqual(ProfileIds[0].ToString("D"), savedProfile.GetProperty("id").GetString());
        Assert.AreEqual(3, savedProfile.GetProperty("order").GetInt32());
        Assert.IsFalse(document.RootElement.GetProperty("profiles")[1].TryGetProperty("legacyId", out _));
        Assert.IsFalse(document.RootElement.TryGetProperty("nextId", out _));
    }

    [TestMethod]
    public void ProfileJsonConverter_LegacyInteger_RetainsMappingWithoutAssigningUuid()
    {
        const string json = """
            {"name":"Legacy","id":7,"monitorSettings":[{"monitorId":"MON1","brightness":60}]}
            """;

        var profile = JsonSerializer.Deserialize(json, ProfileSerializationContext.Default.PowerDisplayProfile);

        Assert.IsNotNull(profile);
        Assert.AreEqual(Guid.Empty, profile.Id);
        Assert.AreEqual(7, profile.LegacyId);
        Assert.AreEqual(-1, profile.Order);
        Assert.AreEqual(60, profile.MonitorSettings[0].Brightness);
    }

    [TestMethod]
    public void ProfileJsonConverter_MissingId_DoesNotAssignUuid()
    {
        const string json = """{"name":"Legacy","monitorSettings":[]}""";

        var profile = JsonSerializer.Deserialize(json, ProfileSerializationContext.Default.PowerDisplayProfile);

        Assert.IsNotNull(profile);
        Assert.AreEqual(Guid.Empty, profile.Id);
        Assert.IsNull(profile.LegacyId);
        Assert.AreEqual(-1, profile.Order);
    }

    [TestMethod]
    [DataRow("""{"id":"not-a-uuid"}""")]
    [DataRow("""{"id":1.5}""")]
    [DataRow("""{"id":true}""")]
    public void ProfileJsonConverter_InvalidId_ThrowsJsonException(string json)
    {
        Assert.ThrowsExactly<JsonException>(() =>
            JsonSerializer.Deserialize(json, ProfileSerializationContext.Default.PowerDisplayProfile));
    }

    [TestMethod]
    public void GetByIdAndLegacyId_ResolveTheirOwnIdentityTypes()
    {
        var profiles = MakeOrderedProfiles();

        Assert.AreSame(profiles.Profiles[1], profiles.GetById(ProfileIds[1]));
        Assert.AreSame(profiles.Profiles[1], profiles.GetByLegacyId(2));
        Assert.IsNull(profiles.GetById(Guid.Empty));
        Assert.IsNull(profiles.GetById(MissingId));
        Assert.IsNull(profiles.GetByLegacyId(0));
        Assert.IsNull(profiles.GetByLegacyId(-1));
        Assert.IsNull(profiles.GetByLegacyId(99));
    }

    [TestMethod]
    public void GetAssignedProfiles_UsesOrderAndExcludesUnassignedUuids()
    {
        var profiles = MakeOrderedProfiles();
        profiles.Profiles.Reverse();
        profiles.Profiles.Add(MakeProfile("Unassigned", order: 0));

        Assert.AreEqual("1,2,3,4", DisplayOrder(profiles));
    }

    [TestMethod]
    public void GetLegacyProfileByName_UsesFirstArrayMatchRegardlessOfDisplayOrder()
    {
        var profiles = MakeOrderedProfiles();
        profiles.Profiles[0].Name = "Same";
        profiles.Profiles[1].Name = "same";
        profiles.MoveProfileBefore(ProfileIds[1], ProfileIds[0]);

        Assert.AreSame(profiles.Profiles[0], profiles.GetLegacyProfileByName("SAME"));
        Assert.IsNull(profiles.GetLegacyProfileByName("Missing"));
    }

    [TestMethod]
    public void RemoveProfile_RemovesByUuidAndClosesOrderGap()
    {
        var profiles = MakeOrderedProfiles();
        profiles.MoveProfileBefore(ProfileIds[2], ProfileIds[0]);

        Assert.IsTrue(profiles.RemoveProfile(ProfileIds[0]));
        Assert.IsFalse(profiles.RemoveProfile(ProfileIds[0]));
        Assert.IsFalse(profiles.RemoveProfile(Guid.Empty));

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
    public void MoveProfileBefore_ChangesOnlyOrderAndPreservesPhysicalArray(int profileIndex, int? beforeIndex, string expectedOrder)
    {
        var profiles = MakeOrderedProfiles();
        var originalArray = profiles.Profiles.ToArray();
        var originalContents = profiles.Profiles.ToDictionary(profile => profile.Id, SerializeContents);

        Assert.IsTrue(profiles.MoveProfileBefore(FixtureId(profileIndex), beforeIndex.HasValue ? FixtureId(beforeIndex.Value) : null));

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
    [DataRow(0, 1)]
    [DataRow(1, 0)]
    [DataRow(99, 1)]
    [DataRow(1, 99)]
    [DataRow(1, 1)]
    [DataRow(1, 2)]
    [DataRow(4, null)]
    public void MoveProfileBefore_InvalidOrUnchangedPosition_LeavesCollectionUnchanged(int profileIndex, int? beforeIndex)
    {
        var profiles = MakeOrderedProfiles();
        var originalJson = JsonSerializer.Serialize(profiles, ProfileSerializationContext.Default.PowerDisplayProfiles);

        Assert.IsFalse(profiles.MoveProfileBefore(FixtureId(profileIndex), beforeIndex.HasValue ? FixtureId(beforeIndex.Value) : null));

        Assert.AreEqual(originalJson, JsonSerializer.Serialize(profiles, ProfileSerializationContext.Default.PowerDisplayProfiles));
    }

    [TestMethod]
    public void MoveProfileBefore_EmptyOrSingleProfile_LeavesCollectionUnchanged()
    {
        var profiles = new PowerDisplayProfiles { LastUpdated = DateTime.UnixEpoch };
        Assert.IsFalse(profiles.MoveProfileBefore(ProfileIds[0], null));
        Assert.IsFalse(profiles.MoveProfileBefore(ProfileIds[0], ProfileIds[1]));

        var profile = MakeProfile("Only", ProfileIds[0], order: 0);
        profiles.Profiles.Add(profile);

        Assert.IsFalse(profiles.MoveProfileBefore(ProfileIds[0], null));
        Assert.IsFalse(profiles.MoveProfileBefore(ProfileIds[0], ProfileIds[0]));
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

        Assert.IsTrue(profiles.MoveProfileBefore(ProfileIds[1], ProfileIds[0]));

        var json = JsonSerializer.Serialize(profiles, ProfileSerializationContext.Default.PowerDisplayProfiles);
        var restored = JsonSerializer.Deserialize(json, ProfileSerializationContext.Default.PowerDisplayProfiles);
        Assert.IsNotNull(restored);
        Assert.AreEqual("2,1,3,4", DisplayOrder(restored));
        CollectionAssert.AreEqual(originalArrayIds, restored.Profiles.Select(profile => profile.Id).ToArray());
        Assert.IsFalse(restored.EnsureIdsAndOrder());
    }

    [TestMethod]
    public void SetProfile_AssignsDistinctRandomUuidsAndAppendsOrder()
    {
        var profiles = new PowerDisplayProfiles();
        var first = MakeProfile("Same");
        var second = MakeProfile("Same");

        profiles.SetProfile(first);
        profiles.SetProfile(second);

        Assert.AreNotEqual(Guid.Empty, first.Id);
        Assert.AreNotEqual(Guid.Empty, second.Id);
        Assert.AreNotEqual(first.Id, second.Id);
        Assert.AreEqual(0, first.Order);
        Assert.AreEqual(1, second.Order);
        Assert.IsNull(first.LegacyId);
        Assert.IsNull(second.LegacyId);
        Assert.AreEqual(2, profiles.Profiles.Count);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    public void SetProfile_AfterReordering_PreservesOrderLegacyMappingAndArrayPosition(int profileIndex)
    {
        var profiles = MakeOrderedProfiles();
        profiles.MoveProfileBefore(ProfileIds[2], ProfileIds[0]);
        var existing = profiles.GetById(FixtureId(profileIndex))!;
        var originalOrder = existing.Order;
        var replacement = MakeProfile("Edited", existing.Id, order: 99);
        replacement.LegacyId = 99;

        profiles.SetProfile(replacement);

        Assert.AreEqual("3,1,2,4", DisplayOrder(profiles));
        Assert.AreSame(replacement, profiles.Profiles[profileIndex - 1]);
        Assert.AreEqual(originalOrder, replacement.Order);
        Assert.AreEqual(profileIndex, replacement.LegacyId);
    }

    [TestMethod]
    public void SetProfile_NewProfileAfterReordering_AppendsToDisplayOrder()
    {
        var profiles = MakeOrderedProfiles();
        profiles.MoveProfileBefore(ProfileIds[2], ProfileIds[0]);
        var added = MakeProfile("New", order: 0);
        added.LegacyId = 1;

        profiles.SetProfile(added);

        Assert.AreSame(added, profiles.GetAssignedProfiles().Last());
        Assert.AreEqual(4, added.Order);
        Assert.IsNull(added.LegacyId);
        Assert.AreSame(profiles.Profiles[0], profiles.GetByLegacyId(1));
    }

    [TestMethod]
    public void EnsureIdsAndOrder_UpgradesLegacyIdsAndMissingOrdersIdempotently()
    {
        const string json = """
            {"nextId":8,"profiles":[{"name":"Integer","id":7},{"name":"No ID"}]}
            """;
        var profiles = JsonSerializer.Deserialize(json, ProfileSerializationContext.Default.PowerDisplayProfiles)!;

        Assert.IsTrue(profiles.EnsureIdsAndOrder());
        var assignedIds = profiles.Profiles.Select(profile => profile.Id).ToArray();

        Assert.IsTrue(assignedIds.All(id => id != Guid.Empty));
        Assert.AreNotEqual(assignedIds[0], assignedIds[1]);
        Assert.AreEqual(0, profiles.Profiles[0].Order);
        Assert.AreEqual(1, profiles.Profiles[1].Order);
        Assert.AreEqual(7, profiles.Profiles[0].LegacyId);
        Assert.AreSame(profiles.Profiles[0], profiles.GetByLegacyId(7));
        Assert.IsFalse(profiles.EnsureIdsAndOrder());
        CollectionAssert.AreEqual(assignedIds, profiles.Profiles.Select(profile => profile.Id).ToArray());
    }

    [TestMethod]
    public void EnsureIdsAndOrder_RepairsDuplicateIdsAndOrdersWithoutRearrangingArray()
    {
        var profiles = new PowerDisplayProfiles();
        var first = MakeProfile("First", ProfileIds[0], order: 5);
        first.LegacyId = 7;
        var duplicate = MakeProfile("Duplicate", ProfileIds[0]);
        duplicate.LegacyId = 7;
        var missing = MakeProfile("Missing", order: 5);
        var negative = MakeProfile("Negative", ProfileIds[3], order: -4);
        negative.LegacyId = -1;
        profiles.Profiles.Add(first);
        profiles.Profiles.Add(duplicate);
        profiles.Profiles.Add(missing);
        profiles.Profiles.Add(negative);
        var originalArray = profiles.Profiles.ToArray();

        Assert.IsTrue(profiles.EnsureIdsAndOrder());

        Assert.AreEqual("Duplicate,Negative,First,Missing", string.Join(",", profiles.GetAssignedProfiles().Select(profile => profile.Name)));
        Assert.AreEqual("0,1,2,3", string.Join(",", profiles.GetAssignedProfiles().Select(profile => profile.Order)));
        Assert.AreEqual(ProfileIds[0], first.Id);
        Assert.AreEqual(4, profiles.Profiles.Select(profile => profile.Id).Distinct().Count());
        Assert.IsTrue(profiles.Profiles.All(profile => profile.Id != Guid.Empty));
        Assert.AreEqual(7, first.LegacyId);
        Assert.IsNull(duplicate.LegacyId);
        Assert.IsNull(negative.LegacyId);
        Assert.AreSame(first, profiles.GetByLegacyId(7));
        CollectionAssert.AreEqual(originalArray, profiles.Profiles.ToArray());
        Assert.IsTrue(profiles.Profiles.All(profile => profile.LastModified == DateTime.UnixEpoch));
        var repairedJson = JsonSerializer.Serialize(profiles, ProfileSerializationContext.Default.PowerDisplayProfiles);
        Assert.IsFalse(profiles.EnsureIdsAndOrder());
        Assert.AreEqual(repairedJson, JsonSerializer.Serialize(profiles, ProfileSerializationContext.Default.PowerDisplayProfiles));
    }

    [TestMethod]
    public void EnsureIdsAndOrder_SameLegacyIntegerInDifferentCollections_GeneratesDifferentUuids()
    {
        const string json = """{"profiles":[{"id":7,"name":"Legacy"}]}""";
        var first = JsonSerializer.Deserialize(json, ProfileSerializationContext.Default.PowerDisplayProfiles)!;
        var second = JsonSerializer.Deserialize(json, ProfileSerializationContext.Default.PowerDisplayProfiles)!;

        first.EnsureIdsAndOrder();
        second.EnsureIdsAndOrder();

        Assert.AreNotEqual(first.Profiles[0].Id, second.Profiles[0].Id);
    }

    [TestMethod]
    public void EnsureIdsAndOrder_ThenEditByUuid_ReplacesInsteadOfDuplicating()
    {
        var profiles = new PowerDisplayProfiles();
        profiles.Profiles.Add(MakeProfile("A"));
        profiles.Profiles.Add(MakeProfile("B"));
        profiles.EnsureIdsAndOrder();
        var editedId = profiles.Profiles[0].Id;
        var edited = MakeProfile("A-renamed", editedId);

        profiles.SetProfile(edited);

        Assert.AreEqual(2, profiles.Profiles.Count);
        Assert.AreSame(edited, profiles.GetById(editedId));
        Assert.AreSame(edited, profiles.Profiles[0]);
        Assert.AreEqual(0, edited.Order);
    }

    [TestMethod]
    public void ProfileDisplayNameFormatter_UsesProvidedFormatOrder()
    {
        Assert.AreEqual("#11111111: Gaming", ProfileDisplayNameFormatter.Format("Gaming", ProfileIds[0], "#{1}: {0}"));
    }

    [TestMethod]
    public void ProfileDisplayNameFormatter_InvalidFormat_FallsBackToNeutral()
    {
        Assert.AreEqual("Gaming (#11111111)", ProfileDisplayNameFormatter.Format("Gaming", ProfileIds[0], "{0"));
    }

    [TestMethod]
    public void ProfileDisplayNameResource_ContainsNeutralFormat()
    {
        var resourceManager = new ResourceManager("PowerDisplay.Models.Properties.Resources", typeof(PowerDisplayProfile).Assembly);
        Assert.AreEqual("{0} (#{1})", resourceManager.GetString("ProfileDisplayNameFormat", CultureInfo.InvariantCulture));
    }

    [TestMethod]
    public void DisplayName_UsesShortUuidAndOmitsUnassignedId()
    {
        Assert.AreEqual("Gaming (#11111111)", MakeProfile("Gaming", ProfileIds[0]).DisplayName);
        Assert.AreEqual("Gaming", MakeProfile("Gaming").DisplayName);
    }

    private static PowerDisplayProfile MakeProfile(string name, Guid id = default, int order = -1)
    {
        return new PowerDisplayProfile(name, new List<ProfileMonitorSetting>
        {
            new ProfileMonitorSetting("MON1", 50, 5, 60, 20),
        })
        {
            Id = id,
            Order = order,
            CreatedDate = DateTime.UnixEpoch,
            LastModified = DateTime.UnixEpoch,
        };
    }

    private static PowerDisplayProfiles MakeOrderedProfiles()
    {
        var profiles = new PowerDisplayProfiles { LastUpdated = DateTime.UnixEpoch };
        for (var index = 0; index < ProfileIds.Length; index++)
        {
            var profile = MakeProfile("Same", ProfileIds[index], index);
            profile.LegacyId = index + 1;
            profiles.Profiles.Add(profile);
        }

        return profiles;
    }

    private static Guid FixtureId(int index) => index == 0 ? Guid.Empty : index <= ProfileIds.Length ? ProfileIds[index - 1] : MissingId;

    private static string DisplayOrder(PowerDisplayProfiles profiles)
        => string.Join(",", profiles.GetAssignedProfiles().Select(profile => Array.IndexOf(ProfileIds, profile.Id) + 1));

    private static string SerializeContents(PowerDisplayProfile profile)
    {
        var node = JsonNode.Parse(JsonSerializer.Serialize(profile, ProfileSerializationContext.Default.PowerDisplayProfile))!.AsObject();
        node.Remove("order");
        return node.ToJsonString();
    }
}

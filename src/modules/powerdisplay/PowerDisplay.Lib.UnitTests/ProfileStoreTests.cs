// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Models;

namespace PowerDisplay.UnitTests;

[TestClass]
public class ProfileStoreTests
{
    private const string LegacyJson = """
        {"nextId":8,"profiles":[
          {"id":7,"name":"Legacy","monitorSettings":[{"monitorId":"MON1","brightness":60}],
           "createdDate":"1970-01-01T00:00:00Z","lastModified":"1970-01-01T00:00:00Z"},
          {"name":"Older","monitorSettings":[{"monitorId":"MON1","brightness":40}],
           "createdDate":"1970-01-01T00:00:00Z","lastModified":"1970-01-01T00:00:00Z"}]}
        """;

    private static readonly string[] ExpectedConcurrentProfileNames = { "First", "Second" };

    private static readonly Guid[] ProfileIds =
    {
        Guid.Parse("11111111-1111-4111-8111-111111111111"),
        Guid.Parse("22222222-2222-4222-8222-222222222222"),
        Guid.Parse("33333333-3333-4333-8333-333333333333"),
    };

    private string _tempDir = string.Empty;
    private string _profilesPath = string.Empty;
    private string _mutexName = string.Empty;

    [TestInitialize]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"pd-profile-store-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _profilesPath = Path.Combine(_tempDir, "profiles.json");
        _mutexName = $@"Local\PowerToys_PowerDisplay_ProfileStore_Test_{Guid.NewGuid():N}";
    }

    [TestCleanup]
    public void TearDown()
    {
        try
        {
            if (File.Exists(_profilesPath))
            {
                File.SetAttributes(_profilesPath, FileAttributes.Normal);
            }

            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup.
        }
    }

    [TestMethod]
    public void LoadProfiles_MigratesLegacyIdsAndOrderBeforeReturning()
    {
        File.WriteAllText(_profilesPath, LegacyJson);
        var store = CreateStore();

        var loaded = store.LoadProfiles();

        Assert.AreEqual(2, loaded.Profiles.Count);
        Assert.IsTrue(loaded.Profiles.All(profile => profile.Id != Guid.Empty));
        Assert.AreNotEqual(loaded.Profiles[0].Id, loaded.Profiles[1].Id);
        Assert.AreEqual(7, loaded.Profiles[0].LegacyId);
        Assert.IsNull(loaded.Profiles[1].LegacyId);
        Assert.AreEqual("0,1", string.Join(",", loaded.Profiles.Select(profile => profile.Order)));
        Assert.IsTrue(loaded.Profiles.All(profile => profile.LastModified == DateTime.UnixEpoch));
        var savedJson = File.ReadAllText(_profilesPath);
        using var saved = JsonDocument.Parse(savedJson);
        Assert.AreEqual(loaded.Profiles[0].Id.ToString("D"), saved.RootElement.GetProperty("profiles")[0].GetProperty("id").GetString());
        Assert.IsFalse(saved.RootElement.TryGetProperty("nextId", out _));

        var reloaded = CreateStore().LoadProfiles();
        CollectionAssert.AreEqual(loaded.Profiles.Select(profile => profile.Id).ToArray(), reloaded.Profiles.Select(profile => profile.Id).ToArray());
        Assert.AreEqual(savedJson, File.ReadAllText(_profilesPath));
        Assert.IsFalse(Directory.EnumerateFiles(_tempDir, "*.tmp").Any());
    }

    [TestMethod]
    public void LoadProfiles_MigrationSaveFails_DoesNotPublishUuidsOrReplaceSource()
    {
        File.WriteAllText(_profilesPath, LegacyJson);
        File.SetAttributes(_profilesPath, FileAttributes.ReadOnly);
        var store = CreateStore();
        PowerDisplayProfiles? published = null;
        Exception? exception = null;

        try
        {
            published = store.LoadProfiles();
        }
        catch (Exception ex)
        {
            exception = ex;
        }

        Assert.IsNotNull(exception);
        Assert.IsNull(published);
        Assert.AreEqual(LegacyJson, File.ReadAllText(_profilesPath));
        Assert.IsFalse(Directory.EnumerateFiles(_tempDir, "*.tmp").Any());

        File.SetAttributes(_profilesPath, FileAttributes.Normal);
        var retried = store.LoadProfiles();
        Assert.AreNotEqual(Guid.Empty, retried.GetByLegacyId(7)!.Id);
        Assert.AreEqual(retried.GetByLegacyId(7)!.Id, CreateStore().LoadProfiles().GetByLegacyId(7)!.Id);
    }

    [TestMethod]
    public void LoadProfiles_AlreadyMigratedReadOnlyFile_DoesNotRewrite()
    {
        var store = CreateStore();
        SaveInitialProfiles(store);
        var originalJson = File.ReadAllText(_profilesPath);
        File.SetAttributes(_profilesPath, FileAttributes.ReadOnly);

        var loaded = store.LoadProfiles();

        Assert.AreEqual(3, loaded.Profiles.Count);
        Assert.AreEqual(originalJson, File.ReadAllText(_profilesPath));
    }

    [TestMethod]
    public async Task LoadProfilesAsync_TwoStoresMigrateLegacyFile_ExposeSamePersistedUuids()
    {
        File.WriteAllText(_profilesPath, LegacyJson);

        var loads = await Task.WhenAll(CreateStore().LoadProfilesAsync(), CreateStore().LoadProfilesAsync());

        CollectionAssert.AreEqual(
            loads[0].Profiles.Select(profile => profile.Id).ToArray(),
            loads[1].Profiles.Select(profile => profile.Id).ToArray());
        Assert.AreEqual(loads[0].GetByLegacyId(7)!.Id, CreateStore().LoadProfiles().GetByLegacyId(7)!.Id);
    }

    [TestMethod]
    public void LoadProfiles_MigrationMappingSurvivesEditReorderAndRestart()
    {
        File.WriteAllText(_profilesPath, LegacyJson);
        var store = CreateStore();
        var migratedId = store.LoadProfiles().GetByLegacyId(7)!.Id;
        Assert.IsTrue(store.UpdateProfiles(profiles => profiles.MoveProfileBefore(migratedId, null)));

        var edited = MakeProfile("Edited", migratedId);
        edited.LegacyId = 99;
        edited.Order = 99;
        store.AddOrUpdateProfile(edited);

        var reloaded = CreateStore().LoadProfiles();
        Assert.AreEqual(migratedId, reloaded.GetByLegacyId(7)!.Id);
        Assert.AreEqual("Edited", reloaded.GetByLegacyId(7)!.Name);
        Assert.AreEqual(1, reloaded.GetByLegacyId(7)!.Order);
        Assert.IsNull(reloaded.GetByLegacyId(99));
    }

    [TestMethod]
    public void LoadProfiles_RepairsDuplicateUuidsOrdersAndLegacyMappingsOnce()
    {
        var store = CreateStore();
        var original = SaveInitialProfiles(store);
        original.Profiles[1].Id = original.Profiles[0].Id;
        original.Profiles[1].LegacyId = original.Profiles[0].LegacyId;
        original.Profiles[1].Order = -1;
        original.Profiles[2].Order = 0;
        store.SaveProfiles(original);

        var repaired = store.LoadProfiles();

        Assert.AreEqual(ProfileIds[0], repaired.Profiles[0].Id);
        Assert.AreEqual(3, repaired.Profiles.Select(profile => profile.Id).Distinct().Count());
        Assert.IsNull(repaired.Profiles[1].LegacyId);
        Assert.AreSame(repaired.Profiles[0], repaired.GetByLegacyId(1));
        Assert.AreEqual("0,1,2", string.Join(",", repaired.GetAssignedProfiles().Select(profile => profile.Order)));
        var repairedJson = File.ReadAllText(_profilesPath);
        CollectionAssert.AreEqual(
            repaired.Profiles.Select(profile => profile.Id).ToArray(),
            CreateStore().LoadProfiles().Profiles.Select(profile => profile.Id).ToArray());
        Assert.AreEqual(repairedJson, File.ReadAllText(_profilesPath));
    }

    [TestMethod]
    public void UpdateProfiles_CorruptJson_DoesNotOverwriteSource()
    {
        const string corruptJson = "{not-json";
        File.WriteAllText(_profilesPath, corruptJson);
        var store = CreateStore();

        Assert.ThrowsExactly<JsonException>(() => store.UpdateProfiles(profiles => profiles.EnsureIdsAndOrder()));

        Assert.AreEqual(corruptJson, File.ReadAllText(_profilesPath));
    }

    [TestMethod]
    public void AddOrUpdateProfile_WritesAssignedUuidAtomically()
    {
        var store = CreateStore();
        var incoming = MakeProfile("Gaming");

        store.AddOrUpdateProfile(incoming);

        var loaded = store.LoadProfiles();
        Assert.AreEqual(1, loaded.Profiles.Count);
        Assert.AreNotEqual(Guid.Empty, loaded.Profiles[0].Id);
        Assert.AreEqual(incoming.Id, loaded.Profiles[0].Id);
        Assert.AreEqual(0, loaded.Profiles[0].Order);
        Assert.IsFalse(Directory.EnumerateFiles(_tempDir, "*.tmp").Any());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AddOrUpdateProfile_SaveFails_RestoresIncomingIdentityOrderAndMetadata(bool updateExisting)
    {
        var store = CreateStore();
        SaveInitialProfiles(store);
        var originalJson = File.ReadAllText(_profilesPath);
        File.SetAttributes(_profilesPath, FileAttributes.ReadOnly);
        var incomingId = updateExisting ? ProfileIds[2] : Guid.Empty;
        var incoming = MakeProfile("Changed", incomingId, order: 99);
        incoming.LegacyId = 99;
        Exception? exception = null;

        try
        {
            store.AddOrUpdateProfile(incoming);
        }
        catch (Exception ex)
        {
            exception = ex;
        }

        Assert.IsNotNull(exception);
        Assert.AreEqual(incomingId, incoming.Id);
        Assert.AreEqual(99, incoming.Order);
        Assert.AreEqual(99, incoming.LegacyId);
        Assert.AreEqual(DateTime.UnixEpoch, incoming.LastModified);
        Assert.AreEqual(originalJson, File.ReadAllText(_profilesPath));
        Assert.IsFalse(Directory.EnumerateFiles(_tempDir, "*.tmp").Any());
    }

    [TestMethod]
    public void SaveProfiles_SaveFails_RestoresLastUpdated()
    {
        File.WriteAllText(_profilesPath, "{}");
        File.SetAttributes(_profilesPath, FileAttributes.ReadOnly);
        var profiles = new PowerDisplayProfiles { LastUpdated = DateTime.UnixEpoch };
        var store = CreateStore();
        Exception? exception = null;

        try
        {
            store.SaveProfiles(profiles);
        }
        catch (Exception ex)
        {
            exception = ex;
        }

        Assert.IsNotNull(exception);
        Assert.AreEqual(DateTime.UnixEpoch, profiles.LastUpdated);
    }

    [TestMethod]
    public async Task AddOrUpdateProfileAsync_TwoStoresSharingMutex_PreserveBothUpdates()
    {
        var firstStore = CreateStore();
        var secondStore = CreateStore();
        using var start = new ManualResetEventSlim();
        var first = Task.Run(async () =>
        {
            start.Wait();
            await firstStore.AddOrUpdateProfileAsync(MakeProfile("First"));
        });
        var second = Task.Run(async () =>
        {
            start.Wait();
            await secondStore.AddOrUpdateProfileAsync(MakeProfile("Second"));
        });

        start.Set();
        await Task.WhenAll(first, second);

        var loaded = await firstStore.LoadProfilesAsync();
        Assert.AreEqual(2, loaded.Profiles.Count);
        Assert.AreEqual(2, loaded.Profiles.Select(profile => profile.Id).Distinct().Count());
        Assert.AreEqual("0,1", string.Join(",", loaded.GetAssignedProfiles().Select(profile => profile.Order)));
        CollectionAssert.AreEquivalent(ExpectedConcurrentProfileNames, loaded.Profiles.Select(profile => profile.Name).ToArray());
    }

    [TestMethod]
    public async Task UpdateProfilesAsync_WaitsWithoutBlockingCaller()
    {
        var firstStore = CreateStore();
        var secondStore = CreateStore();
        using var updateLoaded = new ManualResetEventSlim();
        using var continueUpdate = new ManualResetEventSlim();
        var holder = Task.Run(() => firstStore.UpdateProfiles(profiles =>
        {
            updateLoaded.Set();
            if (!continueUpdate.Wait(TimeSpan.FromSeconds(5)))
            {
                throw new TimeoutException("The profile update was not released.");
            }

            return false;
        }));

        try
        {
            Assert.IsTrue(updateLoaded.Wait(TimeSpan.FromSeconds(5)));
            var waitingUpdate = secondStore.UpdateProfilesAsync(_ => false);
            Assert.IsFalse(waitingUpdate.IsCompleted);
            continueUpdate.Set();
            await holder;
            Assert.IsFalse(await waitingUpdate);
        }
        finally
        {
            continueUpdate.Set();
            await holder;
        }
    }

    [TestMethod]
    public void UpdateProfiles_HoldsMutexAcrossLoadModifySave()
    {
        var firstStore = CreateStore();
        var secondStore = CreateStore();
        using var updateLoaded = new ManualResetEventSlim();
        using var continueUpdate = new ManualResetEventSlim();
        var first = Task.Run(() => firstStore.UpdateProfiles(profiles =>
        {
            updateLoaded.Set();
            if (!continueUpdate.Wait(TimeSpan.FromSeconds(5)))
            {
                throw new TimeoutException("The profile update was not released.");
            }

            profiles.SetProfile(MakeProfile("First"));
            return true;
        }));

        try
        {
            Assert.IsTrue(updateLoaded.Wait(TimeSpan.FromSeconds(5)));
            var second = Task.Run(() => secondStore.AddOrUpdateProfile(MakeProfile("Second")));
            continueUpdate.Set();
            Task.WaitAll(first, second);
        }
        finally
        {
            continueUpdate.Set();
            first.GetAwaiter().GetResult();
        }

        var loaded = firstStore.LoadProfiles();
        CollectionAssert.AreEquivalent(ExpectedConcurrentProfileNames, loaded.Profiles.Select(profile => profile.Name).ToArray());
    }

    [TestMethod]
    public async Task UpdateProfilesAsync_MoveProfileBefore_PersistsOrderIndependentlyOfPhysicalArray()
    {
        var store = CreateStore();
        var original = SaveInitialProfiles(store);
        original.Profiles.Reverse();
        store.SaveProfiles(original);
        var originalArrayIds = original.Profiles.Select(profile => profile.Id).ToArray();
        var originalContents = original.Profiles.ToDictionary(profile => profile.Id, SerializeContents);

        Assert.IsTrue(await store.UpdateProfilesAsync(profiles => profiles.MoveProfileBefore(ProfileIds[2], ProfileIds[0])));

        var loaded = await CreateStore().LoadProfilesAsync();
        Assert.AreEqual("3,1,2", DisplayOrder(loaded));
        CollectionAssert.AreEqual(originalArrayIds, loaded.Profiles.Select(profile => profile.Id).ToArray());
        foreach (var profile in loaded.Profiles)
        {
            Assert.AreEqual(originalContents[profile.Id], SerializeContents(profile));
        }

        Assert.IsFalse(Directory.EnumerateFiles(_tempDir, "*.tmp").Any());
    }

    [TestMethod]
    public void AddOrUpdateProfile_AfterReordering_PreservesDisplayOrderAndArrayPosition()
    {
        var store = CreateStore();
        SaveInitialProfiles(store);
        Assert.IsTrue(store.UpdateProfiles(profiles => profiles.MoveProfileBefore(ProfileIds[2], ProfileIds[0])));
        var edited = MakeProfile("Edited", ProfileIds[2]);
        edited.MonitorSettings[0].Brightness = 60;

        CreateStore().AddOrUpdateProfile(edited);

        var loaded = store.LoadProfiles();
        Assert.AreEqual("3,1,2", DisplayOrder(loaded));
        Assert.AreEqual(ProfileIds[2], loaded.Profiles[2].Id);
        Assert.AreEqual("Edited", loaded.Profiles[2].Name);
        Assert.AreEqual(60, loaded.Profiles[2].MonitorSettings[0].Brightness);
        Assert.AreEqual(0, loaded.Profiles[2].Order);
    }

    [TestMethod]
    public void UpdateProfiles_MoveProfileBefore_Unchanged_DoesNotRewriteFile()
    {
        var store = CreateStore();
        SaveInitialProfiles(store);
        var originalJson = File.ReadAllText(_profilesPath);
        File.SetAttributes(_profilesPath, FileAttributes.ReadOnly);

        Assert.IsFalse(store.UpdateProfiles(profiles => profiles.MoveProfileBefore(ProfileIds[0], ProfileIds[1])));

        Assert.AreEqual(originalJson, File.ReadAllText(_profilesPath));
        Assert.IsFalse(Directory.EnumerateFiles(_tempDir, "*.tmp").Any());
    }

    [TestMethod]
    public void UpdateProfiles_MoveProfileBefore_SaveFails_PreservesPersistedOrderAndContents()
    {
        var store = CreateStore();
        SaveInitialProfiles(store);
        var originalJson = File.ReadAllText(_profilesPath);
        File.SetAttributes(_profilesPath, FileAttributes.ReadOnly);
        Exception? exception = null;

        try
        {
            store.UpdateProfiles(profiles => profiles.MoveProfileBefore(ProfileIds[2], ProfileIds[0]));
        }
        catch (Exception ex)
        {
            exception = ex;
        }

        Assert.IsNotNull(exception);
        Assert.AreEqual(originalJson, File.ReadAllText(_profilesPath));
        Assert.AreEqual("1,2,3", DisplayOrder(store.LoadProfiles()));
        Assert.IsFalse(Directory.EnumerateFiles(_tempDir, "*.tmp").Any());
    }

    [TestMethod]
    public async Task UpdateProfilesAsync_MoveProfileBefore_PreservesConcurrentAddAndEdit()
    {
        var firstStore = CreateStore();
        var secondStore = CreateStore();
        SaveInitialProfiles(firstStore);
        using var writerLoaded = new ManualResetEventSlim();
        using var continueWriter = new ManualResetEventSlim();
        var writer = Task.Run(() => secondStore.UpdateProfiles(profiles =>
        {
            writerLoaded.Set();
            if (!continueWriter.Wait(TimeSpan.FromSeconds(5)))
            {
                throw new TimeoutException("The concurrent profile writer was not released.");
            }

            var edited = MakeProfile("Edited", ProfileIds[2]);
            edited.MonitorSettings[0].Brightness = 60;
            profiles.SetProfile(edited);
            profiles.SetProfile(MakeProfile("Added"));
            return true;
        }));

        try
        {
            Assert.IsTrue(writerLoaded.Wait(TimeSpan.FromSeconds(5)));
            var move = firstStore.UpdateProfilesAsync(profiles => profiles.MoveProfileBefore(ProfileIds[2], ProfileIds[0]));
            Assert.IsFalse(move.IsCompleted);
            continueWriter.Set();
            Assert.IsTrue(await writer);
            Assert.IsTrue(await move);
        }
        finally
        {
            continueWriter.Set();
            await writer;
        }

        var loaded = await firstStore.LoadProfilesAsync();
        Assert.AreEqual("Edited,Profile 1,Profile 2,Added", string.Join(",", loaded.GetAssignedProfiles().Select(profile => profile.Name)));
        Assert.AreEqual(60, loaded.GetById(ProfileIds[2])!.MonitorSettings[0].Brightness);
        Assert.AreEqual(3, loaded.GetById(ProfileIds[2])!.LegacyId);
        Assert.AreEqual(4, loaded.Profiles.Select(profile => profile.Id).Distinct().Count());
        Assert.AreEqual("0,1,2,3", string.Join(",", loaded.GetAssignedProfiles().Select(profile => profile.Order)));
    }

    [TestMethod]
    [DataRow(1, false, "2,3")]
    [DataRow(3, false, "1,2")]
    [DataRow(2, true, "3,1")]
    public async Task UpdateProfilesAsync_MoveProfileBefore_UsesLatestIdsAfterDeletion(int deletedIndex, bool expectedChanged, string expectedOrder)
    {
        var firstStore = CreateStore();
        var secondStore = CreateStore();
        SaveInitialProfiles(firstStore);
        var deletedId = ProfileIds[deletedIndex - 1];

        Assert.IsTrue(secondStore.RemoveProfileById(deletedId));
        var jsonAfterDeletion = File.ReadAllText(_profilesPath);
        Assert.AreEqual(
            expectedChanged,
            await firstStore.UpdateProfilesAsync(profiles => profiles.MoveProfileBefore(ProfileIds[2], ProfileIds[0])));

        var loaded = firstStore.LoadProfiles();
        Assert.AreEqual(expectedOrder, DisplayOrder(loaded));
        Assert.IsNull(loaded.GetById(deletedId));
        Assert.AreEqual("0,1", string.Join(",", loaded.GetAssignedProfiles().Select(profile => profile.Order)));
        if (!expectedChanged)
        {
            Assert.AreEqual(jsonAfterDeletion, File.ReadAllText(_profilesPath));
        }
    }

    private static PowerDisplayProfiles SaveInitialProfiles(ProfileStore store)
    {
        var profiles = new PowerDisplayProfiles();
        for (var index = 0; index < ProfileIds.Length; index++)
        {
            var profile = MakeProfile($"Profile {index + 1}", ProfileIds[index], index);
            profile.LegacyId = index + 1;
            profiles.Profiles.Add(profile);
        }

        store.SaveProfiles(profiles);
        return profiles;
    }

    private ProfileStore CreateStore()
        => new ProfileStore(_profilesPath, _mutexName, TimeSpan.FromSeconds(5));

    private static PowerDisplayProfile MakeProfile(string name, Guid id = default, int order = -1)
    {
        return new PowerDisplayProfile(name, new List<ProfileMonitorSetting>
        {
            new ProfileMonitorSetting("MON1", 50, null, null, null),
        })
        {
            Id = id,
            Order = order,
            CreatedDate = DateTime.UnixEpoch,
            LastModified = DateTime.UnixEpoch,
        };
    }

    private static string DisplayOrder(PowerDisplayProfiles profiles)
        => string.Join(",", profiles.GetAssignedProfiles().Select(profile => Array.IndexOf(ProfileIds, profile.Id) + 1));

    private static string SerializeContents(PowerDisplayProfile profile)
    {
        var node = JsonNode.Parse(JsonSerializer.Serialize(profile, ProfileSerializationContext.Default.PowerDisplayProfile))!.AsObject();
        node.Remove("order");
        return node.ToJsonString();
    }
}

// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
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
    public void UpdateProfiles_CorruptJson_DoesNotOverwriteSource()
    {
        const string corruptJson = "{not-json";
        File.WriteAllText(_profilesPath, corruptJson);
        var store = CreateStore();

        Exception? exception = null;
        try
        {
            store.UpdateProfiles(profiles => profiles.EnsureIds());
        }
        catch (Exception ex)
        {
            exception = ex;
        }

        Assert.IsInstanceOfType<JsonException>(exception);
        Assert.AreEqual(corruptJson, File.ReadAllText(_profilesPath));
    }

    [TestMethod]
    public void UpdateProfiles_SaveFails_DoesNotPublishTransientIdsOrReplaceSource()
    {
        var profiles = new PowerDisplayProfiles();
        profiles.Profiles.Add(MakeProfile("Legacy"));
        var originalJson = JsonSerializer.Serialize(profiles, ProfileSerializationContext.Default.PowerDisplayProfiles);
        File.WriteAllText(_profilesPath, originalJson);
        File.SetAttributes(_profilesPath, FileAttributes.ReadOnly);
        var store = CreateStore();

        Exception? exception = null;
        try
        {
            store.UpdateProfiles(loaded => loaded.EnsureIds());
        }
        catch (Exception ex)
        {
            exception = ex;
        }

        Assert.IsNotNull(exception);
        Assert.AreEqual(originalJson, File.ReadAllText(_profilesPath));
        var persisted = JsonSerializer.Deserialize(File.ReadAllText(_profilesPath), ProfileSerializationContext.Default.PowerDisplayProfiles);
        Assert.AreEqual(0, persisted!.Profiles[0].Id);
        Assert.IsFalse(Directory.EnumerateFiles(_tempDir, "*.tmp").Any());
    }

    [TestMethod]
    public void AddOrUpdateProfile_WritesAtomicallyWithoutLeavingTemporaryFile()
    {
        var store = CreateStore();

        store.AddOrUpdateProfile(MakeProfile("Gaming"));

        var loaded = store.LoadProfiles();
        Assert.AreEqual(1, loaded.Profiles.Count);
        Assert.AreEqual(1, loaded.Profiles[0].Id);
        Assert.IsFalse(Directory.EnumerateFiles(_tempDir, "*.tmp").Any());
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    public void AddOrUpdateProfile_SaveFails_RestoresIncomingProfileState(int incomingId)
    {
        var profiles = new PowerDisplayProfiles { NextId = 2 };
        profiles.Profiles.Add(MakeProfile("Existing", id: 1));
        var originalJson = JsonSerializer.Serialize(profiles, ProfileSerializationContext.Default.PowerDisplayProfiles);
        File.WriteAllText(_profilesPath, originalJson);
        File.SetAttributes(_profilesPath, FileAttributes.ReadOnly);
        var incoming = MakeProfile("Changed", id: incomingId);
        incoming.LastModified = DateTime.UnixEpoch;
        var store = CreateStore();

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
    public void AddOrUpdateProfile_TwoStoresSharingMutex_PreserveBothUpdates()
    {
        var firstStore = CreateStore();
        var secondStore = CreateStore();
        using var start = new ManualResetEventSlim();

        var first = Task.Run(() =>
        {
            start.Wait();
            firstStore.AddOrUpdateProfile(MakeProfile("First"));
        });
        var second = Task.Run(() =>
        {
            start.Wait();
            secondStore.AddOrUpdateProfile(MakeProfile("Second"));
        });

        start.Set();
        Task.WaitAll(first, second);

        var loaded = firstStore.LoadProfiles();
        Assert.AreEqual(2, loaded.Profiles.Count);
        Assert.AreEqual(2, loaded.Profiles.Select(profile => profile.Id).Distinct().Count());
        CollectionAssert.AreEquivalent(
            ExpectedConcurrentProfileNames,
            loaded.Profiles.Select(profile => profile.Name).ToArray());
    }

    [TestMethod]
    public async Task UpdateProfilesAsync_WaitsWithoutBlockingCaller()
    {
        var firstStore = CreateStore();
        var secondStore = CreateStore();
        using var updateLoaded = new ManualResetEventSlim();
        using var continueUpdate = new ManualResetEventSlim();

        var holder = Task.Run(() =>
            firstStore.UpdateProfiles(profiles =>
            {
                updateLoaded.Set();
                continueUpdate.Wait();
                return false;
            }));

        Assert.IsTrue(updateLoaded.Wait(TimeSpan.FromSeconds(5)));

        var waitingUpdate = secondStore.UpdateProfilesAsync(_ => false);
        Assert.IsFalse(waitingUpdate.IsCompleted);

        continueUpdate.Set();
        await holder;
        Assert.IsFalse(await waitingUpdate);
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
    }

    [TestMethod]
    public void UpdateProfiles_HoldsMutexAcrossLoadModifySave()
    {
        var firstStore = CreateStore();
        var secondStore = CreateStore();
        using var updateLoaded = new ManualResetEventSlim();
        using var continueUpdate = new ManualResetEventSlim();

        var first = Task.Run(() =>
            firstStore.UpdateProfiles(profiles =>
            {
                updateLoaded.Set();
                continueUpdate.Wait();
                profiles.SetProfile(MakeProfile("First"));
                return true;
            }));

        Assert.IsTrue(updateLoaded.Wait(TimeSpan.FromSeconds(5)));
        var second = Task.Run(() => secondStore.AddOrUpdateProfile(MakeProfile("Second")));
        continueUpdate.Set();
        Task.WaitAll(first, second);

        var loaded = firstStore.LoadProfiles();
        Assert.AreEqual(2, loaded.Profiles.Count);
        CollectionAssert.AreEquivalent(
            ExpectedConcurrentProfileNames,
            loaded.Profiles.Select(profile => profile.Name).ToArray());
    }

    [TestMethod]
    public void LoadProfiles_AssignedIdsReadOnlyFile_DoesNotRewrite()
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
    public async Task UpdateProfilesAsync_MoveProfileBefore_PersistsArrayOrderWithoutChangingProfiles()
    {
        var store = CreateStore();
        var original = SaveInitialProfiles(store);
        var originalContents = original.Profiles.ToDictionary(profile => profile.Id, SerializeContents);
        var originalNextId = original.NextId;

        Assert.IsTrue(await store.UpdateProfilesAsync(profiles => profiles.MoveProfileBefore(3, 1)));

        var loaded = await CreateStore().LoadProfilesAsync();
        Assert.AreEqual("3,1,2", DisplayOrder(loaded));
        Assert.AreEqual("3,1,2", string.Join(",", loaded.Profiles.Select(profile => profile.Id)));
        Assert.AreEqual(originalContents.Count, loaded.Profiles.Count);
        Assert.AreEqual(originalNextId, loaded.NextId);
        foreach (var profile in loaded.Profiles)
        {
            Assert.AreEqual(originalContents[profile.Id], SerializeContents(profile));
        }

        Assert.IsFalse(Directory.EnumerateFiles(_tempDir, "*.tmp").Any());
    }

    [TestMethod]
    public void AddOrUpdateProfile_AfterReordering_PreservesArrayPosition()
    {
        var store = CreateStore();
        SaveInitialProfiles(store);
        Assert.IsTrue(store.UpdateProfiles(profiles => profiles.MoveProfileBefore(3, 1)));
        var edited = MakeProfile("Edited", 3);
        edited.MonitorSettings[0].Brightness = 60;

        CreateStore().AddOrUpdateProfile(edited);

        var loaded = store.LoadProfiles();
        Assert.AreEqual("3,1,2", DisplayOrder(loaded));
        Assert.AreEqual(3, loaded.Profiles[0].Id);
        Assert.AreEqual("Edited", loaded.Profiles[0].Name);
        Assert.AreEqual(60, loaded.Profiles[0].MonitorSettings[0].Brightness);
    }

    [TestMethod]
    [DataRow(1, 2)]
    [DataRow(3, null)]
    [DataRow(1, 99)]
    [DataRow(99, 1)]
    [DataRow(0, 1)]
    [DataRow(1, 0)]
    [DataRow(1, 1)]
    public void UpdateProfiles_MoveProfileBefore_InvalidOrUnchanged_DoesNotRewriteFile(int profileId, int? beforeProfileId)
    {
        var store = CreateStore();
        SaveInitialProfiles(store);
        var originalJson = File.ReadAllText(_profilesPath);
        File.SetAttributes(_profilesPath, FileAttributes.ReadOnly);

        Assert.IsFalse(store.UpdateProfiles(profiles => profiles.MoveProfileBefore(profileId, beforeProfileId)));

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
            store.UpdateProfiles(profiles => profiles.MoveProfileBefore(3, 1));
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

            var edited = MakeProfile("Edited", 3);
            edited.MonitorSettings[0].Brightness = 60;
            profiles.SetProfile(edited);
            profiles.SetProfile(MakeProfile("Added"));
            return true;
        }));

        try
        {
            Assert.IsTrue(writerLoaded.Wait(TimeSpan.FromSeconds(5)));
            var move = firstStore.UpdateProfilesAsync(profiles => profiles.MoveProfileBefore(3, 1));
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
        Assert.AreEqual(60, loaded.GetById(3)!.MonitorSettings[0].Brightness);
        Assert.AreEqual(4, loaded.Profiles.Select(profile => profile.Id).Distinct().Count());
        Assert.AreEqual("3,1,2,4", DisplayOrder(loaded));
    }

    [TestMethod]
    [DataRow(1, false, "2,3")]
    [DataRow(3, false, "1,2")]
    [DataRow(2, true, "3,1")]
    public async Task UpdateProfilesAsync_MoveProfileBefore_UsesLatestIdsAfterDeletion(int deletedId, bool expectedChanged, string expectedOrder)
    {
        var firstStore = CreateStore();
        var secondStore = CreateStore();
        SaveInitialProfiles(firstStore);

        Assert.IsTrue(secondStore.RemoveProfileById(deletedId));
        var jsonAfterDeletion = File.ReadAllText(_profilesPath);
        Assert.AreEqual(
            expectedChanged,
            await firstStore.UpdateProfilesAsync(profiles => profiles.MoveProfileBefore(3, 1)));

        var loaded = firstStore.LoadProfiles();
        Assert.AreEqual(expectedOrder, DisplayOrder(loaded));
        Assert.IsNull(loaded.GetById(deletedId));
        Assert.AreEqual(2, loaded.Profiles.Count);
        if (!expectedChanged)
        {
            Assert.AreEqual(jsonAfterDeletion, File.ReadAllText(_profilesPath));
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void LoadProfiles_LegacyFile_PreservesIdsAndArrayOrderWithoutRewriting(bool readOnly)
    {
        File.WriteAllText(_profilesPath, LegacyJson);
        if (readOnly)
        {
            File.SetAttributes(_profilesPath, FileAttributes.ReadOnly);
        }

        var store = CreateStore();

        var loaded = store.LoadProfiles();

        Assert.AreEqual(2, loaded.Profiles.Count);
        Assert.AreEqual(7, loaded.Profiles[0].Id);
        Assert.AreEqual(0, loaded.Profiles[1].Id);
        Assert.AreEqual(8, loaded.NextId);
        Assert.AreEqual("Legacy,Older", string.Join(",", loaded.Profiles.Select(profile => profile.Name)));
        Assert.IsTrue(loaded.Profiles.All(profile => profile.LastModified == DateTime.UnixEpoch));
        Assert.AreEqual(LegacyJson, File.ReadAllText(_profilesPath));
        var reloaded = CreateStore().LoadProfiles();
        Assert.AreEqual(0, reloaded.Profiles[1].Id);
        Assert.AreEqual(8, reloaded.NextId);
        Assert.AreEqual(LegacyJson, File.ReadAllText(_profilesPath));
        Assert.IsFalse(Directory.EnumerateFiles(_tempDir, "*.tmp").Any());
    }

    private static PowerDisplayProfiles SaveInitialProfiles(ProfileStore store)
    {
        var profiles = new PowerDisplayProfiles { NextId = 4 };
        for (var id = 1; id <= 3; id++)
        {
            var profile = MakeProfile($"Profile {id}", id);
            profile.CreatedDate = DateTime.UnixEpoch;
            profile.LastModified = DateTime.UnixEpoch;
            profiles.Profiles.Add(profile);
        }

        store.SaveProfiles(profiles);
        return profiles;
    }

    private static string DisplayOrder(PowerDisplayProfiles profiles)
        => string.Join(",", profiles.GetAssignedProfiles().Select(profile => profile.Id));

    private static string SerializeContents(PowerDisplayProfile profile)
        => JsonSerializer.Serialize(profile, ProfileSerializationContext.Default.PowerDisplayProfile);

    private ProfileStore CreateStore()
    {
        return new ProfileStore(_profilesPath, _mutexName, TimeSpan.FromSeconds(5));
    }

    private static PowerDisplayProfile MakeProfile(string name, int id = 0)
    {
        return new PowerDisplayProfile(
            name,
            new List<ProfileMonitorSetting>
            {
                new ProfileMonitorSetting("MON1", 50, null, null, null),
            })
        {
            Id = id,
        };
    }
}

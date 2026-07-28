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
        Assert.AreEqual(0, store.LoadProfiles().Profiles[0].Id);
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
    public void AddOrUpdateProfile_SaveFails_RestoresIncomingProfileState()
    {
        var profiles = new PowerDisplayProfiles { NextId = 2 };
        profiles.Profiles.Add(MakeProfile("Existing", id: 1));
        var originalJson = JsonSerializer.Serialize(profiles, ProfileSerializationContext.Default.PowerDisplayProfiles);
        File.WriteAllText(_profilesPath, originalJson);
        File.SetAttributes(_profilesPath, FileAttributes.ReadOnly);
        var incoming = MakeProfile("New");
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
        Assert.AreEqual(0, incoming.Id);
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

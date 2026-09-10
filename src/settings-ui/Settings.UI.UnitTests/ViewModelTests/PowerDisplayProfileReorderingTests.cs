// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.UnitTests.BackwardsCompatibility;
using Microsoft.PowerToys.Settings.UI.UnitTests.Mocks;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PowerDisplay.Models;

namespace ViewModelTests;

[TestClass]
public class PowerDisplayProfileReorderingTests
{
    private const int FirstId = 1;
    private const int SecondId = 2;
    private const int ThirdId = 3;
    private const int MissingId = 4;

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Elevation_DisablesDraggingButKeepsMenuReordering(bool elevated)
    {
        var store = new ProfileSession();
        using var viewModel = CreateViewModel(store, elevated);
        await viewModel.InitializeProfilesAsync();

        Assert.AreEqual(!elevated, viewModel.CanDragProfiles);
        Assert.AreEqual(elevated, viewModel.IsProfileDragDisabledByElevation);
        Assert.IsTrue(viewModel.CanMoveProfileUp(viewModel.Profiles[1]));
        Assert.IsTrue(viewModel.CanMoveProfileDown(viewModel.Profiles[1]));

        await viewModel.MoveProfileUpAsync(viewModel.Profiles[1]);
        AssertOrder(viewModel, SecondId, FirstId, ThirdId);
        await viewModel.MoveProfileDownAsync(viewModel.Profiles[0]);
        AssertOrder(viewModel, FirstId, SecondId, ThirdId);
        Assert.AreEqual(2, store.Notifications);
    }

    [TestMethod]
    public async Task EmptyList_DoesNotShowElevationMessage()
    {
        var store = new ProfileSession();
        store.Data.Profiles.Clear();
        using var viewModel = CreateViewModel(store, elevated: true);
        await viewModel.InitializeProfilesAsync();

        Assert.IsFalse(viewModel.IsProfileDragDisabledByElevation);
        Assert.IsFalse(viewModel.HasProfiles);
    }

    [TestMethod]
    public async Task DraggedOrder_IsSavedReloadedAndNotifiedWithoutAnEmptyState()
    {
        var store = new ProfileSession();
        using var viewModel = CreateViewModel(store);
        await viewModel.InitializeProfilesAsync();
        var observedEmptyList = false;
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(viewModel.HasProfiles))
            {
                observedEmptyList |= !viewModel.HasProfiles;
            }
        };

        // ListView has already reordered the bound collection when DragItemsCompleted fires.
        viewModel.Profiles.Move(2, 0);
        await viewModel.ReorderProfileAsync(ThirdId, FirstId);

        AssertOrder(viewModel, ThirdId, FirstId, SecondId);

        // The persisted array is the display order; moving an entry preserves its stable id.
        CollectionAssert.AreEqual(new[] { ThirdId, FirstId, SecondId }, store.Data.Profiles.Select(profile => profile.Id).ToArray());
        Assert.AreEqual(1, store.Notifications);
        Assert.IsFalse(observedEmptyList);
        Assert.IsFalse(viewModel.IsProfileReorderError);
    }

    [TestMethod]
    public async Task SaveInProgress_DisablesFurtherOperationsUntilItCompletes()
    {
        var store = new ProfileSession { PendingWrite = new TaskCompletionSource<bool>() };
        using var viewModel = CreateViewModel(store);
        await viewModel.InitializeProfilesAsync();

        var save = viewModel.ReorderProfileAsync(ThirdId, FirstId);
        Assert.IsTrue(viewModel.IsProfilesLoading);
        Assert.IsFalse(viewModel.CanDragProfiles);
        Assert.IsFalse(viewModel.CanMoveProfileUp(viewModel.Profiles[1]));
        await viewModel.ReorderProfileAsync(SecondId, FirstId);
        Assert.AreEqual(1, store.Writes);

        store.PendingWrite.SetResult(true);
        await save;
        Assert.IsFalse(viewModel.IsProfilesLoading);
        Assert.IsTrue(viewModel.CanDragProfiles);
        AssertOrder(viewModel, ThirdId, FirstId, SecondId);
    }

    [TestMethod]
    public async Task DeletedSource_ReloadsTheCurrentListWithoutRecreatingIt()
    {
        var store = new ProfileSession();
        using var viewModel = CreateViewModel(store);
        await viewModel.InitializeProfilesAsync();
        viewModel.Profiles.Move(2, 0);
        store.Data.RemoveProfile(ThirdId);

        await viewModel.ReorderProfileAsync(ThirdId, FirstId);

        AssertOrder(viewModel, FirstId, SecondId);
        Assert.AreEqual(0, store.Notifications);
    }

    [TestMethod]
    public async Task FailedSave_RestoresPersistedOrderAndAllowsRetry()
    {
        var store = new ProfileSession();
        using var viewModel = CreateViewModel(store);
        await viewModel.InitializeProfilesAsync();
        viewModel.Profiles.Move(2, 0);
        store.FailWrite = true;

        await viewModel.ReorderProfileAsync(ThirdId, FirstId);

        AssertOrder(viewModel, FirstId, SecondId, ThirdId);
        Assert.IsTrue(viewModel.IsProfileReorderError);
        Assert.IsFalse(viewModel.IsProfilesLoading);
        Assert.AreEqual(0, store.Notifications);

        store.FailWrite = false;
        await viewModel.MoveProfileUpAsync(viewModel.Profiles[2]);
        AssertOrder(viewModel, FirstId, ThirdId, SecondId);
        Assert.IsFalse(viewModel.IsProfileReorderError);
    }

    [TestMethod]
    public async Task FailedSaveAndReload_RestoresLastLoadedOrder()
    {
        var store = new ProfileSession();
        using var viewModel = CreateViewModel(store);
        await viewModel.InitializeProfilesAsync();
        viewModel.Profiles.Move(2, 0);
        store.FailWrite = true;
        store.FailRead = true;

        await viewModel.ReorderProfileAsync(ThirdId, FirstId);

        AssertOrder(viewModel, FirstId, SecondId, ThirdId);
        Assert.IsTrue(viewModel.IsProfileReorderError);
        Assert.AreEqual(0, store.Notifications);
    }

    [TestMethod]
    public async Task SuccessfulSaveButFailedReload_KeepsCommittedOrderAndNotifiesModule()
    {
        var store = new ProfileSession();
        using var viewModel = CreateViewModel(store);
        await viewModel.InitializeProfilesAsync();
        store.FailRead = true;

        await viewModel.MoveProfileUpAsync(viewModel.Profiles[2]);

        AssertOrder(viewModel, FirstId, ThirdId, SecondId);
        Assert.IsTrue(viewModel.IsProfileReorderError);
        Assert.AreEqual(1, store.Notifications);
    }

    [TestMethod]
    public async Task MenuBoundariesAndMissingProfiles_DoNotWrite()
    {
        var store = new ProfileSession();
        using var viewModel = CreateViewModel(store);
        await viewModel.InitializeProfilesAsync();

        Assert.IsFalse(viewModel.CanMoveProfileUp(viewModel.Profiles[0]));
        Assert.IsFalse(viewModel.CanMoveProfileDown(viewModel.Profiles[2]));
        Assert.IsFalse(viewModel.CanMoveProfileUp(new PowerDisplayProfile { Id = MissingId }));
        Assert.IsFalse(viewModel.CanMoveProfileDown(null));
        await viewModel.MoveProfileUpAsync(viewModel.Profiles[0]);
        await viewModel.MoveProfileDownAsync(viewModel.Profiles[2]);

        Assert.AreEqual(0, store.Writes);
    }

    [TestMethod]
    public async Task DisabledModule_DoesNotAllowReordering()
    {
        var store = new ProfileSession();
        using var viewModel = CreateViewModel(store, enabled: false);
        await viewModel.InitializeProfilesAsync();

        Assert.IsFalse(viewModel.CanDragProfiles);
        Assert.IsFalse(viewModel.CanMoveProfileUp(viewModel.Profiles[1]));
        await viewModel.ReorderProfileAsync(ThirdId, FirstId);
        Assert.AreEqual(0, store.Writes);
    }

    [TestMethod]
    public async Task FailedLoad_ShowsAnErrorAndAllowsRetry()
    {
        var store = new ProfileSession { FailRead = true };
        using var viewModel = CreateViewModel(store);

        await viewModel.InitializeProfilesAsync();

        Assert.IsFalse(viewModel.HasProfiles);
        Assert.IsFalse(viewModel.IsProfilesLoading);
        Assert.IsTrue(viewModel.IsProfileReorderError);

        store.FailRead = false;
        await viewModel.InitializeProfilesAsync();
        AssertOrder(viewModel, FirstId, SecondId, ThirdId);
        Assert.IsFalse(viewModel.IsProfileReorderError);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task FailedReorder_SuccessfulUpsertClearsError(bool isNew)
    {
        var store = new ProfileSession();
        using var viewModel = CreateViewModel(store);
        await viewModel.InitializeProfilesAsync();
        store.FailWrite = true;
        await viewModel.ReorderProfileAsync(ThirdId, FirstId);
        Assert.IsTrue(viewModel.IsProfileReorderError);

        store.FailWrite = false;
        var profile = CreateProfile(isNew ? 0 : SecondId, "Saved");
        if (isNew)
        {
            await viewModel.CreateProfileAsync(profile);
            AssertOrder(viewModel, FirstId, SecondId, ThirdId, MissingId);
        }
        else
        {
            await viewModel.UpdateProfileAsync(profile);
            AssertOrder(viewModel, FirstId, SecondId, ThirdId);
        }

        Assert.AreEqual("Saved", viewModel.Profiles.Single(item => item.Id == profile.Id).Name);
        Assert.IsFalse(viewModel.IsProfileReorderError);
        Assert.IsFalse(viewModel.IsProfilesLoading);
        Assert.AreEqual(1, store.Notifications);
    }

    [TestMethod]
    public async Task FailedReorder_SuccessfulDeleteClearsError()
    {
        var store = new ProfileSession();
        using var viewModel = CreateViewModel(store);
        await viewModel.InitializeProfilesAsync();
        store.FailWrite = true;
        await viewModel.ReorderProfileAsync(ThirdId, FirstId);
        Assert.IsTrue(viewModel.IsProfileReorderError);

        store.FailWrite = false;
        await viewModel.DeleteProfileAsync(SecondId);

        AssertOrder(viewModel, FirstId, ThirdId);
        Assert.IsFalse(viewModel.IsProfileReorderError);
        Assert.IsFalse(viewModel.IsProfilesLoading);
        Assert.AreEqual(1, store.Notifications);
    }

    [TestMethod]
    public async Task FailedLoad_SuccessfulCreateClearsError()
    {
        var store = new ProfileSession { FailRead = true };
        using var viewModel = CreateViewModel(store);
        await viewModel.InitializeProfilesAsync();
        Assert.IsTrue(viewModel.IsProfileReorderError);

        store.FailRead = false;
        await viewModel.CreateProfileAsync(CreateProfile(0, "Added"));

        AssertOrder(viewModel, FirstId, SecondId, ThirdId, MissingId);
        Assert.IsFalse(viewModel.IsProfileReorderError);
        Assert.IsFalse(viewModel.IsProfilesLoading);
        Assert.AreEqual(1, store.Notifications);
    }

    [TestMethod]
    public async Task FailedLoad_SuccessfulWriteButFailedReloadKeepsError()
    {
        var store = new ProfileSession { FailRead = true };
        using var viewModel = CreateViewModel(store);
        await viewModel.InitializeProfilesAsync();
        Assert.IsTrue(viewModel.IsProfileReorderError);

        await viewModel.CreateProfileAsync(CreateProfile(0, "Added"));

        Assert.AreEqual("Added", store.Data.GetById(MissingId).Name);
        Assert.IsTrue(viewModel.IsProfileReorderError);
        Assert.IsFalse(viewModel.IsProfilesLoading);
        Assert.IsFalse(viewModel.HasProfiles);
    }

    [TestMethod]
    public async Task LoadProfiles_UsesArrayOrderInsteadOfIdOrder()
    {
        var store = new ProfileSession();
        store.Data.Profiles = new List<PowerDisplayProfile>
        {
            CreateProfile(SecondId, "Second"),
            CreateProfile(ThirdId, "Third"),
            CreateProfile(FirstId, "First"),
        };
        using var viewModel = CreateViewModel(store);

        await viewModel.InitializeProfilesAsync();

        AssertOrder(viewModel, SecondId, ThirdId, FirstId);
        Assert.AreEqual(0, store.Writes);
    }

    private static PowerDisplayProfile CreateProfile(int id, string name)
    {
        return new PowerDisplayProfile(name, new List<ProfileMonitorSetting> { new ProfileMonitorSetting("monitor-1", brightness: 50) })
        {
            Id = id,
        };
    }

    private static void AssertOrder(PowerDisplayViewModel viewModel, params int[] ids)
        => CollectionAssert.AreEqual(ids, viewModel.Profiles.Select(p => p.Id).ToArray());

    private static PowerDisplayViewModel CreateViewModel(ProfileSession store, bool elevated = false, bool enabled = true)
    {
        var settingsUtils = ISettingsUtilsMocks.GetStubSettingsUtils<PowerDisplaySettings>();
        settingsUtils
            .Setup(utils => utils.GetSettingsOrDefault<LightSwitchSettings>(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(new LightSwitchSettings());
        var generalSettingsUtils = ISettingsUtilsMocks.GetStubSettingsUtils<GeneralSettings>();
        var generalRepository = new BackCompatTestProperties.MockSettingsRepository<GeneralSettings>(generalSettingsUtils.Object);
        generalRepository.SettingsConfig.IsElevated = elevated;
        generalRepository.SettingsConfig.Enabled.PowerDisplay = enabled;

        return new PowerDisplayViewModel(
            settingsUtils.Object,
            generalRepository,
            new BackCompatTestProperties.MockSettingsRepository<PowerDisplaySettings>(settingsUtils.Object),
            _ => 0,
            (_, _) => { },
            store.LoadAsync,
            store.ReorderAsync,
            () => store.Notifications++,
            store.AddOrUpdateAsync,
            store.RemoveAsync);
    }

    private sealed class ProfileSession
    {
        public PowerDisplayProfiles Data { get; } = new PowerDisplayProfiles
        {
            Profiles = new List<PowerDisplayProfile>
            {
                CreateProfile(FirstId, "First"),
                CreateProfile(SecondId, "Second"),
                CreateProfile(ThirdId, "Third"),
            },
        };

        public bool FailWrite { get; set; }

        public bool FailRead { get; set; }

        public int Writes { get; private set; }

        public int Notifications { get; set; }

        public TaskCompletionSource<bool> PendingWrite { get; set; }

        public Task<PowerDisplayProfiles> LoadAsync(CancellationToken cancellationToken)
        {
            return FailRead
                ? Task.FromException<PowerDisplayProfiles>(new IOException("Cannot read profiles"))
                : Task.FromResult(JsonSerializer.Deserialize(
                    JsonSerializer.Serialize(Data, ProfileSerializationContext.Default.PowerDisplayProfiles),
                    ProfileSerializationContext.Default.PowerDisplayProfiles));
        }

        public Task AddOrUpdateAsync(PowerDisplayProfile profile)
        {
            Writes++;
            if (FailWrite)
            {
                return Task.FromException(new IOException("Cannot write profiles"));
            }

            Data.SetProfile(profile);
            return Task.CompletedTask;
        }

        public Task<bool> RemoveAsync(int id)
        {
            Writes++;
            return FailWrite
                ? Task.FromException<bool>(new IOException("Cannot write profiles"))
                : Task.FromResult(Data.RemoveProfile(id));
        }

        public async Task<bool> ReorderAsync(int id, int? beforeId)
        {
            Writes++;
            if (PendingWrite != null)
            {
                await PendingWrite.Task;
            }

            if (FailWrite)
            {
                throw new IOException("Cannot write profiles");
            }

            return Data.MoveProfileBefore(id, beforeId);
        }
    }
}

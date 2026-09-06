// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public class SettingsViewModelTests
{
    [TestMethod]
    public async Task SettingsChanges_RefreshShelfBindingsOnSuppliedSchedulerWithoutSavingAgain()
    {
        var settingsService = CreateSettingsService();
        var scheduler = new QueuedTaskScheduler();
        using var viewModel = CreateViewModel(settingsService.Object, scheduler);
        var boundCompactMode = viewModel.CompactMode;
        var boundShowShelf = viewModel.ShowQuickAccessShelf;
        var boundCanConfigureShelf = viewModel.CanConfigureQuickAccessShelf;
        TaskScheduler? notificationScheduler = null;

        viewModel.PropertyChanged += (_, args) =>
        {
            notificationScheduler = TaskScheduler.Current;
            if (args.PropertyName == nameof(SettingsViewModel.CompactMode))
            {
                boundCompactMode = viewModel.CompactMode;
            }
            else if (args.PropertyName == nameof(SettingsViewModel.ShowQuickAccessShelf))
            {
                boundShowShelf = viewModel.ShowQuickAccessShelf;
            }
            else if (args.PropertyName == nameof(SettingsViewModel.CanConfigureQuickAccessShelf))
            {
                boundCanConfigureShelf = viewModel.CanConfigureQuickAccessShelf;
            }
        };

        var expectedSaves = 0;
        foreach (var (compactMode, showShelf) in new[] { (true, true), (true, false), (false, true), (true, true) })
        {
            var previousCompactMode = boundCompactMode;
            var previousShowShelf = boundShowShelf;
            await Task.Run(() => settingsService.Object.UpdateSettings(
                settings => settings with { CompactMode = compactMode, ShowQuickAccessShelf = showShelf }));
            expectedSaves++;

            Assert.AreEqual(previousCompactMode, boundCompactMode);
            Assert.AreEqual(previousShowShelf, boundShowShelf);
            scheduler.ExecuteAllAvailable();

            Assert.AreEqual(compactMode, boundCompactMode);
            Assert.AreEqual(showShelf, boundShowShelf);
            Assert.AreEqual(compactMode && showShelf, boundCanConfigureShelf);
            Assert.AreSame(scheduler, notificationScheduler);

            viewModel.CompactMode = boundCompactMode;
            viewModel.ShowQuickAccessShelf = boundShowShelf;
            settingsService.Verify(
                service => service.UpdateSettings(It.IsAny<Func<SettingsModel, SettingsModel>>(), It.IsAny<bool>()),
                Times.Exactly(expectedSaves));
        }
    }

    [TestMethod]
    public void QueuedSettingsChanges_ReadLatestSettings()
    {
        var settingsService = CreateSettingsService();
        var scheduler = new QueuedTaskScheduler();
        using var viewModel = CreateViewModel(settingsService.Object, scheduler);
        var shelfValues = new List<bool>();
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(SettingsViewModel.ShowQuickAccessShelf))
            {
                shelfValues.Add(viewModel.ShowQuickAccessShelf);
            }
        };

        settingsService.Object.UpdateSettings(settings => settings with { ShowQuickAccessShelf = true });
        settingsService.Object.UpdateSettings(settings => settings with { ShowQuickAccessShelf = false });
        scheduler.ExecuteAllAvailable();

        Assert.AreEqual(2, shelfValues.Count);
        CollectionAssert.DoesNotContain(shelfValues, true);
    }

    [TestMethod]
    public void Dispose_UnsubscribesOnceAndIgnoresQueuedRefresh()
    {
        var settingsService = CreateSettingsService();
        var scheduler = new QueuedTaskScheduler();
        using var viewModel = CreateViewModel(settingsService.Object, scheduler);
        var notifications = 0;
        viewModel.PropertyChanged += (_, _) => notifications++;

        settingsService.Object.UpdateSettings(settings => settings with { ShowQuickAccessShelf = true });
        Assert.AreEqual(1, scheduler.PendingCount);

        viewModel.Dispose();
        viewModel.Dispose();
        scheduler.ExecuteAllAvailable();
        Assert.AreEqual(0, notifications);

        settingsService.Object.UpdateSettings(settings => settings with { ShowQuickAccessShelf = false });
        Assert.AreEqual(0, scheduler.PendingCount);
        settingsService.VerifyRemove(
            service => service.SettingsChanged -= It.IsAny<TypedEventHandler<ISettingsService, SettingsModel>>(),
            Times.Once);
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(3)]
    public void RecentPlacementIndexes_IgnoreInvalidSelections(int index)
    {
        var settingsService = CreateSettingsService();
        using var viewModel = CreateViewModel(settingsService.Object, new QueuedTaskScheduler());
        var originalSettings = settingsService.Object.Settings;

        viewModel.RecentCommandsOnQuickAccessShelfIndex = index;
        viewModel.RecentCommandsOnHomeIndex = index;

        Assert.AreSame(originalSettings, settingsService.Object.Settings);
        settingsService.Verify(
            service => service.UpdateSettings(It.IsAny<Func<SettingsModel, SettingsModel>>(), It.IsAny<bool>()),
            Times.Never);
    }

    [TestMethod]
    [DataRow(RecentCommandsPlacement.Hidden)]
    [DataRow(RecentCommandsPlacement.BeforePinned)]
    [DataRow(RecentCommandsPlacement.AfterPinned)]
    public void RecentPlacementIndexes_SaveValidSelections(RecentCommandsPlacement placement)
    {
        var settingsService = CreateSettingsService();
        using var viewModel = CreateViewModel(settingsService.Object, new QueuedTaskScheduler());

        viewModel.RecentCommandsOnQuickAccessShelfIndex = (int)placement;
        viewModel.RecentCommandsOnHomeIndex = (int)placement;

        Assert.AreEqual(placement, settingsService.Object.Settings.RecentCommandsOnQuickAccessShelf);
        Assert.AreEqual(placement, settingsService.Object.Settings.RecentCommandsOnHome);
    }

    [TestMethod]
    public void Dispose_ReleasesOwnedProvidersWithoutDisposingAnotherPagesProvider()
    {
        var settingsService = CreateSettingsService();
        using var viewModel = CreateViewModel(settingsService.Object, new QueuedTaskScheduler());
        var provider = (CommandProviderWrapper)RuntimeHelpers.GetUninitializedObject(typeof(CommandProviderWrapper));
        typeof(CommandProviderWrapper).GetProperty(nameof(CommandProviderWrapper.Id))!.SetValue(provider, "test-provider");
        typeof(CommandProviderWrapper).GetProperty(nameof(CommandProviderWrapper.FallbackItems))!.SetValue(provider, Array.Empty<TopLevelViewModel>());
        typeof(CommandProviderWrapper).GetProperty(nameof(CommandProviderWrapper.TopLevelItems))!.SetValue(provider, Array.Empty<TopLevelViewModel>());
        var ownedProvider = new ProviderSettingsViewModel(provider, new ProviderSettings(), settingsService.Object);
        viewModel.CommandProviders.Add(ownedProvider);
        using var otherPageProvider = new ProviderSettingsViewModel(provider, new ProviderSettings(), settingsService.Object);
        ownedProvider.IsEnabled = true;
        otherPageProvider.IsEnabled = true;
        var ownedNotifications = 0;
        var otherNotifications = 0;
        ownedProvider.PropertyChanged += (_, _) => ownedNotifications++;
        otherPageProvider.PropertyChanged += (_, _) => otherNotifications++;

        var eventField = typeof(CommandProviderWrapper).GetField(nameof(CommandProviderWrapper.CommandsChanged), BindingFlags.Instance | BindingFlags.NonPublic)!;
        var pendingNotification = (TypedEventHandler<CommandProviderWrapper, IItemsChangedEventArgs>)eventField.GetValue(provider)!;
        Assert.AreEqual(2, pendingNotification.GetInvocationList().Length);

        viewModel.Dispose();
        var remainingHandler = (Delegate)eventField.GetValue(provider)!;
        Assert.AreSame(otherPageProvider, remainingHandler.Target);
        pendingNotification(provider, new ItemsChangedEventArgs());
        Assert.AreEqual(0, ownedNotifications);
        Assert.AreEqual(2, otherNotifications);

        otherPageProvider.Dispose();
        otherPageProvider.IsEnabled = false;
        pendingNotification(provider, new ItemsChangedEventArgs());
        Assert.IsNull(eventField.GetValue(provider));
        Assert.IsTrue(otherPageProvider.IsEnabled);
        Assert.AreEqual(2, otherNotifications);
        settingsService.Verify(
            service => service.UpdateSettings(It.IsAny<Func<SettingsModel, SettingsModel>>(), It.IsAny<bool>()),
            Times.Never);
    }

    private static Mock<ISettingsService> CreateSettingsService()
    {
        var settings = new SettingsModel { CompactMode = true, ShowQuickAccessShelf = false };
        var service = new Mock<ISettingsService>();
        service.SetupGet(sender => sender.Settings).Returns(() => settings);
        service.Setup(sender => sender.UpdateSettings(It.IsAny<Func<SettingsModel, SettingsModel>>(), It.IsAny<bool>()))
            .Callback<Func<SettingsModel, SettingsModel>, bool>((transform, hotReload) =>
            {
                settings = transform(settings);
                if (hotReload)
                {
                    service.Raise(sender => sender.SettingsChanged += null, service.Object, settings);
                }
            });
        return service;
    }

    private static SettingsViewModel CreateViewModel(ISettingsService settingsService, TaskScheduler scheduler)
    {
        // The console test host cannot activate the constructor's WinUI theme and timer dependencies.
        // Wire the settings event to the real handler without constructing those unrelated child view models.
        var viewModel = (SettingsViewModel)RuntimeHelpers.GetUninitializedObject(typeof(SettingsViewModel));
        typeof(SettingsViewModel).GetField("_settingsService", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(viewModel, settingsService);
        typeof(SettingsViewModel).GetField("_uiScheduler", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(viewModel, scheduler);
        typeof(SettingsViewModel).GetField("<CommandProviders>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(viewModel, new ObservableCollection<ProviderSettingsViewModel>());

        var handler = typeof(SettingsViewModel).GetMethod("SettingsService_SettingsChanged", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(handler);
        settingsService.SettingsChanged += handler.CreateDelegate<TypedEventHandler<ISettingsService, SettingsModel>>(viewModel);
        return viewModel;
    }

    private sealed class QueuedTaskScheduler : TaskScheduler
    {
        private readonly ConcurrentQueue<Task> _tasks = [];

        public int PendingCount => _tasks.Count;

        protected override IEnumerable<Task> GetScheduledTasks() => _tasks.ToArray();

        protected override void QueueTask(Task task) => _tasks.Enqueue(task);

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued) => false;

        public void ExecuteAllAvailable()
        {
            while (_tasks.TryDequeue(out var task))
            {
                TryExecuteTask(task);
                task.GetAwaiter().GetResult();
            }
        }
    }
}

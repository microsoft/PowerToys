// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Controls;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace ViewModelTests
{
    [TestClass]
    public class QuickAccessLifetimeTests
    {
        [TestMethod]
        public void DisposeUnsubscribesBothRepositoriesAndEnabledCallback()
        {
            var general = new LifetimeTestRepository<GeneralSettings>(new GeneralSettings());
            var keyboard = new LifetimeTestRepository<KeyboardManagerSettings>(new KeyboardManagerSettings());
            var queue = new Queue<Action>();
            var viewModel = CreateViewModel(general, keyboard, queue);

            Assert.AreEqual(1, general.SubscriberCount);
            Assert.AreEqual(1, keyboard.SubscriberCount);
            viewModel.Dispose();
            viewModel.Dispose();

            Assert.AreEqual(0, general.SubscriberCount);
            Assert.AreEqual(0, keyboard.SubscriberCount);
            Assert.AreEqual(1, general.RemovedCount);
            Assert.AreEqual(1, keyboard.RemovedCount);
            general.Notify();
            keyboard.Notify();
            general.SettingsConfig.Enabled.ColorPicker = !general.SettingsConfig.Enabled.ColorPicker;
            Assert.AreEqual(0, queue.Count);
        }

        [TestMethod]
        public void QueuedAndInFlightCallbacksCannotMutateOrReattachAfterDispose()
        {
            var original = new GeneralSettings();
            var general = new LifetimeTestRepository<GeneralSettings>(original);
            var keyboard = new LifetimeTestRepository<KeyboardManagerSettings>(new KeyboardManagerSettings());
            var queue = new Queue<Action>();
            var viewModel = CreateViewModel(general, keyboard, queue);
            var item = FindItem(viewModel, ModuleType.ColorPicker);
            var visible = item.Visible;
            var generalInFlight = general.CaptureNotification();
            var keyboardInFlight = keyboard.CaptureNotification();

            original.Enabled.ColorPicker = !visible;
            general.SettingsConfig = new GeneralSettings();
            general.SettingsConfig.Enabled.ColorPicker = !visible;
            general.Notify();
            keyboard.Notify();
            Assert.AreEqual(3, queue.Count);
            viewModel.Dispose();
            Drain(queue);
            generalInFlight();
            keyboardInFlight();

            Assert.AreEqual(visible, item.Visible);
            Assert.AreEqual(0, queue.Count);
            original.Enabled.ColorPicker = visible;
            general.SettingsConfig.Enabled.ColorPicker = visible;
            Assert.AreEqual(0, queue.Count);
            Assert.AreEqual(0, general.SubscriberCount);
            Assert.AreEqual(0, keyboard.SubscriberCount);
        }

        [TestMethod]
        public void LiveModelUsesLatestSnapshotAndDetachesOldSnapshot()
        {
            var original = new GeneralSettings();
            var general = new LifetimeTestRepository<GeneralSettings>(original);
            var keyboard = new LifetimeTestRepository<KeyboardManagerSettings>(new KeyboardManagerSettings());
            var queue = new Queue<Action>();
            using var viewModel = CreateViewModel(general, keyboard, queue);
            var intermediate = new GeneralSettings();
            intermediate.Enabled.ColorPicker = false;
            general.SettingsConfig = intermediate;
            general.Notify();
            var latest = new GeneralSettings();
            latest.Enabled.ColorPicker = true;
            general.SettingsConfig = latest;
            general.Notify();

            queue.Dequeue()();
            Assert.IsTrue(FindItem(viewModel, ModuleType.ColorPicker).Visible);
            Drain(queue);
            original.Enabled.ColorPicker = !original.Enabled.ColorPicker;
            intermediate.Enabled.ColorPicker = true;
            Assert.AreEqual(0, queue.Count);

            latest.Enabled.ColorPicker = false;
            Assert.AreEqual(1, queue.Count);
            Drain(queue);
            Assert.IsFalse(FindItem(viewModel, ModuleType.ColorPicker).Visible);
        }

        [TestMethod]
        public void LiveKeyboardManagerChangesKeepItemIdentityAndLaunchCommand()
        {
            var general = new LifetimeTestRepository<GeneralSettings>(new GeneralSettings());
            general.SettingsConfig.Enabled.KeyboardManager = true;
            var keyboard = new LifetimeTestRepository<KeyboardManagerSettings>(new KeyboardManagerSettings());
            var queue = new Queue<Action>();
            var launcher = new Mock<IQuickAccessLauncher>();
            using var viewModel = CreateViewModel(general, keyboard, queue, launcher.Object);
            var items = viewModel.Items;
            var item = FindItem(viewModel, ModuleType.KeyboardManager);
            Assert.IsTrue(item.Visible);

            keyboard.SettingsConfig = new KeyboardManagerSettings();
            keyboard.SettingsConfig.Properties.UseNewEditor = false;
            keyboard.Notify();
            Drain(queue);
            Assert.IsFalse(item.Visible);
            keyboard.SettingsConfig.Properties.UseNewEditor = true;
            keyboard.Notify();
            Drain(queue);

            Assert.IsTrue(item.Visible);
            Assert.AreSame(items, viewModel.Items);
            Assert.AreSame(item, FindItem(viewModel, ModuleType.KeyboardManager));
            item.Command.Execute(null);
            launcher.Verify(instance => instance.Launch(ModuleType.KeyboardManager), Times.Once);
        }

        [TestMethod]
        public void NoOpRefreshDoesNotChangeItemsOrPublishCollectionChanges()
        {
            var general = new LifetimeTestRepository<GeneralSettings>(new GeneralSettings());
            var keyboard = new LifetimeTestRepository<KeyboardManagerSettings>(new KeyboardManagerSettings());
            var queue = new Queue<Action>();
            using var viewModel = CreateViewModel(general, keyboard, queue);
            var items = viewModel.Items.ToArray();
            var collectionChanges = 0;
            var propertyChanges = 0;
            viewModel.Items.CollectionChanged += (_, _) => collectionChanges++;
            foreach (var item in items)
            {
                item.PropertyChanged += (_, _) => propertyChanges++;
            }

            for (var i = 0; i < 20; i++)
            {
                general.Notify();
                keyboard.Notify();
            }

            Drain(queue);
            CollectionAssert.AreEqual(items, viewModel.Items.ToArray());
            Assert.AreEqual(0, collectionChanges);
            Assert.AreEqual(0, propertyChanges);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void DisposingOlderOwnerDoesNotRemoveNewOwner(bool replaceSnapshot)
        {
            var general = new LifetimeTestRepository<GeneralSettings>(new GeneralSettings());
            var keyboard = new LifetimeTestRepository<KeyboardManagerSettings>(new KeyboardManagerSettings());
            var oldQueue = new Queue<Action>();
            var newQueue = new Queue<Action>();
            var oldOwner = CreateViewModel(general, keyboard, oldQueue);
            if (replaceSnapshot)
            {
                general.SettingsConfig = new GeneralSettings();
                general.Notify();
            }

            using var newOwner = CreateViewModel(general, keyboard, newQueue);
            oldOwner.Dispose();
            Drain(oldQueue);
            general.SettingsConfig.Enabled.ColorPicker = !general.SettingsConfig.Enabled.ColorPicker;

            Assert.AreEqual(0, oldQueue.Count);
            Assert.AreEqual(1, newQueue.Count);
            Drain(newQueue);
            Assert.AreEqual(general.SettingsConfig.Enabled.ColorPicker, FindItem(newOwner, ModuleType.ColorPicker).Visible);
            Assert.AreEqual(1, general.SubscriberCount);
            Assert.AreEqual(1, keyboard.SubscriberCount);
        }

        [TestMethod]
        public void OlderQueuedSnapshotRefreshCannotOverwriteNewerOwner()
        {
            var general = new LifetimeTestRepository<GeneralSettings>(new GeneralSettings());
            var keyboard = new LifetimeTestRepository<KeyboardManagerSettings>(new KeyboardManagerSettings());
            var oldQueue = new Queue<Action>();
            var newQueue = new Queue<Action>();
            using var oldOwner = CreateViewModel(general, keyboard, oldQueue);
            general.SettingsConfig = new GeneralSettings();
            general.Notify();
            using var newOwner = CreateViewModel(general, keyboard, newQueue);
            Drain(oldQueue);
            general.SettingsConfig.Enabled.ColorPicker = !general.SettingsConfig.Enabled.ColorPicker;

            Assert.AreEqual(0, oldQueue.Count);
            Assert.AreEqual(1, newQueue.Count);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void SnapshotReplacementKeepsLatestOwnerRegardlessOfQueueOrder(bool newestFirst)
        {
            var general = new LifetimeTestRepository<GeneralSettings>(new GeneralSettings());
            var keyboard = new LifetimeTestRepository<KeyboardManagerSettings>(new KeyboardManagerSettings());
            var oldQueue = new Queue<Action>();
            var newQueue = new Queue<Action>();
            using var oldOwner = CreateViewModel(general, keyboard, oldQueue);
            using var newOwner = CreateViewModel(general, keyboard, newQueue);
            general.SettingsConfig = new GeneralSettings();
            general.Notify();
            Drain(newestFirst ? newQueue : oldQueue);
            Drain(newestFirst ? oldQueue : newQueue);
            general.SettingsConfig.Enabled.ColorPicker = !general.SettingsConfig.Enabled.ColorPicker;

            Assert.AreEqual(0, oldQueue.Count);
            Assert.AreEqual(1, newQueue.Count);
        }

        [TestMethod]
        public void SnapshotDetachDoesNotRemoveAnotherOwnersOldSnapshotCallback()
        {
            var original = new GeneralSettings();
            var general = new LifetimeTestRepository<GeneralSettings>(original);
            var keyboard = new LifetimeTestRepository<KeyboardManagerSettings>(new KeyboardManagerSettings());
            var queue = new Queue<Action>();
            using var viewModel = CreateViewModel(general, keyboard, queue);
            var calls = 0;
            Action newerCallback = () => calls++;
            original.AddEnabledModuleChangeNotification(newerCallback);
            general.SettingsConfig = new GeneralSettings();
            general.Notify();
            Drain(queue);
            original.Enabled.ColorPicker = !original.Enabled.ColorPicker;

            Assert.AreEqual(1, calls);
            Assert.AreEqual(0, queue.Count);
            original.RemoveEnabledModuleChangeNotification(newerCallback);
        }

        [TestMethod]
        public void DisposingLatestOwnerDoesNotRestoreAnOlderCallback()
        {
            var general = new LifetimeTestRepository<GeneralSettings>(new GeneralSettings());
            var keyboard = new LifetimeTestRepository<KeyboardManagerSettings>(new KeyboardManagerSettings());
            var oldQueue = new Queue<Action>();
            var newQueue = new Queue<Action>();
            using var oldOwner = CreateViewModel(general, keyboard, oldQueue);
            var newOwner = CreateViewModel(general, keyboard, newQueue);
            newOwner.Dispose();
            general.SettingsConfig.Enabled.ColorPicker = !general.SettingsConfig.Enabled.ColorPicker;
            Assert.AreEqual(0, oldQueue.Count);
            Assert.AreEqual(0, newQueue.Count);

            general.Notify();
            Drain(oldQueue);
            Assert.AreEqual(general.SettingsConfig.Enabled.ColorPicker, FindItem(oldOwner, ModuleType.ColorPicker).Visible);
            general.SettingsConfig.Enabled.ColorPicker = !general.SettingsConfig.Enabled.ColorPicker;
            Assert.AreEqual(0, oldQueue.Count);
        }

        [TestMethod]
        public void EnabledCallbackRemovalRequiresExactDelegateInstance()
        {
            var settings = new GeneralSettings();
            var calls = 0;
            Action first = () => calls++;
            var replacement = (Action)first.Clone();
            Assert.AreEqual(first, replacement);
            Assert.AreNotSame(first, replacement);
            settings.AddEnabledModuleChangeNotification(first);
            settings.AddEnabledModuleChangeNotification(replacement);
            settings.RemoveEnabledModuleChangeNotification(first);
            Assert.IsFalse(settings.TryAddEnabledModuleChangeNotification(first));
            settings.Enabled.ColorPicker = !settings.Enabled.ColorPicker;
            Assert.AreEqual(1, calls);
            settings.RemoveEnabledModuleChangeNotification(replacement);
            settings.Enabled.ColorPicker = !settings.Enabled.ColorPicker;
            Assert.AreEqual(1, calls);
        }

        [TestMethod]
        public void ReentrantDisposalStopsRemainingItemUpdates()
        {
            var general = new LifetimeTestRepository<GeneralSettings>(new GeneralSettings());
            var keyboard = new LifetimeTestRepository<KeyboardManagerSettings>(new KeyboardManagerSettings());
            var queue = new Queue<Action>();
            var viewModel = CreateViewModel(general, keyboard, queue);
            var first = FindItem(viewModel, ModuleType.ColorPicker);
            var later = FindItem(viewModel, ModuleType.FancyZones);
            var previousVisibility = later.Visible;
            first.PropertyChanged += (_, _) => viewModel.Dispose();
            general.SettingsConfig.Enabled.ColorPicker = !first.Visible;
            general.SettingsConfig.Enabled.FancyZones = !later.Visible;
            Drain(queue);

            Assert.AreEqual(previousVisibility, later.Visible);
            Assert.AreEqual(0, general.SubscriberCount);
            Assert.AreEqual(0, keyboard.SubscriberCount);
        }

        [TestMethod]
        public void DashboardUnloadReleasesSubscriptionsAndReloadReplacesModelAndItems()
        {
            var general = new LifetimeTestRepository<GeneralSettings>(new GeneralSettings());
            var keyboard = new LifetimeTestRepository<KeyboardManagerSettings>(new KeyboardManagerSettings());
            var queue = new Queue<Action>();
            var lifetime = new PageViewModelLifetime<DashboardViewModel>(
                () => new DashboardViewModel(general, _ => 0, CreateViewModel(general, keyboard, queue), enqueue: queue.Enqueue));
            var original = lifetime.ViewModel;
            var originalItems = original.QuickAccessItems;
            var originalToken = lifetime.CancellationToken;
            Assert.IsFalse(lifetime.Load());
            Assert.AreSame(original, lifetime.ViewModel);
            Assert.AreEqual(2, general.SubscriberCount);
            Assert.AreEqual(1, keyboard.SubscriberCount);

            lifetime.Unload();
            lifetime.Unload();
            Assert.IsTrue(originalToken.IsCancellationRequested);
            Assert.AreEqual(0, general.SubscriberCount);
            Assert.AreEqual(0, keyboard.SubscriberCount);
            general.SettingsConfig = new GeneralSettings();
            general.SettingsConfig.Enabled.ColorPicker = false;
            Assert.IsTrue(lifetime.Load());

            Assert.AreNotSame(original, lifetime.ViewModel);
            Assert.AreNotSame(originalItems, lifetime.ViewModel.QuickAccessItems);
            Assert.IsFalse(lifetime.CancellationToken.IsCancellationRequested);
            Assert.IsFalse(lifetime.ViewModel.QuickAccessItems.Single(item => Equals(item.Tag, ModuleType.ColorPicker)).Visible);
            Assert.IsFalse(lifetime.Load());
            Assert.AreEqual(2, general.SubscriberCount);
            Assert.AreEqual(1, keyboard.SubscriberCount);
            general.SettingsConfig.Enabled.ColorPicker = true;
            Drain(queue);
            Assert.IsTrue(lifetime.ViewModel.QuickAccessItems.Single(item => Equals(item.Tag, ModuleType.ColorPicker)).Visible);
            lifetime.Unload();
            Assert.AreEqual(0, general.SubscriberCount);
            Assert.AreEqual(0, keyboard.SubscriberCount);
        }

        [TestMethod]
        public void DashboardQueuedSettingsCallbacksAreIgnoredAfterUnload()
        {
            var general = new LifetimeTestRepository<GeneralSettings>(new GeneralSettings());
            var keyboard = new LifetimeTestRepository<KeyboardManagerSettings>(new KeyboardManagerSettings());
            var queue = new Queue<Action>();
            var dashboard = new DashboardViewModel(general, _ => 0, CreateViewModel(general, keyboard, queue), enqueue: queue.Enqueue);
            var originalOrder = dashboard.DashboardSortOrder;
            var inFlight = general.CaptureNotification();
            general.SettingsConfig = new GeneralSettings { DashboardSortOrder = DashboardSortOrder.ByStatus };
            general.Notify();
            dashboard.Dispose();
            Drain(queue);
            inFlight();

            Assert.AreEqual(originalOrder, dashboard.DashboardSortOrder);
            Assert.AreEqual(0, queue.Count);
        }

        [TestMethod]
        public void RepeatedDashboardTeardownDoesNotRetainModels()
        {
            var general = new LifetimeTestRepository<GeneralSettings>(new GeneralSettings());
            var keyboard = new LifetimeTestRepository<KeyboardManagerSettings>(new KeyboardManagerSettings());
            var queue = new Queue<Action>();
            var references = new List<WeakReference>();
            for (var i = 0; i < 20; i++)
            {
                references.AddRange(CreateAndDisposeDashboard(general, keyboard, queue));
                Assert.AreEqual(0, general.SubscriberCount);
                Assert.AreEqual(0, keyboard.SubscriberCount);
            }

            Drain(queue);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            Assert.IsTrue(references.All(reference => !reference.IsAlive));
            GC.KeepAlive(general);
            GC.KeepAlive(keyboard);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference[] CreateAndDisposeDashboard(
            LifetimeTestRepository<GeneralSettings> general,
            LifetimeTestRepository<KeyboardManagerSettings> keyboard,
            Queue<Action> queue)
        {
            var quickAccess = CreateViewModel(general, keyboard, queue);
            var dashboard = new DashboardViewModel(general, _ => 0, quickAccess, enqueue: queue.Enqueue);
            keyboard.Notify();
            var references = new[] { new WeakReference(quickAccess), new WeakReference(dashboard) };
            dashboard.Dispose();
            return references;
        }

        private static QuickAccessViewModel CreateViewModel(
            LifetimeTestRepository<GeneralSettings> general,
            LifetimeTestRepository<KeyboardManagerSettings> keyboard,
            Queue<Action> queue,
            IQuickAccessLauncher launcher = null)
        {
            return new QuickAccessViewModel(
                general,
                keyboard,
                launcher ?? Mock.Of<IQuickAccessLauncher>(),
                _ => false,
                _ => false,
                key => key,
                module => module.ToString(),
                queue.Enqueue);
        }

        private static QuickAccessItem FindItem(QuickAccessViewModel viewModel, ModuleType module)
        {
            return viewModel.Items.Single(item => Equals(item.Tag, module));
        }

        private static void Drain(Queue<Action> queue)
        {
            while (queue.TryDequeue(out var callback))
            {
                callback();
            }
        }
    }
}

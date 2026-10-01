// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ViewModelTests
{
    [TestClass]
    public class GeneralLifetimeTests
    {
        [TestMethod]
        public void DisposeDetachesRepositoryAndSharedShortcutExactlyOnce()
        {
            var repository = new LifetimeTestRepository<GeneralSettings>(new GeneralSettings());
            var queue = new Queue<Action>();
            var ipc = new List<string>();
            var viewModel = CreateViewModel(repository, queue, ipc);
            var shortcut = repository.SettingsConfig.QuickAccessShortcut;
            Assert.AreEqual(1, repository.SubscriberCount);
            Assert.AreEqual(1, SubscriberCount(shortcut));

            viewModel.Dispose();
            viewModel.Dispose();
            Assert.AreEqual(0, repository.SubscriberCount);
            Assert.AreEqual(1, repository.RemovedCount);
            Assert.AreEqual(0, SubscriberCount(shortcut));
            shortcut.HasConflict = !shortcut.HasConflict;
            viewModel.QuickAccessShortcut = new HotkeySettings();
            Assert.AreEqual(0, ipc.Count);
            Assert.AreSame(shortcut, viewModel.QuickAccessShortcut);
        }

        [TestMethod]
        public void LatePageCommandsCannotUseReleasedDelegatesOrStartBackupWork()
        {
            var repository = new LifetimeTestRepository<GeneralSettings>(new GeneralSettings());
            var queue = new Queue<Action>();
            var ipc = new List<string>();
            var viewModel = CreateViewModel(repository, queue, ipc);
            var backup = viewModel.BackupConfigsEventHandler;
            var restore = viewModel.RestoreConfigsEventHandler;
            var refresh = viewModel.RefreshBackupStatusEventHandler;
            var picker = viewModel.SelectSettingBackupDirEventHandler;
            var restart = viewModel.RestartElevatedButtonEventHandler;
            viewModel.Dispose();

            backup.Execute(null);
            restore.Execute(null);
            refresh.Execute(null);
            picker.Execute(null);
            restart.Execute(null);
            viewModel.Restart();
            viewModel.HideBackupAndRestoreMessageArea();
            viewModel.LanguagesIndex = 1;
            Assert.AreEqual(0, ipc.Count);
            Assert.AreEqual(0, repository.SubscriberCount);
        }

        [TestMethod]
        public void EqualShortcutReplacementMovesSubscriptionByIdentityWithoutPersistingSnapshot()
        {
            var repository = new LifetimeTestRepository<GeneralSettings>(new GeneralSettings());
            var queue = new Queue<Action>();
            var ipc = new List<string>();
            using var viewModel = CreateViewModel(repository, queue, ipc);
            var original = viewModel.QuickAccessShortcut;
            var latest = new GeneralSettings();
            latest.QuickAccessShortcut = new HotkeySettings(original.Win, original.Ctrl, original.Alt, original.Shift, original.Code);
            repository.SettingsConfig = latest;
            repository.Notify();
            Drain(queue);

            Assert.AreSame(latest.QuickAccessShortcut, viewModel.QuickAccessShortcut);
            Assert.AreEqual(0, SubscriberCount(original));
            Assert.AreEqual(1, SubscriberCount(latest.QuickAccessShortcut));
            Assert.AreEqual(0, ipc.Count);
            original.HasConflict = true;
            Assert.AreEqual(0, ipc.Count);
            latest.QuickAccessShortcut.HasConflict = true;
            Assert.AreEqual(1, ipc.Count);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void SnapshotKeepsConflictMetadataOnlyForUnchangedShortcut(bool changeShortcut)
        {
            var repository = new LifetimeTestRepository<GeneralSettings>(new GeneralSettings());
            var queue = new Queue<Action>();
            var ipc = new List<string>();
            using var viewModel = CreateViewModel(repository, queue, ipc);
            var original = viewModel.QuickAccessShortcut;
            original.HasConflict = true;
            original.ConflictDescription = "Existing conflict";
            original.IsSystemConflict = true;
            ipc.Clear();
            var latest = new GeneralSettings();
            latest.QuickAccessShortcut = new HotkeySettings(original.Win, original.Ctrl, original.Alt, original.Shift, changeShortcut ? original.Code + 1 : original.Code);
            repository.SettingsConfig = latest;
            repository.Notify();
            Drain(queue);

            Assert.AreEqual(!changeShortcut, viewModel.QuickAccessShortcut.HasConflict);
            Assert.AreEqual(!changeShortcut, viewModel.QuickAccessShortcut.IsSystemConflict);
            Assert.AreEqual(changeShortcut ? null : "Existing conflict", viewModel.QuickAccessShortcut.ConflictDescription);
            Assert.AreEqual(0, ipc.Count);
        }

        [TestMethod]
        public void UserShortcutReplacementDetachesPreviousOwner()
        {
            var repository = new LifetimeTestRepository<GeneralSettings>(new GeneralSettings());
            var queue = new Queue<Action>();
            var ipc = new List<string>();
            using var viewModel = CreateViewModel(repository, queue, ipc);
            var original = viewModel.QuickAccessShortcut;
            var replacement = new HotkeySettings(original.Win, original.Ctrl, original.Alt, original.Shift, original.Code);
            viewModel.QuickAccessShortcut = replacement;

            Assert.AreSame(replacement, repository.SettingsConfig.QuickAccessShortcut);
            Assert.AreEqual(0, SubscriberCount(original));
            Assert.AreEqual(1, SubscriberCount(replacement));
            Assert.AreEqual(1, ipc.Count);
        }

        [TestMethod]
        public void OutOfOrderNotificationsReadLatestRepositorySnapshot()
        {
            var repository = new LifetimeTestRepository<GeneralSettings>(new GeneralSettings());
            var queue = new Queue<Action>();
            var ipc = new List<string>();
            using var viewModel = CreateViewModel(repository, queue, ipc);
            var original = viewModel.QuickAccessShortcut;
            var stale = repository.CaptureNotification();
            var latest = new GeneralSettings { EnableQuickAccess = !viewModel.EnableQuickAccess };
            repository.SettingsConfig = latest;
            repository.Notify();
            stale();
            Drain(queue);

            Assert.AreEqual(latest.EnableQuickAccess, viewModel.EnableQuickAccess);
            Assert.AreSame(latest.QuickAccessShortcut, viewModel.QuickAccessShortcut);
            Assert.AreEqual(0, SubscriberCount(original));
            Assert.AreEqual(1, SubscriberCount(latest.QuickAccessShortcut));
            Assert.AreEqual(0, ipc.Count);
        }

        [TestMethod]
        public void QueuedAndInFlightCallbacksCannotMutateDisposedModel()
        {
            var repository = new LifetimeTestRepository<GeneralSettings>(new GeneralSettings());
            var queue = new Queue<Action>();
            var ipc = new List<string>();
            var viewModel = CreateViewModel(repository, queue, ipc);
            var enabled = viewModel.EnableQuickAccess;
            var shortcut = viewModel.QuickAccessShortcut;
            var inFlight = repository.CaptureNotification();
            var shortcutInFlight = (PropertyChangedEventHandler)ShortcutHandlers(shortcut);
            var latest = new GeneralSettings { EnableQuickAccess = !enabled };
            repository.SettingsConfig = latest;
            repository.Notify();
            viewModel.Dispose();
            Drain(queue);
            inFlight();
            shortcutInFlight(shortcut, new PropertyChangedEventArgs(nameof(HotkeySettings.HasConflict)));
            viewModel.NotifyAllBackupAndRestoreProperties();

            Assert.AreEqual(enabled, viewModel.EnableQuickAccess);
            Assert.AreSame(shortcut, viewModel.QuickAccessShortcut);
            Assert.AreEqual(0, SubscriberCount(shortcut));
            Assert.AreEqual(0, SubscriberCount(latest.QuickAccessShortcut));
            Assert.AreEqual(0, ipc.Count);
            Assert.AreEqual(0, queue.Count);
        }

        [TestMethod]
        public void ReentrantDisposalDuringSnapshotUpdateDoesNotLeaveNewShortcutSubscribed()
        {
            var repository = new LifetimeTestRepository<GeneralSettings>(new GeneralSettings());
            var queue = new Queue<Action>();
            var ipc = new List<string>();
            var viewModel = CreateViewModel(repository, queue, ipc);
            var originalEnabled = viewModel.EnableQuickAccess;
            var original = viewModel.QuickAccessShortcut;
            viewModel.PropertyChanged += (_, _) => viewModel.Dispose();
            repository.SettingsConfig = new GeneralSettings { EnableQuickAccess = !originalEnabled };
            repository.Notify();
            Drain(queue);

            Assert.AreEqual(0, SubscriberCount(original));
            Assert.AreEqual(0, SubscriberCount(repository.SettingsConfig.QuickAccessShortcut));
            Assert.AreEqual(0, repository.SubscriberCount);
            Assert.AreEqual(originalEnabled, viewModel.EnableQuickAccess);
            Assert.AreEqual(0, ipc.Count);
        }

        [TestMethod]
        public void ReentrantDisposalDuringConflictMetadataCopyDoesNotReattachShortcut()
        {
            var repository = new LifetimeTestRepository<GeneralSettings>(new GeneralSettings());
            var queue = new Queue<Action>();
            var ipc = new List<string>();
            var viewModel = CreateViewModel(repository, queue, ipc);
            var original = viewModel.QuickAccessShortcut;
            original.HasConflict = true;
            var latest = new GeneralSettings();
            latest.QuickAccessShortcut = new HotkeySettings(original.Win, original.Ctrl, original.Alt, original.Shift, original.Code);
            PropertyChangedEventHandler dispose = (_, _) => viewModel.Dispose();
            latest.QuickAccessShortcut.PropertyChanged += dispose;
            repository.SettingsConfig = latest;
            repository.Notify();
            Drain(queue);
            latest.QuickAccessShortcut.PropertyChanged -= dispose;

            Assert.AreEqual(0, SubscriberCount(original));
            Assert.AreEqual(0, SubscriberCount(latest.QuickAccessShortcut));
            Assert.AreEqual(0, repository.SubscriberCount);
        }

        [TestMethod]
        public void ReentrantDisposalDuringPropertyNotificationSuppressesIpcAndBackupCallback()
        {
            var repository = new LifetimeTestRepository<GeneralSettings>(new GeneralSettings());
            var queue = new Queue<Action>();
            var ipc = new List<string>();
            var viewModel = CreateViewModel(repository, queue, ipc);
            viewModel.PropertyChanged += (_, _) => viewModel.Dispose();
            viewModel.QuickAccessShortcut.HasConflict = true;
            Assert.AreEqual(0, ipc.Count);
            Assert.AreEqual(0, repository.SubscriberCount);
        }

        [TestMethod]
        public void SamePageReloadCreatesFreshModelAndCancelsOnlyPreviousLoad()
        {
            var repository = new LifetimeTestRepository<GeneralSettings>(new GeneralSettings());
            var queue = new Queue<Action>();
            var ipc = new List<string>();
            var lifetime = new PageViewModelLifetime<GeneralViewModel>(() => CreateViewModel(repository, queue, ipc));
            var original = lifetime.ViewModel;
            var token = lifetime.CancellationToken;
            Assert.IsFalse(lifetime.Load());
            lifetime.Unload();
            lifetime.Unload();
            Assert.IsTrue(token.IsCancellationRequested);
            Assert.AreEqual(0, repository.SubscriberCount);

            repository.SettingsConfig = new GeneralSettings { EnableQuickAccess = !original.EnableQuickAccess };
            Assert.IsTrue(lifetime.Load());
            Assert.AreNotSame(original, lifetime.ViewModel);
            Assert.AreEqual(repository.SettingsConfig.EnableQuickAccess, lifetime.ViewModel.EnableQuickAccess);
            Assert.IsFalse(lifetime.CancellationToken.IsCancellationRequested);
            Assert.IsTrue(token.IsCancellationRequested);
            Assert.AreEqual(1, repository.SubscriberCount);
            Assert.AreEqual(1, SubscriberCount(repository.SettingsConfig.QuickAccessShortcut));
            lifetime.Unload();
            Assert.AreEqual(0, repository.SubscriberCount);
            Assert.AreEqual(0, SubscriberCount(repository.SettingsConfig.QuickAccessShortcut));
        }

        [TestMethod]
        public void TerminalLifetimeDisposalReleasesSubscriptionsAndRejectsReload()
        {
            var repository = new LifetimeTestRepository<GeneralSettings>(new GeneralSettings());
            var queue = new Queue<Action>();
            var ipc = new List<string>();
            var lifetime = new PageViewModelLifetime<GeneralViewModel>(() => CreateViewModel(repository, queue, ipc));
            var token = lifetime.CancellationToken;
            lifetime.Dispose();
            lifetime.Dispose();

            Assert.IsTrue(token.IsCancellationRequested);
            Assert.AreEqual(0, repository.SubscriberCount);
            Assert.AreEqual(0, SubscriberCount(repository.SettingsConfig.QuickAccessShortcut));
            Assert.ThrowsExactly<ObjectDisposedException>(() => lifetime.Load());
        }

        [TestMethod]
        public void DisposedModelDoesNotRetainPageCallbackTargets()
        {
            var repository = new LifetimeTestRepository<GeneralSettings>(new GeneralSettings());
            var (viewModel, owner) = CreateWithPageCallbacks(repository);
            viewModel.Dispose();
            Collect();
            Assert.IsFalse(owner.IsAlive);
            GC.KeepAlive(viewModel);
            GC.KeepAlive(repository);
        }

        [TestMethod]
        public void RepeatedTeardownAllowsModelsToBeCollectedWhileRepositoryLives()
        {
            var repository = new LifetimeTestRepository<GeneralSettings>(new GeneralSettings());
            var queue = new Queue<Action>();
            var references = new List<WeakReference>();
            for (var i = 0; i < 20; i++)
            {
                references.Add(CreateAndDispose(repository, queue));
            }

            Drain(queue);
            Collect();
            Assert.IsTrue(references.All(reference => !reference.IsAlive));
            Assert.AreEqual(0, repository.SubscriberCount);
            Assert.AreEqual(0, SubscriberCount(repository.SettingsConfig.QuickAccessShortcut));
            GC.KeepAlive(repository);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference CreateAndDispose(LifetimeTestRepository<GeneralSettings> repository, Queue<Action> queue)
        {
            var viewModel = CreateViewModel(repository, queue, new List<string>());
            repository.Notify();
            viewModel.Dispose();
            return new WeakReference(viewModel);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static (GeneralViewModel ViewModel, WeakReference Owner) CreateWithPageCallbacks(LifetimeTestRepository<GeneralSettings> repository)
        {
            var owner = new PageCallbacks();
            var viewModel = new GeneralViewModel(repository, _ => { }, owner.Send, owner.Send, owner.Check, owner.Hide, owner.Refresh, owner.PickFolder);
            return (viewModel, new WeakReference(owner));
        }

        private static GeneralViewModel CreateViewModel(LifetimeTestRepository<GeneralSettings> repository, Queue<Action> queue, List<string> ipc)
        {
            return new GeneralViewModel(
                repository,
                queue.Enqueue,
                message =>
                {
                    ipc.Add(message);
                    return 0;
                },
                _ => 0,
                () => { });
        }

        private static Delegate ShortcutHandlers(HotkeySettings shortcut)
        {
            return (Delegate)typeof(HotkeySettings).GetField("PropertyChanged", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(shortcut);
        }

        private static int SubscriberCount(HotkeySettings shortcut) => ShortcutHandlers(shortcut)?.GetInvocationList().Length ?? 0;

        private static void Drain(Queue<Action> queue)
        {
            while (queue.TryDequeue(out var callback))
            {
                callback();
            }
        }

        private static void Collect()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        private sealed class PageCallbacks
        {
            internal int Send(string message) => 0;

            internal void Check()
            {
            }

            internal void Hide()
            {
            }

            internal void Refresh(int delay)
            {
            }

            internal Task<string> PickFolder() => Task.FromResult(string.Empty);
        }
    }
}

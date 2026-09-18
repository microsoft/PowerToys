// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.HotkeyConflicts;
using Microsoft.PowerToys.Settings.UI.Services;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ViewModelTests
{
    [TestClass]
    [DoNotParallelize]
    public class PageViewModelBaseLifetimeTests
    {
        private readonly List<GlobalHotkeyConflictManager> _managers = new();
        private GlobalHotkeyConflictManager _previousManager;

        [TestInitialize]
        public void Initialize()
        {
            _previousManager = GlobalHotkeyConflictManager.Instance;
        }

        [TestCleanup]
        public void Cleanup()
        {
            foreach (var manager in _managers)
            {
                var handler = typeof(GlobalHotkeyConflictManager).GetMethod("OnAllHotkeyConflictsReceived", BindingFlags.Instance | BindingFlags.NonPublic)
                    .CreateDelegate<EventHandler<AllHotkeyConflictsEventArgs>>(manager);
                IPCResponseService.AllHotkeyConflictsReceived -= handler;
            }

            typeof(GlobalHotkeyConflictManager).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, _previousManager);
        }

        [TestMethod]
        public void DisposeUnsubscribesExactManagerEvenAfterSingletonReplacement()
        {
            var original = CreateManager();
            var viewModel = new TestPageViewModel();
            Assert.AreEqual(1, SubscriberCount(original));
            var replacement = CreateManager();
            using var replacementViewModel = new TestPageViewModel();
            viewModel.Dispose();
            viewModel.Dispose();

            Assert.AreEqual(0, SubscriberCount(original));
            Assert.AreEqual(1, SubscriberCount(replacement));
        }

        [TestMethod]
        public void DisposedPageCanBeCollectedWhileConflictManagerLives()
        {
            var manager = CreateManager();
            var reference = CreateAndDispose();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            Assert.IsFalse(reference.IsAlive);
            Assert.AreEqual(0, SubscriberCount(manager));
            GC.KeepAlive(manager);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference CreateAndDispose()
        {
            var viewModel = new TestPageViewModel();
            viewModel.Dispose();
            return new WeakReference(viewModel);
        }

        [TestMethod]
        public void QueuedConflictUpdateAndLateNotificationAreIgnoredAfterDispose()
        {
            using var viewModel = new TestPageViewModel();
            viewModel.Publish();
            Assert.AreEqual(1, viewModel.Queue.Count);
            viewModel.Dispose();
            viewModel.Queue.Dequeue()();
            viewModel.Publish();

            Assert.AreEqual(0, viewModel.Queue.Count);
            Assert.AreEqual(0, viewModel.AppliedUpdates);
            Assert.IsFalse(viewModel.Shortcut.HasConflict);
        }

        [TestMethod]
        public void ConflictUpdateUsesCurrentShortcutWhenQueueRuns()
        {
            using var viewModel = new TestPageViewModel();
            var original = viewModel.Shortcut;
            viewModel.Publish();
            viewModel.Shortcut = new HotkeySettings();
            viewModel.Queue.Dequeue()();

            Assert.AreEqual(1, viewModel.AppliedUpdates);
            Assert.IsFalse(original.HasConflict);
            Assert.IsTrue(viewModel.Shortcut.HasConflict);
        }

        [TestMethod]
        public void ReentrantDisposalStopsRemainingConflictProperties()
        {
            using var viewModel = new TestPageViewModel();
            viewModel.Shortcut.PropertyChanged += (_, _) => viewModel.Dispose();
            viewModel.Publish();
            viewModel.Queue.Dequeue()();

            Assert.IsTrue(viewModel.Shortcut.HasConflict);
            Assert.IsNull(viewModel.Shortcut.ConflictDescription);
        }

        private GlobalHotkeyConflictManager CreateManager()
        {
            GlobalHotkeyConflictManager.Initialize(_ => 0);
            var manager = GlobalHotkeyConflictManager.Instance;
            _managers.Add(manager);
            return manager;
        }

        private static int SubscriberCount(GlobalHotkeyConflictManager manager)
        {
            var handlers = (Delegate)typeof(GlobalHotkeyConflictManager).GetField("ConflictsUpdated", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
            return handlers?.GetInvocationList().Length ?? 0;
        }

        private sealed class TestPageViewModel : PageViewModelBase
        {
            internal Queue<Action> Queue { get; } = new();

            internal HotkeySettings Shortcut { get; set; } = new();

            internal int AppliedUpdates { get; private set; }

            protected override string ModuleName => "Test";

            internal void Publish() => OnConflictsUpdated(this, new AllHotkeyConflictsEventArgs(new AllHotkeyConflictsData()));

            public override Dictionary<string, HotkeySettings[]> GetAllHotkeySettings() => new() { { ModuleName, new[] { Shortcut } } };

            protected override void EnqueueConflictUpdate(Action update) => Queue.Enqueue(update);

            protected override void UpdateHotkeyConflictStatus(AllHotkeyConflictsData allConflicts) => AppliedUpdates++;

            protected override bool GetHotkeyConflictStatus(string key) => true;

            protected override string GetHotkeyConflictTooltip(string key) => "Conflict";
        }
    }
}

// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.PowerToys.Settings.UI.Views;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ViewModelTests
{
    [TestClass]
    public class GeneralCompiledBindingsLifetimeTests
    {
        [TestMethod]
        public void StopTrackingClearsActualGeneratedCachesAndUnsubscribesEveryPublisher()
        {
            var repository = new LifetimeTestRepository<GeneralSettings>(new GeneralSettings());
            using var sharedUpdate = CreateSharedUpdate(repository);
            using var viewModel = CreateViewModel(repository);
            using var bindings = new GeneratedBindings();
            bindings.Track(viewModel, sharedUpdate);

            Assert.AreEqual(1, SubscriberCount(sharedUpdate));
            Assert.AreEqual(1, SubscriberCount(viewModel));
            Assert.AreEqual(2, SubscriberCount(viewModel.QuickAccessShortcut));
            viewModel.Dispose();
            Assert.AreEqual(1, SubscriberCount(viewModel.QuickAccessShortcut));

            bindings.StopTracking();
            bindings.StopTracking();
            Assert.AreEqual(0, SubscriberCount(sharedUpdate));
            Assert.AreEqual(0, SubscriberCount(viewModel));
            Assert.AreEqual(0, SubscriberCount(viewModel.QuickAccessShortcut));
            Assert.IsNull(bindings.Cached("ViewModel"));
            Assert.IsNull(bindings.Cached("ViewModel_QuickAccessShortcut"));
            Assert.IsNull(bindings.Cached("SharedUpdateViewModel"));
            GC.KeepAlive(bindings);
        }

        [TestMethod]
        public void StoppedTrackersAndModelsAreCollectibleWhileSharedPublishersStayAlive()
        {
            var repository = new LifetimeTestRepository<GeneralSettings>(new GeneralSettings());
            using var sharedUpdate = CreateSharedUpdate(repository);
            var references = new List<WeakReference>();
            for (var i = 0; i < 20; i++)
            {
                references.AddRange(CreateDisposedBindings(repository, sharedUpdate, stopTracking: true));
            }

            Collect();
            Assert.IsTrue(references.All(reference => !reference.IsAlive));
            Assert.AreEqual(0, SubscriberCount(sharedUpdate));
            Assert.AreEqual(0, SubscriberCount(repository.SettingsConfig.QuickAccessShortcut));
            Assert.AreEqual(0, repository.SubscriberCount);
            GC.KeepAlive(sharedUpdate);
            GC.KeepAlive(repository);
        }

        [TestMethod]
        public void UnstoppedGeneratedTrackerRetainsDisposedModelDespiteWeakBindingReference()
        {
            var repository = new LifetimeTestRepository<GeneralSettings>(new GeneralSettings());
            using var sharedUpdate = CreateSharedUpdate(repository);
            var references = CreateDisposedBindings(repository, sharedUpdate, stopTracking: false);
            try
            {
                Collect();

                // Neither publisher raises another event: generated weak-target cleanup
                // cannot hide the same retention that occurs in an idle Settings window.
                Assert.IsTrue(references[0].IsAlive, "Disposed GeneralViewModel is still cached by the tracker.");
                Assert.IsTrue(references[1].IsAlive, "Shared publishers retain the generated tracker.");
                Assert.IsFalse(references[2].IsAlive, "The weak binding-object backlink does not root the binding.");
                Assert.AreEqual(1, SubscriberCount(sharedUpdate));
                Assert.AreEqual(1, SubscriberCount(repository.SettingsConfig.QuickAccessShortcut));
            }
            finally
            {
                ReleaseTracker(references[1]);
            }

            Collect();
            Assert.IsTrue(references.All(reference => !reference.IsAlive));
            GC.KeepAlive(sharedUpdate);
            GC.KeepAlive(repository);
        }

        [TestMethod]
        public void SameGeneratedTrackerReattachesOnlyTheReplacementModel()
        {
            var repository = new LifetimeTestRepository<GeneralSettings>(new GeneralSettings());
            using var sharedUpdate = CreateSharedUpdate(repository);
            using var lifetime = new PageViewModelLifetime<GeneralViewModel>(() => CreateViewModel(repository));
            using var bindings = new GeneratedBindings();
            var original = lifetime.ViewModel;
            bindings.Track(original, sharedUpdate);
            bindings.StopTracking();
            lifetime.Unload();

            Assert.IsTrue(lifetime.Load());
            bindings.Track(lifetime.ViewModel, sharedUpdate);
            Assert.AreNotSame(original, lifetime.ViewModel);
            Assert.AreSame(lifetime.ViewModel, bindings.Cached("ViewModel"));
            Assert.AreEqual(0, SubscriberCount(original));
            Assert.AreEqual(1, SubscriberCount(lifetime.ViewModel));
            Assert.AreEqual(1, SubscriberCount(sharedUpdate));
            Assert.AreEqual(2, SubscriberCount(repository.SettingsConfig.QuickAccessShortcut));

            bindings.StopTracking();
            lifetime.Unload();
            Assert.IsNull(bindings.Cached("ViewModel"));
            Assert.AreEqual(0, SubscriberCount(sharedUpdate));
            Assert.AreEqual(0, SubscriberCount(repository.SettingsConfig.QuickAccessShortcut));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference[] CreateDisposedBindings(
            LifetimeTestRepository<GeneralSettings> repository,
            UpdateViewModel sharedUpdate,
            bool stopTracking)
        {
            var viewModel = CreateViewModel(repository);
            var bindings = new GeneratedBindings();
            bindings.Track(viewModel, sharedUpdate);
            var references = new[] { new WeakReference(viewModel), bindings.TrackerReference, bindings.BindingReference };
            viewModel.Dispose();
            if (stopTracking)
            {
                bindings.StopTracking();
            }

            return references;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ReleaseTracker(WeakReference reference)
        {
            var tracker = reference.Target;
            Assert.IsNotNull(tracker);
            tracker.GetType().GetMethod("ReleaseAllListeners").Invoke(tracker, null);
        }

        private static GeneralViewModel CreateViewModel(LifetimeTestRepository<GeneralSettings> repository)
        {
            return new GeneralViewModel(repository, _ => { }, _ => 0, _ => 0, () => { });
        }

        private static UpdateViewModel CreateSharedUpdate(LifetimeTestRepository<GeneralSettings> repository)
        {
            return new UpdateViewModel(repository, _ => 0, () => new UpdatingSettings(), () => { }, false, false, null);
        }

        private static int SubscriberCount(INotifyPropertyChanged source)
        {
            var declaringType = source is HotkeySettings ? typeof(HotkeySettings) : typeof(Observable);
            var handlers = (Delegate)declaringType.GetField("PropertyChanged", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(source);
            return handlers?.GetInvocationList().Length ?? 0;
        }

        private static void Collect()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        // Exercise the compiler's actual tracker without constructing a WinUI page
        // or replacing its event/cache behavior with a test implementation.
        private sealed class GeneratedBindings : IDisposable
        {
            private readonly object _bindings;
            private readonly object _tracker;

            internal GeneratedBindings()
            {
                var type = typeof(GeneralPage).GetNestedType("GeneralPage_obj1_Bindings", BindingFlags.NonPublic);
                Assert.IsNotNull(type);
                _bindings = Activator.CreateInstance(type, nonPublic: true);
                _tracker = type.GetField("bindingsTracking", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_bindings);
            }

            internal WeakReference TrackerReference => new(_tracker);

            internal WeakReference BindingReference => new(_bindings);

            internal void Track(GeneralViewModel model, UpdateViewModel sharedUpdate)
            {
                SetListener("ViewModel", model);
                SetListener("ViewModel_QuickAccessShortcut", model.QuickAccessShortcut);
                SetListener("SharedUpdateViewModel", sharedUpdate);
            }

            internal object Cached(string path)
            {
                return _tracker.GetType().GetField("cache_" + path, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_tracker);
            }

            internal void StopTracking() => _bindings.GetType().GetMethod("StopTracking").Invoke(_bindings, null);

            private void SetListener(string path, object source)
            {
                _tracker.GetType().GetMethod("UpdateChildListeners_" + path).Invoke(_tracker, new[] { source });
            }

            public void Dispose() => StopTracking();
        }
    }
}

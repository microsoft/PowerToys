// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using CoreWidgetProvider.Widgets.Enums;
using Microsoft.CommandPalette.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Foundation;

namespace Microsoft.CmdPal.Ext.PerformanceMonitor.UnitTests;

[TestClass]
public partial class PageActivationTests
{
    private sealed partial class TrackingPage : OnLoadBasePage
    {
        public int LoadCount { get; private set; }

        public int UnloadCount { get; private set; }

        public void TriggerItemsChanged() => RaiseItemsChanged();

        protected override void Loaded() => LoadCount++;

        protected override void Unloaded() => UnloadCount++;
    }

    private sealed partial class TrackingWidgetPage : WidgetPage
    {
        public int ActivationCount { get; private set; }

        public int DeactivationCount { get; private set; }

        protected override void LoadContentData()
        {
        }

        protected override string GetTemplatePath(WidgetPageState page) => string.Empty;

        protected override void OnActivated() => ActivationCount++;

        protected override void OnDeactivated() => DeactivationCount++;
    }

    private sealed partial class ThrowingTrackingPage : OnLoadBasePage
    {
        public int LoadAttempts { get; private set; }

        public int UnloadAttempts { get; private set; }

        public int RemainingLoadFailures { get; set; }

        public int RemainingUnloadFailures { get; set; }

        protected override void Loaded()
        {
            LoadAttempts++;
            if (RemainingLoadFailures > 0)
            {
                RemainingLoadFailures--;
                throw new InvalidOperationException("Load failed.");
            }
        }

        protected override void Unloaded()
        {
            UnloadAttempts++;
            if (RemainingUnloadFailures > 0)
            {
                RemainingUnloadFailures--;
                throw new InvalidOperationException("Unload failed.");
            }
        }
    }

    private sealed partial class ThrowingTrackingWidgetPage : WidgetPage
    {
        public int ActivationAttempts { get; private set; }

        public int DeactivationAttempts { get; private set; }

        public int RemainingActivationFailures { get; set; }

        public int RemainingDeactivationFailures { get; set; }

        protected override void LoadContentData()
        {
        }

        protected override string GetTemplatePath(WidgetPageState page) => string.Empty;

        protected override void OnActivated()
        {
            ActivationAttempts++;
            if (RemainingActivationFailures > 0)
            {
                RemainingActivationFailures--;
                throw new InvalidOperationException("Activation failed.");
            }
        }

        protected override void OnDeactivated()
        {
            DeactivationAttempts++;
            if (RemainingDeactivationFailures > 0)
            {
                RemainingDeactivationFailures--;
                throw new InvalidOperationException("Deactivation failed.");
            }
        }
    }

    [TestMethod]
    public void RemovingUnknownHandler_DoesNotUnloadPage()
    {
        var page = new TrackingPage();
        TypedEventHandler<object, IItemsChangedEventArgs> handler = (_, _) => { };

        page.ItemsChanged -= handler;

        Assert.AreEqual(0, page.LoadCount);
        Assert.AreEqual(0, page.UnloadCount);
    }

    [TestMethod]
    public void RemovingDifferentHandler_KeepsPageLoadedAndSubscribed()
    {
        var page = new TrackingPage();
        var notifications = 0;
        TypedEventHandler<object, IItemsChangedEventArgs> subscribed = (_, _) => notifications++;
        TypedEventHandler<object, IItemsChangedEventArgs> unknown = (_, _) => { };

        page.ItemsChanged += subscribed;
        page.ItemsChanged -= unknown;
        page.TriggerItemsChanged();

        Assert.AreEqual(1, page.LoadCount);
        Assert.AreEqual(0, page.UnloadCount);
        Assert.AreEqual(1, notifications);

        page.ItemsChanged -= subscribed;
        Assert.AreEqual(1, page.UnloadCount);
    }

    [TestMethod]
    public void DuplicateHandler_UnloadsOnlyAfterFinalRemoval()
    {
        var page = new TrackingPage();
        TypedEventHandler<object, IItemsChangedEventArgs> handler = (_, _) => { };

        page.ItemsChanged += handler;
        page.ItemsChanged += handler;
        page.ItemsChanged -= handler;

        Assert.AreEqual(1, page.LoadCount);
        Assert.AreEqual(0, page.UnloadCount);

        page.ItemsChanged -= handler;
        page.ItemsChanged -= handler;

        Assert.AreEqual(1, page.LoadCount);
        Assert.AreEqual(1, page.UnloadCount);
    }

    [TestMethod]
    public void WidgetActivation_UsesZeroToOneAndOneToZeroTransitions()
    {
        var page = new TrackingWidgetPage();

        page.PopActivate();
        page.PushActivate();
        page.PushActivate();
        page.PopActivate();

        Assert.AreEqual(1, page.ActivationCount);
        Assert.AreEqual(0, page.DeactivationCount);

        page.PopActivate();
        page.PopActivate();

        Assert.AreEqual(1, page.ActivationCount);
        Assert.AreEqual(1, page.DeactivationCount);

        page.PushActivate();

        Assert.AreEqual(2, page.ActivationCount);
        Assert.AreEqual(1, page.DeactivationCount);
    }

    [TestMethod]
    public void LoadFailure_IsContainedAndRetriedOnNextSubscriptionChange()
    {
        var page = new ThrowingTrackingPage { RemainingLoadFailures = 1 };
        TypedEventHandler<object, IItemsChangedEventArgs> first = (_, _) => { };
        TypedEventHandler<object, IItemsChangedEventArgs> second = (_, _) => { };

        page.ItemsChanged += first;
        Assert.AreEqual(1, page.LoadAttempts);

        page.ItemsChanged += second;
        Assert.AreEqual(2, page.LoadAttempts);

        page.ItemsChanged -= first;
        Assert.AreEqual(0, page.UnloadAttempts);

        page.ItemsChanged -= second;
        Assert.AreEqual(1, page.UnloadAttempts);
    }

    [TestMethod]
    public void UnloadFailure_IsContainedAndRetriedOnNextSubscriptionChange()
    {
        var page = new ThrowingTrackingPage { RemainingUnloadFailures = 1 };
        TypedEventHandler<object, IItemsChangedEventArgs> handler = (_, _) => { };

        page.ItemsChanged += handler;
        page.ItemsChanged -= handler;
        Assert.AreEqual(1, page.UnloadAttempts);

        page.ItemsChanged -= handler;
        Assert.AreEqual(2, page.UnloadAttempts);
    }

    [TestMethod]
    public void ActivationFailure_IsContainedAndRetriedWithoutLosingOwners()
    {
        var page = new ThrowingTrackingWidgetPage { RemainingActivationFailures = 1 };

        page.PushActivate();
        Assert.AreEqual(1, page.ActivationAttempts);

        page.PushActivate();
        Assert.AreEqual(2, page.ActivationAttempts);

        page.PopActivate();
        Assert.AreEqual(0, page.DeactivationAttempts);

        page.PopActivate();
        Assert.AreEqual(1, page.DeactivationAttempts);
    }

    [TestMethod]
    public void DeactivationFailure_IsContainedAndRetriedAtZeroOwners()
    {
        var page = new ThrowingTrackingWidgetPage { RemainingDeactivationFailures = 1 };

        page.PushActivate();
        page.PopActivate();
        Assert.AreEqual(1, page.DeactivationAttempts);

        page.PopActivate();
        Assert.AreEqual(2, page.DeactivationAttempts);
    }
}

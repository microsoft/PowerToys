// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Foundation;

namespace Microsoft.CommandPalette.Extensions.Toolkit.UnitTests;

[TestClass]
public class EventNotificationTests
{
    [TestMethod]
    [DataRow(unchecked((int)0x800706BA), 1)]
    [DataRow(unchecked((int)0x80010108), 1)]
    [DataRow(unchecked((int)0x80004005), 2)]
    public void PropChanged_ContinuesAfterFailureAndRemovesOnlyDeadSubscribers(int hresult, int expectedFailures)
    {
        var page = new Page();
        var failures = 0;
        List<(object Sender, string PropertyName, string Title)> notifications = [];
        page.PropChanged += (_, _) =>
        {
            failures++;
            Marshal.ThrowExceptionForHR(hresult);
        };
        page.PropChanged += (sender, args) => notifications.Add((sender, args.PropertyName, page.Title));

        page.Title = "First";
        page.Title = "Second";
        page.Title = "Second";

        Assert.AreEqual("Second", page.Title);
        Assert.AreEqual(expectedFailures, failures);
        CollectionAssert.AreEqual(
            new[] { ((object)page, nameof(Page.Title), "First"), ((object)page, nameof(Page.Title), "Second") },
            notifications);
    }

    [TestMethod]
    public void ItemsChanged_AllPublishersContinueAfterFailureAndPreserveSubscriptionChanges()
    {
        var listPage = new TestListPage();
        VerifyItemNotifications(listPage, listPage.NotifyChanged);

        var contentPage = new TestContentPage();
        VerifyItemNotifications(contentPage, contentPage.NotifyChanged);

        var treeContent = new TestTreeContent();
        VerifyItemNotifications(treeContent, treeContent.NotifyChanged);

        var provider = new TestCommandProvider();
        VerifyItemNotifications(provider, provider.NotifyChanged);
    }

    private static void VerifyItemNotifications(INotifyItemsChanged source, Action<int> raise)
    {
        raise(0);

        var unavailableCalls = 0;
        var disconnectedCalls = 0;
        var otherFailureCalls = 0;
        var addedCalls = 0;
        List<(object Sender, int TotalItems)> notifications = [];
        TypedEventHandler<object, IItemsChangedEventArgs> addedHandler = (_, _) => addedCalls++;
        TypedEventHandler<object, IItemsChangedEventArgs> healthyHandler =
            (sender, args) => notifications.Add((sender, args.TotalItems));

        source.ItemsChanged += (_, _) =>
        {
            unavailableCalls++;
            source.ItemsChanged += addedHandler;
            Marshal.ThrowExceptionForHR(unchecked((int)0x800706BA));
        };
        source.ItemsChanged += (_, _) =>
        {
            disconnectedCalls++;
            Marshal.ThrowExceptionForHR(unchecked((int)0x80010108));
        };
        source.ItemsChanged += (_, _) =>
        {
            otherFailureCalls++;
            throw new InvalidOperationException("Ordinary callback failure");
        };
        source.ItemsChanged += healthyHandler;

        raise(1);
        Assert.AreEqual(0, addedCalls, "A new subscriber joins the next notification.");
        raise(2);
        source.ItemsChanged -= healthyHandler;
        raise(3);

        Assert.AreEqual(1, unavailableCalls, source.GetType().Name);
        Assert.AreEqual(1, disconnectedCalls, source.GetType().Name);
        Assert.AreEqual(3, otherFailureCalls, "Ordinary failures must not unsubscribe the callback.");
        Assert.AreEqual(2, addedCalls, "Removing a dead callback must preserve a newly added subscriber.");
        CollectionAssert.AreEqual(new[] { ((object)source, 1), ((object)source, 2) }, notifications);
    }

    private sealed partial class TestListPage : ListPage
    {
        public void NotifyChanged(int totalItems) => RaiseItemsChanged(totalItems);
    }

    private sealed partial class TestContentPage : ContentPage
    {
        public override IContent[] GetContent() => [];

        public void NotifyChanged(int totalItems) => RaiseItemsChanged(totalItems);
    }

    private sealed partial class TestTreeContent : TreeContent
    {
        public void NotifyChanged(int totalItems) => RaiseItemsChanged(totalItems);
    }

    private sealed partial class TestCommandProvider : CommandProvider
    {
        public override ICommandItem[] TopLevelCommands() => [];

        public void NotifyChanged(int totalItems) => RaiseItemsChanged(totalItems);
    }
}

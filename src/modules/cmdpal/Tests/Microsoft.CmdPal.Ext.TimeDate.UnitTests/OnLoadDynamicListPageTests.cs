// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.CmdPal.Ext.TimeDate.Pages;
using Microsoft.CommandPalette.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Foundation;

namespace Microsoft.CmdPal.Ext.TimeDate.UnitTests;

[TestClass]
public partial class OnLoadDynamicListPageTests
{
    private static readonly int[] _expectedItemCounts = [1, 2, 3];

    [TestMethod]
    [DataRow(unchecked((int)0x800706BA))]
    [DataRow(unchecked((int)0x80010108))]
    [DataRow(unchecked((int)0x80004005))]
    public void ItemsChanged_ContinuesAfterSubscriberFailureWithoutChangingPageLifetime(int hresult)
    {
        var page = new TrackingPage();
        var failures = 0;
        List<int> notifications = [];
        TypedEventHandler<object, IItemsChangedEventArgs> broken = (_, _) =>
        {
            failures++;
            Marshal.ThrowExceptionForHR(hresult);
        };
        TypedEventHandler<object, IItemsChangedEventArgs> healthy = (_, args) => notifications.Add(args.TotalItems);
        page.ItemsChanged += broken;
        page.ItemsChanged += healthy;

        page.TriggerItemsChanged(1);
        page.TriggerItemsChanged(2);
        page.ItemsChanged -= broken;
        page.TriggerItemsChanged(3);

        Assert.AreEqual(2, failures);
        CollectionAssert.AreEqual(_expectedItemCounts, notifications);
        Assert.AreEqual(1, page.LoadCount);
        Assert.AreEqual(0, page.UnloadCount);

        page.ItemsChanged -= healthy;
        Assert.AreEqual(1, page.UnloadCount);
    }

    private sealed partial class TrackingPage : OnLoadDynamicListPage
    {
        public int LoadCount { get; private set; }

        public int UnloadCount { get; private set; }

        public override IListItem[] GetItems() => [];

        public override void UpdateSearchText(string oldSearch, string newSearch)
        {
        }

        public void TriggerItemsChanged(int totalItems) => RaiseItemsChanged(totalItems);

        protected override void Loaded() => LoadCount++;

        protected override void Unloaded() => UnloadCount++;
    }
}

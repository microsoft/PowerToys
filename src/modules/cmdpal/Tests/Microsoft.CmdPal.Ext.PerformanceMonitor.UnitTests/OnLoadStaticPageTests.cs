// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Foundation;

namespace Microsoft.CmdPal.Ext.PerformanceMonitor.UnitTests;

[TestClass]
public sealed partial class OnLoadStaticPageTests
{
    private sealed partial class TestPage : OnLoadStaticListPage
    {
        public int LoadedCount { get; private set; }

        public int UnloadedCount { get; private set; }

        public override IListItem[] GetItems() => [];

        protected override void Loaded() => LoadedCount++;

        protected override void Unloaded() => UnloadedCount++;
    }

    [TestMethod]
    public void RemovingBeforeAdding_DoesNotSuppressLaterLoad()
    {
        var page = new TestPage();
        TypedEventHandler<object, IItemsChangedEventArgs> handler = (_, _) => { };

        page.ItemsChanged -= handler;
        page.ItemsChanged += handler;

        Assert.AreEqual(1, page.LoadedCount);
        Assert.AreEqual(0, page.UnloadedCount);

        page.ItemsChanged -= handler;

        Assert.AreEqual(1, page.UnloadedCount);
    }

    [TestMethod]
    public void RemovingUnknownSubscriber_DoesNotUnloadActivePage()
    {
        var page = new TestPage();
        TypedEventHandler<object, IItemsChangedEventArgs> activeHandler = (_, _) => { };
        TypedEventHandler<object, IItemsChangedEventArgs> unknownHandler = (_, _) => { };

        page.ItemsChanged += activeHandler;
        page.ItemsChanged -= unknownHandler;

        Assert.AreEqual(1, page.LoadedCount);
        Assert.AreEqual(0, page.UnloadedCount);

        page.ItemsChanged -= activeHandler;

        Assert.AreEqual(1, page.UnloadedCount);
    }
}

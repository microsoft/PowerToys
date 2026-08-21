// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

/// <summary>
/// Round 2, phase 3 adapter remediation. Covers r2-p3-01 pagination load,
/// r2-p3-03 registry init ordering, r2-p3-04 context icon fallback, and
/// r2-p3-05 status disposal synchronization. Shared helpers and the recording
/// host live in the primary <see cref="JSAdapterRemediationTests"/> partial.
/// </summary>
public partial class JSAdapterRemediationTests
{
    // r2-p3-01: LoadMore folds the loaded page into pagination state and raises
    // ItemsChanged so the host asks GetItems again and sees the appended items.
    // It stops once the extension reports the final page.
    [TestMethod]
    public async Task ListPage_LoadMoreRaisesItemsChangedAndAppendsItems()
    {
        using var fake = new JSFakeExtension();
        var loaded = 0;

        fake.OnRequest("listPage/getItems", _ =>
        {
            var items = new JsonArray { new JsonObject { ["title"] = "item-0" } };
            if (Volatile.Read(ref loaded) >= 1)
            {
                items.Add(new JsonObject { ["title"] = "item-1" });
            }

            return new JsonObject
            {
                ["items"] = items,
                ["hasMoreItems"] = Volatile.Read(ref loaded) < 1,
            };
        });

        fake.OnRequest("listPage/loadMore", _ =>
        {
            Interlocked.Increment(ref loaded);
            return new JsonObject { ["hasMoreItems"] = false, ["totalItems"] = 2 };
        });

        var page = new JSListPageProxy("pager", fake.Connection);
        var changed = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        page.ItemsChanged += (_, args) => changed.TrySetResult(args.TotalItems);

        var firstItems = await Task.Run(() => page.GetItems());
        Assert.AreEqual(1, firstItems.Length);
        Assert.IsTrue(page.HasMoreItems);

        await Task.Run(() => page.LoadMore());

        var total = await changed.Task.WaitAsync(Timeout);
        Assert.AreEqual(2, total);
        Assert.IsFalse(page.HasMoreItems);

        var secondItems = await Task.Run(() => page.GetItems());
        Assert.AreEqual(2, secondItems.Length);
    }

    // r2-p3-01: a loadMore response with no hasMoreItems flag is the final page,
    // so no further LoadMore is issued.
    [TestMethod]
    public async Task ListPage_LoadMoreWithoutHasMoreItemsStopsPaging()
    {
        using var fake = new JSFakeExtension();
        var loadMoreCount = 0;

        fake.OnRequest("listPage/getItems", _ => new JsonObject
        {
            ["items"] = new JsonArray(),
            ["hasMoreItems"] = true,
        });

        fake.OnRequest("listPage/loadMore", _ =>
        {
            Interlocked.Increment(ref loadMoreCount);
            return new JsonObject();
        });

        var page = new JSListPageProxy("pager", fake.Connection);

        await Task.Run(() => page.GetItems());
        Assert.IsTrue(page.HasMoreItems);

        await Task.Run(() => page.LoadMore());
        Assert.IsFalse(page.HasMoreItems);

        await Task.Run(() => page.LoadMore());
        Assert.AreEqual(1, Volatile.Read(ref loadMoreCount));
    }

    // r2-p3-03: concurrent proxies on one connection share the retained registry.
    // The itemsChanged handler binds to the same registry the proxies use, so the
    // notification is not lost to a discarded registry.
    [TestMethod]
    public async Task ListPage_ConcurrentInitBindsItemsChangedToRetainedRegistry()
    {
        using var fake = new JSFakeExtension();
        const int count = 32;
        var proxies = new JSListPageProxy[count];

        Parallel.For(0, count, i => proxies[i] = new JSListPageProxy("race-page", fake.Connection));

        var received = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        foreach (var proxy in proxies)
        {
            proxy.ItemsChanged += (_, args) => received.TrySetResult(args.TotalItems);
        }

        await fake.PushNotificationAsync(
            "listPage/itemsChanged",
            new JsonObject { ["pageId"] = "race-page", ["totalItems"] = 7 });

        var total = await received.Task.WaitAsync(Timeout);
        Assert.AreEqual(7, total);

        foreach (var proxy in proxies)
        {
            GC.KeepAlive(proxy);
        }
    }

    // r2-p3-04: a context item with no icon of its own inherits the command's
    // icon instead of being overwritten with an empty glyph.
    [TestMethod]
    public void ContextItem_AbsentIconFallsBackToCommandIcon()
    {
        using var fake = new JSFakeExtension();
        var element = ParseElement(new JsonObject
        {
            ["title"] = "Context Item",
            ["command"] = new JsonObject
            {
                ["id"] = "c",
                ["name"] = "C",
                ["icon"] = new JsonObject { ["light"] = new JsonObject { ["icon"] = "CTXCMD" } },
            },
        });

        var item = (CommandContextItem)JSModelMapper.ParseContextItem(element, fake.Connection);
        Assert.AreEqual("CTXCMD", item.Icon!.Light.Icon);
    }

    // r2-p3-04: a context item that carries its own icon keeps it and does not
    // fall back to the command's icon.
    [TestMethod]
    public void ContextItem_OwnIconTakesPrecedenceOverCommandIcon()
    {
        using var fake = new JSFakeExtension();
        var element = ParseElement(new JsonObject
        {
            ["title"] = "Context Item",
            ["icon"] = new JsonObject { ["light"] = new JsonObject { ["icon"] = "OWN" } },
            ["command"] = new JsonObject
            {
                ["id"] = "c",
                ["name"] = "C",
                ["icon"] = new JsonObject { ["light"] = new JsonObject { ["icon"] = "CTXCMD" } },
            },
        });

        var item = (CommandContextItem)JSModelMapper.ParseContextItem(element, fake.Connection);
        Assert.AreEqual("OWN", item.Icon!.Light.Icon);
    }

    // r2-p3-05: status notifications racing Dispose never enumerate the status map
    // while it is changing. Before the fix, Dispose could throw when the collection
    // changed mid-enumeration. This check is best effort because the race depends
    // on timing. Dispose should complete and leave consistent counts.
    [TestMethod]
    public async Task Status_ConcurrentNotificationsDuringDisposeStaySynchronized()
    {
        using var fake = new JSFakeExtension();
        var provider = CreateProvider(fake);
        var host = new RecordingExtensionHost();
        provider.InitializeWithHost(host);

        const int seeded = 25;
        for (var i = 0; i < seeded; i++)
        {
            await fake.PushNotificationAsync(
                "host/showStatus",
                new JsonObject
                {
                    ["statusId"] = $"status-{i}",
                    ["message"] = new JsonObject { ["Message"] = $"m{i}", ["State"] = 0 },
                });
        }

        await WaitForAsync(() => host.ShownCount == seeded);

        var flood = Task.Run(async () =>
        {
            for (var i = seeded; i < seeded + 50; i++)
            {
                await fake.PushNotificationAsync(
                    "host/showStatus",
                    new JsonObject
                    {
                        ["statusId"] = $"status-{i}",
                        ["message"] = new JsonObject { ["Message"] = $"m{i}", ["State"] = 0 },
                    });
            }
        });

        // Dispose runs on this thread and rethrows if enumerating the status map
        // races a mutation, so completing without throwing is the assertion.
        provider.Dispose();
        await flood;

        Assert.IsTrue(host.HiddenCount >= 1);
        Assert.IsTrue(host.HiddenCount <= host.ShownCount);
    }
}

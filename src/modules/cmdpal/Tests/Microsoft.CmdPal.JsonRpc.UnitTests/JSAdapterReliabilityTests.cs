// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.JsonRpc.Models;
using Microsoft.CommandPalette.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Foundation;

namespace Microsoft.CmdPal.JsonRpc.UnitTests;

public partial class JSAdapterTests
{
    // A loadMore that fails with a JSON-RPC error must not strand the
    // host in its loading state. HasMoreItems settles to false and ItemsChanged
    // is raised so the host clears its spinner and re-queries.
    [TestMethod]
    public async Task ListPage_LoadMoreErrorClearsLoadingAndSettlesPaging()
    {
        using var fake = new JSFakeExtension();
        fake.OnRequest("listPage/getItems", _ => new JsonObject
        {
            ["items"] = new JsonArray { new JsonObject { ["title"] = "item-0" } },
            ["hasMoreItems"] = true,
        });
        fake.OnError("listPage/loadMore", JsonRpcError.InternalError, "boom");

        var page = new JSListPageProxy("pager", fake.Connection);
        var changed = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        page.ItemsChanged += (_, args) => changed.TrySetResult(args.TotalItems);

        await Task.Run(() => page.GetItems());
        Assert.IsTrue(page.HasMoreItems);

        await Task.Run(() => page.LoadMore());

        var total = await changed.Task.WaitAsync(Timeout);
        Assert.AreEqual(-1, total);
        Assert.IsFalse(page.HasMoreItems);

        // Paging has settled, so a further LoadMore short-circuits without issuing
        // another request rather than retrying the failed page.
        await Task.Run(() => page.LoadMore());
        Assert.IsFalse(page.HasMoreItems);

        GC.KeepAlive(page);
    }

    // The itemsChanged handler is bound before the constructor returns,
    // so a notification pushed immediately after subscribing is not dropped.
    [TestMethod]
    public async Task ListPage_NotificationRightAfterSubscriptionIsDelivered()
    {
        using var fake = new JSFakeExtension();

        var page = new JSListPageProxy("fresh-page", fake.Connection);
        var received = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        page.ItemsChanged += (_, args) => received.TrySetResult(args.TotalItems);

        await fake.PushNotificationAsync(
            "listPage/itemsChanged",
            new JsonObject { ["pageId"] = "fresh-page", ["totalItems"] = 3 });

        Assert.AreEqual(3, await received.Task.WaitAsync(Timeout));
        GC.KeepAlive(page);
    }

    // Constructing proxies concurrently on one connection keeps the
    // subscription under one lock, so the notification is delivered no matter
    // which constructor wins the registration race.
    [TestMethod]
    public async Task ListPage_ConcurrentSubscriptionNeverDropsNotification()
    {
        for (var iteration = 0; iteration < 25; iteration++)
        {
            using var fake = new JSFakeExtension();
            const int count = 8;
            var proxies = new JSListPageProxy[count];

            Parallel.For(0, count, i => proxies[i] = new JSListPageProxy("race", fake.Connection));

            var received = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            foreach (var proxy in proxies)
            {
                proxy.ItemsChanged += (_, args) => received.TrySetResult(args.TotalItems);
            }

            await fake.PushNotificationAsync(
                "listPage/itemsChanged",
                new JsonObject { ["pageId"] = "race", ["totalItems"] = 11 });

            Assert.AreEqual(11, await received.Task.WaitAsync(Timeout));

            foreach (var proxy in proxies)
            {
                GC.KeepAlive(proxy);
            }
        }
    }

    // A host notification that arrives before InitializeWithHost is
    // buffered and replayed once the host is attached. The itemsChanged pushed
    // afterward runs behind it, which proves FIFO order.
    [TestMethod]
    public async Task Provider_HostNotificationBeforeInitIsReplayedAfterInit()
    {
        using var fake = new JSFakeExtension();
        var provider = CreateProvider(fake);
        var host = new RecordingExtensionHost();

        var ordered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        provider.ItemsChanged += (_, _) => ordered.TrySetResult(true);

        await fake.PushNotificationAsync(
            "host/showStatus",
            new JsonObject
            {
                ["statusId"] = "startup",
                ["message"] = new JsonObject { ["message"] = "Booting", ["state"] = 0 },
            });

        // The itemsChanged handler needs no host. Seeing it means the earlier
        // showStatus has already been processed and buffered.
        await fake.PushNotificationAsync("provider/itemsChanged", new JsonObject { ["totalItems"] = 1 });
        await ordered.Task.WaitAsync(Timeout);

        // The host was never attached, so the buffered status has not been shown.
        Assert.AreEqual(0, host.ShownCount);

        provider.InitializeWithHost(host);

        await WaitForAsync(() => host.ShownCount == 1);
        Assert.AreEqual("Booting", host.ShownStatuses[0].Message);
    }

    // The initialize handshake id, displayName and icon are applied to
    // the provider instead of being ignored in favor of the package manifest.
    [TestMethod]
    public void Provider_HandshakeIdentityAndIconAreApplied()
    {
        using var fake = new JSFakeExtension();
        var metadata = ParseElement(new JsonObject
        {
            ["id"] = "handshake.id",
            ["displayName"] = "Handshake Name",
            ["icon"] = new JsonObject
            {
                ["light"] = new JsonObject { ["icon"] = "LIGHTGLYPH" },
                ["dark"] = new JsonObject { ["icon"] = "DARKGLYPH" },
            },
        });

        var provider = new JSCommandProviderProxy(fake.Connection, "test.ext", "Test Extension", null, metadata);

        Assert.AreEqual("handshake.id", provider.Id);
        Assert.AreEqual("Handshake Name", provider.DisplayName);
        Assert.AreEqual("LIGHTGLYPH", provider.Icon.Light.Icon);
        Assert.AreEqual("DARKGLYPH", provider.Icon.Dark.Icon);
    }

    // Missing handshake identity fields use the configured fallback values.
    [TestMethod]
    public void Provider_HandshakeMissingFieldsUseConfiguredFallback()
    {
        using var fake = new JSFakeExtension();
        var provider = new JSCommandProviderProxy(fake.Connection, "test.ext", "Test Extension");

        Assert.AreEqual("test.ext", provider.Id);
        Assert.AreEqual("Test Extension", provider.DisplayName);
    }

    // A pending show holds the status lock, so a hide from Dispose
    // cannot run until the show has been dispatched. The order is always show
    // then hide.
    [TestMethod]
    public async Task Provider_DisposeHidesStrictlyAfterPendingShow()
    {
        using var fake = new JSFakeExtension();
        var provider = CreateProvider(fake);
        using var host = new OrderedGatingHost();
        provider.InitializeWithHost(host);

        await fake.PushNotificationAsync(
            "host/showStatus",
            new JsonObject
            {
                ["statusId"] = "s1",
                ["message"] = new JsonObject { ["message"] = "Working", ["state"] = 0 },
            });

        Assert.IsTrue(host.ShowEntered.Wait(Timeout), "The show call should be in flight.");

        var dispose = Task.Run(() => provider.Dispose());

        // The pending show still holds the status lock, so Dispose cannot hide the
        // status yet. Give it time to prove the hide waits.
        await Task.Delay(200);
        Assert.AreEqual(0, host.HiddenCount);

        host.ReleaseShow();
        await dispose.WaitAsync(Timeout);

        CollectionAssert.AreEqual(ExpectedShowThenHide, host.Operations);
    }

    private static readonly string[] ExpectedShowThenHide = { "show", "hide" };

    // A single supplied theme variant maps to both themes so the icon
    // renders in light and dark rather than vanishing in the omitted theme.
    [TestMethod]
    public void Icon_SingleLightVariantMirrorsToBothThemes()
    {
        var icon = JSModelMapper.ParseIconInfo(ParseElement(new JsonObject
        {
            ["light"] = new JsonObject { ["icon"] = "ONLYLIGHT" },
        }));

        Assert.AreEqual("ONLYLIGHT", icon.Light.Icon);
        Assert.AreEqual("ONLYLIGHT", icon.Dark.Icon);
    }

    // A single dark variant mirrors to the light theme too.
    [TestMethod]
    public void Icon_SingleDarkVariantMirrorsToBothThemes()
    {
        var icon = JSModelMapper.ParseIconInfo(ParseElement(new JsonObject
        {
            ["dark"] = new JsonObject { ["icon"] = "ONLYDARK" },
        }));

        Assert.AreEqual("ONLYDARK", icon.Light.Icon);
        Assert.AreEqual("ONLYDARK", icon.Dark.Icon);
    }

    // When both variants are supplied they are preserved independently
    // and are not collapsed into a single shared glyph.
    [TestMethod]
    public void Icon_BothVariantsArePreservedIndependently()
    {
        var icon = JSModelMapper.ParseIconInfo(ParseElement(new JsonObject
        {
            ["light"] = new JsonObject { ["icon"] = "LIGHTICON" },
            ["dark"] = new JsonObject { ["icon"] = "DARKICON" },
        }));

        Assert.AreEqual("LIGHTICON", icon.Light.Icon);
        Assert.AreEqual("DARKICON", icon.Dark.Icon);
    }

    /// <summary>
    /// A host whose ShowStatus blocks until released and records show and hide
    /// order. This makes the show versus dispose ordering deterministic.
    /// </summary>
    private sealed partial class OrderedGatingHost : IExtensionHost, IDisposable
    {
        private readonly ManualResetEventSlim _releaseShow = new(false);
        private readonly object _lock = new();

        public ManualResetEventSlim ShowEntered { get; } = new(false);

        public List<string> Operations { get; } = new();

        public int HiddenCount
        {
            get
            {
                lock (_lock)
                {
                    return Operations.FindAll(op => op == "hide").Count;
                }
            }
        }

        public void ReleaseShow() => _releaseShow.Set();

        public IAsyncAction ShowStatus(IStatusMessage message, StatusContext context)
        {
            ShowEntered.Set();
            _releaseShow.Wait();
            lock (_lock)
            {
                Operations.Add("show");
            }

            return Task.CompletedTask.AsAsyncAction();
        }

        public IAsyncAction HideStatus(IStatusMessage message)
        {
            lock (_lock)
            {
                Operations.Add("hide");
            }

            return Task.CompletedTask.AsAsyncAction();
        }

        public IAsyncAction LogMessage(ILogMessage message) => Task.CompletedTask.AsAsyncAction();

        public void Dispose()
        {
            _releaseShow.Dispose();
            ShowEntered.Dispose();
        }
    }
}

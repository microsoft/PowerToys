// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.JsonRpc.Models;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.JsonRpc.UnitTests;

public partial class JSAdapterTests
{
    private static readonly string[] SubtitleProperty = ["Subtitle"];

    [TestMethod]
    public void PropertyChangeRegistry_PrunesDeadTargetsAndDeduplicatesLiveTargets()
    {
        using var fake = new JSFakeExtension();
        const string commandId = "shared-command";
        var deadTarget = RegisterTemporaryTarget(fake.Connection, commandId);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.IsFalse(deadTarget.TryGetTarget(out _));
        Assert.AreEqual(1, JSPropertyChangeRegistry.GetRegistrationCount(fake.Connection, commandId));

        var liveTarget = new RecordingPropertyChangeTarget();
        Parallel.For(
            0,
            64,
            _ => JSPropertyChangeRegistry.Register(fake.Connection, commandId, liveTarget));

        Assert.AreEqual(1, JSPropertyChangeRegistry.GetRegistrationCount(fake.Connection, commandId));

        JSPropertyChangeRegistry.Dispatch(
            fake.Connection,
            ParseElement(new JsonObject
            {
                ["commandId"] = commandId,
                ["properties"] = new JsonObject { ["title"] = "updated" },
            }));

        Assert.AreEqual(1, liveTarget.ApplyCount);
        JSPropertyChangeRegistry.Unregister(fake.Connection, commandId, liveTarget);
    }

    [TestMethod]
    public void WeakReferenceRegistry_PrunesDeadTargetsDuringRegistration()
    {
        var registry = new JSWeakReferenceRegistry<string, object>();
        var deadTarget = RegisterTemporaryTarget(registry, "page");
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.IsFalse(deadTarget.TryGetTarget(out _));
        Assert.AreEqual(1, registry.GetRegistrationCount("page"));

        var liveTarget = new object();
        registry.Register("page", liveTarget);

        Assert.AreEqual(1, registry.GetRegistrationCount("page"));
        Assert.AreSame(liveTarget, registry.GetLiveTargets("page")[0]);
    }

    [TestMethod]
    public void WeakReferenceRegistry_ConcurrentReplacementKeepsNewestTarget()
    {
        for (var i = 0; i < 128; i++)
        {
            var registry = new JSWeakReferenceRegistry<string, object>();
            var oldTarget = new object();
            var newTarget = new object();
            registry.Register("page", oldTarget);

            Parallel.Invoke(
                () => registry.Unregister("page", oldTarget),
                () => registry.Register("page", newTarget));

            var liveTargets = registry.GetLiveTargets("page");
            Assert.AreEqual(1, liveTargets.Count);
            Assert.AreSame(newTarget, liveTargets[0]);
        }
    }

    [TestMethod]
    public void NestedProxyGetters_CacheIdentityAndRegistration()
    {
        using var fake = new JSFakeExtension();
        using var settings = new JSCommandSettingsProxy("settings", fake.Connection);
        using var listPage = new JSListPageProxy(
            "list-page",
            fake.Connection,
            ParseElement(new JsonObject
            {
                ["id"] = "list-page",
                ["filters"] = new JsonObject { ["filters"] = new JsonArray() },
                ["emptyContent"] = CommandItem("empty-command"),
            }));
        using var contentPage = new JSContentPageProxy(
            "content-page",
            fake.Connection,
            ParseElement(new JsonObject
            {
                ["id"] = "content-page",
                ["details"] = new JsonObject { ["title"] = "Details" },
                ["commands"] = new JsonArray(ContextItem("content-command")),
            }));
        using var commandItem = new JSCommandItemAdapter(
            ParseElement(new JsonObject
            {
                ["id"] = "command-item",
                ["title"] = "Command",
                ["moreCommands"] = new JsonArray(ContextItem("command-more")),
            }),
            fake.Connection);
        using var listItem = new JSListItemAdapter(
            ParseElement(new JsonObject
            {
                ["id"] = "list-item",
                ["title"] = "List",
                ["details"] = new JsonObject { ["title"] = "Details" },
                ["moreCommands"] = new JsonArray(ContextItem("list-more")),
            }),
            fake.Connection);
        using var fallbackItem = new JSFallbackCommandItemAdapter(
            ParseElement(new JsonObject
            {
                ["id"] = "fallback-item",
                ["title"] = "Fallback",
                ["command"] = Command("fallback-command"),
                ["moreCommands"] = new JsonArray(ContextItem("fallback-more")),
            }),
            fake.Connection);
        var concurrentSettingsPages = new IContentPage[64];
        Parallel.For(0, concurrentSettingsPages.Length, i => concurrentSettingsPages[i] = settings.SettingsPage);

        foreach (var settingsPage in concurrentSettingsPages)
        {
            Assert.AreSame(settings.SettingsPage, settingsPage);
        }

        Assert.AreSame(listPage.Filters, listPage.Filters);
        Assert.AreSame(listPage.EmptyContent, listPage.EmptyContent);
        Assert.AreSame(contentPage.Details, contentPage.Details);
        Assert.AreSame(contentPage.Commands, contentPage.Commands);
        Assert.AreSame(commandItem.MoreCommands, commandItem.MoreCommands);
        Assert.AreSame(listItem.MoreCommands, listItem.MoreCommands);
        Assert.AreSame(listItem.Details, listItem.Details);
        Assert.AreSame(fallbackItem.MoreCommands, fallbackItem.MoreCommands);

        Assert.AreEqual(1, JSPropertyChangeRegistry.GetRegistrationCount(fake.Connection, "empty-command"));
        Assert.AreEqual(1, JSPropertyChangeRegistry.GetRegistrationCount(fake.Connection, "content-command"));
        Assert.AreEqual(1, JSPropertyChangeRegistry.GetRegistrationCount(fake.Connection, "command-more"));
        Assert.AreEqual(1, JSPropertyChangeRegistry.GetRegistrationCount(fake.Connection, "list-more"));
        Assert.AreEqual(1, JSPropertyChangeRegistry.GetRegistrationCount(fake.Connection, "fallback-more"));
    }

    [TestMethod]
    public void NestedProxyGetters_InvalidateOnlyTheChangedProperty()
    {
        using var fake = new JSFakeExtension();
        using var item = new JSListItemAdapter(
            ParseElement(new JsonObject
            {
                ["id"] = "list-item",
                ["title"] = "Before",
                ["details"] = new JsonObject { ["title"] = "Old details" },
                ["moreCommands"] = new JsonArray(ContextItem("old-more")),
            }),
            fake.Connection);
        var originalDetails = item.Details;
        var originalMoreCommands = item.MoreCommands;
        var originalCommand = ((ICommandContextItem)originalMoreCommands[0]).Command;

        JSPropertyChangeRegistry.Dispatch(
            fake.Connection,
            ParseElement(new JsonObject
            {
                ["commandId"] = "list-item",
                ["properties"] = new JsonObject { ["title"] = "After" },
            }));

        Assert.AreEqual("After", item.Title);
        Assert.AreSame(originalDetails, item.Details);
        Assert.AreSame(originalMoreCommands, item.MoreCommands);

        JSPropertyChangeRegistry.Dispatch(
            fake.Connection,
            ParseElement(new JsonObject
            {
                ["commandId"] = "list-item",
                ["properties"] = new JsonObject
                {
                    ["details"] = new JsonObject { ["title"] = "New details" },
                    ["moreCommands"] = new JsonArray(ContextItem("new-more")),
                },
            }));

        Assert.AreEqual("New details", item.Details?.Title);
        Assert.AreNotSame(originalDetails, item.Details);
        Assert.AreNotSame(originalMoreCommands, item.MoreCommands);
        Assert.AreSame(item.MoreCommands, item.MoreCommands);
        Assert.AreEqual(1, JSPropertyChangeRegistry.GetRegistrationCount(fake.Connection, "old-more"));
        Assert.AreEqual(1, JSPropertyChangeRegistry.GetRegistrationCount(fake.Connection, "new-more"));

        JSPropertyChangeRegistry.Dispatch(
            fake.Connection,
            ParseElement(new JsonObject
            {
                ["commandId"] = "old-more",
                ["properties"] = new JsonObject { ["name"] = "Still alive" },
            }));

        Assert.AreEqual("Still alive", originalCommand.Name);
        (originalCommand as IDisposable)?.Dispose();
    }

    [TestMethod]
    public async Task PageRegistries_RouteToNewestProxyAfterOlderProxyIsRemoved()
    {
        using var fake = new JSFakeExtension();
        using var oldListPage = new JSListPageProxy("list-page", fake.Connection);
        using var newListPage = new JSListPageProxy("list-page", fake.Connection);
        using var oldContentPage = new JSContentPageProxy("content-page", fake.Connection);
        using var newContentPage = new JSContentPageProxy("content-page", fake.Connection);
        var listChanged = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var contentChanged = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        newListPage.ItemsChanged += (_, _) => listChanged.TrySetResult();
        newContentPage.ItemsChanged += (_, _) => contentChanged.TrySetResult();

        oldListPage.Dispose();
        oldContentPage.Dispose();
        await fake.PushNotificationAsync(
            "listPage/itemsChanged",
            new JsonObject { ["pageId"] = "list-page" });
        await fake.PushNotificationAsync(
            "contentPage/itemsChanged",
            new JsonObject { ["pageId"] = "content-page" });

        await Task.WhenAll(listChanged.Task, contentChanged.Task).WaitAsync(Timeout);
    }

    [TestMethod]
    public void ListPage_RemovedItemKeepsHostHeldProxyLive()
    {
        using var fake = new JSFakeExtension();
        var requestCount = 0;
        fake.OnRequest("listPage/getItems", _ =>
        {
            if (Interlocked.Increment(ref requestCount) == 1)
            {
                return new JsonObject
                {
                    ["items"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["id"] = "row",
                            ["command"] = Command("nested-command"),
                        },
                    },
                };
            }

            return new JsonObject { ["items"] = new JsonArray() };
        });

        using var page = new JSListPageProxy("page", fake.Connection);
        var firstItems = page.GetItems();
        var command = firstItems[0].Command;

        Assert.AreEqual(2, JSPropertyChangeRegistry.GetRegistrationCount(fake.Connection, "nested-command"));

        Assert.AreEqual(0, page.GetItems().Length);
        Assert.AreEqual(2, JSPropertyChangeRegistry.GetRegistrationCount(fake.Connection, "nested-command"));

        JSPropertyChangeRegistry.Dispatch(
            fake.Connection,
            ParseElement(new JsonObject
            {
                ["commandId"] = "nested-command",
                ["properties"] = new JsonObject { ["name"] = "Still live" },
            }));

        Assert.AreEqual("Still live", command?.Name);

        (firstItems[0] as IDisposable)?.Dispose();
        Assert.AreEqual(0, JSPropertyChangeRegistry.GetRegistrationCount(fake.Connection, "nested-command"));
    }

    [TestMethod]
    public async Task ListPage_ConcurrentGetItemsRequestsShareOneRefresh()
    {
        using var fake = new JSFakeExtension();
        var requestCount = 0;
        var firstRequestStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstRequest = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fake.OnRequestAsync("listPage/getItems", async _ =>
        {
            var request = Interlocked.Increment(ref requestCount);
            firstRequestStarted.SetResult();
            await releaseFirstRequest.Task;

            return new JsonObject
            {
                ["items"] = new JsonArray
                {
                    new JsonObject { ["id"] = "row", ["title"] = $"Response {request}" },
                },
            };
        });

        using var page = new JSListPageProxy("page", fake.Connection);
        var firstGetItems = Task.Run(page.GetItems);
        await firstRequestStarted.Task.WaitAsync(Timeout);
        var secondGetItems = Task.Run(page.GetItems);

        await Task.Delay(100);
        Assert.AreEqual(1, Volatile.Read(ref requestCount));

        releaseFirstRequest.SetResult();
        var firstItems = await firstGetItems.WaitAsync(Timeout);
        var secondItems = await secondGetItems.WaitAsync(Timeout);

        Assert.AreEqual(1, requestCount);
        Assert.AreEqual("Response 1", secondItems[0].Title);
        Assert.AreSame(firstItems[0], secondItems[0]);
        Assert.AreEqual("Response 1", firstItems[0].Title);
    }

    [TestMethod]
    public async Task ListPage_ConcurrentFailureIsSharedAndNextCallRetries()
    {
        using var fake = new JSFakeExtension();
        var requestCount = 0;
        var requestStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFailure = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fake.OnErrorAsync("listPage/getItems", async _ =>
        {
            Interlocked.Increment(ref requestCount);
            requestStarted.SetResult();
            await releaseFailure.Task;
            return (-32000, "failed");
        });

        using var page = new JSListPageProxy("page", fake.Connection);
        var firstGetItems = Task.Run(page.GetItems);
        await requestStarted.Task.WaitAsync(Timeout);
        var secondGetItems = Task.Run(page.GetItems);
        await Task.Delay(100);
        Assert.AreEqual(1, Volatile.Read(ref requestCount));

        releaseFailure.SetResult();
        Assert.AreEqual(0, (await firstGetItems.WaitAsync(Timeout)).Length);
        Assert.AreEqual(0, (await secondGetItems.WaitAsync(Timeout)).Length);
        Assert.AreEqual(1, requestCount);

        fake.ClearError("listPage/getItems");
        fake.OnRequest("listPage/getItems", _ =>
        {
            Interlocked.Increment(ref requestCount);
            return new JsonObject
            {
                ["items"] = new JsonArray
                {
                    new JsonObject { ["id"] = "retry", ["title"] = "Recovered" },
                },
            };
        });

        var recovered = page.GetItems();
        Assert.AreEqual(2, requestCount);
        Assert.AreEqual("Recovered", recovered[0].Title);
    }

    [TestMethod]
    public void ListItem_UpdateDataRebindsEffectiveNotificationId()
    {
        using var fake = new JSFakeExtension();
        using var item = new JSListItemAdapter(
            ParseElement(new JsonObject
            {
                ["id"] = "stable-item",
                ["title"] = "Before",
                ["command"] = Command("old-command"),
            }),
            fake.Connection);

        Assert.AreEqual(1, JSPropertyChangeRegistry.GetRegistrationCount(fake.Connection, "old-command"));
        item.UpdateData(
            ParseElement(new JsonObject
            {
                ["id"] = "stable-item",
                ["title"] = "Reused",
                ["command"] = Command("new-command"),
            }));

        Assert.AreEqual(0, JSPropertyChangeRegistry.GetRegistrationCount(fake.Connection, "old-command"));
        Assert.AreEqual(1, JSPropertyChangeRegistry.GetRegistrationCount(fake.Connection, "new-command"));

        JSPropertyChangeRegistry.Dispatch(
            fake.Connection,
            ParseElement(new JsonObject
            {
                ["commandId"] = "old-command",
                ["properties"] = new JsonObject { ["title"] = "Stale" },
            }));
        item.ApplyPropertyChanges(
            "old-command",
            ParseElement(new JsonObject { ["title"] = "Captured stale dispatch" }));
        Assert.AreEqual("Reused", item.Title);

        JSPropertyChangeRegistry.Dispatch(
            fake.Connection,
            ParseElement(new JsonObject
            {
                ["commandId"] = "new-command",
                ["properties"] = new JsonObject { ["title"] = "Current" },
            }));
        Assert.AreEqual("Current", item.Title);
    }

    [TestMethod]
    public void ListItem_UpdateDataRaisesOnlyChangedPropertiesAndInvalidatesMatchingCaches()
    {
        using var fake = new JSFakeExtension();
        using var item = new JSListItemAdapter(
            ParseElement(new JsonObject
            {
                ["id"] = "stable-item",
                ["title"] = "Title",
                ["subtitle"] = "Before",
                ["command"] = Command("command"),
                ["details"] = new JsonObject { ["title"] = "Before details" },
                ["moreCommands"] = new JsonArray(ContextItem("before-more")),
            }),
            fake.Connection);
        var originalCommand = item.Command;
        var originalDetails = item.Details;
        var originalMoreCommands = item.MoreCommands;
        var changedProperties = new List<string>();
        item.PropChanged += (_, args) => changedProperties.Add(args.PropertyName);

        item.UpdateData(
            ParseElement(new JsonObject
            {
                ["id"] = "stable-item",
                ["title"] = "Title",
                ["subtitle"] = "After",
                ["command"] = Command("command"),
                ["details"] = new JsonObject { ["title"] = "Before details" },
                ["moreCommands"] = new JsonArray(ContextItem("before-more")),
            }));

        Assert.HasCount(1, changedProperties);
        Assert.AreEqual("Subtitle", changedProperties[0]);
        Assert.AreSame(originalCommand, item.Command);
        Assert.AreSame(originalDetails, item.Details);
        Assert.AreSame(originalMoreCommands, item.MoreCommands);

        changedProperties.Clear();
        item.UpdateData(
            ParseElement(new JsonObject
            {
                ["id"] = "stable-item",
                ["title"] = "Title",
                ["subtitle"] = "After",
                ["command"] = Command("command"),
                ["details"] = new JsonObject { ["title"] = "After details" },
                ["moreCommands"] = new JsonArray(ContextItem("after-more")),
            }));

        Assert.HasCount(2, changedProperties);
        Assert.IsTrue(changedProperties.Contains("Details"));
        Assert.IsTrue(changedProperties.Contains("MoreCommands"));
        Assert.AreSame(originalCommand, item.Command);
        Assert.AreNotSame(originalDetails, item.Details);
        Assert.AreNotSame(originalMoreCommands, item.MoreCommands);
    }

    [TestMethod]
    public void ReplaceData_IdAndUnknownFieldChangesUpdateBackingDataWithoutNotifications()
    {
        using var fake = new JSFakeExtension();
        using var proxy = new RecordingObservableProxy(
            fake.Connection,
            ParseElement(new JsonObject
            {
                ["id"] = "before",
                ["title"] = "Title",
                ["subtitle"] = "Subtitle",
                ["unknown"] = "before",
            }));
        var changedProperties = new List<string>();
        proxy.PropChanged += (_, args) => changedProperties.Add(args.PropertyName);

        proxy.Update(
            ParseElement(new JsonObject
            {
                ["id"] = "after",
                ["title"] = "Title",
                ["subtitle"] = "Subtitle",
                ["unknown"] = "after",
            }));

        Assert.AreEqual("after", proxy.CurrentData.GetProperty("id").GetString());
        Assert.AreEqual("after", proxy.CurrentData.GetProperty("unknown").GetString());
        Assert.IsEmpty(changedProperties);
    }

    [TestMethod]
    public void ReplaceData_UnchangedVisiblePropertiesStillStoresLatestPayload()
    {
        using var fake = new JSFakeExtension();
        using var proxy = new RecordingObservableProxy(
            fake.Connection,
            ParseElement(new JsonObject
            {
                ["title"] = "Title",
                ["subtitle"] = "Subtitle",
                ["metadata"] = new JsonObject { ["version"] = 1 },
            }));
        var changedProperties = new List<string>();
        proxy.PropChanged += (_, args) => changedProperties.Add(args.PropertyName);

        proxy.Update(
            ParseElement(new JsonObject
            {
                ["title"] = "Title",
                ["subtitle"] = "Subtitle",
                ["metadata"] = new JsonObject { ["version"] = 2 },
            }));

        Assert.AreEqual(2, proxy.CurrentData.GetProperty("metadata").GetProperty("version").GetInt32());
        Assert.IsEmpty(changedProperties);
    }

    [TestMethod]
    public void ReplaceData_SubtitleChangeUpdatesBackingDataAndRaisesOnlySubtitle()
    {
        using var fake = new JSFakeExtension();
        using var proxy = new RecordingObservableProxy(
            fake.Connection,
            ParseElement(new JsonObject
            {
                ["title"] = "Title",
                ["subtitle"] = "Before",
                ["unknown"] = "before",
            }));
        var changedProperties = new List<string>();
        proxy.PropChanged += (_, args) => changedProperties.Add(args.PropertyName);

        proxy.Update(
            ParseElement(new JsonObject
            {
                ["title"] = "Title",
                ["subtitle"] = "After",
                ["unknown"] = "after",
            }));

        Assert.AreEqual("After", proxy.CurrentData.GetProperty("subtitle").GetString());
        Assert.AreEqual("after", proxy.CurrentData.GetProperty("unknown").GetString());
        CollectionAssert.AreEqual(SubtitleProperty, changedProperties);
    }

    [TestMethod]
    public void ProviderSettings_ConcurrentReadsReturnOneProxy()
    {
        using var fake = new JSFakeExtension();
        var requestCount = 0;
        fake.OnRequest("provider/getSettings", _ =>
        {
            Interlocked.Increment(ref requestCount);
            return new JsonObject { ["id"] = "settings-page" };
        });
        using var provider = CreateProvider(fake);
        var settings = new ICommandSettings?[64];

        Parallel.For(0, settings.Length, i => settings[i] = provider.Settings);

        Assert.AreEqual(1, requestCount);
        foreach (var current in settings)
        {
            Assert.AreSame(settings[0], current);
        }
    }

    [TestMethod]
    public async Task ProviderSettings_DisposeDuringRequestDoesNotPublishProxy()
    {
        using var fake = new JSFakeExtension();
        using var requestStarted = new ManualResetEventSlim();
        using var releaseRequest = new ManualResetEventSlim();
        fake.OnRequest("provider/getSettings", _ =>
        {
            requestStarted.Set();
            releaseRequest.Wait(Timeout);
            return new JsonObject { ["id"] = "settings-page" };
        });
        using var provider = CreateProvider(fake);

        var settings = Task.Run(() => provider.Settings);
        Assert.IsTrue(requestStarted.Wait(Timeout));
        provider.Dispose();
        releaseRequest.Set();

        Assert.IsNull(await settings.WaitAsync(Timeout));
    }

    // LoadMore folds the loaded page into pagination state and raises
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

    // A loadMore response with no hasMoreItems flag is the final page,
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

    // Concurrent proxies on one connection share the retained registry.
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

    // A context item with no icon of its own inherits the command's
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

    // A context item that carries its own icon keeps it and does not
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

    // Status notifications racing Dispose never enumerate the status map
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
                    ["message"] = new JsonObject { ["message"] = $"m{i}", ["state"] = 0 },
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
                        ["message"] = new JsonObject { ["message"] = $"m{i}", ["state"] = 0 },
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

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<RecordingPropertyChangeTarget> RegisterTemporaryTarget(
        JsonRpcConnection connection,
        string commandId)
    {
        var target = new RecordingPropertyChangeTarget();
        JSPropertyChangeRegistry.Register(connection, commandId, target);
        return new WeakReference<RecordingPropertyChangeTarget>(target);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<object> RegisterTemporaryTarget(
        JSWeakReferenceRegistry<string, object> registry,
        string key)
    {
        var target = new object();
        registry.Register(key, target);
        return new WeakReference<object>(target);
    }

    private static JsonObject Command(string id) => new()
    {
        ["id"] = id,
        ["name"] = id,
    };

    private static JsonObject CommandItem(string id) => new()
    {
        ["id"] = id,
        ["title"] = id,
        ["command"] = Command(id),
    };

    private static JsonObject ContextItem(string id) => new()
    {
        ["title"] = id,
        ["command"] = Command(id),
    };

    private sealed class RecordingPropertyChangeTarget : IJSPropertyChangeTarget
    {
        public int ApplyCount { get; private set; }

        public void ApplyPropertyChanges(string notificationId, JsonElement properties)
        {
            ApplyCount++;
        }
    }

    private sealed class RecordingObservableProxy : JSObservableProxyBase
    {
        internal RecordingObservableProxy(JsonRpcConnection connection, JsonElement data)
            : base("recording", connection, data)
        {
        }

        internal JsonElement CurrentData => Data;

        internal void Update(JsonElement data)
        {
            ReplaceData(data, ["title", "subtitle"]);
        }

        protected override bool SupportsProperty(string propertyName)
        {
            return propertyName is "title" or "subtitle";
        }
    }
}

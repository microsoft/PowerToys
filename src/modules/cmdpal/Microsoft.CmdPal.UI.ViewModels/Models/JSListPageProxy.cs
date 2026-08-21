// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.CmdPal.UI.ViewModels.Services.JsonRpc;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels.Models;

/// <summary>
/// Exposes a Node.js extension list page as <see cref="IListPage"/>.
/// Items come from <c>listPage/getItems</c>. The extension can send
/// <c>listPage/itemsChanged</c> to refresh the view.
/// </summary>
internal sealed partial class JSListPageProxy : JSObservableProxyBase, IListPage
{
    // Routing is scoped per connection so identical page ids from different
    // extensions do not collide. A page id can have more than one live proxy, so
    // notifications go to every visible reference instead of only the newest one.
    // Weak references let old proxies be collected, then pruned on dispatch and dispose.
    private static readonly ConditionalWeakTable<JsonRpcConnection, PageRegistry> Registries = new();

    private readonly string _pageId;
    private readonly PageRegistry _registry;
    private readonly object _stateLock = new();
    private bool? _hasMoreItemsState;
    private bool _disposed;

    public JSListPageProxy(string pageId, JsonRpcConnection connection, JsonElement pageData = default)
        : base(pageId, connection, pageData)
    {
        _pageId = pageId ?? throw new ArgumentNullException(nameof(pageId));

        // Get the retained registry before subscribing. ConditionalWeakTable may run
        // the factory on a thread that loses the race and discards its result. If the
        // factory subscribed, the connection could keep a handler for a discarded
        // registry while proxies register with the retained one.
        _registry = Registries.GetValue(Connection, static _ => new PageRegistry());
        _registry.EnsureSubscribed(Connection);

        var list = _registry.Pages.GetOrAdd(_pageId, static _ => new List<WeakReference<JSListPageProxy>>());
        lock (list)
        {
            list.Add(new WeakReference<JSListPageProxy>(this));
        }
    }

    public event TypedEventHandler<object, IItemsChangedEventArgs>? ItemsChanged;

    public string Id => _pageId;

    public string Name => JSModelMapper.GetString(Data, "name") ?? string.Empty;

    public IIconInfo Icon => JSModelMapper.GetIcon(Data, "icon", "Icon");

    public string Title => JSModelMapper.GetString(Data, "title") ?? Name;

    public bool IsLoading => JSModelMapper.GetBool(Data, "isLoading", false);

    public OptionalColor AccentColor => JSModelMapper.ParseColor(Data, "accentColor", "AccentColor");

    public string SearchText => JSModelMapper.GetString(Data, "searchText") ?? string.Empty;

    public string PlaceholderText => JSModelMapper.GetString(Data, "placeholderText") ?? string.Empty;

    public bool ShowDetails => JSModelMapper.GetBool(Data, "showDetails", false);

    public IFilters? Filters
    {
        get
        {
            if (JSModelMapper.TryGetAnyCase(Data, "filters", "Filters", out var filtersProp) &&
                filtersProp.ValueKind == JsonValueKind.Object)
            {
                return new JSFiltersAdapter(filtersProp, Connection, _pageId);
            }

            return null;
        }
    }

    public IGridProperties? GridProperties => JSModelMapper.ParseGridProperties(Data);

    // Pagination state can change after construction. The extension reports
    // whether more pages remain through getItems, loadMore, and itemsChanged.
    // The seeded page metadata is only the starting value.
    public bool HasMoreItems
    {
        get
        {
            lock (_stateLock)
            {
                return _hasMoreItemsState ?? JSModelMapper.GetBool(Data, "hasMoreItems", false);
            }
        }
    }

    public ICommandItem? EmptyContent
    {
        get
        {
            if (JSModelMapper.TryGetAnyCase(Data, "emptyContent", "EmptyContent", out var emptyProp) &&
                emptyProp.ValueKind == JsonValueKind.Object)
            {
                return new JSCommandItemAdapter(emptyProp, Connection);
            }

            return null;
        }
    }

    public IListItem[] GetItems()
    {
        try
        {
            var response = Connection.SendRequestAsync(
                "listPage/getItems",
                new JsonObject { ["pageId"] = _pageId },
                CancellationToken.None).GetAwaiter().GetResult();

            if (response.Error != null)
            {
                Logger.LogError($"GetItems error for page {_pageId}: {response.Error.Message}");
                return [];
            }

            UpdatePageState(response.Result);
            return ParseListItems(response.Result);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to get items for page {_pageId}: {ex.Message}");
            return [];
        }
    }

    public void LoadMore()
    {
        lock (_stateLock)
        {
            // The extension already reported the final page, so do not ask again.
            if (_hasMoreItemsState == false)
            {
                return;
            }
        }

        try
        {
            var response = Connection.SendRequestAsync(
                "listPage/loadMore",
                new JsonObject { ["pageId"] = _pageId },
                CancellationToken.None).GetAwaiter().GetResult();

            if (response.Error != null)
            {
                Logger.LogWarning($"LoadMore error for page {_pageId}: {response.Error.Message}");
                SettleLoadMoreFailure();
                return;
            }

            UpdatePageState(response.Result);

            // If loadMore does not report more pages, treat it as the final page
            // instead of keeping an old true value.
            SettleHasMoreItemsAfterLoadMore(response.Result);

            // The host waits for ItemsChanged after LoadMore before it asks for
            // items again and clears its loading state.
            RaiseItemsChanged(ReadTotalItems(response.Result));
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Failed to load more items for page {_pageId}: {ex.Message}");
            SettleLoadMoreFailure();
        }
    }

    // A failed loadMore must not leave the host stuck loading. Stop paging and
    // raise ItemsChanged so the host can clear its spinner and keep the items it
    // already has. The total is unknown because the failed page delivered no count.
    private void SettleLoadMoreFailure()
    {
        var changed = false;
        lock (_stateLock)
        {
            if (_hasMoreItemsState != false)
            {
                _hasMoreItemsState = false;
                changed = true;
            }
        }

        if (changed)
        {
            OnPropertyChanged(nameof(HasMoreItems));
        }

        RaiseItemsChanged(-1);
    }

    // Applies mutable page state from getItems or loadMore and raises a change
    // notification when HasMoreItems changes.
    private void UpdatePageState(JsonElement? envelope)
    {
        if (!envelope.HasValue || envelope.Value.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (!JSModelMapper.TryGetAnyCase(envelope.Value, "hasMoreItems", "HasMoreItems", out var hasMoreProp))
        {
            return;
        }

        bool? parsed = hasMoreProp.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null,
        };

        if (parsed is null)
        {
            return;
        }

        var changed = false;
        lock (_stateLock)
        {
            if (_hasMoreItemsState != parsed)
            {
                _hasMoreItemsState = parsed;
                changed = true;
            }
        }

        if (changed)
        {
            OnPropertyChanged(nameof(HasMoreItems));
        }
    }

    // After loadMore, an explicit hasMoreItems true keeps paging alive. False or a
    // missing flag means the extension delivered its final page. itemsChanged keeps
    // the old value when the flag is missing, since it can be only a refresh.
    private void SettleHasMoreItemsAfterLoadMore(JsonElement? envelope)
    {
        var hasMore = false;
        if (envelope.HasValue &&
            envelope.Value.ValueKind == JsonValueKind.Object &&
            JSModelMapper.TryGetAnyCase(envelope.Value, "hasMoreItems", "HasMoreItems", out var hasMoreProp))
        {
            hasMore = hasMoreProp.ValueKind == JsonValueKind.True;
        }

        var changed = false;
        lock (_stateLock)
        {
            if (_hasMoreItemsState != hasMore)
            {
                _hasMoreItemsState = hasMore;
                changed = true;
            }
        }

        if (changed)
        {
            OnPropertyChanged(nameof(HasMoreItems));
        }
    }

    private static int ReadTotalItems(JsonElement? envelope)
    {
        if (envelope.HasValue &&
            envelope.Value.ValueKind == JsonValueKind.Object &&
            envelope.Value.TryGetProperty("totalItems", out var totalItemsProp) &&
            totalItemsProp.ValueKind == JsonValueKind.Number)
        {
            return totalItemsProp.GetInt32();
        }

        return -1;
    }

    private void RaiseItemsChanged(int totalItems)
    {
        ItemsChanged?.Invoke(this, new ItemsChangedEventArgs(totalItems));
    }

    public override void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        base.Dispose();

        if (_registry.Pages.TryGetValue(_pageId, out var list))
        {
            lock (list)
            {
                list.RemoveAll(weak => !weak.TryGetTarget(out var target) || ReferenceEquals(target, this));
                if (list.Count == 0)
                {
                    _registry.Pages.TryRemove(_pageId, out _);
                }
            }
        }
    }

    private static void DispatchItemsChanged(PageRegistry registry, JsonElement paramsElement)
    {
        try
        {
            if (paramsElement.ValueKind != JsonValueKind.Object ||
                !paramsElement.TryGetProperty("pageId", out var pageProp))
            {
                return;
            }

            var pageId = pageProp.GetString();
            if (pageId == null || !registry.Pages.TryGetValue(pageId, out var proxyRefs))
            {
                return;
            }

            var totalItems = -1;
            if (paramsElement.TryGetProperty("totalItems", out var totalItemsProp) &&
                totalItemsProp.ValueKind == JsonValueKind.Number)
            {
                totalItems = totalItemsProp.GetInt32();
            }

            // Snapshot live proxies and prune collected ones so the registry does
            // not grow as pages come and go.
            List<JSListPageProxy> targets = new();
            lock (proxyRefs)
            {
                proxyRefs.RemoveAll(weak => !weak.TryGetTarget(out _));
                foreach (var weak in proxyRefs)
                {
                    if (weak.TryGetTarget(out var proxy))
                    {
                        targets.Add(proxy);
                    }
                }

                if (proxyRefs.Count == 0)
                {
                    registry.Pages.TryRemove(pageId, out _);
                }
            }

            foreach (var proxy in targets)
            {
                proxy.UpdatePageState(paramsElement);

                var args = new ItemsChangedEventArgs(totalItems);
                var handler = proxy.ItemsChanged;
                if (handler != null)
                {
                    _ = Task.Run(() => handler.Invoke(proxy, args));
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Error handling listPage/itemsChanged notification: {ex.Message}");
        }
    }

    protected override bool SupportsProperty(string propertyName) => propertyName switch
    {
        "id" or "name" or "icon" or "title" or "isLoading" or "accentColor" or
        "searchText" or "placeholderText" or "showDetails" or "filters" or
        "gridProperties" or "hasMoreItems" or "emptyContent" => true,
        _ => false,
    };

    protected override void OnPropertyChangesApplied(IReadOnlyList<string> propertyNames)
    {
        if (!propertyNames.Contains("hasMoreItems"))
        {
            return;
        }

        var value = JSModelMapper.GetBool(Data, "hasMoreItems", false);
        lock (_stateLock)
        {
            _hasMoreItemsState = value;
        }
    }

    private IListItem[] ParseListItems(JsonElement? result)
    {
        if (!result.HasValue)
        {
            return [];
        }

        var arrayElement = result.Value;
        if (result.Value.ValueKind == JsonValueKind.Object &&
            result.Value.TryGetProperty("items", out var itemsProp))
        {
            arrayElement = itemsProp;
        }

        if (arrayElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var items = new List<IListItem>();
        foreach (var element in arrayElement.EnumerateArray())
        {
            if (element.ValueKind == JsonValueKind.Object &&
                JSModelMapper.GetBool(element, "_isSeparator", false))
            {
                items.Add(new Separator(JSModelMapper.GetString(element, "title") ?? string.Empty));
            }
            else
            {
                items.Add(new JSListItemAdapter(element, Connection));
            }
        }

        return items.ToArray();
    }

    private sealed class PageRegistry
    {
        private readonly object _subscribeLock = new();
        private bool _subscribed;

        public ConcurrentDictionary<string, List<WeakReference<JSListPageProxy>>> Pages { get; } = new();

        // Binds the itemsChanged handler to the retained registry once. Binding here,
        // instead of inside the ConditionalWeakTable factory, keeps the handler from
        // pointing at a registry that lost the creation race.
        public void EnsureSubscribed(JsonRpcConnection connection)
        {
            lock (_subscribeLock)
            {
                if (_subscribed)
                {
                    return;
                }

                // Register the handler before marking the registry subscribed. No caller
                // should see the registry as subscribed and add a proxy until the
                // connection can route itemsChanged here. Holding the lock across both
                // steps closes the drop window.
                connection.RegisterNotificationHandler(
                    "listPage/itemsChanged",
                    paramsElement => DispatchItemsChanged(this, paramsElement));

                _subscribed = true;
            }
        }
    }
}

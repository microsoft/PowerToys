// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
/// Proxy that presents a Node.js extension list page as <see cref="IListPage"/>.
/// Items are fetched with <c>listPage/getItems</c> and the extension can push
/// <c>listPage/itemsChanged</c> notifications to refresh the view.
/// </summary>
internal sealed partial class JSListPageProxy : BaseObservable, IListPage, IDisposable
{
    // Routing is scoped per connection so that identical page ids from different
    // extensions never collide. Each page id maps to the set of live proxies that
    // share it, so a notification reaches every visible reference (the same page
    // can be materialized more than once) instead of only the most recent proxy.
    // Proxies are held weakly so they can be collected without the registry
    // keeping them alive, and dead references are pruned on dispatch and dispose.
    private static readonly ConditionalWeakTable<JsonRpcConnection, PageRegistry> Registries = new();

    private readonly string _pageId;
    private readonly JsonRpcConnection _connection;
    private readonly JsonElement _pageData;
    private readonly PageRegistry _registry;
    private readonly object _stateLock = new();
    private bool? _hasMoreItemsState;
    private bool _disposed;

    public JSListPageProxy(string pageId, JsonRpcConnection connection, JsonElement pageData = default)
    {
        _pageId = pageId ?? throw new ArgumentNullException(nameof(pageId));
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _pageData = pageData;

        _registry = Registries.GetValue(_connection, static conn =>
        {
            var created = new PageRegistry();
            conn.RegisterNotificationHandler("listPage/itemsChanged", paramsElement => DispatchItemsChanged(created, paramsElement));
            return created;
        });

        var list = _registry.Pages.GetOrAdd(_pageId, static _ => new List<WeakReference<JSListPageProxy>>());
        lock (list)
        {
            list.Add(new WeakReference<JSListPageProxy>(this));
        }
    }

    public event TypedEventHandler<object, IItemsChangedEventArgs>? ItemsChanged;

    public string Id => _pageId;

    public string Name => JSModelMapper.GetString(_pageData, "name") ?? string.Empty;

    public IIconInfo Icon => JSModelMapper.GetIcon(_pageData, "icon", "Icon");

    public string Title => JSModelMapper.GetString(_pageData, "title") ?? Name;

    public bool IsLoading => JSModelMapper.GetBool(_pageData, "isLoading", false);

    public OptionalColor AccentColor => JSModelMapper.ParseColor(_pageData, "accentColor", "AccentColor");

    public string SearchText => JSModelMapper.GetString(_pageData, "searchText") ?? string.Empty;

    public string PlaceholderText => JSModelMapper.GetString(_pageData, "placeholderText") ?? string.Empty;

    public bool ShowDetails => JSModelMapper.GetBool(_pageData, "showDetails", false);

    public IFilters? Filters
    {
        get
        {
            if (JSModelMapper.TryGetAnyCase(_pageData, "filters", "Filters", out var filtersProp) &&
                filtersProp.ValueKind == JsonValueKind.Object)
            {
                return new JSFiltersAdapter(filtersProp, _connection, _pageId);
            }

            return null;
        }
    }

    public IGridProperties? GridProperties => JSModelMapper.ParseGridProperties(_pageData);

    // Pagination state is mutable: the extension reports whether more pages
    // remain via the getItems / loadMore responses and itemsChanged
    // notifications. The seeded page metadata is only the initial value; once the
    // extension reports the final page we stop and never issue another loadMore.
    public bool HasMoreItems
    {
        get
        {
            lock (_stateLock)
            {
                return _hasMoreItemsState ?? JSModelMapper.GetBool(_pageData, "hasMoreItems", false);
            }
        }
    }

    public ICommandItem? EmptyContent
    {
        get
        {
            if (JSModelMapper.TryGetAnyCase(_pageData, "emptyContent", "EmptyContent", out var emptyProp) &&
                emptyProp.ValueKind == JsonValueKind.Object)
            {
                return new JSCommandItemAdapter(emptyProp, _connection);
            }

            return null;
        }
    }

    public IListItem[] GetItems()
    {
        try
        {
            var response = _connection.SendRequestAsync(
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
            // The extension has already reported the final page; do not ask again.
            if (_hasMoreItemsState == false)
            {
                return;
            }
        }

        try
        {
            var response = _connection.SendRequestAsync(
                "listPage/loadMore",
                new JsonObject { ["pageId"] = _pageId },
                CancellationToken.None).GetAwaiter().GetResult();

            if (response.Error != null)
            {
                Logger.LogWarning($"LoadMore error for page {_pageId}: {response.Error.Message}");
                return;
            }

            UpdatePageState(response.Result);
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Failed to load more items for page {_pageId}: {ex.Message}");
        }
    }

    // Reads the mutable page state (currently HasMoreItems) from a getItems /
    // loadMore response envelope and raises a change notification when it moves.
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

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

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

            // Snapshot the live proxies and prune any that were collected so the
            // registry does not grow without bound as pages come and go.
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
                items.Add(new JSListItemAdapter(element, _connection));
            }
        }

        return items.ToArray();
    }

    private sealed class PageRegistry
    {
        public ConcurrentDictionary<string, List<WeakReference<JSListPageProxy>>> Pages { get; } = new();
    }
}

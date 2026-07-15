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
internal sealed partial class JSListPageProxy : BaseObservable, IListPage
{
    // Routing is scoped per connection so that identical page ids from different
    // extensions never collide. Proxies are held weakly so they can be collected
    // without the registry keeping them alive.
    private static readonly ConditionalWeakTable<JsonRpcConnection, PageRegistry> Registries = new();

    private readonly string _pageId;
    private readonly JsonRpcConnection _connection;
    private readonly JsonElement _pageData;

    public JSListPageProxy(string pageId, JsonRpcConnection connection, JsonElement pageData = default)
    {
        _pageId = pageId ?? throw new ArgumentNullException(nameof(pageId));
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _pageData = pageData;

        var registry = Registries.GetValue(_connection, static conn =>
        {
            var created = new PageRegistry();
            conn.RegisterNotificationHandler("listPage/itemsChanged", paramsElement => DispatchItemsChanged(created, paramsElement));
            return created;
        });

        registry.Pages[_pageId] = new WeakReference<JSListPageProxy>(this);
    }

    public event TypedEventHandler<object, IItemsChangedEventArgs>? ItemsChanged;

    public string Id => _pageId;

    public string Name => JSModelMapper.GetString(_pageData, "name") ?? string.Empty;

    public IIconInfo Icon => JSModelMapper.GetIcon(_pageData, "icon", "Icon");

    public string Title => JSModelMapper.GetString(_pageData, "title") ?? Name;

    public bool IsLoading => JSModelMapper.GetBool(_pageData, "isLoading", false);

    public OptionalColor AccentColor => ColorHelpers.NoColor();

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

    public bool HasMoreItems => JSModelMapper.GetBool(_pageData, "hasMoreItems", false);

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
        try
        {
            _connection.SendRequestAsync(
                "listPage/loadMore",
                new JsonObject { ["pageId"] = _pageId },
                CancellationToken.None).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Failed to load more items for page {_pageId}: {ex.Message}");
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
            if (pageId == null || !registry.Pages.TryGetValue(pageId, out var weakProxy))
            {
                return;
            }

            if (!weakProxy.TryGetTarget(out var proxy))
            {
                // The proxy has been collected; drop the stale entry.
                registry.Pages.TryRemove(pageId, out _);
                return;
            }

            var totalItems = -1;
            if (paramsElement.TryGetProperty("totalItems", out var totalItemsProp) &&
                totalItemsProp.ValueKind == JsonValueKind.Number)
            {
                totalItems = totalItemsProp.GetInt32();
            }

            var args = new ItemsChangedEventArgs(totalItems);
            var handler = proxy.ItemsChanged;
            if (handler != null)
            {
                _ = Task.Run(() => handler.Invoke(proxy, args));
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
        public ConcurrentDictionary<string, WeakReference<JSListPageProxy>> Pages { get; } = new();
    }
}

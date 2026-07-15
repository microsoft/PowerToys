// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using ManagedCommon;
using Microsoft.CmdPal.UI.ViewModels.Services.JsonRpc;
using Microsoft.CommandPalette.Extensions;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels.Models;

/// <summary>
/// Proxy that presents a Node.js extension page as <see cref="IDynamicListPage"/>.
/// Setting the search text forwards a <c>listPage/setSearchText</c> request so the
/// extension can perform its own filtering.
/// </summary>
internal sealed partial class JSDynamicListPageProxy : IDynamicListPage
{
    private readonly JSListPageProxy _inner;
    private readonly string _pageId;
    private readonly JsonRpcConnection _connection;

    public JSDynamicListPageProxy(string pageId, JsonRpcConnection connection, JsonElement pageData = default)
    {
        _pageId = pageId ?? throw new ArgumentNullException(nameof(pageId));
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _inner = new JSListPageProxy(pageId, connection, pageData);
    }

    public event TypedEventHandler<object, IItemsChangedEventArgs>? ItemsChanged
    {
        add => _inner.ItemsChanged += value;
        remove => _inner.ItemsChanged -= value;
    }

    public event TypedEventHandler<object, IPropChangedEventArgs>? PropChanged
    {
        add => _inner.PropChanged += value;
        remove => _inner.PropChanged -= value;
    }

    public string Id => _inner.Id;

    public string Name => _inner.Name;

    public IIconInfo Icon => _inner.Icon;

    public string Title => _inner.Title;

    public bool IsLoading => _inner.IsLoading;

    public OptionalColor AccentColor => _inner.AccentColor;

    public string PlaceholderText => _inner.PlaceholderText;

    public bool ShowDetails => _inner.ShowDetails;

    public IFilters? Filters => _inner.Filters;

    public IGridProperties? GridProperties => _inner.GridProperties;

    public bool HasMoreItems => _inner.HasMoreItems;

    public ICommandItem? EmptyContent => _inner.EmptyContent;

    public string SearchText
    {
        get => _inner.SearchText;

        set
        {
            try
            {
                _connection.SendRequestAsync(
                    "listPage/setSearchText",
                    new JsonObject { ["pageId"] = _pageId, ["searchText"] = value },
                    CancellationToken.None).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Failed to set search text for page {_pageId}: {ex.Message}");
            }
        }
    }

    public IListItem[] GetItems() => _inner.GetItems();

    public void LoadMore() => _inner.LoadMore();
}

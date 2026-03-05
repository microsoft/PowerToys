// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using ManagedCommon;
using Microsoft.CmdPal.UI.ViewModels.Services.JsonRpc;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels.Models;

/// <summary>
/// Proxy that implements IListPage by forwarding calls to a Node.js extension via JSON-RPC.
/// </summary>
internal sealed class JSListPageProxy : IListPage
{
    // Shared registry: routes "listPage/itemsChanged" notifications to the
    // correct proxy by pageId.  Every proxy registers itself here; the single
    // notification handler dispatches by looking up the pageId.
    private static readonly ConcurrentDictionary<string, JSListPageProxy> _proxyRegistry = new();

    private readonly string _pageId;
    private readonly JsonRpcConnection _connection;
    private readonly Lock _eventLock = new();
    private readonly string _name = string.Empty;
    private readonly string _title = string.Empty;
    private readonly string _placeholderText = string.Empty;
    private readonly bool _showDetails;
    private readonly IGridProperties? _gridProperties;
    private readonly JsonElement _filtersData;

#pragma warning disable SA1300 // Element should begin with upper-case letter - matches existing pattern in JSCommandProviderProxy
    private event TypedEventHandler<object, IItemsChangedEventArgs>? _itemsChanged;
#pragma warning restore SA1300

    public JSListPageProxy(string pageId, JsonRpcConnection connection, JsonElement pageData = default)
    {
        _pageId = pageId ?? throw new ArgumentNullException(nameof(pageId));
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));

        // Read initial page properties from JSON data when available
        if (pageData.ValueKind == JsonValueKind.Object)
        {
#pragma warning disable CA1507 // Use nameof - these are JSON property names, not C# member names
            _name = pageData.TryGetProperty("name", out var nameProp) && nameProp.ValueKind == JsonValueKind.String
                ? nameProp.GetString() ?? string.Empty : string.Empty;
            _title = _name;
            _placeholderText = pageData.TryGetProperty("placeholderText", out var phProp) && phProp.ValueKind == JsonValueKind.String
                ? phProp.GetString() ?? string.Empty : string.Empty;
            _showDetails = pageData.TryGetProperty("showDetails", out var sdProp) && sdProp.ValueKind == JsonValueKind.True;
            if (pageData.TryGetProperty("gridProperties", out var gridProp) && gridProp.ValueKind == JsonValueKind.Object)
            {
                _gridProperties = JSGridPropertiesFactory.FromJson(gridProp);
            }

            if (pageData.TryGetProperty("filters", out var filtersProp) && filtersProp.ValueKind == JsonValueKind.Object)
            {
                _filtersData = filtersProp;
            }
#pragma warning restore CA1507
        }

        // Register this proxy in the shared registry and install the shared
        // notification dispatcher.  The dictionary-based handler in
        // JsonRpcConnection only keeps one handler per method name, so each
        // call here overwrites the previous — but that's fine because
        // DispatchItemsChanged looks up the correct proxy by pageId.
        _proxyRegistry[_pageId] = this;
        _connection.RegisterNotificationHandler("listPage/itemsChanged", DispatchItemsChanged);
    }

    // ICommand members
    public string Name => _name;

    public string Id => _pageId;

    public IIconInfo Icon => new IconInfo(string.Empty);

    // IPage members
    public string Title => _title;

    public bool IsLoading => false;

    public OptionalColor AccentColor => ColorHelpers.NoColor();

    // IListPage members
    public string SearchText => string.Empty;

    public string PlaceholderText => _placeholderText;

    public bool ShowDetails => _showDetails;

    public IFilters Filters => new JSFiltersAdapter(_filtersData, _connection, _pageId);

    public IGridProperties? GridProperties => _gridProperties;

    public bool HasMoreItems => false;

    public ICommandItem? EmptyContent => null;

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

    // INotifyItemsChanged
    public event TypedEventHandler<object, IItemsChangedEventArgs>? ItemsChanged
    {
        add
        {
            lock (_eventLock)
            {
                _itemsChanged += value;
            }
        }

        remove
        {
            lock (_eventLock)
            {
                _itemsChanged -= value;
            }
        }
    }

    // INotifyPropChanged
    public event TypedEventHandler<object, IPropChangedEventArgs>? PropChanged
    {
        add { }
        remove { }
    }

    private static void DispatchItemsChanged(JsonElement paramsElement)
    {
        try
        {
            if (!paramsElement.TryGetProperty("pageId", out var pageProp))
            {
                return;
            }

            var pageId = pageProp.GetString();
            if (pageId == null || !_proxyRegistry.TryGetValue(pageId, out var proxy))
            {
                return;
            }

            var totalItems = -1;
            if (paramsElement.TryGetProperty("totalItems", out var totalItemsProp))
            {
                totalItems = totalItemsProp.GetInt32();
            }

            // Dispatch off the reader thread to avoid deadlock:
            // ItemsChanged subscribers call GetItems() which sends an RPC and
            // blocks waiting for the response on this same reader thread.
            var args = new ItemsChangedEventArgs(totalItems);
            Task.Run(() =>
            {
                lock (proxy._eventLock)
                {
                    proxy._itemsChanged?.Invoke(proxy, args);
                }
            });
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

        // Support { items: [...] } response shape
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
            items.Add(new JSListItemAdapter(element, _connection));
        }

        return items.ToArray();
    }
}

/// <summary>
/// Proxy that implements IDynamicListPage by forwarding calls to a Node.js extension via JSON-RPC.
/// Extends JSListPageProxy to add the settable SearchText property.
/// </summary>
internal sealed class JSDynamicListPageProxy : IDynamicListPage
{
    private readonly JSListPageProxy _inner;
    private readonly string _pageId;
    private readonly JsonRpcConnection _connection;
    private bool _hasSearched;

    public JSDynamicListPageProxy(string pageId, JsonRpcConnection connection, JsonElement pageData = default)
    {
        _pageId = pageId ?? throw new ArgumentNullException(nameof(pageId));
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _inner = new JSListPageProxy(pageId, connection, pageData);
    }

    // ICommand members
    public string Name => _inner.Name;

    public string Id => _inner.Id;

    public IIconInfo Icon => _inner.Icon;

    // IPage members
    public string Title => _inner.Title;

    public bool IsLoading => _inner.IsLoading;

    public OptionalColor AccentColor => _inner.AccentColor;

    // IListPage members (read-only)
    public string PlaceholderText => _inner.PlaceholderText;

    public bool ShowDetails => _inner.ShowDetails;

    public IFilters Filters => _inner.Filters;

    public IGridProperties? GridProperties => _inner.GridProperties;

    public bool HasMoreItems => _inner.HasMoreItems;

    public ICommandItem? EmptyContent => _inner.EmptyContent;

    // IDynamicListPage: SearchText is settable
    public string SearchText
    {
        get
        {
            // Always return empty so the VM shows a blank search box on every
            // (re-)navigation.  If the user previously searched, also reset the
            // TS page so stale filter state doesn't leak into GetItems().
            if (_hasSearched)
            {
                _hasSearched = false;
                try
                {
                    _connection.SendRequestAsync(
                        "listPage/setSearchText",
                        new JsonObject { ["pageId"] = _pageId, ["searchText"] = string.Empty },
                        CancellationToken.None).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"Failed to reset search text for page {_pageId}: {ex.Message}");
                }
            }

            return string.Empty;
        }

        set
        {
            try
            {
                _connection.SendRequestAsync(
                    "listPage/setSearchText",
                    new JsonObject { ["pageId"] = _pageId, ["searchText"] = value },
                    CancellationToken.None).GetAwaiter().GetResult();
                _hasSearched = true;
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Failed to set search text for page {_pageId}: {ex.Message}");
            }
        }
    }

    public IListItem[] GetItems() => _inner.GetItems();

    public void LoadMore() => _inner.LoadMore();

    // INotifyItemsChanged
    public event TypedEventHandler<object, IItemsChangedEventArgs>? ItemsChanged
    {
        add => _inner.ItemsChanged += value;
        remove => _inner.ItemsChanged -= value;
    }

    // INotifyPropChanged
    public event TypedEventHandler<object, IPropChangedEventArgs>? PropChanged
    {
        add { }
        remove { }
    }
}

/// <summary>
/// Adapts JSON list item data to IListItem interface, extending the JSCommandItemAdapter pattern.
/// </summary>
internal sealed class JSListItemAdapter : IListItem
{
    private readonly JsonElement _data;
    private readonly JsonRpcConnection _connection;
    private ICommand? _command;
    private bool _commandResolved;

    public JSListItemAdapter(JsonElement data, JsonRpcConnection connection)
    {
        _data = data;
        _connection = connection;
    }

    // ICommandItem members
    public ICommand Command
    {
        get
        {
            if (!_commandResolved)
            {
                _commandResolved = true;

                if (_data.ValueKind == JsonValueKind.Object &&
                    _data.TryGetProperty("command", out var commandElement) &&
                    commandElement.ValueKind == JsonValueKind.Object)
                {
                    _command = JSCommandFactory.CreateCommandFromJson(commandElement, _connection);
                }

                // When no "command" property exists, _command stays null.
                // This signals the UI that the item is a section separator.
            }

            return _command!;
        }
    }

    public IContextItem[] MoreCommands => ParseMoreCommands();

    public IIconInfo Icon => GetIconInfo();

    public string Title => GetStringProperty("displayName") ?? GetStringProperty("title") ?? string.Empty;

    public string Subtitle => GetStringProperty("description") ?? GetStringProperty("subtitle") ?? string.Empty;

    // IListItem members
    public ITag[] Tags => ParseTags();

    public IDetails? Details => ParseDetails();

    public string Section => GetStringProperty("section") ?? string.Empty;

    public string TextToSuggest => GetStringProperty("textToSuggest") ?? string.Empty;

    public event TypedEventHandler<object, IPropChangedEventArgs>? PropChanged
    {
        add { }
        remove { }
    }

    private string? GetStringProperty(string name)
    {
        if (_data.ValueKind == JsonValueKind.Object &&
            _data.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String)
        {
            return prop.GetString();
        }

        return null;
    }

    private IIconInfo GetIconInfo()
    {
        if (_data.ValueKind == JsonValueKind.Object &&
            _data.TryGetProperty("icon", out var iconProp))
        {
            return JSIconInfoAdapter.FromJson(iconProp);
        }

        return new IconInfo(string.Empty);
    }

    private ITag[] ParseTags()
    {
        if (_data.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

#pragma warning disable CA1507 // JSON property names, not C# members
        JsonElement tagsProp;
        if (_data.TryGetProperty("Tags", out tagsProp) && tagsProp.ValueKind == JsonValueKind.Array)
        {
            // PascalCase from TS getter
        }
        else if (_data.TryGetProperty("tags", out tagsProp) && tagsProp.ValueKind == JsonValueKind.Array)
        {
            // camelCase from TS field
        }
        else
        {
            return [];
        }
#pragma warning restore CA1507

        var tags = new List<ITag>();
        foreach (var tagElement in tagsProp.EnumerateArray())
        {
            tags.Add(new JSTagAdapter(tagElement));
        }

        return tags.ToArray();
    }

    private IDetails? ParseDetails()
    {
        if (_data.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

#pragma warning disable CA1507 // JSON property names
        if (_data.TryGetProperty("Details", out var detailsProp) && detailsProp.ValueKind == JsonValueKind.Object)
        {
            return new JSDetailsAdapter(detailsProp);
        }
#pragma warning restore CA1507

        if (_data.TryGetProperty("details", out detailsProp) && detailsProp.ValueKind == JsonValueKind.Object)
        {
            return new JSDetailsAdapter(detailsProp);
        }

        return null;
    }

    private IContextItem[] ParseMoreCommands()
    {
        if (_data.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        JsonElement cmdsProp;
#pragma warning disable CA1507 // JSON property names
        if (!(_data.TryGetProperty("MoreCommands", out cmdsProp) || _data.TryGetProperty("moreCommands", out cmdsProp)) ||
            cmdsProp.ValueKind != JsonValueKind.Array)
#pragma warning restore CA1507
        {
            return [];
        }

        var items = new List<IContextItem>();
        foreach (var element in cmdsProp.EnumerateArray())
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                items.Add(new JSCommandContextItemAdapter(element, _connection));
            }
        }

        return items.ToArray();
    }
}

/// <summary>
/// Adapts JSON context item data to ICommandContextItem for context menus.
/// </summary>
internal sealed class JSCommandContextItemAdapter : ICommandContextItem
{
    private readonly JsonElement _data;
    private readonly JsonRpcConnection _connection;
    private ICommand? _command;

    public JSCommandContextItemAdapter(JsonElement data, JsonRpcConnection connection)
    {
        _data = data;
        _connection = connection;
    }

    public ICommand Command
    {
        get
        {
            if (_command == null)
            {
                if (_data.ValueKind == JsonValueKind.Object &&
                    _data.TryGetProperty("command", out var commandElement) &&
                    commandElement.ValueKind == JsonValueKind.Object)
                {
                    _command = JSCommandFactory.CreateCommandFromJson(commandElement, _connection);
                }
                else if (_data.ValueKind == JsonValueKind.Object)
                {
                    _command = JSCommandFactory.CreateCommandFromJson(_data, _connection);
                }
                else
                {
                    _command = new JSCommandAdapter(default, _connection);
                }
            }

            return _command;
        }
    }

    public IContextItem[] MoreCommands
    {
        get
        {
            if (_data.ValueKind != JsonValueKind.Object)
            {
                return [];
            }

            JsonElement cmdsProp;
#pragma warning disable CA1507 // JSON property names
            if (!(_data.TryGetProperty("MoreCommands", out cmdsProp) || _data.TryGetProperty("moreCommands", out cmdsProp)) ||
                cmdsProp.ValueKind != JsonValueKind.Array)
#pragma warning restore CA1507
            {
                return [];
            }

            var items = new List<IContextItem>();
            foreach (var element in cmdsProp.EnumerateArray())
            {
                if (element.ValueKind == JsonValueKind.Object)
                {
                    items.Add(new JSCommandContextItemAdapter(element, _connection));
                }
            }

            return items.ToArray();
        }
    }

#pragma warning disable CA1507
    public IIconInfo Icon
    {
        get
        {
            if (_data.ValueKind == JsonValueKind.Object &&
                (_data.TryGetProperty("Icon", out var iconProp) || _data.TryGetProperty("icon", out iconProp)))
            {
                return JSIconInfoAdapter.FromJson(iconProp);
            }

            return new IconInfo(string.Empty);
        }
    }

    public string Title
    {
        get
        {
            if (_data.ValueKind == JsonValueKind.Object &&
                (_data.TryGetProperty("Title", out var p) || _data.TryGetProperty("title", out p)) &&
                p.ValueKind == JsonValueKind.String)
            {
                return p.GetString() ?? string.Empty;
            }

            return string.Empty;
        }
    }

    public string Subtitle
    {
        get
        {
            if (_data.ValueKind == JsonValueKind.Object &&
                (_data.TryGetProperty("Subtitle", out var p) || _data.TryGetProperty("subtitle", out p)) &&
                p.ValueKind == JsonValueKind.String)
            {
                return p.GetString() ?? string.Empty;
            }

            return string.Empty;
        }
    }
#pragma warning restore CA1507

    public bool IsCritical => false;

    public KeyChord RequestedShortcut => default;

    public event TypedEventHandler<object, IPropChangedEventArgs>? PropChanged
    {
        add { }
        remove { }
    }
}

/// <summary>
/// Adapts JSON tag data to ITag interface.
/// </summary>
internal sealed class JSTagAdapter : ITag
{
    private readonly JsonElement _data;

    public JSTagAdapter(JsonElement data)
    {
        _data = data;
    }

    public string Text => GetStringProperty("Text") ?? GetStringProperty("text") ?? string.Empty;

    public IIconInfo Icon => GetIconInfo();

    public OptionalColor Foreground => ParseOptionalColor("Foreground", "foreground");

    public OptionalColor Background => ParseOptionalColor("Background", "background");

    public string ToolTip => GetStringProperty("ToolTip") ?? GetStringProperty("toolTip") ?? GetStringProperty("tooltip") ?? string.Empty;

    public event TypedEventHandler<object, IPropChangedEventArgs>? PropChanged
    {
        add { }
        remove { }
    }

    private string? GetStringProperty(string name)
    {
        if (_data.ValueKind == JsonValueKind.Object &&
            _data.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String)
        {
            return prop.GetString();
        }

        return null;
    }

    private IIconInfo GetIconInfo()
    {
#pragma warning disable CA1507 // JSON property names, not C# members
        if (_data.ValueKind == JsonValueKind.Object &&
            (_data.TryGetProperty("Icon", out var iconProp) || _data.TryGetProperty("icon", out iconProp)))
#pragma warning restore CA1507
        {
            return JSIconInfoAdapter.FromJson(iconProp);
        }

        return new IconInfo(string.Empty);
    }

    private OptionalColor ParseOptionalColor(string pascalName, string camelName)
    {
        if (_data.ValueKind != JsonValueKind.Object)
        {
            return ColorHelpers.NoColor();
        }

        if (!_data.TryGetProperty(pascalName, out var colorProp) || colorProp.ValueKind != JsonValueKind.Object)
        {
            if (!_data.TryGetProperty(camelName, out colorProp) || colorProp.ValueKind != JsonValueKind.Object)
            {
                return ColorHelpers.NoColor();
            }
        }

        // Check for HasValue wrapper: { HasValue: true, Color: { R, G, B, A } }
        if (colorProp.TryGetProperty("HasValue", out var hasValueProp) ||
            colorProp.TryGetProperty("hasValue", out hasValueProp))
        {
            if (hasValueProp.ValueKind == JsonValueKind.False)
            {
                return ColorHelpers.NoColor();
            }

            if (colorProp.TryGetProperty("Color", out var innerColor) ||
                colorProp.TryGetProperty("color", out innerColor))
            {
                colorProp = innerColor;
            }
        }

        byte r = 0, g = 0, b = 0, a = 255;
        if (colorProp.TryGetProperty("R", out var rProp) || colorProp.TryGetProperty("r", out rProp))
        {
            r = rProp.GetByte();
        }

        if (colorProp.TryGetProperty("G", out var gProp) || colorProp.TryGetProperty("g", out gProp))
        {
            g = gProp.GetByte();
        }

        if (colorProp.TryGetProperty("B", out var bProp) || colorProp.TryGetProperty("b", out bProp))
        {
            b = bProp.GetByte();
        }

        if (colorProp.TryGetProperty("A", out var aProp) || colorProp.TryGetProperty("a", out aProp))
        {
            a = aProp.GetByte();
        }

        return ColorHelpers.FromArgb(a, r, g, b);
    }
}

/// <summary>
/// Adapts JSON details data to IDetails interface.
/// </summary>
internal sealed class JSDetailsAdapter : IDetails, IExtendedAttributesProvider
{
    private readonly JsonElement _data;

    public JSDetailsAdapter(JsonElement data)
    {
        _data = data;
    }

    private bool HasData => _data.ValueKind == JsonValueKind.Object;

    public IDictionary<string, object> GetProperties()
    {
        var props = new Dictionary<string, object>();
        if (!HasData)
        {
            return props;
        }

#pragma warning disable CA1507 // JSON property names from external data
        if ((_data.TryGetProperty("Size", out var sizeProp) || _data.TryGetProperty("size", out sizeProp)) &&
            sizeProp.ValueKind == JsonValueKind.Number)
        {
            props["Size"] = sizeProp.GetInt32();
        }
#pragma warning restore CA1507

        return props;
    }

#pragma warning disable CA1507 // JSON property names from external data
    public IIconInfo HeroImage => GetIconInfo("HeroImage", "heroImage");

    public string Title => GetStringProperty("Title") ?? GetStringProperty("title") ?? string.Empty;

    public string Body => GetStringProperty("Body") ?? GetStringProperty("body") ?? string.Empty;
#pragma warning restore CA1507

    public IDetailsElement[] Metadata => ParseMetadata();

    public event TypedEventHandler<object, IPropChangedEventArgs>? PropChanged
    {
        add { }
        remove { }
    }

    private string? GetStringProperty(string name)
    {
        if (HasData && _data.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String)
        {
            return prop.GetString();
        }

        return null;
    }

    private IIconInfo GetIconInfo(string pascalName, string camelName)
    {
        if (!HasData)
        {
            return new IconInfo(string.Empty);
        }

#pragma warning disable CA1507
        if (_data.TryGetProperty(pascalName, out var iconProp) || _data.TryGetProperty(camelName, out iconProp))
#pragma warning restore CA1507
        {
            return JSIconInfoAdapter.FromJson(iconProp);
        }

        return new IconInfo(string.Empty);
    }

    private IDetailsElement[] ParseMetadata()
    {
        if (!HasData)
        {
            return [];
        }

#pragma warning disable CA1507
        if (!_data.TryGetProperty("Metadata", out var metaProp) || metaProp.ValueKind != JsonValueKind.Array)
        {
            if (!_data.TryGetProperty("metadata", out metaProp) || metaProp.ValueKind != JsonValueKind.Array)
            {
                return [];
            }
        }
#pragma warning restore CA1507

        var elements = new List<IDetailsElement>();
        foreach (var element in metaProp.EnumerateArray())
        {
            var key = string.Empty;
#pragma warning disable CA1507
            if ((element.TryGetProperty("Key", out var keyProp) || element.TryGetProperty("key", out keyProp)) &&
                keyProp.ValueKind == JsonValueKind.String)
            {
                key = keyProp.GetString() ?? string.Empty;
            }

            IDetailsData? data = null;
            if (element.TryGetProperty("Data", out var dataProp) || element.TryGetProperty("data", out dataProp))
            {
                data = ParseDetailsData(dataProp);
            }
#pragma warning restore CA1507

            elements.Add(new DetailsElement { Key = key, Data = data });
        }

        return elements.ToArray();
    }

    private static IDetailsData? ParseDetailsData(JsonElement dataElement)
    {
        if (dataElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

#pragma warning disable CA1507
        // Check for Tags data
        if (dataElement.TryGetProperty("Tags", out var tagsProp) || dataElement.TryGetProperty("tags", out tagsProp))
        {
            if (tagsProp.ValueKind == JsonValueKind.Array)
            {
                var tags = new List<ITag>();
                foreach (var tagEl in tagsProp.EnumerateArray())
                {
                    tags.Add(new JSTagAdapter(tagEl));
                }

                return new DetailsTags { Tags = tags.ToArray() };
            }
        }
#pragma warning restore CA1507

        return null;
    }
}

/// <summary>
/// Adapts JSON filters data to IFilters interface.
/// </summary>
internal sealed class JSFiltersAdapter : IFilters
{
    private readonly JsonElement _data;
    private readonly JsonRpcConnection _connection;
    private readonly string _pageId;
    private string _currentFilterId = string.Empty;

    public JSFiltersAdapter(JsonElement data, JsonRpcConnection connection, string pageId = "")
    {
        _data = data;
        _connection = connection;
        _pageId = pageId;

        // Read initial filter ID from JSON data
        if (_data.ValueKind == JsonValueKind.Object &&
            _data.TryGetProperty("currentFilterId", out var prop) &&
            prop.ValueKind == JsonValueKind.String)
        {
            _currentFilterId = prop.GetString() ?? string.Empty;
        }
    }

    public string CurrentFilterId
    {
        get => _currentFilterId;

        set
        {
            _currentFilterId = value;

            if (!string.IsNullOrEmpty(_pageId))
            {
                var args = new JsonObject
                {
                    ["pageId"] = _pageId,
                    ["filterId"] = value,
                };
                _ = _connection.SendRequestAsync("listPage/setFilter", args, CancellationToken.None);
            }
        }
    }

    public IFilterItem[] GetFilters()
    {
        if (_data.ValueKind != JsonValueKind.Object ||
            !_data.TryGetProperty("filters", out var filtersProp) ||
            filtersProp.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var filters = new List<IFilterItem>();
        foreach (var element in filtersProp.EnumerateArray())
        {
            filters.Add(new JSFilterAdapter(element));
        }

        return filters.ToArray();
    }
}

/// <summary>
/// Adapts JSON filter data to IFilter interface.
/// </summary>
internal sealed class JSFilterAdapter : IFilter
{
    private readonly JsonElement _data;

    public JSFilterAdapter(JsonElement data)
    {
        _data = data;
    }

    public string Id => GetStringProperty("id") ?? string.Empty;

    public string Name => GetStringProperty("name") ?? string.Empty;

    public IIconInfo Icon => GetIconInfo();

    public event TypedEventHandler<object, IPropChangedEventArgs>? PropChanged
    {
        add { }
        remove { }
    }

    private string? GetStringProperty(string name)
    {
        if (_data.ValueKind == JsonValueKind.Object &&
            _data.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String)
        {
            return prop.GetString();
        }

        return null;
    }

    private IIconInfo GetIconInfo()
    {
        if (_data.ValueKind == JsonValueKind.Object &&
            _data.TryGetProperty("icon", out var iconProp))
        {
            return JSIconInfoAdapter.FromJson(iconProp);
        }

        return new IconInfo(string.Empty);
    }
}

// ---------------------------------------------------------------------------
// Grid layout adapters
// ---------------------------------------------------------------------------

/// <summary>
/// Adapts JSON grid properties to ISmallGridLayout.
/// </summary>
internal sealed class JSSmallGridLayoutAdapter : ISmallGridLayout
{
    public event TypedEventHandler<object, IPropChangedEventArgs>? PropChanged
    {
        add { }
        remove { }
    }
}

/// <summary>
/// Adapts JSON grid properties to IMediumGridLayout.
/// </summary>
internal sealed class JSMediumGridLayoutAdapter : IMediumGridLayout
{
    public bool ShowTitle { get; }

    public JSMediumGridLayoutAdapter(bool showTitle)
    {
        ShowTitle = showTitle;
    }

    public event TypedEventHandler<object, IPropChangedEventArgs>? PropChanged
    {
        add { }
        remove { }
    }
}

/// <summary>
/// Adapts JSON grid properties to IGalleryGridLayout.
/// </summary>
internal sealed class JSGalleryGridLayoutAdapter : IGalleryGridLayout
{
    public bool ShowTitle { get; }

    public bool ShowSubtitle { get; }

    public JSGalleryGridLayoutAdapter(bool showTitle, bool showSubtitle)
    {
        ShowTitle = showTitle;
        ShowSubtitle = showSubtitle;
    }

    public event TypedEventHandler<object, IPropChangedEventArgs>? PropChanged
    {
        add { }
        remove { }
    }
}

/// <summary>
/// Factory for creating grid layout adapters from JSON.
/// </summary>
internal static class JSGridPropertiesFactory
{
    internal static IGridProperties? FromJson(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var layout = string.Empty;
        if (element.TryGetProperty("layout", out var layoutProp) && layoutProp.ValueKind == JsonValueKind.String)
        {
            layout = layoutProp.GetString() ?? string.Empty;
        }

        var showTitle = !element.TryGetProperty("showTitle", out var stProp) || stProp.ValueKind != JsonValueKind.False;
        var showSubtitle = !element.TryGetProperty("showSubtitle", out var ssProp) || ssProp.ValueKind != JsonValueKind.False;

        return layout switch
        {
            "small" => new JSSmallGridLayoutAdapter(),
            "medium" => new JSMediumGridLayoutAdapter(showTitle),
            "gallery" => new JSGalleryGridLayoutAdapter(showTitle, showSubtitle),
            _ => null,
        };
    }
}

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
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels.Models;

/// <summary>
/// Proxy that presents a Node.js extension content page as <see cref="IContentPage"/>.
/// Content is fetched with <c>contentPage/getContent</c>; details and commands are
/// materialized from the page payload.
/// </summary>
internal sealed partial class JSContentPageProxy : BaseObservable, IContentPage
{
    private readonly string _pageId;
    private readonly JsonRpcConnection _connection;
    private readonly JsonElement _pageData;

    public JSContentPageProxy(string pageId, JsonRpcConnection connection, JsonElement pageData = default)
    {
        _pageId = pageId ?? throw new ArgumentNullException(nameof(pageId));
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _pageData = pageData;
    }

    public event TypedEventHandler<object, IItemsChangedEventArgs>? ItemsChanged
    {
        add { }
        remove { }
    }

    public string Id => JSModelMapper.GetString(_pageData, "id") ?? _pageId;

    public string Name => JSModelMapper.GetString(_pageData, "name") ?? string.Empty;

    public IIconInfo Icon => JSModelMapper.GetIcon(_pageData, "icon", "Icon");

    public string Title => JSModelMapper.GetString(_pageData, "title") ?? Name;

    public bool IsLoading => JSModelMapper.GetBool(_pageData, "isLoading", false);

    public OptionalColor AccentColor => ColorHelpers.NoColor();

    public IDetails? Details => JSModelMapper.ParseDetails(_pageData, _connection);

    public IContextItem[] Commands => JSModelMapper.ParseContextItems(_pageData, "commands", "Commands", _connection);

    public IContent[] GetContent()
    {
        try
        {
            var response = _connection.SendRequestAsync(
                "contentPage/getContent",
                new JsonObject { ["pageId"] = _pageId },
                CancellationToken.None).GetAwaiter().GetResult();

            if (response.Error != null)
            {
                Logger.LogError($"GetContent error for page {_pageId}: {response.Error.Message}");
                return [];
            }

            return JSModelMapper.ParseContentArray(UnwrapContent(response.Result), _pageId, _connection);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to get content for page {_pageId}: {ex.Message}");
            return [];
        }
    }

    private static JsonElement? UnwrapContent(JsonElement? result)
    {
        if (!result.HasValue)
        {
            return null;
        }

        if (result.Value.ValueKind == JsonValueKind.Object &&
            result.Value.TryGetProperty("content", out var contentProp))
        {
            return contentProp;
        }

        return result;
    }
}

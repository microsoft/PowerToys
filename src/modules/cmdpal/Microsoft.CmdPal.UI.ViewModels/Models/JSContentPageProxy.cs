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
/// Exposes a Node.js extension content page as <see cref="IContentPage"/>.
/// Content comes from <c>contentPage/getContent</c>. Details and commands come
/// from the page payload.
/// </summary>
internal sealed partial class JSContentPageProxy : JSObservableProxyBase, IContentPage
{
    private static readonly ConditionalWeakTable<JsonRpcConnection, PageRegistry> Registries = new();

    private readonly string _pageId;
    private readonly PageRegistry _registry;

    public JSContentPageProxy(string pageId, JsonRpcConnection connection, JsonElement pageData = default)
        : base(pageId, connection, pageData)
    {
        _pageId = pageId ?? throw new ArgumentNullException(nameof(pageId));

        _registry = Registries.GetValue(Connection, static _ => new PageRegistry());
        _registry.EnsureSubscribed(Connection);

        var pages = _registry.Pages.GetOrAdd(_pageId, static _ => new List<WeakReference<JSContentPageProxy>>());
        lock (pages)
        {
            pages.Add(new WeakReference<JSContentPageProxy>(this));
        }
    }

    public event TypedEventHandler<object, IItemsChangedEventArgs>? ItemsChanged;

    public string Id => JSModelMapper.GetString(Data, "id") ?? _pageId;

    public string Name => JSModelMapper.GetString(Data, "name") ?? string.Empty;

    public IIconInfo Icon => JSModelMapper.GetIcon(Data, "icon", "Icon");

    public string Title => JSModelMapper.GetString(Data, "title") ?? Name;

    public bool IsLoading => JSModelMapper.GetBool(Data, "isLoading", false);

    public OptionalColor AccentColor => JSModelMapper.ParseColor(Data, "accentColor", "AccentColor");

    public IDetails? Details => JSModelMapper.ParseDetails(Data, Connection);

    public IContextItem[] Commands => JSModelMapper.ParseContextItems(Data, "commands", "Commands", Connection);

    public IContent[] GetContent()
    {
        try
        {
            var response = Connection.SendRequestAsync(
                "contentPage/getContent",
                new JsonObject { ["pageId"] = _pageId },
                CancellationToken.None).GetAwaiter().GetResult();

            if (response.Error != null)
            {
                Logger.LogError($"GetContent error for page {_pageId}: {response.Error.Message}");
                return [];
            }

            return JSModelMapper.ParseContentArray(UnwrapContent(response.Result), _pageId, Connection);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to get content for page {_pageId}: {ex.Message}");
            return [];
        }
    }

    public override void Dispose()
    {
        if (_registry.Pages.TryGetValue(_pageId, out var pages))
        {
            lock (pages)
            {
                pages.RemoveAll(weak => !weak.TryGetTarget(out var target) || ReferenceEquals(target, this));
                if (pages.Count == 0)
                {
                    _registry.Pages.TryRemove(_pageId, out _);
                }
            }
        }

        base.Dispose();
    }

    protected override bool SupportsProperty(string propertyName) => propertyName switch
    {
        "id" or "name" or "icon" or "title" or "isLoading" or "accentColor" or
        "details" or "commands" => true,
        _ => false,
    };

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

    private static void DispatchItemsChanged(PageRegistry registry, JsonElement parameters)
    {
        try
        {
            if (parameters.ValueKind != JsonValueKind.Object ||
                !parameters.TryGetProperty("pageId", out var pageProperty))
            {
                return;
            }

            var pageId = pageProperty.GetString();
            if (pageId is null || !registry.Pages.TryGetValue(pageId, out var pageReferences))
            {
                return;
            }

            List<JSContentPageProxy> targets = [];
            lock (pageReferences)
            {
                pageReferences.RemoveAll(weak => !weak.TryGetTarget(out _));
                foreach (var weak in pageReferences)
                {
                    if (weak.TryGetTarget(out var target))
                    {
                        targets.Add(target);
                    }
                }

                if (pageReferences.Count == 0)
                {
                    registry.Pages.TryRemove(pageId, out _);
                }
            }

            foreach (var target in targets)
            {
                var handler = target.ItemsChanged;
                if (handler is not null)
                {
                    _ = Task.Run(() => handler.Invoke(target, new ItemsChangedEventArgs(-1)));
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Error handling contentPage/itemsChanged notification: {ex.Message}");
        }
    }

    private sealed class PageRegistry
    {
        private readonly object _subscribeLock = new();
        private bool _subscribed;

        public ConcurrentDictionary<string, List<WeakReference<JSContentPageProxy>>> Pages { get; } = new();

        public void EnsureSubscribed(JsonRpcConnection connection)
        {
            lock (_subscribeLock)
            {
                if (_subscribed)
                {
                    return;
                }

                connection.RegisterNotificationHandler(
                    "contentPage/itemsChanged",
                    parameters => DispatchItemsChanged(this, parameters));
                _subscribed = true;
            }
        }
    }
}

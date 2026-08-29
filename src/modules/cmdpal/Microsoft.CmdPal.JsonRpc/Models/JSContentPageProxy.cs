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
using ManagedCommon;
using Microsoft.CmdPal.JsonRpc;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Foundation;

namespace Microsoft.CmdPal.JsonRpc.Models;

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
    private readonly JSLazyCache<IDetails?> _details;
    private readonly JSLazyCache<IContextItem[]> _commands;

    public JSContentPageProxy(string pageId, JsonRpcConnection connection, JsonElement pageData = default)
        : base(pageId, connection, pageData)
    {
        _pageId = pageId ?? throw new ArgumentNullException(nameof(pageId));
        _details = new JSLazyCache<IDetails?>(
            () => JSModelMapper.ParseDetails(Data, Connection),
            JSModelMapper.DisposeDetails);
        _commands = new JSLazyCache<IContextItem[]>(
            () => JSModelMapper.ParseContextItems(Data, "commands", Connection),
            JSModelMapper.DisposeContextItems);

        _registry = Registries.GetValue(Connection, static _ => new PageRegistry());
        _registry.EnsureSubscribed(Connection);

        _registry.Pages.Register(_pageId, this);
    }

    public event TypedEventHandler<object, IItemsChangedEventArgs>? ItemsChanged;

    public string Id => JSModelMapper.GetString(Data, "id") ?? _pageId;

    public string Name => JSModelMapper.GetString(Data, "name") ?? string.Empty;

    public IIconInfo Icon => JSModelMapper.GetIcon(Data, "icon");

    public string Title => JSModelMapper.GetString(Data, "title") ?? Name;

    public bool IsLoading => JSModelMapper.GetBool(Data, "isLoading", false);

    public OptionalColor AccentColor => JSModelMapper.ParseColor(Data, "accentColor");

    public IDetails? Details => _details.Value;

    public IContextItem[] Commands => _commands.Value;

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
            Logger.LogError($"Failed to get content for page {_pageId}.", ex);
            return [];
        }
    }

    public override void Dispose()
    {
        _details.Dispose();
        _commands.Dispose();
        _registry.Pages.Unregister(_pageId, this);

        base.Dispose();
    }

    protected override bool SupportsProperty(string propertyName) => propertyName switch
    {
        "id" or "name" or "icon" or "title" or "isLoading" or "accentColor" or
        "details" or "commands" => true,
        _ => false,
    };

    protected override void OnPropertyChangesApplied(IReadOnlyList<string> propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (propertyName == "details")
            {
                _details.Reset();
            }
            else if (propertyName == "commands")
            {
                _commands.Reset();
            }
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
            if (pageId is null)
            {
                return;
            }

            foreach (var target in registry.Pages.GetLiveTargets(pageId))
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

        public JSWeakReferenceRegistry<string, JSContentPageProxy> Pages { get; } = new();

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

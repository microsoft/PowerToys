// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using ManagedCommon;
using Microsoft.CmdPal.JsonRpc;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.JsonRpc.Models;

/// <summary>
/// Adapts a JSON fallback command payload to <see cref="IFallbackCommandItem2"/>.
/// Canonical property updates apply through <c>command/propChanged</c>.
/// </summary>
internal sealed partial class JSFallbackCommandItemAdapter : JSObservableProxyBase, IFallbackCommandItem2
{
    private readonly object _commandStateLock = new();
    private readonly JSLazyCache<ICommand?> _command;
    private readonly JSLazyCache<IContextItem[]> _moreCommands;
    private IFallbackHandler? _fallbackHandler;

    public JSFallbackCommandItemAdapter(JsonElement data, JsonRpcConnection connection)
        : base(GetNotificationId(data), connection, data)
    {
        _command = new JSLazyCache<ICommand?>(
            CreateCommand,
            JSLazyCache<ICommand?>.DisposeValue);
        _moreCommands = new JSLazyCache<IContextItem[]>(
            () => JSModelMapper.ParseMoreCommands(Data, Connection),
            JSModelMapper.DisposeContextItems);
    }

    public ICommand? Command => _command.Value;

    public IContextItem[] MoreCommands => _moreCommands.Value;

    public IIconInfo Icon => JSModelMapper.TryGetIcon(Data, "icon", out var icon)
        ? icon
        : Command?.Icon ?? new IconInfo(string.Empty);

    public string Title => JSModelMapper.GetString(Data, "title") ?? string.Empty;

    public string Subtitle => JSModelMapper.GetString(Data, "subtitle") ?? string.Empty;

    public string DisplayTitle => JSModelMapper.GetString(Data, "displayTitle") ?? Title;

    public string Id => JSModelMapper.GetString(Data, "id") ?? Command?.Id ?? string.Empty;

    public IFallbackHandler FallbackHandler
    {
        get
        {
            lock (_commandStateLock)
            {
                _fallbackHandler ??= new JSFallbackHandler(Connection, _command.Value?.Id ?? Id);
                return _fallbackHandler;
            }
        }
    }

    protected override bool SupportsProperty(string propertyName) => propertyName switch
    {
        "command" or "moreCommands" or "icon" or "title" or "subtitle" or "displayTitle" => true,
        _ => false,
    };

    protected override void OnPropertyChangesApplied(IReadOnlyList<string> propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (propertyName == "command")
            {
                lock (_commandStateLock)
                {
                    _command.Reset();
                    _fallbackHandler = null;
                }
            }
            else if (propertyName == "moreCommands")
            {
                _moreCommands.Reset();
            }
        }
    }

    public override void Dispose()
    {
        _moreCommands.Dispose();
        lock (_commandStateLock)
        {
            _command.Dispose();
        }

        base.Dispose();
    }

    private ICommand? CreateCommand()
    {
        return JSCommandFactory.CreateCommandFromJson(JSModelMapper.GetCommandData(Data), Connection);
    }

    private static string GetNotificationId(JsonElement data)
    {
        var commandId = JSModelMapper.GetString(JSModelMapper.GetCommandData(data), "id");
        if (!string.IsNullOrEmpty(commandId))
        {
            return commandId;
        }

        return JSModelMapper.GetString(data, "id") ?? string.Empty;
    }

    private sealed partial class JSFallbackHandler : IFallbackHandler
    {
        private readonly JsonRpcConnection _connection;
        private readonly string _commandId;

        public JSFallbackHandler(JsonRpcConnection connection, string commandId)
        {
            _connection = connection;
            _commandId = commandId;
        }

        public void UpdateQuery(string query)
        {
            try
            {
                _connection.SendRequestAsync(
                    "fallback/updateQuery",
                    new JsonObject { ["commandId"] = _commandId, ["query"] = query },
                    CancellationToken.None).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Failed to send fallback query update: {ex.Message}");
            }
        }
    }
}

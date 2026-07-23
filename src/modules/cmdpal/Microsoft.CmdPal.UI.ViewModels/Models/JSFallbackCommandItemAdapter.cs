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

namespace Microsoft.CmdPal.UI.ViewModels.Models;

/// <summary>
/// Adapts a JSON fallback command payload to <see cref="IFallbackCommandItem2"/>.
/// The display title can be updated when the extension pushes a
/// <c>command/propChanged</c> notification, and query updates are forwarded via
/// <c>fallback/updateQuery</c>.
/// </summary>
internal sealed partial class JSFallbackCommandItemAdapter : BaseObservable, IFallbackCommandItem2
{
    private readonly JsonElement _data;
    private readonly JsonRpcConnection _connection;
    private ICommand? _command;
    private bool _commandResolved;
    private IFallbackHandler? _fallbackHandler;
    private string? _displayTitleOverride;

    public JSFallbackCommandItemAdapter(JsonElement data, JsonRpcConnection connection)
    {
        _data = data;
        _connection = connection;
    }

    public ICommand? Command
    {
        get
        {
            if (!_commandResolved)
            {
                _commandResolved = true;

                var commandData = _data;
                if (JSModelMapper.TryGetAnyCase(_data, "command", "Command", out var commandElement) &&
                    commandElement.ValueKind == JsonValueKind.Object)
                {
                    commandData = commandElement;
                }

                _command = JSCommandFactory.CreateCommandFromJson(commandData, _connection);
            }

            return _command;
        }
    }

    public IContextItem[] MoreCommands => JSModelMapper.ParseContextItems(_data, "moreCommands", "MoreCommands", _connection);

    public IIconInfo Icon => JSModelMapper.TryGetIcon(_data, "icon", "Icon", out var icon)
        ? icon
        : Command?.Icon ?? new IconInfo(string.Empty);

    public string Title => JSModelMapper.GetString(_data, "displayName") ?? JSModelMapper.GetString(_data, "title") ?? string.Empty;

    public string Subtitle => JSModelMapper.GetString(_data, "description") ?? JSModelMapper.GetString(_data, "subtitle") ?? string.Empty;

    public string DisplayTitle => _displayTitleOverride ?? JSModelMapper.GetString(_data, "displayTitle") ?? Title;

    public string Id => JSModelMapper.GetString(_data, "id") ?? string.Empty;

    public IFallbackHandler FallbackHandler
    {
        get
        {
            _fallbackHandler ??= new JSFallbackHandler(_connection, Id);
            return _fallbackHandler;
        }
    }

    public void UpdateDisplayTitle(string newTitle)
    {
        _displayTitleOverride = newTitle;
        OnPropertyChanged(nameof(DisplayTitle));
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

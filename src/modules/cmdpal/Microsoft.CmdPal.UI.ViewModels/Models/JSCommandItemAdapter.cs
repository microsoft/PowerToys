// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using Microsoft.CmdPal.UI.ViewModels.Services.JsonRpc;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.ViewModels.Models;

/// <summary>
/// Adapts a JSON command item payload to <see cref="ICommandItem"/>. The nested
/// command is resolved lazily so page proxies are only created on demand.
/// </summary>
internal sealed partial class JSCommandItemAdapter : BaseObservable, ICommandItem
{
    private readonly JsonElement _data;
    private readonly JsonRpcConnection _connection;
    private ICommand? _command;
    private bool _commandResolved;

    public JSCommandItemAdapter(JsonElement data, JsonRpcConnection connection)
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
}

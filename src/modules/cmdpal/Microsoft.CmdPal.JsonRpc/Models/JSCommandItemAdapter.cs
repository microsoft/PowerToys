// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using Microsoft.CmdPal.JsonRpc;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.JsonRpc.Models;

/// <summary>
/// Adapts a JSON command item payload to <see cref="ICommandItem"/>.
/// The nested command is resolved lazily so page proxies are created only when needed.
/// </summary>
internal sealed partial class JSCommandItemAdapter : JSObservableProxyBase, ICommandItem
{
    private readonly JSLazyCache<ICommand?> _command;
    private readonly JSLazyCache<IContextItem[]> _moreCommands;

    public JSCommandItemAdapter(JsonElement data, JsonRpcConnection connection)
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

    protected override bool SupportsProperty(string propertyName) => propertyName switch
    {
        "command" or "moreCommands" or "icon" or "title" or "subtitle" => true,
        _ => false,
    };

    protected override void OnPropertyChangesApplied(IReadOnlyList<string> propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (propertyName == "command")
            {
                _command.Reset();
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
        _command.Dispose();
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
}

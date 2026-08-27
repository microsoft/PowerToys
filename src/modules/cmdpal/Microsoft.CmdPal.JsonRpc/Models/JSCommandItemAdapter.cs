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
    private Lazy<ICommand?> _command;

    public JSCommandItemAdapter(JsonElement data, JsonRpcConnection connection)
        : base(GetNotificationId(data), connection, data)
    {
        _command = CreateCommand();
    }

    public ICommand? Command => Volatile.Read(ref _command).Value;

    public IContextItem[] MoreCommands => JSModelMapper.ParseMoreCommands(Data, Connection);

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
                Volatile.Write(ref _command, CreateCommand());
                break;
            }
        }
    }

    private Lazy<ICommand?> CreateCommand()
    {
        return new Lazy<ICommand?>(
            () => JSCommandFactory.CreateCommandFromJson(JSModelMapper.GetCommandData(Data), Connection));
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

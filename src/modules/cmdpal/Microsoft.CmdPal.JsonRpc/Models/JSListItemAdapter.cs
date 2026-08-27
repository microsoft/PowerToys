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
/// Adapts a JSON list item payload to <see cref="IListItem"/>.
/// The nested command is resolved lazily. Tags, details, and context items are
/// built through <see cref="JSModelMapper"/>.
/// </summary>
internal sealed partial class JSListItemAdapter : JSObservableProxyBase, IListItem
{
    private Lazy<ICommand?> _command;

    public JSListItemAdapter(JsonElement data, JsonRpcConnection connection)
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

    public ITag[] Tags => JSModelMapper.ParseTags(Data);

    public IDetails? Details => JSModelMapper.ParseDetails(Data, Connection);

    public string Section => JSModelMapper.GetString(Data, "section") ?? string.Empty;

    public string TextToSuggest => JSModelMapper.GetString(Data, "textToSuggest") ?? string.Empty;

    protected override bool SupportsProperty(string propertyName) => propertyName switch
    {
        "command" or "moreCommands" or "icon" or "title" or "subtitle" or
        "tags" or "details" or "section" or "textToSuggest" => true,
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
        return new Lazy<ICommand?>(() =>
        {
            return JSModelMapper.TryGetCommandData(Data, out var commandData)
                ? JSCommandFactory.CreateCommandFromJson(commandData, Connection)
                : null;
        });
    }

    private static string GetNotificationId(JsonElement data)
    {
        if (JSModelMapper.TryGetCommandData(data, out var commandData))
        {
            var commandId = JSModelMapper.GetString(commandData, "id");
            if (!string.IsNullOrEmpty(commandId))
            {
                return commandId;
            }
        }

        return JSModelMapper.GetString(data, "id") ?? string.Empty;
    }
}

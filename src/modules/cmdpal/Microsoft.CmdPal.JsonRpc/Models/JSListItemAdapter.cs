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
    private readonly JSLazyCache<ICommand?> _command;
    private readonly JSLazyCache<IContextItem[]> _moreCommands;
    private readonly JSLazyCache<IDetails?> _details;
    private static readonly string[] RefreshableProperties =
    [
        "command",
        "moreCommands",
        "icon",
        "title",
        "subtitle",
        "tags",
        "details",
        "section",
        "textToSuggest",
    ];

    public JSListItemAdapter(JsonElement data, JsonRpcConnection connection)
        : base(GetNotificationId(data), connection, data)
    {
        _command = new JSLazyCache<ICommand?>(
            CreateCommand,
            JSLazyCache<ICommand?>.DisposeValue);
        _moreCommands = new JSLazyCache<IContextItem[]>(
            () => JSModelMapper.ParseMoreCommands(Data, Connection),
            JSModelMapper.DisposeContextItems);
        _details = new JSLazyCache<IDetails?>(
            () => JSModelMapper.ParseDetails(Data, Connection),
            JSModelMapper.DisposeDetails);
    }

    public ICommand? Command => _command.Value;

    public IContextItem[] MoreCommands => _moreCommands.Value;

    public IIconInfo Icon => JSModelMapper.TryGetIcon(Data, "icon", out var icon)
        ? icon
        : Command?.Icon ?? new IconInfo(string.Empty);

    public string Title => JSModelMapper.GetString(Data, "title") ?? string.Empty;

    public string Subtitle => JSModelMapper.GetString(Data, "subtitle") ?? string.Empty;

    public ITag[] Tags => JSModelMapper.ParseTags(Data);

    public IDetails? Details => _details.Value;

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
                _command.Reset();
            }
            else if (propertyName == "moreCommands")
            {
                _moreCommands.Reset();
            }
            else if (propertyName == "details")
            {
                _details.Reset();
            }
        }
    }

    internal static string ComputeKey(JsonElement data)
    {
        var id = JSModelMapper.GetString(data, "id");
        if (!string.IsNullOrEmpty(id))
        {
            return "id:" + id;
        }

        if (JSModelMapper.TryGetCommandData(data, out var commandData))
        {
            var commandId = JSModelMapper.GetString(commandData, "id");
            if (!string.IsNullOrEmpty(commandId))
            {
                return "cmd:" + commandId;
            }
        }

        return "title:" + (JSModelMapper.GetString(data, "title") ?? string.Empty);
    }

    internal void UpdateData(JsonElement data)
    {
        ReplaceData(data, RefreshableProperties, GetNotificationId(data));
    }

    public override void Dispose()
    {
        _moreCommands.Dispose();
        _details.Dispose();
        _command.Dispose();
        base.Dispose();
    }

    private ICommand? CreateCommand()
    {
        return JSModelMapper.TryGetCommandData(Data, out var commandData)
            ? JSCommandFactory.CreateCommandFromJson(commandData, Connection)
            : null;
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

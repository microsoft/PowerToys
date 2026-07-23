// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json;
using System.Threading;
using Microsoft.CmdPal.UI.ViewModels.Services.JsonRpc;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.ViewModels.Models;

/// <summary>
/// Adapts a JSON list item payload to <see cref="IListItem"/>. The nested
/// command is resolved lazily; tags, details and context items are materialized
/// through <see cref="JSModelMapper"/>.
/// </summary>
/// <remarks>
/// The backing payload can be refreshed in place through <see cref="UpdateData"/>
/// when a dynamic page rebuilds its items. Reusing the same adapter instance lets
/// the host keep the same view model (and therefore the current selection) while
/// the item's content updates. The payload is held behind a boxed reference so the
/// background fetch thread can swap it without a UI-thread reader seeing a torn
/// <see cref="JsonElement"/> struct.
/// </remarks>
internal sealed partial class JSListItemAdapter : BaseObservable, IListItem
{
    private readonly JsonRpcConnection _connection;
    private DataBox _dataBox;
    private ICommand? _command;
    private bool _commandResolved;

    public JSListItemAdapter(JsonElement data, JsonRpcConnection connection)
    {
        _dataBox = new DataBox(data);
        _connection = connection;
    }

    private JsonElement Data => Volatile.Read(ref _dataBox).Element;

    public ICommand? Command
    {
        get
        {
            if (!_commandResolved)
            {
                _commandResolved = true;

                if (JSModelMapper.TryGetAnyCase(Data, "command", "Command", out var commandElement) &&
                    commandElement.ValueKind == JsonValueKind.Object)
                {
                    _command = JSCommandFactory.CreateCommandFromJson(commandElement, _connection);
                }
            }

            return _command;
        }
    }

    public IContextItem[] MoreCommands => JSModelMapper.ParseContextItems(Data, "moreCommands", "MoreCommands", _connection);

    public IIconInfo Icon => JSModelMapper.TryGetIcon(Data, "icon", "Icon", out var icon)
        ? icon
        : Command?.Icon ?? new IconInfo(string.Empty);

    public string Title => JSModelMapper.GetString(Data, "displayName") ?? JSModelMapper.GetString(Data, "title") ?? string.Empty;

    public string Subtitle => JSModelMapper.GetString(Data, "description") ?? JSModelMapper.GetString(Data, "subtitle") ?? string.Empty;

    public ITag[] Tags => JSModelMapper.ParseTags(Data);

    public IDetails? Details => JSModelMapper.ParseDetails(Data, _connection);

    public string Section => JSModelMapper.GetString(Data, "section") ?? string.Empty;

    public string TextToSuggest => JSModelMapper.GetString(Data, "textToSuggest") ?? string.Empty;

    /// <summary>
    /// Derives a stable identity for a list item payload so adapters can be
    /// reused across refreshes. Mirrors the <see cref="Title"/> resolution.
    /// </summary>
    internal static string ComputeKey(JsonElement data)
        => JSModelMapper.GetString(data, "displayName") ?? JSModelMapper.GetString(data, "title") ?? string.Empty;

    /// <summary>
    /// Refreshes the backing payload in place. When the content actually changes
    /// this raises <c>PropChanged</c> for the affected properties so the bound
    /// view model updates without being replaced, preserving the current
    /// selection on a live-updating list.
    /// </summary>
    internal void UpdateData(JsonElement data)
    {
        var current = Volatile.Read(ref _dataBox).Element;
        if (current.ValueKind != JsonValueKind.Undefined &&
            string.Equals(current.GetRawText(), data.GetRawText(), StringComparison.Ordinal))
        {
            return;
        }

        Volatile.Write(ref _dataBox, new DataBox(data));
        _commandResolved = false;
        _command = null;

        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Subtitle));
        OnPropertyChanged(nameof(Icon));
        OnPropertyChanged(nameof(Tags));
        OnPropertyChanged(nameof(Details));
        OnPropertyChanged(nameof(Section));
        OnPropertyChanged(nameof(TextToSuggest));
        OnPropertyChanged(nameof(MoreCommands));
        OnPropertyChanged(nameof(Command));
    }

    private sealed class DataBox
    {
        internal DataBox(JsonElement element) => Element = element;

        internal JsonElement Element { get; }
    }
}

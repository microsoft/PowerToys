// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Core.Common.Helpers;

#pragma warning disable SA1402 // File may only contain a single type

public partial class WrappedDockItem : CommandItem
{
    public override string Title => _itemTitle;

    public override IIconInfo? Icon => _icon;

    private readonly string _itemTitle;
    private readonly IIconInfo? _icon;

    public WrappedDockItem(
        ICommand command,
        string displayTitle)
    {
        Command = new WrappedDockList(command);
        _itemTitle = string.IsNullOrEmpty(displayTitle) ? command.Name : displayTitle;
        _icon = command.Icon;
    }

    public WrappedDockItem(
        ICommandItem item,
        string id,
        string displayTitle)
    {
        Command = new WrappedDockList(item, id);
        _itemTitle = string.IsNullOrEmpty(displayTitle) ? item.Title : displayTitle;
        _icon = item.Icon;
    }
}

public partial class PinnedDockItem : WrappedDockItem
{
    public override string Title => $"{base.Title} (Pinned)"; // TODO! localization

    public PinnedDockItem(ICommand command)
        : base(command, command.Name)
    {
    }

    public PinnedDockItem(ICommandItem item, string id)
        : base(item, id, item.Title)
    {
    }
}

public partial class WrappedDockList : ListPage
{
    public override string Name => _command.Name;

    private string _id;

    public override string Id => _id;

    private ICommand _command;
    private IListItem[] _items;

    public WrappedDockList(ICommand command)
    {
        _command = command;
        _items = new IListItem[] { new ListItem(command) };
        Name = _command.Name;
        _id = _command.Id; // + "__DockBand";
    }

    public WrappedDockList(ICommandItem item, string id)
    {
        _command = item.Command;

        // TODO! This isn't _totally correct, because the wrapping item will not
        // listen for property changes on the inner item.
        _items = new IListItem[]
        {
            new ListItem(_command)
            {
                Title = item.Title,
                Subtitle = item.Subtitle,
                Icon = item.Icon,
                MoreCommands = item.MoreCommands,
            },
        };
        Name = _command.Name;
        _id = string.IsNullOrEmpty(id) ? _command.Id : id;
    }

    public override IListItem[] GetItems()
    {
        return _items;
    }
}

#pragma warning restore SA1402 // File may only contain a single type

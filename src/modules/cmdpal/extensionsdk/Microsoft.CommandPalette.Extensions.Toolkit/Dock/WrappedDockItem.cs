// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

#pragma warning disable SA1402 // File may only contain a single type

public partial class WrappedDockItem : CommandItem
{
    public override string Title => _itemTitle;

    public override IIconInfo? Icon => _icon;

    public override ICommand? Command => _backingList;

    private readonly string _itemTitle;
    private readonly IIconInfo? _icon;
    private readonly WrappedDockList _backingList;

    public IListItem[] Items { get => _backingList.GetItems(); set => _backingList.SetItems(value); }

    public WrappedDockItem(
        ICommand command,
        string displayTitle)
    {
        _backingList = new WrappedDockList(command);
        _itemTitle = string.IsNullOrEmpty(displayTitle) ? command.Name : displayTitle;
        _icon = command.Icon;
    }

    public WrappedDockItem(
        ICommandItem item,
        string id,
        string displayTitle)
    {
        _backingList = new WrappedDockList(item, id);
        _itemTitle = string.IsNullOrEmpty(displayTitle) ? item.Title : displayTitle;
        _icon = item.Icon;
    }

    public WrappedDockItem(IListItem[] items, string id, string displayTitle)
    {
        _backingList = new WrappedDockList(items, id, displayTitle);
        _itemTitle = displayTitle;
    }
}

public partial class WrappedDockList : ListPage
{
    private string _id;

    public override string Id => _id;

    // private ICommand _command;
    private List<IListItem> _items;

    public WrappedDockList(ICommand command)
    {
        // _command = command;
        _items = new() { new ListItem(command) };
        Name = command.Name;
        _id = command.Id;
    }

    public WrappedDockList(ICommandItem item, string id)
    {
        var command = item.Command;

        // TODO! This isn't _totally correct, because the wrapping item will not
        // listen for property changes on the inner item.
        _items = new()
        {
            new ListItem(command)
            {
                Title = item.Title,
                Subtitle = item.Subtitle,
                Icon = item.Icon,
                MoreCommands = item.MoreCommands,
            },
        };
        Name = command.Name;
        _id = string.IsNullOrEmpty(id) ? command.Id : id;
    }

    public WrappedDockList(IListItem[] items, string id, string name)
    {
        _items = new(items);
        Name = name;
        _id = id;
    }

    public WrappedDockList(ICommand[] items, string id, string name)
    {
        _items = new();
        foreach (var item in items)
        {
            _items.Add(new ListItem(item));
        }

        Name = name;
        _id = id;
    }

    public override IListItem[] GetItems()
    {
        return _items.ToArray();
    }

    internal void SetItems(IListItem[]? newItems)
    {
        if (newItems == null)
        {
            _items = [];
            RaiseItemsChanged(0);
            return;
        }

        ListHelpers.InPlaceUpdateList(_items, newItems);
        RaiseItemsChanged(_items.Count);
    }
}

#pragma warning restore SA1402 // File may only contain a single type

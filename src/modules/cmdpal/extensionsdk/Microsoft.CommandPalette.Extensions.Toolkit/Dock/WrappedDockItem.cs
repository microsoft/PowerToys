// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

#pragma warning disable SA1402 // File may only contain a single type

/// <summary>
/// Helper class for creating a band out of a set of items. This allows you to
/// simply just instantiate a set of buttons as ListItems, then pass them in to
/// this class to create a band from those items. For example:
///
/// ```cs
/// var foo = new MyFooListItem();
/// var bar = new MyBarListItem();
/// var band = new WrappedDockItem([foo, bar], "com.me.myBand", "My cool desk band");
/// ```
/// </summary>
public partial class WrappedDockItem : CommandItem
{
    public override string Title => _itemTitle;

    public override IIconInfo? Icon
    {
        get => _icon; set { _icon = value; }
    }

    public override ICommand? Command => _backingList;

    private readonly string _itemTitle;
    private readonly WrappedDockList _backingList;
    private IIconInfo? _icon;

    public IListItem[] Items { get => _backingList.GetItems(); set => _backingList.SetItems(value); }

    public WrappedDockItem(
        ICommand command,
        string displayTitle)
    {
        _backingList = new WrappedDockList(command);
        _itemTitle = string.IsNullOrEmpty(displayTitle) ? command.Name : displayTitle;
        _icon = command.Icon;
    }

    // This was too much of a footgun - we'd internally create a ListItem that
    // didn't bubble the prop change events back up. That was bad.
    // public WrappedDockItem(
    //    ICommandItem item,
    //    string id,
    //    string displayTitle)
    // {
    //    _backingList = new WrappedDockList(item, id);
    //    _itemTitle = string.IsNullOrEmpty(displayTitle) ? item.Title : displayTitle;
    //    _icon = item.Icon;
    // }

    /// <summary>
    /// Initializes a new instance of the <see cref="WrappedDockItem"/> class.
    /// Create a new dock band for a set of list items
    /// </summary>
    public WrappedDockItem(
        IListItem[] items,
        string id,
        string displayTitle)
    {
        _backingList = new WrappedDockList(items, id, displayTitle);
        _itemTitle = displayTitle;
    }
}

/// <summary>
/// Helper class for a list page that just holds a set of items as a band.
/// The page itself doesn't do anything interesting.
/// </summary>
internal sealed partial class WrappedDockList : ListPage
{
    private string _id;

    public override string Id => _id;

    private List<IListItem> _items;

    internal WrappedDockList(ICommand command)
    {
        _items = new() { new ListItem(command) };
        Name = command.Name;
        _id = command.Id;
    }

    // Maybe revisit sometime.
    // The hard problem is that  the wrapping item will not
    // listen for property changes on the inner item.
    // public WrappedDockList(ICommandItem item, string id)
    // {
    //    var command = item.Command;
    //    _items = new()
    //    {
    //        new ListItem(command)
    //        {
    //            Title = item.Title,
    //            Subtitle = item.Subtitle,
    //            Icon = item.Icon,
    //            MoreCommands = item.MoreCommands,
    //        },
    //    };
    //    Name = command.Name;
    //    _id = string.IsNullOrEmpty(id) ? command.Id : id;
    // }

    /// <summary>
    /// Initializes a new instance of the <see cref="WrappedDockList"/> class.
    /// Create a new list page for the set of items provided.
    /// </summary>
    internal WrappedDockList(IListItem[] items, string id, string name)
    {
        _items = new(items);
        Name = name;
        _id = id;
    }

    internal WrappedDockList(ICommand[] items, string id, string name)
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

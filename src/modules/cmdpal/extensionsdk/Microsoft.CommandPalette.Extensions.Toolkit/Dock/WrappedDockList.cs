// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

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

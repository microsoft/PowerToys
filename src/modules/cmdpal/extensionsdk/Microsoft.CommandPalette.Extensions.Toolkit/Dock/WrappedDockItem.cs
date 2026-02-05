// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

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

    // This was too much of a foot gun - we'd internally create a ListItem that
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

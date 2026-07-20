// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.Ext.Bookmarks.Pages;

/// <summary>
/// Specialized version of the wrapper that updates its title and icon after the underlying <see cref="BookmarkListItem"/>
/// changes, because the BookmarkListItem loads lazily and the title and icon may not be available when the item is first created.
/// </summary>
internal sealed partial class BookmarkDockItem : WrappedDockItem, IDisposable
{
    private readonly BookmarkListItem _item;
    private string _displayTitle;

    public override string Title => _displayTitle;

    public BookmarkDockItem(BookmarkListItem item, string id)
        : base([item], id, item.BookmarkTitle)
    {
        _item = item;
        _displayTitle = item.BookmarkTitle;
        Icon = Icons.BookmarksExtensionIcon;
        _item.PropChanged += Item_PropChanged;
    }

    public void Dispose()
    {
        _item.PropChanged -= Item_PropChanged;
        _item.Dispose();
    }

    private void Item_PropChanged(object sender, IPropChangedEventArgs args)
    {
        switch (args.PropertyName)
        {
            case nameof(BookmarkListItem.Title) or nameof(BookmarkListItem.BookmarkTitle):
                UpdateTitle();
                break;
            case nameof(Icon):
                UpdateIcon();
                break;
        }
    }

    private void UpdateTitle()
    {
        var title = string.IsNullOrEmpty(_item.Title) ? _item.BookmarkTitle : _item.Title;
        if (_displayTitle != title)
        {
            _displayTitle = title;
            OnPropertyChanged(nameof(Title));
        }
    }

    private void UpdateIcon()
    {
        // Intentionally excluding reloading icon to keep the UI simple.
        var icon = _item.Icon ?? _item.Command?.Icon;
        Icon = icon is null || ReferenceEquals(icon, Icons.Reloading)
            ? Icons.BookmarksExtensionIcon
            : icon;
    }
}

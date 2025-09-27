// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Core.Common.Helpers;
using Microsoft.CmdPal.Ext.Bookmarks.Helpers;
using Microsoft.CmdPal.Ext.Bookmarks.Persistence;
using Microsoft.CmdPal.Ext.Bookmarks.Services;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.Ext.Bookmarks.Pages;

internal sealed partial class BookmarkPlaceholderPage : ContentPage, IDisposable
{
    private readonly FormContent _bookmarkPlaceholder;
    private readonly SupersedingAsyncValueGate<IIconInfo?> _iconReloadGate;

    public BookmarkPlaceholderPage(BookmarkData bookmarkData, IBookmarkIconLocator iconLocator, IBookmarkResolver resolver, IPlaceholderParser placeholderParser)
    {
        Name = Resources.bookmarks_command_name_open;
        Id = CommandIds.GetLaunchBookmarkItemId(bookmarkData.Id);

        _bookmarkPlaceholder = new BookmarkPlaceholderForm(bookmarkData, resolver, placeholderParser);

        _iconReloadGate = new(
            async ct =>
            {
                var c = resolver.ClassifyOrUnknown(bookmarkData.Bookmark);
                return await iconLocator.GetIconForPath(c, ct);
            },
            icon =>
            {
                Icon = icon as IconInfo ?? Icons.PinIcon;
            });
        RequestIconReloadAsync();
    }

    public override IContent[] GetContent() => [_bookmarkPlaceholder];

    private void RequestIconReloadAsync()
    {
        Icon = Icons.Reloading;
        OnPropertyChanged(nameof(Icon));
        _ = _iconReloadGate.ExecuteAsync();
    }

    public void Dispose() => _iconReloadGate.Dispose();
}

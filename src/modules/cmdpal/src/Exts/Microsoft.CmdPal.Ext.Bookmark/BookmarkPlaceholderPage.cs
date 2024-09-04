// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Windows.CommandPalette.Extensions;
using Microsoft.Windows.CommandPalette.Extensions.Helpers;

namespace Run.Bookmarks;

internal sealed class BookmarkPlaceholderPage : FormPage
{
    private readonly IForm _bookmarkPlaceholder;

    public override IForm[] Forms() => [_bookmarkPlaceholder];

    public BookmarkPlaceholderPage(string name, string url, string type)
    {
        _Name = name;
        Icon = new(UrlAction.IconFromUrl(url, type));
        _bookmarkPlaceholder = new BookmarkPlaceholderForm(name, url, type);
    }
}

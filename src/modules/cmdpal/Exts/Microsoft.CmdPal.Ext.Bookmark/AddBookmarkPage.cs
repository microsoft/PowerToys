// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Foundation;

namespace Microsoft.CmdPal.Ext.Bookmarks;

internal sealed partial class AddBookmarkPage : FormPage
{
    private readonly AddBookmarkForm _addBookmark = new();

    internal event TypedEventHandler<object, object?>? AddedCommand
    {
        add => _addBookmark.AddedCommand += value;
        remove => _addBookmark.AddedCommand -= value;
    }

    public override IForm[] Forms() => [_addBookmark];

    public AddBookmarkPage()
    {
        this.Icon = new IconInfo("\ued0e");
        this.Name = "Add a Bookmark";
    }
}

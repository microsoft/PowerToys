// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;
using Windows.Foundation;

namespace Microsoft.CmdPal.Ext.Bookmarks;

internal sealed class AddBookmarkPage : FormPage
{
    private readonly AddBookmarkForm _addBookmark = new();

    internal event TypedEventHandler<object, object?>? AddedAction
    {
        add => _addBookmark.AddedAction += value;
        remove => _addBookmark.AddedAction -= value;
    }

    public override IForm[] Forms() => [_addBookmark];

    public AddBookmarkPage()
    {
        this.Icon = new("\ued0e");
        this.Name = "Add a Bookmark";
    }
}

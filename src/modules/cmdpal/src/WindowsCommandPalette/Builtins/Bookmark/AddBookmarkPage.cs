// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Windows.CommandPalette.Extensions;
using Microsoft.Windows.CommandPalette.Extensions.Helpers;
using Windows.Foundation;
using Windows.System;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Run.Bookmarks;

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

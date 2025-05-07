// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CmdPal.Ext.Bookmarks.Models;

namespace Microsoft.CmdPal.Ext.Bookmarks.Helpers;

public static partial class BookmarkTypeHelper
{
    /*
     * Summary:
     * If bookmark has a space, we assume it's a shell command. eg: "python test.py" or "test.ps1 /C /D"
     * Ok fine, we can ensure the bookmark don't have spaces now.
     * So:
     * 1. Check if it follow such format 'COMMAND ARGS'
     * 2. Check if it's a valid url.
     * 3. Check if it's a existing folder or file.
     * By default, we assume it's Web Link.
     */

    public static BookmarkType GetBookmarkTypeFromValue(string bookmark)
    {
        // judge if the bookmark is a url
        if (Uri.TryCreate(bookmark, UriKind.Absolute, out var uriResult))
        {
            if (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps)
            {
                return BookmarkType.Web;
            }
        }

        // judge if the bookmark is a existing folder
        if (System.IO.Directory.Exists(bookmark))
        {
            return BookmarkType.Folder;
        }

        // ok, fine. Actually, it's also have the possibility to be a shell command.
        // Such as 'test.cmd' or 'test.ps1'. Try to catch this case.
        if (System.IO.File.Exists(bookmark))
        {
            return BookmarkType.File;
        }

        // by default. we assume it's a command type
        return BookmarkType.Command;
    }
}

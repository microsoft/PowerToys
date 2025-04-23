// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CmdPal.Ext.Bookmarks.Models;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.UI.Xaml;

namespace Microsoft.CmdPal.Ext.Bookmarks.Helpers;

public static class IconHelper
{
    public static IconInfo DeleteIcon { get; private set; } = new("\uE74D"); // Delete

    public static IconInfo EditIcon { get; private set; } = new("\uE70F"); // Edit

    public static IconInfo UrlIcon { get; private set; } = new("🔗"); // Web

    public static IconInfo FolderIcon { get; private set; } = new("📁"); // Folder

    public static IconInfo FileIcon { get; private set; } = new("📄"); // File

    public static IconInfo CmdIcon { get; private set; } = new("\uE756"); // Cmd

    public static IconInfo PWSHIcon { get; private set; } = new("\uE756"); // PWSH

    public static IconInfo PowerShellIcon { get; private set; } = new("\uE756"); // PowerShell

    public static IconInfo PythonIcon { get; private set; } = new("\uE756"); // Python

    public static IconInfo Python3Icon { get; private set; } = new("\uE756"); // Python3

    public static IconInfo GetIconByType(BookmarkType type)
    {
        return type switch
        {
            BookmarkType.Web => UrlIcon,
            BookmarkType.Folder => FolderIcon,
            BookmarkType.File => FileIcon,
            BookmarkType.Cmd => CmdIcon,
            BookmarkType.PWSH => PWSHIcon,
            BookmarkType.PowerShell => PowerShellIcon,
            BookmarkType.Python => PythonIcon,
            BookmarkType.Ptyhon3 => Python3Icon,

            _ => UrlIcon, // Default icon
        };
    }

    public static IconInfo CreateIcon(string bookmark, BookmarkType bookmarkType)
    {
        if (bookmarkType == BookmarkType.Web)
        {
            // Get the base url up to the first placeholder
            var placeholderIndex = bookmark.IndexOf('{');
            var baseString = placeholderIndex > 0 ? bookmark.Substring(0, placeholderIndex) : bookmark;
            try
            {
                var uri = UrlCommand.GetUri(baseString);
                if (uri != null)
                {
                    var hostname = uri.Host;
                    var faviconUrl = $"{uri.Scheme}://{hostname}/favicon.ico";
                    return new IconInfo(faviconUrl);
                }
            }
            catch (UriFormatException)
            {
                // return "🔗";
            }

            return GetIconByType(bookmarkType);
        }

        return GetIconByType(bookmarkType);
    }
}

// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CmdPal.Ext.Bookmarks.Models;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Storage.Streams;

namespace Microsoft.CmdPal.Ext.Bookmarks.Helpers;

public static class IconHelper
{
    public static IconInfo DeleteIcon { get; private set; } = new("\uE74D"); // Delete

    public static IconInfo EditIcon { get; private set; } = new("\uE70F"); // Edit

    public static IconInfo UrlIcon { get; private set; } = new("🔗"); // Web

    public static IconInfo FolderIcon { get; private set; } = new("📁"); // Folder

    public static IconInfo FileIcon { get; private set; } = new("📄"); // File

    public static IconInfo CommandIcon { get; private set; } = new("\uE756"); // Command

    public static IconInfo GetIconByType(BookmarkType type)
    {
        return type switch
        {
            BookmarkType.Web => UrlIcon,
            BookmarkType.Folder => FolderIcon,
            BookmarkType.File => FileIcon,
            BookmarkType.Command => CommandIcon,

            _ => UrlIcon, // Default icon
        };
    }

    public static IconInfo CreateIcon(string bookmark, BookmarkType bookmarkType, bool isPlaceholde)
    {
        // In some case, we want to use placeholder, but we can still get the favicon.
        // eg: "https://google.com?q={query}"
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
            catch (UriFormatException ex)
            {
                ExtensionHost.LogMessage(new LogMessage() { Message = $"Create Icon failed. {ex}: {ex.Message}" });
            }
        }

        if (isPlaceholde)
        {
            // If it's a placeholder bookmark, we don't need to get the icon.
            // Just use the default icon.
            return GetIconByType(bookmarkType);
        }

        if (bookmarkType == BookmarkType.File || bookmarkType == BookmarkType.Folder)
        {
            // try to get the file icon first. If not, use the default file icon.
            try
            {
                // To be honest, I don't like to block thread.
                // We need to refactor in the future.
                // But now, it's ok. Only image file will trigger the async path.
                var stream = ThumbnailHelper.GetThumbnail(bookmark).Result;
                if (stream != null)
                {
                    var data = new IconData(RandomAccessStreamReference.CreateFromStream(stream));
                    return new IconInfo(data, data);
                }
            }
            catch
            {
                ExtensionHost.LogMessage(new LogMessage() { Message = $"Create Icon failed. {bookmarkType}: {bookmark}" });
            }
        }

        // If we can't get the icon, just use the default icon.
        return GetIconByType(bookmarkType);
    }
}

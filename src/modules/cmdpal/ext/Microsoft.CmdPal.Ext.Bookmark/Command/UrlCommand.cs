// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CmdPal.Ext.Bookmarks.Helpers;
using Microsoft.CmdPal.Ext.Bookmarks.Models;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.System;

namespace Microsoft.CmdPal.Ext.Bookmarks;

public partial class UrlCommand : InvokableCommand
{
    public BookmarkType Type { get; }

    public string Url { get; }

    public UrlCommand(BookmarkData data)
        : this(data.Name, data.Bookmark, data.Type)
    {
    }

    public UrlCommand(string name, string url, BookmarkType type)
    {
        Name = name;
        Type = type;
        Url = url;
        Icon = IconHelper.CreateIcon(url, type, false);
    }

    public override CommandResult Invoke()
    {
        return UrlCommand.Invoke(Url);
    }

    public static CommandResult Invoke(string url)
    {
        var target = url;
        try
        {
            var uri = GetUri(target);
            if (uri != null)
            {
                _ = Launcher.LaunchUriAsync(uri);
            }
            else
            {
                // throw new UriFormatException("The provided URL is not valid.");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error launching URL: {ex.Message}");
        }

        return CommandResult.Dismiss();
    }

    internal static Uri? GetUri(string url)
    {
        Uri? uri;
        if (!Uri.TryCreate(url, UriKind.Absolute, out uri))
        {
            if (!Uri.TryCreate("https://" + url, UriKind.Absolute, out uri))
            {
                return null;
            }
        }

        return uri;
    }
}

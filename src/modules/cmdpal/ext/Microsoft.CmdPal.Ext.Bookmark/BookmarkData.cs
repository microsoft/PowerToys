// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json.Serialization;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Bookmarks;

public class BookmarkData
{
    public string Name { get; set; } = string.Empty;

    public string Bookmark { get; set; } = string.Empty;

    // public string Type { get; set; } = string.Empty;
    [JsonIgnore]
    public bool IsPlaceholder => Bookmark.Contains('{') && Bookmark.Contains('}');

    internal void GetExeAndArgs(out string exe, out string args)
    {
        ShellHelpers.ParseExecutableAndArgs(Bookmark, out exe, out args);
    }

    internal bool IsWebUrl()
    {
        GetExeAndArgs(out var exe, out var args);
        if (string.IsNullOrEmpty(exe))
        {
            return false;
        }

        if (Uri.TryCreate(exe, UriKind.Absolute, out var uri))
        {
            if (uri.Scheme == Uri.UriSchemeFile)
            {
                return false;
            }

            // return true if the scheme is http or https, or if there's no scheme (e.g., "www.example.com") but there is a dot in the host
            return
                uri.Scheme == Uri.UriSchemeHttp ||
                uri.Scheme == Uri.UriSchemeHttps ||
                (string.IsNullOrEmpty(uri.Scheme) && uri.Host.Contains('.'));
        }

        // If we can't parse it as a URI, we assume it's not a web URL
        return false;
    }
}

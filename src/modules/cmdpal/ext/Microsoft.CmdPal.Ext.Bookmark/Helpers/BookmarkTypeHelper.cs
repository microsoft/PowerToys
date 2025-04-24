// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Nodes;
using Microsoft.CmdPal.Ext.Bookmarks.Models;
using Microsoft.CmdPal.Ext.Bookmarks.Properties;

namespace Microsoft.CmdPal.Ext.Bookmarks.Helpers;

public static partial class BookmarkTypeHelper
{
    public static string GetBookmarkChoices()
    {
        JsonArray bookmarkChoices = new JsonArray
        {
            new JsonObject
            {
                ["title"] = Resources.bookmarks_form_bookmark_type_web,
                ["value"] = BookmarkType.Web.ToString(),
            },
            new JsonObject
            {
                ["title"] = Resources.bookmarks_form_bookmark_type_File,
                ["value"] = BookmarkType.File.ToString(),
            },
            new JsonObject
            {
                ["title"] = Resources.bookmarks_form_bookmark_type_Folder,
                ["value"] = BookmarkType.Folder.ToString(),
            },
        };

        if (EnvironmentsCache.Instance.TryGetExecutableFileFullPath("pwsh.exe", out _))
        {
            bookmarkChoices.Add(new JsonObject
            {
                ["title"] = Resources.bookmarks_form_bookmark_type_PWSH,
                ["value"] = BookmarkType.PWSH.ToString(),
            });
        }

        if (EnvironmentsCache.Instance.TryGetExecutableFileFullPath("powershell.exe", out _))
        {
            bookmarkChoices.Add(new JsonObject
            {
                ["title"] = Resources.bookmarks_form_bookmark_type_PowerShell,
                ["value"] = BookmarkType.PowerShell.ToString(),
            });
        }

        if (EnvironmentsCache.Instance.TryGetExecutableFileFullPath("cmd.exe", out _))
        {
            bookmarkChoices.Add(new JsonObject
            {
                ["title"] = Resources.bookmarks_form_bookmark_type_CMD,
                ["value"] = BookmarkType.Cmd.ToString(),
            });
        }

        if (EnvironmentsCache.Instance.TryGetExecutableFileFullPath("python.exe", out _))
        {
            bookmarkChoices.Add(new JsonObject
            {
                ["title"] = Resources.bookmarks_form_bookmark_type_Python,
                ["value"] = BookmarkType.Python.ToString(),
            });
        }

        if (EnvironmentsCache.Instance.TryGetExecutableFileFullPath("python3.exe", out _))
        {
            bookmarkChoices.Add(new JsonObject
            {
                ["title"] = Resources.bookmarks_form_bookmark_type_Python3,
                ["value"] = BookmarkType.Python3.ToString(),
            });
        }

        return bookmarkChoices.ToJsonString();
    }
}

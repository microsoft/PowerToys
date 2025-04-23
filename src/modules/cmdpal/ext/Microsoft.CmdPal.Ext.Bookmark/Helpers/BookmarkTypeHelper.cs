// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.Bookmarks.Models;

namespace Microsoft.CmdPal.Ext.Bookmarks.Helpers;

public static partial class BookmarkTypeHelper
{
    public static string GetBookmarkChoices()
    {
        JsonArray bookmarkChoices = new JsonArray
        {
            new JsonObject
            {
                ["title"] = "Web",
                ["value"] = BookmarkType.Web.ToString(),
            },
            new JsonObject
            {
                ["title"] = "File",
                ["value"] = BookmarkType.File.ToString(),
            },
            new JsonObject
            {
                ["title"] = "Folder",
                ["value"] = BookmarkType.Folder.ToString(),
            },
        };

        if (EnvironmentsCache.Instance.TryGetExecutableFileFullPath("pwsh.exe", out _))
        {
            bookmarkChoices.Add(new JsonObject
            {
                ["title"] = "pwsh",
                ["value"] = BookmarkType.PWSH.ToString(),
            });
        }

        if (EnvironmentsCache.Instance.TryGetExecutableFileFullPath("powershell.exe", out _))
        {
            bookmarkChoices.Add(new JsonObject
            {
                ["title"] = "Windows PowerShell",
                ["value"] = BookmarkType.PowerShell.ToString(),
            });
        }

        if (EnvironmentsCache.Instance.TryGetExecutableFileFullPath("cmd.exe", out _))
        {
            bookmarkChoices.Add(new JsonObject
            {
                ["title"] = "Command Prompt",
                ["value"] = BookmarkType.Cmd.ToString(),
            });
        }

        if (EnvironmentsCache.Instance.TryGetExecutableFileFullPath("python.exe", out _))
        {
            bookmarkChoices.Add(new JsonObject
            {
                ["title"] = "Python",
                ["value"] = BookmarkType.Python.ToString(),
            });
        }

        if (EnvironmentsCache.Instance.TryGetExecutableFileFullPath("python3.exe", out _))
        {
            bookmarkChoices.Add(new JsonObject
            {
                ["title"] = "Python3",
                ["value"] = BookmarkType.Ptyhon3.ToString(),
            });
        }

        return bookmarkChoices.ToJsonString();
    }
}

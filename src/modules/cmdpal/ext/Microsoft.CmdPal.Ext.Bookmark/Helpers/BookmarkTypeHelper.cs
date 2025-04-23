// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

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
                ["value"] = "Web",
            },
            new JsonObject
            {
                ["title"] = "File",
                ["value"] = "File",
            },
            new JsonObject
            {
                ["title"] = "Folder",
                ["value"] = "Folder",
            },
        };

        if (EnvironmentsCache.Instance.TryGetExecutableFileFullPath("pwsh.exe", out _))
        {
            bookmarkChoices.Add(new JsonObject
            {
                ["title"] = "pwsh",
                ["value"] = "PWSH",
            });
        }

        if (EnvironmentsCache.Instance.TryGetExecutableFileFullPath("powershell.exe", out _))
        {
            bookmarkChoices.Add(new JsonObject
            {
                ["title"] = "Windows PowerShell",
                ["value"] = "PowerShell",
            });
        }

        if (EnvironmentsCache.Instance.TryGetExecutableFileFullPath("cmd.exe", out _))
        {
            bookmarkChoices.Add(new JsonObject
            {
                ["title"] = "Command Prompt",
                ["value"] = "Cmd",
            });
        }

        if (EnvironmentsCache.Instance.TryGetExecutableFileFullPath("python.exe", out _))
        {
            bookmarkChoices.Add(new JsonObject
            {
                ["title"] = "Python",
                ["value"] = "python",
            });
        }

        if (EnvironmentsCache.Instance.TryGetExecutableFileFullPath("python3.exe", out _))
        {
            bookmarkChoices.Add(new JsonObject
            {
                ["title"] = "Python3",
                ["value"] = "python3",
            });
        }

        return bookmarkChoices.ToJsonString();
    }
}

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
     * 1. Check if it's a valid url.
     * 2. Check if it's a existing folder.
     * 3. if it's a exsiting file, it also have the possibility to be a shell command file. eg: "test.cmd" or "test.ps1". So, check the extension. If not, assume it's a normal file.
     * By default, we assume it's Web Link.
     */

    public static BookmarkType GetBookmarkTypeFromValue(string bookmark)
    {
        var splittedBookmarkValue = bookmark.Split(" ");

        if (splittedBookmarkValue.Length > 1)
        {
            // absolutely it's a shell command
            // we don't need to check the file name
            var exectuableFileName = splittedBookmarkValue[0];
            var executableExtension = System.IO.Path.GetExtension(exectuableFileName);

            // if it's a cmd
            if (executableExtension == ".cmd" || executableExtension == ".bat")
            {
                return BookmarkType.Cmd;
            }

            // Otherwise, we assume it's a powershell or pwsh.
            // Prefer to use pwsh, but check if pwsh is installed
            // if not, we use powershell
            if (EnvironmentsCache.Instance.TryGetExecutableFileFullPath("pwsh.exe", out _))
            {
                return BookmarkType.PWSH;
            }

            return BookmarkType.PowerShell;
        }

        // judge if the bookmark is a url
        if (Uri.TryCreate(bookmark, UriKind.Absolute, out var uriResult))
        {
            if (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps)
            {
                return BookmarkType.Web;
            }
        }

        // judge if the bookmak is a existing folder
        if (System.IO.Directory.Exists(bookmark))
        {
            return BookmarkType.Folder;
        }

        // ok, fine. Actually, it's also have the possibility to be a shell command.
        // Such as 'test.cmd' or 'test.ps1'. Try to catch this case.
        if (System.IO.File.Exists(bookmark))
        {
            // get file name
            var extension = System.IO.Path.GetExtension(bookmark);
            switch (extension)
            {
                case ".ps1":
                case ".psm1":
                    // prefer pwsh.exe over powershell.exe
                    if (EnvironmentsCache.Instance.TryGetExecutableFileFullPath("pwsh.exe", out _))
                    {
                        return BookmarkType.PWSH;
                    }

                    return BookmarkType.PowerShell;
                case ".cmd":
                case ".bat":
                    return BookmarkType.Cmd;
            }

            return BookmarkType.File;
        }

        // by default. we assume the bookmark is a Web link
        return BookmarkType.Web;
    }
}

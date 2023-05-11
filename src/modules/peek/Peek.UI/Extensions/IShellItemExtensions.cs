// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Peek.Common.Helpers;
using Peek.Common.Models;

namespace Peek.UI.Extensions
{
    public static class IShellItemExtensions
    {
        public static IFileSystemItem ToIFileSystemItem(this IShellItem shellItem)
        {
            string path = string.Empty;
            try
            {
                path = shellItem.GetDisplayName(Windows.Win32.UI.Shell.SIGDN.SIGDN_FILESYSPATH);
            }
            catch (Exception ex)
            {
                // TODO: Handle cases that do not have a file system path like Recycle Bin.
                Logger.LogError("Getting path failed. " + ex.Message);
            }

            return File.Exists(path) ? new FileItem(path) : new FolderItem(path);
        }
    }
}

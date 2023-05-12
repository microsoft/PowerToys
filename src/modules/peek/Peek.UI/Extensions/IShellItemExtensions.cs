// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Runtime.CompilerServices;
using Peek.Common.Helpers;
using Peek.Common.Models;

namespace Peek.UI.Extensions
{
    public static class IShellItemExtensions
    {
        public static IFileSystemItem ToIFileSystemItem(this IShellItem shellItem)
        {
            string path = shellItem.GetPath();
            string name = shellItem.GetName();

            return File.Exists(path) ? new FileItem(path, name) : new FolderItem(path, name);
        }

        private static string GetPath(this IShellItem shellItem)
        {
            string path = string.Empty;
            try
            {
                path = shellItem.GetDisplayName(Windows.Win32.UI.Shell.SIGDN.SIGDN_FILESYSPATH);
            }
            catch (Exception ex)
            {
                // TODO: Handle cases that do not have a file system path like Recycle Bin.
                path = string.Empty;
                Logger.LogError("Getting path failed. " + ex.Message);
            }

            return path;
        }

        private static string GetName(this IShellItem shellItem)
        {
            string name = string.Empty;
            try
            {
                name = shellItem.GetDisplayName(Windows.Win32.UI.Shell.SIGDN.SIGDN_NORMALDISPLAY);
            }
            catch (Exception ex)
            {
                name = string.Empty;
                Logger.LogError("Getting path failed. " + ex.Message);
            }

            return name;
        }
    }
}

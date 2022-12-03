// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Peek.Common.Models;
using PeekUI.WASDK.Native;

namespace PeekUI.WASDK.Helpers
{
    public static class FileExplorerHelper
    {
        public static List<File> GetSelectedFileExplorerFiles()
        {
            var foregroundWindowHandle = NativeMethods.GetForegroundWindow();

            var selectedItems = new List<File>();
            var shell = new Shell32.Shell();
            foreach (SHDocVw.InternetExplorer window in shell.Windows())
            {
                if (window.HWND == (int)foregroundWindowHandle)
                {
                    Shell32.FolderItems items = ((Shell32.IShellFolderViewDual2)window.Document).SelectedItems();
                    if (items != null && items.Count > 0)
                    {
                        foreach (Shell32.FolderItem item in items)
                        {
                            selectedItems.Add(new File(item.Path));
                        }
                    }
                }
            }

            return selectedItems;
        }
    }
}

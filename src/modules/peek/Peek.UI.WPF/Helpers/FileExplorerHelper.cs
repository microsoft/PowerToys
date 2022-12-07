// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Peek.UI.Helpers
{
    public static class FileExplorerHelper
    {
        public static IEnumerable<string> GetSelectedItems(IntPtr handle)
        {
            var selectedItems = new List<string>();
            var shell = new Shell32.Shell();
            foreach (SHDocVw.InternetExplorer window in shell.Windows())
            {
                // TODO: figure out which window is the active explorer tab
                // https://github.com/microsoft/PowerToys/issues/22507
                if (window.HWND == (int)handle)
                {
                    Shell32.FolderItems items = ((Shell32.IShellFolderViewDual2)window.Document).SelectedItems();
                    if (items != null && items.Count > 0)
                    {
                        foreach (Shell32.FolderItem item in items)
                        {
                            selectedItems.Add(item.Path);
                        }
                    }
                }
            }

            return selectedItems;
        }
    }
}

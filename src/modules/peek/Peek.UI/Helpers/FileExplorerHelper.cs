// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Text;
using Peek.UI.Native;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace Peek.UI.Helpers
{
    public static class FileExplorerHelper
    {
        public static Shell32.FolderItems? GetSelectedItems()
        {
            var folderView = GetCurrentFolderView();
            if (folderView == null)
            {
                return null;
            }

            Shell32.FolderItems selectedItems = folderView.SelectedItems();
            if (selectedItems == null || selectedItems.Count == 0)
            {
                return null;
            }

            return selectedItems;
        }

        public static Shell32.IShellFolderViewDual2? GetCurrentFolderView()
        {
            var foregroundWindowHandle = NativeMethods.GetForegroundWindow();

            int capacity = PInvoke.GetWindowTextLength(new HWND(foregroundWindowHandle)) * 2;
            StringBuilder foregroundWindowTitleBuffer = new StringBuilder(capacity);
            NativeMethods.GetWindowText(new HWND(foregroundWindowHandle), foregroundWindowTitleBuffer, foregroundWindowTitleBuffer.Capacity);

            string foregroundWindowTitle = foregroundWindowTitleBuffer.ToString();

            var shell = new Shell32.Shell();
            foreach (SHDocVw.InternetExplorer window in shell.Windows())
            {
                var shellFolderView = (Shell32.IShellFolderViewDual2)window.Document;
                var folderTitle = shellFolderView.Folder.Title;

                if (window.HWND == (int)foregroundWindowHandle && folderTitle == foregroundWindowTitle)
                {
                    return shellFolderView;
                }
            }

            return null;
        }
    }
}

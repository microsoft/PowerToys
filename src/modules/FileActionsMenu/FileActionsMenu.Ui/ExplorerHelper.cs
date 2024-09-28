// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Navigation;
using FileActionsMenu.Helpers;
using Peek.Common.Models;
using Peek.Helpers;
using Peek.Helpers.Extensions;
using Windows.Win32.Foundation;

namespace FileActionsMenu.Ui.Helpers
{
    public sealed partial class ExplorerHelper
    {
        [LibraryImport("user32.dll")]
        private static partial IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int GetWindowTextW(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        /// <summary>
        /// Gets the selected items in the active Windows Explorer window.
        /// </summary>
        /// <returns>An array of paths of the selected items.</returns>
        public static string[] GetSelectedItems()
        {
            List<string> selected = [];
            IShellItemArray? itemArray = default;
            if (((HWND)GetForegroundWindow()).IsDesktopWindow())
            {
                itemArray = FileExplorerHelper.GetItemsFromDesktop((HWND)GetForegroundWindow(), true);
            }
            else
            {
                // Source: https://stackoverflow.com/questions/14193388/how-to-get-windows-explorers-selected-files-from-within-c
                string filename;

                foreach (SHDocVw.InternetExplorer window in new SHDocVw.ShellWindows())
                {
                    filename = Path.GetFileNameWithoutExtension(window.FullName).ToLower(CultureInfo.InvariantCulture);
                    if (filename.Equals("explorer", StringComparison.OrdinalIgnoreCase) && window.HWND == GetForegroundWindow())
                    {
                        checked
                        {
                            itemArray = FileExplorerHelper.GetSelectedItems(new HWND((IntPtr)window.HWND));
                        }
                    }
                }
            }

            checked
            {
                if (itemArray is null || itemArray.GetCount() == 0)
                {
                    return [];
                }
            }

            try
            {
                for (int i = 0; i < itemArray.GetCount(); i++)
                {
                    IShellItem item = itemArray.GetItemAt(i);
                    selected.Add(item.GetDisplayName(Windows.Win32.UI.Shell.SIGDN.SIGDN_FILESYSPATH));
                }
            }
            catch (Exception)
            {
                MessageBox.Show(ResourceHelper.GetResource("InvalidExplorerItem"), ResourceHelper.GetResource("Error"), MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return [.. selected];
        }
    }
}

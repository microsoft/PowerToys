// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using Peek.Common.Models;
using Peek.UI.Extensions;
using SHDocVw;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;
using static System.Runtime.InteropServices.JavaScript.JSType;
using IServiceProvider = Peek.Common.Models.IServiceProvider;

namespace Peek.UI.Helpers
{
    public static class FileExplorerHelper
    {
        public static IEnumerable<IFileSystemItem> GetSelectedItems()
        {
            return GetItemsInternal(onlySelectedFiles: true);
        }

        public static IEnumerable<IFileSystemItem> GetItems()
        {
            return GetItemsInternal(onlySelectedFiles: false);
        }

        private static IEnumerable<IFileSystemItem> GetItemsInternal(bool onlySelectedFiles)
        {
            var foregroundWindowHandle = PInvoke.GetForegroundWindow();
            if (foregroundWindowHandle.IsDesktopWindow())
            {
                return GetItemsFromDesktop(foregroundWindowHandle, onlySelectedFiles);
            }
            else
            {
                return GetItemsFromFileExplorer(foregroundWindowHandle, onlySelectedFiles);
            }
        }

        private static IEnumerable<IFileSystemItem> GetItemsFromDesktop(HWND foregroundWindowHandle, bool onlySelectedFiles)
        {
            const int SWC_DESKTOP = 8;
            const int SWFO_NEEDDISPATCH = 1;

            var shell = new Shell32.Shell();
            ShellWindows shellWindows = shell.Windows();
            object? oNull1 = null;
            object? oNull2 = null;
            var serviceProvider = (IServiceProvider)shellWindows.FindWindowSW(ref oNull1, ref oNull2, SWC_DESKTOP, out int pHWND, SWFO_NEEDDISPATCH);
            var shellBrowser = (IShellBrowser)serviceProvider.QueryService(PInvoke.SID_STopLevelBrowser, typeof(IShellBrowser).GUID);
            var shellView = (IFolderView)shellBrowser.QueryActiveShellView();

            var selectionFlag = onlySelectedFiles ? (uint)_SVGIO.SVGIO_SELECTION : (uint)_SVGIO.SVGIO_ALLVIEW;
            shellView.Items(selectionFlag, typeof(IShellItemArray).GUID, out var items);
            if (items is IShellItemArray array)
            {
                return array.ToEnumerable();
            }

            return new List<IFileSystemItem>();
        }

        private static IEnumerable<IFileSystemItem> GetItemsFromFileExplorer(HWND foregroundWindowHandle, bool onlySelectedFiles)
        {
            var activeTab = foregroundWindowHandle.GetActiveTab();

            var shell = new Shell32.Shell();
            ShellWindows shellWindows = shell.Windows();
            foreach (IWebBrowserApp webBrowserApp in shell.Windows())
            {
                var shellFolderView = (Shell32.IShellFolderViewDual2)webBrowserApp.Document;
                var folderTitle = shellFolderView.Folder.Title;

                if (webBrowserApp.HWND == foregroundWindowHandle)
                {
                    var serviceProvider = (IServiceProvider)webBrowserApp;
                    var shellBrowser = (IShellBrowser)serviceProvider.QueryService(PInvoke.SID_STopLevelBrowser, typeof(IShellBrowser).GUID);
                    shellBrowser.GetWindow(out IntPtr shellBrowserHandle);

                    if (activeTab == shellBrowserHandle)
                    {
                        var items = onlySelectedFiles ? shellFolderView.SelectedItems() : shellFolderView.Folder?.Items();
                        return items != null ? items.ToEnumerable() : new List<IFileSystemItem>();
                    }
                }
            }

            return new List<IFileSystemItem>();
        }

        private static IEnumerable<IFileSystemItem> ToEnumerable(this IShellItemArray array)
        {
            for (var i = 0; i < array.GetCount(); i++)
            {
                IShellItem item = array.GetItemAt(i);
                string path = string.Empty;
                try
                {
                    path = item.GetDisplayName(SIGDN.SIGDN_FILESYSPATH);
                }
                catch (Exception)
                {
                    // TODO: Handle cases that do not have a file system path like Recycle Bin.
                }

                yield return File.Exists(path) ? new FileItem(path) : new FolderItem(path);
            }
        }

        private static IEnumerable<IFileSystemItem> ToEnumerable(this Shell32.FolderItems folderItems)
        {
            foreach (Shell32.FolderItem item in folderItems)
            {
                // TODO: Handle cases where it is neither a file or a folder
                yield return File.Exists(item.Path) ? new FileItem(item.Path) : new FolderItem(item.Path);
            }
        }
    }
}

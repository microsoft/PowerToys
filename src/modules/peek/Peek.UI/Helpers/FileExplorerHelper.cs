// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Peek.Common.Models;
using Peek.UI.Extensions;
using SHDocVw;
using Shell32;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;
using static Peek.Common.Models.PropertyStoreShellApi;
using IServiceProvider = Peek.Common.Models.IServiceProvider;

namespace Peek.UI.Helpers
{
    public static class FileExplorerHelper
    {
        public static IEnumerable<File> GetSelectedItems()
        {
            return GetItemsInternal(onlySelectedFiles: true);
        }

        public static IEnumerable<File> GetItems()
        {
            return GetItemsInternal(onlySelectedFiles: false);
        }

        private static IEnumerable<File> GetItemsInternal(bool onlySelectedFiles)
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

        private static IEnumerable<File> GetItemsFromDesktop(HWND foregroundWindowHandle, bool onlySelectedFiles)
        {
            const int SWC_DESKTOP = 8;
            const int SWFO_NEEDDISPATCH = 1;

            var shell = new Shell();
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
                return array.ToFilesEnumerable();
            }

            return new List<File>();
        }

        private static IEnumerable<File> GetItemsFromFileExplorer(HWND foregroundWindowHandle, bool onlySelectedFiles)
        {
            var activeTab = foregroundWindowHandle.GetActiveTab();

            var shell = new Shell();
            ShellWindows shellWindows = shell.Windows();
            foreach (IWebBrowserApp webBrowserApp in shell.Windows())
            {
                var shellFolderView = (IShellFolderViewDual2)webBrowserApp.Document;
                var folderTitle = shellFolderView.Folder.Title;

                if (webBrowserApp.HWND == foregroundWindowHandle)
                {
                    var serviceProvider = (IServiceProvider)webBrowserApp;
                    var shellBrowser = (IShellBrowser)serviceProvider.QueryService(PInvoke.SID_STopLevelBrowser, typeof(IShellBrowser).GUID);
                    shellBrowser.GetWindow(out IntPtr shellBrowserHandle);

                    if (activeTab == shellBrowserHandle)
                    {
                        var items = onlySelectedFiles ? shellFolderView.SelectedItems() : shellFolderView.Folder?.Items();
                        return items != null ? items.ToFilesEnumerable() : new List<File>();
                    }
                }
            }

            return new List<File>();
        }

        private static IEnumerable<File> ToFilesEnumerable(this IShellItemArray array)
        {
            for (var i = 0; i < array.GetCount(); i++)
            {
                var item = array.GetItemAt(i);
                var path = item.GetDisplayName(SIGDN.FILESYSPATH);
                yield return new File(path);
            }
        }

        private static IEnumerable<File> ToFilesEnumerable(this FolderItems folderItems)
        {
            foreach (FolderItem item in folderItems)
            {
                yield return new File(item.Path);
            }
        }
    }

    [ComImport]
    [Guid("B63EA76D-1F85-456F-A19C-48159EFA858B")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IShellItemArray
    {
        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void BindToHandler([In, MarshalAs(UnmanagedType.Interface)] IntPtr pbc, [In] ref Guid rbhid, [In] ref Guid riid, out IntPtr ppvOut);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void GetPropertyStore([In] int flags, [In] ref Guid riid, out IntPtr ppv);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void GetPropertyDescriptionList([In] ref PropertyKey keyType, [In] ref Guid riid, out IntPtr ppv);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void GetAttributes([In] SIATTRIBFLAGS dwAttribFlags, [In] uint sfgaoMask, out uint psfgaoAttribs);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        int GetCount();

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        Common.Models.IShellItem GetItemAt(int dwIndex);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        void EnumItems([MarshalAs(UnmanagedType.Interface)] out IntPtr ppenumShellItems);
    }
}

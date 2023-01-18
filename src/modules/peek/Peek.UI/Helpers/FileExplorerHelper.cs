// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security;
using Peek.UI.Native;
using Windows.Win32;
using Windows.Win32.Foundation;
using static Peek.Common.Models.PropertyStoreShellApi;

namespace Peek.UI.Helpers
{
    public static class FileExplorerHelper
    {
        public static readonly Guid SIDSTopLevelBrowser = new Guid("4c96be40-915c-11cf-99d3-00aa004ae837");

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
            var activeTab = PInvoke.FindWindowEx(new HWND(foregroundWindowHandle), HWND.Null, "ShellTabWindowClass", null);
            if (activeTab == HWND.Null)
            {
                activeTab = PInvoke.FindWindowEx(new HWND(foregroundWindowHandle), HWND.Null, "TabWindowClass", null);
            }

            var shell = new Shell32.Shell();
            SHDocVw.ShellWindows shellWindows = shell.Windows();
            foreach (SHDocVw.IWebBrowserApp webBrowserApp in shell.Windows())
            {
                var shellFolderView = (Shell32.IShellFolderViewDual2)webBrowserApp.Document;
                var folderTitle = shellFolderView.Folder.Title;

                if (webBrowserApp.HWND == (int)foregroundWindowHandle)
                {
                    var serviceProvider = (IServiceProvider)webBrowserApp;
                    var shellBrowser = (IShellBrowser)serviceProvider.QueryService(SIDSTopLevelBrowser, typeof(IShellBrowser).GUID);
                    shellBrowser.GetWindow(out IntPtr shellBrowserHandle);

                    if (activeTab == shellBrowserHandle)
                    {
                        return shellFolderView;
                    }
                }
            }

            return null;
        }

        [Guid("6D5140C1-7436-11CE-8034-00AA006009FA")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IServiceProvider
        {
            [return: MarshalAs(UnmanagedType.IUnknown)]
            object QueryService([MarshalAs(UnmanagedType.LPStruct)] Guid service, [MarshalAs(UnmanagedType.LPStruct)] Guid riid);
        }

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("000214E2-0000-0000-C000-000000000046")]
        public interface IShellBrowser
        {
            void GetWindow(out IntPtr phwnd);

            void ContextSensitiveHelp(bool fEnterMode);

            void InsertMenusSB(IntPtr hmenuShared, IntPtr lpMenuWidths);

            void SetMenuSB(IntPtr hmenuShared, IntPtr holemenuRes, IntPtr hwndActiveObject);

            void RemoveMenusSB(IntPtr hmenuShared);

            void SetStatusTextSB(IntPtr pszStatusText);

            void EnableModelessSB(bool fEnable);

            void TranslateAcceleratorSB(IntPtr pmsg, ushort wID);

            void BrowseObject(IntPtr pidl, uint wFlags);

            void GetViewStateStream(uint grfMode, IntPtr ppStrm);

            void GetControlWindow(uint id, out IntPtr lpIntPtr);

            void SendControlMsg(uint id, uint uMsg, uint wParam, uint lParam, IntPtr pret);

            void QueryActiveShellView(ref IShellView ppshv);

            void OnViewWindowActive(IShellView ppshv);

            void SetToolbarItems(IntPtr lpButtons, uint nButtons, uint uFlags);
        }

        [ComImport]
        [Guid("000214E3-0000-0000-C000-000000000046")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [SuppressUnmanagedCodeSecurity]
        public interface IShellView
        {
        }
    }
}

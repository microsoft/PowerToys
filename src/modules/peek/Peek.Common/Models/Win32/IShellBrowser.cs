// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Peek.Common.Models
{
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214E2-0000-0000-C000-000000000046")]
    public interface IShellBrowser
    {
        void GetWindow(out IntPtr phwnd);

        void ContextSensitiveHelp(bool fEnterMode);

        void InsertMenusSB(IntPtr hmenuShared, IntPtr lpMenuWidths);

        void SetMenuSB(IntPtr hmenuShared, IntPtr holeMenuRes, IntPtr hwndActiveObject);

        void RemoveMenusSB(IntPtr hmenuShared);

        void SetStatusTextSB(IntPtr pszStatusText);

        void EnableModelessSB(bool fEnable);

        void TranslateAcceleratorSB(IntPtr pmsg, ushort wID);

        void BrowseObject(IntPtr pidl, uint wFlags);

        void GetViewStateStream(uint grfMode, IntPtr ppStrm);

        void GetControlWindow(uint id, out IntPtr lpIntPtr);

        void SendControlMsg(uint id, uint uMsg, uint wParam, uint lParam, IntPtr pret);

        [return: MarshalAs(UnmanagedType.IUnknown)]
        object QueryActiveShellView();

        void OnViewWindowActive(IShellView ppshv);

        void SetToolbarItems(IntPtr lpButtons, uint nButtons, uint uFlags);
    }
}

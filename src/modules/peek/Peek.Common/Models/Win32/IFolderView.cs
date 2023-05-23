// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Security;

namespace Peek.Common.Models
{
    [ComImport]
    [Guid("cde725b0-ccc9-4519-917e-325d72fab4ce")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [SuppressUnmanagedCodeSecurity]
    public interface IFolderView
    {
        void GetCurrentViewMode([Out] out uint pViewMode);

        void SetCurrentViewMode([In] uint viewMode);

        void GetFolder([In, MarshalAs(UnmanagedType.LPStruct)] Guid riid, [Out, MarshalAs(UnmanagedType.Interface)] out object ppv);

        void Item([In] int iItemIndex, [Out] out IntPtr ppidl);

        void ItemCount([In] uint uFlags, [Out] out int pcItems);

        void Items([In] uint uFlags, [In, MarshalAs(UnmanagedType.LPStruct)] Guid riid, [Out, MarshalAs(UnmanagedType.Interface)] out object ppv);

        void GetSelectionMarkedItem([Out] out int piItem);

        void GetFocusedItem([Out] out int piItem);

        void GetItemPosition([In] IntPtr pidl, [Out] out Point ppt);

        void GetSpacing([In, Out] ref Point ppt);

        void GetDefaultSpacing([Out] out Point ppt);

        void GetAutoArrange();

        void SelectItem([In] int iItem, [In] uint dwFlags);

        void SelectAndPositionItems([In] uint cidl, [In] IntPtr apidl, [In] IntPtr apt, [In] uint dwFlags);
    }
}

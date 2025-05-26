﻿// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Microsoft.CmdPal.Ext.Indexer.Native;

public sealed partial class NativeMethods
{
    public static readonly Guid PropertyStoreGUID = new Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99");

    [LibraryImport("ole32.dll")]
    [return: MarshalAs(UnmanagedType.U4)]
    public static partial uint CoCreateInstance(
        Guid rclsid,
        IntPtr pUnkOuter,
        uint dwClsContext,
        Guid riid,
        out IntPtr rReturnedComObject);

    [LibraryImport("shell32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool ShellExecuteEx([MarshalUsing(typeof(SHELLEXECUTEINFOWMarshaller))]ref SHELLEXECUTEINFOW lpExecInfo);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct SHELLEXECUTEINFOW
    {
        public int cbSize;
        public uint fMask;
        public IntPtr hwnd;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string lpVerb;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string lpFile;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string lpParameters;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string lpDirectory;
        public int nShow;
        public IntPtr hInstApp;
        public IntPtr lpIDList;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string lpClass;
        public IntPtr hkeyClass;
        public uint dwHotKey;
        public IntPtr hIcon;
        public IntPtr hProcess;
    }

    [CustomMarshaller(typeof(SHELLEXECUTEINFOW), MarshalMode.ManagedToUnmanagedRef, typeof(SHELLEXECUTEINFOWMarshaller))]
    [CustomMarshaller(typeof(SHELLEXECUTEINFOW), MarshalMode.UnmanagedToManagedRef, typeof(SHELLEXECUTEINFOWMarshaller))]
    public static partial class SHELLEXECUTEINFOWMarshaller
    {
        public static SHELLEXECUTEINFOW ConvertToManaged(IntPtr unmanaged)
        {
            var obj = Marshal.GetObjectForIUnknown(unmanaged);
            return (SHELLEXECUTEINFOW)obj;
        }

        public static void Free(IntPtr? managed)
        {
            if (managed != null)
            {
                Marshal.ReleaseComObject(managed);
            }
        }

        public static nint ConvertToUnmanaged(SHELLEXECUTEINFOW managed)
        {
            var obj = Marshal.GetIUnknownForObject(managed);
            return obj;
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1069:Enums values should not be duplicated", Justification = "No we don't need to fix it")]
    public enum SHOW_WINDOW_CMD : int
    {
        SW_FORCEMINIMIZE = 0x0000000B,
        SW_HIDE = 0x00000000,
        SW_MAXIMIZE = 0x00000003,
        SW_MINIMIZE = 0x00000006,
        SW_RESTORE = 0x00000009,
        SW_SHOW = 0x00000005,
        SW_SHOWDEFAULT = 0x0000000A,
        SW_SHOWMAXIMIZED = 0x00000003,
        SW_SHOWMINIMIZED = 0x00000002,
        SW_SHOWMINNOACTIVE = 0x00000007,
        SW_SHOWNA = 0x00000008,
        SW_SHOWNOACTIVATE = 0x00000004,
        SW_SHOWNORMAL = 0x00000001,
        SW_NORMAL = 0x00000001,
        SW_MAX = 0x0000000B,
        SW_PARENTCLOSING = 0x00000001,
        SW_OTHERZOOM = 0x00000002,
        SW_PARENTOPENING = 0x00000003,
        SW_OTHERUNZOOM = 0x00000004,
        SW_SCROLLCHILDREN = 0x00000001,
        SW_INVALIDATE = 0x00000002,
        SW_ERASE = 0x00000004,
        SW_SMOOTHSCROLL = 0x00000010,
    }
}

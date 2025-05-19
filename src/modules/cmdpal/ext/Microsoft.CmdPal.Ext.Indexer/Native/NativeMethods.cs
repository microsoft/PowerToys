// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using static Microsoft.CmdPal.Ext.Indexer.Native.NativeHelpers;

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
}

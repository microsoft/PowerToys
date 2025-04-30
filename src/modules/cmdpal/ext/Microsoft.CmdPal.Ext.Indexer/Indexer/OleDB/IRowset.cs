// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.CmdPal.Ext.Indexer.Indexer.OleDB;

[ComImport]
[Guid("0c733a7c-2a1c-11ce-ade5-00aa0044773d")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IRowset
{
    [PreserveSig]
    int AddRefRows(
        uint cRows,
        [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] IntPtr[] rghRows,
        [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] uint[] rgRefCounts,
        [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] int[] rgRowStatus);

    [PreserveSig]
    int GetData(
        IntPtr hRow,
        IntPtr hAccessor,
        IntPtr pData);

    [PreserveSig]
    int GetNextRows(
       IntPtr hReserved,
       long lRowsOffset,
       long cRows,
       out uint pcRowsObtained,
       out IntPtr prghRows);

    [PreserveSig]
    int ReleaseRows(
        uint cRows,
        [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] IntPtr[] rghRows,
        IntPtr rgRowOptions,
        [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] uint[] rgRefCounts,
        [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] int[] rgRowStatus);
}

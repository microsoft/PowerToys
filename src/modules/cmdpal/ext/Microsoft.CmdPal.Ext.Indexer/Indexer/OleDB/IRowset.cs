// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Microsoft.CmdPal.Ext.Indexer.Indexer.OleDB;

[Guid("0c733a7c-2a1c-11ce-ade5-00aa0044773d")]
[GeneratedComInterface]
public partial interface IRowset
{
    void AddRefRows(
        uint cRows,
        [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] IntPtr[] rghRows,
        [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] uint[] rgRefCounts,
        [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] int[] rgRowStatus);

    void GetData(
        IntPtr hRow,
        IntPtr hAccessor,
        IntPtr pData);

    void GetNextRows(
       IntPtr hReserved,
       long lRowsOffset,
       long cRows,
       out uint pcRowsObtained,
       out IntPtr prghRows);

    void ReleaseRows(
        uint cRows,
        [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] IntPtr[] rghRows,
        IntPtr rgRowOptions,
        [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] uint[] rgRefCounts,
        [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] int[] rgRowStatus);

    void RestartPosition(nuint hReserved);
}

// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.CmdPal.Ext.Indexer.Indexer.OleDB;

[ComImport]
[Guid("0C733A55-2A1C-11CE-ADE5-00AA0044773D")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IRowsetInfo
{
    [PreserveSig]
    int GetProperties(
        uint cPropertyIDSets,
        [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] DBPROPIDSET[] rgPropertyIDSets,
        out ulong pcPropertySets,
        out IntPtr prgPropertySets);

    [PreserveSig]
    int GetReferencedRowset(
        uint iOrdinal,
        [In] ref Guid riid,
        [Out, MarshalAs(UnmanagedType.Interface)] out object ppReferencedRowset);

    [PreserveSig]
    int GetSpecification(
        [In] ref Guid riid,
        [Out, MarshalAs(UnmanagedType.Interface)] out object ppSpecification);
}

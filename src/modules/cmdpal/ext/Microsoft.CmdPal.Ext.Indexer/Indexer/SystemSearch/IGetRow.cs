// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.Indexer.Native;

namespace Microsoft.CmdPal.Ext.Indexer.Indexer.SystemSearch;

[Guid("0C733AAF-2A1C-11CE-ADE5-00AA0044773D")]
[GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
public partial interface IGetRow
{
    unsafe void GetRowFromHROW([MarshalAs(UnmanagedType.Interface)] object pUnkOuter, nuint hRow, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out IPropertyStore ppUnk);

    unsafe string GetURLFromHROW(nuint hRow);
}

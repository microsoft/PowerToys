// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Microsoft.CmdPal.Ext.Indexer.Indexer.SystemSearch;

[Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
public partial interface IPropertyStore
{
    void GetCount(out uint cProps);

    void GetAt(uint iProp, out PropertyKey pkey);

    unsafe void GetValue(ref PropertyKey key, out IntPtr propVariant);

    int SetValue(ref PropertyKey key, [MarshalUsing(typeof(PROPVARIANTRefMarshaller))] PropVariant spv);

    void Commit();
}

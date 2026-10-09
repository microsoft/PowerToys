// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

[assembly: System.Runtime.CompilerServices.DisableRuntimeMarshalling]

namespace Microsoft.CmdPal.Ext.Indexer.Indexer.SystemSearch;

[Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99")]
[GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
public partial interface IPropertyStore
{
    uint GetCount();

    PropertyKey GetAt(uint iProp);

    void GetValue(in PropertyKey pkey, out PropVariant pv);

    void SetValue(in PropertyKey pkey, in PropVariant pv);

    void Commit();
}

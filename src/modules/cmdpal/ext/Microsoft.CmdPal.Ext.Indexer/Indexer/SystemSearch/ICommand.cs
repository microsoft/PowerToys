// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Microsoft.CmdPal.Ext.Indexer.Indexer.OleDB;

namespace Microsoft.CmdPal.Ext.Indexer.Indexer.SystemSearch;

[Guid("0C733A63-2A1C-11CE-ADE5-00AA0044773D")]
[GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
public partial interface ICommand
{
    void Cancel();

    void Execute([MarshalAs(UnmanagedType.Interface)] object pUnkOuter, ref Guid riid, [Optional][MarshalAs(UnmanagedType.Interface)] object pParams, [Optional]out int pcRowsAffected, out IRowset ppRowset);

    void GetDBSession(ref Guid riid, out IntPtr ppSession);
}
